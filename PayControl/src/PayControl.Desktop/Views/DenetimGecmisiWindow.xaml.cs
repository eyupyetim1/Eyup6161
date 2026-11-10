using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class DenetimGecmisiWindow : Window
{
    public DenetimGecmisiWindow(DenetimGecmisiViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.YukleAsync();
    }
}
