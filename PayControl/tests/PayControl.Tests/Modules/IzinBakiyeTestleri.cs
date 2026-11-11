using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Application.Services;
using PayControl.Domain;
using PayControl.Domain.Entities;
using PayControl.Domain.Entities.Pys;
using PayControl.Domain.Services;
using PayControl.Infrastructure.Import;
using Xunit.Abstractions;

namespace PayControl.Tests.Modules;

public class IzinBakiyeTestleri : TestOrtami
{
    private readonly ITestOutputHelper _cikti;
    public IzinBakiyeTestleri(ITestOutputHelper cikti) => _cikti = cikti;

    private static readonly DateOnly Bugun = new(2026, 10, 10);
    private static readonly IReadOnlyList<ResmiTatilGirdisi> Tatiller =
        PuantajServisi.VarsayilanTatiller.Select(t => new ResmiTatilGirdisi(t.Tarih, t.Ad, t.Hak)).Where(t => t.Tarih <= Bugun).ToList();

    private static IzinBakiyeSonucu H(IzinBakiyeGirdisi g, HaftaTatiliPolitikasi p = HaftaTatiliPolitikasi.SadecePazar) =>
        IzinBakiyeHesaplayici.Hesapla(g, Tatiller, p, Bugun);

    [Fact]
    public void Hafta_tatili_hakki_kullanim_ve_resmi_tatil_vba_kurallariyla_hesaplanir()
    {
        // 1 Eylül 2026 (salı) işe giriş, 10 Ekim'e kadar 5 pazar (6,13,20,27 Eyl, 4 Eki) → +5
        var kod = new Dictionary<DateOnly, string> { [new(2026, 9, 6)] = "HT", [new(2026, 9, 13)] = "HT", [new(2026, 9, 19)] = "YG" };
        var s = H(new IzinBakiyeGirdisi { TcKimlikNo = "1", AdSoyad = "A", IseGiris = new DateOnly(2026, 9, 1), Kodlar = kod });
        Assert.Equal(5m - 2m - 0.5m, s.HT);
        Assert.Equal(0m, s.RT);      // aralıkta resmi tatil yok
        Assert.Equal(2.5m, s.Toplam);
        Assert.Contains(s.Defter, d => d.Aciklama == "İşe giriş");
        Assert.Equal(2.5m, s.Defter[0].Bakiye);  // en yeni üstte, son bakiye

        // Ağustos girişli: 30 Ağustos Zafer Bayramı pazar → hak verilmez; 15 Temmuz (çarşamba) işe girişten önce
        var s2 = H(new IzinBakiyeGirdisi { TcKimlikNo = "2", AdSoyad = "B", IseGiris = new DateOnly(2026, 7, 15) });
        Assert.Equal(1m, s2.RT);  // yalnızca 15 Temmuz (giriş günü dahil)
        Assert.Contains(s2.Defter, d => d.Aciklama.StartsWith("Resmi tatil pazara denk geldi"));
        // RT dahil değilse toplamda yok
        var s3 = H(new IzinBakiyeGirdisi { TcKimlikNo = "2", AdSoyad = "B", IseGiris = new DateOnly(2026, 7, 15), RtDahil = false });
        Assert.Equal(s3.HT, s3.Toplam);
    }

    [Fact]
    public void Ucretsiz_izin_rapor_kesintisi_yillik_izin_pazari_ve_izin_donusu()
    {
        var giris = new DateOnly(2026, 8, 1);
        var kod = new Dictionary<DateOnly, string>();
        // 3–4 Ağustos 2 Üİ → 9 Ağustos pazar hakkı yanar
        kod[new(2026, 8, 3)] = "UI"; kod[new(2026, 8, 4)] = "UI";
        // Eylülde 13 gün R → 2 gün kesinti
        for (var g = 1; g <= 13; g++) kod[new(2026, 9, g)] = "R";
        // Ekim: 1–3 Yİ (perş–cmt), 4 Ekim pazar boş → izin dönüşü pazarı HT sayılır
        kod[new(2026, 10, 1)] = "YI"; kod[new(2026, 10, 2)] = "YI"; kod[new(2026, 10, 3)] = "YI";
        // 23 Ağustos pazar Yİ → HT'den düşer, yıllık izinden değil
        kod[new(2026, 8, 22)] = "YI"; kod[new(2026, 8, 23)] = "YI";
        var s = H(new IzinBakiyeGirdisi { TcKimlikNo = "3", AdSoyad = "C", IseGiris = giris, Kodlar = kod });
        // Pazarlar: Ağu 2,9,16,23,30 (5, 9'u yanar → 4), Eyl 6,13,20,27 (4 − 2 rapor = 2), Eki 4 (1) = 7
        // Kullanım: 23 Ağustos Yİ pazarı −1, 4 Ekim izin dönüşü −1 → 5
        Assert.Equal(5m, s.HT);
        Assert.Contains(s.Defter, d => d.Aciklama.StartsWith("Hafta tatili hakkı yandı"));
        Assert.Contains(s.Defter, d => d.Aciklama == "Rapor kesintisi (13 gün R)" && d.Etki == -2m);
        Assert.Contains(s.Defter, d => d.Aciklama == "Yıllık izin dönüşü pazar (HT sayıldı)");
        Assert.Equal(4m, s.YIPuantaj); // 22 Ağu + 1–3 Eki (pazar hariç)
        Assert.Equal(0m, s.RT);        // 30 Ağustos pazar → hak yok, 29 Ekim henüz gelmedi
    }

    [Fact]
    public void Kesin_bakiye_onceki_her_seyin_yerine_gecer_ve_kesintiler_sonraki_aylardan_dusulur()
    {
        var g = new IzinBakiyeGirdisi
        {
            TcKimlikNo = "4", AdSoyad = "D", IseGiris = new DateOnly(2020, 1, 1),
            KesinBakiye = new KesinBakiyeGirdisi(new DateOnly(2026, 9, 15), 10m, "sezon"),
            GorevKesintileri = new Dictionary<int, decimal> { [202609] = 1m, [202610] = 2m },
            Hareketler = [new IzinHareketGirdisi(1, new DateOnly(2026, 9, 1), "HTDUZELTME", 5, null, null, false),   // kesin bakiye öncesi: sayılmaz
                          new IzinHareketGirdisi(2, new DateOnly(2026, 10, 1), "HTDUZELTME", 1.5m, null, "x", false),
                          new IzinHareketGirdisi(3, new DateOnly(2026, 10, 2), "HTDUZELTME", 9, null, null, true),   // iptal
                          new IzinHareketGirdisi(4, new DateOnly(2026, 10, 3), "RTKULLANIM", 1, "Zafer", null, false)]
        };
        var s = H(g);
        // 10 + pazarlar 20,27 Eyl, 4 Eki (3) − Ekim Gİ 2 (Eylül kesin bakiye ayı: sayılmaz) + 1,5
        Assert.Equal(12.5m, s.HT);
        Assert.Equal(-1m, s.RT);
        Assert.Equal(11.5m, s.Toplam);
        Assert.Equal(new DateOnly(2026, 9, 15), s.KesinBakiyeTarihi);
        // Yıllık izin: 6 hizmet yılı (2021..2026 yıl dönümleri) 14*5 + 20 = 90
        Assert.Equal(6, s.Kidem);
        Assert.Equal(90m, s.YIHak);
        Assert.Equal(20m, s.YillikHak);
    }

    [Fact]
    public void Yillik_izin_hakki_yas_ve_kidem_kurali_ve_kesin_kalan()
    {
        Assert.Equal(0, IzinBakiyeHesaplayici.YillikIzinHakki(0));
        Assert.Equal(14, IzinBakiyeHesaplayici.YillikIzinHakki(5));
        Assert.Equal(20, IzinBakiyeHesaplayici.YillikIzinHakki(6));
        Assert.Equal(26, IzinBakiyeHesaplayici.YillikIzinHakki(15));
        Assert.Equal(20, IzinBakiyeHesaplayici.YillikIzinHakki(2, 50));
        Assert.Equal(20, IzinBakiyeHesaplayici.YillikIzinHakki(2, 18));
        Assert.Equal(14, IzinBakiyeHesaplayici.YillikIzinHakki(2, 30));

        var g = new IzinBakiyeGirdisi
        {
            TcKimlikNo = "5", AdSoyad = "E", IseGiris = new DateOnly(2022, 3, 1), Dogum = new DateOnly(1974, 6, 1),
            Kodlar = new Dictionary<DateOnly, string> { [new(2026, 5, 4)] = "YI", [new(2026, 5, 5)] = "YI" },
            Hareketler = [new IzinHareketGirdisi(1, new DateOnly(2025, 1, 1), "YISABIT", 7, null, null, false),
                          new IzinHareketGirdisi(2, new DateOnly(2026, 6, 1), "YIKULLANIM", 3, null, null, false),
                          new IzinHareketGirdisi(3, new DateOnly(2026, 7, 1), "YIDEVIR", 2, null, null, false)]
        };
        var s = H(g);
        // kesin kalan 7 (2025-01-01) + 2025-03-01 (3. yıl, yaş 50) 20 + 2026-03-01 (4. yıl, 51) 20 + devir 2 = 49; kullanılan 2 + 3 = 5
        Assert.Equal(49m, s.YIHak);
        Assert.Equal(5m, s.YIKullanilan);
        Assert.Equal(44m, s.YIKalan);
    }

    [Fact]
    public void Ise_giris_yoksa_ve_pasif_cikissizsa_bakiye_hesaplanmaz()
    {
        Assert.Equal("İşe giriş tarihi yok", H(new IzinBakiyeGirdisi { TcKimlikNo = "6", AdSoyad = "F" }).Uyari);
        var s = H(new IzinBakiyeGirdisi { TcKimlikNo = "7", AdSoyad = "G", IseGiris = new DateOnly(2026, 1, 1), Pasif = true });
        Assert.Equal("Pasif, çıkış tarihi yok", s.Uyari);
        Assert.Null(s.Toplam);
        // kişisel hafta tatili: perşembe
        var p = H(new IzinBakiyeGirdisi { TcKimlikNo = "8", AdSoyad = "H", IseGiris = new DateOnly(2026, 10, 1), HaftaGunleri = "4" });
        Assert.Equal(2m, p.HT); // 1 ve 8 Ekim perşembe
        Assert.Equal("Perşembe", p.HaftaGunMetni);
        var c = H(new IzinBakiyeGirdisi { TcKimlikNo = "9", AdSoyad = "I", IseGiris = new DateOnly(2026, 10, 1) }, HaftaTatiliPolitikasi.CumartesiVePazar);
        Assert.Equal(3m, c.HT); // 3, 4 ve 10 Ekim
    }

    private async Task<Personel> PersonelEkleAsync(string tc, DateOnly giris)
    {
        var p = new Personel { TcKimlikNo = tc, AdSoyad = "TEST " + tc[..3], IlkIseGirisTarihi = giris, Isyeri = "Arsin Limanı" };
        Db.Personeller.Add(p);
        await Db.SaveChangesAsync();
        return p;
    }

    [Fact]
    public async Task Izin_karti_islemleri_kaydedilir_ve_geri_alinir()
    {
        var srv = S<IIzinBakiyeServisi>();
        var tc = "12345678901";
        await PersonelEkleAsync(tc, new DateOnly(2026, 1, 5));
        var bugun = DateOnly.FromDateTime(DateTime.Today);

        await srv.KesinBakiyeKaydetAsync(tc, bugun.AddDays(-3), 4m, "devir", Muhasebe);
        var k = await srv.KisiAsync(tc);
        Assert.Equal(bugun.AddDays(-3), k!.KesinBakiyeTarihi);
        var ilk = k.Toplam;

        await srv.HareketKaydetAsync(tc, bugun.AddDays(-1), "HTDUZELTME", 2m, null, "elle", Muhasebe);
        Assert.Equal(ilk + 2m, (await srv.KisiAsync(tc))!.Toplam);

        await srv.KesintiKaydetAsync(tc, "G", bugun.Year, bugun.Month, 1m, Muhasebe);
        await srv.RtDahilKaydetAsync(tc, false, Muhasebe);
        await srv.HaftaGunuKaydetAsync(tc, "64", Muhasebe);
        var ayar = await Db.PersonelIzinAyarlari.AsNoTracking().SingleAsync(a => a.TcKimlikNo == tc);
        Assert.Equal(("46", false), (ayar.HaftaTatili, ayar.RtDahil));

        var log = await srv.IslemListesiAsync(tc);
        Assert.Equal(5, log.Count);
        Assert.All(log, l => Assert.Equal("Yapıldı", l.Durum));
        // sondan başa geri al: ayar, RT dahil, kesinti, hareket, kesin bakiye
        foreach (var l in log) await srv.GeriAlAsync(l.Id, Muhasebe);
        Assert.False(await Db.PersonelIzinAyarlari.AnyAsync(a => a.TcKimlikNo == tc));
        var son = await srv.KisiAsync(tc);
        Assert.Null(son!.KesinBakiyeTarihi);
        Assert.Empty((await srv.ManuelKayitlarAsync(tc)).Where(m => !m.Iptal));
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.GeriAlAsync(log[0].Id, Muhasebe));

        // izleyici yazamaz; geçersiz değerler reddedilir
        await Assert.ThrowsAsync<YetkiHatasiException>(() => srv.HareketKaydetAsync(tc, bugun, "HTDUZELTME", 1, null, null, Izleyici));
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.HareketKaydetAsync(tc, bugun, "XYZ", 1, null, null, Muhasebe));
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.HareketKaydetAsync(tc, bugun, "HTDUZELTME", 0.3m, null, null, Muhasebe));
        await Assert.ThrowsAsync<InvalidOperationException>(() => srv.DogumTarihiKaydetAsync(tc, bugun.AddYears(-10), Muhasebe));
        await srv.DogumTarihiKaydetAsync(tc, new DateOnly(1990, 5, 5), Muhasebe);

        // toplu kesin bakiye + toplu izin günü tek işlemde geri alınır
        var tc2 = "22345678901";
        await PersonelEkleAsync(tc2, new DateOnly(2025, 6, 1));
        await srv.TopluKesinBakiyeAsync([tc, tc2], bugun.AddDays(-2), 0m, "Sezon kapama", Muhasebe);
        Assert.Equal(2, (await srv.TumuAsync(pasifDahil: true)).Count(x => x.KesinBakiyeTarihi == bugun.AddDays(-2) && x.KesinBakiyeDegeri == 0m));
        var sezon = (await srv.IslemListesiAsync("SEZON")).Single();
        await srv.GeriAlAsync(sezon.Id, Muhasebe);
        Assert.All(await srv.TumuAsync(pasifDahil: true), x => Assert.Null(x.KesinBakiyeTarihi));
        await srv.TopluHaftaGunuAsync([tc, tc2], "7", Muhasebe);
        Assert.Equal(2, await Db.PersonelIzinAyarlari.CountAsync());
        await srv.GeriAlAsync((await srv.IslemListesiAsync("*")).Single().Id, Muhasebe);
        Assert.Equal(0, await Db.PersonelIzinAyarlari.CountAsync());

        // manuel kaldır / geri yükle
        await srv.HareketKaydetAsync(tc, bugun, "RTKULLANIM", 1, "Cumhuriyet Bayramı", null, Muhasebe);
        var h = (await srv.ManuelKayitlarAsync(tc)).First(m => m.Anahtar.StartsWith("H|") && !m.Iptal);
        await srv.ManuelKaldirAsync(tc, h.Anahtar, h.Tur, Muhasebe);
        Assert.True((await srv.ManuelKayitlarAsync(tc)).First(m => m.Anahtar == h.Anahtar).Iptal);
        await srv.ManuelGeriYukleAsync(tc, h.Anahtar, h.Tur, Muhasebe);
        Assert.False((await srv.ManuelKayitlarAsync(tc)).First(m => m.Anahtar == h.Anahtar).Iptal);
    }

    [Fact]
    public async Task Excelden_gelen_eski_islem_gunlugu_komutlari_da_geri_alinir()
    {
        var tc = "32345678901";
        await PersonelEkleAsync(tc, new DateOnly(2026, 1, 5));
        Db.PuantajGunleri.Add(new PuantajGunu { TesisAdi = "Arsin Limanı", Yil = 2026, Ay = 11, Gun = 3, TcKimlikNo = tc, Kod = "Ç" });
        Db.PersonelIzinAyarlari.Add(new PersonelIzinAyari { TcKimlikNo = tc, RtDahil = false, HaftaTatili = "4" });
        Db.PysEkle(new PysIzinHareket { ID = 7, TCKN = tc, Tarih = new DateTime(2026, 3, 1), Tur = "HTDUZELTME", Gun = 1 });
        Db.PysEkle(new PysIzinIslemLog
        {
            ID = 1, TCKN = tc, Durum = "Yapıldı", Islem = "Excel işlemi",
            GeriAl = $"DELETE FROM [PuantajGunluk_Arsin_Limani] WHERE Yil=2026 AND Ay=11 AND Gun=3 AND TCKN='{tc}'\n" +
                     $"INSERT INTO [PuantajGunluk_Arsin_Limani] (Yil, Ay, TCKN, Gun, Kod) VALUES (2026, 11, '{tc}', 3, 'Yi')\n" +
                     "UPDATE IzinHareket SET Iptal=True WHERE ID=7\n" +
                     $"DELETE FROM PersonelIzinAyar WHERE TCKN='{tc}'\n" +
                     $"INSERT INTO PersonelIzinAyar (TCKN, RTDahil, HaftaTatili) VALUES ('{tc}', True, '67')"
        });
        await Db.SaveChangesAsync();
        await S<IIzinBakiyeServisi>().GeriAlAsync(1, Muhasebe);
        Db.ChangeTracker.Clear();
        Assert.Equal("Yi", (await Db.PuantajGunleri.SingleAsync(p => p.TcKimlikNo == tc)).Kod);
        Assert.True(((PysIzinHareket)(await Db.PysSorgu(typeof(PysIzinHareket)).ToListAsync()).Single()).Iptal);
        var a = await Db.PersonelIzinAyarlari.SingleAsync(x => x.TcKimlikNo == tc);
        Assert.Equal((true, "67"), (a.RtDahil, a.HaftaTatili));
    }

    /// <summary>
    /// Gerçek veriyle karşılaştırma (yalnız yerelde): PYS_ACCESS_CSV (Access CSV klasörü) ve PYS_SAYFA7_CSV
    /// (Excel 7. sayfasındaki güncel izin durumu ve yıllık izin sütunları) verilirse çalışır.
    /// </summary>
    [Fact]
    public async Task Gercek_veride_bakiyeler_excel_sayfa7_ile_ayni()
    {
        var klasor = Environment.GetEnvironmentVariable("PYS_ACCESS_CSV");
        var sayfa7 = Environment.GetEnvironmentVariable("PYS_SAYFA7_CSV");
        if (string.IsNullOrWhiteSpace(klasor) || !Directory.Exists(klasor) || string.IsNullOrWhiteSpace(sayfa7) || !File.Exists(sayfa7)) return;
        await S<IAccessAktarimServisi>().IceAktarAsync(klasor, Yonetici);

        var satirlar = CsvOkuyucu.Ayristir(File.ReadAllText(sayfa7)).Where(r => r.Count > 14 && r[1].Length == 11 && r[1].All(char.IsDigit)).ToList();
        // Excel 7. sayfayı her izin işleminden sonra yeniden yazar: hesap günü = işlem günlüğündeki son işlemin günü
        var sonIslem = (await Db.PysSorgu(typeof(PysIzinIslemLog)).ToListAsync()).Cast<PysIzinIslemLog>().Max(l => l.Zaman)!.Value;
        var hesap = DateOnly.FromDateTime(sonIslem);
        if (DateOnly.TryParse(Environment.GetEnvironmentVariable("PYS_HESAP_TARIHI"), CultureInfo.InvariantCulture, out var ht)) hesap = ht;
        _cikti.WriteLine($"Hesap tarihi {hesap:dd.MM.yyyy}, {satirlar.Count} kişi");
        // o tarihten sonra girilen kayıtlar karşılaştırmaya alınmaz
        var sinir = hesap.ToDateTime(TimeOnly.MaxValue);
        foreach (var t in new[] { typeof(PysHTBakiyeSabitleme), typeof(PysIzinHareket), typeof(PysGorevIzinKesinti), typeof(PysHasatIzinKesinti) })
            foreach (var r in await Db.PysSorgu(t).ToListAsync())
                if (t.GetProperty("KayitTarihi")!.GetValue(r) is DateTime k && k > sinir) Db.PysSil(r);
        await Db.SaveChangesAsync();
        var sonuc = (await S<IIzinBakiyeServisi>().TumuAsync(pasifDahil: true, bugun: hesap)).ToDictionary(s => s.TcKimlikNo);
        static decimal? D(string s) => decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        int ayni = 0, fark = 0, yiAyni = 0, yiFark = 0;
        foreach (var r in satirlar)
        {
            if (!sonuc.TryGetValue(r[1], out var s)) { _cikti.WriteLine("YOK " + r[1][..3]); fark++; continue; }
            var excel = D(r[7]);
            if (excel == s.Toplam) ayni++;
            else { fark++; _cikti.WriteLine($"FARK bakiye {r[1][..3]}… {s.Tesis}: Excel {excel?.ToString() ?? "boş"} / program {s.Toplam?.ToString() ?? "boş"} ({s.Uyari}) kesin={s.KesinBakiyeTarihi}"); }
            var yi = (D(r[12]), D(r[13]), D(r[14]));
            if (yi == (s.YIHak ?? 0, s.YIKullanilan, s.YIKalan ?? 0)) yiAyni++;
            else { yiFark++; if (yiFark <= 5) _cikti.WriteLine($"FARK yıllık {r[1][..3]}…: Excel {yi} / program ({s.YIHak}, {s.YIKullanilan}, {s.YIKalan})"); }
        }
        _cikti.WriteLine($"Bakiye: {ayni} aynı, {fark} farklı. Yıllık izin (7. sayfa formülleri, bilgi amaçlı): {yiAyni} aynı, {yiFark} farklı.");
        Assert.Equal(0, fark);
        Assert.Equal(satirlar.Count, ayni);
    }
}
