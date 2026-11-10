namespace PayControl.Domain.Services;

public sealed class EkstreOzeti
{
    public decimal ToplamHakEdis { get; init; }
    public decimal GerceklesenToplamOdeme { get; init; }
    public decimal OdenmemisHakEdis { get; init; }
    public decimal AcikAvansBorcu { get; init; }
    public decimal IncelemeBekleyenFazlaOdeme { get; init; }
    public decimal KesinlesmisGeriOdemeBorcu { get; init; }
}
