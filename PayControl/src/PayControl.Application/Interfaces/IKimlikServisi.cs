using PayControl.Domain.Enums;

namespace PayControl.Application.Interfaces;

public interface IKimlikServisi
{
    Task<GirisSonucu> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken ct = default);
    Task EnsureAdminAsync(CancellationToken ct = default);
}

public sealed class GirisSonucu
{
    public bool Basarili { get; init; }
    public Guid? KullaniciId { get; init; }
    public string? KullaniciAdi { get; init; }
    public KullaniciRolu? Rol { get; init; }
    public string? Hata { get; init; }
}
