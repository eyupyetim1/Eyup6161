using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

public sealed class OdemeOncesiKontrolServisi : IOdemeOncesiKontrolServisi
{
    private readonly IPayControlDbContext _db;
    private readonly IDonemKapanisServisi _donem;
    private readonly IYetkiServisi _yetki;

    public OdemeOncesiKontrolServisi(IPayControlDbContext db, IDonemKapanisServisi donem, IYetkiServisi yetki)
    {
        _db = db;
        _donem = donem;
        _yetki = yetki;
    }

    public Task<OdemeKontrolOturumu> KontrolBaslatAsync(
        int donemYil,
        int donemAy,
        IReadOnlyList<BankaTalimatSatiri> bankaTalimatlari,
        string? bankaDosyaAdi,
        string? bankaHash,
        string kullanici,
        CancellationToken ct = default) =>
        KontrolBaslatAsync(donemYil, donemAy, new BankaTalimatDosyasi { Satirlar = bankaTalimatlari },
            bankaDosyaAdi, bankaHash, kullanici, null, ct);

    public async Task<OdemeKontrolOturumu> KontrolBaslatAsync(
        int donemYil,
        int donemAy,
        BankaTalimatDosyasi bankaDosyasi,
        string? bankaDosyaAdi,
        string? bankaHash,
        string kullanici,
        Guid? bordroIceAktarmaId = null,
        CancellationToken ct = default,
        string? tesis = null)
    {
        tesis = string.IsNullOrWhiteSpace(tesis) ? null : tesis.Trim();
        await _donem.EnsureDonemAcikAsync(donemYil, donemAy, ct);

        if (!await _db.MaasDonemKayitlari.AnyAsync(m => m.DonemYil == donemYil && m.DonemAy == donemAy && (tesis == null || m.Isyeri == tesis), ct))
            throw new InvalidOperationException(
                $"{donemYil}-{donemAy:D2} dönemi için {(tesis is null ? "" : tesis + " tesisine ait ")}içe aktarılmış bordro yok. Önce bordro dosyasını yükleyin.");

        if (bankaDosyasi.Satirlar.Count == 0)
            throw new InvalidOperationException("Banka ödeme dosyasında okunabilir talimat satırı yok.");
        var cift = bankaDosyasi.Satirlar.GroupBy(s => s.SatirNo).FirstOrDefault(g => g.Count() > 1);
        if (cift is not null)
            throw new InvalidOperationException($"Banka talimat satır numarası tekrarlıyor: {cift.Key}.");

        var bordroAktarma = bordroIceAktarmaId.HasValue
            ? await _db.IceAktarmalar.AsNoTracking().FirstOrDefaultAsync(i => i.Id == bordroIceAktarmaId, ct)
            : await _db.IceAktarmalar.AsNoTracking()
                .Where(i => i.DonemYil == donemYil && i.DonemAy == donemAy && !i.GeriAlindi && i.KaynakTuru == "BordroExcel")
                .OrderByDescending(i => i.IceAktarmaZamani).FirstOrDefaultAsync(ct);
        if (bordroAktarma is { GeriAlindi: true })
            throw new InvalidOperationException("Seçilen bordro içe aktarması geri alınmış.");
        if (bordroAktarma is not null && (bordroAktarma.DonemYil != donemYil || bordroAktarma.DonemAy != donemAy))
            throw new InvalidOperationException("Seçilen bordro farklı bir döneme ait.");

        var oturum = new OdemeKontrolOturumu
        {
            DonemYil = donemYil,
            DonemAy = donemAy,
            Tesis = tesis,
            BankaDosyaAdi = bankaDosyaAdi,
            BankaDosyaHash = bankaHash,
            BordroIceAktarmaId = bordroAktarma?.Id,
            BordroDosyaAdi = bordroAktarma?.DosyaAdi,
            BordroDosyaHash = bordroAktarma?.DosyaHashSha256,
            BankaSablonJson = bankaDosyasi.Sablon.Basliklar.Count > 0 ? JsonSerializer.Serialize(bankaDosyasi.Sablon) : null,
            OlusturanKullanici = kullanici
        };

        foreach (var t in bankaDosyasi.Satirlar)
        {
            oturum.Talimatlar.Add(new OdemeTalimatSatiri
            {
                SatirNo = t.SatirNo,
                TcKimlikNo = t.TcKimlikNo,
                AdSoyad = t.AdSoyad,
                Iban = IbanKurallari.Normalize(t.Iban),
                Tutar = t.Tutar,
                OrijinalIban = IbanKurallari.Normalize(t.Iban),
                OrijinalTutar = t.Tutar,
                HamHucrelerJson = bankaDosyasi.HamHucreler.TryGetValue(t.SatirNo, out var ham) ? JsonSerializer.Serialize(ham) : null
            });
        }

        var satirlar = await DegerlendirAsync(oturum, ct);
        foreach (var s in satirlar)
            oturum.Satirlar.Add(s);

        _db.OdemeKontrolOturumlari.Add(oturum);

        var kapanis = await _donem.GetOrCreateAsync(donemYil, donemAy, ct: ct);
        if (kapanis.Durum is MaasDonemDurumu.Hazirlaniyor or MaasDonemDurumu.BordroYuklendi)
            await _donem.DurumGuncelleAsync(kapanis.Id, MaasDonemDurumu.OdemeOncesiKontrolde, kullanici, ct);

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(OdemeKontrolOturumu),
            VarlikId = oturum.Id,
            Islem = "OdemeOncesiKontrol",
            YeniDeger = $"{donemYil}-{donemAy:D2} | bordro={bordroAktarma?.DosyaAdi} | banka={bankaDosyaAdi} | {oturum.Satirlar.Count} satır | kritik={oturum.Satirlar.Count(s => s.KritikMi)}",
            KullaniciAdi = kullanici
        });

        await _db.SaveChangesAsync(ct);
        return oturum;
    }

    public async Task<OdemeKontrolOturumu> TalimatDuzeltAsync(
        Guid oturumId,
        int talimatSatirNo,
        string? yeniIban,
        decimal? yeniTutar,
        bool haric,
        string gerekce,
        string kullanici,
        CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Banka talimatı düzeltme", ct);
        if (string.IsNullOrWhiteSpace(gerekce))
            throw new InvalidOperationException("Düzeltme gerekçesi zorunludur.");
        if (yeniTutar is <= 0)
            throw new InvalidOperationException("Tutar pozitif olmalıdır; ödemeyi kaldırmak için 'listeden çıkar' seçin.");

        var oturum = await OturumYukleAsync(oturumId, ct);
        var talimat = oturum.Talimatlar.FirstOrDefault(t => t.SatirNo == talimatSatirNo)
                      ?? throw new InvalidOperationException($"Talimat satırı bulunamadı: {talimatSatirNo}.");

        var eski = $"IBAN={talimat.Iban}, Tutar={talimat.Tutar:N2}, Hariç={talimat.Haric}";
        if (yeniIban is not null) talimat.Iban = IbanKurallari.Normalize(yeniIban);
        if (yeniTutar.HasValue) talimat.Tutar = yeniTutar.Value;
        talimat.Haric = haric;
        talimat.Duzeltildi = true;
        talimat.DuzeltmeGerekcesi = gerekce;
        talimat.DuzeltenKullanici = kullanici;
        talimat.DuzeltmeZamani = DateTime.UtcNow;

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(OdemeTalimatSatiri),
            VarlikId = talimat.Id,
            Islem = "TalimatDuzeltme",
            AlanAdi = $"Satır {talimatSatirNo}",
            EskiDeger = eski,
            YeniDeger = $"IBAN={talimat.Iban}, Tutar={talimat.Tutar:N2}, Hariç={talimat.Haric}",
            DegisiklikNedeni = gerekce,
            KullaniciAdi = kullanici
        });

        ListeleriGecersizKil(oturum, $"Liste üretildikten sonra talimat düzeltildi (satır {talimatSatirNo}); yeni sürüm gerekir.");
        await SatirlariYenileAsync(oturum, ct);
        oturum.SonDegisiklikZamani = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return oturum;
    }

    public async Task<OdemeKontrolOturumu> YenidenDegerlendirAsync(Guid oturumId, string kullanici, CancellationToken ct = default)
    {
        var oturum = await OturumYukleAsync(oturumId, ct);
        await _donem.EnsureDonemAcikAsync(oturum.DonemYil, oturum.DonemAy, ct);
        var degisti = await SatirlariYenileAsync(oturum, ct);
        if (degisti)
        {
            ListeleriGecersizKil(oturum, "Yeniden değerlendirmede kontrol sonuçları değişti; yeni sürüm gerekir.");
            oturum.SonDegisiklikZamani = DateTime.UtcNow;
        }
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(OdemeKontrolOturumu),
            VarlikId = oturum.Id,
            Islem = "YenidenDegerlendirme",
            YeniDeger = degisti ? "Sonuçlar değişti" : "Değişiklik yok",
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
        return oturum;
    }

    public async Task IstisnaOnaylaAsync(
        Guid satirId,
        string gerekce,
        string onaylayan,
        CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(onaylayan, KullaniciRolu.Yonetici, "Kritik ödeme kaydı için istisna onayı", ct);

        var satir = await _db.OdemeKontrolSatirlari.FirstOrDefaultAsync(s => s.Id == satirId, ct)
            ?? throw new InvalidOperationException("Kontrol satırı bulunamadı.");

        if (satir.EslesmeBelirsiz)
            throw new InvalidOperationException(
                "Kimlik veya ödeme sahibi eşleşmesi belirsiz kayıtlar istisna ile onaylanamaz. Talimatı düzeltin veya listeden çıkarın.");
        if (!satir.KritikMi)
            throw new InvalidOperationException("Bu satırda kritik uyarı yok; istisna onayı gerekmez.");
        if (string.IsNullOrWhiteSpace(gerekce) || gerekce.Trim().Length < 10)
            throw new InvalidOperationException("İstisna gerekçesi zorunludur (en az 10 karakter).");

        satir.IstisnaOnayi = true;
        satir.IstisnaGerekcesi = gerekce.Trim();
        satir.IstisnaOnaylayan = onaylayan;
        satir.IstisnaOnayZamani = DateTime.UtcNow;
        satir.Onaylandi = true;

        var oturum = await OturumYukleAsync(satir.OdemeKontrolOturumuId, ct);
        ListeleriGecersizKil(oturum, "Liste üretildikten sonra istisna onayı verildi; yeni sürüm gerekir.");
        oturum.SonDegisiklikZamani = DateTime.UtcNow;

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            PersonelId = satir.PersonelId,
            VarlikTuru = nameof(OdemeKontrolSatiri),
            VarlikId = satir.Id,
            Islem = "IstisnaOnayi",
            AlanAdi = satir.Sonuc.ToString(),
            EskiDeger = satir.Gerekce,
            YeniDeger = $"Banka {satir.BankaOdemeTutari:N2} / Bordro {satir.BordroNetIstihkak:N2}",
            DegisiklikNedeni = gerekce,
            KullaniciAdi = onaylayan
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<OnayliOdemeListesi> OnayliListeUretAsync(
        Guid oturumId,
        string kullanici,
        string hedefKlasor,
        CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Onaylı banka ödeme listesi oluşturma", ct);
        var oturum = await OturumYukleAsync(oturumId, ct);
        await _donem.EnsureDonemAcikAsync(oturum.DonemYil, oturum.DonemAy, ct);

        // Kontrol ile liste arasında veri değiştiyse (yeni ödeme, çıkış kaydı vb.) liste üretilmez.
        if (await SatirlariYenileAsync(oturum, ct))
        {
            oturum.SonDegisiklikZamani = DateTime.UtcNow;
            _db.DenetimKayitlari.Add(new DenetimKaydi
            {
                VarlikTuru = nameof(OdemeKontrolOturumu),
                VarlikId = oturum.Id,
                Islem = "ListeUretimiDurduruldu",
                DegisiklikNedeni = "Kontrolden sonra kaynak veriler değişti",
                KullaniciAdi = kullanici
            });
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException(
                "Kontrol yapıldıktan sonra personel/ödeme verileri değişti. Kontrol sonuçları güncellendi; satırları yeniden inceleyin.");
        }

        var kritikCozulmemis = oturum.Satirlar.Where(s => s.KritikMi && !s.OnayliListeyeAlinabilir).ToList();
        if (kritikCozulmemis.Count > 0)
            throw new InvalidOperationException(
                $"Kritik uyarılı {kritikCozulmemis.Count} kayıt çözülmeden onaylı banka ödeme listesi oluşturulamaz: " +
                string.Join("; ", kritikCozulmemis.Take(5).Select(s => $"{s.AdSoyad ?? s.TcKimlikNo} ({s.Sonuc})")));

        var talimatBySatir = oturum.Talimatlar.ToDictionary(t => t.SatirNo);
        var listeSatirlari = new List<(OdemeKontrolSatiri Kontrol, OdemeTalimatSatiri Talimat)>();
        foreach (var s in oturum.Satirlar.Where(s => s.OnayliListeyeAlinabilir && s.PersonelId.HasValue && s.BankaOdemeTutari > 0))
        {
            foreach (var no in (s.TalimatSatirNolari ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse))
                if (talimatBySatir.TryGetValue(no, out var t) && !t.Haric)
                    listeSatirlari.Add((s, t));
        }
        if (listeSatirlari.Count == 0)
            throw new InvalidOperationException("Onaylı listeye alınabilecek ödeme satırı yok.");
        // Yüklenen banka dosyasındaki sıra korunur.
        listeSatirlari = listeSatirlari.OrderBy(x => x.Talimat.SatirNo).ToList();

        var oncekiSurum = await _db.OnayliOdemeListeleri
            .Where(l => l.DonemYil == oturum.DonemYil && l.DonemAy == oturum.DonemAy && l.Tesis == oturum.Tesis)
            .Select(l => (int?)l.Surum).MaxAsync(ct) ?? 0;
        var surum = oncekiSurum + 1;

        Directory.CreateDirectory(hedefKlasor);
        var tesisEki = oturum.Tesis is null ? "" : "_" + new string(oturum.Tesis.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        var dosyaAdi = $"OnayliOdeme_{oturum.DonemYil}-{oturum.DonemAy:D2}{tesisEki}_v{surum}.xlsx";
        var yol = Path.Combine(hedefKlasor, dosyaAdi);
        var sablon = string.IsNullOrWhiteSpace(oturum.BankaSablonJson)
            ? null
            : JsonSerializer.Deserialize<BankaDosyaSablonu>(oturum.BankaSablonJson);
        ExcelYaz(yol, oturum, sablon, listeSatirlari, surum, kullanici);

        var bytes = await File.ReadAllBytesAsync(yol, ct);
        var liste = new OnayliOdemeListesi
        {
            OdemeKontrolOturumuId = oturum.Id,
            Surum = surum,
            DonemYil = oturum.DonemYil,
            DonemAy = oturum.DonemAy,
            Tesis = oturum.Tesis,
            DosyaAdi = dosyaAdi,
            DosyaYolu = yol,
            DosyaHashSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            SatirSayisi = listeSatirlari.Count,
            ToplamTutar = listeSatirlari.Sum(x => x.Talimat.Tutar),
            OlusturanKullanici = kullanici,
            BordroIceAktarmaId = oturum.BordroIceAktarmaId,
            BordroDosyaAdi = oturum.BordroDosyaAdi,
            KaynakFormatNotu = sablon is null
                ? "Varsayılan format (TC, Ad, IBAN, Tutar)"
                : $"Yüklenen banka dosyası formatı ({oturum.BankaDosyaAdi}): {string.Join(" | ", sablon.Basliklar.OrderBy(k => k.Key).Select(k => k.Value))}"
        };
        foreach (var (k, t) in listeSatirlari)
        {
            liste.Satirlar.Add(new OnayliOdemeListesiSatiri
            {
                OdemeKontrolSatiriId = k.Id,
                PersonelId = k.PersonelId,
                TalimatSatirNo = t.SatirNo,
                TcKimlikNo = k.TcKimlikNo,
                AdSoyad = k.AdSoyad,
                Iban = t.Iban,
                Tutar = t.Tutar,
                KontrolSonucu = k.Sonuc.ToString(),
                IstisnaOnayi = k.IstisnaOnayi,
                IstisnaGerekcesi = k.IstisnaGerekcesi,
                IstisnaOnaylayan = k.IstisnaOnaylayan
            });
        }

        // Aynı dönemin önceki geçerli listeleri yeni sürümle geçersiz olur (dosyalar silinmez).
        var oncekiler = await _db.OnayliOdemeListeleri
            .Where(l => l.DonemYil == oturum.DonemYil && l.DonemAy == oturum.DonemAy && l.Gecerli && l.Tesis == oturum.Tesis)
            .ToListAsync(ct);
        foreach (var o in oncekiler)
        {
            o.Gecerli = false;
            o.YerineGecenListeId = liste.Id;
            o.GecersizlikNedeni ??= $"v{surum} sürümü üretildi.";
        }

        oturum.KontrolTamamlandi = true;
        oturum.OnayZamani = DateTime.UtcNow;
        oturum.OnaylayanKullanici = kullanici;
        _db.OnayliOdemeListeleri.Add(liste);

        var kapanis = await _donem.GetOrCreateAsync(oturum.DonemYil, oturum.DonemAy, ct: ct);
        if (kapanis.Durum < MaasDonemDurumu.OdemeListesiOnaylandi)
            await _donem.DurumGuncelleAsync(kapanis.Id, MaasDonemDurumu.OdemeListesiOnaylandi, kullanici, ct);

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(OnayliOdemeListesi),
            VarlikId = liste.Id,
            Islem = "OnayliListeUretildi",
            YeniDeger = $"{dosyaAdi} | v{surum} | {liste.SatirSayisi} satır | {liste.ToplamTutar:N2} | bordro={oturum.BordroDosyaAdi} | SHA256={liste.DosyaHashSha256}",
            KullaniciAdi = kullanici
        });

        await _db.SaveChangesAsync(ct);
        return liste;
    }

    public async Task<OdemeKontrolOturumu?> GetirAsync(Guid oturumId, CancellationToken ct = default) =>
        await _db.OdemeKontrolOturumlari
            .Include(o => o.Satirlar)
            .Include(o => o.Talimatlar)
            .Include(o => o.OnayliListeler).ThenInclude(l => l.Satirlar)
            .FirstOrDefaultAsync(o => o.Id == oturumId, ct);

    public async Task<IReadOnlyList<OnayliOdemeListesi>> DonemListeleriAsync(int donemYil, int donemAy, CancellationToken ct = default) =>
        await _db.OnayliOdemeListeleri.AsNoTracking()
            .Include(l => l.Satirlar)
            .Where(l => l.DonemYil == donemYil && l.DonemAy == donemAy)
            .OrderByDescending(l => l.Surum)
            .ToListAsync(ct);

    // ----------------------------------------------------------------------------------------

    private async Task<OdemeKontrolOturumu> OturumYukleAsync(Guid oturumId, CancellationToken ct) =>
        await _db.OdemeKontrolOturumlari
            .Include(o => o.Satirlar)
            .Include(o => o.Talimatlar)
            .Include(o => o.OnayliListeler)
            .FirstOrDefaultAsync(o => o.Id == oturumId, ct)
        ?? throw new InvalidOperationException("Kontrol oturumu bulunamadı.");

    private static void ListeleriGecersizKil(OdemeKontrolOturumu oturum, string neden)
    {
        foreach (var l in oturum.OnayliListeler.Where(l => l.Gecerli))
        {
            l.Gecerli = false;
            l.GecersizlikNedeni = neden;
        }
    }

    /// <summary>Oturumu güncel verilerle yeniden değerlendirir; sonuç değiştiyse true döner.</summary>
    private async Task<bool> SatirlariYenileAsync(OdemeKontrolOturumu oturum, CancellationToken ct)
    {
        var yeni = await DegerlendirAsync(oturum, ct);
        var eskiler = oturum.Satirlar.ToList();

        static string Anahtar(OdemeKontrolSatiri s) => $"{s.PersonelId}|{s.TcKimlikNo}|{s.TalimatSatirNolari}";
        static string Imza(OdemeKontrolSatiri s) =>
            $"{Anahtar(s)}|{s.Sonuc}|{s.KritikMi}|{s.EslesmeBelirsiz}|{s.BankaOdemeTutari}|{s.BankaIban}|{s.BordroNetIstihkak}";

        var eskiImzalar = eskiler.Select(Imza).OrderBy(x => x).ToList();
        var yeniImzalar = yeni.Select(Imza).OrderBy(x => x).ToList();
        if (eskiImzalar.SequenceEqual(yeniImzalar))
            return false;

        // Değişmeyen satırların istisna onayı korunur; değişenlerinki düşer (yeniden onay gerekir).
        var eskiByImza = eskiler.Where(s => s.IstisnaOnayi).GroupBy(Imza).ToDictionary(g => g.Key, g => g.First());
        foreach (var s in yeni)
        {
            if (eskiByImza.TryGetValue(Imza(s), out var e))
            {
                s.IstisnaOnayi = true;
                s.IstisnaGerekcesi = e.IstisnaGerekcesi;
                s.IstisnaOnaylayan = e.IstisnaOnaylayan;
                s.IstisnaOnayZamani = e.IstisnaOnayZamani;
                s.Onaylandi = true;
            }
        }

        foreach (var e in eskiler)
        {
            oturum.Satirlar.Remove(e);
            _db.OdemeKontrolSatirlari.Remove(e);
        }
        foreach (var s in yeni)
        {
            s.OdemeKontrolOturumuId = oturum.Id;
            oturum.Satirlar.Add(s);
            _db.OdemeKontrolSatirlari.Add(s);
        }
        return true;
    }

    private async Task<IReadOnlyList<OdemeKontrolSatiri>> DegerlendirAsync(OdemeKontrolOturumu oturum, CancellationToken ct)
    {
        var yil = oturum.DonemYil;
        var ay = oturum.DonemAy;
        var onceki = new DateOnly(yil, ay, 1).AddMonths(-1);

        var bordro = await _db.MaasDonemKayitlari.AsNoTracking().Include(m => m.Kalemler)
            .Where(m => m.DonemYil == yil && m.DonemAy == ay && (oturum.Tesis == null || m.Isyeri == oturum.Tesis)).ToListAsync(ct);
        var oncekiBordro = await _db.MaasDonemKayitlari.AsNoTracking().Include(m => m.Kalemler)
            .Where(m => m.DonemYil == onceki.Year && m.DonemAy == onceki.Month).ToListAsync(ct);
        var personeller = await _db.Personeller.AsNoTracking().Include(p => p.CalismaDonemleri).ToListAsync(ct);
        var odemeler = await _db.BankaOdemeleri.AsNoTracking()
            .Where(o => o.HakEdisDonemYil == yil && o.HakEdisDonemAy == ay).ToListAsync(ct);
        var cikislar = await _db.IstenCikisKayitlari.AsNoTracking().ToListAsync(ct);
        var avanslar = await _db.Avanslar.AsNoTracking().Include(a => a.Mahsuplar).ToListAsync(ct);

        var duzeltmeler = (await _db.DonemDuzeltmeler.AsNoTracking()
                .Where(d => d.UygulanacakDonemYil == yil && d.UygulanacakDonemAy == ay && d.PersonelId != null && d.FarkTutari != null)
                .ToListAsync(ct))
            .GroupBy(d => d.PersonelId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.FarkTutari!.Value));

        var supheli = (await _db.MaliHareketler.AsNoTracking()
                .Where(h => h.BakiyeTuru == BakiyeTuru.SupheliFazlaOdeme).ToListAsync(ct))
            .GroupBy(h => h.PersonelId)
            .ToDictionary(g => g.Key, g => g.Sum(MaliHareketKurallari.Etki));

        var oncekiListeler = await _db.OnayliOdemeListesiSatirlari.AsNoTracking()
            .Where(s => s.PersonelId != null &&
                        s.OnayliOdemeListesi!.Gecerli &&
                        s.OnayliOdemeListesi.DonemYil == yil &&
                        s.OnayliOdemeListesi.DonemAy == ay &&
                        s.OnayliOdemeListesi.OdemeKontrolOturumuId != oturum.Id)
            .Select(s => new OncekiOnayliOdeme(s.PersonelId!.Value, s.OnayliOdemeListesi!.Surum, s.Tutar, s.OnayliOdemeListesi.DosyaAdi))
            .ToListAsync(ct);

        var talimatlar = oturum.Talimatlar
            .Where(t => !t.Haric)
            .OrderBy(t => t.SatirNo)
            .Select(t => new BankaTalimatSatiri(t.TcKimlikNo, t.AdSoyad, t.Iban, t.Tutar, t.SatirNo))
            .ToList();

        var satirlar = new OdemeOncesiKontrolMotoru().Karsilastir(new OdemeKontrolBaglami
        {
            DonemYil = yil,
            DonemAy = ay,
            Bordro = bordro,
            Talimatlar = talimatlar,
            Personeller = personeller,
            DonemOdemeleri = odemeler,
            Cikislar = cikislar,
            Avanslar = avanslar,
            OncekiAyBordro = oncekiBordro,
            DevredenDuzeltmeler = duzeltmeler,
            AcikSupheliBakiyeler = supheli,
            OncekiOnayliOdemeler = oncekiListeler,
            IcraDosyalari = await _db.IcraDosyalari.AsNoTracking().Include(d => d.Kesintiler).ToListAsync(ct),
            BesTanimlari = await _db.BesTanimlari.AsNoTracking().ToListAsync(ct),
            TahsilatPlanlari = await _db.TahsilatPlanlari.AsNoTracking().Include(t => t.Taksitler).ToListAsync(ct)
        });

        // Listeden çıkarılan talimatlar ilgili kişinin satırında görünür kalsın
        foreach (var t in oturum.Talimatlar.Where(t => t.Haric))
        {
            var ilgili = satirlar.FirstOrDefault(s => !string.IsNullOrEmpty(t.TcKimlikNo) && s.TcKimlikNo == t.TcKimlikNo);
            if (ilgili is not null)
                ilgili.Gerekce += $" [BİLGİ] Banka satırı {t.SatirNo} ({t.Tutar:N2}) kullanıcı tarafından listeden çıkarıldı: {t.DuzeltmeGerekcesi}";
        }

        return satirlar;
    }

    private static void ExcelYaz(
        string yol,
        OdemeKontrolOturumu oturum,
        BankaDosyaSablonu? sablon,
        List<(OdemeKontrolSatiri Kontrol, OdemeTalimatSatiri Talimat)> satirlar,
        int surum,
        string kullanici)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(string.IsNullOrWhiteSpace(sablon?.SayfaAdi) ? "OnayliOdeme" : sablon!.SayfaAdi);

        if (sablon is not null)
        {
            var r = 1;
            foreach (var ust in sablon.UstBilgiSatirlari)
            {
                foreach (var (c, v) in ust) ws.Cell(r, c).Value = v;
                r++;
            }
            foreach (var (c, v) in sablon.Basliklar) ws.Cell(r, c).Value = v;
            ws.Row(r).Style.Font.Bold = true;
            r++;

            var roller = sablon.KolonRolleri;
            foreach (var (k, t) in satirlar)
            {
                var ham = string.IsNullOrWhiteSpace(t.HamHucrelerJson)
                    ? new Dictionary<int, string>()
                    : JsonSerializer.Deserialize<Dictionary<int, string>>(t.HamHucrelerJson) ?? new();
                foreach (var (c, v) in ham) ws.Cell(r, c).Value = v;
                if (roller.TryGetValue("TC", out var tc)) ws.Cell(r, tc).Value = k.TcKimlikNo ?? t.TcKimlikNo;
                if (roller.TryGetValue("AD", out var ad)) ws.Cell(r, ad).Value = t.AdSoyad ?? k.AdSoyad;
                if (roller.TryGetValue("IBAN", out var ib)) ws.Cell(r, ib).Value = t.Iban;
                if (roller.TryGetValue("TUTAR", out var tu))
                {
                    ws.Cell(r, tu).Value = t.Tutar;
                    ws.Cell(r, tu).Style.NumberFormat.Format = string.IsNullOrWhiteSpace(sablon.TutarBicimi) ? "0.00" : sablon.TutarBicimi;
                }
                r++;
            }
        }
        else
        {
            string[] b = ["T.C. KİMLİK NO", "ADI SOYADI", "IBAN NO", "TUTAR"];
            for (var i = 0; i < b.Length; i++) ws.Cell(1, i + 1).Value = b[i];
            ws.Row(1).Style.Font.Bold = true;
            var r = 2;
            foreach (var (k, t) in satirlar)
            {
                ws.Cell(r, 1).Value = k.TcKimlikNo;
                ws.Cell(r, 2).Value = t.AdSoyad ?? k.AdSoyad;
                ws.Cell(r, 3).Value = t.Iban;
                ws.Cell(r, 4).Value = t.Tutar;
                ws.Cell(r, 4).Style.NumberFormat.Format = "#,##0.00";
                r++;
            }
        }
        ws.Columns().AdjustToContents();

        // Kontrol bilgisi ayrı sayfada — banka formatı bozulmaz.
        var k2 = wb.AddWorksheet("Kontrol Bilgisi");
        k2.Cell(1, 1).Value = "PayControl onaylı ödeme listesi — bankaya otomatik gönderilmez";
        k2.Cell(2, 1).Value = $"Dönem: {oturum.DonemYil}-{oturum.DonemAy:D2} | Sürüm: v{surum} | Oluşturan: {kullanici} | {DateTime.Now:dd.MM.yyyy HH:mm}";
        k2.Cell(3, 1).Value = $"Bordro: {oturum.BordroDosyaAdi} | Banka dosyası: {oturum.BankaDosyaAdi} | Kontrol oturumu: {oturum.Id}";
        string[] kb = ["Talimat satırı", "TC", "Ad Soyad", "IBAN", "Tutar", "Bordro net", "Kontrol sonucu", "İstisna", "Gerekçe", "Onaylayan"];
        for (var i = 0; i < kb.Length; i++) k2.Cell(5, i + 1).Value = kb[i];
        k2.Row(5).Style.Font.Bold = true;
        var rr = 6;
        foreach (var (k, t) in satirlar)
        {
            k2.Cell(rr, 1).Value = t.SatirNo;
            k2.Cell(rr, 2).Value = k.TcKimlikNo;
            k2.Cell(rr, 3).Value = k.AdSoyad;
            k2.Cell(rr, 4).Value = t.Iban;
            k2.Cell(rr, 5).Value = t.Tutar;
            k2.Cell(rr, 6).Value = k.BordroNetIstihkak;
            k2.Cell(rr, 7).Value = k.Sonuc.ToString();
            k2.Cell(rr, 8).Value = k.IstisnaOnayi ? "Evet" : "";
            k2.Cell(rr, 9).Value = k.IstisnaOnayi ? k.IstisnaGerekcesi : k.Gerekce;
            k2.Cell(rr, 10).Value = k.IstisnaOnaylayan;
            rr++;
        }
        k2.Cell(rr + 1, 4).Value = "TOPLAM";
        k2.Cell(rr + 1, 5).Value = satirlar.Sum(x => x.Talimat.Tutar);
        k2.Columns(1, 8).AdjustToContents();

        wb.SaveAs(yol);
    }
}
