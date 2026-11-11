using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

public class Kullanici
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string KullaniciAdi { get; set; } = string.Empty;
    public string SifreHash { get; set; } = string.Empty;
    public string SifreSalt { get; set; } = string.Empty;
    public KullaniciRolu Rol { get; set; } = KullaniciRolu.Goruntuleyici;
    public bool Aktif { get; set; } = true;
    public string? AdSoyad { get; set; }
    public string? Eposta { get; set; }
    public string? Telefon { get; set; }
    /// <summary>Varsayılan / sıfırlanmış şifrede ilk girişte değiştirme zorunlu.</summary>
    public bool SifreDegistirmeGerekli { get; set; }
    public int BasarisizGirisSayisi { get; set; }
    public DateTime? KilitBitis { get; set; }
    public DateTime? SonGiris { get; set; }
    public DateTime? SifreDegisimZamani { get; set; }
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
}
