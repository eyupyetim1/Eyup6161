using PayControl.Application.Dtos;

namespace PayControl.Application.Interfaces;

public interface IPersonelServisi
{
    Task<IReadOnlyList<PersonelOzetDto>> ListeleAsync(
        string? arama = null,
        string? sirket = null,
        string? isyeri = null,
        CancellationToken ct = default);

    Task<PersonelDetayDto?> GetirAsync(Guid id, CancellationToken ct = default);

    Task<PersonelDetayDto> OlusturAsync(PersonelOlusturDto dto, string kullaniciAdi, CancellationToken ct = default);

    Task<PersonelDetayDto> GuncelleAsync(Guid id, PersonelGuncelleDto dto, string kullaniciAdi, string? neden, CancellationToken ct = default);

    /// <summary>İşten ayrılmış personeli aynı kartta yeni çalışma dönemiyle yeniden işe alır; geçmiş silinmez.</summary>
    Task<PersonelDetayDto> YenidenIseAlAsync(Guid id, DateOnly iseGirisTarihi, string? isyeri, string? gorevi, string? sirket,
        decimal? maas, string kullaniciAdi, CancellationToken ct = default);
}

public sealed class PersonelOlusturDto
{
    public string TcKimlikNo { get; set; } = string.Empty;
    public string AdSoyad { get; set; } = string.Empty;
    public string? SicilNo { get; set; }
    public string? Sirket { get; set; }
    public string? Isyeri { get; set; }
    public string? Gorevi { get; set; }
    public DateOnly? IseGirisTarihi { get; set; }
    public decimal? GuncelMaas { get; set; }
    public string? BankaAdi { get; set; }
    public string? Iban { get; set; }
    public string? Notlar { get; set; }
}

public sealed class PersonelGuncelleDto
{
    public string AdSoyad { get; set; } = string.Empty;
    public string? SicilNo { get; set; }
    public string? Sirket { get; set; }
    public string? Isyeri { get; set; }
    public string? Gorevi { get; set; }
    public decimal? GuncelMaas { get; set; }
    public string? BankaAdi { get; set; }
    public string? Iban { get; set; }
    public string? Notlar { get; set; }
}
