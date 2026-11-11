namespace PayControl.Domain.Entities;

/// <summary>
/// Kapatılmış döneme doğrudan müdahale yerine düzeltme kaydı.
/// Eski kayıt silinmez; eski/yeni tutar ve neden saklanır.
/// </summary>
public class DonemDuzeltmeKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaasDonemKapanisiId { get; set; }
    public MaasDonemKapanisi? MaasDonemKapanisi { get; set; }

    public Guid? PersonelId { get; set; }
    public string VarlikTuru { get; set; } = string.Empty;
    public Guid? VarlikId { get; set; }
    public string AlanAdi { get; set; } = string.Empty;
    public string? EskiDeger { get; set; }
    public string? YeniDeger { get; set; }
    /// <summary>Eski/yeni sayısal ise fark (yeni - eski).</summary>
    public decimal? FarkTutari { get; set; }

    /// <summary>Farkın uygulanacağı (devredeceği) açık dönem.</summary>
    public int? UygulanacakDonemYil { get; set; }
    public int? UygulanacakDonemAy { get; set; }

    public string Neden { get; set; } = string.Empty;
    public string KullaniciAdi { get; set; } = string.Empty;
    public DateTime IslemZamani { get; set; } = DateTime.UtcNow;
}
