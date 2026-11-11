using System.Windows;
using System.Windows.Controls;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void SifreBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox pb)
            vm.Sifre = pb.Password;
    }
}
