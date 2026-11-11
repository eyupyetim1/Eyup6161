using System.Security.Cryptography;
using System.Text.Json;
using PayControl.Application.Interfaces;

namespace PayControl.Tests.Modules;

public class GuncellemeTestleri : TestOrtami
{
    private async Task<(string Bildirim, byte[] Paket)> HazirlaAsync(string surum, string? sha = null)
    {
        Directory.CreateDirectory(Klasor);
        var paket = new byte[] { 0x50, 0x4B, 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(Path.Combine(Klasor, "paket.zip"), paket);
        var bildirim = Path.Combine(Klasor, "guncelleme.json");
        await File.WriteAllTextAsync(bildirim, JsonSerializer.Serialize(new
        {
            surum, adres = "paket.zip", sha256 = sha ?? Convert.ToHexString(SHA256.HashData(paket)), notlar = "Yeni raporlar"
        }));
        await S<IAyarServisi>().AyarlaAsync(AyarAnahtarlari.GuncellemeAdresi, bildirim, Yonetici);
        return (bildirim, paket);
    }

    [Fact]
    public async Task Ag_paylasimindaki_yeni_surum_bulunur_indirilir_ve_ozet_dogrulanir()
    {
        var (_, paket) = await HazirlaAsync("1.4.0");
        var srv = S<IGuncellemeServisi>();
        Assert.Null(await srv.DenetleAsync(new Version(1, 4, 0)));
        Assert.Null(await srv.DenetleAsync(new Version(1, 5)));
        var b = await srv.DenetleAsync(new Version(1, 3, 0, 0));
        Assert.NotNull(b);
        Assert.Equal(Path.Combine(Klasor, "paket.zip"), b!.Adres);
        var zip = await srv.IndirAsync(b);
        Assert.Equal(paket, await File.ReadAllBytesAsync(zip));
        var betik = srv.GuncelleyiciBetigi(zip, @"C:\Kullanici's\PayControl", 4242);
        Assert.Contains("Wait-Process -Id 4242", betik);
        Assert.Contains("'C:\\Kullanici''s\\PayControl'", betik);
        File.Delete(zip);
    }

    [Fact]
    public async Task Ozet_uyusmazsa_paket_kurulmaz_ozetsiz_bildirim_reddedilir()
    {
        await HazirlaAsync("2.0.0", new string('A', 64));
        var srv = S<IGuncellemeServisi>();
        var b = await srv.DenetleAsync(new Version(1, 3));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => srv.IndirAsync(b!));
        Assert.Contains("SHA-256", ex.Message);

        await HazirlaAsync("2.0.0", "");
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.DenetleAsync(new Version(1, 3)));
    }
}
