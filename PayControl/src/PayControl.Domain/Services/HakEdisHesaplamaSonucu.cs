namespace PayControl.Domain.Services;

/// <summary>
/// Ücret hesaplama sonucu. Belirsizlik varsa otomatik nihai sonuç kabul edilmez.
/// </summary>
public sealed class HakEdisHesaplamaSonucu
{
    public bool NihaiSonucMu { get; init; }
    public decimal? TahminiTutar { get; init; }
    public string? UyariMesaji { get; init; }
    public string HesaplamaYontemi { get; init; } = "Belirsiz";
    public IReadOnlyList<string> EksikBilgiler { get; init; } = Array.Empty<string>();

    public static HakEdisHesaplamaSonucu Belirsiz(params string[] eksikler) => new()
    {
        NihaiSonucMu = false,
        TahminiTutar = null,
        UyariMesaji = "Hesaplama belirsiz; otomatik tahmini nihai sonuç olarak kabul edilmedi.",
        HesaplamaYontemi = "OnayBekliyor",
        EksikBilgiler = eksikler
    };

    public static HakEdisHesaplamaSonucu Onayli(decimal tutar, string yontem) => new()
    {
        NihaiSonucMu = true,
        TahminiTutar = tutar,
        HesaplamaYontemi = yontem
    };
}
