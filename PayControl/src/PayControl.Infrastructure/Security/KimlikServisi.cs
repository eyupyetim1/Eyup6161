using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Infrastructure.Data;

namespace PayControl.Infrastructure.Security;

public sealed class KimlikServisi : IKimlikServisi
{
    private readonly PayControlDbContext _db;

    public KimlikServisi(PayControlDbContext db) => _db = db;

    public async Task EnsureAdminAsync(CancellationToken ct = default)
    {
        if (await _db.Kullanicilar.AnyAsync(ct))
            return;

        var (hash, salt) = SifreHasher.Hashle("Admin123!");
        _db.Kullanicilar.Add(new Kullanici
        {
            KullaniciAdi = "admin",
            SifreHash = hash,
            SifreSalt = salt,
            Rol = KullaniciRolu.Yonetici,
            Aktif = true
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<GirisSonucu> GirisYapAsync(string kullaniciAdi, string sifre, CancellationToken ct = default)
    {
        var user = await _db.Kullanicilar
            .FirstOrDefaultAsync(u => u.KullaniciAdi == kullaniciAdi && u.Aktif, ct);

        if (user is null || !SifreHasher.Dogrula(sifre, user.SifreHash, user.SifreSalt))
        {
            return new GirisSonucu { Basarili = false, Hata = "Kullanıcı adı veya şifre hatalı." };
        }

        return new GirisSonucu
        {
            Basarili = true,
            KullaniciId = user.Id,
            KullaniciAdi = user.KullaniciAdi,
            Rol = user.Rol
        };
    }
}
