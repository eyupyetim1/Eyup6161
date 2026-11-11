using PayControl.Application.Dtos;
using PayControl.Domain.Services;

namespace PayControl.Application.Interfaces;

public interface IIstenCikisServisi
{
    Task<IstenCikisDto> OlusturAsync(IstenCikisOlusturDto dto, string kullaniciAdi, CancellationToken ct = default);

    Task SgkOnaylaAsync(
        Guid cikisId,
        DateOnly bildirgeTarihi,
        string onaylayanKullanici,
        string? belgeYolu,
        CancellationToken ct = default);

    Task<CikisOdemeIncelemeSonucu> OdemeleriInceleAsync(
        Guid cikisId,
        CancellationToken ct = default);

    Task<HakEdisHesaplamaSonucu> CikisAyiHakEdisHesaplaAsync(
        Guid personelId,
        DateOnly cikisTarihi,
        bool bordroOnayli,
        CancellationToken ct = default);
}
