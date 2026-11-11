namespace PayControl.Domain.Entities;

/// <summary>Herhangi bir kayda eklenen kaynak belge (PDF, görüntü, Excel). Silinmez; gerekirse geçersiz işaretlenir.</summary>
public class Belge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string VarlikTuru { get; set; } = string.Empty;
    public Guid? VarlikId { get; set; }
    public Guid? PersonelId { get; set; }
    public string Baslik { get; set; } = string.Empty;
    public string OrijinalAd { get; set; } = string.Empty;
    public string DepoYolu { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long Boyut { get; set; }
    public string? Aciklama { get; set; }
    public string YukleyenKullanici { get; set; } = string.Empty;
    public DateTime YuklemeZamani { get; set; } = DateTime.UtcNow;
    public bool Gecersiz { get; set; }
    public string? GecersizNedeni { get; set; }
}
