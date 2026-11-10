using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PayControl.Desktop.Controls;

/// <summary>Özet gösterge kartı: ikon, başlık, büyük değer ve isteğe bağlı alt bilgi. Görünümü Themes/Theme.xaml'da.</summary>
public sealed class KpiKarti : Control
{
    public static readonly DependencyProperty BaslikProperty =
        DependencyProperty.Register(nameof(Baslik), typeof(string), typeof(KpiKarti), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DegerProperty =
        DependencyProperty.Register(nameof(Deger), typeof(string), typeof(KpiKarti), new PropertyMetadata("—"));
    public static readonly DependencyProperty AltBilgiProperty =
        DependencyProperty.Register(nameof(AltBilgi), typeof(string), typeof(KpiKarti), new PropertyMetadata(null));
    public static readonly DependencyProperty IkonProperty =
        DependencyProperty.Register(nameof(Ikon), typeof(string), typeof(KpiKarti), new PropertyMetadata(""));
    public static readonly DependencyProperty VurguProperty =
        DependencyProperty.Register(nameof(Vurgu), typeof(Brush), typeof(KpiKarti), new PropertyMetadata(Brushes.SteelBlue));

    public string Baslik { get => (string)GetValue(BaslikProperty); set => SetValue(BaslikProperty, value); }
    public string Deger { get => (string)GetValue(DegerProperty); set => SetValue(DegerProperty, value); }
    public string? AltBilgi { get => (string?)GetValue(AltBilgiProperty); set => SetValue(AltBilgiProperty, value); }
    public string Ikon { get => (string)GetValue(IkonProperty); set => SetValue(IkonProperty, value); }
    public Brush Vurgu { get => (Brush)GetValue(VurguProperty); set => SetValue(VurguProperty, value); }
}
