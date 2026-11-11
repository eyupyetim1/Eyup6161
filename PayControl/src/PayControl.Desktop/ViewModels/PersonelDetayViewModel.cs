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
    private readonly IAvansServisi _avansServisi;
    private readonly OturumServisi _oturum;

    [ObservableProperty] private PersonelDetayDto? _detay;
    [ObservableProperty] private string _baslik = "Personel Detayı";
    [ObservableProperty] private string? _kontrolOzeti;
    [ObservableProperty] private string? _islemMesaji;

    // Avans verme
    [ObservableProperty] private DateTime _avansTarihi = DateTime.Today;
    [ObservableProperty] private decimal _avansTutari;
    [ObservableProperty] private int _avansTaksit = 1;
    [ObservableProperty] private string? _avansAciklama;

    // Yeniden işe alım
    [ObservableProperty] private DateTime _yenidenGirisTarihi = DateTime.Today;
    [ObservableProperty] private string? _yenidenIsyeri;
    [ObservableProperty] private decimal? _yenidenMaas;

    public HesapEkstresiPanelModel Ekstre { get; }
    public PersonelEkPanelModel Ek { get; }
    public ObservableCollection<MaasDonemDto> MaasGecmisi { get; } = new();
    public ObservableCollection<CalismaDonemiDto> CalismaDonemleri { get; } = new();
    public ObservableCollection<IstenCikisDto> CikisKayitlari { get; } = new();
    public ObservableCollection<Domain.Entities.AvansKaydi> Avanslar { get; } = new();

    public bool MaasGorebilir => _oturum.MaasGorebilir;

    public PersonelDetayViewModel(
        IPersonelServisi personelServisi,
        IIstenCikisServisi cikisServisi,
        IAvansServisi avansServisi,
        IHesapEkstresiServisi ekstreServisi,
        IMaliHareketServisi defter,
        IBankaOdemeServisi banka,
        IKesintiServisi kesinti,
        IBelgeServisi belge,
        OturumServisi oturum)
    {
        _personelServisi = personelServisi;
        _cikisServisi = cikisServisi;
        _avansServisi = avansServisi;
        _oturum = oturum;
        Ekstre = new HesapEkstresiPanelModel(ekstreServisi, defter, banka, oturum);
        Ek = new PersonelEkPanelModel(kesinti, belge, banka, oturum);
        Ek.Degisti = async () => { if (Detay is not null) await Ekstre.YukleAsync(Detay.Id); };
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

        Avanslar.Clear();
        foreach (var a in await _avansServisi.ListeleAsync(personelId))
            Avanslar.Add(a);

        await Ekstre.YukleAsync(personelId);
        await Ek.YukleAsync(personelId);

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
        ArayuzYardimcisi.SahipAta(win);
        if (win.ShowDialog() == true)
            await YukleAsync(Detay.Id);
    }

    public bool IstenAyrildi => Detay?.CalismaDurumu == CalismaDurumu.IstenAyrildi;

    partial void OnDetayChanged(PersonelDetayDto? value) => OnPropertyChanged(nameof(IstenAyrildi));

    [RelayCommand]
    private async Task AvansVerAsync()
    {
        if (Detay is null) return;
        try
        {
            await _avansServisi.VerAsync(Detay.Id, DateOnly.FromDateTime(AvansTarihi), AvansTutari, AvansTaksit, 0m,
                null, _oturum.KullaniciAdi ?? "sistem", AvansAciklama);
            IslemMesaji = $"{AvansTutari:N2} TL avans verildi; {AvansTaksit} taksitlik mahsup planı oluşturuldu.";
            AvansTutari = 0;
            AvansAciklama = null;
            await YukleAsync(Detay.Id);
        }
        catch (Exception ex) { IslemMesaji = ex.Message; }
    }

    [RelayCommand]
    private async Task YenidenIseAlAsync()
    {
        if (Detay is null) return;
        try
        {
            await _personelServisi.YenidenIseAlAsync(Detay.Id, DateOnly.FromDateTime(YenidenGirisTarihi),
                string.IsNullOrWhiteSpace(YenidenIsyeri) ? null : YenidenIsyeri, null, null, YenidenMaas, _oturum.KullaniciAdi ?? "sistem");
            IslemMesaji = "Personel yeni çalışma dönemiyle yeniden işe alındı; önceki dönem ve mali geçmiş korunuyor.";
            await YukleAsync(Detay.Id);
        }
        catch (Exception ex) { IslemMesaji = ex.Message; }
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

    [RelayCommand]
    private async Task CikisTalimatiVerAsync()
    {
        if (Detay is null) return;
        ArayuzYardimcisi.PencereAc<CikisTakibiWindow>(w => w.Vm.PersoneleOdaklan(Detay.Id));
        await YukleAsync(Detay.Id);
    }
}
