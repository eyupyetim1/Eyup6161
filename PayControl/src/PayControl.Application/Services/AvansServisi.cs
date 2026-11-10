using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class AvansServisi : IAvansServisi
{
    private readonly IPayControlDbContext _db;

    public AvansServisi(IPayControlDbContext db) => _db = db;

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

        var avans = new AvansKaydi
        {
            PersonelId = personelId,
            AvansTarihi = tarih,
            Tutar = tutar,
            AvansTuru = tur ?? "Normal maaş avansı",
            TaksitSayisi = taksitSayisi,
            AylikMahsupTutari = aylikMahsup > 0 ? aylikMahsup : Math.Round(tutar / taksitSayisi, 2),
            KalanBorc = tutar,
            Aciklama = aciklama,
            OlusturanKullanici = kullanici
        };

        // Mahsup planı — gerçekleşen otomatik düşülmez
        for (var i = 0; i < taksitSayisi; i++)
        {
            var planTarih = tarih.AddMonths(i + 1);
            avans.Mahsuplar.Add(new AvansMahsupKaydi
            {
                DonemYil = planTarih.Year,
                DonemAy = planTarih.Month,
                PlanlananTutar = avans.AylikMahsupTutari,
                GerceklesenTutar = null,
                BordroyaGirildi = false
            });
        }

        _db.Avanslar.Add(avans);

        var islemKimligi = MaliHareketKurallari.IslemKimligi("AvansVerildi", personelId, avans.Id.ToString("N"));
        if (!await _db.MaliHareketler.AnyAsync(h => h.IslemKimligi == islemKimligi, ct))
        {
            _db.MaliHareketler.Add(MaliHareketKurallari.Olustur(
                personelId, islemKimligi, tarih, tarih.Year, tarih.Month,
                MaliIslemTuru.AvansVerildi, BakiyeTuru.AvansBorcu, tutar,
                $"Avans verildi ({tur}). Kalan borç: {tutar:N2}",
                nameof(AvansKaydi), avans.Id.ToString(), null, kullanici));
        }

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personelId,
            VarlikTuru = nameof(AvansKaydi),
            VarlikId = avans.Id,
            Islem = "AvansVerildi",
            YeniDeger = tutar.ToString("N2"),
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
        var avans = await _db.Avanslar.Include(a => a.Mahsuplar)
            .FirstOrDefaultAsync(a => a.Id == avansId, ct)
            ?? throw new InvalidOperationException("Avans bulunamadı.");

        // Aynı dönem için ikinci mahsup engeli
        var mevcut = avans.Mahsuplar.FirstOrDefault(m =>
            m.DonemYil == donemYil && m.DonemAy == donemAy && m.GerceklesenTutar.HasValue);
        if (mevcut is not null)
            throw new InvalidOperationException(
                $"Bu avans için {donemYil}-{donemAy:D2} döneminde mahsup zaten gerçekleşmiş. Çift mahsup engellendi.");

        var plan = avans.Mahsuplar.FirstOrDefault(m => m.DonemYil == donemYil && m.DonemAy == donemAy);
        if (plan is null)
        {
            plan = new AvansMahsupKaydi
            {
                AvansKaydiId = avans.Id,
                DonemYil = donemYil,
                DonemAy = donemAy,
                PlanlananTutar = tutar
            };
            avans.Mahsuplar.Add(plan);
            _db.AvansMahsuplari.Add(plan);
        }

        plan.GerceklesenTutar = tutar;
        plan.BordroyaGirildi = bordroyaGirildi;
        plan.GerceklesmeTarihi = new DateOnly(donemYil, donemAy, 1);
        avans.KalanBorc = Math.Max(0, avans.KalanBorc - tutar);

        var islemKimligi = MaliHareketKurallari.IslemKimligi(
            "AvansMahsup", avans.PersonelId, $"{avans.Id:N}:{donemYil}-{donemAy:D2}");

        if (await _db.MaliHareketler.AnyAsync(h => h.IslemKimligi == islemKimligi, ct))
            throw new InvalidOperationException("Aynı avans mahsupu mali harekette zaten kayıtlı (mükerrer engeli).");

        _db.MaliHareketler.Add(MaliHareketKurallari.Olustur(
            avans.PersonelId, islemKimligi, new DateOnly(donemYil, donemAy, DateTime.DaysInMonth(donemYil, donemAy)),
            donemYil, donemAy, MaliIslemTuru.AvansMahsup, BakiyeTuru.AvansBorcu, tutar,
            $"Avans mahsupu. Kalan borç: {avans.KalanBorc:N2}",
            nameof(AvansMahsupKaydi), plan.Id.ToString(), null, kullanici));

        await _db.SaveChangesAsync(ct);
        return plan;
    }

    public async Task<IReadOnlyList<AvansKaydi>> ListeleAsync(Guid personelId, CancellationToken ct = default) =>
        await _db.Avanslar.AsNoTracking()
            .Include(a => a.Mahsuplar)
            .Where(a => a.PersonelId == personelId)
            .OrderByDescending(a => a.AvansTarihi)
            .ToListAsync(ct);
}
