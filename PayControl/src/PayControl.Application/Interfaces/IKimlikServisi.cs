using PayControl.Domain.Enums;

namespace PayControl.Application.Interfaces;

public interface IKimlikServisi
{
    Task<GirisSonucu> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken ct = default);
    Task EnsureAdminAsync(CancellationToken ct = default);

    /// <summary>Kullanıcı kendi şifresini değiştirir (eski şifre doğrulanır, politika uygulanır).</summary>
    Task SifreDegistirAsync(string kullaniciAdi, string eskiSifre, string yeniSifre, CancellationToken ct = default);

    Task<IReadOnlyList<KullaniciDto>> ListeleAsync(CancellationToken ct = default);
    Task<KullaniciDto> OlusturAsync(KullaniciOlusturDto dto, string yapan, CancellationToken ct = default);
    Task GuncelleAsync(Guid id, KullaniciRolu rol, bool aktif, string? adSoyad, string? eposta, string? telefon, string yapan, CancellationToken ct = default);
    /// <summary>Geçici şifre atar; kullanıcı ilk girişte değiştirmek zorundadır.</summary>
    Task SifreSifirlaAsync(Guid id, string geciciSifre, string yapan, CancellationToken ct = default);
}

public sealed class GirisSonucu
{
    public bool Basarili { get; init; }
    public Guid? KullaniciId { get; init; }
    public string? KullaniciAdi { get; init; }
    public KullaniciRolu? Rol { get; init; }
    public string? Hata { get; init; }
    public bool SifreDegistirmeGerekli { get; init; }
}

public sealed class KullaniciDto
{
    public Guid Id { get; init; }
    public string KullaniciAdi { get; init; } = string.Empty;
    public string? AdSoyad { get; init; }
    public string? Eposta { get; init; }
    public string? Telefon { get; init; }
    public KullaniciRolu Rol { get; init; }
    public bool Aktif { get; init; }
    public bool Kilitli { get; init; }
    public bool SifreDegistirmeGerekli { get; init; }
    public DateTime? SonGiris { get; init; }
}

public sealed class KullaniciOlusturDto
{
    public string KullaniciAdi { get; set; } = string.Empty;
    public string Sifre { get; set; } = string.Empty;
    public KullaniciRolu Rol { get; set; } = KullaniciRolu.Goruntuleyici;
    public string? AdSoyad { get; set; }
    public string? Eposta { get; set; }
    public string? Telefon { get; set; }
}
