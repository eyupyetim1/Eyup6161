using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PayControl.Application.Interfaces;
using PayControl.Application.Services;
using PayControl.Domain;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Infrastructure.Bildirim;

namespace PayControl.Tests.Modules;

public class BildirimTestleri : TestOrtami
{
    /// <summary>Testler için tek bağlantılık sahte SMTP sunucusu: gelen DATA içeriğini yakalar.</summary>
    private sealed class SahteSmtp : IAsyncDisposable
    {
        private readonly TcpListener _dinleyici = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _iptal = new();
        public List<string> Mesajlar { get; } = new();
        public int Port => ((IPEndPoint)_dinleyici.LocalEndpoint).Port;

        public SahteSmtp()
        {
            _dinleyici.Start();
            _ = Task.Run(DonguAsync);
        }

        private async Task DonguAsync()
        {
            while (!_iptal.IsCancellationRequested)
            {
                TcpClient istemci;
                try { istemci = await _dinleyici.AcceptTcpClientAsync(_iptal.Token); } catch { return; }
                _ = Task.Run(async () =>
                {
                    using var _ = istemci;
                    var akis = istemci.GetStream();
                    var okuyucu = new StreamReader(akis, Encoding.ASCII);
                    var yazici = new StreamWriter(akis, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
                    await yazici.WriteLineAsync("220 sahte ESMTP");
                    var veri = false;
                    var sb = new StringBuilder();
                    while (await okuyucu.ReadLineAsync() is { } satir)
                    {
                        if (veri)
                        {
                            if (satir == ".") { veri = false; lock (Mesajlar) Mesajlar.Add(sb.ToString()); sb.Clear(); await yazici.WriteLineAsync("250 OK"); }
                            else sb.AppendLine(satir);
                            continue;
                        }
                        var k = satir.ToUpperInvariant();
                        if (k.StartsWith("EHLO")) { await yazici.WriteLineAsync("250-sahte"); await yazici.WriteLineAsync("250 8BITMIME"); }
                        else if (k.StartsWith("DATA")) { veri = true; await yazici.WriteLineAsync("354 devam"); }
                        else if (k.StartsWith("QUIT")) { await yazici.WriteLineAsync("221 bye"); return; }
                        else await yazici.WriteLineAsync("250 OK");
                    }
                });
            }
        }

        public async ValueTask DisposeAsync()
        {
            _iptal.Cancel();
            _dinleyici.Stop();
            await Task.Delay(50);
        }
    }

    private sealed class SahteHttp : HttpMessageHandler
    {
        public List<(string Url, string? Yetki, string Govde)> Istekler { get; } = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var govde = r.Content is null ? "" : await r.Content.ReadAsStringAsync(ct);
            Istekler.Add((r.RequestUri!.ToString(), r.Headers.Authorization?.ToString(), govde));
            var yanit = r.RequestUri!.AbsolutePath.EndsWith("/media") ? "{\"id\":\"MEDYA123\"}" : "{\"messages\":[{\"id\":\"wamid.1\"}]}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(yanit) };
        }
    }

    private async Task<BildirimAlicisi> AliciAsync(string ad, string? eposta, string? tel, string rol = "Yönetici", string? tesis = null) =>
        await S<IBildirimServisi>().AliciKaydetAsync(new BildirimAlicisi { AdSoyad = ad, Eposta = eposta, Telefon = tel, Rol = rol, Tesis = tesis }, Muhasebe);

    [Fact]
    public async Task Eposta_gercek_SMTP_istemcisiyle_ekli_rapor_gonderir_sifre_sifreli_saklanir()
    {
        await using var smtp = new SahteSmtp();
        var srv = S<IBildirimServisi>();
        await Assert.ThrowsAsync<YetkiHatasiException>(() => srv.AyarlariKaydetAsync(new BildirimAyarDto(), Muhasebe));
        await srv.AyarlariKaydetAsync(new BildirimAyarDto
        {
            SmtpSunucu = "127.0.0.1", SmtpPort = smtp.Port, SmtpSsl = false, GonderenEposta = "paycontrol@ornek.test", SmtpSifre = "SmtpGizli1"
        }, Yonetici);
        var ayar = await Db.BildirimAyarlari.AsNoTracking().SingleAsync();
        Assert.DoesNotContain("SmtpGizli1", ayar.SmtpSifreSifreli);
        Assert.True((await srv.AyarlariGetirAsync()).SmtpSifreKayitli);

        var p = await PersonelAsync("Mail Rapor", 30000, 90, isyeri: "İstanbul Depo");
        await BordroYukleAsync(2026, 10, new BordroSatir(p, 25000m));
        var alici = await AliciAsync("Müdür", "mudur@ornek.test", null);

        var sonuc = await srv.RaporGonderAsync(RaporTuru.DonemBordroOzeti, RaporParametreleri.TekDonem(2026, 10), RaporBicimi.Excel,
            BildirimKanali.Eposta, [alici.Id], "Ekim bordro özeti ektedir.", Muhasebe);
        var k = Assert.Single(sonuc);
        Assert.Equal(BildirimDurumu.Gonderildi, k.Durum);
        await Task.Delay(200);
        var mesaj = Assert.Single(smtp.Mesajlar);
        Assert.Contains("mudur@ornek.test", mesaj);
        Assert.Contains("Subject:", mesaj);
        Assert.Contains(".xlsx", mesaj);
        Assert.Contains("Content-Disposition: attachment", mesaj);
    }

    [Fact]
    public async Task WhatsApp_cloud_api_sablon_ve_belge_istegi_dogru_olusturulur()
    {
        var http = new SahteHttp();
        var gonderici = new WhatsAppGonderici(new HttpClient(http));
        var ayar = new BildirimAyari { WhatsAppEtkin = true, WhatsAppTelefonNumarasiId = "12345", WhatsAppSablonAdi = "paycontrol_bildirim" };
        await gonderici.GonderAsync(ayar, "TOKEN-X", "905321112233", "Ekim raporu hazır", new BildirimEki("rapor.pdf", new byte[] { 1, 2, 3 }, "application/pdf"));

        Assert.Equal(3, http.Istekler.Count);
        Assert.EndsWith("/12345/messages", http.Istekler[0].Url);
        Assert.Equal("Bearer TOKEN-X", http.Istekler[0].Yetki);
        Assert.Contains("\"type\":\"template\"", http.Istekler[0].Govde);
        Assert.Contains("\"name\":\"paycontrol_bildirim\"", http.Istekler[0].Govde);
        Assert.Contains("\"to\":\"905321112233\"", http.Istekler[0].Govde);
        Assert.EndsWith("/12345/media", http.Istekler[1].Url);
        Assert.Contains("\"type\":\"document\"", http.Istekler[2].Govde);
        Assert.Contains("MEDYA123", http.Istekler[2].Govde);
    }

    [Fact]
    public async Task Elle_whatsapp_baglantisi_ve_telefon_normalizasyonu()
    {
        var url = await S<IBildirimServisi>().WhatsAppBaglantisiAsync("0532 111 22 33", "Ekim maaşı: 25.000,00 ₺ & avans", Muhasebe);
        Assert.StartsWith("https://wa.me/905321112233?text=", url);
        Assert.Contains("%26", url);
        Assert.Equal(BildirimDurumu.ElleAcildi, (await S<IBildirimServisi>().GecmisAsync()).Single().Durum);
        Assert.Equal("905321112233", BildirimServisi.TelefonNormalize("+90 (532) 111-22-33"));
        Assert.Throws<InvalidOperationException>(() => BildirimServisi.TelefonNormalize("123"));
    }

    [Fact]
    public async Task Ayar_eksikse_gonderim_hata_olarak_kaydedilir_ve_yeniden_denenir()
    {
        var alici = await AliciAsync("Muhasebe", "m@ornek.test", "05321112233");
        var sonuc = await S<IBildirimServisi>().GonderAsync(new BildirimTalebi
        {
            Kanal = BildirimKanali.EpostaVeWhatsApp, AliciIdleri = [alici.Id], Konu = "Deneme", Metin = "Metin"
        }, Muhasebe);
        Assert.Equal(2, sonuc.Count);
        Assert.All(sonuc, k => Assert.Equal(BildirimDurumu.Hata, k.Durum));
        Assert.Contains(sonuc, k => k.Hata!.Contains("SMTP"));
        Assert.Contains(sonuc, k => k.Hata!.Contains("Elle gönderim"));

        await using var smtp = new SahteSmtp();
        await S<IBildirimServisi>().AyarlariKaydetAsync(new BildirimAyarDto { SmtpSunucu = "127.0.0.1", SmtpPort = smtp.Port, SmtpSsl = false, GonderenEposta = "p@ornek.test" }, Yonetici);
        Assert.Equal(1, await S<IBildirimServisi>().BasarisizlariYenidenDeneAsync());
        Assert.Contains(await S<IUyariServisi>().ListeleAsync(), u => u.Kategori == "Bildirim");
    }

    [Fact]
    public async Task Zamanlanmis_aylik_rapor_vadesinde_bir_kez_gonderilir_olay_bildirimi_tekrarlanmaz()
    {
        await using var smtp = new SahteSmtp();
        var srv = S<IBildirimServisi>();
        await srv.AyarlariKaydetAsync(new BildirimAyarDto { SmtpSunucu = "127.0.0.1", SmtpPort = smtp.Port, SmtpSsl = false, GonderenEposta = "p@ornek.test" }, Yonetici);
        await AliciAsync("Ankara Sorumlusu", "ank@ornek.test", null, "Tesis sorumlusu", "Ankara Depo");
        await AliciAsync("İstanbul Sorumlusu", "ist@ornek.test", null, "Tesis sorumlusu", "İstanbul Depo");

        var kural = await srv.KuralKaydetAsync(new BildirimKurali
        {
            Ad = "Aylık Ankara özeti", Tetikleyici = BildirimTetikleyici.ZamanlanmisRapor, Siklik = BildirimSikligi.Aylik, Gun = 5, Saat = 9,
            Kanal = BildirimKanali.Eposta, AliciRolu = "Tesis sorumlusu", Tesis = "Ankara Depo", RaporTuru = RaporTuru.TesisKarsilastirma
        }, Muhasebe);
        // Kural geçmişte oluşturulmuş gibi
        var db = Db;
        (await db.BildirimKurallari.SingleAsync()).OlusturmaZamani = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        await db.SaveChangesAsync();

        // 5 Ekim 08:00: son vade 5 Eylül 09:00 ve kural 1 Eylül'de oluşturulduğu için bu vade gönderilir
        Assert.Equal(1, await srv.ZamanlanmisKurallariCalistirAsync(new DateTime(2026, 10, 5, 8, 0, 0)));
        // Aynı vade ikinci kez gönderilmez
        Assert.Equal(0, await srv.ZamanlanmisKurallariCalistirAsync(new DateTime(2026, 10, 5, 8, 30, 0)));
        await Task.Delay(200);
        var mesaj = Assert.Single(smtp.Mesajlar);
        Assert.Contains("ank@ornek.test", mesaj);
        Assert.DoesNotContain("ist@ornek.test", mesaj);
        Assert.Contains("Content-Disposition: attachment", mesaj);
        var gecmis = await srv.GecmisAsync();
        var kayit = Assert.Single(gecmis);
        Assert.Equal(kural.Id, kayit.KuralId);
        Assert.Equal(BildirimDurumu.Gonderildi, kayit.Durum);

        // Bir sonraki vade (5 Ekim 09:00) geldiğinde yeniden çalışır
        Assert.Equal(1, await srv.ZamanlanmisKurallariCalistirAsync(new DateTime(2026, 10, 5, 9, 5, 0)));

        // Olay bildirimi: aynı olay anahtarı ikinci kez gönderilmez
        await srv.KuralKaydetAsync(new BildirimKurali
        {
            Ad = "Kapanış bildirimi", Tetikleyici = BildirimTetikleyici.DonemKapandi, Kanal = BildirimKanali.Eposta, AliciRolu = "Tesis sorumlusu"
        }, Muhasebe);
        await srv.OlayBildirAsync(BildirimTetikleyici.DonemKapandi, "2026-10", "Dönem kapandı", "Ekim 2026 kapandı.", null);
        await srv.OlayBildirAsync(BildirimTetikleyici.DonemKapandi, "2026-10", "Dönem kapandı", "Ekim 2026 kapandı.", null);
        var olayKayitlari = (await srv.GecmisAsync()).Where(g => g.Konu == "Dönem kapandı").ToList();
        Assert.Equal(2, olayKayitlari.Count); // iki alıcı × bir kez
        Assert.Equal(2, olayKayitlari.Select(g => g.Alici).Distinct().Count());
    }

    [Fact]
    public async Task Vade_hesaplamasi_gunluk_haftalik_aylik()
    {
        var aylik = new BildirimKurali { Siklik = BildirimSikligi.Aylik, Gun = 5, Saat = 9 };
        Assert.Equal(new DateTime(2026, 10, 5, 9, 0, 0), BildirimServisi.SonVade(aylik, new DateTime(2026, 10, 20, 12, 0, 0)));
        Assert.Equal(new DateTime(2026, 9, 5, 9, 0, 0), BildirimServisi.SonVade(aylik, new DateTime(2026, 10, 5, 8, 59, 0)));
        var haftalik = new BildirimKurali { Siklik = BildirimSikligi.Haftalik, Gun = 1, Saat = 8 }; // Pazartesi
        Assert.Equal(new DateTime(2026, 11, 9, 8, 0, 0), BildirimServisi.SonVade(haftalik, new DateTime(2026, 11, 10, 7, 0, 0))); // 10.11.2026 Salı
        var gunluk = new BildirimKurali { Siklik = BildirimSikligi.Gunluk, Saat = 18 };
        Assert.Equal(new DateTime(2026, 11, 9, 18, 0, 0), BildirimServisi.SonVade(gunluk, new DateTime(2026, 11, 10, 17, 0, 0)));
    }

    [Fact]
    public async Task Zamanlanmis_gorev_calistiricisi_yedek_alir_ve_adim_hatasinda_digerlerine_devam_eder()
    {
        var gorev = S<IZamanlanmisGorevServisi>();
        var alici = await AliciAsync("Muhasebe", "m@ornek.test", null);
        await S<IBildirimServisi>().GonderAsync(new BildirimTalebi { AliciIdleri = [alici.Id], Konu = "K", Metin = "M" }, Muhasebe); // SMTP yok → hata

        var ilk = await gorev.CalistirAsync(new DateTime(2026, 11, 10, 10, 0, 0));
        Assert.Equal(!PgMi, ilk.YedekAlindi);
        Assert.Equal(0, ilk.YenidenDenenen); // SMTP hâlâ ayarsız
        Assert.Empty(ilk.Hatalar);
        var ikinci = await gorev.CalistirAsync(new DateTime(2026, 11, 10, 10, 5, 0));
        Assert.False(ikinci.YedekAlindi); // 24 saat dolmadı
        Assert.Equal(PgMi ? 0 : 1, (await S<IYedekServisi>().ListeleAsync()).Count(y => y.Otomatik));
    }

    [Fact]
    public void Windows_gorev_zamanlayici_argumanlari()
    {
        var a = PayControl.Infrastructure.Platform.WindowsGorevZamanlayici.OlusturmaArgumanlari(@"C:\Program Files\PayControl\PayControl.exe", 15);
        Assert.Contains("/Create", a);
        Assert.Equal("15", a[a.ToList().IndexOf("/MO") + 1]);
        Assert.Equal("\"C:\\Program Files\\PayControl\\PayControl.exe\" --zamanlanmis-gorevler", a[^1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => PayControl.Infrastructure.Platform.WindowsGorevZamanlayici.OlusturmaArgumanlari("x.exe", 1));
    }
}
