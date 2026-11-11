using System.Windows;
using PayControl.Desktop.ViewModels;

namespace PayControl.Desktop.Views;

public partial class BildirimlerWindow : Window
{
    private readonly BildirimlerViewModel _vm;

    public BildirimlerWindow(BildirimlerViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        Loaded += async (_, _) => await vm.YukleAsync();
        vm.PropertyChanged += (_, e) =>
        {
            // Kaydedildikten sonra şifre kutularını temizle
            if (e.PropertyName == nameof(BildirimlerViewModel.YeniSmtpSifre) && vm.YeniSmtpSifre is null) SmtpSifreKutusu.Clear();
            if (e.PropertyName == nameof(BildirimlerViewModel.YeniWhatsAppAnahtari) && vm.YeniWhatsAppAnahtari is null) WhatsAppAnahtarKutusu.Clear();
        };
    }

    private void SmtpSifre_OnPasswordChanged(object sender, RoutedEventArgs e) => _vm.YeniSmtpSifre = SmtpSifreKutusu.Password;
    private void WhatsAppAnahtar_OnPasswordChanged(object sender, RoutedEventArgs e) => _vm.YeniWhatsAppAnahtari = WhatsAppAnahtarKutusu.Password;
}
