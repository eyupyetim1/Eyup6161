using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Infrastructure.Data;
using PayControl.Infrastructure.DependencyInjection;
using PayControl.Infrastructure.Import;

namespace PayControl.Tests.Modules;

/// <summary>Sentetik verilerle uçtan uca senaryolar için ortak test ortamı.</summary>
public abstract class TestOrtami : IAsyncLifetime
{
    protected const string Yonetici = "yonetici";
    protected const string Muhasebe = "muhasebe";
    protected const string Izleyici = "izleyici";

    protected ServiceProvider Sp = null!;
    protected string DbYolu = null!;
    protected string Klasor = null!;

    public async Task InitializeAsync()
    {
        DbYolu = Path.Combine(Path.GetTempPath(), $"pc-v12-{Guid.NewGuid():N}.db");
        Klasor = Path.Combine(Path.GetTempPath(), $"pc-v12-out-{Guid.NewGuid():N}");
        var services = new ServiceCollection();
        services.AddPayControlInfrastructure(DbYolu);
        Sp = services.BuildServiceProvider();
        await ServiceCollectionExtensions.InitializeDatabaseAsync(Sp);

        var db = Db;
        db.Kullanicilar.AddRange(
            new Kullanici { KullaniciAdi = Yonetici, Rol = KullaniciRolu.Yonetici, SifreHash = "x", SifreSalt = "x" },
            new Kullanici { KullaniciAdi = Muhasebe, Rol = KullaniciRolu.Muhasebe, SifreHash = "x", SifreSalt = "x" },
            new Kullanici { KullaniciAdi = Izleyici, Rol = KullaniciRolu.Goruntuleyici, SifreHash = "x", SifreSalt = "x" });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await TestDbCleanup.DisposeProviderAndDbAsync(Sp, DbYolu);
        try { if (Directory.Exists(Klasor)) Directory.Delete(Klasor, true); } catch { /* yoksay */ }
    }

    protected PayControlDbContext Db => Sp.GetRequiredService<PayControlDbContext>();
    protected T S<T>() where T : notnull => Sp.GetRequiredService<T>();

    /// <summary>Kontrol hanesi doğru, birbirinden farklı sentetik TR IBAN üretir.</summary>
    protected static string Iban(int n)
    {
        var bban = "00061" + n.ToString(CultureInfo.InvariantCulture).PadLeft(17, '0');
        var sayi = BigInteger.Parse(bban + "292700", CultureInfo.InvariantCulture);
        var kontrol = 98 - (int)(sayi % 97);
        return $"TR{kontrol:D2}{bban}";
    }

    private static int _tcSayac = 100;
    protected static string Tc() => "1" + Interlocked.Increment(ref _tcSayac).ToString(CultureInfo.InvariantCulture).PadLeft(10, '0');

    protected async Task<Personel> PersonelAsync(string ad, decimal maas, int ibanNo, DateOnly? giris = null,
        string sirket = "Mavi A.Ş.", string isyeri = "İstanbul Tesisi")
    {
        var dto = await S<IPersonelServisi>().OlusturAsync(new PersonelOlusturDto
        {
            TcKimlikNo = Tc(),
            AdSoyad = ad,
            IseGirisTarihi = giris ?? new DateOnly(2025, 1, 1),
            GuncelMaas = maas,
            Iban = Iban(ibanNo),
            Sirket = sirket,
            Isyeri = isyeri
        }, Muhasebe);
        return await Db.Personeller.AsNoTracking().Include(p => p.CalismaDonemleri).FirstAsync(p => p.Id == dto.Id);
    }

    protected sealed record BordroSatir(Personel P, decimal Net, decimal? Maas = null, decimal? Gun = 30, decimal? Avans = null,
        decimal? Icra = null, decimal? Bes = null, DateOnly? Giris = null, DateOnly? Cikis = null, string? Iban = null);

    protected static byte[] BordroExcel(IEnumerable<BordroSatir> satirlar)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Bordro");
        string[] b = ["T.C. KİMLİK NO", "ADI SOYADI", "İŞYERİ", "GÖREVİ", "MAAŞ", "GİRİŞ TARİHİ", "ÇIKIŞ TARİHİ", "GÜN",
            "NET İSTİHKAK", "IBAN NO", "AVANS", "İCRA", "OTO.KAT.BES"];
        for (var i = 0; i < b.Length; i++) ws.Cell(1, i + 1).Value = b[i];
        var r = 2;
        foreach (var s in satirlar)
        {
            ws.Cell(r, 1).Value = s.P.TcKimlikNo;
            ws.Cell(r, 2).Value = s.P.AdSoyad;
            ws.Cell(r, 3).Value = s.P.Isyeri;
            ws.Cell(r, 4).Value = "Operatör";
            if ((s.Maas ?? s.P.GuncelMaas) is { } m) ws.Cell(r, 5).Value = m;
            if (s.Giris is { } g) ws.Cell(r, 6).Value = g.ToDateTime(TimeOnly.MinValue);
            if (s.Cikis is { } c) ws.Cell(r, 7).Value = c.ToDateTime(TimeOnly.MinValue);
            if (s.Gun is { } gun) ws.Cell(r, 8).Value = gun;
            ws.Cell(r, 9).Value = s.Net;
            ws.Cell(r, 10).Value = s.Iban ?? s.P.Iban;
            if (s.Avans is { } a) ws.Cell(r, 11).Value = a;
            if (s.Icra is { } ic) ws.Cell(r, 12).Value = ic;
            if (s.Bes is { } be) ws.Cell(r, 13).Value = be;
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    protected async Task<IceAktarmaSonucu> BordroYukleAsync(int yil, int ay, params BordroSatir[] satirlar)
    {
        var bytes = BordroExcel(satirlar);
        using var ms = new MemoryStream(bytes);
        var sonuc = await S<IBordroIceAktarmaServisi>().AktarAsync(ms, $"bordro-{yil}-{ay:D2}.xlsx", yil, ay, Muhasebe);
        Assert.False(sonuc.MukerrerEngellendi, sonuc.Mesaj);
        Assert.Equal(satirlar.Length, sonuc.BasariliSatir);
        return sonuc;
    }

    protected sealed record BankaSatir(string? Tc, string? Ad, string? Iban, decimal Tutar, string? Aciklama = null);

    /// <summary>Bankaya gönderilecek dosya. Banka formatını taklit etmek için üstte bilgi satırları ve ek kolon var.</summary>
    protected static byte[] BankaExcel(IEnumerable<BankaSatir> satirlar, bool ustBilgi = true)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("EFT Talimat");
        var r = 1;
        if (ustBilgi)
        {
            ws.Cell(1, 1).Value = "MAVİ BANK TOPLU ÖDEME TALİMATI";
            ws.Cell(2, 1).Value = "Müşteri No: 123456";
            r = 3;
        }
        string[] b = ["SIRA", "ALICI ADI SOYADI", "T.C. KİMLİK NO", "IBAN", "TUTAR", "AÇIKLAMA"];
        for (var i = 0; i < b.Length; i++) ws.Cell(r, i + 1).Value = b[i];
        r++;
        var sira = 1;
        foreach (var s in satirlar)
        {
            ws.Cell(r, 1).Value = sira++;
            ws.Cell(r, 2).Value = s.Ad;
            if (s.Tc is not null) ws.Cell(r, 3).Value = s.Tc;
            ws.Cell(r, 4).Value = s.Iban;
            ws.Cell(r, 5).Value = s.Tutar;
            ws.Cell(r, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, 6).Value = s.Aciklama ?? "MAAŞ";
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    protected static BankaSatir Talimat(Personel p, decimal tutar, string? iban = null) => new(p.TcKimlikNo, p.AdSoyad, iban ?? p.Iban, tutar);

    protected async Task<OdemeKontrolOturumu> KontrolAsync(int yil, int ay, params BankaSatir[] satirlar)
    {
        var bytes = BankaExcel(satirlar);
        using var ms = new MemoryStream(bytes);
        var dosya = BankaTalimatOkuyucu.OkuDosya(ms);
        return await S<IOdemeOncesiKontrolServisi>().KontrolBaslatAsync(yil, ay, dosya, "banka.xlsx",
            Convert.ToHexString(SHA256.HashData(bytes)), Muhasebe);
    }

    protected static byte[] BankaHareketExcel(IEnumerable<BankaHareketSatiri> satirlar)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Hesap Hareketleri");
        string[] b = ["İŞLEM TARİHİ", "DEKONT NO", "ALICI", "TC KİMLİK", "IBAN", "TUTAR", "AÇIKLAMA"];
        for (var i = 0; i < b.Length; i++) ws.Cell(1, i + 1).Value = b[i];
        var r = 2;
        foreach (var s in satirlar)
        {
            ws.Cell(r, 1).Value = s.Tarih.ToDateTime(TimeOnly.MinValue);
            if (s.ReferansNo is not null) ws.Cell(r, 2).Value = s.ReferansNo;
            ws.Cell(r, 3).Value = s.AdSoyad;
            if (s.TcKimlikNo is not null) ws.Cell(r, 4).Value = s.TcKimlikNo;
            ws.Cell(r, 5).Value = s.Iban;
            ws.Cell(r, 6).Value = s.Tutar;
            ws.Cell(r, 7).Value = s.Aciklama;
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    protected async Task<BankaHareketIceAktarmaSonucu> BankaHareketYukleAsync(int yil, int ay, byte[] excel, string dosyaAdi = "ekstre.xlsx")
    {
        using var ms = new MemoryStream(excel);
        var satirlar = BankaHareketOkuyucu.Oku(ms);
        return await S<IBankaOdemeServisi>().HareketleriIceAktarAsync(yil, ay, satirlar, dosyaAdi,
            Convert.ToHexString(SHA256.HashData(excel)), Muhasebe);
    }

    protected static BankaHareketSatiri Hareket(Personel p, decimal tutar, DateOnly tarih, string? referans = null, int satir = 0) =>
        new(satir, tarih, p.TcKimlikNo, p.AdSoyad, p.Iban, tutar, referans, "MAAŞ ÖDEMESİ");

    /// <summary>Dönemi kontrol listesi, mutabakat ve yönetici onayıyla kapatır.</summary>
    protected async Task DonemiKapatAsync(int yil, int ay, string? elleOnayNotu = "Sentetik test dönemi; madde elle doğrulandı.")
    {
        var srv = S<IDonemKapanisServisi>();
        var donem = await srv.GetOrCreateAsync(yil, ay);
        await srv.MutabakatYapAsync(donem.Id, Muhasebe);
        var maddeler = await srv.KontrolListesiDegerlendirAsync(donem.Id);
        foreach (var m in maddeler.Where(m => m.MaddeKodu != "YONETICI" && !m.Tamamlandi))
            await srv.MaddeTamamlaAsync(m.Id, Muhasebe, m.OtomatikSonuc == false ? elleOnayNotu : null);
        var yon = maddeler.First(m => m.MaddeKodu == "YONETICI");
        await srv.MaddeTamamlaAsync(yon.Id, Yonetici, "Yönetici kapanış onayı");
        await srv.KapatAsync(donem.Id, Yonetici, "Test kapanışı");
    }
}
