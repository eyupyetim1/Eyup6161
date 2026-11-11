namespace PayControl.Domain.Entities;

/// <summary>
/// Finansal geçmiş değişikliklerinin denetim izi.
/// </summary>
public class DenetimKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? PersonelId { get; set; }
    public Personel? Personel { get; set; }

    public string VarlikTuru { get; set; } = string.Empty;
    public Guid? VarlikId { get; set; }
    public string Islem { get; set; } = string.Empty;
    public string? AlanAdi { get; set; }
    public string? EskiDeger { get; set; }
    public string? YeniDeger { get; set; }
    public string? DegisiklikNedeni { get; set; }
    public string? KullaniciAdi { get; set; }
    public DateTime IslemZamani { get; set; } = DateTime.UtcNow;
}
