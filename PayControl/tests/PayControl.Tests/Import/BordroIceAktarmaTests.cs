using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayControl.Application.Interfaces;
using PayControl.Infrastructure.Data;
using PayControl.Infrastructure.DependencyInjection;
using PayControl.Infrastructure.Import;

namespace PayControl.Tests.Import;

public class BordroIceAktarmaTests : IAsyncLifetime
{
    private ServiceProvider _sp = null!;
    private string _dbPath = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"paycontrol-test-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddPayControlInfrastructure(_dbPath);
        _sp = services.BuildServiceProvider();
        await ServiceCollectionExtensions.InitializeDatabaseAsync(_sp);
    }

    public Task DisposeAsync() => TestDbCleanup.DisposeProviderAndDbAsync(_sp, _dbPath);

    [Fact]
    public void Baslik_esleme_kullanici_kolonlarini_tanir()
    {
        var basliklar = new[]
        {
            "T.C. KİMLİK NO", "ADI SOYADI", "İŞYERİ", "GÖREVİ", "MAAŞ (Net/Brüt)",
            "GİRİŞ TARİHİ", "ÇIKIŞ TARİHİ", "GÜN", "NET İSTİHKAK", "IBAN NO",
            "AVANS", "İCRA", "OTO.KAT.BES", "TRAFİK CEZASI", "AİLE", "İHBAR TAZMİNATI", "YEMEK", "YOL"
        };

        var sonuc = BordroBaslikEsleyici.Esle(basliklar);
        Assert.Contains("TcKimlikNo", sonuc.KolonAlanlari.Values);
        Assert.Contains("NetIstihkak", sonuc.KolonAlanlari.Values);
        Assert.Contains("Avans", sonuc.KolonAlanlari.Values);
        Assert.Empty(sonuc.BelirsizBasliklar);
    }

    [Fact]
    public async Task Mukerrer_excel_yukleme_engellenir()
    {
        var path = CreateSampleExcel();
        try
        {
            var servis = _sp.GetRequiredService<IBordroIceAktarmaServisi>();

            await using (var fs1 = File.OpenRead(path))
            {
                var r1 = await servis.AktarAsync(fs1, Path.GetFileName(path), 2026, 10, "tester");
                Assert.False(r1.MukerrerEngellendi);
                Assert.True(r1.BasariliSatir >= 1);
            }

            await using (var fs2 = File.OpenRead(path))
            {
                var r2 = await servis.AktarAsync(fs2, Path.GetFileName(path), 2026, 10, "tester");
                Assert.True(r2.MukerrerEngellendi);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Ice_aktarma_geri_alinabilir()
    {
        var path = CreateSampleExcel();
        try
        {
            var servis = _sp.GetRequiredService<IBordroIceAktarmaServisi>();
            Guid id;
            await using (var fs = File.OpenRead(path))
            {
                var r = await servis.AktarAsync(fs, Path.GetFileName(path), 2026, 9, "tester");
                id = r.IceAktarmaId;
            }

            var ok = await servis.GeriAlAsync(id, "tester");
            Assert.True(ok);

            var db = _sp.GetRequiredService<PayControlDbContext>();
            Assert.Equal(0, await db.MaasDonemKayitlari.CountAsync(m => m.IceAktarmaId == id));
            Assert.True(await db.IceAktarmalar.AnyAsync(i => i.Id == id && i.GeriAlindi));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Maas_donemleri_ayri_korunur()
    {
        var pathEylul = CreateSampleExcel(maas: 30000, net: 30000, ayEtiket: "eylul");
        var pathEkim = CreateSampleExcel(maas: 35000, net: 35000, ayEtiket: "ekim");
        try
        {
            var servis = _sp.GetRequiredService<IBordroIceAktarmaServisi>();
            await using (var fs = File.OpenRead(pathEylul))
                await servis.AktarAsync(fs, Path.GetFileName(pathEylul), 2026, 9, "tester");
            await using (var fs = File.OpenRead(pathEkim))
                await servis.AktarAsync(fs, Path.GetFileName(pathEkim), 2026, 10, "tester");

            var db = _sp.GetRequiredService<PayControlDbContext>();
            // Her dosyada 2 personel satırı var → dönem başına 2 kayıt
            var eylul = await db.MaasDonemKayitlari.Where(m => m.DonemAy == 9).ToListAsync();
            var ekim = await db.MaasDonemKayitlari.Where(m => m.DonemAy == 10).ToListAsync();
            Assert.Equal(2, eylul.Count);
            Assert.Equal(2, ekim.Count);
            Assert.All(eylul, k => Assert.Equal(30000m, k.MaasTutar));
            Assert.All(ekim, k => Assert.Equal(35000m, k.MaasTutar));
            // Eylül kayıtları Ekim import'undan etkilenmemiş olmalı
            Assert.All(eylul, k => Assert.NotEqual(35000m, k.MaasTutar));
        }
        finally
        {
            File.Delete(pathEylul);
            File.Delete(pathEkim);
        }
    }

    private static string CreateSampleExcel(decimal maas = 30000, decimal net = 25000, string ayEtiket = "ornek")
    {
        var path = Path.Combine(Path.GetTempPath(), $"bordro-{ayEtiket}-{Guid.NewGuid():N}.xlsx");
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Bordro");
        var headers = new[]
        {
            "T.C. KİMLİK NO", "ADI SOYADI", "İŞYERİ", "GÖREVİ", "MAAŞ (Net/Brüt)",
            "GİRİŞ TARİHİ", "ÇIKIŞ TARİHİ", "GÜN", "NET İSTİHKAK", "IBAN NO",
            "AVANS", "İCRA", "OTO.KAT.BES", "TRAFİK CEZASI", "AİLE", "İHBAR TAZMİNATI", "YEMEK", "YOL"
        };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Cell(2, 1).Value = "10000000146";
        ws.Cell(2, 2).Value = "Test Personel";
        ws.Cell(2, 3).Value = "Tesis A";
        ws.Cell(2, 4).Value = "Operatör";
        ws.Cell(2, 5).Value = (double)maas;
        ws.Cell(2, 6).Value = new DateTime(2024, 3, 1);
        ws.Cell(2, 8).Value = 30;
        ws.Cell(2, 9).Value = (double)net;
        ws.Cell(2, 10).Value = "TR330006100519786457841326";
        ws.Cell(2, 11).Value = 5000;
        ws.Cell(2, 12).Value = 0;
        ws.Cell(2, 13).Value = 500;
        ws.Cell(2, 15).Value = 1000;
        ws.Cell(2, 17).Value = 1500;
        ws.Cell(2, 18).Value = 800;

        // Aynı isimli ikinci personel — farklı TC
        ws.Cell(3, 1).Value = "10000000154";
        ws.Cell(3, 2).Value = "Test Personel";
        ws.Cell(3, 3).Value = "Tesis B";
        ws.Cell(3, 4).Value = "Teknisyen";
        ws.Cell(3, 5).Value = (double)maas;
        ws.Cell(3, 8).Value = 30;
        ws.Cell(3, 9).Value = (double)net;
        ws.Cell(3, 10).Value = "TR320010009999999999999999";

        wb.SaveAs(path);
        return path;
    }
}
