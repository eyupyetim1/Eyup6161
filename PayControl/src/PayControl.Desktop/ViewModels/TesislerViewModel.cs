using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;

namespace PayControl.Desktop.ViewModels;

public partial class TesislerViewModel : ObservableObject
{
    private readonly ITesisServisi _tesis;
    private readonly IDonemKapanisServisi _donem;
    private readonly OturumServisi _oturum;

    public TesislerViewModel(ITesisServisi tesis, IDonemKapanisServisi donem, OturumServisi oturum)
    {
        _tesis = tesis;
        _donem = donem;
        _oturum = oturum;
    }

    private string Kullanici => _oturum.KullaniciAdi ?? "";

    public ObservableCollection<TesisOzetDto> Tesisler { get; } = new();
    public ObservableCollection<DonemTesisDurumuDto> DonemDurumu { get; } = new();

    [ObservableProperty] private TesisOzetDto? _seciliTesis;
    [ObservableProperty] private TesisDto _form = new();
    [ObservableProperty] private int _donemYil = DateTime.Today.Year;
    [ObservableProperty] private int _donemAy = DateTime.Today.Month;
    [ObservableProperty] private DonemTesisDurumuDto? _seciliDurum;
    [ObservableProperty] private string? _onayNotu;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private int _seciliSekme;
    [ObservableProperty] private string _onayOzeti = "—";

    /// <summary>Dönem kapanış ekranından açıldığında onay sekmesi ve dönem hazır gelir.</summary>
    public void DonemeOdaklan(int yil, int ay)
    {
        DonemYil = yil;
        DonemAy = ay;
        SeciliSekme = 1;
    }

    partial void OnSeciliTesisChanged(TesisOzetDto? value)
    {
        if (value is null) return;
        var t = value.Tesis;
        Form = new TesisDto
        {
            Id = t.Id, Kod = t.Kod, Ad = t.Ad, Sirket = t.Sirket, Adres = t.Adres, SorumluAdSoyad = t.SorumluAdSoyad,
            SorumluEposta = t.SorumluEposta, SorumluTelefon = t.SorumluTelefon, Aktif = t.Aktif
        };
    }

    public async Task YukleAsync()
    {
        await ListeYukleAsync();
        await DonemYukleAsync();
    }

    private async Task ListeYukleAsync()
    {
        Tesisler.Clear();
        foreach (var t in await _tesis.ListeleAsync()) Tesisler.Add(t);
    }

    [RelayCommand]
    private void Yeni()
    {
        SeciliTesis = null;
        Form = new TesisDto { Kod = $"T{Tesisler.Count + 1:000}" };
    }

    [RelayCommand]
    private async Task KaydetAsync()
    {
        try
        {
            var t = await _tesis.KaydetAsync(Form, Kullanici);
            await ListeYukleAsync();
            Mesaj = $"Tesis kaydedildi: {t.Kod} · {t.Ad}";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task EksikleriOlusturAsync()
    {
        try
        {
            var n = await _tesis.EksikleriOlusturAsync(Kullanici);
            await ListeYukleAsync();
            Mesaj = n == 0 ? "Personel kartlarındaki tüm işyerleri zaten tanımlı." : $"{n} tesis personel kartlarından oluşturuldu.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task DonemYukleAsync()
    {
        try
        {
            DonemDurumu.Clear();
            foreach (var d in await _tesis.DonemTesisDurumuAsync(DonemYil, Math.Clamp(DonemAy, 1, 12))) DonemDurumu.Add(d);
            OnayOzeti = $"{DonemDurumu.Count(d => d.Onaylandi)}/{DonemDurumu.Count} tesis onaylı";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task OnaylaAsync()
    {
        if (SeciliDurum is null) { Mesaj = "Onaylanacak tesisi seçin."; return; }
        try
        {
            var donem = await _donem.GetOrCreateAsync(DonemYil, DonemAy);
            await _tesis.TesisOnaylaAsync(donem.Id, SeciliDurum.Tesis, OnayNotu, Kullanici);
            OnayNotu = null;
            Mesaj = $"{SeciliDurum.Tesis} için {DonemAy:D2}/{DonemYil} dönemi onaylandı.";
            await DonemYukleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }
}
