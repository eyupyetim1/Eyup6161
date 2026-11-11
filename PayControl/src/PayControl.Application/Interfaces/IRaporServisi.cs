using PayControl.Domain.Enums;

namespace PayControl.Application.Interfaces;

public enum RaporKolonTuru { Metin, Tutar, Sayi, Tarih, Yuzde }

public sealed record RaporKolonu(string Baslik, RaporKolonTuru Tur = RaporKolonTuru.Metin, int Genislik = 14);

public sealed record RaporTanimi(RaporTuru Tur, string Ad, string Kategori, string Aciklama);

public sealed class RaporParametreleri
{
    public int BaslangicYil { get; set; } = DateTime.Today.AddMonths(-11).Year;
    public int BaslangicAy { get; set; } = DateTime.Today.AddMonths(-11).Month;
    public int BitisYil { get; set; } = DateTime.Today.Year;
    public int BitisAy { get; set; } = DateTime.Today.Month;
    public string? Sirket { get; set; }
    public string? Tesis { get; set; }
    public Guid? PersonelId { get; set; }

    public DateOnly Baslangic => new(BaslangicYil, BaslangicAy, 1);
    public DateOnly Bitis => new DateOnly(BitisYil, BitisAy, 1).AddMonths(1).AddDays(-1);

    public static RaporParametreleri TekDonem(int yil, int ay, string? tesis = null) =>
        new() { BaslangicYil = yil, BaslangicAy = ay, BitisYil = yil, BitisAy = ay, Tesis = tesis };
}

public sealed class RaporSonucu
{
    public RaporTuru Tur { get; init; }
    public string Baslik { get; init; } = string.Empty;
    public string Aciklama { get; init; } = string.Empty;
    public string ParametreMetni { get; init; } = string.Empty;
    public IReadOnlyList<RaporKolonu> Kolonlar { get; init; } = Array.Empty<RaporKolonu>();
    public IReadOnlyList<object?[]> Satirlar { get; init; } = Array.Empty<object?[]>();
    public object?[]? Toplam { get; init; }
    /// <summary>Raporun üstünde gösterilen özet göstergeler.</summary>
    public IReadOnlyList<(string Ad, string Deger)> Ozet { get; init; } = Array.Empty<(string, string)>();
    public DateTime OlusturmaZamani { get; init; } = DateTime.Now;
}

public interface IRaporServisi
{
    IReadOnlyList<RaporTanimi> Tanimlar { get; }
    Task<RaporSonucu> OlusturAsync(RaporTuru tur, RaporParametreleri p, CancellationToken ct = default);
    byte[] DisaAktar(RaporSonucu rapor, RaporBicimi bicim);
    string DosyaAdi(RaporSonucu rapor, RaporBicimi bicim);
}

public sealed class GostergeVerisi
{
    public IReadOnlyList<AylikGosterge> Aylar { get; init; } = Array.Empty<AylikGosterge>();
    public IReadOnlyList<TesisGosterge> Tesisler { get; init; } = Array.Empty<TesisGosterge>();
    public int KritikUyari { get; init; }
    public int ToplamUyari { get; init; }
}

public sealed record AylikGosterge(int Yil, int Ay, decimal BordroNet, decimal Odenen, int Personel, decimal Fark)
{
    public string Etiket => $"{Ay:D2}/{Yil % 100:D2}";
}

public sealed record TesisGosterge(string Tesis, int Personel, decimal BordroNet, decimal Pay);

public interface IGostergeServisi
{
    Task<GostergeVerisi> GetirAsync(int aySayisi = 12, string? tesis = null, CancellationToken ct = default);
}
