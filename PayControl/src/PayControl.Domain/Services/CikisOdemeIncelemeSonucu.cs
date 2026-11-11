using PayControl.Domain.Enums;

namespace PayControl.Domain.Services;

public sealed class CikisOdemeIncelemeSonucu
{
    public OdemeSiniflandirma Siniflandirma { get; init; }
    public string Gerekce { get; init; } = string.Empty;
    public decimal IncelenenTutar { get; init; }
    public decimal? BeklenenHakEdis { get; init; }
    public decimal? ToplamOdenen { get; init; }
    public decimal? Fark { get; init; }
    public bool KullaniciOnayiGerekli { get; init; }
}
