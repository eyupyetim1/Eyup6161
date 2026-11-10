namespace PayControl.Application.Interfaces;

public static class AyarAnahtarlari
{
    public const string OtomatikYedek = "Yedek.Otomatik";
    public const string YedekKlasoru = "Yedek.Klasor";
    public const string YedekSaklamaSayisi = "Yedek.SaklamaSayisi";
    public const string YedekSifresi = "Yedek.SifreSifreli";
    public const string GuncellemeAdresi = "Guncelleme.Adres";
    public const string GuncellemeOtomatikDenetim = "Guncelleme.Otomatik";
}

public interface IAyarServisi
{
    Task<string?> GetirAsync(string anahtar, CancellationToken ct = default);
    Task AyarlaAsync(string anahtar, string? deger, string kullanici, CancellationToken ct = default);
}

/// <summary>Parola / erişim anahtarı gibi gizli değerleri kalıcı olarak şifreler.</summary>
public interface ISifreKoruyucu
{
    string Koru(string acikMetin);
    string Coz(string korunmus);
}
