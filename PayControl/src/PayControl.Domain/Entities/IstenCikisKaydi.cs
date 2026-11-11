using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

/// <summary>
/// Manuel oluşturulan işten çıkış kaydı.
/// Çalışma ilişkisinin sona ermesi ile SGK bildirgesi ayrı takip edilir.
/// </summary>
public class IstenCikisKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonelId { get; set; }
    public Personel? Personel { get; set; }

    public Guid? CalismaDonemiId { get; set; }
    public CalismaDonemi? CalismaDonemi { get; set; }

    public DateOnly FiiliSonCalismaTarihi { get; set; }
    public DateOnly IstenCikisTarihi { get; set; }

    public SgkCikisDurumu SgkCikisDurumu { get; set; } = SgkCikisDurumu.Belirsiz;
    public DateOnly? SgkBildirgeTarihi { get; set; }
    public string? SgkOnaylayanKullanici { get; set; }
    public string? SgkBelgeYolu { get; set; }

    public string? CikisNedeni { get; set; }

    public int? SonBordroDonemYil { get; set; }
    public int? SonBordroDonemAy { get; set; }

    public SonMaasOdemeDurumu SonMaasOdemeDurumu { get; set; } = SonMaasOdemeDurumu.Belirsiz;

    public decimal? KalanAvans { get; set; }
    public decimal? IzinUcreti { get; set; }
    public decimal? KidemTazminati { get; set; }
    public decimal? IhbarTazminati { get; set; }

    public string? Aciklama { get; set; }
    public string? BelgeYolu { get; set; }

    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    public string? OlusturanKullanici { get; set; }
}
