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
    DbSet<Tesis> Tesisler { get; }
    DbSet<DonemTesisOnayi> DonemTesisOnaylari { get; }
    DbSet<IcraDosyasi> IcraDosyalari { get; }
    DbSet<IcraKesintiKaydi> IcraKesintileri { get; }
    DbSet<BesTanimi> BesTanimlari { get; }
    DbSet<Belge> Belgeler { get; }
    DbSet<TahsilatPlani> TahsilatPlanlari { get; }
    DbSet<CikisTalimati> CikisTalimatlari { get; }
    DbSet<PuantajGunu> PuantajGunleri { get; }
    DbSet<PuantajNotu> PuantajNotlari { get; }
    DbSet<ResmiTatil> ResmiTatiller { get; }
    DbSet<PersonelNakil> PersonelNakilleri { get; }
    DbSet<PersonelIzinAyari> PersonelIzinAyarlari { get; }
    DbSet<AylikOzetKaydi> AylikOzetKayitlari { get; }
    DbSet<TahsilatTaksiti> TahsilatTaksitleri { get; }
    DbSet<BildirimAyari> BildirimAyarlari { get; }
    DbSet<BildirimAlicisi> BildirimAlicilari { get; }
    DbSet<BildirimKurali> BildirimKurallari { get; }
    DbSet<BildirimKaydi> BildirimKayitlari { get; }
    DbSet<YedekKaydi> YedekKayitlari { get; }
    DbSet<UygulamaAyari> UygulamaAyarlari { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
