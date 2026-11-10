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

        _ = BaslatAsync();
    }

    private async Task BaslatAsync()
    {
        try
        {
            ServiceCollectionExtensions.ConfigurePayControlLogging();
            Log.Information("PayControl başlatılıyor");

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

            Services = services.BuildServiceProvider();
            await ServiceCollectionExtensions.InitializeDatabaseAsync(Services);

            var login = Services.GetRequiredService<LoginWindow>();
            login.Show();
        }
        catch (Exception ex)
        {
            KaydetVeGoster(ex);
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
                "PayControl açılamadı.\n\n" +
                ex.Message +
                "\n\nDetay masaüstüne yazıldı:\nPayControl-HATA.txt",
                "PayControl Hata",
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
            Log.Information("PayControl kapatılıyor");
            Log.CloseAndFlush();
        }
        catch { /* ignore */ }
        base.OnExit(e);
    }
}
