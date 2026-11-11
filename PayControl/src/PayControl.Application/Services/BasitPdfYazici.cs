using System.Globalization;
using System.Text;

namespace PayControl.Application.Services;

/// <summary>
/// Harici kütüphane gerektirmeyen, çok sayfalı metin/tablo PDF üretici.
/// Türkçe karakterler ISO-8859-9 kodlanır; Helvetica'nın ğ/Ğ/ı/İ/ş/Ş glifleri /Differences ile eşlenir.
/// </summary>
public sealed class BasitPdfYazici
{
    private const float SayfaGenislik = 842f;   // A4 yatay
    private const float SayfaYukseklik = 595f;
    private const float Kenar = 32f;
    private const float SatirAraligi = 11f;

    private readonly List<List<(float X, float Y, float Boyut, bool Kalin, string Metin)>> _sayfalar = new();
    private List<(float, float, float, bool, string)> _sayfa = null!;
    private float _y;

    public BasitPdfYazici() => YeniSayfa();

    public float KullanilabilirGenislik => SayfaGenislik - 2 * Kenar;

    public void YeniSayfa()
    {
        _sayfa = new List<(float, float, float, bool, string)>();
        _sayfalar.Add(_sayfa);
        _y = SayfaYukseklik - Kenar;
    }

    public void Baslik(string metin, float boyut = 13f)
    {
        YerAc(boyut + 4);
        _sayfa.Add((Kenar, _y, boyut, true, metin));
        _y -= boyut + 4;
    }

    public void Satir(string metin, float boyut = 8.5f, bool kalin = false)
    {
        foreach (var parca in Sar(metin, (int)(KullanilabilirGenislik / (boyut * 0.5f))))
        {
            YerAc(SatirAraligi);
            _sayfa.Add((Kenar, _y, boyut, kalin, parca));
            _y -= SatirAraligi;
        }
    }

    public void Bosluk(float pt = 6f) => _y -= pt;

    /// <summary>Kolon genişlikleri karakter cinsinden; başlık her sayfada tekrarlanır.</summary>
    public void Tablo(IReadOnlyList<(string Baslik, int Genislik, bool Sag)> kolonlar, IEnumerable<string[]> satirlar, float boyut = 7f)
    {
        var karakter = boyut * 0.55f;
        void BaslikYaz()
        {
            YerAc(SatirAraligi * 2);
            var x = Kenar;
            foreach (var k in kolonlar)
            {
                var b = Kes(k.Baslik, k.Genislik);
                var bx = k.Sag ? (k.Genislik - 1 - b.Length) * karakter : 0;
                _sayfa.Add((x + Math.Max(0, bx), _y, boyut, true, b));
                x += k.Genislik * karakter;
            }
            _y -= SatirAraligi;
        }

        BaslikYaz();
        foreach (var satir in satirlar)
        {
            if (_y - SatirAraligi < Kenar)
            {
                YeniSayfa();
                BaslikYaz();
            }
            var x = Kenar;
            for (var i = 0; i < kolonlar.Count; i++)
            {
                var deger = Kes(i < satir.Length ? satir[i] ?? "" : "", kolonlar[i].Genislik);
                var dx = kolonlar[i].Sag ? (kolonlar[i].Genislik - 1 - deger.Length) * karakter : 0;
                _sayfa.Add((x + Math.Max(0, dx), _y, boyut, false, deger));
                x += kolonlar[i].Genislik * karakter;
            }
            _y -= SatirAraligi;
        }
    }

    public byte[] Olustur()
    {
        var latin5 = Latin5();
        var nesneler = new List<byte[]>();
        int SonrakiNo() => nesneler.Count + 1;

        // 1 katalog, 2 sayfalar, 3 font normal, 4 font kalın, 5 encoding; ardından sayfa + içerik çiftleri
        var sayfaNolari = new List<int>();
        var ilkSayfaNo = 6;
        for (var i = 0; i < _sayfalar.Count; i++)
            sayfaNolari.Add(ilkSayfaNo + i * 2);

        nesneler.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));
        nesneler.Add(Ascii($"<< /Type /Pages /Kids [{string.Join(" ", sayfaNolari.Select(n => $"{n} 0 R"))}] /Count {_sayfalar.Count} >>"));
        nesneler.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding 5 0 R >>"));
        nesneler.Add(Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding 5 0 R >>"));
        nesneler.Add(Ascii("<< /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences [208 /Gbreve 221 /Idotaccent 222 /Scedilla 240 /gbreve 253 /dotlessi 254 /scedilla] >>"));

        for (var i = 0; i < _sayfalar.Count; i++)
        {
            var icerik = new MemoryStream();
            void Yaz(string s) => icerik.Write(Encoding.ASCII.GetBytes(s));
            foreach (var (x, y, boyut, kalin, metin) in _sayfalar[i])
            {
                Yaz($"BT /{(kalin ? "F2" : "F1")} {F(boyut)} Tf {F(x)} {F(y)} Td (");
                foreach (var b in latin5.GetBytes(Sadelestir(metin)))
                {
                    if (b is (byte)'(' or (byte)')' or (byte)'\\') { icerik.WriteByte((byte)'\\'); icerik.WriteByte(b); }
                    else if (b < 32 || b > 126) Yaz("\\" + Convert.ToString(b, 8).PadLeft(3, '0'));
                    else icerik.WriteByte(b);
                }
                Yaz(") Tj ET\n");
            }
            var footer = $"Sayfa {i + 1} / {_sayfalar.Count}";
            Yaz($"BT /F1 7 Tf {F(SayfaGenislik - Kenar - 50)} {F(Kenar / 2)} Td ({footer}) Tj ET\n");
            var akis = icerik.ToArray();

            nesneler.Add(Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {F(SayfaGenislik)} {F(SayfaYukseklik)}] " +
                               $"/Contents {SonrakiNo() + 1} 0 R /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> >>"));
            nesneler.Add(Ascii($"<< /Length {akis.Length} >>\nstream\n").Concat(akis).Concat(Ascii("\nendstream")).ToArray());
        }

        using var ms = new MemoryStream();
        ms.Write(Ascii("%PDF-1.4\n%âã\n"));
        var ofsetler = new List<long>();
        for (var i = 0; i < nesneler.Count; i++)
        {
            ofsetler.Add(ms.Position);
            ms.Write(Ascii($"{i + 1} 0 obj\n"));
            ms.Write(nesneler[i]);
            ms.Write(Ascii("\nendobj\n"));
        }
        var xref = ms.Position;
        var sb = new StringBuilder();
        sb.Append($"xref\n0 {nesneler.Count + 1}\n0000000000 65535 f \n");
        foreach (var o in ofsetler) sb.Append($"{o:D10} 00000 n \n");
        sb.Append($"trailer\n<< /Size {nesneler.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        ms.Write(Ascii(sb.ToString()));
        return ms.ToArray();
    }

    /// <summary>ISO-8859-9'da olmayan karakterlerin okunur karşılıkları.</summary>
    private static string Sadelestir(string s) => s
        .Replace('—', '-').Replace('–', '-').Replace('→', '>').Replace('…', '.')
        .Replace('“', '"').Replace('”', '"').Replace('‘', '\'').Replace('’', '\'')
        .Replace("₺", "TL");

    private void YerAc(float gereken)
    {
        if (_y - gereken < Kenar) YeniSayfa();
    }

    private static Encoding Latin5()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(28599, EncoderFallback.ReplacementFallback, DecoderFallback.ReplacementFallback);
        }
        catch
        {
            return Encoding.Latin1;
        }
    }

    private static IEnumerable<string> Sar(string metin, int max)
    {
        foreach (var satir in metin.Replace("\r", "").Split('\n'))
        {
            var kalan = satir;
            while (kalan.Length > max)
            {
                var kes = kalan.LastIndexOf(' ', max);
                if (kes <= 0) kes = max;
                yield return kalan[..kes];
                kalan = kalan[kes..].TrimStart();
            }
            yield return kalan;
        }
    }

    private static string Kes(string s, int n) => s.Length <= n - 1 ? s : s[..Math.Max(0, n - 2)] + "…".Replace("…", ".");
    private static string F(float f) => f.ToString("0.##", CultureInfo.InvariantCulture);
    private static byte[] Ascii(string s) => Encoding.Latin1.GetBytes(s);
}
