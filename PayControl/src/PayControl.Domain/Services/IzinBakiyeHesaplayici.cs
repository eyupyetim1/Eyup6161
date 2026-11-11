using System.Globalization;

namespace PayControl.Domain.Services;

/// <summary>Genel hafta tatili kuralı (VBA: Puantaj!AA6 "Sadece Pazar" / "Cumartesi" / "Cumartesi ve Pazar").</summary>
public enum HaftaTatiliPolitikasi
{
    SadecePazar = 1,
    Cumartesi = 2,
    CumartesiVePazar = 3
}

public sealed record IzinHareketGirdisi(int Id, DateOnly Tarih, string Tur, decimal Gun, string? TatilAdi, string? Aciklama, bool Iptal);
public sealed record ResmiTatilGirdisi(DateOnly Tarih, string Ad, decimal Hak);
public sealed record NakilGirdisi(DateOnly Tarih, string? EskiTesis, string? YeniTesis);
public sealed record KesinBakiyeGirdisi(DateOnly Tarih, decimal Bakiye, string? Aciklama);

/// <summary>Bir personelin bakiye hesabı için gereken tüm veriler (VBA: BK_Hesapla'nın topladıkları).</summary>
public sealed class IzinBakiyeGirdisi
{
    public string TcKimlikNo { get; init; } = string.Empty;
    public string AdSoyad { get; init; } = string.Empty;
    public string? Tesis { get; init; }
    public string? Unvan { get; init; }
    public bool Pasif { get; init; }
    public DateOnly? IseGiris { get; init; }
    public DateOnly? Cikis { get; init; }
    public DateOnly? Dogum { get; init; }
    public bool RtDahil { get; init; } = true;
    /// <summary>Kişiye özel hafta tatili günleri ("1"=Pzt … "7"=Paz); boşsa genel kural.</summary>
    public string? HaftaGunleri { get; init; }
    /// <summary>Puantaj kodları (normalize edilmiş: HT, YG, R, YI, UI …).</summary>
    public IReadOnlyDictionary<DateOnly, string> Kodlar { get; init; } = new Dictionary<DateOnly, string>();
    public KesinBakiyeGirdisi? KesinBakiye { get; init; }
    /// <summary>Anahtar yyyymm.</summary>
    public IReadOnlyDictionary<int, decimal> GorevKesintileri { get; init; } = new Dictionary<int, decimal>();
    public IReadOnlyDictionary<int, decimal> HasatKesintileri { get; init; } = new Dictionary<int, decimal>();
    /// <summary>ManuelDuzeltme Alan=HaftasonuHakki; anahtar yyyymm.</summary>
    public IReadOnlyDictionary<int, decimal> AylikHakManuel { get; init; } = new Dictionary<int, decimal>();
    /// <summary>ManuelDuzeltme Alan=YillikIzinKullanilanManuel; anahtar yıl.</summary>
    public IReadOnlyDictionary<int, decimal> YillikIzinManuel { get; init; } = new Dictionary<int, decimal>();
    /// <summary>Tarih, ID sırasıyla.</summary>
    public IReadOnlyList<IzinHareketGirdisi> Hareketler { get; init; } = Array.Empty<IzinHareketGirdisi>();
    public IReadOnlyList<NakilGirdisi> Nakiller { get; init; } = Array.Empty<NakilGirdisi>();
}

/// <summary>Defter satırı. Kova: H = hafta tatili, R = resmi tatil, B = bilgi, K = kesin bakiye.</summary>
public sealed record BakiyeDefterSatiri(DateOnly Tarih, string Aciklama, string EtkiMetni, decimal Bakiye, string Kova, decimal? Etki, int Sira);

public sealed record RtTabloSatiri(string Tatil, decimal Hak, decimal Kullanilan, DateOnly? Tarih)
{
    public decimal Kalan => Hak - Kullanilan;
}

public sealed class IzinBakiyeSonucu
{
    public string TcKimlikNo { get; init; } = string.Empty;
    public string AdSoyad { get; init; } = string.Empty;
    public string? Tesis { get; init; }
    public string? Unvan { get; init; }
    public bool Pasif { get; init; }
    public DateOnly? IseGiris { get; init; }
    public DateOnly? Cikis { get; init; }
    public DateOnly? Dogum { get; init; }
    public bool RtDahil { get; init; }
    /// <summary>Kişiye özel izin günleri ("4", "67"); boşsa genel ayar.</summary>
    public string KisiHaftaGunleri { get; init; } = string.Empty;
    public string HaftaGunMetni { get; set; } = string.Empty;

    public decimal? HT { get; set; }
    public decimal? RT { get; set; }
    /// <summary>Güncel izin durumu = HT + (RT dahilse) RT.</summary>
    public decimal? Toplam { get; set; }
    public string? Uyari { get; set; }

    public DateOnly? KesinBakiyeTarihi { get; set; }
    public decimal? KesinBakiyeDegeri { get; set; }

    public int? Kidem { get; set; }
    public int? Yas { get; set; }
    public decimal? YillikHak { get; set; }
    public decimal? YIHak { get; set; }
    public decimal YIKullanilan { get; set; }
    public decimal? YIKalan { get; set; }
    public decimal YIPuantaj { get; set; }
    public decimal YIManuel { get; set; }
    public decimal YIElle { get; set; }
    public decimal YIEk { get; set; }
    public DateOnly? YISabitTarihi { get; set; }
    public decimal? YISabitDegeri { get; set; }

    /// <summary>En yeni üstte.</summary>
    public List<BakiyeDefterSatiri> Defter { get; } = new();
    public List<RtTabloSatiri> RtTablosu { get; } = new();
}

/// <summary>
/// VBA mdlBakiye (BK_KisiHesapla) kurallarının birebir karşılığı: hafta tatili (HT) ve resmi tatil (RT)
/// bakiyesi, kesin bakiye, Üİ ile yanan hak, rapor kesintisi, Gİ/Hİ kesintileri, aylık manuel hak,
/// izin hareketleri ve yıllık izin (bakiyeden ayrı).
/// </summary>
public static class IzinBakiyeHesaplayici
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] GunAdlari = { "", "Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar" };

    /// <summary>VBA FY_KodNorm: Ç→C, İ/ı→I, Ü→U, Ö→O, Ş→S, Ğ→G, büyük harf.</summary>
    public static string KodNormalize(string? kod)
    {
        if (string.IsNullOrWhiteSpace(kod)) return string.Empty;
        var s = kod.Trim().ToUpper(Tr);
        return s.Replace('Ç', 'C').Replace('İ', 'I').Replace('I', 'I').Replace('Ü', 'U')
                .Replace('Ö', 'O').Replace('Ş', 'S').Replace('Ğ', 'G').Replace('Â', 'A');
    }

    public static HaftaTatiliPolitikasi PolitikaCoz(string? metin)
    {
        var m = KodNormalize(string.IsNullOrWhiteSpace(metin) ? "Sadece Pazar" : metin);
        if (m.Contains("CUMARTESI") && m.Contains("PAZAR")) return HaftaTatiliPolitikasi.CumartesiVePazar;
        if (m.Contains("CUMARTESI")) return HaftaTatiliPolitikasi.Cumartesi;
        return HaftaTatiliPolitikasi.SadecePazar;
    }

    public static string PolitikaMetni(HaftaTatiliPolitikasi p) => p switch
    {
        HaftaTatiliPolitikasi.Cumartesi => "Cumartesi",
        HaftaTatiliPolitikasi.CumartesiVePazar => "Cumartesi ve Pazar",
        _ => "Sadece Pazar"
    };

    /// <summary>VBA BK_GunTemizle: yalnızca 1-7, tekrarsız ve sıralı.</summary>
    public static string GunTemizle(string? gun)
    {
        if (string.IsNullOrEmpty(gun)) return string.Empty;
        var s = string.Empty;
        for (var i = 1; i <= 7; i++)
            if (gun.Contains((char)('0' + i))) s += (char)('0' + i);
        return s;
    }

    public static string GunMetni(string? gun, HaftaTatiliPolitikasi? genel = null)
    {
        gun = GunTemizle(gun);
        if (gun.Length == 0) return genel is null ? string.Empty : $"Genel ayar ({PolitikaMetni(genel.Value)})";
        return string.Join(" + ", gun.Select(c => GunAdlari[c - '0']));
    }

    /// <summary>1=Pazartesi … 7=Pazar (VBA Weekday(d, vbMonday)).</summary>
    public static int HaftaGunu(DateOnly d) => d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek;

    /// <summary>VBA FY_KidemYil: tam yıl.</summary>
    public static int KidemYil(DateOnly bas, DateOnly bit)
    {
        if (bit < bas) return 0;
        var y = bit.Year - bas.Year;
        if (bit.Month < bas.Month || (bit.Month == bas.Month && bit.Day < bas.Day)) y--;
        return Math.Max(0, y);
    }

    /// <summary>VBA FY_YillikIzinHakki (İş Kanunu md. 53).</summary>
    public static int YillikIzinHakki(int kidem, int yas = -1)
    {
        int h = kidem < 1 ? 0 : kidem <= 5 ? 14 : kidem < 15 ? 20 : 26;
        if (kidem >= 1 && yas >= 0 && (yas >= 50 || yas <= 18) && h < 20) h = 20;
        return h;
    }

    /// <summary>VBA BK_RaporCeza: her 7 R için 1, kalan 6 ve üstüyse +1.</summary>
    public static decimal RaporCeza(decimal r)
    {
        var tam = Math.Floor(r / 7m);
        var kalan = r - tam * 7m;
        return tam + (kalan >= 6 ? 1 : 0);
    }

    /// <summary>VBA BK_Metin.</summary>
    public static string Metin(decimal? v, bool isaretli = true)
    {
        if (v is null) return "—";
        var s = Math.Round(v.Value, 1, MidpointRounding.AwayFromZero).ToString("0.#", Tr);
        if (s == "-0") s = "0";
        if (isaretli && v.Value > 0) s = "+" + s;
        return s;
    }

    private static DateOnly DateSerial(int yil, int ay, int gun)
    {
        // VBA DateSerial taşmayı kabul eder (ör. 29 Şubat → 1 Mart)
        var d = new DateOnly(yil, 1, 1).AddMonths(ay - 1);
        return d.AddDays(gun - 1);
    }

    private static DateOnly AySonu(DateOnly d) => new DateOnly(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));

    private sealed class Olay
    {
        public DateOnly Gun; public int Sira; public string Metin = ""; public decimal? Etki; public string Kova = ""; public int No;
    }

    private sealed class Durum
    {
        public decimal HT, RT;
        public DateOnly? SabitGun;
        public int SabitAy;
        public readonly List<Olay> Defter = new();
        public bool Defterli;

        public void Ekle(DateOnly gun, int sira, string metin, decimal? etki, string kova)
        {
            if (kova is "H" or "R")
            {
                if (SabitGun is { } sg && gun <= sg) return;
                if (kova == "H") HT += etki ?? 0; else RT += etki ?? 0;
            }
            else if (kova == "B")
            {
                if (SabitGun is { } sg && gun <= sg && sira > 0) return;
            }
            else if (kova == "Y") kova = "B";
            else if (kova == "K") HT = etki ?? 0;
            if (Defterli) Defter.Add(new Olay { Gun = gun, Sira = sira, Metin = metin, Etki = etki, Kova = kova, No = Defter.Count + 1 });
        }
    }

    public static IzinBakiyeSonucu Hesapla(IzinBakiyeGirdisi p, IReadOnlyList<ResmiTatilGirdisi> tatiller,
        HaftaTatiliPolitikasi politika, DateOnly bugun, bool ayrinti = true)
    {
        var kisiGun = GunTemizle(p.HaftaGunleri);
        var s = new IzinBakiyeSonucu
        {
            TcKimlikNo = p.TcKimlikNo, AdSoyad = p.AdSoyad, Tesis = p.Tesis, Unvan = p.Unvan, Pasif = p.Pasif,
            IseGiris = p.IseGiris, Cikis = p.Cikis, Dogum = p.Dogum, RtDahil = p.RtDahil, KisiHaftaGunleri = kisiGun, HaftaGunMetni = GunMetni(kisiGun)
        };
        var bit = p.Cikis ?? bugun;
        if (bit > bugun) bit = bugun;
        var st = new Durum { Defterli = ayrinti };
        // yalnızca bugüne kadarki kodlar
        var kd = p.Kodlar.Where(k => k.Key <= bugun && k.Value.Length > 0).ToDictionary(k => k.Key, k => k.Value);
        var rtTablo = new Dictionary<string, (decimal hak, decimal kul, DateOnly? gun)>();

        // --- yıllık izin (bakiyeden bağımsız)
        bool yiSabitVar = false; DateOnly yiSabitTarih = default; decimal yiSabitDeger = 0, yiHak = 0;
        bool yiHakVar = false;
        foreach (var h in p.Hareketler)
            if (!h.Iptal && Tur(h) == "YISABIT" && (!yiSabitVar || h.Tarih >= yiSabitTarih))
            { yiSabitVar = true; yiSabitTarih = h.Tarih; yiSabitDeger = h.Gun; }
        if (yiSabitVar)
        {
            yiHak = yiSabitDeger; yiHakVar = true;
            s.YISabitTarihi = yiSabitTarih; s.YISabitDegeri = yiSabitDeger;
            st.Ekle(yiSabitTarih, 7, $"Yıllık izin kalan {Metin(yiSabitDeger, false)} gün olarak belirlendi (bakiyeyi etkilemez)", null, "Y");
        }
        if (p.IseGiris is { } bas)
        {
            var kidem = KidemYil(bas, bit);
            s.Kidem = kidem;
            if (p.Dogum is { } dg)
            {
                s.Yas = KidemYil(dg, bit);
                s.YillikHak = YillikIzinHakki(kidem, s.Yas.Value);
            }
            else s.YillikHak = YillikIzinHakki(kidem);
            for (var yil = 1; yil <= kidem; yil++)
            {
                var yd = DateSerial(bas.Year + yil, bas.Month, bas.Day);
                if (yiSabitVar && yd <= yiSabitTarih) continue;
                var yas = p.Dogum is { } d2 ? KidemYil(d2, yd) : -1;
                var yilHak = YillikIzinHakki(yil, yas);
                yiHak += yilHak;
                var ek = yas >= 50 ? " (50 yaş üstü)" : yas >= 0 && yas <= 18 ? " (18 yaş ve altı)" : "";
                st.Ekle(yd, 7, $"Yıllık izin hakkı: {yil}. hizmet yılı {yilHak} gün{ek} (bakiyeyi etkilemez)", null, "Y");
            }
            yiHakVar = true;
        }
        decimal yiKul = 0, yiMan = 0, yiElle = 0, yiEk = 0;
        foreach (var (gun, kod) in kd)
            if (kod == "YI" && HaftaGunu(gun) != 7 && (!yiSabitVar || gun > yiSabitTarih)) yiKul++;
        foreach (var (yil, deger) in p.YillikIzinManuel)
            if (!yiSabitVar || yil > yiSabitTarih.Year) yiMan += deger;
        s.YIPuantaj = yiKul;
        yiKul += yiMan;
        s.YIManuel = yiMan;
        foreach (var h in p.Hareketler)
        {
            if (h.Iptal || (yiSabitVar && h.Tarih <= yiSabitTarih)) continue;
            if (Tur(h) == "YIKULLANIM") yiElle += h.Gun;
            if (Tur(h) == "YIDEVIR") yiEk += h.Gun;
        }
        yiKul += yiElle;
        s.YIElle = yiElle; s.YIEk = yiEk;
        s.YIKullanilan = yiKul;
        if (yiHakVar || yiEk != 0)
        {
            s.YIHak = yiHak + yiEk;
            s.YIKalan = s.YIHak - yiKul;
        }

        // --- başlangıç
        DateOnly baslangic;
        if (p.KesinBakiye is { } sb)
        {
            st.SabitGun = sb.Tarih;
            st.SabitAy = sb.Tarih.Year * 100 + sb.Tarih.Month;
            s.KesinBakiyeTarihi = sb.Tarih; s.KesinBakiyeDegeri = sb.Bakiye;
            baslangic = sb.Tarih.AddDays(1);
        }
        else if (p.IseGiris is { } b2) baslangic = b2;
        else return Bos(s, "İşe giriş tarihi yok", st);
        if (p.IseGiris is { } b3 && baslangic < b3) baslangic = b3;
        if (p.Pasif && p.Cikis is null && st.SabitGun is null) return Bos(s, "Pasif, çıkış tarihi yok", st);

        // --- bilgi satırları
        if (p.IseGiris is { } b4) st.Ekle(b4, 0, "İşe giriş", null, "B");
        foreach (var nk in p.Nakiller) st.Ekle(nk.Tarih, 0, $"Tesis değişikliği: {nk.EskiTesis} → {nk.YeniTesis}", null, "B");
        if (p.Cikis is { } ck) st.Ekle(ck, 8, "İşten çıkış", null, "B");
        if (p.KesinBakiye is { } sb2)
            st.Ekle(sb2.Tarih, 9, $"Kesin bakiye {Metin(sb2.Bakiye, false)} olarak belirlendi" +
                (string.IsNullOrWhiteSpace(sb2.Aciklama) ? "" : $" ({sb2.Aciklama})"), sb2.Bakiye, "K");

        // --- Üİ haftaları (anahtar: haftanın pazarı)
        var ui = new Dictionary<DateOnly, int>();
        foreach (var (gun, kod) in kd)
            if (kod == "UI")
            {
                var hs = gun.AddDays(7 - HaftaGunu(gun));
                ui[hs] = ui.GetValueOrDefault(hs) + 1;
            }
        var tatilGunleri = tatiller.Select(t => t.Tarih).ToHashSet();

        bool HaftaTatiliMi(int wd)
        {
            if (kisiGun.Length > 0) return kisiGun.Contains((char)('0' + wd));
            return politika switch
            {
                HaftaTatiliPolitikasi.CumartesiVePazar => wd is 6 or 7,
                HaftaTatiliPolitikasi.Cumartesi => wd == 6,
                _ => wd == 7
            };
        }

        // --- günlük döngü
        decimal ayHak = 0, ayR = 0;
        for (var d = baslangic; d <= bit; d = d.AddDays(1))
        {
            var wd = HaftaGunu(d);
            if (HaftaTatiliMi(wd))
            {
                var hs = d.AddDays(7 - wd);
                var n = ui.GetValueOrDefault(hs);
                if (n < 2) { ayHak += 1; st.Ekle(d, 1, "Hafta tatili hakkı", 1m, "H"); }
                else if (ayrinti) st.Ekle(d, 1, $"Hafta tatili hakkı yandı (haftada {n} Üİ)", null, "B");
            }
            if (kd.TryGetValue(d, out var kod))
            {
                switch (kod)
                {
                    case "HT":
                        st.Ekle(d, 3, "HT kullanımı" + (tatilGunleri.Contains(d) ? " (resmi tatil günü)" : ""), -1m, "H"); break;
                    case "YG": st.Ekle(d, 3, "YG (yarım gün) kullanımı", -0.5m, "H"); break;
                    case "R": ayR += 1; break;
                    case "YI":
                        if (wd == 7) st.Ekle(d, 3, "Yıllık izne denk gelen pazar (HT sayıldı)", -1m, "H");
                        break;
                }
            }
            else if (wd == 7 && IzinDonusTatili(kd, d))
                st.Ekle(d, 3, "Yıllık izin dönüşü pazar (HT sayıldı)", -1m, "H");

            if (d.AddDays(1).Day == 1 || d == bit)
            {
                var ay = d.Year * 100 + d.Month;
                var ceza = RaporCeza(ayR);
                if (ceza > ayHak) ceza = ayHak;
                if (ceza > 0) st.Ekle(d, 4, $"Rapor kesintisi ({ayR.ToString("0.#", Tr)} gün R)", -ceza, "H");
                var otomatik = ayHak - ceza;
                if (p.AylikHakManuel.TryGetValue(ay, out var deger) && deger != otomatik)
                    st.Ekle(d, 5, $"Aylık hak manuel: {Metin(deger, false)} (otomatik {Metin(otomatik, false)})", deger - otomatik, "H");
                ayHak = 0; ayR = 0;
            }
        }

        // --- Gİ / Hİ kesintileri
        KesintiOlaylari(p.GorevKesintileri, "Görev izni kesintisi", p, bit, st);
        KesintiOlaylari(p.HasatKesintileri, "Hasat izni kesintisi", p, bit, st);

        // --- resmi tatil hakları
        foreach (var t in tatiller)
        {
            if (!TatilUygun(p, t.Tarih, bit, st)) continue;
            if (HaftaGunu(t.Tarih) == 7)
                st.Ekle(t.Tarih, 2, "Resmi tatil pazara denk geldi, hak verilmedi: " + t.Ad, null, "B");
            else
            {
                st.Ekle(t.Tarih, 2, "Resmi tatil hakkı: " + t.Ad, t.Hak, "R");
                RtEkle(rtTablo, $"{t.Ad} {t.Tarih.Year}", t.Hak, 0, t.Tarih);
            }
        }

        // --- manuel hareketler
        foreach (var h in p.Hareketler)
        {
            if (h.Iptal) continue;
            var ack = string.IsNullOrWhiteSpace(h.Aciklama) ? "" : " - " + h.Aciklama!.Trim();
            switch (Tur(h))
            {
                case "HTDUZELTME": st.Ekle(h.Tarih, 6, "HT düzeltmesi" + ack, h.Gun, "H"); break;
                case "RTEK": st.Ekle(h.Tarih, 6, (h.Gun >= 0 ? "Resmi tatil ek hak" : "Resmi tatil hakkı düşürüldü") + ack, h.Gun, "R"); break;
                case "RTKULLANIM":
                    st.Ekle(h.Tarih, 6, "Resmi tatil kullanımı" + (string.IsNullOrEmpty(h.TatilAdi) ? "" : ": " + h.TatilAdi) + ack, -h.Gun, "R");
                    RtEkle(rtTablo, string.IsNullOrEmpty(h.TatilAdi) ? "(tatil seçilmeden)" : h.TatilAdi!, 0, h.Gun, null);
                    break;
                case "YIKULLANIM": st.Ekle(h.Tarih, 7, $"Yıllık izin kullanımı {Metin(h.Gun, false)} gün (bakiyeyi etkilemez){ack}", null, "Y"); break;
                case "YIDEVIR": st.Ekle(h.Tarih, 7, $"Yıllık izin ekle / düş {Metin(h.Gun)} gün (bakiyeyi etkilemez){ack}", null, "Y"); break;
            }
        }

        s.HT = st.HT;
        s.RT = st.RT;
        s.Toplam = st.HT + (p.RtDahil ? st.RT : 0);
        if (ayrinti)
        {
            DefterSirala(st, p.RtDahil, s.Defter);
            s.RtTablosu.AddRange(rtTablo.Select(k => new RtTabloSatiri(k.Key, k.Value.hak, k.Value.kul, k.Value.gun))
                .OrderBy(r => r.Tarih ?? DateOnly.MaxValue));
        }
        return s;
    }

    private static string Tur(IzinHareketGirdisi h) => (h.Tur ?? string.Empty).Trim().ToUpperInvariant();

    private static IzinBakiyeSonucu Bos(IzinBakiyeSonucu s, string uyari, Durum st)
    {
        s.HT = null; s.RT = null; s.Toplam = null; s.Uyari = uyari;
        return s;
    }

    private static void RtEkle(Dictionary<string, (decimal hak, decimal kul, DateOnly? gun)> t, string ad, decimal hak, decimal kul, DateOnly? gun)
    {
        var a = t.TryGetValue(ad, out var v) ? v : (0m, 0m, gun);
        a.Item1 += hak; a.Item2 += kul;
        if (gun is { } g && (a.Item3 is null || g < a.Item3)) a.Item3 = g;
        t[ad] = a;
    }

    /// <summary>VBA BK_IzinDonusTatili: geriye doğru (boş pazarları atlayarak) önceki dolu gün Yİ mi.</summary>
    private static bool IzinDonusTatili(Dictionary<DateOnly, string> kd, DateOnly d)
    {
        var g = d.AddDays(-1);
        for (var i = 1; i <= 3; i++)
        {
            if (kd.TryGetValue(g, out var k)) return k == "YI";
            if (HaftaGunu(g) != 7) return false;
            g = g.AddDays(-1);
        }
        return false;
    }

    private static bool TatilUygun(IzinBakiyeGirdisi p, DateOnly gun, DateOnly bit, Durum st)
    {
        if (gun > bit) return false;
        if (p.IseGiris is { } bas) { if (gun < bas) return false; }
        else if (st.SabitGun is null) return false;
        return true;
    }

    private static void KesintiOlaylari(IReadOnlyDictionary<int, decimal> kes, string metin, IzinBakiyeGirdisi p, DateOnly bit, Durum st)
    {
        foreach (var (ym, deger) in kes)
        {
            var ayIlk = new DateOnly(ym / 100, ym % 100, 1);
            var tarih = AySonu(ayIlk);
            if (tarih > bit) tarih = bit;
            if (st.SabitGun is not null && ym <= st.SabitAy) continue;
            if (p.IseGiris is { } bas && AySonu(ayIlk) < bas) continue;
            if (ayIlk > bit) continue;
            if (deger != 0)
                st.Ekle(tarih, 4, $"{metin} ({ayIlk.ToString("MMMM yyyy", Tr)})", -deger, "H");
        }
    }

    private static void DefterSirala(Durum st, bool rtDahil, List<BakiyeDefterSatiri> sonuc)
    {
        decimal bakiye = 0;
        var sirali = st.Defter.OrderBy(o => o.Gun).ThenBy(o => o.Sira).ThenBy(o => o.No).ToList();
        var liste = new List<BakiyeDefterSatiri>(sirali.Count);
        foreach (var o in sirali)
        {
            string etkiMetni;
            switch (o.Kova)
            {
                case "K": bakiye = o.Etki ?? 0; etkiMetni = "= " + Metin(o.Etki, false); break;
                case "H": bakiye += o.Etki ?? 0; etkiMetni = Metin(o.Etki); break;
                case "R":
                    if (rtDahil) { bakiye += o.Etki ?? 0; etkiMetni = Metin(o.Etki); }
                    else etkiMetni = "(" + Metin(o.Etki) + ")";
                    break;
                default: etkiMetni = "—"; break;
            }
            liste.Add(new BakiyeDefterSatiri(o.Gun, o.Metin, etkiMetni, bakiye, o.Kova, o.Etki, o.Sira));
        }
        liste.Reverse(); // en yeni üstte
        sonuc.AddRange(liste);
    }

    private static bool HaftaTatiliGunu(int wd, string kisiGun, HaftaTatiliPolitikasi politika)
    {
        if (kisiGun.Length > 0) return kisiGun.Contains((char)('0' + wd));
        return politika switch
        {
            HaftaTatiliPolitikasi.CumartesiVePazar => wd is 6 or 7,
            HaftaTatiliPolitikasi.Cumartesi => wd == 6,
            _ => wd == 7
        };
    }

    /// <summary>VBA BK_IzinGunuSay: iki tarih arası izin günü; hafta tatili günleri sayılmaz, resmi tatil (1 - hak) sayılır.</summary>
    public static decimal IzinGunuSay(DateOnly d1, DateOnly d2, string? kisiGunleri, HaftaTatiliPolitikasi politika, IReadOnlyList<ResmiTatilGirdisi> tumTatiller)
    {
        if (d2 < d1) return 0;
        var kisi = GunTemizle(kisiGunleri);
        var tatil = new Dictionary<DateOnly, decimal>();
        foreach (var t in tumTatiller) tatil.TryAdd(t.Tarih, t.Hak);
        decimal n = 0;
        for (var d = d1; d <= d2; d = d.AddDays(1))
            if (!HaftaTatiliGunu(HaftaGunu(d), kisi, politika))
                n += tatil.TryGetValue(d, out var h) ? 1 - h : 1;
        return n;
    }

    public sealed record IzinBitisSonucu(DateOnly SonGun, DateOnly IseBaslama, int Pazar, int ResmiTatil, int TakvimGunu, IReadOnlyList<(DateOnly Gun, string Aciklama)> Gunler);

    /// <summary>VBA BK_IzinBitisHesapla: yıllık izinde pazarlar ve tam gün resmi tatiller sayılmaz; izin sonrası pazar/tatil atlanır.</summary>
    public static IzinBitisSonucu? IzinBitisHesapla(DateOnly bas, int isGunu, IReadOnlyList<ResmiTatilGirdisi> tumTatiller)
    {
        if (isGunu < 1) return null;
        var tatil = new Dictionary<DateOnly, string>();
        foreach (var t in tumTatiller) if (t.Hak >= 1) tatil.TryAdd(t.Tarih, t.Ad);
        var gunler = new List<(DateOnly, string)>();
        int sayilan = 0, ht = 0, rt = 0;
        var d = bas;
        while (true)
        {
            if (HaftaGunu(d) == 7) { ht++; gunler.Add((d, "Pazar")); }
            else if (tatil.TryGetValue(d, out var ad)) { rt++; gunler.Add((d, "Resmi tatil: " + ad)); }
            else { sayilan++; gunler.Add((d, $"İzin ({sayilan}. gün)")); }
            if (sayilan >= isGunu) break;
            d = d.AddDays(1);
        }
        var isbasi = d.AddDays(1);
        for (var i = 1; i <= 10; i++)
        {
            if (HaftaGunu(isbasi) == 7) gunler.Add((isbasi, "Pazar (izin dönüşü)"));
            else if (tatil.TryGetValue(isbasi, out var ad)) gunler.Add((isbasi, $"Resmi tatil: {ad} (izin dönüşü)"));
            else break;
            isbasi = isbasi.AddDays(1);
        }
        return new IzinBitisSonucu(d, isbasi, ht, rt, d.DayNumber - bas.DayNumber + 1, gunler);
    }

    /// <summary>VBA BK_YIBitis: elle girilen yıllık izin kaydının yaklaşık bitişi.</summary>
    public static DateOnly YillikIzinBitisi(DateOnly bas, decimal gun, string? aciklama)
    {
        var a = aciklama ?? "";
        if (a.Length >= 21 && a.IndexOf('-') == 10 &&
            DateOnly.TryParseExact(a.Substring(11, 10).Trim(), "dd.MM.yyyy", Tr, DateTimeStyles.None, out var d) && d >= bas) return d;
        if (gun < 1) gun = 1;
        return bas.AddDays((int)Math.Floor(gun) - 1 + (int)Math.Floor(gun / 6));
    }

    /// <summary>VBA BK_Ozet: bakiyenin kalem kalem özeti.</summary>
    public static List<(string Kalem, string Deger)> Ozet(IzinBakiyeSonucu p)
    {
        var c = new List<(string, string)>();
        if (p.Toplam is null) { c.Add(("Hesaplanamadı", p.Uyari ?? "")); return c; }
        decimal hak = 0, kul = 0, rap = 0, kes = 0, man = 0, rtHak = 0, rtMan = 0;
        foreach (var a in p.Defter)
        {
            var e = a.Etki ?? 0;
            if (a.Kova == "H")
                switch (a.Sira)
                {
                    case 1: hak += e; break;
                    case 3: kul += e; break;
                    case 4: if (a.Aciklama.StartsWith("Rapor", StringComparison.Ordinal)) rap += e; else kes += e; break;
                    default: man += e; break;
                }
            else if (a.Kova == "R") { if (a.Sira == 2) rtHak += e; else rtMan += e; }
        }
        if (p.KesinBakiyeTarihi is { } kt) c.Add(($"Kesin bakiye {kt:dd.MM.yyyy}", Metin(p.KesinBakiyeDegeri, false)));
        else if (p.IseGiris is { } bas) c.Add(($"Başlangıç (işe giriş {bas:dd.MM.yyyy})", "0"));
        c.Add(("Hafta tatili hakkı", Metin(hak)));
        if (kul != 0) c.Add(("HT / YG kullanımı", Metin(kul)));
        if (rap != 0) c.Add(("Rapor kesintisi", Metin(rap)));
        if (kes != 0) c.Add(("Gİ / Hİ kesintisi", Metin(kes)));
        if (man != 0) c.Add(("Manuel düzeltme", Metin(man)));
        if (rtHak != 0 || rtMan != 0)
        {
            c.Add(("Resmi tatil hakkı" + (p.RtDahil ? "" : " (dahil değil)"), Metin(rtHak)));
            if (rtMan != 0) c.Add(("RT kullanım / ek" + (p.RtDahil ? "" : " (dahil değil)"), Metin(rtMan)));
        }
        c.Add(("= Güncel bakiye", Metin(p.Toplam)));
        return c;
    }
}
