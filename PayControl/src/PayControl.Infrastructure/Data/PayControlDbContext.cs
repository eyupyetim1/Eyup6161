using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;

namespace PayControl.Infrastructure.Data;

public sealed class PayControlDbContext : DbContext, IPayControlDbContext
{
    public PayControlDbContext(DbContextOptions<PayControlDbContext> options) : base(options)
    {
    }

    public DbSet<Personel> Personeller => Set<Personel>();
    public DbSet<CalismaDonemi> CalismaDonemleri => Set<CalismaDonemi>();
    public DbSet<MaasDonemKaydi> MaasDonemKayitlari => Set<MaasDonemKaydi>();
    public DbSet<MaasKalemi> MaasKalemleri => Set<MaasKalemi>();
    public DbSet<IstenCikisKaydi> IstenCikisKayitlari => Set<IstenCikisKaydi>();
    public DbSet<DenetimKaydi> DenetimKayitlari => Set<DenetimKaydi>();
    public DbSet<IceAktarmaKaydi> IceAktarmalar => Set<IceAktarmaKaydi>();
    public DbSet<PersonelBelge> PersonelBelgeler => Set<PersonelBelge>();
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<BankaOdemeKaydi> BankaOdemeleri => Set<BankaOdemeKaydi>();
    public DbSet<MaliHareket> MaliHareketler => Set<MaliHareket>();
    public DbSet<AvansKaydi> Avanslar => Set<AvansKaydi>();
    public DbSet<AvansMahsupKaydi> AvansMahsuplari => Set<AvansMahsupKaydi>();
    public DbSet<MaasDonemKapanisi> MaasDonemKapanislari => Set<MaasDonemKapanisi>();
    public DbSet<DonemKontrolMaddesi> DonemKontrolMaddeleri => Set<DonemKontrolMaddesi>();
    public DbSet<DonemDuzeltmeKaydi> DonemDuzeltmeler => Set<DonemDuzeltmeKaydi>();
    public DbSet<OdemeKontrolOturumu> OdemeKontrolOturumlari => Set<OdemeKontrolOturumu>();
    public DbSet<OdemeKontrolSatiri> OdemeKontrolSatirlari => Set<OdemeKontrolSatiri>();
    public DbSet<OnayliOdemeListesi> OnayliOdemeListeleri => Set<OnayliOdemeListesi>();
    public DbSet<OnayliOdemeListesiSatiri> OnayliOdemeListesiSatirlari => Set<OnayliOdemeListesiSatiri>();
    public DbSet<OdemeTalimatSatiri> OdemeTalimatSatirlari => Set<OdemeTalimatSatiri>();
    public DbSet<MutabakatFarki> MutabakatFarklari => Set<MutabakatFarki>();
    public DbSet<DonemKapanisAnlikGoruntusu> DonemKapanisAnlikGoruntuleri => Set<DonemKapanisAnlikGoruntusu>();
    public DbSet<DonemKapanisOzeti> DonemKapanisOzetleri => Set<DonemKapanisOzeti>();
    public DbSet<Tesis> Tesisler => Set<Tesis>();
    public DbSet<DonemTesisOnayi> DonemTesisOnaylari => Set<DonemTesisOnayi>();
    public DbSet<IcraDosyasi> IcraDosyalari => Set<IcraDosyasi>();
    public DbSet<IcraKesintiKaydi> IcraKesintileri => Set<IcraKesintiKaydi>();
    public DbSet<BesTanimi> BesTanimlari => Set<BesTanimi>();
    public DbSet<Belge> Belgeler => Set<Belge>();
    public DbSet<TahsilatPlani> TahsilatPlanlari => Set<TahsilatPlani>();
    public DbSet<CikisTalimati> CikisTalimatlari => Set<CikisTalimati>();
    public DbSet<TahsilatTaksiti> TahsilatTaksitleri => Set<TahsilatTaksiti>();
    public DbSet<BildirimAyari> BildirimAyarlari => Set<BildirimAyari>();
    public DbSet<BildirimAlicisi> BildirimAlicilari => Set<BildirimAlicisi>();
    public DbSet<BildirimKurali> BildirimKurallari => Set<BildirimKurali>();
    public DbSet<BildirimKaydi> BildirimKayitlari => Set<BildirimKaydi>();
    public DbSet<YedekKaydi> YedekKayitlari => Set<YedekKaydi>();
    public DbSet<UygulamaAyari> UygulamaAyarlari => Set<UygulamaAyari>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        VeriButunluguKorumasi.Denetle(this);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        VeriButunluguKorumasi.Denetle(this);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Personel>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.TcKimlikNo).IsUnique();
            e.Property(x => x.TcKimlikNo).HasMaxLength(11).IsRequired();
            e.Property(x => x.AdSoyad).HasMaxLength(200).IsRequired();
            e.Property(x => x.Iban).HasMaxLength(34);
            e.Property(x => x.GuncelMaas).HasPrecision(18, 2);
        });

        modelBuilder.Entity<CalismaDonemi>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Personel).WithMany(p => p.CalismaDonemleri)
                .HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaasDonemKaydi>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PersonelId, x.DonemYil, x.DonemAy, x.IceAktarmaId });
            // Geri alınmış (iptal) bordro satırları silinmez; varsayılan sorgulardan gizlenir.
            e.HasQueryFilter(x => !x.IptalEdildi);
            e.Property(x => x.NetIstihkak).HasPrecision(18, 2);
            e.Property(x => x.MaasTutar).HasPrecision(18, 2);
            e.Property(x => x.Gun).HasPrecision(18, 2);
            e.HasOne(x => x.Personel).WithMany(p => p.MaasDonemKayitlari)
                .HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.IceAktarma).WithMany(i => i.MaasDonemKayitlari)
                .HasForeignKey(x => x.IceAktarmaId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<MaasKalemi>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tutar).HasPrecision(18, 2);
            e.HasOne(x => x.MaasDonemKaydi).WithMany(m => m.Kalemler)
                .HasForeignKey(x => x.MaasDonemKaydiId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IstenCikisKaydi>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.KalanAvans).HasPrecision(18, 2);
            e.Property(x => x.IzinUcreti).HasPrecision(18, 2);
            e.Property(x => x.KidemTazminati).HasPrecision(18, 2);
            e.Property(x => x.IhbarTazminati).HasPrecision(18, 2);
            e.HasOne(x => x.Personel).WithMany(p => p.IstenCikisKayitlari)
                .HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CalismaDonemi).WithMany(d => d.IstenCikisKayitlari)
                .HasForeignKey(x => x.CalismaDonemiId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DenetimKaydi>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Personel).WithMany(p => p.DenetimKayitlari)
                .HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<IceAktarmaKaydi>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DosyaHashSha256);
        });

        modelBuilder.Entity<Kullanici>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KullaniciAdi).IsUnique();
        });

        modelBuilder.Entity<BankaOdemeKaydi>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tutar).HasPrecision(18, 2);
            e.HasIndex(x => new { x.PersonelId, x.HakEdisDonemYil, x.HakEdisDonemAy });
            e.HasIndex(x => x.ReferansNo);
            e.HasIndex(x => x.TekillikAnahtari).IsUnique();
            // İptal edilen ödemeler silinmez; varsayılan sorgulardan gizlenir.
            e.HasQueryFilter(x => !x.IptalEdildi);
        });

        modelBuilder.Entity<Tesis>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Ad).IsUnique();
            e.HasIndex(x => x.Kod).IsUnique();
            e.Property(x => x.Ad).HasMaxLength(200).IsRequired();
            e.Property(x => x.Kod).HasMaxLength(50).IsRequired();
        });
        modelBuilder.Entity<DonemTesisOnayi>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.MaasDonemKapanisiId, x.TesisAd }).IsUnique();
        });
        modelBuilder.Entity<IcraDosyasi>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ToplamBorc).HasPrecision(18, 2);
            e.Property(x => x.Kesilen).HasPrecision(18, 2);
            e.Property(x => x.KesintiOrani).HasPrecision(9, 4);
            e.Ignore(x => x.Kalan);
            e.HasIndex(x => new { x.PersonelId, x.Sira });
        });
        modelBuilder.Entity<IcraKesintiKaydi>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tutar).HasPrecision(18, 2);
            e.HasOne(x => x.IcraDosyasi).WithMany(d => d.Kesintiler).HasForeignKey(x => x.IcraDosyasiId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BesTanimi>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Oran).HasPrecision(9, 4);
        });
        modelBuilder.Entity<Belge>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.VarlikTuru, x.VarlikId });
            e.HasIndex(x => x.PersonelId);
        });
        modelBuilder.Entity<CikisTalimati>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PersonelId, x.Durum });
            e.HasOne(x => x.Personel).WithMany().HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Neden).HasMaxLength(500);
        });
        modelBuilder.Entity<TahsilatPlani>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tutar).HasPrecision(18, 2);
            e.Property(x => x.AylikTutar).HasPrecision(18, 2);
            e.Property(x => x.KalanBorc).HasPrecision(18, 2);
        });
        modelBuilder.Entity<TahsilatTaksiti>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Planlanan).HasPrecision(18, 2);
            e.Property(x => x.Gerceklesen).HasPrecision(18, 2);
            e.HasIndex(x => new { x.TahsilatPlaniId, x.DonemYil, x.DonemAy }).IsUnique();
            e.HasOne(x => x.TahsilatPlani).WithMany(p => p.Taksitler).HasForeignKey(x => x.TahsilatPlaniId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BildirimAyari>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<BildirimAlicisi>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<BildirimKurali>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<BildirimKaydi>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OlayAnahtari).IsUnique();
            e.HasIndex(x => x.OlusturmaZamani);
        });
        modelBuilder.Entity<YedekKaydi>(e => e.HasKey(x => x.Id));
        modelBuilder.Entity<UygulamaAyari>(e =>
        {
            e.HasKey(x => x.Anahtar);
            e.Property(x => x.Anahtar).HasMaxLength(100);
        });

        modelBuilder.Entity<PersonelBelge>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Personel).WithMany(p => p.Belgeler)
                .HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MaliHareket>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.IslemKimligi).IsUnique();
            e.Property(x => x.IslemKimligi).HasMaxLength(200).IsRequired();
            e.Property(x => x.Tutar).HasPrecision(18, 2);
            e.HasIndex(x => new { x.PersonelId, x.IslemTarihi });
            e.HasOne(x => x.Personel).WithMany(p => p.MaliHareketler)
                .HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AvansKaydi>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tutar).HasPrecision(18, 2);
            e.Property(x => x.AylikMahsupTutari).HasPrecision(18, 2);
            e.Property(x => x.KalanBorc).HasPrecision(18, 2);
            e.HasOne(x => x.Personel).WithMany(p => p.Avanslar)
                .HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AvansMahsupKaydi>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.PlanlananTutar).HasPrecision(18, 2);
            e.Property(x => x.GerceklesenTutar).HasPrecision(18, 2);
            e.HasIndex(x => new { x.AvansKaydiId, x.DonemYil, x.DonemAy }).IsUnique();
            e.HasOne(x => x.AvansKaydi).WithMany(a => a.Mahsuplar)
                .HasForeignKey(x => x.AvansKaydiId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MaasDonemKapanisi>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DonemYil, x.DonemAy, x.Sirket }).IsUnique();
            e.Property(x => x.ToplamBeklenenMaas).HasPrecision(18, 2);
            e.Property(x => x.ToplamBankaOdemesi).HasPrecision(18, 2);
            e.Property(x => x.ToplamEksikOdeme).HasPrecision(18, 2);
            e.Property(x => x.ToplamFazlaOdeme).HasPrecision(18, 2);
            e.Property(x => x.OdenmemisHakEdisler).HasPrecision(18, 2);
            e.Property(x => x.AcikAvanslar).HasPrecision(18, 2);
            e.Property(x => x.CikisSonrasiSupheli).HasPrecision(18, 2);
        });

        modelBuilder.Entity<DonemKontrolMaddesi>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.MaasDonemKapanisi).WithMany(d => d.KontrolMaddeleri)
                .HasForeignKey(x => x.MaasDonemKapanisiId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DonemDuzeltmeKaydi>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FarkTutari).HasPrecision(18, 2);
            e.HasOne(x => x.MaasDonemKapanisi).WithMany(d => d.Duzeltmeler)
                .HasForeignKey(x => x.MaasDonemKapanisiId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OdemeKontrolOturumu>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DonemYil, x.DonemAy, x.OlusturmaZamani });
        });

        modelBuilder.Entity<OdemeKontrolSatiri>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.BordroNetIstihkak).HasPrecision(18, 2);
            e.Property(x => x.BankaOdemeTutari).HasPrecision(18, 2);
            e.HasOne(x => x.OdemeKontrolOturumu).WithMany(o => o.Satirlar)
                .HasForeignKey(x => x.OdemeKontrolOturumuId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Personel).WithMany()
                .HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OnayliOdemeListesi>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ToplamTutar).HasPrecision(18, 2);
            e.HasIndex(x => new { x.DonemYil, x.DonemAy, x.Surum });
            e.HasOne(x => x.OdemeKontrolOturumu).WithMany(o => o.OnayliListeler)
                .HasForeignKey(x => x.OdemeKontrolOturumuId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OnayliOdemeListesiSatiri>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tutar).HasPrecision(18, 2);
            e.HasOne(x => x.OnayliOdemeListesi).WithMany(l => l.Satirlar)
                .HasForeignKey(x => x.OnayliOdemeListesiId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OdemeTalimatSatiri>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Tutar).HasPrecision(18, 2);
            e.Property(x => x.OrijinalTutar).HasPrecision(18, 2);
            e.HasIndex(x => new { x.OdemeKontrolOturumuId, x.SatirNo }).IsUnique();
            e.HasOne(x => x.OdemeKontrolOturumu).WithMany(o => o.Talimatlar)
                .HasForeignKey(x => x.OdemeKontrolOturumuId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MutabakatFarki>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Beklenen).HasPrecision(18, 2);
            e.Property(x => x.Odenen).HasPrecision(18, 2);
            e.Property(x => x.Fark).HasPrecision(18, 2);
            e.HasIndex(x => new { x.MaasDonemKapanisiId, x.Anahtar }).IsUnique();
            e.HasOne(x => x.MaasDonemKapanisi).WithMany(d => d.Farklar)
                .HasForeignKey(x => x.MaasDonemKapanisiId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DonemKapanisAnlikGoruntusu>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.MaasTutar).HasPrecision(18, 2);
            e.Property(x => x.BordroNet).HasPrecision(18, 2);
            e.Property(x => x.Odenen).HasPrecision(18, 2);
            e.Property(x => x.Fark).HasPrecision(18, 2);
            e.HasIndex(x => new { x.MaasDonemKapanisiId, x.KapanisNo });
        });

        modelBuilder.Entity<DonemKapanisOzeti>(e =>
        {
            e.HasKey(x => x.Id);
            foreach (var prop in new[] { nameof(DonemKapanisOzeti.ToplamBeklenenMaas), nameof(DonemKapanisOzeti.ToplamBankaOdemesi),
                         nameof(DonemKapanisOzeti.ToplamEksikOdeme), nameof(DonemKapanisOzeti.ToplamFazlaOdeme),
                         nameof(DonemKapanisOzeti.OdenmemisHakEdisler), nameof(DonemKapanisOzeti.AcikAvanslar),
                         nameof(DonemKapanisOzeti.CikisSonrasiSupheli) })
                e.Property(prop).HasPrecision(18, 2);
            e.HasIndex(x => new { x.MaasDonemKapanisiId, x.KapanisNo }).IsUnique();
        });
    }
}
