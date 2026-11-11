using System.Security.Cryptography;
using System.Text.Json;
using PayControl.Application.Interfaces;

namespace PayControl.Infrastructure.Platform;

/// <summary>
/// Otomatik güncelleme: bildirim dosyası HTTP(S) adresinden, dosya yolundan veya ağ paylaşımından (\\sunucu\paylasim\guncelleme.json) okunabilir.
/// İndirilen paket SHA-256 ile doğrulanmadan kurulmaz. Sürüm düşürme yapılmaz.
/// </summary>
public sealed class GuncellemeServisi : IGuncellemeServisi
{
    private readonly HttpClient _http;
    private readonly IAyarServisi _ayar;

    public GuncellemeServisi(HttpClient http, IAyarServisi ayar)
    {
        _http = http;
        _ayar = ayar;
    }

    public string VarsayilanAdres => "https://github.com/eyupyetim1/Eyup6161/releases/latest/download/guncelleme.json";

    public async Task<GuncellemeBilgisi?> DenetleAsync(Version mevcut, CancellationToken ct = default)
    {
        var adres = await _ayar.GetirAsync(AyarAnahtarlari.GuncellemeAdresi, ct);
        if (string.IsNullOrWhiteSpace(adres)) adres = VarsayilanAdres;
        var json = await OkuAsync(adres, ct);
        GuncellemeBilgisi? b;
        try { b = JsonSerializer.Deserialize<GuncellemeBilgisi>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException ex) { throw new InvalidOperationException($"Güncelleme bildirimi okunamadı: {ex.Message}"); }
        if (b is null || string.IsNullOrWhiteSpace(b.Surum) || string.IsNullOrWhiteSpace(b.Adres))
            throw new InvalidOperationException("Güncelleme bildirimi eksik (surum/adres).");
        if (!Version.TryParse(b.Surum.TrimStart('v', 'V'), out var yeni))
            throw new InvalidOperationException($"Güncelleme sürümü geçersiz: {b.Surum}");
        if (b.Sha256.Length != 64 || !b.Sha256.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Güncelleme bildiriminde geçerli SHA-256 özeti yok; güvenlik nedeniyle kurulmaz.");
        // Göreli paket adresi bildirim adresine göre çözülür
        if (!Uri.TryCreate(b.Adres, UriKind.Absolute, out _))
            b.Adres = Uri.TryCreate(adres, UriKind.Absolute, out var taban) && !taban.IsFile
                ? new Uri(taban, b.Adres).ToString()
                : Path.Combine(Path.GetDirectoryName(adres) ?? "", b.Adres);
        return Normalize(yeni) > Normalize(mevcut) ? b : null;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

    private async Task<string> OkuAsync(string adres, CancellationToken ct)
    {
        if (Uri.TryCreate(adres, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
        {
            using var yanit = await _http.GetAsync(u, ct);
            if (!yanit.IsSuccessStatusCode) throw new InvalidOperationException($"Güncelleme bildirimi alınamadı ({(int)yanit.StatusCode}).");
            return await yanit.Content.ReadAsStringAsync(ct);
        }
        var yol = u?.IsFile == true ? u.LocalPath : adres;
        if (!File.Exists(yol)) throw new InvalidOperationException($"Güncelleme bildirimi bulunamadı: {adres}");
        return await File.ReadAllTextAsync(yol, ct);
    }

    public async Task<string> IndirAsync(GuncellemeBilgisi bilgi, IProgress<double>? ilerleme = null, CancellationToken ct = default)
    {
        var hedef = Path.Combine(Path.GetTempPath(), $"PayControl-{bilgi.Surum}-{Guid.NewGuid():N}.zip");
        await using (var cikis = File.Create(hedef))
        {
            if (Uri.TryCreate(bilgi.Adres, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
            {
                using var yanit = await _http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!yanit.IsSuccessStatusCode) throw new InvalidOperationException($"Güncelleme paketi indirilemedi ({(int)yanit.StatusCode}).");
                var toplam = yanit.Content.Headers.ContentLength;
                await using var giris = await yanit.Content.ReadAsStreamAsync(ct);
                var tampon = new byte[81920];
                long okunan = 0;
                int n;
                while ((n = await giris.ReadAsync(tampon, ct)) > 0)
                {
                    await cikis.WriteAsync(tampon.AsMemory(0, n), ct);
                    okunan += n;
                    if (toplam > 0) ilerleme?.Report((double)okunan / toplam.Value);
                }
            }
            else
            {
                var yol = u?.IsFile == true ? u.LocalPath : bilgi.Adres;
                await using var giris = File.OpenRead(yol);
                await giris.CopyToAsync(cikis, ct);
            }
        }
        string ozet;
        await using (var f = File.OpenRead(hedef)) ozet = Convert.ToHexString(await SHA256.HashDataAsync(f, ct));
        if (!string.Equals(ozet, bilgi.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(hedef);
            throw new InvalidOperationException("Güncelleme paketinin SHA-256 özeti bildirimle uyuşmuyor; paket silindi ve kurulmadı.");
        }
        ilerleme?.Report(1);
        return hedef;
    }

    public string GuncelleyiciBetigi(string zipYolu, string kurulumKlasoru, int beklenecekSurecId)
    {
        static string Q(string s) => "'" + s.Replace("'", "''") + "'";
        return $$"""
            $ErrorActionPreference = 'Stop'
            $zip = {{Q(zipYolu)}}
            $hedef = {{Q(kurulumKlasoru)}}
            $log = Join-Path $env:TEMP 'PayControl-guncelleme.log'
            try {
              Wait-Process -Id {{beklenecekSurecId}} -Timeout 120 -ErrorAction SilentlyContinue
              $acilan = Join-Path $env:TEMP ('PayControl-guncelleme-' + [guid]::NewGuid().ToString('N'))
              Expand-Archive -LiteralPath $zip -DestinationPath $acilan -Force
              $kaynak = $acilan
              if (-not (Test-Path (Join-Path $kaynak 'PayControl.exe'))) {
                $alt = Get-ChildItem $acilan -Directory | Where-Object { Test-Path (Join-Path $_.FullName 'PayControl.exe') } | Select-Object -First 1
                if ($alt) { $kaynak = $alt.FullName } else { throw 'Pakette PayControl.exe yok.' }
              }
              $yedek = $hedef + '.onceki'
              if (Test-Path $yedek) { Remove-Item $yedek -Recurse -Force }
              Copy-Item $hedef $yedek -Recurse -Force
              try {
                Get-ChildItem $kaynak | Where-Object { $_.Name -notlike 'Install-PayControl*' -and $_.Name -notlike 'KURULUM*' -and $_.Name -notlike 'BASLAT*' } |
                  ForEach-Object { Copy-Item $_.FullName (Join-Path $hedef $_.Name) -Recurse -Force }
              } catch {
                Copy-Item (Join-Path $yedek '*') $hedef -Recurse -Force
                throw
              }
              Get-ChildItem -LiteralPath $hedef -Recurse -File | Unblock-File
              Remove-Item $acilan -Recurse -Force -ErrorAction SilentlyContinue
              Remove-Item $zip -Force -ErrorAction SilentlyContinue
              "$(Get-Date -Format o) Güncelleme tamamlandı" | Out-File $log -Append -Encoding utf8
            } catch {
              "$(Get-Date -Format o) Güncelleme hatası: $_" | Out-File $log -Append -Encoding utf8
            }
            Start-Process (Join-Path $hedef 'PayControl.exe')
            """;
    }
}
