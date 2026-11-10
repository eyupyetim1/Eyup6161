using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

/// <summary>
/// Aylık maaş dönemi kontrol ve kapanış kaydı.
/// </summary>
public class MaasDonemKapanisi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int DonemYil { get; set; }
    public int DonemAy { get; set; }
    public string? Sirket { get; set; }

    public MaasDonemDurumu Durum { get; set; } = MaasDonemDurumu.Hazirlaniyor;

    public decimal? ToplamBeklenenMaas { get; set; }
    public decimal? ToplamBankaOdemesi { get; set; }
    public decimal? ToplamEksikOdeme { get; set; }
    public decimal? ToplamFazlaOdeme { get; set; }
    public decimal? OdenmemisHakEdisler { get; set; }
    public decimal? AcikAvanslar { get; set; }
    public decimal? CikisSonrasiSupheli { get; set; }
    public int CozulmemisUyariSayisi { get; set; }
    public int GerekceliIstisnaSayisi { get; set; }

    public DateTime? KapanisZamani { get; set; }
    public string? KapatanKullanici { get; set; }
    public string? KapanisNotu { get; set; }

    public DateTime? YenidenAcmaZamani { get; set; }
    public string? YenidenAcanKullanici { get; set; }
    public string? YenidenAcmaNedeni { get; set; }

    /// <summary>Kaç kez kapatıldığı (yeniden açılıp tekrar kapatılanlar dahil).</summary>
    public int KapanisSayisi { get; set; }
    public DateTime? MutabakatZamani { get; set; }

    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;

    public ICollection<DonemKontrolMaddesi> KontrolMaddeleri { get; set; } = new List<DonemKontrolMaddesi>();
    public ICollection<MutabakatFarki> Farklar { get; set; } = new List<MutabakatFarki>();
    public ICollection<DonemDuzeltmeKaydi> Duzeltmeler { get; set; } = new List<DonemDuzeltmeKaydi>();
}
