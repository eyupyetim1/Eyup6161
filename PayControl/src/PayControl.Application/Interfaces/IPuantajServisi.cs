using PayControl.Domain.Entities;
using PayControl.Domain.Services;

namespace PayControl.Application.Interfaces;

public sealed record PuantajGunBasligi(int Gun, DateOnly Tarih, string HaftaGunuKisa, bool HaftaSonu, string? ResmiTatil, decimal TatilHakki);

public sealed class PuantajSatiri
{
    public Guid? PersonelId { get; init; }
    public string TcKimlikNo { get; init; } = string.Empty;
    public string AdSoyad { get; init; } = string.Empty;
    public string? Unvan { get; init; }
    public bool Aktif { get; init; }
    public Dictionary<int, string> Kodlar { get; init; } = new();
    public Dictionary<int, string> Notlar { get; init; } = new();
    public AylikPuantajOzeti Ozet { get; set; } = PuantajHesaplayici.Hesapla(2000, 1, new Dictionary<int, string>());
}

public sealed class PuantajTablosu
{
    public string Tesis { get; init; } = string.Empty;
    public int Yil { get; init; }
    public int Ay { get; init; }
    public IReadOnlyList<PuantajGunBasligi> Gunler { get; init; } = Array.Empty<PuantajGunBasligi>();
    public IReadOnlyList<PuantajSatiri> Satirlar { get; init; } = Array.Empty<PuantajSatiri>();
}

public sealed record PuantajDegisikligi(string TcKimlikNo, int Gun, string? Kod);

public sealed class NakilDto
{
    public PersonelNakil Kayit { get; init; } = new();
}

public interface IPuantajServisi
{
    Task<IReadOnlyList<string>> TesislerAsync(CancellationToken ct = default);
    Task<PuantajTablosu> TabloAsync(string tesis, int yil, int ay, CancellationToken ct = default);
    /// <summary>Hücre değişikliklerini kaydeder (kod boşsa silinir); geçersiz kod reddedilir. Kaydedilen sayısını döner.</summary>
    Task<int> KaydetAsync(string tesis, int yil, int ay, IReadOnlyList<PuantajDegisikligi> degisiklikler, string kullanici, CancellationToken ct = default);
    Task NotKaydetAsync(string tesis, string tc, int yil, int ay, int gun, string? not, string kullanici, CancellationToken ct = default);

    Task<IReadOnlyList<ResmiTatil>> ResmiTatillerAsync(int? yil = null, CancellationToken ct = default);
    Task<ResmiTatil> ResmiTatilKaydetAsync(DateOnly tarih, string ad, decimal hak, string kullanici, CancellationToken ct = default);
    Task ResmiTatilSilAsync(Guid id, string kullanici, CancellationToken ct = default);

    Task<IReadOnlyList<PersonelNakil>> NakillerAsync(string? tesis = null, CancellationToken ct = default);
    /// <summary>Personeli başka tesise aktarır (VBA frmPersonelAktarma): kart tesisi güncellenir, nakil kaydı ve denetim izi yazılır.</summary>
    Task<PersonelNakil> NakilYapAsync(Guid personelId, string yeniTesis, DateOnly tarih, string? aciklama, string kullanici, CancellationToken ct = default);
}
