using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace PayControl.Desktop.Services;

/// <summary>Açılır listeler için değer + Türkçe etiket.</summary>
public sealed record Secenek<T>(T Deger, string Ad)
{
    public override string ToString() => Ad;
}

public static class ArayuzYardimcisi
{
    public static Window? AktifPencere =>
        System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? System.Windows.Application.Current?.MainWindow;

    public static bool Onayla(string mesaj, string baslik = "PayControl") =>
        MessageBox.Show(AktifPencere!, mesaj, baslik, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    /// <summary>Dosyayı kaydetme diyaloğuyla kaydeder ve isteğe bağlı açar; iptal edilirse null.</summary>
    public static string? Kaydet(byte[] icerik, string onerilenAd, bool ac = true)
    {
        var uzanti = Path.GetExtension(onerilenAd);
        var dlg = new SaveFileDialog { FileName = onerilenAd, Filter = $"{uzanti.TrimStart('.').ToUpperInvariant()} dosyası|*{uzanti}|Tüm dosyalar|*.*" };
        if (dlg.ShowDialog() != true) return null;
        File.WriteAllBytes(dlg.FileName, icerik);
        if (ac) Ac(dlg.FileName);
        return dlg.FileName;
    }

    public static void Ac(string yolVeyaAdres)
    {
        try { Process.Start(new ProcessStartInfo(yolVeyaAdres) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(AktifPencere!, $"Açılamadı: {ex.Message}", "PayControl", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    public static string? DosyaSec(string filtre, string baslik)
    {
        var dlg = new OpenFileDialog { Filter = filtre, Title = baslik };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public static IReadOnlyList<Secenek<T>> EnumSecenekleri<T>(params (T Deger, string Ad)[] ogeler) => ogeler.Select(o => new Secenek<T>(o.Deger, o.Ad)).ToList();

    /// <summary>Pencereyi Services'ten çözer, sahibini ayarlar ve modal açar.</summary>
    public static void PencereAc<TPencere>(Action<TPencere>? hazirla = null) where TPencere : Window
    {
        var w = (TPencere)App.Services.GetService(typeof(TPencere))!;
        w.Owner = AktifPencere;
        hazirla?.Invoke(w);
        w.ShowDialog();
    }
}
