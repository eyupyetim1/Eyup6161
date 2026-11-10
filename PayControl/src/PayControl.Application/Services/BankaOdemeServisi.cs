using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class BankaOdemeServisi : IBankaOdemeServisi
{
    private readonly IPayControlDbContext _db;
    private readonly IDonemKapanisServisi _donem;
    private readonly IMaliHareketServisi _defter;

    public BankaOdemeServisi(IPayControlDbContext db, IDonemKapanisServisi donem, IMaliHareketServisi defter)
    {
        _db = db;
        _donem = donem;
        _defter = defter;
    }

    public async Task<BankaOdemeKaydi> KaydetAsync(
        Guid personelId,
        DateOnly odemeTarihi,
        decimal tutar,
        int hakEdisYil,
        int hakEdisAy,
        string? iban,
        string? referansNo,
        string kullanici,
        CancellationToken ct = default)
    {
        await _donem.EnsureDonemAcikAsync(hakEdisYil, hakEdisAy, ct);
        if (tutar <= 0) throw new InvalidOperationException("Ödeme tutarı pozitif olmalıdır.");

        var anahtar = string.IsNullOrWhiteSpace(referansNo) ? $"MAN:{Guid.NewGuid():N}" : $"REF:{referansNo.Trim()}";
        if (await AnahtarVarMi(anahtar, referansNo, ct))
            throw new InvalidOperationException(
                $"Aynı banka hareketi (referans: {referansNo}) daha önce içe aktarılmış. Mükerrer kayıt engellendi.");

        var personel = await _db.Personeller.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personelId, ct)
                       ?? throw new InvalidOperationException("Personel bulunamadı.");

        var kayit = await OdemeEkleAsync(personel, OdemeKanali.Banka, odemeTarihi, tutar, hakEdisYil, hakEdisAy,
            iban, referansNo, anahtar, "Manuel", null, null, kullanici, ct);
        await _db.SaveChangesAsync(ct);
        return kayit;
    }

    public async Task<BankaOdemeKaydi> KasaOdemesiKaydetAsync(
        Guid personelId,
        DateOnly odemeTarihi,
        decimal tutar,
        int hakEdisYil,
        int hakEdisAy,
        string aciklama,
        string? belgeNo,
        string kullanici,
        CancellationToken ct = default)
    {
        await _donem.EnsureDonemAcikAsync(hakEdisYil, hakEdisAy, ct);
        if (tutar <= 0) throw new InvalidOperationException("Ödeme tutarı pozitif olmalıdır.");
        if (string.IsNullOrWhiteSpace(aciklama)) throw new InvalidOperationException("Kasa ödemesi için açıklama zorunludur.");

        var anahtar = string.IsNullOrWhiteSpace(belgeNo) ? $"KASA:{Guid.NewGuid():N}" : $"KASA:{belgeNo.Trim()}";
        if (await AnahtarVarMi(anahtar, null, ct))
            throw new InvalidOperationException($"Bu kasa belgesi ({belgeNo}) daha önce kaydedilmiş. Mükerrer kayıt engellendi.");

        var personel = await _db.Personeller.AsNoTracking().FirstOrDefaultAsync(p => p.Id == personelId, ct)
                       ?? throw new InvalidOperationException("Personel bulunamadı.");

        var kayit = await OdemeEkleAsync(personel, OdemeKanali.Kasa, odemeTarihi, tutar, hakEdisYil, hakEdisAy,
            null, belgeNo, anahtar, "Kasa", null, aciklama, kullanici, ct);
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personelId,
            VarlikTuru = nameof(BankaOdemeKaydi),
            VarlikId = kayit.Id,
            Islem = "KasaOdemesi",
            YeniDeger = $"{tutar:N2} ({hakEdisYil}-{hakEdisAy:D2})",
            DegisiklikNedeni = aciklama,
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
        return kayit;
    }

    public async Task<BankaHareketIceAktarmaSonucu> HareketleriIceAktarAsync(
        int hakEdisYil,
        int hakEdisAy,
        IReadOnlyList<BankaHareketSatiri> satirlar,
        string dosyaAdi,
        string dosyaHash,
        string kullanici,
        CancellationToken ct = default)
    {
        await _donem.EnsureDonemAcikAsync(hakEdisYil, hakEdisAy, ct);

        var ayniDosya = await _db.IceAktarmalar.AnyAsync(i => i.DosyaHashSha256 == dosyaHash && i.KaynakTuru == "BankaHareketExcel", ct);
        var iceAktarma = new IceAktarmaKaydi
        {
            DosyaAdi = dosyaAdi,
            DosyaHashSha256 = dosyaHash,
            KaynakTuru = "BankaHareketExcel",
            DonemYil = hakEdisYil,
            DonemAy = hakEdisAy,
            SatirSayisi = satirlar.Count,
            KullaniciAdi = kullanici
        };
        _db.IceAktarmalar.Add(iceAktarma);

        var personeller = await _db.Personeller.AsNoTracking().ToListAsync(ct);
        var tcMap = personeller.ToDictionary(p => p.TcKimlikNo);
        var ibanMap = personeller.Where(p => !string.IsNullOrWhiteSpace(p.Iban))
            .GroupBy(p => p.Iban!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var mesajlar = new List<string>();
        var tekrarSayaci = new Dictionary<string, int>();
        var eklenen = 0;
        var mukerrer = 0;
        var eslesmeyen = 0;

        foreach (var s in satirlar)
        {
            if (s.Tutar <= 0)
            {
                mesajlar.Add($"Satır {s.SatirNo}: tutar pozitif değil, atlandı.");
                continue;
            }

            var iban = Normalize(s.Iban);
            var tc = new string((s.TcKimlikNo ?? "").Where(char.IsDigit).ToArray());
            string anahtar;
            if (!string.IsNullOrWhiteSpace(s.ReferansNo))
                anahtar = $"REF:{s.ReferansNo.Trim()}";
            else
            {
                var ozet = string.Join("|", s.Tarih.ToString("yyyyMMdd"), iban, s.Tutar.ToString("0.00", CultureInfo.InvariantCulture),
                    tc, (s.Aciklama ?? "").Trim().ToUpperInvariant());
                tekrarSayaci[ozet] = tekrarSayaci.GetValueOrDefault(ozet) + 1;
                anahtar = $"HAR:{ozet}#{tekrarSayaci[ozet]}";
            }

            if (await AnahtarVarMi(anahtar, s.ReferansNo, ct))
            {
                mukerrer++;
                mesajlar.Add($"Satır {s.SatirNo}: aynı banka hareketi daha önce içe aktarılmış — mükerrer kayıt engellendi.");
                continue;
            }

            Personel? personel = null;
            if (tc.Length == 11 && tcMap.TryGetValue(tc, out var pTc))
                personel = pTc;
            else if (!string.IsNullOrEmpty(iban) && ibanMap.TryGetValue(iban, out var adaylar) && adaylar.Count == 1)
                personel = adaylar[0];

            if (personel is null)
            {
                eslesmeyen++;
                mesajlar.Add($"Satır {s.SatirNo}: personel ile eşleşmedi (TC/IBAN). Mutabakatta 'eşleşmeyen hareket' olarak incelenir.");
            }

            await OdemeEkleAsync(personel, OdemeKanali.Banka, s.Tarih, s.Tutar, hakEdisYil, hakEdisAy, iban,
                s.ReferansNo, anahtar, "BankaHareketExcel", iceAktarma.Id, s.Aciklama, kullanici, ct,
                tc.Length > 0 ? tc : null, s.AdSoyad);
            eklenen++;
        }

        iceAktarma.BasariliSatir = eklenen;
        iceAktarma.HataliSatir = satirlar.Count - eklenen;

        var kapanis = await _donem.GetOrCreateAsync(hakEdisYil, hakEdisAy, ct: ct);
        if (kapanis.Durum < MaasDonemDurumu.BankaMutabakatiYapiliyor)
            await _donem.DurumGuncelleAsync(kapanis.Id, MaasDonemDurumu.BankaMutabakatiYapiliyor, kullanici, ct);

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(IceAktarmaKaydi),
            VarlikId = iceAktarma.Id,
            Islem = "BankaHareketIceAktarma",
            YeniDeger = $"{dosyaAdi} | {hakEdisYil}-{hakEdisAy:D2} | eklenen={eklenen}, mükerrer={mukerrer}, eşleşmeyen={eslesmeyen}",
            KullaniciAdi = kullanici
        });

        await _db.SaveChangesAsync(ct);

        if (ayniDosya)
            mesajlar.Insert(0, "Bu dosya daha önce yüklenmiş; yalnızca yeni hareketler eklendi.");

        return new BankaHareketIceAktarmaSonucu
        {
            IceAktarmaId = iceAktarma.Id,
            ToplamSatir = satirlar.Count,
            Eklenen = eklenen,
            MukerrerAtlanan = mukerrer,
            EslesmeyenKayit = eslesmeyen,
            AyniDosyaDahaOnceYuklendi = ayniDosya,
            Mesajlar = mesajlar
        };
    }

    public async Task<IReadOnlyList<BankaOdemeKaydi>> DonemOdemeleriAsync(int hakEdisYil, int hakEdisAy, CancellationToken ct = default) =>
        await _db.BankaOdemeleri.AsNoTracking()
            .Where(o => o.HakEdisDonemYil == hakEdisYil && o.HakEdisDonemAy == hakEdisAy)
            .OrderBy(o => o.OdemeTarihi)
            .ToListAsync(ct);

    private async Task<bool> AnahtarVarMi(string anahtar, string? referansNo, CancellationToken ct)
    {
        if (_db.BankaOdemeleri.Local.Any(o => o.TekillikAnahtari == anahtar)) return true;
        if (await _db.BankaOdemeleri.AnyAsync(o => o.TekillikAnahtari == anahtar, ct)) return true;
        // Eski sürüm kayıtları (tekillik anahtarı olmayan) için referans kontrolü
        return !string.IsNullOrWhiteSpace(referansNo) &&
               await _db.BankaOdemeleri.AnyAsync(o => o.ReferansNo == referansNo && o.TekillikAnahtari == null, ct);
    }

    private async Task<BankaOdemeKaydi> OdemeEkleAsync(
        Personel? personel, OdemeKanali kanal, DateOnly tarih, decimal tutar, int yil, int ay,
        string? iban, string? referans, string anahtar, string kaynakTuru, Guid? iceAktarmaId,
        string? aciklama, string kullanici, CancellationToken ct, string? tc = null, string? adSoyad = null)
    {
        var kayit = new BankaOdemeKaydi
        {
            PersonelId = personel?.Id,
            OdemeKanali = kanal,
            OdemeTarihi = tarih,
            Tutar = tutar,
            HakEdisDonemYil = yil,
            HakEdisDonemAy = ay,
            Iban = Normalize(iban),
            ReferansNo = referans,
            TekillikAnahtari = anahtar,
            KaynakTuru = kaynakTuru,
            IceAktarmaId = iceAktarmaId,
            Aciklama = aciklama,
            TcKimlikNo = tc ?? personel?.TcKimlikNo,
            AdSoyad = adSoyad ?? personel?.AdSoyad,
            KullaniciAdi = kullanici,
            Siniflandirma = OdemeSiniflandirma.NormalHakEdisOdemesi
        };
        _db.BankaOdemeleri.Add(kayit);

        if (personel is not null)
        {
            var tur = kanal == OdemeKanali.Kasa ? MaliIslemTuru.KasaOdemesi : MaliIslemTuru.BankaMaasOdemesi;
            await _defter.DeftereEkleAsync(MaliHareketKurallari.Olustur(
                personel.Id,
                MaliHareketKurallari.IslemKimligi(kanal == OdemeKanali.Kasa ? "KasaOdeme" : "BankaOdeme", personel.Id,
                    referans is { Length: > 0 } && kanal == OdemeKanali.Banka ? referans : kayit.Id.ToString("N")),
                tarih, yil, ay, tur, BakiyeTuru.MaasAlacagi, tutar,
                $"{(kanal == OdemeKanali.Kasa ? "Kasa" : "Banka")} ödemesi (hak ediş {yil}-{ay:D2})" +
                (string.IsNullOrWhiteSpace(aciklama) ? "" : $" — {aciklama}"),
                nameof(BankaOdemeKaydi), kayit.Id.ToString(), referans, kullanici), true, ct);
        }

        return kayit;
    }

    private static string? Normalize(string? iban) =>
        string.IsNullOrWhiteSpace(iban) ? null : new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
}
