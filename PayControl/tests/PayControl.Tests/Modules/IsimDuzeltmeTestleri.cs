using Microsoft.EntityFrameworkCore;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Services;
using PayControl.Infrastructure.DependencyInjection;

namespace PayControl.Tests.Modules;

public class IsimDuzeltmeTestleri : TestOrtami
{
    [Theory]
    [InlineData("EYÜP  YETİM", "EYÜP YETİM")]
    [InlineData("  EYÜP    YETİM  ", "EYÜP YETİM")]
    [InlineData("ALİ  CAN\t\tAKSOY", "ALİ CAN AKSOY")]
    [InlineData("AYŞE​ FATMA\r\n KAYA", "AYŞE FATMA KAYA")]
    [InlineData("Mehmet Kaya", "Mehmet Kaya")]
    public void Fazla_ve_gorunmez_bosluklar_tek_bosluga_iner(string ham, string beklenen)
    {
        Assert.Equal(beklenen, IsimKurallari.Duzelt(ham));
        Assert.Equal(ham != beklenen, IsimKurallari.DuzeltmeGerekirMi(ham));
    }

    [Fact]
    public async Task Excel_bordrosundaki_cift_bosluklu_isim_duzeltilerek_kaydedilir_ve_uyari_verilir()
    {
        var p = new PayControl.Domain.Entities.Personel { TcKimlikNo = Tc(), AdSoyad = "EYÜP   YETİM ", Iban = Iban(900), Isyeri = "Arsin Limanı" };
        var bytes = BordroExcel([new BordroSatir(p, 20000m, Giris: new DateOnly(2025, 1, 1))]);
        await using (var ms = new MemoryStream(bytes))
        {
            var on = await S<IBordroIceAktarmaServisi>().OnizleAsync(ms, "b.xlsx", 2026, 10);
            var satir = Assert.Single(on.Satirlar);
            Assert.Equal("EYÜP YETİM", satir.AdSoyad);
            Assert.Contains(satir.Uyarilar, u => u.Contains("fazla boşluk"));
        }
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 20000m, Giris: new DateOnly(2025, 1, 1)));
        var kayit = await Db.Personeller.AsNoTracking().SingleAsync(x => x.TcKimlikNo == p.TcKimlikNo);
        Assert.Equal("EYÜP YETİM", kayit.AdSoyad);
        // Arama boşluk farkına duyarsız
        Assert.Single(await S<IPersonelServisi>().ListeleAsync("EYÜP    YETİM"));
    }

    [Fact]
    public async Task Elle_girilen_isim_duzeltilir_eski_kayitlar_acilista_denetim_iziyle_duzeltilir()
    {
        var dto = await S<IPersonelServisi>().OlusturAsync(new PersonelOlusturDto
        {
            TcKimlikNo = Tc(), AdSoyad = " Ali   Can  Aksoy", IseGirisTarihi = new DateOnly(2025, 1, 1), GuncelMaas = 30000, Iban = Iban(901)
        }, Muhasebe);
        Assert.Equal("Ali Can Aksoy", (await Db.Personeller.AsNoTracking().SingleAsync(x => x.Id == dto.Id)).AdSoyad);

        // Önceki sürümden kalmış bozuk kayıt
        var db = Db;
        var eski = await db.Personeller.SingleAsync(x => x.Id == dto.Id);
        eski.AdSoyad = "ALİ  CAN   AKSOY";
        await db.SaveChangesAsync();
        Assert.Equal(1, await ServiceCollectionExtensions.IsimleriDuzeltAsync(Db));
        Assert.Equal("ALİ CAN AKSOY", (await Db.Personeller.AsNoTracking().SingleAsync(x => x.Id == dto.Id)).AdSoyad);
        Assert.Contains(await Db.DenetimKayitlari.AsNoTracking().ToListAsync(), d => d.AlanAdi == "AdSoyad" && d.EskiDeger == "ALİ  CAN   AKSOY");
        Assert.Equal(0, await ServiceCollectionExtensions.IsimleriDuzeltAsync(Db));
    }

    [Theory]
    [InlineData("eyüp yetim", true)]
    [InlineData("eyup yetim", true)]
    [InlineData("YETİM eyüp", true)]
    [InlineData("yetım", true)]
    [InlineData("  Eyüp   ", true)]
    [InlineData("eyüp kaya", false)]
    public void Turkce_arama_harf_ve_sira_duyarsiz(string arama, bool beklenen) =>
        Assert.Equal(beklenen, AramaKurallari.Eslesir(arama, "EYÜP YETİM", "12345678901"));

    [Fact]
    public async Task Personel_aramasi_kucuk_harf_turkce_harf_ve_tc_parcasiyla_bulur()
    {
        var p1 = await PersonelAsync("ALİ ŞEN", 30000, 950, isyeri: "Arsin Limanı");
        await PersonelAsync("Çağrı Öztürk", 30000, 951, isyeri: "Rize Limanı");
        var srv = S<IPersonelServisi>();
        Assert.Single(await srv.ListeleAsync("ali şen"));
        Assert.Single(await srv.ListeleAsync("sen ali"));
        Assert.Single(await srv.ListeleAsync("cagri ozturk"));
        Assert.Single(await srv.ListeleAsync("ÇAĞRI"));
        Assert.Single(await srv.ListeleAsync(p1.TcKimlikNo[7..]));
        Assert.Single(await srv.ListeleAsync(null, null, "arsin"));
        Assert.Equal(2, (await srv.ListeleAsync("")).Count);
        Assert.Empty(await srv.ListeleAsync("mehmet"));
    }
}
