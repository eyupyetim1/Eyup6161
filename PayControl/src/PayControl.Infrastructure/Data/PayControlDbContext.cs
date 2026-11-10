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
        });

        modelBuilder.Entity<PersonelBelge>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Personel).WithMany(p => p.Belgeler)
                .HasForeignKey(x => x.PersonelId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
