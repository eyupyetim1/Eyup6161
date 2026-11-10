using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class OdemeKontrolWindow : Window
{
    public OdemeKontrolWindow(OdemeKontrolViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
