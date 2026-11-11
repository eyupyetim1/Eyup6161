using System.Data;
using System.Windows;
using System.Windows.Controls;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class ModulVeriWindow : Window
{
    public ModulVeriViewModel Vm { get; }
    public string Grup { get; set; } = "İK işlemleri";

    public ModulVeriWindow(ModulVeriViewModel vm)
    {
        InitializeComponent();
        DataContext = Vm = vm;
        Loaded += async (_, _) => await vm.YukleAsync(Grup);
    }

    private void Tablo_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        var ad = e.PropertyName;
        if (ad is "PysId" or ModulVeriViewModel.AramaSutunu) { e.Cancel = true; return; }
        e.Column.Header = Vm.Servis.SutunBasligi(ad);
        if (ad == "Personel")
        {
            e.Column.IsReadOnly = true;
            e.Column.DisplayIndex = 0;
            if (e.Column is DataGridTextColumn p) p.FontWeight = FontWeights.SemiBold;
        }
        if (e.PropertyType == typeof(DateTime) && e.Column is DataGridTextColumn t)
            t.Binding = new System.Windows.Data.Binding(ad) { StringFormat = "dd.MM.yyyy", TargetNullValue = "" };
        if (e.PropertyType == typeof(decimal) && e.Column is DataGridTextColumn d)
            d.Binding = new System.Windows.Data.Binding(ad) { StringFormat = "#,0.##", TargetNullValue = "" };
    }

    private void Sil_Click(object sender, RoutedEventArgs e) =>
        Vm.SatirlariSil(Tablo.SelectedItems.OfType<DataRowView>());
}
