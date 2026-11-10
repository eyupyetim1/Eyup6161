using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayControl.Application.Interfaces;
using PayControl.Domain;
using PayControl.Domain.Enums;

namespace PayControl.Tests.Modules;

public class GuvenlikVeYedekTestleri : TestOrtami
{
    [Fact]
    public async Task Kullanici_olusturma_politika_ilk_giriste_sifre_degisimi_ve_kilitleme()
    {
        var kimlik = S<IKimlikServisi>();
        await Assert.ThrowsAsync<YetkiHatasiException>(() =>
            kimlik.OlusturAsync(new KullaniciOlusturDto { KullaniciAdi = "ayse", Sifre = "Guclu2026!", Rol = KullaniciRolu.Muhasebe }, Muhasebe));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            kimlik.OlusturAsync(new KullaniciOlusturDto { KullaniciAdi = "ayse", Sifre = "kisa1" }, Yonetici));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            kimlik.OlusturAsync(new KullaniciOlusturDto { KullaniciAdi = "ayse", Sifre = "sadeceharf" }, Yonetici));

        await kimlik.OlusturAsync(new KullaniciOlusturDto { KullaniciAdi = "Ayse", Sifre = "Guclu2026!", Rol = KullaniciRolu.Muhasebe, Eposta = "a@ornek.test" }, Yonetici);
        var giris = await kimlik.GirisYapAsync("ayse", "Guclu2026!");
        Assert.True(giris.Basarili);
        Assert.True(giris.SifreDegistirmeGerekli);

        await Assert.ThrowsAsync<InvalidOperationException>(() => kimlik.SifreDegistirAsync("ayse", "yanlis", "YeniSifre2027", default));
        await kimlik.SifreDegistirAsync("ayse", "Guclu2026!", "YeniSifre2027");
        Assert.False((await kimlik.GirisYapAsync("ayse", "YeniSifre2027")).SifreDegistirmeGerekli);

        for (var i = 0; i < 5; i++) Assert.False((await kimlik.GirisYapAsync("ayse", "hatali" + i)).Basarili);
        var kilitli = await kimlik.GirisYapAsync("ayse", "YeniSifre2027");
        Assert.False(kilitli.Basarili);
        Assert.Contains("kilitli", kilitli.Hata);
        Assert.Contains(await Db.DenetimKayitlari.Select(d => d.Islem).ToListAsync(), i => i == "HesapKilitlendi");
    }

    [Fact]
    public async Task Varsayilan_admin_sifre_degistirmeye_zorlanir_ve_son_yonetici_korunur()
    {
        var kimlik = S<IKimlikServisi>();
        var admin = await kimlik.GirisYapAsync("admin", "Admin123!");
        Assert.True(admin.Basarili);
        Assert.True(admin.SifreDegistirmeGerekli);
        await Assert.ThrowsAsync<InvalidOperationException>(() => kimlik.SifreDegistirAsync("admin", "Admin123!", "Admin123!"));

        // Test ortamında iki yönetici var (admin, yonetici); ikisinden biri kalmalı
        var liste = await kimlik.ListeleAsync();
        var adminK = liste.Single(k => k.KullaniciAdi == "admin");
        var yon = liste.Single(k => k.KullaniciAdi == Yonetici);
        await kimlik.GuncelleAsync(adminK.Id, KullaniciRolu.Goruntuleyici, true, null, null, null, Yonetici);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            kimlik.GuncelleAsync(yon.Id, KullaniciRolu.Muhasebe, true, null, null, null, Yonetici));
    }

    [Fact]
    public async Task Yanlis_odeme_iptal_edilir_ters_kayit_olusur_ve_dogrusu_yeniden_girilebilir()
    {
        var a = await PersonelAsync("Ömer Yanlış", 30000, 60);
        var b = await PersonelAsync("Sena Doğru", 30000, 61);
        await BordroYukleAsync(2026, 10, new BordroSatir(a, 25000m), new BordroSatir(b, 25000m));
        var banka = S<IBankaOdemeServisi>();
        var yanlis = await banka.KaydetAsync(a.Id, new DateOnly(2026, 11, 5), 25000m, 2026, 10, a.Iban, "DKT-555", Muhasebe);

        await Assert.ThrowsAsync<YetkiHatasiException>(() => banka.IptalEtAsync(yanlis.Id, "Yanlış personele işlendi", Izleyici));
        await Assert.ThrowsAsync<InvalidOperationException>(() => banka.IptalEtAsync(yanlis.Id, "kısa", Muhasebe));
        await banka.IptalEtAsync(yanlis.Id, "Dekont Sena Doğru'ya ait, yanlış personele işlendi", Muhasebe);

        var ea = await S<IHesapEkstresiServisi>().GetirAsync(a.Id);
        Assert.Equal(0m, ea.Ozet.GerceklesenToplamOdeme);
        Assert.Contains(ea.Hareketler, h => h.TersKayitMi && h.IslemTuru == MaliIslemTuru.BankaMaasOdemesi);
        Assert.Empty(await banka.DonemOdemeleriAsync(2026, 10));
        Assert.Single(await banka.PersonelOdemeleriAsync(a.Id)); // iptal kaydı görünür kalır

        await banka.KaydetAsync(b.Id, new DateOnly(2026, 11, 5), 25000m, 2026, 10, b.Iban, "DKT-555", Muhasebe);
        Assert.Equal(25000m, (await S<IHesapEkstresiServisi>().GetirAsync(b.Id)).Ozet.GerceklesenToplamOdeme);
    }

    [Fact]
    public async Task Sifreli_yedek_alinir_ve_geri_yuklenince_veri_onceki_haline_doner()
    {
        if (PgMi) return; // dosya yedeği SQLite'a özgü; PostgreSQL'de sunucu yedeği (pg_dump) kullanılır
        var p = await PersonelAsync("Yedek Test", 30000, 62);
        var yedek = S<IYedekServisi>();
        await Assert.ThrowsAsync<YetkiHatasiException>(() => yedek.YedekSifresiAyarlaAsync("CokGizli2026", Muhasebe));
        await yedek.YedekSifresiAyarlaAsync("CokGizli2026", Yonetici);

        var k = await yedek.YedekAlAsync(Muhasebe, hedefKlasor: Klasor);
        Assert.True(k.Basarili, k.Hata);
        Assert.True(k.Sifreli);
        Assert.EndsWith(".pcyedek", k.DosyaYolu);
        Assert.StartsWith("PCYEDEK1", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(k.DosyaYolu)[..8]));

        // Yedekten sonra veri değişiyor
        await PersonelAsync("Sonradan Eklenen", 20000, 63);
        Assert.Equal(2, await Db.Personeller.CountAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() => yedek.GeriYukleAsync(k.DosyaYolu, "yanlis-sifre", Yonetici));
        await Assert.ThrowsAsync<YetkiHatasiException>(() => yedek.GeriYukleAsync(k.DosyaYolu, "CokGizli2026", Muhasebe));
        var mesaj = await yedek.GeriYukleAsync(k.DosyaYolu, "CokGizli2026", Yonetici);
        Assert.Contains("güvenlik yedeği", mesaj);

        using var scope = Sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Data.PayControlDbContext>();
        Assert.Equal(1, await db.Personeller.CountAsync());
        Assert.Equal(p.Id, (await db.Personeller.SingleAsync()).Id);
        Assert.True(await db.DenetimKayitlari.AnyAsync(d => d.Islem == "GeriYukleme"));
    }

    [Fact]
    public async Task Otomatik_yedek_gunde_bir_kez_alinir_ve_saklama_sayisi_uygulanir()
    {
        if (PgMi) return; // dosya yedeği SQLite'a özgü; PostgreSQL'de sunucu yedeği (pg_dump) kullanılır
        var yedek = S<IYedekServisi>();
        await S<IAyarServisi>().AyarlaAsync(AyarAnahtarlari.YedekKlasoru, Klasor, Yonetici);
        await S<IAyarServisi>().AyarlaAsync(AyarAnahtarlari.YedekSaklamaSayisi, "2", Yonetici);

        Assert.NotNull(await yedek.OtomatikYedekGerekirseAsync());
        Assert.Null(await yedek.OtomatikYedekGerekirseAsync()); // 24 saat dolmadı

        for (var i = 0; i < 3; i++)
        {
            await yedek.YedekAlAsync(Muhasebe);
            await Task.Delay(1100); // dosya adları saniye çözünürlüklü
        }
        Assert.Equal(2, Directory.GetFiles(Klasor, "PayControl-Yedek-*").Length);
    }
}
