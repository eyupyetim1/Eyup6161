using System.Text;
using ClosedXML.Excel;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Enums;

namespace PayControl.Tests.Modules;

public class RaporTestleri : TestOrtami
{
    private async Task<(PayControl.Domain.Entities.Personel Ist, PayControl.Domain.Entities.Personel Ank, PayControl.Domain.Entities.Personel Izm)> VeriAsync()
    {
        var ist = await PersonelAsync("Rapor İstanbul", 30000, 80, new DateOnly(2025, 1, 1), isyeri: "İstanbul Depo");
        var ank = await PersonelAsync("Rapor Ankara", 28000, 81, new DateOnly(2025, 1, 1), isyeri: "Ankara Depo");
        var izm = await PersonelAsync("Rapor İzmir", 26000, 82, new DateOnly(2026, 9, 1), isyeri: "İzmir Aktarma");
        await S<IAvansServisi>().VerAsync(ist.Id, new DateOnly(2026, 8, 15), 6000m, 2, 3000m, null, Muhasebe);
        await BordroYukleAsync(2026, 9, new BordroSatir(ist, 21000m, Avans: 3000m, Icra: 1000m), new BordroSatir(ank, 20000m), new BordroSatir(izm, 18000m));
        await BordroYukleAsync(2026, 10, new BordroSatir(ist, 24000m), new BordroSatir(ank, 20000m, Gun: 20), new BordroSatir(izm, 18000m));
        var banka = S<IBankaOdemeServisi>();
        await banka.KaydetAsync(ist.Id, new DateOnly(2026, 10, 5), 21000m, 2026, 9, ist.Iban, "R1", Muhasebe);
        await banka.KaydetAsync(ank.Id, new DateOnly(2026, 10, 5), 18000m, 2026, 9, ank.Iban, "R2", Muhasebe); // 2.000 eksik
        await banka.KasaOdemesiKaydetAsync(izm.Id, new DateOnly(2026, 10, 6), 18000m, 2026, 9, "Kasadan", "K1", Muhasebe);
        await S<IIstenCikisServisi>().OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = ank.Id, FiiliSonCalismaTarihi = new DateOnly(2026, 10, 20), IstenCikisTarihi = new DateOnly(2026, 10, 20), SonBordroDonemYil = 2026, SonBordroDonemAy = 10
        }, Muhasebe);
        await S<IPersonelServisi>().GuncelleAsync(ist.Id, new PersonelGuncelleDto { AdSoyad = ist.AdSoyad, GuncelMaas = 33000m, Iban = ist.Iban, Isyeri = ist.Isyeri, Sirket = ist.Sirket }, Muhasebe, "Terfi");
        var donem = await S<IDonemKapanisServisi>().GetOrCreateAsync(2026, 9);
        await S<IDonemKapanisServisi>().MutabakatYapAsync(donem.Id, Muhasebe);
        return (ist, ank, izm);
    }

    private static RaporParametreleri Aralik(string? tesis = null) => new()
    {
        BaslangicYil = 2026, BaslangicAy = 9, BitisYil = 2026, BitisAy = 10, Tesis = tesis
    };

    [Fact]
    public async Task Tum_rapor_turleri_uretilir_ve_uc_bicimde_disa_aktarilir()
    {
        await VeriAsync();
        var srv = S<IRaporServisi>();
        Assert.Equal(Enum.GetValues<RaporTuru>().Length, srv.Tanimlar.Count);

        foreach (var tanim in srv.Tanimlar)
        {
            var r = await srv.OlusturAsync(tanim.Tur, Aralik());
            Assert.Equal(tanim.Ad, r.Baslik);
            Assert.NotEmpty(r.Kolonlar);
            Assert.All(r.Satirlar, s => Assert.Equal(r.Kolonlar.Count, s.Length));

            var xlsx = srv.DisaAktar(r, RaporBicimi.Excel);
            using (var wb = new XLWorkbook(new MemoryStream(xlsx)))
                Assert.Contains("PayControl", wb.Worksheets.First().Cell(1, 1).GetString());
            var pdf = srv.DisaAktar(r, RaporBicimi.Pdf);
            Assert.StartsWith("%PDF-1.4", Encoding.Latin1.GetString(pdf, 0, 8));
            var csv = srv.DisaAktar(r, RaporBicimi.Csv);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, csv[..3]);
            Assert.EndsWith(".csv", srv.DosyaAdi(r, RaporBicimi.Csv));
        }
    }

    [Fact]
    public async Task Rapor_rakamlari_kaynak_kayitlarla_uyusur_ve_tesise_gore_suzulur()
    {
        var (ist, ank, izm) = await VeriAsync();
        var srv = S<IRaporServisi>();

        var ozet = await srv.OlusturAsync(RaporTuru.DonemBordroOzeti, Aralik());
        Assert.Equal(6, ozet.Satirlar.Count); // 2 dönem × 3 tesis
        Assert.Equal(21000m + 20000m + 18000m + 24000m + 20000m + 18000m, (decimal)ozet.Toplam![4]!);
        Assert.Equal(3000m, (decimal)ozet.Toplam[5]!); // avans kesintisi
        Assert.Equal(1000m, (decimal)ozet.Toplam[6]!); // icra
        Assert.Equal(1, Convert.ToInt32(ozet.Toplam[9])); // eksik günlü satır

        var ankara = await srv.OlusturAsync(RaporTuru.DonemBordroOzeti, Aralik("Ankara Depo"));
        Assert.All(ankara.Satirlar, s => Assert.Equal("Ankara Depo", s[1]));
        Assert.Equal(40000m, (decimal)ankara.Toplam![4]!);

        var karsilastirma = await srv.OlusturAsync(RaporTuru.TesisKarsilastirma, RaporParametreleri.TekDonem(2026, 9));
        var ankSatir = karsilastirma.Satirlar.Single(s => (string)s[0]! == "Ankara Depo");
        Assert.Equal(-2000m, (decimal)ankSatir[4]!);
        Assert.Equal(1, Convert.ToInt32(ankSatir[8])); // açık mutabakat farkı

        var trend = await srv.OlusturAsync(RaporTuru.PersonelMaliyetTrendi, Aralik());
        Assert.Equal(4, trend.Kolonlar.Count); // tesis + 2 ay + toplam

        var yas = await srv.OlusturAsync(RaporTuru.OdenmemisHakEdisYaslandirma, Aralik());
        var ankYas = yas.Satirlar.Single(s => (string)s[0]! == ank.AdSoyad);
        Assert.Equal(22000m, (decimal)ankYas[7]!); // Eylül 2.000 + Ekim 20.000 ödenmemiş

        var giris = await srv.OlusturAsync(RaporTuru.GirisCikisDevirHizi, Aralik());
        Assert.Equal(1, Convert.ToInt32(giris.Satirlar.Single(s => (string)s[0]! == "İzmir Aktarma" && (string)s[1]! == "2026-09")[3]));
        Assert.Equal(1, Convert.ToInt32(giris.Satirlar.Single(s => (string)s[0]! == "Ankara Depo" && (string)s[1]! == "2026-10")[4]));

        var odemeler = await srv.OlusturAsync(RaporTuru.BankaKasaOdemeDokumu, Aralik());
        Assert.Equal(57000m, (decimal)odemeler.Toplam![7]!);
        Assert.Contains(odemeler.Satirlar, s => (string)s[4]! == "Kasa");

        var maas = await srv.OlusturAsync(RaporTuru.MaasDegisiklikleri, new RaporParametreleri
        {
            BaslangicYil = DateTime.Today.Year, BaslangicAy = 1, BitisYil = DateTime.Today.Year, BitisAy = DateTime.Today.Month
        });
        var m = maas.Satirlar.Single();
        Assert.Equal(3000m, (decimal)m[5]!);
        Assert.Equal("Terfi", m[7]);

        var sgk = await srv.OlusturAsync(RaporTuru.SgkCikisDurumu, Aralik());
        Assert.Equal("Belirsiz", sgk.Satirlar.Single()[4]);

        var avans = await srv.OlusturAsync(RaporTuru.AvansDurumu, Aralik());
        Assert.Equal(3000m, (decimal)avans.Toplam![7]!);

        var bakiye = await srv.OlusturAsync(RaporTuru.PersonelBakiyeListesi, Aralik("İzmir Aktarma"));
        Assert.Equal(izm.AdSoyad, bakiye.Satirlar.Single()[0]);
        _ = ist;
    }

    [Fact]
    public async Task Gosterge_paneli_aylik_ve_tesis_dagilimini_verir()
    {
        await VeriAsync();
        var g = await S<IGostergeServisi>().GetirAsync(24);
        var eylul = g.Aylar.Single(a => a.Yil == 2026 && a.Ay == 9);
        Assert.Equal(59000m, eylul.BordroNet);
        Assert.Equal(57000m, eylul.Odenen);
        Assert.Equal(3, eylul.Personel);
        Assert.Equal(3, g.Tesisler.Count);
        Assert.Equal(1m, Math.Round(g.Tesisler.Sum(t => t.Pay), 6));
        Assert.True(g.ToplamUyari > 0);
    }
}
