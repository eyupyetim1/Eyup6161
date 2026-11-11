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
        string? veritabaniYolu = null,
        VeritabaniYapilandirmasi? yapilandirma = null)
    {
        var veriKlasoru = veritabaniYolu is null ? VeritabaniYapilandirmasi.VarsayilanKlasor : Path.GetDirectoryName(Path.GetFullPath(veritabaniYolu))!;
        Directory.CreateDirectory(veriKlasoru);
        var koruyucu = new SifreKoruyucu(veriKlasoru);
        services.AddSingleton<ISifreKoruyucu>(koruyucu);

        // Testte veya açık yol verildiğinde yapılandırma dosyası okunmaz (yanlışlıkla sunucuya bağlanmayı önler)
        var yap = yapilandirma ?? (veritabaniYolu is null ? VeritabaniYapilandirmasi.Yukle() : new VeritabaniYapilandirmasi());
        if (yap.PostgreSqlMi)
        {
            var baglanti = !string.IsNullOrWhiteSpace(yap.BaglantiMetniSifreli) ? koruyucu.Coz(yap.BaglantiMetniSifreli!) : yap.BaglantiMetni;
            if (string.IsNullOrWhiteSpace(baglanti))
                throw new InvalidOperationException("veritabani.json: PostgreSQL seçili ama bağlantı metni boş.");
            // Uygulama yerel saat (DateTime.Now) ve UTC değerlerini birlikte kullanır; 'timestamp without time zone' eşlemesi korunur.
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
            services.AddDbContext<PayControlDbContext>(opt => opt.UseNpgsql(baglanti));
            services.AddSingleton(new VeritabaniBilgisi("PostgreSql", baglanti, null));
        }
        else
        {
            // v1.1 şema (mali hareket, ödeme kontrol, dönem kapanış)
            var dbPath = veritabaniYolu ?? Path.Combine(veriKlasoru, "paycontrol-v11.db");
            // Pooling=False: test/Windows'ta Dispose sonrası dosya kilidini bırakır
            services.AddDbContext<PayControlDbContext>(opt => opt.UseSqlite($"Data Source={dbPath};Pooling=False"));
            services.AddSingleton(new VeritabaniBilgisi("Sqlite", $"Data Source={dbPath};Pooling=False", dbPath));
        }
        services.AddScoped<IPayControlDbContext>(sp => sp.GetRequiredService<PayControlDbContext>());

        services.AddScoped<IAyarServisi, AyarServisi>();
        services.AddScoped<IYedekServisi, YedekServisi>();
        services.AddScoped<IYetkiServisi, YetkiServisi>();
        services.AddScoped<ITesisServisi, TesisServisi>();
        services.AddScoped<IKesintiServisi, KesintiServisi>();
        services.AddScoped<IUyariServisi, UyariServisi>();
        services.AddScoped<IDenetimServisi, DenetimServisi>();
        services.AddScoped<IBelgeServisi, BelgeServisi>();
        services.AddScoped<IRaporServisi, RaporServisi>();
        services.AddScoped<IGostergeServisi, GostergeServisi>();
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(60) });
        services.AddSingleton<IEpostaGonderici, Bildirim.EpostaGonderici>();
        services.AddSingleton<IWhatsAppGonderici, Bildirim.WhatsAppGonderici>();
        services.AddScoped<IBildirimServisi, BildirimServisi>();
        services.AddScoped<ICikisTalimatServisi, CikisTalimatServisi>();
        services.AddScoped<IPuantajServisi, PuantajServisi>();
        services.AddScoped<IAccessAktarimServisi, Import.AccessAktarimServisi>();
        services.AddSingleton<IZamanlanmisGorevServisi, ZamanlanmisGorevServisi>();
        services.AddScoped<IGuncellemeServisi, Platform.GuncellemeServisi>();
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
            "Personel Yönetim Sistemi",
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

    /// <summary>Kayıtlı ad soyadlardaki fazla boşlukları bir kez düzeltir (her düzeltme denetim kaydına yazılır).</summary>
    public static async Task<int> IsimleriDuzeltAsync(PayControlDbContext db, CancellationToken ct = default)
    {
        var n = 0;
        foreach (var p in await db.Personeller.ToListAsync(ct))
        {
            if (!IsimKurallari.DuzeltmeGerekirMi(p.AdSoyad)) continue;
            var eski = p.AdSoyad;
            p.AdSoyad = IsimKurallari.Duzelt(p.AdSoyad)!;
            db.DenetimKayitlari.Add(new PayControl.Domain.Entities.DenetimKaydi
            {
                PersonelId = p.Id, VarlikTuru = "Personel", VarlikId = p.Id, Islem = "Guncelleme", AlanAdi = "AdSoyad",
                EskiDeger = eski, YeniDeger = p.AdSoyad, DegisiklikNedeni = "Ad soyaddaki fazla boşluk otomatik düzeltildi.", KullaniciAdi = "sistem"
            });
            n++;
        }
        foreach (var a in await db.BildirimAlicilari.ToListAsync(ct))
            if (IsimKurallari.DuzeltmeGerekirMi(a.AdSoyad)) { a.AdSoyad = IsimKurallari.Duzelt(a.AdSoyad)!; n++; }
        if (n > 0) await db.SaveChangesAsync(ct);
        return n;
    }

    public static async Task InitializeDatabaseAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayControlDbContext>();
        var yeniOlustu = await db.Database.EnsureCreatedAsync();
        if (!yeniOlustu)
            await SemaYukseltici.YukseltAsync(db);
        await IsimleriDuzeltAsync(db);
        await PuantajServisi.VarsayilanTatilleriYukleAsync(db);
        var kimlik = scope.ServiceProvider.GetRequiredService<IKimlikServisi>();
        await kimlik.EnsureAdminAsync();
    }
}
