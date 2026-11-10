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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("PayControl kapatılıyor");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
