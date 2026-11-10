using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Desktop.Views;

namespace PayControl.Desktop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IPersonelServisi _personelServisi;
    private readonly OturumServisi _oturum;

    [ObservableProperty]
    private string _arama = string.Empty;

    [ObservableProperty]
    private string? _filtreSirket;

    [ObservableProperty]
    private string? _filtreIsyeri;

    [ObservableProperty]
    private PersonelOzetDto? _seciliPersonel;

    [ObservableProperty]
    private string _durumMetni = "Hazır";

    [ObservableProperty]
    private string _kullaniciEtiketi = string.Empty;

    public ObservableCollection<PersonelOzetDto> Personeller { get; } = new();

    public MainViewModel(IPersonelServisi personelServisi, OturumServisi oturum)
    {
        _personelServisi = personelServisi;
        _oturum = oturum;
        KullaniciEtiketi = $"{_oturum.KullaniciAdi} ({_oturum.Rol})";
    }

    [RelayCommand]
    private async Task YenileAsync()
    {
        DurumMetni = "Yükleniyor...";
        var liste = await _personelServisi.ListeleAsync(Arama, FiltreSirket, FiltreIsyeri);
        Personeller.Clear();
        foreach (var p in liste)
            Personeller.Add(p);
        DurumMetni = $"{Personeller.Count} personel";
    }

    [RelayCommand]
    private void PersonelAc()
    {
        if (SeciliPersonel is null) return;
        var vm = (PersonelDetayViewModel)App.Services.GetService(typeof(PersonelDetayViewModel))!;
        var win = new PersonelDetayWindow(vm);
        _ = vm.YukleAsync(SeciliPersonel.Id);
        win.Owner = System.Windows.Application.Current.MainWindow;
        win.ShowDialog();
        _ = YenileAsync();
    }

    [RelayCommand]
    private void BordroAktar()
    {
        var vm = (BordroImportViewModel)App.Services.GetService(typeof(BordroImportViewModel))!;
        var win = new BordroImportWindow(vm);
        win.Owner = System.Windows.Application.Current.MainWindow;
        win.ShowDialog();
        _ = YenileAsync();
    }

    public async Task IlkYukleAsync() => await YenileAsync();
}
