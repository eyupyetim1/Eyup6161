using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Desktop.Views;
using PayControl.Domain.Enums;

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

    [ObservableProperty] private string _kullaniciAdi = string.Empty;
    [ObservableProperty] private string _rolEtiketi = string.Empty;
    [ObservableProperty] private string _toplamPersonel = "—";
    [ObservableProperty] private string _aktifPersonel = "—";
    [ObservableProperty] private string _ayrilanPersonel = "—";
    [ObservableProperty] private string _aylikMaasToplami = "—";

    public bool MaasGorebilir => _oturum.MaasGorebilir;

    public string Selamlama => DateTime.Now.Hour switch
    {
        < 12 => "Günaydın",
        < 18 => "İyi günler",
        _ => "İyi akşamlar"
    };

    public string Bugun => DateTime.Today.ToString("d MMMM yyyy, dddd", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));

    public ObservableCollection<PersonelOzetDto> Personeller { get; } = new();

    public MainViewModel(IPersonelServisi personelServisi, OturumServisi oturum)
    {
        _personelServisi = personelServisi;
        _oturum = oturum;
        KullaniciEtiketi = $"{_oturum.KullaniciAdi} ({_oturum.Rol})";
        KullaniciAdi = _oturum.KullaniciAdi ?? "";
        RolEtiketi = _oturum.Rol switch
        {
            KullaniciRolu.Yonetici => "Yönetici",
            KullaniciRolu.Muhasebe => "Muhasebe",
            _ => "Görüntüleyici"
        };
    }

    [RelayCommand]
    private async Task YenileAsync()
    {
        DurumMetni = "Yükleniyor...";
        var liste = await _personelServisi.ListeleAsync(Arama, FiltreSirket, FiltreIsyeri);
        Personeller.Clear();
        foreach (var p in liste)
            Personeller.Add(p);
        DurumMetni = $"{Personeller.Count} personel listelendi";
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        ToplamPersonel = Personeller.Count.ToString("N0", tr);
        AktifPersonel = Personeller.Count(p => p.CalismaDurumu == CalismaDurumu.Aktif).ToString("N0", tr);
        AyrilanPersonel = Personeller.Count(p => p.CalismaDurumu == CalismaDurumu.IstenAyrildi).ToString("N0", tr);
        AylikMaasToplami = MaasGorebilir
            ? Personeller.Where(p => p.CalismaDurumu == CalismaDurumu.Aktif).Sum(p => p.GuncelMaas ?? 0m).ToString("N2", tr) + " ₺"
            : "Yetki yok";
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

    [RelayCommand]
    private void OdemeKontrol()
    {
        var vm = (OdemeKontrolViewModel)App.Services.GetService(typeof(OdemeKontrolViewModel))!;
        var win = new OdemeKontrolWindow(vm);
        win.Owner = System.Windows.Application.Current.MainWindow;
        win.ShowDialog();
    }

    [RelayCommand]
    private void DonemKapanis()
    {
        var vm = (DonemKapanisViewModel)App.Services.GetService(typeof(DonemKapanisViewModel))!;
        var win = new DonemKapanisWindow(vm);
        win.Owner = System.Windows.Application.Current.MainWindow;
        win.ShowDialog();
    }

    public async Task IlkYukleAsync() => await YenileAsync();
}
