using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Desktop.Views;
using PayControl.Domain.Enums;

namespace PayControl.Desktop.ViewModels;

public partial class PersonelDetayViewModel : ObservableObject
{
    private readonly IPersonelServisi _personelServisi;
    private readonly IIstenCikisServisi _cikisServisi;
    private readonly OturumServisi _oturum;

    [ObservableProperty]
    private PersonelDetayDto? _detay;

    [ObservableProperty]
    private string _baslik = "Personel Detayı";

    [ObservableProperty]
    private string? _kontrolOzeti;

    public ObservableCollection<MaasDonemDto> MaasGecmisi { get; } = new();
    public ObservableCollection<CalismaDonemiDto> CalismaDonemleri { get; } = new();
    public ObservableCollection<IstenCikisDto> CikisKayitlari { get; } = new();

    public bool MaasGorebilir => _oturum.MaasGorebilir;

    public PersonelDetayViewModel(
        IPersonelServisi personelServisi,
        IIstenCikisServisi cikisServisi,
        OturumServisi oturum)
    {
        _personelServisi = personelServisi;
        _cikisServisi = cikisServisi;
        _oturum = oturum;
    }

    public async Task YukleAsync(Guid personelId)
    {
        Detay = await _personelServisi.GetirAsync(personelId);
        if (Detay is null) return;

        Baslik = $"{Detay.AdSoyad} — {Detay.TcKimlikNo}";
        MaasGecmisi.Clear();
        foreach (var m in Detay.MaasGecmisi)
            MaasGecmisi.Add(m);

        CalismaDonemleri.Clear();
        foreach (var d in Detay.CalismaDonemleri)
            CalismaDonemleri.Add(d);

        CikisKayitlari.Clear();
        foreach (var c in Detay.CikisKayitlari)
            CikisKayitlari.Add(c);

        KontrolOzeti = Detay.CalismaDurumu == CalismaDurumu.IstenAyrildi
            ? "Personel işten ayrılmış. SGK ve ödeme denetimini Kontrol sekmesinden inceleyin."
            : "Aktif personel.";
    }

    [RelayCommand]
    private async Task IstenCikisOlusturAsync()
    {
        if (Detay is null) return;
        var vm = (IstenCikisViewModel)App.Services.GetService(typeof(IstenCikisViewModel))!;
        vm.PersonelId = Detay.Id;
        vm.PersonelAd = Detay.AdSoyad;
        var win = new IstenCikisWindow(vm);
        win.Owner = System.Windows.Application.Current.Windows.OfType<PersonelDetayWindow>().FirstOrDefault();
        if (win.ShowDialog() == true)
            await YukleAsync(Detay.Id);
    }

    [RelayCommand]
    private async Task CikisOdemeInceleAsync()
    {
        if (CikisKayitlari.Count == 0)
        {
            KontrolOzeti = "İncelenecek çıkış kaydı yok.";
            return;
        }

        var son = CikisKayitlari[0];
        var sonuc = await _cikisServisi.OdemeleriInceleAsync(son.Id);
        KontrolOzeti = $"{sonuc.Siniflandirma}: {sonuc.Gerekce}";
    }
}
