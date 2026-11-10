using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class BankaOdemeServisi : IBankaOdemeServisi
{
    private readonly IPayControlDbContext _db;
    private readonly IDonemKapanisServisi _donem;

    public BankaOdemeServisi(IPayControlDbContext db, IDonemKapanisServisi donem)
    {
        _db = db;
        _donem = donem;
    }

    public async Task<BankaOdemeKaydi> KaydetAsync(
        Guid personelId,
        DateOnly odemeTarihi,
        decimal tutar,
        int hakEdisYil,
        int hakEdisAy,
        string? iban,
        string? referansNo,
        string kullanici,
        CancellationToken ct = default)
    {
        await _donem.EnsureDonemAcikAsync(hakEdisYil, hakEdisAy, ct);

        if (!string.IsNullOrWhiteSpace(referansNo))
        {
            var mukerrer = await _db.BankaOdemeleri.AnyAsync(o => o.ReferansNo == referansNo, ct);
            if (mukerrer)
                throw new InvalidOperationException(
                    $"Aynı banka hareketi (referans: {referansNo}) daha önce içe aktarılmış. Mükerrer kayıt engellendi.");
        }

        var kayit = new BankaOdemeKaydi
        {
            PersonelId = personelId,
            OdemeTarihi = odemeTarihi,
            Tutar = tutar,
            HakEdisDonemYil = hakEdisYil,
            HakEdisDonemAy = hakEdisAy,
            Iban = iban,
            ReferansNo = referansNo,
            Siniflandirma = OdemeSiniflandirma.NormalHakEdisOdemesi
        };
        _db.BankaOdemeleri.Add(kayit);

        var kimlik = MaliHareketKurallari.IslemKimligi(
            "BankaOdeme", personelId,
            string.IsNullOrWhiteSpace(referansNo) ? kayit.Id.ToString("N") : referansNo);
        if (await _db.MaliHareketler.AnyAsync(h => h.IslemKimligi == kimlik, ct))
            throw new InvalidOperationException("Aynı banka ödemesi mali harekette zaten var.");

        _db.MaliHareketler.Add(MaliHareketKurallari.Olustur(
            personelId, kimlik, odemeTarihi, hakEdisYil, hakEdisAy,
            MaliIslemTuru.BankaMaasOdemesi, BakiyeTuru.MaasAlacagi, tutar,
            $"Banka ödemesi (hak ediş {hakEdisYil}-{hakEdisAy:D2})",
            nameof(BankaOdemeKaydi), kayit.Id.ToString(), referansNo, kullanici));

        await _db.SaveChangesAsync(ct);
        return kayit;
    }
}
