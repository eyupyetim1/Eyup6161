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
        System.Windows.Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w.IsVisible)
        ?? (System.Windows.Application.Current?.MainWindow is { IsVisible: true } m ? m : null)
        ?? System.Windows.Application.Current?.Windows.OfType<Window>().LastOrDefault(w => w.IsVisible);

    /// <summary>
    /// Yeni pencerenin sahibini güvenle atar: etkin pencere → ana pencere → görünür son pencere; pencerenin kendisi asla seçilmez.
    /// (Giriş penceresi kapandıktan sonra WPF, ilk oluşturulan pencereyi "ana pencere" sayabildiği için Owner kendisine ayarlanmaya çalışılıyordu.)
    /// </summary>
    public static T SahipAta<T>(T w) where T : Window
    {
        var app = System.Windows.Application.Current;
        if (app is null) return w;
        var adaylar = app.Windows.OfType<Window>().Where(x => !ReferenceEquals(x, w) && x.IsVisible && x.IsLoaded).ToList();
        var sahip = adaylar.FirstOrDefault(x => x.IsActive)
                    ?? (app.MainWindow is { } m && !ReferenceEquals(m, w) && m.IsVisible ? m : null)
                    ?? adaylar.LastOrDefault();
        if (ReferenceEquals(app.MainWindow, w) && sahip is not null) app.MainWindow = sahip;
        if (sahip is not null && !ReferenceEquals(sahip, w)) w.Owner = sahip;
        else w.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return w;
    }

    /// <summary>Ekrana sığmayan pencereyi (küçük ekran / yüksek ölçekleme) ekranı kaplayacak şekilde açar.</summary>
    public static void EkranaSigdir(Window w)
    {
        var alan = SystemParameters.WorkArea;
        var g = double.IsNaN(w.Width) ? w.ActualWidth : w.Width;
        var y = double.IsNaN(w.Height) ? w.ActualHeight : w.Height;
        if (w.WindowState == WindowState.Normal && w.ResizeMode != ResizeMode.NoResize && (g > alan.Width || y > alan.Height))
            w.WindowState = WindowState.Maximized;
    }

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
        SahipAta(w);
        hazirla?.Invoke(w);
        w.ShowDialog();
    }
}
