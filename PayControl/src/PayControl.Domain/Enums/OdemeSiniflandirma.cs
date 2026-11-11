namespace PayControl.Domain.Enums;

/// <summary>
/// Çıkış sonrası / fazla ödeme inceleme sınıfları.
/// Açıklanamayan fazla ödeme doğrudan kesin zarar değildir.
/// </summary>
public enum OdemeSiniflandirma
{
    NormalHakEdisOdemesi = 0,
    BrutSupheliOdeme = 1,
    BelgelenmisEkHakEdis = 2,
    AciklanamayanFazlaOdeme = 3,
    TeyitEdilmisFazlaOdeme = 4,
    IadeAlinanTutar = 5,
    KalanTahsilEdilebilirTutar = 6,
    KesinlesmisSirketZarari = 7
}
