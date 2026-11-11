using System.Globalization;

namespace PayControl.Domain.Services;

public sealed record PuantajKodu(string Kod, string Ad, string Renk, string YaziRengi);

/// <summary>Bir personelin bir aylık puantaj özeti (Access AylikOzet alanlarıyla birebir).</summary>
public sealed record AylikPuantajOzeti(
    decimal HaftasonuHakki, decimal KullanilanHaftalikIzin, decimal Calisilan, decimal YillikIzin, decimal Raporlu,
    decimal ResmiTatil, decimal UcretsizIzin, decimal HT, decimal AylikToplam, decimal IdariIzin, decimal DevredenIzin,
    decimal Gorev, decimal GorevIzni, decimal Mazeret, decimal HasatIzni, decimal YarimGun);

/// <summary>
/// Personel Yönetim Sistemi (VBA) puantaj kuralları — mdlHesaplamaVeIzin.CountPuantajStatuses, RaporCezaHaftaSayisi
/// ve mdlBakiye hafta tatili hakkı kuralının karşılığı. Gerçek Access verisindeki aylık özetlerle doğrulanmıştır.
/// </summary>
public static class PuantajHesaplayici
{
    /// <summary>Excel "Ayarlar → İzinler" listesindeki kodlar ve renkleri.</summary>
    public static readonly IReadOnlyList<PuantajKodu> Kodlar =
    [
        new("Ç", "Çalışıyor", "#DCFCE7", "#14532D"),
        new("HT", "Hafta sonu tatili", "#E0E7FF", "#312E81"),
        new("Yi", "Yıllık izin", "#FEF3C7", "#78350F"),
        new("RT", "Resmi tatil", "#FCE7F3", "#831843"),
        new("R", "Raporlu", "#FEE2E2", "#7F1D1D"),
        new("Üİ", "Ücretsiz izin", "#F3F4F6", "#374151"),
        new("M", "Mazeret izni", "#E0F2FE", "#0C4A6E"),
        new("YG", "Yarım gün izinli", "#FFEDD5", "#7C2D12"),
        new("G", "Görevde", "#CCFBF1", "#134E4A"),
        new("Gİ", "Görev izni", "#D1FAE5", "#065F46"),
        new("Hİ", "Hasat izni", "#ECFCCB", "#365314"),
        new("İİ", "İdari izin", "#EDE9FE", "#4C1D95"),
    ];

    /// <summary>VBA NormalizeStatusCode: büyük/küçük harf ve Türkçe harf farkını yok sayar ("ç", "Ç", "C" → "C"; "Üİ" → "UI").</summary>
    public static string Normalize(string? kod)
    {
        if (string.IsNullOrWhiteSpace(kod)) return string.Empty;
        var u = kod.Trim().ToUpper(CultureInfo.GetCultureInfo("tr-TR"));
        return u.Replace('Ç', 'C').Replace('İ', 'I').Replace('Ü', 'U').Replace('Ş', 'S').Replace('Ğ', 'G').Replace('Ö', 'O');
    }

    /// <summary>Kodu ekranda kullanılan standart yazımına çevirir ("yi" → "Yi"); tanınmayan kod null.</summary>
    public static string? Standart(string? kod)
    {
        var n = Normalize(kod);
        return n.Length == 0 ? null : Kodlar.FirstOrDefault(k => Normalize(k.Kod) == n)?.Kod;
    }

    /// <summary>VBA RaporCezaHaftaSayisi: her tam 7 rapor günü 1 hak düşürür; artan 6 gün de 1 sayılır.</summary>
    public static int RaporCezasi(decimal raporGunu)
    {
        var tam = (int)Math.Floor(raporGunu / 7m);
        var kalan = raporGunu - tam * 7;
        return tam + (kalan >= 6 ? 1 : 0);
    }

    /// <summary>Haftanın günü (1=Pzt … 7=Paz).</summary>
    public static int HaftaGunu(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    /// <summary>
    /// Aylık özet. <paramref name="haftaTatiliGunleri"/>: hafta tatili sayılan günler (varsayılan yalnız pazar).
    /// Kurallar:
    ///  • Çalışılan = Ç + G + 0,5 × YG; Kullanılan haftalık izin = HT + 0,5 × YG
    ///  • Aylık toplam = kodlu gün sayısı − İİ − YG
    ///  • Hafta sonu hakkı = aydaki hafta tatili günleri (o haftada 2+ Üİ varsa o haftanın hakkı yanar) − rapor cezası (hak kadar)
    ///  • Devreden = hak − kullanılan; o ay hiç çalışma ve ücretsiz izin yoksa 0
    /// </summary>
    public static AylikPuantajOzeti Hesapla(int yil, int ay, IReadOnlyDictionary<int, string> gunKodlari, IReadOnlyCollection<int>? haftaTatiliGunleri = null)
    {
        var tatil = haftaTatiliGunleri is { Count: > 0 } ? haftaTatiliGunleri : [7];
        decimal c = 0, yi = 0, r = 0, rt = 0, ui = 0, ht = 0, m = 0, yg = 0, ii = 0, g = 0, gi = 0, hi = 0;
        var kodlu = 0;
        var gunSayisi = DateTime.DaysInMonth(yil, ay);
        var uiHaftalari = new Dictionary<int, int>(); // anahtar: haftanın pazar gününün gün numarası (DayNumber)
        foreach (var (gun, ham) in gunKodlari)
        {
            if (gun < 1 || gun > gunSayisi) continue;
            var k = Normalize(ham);
            if (k.Length == 0) continue;
            kodlu++;
            switch (k)
            {
                case "C": c++; break;
                case "YI": yi++; break;
                case "R": r++; break;
                case "RT": rt++; break;
                case "UI":
                    ui++;
                    var d = new DateOnly(yil, ay, gun);
                    var anahtar = d.DayNumber + (7 - HaftaGunu(d));
                    uiHaftalari[anahtar] = uiHaftalari.GetValueOrDefault(anahtar) + 1;
                    break;
                case "HT": ht++; break;
                case "M": m++; break;
                case "YG": yg++; break;
                case "II": ii++; break;
                case "G": g++; break;
                case "GI": gi++; break;
                case "HI": hi++; break;
            }
        }

        var hak = 0;
        for (var gun = 1; gun <= gunSayisi; gun++)
        {
            var d = new DateOnly(yil, ay, gun);
            var hg = HaftaGunu(d);
            if (!tatil.Contains(hg)) continue;
            if (uiHaftalari.GetValueOrDefault(d.DayNumber + (7 - hg)) < 2) hak++;
        }
        var ceza = Math.Min(RaporCezasi(r), hak);
        decimal haftasonuHakki = hak - ceza;

        var calisilan = c + g + 0.5m * yg;
        var kullanilan = ht + 0.5m * yg;
        var devreden = calisilan == 0 && ui == 0 ? 0 : haftasonuHakki - kullanilan;
        return new AylikPuantajOzeti(haftasonuHakki, kullanilan, calisilan, yi, r, rt, ui, ht, kodlu - ii - yg, ii, devreden, g, gi, m, hi, yg);
    }

    /// <summary>VBA'daki tablo adı dönüşümü: Türkçe harfler sadeleşir, harf/rakam dışı '_' olur ("Giresun (Keşap) Limanı" → "Giresun__Kesap__Limani").</summary>
    public static string TabloAdi(string tesis)
    {
        var s = tesis.Replace('ı', 'i').Replace('İ', 'I').Replace('ş', 's').Replace('Ş', 'S').Replace('ğ', 'g').Replace('Ğ', 'G')
            .Replace('ü', 'u').Replace('Ü', 'U').Replace('ö', 'o').Replace('Ö', 'O').Replace('ç', 'c').Replace('Ç', 'C');
        return new string(s.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
    }
}
