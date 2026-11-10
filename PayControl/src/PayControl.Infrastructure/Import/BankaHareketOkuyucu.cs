using System.Globalization;
using ClosedXML.Excel;
using PayControl.Application.Interfaces;

namespace PayControl.Infrastructure.Import;

/// <summary>
/// Bankadan alınan gerçekleşen ödeme dökümünü (Excel) okur.
/// Gerekli kolonlar: tarih, tutar ve TC veya IBAN. Referans/dekont no varsa mükerrer kontrolünde esas alınır.
/// </summary>
public static class BankaHareketOkuyucu
{
    public static IReadOnlyList<BankaHareketSatiri> Oku(Stream stream)
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
            list.Add(new BankaHareketSatiri(r, tarih, Get("TC"), Get("AD"), Get("IBAN"), Math.Abs(tutar),
                string.IsNullOrWhiteSpace(Get("REF")) ? null : Get("REF"), Get("ACIKLAMA")));
        }
        return list;
    }

    private static string N(string v) =>
        (v ?? "").Trim().ToUpper(CultureInfo.GetCultureInfo("tr-TR"))
            .Replace("İ", "I").Replace("Ş", "S").Replace("Ğ", "G").Replace("Ü", "U").Replace("Ö", "O").Replace("Ç", "C");
}
