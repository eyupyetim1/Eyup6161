using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class HesapEkstresiServisi : IHesapEkstresiServisi
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly IPayControlDbContext _db;

    public HesapEkstresiServisi(IPayControlDbContext db) => _db = db;

    public async Task<HesapEkstresiDto> GetirAsync(
        Guid personelId,
        HesapEkstresiFiltre? filtre = null,
        CancellationToken ct = default)
    {
        filtre ??= new HesapEkstresiFiltre();
        if (filtre.Baslangic.HasValue && filtre.Bitis.HasValue && filtre.Baslangic > filtre.Bitis)
            throw new InvalidOperationException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");

        var personel = await _db.Personeller.AsNoTracking()
            .Include(p => p.CalismaDonemleri)
            .FirstOrDefaultAsync(p => p.Id == personelId, ct)
            ?? throw new InvalidOperationException("Personel bulunamadı.");

        // Tüm hareketler bir kez okunur: genel bakiye ve yürüyen bakiyeler filtreden bağımsız doğru kalır.
        var tumu = (await _db.MaliHareketler.AsNoTracking()
                .Where(h => h.PersonelId == personelId)
                .ToListAsync(ct))
            .OrderBy(h => h.IslemTarihi).ThenBy(h => h.KayitZamani).ThenBy(h => h.IslemKimligi)
            .ToList();

        var satirlar = new List<MaliHareketDto>(tumu.Count);
        decimal maas = 0, avans = 0, supheli = 0, geri = 0;
        foreach (var h in tumu)
        {
            var etki = MaliHareketKurallari.Etki(h);
            if (MaliHareketKurallari.HakEdisMi(h)) maas += etki;
            else if (MaliHareketKurallari.OdemeMi(h)) maas -= etki;
            else if (h.BakiyeTuru == BakiyeTuru.AvansBorcu) avans += etki;
            else if (h.BakiyeTuru == BakiyeTuru.SupheliFazlaOdeme) supheli += etki;
            else if (h.BakiyeTuru == BakiyeTuru.KesinlesmisGeriOdeme) geri += etki;

            if (!Uyar(h, filtre)) continue;
            satirlar.Add(new MaliHareketDto
            {
                Id = h.Id,
                IslemKimligi = h.IslemKimligi,
                IslemTarihi = h.IslemTarihi,
                DonemYil = h.DonemYil,
                DonemAy = h.DonemAy,
                IslemTuru = h.IslemTuru,
                BakiyeTuru = h.BakiyeTuru,
                Tutar = h.Tutar,
                Etki = etki,
                Aciklama = h.Aciklama,
                KaynakTuru = h.KaynakTuru,
                KaynakBelge = h.KaynakBelge,
                KaynakReferans = h.KaynakReferans,
                KullaniciAdi = h.KullaniciAdi,
                KayitZamani = h.KayitZamani,
                CalismaDonemiId = h.CalismaDonemiId,
                Sirket = h.Sirket,
                Isyeri = h.Isyeri,
                TersKayitMi = h.TersKayitMi,
                MaasAlacagiBakiyesi = maas,
                AvansBorcuBakiyesi = avans,
                SupheliFazlaBakiyesi = supheli,
                GeriOdemeBakiyesi = geri
            });
        }

        var filtreli = tumu.Where(h => Uyar(h, filtre)).ToList();
        var donemler = personel.CalismaDonemleri.OrderBy(d => d.IseGirisTarihi).ToList();

        return new HesapEkstresiDto
        {
            PersonelId = personel.Id,
            AdSoyad = personel.AdSoyad,
            TcKimlikNo = personel.TcKimlikNo,
            IlkIseGirisTarihi = personel.IlkIseGirisTarihi,
            SonCikisTarihi = personel.SonCikisTarihi,
            CalismaDurumu = personel.CalismaDurumu,
            Ozet = MaliHareketKurallari.OzetHesapla(filtreli),
            GenelOzet = MaliHareketKurallari.OzetHesapla(tumu),
            Hareketler = satirlar,
            DonemBakiyeleri = MaliHareketKurallari.DonemBakiyeleri(filtreli),
            CalismaDonemleri = donemler.OrderByDescending(d => d.IseGirisTarihi).Select(Map).ToList(),
            CalismaDonemiOzetleri = donemler.Select(d => new CalismaDonemiOzetDto
            {
                Donem = Map(d),
                Ozet = MaliHareketKurallari.OzetHesapla(tumu.Where(h => h.CalismaDonemiId == d.Id))
            }).ToList(),
            Filtre = filtre,
            FiltreAciklamasi = FiltreMetni(filtre, donemler)
        };
    }

    private static bool Uyar(MaliHareket h, HesapEkstresiFiltre f) =>
        (f.Baslangic is null || h.IslemTarihi >= f.Baslangic) &&
        (f.Bitis is null || h.IslemTarihi <= f.Bitis) &&
        (f.IslemTuru is null || h.IslemTuru == f.IslemTuru) &&
        (f.BakiyeTuru is null || h.BakiyeTuru == f.BakiyeTuru) &&
        (f.CalismaDonemiId is null || h.CalismaDonemiId == f.CalismaDonemiId) &&
        (string.IsNullOrWhiteSpace(f.Sirket) || string.Equals(h.Sirket?.Trim(), f.Sirket.Trim(), StringComparison.CurrentCultureIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(f.Isyeri) || string.Equals(h.Isyeri?.Trim(), f.Isyeri.Trim(), StringComparison.CurrentCultureIgnoreCase));

    private static string FiltreMetni(HesapEkstresiFiltre f, IReadOnlyList<CalismaDonemi> donemler)
    {
        var parcalar = new List<string>();
        if (f.Baslangic.HasValue || f.Bitis.HasValue)
            parcalar.Add($"Tarih: {f.Baslangic?.ToString("dd.MM.yyyy") ?? "…"} – {f.Bitis?.ToString("dd.MM.yyyy") ?? "…"}");
        if (!string.IsNullOrWhiteSpace(f.Sirket)) parcalar.Add($"Şirket: {f.Sirket}");
        if (!string.IsNullOrWhiteSpace(f.Isyeri)) parcalar.Add($"Tesis: {f.Isyeri}");
        if (f.IslemTuru.HasValue) parcalar.Add($"İşlem: {MaliHareketKurallari.TurEtiketi(f.IslemTuru.Value)}");
        if (f.BakiyeTuru.HasValue) parcalar.Add($"Bakiye: {MaliHareketKurallari.BakiyeEtiketi(f.BakiyeTuru.Value)}");
        if (f.CalismaDonemiId.HasValue)
        {
            var d = donemler.FirstOrDefault(x => x.Id == f.CalismaDonemiId);
            parcalar.Add(d is null ? "Çalışma dönemi: ?" : $"Çalışma dönemi: {d.IseGirisTarihi:dd.MM.yyyy}–{d.IstenCikisTarihi?.ToString("dd.MM.yyyy") ?? "devam"}");
        }
        return parcalar.Count == 0 ? "Tüm hareketler" : string.Join(" | ", parcalar);
    }

    private static CalismaDonemiDto Map(CalismaDonemi d) => new()
    {
        Id = d.Id,
        IseGirisTarihi = d.IseGirisTarihi,
        IstenCikisTarihi = d.IstenCikisTarihi,
        Isyeri = d.Isyeri,
        Gorevi = d.Gorevi,
        Sirket = d.Sirket,
        Durum = d.Durum
    };

    public async Task<byte[]> ExcelOlarakAktarAsync(
        Guid personelId,
        HesapEkstresiFiltre? filtre = null,
        CancellationToken ct = default)
    {
        var e = await GetirAsync(personelId, filtre, ct);
        using var wb = new XLWorkbook();

        var ws = wb.AddWorksheet("Hesap Ekstresi");
        ws.Cell(1, 1).Value = "PayControl — Personel Hesap Ekstresi";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"{e.AdSoyad} — TC {e.TcKimlikNo}";
        ws.Cell(3, 1).Value = $"İlk giriş: {e.IlkIseGirisTarihi?.ToString("dd.MM.yyyy") ?? "-"} | Son çıkış: {e.SonCikisTarihi?.ToString("dd.MM.yyyy") ?? "-"} | Durum: {e.CalismaDurumu}";
        ws.Cell(4, 1).Value = $"Filtre: {e.FiltreAciklamasi}";
        ws.Cell(5, 1).Value = $"Rapor tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}";

        var ozetSatirlari = new (string, decimal, decimal)[]
        {
            ("Toplam hak ediş", e.Ozet.ToplamHakEdis, e.GenelOzet.ToplamHakEdis),
            ("Gerçekleşen toplam ödeme", e.Ozet.GerceklesenToplamOdeme, e.GenelOzet.GerceklesenToplamOdeme),
            ("Ödenmemiş hak ediş", e.Ozet.OdenmemisHakEdis, e.GenelOzet.OdenmemisHakEdis),
            ("Dönem fazla ödeme farkı", e.Ozet.DonemFazlaOdemeFarki, e.GenelOzet.DonemFazlaOdemeFarki),
            ("Açık avans borcu", e.Ozet.AcikAvansBorcu, e.GenelOzet.AcikAvansBorcu),
            ("İnceleme bekleyen fazla ödeme", e.Ozet.IncelemeBekleyenFazlaOdeme, e.GenelOzet.IncelemeBekleyenFazlaOdeme),
            ("Kesinleşmiş geri ödeme borcu", e.Ozet.KesinlesmisGeriOdemeBorcu, e.GenelOzet.KesinlesmisGeriOdemeBorcu)
        };
        ws.Cell(7, 1).Value = "Özet";
        ws.Cell(7, 2).Value = "Filtreli";
        ws.Cell(7, 3).Value = "Genel (tüm zamanlar)";
        ws.Range(7, 1, 7, 3).Style.Font.Bold = true;
        var r = 8;
        foreach (var (ad, f, g) in ozetSatirlari)
        {
            ws.Cell(r, 1).Value = ad;
            ws.Cell(r, 2).Value = f;
            ws.Cell(r, 3).Value = g;
            ws.Range(r, 2, r, 3).Style.NumberFormat.Format = "#,##0.00";
            r++;
        }

        r += 1;
        var basliklar = new[]
        {
            "Tarih", "Dönem", "İşlem türü", "Bakiye", "Tutar", "Etki", "Açıklama", "Kaynak", "Kaynak belge", "Kaynak referans",
            "Kullanıcı", "Şirket", "Tesis", "Maaş alacağı bak.", "Avans borcu bak.", "Şüpheli fazla bak.", "Geri ödeme bak.", "İşlem kimliği"
        };
        var baslikSatiri = r;
        for (var i = 0; i < basliklar.Length; i++)
            ws.Cell(r, i + 1).Value = basliklar[i];
        ws.Range(r, 1, r, basliklar.Length).Style.Font.Bold = true;
        ws.Range(r, 1, r, basliklar.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");
        r++;
        foreach (var h in e.Hareketler)
        {
            ws.Cell(r, 1).Value = h.IslemTarihi.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 1).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(r, 2).Value = h.DonemEtiket;
            ws.Cell(r, 3).Value = h.IslemTuruEtiket + (h.TersKayitMi ? " (ters kayıt)" : "");
            ws.Cell(r, 4).Value = h.BakiyeEtiket;
            ws.Cell(r, 5).Value = h.Tutar;
            ws.Cell(r, 6).Value = h.Etki;
            ws.Cell(r, 7).Value = h.Aciklama;
            ws.Cell(r, 8).Value = h.KaynakTuru;
            ws.Cell(r, 9).Value = h.KaynakBelge;
            ws.Cell(r, 10).Value = h.KaynakReferans;
            ws.Cell(r, 11).Value = h.KullaniciAdi;
            ws.Cell(r, 12).Value = h.Sirket;
            ws.Cell(r, 13).Value = h.Isyeri;
            ws.Cell(r, 14).Value = h.MaasAlacagiBakiyesi;
            ws.Cell(r, 15).Value = h.AvansBorcuBakiyesi;
            ws.Cell(r, 16).Value = h.SupheliFazlaBakiyesi;
            ws.Cell(r, 17).Value = h.GeriOdemeBakiyesi;
            ws.Cell(r, 18).Value = h.IslemKimligi;
            r++;
        }
        if (e.Hareketler.Count > 0)
        {
            ws.Range(baslikSatiri + 1, 5, r - 1, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(baslikSatiri + 1, 14, r - 1, 17).Style.NumberFormat.Format = "#,##0.00";
        }
        ws.Columns(1, 6).AdjustToContents();
        ws.Column(7).Width = 60;

        var wd = wb.AddWorksheet("Dönem Bakiyeleri");
        var dB = new[] { "Dönem", "Hak ediş", "Ödenen", "Fark (ödenen − hak ediş)", "Avans mahsubu", "Kesintiler", "Durum" };
        for (var i = 0; i < dB.Length; i++) wd.Cell(1, i + 1).Value = dB[i];
        wd.Range(1, 1, 1, dB.Length).Style.Font.Bold = true;
        r = 2;
        foreach (var d in e.DonemBakiyeleri)
        {
            wd.Cell(r, 1).Value = d.DonemEtiket;
            wd.Cell(r, 2).Value = d.HakEdis;
            wd.Cell(r, 3).Value = d.Odenen;
            wd.Cell(r, 4).Value = d.Fark;
            wd.Cell(r, 5).Value = d.AvansMahsup;
            wd.Cell(r, 6).Value = d.Kesintiler;
            wd.Cell(r, 7).Value = d.Durum;
            wd.Range(r, 2, r, 6).Style.NumberFormat.Format = "#,##0.00";
            r++;
        }
        wd.Columns().AdjustToContents();

        var wc = wb.AddWorksheet("Çalışma Dönemleri");
        var cB = new[] { "Giriş", "Çıkış", "Şirket", "Tesis", "Görev", "Hak ediş", "Ödeme", "Ödenmemiş", "Açık avans" };
        for (var i = 0; i < cB.Length; i++) wc.Cell(1, i + 1).Value = cB[i];
        wc.Range(1, 1, 1, cB.Length).Style.Font.Bold = true;
        r = 2;
        foreach (var c in e.CalismaDonemiOzetleri)
        {
            wc.Cell(r, 1).Value = c.Donem.IseGirisTarihi.ToString("dd.MM.yyyy");
            wc.Cell(r, 2).Value = c.Donem.IstenCikisTarihi?.ToString("dd.MM.yyyy") ?? "devam";
            wc.Cell(r, 3).Value = c.Donem.Sirket;
            wc.Cell(r, 4).Value = c.Donem.Isyeri;
            wc.Cell(r, 5).Value = c.Donem.Gorevi;
            wc.Cell(r, 6).Value = c.Ozet.ToplamHakEdis;
            wc.Cell(r, 7).Value = c.Ozet.GerceklesenToplamOdeme;
            wc.Cell(r, 8).Value = c.Ozet.OdenmemisHakEdis;
            wc.Cell(r, 9).Value = c.Ozet.AcikAvansBorcu;
            wc.Range(r, 6, r, 9).Style.NumberFormat.Format = "#,##0.00";
            r++;
        }
        wc.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> PdfOlarakAktarAsync(
        Guid personelId,
        HesapEkstresiFiltre? filtre = null,
        CancellationToken ct = default)
    {
        var e = await GetirAsync(personelId, filtre, ct);
        var pdf = new BasitPdfYazici();
        pdf.Baslik("PayControl — Personel Hesap Ekstresi");
        pdf.Satir($"{e.AdSoyad}   TC: {e.TcKimlikNo}   Durum: {e.CalismaDurumu}", 9.5f, true);
        pdf.Satir($"İlk işe giriş: {e.IlkIseGirisTarihi?.ToString("dd.MM.yyyy") ?? "-"}   Son çıkış: {e.SonCikisTarihi?.ToString("dd.MM.yyyy") ?? "-"}   Rapor: {DateTime.Now:dd.MM.yyyy HH:mm}");
        pdf.Satir($"Filtre: {e.FiltreAciklamasi}");
        pdf.Bosluk();
        pdf.Satir("Bakiyeler (filtreli / genel) — maaş alacağı, avans borcu ve fazla ödeme ayrı bakiyelerdir:", 8.5f, true);
        pdf.Satir($"Toplam hak ediş: {T(e.Ozet.ToplamHakEdis)} / {T(e.GenelOzet.ToplamHakEdis)}    Gerçekleşen ödeme: {T(e.Ozet.GerceklesenToplamOdeme)} / {T(e.GenelOzet.GerceklesenToplamOdeme)}    Ödenmemiş hak ediş: {T(e.Ozet.OdenmemisHakEdis)} / {T(e.GenelOzet.OdenmemisHakEdis)}");
        pdf.Satir($"Açık avans borcu: {T(e.Ozet.AcikAvansBorcu)} / {T(e.GenelOzet.AcikAvansBorcu)}    İnceleme bekleyen fazla ödeme: {T(e.Ozet.IncelemeBekleyenFazlaOdeme)} / {T(e.GenelOzet.IncelemeBekleyenFazlaOdeme)}    Kesinleşmiş geri ödeme: {T(e.Ozet.KesinlesmisGeriOdemeBorcu)} / {T(e.GenelOzet.KesinlesmisGeriOdemeBorcu)}");
        pdf.Bosluk();

        if (e.CalismaDonemiOzetleri.Count > 0)
        {
            pdf.Satir("Çalışma dönemleri", 8.5f, true);
            pdf.Tablo(
            [
                ("Giriş", 12, false), ("Çıkış", 12, false), ("Şirket / Tesis", 40, false),
                ("Hak ediş", 16, true), ("Ödeme", 16, true), ("Ödenmemiş", 16, true), ("Açık avans", 16, true)
            ], e.CalismaDonemiOzetleri.Select(c => new[]
            {
                c.Donem.IseGirisTarihi.ToString("dd.MM.yyyy"), c.Donem.IstenCikisTarihi?.ToString("dd.MM.yyyy") ?? "devam",
                $"{c.Donem.Sirket} / {c.Donem.Isyeri}", T(c.Ozet.ToplamHakEdis), T(c.Ozet.GerceklesenToplamOdeme),
                T(c.Ozet.OdenmemisHakEdis), T(c.Ozet.AcikAvansBorcu)
            }));
            pdf.Bosluk();
        }

        pdf.Satir("Mali hareketler (kronolojik)", 8.5f, true);
        pdf.Tablo(
        [
            ("Tarih", 11, false), ("Dönem", 8, false), ("İşlem", 22, false), ("Bakiye", 15, false), ("Tutar", 14, true),
            ("Açıklama", 62, false), ("Belge/Ref.", 18, false), ("Kullanıcı", 10, false),
            ("Maaş al.", 13, true), ("Avans", 12, true), ("Şüpheli", 12, true)
        ], e.Hareketler.Select(h => new[]
        {
            h.IslemTarihi.ToString("dd.MM.yyyy"), h.DonemEtiket, h.IslemTuruEtiket + (h.TersKayitMi ? " (ters)" : ""),
            h.BakiyeEtiket, T(h.Etki), h.Aciklama, h.KaynakBelge ?? h.KaynakReferans ?? "", h.KullaniciAdi ?? "",
            T(h.MaasAlacagiBakiyesi), T(h.AvansBorcuBakiyesi), T(h.SupheliFazlaBakiyesi)
        }));
        pdf.Bosluk();
        pdf.Satir($"Toplam {e.Hareketler.Count} hareket. Her hareketin benzersiz işlem kimliği Excel çıktısında yer alır.");
        return pdf.Olustur();
    }

    private static string T(decimal d) => d.ToString("N2", Tr);
}
