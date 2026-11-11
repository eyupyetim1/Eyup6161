using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Entities.Pys;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

/// <summary>
/// Eski Personel Yönetim Sistemi'nin (Access) puantaj dışındaki tüm tabloları: görüntüleme, arama, ekleme, düzeltme, silme.
/// Sütunlar Access'teki adlarla birebir tutulur; özel iş akışları (bakiye motoru, belge hatırlatma vb.) ayrı modüllerde taşınır.
/// </summary>
public sealed class EskiVeriServisi : IEskiVeriServisi
{
    public const string PersonelSutunu = "Personel";
    private readonly IPayControlDbContext _db;
    private readonly IYetkiServisi _yetki;

    public EskiVeriServisi(IPayControlDbContext db, IYetkiServisi yetki)
    {
        _db = db;
        _yetki = yetki;
    }

    public IReadOnlyList<PysTabloTanimi> Tanimlar { get; } =
    [
        new("IzinHareket", "İzin hareketleri", "İzin ve bakiye", "Yıllık izin / resmi tatil kullanımları ve elle eklenen hareketler"),
        new("HTBakiyeSabitleme", "HT bakiye sabitleme", "İzin ve bakiye", "Belirli tarihte kesinleştirilen hafta tatili bakiyeleri"),
        new("GorevIzinKesinti", "Görev izni kesintileri", "İzin ve bakiye", "Aylık görev izni kesintileri"),
        new("HasatIzinKesinti", "Hasat izni kesintileri", "İzin ve bakiye", "Aylık hasat izni kesintileri"),
        new("ManuelDuzeltme", "Manuel düzeltmeler", "İzin ve bakiye", "Aylık özet alanlarına yönetici düzeltmeleri"),
        new("IzinIslemLog", "İzin işlem geçmişi", "İzin ve bakiye", "Toplu izin girişleri ve geri alma kayıtları"),
        new("PersonelBelge", "Personel belgeleri", "Personel dosyaları", "Kimde hangi belge teslim edildi, bekleniyor, eksik"),
        new("BelgeTanim", "Belge tanımları", "Personel dosyaları", "Özlük dosyasında istenen belgeler ve süreleri"),
        new("SirketSozlesme", "Şirket sözleşmeleri", "Sözleşmeler", "Şirket sözleşme şablonları ve sürümleri"),
        new("PersonelSozlesme", "Personel sözleşmeleri", "Sözleşmeler", "Personelin imzaladığı sözleşmeler"),
        new("IsyeriBelge", "İşyeri İSG belgeleri", "İSG", "Tesis bazında İSG belgeleri ve bitiş tarihleri"),
        new("IsyeriBelgeTur", "İSG belge türleri", "İSG", "Belge türleri ve yenileme süreleri"),
        new("IsyeriBelgeKisi", "İSG belgesi kişileri", "İSG", "Belgeye bağlı personeller"),
        new("Zimmet", "Zimmetler", "İK işlemleri", "Personele verilen KKD, demirbaş, ekipman"),
        new("ZimmetMalzeme", "Zimmet malzemeleri", "İK işlemleri", "Malzeme kataloğu"),
        new("ZimmetBakim", "Zimmet bakımları", "İK işlemleri", "Zimmet bakım / değişim kayıtları"),
        new("Egitim", "Eğitimler", "İK işlemleri", "Verilen eğitimler"),
        new("EgitimKatilim", "Eğitim katılımları", "İK işlemleri", "Katılımcı, sonuç, sertifika"),
        new("EgitimTur", "Eğitim türleri", "İK işlemleri", "Eğitim türleri ve geçerlilik süreleri"),
        new("AvansKesinti", "Avans ve kesintiler", "İK işlemleri", "Avans / kesinti kayıtları ve onay durumu"),
        new("AvansTaksit", "Avans taksitleri", "İK işlemleri", "Aylara dağıtılmış taksitler"),
        new("FazlaMesai", "Fazla mesai", "İK işlemleri", "Fazla mesai kayıtları ve onay durumu"),
        new("Disiplin", "Disiplin", "İK işlemleri", "Disiplin olayları, savunma ve karar"),
        new("Gorevlendirme", "Görevlendirme", "İK işlemleri", "Başka yere görevlendirmeler"),
        new("IseGirisKontrol", "İşe giriş kontrolü", "İK işlemleri", "Sözleşme, SGK, İSG, KKD, sağlık adımları"),
        new("PersonelMaas", "Personel maaşları", "İK işlemleri", "Net maaş bilgisi"),
        new("Sirket", "Şirketler", "Şirketler", "Şirket tanımları"),
        new("PersonelSirket", "Personel - şirket", "Şirketler", "Personelin bağlı olduğu şirket ve dönemler"),
        new("TesisSirket", "Tesis - şirket", "Şirketler", "Tesislerin bağlı olduğu şirketler"),
        new("TesisAdEsleme", "Tesis adı eşleme", "Şirketler", "Farklı yazılan tesis adlarının eşleşmesi"),
        new("Kullanicilar", "Eski kullanıcılar", "Sistem", "Eski programın kullanıcıları (şifreler aktarılmaz)"),
        new("KullaniciYetki", "Eski kullanıcı yetkileri", "Sistem", "Modül bazlı yetkiler"),
        new("ProgramAyar", "Program ayarları", "Sistem", "Eski programın ayarları"),
        new("PersonelIslemLog", "Personel işlem günlüğü", "Sistem", "Personel ekleme / güncelleme / nakil günlüğü"),
        new("DonemDevri", "Dönem devirleri", "Sistem", "Yedekle ve yeni döneme başla kayıtları"),
    ];

    private static readonly Dictionary<string, string> Basliklar = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TCKN"] = "TC kimlik no", ["AdSoyad"] = "Ad soyad", ["Aciklama"] = "Açıklama", ["KayitTarihi"] = "Kayıt tarihi", ["Iptal"] = "İptal",
        ["IptalNedeni"] = "İptal nedeni", ["IptalEden"] = "İptal eden", ["IptalZamani"] = "İptal zamanı", ["IslemYapan"] = "İşlemi yapan",
        ["IslemZamani"] = "İşlem zamanı", ["Tur"] = "Tür", ["Gun"] = "Gün", ["TatilAdi"] = "Tatil adı", ["EtkinTarih"] = "Etkin tarih",
        ["Kesinti"] = "Kesinti", ["Yil"] = "Yıl", ["Ay"] = "Ay", ["Islem"] = "İşlem", ["GeriAl"] = "Geri al", ["BasTarih"] = "Başlangıç",
        ["BitTarih"] = "Bitiş", ["GeriAlmaZamani"] = "Geri alma zamanı", ["Kullanici"] = "Kullanıcı", ["Grup"] = "Grup", ["Ad"] = "Ad",
        ["Kosul"] = "Koşul", ["Sure"] = "Süre", ["Sira"] = "Sıra", ["Teslim"] = "Teslim", ["Bitis"] = "Bitiş", ["Dosya"] = "Dosya",
        ["Zaman"] = "Zaman", ["Istenme"] = "İstenme", ["BelgeID"] = "Belge no", ["SirketID"] = "Şirket no", ["Surum"] = "Sürüm",
        ["Yururluk"] = "Yürürlük", ["Nusha"] = "Nüsha", ["OncekiID"] = "Önceki no", ["ImzaTarihi"] = "İmza tarihi", ["TurID"] = "Tür no",
        ["Tarih"] = "Tarih", ["MalzemeAdi"] = "Malzeme", ["Kategori"] = "Kategori", ["Birim"] = "Birim", ["BedenVar"] = "Beden var",
        ["SeriTakip"] = "Seri takip", ["YenilemeAy"] = "Yenileme (ay)", ["StokKodu"] = "Stok kodu", ["BirimBedel"] = "Birim bedel",
        ["IslemNo"] = "İşlem no", ["MalzemeID"] = "Malzeme no", ["Beden"] = "Beden", ["SeriNo"] = "Seri no", ["Miktar"] = "Miktar",
        ["TeslimTarihi"] = "Teslim tarihi", ["TeslimEden"] = "Teslim eden", ["IadeTarihi"] = "İade tarihi", ["IadeAlan"] = "İade alan",
        ["IadeAciklama"] = "İade açıklaması", ["Bedel"] = "Bedel", ["YenilemeTarihi"] = "Yenileme tarihi", ["TutanakTarihi"] = "Tutanak tarihi",
        ["TutanakDosya"] = "Tutanak dosyası", ["TesisID"] = "Tesis no", ["TeslimBedeli"] = "Teslim bedeli", ["Getiren"] = "Getiren",
        ["Degisenler"] = "Değişenler", ["TurAdi"] = "Tür adı", ["GecerlilikAy"] = "Geçerlilik (ay)", ["Zorunlu"] = "Zorunlu",
        ["BelgeTanimID"] = "Belge tanımı no", ["SureSaat"] = "Süre (saat)", ["Egitmen"] = "Eğitmen", ["Kurum"] = "Kurum", ["Yer"] = "Yer",
        ["Sonuc"] = "Sonuç", ["SertifikaNo"] = "Sertifika no", ["GecerlilikBitis"] = "Geçerlilik bitişi", ["DosyaYolu"] = "Dosya yolu",
        ["BelgeAktarildi"] = "Belge aktarıldı", ["Tutar"] = "Tutar", ["TaksitSayisi"] = "Taksit sayısı", ["IlkAy"] = "İlk ay",
        ["Kaynak"] = "Kaynak", ["Durum"] = "Durum", ["Onaylayan"] = "Onaylayan", ["OnayZamani"] = "Onay zamanı", ["KapanisAy"] = "Kapanış ayı",
        ["KapanisNedeni"] = "Kapanış nedeni", ["Kapatan"] = "Kapatan", ["KapanisZamani"] = "Kapanış zamanı", ["SiraNo"] = "Sıra no",
        ["Saat"] = "Saat", ["RedNedeni"] = "Red nedeni", ["KaynakTesis"] = "Kaynak tesis", ["GidecegiYer"] = "Gideceği yer",
        ["HedefTesisID"] = "Hedef tesis no", ["Gorevi"] = "Görevi", ["Baslangic"] = "Başlangıç", ["OlayTarihi"] = "Olay tarihi",
        ["Konu"] = "Konu", ["SavunmaIstem"] = "Savunma istemi", ["SavunmaSon"] = "Savunma son günü", ["SavunmaAlindi"] = "Savunma alındı",
        ["SavunmaOzet"] = "Savunma özeti", ["Karar"] = "Karar", ["KararTarihi"] = "Karar tarihi", ["KararAciklama"] = "Karar açıklaması",
        ["Kapandi"] = "Kapandı", ["SozlesmeTarih"] = "Sözleşme", ["SgkTarih"] = "SGK girişi", ["IsgTarih"] = "İSG eğitimi",
        ["KkdTarih"] = "KKD teslimi", ["SaglikTarih"] = "Sağlık raporu", ["HazirTarih"] = "Hazır", ["NetMaas"] = "Net maaş",
        ["Guncelleme"] = "Güncelleme", ["SirketAdi"] = "Şirket adı", ["KisaAd"] = "Kısa ad", ["VergiNo"] = "Vergi no", ["LogoYolu"] = "Logo",
        ["Aktif"] = "Aktif", ["OlusturmaTarihi"] = "Oluşturma tarihi", ["KayitID"] = "Kayıt no", ["PersonelID"] = "Personel no",
        ["BaslangicTarihi"] = "Başlangıç", ["BitisTarihi"] = "Bitiş", ["TesisAdi"] = "Tesis", ["AdNormal"] = "Normalleştirilmiş ad",
        ["OrijinalAd"] = "Orijinal ad", ["KullaniciAdi"] = "Kullanıcı adı", ["Rol"] = "Rol", ["SonGiris"] = "Son giriş", ["Olusturma"] = "Oluşturma",
        ["SifreDegistir"] = "Şifre değiştir", ["Cinsiyet"] = "Cinsiyet", ["Yetki"] = "Yetki", ["Izin"] = "İzin", ["Anahtar"] = "Anahtar",
        ["Deger"] = "Değer", ["Ayrinti"] = "Ayrıntı", ["Modul"] = "Modül", ["KesimTarihi"] = "Kesim tarihi", ["YedekYolu"] = "Yedek yolu",
        ["YedekOzet"] = "Yedek özeti", ["CsvYolu"] = "CSV yolu", ["PersonelSayisi"] = "Personel sayısı", ["DevirSayisi"] = "Devir sayısı",
        ["SilinenPuantaj"] = "Silinen puantaj", ["SilinenHareket"] = "Silinen hareket", ["Bilgisayar"] = "Bilgisayar", ["Bakiye"] = "Bakiye",
        ["Alan"] = "Alan", ["Tesis"] = "Tesis", ["EgitimID"] = "Eğitim no", ["EgitimTurID"] = "Eğitim türü no", ["ZimmetID"] = "Zimmet no",
        ["Gizli"] = "Gizli"
    };

    /// <summary>Ekranda gösterilmeyen sütunlar (şifre özetleri).</summary>
    private static readonly HashSet<string> Gizli = new(StringComparer.OrdinalIgnoreCase) { "SifreOzet", "Tuz" };

    public string SutunBasligi(string sutun) => sutun == PersonelSutunu ? "Personel" :
        Basliklar.TryGetValue(sutun, out var b) ? b : Regex.Replace(sutun, "(?<=[a-zçğıöşü])(?=[A-ZÇĞİÖŞÜ])", " ");

    private static Type Tur(string tablo) => PysTablolari.Tumu.TryGetValue(tablo, out var t) ? t : throw new InvalidOperationException($"Bilinmeyen tablo: {tablo}");

    public async Task<IReadOnlyDictionary<string, int>> SatirSayilariAsync(CancellationToken ct = default)
    {
        var d = new Dictionary<string, int>();
        foreach (var t in Tanimlar) d[t.Tablo] = await _db.PysSorgu(Tur(t.Tablo)).CountAsync(ct);
        return d;
    }

    public async Task<DataTable> TabloAsync(string tablo, CancellationToken ct = default)
    {
        var tur = Tur(tablo);
        var ozellikler = tur.GetProperties().Where(p => p.Name != nameof(IPysTablo.PysId) && !Gizli.Contains(p.Name)).ToList();
        var dt = new DataTable(tablo);
        dt.Columns.Add(nameof(IPysTablo.PysId), typeof(Guid));
        var personelVar = ozellikler.Any(p => p.Name is "TCKN" or "PersonelID");
        if (personelVar) dt.Columns.Add(PersonelSutunu, typeof(string)).ReadOnly = true;
        foreach (var p in ozellikler)
            dt.Columns.Add(p.Name, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType).AllowDBNull = true;

        var adlar = personelVar ? await _db.Personeller.AsNoTracking().Select(p => new { p.TcKimlikNo, p.EskiSistemId, p.AdSoyad }).ToListAsync(ct) : null;
        var tcAd = adlar?.GroupBy(a => a.TcKimlikNo).ToDictionary(g => g.Key, g => g.First().AdSoyad);
        var idAd = adlar?.Where(a => a.EskiSistemId.HasValue).GroupBy(a => a.EskiSistemId!.Value).ToDictionary(g => g.Key, g => g.First().AdSoyad);

        foreach (var k in await _db.PysSorgu(tur).AsNoTracking().ToListAsync(ct))
        {
            var r = dt.NewRow();
            r[nameof(IPysTablo.PysId)] = ((IPysTablo)k).PysId;
            foreach (var p in ozellikler) r[p.Name] = p.GetValue(k) ?? DBNull.Value;
            if (personelVar)
            {
                var tc = ozellikler.FirstOrDefault(p => p.Name == "TCKN")?.GetValue(k) as string;
                var pid = ozellikler.FirstOrDefault(p => p.Name == "PersonelID")?.GetValue(k) as int?;
                string? ad = null;
                if (tc is not null) tcAd!.TryGetValue(tc, out ad);
                if (ad is null && pid is { } i) idAd!.TryGetValue(i, out ad);
                if (ad is null && ozellikler.Any(p => p.Name == "AdSoyad")) ad = ozellikler.First(p => p.Name == "AdSoyad").GetValue(k) as string;
                dt.Columns[PersonelSutunu]!.ReadOnly = false;
                r[PersonelSutunu] = (object?)ad ?? DBNull.Value;
                dt.Columns[PersonelSutunu]!.ReadOnly = true;
            }
            dt.Rows.Add(r);
        }
        dt.AcceptChanges();
        return dt;
    }

    public async Task<int> KaydetAsync(string tablo, DataTable dt, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Kayıt düzenleme", ct);
        var tur = Tur(tablo);
        var ozellikler = tur.GetProperties().Where(p => p.Name != nameof(IPysTablo.PysId) && dt.Columns.Contains(p.Name)).ToList();
        var mevcut = (await _db.PysSorgu(tur).ToListAsync(ct)).Cast<IPysTablo>().ToDictionary(k => k.PysId);
        var n = 0;
        foreach (DataRow r in dt.Rows)
        {
            switch (r.RowState)
            {
                case DataRowState.Deleted:
                    var silId = (Guid)r[nameof(IPysTablo.PysId), DataRowVersion.Original];
                    if (mevcut.TryGetValue(silId, out var sil)) { _db.PysSil(sil); n++; }
                    break;
                case DataRowState.Added:
                case DataRowState.Modified:
                    object kayit;
                    if (r.RowState == DataRowState.Modified && r[nameof(IPysTablo.PysId)] is Guid id && mevcut.TryGetValue(id, out var m)) kayit = m;
                    else { kayit = Activator.CreateInstance(tur)!; _db.PysEkle(kayit); }
                    foreach (var p in ozellikler)
                    {
                        var v = r[p.Name];
                        var hedef = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                        object? deger = v is DBNull ? (p.PropertyType == typeof(bool) ? false : null) : Convert.ChangeType(v, hedef, CultureInfo.CurrentCulture);
                        if (deger is string s) deger = p.Name.Contains("AdSoyad") ? IsimKurallari.Duzelt(s) : s.Trim();
                        p.SetValue(kayit, deger);
                    }
                    n++;
                    break;
            }
        }
        if (n > 0)
        {
            _db.DenetimKayitlari.Add(new DenetimKaydi
            {
                VarlikTuru = "Pys_" + tablo, Islem = "Duzenleme", KullaniciAdi = kullanici,
                YeniDeger = $"{Tanimlar.FirstOrDefault(t => t.Tablo == tablo)?.Baslik ?? tablo}: {n} satır"
            });
            await _db.SaveChangesAsync(ct);
        }
        return n;
    }

    /// <summary>Access'ten aktarım: tablonun tüm satırları CSV'deki satırlarla değiştirilir.</summary>
    public static async Task<int> CsvdenAktarAsync(IPayControlDbContext db, string tablo, List<Dictionary<string, string>> satirlar, CancellationToken ct)
    {
        var tur = Tur(tablo);
        foreach (var eski in await db.PysSorgu(tur).ToListAsync(ct)) db.PysSil(eski);
        var ozellikler = tur.GetProperties().Where(p => p.Name != nameof(IPysTablo.PysId)).ToList();
        foreach (var r in satirlar)
        {
            var k = Activator.CreateInstance(tur)!;
            foreach (var p in ozellikler)
            {
                if (!r.TryGetValue(p.Name, out var s)) continue;
                var hedef = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                object? v = null;
                if (hedef == typeof(string)) v = string.IsNullOrEmpty(s) ? null : (p.Name.Contains("AdSoyad") ? IsimKurallari.Duzelt(s) : s);
                else if (hedef == typeof(int)) v = int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var di) ? (int)di : null;
                else if (hedef == typeof(decimal)) v = decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
                else if (hedef == typeof(DateTime)) v = DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;
                else if (hedef == typeof(bool)) v = s is "1" or "True" or "true" or "-1";
                if (v is null && p.PropertyType == typeof(bool)) v = false;
                p.SetValue(k, v);
            }
            db.PysEkle(k);
        }
        return satirlar.Count;
    }
}
