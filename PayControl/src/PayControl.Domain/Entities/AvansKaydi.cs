namespace PayControl.Domain.Entities;

/// <summary>
/// Personel avans kartı. Verilmiş avans otomatik mahsup sayılmaz.
/// </summary>
public class AvansKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonelId { get; set; }
    public Personel? Personel { get; set; }

    public DateOnly AvansTarihi { get; set; }
    public decimal Tutar { get; set; }
    public string? AvansTuru { get; set; }
    public string? OdemeSekli { get; set; }
    public string? BankaKasa { get; set; }
    public string? Veren { get; set; }
    public string? Onaylayan { get; set; }
    public string? Aciklama { get; set; }

    public int TaksitSayisi { get; set; } = 1;
    public decimal AylikMahsupTutari { get; set; }
    public decimal KalanBorc { get; set; }

    public int? IlgiliBordroDonemYil { get; set; }
    public int? IlgiliBordroDonemAy { get; set; }

    public string? BelgeYolu { get; set; }
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    public string? OlusturanKullanici { get; set; }

    public ICollection<AvansMahsupKaydi> Mahsuplar { get; set; } = new List<AvansMahsupKaydi>();
}
