using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class TesislerWindow : Window
{
    public TesislerViewModel Vm { get; }

    public TesislerWindow(TesislerViewModel vm)
    {
        InitializeComponent();
        DataContext = Vm = vm;
        Loaded += async (_, _) => await vm.YukleAsync();
    }
}
