using System.Text;

namespace PayControl.Domain.Services;

/// <summary>
/// Ad soyad temizliği: Excel'den gelen çift/üçlü boşluklar, sekme, satır sonu, bölünemez boşluk (NBSP)
/// ve görünmez karakterler tek boşluğa indirilir; baş/son boşluk atılır. Harf büyüklüğüne dokunulmaz.
/// Örnek: "  EYÜP   YETİM " → "EYÜP YETİM".
/// </summary>
public static class IsimKurallari
{
    public static string? Duzelt(string? ad)
    {
        if (ad is null) return null;
        var sb = new StringBuilder(ad.Length);
        var oncekiBosluk = true; // baştaki boşlukları atmak için
        foreach (var c in ad)
        {
            if (Gorunmez(c)) continue;
            if (char.IsWhiteSpace(c) || c == ' ' || c == ' ' || c == ' ')
            {
                if (!oncekiBosluk) sb.Append(' ');
                oncekiBosluk = true;
                continue;
            }
            sb.Append(c);
            oncekiBosluk = false;
        }
        if (sb.Length > 0 && sb[^1] == ' ') sb.Length--;
        return sb.ToString();
    }

    /// <summary>Düzeltme gerekiyor mu (fazla/garip boşluk veya görünmez karakter var mı)?</summary>
    public static bool DuzeltmeGerekirMi(string? ad) => ad is not null && !string.Equals(Duzelt(ad), ad, StringComparison.Ordinal);

    private static bool Gorunmez(char c) => c is '​' or '‌' or '‍' or '⁠' or '﻿' or '­';
}

/// <summary>
/// Türkçe arama: büyük/küçük harf, Türkçe harf (ı/i, ş/s, ğ/g, ü/u, ö/o, ç/c), fazla boşluk ve kelime sırası farkı yok sayılır.
/// "eyup yetim", "YETİM eyüp", "Eyüp  Yetim" → "EYÜP YETİM" ile eşleşir. Rakamlar (TC, sicil) parça olarak aranır.
/// </summary>
public static class AramaKurallari
{
    public static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var u = (IsimKurallari.Duzelt(s) ?? "").ToUpper(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        var sb = new System.Text.StringBuilder(u.Length);
        foreach (var c in u)
            sb.Append(c switch
            {
                'İ' or 'I' or 'Î' => 'I', 'Ş' => 'S', 'Ğ' => 'G', 'Ü' or 'Û' => 'U', 'Ö' => 'O', 'Ç' => 'C', 'Â' => 'A',
                _ => c
            });
        return sb.ToString();
    }

    /// <summary>Aramadaki her kelime alanlardan birinde geçiyorsa true (boş arama her şeyle eşleşir).</summary>
    public static bool Eslesir(string? arama, params string?[] alanlar)
    {
        var a = Normalize(arama);
        if (a.Length == 0) return true;
        var metin = string.Join(" | ", alanlar.Where(x => !string.IsNullOrEmpty(x)).Select(Normalize));
        return a.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(metin.Contains);
    }
}
