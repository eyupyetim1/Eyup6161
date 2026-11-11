using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Desktop.Views;
using PayControl.Domain.Enums;

namespace PayControl.Desktop.ViewModels;

public sealed record GrafikCubugu(string Etiket, double BordroYukseklik, double OdenenYukseklik, string Ipucu);

public sealed record TesisPayi(string Tesis, string Personel, string Tutar, double Genislik, string Yuzde);

public partial class MainViewModel : ObservableObject
{
    private const double GrafikYuksekligi = 150;
    private readonly IPersonelServisi _personelServisi;
    private readonly IGostergeServisi _gosterge;
    private readonly OturumServisi _oturum;

    public ObservableCollection<GrafikCubugu> AylikGrafik { get; } = new();
    public ObservableCollection<TesisPayi> TesisPaylari { get; } = new();
    [ObservableProperty] private string _kritikUyari = "—";
    [ObservableProperty] private string _toplamUyari = "—";
    [ObservableProperty] private string _sonDonemNet = "—";
    [ObservableProperty] private string _sonDonemFark = "—";
    [ObservableProperty] private string _sonDonemEtiketi = "";
    [ObservableProperty] private string _grafikOzeti = "";
    public bool Yonetici => _oturum.Rol == KullaniciRolu.Yonetici;

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

    public MainViewModel(IPersonelServisi personelServisi, IGostergeServisi gosterge, OturumServisi oturum)
    {
        _personelServisi = personelServisi;
        _gosterge = gosterge;
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
            ? Personeller.Where(p => p.CalismaDurumu == CalismaDurumu.Aktif).Sum(p => p.GuncelMaas ?? 0m).ToString("N0", tr) + " ₺"
            : "Yetki yok";
    }

    [RelayCommand]
    private void PersonelAc()
    {
        if (SeciliPersonel is null) return;
        var vm = (PersonelDetayViewModel)App.Services.GetService(typeof(PersonelDetayViewModel))!;
        var win = new PersonelDetayWindow(vm);
        _ = vm.YukleAsync(SeciliPersonel.Id);
        ArayuzYardimcisi.SahipAta(win);
        win.ShowDialog();
        _ = YenileAsync();
    }

    [RelayCommand]
    private void BordroAktar()
    {
        var vm = (BordroImportViewModel)App.Services.GetService(typeof(BordroImportViewModel))!;
        var win = new BordroImportWindow(vm);
        ArayuzYardimcisi.SahipAta(win);
        win.ShowDialog();
        _ = YenileAsync();
    }

    [RelayCommand]
    private void OdemeKontrol()
    {
        var vm = (OdemeKontrolViewModel)App.Services.GetService(typeof(OdemeKontrolViewModel))!;
        var win = new OdemeKontrolWindow(vm);
        ArayuzYardimcisi.SahipAta(win);
        win.ShowDialog();
    }

    [RelayCommand]
    private void DonemKapanis()
    {
        var vm = (DonemKapanisViewModel)App.Services.GetService(typeof(DonemKapanisViewModel))!;
        var win = new DonemKapanisWindow(vm);
        ArayuzYardimcisi.SahipAta(win);
        win.ShowDialog();
    }

    public async Task IlkYukleAsync()
    {
        await YenileAsync();
        await GostergeYukleAsync();
        _ = GuncellemeDenetleAsync();
    }

    /// <summary>Açılışta sessiz güncelleme denetimi (Ayarlar → Güncelleme'den kapatılabilir; ağ yoksa sessizce geçilir).</summary>
    private async Task GuncellemeDenetleAsync()
    {
        try
        {
            using var scope = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(App.Services);
            var sp = scope.ServiceProvider;
            var ayar = (IAyarServisi)sp.GetService(typeof(IAyarServisi))!;
            if (await ayar.GetirAsync(AyarAnahtarlari.GuncellemeOtomatikDenetim) == "false") return;
            var surum = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 3);
            var yeni = await ((IGuncellemeServisi)sp.GetService(typeof(IGuncellemeServisi))!).DenetleAsync(surum);
            if (yeni is not null) DurumMetni = $"Yeni sürüm mevcut: Personel Yönetim Sistemi {yeni.Surum} — Ayarlar ve Yönetim → Güncelleme";
        }
        catch { /* çevrimdışı veya bildirim yok */ }
    }

    [RelayCommand]
    private async Task GostergeYukleAsync()
    {
        try
        {
            var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            var g = await _gosterge.GetirAsync(12);
            KritikUyari = g.KritikUyari.ToString("N0", tr);
            ToplamUyari = g.ToplamUyari.ToString("N0", tr);
            AylikGrafik.Clear();
            var enBuyuk = g.Aylar.Select(a => Math.Max(a.BordroNet, a.Odenen)).DefaultIfEmpty(0m).Max();
            foreach (var a in g.Aylar)
            {
                double H(decimal v) => enBuyuk <= 0 ? 0 : Math.Max(v > 0 ? 3 : 0, (double)(v / enBuyuk) * GrafikYuksekligi);
                AylikGrafik.Add(new GrafikCubugu(a.Etiket, MaasGorebilir ? H(a.BordroNet) : 0, MaasGorebilir ? H(a.Odenen) : 0,
                    MaasGorebilir ? $"{a.Ay:D2}/{a.Yil}\nBordro net: {a.BordroNet:N2} ₺\nÖdenen: {a.Odenen:N2} ₺\nFark: {a.Fark:N2} ₺\nPersonel: {a.Personel}" : $"{a.Ay:D2}/{a.Yil} · {a.Personel} personel"));
            }
            var son = g.Aylar.LastOrDefault(a => a.BordroNet > 0);
            SonDonemEtiketi = son is null ? "Bordro yok" : $"{son.Ay:D2}/{son.Yil} dönemi";
            SonDonemNet = son is null || !MaasGorebilir ? "—" : son.BordroNet.ToString("N2", tr) + " ₺";
            SonDonemFark = son is null || !MaasGorebilir ? "—" : son.Fark.ToString("N2", tr) + " ₺";
            GrafikOzeti = MaasGorebilir ? "Son 12 ay · bordro net ve gerçekleşen ödeme" : "Tutarları görme yetkiniz yok";
            TesisPaylari.Clear();
            foreach (var t in g.Tesisler.OrderByDescending(t => t.BordroNet).Take(8))
                TesisPaylari.Add(new TesisPayi(t.Tesis, $"{t.Personel} personel", MaasGorebilir ? t.BordroNet.ToString("N0", tr) + " ₺" : "",
                    Math.Max(4, (double)t.Pay * 260), (t.Pay * 100).ToString("N1", tr) + " %"));
        }
        catch (Exception ex) { DurumMetni = ex.Message; }
    }

    private static void Ac<TPencere>() where TPencere : System.Windows.Window => ArayuzYardimcisi.PencereAc<TPencere>();

    [ObservableProperty] private int _anaSekme;

    [RelayCommand]
    private void AnaSekmeAc(string? sekme) => AnaSekme = int.TryParse(sekme, out var i) ? i : 0;

    [RelayCommand]
    private void Puantaj(string? sekme)
    {
        ArayuzYardimcisi.PencereAc<PuantajWindow>(w =>
        {
            if (sekme == "1") w.Loaded += (_, _) => { if (w.FindName("Sekmeler") is System.Windows.Controls.TabControl tc) tc.SelectedIndex = 1; };
        });
        _ = GostergeYukleAsync();
    }

    [RelayCommand] private void Nakil() { Ac<NakilWindow>(); _ = YenileAsync(); }
    [RelayCommand] private void VeriAktarimi() { Ac<VeriAktarimiWindow>(); _ = YenileAsync(); _ = GostergeYukleAsync(); }
    [RelayCommand] private void RaporMerkezi() => Ac<RaporMerkeziWindow>();
    [RelayCommand] private void CikisTakibi() { Ac<CikisTakibiWindow>(); _ = YenileAsync(); _ = GostergeYukleAsync(); }
    [RelayCommand] private void Bildirimler() => Ac<BildirimlerWindow>();
    [RelayCommand] private void Tesisler() { Ac<TesislerWindow>(); _ = GostergeYukleAsync(); }
    [RelayCommand] private void DenetimGecmisi() => Ac<DenetimGecmisiWindow>();
    [RelayCommand] private void Yonetim() => Ac<YonetimWindow>();
    [RelayCommand] private void UyariMerkezi() { Ac<UyariMerkeziWindow>(); _ = GostergeYukleAsync(); }

    [RelayCommand]
    private void SifreDegistir()
    {
        var vm = (SifreDegistirViewModel)App.Services.GetService(typeof(SifreDegistirViewModel))!;
        vm.KullaniciAdi = _oturum.KullaniciAdi ?? "";
        var w = ArayuzYardimcisi.SahipAta(new SifreDegistirWindow(vm));
        if (w.ShowDialog() == true) DurumMetni = "Şifreniz değiştirildi.";
    }
}
