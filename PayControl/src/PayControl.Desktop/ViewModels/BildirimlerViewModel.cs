using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Desktop.ViewModels;

public partial class BildirimlerViewModel : ObservableObject
{
    private readonly IBildirimServisi _servis;
    private readonly ITesisServisi _tesis;
    private readonly IRaporServisi _rapor;
    private readonly IZamanlanmisGorevServisi _gorev;
    private readonly OturumServisi _oturum;

    public BildirimlerViewModel(IBildirimServisi servis, ITesisServisi tesis, IRaporServisi rapor, IZamanlanmisGorevServisi gorev, OturumServisi oturum)
    {
        _servis = servis;
        _tesis = tesis;
        _rapor = rapor;
        _gorev = gorev;
        _oturum = oturum;
        RaporTurleri = new[] { new Secenek<RaporTuru?>(null, "— (rapor yok)") }
            .Concat(rapor.Tanimlar.Select(t => new Secenek<RaporTuru?>(t.Tur, t.Ad))).ToList();
    }

    private string Kullanici => _oturum.KullaniciAdi ?? "";
    public bool Yonetici => _oturum.Rol == KullaniciRolu.Yonetici;

    public IReadOnlyList<Secenek<BildirimKanali>> Kanallar { get; } = ArayuzYardimcisi.EnumSecenekleri(
        (BildirimKanali.Eposta, "E-posta"), (BildirimKanali.WhatsApp, "WhatsApp"), (BildirimKanali.EpostaVeWhatsApp, "E-posta + WhatsApp"));
    public IReadOnlyList<Secenek<BildirimTetikleyici>> Tetikleyiciler { get; } = ArayuzYardimcisi.EnumSecenekleri(
        (BildirimTetikleyici.ZamanlanmisRapor, "Zamanlanmış rapor"), (BildirimTetikleyici.UyariOzeti, "Uyarı özeti (zamanlanmış)"),
        (BildirimTetikleyici.DonemKapandi, "Olay: dönem kapandı"), (BildirimTetikleyici.MutabakatFarkiOlustu, "Olay: mutabakat farkı"),
        (BildirimTetikleyici.YedekHatasi, "Olay: yedekleme hatası"));
    public IReadOnlyList<Secenek<BildirimSikligi>> Sikliklar { get; } = ArayuzYardimcisi.EnumSecenekleri(
        (BildirimSikligi.Gunluk, "Her gün"), (BildirimSikligi.Haftalik, "Her hafta (gün: 1=Pzt … 7=Paz)"), (BildirimSikligi.Aylik, "Her ay (gün: 1–28)"));
    public IReadOnlyList<Secenek<RaporBicimi>> Bicimler { get; } = ArayuzYardimcisi.EnumSecenekleri(
        (RaporBicimi.Excel, "Excel"), (RaporBicimi.Pdf, "PDF"), (RaporBicimi.Csv, "CSV"));
    public IReadOnlyList<Secenek<RaporTuru?>> RaporTurleri { get; }
    public IReadOnlyList<string> Roller { get; } = ["Yönetici", "Muhasebe", "Tesis sorumlusu", "İnsan kaynakları", "Denetçi"];

    public ObservableCollection<string> Tesisler { get; } = new();
    public ObservableCollection<BildirimAlicisi> Alicilar { get; } = new();
    public ObservableCollection<SecilebilirAlici> SecilebilirAlicilar { get; } = new();
    public ObservableCollection<BildirimKurali> Kurallar { get; } = new();
    public ObservableCollection<BildirimKaydi> Gecmis { get; } = new();

    [ObservableProperty] private BildirimAyarDto _ayar = new();
    [ObservableProperty] private string? _yeniSmtpSifre;
    [ObservableProperty] private string? _yeniWhatsAppAnahtari;
    [ObservableProperty] private string? _mesaj;

    [ObservableProperty] private BildirimAlicisi? _seciliAlici;
    [ObservableProperty] private BildirimAlicisi _formAlici = new();

    [ObservableProperty] private BildirimKurali? _seciliKural;
    [ObservableProperty] private BildirimKurali _formKural = new() { Ad = "Aylık tesis karşılaştırması", RaporTuru = RaporTuru.TesisKarsilastirma, Gun = 5 };

    [ObservableProperty] private BildirimKanali _elleKanal = BildirimKanali.Eposta;
    [ObservableProperty] private string? _elleAdresler;
    [ObservableProperty] private string? _elleKonu;
    [ObservableProperty] private string? _elleMetin;
    [ObservableProperty] private string? _waTelefon;

    partial void OnSeciliAliciChanged(BildirimAlicisi? value)
    {
        if (value is null) return;
        FormAlici = new BildirimAlicisi { Id = value.Id, AdSoyad = value.AdSoyad, Eposta = value.Eposta, Telefon = value.Telefon, Rol = value.Rol, Tesis = value.Tesis, Aktif = value.Aktif };
    }

    partial void OnSeciliKuralChanged(BildirimKurali? value)
    {
        if (value is null) return;
        FormKural = new BildirimKurali
        {
            Id = value.Id, Ad = value.Ad, Aktif = value.Aktif, Tetikleyici = value.Tetikleyici, Siklik = value.Siklik, Gun = value.Gun, Saat = value.Saat,
            Kanal = value.Kanal, AliciIdleri = value.AliciIdleri, AliciRolu = value.AliciRolu, Tesis = value.Tesis, RaporTuru = value.RaporTuru,
            RaporBicimi = value.RaporBicimi, SonCalisma = value.SonCalisma
        };
    }

    public async Task YukleAsync()
    {
        try
        {
            Ayar = await _servis.AyarlariGetirAsync();
            Tesisler.Clear();
            Tesisler.Add("");
            foreach (var t in (await _tesis.ListeleAsync()).Select(t => t.Tesis.Ad)) Tesisler.Add(t);
            await AlicilariYukleAsync();
            await KurallariYukleAsync();
            await GecmisYukleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private async Task AlicilariYukleAsync()
    {
        Alicilar.Clear();
        SecilebilirAlicilar.Clear();
        foreach (var a in await _servis.AlicilarAsync())
        {
            Alicilar.Add(a);
            if (a.Aktif) SecilebilirAlicilar.Add(new SecilebilirAlici(a));
        }
    }

    private async Task KurallariYukleAsync()
    {
        Kurallar.Clear();
        foreach (var k in await _servis.KurallarAsync()) Kurallar.Add(k);
    }

    [RelayCommand]
    private async Task GecmisYukleAsync()
    {
        Gecmis.Clear();
        foreach (var g in await _servis.GecmisAsync(500)) Gecmis.Add(g);
    }

    [RelayCommand]
    private async Task AyarKaydetAsync()
    {
        try
        {
            Ayar.SmtpSifre = string.IsNullOrEmpty(YeniSmtpSifre) ? null : YeniSmtpSifre;
            Ayar.WhatsAppErisimAnahtari = string.IsNullOrEmpty(YeniWhatsAppAnahtari) ? null : YeniWhatsAppAnahtari;
            await _servis.AyarlariKaydetAsync(Ayar, Kullanici);
            YeniSmtpSifre = null;
            YeniWhatsAppAnahtari = null;
            Ayar = await _servis.AyarlariGetirAsync();
            Mesaj = "Bildirim ayarları kaydedildi. Şifre ve erişim anahtarı şifrelenmiş olarak saklanır.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task DenemeGonderAsync()
    {
        var adres = Ayar.GonderenEposta;
        if (string.IsNullOrWhiteSpace(adres)) { Mesaj = "Önce gönderen e-posta adresini girin ve kaydedin."; return; }
        try
        {
            var k = await _servis.GonderAsync(new BildirimTalebi
            {
                Kanal = BildirimKanali.Eposta, DogrudanAdresler = [adres], Konu = "PayControl deneme e-postası",
                Metin = $"Bu e-posta PayControl bildirim ayarlarını doğrulamak için {DateTime.Now:dd.MM.yyyy HH:mm} tarihinde gönderildi."
            }, Kullanici);
            Mesaj = k.All(x => x.Durum == BildirimDurumu.Gonderildi) ? $"Deneme e-postası {adres} adresine gönderildi." : $"Gönderilemedi: {k.FirstOrDefault()?.Hata}";
            await GecmisYukleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private void YeniAlici()
    {
        SeciliAlici = null;
        FormAlici = new BildirimAlicisi { Id = Guid.Empty };
    }

    [RelayCommand]
    private async Task AliciKaydetAsync()
    {
        try
        {
            await _servis.AliciKaydetAsync(FormAlici, Kullanici);
            await AlicilariYukleAsync();
            Mesaj = $"Alıcı kaydedildi: {FormAlici.AdSoyad}";
            YeniAlici();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private void YeniKural()
    {
        SeciliKural = null;
        FormKural = new BildirimKurali { Id = Guid.Empty, Ad = "Yeni kural", RaporTuru = RaporTuru.DonemBordroOzeti, Gun = 5 };
    }

    [RelayCommand]
    private async Task KuralKaydetAsync()
    {
        try
        {
            await _servis.KuralKaydetAsync(FormKural, Kullanici);
            await KurallariYukleAsync();
            Mesaj = $"Kural kaydedildi: {FormKural.Ad}. Uygulama açıkken 5 dakikada bir; kapalıyken Windows Görev Zamanlayıcısı ile çalışır (Ayarlar → Zamanlanmış görevler).";
            YeniKural();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task KurallariSimdiCalistirAsync()
    {
        try
        {
            var s = await _gorev.CalistirAsync(DateTime.Now);
            await KurallariYukleAsync();
            await GecmisYukleAsync();
            Mesaj = s.ToString();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task ElleGonderAsync()
    {
        try
        {
            var idler = SecilebilirAlicilar.Where(a => a.Secili).Select(a => a.Alici.Id).ToList();
            var adresler = (ElleAdresler ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (idler.Count == 0 && adresler.Count == 0) { Mesaj = "Alıcı seçin veya adres yazın."; return; }
            var k = await _servis.GonderAsync(new BildirimTalebi
            {
                Kanal = ElleKanal, AliciIdleri = idler, DogrudanAdresler = adresler, Konu = ElleKonu ?? "", Metin = ElleMetin ?? ""
            }, Kullanici);
            var basarili = k.Count(x => x.Durum == BildirimDurumu.Gonderildi);
            Mesaj = $"{basarili}/{k.Count} gönderildi." + string.Concat(k.Where(x => x.Durum == BildirimDurumu.Hata).Take(5).Select(x => $"{Environment.NewLine}{x.Alici}: {x.Hata}"));
            await GecmisYukleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    /// <summary>API anahtarı olmadan: mesajı hazır WhatsApp sohbetini tarayıcıda / WhatsApp Masaüstü'nde açar.</summary>
    [RelayCommand]
    private async Task WhatsAppAcAsync()
    {
        try
        {
            var tel = WaTelefon ?? SecilebilirAlicilar.FirstOrDefault(a => a.Secili && !string.IsNullOrWhiteSpace(a.Alici.Telefon))?.Alici.Telefon;
            if (string.IsNullOrWhiteSpace(tel)) { Mesaj = "Telefon numarası girin veya telefonu olan bir alıcı seçin."; return; }
            var metin = string.IsNullOrWhiteSpace(ElleKonu) ? ElleMetin ?? "" : $"*{ElleKonu}*\n{ElleMetin}";
            ArayuzYardimcisi.Ac(await _servis.WhatsAppBaglantisiAsync(tel, metin, Kullanici));
            await GecmisYukleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task YenidenDeneAsync()
    {
        try
        {
            var n = await _servis.BasarisizlariYenidenDeneAsync();
            await GecmisYukleAsync();
            Mesaj = $"{n} bildirim yeniden denendi ve gönderildi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }
}
