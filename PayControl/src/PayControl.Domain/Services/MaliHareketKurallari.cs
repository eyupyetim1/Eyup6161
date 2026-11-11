using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Domain.Services;

public static class MaliHareketKurallari
{
    public static readonly MaliIslemTuru[] HakEdisTurleri =
    [
        MaliIslemTuru.MaasHakEdis, MaliIslemTuru.Prim, MaliIslemTuru.Ikramiye, MaliIslemTuru.FazlaMesai,
        MaliIslemTuru.KidemTazminati, MaliIslemTuru.IhbarTazminati, MaliIslemTuru.IzinUcreti,
        MaliIslemTuru.OncekiDonemDuzeltme, MaliIslemTuru.DevredenAlacak
    ];

    public static readonly MaliIslemTuru[] OdemeTurleri =
        [MaliIslemTuru.BankaMaasOdemesi, MaliIslemTuru.KasaOdemesi];

    public static readonly MaliIslemTuru[] KesintiTurleri =
        [MaliIslemTuru.IcraKesinti, MaliIslemTuru.BesKesinti, MaliIslemTuru.DigerKesinti];

    public static string IslemKimligi(string kaynak, Guid personelId, string ayirac) =>
        $"{kaynak}:{personelId:N}:{ayirac}";

    public static MaliHareket Olustur(
        Guid personelId,
        string islemKimligi,
        DateOnly tarih,
        int? donemYil,
        int? donemAy,
        MaliIslemTuru tur,
        BakiyeTuru bakiye,
        decimal tutar,
        string aciklama,
        string? kaynakTuru,
        string? kaynakReferans,
        string? belge,
        string? kullanici,
        Guid? calismaDonemiId = null)
    {
        if (string.IsNullOrWhiteSpace(islemKimligi))
            throw new ArgumentException("İşlem kimliği zorunludur.", nameof(islemKimligi));
        if (donemAy is < 1 or > 12)
            throw new ArgumentException("Dönem ayı 1-12 arasında olmalıdır.", nameof(donemAy));

        return new MaliHareket
        {
            PersonelId = personelId,
            CalismaDonemiId = calismaDonemiId,
            IslemKimligi = islemKimligi,
            IslemTarihi = tarih,
            DonemYil = donemYil,
            DonemAy = donemAy,
            IslemTuru = tur,
            BakiyeTuru = bakiye,
            Tutar = tutar,
            Aciklama = aciklama,
            KaynakTuru = kaynakTuru,
            KaynakReferans = kaynakReferans,
            KaynakBelge = belge,
            KullaniciAdi = kullanici
        };
    }

    /// <summary>Hareketin kendi bakiyesi üzerindeki işaretli etkisi.</summary>
    public static decimal Etki(MaliHareket h) => h.Tutar * Isaret(h.IslemTuru, h.BakiyeTuru);

    private static int Isaret(MaliIslemTuru tur, BakiyeTuru bakiye) => (tur, bakiye) switch
    {
        (MaliIslemTuru.AvansMahsup, BakiyeTuru.AvansBorcu) => -1,
        (MaliIslemTuru.IadeAlinan, _) => -1,
        (MaliIslemTuru.FazlaOdemeKesinlesti, BakiyeTuru.SupheliFazlaOdeme) => -1,
        (MaliIslemTuru.FazlaOdemeAciklandi, BakiyeTuru.SupheliFazlaOdeme) => -1,
        _ => 1
    };

    public static bool HakEdisMi(MaliHareket h) =>
        h.BakiyeTuru == BakiyeTuru.MaasAlacagi && HakEdisTurleri.Contains(h.IslemTuru);

    public static bool OdemeMi(MaliHareket h) =>
        h.BakiyeTuru == BakiyeTuru.MaasAlacagi && OdemeTurleri.Contains(h.IslemTuru);

    public static EkstreOzeti OzetHesapla(IEnumerable<MaliHareket> hareketler)
    {
        var list = hareketler.ToList();
        var donemler = DonemBakiyeleri(list);

        return new EkstreOzeti
        {
            ToplamHakEdis = list.Where(HakEdisMi).Sum(Etki),
            GerceklesenToplamOdeme = list.Where(OdemeMi).Sum(Etki),
            OdenmemisHakEdis = donemler.Where(d => d.Fark < -0.005m).Sum(d => -d.Fark),
            DonemFazlaOdemeFarki = donemler.Where(d => d.Fark > 0.005m).Sum(d => d.Fark),
            AcikAvansBorcu = Math.Max(0, list.Where(h => h.BakiyeTuru == BakiyeTuru.AvansBorcu).Sum(Etki)),
            IncelemeBekleyenFazlaOdeme = Math.Max(0, list.Where(h => h.BakiyeTuru == BakiyeTuru.SupheliFazlaOdeme).Sum(Etki)),
            KesinlesmisGeriOdemeBorcu = Math.Max(0, list.Where(h => h.BakiyeTuru == BakiyeTuru.KesinlesmisGeriOdeme).Sum(Etki)),
            ToplamKesinti = list.Where(h => KesintiTurleri.Contains(h.IslemTuru)).Sum(Etki),
            HareketSayisi = list.Count
        };
    }

    /// <summary>Hak ediş dönemi bazında hak ediş / ödeme karşılaştırması.</summary>
    public static IReadOnlyList<DonemBakiyesi> DonemBakiyeleri(IEnumerable<MaliHareket> hareketler) =>
        hareketler
            .Where(h => HakEdisMi(h) || OdemeMi(h) || h.IslemTuru == MaliIslemTuru.AvansMahsup || KesintiTurleri.Contains(h.IslemTuru))
            .GroupBy(h => (h.DonemYil, h.DonemAy))
            .OrderBy(g => g.Key.DonemYil ?? 0).ThenBy(g => g.Key.DonemAy ?? 0)
            .Select(g => new DonemBakiyesi
            {
                DonemYil = g.Key.DonemYil,
                DonemAy = g.Key.DonemAy,
                HakEdis = g.Where(HakEdisMi).Sum(Etki),
                Odenen = g.Where(OdemeMi).Sum(Etki),
                AvansMahsup = -g.Where(h => h.IslemTuru == MaliIslemTuru.AvansMahsup && h.BakiyeTuru == BakiyeTuru.AvansBorcu).Sum(Etki),
                Kesintiler = g.Where(h => KesintiTurleri.Contains(h.IslemTuru)).Sum(Etki)
            })
            .ToList();

    /// <summary>Hareketin ters kaydını üretir. Orijinal kayıt değişmez.</summary>
    public static MaliHareket TersKayit(MaliHareket orijinal, string neden, string kullanici, DateOnly tarih)
    {
        if (orijinal.TersKayitMi)
            throw new InvalidOperationException("Ters kaydın ters kaydı alınamaz; yeni bir hareket girin.");
        if (string.IsNullOrWhiteSpace(neden))
            throw new InvalidOperationException("Ters kayıt nedeni zorunludur.");

        return new MaliHareket
        {
            IslemKimligi = $"Ters:{orijinal.IslemKimligi}",
            PersonelId = orijinal.PersonelId,
            CalismaDonemiId = orijinal.CalismaDonemiId,
            IslemTarihi = tarih,
            DonemYil = orijinal.DonemYil,
            DonemAy = orijinal.DonemAy,
            IslemTuru = orijinal.IslemTuru,
            BakiyeTuru = orijinal.BakiyeTuru,
            Tutar = -orijinal.Tutar,
            Aciklama = $"TERS KAYIT: {orijinal.Aciklama} | Neden: {neden}",
            KaynakTuru = orijinal.KaynakTuru,
            KaynakReferans = orijinal.KaynakReferans,
            KaynakBelge = orijinal.KaynakBelge,
            KullaniciAdi = kullanici,
            Sirket = orijinal.Sirket,
            Isyeri = orijinal.Isyeri,
            IlgiliHareketId = orijinal.Id,
            TersKayitMi = true
        };
    }

    /// <summary>Hareketin tarihine göre ait olduğu çalışma dönemi.</summary>
    public static CalismaDonemi? CalismaDonemiBul(IEnumerable<CalismaDonemi> donemler, DateOnly tarih)
    {
        var list = donemler.OrderByDescending(d => d.IseGirisTarihi).ToList();
        return list.FirstOrDefault(d => d.IseGirisTarihi <= tarih && (d.IstenCikisTarihi is null || tarih <= d.IstenCikisTarihi))
               ?? list.FirstOrDefault(d => d.IseGirisTarihi <= tarih)
               ?? list.LastOrDefault();
    }

    public static string TurEtiketi(MaliIslemTuru tur) => tur switch
    {
        MaliIslemTuru.MaasHakEdis => "Maaş hak edişi",
        MaliIslemTuru.BankaMaasOdemesi => "Banka ödemesi",
        MaliIslemTuru.KasaOdemesi => "Kasa ödemesi",
        MaliIslemTuru.AvansVerildi => "Avans verildi",
        MaliIslemTuru.AvansMahsup => "Avans mahsubu",
        MaliIslemTuru.IcraKesinti => "İcra kesintisi",
        MaliIslemTuru.BesKesinti => "BES kesintisi",
        MaliIslemTuru.DigerKesinti => "Diğer kesinti",
        MaliIslemTuru.Prim => "Prim",
        MaliIslemTuru.Ikramiye => "İkramiye",
        MaliIslemTuru.FazlaMesai => "Fazla mesai",
        MaliIslemTuru.KidemTazminati => "Kıdem tazminatı",
        MaliIslemTuru.IhbarTazminati => "İhbar tazminatı",
        MaliIslemTuru.EksikOdemeFarki => "Eksik ödeme farkı",
        MaliIslemTuru.FazlaOdemeFarki => "Fazla ödeme (inceleme)",
        MaliIslemTuru.IadeAlinan => "İade alındı",
        MaliIslemTuru.OncekiDonemDuzeltme => "Önceki dönem düzeltmesi",
        MaliIslemTuru.DevredenBorc => "Devreden borç",
        MaliIslemTuru.DevredenAlacak => "Devreden alacak",
        MaliIslemTuru.MaasDegisikligi => "Maaş değişikliği",
        MaliIslemTuru.IzinUcreti => "İzin ücreti",
        MaliIslemTuru.IseGiris => "İşe giriş",
        MaliIslemTuru.IstenCikis => "İşten çıkış",
        MaliIslemTuru.FazlaOdemeKesinlesti => "Fazla ödeme kesinleşti",
        MaliIslemTuru.FazlaOdemeAciklandi => "Fazla ödeme açıklandı",
        MaliIslemTuru.BordroKalemBilgisi => "Bordro kalemi (bilgi)",
        _ => tur.ToString()
    };

    public static string BakiyeEtiketi(BakiyeTuru b) => b switch
    {
        BakiyeTuru.MaasAlacagi => "Maaş alacağı",
        BakiyeTuru.AvansBorcu => "Avans borcu",
        BakiyeTuru.SupheliFazlaOdeme => "Şüpheli fazla ödeme",
        BakiyeTuru.KesinlesmisGeriOdeme => "Kesin geri ödeme",
        _ => "Bilgi"
    };
}
