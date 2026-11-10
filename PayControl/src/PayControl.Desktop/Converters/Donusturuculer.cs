using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PayControl.Domain.Enums;

namespace PayControl.Desktop.Converters;

/// <summary>Rozet tonu: arka plan, yazı ve nokta renkleri.</summary>
public enum RozetTonu { Notr, Basari, Uyari, Tehlike, Bilgi, Mor }

/// <summary>
/// Enum/bool değerini renkli durum rozetine çevirir.
/// ConverterParameter: "arka" (arka plan), "yazi" (yazı rengi), "nokta" (vurgu), "metin" (Türkçe etiket).
/// </summary>
public sealed class DurumRozetiConverter : IValueConverter
{
    private static readonly Dictionary<RozetTonu, (string Arka, string Yazi, string Nokta)> Renkler = new()
    {
        [RozetTonu.Notr] = ("#F2F4F7", "#344054", "#98A2B3"),
        [RozetTonu.Basari] = ("#ECFDF3", "#067647", "#17B26A"),
        [RozetTonu.Uyari] = ("#FFFAEB", "#B54708", "#F79009"),
        [RozetTonu.Tehlike] = ("#FEF3F2", "#B42318", "#F04438"),
        [RozetTonu.Bilgi] = ("#EFF4FF", "#1D4ED8", "#2E6BF0"),
        [RozetTonu.Mor] = ("#F4F3FF", "#5925DC", "#7A5AF8"),
    };

    private static readonly Dictionary<string, Brush> Onbellek = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (ton, metin) = Coz(value);
        var p = parameter as string ?? "metin";
        if (p == "metin") return metin;
        var r = Renkler[ton];
        var hex = p switch { "arka" => r.Arka, "yazi" => r.Yazi, _ => r.Nokta };
        if (!Onbellek.TryGetValue(hex, out var brush))
        {
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            Onbellek[hex] = brush;
        }
        return brush;
    }

    public static (RozetTonu Ton, string Metin) Coz(object? value) => value switch
    {
        OdemeKontrolSonucu s => s switch
        {
            OdemeKontrolSonucu.Uygun => (RozetTonu.Basari, "Uygun"),
            OdemeKontrolSonucu.EksikOdemeRiski => (RozetTonu.Uyari, "Eksik ödeme riski"),
            OdemeKontrolSonucu.FazlaOdemeRiski => (RozetTonu.Tehlike, "Fazla ödeme riski"),
            OdemeKontrolSonucu.IstenCikisKontrolu => (RozetTonu.Mor, "İşten çıkış kontrolü"),
            OdemeKontrolSonucu.IbanUyusmazligi => (RozetTonu.Tehlike, "IBAN uyuşmazlığı"),
            OdemeKontrolSonucu.MukerrerOdemeRiski => (RozetTonu.Tehlike, "Mükerrer ödeme riski"),
            OdemeKontrolSonucu.BordroUyusmazligi => (RozetTonu.Tehlike, "Bordro uyuşmazlığı"),
            _ => (RozetTonu.Uyari, "İnceleme gerekiyor")
        },
        CalismaDurumu c => c switch
        {
            CalismaDurumu.Aktif => (RozetTonu.Basari, "Aktif"),
            CalismaDurumu.IstenAyrildi => (RozetTonu.Notr, "İşten ayrıldı"),
            _ => (RozetTonu.Uyari, "Askıda")
        },
        MaasDonemDurumu d => d switch
        {
            MaasDonemDurumu.Hazirlaniyor => (RozetTonu.Notr, "Hazırlanıyor"),
            MaasDonemDurumu.BordroYuklendi => (RozetTonu.Bilgi, "Bordro yüklendi"),
            MaasDonemDurumu.OdemeOncesiKontrolde => (RozetTonu.Bilgi, "Ödeme öncesi kontrolde"),
            MaasDonemDurumu.OdemeListesiOnaylandi => (RozetTonu.Bilgi, "Ödeme listesi onaylandı"),
            MaasDonemDurumu.BankaOdemesiBekleniyor => (RozetTonu.Bilgi, "Banka ödemesi bekleniyor"),
            MaasDonemDurumu.BankaMutabakatiYapiliyor => (RozetTonu.Mor, "Banka mutabakatı yapılıyor"),
            MaasDonemDurumu.IncelemeBekliyor => (RozetTonu.Uyari, "İnceleme bekliyor"),
            MaasDonemDurumu.KapanisaHazir => (RozetTonu.Basari, "Kapanışa hazır"),
            _ => (RozetTonu.Basari, "Kapatıldı")
        },
        FarkDurumu f => f switch
        {
            FarkDurumu.Acik => (RozetTonu.Tehlike, "Açık"),
            FarkDurumu.Cozuldu => (RozetTonu.Basari, "Çözüldü"),
            _ => (RozetTonu.Mor, "İstisna")
        },
        MutabakatFarkTuru t => t switch
        {
            MutabakatFarkTuru.EksikOdeme => (RozetTonu.Uyari, "Eksik ödeme"),
            MutabakatFarkTuru.FazlaOdeme => (RozetTonu.Tehlike, "Fazla ödeme"),
            MutabakatFarkTuru.BordrosuzOdeme => (RozetTonu.Tehlike, "Bordrosuz ödeme"),
            MutabakatFarkTuru.OnayliListeDisiOdeme => (RozetTonu.Uyari, "Liste dışı ödeme"),
            MutabakatFarkTuru.OnayliListedeOdenmedi => (RozetTonu.Uyari, "Listede, ödenmedi"),
            MutabakatFarkTuru.CikisSonrasiOdeme => (RozetTonu.Mor, "Çıkış sonrası ödeme"),
            _ => (RozetTonu.Tehlike, "Eşleşmeyen hareket")
        },
        SgkCikisDurumu g => g switch
        {
            SgkCikisDurumu.Verildi => (RozetTonu.Basari, "Verildi"),
            SgkCikisDurumu.Verilmedi => (RozetTonu.Tehlike, "Verilmedi"),
            _ => (RozetTonu.Uyari, "Belirsiz")
        },
        BakiyeTuru b => b switch
        {
            BakiyeTuru.MaasAlacagi => (RozetTonu.Bilgi, "Maaş alacağı"),
            BakiyeTuru.AvansBorcu => (RozetTonu.Uyari, "Avans borcu"),
            BakiyeTuru.SupheliFazlaOdeme => (RozetTonu.Mor, "Şüpheli fazla"),
            BakiyeTuru.KesinlesmisGeriOdeme => (RozetTonu.Tehlike, "Kesin geri ödeme"),
            _ => (RozetTonu.Notr, "Bilgi")
        },
        BildirimDurumu bd => bd switch
        {
            BildirimDurumu.Gonderildi => (RozetTonu.Basari, "Gönderildi"),
            BildirimDurumu.Hata => (RozetTonu.Tehlike, "Hata"),
            BildirimDurumu.ElleAcildi => (RozetTonu.Bilgi, "Elle açıldı"),
            _ => (RozetTonu.Uyari, "Bekliyor")
        },
        PayControl.Application.Interfaces.UyariSeviyesi us => us switch
        {
            PayControl.Application.Interfaces.UyariSeviyesi.Kritik => (RozetTonu.Tehlike, "Kritik"),
            PayControl.Application.Interfaces.UyariSeviyesi.Uyari => (RozetTonu.Uyari, "Uyarı"),
            _ => (RozetTonu.Bilgi, "Bilgi")
        },
        KullaniciRolu kr => kr switch
        {
            KullaniciRolu.Yonetici => (RozetTonu.Mor, "Yönetici"),
            KullaniciRolu.Muhasebe => (RozetTonu.Bilgi, "Muhasebe"),
            _ => (RozetTonu.Notr, "Görüntüleyici")
        },
        BildirimKanali bk => bk switch
        {
            BildirimKanali.Eposta => (RozetTonu.Bilgi, "E-posta"),
            BildirimKanali.WhatsApp => (RozetTonu.Basari, "WhatsApp"),
            _ => (RozetTonu.Mor, "E-posta + WhatsApp")
        },
        bool v => v ? (RozetTonu.Basari, "Evet") : (RozetTonu.Notr, "Hayır"),
        null => (RozetTonu.Notr, "—"),
        _ => (RozetTonu.Notr, value.ToString() ?? "")
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>"Ayşe Yılmaz" → "AY" (avatar).</summary>
public sealed class BasHarfConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parcalar = (value as string ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parcalar.Length == 0) return "?";
        var tr = CultureInfo.GetCultureInfo("tr-TR");
        return parcalar.Length == 1
            ? parcalar[0][..1].ToUpper(tr)
            : (parcalar[0][..1] + parcalar[^1][..1]).ToUpper(tr);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Negatif tutarlar kırmızı, pozitifler normal, sıfır soluk.</summary>
public sealed class TutarRengiConverter : IValueConverter
{
    private static readonly Brush Negatif = Dondur("#B42318");
    private static readonly Brush Pozitif = Dondur("#101828");
    private static readonly Brush Sifir = Dondur("#98A2B3");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var d = value switch
        {
            decimal x => x,
            double x => (decimal)x,
            int x => x,
            string t when decimal.TryParse(t, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out var y) => y,
            string t when decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var z) => z,
            _ => 0m
        };
        return d < 0 ? Negatif : d == 0 ? Sifir : Pozitif;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Brush Dondur(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }
}

/// <summary>null / boş metin / sıfır → Collapsed. Parametre "ters" ise tersi.</summary>
public sealed class GorunurlukConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var dolu = value switch
        {
            null => false,
            string s => !string.IsNullOrWhiteSpace(s),
            bool b => b,
            int i => i != 0,
            _ => true
        };
        if (parameter as string == "ters") dolu = !dolu;
        return dolu ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>UTC saklanan zamanı yerel saate çevirip "dd.MM.yyyy HH:mm" biçiminde gösterir.</summary>
public sealed class YerelZamanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DateTime d => (d.Kind == DateTimeKind.Local ? d : DateTime.SpecifyKind(d, DateTimeKind.Utc).ToLocalTime()).ToString(parameter as string ?? "dd.MM.yyyy HH:mm", culture),
        _ => ""
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>bool tersi (iki yönlü; RadioButton çiftleri için).</summary>
public sealed class TersBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
