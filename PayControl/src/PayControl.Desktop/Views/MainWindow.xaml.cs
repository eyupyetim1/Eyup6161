using System.Windows;
using System.Windows.Input;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.IlkYukleAsync();
    }

    private void PersonelGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_vm.PersonelAcCommand.CanExecute(null))
            _vm.PersonelAcCommand.Execute(null);
    }
}
