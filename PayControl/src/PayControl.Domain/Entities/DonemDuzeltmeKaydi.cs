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
    public string Neden { get; set; } = string.Empty;
    public string KullaniciAdi { get; set; } = string.Empty;
    public DateTime IslemZamani { get; set; } = DateTime.UtcNow;
}
