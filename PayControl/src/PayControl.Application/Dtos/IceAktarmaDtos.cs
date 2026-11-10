namespace PayControl.Application.Dtos;

public sealed class BordroSatirOnizlemeDto
{
    public int SatirNo { get; init; }
    public string? TcKimlikNo { get; init; }
    public string? AdSoyad { get; init; }
    public string? Isyeri { get; init; }
    public string? Gorevi { get; init; }
    public decimal? Maas { get; init; }
    public string? MaasTuruHam { get; init; }
    public DateOnly? GirisTarihi { get; init; }
    public DateOnly? CikisTarihi { get; init; }
    public decimal? Gun { get; init; }
    public decimal? NetIstihkak { get; init; }
    public string? Iban { get; init; }
    public decimal? Avans { get; init; }
    public decimal? GeriOdeme { get; init; }
    public decimal? Icra { get; init; }
    public decimal? OtoKatBes { get; init; }
    public decimal? TrafikCezasi { get; init; }
    public decimal? Aile { get; init; }
    public decimal? IhbarTazminati { get; init; }
    public decimal? Yemek { get; init; }
    public decimal? Yol { get; init; }
    public IReadOnlyList<string> Hatalar { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Uyarilar { get; init; } = Array.Empty<string>();
    public bool Gecerli => Hatalar.Count == 0;
}

public sealed class IceAktarmaOnizlemeSonucu
{
    public string DosyaAdi { get; init; } = string.Empty;
    public string DosyaHashSha256 { get; init; } = string.Empty;
    public bool MukerrerDosya { get; init; }
    public Guid? OncekiIceAktarmaId { get; init; }
    public IReadOnlyList<string> EslesenBasliklar { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EslesmeyenBasliklar { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> BelirsizBasliklar { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BordroSatirOnizlemeDto> Satirlar { get; init; } = Array.Empty<BordroSatirOnizlemeDto>();
    public int GecerliSatirSayisi => Satirlar.Count(s => s.Gecerli);
    public int HataliSatirSayisi => Satirlar.Count(s => !s.Gecerli);
}

public sealed class IceAktarmaSonucu
{
    public Guid IceAktarmaId { get; init; }
    public int BasariliSatir { get; init; }
    public int HataliSatir { get; init; }
    public int YeniPersonel { get; init; }
    public int GuncellenenPersonel { get; init; }
    public bool MukerrerEngellendi { get; init; }
    public string Mesaj { get; init; } = string.Empty;
    public IReadOnlyList<string> Uyarilar { get; init; } = Array.Empty<string>();
}
