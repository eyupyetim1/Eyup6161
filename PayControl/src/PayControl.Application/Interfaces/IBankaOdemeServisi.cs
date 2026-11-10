using PayControl.Domain.Entities;

namespace PayControl.Application.Interfaces;

public interface IBankaOdemeServisi
{
    /// <summary>
    /// Gerçekleşen banka ödemesini kaydeder. Aynı referans mükerrer engellenir.
    /// </summary>
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
}
