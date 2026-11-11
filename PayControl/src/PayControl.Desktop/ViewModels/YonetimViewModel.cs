using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npgsql;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Infrastructure.Data;
using PayControl.Infrastructure.Platform;

namespace PayControl.Desktop.ViewModels;

public partial class YonetimViewModel : ObservableObject
{
    private readonly IKimlikServisi _kimlik;
    private readonly IYedekServisi _yedek;
    private readonly IAyarServisi _ayar;
    private readonly IGuncellemeServisi _guncelleme;
    private readonly IZamanlanmisGorevServisi _gorev;
    private readonly ISifreKoruyucu _koruyucu;
    private readonly VeritabaniBilgisi _vt;
    private readonly OturumServisi _oturum;
    private GuncellemeBilgisi? _bulunan;

    public YonetimViewModel(IKimlikServisi kimlik, IYedekServisi yedek, IAyarServisi ayar, IGuncellemeServisi guncelleme, IZamanlanmisGorevServisi gorev,
        ISifreKoruyucu koruyucu, VeritabaniBilgisi vt, OturumServisi oturum)
    {
        _kimlik = kimlik;
        _yedek = yedek;
        _ayar = ayar;
        _guncelleme = guncelleme;
        _gorev = gorev;
        _koruyucu = koruyucu;
        _vt = vt;
        _oturum = oturum;
        MevcutSurum = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 3, 0);
        VeritabaniOzeti = vt.SqliteMi ? $"SQLite (tek kullanıcı) · {vt.DosyaYolu}" : "PostgreSQL (çok kullanıcılı sunucu)";
        PgSecili = !vt.SqliteMi;
    }

    private string Kullanici => _oturum.KullaniciAdi ?? "";

    public IReadOnlyList<Secenek<KullaniciRolu>> Roller { get; } = ArayuzYardimcisi.EnumSecenekleri(
        (KullaniciRolu.Goruntuleyici, "Görüntüleyici (maaş göremez)"), (KullaniciRolu.Muhasebe, "Muhasebe"), (KullaniciRolu.Yonetici, "Yönetici"));

    public ObservableCollection<KullaniciDto> Kullanicilar { get; } = new();
    public ObservableCollection<YedekKaydi> Yedekler { get; } = new();

    [ObservableProperty] private string? _mesaj;

    // ---- Kullanıcılar
    [ObservableProperty] private KullaniciDto? _seciliKullanici;
    [ObservableProperty] private KullaniciOlusturDto _yeniKullanici = new() { Rol = KullaniciRolu.Muhasebe };
    [ObservableProperty] private KullaniciRolu _duzenRol;
    [ObservableProperty] private bool _duzenAktif;
    [ObservableProperty] private string? _duzenAdSoyad;
    [ObservableProperty] private string? _duzenEposta;
    [ObservableProperty] private string? _duzenTelefon;
    [ObservableProperty] private string? _geciciSifre;

    partial void OnSeciliKullaniciChanged(KullaniciDto? value)
    {
        if (value is null) return;
        DuzenRol = value.Rol; DuzenAktif = value.Aktif; DuzenAdSoyad = value.AdSoyad; DuzenEposta = value.Eposta; DuzenTelefon = value.Telefon;
    }

    // ---- Yedekleme
    [ObservableProperty] private bool _otomatikYedek = true;
    [ObservableProperty] private string? _yedekKlasoru;
    [ObservableProperty] private string _saklamaSayisi = "30";
    [ObservableProperty] private string? _yedekSifresi;
    [ObservableProperty] private string? _geriYuklemeSifresi;

    // ---- Zamanlanmış görevler
    [ObservableProperty] private string _gorevAraligi = "15";
    [ObservableProperty] private string _gorevDurumu = "—";

    // ---- Veritabanı
    public string VeritabaniOzeti { get; }
    [ObservableProperty] private bool _pgSecili;
    [ObservableProperty] private string _pgSunucu = "localhost";
    [ObservableProperty] private string _pgPort = "5432";
    [ObservableProperty] private string _pgVeritabani = "paycontrol";
    [ObservableProperty] private string _pgKullanici = "paycontrol";
    [ObservableProperty] private string? _pgSifre;

    // ---- Güncelleme
    public Version MevcutSurum { get; }
    public string SurumMetni => $"Kurulu sürüm: {MevcutSurum.ToString(3)}";
    [ObservableProperty] private string? _guncellemeAdresi;
    [ObservableProperty] private bool _guncellemeOtomatik = true;
    [ObservableProperty] private string? _guncellemeDurumu;
    [ObservableProperty] private double _indirmeIlerleme;
    [ObservableProperty] private bool _guncellemeHazir;

    public async Task YukleAsync()
    {
        try
        {
            await KullanicilariYukleAsync();
            await YedekleriYukleAsync();
            OtomatikYedek = await _ayar.GetirAsync(AyarAnahtarlari.OtomatikYedek) != "false";
            YedekKlasoru = await _ayar.GetirAsync(AyarAnahtarlari.YedekKlasoru);
            SaklamaSayisi = await _ayar.GetirAsync(AyarAnahtarlari.YedekSaklamaSayisi) ?? "30";
            GuncellemeAdresi = await _ayar.GetirAsync(AyarAnahtarlari.GuncellemeAdresi);
            GuncellemeOtomatik = await _ayar.GetirAsync(AyarAnahtarlari.GuncellemeOtomatikDenetim) != "false";
            GorevDurumu = WindowsGorevZamanlayici.KayitliMi() ? "Windows Görev Zamanlayıcısı'nda kayıtlı" : "Kayıtlı değil (yalnız uygulama açıkken çalışır)";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private async Task KullanicilariYukleAsync()
    {
        Kullanicilar.Clear();
        foreach (var k in await _kimlik.ListeleAsync()) Kullanicilar.Add(k);
    }

    private async Task YedekleriYukleAsync()
    {
        Yedekler.Clear();
        foreach (var y in await _yedek.ListeleAsync()) Yedekler.Add(y);
    }

    [RelayCommand]
    private async Task KullaniciOlusturAsync()
    {
        try
        {
            var k = await _kimlik.OlusturAsync(YeniKullanici, Kullanici);
            YeniKullanici = new KullaniciOlusturDto { Rol = KullaniciRolu.Muhasebe };
            await KullanicilariYukleAsync();
            Mesaj = $"{k.KullaniciAdi} oluşturuldu. İlk girişte şifresini değiştirmesi istenecek.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task KullaniciGuncelleAsync()
    {
        if (SeciliKullanici is null) { Mesaj = "Kullanıcı seçin."; return; }
        try
        {
            await _kimlik.GuncelleAsync(SeciliKullanici.Id, DuzenRol, DuzenAktif, DuzenAdSoyad, DuzenEposta, DuzenTelefon, Kullanici);
            await KullanicilariYukleAsync();
            Mesaj = "Kullanıcı güncellendi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task SifreSifirlaAsync()
    {
        if (SeciliKullanici is null || string.IsNullOrWhiteSpace(GeciciSifre)) { Mesaj = "Kullanıcı seçin ve geçici şifre yazın."; return; }
        try
        {
            await _kimlik.SifreSifirlaAsync(SeciliKullanici.Id, GeciciSifre, Kullanici);
            GeciciSifre = null;
            await KullanicilariYukleAsync();
            Mesaj = "Geçici şifre atandı; kilit kaldırıldı. Kullanıcı ilk girişte yeni şifre belirleyecek.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task YedekAlAsync()
    {
        try
        {
            var y = await _yedek.YedekAlAsync(Kullanici);
            await YedekleriYukleAsync();
            Mesaj = $"Yedek alındı: {y.DosyaYolu} ({y.Boyut / 1024.0:N0} KB{(y.Sifreli ? ", şifreli" : "")})";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task YedekAyarlariKaydetAsync()
    {
        try
        {
            if (!int.TryParse(SaklamaSayisi, out var n) || n < 1) throw new InvalidOperationException("Saklanacak yedek sayısı 1 veya daha büyük olmalıdır.");
            if (!string.IsNullOrWhiteSpace(YedekKlasoru)) Directory.CreateDirectory(YedekKlasoru);
            await _ayar.AyarlaAsync(AyarAnahtarlari.OtomatikYedek, OtomatikYedek ? "true" : "false", Kullanici);
            await _ayar.AyarlaAsync(AyarAnahtarlari.YedekKlasoru, string.IsNullOrWhiteSpace(YedekKlasoru) ? null : YedekKlasoru.Trim(), Kullanici);
            await _ayar.AyarlaAsync(AyarAnahtarlari.YedekSaklamaSayisi, n.ToString(), Kullanici);
            if (YedekSifresi is not null)
            {
                await _yedek.YedekSifresiAyarlaAsync(YedekSifresi.Length == 0 ? null : YedekSifresi, Kullanici);
                YedekSifresi = null;
            }
            Mesaj = "Yedekleme ayarları kaydedildi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private void KlasorSec()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Yedek klasörü (ağ sürücüsü veya harici disk önerilir)" };
        if (dlg.ShowDialog() == true) YedekKlasoru = dlg.FolderName;
    }

    [RelayCommand]
    private async Task GeriYukleAsync()
    {
        var dosya = ArayuzYardimcisi.DosyaSec("Personel Yönetim Sistemi yedeği|*.pcyedek;*.zip|Tüm dosyalar|*.*", "Geri yüklenecek yedek");
        if (dosya is null) return;
        if (!ArayuzYardimcisi.Onayla($"{Path.GetFileName(dosya)} geri yüklenecek.\n\nMevcut veriler önce otomatik güvenlik yedeğine alınır, ardından yedekteki verilerle değiştirilir. Uygulama yeniden başlatılacak.\n\nDevam edilsin mi?"))
            return;
        try
        {
            var guvenlik = await _yedek.GeriYukleAsync(dosya, GeriYuklemeSifresi, Kullanici);
            MessageBox.Show(ArayuzYardimcisi.AktifPencere!, $"Geri yükleme tamamlandı.\nÖnceki verilerin güvenlik yedeği:\n{guvenlik}\n\nPersonel Yönetim Sistemi şimdi yeniden başlatılacak.",
                "Personel Yönetim Sistemi", MessageBoxButton.OK, MessageBoxImage.Information);
            YenidenBaslat();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private static void YenidenBaslat()
    {
        var exe = Environment.ProcessPath;
        if (exe is not null) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        System.Windows.Application.Current.Shutdown();
    }

    [RelayCommand]
    private void GorevKaydet()
    {
        if (!int.TryParse(GorevAraligi, out var dk)) { Mesaj = "Aralık dakika olarak girilmelidir."; return; }
        try
        {
            var (ok, cikti) = WindowsGorevZamanlayici.Kaydet(Environment.ProcessPath ?? "PayControl.exe", dk);
            GorevDurumu = ok ? $"Kayıtlı: her {dk} dakikada bir (Personel Yönetim Sistemi kapalıyken de)" : "Kaydedilemedi";
            Mesaj = ok ? "Windows Görev Zamanlayıcısı'na kaydedildi: otomatik yedek, zamanlanmış raporlar ve bildirim yeniden denemeleri uygulama kapalıyken de çalışır." : cikti;
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private void GorevKaldir()
    {
        var (ok, cikti) = WindowsGorevZamanlayici.Kaldir();
        GorevDurumu = ok ? "Kayıtlı değil (yalnız uygulama açıkken çalışır)" : GorevDurumu;
        Mesaj = ok ? "Zamanlanmış görev kaldırıldı." : cikti;
    }

    [RelayCommand]
    private async Task GorevSimdiCalistirAsync()
    {
        try
        {
            Mesaj = (await _gorev.CalistirAsync(DateTime.Now)).ToString();
            await YedekleriYukleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private string PgBaglantisi() =>
        new NpgsqlConnectionStringBuilder
        {
            Host = PgSunucu.Trim(), Port = int.TryParse(PgPort, out var p) ? p : 5432, Database = PgVeritabani.Trim(),
            Username = PgKullanici.Trim(), Password = PgSifre ?? "", Timeout = 8
        }.ConnectionString;

    [RelayCommand]
    private async Task PgDeneAsync()
    {
        try
        {
            await using var c = new NpgsqlConnection(PgBaglantisi());
            await c.OpenAsync();
            Mesaj = $"Bağlantı başarılı: PostgreSQL {c.PostgreSqlVersion}.";
        }
        catch (Exception ex) { Mesaj = $"Bağlanılamadı: {ex.Message}"; }
    }

    [RelayCommand]
    private void VeritabaniKaydet()
    {
        if (_oturum.Rol != KullaniciRolu.Yonetici) { Mesaj = "Veritabanı ayarını yalnız yönetici değiştirebilir."; return; }
        try
        {
            var y = PgSecili
                ? new VeritabaniYapilandirmasi { Saglayici = "PostgreSql", BaglantiMetniSifreli = _koruyucu.Koru(PgBaglantisi()) }
                : new VeritabaniYapilandirmasi { Saglayici = "Sqlite" };
            if (!ArayuzYardimcisi.Onayla(PgSecili
                    ? "Uygulama bundan sonra PostgreSQL sunucusunu kullanacak. Boş veritabanında tablolar otomatik oluşturulur.\nMevcut SQLite verileri sunucuya otomatik taşınmaz (SQLite dosyası korunur).\n\nKaydedilip yeniden başlatılsın mı?"
                    : "Uygulama yerel SQLite veritabanına dönecek. Kaydedilip yeniden başlatılsın mı?"))
                return;
            y.Kaydet();
            YenidenBaslat();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task GuncellemeAyarKaydetAsync()
    {
        try
        {
            await _ayar.AyarlaAsync(AyarAnahtarlari.GuncellemeAdresi, string.IsNullOrWhiteSpace(GuncellemeAdresi) ? null : GuncellemeAdresi.Trim(), Kullanici);
            await _ayar.AyarlaAsync(AyarAnahtarlari.GuncellemeOtomatikDenetim, GuncellemeOtomatik ? "true" : "false", Kullanici);
            Mesaj = "Güncelleme ayarları kaydedildi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task GuncellemeDenetleAsync()
    {
        try
        {
            GuncellemeDurumu = "Denetleniyor…";
            _bulunan = await _guncelleme.DenetleAsync(MevcutSurum);
            GuncellemeHazir = _bulunan is not null;
            GuncellemeDurumu = _bulunan is null ? "Personel Yönetim Sistemi güncel." : $"Yeni sürüm: {_bulunan.Surum}" + (string.IsNullOrWhiteSpace(_bulunan.Notlar) ? "" : $" — {_bulunan.Notlar}");
        }
        catch (Exception ex) { GuncellemeDurumu = ex.Message; }
    }

    [RelayCommand]
    private async Task GuncellemeKurAsync()
    {
        if (_bulunan is null) return;
        if (!ArayuzYardimcisi.Onayla($"Personel Yönetim Sistemi {_bulunan.Surum} indirilip kurulacak. Kurulumdan önce otomatik yedek alınır; uygulama kapanıp yeniden açılır. Devam edilsin mi?")) return;
        try
        {
            try { await _yedek.YedekAlAsync(Kullanici, otomatik: false); } catch { /* yedek alınamazsa da kullanıcı onayıyla devam */ }
            var zip = await _guncelleme.IndirAsync(_bulunan, new Progress<double>(p => IndirmeIlerleme = p * 100));
            var betik = Path.Combine(Path.GetTempPath(), $"PayControl-guncelle-{Guid.NewGuid():N}.ps1");
            await File.WriteAllTextAsync(betik, _guncelleme.GuncelleyiciBetigi(zip, AppContext.BaseDirectory.TrimEnd('\\', '/'), Environment.ProcessId),
                new System.Text.UTF8Encoding(true));
            Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{betik}\"") { UseShellExecute = false, CreateNoWindow = true });
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex) { GuncellemeDurumu = ex.Message; }
    }
}
