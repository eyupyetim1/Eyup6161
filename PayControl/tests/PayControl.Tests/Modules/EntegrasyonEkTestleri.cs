using System.Text;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Enums;
using PayControl.Infrastructure.Data;
using PayControl.Infrastructure.DependencyInjection;

namespace PayControl.Tests.Modules;

/// <summary>Üç modülün birbirine ve mevcut modüllere entegrasyonu için ek senaryolar.</summary>
public class EntegrasyonEkTestleri : TestOrtami
{
    [Fact]
    public async Task Yeniden_ise_alimda_gecmis_silinmez_ve_calisma_donemleri_ayri_incelenir()
    {
        var p = await PersonelAsync("Nazlı Kara", 30000, 30, new DateOnly(2025, 1, 1));
        await BordroYukleAsync(2025, 6, new BordroSatir(p, 30000m));
        await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p.Id, FiiliSonCalismaTarihi = new DateOnly(2025, 6, 30), IstenCikisTarihi = new DateOnly(2025, 6, 30),
            SonBordroDonemYil = 2025, SonBordroDonemAy = 6
        }, Muhasebe);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            S<IPersonelServisi>().YenidenIseAlAsync(p.Id, new DateOnly(2025, 6, 1), null, null, null, null, Muhasebe));
        await S<IPersonelServisi>().YenidenIseAlAsync(p.Id, new DateOnly(2026, 2, 1), "Ankara Tesisi", "Usta", "Mavi A.Ş.", 36000m, Muhasebe);
        await BordroYukleAsync(2026, 2, new BordroSatir(p, 36000m));

        var e = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);
        Assert.Equal(2, e.CalismaDonemleri.Count);
        Assert.Equal(2, e.Hareketler.Count(h => h.IslemTuru == MaliIslemTuru.IseGiris));
        Assert.Single(e.Hareketler, h => h.IslemTuru == MaliIslemTuru.IstenCikis);
        Assert.Equal(66000m, e.Ozet.ToplamHakEdis);

        var ilk = e.CalismaDonemiOzetleri.Single(c => c.Donem.IseGirisTarihi.Year == 2025);
        var ikinci = e.CalismaDonemiOzetleri.Single(c => c.Donem.IseGirisTarihi.Year == 2026);
        Assert.Equal(30000m, ilk.Ozet.ToplamHakEdis);
        Assert.Equal(36000m, ikinci.Ozet.ToplamHakEdis);

        var filtreli = await S<IHesapEkstresiServisi>().GetirAsync(p.Id, new HesapEkstresiFiltre { CalismaDonemiId = ikinci.Donem.Id });
        Assert.All(filtreli.Hareketler, h => Assert.Equal(ikinci.Donem.Id, h.CalismaDonemiId));
        Assert.Equal(36000m, filtreli.Ozet.ToplamHakEdis);
        Assert.Equal(66000m, filtreli.GenelOzet.ToplamHakEdis);
    }

    [Fact]
    public async Task Bordroda_yeni_giris_tarihiyle_gelen_eski_personel_icin_yeni_calisma_donemi_acilir()
    {
        var p = await PersonelAsync("Tolga Bal", 25000, 31, new DateOnly(2025, 1, 1));
        await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p.Id, FiiliSonCalismaTarihi = new DateOnly(2025, 5, 31), IstenCikisTarihi = new DateOnly(2025, 5, 31)
        }, Muhasebe);
        var sonuc = await BordroYukleAsync(2026, 3, new BordroSatir(p, 26000m, Giris: new DateOnly(2026, 3, 1)));
        Assert.Contains(sonuc.Uyarilar, u => u.Contains("yeniden işe giriş"));
        var kart = await Db.Personeller.AsNoTracking().Include(x => x.CalismaDonemleri).FirstAsync(x => x.Id == p.Id);
        Assert.Equal(2, kart.CalismaDonemleri.Count);
        Assert.Equal(CalismaDurumu.Aktif, kart.CalismaDurumu);
    }

    [Fact]
    public async Task Ekstre_tarih_sirket_tesis_ve_islem_turune_gore_filtrelenir()
    {
        var p = await PersonelAsync("Ufuk Tan", 30000, 32, new DateOnly(2025, 1, 1), "Mavi A.Ş.", "İstanbul Tesisi");
        await BordroYukleAsync(2025, 12, new BordroSatir(p, 30000m));
        await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p.Id, FiiliSonCalismaTarihi = new DateOnly(2025, 12, 31), IstenCikisTarihi = new DateOnly(2025, 12, 31)
        }, Muhasebe);
        await S<IPersonelServisi>().YenidenIseAlAsync(p.Id, new DateOnly(2026, 1, 1), "İzmir Tesisi", null, "Yeşil Ltd.", null, Muhasebe);
        await BordroYukleAsync(2026, 1, new BordroSatir(p, 31000m));
        await S<IAvansServisi>().VerAsync(p.Id, new DateOnly(2026, 1, 15), 4000m, 1, 0, null, Muhasebe);

        var srv = S<IHesapEkstresiServisi>();
        var sirket = await srv.GetirAsync(p.Id, new HesapEkstresiFiltre { Sirket = "yeşil ltd." });
        Assert.NotEmpty(sirket.Hareketler);
        Assert.All(sirket.Hareketler, h => Assert.Equal("Yeşil Ltd.", h.Sirket));
        Assert.Equal(31000m, sirket.Ozet.ToplamHakEdis);

        var tesis = await srv.GetirAsync(p.Id, new HesapEkstresiFiltre { Isyeri = "İstanbul Tesisi" });
        Assert.Equal(30000m, tesis.Ozet.ToplamHakEdis);

        var tur = await srv.GetirAsync(p.Id, new HesapEkstresiFiltre { IslemTuru = MaliIslemTuru.AvansVerildi });
        Assert.Equal(MaliIslemTuru.AvansVerildi, Assert.Single(tur.Hareketler).IslemTuru);

        var tarih = await srv.GetirAsync(p.Id, new HesapEkstresiFiltre { Baslangic = new DateOnly(2026, 1, 1), Bitis = new DateOnly(2026, 1, 31) });
        Assert.All(tarih.Hareketler, h => Assert.InRange(h.IslemTarihi, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
        Assert.Contains("Tarih", tarih.FiltreAciklamasi);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            srv.GetirAsync(p.Id, new HesapEkstresiFiltre { Baslangic = new DateOnly(2026, 2, 1), Bitis = new DateOnly(2026, 1, 1) }));
    }

    [Fact]
    public async Task Pdf_cok_sayfali_ve_turkce_karakterli_uretilir()
    {
        var p = await PersonelAsync("Şükrü Güçlüoğlu İnce", 30000, 33);
        for (var i = 0; i < 70; i++)
            await S<IMaliHareketServisi>().BordroDisiHakEdisAsync(p.Id, MaliIslemTuru.Prim, new DateOnly(2026, 1, 1).AddDays(i), 2026, 1 + i % 10,
                100m + i, $"Prim çalışması {i}", null, Muhasebe);

        var pdf = await S<IHesapEkstresiServisi>().PdfOlarakAktarAsync(p.Id);
        var metin = Encoding.Latin1.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", metin);
        Assert.EndsWith("%%EOF\n", metin);
        Assert.Contains("/Count 2", metin); // 70+ satır iki sayfaya sığar; kesilmez
        Assert.Contains("/Differences [208 /Gbreve 221 /Idotaccent 222 /Scedilla 240 /gbreve 253 /dotlessi 254 /scedilla]", metin);
        // "Ş" ISO-8859-9'da 0xDE (oktal 336) olarak kodlanır, ASCII'ye indirgenmez
        Assert.Contains("\\336\\374kr\\374", metin);
        Assert.Contains("Prim \\347al\\375\\376mas\\375 69", metin); // son satır da basıldı
    }

    [Fact]
    public async Task Onayli_liste_yuklenen_banka_formatinda_uretilir_ve_kaynak_kayitlari_saklanir()
    {
        var a = await PersonelAsync("Arda Gök", 30000, 34);
        var b = await PersonelAsync("Berk Su", 30000, 35);
        var bordro = await BordroYukleAsync(2026, 10, new BordroSatir(a, 25000m), new BordroSatir(b, 24000m));
        var oturum = await KontrolAsync(2026, 10, Talimat(a, 25000m), Talimat(b, 24000m));
        Assert.Equal(bordro.IceAktarmaId, oturum.BordroIceAktarmaId);

        var liste = await S<IOdemeOncesiKontrolServisi>().OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor);
        using var wb = new XLWorkbook(liste.DosyaYolu!);
        var ws = wb.Worksheet("EFT Talimat");
        Assert.Equal("MAVİ BANK TOPLU ÖDEME TALİMATI", ws.Cell(1, 1).GetString());
        Assert.Equal(new[] { "SIRA", "ALICI ADI SOYADI", "T.C. KİMLİK NO", "IBAN", "TUTAR", "AÇIKLAMA" },
            Enumerable.Range(1, 6).Select(c => ws.Cell(3, c).GetString()));
        Assert.Equal(a.Iban, ws.Cell(4, 4).GetString());
        Assert.Equal(25000m, ws.Cell(4, 5).GetValue<decimal>());
        Assert.Equal("MAAŞ", ws.Cell(4, 6).GetString());
        Assert.True(wb.Worksheets.Contains("Kontrol Bilgisi"));

        var kayitli = await Db.OnayliOdemeListeleri.AsNoTracking().Include(l => l.Satirlar).SingleAsync();
        Assert.Equal(bordro.IceAktarmaId, kayitli.BordroIceAktarmaId);
        Assert.Equal(2, kayitli.Satirlar.Count);
        Assert.Contains("ALICI ADI SOYADI | T.C. KİMLİK NO | IBAN | TUTAR", kayitli.KaynakFormatNotu);
        Assert.Equal(64, kayitli.DosyaHashSha256!.Length);
    }

    [Fact]
    public async Task Liste_uretildikten_sonra_degisiklik_yeni_surum_olusturur_eski_gecersiz_olur()
    {
        var a = await PersonelAsync("Cem Uz", 30000, 36);
        await BordroYukleAsync(2026, 10, new BordroSatir(a, 25000m));
        var oturum = await KontrolAsync(2026, 10, Talimat(a, 25000m));
        var srv = S<IOdemeOncesiKontrolServisi>();
        var v1 = await srv.OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor);
        Assert.Equal(1, v1.Surum);

        await srv.TalimatDuzeltAsync(oturum.Id, 4, a.Iban, 25000m, false, "IBAN boşlukları düzeltildi, yeniden onay.", Muhasebe);
        var eski = await Db.OnayliOdemeListeleri.AsNoTracking().SingleAsync(l => l.Id == v1.Id);
        Assert.False(eski.Gecerli);
        Assert.Contains("düzeltildi", eski.GecersizlikNedeni);

        var v2 = await srv.OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor);
        Assert.Equal(2, v2.Surum);
        Assert.True(File.Exists(v1.DosyaYolu));  // eski dosya silinmez
        Assert.Equal(2, (await srv.DonemListeleriAsync(2026, 10)).Count);
        var talimat = (await srv.GetirAsync(oturum.Id))!.Talimatlar.Single();
        Assert.True(talimat.Duzeltildi);
        Assert.Equal(Muhasebe, talimat.DuzeltenKullanici);
    }

    [Fact]
    public async Task Kontrolden_sonra_veri_degisirse_liste_uretilmez()
    {
        var a = await PersonelAsync("Derya Öz", 30000, 37);
        await BordroYukleAsync(2026, 10, new BordroSatir(a, 25000m));
        var oturum = await KontrolAsync(2026, 10, Talimat(a, 25000m));
        Assert.False(oturum.Satirlar.Single().KritikMi);

        // Kontrolden sonra kasadan ödeme yapıldı
        await S<IBankaOdemeServisi>().KasaOdemesiKaydetAsync(a.Id, new DateOnly(2026, 11, 2), 25000m, 2026, 10, "Acil kasa ödemesi", "K-55", Muhasebe);
        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            S<IOdemeOncesiKontrolServisi>().OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor));
        Assert.Contains("değişti", hata.Message);
        var guncel = (await S<IOdemeOncesiKontrolServisi>().GetirAsync(oturum.Id))!;
        Assert.Equal(OdemeKontrolSonucu.MukerrerOdemeRiski, guncel.Satirlar.Single().Sonuc);
        Assert.Equal(0, await Db.OnayliOdemeListeleri.CountAsync());
    }

    [Fact]
    public async Task Cikis_sonrasi_doneme_odeme_ve_cikis_ayinda_fazla_gun_kritik_isaretlenir()
    {
        var eski = await PersonelAsync("Erkan Yol", 30000, 38, new DateOnly(2024, 1, 1));
        var cikan = await PersonelAsync("Figen Dal", 30000, 39, new DateOnly(2024, 1, 1));
        await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = eski.Id, FiiliSonCalismaTarihi = new DateOnly(2026, 8, 31), IstenCikisTarihi = new DateOnly(2026, 8, 31)
        }, Muhasebe);
        await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = cikan.Id, FiiliSonCalismaTarihi = new DateOnly(2026, 10, 10), IstenCikisTarihi = new DateOnly(2026, 10, 10)
        }, Muhasebe);
        await BordroYukleAsync(2026, 10, new BordroSatir(eski, 30000m), new BordroSatir(cikan, 30000m, Gun: 30));

        var oturum = await KontrolAsync(2026, 10, Talimat(eski, 30000m), Talimat(cikan, 30000m));
        var s1 = oturum.Satirlar.Single(s => s.PersonelId == eski.Id);
        Assert.Equal(OdemeKontrolSonucu.IstenCikisKontrolu, s1.Sonuc);
        Assert.True(s1.KritikMi);
        var s2 = oturum.Satirlar.Single(s => s.PersonelId == cikan.Id);
        Assert.Equal(OdemeKontrolSonucu.FazlaOdemeRiski, s2.Sonuc);
        Assert.Contains("en fazla 10 gün", s2.Gerekce);
    }

    [Fact]
    public async Task Avans_mahsubu_bordroya_yansimazsa_fazla_odeme_riski_kritiktir()
    {
        var p = await PersonelAsync("Gizem Er", 30000, 40);
        await S<IAvansServisi>().VerAsync(p.Id, new DateOnly(2026, 9, 1), 10000m, 2, 5000m, null, Muhasebe);
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 30000m)); // avans kesintisi unutulmuş
        var oturum = await KontrolAsync(2026, 10, Talimat(p, 30000m));
        var s = oturum.Satirlar.Single();
        Assert.True(s.KritikMi);
        Assert.Equal(OdemeKontrolSonucu.FazlaOdemeRiski, s.Sonuc);
        Assert.Contains("Avans mahsubu eksik", s.Gerekce);
    }

    [Fact]
    public async Task Bordroda_olmayan_ve_kimligi_belirsiz_banka_satirlari_raporlanir()
    {
        var a = await PersonelAsync("Hakan Su", 30000, 41);
        var bordrosuz = await PersonelAsync("İpek Bay", 30000, 42);
        await BordroYukleAsync(2026, 10, new BordroSatir(a, 25000m));
        var oturum = await KontrolAsync(2026, 10,
            Talimat(a, 25000m), Talimat(bordrosuz, 1000m),
            new BankaSatir(null, "Hakan Su", a.Iban, 300m)); // TC'siz satır, IBAN ile a'ya bağlanır → mükerrer

        Assert.Equal(OdemeKontrolSonucu.BordroUyusmazligi, oturum.Satirlar.Single(s => s.PersonelId == bordrosuz.Id).Sonuc);
        var sa = oturum.Satirlar.Single(s => s.PersonelId == a.Id);
        Assert.Equal(OdemeKontrolSonucu.MukerrerOdemeRiski, sa.Sonuc);
        Assert.Contains("IBAN ve ad üzerinden", sa.Gerekce);
    }

    [Fact]
    public async Task Mutabakat_farklari_cozulmeden_donem_kapanmaz_istisna_yetki_ister()
    {
        var a = await PersonelAsync("Kaan Ilgaz", 30000, 43);
        var b = await PersonelAsync("Lale Nur", 28000, 44);
        await BordroYukleAsync(2026, 10, new BordroSatir(a, 25000m), new BordroSatir(b, 22000m));
        var oturum = await KontrolAsync(2026, 10, Talimat(a, 25000m), Talimat(b, 22000m));
        await S<IOdemeOncesiKontrolServisi>().OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor);

        await BankaHareketYukleAsync(2026, 10, BankaHareketExcel([
            Hareket(a, 25000m, new DateOnly(2026, 11, 5), "D1"),
            Hareket(b, 20000m, new DateOnly(2026, 11, 5), "D2"),             // eksik
            new BankaHareketSatiri(0, new DateOnly(2026, 11, 5), null, "Tanınmayan", Iban(888), 750m, "D3", "EFT") // eşleşmeyen
        ]));

        var srv = S<IDonemKapanisServisi>();
        var donem = await srv.GetOrCreateAsync(2026, 10);
        var farklar = await srv.MutabakatYapAsync(donem.Id, Muhasebe);
        var eksik = Assert.Single(farklar, f => f.FarkTuru == MutabakatFarkTuru.EksikOdeme);
        Assert.Equal(-2000m, eksik.Fark);
        var eslesmeyen = Assert.Single(farklar, f => f.FarkTuru == MutabakatFarkTuru.EslesmeyenBankaHareketi);
        Assert.Single(farklar, f => f.FarkTuru == MutabakatFarkTuru.OnayliListedeOdenmedi && f.PersonelId == b.Id);
        Assert.Equal(MaasDonemDurumu.IncelemeBekliyor, (await Db.MaasDonemKapanislari.AsNoTracking().FirstAsync(d => d.Id == donem.Id)).Durum);

        // Mutabakat tekrarlanırsa fark çoğalmaz
        Assert.Equal(farklar.Count, (await srv.MutabakatYapAsync(donem.Id, Muhasebe)).Count);

        // Kapanış engellenir
        var maddeler = await srv.KontrolListesiDegerlendirAsync(donem.Id);
        var acikFark = maddeler.Single(m => m.MaddeKodu == "ACIK_FARK");
        Assert.False(acikFark.OtomatikSonuc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.MaddeTamamlaAsync(acikFark.Id, Muhasebe));

        await Assert.ThrowsAsync<PayControl.Domain.YetkiHatasiException>(() =>
            srv.FarkCozAsync(eslesmeyen.Id, FarkDurumu.Istisna, "Şirket dışı EFT, maaş değil.", Muhasebe));
        await srv.FarkCozAsync(eslesmeyen.Id, FarkDurumu.Istisna, "Tedarikçi EFT'si yanlış dosyaya düşmüş, maaş değil.", Yonetici);
        foreach (var f in farklar.Where(f => f.Id != eslesmeyen.Id))
            await srv.FarkCozAsync(f.Id, FarkDurumu.Cozuldu, "Kalan 2.000 TL 12 Kasım'da ödenecek, takipte.", Muhasebe);

        Assert.Equal(MaasDonemDurumu.KapanisaHazir, (await Db.MaasDonemKapanislari.AsNoTracking().FirstAsync(d => d.Id == donem.Id)).Durum);
        await DonemiKapatAsync(2026, 10);
        var rapor = await srv.KapanisRaporuAsync(donem.Id);
        Assert.Equal(47000m, rapor.ToplamBeklenenMaas);
        Assert.Equal(45750m, rapor.ToplamBankaOdemesi);
        Assert.Equal(2000m, rapor.ToplamEksikOdeme);
        Assert.Contains(rapor.Istisnalar, i => i.Contains("Tedarikçi"));
        var excel = await srv.KapanisRaporuExcelAsync(donem.Id);
        using var wb = new XLWorkbook(new MemoryStream(excel));
        Assert.True(wb.Worksheets.Contains("Mutabakat Farkları"));
    }

    [Fact]
    public async Task Onceki_ay_karsilastirmasi_nedenleri_kaynak_kayitlarla_gosterir()
    {
        var kalan = await PersonelAsync("Mert Ak", 30000, 45, new DateOnly(2025, 1, 1));
        var ayrilan = await PersonelAsync("Nil Ece", 30000, 46, new DateOnly(2025, 1, 1));
        var gunDegisen = await PersonelAsync("Onur Taş", 30000, 47, new DateOnly(2025, 1, 1));
        await BordroYukleAsync(2026, 9, new BordroSatir(kalan, 30000m), new BordroSatir(ayrilan, 30000m), new BordroSatir(gunDegisen, 30000m));

        await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = ayrilan.Id, FiiliSonCalismaTarihi = new DateOnly(2026, 9, 30), IstenCikisTarihi = new DateOnly(2026, 9, 30), CikisNedeni = "Emeklilik"
        }, Muhasebe);
        var yeni = await PersonelAsync("Pelin Yaz", 32000, 48, new DateOnly(2026, 10, 1));
        await BordroYukleAsync(2026, 10, new BordroSatir(kalan, 30000m), new BordroSatir(yeni, 32000m), new BordroSatir(gunDegisen, 25000m, Gun: 25));

        var k = await S<IDonemKapanisServisi>().OncekiAyKarsilastirAsync(2026, 10);
        Assert.Equal((3, 3, 1, 1), (k.OncekiPersonelSayisi, k.BuAyPersonelSayisi, k.IstenAyrilan, k.IseBaslayan));
        Assert.Contains("Emeklilik", k.Detaylar.Single(d => d.Tur == "İşten ayrılan").Kaynak);
        Assert.Contains("01.10.2026", k.Detaylar.Single(d => d.Tur == "İşe başlayan").Kaynak);
        Assert.Contains("gün 30 → 25", k.Detaylar.Single(d => d.Tur == "Net tutar değişimi").Kaynak);
        // Ayrıştırma toplamı = toplam fark
        Assert.Equal(k.ToplamFark, k.AyrilanEtkisi + k.BaslayanEtkisi + k.MaasDegisikligiEtkisi + k.DigerNetEtkisi);
        Assert.Equal(-3000m, k.ToplamFark);
    }

    [Fact]
    public async Task Kontrol_maddesi_otomatik_kontrol_basarisizsa_gerekce_ister()
    {
        var p = await PersonelAsync("Rana Gül", 30000, 49);
        var srv = S<IDonemKapanisServisi>();
        var donem = await srv.GetOrCreateAsync(2026, 10);
        var maddeler = await srv.KontrolListesiDegerlendirAsync(donem.Id);
        var bordroMaddesi = maddeler.Single(m => m.MaddeKodu == "BORDRO");
        Assert.False(bordroMaddesi.OtomatikSonuc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.MaddeTamamlaAsync(bordroMaddesi.Id, Muhasebe));
        await srv.MaddeTamamlaAsync(bordroMaddesi.Id, Muhasebe, "Bu dönem bordro dış serviste, belge no 45.");
        Assert.True((await Db.DonemKontrolMaddeleri.AsNoTracking().FirstAsync(m => m.Id == bordroMaddesi.Id)).ElleOnaylandi);

        var yon = maddeler.Single(m => m.MaddeKodu == "YONETICI");
        await Assert.ThrowsAsync<PayControl.Domain.YetkiHatasiException>(() => srv.MaddeTamamlaAsync(yon.Id, Muhasebe));
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.MaddeTamamlaAsync(yon.Id, Yonetici)); // diğerleri eksik
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.KapatAsync(donem.Id, Yonetici, null));
        // Durum makinesi: geri dönüş ve doğrudan "Kapatıldı" yapılamaz
        await srv.DurumGuncelleAsync(donem.Id, MaasDonemDurumu.BordroYuklendi, Muhasebe);
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.DurumGuncelleAsync(donem.Id, MaasDonemDurumu.Hazirlaniyor, Muhasebe));
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.DurumGuncelleAsync(donem.Id, MaasDonemDurumu.Kapatildi, Yonetici));
        _ = p;
    }

    [Fact]
    public async Task Eski_surum_veritabani_veri_kaybi_olmadan_yukseltilir()
    {
        var p = await PersonelAsync("Sinan Ok", 30000, 50);
        await S<IAvansServisi>().VerAsync(p.Id, new DateOnly(2026, 9, 1), 1000m, 1, 0, null, Muhasebe);
        await Sp.DisposeAsync();
        SqliteConnection.ClearAllPools();

        // 1.1 şemasını taklit et: yeni tablo ve kolonları kaldır
        await using (var conn = new SqliteConnection($"Data Source={DbYolu};Pooling=False"))
        {
            await conn.OpenAsync();
            foreach (var sql in new[]
                     {
                         "DROP TABLE MutabakatFarklari", "DROP TABLE DonemKapanisOzetleri",
                         "DROP INDEX IF EXISTS IX_MaliHareketler_PersonelId_IslemTarihi",
                         "ALTER TABLE MaliHareketler DROP COLUMN Sirket", "ALTER TABLE MaliHareketler DROP COLUMN TersKayitMi"
                     })
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        var services = new ServiceCollection();
        services.AddPayControlInfrastructure(DbYolu);
        Sp = services.BuildServiceProvider();
        await ServiceCollectionExtensions.InitializeDatabaseAsync(Sp);

        var db = Sp.GetRequiredService<PayControlDbContext>();
        Assert.Equal(0, await db.MutabakatFarklari.CountAsync());
        var hareket = await db.MaliHareketler.AsNoTracking().SingleAsync(h => h.IslemTuru == MaliIslemTuru.AvansVerildi);
        Assert.Null(hareket.Sirket);
        Assert.False(hareket.TersKayitMi);
        var e = await Sp.GetRequiredService<IHesapEkstresiServisi>().GetirAsync(p.Id);
        Assert.Equal(1000m, e.Ozet.AcikAvansBorcu);
    }
}
