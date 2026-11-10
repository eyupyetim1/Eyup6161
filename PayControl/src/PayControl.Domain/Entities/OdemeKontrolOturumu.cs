namespace PayControl.Domain.Entities;

/// <summary>
/// Bankaya göndermeden önce bordro × banka ödeme listesi kontrol oturumu.
/// </summary>
public class OdemeKontrolOturumu
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int DonemYil { get; set; }
    public int DonemAy { get; set; }
    public string? Sirket { get; set; }

    public string? BordroDosyaAdi { get; set; }
    public string? BankaDosyaAdi { get; set; }
    public string? BordroDosyaHash { get; set; }
    public string? BankaDosyaHash { get; set; }

    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    public string? OlusturanKullanici { get; set; }

    public bool KontrolTamamlandi { get; set; }
    public DateTime? OnayZamani { get; set; }
    public string? OnaylayanKullanici { get; set; }

    public ICollection<OdemeKontrolSatiri> Satirlar { get; set; } = new List<OdemeKontrolSatiri>();
    public ICollection<OnayliOdemeListesi> OnayliListeler { get; set; } = new List<OnayliOdemeListesi>();
}
