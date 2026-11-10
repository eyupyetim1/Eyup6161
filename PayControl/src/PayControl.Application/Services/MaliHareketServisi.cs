using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class MaliHareketServisi : IMaliHareketServisi
{
    private static readonly string[] KaynakModulTurleri =
        [nameof(AvansKaydi), nameof(AvansMahsupKaydi), nameof(MaasDonemKaydi), nameof(MaasKalemi), nameof(BankaOdemeKaydi), nameof(TahsilatTaksiti)];

    private static readonly MaliIslemTuru[] BordroDisiHakEdisTurleri =
    [
        MaliIslemTuru.Prim, MaliIslemTuru.Ikramiye, MaliIslemTuru.FazlaMesai,
        MaliIslemTuru.KidemTazminati, MaliIslemTuru.IhbarTazminati, MaliIslemTuru.IzinUcreti
    ];

    private readonly IPayControlDbContext _db;
    private readonly IYetkiServisi _yetki;

    public MaliHareketServisi(IPayControlDbContext db, IYetkiServisi yetki)
    {
        _db = db;
        _yetki = yetki;
    }

    public async Task<MaliHareket?> DeftereEkleAsync(MaliHareket hareket, bool mukerrerdeHata = false, CancellationToken ct = default)
    {
        if (hareket.Tutar != decimal.Round(hareket.Tutar, 2))
            hareket.Tutar = decimal.Round(hareket.Tutar, 2, MidpointRounding.AwayFromZero);

        var mukerrer = _db.MaliHareketler.Local.Any(h => h.IslemKimligi == hareket.IslemKimligi) ||
                       await _db.MaliHareketler.AnyAsync(h => h.IslemKimligi == hareket.IslemKimligi, ct);
        if (mukerrer)
        {
            if (mukerrerdeHata)
                throw new InvalidOperationException(
                    $"Bu mali hareket daha önce kaydedilmiş (işlem kimliği: {hareket.IslemKimligi}). Çift kayıt engellendi.");
            return null;
        }

        if (hareket.DonemYil.HasValue && hareket.DonemAy.HasValue &&
            hareket.IslemTuru != MaliIslemTuru.OncekiDonemDuzeltme)
        {
            var kapali = await _db.MaasDonemKapanislari.AnyAsync(d =>
                d.DonemYil == hareket.DonemYil && d.DonemAy == hareket.DonemAy && d.Durum == MaasDonemDurumu.Kapatildi, ct);
            if (kapali)
                throw new KapaliDonemIhlaliException(
                    $"{hareket.DonemYil}-{hareket.DonemAy:D2} dönemi kapatılmıştır. Bu döneme yeni hareket yazılamaz; düzeltme kaydı oluşturun.");
        }

        var personel = _db.Personeller.Local.FirstOrDefault(p => p.Id == hareket.PersonelId)
                       ?? await _db.Personeller.Include(p => p.CalismaDonemleri)
                           .FirstOrDefaultAsync(p => p.Id == hareket.PersonelId, ct)
                       ?? throw new InvalidOperationException("Mali hareket için personel bulunamadı.");

        var donemler = personel.CalismaDonemleri.ToList();
        if (donemler.Count == 0)
            donemler = await _db.CalismaDonemleri.Where(d => d.PersonelId == personel.Id).ToListAsync(ct);

        var referansTarih = hareket.DonemYil.HasValue && hareket.DonemAy.HasValue &&
                            hareket.IslemTuru is not (MaliIslemTuru.AvansVerildi or MaliIslemTuru.IseGiris or MaliIslemTuru.IstenCikis)
            ? new DateOnly(hareket.DonemYil.Value, hareket.DonemAy.Value, 1)
            : hareket.IslemTarihi;
        var donem = hareket.CalismaDonemiId.HasValue
            ? donemler.FirstOrDefault(d => d.Id == hareket.CalismaDonemiId)
            : MaliHareketKurallari.CalismaDonemiBul(donemler, referansTarih);

        hareket.CalismaDonemiId ??= donem?.Id;
        hareket.Sirket ??= donem?.Sirket ?? personel.Sirket;
        hareket.Isyeri ??= donem?.Isyeri ?? personel.Isyeri;

        _db.MaliHareketler.Add(hareket);
        return hareket;
    }

    public async Task<MaliHareket> BordroDisiHakEdisAsync(Guid personelId, MaliIslemTuru tur, DateOnly tarih, int donemYil,
        int donemAy, decimal tutar, string aciklama, string? belge, string kullanici, CancellationToken ct = default)
    {
        if (!BordroDisiHakEdisTurleri.Contains(tur))
            throw new InvalidOperationException("Bordro dışı hak ediş türü prim, ikramiye, fazla mesai, kıdem, ihbar veya izin ücreti olmalıdır.");
        PozitifOlmali(tutar);
        GerekceZorunlu(aciklama, "Açıklama");

        var kimlik = MaliHareketKurallari.IslemKimligi($"BordroDisi:{tur}", personelId, Guid.NewGuid().ToString("N"));
        var h = MaliHareketKurallari.Olustur(personelId, kimlik, tarih, donemYil, donemAy, tur, BakiyeTuru.MaasAlacagi,
            tutar, $"{MaliHareketKurallari.TurEtiketi(tur)} (bordro dışı): {aciklama}", "Manuel", null, belge, kullanici);
        await DeftereEkleAsync(h, true, ct);
        Denetim(personelId, h, "BordroDisiHakEdis", kullanici, aciklama);
        await _db.SaveChangesAsync(ct);
        return h;
    }

    public async Task<MaliHareket> IadeAlAsync(Guid personelId, DateOnly tarih, decimal tutar, BakiyeTuru bakiye,
        string aciklama, string? belge, string kullanici, CancellationToken ct = default)
    {
        PozitifOlmali(tutar);
        GerekceZorunlu(aciklama, "Açıklama");
        if (bakiye == BakiyeTuru.AvansBorcu)
            throw new InvalidOperationException("Avans iadesi avans kartı üzerinden mahsup olarak girilmelidir (kalan borç ve plan birlikte güncellenir).");
        if (bakiye is not (BakiyeTuru.KesinlesmisGeriOdeme or BakiyeTuru.SupheliFazlaOdeme))
            throw new InvalidOperationException("İade yalnızca kesin geri ödeme veya şüpheli fazla ödeme bakiyesinden düşülebilir.");

        var mevcut = await BakiyeTutariAsync(personelId, bakiye, ct);
        if (tutar > mevcut + 0.005m)
            throw new InvalidOperationException(
                $"İade tutarı ({tutar:N2}) ilgili bakiyeyi ({MaliHareketKurallari.BakiyeEtiketi(bakiye)}: {mevcut:N2}) aşamaz.");

        var tur = MaliIslemTuru.IadeAlinan;
        var kimlik = MaliHareketKurallari.IslemKimligi("Iade", personelId, Guid.NewGuid().ToString("N"));
        var h = MaliHareketKurallari.Olustur(personelId, kimlik, tarih, null, null, tur, bakiye, tutar,
            $"İade alındı: {aciklama}", "Manuel", null, belge, kullanici);
        await DeftereEkleAsync(h, true, ct);
        Denetim(personelId, h, "IadeAlindi", kullanici, aciklama);
        await _db.SaveChangesAsync(ct);
        return h;
    }

    public async Task<MaliHareket> DevredenBakiyeAsync(Guid personelId, bool borcMu, BakiyeTuru bakiye, DateOnly tarih,
        int? donemYil, int? donemAy, decimal tutar, string aciklama, string? belge, string kullanici, CancellationToken ct = default)
    {
        PozitifOlmali(tutar);
        GerekceZorunlu(aciklama, "Açıklama");
        if (borcMu && bakiye is not (BakiyeTuru.AvansBorcu or BakiyeTuru.KesinlesmisGeriOdeme))
            throw new InvalidOperationException("Devreden borç avans borcu veya kesin geri ödeme bakiyesine yazılır.");
        if (!borcMu) bakiye = BakiyeTuru.MaasAlacagi;

        var tur = borcMu ? MaliIslemTuru.DevredenBorc : MaliIslemTuru.DevredenAlacak;
        var kimlik = MaliHareketKurallari.IslemKimligi(tur.ToString(), personelId, Guid.NewGuid().ToString("N"));
        var h = MaliHareketKurallari.Olustur(personelId, kimlik, tarih, donemYil, donemAy, tur, bakiye, tutar,
            $"{MaliHareketKurallari.TurEtiketi(tur)}: {aciklama}", "Manuel", null, belge, kullanici);
        await DeftereEkleAsync(h, true, ct);
        Denetim(personelId, h, tur.ToString(), kullanici, aciklama);
        await _db.SaveChangesAsync(ct);
        return h;
    }

    public async Task<MaliHareket?> SupheliFazlaOdemeKaydetAsync(Guid personelId, int donemYil, int donemAy, decimal tutar,
        string aciklama, string kaynakTuru, string? kaynakReferans, string islemKimligiEki, string kullanici,
        bool kaydet = true, CancellationToken ct = default)
    {
        if (tutar == 0) return null;
        var kimlik = MaliHareketKurallari.IslemKimligi("SupheliFazla", personelId, $"{donemYil}-{donemAy:D2}:{islemKimligiEki}");
        var h = MaliHareketKurallari.Olustur(personelId, kimlik, new DateOnly(donemYil, donemAy, 1).AddMonths(1).AddDays(-1),
            donemYil, donemAy, MaliIslemTuru.FazlaOdemeFarki, BakiyeTuru.SupheliFazlaOdeme, tutar,
            aciklama, kaynakTuru, kaynakReferans, null, kullanici);
        var eklendi = await DeftereEkleAsync(h, false, ct);
        if (eklendi is not null && kaydet)
            await _db.SaveChangesAsync(ct);
        return eklendi;
    }

    public async Task FazlaOdemeKesinlestirAsync(Guid personelId, decimal tutar, string gerekce, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Fazla ödeme kesinleştirme", ct);
        PozitifOlmali(tutar);
        GerekceZorunlu(gerekce, "Gerekçe");
        await SupheliYeterliMi(personelId, tutar, ct);

        var anahtar = Guid.NewGuid().ToString("N");
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        var dus = MaliHareketKurallari.Olustur(personelId, MaliHareketKurallari.IslemKimligi("Kesinlesti:S", personelId, anahtar),
            bugun, null, null, MaliIslemTuru.FazlaOdemeKesinlesti, BakiyeTuru.SupheliFazlaOdeme, tutar,
            $"İnceleme sonucu kesinleşti — şüpheli bakiyeden düşüldü: {gerekce}", "FazlaOdemeInceleme", null, null, kullanici);
        var ekle = MaliHareketKurallari.Olustur(personelId, MaliHareketKurallari.IslemKimligi("Kesinlesti:K", personelId, anahtar),
            bugun, null, null, MaliIslemTuru.FazlaOdemeKesinlesti, BakiyeTuru.KesinlesmisGeriOdeme, tutar,
            $"Kesinleşmiş geri ödeme borcu: {gerekce}", "FazlaOdemeInceleme", null, null, kullanici);
        await DeftereEkleAsync(dus, true, ct);
        await DeftereEkleAsync(ekle, true, ct);
        ekle.IlgiliHareketId = dus.Id;
        Denetim(personelId, ekle, "FazlaOdemeKesinlesti", kullanici, gerekce);
        await _db.SaveChangesAsync(ct);
    }

    public async Task FazlaOdemeAciklaAsync(Guid personelId, decimal tutar, MaliIslemTuru hakEdisTuru, int donemYil, int donemAy,
        string gerekce, string? belge, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Fazla ödemeyi belgeli hak ediş olarak açıklama", ct);
        if (!BordroDisiHakEdisTurleri.Contains(hakEdisTuru))
            throw new InvalidOperationException("Açıklama için geçerli bir hak ediş türü seçin (prim, ikramiye, fazla mesai, tazminat, izin).");
        PozitifOlmali(tutar);
        GerekceZorunlu(gerekce, "Gerekçe");
        if (string.IsNullOrWhiteSpace(belge))
            throw new InvalidOperationException("Fazla ödemenin hak ediş olarak kabulü için kaynak belge zorunludur.");
        await SupheliYeterliMi(personelId, tutar, ct);

        var anahtar = Guid.NewGuid().ToString("N");
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        var dus = MaliHareketKurallari.Olustur(personelId, MaliHareketKurallari.IslemKimligi("Aciklandi:S", personelId, anahtar),
            bugun, null, null, MaliIslemTuru.FazlaOdemeAciklandi, BakiyeTuru.SupheliFazlaOdeme, tutar,
            $"Fazla ödeme belgeli hak ediş olarak açıklandı: {gerekce}", "FazlaOdemeInceleme", null, belge, kullanici);
        var hak = MaliHareketKurallari.Olustur(personelId, MaliHareketKurallari.IslemKimligi("Aciklandi:H", personelId, anahtar),
            bugun, donemYil, donemAy, hakEdisTuru, BakiyeTuru.MaasAlacagi, tutar,
            $"{MaliHareketKurallari.TurEtiketi(hakEdisTuru)} — fazla ödeme incelemesi ile belgelendi: {gerekce}", "FazlaOdemeInceleme", null, belge, kullanici);
        await DeftereEkleAsync(dus, true, ct);
        await DeftereEkleAsync(hak, true, ct);
        hak.IlgiliHareketId = dus.Id;
        Denetim(personelId, hak, "FazlaOdemeAciklandi", kullanici, gerekce);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<MaliHareket> TersKayitAsync(Guid hareketId, string neden, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Mali hareket ters kaydı", ct);
        var orijinal = await _db.MaliHareketler.AsNoTracking().FirstOrDefaultAsync(h => h.Id == hareketId, ct)
                       ?? throw new InvalidOperationException("Mali hareket bulunamadı.");

        if (orijinal.KaynakTuru is not null && KaynakModulTurleri.Contains(orijinal.KaynakTuru))
            throw new InvalidOperationException(
                $"Bu hareket {orijinal.KaynakTuru} kaydından üretildi; kaynak modülden iptal edilmelidir (bordro geri alma, avans vb.).");
        if (await _db.MaliHareketler.AnyAsync(h => h.IlgiliHareketId == hareketId && h.TersKayitMi, ct))
            throw new InvalidOperationException("Bu hareketin ters kaydı zaten alınmış.");

        var ters = MaliHareketKurallari.TersKayit(orijinal, neden, kullanici, DateOnly.FromDateTime(DateTime.Today));
        await DeftereEkleAsync(ters, true, ct);
        Denetim(orijinal.PersonelId, ters, "TersKayit", kullanici, neden);
        await _db.SaveChangesAsync(ct);
        return ters;
    }

    public async Task<EkstreOzeti> BakiyeAsync(Guid personelId, CancellationToken ct = default) =>
        MaliHareketKurallari.OzetHesapla(await _db.MaliHareketler.AsNoTracking()
            .Where(h => h.PersonelId == personelId).ToListAsync(ct));

    private async Task<decimal> BakiyeTutariAsync(Guid personelId, BakiyeTuru bakiye, CancellationToken ct)
    {
        var hareketler = await _db.MaliHareketler.AsNoTracking()
            .Where(h => h.PersonelId == personelId && h.BakiyeTuru == bakiye).ToListAsync(ct);
        return hareketler.Sum(MaliHareketKurallari.Etki);
    }

    private async Task SupheliYeterliMi(Guid personelId, decimal tutar, CancellationToken ct)
    {
        var supheli = await BakiyeTutariAsync(personelId, BakiyeTuru.SupheliFazlaOdeme, ct);
        if (tutar > supheli + 0.005m)
            throw new InvalidOperationException($"Tutar ({tutar:N2}) inceleme bekleyen fazla ödeme bakiyesini ({supheli:N2}) aşamaz.");
    }

    private void Denetim(Guid personelId, MaliHareket h, string islem, string kullanici, string? neden) =>
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personelId,
            VarlikTuru = nameof(MaliHareket),
            VarlikId = h.Id,
            Islem = islem,
            YeniDeger = $"{MaliHareketKurallari.TurEtiketi(h.IslemTuru)} {h.Tutar:N2}",
            DegisiklikNedeni = neden,
            KullaniciAdi = kullanici
        });

    private static void PozitifOlmali(decimal tutar)
    {
        if (tutar <= 0) throw new InvalidOperationException("Tutar pozitif olmalıdır.");
    }

    private static void GerekceZorunlu(string? deger, string alan)
    {
        if (string.IsNullOrWhiteSpace(deger)) throw new InvalidOperationException($"{alan} zorunludur.");
    }
}
