namespace PayControl.Application.Interfaces;

/// <summary>Güncelleme bildirimi (guncelleme.json): { "surum": "1.3.0", "adres": "...zip", "sha256": "...", "notlar": "..." }</summary>
public sealed class GuncellemeBilgisi
{
    public string Surum { get; set; } = string.Empty;
    public string Adres { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public string? Notlar { get; set; }
    public DateTime? Tarih { get; set; }
}

public interface IGuncellemeServisi
{
    /// <summary>Varsayılan bildirim adresi (GitHub Releases'teki son sürümün guncelleme.json dosyası).</summary>
    string VarsayilanAdres { get; }

    /// <summary>Bildirimi okur; mevcut sürümden yeni bir sürüm varsa döner, yoksa null.</summary>
    Task<GuncellemeBilgisi?> DenetleAsync(Version mevcut, CancellationToken ct = default);

    /// <summary>Paketi indirir ve SHA-256 özetini doğrular; doğrulanmış zip yolunu döner.</summary>
    Task<string> IndirAsync(GuncellemeBilgisi bilgi, IProgress<double>? ilerleme = null, CancellationToken ct = default);

    /// <summary>Uygulama kapandıktan sonra paketi kurulum klasörüne açıp uygulamayı yeniden başlatan PowerShell betiğini üretir.</summary>
    string GuncelleyiciBetigi(string zipYolu, string kurulumKlasoru, int beklenecekSurecId);
}
