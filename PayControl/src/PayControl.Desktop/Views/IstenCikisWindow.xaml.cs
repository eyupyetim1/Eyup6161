using System.ComponentModel;
using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class IstenCikisWindow : Window
{
    private readonly IstenCikisViewModel _vm;

    public IstenCikisWindow(IstenCikisViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.PropertyChanged += VmOnPropertyChanged;
    }

    private void VmOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IstenCikisViewModel.Kaydedildi) && _vm.Kaydedildi)
        {
            DialogResult = true;
            Close();
        }
    }

    private void Iptal_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void Kaydet_Click(object sender, RoutedEventArgs e)
    {
        // Command async çalışır; PropertyChanged ile kapanır.
    }
}
