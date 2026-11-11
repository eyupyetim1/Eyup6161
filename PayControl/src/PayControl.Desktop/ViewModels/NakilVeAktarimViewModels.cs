using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Desktop.ViewModels;

public partial class NakilViewModel : ObservableObject
{
    private readonly IPuantajServisi _puantaj;
    private readonly IPersonelServisi _personel;
    private readonly OturumServisi _oturum;

    public NakilViewModel(IPuantajServisi puantaj, IPersonelServisi personel, OturumServisi oturum)
    {
        _puantaj = puantaj;
        _personel = personel;
        _oturum = oturum;
    }

    public ObservableCollection<PersonelNakil> Nakiller { get; } = new();
    public ObservableCollection<PersonelOzetDto> Personeller { get; } = new();
    public ObservableCollection<string> Tesisler { get; } = new();
    public ObservableCollection<string> FiltreTesisler { get; } = new();

    [ObservableProperty] private string _filtreTesis = "Tüm tesisler";
    [ObservableProperty] private PersonelOzetDto? _seciliPersonel;
    [ObservableProperty] private string? _yeniTesis;
    [ObservableProperty] private DateTime _nakilTarihi = DateTime.Today;
    [ObservableProperty] private string? _aciklama;
    [ObservableProperty] private string? _mesaj;

    partial void OnFiltreTesisChanged(string value) => _ = ListeleAsync();

    public async Task YukleAsync()
    {
        try
        {
            Tesisler.Clear();
            FiltreTesisler.Clear();
            FiltreTesisler.Add("Tüm tesisler");
            foreach (var t in await _puantaj.TesislerAsync()) { Tesisler.Add(t); FiltreTesisler.Add(t); }
            Personeller.Clear();
            foreach (var p in (await _personel.ListeleAsync()).Where(p => p.CalismaDurumu == CalismaDurumu.Aktif)) Personeller.Add(p);
            await ListeleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private async Task ListeleAsync()
    {
        Nakiller.Clear();
        foreach (var n in await _puantaj.NakillerAsync(FiltreTesis == "Tüm tesisler" ? null : FiltreTesis)) Nakiller.Add(n);
    }

    [RelayCommand]
    private async Task NakilYapAsync()
    {
        if (SeciliPersonel is null || string.IsNullOrWhiteSpace(YeniTesis)) { Mesaj = "Personel ve yeni tesisi seçin."; return; }
        try
        {
            var n = await _puantaj.NakilYapAsync(SeciliPersonel.Id, YeniTesis, DateOnly.FromDateTime(NakilTarihi), Aciklama, _oturum.KullaniciAdi ?? "");
            Mesaj = $"{n.AdSoyad}: {n.EskiTesis} → {n.YeniTesis} ({n.NakilTarihi:dd.MM.yyyy}) aktarıldı.";
            Aciklama = null;
            await YukleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }
}

public partial class VeriAktarimiViewModel : ObservableObject
{
    private readonly IAccessAktarimServisi _servis;
    private readonly OturumServisi _oturum;

    public VeriAktarimiViewModel(IAccessAktarimServisi servis, OturumServisi oturum)
    {
        _servis = servis;
        _oturum = oturum;
    }

    public ObservableCollection<string> Ayrintilar { get; } = new();
    [ObservableProperty] private string? _dosya;
    [ObservableProperty] private bool _calisiyor;
    [ObservableProperty] private string? _sonuc;
    [ObservableProperty] private string? _mesaj;

    [RelayCommand]
    private void DosyaSec()
    {
        var d = ArayuzYardimcisi.DosyaSec("Access veritabanı|*.accdb;*.mdb|Tüm dosyalar|*.*", "Personel Yönetim Sistemi veritabanı (Database.accdb)");
        if (d is not null) Dosya = d;
    }

    [RelayCommand]
    private void KlasorSec()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Daha önce dışa aktarılmış CSV klasörü" };
        if (dlg.ShowDialog() == true) Dosya = dlg.FolderName;
    }

    [RelayCommand]
    private async Task AktarAsync()
    {
        if (string.IsNullOrWhiteSpace(Dosya)) { Mesaj = "Önce Access dosyasını seçin."; return; }
        if (!ArayuzYardimcisi.Onayla("Access verisi programa aktarılacak. Aynı TC'li personel güncellenir, puantaj günleri üzerine yazılır; başka veri silinmez. " +
                                     "İşlemden önce otomatik yedek almanız önerilir.\n\nDevam edilsin mi?")) return;
        Calisiyor = true;
        Mesaj = null;
        Ayrintilar.Clear();
        try
        {
            var klasor = Directory.Exists(Dosya) ? Dosya : await _servis.CsvyeCikarAsync(Dosya);
            var s = await _servis.IceAktarAsync(klasor, _oturum.KullaniciAdi ?? "");
            Sonuc = s.ToString();
            foreach (var u in s.Uyarilar.Take(200)) Ayrintilar.Add("Uyarı: " + u);
            if (s.AktarilmayanTablolar.Count > 0)
                Ayrintilar.Add("Sonraki aşamalarda aktarılacak tablolar (veri korunuyor, Access dosyanızda duruyor): " + string.Join(", ", s.AktarilmayanTablolar));
            if (!Directory.Exists(Dosya)) try { Directory.Delete(klasor, true); } catch { /* geçici klasör */ }
        }
        catch (Exception ex) { Mesaj = ex.Message; }
        finally { Calisiyor = false; }
    }
}
