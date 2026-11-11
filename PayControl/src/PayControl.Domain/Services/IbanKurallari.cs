using System.Globalization;
using System.Numerics;
using System.Text;

namespace PayControl.Domain.Services;

public static class IbanKurallari
{
    public static string? Normalize(string? iban) =>
        string.IsNullOrWhiteSpace(iban) ? null : new string(iban.Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();

    /// <summary>ISO 13616 mod-97 kontrolü; TR IBAN'ı 26 karakter olmalıdır.</summary>
    public static bool GecerliMi(string? iban)
    {
        var n = Normalize(iban);
        if (n is null || n.Length < 15 || n.Length > 34 || !n.All(char.IsLetterOrDigit)) return false;
        if (n.StartsWith("TR", StringComparison.Ordinal) && n.Length != 26) return false;

        var yeniden = n[4..] + n[..4];
        var sb = new StringBuilder();
        foreach (var c in yeniden)
            sb.Append(char.IsLetter(c) ? (c - 'A' + 10).ToString(CultureInfo.InvariantCulture) : c.ToString());
        return BigInteger.Parse(sb.ToString(), CultureInfo.InvariantCulture) % 97 == 1;
    }

    /// <summary>Banka satırındaki ad ile kart adı aynı kişiyi gösteriyor mu (Türkçe harf ve sıra farkına dayanıklı).</summary>
    public static bool AdUyumluMu(string? a, string? b)
    {
        var ta = Tokenler(a);
        var tb = Tokenler(b);
        if (ta.Count == 0 || tb.Count == 0) return true; // ad yoksa karar verilmez
        var (kisa, uzun) = ta.Count <= tb.Count ? (ta, tb) : (tb, ta);
        return kisa.All(uzun.Contains) || string.Concat(ta.OrderBy(x => x)) == string.Concat(tb.OrderBy(x => x));
    }

    private static HashSet<string> Tokenler(string? ad)
    {
        if (string.IsNullOrWhiteSpace(ad)) return new HashSet<string>();
        var u = ad.ToUpper(CultureInfo.GetCultureInfo("tr-TR"))
            .Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G').Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
        return u.Split(new[] { ' ', '.', ',', '-', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => new string(t.Where(char.IsLetter).ToArray()))
            .Where(t => t.Length > 1)
            .ToHashSet();
    }
}
