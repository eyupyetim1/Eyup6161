namespace PayControl.Domain.Entities;

/// <summary>
/// Yüklenen banka ödeme dosyasının ham satırı. Kullanıcı düzeltmesi orijinal değerleri silmez.
/// </summary>
public class OdemeTalimatSatiri
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OdemeKontrolOturumuId { get; set; }
    public OdemeKontrolOturumu? OdemeKontrolOturumu { get; set; }

    public int SatirNo { get; set; }
    public string? TcKimlikNo { get; set; }
    public string? AdSoyad { get; set; }
    public string? Iban { get; set; }
    public decimal Tutar { get; set; }

    public string? OrijinalIban { get; set; }
    public decimal OrijinalTutar { get; set; }

    /// <summary>Banka şablonundaki diğer hücreler (JSON: kolon → değer).</summary>
    public string? HamHucrelerJson { get; set; }

    /// <summary>Kullanıcı bu talimatı ödeme listesinden çıkardı.</summary>
    public bool Haric { get; set; }

    public bool Duzeltildi { get; set; }
    public string? DuzeltmeGerekcesi { get; set; }
    public string? DuzeltenKullanici { get; set; }
    public DateTime? DuzeltmeZamani { get; set; }
}
