using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Entities;
using PayControl.Infrastructure.Import;

namespace PayControl.Desktop.ViewModels;

public partial class OdemeKontrolViewModel : ObservableObject
{
    private readonly IOdemeOncesiKontrolServisi _servis;
    private readonly OturumServisi _oturumServisi;

    [ObservableProperty] private int _donemYil = DateTime.Today.Year;
    [ObservableProperty] private int _donemAy = DateTime.Today.Month;
    [ObservableProperty] private string? _bankaDosyaYolu;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private OdemeKontrolOturumu? _kontrolOturumu;
    [ObservableProperty] private OdemeKontrolSatiri? _seciliSatir;
    [ObservableProperty] private string? _istisnaGerekce;

    public ObservableCollection<OdemeKontrolSatiri> Satirlar { get; } = new();

    public OdemeKontrolViewModel(IOdemeOncesiKontrolServisi servis, OturumServisi oturum)
    {
        _servis = servis;
        _oturumServisi = oturum;
    }

    [RelayCommand]
    private void DosyaSec()
    {
        var dlg = new OpenFileDialog { Filter = "Excel|*.xlsx;*.xlsm;*.xls|Tümü|*.*" };
        if (dlg.ShowDialog() == true)
            BankaDosyaYolu = dlg.FileName;
    }

    [RelayCommand]
    private async Task KontrolEtAsync()
    {
        Mesaj = null;
        if (string.IsNullOrWhiteSpace(BankaDosyaYolu) || !File.Exists(BankaDosyaYolu))
        {
            Mesaj = "Banka ödeme Excel dosyasını seçin.";
            return;
        }

        try
        {
            await using var fs = File.OpenRead(BankaDosyaYolu);
            var bytes = await File.ReadAllBytesAsync(BankaDosyaYolu);
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            await using var ms = new MemoryStream(bytes);
            var talimatlar = BankaTalimatOkuyucu.Oku(ms);

            KontrolOturumu = await _servis.KontrolBaslatAsync(
                DonemYil, DonemAy, talimatlar,
                Path.GetFileName(BankaDosyaYolu), hash,
                _oturumServisi.KullaniciAdi ?? "sistem");

            Satirlar.Clear();
            foreach (var s in KontrolOturumu.Satirlar.OrderByDescending(x => x.KritikMi))
                Satirlar.Add(s);

            var kritik = Satirlar.Count(s => s.KritikMi);
            Mesaj = $"Kontrol tamamlandı. {Satirlar.Count} satır, {kritik} kritik uyarı. Program bankaya ödeme göndermez.";
        }
        catch (Exception ex)
        {
            Mesaj = ex.Message;
        }
    }

    [RelayCommand]
    private async Task IstisnaOnaylaAsync()
    {
        if (SeciliSatir is null || KontrolOturumu is null) return;
        try
        {
            await _servis.IstisnaOnaylaAsync(SeciliSatir.Id, IstisnaGerekce ?? "", _oturumServisi.KullaniciAdi ?? "sistem");
            var guncel = await _servis.GetirAsync(KontrolOturumu.Id);
            KontrolOturumu = guncel;
            Satirlar.Clear();
            foreach (var s in guncel!.Satirlar.OrderByDescending(x => x.KritikMi))
                Satirlar.Add(s);
            Mesaj = "İstisna onaylandı.";
        }
        catch (Exception ex)
        {
            Mesaj = ex.Message;
        }
    }

    [RelayCommand]
    private async Task OnayliListeUretAsync()
    {
        if (KontrolOturumu is null) return;
        try
        {
            var klasor = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "PayControl", "OnayliOdemeListeleri");
            var liste = await _servis.OnayliListeUretAsync(KontrolOturumu.Id, _oturumServisi.KullaniciAdi ?? "sistem", klasor);
            Mesaj = $"Onaylı liste oluşturuldu (v{liste.Surum}): {liste.DosyaYolu}";
        }
        catch (Exception ex)
        {
            Mesaj = ex.Message;
        }
    }
}
