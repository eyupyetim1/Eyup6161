using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PayControl.Desktop.Services;
using PayControl.Desktop.ViewModels;
using PayControl.Desktop.Views;
using PayControl.Infrastructure.DependencyInjection;
using Serilog;

namespace PayControl.Desktop;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Tutar/tarih metinleri (ekstre, PDF, kontrol gerekçeleri) Türkçe biçimde üretilir.
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = tr;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = tr;
        System.Threading.Thread.CurrentThread.CurrentCulture = tr;
        System.Threading.Thread.CurrentThread.CurrentUICulture = tr;
        // WPF bağlamaları (StringFormat=N2, tarih) varsayılan en-US yerine Türkçe biçim kullanır.
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(tr.IetfLanguageTag)));

        DispatcherUnhandledException += (_, args) =>
        {
            KaydetVeGoster(args.Exception);
            args.Handled = true;
            Shutdown(1);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                KaydetVeGoster(ex);
        };

        var i = Array.IndexOf(e.Args, "--ekran-goruntusu");
        if (i >= 0)
        {
            var klasor = i + 1 < e.Args.Length ? e.Args[i + 1] : Path.Combine(Environment.CurrentDirectory, "ekran-goruntuleri");
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = EkranGoruntusuAsync(klasor);
            return;
        }

        var ac = Array.IndexOf(e.Args, "--access-csv");
        if (ac >= 0 && ac + 2 < e.Args.Length)
        {
            // Tanılama: Access dosyasını CSV'ye çıkarır (veri aktarımının Windows adımını doğrulamak için)
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = AccessCsvAsync(e.Args[ac + 1], e.Args[ac + 2]);
            return;
        }

        if (e.Args.Contains("--zamanlanmis-gorevler"))
        {
            // Windows Görev Zamanlayıcısı modu: pencere açmadan arka plan işlerini çalıştırır ve çıkar.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            _ = ZamanlanmisGorevlerAsync();
            return;
        }

        // Ekrana sığmayan pencereler (küçük dizüstü ekranı, %125-150 ölçekleme) ekranı kaplayarak açılır
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) => { if (s is Window w) ArayuzYardimcisi.EkranaSigdir(w); }));

        _ = BaslatAsync();
    }

    /// <summary>1.3 ekranları: rapor merkezi, bildirimler, uyarı merkezi, denetim, tesisler, yönetim, şifre.</summary>
    private static void EkEkranlariKaydet(ServiceCollection services)
    {
        services.AddTransient<RaporMerkeziViewModel>();
        services.AddTransient<BildirimlerViewModel>();
        services.AddTransient<UyariMerkeziViewModel>();
        services.AddTransient<DenetimGecmisiViewModel>();
        services.AddTransient<TesislerViewModel>();
        services.AddTransient<YonetimViewModel>();
        services.AddTransient<SifreDegistirViewModel>();
        services.AddTransient<CikisTakibiViewModel>();
        services.AddTransient<PuantajViewModel>();
        services.AddTransient<PuantajWindow>();
        services.AddTransient<NakilViewModel>();
        services.AddTransient<NakilWindow>();
        services.AddTransient<VeriAktarimiViewModel>();
        services.AddTransient<VeriAktarimiWindow>();
        services.AddTransient<ModulVeriViewModel>();
        services.AddTransient<ModulVeriWindow>();
        services.AddTransient<CikisTakibiWindow>();
        services.AddTransient<IzinBakiyeViewModel>();
        services.AddTransient<IzinBakiyeWindow>();
        services.AddTransient<RaporMerkeziWindow>();
        services.AddTransient<BildirimlerWindow>();
        services.AddTransient<UyariMerkeziWindow>();
        services.AddTransient<DenetimGecmisiWindow>();
        services.AddTransient<TesislerWindow>();
        services.AddTransient<YonetimWindow>();
    }

    /// <summary>İlk açılış mesajı (başlangıç verisi yüklendiyse ana ekranda gösterilir).</summary>
    public static string? AcilisMesaji { get; private set; }

    /// <summary>
    /// Kurulumla gelen hazır veri paketi (baslangic-verisi.zip: eski Access sisteminin tabloları) ilk açılışta,
    /// programda hiç personel yokken bir kez aktarılır.
    /// </summary>
    private static async Task BaslangicVerisiniYukleAsync()
    {
        var paket = Path.Combine(AppContext.BaseDirectory, "baslangic-verisi.zip");
        if (!File.Exists(paket)) return;
        try
        {
            using var scope = Services.CreateScope();
            var sp = scope.ServiceProvider;
            var ayar = sp.GetRequiredService<PayControl.Application.Interfaces.IAyarServisi>();
            var ozet = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(paket)));
            if (await ayar.GetirAsync("BaslangicVerisi.Aktarilan") == ozet) return;
            var db = sp.GetRequiredService<PayControl.Infrastructure.Data.PayControlDbContext>();
            if (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.Personeller))
            {
                Log.Information("Başlangıç verisi atlandı: programda zaten personel var (Veri aktarımı ekranından elle aktarılabilir)");
                return;
            }
            var sonuc = await sp.GetRequiredService<PayControl.Application.Interfaces.IAccessAktarimServisi>().IceAktarAsync(paket, "admin");
            await ayar.AyarlaAsync("BaslangicVerisi.Aktarilan", ozet, "admin");
            AcilisMesaji = "Eski sistemdeki verileriniz yüklendi: " + sonuc;
            Log.Information("Başlangıç verisi aktarıldı: {Sonuc}", sonuc.ToString());
        }
        catch (Exception ex)
        {
            AcilisMesaji = "Hazır veri paketi yüklenemedi: " + ex.Message + " (Yönetim → Veri aktarımı ile tekrar deneyin)";
            Log.Error(ex, "Başlangıç verisi aktarılamadı");
        }
    }

    private System.Windows.Threading.DispatcherTimer? _zamanlayici;

    /// <summary>Uygulama açıkken her 5 dakikada bir otomatik yedek / zamanlanmış bildirim / yeniden deneme çalıştırır.</summary>
    private void ZamanlayiciyiBaslat()
    {
        _zamanlayici = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMinutes(5) };
        async void Calis()
        {
            try
            {
                var sonuc = await Task.Run(() => Services.GetRequiredService<PayControl.Application.Interfaces.IZamanlanmisGorevServisi>().CalistirAsync(DateTime.Now));
                if (sonuc.YedekAlindi || sonuc.CalisanKural > 0 || sonuc.YenidenDenenen > 0 || sonuc.CikisHatirlatma > 0 || sonuc.Hatalar.Count > 0)
                    Log.Information("Zamanlanmış görevler: {Sonuc}", sonuc.ToString());
            }
            catch (Exception ex) { Log.Warning(ex, "Zamanlanmış görev hatası"); }
        }
        _zamanlayici.Tick += (_, _) => Calis();
        _zamanlayici.Start();
        Calis();
    }

    private async Task AccessCsvAsync(string accdb, string hedef)
    {
        var kod = 0;
        try
        {
            var srv = new PayControl.Infrastructure.Import.AccessAktarimServisi(null!, null!);
            var klasor = await srv.CsvyeCikarAsync(accdb);
            Directory.CreateDirectory(hedef);
            foreach (var f in Directory.GetFiles(klasor, "*.csv")) File.Copy(f, Path.Combine(hedef, Path.GetFileName(f)), true);
            File.WriteAllText(Path.Combine(hedef, "TAMAM.txt"), "ok");
        }
        catch (Exception ex)
        {
            try { Directory.CreateDirectory(hedef); File.WriteAllText(Path.Combine(hedef, "HATA.txt"), ex.ToString()); } catch { /* yoksay */ }
            kod = 1;
        }
        Shutdown(kod);
    }

    private async Task ZamanlanmisGorevlerAsync()
    {
        var kod = 0;
        try
        {
            ServiceCollectionExtensions.ConfigurePayControlLogging();
            var services = new ServiceCollection();
            services.AddPayControlInfrastructure();
            Services = services.BuildServiceProvider();
            await ServiceCollectionExtensions.InitializeDatabaseAsync(Services);
            var sonuc = await Services.GetRequiredService<PayControl.Application.Interfaces.IZamanlanmisGorevServisi>().CalistirAsync(DateTime.Now);
            Log.Information("Zamanlanmış görevler (Görev Zamanlayıcısı): {Sonuc}", sonuc.ToString());
            kod = sonuc.Hatalar.Count == 0 ? 0 : 2;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Zamanlanmış görevler çalıştırılamadı");
            kod = 1;
        }
        finally
        {
            Log.CloseAndFlush();
            Shutdown(kod);
        }
    }

    private async Task BaslatAsync()
    {
        try
        {
            ServiceCollectionExtensions.ConfigurePayControlLogging();
            Log.Information("Personel Yönetim Sistemi başlatılıyor");

            var services = new ServiceCollection();
            services.AddPayControlInfrastructure();
            services.AddSingleton<OturumServisi>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<PersonelDetayViewModel>();
            services.AddTransient<IstenCikisViewModel>();
            services.AddTransient<BordroImportViewModel>();
            services.AddTransient<OdemeKontrolViewModel>();
            services.AddTransient<DonemKapanisViewModel>();
            services.AddTransient<LoginWindow>();
            services.AddTransient<MainWindow>();
            services.AddTransient<PersonelDetayWindow>();
            services.AddTransient<IstenCikisWindow>();
            services.AddTransient<BordroImportWindow>();
            services.AddTransient<OdemeKontrolWindow>();
            services.AddTransient<DonemKapanisWindow>();
            EkEkranlariKaydet(services);

            Services = services.BuildServiceProvider();
            await ServiceCollectionExtensions.InitializeDatabaseAsync(Services);
            await BaslangicVerisiniYukleAsync();

            var login = Services.GetRequiredService<LoginWindow>();
            login.Show();
            ZamanlayiciyiBaslat();
        }
        catch (Exception ex)
        {
            KaydetVeGoster(ex);
            Shutdown(1);
        }
    }

    /// <summary>Geçici veritabanı + demo veri ile pencerelerin PNG görüntülerini üretir (kullanıcı verisine dokunmaz).</summary>
    private async Task EkranGoruntusuAsync(string klasor)
    {
        try
        {
            Directory.CreateDirectory(klasor);
            var db = Path.Combine(Path.GetTempPath(), $"paycontrol-demo-{Guid.NewGuid():N}.db");
            var services = new ServiceCollection();
            services.AddPayControlInfrastructure(db);
            services.AddSingleton<OturumServisi>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<PersonelDetayViewModel>();
            services.AddTransient<IstenCikisViewModel>();
            services.AddTransient<BordroImportViewModel>();
            services.AddTransient<OdemeKontrolViewModel>();
            services.AddTransient<DonemKapanisViewModel>();
            services.AddTransient<LoginWindow>();
            EkEkranlariKaydet(services);
            Services = services.BuildServiceProvider();
            await ServiceCollectionExtensions.InitializeDatabaseAsync(Services);
            await EkranGoruntusuModu.CalistirAsync(Services, klasor);
            File.WriteAllText(Path.Combine(klasor, "TAMAM.txt"), DateTime.Now.ToString("O"));
            Shutdown(0);
        }
        catch (Exception ex)
        {
            try { File.WriteAllText(Path.Combine(klasor, "HATA.txt"), ex.ToString(), Encoding.UTF8); } catch { /* yoksay */ }
            Shutdown(1);
        }
    }

    private static void KaydetVeGoster(Exception ex)
    {
        try
        {
            var masaustu = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var yol = Path.Combine(masaustu, "PayControl-HATA.txt");
            var sb = new StringBuilder();
            sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine(ex.ToString());
            File.WriteAllText(yol, sb.ToString(), Encoding.UTF8);

            MessageBox.Show(
                "Personel Yönetim Sistemi açılamadı.\n\n" +
                ex.Message +
                "\n\nDetay masaüstüne yazıldı:\nPayControl-HATA.txt",
                "Personel Yönetim Sistemi Hata",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch
        {
            // yut
        }

        try { Log.Fatal(ex, "Kritik hata"); Log.CloseAndFlush(); } catch { /* ignore */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.Information("Personel Yönetim Sistemi kapatılıyor");
            Log.CloseAndFlush();
        }
        catch { /* ignore */ }
        base.OnExit(e);
    }
}
