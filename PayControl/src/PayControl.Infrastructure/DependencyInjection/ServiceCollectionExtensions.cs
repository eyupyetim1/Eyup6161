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

        services.AddDbContext<PayControlDbContext>(opt =>
            opt.UseSqlite($"Data Source={dbPath}"));
        services.AddScoped<IPayControlDbContext>(sp => sp.GetRequiredService<PayControlDbContext>());

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
        await db.Database.EnsureCreatedAsync();
        var kimlik = scope.ServiceProvider.GetRequiredService<IKimlikServisi>();
        await kimlik.EnsureAdminAsync();
    }
}
