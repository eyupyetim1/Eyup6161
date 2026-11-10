using System.Diagnostics;

namespace PayControl.Infrastructure.Platform;

/// <summary>
/// PayControl kapalıyken de otomatik yedek ve zamanlanmış bildirimlerin çalışması için
/// Windows Görev Zamanlayıcısı'na (schtasks.exe) görev kaydeder / kaldırır.
/// </summary>
public static class WindowsGorevZamanlayici
{
    public const string GorevAdi = "PayControl\\Zamanlanmis Gorevler";

    /// <summary>schtasks /Create argümanları: her N dakikada bir, oturum açmış kullanıcı adına, en düşük yetkiyle.</summary>
    public static IReadOnlyList<string> OlusturmaArgumanlari(string exeYolu, int dakika)
    {
        if (dakika is < 5 or > 1439) throw new ArgumentOutOfRangeException(nameof(dakika), "Aralık 5–1439 dakika olmalıdır.");
        if (string.IsNullOrWhiteSpace(exeYolu)) throw new ArgumentException("Uygulama yolu boş.", nameof(exeYolu));
        return ["/Create", "/F", "/TN", GorevAdi, "/SC", "MINUTE", "/MO", dakika.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "/RL", "LIMITED", "/TR", $"\"{exeYolu}\" --zamanlanmis-gorevler"];
    }

    public static IReadOnlyList<string> SilmeArgumanlari() => ["/Delete", "/F", "/TN", GorevAdi];
    public static IReadOnlyList<string> SorguArgumanlari() => ["/Query", "/TN", GorevAdi];

    public static (bool Basarili, string Cikti) Calistir(IReadOnlyList<string> argumanlar)
    {
        if (!OperatingSystem.IsWindows()) return (false, "Görev Zamanlayıcısı yalnızca Windows'ta kullanılabilir.");
        var psi = new ProcessStartInfo("schtasks.exe") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var a in argumanlar) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var cikti = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit(15_000);
        return (p.ExitCode == 0, cikti.Trim());
    }

    public static (bool Basarili, string Cikti) Kaydet(string exeYolu, int dakika) => Calistir(OlusturmaArgumanlari(exeYolu, dakika));
    public static (bool Basarili, string Cikti) Kaldir() => Calistir(SilmeArgumanlari());
    public static bool KayitliMi() => OperatingSystem.IsWindows() && Calistir(SorguArgumanlari()).Basarili;
}
