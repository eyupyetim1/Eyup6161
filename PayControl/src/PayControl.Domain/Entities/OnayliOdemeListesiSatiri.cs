namespace PayControl.Domain.Entities;

/// <summary>Onaylı ödeme listesine giren her kayıt — dosyanın hangi onaylı kayıtlarla üretildiği.</summary>
public class OnayliOdemeListesiSatiri
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OnayliOdemeListesiId { get; set; }
    public OnayliOdemeListesi? OnayliOdemeListesi { get; set; }

    public Guid? OdemeKontrolSatiriId { get; set; }
    public Guid? PersonelId { get; set; }
    public int TalimatSatirNo { get; set; }
    public string? TcKimlikNo { get; set; }
    public string? AdSoyad { get; set; }
    public string? Iban { get; set; }
    public decimal Tutar { get; set; }

    public string KontrolSonucu { get; set; } = string.Empty;
    public bool IstisnaOnayi { get; set; }
    public string? IstisnaGerekcesi { get; set; }
    public string? IstisnaOnaylayan { get; set; }
}
