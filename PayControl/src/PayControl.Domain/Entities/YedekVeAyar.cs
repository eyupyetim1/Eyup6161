namespace PayControl.Domain.Entities;

public class YedekKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DosyaYolu { get; set; } = string.Empty;
    public long Boyut { get; set; }
    public string? Sha256 { get; set; }
    public bool Sifreli { get; set; }
    public bool Otomatik { get; set; }
    public bool Basarili { get; set; }
    public string? Hata { get; set; }
    public string? Kullanici { get; set; }
    public DateTime Zaman { get; set; } = DateTime.UtcNow;
}

/// <summary>Anahtar-değer uygulama ayarı.</summary>
public class UygulamaAyari
{
    public string Anahtar { get; set; } = string.Empty;
    public string? Deger { get; set; }
    public DateTime GuncellemeZamani { get; set; } = DateTime.UtcNow;
}
