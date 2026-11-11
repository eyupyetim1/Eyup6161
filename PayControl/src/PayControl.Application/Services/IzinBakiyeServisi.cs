using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Entities.Pys;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

/// <summary>İzin kartındaki elle girilen kayıt (kesin bakiye, hareket, kesinti). Anahtar VBA ile aynı: S|id, H|id, G|yil|ay, A|yil|ay.</summary>
public sealed record IzinManuelKayit(string Anahtar, DateOnly Tarih, string Tur, string Aciklama, string Deger, bool Iptal, DateTime? KayitTarihi);

public sealed record IzinIslemKaydi(int Id, DateTime? Zaman, string Islem, string Durum, string? Kullanici)
{
    public bool GeriAlinabilir => Durum == "Yapıldı";
}

public sealed record YilOzetiSatiri(string Kod, string Aciklama, decimal Gun, string Tur);

public interface IIzinBakiyeServisi
{
    Task<HaftaTatiliPolitikasi> PolitikaAsync(CancellationToken ct = default);
    Task PolitikaKaydetAsync(HaftaTatiliPolitikasi politika, string kullanici, CancellationToken ct = default);
    Task<IReadOnlyList<IzinBakiyeSonucu>> TumuAsync(string? tesis = null, bool pasifDahil = false, DateOnly? bugun = null, CancellationToken ct = default);
    Task<IzinBakiyeSonucu?> KisiAsync(string tc, DateOnly? bugun = null, CancellationToken ct = default);
    Task<IReadOnlyList<IzinManuelKayit>> ManuelKayitlarAsync(string tc, CancellationToken ct = default);
    Task<IReadOnlyList<YilOzetiSatiri>> YilOzetiAsync(string tc, int yil, CancellationToken ct = default);
    Task<IReadOnlyList<IzinIslemKaydi>> IslemListesiAsync(string tc, int enFazla = 200, CancellationToken ct = default);

    Task KesinBakiyeKaydetAsync(string tc, DateOnly tarih, decimal bakiye, string? aciklama, string kullanici, CancellationToken ct = default);
    Task TopluKesinBakiyeAsync(IReadOnlyCollection<string> tcler, DateOnly tarih, decimal bakiye, string aciklama, string kullanici, CancellationToken ct = default);
    Task HareketKaydetAsync(string tc, DateOnly tarih, string tur, decimal gun, string? tatilAdi, string? aciklama, string kullanici, CancellationToken ct = default);
    Task KesintiKaydetAsync(string tc, string tur, int yil, int ay, decimal gun, string kullanici, CancellationToken ct = default);
    Task RtDahilKaydetAsync(string tc, bool dahil, string kullanici, CancellationToken ct = default);
    Task HaftaGunuKaydetAsync(string tc, string? gun, string kullanici, CancellationToken ct = default);
    Task TopluHaftaGunuAsync(IReadOnlyCollection<string> tcler, string? gun, string kullanici, CancellationToken ct = default);
    Task ManuelKaldirAsync(string tc, string anahtar, string metin, string kullanici, CancellationToken ct = default);
    Task ManuelGeriYukleAsync(string tc, string anahtar, string metin, string kullanici, CancellationToken ct = default);
    Task DogumTarihiKaydetAsync(string tc, DateOnly? dogum, string kullanici, CancellationToken ct = default);
    Task GeriAlAsync(int logId, string kullanici, CancellationToken ct = default);

    Task<decimal> IzinGunuSayAsync(DateOnly d1, DateOnly d2, string? tc, CancellationToken ct = default);
    Task<IzinBakiyeHesaplayici.IzinBitisSonucu?> IzinBitisHesaplaAsync(DateOnly bas, int isGunu, CancellationToken ct = default);
    Task<IReadOnlyList<DateOnly>> PuantajYIGunleriAsync(string tc, DateOnly d1, DateOnly d2, CancellationToken ct = default);
    /// <summary>İzin kartından yıllık izin kullanımı; aynı günler puantajda Yİ ise çift düşüm engellenir (VBA V2.2.1).</summary>
    Task YillikIzinKullanimiKaydetAsync(string tc, DateOnly bas, DateOnly? bit, decimal gun, string? aciklama, string kullanici, CancellationToken ct = default);
    Task<IReadOnlyList<CiftDusumSuphesi>> CiftDusumSupheleriAsync(CancellationToken ct = default);
}

public sealed record CiftDusumSuphesi(string TcKimlikNo, string AdSoyad, DateOnly Tarih, decimal Gun, DateOnly Bitis, int PuantajYIGunu);

/// <summary>
/// VBA mdlBakiye'nin program karşılığı: verileri toplar (BK_Hesapla), kişi kişi hesaplar
/// (<see cref="IzinBakiyeHesaplayici"/>), izin kartı işlemlerini geri alınabilir şekilde kaydeder (BK_Islem / BK_GeriAl).
/// </summary>
public sealed class IzinBakiyeServisi : IIzinBakiyeServisi
{
    public const string PolitikaAnahtari = "HaftaTatiliPolitikasi";
    public static readonly string[] HareketTurleri = ["HTDUZELTME", "RTKULLANIM", "RTEK", "YIKULLANIM", "YIDEVIR", "YISABIT"];
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IPayControlDbContext _db;
    private readonly IYetkiServisi _yetki;

    public IzinBakiyeServisi(IPayControlDbContext db, IYetkiServisi yetki)
    {
        _db = db;
        _yetki = yetki;
    }

    private async Task<List<T>> Pys<T>(CancellationToken ct) where T : class =>
        (await _db.PysSorgu(typeof(T)).AsNoTracking().ToListAsync(ct)).Cast<T>().ToList();

    private static string Tc(string? s) => (s ?? string.Empty).Trim();

    // ------------------------------------------------------------------ ayar

    public async Task<HaftaTatiliPolitikasi> PolitikaAsync(CancellationToken ct = default)
    {
        var a = (await Pys<PysProgramAyar>(ct)).FirstOrDefault(x => x.Anahtar == PolitikaAnahtari);
        return IzinBakiyeHesaplayici.PolitikaCoz(a?.Deger);
    }

    public async Task PolitikaKaydetAsync(HaftaTatiliPolitikasi politika, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Yonetici, "Hafta tatili genel ayarı", ct);
        var liste = (await _db.PysSorgu(typeof(PysProgramAyar)).ToListAsync(ct)).Cast<PysProgramAyar>().ToList();
        var a = liste.FirstOrDefault(x => x.Anahtar == PolitikaAnahtari);
        if (a is null) { a = new PysProgramAyar { Anahtar = PolitikaAnahtari }; _db.PysEkle(a); }
        a.Deger = IzinBakiyeHesaplayici.PolitikaMetni(politika);
        await _db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ hesap (BK_Hesapla)

    public async Task<IReadOnlyList<IzinBakiyeSonucu>> TumuAsync(string? tesis = null, bool pasifDahil = false, DateOnly? bugun = null, CancellationToken ct = default) =>
        await HesaplaAsync(null, tesis, pasifDahil, bugun ?? DateOnly.FromDateTime(DateTime.Today), false, ct);

    public async Task<IzinBakiyeSonucu?> KisiAsync(string tc, DateOnly? bugun = null, CancellationToken ct = default) =>
        (await HesaplaAsync(Tc(tc), null, true, bugun ?? DateOnly.FromDateTime(DateTime.Today), true, ct)).FirstOrDefault();

    private async Task<List<IzinBakiyeSonucu>> HesaplaAsync(string? tcFiltre, string? tesisFiltre, bool pasifDahil, DateOnly bugun, bool ayrinti, CancellationToken ct)
    {
        var politika = await PolitikaAsync(ct);
        var pq = _db.Personeller.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(tcFiltre)) pq = pq.Where(p => p.TcKimlikNo == tcFiltre);
        if (!string.IsNullOrEmpty(tesisFiltre)) pq = pq.Where(p => p.Isyeri == tesisFiltre);
        var personeller = (await pq.ToListAsync(ct))
            .Where(p => !string.IsNullOrWhiteSpace(p.TcKimlikNo) && (pasifDahil || p.CalismaDurumu == CalismaDurumu.Aktif))
            .GroupBy(p => Tc(p.TcKimlikNo)).Select(g => g.First())
            .OrderBy(p => p.AdSoyad, StringComparer.Create(Tr, true)).ToList();
        if (personeller.Count == 0) return new List<IzinBakiyeSonucu>();
        var tcler = personeller.Select(p => Tc(p.TcKimlikNo)).ToHashSet();
        bool Ilgili(string? tc) => tcler.Contains(Tc(tc));

        // puantaj kodları (tüm tesisler; aynı gün birden çok tesiste varsa ilk okunan esas)
        var kq = _db.PuantajGunleri.AsNoTracking().Where(k => k.Yil <= bugun.Year);
        if (!string.IsNullOrEmpty(tcFiltre)) kq = kq.Where(k => k.TcKimlikNo == tcFiltre);
        var kodSatirlari = await kq.Select(k => new { k.TcKimlikNo, k.Yil, k.Ay, k.Gun, k.Kod, k.TesisAdi }).ToListAsync(ct);
        var kodlar = new Dictionary<string, Dictionary<DateOnly, string>>();
        foreach (var k in kodSatirlari.OrderBy(k => k.TesisAdi, StringComparer.Ordinal))
        {
            var tc = Tc(k.TcKimlikNo);
            if (!tcler.Contains(tc) || k.Ay is < 1 or > 12 || k.Gun < 1 || k.Gun > DateTime.DaysInMonth(k.Yil, k.Ay)) continue;
            var d = new DateOnly(k.Yil, k.Ay, k.Gun);
            if (d > bugun) continue;
            var kod = IzinBakiyeHesaplayici.KodNormalize(k.Kod);
            if (kod.Length == 0) continue;
            if (!kodlar.TryGetValue(tc, out var kd)) kodlar[tc] = kd = new Dictionary<DateOnly, string>();
            kd.TryAdd(d, kod);
        }

        // kesin bakiyeler: iptal edilmemiş, bugüne kadar olan en sonuncusu (EtkinTarih, ID sırası)
        var sabit = new Dictionary<string, KesinBakiyeGirdisi>();
        foreach (var s in (await Pys<PysHTBakiyeSabitleme>(ct)).Where(s => Ilgili(s.TCKN) && s.EtkinTarih.HasValue)
                     .OrderBy(s => s.EtkinTarih).ThenBy(s => s.ID ?? 0))
        {
            var d = DateOnly.FromDateTime(s.EtkinTarih!.Value);
            if (!s.Iptal && d <= bugun) sabit[Tc(s.TCKN)] = new KesinBakiyeGirdisi(d, s.Bakiye ?? 0, s.Aciklama);
        }

        var kesG = Kesintiler((await Pys<PysGorevIzinKesinti>(ct)).Select(k => (k.TCKN, k.Yil, k.Ay, k.Kesinti)), tcler, bugun);
        var kesH = Kesintiler((await Pys<PysHasatIzinKesinti>(ct)).Select(k => (k.TCKN, k.Yil, k.Ay, k.Kesinti)), tcler, bugun);

        var aylikHak = new Dictionary<string, Dictionary<int, decimal>>();
        var manuelYi = new Dictionary<string, Dictionary<int, decimal>>();
        foreach (var m in (await Pys<PysManuelDuzeltme>(ct)).Where(m => Ilgili(m.TCKN)))
        {
            var tc = Tc(m.TCKN);
            if (m.Alan == "HaftasonuHakki")
                Al(aylikHak, tc)[(m.Yil ?? 0) * 100 + (m.Ay ?? 0)] = m.Deger ?? 0;
            else if (m.Alan == "YillikIzinKullanilanManuel")
            {
                var d = Al(manuelYi, tc);
                d[m.Yil ?? 0] = d.GetValueOrDefault(m.Yil ?? 0) + (m.Deger ?? 0);
            }
        }

        var hareketler = (await Pys<PysIzinHareket>(ct)).Where(h => Ilgili(h.TCKN) && h.Tarih.HasValue)
            .OrderBy(h => h.Tarih).ThenBy(h => h.ID ?? 0)
            .GroupBy(h => Tc(h.TCKN))
            .ToDictionary(g => g.Key, g => g.Select(h => new IzinHareketGirdisi(h.ID ?? 0, DateOnly.FromDateTime(h.Tarih!.Value),
                (h.Tur ?? "").ToUpperInvariant(), h.Gun ?? 0, h.TatilAdi, h.Aciklama, h.Iptal)).ToList());

        var ayarlar = (await _db.PersonelIzinAyarlari.AsNoTracking().ToListAsync(ct)).GroupBy(a => Tc(a.TcKimlikNo)).ToDictionary(g => g.Key, g => g.First());
        var nakiller = ayrinti
            ? (await _db.PersonelNakilleri.AsNoTracking().ToListAsync(ct)).Where(n => Ilgili(n.TcKimlikNo)).OrderBy(n => n.NakilTarihi)
                .GroupBy(n => Tc(n.TcKimlikNo)).ToDictionary(g => g.Key, g => g.Select(n => new NakilGirdisi(n.NakilTarihi, n.EskiTesis, n.YeniTesis)).ToList())
            : new Dictionary<string, List<NakilGirdisi>>();

        var tatiller = (await _db.ResmiTatiller.AsNoTracking().Where(t => t.Tarih <= bugun).ToListAsync(ct))
            .GroupBy(t => t.Tarih).Select(g => g.First()).OrderBy(t => t.Tarih)
            .Select(t => new ResmiTatilGirdisi(t.Tarih, t.Ad.Trim(), t.Hak)).ToList();

        var sonuc = new List<IzinBakiyeSonucu>(personeller.Count);
        foreach (var p in personeller)
        {
            var tc = Tc(p.TcKimlikNo);
            ayarlar.TryGetValue(tc, out var ayar);
            var girdi = new IzinBakiyeGirdisi
            {
                TcKimlikNo = tc, AdSoyad = p.AdSoyad, Tesis = p.Isyeri, Unvan = p.Gorevi,
                Pasif = p.CalismaDurumu != CalismaDurumu.Aktif,
                IseGiris = p.IlkIseGirisTarihi, Cikis = p.SonCikisTarihi, Dogum = p.DogumTarihi,
                RtDahil = ayar?.RtDahil ?? true, HaftaGunleri = ayar?.HaftaTatili,
                Kodlar = kodlar.GetValueOrDefault(tc) ?? new Dictionary<DateOnly, string>(),
                KesinBakiye = sabit.GetValueOrDefault(tc),
                GorevKesintileri = kesG.GetValueOrDefault(tc) ?? new Dictionary<int, decimal>(),
                HasatKesintileri = kesH.GetValueOrDefault(tc) ?? new Dictionary<int, decimal>(),
                AylikHakManuel = aylikHak.GetValueOrDefault(tc) ?? new Dictionary<int, decimal>(),
                YillikIzinManuel = manuelYi.GetValueOrDefault(tc) ?? new Dictionary<int, decimal>(),
                Hareketler = hareketler.GetValueOrDefault(tc) ?? new List<IzinHareketGirdisi>(),
                Nakiller = nakiller.GetValueOrDefault(tc) ?? new List<NakilGirdisi>()
            };
            var s = IzinBakiyeHesaplayici.Hesapla(girdi, tatiller, politika, bugun, ayrinti);
            if (s.HaftaGunMetni.Length == 0) s.HaftaGunMetni = IzinBakiyeHesaplayici.GunMetni(null, politika);
            sonuc.Add(s);
        }
        return sonuc;
    }

    private static Dictionary<int, decimal> Al(Dictionary<string, Dictionary<int, decimal>> d, string tc)
    {
        if (!d.TryGetValue(tc, out var x)) d[tc] = x = new Dictionary<int, decimal>();
        return x;
    }

    private static Dictionary<string, Dictionary<int, decimal>> Kesintiler(IEnumerable<(string? Tc, int? Yil, int? Ay, decimal? Kesinti)> satirlar, HashSet<string> tcler, DateOnly bugun)
    {
        var d = new Dictionary<string, Dictionary<int, decimal>>();
        foreach (var (tc0, yil, ay, kes) in satirlar)
        {
            var tc = Tc(tc0);
            if (!tcler.Contains(tc) || yil is null || ay is null or < 1 or > 12 || yil > bugun.Year) continue;
            var x = Al(d, tc);
            var k = yil.Value * 100 + ay.Value;
            x[k] = x.GetValueOrDefault(k) + (kes ?? 0);
        }
        return d;
    }

    // ------------------------------------------------------------------ kart listeleri

    public async Task<IReadOnlyList<IzinManuelKayit>> ManuelKayitlarAsync(string tc, CancellationToken ct = default)
    {
        tc = Tc(tc);
        var liste = new List<IzinManuelKayit>();
        foreach (var s in (await Pys<PysHTBakiyeSabitleme>(ct)).Where(s => Tc(s.TCKN) == tc && s.EtkinTarih.HasValue))
            liste.Add(new IzinManuelKayit($"S|{s.ID}", DateOnly.FromDateTime(s.EtkinTarih!.Value), "Kesin bakiye", s.Aciklama ?? "",
                "= " + IzinBakiyeHesaplayici.Metin(s.Bakiye, false), s.Iptal, s.KayitTarihi));
        foreach (var h in (await Pys<PysIzinHareket>(ct)).Where(h => Tc(h.TCKN) == tc && h.Tarih.HasValue))
            liste.Add(new IzinManuelKayit($"H|{h.ID}", DateOnly.FromDateTime(h.Tarih!.Value), HareketAdi(h.Tur),
                string.Join(" - ", new[] { h.TatilAdi, h.Aciklama }.Where(x => !string.IsNullOrWhiteSpace(x))),
                IzinBakiyeHesaplayici.Metin(h.Gun, (h.Tur ?? "").ToUpperInvariant() is "HTDUZELTME" or "RTEK" or "YIDEVIR"), h.Iptal, h.KayitTarihi));
        foreach (var k in (await Pys<PysGorevIzinKesinti>(ct)).Where(k => Tc(k.TCKN) == tc && k.Yil.HasValue && k.Ay is >= 1 and <= 12))
            liste.Add(new IzinManuelKayit($"G|{k.Yil}|{k.Ay}", AySonu(k.Yil!.Value, k.Ay!.Value), "Görev izni kesintisi",
                new DateOnly(k.Yil.Value, k.Ay.Value, 1).ToString("MMMM yyyy", Tr), IzinBakiyeHesaplayici.Metin(-(k.Kesinti ?? 0)), false, k.KayitTarihi));
        foreach (var k in (await Pys<PysHasatIzinKesinti>(ct)).Where(k => Tc(k.TCKN) == tc && k.Yil.HasValue && k.Ay is >= 1 and <= 12))
            liste.Add(new IzinManuelKayit($"A|{k.Yil}|{k.Ay}", AySonu(k.Yil!.Value, k.Ay!.Value), "Hasat izni kesintisi",
                new DateOnly(k.Yil.Value, k.Ay.Value, 1).ToString("MMMM yyyy", Tr), IzinBakiyeHesaplayici.Metin(-(k.Kesinti ?? 0)), false, k.KayitTarihi));
        return liste.OrderByDescending(l => l.Tarih).ThenByDescending(l => l.KayitTarihi).ToList();
    }

    private static DateOnly AySonu(int yil, int ay) => new(yil, ay, DateTime.DaysInMonth(yil, ay));

    public static string HareketAdi(string? tur) => (tur ?? "").ToUpperInvariant() switch
    {
        "HTDUZELTME" => "HT düzeltmesi",
        "RTKULLANIM" => "Resmi tatil kullanımı",
        "RTEK" => "Resmi tatil ek hak / düşürme",
        "YIKULLANIM" => "Yıllık izin kullanımı",
        "YIDEVIR" => "Yıllık izin ekle / düş",
        "YISABIT" => "Yıllık izin kalan (kesin)",
        var t => t
    };

    public async Task<IReadOnlyList<YilOzetiSatiri>> YilOzetiAsync(string tc, int yil, CancellationToken ct = default)
    {
        tc = Tc(tc);
        var kodlar = await _db.PuantajGunleri.AsNoTracking().Where(k => k.TcKimlikNo == tc && k.Yil == yil).Select(k => new { k.Ay, k.Gun, k.Kod }).ToListAsync(ct);
        var top = new Dictionary<string, decimal>();
        foreach (var g in kodlar.GroupBy(k => (k.Ay, k.Gun)))
        {
            var kod = IzinBakiyeHesaplayici.KodNormalize(g.First().Kod);
            if (kod.Length == 0) continue;
            top[kod] = top.GetValueOrDefault(kod) + (kod == "YG" ? 0.5m : 1m);
        }
        foreach (var h in (await Pys<PysIzinHareket>(ct)).Where(h => Tc(h.TCKN) == tc && !h.Iptal && h.Tarih?.Year == yil))
        {
            var t = (h.Tur ?? "").ToUpperInvariant();
            if (t == "YIKULLANIM") top["YI"] = top.GetValueOrDefault("YI") + (h.Gun ?? 0);
            if (t == "RTKULLANIM") top["RT (elle)"] = top.GetValueOrDefault("RT (elle)") + (h.Gun ?? 0);
        }
        static bool Izin(string k) => k is not ("C" or "G" or "M");
        return top.OrderBy(k => (Izin(k.Key) ? 1000 : 100000) - k.Value)
            .Select(k =>
            {
                if (k.Key == "RT (elle)") return new YilOzetiSatiri("RT", "Resmi tatil kullanımı (elle)", k.Value, "İzin");
                var kod = PuantajHesaplayici.Kodlar.FirstOrDefault(x => IzinBakiyeHesaplayici.KodNormalize(x.Kod) == k.Key);
                return new YilOzetiSatiri(kod?.Kod ?? k.Key, kod?.Ad ?? k.Key, k.Value, Izin(k.Key) ? "İzin" : "Çalışma");
            }).ToList();
    }

    public async Task<IReadOnlyList<IzinIslemKaydi>> IslemListesiAsync(string tc, int enFazla = 200, CancellationToken ct = default)
    {
        tc = Tc(tc);
        return (await Pys<PysIzinIslemLog>(ct)).Where(l => Tc(l.TCKN) == tc).OrderByDescending(l => l.ID ?? 0).Take(enFazla)
            .Select(l => new IzinIslemKaydi(l.ID ?? 0, l.Zaman, l.Islem ?? "", l.Durum ?? "", l.Kullanici)).ToList();
    }

    // ------------------------------------------------------------------ işlemler (BK_Islem)

    /// <summary>İleri komutları tek işlemde çalıştırır, geri alma komutlarını günlüğe yazar. {ID} = son INSERT'in kimliği.</summary>
    private async Task IslemAsync(string tc, string islem, IEnumerable<string?> ileri, IEnumerable<string?> geri, string kullanici, CancellationToken ct)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "İzin ve bakiye", ct);
        var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            int? yeniId = null;
            foreach (var s in ileri.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var id = await IzinIslemSql.CalistirAsync(_db, s!, ct);
                await _db.SaveChangesAsync(ct);
                if (s!.TrimStart().StartsWith("INSERT", StringComparison.OrdinalIgnoreCase)) yeniId = id;
            }
            var geriMetin = string.Join("\n", geri.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g!.Replace("{ID}", yeniId?.ToString(CultureInfo.InvariantCulture) ?? "0")));
            await LogYazAsync(tc, islem, geriMetin, kullanici, ct);
            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    private async Task LogYazAsync(string tc, string islem, string geriMetin, string kullanici, CancellationToken ct)
    {
        var loglar = (await _db.PysSorgu(typeof(PysIzinIslemLog)).ToListAsync(ct)).Cast<PysIzinIslemLog>().ToList();
        _db.PysEkle(new PysIzinIslemLog
        {
            ID = loglar.Select(l => l.ID ?? 0).DefaultIfEmpty(0).Max() + 1,
            TCKN = tc, Zaman = DateTime.Now, Islem = islem.Length > 255 ? islem[..255] : islem,
            GeriAl = geriMetin, Durum = "Yapıldı", Kullanici = kullanici
        });
    }

    private static string Kisalt(string? s, int n) { s = (s ?? string.Empty).Trim(); return s.Length > n ? s[..n] : s; }
    private static string Gun(DateOnly d) => d.ToString("dd.MM.yyyy", Tr);

    private async Task<string> IdlerAsync<T>(Func<T, bool> kosul, Func<T, int?> id, CancellationToken ct) where T : class =>
        string.Join(",", (await Pys<T>(ct)).Where(kosul).Select(id).Where(i => i.HasValue));

    private async Task KisiVarDogrulaAsync(string tc, CancellationToken ct)
    {
        if (tc.Length == 0) throw new InvalidOperationException("Personel seçilmedi.");
        if (!await _db.Personeller.AnyAsync(p => p.TcKimlikNo == tc, ct)) throw new InvalidOperationException("Personel bulunamadı: " + tc);
    }

    public async Task KesinBakiyeKaydetAsync(string tc, DateOnly tarih, decimal bakiye, string? aciklama, string kullanici, CancellationToken ct = default)
    {
        tc = Tc(tc);
        await KisiVarDogrulaAsync(tc, ct);
        if (bakiye % 0.5m != 0) throw new InvalidOperationException("Bakiye yarım gün katı olmalıdır (ör. 3 ya da 3,5).");
        aciklama = Kisalt(aciklama, 255);
        var eski = await IdlerAsync<PysHTBakiyeSabitleme>(s => Tc(s.TCKN) == tc && s.EtkinTarih.HasValue && DateOnly.FromDateTime(s.EtkinTarih.Value) == tarih && !s.Iptal, s => s.ID, ct);
        await IslemAsync(tc, $"Kesin bakiye {Gun(tarih)} = {IzinBakiyeHesaplayici.Metin(bakiye, false)}" + (aciklama.Length > 0 ? $" ({aciklama})" : ""),
            [
                $"UPDATE HTBakiyeSabitleme SET Iptal=True WHERE TCKN={IzinIslemSql.Metin(tc)} AND EtkinTarih={IzinIslemSql.Tarih(tarih)}",
                $"INSERT INTO HTBakiyeSabitleme (TCKN, EtkinTarih, Bakiye, Aciklama, KayitTarihi, Iptal) VALUES ({IzinIslemSql.Metin(tc)}, {IzinIslemSql.Tarih(tarih)}, {IzinIslemSql.Sayi(bakiye)}, {IzinIslemSql.Metin(aciklama)}, Now(), False)"
            ],
            [
                "UPDATE HTBakiyeSabitleme SET Iptal=True WHERE ID={ID}",
                eski.Length > 0 ? $"UPDATE HTBakiyeSabitleme SET Iptal=False WHERE ID IN ({eski})" : null
            ], kullanici, ct);
    }

    public async Task TopluKesinBakiyeAsync(IReadOnlyCollection<string> tcler, DateOnly tarih, decimal bakiye, string aciklama, string kullanici, CancellationToken ct = default)
    {
        var liste = tcler.Select(Tc).Where(t => t.Length > 0).Distinct().ToList();
        if (liste.Count == 0) throw new InvalidOperationException("Personel seçilmedi.");
        if (bakiye % 0.5m != 0) throw new InvalidOperationException("Bakiye yarım gün katı olmalıdır.");
        var etiket = Kisalt(aciklama, 200) + " [" + DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + "]";
        var inList = string.Join(",", liste.Select(IzinIslemSql.Metin));
        var set = liste.ToHashSet();
        var eski = await IdlerAsync<PysHTBakiyeSabitleme>(s => set.Contains(Tc(s.TCKN)) && s.EtkinTarih.HasValue && DateOnly.FromDateTime(s.EtkinTarih.Value) == tarih && !s.Iptal, s => s.ID, ct);
        var ileri = new List<string?> { $"UPDATE HTBakiyeSabitleme SET Iptal=True WHERE TCKN IN ({inList}) AND EtkinTarih={IzinIslemSql.Tarih(tarih)}" };
        ileri.AddRange(liste.Select(tc => $"INSERT INTO HTBakiyeSabitleme (TCKN, EtkinTarih, Bakiye, Aciklama, KayitTarihi, Iptal) VALUES ({IzinIslemSql.Metin(tc)}, {IzinIslemSql.Tarih(tarih)}, {IzinIslemSql.Sayi(bakiye)}, {IzinIslemSql.Metin(etiket)}, Now(), False)"));
        await IslemAsync("SEZON", $"Toplu kesin bakiye ({liste.Count} kişi): {Gun(tarih)} = {IzinBakiyeHesaplayici.Metin(bakiye, false)} - {aciklama}", ileri,
            [
                $"UPDATE HTBakiyeSabitleme SET Iptal=True WHERE Aciklama={IzinIslemSql.Metin(etiket)}",
                eski.Length > 0 ? $"UPDATE HTBakiyeSabitleme SET Iptal=False WHERE ID IN ({eski})" : null
            ], kullanici, ct);
    }

    public async Task HareketKaydetAsync(string tc, DateOnly tarih, string tur, decimal gun, string? tatilAdi, string? aciklama, string kullanici, CancellationToken ct = default)
    {
        tc = Tc(tc);
        tur = (tur ?? "").Trim().ToUpperInvariant();
        await KisiVarDogrulaAsync(tc, ct);
        if (!HareketTurleri.Contains(tur)) throw new InvalidOperationException("Geçersiz hareket türü: " + tur);
        if (gun % 0.5m != 0) throw new InvalidOperationException("Gün yarım gün katı olmalıdır (ör. 1 ya da 0,5).");
        if (gun == 0 && tur != "YISABIT") throw new InvalidOperationException("Gün 0 olamaz.");
        if (tur is "RTKULLANIM" or "YIKULLANIM" && gun < 0) throw new InvalidOperationException("Kullanım günü eksi olamaz.");
        if (tur == "YISABIT" && gun < 0) throw new InvalidOperationException("Yıllık izin kalan eksi olamaz.");
        if (tarih > DateOnly.FromDateTime(DateTime.Today).AddYears(1)) throw new InvalidOperationException("Tarih bir yıldan daha ileri olamaz.");
        tatilAdi = Kisalt(tatilAdi, 120);
        aciklama = Kisalt(aciklama, 255);
        var ad = tur switch
        {
            "HTDUZELTME" => "HT düzeltmesi " + IzinBakiyeHesaplayici.Metin(gun),
            "RTKULLANIM" => "Resmi tatil kullanımı " + IzinBakiyeHesaplayici.Metin(gun, false) + (tatilAdi.Length > 0 ? $" ({tatilAdi})" : ""),
            "RTEK" => "Resmi tatil ek hak / düşürme " + IzinBakiyeHesaplayici.Metin(gun),
            "YIKULLANIM" => "Yıllık izin kullanımı " + IzinBakiyeHesaplayici.Metin(gun, false) + " gün",
            "YIDEVIR" => "Yıllık izin ekle / düş " + IzinBakiyeHesaplayici.Metin(gun),
            "YISABIT" => "Yıllık izin kalan (kesin) " + IzinBakiyeHesaplayici.Metin(gun, false) + " gün",
            _ => tur + " " + IzinBakiyeHesaplayici.Metin(gun)
        };
        await IslemAsync(tc, $"{Gun(tarih)} {ad}" + (aciklama.Length > 0 ? " - " + aciklama : ""),
            [$"INSERT INTO IzinHareket (TCKN, Tarih, Tur, Gun, TatilAdi, Aciklama, KayitTarihi, Iptal) VALUES ({IzinIslemSql.Metin(tc)}, {IzinIslemSql.Tarih(tarih)}, {IzinIslemSql.Metin(tur)}, {IzinIslemSql.Sayi(gun)}, {IzinIslemSql.Metin(tatilAdi)}, {IzinIslemSql.Metin(aciklama)}, Now(), False)"],
            ["UPDATE IzinHareket SET Iptal=True WHERE ID={ID}"], kullanici, ct);
    }

    private static string? KesintiSql(string tablo, string tc, int yil, int ay, decimal? gun) =>
        gun is null || gun == 0 ? null
            : $"INSERT INTO {tablo} (TCKN, Yil, Ay, Kesinti, KayitTarihi) VALUES ({IzinIslemSql.Metin(tc)}, {yil}, {ay}, {IzinIslemSql.Sayi(gun.Value)}, Now())";

    public async Task KesintiKaydetAsync(string tc, string tur, int yil, int ay, decimal gun, string kullanici, CancellationToken ct = default)
    {
        tc = Tc(tc);
        await KisiVarDogrulaAsync(tc, ct);
        if (ay is < 1 or > 12 || yil < 2000 || yil > 2100) throw new InvalidOperationException("Geçersiz ay / yıl.");
        if (gun < 0 || gun > 31 || gun % 0.5m != 0) throw new InvalidOperationException("Kesinti 0-31 arası, yarım gün katı olmalıdır.");
        var gorev = tur.Trim().ToUpperInvariant() == "G";
        var tablo = gorev ? "GorevIzinKesinti" : "HasatIzinKesinti";
        decimal? eski = gorev
            ? (await Pys<PysGorevIzinKesinti>(ct)).Where(k => Tc(k.TCKN) == tc && k.Yil == yil && k.Ay == ay).Select(k => k.Kesinti).FirstOrDefault()
            : (await Pys<PysHasatIzinKesinti>(ct)).Where(k => Tc(k.TCKN) == tc && k.Yil == yil && k.Ay == ay).Select(k => k.Kesinti).FirstOrDefault();
        var sil = $"DELETE FROM {tablo} WHERE TCKN={IzinIslemSql.Metin(tc)} AND Yil={yil} AND Ay={ay}";
        await IslemAsync(tc, $"{(gorev ? "Görev" : "Hasat")} izni kesintisi {new DateOnly(yil, ay, 1).ToString("MMMM yyyy", Tr)}: " +
                             $"{(eski is null ? "yok" : IzinBakiyeHesaplayici.Metin(eski, false))} → {IzinBakiyeHesaplayici.Metin(gun, false)}",
            [sil, KesintiSql(tablo, tc, yil, ay, gun)], [sil, KesintiSql(tablo, tc, yil, ay, eski)], kullanici, ct);
    }

    public Task RtDahilKaydetAsync(string tc, bool dahil, string kullanici, CancellationToken ct = default) =>
        AyarYazAsync([Tc(tc)], dahil, null, "Resmi tatiller bakiyeye " + (dahil ? "dahil edildi" : "dahil edilmedi"), Tc(tc), kullanici, ct);

    public async Task HaftaGunuKaydetAsync(string tc, string? gun, string kullanici, CancellationToken ct = default)
    {
        var temiz = IzinBakiyeHesaplayici.GunTemizle(gun);
        await AyarYazAsync([Tc(tc)], null, temiz, "İzin günü: " + IzinBakiyeHesaplayici.GunMetni(temiz, await PolitikaAsync(ct)), Tc(tc), kullanici, ct);
    }

    public async Task TopluHaftaGunuAsync(IReadOnlyCollection<string> tcler, string? gun, string kullanici, CancellationToken ct = default)
    {
        var liste = tcler.Select(Tc).Where(t => t.Length > 0).Distinct().ToList();
        if (liste.Count == 0) throw new InvalidOperationException("Personel seçilmedi.");
        var temiz = IzinBakiyeHesaplayici.GunTemizle(gun);
        await AyarYazAsync(liste, null, temiz, $"Toplu izin günü ({liste.Count} kişi): " + IzinBakiyeHesaplayici.GunMetni(temiz, await PolitikaAsync(ct)), "*", kullanici, ct);
    }

    /// <summary>VBA BK_AyarSql: PersonelIzinAyar satırını sil + yeniden yaz; geri alma eskisini geri koyar. null = değiştirme.</summary>
    private async Task AyarYazAsync(IReadOnlyList<string> tcler, bool? rt, string? gun, string islem, string logTc, string kullanici, CancellationToken ct)
    {
        foreach (var tc in tcler) await KisiVarDogrulaAsync(tc, ct);
        var ayarlar = (await _db.PersonelIzinAyarlari.AsNoTracking().ToListAsync(ct)).GroupBy(a => Tc(a.TcKimlikNo)).ToDictionary(g => g.Key, g => g.First());
        var ileri = new List<string?>();
        var geri = new List<string?>();
        foreach (var tc in tcler)
        {
            var varMi = ayarlar.TryGetValue(tc, out var eski);
            var yeniRt = rt ?? eski?.RtDahil ?? true;
            var yeniGun = gun ?? eski?.HaftaTatili ?? "";
            var sil = $"DELETE FROM PersonelIzinAyar WHERE TCKN={IzinIslemSql.Metin(tc)}";
            ileri.Add(sil);
            ileri.Add($"INSERT INTO PersonelIzinAyar (TCKN, RTDahil, HaftaTatili) VALUES ({IzinIslemSql.Metin(tc)}, {IzinIslemSql.Mantik(yeniRt)}, {(yeniGun.Length == 0 ? "Null" : IzinIslemSql.Metin(yeniGun))})");
            geri.Add(sil);
            if (varMi)
                geri.Add($"INSERT INTO PersonelIzinAyar (TCKN, RTDahil, HaftaTatili) VALUES ({IzinIslemSql.Metin(tc)}, {IzinIslemSql.Mantik(eski!.RtDahil)}, {(string.IsNullOrEmpty(eski.HaftaTatili) ? "Null" : IzinIslemSql.Metin(eski.HaftaTatili))})");
        }
        await IslemAsync(logTc, islem, ileri, geri, kullanici, ct);
    }

    public async Task ManuelKaldirAsync(string tc, string anahtar, string metin, string kullanici, CancellationToken ct = default)
    {
        var p = anahtar.Split('|');
        switch (p[0])
        {
            case "S":
                await IslemAsync(Tc(tc), "İptal: " + metin, [$"UPDATE HTBakiyeSabitleme SET Iptal=True WHERE ID={int.Parse(p[1], CultureInfo.InvariantCulture)}"],
                    [$"UPDATE HTBakiyeSabitleme SET Iptal=False WHERE ID={int.Parse(p[1], CultureInfo.InvariantCulture)}"], kullanici, ct);
                break;
            case "H":
                await IslemAsync(Tc(tc), "İptal: " + metin, [$"UPDATE IzinHareket SET Iptal=True WHERE ID={int.Parse(p[1], CultureInfo.InvariantCulture)}"],
                    [$"UPDATE IzinHareket SET Iptal=False WHERE ID={int.Parse(p[1], CultureInfo.InvariantCulture)}"], kullanici, ct);
                break;
            case "G":
            case "A":
                await KesintiKaydetAsync(tc, p[0] == "G" ? "G" : "H", int.Parse(p[1], CultureInfo.InvariantCulture), int.Parse(p[2], CultureInfo.InvariantCulture), 0, kullanici, ct);
                break;
            default: throw new InvalidOperationException("Geçersiz kayıt anahtarı.");
        }
    }

    public async Task ManuelGeriYukleAsync(string tc, string anahtar, string metin, string kullanici, CancellationToken ct = default)
    {
        var p = anahtar.Split('|');
        var tablo = p[0] switch { "S" => "HTBakiyeSabitleme", "H" => "IzinHareket", _ => throw new InvalidOperationException("Bu kayıt yeniden etkinleştirilemez.") };
        var id = int.Parse(p[1], CultureInfo.InvariantCulture);
        await IslemAsync(Tc(tc), "Yeniden etkin: " + metin, [$"UPDATE {tablo} SET Iptal=False WHERE ID={id}"], [$"UPDATE {tablo} SET Iptal=True WHERE ID={id}"], kullanici, ct);
    }

    /// <summary>VBA PYS_DogumTarihiHatasi denetimi ile.</summary>
    public static string? DogumTarihiHatasi(DateOnly? dogum, DateOnly? iseGiris, DateOnly bugun)
    {
        if (dogum is not { } dt) return null;
        if (dt < new DateOnly(1930, 1, 1)) return $"Doğum tarihi 1930'dan önce olamaz ({dt:dd.MM.yyyy}).";
        if (dt > bugun.AddYears(-14)) return $"Doğum tarihi {dt:dd.MM.yyyy}: kişi 14 yaşından küçük görünüyor.\nİki haneli yıl yazdıysanız dört haneli yazın (örn. 06.07.1996).";
        if (iseGiris is { } bas && dt > bas.AddYears(-14)) return $"Doğum tarihi {dt:dd.MM.yyyy} ile işe başlama {bas:dd.MM.yyyy} uyuşmuyor (işe girişte 14 yaşından küçük).";
        return null;
    }

    public async Task DogumTarihiKaydetAsync(string tc, DateOnly? dogum, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Doğum tarihi", ct);
        tc = Tc(tc);
        var p = await _db.Personeller.FirstOrDefaultAsync(x => x.TcKimlikNo == tc, ct) ?? throw new InvalidOperationException("Personel bulunamadı.");
        var hata = DogumTarihiHatasi(dogum, p.IlkIseGirisTarihi, DateOnly.FromDateTime(DateTime.Today));
        if (hata is not null) throw new InvalidOperationException(hata + "\n\nDoğum tarihi kaydedilmedi; düzeltip tekrar kaydedin.");
        var eski = p.DogumTarihi;
        p.DogumTarihi = dogum;
        p.GuncellemeZamani = DateTime.UtcNow;
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = p.Id, VarlikTuru = "Personel", VarlikId = p.Id, Islem = "Doğum tarihi", AlanAdi = "DogumTarihi",
            EskiDeger = eski?.ToString("dd.MM.yyyy"), YeniDeger = dogum?.ToString("dd.MM.yyyy"), KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task GeriAlAsync(int logId, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "İzin işlemi geri alma", ct);
        var log = (await _db.PysSorgu(typeof(PysIzinIslemLog)).ToListAsync(ct)).Cast<PysIzinIslemLog>().FirstOrDefault(l => l.ID == logId)
                  ?? throw new InvalidOperationException("İşlem bulunamadı.");
        if (log.Durum != "Yapıldı") throw new InvalidOperationException("Bu işlem zaten geri alınmış.");
        var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var satir in (log.GeriAl ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                await IzinIslemSql.CalistirAsync(_db, satir, ct);
                await _db.SaveChangesAsync(ct);
            }
            log.Durum = "Geri alındı";
            log.GeriAlmaZamani = DateTime.Now;
            await _db.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);
        }
        catch
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------ izin süresi ve çift düşüm

    private async Task<List<ResmiTatilGirdisi>> TumTatillerAsync(CancellationToken ct) =>
        (await _db.ResmiTatiller.AsNoTracking().ToListAsync(ct)).GroupBy(t => t.Tarih).Select(g => g.First())
            .Select(t => new ResmiTatilGirdisi(t.Tarih, t.Ad.Trim(), t.Hak)).OrderBy(t => t.Tarih).ToList();

    public async Task<decimal> IzinGunuSayAsync(DateOnly d1, DateOnly d2, string? tc, CancellationToken ct = default)
    {
        string? gun = null;
        if (!string.IsNullOrWhiteSpace(tc))
            gun = (await _db.PersonelIzinAyarlari.AsNoTracking().FirstOrDefaultAsync(a => a.TcKimlikNo == Tc(tc), ct))?.HaftaTatili;
        return IzinBakiyeHesaplayici.IzinGunuSay(d1, d2, gun, await PolitikaAsync(ct), await TumTatillerAsync(ct));
    }

    public async Task<IzinBakiyeHesaplayici.IzinBitisSonucu?> IzinBitisHesaplaAsync(DateOnly bas, int isGunu, CancellationToken ct = default) =>
        IzinBakiyeHesaplayici.IzinBitisHesapla(bas, isGunu, await TumTatillerAsync(ct));

    public async Task<IReadOnlyList<DateOnly>> PuantajYIGunleriAsync(string tc, DateOnly d1, DateOnly d2, CancellationToken ct = default)
    {
        tc = Tc(tc);
        var satir = await _db.PuantajGunleri.AsNoTracking().Where(k => k.TcKimlikNo == tc && k.Yil >= d1.Year && k.Yil <= d2.Year)
            .Select(k => new { k.Yil, k.Ay, k.Gun, k.Kod }).ToListAsync(ct);
        return satir.Where(k => k.Ay is >= 1 and <= 12 && k.Gun >= 1 && k.Gun <= DateTime.DaysInMonth(k.Yil, k.Ay) && IzinBakiyeHesaplayici.KodNormalize(k.Kod) == "YI")
            .Select(k => new DateOnly(k.Yil, k.Ay, k.Gun)).Where(d => d >= d1 && d <= d2).Distinct().OrderBy(d => d).ToList();
    }

    public async Task YillikIzinKullanimiKaydetAsync(string tc, DateOnly bas, DateOnly? bit, decimal gun, string? aciklama, string kullanici, CancellationToken ct = default)
    {
        if (gun <= 0) throw new InvalidOperationException("Gün sıfırdan büyük olmalıdır.");
        if (bit is { } b && (b < bas || b.DayNumber - bas.DayNumber > 366)) throw new InvalidOperationException("Bitiş tarihi hatalı.");
        var son = bit ?? bas.AddDays((int)Math.Floor(gun) - 1 + (int)Math.Floor(gun / 6));
        if (son < bas) son = bas;
        var yi = await PuantajYIGunleriAsync(tc, bas, son, ct);
        if (yi.Count > 0)
            throw new InvalidOperationException($"Puantajda {bas:dd.MM.yyyy} - {son:dd.MM.yyyy} arasında zaten {yi.Count} gün Yİ var.\n" +
                "Puantajdaki Yİ günleri yıllık izinden kendiliğinden düşülür; bu elle kayıt da girilirse aynı izin İKİ KEZ düşülürdü.\n\n" +
                "Kayıt yapılmadı. Bu günler puantajda Yİ olarak duruyorsa elle kullanım girmeyin; elle girmek istiyorsanız önce puantajdaki Yİ günlerini düzeltin.");
        var ack = (aciklama ?? "").Trim();
        if (bit is { } b2) ack = $"{bas:dd.MM.yyyy}-{b2:dd.MM.yyyy}" + (ack.Length > 0 ? " " + ack : "");
        await HareketKaydetAsync(tc, bas, "YIKULLANIM", gun, null, ack, kullanici, ct);
    }

    public async Task<IReadOnlyList<CiftDusumSuphesi>> CiftDusumSupheleriAsync(CancellationToken ct = default)
    {
        var hareket = (await Pys<PysIzinHareket>(ct)).Where(h => !h.Iptal && (h.Tur ?? "").ToUpperInvariant() == "YIKULLANIM" && h.Tarih.HasValue).ToList();
        if (hareket.Count == 0) return Array.Empty<CiftDusumSuphesi>();
        var tcler = hareket.Select(h => Tc(h.TCKN)).ToHashSet();
        var minY = hareket.Min(h => h.Tarih!.Value.Year);
        var gunler = (await _db.PuantajGunleri.AsNoTracking().Where(k => k.Yil >= minY).Select(k => new { k.TcKimlikNo, k.Yil, k.Ay, k.Gun, k.Kod }).ToListAsync(ct))
            .Where(k => tcler.Contains(Tc(k.TcKimlikNo)) && IzinBakiyeHesaplayici.KodNormalize(k.Kod) == "YI" && k.Ay is >= 1 and <= 12 && k.Gun >= 1 && k.Gun <= DateTime.DaysInMonth(k.Yil, k.Ay))
            .Select(k => (Tc(k.TcKimlikNo), new DateOnly(k.Yil, k.Ay, k.Gun))).ToHashSet();
        var adlar = (await _db.Personeller.AsNoTracking().Select(p => new { p.TcKimlikNo, p.AdSoyad }).ToListAsync(ct))
            .GroupBy(p => Tc(p.TcKimlikNo)).ToDictionary(g => g.Key, g => g.First().AdSoyad);
        var sonuc = new List<CiftDusumSuphesi>();
        foreach (var h in hareket)
        {
            var tc = Tc(h.TCKN);
            var b = DateOnly.FromDateTime(h.Tarih!.Value);
            var e = IzinBakiyeHesaplayici.YillikIzinBitisi(b, h.Gun ?? 0, h.Aciklama);
            var n = 0;
            for (var d = b; d <= e; d = d.AddDays(1)) if (gunler.Contains((tc, d))) n++;
            if (n > 0) sonuc.Add(new CiftDusumSuphesi(tc, adlar.GetValueOrDefault(tc) ?? tc, b, h.Gun ?? 0, e, n));
        }
        return sonuc;
    }
}
