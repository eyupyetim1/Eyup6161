using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

/// <summary>
/// Belirli bir aya ait maaş/hak ediş kaydı. Maaş değişince önceki dönemler değişmez.
/// </summary>
public class MaasDonemKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonelId { get; set; }
    public Personel? Personel { get; set; }

    public int DonemYil { get; set; }
    public int DonemAy { get; set; }

    public string? Isyeri { get; set; }
    public string? Gorevi { get; set; }

    public decimal? MaasTutar { get; set; }
    public MaasTuru MaasTuru { get; set; } = MaasTuru.Belirsiz;

    public DateOnly? GirisTarihi { get; set; }
    public DateOnly? CikisTarihi { get; set; }

    public decimal? Gun { get; set; }

    /// <summary>Kesintiler sonrası bankaya yatırılması beklenen tutar.</summary>
    public decimal NetIstihkak { get; set; }

    public string? Iban { get; set; }

    public Guid? IceAktarmaId { get; set; }
    public IceAktarmaKaydi? IceAktarma { get; set; }

    public int? KaynakSatirNo { get; set; }

    public Guid? CalismaDonemiId { get; set; }
    public string? Sirket { get; set; }

    /// <summary>Bordro geri alındıysa kayıt silinmez, iptal işaretlenir.</summary>
    public bool IptalEdildi { get; set; }
    public DateTime? IptalZamani { get; set; }
    public string? IptalEden { get; set; }
    public string? IptalNedeni { get; set; }

    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;

    public ICollection<MaasKalemi> Kalemler { get; set; } = new List<MaasKalemi>();
}
