namespace PayControl.Domain.Entities;

/// <summary>
/// Kapanış anında personel bazında dondurulmuş değerler. Sonradan değişen personel/ödeme
/// kayıtları kapatılmış dönemin raporunu etkilemez.
/// </summary>
public class DonemKapanisAnlikGoruntusu
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaasDonemKapanisiId { get; set; }
    public int KapanisNo { get; set; }

    public Guid PersonelId { get; set; }
    public string TcKimlikNo { get; set; } = string.Empty;
    public string AdSoyad { get; set; } = string.Empty;
    public string? Iban { get; set; }
    /// <summary>Tesis / işyeri (boş: tüm tesisler).</summary>
    public string? Tesis { get; set; }
    public decimal? MaasTutar { get; set; }
    public decimal BordroNet { get; set; }
    public decimal Odenen { get; set; }
    public decimal Fark { get; set; }
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
}
