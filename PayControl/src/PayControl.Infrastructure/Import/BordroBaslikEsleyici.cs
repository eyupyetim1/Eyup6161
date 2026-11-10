namespace PayControl.Infrastructure.Import;

/// <summary>
/// Kullanıcı Excel başlıklarını sistem alanlarına eşler.
/// Belirsiz başlıklarda otomatik atama yapılmaz.
/// </summary>
public static class BordroBaslikEsleyici
{
    private static readonly Dictionary<string, string> BilinenEslesmeler = new(StringComparer.OrdinalIgnoreCase)
    {
        ["T.C. KİMLİK NO"] = "TcKimlikNo",
        ["T.C. KIMLIK NO"] = "TcKimlikNo",
        ["TC KİMLİK NO"] = "TcKimlikNo",
        ["TC KIMLIK NO"] = "TcKimlikNo",
        ["TC"] = "TcKimlikNo",
        ["ADI SOYADI"] = "AdSoyad",
        ["AD SOYAD"] = "AdSoyad",
        ["AD SOYADI"] = "AdSoyad",
        ["İŞYERİ"] = "Isyeri",
        ["ISYERI"] = "Isyeri",
        ["GÖREVİ"] = "Gorevi",
        ["GOREVI"] = "Gorevi",
        ["MAAŞ (NET/BRÜT)"] = "Maas",
        ["MAAS (NET/BRUT)"] = "Maas",
        ["MAAŞ (Net/Brüt)"] = "Maas",
        ["MAAŞ"] = "Maas",
        ["MAAS"] = "Maas",
        ["GİRİŞ TARİHİ"] = "GirisTarihi",
        ["GIRIS TARIHI"] = "GirisTarihi",
        ["ÇIKIŞ TARİHİ"] = "CikisTarihi",
        ["CIKIS TARIHI"] = "CikisTarihi",
        ["GÜN"] = "Gun",
        ["GUN"] = "Gun",
        ["NET İSTİHKAK"] = "NetIstihkak",
        ["NET ISTIHKAK"] = "NetIstihkak",
        ["IBAN NO"] = "Iban",
        ["IBAN"] = "Iban",
        ["AVANS"] = "Avans",
        ["İCRA"] = "Icra",
        ["ICRA"] = "Icra",
        ["OTO.KAT.BES"] = "OtoKatBes",
        ["OTO KAT BES"] = "OtoKatBes",
        ["BES"] = "OtoKatBes",
        ["TRAFİK CEZASI"] = "TrafikCezasi",
        ["TRAFIK CEZASI"] = "TrafikCezasi",
        ["AİLE"] = "Aile",
        ["AILE"] = "Aile",
        ["İHBAR TAZMİNATI"] = "IhbarTazminati",
        ["IHBAR TAZMINATI"] = "IhbarTazminati",
        ["YEMEK"] = "Yemek",
        ["YOL"] = "Yol"
    };

    public sealed record EslemeSonucu(
        IReadOnlyDictionary<int, string> KolonAlanlari,
        IReadOnlyList<string> EslesenBasliklar,
        IReadOnlyList<string> EslesmeyenBasliklar,
        IReadOnlyList<string> BelirsizBasliklar);

    public static EslemeSonucu Esle(IReadOnlyList<string> basliklar)
    {
        var kolonAlanlari = new Dictionary<int, string>();
        var eslesen = new List<string>();
        var eslesmeyen = new List<string>();
        var belirsiz = new List<string>();

        for (var i = 0; i < basliklar.Count; i++)
        {
            var ham = (basliklar[i] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(ham))
                continue;

            var normalize = Normalize(ham);
            var bulundu = false;

            foreach (var kv in BilinenEslesmeler)
            {
                if (Normalize(kv.Key) == normalize)
                {
                    kolonAlanlari[i] = kv.Value;
                    eslesen.Add(ham);
                    bulundu = true;
                    break;
                }
            }

            if (!bulundu)
            {
                // Kısmi benzerlik: belirsiz — otomatik atama yok
                if (normalize.Contains("MAAS") || normalize.Contains("MAAŞ") ||
                    normalize.Contains("TAZMINAT") || normalize.Contains("KESINTI"))
                {
                    belirsiz.Add(ham);
                }
                else
                {
                    eslesmeyen.Add(ham);
                }
            }
        }

        return new EslemeSonucu(kolonAlanlari, eslesen, eslesmeyen, belirsiz);
    }

    private static string Normalize(string value)
    {
        return value
            .Replace('\u00A0', ' ')
            .Trim()
            .ToUpperInvariant()
            .Replace("İ", "I")
            .Replace("Ş", "S")
            .Replace("Ğ", "G")
            .Replace("Ü", "U")
            .Replace("Ö", "O")
            .Replace("Ç", "C")
            .Replace(".", " ")
            .Replace("  ", " ");
    }
}
