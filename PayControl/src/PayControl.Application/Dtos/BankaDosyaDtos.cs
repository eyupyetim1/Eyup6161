using PayControl.Domain.Services;

namespace PayControl.Application.Dtos;

/// <summary>
/// Yüklenen banka ödeme dosyasının formatı. Onaylı liste aynı başlık sırası ve kolon düzeniyle üretilir.
/// </summary>
public sealed class BankaDosyaSablonu
{
    public string? SayfaAdi { get; set; }
    /// <summary>Başlıktan önceki satırlar (banka üst bilgisi) — aynen korunur. Anahtar: satır, değer: kolon→metin.</summary>
    public List<Dictionary<int, string>> UstBilgiSatirlari { get; set; } = new();
    /// <summary>Kolon numarası (1 tabanlı) → başlık metni.</summary>
    public Dictionary<int, string> Basliklar { get; set; } = new();
    /// <summary>Rol (TC, AD, IBAN, TUTAR, ACIKLAMA) → kolon numarası.</summary>
    public Dictionary<string, int> KolonRolleri { get; set; } = new();
    /// <summary>Tutar hücresinin sayı biçimi (örn. #,##0.00).</summary>
    public string? TutarBicimi { get; set; }
}

public sealed class BankaTalimatDosyasi
{
    public BankaDosyaSablonu Sablon { get; init; } = new();
    public IReadOnlyList<BankaTalimatSatiri> Satirlar { get; init; } = Array.Empty<BankaTalimatSatiri>();
    /// <summary>Satır no → tüm hücreler (kolon → metin); şablonda tanımlı olmayan kolonlar listeye aynen taşınır.</summary>
    public IReadOnlyDictionary<int, Dictionary<int, string>> HamHucreler { get; init; } = new Dictionary<int, Dictionary<int, string>>();
    public IReadOnlyList<string> Uyarilar { get; init; } = Array.Empty<string>();
}
