using Microsoft.EntityFrameworkCore;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class PersonelServisi : IPersonelServisi
{
    private static readonly System.Globalization.CultureInfo Tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
    private readonly IPayControlDbContext _db;
    private readonly IMaliHareketServisi _defter;

    public PersonelServisi(IPayControlDbContext db, IMaliHareketServisi defter)
    {
        _db = db;
        _defter = defter;
    }

    public async Task<IReadOnlyList<PersonelOzetDto>> ListeleAsync(
        string? arama = null,
        string? sirket = null,
        string? isyeri = null,
        CancellationToken ct = default)
    {
        // Türkçe harf/büyük-küçük harf duyarsız arama bellekte yapılır (SQLite yalnız İngilizce harflerde duyarsızdır)
        var tum = await _db.Personeller.AsNoTracking().ToListAsync(ct);
        var trKarsilastir = StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true);
        return tum
            .Where(p => AramaKurallari.Eslesir(arama, p.AdSoyad, p.TcKimlikNo, p.SicilNo, p.Gorevi, p.Telefon))
            .Where(p => string.IsNullOrWhiteSpace(sirket) || AramaKurallari.Eslesir(sirket, p.Sirket))
            .Where(p => string.IsNullOrWhiteSpace(isyeri) || AramaKurallari.Eslesir(isyeri, p.Isyeri))
            .OrderBy(p => p.AdSoyad, trKarsilastir)
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
            .ToList();
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
            AdSoyad = IsimKurallari.Duzelt(dto.AdSoyad)!,
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

        CalismaDonemi? ilkDonem = null;
        if (dto.IseGirisTarihi.HasValue)
        {
            ilkDonem = PersonelDonemKurallari.YeniDonemAc(personel, dto.IseGirisTarihi.Value, dto.Isyeri, dto.Gorevi, dto.Sirket);
        }

        _db.Personeller.Add(personel);
        if (ilkDonem is not null)
            await IseGirisHareketiAsync(personel, ilkDonem, kullaniciAdi, ct);
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

        if (personel.GuncelMaas != dto.GuncelMaas)
        {
            // Maaş değişikliği ekstrede bilgi satırı olarak görünür; geçmiş dönem hak edişleri değişmez.
            await _defter.DeftereEkleAsync(new MaliHareket
            {
                IslemKimligi = MaliHareketKurallari.IslemKimligi("MaasDegisikligi", personel.Id, Guid.NewGuid().ToString("N")),
                PersonelId = personel.Id,
                IslemTarihi = DateOnly.FromDateTime(DateTime.Today),
                IslemTuru = MaliIslemTuru.MaasDegisikligi,
                BakiyeTuru = BakiyeTuru.Bilgi,
                Tutar = dto.GuncelMaas ?? 0m,
                Aciklama = $"Maaş değişikliği: {personel.GuncelMaas?.ToString("N2", Tr) ?? "-"} → {dto.GuncelMaas?.ToString("N2", Tr) ?? "-"}" +
                           (string.IsNullOrWhiteSpace(neden) ? "" : $" ({neden})"),
                KaynakTuru = nameof(Personel),
                KaynakReferans = personel.Id.ToString(),
                KullaniciAdi = kullaniciAdi
            }, false, ct);
        }

        KaydetDegisiklik(personel, nameof(Personel.AdSoyad), personel.AdSoyad, dto.AdSoyad, kullaniciAdi, neden);
        personel.AdSoyad = IsimKurallari.Duzelt(dto.AdSoyad)!;

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

    public async Task<PersonelDetayDto> YenidenIseAlAsync(
        Guid id,
        DateOnly iseGirisTarihi,
        string? isyeri,
        string? gorevi,
        string? sirket,
        decimal? maas,
        string kullaniciAdi,
        CancellationToken ct = default)
    {
        var personel = await _db.Personeller.Include(p => p.CalismaDonemleri)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new InvalidOperationException("Personel bulunamadı.");

        if (personel.CalismaDurumu != CalismaDurumu.IstenAyrildi)
            throw new InvalidOperationException("Yalnızca işten ayrılmış personel yeniden işe alınabilir.");
        if (personel.SonCikisTarihi.HasValue && iseGirisTarihi <= personel.SonCikisTarihi.Value)
            throw new InvalidOperationException("Yeni giriş tarihi önceki çıkış tarihinden sonra olmalıdır.");

        // Önceki çalışma dönemleri ve tüm mali geçmiş aynı kartta korunur.
        var donem = PersonelDonemKurallari.YeniDonemAc(personel, iseGirisTarihi, isyeri, gorevi, sirket);
        _db.CalismaDonemleri.Add(donem);
        if (isyeri is not null) personel.Isyeri = isyeri;
        if (gorevi is not null) personel.Gorevi = gorevi;
        if (sirket is not null) personel.Sirket = sirket;
        if (maas.HasValue) personel.GuncelMaas = maas;

        await IseGirisHareketiAsync(personel, donem, kullaniciAdi, ct);
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personel.Id,
            VarlikTuru = nameof(CalismaDonemi),
            VarlikId = donem.Id,
            Islem = "YenidenIseAlim",
            YeniDeger = iseGirisTarihi.ToString("yyyy-MM-dd"),
            KullaniciAdi = kullaniciAdi
        });

        await _db.SaveChangesAsync(ct);
        return (await GetirAsync(id, ct))!;
    }

    private async Task IseGirisHareketiAsync(Personel personel, CalismaDonemi donem, string kullanici, CancellationToken ct) =>
        await _defter.DeftereEkleAsync(new MaliHareket
        {
            IslemKimligi = MaliHareketKurallari.IslemKimligi("IseGiris", personel.Id, donem.Id.ToString("N")),
            PersonelId = personel.Id,
            CalismaDonemiId = donem.Id,
            IslemTarihi = donem.IseGirisTarihi,
            IslemTuru = MaliIslemTuru.IseGiris,
            BakiyeTuru = BakiyeTuru.Bilgi,
            Tutar = personel.GuncelMaas ?? 0m,
            Aciklama = $"İşe giriş — {donem.Sirket ?? personel.Sirket} / {donem.Isyeri ?? personel.Isyeri}" +
                       (personel.GuncelMaas.HasValue ? $", başlangıç maaşı {personel.GuncelMaas:N2}" : ""),
            KaynakTuru = nameof(CalismaDonemi),
            KaynakReferans = donem.Id.ToString(),
            KullaniciAdi = kullanici
        }, false, ct);

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
        Cinsiyet = p.Cinsiyet,
        DogumTarihi = p.DogumTarihi,
        MedeniHal = p.MedeniHal,
        Telefon = p.Telefon,
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
