using System.Globalization;
using System.Numerics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Desktop.ViewModels;
using PayControl.Desktop.Views;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Infrastructure.Data;
using PayControl.Infrastructure.Import;

namespace PayControl.Desktop.Services;

/// <summary>
/// <c>PayControl.exe --ekran-goruntusu &lt;klasör&gt;</c>: geçici veritabanına sentetik demo verisi yükler
/// (gerçek bordro / banka dosyası akışlarıyla), her pencereyi açıp PNG olarak kaydeder ve çıkar.
/// Dokümantasyon ve tasarım incelemesi içindir; kullanıcının veritabanına dokunmaz.
/// </summary>
public static class EkranGoruntusuModu
{
    private const string Sirket = "Mavi Lojistik A.Ş.";
    private const double Olcek = 1.5;

    public static async Task CalistirAsync(IServiceProvider sp, string klasor)
    {
        Directory.CreateDirectory(klasor);
        var gecici = Path.Combine(Path.GetTempPath(), "pc-ekran-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(gecici);

        var kimlik = sp.GetRequiredService<IKimlikServisi>();
        var giris = await kimlik.GirisYapAsync("admin", "Admin123!");
        sp.GetRequiredService<OturumServisi>().Baslat(giris.KullaniciId!.Value, giris.KullaniciAdi!, giris.Rol!.Value);

        var demo = await DemoVeriAsync(sp, gecici);

        // 1) Giriş
        var login = sp.GetRequiredService<LoginWindow>();
        await KaydetAsync(login, Path.Combine(klasor, "01-giris.png"));

        // 2) Ana ekran
        var mainVm = sp.GetRequiredService<MainViewModel>();
        var main = new MainWindow(mainVm);
        await KaydetAsync(main, Path.Combine(klasor, "02-ana-ekran.png"), mainVm.IlkYukleAsync);

        // 3) Personel kartı — hesap ekstresi
        var detayVm = sp.GetRequiredService<PersonelDetayViewModel>();
        var detay = new PersonelDetayWindow(detayVm);
        await KaydetAsync(detay, Path.Combine(klasor, "03-personel-hesap-ekstresi.png"), () => detayVm.YukleAsync(demo.EkstrePersonelId));

        // 4) Ödeme öncesi kontrol (seçili kritik satırla)
        var odemeVm = sp.GetRequiredService<OdemeKontrolViewModel>();
        odemeVm.DonemYil = 2026;
        odemeVm.DonemAy = 10;
        odemeVm.BordroDosyaYolu = demo.EkimBordroDosyasi;
        odemeVm.BankaDosyaYolu = demo.EkimBankaDosyasi;
        var odeme = new OdemeKontrolWindow(odemeVm);
        await KaydetAsync(odeme, Path.Combine(klasor, "04-odeme-oncesi-kontrol.png"), async () =>
        {
            await odemeVm.KontrolEtCommand.ExecuteAsync(null);
            odemeVm.SeciliSatir = odemeVm.Satirlar.FirstOrDefault(s => s.KritikMi && !s.EslesmeBelirsiz) ?? odemeVm.Satirlar.FirstOrDefault();
        });

        // 5) Kapanış ve mutabakat
        var kapanisVm = sp.GetRequiredService<DonemKapanisViewModel>();
        kapanisVm.DonemYil = 2026;
        kapanisVm.DonemAy = 9;
        var kapanis = new DonemKapanisWindow(kapanisVm);
        await KaydetAsync(kapanis, Path.Combine(klasor, "05-kapanis-ve-mutabakat.png"), () => kapanisVm.YukleCommand.ExecuteAsync(null));

        // 6) Bordro içe aktarma önizleme
        var bordroVm = sp.GetRequiredService<BordroImportViewModel>();
        bordroVm.DosyaYolu = demo.KasimBordroDosyasi;
        bordroVm.DonemYil = 2026;
        bordroVm.DonemAy = 11;
        var bordro = new BordroImportWindow(bordroVm);
        await KaydetAsync(bordro, Path.Combine(klasor, "06-bordro-ice-aktarma.png"), () => bordroVm.OnizleCommand.ExecuteAsync(null));

        // 7) İşten çıkış formu
        var cikisVm = sp.GetRequiredService<IstenCikisViewModel>();
        cikisVm.PersonelId = demo.EkstrePersonelId;
        cikisVm.PersonelAd = "Ayşe Yılmaz — 10000000001";
        cikisVm.CikisNedeni = "İstifa";
        var cikis = new IstenCikisWindow(cikisVm);
        await KaydetAsync(cikis, Path.Combine(klasor, "07-isten-cikis.png"));

        // 8) Rapor merkezi — tesis karşılaştırması
        var raporVm = sp.GetRequiredService<RaporMerkeziViewModel>();
        raporVm.BaslangicYil = 2026; raporVm.BaslangicAy = 9; raporVm.BitisYil = 2026; raporVm.BitisAy = 9;
        raporVm.SeciliTanim = raporVm.Tanimlar.First(t => t.Tur == RaporTuru.TesisKarsilastirma);
        await KaydetAsync(new RaporMerkeziWindow(raporVm), Path.Combine(klasor, "08-rapor-merkezi.png"), async () =>
        {
            await Task.Delay(600);
            foreach (var a in raporVm.Alicilar.Take(2)) a.Secili = true;
        });

        // 9) Bildirimler — otomatik kurallar
        var bildirimVm = sp.GetRequiredService<BildirimlerViewModel>();
        await KaydetAsync(new BildirimlerWindow(bildirimVm), Path.Combine(klasor, "09-bildirimler.png"), async () =>
        {
            await Task.Delay(400);
            bildirimVm.SeciliKural = bildirimVm.Kurallar.FirstOrDefault();
        }, sekme: 1);

        // 10) Uyarı merkezi
        await KaydetAsync(sp.GetRequiredService<UyariMerkeziWindow>(), Path.Combine(klasor, "10-uyari-merkezi.png"));

        // 11) Tesisler — dönem tesis onayları
        var tesisVm = sp.GetRequiredService<TesislerViewModel>();
        tesisVm.DonemeOdaklan(2026, 9);
        await KaydetAsync(new TesislerWindow(tesisVm), Path.Combine(klasor, "11-tesis-onaylari.png"));

        // 12) Denetim geçmişi
        var denetimVm = sp.GetRequiredService<DenetimGecmisiViewModel>();
        denetimVm.Baslangic = DateTime.Today.AddDays(-1);
        await KaydetAsync(new DenetimGecmisiWindow(denetimVm), Path.Combine(klasor, "12-denetim-gecmisi.png"));

        // 13) Ayarlar ve yönetim — kullanıcılar
        await KaydetAsync(sp.GetRequiredService<YonetimWindow>(), Path.Combine(klasor, "13-ayarlar-yonetim.png"));

        // 15) Çıkış takibi
        var takipVm = sp.GetRequiredService<CikisTakibiViewModel>();
        await KaydetAsync(new CikisTakibiWindow(takipVm), Path.Combine(klasor, "15-cikis-takibi.png"));

        // 14) Personel kartı — icra / BES / tahsilat
        var detayVm2 = sp.GetRequiredService<PersonelDetayViewModel>();
        await KaydetAsync(new PersonelDetayWindow(detayVm2), Path.Combine(klasor, "14-personel-icra-bes-tahsilat.png"),
            () => detayVm2.YukleAsync(demo.IcraPersonelId), sekme: -3);

        try { Directory.Delete(gecici, true); } catch { /* yoksay */ }
    }

    /// <summary>Penceredeki ilk TabControl'de sekme seçer (negatif: sondan).</summary>
    private static void SekmeSec(DependencyObject kok, int sekme)
    {
        var tc = Bul<System.Windows.Controls.TabControl>(kok);
        if (tc is null || tc.Items.Count == 0) return;
        tc.SelectedIndex = sekme >= 0 ? Math.Min(sekme, tc.Items.Count - 1) : Math.Max(0, tc.Items.Count + sekme);
    }

    private static T? Bul<T>(DependencyObject d) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            var c = VisualTreeHelper.GetChild(d, i);
            if (c is T t) return t;
            if (Bul<T>(c) is { } r) return r;
        }
        return null;
    }

    private static async Task KaydetAsync(Window w, string yol, Func<Task>? hazirla = null, int? sekme = null)
    {
        // Pencere önce gerçekten açılır (bağlamalar, Loaded); ardından içerik ekran boyutundan bağımsız
        // olarak pencerenin tasarım boyutunda yeniden yerleştirilip çizilir.
        // Tasarım boyutu gösterimden önce okunur (küçük ekranda Windows pencereyi daraltır).
        var tasarimGenislik = double.IsNaN(w.Width) ? 1200 : w.Width;
        var tasarimYukseklik = double.IsNaN(w.Height) ? 800 : w.Height;
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = 0;
        w.Top = 0;
        w.ShowActivated = false;
        w.ShowInTaskbar = false;
        w.Show();
        if (hazirla is not null)
            await hazirla();
        if (sekme is { } sk)
        {
            await w.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            SekmeSec(w, sk);
        }
        await Task.Delay(400);

        var genislik = tasarimGenislik - 16;   // pencere kenarlığı
        var yukseklik = tasarimYukseklik - 39; // başlık çubuğu
        var icerik = (FrameworkElement)w.Content;
        var dataContext = w.DataContext;
        w.Content = null;
        var kap = new System.Windows.Controls.Border
        {
            Background = w.Background ?? Brushes.White,
            Child = icerik,
            Width = genislik,
            Height = yukseklik,
            DataContext = dataContext,
            Resources = w.Resources
        };
        System.Windows.Controls.TextBlock.SetFontFamily(kap, w.FontFamily);

        for (var i = 0; i < 3; i++)
        {
            kap.Measure(new Size(genislik, yukseklik));
            kap.Arrange(new Rect(0, 0, genislik, yukseklik));
            kap.UpdateLayout();
            await w.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            await Task.Delay(350);
        }

        var rtb = new RenderTargetBitmap((int)(genislik * Olcek), (int)(yukseklik * Olcek), 96 * Olcek, 96 * Olcek, PixelFormats.Pbgra32);
        rtb.Render(kap);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        await using (var fs = File.Create(yol))
            enc.Save(fs);

        kap.Child = null;
        w.Close();
    }

    // ------------------------------------------------------------------ Demo veri

    private sealed record DemoSonuc(Guid EkstrePersonelId, string EkimBordroDosyasi, string EkimBankaDosyasi, string KasimBordroDosyasi, Guid IcraPersonelId);

    private sealed record P(string Tc, string Ad, string Tesis, string Gorev, decimal Maas, int IbanNo, DateOnly Giris);

    private static async Task<DemoSonuc> DemoVeriAsync(IServiceProvider sp, string gecici)
    {
        var personelSrv = sp.GetRequiredService<IPersonelServisi>();
        var bordroSrv = sp.GetRequiredService<IBordroIceAktarmaServisi>();
        var avansSrv = sp.GetRequiredService<IAvansServisi>();
        var bankaSrv = sp.GetRequiredService<IBankaOdemeServisi>();
        var cikisSrv = sp.GetRequiredService<IIstenCikisServisi>();
        var defter = sp.GetRequiredService<IMaliHareketServisi>();
        var kontrol = sp.GetRequiredService<IOdemeOncesiKontrolServisi>();
        var donem = sp.GetRequiredService<IDonemKapanisServisi>();
        const string k = "admin";

        var liste = new List<P>
        {
            new("10000000001", "Ayşe Yılmaz", "İstanbul Depo", "Vardiya Amiri", 42000, 1, new(2023, 3, 1)),
            new("10000000002", "Mehmet Kaya", "İstanbul Depo", "Forklift Operatörü", 31000, 2, new(2024, 1, 15)),
            new("10000000003", "Zeynep Şahin", "Ankara Depo", "Muhasebe Uzmanı", 38500, 3, new(2022, 9, 1)),
            new("10000000004", "Ali Demir", "Ankara Depo", "Depo Görevlisi", 28500, 4, new(2025, 2, 1)),
            new("10000000005", "Elif Arslan", "İzmir Aktarma", "Planlama Uzmanı", 36000, 5, new(2024, 6, 3)),
            new("10000000006", "Burak Aydın", "İstanbul Depo", "Şoför", 30500, 6, new(2023, 11, 20)),
            new("10000000007", "Selin Koç", "İzmir Aktarma", "İK Uzmanı", 37500, 7, new(2021, 4, 12)),
            new("10000000008", "Hasan Çelik", "Ankara Depo", "Bakım Teknisyeni", 33000, 8, new(2022, 1, 10)),
            new("10000000009", "Deniz Öztürk", "İstanbul Depo", "Depo Görevlisi", 28500, 9, new(2025, 10, 1)),
            new("10000000010", "Gül Ateş", "İzmir Aktarma", "Müşteri Temsilcisi", 30000, 10, new(2024, 3, 1)),
        };

        var idler = new Dictionary<string, Guid>();
        foreach (var p in liste.Where(p => p.Giris < new DateOnly(2025, 10, 1)))
        {
            var dto = await personelSrv.OlusturAsync(new PersonelOlusturDto
            {
                TcKimlikNo = p.Tc, AdSoyad = p.Ad, Sirket = Sirket, Isyeri = p.Tesis, Gorevi = p.Gorev,
                IseGirisTarihi = p.Giris, GuncelMaas = p.Maas, Iban = Iban(p.IbanNo), BankaAdi = "Mavi Bank"
            }, k);
            idler[p.Tc] = dto.Id;
        }

        var ayse = idler["10000000001"];
        await avansSrv.VerAsync(ayse, new DateOnly(2026, 8, 20), 12000m, 3, 4000m, "Taksitli maaş avansı", k, "Konut tadilatı");
        await avansSrv.VerAsync(idler["10000000004"], new DateOnly(2026, 9, 10), 6000m, 2, 3000m, null, k);

        // Eylül bordrosu
        string Yol(string ad) => Path.Combine(gecici, ad);
        var eylul = liste.Where(p => idler.ContainsKey(p.Tc)).Select(p => new BordroSatiri(p, NetHesapla(p.Maas),
            Avans: p.Tc == "10000000001" ? 4000m : null, Icra: p.Tc == "10000000006" ? 2500m : null)).ToList();
        await BordroAktar(bordroSrv, Yol("Bordro-2026-09.xlsx"), 2026, 9, eylul, k);

        // Eylül banka ödemeleri (biri eksik ödendi) → mutabakat farkı
        var hareketler = eylul.Select((s, i) => new BankaHareketSatiri(i + 2, new DateOnly(2026, 10, 5), s.P.Tc, s.P.Ad, Iban(s.P.IbanNo),
            s.P.Tc == "10000000005" ? s.Net - 1500m : s.Net, $"DKT-2609-{i + 1:D3}", "EYLÜL MAAŞ")).ToList();
        await bankaSrv.HareketleriIceAktarAsync(2026, 9, hareketler, "Hesap-Hareketleri-Ekim.xlsx", "DEMO-HASH-0910", k);
        await bankaSrv.KasaOdemesiKaydetAsync(idler["10000000008"], new DateOnly(2026, 10, 6), 750m, 2026, 9, "Yol farkı kasadan", "KASA-0912", k);
        var eylulDonem = await donem.GetOrCreateAsync(2026, 9);
        await donem.MutabakatYapAsync(eylulDonem.Id, k);

        // Ekim: bir kişi 30 Eylül'de ayrıldı ama banka listesinde; bir kişi 15 Ekim'de ayrıldı; yeni giriş
        await cikisSrv.OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = idler["10000000006"], FiiliSonCalismaTarihi = new DateOnly(2026, 9, 30), IstenCikisTarihi = new DateOnly(2026, 9, 30),
            CikisNedeni = "İstifa", SonBordroDonemYil = 2026, SonBordroDonemAy = 9, SgkCikisDurumu = SgkCikisDurumu.Verildi, SgkKullaniciOnayi = true
        }, k);
        await cikisSrv.OlusturAsync(new IstenCikisOlusturDto
        {
            PersonelId = idler["10000000010"], FiiliSonCalismaTarihi = new DateOnly(2026, 10, 15), IstenCikisTarihi = new DateOnly(2026, 10, 15),
            CikisNedeni = "Sözleşme sonu", SonBordroDonemYil = 2026, SonBordroDonemAy = 10
        }, k);
        var deniz = liste.Single(p => p.Tc == "10000000009");
        idler[deniz.Tc] = (await personelSrv.OlusturAsync(new PersonelOlusturDto
        {
            TcKimlikNo = deniz.Tc, AdSoyad = deniz.Ad, Sirket = Sirket, Isyeri = deniz.Tesis, Gorevi = deniz.Gorev,
            IseGirisTarihi = deniz.Giris, GuncelMaas = deniz.Maas, Iban = Iban(deniz.IbanNo)
        }, k)).Id;
        await personelSrv.GuncelleAsync(idler["10000000003"], new PersonelGuncelleDto
        {
            AdSoyad = "Zeynep Şahin", Sirket = Sirket, Isyeri = "Ankara Depo", Gorevi = "Muhasebe Uzmanı", GuncelMaas = 41000m, Iban = Iban(3), BankaAdi = "Mavi Bank"
        }, k, "Ekim dönemi zammı");

        var ekim = liste.Where(p => p.Tc != "10000000006").Select(p => new BordroSatiri(
            p with { Maas = p.Tc == "10000000003" ? 41000m : p.Maas },
            p.Tc == "10000000010" ? Math.Round(NetHesapla(p.Maas) / 2, 2) : NetHesapla(p.Tc == "10000000003" ? 41000m : p.Maas),
            Gun: p.Tc == "10000000010" ? 15 : 30,
            Avans: p.Tc == "10000000001" ? 4000m : p.Tc == "10000000004" ? 3000m : null,
            Cikis: p.Tc == "10000000010" ? new DateOnly(2026, 10, 15) : null)).ToList();
        var ekimYol = Yol("Bordro-2026-10.xlsx");
        File.WriteAllBytes(ekimYol, BordroExcel(ekim));

        // Bankaya gönderilecek Ekim dosyası: IBAN hatası, fazla ödeme, ayrılmış personel, kimliği belirsiz satır
        var talimatlar = ekim.Select(s => (s.P.Tc, s.P.Ad, Iban: s.P.Tc == "10000000002" ? Iban(902) : Iban(s.P.IbanNo),
            Tutar: s.P.Tc == "10000000005" ? s.Net + 4200m : s.Net)).ToList();
        var burak = liste.Single(p => p.Tc == "10000000006");
        talimatlar.Add((burak.Tc, burak.Ad, Iban(burak.IbanNo), NetHesapla(burak.Maas)));
        talimatlar.Add(("", "Kemal Usta", Iban(777), 5000m));
        var bankaYol = Yol("MaviBank-Toplu-Odeme-2026-10.xlsx");
        File.WriteAllBytes(bankaYol, BankaExcel(talimatlar));

        // Ayşe'nin ekstresi için ek hareketler
        await defter.BordroDisiHakEdisAsync(ayse, MaliIslemTuru.Prim, new DateOnly(2026, 9, 30), 2026, 9, 2500m, "Eylül performans primi", "PRM-0925", k);
        await bankaSrv.KasaOdemesiKaydetAsync(ayse, new DateOnly(2026, 10, 8), 2500m, 2026, 9, "Performans primi kasadan ödendi", "KASA-0915", k);

        // Kasım bordrosu (önizleme ekranı için, içe aktarılmaz)
        var kasimYol = Yol("Bordro-2026-11.xlsx");
        var kasim = ekim.Where(s => s.P.Tc != "10000000010").Select(s => s with { Avans = s.P.Tc == "10000000001" ? 4000m : null, Cikis = null, Gun = 30 }).ToList();
        File.WriteAllBytes(kasimYol, BordroExcel(kasim));

        // 1.3: tesisler, icra/BES/tahsilat, bildirim alıcıları ve kuralları, tesis onayı
        var tesisSrv = sp.GetRequiredService<ITesisServisi>();
        await tesisSrv.EksikleriOlusturAsync(k);
        foreach (var t in await tesisSrv.ListeleAsync())
            await tesisSrv.KaydetAsync(new TesisDto
            {
                Id = t.Tesis.Id, Kod = t.Tesis.Kod, Ad = t.Tesis.Ad, Sirket = Sirket, Aktif = true,
                SorumluAdSoyad = t.Tesis.Ad switch { "İstanbul Depo" => "Murat Er", "Ankara Depo" => "Hande Yıldız", _ => "Can Tekin" },
                SorumluEposta = "sorumlu@ornek.test"
            }, k);
        await tesisSrv.TesisOnaylaAsync(eylulDonem.Id, "İstanbul Depo", "Bordro ve banka tutarları kontrol edildi.", k);

        var kesinti = sp.GetRequiredService<IKesintiServisi>();
        var hasan = idler["10000000008"];
        await kesinti.IcraDosyasiKaydetAsync(hasan, "2026/4417 E.", "Ankara 12. İcra Dairesi", "Örnek Finans A.Ş.", 48000m, 0.25m, 1, new DateOnly(2026, 8, 1), k);
        await kesinti.IcraDosyasiKaydetAsync(hasan, "2026/5120 E.", "Ankara 3. İcra Dairesi", "Demo Bank", 15000m, 0.25m, 2, new DateOnly(2026, 9, 15), k);
        await kesinti.BesTanimlaAsync(hasan, 0.03m, new DateOnly(2025, 1, 1), k);

        var bildirim = sp.GetRequiredService<IBildirimServisi>();
        await bildirim.AliciKaydetAsync(new BildirimAlicisi { AdSoyad = "Genel Müdür", Eposta = "gm@ornek.test", Telefon = "05321112233", Rol = "Yönetici" }, k);
        await bildirim.AliciKaydetAsync(new BildirimAlicisi { AdSoyad = "Murat Er", Eposta = "murat@ornek.test", Telefon = "05331112233", Rol = "Tesis sorumlusu", Tesis = "İstanbul Depo" }, k);
        await bildirim.AliciKaydetAsync(new BildirimAlicisi { AdSoyad = "Hande Yıldız", Eposta = "hande@ornek.test", Rol = "Tesis sorumlusu", Tesis = "Ankara Depo" }, k);
        await bildirim.AliciKaydetAsync(new BildirimAlicisi { AdSoyad = "Muhasebe Ekibi", Eposta = "muhasebe@ornek.test", Rol = "Muhasebe" }, k);
        await bildirim.KuralKaydetAsync(new BildirimKurali
        {
            Ad = "Aylık tesis karşılaştırması", Tetikleyici = BildirimTetikleyici.ZamanlanmisRapor, Siklik = BildirimSikligi.Aylik, Gun = 5, Saat = 9,
            Kanal = BildirimKanali.Eposta, AliciRolu = "Yönetici", RaporTuru = RaporTuru.TesisKarsilastirma, RaporBicimi = RaporBicimi.Pdf
        }, k);
        await bildirim.KuralKaydetAsync(new BildirimKurali
        {
            Ad = "Tesis bazlı bordro özeti", Tetikleyici = BildirimTetikleyici.ZamanlanmisRapor, Siklik = BildirimSikligi.Aylik, Gun = 6, Saat = 10,
            Kanal = BildirimKanali.EpostaVeWhatsApp, AliciRolu = "Tesis sorumlusu", Tesis = "İstanbul Depo", RaporTuru = RaporTuru.DonemBordroOzeti
        }, k);
        await bildirim.KuralKaydetAsync(new BildirimKurali
        {
            Ad = "Günlük kritik uyarı özeti", Tetikleyici = BildirimTetikleyici.UyariOzeti, Siklik = BildirimSikligi.Gunluk, Saat = 8,
            Kanal = BildirimKanali.WhatsApp, AliciRolu = "Muhasebe"
        }, k);
        await bildirim.KuralKaydetAsync(new BildirimKurali
        {
            Ad = "Dönem kapandığında bildir", Tetikleyici = BildirimTetikleyici.DonemKapandi, Kanal = BildirimKanali.Eposta, AliciRolu = "Yönetici"
        }, k);
        await bildirim.WhatsAppBaglantisiAsync("05321112233", "Eylül 2026 bordro özeti hazır.", k);

        // Çıkış talimatları: İK'ya verildi; biri unutuldu ve maaş yatmaya devam etti
        var ik = await bildirim.AliciKaydetAsync(new BildirimAlicisi { AdSoyad = "Ece Kara", Eposta = "ik@ornek.test", Telefon = "05351112233", Rol = "İnsan kaynakları" }, k);
        var cikisSrv2 = sp.GetRequiredService<ICikisTalimatServisi>();
        await cikisSrv2.OlusturAsync(new CikisTalimatiOlusturDto
        {
            PersonelId = idler["10000000004"], SonCalismaGunu = new DateOnly(2026, 9, 18), Neden = "Devamsızlık", SorumluAliciId = ik.Id,
            Aciklama = "3 gün üst üste gelmedi, tutanak tutuldu."
        }, k);
        await bankaSrv.KaydetAsync(idler["10000000004"], new DateOnly(2026, 11, 5), NetHesapla(28500), 2026, 10, Iban(4), "DKT-2610-004", k);
        await cikisSrv2.OlusturAsync(new CikisTalimatiOlusturDto
        {
            PersonelId = idler["10000000007"], SonCalismaGunu = new DateOnly(2026, 11, 30), Neden = "İstifa", SorumluAliciId = ik.Id
        }, k);

        var kimlik = sp.GetRequiredService<IKimlikServisi>();
        await kimlik.OlusturAsync(new KullaniciOlusturDto { KullaniciAdi = "muhasebe1", Sifre = "Muhasebe2026", Rol = KullaniciRolu.Muhasebe, AdSoyad = "Seda Uçar", Eposta = "seda@ornek.test" }, k);
        await kimlik.OlusturAsync(new KullaniciOlusturDto { KullaniciAdi = "denetci", Sifre = "Denetim2026", Rol = KullaniciRolu.Goruntuleyici, AdSoyad = "Okan Bal" }, k);

        return new DemoSonuc(ayse, ekimYol, bankaYol, kasimYol, hasan);
    }

    private static decimal NetHesapla(decimal brut) => Math.Round(brut * 0.71m, 2);

    private sealed record BordroSatiri(P P, decimal Net, decimal? Gun = 30, decimal? Avans = null, decimal? Icra = null, DateOnly? Cikis = null);

    private static async Task BordroAktar(IBordroIceAktarmaServisi srv, string yol, int yil, int ay, List<BordroSatiri> satirlar, string k)
    {
        File.WriteAllBytes(yol, BordroExcel(satirlar));
        await using var fs = File.OpenRead(yol);
        await srv.AktarAsync(fs, Path.GetFileName(yol), yil, ay, k);
    }

    private static byte[] BordroExcel(IEnumerable<BordroSatiri> satirlar)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Bordro");
        string[] b = ["T.C. KİMLİK NO", "ADI SOYADI", "İŞYERİ", "GÖREVİ", "MAAŞ", "GİRİŞ TARİHİ", "ÇIKIŞ TARİHİ", "GÜN", "NET İSTİHKAK", "IBAN NO", "AVANS", "İCRA"];
        for (var i = 0; i < b.Length; i++) ws.Cell(1, i + 1).Value = b[i];
        var r = 2;
        foreach (var s in satirlar)
        {
            ws.Cell(r, 1).Value = s.P.Tc;
            ws.Cell(r, 2).Value = s.P.Ad;
            ws.Cell(r, 3).Value = s.P.Tesis;
            ws.Cell(r, 4).Value = s.P.Gorev;
            ws.Cell(r, 5).Value = s.P.Maas;
            ws.Cell(r, 6).Value = s.P.Giris.ToDateTime(TimeOnly.MinValue);
            if (s.Cikis is { } c) ws.Cell(r, 7).Value = c.ToDateTime(TimeOnly.MinValue);
            if (s.Gun is { } g) ws.Cell(r, 8).Value = g;
            ws.Cell(r, 9).Value = s.Net;
            ws.Cell(r, 10).Value = Iban(s.P.IbanNo);
            if (s.Avans is { } a) ws.Cell(r, 11).Value = a;
            if (s.Icra is { } ic) ws.Cell(r, 12).Value = ic;
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] BankaExcel(IEnumerable<(string Tc, string Ad, string Iban, decimal Tutar)> satirlar)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("EFT Talimat");
        ws.Cell(1, 1).Value = "MAVİ BANK TOPLU ÖDEME TALİMATI";
        ws.Cell(2, 1).Value = "Müşteri No: 4471029";
        string[] b = ["SIRA", "ALICI ADI SOYADI", "T.C. KİMLİK NO", "IBAN", "TUTAR", "AÇIKLAMA"];
        for (var i = 0; i < b.Length; i++) ws.Cell(3, i + 1).Value = b[i];
        var r = 4;
        foreach (var s in satirlar)
        {
            ws.Cell(r, 1).Value = r - 3;
            ws.Cell(r, 2).Value = s.Ad;
            if (!string.IsNullOrEmpty(s.Tc)) ws.Cell(r, 3).Value = s.Tc;
            ws.Cell(r, 4).Value = s.Iban;
            ws.Cell(r, 5).Value = s.Tutar;
            ws.Cell(r, 6).Value = "EKİM MAAŞ";
            r++;
        }
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string Iban(int n)
    {
        var bban = "00061" + n.ToString(CultureInfo.InvariantCulture).PadLeft(17, '0');
        var kontrol = 98 - (int)(BigInteger.Parse(bban + "292700", CultureInfo.InvariantCulture) % 97);
        return $"TR{kontrol:D2}{bban}";
    }
}
