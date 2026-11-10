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
}
