using PayControl.Domain.Enums;

namespace PayControl.Application.Dtos;

public sealed class IstenCikisOlusturDto
{
    public Guid PersonelId { get; set; }
    public DateOnly FiiliSonCalismaTarihi { get; set; }
    public DateOnly IstenCikisTarihi { get; set; }
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

    /// <summary>
    /// SGK durumu varsayılan olarak Belirsiz kalır. Verildi yalnızca ayrı onay ile set edilir.
    /// </summary>
    public SgkCikisDurumu SgkCikisDurumu { get; set; } = SgkCikisDurumu.Belirsiz;
    public DateOnly? SgkBildirgeTarihi { get; set; }
    public string? SgkBelgeYolu { get; set; }
    public bool SgkKullaniciOnayi { get; set; }
}
