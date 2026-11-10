using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Interfaces;

namespace PayControl.Desktop.ViewModels;

public partial class SifreDegistirViewModel : ObservableObject
{
    private readonly IKimlikServisi _kimlik;

    public SifreDegistirViewModel(IKimlikServisi kimlik) => _kimlik = kimlik;

    public string KullaniciAdi { get; set; } = string.Empty;
    /// <summary>İlk girişte zorunlu değişim: eski şifre girişte doğrulandığı için alan gizlenir.</summary>
    public bool Zorunlu { get; set; }
    public string Aciklama => Zorunlu
        ? "Güvenliğiniz için ilk girişte veya şifreniz sıfırlandıktan sonra yeni bir şifre belirlemeniz gerekiyor."
        : "Şifre en az 8 karakter olmalı; büyük harf, küçük harf ve rakam içermelidir.";

    [ObservableProperty] private string? _eskiSifre;
    [ObservableProperty] private string? _yeniSifre;
    [ObservableProperty] private string? _yeniSifreTekrar;
    [ObservableProperty] private string? _hata;

    public event Action? Tamamlandi;

    [RelayCommand]
    private async Task KaydetAsync()
    {
        Hata = null;
        if (string.IsNullOrEmpty(YeniSifre) || YeniSifre != YeniSifreTekrar) { Hata = "Yeni şifreler aynı değil."; return; }
        try
        {
            await _kimlik.SifreDegistirAsync(KullaniciAdi, EskiSifre ?? "", YeniSifre);
            Tamamlandi?.Invoke();
        }
        catch (Exception ex) { Hata = ex.Message; }
    }
}
