using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;
using PayControl.Infrastructure.Data;
using PayControl.Infrastructure.DependencyInjection;

namespace PayControl.Tests.Modules;

/// <summary>
/// Bölüm 16-18 entegrasyon senaryoları (sentetik veri).
/// </summary>
public class UcModulSenaryoTests : IAsyncLifetime
{
    private ServiceProvider _sp = null!;
    private string _dbPath = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"pc-mod-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddPayControlInfrastructure(_dbPath);
        _sp = services.BuildServiceProvider();
        await ServiceCollectionExtensions.InitializeDatabaseAsync(_sp);
    }

    public async Task DisposeAsync()
    {
        await _sp.DisposeAsync();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private async Task<Personel> PersonelOlustur(string tc, string ad, decimal maas = 30000)
    {
        var p = _sp.GetRequiredService<IPersonelServisi>();
        var dto = await p.OlusturAsync(new PersonelOlusturDto
        {
            TcKimlikNo = tc,
            AdSoyad = ad,
            IseGirisTarihi = new DateOnly(2024, 1, 1),
            GuncelMaas = maas,
            Iban = "TR330006100519786457841326"
        }, "tester");
        var db = _sp.GetRequiredService<PayControlDbContext>();
        return await db.Personeller.FirstAsync(x => x.Id == dto.Id);
    }

    [Fact]
    public async Task S1_Avans_verilince_ekstrede_gorunur()
    {
        var personel = await PersonelOlustur("10000000146", "Avans Test");
        var avans = _sp.GetRequiredService<IAvansServisi>();
        await avans.VerAsync(personel.Id, new DateOnly(2026, 9, 5), 20000m, 4, 5000m, "Taksitli", "tester");

        var ekstre = await _sp.GetRequiredService<IHesapEkstresiServisi>().GetirAsync(personel.Id);
        Assert.Contains(ekstre.Hareketler, h => h.IslemTuru == MaliIslemTuru.AvansVerildi && h.Tutar == 20000m);
        Assert.Equal(20000m, ekstre.Ozet.AcikAvansBorcu);
    }

    [Fact]
    public async Task S2_Avans_mahsup_bakiyeyi_azaltir()
    {
        var personel = await PersonelOlustur("10000000154", "Mahsup Test");
        var avansSrv = _sp.GetRequiredService<IAvansServisi>();
        var avans = await avansSrv.VerAsync(personel.Id, new DateOnly(2026, 8, 1), 20000m, 4, 5000m, "Taksitli", "tester");
        await avansSrv.MahsupGerceklestirAsync(avans.Id, 2026, 9, 5000m, "tester");

        var guncel = (await avansSrv.ListeleAsync(personel.Id)).First();
        Assert.Equal(15000m, guncel.KalanBorc);

        var ekstre = await _sp.GetRequiredService<IHesapEkstresiServisi>().GetirAsync(personel.Id);
        Assert.Equal(15000m, ekstre.Ozet.AcikAvansBorcu);
        Assert.Contains(ekstre.Hareketler, h => h.IslemTuru == MaliIslemTuru.AvansMahsup && h.Tutar == 5000m);
    }

    [Fact]
    public async Task S3_Ayni_banka_hareketi_mukerrer_engellenir()
    {
        var personel = await PersonelOlustur("10000000162", "Banka Mukerrer");
        var banka = _sp.GetRequiredService<IBankaOdemeServisi>();
        await banka.KaydetAsync(personel.Id, new DateOnly(2026, 11, 5), 15000m, 2026, 10,
            personel.Iban, "REF-001", "tester");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            banka.KaydetAsync(personel.Id, new DateOnly(2026, 11, 6), 15000m, 2026, 10,
                personel.Iban, "REF-001", "tester"));
    }

    [Fact]
    public async Task S4_Cikis_son_ucret_dogru_doneme_baglanir()
    {
        var personel = await PersonelOlustur("10000000170", "Cikis Donem");
        var db = _sp.GetRequiredService<PayControlDbContext>();
        db.MaasDonemKayitlari.Add(new MaasDonemKaydi
        {
            PersonelId = personel.Id,
            DonemYil = 2026,
            DonemAy = 10,
            NetIstihkak = 15000m,
            MaasTutar = 30000m,
            Gun = 15
        });
        await db.SaveChangesAsync();

        var cikis = await _sp.GetRequiredService<IIstenCikisServisi>().OlusturAsync(new Application.Dtos.IstenCikisOlusturDto
        {
            PersonelId = personel.Id,
            FiiliSonCalismaTarihi = new DateOnly(2026, 10, 15),
            IstenCikisTarihi = new DateOnly(2026, 10, 15),
            SonBordroDonemYil = 2026,
            SonBordroDonemAy = 10
        }, "tester");

        Assert.Equal(2026, cikis.SonBordroDonemYil);
        Assert.Equal(10, cikis.SonBordroDonemAy);
    }

    [Fact]
    public async Task S5_Cikis_sonrasi_hak_edilmis_ucret_zarar_yazilmaz()
    {
        var personel = await PersonelOlustur("10000000188", "Normal Son Odeme");
        var db = _sp.GetRequiredService<PayControlDbContext>();
        db.MaasDonemKayitlari.Add(new MaasDonemKaydi
        {
            PersonelId = personel.Id, DonemYil = 2026, DonemAy = 10, NetIstihkak = 15000m
        });
        await db.SaveChangesAsync();

        var cikis = await _sp.GetRequiredService<IIstenCikisServisi>().OlusturAsync(new Application.Dtos.IstenCikisOlusturDto
        {
            PersonelId = personel.Id,
            FiiliSonCalismaTarihi = new DateOnly(2026, 10, 15),
            IstenCikisTarihi = new DateOnly(2026, 10, 15),
            SonBordroDonemYil = 2026,
            SonBordroDonemAy = 10
        }, "tester");

        await _sp.GetRequiredService<IBankaOdemeServisi>().KaydetAsync(
            personel.Id, new DateOnly(2026, 11, 5), 15000m, 2026, 10, personel.Iban, "REF-OK", "tester");

        var inceleme = await _sp.GetRequiredService<IIstenCikisServisi>().OdemeleriInceleAsync(cikis.Id);
        Assert.Equal(OdemeSiniflandirma.NormalHakEdisOdemesi, inceleme.Siniflandirma);
    }

    [Fact]
    public async Task S6_Tekrar_odeme_yakalanir()
    {
        var personel = await PersonelOlustur("10000000196", "Cift Odeme");
        var db = _sp.GetRequiredService<PayControlDbContext>();
        db.MaasDonemKayitlari.Add(new MaasDonemKaydi
        {
            PersonelId = personel.Id, DonemYil = 2026, DonemAy = 10, NetIstihkak = 15000m
        });
        await db.SaveChangesAsync();

        var cikis = await _sp.GetRequiredService<IIstenCikisServisi>().OlusturAsync(new Application.Dtos.IstenCikisOlusturDto
        {
            PersonelId = personel.Id,
            FiiliSonCalismaTarihi = new DateOnly(2026, 10, 15),
            IstenCikisTarihi = new DateOnly(2026, 10, 15),
            SonBordroDonemYil = 2026,
            SonBordroDonemAy = 10
        }, "tester");

        var banka = _sp.GetRequiredService<IBankaOdemeServisi>();
        await banka.KaydetAsync(personel.Id, new DateOnly(2026, 11, 5), 15000m, 2026, 10, personel.Iban, "R1", "tester");
        await banka.KaydetAsync(personel.Id, new DateOnly(2026, 11, 20), 30000m, 2026, 10, personel.Iban, "R2", "tester");

        var inceleme = await _sp.GetRequiredService<IIstenCikisServisi>().OdemeleriInceleAsync(cikis.Id);
        Assert.Equal(OdemeSiniflandirma.BrutSupheliOdeme, inceleme.Siniflandirma);
    }

    [Fact]
    public async Task S7_Yanlis_IBAN_tespit_edilir()
    {
        var personel = await PersonelOlustur("10000000204", "IBAN Test");
        var db = _sp.GetRequiredService<PayControlDbContext>();
        db.MaasDonemKayitlari.Add(new MaasDonemKaydi
        {
            PersonelId = personel.Id, DonemYil = 2026, DonemAy = 10,
            NetIstihkak = 25000m, Iban = personel.Iban
        });
        await db.SaveChangesAsync();

        var oturum = await _sp.GetRequiredService<IOdemeOncesiKontrolServisi>().KontrolBaslatAsync(
            2026, 10,
            new[] { new BankaTalimatSatiri(personel.TcKimlikNo, personel.AdSoyad, "TR000000000000000000000001", 25000m, 2) },
            "banka.xlsx", "HASH1", "tester");

        var satir = oturum.Satirlar.First(s => s.PersonelId == personel.Id);
        Assert.Equal(OdemeKontrolSonucu.IbanUyusmazligi, satir.Sonuc);
        Assert.True(satir.KritikMi);
    }

    [Fact]
    public async Task S8_Kritik_hatali_talimat_onaysiz_disa_aktarilamaz()
    {
        var personel = await PersonelOlustur("10000000212", "Kritik Blok");
        var db = _sp.GetRequiredService<PayControlDbContext>();
        db.MaasDonemKayitlari.Add(new MaasDonemKaydi
        {
            PersonelId = personel.Id, DonemYil = 2026, DonemAy = 10, NetIstihkak = 25000m, Iban = personel.Iban
        });
        await db.SaveChangesAsync();

        var kontrol = _sp.GetRequiredService<IOdemeOncesiKontrolServisi>();
        var oturum = await kontrol.KontrolBaslatAsync(
            2026, 10,
            new[] { new BankaTalimatSatiri(personel.TcKimlikNo, personel.AdSoyad, "TR111", 99999m, 2) },
            "banka.xlsx", "HASH2", "tester");

        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            kontrol.OnayliListeUretAsync(oturum.Id, "tester", dir));
    }

    [Fact]
    public async Task S9_Kapatilmis_doneme_mudahale_engellenir()
    {
        var donemSrv = _sp.GetRequiredService<IDonemKapanisServisi>();
        var donem = await donemSrv.GetOrCreateAsync(2026, 10);
        foreach (var m in donem.KontrolMaddeleri)
            await donemSrv.MaddeTamamlaAsync(m.Id, "tester");
        await donemSrv.KapatAsync(donem.Id, "tester", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            donemSrv.EnsureDonemAcikAsync(2026, 10));
    }

    [Fact]
    public async Task S10_Duzeltmeler_eski_kaydi_silmeden_izlenir()
    {
        var personel = await PersonelOlustur("10000000220", "Duzeltme");
        var donemSrv = _sp.GetRequiredService<IDonemKapanisServisi>();
        var donem = await donemSrv.GetOrCreateAsync(2026, 9);
        var db = _sp.GetRequiredService<PayControlDbContext>();
        var kayit = new MaasDonemKaydi
        {
            PersonelId = personel.Id, DonemYil = 2026, DonemAy = 9, NetIstihkak = 30000m, MaasTutar = 30000m
        };
        db.MaasDonemKayitlari.Add(kayit);
        await db.SaveChangesAsync();

        await donemSrv.DuzeltmeEkleAsync(donem.Id, personel.Id, nameof(MaasDonemKaydi), kayit.Id,
            "NetIstihkak", "30000", "30500", "Eksik prim farkı", "tester");

        // Eski kayıt duruyor
        var eski = await db.MaasDonemKayitlari.AsNoTracking().FirstAsync(m => m.Id == kayit.Id);
        Assert.Equal(30000m, eski.NetIstihkak);

        Assert.True(await db.DonemDuzeltmeler.AnyAsync(d => d.MaasDonemKapanisiId == donem.Id));
        Assert.True(await db.MaliHareketler.AnyAsync(h =>
            h.PersonelId == personel.Id && h.IslemTuru == MaliIslemTuru.OncekiDonemDuzeltme));
    }

    [Fact]
    public async Task S11_Gecmis_maas_yeni_degisiklikten_etkilenmez()
    {
        var personel = await PersonelOlustur("10000000238", "Maas Koruma", 30000);
        var db = _sp.GetRequiredService<PayControlDbContext>();
        db.MaasDonemKayitlari.Add(new MaasDonemKaydi
        {
            PersonelId = personel.Id, DonemYil = 2026, DonemAy = 9, MaasTutar = 30000m, NetIstihkak = 30000m
        });
        await db.SaveChangesAsync();

        await _sp.GetRequiredService<IPersonelServisi>().GuncelleAsync(personel.Id,
            new PersonelGuncelleDto { AdSoyad = personel.AdSoyad, GuncelMaas = 35000m, Iban = personel.Iban },
            "tester", "Zam");

        var eylul = await db.MaasDonemKayitlari.AsNoTracking()
            .FirstAsync(m => m.PersonelId == personel.Id && m.DonemAy == 9);
        Assert.Equal(30000m, eylul.MaasTutar);
    }

    [Fact]
    public async Task S12_Ekstre_toplamlari_hareketlerle_uyusur()
    {
        var personel = await PersonelOlustur("10000000246", "Ekstre Toplam");
        var avans = _sp.GetRequiredService<IAvansServisi>();
        await avans.VerAsync(personel.Id, new DateOnly(2026, 9, 1), 10000m, 2, 5000m, "Normal", "tester");
        await avans.MahsupGerceklestirAsync(
            (await avans.ListeleAsync(personel.Id)).First().Id, 2026, 10, 5000m, "tester");

        var db = _sp.GetRequiredService<PayControlDbContext>();
        var kimlik = MaliHareketKurallari.IslemKimligi("BordroHakEdis", personel.Id, "2026-10:test");
        db.MaliHareketler.Add(MaliHareketKurallari.Olustur(
            personel.Id, kimlik, new DateOnly(2026, 10, 31), 2026, 10,
            MaliIslemTuru.MaasHakEdis, BakiyeTuru.MaasAlacagi, 25000m, "Hak ediş", "Test", null, null, "tester"));
        await db.SaveChangesAsync();

        await _sp.GetRequiredService<IBankaOdemeServisi>().KaydetAsync(
            personel.Id, new DateOnly(2026, 11, 5), 20000m, 2026, 10, personel.Iban, "REF-T", "tester");

        var ekstre = await _sp.GetRequiredService<IHesapEkstresiServisi>().GetirAsync(personel.Id);
        var yeniden = MaliHareketKurallari.OzetHesapla(
            await db.MaliHareketler.Where(h => h.PersonelId == personel.Id).ToListAsync());

        Assert.Equal(yeniden.ToplamHakEdis, ekstre.Ozet.ToplamHakEdis);
        Assert.Equal(yeniden.GerceklesenToplamOdeme, ekstre.Ozet.GerceklesenToplamOdeme);
        Assert.Equal(yeniden.AcikAvansBorcu, ekstre.Ozet.AcikAvansBorcu);
        Assert.Equal(5000m, ekstre.Ozet.AcikAvansBorcu);
        Assert.Equal(25000m, ekstre.Ozet.ToplamHakEdis);
        Assert.Equal(20000m, ekstre.Ozet.GerceklesenToplamOdeme);
    }

    [Fact]
    public async Task Cift_avans_mahsup_engellenir()
    {
        var personel = await PersonelOlustur("10000000253", "Cift Mahsup");
        var avansSrv = _sp.GetRequiredService<IAvansServisi>();
        var avans = await avansSrv.VerAsync(personel.Id, new DateOnly(2026, 8, 1), 10000m, 2, 5000m, "Taksitli", "tester");
        await avansSrv.MahsupGerceklestirAsync(avans.Id, 2026, 9, 5000m, "tester");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            avansSrv.MahsupGerceklestirAsync(avans.Id, 2026, 9, 5000m, "tester"));
    }
}
