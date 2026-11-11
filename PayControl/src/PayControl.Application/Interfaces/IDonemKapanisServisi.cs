using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Application.Interfaces;

public interface IDonemKapanisServisi
{
    Task<MaasDonemKapanisi> GetOrCreateAsync(int yil, int ay, string? sirket = null, CancellationToken ct = default);
    Task DurumGuncelleAsync(Guid donemId, MaasDonemDurumu durum, string kullanici, CancellationToken ct = default);

    /// <summary>Kontrol listesinin otomatik veri kontrollerini çalıştırır (kapalı dönemde yalnızca okur).</summary>
    Task<IReadOnlyList<DonemKontrolMaddesi>> KontrolListesiDegerlendirAsync(Guid donemId, CancellationToken ct = default);

    /// <summary>
    /// Maddeyi tamamlar. Otomatik kontrolü başarısız madde ancak gerekçe ile elle onaylanabilir.
    /// "Yönetici kapanış onayı" maddesi Yönetici rolü ve diğer tüm maddelerin tamamlanmasını gerektirir.
    /// </summary>
    Task MaddeTamamlaAsync(Guid maddeId, string kullanici, string? not = null, CancellationToken ct = default);

    /// <summary>Bordro × onaylı liste × gerçekleşen banka ödemesi mutabakatı.</summary>
    Task<IReadOnlyList<MutabakatFarki>> MutabakatYapAsync(Guid donemId, string kullanici, CancellationToken ct = default);

    Task FarkCozAsync(Guid farkId, FarkDurumu durum, string aciklama, string kullanici, CancellationToken ct = default);
    Task<IReadOnlyList<MutabakatFarki>> FarklarAsync(Guid donemId, CancellationToken ct = default);

    Task OzetHesaplaAsync(Guid donemId, CancellationToken ct = default);
    Task KapatAsync(Guid donemId, string kullanici, string? not, CancellationToken ct = default);
    Task YenidenAcAsync(Guid donemId, string kullanici, string neden, CancellationToken ct = default);

    Task<DonemDuzeltmeKaydi> DuzeltmeEkleAsync(
        Guid donemId,
        Guid? personelId,
        string varlikTuru,
        Guid? varlikId,
        string alan,
        string? eski,
        string? yeni,
        string neden,
        string kullanici,
        CancellationToken ct = default);

    /// <summary>
    /// Düzeltme kaydı: eski kayıt değişmez; sayısal fark <paramref name="uygulanacakYil"/>/<paramref name="uygulanacakAy"/>
    /// (varsayılan: düzeltilen dönemden sonraki ilk açık dönem) dönemine devreden düzeltme olarak deftere yazılır.
    /// </summary>
    Task<DonemDuzeltmeKaydi> DuzeltmeEkleAsync(
        Guid donemId,
        Guid? personelId,
        string varlikTuru,
        Guid? varlikId,
        string alan,
        string? eski,
        string? yeni,
        string neden,
        string kullanici,
        int? uygulanacakYil,
        int? uygulanacakAy,
        bool? maliEtkili,
        CancellationToken ct = default);

    Task<DonemKarsilastirmaDto> OncekiAyKarsilastirAsync(int yil, int ay, CancellationToken ct = default);
    Task<KapanisRaporuDto> KapanisRaporuAsync(Guid donemId, CancellationToken ct = default);
    Task<byte[]> KapanisRaporuExcelAsync(Guid donemId, CancellationToken ct = default);
    Task EnsureDonemAcikAsync(int yil, int ay, CancellationToken ct = default);
}

public sealed class DonemKarsilastirmaDto
{
    public int OncekiYil { get; init; }
    public int OncekiAy { get; init; }
    public int OncekiPersonelSayisi { get; init; }
    public int BuAyPersonelSayisi { get; init; }
    public int IstenAyrilan { get; init; }
    public int IseBaslayan { get; init; }
    public int MaasDegisen { get; init; }
    public decimal OncekiToplamNet { get; init; }
    public decimal BuAyToplamNet { get; init; }
    public decimal ToplamFark => BuAyToplamNet - OncekiToplamNet;

    /// <summary>Toplam farkın kaynaklara göre ayrıştırması: ayrılan + başlayan + maaş değişikliği + diğer = toplam fark.</summary>
    public decimal AyrilanEtkisi { get; init; }
    public decimal BaslayanEtkisi { get; init; }
    public decimal MaasDegisikligiEtkisi { get; init; }
    public decimal DigerNetEtkisi { get; init; }

    public IReadOnlyList<string> Nedenler { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DonemDegisiklikDetayi> Detaylar { get; init; } = Array.Empty<DonemDegisiklikDetayi>();
}

public sealed class DonemDegisiklikDetayi
{
    public string Tur { get; init; } = string.Empty;
    public Guid PersonelId { get; init; }
    public string TcKimlikNo { get; init; } = string.Empty;
    public string AdSoyad { get; init; } = string.Empty;
    public decimal? OncekiTutar { get; init; }
    public decimal? YeniTutar { get; init; }
    public decimal NetEtki { get; init; }
    /// <summary>Değişikliğin dayandığı kaynak kayıt (çıkış kaydı, çalışma dönemi, maaş değişikliği denetim kaydı, bordro kalemleri).</summary>
    public string Kaynak { get; init; } = string.Empty;
    public string? KaynakReferans { get; init; }
}

public sealed class KapanisRaporuDto
{
    public Guid DonemId { get; init; }
    public int DonemYil { get; init; }
    public int DonemAy { get; init; }
    public MaasDonemDurumu Durum { get; init; }
    public int KapanisSayisi { get; init; }
    /// <summary>true: değerler kapanış anında dondurulmuş anlık görüntüden gelir.</summary>
    public bool DondurulmusVeri { get; init; }

    public decimal ToplamBeklenenMaas { get; init; }
    public decimal ToplamBankaOdemesi { get; init; }
    public decimal ToplamEksikOdeme { get; init; }
    public decimal ToplamFazlaOdeme { get; init; }
    public decimal OdenmemisHakEdisler { get; init; }
    public decimal AcikAvanslar { get; init; }
    public decimal CikisSonrasiSupheli { get; init; }
    public int CozulmemisUyariSayisi { get; init; }
    public int GerekceliIstisnaSayisi { get; init; }

    public IReadOnlyList<MutabakatFarki> Farklar { get; init; } = Array.Empty<MutabakatFarki>();
    public IReadOnlyList<string> Istisnalar { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DonemKontrolMaddesi> KontrolMaddeleri { get; init; } = Array.Empty<DonemKontrolMaddesi>();
    public IReadOnlyList<DonemKapanisAnlikGoruntusu> AnlikGoruntu { get; init; } = Array.Empty<DonemKapanisAnlikGoruntusu>();
    public IReadOnlyList<DonemKapanisOzeti> KapanisGecmisi { get; init; } = Array.Empty<DonemKapanisOzeti>();
    public IReadOnlyList<DonemDuzeltmeKaydi> Duzeltmeler { get; init; } = Array.Empty<DonemDuzeltmeKaydi>();
    public DonemKarsilastirmaDto? Karsilastirma { get; init; }
}
