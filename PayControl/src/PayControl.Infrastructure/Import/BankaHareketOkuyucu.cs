using PayControl.Domain.Services;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ClosedXML.Excel;
using PayControl.Application.Interfaces;

namespace PayControl.Infrastructure.Import;

/// <summary>
/// Bankadan alınan gerçekleşen ödeme dökümünü okur. Biçim içerikten algılanır:
/// Excel (.xlsx), SWIFT MT940 hesap özeti veya ISO 20022 CAMT.053 (XML).
/// Excel için gerekli kolonlar: tarih, tutar ve TC veya IBAN. Referans/dekont no varsa mükerrer kontrolünde esas alınır.
/// MT940/CAMT dosyalarından yalnızca borç (giden) hareketler alınır; ters kayıtlar (iptal) atlanır.
/// </summary>
public static class BankaHareketOkuyucu
{
    public enum Bicim { Excel, Mt940, Camt053 }

    public static Bicim BicimAlgila(byte[] icerik)
    {
        if (icerik.Length >= 2 && icerik[0] == 0x50 && icerik[1] == 0x4B) return Bicim.Excel; // ZIP (xlsx)
        var bas = Encoding.UTF8.GetString(icerik, 0, Math.Min(icerik.Length, 4096)).TrimStart('\uFEFF', ' ', '\r', '\n', '\t');
        if (bas.StartsWith('<')) return Bicim.Camt053;
        if (bas.Contains(":20:") || bas.Contains(":61:") || bas.StartsWith("{1:")) return Bicim.Mt940;
        throw new InvalidOperationException("Dosya biçimi tanınmadı. Excel (.xlsx), MT940 (.sta/.txt) veya CAMT.053 (.xml) yükleyin.");
    }

    public static IReadOnlyList<BankaHareketSatiri> Oku(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();
        return BicimAlgila(bytes) switch
        {
            Bicim.Mt940 => Mt940Oku(Encoding.UTF8.GetString(bytes)),
            Bicim.Camt053 => CamtOku(bytes),
            _ => ExcelOku(new MemoryStream(bytes))
        };
    }

    // ------------------------------------------------------------------ MT940

    private static readonly Regex Satir61 = new(
        @"^(?<tarih>\d{6})(?<kayit>\d{4})?(?<yon>RC|RD|C|D)(?<fon>[A-Z])?(?<tutar>\d+,\d{0,2})(?<tur>[NSF][A-Z0-9]{3})(?<ref>.*?)(?://(?<bref>.*))?$",
        RegexOptions.Compiled);
    private static readonly Regex IbanDeseni = new(@"TR\d{2}\s?(?:\d{4}\s?){5}\d{2}", RegexOptions.Compiled);
    private static readonly Regex OnBirHane = new(@"(?<!\d)\d{11}(?!\d)", RegexOptions.Compiled);

    public static IReadOnlyList<BankaHareketSatiri> Mt940Oku(string metin)
    {
        // Etiketleri (:XX:) ve devam satırlarını birleştir
        var alanlar = new List<(string Etiket, string Deger, int Satir)>();
        var satirNo = 0;
        foreach (var hamSatir in metin.Replace("\r\n", "\n").Split('\n'))
        {
            satirNo++;
            var s = hamSatir.TrimEnd();
            if (s.Length == 0 || s == "-" || s.StartsWith("-}") || s.StartsWith("{")) { if (s.Contains(":20:")) s = s[s.IndexOf(":20:")..]; else continue; }
            var m = Regex.Match(s, @"^:(\d{2}[A-Z]?):(.*)$");
            if (m.Success) alanlar.Add((m.Groups[1].Value, m.Groups[2].Value, satirNo));
            else if (alanlar.Count > 0) { var son = alanlar[^1]; alanlar[^1] = (son.Etiket, son.Deger + "\n" + s, son.Satir); }
        }
        if (!alanlar.Any(a => a.Etiket == "61")) throw new InvalidOperationException("MT940 dosyasında işlem satırı (:61:) bulunamadı.");

        var liste = new List<BankaHareketSatiri>();
        for (var i = 0; i < alanlar.Count; i++)
        {
            if (alanlar[i].Etiket != "61") continue;
            var ilk = alanlar[i].Deger.Split('\n')[0];
            var m = Satir61.Match(ilk);
            if (!m.Success) throw new InvalidOperationException($"MT940 satır {alanlar[i].Satir}: :61: alanı çözümlenemedi ('{ilk}').");
            var aciklama = i + 1 < alanlar.Count && alanlar[i + 1].Etiket == "86" ? alanlar[i + 1].Deger.Replace("\n", "") : "";
            if (m.Groups["yon"].Value != "D") continue; // yalnız giden ödemeler; RD/RC ters kayıttır

            var t = m.Groups["tarih"].Value;
            var tarih = new DateOnly(2000 + int.Parse(t[..2], CultureInfo.InvariantCulture), int.Parse(t[2..4], CultureInfo.InvariantCulture), int.Parse(t[4..6], CultureInfo.InvariantCulture));
            var tutar = decimal.Parse(m.Groups["tutar"].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            var refm = m.Groups["ref"].Value.Trim();
            var bref = m.Groups["bref"].Value.Trim();
            var referans = refm.Length > 0 && !refm.Equals("NONREF", StringComparison.OrdinalIgnoreCase) ? refm : bref.Length > 0 ? bref : null;

            var (iban, tc, ad, serbest) = AciklamaCozumle(aciklama);
            liste.Add(new BankaHareketSatiri(alanlar[i].Satir, tarih, tc, ad, iban, tutar, referans, serbest));
        }
        return liste;
    }

    /// <summary>:86: alanı: yapılandırılmış (?20..?33 alt alanları) veya serbest metin. IBAN ve geçerli TC kimlik no aranır.</summary>
    private static (string? Iban, string? Tc, string? Ad, string Aciklama) AciklamaCozumle(string a)
    {
        string? ad = null;
        var serbest = a;
        if (a.Contains('?'))
        {
            var parca = Regex.Matches(a, @"\?(\d{2})([^?]*)").ToDictionary(x => x.Groups[1].Value, x => x.Groups[2].Value.Trim(), StringComparer.Ordinal);
            var adParca = new[] { "32", "33" }.Where(parca.ContainsKey).Select(k => parca[k]).ToList();
            if (adParca.Count > 0) ad = string.Join("", adParca).Trim();
            serbest = string.Join(" ", parca.Where(kv => kv.Key is "20" or "21" or "22" or "23" or "24" or "25" or "26" or "27" or "28" or "29").Select(kv => kv.Value)).Trim();
            if (parca.TryGetValue("31", out var hesap) && IbanDeseni.IsMatch(hesap.Replace(" ", ""))) a += " " + hesap;
        }
        var iban = IbanDeseni.Match(a) is { Success: true } im ? im.Value.Replace(" ", "") : null;
        var tc = OnBirHane.Matches(iban is null ? a : a.Replace(iban, "")).Select(x => x.Value).FirstOrDefault(TcGecerli);
        return (iban, tc, ad, serbest.Length == 0 ? a : serbest);
    }

    public static bool TcGecerli(string tc)
    {
        if (tc.Length != 11 || tc[0] == '0' || !tc.All(char.IsDigit)) return false;
        var d = tc.Select(c => c - '0').ToArray();
        var tek = d[0] + d[2] + d[4] + d[6] + d[8];
        var cift = d[1] + d[3] + d[5] + d[7];
        if (((tek * 7 - cift) % 10 + 10) % 10 != d[9]) return false;
        return d.Take(10).Sum() % 10 == d[10];
    }

    // ------------------------------------------------------------------ CAMT.053

    public static IReadOnlyList<BankaHareketSatiri> CamtOku(byte[] xml)
    {
        XDocument doc;
        try
        {
            using var ms = new MemoryStream(xml);
            using var r = XmlReader.Create(ms, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            doc = XDocument.Load(r);
        }
        catch (XmlException ex) { throw new InvalidOperationException($"CAMT dosyası geçerli XML değil: {ex.Message}"); }

        static IEnumerable<XElement> E(XElement? x, string ad) => x?.Elements().Where(e => e.Name.LocalName == ad) ?? [];
        static XElement? B(XElement? x, params string[] yol) { foreach (var p in yol) x = E(x, p).FirstOrDefault(); return x; }
        static string? V(XElement? x, params string[] yol) => B(x, yol)?.Value.Trim() is { Length: > 0 } v ? v : null;

        var girisler = doc.Descendants().Where(e => e.Name.LocalName == "Ntry").ToList();
        if (doc.Root is null || !doc.Descendants().Any(e => e.Name.LocalName is "Stmt" or "Rpt"))
            throw new InvalidOperationException("CAMT.053 hesap özeti (Stmt) bulunamadı.");

        var liste = new List<BankaHareketSatiri>();
        var no = 0;
        foreach (var n in girisler)
        {
            no++;
            if (V(n, "CdtDbtInd") != "DBIT") continue;
            if (string.Equals(V(n, "RvslInd"), "true", StringComparison.OrdinalIgnoreCase)) continue;
            var tarihMetni = V(n, "BookgDt", "Dt") ?? V(n, "BookgDt", "DtTm") ?? V(n, "ValDt", "Dt")
                             ?? throw new InvalidOperationException($"CAMT kaydı {no}: işlem tarihi yok.");
            var tarih = DateOnly.FromDateTime(DateTime.Parse(tarihMetni[..Math.Min(10, tarihMetni.Length)], CultureInfo.InvariantCulture));
            var girisTutari = decimal.Parse(V(n, "Amt")!, CultureInfo.InvariantCulture);
            var islemler = E(B(n, "NtryDtls"), "TxDtls").ToList();
            if (islemler.Count == 0) islemler = [new XElement("TxDtls")];

            foreach (var tx in islemler)
            {
                var tutar = islemler.Count == 1 ? girisTutari
                    : decimal.Parse(V(tx, "Amt") ?? V(tx, "AmtDtls", "TxAmt", "Amt") ?? throw new InvalidOperationException($"CAMT kaydı {no}: toplu işlemde tutar yok."), CultureInfo.InvariantCulture);
                var taraf = B(tx, "RltdPties");
                var ad = V(taraf, "Cdtr", "Nm") ?? V(taraf, "Cdtr", "Pty", "Nm");
                var iban = V(taraf, "CdtrAcct", "Id", "IBAN");
                var tc = V(taraf, "Cdtr", "Id", "PrvtId", "Othr", "Id") ?? V(taraf, "Cdtr", "Pty", "Id", "PrvtId", "Othr", "Id");
                var aciklama = string.Join(" ", E(B(tx, "RmtInf"), "Ustrd").Select(u => u.Value.Trim())) is { Length: > 0 } u ? u : V(n, "AddtlNtryInf");
                if (tc is null && aciklama is not null) tc = OnBirHane.Matches(aciklama).Select(x => x.Value).FirstOrDefault(TcGecerli);
                var referans = V(tx, "Refs", "EndToEndId") is { } e2e && e2e != "NOTPROVIDED" ? e2e
                    : V(tx, "Refs", "AcctSvcrRef") ?? V(n, "AcctSvcrRef") ?? V(n, "NtryRef");
                liste.Add(new BankaHareketSatiri(no, tarih, tc, ad, iban, Math.Abs(tutar), referans, aciklama));
            }
        }
        return liste;
    }

    // ------------------------------------------------------------------ Excel

    public static IReadOnlyList<BankaHareketSatiri> ExcelOku(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var used = ws.RangeUsed() ?? throw new InvalidOperationException("Banka hareket dosyası boş.");
        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();

        int header = -1;
        var map = new Dictionary<string, int>();
        for (var r = firstRow; r <= Math.Min(lastRow, firstRow + 15) && header < 0; r++)
        {
            var m = new Dictionary<string, int>();
            for (var c = firstCol; c <= lastCol; c++)
            {
                var h = N(ws.Cell(r, c).GetString());
                if (h.Length == 0) continue;
                if (h.Contains("TARIH") && !m.ContainsKey("TARIH")) m["TARIH"] = c;
                else if ((h.Contains("KIMLIK") || h == "TC" || h == "TCKN") && !m.ContainsKey("TC")) m["TC"] = c;
                else if (h.Contains("IBAN") && !m.ContainsKey("IBAN")) m["IBAN"] = c;
                else if ((h.Contains("TUTAR") || h.Contains("MIKTAR") || h.Contains("BORC")) && !m.ContainsKey("TUTAR")) m["TUTAR"] = c;
                else if ((h.Contains("REFERANS") || h.Contains("DEKONT") || h.Contains("ISLEM NO") || h == "REF") && !m.ContainsKey("REF")) m["REF"] = c;
                else if (h.Contains("ACIKLAMA") && !m.ContainsKey("ACIKLAMA")) m["ACIKLAMA"] = c;
                else if ((h.Contains("ADI") || h.Contains("SOYAD") || h.Contains("ALICI") || h.Contains("UNVAN")) && !m.ContainsKey("AD")) m["AD"] = c;
            }
            if (m.ContainsKey("TUTAR") && m.ContainsKey("TARIH") && (m.ContainsKey("TC") || m.ContainsKey("IBAN")))
            {
                header = r;
                map = m;
            }
        }
        if (header < 0)
            throw new InvalidOperationException("Banka hareket dosyasında başlık bulunamadı (TARİH, TUTAR ve TC veya IBAN kolonları gerekli).");

        var list = new List<BankaHareketSatiri>();
        for (var r = header + 1; r <= lastRow; r++)
        {
            var tutarCell = ws.Cell(r, map["TUTAR"]);
            if (tutarCell.IsEmpty()) continue;
            decimal tutar;
            if (tutarCell.DataType == XLDataType.Number) tutar = Math.Round((decimal)tutarCell.GetDouble(), 2);
            else if (!decimal.TryParse(tutarCell.GetString(), NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out tutar)) continue;

            var tarihCell = ws.Cell(r, map["TARIH"]);
            DateOnly tarih;
            if (tarihCell.DataType == XLDataType.DateTime) tarih = DateOnly.FromDateTime(tarihCell.GetDateTime());
            else if (!DateOnly.TryParse(tarihCell.GetString(), CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out tarih))
                throw new InvalidOperationException($"Satır {r}: tarih okunamadı ('{tarihCell.GetString()}').");

            string? Get(string k) => map.TryGetValue(k, out var c) ? ws.Cell(r, c).GetString().Trim() : null;
            list.Add(new BankaHareketSatiri(r, tarih, Get("TC"), IsimKurallari.Duzelt(Get("AD")), Get("IBAN"), Math.Abs(tutar),
                string.IsNullOrWhiteSpace(Get("REF")) ? null : Get("REF"), Get("ACIKLAMA")));
        }
        return list;
    }

    private static string N(string v) =>
        (v ?? "").Trim().ToUpper(CultureInfo.GetCultureInfo("tr-TR"))
            .Replace("İ", "I").Replace("Ş", "S").Replace("Ğ", "G").Replace("Ü", "U").Replace("Ö", "O").Replace("Ç", "C");
}
