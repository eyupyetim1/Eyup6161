using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class DonemKapanisServisi : IDonemKapanisServisi
{
    private readonly IPayControlDbContext _db;

    public DonemKapanisServisi(IPayControlDbContext db) => _db = db;

    public async Task<MaasDonemKapanisi> GetOrCreateAsync(int yil, int ay, string? sirket = null, CancellationToken ct = default)
    {
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
        var donem = await _db.MaasDonemKapanislari
            .FirstOrDefaultAsync(d => d.DonemYil == yil && d.DonemAy == ay, ct);
        DonemKapanisKurallari.DogrudanDegisikligiEngelle(donem);
    }

    public async Task DurumGuncelleAsync(Guid donemId, MaasDonemDurumu durum, string kullanici, CancellationToken ct = default)
    {
        var donem = await _db.MaasDonemKapanislari.FirstOrDefaultAsync(d => d.Id == donemId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");

        if (donem.Durum == MaasDonemDurumu.Kapatildi && durum != MaasDonemDurumu.Kapatildi)
            throw new InvalidOperationException("Kapatılmış dönem durumunu değiştirmek için yeniden açma kullanın.");

        var eski = donem.Durum.ToString();
        donem.Durum = durum;
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(MaasDonemKapanisi),
            VarlikId = donem.Id,
            Islem = "DurumGuncelleme",
            AlanAdi = "Durum",
            EskiDeger = eski,
            YeniDeger = durum.ToString(),
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task MaddeTamamlaAsync(Guid maddeId, string kullanici, string? not = null, CancellationToken ct = default)
    {
        var madde = await _db.DonemKontrolMaddeleri
            .Include(m => m.MaasDonemKapanisi)
            .FirstOrDefaultAsync(m => m.Id == maddeId, ct)
            ?? throw new InvalidOperationException("Kontrol maddesi bulunamadı.");

        DonemKapanisKurallari.DogrudanDegisikligiEngelle(madde.MaasDonemKapanisi);

        madde.Tamamlandi = true;
        madde.TamamlanmaZamani = DateTime.UtcNow;
        madde.TamamlayanKullanici = kullanici;
        madde.Not = not;
        await _db.SaveChangesAsync(ct);
    }

    public async Task OzetHesaplaAsync(Guid donemId, CancellationToken ct = default)
    {
        var donem = await _db.MaasDonemKapanislari.FirstOrDefaultAsync(d => d.Id == donemId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");

        var bordrolar = await _db.MaasDonemKayitlari
            .Where(m => m.DonemYil == donem.DonemYil && m.DonemAy == donem.DonemAy)
            .ToListAsync(ct);
        var odemeler = await _db.BankaOdemeleri
            .Where(o => o.HakEdisDonemYil == donem.DonemYil && o.HakEdisDonemAy == donem.DonemAy)
            .ToListAsync(ct);

        var beklenen = bordrolar.Sum(b => b.NetIstihkak);
        var odenen = odemeler.Sum(o => o.Tutar);
        var fazla = 0m;
        var eksik = 0m;
        foreach (var b in bordrolar)
        {
            var pOdenen = odemeler.Where(o => o.PersonelId == b.PersonelId).Sum(o => o.Tutar);
            var fark = pOdenen - b.NetIstihkak;
            if (fark > 0.01m) fazla += fark;
            else if (fark < -0.01m) eksik += -fark;
        }

        donem.ToplamBeklenenMaas = beklenen;
        donem.ToplamBankaOdemesi = odenen;
        donem.ToplamFazlaOdeme = fazla;
        donem.ToplamEksikOdeme = eksik;
        donem.OdenmemisHakEdisler = Math.Max(0, beklenen - odenen);
        var avansKalanlar = await _db.Avanslar.AsNoTracking().Select(a => a.KalanBorc).ToListAsync(ct);
        donem.AcikAvanslar = avansKalanlar.Sum();
        donem.CikisSonrasiSupheli = odemeler
            .Where(o => o.Siniflandirma == OdemeSiniflandirma.BrutSupheliOdeme ||
                        o.Siniflandirma == OdemeSiniflandirma.AciklanamayanFazlaOdeme)
            .Sum(o => o.Tutar);
        donem.GerekceliIstisnaSayisi = await _db.OdemeKontrolSatirlari
            .CountAsync(s => s.IstisnaOnayi &&
                             s.OdemeKontrolOturumu!.DonemYil == donem.DonemYil &&
                             s.OdemeKontrolOturumu.DonemAy == donem.DonemAy, ct);

        await _db.SaveChangesAsync(ct);
    }

    public async Task KapatAsync(Guid donemId, string kullanici, string? not, CancellationToken ct = default)
    {
        var donem = await _db.MaasDonemKapanislari
            .Include(d => d.KontrolMaddeleri)
            .FirstOrDefaultAsync(d => d.Id == donemId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");

        await OzetHesaplaAsync(donemId, ct);
        donem = (await _db.MaasDonemKapanislari.Include(d => d.KontrolMaddeleri)
            .FirstAsync(d => d.Id == donemId, ct));

        var tumTamam = donem.KontrolMaddeleri.All(m => m.Tamamlandi);
        DonemKapanisKurallari.Kapat(donem, kullanici, not, tumTamam, donem.CozulmemisUyariSayisi);

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(MaasDonemKapanisi),
            VarlikId = donem.Id,
            Islem = "DonemKapatildi",
            YeniDeger = $"{donem.DonemYil}-{donem.DonemAy:D2}",
            DegisiklikNedeni = not,
            KullaniciAdi = kullanici
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task YenidenAcAsync(Guid donemId, string kullanici, string neden, CancellationToken ct = default)
    {
        var donem = await _db.MaasDonemKapanislari.FirstOrDefaultAsync(d => d.Id == donemId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");

        DonemKapanisKurallari.YenidenAc(donem, kullanici, neden);
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(MaasDonemKapanisi),
            VarlikId = donem.Id,
            Islem = "DonemYenidenAcildi",
            DegisiklikNedeni = neden,
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<DonemDuzeltmeKaydi> DuzeltmeEkleAsync(
        Guid donemId,
        Guid? personelId,
        string varlikTuru,
        Guid? varlikId,
        string alan,
        string? eski,
        string? yeni,
        string neden,
        string kullanici,
        CancellationToken ct = default)
    {
        var donem = await _db.MaasDonemKapanislari.FirstOrDefaultAsync(d => d.Id == donemId, ct)
            ?? throw new InvalidOperationException("Dönem bulunamadı.");

        if (string.IsNullOrWhiteSpace(neden))
            throw new InvalidOperationException("Düzeltme nedeni zorunludur.");

        var duzeltme = new DonemDuzeltmeKaydi
        {
            MaasDonemKapanisiId = donem.Id,
            PersonelId = personelId,
            VarlikTuru = varlikTuru,
            VarlikId = varlikId,
            AlanAdi = alan,
            EskiDeger = eski,
            YeniDeger = yeni,
            Neden = neden,
            KullaniciAdi = kullanici
        };
        _db.DonemDuzeltmeler.Add(duzeltme);

        // Eski kayıt silinmez; mali harekette düzeltme satırı
        if (personelId.HasValue && decimal.TryParse(yeni, out var yeniTutar))
        {
            var kimlik = MaliHareketKurallari.IslemKimligi("Duzeltme", personelId.Value, duzeltme.Id.ToString("N"));
            _db.MaliHareketler.Add(MaliHareketKurallari.Olustur(
                personelId.Value, kimlik, DateOnly.FromDateTime(DateTime.Today),
                donem.DonemYil, donem.DonemAy, MaliIslemTuru.OncekiDonemDuzeltme, BakiyeTuru.Bilgi,
                yeniTutar, $"Düzeltme: {alan} | {neden}", nameof(DonemDuzeltmeKaydi),
                duzeltme.Id.ToString(), null, kullanici));
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
            DegisiklikNedeni = neden,
            KullaniciAdi = kullanici
        });

        await _db.SaveChangesAsync(ct);
        return duzeltme;
    }

    public async Task<DonemKarsilastirmaDto> OncekiAyKarsilastirAsync(int yil, int ay, CancellationToken ct = default)
    {
        var onceki = new DateOnly(yil, ay, 1).AddMonths(-1);
        var oncekiBordro = await _db.MaasDonemKayitlari.AsNoTracking()
            .Where(m => m.DonemYil == onceki.Year && m.DonemAy == onceki.Month).ToListAsync(ct);
        var buBordro = await _db.MaasDonemKayitlari.AsNoTracking()
            .Where(m => m.DonemYil == yil && m.DonemAy == ay).ToListAsync(ct);

        var oncekiIds = oncekiBordro.Select(b => b.PersonelId).ToHashSet();
        var buIds = buBordro.Select(b => b.PersonelId).ToHashSet();
        var ayrilan = oncekiIds.Except(buIds).Count();
        var baslayan = buIds.Except(oncekiIds).Count();

        var maasDegisen = 0;
        foreach (var id in oncekiIds.Intersect(buIds))
        {
            var o = oncekiBordro.First(x => x.PersonelId == id).MaasTutar;
            var b = buBordro.First(x => x.PersonelId == id).MaasTutar;
            if (o != b) maasDegisen++;
        }

        var nedenler = new List<string>
        {
            $"Önceki ay {oncekiIds.Count} personel, bu ay {buIds.Count} personel.",
            $"{ayrilan} kişi işten ayrılmış / bordroda yok.",
            $"{baslayan} kişi işe başlamış / yeni bordroda.",
            $"{maasDegisen} personelde maaş değişikliği.",
            $"Toplam net: {oncekiBordro.Sum(x => x.NetIstihkak):N2} → {buBordro.Sum(x => x.NetIstihkak):N2}."
        };

        return new DonemKarsilastirmaDto
        {
            OncekiPersonelSayisi = oncekiIds.Count,
            BuAyPersonelSayisi = buIds.Count,
            IstenAyrilan = ayrilan,
            IseBaslayan = baslayan,
            MaasDegisen = maasDegisen,
            OncekiToplamNet = oncekiBordro.Sum(x => x.NetIstihkak),
            BuAyToplamNet = buBordro.Sum(x => x.NetIstihkak),
            Nedenler = nedenler
        };
    }
}
