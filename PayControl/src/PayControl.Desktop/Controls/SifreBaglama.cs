using System.Windows;
using System.Windows.Controls;

namespace PayControl.Desktop.Controls;

/// <summary>PasswordBox için bağlanabilir şifre: ctl:SifreBaglama.Sifre="{Binding Alan, Mode=TwoWay}". Kaynak null/boş olursa kutu temizlenir.</summary>
public static class SifreBaglama
{
    public static readonly DependencyProperty SifreProperty = DependencyProperty.RegisterAttached(
        "Sifre", typeof(string), typeof(SifreBaglama),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, Degisti));

    private static readonly DependencyProperty GuncelleniyorProperty =
        DependencyProperty.RegisterAttached("Guncelleniyor", typeof(bool), typeof(SifreBaglama));

    public static string? GetSifre(DependencyObject d) => (string?)d.GetValue(SifreProperty);
    public static void SetSifre(DependencyObject d, string? v) => d.SetValue(SifreProperty, v);

    private static void Degisti(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox kutu) return;
        kutu.PasswordChanged -= KutuDegisti;
        if (!(bool)kutu.GetValue(GuncelleniyorProperty))
        {
            var yeni = e.NewValue as string ?? "";
            if (kutu.Password != yeni) kutu.Password = yeni;
        }
        kutu.PasswordChanged += KutuDegisti;
    }

    private static void KutuDegisti(object sender, RoutedEventArgs e)
    {
        var kutu = (PasswordBox)sender;
        kutu.SetValue(GuncelleniyorProperty, true);
        SetSifre(kutu, kutu.Password);
        kutu.SetValue(GuncelleniyorProperty, false);
    }
}
