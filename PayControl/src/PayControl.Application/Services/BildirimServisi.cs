using PayControl.Domain.Services;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Application.Services;

public sealed class BildirimServisi : IBildirimServisi
{
    private const int AzamiDeneme = 3;
    private readonly IPayControlDbContext _db;
    private readonly IYetkiServisi _yetki;
    private readonly ISifreKoruyucu _koruyucu;
    private readonly IEpostaGonderici _eposta;
    private readonly IWhatsAppGonderici _whatsapp;
    private readonly IRaporServisi _rapor;
    private readonly IUyariServisi _uyari;

    public BildirimServisi(IPayControlDbContext db, IYetkiServisi yetki, ISifreKoruyucu koruyucu, IEpostaGonderici eposta,
        IWhatsAppGonderici whatsapp, IRaporServisi rapor, IUyariServisi uyari)
    {
        _db = db;
        _yetki = yetki;
        _koruyucu = koruyucu;
        _eposta = eposta;
        _whatsapp = whatsapp;
        _rapor = rapor;
        _uyari = uyari;
    }

    // ------------------------------------------------------------------ Ayarlar, alıcılar, kurallar

    private async Task<BildirimAyari> AyarAsync(CancellationToken ct)
    {
        var a = await _db.BildirimAyarlari.FirstOrDefaultAsync(ct);
        if (a is not null) return a;
        a = new BildirimAyari();
        _db.BildirimAyarlari.Add(a);
        return a;
    }

    public async Task<BildirimAyarDto> AyarlariGetirAsync(CancellationToken ct = default)
    {
        var a = await _db.BildirimAyarlari.AsNoTracking().FirstOrDefaultAsync(ct) ?? new BildirimAyari();
        return new BildirimAyarDto
        {
            SmtpSunucu = a.SmtpSunucu, SmtpPort = a.SmtpPort, SmtpSsl = a.SmtpSsl, SmtpKullanici = a.SmtpKullanici,
            GonderenEposta = a.GonderenEposta, GonderenAd = a.GonderenAd, WhatsAppEtkin = a.WhatsAppEtkin,
            WhatsAppTelefonNumarasiId = a.WhatsAppTelefonNumarasiId, WhatsAppSablonAdi = a.WhatsAppSablonAdi, WhatsAppDil = a.WhatsAppDil,
            SmtpSifreKayitli = !string.IsNullOrEmpty(a.SmtpSifreSifreli), WhatsAppAnahtarKayitli = !string.IsNullOrEmpty(a.WhatsAppErisimAnahtariSifreli)
        };
    }

    public async Task AyarlariKaydetAsync(BildirimAyarDto dto, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Yonetici, "Bildirim ayarları", ct);
        if (!string.IsNullOrWhiteSpace(dto.GonderenEposta) && !dto.GonderenEposta.Contains('@'))
            throw new InvalidOperationException("Gönderen e-posta adresi geçersiz.");
        if (dto.SmtpPort is < 1 or > 65535) throw new InvalidOperationException("SMTP portu geçersiz.");
        var a = await AyarAsync(ct);
        a.SmtpSunucu = dto.SmtpSunucu?.Trim();
        a.SmtpPort = dto.SmtpPort;
        a.SmtpSsl = dto.SmtpSsl;
        a.SmtpKullanici = dto.SmtpKullanici?.Trim();
        if (!string.IsNullOrEmpty(dto.SmtpSifre)) a.SmtpSifreSifreli = _koruyucu.Koru(dto.SmtpSifre);
        a.GonderenEposta = dto.GonderenEposta?.Trim();
        a.GonderenAd = dto.GonderenAd;
        a.WhatsAppEtkin = dto.WhatsAppEtkin;
        a.WhatsAppTelefonNumarasiId = dto.WhatsAppTelefonNumarasiId?.Trim();
        if (!string.IsNullOrEmpty(dto.WhatsAppErisimAnahtari)) a.WhatsAppErisimAnahtariSifreli = _koruyucu.Koru(dto.WhatsAppErisimAnahtari);
        a.WhatsAppSablonAdi = string.IsNullOrWhiteSpace(dto.WhatsAppSablonAdi) ? null : dto.WhatsAppSablonAdi.Trim();
        a.WhatsAppDil = string.IsNullOrWhiteSpace(dto.WhatsAppDil) ? "tr" : dto.WhatsAppDil;
        a.GuncellemeZamani = DateTime.UtcNow;
        a.GuncelleyenKullanici = kullanici;
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(BildirimAyari), Islem = "BildirimAyarlariGuncellendi",
            YeniDeger = $"SMTP {a.SmtpSunucu}:{a.SmtpPort}, WhatsApp {(a.WhatsAppEtkin ? "açık" : "kapalı")} (gizli değerler maskelendi)",
            KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<BildirimAlicisi>> AlicilarAsync(CancellationToken ct = default) =>
        await _db.BildirimAlicilari.AsNoTracking().OrderBy(a => a.Rol).ThenBy(a => a.AdSoyad).ToListAsync(ct);

    public async Task<BildirimAlicisi> AliciKaydetAsync(BildirimAlicisi alici, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Bildirim alıcısı", ct);
        if (string.IsNullOrWhiteSpace(alici.AdSoyad)) throw new InvalidOperationException("Alıcı adı zorunludur.");
        if (string.IsNullOrWhiteSpace(alici.Eposta) && string.IsNullOrWhiteSpace(alici.Telefon))
            throw new InvalidOperationException("E-posta veya telefon girilmelidir.");
        if (!string.IsNullOrWhiteSpace(alici.Eposta) && !alici.Eposta.Contains('@')) throw new InvalidOperationException("E-posta adresi geçersiz.");
        var telefon = TelefonNormalize(alici.Telefon);
        var mevcut = await _db.BildirimAlicilari.FirstOrDefaultAsync(a => a.Id == alici.Id, ct);
        if (mevcut is null)
        {
            mevcut = new BildirimAlicisi { Id = alici.Id == Guid.Empty ? Guid.NewGuid() : alici.Id };
            _db.BildirimAlicilari.Add(mevcut);
        }
        mevcut.AdSoyad = IsimKurallari.Duzelt(alici.AdSoyad)!;
        mevcut.Eposta = alici.Eposta?.Trim();
        mevcut.Telefon = telefon;
        mevcut.Rol = alici.Rol;
        mevcut.Tesis = string.IsNullOrWhiteSpace(alici.Tesis) ? null : alici.Tesis;
        mevcut.Aktif = alici.Aktif;
        await _db.SaveChangesAsync(ct);
        return mevcut;
    }

    public async Task<IReadOnlyList<BildirimKurali>> KurallarAsync(CancellationToken ct = default) =>
        await _db.BildirimKurallari.AsNoTracking().OrderBy(k => k.Ad).ToListAsync(ct);

    public async Task<BildirimKurali> KuralKaydetAsync(BildirimKurali kural, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "Bildirim kuralı", ct);
        if (string.IsNullOrWhiteSpace(kural.Ad)) throw new InvalidOperationException("Kural adı zorunludur.");
        if (kural.Saat is < 0 or > 23) throw new InvalidOperationException("Saat 0-23 arasında olmalıdır.");
        if (kural.Siklik == BildirimSikligi.Aylik && kural.Gun is < 1 or > 28) throw new InvalidOperationException("Aylık kuralda gün 1-28 arasında olmalıdır.");
        if (kural.Siklik == BildirimSikligi.Haftalik && kural.Gun is < 1 or > 7) throw new InvalidOperationException("Haftalık kuralda gün 1 (Pazartesi) – 7 (Pazar) olmalıdır.");
        if (kural.Tetikleyici == BildirimTetikleyici.ZamanlanmisRapor && kural.RaporTuru is null)
            throw new InvalidOperationException("Zamanlanmış rapor kuralında rapor türü seçilmelidir.");
        if (string.IsNullOrWhiteSpace(kural.AliciIdleri) && string.IsNullOrWhiteSpace(kural.AliciRolu))
            throw new InvalidOperationException("Alıcı veya alıcı rolü seçilmelidir.");

        var k = await _db.BildirimKurallari.FirstOrDefaultAsync(x => x.Id == kural.Id, ct);
        if (k is null)
        {
            k = new BildirimKurali { Id = kural.Id == Guid.Empty ? Guid.NewGuid() : kural.Id, OlusturanKullanici = kullanici };
            _db.BildirimKurallari.Add(k);
        }
        k.Ad = kural.Ad.Trim(); k.Aktif = kural.Aktif; k.Tetikleyici = kural.Tetikleyici; k.Siklik = kural.Siklik; k.Gun = kural.Gun; k.Saat = kural.Saat;
        k.Kanal = kural.Kanal; k.AliciIdleri = kural.AliciIdleri; k.AliciRolu = kural.AliciRolu; k.Tesis = kural.Tesis;
        k.RaporTuru = kural.RaporTuru; k.RaporBicimi = kural.RaporBicimi;
        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(BildirimKurali), VarlikId = k.Id, Islem = "BildirimKuraliKaydedildi",
            YeniDeger = $"{k.Ad}: {k.Tetikleyici}, {k.Siklik} gün {k.Gun} saat {k.Saat}, {k.Kanal}", KullaniciAdi = kullanici
        });
        await _db.SaveChangesAsync(ct);
        return k;
    }

    // ------------------------------------------------------------------ Gönderim

    public async Task<IReadOnlyList<BildirimKaydi>> GonderAsync(BildirimTalebi talep, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Muhasebe, "E-posta / WhatsApp gönderimi", ct);
        if (string.IsNullOrWhiteSpace(talep.Konu) && string.IsNullOrWhiteSpace(talep.Metin))
            throw new InvalidOperationException("Konu veya metin girilmelidir.");
        var alicilar = await _db.BildirimAlicilari.AsNoTracking().Where(a => talep.AliciIdleri.Contains(a.Id)).ToListAsync(ct);
        var hedefler = Hedefler(alicilar, talep.Kanal).Concat(talep.DogrudanAdresler.Select(a =>
            a.Contains('@') ? (BildirimKanali.Eposta, a.Trim()) : (BildirimKanali.WhatsApp, TelefonNormalize(a)!))).Distinct().ToList();
        if (hedefler.Count == 0) throw new InvalidOperationException("Seçilen kanal için e-posta adresi veya telefonu olan alıcı yok.");
        return await TeslimEtAsync(hedefler, talep.Konu, talep.Metin, talep.Ek, null, null, kullanici, ct);
    }

    public async Task<IReadOnlyList<BildirimKaydi>> RaporGonderAsync(RaporTuru tur, RaporParametreleri p, RaporBicimi bicim, BildirimKanali kanal,
        IReadOnlyList<Guid> aliciIdleri, string? notMetni, string kullanici, CancellationToken ct = default)
    {
        var rapor = await _rapor.OlusturAsync(tur, p, ct);
        return await GonderAsync(new BildirimTalebi
        {
            Kanal = kanal,
            AliciIdleri = aliciIdleri,
            Konu = $"Personel Yönetim Sistemi raporu: {rapor.Baslik}",
            Metin = RaporMetni(rapor, notMetni),
            Ek = new BildirimEki(_rapor.DosyaAdi(rapor, bicim), _rapor.DisaAktar(rapor, bicim), IcerikTuru(bicim))
        }, kullanici, ct);
    }

    public async Task<string> WhatsAppBaglantisiAsync(string telefon, string metin, string kullanici, CancellationToken ct = default)
    {
        var tel = TelefonNormalize(telefon) ?? throw new InvalidOperationException("Telefon numarası geçersiz.");
        var url = $"https://wa.me/{tel}?text={Uri.EscapeDataString(metin)}";
        _db.BildirimKayitlari.Add(new BildirimKaydi
        {
            Kanal = BildirimKanali.WhatsApp, Alici = tel, Konu = "Elle WhatsApp", Ozet = Kisalt(metin, 4000),
            Durum = BildirimDurumu.ElleAcildi, Kullanici = kullanici, GonderimZamani = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        return url;
    }

    public async Task KisiyeBildirAsync(Guid aliciId, string olayAnahtari, string konu, string metin, CancellationToken ct = default)
    {
        var a = await _db.BildirimAlicilari.AsNoTracking().FirstOrDefaultAsync(x => x.Id == aliciId && x.Aktif, ct);
        if (a is null) return;
        var ayar = await _db.BildirimAyarlari.AsNoTracking().FirstOrDefaultAsync(ct);
        var whatsAppHazir = ayar is { WhatsAppEtkin: true } && !string.IsNullOrEmpty(ayar.WhatsAppErisimAnahtariSifreli);
        var kanal = whatsAppHazir ? BildirimKanali.EpostaVeWhatsApp : BildirimKanali.Eposta;
        var hedefler = Hedefler([a], kanal).ToList();
        if (hedefler.Count == 0 && !string.IsNullOrWhiteSpace(a.Telefon))
            hedefler = Hedefler([a], BildirimKanali.WhatsApp).ToList(); // e-postası yoksa (ayar eksikse hata olarak kaydedilir)
        if (hedefler.Count > 0)
            await TeslimEtAsync(hedefler, konu, metin, null, null, $"Kisi:{olayAnahtari}", "sistem", ct);
    }

    private async Task<List<BildirimKaydi>> TeslimEtAsync(IReadOnlyList<(BildirimKanali Kanal, string Adres)> hedefler, string konu, string metin,
        BildirimEki? ek, Guid? kuralId, string? olayAnahtari, string kullanici, CancellationToken ct)
    {
        var ayar = await _db.BildirimAyarlari.AsNoTracking().FirstOrDefaultAsync(ct) ?? new BildirimAyari();
        var kayitlar = new List<BildirimKaydi>();
        foreach (var (kanal, adres) in hedefler)
        {
            var anahtar = olayAnahtari is null ? null : $"{olayAnahtari}:{kanal}:{adres}";
            if (anahtar is not null && await _db.BildirimKayitlari.AnyAsync(b => b.OlayAnahtari == anahtar, ct)) continue;
            var k = new BildirimKaydi
            {
                KuralId = kuralId, Kanal = kanal, Alici = adres, Konu = konu, Ozet = Kisalt(metin, 4000),
                EkDosyaAdi = ek?.DosyaAdi, OlayAnahtari = anahtar, Kullanici = kullanici
            };
            _db.BildirimKayitlari.Add(k);
            await TekGonderimAsync(k, ayar, metin, ek, ct);
            kayitlar.Add(k);
        }
        await _db.SaveChangesAsync(ct);
        return kayitlar;
    }

    private async Task TekGonderimAsync(BildirimKaydi k, BildirimAyari ayar, string metin, BildirimEki? ek, CancellationToken ct)
    {
        k.DenemeSayisi++;
        try
        {
            if (k.Kanal == BildirimKanali.Eposta)
            {
                if (string.IsNullOrWhiteSpace(ayar.SmtpSunucu) || string.IsNullOrWhiteSpace(ayar.GonderenEposta))
                    throw new InvalidOperationException("SMTP sunucusu ve gönderen adresi ayarlanmamış (Bildirimler → Ayarlar).");
                var sifre = string.IsNullOrEmpty(ayar.SmtpSifreSifreli) ? "" : _koruyucu.Coz(ayar.SmtpSifreSifreli);
                await _eposta.GonderAsync(ayar, sifre, k.Alici, k.Konu, metin, ek, ct);
            }
            else
            {
                if (!ayar.WhatsAppEtkin || string.IsNullOrWhiteSpace(ayar.WhatsAppTelefonNumarasiId) || string.IsNullOrEmpty(ayar.WhatsAppErisimAnahtariSifreli))
                    throw new InvalidOperationException("Otomatik WhatsApp için Cloud API ayarları eksik. Elle gönderim için 'WhatsApp'ta aç' kullanın.");
                await _whatsapp.GonderAsync(ayar, _koruyucu.Coz(ayar.WhatsAppErisimAnahtariSifreli), k.Alici, $"*{k.Konu}*\n{metin}", ek, ct);
            }
            k.Durum = BildirimDurumu.Gonderildi;
            k.Hata = null;
            k.GonderimZamani = DateTime.UtcNow;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            k.Durum = BildirimDurumu.Hata;
            k.Hata = Kisalt(ex.Message, 1000);
        }
    }

    public async Task<int> BasarisizlariYenidenDeneAsync(CancellationToken ct = default)
    {
        var ayar = await _db.BildirimAyarlari.AsNoTracking().FirstOrDefaultAsync(ct) ?? new BildirimAyari();
        var bekleyen = await _db.BildirimKayitlari.Where(b => b.Durum == BildirimDurumu.Hata && b.DenemeSayisi < AzamiDeneme).ToListAsync(ct);
        var basarili = 0;
        foreach (var k in bekleyen)
        {
            BildirimEki? ek = null;
            if (k.EkDosyaAdi is not null && k.KuralId is { } kid)
            {
                var kural = await _db.BildirimKurallari.AsNoTracking().FirstOrDefaultAsync(x => x.Id == kid, ct);
                if (kural?.RaporTuru is { } tur)
                {
                    var r = await _rapor.OlusturAsync(tur, RaporDonemi(kural, DateTime.Now), ct);
                    ek = new BildirimEki(k.EkDosyaAdi, _rapor.DisaAktar(r, kural.RaporBicimi), IcerikTuru(kural.RaporBicimi));
                }
            }
            await TekGonderimAsync(k, ayar, k.Ozet, ek, ct);
            if (k.Durum == BildirimDurumu.Gonderildi) basarili++;
        }
        await _db.SaveChangesAsync(ct);
        return basarili;
    }

    public async Task<IReadOnlyList<BildirimKaydi>> GecmisAsync(int adet = 500, CancellationToken ct = default) =>
        await _db.BildirimKayitlari.AsNoTracking().OrderByDescending(b => b.OlusturmaZamani).Take(adet).ToListAsync(ct);

    // ------------------------------------------------------------------ Kurallar

    /// <summary>Kuralın şimdiye kadarki en son vade zamanı.</summary>
    public static DateTime SonVade(BildirimKurali k, DateTime simdi)
    {
        var bugunVade = simdi.Date.AddHours(k.Saat);
        switch (k.Siklik)
        {
            case BildirimSikligi.Gunluk:
                return simdi >= bugunVade ? bugunVade : bugunVade.AddDays(-1);
            case BildirimSikligi.Haftalik:
            {
                var hedef = (DayOfWeek)(k.Gun % 7); // 7 = Pazar
                var fark = ((int)simdi.DayOfWeek - (int)hedef + 7) % 7;
                var vade = simdi.Date.AddDays(-fark).AddHours(k.Saat);
                return vade <= simdi ? vade : vade.AddDays(-7);
            }
            default:
            {
                var vade = new DateTime(simdi.Year, simdi.Month, k.Gun, k.Saat, 0, 0, simdi.Kind);
                return vade <= simdi ? vade : vade.AddMonths(-1);
            }
        }
    }

    private static RaporParametreleri RaporDonemi(BildirimKurali k, DateTime simdi)
    {
        var ay = new DateOnly(simdi.Year, simdi.Month, 1);
        var hedef = k.Siklik == BildirimSikligi.Aylik ? ay.AddMonths(-1) : ay;
        return k.RaporTuru is RaporTuru.PersonelMaliyetTrendi or RaporTuru.GirisCikisDevirHizi
            ? new RaporParametreleri { BaslangicYil = hedef.AddMonths(-11).Year, BaslangicAy = hedef.AddMonths(-11).Month, BitisYil = hedef.Year, BitisAy = hedef.Month, Tesis = k.Tesis }
            : RaporParametreleri.TekDonem(hedef.Year, hedef.Month, k.Tesis);
    }

    public async Task<int> ZamanlanmisKurallariCalistirAsync(DateTime simdi, CancellationToken ct = default)
    {
        var kurallar = await _db.BildirimKurallari
            .Where(k => k.Aktif && (k.Tetikleyici == BildirimTetikleyici.ZamanlanmisRapor || k.Tetikleyici == BildirimTetikleyici.UyariOzeti))
            .ToListAsync(ct);
        var calisan = 0;
        foreach (var k in kurallar)
        {
            var vade = SonVade(k, simdi);
            var referans = k.SonCalisma ?? k.OlusturmaZamani.ToLocalTime();
            if (vade <= referans) continue;

            string konu, metin;
            BildirimEki? ek = null;
            if (k.Tetikleyici == BildirimTetikleyici.ZamanlanmisRapor)
            {
                var r = await _rapor.OlusturAsync(k.RaporTuru!.Value, RaporDonemi(k, simdi), ct);
                konu = $"Personel Yönetim Sistemi {k.Ad}: {r.Baslik}";
                metin = RaporMetni(r, null);
                ek = new BildirimEki(_rapor.DosyaAdi(r, k.RaporBicimi), _rapor.DisaAktar(r, k.RaporBicimi), IcerikTuru(k.RaporBicimi));
            }
            else
            {
                var uyarilar = await _uyari.ListeleAsync(k.Tesis, ct);
                var onemli = uyarilar.Where(u => u.Seviye != UyariSeviyesi.Bilgi).ToList();
                if (onemli.Count == 0) { k.SonCalisma = simdi; continue; }
                konu = $"Personel Yönetim Sistemi uyarı özeti: {onemli.Count(u => u.Seviye == UyariSeviyesi.Kritik)} kritik, {onemli.Count(u => u.Seviye == UyariSeviyesi.Uyari)} uyarı";
                var sb = new StringBuilder();
                sb.AppendLine($"{(k.Tesis is null ? "Tüm tesisler" : k.Tesis)} için açık işler ({simdi:dd.MM.yyyy HH:mm}):");
                foreach (var u in onemli.Take(25))
                    sb.AppendLine($"• [{(u.Seviye == UyariSeviyesi.Kritik ? "KRİTİK" : "Uyarı")}] {u.Kategori}: {u.Baslik}{(u.Tesis is null ? "" : $" ({u.Tesis})")}");
                if (onemli.Count > 25) sb.AppendLine($"… ve {onemli.Count - 25} uyarı daha. Ayrıntılar Personel Yönetim Sistemi → Uyarı merkezi.");
                metin = sb.ToString();
            }

            var hedefler = await KuralHedefleriAsync(k, ct);
            await TeslimEtAsync(hedefler, konu, metin, ek, k.Id, $"Kural:{k.Id:N}:{vade:yyyyMMddHH}", "sistem", ct);
            k.SonCalisma = simdi;
            calisan++;
        }
        await _db.SaveChangesAsync(ct);
        return calisan;
    }

    public async Task OlayBildirAsync(BildirimTetikleyici olay, string olayAnahtari, string konu, string metin, string? tesis, CancellationToken ct = default)
    {
        var kurallar = await _db.BildirimKurallari.AsNoTracking().Where(k => k.Aktif && k.Tetikleyici == olay).ToListAsync(ct);
        foreach (var k in kurallar.Where(k => k.Tesis is null || tesis is null || string.Equals(k.Tesis, tesis, StringComparison.CurrentCultureIgnoreCase)))
        {
            var hedefler = await KuralHedefleriAsync(k, ct);
            await TeslimEtAsync(hedefler, konu, metin, null, k.Id, $"{olay}:{olayAnahtari}:{k.Id:N}", "sistem", ct);
        }
    }

    private async Task<List<(BildirimKanali, string)>> KuralHedefleriAsync(BildirimKurali k, CancellationToken ct)
    {
        var tum = await _db.BildirimAlicilari.AsNoTracking().Where(a => a.Aktif).ToListAsync(ct);
        var idler = (k.AliciIdleri ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).ToHashSet();
        var secilen = tum.Where(a => idler.Contains(a.Id) ||
                                     (!string.IsNullOrWhiteSpace(k.AliciRolu) && string.Equals(a.Rol, k.AliciRolu, StringComparison.CurrentCultureIgnoreCase) &&
                                      (k.Tesis is null || a.Tesis is null || string.Equals(a.Tesis, k.Tesis, StringComparison.CurrentCultureIgnoreCase))))
            .ToList();
        return Hedefler(secilen, k.Kanal).ToList();
    }

    private static IEnumerable<(BildirimKanali, string)> Hedefler(IEnumerable<BildirimAlicisi> alicilar, BildirimKanali kanal)
    {
        foreach (var a in alicilar)
        {
            if (kanal is BildirimKanali.Eposta or BildirimKanali.EpostaVeWhatsApp && !string.IsNullOrWhiteSpace(a.Eposta))
                yield return (BildirimKanali.Eposta, a.Eposta!);
            if (kanal is BildirimKanali.WhatsApp or BildirimKanali.EpostaVeWhatsApp && !string.IsNullOrWhiteSpace(a.Telefon))
                yield return (BildirimKanali.WhatsApp, a.Telefon!);
        }
    }

    private static string RaporMetni(RaporSonucu r, string? not)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(not)) sb.AppendLine(not).AppendLine();
        sb.AppendLine(r.Baslik);
        sb.AppendLine(r.ParametreMetni);
        foreach (var (ad, deger) in r.Ozet) sb.AppendLine($"• {ad}: {deger}");
        sb.AppendLine($"Satır sayısı: {r.Satirlar.Count}. Ayrıntılar ekteki dosyadadır.");
        sb.AppendLine().AppendLine("Bu mesaj Personel Yönetim Sistemi tarafından gönderilmiştir.");
        return sb.ToString();
    }

    private static string IcerikTuru(RaporBicimi b) => b switch
    {
        RaporBicimi.Pdf => "application/pdf",
        RaporBicimi.Csv => "text/csv",
        _ => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    /// <summary>Türkiye numaralarını uluslararası biçime çevirir: 0532… / +90 532… → 90532….</summary>
    public static string? TelefonNormalize(string? tel)
    {
        if (string.IsNullOrWhiteSpace(tel)) return null;
        var r = new string(tel.Where(char.IsDigit).ToArray());
        if (r.StartsWith("00")) r = r[2..];
        if (r.Length == 11 && r.StartsWith('0')) r = "90" + r[1..];
        if (r.Length == 10 && r.StartsWith('5')) r = "90" + r;
        if (r.Length is < 10 or > 15) throw new InvalidOperationException($"Telefon numarası geçersiz: {tel}");
        return r;
    }

    private static string Kisalt(string s, int n) => s.Length <= n ? s : s[..n];
}
