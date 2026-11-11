using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Domain;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;
using PayControl.Infrastructure.Import;
using Xunit.Abstractions;

namespace PayControl.Tests.Modules;

public class PuantajVeAccessTestleri : TestOrtami
{
    private readonly ITestOutputHelper _cikti;
    public PuantajVeAccessTestleri(ITestOutputHelper cikti) => _cikti = cikti;

    private static Dictionary<int, string> Ay(int yil, int ay, Func<DateOnly, string?> kod) =>
        Enumerable.Range(1, DateTime.DaysInMonth(yil, ay)).Select(g => (g, k: kod(new DateOnly(yil, ay, g)))).Where(x => x.k != null).ToDictionary(x => x.g, x => x.k!);

    [Fact]
    public void Vba_kurallari_yarim_gun_ucretsiz_izin_haftasi_ve_rapor_cezasi()
    {
        // Ağustos 2026: 5 pazar. 27 Ç + 2 HT + 1 Yi + 1 YG (Access'teki gerçek bir satır: Çalışılan 27,5, Kullanılan 2,5, Toplam 30)
        var g = new Dictionary<int, string>();
        for (var i = 1; i <= 27; i++) g[i] = "Ç";
        g[28] = "HT"; g[29] = "HT"; g[30] = "Yi"; g[31] = "YG";
        var o = PuantajHesaplayici.Hesapla(2026, 8, g);
        Assert.Equal((27.5m, 2.5m, 1m, 30m, 5m), (o.Calisilan, o.KullanilanHaftalikIzin, o.YillikIzin, o.AylikToplam, o.HaftasonuHakki));
        Assert.Equal(2.5m, o.DevredenIzin);

        // Ücretsiz izin: haftada 2+ Üİ varsa o haftanın pazar hakkı yanar (Ağustos 2026, Üİ 5–11 → 2 hafta yanar: 5−2=3)
        var u = Ay(2026, 8, d => d.Day is >= 5 and <= 11 ? "Üİ" : d.DayOfWeek == DayOfWeek.Sunday ? "HT" : "Ç");
        Assert.Equal(3m, PuantajHesaplayici.Hesapla(2026, 8, u).HaftasonuHakki);
        // Tek Üİ günü olan hafta yanmaz (Eylül 2026: Üİ 1–14 → 14 Pazartesi tek → 4−2=2)
        var u2 = Ay(2026, 9, d => d.Day <= 14 ? "Üİ" : "Ç");
        Assert.Equal(2m, PuantajHesaplayici.Hesapla(2026, 9, u2).HaftasonuHakki);

        // Rapor: her tam 7 gün 1 hak, artan 6+ gün 1 hak daha; hak kadar düşülür
        Assert.Equal((0, 1, 1, 2, 2), (PuantajHesaplayici.RaporCezasi(5), PuantajHesaplayici.RaporCezasi(6), PuantajHesaplayici.RaporCezasi(7), PuantajHesaplayici.RaporCezasi(13), PuantajHesaplayici.RaporCezasi(14)));
        Assert.Equal(0m, PuantajHesaplayici.Hesapla(2026, 7, Ay(2026, 7, _ => "R")).HaftasonuHakki);
        // Küçük harf ve Türkçe harf farkı: "ç", "yi" de sayılır; G çalışılana dahil; İİ ve YG toplamda yok
        var k = PuantajHesaplayici.Hesapla(2026, 9, new Dictionary<int, string> { [1] = "ç", [2] = "yi", [3] = "G", [4] = "İİ", [5] = "Gİ" });
        Assert.Equal((2m, 1m, 1m, 1m, 1m, 4m), (k.Calisilan, k.YillikIzin, k.Gorev, k.IdariIzin, k.GorevIzni, k.AylikToplam));
        Assert.Equal("Yi", PuantajHesaplayici.Standart("yi"));
        Assert.Null(PuantajHesaplayici.Standart("XX"));
    }

    private string SentetikAccessKlasoru()
    {
        var k = Path.Combine(Klasor, "access");
        Directory.CreateDirectory(k);
        File.WriteAllText(Path.Combine(k, "Tesisler.csv"), "TesisID,TesisAdi,Aktif,Aciklama\n1,\"Giresun (Keşap) Limanı\",1,\n2,\"Arsin Limanı\",1,\n");
        File.WriteAllText(Path.Combine(k, "Personel.csv"),
            "TCKN,AdSoyad,Tesis,Unvan,Cinsiyet,Durum,IseBaslama,IstenCikis,DogumTarihi,MedeniHal,PersonelID,Sicil,IBAN,Telefon\n" +
            $"\"{Tc1}\",\"EYÜP   YETİM\",\"Giresun (Keşap) Limanı\",\"Dalgıç (Balıkadam)\",\"Erkek\",\"Aktif\",2025-01-01 00:00:00,,1990-05-04 00:00:00,\"Evli\",7,\"S-7\",\"{Iban(700)}\",\"0532 000 00 00\"\n" +
            $"\"{Tc2}\",\"AYŞE KAYA\",\"Arsin Limanı\",\"Aşçı\",\"Kadın\",\"Pasif\",2024-03-01 00:00:00,2026-08-31 00:00:00,,,8,,,\n");
        var satirlar = new List<string> { "Yil,Ay,TCKN,Gun,Kod" };
        for (var g = 1; g <= 30; g++)
            satirlar.Add($"2026,9,\"{Tc1}\",{g},\"{(new DateOnly(2026, 9, g).DayOfWeek == DayOfWeek.Sunday ? "HT" : g == 10 ? "yi" : "Ç")}\"");
        satirlar.Add($"2026,9,\"{Tc1}\",31,\"Ç\""); // geçersiz gün
        File.WriteAllText(Path.Combine(k, "PuantajGunluk_Giresun__Kesap__Limani.csv"), string.Join("\n", satirlar) + "\n");
        File.WriteAllText(Path.Combine(k, "PuantajNot.csv"), $"Tesis,TCKN,Yil,Ay,Gun,Kod,NotMetni,KayitTarihi,AdSoyad\n\"Giresun (Keşap) Limanı\",\"{Tc1}\",2026,9,10,\"Yi\",\"Düğün izni, \"\"belgeli\"\"\",2026-09-10 10:00:00,\"EYÜP YETİM\"\n");
        File.WriteAllText(Path.Combine(k, "AylikOzet.csv"), "Tesis,Yil,Ay,TCKN,AdSoyad,HaftasonuHakki,KullanilanHaftalikIzin,Calisilan,YillikIzin,Raporlu,ResmiTatil,UcretsizIzin,HT,AylikToplam,IdariIzin,DevredenIzin,Gorev,GorevIzni\n" +
            $"\"Giresun (Keşap) Limanı\",2026,9,\"{Tc1}\",\"EYÜP YETİM\",4,4,25,1,0,0,0,4,30,0,0,0,0\n");
        File.WriteAllText(Path.Combine(k, "PersonelNakil.csv"), $"NakilID,TCKN,AdSoyad,EskiTesis,YeniTesis,NakilTarihi,Aciklama,KayitTarihi\n1,\"{Tc1}\",\"EYÜP YETİM\",\"Arsin Limanı\",\"Giresun (Keşap) Limanı\",2026-06-01 00:00:00,\"İhtiyaç\",2026-06-01 09:00:00\n");
        File.WriteAllText(Path.Combine(k, "PersonelIzinAyar.csv"), $"TCKN,RTDahil,HaftaTatili\n\"{Tc1}\",1,\"6\"\n");
        File.WriteAllText(Path.Combine(k, "Zimmet.csv"), $"ZimmetID,IslemNo,PersonelID,TCKN,MalzemeID,Miktar,TeslimTarihi,Durum,Iptal,Bedel\n1,10,7,\"{Tc1}\",3,2,2026-09-01 00:00:00,\"Teslim\",0,150.5\n");
        File.WriteAllText(Path.Combine(k, "Bilinmeyen.csv"), "A,B\n1,2\n");
        return k;
    }

    private static readonly string Tc1 = "1" + new Random(7).NextInt64(1000000000, 9999999999);
    private static readonly string Tc2 = "2" + new Random(8).NextInt64(1000000000, 9999999999);

    [Fact]
    public async Task Access_verisi_aktarilir_tekrar_aktarimda_cogalmaz_puantaj_tablosu_ve_ozet_dogru()
    {
        var k = SentetikAccessKlasoru();
        var srv = S<IAccessAktarimServisi>();
        await Assert.ThrowsAsync<YetkiHatasiException>(() => srv.IceAktarAsync(k, Muhasebe));
        var s = await srv.IceAktarAsync(k, Yonetici);
        Assert.Equal((2, 2, 30, 1, 1, 1, 1, 1), (s.Tesis, s.PersonelYeni, s.Puantaj, s.PuantajNotu, s.AylikOzet, s.Nakil, s.IzinAyari, s.CikisYapilan));
        Assert.Equal(1, s.DigerTablolar["Zimmet"]);
        Assert.Contains("Bilinmeyen", s.AktarilmayanTablolar);

        var p = await Db.Personeller.AsNoTracking().Include(x => x.CalismaDonemleri).SingleAsync(x => x.TcKimlikNo == Tc1);
        Assert.Equal(("EYÜP YETİM", "Giresun (Keşap) Limanı", "Erkek", "Evli", "S-7", new DateOnly(1990, 5, 4)), (p.AdSoyad, p.Isyeri, p.Cinsiyet, p.MedeniHal, p.SicilNo, p.DogumTarihi!.Value));
        var ayse = await Db.Personeller.AsNoTracking().SingleAsync(x => x.TcKimlikNo == Tc2);
        Assert.Equal((CalismaDurumu.IstenAyrildi, new DateOnly(2026, 8, 31)), (ayse.CalismaDurumu, ayse.SonCikisTarihi!.Value));

        var s2 = await srv.IceAktarAsync(k, Yonetici);
        Assert.Equal((0, 2), (s2.PersonelYeni, s2.PersonelGuncellenen));
        Assert.Equal(30, await Db.PuantajGunleri.CountAsync());
        Assert.Equal(1, await Db.PuantajNotlari.CountAsync());

        var tablo = await S<IPuantajServisi>().TabloAsync("Giresun (Keşap) Limanı", 2026, 9);
        Assert.Equal(30, tablo.Gunler.Count);
        var satir = tablo.Satirlar.Single(x => x.TcKimlikNo == Tc1);
        Assert.Equal("Yi", satir.Kodlar[10]);
        Assert.Equal("Düğün izni, \"belgeli\"", satir.Notlar[10]);
        Assert.Equal((25m, 1m, 4m, 4m, 30m), (satir.Ozet.Calisilan, satir.Ozet.YillikIzin, satir.Ozet.HaftasonuHakki, satir.Ozet.HT, satir.Ozet.AylikToplam));
        var aktarilan = await Db.AylikOzetKayitlari.AsNoTracking().SingleAsync();
        Assert.Equal((aktarilan.Calisilan, aktarilan.HaftasonuHakki, aktarilan.AylikToplam), (satir.Ozet.Calisilan, satir.Ozet.HaftasonuHakki, satir.Ozet.AylikToplam));
    }

    [Fact]
    public async Task Eski_sistem_tablolari_personel_adiyla_gorunur_duzenlenir_ve_silinir()
    {
        await S<IAccessAktarimServisi>().IceAktarAsync(SentetikAccessKlasoru(), Yonetici);
        var srv = S<IEskiVeriServisi>();
        Assert.Equal(1, (await srv.SatirSayilariAsync())["Zimmet"]);
        var dt = await srv.TabloAsync("Zimmet");
        var r = dt.Rows[0];
        Assert.Equal("EYÜP YETİM", r["Personel"]);
        Assert.Equal(150.5m, r["Bedel"]);
        Assert.Equal(new DateTime(2026, 9, 1), r["TeslimTarihi"]);
        Assert.Equal("Teslim tarihi", srv.SutunBasligi("TeslimTarihi"));

        r["Durum"] = "İade";
        var yeni = dt.NewRow();
        yeni["TCKN"] = Tc1; yeni["Miktar"] = 1; yeni["Durum"] = "Teslim"; yeni["Iptal"] = false;
        dt.Rows.Add(yeni);
        await Assert.ThrowsAsync<YetkiHatasiException>(() => srv.KaydetAsync("Zimmet", dt, Izleyici));
        Assert.Equal(2, await srv.KaydetAsync("Zimmet", dt, Muhasebe));
        var dt2 = await srv.TabloAsync("Zimmet");
        Assert.Equal(2, dt2.Rows.Count);
        Assert.Contains(dt2.Rows.Cast<System.Data.DataRow>(), x => (string)x["Durum"] == "İade");
        dt2.Rows[0].Delete();
        Assert.Equal(1, await srv.KaydetAsync("Zimmet", dt2, Muhasebe));
        Assert.Equal(1, (await srv.SatirSayilariAsync())["Zimmet"]);
        // Kullanıcı şifre özetleri gösterilmez
        Assert.DoesNotContain((await srv.TabloAsync("Kullanicilar")).Columns.Cast<System.Data.DataColumn>(), c => c.ColumnName is "SifreOzet" or "Tuz");
    }

    [Fact]
    public async Task Hazir_veri_paketi_zip_olarak_aktarilir()
    {
        var k = SentetikAccessKlasoru();
        var zip = Path.Combine(Klasor, "baslangic-verisi.zip");
        System.IO.Compression.ZipFile.CreateFromDirectory(k, zip);
        var s = await S<IAccessAktarimServisi>().IceAktarAsync(zip, Yonetici);
        Assert.Equal(2, s.PersonelYeni);
        Assert.Equal(30, s.Puantaj);
    }

    [Fact]
    public async Task Puantaj_girisi_gecersiz_kodu_reddeder_nakil_ve_resmi_tatil()
    {
        var p = await PersonelAsync("Puantaj Personeli", 30000, 710, new DateOnly(2025, 1, 1), isyeri: "Arsin Limanı");
        var srv = S<IPuantajServisi>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.KaydetAsync("Arsin Limanı", 2026, 10, [new(p.TcKimlikNo, 1, "XX")], Muhasebe));
        await Assert.ThrowsAsync<YetkiHatasiException>(() => srv.KaydetAsync("Arsin Limanı", 2026, 10, [new(p.TcKimlikNo, 1, "Ç")], Izleyici));
        Assert.Equal(3, await srv.KaydetAsync("Arsin Limanı", 2026, 10, [new(p.TcKimlikNo, 1, "ç"), new(p.TcKimlikNo, 2, "R"), new(p.TcKimlikNo, 29, "RT")], Muhasebe));
        Assert.Equal(1, await srv.KaydetAsync("Arsin Limanı", 2026, 10, [new(p.TcKimlikNo, 2, null), new(p.TcKimlikNo, 1, "Ç")], Muhasebe)); // 1 değişmedi, 2 silindi
        var t = await srv.TabloAsync("Arsin Limanı", 2026, 10);
        var s = t.Satirlar.Single();
        Assert.Equal(new Dictionary<int, string> { [1] = "Ç", [29] = "RT" }, s.Kodlar);
        Assert.Equal("Cumhuriyet Bayramı", t.Gunler[28].ResmiTatil);
        Assert.True(t.Gunler.Single(g => g.Gun == 4).HaftaSonu); // 4 Ekim 2026 pazar

        Assert.Equal(22, (await srv.ResmiTatillerAsync()).Count); // Excel Ayarlar listesi
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.ResmiTatilKaydetAsync(new DateOnly(2026, 12, 31), "Deneme", 0.7m, Muhasebe));

        var n = await srv.NakilYapAsync(p.Id, "Rize Limanı", new DateOnly(2026, 11, 1), "Sezon", Muhasebe);
        Assert.Equal(("Arsin Limanı", "Rize Limanı"), (n.EskiTesis, n.YeniTesis));
        Assert.Equal("Rize Limanı", (await Db.Personeller.AsNoTracking().SingleAsync(x => x.Id == p.Id)).Isyeri);
        Assert.Single(await srv.NakillerAsync("Rize Limanı"));
        // Eski aya ait puantaj eski tesiste görünmeye devam eder
        Assert.Single((await srv.TabloAsync("Arsin Limanı", 2026, 10)).Satirlar);
    }

    /// <summary>Gerçek Access verisiyle karşılaştırma (yalnız yerelde: PYS_ACCESS_CSV klasörü verilirse çalışır, CI'da atlanır).</summary>
    [Fact]
    public async Task Gercek_Access_verisi_aylik_ozetleri_birebir_hesaplanir()
    {
        var klasor = Environment.GetEnvironmentVariable("PYS_ACCESS_CSV");
        if (string.IsNullOrWhiteSpace(klasor) || !Directory.Exists(klasor)) return;
        var s = await S<IAccessAktarimServisi>().IceAktarAsync(klasor, Yonetici);
        _cikti.WriteLine(s.ToString());
        _cikti.WriteLine("Aktarılmayan tablolar: " + string.Join(", ", s.AktarilmayanTablolar));
        foreach (var (t, n) in s.DigerTablolar.OrderByDescending(x => x.Value))
        {
            var csvSatir = File.ReadAllLines(Path.Combine(klasor, t + ".csv")).Length - 1;
            _cikti.WriteLine($"  {t}: {n} (CSV satırı ~{csvSatir})");
        }
        Assert.Empty(s.AktarilmayanTablolar);
        Assert.Equal(131, s.DigerTablolar["PersonelBelge"]);
        Assert.Equal(147, s.DigerTablolar["HTBakiyeSabitleme"]);
        foreach (var u in s.Uyarilar.Take(10)) _cikti.WriteLine("Uyarı: " + u);

        var ozetler = await Db.AylikOzetKayitlari.AsNoTracking().ToListAsync();
        var puantaj = (await Db.PuantajGunleri.AsNoTracking().ToListAsync()).GroupBy(p => (p.TesisAdi, p.Yil, p.Ay, p.TcKimlikNo))
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Gun, x => x.Kod));
        var alanlar = new (string Ad, Func<AylikPuantajOzeti, decimal> H, Func<PayControl.Domain.Entities.AylikOzetKaydi, decimal> A)[]
        {
            ("HaftasonuHakki", h => h.HaftasonuHakki, a => a.HaftasonuHakki), ("KullanilanHaftalikIzin", h => h.KullanilanHaftalikIzin, a => a.KullanilanHaftalikIzin),
            ("Calisilan", h => h.Calisilan, a => a.Calisilan), ("YillikIzin", h => h.YillikIzin, a => a.YillikIzin), ("Raporlu", h => h.Raporlu, a => a.Raporlu),
            ("ResmiTatil", h => h.ResmiTatil, a => a.ResmiTatil), ("UcretsizIzin", h => h.UcretsizIzin, a => a.UcretsizIzin), ("HT", h => h.HT, a => a.HT),
            ("AylikToplam", h => h.AylikToplam, a => a.AylikToplam), ("IdariIzin", h => h.IdariIzin, a => a.IdariIzin), ("DevredenIzin", h => h.DevredenIzin, a => a.DevredenIzin),
            ("Gorev", h => h.Gorev, a => a.Gorev), ("GorevIzni", h => h.GorevIzni, a => a.GorevIzni)
        };
        var karsilastirilan = 0;
        var farklar = alanlar.ToDictionary(a => a.Ad, _ => 0);
        foreach (var o in ozetler)
        {
            if (!puantaj.TryGetValue((o.TesisAdi, o.Yil, o.Ay, o.TcKimlikNo), out var kodlar)) continue;
            karsilastirilan++;
            var h = PuantajHesaplayici.Hesapla(o.Yil, o.Ay, kodlar);
            foreach (var a in alanlar)
                if (a.H(h) != a.A(o))
                {
                    farklar[a.Ad]++;
                    _cikti.WriteLine($"FARK {a.Ad} {o.TesisAdi} {o.Yil}-{o.Ay:D2}: Access {a.A(o)} / program {a.H(h)}");
                }
        }
        _cikti.WriteLine($"Karşılaştırılan kişi-ay: {karsilastirilan}; farklar: {string.Join(", ", farklar.Select(f => $"{f.Key}={f.Value}"))}");
        Assert.True(karsilastirilan >= 400);
        Assert.All(farklar.Where(f => f.Key != "DevredenIzin"), f => Assert.Equal(0, f.Value));
        Assert.True(farklar["DevredenIzin"] <= 1);
    }
}
