using System.Text.Json;

namespace PayControl.Infrastructure.Data;

/// <summary>
/// Veritabanı seçimi. Varsayılan: kullanıcının yerel SQLite dosyası (tek kullanıcı).
/// Çok kullanıcılı kullanım için %LOCALAPPDATA%\PayControl\veritabani.json (veya uygulama klasöründeki veritabani.json):
/// { "Saglayici": "PostgreSql", "BaglantiMetni": "Host=sunucu;Database=paycontrol;Username=paycontrol;Password=..." }
/// Parola düz metin tutulmak istenmezse "BaglantiMetniSifreli" alanına uygulamanın Ayarlar ekranından şifrelenmiş değer yazılır.
/// </summary>
public sealed class VeritabaniYapilandirmasi
{
    public const string DosyaAdi = "veritabani.json";
    public string Saglayici { get; set; } = "Sqlite";
    public string? BaglantiMetni { get; set; }
    public string? BaglantiMetniSifreli { get; set; }

    public bool PostgreSqlMi => string.Equals(Saglayici, "PostgreSql", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(Saglayici, "Postgres", StringComparison.OrdinalIgnoreCase);

    public static string VarsayilanKlasor => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PayControl");

    /// <summary>Önce kullanıcı klasörü, sonra uygulama klasörü aranır; yoksa SQLite.</summary>
    public static VeritabaniYapilandirmasi Yukle(string? klasor = null)
    {
        foreach (var yol in new[] { Path.Combine(klasor ?? VarsayilanKlasor, DosyaAdi), Path.Combine(AppContext.BaseDirectory, DosyaAdi) })
        {
            if (!File.Exists(yol)) continue;
            try
            {
                var y = JsonSerializer.Deserialize<VeritabaniYapilandirmasi>(File.ReadAllText(yol), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (y is not null) return y;
            }
            catch (JsonException ex) { throw new InvalidOperationException($"{yol} okunamadı: {ex.Message}"); }
        }
        return new VeritabaniYapilandirmasi();
    }

    public void Kaydet(string? klasor = null)
    {
        var k = klasor ?? VarsayilanKlasor;
        Directory.CreateDirectory(k);
        File.WriteAllText(Path.Combine(k, DosyaAdi), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
