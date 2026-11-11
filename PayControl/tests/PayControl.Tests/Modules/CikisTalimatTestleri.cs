using Microsoft.EntityFrameworkCore;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Tests.Modules;

/// <summary>
/// Senaryo: yönetici İK'ya "şu kişiyi çıkar" der, İK unutur ve personele çalışmadığı halde maaş yatmaya devam eder.
/// </summary>
public class CikisTalimatTestleri : TestOrtami
{
    private async Task<(PayControl.Domain.Entities.Personel P, BildirimAlicisi Ik)> HazirlaAsync()
    {
        var p = await PersonelAsync("Unutulan Personel", 30000, 300, new DateOnly(2025, 1, 1), isyeri: "Ankara Depo");
        var ik = await S<IBildirimServisi>().AliciKaydetAsync(new BildirimAlicisi { AdSoyad = "İK Uzmanı", Eposta = "ik@ornek.test", Rol = "İnsan kaynakları" }, Muhasebe);
        return (p, ik);
    }

    [Fact]
    public async Task Ik_cikisi_unutursa_son_gunden_sonraki_odemeler_ne_icin_yapildigiyla_listelenir_ve_kontrol_engeller()
    {
        var (p, ik) = await HazirlaAsync();
        var srv = S<ICikisTalimatServisi>();
        var t = await srv.OlusturAsync(new CikisTalimatiOlusturDto
        {
            PersonelId = p.Id, SonCalismaGunu = new DateOnly(2026, 9, 15), Neden = "Devamsızlık", SorumluAliciId = ik.Id
        }, Yonetici);
        Assert.Equal("İK Uzmanı", t.Sorumlu);
        Assert.Contains(await Db.DenetimKayitlari.AsNoTracking().ToListAsync(), d => d.Islem == "CikisTalimatiVerildi" && d.PersonelId == p.Id);
        // İK'ya bildirim denendi (SMTP ayarsız olduğu için hata olarak kaydedilir, yeniden denenecek)
        Assert.Contains(await S<IBildirimServisi>().GecmisAsync(), k => k.Alici == "ik@ornek.test" && k.Konu.Contains("çıkış talimatı"));

        // İK unuttu: Eylül ve Ekim tam maaş bordroya girdi ve ödendi
        await BordroYukleAsync(2026, 9, new BordroSatir(p, 21000m));
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 21000m));
        var banka = S<IBankaOdemeServisi>();
        await banka.KaydetAsync(p.Id, new DateOnly(2026, 10, 5), 21000m, 2026, 9, p.Iban, "EYL-1", Muhasebe);
        await banka.KaydetAsync(p.Id, new DateOnly(2026, 11, 5), 21000m, 2026, 10, p.Iban, "EKI-1", Muhasebe);
        await banka.KaydetAsync(p.Id, new DateOnly(2026, 9, 5), 21000m, 2026, 8, p.Iban, "AGU-1", Muhasebe); // son günden önce, listelenmez

        var satir = Assert.Single(await srv.ListeleAsync());
        Assert.Equal(CikisTakipDurumu.Gecikti, satir.Durum);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today).DayNumber - new DateOnly(2026, 9, 15).DayNumber, satir.GecikmeGun);
        Assert.Null(satir.CikisKaydiTarihi);
        Assert.Equal(2, satir.Odemeler.Count);
        Assert.Equal(10500m, satir.CikisAyiTahminiHakEdis); // 21.000 / 30 × 15 gün
        var eylul = satir.Odemeler.Single(o => o.HakEdisDonemi == "2026-09");
        Assert.Equal(10500m, eylul.HakEdilmeyen);
        Assert.Contains("15 günlük", eylul.Degerlendirme);
        var ekim = satir.Odemeler.Single(o => o.HakEdisDonemi == "2026-10");
        Assert.Equal(21000m, ekim.HakEdilmeyen);
        Assert.Contains("çalışmadığı dönem", ekim.Degerlendirme);
        Assert.Equal("EKI-1", ekim.Referans);
        Assert.Equal(31500m, satir.TahminiFazlaOdeme);
        Assert.Equal(42000m, satir.SonGunSonrasiOdenen);
        Assert.Equal(1, satir.SonrakiBordroSayisi);

        // Kasım ödeme kontrolü bu kişiyi kritik olarak durdurur
        await BordroYukleAsync(2026, 11, new BordroSatir(p, 21000m));
        var oturum = await KontrolAsync(2026, 11, Talimat(p, 21000m));
        var k = oturum.Satirlar.Single(s => s.PersonelId == p.Id);
        Assert.True(k.KritikMi);
        Assert.Contains("ÇIKIŞ TALİMATI UYGULANMAMIŞ", k.Gerekce);

        // Uyarı merkezinde iki kritik uyarı
        var uyarilar = await S<IUyariServisi>().ListeleAsync();
        Assert.Contains(uyarilar, u => u.Kategori == "Çıkış talimatı" && u.Seviye == UyariSeviyesi.Kritik && u.PersonelId == p.Id);
        Assert.Contains(uyarilar, u => u.Kategori == "Çıkış sonrası ödeme" && u.Baslik.Contains(31500m.ToString("N2")));

        // Rapor
        var rapor = await S<IRaporServisi>().OlusturAsync(RaporTuru.CikisTalimatTakibi, new RaporParametreleri { BaslangicYil = 2026, BaslangicAy = 1, BitisYil = 2026, BitisAy = 12 });
        Assert.Equal("GECİKTİ", Assert.Single(rapor.Satirlar)[6]);
    }

    [Fact]
    public async Task Gunluk_hatirlatma_bir_kez_gider_cikis_kaydi_talimati_kapatir()
    {
        var (p, ik) = await HazirlaAsync();
        var srv = S<ICikisTalimatServisi>();
        await srv.OlusturAsync(new CikisTalimatiOlusturDto { PersonelId = p.Id, SonCalismaGunu = new DateOnly(2026, 10, 31), Neden = "İstifa", SorumluAliciId = ik.Id }, Muhasebe);

        Assert.Equal(0, await srv.GecikenleriHatirlatAsync(new DateTime(2026, 10, 31, 10, 0, 0))); // son gün henüz geçmedi
        Assert.Equal(1, await srv.GecikenleriHatirlatAsync(new DateTime(2026, 11, 2, 9, 0, 0)));
        Assert.Equal(0, await srv.GecikenleriHatirlatAsync(new DateTime(2026, 11, 2, 15, 0, 0))); // aynı gün ikinci kez yok
        Assert.Equal(1, await srv.GecikenleriHatirlatAsync(new DateTime(2026, 11, 3, 9, 0, 0)));
        Assert.Equal(2, (await Db.CikisTalimatlari.AsNoTracking().SingleAsync()).HatirlatmaSayisi);
        Assert.Contains(await S<IBildirimServisi>().GecmisAsync(), k => k.Konu.StartsWith("HATIRLATMA") && k.Alici == "ik@ornek.test");

        // İK çıkışı girer → talimat kendiliğinden tamamlanır
        await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p.Id, FiiliSonCalismaTarihi = new DateOnly(2026, 10, 31), IstenCikisTarihi = new DateOnly(2026, 10, 31), CikisNedeni = "İstifa",
            SonBordroDonemYil = 2026, SonBordroDonemAy = 10
        }, "muhasebe");
        var satir = Assert.Single(await srv.ListeleAsync());
        Assert.Equal(CikisTakipDurumu.CikisYapildi, satir.Durum);
        Assert.Equal(new DateOnly(2026, 10, 31), satir.CikisKaydiTarihi);
        Assert.Equal("muhasebe", satir.CikisiYapan);
        Assert.Equal(0, await srv.GecikenleriHatirlatAsync(new DateTime(2026, 11, 4, 9, 0, 0)));
        Assert.Empty((await S<IUyariServisi>().ListeleAsync()).Where(u => u.Kategori == "Çıkış talimatı"));
    }

    [Fact]
    public async Task Talimatsiz_dogrudan_cikislar_da_listelenir_dogrulama_ve_yetki()
    {
        var (p, _) = await HazirlaAsync();
        var p2 = await PersonelAsync("Doğrudan Çıkan", 25000, 301, new DateOnly(2025, 1, 1));
        var srv = S<ICikisTalimatServisi>();

        await Assert.ThrowsAsync<YetkiHatasiException>(() => srv.OlusturAsync(new CikisTalimatiOlusturDto { PersonelId = p.Id, SonCalismaGunu = new DateOnly(2026, 10, 1), Neden = "x" }, Izleyici));
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.OlusturAsync(new CikisTalimatiOlusturDto { PersonelId = p.Id, SonCalismaGunu = new DateOnly(2026, 10, 1), Neden = "" }, Muhasebe));
        var t = await srv.OlusturAsync(new CikisTalimatiOlusturDto { PersonelId = p.Id, SonCalismaGunu = new DateOnly(2026, 12, 31), Neden = "Sözleşme sonu" }, Muhasebe);
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.OlusturAsync(new CikisTalimatiOlusturDto { PersonelId = p.Id, SonCalismaGunu = new DateOnly(2026, 12, 31), Neden = "Tekrar" }, Muhasebe));
        Assert.Equal(CikisTakipDurumu.Bekliyor, (await srv.ListeleAsync()).Single(s => s.PersonelId == p.Id).Durum);

        await srv.IptalEtAsync(t.Id, "Sözleşme uzatıldı", Muhasebe);
        Assert.Equal(CikisTakipDurumu.IptalEdildi, (await srv.ListeleAsync()).Single(s => s.PersonelId == p.Id).Durum);

        await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p2.Id, FiiliSonCalismaTarihi = new DateOnly(2026, 8, 31), IstenCikisTarihi = new DateOnly(2026, 8, 31), CikisNedeni = "İstifa"
        }, Muhasebe);
        await S<IBankaOdemeServisi>().KaydetAsync(p2.Id, new DateOnly(2026, 10, 5), 18000m, 2026, 9, p2.Iban, "SONRA-1", Muhasebe);
        var d = (await srv.ListeleAsync(new CikisTakipFiltre { YalnizSorunlu = true })).Single();
        Assert.Equal("Doğrudan çıkış kaydı", d.Kaynak);
        Assert.Equal(18000m, d.TahminiFazlaOdeme);
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.OlusturAsync(new CikisTalimatiOlusturDto { PersonelId = p2.Id, SonCalismaGunu = new DateOnly(2026, 10, 1), Neden = "x" }, Muhasebe));
    }
}
