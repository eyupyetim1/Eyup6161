using System.Windows;
using System.Windows.Input;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class UyariMerkeziWindow : Window
{
    private readonly UyariMerkeziViewModel _vm;

    public UyariMerkeziWindow(UyariMerkeziViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        Loaded += async (_, _) => await vm.YukleAsync();
    }

    private void Grid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.SeciliUyari?.PersonelId is not null) _vm.PersonelAcCommand.Execute(null);
    }
}
