using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Interfaces;

/// <summary>
/// Personel mali hareket defteri. Tüm modüller harekete buradan yazar: benzersiz işlem kimliği,
/// çalışma dönemi / şirket / tesis ataması ve kapalı dönem kontrolü tek noktadadır.
/// </summary>
public interface IMaliHareketServisi
{
    /// <summary>
    /// Hareketi deftere ekler (SaveChanges çağırmaz). Aynı işlem kimliği varsa null döner
    /// veya <paramref name="mukerrerdeHata"/> true ise hata verir.
    /// </summary>
    Task<MaliHareket?> DeftereEkleAsync(MaliHareket hareket, bool mukerrerdeHata = false, CancellationToken ct = default);

    Task<MaliHareket> BordroDisiHakEdisAsync(Guid personelId, MaliIslemTuru tur, DateOnly tarih, int donemYil, int donemAy,
        decimal tutar, string aciklama, string? belge, string kullanici, CancellationToken ct = default);

    Task<MaliHareket> IadeAlAsync(Guid personelId, DateOnly tarih, decimal tutar, BakiyeTuru bakiye,
        string aciklama, string? belge, string kullanici, CancellationToken ct = default);

    Task<MaliHareket> DevredenBakiyeAsync(Guid personelId, bool borcMu, BakiyeTuru bakiye, DateOnly tarih,
        int? donemYil, int? donemAy, decimal tutar, string aciklama, string? belge, string kullanici, CancellationToken ct = default);

    Task<MaliHareket?> SupheliFazlaOdemeKaydetAsync(Guid personelId, int donemYil, int donemAy, decimal tutar,
        string aciklama, string kaynakTuru, string? kaynakReferans, string islemKimligiEki, string kullanici,
        bool kaydet = true, CancellationToken ct = default);

    Task FazlaOdemeKesinlestirAsync(Guid personelId, decimal tutar, string gerekce, string kullanici, CancellationToken ct = default);

    Task FazlaOdemeAciklaAsync(Guid personelId, decimal tutar, MaliIslemTuru hakEdisTuru, int donemYil, int donemAy,
        string gerekce, string? belge, string kullanici, CancellationToken ct = default);

    Task<MaliHareket> TersKayitAsync(Guid hareketId, string neden, string kullanici, CancellationToken ct = default);

    Task<EkstreOzeti> BakiyeAsync(Guid personelId, CancellationToken ct = default);
}
