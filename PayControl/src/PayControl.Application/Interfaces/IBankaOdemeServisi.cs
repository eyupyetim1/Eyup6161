using PayControl.Domain.Entities;

namespace PayControl.Application.Interfaces;

public interface IBankaOdemeServisi
{
    /// <summary>Gerçekleşen banka ödemesini kaydeder. Aynı banka referansı mükerrer engellenir.</summary>
    Task<BankaOdemeKaydi> KaydetAsync(
        Guid personelId,
        DateOnly odemeTarihi,
        decimal tutar,
        int hakEdisYil,
        int hakEdisAy,
        string? iban,
        string? referansNo,
        string kullanici,
        CancellationToken ct = default);

    /// <summary>Kasadan yapılan ödeme (hak ediş dönemine bağlı).</summary>
    Task<BankaOdemeKaydi> KasaOdemesiKaydetAsync(
        Guid personelId,
        DateOnly odemeTarihi,
        decimal tutar,
        int hakEdisYil,
        int hakEdisAy,
        string aciklama,
        string? belgeNo,
        string kullanici,
        CancellationToken ct = default);

    /// <summary>
    /// Bankadan alınan gerçekleşen ödeme hareketlerini içe aktarır. Her satırın tekillik anahtarı
    /// (banka referansı veya tarih+IBAN+tutar+açıklama) ile aynı hareket ikinci kez kaydedilmez.
    /// </summary>
    Task<BankaHareketIceAktarmaSonucu> HareketleriIceAktarAsync(
        int hakEdisYil,
        int hakEdisAy,
        IReadOnlyList<BankaHareketSatiri> satirlar,
        string dosyaAdi,
        string dosyaHash,
        string kullanici,
        CancellationToken ct = default);

    /// <summary>
    /// Yanlış girilmiş banka/kasa ödemesini gerekçeyle iptal eder: kayıt silinmez, mali hareketi ters kayıtla kapanır,
    /// tekillik anahtarı serbest bırakılır (doğru kayıt yeniden girilebilir). Kapalı dönemde yapılamaz.
    /// </summary>
    Task IptalEtAsync(Guid odemeId, string neden, string kullanici, CancellationToken ct = default);

    Task<IReadOnlyList<BankaOdemeKaydi>> PersonelOdemeleriAsync(Guid personelId, CancellationToken ct = default);

    Task<IReadOnlyList<BankaOdemeKaydi>> DonemOdemeleriAsync(int hakEdisYil, int hakEdisAy, CancellationToken ct = default);
}

public sealed record BankaHareketSatiri(
    int SatirNo,
    DateOnly Tarih,
    string? TcKimlikNo,
    string? AdSoyad,
    string? Iban,
    decimal Tutar,
    string? ReferansNo,
    string? Aciklama);

public sealed class BankaHareketIceAktarmaSonucu
{
    public Guid IceAktarmaId { get; init; }
    public int ToplamSatir { get; init; }
    public int Eklenen { get; init; }
    public int MukerrerAtlanan { get; init; }
    public int EslesmeyenKayit { get; init; }
    public bool AyniDosyaDahaOnceYuklendi { get; init; }
    public IReadOnlyList<string> Mesajlar { get; init; } = Array.Empty<string>();
}
