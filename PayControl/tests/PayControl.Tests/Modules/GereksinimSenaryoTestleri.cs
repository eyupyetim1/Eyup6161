using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Tests.Modules;

/// <summary>
/// Bölüm 19 — zorunlu 12 otomatik test senaryosu. Tüm veriler sentetiktir.
/// </summary>
public class GereksinimSenaryoTestleri : TestOrtami
{
    // 1. Avans verildiğinde personel ekstresinde görünüyor mu?
    [Fact]
    public async Task T01_Avans_verildiginde_ekstrede_gorunur()
    {
        var p = await PersonelAsync("Ayşe Yılmaz", 30000, 1);
        var avans = await S<IAvansServisi>().VerAsync(p.Id, new DateOnly(2026, 9, 5), 20000m, 4, 5000m, "Taksitli", Muhasebe, "Kira");

        var e = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);
        var satir = Assert.Single(e.Hareketler, h => h.IslemTuru == MaliIslemTuru.AvansVerildi);
        Assert.Equal(20000m, satir.Tutar);
        Assert.Equal(BakiyeTuru.AvansBorcu, satir.BakiyeTuru);
        Assert.Equal(avans.Id.ToString(), satir.KaynakReferans);
        Assert.Equal(Muhasebe, satir.KullaniciAdi);
        Assert.Equal(p.CalismaDonemleri.Single().Id, satir.CalismaDonemiId);
        Assert.Equal("Mavi A.Ş.", satir.Sirket);
        Assert.Equal(20000m, e.Ozet.AcikAvansBorcu);
        // Avans maaş alacağına karışmaz
        Assert.Equal(0m, e.Ozet.ToplamHakEdis);
        Assert.Equal(0m, e.Ozet.OdenmemisHakEdis);
        // Mahsup planı: 4 taksit, toplam = avans
        Assert.Equal(20000m, avans.Mahsuplar.Sum(m => m.PlanlananTutar));
    }

    // 2. Avans maaştan mahsup edildiğinde bakiye doğru azalıyor mu?
    [Fact]
    public async Task T02_Avans_bordrodan_mahsup_edilince_bakiye_dogru_azalir_ve_cift_mahsup_olmaz()
    {
        var p = await PersonelAsync("Mehmet Kaya", 30000, 2);
        var avansSrv = S<IAvansServisi>();
        var avans = await avansSrv.VerAsync(p.Id, new DateOnly(2026, 9, 10), 20000m, 4, 5000m, "Taksitli", Muhasebe);

        var bordro = await BordroYukleAsync(2026, 10, new BordroSatir(p, 25000m, Avans: 5000m));

        var guncel = (await avansSrv.ListeleAsync(p.Id)).Single();
        Assert.Equal(15000m, guncel.KalanBorc);
        var plan = guncel.Mahsuplar.Single(m => m.DonemYil == 2026 && m.DonemAy == 10);
        Assert.Equal(5000m, plan.GerceklesenTutar);
        Assert.Equal(bordro.IceAktarmaId, plan.KaynakIceAktarmaId);

        var e = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);
        Assert.Equal(15000m, e.Ozet.AcikAvansBorcu);
        Assert.Equal(guncel.KalanBorc, e.Ozet.AcikAvansBorcu); // avans kartı = ekstre
        Assert.Single(e.Hareketler, h => h.IslemTuru == MaliIslemTuru.AvansMahsup);

        // Aynı dönem için elle ikinci mahsup engellenir
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            avansSrv.MahsupGerceklestirAsync(avans.Id, 2026, 10, 5000m, Muhasebe));

        // Bordro geri alınırsa mahsup iade edilir, kayıtlar silinmez (ters kayıt)
        Assert.True(await S<IBordroIceAktarmaServisi>().GeriAlAsync(bordro.IceAktarmaId, Muhasebe));
        Assert.Equal(20000m, (await avansSrv.ListeleAsync(p.Id)).Single().KalanBorc);
        var e2 = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);
        Assert.Equal(20000m, e2.Ozet.AcikAvansBorcu);
        Assert.Equal(0m, e2.Ozet.ToplamHakEdis);
        Assert.Contains(e2.Hareketler, h => h.TersKayitMi && h.IslemTuru == MaliIslemTuru.AvansMahsup);
        Assert.Contains(e2.Hareketler, h => !h.TersKayitMi && h.IslemTuru == MaliIslemTuru.AvansMahsup);
        Assert.Equal(1, await Db.MaasDonemKayitlari.IgnoreQueryFilters().CountAsync(m => m.PersonelId == p.Id && m.IptalEdildi));
    }

    // 3. Aynı banka hareketi iki kez içe aktarıldığında mükerrer kayıt engelleniyor mu?
    [Fact]
    public async Task T03_Ayni_banka_hareketi_iki_kez_ice_aktarilinca_mukerrer_engellenir()
    {
        var a = await PersonelAsync("Ali Demir", 30000, 3);
        var b = await PersonelAsync("Zeynep Şahin", 28000, 4);
        await BordroYukleAsync(2026, 10, new BordroSatir(a, 25000m), new BordroSatir(b, 23000m));

        var tarih = new DateOnly(2026, 11, 5);
        var excel = BankaHareketExcel([
            Hareket(a, 25000m, tarih, "DKT-1001"),
            Hareket(b, 23000m, tarih) // referanssız hareket
        ]);

        var ilk = await BankaHareketYukleAsync(2026, 10, excel);
        Assert.Equal(2, ilk.Eklenen);

        var ikinci = await BankaHareketYukleAsync(2026, 10, excel);
        Assert.Equal(0, ikinci.Eklenen);
        Assert.Equal(2, ikinci.MukerrerAtlanan);
        Assert.True(ikinci.AyniDosyaDahaOnceYuklendi);

        // Aynı hareketi içeren farklı bir döküm (ek bir yeni hareketle)
        var c = await PersonelAsync("Can Öztürk", 20000, 5);
        var excel2 = BankaHareketExcel([Hareket(a, 25000m, tarih, "DKT-1001"), Hareket(c, 1000m, tarih, "DKT-2000")]);
        var ucuncu = await BankaHareketYukleAsync(2026, 10, excel2, "ekstre-2.xlsx");
        Assert.Equal(1, ucuncu.Eklenen);
        Assert.Equal(1, ucuncu.MukerrerAtlanan);

        Assert.Equal(3, await Db.BankaOdemeleri.CountAsync());
        Assert.Equal(1, await Db.MaliHareketler.CountAsync(h => h.PersonelId == a.Id && h.IslemTuru == MaliIslemTuru.BankaMaasOdemesi));

        // Tek kayıt girişinde de aynı referans engellenir
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            S<IBankaOdemeServisi>().KaydetAsync(a.Id, tarih, 25000m, 2026, 10, a.Iban, "DKT-1001", Muhasebe));
    }

    // 4. İşten ayrılan personelin hak ettiği son ücret doğru döneme bağlanıyor mu?
    [Fact]
    public async Task T04_Isten_ayrilanin_son_ucreti_dogru_doneme_baglanir()
    {
        var p = await PersonelAsync("Hasan Çelik", 30000, 6, new DateOnly(2024, 3, 1));
        await BordroYukleAsync(2026, 9, new BordroSatir(p, 30000m));
        var cikis = await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p.Id,
            FiiliSonCalismaTarihi = new DateOnly(2026, 10, 15),
            IstenCikisTarihi = new DateOnly(2026, 10, 15),
            SonBordroDonemYil = 2026,
            SonBordroDonemAy = 10,
            SgkCikisDurumu = SgkCikisDurumu.Verildi,
            SgkKullaniciOnayi = true,
            CikisNedeni = "İstifa"
        }, Muhasebe);
        Assert.Equal((2026, 10), (cikis.SonBordroDonemYil, cikis.SonBordroDonemAy));

        await BordroYukleAsync(2026, 10, new BordroSatir(p, 15000m, Gun: 15, Cikis: new DateOnly(2026, 10, 15)));

        var e = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);
        var hakEdis = Assert.Single(e.Hareketler, h => h.IslemTuru == MaliIslemTuru.MaasHakEdis && h.DonemAy == 10);
        Assert.Equal(15000m, hakEdis.Tutar);
        Assert.Equal(p.CalismaDonemleri.Single().Id, hakEdis.CalismaDonemiId);
        Assert.Contains(e.Hareketler, h => h.IslemTuru == MaliIslemTuru.IstenCikis);

        // Ödeme kontrolü: çıkış ayının 15 günlük hak edişi kritik sayılmaz
        var oturum = await KontrolAsync(2026, 10, Talimat(p, 15000m));
        var satir = oturum.Satirlar.Single(s => s.PersonelId == p.Id);
        Assert.False(satir.KritikMi, satir.Gerekce);
        Assert.Equal(OdemeKontrolSonucu.IstenCikisKontrolu, satir.Sonuc);

        // Son ücret çıkış ayından sonraki bir döneme bağlanamaz
        var p2 = await PersonelAsync("Elif Arslan", 30000, 7);
        await Assert.ThrowsAsync<InvalidOperationException>(() => S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p2.Id,
            FiiliSonCalismaTarihi = new DateOnly(2026, 10, 15),
            IstenCikisTarihi = new DateOnly(2026, 10, 15),
            SonBordroDonemYil = 2026,
            SonBordroDonemAy = 11
        }, Muhasebe));
    }

    // 5. Çıkış sonrasında hak edilmiş ücretin ödenmesi yanlışlıkla zarar yazılıyor mu?
    [Fact]
    public async Task T05_Cikis_sonrasi_hak_edilmis_ucret_odemesi_zarar_yazilmaz()
    {
        var p = await PersonelAsync("Fatma Koç", 30000, 8, new DateOnly(2024, 3, 1));
        var cikis = await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p.Id,
            FiiliSonCalismaTarihi = new DateOnly(2026, 10, 15),
            IstenCikisTarihi = new DateOnly(2026, 10, 15),
            SonBordroDonemYil = 2026,
            SonBordroDonemAy = 10
        }, Muhasebe);
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 15000m, Gun: 15));

        // Çıkıştan sonra (5 Kasım) Ekim hak edişi ödeniyor
        await BankaHareketYukleAsync(2026, 10, BankaHareketExcel([Hareket(p, 15000m, new DateOnly(2026, 11, 5), "DKT-5")]));
        var donem = await S<IDonemKapanisServisi>().GetOrCreateAsync(2026, 10);
        var farklar = await S<IDonemKapanisServisi>().MutabakatYapAsync(donem.Id, Muhasebe);

        Assert.DoesNotContain(farklar, f => f.PersonelId == p.Id);
        var e = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);
        Assert.Equal(0m, e.Ozet.IncelemeBekleyenFazlaOdeme);
        Assert.Equal(0m, e.Ozet.KesinlesmisGeriOdemeBorcu);
        Assert.Equal(0m, e.Ozet.OdenmemisHakEdis);
        Assert.Equal(OdemeSiniflandirma.NormalHakEdisOdemesi,
            (await S<IIstenCikisServisi>().OdemeleriInceleAsync(cikis.Id)).Siniflandirma);
        Assert.Equal(0m, (await S<IDonemKapanisServisi>().KapanisRaporuAsync(donem.Id)).CikisSonrasiSupheli);
    }

    // 6. Daha önce ödenmiş maaşın tekrar ödenmesi yakalanıyor mu?
    [Fact]
    public async Task T06_Daha_once_odenmis_maasin_tekrar_odenmesi_yakalanir()
    {
        var p = await PersonelAsync("Burak Aydın", 30000, 9);
        var q = await PersonelAsync("Seda Polat", 26000, 10);
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 25000m), new BordroSatir(q, 22000m));

        // a) Bankaya zaten ödenmiş dönem için yeni talimat
        await S<IBankaOdemeServisi>().KaydetAsync(p.Id, new DateOnly(2026, 11, 3), 25000m, 2026, 10, p.Iban, "DKT-77", Muhasebe);
        var oturum = await KontrolAsync(2026, 10, Talimat(p, 25000m), Talimat(q, 22000m));
        var sp = oturum.Satirlar.Single(s => s.PersonelId == p.Id);
        Assert.Equal(OdemeKontrolSonucu.MukerrerOdemeRiski, sp.Sonuc);
        Assert.True(sp.KritikMi);
        Assert.Contains("daha önce", sp.Gerekce);

        // b) İkinci kez onaylı listeye alma
        var q2 = oturum.Satirlar.Single(s => s.PersonelId == q.Id);
        Assert.False(q2.KritikMi, q2.Gerekce);
        await S<IOdemeOncesiKontrolServisi>().TalimatDuzeltAsync(oturum.Id, int.Parse(sp.TalimatSatirNolari!), null, null, true,
            "Bu kişinin maaşı 3 Kasım'da ödendi; listeden çıkarıldı.", Muhasebe);
        var guncel = (await S<IOdemeOncesiKontrolServisi>().GetirAsync(oturum.Id))!;
        var pSatiri = guncel.Satirlar.Single(s => s.PersonelId == p.Id);
        await S<IOdemeOncesiKontrolServisi>().IstisnaOnaylaAsync(pSatiri.Id, "Maaş daha önce ödendi, bu listede ödeme yok.", Yonetici);
        var liste = await S<IOdemeOncesiKontrolServisi>().OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor);
        Assert.Single(liste.Satirlar);

        var oturum2 = await KontrolAsync(2026, 10, Talimat(q, 22000m));
        var tekrar = oturum2.Satirlar.Single(s => s.PersonelId == q.Id);
        Assert.Equal(OdemeKontrolSonucu.MukerrerOdemeRiski, tekrar.Sonuc);
        Assert.Contains("onaylı ödeme listesine alınmış", tekrar.Gerekce);

        // c) Gerçekleşen çift ödeme mutabakatta şüpheli fazla ödeme olur
        await BankaHareketYukleAsync(2026, 10, BankaHareketExcel([Hareket(p, 25000m, new DateOnly(2026, 11, 6), "DKT-78")]));
        var donem = await S<IDonemKapanisServisi>().GetOrCreateAsync(2026, 10);
        var farklar = await S<IDonemKapanisServisi>().MutabakatYapAsync(donem.Id, Muhasebe);
        var fazla = Assert.Single(farklar, f => f.PersonelId == p.Id && f.FarkTuru == MutabakatFarkTuru.FazlaOdeme);
        Assert.Equal(25000m, fazla.Fark);
        var e = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);
        Assert.Equal(25000m, e.Ozet.IncelemeBekleyenFazlaOdeme);
        Assert.Equal(0m, e.Ozet.KesinlesmisGeriOdemeBorcu); // inceleme sonucu olmadan kesin borç yazılmaz
        Assert.Equal(0m, e.Ozet.OdenmemisHakEdis);
    }

    // 7. Bankaya gönderilecek listede yanlış IBAN tespit ediliyor mu?
    [Fact]
    public async Task T07_Yanlis_IBAN_tespit_edilir()
    {
        var a = await PersonelAsync("Deniz Er", 30000, 11);
        var b = await PersonelAsync("Ece Tunç", 30000, 12);
        var c = await PersonelAsync("Oğuz Kurt", 30000, 13);
        await BordroYukleAsync(2026, 10, new BordroSatir(a, 25000m), new BordroSatir(b, 25000m), new BordroSatir(c, 25000m));

        var oturum = await KontrolAsync(2026, 10,
            Talimat(a, 25000m, Iban(999)),          // geçerli ama karttakinden farklı
            Talimat(b, 25000m, c.Iban),             // başka personelin IBAN'ı
            Talimat(c, 25000m, "TR000000000000000000000001")); // kontrol hanesi hatalı

        var sa = oturum.Satirlar.Single(s => s.PersonelId == a.Id);
        Assert.Equal(OdemeKontrolSonucu.IbanUyusmazligi, sa.Sonuc);
        Assert.True(sa.KritikMi);
        Assert.False(sa.EslesmeBelirsiz);

        var sb = oturum.Satirlar.Single(s => s.PersonelId == b.Id);
        Assert.Equal(OdemeKontrolSonucu.IbanUyusmazligi, sb.Sonuc);
        Assert.True(sb.EslesmeBelirsiz);
        Assert.Contains("başka bir personelin", sb.Gerekce);

        var sc = oturum.Satirlar.Single(s => s.PersonelId == c.Id);
        Assert.Equal(OdemeKontrolSonucu.IbanUyusmazligi, sc.Sonuc);
        Assert.Contains("geçersiz", sc.Gerekce);
    }

    // 8. Kritik hatalı banka talimatı onaylanmadan dışa aktarılabiliyor mu?
    [Fact]
    public async Task T08_Kritik_hatali_talimat_onaylanmadan_disa_aktarilamaz()
    {
        var a = await PersonelAsync("Gül Ateş", 30000, 14);
        var b = await PersonelAsync("Kemal Usta", 30000, 15);
        await BordroYukleAsync(2026, 10, new BordroSatir(a, 25000m), new BordroSatir(b, 20000m));
        var oturum = await KontrolAsync(2026, 10,
            Talimat(a, 25000m),
            Talimat(b, 29000m),                          // fazla ödeme riski (kritik)
            new BankaSatir(null, "Bilinmeyen Kişi", Iban(500), 5000m)); // kimliği belirsiz
        var srv = S<IOdemeOncesiKontrolServisi>();

        var hata = await Assert.ThrowsAsync<InvalidOperationException>(() => srv.OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor));
        Assert.Contains("Kritik", hata.Message);
        Assert.False(Directory.Exists(Klasor) && Directory.GetFiles(Klasor).Length > 0);
        Assert.Equal(0, await Db.OnayliOdemeListeleri.CountAsync());

        var fazla = oturum.Satirlar.Single(s => s.PersonelId == b.Id);
        var belirsiz = oturum.Satirlar.Single(s => s.PersonelId == null);
        Assert.Equal(OdemeKontrolSonucu.FazlaOdemeRiski, fazla.Sonuc);
        Assert.True(belirsiz.EslesmeBelirsiz);

        // İstisna yetkisi yalnızca yöneticide; gerekçe zorunlu; belirsiz eşleşme hiç onaylanamaz
        await Assert.ThrowsAsync<YetkiHatasiException>(() => srv.IstisnaOnaylaAsync(fazla.Id, "Prim farkı ödenecek, onaylıyorum.", Muhasebe));
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.IstisnaOnaylaAsync(fazla.Id, "", Yonetici));
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.IstisnaOnaylaAsync(belirsiz.Id, "Tanıdık kişi, ödensin lütfen.", Yonetici));
        await srv.IstisnaOnaylaAsync(fazla.Id, "Ekim prim farkı 9.000 TL ayrıca ödenecek (yönetim kararı 12/10).", Yonetici);

        // Belirsiz satır hâlâ engelliyor → listeden çıkarılır
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor));
        await srv.TalimatDuzeltAsync(oturum.Id, int.Parse(belirsiz.TalimatSatirNolari!), null, null, true, "Kimliği belirsiz alıcı listeden çıkarıldı.", Muhasebe);

        await Assert.ThrowsAsync<YetkiHatasiException>(() => srv.OnayliListeUretAsync(oturum.Id, Izleyici, Klasor));
        var liste = await srv.OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor);
        Assert.Equal(2, liste.SatirSayisi);
        Assert.Equal(54000m, liste.ToplamTutar);
        Assert.True(File.Exists(liste.DosyaYolu));

        var istisnaSatiri = liste.Satirlar.Single(s => s.PersonelId == b.Id);
        Assert.True(istisnaSatiri.IstisnaOnayi);
        Assert.Equal(Yonetici, istisnaSatiri.IstisnaOnaylayan);
        var denetim = await Db.DenetimKayitlari.SingleAsync(d => d.Islem == "IstisnaOnayi");
        Assert.Equal(Yonetici, denetim.KullaniciAdi);
        Assert.Contains("prim farkı", denetim.DegisiklikNedeni);
    }

    // 9. Kapatılmış maaş dönemine doğrudan müdahale engelleniyor mu?
    [Fact]
    public async Task T09_Kapatilmis_doneme_dogrudan_mudahale_engellenir()
    {
        var p = await PersonelAsync("Leyla Yurt", 30000, 16);
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 25000m));
        var oturum = await KontrolAsync(2026, 10, Talimat(p, 25000m));
        await S<IOdemeOncesiKontrolServisi>().OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor);
        await BankaHareketYukleAsync(2026, 10, BankaHareketExcel([Hareket(p, 25000m, new DateOnly(2026, 11, 5), "DKT-9")]));
        await DonemiKapatAsync(2026, 10, elleOnayNotu: null);

        var donemSrv = S<IDonemKapanisServisi>();
        var donem = await donemSrv.GetOrCreateAsync(2026, 10);
        Assert.Equal(MaasDonemDurumu.Kapatildi, donem.Durum);

        // Servis katmanı
        await Assert.ThrowsAsync<InvalidOperationException>(() => donemSrv.EnsureDonemAcikAsync(2026, 10));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            S<IBankaOdemeServisi>().KaydetAsync(p.Id, new DateOnly(2026, 11, 20), 100m, 2026, 10, p.Iban, "DKT-X", Muhasebe));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => BordroYukleAsync(2026, 10, new BordroSatir(p, 1m)));
        var aktarma = await Db.IceAktarmalar.FirstAsync(i => i.KaynakTuru == "BordroExcel");
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => S<IBordroIceAktarmaServisi>().GeriAlAsync(aktarma.Id, Muhasebe));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => KontrolAsync(2026, 10, Talimat(p, 25000m)));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
            S<IMaliHareketServisi>().BordroDisiHakEdisAsync(p.Id, MaliIslemTuru.Prim, new DateOnly(2026, 11, 1), 2026, 10, 500m, "Prim", null, Muhasebe));
        await Assert.ThrowsAsync<InvalidOperationException>(() => donemSrv.DurumGuncelleAsync(donem.Id, MaasDonemDurumu.IncelemeBekliyor, Yonetici));

        // Veritabanı katmanı (servisler atlansa bile)
        using (var scope = Sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.PayControlDbContext>();
            var kayit = await db.MaasDonemKayitlari.FirstAsync(m => m.PersonelId == p.Id);
            kayit.NetIstihkak = 99999m;
            await Assert.ThrowsAsync<KapaliDonemIhlaliException>(() => db.SaveChangesAsync());
        }
        using (var scope = Sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.PayControlDbContext>();
            var odeme = await db.BankaOdemeleri.FirstAsync();
            db.BankaOdemeleri.Remove(odeme);
            await Assert.ThrowsAsync<KapaliDonemIhlaliException>(() => db.SaveChangesAsync());
        }
        using (var scope = Sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.PayControlDbContext>();
            var d = await db.MaasDonemKapanislari.FirstAsync(x => x.Id == donem.Id);
            d.ToplamBankaOdemesi = 1m;
            await Assert.ThrowsAsync<KapaliDonemIhlaliException>(() => db.SaveChangesAsync());
        }

        // Toplamlar sonradan değişen kartlardan etkilenmez (özet yeniden hesaplanmaz)
        await S<IPersonelServisi>().GuncelleAsync(p.Id, new PersonelGuncelleDto { AdSoyad = "Leyla Yurt Ak", GuncelMaas = 40000m, Iban = Iban(1600) }, Muhasebe, "Zam ve IBAN değişikliği");
        await donemSrv.OzetHesaplaAsync(donem.Id);
        var rapor = await donemSrv.KapanisRaporuAsync(donem.Id);
        Assert.True(rapor.DondurulmusVeri);
        Assert.Equal(25000m, rapor.ToplamBeklenenMaas);
        var anlik = Assert.Single(rapor.AnlikGoruntu);
        Assert.Equal("Leyla Yurt", anlik.AdSoyad);
        Assert.Equal(p.Iban, anlik.Iban);

        // Yalnızca yönetici, gerekçeyle yeniden açabilir; tekrar kapanış denetim geçmişinde
        await Assert.ThrowsAsync<YetkiHatasiException>(() => donemSrv.YenidenAcAsync(donem.Id, Muhasebe, "Hatalı kapanış düzeltilecek"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => donemSrv.YenidenAcAsync(donem.Id, Yonetici, ""));
        await donemSrv.YenidenAcAsync(donem.Id, Yonetici, "Banka iadesi işlenecek, dönem yeniden açıldı.");
        await DonemiKapatAsync(2026, 10, elleOnayNotu: null);

        var d2 = await Db.MaasDonemKapanislari.AsNoTracking().FirstAsync(x => x.Id == donem.Id);
        Assert.Equal(2, d2.KapanisSayisi);
        Assert.Equal(2, await Db.DonemKapanisOzetleri.CountAsync(o => o.MaasDonemKapanisiId == donem.Id));
        var islemler = await Db.DenetimKayitlari.Where(x => x.VarlikId == donem.Id).Select(x => x.Islem).ToListAsync();
        Assert.Equal(2, islemler.Count(i => i == "DonemKapatildi"));
        Assert.Single(islemler, i => i == "DonemYenidenAcildi");
    }

    // 10. Düzeltmeler eski kayıtları silmeden izlenebiliyor mu?
    [Fact]
    public async Task T10_Duzeltmeler_eski_kayitlari_silmeden_izlenir()
    {
        var p = await PersonelAsync("Murat Şen", 30000, 17);
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 30000m));
        var oturum = await KontrolAsync(2026, 10, Talimat(p, 30000m));
        await S<IOdemeOncesiKontrolServisi>().OnayliListeUretAsync(oturum.Id, Muhasebe, Klasor);
        await BankaHareketYukleAsync(2026, 10, BankaHareketExcel([Hareket(p, 30000m, new DateOnly(2026, 11, 5), "DKT-10")]));
        await DonemiKapatAsync(2026, 10, elleOnayNotu: null);

        var donemSrv = S<IDonemKapanisServisi>();
        var ekim = await donemSrv.GetOrCreateAsync(2026, 10);
        var kayit = await Db.MaasDonemKayitlari.AsNoTracking().FirstAsync(m => m.PersonelId == p.Id && m.DonemAy == 10);

        await Assert.ThrowsAsync<YetkiHatasiException>(() => donemSrv.DuzeltmeEkleAsync(ekim.Id, p.Id, nameof(MaasDonemKaydi), kayit.Id,
            "NetIstihkak", "30000", "30500", "Eksik prim", Izleyici));
        var duzeltme = await donemSrv.DuzeltmeEkleAsync(ekim.Id, p.Id, nameof(MaasDonemKaydi), kayit.Id,
            "NetIstihkak", "30000", "30500", "Ekim vardiya primi bordroya eksik yansımış", Muhasebe);

        // Eski kayıt aynen duruyor
        Assert.Equal(30000m, (await Db.MaasDonemKayitlari.AsNoTracking().FirstAsync(m => m.Id == kayit.Id)).NetIstihkak);
        Assert.Equal(25000m + 5000m, (await Db.MaasDonemKapanislari.AsNoTracking().FirstAsync(d => d.Id == ekim.Id)).ToplamBeklenenMaas);

        // Düzeltme eski/yeni/neden ve devredeceği dönemle kayıtlı
        var dk = await Db.DonemDuzeltmeler.AsNoTracking().SingleAsync(d => d.Id == duzeltme.Id);
        Assert.Equal(("30000", "30500", 500m), (dk.EskiDeger, dk.YeniDeger, dk.FarkTutari));
        Assert.Equal((2026, 11), (dk.UygulanacakDonemYil, dk.UygulanacakDonemAy));
        Assert.Contains("prim", dk.Neden);

        // Fark açık döneme (Kasım) devreden maaş alacağı olarak deftere yazıldı
        var e = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);
        var h = Assert.Single(e.Hareketler, x => x.IslemTuru == MaliIslemTuru.OncekiDonemDuzeltme);
        Assert.Equal((500m, 2026, 11), (h.Tutar, h.DonemYil!.Value, h.DonemAy!.Value));
        Assert.Equal(500m, e.Ozet.OdenmemisHakEdis);
        Assert.Equal(0m, e.DonemBakiyeleri.Single(d => d.DonemAy == 10).Fark);

        // Kasım ödeme kontrolü devreden düzeltmeyi gösterir
        await BordroYukleAsync(2026, 11, new BordroSatir(p, 30500m));
        var kasim = await KontrolAsync(2026, 11, Talimat(p, 30500m));
        Assert.Contains("devreden düzeltme", kasim.Satirlar.Single(s => s.PersonelId == p.Id).Gerekce);

        // Ters kayıt: orijinal silinmez; defter kayıtları doğrudan değiştirilemez
        var prim = await S<IMaliHareketServisi>().BordroDisiHakEdisAsync(p.Id, MaliIslemTuru.Prim, new DateOnly(2026, 11, 10), 2026, 11, 750m, "Satış primi", "PRM-1", Muhasebe);
        var ters = await S<IMaliHareketServisi>().TersKayitAsync(prim.Id, "Yanlış personele girilmiş", Muhasebe);
        Assert.Equal(-750m, ters.Tutar);
        Assert.True(await Db.MaliHareketler.AnyAsync(x => x.Id == prim.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => S<IMaliHareketServisi>().TersKayitAsync(prim.Id, "tekrar", Muhasebe));
        using var scope = Sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.PayControlDbContext>();
        var degistir = await db.MaliHareketler.FirstAsync(x => x.Id == prim.Id);
        degistir.Tutar = 1m;
        await Assert.ThrowsAsync<KapaliDonemIhlaliException>(() => db.SaveChangesAsync());

        var denetim = await Db.DenetimKayitlari.AsNoTracking().SingleAsync(d => d.Islem == "DonemDuzeltme");
        Assert.Equal(("30000", "30500"), (denetim.EskiDeger, denetim.YeniDeger));
    }

    // 11. Personelin geçmiş dönem maaşları yeni maaş değişikliğinden etkileniyor mu?
    [Fact]
    public async Task T11_Gecmis_donem_maaslari_yeni_maas_degisikliginden_etkilenmez()
    {
        var p = await PersonelAsync("Selin Ay", 30000, 18);
        await BordroYukleAsync(2026, 9, new BordroSatir(p, 30000m, Maas: 30000m));
        await S<IPersonelServisi>().GuncelleAsync(p.Id,
            new PersonelGuncelleDto { AdSoyad = p.AdSoyad, GuncelMaas = 35000m, Iban = p.Iban, Sirket = p.Sirket, Isyeri = p.Isyeri },
            Muhasebe, "Ekim zammı");
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 35000m, Maas: 35000m));

        var eylul = await Db.MaasDonemKayitlari.AsNoTracking().SingleAsync(m => m.PersonelId == p.Id && m.DonemAy == 9);
        Assert.Equal(30000m, eylul.MaasTutar);
        Assert.Equal(30000m, eylul.NetIstihkak);

        var e = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);
        Assert.Equal(30000m, e.Hareketler.Single(h => h.IslemTuru == MaliIslemTuru.MaasHakEdis && h.DonemAy == 9).Tutar);
        Assert.Equal(35000m, e.Hareketler.Single(h => h.IslemTuru == MaliIslemTuru.MaasHakEdis && h.DonemAy == 10).Tutar);
        var degisiklik = Assert.Single(e.Hareketler, h => h.IslemTuru == MaliIslemTuru.MaasDegisikligi);
        Assert.Contains("30.000,00 → 35.000,00", degisiklik.Aciklama);
        Assert.Equal(BakiyeTuru.Bilgi, degisiklik.BakiyeTuru);

        // Karşılaştırma maaş değişikliğini kaynağıyla gösterir
        var k = await S<IDonemKapanisServisi>().OncekiAyKarsilastirAsync(2026, 10);
        var d = Assert.Single(k.Detaylar, x => x.Tur == "Maaş değişikliği");
        Assert.Contains("Ekim zammı", d.Kaynak);
        Assert.Equal(5000m, d.NetEtki);
    }

    // 12. Geçmiş ekstredeki toplamlar ilgili finansal hareketlerle uyuşuyor mu?
    [Fact]
    public async Task T12_Ekstre_toplamlari_finansal_hareketlerle_uyusur()
    {
        var p = await PersonelAsync("Kader Işık", 30000, 19, new DateOnly(2026, 1, 1));
        var avans = S<IAvansServisi>();
        await avans.VerAsync(p.Id, new DateOnly(2026, 8, 20), 9000m, 3, 3000m, "Taksitli", Muhasebe);
        await BordroYukleAsync(2026, 9, new BordroSatir(p, 27000m, Avans: 3000m, Icra: 1500m));
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 27000m, Avans: 3000m, Icra: 1500m));

        var banka = S<IBankaOdemeServisi>();
        await banka.KaydetAsync(p.Id, new DateOnly(2026, 10, 5), 27000m, 2026, 9, p.Iban, "R-9", Muhasebe);
        await banka.KaydetAsync(p.Id, new DateOnly(2026, 11, 5), 20000m, 2026, 10, p.Iban, "R-10", Muhasebe);
        await banka.KasaOdemesiKaydetAsync(p.Id, new DateOnly(2026, 11, 7), 4000m, 2026, 10, "Kalan kısım kasadan", "KASA-1", Muhasebe);
        await S<IMaliHareketServisi>().BordroDisiHakEdisAsync(p.Id, MaliIslemTuru.FazlaMesai, new DateOnly(2026, 10, 31), 2026, 10, 1200m, "Ekim fazla mesai", "FM-10", Muhasebe);

        // Fazla ödeme → inceleme → kesinleşti → kısmi iade
        await S<IMaliHareketServisi>().SupheliFazlaOdemeKaydetAsync(p.Id, 2026, 9, 2000m, "Test fazla ödeme", "Test", null, "t1", Muhasebe);
        await S<IMaliHareketServisi>().FazlaOdemeKesinlestirAsync(p.Id, 1500m, "İnceleme: mükerrer yol ödemesi", Muhasebe);
        await S<IMaliHareketServisi>().IadeAlAsync(p.Id, new DateOnly(2026, 11, 15), 500m, BakiyeTuru.KesinlesmisGeriOdeme, "Elden iade", "MAKBUZ-3", Muhasebe);

        var hareketler = await Db.MaliHareketler.AsNoTracking().Where(h => h.PersonelId == p.Id).ToListAsync();
        var e = await S<IHesapEkstresiServisi>().GetirAsync(p.Id);

        // Elle hesaplanan beklenen değerler
        Assert.Equal(27000m + 27000m + 1200m, e.Ozet.ToplamHakEdis);
        Assert.Equal(27000m + 20000m + 4000m, e.Ozet.GerceklesenToplamOdeme);
        Assert.Equal(4200m, e.Ozet.OdenmemisHakEdis);         // Eylül 0, Ekim 28.200 − 24.000
        Assert.Equal(3000m, e.Ozet.AcikAvansBorcu);           // 9.000 − 2×3.000
        Assert.Equal(500m, e.Ozet.IncelemeBekleyenFazlaOdeme); // 2.000 − 1.500
        Assert.Equal(1000m, e.Ozet.KesinlesmisGeriOdemeBorcu); // 1.500 − 500
        Assert.Equal(3000m, e.Ozet.ToplamKesinti);            // icra 2×1.500

        // Ekstre toplamları = defterdeki hareketlerden bağımsız yeniden hesaplama
        var yeniden = MaliHareketKurallari.OzetHesapla(hareketler);
        Assert.Equivalent(yeniden, e.Ozet);
        Assert.Equal(hareketler.Count, e.Hareketler.Count);
        Assert.Equal(e.Ozet.OdenmemisHakEdis, e.DonemBakiyeleri.Where(d => d.Fark < 0).Sum(d => -d.Fark));

        // Son satırdaki yürüyen bakiyeler genel bakiyelerle aynı
        var son = e.Hareketler.Last();
        Assert.Equal(e.Ozet.ToplamHakEdis - e.Ozet.GerceklesenToplamOdeme, son.MaasAlacagiBakiyesi);
        Assert.Equal(e.Ozet.AcikAvansBorcu, son.AvansBorcuBakiyesi);
        Assert.Equal(e.Ozet.IncelemeBekleyenFazlaOdeme, son.SupheliFazlaBakiyesi);
        Assert.Equal(e.Ozet.KesinlesmisGeriOdemeBorcu, son.GeriOdemeBakiyesi);

        // Çalışma dönemi özeti = genel özet (tek dönem)
        Assert.Equal(e.Ozet.ToplamHakEdis, e.CalismaDonemiOzetleri.Single().Ozet.ToplamHakEdis);

        // Benzersiz işlem kimliği
        Assert.Equal(hareketler.Count, hareketler.Select(h => h.IslemKimligi).Distinct().Count());

        // Excel çıktısı aynı toplamları taşır
        var xlsx = await S<IHesapEkstresiServisi>().ExcelOlarakAktarAsync(p.Id);
        using var wb = new XLWorkbook(new MemoryStream(xlsx));
        var ws = wb.Worksheet("Hesap Ekstresi");
        Assert.Equal(e.Ozet.ToplamHakEdis, ws.Cell(8, 2).GetValue<decimal>());
        Assert.Equal(e.Ozet.OdenmemisHakEdis, ws.Cell(10, 2).GetValue<decimal>());
        Assert.Equal(e.DonemBakiyeleri.Count + 1, wb.Worksheet("Dönem Bakiyeleri").LastRowUsed()!.RowNumber());
    }
}
