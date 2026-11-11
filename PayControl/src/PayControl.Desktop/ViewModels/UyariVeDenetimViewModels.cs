using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Desktop.Views;
using PayControl.Domain.Entities;

namespace PayControl.Desktop.ViewModels;

public partial class UyariMerkeziViewModel : ObservableObject
{
    private readonly IUyariServisi _uyari;
    private readonly ITesisServisi _tesis;
    private IReadOnlyList<UyariDto> _tum = Array.Empty<UyariDto>();

    public UyariMerkeziViewModel(IUyariServisi uyari, ITesisServisi tesis)
    {
        _uyari = uyari;
        _tesis = tesis;
    }

    public ObservableCollection<string> Tesisler { get; } = new();
    public ObservableCollection<UyariDto> Uyarilar { get; } = new();
    public IReadOnlyList<string> Seviyeler { get; } = ["Tümü", "Kritik", "Uyarı", "Bilgi"];
    public ObservableCollection<string> Kategoriler { get; } = new();

    [ObservableProperty] private string _seciliTesis = "Tüm tesisler";
    [ObservableProperty] private string _seciliSeviye = "Tümü";
    [ObservableProperty] private string _seciliKategori = "Tümü";
    [ObservableProperty] private UyariDto? _seciliUyari;
    [ObservableProperty] private string _kritik = "—";
    [ObservableProperty] private string _uyariSayisi = "—";
    [ObservableProperty] private string _bilgi = "—";
    [ObservableProperty] private string? _mesaj;

    partial void OnSeciliTesisChanged(string value) => _ = YenileAsync();
    partial void OnSeciliSeviyeChanged(string value) => Suz();
    partial void OnSeciliKategoriChanged(string value) => Suz();

    public async Task YukleAsync()
    {
        Tesisler.Clear();
        Tesisler.Add("Tüm tesisler");
        foreach (var t in (await _tesis.ListeleAsync()).Select(t => t.Tesis.Ad)) Tesisler.Add(t);
        await YenileAsync();
    }

    [RelayCommand]
    private async Task YenileAsync()
    {
        try
        {
            _tum = await _uyari.ListeleAsync(SeciliTesis == "Tüm tesisler" ? null : SeciliTesis);
            Kritik = _tum.Count(u => u.Seviye == UyariSeviyesi.Kritik).ToString("N0");
            UyariSayisi = _tum.Count(u => u.Seviye == UyariSeviyesi.Uyari).ToString("N0");
            Bilgi = _tum.Count(u => u.Seviye == UyariSeviyesi.Bilgi).ToString("N0");
            var secili = SeciliKategori;
            Kategoriler.Clear();
            Kategoriler.Add("Tümü");
            foreach (var k in _tum.Select(u => u.Kategori).Distinct().Order()) Kategoriler.Add(k);
            SeciliKategori = Kategoriler.Contains(secili) ? secili : "Tümü";
            Suz();
            Mesaj = _tum.Count == 0 ? "Açık uyarı yok." : null;
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private void Suz()
    {
        Uyarilar.Clear();
        foreach (var u in _tum.Where(u =>
                     (SeciliSeviye == "Tümü" || (SeciliSeviye == "Kritik" && u.Seviye == UyariSeviyesi.Kritik) ||
                      (SeciliSeviye == "Uyarı" && u.Seviye == UyariSeviyesi.Uyari) || (SeciliSeviye == "Bilgi" && u.Seviye == UyariSeviyesi.Bilgi)) &&
                     (SeciliKategori == "Tümü" || u.Kategori == SeciliKategori)))
            Uyarilar.Add(u);
    }

    [RelayCommand]
    private void PersonelAc()
    {
        if (SeciliUyari?.PersonelId is not { } id) { Mesaj = "Seçili uyarı bir personele bağlı değil."; return; }
        var vm = (PersonelDetayViewModel)App.Services.GetService(typeof(PersonelDetayViewModel))!;
        var win = ArayuzYardimcisi.SahipAta(new PersonelDetayWindow(vm));
        _ = vm.YukleAsync(id);
        win.ShowDialog();
        _ = YenileAsync();
    }
}

public partial class DenetimGecmisiViewModel : ObservableObject
{
    private readonly IDenetimServisi _denetim;

    public DenetimGecmisiViewModel(IDenetimServisi denetim) => _denetim = denetim;

    public ObservableCollection<DenetimKaydi> Kayitlar { get; } = new();
    public ObservableCollection<string> Kullanicilar { get; } = new();
    public ObservableCollection<string> Islemler { get; } = new();

    [ObservableProperty] private DateTime? _baslangic = DateTime.Today.AddDays(-30);
    [ObservableProperty] private DateTime? _bitis = DateTime.Today;
    [ObservableProperty] private string _seciliKullanici = "Tümü";
    [ObservableProperty] private string _seciliIslem = "Tümü";
    [ObservableProperty] private string? _varlikTuru;
    [ObservableProperty] private string? _metin;
    [ObservableProperty] private int _sayfa = 1;
    [ObservableProperty] private string _sayfaBilgisi = "";
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private DenetimKaydi? _seciliKayit;
    private int _toplam;
    private const int SayfaBoyutu = 200;

    private DenetimFiltre Filtre() => new()
    {
        Baslangic = Baslangic?.Date,
        Bitis = Bitis?.Date,
        Kullanici = SeciliKullanici == "Tümü" ? null : SeciliKullanici,
        Islem = SeciliIslem == "Tümü" ? null : SeciliIslem,
        VarlikTuru = string.IsNullOrWhiteSpace(VarlikTuru) ? null : VarlikTuru.Trim(),
        Metin = string.IsNullOrWhiteSpace(Metin) ? null : Metin.Trim(),
        Sayfa = Sayfa, SayfaBoyutu = SayfaBoyutu
    };

    public Task YukleAsync() => AraAsync();

    [RelayCommand]
    private async Task AraAsync()
    {
        try
        {
            var s = await _denetim.AraAsync(Filtre());
            _toplam = s.Toplam;
            Kayitlar.Clear();
            foreach (var k in s.Kayitlar) Kayitlar.Add(k);
            Doldur(Kullanicilar, s.Kullanicilar, SeciliKullanici, v => SeciliKullanici = v);
            Doldur(Islemler, s.Islemler, SeciliIslem, v => SeciliIslem = v);
            var sayfaSayisi = Math.Max(1, (int)Math.Ceiling(_toplam / (double)SayfaBoyutu));
            SayfaBilgisi = $"{_toplam:N0} kayıt · sayfa {Sayfa}/{sayfaSayisi}";
            Mesaj = null;
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private static void Doldur(ObservableCollection<string> hedef, IReadOnlyList<string> kaynak, string secili, Action<string> sec)
    {
        if (hedef.Count == kaynak.Count + 1) return;
        hedef.Clear();
        hedef.Add("Tümü");
        foreach (var x in kaynak) hedef.Add(x);
        sec(hedef.Contains(secili) ? secili : "Tümü");
    }

    [RelayCommand]
    private async Task FiltreUygulaAsync()
    {
        Sayfa = 1;
        await AraAsync();
    }

    [RelayCommand]
    private async Task SonrakiAsync()
    {
        if (Sayfa * SayfaBoyutu >= _toplam) return;
        Sayfa++;
        await AraAsync();
    }

    [RelayCommand]
    private async Task OncekiAsync()
    {
        if (Sayfa <= 1) return;
        Sayfa--;
        await AraAsync();
    }

    [RelayCommand]
    private async Task ExcelAsync()
    {
        try
        {
            var f = Filtre();
            f.Sayfa = 1;
            f.SayfaBoyutu = 100_000;
            var yol = ArayuzYardimcisi.Kaydet(await _denetim.ExcelAsync(f), $"Denetim-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
            if (yol is not null) Mesaj = $"Kaydedildi: {yol}";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }
}
