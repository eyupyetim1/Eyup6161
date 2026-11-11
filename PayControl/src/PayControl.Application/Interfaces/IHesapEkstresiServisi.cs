using PayControl.Application.Dtos;

namespace PayControl.Application.Interfaces;

public interface IHesapEkstresiServisi
{
    Task<HesapEkstresiDto> GetirAsync(Guid personelId, HesapEkstresiFiltre? filtre = null, CancellationToken ct = default);
    Task<byte[]> ExcelOlarakAktarAsync(Guid personelId, HesapEkstresiFiltre? filtre = null, CancellationToken ct = default);
    Task<byte[]> PdfOlarakAktarAsync(Guid personelId, HesapEkstresiFiltre? filtre = null, CancellationToken ct = default);
}
