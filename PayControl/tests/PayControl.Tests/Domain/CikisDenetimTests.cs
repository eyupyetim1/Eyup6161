using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Tests.Domain;

public class CikisDenetimTests
{
    private readonly CikisDenetimServisi _sut = new();

    [Fact]
    public void Cikis_sonrasi_eslesen_tek_odeme_normal_olabilir()
    {
        // Çıkış 15.10.2026, Ekim hak edişi 15.000, Kasım'da yatan 15.000 → normal
        var cikis = new IstenCikisKaydi
        {
            IstenCikisTarihi = new DateOnly(2026, 10, 15),
            SonBordroDonemYil = 2026,
            SonBordroDonemAy = 10
        };
        var hakEdis = new MaasDonemKaydi
        {
            DonemYil = 2026,
            DonemAy = 10,
            NetIstihkak = 15000m
        };
        var odemeler = new List<BankaOdemeKaydi>
        {
            new()
            {
                OdemeTarihi = new DateOnly(2026, 11, 5),
                Tutar = 15000m,
                HakEdisDonemYil = 2026,
                HakEdisDonemAy = 10
            }
        };

        var sonuc = _sut.OdemeleriIncele(cikis, hakEdis, odemeler);

        Assert.Equal(OdemeSiniflandirma.NormalHakEdisOdemesi, sonuc.Siniflandirma);
        Assert.Equal(0m, sonuc.Fark);
        Assert.Contains("tek başına hata değildir", sonuc.Gerekce);
    }

    [Fact]
    public void Ayni_hak_edise_ikinci_odeme_supheli_fazla()
    {
        var cikis = new IstenCikisKaydi
        {
            IstenCikisTarihi = new DateOnly(2026, 10, 15),
            SonBordroDonemYil = 2026,
            SonBordroDonemAy = 10
        };
        var hakEdis = new MaasDonemKaydi { NetIstihkak = 15000m };
        var odemeler = new List<BankaOdemeKaydi>
        {
            new() { OdemeTarihi = new DateOnly(2026, 11, 5), Tutar = 15000m },
            new() { OdemeTarihi = new DateOnly(2026, 11, 20), Tutar = 30000m }
        };

        var sonuc = _sut.OdemeleriIncele(cikis, hakEdis, odemeler);

        Assert.Equal(OdemeSiniflandirma.BrutSupheliOdeme, sonuc.Siniflandirma);
        Assert.Equal(30000m, sonuc.Fark); // 45000 - 15000
        Assert.True(sonuc.KullaniciOnayiGerekli);
    }

    [Fact]
    public void Sgk_cikis_yalnizca_onay_ile_verildi_olur()
    {
        var cikis = new IstenCikisKaydi { SgkCikisDurumu = SgkCikisDurumu.Belirsiz };

        _sut.SgkCikisOnayla(cikis, new DateOnly(2026, 10, 16), "admin", "sgk.pdf");

        Assert.Equal(SgkCikisDurumu.Verildi, cikis.SgkCikisDurumu);
        Assert.Equal("admin", cikis.SgkOnaylayanKullanici);
    }

    [Fact]
    public void Sgk_cikis_onaysiz_kullanici_ile_hata()
    {
        var cikis = new IstenCikisKaydi();
        Assert.Throws<InvalidOperationException>(() =>
            _sut.SgkCikisOnayla(cikis, new DateOnly(2026, 10, 16), "", null));
    }
}
