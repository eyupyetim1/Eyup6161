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
    DbSet<MaliHareket> MaliHareketler { get; }
    DbSet<AvansKaydi> Avanslar { get; }
    DbSet<AvansMahsupKaydi> AvansMahsuplari { get; }
    DbSet<MaasDonemKapanisi> MaasDonemKapanislari { get; }
    DbSet<DonemKontrolMaddesi> DonemKontrolMaddeleri { get; }
    DbSet<DonemDuzeltmeKaydi> DonemDuzeltmeler { get; }
    DbSet<OdemeKontrolOturumu> OdemeKontrolOturumlari { get; }
    DbSet<OdemeKontrolSatiri> OdemeKontrolSatirlari { get; }
    DbSet<OnayliOdemeListesi> OnayliOdemeListeleri { get; }
    DbSet<OnayliOdemeListesiSatiri> OnayliOdemeListesiSatirlari { get; }
    DbSet<OdemeTalimatSatiri> OdemeTalimatSatirlari { get; }
    DbSet<MutabakatFarki> MutabakatFarklari { get; }
    DbSet<DonemKapanisAnlikGoruntusu> DonemKapanisAnlikGoruntuleri { get; }
    DbSet<DonemKapanisOzeti> DonemKapanisOzetleri { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
