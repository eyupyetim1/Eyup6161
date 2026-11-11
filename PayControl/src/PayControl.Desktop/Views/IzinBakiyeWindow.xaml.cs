using System.Windows;
using System.Windows.Controls;
using PayControl.Desktop.Services;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class IzinBakiyeWindow : Window
{
    public IzinBakiyeViewModel Vm { get; }

    public IzinBakiyeWindow(IzinBakiyeViewModel vm)
    {
        InitializeComponent();
        DataContext = Vm = vm;
        Loaded += async (_, _) => await vm.YukleAsync();
    }

    /// <summary>VBA frmKurallar: hesap kuralları kaydırılabilir pencerede.</summary>
    private void Kurallar_Click(object sender, RoutedEventArgs e)
    {
        var w = new Window
        {
            Title = "Hesap kuralları", Width = 760, Height = 720, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = (System.Windows.Media.Brush)FindResource("ZeminBrush"), FontFamily = (System.Windows.Media.FontFamily)FindResource("AnaFont"),
            Content = new ScrollViewer
            {
                Padding = new Thickness(24, 20, 24, 20), VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new TextBlock { Text = Vm.Kurallar, TextWrapping = TextWrapping.Wrap, FontSize = 13.5, LineHeight = 21 }
            }
        };
        ArayuzYardimcisi.SahipAta(w).ShowDialog();
    }

    private void HamTablolar_Click(object sender, RoutedEventArgs e)
    {
        ArayuzYardimcisi.PencereAc<ModulVeriWindow>(w => w.Grup = "İzin ve bakiye");
        _ = Vm.YukleAsync();
    }
}
