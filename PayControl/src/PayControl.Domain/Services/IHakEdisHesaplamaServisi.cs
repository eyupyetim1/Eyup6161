using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Domain.Services;

/// <summary>
/// Yasal bordro / ücret hesaplama katmanı arayüzü.
/// Net/brüt dönüşümü ve ay içi giriş-çıkış için mevzuat kuralları burada yönetilir.
/// </summary>
public interface IHakEdisHesaplamaServisi
{
    HakEdisHesaplamaSonucu CikisAyiHakEdisHesapla(
        decimal? aylikMaas,
        MaasTuru maasTuru,
        DateOnly cikisTarihi,
        decimal? bordroGunu,
        decimal? bordroNetIstihkak,
        bool bordroOnayli);
}
