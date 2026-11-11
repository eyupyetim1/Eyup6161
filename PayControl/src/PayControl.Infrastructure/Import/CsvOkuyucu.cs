using System.Text;

namespace PayControl.Infrastructure.Import;

/// <summary>RFC 4180 CSV okuyucu (tırnaklı alan, "" kaçışı, alan içinde satır sonu). Başlık satırını sözlük anahtarı yapar.</summary>
public static class CsvOkuyucu
{
    public static List<Dictionary<string, string>> Oku(string yol)
    {
        var metin = File.ReadAllText(yol, Encoding.UTF8);
        var satirlar = Ayristir(metin);
        if (satirlar.Count == 0) return new();
        var basliklar = satirlar[0];
        var sonuc = new List<Dictionary<string, string>>(satirlar.Count - 1);
        for (var i = 1; i < satirlar.Count; i++)
        {
            var s = satirlar[i];
            if (s.Count == 1 && s[0].Length == 0) continue;
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var j = 0; j < basliklar.Count; j++) d[basliklar[j]] = j < s.Count ? s[j] : "";
            sonuc.Add(d);
        }
        return sonuc;
    }

    public static List<List<string>> Ayristir(string metin)
    {
        var satirlar = new List<List<string>>();
        var satir = new List<string>();
        var alan = new StringBuilder();
        var tirnak = false;
        for (var i = 0; i < metin.Length; i++)
        {
            var c = metin[i];
            if (i == 0 && c == '﻿') continue;
            if (tirnak)
            {
                if (c == '"')
                {
                    if (i + 1 < metin.Length && metin[i + 1] == '"') { alan.Append('"'); i++; }
                    else tirnak = false;
                }
                else alan.Append(c);
                continue;
            }
            switch (c)
            {
                case '"': tirnak = true; break;
                case ',': satir.Add(alan.ToString()); alan.Clear(); break;
                case '\r': break;
                case '\n': satir.Add(alan.ToString()); alan.Clear(); satirlar.Add(satir); satir = new List<string>(); break;
                default: alan.Append(c); break;
            }
        }
        if (alan.Length > 0 || satir.Count > 0) { satir.Add(alan.ToString()); satirlar.Add(satir); }
        return satirlar;
    }
}
