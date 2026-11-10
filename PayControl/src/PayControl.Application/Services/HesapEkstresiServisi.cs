using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class HesapEkstresiServisi : IHesapEkstresiServisi
{
    private readonly IPayControlDbContext _db;

    public HesapEkstresiServisi(IPayControlDbContext db) => _db = db;

    public async Task<HesapEkstresiDto> GetirAsync(
        Guid personelId,
        HesapEkstresiFiltre? filtre = null,
        CancellationToken ct = default)
    {
        var personel = await _db.Personeller.AsNoTracking()
            .Include(p => p.CalismaDonemleri)
            .FirstOrDefaultAsync(p => p.Id == personelId, ct)
            ?? throw new InvalidOperationException("Personel bulunamadı.");

        var q = _db.MaliHareketler.AsNoTracking().Where(h => h.PersonelId == personelId);

        if (filtre?.Baslangic is not null)
            q = q.Where(h => h.IslemTarihi >= filtre.Baslangic);
        if (filtre?.Bitis is not null)
            q = q.Where(h => h.IslemTarihi <= filtre.Bitis);
        if (filtre?.IslemTuru is not null)
            q = q.Where(h => h.IslemTuru == filtre.IslemTuru);
        if (filtre?.CalismaDonemiId is not null)
            q = q.Where(h => h.CalismaDonemiId == filtre.CalismaDonemiId);

        var hareketler = await q.OrderBy(h => h.IslemTarihi).ThenBy(h => h.KayitZamani).ToListAsync(ct);
        var ozet = MaliHareketKurallari.OzetHesapla(hareketler);

        return new HesapEkstresiDto
        {
            PersonelId = personel.Id,
            AdSoyad = personel.AdSoyad,
            TcKimlikNo = personel.TcKimlikNo,
            Ozet = ozet,
            Hareketler = hareketler.Select(h => new MaliHareketDto
            {
                Id = h.Id,
                IslemKimligi = h.IslemKimligi,
                IslemTarihi = h.IslemTarihi,
                DonemYil = h.DonemYil,
                DonemAy = h.DonemAy,
                IslemTuru = h.IslemTuru,
                BakiyeTuru = h.BakiyeTuru,
                Tutar = h.Tutar,
                Aciklama = h.Aciklama,
                KaynakBelge = h.KaynakBelge,
                KaynakReferans = h.KaynakReferans,
                KullaniciAdi = h.KullaniciAdi,
                CalismaDonemiId = h.CalismaDonemiId
            }).ToList(),
            CalismaDonemleri = personel.CalismaDonemleri
                .OrderByDescending(d => d.IseGirisTarihi)
                .Select(d => new CalismaDonemiDto
                {
                    Id = d.Id,
                    IseGirisTarihi = d.IseGirisTarihi,
                    IstenCikisTarihi = d.IstenCikisTarihi,
                    Isyeri = d.Isyeri,
                    Gorevi = d.Gorevi,
                    Sirket = d.Sirket,
                    Durum = d.Durum
                }).ToList()
        };
    }

    public async Task<byte[]> ExcelOlarakAktarAsync(
        Guid personelId,
        HesapEkstresiFiltre? filtre = null,
        CancellationToken ct = default)
    {
        var ekstre = await GetirAsync(personelId, filtre, ct);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Hesap Ekstresi");
        ws.Cell(1, 1).Value = "PayControl Personel Hesap Ekstresi";
        ws.Cell(2, 1).Value = ekstre.AdSoyad;
        ws.Cell(2, 2).Value = ekstre.TcKimlikNo;
        ws.Cell(3, 1).Value = "Toplam hak ediş";
        ws.Cell(3, 2).Value = ekstre.Ozet.ToplamHakEdis;
        ws.Cell(4, 1).Value = "Gerçekleşen ödeme";
        ws.Cell(4, 2).Value = ekstre.Ozet.GerceklesenToplamOdeme;
        ws.Cell(5, 1).Value = "Ödenmemiş hak ediş";
        ws.Cell(5, 2).Value = ekstre.Ozet.OdenmemisHakEdis;
        ws.Cell(6, 1).Value = "Açık avans";
        ws.Cell(6, 2).Value = ekstre.Ozet.AcikAvansBorcu;
        ws.Cell(7, 1).Value = "İnceleme bekleyen fazla";
        ws.Cell(7, 2).Value = ekstre.Ozet.IncelemeBekleyenFazlaOdeme;
        ws.Cell(8, 1).Value = "Kesinleşmiş geri ödeme";
        ws.Cell(8, 2).Value = ekstre.Ozet.KesinlesmisGeriOdemeBorcu;

        var headers = new[] { "Tarih", "Dönem", "Tür", "Bakiye", "Tutar", "Açıklama", "Belge", "Referans", "Kullanıcı", "İşlem Kimliği" };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(10, i + 1).Value = headers[i];

        var row = 11;
        foreach (var h in ekstre.Hareketler)
        {
            ws.Cell(row, 1).Value = h.IslemTarihi.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR"));
            ws.Cell(row, 2).Value = h.DonemEtiket;
            ws.Cell(row, 3).Value = h.IslemTuru.ToString();
            ws.Cell(row, 4).Value = h.BakiyeTuru.ToString();
            ws.Cell(row, 5).Value = h.Tutar;
            ws.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(row, 6).Value = h.Aciklama;
            ws.Cell(row, 7).Value = h.KaynakBelge;
            ws.Cell(row, 8).Value = h.KaynakReferans;
            ws.Cell(row, 9).Value = h.KullaniciAdi;
            ws.Cell(row, 10).Value = h.IslemKimligi;
            row++;
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> PdfOlarakAktarAsync(
        Guid personelId,
        HesapEkstresiFiltre? filtre = null,
        CancellationToken ct = default)
    {
        // Bağımsız PDF kütüphanesi olmadan metin tabanlı basit PDF (PDF 1.4) üretilir.
        var ekstre = await GetirAsync(personelId, filtre, ct);
        var sb = new StringBuilder();
        sb.AppendLine($"PayControl Hesap Ekstresi — {ekstre.AdSoyad} ({ekstre.TcKimlikNo})");
        sb.AppendLine($"Hak ediş: {ekstre.Ozet.ToplamHakEdis:N2} | Ödeme: {ekstre.Ozet.GerceklesenToplamOdeme:N2} | Açık avans: {ekstre.Ozet.AcikAvansBorcu:N2}");
        sb.AppendLine(new string('-', 80));
        foreach (var h in ekstre.Hareketler)
        {
            sb.AppendLine($"{h.IslemTarihi:dd.MM.yyyy} | {h.DonemEtiket} | {h.IslemTuru} | {h.Tutar:N2} | {h.Aciklama}");
        }

        return BuildSimplePdf(sb.ToString());
    }

    /// <summary>Minimal Latin-1 PDF; Türkçe karakterler ASCII'ye yaklaştırılır.</summary>
    private static byte[] BuildSimplePdf(string text)
    {
        var normalized = text
            .Replace("İ", "I").Replace("ı", "i").Replace("Ş", "S").Replace("ş", "s")
            .Replace("Ğ", "G").Replace("ğ", "g").Replace("Ü", "U").Replace("ü", "u")
            .Replace("Ö", "O").Replace("ö", "o").Replace("Ç", "C").Replace("ç", "c");

        var lines = normalized.Replace("\r", "").Split('\n');
        var content = new StringBuilder();
        content.AppendLine("BT /F1 9 Tf 40 800 Td");
        var first = true;
        foreach (var line in lines.Take(80))
        {
            var safe = line.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
            if (!first) content.AppendLine("0 -12 Td");
            content.AppendLine($"({safe}) Tj");
            first = false;
        }
        content.AppendLine("ET");
        var stream = content.ToString();
        var streamBytes = Encoding.ASCII.GetBytes(stream);

        var objs = new List<byte[]>();
        objs.Add(Encoding.ASCII.GetBytes("1 0 obj<< /Type /Catalog /Pages 2 0 R >>endobj\n"));
        objs.Add(Encoding.ASCII.GetBytes("2 0 obj<< /Type /Pages /Kids [3 0 R] /Count 1 >>endobj\n"));
        objs.Add(Encoding.ASCII.GetBytes("3 0 obj<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Contents 4 0 R /Resources<< /Font<< /F1 5 0 R >> >> >>endobj\n"));
        var s4 = new byte[streamBytes.Length + 64];
        var header4 = Encoding.ASCII.GetBytes($"4 0 obj<< /Length {streamBytes.Length} >>stream\n");
        var footer4 = Encoding.ASCII.GetBytes("\nendstream\nendobj\n");
        objs.Add(header4.Concat(streamBytes).Concat(footer4).ToArray());
        objs.Add(Encoding.ASCII.GetBytes("5 0 obj<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>endobj\n"));

        using var ms = new MemoryStream();
        var header = Encoding.ASCII.GetBytes("%PDF-1.4\n");
        ms.Write(header);
        var offsets = new List<long> { 0 };
        foreach (var o in objs)
        {
            offsets.Add(ms.Position);
            ms.Write(o);
        }
        var xrefPos = ms.Position;
        var xref = new StringBuilder();
        xref.AppendLine("xref");
        xref.AppendLine($"0 {objs.Count + 1}");
        xref.AppendLine("0000000000 65535 f ");
        for (var i = 1; i < offsets.Count; i++)
            xref.AppendLine($"{offsets[i]:D10} 00000 n ");
        xref.AppendLine($"trailer<< /Size {objs.Count + 1} /Root 1 0 R >>");
        xref.AppendLine("startxref");
        xref.AppendLine(xrefPos.ToString());
        xref.AppendLine("%%EOF");
        var xrefBytes = Encoding.ASCII.GetBytes(xref.ToString());
        ms.Write(xrefBytes);
        return ms.ToArray();
    }
}
