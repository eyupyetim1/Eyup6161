using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class CikisTakibiWindow : Window
{
    public CikisTakibiViewModel Vm { get; }

    public CikisTakibiWindow(CikisTakibiViewModel vm)
    {
        InitializeComponent();
        DataContext = Vm = vm;
        Loaded += async (_, _) => await vm.YukleAsync();
    }
}
