using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Application.Interfaces;

public sealed record BildirimEki(string DosyaAdi, byte[] Icerik, string IcerikTuru);

/// <summary>SMTP ile e-posta gönderir (Infrastructure: MailKit).</summary>
public interface IEpostaGonderici
{
    Task GonderAsync(BildirimAyari ayar, string sifre, string alici, string konu, string metin, BildirimEki? ek, CancellationToken ct = default);
}

/// <summary>WhatsApp Business Cloud API ile mesaj gönderir.</summary>
public interface IWhatsAppGonderici
{
    Task GonderAsync(BildirimAyari ayar, string erisimAnahtari, string telefon, string metin, BildirimEki? ek, CancellationToken ct = default);
}

public sealed class BildirimAyarDto
{
    public string? SmtpSunucu { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpSsl { get; set; } = true;
    public string? SmtpKullanici { get; set; }
    /// <summary>Boş bırakılırsa mevcut şifre korunur.</summary>
    public string? SmtpSifre { get; set; }
    public string? GonderenEposta { get; set; }
    public string? GonderenAd { get; set; } = "PayControl";
    public bool WhatsAppEtkin { get; set; }
    public string? WhatsAppTelefonNumarasiId { get; set; }
    /// <summary>Boş bırakılırsa mevcut anahtar korunur.</summary>
    public string? WhatsAppErisimAnahtari { get; set; }
    public string? WhatsAppSablonAdi { get; set; }
    public string WhatsAppDil { get; set; } = "tr";
    public bool SmtpSifreKayitli { get; set; }
    public bool WhatsAppAnahtarKayitli { get; set; }
}

public sealed class BildirimTalebi
{
    public BildirimKanali Kanal { get; set; } = BildirimKanali.Eposta;
    public IReadOnlyList<Guid> AliciIdleri { get; set; } = Array.Empty<Guid>();
    /// <summary>Kayıtlı alıcı dışındaki doğrudan adresler (e-posta veya telefon).</summary>
    public IReadOnlyList<string> DogrudanAdresler { get; set; } = Array.Empty<string>();
    public string Konu { get; set; } = string.Empty;
    public string Metin { get; set; } = string.Empty;
    public BildirimEki? Ek { get; set; }
}

public interface IBildirimServisi
{
    Task<BildirimAyarDto> AyarlariGetirAsync(CancellationToken ct = default);
    Task AyarlariKaydetAsync(BildirimAyarDto dto, string kullanici, CancellationToken ct = default);

    Task<IReadOnlyList<BildirimAlicisi>> AlicilarAsync(CancellationToken ct = default);
    Task<BildirimAlicisi> AliciKaydetAsync(BildirimAlicisi alici, string kullanici, CancellationToken ct = default);

    Task<IReadOnlyList<BildirimKurali>> KurallarAsync(CancellationToken ct = default);
    Task<BildirimKurali> KuralKaydetAsync(BildirimKurali kural, string kullanici, CancellationToken ct = default);

    /// <summary>Elle (anında) gönderim; her alıcı için gönderim kaydı döner.</summary>
    Task<IReadOnlyList<BildirimKaydi>> GonderAsync(BildirimTalebi talep, string kullanici, CancellationToken ct = default);

    /// <summary>Raporu oluşturup ek olarak gönderir.</summary>
    Task<IReadOnlyList<BildirimKaydi>> RaporGonderAsync(RaporTuru tur, RaporParametreleri p, RaporBicimi bicim, BildirimKanali kanal,
        IReadOnlyList<Guid> aliciIdleri, string? notMetni, string kullanici, CancellationToken ct = default);

    /// <summary>WhatsApp API'si kullanmadan, mesajı hazır doldurulmuş WhatsApp bağlantısı (wa.me) üretir ve kaydeder.</summary>
    Task<string> WhatsAppBaglantisiAsync(string telefon, string metin, string kullanici, CancellationToken ct = default);

    /// <summary>Vadesi gelen zamanlanmış kuralları çalıştırır (uygulama içi zamanlayıcı veya Windows görevi).</summary>
    Task<int> ZamanlanmisKurallariCalistirAsync(DateTime simdi, CancellationToken ct = default);

    /// <summary>Olay tetikleyicili kurallar için bildirim üretir; aynı olay aynı alıcıya ikinci kez gönderilmez.</summary>
    Task OlayBildirAsync(BildirimTetikleyici olay, string olayAnahtari, string konu, string metin, string? tesis, CancellationToken ct = default);

    /// <summary>Sistem bildirimi: tek alıcıya e-posta (ve WhatsApp ayarlıysa WhatsApp); aynı olay anahtarı ikinci kez gönderilmez.</summary>
    Task KisiyeBildirAsync(Guid aliciId, string olayAnahtari, string konu, string metin, CancellationToken ct = default);

    /// <summary>Başarısız gönderimleri (en fazla 3 deneme) yeniden dener.</summary>
    Task<int> BasarisizlariYenidenDeneAsync(CancellationToken ct = default);

    Task<IReadOnlyList<BildirimKaydi>> GecmisAsync(int adet = 500, CancellationToken ct = default);
}
