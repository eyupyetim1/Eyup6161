using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Infrastructure.Import;

/// <summary>
/// Excel/VBA "Personel Yönetim Sistemi" Access veritabanını (Database.accdb) içe aktarır.
/// Aktarılan: Tesisler, Personel (tüm alanlar), PuantajGunluk_&lt;Tesis&gt; (tümü), PuantajNot, AylikOzet, PersonelNakil, PersonelIzinAyar.
/// </summary>
public sealed class AccessAktarimServisi : IAccessAktarimServisi
{
    private readonly IPayControlDbContext _db;
    private readonly IYetkiServisi _yetki;

    public AccessAktarimServisi(IPayControlDbContext db, IYetkiServisi yetki)
    {
        _db = db;
        _yetki = yetki;
    }

    // ------------------------------------------------------------------ Access → CSV (Windows)

    public const string DisaAktarmaBetigi = """
        param([string]$Db, [string]$Out)
        $ErrorActionPreference = 'Stop'
        $cn = $null; $hata = ''
        foreach ($p in @('Microsoft.ACE.OLEDB.16.0', 'Microsoft.ACE.OLEDB.12.0', 'Microsoft.Jet.OLEDB.4.0')) {
          try { $cn = New-Object System.Data.OleDb.OleDbConnection("Provider=$p;Data Source=$Db;Mode=Read;Persist Security Info=False"); $cn.Open(); break }
          catch { $hata += "$p : $($_.Exception.Message)`n"; $cn = $null }
        }
        if ($null -eq $cn) { Write-Error "ACCESS_SURUCU_YOK`n$hata"; exit 3 }
        New-Item -ItemType Directory -Force -Path $Out | Out-Null
        $tablolar = $cn.GetSchema('Tables') | Where-Object { $_.TABLE_TYPE -eq 'TABLE' } | ForEach-Object { $_.TABLE_NAME }
        $inv = [Globalization.CultureInfo]::InvariantCulture
        foreach ($t in $tablolar) {
          $cmd = $cn.CreateCommand(); $cmd.CommandText = "SELECT * FROM [$t]"
          $r = $cmd.ExecuteReader()
          $sb = New-Object System.Text.StringBuilder
          $ad = @(); for ($i = 0; $i -lt $r.FieldCount; $i++) { $ad += $r.GetName($i) }
          [void]$sb.AppendLine(($ad -join ','))
          while ($r.Read()) {
            $v = @()
            for ($i = 0; $i -lt $r.FieldCount; $i++) {
              if ($r.IsDBNull($i)) { $v += ''; continue }
              $x = $r.GetValue($i)
              if ($x -is [DateTime]) { $v += $x.ToString('yyyy-MM-dd HH:mm:ss', $inv) }
              elseif ($x -is [bool]) { $v += $(if ($x) { '1' } else { '0' }) }
              elseif ($x -is [string]) { $v += '"' + $x.Replace('"', '""') + '"' }
              elseif ($x -is [byte[]]) { $v += '' }
              else { $v += [Convert]::ToString($x, $inv) }
            }
            [void]$sb.AppendLine(($v -join ','))
          }
          $r.Close()
          [IO.File]::WriteAllText((Join-Path $Out "$t.csv"), $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
        }
        $cn.Close()
        Write-Output "TAMAM $($tablolar.Count)"
        """;

    public async Task<string> CsvyeCikarAsync(string accessDosyasi, CancellationToken ct = default)
    {
        if (!File.Exists(accessDosyasi)) throw new InvalidOperationException("Access dosyası bulunamadı.");
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Access dosyası yalnızca Windows'ta okunabilir.");
        var klasor = Path.Combine(Path.GetTempPath(), $"pys-access-{Guid.NewGuid():N}");
        var betik = Path.Combine(Path.GetTempPath(), $"pys-access-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(betik, DisaAktarmaBetigi, new UTF8Encoding(true), ct);
        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        // Önce 64 bit, olmazsa 32 bit PowerShell (Office/Access sürücüsü 32 bit kurulu olabilir)
        var adaylar = new[] { Path.Combine(win, "System32", "WindowsPowerShell", "v1.0", "powershell.exe"), Path.Combine(win, "SysWOW64", "WindowsPowerShell", "v1.0", "powershell.exe") };
        var hatalar = new StringBuilder();
        try
        {
            foreach (var ps in adaylar.Where(File.Exists))
            {
                var psi = new ProcessStartInfo(ps) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                foreach (var a in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", betik, "-Db", accessDosyasi, "-Out", klasor }) psi.ArgumentList.Add(a);
                using var p = Process.Start(psi)!;
                var cikti = await p.StandardOutput.ReadToEndAsync(ct);
                var hata = await p.StandardError.ReadToEndAsync(ct);
                await p.WaitForExitAsync(ct);
                if (p.ExitCode == 0 && cikti.Contains("TAMAM")) return klasor;
                hatalar.AppendLine($"{(ps.Contains("SysWOW64") ? "32" : "64")} bit: {hata.Trim()}");
            }
        }
        finally { try { File.Delete(betik); } catch { /* yoksay */ } }
        if (hatalar.ToString().Contains("ACCESS_SURUCU_YOK"))
            throw new InvalidOperationException("Bu bilgisayarda Access veritabanı sürücüsü bulunamadı. Microsoft 'Access Database Engine 2016 Redistributable' kurulmalı " +
                                                "(Office/Access kuruluysa genelde vardır). Ayrıntı:\n" + hatalar);
        throw new InvalidOperationException("Access dosyası okunamadı:\n" + hatalar);
    }

    // ------------------------------------------------------------------ CSV → program

    /// <summary>VBA'daki tablo adı dönüşümü: Türkçe harfler sadeleşir, harf/rakam dışı '_' olur ("Giresun (Keşap) Limanı" → "Giresun__Kesap__Limani").</summary>
    public static string TabloAdi(string tesis)
    {
        var s = tesis.Replace('ı', 'i').Replace('İ', 'I').Replace('ş', 's').Replace('Ş', 'S').Replace('ğ', 'g').Replace('Ğ', 'G')
            .Replace('ü', 'u').Replace('Ü', 'U').Replace('ö', 'o').Replace('Ö', 'O').Replace('ç', 'c').Replace('Ç', 'C');
        return new string(s.Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
    }

    private static DateOnly? Tarih(string? s) =>
        DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) && d.Year > 1900 ? DateOnly.FromDateTime(d) : null;
    private static decimal Sayi(string? s) => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m;
    private static int Tam(string? s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : (int)Sayi(s);
    private static string? Bos(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public async Task<AccessAktarimSonucu> IceAktarAsync(string csvKlasoru, string kullanici, CancellationToken ct = default)
    {
        await _yetki.DogrulaAsync(kullanici, KullaniciRolu.Yonetici, "Access verisi aktarımı", ct);
        if (File.Exists(csvKlasoru) && csvKlasoru.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            // Hazır veri paketi (CSV'lerin zip'i)
            var acilan = Path.Combine(Path.GetTempPath(), $"pys-paket-{Guid.NewGuid():N}");
            System.IO.Compression.ZipFile.ExtractToDirectory(csvKlasoru, acilan);
            var ic = Directory.GetFiles(acilan, "Personel.csv", SearchOption.AllDirectories).FirstOrDefault();
            try { return await IceAktarAsync(ic is null ? acilan : Path.GetDirectoryName(ic)!, kullanici, ct); }
            finally { try { Directory.Delete(acilan, true); } catch { /* yoksay */ } }
        }
        if (!Directory.Exists(csvKlasoru)) throw new InvalidOperationException("Aktarım klasörü bulunamadı.");
        string Yol(string t) => Path.Combine(csvKlasoru, t + ".csv");
        if (!File.Exists(Yol("Personel"))) throw new InvalidOperationException("Klasörde Personel tablosu yok; doğru Access dosyası mı?");
        var sonuc = new AccessAktarimSonucu();
        var bilinen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Tesisler", "Personel", "PuantajNot", "AylikOzet", "PersonelNakil", "PersonelIzinAyar" };

        // --- Tesisler
        var tesisAdlari = new List<string>();
        if (File.Exists(Yol("Tesisler")))
        {
            var mevcut = await _db.Tesisler.ToListAsync(ct);
            foreach (var r in CsvOkuyucu.Oku(Yol("Tesisler")))
            {
                var ad = IsimKurallari.Duzelt(r.GetValueOrDefault("TesisAdi")) ?? "";
                if (ad.Length == 0) continue;
                tesisAdlari.Add(ad);
                var t = mevcut.FirstOrDefault(x => string.Equals(x.Ad, ad, StringComparison.CurrentCultureIgnoreCase));
                if (t is null)
                {
                    var kod = $"T{Tam(r.GetValueOrDefault("TesisID")):000}";
                    while (mevcut.Any(x => x.Kod == kod)) kod += "A";
                    t = new Tesis { Kod = kod, Ad = ad };
                    _db.Tesisler.Add(t);
                    mevcut.Add(t);
                    sonuc.Tesis++;
                }
                t.Aktif = r.GetValueOrDefault("Aktif") is not ("0" or "False");
                if (Bos(r.GetValueOrDefault("Aciklama")) is { } ac) t.Adres ??= ac;
            }
        }

        // --- Personel
        var personeller = await _db.Personeller.Include(p => p.CalismaDonemleri).ToListAsync(ct);
        var tcHarita = personeller.ToDictionary(p => p.TcKimlikNo);
        foreach (var r in CsvOkuyucu.Oku(Yol("Personel")))
        {
            var tc = new string((r.GetValueOrDefault("TCKN") ?? "").Where(char.IsDigit).ToArray());
            if (tc.Length != 11) { sonuc.Uyarilar.Add($"Geçersiz TCKN atlandı: '{r.GetValueOrDefault("TCKN")}' ({r.GetValueOrDefault("AdSoyad")})"); continue; }
            var ad = IsimKurallari.Duzelt(r.GetValueOrDefault("AdSoyad")) ?? tc;
            var tesis = IsimKurallari.Duzelt(r.GetValueOrDefault("Tesis"));
            var giris = Tarih(r.GetValueOrDefault("IseBaslama"));
            var cikis = Tarih(r.GetValueOrDefault("IstenCikis"));
            var pasif = string.Equals(r.GetValueOrDefault("Durum"), "Pasif", StringComparison.OrdinalIgnoreCase);
            if (!tcHarita.TryGetValue(tc, out var p))
            {
                p = new Personel { TcKimlikNo = tc, AdSoyad = ad, Isyeri = tesis, Gorevi = Bos(r.GetValueOrDefault("Unvan")), IlkIseGirisTarihi = giris };
                if (giris is { } g)
                {
                    var donem = PersonelDonemKurallari.YeniDonemAc(p, g, tesis, p.Gorevi);
                    if (cikis is { } c && c >= g) { PersonelDonemKurallari.DonemiKapat(p, donem, c); sonuc.CikisYapilan++; }
                }
                if (pasif && p.CalismaDurumu == CalismaDurumu.Aktif) { p.CalismaDurumu = CalismaDurumu.IstenAyrildi; p.SonCikisTarihi ??= cikis; }
                _db.Personeller.Add(p);
                tcHarita[tc] = p;
                sonuc.PersonelYeni++;
            }
            else sonuc.PersonelGuncellenen++;
            p.AdSoyad = ad;
            p.Isyeri = tesis ?? p.Isyeri;
            p.Gorevi = Bos(r.GetValueOrDefault("Unvan")) ?? p.Gorevi;
            p.Cinsiyet = Bos(r.GetValueOrDefault("Cinsiyet")) ?? p.Cinsiyet;
            p.DogumTarihi = Tarih(r.GetValueOrDefault("DogumTarihi")) ?? p.DogumTarihi;
            p.MedeniHal = Bos(r.GetValueOrDefault("MedeniHal")) ?? p.MedeniHal;
            p.SicilNo = Bos(r.GetValueOrDefault("Sicil")) ?? p.SicilNo;
            p.Telefon = Bos(r.GetValueOrDefault("Telefon")) ?? p.Telefon;
            if (Bos(r.GetValueOrDefault("IBAN")) is { } iban) p.Iban = new string(iban.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            if (int.TryParse(r.GetValueOrDefault("PersonelID"), out var pid)) p.EskiSistemId = pid;
            p.GuncellemeZamani = DateTime.UtcNow;
            if (tesis is not null && !tesisAdlari.Contains(tesis, StringComparer.CurrentCultureIgnoreCase)) tesisAdlari.Add(tesis);
        }

        // --- Puantaj (tesis başına tablo)
        var mevcutPuantaj = (await _db.PuantajGunleri.ToListAsync(ct)).ToDictionary(x => (x.TesisAdi, x.Yil, x.Ay, x.TcKimlikNo, x.Gun));
        var islenenDosyalar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tesis in tesisAdlari.Distinct(StringComparer.CurrentCultureIgnoreCase))
        {
            var dosya = "PuantajGunluk_" + TabloAdi(tesis);
            if (!File.Exists(Yol(dosya))) continue;
            islenenDosyalar.Add(dosya);
            bilinen.Add(dosya);
            foreach (var r in CsvOkuyucu.Oku(Yol(dosya)))
            {
                var kod = PuantajHesaplayici.Standart(r.GetValueOrDefault("Kod"));
                var tc = new string((r.GetValueOrDefault("TCKN") ?? "").Where(char.IsDigit).ToArray());
                var (y, a, g) = (Tam(r.GetValueOrDefault("Yil")), Tam(r.GetValueOrDefault("Ay")), Tam(r.GetValueOrDefault("Gun")));
                if (kod is null || tc.Length == 0 || y < 2000 || a is < 1 or > 12 || g < 1 || g > DateTime.DaysInMonth(y, a))
                {
                    if (!string.IsNullOrWhiteSpace(r.GetValueOrDefault("Kod")) && kod is null)
                        sonuc.Uyarilar.Add($"{tesis} {y}-{a:D2}-{g:D2} {tc}: tanınmayan kod '{r.GetValueOrDefault("Kod")}' atlandı");
                    continue;
                }
                if (mevcutPuantaj.TryGetValue((tesis, y, a, tc, g), out var k)) k.Kod = kod;
                else
                {
                    k = new PuantajGunu { TesisAdi = tesis, Yil = y, Ay = a, Gun = g, TcKimlikNo = tc, Kod = kod, Kullanici = "Access aktarımı",
                        PersonelId = tcHarita.TryGetValue(tc, out var pp) ? pp.Id : null };
                    _db.PuantajGunleri.Add(k);
                    mevcutPuantaj[(tesis, y, a, tc, g)] = k;
                }
                sonuc.Puantaj++;
            }
        }
        foreach (var f in Directory.GetFiles(csvKlasoru, "PuantajGunluk_*.csv").Select(Path.GetFileNameWithoutExtension))
            if (!islenenDosyalar.Contains(f!) && f != "PuantajGunluk__Tumu_" && new FileInfo(Yol(f!)).Length > 40)
                sonuc.Uyarilar.Add($"{f}: tesis listesinde karşılığı olmayan puantaj tablosu aktarılmadı.");
        bilinen.Add("PuantajGunluk__Tumu_");

        // --- Puantaj notları
        if (File.Exists(Yol("PuantajNot")))
        {
            var mevcutNot = await _db.PuantajNotlari.ToListAsync(ct);
            foreach (var r in CsvOkuyucu.Oku(Yol("PuantajNot")))
            {
                var not = Bos(r.GetValueOrDefault("NotMetni"));
                if (not is null) continue;
                var tesis = IsimKurallari.Duzelt(r.GetValueOrDefault("Tesis")) ?? "";
                var tc = new string((r.GetValueOrDefault("TCKN") ?? "").Where(char.IsDigit).ToArray());
                var (y, a, g) = (Tam(r.GetValueOrDefault("Yil")), Tam(r.GetValueOrDefault("Ay")), Tam(r.GetValueOrDefault("Gun")));
                if (mevcutNot.Any(n => n.TesisAdi == tesis && n.TcKimlikNo == tc && n.Yil == y && n.Ay == a && n.Gun == g && n.NotMetni == not)) continue;
                var n = new PuantajNotu { TesisAdi = tesis, TcKimlikNo = tc, Yil = y, Ay = a, Gun = g, Kod = Bos(r.GetValueOrDefault("Kod")), NotMetni = not,
                    AdSoyad = IsimKurallari.Duzelt(r.GetValueOrDefault("AdSoyad")) };
                _db.PuantajNotlari.Add(n);
                mevcutNot.Add(n);
                sonuc.PuantajNotu++;
            }
        }

        // --- Aylık özet (geçmiş)
        if (File.Exists(Yol("AylikOzet")))
        {
            var mevcutOzet = (await _db.AylikOzetKayitlari.ToListAsync(ct)).ToDictionary(x => (x.TesisAdi, x.Yil, x.Ay, x.TcKimlikNo));
            foreach (var r in CsvOkuyucu.Oku(Yol("AylikOzet")))
            {
                var anahtar = (IsimKurallari.Duzelt(r.GetValueOrDefault("Tesis")) ?? "", Tam(r.GetValueOrDefault("Yil")), Tam(r.GetValueOrDefault("Ay")),
                    new string((r.GetValueOrDefault("TCKN") ?? "").Where(char.IsDigit).ToArray()));
                if (!mevcutOzet.TryGetValue(anahtar, out var o))
                {
                    o = new AylikOzetKaydi { TesisAdi = anahtar.Item1, Yil = anahtar.Item2, Ay = anahtar.Item3, TcKimlikNo = anahtar.Item4 };
                    _db.AylikOzetKayitlari.Add(o);
                    mevcutOzet[anahtar] = o;
                }
                o.AdSoyad = IsimKurallari.Duzelt(r.GetValueOrDefault("AdSoyad"));
                o.HaftasonuHakki = Sayi(r.GetValueOrDefault("HaftasonuHakki")); o.KullanilanHaftalikIzin = Sayi(r.GetValueOrDefault("KullanilanHaftalikIzin"));
                o.Calisilan = Sayi(r.GetValueOrDefault("Calisilan")); o.YillikIzin = Sayi(r.GetValueOrDefault("YillikIzin")); o.Raporlu = Sayi(r.GetValueOrDefault("Raporlu"));
                o.ResmiTatil = Sayi(r.GetValueOrDefault("ResmiTatil")); o.UcretsizIzin = Sayi(r.GetValueOrDefault("UcretsizIzin")); o.HT = Sayi(r.GetValueOrDefault("HT"));
                o.AylikToplam = Sayi(r.GetValueOrDefault("AylikToplam")); o.IdariIzin = Sayi(r.GetValueOrDefault("IdariIzin")); o.DevredenIzin = Sayi(r.GetValueOrDefault("DevredenIzin"));
                o.Gorev = Sayi(r.GetValueOrDefault("Gorev")); o.GorevIzni = Sayi(r.GetValueOrDefault("GorevIzni"));
                sonuc.AylikOzet++;
            }
        }

        // --- Nakiller
        if (File.Exists(Yol("PersonelNakil")))
        {
            var mevcutNakil = await _db.PersonelNakilleri.ToListAsync(ct);
            foreach (var r in CsvOkuyucu.Oku(Yol("PersonelNakil")))
            {
                var tc = new string((r.GetValueOrDefault("TCKN") ?? "").Where(char.IsDigit).ToArray());
                var tarih = Tarih(r.GetValueOrDefault("NakilTarihi"));
                var yeni = IsimKurallari.Duzelt(r.GetValueOrDefault("YeniTesis")) ?? "";
                if (tc.Length == 0 || tarih is null || yeni.Length == 0) continue;
                if (mevcutNakil.Any(n => n.TcKimlikNo == tc && n.NakilTarihi == tarih && n.YeniTesis == yeni)) continue;
                var n = new PersonelNakil
                {
                    TcKimlikNo = tc, PersonelId = tcHarita.TryGetValue(tc, out var pp) ? pp.Id : null, AdSoyad = IsimKurallari.Duzelt(r.GetValueOrDefault("AdSoyad")),
                    EskiTesis = IsimKurallari.Duzelt(r.GetValueOrDefault("EskiTesis")), YeniTesis = yeni, NakilTarihi = tarih.Value,
                    Aciklama = Bos(r.GetValueOrDefault("Aciklama")), Kullanici = "Access aktarımı"
                };
                _db.PersonelNakilleri.Add(n);
                mevcutNakil.Add(n);
                sonuc.Nakil++;
            }
        }

        // --- Kişisel izin ayarları
        if (File.Exists(Yol("PersonelIzinAyar")))
        {
            var mevcutAyar = await _db.PersonelIzinAyarlari.ToDictionaryAsync(a => a.TcKimlikNo, ct);
            foreach (var r in CsvOkuyucu.Oku(Yol("PersonelIzinAyar")))
            {
                var tc = new string((r.GetValueOrDefault("TCKN") ?? "").Where(char.IsDigit).ToArray());
                if (tc.Length == 0) continue;
                if (!mevcutAyar.TryGetValue(tc, out var a)) { a = new PersonelIzinAyari { TcKimlikNo = tc }; _db.PersonelIzinAyarlari.Add(a); mevcutAyar[tc] = a; }
                a.RtDahil = r.GetValueOrDefault("RTDahil") is not ("0" or "False");
                a.HaftaTatili = Bos(r.GetValueOrDefault("HaftaTatili"));
                sonuc.IzinAyari++;
            }
        }

        // --- Diğer tüm tablolar (izin, belge, sözleşme, İSG, zimmet, eğitim, avans, mesai, disiplin, görev, şirket…): Access sütunlarıyla birebir
        foreach (var (tablo, _) in PayControl.Domain.Entities.Pys.PysTablolari.Tumu)
        {
            if (!File.Exists(Yol(tablo))) continue;
            bilinen.Add(tablo);
            var n = await PayControl.Application.Services.EskiVeriServisi.CsvdenAktarAsync(_db, tablo, CsvOkuyucu.Oku(Yol(tablo)), ct);
            sonuc.DigerTablolar[tablo] = n;
        }
        foreach (var t in new[] { "OturumKilit", "SemaSurum", "GirisKayit" }) bilinen.Add(t); // eski programın teknik tabloları

        foreach (var f in Directory.GetFiles(csvKlasoru, "*.csv").Select(Path.GetFileNameWithoutExtension))
            if (!bilinen.Contains(f!) && !f!.StartsWith("PuantajGunluk_") && new FileInfo(Yol(f)).Length > 0 && File.ReadLines(Yol(f)).Skip(1).Any())
                sonuc.AktarilmayanTablolar.Add(f);

        _db.DenetimKayitlari.Add(new DenetimKaydi
        {
            VarlikTuru = "AccessAktarimi", Islem = "AccessAktarimi", KullaniciAdi = kullanici, YeniDeger = sonuc.ToString(),
            DegisiklikNedeni = sonuc.AktarilmayanTablolar.Count == 0 ? "Access verisi aktarıldı" : "Sonraki aşamalarda aktarılacak tablolar: " + string.Join(", ", sonuc.AktarilmayanTablolar)
        });
        await _db.SaveChangesAsync(ct);
        return sonuc;
    }
}
