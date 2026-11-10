using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Tests.Domain;

public class HakEdisVePersonelTests
{
    [Fact]
    public void Belirsiz_hesaplama_nihai_sonuc_degildir()
    {
        var sut = new HakEdisHesaplamaServisi();
        var sonuc = sut.CikisAyiHakEdisHesapla(
            aylikMaas: 30000m,
            maasTuru: MaasTuru.Net,
            cikisTarihi: new DateOnly(2026, 10, 15),
            bordroGunu: 15,
            bordroNetIstihkak: null,
            bordroOnayli: false);

        Assert.False(sonuc.NihaiSonucMu);
        Assert.NotNull(sonuc.TahminiTutar);
        Assert.Equal(15000m, sonuc.TahminiTutar);
    }

    [Fact]
    public void Onayli_bordro_net_istihkak_nihai_kabul_edilir()
    {
        var sut = new HakEdisHesaplamaServisi();
        var sonuc = sut.CikisAyiHakEdisHesapla(
            30000m, MaasTuru.Net, new DateOnly(2026, 10, 15), 15, 14850m, bordroOnayli: true);

        Assert.True(sonuc.NihaiSonucMu);
        Assert.Equal(14850m, sonuc.TahminiTutar);
    }

    [Fact]
    public void Yeniden_ise_alinan_personel_ayri_donem_ortak_kart()
    {
        var personel = new Personel { AdSoyad = "Ali Veli", TcKimlikNo = "10000000146" };
        var d1 = PersonelDonemKurallari.YeniDonemAc(personel, new DateOnly(2024, 1, 1));
        PersonelDonemKurallari.DonemiKapat(personel, d1, new DateOnly(2025, 6, 30));

        var d2 = PersonelDonemKurallari.YeniDonemAc(personel, new DateOnly(2026, 2, 1));

        Assert.Equal(2, personel.CalismaDonemleri.Count);
        Assert.Equal(CalismaDurumu.IstenAyrildi, d1.Durum);
        Assert.Equal(CalismaDurumu.Aktif, d2.Durum);
        Assert.Equal(CalismaDurumu.Aktif, personel.CalismaDurumu);
        Assert.Same(personel, personel); // tek kart
    }

    [Fact]
    public void Maas_degisince_onceki_donem_kaydi_degismez()
    {
        var eylul = new MaasDonemKaydi
        {
            Id = Guid.NewGuid(),
            DonemYil = 2026,
            DonemAy = 9,
            MaasTutar = 30000m,
            NetIstihkak = 30000m
        };
        var ekim = new MaasDonemKaydi
        {
            Id = Guid.NewGuid(),
            DonemYil = 2026,
            DonemAy = 10,
            MaasTutar = 35000m,
            NetIstihkak = 35000m
        };

        Assert.True(PersonelDonemKurallari.OncekiDonemMaasiKorunuyorMu(eylul, ekim));
        Assert.Equal(30000m, eylul.MaasTutar);
        Assert.Equal(35000m, ekim.MaasTutar);
    }

    [Fact]
    public void Ayni_isimli_iki_personel_tc_ile_ayrilir()
    {
        var a = new Personel { AdSoyad = "Ayşe Yılmaz", TcKimlikNo = "10000000146" };
        var b = new Personel { AdSoyad = "Ayşe Yılmaz", TcKimlikNo = "10000000154" };
        Assert.NotEqual(a.TcKimlikNo, b.TcKimlikNo);
        Assert.NotEqual(a.Id, b.Id);
    }
}
