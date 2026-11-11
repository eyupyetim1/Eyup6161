namespace PayControl.Domain.Entities;

/// <summary>Her kapanış işleminin toplamları (yeniden açıp tekrar kapatma dahil tarihçe).</summary>
public class DonemKapanisOzeti
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaasDonemKapanisiId { get; set; }
    public int KapanisNo { get; set; }

    public decimal ToplamBeklenenMaas { get; set; }
    public decimal ToplamBankaOdemesi { get; set; }
    public decimal ToplamEksikOdeme { get; set; }
    public decimal ToplamFazlaOdeme { get; set; }
    public decimal OdenmemisHakEdisler { get; set; }
    public decimal AcikAvanslar { get; set; }
    public decimal CikisSonrasiSupheli { get; set; }
    public int CozulmemisUyariSayisi { get; set; }
    public int GerekceliIstisnaSayisi { get; set; }
    public int PersonelSayisi { get; set; }

    public string KapatanKullanici { get; set; } = string.Empty;
    public string? KapanisNotu { get; set; }
    public DateTime KapanisZamani { get; set; } = DateTime.UtcNow;
}
