using PayControl.Domain.Services;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Application.Services;

/// <summary>
/// "Bu kişiyi çıkar" talimatlarının takibi. Amaç: İK çıkışı unutursa personele maaş yatmaya devam etmesini
/// erken yakalamak — takip listesi, ödeme öncesi kontrolde kritik bulgu, uyarı merkezi ve günlük hatırlatma.
/// </summary>
public sealed class CikisTalimatServisi : ICikisTalimatServisi
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly IPayControlDbContext _db;
    private readonly IYetkiServisi _yetki;
    private readonly IServiceProvider? _sp;

    public CikisTalimatServisi(IPayControlDbContext db, IYetkiServisi yetki, IServiceProvider? sp = null)
    {
        _db = db;
        _yetki = yetki;
        _sp = sp;
    }

    public async Task<CikisTalimati> OlusturAsync(CikisTalimatiOlusturDto dto, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Çıkış talimatı", ct);
        if (string.IsNullOrWhiteSpace(dto.Neden)) throw new InvalidOperationException("Çıkış nedeni zorunludur.");
        var p = await _db.Personeller.Include(x => x.CalismaDonemleri).FirstOrDefaultAsync(x => x.Id == dto.PersonelId, ct)
                ?? throw new InvalidOperationException("Personel bulunamadı.");
        if (p.CalismaDurumu == CalismaDurumu.IstenAyrildi)
            throw new InvalidOperationException($"{p.AdSoyad} zaten işten ayrılmış görünüyor (son çıkış {p.SonCikisTarihi:dd.MM.yyyy}).");
        var aktif = p.CalismaDonemleri.FirstOrDefault(d => d.Durum == CalismaDurumu.Aktif);
        if (aktif is not null && dto.SonCalismaGunu < aktif.IseGirisTarihi)
            throw new InvalidOperationException("Son çalışma günü işe giriş tarihinden önce olamaz.");
        if (await _db.CikisTalimatlari.AnyAsync(t => t.PersonelId == p.Id && t.Durum == CikisTalimatDurumu.Bekliyor, ct))
            throw new InvalidOperationException($"{p.AdSoyad} için zaten bekleyen bir çıkış talimatı var.");

        string? sorumlu = null;
        if (dto.SorumluAliciId is { } aid)
            sorumlu = (await _db.BildirimAlicilari.AsNoTracking().FirstOrDefaultAsync(a => a.Id == aid, ct))?.AdSoyad
                      ?? throw new InvalidOperationException("Seçilen sorumlu bulunamadı.");

        var t = new CikisTalimati
        {
            PersonelId = p.Id, SonCalismaGunu = dto.SonCalismaGunu, Neden = dto.Neden.Trim(), Aciklama = dto.Aciklama?.Trim(),
            SorumluAliciId = dto.SorumluAliciId, Sorumlu = sorumlu, TalepEden = kullanici
        };
        _db.CikisTalimatlari.Add(t);
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = p.Id, VarlikTuru = nameof(CikisTalimati), VarlikId = t.Id, Islem = "CikisTalimatiVerildi",
            YeniDeger = $"Son çalışma günü {dto.SonCalismaGunu:yyyy-MM-dd}" + (sorumlu is null ? "" : $"; sorumlu {sorumlu}"),
            DegisiklikNedeni = t.Neden, KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);

        var konu = $"Personel Yönetim Sistemi: {p.AdSoyad} için çıkış talimatı (son gün {dto.SonCalismaGunu:dd.MM.yyyy})";
        var metin = $"{p.AdSoyad} ({p.TcKimlikNo}, {p.Isyeri}) için işten çıkış yapılması istendi.\n" +
                    $"Son çalışma günü: {dto.SonCalismaGunu:dd.MM.yyyy}\nNeden: {t.Neden}\nTalimatı veren: {kullanici}\n" +
                    (string.IsNullOrWhiteSpace(t.Aciklama) ? "" : $"Not: {t.Aciklama}\n") +
                    "Lütfen SGK çıkışını yapıp Personel Yönetim Sistemi'de işten çıkış kaydını girin. Çıkış kaydı girilene kadar ödeme kontrolü bu kişiyi engeller.";
        await BildirAsync(BildirimTetikleyici.CikisTalimatiVerildi, t.Id.ToString("N"), konu, metin, p.Isyeri, t.SorumluAliciId, ct);
        return t;
    }

    public async Task IptalEtAsync(Guid talimatId, string neden, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Çıkış talimatı iptali", ct);
        if (string.IsNullOrWhiteSpace(neden)) throw new InvalidOperationException("İptal nedeni zorunludur.");
        var t = await _db.CikisTalimatlari.FirstOrDefaultAsync(x => x.Id == talimatId, ct) ?? throw new InvalidOperationException("Talimat bulunamadı.");
        if (t.Durum != CikisTalimatDurumu.Bekliyor) throw new InvalidOperationException("Yalnız bekleyen talimat iptal edilebilir.");
        t.Durum = CikisTalimatDurumu.IptalEdildi;
        t.IptalNedeni = neden.Trim();
        t.TamamlanmaZamani = DateTime.UtcNow;
        t.TamamlayanKullanici = kullanici;
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = t.PersonelId, VarlikTuru = nameof(CikisTalimati), VarlikId = t.Id, Islem = "CikisTalimatiIptal",
            DegisiklikNedeni = t.IptalNedeni, KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Çıkış kaydı girildiğinde bekleyen talimatı kapatır (SaveChanges çağırmaz).</summary>
    public static async Task TalimatiTamamlaAsync(IPayControlDbContext db, IstenCikisKaydi cikis, string kullanici, CancellationToken ct)
    {
        var t = await db.CikisTalimatlari.FirstOrDefaultAsync(x => x.PersonelId == cikis.PersonelId && x.Durum == CikisTalimatDurumu.Bekliyor, ct);
        if (t is null) return;
        t.Durum = CikisTalimatDurumu.Tamamlandi;
        t.IstenCikisKaydiId = cikis.Id;
        t.TamamlanmaZamani = DateTime.UtcNow;
        t.TamamlayanKullanici = kullanici;
        if (cikis.FiiliSonCalismaTarihi > t.SonCalismaGunu)
            db.DenetimKayitlari.Add(new DenetimKaydi
            {
                PersonelId = t.PersonelId, VarlikTuru = nameof(CikisTalimati), VarlikId = t.Id, Islem = "Uyari", AlanAdi = "SonCalismaGunu",
                EskiDeger = t.SonCalismaGunu.ToString("yyyy-MM-dd"), YeniDeger = cikis.FiiliSonCalismaTarihi.ToString("yyyy-MM-dd"),
                DegisiklikNedeni = "Çıkış kaydındaki fiilî son gün, talimattaki son günden sonra.", KullaniciAdi = kullanici
            });
    }

    public Task<IReadOnlyList<CikisTakipDto>> ListeleAsync(CikisTakipFiltre? filtre = null, CancellationToken ct = default) =>
        HesaplaAsync(_db, filtre ?? new CikisTakipFiltre(), DateOnly.FromDateTime(DateTime.Today), ct);

    /// <summary>Takip listesinin hesaplanması (uyarı merkezi ve rapor da kullanır).</summary>
    public static async Task<IReadOnlyList<CikisTakipDto>> HesaplaAsync(IPayControlDbContext db, CikisTakipFiltre f, DateOnly bugun, CancellationToken ct)
    {
        var talimatlar = await db.CikisTalimatlari.AsNoTracking().ToListAsync(ct);
        var cikislar = await db.IstenCikisKayitlari.AsNoTracking().ToListAsync(ct);
        var ilgili = talimatlar.Select(t => t.PersonelId).Concat(f.DogrudanCikislariDahilEt ? cikislar.Select(c => c.PersonelId) : []).ToHashSet();
        if (ilgili.Count == 0) return Array.Empty<CikisTakipDto>();

        var personeller = await db.Personeller.AsNoTracking().Where(p => ilgili.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var odemeler = (await db.BankaOdemeleri.AsNoTracking().Where(o => o.PersonelId != null && ilgili.Contains(o.PersonelId.Value)).ToListAsync(ct))
            .ToLookup(o => o.PersonelId!.Value);
        var bordrolar = (await db.MaasDonemKayitlari.AsNoTracking().Where(b => ilgili.Contains(b.PersonelId)).ToListAsync(ct)).ToLookup(b => b.PersonelId);
        var cikisById = cikislar.ToDictionary(c => c.Id);

        var sonuc = new List<CikisTakipDto>();
        var talimatliCikislar = new HashSet<Guid>();
        foreach (var t in talimatlar)
        {
            if (!personeller.TryGetValue(t.PersonelId, out var p)) continue;
            IstenCikisKaydi? c = t.IstenCikisKaydiId is { } cid && cikisById.TryGetValue(cid, out var cc) ? cc : null;
            if (c is not null) talimatliCikislar.Add(c.Id);
            var durum = t.Durum switch
            {
                CikisTalimatDurumu.Tamamlandi => CikisTakipDurumu.CikisYapildi,
                CikisTalimatDurumu.IptalEdildi => CikisTakipDurumu.IptalEdildi,
                _ => t.SonCalismaGunu < bugun ? CikisTakipDurumu.Gecikti : CikisTakipDurumu.Bekliyor
            };
            sonuc.Add(Satir(p, t.SonCalismaGunu, durum, odemeler[p.Id], bordrolar[p.Id], bugun, t, c));
        }
        if (f.DogrudanCikislariDahilEt)
            foreach (var c in cikislar.Where(c => !talimatliCikislar.Contains(c.Id)))
                if (personeller.TryGetValue(c.PersonelId, out var p))
                    sonuc.Add(Satir(p, c.FiiliSonCalismaTarihi, CikisTakipDurumu.CikisYapildi, odemeler[p.Id], bordrolar[p.Id], bugun, null, c));

        IEnumerable<CikisTakipDto> q = sonuc;
        if (f.YalnizSorunlu) q = q.Where(s => s.Durum == CikisTakipDurumu.Gecikti || s.TahminiFazlaOdeme > 0 || s.SonrakiBordroSayisi > 0);
        if (!string.IsNullOrWhiteSpace(f.Tesis)) q = q.Where(s => string.Equals(s.Tesis, f.Tesis, StringComparison.CurrentCultureIgnoreCase));
        if (!string.IsNullOrWhiteSpace(f.Metin))
            q = q.Where(s => AramaKurallari.Eslesir(f.Metin, s.AdSoyad, s.TcKimlikNo, s.Tesis, s.Neden, s.Sorumlu));
        if (f.Baslangic is { } bas) q = q.Where(s => s.Durum is CikisTakipDurumu.Bekliyor or CikisTakipDurumu.Gecikti || s.SonCalismaGunu >= bas);
        return q.OrderBy(s => s.Durum switch { CikisTakipDurumu.Gecikti => 0, CikisTakipDurumu.Bekliyor => 1, CikisTakipDurumu.CikisYapildi => 2, _ => 3 })
            .ThenByDescending(s => s.TahminiFazlaOdeme).ThenByDescending(s => s.SonCalismaGunu).ToList();
    }

    private static CikisTakipDto Satir(Personel p, DateOnly sonGun, CikisTakipDurumu durum, IEnumerable<BankaOdemeKaydi> odemeler,
        IEnumerable<MaasDonemKaydi> bordrolar, DateOnly bugun, CikisTalimati? t, IstenCikisKaydi? c)
    {
        var cikisAyi = new DateOnly(sonGun.Year, sonGun.Month, 1);
        var bordroListesi = bordrolar.ToList();
        var cikisAyiBordro = bordroListesi.Where(b => b.DonemYil == sonGun.Year && b.DonemAy == sonGun.Month).OrderByDescending(b => b.OlusturmaZamani).FirstOrDefault();
        var maas = cikisAyiBordro?.MaasTutar ?? p.GuncelMaas;
        var gun = Math.Min(30, sonGun.Day);
        // Tahmin: bordro netinin günlere oranı; bordro yoksa kart maaşının 30'da biri × gün
        var tahmin = cikisAyiBordro is { NetIstihkak: > 0 } bb && bb.Gun is > 0
            ? Math.Round(bb.NetIstihkak / bb.Gun.Value * Math.Min(gun, bb.Gun.Value), 2)
            : Math.Round((maas ?? 0m) / 30m * gun, 2);
        if (cikisAyiBordro is { NetIstihkak: > 0 } b2 && tahmin > b2.NetIstihkak) tahmin = b2.NetIstihkak;

        var satirlar = new List<CikisSonrasiOdemeDto>();
        var cikisAyiOdenen = 0m;
        foreach (var o in odemeler.OrderBy(o => o.OdemeTarihi))
        {
            DateOnly? donem = o.HakEdisDonemYil is { } y && o.HakEdisDonemAy is { } a ? new DateOnly(y, a, 1) : null;
            string deger;
            decimal hakEdilmeyen;
            if (donem is { } d && d > cikisAyi)
            {
                deger = $"Son çalışma gününden ({sonGun:dd.MM.yyyy}) sonraki ayın ücreti — çalışmadığı dönem için ödenmiş.";
                hakEdilmeyen = o.Tutar;
            }
            else if (donem is { } d2 && d2 == cikisAyi)
            {
                cikisAyiOdenen += o.Tutar;
                var asim = Math.Max(0, cikisAyiOdenen - tahmin) - Math.Max(0, cikisAyiOdenen - o.Tutar - tahmin);
                hakEdilmeyen = asim;
                deger = asim > 0
                    ? $"Çıkış ayı ücreti: {gun} günlük tahmini hak ediş {tahmin:N2} TL; {asim:N2} TL fazlası çalışılmayan günler için."
                    : $"Çıkış ayı ücreti ({gun} gün) — hak ediş içinde.";
            }
            else if (donem is null && o.OdemeTarihi > sonGun)
            {
                deger = "Son günden sonra yapılan, hak ediş dönemi belirtilmemiş ödeme — açıklamasını kontrol edin.";
                hakEdilmeyen = 0;
            }
            else if (o.OdemeTarihi > sonGun)
            {
                deger = "Önceki dönemin ücreti, son günden sonra ödenmiş (normal).";
                hakEdilmeyen = 0;
            }
            else continue;
            satirlar.Add(new CikisSonrasiOdemeDto
            {
                OdemeId = o.Id, OdemeTarihi = o.OdemeTarihi, Tutar = o.Tutar,
                HakEdisDonemi = donem is { } dd ? dd.ToString("yyyy-MM", Tr) : "—",
                Kanal = o.OdemeKanali == OdemeKanali.Kasa ? "Kasa" : "Banka", Referans = o.ReferansNo, Aciklama = o.Aciklama,
                Degerlendirme = deger, HakEdilmeyen = hakEdilmeyen
            });
        }

        return new CikisTakipDto
        {
            TalimatId = t?.Id, PersonelId = p.Id, AdSoyad = p.AdSoyad, TcKimlikNo = p.TcKimlikNo, Tesis = p.Isyeri, Maas = maas,
            Kaynak = t is null ? "Doğrudan çıkış kaydı" : "Talimat",
            SonCalismaGunu = sonGun, Neden = t?.Neden ?? c?.CikisNedeni, TalepEden = t?.TalepEden, TalepZamani = t?.TalepZamani, Sorumlu = t?.Sorumlu,
            Durum = durum,
            GecikmeGun = durum == CikisTakipDurumu.Gecikti ? bugun.DayNumber - sonGun.DayNumber : 0,
            CikisKaydiTarihi = c?.IstenCikisTarihi, CikisiYapan = c?.OlusturanKullanici, CikisKaydiZamani = c?.OlusturmaZamani,
            SgkDurumu = c is null ? null : c.SgkCikisDurumu switch { SgkCikisDurumu.Verildi => "Verildi", SgkCikisDurumu.Verilmedi => "Verilmedi", _ => "Belirsiz" },
            CikisAyiTahminiHakEdis = tahmin,
            SonGunSonrasiOdenen = satirlar.Sum(s => s.Tutar),
            TahminiFazlaOdeme = satirlar.Sum(s => s.HakEdilmeyen),
            SonrakiBordroSayisi = durum == CikisTakipDurumu.IptalEdildi ? 0 : bordroListesi.Count(b => new DateOnly(b.DonemYil, b.DonemAy, 1) > cikisAyi && b.NetIstihkak > 0),
            Odemeler = satirlar
        };
    }

    public async Task<int> GecikenleriHatirlatAsync(DateTime simdi, CancellationToken ct = default)
    {
        var bugun = DateOnly.FromDateTime(simdi);
        var gecikenler = await _db.CikisTalimatlari.Include(t => t.Personel)
            .Where(t => t.Durum == CikisTalimatDurumu.Bekliyor && t.SonCalismaGunu < bugun).ToListAsync(ct);
        var n = 0;
        foreach (var t in gecikenler.Where(t => t.SonHatirlatma is null || DateOnly.FromDateTime(t.SonHatirlatma.Value.ToLocalTime()) < bugun))
        {
            var p = t.Personel!;
            var gun = bugun.DayNumber - t.SonCalismaGunu.DayNumber;
            await BildirAsync(BildirimTetikleyici.CikisTalimatiGecikti, $"{t.Id:N}:{bugun:yyyyMMdd}",
                $"HATIRLATMA: {p.AdSoyad} çıkışı {gun} gündür yapılmadı",
                $"{p.AdSoyad} ({p.TcKimlikNo}, {p.Isyeri}) için son çalışma günü {t.SonCalismaGunu:dd.MM.yyyy} olarak verilen çıkış talimatı " +
                $"{gun} gündür uygulanmadı.\nTalimatı veren: {t.TalepEden} ({t.TalepZamani.ToLocalTime():dd.MM.yyyy})\nNeden: {t.Neden}\n" +
                "Çıkış kaydı yapılmadıkça personele çalışmadığı günler için maaş ödenme riski vardır. Lütfen bugün SGK çıkışını yapıp Personel Yönetim Sistemi'e girin.",
                p.Isyeri, t.SorumluAliciId, ct);
            t.SonHatirlatma = simdi.Kind == DateTimeKind.Utc ? simdi : simdi.ToUniversalTime();
            t.HatirlatmaSayisi++;
            n++;
        }
        if (n > 0) await _db.SaveChangesAsync(ct);
        return n;
    }

    /// <summary>Kurallara göre olay bildirimi + seçilen sorumluya doğrudan gönderim. Bildirim hatası asıl işlemi bozmaz.</summary>
    private async Task BildirAsync(BildirimTetikleyici olay, string anahtar, string konu, string metin, string? tesis, Guid? sorumluAliciId, CancellationToken ct)
    {
        try
        {
            if (_sp?.GetService(typeof(IBildirimServisi)) is not IBildirimServisi b) return;
            await b.OlayBildirAsync(olay, anahtar, konu, metin, tesis, ct);
            if (sorumluAliciId is { } id)
                await b.KisiyeBildirAsync(id, $"{olay}:{anahtar}", konu, metin, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Trace.TraceWarning($"Çıkış talimatı bildirimi gönderilemedi: {ex.Message}");
        }
    }
}
