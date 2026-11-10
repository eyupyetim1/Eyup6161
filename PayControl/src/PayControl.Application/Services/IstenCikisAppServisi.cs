using Microsoft.EntityFrameworkCore;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class IstenCikisAppServisi : IIstenCikisServisi
{
    private readonly IPayControlDbContext _db;
    private readonly ICikisDenetimServisi _cikisDenetim;
    private readonly IHakEdisHesaplamaServisi _hakEdis;
    private readonly IMaliHareketServisi _defter;

    public IstenCikisAppServisi(
        IPayControlDbContext db,
        ICikisDenetimServisi cikisDenetim,
        IHakEdisHesaplamaServisi hakEdis,
        IMaliHareketServisi defter)
    {
        _db = db;
        _cikisDenetim = cikisDenetim;
        _hakEdis = hakEdis;
        _defter = defter;
    }

    public async Task<IstenCikisDto> OlusturAsync(IstenCikisOlusturDto dto, string kullaniciAdi, CancellationToken ct = default)
    {
        var personel = await _db.Personeller
            .Include(p => p.CalismaDonemleri)
            .FirstOrDefaultAsync(p => p.Id == dto.PersonelId, ct)
            ?? throw new InvalidOperationException("Personel bulunamadı.");

        var aktifDonem = personel.CalismaDonemleri
            .FirstOrDefault(d => d.Durum == CalismaDurumu.Aktif);

        if (dto.IstenCikisTarihi < dto.FiiliSonCalismaTarihi)
            throw new InvalidOperationException("İşten çıkış tarihi fiilî son çalışma tarihinden önce olamaz.");
        if (aktifDonem is not null && dto.IstenCikisTarihi < aktifDonem.IseGirisTarihi)
            throw new InvalidOperationException("Çıkış tarihi aktif çalışma döneminin giriş tarihinden önce olamaz.");
        if (dto.SonBordroDonemYil.HasValue != dto.SonBordroDonemAy.HasValue)
            throw new InvalidOperationException("Son bordro dönemi yıl ve ay birlikte girilmelidir.");
        if (dto.SonBordroDonemYil.HasValue &&
            new DateOnly(dto.SonBordroDonemYil.Value, dto.SonBordroDonemAy!.Value, 1) >
            new DateOnly(dto.IstenCikisTarihi.Year, dto.IstenCikisTarihi.Month, 1))
            throw new InvalidOperationException("Son ücretin bağlandığı bordro dönemi çıkış ayından sonra olamaz.");

        // SGK: kullanıcı açıkça onaylamadıysa Belirsiz kalır; otomatik Verildi varsayılmaz.
        var sgkDurum = SgkCikisDurumu.Belirsiz;
        if (dto.SgkKullaniciOnayi && dto.SgkCikisDurumu == SgkCikisDurumu.Verildi)
            sgkDurum = SgkCikisDurumu.Verildi;
        else if (dto.SgkCikisDurumu == SgkCikisDurumu.Verilmedi)
            sgkDurum = SgkCikisDurumu.Verilmedi;

        var kayit = new IstenCikisKaydi
        {
            PersonelId = personel.Id,
            CalismaDonemiId = aktifDonem?.Id,
            FiiliSonCalismaTarihi = dto.FiiliSonCalismaTarihi,
            IstenCikisTarihi = dto.IstenCikisTarihi,
            SgkCikisDurumu = sgkDurum,
            SgkBildirgeTarihi = sgkDurum == SgkCikisDurumu.Verildi ? dto.SgkBildirgeTarihi : null,
            SgkOnaylayanKullanici = sgkDurum == SgkCikisDurumu.Verildi ? kullaniciAdi : null,
            SgkBelgeYolu = sgkDurum == SgkCikisDurumu.Verildi ? dto.SgkBelgeYolu : null,
            CikisNedeni = dto.CikisNedeni,
            SonBordroDonemYil = dto.SonBordroDonemYil,
            SonBordroDonemAy = dto.SonBordroDonemAy,
            SonMaasOdemeDurumu = dto.SonMaasOdemeDurumu,
            KalanAvans = dto.KalanAvans,
            IzinUcreti = dto.IzinUcreti,
            KidemTazminati = dto.KidemTazminati,
            IhbarTazminati = dto.IhbarTazminati,
            Aciklama = dto.Aciklama,
            BelgeYolu = dto.BelgeYolu,
            OlusturanKullanici = kullaniciAdi
        };

        if (aktifDonem is not null)
            PersonelDonemKurallari.DonemiKapat(personel, aktifDonem, dto.IstenCikisTarihi);
        else
        {
            personel.CalismaDurumu = CalismaDurumu.IstenAyrildi;
            personel.SonCikisTarihi = dto.IstenCikisTarihi;
        }

        _db.IstenCikisKayitlari.Add(kayit);

        // Ekstre: çıkış olayı ve beyan edilen tazminat/izin tutarları (bordroya dahil olacakları için bilgi bakiyesinde).
        var bilgi = new List<(MaliIslemTuru Tur, decimal? Tutar, string Ad)>
        {
            (MaliIslemTuru.IstenCikis, 0m, "İşten çıkış"),
            (MaliIslemTuru.KidemTazminati, dto.KidemTazminati, "Kıdem tazminatı (çıkış kaydında beyan)"),
            (MaliIslemTuru.IhbarTazminati, dto.IhbarTazminati, "İhbar tazminatı (çıkış kaydında beyan)"),
            (MaliIslemTuru.IzinUcreti, dto.IzinUcreti, "İzin ücreti (çıkış kaydında beyan)")
        };
        foreach (var (tur, tutar, ad) in bilgi.Where(b => b.Tur == MaliIslemTuru.IstenCikis || b.Tutar is > 0))
        {
            await _defter.DeftereEkleAsync(new MaliHareket
            {
                IslemKimligi = MaliHareketKurallari.IslemKimligi($"Cikis:{tur}", personel.Id, kayit.Id.ToString("N")),
                PersonelId = personel.Id,
                CalismaDonemiId = aktifDonem?.Id,
                IslemTarihi = dto.IstenCikisTarihi,
                IslemTuru = tur,
                BakiyeTuru = BakiyeTuru.Bilgi,
                Tutar = tutar ?? 0m,
                Aciklama = tur == MaliIslemTuru.IstenCikis
                    ? $"İşten çıkış. Fiilî son gün {dto.FiiliSonCalismaTarihi:dd.MM.yyyy}. Son ücret dönemi: " +
                      (dto.SonBordroDonemYil.HasValue ? $"{dto.SonBordroDonemYil}-{dto.SonBordroDonemAy:D2}" : "belirtilmedi") +
                      (string.IsNullOrWhiteSpace(dto.CikisNedeni) ? "" : $". Neden: {dto.CikisNedeni}")
                    : $"{ad}. Ödemesi bordro/mutabakat ile izlenir.",
                KaynakTuru = nameof(IstenCikisKaydi),
                KaynakReferans = kayit.Id.ToString(),
                KaynakBelge = dto.BelgeYolu,
                KullaniciAdi = kullaniciAdi
            }, false, ct);
        }
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personel.Id,
            VarlikTuru = nameof(IstenCikisKaydi),
            VarlikId = kayit.Id,
            Islem = "Olusturma",
            KullaniciAdi = kullaniciAdi,
            YeniDeger = dto.IstenCikisTarihi.ToString("yyyy-MM-dd"),
            DegisiklikNedeni = dto.CikisNedeni
        });

        if (dto.KalanAvans is > 0)
        {
            _db.DenetimKayitlari.Add(new DenetimKaydi
            {
                PersonelId = personel.Id,
                VarlikTuru = nameof(IstenCikisKaydi),
                VarlikId = kayit.Id,
                Islem = "Uyari",
                AlanAdi = "KalanAvans",
                YeniDeger = dto.KalanAvans.Value.ToString("N2"),
                DegisiklikNedeni = "İşten ayrılan personelin kapanmamış avans borcu var.",
                KullaniciAdi = kullaniciAdi
            });
        }

        await _db.SaveChangesAsync(ct);

        return new IstenCikisDto
        {
            Id = kayit.Id,
            FiiliSonCalismaTarihi = kayit.FiiliSonCalismaTarihi,
            IstenCikisTarihi = kayit.IstenCikisTarihi,
            SgkCikisDurumu = kayit.SgkCikisDurumu,
            SgkBildirgeTarihi = kayit.SgkBildirgeTarihi,
            CikisNedeni = kayit.CikisNedeni,
            SonBordroDonemYil = kayit.SonBordroDonemYil,
            SonBordroDonemAy = kayit.SonBordroDonemAy,
            SonMaasOdemeDurumu = kayit.SonMaasOdemeDurumu,
            KalanAvans = kayit.KalanAvans,
            IzinUcreti = kayit.IzinUcreti,
            KidemTazminati = kayit.KidemTazminati,
            IhbarTazminati = kayit.IhbarTazminati,
            Aciklama = kayit.Aciklama
        };
    }

    public async Task SgkOnaylaAsync(
        Guid cikisId,
        DateOnly bildirgeTarihi,
        string onaylayanKullanici,
        string? belgeYolu,
        CancellationToken ct = default)
    {
        var cikis = await _db.IstenCikisKayitlari.FirstOrDefaultAsync(c => c.Id == cikisId, ct)
            ?? throw new InvalidOperationException("İşten çıkış kaydı bulunamadı.");

        var eski = cikis.SgkCikisDurumu.ToString();
        _cikisDenetim.SgkCikisOnayla(cikis, bildirgeTarihi, onaylayanKullanici, belgeYolu);

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = cikis.PersonelId,
            VarlikTuru = nameof(IstenCikisKaydi),
            VarlikId = cikis.Id,
            Islem = "SgkOnay",
            AlanAdi = "SgkCikisDurumu",
            EskiDeger = eski,
            YeniDeger = cikis.SgkCikisDurumu.ToString(),
            KullaniciAdi = onaylayanKullanici,
            DegisiklikNedeni = "Kullanıcı onayı / belge"
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<CikisOdemeIncelemeSonucu> OdemeleriInceleAsync(Guid cikisId, CancellationToken ct = default)
    {
        var cikis = await _db.IstenCikisKayitlari.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cikisId, ct)
            ?? throw new InvalidOperationException("İşten çıkış kaydı bulunamadı.");

        MaasDonemKaydi? hakEdis = null;
        if (cikis.SonBordroDonemYil.HasValue && cikis.SonBordroDonemAy.HasValue)
        {
            hakEdis = await _db.MaasDonemKayitlari.AsNoTracking()
                .FirstOrDefaultAsync(m =>
                    m.PersonelId == cikis.PersonelId &&
                    m.DonemYil == cikis.SonBordroDonemYil &&
                    m.DonemAy == cikis.SonBordroDonemAy, ct);
        }

        // Ödemeleri hak ediş dönemine göre getir — ödeme tarihine göre bordro ayına bağlamayız.
        var odemeler = await _db.BankaOdemeleri.AsNoTracking()
            .Where(o => o.PersonelId == cikis.PersonelId &&
                        o.HakEdisDonemYil == cikis.SonBordroDonemYil &&
                        o.HakEdisDonemAy == cikis.SonBordroDonemAy)
            .ToListAsync(ct);

        return _cikisDenetim.OdemeleriIncele(cikis, hakEdis, odemeler);
    }

    public async Task<HakEdisHesaplamaSonucu> CikisAyiHakEdisHesaplaAsync(
        Guid personelId,
        DateOnly cikisTarihi,
        bool bordroOnayli,
        CancellationToken ct = default)
    {
        var personel = await _db.Personeller.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == personelId, ct)
            ?? throw new InvalidOperationException("Personel bulunamadı.");

        var donemKaydi = await _db.MaasDonemKayitlari.AsNoTracking()
            .FirstOrDefaultAsync(m =>
                m.PersonelId == personelId &&
                m.DonemYil == cikisTarihi.Year &&
                m.DonemAy == cikisTarihi.Month, ct);

        return _hakEdis.CikisAyiHakEdisHesapla(
            donemKaydi?.MaasTutar ?? personel.GuncelMaas,
            donemKaydi?.MaasTuru ?? personel.GuncelMaasTuru,
            cikisTarihi,
            donemKaydi?.Gun,
            donemKaydi?.NetIstihkak,
            bordroOnayli);
    }
}
