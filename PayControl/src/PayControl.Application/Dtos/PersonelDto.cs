using PayControl.Domain.Enums;

namespace PayControl.Application.Dtos;

public sealed class PersonelOzetDto
{
    public Guid Id { get; init; }
    public string TcKimlikNo { get; init; } = string.Empty;
    public string AdSoyad { get; init; } = string.Empty;
    public string? SicilNo { get; init; }
    public string? Sirket { get; init; }
    public string? Isyeri { get; init; }
    public string? Gorevi { get; init; }
    public CalismaDurumu CalismaDurumu { get; init; }
    public decimal? GuncelMaas { get; init; }
    public string? Iban { get; init; }
}

public sealed class PersonelDetayDto
{
    public Guid Id { get; init; }
    public string TcKimlikNo { get; init; } = string.Empty;
    public string AdSoyad { get; init; } = string.Empty;
    public string? SicilNo { get; init; }
    public string? Sirket { get; init; }
    public string? Isyeri { get; init; }
    public string? Gorevi { get; init; }
    public DateOnly? IlkIseGirisTarihi { get; init; }
    public DateOnly? SonCikisTarihi { get; init; }
    public CalismaDurumu CalismaDurumu { get; init; }
    public decimal? GuncelMaas { get; init; }
    public MaasTuru GuncelMaasTuru { get; init; }
    public string? BankaAdi { get; init; }
    public string? Iban { get; init; }
    public string? Notlar { get; init; }
    public string? Cinsiyet { get; init; }
    public DateOnly? DogumTarihi { get; init; }
    public string? MedeniHal { get; init; }
    public string? Telefon { get; init; }
    public IReadOnlyList<CalismaDonemiDto> CalismaDonemleri { get; init; } = Array.Empty<CalismaDonemiDto>();
    public IReadOnlyList<MaasDonemDto> MaasGecmisi { get; init; } = Array.Empty<MaasDonemDto>();
    public IReadOnlyList<IstenCikisDto> CikisKayitlari { get; init; } = Array.Empty<IstenCikisDto>();
}

public sealed class CalismaDonemiDto
{
    public Guid Id { get; init; }
    public DateOnly IseGirisTarihi { get; init; }
    public DateOnly? IstenCikisTarihi { get; init; }
    public string? Isyeri { get; init; }
    public string? Gorevi { get; init; }
    public string? Sirket { get; init; }
    public CalismaDurumu Durum { get; init; }
}

public sealed class MaasDonemDto
{
    public Guid Id { get; init; }
    public int DonemYil { get; init; }
    public int DonemAy { get; init; }
    public string DonemEtiket => $"{DonemYil:D4}-{DonemAy:D2}";
    public decimal? MaasTutar { get; init; }
    public MaasTuru MaasTuru { get; init; }
    public decimal? Gun { get; init; }
    public decimal NetIstihkak { get; init; }
    public string? Isyeri { get; init; }
    public string? Gorevi { get; init; }
    public string? Iban { get; init; }
    public DateOnly? GirisTarihi { get; init; }
    public DateOnly? CikisTarihi { get; init; }
    public IReadOnlyList<MaasKalemDto> Kalemler { get; init; } = Array.Empty<MaasKalemDto>();
}

public sealed class MaasKalemDto
{
    public string Ad { get; init; } = string.Empty;
    public decimal Tutar { get; init; }
    public bool KesintiMi { get; init; }
    public bool NetIstihkakaDahil { get; init; }
}

public sealed class IstenCikisDto
{
    public Guid Id { get; init; }
    public DateOnly FiiliSonCalismaTarihi { get; init; }
    public DateOnly IstenCikisTarihi { get; init; }
    public SgkCikisDurumu SgkCikisDurumu { get; init; }
    public DateOnly? SgkBildirgeTarihi { get; init; }
    public string? CikisNedeni { get; init; }
    public int? SonBordroDonemYil { get; init; }
    public int? SonBordroDonemAy { get; init; }
    public SonMaasOdemeDurumu SonMaasOdemeDurumu { get; init; }
    public decimal? KalanAvans { get; init; }
    public decimal? IzinUcreti { get; init; }
    public decimal? KidemTazminati { get; init; }
    public decimal? IhbarTazminati { get; init; }
    public string? Aciklama { get; init; }
}
