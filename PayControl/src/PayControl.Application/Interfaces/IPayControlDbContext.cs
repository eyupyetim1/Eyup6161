using Microsoft.EntityFrameworkCore;
using PayControl.Domain.Entities;

namespace PayControl.Application.Interfaces;

/// <summary>
/// Uygulama katmanının ihtiyaç duyduğu DbContext sözleşmesi.
/// </summary>
public interface IPayControlDbContext
{
    DbSet<Personel> Personeller { get; }
    DbSet<CalismaDonemi> CalismaDonemleri { get; }
    DbSet<MaasDonemKaydi> MaasDonemKayitlari { get; }
    DbSet<MaasKalemi> MaasKalemleri { get; }
    DbSet<IstenCikisKaydi> IstenCikisKayitlari { get; }
    DbSet<DenetimKaydi> DenetimKayitlari { get; }
    DbSet<IceAktarmaKaydi> IceAktarmalar { get; }
    DbSet<PersonelBelge> PersonelBelgeler { get; }
    DbSet<Kullanici> Kullanicilar { get; }
    DbSet<BankaOdemeKaydi> BankaOdemeleri { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
