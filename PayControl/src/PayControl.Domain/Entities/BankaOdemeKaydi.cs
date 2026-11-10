using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

/// <summary>
/// Banka ödeme hareketi (V1 iskelet). Hak ediş dönemi ile ödeme tarihi ayrı tutulur.
/// </summary>
public class BankaOdemeKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? PersonelId { get; set; }
    public Personel? Personel { get; set; }

    /// <summary>Ödemenin ait olduğu hak ediş dönemi (yıl).</summary>
    public int? HakEdisDonemYil { get; set; }
    public int? HakEdisDonemAy { get; set; }

    public DateOnly OdemeTarihi { get; set; }
    public decimal Tutar { get; set; }
    public string? Iban { get; set; }
    public string? Aciklama { get; set; }
    public string? ReferansNo { get; set; }

    public Guid? IlgiliMaasDonemKaydiId { get; set; }
    public MaasDonemKaydi? IlgiliMaasDonemKaydi { get; set; }

    public OdemeSiniflandirma Siniflandirma { get; set; } = OdemeSiniflandirma.NormalHakEdisOdemesi;
    public bool EslesmeOnaylandi { get; set; }
    public string? EslesmeNotu { get; set; }

    public OdemeKanali OdemeKanali { get; set; } = OdemeKanali.Banka;

    /// <summary>Mükerrer engeli için benzersiz anahtar (banka referansı veya satır özeti).</summary>
    public string? TekillikAnahtari { get; set; }

    public string? KaynakTuru { get; set; }
    public Guid? IceAktarmaId { get; set; }

    /// <summary>Banka satırındaki ham kimlik/ad (eşleşmeyen hareketler için).</summary>
    public string? TcKimlikNo { get; set; }
    public string? AdSoyad { get; set; }
    public string? KullaniciAdi { get; set; }

    /// <summary>Yanlış girilen ödeme silinmez; gerekçeyle iptal edilir ve mali hareketi ters kayıtla kapanır.</summary>
    public bool IptalEdildi { get; set; }
    public DateTime? IptalZamani { get; set; }
    public string? IptalEden { get; set; }
    public string? IptalNedeni { get; set; }

    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
}
