using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Infrastructure.Data;
using PayControl.Infrastructure.DependencyInjection;

namespace PayControl.Tests.ExitAudit;

public class IstenCikisAppTests : IAsyncLifetime
{
    private ServiceProvider _sp = null!;
    private string _dbPath = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"paycontrol-exit-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddPayControlInfrastructure(_dbPath);
        _sp = services.BuildServiceProvider();
        await ServiceCollectionExtensions.InitializeDatabaseAsync(_sp);
    }

    public async Task DisposeAsync()
    {
        await _sp.DisposeAsync();
        if (File.Exists(_dbPath))
            File.Delete(_dbPath);
    }

    [Fact]
    public async Task Cikis_kaydi_sgk_otomatik_verildi_yapmaz()
    {
        var personelServis = _sp.GetRequiredService<IPersonelServisi>();
        var cikisServis = _sp.GetRequiredService<IIstenCikisServisi>();

        var p = await personelServis.OlusturAsync(new PersonelOlusturDto
        {
            TcKimlikNo = "10000000146",
            AdSoyad = "Deneme Kullanıcı",
            IseGirisTarihi = new DateOnly(2024, 1, 1),
            GuncelMaas = 30000
        }, "admin");

        var kayit = await cikisServis.OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p.Id,
            FiiliSonCalismaTarihi = new DateOnly(2026, 10, 15),
            IstenCikisTarihi = new DateOnly(2026, 10, 15),
            SonBordroDonemYil = 2026,
            SonBordroDonemAy = 10,
            // SgkKullaniciOnayi false — Belirsiz kalmalı
            SgkCikisDurumu = SgkCikisDurumu.Verildi,
            SgkKullaniciOnayi = false
        }, "admin");

        Assert.Equal(SgkCikisDurumu.Belirsiz, kayit.SgkCikisDurumu);

        var detay = await personelServis.GetirAsync(p.Id);
        Assert.Equal(CalismaDurumu.IstenAyrildi, detay!.CalismaDurumu);
    }

    [Fact]
    public async Task Acik_avansla_cikis_denetim_uyarisi_yazilir()
    {
        var personelServis = _sp.GetRequiredService<IPersonelServisi>();
        var cikisServis = _sp.GetRequiredService<IIstenCikisServisi>();
        var db = _sp.GetRequiredService<PayControlDbContext>();

        var p = await personelServis.OlusturAsync(new PersonelOlusturDto
        {
            TcKimlikNo = "10000000154",
            AdSoyad = "Avansli Personel",
            IseGirisTarihi = new DateOnly(2023, 5, 1)
        }, "admin");

        var kayit = await cikisServis.OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p.Id,
            FiiliSonCalismaTarihi = new DateOnly(2026, 10, 10),
            IstenCikisTarihi = new DateOnly(2026, 10, 10),
            KalanAvans = 7500m
        }, "admin");

        Assert.Equal(7500m, kayit.KalanAvans);
        Assert.True(await db.DenetimKayitlari.AnyAsync(d =>
            d.PersonelId == p.Id &&
            d.AlanAdi == "KalanAvans" &&
            d.Islem == "Uyari"));
    }

    [Fact]
    public async Task Cikis_odeme_inceleme_cift_odemeyi_tespit_eder()
    {
        var personelServis = _sp.GetRequiredService<IPersonelServisi>();
        var cikisServis = _sp.GetRequiredService<IIstenCikisServisi>();
        var db = _sp.GetRequiredService<PayControlDbContext>();

        var p = await personelServis.OlusturAsync(new PersonelOlusturDto
        {
            TcKimlikNo = "10000000162",
            AdSoyad = "Fazla Odeme",
            IseGirisTarihi = new DateOnly(2022, 1, 1)
        }, "admin");

        db.MaasDonemKayitlari.Add(new MaasDonemKaydi
        {
            PersonelId = p.Id,
            DonemYil = 2026,
            DonemAy = 10,
            NetIstihkak = 15000m,
            MaasTutar = 30000m,
            Gun = 15
        });
        await db.SaveChangesAsync();

        var cikis = await cikisServis.OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = p.Id,
            FiiliSonCalismaTarihi = new DateOnly(2026, 10, 15),
            IstenCikisTarihi = new DateOnly(2026, 10, 15),
            SonBordroDonemYil = 2026,
            SonBordroDonemAy = 10
        }, "admin");

        db.BankaOdemeleri.AddRange(
            new BankaOdemeKaydi
            {
                PersonelId = p.Id,
                HakEdisDonemYil = 2026,
                HakEdisDonemAy = 10,
                OdemeTarihi = new DateOnly(2026, 11, 5),
                Tutar = 15000m
            },
            new BankaOdemeKaydi
            {
                PersonelId = p.Id,
                HakEdisDonemYil = 2026,
                HakEdisDonemAy = 10,
                OdemeTarihi = new DateOnly(2026, 11, 20),
                Tutar = 30000m
            });
        await db.SaveChangesAsync();

        var inceleme = await cikisServis.OdemeleriInceleAsync(cikis.Id);
        Assert.Equal(OdemeSiniflandirma.BrutSupheliOdeme, inceleme.Siniflandirma);
        Assert.Equal(30000m, inceleme.Fark);
    }
}
