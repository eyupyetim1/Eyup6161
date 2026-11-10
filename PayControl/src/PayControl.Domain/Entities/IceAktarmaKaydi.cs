namespace PayControl.Domain.Entities;

/// <summary>
/// Excel/PDF içe aktarma işleminin denetim kaydı. Aynı dosya hash ile mükerrer üretim engellenir.
/// </summary>
public class IceAktarmaKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DosyaAdi { get; set; } = string.Empty;
    public string DosyaHashSha256 { get; set; } = string.Empty;
    public string KaynakTuru { get; set; } = "BordroExcel";

    public int? DonemYil { get; set; }
    public int? DonemAy { get; set; }

    public int SatirSayisi { get; set; }
    public int BasariliSatir { get; set; }
    public int HataliSatir { get; set; }

    public DateTime IceAktarmaZamani { get; set; } = DateTime.UtcNow;
    public string? KullaniciAdi { get; set; }

    public bool GeriAlindi { get; set; }
    public DateTime? GeriAlmaZamani { get; set; }
    public string? GeriAlanKullanici { get; set; }

    public ICollection<MaasDonemKaydi> MaasDonemKayitlari { get; set; } = new List<MaasDonemKaydi>();
}
