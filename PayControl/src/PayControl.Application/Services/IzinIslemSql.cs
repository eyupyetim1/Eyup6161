using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Entities.Pys;
using PayControl.Domain.Services;

namespace PayControl.Application.Services;

/// <summary>
/// VBA'daki işlem günlüğü (IzinIslemLog) ileri / geri alma komutlarının yorumlayıcısı.
/// Komutlar VBA ile aynı biçimde (Access SQL) yazılır ve okunur; böylece Excel'den gelen eski kayıtlar
/// da bu programdan geri alınabilir. Desteklenen biçim: UPDATE … SET a=b[, …] WHERE …, DELETE FROM … WHERE …,
/// INSERT INTO … (…) VALUES (…). WHERE: kolon=değer / kolon IN (…) koşulları AND ile.
/// </summary>
public static class IzinIslemSql
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Metin(string? s) => "'" + (s ?? string.Empty).Replace("'", "''") + "'";
    public static string Tarih(DateOnly d) => "#" + d.ToString("yyyy-MM-dd", Inv) + "#";
    public static string Sayi(decimal d) => d.ToString(Inv);
    public static string Mantik(bool b) => b ? "True" : "False";

    // ------------------------------------------------------------------ çalıştırma

    /// <summary>Komutu çalıştırır; INSERT'te oluşan kaydın ID'sini döndürür.</summary>
    public static async Task<int?> CalistirAsync(IPayControlDbContext db, string komut, CancellationToken ct = default)
    {
        komut = komut.Trim();
        if (komut.Length == 0) return null;
        Match m;
        if ((m = Regex.Match(komut, @"^UPDATE\s+\[?(\w+)\]?\s+SET\s+(.+?)\s+WHERE\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)).Success)
        {
            var tablo = await TabloAsync(db, m.Groups[1].Value, ct);
            var atamalar = Bol(m.Groups[2].Value, ',').Select(a => a.Split('=', 2)).Select(a => (a[0].Trim(), Deger(a[1].Trim()))).ToList();
            var kosul = Kosullar(m.Groups[3].Value);
            foreach (var satir in tablo.Satirlar.Where(s => Uyar(tablo, s, kosul)).ToList())
                foreach (var (k, v) in atamalar) tablo.Yaz(satir, k, v);
            return null;
        }
        if ((m = Regex.Match(komut, @"^DELETE\s+FROM\s+\[?(\w+)\]?\s+WHERE\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)).Success)
        {
            var tablo = await TabloAsync(db, m.Groups[1].Value, ct);
            var kosul = Kosullar(m.Groups[2].Value);
            foreach (var satir in tablo.Satirlar.Where(s => Uyar(tablo, s, kosul)).ToList()) tablo.Sil(satir);
            return null;
        }
        if ((m = Regex.Match(komut, @"^INSERT\s+INTO\s+\[?(\w+)\]?\s*\((.+?)\)\s*VALUES\s*\((.*)\)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)).Success)
        {
            var tablo = await TabloAsync(db, m.Groups[1].Value, ct);
            var kolonlar = m.Groups[2].Value.Split(',').Select(k => k.Trim().Trim('[', ']')).ToList();
            var degerler = Bol(m.Groups[3].Value, ',').Select(v => Deger(v.Trim())).ToList();
            if (kolonlar.Count != degerler.Count) throw new InvalidOperationException("Komut çözülemedi: " + komut);
            var satir = tablo.Yeni();
            for (var i = 0; i < kolonlar.Count; i++) tablo.Yaz(satir, kolonlar[i], degerler[i]);
            var id = tablo.KimlikAta(satir);
            tablo.Ekle(satir);
            return id;
        }
        throw new InvalidOperationException("Desteklenmeyen işlem komutu: " + komut);
    }

    // ------------------------------------------------------------------ ayrıştırma

    private static List<string> Bol(string s, char ayirici)
    {
        var sonuc = new List<string>();
        var sb = new StringBuilder();
        bool tirnak = false; int parantez = 0;
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c == '\'') { tirnak = !tirnak; sb.Append(c); continue; }
            if (!tirnak && c == '(') parantez++;
            if (!tirnak && c == ')') parantez--;
            if (!tirnak && parantez == 0 && c == ayirici) { sonuc.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(c);
        }
        sonuc.Add(sb.ToString());
        return sonuc;
    }

    private static List<(string Kolon, List<object?> Degerler)> Kosullar(string where)
    {
        var parcalar = Regex.Split(where, @"\s+AND\s+(?=(?:[^']*'[^']*')*[^']*$)", RegexOptions.IgnoreCase);
        var sonuc = new List<(string, List<object?>)>();
        foreach (var p in parcalar)
        {
            var t = p.Trim();
            if (t.StartsWith('(') && t.EndsWith(')')) t = t[1..^1].Trim();
            // "(Iptal=False OR Iptal IS NULL)" gibi koşullar: iptal edilmemiş
            if (Regex.IsMatch(t, @"^Iptal\s*=\s*False\s+OR\s+Iptal\s+IS\s+NULL$", RegexOptions.IgnoreCase)) { sonuc.Add(("Iptal", new List<object?> { false })); continue; }
            var mIn = Regex.Match(t, @"^\[?(\w+)\]?\s+IN\s*\((.*)\)$", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (mIn.Success) { sonuc.Add((mIn.Groups[1].Value, Bol(mIn.Groups[2].Value, ',').Select(v => Deger(v.Trim())).ToList())); continue; }
            var mEs = Regex.Match(t, @"^\[?(\w+)\]?\s*=\s*(.+)$", RegexOptions.Singleline);
            if (mEs.Success) { sonuc.Add((mEs.Groups[1].Value, new List<object?> { Deger(mEs.Groups[2].Value.Trim()) })); continue; }
            throw new InvalidOperationException("Koşul çözülemedi: " + t);
        }
        return sonuc;
    }

    private static object? Deger(string v)
    {
        if (v.StartsWith('\'') && v.EndsWith('\'') && v.Length >= 2) return v[1..^1].Replace("''", "'");
        if (v.Equals("Null", StringComparison.OrdinalIgnoreCase)) return null;
        if (v.Equals("True", StringComparison.OrdinalIgnoreCase)) return true;
        if (v.Equals("False", StringComparison.OrdinalIgnoreCase)) return false;
        if (v.Equals("Now()", StringComparison.OrdinalIgnoreCase)) return DateTime.Now;
        if (v.StartsWith('#') && v.EndsWith('#'))
        {
            var ic = v.Trim('#');
            if (DateTime.TryParseExact(ic, new[] { "yyyy-MM-dd", "yyyy-MM-dd HH:mm:ss", "MM/dd/yyyy", "M/d/yyyy" }, Inv, DateTimeStyles.None, out var d)) return d;
            throw new InvalidOperationException("Tarih çözülemedi: " + v);
        }
        if (decimal.TryParse(v, NumberStyles.Number, Inv, out var n)) return n;
        throw new InvalidOperationException("Değer çözülemedi: " + v);
    }

    private static bool Uyar(Tablo t, object satir, List<(string Kolon, List<object?> Degerler)> kosul) =>
        kosul.All(k => k.Degerler.Any(d => Esit(t.Oku(satir, k.Kolon), d)));

    private static bool Esit(object? a, object? b)
    {
        if (a is null || b is null) return a is null && b is null || (a is false && b is null) || (b is false && a is null);
        if (a is DateOnly da) a = da.ToDateTime(TimeOnly.MinValue);
        if (b is DateOnly db) b = db.ToDateTime(TimeOnly.MinValue);
        if (a is DateTime x && b is DateTime y) return x.Date == y.Date;
        if (a is bool ab && b is bool bb) return ab == bb;
        if (IsSayi(a) && IsSayi(b)) return Convert.ToDecimal(a, Inv) == Convert.ToDecimal(b, Inv);
        return string.Equals(Convert.ToString(a, Inv)?.Trim(), Convert.ToString(b, Inv)?.Trim(), StringComparison.Ordinal);
    }

    private static bool IsSayi(object o) => o is int or long or decimal or double or float or short;

    // ------------------------------------------------------------------ tablolar

    private sealed class Tablo
    {
        public required List<object> Satirlar;
        public required Func<object, string, object?> Oku;
        public required Action<object, string, object?> Yaz;
        public required Func<object> Yeni;
        public required Action<object> Sil;
        public required Action<object> Ekle;
        public Func<object, int?> KimlikAta = _ => null;
    }

    private static async Task<Tablo> TabloAsync(IPayControlDbContext db, string ad, CancellationToken ct)
    {
        if (ad.Equals("PersonelIzinAyar", StringComparison.OrdinalIgnoreCase))
        {
            var liste = (await db.PersonelIzinAyarlari.ToListAsync(ct)).Cast<object>().ToList();
            static string Kol(string k) => k.ToUpperInvariant() switch { "TCKN" => "TcKimlikNo", "RTDAHIL" => "RtDahil", _ => k };
            return new Tablo
            {
                Satirlar = liste,
                Oku = (s, k) => Prop(s.GetType(), Kol(k)).GetValue(s),
                Yaz = (s, k, v) => YazProp(s, Kol(k), k.Equals("RTDahil", StringComparison.OrdinalIgnoreCase) && v is null ? true : v),
                Yeni = () => new PersonelIzinAyari(),
                Ekle = s => { db.PersonelIzinAyarlari.Add((PersonelIzinAyari)s); liste.Add(s); },
                Sil = s => { db.PersonelIzinAyarlari.Remove((PersonelIzinAyari)s); liste.Remove(s); }
            };
        }
        if (ad.StartsWith("PuantajGunluk_", StringComparison.OrdinalIgnoreCase))
        {
            var ek = ad["PuantajGunluk_".Length..];
            var tesisler = await db.PuantajGunleri.Select(p => p.TesisAdi).Distinct().ToListAsync(ct);
            var kartTesis = await db.Personeller.Where(p => p.Isyeri != null).Select(p => p.Isyeri!).Distinct().ToListAsync(ct);
            var tanimli = await db.Tesisler.Select(t => t.Ad).ToListAsync(ct);
            var tesis = tesisler.Concat(kartTesis).Concat(tanimli).FirstOrDefault(t => string.Equals(PuantajHesaplayici.TabloAdi(t), ek, StringComparison.OrdinalIgnoreCase))
                        ?? throw new InvalidOperationException("Puantaj tablosunun tesisi bulunamadı: " + ad);
            var liste = (await db.PuantajGunleri.Where(p => p.TesisAdi == tesis).ToListAsync(ct)).Cast<object>().ToList();
            static string Kol(string k) => k.ToUpperInvariant() == "TCKN" ? "TcKimlikNo" : k;
            return new Tablo
            {
                Satirlar = liste,
                Oku = (s, k) => Prop(typeof(PuantajGunu), Kol(k)).GetValue(s),
                Yaz = (s, k, v) => YazProp(s, Kol(k), k.Equals("Kod", StringComparison.OrdinalIgnoreCase) ? (PuantajHesaplayici.Standart(Convert.ToString(v, Inv) ?? "") ?? Convert.ToString(v, Inv)) : v),
                Yeni = () => new PuantajGunu { TesisAdi = tesis, Kullanici = "geri alma" },
                Ekle = s => { db.PuantajGunleri.Add((PuantajGunu)s); liste.Add(s); },
                Sil = s => { db.PuantajGunleri.Remove((PuantajGunu)s); liste.Remove(s); }
            };
        }
        if (!PysTablolari.Tumu.TryGetValue(ad, out var tip))
        {
            var k = PysTablolari.Tumu.Keys.FirstOrDefault(x => x.Equals(ad, StringComparison.OrdinalIgnoreCase));
            if (k is null) throw new InvalidOperationException("Bilinmeyen tablo: " + ad);
            tip = PysTablolari.Tumu[k];
        }
        var satirlar = await db.PysSorgu(tip).ToListAsync(ct);
        var idProp = tip.GetProperty("ID");
        return new Tablo
        {
            Satirlar = satirlar,
            Oku = (s, k) => Prop(tip, k).GetValue(s),
            Yaz = (s, k, v) => YazProp(s, k, v),
            Yeni = () => Activator.CreateInstance(tip)!,
            Ekle = y => { db.PysEkle(y); satirlar.Add(y); },
            Sil = y => { db.PysSil(y); satirlar.Remove(y); },
            KimlikAta = s =>
            {
                if (idProp is null) return null;
                var mevcut = satirlar.Select(x => idProp.GetValue(x) as int?).Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(0).Max();
                var yeni = mevcut + 1;
                idProp.SetValue(s, yeni);
                return yeni;
            }
        };
    }

    private static PropertyInfo Prop(Type t, string ad) =>
        t.GetProperty(ad, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
        ?? throw new InvalidOperationException($"{t.Name} tablosunda {ad} alanı yok.");

    private static void YazProp(object s, string kolon, object? v)
    {
        var p = Prop(s.GetType(), kolon);
        var hedef = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
        object? deger;
        if (v is null) deger = hedef == typeof(bool) && Nullable.GetUnderlyingType(p.PropertyType) is null ? false : null;
        else if (hedef == typeof(DateOnly)) deger = DateOnly.FromDateTime(Convert.ToDateTime(v, Inv));
        else if (hedef == typeof(Guid)) deger = Guid.Parse(Convert.ToString(v, Inv)!);
        else if (hedef == typeof(string)) deger = Convert.ToString(v, Inv);
        else if (hedef == typeof(bool) && v is not bool) deger = Convert.ToDecimal(v, Inv) != 0;
        else deger = Convert.ChangeType(v, hedef, Inv);
        p.SetValue(s, deger);
    }
}
