using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

/// <summary>Bordro × onaylı liste × gerçekleşen banka ödemesi mutabakat farkı.</summary>
public class MutabakatFarki
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaasDonemKapanisiId { get; set; }
    public MaasDonemKapanisi? MaasDonemKapanisi { get; set; }

    public int DonemYil { get; set; }
    public int DonemAy { get; set; }

    public Guid? PersonelId { get; set; }
    public string? TcKimlikNo { get; set; }
    public string? AdSoyad { get; set; }
    /// <summary>Tesis / işyeri (boş: tüm tesisler).</summary>
    public string? Tesis { get; set; }

    /// <summary>Aynı farkın tekrar mutabakatta güncellenmesi için anahtar.</summary>
    public string Anahtar { get; set; } = string.Empty;

    public MutabakatFarkTuru FarkTuru { get; set; }
    public decimal Beklenen { get; set; }
    public decimal Odenen { get; set; }
    public decimal Fark { get; set; }
    public string Aciklama { get; set; } = string.Empty;

    public FarkDurumu Durum { get; set; } = FarkDurumu.Acik;
    public string? CozumAciklamasi { get; set; }
    public string? Cozen { get; set; }
    public DateTime? CozumZamani { get; set; }

    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    public DateTime? SonGuncellemeZamani { get; set; }
}
