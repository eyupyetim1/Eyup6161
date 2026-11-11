using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

/// <summary>Rapor merkezi: her rapor tablo (kolon + satır + toplam) üretir; dışa aktarma tüm raporlar için ortaktır.</summary>
public sealed class RaporServisi : IRaporServisi
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private const string TesisYok = "(Tesis belirtilmemiş)";
    private readonly IPayControlDbContext _db;

    public RaporServisi(IPayControlDbContext db) => _db = db;

    public IReadOnlyList<RaporTanimi> Tanimlar { get; } =
    [
        new(RaporTuru.DonemBordroOzeti, "Dönem bordro özeti", "Bordro", "Dönem ve tesis bazında personel sayısı, maaş, net istihkak ve kesintiler."),
        new(RaporTuru.TesisKarsilastirma, "Tesis karşılaştırma", "Tesis", "Tesisler arasında personel, bordro, ödeme, fark, giriş-çıkış ve onay durumu."),
        new(RaporTuru.PersonelMaliyetTrendi, "Personel maliyet trendi", "Tesis", "Tesis bazında aylık net maaş maliyeti (dönem sütunlu)."),
        new(RaporTuru.TesisPersonelListesi, "Tesis personel listesi", "Tesis", "Tesislere göre personel, durum, giriş tarihi, güncel maaş ve IBAN."),
        new(RaporTuru.OdenmemisHakEdisYaslandirma, "Ödenmemiş hak ediş yaşlandırma", "Ödeme", "Ödenmemiş hak edişlerin 0-30 / 31-60 / 61-90 / 90+ gün dağılımı."),
        new(RaporTuru.BankaKasaOdemeDokumu, "Banka ve kasa ödeme dökümü", "Ödeme", "Hak ediş dönemine göre tüm ödemeler (iptaller işaretli)."),
        new(RaporTuru.OdemeKontrolIstisnalari, "Ödeme kontrol istisnaları", "Denetim", "Kritik uyarıya rağmen gerekçeli istisna ile onaylanan ödemeler."),
        new(RaporTuru.MutabakatFarklari, "Mutabakat farkları", "Denetim", "Bordro × onaylı liste × banka farkları ve çözüm durumu."),
        new(RaporTuru.KapanisOzetleri, "Kapanış özetleri", "Denetim", "Her kapanışın dondurulmuş toplamları (yeniden açma sonrası kapanışlar dahil)."),
        new(RaporTuru.DenetimIzi, "Denetim izi", "Denetim", "Seçilen tarih aralığındaki tüm değişiklikler ve kullanıcılar."),
        new(RaporTuru.AvansDurumu, "Avans durumu", "Alacak/Borç", "Avanslar, mahsup edilen, kalan ve geciken taksitler."),
        new(RaporTuru.FazlaOdemeVeTahsilat, "Fazla ödeme ve tahsilat", "Alacak/Borç", "Şüpheli ve kesinleşmiş fazla ödemeler, tahsil edilen ve plan kalanı."),
        new(RaporTuru.PersonelBakiyeListesi, "Personel bakiye listesi", "Alacak/Borç", "Tüm personelin hesap ekstresi bakiyeleri tek tabloda."),
        new(RaporTuru.KesintiRaporu, "Kesinti raporu", "Bordro", "Dönem bazında icra, BES, avans, fazla ödeme ve diğer kesintiler."),
        new(RaporTuru.IcraDosyalari, "İcra dosyaları", "Bordro", "İcra dosyaları, kesilen ve kalan tutarlar."),
        new(RaporTuru.GirisCikisDevirHizi, "Giriş-çıkış ve devir hızı", "Personel", "Tesis ve ay bazında işe giriş, çıkış ve devir hızı."),
        new(RaporTuru.CikisTalimatTakibi, "Çıkış talimatı takibi", "Personel", "Verilen çıkış talimatları, uygulanıp uygulanmadığı, son çalışma gününden sonra yapılan ödemeler ve tahmini fazla ödeme."),
        new(RaporTuru.SgkCikisDurumu, "SGK çıkış bildirgesi durumu", "Personel", "İşten çıkışların SGK bildirge durumu ve gecikmeler."),
        new(RaporTuru.MaasDegisiklikleri, "Maaş değişiklikleri", "Personel", "Personel kartlarındaki maaş değişiklikleri, kullanıcı ve gerekçe.")
    ];

    public async Task<RaporSonucu> OlusturAsync(RaporTuru tur, RaporParametreleri p, CancellationToken ct = default)
    {
        if (p.Baslangic > p.Bitis) throw new InvalidOperationException("Başlangıç dönemi bitiş döneminden sonra olamaz.");
        var tanim = Tanimlar.First(t => t.Tur == tur);
        var personeller = await _db.Personeller.AsNoTracking().Include(x => x.CalismaDonemleri).ToListAsync(ct);
        var kapsam = personeller.Where(x => PersonelUyar(x, p)).ToDictionary(x => x.Id);

        var (kolonlar, satirlar, toplam, ozet) = tur switch
        {
            RaporTuru.DonemBordroOzeti => await DonemBordroOzeti(p, ct),
            RaporTuru.TesisKarsilastirma => await TesisKarsilastirma(p, personeller, ct),
            RaporTuru.PersonelMaliyetTrendi => await MaliyetTrendi(p, ct),
            RaporTuru.TesisPersonelListesi => TesisPersonelListesi(kapsam.Values),
            RaporTuru.OdenmemisHakEdisYaslandirma => await Yaslandirma(kapsam, ct),
            RaporTuru.BankaKasaOdemeDokumu => await OdemeDokumu(p, kapsam, ct),
            RaporTuru.OdemeKontrolIstisnalari => await Istisnalar(p, ct),
            RaporTuru.MutabakatFarklari => await Farklar(p, ct),
            RaporTuru.KapanisOzetleri => await Kapanislar(p, ct),
            RaporTuru.DenetimIzi => await DenetimIzi(p, personeller, ct),
            RaporTuru.AvansDurumu => await Avanslar(kapsam, ct),
            RaporTuru.FazlaOdemeVeTahsilat => await FazlaOdeme(kapsam, ct),
            RaporTuru.PersonelBakiyeListesi => await Bakiyeler(kapsam, ct),
            RaporTuru.KesintiRaporu => await Kesintiler(p, ct),
            RaporTuru.IcraDosyalari => await Icra(kapsam, ct),
            RaporTuru.GirisCikisDevirHizi => GirisCikis(p, kapsam.Values),
            RaporTuru.SgkCikisDurumu => await Sgk(p, kapsam, ct),
            RaporTuru.CikisTalimatTakibi => await CikisTakibi(p, kapsam, ct),
            RaporTuru.MaasDegisiklikleri => await MaasDegisiklikleri(p, kapsam, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(tur))
        };

        return new RaporSonucu
        {
            Tur = tur,
            Baslik = tanim.Ad,
            Aciklama = tanim.Aciklama,
            ParametreMetni = ParametreMetni(p),
            Kolonlar = kolonlar,
            Satirlar = satirlar,
            Toplam = toplam,
            Ozet = ozet
        };
    }

    private static bool PersonelUyar(Personel x, RaporParametreleri p) =>
        (p.PersonelId is null || x.Id == p.PersonelId) &&
        (string.IsNullOrWhiteSpace(p.Sirket) || Esit(x.Sirket, p.Sirket)) &&
        (string.IsNullOrWhiteSpace(p.Tesis) || Esit(x.Isyeri, p.Tesis));

    private static bool Esit(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.CurrentCultureIgnoreCase);
    private static bool TesisUyar(string? tesis, RaporParametreleri p) => string.IsNullOrWhiteSpace(p.Tesis) || Esit(tesis, p.Tesis);
    private static string D(int y, int a) => $"{y}-{a:D2}";
    private static bool DonemIcinde(int y, int a, RaporParametreleri p) { var d = new DateOnly(y, a, 1); return d >= p.Baslangic && d <= p.Bitis; }

    private static string ParametreMetni(RaporParametreleri p)
    {
        var s = new List<string> { $"Dönem: {D(p.BaslangicYil, p.BaslangicAy)} – {D(p.BitisYil, p.BitisAy)}" };
        if (!string.IsNullOrWhiteSpace(p.Sirket)) s.Add($"Şirket: {p.Sirket}");
        s.Add($"Tesis: {(string.IsNullOrWhiteSpace(p.Tesis) ? "Tümü" : p.Tesis)}");
        return string.Join(" | ", s);
    }

    private static IEnumerable<DateOnly> Donemler(RaporParametreleri p)
    {
        for (var d = p.Baslangic; d <= p.Bitis; d = d.AddMonths(1)) yield return d;
    }

    private async Task<List<MaasDonemKaydi>> BordroAsync(RaporParametreleri p, CancellationToken ct)
    {
        var bas = p.BaslangicYil * 100 + p.BaslangicAy;
        var bit = p.BitisYil * 100 + p.BitisAy;
        return (await _db.MaasDonemKayitlari.AsNoTracking().Include(m => m.Kalemler)
                .Where(m => m.DonemYil * 100 + m.DonemAy >= bas && m.DonemYil * 100 + m.DonemAy <= bit).ToListAsync(ct))
            .Where(m => TesisUyar(m.Isyeri, p) && (string.IsNullOrWhiteSpace(p.Sirket) || Esit(m.Sirket, p.Sirket)) && (p.PersonelId is null || m.PersonelId == p.PersonelId))
            .ToList();
    }

    private static decimal Kalem(MaasDonemKaydi m, MaasKalemTuru t) => m.Kalemler.Where(k => k.Tur == t).Sum(k => k.Tutar);

    // -------------------------------------------------------------------------- Raporlar

    private async Task<Tablo> DonemBordroOzeti(RaporParametreleri p, CancellationToken ct)
    {
        var bordro = await BordroAsync(p, ct);
        var satirlar = bordro.GroupBy(m => (m.DonemYil, m.DonemAy, Tesis: m.Isyeri ?? TesisYok))
            .OrderBy(g => g.Key.DonemYil).ThenBy(g => g.Key.DonemAy).ThenBy(g => g.Key.Tesis)
            .Select(g => new object?[]
            {
                D(g.Key.DonemYil, g.Key.DonemAy), g.Key.Tesis, g.Select(x => x.PersonelId).Distinct().Count(),
                g.Sum(x => x.MaasTutar ?? 0), g.Sum(x => x.NetIstihkak), g.Sum(x => Kalem(x, MaasKalemTuru.AvansKesinti)),
                g.Sum(x => Kalem(x, MaasKalemTuru.Icra)), g.Sum(x => Kalem(x, MaasKalemTuru.OtoKatBes)),
                g.Sum(x => Kalem(x, MaasKalemTuru.GeriOdemeKesinti) + Kalem(x, MaasKalemTuru.TrafikCezasi) + Kalem(x, MaasKalemTuru.DigerKesinti)),
                g.Count(x => x.Gun is < 30 and > 0)
            }).ToList();
        return new Tablo(
            [new("Dönem", Genislik: 9), new("Tesis", Genislik: 22), new("Personel", RaporKolonTuru.Sayi, 9), new("Toplam maaş", RaporKolonTuru.Tutar),
             new("Net istihkak", RaporKolonTuru.Tutar), new("Avans kesintisi", RaporKolonTuru.Tutar), new("İcra", RaporKolonTuru.Tutar),
             new("BES", RaporKolonTuru.Tutar), new("Diğer kesinti", RaporKolonTuru.Tutar), new("Eksik günlü", RaporKolonTuru.Sayi, 9)],
            satirlar, Topla(satirlar, 2, 3, 4, 5, 6, 7, 8, 9),
            [("Toplam net istihkak", T(bordro.Sum(b => b.NetIstihkak))), ("Bordro satırı", bordro.Count.ToString(Tr)),
             ("Tesis sayısı", bordro.Select(b => b.Isyeri).Distinct().Count().ToString(Tr))]);
    }

    private async Task<Tablo> TesisKarsilastirma(RaporParametreleri p, List<Personel> personeller, CancellationToken ct)
    {
        var bordro = await BordroAsync(p, ct);
        var odemeler = await OdemelerAsync(p, ct);
        var donemler = await _db.MaasDonemKapanislari.AsNoTracking().ToListAsync(ct);
        var donemIdler = donemler.Where(d => DonemIcinde(d.DonemYil, d.DonemAy, p)).Select(d => d.Id).ToHashSet();
        var farklar = (await _db.MutabakatFarklari.AsNoTracking().Where(f => f.Durum == FarkDurumu.Acik).ToListAsync(ct)).Where(f => donemIdler.Contains(f.MaasDonemKapanisiId)).ToList();
        var onaylar = (await _db.DonemTesisOnaylari.AsNoTracking().ToListAsync(ct)).Where(o => donemIdler.Contains(o.MaasDonemKapanisiId)).ToList();

        var tesisler = bordro.Select(b => b.Isyeri ?? TesisYok)
            .Concat(personeller.Where(x => TesisUyar(x.Isyeri, p)).Select(x => x.Isyeri ?? TesisYok)).Distinct().OrderBy(x => x).ToList();
        var satirlar = tesisler.Select(t =>
        {
            var b = bordro.Where(x => (x.Isyeri ?? TesisYok) == t).ToList();
            var kisiler = b.Select(x => x.PersonelId).ToHashSet();
            var odenen = odemeler.Where(o => o.PersonelId.HasValue && kisiler.Contains(o.PersonelId.Value)).Sum(o => o.Tutar);
            var net = b.Sum(x => x.NetIstihkak);
            var giris = personeller.Count(x => (x.Isyeri ?? TesisYok) == t && x.CalismaDonemleri.Any(c => c.IseGirisTarihi >= p.Baslangic && c.IseGirisTarihi <= p.Bitis));
            var cikis = personeller.Count(x => (x.Isyeri ?? TesisYok) == t && x.CalismaDonemleri.Any(c => c.IstenCikisTarihi >= p.Baslangic && c.IstenCikisTarihi <= p.Bitis));
            return new object?[]
            {
                t, kisiler.Count, net, odenen, odenen - net, kisiler.Count == 0 ? 0m : Math.Round(net / b.Select(x => (x.DonemYil, x.DonemAy)).Distinct().Count() / Math.Max(1, kisiler.Count), 2),
                giris, cikis, farklar.Count(f => (f.Tesis ?? TesisYok) == t), onaylar.Count(o => o.TesisAd == t)
            };
        }).ToList();
        return new Tablo(
            [new("Tesis", Genislik: 22), new("Personel", RaporKolonTuru.Sayi, 9), new("Bordro net", RaporKolonTuru.Tutar), new("Ödenen", RaporKolonTuru.Tutar),
             new("Fark", RaporKolonTuru.Tutar), new("Kişi başı aylık net", RaporKolonTuru.Tutar), new("Giriş", RaporKolonTuru.Sayi, 7),
             new("Çıkış", RaporKolonTuru.Sayi, 7), new("Açık fark", RaporKolonTuru.Sayi, 9), new("Onaylı dönem", RaporKolonTuru.Sayi, 9)],
            satirlar, Topla(satirlar, 1, 2, 3, 4, 6, 7, 8, 9),
            [("Tesis sayısı", tesisler.Count.ToString(Tr)), ("Toplam bordro net", T(bordro.Sum(x => x.NetIstihkak))), ("Toplam ödenen", T(odemeler.Sum(o => o.Tutar)))]);
    }

    private async Task<Tablo> MaliyetTrendi(RaporParametreleri p, CancellationToken ct)
    {
        var bordro = await BordroAsync(p, ct);
        var donemler = Donemler(p).ToList();
        var kolonlar = new List<RaporKolonu> { new("Tesis", Genislik: 22) };
        kolonlar.AddRange(donemler.Select(d => new RaporKolonu($"{d:MM.yy}", RaporKolonTuru.Tutar, 12)));
        kolonlar.Add(new("Toplam", RaporKolonTuru.Tutar));
        var satirlar = bordro.GroupBy(b => b.Isyeri ?? TesisYok).OrderBy(g => g.Key).Select(g =>
        {
            var row = new List<object?> { g.Key };
            row.AddRange(donemler.Select(d => (object?)g.Where(x => x.DonemYil == d.Year && x.DonemAy == d.Month).Sum(x => x.NetIstihkak)));
            row.Add(g.Sum(x => x.NetIstihkak));
            return row.ToArray();
        }).ToList();
        var ilk = bordro.Where(b => b.DonemYil == donemler[0].Year && b.DonemAy == donemler[0].Month).Sum(b => b.NetIstihkak);
        var son = bordro.Where(b => b.DonemYil == donemler[^1].Year && b.DonemAy == donemler[^1].Month).Sum(b => b.NetIstihkak);
        return new Tablo(kolonlar, satirlar, Topla(satirlar, Enumerable.Range(1, donemler.Count + 1).ToArray()),
            [("İlk dönem", T(ilk)), ("Son dönem", T(son)), ("Değişim", ilk == 0 ? "—" : (son / ilk - 1).ToString("P1", Tr))]);
    }

    private static Tablo TesisPersonelListesi(IEnumerable<Personel> personeller)
    {
        var satirlar = personeller.OrderBy(x => x.Isyeri).ThenBy(x => x.AdSoyad).Select(x => new object?[]
        {
            x.Isyeri ?? TesisYok, x.AdSoyad, x.TcKimlikNo, x.Gorevi, x.CalismaDurumu == CalismaDurumu.Aktif ? "Aktif" : x.CalismaDurumu == CalismaDurumu.IstenAyrildi ? "İşten ayrıldı" : "Askıda",
            x.CalismaDonemleri.OrderByDescending(c => c.IseGirisTarihi).FirstOrDefault()?.IseGirisTarihi, x.SonCikisTarihi, x.GuncelMaas, x.Iban
        }).ToList();
        return new Tablo(
            [new("Tesis", Genislik: 20), new("Ad soyad", Genislik: 22), new("TC", Genislik: 12), new("Görevi", Genislik: 18), new("Durum", Genislik: 12),
             new("Son giriş", RaporKolonTuru.Tarih, 11), new("Son çıkış", RaporKolonTuru.Tarih, 11), new("Güncel maaş", RaporKolonTuru.Tutar), new("IBAN", Genislik: 28)],
            satirlar, null,
            [("Personel", satirlar.Count.ToString(Tr)), ("Aktif", satirlar.Count(s => (string?)s[4] == "Aktif").ToString(Tr))]);
    }

    private async Task<Tablo> Yaslandirma(Dictionary<Guid, Personel> kapsam, CancellationToken ct)
    {
        var hareketler = await _db.MaliHareketler.AsNoTracking().ToListAsync(ct);
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        var satirlar = new List<object?[]>();
        foreach (var g in hareketler.Where(h => kapsam.ContainsKey(h.PersonelId)).GroupBy(h => h.PersonelId))
        {
            decimal[] kova = new decimal[4];
            var enEski = (DateOnly?)null;
            foreach (var d in MaliHareketKurallari.DonemBakiyeleri(g).Where(d => d.Fark < -0.005m && d.DonemYil.HasValue))
            {
                var sonGun = new DateOnly(d.DonemYil!.Value, d.DonemAy!.Value, 1).AddMonths(1).AddDays(-1);
                var gun = bugun.DayNumber - sonGun.DayNumber;
                kova[gun <= 30 ? 0 : gun <= 60 ? 1 : gun <= 90 ? 2 : 3] += -d.Fark;
                enEski = enEski is null || sonGun < enEski ? sonGun : enEski;
            }
            if (kova.Sum() <= 0.005m) continue;
            var x = kapsam[g.Key];
            satirlar.Add([x.AdSoyad, x.TcKimlikNo, x.Isyeri ?? TesisYok, kova[0], kova[1], kova[2], kova[3], kova.Sum(), enEski]);
        }
        satirlar = satirlar.OrderByDescending(s => (decimal)s[7]!).ToList();
        return new Tablo(
            [new("Personel", Genislik: 22), new("TC", Genislik: 12), new("Tesis", Genislik: 18), new("0-30 gün", RaporKolonTuru.Tutar), new("31-60 gün", RaporKolonTuru.Tutar),
             new("61-90 gün", RaporKolonTuru.Tutar), new("90+ gün", RaporKolonTuru.Tutar), new("Toplam", RaporKolonTuru.Tutar), new("En eski dönem sonu", RaporKolonTuru.Tarih, 12)],
            satirlar, Topla(satirlar, 3, 4, 5, 6, 7),
            [("Ödenmemiş toplam", T(satirlar.Sum(s => (decimal)s[7]!))), ("90 günü aşan", T(satirlar.Sum(s => (decimal)s[6]!))), ("Personel", satirlar.Count.ToString(Tr))]);
    }

    private async Task<List<BankaOdemeKaydi>> OdemelerAsync(RaporParametreleri p, CancellationToken ct, bool iptallerDahil = false)
    {
        var q = _db.BankaOdemeleri.AsNoTracking();
        if (iptallerDahil) q = q.IgnoreQueryFilters();
        return (await q.Where(o => o.HakEdisDonemYil != null).ToListAsync(ct))
            .Where(o => DonemIcinde(o.HakEdisDonemYil!.Value, o.HakEdisDonemAy!.Value, p)).ToList();
    }

    private async Task<Tablo> OdemeDokumu(RaporParametreleri p, Dictionary<Guid, Personel> kapsam, CancellationToken ct)
    {
        var odemeler = (await OdemelerAsync(p, ct, true))
            .Where(o => (o.PersonelId.HasValue && kapsam.ContainsKey(o.PersonelId.Value)) || (o.PersonelId is null && string.IsNullOrWhiteSpace(p.Tesis)))
            .OrderBy(o => o.OdemeTarihi).ToList();
        var satirlar = odemeler.Select(o =>
        {
            kapsam.TryGetValue(o.PersonelId ?? Guid.Empty, out var x);
            return new object?[]
            {
                o.OdemeTarihi, D(o.HakEdisDonemYil!.Value, o.HakEdisDonemAy!.Value), x?.AdSoyad ?? o.AdSoyad ?? "Eşleşmeyen", x?.Isyeri ?? TesisYok,
                o.OdemeKanali == OdemeKanali.Kasa ? "Kasa" : "Banka", o.Iban, o.ReferansNo, o.IptalEdildi ? 0m : o.Tutar,
                o.IptalEdildi ? $"İPTAL: {o.IptalNedeni}" : "", o.KullaniciAdi
            };
        }).ToList();
        return new Tablo(
            [new("Ödeme tarihi", RaporKolonTuru.Tarih, 11), new("Hak ediş", Genislik: 9), new("Personel", Genislik: 22), new("Tesis", Genislik: 16), new("Kanal", Genislik: 7),
             new("IBAN", Genislik: 28), new("Referans", Genislik: 14), new("Tutar", RaporKolonTuru.Tutar), new("İptal", Genislik: 24), new("Kullanıcı", Genislik: 10)],
            satirlar, Topla(satirlar, 7),
            [("Banka", T(odemeler.Where(o => !o.IptalEdildi && o.OdemeKanali == OdemeKanali.Banka).Sum(o => o.Tutar))),
             ("Kasa", T(odemeler.Where(o => !o.IptalEdildi && o.OdemeKanali == OdemeKanali.Kasa).Sum(o => o.Tutar))),
             ("İptal edilen", odemeler.Count(o => o.IptalEdildi).ToString(Tr))]);
    }

    private async Task<Tablo> Istisnalar(RaporParametreleri p, CancellationToken ct)
    {
        var oturumlar = (await _db.OdemeKontrolOturumlari.AsNoTracking().Include(o => o.Satirlar).ToListAsync(ct))
            .Where(o => DonemIcinde(o.DonemYil, o.DonemAy, p) && (string.IsNullOrWhiteSpace(p.Tesis) || o.Tesis is null || Esit(o.Tesis, p.Tesis))).ToList();
        var satirlar = oturumlar.SelectMany(o => o.Satirlar.Where(s => s.IstisnaOnayi).Select(s => new object?[]
        {
            D(o.DonemYil, o.DonemAy), o.Tesis ?? "Tümü", s.AdSoyad, s.TcKimlikNo, s.Sonuc.ToString(), s.BordroNetIstihkak, s.BankaOdemeTutari,
            s.IstisnaGerekcesi, s.IstisnaOnaylayan, s.IstisnaOnayZamani?.ToLocalTime()
        })).ToList();
        return new Tablo(
            [new("Dönem", Genislik: 9), new("Tesis", Genislik: 14), new("Personel", Genislik: 20), new("TC", Genislik: 12), new("Kontrol sonucu", Genislik: 18),
             new("Bordro", RaporKolonTuru.Tutar), new("Banka", RaporKolonTuru.Tutar), new("Gerekçe", Genislik: 36), new("Onaylayan", Genislik: 10), new("Zaman", RaporKolonTuru.Tarih, 16)],
            satirlar, null, [("İstisna sayısı", satirlar.Count.ToString(Tr))]);
    }

    private async Task<Tablo> Farklar(RaporParametreleri p, CancellationToken ct)
    {
        var farklar = (await _db.MutabakatFarklari.AsNoTracking().ToListAsync(ct))
            .Where(f => DonemIcinde(f.DonemYil, f.DonemAy, p) && TesisUyar(f.Tesis, p)).OrderBy(f => f.DonemYil).ThenBy(f => f.DonemAy).ThenBy(f => f.AdSoyad).ToList();
        var satirlar = farklar.Select(f => new object?[]
        {
            D(f.DonemYil, f.DonemAy), f.Tesis ?? TesisYok, f.AdSoyad ?? "Eşleşmeyen", f.FarkTuru.ToString(), f.Beklenen, f.Odenen, f.Fark,
            f.Durum switch { FarkDurumu.Acik => "Açık", FarkDurumu.Cozuldu => "Çözüldü", _ => "İstisna" }, f.CozumAciklamasi, f.Cozen
        }).ToList();
        return new Tablo(
            [new("Dönem", Genislik: 9), new("Tesis", Genislik: 16), new("Personel", Genislik: 20), new("Fark türü", Genislik: 18), new("Beklenen", RaporKolonTuru.Tutar),
             new("Ödenen", RaporKolonTuru.Tutar), new("Fark", RaporKolonTuru.Tutar), new("Durum", Genislik: 9), new("Çözüm", Genislik: 30), new("Çözen", Genislik: 10)],
            satirlar, Topla(satirlar, 4, 5, 6),
            [("Açık", farklar.Count(f => f.Durum == FarkDurumu.Acik).ToString(Tr)), ("Çözüldü", farklar.Count(f => f.Durum == FarkDurumu.Cozuldu).ToString(Tr)),
             ("İstisna", farklar.Count(f => f.Durum == FarkDurumu.Istisna).ToString(Tr))]);
    }

    private async Task<Tablo> Kapanislar(RaporParametreleri p, CancellationToken ct)
    {
        var donemler = (await _db.MaasDonemKapanislari.AsNoTracking().ToListAsync(ct)).Where(d => DonemIcinde(d.DonemYil, d.DonemAy, p)).ToDictionary(d => d.Id);
        var ozetler = (await _db.DonemKapanisOzetleri.AsNoTracking().ToListAsync(ct)).Where(o => donemler.ContainsKey(o.MaasDonemKapanisiId))
            .OrderBy(o => donemler[o.MaasDonemKapanisiId].DonemYil).ThenBy(o => donemler[o.MaasDonemKapanisiId].DonemAy).ThenBy(o => o.KapanisNo).ToList();
        var satirlar = ozetler.Select(o =>
        {
            var d = donemler[o.MaasDonemKapanisiId];
            return new object?[]
            {
                D(d.DonemYil, d.DonemAy), o.KapanisNo, o.PersonelSayisi, o.ToplamBeklenenMaas, o.ToplamBankaOdemesi, o.ToplamEksikOdeme, o.ToplamFazlaOdeme,
                o.AcikAvanslar, o.CikisSonrasiSupheli, o.GerekceliIstisnaSayisi, o.KapatanKullanici, o.KapanisZamani.ToLocalTime()
            };
        }).ToList();
        return new Tablo(
            [new("Dönem", Genislik: 9), new("Kapanış #", RaporKolonTuru.Sayi, 8), new("Personel", RaporKolonTuru.Sayi, 8), new("Beklenen", RaporKolonTuru.Tutar),
             new("Ödenen", RaporKolonTuru.Tutar), new("Eksik", RaporKolonTuru.Tutar), new("Fazla", RaporKolonTuru.Tutar), new("Açık avans", RaporKolonTuru.Tutar),
             new("Çıkış sonrası", RaporKolonTuru.Tutar), new("İstisna", RaporKolonTuru.Sayi, 7), new("Kapatan", Genislik: 10), new("Zaman", RaporKolonTuru.Tarih, 16)],
            satirlar, null, [("Kapanış sayısı", satirlar.Count.ToString(Tr)), ("Açık dönem", donemler.Values.Count(d => d.Durum != MaasDonemDurumu.Kapatildi).ToString(Tr))]);
    }

    private async Task<Tablo> DenetimIzi(RaporParametreleri p, List<Personel> personeller, CancellationToken ct)
    {
        var bas = p.Baslangic.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var bit = p.Bitis.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var kayitlar = await _db.DenetimKayitlari.AsNoTracking().Where(d => d.IslemZamani >= bas && d.IslemZamani < bit)
            .OrderByDescending(d => d.IslemZamani).Take(20000).ToListAsync(ct);
        var adlar = personeller.ToDictionary(x => x.Id, x => x.AdSoyad);
        var satirlar = kayitlar.Where(d => p.PersonelId is null || d.PersonelId == p.PersonelId).Select(d => new object?[]
        {
            d.IslemZamani.ToLocalTime(), d.KullaniciAdi, d.Islem, d.VarlikTuru, d.PersonelId is { } id && adlar.TryGetValue(id, out var a) ? a : null,
            d.AlanAdi, d.EskiDeger, d.YeniDeger, d.DegisiklikNedeni
        }).ToList();
        return new Tablo(
            [new("Zaman", RaporKolonTuru.Tarih, 16), new("Kullanıcı", Genislik: 10), new("İşlem", Genislik: 18), new("Varlık", Genislik: 16), new("Personel", Genislik: 18),
             new("Alan", Genislik: 14), new("Eski", Genislik: 18), new("Yeni", Genislik: 24), new("Neden", Genislik: 24)],
            satirlar, null,
            [("Kayıt", satirlar.Count.ToString(Tr)), ("Kullanıcı", kayitlar.Select(k => k.KullaniciAdi).Distinct().Count().ToString(Tr))]);
    }

    private async Task<Tablo> Avanslar(Dictionary<Guid, Personel> kapsam, CancellationToken ct)
    {
        var gecmis = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
        var avanslar = (await _db.Avanslar.AsNoTracking().Include(a => a.Mahsuplar).ToListAsync(ct)).Where(a => kapsam.ContainsKey(a.PersonelId)).OrderBy(a => a.AvansTarihi).ToList();
        var satirlar = avanslar.Select(a => new object?[]
        {
            kapsam[a.PersonelId].AdSoyad, kapsam[a.PersonelId].Isyeri ?? TesisYok, a.AvansTarihi, a.AvansTuru, a.Tutar, a.TaksitSayisi,
            a.Mahsuplar.Sum(m => m.GerceklesenTutar ?? 0), a.KalanBorc,
            a.Mahsuplar.Count(m => m.GerceklesenTutar is null && new DateOnly(m.DonemYil, m.DonemAy, 1) <= gecmis && a.KalanBorc > 0)
        }).ToList();
        return new Tablo(
            [new("Personel", Genislik: 22), new("Tesis", Genislik: 16), new("Tarih", RaporKolonTuru.Tarih, 11), new("Tür", Genislik: 16), new("Tutar", RaporKolonTuru.Tutar),
             new("Taksit", RaporKolonTuru.Sayi, 7), new("Mahsup edilen", RaporKolonTuru.Tutar), new("Kalan", RaporKolonTuru.Tutar), new("Geciken taksit", RaporKolonTuru.Sayi, 9)],
            satirlar, Topla(satirlar, 4, 6, 7, 8),
            [("Açık avans", T(avanslar.Sum(a => a.KalanBorc))), ("Avans sayısı", avanslar.Count.ToString(Tr))]);
    }

    private async Task<Tablo> FazlaOdeme(Dictionary<Guid, Personel> kapsam, CancellationToken ct)
    {
        var h = await _db.MaliHareketler.AsNoTracking().Where(x => x.BakiyeTuru == BakiyeTuru.SupheliFazlaOdeme || x.BakiyeTuru == BakiyeTuru.KesinlesmisGeriOdeme).ToListAsync(ct);
        var planlar = await _db.TahsilatPlanlari.AsNoTracking().ToListAsync(ct);
        var satirlar = h.Where(x => kapsam.ContainsKey(x.PersonelId)).GroupBy(x => x.PersonelId).Select(g =>
        {
            var supheli = g.Where(x => x.BakiyeTuru == BakiyeTuru.SupheliFazlaOdeme).Sum(MaliHareketKurallari.Etki);
            var tespit = g.Where(x => x.BakiyeTuru == BakiyeTuru.SupheliFazlaOdeme && x.IslemTuru == MaliIslemTuru.FazlaOdemeFarki).Sum(MaliHareketKurallari.Etki);
            var kesin = g.Where(x => x.BakiyeTuru == BakiyeTuru.KesinlesmisGeriOdeme).Sum(MaliHareketKurallari.Etki);
            var tahsil = -g.Where(x => x.BakiyeTuru == BakiyeTuru.KesinlesmisGeriOdeme && x.IslemTuru == MaliIslemTuru.IadeAlinan).Sum(MaliHareketKurallari.Etki);
            var planKalan = planlar.Where(p => p.PersonelId == g.Key).Sum(p => p.KalanBorc);
            return new object?[] { kapsam[g.Key].AdSoyad, kapsam[g.Key].Isyeri ?? TesisYok, tespit, supheli, kesin + tahsil, tahsil, kesin, planKalan, kesin - planKalan };
        }).Where(r => r.Skip(2).Any(v => v is decimal d && d != 0)).ToList();
        return new Tablo(
            [new("Personel", Genislik: 22), new("Tesis", Genislik: 16), new("Tespit edilen", RaporKolonTuru.Tutar), new("İnceleme bekleyen", RaporKolonTuru.Tutar),
             new("Kesinleşen", RaporKolonTuru.Tutar), new("Tahsil edilen", RaporKolonTuru.Tutar), new("Kalan borç", RaporKolonTuru.Tutar), new("Planlı", RaporKolonTuru.Tutar),
             new("Plansız", RaporKolonTuru.Tutar)],
            satirlar, Topla(satirlar, 2, 3, 4, 5, 6, 7, 8),
            [("İnceleme bekleyen", T(satirlar.Sum(s => (decimal)s[3]!))), ("Kalan geri ödeme", T(satirlar.Sum(s => (decimal)s[6]!)))]);
    }

    private async Task<Tablo> Bakiyeler(Dictionary<Guid, Personel> kapsam, CancellationToken ct)
    {
        var h = (await _db.MaliHareketler.AsNoTracking().ToListAsync(ct)).Where(x => kapsam.ContainsKey(x.PersonelId)).GroupBy(x => x.PersonelId).ToDictionary(g => g.Key, g => g.ToList());
        var satirlar = kapsam.Values.OrderBy(x => x.Isyeri).ThenBy(x => x.AdSoyad).Select(x =>
        {
            var o = MaliHareketKurallari.OzetHesapla(h.GetValueOrDefault(x.Id) ?? new List<MaliHareket>());
            return new object?[] { x.AdSoyad, x.TcKimlikNo, x.Isyeri ?? TesisYok, o.ToplamHakEdis, o.GerceklesenToplamOdeme, o.OdenmemisHakEdis,
                o.AcikAvansBorcu, o.IncelemeBekleyenFazlaOdeme, o.KesinlesmisGeriOdemeBorcu };
        }).ToList();
        return new Tablo(
            [new("Personel", Genislik: 22), new("TC", Genislik: 12), new("Tesis", Genislik: 16), new("Hak ediş", RaporKolonTuru.Tutar), new("Ödenen", RaporKolonTuru.Tutar),
             new("Ödenmemiş", RaporKolonTuru.Tutar), new("Açık avans", RaporKolonTuru.Tutar), new("Şüpheli fazla", RaporKolonTuru.Tutar), new("Kesin geri ödeme", RaporKolonTuru.Tutar)],
            satirlar, Topla(satirlar, 3, 4, 5, 6, 7, 8),
            [("Ödenmemiş toplam", T(satirlar.Sum(s => (decimal)s[5]!))), ("Açık avans toplam", T(satirlar.Sum(s => (decimal)s[6]!)))]);
    }

    private async Task<Tablo> Kesintiler(RaporParametreleri p, CancellationToken ct)
    {
        var bordro = (await BordroAsync(p, ct)).Where(b => b.Kalemler.Any(k => k.KesintiMi)).ToList();
        var adlar = await _db.Personeller.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.AdSoyad, ct);
        var satirlar = bordro.OrderBy(b => b.DonemYil).ThenBy(b => b.DonemAy).ThenBy(b => adlar.GetValueOrDefault(b.PersonelId)).Select(b => new object?[]
        {
            D(b.DonemYil, b.DonemAy), adlar.GetValueOrDefault(b.PersonelId), b.Isyeri ?? TesisYok, Kalem(b, MaasKalemTuru.Icra), Kalem(b, MaasKalemTuru.OtoKatBes),
            Kalem(b, MaasKalemTuru.AvansKesinti), Kalem(b, MaasKalemTuru.GeriOdemeKesinti), Kalem(b, MaasKalemTuru.TrafikCezasi) + Kalem(b, MaasKalemTuru.DigerKesinti),
            b.Kalemler.Where(k => k.KesintiMi).Sum(k => k.Tutar)
        }).ToList();
        return new Tablo(
            [new("Dönem", Genislik: 9), new("Personel", Genislik: 22), new("Tesis", Genislik: 16), new("İcra", RaporKolonTuru.Tutar), new("BES", RaporKolonTuru.Tutar),
             new("Avans", RaporKolonTuru.Tutar), new("Fazla ödeme", RaporKolonTuru.Tutar), new("Diğer", RaporKolonTuru.Tutar), new("Toplam", RaporKolonTuru.Tutar)],
            satirlar, Topla(satirlar, 3, 4, 5, 6, 7, 8), [("Toplam kesinti", T(satirlar.Sum(s => (decimal)s[8]!)))]);
    }

    private async Task<Tablo> Icra(Dictionary<Guid, Personel> kapsam, CancellationToken ct)
    {
        var dosyalar = (await _db.IcraDosyalari.AsNoTracking().ToListAsync(ct)).Where(d => kapsam.ContainsKey(d.PersonelId)).OrderBy(d => kapsam[d.PersonelId].AdSoyad).ThenBy(d => d.Sira).ToList();
        var satirlar = dosyalar.Select(d => new object?[]
        {
            kapsam[d.PersonelId].AdSoyad, kapsam[d.PersonelId].Isyeri ?? TesisYok, d.DosyaNo, d.IcraDairesi, d.Alacakli, d.Sira, d.ToplamBorc, d.Kesilen,
            Math.Max(0, d.ToplamBorc - d.Kesilen), d.KesintiOrani, d.Aktif ? "Aktif" : "Kapandı"
        }).ToList();
        return new Tablo(
            [new("Personel", Genislik: 20), new("Tesis", Genislik: 14), new("Dosya no", Genislik: 12), new("İcra dairesi", Genislik: 18), new("Alacaklı", Genislik: 16),
             new("Sıra", RaporKolonTuru.Sayi, 5), new("Borç", RaporKolonTuru.Tutar), new("Kesilen", RaporKolonTuru.Tutar), new("Kalan", RaporKolonTuru.Tutar),
             new("Oran", RaporKolonTuru.Yuzde, 7), new("Durum", Genislik: 8)],
            satirlar, Topla(satirlar, 6, 7, 8), [("Aktif dosya", dosyalar.Count(d => d.Aktif).ToString(Tr))]);
    }

    private static Tablo GirisCikis(RaporParametreleri p, IEnumerable<Personel> personeller)
    {
        var liste = personeller.ToList();
        var satirlar = new List<object?[]>();
        foreach (var tesis in liste.Select(x => x.Isyeri ?? TesisYok).Distinct().OrderBy(x => x))
        {
            var ps = liste.Where(x => (x.Isyeri ?? TesisYok) == tesis).ToList();
            foreach (var d in Donemler(p))
            {
                var sonu = d.AddMonths(1).AddDays(-1);
                bool Calisiyor(Personel x, DateOnly t) => x.CalismaDonemleri.Any(c => c.IseGirisTarihi <= t && (c.IstenCikisTarihi is null || c.IstenCikisTarihi >= t));
                var bas = ps.Count(x => Calisiyor(x, d));
                var son = ps.Count(x => Calisiyor(x, sonu));
                var giris = ps.Sum(x => x.CalismaDonemleri.Count(c => c.IseGirisTarihi >= d && c.IseGirisTarihi <= sonu));
                var cikis = ps.Sum(x => x.CalismaDonemleri.Count(c => c.IstenCikisTarihi >= d && c.IstenCikisTarihi <= sonu));
                var ort = (bas + son) / 2m;
                satirlar.Add([tesis, $"{d:yyyy-MM}", bas, giris, cikis, son, ort == 0 ? 0m : Math.Round(cikis / ort, 4)]);
            }
        }
        return new Tablo(
            [new("Tesis", Genislik: 20), new("Dönem", Genislik: 9), new("Dönem başı", RaporKolonTuru.Sayi, 9), new("Giriş", RaporKolonTuru.Sayi, 7),
             new("Çıkış", RaporKolonTuru.Sayi, 7), new("Dönem sonu", RaporKolonTuru.Sayi, 9), new("Devir hızı", RaporKolonTuru.Yuzde, 9)],
            satirlar, null,
            [("Toplam giriş", satirlar.Sum(s => (int)s[3]!).ToString(Tr)), ("Toplam çıkış", satirlar.Sum(s => (int)s[4]!).ToString(Tr))]);
    }

    private async Task<Tablo> Sgk(RaporParametreleri p, Dictionary<Guid, Personel> kapsam, CancellationToken ct)
    {
        var cikislar = (await _db.IstenCikisKayitlari.AsNoTracking().ToListAsync(ct))
            .Where(c => kapsam.ContainsKey(c.PersonelId) && c.IstenCikisTarihi >= p.Baslangic && c.IstenCikisTarihi <= p.Bitis).OrderBy(c => c.IstenCikisTarihi).ToList();
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        var satirlar = cikislar.Select(c => new object?[]
        {
            kapsam[c.PersonelId].AdSoyad, kapsam[c.PersonelId].Isyeri ?? TesisYok, c.IstenCikisTarihi, c.CikisNedeni,
            c.SgkCikisDurumu switch { SgkCikisDurumu.Verildi => "Verildi", SgkCikisDurumu.Verilmedi => "Verilmedi", _ => "Belirsiz" },
            c.SgkBildirgeTarihi, c.SgkCikisDurumu == SgkCikisDurumu.Verildi ? null : bugun.DayNumber - c.IstenCikisTarihi.DayNumber, c.SgkOnaylayanKullanici
        }).ToList();
        return new Tablo(
            [new("Personel", Genislik: 22), new("Tesis", Genislik: 16), new("Çıkış tarihi", RaporKolonTuru.Tarih, 11), new("Neden", Genislik: 18), new("SGK", Genislik: 9),
             new("Bildirge tarihi", RaporKolonTuru.Tarih, 11), new("Bekleyen gün", RaporKolonTuru.Sayi, 9), new("Onaylayan", Genislik: 10)],
            satirlar, null,
            [("Çıkış", cikislar.Count.ToString(Tr)), ("SGK bekleyen", cikislar.Count(c => c.SgkCikisDurumu != SgkCikisDurumu.Verildi).ToString(Tr))]);
    }

    private async Task<Tablo> CikisTakibi(RaporParametreleri p, Dictionary<Guid, Personel> kapsam, CancellationToken ct)
    {
        var liste = (await CikisTalimatServisi.HesaplaAsync(_db, new CikisTakipFiltre { Baslangic = p.Baslangic }, DateOnly.FromDateTime(DateTime.Today), ct))
            .Where(s => kapsam.ContainsKey(s.PersonelId)).ToList();
        string Durum(CikisTakipDurumu d) => d switch
        {
            CikisTakipDurumu.Gecikti => "GECİKTİ", CikisTakipDurumu.Bekliyor => "Bekliyor", CikisTakipDurumu.CikisYapildi => "Çıkış yapıldı", _ => "İptal"
        };
        var satirlar = liste.Select(s => new object?[]
        {
            s.AdSoyad, s.Tesis ?? TesisYok, s.Kaynak, s.SonCalismaGunu, s.Neden, s.TalepEden, Durum(s.Durum), s.GecikmeGun == 0 ? null : s.GecikmeGun,
            s.CikisKaydiTarihi, s.CikisiYapan, s.Odemeler.Count, s.SonGunSonrasiOdenen, s.TahminiFazlaOdeme, s.SonrakiBordroSayisi
        }).ToList();
        return new Tablo(
            [new("Personel", Genislik: 22), new("Tesis", Genislik: 14), new("Kaynak", Genislik: 12), new("Son çalışma günü", RaporKolonTuru.Tarih, 11),
             new("Neden", Genislik: 16), new("Talimatı veren", Genislik: 10), new("Durum", Genislik: 11), new("Gecikme (gün)", RaporKolonTuru.Sayi, 8),
             new("Çıkış kaydı", RaporKolonTuru.Tarih, 11), new("Çıkışı yapan", Genislik: 10), new("Ödeme adedi", RaporKolonTuru.Sayi, 7),
             new("Son gün sonrası ödenen", RaporKolonTuru.Tutar, 13), new("Tahmini fazla ödeme", RaporKolonTuru.Tutar, 13), new("Sonraki bordro", RaporKolonTuru.Sayi, 8)],
            satirlar, Topla(satirlar, 10, 11, 12, 13),
            [("Geciken talimat", liste.Count(s => s.Durum == CikisTakipDurumu.Gecikti).ToString(Tr)),
             ("Bekleyen", liste.Count(s => s.Durum == CikisTakipDurumu.Bekliyor).ToString(Tr)),
             ("Tahmini fazla ödeme", liste.Sum(s => s.TahminiFazlaOdeme).ToString("N2", Tr))]);
    }

    private async Task<Tablo> MaasDegisiklikleri(RaporParametreleri p, Dictionary<Guid, Personel> kapsam, CancellationToken ct)
    {
        var bas = p.Baslangic.ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var bit = p.Bitis.AddDays(1).ToDateTime(TimeOnly.MinValue).ToUniversalTime();
        var kayitlar = (await _db.DenetimKayitlari.AsNoTracking().Where(d => d.AlanAdi == nameof(Personel.GuncelMaas) && d.IslemZamani >= bas && d.IslemZamani < bit).ToListAsync(ct))
            .Where(d => d.PersonelId.HasValue && kapsam.ContainsKey(d.PersonelId.Value)).OrderBy(d => d.IslemZamani).ToList();
        decimal? Sayi(string? s) => decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) || decimal.TryParse(s, NumberStyles.Number, Tr, out d) ? d : null;
        var satirlar = kayitlar.Select(d =>
        {
            var eski = Sayi(d.EskiDeger);
            var yeni = Sayi(d.YeniDeger);
            return new object?[]
            {
                d.IslemZamani.ToLocalTime(), kapsam[d.PersonelId!.Value].AdSoyad, kapsam[d.PersonelId.Value].Isyeri ?? TesisYok, eski, yeni,
                eski.HasValue && yeni.HasValue ? yeni - eski : null, eski is > 0 && yeni.HasValue ? (yeni - eski) / eski : null, d.DegisiklikNedeni, d.KullaniciAdi
            };
        }).ToList();
        return new Tablo(
            [new("Zaman", RaporKolonTuru.Tarih, 16), new("Personel", Genislik: 22), new("Tesis", Genislik: 16), new("Eski maaş", RaporKolonTuru.Tutar), new("Yeni maaş", RaporKolonTuru.Tutar),
             new("Fark", RaporKolonTuru.Tutar), new("Artış", RaporKolonTuru.Yuzde, 8), new("Gerekçe", Genislik: 24), new("Kullanıcı", Genislik: 10)],
            satirlar, null, [("Değişiklik", satirlar.Count.ToString(Tr))]);
    }

    // -------------------------------------------------------------------------- Yardımcılar

    private sealed record Tablo(List<RaporKolonu> Kolonlar, List<object?[]> Satirlar, object?[]? Toplam, List<(string, string)> Ozet);

    private static object?[]? Topla(List<object?[]> satirlar, params int[] kolonlar)
    {
        if (satirlar.Count == 0) return null;
        var genislik = satirlar[0].Length;
        var t = new object?[genislik];
        t[0] = "TOPLAM";
        foreach (var k in kolonlar)
            t[k] = satirlar.Sum(r => r[k] switch { decimal d => d, int i => i, null => 0m, _ => 0m });
        return t;
    }

    private static string T(decimal d) => d.ToString("N2", Tr);

    public string DosyaAdi(RaporSonucu rapor, RaporBicimi bicim)
    {
        var ad = new string(rapor.Baslik.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        return $"{ad}-{rapor.OlusturmaZamani:yyyyMMdd-HHmm}.{bicim switch { RaporBicimi.Pdf => "pdf", RaporBicimi.Csv => "csv", _ => "xlsx" }}";
    }

    public byte[] DisaAktar(RaporSonucu rapor, RaporBicimi bicim) => bicim switch
    {
        RaporBicimi.Pdf => Pdf(rapor),
        RaporBicimi.Csv => Csv(rapor),
        _ => Excel(rapor)
    };

    private static string Metin(object? v, RaporKolonTuru tur) => v switch
    {
        null => "",
        decimal d when tur == RaporKolonTuru.Yuzde => d.ToString("P1", Tr),
        decimal d => d.ToString("N2", Tr),
        DateOnly d => d.ToString("dd.MM.yyyy", Tr),
        DateTime d => d.ToString("dd.MM.yyyy HH:mm", Tr),
        int i => i.ToString("N0", Tr),
        _ => v.ToString() ?? ""
    };

    private static byte[] Excel(RaporSonucu r)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(r.Baslik.Length > 31 ? r.Baslik[..31] : r.Baslik);
        ws.Cell(1, 1).Value = $"Personel Yönetim Sistemi — {r.Baslik}";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = r.Aciklama;
        ws.Cell(3, 1).Value = $"{r.ParametreMetni} | Oluşturma: {r.OlusturmaZamani:dd.MM.yyyy HH:mm}";
        var satir = 4;
        if (r.Ozet.Count > 0)
        {
            satir++;
            for (var i = 0; i < r.Ozet.Count; i++)
            {
                ws.Cell(satir, 1 + i * 2).Value = r.Ozet[i].Ad;
                ws.Cell(satir, 2 + i * 2).Value = r.Ozet[i].Deger;
                ws.Cell(satir, 2 + i * 2).Style.Font.Bold = true;
            }
            satir++;
        }
        satir++;
        var baslik = satir;
        for (var c = 0; c < r.Kolonlar.Count; c++)
            ws.Cell(satir, c + 1).Value = r.Kolonlar[c].Baslik;
        var hr = ws.Range(satir, 1, satir, r.Kolonlar.Count);
        hr.Style.Font.Bold = true;
        hr.Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");
        foreach (var row in r.Satirlar.Concat(r.Toplam is null ? [] : [r.Toplam]))
        {
            satir++;
            for (var c = 0; c < r.Kolonlar.Count && c < row.Length; c++)
            {
                var cell = ws.Cell(satir, c + 1);
                switch (row[c])
                {
                    case null: break;
                    case decimal d: cell.Value = d; cell.Style.NumberFormat.Format = r.Kolonlar[c].Tur == RaporKolonTuru.Yuzde ? "0.0%" : "#,##0.00"; break;
                    case int i: cell.Value = i; break;
                    case DateOnly d: cell.Value = d.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "dd.MM.yyyy"; break;
                    case DateTime d: cell.Value = d; cell.Style.DateFormat.Format = "dd.MM.yyyy HH:mm"; break;
                    default: cell.Value = row[c]!.ToString(); break;
                }
            }
        }
        if (r.Toplam is not null) ws.Row(satir).Style.Font.Bold = true;
        if (r.Satirlar.Count > 0) ws.Range(baslik, 1, satir - (r.Toplam is null ? 0 : 1), r.Kolonlar.Count).SetAutoFilter();
        ws.SheetView.FreezeRows(baslik);
        for (var c = 0; c < r.Kolonlar.Count; c++) ws.Column(c + 1).Width = Math.Max(10, r.Kolonlar[c].Genislik + 2);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] Csv(RaporSonucu r)
    {
        // Türkçe Excel için noktalı virgül ayırıcı ve UTF-8 BOM
        static string K(string s) => s.Contains(';') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", r.Kolonlar.Select(k => K(k.Baslik))));
        foreach (var row in r.Satirlar.Concat(r.Toplam is null ? [] : [r.Toplam]))
            sb.AppendLine(string.Join(";", r.Kolonlar.Select((k, i) => K(i < row.Length ? Metin(row[i], k.Tur) : ""))));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static byte[] Pdf(RaporSonucu r)
    {
        var pdf = new BasitPdfYazici();
        pdf.Baslik($"Personel Yönetim Sistemi — {r.Baslik}");
        pdf.Satir(r.Aciklama);
        pdf.Satir($"{r.ParametreMetni}   |   Oluşturma: {r.OlusturmaZamani:dd.MM.yyyy HH:mm}");
        if (r.Ozet.Count > 0) pdf.Satir(string.Join("     ", r.Ozet.Select(o => $"{o.Ad}: {o.Deger}")), 8.5f, true);
        pdf.Bosluk();
        // Kolon genişliklerini sayfaya sığdır
        var toplamGenislik = r.Kolonlar.Sum(k => k.Genislik);
        var sigan = (int)(pdf.KullanilabilirGenislik / (7f * 0.55f));
        var oran = toplamGenislik > sigan ? (double)sigan / toplamGenislik : 1;
        var kolonlar = r.Kolonlar.Select(k => (k.Baslik, Math.Max(5, (int)(k.Genislik * oran)), k.Tur is RaporKolonTuru.Tutar or RaporKolonTuru.Sayi or RaporKolonTuru.Yuzde)).ToList();
        pdf.Tablo(kolonlar, r.Satirlar.Concat(r.Toplam is null ? [] : [r.Toplam]).Select(row => r.Kolonlar.Select((k, i) => i < row.Length ? Metin(row[i], k.Tur) : "").ToArray()));
        pdf.Bosluk();
        pdf.Satir($"{r.Satirlar.Count} satır.");
        return pdf.Olustur();
    }
}

public sealed class GostergeServisi : IGostergeServisi
{
    private readonly IPayControlDbContext _db;
    private readonly IUyariServisi _uyari;

    public GostergeServisi(IPayControlDbContext db, IUyariServisi uyari)
    {
        _db = db;
        _uyari = uyari;
    }

    public async Task<GostergeVerisi> GetirAsync(int aySayisi = 12, string? tesis = null, CancellationToken ct = default)
    {
        var son = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        var ilk = son.AddMonths(-(aySayisi - 1));
        var bas = ilk.Year * 100 + ilk.Month;
        var bordro = (await _db.MaasDonemKayitlari.AsNoTracking().Where(m => m.DonemYil * 100 + m.DonemAy >= bas)
                .Select(m => new { m.DonemYil, m.DonemAy, m.PersonelId, m.Isyeri, m.NetIstihkak }).ToListAsync(ct))
            .Where(m => tesis is null || string.Equals(m.Isyeri, tesis, StringComparison.CurrentCultureIgnoreCase)).ToList();
        var kisiler = bordro.Select(b => b.PersonelId).ToHashSet();
        var odemeler = (await _db.BankaOdemeleri.AsNoTracking().Where(o => o.HakEdisDonemYil != null && o.HakEdisDonemYil * 100 + o.HakEdisDonemAy >= bas)
                .Select(o => new { o.HakEdisDonemYil, o.HakEdisDonemAy, o.PersonelId, o.Tutar }).ToListAsync(ct))
            .Where(o => tesis is null || (o.PersonelId.HasValue && kisiler.Contains(o.PersonelId.Value))).ToList();

        var aylar = new List<AylikGosterge>();
        for (var d = ilk; d <= son; d = d.AddMonths(1))
        {
            var b = bordro.Where(x => x.DonemYil == d.Year && x.DonemAy == d.Month).ToList();
            var o = odemeler.Where(x => x.HakEdisDonemYil == d.Year && x.HakEdisDonemAy == d.Month).Sum(x => x.Tutar);
            aylar.Add(new AylikGosterge(d.Year, d.Month, b.Sum(x => x.NetIstihkak), o, b.Select(x => x.PersonelId).Distinct().Count(), o - b.Sum(x => x.NetIstihkak)));
        }

        var sonVeri = aylar.LastOrDefault(a => a.BordroNet > 0);
        var tesisler = sonVeri is null ? new List<TesisGosterge>() : bordro.Where(b => b.DonemYil == sonVeri.Yil && b.DonemAy == sonVeri.Ay)
            .GroupBy(b => b.Isyeri ?? "(Tesis belirtilmemiş)")
            .Select(g => new TesisGosterge(g.Key, g.Select(x => x.PersonelId).Distinct().Count(), g.Sum(x => x.NetIstihkak), sonVeri.BordroNet == 0 ? 0 : g.Sum(x => x.NetIstihkak) / sonVeri.BordroNet))
            .OrderByDescending(t => t.BordroNet).ToList();

        var uyarilar = await _uyari.ListeleAsync(tesis, ct);
        return new GostergeVerisi
        {
            Aylar = aylar,
            Tesisler = tesisler,
            KritikUyari = uyarilar.Count(u => u.Seviye == UyariSeviyesi.Kritik),
            ToplamUyari = uyarilar.Count
        };
    }
}
