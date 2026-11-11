using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class RaporMerkeziWindow : Window
{
    public RaporMerkeziWindow(RaporMerkeziViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.YukleAsync();
    }
}
