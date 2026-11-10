using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Dtos;

public sealed class MaliHareketDto
{
    public Guid Id { get; init; }
    public string IslemKimligi { get; init; } = string.Empty;
    public DateOnly IslemTarihi { get; init; }
    public int? DonemYil { get; init; }
    public int? DonemAy { get; init; }
    public string DonemEtiket => DonemYil is null || DonemAy is null ? "-" : $"{DonemYil:D4}-{DonemAy:D2}";
    public MaliIslemTuru IslemTuru { get; init; }
    public BakiyeTuru BakiyeTuru { get; init; }
    public decimal Tutar { get; init; }
    public string Aciklama { get; init; } = string.Empty;
    public string? KaynakBelge { get; init; }
    public string? KaynakReferans { get; init; }
    public string? KullaniciAdi { get; init; }
    public Guid? CalismaDonemiId { get; init; }
}

public sealed class HesapEkstresiDto
{
    public Guid PersonelId { get; init; }
    public string AdSoyad { get; init; } = string.Empty;
    public string TcKimlikNo { get; init; } = string.Empty;
    public EkstreOzeti Ozet { get; init; } = new();
    public IReadOnlyList<MaliHareketDto> Hareketler { get; init; } = Array.Empty<MaliHareketDto>();
    public IReadOnlyList<CalismaDonemiDto> CalismaDonemleri { get; init; } = Array.Empty<CalismaDonemiDto>();
}

public sealed class HesapEkstresiFiltre
{
    public DateOnly? Baslangic { get; set; }
    public DateOnly? Bitis { get; set; }
    public string? Sirket { get; set; }
    public string? Isyeri { get; set; }
    public MaliIslemTuru? IslemTuru { get; set; }
    public Guid? CalismaDonemiId { get; set; }
}
