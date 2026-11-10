namespace PayControl.Domain.Entities;

/// <summary>Personel icra dosyası. Bordrodaki icra kesintileri sıraya göre dosyalara işlenir.</summary>
public class IcraDosyasi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonelId { get; set; }
    public string DosyaNo { get; set; } = string.Empty;
    public string? IcraDairesi { get; set; }
    public string? Alacakli { get; set; }
    public decimal ToplamBorc { get; set; }
    public decimal Kesilen { get; set; }
    /// <summary>Net ücretten kesilecek oran (yasal üst sınır genellikle 1/4).</summary>
    public decimal KesintiOrani { get; set; } = 0.25m;
    public int Sira { get; set; } = 1;
    public DateOnly Baslangic { get; set; }
    public DateOnly? Bitis { get; set; }
    public bool Aktif { get; set; } = true;
    public string? Aciklama { get; set; }
    public string? OlusturanKullanici { get; set; }
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;

    public decimal Kalan => Math.Max(0, ToplamBorc - Kesilen);
    public ICollection<IcraKesintiKaydi> Kesintiler { get; set; } = new List<IcraKesintiKaydi>();
}

public class IcraKesintiKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid IcraDosyasiId { get; set; }
    public IcraDosyasi? IcraDosyasi { get; set; }
    public int DonemYil { get; set; }
    public int DonemAy { get; set; }
    public decimal Tutar { get; set; }
    public Guid? KaynakIceAktarmaId { get; set; }
    public bool IptalEdildi { get; set; }
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
}

/// <summary>Otomatik katılım BES tanımı.</summary>
public class BesTanimi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonelId { get; set; }
    public decimal Oran { get; set; } = 0.03m;
    public DateOnly Baslangic { get; set; }
    public DateOnly? CaymaTarihi { get; set; }
    public bool Aktif { get; set; } = true;
    public string? OlusturanKullanici { get; set; }
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
}
