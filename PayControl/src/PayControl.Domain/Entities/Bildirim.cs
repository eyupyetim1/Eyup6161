using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

/// <summary>E-posta ve WhatsApp gönderim ayarları (tek satır). Parolalar şifreli saklanır.</summary>
public class BildirimAyari
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? SmtpSunucu { get; set; }
    public int SmtpPort { get; set; } = 587;
    public bool SmtpSsl { get; set; } = true;
    public string? SmtpKullanici { get; set; }
    public string? SmtpSifreSifreli { get; set; }
    public string? GonderenEposta { get; set; }
    public string? GonderenAd { get; set; } = "PayControl";

    public bool WhatsAppEtkin { get; set; }
    public string? WhatsAppTelefonNumarasiId { get; set; }
    public string? WhatsAppErisimAnahtariSifreli { get; set; }
    /// <summary>24 saat penceresi dışındaki iş başlatmalı mesajlar için Meta onaylı şablon adı.</summary>
    public string? WhatsAppSablonAdi { get; set; }
    public string WhatsAppDil { get; set; } = "tr";
    public string WhatsAppApiAdresi { get; set; } = "https://graph.facebook.com/v19.0";

    public DateTime? GuncellemeZamani { get; set; }
    public string? GuncelleyenKullanici { get; set; }
}

public class BildirimAlicisi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AdSoyad { get; set; } = string.Empty;
    public string? Eposta { get; set; }
    /// <summary>Uluslararası biçim, örn. 905321234567.</summary>
    public string? Telefon { get; set; }
    public string Rol { get; set; } = "Yönetici";
    public string? Tesis { get; set; }
    public bool Aktif { get; set; } = true;
}

public class BildirimKurali
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Ad { get; set; } = string.Empty;
    public bool Aktif { get; set; } = true;
    public BildirimTetikleyici Tetikleyici { get; set; }
    public BildirimSikligi Siklik { get; set; } = BildirimSikligi.Aylik;
    /// <summary>Aylık: ayın günü (1-28); haftalık: 1=Pazartesi … 7=Pazar.</summary>
    public int Gun { get; set; } = 1;
    public int Saat { get; set; } = 9;
    public BildirimKanali Kanal { get; set; } = BildirimKanali.Eposta;
    /// <summary>Alıcı kimlikleri (virgülle). Boşsa <see cref="AliciRolu"/> kullanılır.</summary>
    public string? AliciIdleri { get; set; }
    public string? AliciRolu { get; set; }
    /// <summary>Doluysa yalnızca bu tesise ait veri/alıcılar.</summary>
    public string? Tesis { get; set; }
    public RaporTuru? RaporTuru { get; set; }
    public RaporBicimi RaporBicimi { get; set; } = RaporBicimi.Excel;
    public DateTime? SonCalisma { get; set; }
    public string? OlusturanKullanici { get; set; }
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
}

public class BildirimKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? KuralId { get; set; }
    public BildirimKanali Kanal { get; set; }
    public string Alici { get; set; } = string.Empty;
    public string Konu { get; set; } = string.Empty;
    public string Ozet { get; set; } = string.Empty;
    public string? EkDosyaAdi { get; set; }
    public BildirimDurumu Durum { get; set; } = BildirimDurumu.Bekliyor;
    public string? Hata { get; set; }
    public int DenemeSayisi { get; set; }
    /// <summary>Aynı olay için tekrar gönderimi engeller (örn. "DonemKapandi:2026-10:alici").</summary>
    public string? OlayAnahtari { get; set; }
    public string? Kullanici { get; set; }
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
    public DateTime? GonderimZamani { get; set; }
}
