namespace PayControl.Domain.Entities;

public class DonemKontrolMaddesi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaasDonemKapanisiId { get; set; }
    public MaasDonemKapanisi? MaasDonemKapanisi { get; set; }

    public int Sira { get; set; }
    public string MaddeKodu { get; set; } = string.Empty;
    public string Aciklama { get; set; } = string.Empty;
    public bool Tamamlandi { get; set; }
    public DateTime? TamamlanmaZamani { get; set; }
    public string? TamamlayanKullanici { get; set; }
    public string? Not { get; set; }

    /// <summary>Son otomatik veri kontrolünün sonucu (null: otomatik kontrolü olmayan madde).</summary>
    public bool? OtomatikSonuc { get; set; }
    public string? OtomatikAciklama { get; set; }

    /// <summary>Otomatik kontrol başarısızken gerekçeyle elle onaylandı.</summary>
    public bool ElleOnaylandi { get; set; }
}
