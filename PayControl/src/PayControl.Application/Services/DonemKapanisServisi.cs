using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class DonemKapanisServisi : IDonemKapanisServisi
{
    private const decimal Tolerans = 0.01m;
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private readonly IPayControlDbContext _db;
    private readonly IYetkiServisi _yetki;
    private readonly IMaliHareketServisi _defter;

    public DonemKapanisServisi(IPayControlDbContext db, IYetkiServisi yetki, IMaliHareketServisi defter)
    {
        _db = db;
        _yetki = yetki;
        _defter = defter;
    }

    public async Task<MaasDonemKapanisi> GetOrCreateAsync(int yil, int ay, string? sirket = null, CancellationToken ct = default)
    {
        if (ay is < 1 or > 12) throw new InvalidOperationException("Dönem ayı 1-12 arasında olmalıdır.");
        var mevcut = await _db.MaasDonemKapanislari
            .Include(d => d.KontrolMaddeleri)
            .Include(d => d.Duzeltmeler)
            .FirstOrDefaultAsync(d => d.DonemYil == yil && d.DonemAy == ay && d.Sirket == sirket, ct);

        if (mevcut is not null)
            return mevcut;

        var donem = DonemKapanisKurallari.YeniDonem(yil, ay, sirket);
        _db.MaasDonemKapanislari.Add(donem);
        await _db.SaveChangesAsync(ct);
        return donem;
    }

    public async Task EnsureDonemAcikAsync(int yil, int ay, CancellationToken ct = default)
    {
        var donem = await _db.MaasDonemKapanislari.AsNoTracking()
            .FirstOrDefaultAsync(d => d.DonemYil == yil && d.DonemAy == ay && d.Durum == MaasDonemDurumu.Kapatildi, ct);
        DonemKapanisKurallari.DogrudanDegisikligiEngelle(donem);
    }

    public async Task DurumGuncelleAsync(Guid donemId, MaasDonemDurumu durum, string kullanici, CancellationToken ct = default)
    {
        var donem = await DonemAsync(donemId, ct);
        if (donem.Durum == MaasDonemDurumu.Kapatildi)
            throw new InvalidOperationException("Kapatılmış dönem durumunu değiştirmek için yeniden açma kullanın.");
        if (durum == MaasDonemDurumu.Kapatildi)
            throw new InvalidOperationException("Dönem yalnızca kapanış işlemiyle kapatılabilir.");
        if (!DonemKapanisKurallari.DurumGecisiGecerliMi(donem.Durum, durum))
            throw new InvalidOperationException($"Geçersiz durum geçişi: {donem.Durum} → {durum}.");
        DurumYaz(donem, durum, kullanici, null);
        await _db.SaveChangesAsync(ct);
    }

    private void DurumYaz(MaasDonemKapanisi donem, MaasDonemDurumu durum, string kullanici, string? neden)
    {
        if (donem.Durum == durum) return;
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(MaasDonemKapanisi),
            VarlikId = donem.Id,
            Islem = "DurumGuncelleme",
            AlanAdi = "Durum",
            EskiDeger = donem.Durum.ToString(),
            YeniDeger = durum.ToString(),
            DegisiklikNedeni = neden,
            KullaniciAdi = kullanici
        });
        donem.Durum = durum;
    }

    // ------------------------------------------------------------------ Kontrol listesi

    public async Task<IReadOnlyList<DonemKontrolMaddesi>> KontrolListesiDegerlendirAsync(Guid donemId, CancellationToken ct = default)
    {
        var donem = await _db.MaasDonemKapanislari.Include(d => d.KontrolMaddeleri)
            .FirstOrDefaultAsync(d => d.Id == donemId, ct) ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (donem.Durum == MaasDonemDurumu.Kapatildi)
            return donem.KontrolMaddeleri.OrderBy(m => m.Sira).ToList();

        var veri = await KontrolVerisiAsync(donem, ct);
        foreach (var m in donem.KontrolMaddeleri)
        {
            var (sonuc, aciklama) = MaddeKontrolu(m.MaddeKodu, donem, veri);
            m.OtomatikSonuc = sonuc;
            m.OtomatikAciklama = aciklama;
        }
        await _db.SaveChangesAsync(ct);
        return donem.KontrolMaddeleri.OrderBy(m => m.Sira).ToList();
    }

    public async Task MaddeTamamlaAsync(Guid maddeId, string kullanici, string? not = null, CancellationToken ct = default)
    {
        var madde = await _db.DonemKontrolMaddeleri
            .Include(m => m.MaasDonemKapanisi).ThenInclude(d => d!.KontrolMaddeleri)
            .FirstOrDefaultAsync(m => m.Id == maddeId, ct)
            ?? throw new InvalidOperationException("Kontrol maddesi bulunamadı.");
        var donem = madde.MaasDonemKapanisi!;
        DonemKapanisKurallari.DogrudanDegisikligiEngelle(donem);
        await _yetki.DogrulaAsync(kullanici, madde.MaddeKodu == "YONETICI" ? KullaniciRolu.Yonetici : KullaniciRolu.Muhasebe,
            $"Kontrol maddesi: {madde.Aciklama}", ct);

        if (madde.MaddeKodu == "YONETICI")
        {
            var eksik = donem.KontrolMaddeleri.Where(m => m.MaddeKodu != "YONETICI" && !m.Tamamlandi).ToList();
            if (eksik.Count > 0)
                throw new InvalidOperationException("Yönetici onayı için önce diğer maddeler tamamlanmalı: " +
                                                    string.Join(", ", eksik.Select(m => m.MaddeKodu)));
        }

        var veri = await KontrolVerisiAsync(donem, ct);
        var (sonuc, aciklama) = MaddeKontrolu(madde.MaddeKodu, donem, veri);
        madde.OtomatikSonuc = sonuc;
        madde.OtomatikAciklama = aciklama;

        if (sonuc == false)
        {
            if (string.IsNullOrWhiteSpace(not) || not.Trim().Length < 10)
                throw new InvalidOperationException(
                    $"Otomatik kontrol başarısız: {aciklama} Maddeyi elle onaylamak için en az 10 karakterlik gerekçe girin.");
            madde.ElleOnaylandi = true;
        }

        madde.Tamamlandi = true;
        madde.TamamlanmaZamani = DateTime.UtcNow;
        madde.TamamlayanKullanici = kullanici;
        madde.Not = not;

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(DonemKontrolMaddesi),
            VarlikId = madde.Id,
            Islem = madde.ElleOnaylandi ? "KontrolMaddesiElleOnay" : "KontrolMaddesiTamamlandi",
            AlanAdi = madde.MaddeKodu,
            YeniDeger = aciklama,
            DegisiklikNedeni = not,
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
    }

    private sealed class KontrolVerisi
    {
        public List<MaasDonemKaydi> Bordro { get; init; } = new();
        public List<Personel> Personeller { get; init; } = new();
        public List<IstenCikisKaydi> Cikislar { get; init; } = new();
        public List<AvansKaydi> Avanslar { get; init; } = new();
        public List<BankaOdemeKaydi> Odemeler { get; init; } = new();
        public bool GecerliListeVar { get; init; }
        public int AcikFark { get; init; }
        public DateOnly Basi { get; init; }
        public DateOnly Sonu { get; init; }
    }

    private async Task<KontrolVerisi> KontrolVerisiAsync(MaasDonemKapanisi d, CancellationToken ct)
    {
        var basi = new DateOnly(d.DonemYil, d.DonemAy, 1);
        return new KontrolVerisi
        {
            Basi = basi,
            Sonu = basi.AddMonths(1).AddDays(-1),
            Bordro = await _db.MaasDonemKayitlari.AsNoTracking().Where(m => m.DonemYil == d.DonemYil && m.DonemAy == d.DonemAy).ToListAsync(ct),
            Personeller = await _db.Personeller.AsNoTracking().Include(p => p.CalismaDonemleri).ToListAsync(ct),
            Cikislar = await _db.IstenCikisKayitlari.AsNoTracking().ToListAsync(ct),
            Avanslar = await _db.Avanslar.AsNoTracking().Include(a => a.Mahsuplar).ToListAsync(ct),
            Odemeler = await _db.BankaOdemeleri.AsNoTracking().Where(o => o.HakEdisDonemYil == d.DonemYil && o.HakEdisDonemAy == d.DonemAy).ToListAsync(ct),
            GecerliListeVar = await _db.OnayliOdemeListeleri.AnyAsync(l => l.DonemYil == d.DonemYil && l.DonemAy == d.DonemAy && l.Gecerli, ct),
            AcikFark = await _db.MutabakatFarklari.CountAsync(f => f.MaasDonemKapanisiId == d.Id && f.Durum == FarkDurumu.Acik, ct)
        };
    }

    private static (bool? Sonuc, string Aciklama) MaddeKontrolu(string kod, MaasDonemKapanisi d, KontrolVerisi v)
    {
        bool Calisiyor(Personel p) => p.CalismaDonemleri.Any(c => c.IseGirisTarihi <= v.Sonu && (c.IstenCikisTarihi is null || c.IstenCikisTarihi >= v.Basi));
        var bordroKisileri = v.Bordro.Select(b => b.PersonelId).ToHashSet();
        string Isimler(IEnumerable<Personel> ps) => string.Join(", ", ps.Take(5).Select(p => p.AdSoyad)) + (ps.Count() > 5 ? $" (+{ps.Count() - 5})" : "");

        switch (kod)
        {
            case "PER_LISTE":
            {
                var calisanlar = v.Personeller.Where(Calisiyor).ToList();
                var bordrodaYok = calisanlar.Where(p => !bordroKisileri.Contains(p.Id)).ToList();
                var calismayan = v.Personeller.Where(p => bordroKisileri.Contains(p.Id) && p.CalismaDonemleri.Count > 0 && !Calisiyor(p)).ToList();
                if (v.Bordro.Count == 0) return (false, "Bordro yok; personel listesi karşılaştırılamadı.");
                if (bordrodaYok.Count == 0 && calismayan.Count == 0)
                    return (true, $"Dönemde çalışan {calisanlar.Count} personelin tamamı bordroda; bordroda çalışma dönemi dışı kişi yok.");
                return (false, (bordrodaYok.Count > 0 ? $"Çalıştığı halde bordroda olmayan: {Isimler(bordrodaYok)}. " : "") +
                               (calismayan.Count > 0 ? $"Çalışma dönemi dışında bordroda olan: {Isimler(calismayan)}." : ""));
            }
            case "YENI_GIRIS":
            {
                var yeniler = v.Personeller.Where(p => p.CalismaDonemleri.Any(c => c.IseGirisTarihi >= v.Basi && c.IseGirisTarihi <= v.Sonu)).ToList();
                var eksik = yeniler.Where(p => !bordroKisileri.Contains(p.Id)).ToList();
                return eksik.Count == 0
                    ? (true, $"{yeniler.Count} yeni giriş; tamamı bordroda.")
                    : (false, $"Yeni girişi olup bordroda olmayan: {Isimler(eksik)}.");
            }
            case "CIKISLAR":
            {
                var cikanlar = v.Personeller.Where(p => p.CalismaDonemleri.Any(c => c.IstenCikisTarihi >= v.Basi && c.IstenCikisTarihi <= v.Sonu)).ToList();
                var kayitsiz = cikanlar.Where(p => !v.Cikislar.Any(c => c.PersonelId == p.Id && c.IstenCikisTarihi >= v.Basi && c.IstenCikisTarihi <= v.Sonu)).ToList();
                var bordroCikis = v.Bordro.Where(b => b.CikisTarihi >= v.Basi && b.CikisTarihi <= v.Sonu)
                    .Where(b => !v.Cikislar.Any(c => c.PersonelId == b.PersonelId && c.IstenCikisTarihi >= v.Basi && c.IstenCikisTarihi <= v.Sonu))
                    .Select(b => v.Personeller.First(p => p.Id == b.PersonelId)).ToList();
                var tumu = kayitsiz.Concat(bordroCikis).DistinctBy(p => p.Id).ToList();
                return tumu.Count == 0
                    ? (true, $"{cikanlar.Count} işten çıkış; tamamının çıkış kaydı var.")
                    : (false, $"Çıkışı olup işten çıkış kaydı oluşturulmamış: {Isimler(tumu)}.");
            }
            case "SGK":
            {
                var belirsiz = v.Cikislar.Where(c => c.IstenCikisTarihi >= v.Basi && c.IstenCikisTarihi <= v.Sonu && c.SgkCikisDurumu == SgkCikisDurumu.Belirsiz)
                    .Select(c => v.Personeller.First(p => p.Id == c.PersonelId)).ToList();
                return belirsiz.Count == 0 ? (true, "Dönemdeki tüm çıkışların SGK durumu belirlenmiş.") : (false, $"SGK çıkış durumu belirsiz: {Isimler(belirsiz)}.");
            }
            case "BORDRO":
                return v.Bordro.Count > 0
                    ? (true, $"{v.Bordro.Count} bordro satırı, toplam net {v.Bordro.Sum(b => b.NetIstihkak).ToString("N2", Tr)}.")
                    : (false, "Bu dönem için aktif bordro yok.");
            case "AVANS":
            {
                var bekleyen = v.Avanslar
                    .Where(a => a.Mahsuplar.Any(m => m.DonemYil == d.DonemYil && m.DonemAy == d.DonemAy && m.PlanlananTutar > 0 && m.GerceklesenTutar is null))
                    .Where(a => a.Tutar - a.Mahsuplar.Where(m => m.GerceklesenTutar.HasValue &&
                                   (m.DonemYil < d.DonemYil || (m.DonemYil == d.DonemYil && m.DonemAy < d.DonemAy))).Sum(m => m.GerceklesenTutar!.Value) > Tolerans)
                    .Select(a => v.Personeller.First(p => p.Id == a.PersonelId)).DistinctBy(p => p.Id).ToList();
                return bekleyen.Count == 0 ? (true, "Dönem için planlanan avans mahsuplarının tamamı gerçekleşmiş.") : (false, $"Planlanan mahsubu gerçekleşmeyen: {Isimler(bekleyen)}.");
            }
            case "FARKLAR":
                return d.MutabakatZamani.HasValue
                    ? (true, $"Mutabakat {d.MutabakatZamani.Value.ToLocalTime():dd.MM.yyyy HH:mm} tarihinde yapıldı; farklar listelendi.")
                    : (false, "Eksik/fazla ödeme farkları için önce banka mutabakatı yapılmalı.");
            case "ODEME_ONAY":
                return v.GecerliListeVar ? (true, "Dönem için geçerli onaylı banka ödeme listesi var.") : (false, "Geçerli onaylı ödeme listesi yok.");
            case "BANKA_ICE":
                return v.Odemeler.Count > 0
                    ? (true, $"{v.Odemeler.Count} gerçekleşen ödeme kaydı ({v.Odemeler.Sum(o => o.Tutar).ToString("N2", Tr)}).")
                    : (false, "Gerçekleşen banka ödemesi içe aktarılmamış.");
            case "MUTABAKAT":
            {
                if (!d.MutabakatZamani.HasValue) return (false, "Mutabakat yapılmadı.");
                var sonradan = v.Odemeler.Count(o => o.OlusturmaZamani > d.MutabakatZamani);
                return sonradan == 0 ? (true, "Mutabakat güncel.") : (false, $"Mutabakattan sonra {sonradan} yeni ödeme kaydı girildi; mutabakatı tekrarlayın.");
            }
            case "ACIK_FARK":
                if (!d.MutabakatZamani.HasValue) return (false, "Mutabakat yapılmadı.");
                return v.AcikFark == 0 ? (true, "Açık mutabakat farkı yok.") : (false, $"{v.AcikFark} açık fark çözülmedi / istisna verilmedi.");
            default:
                return (null, "Bu madde için otomatik kontrol yok; yetkili onayı gerekir.");
        }
    }

    // ------------------------------------------------------------------ Mutabakat

    public async Task<IReadOnlyList<MutabakatFarki>> MutabakatYapAsync(Guid donemId, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Banka mutabakatı", ct);
        var donem = await DonemAsync(donemId, ct);
        DonemKapanisKurallari.DogrudanDegisikligiEngelle(donem);
        var yil = donem.DonemYil;
        var ay = donem.DonemAy;
        var basi = new DateOnly(yil, ay, 1);
        var sonu = basi.AddMonths(1).AddDays(-1);

        var bordro = await _db.MaasDonemKayitlari.AsNoTracking().Where(m => m.DonemYil == yil && m.DonemAy == ay).ToListAsync(ct);
        var odemeler = await _db.BankaOdemeleri.AsNoTracking().Where(o => o.HakEdisDonemYil == yil && o.HakEdisDonemAy == ay).ToListAsync(ct);
        var personeller = await _db.Personeller.AsNoTracking().Include(p => p.CalismaDonemleri).ToDictionaryAsync(p => p.Id, ct);
        var gecerliListe = await _db.OnayliOdemeListeleri.AsNoTracking().Include(l => l.Satirlar)
            .Where(l => l.DonemYil == yil && l.DonemAy == ay && l.Gecerli)
            .OrderByDescending(l => l.Surum).FirstOrDefaultAsync(ct);
        var mevcutFarklar = await _db.MutabakatFarklari.Where(f => f.MaasDonemKapanisiId == donem.Id).ToListAsync(ct);
        var mutabakatHareketleri = await _db.MaliHareketler.AsNoTracking()
            .Where(h => h.KaynakTuru == "Mutabakat" && h.DonemYil == yil && h.DonemAy == ay && h.BakiyeTuru == BakiyeTuru.SupheliFazlaOdeme)
            .ToListAsync(ct);

        var bulunan = new List<MutabakatFarki>();
        void Fark(Guid? pid, MutabakatFarkTuru tur, decimal beklenen, decimal odenen, string aciklama, string? anahtarEk = null)
        {
            personeller.TryGetValue(pid ?? Guid.Empty, out var p);
            bulunan.Add(new MutabakatFarki
            {
                MaasDonemKapanisiId = donem.Id,
                DonemYil = yil,
                DonemAy = ay,
                PersonelId = pid,
                TcKimlikNo = p?.TcKimlikNo,
                AdSoyad = p?.AdSoyad,
                Anahtar = $"{tur}:{pid?.ToString("N") ?? anahtarEk}",
                FarkTuru = tur,
                Beklenen = beklenen,
                Odenen = odenen,
                Fark = odenen - beklenen,
                Aciklama = aciklama
            });
        }

        var kisiler = bordro.Select(b => b.PersonelId)
            .Concat(odemeler.Where(o => o.PersonelId.HasValue).Select(o => o.PersonelId!.Value))
            .Concat(gecerliListe?.Satirlar.Where(s => s.PersonelId.HasValue).Select(s => s.PersonelId!.Value) ?? [])
            .Distinct().ToList();

        var supheliHedef = new Dictionary<Guid, (decimal Tutar, string Aciklama)>();
        foreach (var pid in kisiler)
        {
            var beklenen = bordro.Where(b => b.PersonelId == pid).Sum(b => b.NetIstihkak);
            var bordroVar = bordro.Any(b => b.PersonelId == pid);
            var odenen = odemeler.Where(o => o.PersonelId == pid).Sum(o => o.Tutar);
            var listede = gecerliListe?.Satirlar.Where(s => s.PersonelId == pid).Sum(s => s.Tutar);
            personeller.TryGetValue(pid, out var p);
            var calisiyor = p is null || p.CalismaDonemleri.Count == 0 ||
                            p.CalismaDonemleri.Any(c => c.IseGirisTarihi <= sonu && (c.IstenCikisTarihi is null || c.IstenCikisTarihi >= basi));

            if (!calisiyor && odenen > Tolerans)
            {
                var son = p!.CalismaDonemleri.Where(c => c.IseGirisTarihi <= sonu).MaxBy(c => c.IseGirisTarihi)?.IstenCikisTarihi;
                Fark(pid, MutabakatFarkTuru.CikisSonrasiOdeme, 0, odenen,
                    $"İşten çıkış ({son:dd.MM.yyyy}) sonrası döneme {odenen:N2} TL ödenmiş. Şüpheli fazla ödeme olarak incelemeye alındı.");
                supheliHedef[pid] = (odenen, "Çıkış sonrası döneme ödeme");
            }
            else if (!bordroVar && odenen > Tolerans)
            {
                Fark(pid, MutabakatFarkTuru.BordrosuzOdeme, 0, odenen, $"Bordroda hak edişi olmayan kişiye {odenen:N2} TL ödenmiş.");
                supheliHedef[pid] = (odenen, "Bordrosuz ödeme");
            }
            else if (odenen - beklenen > Tolerans)
            {
                Fark(pid, MutabakatFarkTuru.FazlaOdeme, beklenen, odenen, $"Hak edişten {odenen - beklenen:N2} TL fazla ödenmiş.");
                supheliHedef[pid] = (odenen - beklenen, "Hak edişi aşan ödeme");
            }
            else if (beklenen - odenen > Tolerans)
            {
                Fark(pid, MutabakatFarkTuru.EksikOdeme, beklenen, odenen,
                    odenen == 0 ? $"Hak ediş {beklenen:N2} TL hiç ödenmemiş." : $"Hak edişin {beklenen - odenen:N2} TL'si ödenmemiş.");
            }

            if (gecerliListe is not null)
            {
                if ((listede ?? 0) == 0 && odenen > Tolerans)
                    Fark(pid, MutabakatFarkTuru.OnayliListeDisiOdeme, 0, odenen,
                        $"Onaylı ödeme listesinde (v{gecerliListe.Surum}) olmayan ödeme: {odenen:N2} TL.");
                else if (listede > Tolerans && Math.Abs(odenen - listede.Value) > Tolerans)
                    Fark(pid, MutabakatFarkTuru.OnayliListedeOdenmedi, listede.Value, odenen,
                        $"Onaylı listede {listede:N2} TL, bankada gerçekleşen {odenen:N2} TL.");
            }
        }

        foreach (var o in odemeler.Where(o => o.PersonelId is null))
            Fark(null, MutabakatFarkTuru.EslesmeyenBankaHareketi, 0, o.Tutar,
                $"Personelle eşleşmeyen banka hareketi: {o.OdemeTarihi:dd.MM.yyyy} {o.AdSoyad} {o.Iban} {o.Tutar:N2} (ref {o.ReferansNo}).",
                o.Id.ToString("N"));

        // Farkları güncelle: aynı anahtar ve tutar ise çözüm durumu korunur, tutar değiştiyse yeniden açılır.
        var simdi = DateTime.UtcNow;
        foreach (var yeni in bulunan)
        {
            var eski = mevcutFarklar.FirstOrDefault(f => f.Anahtar == yeni.Anahtar);
            if (eski is null)
            {
                _db.MutabakatFarklari.Add(yeni);
                mevcutFarklar.Add(yeni);
                continue;
            }
            if (Math.Abs(eski.Beklenen - yeni.Beklenen) > Tolerans || Math.Abs(eski.Odenen - yeni.Odenen) > Tolerans)
            {
                if (eski.Durum != FarkDurumu.Acik)
                    eski.CozumAciklamasi = $"[Yeniden açıldı: tutar {eski.Odenen:N2}→{yeni.Odenen:N2}] {eski.CozumAciklamasi}";
                eski.Durum = FarkDurumu.Acik;
                eski.Beklenen = yeni.Beklenen;
                eski.Odenen = yeni.Odenen;
                eski.Fark = yeni.Fark;
                eski.Aciklama = yeni.Aciklama;
                eski.SonGuncellemeZamani = simdi;
            }
        }
        foreach (var eski in mevcutFarklar.Where(f => f.Durum == FarkDurumu.Acik && bulunan.All(b => b.Anahtar != f.Anahtar)))
        {
            eski.Durum = FarkDurumu.Cozuldu;
            eski.CozumAciklamasi = "Tekrarlanan mutabakatta fark kalmadı.";
            eski.Cozen = kullanici;
            eski.CozumZamani = simdi;
        }

        // Şüpheli fazla ödeme defterine yalnızca değişen kısım (delta) yazılır; geçmiş kayıtlar değişmez.
        var tumKisiler = supheliHedef.Keys.Concat(mutabakatHareketleri.Select(h => h.PersonelId)).Distinct();
        foreach (var pid in tumKisiler)
        {
            var hedef = supheliHedef.TryGetValue(pid, out var h) ? h.Tutar : 0m;
            var mevcut = mutabakatHareketleri.Where(x => x.PersonelId == pid).Sum(MaliHareketKurallari.Etki);
            var delta = hedef - mevcut;
            if (Math.Abs(delta) <= Tolerans) continue;
            await _defter.SupheliFazlaOdemeKaydetAsync(pid, yil, ay, delta,
                delta > 0
                    ? $"Mutabakat {yil}-{ay:D2}: {(supheliHedef.TryGetValue(pid, out var a) ? a.Aciklama : "")} — inceleme bekliyor ({delta:N2})"
                    : $"Mutabakat {yil}-{ay:D2}: fazla ödeme tekrar mutabakatta azaldı ({delta:N2})",
                "Mutabakat", donem.Id.ToString(), Guid.NewGuid().ToString("N"), kullanici, kaydet: false, ct);
        }

        donem.MutabakatZamani = simdi;
        var acik = mevcutFarklar.Count(f => f.Durum == FarkDurumu.Acik);
        if (DonemKapanisKurallari.DurumGecisiGecerliMi(donem.Durum, acik > 0 ? MaasDonemDurumu.IncelemeBekliyor : MaasDonemDurumu.KapanisaHazir))
            DurumYaz(donem, acik > 0 ? MaasDonemDurumu.IncelemeBekliyor : MaasDonemDurumu.KapanisaHazir, kullanici, "Mutabakat sonucu");

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(MaasDonemKapanisi),
            VarlikId = donem.Id,
            Islem = "BankaMutabakati",
            YeniDeger = $"{yil}-{ay:D2} | bordro {bordro.Sum(b => b.NetIstihkak):N2} | ödenen {odemeler.Sum(o => o.Tutar):N2} | fark {bulunan.Count} | açık {acik}",
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
        await OzetHesaplaAsync(donem.Id, ct);
        return mevcutFarklar.OrderBy(f => f.Durum).ThenBy(f => f.AdSoyad).ToList();
    }

    public async Task FarkCozAsync(Guid farkId, FarkDurumu durum, string aciklama, string kullanici, CancellationToken ct = default)
    {
        if (durum == FarkDurumu.Acik) throw new InvalidOperationException("Çözüm durumu 'Çözüldü' veya 'İstisna' olmalıdır.");
        await _yetki.DogrulaAsync(kullanici, durum == FarkDurumu.Istisna ? KullaniciRolu.Yonetici : KullaniciRolu.Muhasebe,
            durum == FarkDurumu.Istisna ? "Mutabakat farkı için gerekçeli istisna" : "Mutabakat farkı çözümü", ct);
        if (string.IsNullOrWhiteSpace(aciklama) || aciklama.Trim().Length < 10)
            throw new InvalidOperationException("Çözüm/istisna gerekçesi zorunludur (en az 10 karakter).");

        var fark = await _db.MutabakatFarklari.Include(f => f.MaasDonemKapanisi)
            .FirstOrDefaultAsync(f => f.Id == farkId, ct) ?? throw new InvalidOperationException("Fark bulunamadı.");
        DonemKapanisKurallari.DogrudanDegisikligiEngelle(fark.MaasDonemKapanisi);

        var eski = fark.Durum;
        fark.Durum = durum;
        fark.CozumAciklamasi = aciklama.Trim();
        fark.Cozen = kullanici;
        fark.CozumZamani = DateTime.UtcNow;
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = fark.PersonelId,
            VarlikTuru = nameof(MutabakatFarki),
            VarlikId = fark.Id,
            Islem = durum == FarkDurumu.Istisna ? "MutabakatIstisna" : "MutabakatFarkCozuldu",
            AlanAdi = fark.FarkTuru.ToString(),
            EskiDeger = eski.ToString(),
            YeniDeger = $"{durum} | fark {fark.Fark:N2}",
            DegisiklikNedeni = aciklama,
            KullaniciAdi = kullanici
        });

        var donem = fark.MaasDonemKapanisi!;
        await _db.SaveChangesAsync(ct);
        var acik = await _db.MutabakatFarklari.CountAsync(f => f.MaasDonemKapanisiId == donem.Id && f.Durum == FarkDurumu.Acik, ct);
        if (acik == 0 && donem.Durum == MaasDonemDurumu.IncelemeBekliyor)
        {
            DurumYaz(donem, MaasDonemDurumu.KapanisaHazir, kullanici, "Tüm mutabakat farkları çözüldü");
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<MutabakatFarki>> FarklarAsync(Guid donemId, CancellationToken ct = default) =>
        await _db.MutabakatFarklari.AsNoTracking()
            .Where(f => f.MaasDonemKapanisiId == donemId)
            .OrderBy(f => f.Durum).ThenBy(f => f.AdSoyad)
            .ToListAsync(ct);

    // ------------------------------------------------------------------ Özet, kapanış

    public async Task OzetHesaplaAsync(Guid donemId, CancellationToken ct = default)
    {
        var donem = await DonemAsync(donemId, ct);
        // Kapatılmış dönemin toplamları dondurulmuştur; sonradan değişen kayıtlar etkilemez.
        if (donem.Durum == MaasDonemDurumu.Kapatildi)
            return;

        var o = await CanliOzetAsync(donem, ct);
        donem.ToplamBeklenenMaas = o.Beklenen;
        donem.ToplamBankaOdemesi = o.Odenen;
        donem.ToplamFazlaOdeme = o.Fazla;
        donem.ToplamEksikOdeme = o.Eksik;
        donem.OdenmemisHakEdisler = o.Eksik;
        donem.AcikAvanslar = o.AcikAvans;
        donem.CikisSonrasiSupheli = o.CikisSonrasi;
        donem.CozulmemisUyariSayisi = o.Cozulmemis;
        donem.GerekceliIstisnaSayisi = o.Istisna;
        await _db.SaveChangesAsync(ct);
    }

    private sealed record CanliOzet(decimal Beklenen, decimal Odenen, decimal Eksik, decimal Fazla, decimal AcikAvans,
        decimal CikisSonrasi, int Cozulmemis, int Istisna, List<(Guid Pid, decimal Beklenen, decimal Odenen)> Kisiler);

    private async Task<CanliOzet> CanliOzetAsync(MaasDonemKapanisi donem, CancellationToken ct)
    {
        var yil = donem.DonemYil;
        var ay = donem.DonemAy;
        var sonu = new DateOnly(yil, ay, 1).AddMonths(1).AddDays(-1);

        var bordro = await _db.MaasDonemKayitlari.AsNoTracking().Where(m => m.DonemYil == yil && m.DonemAy == ay).ToListAsync(ct);
        var odemeler = await _db.BankaOdemeleri.AsNoTracking().Where(x => x.HakEdisDonemYil == yil && x.HakEdisDonemAy == ay).ToListAsync(ct);

        var kisiler = bordro.Select(b => b.PersonelId).Concat(odemeler.Where(x => x.PersonelId.HasValue).Select(x => x.PersonelId!.Value))
            .Distinct()
            .Select(pid => (pid, bordro.Where(b => b.PersonelId == pid).Sum(b => b.NetIstihkak), odemeler.Where(x => x.PersonelId == pid).Sum(x => x.Tutar)))
            .ToList();
        var eslesmeyen = odemeler.Where(x => x.PersonelId is null).Sum(x => x.Tutar);

        var avanslar = await _db.Avanslar.AsNoTracking().Include(a => a.Mahsuplar).Where(a => a.AvansTarihi <= sonu).ToListAsync(ct);
        var acikAvans = avanslar.Sum(a => Math.Max(0, a.Tutar - a.Mahsuplar
            .Where(m => m.GerceklesenTutar.HasValue && (m.DonemYil < yil || (m.DonemYil == yil && m.DonemAy <= ay)))
            .Sum(m => m.GerceklesenTutar!.Value)));

        var farklar = await _db.MutabakatFarklari.AsNoTracking().Where(f => f.MaasDonemKapanisiId == donem.Id).ToListAsync(ct);
        var sonOturum = await _db.OdemeKontrolOturumlari.AsNoTracking().Include(x => x.Satirlar)
            .Where(x => x.DonemYil == yil && x.DonemAy == ay).OrderByDescending(x => x.OlusturmaZamani).FirstOrDefaultAsync(ct);
        var kritikAcik = sonOturum?.Satirlar.Count(s => s.KritikMi && !s.OnayliListeyeAlinabilir) ?? 0;
        var istisnaSatir = sonOturum?.Satirlar.Count(s => s.IstisnaOnayi) ?? 0;

        return new CanliOzet(
            bordro.Sum(b => b.NetIstihkak),
            odemeler.Sum(x => x.Tutar),
            kisiler.Sum(k => Math.Max(0, k.Item2 - k.Item3)),
            kisiler.Sum(k => Math.Max(0, k.Item3 - k.Item2)) + eslesmeyen,
            acikAvans,
            farklar.Where(f => f.FarkTuru == MutabakatFarkTuru.CikisSonrasiOdeme).Sum(f => f.Odenen),
            farklar.Count(f => f.Durum == FarkDurumu.Acik) + kritikAcik,
            farklar.Count(f => f.Durum == FarkDurumu.Istisna) + istisnaSatir,
            kisiler);
    }

    public async Task KapatAsync(Guid donemId, string kullanici, string? not, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Yonetici, "Maaş dönemi kapanışı", ct);
        var donem = await _db.MaasDonemKapanislari.Include(d => d.KontrolMaddeleri)
            .FirstOrDefaultAsync(d => d.Id == donemId, ct) ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (donem.Durum == MaasDonemDurumu.Kapatildi)
            throw new InvalidOperationException("Dönem zaten kapatılmış.");

        if (donem.MutabakatZamani.HasValue &&
            await _db.BankaOdemeleri.AnyAsync(o => o.HakEdisDonemYil == donem.DonemYil && o.HakEdisDonemAy == donem.DonemAy &&
                                                   o.OlusturmaZamani > donem.MutabakatZamani, ct))
            throw new InvalidOperationException("Mutabakattan sonra yeni ödeme kaydı girildi. Kapanıştan önce mutabakatı tekrarlayın.");

        var o = await CanliOzetAsync(donem, ct);
        var eksikMaddeler = donem.KontrolMaddeleri.Where(m => !m.Tamamlandi).Select(m => m.MaddeKodu).ToList();
        if (eksikMaddeler.Count > 0)
            throw new InvalidOperationException($"Kontrol listesi tamamlanmadan dönem kapatılamaz. Eksik: {string.Join(", ", eksikMaddeler)}.");

        DonemKapanisKurallari.Kapat(donem, kullanici, not, true, o.Cozulmemis);
        donem.ToplamBeklenenMaas = o.Beklenen;
        donem.ToplamBankaOdemesi = o.Odenen;
        donem.ToplamFazlaOdeme = o.Fazla;
        donem.ToplamEksikOdeme = o.Eksik;
        donem.OdenmemisHakEdisler = o.Eksik;
        donem.AcikAvanslar = o.AcikAvans;
        donem.CikisSonrasiSupheli = o.CikisSonrasi;
        donem.GerekceliIstisnaSayisi = o.Istisna;

        // Kapanış anlık görüntüsü: kapatılan dönemin raporu sonradan değişen kartlardan etkilenmez.
        var personeller = await _db.Personeller.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var bordro = await _db.MaasDonemKayitlari.AsNoTracking()
            .Where(m => m.DonemYil == donem.DonemYil && m.DonemAy == donem.DonemAy).ToListAsync(ct);
        foreach (var (pid, beklenen, odenen) in o.Kisiler)
        {
            var p = personeller[pid];
            var b = bordro.FirstOrDefault(x => x.PersonelId == pid);
            _db.DonemKapanisAnlikGoruntuleri.Add(new DonemKapanisAnlikGoruntusu
            {
                MaasDonemKapanisiId = donem.Id,
                KapanisNo = donem.KapanisSayisi,
                PersonelId = pid,
                TcKimlikNo = p.TcKimlikNo,
                AdSoyad = p.AdSoyad,
                Iban = b?.Iban ?? p.Iban,
                MaasTutar = b?.MaasTutar,
                BordroNet = beklenen,
                Odenen = odenen,
                Fark = odenen - beklenen
            });
        }
        _db.DonemKapanisOzetleri.Add(new DonemKapanisOzeti
        {
            MaasDonemKapanisiId = donem.Id,
            KapanisNo = donem.KapanisSayisi,
            ToplamBeklenenMaas = o.Beklenen,
            ToplamBankaOdemesi = o.Odenen,
            ToplamEksikOdeme = o.Eksik,
            ToplamFazlaOdeme = o.Fazla,
            OdenmemisHakEdisler = o.Eksik,
            AcikAvanslar = o.AcikAvans,
            CikisSonrasiSupheli = o.CikisSonrasi,
            CozulmemisUyariSayisi = o.Cozulmemis,
            GerekceliIstisnaSayisi = o.Istisna,
            PersonelSayisi = o.Kisiler.Count,
            KapatanKullanici = kullanici,
            KapanisNotu = not
        });

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(MaasDonemKapanisi),
            VarlikId = donem.Id,
            Islem = "DonemKapatildi",
            YeniDeger = $"{donem.DonemYil}-{donem.DonemAy:D2} | kapanış #{donem.KapanisSayisi} | beklenen {o.Beklenen:N2} | ödenen {o.Odenen:N2} | istisna {o.Istisna}",
            DegisiklikNedeni = not,
            KullaniciAdi = kullanici
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task YenidenAcAsync(Guid donemId, string kullanici, string neden, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Yonetici, "Kapatılmış dönemi yeniden açma", ct);
        var donem = await _db.MaasDonemKapanislari.Include(d => d.KontrolMaddeleri)
            .FirstOrDefaultAsync(d => d.Id == donemId, ct) ?? throw new InvalidOperationException("Dönem bulunamadı.");
        if (string.IsNullOrWhiteSpace(neden) || neden.Trim().Length < 10)
            throw new InvalidOperationException("Yeniden açma gerekçesi zorunludur (en az 10 karakter).");

        DonemKapanisKurallari.YenidenAc(donem, kullanici, neden.Trim());
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(MaasDonemKapanisi),
            VarlikId = donem.Id,
            Islem = "DonemYenidenAcildi",
            EskiDeger = MaasDonemDurumu.Kapatildi.ToString(),
            YeniDeger = donem.Durum.ToString(),
            DegisiklikNedeni = neden,
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);

        // Tekrar kapanış yeni bir yönetici onayı ister.
        var yonetici = donem.KontrolMaddeleri.FirstOrDefault(m => m.MaddeKodu == "YONETICI");
        if (yonetici is { Tamamlandi: true })
        {
            yonetici.Tamamlandi = false;
            yonetici.Not = $"Yeniden açıldı ({DateTime.Now:dd.MM.yyyy}); önceki onay: {yonetici.TamamlayanKullanici}";
            yonetici.TamamlayanKullanici = null;
            yonetici.TamamlanmaZamani = null;
            await _db.SaveChangesAsync(ct);
        }
    }

    // ------------------------------------------------------------------ Düzeltme

    public Task<DonemDuzeltmeKaydi> DuzeltmeEkleAsync(Guid donemId, Guid? personelId, string varlikTuru, Guid? varlikId,
        string alan, string? eski, string? yeni, string neden, string kullanici, CancellationToken ct = default) =>
        DuzeltmeEkleAsync(donemId, personelId, varlikTuru, varlikId, alan, eski, yeni, neden, kullanici, null, null, null, ct);

    public async Task<DonemDuzeltmeKaydi> DuzeltmeEkleAsync(
        Guid donemId, Guid? personelId, string varlikTuru, Guid? varlikId, string alan, string? eski, string? yeni,
        string neden, string kullanici, int? uygulanacakYil, int? uygulanacakAy, bool? maliEtkili, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Dönem düzeltme kaydı", ct);
        var donem = await DonemAsync(donemId, ct);
        if (string.IsNullOrWhiteSpace(neden))
            throw new InvalidOperationException("Düzeltme nedeni zorunludur.");
        if (string.IsNullOrWhiteSpace(alan))
            throw new InvalidOperationException("Düzeltilen alan zorunludur.");
        if (personelId.HasValue && !await _db.Personeller.AnyAsync(p => p.Id == personelId, ct))
            throw new InvalidOperationException("Personel bulunamadı.");

        decimal? fark = SayiMi(eski, out var e) && SayiMi(yeni, out var y) ? y - e : null;
        var mali = maliEtkili ?? (fark.HasValue && !string.Equals(alan, "Gun", StringComparison.OrdinalIgnoreCase));

        // Fark, düzeltilen dönemden sonraki ilk açık döneme devreder (kapatılmış dönemin rakamları değişmez).
        var hedef = uygulanacakYil.HasValue && uygulanacakAy.HasValue
            ? new DateOnly(uygulanacakYil.Value, uygulanacakAy.Value, 1)
            : await IlkAcikDonemAsync(new DateOnly(donem.DonemYil, donem.DonemAy, 1).AddMonths(donem.Durum == MaasDonemDurumu.Kapatildi ? 1 : 0), ct);
        if (await _db.MaasDonemKapanislari.AnyAsync(d => d.DonemYil == hedef.Year && d.DonemAy == hedef.Month && d.Durum == MaasDonemDurumu.Kapatildi, ct))
            throw new KapaliDonemIhlaliException($"Düzeltmenin uygulanacağı {hedef:yyyy-MM} dönemi kapalı; açık bir dönem seçin.");

        var duzeltme = new DonemDuzeltmeKaydi
        {
            MaasDonemKapanisiId = donem.Id,
            PersonelId = personelId,
            VarlikTuru = varlikTuru,
            VarlikId = varlikId,
            AlanAdi = alan,
            EskiDeger = eski,
            YeniDeger = yeni,
            FarkTutari = mali ? fark : null,
            UygulanacakDonemYil = hedef.Year,
            UygulanacakDonemAy = hedef.Month,
            Neden = neden,
            KullaniciAdi = kullanici
        };
        _db.DonemDuzeltmeler.Add(duzeltme);

        if (personelId.HasValue && mali && fark is { } f && f != 0)
        {
            await _defter.DeftereEkleAsync(MaliHareketKurallari.Olustur(
                personelId.Value,
                MaliHareketKurallari.IslemKimligi("Duzeltme", personelId.Value, duzeltme.Id.ToString("N")),
                DateOnly.FromDateTime(DateTime.Today), hedef.Year, hedef.Month,
                MaliIslemTuru.OncekiDonemDuzeltme, BakiyeTuru.MaasAlacagi, f,
                $"{donem.DonemYil}-{donem.DonemAy:D2} dönemi düzeltmesi → {hedef:yyyy-MM}: {alan} {eski} → {yeni} ({neden})",
                nameof(DonemDuzeltmeKaydi), duzeltme.Id.ToString(), null, kullanici), true, ct);
        }

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personelId,
            VarlikTuru = nameof(DonemDuzeltmeKaydi),
            VarlikId = duzeltme.Id,
            Islem = "DonemDuzeltme",
            AlanAdi = alan,
            EskiDeger = eski,
            YeniDeger = yeni,
            DegisiklikNedeni = $"{neden} | kaynak dönem {donem.DonemYil}-{donem.DonemAy:D2} ({donem.Durum}) → uygulanacak {hedef:yyyy-MM}",
            KullaniciAdi = kullanici
        });

        await _db.SaveChangesAsync(ct);
        return duzeltme;
    }

    private async Task<DateOnly> IlkAcikDonemAsync(DateOnly baslangic, CancellationToken ct)
    {
        var kapalilar = (await _db.MaasDonemKapanislari.AsNoTracking()
                .Where(d => d.Durum == MaasDonemDurumu.Kapatildi).Select(d => new { d.DonemYil, d.DonemAy }).ToListAsync(ct))
            .Select(d => new DateOnly(d.DonemYil, d.DonemAy, 1)).ToHashSet();
        var aday = baslangic;
        while (kapalilar.Contains(aday)) aday = aday.AddMonths(1);
        return aday;
    }

    private static bool SayiMi(string? s, out decimal d) =>
        decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d) ||
        decimal.TryParse(s, NumberStyles.Number, Tr, out d);

    // ------------------------------------------------------------------ Karşılaştırma ve rapor

    public async Task<DonemKarsilastirmaDto> OncekiAyKarsilastirAsync(int yil, int ay, CancellationToken ct = default)
    {
        var onceki = new DateOnly(yil, ay, 1).AddMonths(-1);
        var basi = new DateOnly(yil, ay, 1);
        var sonu = basi.AddMonths(1).AddDays(-1);

        var oncekiBordro = await _db.MaasDonemKayitlari.AsNoTracking().Include(m => m.Kalemler)
            .Where(m => m.DonemYil == onceki.Year && m.DonemAy == onceki.Month).ToListAsync(ct);
        var buBordro = await _db.MaasDonemKayitlari.AsNoTracking().Include(m => m.Kalemler)
            .Where(m => m.DonemYil == yil && m.DonemAy == ay).ToListAsync(ct);
        var personeller = await _db.Personeller.AsNoTracking().Include(p => p.CalismaDonemleri).ToDictionaryAsync(p => p.Id, ct);
        var cikislar = await _db.IstenCikisKayitlari.AsNoTracking().ToListAsync(ct);
        var maasDenetim = await _db.DenetimKayitlari.AsNoTracking()
            .Where(d => d.AlanAdi == nameof(Personel.GuncelMaas) && d.IslemZamani >= onceki.ToDateTime(TimeOnly.MinValue))
            .ToListAsync(ct);

        static decimal Net(IEnumerable<MaasDonemKaydi> l) => l.Sum(x => x.NetIstihkak);
        var oncekiMap = oncekiBordro.GroupBy(b => b.PersonelId).ToDictionary(g => g.Key, g => g.ToList());
        var buMap = buBordro.GroupBy(b => b.PersonelId).ToDictionary(g => g.Key, g => g.ToList());
        var detay = new List<DonemDegisiklikDetayi>();

        foreach (var pid in oncekiMap.Keys.Except(buMap.Keys))
        {
            var p = personeller[pid];
            var cikis = cikislar.Where(c => c.PersonelId == pid && c.IstenCikisTarihi >= onceki).MaxBy(c => c.IstenCikisTarihi);
            var donemCikis = p.CalismaDonemleri.Where(c => c.IstenCikisTarihi.HasValue && c.IstenCikisTarihi >= onceki).MaxBy(c => c.IstenCikisTarihi);
            detay.Add(new DonemDegisiklikDetayi
            {
                Tur = "İşten ayrılan",
                PersonelId = pid,
                TcKimlikNo = p.TcKimlikNo,
                AdSoyad = p.AdSoyad,
                OncekiTutar = Net(oncekiMap[pid]),
                NetEtki = -Net(oncekiMap[pid]),
                Kaynak = cikis is not null
                    ? $"İşten çıkış kaydı: {cikis.IstenCikisTarihi:dd.MM.yyyy}, neden: {cikis.CikisNedeni ?? "-"}, SGK: {cikis.SgkCikisDurumu}"
                    : donemCikis is not null
                        ? $"Çalışma dönemi çıkışı {donemCikis.IstenCikisTarihi:dd.MM.yyyy} (işten çıkış kaydı yok)"
                        : "UYARI: Bu ay bordroda yok ancak çıkış kaydı da yok — inceleyin.",
                KaynakReferans = cikis?.Id.ToString() ?? donemCikis?.Id.ToString()
            });
        }

        foreach (var pid in buMap.Keys.Except(oncekiMap.Keys))
        {
            var p = personeller[pid];
            var giris = p.CalismaDonemleri.Where(c => c.IseGirisTarihi <= sonu).MaxBy(c => c.IseGirisTarihi);
            var yeniGiris = giris is not null && giris.IseGirisTarihi >= basi;
            detay.Add(new DonemDegisiklikDetayi
            {
                Tur = "İşe başlayan",
                PersonelId = pid,
                TcKimlikNo = p.TcKimlikNo,
                AdSoyad = p.AdSoyad,
                YeniTutar = Net(buMap[pid]),
                NetEtki = Net(buMap[pid]),
                Kaynak = yeniGiris
                    ? $"İşe giriş {giris!.IseGirisTarihi:dd.MM.yyyy} ({giris.Isyeri})" + (p.CalismaDonemleri.Count > 1 ? " — yeniden işe alım" : "")
                    : giris is not null
                        ? $"Önceki ay bordroda yoktu; çalışma dönemi {giris.IseGirisTarihi:dd.MM.yyyy} başlangıçlı (ücretsiz izin dönüşü / eksik bordro olabilir)"
                        : "UYARI: Çalışma dönemi kaydı yok.",
                KaynakReferans = giris?.Id.ToString()
            });
        }

        var maasDegisen = 0;
        decimal maasEtkisi = 0, digerEtki = 0;
        foreach (var pid in oncekiMap.Keys.Intersect(buMap.Keys))
        {
            var p = personeller[pid];
            var o = oncekiMap[pid].First();
            var b = buMap[pid].First();
            var netFark = Net(buMap[pid]) - Net(oncekiMap[pid]);

            if (o.MaasTutar != b.MaasTutar)
            {
                maasDegisen++;
                maasEtkisi += netFark;
                var kayit = maasDenetim.Where(d => d.PersonelId == pid).MaxBy(d => d.IslemZamani);
                detay.Add(new DonemDegisiklikDetayi
                {
                    Tur = "Maaş değişikliği",
                    PersonelId = pid,
                    TcKimlikNo = p.TcKimlikNo,
                    AdSoyad = p.AdSoyad,
                    OncekiTutar = o.MaasTutar,
                    YeniTutar = b.MaasTutar,
                    NetEtki = netFark,
                    Kaynak = kayit is not null
                        ? $"Kart maaşı {kayit.EskiDeger} → {kayit.YeniDeger} ({kayit.KullaniciAdi}, {kayit.IslemZamani.ToLocalTime():dd.MM.yyyy}; neden: {kayit.DegisiklikNedeni ?? "-"})"
                        : $"Bordroda maaş {o.MaasTutar:N2} → {b.MaasTutar:N2}; personel kartında maaş değişikliği kaydı yok — inceleyin.",
                    KaynakReferans = kayit?.Id.ToString()
                });
            }
            else if (Math.Abs(netFark) > Tolerans)
            {
                digerEtki += netFark;
                detay.Add(new DonemDegisiklikDetayi
                {
                    Tur = "Net tutar değişimi",
                    PersonelId = pid,
                    TcKimlikNo = p.TcKimlikNo,
                    AdSoyad = p.AdSoyad,
                    OncekiTutar = Net(oncekiMap[pid]),
                    YeniTutar = Net(buMap[pid]),
                    NetEtki = netFark,
                    Kaynak = KalemFarki(o, b),
                    KaynakReferans = b.Id.ToString()
                });
            }
        }

        var ayrilanEtki = detay.Where(d => d.Tur == "İşten ayrılan").Sum(d => d.NetEtki);
        var baslayanEtki = detay.Where(d => d.Tur == "İşe başlayan").Sum(d => d.NetEtki);
        var oncekiToplam = Net(oncekiBordro);
        var buToplam = Net(buBordro);
        var ayrilan = oncekiMap.Keys.Except(buMap.Keys).Count();
        var baslayan = buMap.Keys.Except(oncekiMap.Keys).Count();
        var yon = buToplam > oncekiToplam ? "artmış" : buToplam < oncekiToplam ? "azalmış" : "değişmemiş";

        return new DonemKarsilastirmaDto
        {
            OncekiYil = onceki.Year,
            OncekiAy = onceki.Month,
            OncekiPersonelSayisi = oncekiMap.Count,
            BuAyPersonelSayisi = buMap.Count,
            IstenAyrilan = ayrilan,
            IseBaslayan = baslayan,
            MaasDegisen = maasDegisen,
            OncekiToplamNet = oncekiToplam,
            BuAyToplamNet = buToplam,
            AyrilanEtkisi = ayrilanEtki,
            BaslayanEtkisi = baslayanEtki,
            MaasDegisikligiEtkisi = maasEtkisi,
            DigerNetEtkisi = digerEtki,
            Nedenler =
            [
                $"Önceki ay ({onceki:yyyy-MM}) {oncekiMap.Count} personel, bu ay {buMap.Count} personel.",
                $"{ayrilan} kişi işten ayrılmış / bordroda yok (etki {ayrilanEtki:N2}).",
                $"{baslayan} kişi işe başlamış / yeni bordroda (etki {baslayanEtki:N2}).",
                $"{maasDegisen} personelde maaş değişikliği (etki {maasEtkisi:N2}).",
                $"Diğer net değişimler (gün, kesinti, ek ödeme) etkisi {digerEtki:N2}.",
                $"Toplam net ödeme {oncekiToplam:N2} → {buToplam:N2}: {Math.Abs(buToplam - oncekiToplam):N2} TL {yon}."
            ],
            Detaylar = detay.OrderBy(d => d.Tur).ThenBy(d => d.AdSoyad).ToList()
        };
    }

    private static string KalemFarki(MaasDonemKaydi o, MaasDonemKaydi b)
    {
        var parcalar = new List<string>();
        if (o.Gun != b.Gun) parcalar.Add($"gün {o.Gun?.ToString("0.##", Tr) ?? "-"} → {b.Gun?.ToString("0.##", Tr) ?? "-"}");
        var turler = o.Kalemler.Select(k => k.Tur).Concat(b.Kalemler.Select(k => k.Tur)).Distinct().Where(t => t != MaasKalemTuru.Maas);
        foreach (var t in turler)
        {
            var a = o.Kalemler.Where(k => k.Tur == t).Sum(k => k.Tutar);
            var c = b.Kalemler.Where(k => k.Tur == t).Sum(k => k.Tutar);
            if (Math.Abs(a - c) > Tolerans)
                parcalar.Add($"{b.Kalemler.Concat(o.Kalemler).First(k => k.Tur == t).Ad} {a:N2} → {c:N2}");
        }
        return parcalar.Count == 0 ? "Bordro net istihkakı değişti; kalem dökümünde açıklayıcı fark yok — inceleyin." : "Bordro: " + string.Join("; ", parcalar);
    }

    public async Task<KapanisRaporuDto> KapanisRaporuAsync(Guid donemId, CancellationToken ct = default)
    {
        var donem = await _db.MaasDonemKapanislari.AsNoTracking()
            .Include(d => d.KontrolMaddeleri).Include(d => d.Duzeltmeler)
            .FirstOrDefaultAsync(d => d.Id == donemId, ct) ?? throw new InvalidOperationException("Dönem bulunamadı.");
        var farklar = await FarklarAsync(donemId, ct);
        var gecmis = await _db.DonemKapanisOzetleri.AsNoTracking().Where(o => o.MaasDonemKapanisiId == donemId)
            .OrderBy(o => o.KapanisNo).ToListAsync(ct);

        var istisnalar = new List<string>();
        var oturumlar = await _db.OdemeKontrolOturumlari.AsNoTracking().Include(o => o.Satirlar)
            .Where(o => o.DonemYil == donem.DonemYil && o.DonemAy == donem.DonemAy).ToListAsync(ct);
        foreach (var s in oturumlar.SelectMany(o => o.Satirlar).Where(s => s.IstisnaOnayi))
            istisnalar.Add($"Ödeme kontrolü | {s.AdSoyad} ({s.TcKimlikNo}) | {s.Sonuc} | {s.IstisnaGerekcesi} | {s.IstisnaOnaylayan} {s.IstisnaOnayZamani?.ToLocalTime():dd.MM.yyyy HH:mm}");
        foreach (var f in farklar.Where(f => f.Durum == FarkDurumu.Istisna))
            istisnalar.Add($"Mutabakat | {f.AdSoyad} ({f.TcKimlikNo}) | {f.FarkTuru} {f.Fark:N2} | {f.CozumAciklamasi} | {f.Cozen} {f.CozumZamani?.ToLocalTime():dd.MM.yyyy HH:mm}");
        foreach (var m in donem.KontrolMaddeleri.Where(m => m.ElleOnaylandi))
            istisnalar.Add($"Kontrol listesi | {m.MaddeKodu} elle onaylandı | {m.OtomatikAciklama} | Gerekçe: {m.Not} | {m.TamamlayanKullanici}");

        var kapali = donem.Durum == MaasDonemDurumu.Kapatildi;
        var anlik = kapali
            ? await _db.DonemKapanisAnlikGoruntuleri.AsNoTracking()
                .Where(a => a.MaasDonemKapanisiId == donemId && a.KapanisNo == donem.KapanisSayisi).OrderBy(a => a.AdSoyad).ToListAsync(ct)
            : new List<DonemKapanisAnlikGoruntusu>();

        decimal beklenen, odenen, eksik, fazla, avans, cikis;
        int cozulmemis, istisna;
        if (kapali && gecmis.LastOrDefault() is { } son)
        {
            (beklenen, odenen, eksik, fazla, avans, cikis, cozulmemis, istisna) = (son.ToplamBeklenenMaas, son.ToplamBankaOdemesi,
                son.ToplamEksikOdeme, son.ToplamFazlaOdeme, son.AcikAvanslar, son.CikisSonrasiSupheli, son.CozulmemisUyariSayisi, son.GerekceliIstisnaSayisi);
        }
        else
        {
            var o = await CanliOzetAsync(donem, ct);
            (beklenen, odenen, eksik, fazla, avans, cikis, cozulmemis, istisna) = (o.Beklenen, o.Odenen, o.Eksik, o.Fazla, o.AcikAvans, o.CikisSonrasi, o.Cozulmemis, o.Istisna);
        }

        return new KapanisRaporuDto
        {
            DonemId = donem.Id,
            DonemYil = donem.DonemYil,
            DonemAy = donem.DonemAy,
            Durum = donem.Durum,
            KapanisSayisi = donem.KapanisSayisi,
            DondurulmusVeri = kapali,
            ToplamBeklenenMaas = beklenen,
            ToplamBankaOdemesi = odenen,
            ToplamEksikOdeme = eksik,
            ToplamFazlaOdeme = fazla,
            OdenmemisHakEdisler = eksik,
            AcikAvanslar = avans,
            CikisSonrasiSupheli = cikis,
            CozulmemisUyariSayisi = cozulmemis,
            GerekceliIstisnaSayisi = istisna,
            Farklar = farklar,
            Istisnalar = istisnalar,
            KontrolMaddeleri = donem.KontrolMaddeleri.OrderBy(m => m.Sira).ToList(),
            AnlikGoruntu = anlik,
            KapanisGecmisi = gecmis,
            Duzeltmeler = donem.Duzeltmeler.OrderBy(d => d.IslemZamani).ToList(),
            Karsilastirma = await OncekiAyKarsilastirAsync(donem.DonemYil, donem.DonemAy, ct)
        };
    }

    public async Task<byte[]> KapanisRaporuExcelAsync(Guid donemId, CancellationToken ct = default)
    {
        var r = await KapanisRaporuAsync(donemId, ct);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Kapanış Özeti");
        ws.Cell(1, 1).Value = $"PayControl — {r.DonemYil}-{r.DonemAy:D2} Maaş Dönemi Kapanış Raporu";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(2, 1).Value = $"Durum: {r.Durum} | Kapanış sayısı: {r.KapanisSayisi} | {(r.DondurulmusVeri ? "Kapanış anında dondurulmuş değerler" : "Canlı değerler (dönem açık)")}";
        var satirlar = new (string, object)[]
        {
            ("Toplam beklenen maaş", r.ToplamBeklenenMaas), ("Toplam banka/kasa ödemesi", r.ToplamBankaOdemesi),
            ("Toplam eksik ödeme", r.ToplamEksikOdeme), ("Toplam fazla ödeme", r.ToplamFazlaOdeme),
            ("Ödenmemiş hak edişler", r.OdenmemisHakEdisler), ("Açık avanslar (dönem sonu)", r.AcikAvanslar),
            ("Çıkış sonrası şüpheli ödemeler", r.CikisSonrasiSupheli), ("Çözülmemiş uyarılar", r.CozulmemisUyariSayisi),
            ("Gerekçeli istisnalar", r.GerekceliIstisnaSayisi)
        };
        var i = 4;
        foreach (var (ad, deger) in satirlar)
        {
            ws.Cell(i, 1).Value = ad;
            if (deger is decimal d) { ws.Cell(i, 2).Value = d; ws.Cell(i, 2).Style.NumberFormat.Format = "#,##0.00"; }
            else ws.Cell(i, 2).Value = Convert.ToInt32(deger);
            i++;
        }
        i += 1;
        ws.Cell(i++, 1).Value = "Kontrol listesi";
        foreach (var m in r.KontrolMaddeleri)
        {
            ws.Cell(i, 1).Value = $"{m.Sira}. {m.Aciklama}";
            ws.Cell(i, 2).Value = m.Tamamlandi ? (m.ElleOnaylandi ? "Elle onay" : "Tamam") : "Eksik";
            ws.Cell(i, 3).Value = m.OtomatikAciklama;
            ws.Cell(i, 4).Value = m.TamamlayanKullanici;
            i++;
        }
        ws.Columns().AdjustToContents();

        var wf = wb.AddWorksheet("Mutabakat Farkları");
        string[] fb = ["Personel", "TC", "Tür", "Beklenen", "Ödenen", "Fark", "Durum", "Açıklama", "Çözüm", "Çözen"];
        for (var c = 0; c < fb.Length; c++) wf.Cell(1, c + 1).Value = fb[c];
        wf.Row(1).Style.Font.Bold = true;
        i = 2;
        foreach (var f in r.Farklar)
        {
            wf.Cell(i, 1).Value = f.AdSoyad; wf.Cell(i, 2).Value = f.TcKimlikNo; wf.Cell(i, 3).Value = f.FarkTuru.ToString();
            wf.Cell(i, 4).Value = f.Beklenen; wf.Cell(i, 5).Value = f.Odenen; wf.Cell(i, 6).Value = f.Fark;
            wf.Cell(i, 7).Value = f.Durum.ToString(); wf.Cell(i, 8).Value = f.Aciklama; wf.Cell(i, 9).Value = f.CozumAciklamasi; wf.Cell(i, 10).Value = f.Cozen;
            wf.Range(i, 4, i, 6).Style.NumberFormat.Format = "#,##0.00";
            i++;
        }
        wf.Columns().AdjustToContents();

        var wi = wb.AddWorksheet("İstisnalar");
        i = 1;
        foreach (var s in r.Istisnalar) wi.Cell(i++, 1).Value = s;

        if (r.Karsilastirma is { } k)
        {
            var wk = wb.AddWorksheet("Önceki Ay Karşılaştırma");
            i = 1;
            foreach (var n in k.Nedenler) wk.Cell(i++, 1).Value = n;
            i++;
            string[] kb = ["Tür", "Personel", "TC", "Önceki", "Yeni", "Net etki", "Kaynak"];
            for (var c = 0; c < kb.Length; c++) wk.Cell(i, c + 1).Value = kb[c];
            wk.Row(i).Style.Font.Bold = true;
            i++;
            foreach (var d in k.Detaylar)
            {
                wk.Cell(i, 1).Value = d.Tur; wk.Cell(i, 2).Value = d.AdSoyad; wk.Cell(i, 3).Value = d.TcKimlikNo;
                wk.Cell(i, 4).Value = d.OncekiTutar; wk.Cell(i, 5).Value = d.YeniTutar; wk.Cell(i, 6).Value = d.NetEtki; wk.Cell(i, 7).Value = d.Kaynak;
                i++;
            }
            wk.Columns().AdjustToContents();
        }

        if (r.AnlikGoruntu.Count > 0)
        {
            var wa = wb.AddWorksheet("Kapanış Anlık Görüntü");
            string[] ab = ["Personel", "TC", "IBAN", "Maaş", "Bordro net", "Ödenen", "Fark"];
            for (var c = 0; c < ab.Length; c++) wa.Cell(1, c + 1).Value = ab[c];
            wa.Row(1).Style.Font.Bold = true;
            i = 2;
            foreach (var a in r.AnlikGoruntu)
            {
                wa.Cell(i, 1).Value = a.AdSoyad; wa.Cell(i, 2).Value = a.TcKimlikNo; wa.Cell(i, 3).Value = a.Iban;
                wa.Cell(i, 4).Value = a.MaasTutar; wa.Cell(i, 5).Value = a.BordroNet; wa.Cell(i, 6).Value = a.Odenen; wa.Cell(i, 7).Value = a.Fark;
                i++;
            }
            wa.Columns().AdjustToContents();
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private async Task<MaasDonemKapanisi> DonemAsync(Guid id, CancellationToken ct) =>
        await _db.MaasDonemKapanislari.FirstOrDefaultAsync(d => d.Id == id, ct)
        ?? throw new InvalidOperationException("Dönem bulunamadı.");
}
