using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class PersonelDetayWindow : Window
{
    public PersonelDetayWindow(PersonelDetayViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
