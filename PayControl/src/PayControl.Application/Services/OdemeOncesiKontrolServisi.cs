using System.Security.Cryptography;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class OdemeOncesiKontrolServisi : IOdemeOncesiKontrolServisi
{
    private readonly IPayControlDbContext _db;
    private readonly IDonemKapanisServisi _donem;

    public OdemeOncesiKontrolServisi(IPayControlDbContext db, IDonemKapanisServisi donem)
    {
        _db = db;
        _donem = donem;
    }

    public async Task<OdemeKontrolOturumu> KontrolBaslatAsync(
        int donemYil,
        int donemAy,
        IReadOnlyList<BankaTalimatSatiri> bankaTalimatlari,
        string? bankaDosyaAdi,
        string? bankaHash,
        string kullanici,
        CancellationToken ct = default)
    {
        await _donem.EnsureDonemAcikAsync(donemYil, donemAy, ct);

        var bordrolar = await _db.MaasDonemKayitlari
            .Include(m => m.Kalemler)
            .Where(m => m.DonemYil == donemYil && m.DonemAy == donemAy)
            .ToListAsync(ct);

        var personeller = await _db.Personeller.ToListAsync(ct);
        var oncekiOdemeler = await _db.BankaOdemeleri
            .Where(o => o.HakEdisDonemYil == donemYil && o.HakEdisDonemAy == donemAy)
            .ToListAsync(ct);
        var cikislar = await _db.IstenCikisKayitlari.ToListAsync(ct);

        var motor = new OdemeOncesiKontrolMotoru();
        var satirlar = motor.Karsilastir(donemYil, donemAy, bordrolar, bankaTalimatlari, personeller, oncekiOdemeler, cikislar);

        var oturum = new OdemeKontrolOturumu
        {
            DonemYil = donemYil,
            DonemAy = donemAy,
            BankaDosyaAdi = bankaDosyaAdi,
            BankaDosyaHash = bankaHash,
            OlusturanKullanici = kullanici,
            Satirlar = satirlar.ToList()
        };

        foreach (var s in oturum.Satirlar)
        {
            if (s.Sonuc == OdemeKontrolSonucu.Uygun && !s.KritikMi)
                s.Onaylandi = true;
        }

        _db.OdemeKontrolOturumlari.Add(oturum);

        var kapanis = await _donem.GetOrCreateAsync(donemYil, donemAy, ct: ct);
        if (kapanis.Durum is MaasDonemDurumu.Hazirlaniyor or MaasDonemDurumu.BordroYuklendi)
            await _donem.DurumGuncelleAsync(kapanis.Id, MaasDonemDurumu.OdemeOncesiKontrolde, kullanici, ct);

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(OdemeKontrolOturumu),
            VarlikId = oturum.Id,
            Islem = "OdemeOncesiKontrol",
            YeniDeger = $"{donemYil}-{donemAy:D2} | {oturum.Satirlar.Count} satır | kritik={oturum.Satirlar.Count(s => s.KritikMi)}",
            KullaniciAdi = kullanici
        });

        await _db.SaveChangesAsync(ct);
        return oturum;
    }

    public async Task IstisnaOnaylaAsync(
        Guid satirId,
        string gerekce,
        string onaylayan,
        CancellationToken ct = default)
    {
        var satir = await _db.OdemeKontrolSatirlari.FirstOrDefaultAsync(s => s.Id == satirId, ct)
            ?? throw new InvalidOperationException("Kontrol satırı bulunamadı.");

        if (satir.EslesmeBelirsiz)
            throw new InvalidOperationException(
                "Kimlik veya ödeme sahibi eşleşmesi belirsiz kayıtlar istisna ile onaylanamaz.");

        if (string.IsNullOrWhiteSpace(gerekce))
            throw new InvalidOperationException("İstisna gerekçesi zorunludur.");

        satir.IstisnaOnayi = true;
        satir.IstisnaGerekcesi = gerekce;
        satir.IstisnaOnaylayan = onaylayan;
        satir.IstisnaOnayZamani = DateTime.UtcNow;
        satir.Onaylandi = true;

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = satir.PersonelId,
            VarlikTuru = nameof(OdemeKontrolSatiri),
            VarlikId = satir.Id,
            Islem = "IstisnaOnayi",
            DegisiklikNedeni = gerekce,
            KullaniciAdi = onaylayan,
            YeniDeger = satir.Sonuc.ToString()
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<OnayliOdemeListesi> OnayliListeUretAsync(
        Guid oturumId,
        string kullanici,
        string hedefKlasor,
        CancellationToken ct = default)
    {
        var oturum = await _db.OdemeKontrolOturumlari
            .Include(o => o.Satirlar)
            .Include(o => o.OnayliListeler)
            .FirstOrDefaultAsync(o => o.Id == oturumId, ct)
            ?? throw new InvalidOperationException("Kontrol oturumu bulunamadı.");

        var kritikCozulmemis = oturum.Satirlar
            .Where(s => s.KritikMi && !s.OnayliListeyeAlinabilir)
            .ToList();

        if (kritikCozulmemis.Count > 0)
            throw new InvalidOperationException(
                $"Kritik uyarılı {kritikCozulmemis.Count} kayıt çözülmeden onaylı banka ödeme listesi oluşturulamaz.");

        var onayliSatirlar = oturum.Satirlar
            .Where(s => s.OnayliListeyeAlinabilir && s.BankaOdemeTutari.HasValue && s.PersonelId.HasValue)
            .ToList();

        Directory.CreateDirectory(hedefKlasor);
        var surum = oturum.OnayliListeler.Count == 0 ? 1 : oturum.OnayliListeler.Max(x => x.Surum) + 1;
        var dosyaAdi = $"OnayliOdeme_{oturum.DonemYil}-{oturum.DonemAy:D2}_v{surum}.xlsx";
        var yol = Path.Combine(hedefKlasor, dosyaAdi);

        using (var wb = new XLWorkbook())
        {
            var ws = wb.AddWorksheet("OnayliOdeme");
            ws.Cell(1, 1).Value = "T.C. KİMLİK NO";
            ws.Cell(1, 2).Value = "ADI SOYADI";
            ws.Cell(1, 3).Value = "IBAN NO";
            ws.Cell(1, 4).Value = "TUTAR";
            ws.Cell(1, 5).Value = "DÖNEM";
            ws.Cell(1, 6).Value = "KONTROL";
            var r = 2;
            foreach (var s in onayliSatirlar)
            {
                ws.Cell(r, 1).Value = s.TcKimlikNo;
                ws.Cell(r, 2).Value = s.AdSoyad;
                ws.Cell(r, 3).Value = s.BankaIban ?? s.KartIban;
                ws.Cell(r, 4).Value = s.BankaOdemeTutari;
                ws.Cell(r, 5).Value = $"{oturum.DonemYil}-{oturum.DonemAy:D2}";
                ws.Cell(r, 6).Value = s.IstisnaOnayi ? $"İstisna: {s.IstisnaGerekcesi}" : "Uygun";
                r++;
            }
            wb.SaveAs(yol);
        }

        var bytes = await File.ReadAllBytesAsync(yol, ct);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));

        var liste = new OnayliOdemeListesi
        {
            OdemeKontrolOturumuId = oturum.Id,
            Surum = surum,
            DonemYil = oturum.DonemYil,
            DonemAy = oturum.DonemAy,
            DosyaAdi = dosyaAdi,
            DosyaYolu = yol,
            DosyaHashSha256 = hash,
            SatirSayisi = onayliSatirlar.Count,
            ToplamTutar = onayliSatirlar.Sum(s => s.BankaOdemeTutari ?? 0),
            OlusturanKullanici = kullanici,
            KaynakFormatNotu = "Yüklenen banka formatına uyumlu temel sütunlar (TC, Ad, IBAN, Tutar)"
        };

        oturum.KontrolTamamlandi = true;
        oturum.OnayZamani = DateTime.UtcNow;
        oturum.OnaylayanKullanici = kullanici;
        _db.OnayliOdemeListeleri.Add(liste);

        var kapanis = await _donem.GetOrCreateAsync(oturum.DonemYil, oturum.DonemAy, ct: ct);
        await _donem.DurumGuncelleAsync(kapanis.Id, MaasDonemDurumu.OdemeListesiOnaylandi, kullanici, ct);

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(OnayliOdemeListesi),
            VarlikId = liste.Id,
            Islem = "OnayliListeUretildi",
            YeniDeger = $"{dosyaAdi} | v{surum} | {liste.ToplamTutar:N2}",
            KullaniciAdi = kullanici
        });

        await _db.SaveChangesAsync(ct);
        return liste;
    }

    public async Task<OdemeKontrolOturumu?> GetirAsync(Guid oturumId, CancellationToken ct = default) =>
        await _db.OdemeKontrolOturumlari
            .Include(o => o.Satirlar)
            .Include(o => o.OnayliListeler)
            .FirstOrDefaultAsync(o => o.Id == oturumId, ct);
}
