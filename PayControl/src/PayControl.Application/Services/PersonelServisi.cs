using Microsoft.EntityFrameworkCore;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class PersonelServisi : IPersonelServisi
{
    private readonly IPayControlDbContext _db;

    public PersonelServisi(IPayControlDbContext db) => _db = db;

    public async Task<IReadOnlyList<PersonelOzetDto>> ListeleAsync(
        string? arama = null,
        string? sirket = null,
        string? isyeri = null,
        CancellationToken ct = default)
    {
        var q = _db.Personeller.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(arama))
        {
            var a = arama.Trim();
            q = q.Where(p =>
                p.AdSoyad.Contains(a) ||
                p.TcKimlikNo.Contains(a) ||
                (p.SicilNo != null && p.SicilNo.Contains(a)));
        }

        if (!string.IsNullOrWhiteSpace(sirket))
            q = q.Where(p => p.Sirket == sirket);

        if (!string.IsNullOrWhiteSpace(isyeri))
            q = q.Where(p => p.Isyeri == isyeri);

        return await q
            .OrderBy(p => p.AdSoyad)
            .Select(p => new PersonelOzetDto
            {
                Id = p.Id,
                TcKimlikNo = p.TcKimlikNo,
                AdSoyad = p.AdSoyad,
                SicilNo = p.SicilNo,
                Sirket = p.Sirket,
                Isyeri = p.Isyeri,
                Gorevi = p.Gorevi,
                CalismaDurumu = p.CalismaDurumu,
                GuncelMaas = p.GuncelMaas,
                Iban = p.Iban
            })
            .ToListAsync(ct);
    }

    public async Task<PersonelDetayDto?> GetirAsync(Guid id, CancellationToken ct = default)
    {
        var p = await _db.Personeller
            .AsNoTracking()
            .Include(x => x.CalismaDonemleri)
            .Include(x => x.MaasDonemKayitlari).ThenInclude(m => m.Kalemler)
            .Include(x => x.IstenCikisKayitlari)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        return p is null ? null : MapDetay(p);
    }

    public async Task<PersonelDetayDto> OlusturAsync(PersonelOlusturDto dto, string kullaniciAdi, CancellationToken ct = default)
    {
        var tc = NormalizeTc(dto.TcKimlikNo);
        if (await _db.Personeller.AnyAsync(p => p.TcKimlikNo == tc, ct))
            throw new InvalidOperationException($"Bu TC kimlik numarası zaten kayıtlı: {tc}");

        var personel = new Personel
        {
            TcKimlikNo = tc,
            AdSoyad = dto.AdSoyad.Trim(),
            SicilNo = dto.SicilNo,
            Sirket = dto.Sirket,
            Isyeri = dto.Isyeri,
            Gorevi = dto.Gorevi,
            GuncelMaas = dto.GuncelMaas,
            BankaAdi = dto.BankaAdi,
            Iban = NormalizeIban(dto.Iban),
            Notlar = dto.Notlar,
            CalismaDurumu = CalismaDurumu.Aktif
        };

        if (dto.IseGirisTarihi.HasValue)
        {
            PersonelDonemKurallari.YeniDonemAc(personel, dto.IseGirisTarihi.Value, dto.Isyeri, dto.Gorevi, dto.Sirket);
        }

        _db.Personeller.Add(personel);
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personel.Id,
            VarlikTuru = nameof(Personel),
            VarlikId = personel.Id,
            Islem = "Olusturma",
            KullaniciAdi = kullaniciAdi,
            YeniDeger = personel.AdSoyad
        });

        await _db.SaveChangesAsync(ct);
        return (await GetirAsync(personel.Id, ct))!;
    }

    public async Task<PersonelDetayDto> GuncelleAsync(
        Guid id,
        PersonelGuncelleDto dto,
        string kullaniciAdi,
        string? neden,
        CancellationToken ct = default)
    {
        var personel = await _db.Personeller.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException("Personel bulunamadı.");

        KaydetDegisiklik(personel, nameof(Personel.AdSoyad), personel.AdSoyad, dto.AdSoyad, kullaniciAdi, neden);
        personel.AdSoyad = dto.AdSoyad.Trim();

        KaydetDegisiklik(personel, nameof(Personel.GuncelMaas), personel.GuncelMaas?.ToString(), dto.GuncelMaas?.ToString(), kullaniciAdi, neden);
        // Kart üzerindeki güncel maaş değişir; geçmiş dönem MaasDonemKaydi kayıtları dokunulmaz.
        personel.GuncelMaas = dto.GuncelMaas;

        KaydetDegisiklik(personel, nameof(Personel.Iban), personel.Iban, dto.Iban, kullaniciAdi, neden);
        personel.SicilNo = dto.SicilNo;
        personel.Sirket = dto.Sirket;
        personel.Isyeri = dto.Isyeri;
        personel.Gorevi = dto.Gorevi;
        personel.BankaAdi = dto.BankaAdi;
        personel.Iban = NormalizeIban(dto.Iban);
        personel.Notlar = dto.Notlar;
        personel.GuncellemeZamani = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (await GetirAsync(id, ct))!;
    }

    private void KaydetDegisiklik(
        Personel personel,
        string alan,
        string? eski,
        string? yeni,
        string kullanici,
        string? neden)
    {
        if (string.Equals(eski, yeni, StringComparison.Ordinal))
            return;

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personel.Id,
            VarlikTuru = nameof(Personel),
            VarlikId = personel.Id,
            Islem = "Guncelleme",
            AlanAdi = alan,
            EskiDeger = eski,
            YeniDeger = yeni,
            DegisiklikNedeni = neden,
            KullaniciAdi = kullanici
        });
    }

    public static string NormalizeTc(string tc)
    {
        var digits = new string((tc ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length != 11)
            throw new InvalidOperationException("TC kimlik numarası 11 haneli olmalıdır.");
        return digits;
    }

    public static string? NormalizeIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban))
            return null;
        return new string(iban.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
    }

    private static PersonelDetayDto MapDetay(Personel p) => new()
    {
        Id = p.Id,
        TcKimlikNo = p.TcKimlikNo,
        AdSoyad = p.AdSoyad,
        SicilNo = p.SicilNo,
        Sirket = p.Sirket,
        Isyeri = p.Isyeri,
        Gorevi = p.Gorevi,
        IlkIseGirisTarihi = p.IlkIseGirisTarihi,
        SonCikisTarihi = p.SonCikisTarihi,
        CalismaDurumu = p.CalismaDurumu,
        GuncelMaas = p.GuncelMaas,
        GuncelMaasTuru = p.GuncelMaasTuru,
        BankaAdi = p.BankaAdi,
        Iban = p.Iban,
        Notlar = p.Notlar,
        CalismaDonemleri = p.CalismaDonemleri
            .OrderByDescending(d => d.IseGirisTarihi)
            .Select(d => new CalismaDonemiDto
            {
                Id = d.Id,
                IseGirisTarihi = d.IseGirisTarihi,
                IstenCikisTarihi = d.IstenCikisTarihi,
                Isyeri = d.Isyeri,
                Gorevi = d.Gorevi,
                Sirket = d.Sirket,
                Durum = d.Durum
            }).ToList(),
        MaasGecmisi = p.MaasDonemKayitlari
            .OrderByDescending(m => m.DonemYil).ThenByDescending(m => m.DonemAy)
            .Select(m => new MaasDonemDto
            {
                Id = m.Id,
                DonemYil = m.DonemYil,
                DonemAy = m.DonemAy,
                MaasTutar = m.MaasTutar,
                MaasTuru = m.MaasTuru,
                Gun = m.Gun,
                NetIstihkak = m.NetIstihkak,
                Isyeri = m.Isyeri,
                Gorevi = m.Gorevi,
                Iban = m.Iban,
                GirisTarihi = m.GirisTarihi,
                CikisTarihi = m.CikisTarihi,
                Kalemler = m.Kalemler.Select(k => new MaasKalemDto
                {
                    Ad = k.Ad,
                    Tutar = k.Tutar,
                    KesintiMi = k.KesintiMi,
                    NetIstihkakaDahil = k.NetIstihkakaDahil
                }).ToList()
            }).ToList(),
        CikisKayitlari = p.IstenCikisKayitlari
            .OrderByDescending(c => c.IstenCikisTarihi)
            .Select(c => new IstenCikisDto
            {
                Id = c.Id,
                FiiliSonCalismaTarihi = c.FiiliSonCalismaTarihi,
                IstenCikisTarihi = c.IstenCikisTarihi,
                SgkCikisDurumu = c.SgkCikisDurumu,
                SgkBildirgeTarihi = c.SgkBildirgeTarihi,
                CikisNedeni = c.CikisNedeni,
                SonBordroDonemYil = c.SonBordroDonemYil,
                SonBordroDonemAy = c.SonBordroDonemAy,
                SonMaasOdemeDurumu = c.SonMaasOdemeDurumu,
                KalanAvans = c.KalanAvans,
                IzinUcreti = c.IzinUcreti,
                KidemTazminati = c.KidemTazminati,
                IhbarTazminati = c.IhbarTazminati,
                Aciklama = c.Aciklama
            }).ToList()
    };
}
