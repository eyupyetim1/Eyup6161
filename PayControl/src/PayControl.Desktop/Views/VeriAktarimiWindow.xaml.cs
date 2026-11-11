using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class VeriAktarimiWindow : Window
{
    public VeriAktarimiWindow(VeriAktarimiViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
