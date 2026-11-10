using PayControl.Domain.Entities;

namespace PayControl.Application.Interfaces;

public interface IAvansServisi
{
    Task<AvansKaydi> VerAsync(
        Guid personelId,
        DateOnly tarih,
        decimal tutar,
        int taksitSayisi,
        decimal aylikMahsup,
        string? tur,
        string kullanici,
        string? aciklama = null,
        CancellationToken ct = default);

    Task<AvansMahsupKaydi> MahsupGerceklestirAsync(
        Guid avansId,
        int donemYil,
        int donemAy,
        decimal tutar,
        string kullanici,
        bool bordroyaGirildi = true,
        CancellationToken ct = default);

    /// <summary>
    /// Bordrodaki avans kesintisini açık avanslara (en eskiden başlayarak) mahsup eder.
    /// SaveChanges çağırmaz; bordro içe aktarma ile aynı işlemde kaydedilir.
    /// </summary>
    Task<BordroAvansMahsupSonucu> BordroMahsupuUygulaAsync(
        Guid personelId, int donemYil, int donemAy, decimal kesinti, Guid iceAktarmaId, string? dosyaAdi,
        string kullanici, CancellationToken ct = default);

    /// <summary>Bordro geri alındığında o bordrodan gelen mahsupları iade eder (ters kayıt ile).</summary>
    Task BordroMahsupuGeriAlAsync(Guid iceAktarmaId, string kullanici, CancellationToken ct = default);

    Task<IReadOnlyList<AvansKaydi>> ListeleAsync(Guid personelId, CancellationToken ct = default);
}

public sealed class BordroAvansMahsupSonucu
{
    public decimal BordroKesintisi { get; init; }
    public decimal Uygulanan { get; init; }
    public decimal DahaOnceIslenmis { get; init; }
    /// <summary>Açık avans borcunu aşan kesinti (personelden borcu olmadan kesilmiş olabilir).</summary>
    public decimal AcikBorcuAsan { get; init; }
    public IReadOnlyList<string> Uyarilar { get; init; } = Array.Empty<string>();
}
