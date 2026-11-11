using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain;
using PayControl.Domain.Enums;

namespace PayControl.Application.Services;

public sealed class YetkiServisi : IYetkiServisi
{
    private readonly IPayControlDbContext _db;

    public YetkiServisi(IPayControlDbContext db) => _db = db;

    public async Task DogrulaAsync(string kullaniciAdi, KullaniciRolu enAzRol, string islem, CancellationToken ct = default)
    {
        var kullanici = await _db.Kullanicilar.AsNoTracking()
            .FirstOrDefaultAsync(k => k.KullaniciAdi == kullaniciAdi && k.Aktif, ct);

        if (kullanici is null)
            throw new YetkiHatasiException($"'{islem}' işlemi için tanımlı ve aktif kullanıcı gerekir ({kullaniciAdi}).");
        if (kullanici.Rol < enAzRol)
            throw new YetkiHatasiException(
                $"'{islem}' işlemi için en az {enAzRol} yetkisi gerekir. Kullanıcı rolü: {kullanici.Rol}.");
    }
}
