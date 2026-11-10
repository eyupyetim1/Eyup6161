using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;

namespace PayControl.Desktop.ViewModels;

public partial class BordroImportViewModel : ObservableObject
{
    private readonly IBordroIceAktarmaServisi _servis;
    private readonly OturumServisi _oturum;

    [ObservableProperty]
    private string? _dosyaYolu;

    [ObservableProperty]
    private int _donemYil = DateTime.Today.Year;

    [ObservableProperty]
    private int _donemAy = DateTime.Today.Month;

    [ObservableProperty]
    private IceAktarmaOnizlemeSonucu? _onizleme;

    [ObservableProperty]
    private string? _mesaj;

    [ObservableProperty]
    private bool _mukerrereIzinVer;

    public ObservableCollection<BordroSatirOnizlemeDto> Satirlar { get; } = new();

    public BordroImportViewModel(IBordroIceAktarmaServisi servis, OturumServisi oturum)
    {
        _servis = servis;
        _oturum = oturum;
    }

    [RelayCommand]
    private void DosyaSec()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel|*.xlsx;*.xlsm;*.xlsb;*.xls|CSV|*.csv|Tüm dosyalar|*.*",
            Title = "Bordro dosyası seç"
        };
        if (dlg.ShowDialog() == true)
            DosyaYolu = dlg.FileName;
    }

    [RelayCommand]
    private async Task OnizleAsync()
    {
        Mesaj = null;
        if (string.IsNullOrWhiteSpace(DosyaYolu) || !File.Exists(DosyaYolu))
        {
            Mesaj = "Dosya seçiniz.";
            return;
        }

        await using var fs = File.OpenRead(DosyaYolu);
        Onizleme = await _servis.OnizleAsync(fs, Path.GetFileName(DosyaYolu), DonemYil, DonemAy);
        Satirlar.Clear();
        foreach (var s in Onizleme.Satirlar)
            Satirlar.Add(s);

        Mesaj = Onizleme.MukerrerDosya
            ? "UYARI: Bu dosya daha önce içe aktarılmış (mükerrer)."
            : $"Önizleme: {Onizleme.GecerliSatirSayisi} geçerli, {Onizleme.HataliSatirSayisi} hatalı satır.";
    }

    [RelayCommand]
    private async Task AktarAsync()
    {
        Mesaj = null;
        if (string.IsNullOrWhiteSpace(DosyaYolu) || !File.Exists(DosyaYolu))
        {
            Mesaj = "Dosya seçiniz.";
            return;
        }

        await using var fs = File.OpenRead(DosyaYolu);
        var sonuc = await _servis.AktarAsync(
            fs,
            Path.GetFileName(DosyaYolu),
            DonemYil,
            DonemAy,
            _oturum.KullaniciAdi ?? "sistem",
            MukerrereIzinVer);

        Mesaj = sonuc.Mesaj;
        if (!sonuc.MukerrerEngellendi)
            await OnizleAsync();
    }
}
