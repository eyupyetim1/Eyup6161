namespace PayControl.Domain.Enums;

/// <summary>
/// Mali hareketin hangi bakiyeyi etkilediği — tek bakiyede karıştırılmaz.
/// </summary>
public enum BakiyeTuru
{
    MaasAlacagi = 0,
    AvansBorcu = 1,
    SupheliFazlaOdeme = 2,
    KesinlesmisGeriOdeme = 3,
    Bilgi = 4
}
