using PayControl.Application.Dtos;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain;
using PayControl.Domain.Enums;

namespace PayControl.Tests.Modules;

public class TesisVeDenetimTestleri : TestOrtami
{
    [Fact]
    public async Task Bordrodaki_tesisler_otomatik_tanimlanir_tesis_bazli_kontrol_ve_liste_surumu_ayridir()
    {
        var ist = await PersonelAsync("İstanbul Bir", 30000, 70, isyeri: "İstanbul Depo");
        var ank = await PersonelAsync("Ankara Bir", 30000, 71, isyeri: "Ankara Depo");
        var sonuc = await BordroYukleAsync(2026, 10, new BordroSatir(ist, 25000m), new BordroSatir(ank, 24000m));
        Assert.Contains(sonuc.Uyarilar, u => u.Contains("Yeni tesis tanımlandı: Ankara Depo"));

        var tesisler = await S<ITesisServisi>().ListeleAsync();
        Assert.Equal(2, tesisler.Count);
        Assert.All(tesisler, t => Assert.Equal(1, t.AktifPersonel));

        var srv = S<IOdemeOncesiKontrolServisi>();
        var oturumIst = await KontrolTesisAsync(2026, 10, "İstanbul Depo", Talimat(ist, 25000m));
        Assert.Single(oturumIst.Satirlar);
        Assert.False(oturumIst.Satirlar.Single().KritikMi, oturumIst.Satirlar.Single().Gerekce);
        var listeIst = await srv.OnayliListeUretAsync(oturumIst.Id, Muhasebe, Klasor);
        Assert.Contains("_İstanbul-Depo_v1", listeIst.DosyaAdi);

        // Ankara dosyasına İstanbul personeli karışmış → bordroda yok
        var oturumAnk = await KontrolTesisAsync(2026, 10, "Ankara Depo", Talimat(ank, 24000m), Talimat(ist, 25000m));
        var karisan = oturumAnk.Satirlar.Single(s => s.PersonelId == ist.Id);
        Assert.Equal(OdemeKontrolSonucu.BordroUyusmazligi, karisan.Sonuc);
        Assert.Contains("onaylı ödeme listesine alınmış", karisan.Gerekce); // diğer tesisin listesinde zaten var
        await srv.TalimatDuzeltAsync(oturumAnk.Id, 5, null, null, true, "Başka tesisin personeli, listeden çıkarıldı.", Muhasebe);
        var listeAnk = await srv.OnayliListeUretAsync(oturumAnk.Id, Muhasebe, Klasor);
        Assert.Equal(1, listeAnk.Surum); // tesis bazında ayrı sürüm
        Assert.True((await Db.OnayliOdemeListeleri.AsNoTracking().SingleAsync(l => l.Id == listeIst.Id)).Gecerli); // diğer tesisin listesi bozulmaz

        // Mutabakat iki tesisin listesini birlikte kullanır
        await BankaHareketYukleAsync(2026, 10, BankaHareketExcel([Hareket(ist, 25000m, new DateOnly(2026, 11, 5), "I1"), Hareket(ank, 24000m, new DateOnly(2026, 11, 5), "A1")]));
        var donem = await S<IDonemKapanisServisi>().GetOrCreateAsync(2026, 10);
        Assert.Empty(await S<IDonemKapanisServisi>().MutabakatYapAsync(donem.Id, Muhasebe));

        // Çok tesisli dönemde tesis onayları kapanış maddesidir
        var madde = (await S<IDonemKapanisServisi>().KontrolListesiDegerlendirAsync(donem.Id)).Single(m => m.MaddeKodu == "TESIS");
        Assert.False(madde.OtomatikSonuc);
        await S<ITesisServisi>().TesisOnaylaAsync(donem.Id, "İstanbul Depo", "Sorumlu onayı", Muhasebe);
        await S<ITesisServisi>().TesisOnaylaAsync(donem.Id, "Ankara Depo", null, Muhasebe);
        madde = (await S<IDonemKapanisServisi>().KontrolListesiDegerlendirAsync(donem.Id)).Single(m => m.MaddeKodu == "TESIS");
        Assert.True(madde.OtomatikSonuc);

        var durum = await S<ITesisServisi>().DonemTesisDurumuAsync(2026, 10);
        Assert.All(durum, d => { Assert.True(d.Onaylandi); Assert.True(d.GecerliListeVar); Assert.Equal(0m, d.Fark); });
    }

    [Fact]
    public async Task Icra_dosyasi_kesintisi_islenir_eksik_kesinti_kritik_geri_almada_iade_edilir()
    {
        var p = await PersonelAsync("İcra Takipli", 40000, 72);
        var kes = S<IKesintiServisi>();
        await Assert.ThrowsAsync<YetkiHatasiException>(() => kes.IcraDosyasiKaydetAsync(p.Id, "2026/123", "İst. 5. İcra", "Banka", 20000m, 0.25m, 1, new DateOnly(2026, 9, 1), Izleyici));
        var dosya = await kes.IcraDosyasiKaydetAsync(p.Id, "2026/123", "İst. 5. İcra", "Banka", 20000m, 0.25m, 1, new DateOnly(2026, 9, 1), Muhasebe);

        // Kesinti unutulmuş bordro → kritik
        await BordroYukleAsync(2026, 9, new BordroSatir(p, 30000m));
        var o1 = await KontrolAsync(2026, 9, Talimat(p, 30000m));
        Assert.True(o1.Satirlar.Single().KritikMi);
        Assert.Contains("icra kesintisi yok", o1.Satirlar.Single().Gerekce);

        // Doğru kesinti → dosyaya işlenir
        var b2 = await BordroYukleAsync(2026, 10, new BordroSatir(p, 22500m, Icra: 7500m));
        Assert.Equal(7500m, (await kes.IcraDosyalariAsync(p.Id)).Single().Kesilen);
        var o2 = await KontrolAsync(2026, 10, Talimat(p, 22500m));
        Assert.False(o2.Satirlar.Single().KritikMi, o2.Satirlar.Single().Gerekce);

        await S<IBordroIceAktarmaServisi>().GeriAlAsync(b2.IceAktarmaId, Muhasebe);
        Assert.Equal(0m, (await kes.IcraDosyalariAsync(p.Id)).Single().Kesilen);
        _ = dosya;
    }

    [Fact]
    public async Task Bes_caymis_personelden_kesinti_kritik_tanimli_ama_kesinti_yoksa_uyari()
    {
        var a = await PersonelAsync("Bes Var", 30000, 73);
        var c = await PersonelAsync("Bes Caydi", 30000, 74);
        var kes = S<IKesintiServisi>();
        await kes.BesTanimlaAsync(a.Id, 0.03m, new DateOnly(2026, 1, 1), Muhasebe);
        await kes.BesTanimlaAsync(c.Id, 0.03m, new DateOnly(2026, 1, 1), Muhasebe);
        await kes.BesCaymaAsync(c.Id, new DateOnly(2026, 3, 1), Muhasebe);

        await BordroYukleAsync(2026, 10, new BordroSatir(a, 25000m), new BordroSatir(c, 25000m, Bes: 900m));
        var o = await KontrolAsync(2026, 10, Talimat(a, 25000m), Talimat(c, 25000m));
        var sa = o.Satirlar.Single(s => s.PersonelId == a.Id);
        Assert.False(sa.KritikMi);
        Assert.Contains("BES kesintisi yok", sa.Gerekce);
        var sc = o.Satirlar.Single(s => s.PersonelId == c.Id);
        Assert.True(sc.KritikMi);
        Assert.Contains("caymış", sc.Gerekce);
    }

    [Fact]
    public async Task Kesinlesen_fazla_odeme_taksitle_bordrodan_tahsil_edilir()
    {
        var p = await PersonelAsync("Tahsilat Kişi", 30000, 75);
        var defter = S<IMaliHareketServisi>();
        await defter.SupheliFazlaOdemeKaydetAsync(p.Id, 2026, 8, 6000m, "Mükerrer ödeme", "Test", null, "x", Muhasebe);
        await defter.FazlaOdemeKesinlestirAsync(p.Id, 6000m, "İnceleme: mükerrer", Muhasebe);

        var kes = S<IKesintiServisi>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => kes.TahsilatPlaniOlusturAsync(p.Id, 7000m, 3, 2026, 10, "Fazla", Muhasebe));
        var plan = await kes.TahsilatPlaniOlusturAsync(p.Id, 6000m, 3, 2026, 10, "3 ayda geri alınacak", Muhasebe);
        Assert.Equal(3, plan.Taksitler.Count);

        // Taksit bordroya yansımamış → kritik
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 25000m));
        var o = await KontrolAsync(2026, 10, Talimat(p, 25000m));
        Assert.Contains("tahsilat taksiti", o.Satirlar.Single().Gerekce);
        Assert.True(o.Satirlar.Single().KritikMi);

        await BordroYukleAsync(2026, 11, new BordroSatir(p, 23000m, GeriOdeme: 2000m));
        var e = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);
        Assert.Equal(4000m, e.Ozet.KesinlesmisGeriOdemeBorcu);
        Assert.Equal(4000m, (await kes.TahsilatPlanlariAsync(p.Id)).Single().KalanBorc);
    }

    [Fact]
    public async Task Belge_eklenir_butunlugu_dogrulanir_silinemez()
    {
        var p = await PersonelAsync("Belge Kişi", 30000, 76);
        var dosya = Path.Combine(Klasor, "dekont.pdf");
        Directory.CreateDirectory(Klasor);
        await File.WriteAllTextAsync(dosya, "%PDF-1.4 sentetik dekont");
        var srv = S<IBelgeServisi>();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            srv.EkleAsync("Personel", p.Id, p.Id, Path.Combine(Klasor, "virus.exe"), "x", null, Muhasebe));
        var b = await srv.EkleAsync("Personel", p.Id, p.Id, dosya, "Ekim dekontu", "Banka dekontu", Muhasebe);
        Assert.Equal(64, b.Sha256.Length);
        var yol = await srv.DosyaYoluAsync(b.Id);
        Assert.True(File.Exists(yol));

        await File.AppendAllTextAsync(yol, "değiştirildi");
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.DosyaYoluAsync(b.Id));

        await srv.GecersizKilAsync(b.Id, "Yanlış personele yüklendi", Muhasebe);
        Assert.True((await srv.ListeleAsync(personelId: p.Id)).Single().Gecersiz);
        var db = Db;
        db.Belgeler.Remove(await db.Belgeler.FirstAsync());
        await Assert.ThrowsAsync<KapaliDonemIhlaliException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Uyari_merkezi_acik_isleri_toplar_ve_tesise_gore_suzer_denetim_aranabilir()
    {
        var a = await PersonelAsync("Uyarı İst", 30000, 77, isyeri: "İstanbul Depo");
        var b = await PersonelAsync("Uyarı Ank", 30000, 78, isyeri: "Ankara Depo");
        await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = b.Id, FiiliSonCalismaTarihi = new DateOnly(2026, 9, 30), IstenCikisTarihi = new DateOnly(2026, 9, 30)
        }, Muhasebe);
        await S<IMaliHareketServisi>().SupheliFazlaOdemeKaydetAsync(a.Id, 2026, 9, 1500m, "x", "Test", null, "u", Muhasebe);

        var tum = await S<IUyariServisi>().ListeleAsync();
        Assert.Contains(tum, u => u.Kategori == "SGK" && u.PersonelId == b.Id);
        Assert.Contains(tum, u => u.Kategori == "Fazla ödeme" && u.PersonelId == a.Id && u.Seviye == UyariSeviyesi.Kritik);
        Assert.Contains(tum, u => u.Kategori == "Yedek");
        var ist = await S<IUyariServisi>().ListeleAsync("İstanbul Depo");
        Assert.DoesNotContain(ist, u => u.PersonelId == b.Id);

        var d = await S<IDenetimServisi>().AraAsync(new DenetimFiltre { Kullanici = Muhasebe, Islem = "Olusturma" });
        Assert.True(d.Toplam >= 2);
        Assert.Contains(Muhasebe, d.Kullanicilar);
        var excel = await S<IDenetimServisi>().ExcelAsync(new DenetimFiltre { Metin = "Uyarı" });
        Assert.True(excel.Length > 1000);
    }

    private async Task<PayControl.Domain.Entities.OdemeKontrolOturumu> KontrolTesisAsync(int yil, int ay, string tesis, params BankaSatir[] satirlar)
    {
        var bytes = BankaExcel(satirlar);
        using var ms = new MemoryStream(bytes);
        var dosya = Infrastructure.Import.BankaTalimatOkuyucu.OkuDosya(ms);
        return await S<IOdemeOncesiKontrolServisi>().KontrolBaslatAsync(yil, ay, dosya, $"banka-{tesis}.xlsx", Guid.NewGuid().ToString("N"), Muhasebe, null, default, tesis);
    }
}
