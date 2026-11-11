using PayControl.Domain.Entities;

namespace PayControl.Application.Interfaces;

public interface IYedekServisi
{
    /// <summary>Tutarlı anlık yedek alır (SQLite çevrimiçi yedekleme API'si); şifre ayarlıysa AES-GCM ile şifreler.</summary>
    Task<YedekKaydi> YedekAlAsync(string kullanici, bool otomatik = false, string? hedefKlasor = null, CancellationToken ct = default);

    /// <summary>Otomatik yedek açık ve son başarılı otomatik yedek 24 saatten eskiyse yedek alır.</summary>
    Task<YedekKaydi?> OtomatikYedekGerekirseAsync(CancellationToken ct = default);

    /// <summary>Yedeği doğrular, önce güvenlik yedeği alır, ardından veritabanına geri yükler. Uygulama yeniden başlatılmalıdır.</summary>
    Task<string> GeriYukleAsync(string dosyaYolu, string? sifre, string kullanici, CancellationToken ct = default);

    Task<IReadOnlyList<YedekKaydi>> ListeleAsync(CancellationToken ct = default);
    Task YedekSifresiAyarlaAsync(string? sifre, string kullanici, CancellationToken ct = default);
}

/// <summary>Kullanılan veritabanı (yedekleme ve şema yükseltme için).</summary>
public sealed record VeritabaniBilgisi(string Saglayici, string BaglantiMetni, string? DosyaYolu)
{
    public bool SqliteMi => Saglayici == "Sqlite";
}
