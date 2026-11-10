using PayControl.Application.Dtos;
using PayControl.Domain.Entities;
using PayControl.Domain.Services;

namespace PayControl.Application.Interfaces;

public interface IOdemeOncesiKontrolServisi
{
    /// <summary>
    /// Dönem bordrosu (içe aktarılmış) ile bankaya gönderilecek talimatları karşılaştırır.
    /// Bordro yüklenmeden kontrol başlatılamaz.
    /// </summary>
    Task<OdemeKontrolOturumu> KontrolBaslatAsync(
        int donemYil,
        int donemAy,
        IReadOnlyList<BankaTalimatSatiri> bankaTalimatlari,
        string? bankaDosyaAdi,
        string? bankaHash,
        string kullanici,
        CancellationToken ct = default);

    /// <summary>Banka dosyasının formatını (şablon ve ham hücreler) da saklayarak kontrol başlatır.</summary>
    Task<OdemeKontrolOturumu> KontrolBaslatAsync(
        int donemYil,
        int donemAy,
        BankaTalimatDosyasi bankaDosyasi,
        string? bankaDosyaAdi,
        string? bankaHash,
        string kullanici,
        Guid? bordroIceAktarmaId = null,
        CancellationToken ct = default);

    /// <summary>Kullanıcı banka talimatını düzeltir veya listeden çıkarır; oturum yeniden değerlendirilir.</summary>
    Task<OdemeKontrolOturumu> TalimatDuzeltAsync(
        Guid oturumId,
        int talimatSatirNo,
        string? yeniIban,
        decimal? yeniTutar,
        bool haric,
        string gerekce,
        string kullanici,
        CancellationToken ct = default);

    /// <summary>Güncel verilerle (yeni ödeme, çıkış kaydı vb.) oturumu yeniden değerlendirir.</summary>
    Task<OdemeKontrolOturumu> YenidenDegerlendirAsync(Guid oturumId, string kullanici, CancellationToken ct = default);

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

    Task<IReadOnlyList<OnayliOdemeListesi>> DonemListeleriAsync(int donemYil, int donemAy, CancellationToken ct = default);
}
