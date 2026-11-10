using PayControl.Domain.Entities;

namespace PayControl.Domain.Services;

/// <summary>
/// İşten çıkış sonrası ödeme denetimi.
/// Hak ediş dönemi, çıkış tarihi ve banka ödeme tarihi birbirine karıştırılmaz.
/// </summary>
public interface ICikisDenetimServisi
{
    /// <summary>
    /// Aynı hak ediş yükümlülüğüne yapılan ödemeleri inceler.
    /// Çıkış sonrası sonraki ayda yatan hak ediş tek başına hata değildir.
    /// </summary>
    CikisOdemeIncelemeSonucu OdemeleriIncele(
        IstenCikisKaydi cikis,
        MaasDonemKaydi? ilgiliHakEdis,
        IReadOnlyList<BankaOdemeKaydi> ayniHakEdisOdemeleri);

    /// <summary>
    /// SGK çıkış durumunu yalnızca açık onay veya belge ile "Verildi" yapar.
    /// </summary>
    void SgkCikisOnayla(
        IstenCikisKaydi cikis,
        DateOnly bildirgeTarihi,
        string onaylayanKullanici,
        string? belgeYolu);
}
