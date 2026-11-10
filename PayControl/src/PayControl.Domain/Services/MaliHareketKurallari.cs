using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Domain.Services;

public static class MaliHareketKurallari
{
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

    public static EkstreOzeti OzetHesapla(IEnumerable<MaliHareket> hareketler)
    {
        var list = hareketler.ToList();
        var hakEdis = list.Where(h => h.BakiyeTuru == BakiyeTuru.MaasAlacagi &&
                                      h.IslemTuru is MaliIslemTuru.MaasHakEdis or MaliIslemTuru.Prim
                                          or MaliIslemTuru.Ikramiye or MaliIslemTuru.FazlaMesai
                                          or MaliIslemTuru.KidemTazminati or MaliIslemTuru.IhbarTazminati
                                          or MaliIslemTuru.IzinUcreti)
            .Sum(h => h.Tutar);

        var odemeler = list.Where(h => h.IslemTuru is MaliIslemTuru.BankaMaasOdemesi or MaliIslemTuru.KasaOdemesi)
            .Sum(h => h.Tutar);

        var avansBorc = list.Where(h => h.BakiyeTuru == BakiyeTuru.AvansBorcu).Sum(h =>
            h.IslemTuru == MaliIslemTuru.AvansVerildi ? h.Tutar :
            h.IslemTuru == MaliIslemTuru.AvansMahsup ? -h.Tutar : 0m);

        var fazla = list.Where(h => h.BakiyeTuru == BakiyeTuru.SupheliFazlaOdeme).Sum(h => h.Tutar);
        var geriOdeme = list.Where(h => h.BakiyeTuru == BakiyeTuru.KesinlesmisGeriOdeme).Sum(h => h.Tutar);

        return new EkstreOzeti
        {
            ToplamHakEdis = hakEdis,
            GerceklesenToplamOdeme = odemeler,
            OdenmemisHakEdis = Math.Max(0, hakEdis - odemeler),
            AcikAvansBorcu = Math.Max(0, avansBorc),
            IncelemeBekleyenFazlaOdeme = Math.Max(0, fazla),
            KesinlesmisGeriOdemeBorcu = Math.Max(0, geriOdeme)
        };
    }
}
