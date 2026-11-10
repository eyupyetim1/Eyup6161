using System.Security.Cryptography;
using System.Text;
using PayControl.Application.Interfaces;

namespace PayControl.Infrastructure.Security;

/// <summary>
/// Windows'ta DPAPI (kullanıcı hesabına bağlı), diğer platformlarda makineye özel AES-GCM anahtarı ile şifreler.
/// </summary>
public sealed class SifreKoruyucu : ISifreKoruyucu
{
    private readonly string _anahtarDosyasi;

    public SifreKoruyucu(string anahtarKlasoru) => _anahtarDosyasi = Path.Combine(anahtarKlasoru, "anahtar.bin");

    public string Koru(string acikMetin)
    {
        var veri = Encoding.UTF8.GetBytes(acikMetin);
        if (OperatingSystem.IsWindows())
            return "dpapi:" + Convert.ToBase64String(ProtectedData.Protect(veri, Entropi, DataProtectionScope.CurrentUser));

        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var sifreli = new byte[veri.Length];
        using (var aes = new AesGcm(Anahtar(), 16))
            aes.Encrypt(nonce, veri, sifreli, tag);
        return "aes:" + Convert.ToBase64String(nonce.Concat(tag).Concat(sifreli).ToArray());
    }

    public string Coz(string korunmus)
    {
        if (korunmus.StartsWith("dpapi:", StringComparison.Ordinal))
        {
            if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("DPAPI ile korunan değer yalnızca Windows'ta çözülebilir.");
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(korunmus[6..]), Entropi, DataProtectionScope.CurrentUser));
        }
        if (korunmus.StartsWith("aes:", StringComparison.Ordinal))
        {
            var b = Convert.FromBase64String(korunmus[4..]);
            var acik = new byte[b.Length - 28];
            using var aes = new AesGcm(Anahtar(), 16);
            aes.Decrypt(b[..12], b[28..], b[12..28], acik);
            return Encoding.UTF8.GetString(acik);
        }
        throw new InvalidOperationException("Tanınmayan şifreli değer biçimi.");
    }

    private static readonly byte[] Entropi = Encoding.UTF8.GetBytes("PayControl.v1");

    private byte[] Anahtar()
    {
        if (File.Exists(_anahtarDosyasi)) return File.ReadAllBytes(_anahtarDosyasi);
        Directory.CreateDirectory(Path.GetDirectoryName(_anahtarDosyasi)!);
        var k = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(_anahtarDosyasi, k);
        return k;
    }
}
