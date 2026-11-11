using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

/// <summary>
/// Personel mali hareket defteri kaydı. Benzersiz işlem kimliği ile çift kayıt engellenir.
/// </summary>
public class MaliHareket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>İş mantığı benzersiz anahtarı (örn. BordroHakEdis:personelId:2026-10).</summary>
    public string IslemKimligi { get; set; } = string.Empty;

    public Guid PersonelId { get; set; }
    public Personel? Personel { get; set; }

    public Guid? CalismaDonemiId { get; set; }
    public CalismaDonemi? CalismaDonemi { get; set; }

    public DateOnly IslemTarihi { get; set; }
    public int? DonemYil { get; set; }
    public int? DonemAy { get; set; }

    public MaliIslemTuru IslemTuru { get; set; }
    public BakiyeTuru BakiyeTuru { get; set; }

    /// <summary>Pozitif: alacak/ödeme artışı; işaret BakiyeTuru ile yorumlanır.</summary>
    public decimal Tutar { get; set; }

    public string Aciklama { get; set; } = string.Empty;
    public string? KaynakTuru { get; set; }
    public string? KaynakReferans { get; set; }
    public string? KaynakBelge { get; set; }
    public string? KullaniciAdi { get; set; }

    /// <summary>Hareketin ait olduğu şirket (çalışma döneminden).</summary>
    public string? Sirket { get; set; }

    /// <summary>Hareketin ait olduğu tesis / işyeri.</summary>
    public string? Isyeri { get; set; }

    /// <summary>Ters kayıt ise iptal ettiği hareket; mahsup/kesinleştirme ise ilişkili hareket.</summary>
    public Guid? IlgiliHareketId { get; set; }

    /// <summary>
    /// true ise bu kayıt <see cref="IlgiliHareketId"/> hareketinin ters kaydıdır: aynı işlem türü,
    /// aynı bakiye, işaret ters. Orijinal hareket silinmez.
    /// </summary>
    public bool TersKayitMi { get; set; }

    public DateTime KayitZamani { get; set; } = DateTime.UtcNow;
}
