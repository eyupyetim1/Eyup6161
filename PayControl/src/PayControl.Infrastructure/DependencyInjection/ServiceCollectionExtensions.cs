using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayControl.Application.Interfaces;
using PayControl.Application.Services;
using PayControl.Domain.Services;
using PayControl.Infrastructure.Data;
using PayControl.Infrastructure.Import;
using PayControl.Infrastructure.Security;
using Serilog;

namespace PayControl.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPayControlInfrastructure(
        this IServiceCollection services,
        string? veritabaniYolu = null)
    {
        // v1.1 şema (mali hareket, ödeme kontrol, dönem kapanış)
        var dbPath = veritabaniYolu ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PayControl",
            "paycontrol-v11.db");

        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        // Pooling=False: test/Windows'ta Dispose sonrası dosya kilidini bırakır
        services.AddDbContext<PayControlDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath};Pooling=False"));
        services.AddScoped<IPayControlDbContext>(sp => sp.GetRequiredService<PayControlDbContext>());

        services.AddSingleton(new VeritabaniBilgisi("Sqlite", $"Data Source={dbPath};Pooling=False", dbPath));
        services.AddSingleton<ISifreKoruyucu>(new SifreKoruyucu(Path.GetDirectoryName(dbPath)!));
        services.AddScoped<IAyarServisi, AyarServisi>();
        services.AddScoped<IYedekServisi, YedekServisi>();
        services.AddScoped<IYetkiServisi, YetkiServisi>();
        services.AddScoped<ITesisServisi, TesisServisi>();
        services.AddScoped<IKesintiServisi, KesintiServisi>();
        services.AddScoped<IUyariServisi, UyariServisi>();
        services.AddScoped<IDenetimServisi, DenetimServisi>();
        services.AddScoped<IBelgeServisi, BelgeServisi>();
        services.AddScoped<IMaliHareketServisi, MaliHareketServisi>();
        services.AddScoped<IPersonelServisi, PersonelServisi>();
        services.AddScoped<IIstenCikisServisi, IstenCikisAppServisi>();
        services.AddScoped<IBordroIceAktarmaServisi, BordroIceAktarmaServisi>();
        services.AddScoped<IKimlikServisi, KimlikServisi>();
        services.AddScoped<IHesapEkstresiServisi, HesapEkstresiServisi>();
        services.AddScoped<IAvansServisi, AvansServisi>();
        services.AddScoped<IDonemKapanisServisi, DonemKapanisServisi>();
        services.AddScoped<IOdemeOncesiKontrolServisi, OdemeOncesiKontrolServisi>();
        services.AddScoped<IBankaOdemeServisi, BankaOdemeServisi>();
        services.AddSingleton<ICikisDenetimServisi, CikisDenetimServisi>();
        services.AddSingleton<IHakEdisHesaplamaServisi, HakEdisHesaplamaServisi>();

        return services;
    }

    public static void ConfigurePayControlLogging(string? logKlasoru = null)
    {
        var dir = logKlasoru ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PayControl",
            "logs");
        Directory.CreateDirectory(dir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(dir, "paycontrol-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();
    }

    public static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayControlDbContext>();
        var yeniOlustu = await db.Database.EnsureCreatedAsync();
        if (!yeniOlustu)
            await SemaYukseltici.YukseltAsync(db);
        var kimlik = scope.ServiceProvider.GetRequiredService<IKimlikServisi>();
        await kimlik.EnsureAdminAsync();
    }
}
