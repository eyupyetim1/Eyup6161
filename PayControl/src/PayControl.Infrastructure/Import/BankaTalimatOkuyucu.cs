using System.Globalization;
using ClosedXML.Excel;
using PayControl.Application.Dtos;
using PayControl.Domain.Services;

namespace PayControl.Infrastructure.Import;

/// <summary>
/// Bankaya gönderilecek ödeme Excel dosyasını okur (orijinal dosyayı değiştirmez) ve
/// formatını şablon olarak çıkarır.
/// </summary>
public static class BankaTalimatOkuyucu
{
    public static IReadOnlyList<BankaTalimatSatiri> Oku(Stream stream) => OkuDosya(stream).Satirlar;

    public static BankaTalimatDosyasi OkuDosya(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var used = ws.RangeUsed() ?? throw new InvalidOperationException("Banka dosyası boş.");
        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();

        // Başlık satırı: IBAN ve tutar kolonlarını içeren ilk satır (üstte banka bilgi satırları olabilir).
        var headerRow = -1;
        Dictionary<string, int> map = new();
        for (var r = firstRow; r <= Math.Min(lastRow, firstRow + 15) && headerRow < 0; r++)
        {
            var aday = KolonEsle(ws, r, firstCol, lastCol);
            if (aday.ContainsKey("TUTAR") && (aday.ContainsKey("IBAN") || aday.ContainsKey("TC")))
            {
                headerRow = r;
                map = aday;
            }
        }
        if (headerRow < 0)
            throw new InvalidOperationException("Banka dosyasında başlık satırı bulunamadı (IBAN/TC ve TUTAR kolonları gerekli).");

        var sablon = new BankaDosyaSablonu { SayfaAdi = ws.Name, KolonRolleri = map };
        for (var r = firstRow; r < headerRow; r++)
        {
            var d = new Dictionary<int, string>();
            for (var c = firstCol; c <= lastCol; c++)
                if (!ws.Cell(r, c).IsEmpty()) d[c] = ws.Cell(r, c).GetString();
            sablon.UstBilgiSatirlari.Add(d);
        }
        for (var c = firstCol; c <= lastCol; c++)
            sablon.Basliklar[c] = ws.Cell(headerRow, c).GetString().Trim();

        var list = new List<BankaTalimatSatiri>();
        var ham = new Dictionary<int, Dictionary<int, string>>();
        var uyarilar = new List<string>();
        var tutarCol = map["TUTAR"];
        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var tutarCell = ws.Cell(r, tutarCol);
            if (tutarCell.IsEmpty() || string.IsNullOrWhiteSpace(tutarCell.GetString()))
            {
                if (SatirDoluMu(ws, r, firstCol, lastCol))
                    uyarilar.Add($"Satır {r}: tutar boş — satır kontrol dışı bırakıldı, dosyayı kontrol edin.");
                continue;
            }

            decimal tutar;
            if (tutarCell.DataType == XLDataType.Number)
            {
                tutar = Math.Round((decimal)tutarCell.GetDouble(), 2);
                sablon.TutarBicimi ??= tutarCell.Style.NumberFormat.Format;
            }
            else if (!decimal.TryParse(tutarCell.GetString().Replace("TL", "").Replace("₺", "").Trim(),
                         NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out tutar) &&
                     !decimal.TryParse(tutarCell.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out tutar))
            {
                if (r == lastRow && ws.Cell(r, firstCol).GetString().Contains("TOPLAM", StringComparison.OrdinalIgnoreCase)) continue;
                uyarilar.Add($"Satır {r}: tutar okunamadı ('{tutarCell.GetString()}') — satır kontrol dışı bırakıldı.");
                continue;
            }

            // Alt toplam satırını talimat sanma
            var ilkHucre = ws.Cell(r, firstCol).GetString();
            if (Normalize(ilkHucre).StartsWith("TOPLAM") && (!map.ContainsKey("TC") || string.IsNullOrWhiteSpace(Digits(ws.Cell(r, map["TC"]).GetString()))))
                continue;

            var d = new Dictionary<int, string>();
            for (var c = firstCol; c <= lastCol; c++)
                if (!ws.Cell(r, c).IsEmpty()) d[c] = ws.Cell(r, c).GetString();
            ham[r] = d;

            list.Add(new BankaTalimatSatiri(
                map.TryGetValue("TC", out var tcCol) ? Digits(ws.Cell(r, tcCol).GetString()) : null,
                map.TryGetValue("AD", out var adCol) ? ws.Cell(r, adCol).GetString().Trim() : null,
                map.TryGetValue("IBAN", out var ibanCol) ? IbanKurallari.Normalize(ws.Cell(r, ibanCol).GetString()) : null,
                tutar,
                r));
        }

        return new BankaTalimatDosyasi { Sablon = sablon, Satirlar = list, HamHucreler = ham, Uyarilar = uyarilar };
    }

    private static Dictionary<string, int> KolonEsle(IXLWorksheet ws, int r, int firstCol, int lastCol)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var c = firstCol; c <= lastCol; c++)
        {
            var h = Normalize(ws.Cell(r, c).GetString());
            if (h.Length == 0) continue;
            if ((h.Contains("KIMLIK") || h == "TC" || h == "TCKN" || h.StartsWith("T.C")) && !map.ContainsKey("TC")) map["TC"] = c;
            else if (h.Contains("IBAN") && !map.ContainsKey("IBAN")) map["IBAN"] = c;
            else if ((h.Contains("TUTAR") || h.Contains("ODENECEK") || h == "NET" || h.Contains("NET ODEME") || h.Contains("MIKTAR")) && !map.ContainsKey("TUTAR")) map["TUTAR"] = c;
            else if ((h.Contains("ADI") || h.Contains("SOYAD") || h.Contains("UNVAN") || h.Contains("ALICI")) && !map.ContainsKey("AD")) map["AD"] = c;
            else if ((h.Contains("ACIKLAMA") || h.Contains("AÇIKLAMA")) && !map.ContainsKey("ACIKLAMA")) map["ACIKLAMA"] = c;
        }
        return map;
    }

    private static bool SatirDoluMu(IXLWorksheet ws, int r, int c1, int c2)
    {
        for (var c = c1; c <= c2; c++)
            if (!string.IsNullOrWhiteSpace(ws.Cell(r, c).GetString())) return true;
        return false;
    }

    private static string Normalize(string v) =>
        (v ?? "").Trim().ToUpper(CultureInfo.GetCultureInfo("tr-TR"))
            .Replace("İ", "I").Replace("Ş", "S").Replace("Ğ", "G")
            .Replace("Ü", "U").Replace("Ö", "O").Replace("Ç", "C");

    private static string Digits(string v) => new((v ?? "").Where(char.IsDigit).ToArray());
}
