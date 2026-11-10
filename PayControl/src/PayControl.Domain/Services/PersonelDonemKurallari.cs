using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Domain.Services;

/// <summary>
/// Personel çalışma dönemi ve maaş geçmişi domain kuralları.
/// </summary>
public static class PersonelDonemKurallari
{
    public static CalismaDonemi YeniDonemAc(
        Personel personel,
        DateOnly iseGirisTarihi,
        string? isyeri = null,
        string? gorevi = null,
        string? sirket = null)
    {
        var aktif = personel.CalismaDonemleri.FirstOrDefault(d => d.Durum == CalismaDurumu.Aktif);
        if (aktif is not null && aktif.IstenCikisTarihi is null)
            throw new InvalidOperationException("Aktif çalışma dönemi kapatılmadan yeni dönem açılamaz.");

        var donem = new CalismaDonemi
        {
            PersonelId = personel.Id,
            IseGirisTarihi = iseGirisTarihi,
            Isyeri = isyeri ?? personel.Isyeri,
            Gorevi = gorevi ?? personel.Gorevi,
            Sirket = sirket ?? personel.Sirket,
            Durum = CalismaDurumu.Aktif
        };

        personel.CalismaDonemleri.Add(donem);
        personel.CalismaDurumu = CalismaDurumu.Aktif;
        personel.IlkIseGirisTarihi ??= iseGirisTarihi;
        personel.SonCikisTarihi = null;
        personel.GuncellemeZamani = DateTime.UtcNow;
        return donem;
    }

    public static void DonemiKapat(Personel personel, CalismaDonemi donem, DateOnly cikisTarihi)
    {
        if (donem.PersonelId != personel.Id)
            throw new InvalidOperationException("Çalışma dönemi personel ile eşleşmiyor.");

        donem.IstenCikisTarihi = cikisTarihi;
        donem.Durum = CalismaDurumu.IstenAyrildi;
        personel.CalismaDurumu = CalismaDurumu.IstenAyrildi;
        personel.SonCikisTarihi = cikisTarihi;
        personel.GuncellemeZamani = DateTime.UtcNow;
    }

    /// <summary>
    /// Aynı personel+dönem için mevcut maaş kaydını değiştirmez; yeni dönem ayrı kayıttır.
    /// </summary>
    public static bool OncekiDonemMaasiKorunuyorMu(
        MaasDonemKaydi onceki,
        MaasDonemKaydi sonraki)
    {
        return onceki.Id != sonraki.Id
               && (onceki.DonemYil != sonraki.DonemYil || onceki.DonemAy != sonraki.DonemAy)
               && onceki.MaasTutar == onceki.MaasTutar; // kayıt immutable kimlik ile korunur
    }
}
