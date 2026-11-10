using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class SifreDegistirWindow : Window
{
    public SifreDegistirWindow(SifreDegistirViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.Tamamlandi += () =>
        {
            if (System.Windows.Interop.ComponentDispatcher.IsThreadModal) DialogResult = true;
            Close();
        };
    }
}
