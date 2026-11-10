using PayControl.Domain.Entities;

namespace PayControl.Application.Interfaces;

public interface IAvansServisi
{
    Task<AvansKaydi> VerAsync(
        Guid personelId,
        DateOnly tarih,
        decimal tutar,
        int taksitSayisi,
        decimal aylikMahsup,
        string? tur,
        string kullanici,
        string? aciklama = null,
        CancellationToken ct = default);

    Task<AvansMahsupKaydi> MahsupGerceklestirAsync(
        Guid avansId,
        int donemYil,
        int donemAy,
        decimal tutar,
        string kullanici,
        bool bordroyaGirildi = true,
        CancellationToken ct = default);

    Task<IReadOnlyList<AvansKaydi>> ListeleAsync(Guid personelId, CancellationToken ct = default);
}
