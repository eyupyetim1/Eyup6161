using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class NakilWindow : Window
{
    public NakilWindow(NakilViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.YukleAsync();
    }
}
