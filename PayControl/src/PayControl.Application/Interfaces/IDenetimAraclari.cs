using PayControl.Domain.Entities;

namespace PayControl.Application.Interfaces;

// ---------------------------------------------------------------- Tesis

public interface ITesisServisi
{
    Task<IReadOnlyList<TesisOzetDto>> ListeleAsync(CancellationToken ct = default);
    Task<Tesis> KaydetAsync(TesisDto dto, string kullanici, CancellationToken ct = default);
    /// <summary>Personel kartlarında geçen ama tanımı olmayan işyerleri için tesis kaydı açar.</summary>
    Task<int> EksikleriOlusturAsync(string kullanici, CancellationToken ct = default);
    Task TesisOnaylaAsync(Guid donemId, string tesis, string? not, string kullanici, CancellationToken ct = default);
    Task<IReadOnlyList<DonemTesisDurumuDto>> DonemTesisDurumuAsync(int yil, int ay, CancellationToken ct = default);
}

public sealed class TesisDto
{
    public Guid? Id { get; set; }
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string? Sirket { get; set; }
    public string? Adres { get; set; }
    public string? SorumluAdSoyad { get; set; }
    public string? SorumluEposta { get; set; }
    public string? SorumluTelefon { get; set; }
    public bool Aktif { get; set; } = true;
}

public sealed class TesisOzetDto
{
    public Tesis Tesis { get; init; } = new();
    public int AktifPersonel { get; init; }
    public int ToplamPersonel { get; init; }
    public decimal AktifMaasToplami { get; init; }
}

public sealed class DonemTesisDurumuDto
{
    public string Tesis { get; init; } = string.Empty;
    public int BordroSatiri { get; init; }
    public decimal BordroNet { get; init; }
    public decimal Odenen { get; init; }
    public decimal Fark => Odenen - BordroNet;
    public int KritikUyari { get; init; }
    public int AcikFark { get; init; }
    public bool GecerliListeVar { get; init; }
    public bool Onaylandi { get; init; }
    public string? Onaylayan { get; init; }
    public DateTime? OnayZamani { get; init; }
}

// ---------------------------------------------------------------- İcra / BES / tahsilat

public interface IKesintiServisi
{
    Task<IcraDosyasi> IcraDosyasiKaydetAsync(Guid personelId, string dosyaNo, string? icraDairesi, string? alacakli, decimal toplamBorc,
        decimal kesintiOrani, int sira, DateOnly baslangic, string kullanici, CancellationToken ct = default);
    Task IcraDosyasiKapatAsync(Guid id, string neden, string kullanici, CancellationToken ct = default);
    Task<IReadOnlyList<IcraDosyasi>> IcraDosyalariAsync(Guid? personelId = null, CancellationToken ct = default);

    Task<BesTanimi> BesTanimlaAsync(Guid personelId, decimal oran, DateOnly baslangic, string kullanici, CancellationToken ct = default);
    Task BesCaymaAsync(Guid personelId, DateOnly caymaTarihi, string kullanici, CancellationToken ct = default);
    Task<IReadOnlyList<BesTanimi>> BesTanimlariAsync(Guid? personelId = null, CancellationToken ct = default);

    /// <summary>Bordrodaki icra kesintisini aktif dosyalara (sıraya göre) işler. SaveChanges çağırmaz.</summary>
    Task BordroIcraIsleAsync(Guid personelId, int yil, int ay, decimal tutar, Guid iceAktarmaId, CancellationToken ct = default);
    Task BordroIcraGeriAlAsync(Guid iceAktarmaId, CancellationToken ct = default);

    /// <summary>Kesinleşmiş fazla ödeme için bordrodan taksitli tahsilat planı.</summary>
    Task<TahsilatPlani> TahsilatPlaniOlusturAsync(Guid personelId, decimal tutar, int taksit, int basYil, int basAy, string aciklama,
        string kullanici, CancellationToken ct = default);
    Task<IReadOnlyList<TahsilatPlani>> TahsilatPlanlariAsync(Guid? personelId = null, CancellationToken ct = default);
    /// <summary>Bordrodaki fazla ödeme kesintisini taksitlere işler ve geri ödeme borcundan düşer. SaveChanges çağırmaz.</summary>
    Task<IReadOnlyList<string>> BordroTahsilatIsleAsync(Guid personelId, int yil, int ay, decimal tutar, Guid iceAktarmaId, string? dosyaAdi,
        string kullanici, CancellationToken ct = default);
    Task BordroTahsilatGeriAlAsync(Guid iceAktarmaId, string kullanici, CancellationToken ct = default);
}

// ---------------------------------------------------------------- Belge

public interface IBelgeServisi
{
    Task<Belge> EkleAsync(string varlikTuru, Guid? varlikId, Guid? personelId, string kaynakDosya, string baslik, string? aciklama,
        string kullanici, CancellationToken ct = default);
    Task<IReadOnlyList<Belge>> ListeleAsync(string? varlikTuru = null, Guid? varlikId = null, Guid? personelId = null, CancellationToken ct = default);
    /// <summary>Belgenin depodaki yolunu döner; bütünlük (SHA-256) doğrulanır.</summary>
    Task<string> DosyaYoluAsync(Guid id, CancellationToken ct = default);
    Task GecersizKilAsync(Guid id, string neden, string kullanici, CancellationToken ct = default);
}

// ---------------------------------------------------------------- Uyarı merkezi ve denetim

public enum UyariSeviyesi { Kritik = 0, Uyari = 1, Bilgi = 2 }

public sealed class UyariDto
{
    public UyariSeviyesi Seviye { get; init; }
    public string Kategori { get; init; } = string.Empty;
    public string Baslik { get; init; } = string.Empty;
    public string Aciklama { get; init; } = string.Empty;
    public string? Tesis { get; init; }
    public Guid? PersonelId { get; init; }
    public string? Donem { get; init; }
    public string? Eylem { get; init; }
}

public interface IUyariServisi
{
    Task<IReadOnlyList<UyariDto>> ListeleAsync(string? tesis = null, CancellationToken ct = default);
}

public sealed class DenetimFiltre
{
    public DateTime? Baslangic { get; set; }
    public DateTime? Bitis { get; set; }
    public string? Kullanici { get; set; }
    public string? Islem { get; set; }
    public string? VarlikTuru { get; set; }
    public Guid? PersonelId { get; set; }
    public string? Metin { get; set; }
    public int Sayfa { get; set; } = 1;
    public int SayfaBoyutu { get; set; } = 200;
}

public sealed class DenetimSonucu
{
    public int Toplam { get; init; }
    public IReadOnlyList<DenetimKaydi> Kayitlar { get; init; } = Array.Empty<DenetimKaydi>();
    public IReadOnlyList<string> Kullanicilar { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Islemler { get; init; } = Array.Empty<string>();
}

public interface IDenetimServisi
{
    Task<DenetimSonucu> AraAsync(DenetimFiltre filtre, CancellationToken ct = default);
    Task<byte[]> ExcelAsync(DenetimFiltre filtre, CancellationToken ct = default);
}
