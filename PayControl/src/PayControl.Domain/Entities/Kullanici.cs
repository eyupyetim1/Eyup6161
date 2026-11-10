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
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
}
