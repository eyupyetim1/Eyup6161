using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Infrastructure.Data;

public sealed class YedekServisi : IYedekServisi
{
    private static readonly byte[] Imza = "PCYEDEK1"u8.ToArray();
    private const int Iterasyon = 200_000;

    private readonly PayControlDbContext _db;
    private readonly VeritabaniBilgisi _vt;
    private readonly IAyarServisi _ayar;
    private readonly ISifreKoruyucu _koruyucu;
    private readonly IYetkiServisi _yetki;
    private readonly IServiceProvider? _sp;

    public YedekServisi(PayControlDbContext db, VeritabaniBilgisi vt, IAyarServisi ayar, ISifreKoruyucu koruyucu, IYetkiServisi yetki, IServiceProvider? sp = null)
    {
        _sp = sp;
        _db = db;
        _vt = vt;
        _ayar = ayar;
        _koruyucu = koruyucu;
        _yetki = yetki;
    }

    private void SqliteGerekli()
    {
        if (!_vt.SqliteMi || _vt.DosyaYolu is null)
            throw new NotSupportedException("Sunucu veritabanında (PostgreSQL) yedekleme sunucu tarafında yapılır (pg_dump / zamanlanmış sunucu yedeği).");
    }

    public async Task<YedekKaydi> YedekAlAsync(string kullanici, bool otomatik = false, string? hedefKlasor = null, CancellationToken ct = default)
    {
        SqliteGerekli();
        var klasor = hedefKlasor ?? await _ayar.GetirAsync(AyarAnahtarlari.YedekKlasoru, ct)
                     ?? Path.Combine(Path.GetDirectoryName(_vt.DosyaYolu!)!, "yedekler");
        var kayit = new YedekKaydi { Otomatik = otomatik, Kullanici = kullanici };
        try
        {
            Directory.CreateDirectory(klasor);
            var gecici = Path.Combine(Path.GetTempPath(), $"pc-yedek-{Guid.NewGuid():N}.db");
            try
            {
                await using (var kaynak = new SqliteConnection($"Data Source={_vt.DosyaYolu};Pooling=False"))
                await using (var hedef = new SqliteConnection($"Data Source={gecici};Pooling=False"))
                {
                    await kaynak.OpenAsync(ct);
                    await hedef.OpenAsync(ct);
                    kaynak.BackupDatabase(hedef);
                }

                var dbBytes = await File.ReadAllBytesAsync(gecici, ct);
                var manifest = JsonSerializer.Serialize(new
                {
                    Uygulama = "PayControl",
                    Surum = typeof(YedekServisi).Assembly.GetName().Version?.ToString(),
                    Zaman = DateTime.UtcNow,
                    VeritabaniSha256 = Convert.ToHexString(SHA256.HashData(dbBytes)),
                    Kullanici = kullanici
                });

                using var zipAkis = new MemoryStream();
                using (var zip = new ZipArchive(zipAkis, ZipArchiveMode.Create, true))
                {
                    await using (var e = zip.CreateEntry("paycontrol.db", CompressionLevel.Optimal).Open()) await e.WriteAsync(dbBytes, ct);
                    await using (var m = zip.CreateEntry("manifest.json").Open()) await m.WriteAsync(Encoding.UTF8.GetBytes(manifest), ct);
                }
                var zipBytes = zipAkis.ToArray();

                var sifreKorunmus = await _ayar.GetirAsync(AyarAnahtarlari.YedekSifresi, ct);
                var sifre = string.IsNullOrEmpty(sifreKorunmus) ? null : _koruyucu.Coz(sifreKorunmus);
                var cikti = sifre is null ? zipBytes : Sifrele(zipBytes, sifre);

                var ad = $"PayControl-Yedek-{DateTime.Now:yyyyMMdd-HHmmss}{(otomatik ? "-oto" : "")}.{(sifre is null ? "zip" : "pcyedek")}";
                var yol = Path.Combine(klasor, ad);
                await File.WriteAllBytesAsync(yol, cikti, ct);

                kayit.DosyaYolu = yol;
                kayit.Boyut = cikti.Length;
                kayit.Sha256 = Convert.ToHexString(SHA256.HashData(cikti));
                kayit.Sifreli = sifre is not null;
                kayit.Basarili = true;
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                try { File.Delete(gecici); } catch { /* yoksay */ }
            }

            await EskileriTemizleAsync(klasor, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or CryptographicException)
        {
            kayit.Basarili = false;
            kayit.Hata = ex.Message;
        }

        _db.YedekKayitlari.Add(kayit);
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(YedekKaydi),
            VarlikId = kayit.Id,
            Islem = kayit.Basarili ? "YedekAlindi" : "YedekHatasi",
            YeniDeger = kayit.Basarili ? $"{kayit.DosyaYolu} ({kayit.Boyut:N0} bayt, şifreli={kayit.Sifreli})" : kayit.Hata,
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
        if (!kayit.Basarili && _sp?.GetService(typeof(IBildirimServisi)) is IBildirimServisi b)
        {
            try
            {
                await b.OlayBildirAsync(BildirimTetikleyici.YedekHatasi, kayit.Id.ToString("N"), "Personel Yönetim Sistemi: yedekleme başarısız",
                    $"{DateTime.Now:dd.MM.yyyy HH:mm} yedekleme başarısız oldu: {kayit.Hata}", null, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException) { /* bildirim hatası yedek kaydını etkilemez */ }
        }
        return kayit;
    }

    public async Task<YedekKaydi?> OtomatikYedekGerekirseAsync(CancellationToken ct = default)
    {
        if (!_vt.SqliteMi) return null;
        var acik = await _ayar.GetirAsync(AyarAnahtarlari.OtomatikYedek, ct);
        if (acik == "false") return null;
        var son = await _db.YedekKayitlari.AsNoTracking()
            .Where(y => y.Basarili && y.Otomatik).OrderByDescending(y => y.Zaman).FirstOrDefaultAsync(ct);
        if (son is not null && son.Zaman > DateTime.UtcNow.AddHours(-24)) return null;
        return await YedekAlAsync("sistem", true, null, ct);
    }

    public async Task<string> GeriYukleAsync(string dosyaYolu, string? sifre, string kullanici, CancellationToken ct = default)
    {
        SqliteGerekli();
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Yonetici, "Yedekten geri yükleme", ct);
        if (!File.Exists(dosyaYolu)) throw new InvalidOperationException("Yedek dosyası bulunamadı.");

        var ham = await File.ReadAllBytesAsync(dosyaYolu, ct);
        byte[] zipBytes;
        if (ham.AsSpan().StartsWith(Imza))
        {
            if (string.IsNullOrEmpty(sifre)) throw new InvalidOperationException("Bu yedek şifreli; yedek şifresini girin.");
            try { zipBytes = Coz(ham, sifre); }
            catch (CryptographicException) { throw new InvalidOperationException("Yedek şifresi hatalı veya dosya bozuk."); }
        }
        else zipBytes = ham;

        var gecici = Path.Combine(Path.GetTempPath(), $"pc-geri-{Guid.NewGuid():N}.db");
        try
        {
            using (var zip = new ZipArchive(new MemoryStream(zipBytes), ZipArchiveMode.Read))
            {
                var giris = zip.GetEntry("paycontrol.db") ?? throw new InvalidOperationException("Geçerli bir Personel Yönetim Sistemi yedeği değil.");
                var manifestGiris = zip.GetEntry("manifest.json");
                await using (var e = giris.Open())
                await using (var f = File.Create(gecici))
                    await e.CopyToAsync(f, ct);
                if (manifestGiris is not null)
                {
                    using var r = new StreamReader(manifestGiris.Open());
                    var m = JsonDocument.Parse(await r.ReadToEndAsync(ct));
                    var beklenen = m.RootElement.GetProperty("VeritabaniSha256").GetString();
                    var gercek = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(gecici, ct)));
                    if (!string.Equals(beklenen, gercek, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Yedek bütünlük doğrulaması başarısız (SHA-256 uyuşmuyor).");
                }
            }

            await using (var kontrol = new SqliteConnection($"Data Source={gecici};Pooling=False;Mode=ReadOnly"))
            {
                await kontrol.OpenAsync(ct);
                await using var cmd = kontrol.CreateCommand();
                cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name IN ('Personeller','MaliHareketler')";
                if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) < 2)
                    throw new InvalidOperationException("Yedek dosyası Personel Yönetim Sistemi veritabanı içermiyor.");
            }

            var guvenlik = await YedekAlAsync(kullanici, false, null, ct);
            if (!guvenlik.Basarili) throw new InvalidOperationException($"Geri yüklemeden önce güvenlik yedeği alınamadı: {guvenlik.Hata}");

            _db.ChangeTracker.Clear();
            await _db.Database.CloseConnectionAsync();
            SqliteConnection.ClearAllPools();
            await using (var kaynak = new SqliteConnection($"Data Source={gecici};Pooling=False"))
            await using (var hedef = new SqliteConnection($"Data Source={_vt.DosyaYolu};Pooling=False"))
            {
                await kaynak.OpenAsync(ct);
                await hedef.OpenAsync(ct);
                kaynak.BackupDatabase(hedef);
            }
            SqliteConnection.ClearAllPools();

            await SemaYukseltici.YukseltAsync(_db, ct);
            _db.DenetimKayitlari.Add(new DenetimKaydi
            {
                VarlikTuru = nameof(YedekKaydi),
                Islem = "GeriYukleme",
                YeniDeger = Path.GetFileName(dosyaYolu),
                DegisiklikNedeni = $"Önceki durumun güvenlik yedeği: {guvenlik.DosyaYolu}",
                KullaniciAdi = kullanici
            });
            await _db.SaveChangesAsync(ct);
            return $"Yedek geri yüklendi. Önceki durumun güvenlik yedeği: {guvenlik.DosyaYolu}. Uygulamayı yeniden başlatın.";
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { File.Delete(gecici); } catch { /* yoksay */ }
        }
    }

    public async Task<IReadOnlyList<YedekKaydi>> ListeleAsync(CancellationToken ct = default) =>
        await _db.YedekKayitlari.AsNoTracking().OrderByDescending(y => y.Zaman).Take(200).ToListAsync(ct);

    public async Task YedekSifresiAyarlaAsync(string? sifre, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Yonetici, "Yedek şifresi", ct);
        if (!string.IsNullOrEmpty(sifre) && sifre.Length < 8)
            throw new InvalidOperationException("Yedek şifresi en az 8 karakter olmalıdır.");
        await _ayar.AyarlaAsync(AyarAnahtarlari.YedekSifresi, string.IsNullOrEmpty(sifre) ? null : _koruyucu.Koru(sifre), kullanici, ct);
    }

    private async Task EskileriTemizleAsync(string klasor, CancellationToken ct)
    {
        var sayi = int.TryParse(await _ayar.GetirAsync(AyarAnahtarlari.YedekSaklamaSayisi, ct), out var n) && n > 0 ? n : 30;
        var dosyalar = new DirectoryInfo(klasor).GetFiles("PayControl-Yedek-*")
            .OrderByDescending(f => f.CreationTimeUtc).ThenByDescending(f => f.Name).Skip(sayi);
        foreach (var f in dosyalar)
            try { f.Delete(); } catch { /* yoksay */ }
    }

    private static byte[] Sifrele(byte[] veri, string sifre)
    {
        var tuz = RandomNumberGenerator.GetBytes(16);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var anahtar = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(sifre), tuz, Iterasyon, HashAlgorithmName.SHA256, 32);
        var tag = new byte[16];
        var ct = new byte[veri.Length];
        using (var aes = new AesGcm(anahtar, 16)) aes.Encrypt(nonce, veri, ct, tag);
        return Imza.Concat(tuz).Concat(nonce).Concat(tag).Concat(ct).ToArray();
    }

    private static byte[] Coz(byte[] veri, string sifre)
    {
        var tuz = veri[8..24];
        var nonce = veri[24..36];
        var tag = veri[36..52];
        var ct = veri[52..];
        var anahtar = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(sifre), tuz, Iterasyon, HashAlgorithmName.SHA256, 32);
        var acik = new byte[ct.Length];
        using var aes = new AesGcm(anahtar, 16);
        aes.Decrypt(nonce, ct, tag, acik);
        return acik;
    }
}
