using PayControl.Domain.Entities;
using PayControl.Domain.Services;

namespace PayControl.Application.Interfaces;

public interface IOdemeOncesiKontrolServisi
{
    Task<OdemeKontrolOturumu> KontrolBaslatAsync(
        int donemYil,
        int donemAy,
        IReadOnlyList<BankaTalimatSatiri> bankaTalimatlari,
        string? bankaDosyaAdi,
        string? bankaHash,
        string kullanici,
        CancellationToken ct = default);

    Task IstisnaOnaylaAsync(
        Guid satirId,
        string gerekce,
        string onaylayan,
        CancellationToken ct = default);

    Task<OnayliOdemeListesi> OnayliListeUretAsync(
        Guid oturumId,
        string kullanici,
        string hedefKlasor,
        CancellationToken ct = default);

    Task<OdemeKontrolOturumu?> GetirAsync(Guid oturumId, CancellationToken ct = default);
}
