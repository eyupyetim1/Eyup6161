using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Entities;

namespace PayControl.Desktop.ViewModels;

public partial class DonemKapanisViewModel : ObservableObject
{
    private readonly IDonemKapanisServisi _servis;
    private readonly OturumServisi _oturum;

    [ObservableProperty] private int _donemYil = DateTime.Today.Year;
    [ObservableProperty] private int _donemAy = DateTime.Today.Month;
    [ObservableProperty] private MaasDonemKapanisi? _donem;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private string? _kapanisNotu;
    [ObservableProperty] private string? _yenidenAcmaNedeni;
    [ObservableProperty] private string? _karsilastirmaMetni;
    [ObservableProperty] private DonemKontrolMaddesi? _seciliMadde;

    public ObservableCollection<DonemKontrolMaddesi> Maddeler { get; } = new();

    public DonemKapanisViewModel(IDonemKapanisServisi servis, OturumServisi oturum)
    {
        _servis = servis;
        _oturum = oturum;
    }

    [RelayCommand]
    private async Task YukleAsync()
    {
        Donem = await _servis.GetOrCreateAsync(DonemYil, DonemAy);
        await _servis.OzetHesaplaAsync(Donem.Id);
        Donem = await _servis.GetOrCreateAsync(DonemYil, DonemAy);
        Maddeler.Clear();
        foreach (var m in Donem.KontrolMaddeleri.OrderBy(x => x.Sira))
            Maddeler.Add(m);

        var k = await _servis.OncekiAyKarsilastirAsync(DonemYil, DonemAy);
        KarsilastirmaMetni = string.Join(Environment.NewLine, k.Nedenler);
        Mesaj = $"Durum: {Donem.Durum}";
    }

    [RelayCommand]
    private async Task MaddeTamamlaAsync()
    {
        if (SeciliMadde is null) return;
        try
        {
            await _servis.MaddeTamamlaAsync(SeciliMadde.Id, _oturum.KullaniciAdi ?? "sistem");
            await YukleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task KapatAsync()
    {
        if (Donem is null) return;
        try
        {
            await _servis.KapatAsync(Donem.Id, _oturum.KullaniciAdi ?? "sistem", KapanisNotu);
            await YukleAsync();
            Mesaj = "Dönem kapatıldı.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task YenidenAcAsync()
    {
        if (Donem is null) return;
        try
        {
            await _servis.YenidenAcAsync(Donem.Id, _oturum.KullaniciAdi ?? "sistem", YenidenAcmaNedeni ?? "");
            await YukleAsync();
            Mesaj = "Dönem yeniden açıldı.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }
}
