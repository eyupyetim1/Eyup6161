using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class PuantajWindow : Window
{
    public PuantajViewModel Vm { get; }
    private readonly Dictionary<DataGridColumn, int> _gunSutunlari = new();

    public PuantajWindow(PuantajViewModel vm)
    {
        InitializeComponent();
        DataContext = Vm = vm;
        vm.TabloYenilendi += SutunlariKur;
        Loaded += async (_, _) => await vm.IlkYukleAsync();
        Closing += (_, e) =>
        {
            if (vm.KaydedilmemisSayisi > 0 &&
                MessageBox.Show(this, $"{vm.KaydedilmemisSayisi} kaydedilmemiş değişiklik var. Kaydetmeden kapatılsın mı?", "Puantaj",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                e.Cancel = true;
        };
    }

    private const string HucreSablonu = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <Border Background="{Binding [{G}], Converter={StaticResource PuantajKodRengi}}" Margin="1" CornerRadius="4" ToolTip="{Binding Not[{G}]}">
            <Grid>
              <TextBlock Text="{Binding [{G}]}" HorizontalAlignment="Center" VerticalAlignment="Center" FontWeight="SemiBold" FontSize="12"
                         Foreground="{Binding [{G}], Converter={StaticResource PuantajKodRengi}, ConverterParameter=yazi}"/>
              <Polygon Points="0,0 6,0 6,6" Fill="#F79009" HorizontalAlignment="Right" VerticalAlignment="Top"
                       Visibility="{Binding Not[{G}], Converter={StaticResource Gorunurluk}}"/>
            </Grid>
          </Border>
        </DataTemplate>
        """;

    private const string OzetSablonu = """
        <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
          <TextBlock Text="{Binding Ozet.{A}, StringFormat=0.#}" HorizontalAlignment="Center" VerticalAlignment="Center" FontWeight="SemiBold"/>
        </DataTemplate>
        """;

    /// <summary>Gün sütunlarını (1..N) ve özet sütunlarını tabloya göre kurar.</summary>
    private void SutunlariKur()
    {
        var t = Vm.Tablo;
        while (Tablo.Columns.Count > 1) Tablo.Columns.RemoveAt(1);
        _gunSutunlari.Clear();
        if (t is null) return;
        foreach (var g in t.Gunler)
        {
            var baslik = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            baslik.Children.Add(new TextBlock { Text = g.Gun.ToString(), FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            baslik.Children.Add(new TextBlock { Text = g.HaftaGunuKisa, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center });
            var stil = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader), (Style)FindResource(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)));
            stil.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            stil.Setters.Add(new Setter(PaddingProperty, new Thickness(0, 6, 0, 6)));
            if (g.ResmiTatil is not null) stil.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xFC, 0xE7, 0xF3))));
            else if (g.HaftaSonu) stil.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xE0, 0xE7, 0xFF))));
            var sutun = new DataGridTemplateColumn
            {
                Header = baslik, Width = 35, CanUserSort = false, CanUserResize = false, HeaderStyle = stil,
                CellTemplate = (DataTemplate)XamlReader.Parse(HucreSablonu.Replace("{G}", g.Gun.ToString()))
            };
            if (g.ResmiTatil is not null) ToolTipService.SetToolTip(baslik, $"{g.ResmiTatil} ({g.TatilHakki:0.#} gün)");
            Tablo.Columns.Add(sutun);
            _gunSutunlari[sutun] = g.Gun;
        }
        foreach (var (ad, alan, ipucu) in new[]
                 {
                     ("Çalış.", "Calisilan", "Çalışılan gün (Ç + G + ½ YG)"), ("HT hak", "HaftasonuHakki", "Hafta sonu (hafta tatili) hakkı"),
                     ("HT kul.", "KullanilanHaftalikIzin", "Kullanılan haftalık izin (HT + ½ YG)"), ("Devr.", "DevredenIzin", "Devreden hafta tatili"),
                     ("Yi", "YillikIzin", "Yıllık izin"), ("R", "Raporlu", "Raporlu"), ("RT", "ResmiTatil", "Resmi tatil"), ("Üİ", "UcretsizIzin", "Ücretsiz izin"),
                     ("Top.", "AylikToplam", "Aylık toplam")
                 })
        {
            var b = new TextBlock { Text = ad, FontWeight = FontWeights.Bold, ToolTip = ipucu };
            var stil = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader), (Style)FindResource(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader)));
            stil.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xF7))));
            stil.Setters.Add(new Setter(HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
            Tablo.Columns.Add(new DataGridTemplateColumn
            {
                Header = b, Width = 50, CanUserSort = false, HeaderStyle = stil,
                CellTemplate = (DataTemplate)XamlReader.Parse(OzetSablonu.Replace("{A}", alan))
            });
        }
    }

    private IEnumerable<(PuantajSatirVM, int)> SeciliHucreler() =>
        Tablo.SelectedCells.Where(c => c.Item is PuantajSatirVM && c.Column is not null && _gunSutunlari.ContainsKey(c.Column))
            .Select(c => ((PuantajSatirVM)c.Item, _gunSutunlari[c.Column])).ToList();

    private void KodDugmesi_Click(object sender, RoutedEventArgs e)
    {
        var h = SeciliHucreler().ToList();
        if (h.Count == 0) { Vm.Mesaj = "Önce tablodan gün hücrelerini seçin (sürükleyerek veya Ctrl/Shift ile)."; return; }
        Vm.KodUygula(h, (string)((Button)sender).Tag);
        Tablo.Focus();
    }

    private void Sil_Click(object sender, RoutedEventArgs e) => Vm.KodUygula(SeciliHucreler(), null);

    private void BoslariDoldur_Click(object sender, RoutedEventArgs e)
    {
        var satirlar = Tablo.SelectedCells.Select(c => c.Item).OfType<PuantajSatirVM>().Distinct().ToList();
        if (satirlar.Count == 0) satirlar = Vm.Satirlar.ToList();
        Vm.BoslariDoldur(satirlar);
    }

    private void Tablo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Delete or Key.Back)
        {
            Vm.KodUygula(SeciliHucreler(), null);
            e.Handled = true;
        }
    }

    private void Tablo_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        var c = Tablo.CurrentCell;
        if (c.Item is PuantajSatirVM s && c.Column is not null && _gunSutunlari.TryGetValue(c.Column, out var g)) Vm.HucreSecildi(s, g);
        else Vm.HucreSecildi(null, null);
    }
}
