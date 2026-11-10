namespace PayControl.Domain.Entities;

public class PersonelBelge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonelId { get; set; }
    public Personel? Personel { get; set; }

    public string Baslik { get; set; } = string.Empty;
    public string DosyaYolu { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public DateTime YuklemeZamani { get; set; } = DateTime.UtcNow;
}
