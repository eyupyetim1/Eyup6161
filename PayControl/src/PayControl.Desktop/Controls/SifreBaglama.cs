using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace PayControl.Desktop.Controls;

/// <summary>
/// PasswordBox için bağlanabilir şifre: ctl:SifreBaglama.Sifre="{Binding Alan}".
/// Her PasswordBox'ın PasswordChanged olayı sınıf düzeyinde dinlenir; bağlama varsa yazılan değer kaynağa aktarılır.
/// Kaynak null/boş yapılırsa kutu temizlenir.
/// </summary>
public static class SifreBaglama
{
    public static readonly DependencyProperty SifreProperty = DependencyProperty.RegisterAttached(
        "Sifre", typeof(string), typeof(SifreBaglama),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Degisti));

    private static readonly DependencyProperty GuncelleniyorProperty =
        DependencyProperty.RegisterAttached("Guncelleniyor", typeof(bool), typeof(SifreBaglama));

    static SifreBaglama()
    {
        // Başlangıç değeri null olduğunda Degisti hiç çağrılmaz; bu yüzden olay her kutu için sınıf düzeyinde dinlenir.
        EventManager.RegisterClassHandler(typeof(PasswordBox), PasswordBox.PasswordChangedEvent, new RoutedEventHandler(KutuDegisti));
    }

    public static string? GetSifre(DependencyObject d) => (string?)d.GetValue(SifreProperty);
    public static void SetSifre(DependencyObject d, string? v) => d.SetValue(SifreProperty, v);

    private static void Degisti(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox kutu || (bool)kutu.GetValue(GuncelleniyorProperty)) return;
        var yeni = e.NewValue as string ?? "";
        if (kutu.Password != yeni) kutu.Password = yeni;
    }

    private static void KutuDegisti(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox kutu || BindingOperations.GetBindingExpression(kutu, SifreProperty) is null) return;
        kutu.SetValue(GuncelleniyorProperty, true);
        try { SetSifre(kutu, kutu.Password); }
        finally { kutu.SetValue(GuncelleniyorProperty, false); }
    }
}
