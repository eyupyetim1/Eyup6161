namespace PayControl.Domain.Services;

/// <summary>Personel belgesi durum kodu (VBA BG_DurumHesapla).</summary>
public enum BelgeDurumKodu
{
    GerekmiyorVeyaIstegeBagli = 0,
    Tamam = 1,
    YakindaDoluyor = 2,
    SuresiDoldu = 3,
    Eksik = 4,
    Bekliyor = 5
}

/// <summary>VBA mdlBelge kuralları: koşul (Herkes / Erkek / Evli / Varsa), geçerlilik süresi (tehlike sınıfına göre), durum.</summary>
public static class BelgeKurallari
{
    public const int YaklasanGun = 30;
    public static readonly string[] TehlikeSiniflari = ["Çok tehlikeli", "Tehlikeli", "Az tehlikeli"];
    public static readonly string[] Kosullar = ["HERKES", "ERKEK", "EVLI", "VARSA"];
    public static readonly string[] Sureler = ["", "SAGLIK", "ISG", "AY:6", "AY:12", "AY:24", "AY:36", "AY:60"];

    public static string KosulMetni(string? kosul) => (kosul ?? "").ToUpperInvariant() switch
    {
        "ERKEK" => "Erkek personel",
        "EVLI" => "Evli personel",
        "VARSA" => "Varsa (isteğe bağlı)",
        _ => "Herkes"
    };

    public static bool KosulTutar(string? kosul, string? cinsiyet, string? medeni)
    {
        cinsiyet ??= ""; medeni ??= "";
        return (kosul ?? "").ToUpperInvariant() switch
        {
            "ERKEK" => cinsiyet.Contains("erkek", StringComparison.OrdinalIgnoreCase) || cinsiyet.Trim().StartsWith("E", StringComparison.OrdinalIgnoreCase),
            "EVLI" => medeni.Trim().StartsWith("evli", StringComparison.OrdinalIgnoreCase),
            _ => true
        };
    }

    /// <summary>Yenileme süresi (yıl). SAGLIK: çok tehlikeli 1, tehlikeli 3, az tehlikeli 5; ISG: 1 / 2 / 3; "AY:n": n/12.</summary>
    public static decimal SureYil(string? sure, string? sinif)
    {
        sinif = string.IsNullOrWhiteSpace(sinif) ? "Çok tehlikeli" : sinif;
        var s = (sure ?? "").Trim().ToUpperInvariant();
        return s switch
        {
            "SAGLIK" => sinif == "Az tehlikeli" ? 5 : sinif == "Tehlikeli" ? 3 : 1,
            "ISG" => sinif == "Az tehlikeli" ? 3 : sinif == "Tehlikeli" ? 2 : 1,
            _ when s.StartsWith("AY:") && decimal.TryParse(s[3..], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var ay) => ay / 12m,
            _ => 0
        };
    }

    /// <summary>Teslim tarihine göre önerilen geçerlilik bitişi (süresizse null). VBA: DateAdd("m", yıl*12, teslim) - 1.</summary>
    public static DateOnly? OnerilenBitis(string? sure, string? sinif, DateOnly teslim)
    {
        var y = SureYil(sure, sinif);
        if (y <= 0) return null;
        return teslim.AddMonths((int)Math.Round(y * 12)).AddDays(-1);
    }

    public static string SureMetni(string? sure, string? sinif)
    {
        var y = SureYil(sure, sinif);
        if (y <= 0) return "Süresiz";
        if (y >= 1 && y == Math.Floor(y)) return $"{y:0} yılda bir yenilenir";
        return $"{Math.Round(y * 12):0} ay geçerli";
    }

    /// <summary>VBA BG_DurumHesapla. kayitDurum: VAR / BEKLIYOR / GEREKMEZ; null = kayıt yok.</summary>
    public static (BelgeDurumKodu Kod, string Metin) Durum(string? kosul, bool kosulTutar, string? kayitDurum, DateOnly? bitis, DateOnly? istenme, DateOnly bugun)
    {
        if (!kosulTutar) return (BelgeDurumKodu.GerekmiyorVeyaIstegeBagli, "Gerekmiyor");
        if (kayitDurum is not null)
        {
            var d = kayitDurum.Trim().ToUpperInvariant();
            if (d.Length == 0) d = "VAR";
            if (d == "GEREKMEZ") return (BelgeDurumKodu.GerekmiyorVeyaIstegeBagli, "Gerekmiyor");
            if (d == "BEKLIYOR") return (BelgeDurumKodu.Bekliyor, istenme is { } i ? $"Bekliyor ({i:dd.MM})" : "Bekliyor");
            if (bitis is { } b)
            {
                if (b < bugun) return (BelgeDurumKodu.SuresiDoldu, "Süresi doldu");
                if (b <= bugun.AddDays(YaklasanGun)) return (BelgeDurumKodu.YakindaDoluyor, "Yakında doluyor");
            }
            return (BelgeDurumKodu.Tamam, "Tamam");
        }
        if ((kosul ?? "").ToUpperInvariant() == "VARSA") return (BelgeDurumKodu.GerekmiyorVeyaIstegeBagli, "İsteğe bağlı");
        return (BelgeDurumKodu.Eksik, "Eksik");
    }

    public static string DosyaAdiTemiz(string s)
    {
        foreach (var c in new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) s = s.Replace(c, '-');
        s = s.Trim();
        return s.Length > 80 ? s[..80].Trim() : s;
    }
}
