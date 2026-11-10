using PayControl.Domain.Enums;

namespace PayControl.Desktop.Services;

public sealed class OturumServisi
{
    public Guid? KullaniciId { get; private set; }
    public string? KullaniciAdi { get; private set; }
    public KullaniciRolu? Rol { get; private set; }
    public bool GirisYapildi => KullaniciId.HasValue;

    public void Baslat(Guid id, string kullaniciAdi, KullaniciRolu rol)
    {
        KullaniciId = id;
        KullaniciAdi = kullaniciAdi;
        Rol = rol;
    }

    public void Bitir()
    {
        KullaniciId = null;
        KullaniciAdi = null;
        Rol = null;
    }

    public bool MaasGorebilir =>
        Rol is KullaniciRolu.Yonetici or KullaniciRolu.Muhasebe;
}
