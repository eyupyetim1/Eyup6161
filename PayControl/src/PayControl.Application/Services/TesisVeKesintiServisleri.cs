using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class TesisServisi : ITesisServisi
{
    private readonly IPayControlDbContext _db;
    private readonly IYetkiServisi _yetki;

    public TesisServisi(IPayControlDbContext db, IYetkiServisi yetki)
    {
        _db = db;
        _yetki = yetki;
    }

    public async Task<IReadOnlyList<TesisOzetDto>> ListeleAsync(CancellationToken ct = default)
    {
        var tesisler = await _db.Tesisler.AsNoTracking().OrderBy(t => t.Ad).ToListAsync(ct);
        var personel = await _db.Personeller.AsNoTracking().Select(p => new { p.Isyeri, p.CalismaDurumu, p.GuncelMaas }).ToListAsync(ct);
        return tesisler.Select(t =>
        {
            var ps = personel.Where(p => Esit(p.Isyeri, t.Ad)).ToList();
            return new TesisOzetDto
            {
                Tesis = t,
                ToplamPersonel = ps.Count,
                AktifPersonel = ps.Count(p => p.CalismaDurumu == CalismaDurumu.Aktif),
                AktifMaasToplami = ps.Where(p => p.CalismaDurumu == CalismaDurumu.Aktif).Sum(p => p.GuncelMaas ?? 0)
            };
        }).ToList();
    }

    public async Task<Tesis> KaydetAsync(TesisDto dto, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Tesis tanımı", ct);
        if (string.IsNullOrWhiteSpace(dto.Ad) || string.IsNullOrWhiteSpace(dto.Kod))
            throw new InvalidOperationException("Tesis kodu ve adı zorunludur.");
        var ad = dto.Ad.Trim();
        var kod = dto.Kod.Trim().ToUpperInvariant();
        if (await _db.Tesisler.AnyAsync(t => t.Id != dto.Id && (t.Ad == ad || t.Kod == kod), ct))
            throw new InvalidOperationException("Aynı ad veya kodla başka bir tesis var.");

        var t = dto.Id.HasValue ? await _db.Tesisler.FirstAsync(x => x.Id == dto.Id, ct) : new Tesis();
        var eskiAd = t.Ad;
        t.Kod = kod;
        t.Ad = ad;
        t.Sirket = dto.Sirket;
        t.Adres = dto.Adres;
        t.SorumluAdSoyad = dto.SorumluAdSoyad;
        t.SorumluEposta = dto.SorumluEposta;
        t.SorumluTelefon = dto.SorumluTelefon;
        t.Aktif = dto.Aktif;
        if (!dto.Id.HasValue) _db.Tesisler.Add(t);

        // Ad değiştiyse personel kartlarındaki işyeri adı da güncellenir (geçmiş bordro satırları değişmez).
        if (dto.Id.HasValue && !string.IsNullOrEmpty(eskiAd) && eskiAd != ad)
        {
            foreach (var p in await _db.Personeller.Where(p => p.Isyeri == eskiAd).ToListAsync(ct))
                p.Isyeri = ad;
        }

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(Tesis),
            VarlikId = t.Id,
            Islem = dto.Id.HasValue ? "TesisGuncellendi" : "TesisOlusturuldu",
            EskiDeger = eskiAd,
            YeniDeger = $"{kod} {ad}",
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
        return t;
    }

    public async Task<int> EksikleriOlusturAsync(string kullanici, CancellationToken ct = default)
    {
        var tanimli = (await _db.Tesisler.Select(t => t.Ad).ToListAsync(ct)).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var adlar = (await _db.Personeller.Where(p => p.Isyeri != null).Select(p => p.Isyeri!).Distinct().ToListAsync(ct))
            .Where(a => !string.IsNullOrWhiteSpace(a) && !tanimli.Contains(a.Trim()))
            .Select(a => a.Trim()).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();
        var sira = await _db.Tesisler.CountAsync(ct);
        var kodlar = (await _db.Tesisler.Select(t => t.Kod).ToListAsync(ct)).ToHashSet();
        foreach (var ad in adlar)
        {
            string kod;
            do kod = $"T{++sira:D3}"; while (kodlar.Contains(kod));
            kodlar.Add(kod);
            _db.Tesisler.Add(new Tesis { Ad = ad, Kod = kod });
        }
        if (adlar.Count > 0)
        {
            _db.DenetimKayitlari.Add(new DenetimKaydi
            {
                VarlikTuru = nameof(Tesis),
                Islem = "TesisOtomatikOlusturuldu",
                YeniDeger = string.Join(", ", adlar),
                KullaniciAdi = kullanici
            });
            await _db.SaveChangesAsync(ct);
        }
        return adlar.Count;
    }

    public async Task TesisOnaylaAsync(Guid donemId, string tesis, string? not, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Tesis dönem onayı", ct);
        var donem = await _db.MaasDonemKapanislari.FirstOrDefaultAsync(d => d.Id == donemId, ct) ?? throw new InvalidOperationException("Dönem bulunamadı.");
        DonemKapanisKurallari.DogrudanDegisikligiEngelle(donem);
        if (await _db.DonemTesisOnaylari.AnyAsync(o => o.MaasDonemKapanisiId == donemId && o.TesisAd == tesis, ct))
            throw new InvalidOperationException($"{tesis} için bu dönem zaten onaylanmış.");
        _db.DonemTesisOnaylari.Add(new DonemTesisOnayi { MaasDonemKapanisiId = donemId, TesisAd = tesis, Onaylayan = kullanici, Not = not });
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(DonemTesisOnayi),
            VarlikId = donemId,
            Islem = "TesisOnayi",
            AlanAdi = tesis,
            YeniDeger = $"{donem.DonemYil}-{donem.DonemAy:D2}",
            DegisiklikNedeni = not,
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<DonemTesisDurumuDto>> DonemTesisDurumuAsync(int yil, int ay, CancellationToken ct = default)
    {
        var bordro = await _db.MaasDonemKayitlari.AsNoTracking().Where(m => m.DonemYil == yil && m.DonemAy == ay)
            .Select(m => new { m.PersonelId, m.Isyeri, m.NetIstihkak }).ToListAsync(ct);
        var odemeler = await _db.BankaOdemeleri.AsNoTracking().Where(o => o.HakEdisDonemYil == yil && o.HakEdisDonemAy == ay)
            .Select(o => new { o.PersonelId, o.Tutar }).ToListAsync(ct);
        var donem = await _db.MaasDonemKapanislari.AsNoTracking().FirstOrDefaultAsync(d => d.DonemYil == yil && d.DonemAy == ay, ct);
        var onaylar = donem is null ? new List<DonemTesisOnayi>() : await _db.DonemTesisOnaylari.AsNoTracking().Where(o => o.MaasDonemKapanisiId == donem.Id).ToListAsync(ct);
        var farklar = donem is null ? new List<MutabakatFarki>() : await _db.MutabakatFarklari.AsNoTracking().Where(f => f.MaasDonemKapanisiId == donem.Id && f.Durum == FarkDurumu.Acik).ToListAsync(ct);
        var listeler = await _db.OnayliOdemeListeleri.AsNoTracking().Where(l => l.DonemYil == yil && l.DonemAy == ay && l.Gecerli).Select(l => l.Tesis).ToListAsync(ct);
        var oturum = await _db.OdemeKontrolOturumlari.AsNoTracking().Include(o => o.Satirlar)
            .Where(o => o.DonemYil == yil && o.DonemAy == ay).OrderByDescending(o => o.OlusturmaZamani).ToListAsync(ct);

        var tesisler = bordro.Select(b => b.Isyeri ?? "(Tesis belirtilmemiş)").Distinct().OrderBy(t => t).ToList();
        return tesisler.Select(t =>
        {
            var kisiler = bordro.Where(b => (b.Isyeri ?? "(Tesis belirtilmemiş)") == t).Select(b => b.PersonelId).ToHashSet();
            var onay = onaylar.FirstOrDefault(o => o.TesisAd == t);
            var sonOturum = oturum.FirstOrDefault(o => o.Tesis == t) ?? oturum.FirstOrDefault(o => o.Tesis == null);
            return new DonemTesisDurumuDto
            {
                Tesis = t,
                BordroSatiri = kisiler.Count,
                BordroNet = bordro.Where(b => kisiler.Contains(b.PersonelId)).Sum(b => b.NetIstihkak),
                Odenen = odemeler.Where(o => o.PersonelId.HasValue && kisiler.Contains(o.PersonelId.Value)).Sum(o => o.Tutar),
                KritikUyari = sonOturum?.Satirlar.Count(s => s.PersonelId.HasValue && kisiler.Contains(s.PersonelId.Value) && s.KritikMi && !s.OnayliListeyeAlinabilir) ?? 0,
                AcikFark = farklar.Count(f => f.PersonelId.HasValue && kisiler.Contains(f.PersonelId.Value)),
                GecerliListeVar = listeler.Contains(t) || listeler.Contains(null),
                Onaylandi = onay is not null,
                Onaylayan = onay?.Onaylayan,
                OnayZamani = onay?.OnayZamani
            };
        }).ToList();
    }

    private static bool Esit(string? a, string b) => string.Equals(a?.Trim(), b, StringComparison.CurrentCultureIgnoreCase);
}

public sealed class KesintiServisi : IKesintiServisi
{
    private readonly IPayControlDbContext _db;
    private readonly IYetkiServisi _yetki;
    private readonly IMaliHareketServisi _defter;

    public KesintiServisi(IPayControlDbContext db, IYetkiServisi yetki, IMaliHareketServisi defter)
    {
        _db = db;
        _yetki = yetki;
        _defter = defter;
    }

    public async Task<IcraDosyasi> IcraDosyasiKaydetAsync(Guid personelId, string dosyaNo, string? icraDairesi, string? alacakli, decimal toplamBorc,
        decimal kesintiOrani, int sira, DateOnly baslangic, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "İcra dosyası", ct);
        if (string.IsNullOrWhiteSpace(dosyaNo)) throw new InvalidOperationException("İcra dosya numarası zorunludur.");
        if (toplamBorc <= 0) throw new InvalidOperationException("İcra borç tutarı pozitif olmalıdır.");
        if (kesintiOrani is <= 0 or > 1) throw new InvalidOperationException("Kesinti oranı 0 ile 1 arasında olmalıdır (örn. 0,25).");
        if (!await _db.Personeller.AnyAsync(p => p.Id == personelId, ct)) throw new InvalidOperationException("Personel bulunamadı.");
        var d = new IcraDosyasi
        {
            PersonelId = personelId, DosyaNo = dosyaNo.Trim(), IcraDairesi = icraDairesi, Alacakli = alacakli,
            ToplamBorc = toplamBorc, KesintiOrani = kesintiOrani, Sira = sira, Baslangic = baslangic, OlusturanKullanici = kullanici
        };
        _db.IcraDosyalari.Add(d);
        Denetim(personelId, nameof(IcraDosyasi), d.Id, "IcraDosyasiEklendi", $"{d.DosyaNo} {toplamBorc:N2} oran {kesintiOrani:P0}", null, kullanici);
        await _db.SaveChangesAsync(ct);
        return d;
    }

    public async Task IcraDosyasiKapatAsync(Guid id, string neden, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "İcra dosyası kapatma", ct);
        if (string.IsNullOrWhiteSpace(neden)) throw new InvalidOperationException("Kapatma nedeni zorunludur.");
        var d = await _db.IcraDosyalari.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new InvalidOperationException("İcra dosyası bulunamadı.");
        d.Aktif = false;
        d.Bitis = DateOnly.FromDateTime(DateTime.Today);
        Denetim(d.PersonelId, nameof(IcraDosyasi), d.Id, "IcraDosyasiKapatildi", $"Kesilen {d.Kesilen:N2} / {d.ToplamBorc:N2}", neden, kullanici);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<IcraDosyasi>> IcraDosyalariAsync(Guid? personelId = null, CancellationToken ct = default) =>
        await _db.IcraDosyalari.AsNoTracking().Include(d => d.Kesintiler)
            .Where(d => personelId == null || d.PersonelId == personelId)
            .OrderBy(d => d.PersonelId).ThenBy(d => d.Sira).ToListAsync(ct);

    public async Task<BesTanimi> BesTanimlaAsync(Guid personelId, decimal oran, DateOnly baslangic, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "BES tanımı", ct);
        if (oran is <= 0 or > 0.5m) throw new InvalidOperationException("BES oranı 0 ile 0,5 arasında olmalıdır (otomatik katılımda 0,03).");
        foreach (var eski in await _db.BesTanimlari.Where(b => b.PersonelId == personelId && b.Aktif).ToListAsync(ct))
            eski.Aktif = false;
        var t = new BesTanimi { PersonelId = personelId, Oran = oran, Baslangic = baslangic, OlusturanKullanici = kullanici };
        _db.BesTanimlari.Add(t);
        Denetim(personelId, nameof(BesTanimi), t.Id, "BesTanimlandi", $"oran {oran:P1}, başlangıç {baslangic:dd.MM.yyyy}", null, kullanici);
        await _db.SaveChangesAsync(ct);
        return t;
    }

    public async Task BesCaymaAsync(Guid personelId, DateOnly caymaTarihi, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "BES cayma", ct);
        var t = await _db.BesTanimlari.FirstOrDefaultAsync(b => b.PersonelId == personelId && b.Aktif, ct)
                ?? throw new InvalidOperationException("Aktif BES tanımı yok.");
        t.CaymaTarihi = caymaTarihi;
        t.Aktif = false;
        Denetim(personelId, nameof(BesTanimi), t.Id, "BesCayma", caymaTarihi.ToString("dd.MM.yyyy"), null, kullanici);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BesTanimi>> BesTanimlariAsync(Guid? personelId = null, CancellationToken ct = default) =>
        await _db.BesTanimlari.AsNoTracking().Where(b => personelId == null || b.PersonelId == personelId)
            .OrderByDescending(b => b.Baslangic).ToListAsync(ct);

    public async Task BordroIcraIsleAsync(Guid personelId, int yil, int ay, decimal tutar, Guid iceAktarmaId, CancellationToken ct = default)
    {
        var sonu = new DateOnly(yil, ay, 1).AddMonths(1).AddDays(-1);
        var dosyalar = await _db.IcraDosyalari.Where(d => d.PersonelId == personelId && d.Aktif && d.Baslangic <= sonu)
            .OrderBy(d => d.Sira).ThenBy(d => d.Baslangic).ToListAsync(ct);
        var kalan = tutar;
        foreach (var d in dosyalar)
        {
            if (kalan <= 0.005m) break;
            var islenen = Math.Min(kalan, d.ToplamBorc - d.Kesilen);
            if (islenen <= 0) continue;
            d.Kesilen += islenen;
            kalan -= islenen;
            _db.IcraKesintileri.Add(new IcraKesintiKaydi { IcraDosyasiId = d.Id, DonemYil = yil, DonemAy = ay, Tutar = islenen, KaynakIceAktarmaId = iceAktarmaId });
            if (d.Kesilen >= d.ToplamBorc - 0.005m)
            {
                d.Aktif = false;
                d.Bitis = sonu;
            }
        }
    }

    public async Task BordroIcraGeriAlAsync(Guid iceAktarmaId, CancellationToken ct = default)
    {
        foreach (var k in await _db.IcraKesintileri.Include(x => x.IcraDosyasi).Where(x => x.KaynakIceAktarmaId == iceAktarmaId && !x.IptalEdildi).ToListAsync(ct))
        {
            k.IptalEdildi = true;
            k.IcraDosyasi!.Kesilen -= k.Tutar;
            if (k.IcraDosyasi.Kesilen < k.IcraDosyasi.ToplamBorc) { k.IcraDosyasi.Aktif = true; k.IcraDosyasi.Bitis = null; }
        }
    }

    public async Task<TahsilatPlani> TahsilatPlaniOlusturAsync(Guid personelId, decimal tutar, int taksit, int basYil, int basAy, string aciklama,
        string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Fazla ödeme tahsilat planı", ct);
        if (tutar <= 0 || taksit < 1) throw new InvalidOperationException("Tutar pozitif, taksit sayısı en az 1 olmalıdır.");
        if (string.IsNullOrWhiteSpace(aciklama)) throw new InvalidOperationException("Açıklama zorunludur.");

        var bakiye = (await _db.MaliHareketler.AsNoTracking()
            .Where(h => h.PersonelId == personelId && h.BakiyeTuru == BakiyeTuru.KesinlesmisGeriOdeme).ToListAsync(ct)).Sum(MaliHareketKurallari.Etki);
        var planli = (await _db.TahsilatPlanlari.Where(p => p.PersonelId == personelId).Select(p => p.KalanBorc).ToListAsync(ct)).Sum();
        if (tutar > bakiye - planli + 0.005m)
            throw new InvalidOperationException($"Plan tutarı ({tutar:N2}) planlanmamış kesin geri ödeme borcunu ({bakiye - planli:N2}) aşamaz.");

        var aylik = Math.Round(tutar / taksit, 2);
        var plan = new TahsilatPlani
        {
            PersonelId = personelId, Tutar = tutar, TaksitSayisi = taksit, AylikTutar = aylik, KalanBorc = tutar,
            BaslangicYil = basYil, BaslangicAy = basAy, Aciklama = aciklama, OlusturanKullanici = kullanici
        };
        var kalan = tutar;
        var donem = new DateOnly(basYil, basAy, 1);
        for (var i = 0; i < taksit; i++)
        {
            var p = i == taksit - 1 ? kalan : aylik;
            kalan -= p;
            var d = donem.AddMonths(i);
            plan.Taksitler.Add(new TahsilatTaksiti { DonemYil = d.Year, DonemAy = d.Month, Planlanan = p });
        }
        _db.TahsilatPlanlari.Add(plan);
        Denetim(personelId, nameof(TahsilatPlani), plan.Id, "TahsilatPlaniOlusturuldu", $"{tutar:N2} / {taksit} taksit, {basYil}-{basAy:D2}'den itibaren", aciklama, kullanici);
        await _db.SaveChangesAsync(ct);
        return plan;
    }

    public async Task<IReadOnlyList<TahsilatPlani>> TahsilatPlanlariAsync(Guid? personelId = null, CancellationToken ct = default) =>
        await _db.TahsilatPlanlari.AsNoTracking().Include(p => p.Taksitler)
            .Where(p => personelId == null || p.PersonelId == personelId)
            .OrderByDescending(p => p.OlusturmaZamani).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> BordroTahsilatIsleAsync(Guid personelId, int yil, int ay, decimal tutar, Guid iceAktarmaId, string? dosyaAdi,
        string kullanici, CancellationToken ct = default)
    {
        var uyarilar = new List<string>();
        var planlar = await _db.TahsilatPlanlari.Include(p => p.Taksitler)
            .Where(p => p.PersonelId == personelId && p.KalanBorc > 0).OrderBy(p => p.OlusturmaZamani).ToListAsync(ct);
        var kalan = tutar;
        foreach (var plan in planlar)
        {
            if (kalan <= 0.005m) break;
            var taksit = plan.Taksitler.FirstOrDefault(t => t.DonemYil == yil && t.DonemAy == ay && t.Gerceklesen is null)
                         ?? plan.Taksitler.Where(t => t.Gerceklesen is null).OrderBy(t => t.DonemYil).ThenBy(t => t.DonemAy).FirstOrDefault();
            if (taksit is null) continue;
            var islenen = Math.Min(kalan, plan.KalanBorc);
            taksit.Gerceklesen = islenen;
            taksit.KaynakIceAktarmaId = iceAktarmaId;
            taksit.GerceklesmeZamani = DateTime.UtcNow;
            plan.KalanBorc -= islenen;
            kalan -= islenen;
        }

        var islenenToplam = tutar - Math.Max(0, kalan);
        if (islenenToplam > 0)
        {
            await _defter.DeftereEkleAsync(MaliHareketKurallari.Olustur(personelId,
                MaliHareketKurallari.IslemKimligi("BordroTahsilat", personelId, $"{yil}-{ay:D2}:{iceAktarmaId:N}"),
                new DateOnly(yil, ay, 1).AddMonths(1).AddDays(-1), yil, ay, MaliIslemTuru.IadeAlinan, BakiyeTuru.KesinlesmisGeriOdeme, islenenToplam,
                $"Bordrodan fazla ödeme tahsilatı ({yil}-{ay:D2})", nameof(TahsilatTaksiti), iceAktarmaId.ToString(), dosyaAdi, kullanici), false, ct);
        }
        if (kalan > 0.005m)
            uyarilar.Add($"Bordrodaki fazla ödeme kesintisi ({tutar:N2}) tahsilat planlarındaki kalan borcu {kalan:N2} TL aşıyor.");
        return uyarilar;
    }

    public async Task BordroTahsilatGeriAlAsync(Guid iceAktarmaId, string kullanici, CancellationToken ct = default)
    {
        foreach (var t in await _db.TahsilatTaksitleri.Include(x => x.TahsilatPlani).Where(x => x.KaynakIceAktarmaId == iceAktarmaId && x.Gerceklesen != null).ToListAsync(ct))
        {
            t.TahsilatPlani!.KalanBorc += t.Gerceklesen!.Value;
            t.Gerceklesen = null;
            t.GerceklesmeZamani = null;
            t.KaynakIceAktarmaId = null;
        }
        var hareketler = await _db.MaliHareketler.AsNoTracking()
            .Where(h => h.KaynakTuru == nameof(TahsilatTaksiti) && h.KaynakReferans == iceAktarmaId.ToString() && !h.TersKayitMi).ToListAsync(ct);
        foreach (var h in hareketler)
            await _defter.DeftereEkleAsync(MaliHareketKurallari.TersKayit(h, "Bordro içe aktarma geri alındı", kullanici, DateOnly.FromDateTime(DateTime.Today)), false, ct);
    }

    private void Denetim(Guid? personelId, string varlik, Guid id, string islem, string? yeni, string? neden, string kullanici) =>
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personelId, VarlikTuru = varlik, VarlikId = id, Islem = islem, YeniDeger = yeni,
            DegisiklikNedeni = neden, KullaniciAdi = kullanici
        });
}
