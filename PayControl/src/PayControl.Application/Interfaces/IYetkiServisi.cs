using PayControl.Domain.Enums;

namespace PayControl.Application.Interfaces;

public interface IYetkiServisi
{
    /// <summary>Kullanıcı aktif değilse veya rolü yetersizse <see cref="Domain.YetkiHatasiException"/> fırlatır.</summary>
    Task DogrulaAsync(string kullaniciAdi, KullaniciRolu enAzRol, string islem, CancellationToken ct = default);
}
