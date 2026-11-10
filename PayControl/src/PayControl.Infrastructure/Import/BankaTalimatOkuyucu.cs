using System.Globalization;
using ClosedXML.Excel;
using PayControl.Domain.Services;

namespace PayControl.Infrastructure.Import;

/// <summary>
/// Bankaya gönderilecek ödeme Excel dosyasını okur (orijinal dosyayı değiştirmez).
/// </summary>
public static class BankaTalimatOkuyucu
{
    public static IReadOnlyList<BankaTalimatSatiri> Oku(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var used = ws.RangeUsed() ?? throw new InvalidOperationException("Banka dosyası boş.");
        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();

        var headerRow = firstRow;
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = firstCol; c <= lastCol; c++)
        {
            var h = Normalize(ws.Cell(headerRow, c).GetString());
            if (h.Contains("KIMLIK") || h == "TC") map["TC"] = c;
            else if (h.Contains("ADI") || h.Contains("SOYAD") || h.Contains("UNVAN")) map["AD"] = c;
            else if (h.Contains("IBAN")) map["IBAN"] = c;
            else if (h.Contains("TUTAR") || h.Contains("ODENECEK") || h.Contains("NET")) map["TUTAR"] = c;
        }

        if (!map.ContainsKey("TUTAR"))
            throw new InvalidOperationException("Banka dosyasında tutar sütunu bulunamadı.");

        var list = new List<BankaTalimatSatiri>();
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var tutarHam = ws.Cell(r, map["TUTAR"]).GetString();
            if (string.IsNullOrWhiteSpace(tutarHam) && ws.Cell(r, map["TUTAR"]).IsEmpty())
                continue;

            decimal tutar;
            if (ws.Cell(r, map["TUTAR"]).DataType == XLDataType.Number)
                tutar = (decimal)ws.Cell(r, map["TUTAR"]).GetDouble();
            else if (!decimal.TryParse(tutarHam, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out tutar) &&
                     !decimal.TryParse(tutarHam, NumberStyles.Any, CultureInfo.InvariantCulture, out tutar))
                continue;

            list.Add(new BankaTalimatSatiri(
                map.TryGetValue("TC", out var tcCol) ? Digits(ws.Cell(r, tcCol).GetString()) : null,
                map.TryGetValue("AD", out var adCol) ? ws.Cell(r, adCol).GetString().Trim() : null,
                map.TryGetValue("IBAN", out var ibanCol)
                    ? new string(ws.Cell(r, ibanCol).GetString().Where(ch => !char.IsWhiteSpace(ch)).ToArray()).ToUpperInvariant()
                    : null,
                tutar,
                r));
        }

        return list;
    }

    private static string Normalize(string v) =>
        (v ?? "").Trim().ToUpperInvariant()
            .Replace("İ", "I").Replace("Ş", "S").Replace("Ğ", "G")
            .Replace("Ü", "U").Replace("Ö", "O").Replace("Ç", "C");

    private static string Digits(string v) => new string((v ?? "").Where(char.IsDigit).ToArray());
}
