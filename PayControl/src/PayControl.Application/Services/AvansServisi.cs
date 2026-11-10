using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class AvansServisi : IAvansServisi
{
    private readonly IPayControlDbContext _db;
    private readonly IMaliHareketServisi _defter;

    public AvansServisi(IPayControlDbContext db, IMaliHareketServisi defter)
    {
        _db = db;
        _defter = defter;
    }

    public async Task<AvansKaydi> VerAsync(
        Guid personelId,
        DateOnly tarih,
        decimal tutar,
        int taksitSayisi,
        decimal aylikMahsup,
        string? tur,
        string kullanici,
        string? aciklama = null,
        CancellationToken ct = default)
    {
        if (tutar <= 0) throw new InvalidOperationException("Avans tutarı pozitif olmalıdır.");
        if (taksitSayisi < 1) throw new InvalidOperationException("Taksit sayısı en az 1 olmalıdır.");

        _ = await _db.Personeller.FirstOrDefaultAsync(p => p.Id == personelId, ct)
            ?? throw new InvalidOperationException("Personel bulunamadı.");

        var aylik = aylikMahsup > 0 ? aylikMahsup : Math.Round(tutar / taksitSayisi, 2);
        var avans = new AvansKaydi
        {
            PersonelId = personelId,
            AvansTarihi = tarih,
            Tutar = tutar,
            AvansTuru = tur ?? "Normal maaş avansı",
            TaksitSayisi = taksitSayisi,
            AylikMahsupTutari = aylik,
            KalanBorc = tutar,
            Aciklama = aciklama,
            OlusturanKullanici = kullanici
        };

        // Mahsup planı — planlanan, gerçekleşmiş sayılmaz. Son taksit yuvarlama farkını kapatır.
        var kalanPlan = tutar;
        for (var i = 0; i < taksitSayisi && kalanPlan > 0; i++)
        {
            var planTarih = tarih.AddMonths(i + 1);
            var planTutar = i == taksitSayisi - 1 ? kalanPlan : Math.Min(aylik, kalanPlan);
            kalanPlan -= planTutar;
            avans.Mahsuplar.Add(new AvansMahsupKaydi
            {
                DonemYil = planTarih.Year,
                DonemAy = planTarih.Month,
                PlanlananTutar = planTutar
            });
        }

        _db.Avanslar.Add(avans);

        var islemKimligi = MaliHareketKurallari.IslemKimligi("AvansVerildi", personelId, avans.Id.ToString("N"));
        await _defter.DeftereEkleAsync(MaliHareketKurallari.Olustur(
            personelId, islemKimligi, tarih, tarih.Year, tarih.Month,
            MaliIslemTuru.AvansVerildi, BakiyeTuru.AvansBorcu, tutar,
            $"Avans verildi ({avans.AvansTuru}), {taksitSayisi} taksit, aylık {aylik:N2}",
            nameof(AvansKaydi), avans.Id.ToString(), null, kullanici), true, ct);

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personelId,
            VarlikTuru = nameof(AvansKaydi),
            VarlikId = avans.Id,
            Islem = "AvansVerildi",
            YeniDeger = tutar.ToString("N2"),
            DegisiklikNedeni = aciklama,
            KullaniciAdi = kullanici
        });

        await _db.SaveChangesAsync(ct);
        return avans;
    }

    public async Task<AvansMahsupKaydi> MahsupGerceklestirAsync(
        Guid avansId,
        int donemYil,
        int donemAy,
        decimal tutar,
        string kullanici,
        bool bordroyaGirildi = true,
        CancellationToken ct = default)
    {
        if (tutar <= 0) throw new InvalidOperationException("Mahsup tutarı pozitif olmalıdır.");

        var avans = await _db.Avanslar.Include(a => a.Mahsuplar)
            .FirstOrDefaultAsync(a => a.Id == avansId, ct)
            ?? throw new InvalidOperationException("Avans bulunamadı.");

        var mevcut = avans.Mahsuplar.FirstOrDefault(m =>
            m.DonemYil == donemYil && m.DonemAy == donemAy && m.GerceklesenTutar.HasValue);
        if (mevcut is not null)
            throw new InvalidOperationException(
                $"Bu avans için {donemYil}-{donemAy:D2} döneminde mahsup zaten gerçekleşmiş" +
                (mevcut.KaynakIceAktarmaId.HasValue ? " (bordrodan)" : "") + ". Çift mahsup engellendi.");

        if (tutar > avans.KalanBorc + 0.005m)
            throw new InvalidOperationException($"Mahsup tutarı ({tutar:N2}) kalan avans borcunu ({avans.KalanBorc:N2}) aşamaz.");

        var plan = PlanBulVeyaOlustur(avans, donemYil, donemAy, tutar);
        plan.GerceklesenTutar = tutar;
        plan.BordroyaGirildi = bordroyaGirildi;
        plan.GerceklesmeTarihi = new DateOnly(donemYil, donemAy, 1);
        plan.GerceklestirenKullanici = kullanici;
        avans.KalanBorc = Math.Max(0, avans.KalanBorc - tutar);

        var islemKimligi = MaliHareketKurallari.IslemKimligi(
            "AvansMahsup", avans.PersonelId, $"{avans.Id:N}:{donemYil}-{donemAy:D2}");
        await _defter.DeftereEkleAsync(MaliHareketKurallari.Olustur(
            avans.PersonelId, islemKimligi, SonGun(donemYil, donemAy),
            donemYil, donemAy, MaliIslemTuru.AvansMahsup, BakiyeTuru.AvansBorcu, tutar,
            $"Avans mahsubu ({avans.AvansTarihi:dd.MM.yyyy} tarihli avans). Kalan borç: {avans.KalanBorc:N2}",
            nameof(AvansMahsupKaydi), plan.Id.ToString(), null, kullanici), true, ct);

        await _db.SaveChangesAsync(ct);
        return plan;
    }

    public async Task<BordroAvansMahsupSonucu> BordroMahsupuUygulaAsync(
        Guid personelId, int donemYil, int donemAy, decimal kesinti, Guid iceAktarmaId, string? dosyaAdi,
        string kullanici, CancellationToken ct = default)
    {
        var uyarilar = new List<string>();
        var donemSonu = SonGun(donemYil, donemAy);
        var avanslar = await _db.Avanslar.Include(a => a.Mahsuplar)
            .Where(a => a.PersonelId == personelId && a.AvansTarihi <= donemSonu)
            .OrderBy(a => a.AvansTarihi).ThenBy(a => a.OlusturmaZamani)
            .ToListAsync(ct);

        // Aynı dönem için elle yapılmış mahsup varsa bordro kesintisi aynı tahsilattır; ikinci kez düşülmez.
        var dahaOnce = avanslar.SelectMany(a => a.Mahsuplar)
            .Where(m => m.DonemYil == donemYil && m.DonemAy == donemAy && m.GerceklesenTutar.HasValue)
            .Sum(m => m.GerceklesenTutar!.Value);

        var uygulanacak = kesinti - dahaOnce;
        if (dahaOnce > 0)
            uyarilar.Add($"{donemYil}-{donemAy:D2} için daha önce {dahaOnce:N2} TL mahsup işlenmiş; bordro kesintisinden düşüldü.");
        if (uygulanacak < -0.005m)
            uyarilar.Add($"Elle işlenen mahsup ({dahaOnce:N2}) bordrodaki kesintiden ({kesinti:N2}) fazla.");

        var uygulanan = 0m;
        foreach (var avans in avanslar.Where(a => a.KalanBorc > 0))
        {
            if (uygulanacak <= 0.005m) break;
            if (avans.Mahsuplar.Any(m => m.DonemYil == donemYil && m.DonemAy == donemAy && m.GerceklesenTutar.HasValue))
                continue;

            var tutar = Math.Min(avans.KalanBorc, uygulanacak);
            var plan = PlanBulVeyaOlustur(avans, donemYil, donemAy, tutar);
            plan.GerceklesenTutar = tutar;
            plan.BordroyaGirildi = true;
            plan.GerceklesmeTarihi = donemSonu;
            plan.KaynakIceAktarmaId = iceAktarmaId;
            plan.GerceklestirenKullanici = kullanici;
            plan.Aciklama = $"Bordro avans kesintisi ({dosyaAdi})";
            avans.KalanBorc -= tutar;
            uygulanacak -= tutar;
            uygulanan += tutar;

            await _defter.DeftereEkleAsync(MaliHareketKurallari.Olustur(
                personelId,
                MaliHareketKurallari.IslemKimligi("AvansMahsup", personelId, $"{avans.Id:N}:{donemYil}-{donemAy:D2}:{iceAktarmaId:N}"),
                donemSonu, donemYil, donemAy, MaliIslemTuru.AvansMahsup, BakiyeTuru.AvansBorcu, tutar,
                $"Bordrodan avans mahsubu ({avans.AvansTarihi:dd.MM.yyyy} tarihli avans). Kalan borç: {avans.KalanBorc:N2}",
                nameof(AvansMahsupKaydi), iceAktarmaId.ToString(), dosyaAdi, kullanici), true, ct);
        }

        var asan = Math.Max(0, uygulanacak);
        if (asan > 0.005m)
        {
            uyarilar.Add($"Bordrodaki avans kesintisi açık avans borcunu {asan:N2} TL aşıyor.");
            await _defter.DeftereEkleAsync(MaliHareketKurallari.Olustur(
                personelId,
                MaliHareketKurallari.IslemKimligi("AvansKesintiFazla", personelId, $"{donemYil}-{donemAy:D2}:{iceAktarmaId:N}"),
                donemSonu, donemYil, donemAy, MaliIslemTuru.BordroKalemBilgisi, BakiyeTuru.Bilgi, asan,
                $"UYARI: Bordroda açık avans borcunu aşan avans kesintisi ({asan:N2} TL). İnceleyin.",
                nameof(IceAktarmaKaydi), iceAktarmaId.ToString(), dosyaAdi, kullanici), false, ct);
        }

        return new BordroAvansMahsupSonucu
        {
            BordroKesintisi = kesinti,
            Uygulanan = uygulanan,
            DahaOnceIslenmis = dahaOnce,
            AcikBorcuAsan = asan,
            Uyarilar = uyarilar
        };
    }

    public async Task BordroMahsupuGeriAlAsync(Guid iceAktarmaId, string kullanici, CancellationToken ct = default)
    {
        var planlar = await _db.AvansMahsuplari.Include(m => m.AvansKaydi)
            .Where(m => m.KaynakIceAktarmaId == iceAktarmaId && m.GerceklesenTutar.HasValue)
            .ToListAsync(ct);

        foreach (var plan in planlar)
        {
            plan.AvansKaydi!.KalanBorc += plan.GerceklesenTutar!.Value;
            plan.Aciklama = $"Bordro geri alındı; {plan.GerceklesenTutar:N2} mahsup iade edildi.";
            plan.GerceklesenTutar = null;
            plan.GerceklesmeTarihi = null;
            plan.BordroyaGirildi = false;
            plan.KaynakIceAktarmaId = null;
        }

        var hareketler = await _db.MaliHareketler.AsNoTracking()
            .Where(h => h.KaynakTuru == nameof(AvansMahsupKaydi) && h.KaynakReferans == iceAktarmaId.ToString() && !h.TersKayitMi)
            .ToListAsync(ct);
        foreach (var h in hareketler)
        {
            await _defter.DeftereEkleAsync(MaliHareketKurallari.TersKayit(
                h, "Bordro içe aktarma geri alındı", kullanici, DateOnly.FromDateTime(DateTime.Today)), false, ct);
        }
    }

    public async Task<IReadOnlyList<AvansKaydi>> ListeleAsync(Guid personelId, CancellationToken ct = default) =>
        await _db.Avanslar.AsNoTracking()
            .Include(a => a.Mahsuplar)
            .Where(a => a.PersonelId == personelId)
            .OrderByDescending(a => a.AvansTarihi)
            .ToListAsync(ct);

    private AvansMahsupKaydi PlanBulVeyaOlustur(AvansKaydi avans, int yil, int ay, decimal tutar)
    {
        var plan = avans.Mahsuplar.FirstOrDefault(m => m.DonemYil == yil && m.DonemAy == ay);
        if (plan is not null) return plan;
        plan = new AvansMahsupKaydi { AvansKaydiId = avans.Id, DonemYil = yil, DonemAy = ay, PlanlananTutar = tutar };
        avans.Mahsuplar.Add(plan);
        _db.AvansMahsuplari.Add(plan);
        return plan;
    }

    private static DateOnly SonGun(int yil, int ay) => new(yil, ay, DateTime.DaysInMonth(yil, ay));
}
