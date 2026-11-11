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
