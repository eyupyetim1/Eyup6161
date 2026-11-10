using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Desktop.Views;

namespace PayControl.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IKimlikServisi _kimlik;
    private readonly OturumServisi _oturum;

    [ObservableProperty]
    private string _kullaniciAdi = "admin";

    [ObservableProperty]
    private string _sifre = string.Empty;

    [ObservableProperty]
    private string? _hataMesaji;

    [ObservableProperty]
    private bool _isBusy;

    public LoginViewModel(IKimlikServisi kimlik, OturumServisi oturum)
    {
        _kimlik = kimlik;
        _oturum = oturum;
    }

    [RelayCommand]
    private async Task GirisAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        HataMesaji = null;
        try
        {
            var sonuc = await _kimlik.GirisYapAsync(KullaniciAdi.Trim(), Sifre);
            if (!sonuc.Basarili || sonuc.KullaniciId is null || sonuc.Rol is null)
            {
                HataMesaji = sonuc.Hata ?? "Giriş başarısız.";
                return;
            }

            _oturum.Baslat(sonuc.KullaniciId.Value, sonuc.KullaniciAdi!, sonuc.Rol.Value);
            var main = App.Services.GetService(typeof(MainWindow)) as MainWindow
                       ?? new MainWindow(App.Services.GetService(typeof(MainViewModel)) as MainViewModel
                                         ?? throw new InvalidOperationException());
            main.Show();
            foreach (Window w in System.Windows.Application.Current.Windows)
            {
                if (w is LoginWindow)
                {
                    w.Close();
                    break;
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
