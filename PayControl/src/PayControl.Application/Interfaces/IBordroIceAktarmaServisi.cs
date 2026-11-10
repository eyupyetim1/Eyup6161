using PayControl.Application.Dtos;

namespace PayControl.Application.Interfaces;

public interface IBordroIceAktarmaServisi
{
    Task<IceAktarmaOnizlemeSonucu> OnizleAsync(
        Stream dosyaAkisi,
        string dosyaAdi,
        int donemYil,
        int donemAy,
        CancellationToken ct = default);

    Task<IceAktarmaSonucu> AktarAsync(
        Stream dosyaAkisi,
        string dosyaAdi,
        int donemYil,
        int donemAy,
        string kullaniciAdi,
        bool mukerrereIzinVer = false,
        CancellationToken ct = default);

    Task<bool> GeriAlAsync(Guid iceAktarmaId, string kullaniciAdi, CancellationToken ct = default);
}
