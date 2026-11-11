using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PayControl.Application.Interfaces;
using PayControl.Domain.Entities;

namespace PayControl.Infrastructure.Bildirim;

public sealed class EpostaGonderici : IEpostaGonderici
{
    public async Task GonderAsync(BildirimAyari ayar, string sifre, string alici, string konu, string metin, BildirimEki? ek, CancellationToken ct = default)
    {
        var mesaj = new MimeMessage();
        mesaj.From.Add(new MailboxAddress(ayar.GonderenAd ?? "Personel Yönetim Sistemi", ayar.GonderenEposta));
        mesaj.To.Add(MailboxAddress.Parse(alici));
        mesaj.Subject = konu;
        var govde = new BodyBuilder
        {
            TextBody = metin,
            HtmlBody = "<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#101828\">" +
                       string.Join("<br/>", metin.Split('\n').Select(System.Net.WebUtility.HtmlEncode)) +
                       "</div>"
        };
        if (ek is not null) govde.Attachments.Add(ek.DosyaAdi, ek.Icerik, ContentType.Parse(ek.IcerikTuru));
        mesaj.Body = govde.ToMessageBody();

        using var smtp = new SmtpClient { Timeout = 30_000 };
        var guvenlik = !ayar.SmtpSsl ? SecureSocketOptions.None : ayar.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await smtp.ConnectAsync(ayar.SmtpSunucu, ayar.SmtpPort, guvenlik, ct);
        if (!string.IsNullOrEmpty(ayar.SmtpKullanici))
            await smtp.AuthenticateAsync(ayar.SmtpKullanici, sifre, ct);
        await smtp.SendAsync(mesaj, ct);
        await smtp.DisconnectAsync(true, ct);
    }
}

/// <summary>
/// WhatsApp Business Cloud API. Şablon adı ayarlıysa iş başlatmalı mesaj olarak onaylı şablon gönderilir
/// (Meta kuralı: 24 saatlik müşteri penceresi dışında serbest metin gönderilemez); ek, belge mesajı olarak eklenir.
/// </summary>
public sealed class WhatsAppGonderici : IWhatsAppGonderici
{
    private readonly HttpClient _http;

    public WhatsAppGonderici(HttpClient http) => _http = http;

    public async Task GonderAsync(BildirimAyari ayar, string erisimAnahtari, string telefon, string metin, BildirimEki? ek, CancellationToken ct = default)
    {
        var temel = $"{ayar.WhatsAppApiAdresi.TrimEnd('/')}/{ayar.WhatsAppTelefonNumarasiId}";
        object govde = string.IsNullOrWhiteSpace(ayar.WhatsAppSablonAdi)
            ? new { messaging_product = "whatsapp", to = telefon, type = "text", text = new { preview_url = false, body = Kisalt(metin, 4000) } }
            : new
            {
                messaging_product = "whatsapp", to = telefon, type = "template",
                template = new
                {
                    name = ayar.WhatsAppSablonAdi,
                    language = new { code = ayar.WhatsAppDil },
                    components = new object[] { new { type = "body", parameters = new object[] { new { type = "text", text = Kisalt(metin.Replace('\n', ' '), 1000) } } } }
                }
            };
        await PostJsonAsync($"{temel}/messages", erisimAnahtari, govde, ct);

        if (ek is null) return;
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        form.Add(new StringContent(ek.IcerikTuru), "type");
        var dosya = new ByteArrayContent(ek.Icerik);
        dosya.Headers.ContentType = new MediaTypeHeaderValue(ek.IcerikTuru);
        form.Add(dosya, "file", ek.DosyaAdi);
        using var istek = new HttpRequestMessage(HttpMethod.Post, $"{temel}/media") { Content = form };
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", erisimAnahtari);
        using var yanit = await _http.SendAsync(istek, ct);
        var json = await yanit.Content.ReadAsStringAsync(ct);
        if (!yanit.IsSuccessStatusCode) throw new InvalidOperationException($"WhatsApp dosya yükleme hatası ({(int)yanit.StatusCode}): {Kisalt(json, 300)}");
        var medyaId = JsonDocument.Parse(json).RootElement.GetProperty("id").GetString();
        await PostJsonAsync($"{temel}/messages", erisimAnahtari, new
        {
            messaging_product = "whatsapp", to = telefon, type = "document",
            document = new { id = medyaId, filename = ek.DosyaAdi, caption = "Personel Yönetim Sistemi" }
        }, ct);
    }

    private async Task PostJsonAsync(string url, string anahtar, object govde, CancellationToken ct)
    {
        using var istek = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(govde), Encoding.UTF8, "application/json")
        };
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", anahtar);
        using var yanit = await _http.SendAsync(istek, ct);
        if (!yanit.IsSuccessStatusCode)
            throw new InvalidOperationException($"WhatsApp API hatası ({(int)yanit.StatusCode}): {Kisalt(await yanit.Content.ReadAsStringAsync(ct), 300)}");
    }

    private static string Kisalt(string s, int n) => s.Length <= n ? s : s[..n];
}
