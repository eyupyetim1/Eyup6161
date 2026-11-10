namespace PayControl.Domain.Enums;

/// <summary>
/// Personel mali hareket defterindeki işlem türleri.
/// Maaş alacağı, avans borcu ve şüpheli fazla ödeme ayrı bakiyelerde tutulur.
/// </summary>
public enum MaliIslemTuru
{
    MaasHakEdis = 0,
    BankaMaasOdemesi = 1,
    KasaOdemesi = 2,
    AvansVerildi = 3,
    AvansMahsup = 4,
    IcraKesinti = 5,
    BesKesinti = 6,
    DigerKesinti = 7,
    Prim = 8,
    Ikramiye = 9,
    FazlaMesai = 10,
    KidemTazminati = 11,
    IhbarTazminati = 12,
    EksikOdemeFarki = 13,
    FazlaOdemeFarki = 14,
    IadeAlinan = 15,
    OncekiDonemDuzeltme = 16,
    DevredenBorc = 17,
    DevredenAlacak = 18,
    MaasDegisikligi = 19,
    IzinUcreti = 20,
    IseGiris = 21,
    IstenCikis = 22,
    /// <summary>İncelenen şüpheli fazla ödemenin kesin geri ödeme borcuna dönüşmesi.</summary>
    FazlaOdemeKesinlesti = 24,
    /// <summary>Şüpheli fazla ödemenin belgeli hak ediş olarak açıklanması.</summary>
    FazlaOdemeAciklandi = 25,
    /// <summary>Bordro kalem dökümü (net istihkaka dahil kalemler; bakiyeyi etkilemez).</summary>
    BordroKalemBilgisi = 26
}
