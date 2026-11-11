// Otomatik üretildi: Personel Yönetim Sistemi (Access) tablolarının birebir karşılığı. Sütun adları Access ile aynıdır.
// Üretici: Access şeması → C# (veri içermez).
namespace PayControl.Domain.Entities.Pys;

/// <summary>Eski sistem tablosu işareti.</summary>
public interface IPysTablo { Guid PysId { get; set; } }

/// <summary>Access tablosu: IsyeriBelgeTur</summary>
public class PysIsyeriBelgeTur : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? TurID { get; set; }
    public string? Grup { get; set; }
    public string? Ad { get; set; }
    public string? Sure { get; set; }
    public int? Sira { get; set; }
    public bool Aktif { get; set; }
}

/// <summary>Access tablosu: IsyeriBelge</summary>
public class PysIsyeriBelge : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? BelgeID { get; set; }
    public int? TurID { get; set; }
    public string? Tesis { get; set; }
    public DateTime? Tarih { get; set; }
    public DateTime? Bitis { get; set; }
    public string? Dosya { get; set; }
    public string? Aciklama { get; set; }
    public bool Iptal { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: ManuelDuzeltme</summary>
public class PysManuelDuzeltme : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public string? Tesis { get; set; }
    public string? TCKN { get; set; }
    public int? Yil { get; set; }
    public int? Ay { get; set; }
    public string? Alan { get; set; }
    public decimal? Deger { get; set; }
    public string? Aciklama { get; set; }
    public DateTime? KayitTarihi { get; set; }
}

/// <summary>Access tablosu: HTBakiyeSabitleme</summary>
public class PysHTBakiyeSabitleme : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ID { get; set; }
    public string? TCKN { get; set; }
    public DateTime? EtkinTarih { get; set; }
    public decimal? Bakiye { get; set; }
    public string? Aciklama { get; set; }
    public DateTime? KayitTarihi { get; set; }
    public bool Iptal { get; set; }
}

/// <summary>Access tablosu: IzinHareket</summary>
public class PysIzinHareket : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ID { get; set; }
    public string? TCKN { get; set; }
    public DateTime? Tarih { get; set; }
    public string? Tur { get; set; }
    public decimal? Gun { get; set; }
    public string? TatilAdi { get; set; }
    public string? Aciklama { get; set; }
    public DateTime? KayitTarihi { get; set; }
    public bool Iptal { get; set; }
}

/// <summary>Access tablosu: GorevIzinKesinti</summary>
public class PysGorevIzinKesinti : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ID { get; set; }
    public string? TCKN { get; set; }
    public int? Yil { get; set; }
    public int? Ay { get; set; }
    public decimal? Kesinti { get; set; }
    public DateTime? KayitTarihi { get; set; }
}

/// <summary>Access tablosu: HasatIzinKesinti</summary>
public class PysHasatIzinKesinti : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ID { get; set; }
    public string? TCKN { get; set; }
    public int? Yil { get; set; }
    public int? Ay { get; set; }
    public decimal? Kesinti { get; set; }
    public DateTime? KayitTarihi { get; set; }
}

/// <summary>Access tablosu: IzinIslemLog</summary>
public class PysIzinIslemLog : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ID { get; set; }
    public string? TCKN { get; set; }
    public DateTime? Zaman { get; set; }
    public string? Islem { get; set; }
    public string? GeriAl { get; set; }
    public string? Durum { get; set; }
    public string? Tesis { get; set; }
    public DateTime? BasTarih { get; set; }
    public DateTime? BitTarih { get; set; }
    public DateTime? GeriAlmaZamani { get; set; }
    public string? Kullanici { get; set; }
}

/// <summary>Access tablosu: Kullanicilar</summary>
public class PysKullanicilar : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ID { get; set; }
    public string? KullaniciAdi { get; set; }
    public string? AdSoyad { get; set; }
    public string? Rol { get; set; }
    public string? SifreOzet { get; set; }
    public string? Tuz { get; set; }
    public bool Aktif { get; set; }
    public DateTime? SonGiris { get; set; }
    public DateTime? Olusturma { get; set; }
    public bool SifreDegistir { get; set; }
    public string? Cinsiyet { get; set; }
    public string? Foto { get; set; }
}

/// <summary>Access tablosu: PersonelIslemLog</summary>
public class PysPersonelIslemLog : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ID { get; set; }
    public DateTime? Zaman { get; set; }
    public string? Kullanici { get; set; }
    public string? TCKN { get; set; }
    public string? AdSoyad { get; set; }
    public string? Tesis { get; set; }
    public string? Islem { get; set; }
    public string? Ayrinti { get; set; }
    public string? Modul { get; set; }
    public int? KayitID { get; set; }
    public int? PersonelID { get; set; }
}

/// <summary>Access tablosu: PersonelMaas</summary>
public class PysPersonelMaas : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public string? TCKN { get; set; }
    public decimal? NetMaas { get; set; }
    public DateTime? Guncelleme { get; set; }
    public string? Kullanici { get; set; }
}

/// <summary>Access tablosu: ProgramAyar</summary>
public class PysProgramAyar : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public string? Anahtar { get; set; }
    public string? Deger { get; set; }
}

/// <summary>Access tablosu: PersonelBelge</summary>
public class PysPersonelBelge : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ID { get; set; }
    public string? TCKN { get; set; }
    public int? BelgeID { get; set; }
    public string? Durum { get; set; }
    public DateTime? Teslim { get; set; }
    public DateTime? Bitis { get; set; }
    public string? Dosya { get; set; }
    public string? Aciklama { get; set; }
    public string? Kullanici { get; set; }
    public DateTime? Zaman { get; set; }
    public DateTime? Istenme { get; set; }
}

/// <summary>Access tablosu: Sirket</summary>
public class PysSirket : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? SirketID { get; set; }
    public string? SirketAdi { get; set; }
    public string? KisaAd { get; set; }
    public string? VergiNo { get; set; }
    public string? LogoYolu { get; set; }
    public bool Aktif { get; set; }
    public DateTime? OlusturmaTarihi { get; set; }
}

/// <summary>Access tablosu: PersonelSirket</summary>
public class PysPersonelSirket : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? KayitID { get; set; }
    public int? PersonelID { get; set; }
    public int? SirketID { get; set; }
    public DateTime? BaslangicTarihi { get; set; }
    public DateTime? BitisTarihi { get; set; }
    public string? Aciklama { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
    public bool Iptal { get; set; }
}

/// <summary>Access tablosu: TesisAdEsleme</summary>
public class PysTesisAdEsleme : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public string? AdNormal { get; set; }
    public int? TesisID { get; set; }
    public string? OrijinalAd { get; set; }
    public string? Kaynak { get; set; }
    public DateTime? Zaman { get; set; }
}

/// <summary>Access tablosu: KullaniciYetki</summary>
public class PysKullaniciYetki : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ID { get; set; }
    public string? KullaniciAdi { get; set; }
    public string? Yetki { get; set; }
    public bool Izin { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: ZimmetMalzeme</summary>
public class PysZimmetMalzeme : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? MalzemeID { get; set; }
    public string? Kod { get; set; }
    public string? MalzemeAdi { get; set; }
    public string? Kategori { get; set; }
    public string? Birim { get; set; }
    public bool BedenVar { get; set; }
    public bool SeriTakip { get; set; }
    public int? YenilemeAy { get; set; }
    public string? StokKodu { get; set; }
    public bool Aktif { get; set; }
    public string? Aciklama { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
    public decimal? BirimBedel { get; set; }
}

/// <summary>Access tablosu: Zimmet</summary>
public class PysZimmet : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ZimmetID { get; set; }
    public int? IslemNo { get; set; }
    public int? PersonelID { get; set; }
    public string? TCKN { get; set; }
    public int? MalzemeID { get; set; }
    public string? Beden { get; set; }
    public string? SeriNo { get; set; }
    public decimal? Miktar { get; set; }
    public DateTime? TeslimTarihi { get; set; }
    public string? TeslimEden { get; set; }
    public string? Durum { get; set; }
    public DateTime? IadeTarihi { get; set; }
    public string? IadeAlan { get; set; }
    public string? IadeAciklama { get; set; }
    public decimal? Bedel { get; set; }
    public DateTime? YenilemeTarihi { get; set; }
    public DateTime? TutanakTarihi { get; set; }
    public string? TutanakDosya { get; set; }
    public string? Aciklama { get; set; }
    public int? TesisID { get; set; }
    public string? Tesis { get; set; }
    public int? SirketID { get; set; }
    public bool Iptal { get; set; }
    public string? IptalNedeni { get; set; }
    public string? IptalEden { get; set; }
    public DateTime? IptalZamani { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
    public decimal? TeslimBedeli { get; set; }
}

/// <summary>Access tablosu: EgitimTur</summary>
public class PysEgitimTur : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? EgitimTurID { get; set; }
    public string? TurAdi { get; set; }
    public string? Kategori { get; set; }
    public int? GecerlilikAy { get; set; }
    public bool Zorunlu { get; set; }
    public int? BelgeTanimID { get; set; }
    public bool Aktif { get; set; }
    public string? Aciklama { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: Egitim</summary>
public class PysEgitim : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? EgitimID { get; set; }
    public int? EgitimTurID { get; set; }
    public DateTime? Tarih { get; set; }
    public decimal? SureSaat { get; set; }
    public string? Egitmen { get; set; }
    public string? Kurum { get; set; }
    public string? Yer { get; set; }
    public int? TesisID { get; set; }
    public string? Tesis { get; set; }
    public string? Aciklama { get; set; }
    public bool Iptal { get; set; }
    public string? IptalNedeni { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: EgitimKatilim</summary>
public class PysEgitimKatilim : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? KatilimID { get; set; }
    public int? EgitimID { get; set; }
    public int? PersonelID { get; set; }
    public string? TCKN { get; set; }
    public string? Sonuc { get; set; }
    public string? SertifikaNo { get; set; }
    public DateTime? GecerlilikBitis { get; set; }
    public string? DosyaYolu { get; set; }
    public bool BelgeAktarildi { get; set; }
    public string? Aciklama { get; set; }
    public bool Iptal { get; set; }
    public string? IptalNedeni { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: AvansKesinti</summary>
public class PysAvansKesinti : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? KayitID { get; set; }
    public int? PersonelID { get; set; }
    public string? TCKN { get; set; }
    public string? Tur { get; set; }
    public DateTime? Tarih { get; set; }
    public decimal? Tutar { get; set; }
    public int? TaksitSayisi { get; set; }
    public DateTime? IlkAy { get; set; }
    public string? Aciklama { get; set; }
    public string? Kaynak { get; set; }
    public string? Durum { get; set; }
    public string? Onaylayan { get; set; }
    public DateTime? OnayZamani { get; set; }
    public int? TesisID { get; set; }
    public string? Tesis { get; set; }
    public int? SirketID { get; set; }
    public bool Iptal { get; set; }
    public string? IptalNedeni { get; set; }
    public string? IptalEden { get; set; }
    public DateTime? IptalZamani { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
    public DateTime? KapanisAy { get; set; }
    public string? KapanisNedeni { get; set; }
    public string? Kapatan { get; set; }
    public DateTime? KapanisZamani { get; set; }
}

/// <summary>Access tablosu: AvansTaksit</summary>
public class PysAvansTaksit : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? TaksitID { get; set; }
    public int? KayitID { get; set; }
    public int? PersonelID { get; set; }
    public string? TCKN { get; set; }
    public DateTime? Ay { get; set; }
    public int? SiraNo { get; set; }
    public decimal? Tutar { get; set; }
}

/// <summary>Access tablosu: FazlaMesai</summary>
public class PysFazlaMesai : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? KayitID { get; set; }
    public int? PersonelID { get; set; }
    public string? TCKN { get; set; }
    public DateTime? Tarih { get; set; }
    public decimal? Saat { get; set; }
    public string? Tur { get; set; }
    public string? Aciklama { get; set; }
    public string? Durum { get; set; }
    public string? Onaylayan { get; set; }
    public DateTime? OnayZamani { get; set; }
    public string? RedNedeni { get; set; }
    public int? TesisID { get; set; }
    public string? Tesis { get; set; }
    public int? SirketID { get; set; }
    public bool Iptal { get; set; }
    public string? IptalNedeni { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: Gorevlendirme</summary>
public class PysGorevlendirme : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? GorevID { get; set; }
    public int? PersonelID { get; set; }
    public string? TCKN { get; set; }
    public int? SirketID { get; set; }
    public string? KaynakTesis { get; set; }
    public string? GidecegiYer { get; set; }
    public int? HedefTesisID { get; set; }
    public string? Gorevi { get; set; }
    public DateTime? Baslangic { get; set; }
    public DateTime? Bitis { get; set; }
    public string? Aciklama { get; set; }
    public bool Iptal { get; set; }
    public string? IptalNedeni { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: Disiplin</summary>
public class PysDisiplin : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? DisiplinID { get; set; }
    public int? PersonelID { get; set; }
    public string? TCKN { get; set; }
    public DateTime? OlayTarihi { get; set; }
    public string? Tur { get; set; }
    public string? Konu { get; set; }
    public string? Aciklama { get; set; }
    public DateTime? SavunmaIstem { get; set; }
    public DateTime? SavunmaSon { get; set; }
    public DateTime? SavunmaAlindi { get; set; }
    public string? SavunmaOzet { get; set; }
    public string? Karar { get; set; }
    public DateTime? KararTarihi { get; set; }
    public string? KararAciklama { get; set; }
    public bool Kapandi { get; set; }
    public string? Dosya { get; set; }
    public int? TesisID { get; set; }
    public string? Tesis { get; set; }
    public int? SirketID { get; set; }
    public bool Iptal { get; set; }
    public string? IptalNedeni { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: ZimmetBakim</summary>
public class PysZimmetBakim : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? BakimID { get; set; }
    public int? ZimmetID { get; set; }
    public int? PersonelID { get; set; }
    public string? TCKN { get; set; }
    public DateTime? Tarih { get; set; }
    public string? Getiren { get; set; }
    public string? Islem { get; set; }
    public string? Degisenler { get; set; }
    public string? Aciklama { get; set; }
    public bool Iptal { get; set; }
    public string? IptalNedeni { get; set; }
    public string? IptalEden { get; set; }
    public DateTime? IptalZamani { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: DonemDevri</summary>
public class PysDonemDevri : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? DevirID { get; set; }
    public DateTime? KesimTarihi { get; set; }
    public string? YedekYolu { get; set; }
    public string? YedekOzet { get; set; }
    public string? CsvYolu { get; set; }
    public int? PersonelSayisi { get; set; }
    public int? DevirSayisi { get; set; }
    public int? SilinenPuantaj { get; set; }
    public int? SilinenHareket { get; set; }
    public string? Durum { get; set; }
    public string? Kullanici { get; set; }
    public string? Bilgisayar { get; set; }
    public DateTime? Zaman { get; set; }
}

/// <summary>Access tablosu: IseGirisKontrol</summary>
public class PysIseGirisKontrol : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public string? TCKN { get; set; }
    public DateTime? SozlesmeTarih { get; set; }
    public DateTime? SgkTarih { get; set; }
    public DateTime? IsgTarih { get; set; }
    public DateTime? KkdTarih { get; set; }
    public DateTime? SaglikTarih { get; set; }
    public DateTime? HazirTarih { get; set; }
    public string? Kullanici { get; set; }
    public DateTime? Zaman { get; set; }
}

/// <summary>Access tablosu: SirketSozlesme</summary>
public class PysSirketSozlesme : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? SozlesmeID { get; set; }
    public int? SirketID { get; set; }
    public string? Ad { get; set; }
    public string? Surum { get; set; }
    public DateTime? Yururluk { get; set; }
    public int? Nusha { get; set; }
    public int? BelgeID { get; set; }
    public string? Dosya { get; set; }
    public string? Durum { get; set; }
    public int? Sira { get; set; }
    public string? Aciklama { get; set; }
    public int? OncekiID { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: TesisSirket</summary>
public class PysTesisSirket : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? KayitID { get; set; }
    public string? TesisAdi { get; set; }
    public int? SirketID { get; set; }
    public DateTime? Baslangic { get; set; }
    public DateTime? Bitis { get; set; }
    public string? Aciklama { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: PersonelSozlesme</summary>
public class PysPersonelSozlesme : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? KayitID { get; set; }
    public string? TCKN { get; set; }
    public int? PersonelID { get; set; }
    public int? SirketID { get; set; }
    public DateTime? ImzaTarihi { get; set; }
    public string? Dosya { get; set; }
    public string? Aciklama { get; set; }
    public bool Iptal { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: IsyeriBelgeKisi</summary>
public class PysIsyeriBelgeKisi : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? KayitID { get; set; }
    public int? BelgeID { get; set; }
    public string? TCKN { get; set; }
    public int? PersonelID { get; set; }
    public string? IslemYapan { get; set; }
    public DateTime? IslemZamani { get; set; }
}

/// <summary>Access tablosu: BelgeTanim</summary>
public class PysBelgeTanim : IPysTablo
{
    public Guid PysId { get; set; } = Guid.NewGuid();
    public int? ID { get; set; }
    public string? Grup { get; set; }
    public string? Ad { get; set; }
    public string? Kosul { get; set; }
    public string? Sure { get; set; }
    public int? Sira { get; set; }
    public bool Aktif { get; set; }
}

public static class PysTablolari
{
    /// <summary>Access tablo adı → sınıf.</summary>
    public static readonly IReadOnlyDictionary<string, Type> Tumu = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
    {
        ["IsyeriBelgeTur"] = typeof(PysIsyeriBelgeTur),
        ["IsyeriBelge"] = typeof(PysIsyeriBelge),
        ["ManuelDuzeltme"] = typeof(PysManuelDuzeltme),
        ["HTBakiyeSabitleme"] = typeof(PysHTBakiyeSabitleme),
        ["IzinHareket"] = typeof(PysIzinHareket),
        ["GorevIzinKesinti"] = typeof(PysGorevIzinKesinti),
        ["HasatIzinKesinti"] = typeof(PysHasatIzinKesinti),
        ["IzinIslemLog"] = typeof(PysIzinIslemLog),
        ["Kullanicilar"] = typeof(PysKullanicilar),
        ["PersonelIslemLog"] = typeof(PysPersonelIslemLog),
        ["PersonelMaas"] = typeof(PysPersonelMaas),
        ["ProgramAyar"] = typeof(PysProgramAyar),
        ["PersonelBelge"] = typeof(PysPersonelBelge),
        ["Sirket"] = typeof(PysSirket),
        ["PersonelSirket"] = typeof(PysPersonelSirket),
        ["TesisAdEsleme"] = typeof(PysTesisAdEsleme),
        ["KullaniciYetki"] = typeof(PysKullaniciYetki),
        ["ZimmetMalzeme"] = typeof(PysZimmetMalzeme),
        ["Zimmet"] = typeof(PysZimmet),
        ["EgitimTur"] = typeof(PysEgitimTur),
        ["Egitim"] = typeof(PysEgitim),
        ["EgitimKatilim"] = typeof(PysEgitimKatilim),
        ["AvansKesinti"] = typeof(PysAvansKesinti),
        ["AvansTaksit"] = typeof(PysAvansTaksit),
        ["FazlaMesai"] = typeof(PysFazlaMesai),
        ["Gorevlendirme"] = typeof(PysGorevlendirme),
        ["Disiplin"] = typeof(PysDisiplin),
        ["ZimmetBakim"] = typeof(PysZimmetBakim),
        ["DonemDevri"] = typeof(PysDonemDevri),
        ["IseGirisKontrol"] = typeof(PysIseGirisKontrol),
        ["SirketSozlesme"] = typeof(PysSirketSozlesme),
        ["TesisSirket"] = typeof(PysTesisSirket),
        ["PersonelSozlesme"] = typeof(PysPersonelSozlesme),
        ["IsyeriBelgeKisi"] = typeof(PysIsyeriBelgeKisi),
        ["BelgeTanim"] = typeof(PysBelgeTanim),
    };
}
