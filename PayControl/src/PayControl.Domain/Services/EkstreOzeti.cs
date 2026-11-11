namespace PayControl.Domain.Services;

/// <summary>
/// Ekstre üst bölümü. Maaş alacağı, avans borcu, şüpheli fazla ödeme ve kesinleşmiş geri ödeme
/// ayrı bakiyelerdir; birbirinden mahsup edilmez.
/// </summary>
public sealed class EkstreOzeti
{
    public decimal ToplamHakEdis { get; init; }
    public decimal GerceklesenToplamOdeme { get; init; }

    /// <summary>Dönem bazında (hak ediş − ödeme) pozitif farkların toplamı.</summary>
    public decimal OdenmemisHakEdis { get; init; }

    /// <summary>Dönem bazında hak edişi aşan ödemelerin toplamı (mutabakatla incelemeye alınır).</summary>
    public decimal DonemFazlaOdemeFarki { get; init; }

    public decimal AcikAvansBorcu { get; init; }
    public decimal IncelemeBekleyenFazlaOdeme { get; init; }
    public decimal KesinlesmisGeriOdemeBorcu { get; init; }
    public decimal ToplamKesinti { get; init; }
    public int HareketSayisi { get; init; }
}

public sealed class DonemBakiyesi
{
    public int? DonemYil { get; init; }
    public int? DonemAy { get; init; }
    public string DonemEtiket => DonemYil is null || DonemAy is null ? "Dönemsiz" : $"{DonemYil:D4}-{DonemAy:D2}";
    public decimal HakEdis { get; init; }
    public decimal Odenen { get; init; }
    public decimal Fark => Odenen - HakEdis;
    public decimal AvansMahsup { get; init; }
    public decimal Kesintiler { get; init; }
    public string Durum => Math.Abs(Fark) < 0.01m ? "Kapandı" : Fark < 0 ? "Eksik ödeme / ödenmemiş" : "Fazla ödeme";
}
