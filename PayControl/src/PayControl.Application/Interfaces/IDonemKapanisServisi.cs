using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Application.Interfaces;

public interface IDonemKapanisServisi
{
    Task<MaasDonemKapanisi> GetOrCreateAsync(int yil, int ay, string? sirket = null, CancellationToken ct = default);
    Task DurumGuncelleAsync(Guid donemId, MaasDonemDurumu durum, string kullanici, CancellationToken ct = default);
    Task MaddeTamamlaAsync(Guid maddeId, string kullanici, string? not = null, CancellationToken ct = default);
    Task OzetHesaplaAsync(Guid donemId, CancellationToken ct = default);
    Task KapatAsync(Guid donemId, string kullanici, string? not, CancellationToken ct = default);
    Task YenidenAcAsync(Guid donemId, string kullanici, string neden, CancellationToken ct = default);
    Task<DonemDuzeltmeKaydi> DuzeltmeEkleAsync(
        Guid donemId,
        Guid? personelId,
        string varlikTuru,
        Guid? varlikId,
        string alan,
        string? eski,
        string? yeni,
        string neden,
        string kullanici,
        CancellationToken ct = default);
    Task<DonemKarsilastirmaDto> OncekiAyKarsilastirAsync(int yil, int ay, CancellationToken ct = default);
    Task EnsureDonemAcikAsync(int yil, int ay, CancellationToken ct = default);
}

public sealed class DonemKarsilastirmaDto
{
    public int OncekiPersonelSayisi { get; init; }
    public int BuAyPersonelSayisi { get; init; }
    public int IstenAyrilan { get; init; }
    public int IseBaslayan { get; init; }
    public int MaasDegisen { get; init; }
    public decimal OncekiToplamNet { get; init; }
    public decimal BuAyToplamNet { get; init; }
    public IReadOnlyList<string> Nedenler { get; init; } = Array.Empty<string>();
}
