using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

/// <summary>Tüm modüllerden açık işleri tek listede toplar (canlı hesaplanır, saklanmaz).</summary>
public sealed class UyariServisi : IUyariServisi
{
    private readonly IPayControlDbContext _db;

    public UyariServisi(IPayControlDbContext db) => _db = db;

    public async Task<IReadOnlyList<UyariDto>> ListeleAsync(string? tesis = null, CancellationToken ct = default)
    {
        var liste = new List<UyariDto>();
        var personeller = await _db.Personeller.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        string? TesisOf(Guid? id) => id.HasValue && personeller.TryGetValue(id.Value, out var p) ? p.Isyeri : null;
        string Ad(Guid id) => personeller.TryGetValue(id, out var p) ? p.AdSoyad : "?";
        var bugun = DateOnly.FromDateTime(DateTime.Today);

        // Çıkış talimatları: İK uygulamadıysa veya son günden sonra ödeme yapıldıysa
        foreach (var c in await CikisTalimatServisi.HesaplaAsync(_db, new CikisTakipFiltre { YalnizSorunlu = true }, bugun, ct))
        {
            if (c.Durum == CikisTakipDurumu.Gecikti)
                liste.Add(new UyariDto
                {
                    Seviye = UyariSeviyesi.Kritik, Kategori = "Çıkış talimatı",
                    Baslik = $"{c.AdSoyad}: çıkış talimatı {c.GecikmeGun} gündür uygulanmadı",
                    Aciklama = $"Son çalışma günü {c.SonCalismaGunu:dd.MM.yyyy}, talimatı veren {c.TalepEden}" + (c.Sorumlu is null ? "" : $", sorumlu {c.Sorumlu}") +
                               $". Neden: {c.Neden}." + (c.TahminiFazlaOdeme > 0 ? $" Şimdiden {c.TahminiFazlaOdeme:N2} TL hak edilmemiş ödeme yapılmış." : ""),
                    Tesis = c.Tesis, PersonelId = c.PersonelId, Eylem = "Çıkış Takibi → Çıkışı kaydet"
                });
            if (c.TahminiFazlaOdeme > 0 || c.SonrakiBordroSayisi > 0)
                liste.Add(new UyariDto
                {
                    Seviye = UyariSeviyesi.Kritik, Kategori = "Çıkış sonrası ödeme",
                    Baslik = $"{c.AdSoyad}: son çalışma gününden sonra {c.TahminiFazlaOdeme:N2} TL hak edilmemiş ödeme",
                    Aciklama = $"Son gün {c.SonCalismaGunu:dd.MM.yyyy}. {c.OdemeOzeti}." +
                               (c.SonrakiBordroSayisi > 0 ? $" Sonraki {c.SonrakiBordroSayisi} ay bordrosunda hâlâ ücret var." : ""),
                    Tesis = c.Tesis, PersonelId = c.PersonelId, Eylem = "Çıkış Takibi → ödemeleri inceleyin; fazla ödemeyi hesap ekstresinden kesinleştirin"
                });
        }

        // Açık mutabakat farkları
        foreach (var f in await _db.MutabakatFarklari.AsNoTracking().Where(f => f.Durum == FarkDurumu.Acik).ToListAsync(ct))
            liste.Add(new UyariDto
            {
                Seviye = f.FarkTuru is MutabakatFarkTuru.FazlaOdeme or MutabakatFarkTuru.BordrosuzOdeme or MutabakatFarkTuru.CikisSonrasiOdeme or MutabakatFarkTuru.EslesmeyenBankaHareketi
                    ? UyariSeviyesi.Kritik : UyariSeviyesi.Uyari,
                Kategori = "Mutabakat",
                Baslik = $"{f.AdSoyad ?? "Eşleşmeyen kayıt"}: {FarkAdi(f.FarkTuru)} {Math.Abs(f.Fark):N2} TL",
                Aciklama = f.Aciklama,
                Tesis = f.Tesis ?? TesisOf(f.PersonelId),
                PersonelId = f.PersonelId,
                Donem = $"{f.DonemYil}-{f.DonemAy:D2}",
                Eylem = "Kapanış ve Mutabakat → Mutabakat farkları"
            });

        // SGK durumu belirsiz çıkışlar
        foreach (var c in await _db.IstenCikisKayitlari.AsNoTracking().Where(c => c.SgkCikisDurumu == SgkCikisDurumu.Belirsiz).ToListAsync(ct))
            liste.Add(new UyariDto
            {
                Seviye = c.IstenCikisTarihi < bugun.AddDays(-10) ? UyariSeviyesi.Kritik : UyariSeviyesi.Uyari,
                Kategori = "SGK",
                Baslik = $"{Ad(c.PersonelId)}: SGK çıkış bildirgesi durumu belirsiz",
                Aciklama = $"Çıkış {c.IstenCikisTarihi:dd.MM.yyyy}. İşten ayrılış bildirgesi yasal süre içinde verilmelidir.",
                Tesis = TesisOf(c.PersonelId),
                PersonelId = c.PersonelId,
                Eylem = "Personel kartı → Çıkış ve SGK kontrolü"
            });

        // Gecikmiş avans mahsupları
        var gecmisDonem = new DateOnly(bugun.Year, bugun.Month, 1).AddMonths(-1);
        foreach (var a in await _db.Avanslar.AsNoTracking().Include(a => a.Mahsuplar).Where(a => a.KalanBorc > 0).ToListAsync(ct))
        {
            var geciken = a.Mahsuplar.Where(m => m.GerceklesenTutar is null && new DateOnly(m.DonemYil, m.DonemAy, 1) <= gecmisDonem).ToList();
            if (geciken.Count > 0)
                liste.Add(new UyariDto
                {
                    Seviye = UyariSeviyesi.Uyari,
                    Kategori = "Avans",
                    Baslik = $"{Ad(a.PersonelId)}: {geciken.Count} avans taksiti mahsup edilmedi",
                    Aciklama = $"Kalan borç {a.KalanBorc:N2}. Geciken dönemler: {string.Join(", ", geciken.Select(m => $"{m.DonemYil}-{m.DonemAy:D2}"))}.",
                    Tesis = TesisOf(a.PersonelId),
                    PersonelId = a.PersonelId,
                    Eylem = "Bordroya avans kesintisi ekleyin veya personel kartından mahsup girin"
                });
        }

        // İnceleme bekleyen fazla ödemeler
        var supheli = (await _db.MaliHareketler.AsNoTracking().Where(h => h.BakiyeTuru == BakiyeTuru.SupheliFazlaOdeme).ToListAsync(ct))
            .GroupBy(h => h.PersonelId).Select(g => (g.Key, Tutar: g.Sum(MaliHareketKurallari.Etki))).Where(x => x.Tutar > 0.005m);
        foreach (var (pid, tutar) in supheli)
            liste.Add(new UyariDto
            {
                Seviye = UyariSeviyesi.Kritik,
                Kategori = "Fazla ödeme",
                Baslik = $"{Ad(pid)}: {tutar:N2} TL inceleme bekleyen fazla ödeme",
                Aciklama = "Belgeli hak ediş olarak açıklayın veya kesinleştirip tahsilat planı oluşturun.",
                Tesis = TesisOf(pid),
                PersonelId = pid,
                Eylem = "Personel kartı → Hesap ekstresi → Manuel işlem"
            });

        // Kesinleşmiş ama planlanmamış geri ödeme
        var kesin = (await _db.MaliHareketler.AsNoTracking().Where(h => h.BakiyeTuru == BakiyeTuru.KesinlesmisGeriOdeme).ToListAsync(ct))
            .GroupBy(h => h.PersonelId).Select(g => (g.Key, Tutar: g.Sum(MaliHareketKurallari.Etki))).Where(x => x.Tutar > 0.005m).ToList();
        var planlar = await _db.TahsilatPlanlari.AsNoTracking().Include(p => p.Taksitler).ToListAsync(ct);
        foreach (var (pid, tutar) in kesin)
        {
            var planli = planlar.Where(p => p.PersonelId == pid).Sum(p => p.KalanBorc);
            if (tutar - planli > 0.005m)
                liste.Add(new UyariDto
                {
                    Seviye = UyariSeviyesi.Uyari,
                    Kategori = "Tahsilat",
                    Baslik = $"{Ad(pid)}: {tutar - planli:N2} TL geri ödeme borcu için tahsilat planı yok",
                    Tesis = TesisOf(pid),
                    PersonelId = pid,
                    Eylem = "Tahsilat planı oluşturun veya iade girin"
                });
        }
        foreach (var p in planlar)
            foreach (var t in p.Taksitler.Where(t => t.Gerceklesen is null && new DateOnly(t.DonemYil, t.DonemAy, 1) <= gecmisDonem))
                liste.Add(new UyariDto
                {
                    Seviye = UyariSeviyesi.Uyari,
                    Kategori = "Tahsilat",
                    Baslik = $"{Ad(p.PersonelId)}: {t.DonemYil}-{t.DonemAy:D2} tahsilat taksiti ({t.Planlanan:N2}) gerçekleşmedi",
                    Tesis = TesisOf(p.PersonelId),
                    PersonelId = p.PersonelId,
                    Donem = $"{t.DonemYil}-{t.DonemAy:D2}"
                });

        // Kapanmamış geçmiş dönemler
        var bordroDonemleri = await _db.MaasDonemKayitlari.AsNoTracking().Select(m => new { m.DonemYil, m.DonemAy }).Distinct().ToListAsync(ct);
        var kapali = (await _db.MaasDonemKapanislari.AsNoTracking().Where(d => d.Durum == MaasDonemDurumu.Kapatildi)
            .Select(d => new { d.DonemYil, d.DonemAy }).ToListAsync(ct)).ToHashSet();
        foreach (var d in bordroDonemleri.Where(d => !kapali.Contains(d) && new DateOnly(d.DonemYil, d.DonemAy, 1) < gecmisDonem))
            liste.Add(new UyariDto
            {
                Seviye = UyariSeviyesi.Uyari,
                Kategori = "Kapanış",
                Baslik = $"{d.DonemYil}-{d.DonemAy:D2} maaş dönemi kapatılmadı",
                Donem = $"{d.DonemYil}-{d.DonemAy:D2}",
                Eylem = "Kapanış ve Mutabakat"
            });

        // İcra dosyaları
        foreach (var d in await _db.IcraDosyalari.AsNoTracking().Where(d => d.Aktif).ToListAsync(ct))
            if (d.ToplamBorc - d.Kesilen <= d.ToplamBorc * 0.1m)
                liste.Add(new UyariDto
                {
                    Seviye = UyariSeviyesi.Bilgi,
                    Kategori = "İcra",
                    Baslik = $"{Ad(d.PersonelId)}: {d.DosyaNo} icra dosyası bitmek üzere (kalan {d.ToplamBorc - d.Kesilen:N2})",
                    Aciklama = "Son kesintide fazla kesinti yapılmamasına dikkat edin.",
                    Tesis = TesisOf(d.PersonelId),
                    PersonelId = d.PersonelId
                });

        // Yedek ve güvenlik
        var sonYedek = await _db.YedekKayitlari.AsNoTracking().Where(y => y.Basarili).OrderByDescending(y => y.Zaman).FirstOrDefaultAsync(ct);
        if (sonYedek is null || sonYedek.Zaman < DateTime.UtcNow.AddDays(-7))
            liste.Add(new UyariDto
            {
                Seviye = UyariSeviyesi.Kritik,
                Kategori = "Yedek",
                Baslik = sonYedek is null ? "Hiç yedek alınmamış" : $"Son yedek {sonYedek.Zaman.ToLocalTime():dd.MM.yyyy} tarihinde",
                Aciklama = "Ayarlar → Yedekleme ekranından yedek alın ve otomatik yedeği açık tutun.",
                Eylem = "Ayarlar → Yedekleme"
            });
        var sonHata = await _db.YedekKayitlari.AsNoTracking().OrderByDescending(y => y.Zaman).FirstOrDefaultAsync(ct);
        if (sonHata is { Basarili: false })
            liste.Add(new UyariDto { Seviye = UyariSeviyesi.Kritik, Kategori = "Yedek", Baslik = "Son yedekleme başarısız", Aciklama = sonHata.Hata ?? "" });
        if (await _db.Kullanicilar.AnyAsync(k => k.Aktif && k.SifreDegistirmeGerekli, ct))
            liste.Add(new UyariDto
            {
                Seviye = UyariSeviyesi.Uyari,
                Kategori = "Güvenlik",
                Baslik = "Şifresini henüz değiştirmemiş kullanıcı var",
                Eylem = "Ayarlar → Kullanıcılar"
            });
        var hataliBildirim = await _db.BildirimKayitlari.AsNoTracking().CountAsync(b => b.Durum == BildirimDurumu.Hata && b.OlusturmaZamani > DateTime.UtcNow.AddDays(-7), ct);
        if (hataliBildirim > 0)
            liste.Add(new UyariDto
            {
                Seviye = UyariSeviyesi.Uyari,
                Kategori = "Bildirim",
                Baslik = $"Son 7 günde {hataliBildirim} e-posta/WhatsApp gönderimi başarısız",
                Eylem = "Bildirimler → Gönderim geçmişi"
            });

        return liste
            .Where(u => tesis is null || u.Tesis is null || string.Equals(u.Tesis, tesis, StringComparison.CurrentCultureIgnoreCase))
            .OrderBy(u => u.Seviye).ThenBy(u => u.Kategori).ThenBy(u => u.Baslik)
            .ToList();
    }

    private static string FarkAdi(MutabakatFarkTuru t) => t switch
    {
        MutabakatFarkTuru.EksikOdeme => "eksik ödeme",
        MutabakatFarkTuru.FazlaOdeme => "fazla ödeme",
        MutabakatFarkTuru.BordrosuzOdeme => "bordrosuz ödeme",
        MutabakatFarkTuru.OnayliListeDisiOdeme => "onaylı liste dışı ödeme",
        MutabakatFarkTuru.OnayliListedeOdenmedi => "listede var, ödenmedi",
        MutabakatFarkTuru.CikisSonrasiOdeme => "çıkış sonrası ödeme",
        _ => "eşleşmeyen banka hareketi"
    };
}

public sealed class DenetimServisi : IDenetimServisi
{
    private readonly IPayControlDbContext _db;

    public DenetimServisi(IPayControlDbContext db) => _db = db;

    private IQueryable<DenetimKaydi> Sorgu(DenetimFiltre f)
    {
        var q = _db.DenetimKayitlari.AsNoTracking().AsQueryable();
        if (f.Baslangic.HasValue) q = q.Where(d => d.IslemZamani >= f.Baslangic.Value.ToUniversalTime());
        if (f.Bitis.HasValue) q = q.Where(d => d.IslemZamani < f.Bitis.Value.AddDays(1).ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(f.Kullanici)) q = q.Where(d => d.KullaniciAdi == f.Kullanici);
        if (!string.IsNullOrWhiteSpace(f.Islem)) q = q.Where(d => d.Islem == f.Islem);
        if (!string.IsNullOrWhiteSpace(f.VarlikTuru)) q = q.Where(d => d.VarlikTuru == f.VarlikTuru);
        if (f.PersonelId.HasValue) q = q.Where(d => d.PersonelId == f.PersonelId);
        if (!string.IsNullOrWhiteSpace(f.Metin))
        {
            var m = f.Metin.Trim();
            q = q.Where(d => (d.YeniDeger != null && d.YeniDeger.Contains(m)) || (d.EskiDeger != null && d.EskiDeger.Contains(m)) ||
                             (d.DegisiklikNedeni != null && d.DegisiklikNedeni.Contains(m)) || (d.AlanAdi != null && d.AlanAdi.Contains(m)));
        }
        return q;
    }

    public async Task<DenetimSonucu> AraAsync(DenetimFiltre filtre, CancellationToken ct = default)
    {
        var q = Sorgu(filtre);
        var boyut = Math.Clamp(filtre.SayfaBoyutu, 10, 2000);
        return new DenetimSonucu
        {
            Toplam = await q.CountAsync(ct),
            Kayitlar = await q.Include(d => d.Personel).OrderByDescending(d => d.IslemZamani).Skip((Math.Max(1, filtre.Sayfa) - 1) * boyut).Take(boyut).ToListAsync(ct),
            Kullanicilar = await _db.DenetimKayitlari.Where(d => d.KullaniciAdi != null).Select(d => d.KullaniciAdi!).Distinct().OrderBy(x => x).ToListAsync(ct),
            Islemler = await _db.DenetimKayitlari.Select(d => d.Islem).Distinct().OrderBy(x => x).ToListAsync(ct)
        };
    }

    public async Task<byte[]> ExcelAsync(DenetimFiltre filtre, CancellationToken ct = default)
    {
        var kayitlar = await Sorgu(filtre).OrderByDescending(d => d.IslemZamani).Take(100_000).ToListAsync(ct);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Denetim İzi");
        string[] b = ["Zaman", "Kullanıcı", "İşlem", "Varlık", "Alan", "Eski değer", "Yeni değer", "Neden", "Personel", "Varlık kimliği"];
        for (var i = 0; i < b.Length; i++) ws.Cell(1, i + 1).Value = b[i];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var d in kayitlar)
        {
            ws.Cell(r, 1).Value = d.IslemZamani.ToLocalTime();
            ws.Cell(r, 1).Style.DateFormat.Format = "dd.MM.yyyy HH:mm:ss";
            ws.Cell(r, 2).Value = d.KullaniciAdi;
            ws.Cell(r, 3).Value = d.Islem;
            ws.Cell(r, 4).Value = d.VarlikTuru;
            ws.Cell(r, 5).Value = d.AlanAdi;
            ws.Cell(r, 6).Value = d.EskiDeger;
            ws.Cell(r, 7).Value = d.YeniDeger;
            ws.Cell(r, 8).Value = d.DegisiklikNedeni;
            ws.Cell(r, 9).Value = d.PersonelId?.ToString();
            ws.Cell(r, 10).Value = d.VarlikId?.ToString();
            r++;
        }
        ws.Columns(1, 5).AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
