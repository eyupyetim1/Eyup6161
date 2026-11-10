namespace PayControl.Domain.Entities;

public enum CikisTalimatDurumu { Bekliyor = 0, Tamamlandi = 1, IptalEdildi = 2 }

/// <summary>
/// Yöneticinin "bu personelin çıkışını yap" talimatı. İK çıkış kaydını girene kadar açık kalır;
/// planlanan son günden sonra yapılan ödemeler takip listesinde, ödeme öncesi kontrolde ve uyarı merkezinde görünür.
/// </summary>
public class CikisTalimati
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonelId { get; set; }
    public Personel? Personel { get; set; }

    /// <summary>Personelin çalışacağı son gün; bu günden sonrası için ücret beklenmez.</summary>
    public DateOnly SonCalismaGunu { get; set; }
    public string Neden { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    /// <summary>Çıkışı yapacak İK kişisi (bildirim alıcısı adı / kullanıcı adı).</summary>
    public string? Sorumlu { get; set; }
    public Guid? SorumluAliciId { get; set; }

    public CikisTalimatDurumu Durum { get; set; } = CikisTalimatDurumu.Bekliyor;
    public Guid? IstenCikisKaydiId { get; set; }
    public DateTime? TamamlanmaZamani { get; set; }
    public string? TamamlayanKullanici { get; set; }
    public string? IptalNedeni { get; set; }

    public string TalepEden { get; set; } = string.Empty;
    public DateTime TalepZamani { get; set; } = DateTime.UtcNow;
    public DateTime? SonHatirlatma { get; set; }
    public int HatirlatmaSayisi { get; set; }
}
