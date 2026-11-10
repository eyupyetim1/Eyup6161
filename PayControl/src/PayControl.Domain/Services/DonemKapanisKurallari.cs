using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Domain.Services;

public static class DonemKapanisKurallari
{
    public static readonly (string Kod, string Aciklama)[] VarsayilanKontrolListesi =
    [
        ("PER_LISTE", "Personel listesi kontrol edildi."),
        ("YENI_GIRIS", "Yeni işe girişler işlendi."),
        ("CIKISLAR", "İşten çıkışlar işlendi."),
        ("SGK", "SGK çıkış bildirimi durumları kontrol edildi."),
        ("BORDRO", "Bordro içe aktarıldı."),
        ("AVANS", "Avans mahsupları kontrol edildi."),
        ("FARKLAR", "Eksik ve fazla ödeme farkları incelendi."),
        ("ODEME_ONAY", "Banka ödeme listesi onaylandı."),
        ("BANKA_ICE", "Gerçekleşen banka ödemeleri içe aktarıldı."),
        ("MUTABAKAT", "Bordro ve banka mutabakatı tamamlandı."),
        ("ACIK_FARK", "Açık ödeme farkları çözüldü veya gerekçeli istisna olarak kayıt altına alındı."),
        ("TESIS", "Tesis onayları tamamlandı."),
        ("YONETICI", "Yönetici kapanış onayı verildi.")
    ];

    public static bool DonemKilitliMi(MaasDonemKapanisi? donem) =>
        donem?.Durum == MaasDonemDurumu.Kapatildi;

    public static void DogrudanDegisikligiEngelle(MaasDonemKapanisi? donem)
    {
        if (DonemKilitliMi(donem))
            throw new InvalidOperationException(
                $" {donem!.DonemYil}-{donem.DonemAy:D2} dönemi kapatılmıştır. Doğrudan değişiklik yapılamaz; düzeltme kaydı oluşturun.");
    }

    public static void Kapat(
        MaasDonemKapanisi donem,
        string kullanici,
        string? not,
        bool tumMaddelerTamam,
        int cozulmemisUyari)
    {
        if (donem.Durum == MaasDonemDurumu.Kapatildi)
            throw new InvalidOperationException("Dönem zaten kapatılmış.");

        if (!tumMaddelerTamam)
            throw new InvalidOperationException("Kontrol listesi tamamlanmadan dönem kapatılamaz.");

        // Açık fark ve kritik uyarılar not ile geçiştirilemez: her biri çözülmeli veya tek tek gerekçeli istisna olmalı.
        if (cozulmemisUyari > 0)
            throw new InvalidOperationException(
                $"{cozulmemisUyari} çözülmemiş uyarı/fark var. Her biri çözülmeden veya gerekçeli istisna olarak kaydedilmeden dönem kapatılamaz.");

        donem.Durum = MaasDonemDurumu.Kapatildi;
        donem.KapanisZamani = DateTime.UtcNow;
        donem.KapatanKullanici = kullanici;
        donem.KapanisNotu = not;
        donem.CozulmemisUyariSayisi = cozulmemisUyari;
        donem.KapanisSayisi++;
    }

    /// <summary>
    /// Durum makinesi: ileri yönde geçişe izin verilir; "İnceleme Bekliyor" ödeme kontrolünden sonraki
    /// her aşamadan açılabilir. "Kapatıldı" yalnızca kapanış işlemiyle, geri dönüş yalnızca yeniden açma ile olur.
    /// </summary>
    public static bool DurumGecisiGecerliMi(MaasDonemDurumu mevcut, MaasDonemDurumu yeni)
    {
        if (mevcut == yeni) return true;
        if (mevcut == MaasDonemDurumu.Kapatildi || yeni == MaasDonemDurumu.Kapatildi) return false;
        if (yeni == MaasDonemDurumu.IncelemeBekliyor) return mevcut >= MaasDonemDurumu.OdemeOncesiKontrolde;
        if (mevcut == MaasDonemDurumu.IncelemeBekliyor)
            return yeni is MaasDonemDurumu.KapanisaHazir or MaasDonemDurumu.BankaMutabakatiYapiliyor;
        if (mevcut == MaasDonemDurumu.KapanisaHazir)
            return yeni is MaasDonemDurumu.BankaMutabakatiYapiliyor;
        return yeni > mevcut;
    }

    public static void YenidenAc(MaasDonemKapanisi donem, string kullanici, string neden)
    {
        if (donem.Durum != MaasDonemDurumu.Kapatildi)
            throw new InvalidOperationException("Yalnızca kapatılmış dönem yeniden açılabilir.");
        if (string.IsNullOrWhiteSpace(neden))
            throw new InvalidOperationException("Yeniden açma gerekçesi zorunludur.");

        donem.Durum = MaasDonemDurumu.IncelemeBekliyor;
        donem.YenidenAcmaZamani = DateTime.UtcNow;
        donem.YenidenAcanKullanici = kullanici;
        donem.YenidenAcmaNedeni = neden;
        donem.KapanisZamani = null;
        donem.KapatanKullanici = null;
    }

    public static MaasDonemKapanisi YeniDonem(int yil, int ay, string? sirket = null)
    {
        var donem = new MaasDonemKapanisi
        {
            DonemYil = yil,
            DonemAy = ay,
            Sirket = sirket,
            Durum = MaasDonemDurumu.Hazirlaniyor
        };

        var sira = 1;
        foreach (var (kod, aciklama) in VarsayilanKontrolListesi)
        {
            donem.KontrolMaddeleri.Add(new DonemKontrolMaddesi
            {
                Sira = sira++,
                MaddeKodu = kod,
                Aciklama = aciklama
            });
        }

        return donem;
    }
}
