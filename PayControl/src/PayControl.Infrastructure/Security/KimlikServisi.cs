using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;
using PayControl.Infrastructure.Data;

namespace PayControl.Infrastructure.Security;

public sealed class KimlikServisi : IKimlikServisi
{
    private readonly PayControlDbContext _db;
    private readonly IYetkiServisi _yetki;

    public KimlikServisi(PayControlDbContext db, IYetkiServisi yetki)
    {
        _db = db;
        _yetki = yetki;
    }

    public async Task EnsureAdminAsync(CancellationToken ct = default)
    {
        if (await _db.Kullanicilar.AnyAsync(ct))
            return;

        var (hash, salt) = SifreHasher.Hashle(SifrePolitikasi.VarsayilanAdminSifresi);
        _db.Kullanicilar.Add(new Kullanici
        {
            KullaniciAdi = "admin",
            AdSoyad = "Sistem Yöneticisi",
            SifreHash = hash,
            SifreSalt = salt,
            Rol = KullaniciRolu.Yonetici,
            Aktif = true,
            SifreDegistirmeGerekli = true
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<GirisSonucu> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken ct = default)
    {
        var user = await _db.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == kullaniciAdi, ct);
        if (user is null || !user.Aktif)
            return new GirisSonucu { Basarili = false, Hata = "Kullanıcı adı veya şifre hatalı." };

        if (user.KilitBitis is { } kilit && kilit > DateTime.UtcNow)
            return new GirisSonucu
            {
                Basarili = false,
                Hata = $"Çok sayıda hatalı deneme nedeniyle hesap {Math.Ceiling((kilit - DateTime.UtcNow).TotalMinutes)} dakika kilitli."
            };

        if (!SifreHasher.Dogrula(sifre, user.SifreHash, user.SifreSalt))
        {
            user.BasarisizGirisSayisi++;
            string hata = "Kullanıcı adı veya şifre hatalı.";
            if (user.BasarisizGirisSayisi >= SifrePolitikasi.AzamiHataliDeneme)
            {
                user.KilitBitis = DateTime.UtcNow.Add(SifrePolitikasi.KilitSuresi);
                user.BasarisizGirisSayisi = 0;
                hata = $"{SifrePolitikasi.AzamiHataliDeneme} hatalı deneme: hesap {SifrePolitikasi.KilitSuresi.TotalMinutes} dakika kilitlendi.";
                Denetim(user, "HesapKilitlendi", null, user.KullaniciAdi);
            }
            await _db.SaveChangesAsync(ct);
            return new GirisSonucu { Basarili = false, Hata = hata };
        }

        user.BasarisizGirisSayisi = 0;
        user.KilitBitis = null;
        user.SonGiris = DateTime.UtcNow;
        // Eski sürümden gelen varsayılan admin şifresi de değiştirme zorunluluğu doğurur.
        if (sifre == SifrePolitikasi.VarsayilanAdminSifresi) user.SifreDegistirmeGerekli = true;
        Denetim(user, "Giris", null, user.KullaniciAdi);
        await _db.SaveChangesAsync(ct);

        return new GirisSonucu
        {
            Basarili = true,
            KullaniciId = user.Id,
            KullaniciAdi = user.KullaniciAdi,
            Rol = user.Rol,
            SifreDegistirmeGerekli = user.SifreDegistirmeGerekli
        };
    }

    public async Task SifreDegistirAsync(string kullaniciAdi, string eskiSifre, string yeniSifre, CancellationToken ct = default)
    {
        var user = await _db.Kullanicilar.FirstOrDefaultAsync(u => u.KullaniciAdi == kullaniciAdi && u.Aktif, ct)
                   ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");
        if (!SifreHasher.Dogrula(eskiSifre, user.SifreHash, user.SifreSalt))
            throw new InvalidOperationException("Mevcut şifre hatalı.");
        if (eskiSifre == yeniSifre)
            throw new InvalidOperationException("Yeni şifre eskisiyle aynı olamaz.");
        var hatalar = SifrePolitikasi.Denetle(yeniSifre, kullaniciAdi);
        if (hatalar.Count > 0) throw new InvalidOperationException(string.Join(" ", hatalar));

        (user.SifreHash, user.SifreSalt) = SifreHasher.Hashle(yeniSifre);
        user.SifreDegistirmeGerekli = false;
        user.SifreDegisimZamani = DateTime.UtcNow;
        Denetim(user, "SifreDegistirildi", null, kullaniciAdi);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<KullaniciDto>> ListeleAsync(CancellationToken ct = default) =>
        (await _db.Kullanicilar.AsNoTracking().OrderBy(u => u.KullaniciAdi).ToListAsync(ct)).Select(Map).ToList();

    public async Task<KullaniciDto> OlusturAsync(KullaniciOlusturDto dto, string yapan, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(yapan, KullaniciRolu.Yonetici, "Kullanıcı oluşturma", ct);
        var ad = (dto.KullaniciAdi ?? "").Trim().ToLowerInvariant();
        if (ad.Length < 3 || !ad.All(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-'))
            throw new InvalidOperationException("Kullanıcı adı en az 3 karakter olmalı; harf, rakam, nokta, alt çizgi içerebilir.");
        if (await _db.Kullanicilar.AnyAsync(u => u.KullaniciAdi == ad, ct))
            throw new InvalidOperationException($"'{ad}' kullanıcı adı zaten kayıtlı.");
        var hatalar = SifrePolitikasi.Denetle(dto.Sifre, ad);
        if (hatalar.Count > 0) throw new InvalidOperationException(string.Join(" ", hatalar));

        var (hash, salt) = SifreHasher.Hashle(dto.Sifre);
        var k = new Kullanici
        {
            KullaniciAdi = ad,
            SifreHash = hash,
            SifreSalt = salt,
            Rol = dto.Rol,
            AdSoyad = dto.AdSoyad,
            Eposta = dto.Eposta,
            Telefon = dto.Telefon,
            Aktif = true,
            SifreDegistirmeGerekli = true
        };
        _db.Kullanicilar.Add(k);
        Denetim(k, "KullaniciOlusturuldu", dto.Rol.ToString(), yapan);
        await _db.SaveChangesAsync(ct);
        return Map(k);
    }

    public async Task GuncelleAsync(Guid id, KullaniciRolu rol, bool aktif, string? adSoyad, string? eposta, string? telefon, string yapan, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(yapan, KullaniciRolu.Yonetici, "Kullanıcı güncelleme", ct);
        var k = await _db.Kullanicilar.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");

        var yoneticiliktenCikiyor = k.Rol == KullaniciRolu.Yonetici && k.Aktif && (rol != KullaniciRolu.Yonetici || !aktif);
        if (yoneticiliktenCikiyor && await _db.Kullanicilar.CountAsync(u => u.Rol == KullaniciRolu.Yonetici && u.Aktif && u.Id != id, ct) == 0)
            throw new InvalidOperationException("Son aktif yöneticinin yetkisi kaldırılamaz veya pasife alınamaz.");

        var eski = $"Rol={k.Rol}, Aktif={k.Aktif}";
        k.Rol = rol;
        k.Aktif = aktif;
        k.AdSoyad = adSoyad;
        k.Eposta = eposta;
        k.Telefon = telefon;
        Denetim(k, "KullaniciGuncellendi", $"Rol={rol}, Aktif={aktif} (önce: {eski})", yapan);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SifreSifirlaAsync(Guid id, string geciciSifre, string yapan, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(yapan, KullaniciRolu.Yonetici, "Şifre sıfırlama", ct);
        var k = await _db.Kullanicilar.FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new InvalidOperationException("Kullanıcı bulunamadı.");
        var hatalar = SifrePolitikasi.Denetle(geciciSifre, k.KullaniciAdi);
        if (hatalar.Count > 0) throw new InvalidOperationException(string.Join(" ", hatalar));
        (k.SifreHash, k.SifreSalt) = SifreHasher.Hashle(geciciSifre);
        k.SifreDegistirmeGerekli = true;
        k.KilitBitis = null;
        k.BasarisizGirisSayisi = 0;
        Denetim(k, "SifreSifirlandi", null, yapan);
        await _db.SaveChangesAsync(ct);
    }

    private void Denetim(Kullanici k, string islem, string? yeni, string kullanici) =>
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(Kullanici),
            VarlikId = k.Id,
            Islem = islem,
            AlanAdi = k.KullaniciAdi,
            YeniDeger = yeni,
            KullaniciAdi = kullanici
        });

    private static KullaniciDto Map(Kullanici k) => new()
    {
        Id = k.Id,
        KullaniciAdi = k.KullaniciAdi,
        AdSoyad = k.AdSoyad,
        Eposta = k.Eposta,
        Telefon = k.Telefon,
        Rol = k.Rol,
        Aktif = k.Aktif,
        Kilitli = k.KilitBitis > DateTime.UtcNow,
        SifreDegistirmeGerekli = k.SifreDegistirmeGerekli,
        SonGiris = k.SonGiris
    };
}
