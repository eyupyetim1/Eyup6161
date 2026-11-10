namespace PayControl.Domain.Entities;

/// <summary>
/// Kontrol sonrası üretilen onaylı banka ödeme listesi sürümü.
/// Program bankaya göndermez; yalnızca dosya üretir.
/// </summary>
public class OnayliOdemeListesi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OdemeKontrolOturumuId { get; set; }
    public OdemeKontrolOturumu? OdemeKontrolOturumu { get; set; }

    public int Surum { get; set; } = 1;
    public int DonemYil { get; set; }
    public int DonemAy { get; set; }

    public string DosyaAdi { get; set; } = string.Empty;
    public string? DosyaYolu { get; set; }
    public string? DosyaHashSha256 { get; set; }

    public int SatirSayisi { get; set; }
    public decimal ToplamTutar { get; set; }

    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    public string? OlusturanKullanici { get; set; }
    public string? KaynakFormatNotu { get; set; }
}
