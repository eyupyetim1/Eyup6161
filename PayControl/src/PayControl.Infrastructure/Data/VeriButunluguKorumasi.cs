using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PayControl.Domain;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Infrastructure.Data;

/// <summary>
/// Veritabanı katmanında son savunma hattı. Servislerdeki kontroller atlansa bile:
/// <list type="bullet">
/// <item>Mali hareket defteri ve denetim kayıtları değiştirilemez / silinemez (düzeltme = ters kayıt).</item>
/// <item>Kapatılmış maaş dönemine ait bordro, ödeme, kontrol, mutabakat ve avans mahsup kayıtları
/// eklenemez, değiştirilemez, silinemez. Düzeltme yalnızca <see cref="MaliIslemTuru.OncekiDonemDuzeltme"/>
/// hareketi ve <see cref="DonemDuzeltmeKaydi"/> ile yapılır.</item>
/// </list>
/// </summary>
internal static class VeriButunluguKorumasi
{
    public static void Denetle(PayControlDbContext db)
    {
        var degisenler = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();
        if (degisenler.Count == 0)
            return;

        foreach (var e in degisenler)
        {
            if (e.Entity is MaliHareket && e.State != EntityState.Added)
                throw new KapaliDonemIhlaliException(
                    "Mali hareket kayıtları değiştirilemez veya silinemez. Hatalı kayıt için ters kayıt oluşturun.");
            if (e.Entity is DenetimKaydi && e.State != EntityState.Added)
                throw new KapaliDonemIhlaliException("Denetim geçmişi değiştirilemez veya silinemez.");
        }

        var kapaliDonemler = db.MaasDonemKapanislari.AsNoTracking()
            .Where(d => d.Durum == MaasDonemDurumu.Kapatildi)
            .Select(d => new { d.Id, d.DonemYil, d.DonemAy })
            .ToList();
        if (kapaliDonemler.Count == 0)
            return;

        var kapaliSet = kapaliDonemler.Select(d => (d.DonemYil, d.DonemAy)).ToHashSet();
        var kapaliIdler = kapaliDonemler.Select(d => d.Id).ToHashSet();

        bool Kapali(int? yil, int? ay) => yil.HasValue && ay.HasValue && kapaliSet.Contains((yil.Value, ay.Value));

        foreach (var e in degisenler)
        {
            (int? Yil, int? Ay)? donem = e.Entity switch
            {
                MaasDonemKaydi m => (Deger(e, nameof(MaasDonemKaydi.DonemYil), m.DonemYil), Deger(e, nameof(MaasDonemKaydi.DonemAy), m.DonemAy)),
                MaasKalemi k => UstBordroDonemi(db, k.MaasDonemKaydiId),
                BankaOdemeKaydi b => (Deger(e, nameof(BankaOdemeKaydi.HakEdisDonemYil), b.HakEdisDonemYil), Deger(e, nameof(BankaOdemeKaydi.HakEdisDonemAy), b.HakEdisDonemAy)),
                AvansMahsupKaydi a when e.State != EntityState.Added || a.GerceklesenTutar.HasValue => (a.DonemYil, a.DonemAy),
                OdemeKontrolOturumu o => (o.DonemYil, o.DonemAy),
                OdemeKontrolSatiri s => OturumDonemi(db, s.OdemeKontrolOturumuId),
                OdemeTalimatSatiri t => OturumDonemi(db, t.OdemeKontrolOturumuId),
                OnayliOdemeListesi l => (l.DonemYil, l.DonemAy),
                MutabakatFarki f => (f.DonemYil, f.DonemAy),
                MaliHareket h when h.IslemTuru != MaliIslemTuru.OncekiDonemDuzeltme => (h.DonemYil, h.DonemAy),
                _ => null
            };

            if (donem is { } d && Kapali(d.Yil, d.Ay))
                throw new KapaliDonemIhlaliException(
                    $"{d.Yil}-{d.Ay:D2} dönemi kapatılmıştır. {e.Entity.GetType().Name} kaydına doğrudan müdahale edilemez; düzeltme kaydı oluşturun veya dönemi gerekçeyle yeniden açın.");

            if (e.Entity is DonemKontrolMaddesi madde && kapaliIdler.Contains(madde.MaasDonemKapanisiId))
                throw new KapaliDonemIhlaliException("Kapatılmış dönemin kontrol listesi değiştirilemez.");

            if (e.Entity is MaasDonemKapanisi kapanis && e.State == EntityState.Modified && kapaliIdler.Contains(kapanis.Id))
            {
                // Tek izinli geçiş: gerekçeli yeniden açma (Durum Kapatildi → başka durum).
                if (kapanis.Durum == MaasDonemDurumu.Kapatildi || string.IsNullOrWhiteSpace(kapanis.YenidenAcmaNedeni))
                    throw new KapaliDonemIhlaliException(
                        "Kapatılmış dönemin toplamları değiştirilemez. Yalnızca yetkili yönetici gerekçeli yeniden açma yapabilir.");
            }

            if (e.Entity is MaasDonemKapanisi silinen && e.State == EntityState.Deleted)
                throw new KapaliDonemIhlaliException("Maaş dönemi kayıtları silinemez.");
        }
    }

    private static T Deger<T>(EntityEntry e, string prop, T current)
    {
        // Değiştirilen kayıtta orijinal dönem esas alınır (dönemi değiştirerek kilitten kaçılamaz).
        if (e.State == EntityState.Modified || e.State == EntityState.Deleted)
            return (T)e.Property(prop).OriginalValue!;
        return current;
    }

    private static (int?, int?) UstBordroDonemi(PayControlDbContext db, Guid bordroId)
    {
        var yerel = db.MaasDonemKayitlari.Local.FirstOrDefault(m => m.Id == bordroId);
        if (yerel is not null) return (yerel.DonemYil, yerel.DonemAy);
        var d = db.MaasDonemKayitlari.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.Id == bordroId).Select(m => new { m.DonemYil, m.DonemAy }).FirstOrDefault();
        return d is null ? (null, null) : (d.DonemYil, d.DonemAy);
    }

    private static (int?, int?) OturumDonemi(PayControlDbContext db, Guid oturumId)
    {
        var yerel = db.OdemeKontrolOturumlari.Local.FirstOrDefault(o => o.Id == oturumId);
        if (yerel is not null) return (yerel.DonemYil, yerel.DonemAy);
        var d = db.OdemeKontrolOturumlari.AsNoTracking()
            .Where(o => o.Id == oturumId).Select(o => new { o.DonemYil, o.DonemAy }).FirstOrDefault();
        return d is null ? (null, null) : (d.DonemYil, d.DonemAy);
    }
}
