namespace PayControl.Domain.Entities;

/// <summary>Kesinleşmiş fazla ödemenin bordrodan taksitle geri alınma planı.</summary>
public class TahsilatPlani
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonelId { get; set; }
    public decimal Tutar { get; set; }
    public int TaksitSayisi { get; set; }
    public decimal AylikTutar { get; set; }
    public decimal KalanBorc { get; set; }
    public int BaslangicYil { get; set; }
    public int BaslangicAy { get; set; }
    public string? Aciklama { get; set; }
    public string? OlusturanKullanici { get; set; }
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    public ICollection<TahsilatTaksiti> Taksitler { get; set; } = new List<TahsilatTaksiti>();
}

public class TahsilatTaksiti
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TahsilatPlaniId { get; set; }
    public TahsilatPlani? TahsilatPlani { get; set; }
    public int DonemYil { get; set; }
    public int DonemAy { get; set; }
    public decimal Planlanan { get; set; }
    public decimal? Gerceklesen { get; set; }
    public Guid? KaynakIceAktarmaId { get; set; }
    public DateTime? GerceklesmeZamani { get; set; }
}
