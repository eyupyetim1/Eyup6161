using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Infrastructure.Data;

/// <summary>Kaynak belgeleri uygulama veri klasöründe saklar; dosya özetiyle bütünlüğü doğrular.</summary>
public sealed class BelgeServisi : IBelgeServisi
{
    private const long AzamiBoyut = 25 * 1024 * 1024;
    private static readonly string[] Uzantilar = [".pdf", ".jpg", ".jpeg", ".png", ".xlsx", ".xls", ".docx", ".doc", ".txt", ".csv", ".tif", ".tiff"];

    private readonly PayControlDbContext _db;
    private readonly IYetkiServisi _yetki;
    private readonly string _depo;

    public BelgeServisi(PayControlDbContext db, IYetkiServisi yetki, VeritabaniBilgisi vt)
    {
        _db = db;
        _yetki = yetki;
        _depo = Path.Combine(vt.DosyaYolu is null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PayControl")
            : Path.GetDirectoryName(vt.DosyaYolu)!, "belgeler");
    }

    public async Task<Belge> EkleAsync(string varlikTuru, Guid? varlikId, Guid? personelId, string kaynakDosya, string baslik, string? aciklama,
        string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Belge ekleme", ct);
        var fi = new FileInfo(kaynakDosya);
        if (!fi.Exists) throw new InvalidOperationException("Belge dosyası bulunamadı.");
        if (fi.Length == 0) throw new InvalidOperationException("Belge dosyası boş.");
        if (fi.Length > AzamiBoyut) throw new InvalidOperationException("Belge 25 MB'tan büyük olamaz.");
        var uzanti = fi.Extension.ToLowerInvariant();
        if (!Uzantilar.Contains(uzanti)) throw new InvalidOperationException($"Desteklenmeyen dosya türü ({uzanti}). İzin verilen: {string.Join(", ", Uzantilar)}.");
        if (string.IsNullOrWhiteSpace(baslik)) baslik = Path.GetFileNameWithoutExtension(fi.Name);

        var belge = new Belge
        {
            VarlikTuru = varlikTuru, VarlikId = varlikId, PersonelId = personelId, Baslik = baslik.Trim(), OrijinalAd = fi.Name,
            Aciklama = aciklama, YukleyenKullanici = kullanici, Boyut = fi.Length
        };
        var klasor = Path.Combine(_depo, DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"));
        Directory.CreateDirectory(klasor);
        var hedef = Path.Combine(klasor, belge.Id.ToString("N") + uzanti);
        File.Copy(fi.FullName, hedef);
        belge.DepoYolu = Path.GetRelativePath(_depo, hedef);
        belge.Sha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(hedef, ct)));

        _db.Belgeler.Add(belge);
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = personelId, VarlikTuru = nameof(Belge), VarlikId = belge.Id, Islem = "BelgeEklendi",
            AlanAdi = $"{varlikTuru} {varlikId}", YeniDeger = $"{belge.Baslik} ({fi.Name}, {fi.Length:N0} bayt, SHA256 {belge.Sha256[..12]}…)",
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
        return belge;
    }

    public async Task<IReadOnlyList<Belge>> ListeleAsync(string? varlikTuru = null, Guid? varlikId = null, Guid? personelId = null, CancellationToken ct = default) =>
        await _db.Belgeler.AsNoTracking()
            .Where(b => (varlikTuru == null || b.VarlikTuru == varlikTuru) && (varlikId == null || b.VarlikId == varlikId) && (personelId == null || b.PersonelId == personelId))
            .OrderByDescending(b => b.YuklemeZamani).ToListAsync(ct);

    public async Task<string> DosyaYoluAsync(Guid id, CancellationToken ct = default)
    {
        var b = await _db.Belgeler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new InvalidOperationException("Belge bulunamadı.");
        var yol = Path.Combine(_depo, b.DepoYolu);
        if (!File.Exists(yol)) throw new InvalidOperationException("Belge dosyası depoda bulunamadı (taşınmış veya silinmiş olabilir).");
        var ozet = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(yol, ct)));
        if (!string.Equals(ozet, b.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Belge bütünlük doğrulaması başarısız: dosya kayıttan sonra değiştirilmiş.");
        return yol;
    }

    public async Task GecersizKilAsync(Guid id, string neden, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Belge geçersiz kılma", ct);
        if (string.IsNullOrWhiteSpace(neden)) throw new InvalidOperationException("Neden zorunludur.");
        var b = await _db.Belgeler.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new InvalidOperationException("Belge bulunamadı.");
        b.Gecersiz = true;
        b.GecersizNedeni = neden;
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = b.PersonelId, VarlikTuru = nameof(Belge), VarlikId = b.Id, Islem = "BelgeGecersiz", DegisiklikNedeni = neden, KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
    }
}
