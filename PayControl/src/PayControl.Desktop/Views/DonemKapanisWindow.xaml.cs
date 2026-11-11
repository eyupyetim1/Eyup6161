using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class DonemKapanisWindow : Window
{
    public DonemKapanisWindow(DonemKapanisViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
