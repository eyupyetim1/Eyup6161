using PayControl.Domain.Enums;

namespace PayControl.Domain.Services;

/// <summary>
/// Basit hesaplama stub'ı. Belirsiz durumlarda nihai sonuç üretmez.
/// Aylık maaşı 30'a bölüp kesin net ödeme sonucu olarak yazmaz.
/// </summary>
public sealed class HakEdisHesaplamaServisi : IHakEdisHesaplamaServisi
{
    public HakEdisHesaplamaSonucu CikisAyiHakEdisHesapla(
        decimal? aylikMaas,
        MaasTuru maasTuru,
        DateOnly cikisTarihi,
        decimal? bordroGunu,
        decimal? bordroNetIstihkak,
        bool bordroOnayli)
    {
        // Onaylı bordro net istihkakı varsa bu kaynak esas alınır.
        if (bordroOnayli && bordroNetIstihkak.HasValue)
        {
            return HakEdisHesaplamaSonucu.Onayli(
                bordroNetIstihkak.Value,
                "OnayliBordroNetIstihkak");
        }

        var kritikEksikler = new List<string>();
        if (!aylikMaas.HasValue)
            kritikEksikler.Add("Aylık maaş belirsiz");
        if (maasTuru == MaasTuru.Belirsiz)
            kritikEksikler.Add("Maaş net/brüt türü belirsiz");
        if (!bordroGunu.HasValue)
            kritikEksikler.Add("Puantaj/hak ediş günü belirsiz");

        if (kritikEksikler.Count > 0)
            return HakEdisHesaplamaSonucu.Belirsiz(kritikEksikler.ToArray());

        // Bilgi kısmen yeterli olsa bile net/brüt dönüşümü ve ay gün sayısı için onaylı kural seti yoksa
        // tahmini değeri yalnızca bilgilendirme amaçlı bırakırız; nihai kabul etmeyiz.
        var tahmini = aylikMaas!.Value * (bordroGunu!.Value / 30m);
        var eksikler = new List<string> { "Onaylı bordro hesaplama kural seti" };
        if (!bordroOnayli)
            eksikler.Add("Bordro onayı yok");

        return new HakEdisHesaplamaSonucu
        {
            NihaiSonucMu = false,
            TahminiTutar = Math.Round(tahmini, 2),
            UyariMesaji = "30 günlük basitleştirilmiş oran yalnızca tahmindir; nihai sonuç için onaylı bordro kuralı gerekir.",
            HesaplamaYontemi = "BasitOranTahmini",
            EksikBilgiler = eksikler
        };
    }
}
