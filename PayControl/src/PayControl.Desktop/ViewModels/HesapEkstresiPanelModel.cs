using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Services;

namespace PayControl.Desktop.ViewModels;

/// <summary>
/// Personel detay içindeki Hesap Ekstresi sekmesi için alt model.
/// </summary>
public partial class HesapEkstresiPanelModel : ObservableObject
{
    private readonly IHesapEkstresiServisi _servis;
    private Guid _personelId;

    [ObservableProperty] private EkstreOzeti? _ozet;
    [ObservableProperty] private string? _mesaj;

    public ObservableCollection<MaliHareketDto> Hareketler { get; } = new();

    public HesapEkstresiPanelModel(IHesapEkstresiServisi servis) => _servis = servis;

    public async Task YukleAsync(Guid personelId)
    {
        _personelId = personelId;
        var ekstre = await _servis.GetirAsync(personelId);
        Ozet = ekstre.Ozet;
        Hareketler.Clear();
        foreach (var h in ekstre.Hareketler)
            Hareketler.Add(h);
    }

    [RelayCommand]
    private async Task ExcelAktarAsync()
    {
        if (_personelId == Guid.Empty) return;
        var dlg = new SaveFileDialog
        {
            Filter = "Excel|*.xlsx",
            FileName = "hesap-ekstresi.xlsx"
        };
        if (dlg.ShowDialog() != true) return;
        var bytes = await _servis.ExcelOlarakAktarAsync(_personelId);
        await System.IO.File.WriteAllBytesAsync(dlg.FileName, bytes);
        Mesaj = "Excel kaydedildi.";
    }

    [RelayCommand]
    private async Task PdfAktarAsync()
    {
        if (_personelId == Guid.Empty) return;
        var dlg = new SaveFileDialog
        {
            Filter = "PDF|*.pdf",
            FileName = "hesap-ekstresi.pdf"
        };
        if (dlg.ShowDialog() != true) return;
        var bytes = await _servis.PdfOlarakAktarAsync(_personelId);
        await System.IO.File.WriteAllBytesAsync(dlg.FileName, bytes);
        Mesaj = "PDF kaydedildi.";
    }
}
