using ClosedXML.Excel;

var outDir = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "samples"));
Directory.CreateDirectory(outDir);
var path = Path.Combine(outDir, "bordro-ornek.xlsx");

using var wb = new XLWorkbook();
var ws = wb.AddWorksheet("Bordro");
var headers = new[]
{
    "T.C. KİMLİK NO", "ADI SOYADI", "İŞYERİ", "GÖREVİ", "MAAŞ (Net/Brüt)",
    "GİRİŞ TARİHİ", "ÇIKIŞ TARİHİ", "GÜN", "NET İSTİHKAK", "IBAN NO",
    "AVANS", "İCRA", "OTO.KAT.BES", "TRAFİK CEZASI", "AİLE", "İHBAR TAZMİNATI", "YEMEK", "YOL"
};
for (var i = 0; i < headers.Length; i++)
{
    var cell = ws.Cell(1, i + 1);
    cell.Value = headers[i];
    cell.Style.Fill.BackgroundColor = XLColor.LightGray;
    cell.Style.Font.Bold = true;
}

// Sentetik veriler — gerçek kişisel veri yok
object?[][] rows =
[
    ["10000000146", "Ahmet Yılmaz", "Tesis İstanbul", "Operatör", 30000d, new DateTime(2024, 1, 15), null, 30d, 24500d, "TR330006100519786457841326", 5000d, 0d, 500d, 0d, 1000d, 0d, 1500d, 800d],
    ["10000000154", "Ayşe Demir", "Tesis Ankara", "Teknisyen", 28000d, new DateTime(2023, 6, 1), null, 30d, 27000d, "TR320010009999999999999991", 0d, 1000d, 400d, 0d, 0d, 0d, 1200d, 600d],
    ["10000000162", "Mehmet Kaya", "Tesis İstanbul", "Formen", 35000d, new DateTime(2020, 3, 10), new DateTime(2026, 10, 15), 15d, 15000d, "TR640006200000000000000001", 0d, 0d, 250d, 0d, 0d, 5000d, 750d, 400d],
];

for (var r = 0; r < rows.Length; r++)
{
    for (var c = 0; c < rows[r].Length; c++)
    {
        var v = rows[r][c];
        if (v is null) continue;
        ws.Cell(r + 2, c + 1).Value = v switch
        {
            DateTime dt => dt,
            double d => d,
            _ => v.ToString()
        };
    }
}

wb.SaveAs(path);
Console.WriteLine($"Örnek bordro yazıldı: {path}");
