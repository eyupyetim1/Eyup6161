using PayControl.Domain.Entities;

namespace PayControl.Application.Interfaces;

public sealed class CikisTalimatiOlusturDto
{
    public Guid PersonelId { get; set; }
    public DateOnly SonCalismaGunu { get; set; }
    public string Neden { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    /// <summary>Çıkışı yapacak kişi (bildirim alıcısı). Seçilirse talimat ve hatırlatmalar ona gönderilir.</summary>
    public Guid? SorumluAliciId { get; set; }
}

public enum CikisTakipDurumu
{
    /// <summary>Talimat verildi, son gün henüz gelmedi.</summary>
    Bekliyor = 0,
    /// <summary>Son gün geçti, çıkış kaydı yok — İK unutmuş olabilir.</summary>
    Gecikti = 1,
    /// <summary>Çıkış kaydı yapıldı.</summary>
    CikisYapildi = 2,
    IptalEdildi = 3
}

/// <summary>Son çalışma gününden sonra yapılan / son güne ait ödeme ve nedeni.</summary>
public sealed class CikisSonrasiOdemeDto
{
    public Guid OdemeId { get; init; }
    public DateOnly OdemeTarihi { get; init; }
    public decimal Tutar { get; init; }
    public string HakEdisDonemi { get; init; } = "—";
    public string Kanal { get; init; } = string.Empty;
    public string? Referans { get; init; }
    public string? Aciklama { get; init; }
    /// <summary>Ödemenin ne için yapıldığına dair değerlendirme.</summary>
    public string Degerlendirme { get; init; } = string.Empty;
    /// <summary>Hak edilmediği tahmin edilen kısım.</summary>
    public decimal HakEdilmeyen { get; init; }
}

public sealed class CikisTakipDto
{
    public Guid? TalimatId { get; init; }
    public Guid PersonelId { get; init; }
    public string AdSoyad { get; init; } = string.Empty;
    public string TcKimlikNo { get; init; } = string.Empty;
    public string? Tesis { get; init; }
    public decimal? Maas { get; init; }

    /// <summary>"Talimat" veya "Doğrudan çıkış kaydı".</summary>
    public string Kaynak { get; init; } = string.Empty;
    public DateOnly SonCalismaGunu { get; init; }
    public string? Neden { get; init; }
    public string? TalepEden { get; init; }
    public DateTime? TalepZamani { get; init; }
    public string? Sorumlu { get; init; }

    public CikisTakipDurumu Durum { get; init; }
    public int GecikmeGun { get; init; }
    public DateOnly? CikisKaydiTarihi { get; init; }
    public string? CikisiYapan { get; init; }
    public DateTime? CikisKaydiZamani { get; init; }
    public string? SgkDurumu { get; init; }

    /// <summary>Son çalışma gününün ayı için tahmini hak ediş (maaş × gün / 30).</summary>
    public decimal CikisAyiTahminiHakEdis { get; init; }
    public decimal SonGunSonrasiOdenen { get; init; }
    /// <summary>Hak edilmediği tahmin edilen toplam (geri istenmesi gereken).</summary>
    public decimal TahminiFazlaOdeme { get; init; }
    /// <summary>Son günden sonraki aylar için hâlâ bordroya giren satır sayısı.</summary>
    public int SonrakiBordroSayisi { get; init; }
    public IReadOnlyList<CikisSonrasiOdemeDto> Odemeler { get; init; } = Array.Empty<CikisSonrasiOdemeDto>();
    public string OdemeOzeti => Odemeler.Count == 0 ? "Ödeme yok" : $"{Odemeler.Count} ödeme · {Odemeler.Sum(o => o.Tutar):N2} TL";
}

public sealed class CikisTakipFiltre
{
    public bool YalnizSorunlu { get; set; }
    public string? Tesis { get; set; }
    public string? Metin { get; set; }
    /// <summary>Bu tarihten eski tamamlanmış çıkışlar listelenmez (bekleyenler her zaman listelenir).</summary>
    public DateOnly? Baslangic { get; set; }
    public bool DogrudanCikislariDahilEt { get; set; } = true;
}

public interface ICikisTalimatServisi
{
    Task<CikisTalimati> OlusturAsync(CikisTalimatiOlusturDto dto, string kullanici, CancellationToken ct = default);
    Task IptalEtAsync(Guid talimatId, string neden, string kullanici, CancellationToken ct = default);
    Task<IReadOnlyList<CikisTakipDto>> ListeleAsync(CikisTakipFiltre? filtre = null, CancellationToken ct = default);
    /// <summary>Son günü geçmiş, çıkışı yapılmamış talimatlar için günde bir hatırlatma gönderir; gönderilen sayısını döner.</summary>
    Task<int> GecikenleriHatirlatAsync(DateTime simdi, CancellationToken ct = default);
}
