using System.Globalization;
using System.Security.Cryptography;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Application.Services;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;
using PayControl.Infrastructure.Data;

namespace PayControl.Infrastructure.Import;

public sealed class BordroIceAktarmaServisi : IBordroIceAktarmaServisi
{
    private readonly PayControlDbContext _db;
    private readonly IAvansServisi _avans;
    private readonly IMaliHareketServisi _defter;

    public BordroIceAktarmaServisi(PayControlDbContext db, IAvansServisi avans, IMaliHareketServisi defter)
    {
        _db = db;
        _avans = avans;
        _defter = defter;
    }

    public async Task<IceAktarmaOnizlemeSonucu> OnizleAsync(
        Stream dosyaAkisi,
        string dosyaAdi,
        int donemYil,
        int donemAy,
        CancellationToken ct = default)
    {
        var bytes = await ReadAllAsync(dosyaAkisi, ct);
        var hash = Sha256(bytes);
        var onceki = await _db.IceAktarmalar.AsNoTracking()
            .FirstOrDefaultAsync(x => x.DosyaHashSha256 == hash && !x.GeriAlindi, ct);

        using var ms = new MemoryStream(bytes);
        var (basliklar, satirlar) = OkuWorkbook(ms);
        var esleme = BordroBaslikEsleyici.Esle(basliklar);
        var onizlemeSatirlari = ParseSatirlar(satirlar, esleme.KolonAlanlari);

        return new IceAktarmaOnizlemeSonucu
        {
            DosyaAdi = dosyaAdi,
            DosyaHashSha256 = hash,
            MukerrerDosya = onceki is not null,
            OncekiIceAktarmaId = onceki?.Id,
            EslesenBasliklar = esleme.EslesenBasliklar,
            EslesmeyenBasliklar = esleme.EslesmeyenBasliklar,
            BelirsizBasliklar = esleme.BelirsizBasliklar,
            Satirlar = onizlemeSatirlari
        };
    }

    public async Task<IceAktarmaSonucu> AktarAsync(
        Stream dosyaAkisi,
        string dosyaAdi,
        int donemYil,
        int donemAy,
        string kullaniciAdi,
        bool mukerrereIzinVer = false,
        CancellationToken ct = default)
    {
        var onizleme = await OnizleAsync(dosyaAkisi, dosyaAdi, donemYil, donemAy, ct);
        if (onizleme.MukerrerDosya && !mukerrereIzinVer)
        {
            return new IceAktarmaSonucu
            {
                MukerrerEngellendi = true,
                IceAktarmaId = onizleme.OncekiIceAktarmaId ?? Guid.Empty,
                Mesaj = "Aynı dosya daha önce içe aktarılmış. Mükerrer kayıt üretilmedi."
            };
        }

        var kapanis = await _db.MaasDonemKapanislari
            .FirstOrDefaultAsync(d => d.DonemYil == donemYil && d.DonemAy == donemAy, ct);
        DonemKapanisKurallari.DogrudanDegisikligiEngelle(kapanis);
        var uyarilar = new List<string>();

        // Stream OnizleAsync ile tüketildi; yeniden parse için onizleme satırlarını kullanıyoruz.
        var iceAktarma = new IceAktarmaKaydi
        {
            DosyaAdi = dosyaAdi,
            DosyaHashSha256 = onizleme.DosyaHashSha256,
            DonemYil = donemYil,
            DonemAy = donemAy,
            SatirSayisi = onizleme.Satirlar.Count,
            KullaniciAdi = kullaniciAdi
        };
        _db.IceAktarmalar.Add(iceAktarma);

        var basarili = 0;
        var hatali = 0;
        var yeniPersonel = 0;
        var guncellenen = 0;

        foreach (var satir in onizleme.Satirlar)
        {
            if (!satir.Gecerli)
            {
                hatali++;
                continue;
            }

            var tc = PersonelServisi.NormalizeTc(satir.TcKimlikNo!);
            var personel = await _db.Personeller
                .Include(p => p.CalismaDonemleri)
                .FirstOrDefaultAsync(p => p.TcKimlikNo == tc, ct);

            if (personel is null)
            {
                personel = new Personel
                {
                    TcKimlikNo = tc,
                    AdSoyad = satir.AdSoyad!.Trim(),
                    Isyeri = satir.Isyeri,
                    Gorevi = satir.Gorevi,
                    GuncelMaas = satir.Maas,
                    GuncelMaasTuru = ParseMaasTuru(satir.MaasTuruHam),
                    Iban = PersonelServisi.NormalizeIban(satir.Iban),
                    IlkIseGirisTarihi = satir.GirisTarihi,
                    CalismaDurumu = satir.CikisTarihi.HasValue ? CalismaDurumu.IstenAyrildi : CalismaDurumu.Aktif,
                    SonCikisTarihi = satir.CikisTarihi
                };

                if (satir.GirisTarihi.HasValue)
                {
                    PersonelDonemKurallari.YeniDonemAc(personel, satir.GirisTarihi.Value, satir.Isyeri, satir.Gorevi);
                    if (satir.CikisTarihi.HasValue)
                    {
                        var donem = personel.CalismaDonemleri.First();
                        PersonelDonemKurallari.DonemiKapat(personel, donem, satir.CikisTarihi.Value);
                    }
                }

                _db.Personeller.Add(personel);
                yeniPersonel++;
                foreach (var d in personel.CalismaDonemleri)
                    await GirisCikisHareketleriAsync(personel, d, kullaniciAdi, dosyaAdi, ct);
            }
            else
            {
                // Kart güncel alanları güncellenir; eski maaş dönemlerine dokunulmaz.
                personel.AdSoyad = satir.AdSoyad!.Trim();
                personel.Isyeri = satir.Isyeri ?? personel.Isyeri;
                personel.Gorevi = satir.Gorevi ?? personel.Gorevi;
                personel.GuncelMaas = satir.Maas ?? personel.GuncelMaas;
                personel.GuncelMaasTuru = ParseMaasTuru(satir.MaasTuruHam);
                personel.Iban = PersonelServisi.NormalizeIban(satir.Iban) ?? personel.Iban;
                personel.GuncellemeZamani = DateTime.UtcNow;
                guncellenen++;

                // İşten ayrılmış personel yeni bir giriş tarihiyle geldiyse: yeni çalışma dönemi (geçmiş korunur).
                if (personel.CalismaDurumu == CalismaDurumu.IstenAyrildi && satir.GirisTarihi.HasValue &&
                    personel.SonCikisTarihi.HasValue && satir.GirisTarihi.Value > personel.SonCikisTarihi.Value)
                {
                    var yeni = PersonelDonemKurallari.YeniDonemAc(personel, satir.GirisTarihi.Value, satir.Isyeri, satir.Gorevi);
                    _db.CalismaDonemleri.Add(yeni);
                    await GirisCikisHareketleriAsync(personel, yeni, kullaniciAdi, dosyaAdi, ct);
                    uyarilar.Add($"{personel.AdSoyad}: yeniden işe giriş ({satir.GirisTarihi:dd.MM.yyyy}) — yeni çalışma dönemi açıldı.");
                }
            }

            // Aynı dönem + aynı içe aktarma için mükerrer satır kontrolü
            var mevcutDonem = await _db.MaasDonemKayitlari
                .AnyAsync(m => m.PersonelId == personel.Id &&
                               m.DonemYil == donemYil &&
                               m.DonemAy == donemAy &&
                               m.IceAktarmaId == iceAktarma.Id, ct);
            if (mevcutDonem)
            {
                hatali++;
                continue;
            }

            var calismaDonemi = MaliHareketKurallari.CalismaDonemiBul(personel.CalismaDonemleri, new DateOnly(donemYil, donemAy, 1));
            var donemKaydi = new MaasDonemKaydi
            {
                PersonelId = personel.Id,
                CalismaDonemiId = calismaDonemi?.Id,
                Sirket = calismaDonemi?.Sirket ?? personel.Sirket,
                DonemYil = donemYil,
                DonemAy = donemAy,
                Isyeri = satir.Isyeri,
                Gorevi = satir.Gorevi,
                MaasTutar = satir.Maas,
                MaasTuru = ParseMaasTuru(satir.MaasTuruHam),
                GirisTarihi = satir.GirisTarihi,
                CikisTarihi = satir.CikisTarihi,
                Gun = satir.Gun,
                NetIstihkak = satir.NetIstihkak ?? 0m,
                Iban = PersonelServisi.NormalizeIban(satir.Iban),
                IceAktarma = iceAktarma,
                KaynakSatirNo = satir.SatirNo
            };

            EkleKalem(donemKaydi, MaasKalemTuru.AvansKesinti, "AVANS", satir.Avans, kesinti: true, dahil: false);
            EkleKalem(donemKaydi, MaasKalemTuru.Icra, "İCRA", satir.Icra, kesinti: true, dahil: false);
            EkleKalem(donemKaydi, MaasKalemTuru.OtoKatBes, "OTO.KAT.BES", satir.OtoKatBes, kesinti: true, dahil: false);
            EkleKalem(donemKaydi, MaasKalemTuru.TrafikCezasi, "TRAFİK CEZASI", satir.TrafikCezasi, kesinti: true, dahil: false);
            // Aile/Yemek/Yol/İhbar: Net olarak işaretli — Net İstihkak'a dahil kabul (çift sayım yok)
            EkleKalem(donemKaydi, MaasKalemTuru.Aile, "AİLE", satir.Aile, kesinti: false, dahil: true);
            EkleKalem(donemKaydi, MaasKalemTuru.IhbarTazminati, "İHBAR TAZMİNATI", satir.IhbarTazminati, kesinti: false, dahil: true);
            EkleKalem(donemKaydi, MaasKalemTuru.Yemek, "YEMEK", satir.Yemek, kesinti: false, dahil: true);
            EkleKalem(donemKaydi, MaasKalemTuru.Yol, "YOL", satir.Yol, kesinti: false, dahil: true);
            if (satir.Maas.HasValue)
                EkleKalem(donemKaydi, MaasKalemTuru.Maas, "MAAŞ", satir.Maas, kesinti: false, dahil: true);

            _db.MaasDonemKayitlari.Add(donemKaydi);

            // Mali hareket defteri — hak ediş (çift kayıt işlem kimliği ile engellenir)
            var sonGun = new DateOnly(donemYil, donemAy, DateTime.DaysInMonth(donemYil, donemAy));
            var referans = iceAktarma.Id.ToString();
            await _defter.DeftereEkleAsync(MaliHareketKurallari.Olustur(
                personel.Id,
                MaliHareketKurallari.IslemKimligi("BordroHakEdis", personel.Id, $"{donemYil}-{donemAy:D2}:{iceAktarma.Id:N}"),
                sonGun, donemYil, donemAy, MaliIslemTuru.MaasHakEdis, BakiyeTuru.MaasAlacagi,
                donemKaydi.NetIstihkak,
                $"Bordro net istihkak {donemYil}-{donemAy:D2}" + (satir.Gun.HasValue ? $" ({satir.Gun.Value.ToString("0.##", CultureInfo.InvariantCulture)} gün)" : ""),
                nameof(MaasDonemKaydi), referans, dosyaAdi, kullaniciAdi, calismaDonemi?.Id), false, ct);

            foreach (var kalem in donemKaydi.Kalemler.Where(k => k.Tutar != 0 && k.Tur != MaasKalemTuru.Maas))
            {
                if (kalem.Tur == MaasKalemTuru.AvansKesinti)
                {
                    var mahsup = await _avans.BordroMahsupuUygulaAsync(
                        personel.Id, donemYil, donemAy, kalem.Tutar, iceAktarma.Id, dosyaAdi, kullaniciAdi, ct);
                    uyarilar.AddRange(mahsup.Uyarilar.Select(u => $"{personel.AdSoyad}: {u}"));
                    continue;
                }

                var (tur, bakiye) = kalem.Tur switch
                {
                    MaasKalemTuru.Icra => (MaliIslemTuru.IcraKesinti, BakiyeTuru.Bilgi),
                    MaasKalemTuru.OtoKatBes => (MaliIslemTuru.BesKesinti, BakiyeTuru.Bilgi),
                    MaasKalemTuru.IhbarTazminati => (MaliIslemTuru.IhbarTazminati, BakiyeTuru.Bilgi),
                    MaasKalemTuru.KidemTazminati => (MaliIslemTuru.KidemTazminati, BakiyeTuru.Bilgi),
                    _ when kalem.KesintiMi => (MaliIslemTuru.DigerKesinti, BakiyeTuru.Bilgi),
                    _ => (MaliIslemTuru.BordroKalemBilgisi, BakiyeTuru.Bilgi)
                };
                await _defter.DeftereEkleAsync(MaliHareketKurallari.Olustur(
                    personel.Id,
                    MaliHareketKurallari.IslemKimligi($"BordroKalem:{kalem.Tur}", personel.Id, $"{donemYil}-{donemAy:D2}:{iceAktarma.Id:N}"),
                    sonGun, donemYil, donemAy, tur, bakiye, kalem.Tutar,
                    $"Bordro kalemi: {kalem.Ad}" + (kalem.NetIstihkakaDahil ? " (net istihkaka dahil)" : " (kesinti)"),
                    nameof(MaasKalemi), referans, dosyaAdi, kullaniciAdi, calismaDonemi?.Id), false, ct);
            }

            basarili++;
        }

        iceAktarma.BasariliSatir = basarili;
        iceAktarma.HataliSatir = hatali;

        if (kapanis is null)
        {
            kapanis = DonemKapanisKurallari.YeniDonem(donemYil, donemAy);
            kapanis.Durum = MaasDonemDurumu.BordroYuklendi;
            _db.MaasDonemKapanislari.Add(kapanis);
        }
        else if (kapanis.Durum == MaasDonemDurumu.Hazirlaniyor)
        {
            kapanis.Durum = MaasDonemDurumu.BordroYuklendi;
        }

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(IceAktarmaKaydi),
            VarlikId = iceAktarma.Id,
            Islem = "IceAktarma",
            KullaniciAdi = kullaniciAdi,
            YeniDeger = $"{dosyaAdi} | {donemYil}-{donemAy:D2} | {basarili} satır"
        });

        await _db.SaveChangesAsync(ct);

        return new IceAktarmaSonucu
        {
            IceAktarmaId = iceAktarma.Id,
            BasariliSatir = basarili,
            HataliSatir = hatali,
            YeniPersonel = yeniPersonel,
            GuncellenenPersonel = guncellenen,
            Mesaj = $"İçe aktarma tamamlandı. Başarılı: {basarili}, Hatalı: {hatali}." +
                    (uyarilar.Count > 0 ? Environment.NewLine + string.Join(Environment.NewLine, uyarilar) : ""),
            Uyarilar = uyarilar
        };
    }

    public async Task<bool> GeriAlAsync(Guid iceAktarmaId, string kullaniciAdi, CancellationToken ct = default)
    {
        var kayit = await _db.IceAktarmalar
            .Include(i => i.MaasDonemKayitlari)
            .FirstOrDefaultAsync(i => i.Id == iceAktarmaId, ct);

        if (kayit is null || kayit.GeriAlindi)
            return false;

        if (kayit.DonemYil.HasValue && kayit.DonemAy.HasValue)
        {
            var kapanis = await _db.MaasDonemKapanislari.AsNoTracking()
                .FirstOrDefaultAsync(d => d.DonemYil == kayit.DonemYil && d.DonemAy == kayit.DonemAy && d.Durum == MaasDonemDurumu.Kapatildi, ct);
            DonemKapanisKurallari.DogrudanDegisikligiEngelle(kapanis);
        }

        // Kayıtlar silinmez: iptal işaretlenir, mali hareketlerin ters kaydı alınır.
        var simdi = DateTime.UtcNow;
        foreach (var m in kayit.MaasDonemKayitlari)
        {
            m.IptalEdildi = true;
            m.IptalZamani = simdi;
            m.IptalEden = kullaniciAdi;
            m.IptalNedeni = "Bordro içe aktarma geri alındı";
        }

        var referans = kayit.Id.ToString();
        var ek = $":{kayit.Id:N}";
        var hareketler = await _db.MaliHareketler.AsNoTracking()
            .Where(h => !h.TersKayitMi &&
                        (h.KaynakTuru == nameof(MaasDonemKaydi) || h.KaynakTuru == nameof(MaasKalemi) || h.KaynakTuru == nameof(IceAktarmaKaydi)) &&
                        (h.KaynakReferans == referans || h.IslemKimligi.EndsWith(ek)))
            .ToListAsync(ct);
        var bugun = DateOnly.FromDateTime(DateTime.Today);
        foreach (var h in hareketler)
            await _defter.DeftereEkleAsync(MaliHareketKurallari.TersKayit(h, "Bordro içe aktarma geri alındı", kullaniciAdi, bugun), false, ct);

        await _avans.BordroMahsupuGeriAlAsync(kayit.Id, kullaniciAdi, ct);

        kayit.GeriAlindi = true;
        kayit.GeriAlmaZamani = simdi;
        kayit.GeriAlanKullanici = kullaniciAdi;

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = nameof(IceAktarmaKaydi),
            VarlikId = kayit.Id,
            Islem = "GeriAlma",
            KullaniciAdi = kullaniciAdi,
            DegisiklikNedeni = $"İçe aktarma geri alındı; {kayit.MaasDonemKayitlari.Count} bordro satırı iptal edildi, {hareketler.Count} mali hareketin ters kaydı alındı (kayıtlar silinmedi)."
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task GirisCikisHareketleriAsync(Personel personel, CalismaDonemi donem, string kullanici, string kaynak, CancellationToken ct)
    {
        await _defter.DeftereEkleAsync(new MaliHareket
        {
            IslemKimligi = MaliHareketKurallari.IslemKimligi("IseGiris", personel.Id, donem.Id.ToString("N")),
            PersonelId = personel.Id,
            CalismaDonemiId = donem.Id,
            IslemTarihi = donem.IseGirisTarihi,
            IslemTuru = MaliIslemTuru.IseGiris,
            BakiyeTuru = BakiyeTuru.Bilgi,
            Aciklama = $"İşe giriş ({donem.Isyeri ?? personel.Isyeri})",
            KaynakTuru = nameof(CalismaDonemi),
            KaynakReferans = donem.Id.ToString(),
            KaynakBelge = kaynak,
            KullaniciAdi = kullanici
        }, false, ct);

        if (donem.IstenCikisTarihi.HasValue)
        {
            await _defter.DeftereEkleAsync(new MaliHareket
            {
                IslemKimligi = MaliHareketKurallari.IslemKimligi("IstenCikis", personel.Id, donem.Id.ToString("N")),
                PersonelId = personel.Id,
                CalismaDonemiId = donem.Id,
                IslemTarihi = donem.IstenCikisTarihi.Value,
                IslemTuru = MaliIslemTuru.IstenCikis,
                BakiyeTuru = BakiyeTuru.Bilgi,
                Aciklama = "İşten çıkış (bordrodaki çıkış tarihi)",
                KaynakTuru = nameof(CalismaDonemi),
                KaynakReferans = donem.Id.ToString(),
                KaynakBelge = kaynak,
                KullaniciAdi = kullanici
            }, false, ct);
        }
    }

    private static void EkleKalem(
        MaasDonemKaydi donem,
        MaasKalemTuru tur,
        string ad,
        decimal? tutar,
        bool kesinti,
        bool dahil)
    {
        if (!tutar.HasValue || tutar.Value == 0m)
            return;

        donem.Kalemler.Add(new MaasKalemi
        {
            Tur = tur,
            Ad = ad,
            Tutar = tutar.Value,
            KesintiMi = kesinti,
            NetIstihkakaDahil = dahil
        });
    }

    private static MaasTuru ParseMaasTuru(string? ham)
    {
        if (string.IsNullOrWhiteSpace(ham))
            return MaasTuru.Belirsiz;
        var u = ham.ToUpperInvariant();
        if (u.Contains("NET"))
            return MaasTuru.Net;
        if (u.Contains("BRÜT") || u.Contains("BRUT"))
            return MaasTuru.Brut;
        return MaasTuru.Belirsiz;
    }

    private static (List<string> Basliklar, List<Dictionary<int, string?>> Satirlar) OkuWorkbook(Stream stream)
    {
        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheets.First();
        var used = ws.RangeUsed();
        if (used is null)
            return (new List<string>(), new List<Dictionary<int, string?>>());

        var firstRow = used.FirstRow().RowNumber();
        var lastRow = used.LastRow().RowNumber();
        var firstCol = used.FirstColumn().ColumnNumber();
        var lastCol = used.LastColumn().ColumnNumber();

        // Birleşik başlık satırı olabilir (AİLE/İHBAR altında "Net") — ilk dolu satırı başlık kabul et.
        var headerRow = firstRow;
        for (var r = firstRow; r <= Math.Min(firstRow + 2, lastRow); r++)
        {
            var cell1 = ws.Cell(r, firstCol).GetString();
            if (cell1.Contains("T.C", StringComparison.OrdinalIgnoreCase) ||
                cell1.Contains("KİMLİK", StringComparison.OrdinalIgnoreCase) ||
                cell1.Contains("KIMLIK", StringComparison.OrdinalIgnoreCase))
            {
                headerRow = r;
                break;
            }
        }

        var basliklar = new List<string>();
        for (var c = firstCol; c <= lastCol; c++)
        {
            var ust = ws.Cell(headerRow, c).GetString().Trim();
            // Alt satırda "Net" varsa birleştir
            if (headerRow + 1 <= lastRow)
            {
                var alt = ws.Cell(headerRow + 1, c).GetString().Trim();
                if (string.Equals(alt, "Net", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(ust))
                    ust = $"{ust} (Net)";
            }
            basliklar.Add(ust);
        }

        var dataStart = headerRow + 1;
        // Eğer alt satır "Net" ise veri bir satır aşağıdan başlar
        var netAltVar = false;
        for (var c = firstCol; c <= lastCol; c++)
        {
            if (string.Equals(ws.Cell(headerRow + 1, c).GetString().Trim(), "Net", StringComparison.OrdinalIgnoreCase))
            {
                netAltVar = true;
                break;
            }
        }
        if (netAltVar)
            dataStart = headerRow + 2;

        var satirlar = new List<Dictionary<int, string?>>();
        for (var r = dataStart; r <= lastRow; r++)
        {
            var dict = new Dictionary<int, string?>();
            var bos = true;
            for (var c = firstCol; c <= lastCol; c++)
            {
                var val = HucreMetni(ws.Cell(r, c));
                dict[c - firstCol] = val;
                if (!string.IsNullOrWhiteSpace(val))
                    bos = false;
            }
            if (!bos)
                satirlar.Add(dict);
        }

        return (basliklar, satirlar);
    }

    private static string? HucreMetni(IXLCell cell)
    {
        if (cell.IsEmpty())
            return null;
        if (cell.DataType == XLDataType.DateTime)
            return cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (cell.DataType == XLDataType.Number)
            return cell.GetDouble().ToString(CultureInfo.InvariantCulture);
        return cell.GetString().Trim();
    }

    private static List<BordroSatirOnizlemeDto> ParseSatirlar(
        List<Dictionary<int, string?>> satirlar,
        IReadOnlyDictionary<int, string> kolonAlanlari)
    {
        var sonuc = new List<BordroSatirOnizlemeDto>();
        var excelSatirNo = 0;
        foreach (var satir in satirlar)
        {
            excelSatirNo++;
            string? Get(string alan) =>
                kolonAlanlari
                    .Where(kv => kv.Value == alan)
                    .Select(kv => satir.GetValueOrDefault(kv.Key))
                    .FirstOrDefault();

            var hatalar = new List<string>();
            var uyarilar = new List<string>();

            var tc = Get("TcKimlikNo");
            var ad = Get("AdSoyad");
            if (string.IsNullOrWhiteSpace(tc))
                hatalar.Add("TC kimlik no boş");
            else
            {
                try { PersonelServisi.NormalizeTc(tc); }
                catch (Exception ex) { hatalar.Add(ex.Message); }
            }

            if (string.IsNullOrWhiteSpace(ad))
                hatalar.Add("Ad soyad boş");

            var net = ParseDecimal(Get("NetIstihkak"));
            if (!net.HasValue)
                uyarilar.Add("Net istihkak boş veya okunamadı");

            sonuc.Add(new BordroSatirOnizlemeDto
            {
                SatirNo = excelSatirNo,
                TcKimlikNo = tc,
                AdSoyad = ad,
                Isyeri = Get("Isyeri"),
                Gorevi = Get("Gorevi"),
                Maas = ParseDecimal(Get("Maas")),
                MaasTuruHam = Get("Maas"),
                GirisTarihi = ParseDate(Get("GirisTarihi")),
                CikisTarihi = ParseDate(Get("CikisTarihi")),
                Gun = ParseDecimal(Get("Gun")),
                NetIstihkak = net,
                Iban = Get("Iban"),
                Avans = ParseDecimal(Get("Avans")),
                Icra = ParseDecimal(Get("Icra")),
                OtoKatBes = ParseDecimal(Get("OtoKatBes")),
                TrafikCezasi = ParseDecimal(Get("TrafikCezasi")),
                Aile = ParseDecimal(Get("Aile")),
                IhbarTazminati = ParseDecimal(Get("IhbarTazminati")),
                Yemek = ParseDecimal(Get("Yemek")),
                Yol = ParseDecimal(Get("Yol")),
                Hatalar = hatalar,
                Uyarilar = uyarilar
            });
        }

        return sonuc;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var cleaned = value.Replace("₺", "").Replace("TL", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var tr))
            return tr;
        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv))
            return inv;
        return null;
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (DateOnly.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out var d1))
            return d1;
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
            return d2;
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out var dt))
            return DateOnly.FromDateTime(dt);
        return null;
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, CancellationToken ct)
    {
        if (stream.CanSeek)
            stream.Position = 0;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static string Sha256(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexString(hash);
    }
}
