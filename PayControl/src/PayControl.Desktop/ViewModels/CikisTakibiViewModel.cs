using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Desktop.Views;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Desktop.ViewModels;

/// <summary>
/// Çıkış talimatı verme ve takibi: İK çıkışı yaptı mı, yapmadıysa son çalışma gününden sonra ne kadar, ne için ödendi.
/// </summary>
public partial class CikisTakibiViewModel : ObservableObject
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly ICikisTalimatServisi _servis;
    private readonly IPersonelServisi _personel;
    private readonly IBildirimServisi _bildirim;
    private readonly ITesisServisi _tesis;
    private readonly IRaporServisi _rapor;
    private readonly OturumServisi _oturum;
    private Guid? _onSeciliPersonel;

    public CikisTakibiViewModel(ICikisTalimatServisi servis, IPersonelServisi personel, IBildirimServisi bildirim, ITesisServisi tesis,
        IRaporServisi rapor, OturumServisi oturum)
    {
        _servis = servis;
        _personel = personel;
        _bildirim = bildirim;
        _tesis = tesis;
        _rapor = rapor;
        _oturum = oturum;
    }

    private string Kullanici => _oturum.KullaniciAdi ?? "";
    public bool MaasGorebilir => _oturum.MaasGorebilir;

    public ObservableCollection<PersonelOzetDto> AktifPersoneller { get; } = new();
    public ObservableCollection<Secenek<Guid?>> Sorumlular { get; } = new();
    public ObservableCollection<string> Tesisler { get; } = new();
    public ObservableCollection<CikisTakipDto> Satirlar { get; } = new();
    public ObservableCollection<CikisSonrasiOdemeDto> SeciliOdemeler { get; } = new();
    public IReadOnlyList<string> Nedenler { get; } = ["İstifa", "Devamsızlık", "Performans", "Deneme süresi", "Sözleşme sonu", "Disiplin", "Emeklilik", "Karşılıklı anlaşma"];

    // Yeni talimat
    [ObservableProperty] private PersonelOzetDto? _yeniPersonel;
    [ObservableProperty] private DateTime _yeniSonGun = DateTime.Today;
    [ObservableProperty] private string? _yeniNeden;
    [ObservableProperty] private Guid? _yeniSorumlu;
    [ObservableProperty] private string? _yeniAciklama;

    // Filtre
    [ObservableProperty] private bool _yalnizSorunlu;
    [ObservableProperty] private string _seciliTesis = "Tüm tesisler";
    [ObservableProperty] private string? _arama;

    [ObservableProperty] private CikisTakipDto? _seciliSatir;
    [ObservableProperty] private string? _iptalNedeni;
    [ObservableProperty] private string? _mesaj;

    [ObservableProperty] private string _geciken = "—";
    [ObservableProperty] private string _bekleyen = "—";
    [ObservableProperty] private string _fazlaOdeme = "—";
    [ObservableProperty] private string _fazlaOdemeAlt = "";
    [ObservableProperty] private string _tamamlanan = "—";

    public bool SeciliBekliyor => SeciliSatir?.TalimatId is not null && SeciliSatir.Durum is CikisTakipDurumu.Bekliyor or CikisTakipDurumu.Gecikti;

    partial void OnYalnizSorunluChanged(bool value) => _ = YenileAsync();
    private CancellationTokenSource? _aramaGecikme;
    partial void OnAramaChanged(string? value)
    {
        _aramaGecikme?.Cancel();
        var cts = _aramaGecikme = new CancellationTokenSource();
        _ = Task.Delay(250, cts.Token).ContinueWith(t => { if (!t.IsCanceled) _ = YenileAsync(); }, TaskScheduler.FromCurrentSynchronizationContext());
    }
    partial void OnSeciliTesisChanged(string value) => _ = YenileAsync();

    partial void OnSeciliSatirChanged(CikisTakipDto? value)
    {
        SeciliOdemeler.Clear();
        foreach (var o in value?.Odemeler ?? Array.Empty<CikisSonrasiOdemeDto>()) SeciliOdemeler.Add(o);
        OnPropertyChanged(nameof(SeciliBekliyor));
    }

    /// <summary>Personel kartından açıldığında kişi seçili gelir.</summary>
    public void PersoneleOdaklan(Guid personelId) => _onSeciliPersonel = personelId;

    public async Task YukleAsync()
    {
        try
        {
            AktifPersoneller.Clear();
            foreach (var p in (await _personel.ListeleAsync()).Where(p => p.CalismaDurumu == CalismaDurumu.Aktif).OrderBy(p => p.AdSoyad, StringComparer.Create(Tr, true)))
                AktifPersoneller.Add(p);
            if (_onSeciliPersonel is { } id) YeniPersonel = AktifPersoneller.FirstOrDefault(p => p.Id == id);

            Sorumlular.Clear();
            Sorumlular.Add(new Secenek<Guid?>(null, "— (yalnız kurallara göre bildir)"));
            foreach (var a in (await _bildirim.AlicilarAsync()).Where(a => a.Aktif).OrderByDescending(a => a.Rol.Contains("İnsan", StringComparison.CurrentCultureIgnoreCase) || a.Rol.Contains("IK", StringComparison.OrdinalIgnoreCase)))
                Sorumlular.Add(new Secenek<Guid?>(a.Id, $"{a.AdSoyad} · {a.Rol}"));

            Tesisler.Clear();
            Tesisler.Add("Tüm tesisler");
            foreach (var t in (await _tesis.ListeleAsync()).Select(t => t.Tesis.Ad)) Tesisler.Add(t);
            await YenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task YenileAsync()
    {
        try
        {
            var seciliId = SeciliSatir is { } s ? (s.TalimatId, s.PersonelId) : default;
            var liste = await _servis.ListeleAsync(new CikisTakipFiltre
            {
                YalnizSorunlu = YalnizSorunlu, Tesis = SeciliTesis == "Tüm tesisler" ? null : SeciliTesis, Metin = string.IsNullOrWhiteSpace(Arama) ? null : Arama.Trim(),
                Baslangic = DateOnly.FromDateTime(DateTime.Today.AddMonths(-12))
            });
            Satirlar.Clear();
            foreach (var x in liste) Satirlar.Add(x);
            SeciliSatir = Satirlar.FirstOrDefault(x => (x.TalimatId, x.PersonelId) == seciliId) ?? Satirlar.FirstOrDefault();

            Geciken = liste.Count(x => x.Durum == CikisTakipDurumu.Gecikti).ToString("N0", Tr);
            Bekleyen = liste.Count(x => x.Durum == CikisTakipDurumu.Bekliyor).ToString("N0", Tr);
            var fazla = liste.Sum(x => x.TahminiFazlaOdeme);
            FazlaOdeme = MaasGorebilir ? fazla.ToString("N2", Tr) + " ₺" : "Yetki yok";
            FazlaOdemeAlt = $"{liste.Count(x => x.TahminiFazlaOdeme > 0)} kişi";
            var ayBasi = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
            Tamamlanan = liste.Count(x => x.Durum == CikisTakipDurumu.CikisYapildi && x.CikisKaydiTarihi >= ayBasi.AddMonths(-1)).ToString("N0", Tr);
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task TalimatVerAsync()
    {
        if (YeniPersonel is null) { Mesaj = "Personel seçin."; return; }
        if (string.IsNullOrWhiteSpace(YeniNeden)) { Mesaj = "Çıkış nedenini seçin veya yazın."; return; }
        try
        {
            var t = await _servis.OlusturAsync(new CikisTalimatiOlusturDto
            {
                PersonelId = YeniPersonel.Id, SonCalismaGunu = DateOnly.FromDateTime(YeniSonGun), Neden = YeniNeden, Aciklama = YeniAciklama, SorumluAliciId = YeniSorumlu
            }, Kullanici);
            Mesaj = $"{YeniPersonel.AdSoyad} için çıkış talimatı kaydedildi (son gün {t.SonCalismaGunu:dd.MM.yyyy})" +
                    (t.Sorumlu is null ? "." : $"; {t.Sorumlu} kişisine bildirildi.") +
                    " Çıkış kaydı girilene kadar ödeme kontrolü bu kişiyi durdurur ve son gün geçince her gün hatırlatma gider.";
            YeniPersonel = null;
            YeniAciklama = null;
            YeniNeden = null;
            await YenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task CikisiKaydetAsync()
    {
        if (SeciliSatir is null) return;
        if (!SeciliBekliyor) { Mesaj = "Bu kişinin çıkışı zaten yapılmış veya talimat iptal edilmiş."; return; }
        var vm = (IstenCikisViewModel)App.Services.GetService(typeof(IstenCikisViewModel))!;
        vm.PersonelId = SeciliSatir.PersonelId;
        vm.PersonelAd = $"{SeciliSatir.AdSoyad} — {SeciliSatir.TcKimlikNo}";
        var son = SeciliSatir.SonCalismaGunu.ToDateTime(TimeOnly.MinValue);
        vm.FiiliSonCalisma = son;
        vm.IstenCikisTarihi = son;
        vm.CikisNedeni = SeciliSatir.Neden;
        vm.SonBordroYil = son.Year;
        vm.SonBordroAy = son.Month;
        var w = ArayuzYardimcisi.SahipAta(new IstenCikisWindow(vm));
        if (w.ShowDialog() == true)
        {
            Mesaj = $"{SeciliSatir.AdSoyad} için işten çıkış kaydı yapıldı; talimat tamamlandı.";
            await YenileAsync();
        }
    }

    [RelayCommand]
    private async Task IptalAsync()
    {
        if (SeciliSatir?.TalimatId is not { } id || !SeciliBekliyor) { Mesaj = "İptal için bekleyen bir talimat seçin."; return; }
        try
        {
            await _servis.IptalEtAsync(id, IptalNedeni ?? "", Kullanici);
            IptalNedeni = null;
            Mesaj = "Talimat iptal edildi.";
            await YenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task HatirlatAsync()
    {
        try
        {
            var n = await _servis.GecikenleriHatirlatAsync(DateTime.Now);
            Mesaj = n == 0 ? "Bugün hatırlatma gönderilecek (veya henüz gönderilmemiş) geciken talimat yok." : $"{n} geciken talimat için hatırlatma gönderildi.";
            await YenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task PersonelAcAsync()
    {
        if (SeciliSatir is null) return;
        var vm = (PersonelDetayViewModel)App.Services.GetService(typeof(PersonelDetayViewModel))!;
        var win = ArayuzYardimcisi.SahipAta(new PersonelDetayWindow(vm));
        _ = vm.YukleAsync(SeciliSatir.PersonelId);
        win.ShowDialog();
        await YenileAsync();
    }

    [RelayCommand]
    private async Task ExcelAsync()
    {
        try
        {
            var r = await _rapor.OlusturAsync(RaporTuru.CikisTalimatTakibi, new RaporParametreleri
            {
                BaslangicYil = DateTime.Today.AddMonths(-12).Year, BaslangicAy = DateTime.Today.AddMonths(-12).Month,
                BitisYil = DateTime.Today.Year, BitisAy = DateTime.Today.Month, Tesis = SeciliTesis == "Tüm tesisler" ? null : SeciliTesis
            });
            var yol = ArayuzYardimcisi.Kaydet(_rapor.DisaAktar(r, RaporBicimi.Excel), _rapor.DosyaAdi(r, RaporBicimi.Excel));
            if (yol is not null) Mesaj = $"Kaydedildi: {yol}";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }
}
