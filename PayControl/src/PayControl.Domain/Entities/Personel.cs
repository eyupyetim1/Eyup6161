using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

/// <summary>
/// Personel ana kartı. Sistem kimliği kalıcıdır; finansal geçmiş silinmez.
/// </summary>
public class Personel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>TC kimlik numarası (11 hane). Benzersizlik uygulama katmanında doğrulanır.</summary>
    public string TcKimlikNo { get; set; } = string.Empty;

    public string AdSoyad { get; set; } = string.Empty;
    public string? SicilNo { get; set; }
    public string? Sirket { get; set; }
    public string? Isyeri { get; set; }
    public string? Gorevi { get; set; }

    public DateOnly? IlkIseGirisTarihi { get; set; }
    public DateOnly? SonCikisTarihi { get; set; }

    public CalismaDurumu CalismaDurumu { get; set; } = CalismaDurumu.Aktif;

    /// <summary>Güncel kart üzerindeki maaş bilgisi (tarihsel dönem kayıtları ayrı tutulur).</summary>
    public decimal? GuncelMaas { get; set; }
    public MaasTuru GuncelMaasTuru { get; set; } = MaasTuru.Belirsiz;

    public string? BankaAdi { get; set; }
    public string? Iban { get; set; }
    public string? Notlar { get; set; }

    // Personel Yönetim Sistemi (Access) alanları
    public string? Cinsiyet { get; set; }
    public DateOnly? DogumTarihi { get; set; }
    public string? MedeniHal { get; set; }
    public string? Telefon { get; set; }
    /// <summary>Access'teki PersonelID (aktarım eşleşmesi için).</summary>
    public int? EskiSistemId { get; set; }

    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    public DateTime? GuncellemeZamani { get; set; }

    public ICollection<CalismaDonemi> CalismaDonemleri { get; set; } = new List<CalismaDonemi>();
    public ICollection<MaasDonemKaydi> MaasDonemKayitlari { get; set; } = new List<MaasDonemKaydi>();
    public ICollection<IstenCikisKaydi> IstenCikisKayitlari { get; set; } = new List<IstenCikisKaydi>();
    public ICollection<DenetimKaydi> DenetimKayitlari { get; set; } = new List<DenetimKaydi>();
    public ICollection<PersonelBelge> Belgeler { get; set; } = new List<PersonelBelge>();
    public ICollection<MaliHareket> MaliHareketler { get; set; } = new List<MaliHareket>();
    public ICollection<AvansKaydi> Avanslar { get; set; } = new List<AvansKaydi>();
}
