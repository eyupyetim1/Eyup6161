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
    public string IslemTuruEtiket => MaliHareketKurallari.TurEtiketi(IslemTuru);
    public BakiyeTuru BakiyeTuru { get; init; }
    public string BakiyeEtiket => MaliHareketKurallari.BakiyeEtiketi(BakiyeTuru);
    public decimal Tutar { get; init; }
    /// <summary>Kendi bakiyesi üzerindeki işaretli etki (örn. mahsup −).</summary>
    public decimal Etki { get; init; }
    public string Aciklama { get; init; } = string.Empty;
    public string? KaynakTuru { get; init; }
    public string? KaynakBelge { get; init; }
    public string? KaynakReferans { get; init; }
    public string? KullaniciAdi { get; init; }
    public DateTime KayitZamani { get; init; }
    public Guid? CalismaDonemiId { get; init; }
    public string? Sirket { get; init; }
    public string? Isyeri { get; init; }
    public bool TersKayitMi { get; init; }

    // Yürüyen bakiyeler — dört bakiye ayrı izlenir.
    public decimal MaasAlacagiBakiyesi { get; init; }
    public decimal AvansBorcuBakiyesi { get; init; }
    public decimal SupheliFazlaBakiyesi { get; init; }
    public decimal GeriOdemeBakiyesi { get; init; }
}

public sealed class CalismaDonemiOzetDto
{
    public CalismaDonemiDto Donem { get; init; } = new();
    public EkstreOzeti Ozet { get; init; } = new();
    public string Etiket => $"{Donem.IseGirisTarihi:dd.MM.yyyy} – {(Donem.IstenCikisTarihi.HasValue ? Donem.IstenCikisTarihi.Value.ToString("dd.MM.yyyy") : "devam")} ({Donem.Isyeri})";
}

public sealed class HesapEkstresiDto
{
    public Guid PersonelId { get; init; }
    public string AdSoyad { get; init; } = string.Empty;
    public string TcKimlikNo { get; init; } = string.Empty;
    public DateOnly? IlkIseGirisTarihi { get; init; }
    public DateOnly? SonCikisTarihi { get; init; }
    public CalismaDurumu CalismaDurumu { get; init; }

    /// <summary>Filtreye uyan hareketlerin özeti.</summary>
    public EkstreOzeti Ozet { get; init; } = new();
    /// <summary>Filtreden bağımsız, işe girişten bugüne genel bakiye.</summary>
    public EkstreOzeti GenelOzet { get; init; } = new();

    public IReadOnlyList<MaliHareketDto> Hareketler { get; init; } = Array.Empty<MaliHareketDto>();
    public IReadOnlyList<DonemBakiyesi> DonemBakiyeleri { get; init; } = Array.Empty<DonemBakiyesi>();
    public IReadOnlyList<CalismaDonemiDto> CalismaDonemleri { get; init; } = Array.Empty<CalismaDonemiDto>();
    public IReadOnlyList<CalismaDonemiOzetDto> CalismaDonemiOzetleri { get; init; } = Array.Empty<CalismaDonemiOzetDto>();
    public HesapEkstresiFiltre Filtre { get; init; } = new();
    public string FiltreAciklamasi { get; init; } = "Tüm hareketler";
}

public sealed class HesapEkstresiFiltre
{
    public DateOnly? Baslangic { get; set; }
    public DateOnly? Bitis { get; set; }
    public string? Sirket { get; set; }
    /// <summary>Tesis / işyeri.</summary>
    public string? Isyeri { get; set; }
    public MaliIslemTuru? IslemTuru { get; set; }
    public BakiyeTuru? BakiyeTuru { get; set; }
    public Guid? CalismaDonemiId { get; set; }
}
