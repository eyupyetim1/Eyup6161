using Microsoft.Extensions.DependencyInjection;
using PayControl.Application.Interfaces;

namespace PayControl.Application.Services;

public sealed class ZamanlanmisGorevServisi : IZamanlanmisGorevServisi
{
    private static readonly SemaphoreSlim Kilit = new(1, 1);
    private readonly IServiceScopeFactory _scopes;

    public ZamanlanmisGorevServisi(IServiceScopeFactory scopes) => _scopes = scopes;

    public async Task<GorevCalismaSonucu> CalistirAsync(DateTime simdi, CancellationToken ct = default)
    {
        // Aynı süreçte üst üste binen çalışmaları engelle (zamanlayıcı + elle tetikleme)
        if (!await Kilit.WaitAsync(0, ct))
            return new GorevCalismaSonucu { Hatalar = ["Önceki çalışma sürüyor."] };
        try
        {
            var hatalar = new List<string>();
            var yedek = false;
            int kural = 0, yeniden = 0, hatirlatma = 0;

            // Her adım kendi kapsamında: bir adımın DbContext hatası diğerini etkilemez
            await AdimAsync("Otomatik yedek", async sp => yedek = await sp.GetRequiredService<IYedekServisi>().OtomatikYedekGerekirseAsync(ct) is not null);
            await AdimAsync("Zamanlanmış bildirimler", async sp => kural = await sp.GetRequiredService<IBildirimServisi>().ZamanlanmisKurallariCalistirAsync(simdi, ct));
            await AdimAsync("Çıkış talimatı hatırlatma", async sp => hatirlatma = await sp.GetRequiredService<ICikisTalimatServisi>().GecikenleriHatirlatAsync(simdi, ct));
            await AdimAsync("Bildirim yeniden deneme", async sp => yeniden = await sp.GetRequiredService<IBildirimServisi>().BasarisizlariYenidenDeneAsync(ct));

            return new GorevCalismaSonucu { YedekAlindi = yedek, CalisanKural = kural, YenidenDenenen = yeniden, CikisHatirlatma = hatirlatma, Hatalar = hatalar };

            async Task AdimAsync(string ad, Func<IServiceProvider, Task> is_)
            {
                try
                {
                    using var scope = _scopes.CreateScope();
                    await is_(scope.ServiceProvider);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { hatalar.Add($"{ad}: {ex.Message}"); }
            }
        }
        finally { Kilit.Release(); }
    }
}
