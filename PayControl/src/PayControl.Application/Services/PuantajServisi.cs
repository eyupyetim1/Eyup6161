using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class PuantajServisi : IPuantajServisi
{
    private static readonly string[] GunKisa = ["Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz"];
    private readonly IPayControlDbContext _db;
    private readonly IYetkiServisi _yetki;

    public PuantajServisi(IPayControlDbContext db, IYetkiServisi yetki)
    {
        _db = db;
        _yetki = yetki;
    }

    /// <summary>Excel Ayarlar sayfasındaki resmi tatiller (ilk açılışta yüklenir).</summary>
    public static readonly IReadOnlyList<(DateOnly Tarih, string Ad, decimal Hak)> VarsayilanTatiller =
    [
        (new DateOnly(2026, 1, 1), "Yılbaşı", 1m), (new DateOnly(2026, 4, 23), "Ulusal Egemenlik ve Çocuk Bayramı", 1m), (new DateOnly(2026, 5, 1), "Emek ve Dayanışma Günü", 1m),
        (new DateOnly(2026, 5, 19), "Atatürk'ü Anma, Gençlik ve Spor Bayramı", 1m),
        (new DateOnly(2026, 7, 15), "Demokrasi ve Milli Birlik Günü", 1m),
        (new DateOnly(2026, 8, 30), "Zafer Bayramı", 1m), (new DateOnly(2026, 10, 29), "Cumhuriyet Bayramı", 1m),
        (new DateOnly(2027, 1, 1), "Yılbaşı", 1m), (new DateOnly(2027, 3, 8), "Arife (Ramazan bayramı)", 0.5m),
        (new DateOnly(2027, 3, 9), "Ramazan Bayramı", 1m), (new DateOnly(2027, 3, 10), "Ramazan Bayramı", 1m), (new DateOnly(2027, 3, 11), "Ramazan Bayramı", 1m),
        (new DateOnly(2027, 4, 23), "Ulusal Egemenlik ve Çocuk Bayramı", 1m), (new DateOnly(2027, 5, 1), "Emek ve Dayanışma Günü", 1m),
        (new DateOnly(2027, 5, 15), "Kurban Bayramı Arifesi", 0.5m), (new DateOnly(2027, 5, 16), "Kurban Bayramı", 1m), (new DateOnly(2027, 5, 17), "Kurban Bayramı", 1m),
        (new DateOnly(2027, 5, 18), "Kurban Bayramı", 1m), (new DateOnly(2027, 5, 19), "Atatürk'ü Anma, Gençlik ve Spor Bayramı", 1m),
        (new DateOnly(2027, 7, 15), "Demokrasi ve Millî Birlik Günü", 1m), (new DateOnly(2027, 8, 30), "Zafer Bayramı", 1m), (new DateOnly(2027, 10, 29), "Cumhuriyet Bayramı", 1m)
    ];

    public static async Task VarsayilanTatilleriYukleAsync(IPayControlDbContext db, CancellationToken ct = default)
    {
        if (await db.ResmiTatiller.AnyAsync(ct)) return;
        foreach (var (t, a, h) in VarsayilanTatiller) db.ResmiTatiller.Add(new ResmiTatil { Tarih = t, Ad = a, Hak = h });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> TesislerAsync(CancellationToken ct = default)
    {
        var tanimli = await _db.Tesisler.AsNoTracking().Where(t => t.Aktif).Select(t => t.Ad).ToListAsync(ct);
        var puantaj = await _db.PuantajGunleri.AsNoTracking().Select(p => p.TesisAdi).Distinct().ToListAsync(ct);
        var kart = await _db.Personeller.AsNoTracking().Where(p => p.Isyeri != null).Select(p => p.Isyeri!).Distinct().ToListAsync(ct);
        return tanimli.Concat(puantaj).Concat(kart).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(s => s, StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), true)).ToList();
    }

    public async Task<PuantajTablosu> TabloAsync(string tesis, int yil, int ay, CancellationToken ct = default)
    {
        var gunSayisi = DateTime.DaysInMonth(yil, ay);
        var bas = new DateOnly(yil, ay, 1);
        var son = bas.AddDays(gunSayisi - 1);
        var tatiller = (await _db.ResmiTatiller.AsNoTracking().Where(t => t.Tarih >= bas && t.Tarih <= son).ToListAsync(ct)).ToDictionary(t => t.Tarih);
        var gunler = Enumerable.Range(1, gunSayisi).Select(g =>
        {
            var d = new DateOnly(yil, ay, g);
            var hg = PuantajHesaplayici.HaftaGunu(d);
            tatiller.TryGetValue(d, out var rt);
            return new PuantajGunBasligi(g, d, GunKisa[hg - 1], hg >= 6, rt?.Ad, rt?.Hak ?? 0);
        }).ToList();

        var kayitlar = await _db.PuantajGunleri.AsNoTracking().Where(p => p.TesisAdi == tesis && p.Yil == yil && p.Ay == ay).ToListAsync(ct);
        var notlar = await _db.PuantajNotlari.AsNoTracking().Where(p => p.TesisAdi == tesis && p.Yil == yil && p.Ay == ay).ToListAsync(ct);
        var personeller = await _db.Personeller.AsNoTracking().Include(p => p.CalismaDonemleri).ToListAsync(ct);
        var ayarlar = await _db.PersonelIzinAyarlari.AsNoTracking().ToDictionaryAsync(a => a.TcKimlikNo, ct);

        // Tabloya girenler: o ay bu tesiste çalışan (kart tesisi + dönem çakışması) veya bu tesiste puantajı olanlar
        var tcler = kayitlar.Select(k => k.TcKimlikNo).ToHashSet();
        var liste = personeller.Where(p =>
                tcler.Contains(p.TcKimlikNo) ||
                (string.Equals(p.Isyeri, tesis, StringComparison.CurrentCultureIgnoreCase) &&
                 (p.CalismaDonemleri.Count == 0 ? p.CalismaDurumu == CalismaDurumu.Aktif
                     : p.CalismaDonemleri.Any(d => d.IseGirisTarihi <= son && (d.IstenCikisTarihi == null || d.IstenCikisTarihi >= bas)))))
            .ToList();
        var satirlar = new List<PuantajSatiri>();
        foreach (var p in liste)
        {
            var kodlar = kayitlar.Where(k => k.TcKimlikNo == p.TcKimlikNo).GroupBy(k => k.Gun).ToDictionary(g => g.Key, g => PuantajHesaplayici.Standart(g.Last().Kod) ?? g.Last().Kod);
            satirlar.Add(new PuantajSatiri
            {
                PersonelId = p.Id, TcKimlikNo = p.TcKimlikNo, AdSoyad = p.AdSoyad, Unvan = p.Gorevi, Aktif = p.CalismaDurumu == CalismaDurumu.Aktif,
                Kodlar = kodlar,
                Notlar = notlar.Where(n => n.TcKimlikNo == p.TcKimlikNo).GroupBy(n => n.Gun).ToDictionary(g => g.Key, g => g.Last().NotMetni),
                Ozet = PuantajHesaplayici.Hesapla(yil, ay, kodlar)
            });
        }
        // Kartı olmayan TC'lerin puantajı (eski kayıt) da gösterilir
        foreach (var tc in tcler.Where(t => satirlar.All(s => s.TcKimlikNo != t)))
        {
            var kodlar = kayitlar.Where(k => k.TcKimlikNo == tc).GroupBy(k => k.Gun).ToDictionary(g => g.Key, g => PuantajHesaplayici.Standart(g.Last().Kod) ?? g.Last().Kod);
            satirlar.Add(new PuantajSatiri { TcKimlikNo = tc, AdSoyad = "(kartı yok) " + tc, Kodlar = kodlar, Ozet = PuantajHesaplayici.Hesapla(yil, ay, kodlar) });
        }
        _ = ayarlar; // kişiye özel hafta tatili izin/bakiye modülünde kullanılır; aylık özet VBA'daki gibi pazar esaslıdır
        return new PuantajTablosu
        {
            Tesis = tesis, Yil = yil, Ay = ay, Gunler = gunler,
            Satirlar = satirlar.OrderBy(s => s.AdSoyad, StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), true)).ToList()
        };
    }

    public async Task<int> KaydetAsync(string tesis, int yil, int ay, IReadOnlyList<PuantajDegisikligi> degisiklikler, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Puantaj girişi", ct);
        if (string.IsNullOrWhiteSpace(tesis)) throw new InvalidOperationException("Tesis seçilmelidir.");
        var gunSayisi = DateTime.DaysInMonth(yil, ay);
        var mevcut = await _db.PuantajGunleri.Where(p => p.TesisAdi == tesis && p.Yil == yil && p.Ay == ay).ToListAsync(ct);
        var personelId = await _db.Personeller.AsNoTracking().ToDictionaryAsync(p => p.TcKimlikNo, p => p.Id, ct);
        var n = 0;
        foreach (var d in degisiklikler)
        {
            if (d.Gun < 1 || d.Gun > gunSayisi) throw new InvalidOperationException($"Gün {d.Gun} bu ayda yok.");
            string? kod = null;
            if (!string.IsNullOrWhiteSpace(d.Kod))
                kod = PuantajHesaplayici.Standart(d.Kod) ?? throw new InvalidOperationException(
                    $"'{d.Kod}' geçerli bir puantaj kodu değil. Geçerli kodlar: {string.Join(", ", PuantajHesaplayici.Kodlar.Select(k => k.Kod))}");
            var k = mevcut.FirstOrDefault(x => x.TcKimlikNo == d.TcKimlikNo && x.Gun == d.Gun);
            if (kod is null)
            {
                if (k is null) continue;
                _db.PuantajGunleri.Remove(k);
                mevcut.Remove(k);
            }
            else if (k is null)
            {
                k = new PuantajGunu
                {
                    TesisAdi = tesis, Yil = yil, Ay = ay, Gun = d.Gun, TcKimlikNo = d.TcKimlikNo, Kod = kod, Kullanici = kullanici,
                    PersonelId = personelId.TryGetValue(d.TcKimlikNo, out var pid) ? pid : null
                };
                _db.PuantajGunleri.Add(k);
                mevcut.Add(k);
            }
            else if (k.Kod != kod)
            {
                k.Kod = kod;
                k.Kullanici = kullanici;
                k.Zaman = DateTime.UtcNow;
            }
            else continue;
            n++;
        }
        if (n > 0)
        {
            _db.DenetimKayitlari.Add(new DenetimKaydi
            {
                VarlikTuru = nameof(PuantajGunu), Islem = "PuantajKaydi", KullaniciAdi = kullanici,
                YeniDeger = $"{tesis} {yil}-{ay:D2}: {n} hücre", DegisiklikNedeni = "Puantaj girişi"
            });
            await _db.SaveChangesAsync(ct);
        }
        return n;
    }

    public async Task NotKaydetAsync(string tesis, string tc, int yil, int ay, int gun, string? not, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Puantaj notu", ct);
        var mevcut = await _db.PuantajNotlari.Where(n => n.TesisAdi == tesis && n.TcKimlikNo == tc && n.Yil == yil && n.Ay == ay && n.Gun == gun).ToListAsync(ct);
        _db.PuantajNotlari.RemoveRange(mevcut);
        if (!string.IsNullOrWhiteSpace(not))
            _db.PuantajNotlari.Add(new PuantajNotu { TesisAdi = tesis, TcKimlikNo = tc, Yil = yil, Ay = ay, Gun = gun, NotMetni = not.Trim() });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ResmiTatil>> ResmiTatillerAsync(int? yil = null, CancellationToken ct = default)
    {
        var q = _db.ResmiTatiller.AsNoTracking();
        if (yil is { } y) q = q.Where(t => t.Tarih >= new DateOnly(y, 1, 1) && t.Tarih <= new DateOnly(y, 12, 31));
        return await q.OrderBy(t => t.Tarih).ToListAsync(ct);
    }

    public async Task<ResmiTatil> ResmiTatilKaydetAsync(DateOnly tarih, string ad, decimal hak, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Resmi tatil", ct);
        if (string.IsNullOrWhiteSpace(ad)) throw new InvalidOperationException("Tatil adı zorunludur.");
        if (hak is not (0.5m or 1m)) throw new InvalidOperationException("Hak 1 (tam gün) veya 0,5 (arife) olmalıdır.");
        var t = await _db.ResmiTatiller.FirstOrDefaultAsync(x => x.Tarih == tarih, ct);
        if (t is null) _db.ResmiTatiller.Add(t = new ResmiTatil { Tarih = tarih });
        t.Ad = ad.Trim();
        t.Hak = hak;
        await _db.SaveChangesAsync(ct);
        return t;
    }

    public async Task ResmiTatilSilAsync(Guid id, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Resmi tatil", ct);
        var t = await _db.ResmiTatiller.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return;
        _db.ResmiTatiller.Remove(t);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PersonelNakil>> NakillerAsync(string? tesis = null, CancellationToken ct = default)
    {
        var q = _db.PersonelNakilleri.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(tesis)) q = q.Where(n => n.EskiTesis == tesis || n.YeniTesis == tesis);
        return (await q.ToListAsync(ct)).OrderByDescending(n => n.NakilTarihi).ThenByDescending(n => n.Zaman).ToList();
    }

    public async Task<PersonelNakil> NakilYapAsync(Guid personelId, string yeniTesis, DateOnly tarih, string? aciklama, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Personel nakli", ct);
        var p = await _db.Personeller.FirstOrDefaultAsync(x => x.Id == personelId, ct) ?? throw new InvalidOperationException("Personel bulunamadı.");
        yeniTesis = IsimKurallari.Duzelt(yeniTesis) ?? "";
        if (yeniTesis.Length == 0) throw new InvalidOperationException("Yeni tesis seçilmelidir.");
        if (string.Equals(p.Isyeri, yeniTesis, StringComparison.CurrentCultureIgnoreCase)) throw new InvalidOperationException("Personel zaten bu tesiste.");
        var n = new PersonelNakil
        {
            PersonelId = p.Id, TcKimlikNo = p.TcKimlikNo, AdSoyad = p.AdSoyad, EskiTesis = p.Isyeri, YeniTesis = yeniTesis,
            NakilTarihi = tarih, Aciklama = aciklama, Kullanici = kullanici
        };
        _db.PersonelNakilleri.Add(n);
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = p.Id, VarlikTuru = nameof(Personel), VarlikId = p.Id, Islem = "Nakil", AlanAdi = nameof(Personel.Isyeri),
            EskiDeger = p.Isyeri, YeniDeger = yeniTesis, DegisiklikNedeni = aciklama ?? $"Nakil {tarih:dd.MM.yyyy}", KullaniciAdi = kullanici
        });
        p.Isyeri = yeniTesis;
        p.GuncellemeZamani = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return n;
    }
}
