using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

public class OdemeKontrolSatiri
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OdemeKontrolOturumuId { get; set; }
    public OdemeKontrolOturumu? OdemeKontrolOturumu { get; set; }

    public Guid? PersonelId { get; set; }
    public Personel? Personel { get; set; }

    public string? TcKimlikNo { get; set; }
    public string? AdSoyad { get; set; }
    public string? BordroIban { get; set; }
    public string? BankaIban { get; set; }
    public string? KartIban { get; set; }

    public decimal? BordroNetIstihkak { get; set; }
    public decimal? BankaOdemeTutari { get; set; }

    public OdemeKontrolSonucu Sonuc { get; set; } = OdemeKontrolSonucu.IncelemeGerekiyor;
    public string Gerekce { get; set; } = string.Empty;
    public bool KritikMi { get; set; }

    /// <summary>Kimlik/ödeme sahibi eşleşmesi belirsizse true — istisna onayı engellenir.</summary>
    public bool EslesmeBelirsiz { get; set; }

    public bool Onaylandi { get; set; }
    public bool IstisnaOnayi { get; set; }
    public string? IstisnaGerekcesi { get; set; }
    public string? IstisnaOnaylayan { get; set; }
    public DateTime? IstisnaOnayZamani { get; set; }

    public bool OnayliListeyeAlinabilir =>
        !KritikMi || (IstisnaOnayi && !EslesmeBelirsiz) || Sonuc == OdemeKontrolSonucu.Uygun;
}
