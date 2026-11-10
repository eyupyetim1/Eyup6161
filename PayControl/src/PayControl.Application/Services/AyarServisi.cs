using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;

namespace PayControl.Application.Services;

public sealed class AyarServisi : IAyarServisi
{
    private readonly IPayControlDbContext _db;

    public AyarServisi(IPayControlDbContext db) => _db = db;

    public async Task<string?> GetirAsync(string anahtar, CancellationToken ct = default) =>
        (await _db.UygulamaAyarlari.AsNoTracking().FirstOrDefaultAsync(a => a.Anahtar == anahtar, ct))?.Deger;

    public async Task AyarlaAsync(string anahtar, string? deger, string kullanici, CancellationToken ct = default)
    {
        var a = await _db.UygulamaAyarlari.FirstOrDefaultAsync(x => x.Anahtar == anahtar, ct);
        if (a is null)
        {
            a = new UygulamaAyari { Anahtar = anahtar };
            _db.UygulamaAyarlari.Add(a);
        }
        var gizli = anahtar.EndsWith("Sifreli", StringComparison.Ordinal);
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(UygulamaAyari),
            Islem = "AyarDegisti",
            AlanAdi = anahtar,
            EskiDeger = gizli ? "***" : a.Deger,
            YeniDeger = gizli ? "***" : deger,
            KullaniciAdi = kullanici
        });
        a.Deger = deger;
        a.GuncellemeZamani = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}
