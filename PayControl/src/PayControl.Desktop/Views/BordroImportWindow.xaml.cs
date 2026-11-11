using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class BordroImportWindow : Window
{
    public BordroImportWindow(BordroImportViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
