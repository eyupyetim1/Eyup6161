using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Interfaces;
using PayControl.Application.Services;
using PayControl.Desktop.Services;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Desktop.ViewModels;

/// <summary>Listede bir satır (VBA lstBakiye: Ad Soyad, Tesis, İzin günü, Güncel bakiye, HT, RT, Yıllık izin kalan, TCKN).</summary>
public sealed class BakiyeSatiri
{
    public required IzinBakiyeSonucu S { get; init; }
    public string AdSoyad => S.AdSoyad;
    public string TcKimlikNo => S.TcKimlikNo;
    public string? Tesis => S.Tesis;
    public string IzinGunu => S.HaftaGunMetni;
    public decimal? Toplam => S.Toplam;
    public string ToplamMetin => S.Toplam is null ? (S.Uyari ?? "—") : IzinBakiyeHesaplayici.Metin(S.Toplam, false);
    public string HTMetin => IzinBakiyeHesaplayici.Metin(S.HT, false);
    public string RTMetin => S.RT is null ? "—" : IzinBakiyeHesaplayici.Metin(S.RT, false) + (S.RtDahil ? "" : " (dahil değil)");
    public string YIKalanMetin => IzinBakiyeHesaplayici.Metin(S.YIKalan, false);
    public decimal? YIKalan => S.YIKalan;
    public bool Borclu => S.Toplam < 0;
    public bool Hesaplanamadi => S.Toplam is null;
    public string Durum => S.Pasif ? "Pasif" : "Aktif";
}

public sealed record OzetSatiri(string Kalem, string Deger, bool Vurgu = false);

public sealed class HaftaGunuSecimi : ObservableObject
{
    public int No { get; init; }
    public string Ad { get; init; } = "";
    private bool _secili;
    public bool Secili { get => _secili; set => SetProperty(ref _secili, value); }
}

/// <summary>
/// İzin ve bakiye ekranı: VBA'daki "İzin ve bakiye" listesi ile "İzin kartı" (bakiye özeti, hareket defteri,
/// manuel kayıtlar, resmi tatiller, yıl özeti, işlem günlüğü ve geri alma, elle işlemler, izin günü ayarları).
/// </summary>
public partial class IzinBakiyeViewModel : ObservableObject
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    public const string TumTesisler = "Tüm tesisler";
    private readonly IIzinBakiyeServisi _servis;
    private readonly IPuantajServisi _puantaj;
    private readonly OturumServisi _oturum;
    private List<BakiyeSatiri> _tum = new();

    public IzinBakiyeViewModel(IIzinBakiyeServisi servis, IPuantajServisi puantaj, OturumServisi oturum)
    {
        _servis = servis;
        _puantaj = puantaj;
        _oturum = oturum;
        foreach (var (no, ad) in new[] { (1, "Pzt"), (2, "Sal"), (3, "Çar"), (4, "Per"), (5, "Cum"), (6, "Cmt"), (7, "Paz") })
            HaftaGunleri.Add(new HaftaGunuSecimi { No = no, Ad = ad });
    }

    private string Kullanici => _oturum.KullaniciAdi ?? "";
    public bool Yonetici => _oturum.Rol == KullaniciRolu.Yonetici;
    public bool YazabilirMi => _oturum.Rol is KullaniciRolu.Yonetici or KullaniciRolu.Muhasebe;

    /// <summary>Başka ekrandan açılırken önceden seçilecek personel.</summary>
    public string? AcilistaTc { get; set; }

    public ObservableCollection<string> Tesisler { get; } = new();
    public ObservableCollection<BakiyeSatiri> Satirlar { get; } = new();
    [ObservableProperty] private string _seciliTesis = TumTesisler;
    [ObservableProperty] private bool _pasifDahil;
    [ObservableProperty] private bool _yalnizBorclu;
    [ObservableProperty] private string? _arama;
    [ObservableProperty] private BakiyeSatiri? _seciliSatir;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private bool _yukleniyor;

    [ObservableProperty] private string _kpiKisi = "—";
    [ObservableProperty] private string _kpiAlacak = "—";
    [ObservableProperty] private string _kpiAlacakAlt = "";
    [ObservableProperty] private string _kpiBorc = "—";
    [ObservableProperty] private string _kpiBorcAlt = "";
    [ObservableProperty] private string _kpiYillik = "—";
    [ObservableProperty] private string _kpiHesaplanamayan = "—";

    // Genel ayar
    public IReadOnlyList<Secenek<HaftaTatiliPolitikasi>> Politikalar { get; } =
        ArayuzYardimcisi.EnumSecenekleri((HaftaTatiliPolitikasi.SadecePazar, "Sadece Pazar"), (HaftaTatiliPolitikasi.Cumartesi, "Cumartesi"),
            (HaftaTatiliPolitikasi.CumartesiVePazar, "Cumartesi ve Pazar"));
    [ObservableProperty] private HaftaTatiliPolitikasi _politika = HaftaTatiliPolitikasi.SadecePazar;

    // ------------------------------------------------------------------ izin kartı
    [ObservableProperty] private IzinBakiyeSonucu? _kart;
    public ObservableCollection<OzetSatiri> Ozet { get; } = new();
    public ObservableCollection<OzetSatiri> YillikOzet { get; } = new();
    public ObservableCollection<BakiyeDefterSatiri> Defter { get; } = new();
    public ObservableCollection<IzinManuelKayit> ManuelKayitlar { get; } = new();
    public ObservableCollection<RtTabloSatiri> RtTablosu { get; } = new();
    public ObservableCollection<YilOzetiSatiri> YilOzeti { get; } = new();
    public ObservableCollection<IzinIslemKaydi> Islemler { get; } = new();
    public ObservableCollection<HaftaGunuSecimi> HaftaGunleri { get; } = new();
    public ObservableCollection<int> Yillar { get; } = new();
    public ObservableCollection<string> TatilAdlari { get; } = new();
    [ObservableProperty] private int _seciliYil = DateTime.Today.Year;
    [ObservableProperty] private IzinManuelKayit? _seciliManuel;
    [ObservableProperty] private IzinIslemKaydi? _seciliIslem;
    [ObservableProperty] private bool _defterBilgiGoster = true;
    [ObservableProperty] private bool _kartRtDahil = true;
    [ObservableProperty] private DateTime? _kartDogum;
    [ObservableProperty] private string _kartBaslik = "Personel seçin";
    [ObservableProperty] private string _kartAlt = "";
    private List<BakiyeDefterSatiri> _defterTum = new();

    /// <summary>VBA cboIKTur sırası.</summary>
    public IReadOnlyList<string> IslemTurleri { get; } =
    [
        "Kesin bakiye", "HT düzeltme (+/-)", "Resmi tatil kullanımı", "Resmi tatil ek hak / düşürme (+/-)", "Görev izni kesintisi",
        "Hasat izni kesintisi", "Yıllık izin kullanımı", "Yıllık izin ekle / düş (+/-)", "Yıllık izin kalan (kesin)", "Sezon kapama (bu personel)"
    ];
    [ObservableProperty] private int _islemTuru;
    [ObservableProperty] private DateTime? _islemTarih = DateTime.Today;
    [ObservableProperty] private DateTime? _islemBitis;
    [ObservableProperty] private string? _islemGun;
    [ObservableProperty] private string? _islemTatil;
    [ObservableProperty] private string? _islemAciklama;
    [ObservableProperty] private string _islemIpucu = "";
    [ObservableProperty] private string _islemGunNot = "";
    public bool BitisAcik => IslemTuru is 2 or 6;
    public bool TatilAcik => IslemTuru == 2;
    public string TarihEtiketi => IslemTuru is 2 or 6 ? "Başlangıç" : IslemTuru is 4 or 5 ? "Ay (o ayın herhangi bir günü)" : "Tarih";
    public string GunEtiketi => IslemTuru switch
    {
        0 or 9 => "Bakiye (gün)",
        1 or 3 or 7 => "Gün (+ / -)",
        8 => "Kalan (gün)",
        _ => "Gün"
    };

    // Sezon kapama (toplu)
    [ObservableProperty] private DateTime? _sezonTarih = SezonOnerisi();
    [ObservableProperty] private string _sezonBakiye = "0";
    [ObservableProperty] private string? _sezonAciklama = "Sezon kapama " + SezonOnerisi().Year;
    [ObservableProperty] private bool _sezonYalnizSecili;
    [ObservableProperty] private string _sezonSonIslem = "";

    // İzin süresi hesaplama
    [ObservableProperty] private DateTime? _hesapBaslangic = DateTime.Today;
    [ObservableProperty] private string? _hesapIsGunu = "14";
    [ObservableProperty] private string _hesapSonuc = "";
    public ObservableCollection<string> HesapGunleri { get; } = new();

    private static DateTime SezonOnerisi()
    {
        var b = new DateTime(DateTime.Today.Year, 7, 31);
        return b <= DateTime.Today ? b : b.AddYears(-1);
    }

    // ------------------------------------------------------------------ yükleme

    public async Task YukleAsync()
    {
        try
        {
            Tesisler.Clear();
            Tesisler.Add(TumTesisler);
            foreach (var t in await _puantaj.TesislerAsync()) Tesisler.Add(t);
            Politika = await _servis.PolitikaAsync();
            TatilAdlari.Clear();
            foreach (var t in (await _puantaj.ResmiTatillerAsync()).OrderByDescending(t => t.Tarih))
                TatilAdlari.Add($"{t.Ad} {t.Tarih.Year}");
            OnIslemTuruChanged(IslemTuru);
            await YenileAsync();
            if (AcilistaTc is not null) SeciliSatir = _tum.FirstOrDefault(s => s.TcKimlikNo == AcilistaTc);
            await SezonDurumuAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    partial void OnSeciliTesisChanged(string value) => _ = YenileAsync();
    partial void OnPasifDahilChanged(bool value) => _ = YenileAsync();
    partial void OnYalnizBorcluChanged(bool value) => Suz();
    private CancellationTokenSource? _aramaGecikme;
    partial void OnAramaChanged(string? value)
    {
        _aramaGecikme?.Cancel();
        var cts = _aramaGecikme = new CancellationTokenSource();
        _ = Task.Delay(200, cts.Token).ContinueWith(t => { if (!t.IsCanceled) Suz(); }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    [RelayCommand]
    private async Task YenileAsync()
    {
        if (Yukleniyor) return;
        Yukleniyor = true;
        try
        {
            var secili = SeciliSatir?.TcKimlikNo;
            var liste = await _servis.TumuAsync(SeciliTesis == TumTesisler ? null : SeciliTesis, PasifDahil);
            _tum = liste.Select(s => new BakiyeSatiri { S = s }).ToList();
            Suz();
            var hesaplanan = _tum.Where(s => s.Toplam is not null).ToList();
            KpiKisi = _tum.Count.ToString("N0", Tr);
            var alacak = hesaplanan.Where(s => s.Toplam > 0).ToList();
            var borc = hesaplanan.Where(s => s.Toplam < 0).ToList();
            KpiAlacak = IzinBakiyeHesaplayici.Metin(alacak.Sum(s => s.Toplam!.Value), false) + " gün";
            KpiAlacakAlt = $"{alacak.Count} kişide izin alacağı";
            KpiBorc = IzinBakiyeHesaplayici.Metin(borc.Sum(s => s.Toplam!.Value), false) + " gün";
            KpiBorcAlt = $"{borc.Count} kişi fazla izin kullandı";
            KpiYillik = IzinBakiyeHesaplayici.Metin(_tum.Where(s => s.YIKalan > 0).Sum(s => s.YIKalan!.Value), false) + " gün";
            KpiHesaplanamayan = _tum.Count(s => s.Hesaplanamadi).ToString("N0", Tr);
            if (secili is not null) SeciliSatir = Satirlar.FirstOrDefault(s => s.TcKimlikNo == secili);
        }
        catch (Exception ex) { Mesaj = ex.Message; }
        finally { Yukleniyor = false; }
    }

    private void Suz()
    {
        var secili = SeciliSatir;
        Satirlar.Clear();
        foreach (var s in _tum.Where(s => (!YalnizBorclu || s.Borclu) && AramaKurallari.Eslesir(Arama, s.AdSoyad, s.TcKimlikNo, s.Tesis)))
            Satirlar.Add(s);
        if (secili is not null && Satirlar.Contains(secili)) SeciliSatir = secili;
    }

    partial void OnSeciliSatirChanged(BakiyeSatiri? value) => _ = KartYukleAsync();

    private async Task KartYukleAsync()
    {
        var tc = SeciliSatir?.TcKimlikNo;
        Ozet.Clear(); YillikOzet.Clear(); Defter.Clear(); ManuelKayitlar.Clear(); RtTablosu.Clear(); YilOzeti.Clear(); Islemler.Clear();
        _defterTum.Clear();
        if (tc is null) { Kart = null; KartBaslik = "Personel seçin"; KartAlt = ""; return; }
        try
        {
            var k = await _servis.KisiAsync(tc);
            if (SeciliSatir?.TcKimlikNo != tc) return;
            Kart = k;
            if (k is null) return;
            KartBaslik = k.AdSoyad;
            KartAlt = string.Join(" · ", new[]
            {
                k.TcKimlikNo, k.Tesis, k.Unvan,
                k.IseGiris is { } g ? "İşe giriş " + g.ToString("dd.MM.yyyy", Tr) : "İşe giriş yok",
                k.Cikis is { } c ? "Çıkış " + c.ToString("dd.MM.yyyy", Tr) : null,
                "İzin günü: " + k.HaftaGunMetni
            }.Where(x => !string.IsNullOrWhiteSpace(x)));
            foreach (var (kalem, deger) in IzinBakiyeHesaplayici.Ozet(k))
                Ozet.Add(new OzetSatiri(kalem, deger, kalem.StartsWith('=')));
            YillikOzet.Add(new OzetSatiri("Kıdem", k.Kidem is { } kd ? $"{kd} yıl" : "—"));
            if (k.Yas is { } yas) YillikOzet.Add(new OzetSatiri("Yaş", yas.ToString(Tr)));
            YillikOzet.Add(new OzetSatiri("Bu yılki yıllık hak", IzinBakiyeHesaplayici.Metin(k.YillikHak, false) + " gün"));
            if (k.YISabitTarihi is { } yst) YillikOzet.Add(new OzetSatiri($"Kesin kalan {yst:dd.MM.yyyy}", IzinBakiyeHesaplayici.Metin(k.YISabitDegeri, false)));
            YillikOzet.Add(new OzetSatiri("Hakedilen (birikmiş)", IzinBakiyeHesaplayici.Metin(k.YIHak, false)));
            YillikOzet.Add(new OzetSatiri("Kullanılan: puantaj Yİ", IzinBakiyeHesaplayici.Metin(k.YIPuantaj, false)));
            if (k.YIElle != 0) YillikOzet.Add(new OzetSatiri("Kullanılan: izin kartından", IzinBakiyeHesaplayici.Metin(k.YIElle, false)));
            if (k.YIManuel != 0) YillikOzet.Add(new OzetSatiri("Kullanılan: eski manuel düzeltme", IzinBakiyeHesaplayici.Metin(k.YIManuel, false)));
            if (k.YIEk != 0) YillikOzet.Add(new OzetSatiri("Ekle / düş", IzinBakiyeHesaplayici.Metin(k.YIEk)));
            YillikOzet.Add(new OzetSatiri("= Kalan yıllık izin", IzinBakiyeHesaplayici.Metin(k.YIKalan, false), true));
            _defterTum = k.Defter;
            DefterSuz();
            foreach (var r in k.RtTablosu) RtTablosu.Add(r);
            foreach (var m in await _servis.ManuelKayitlarAsync(tc)) ManuelKayitlar.Add(m);
            foreach (var l in await _servis.IslemListesiAsync(tc)) Islemler.Add(l);
            foreach (var hg in HaftaGunleri) hg.Secili = k.KisiHaftaGunleri.Contains((char)('0' + hg.No));
            KartRtDahil = k.RtDahil;
            KartDogum = k.Dogum?.ToDateTime(TimeOnly.MinValue);
            Yillar.Clear();
            var ilk = Math.Min(k.IseGiris?.Year ?? DateTime.Today.Year, DateTime.Today.Year);
            for (var y = DateTime.Today.Year; y >= Math.Max(ilk, DateTime.Today.Year - 15); y--) Yillar.Add(y);
            SeciliYil = DateTime.Today.Year;
            await YilOzetiYukleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    partial void OnDefterBilgiGosterChanged(bool value) => DefterSuz();
    private void DefterSuz()
    {
        Defter.Clear();
        foreach (var d in _defterTum.Where(d => DefterBilgiGoster || d.Kova is "H" or "R" or "K")) Defter.Add(d);
    }

    partial void OnSeciliYilChanged(int value) => _ = YilOzetiYukleAsync();
    private async Task YilOzetiYukleAsync()
    {
        YilOzeti.Clear();
        if (SeciliSatir is null) return;
        try { foreach (var r in await _servis.YilOzetiAsync(SeciliSatir.TcKimlikNo, SeciliYil)) YilOzeti.Add(r); }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private async Task KisiYenileAsync()
    {
        var tc = SeciliSatir?.TcKimlikNo;
        await YenileAsync();
        if (tc is not null && SeciliSatir?.TcKimlikNo == tc) await KartYukleAsync();
        await SezonDurumuAsync();
    }

    // ------------------------------------------------------------------ elle işlem (VBA btnIKKaydet)

    partial void OnIslemTuruChanged(int value)
    {
        OnPropertyChanged(nameof(BitisAcik));
        OnPropertyChanged(nameof(TatilAcik));
        OnPropertyChanged(nameof(TarihEtiketi));
        OnPropertyChanged(nameof(GunEtiketi));
        if (!BitisAcik) IslemBitis = null;
        IslemGunNot = "";
        IslemIpucu = value switch
        {
            0 => "Bu tarihin gün sonundaki kesin bakiye. Hesap ertesi günden bu sayıdan devam eder.",
            1 => "Hafta tatili bakiyesine eklenecek (+) veya düşülecek (-) gün.",
            2 => "Bitiş yazarsanız gün sayısı (hafta tatili ve resmi tatiller hariç) kendiliğinden hesaplanır.",
            3 => "Ek hak (+5 kafa izni) veya düşürme (-5).",
            4 or 5 => "Seçilen ayın kesintisi. Aynı ay yeniden girilirse eskisinin yerine geçer; 0 kaldırır.",
            6 => "Puantaja Yİ yazılmayan izinler için. Puantajdaki Yİ günleri zaten sayılır, tekrar girmeyin.",
            7 => "Yıllık izne elle ekleme (+5) ya da düşme (-3). Hafta tatili bakiyesini etkilemez.",
            8 => "Bu tarih itibarıyla gerçek kalan yıllık izin. Öncesi yeniden sayılmaz; sonraki yıl dönümlerinde hak eklenir.",
            9 => "Bu personelin sezonunu bu tarihte kapatır: gün sonu bakiyesi bu değer olur (çoğunlukla 0). Toplu sezon kapama gibi geri alınabilir.",
            _ => ""
        };
        IslemTarih ??= DateTime.Today;
        if (value == 9)
        {
            IslemTarih = SezonOnerisi();
            if (string.IsNullOrWhiteSpace(IslemGun)) IslemGun = "0";
        }
        _ = GunHesaplaAsync();
    }

    partial void OnIslemTarihChanged(DateTime? value) => _ = GunHesaplaAsync();
    partial void OnIslemBitisChanged(DateTime? value) => _ = GunHesaplaAsync();

    /// <summary>VBA IKGunHesapla: başlangıç-bitiş girilince gün sayısını önerir.</summary>
    private async Task GunHesaplaAsync()
    {
        if (!BitisAcik || IslemTarih is not { } d1 || IslemBitis is not { } d2) return;
        if (d2 < d1 || (d2 - d1).TotalDays > 366) { IslemGunNot = "Tarih hatalı"; return; }
        try
        {
            var g = await _servis.IzinGunuSayAsync(DateOnly.FromDateTime(d1), DateOnly.FromDateTime(d2), SeciliSatir?.TcKimlikNo);
            IslemGun = IzinBakiyeHesaplayici.Metin(g, false);
            IslemGunNot = $"{(d2 - d1).TotalDays + 1:0} takvim günü";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    public static bool SayiOku(string? s, out decimal v)
    {
        v = 0;
        s = (s ?? "").Replace(" ", "").Trim();
        if (s.Length == 0) return false;
        return decimal.TryParse(s, NumberStyles.Number, Tr, out v) || decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out v);
    }

    [RelayCommand]
    private async Task IslemKaydetAsync()
    {
        if (SeciliSatir is null || Kart is null) { Mesaj = "Önce listeden personel seçin."; return; }
        var t = IslemTuru;
        if (IslemTarih is not { } tarihDt) { Mesaj = "Tarihi seçin."; return; }
        var d = DateOnly.FromDateTime(tarihDt);
        if (!SayiOku(IslemGun, out var v)) { Mesaj = "Gün / değer alanına sayı yazın (örn. 6, -2, 1,5)."; return; }
        var ack = (IslemAciklama ?? "").Trim();
        if (d > DateOnly.FromDateTime(DateTime.Today) && t is 0 or 8 or 9) { Mesaj = "Gelecek tarihli kayıt girilemez."; return; }
        if (Math.Abs(v) > 1000) { Mesaj = "Değer aralık dışında."; return; }
        if (t is 2 or 4 or 5 or 6 && v < 0) { Mesaj = "Bu işlemde değer eksi olamaz."; return; }
        var m = IzinBakiyeHesaplayici.Metin;
        var ozet = t switch
        {
            0 => $"Kesin bakiye: {d:dd.MM.yyyy} gün sonu = {m(v, false)} gün",
            1 => $"HT düzeltmesi: {m(v, true)} gün",
            2 => $"Resmi tatil kullanımı: {m(v, false)} gün" + (string.IsNullOrWhiteSpace(IslemTatil) ? "" : $" ({IslemTatil})"),
            3 => $"Resmi tatil ek hak / düşürme: {m(v, true)} gün",
            4 => $"Görev izni kesintisi, {d.ToString("MMMM yyyy", Tr)}: {m(v, false)} gün",
            5 => $"Hasat izni kesintisi, {d.ToString("MMMM yyyy", Tr)}: {m(v, false)} gün",
            6 => $"Yıllık izin kullanımı: {m(v, false)} gün ({d:dd.MM.yyyy}" + (IslemBitis is { } b ? $" - {b:dd.MM.yyyy}" : "") + ")",
            7 => $"Yıllık izin {(v >= 0 ? "ekleme" : "düşme")}: {m(v, true)} gün",
            8 => $"Yıllık izin kalan (kesin): {d:dd.MM.yyyy} itibarıyla {m(v, false)} gün",
            _ => $"Sezon kapama: {d:dd.MM.yyyy} gün sonu bakiyesi = {m(v, false)} gün"
        };
        if (!ArayuzYardimcisi.Onayla($"{Kart.AdSoyad}\n\n{ozet}" + (ack.Length > 0 ? $"\nAçıklama: {ack}" : "") + "\n\nKaydedilsin mi?", "İzin kartı")) return;
        var tc = Kart.TcKimlikNo;
        try
        {
            switch (t)
            {
                case 0: await _servis.KesinBakiyeKaydetAsync(tc, d, v, ack.Length > 0 ? ack : "Manuel kesin bakiye", Kullanici); break;
                case 1: await _servis.HareketKaydetAsync(tc, d, "HTDUZELTME", v, null, ack, Kullanici); break;
                case 2: await _servis.HareketKaydetAsync(tc, d, "RTKULLANIM", v, IslemTatil, ack, Kullanici); break;
                case 3: await _servis.HareketKaydetAsync(tc, d, "RTEK", v, null, ack, Kullanici); break;
                case 4: await _servis.KesintiKaydetAsync(tc, "G", d.Year, d.Month, v, Kullanici); break;
                case 5: await _servis.KesintiKaydetAsync(tc, "H", d.Year, d.Month, v, Kullanici); break;
                case 6: await _servis.YillikIzinKullanimiKaydetAsync(tc, d, IslemBitis is { } bt ? DateOnly.FromDateTime(bt) : null, v, ack, Kullanici); break;
                case 7: await _servis.HareketKaydetAsync(tc, d, "YIDEVIR", v, null, ack, Kullanici); break;
                case 8: await _servis.HareketKaydetAsync(tc, d, "YISABIT", v, null, ack, Kullanici); break;
                case 9: await _servis.TopluKesinBakiyeAsync([tc], d, v, ack.Length > 0 ? ack : "Sezon kapama " + d.Year, Kullanici); break;
            }
            IslemGun = null; IslemAciklama = null; IslemBitis = null;
            Mesaj = $"{Kart.AdSoyad}: {ozet} kaydedildi. Yanlışsa İşlem günlüğü sekmesinden geri alabilirsiniz.";
            await KisiYenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    // ------------------------------------------------------------------ manuel kayıt iptal / yeniden etkin (VBA btnIKSil)

    [RelayCommand]
    private async Task ManuelIptalAsync()
    {
        if (SeciliSatir is null || SeciliManuel is not { } k) { Mesaj = "Manuel kayıtlar listesinden bir kayıt seçin."; return; }
        var metin = $"{k.Tarih:dd.MM.yyyy} {k.Tur} {k.Deger}" + (string.IsNullOrWhiteSpace(k.Aciklama) ? "" : " - " + k.Aciklama);
        try
        {
            if (k.Iptal)
            {
                if (!ArayuzYardimcisi.Onayla(metin + "\n\nBu kayıt iptal edilmiş. Yeniden etkinleştirilsin mi?", "İzin kartı")) return;
                await _servis.ManuelGeriYukleAsync(SeciliSatir.TcKimlikNo, k.Anahtar, metin, Kullanici);
                Mesaj = "Kayıt yeniden etkinleştirildi.";
            }
            else
            {
                if (!ArayuzYardimcisi.Onayla(metin + "\n\nBu kayıt iptal edilsin mi? (silinmez, geri alınabilir)", "İzin kartı")) return;
                await _servis.ManuelKaldirAsync(SeciliSatir.TcKimlikNo, k.Anahtar, metin, Kullanici);
                Mesaj = "Kayıt iptal edildi.";
            }
            await KisiYenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task GeriAlAsync()
    {
        if (SeciliIslem is not { } l) { Mesaj = "İşlem günlüğünden geri alınacak işlemi seçin."; return; }
        if (!l.GeriAlinabilir) { Mesaj = "Bu işlem zaten geri alınmış."; return; }
        if (!ArayuzYardimcisi.Onayla($"{l.Zaman:dd.MM.yyyy HH:mm} — {l.Islem}\n\nBu işlem geri alınsın mı?", "Geri al")) return;
        try
        {
            await _servis.GeriAlAsync(l.Id, Kullanici);
            Mesaj = "İşlem geri alındı.";
            await KisiYenileAsync();
        }
        catch (Exception ex) { Mesaj = "Geri alınamadı: " + ex.Message; }
    }

    // ------------------------------------------------------------------ kişi ayarları

    [RelayCommand]
    private async Task HaftaGunuKaydetAsync()
    {
        if (SeciliSatir is null) return;
        var gun = string.Concat(HaftaGunleri.Where(g => g.Secili).Select(g => (char)('0' + g.No)));
        try
        {
            await _servis.HaftaGunuKaydetAsync(SeciliSatir.TcKimlikNo, gun, Kullanici);
            Mesaj = "İzin günü kaydedildi: " + IzinBakiyeHesaplayici.GunMetni(gun, Politika);
            await KisiYenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task TopluHaftaGunuAsync(System.Collections.IList? secililer)
    {
        var tcler = (secililer?.OfType<BakiyeSatiri>() ?? Enumerable.Empty<BakiyeSatiri>()).Select(s => s.TcKimlikNo).ToList();
        if (tcler.Count < 2) { Mesaj = "Toplu izin günü için listeden birden çok personel seçin (Ctrl veya Shift ile)."; return; }
        var gun = string.Concat(HaftaGunleri.Where(g => g.Secili).Select(g => (char)('0' + g.No)));
        if (!ArayuzYardimcisi.Onayla($"{tcler.Count} personelin izin günü \"{IzinBakiyeHesaplayici.GunMetni(gun, Politika)}\" yapılacak. Devam edilsin mi?", "Toplu izin günü")) return;
        try
        {
            await _servis.TopluHaftaGunuAsync(tcler, gun, Kullanici);
            Mesaj = $"{tcler.Count} personelin izin günü kaydedildi (tek işlemle geri alınabilir).";
            await KisiYenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task RtDahilKaydetAsync()
    {
        if (SeciliSatir is null) return;
        try
        {
            await _servis.RtDahilKaydetAsync(SeciliSatir.TcKimlikNo, KartRtDahil, Kullanici);
            Mesaj = "Resmi tatiller bakiyeye " + (KartRtDahil ? "dahil edildi." : "dahil edilmedi.");
            await KisiYenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task DogumKaydetAsync()
    {
        if (SeciliSatir is null) return;
        try
        {
            await _servis.DogumTarihiKaydetAsync(SeciliSatir.TcKimlikNo, KartDogum is { } d ? DateOnly.FromDateTime(d) : null, Kullanici);
            Mesaj = "Doğum tarihi kaydedildi (yıllık izinde 50 yaş / 18 yaş kuralı için).";
            await KisiYenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task PolitikaKaydetAsync()
    {
        try
        {
            await _servis.PolitikaKaydetAsync(Politika, Kullanici);
            Mesaj = "Genel hafta tatili kuralı: " + IzinBakiyeHesaplayici.PolitikaMetni(Politika) + ". Bakiyeler yeniden hesaplandı.";
            await KisiYenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    // ------------------------------------------------------------------ sezon kapama (VBA btnBSezon)

    private async Task SezonDurumuAsync()
    {
        try
        {
            var son = (await _servis.IslemListesiAsync("SEZON", 50)).FirstOrDefault(l => l.GeriAlinabilir);
            SezonSonIslem = son is null ? "" : $"Son sezon kapama: {son.Islem} ({son.Zaman:dd.MM.yyyy HH:mm})";
        }
        catch { SezonSonIslem = ""; }
    }

    [RelayCommand]
    private async Task SezonKapatAsync()
    {
        if (SezonTarih is not { } t) { Mesaj = "Sezon kapama tarihini seçin."; return; }
        var d = DateOnly.FromDateTime(t);
        if (d > DateOnly.FromDateTime(DateTime.Today)) { Mesaj = "Gelecek tarihe kapama yapılamaz."; return; }
        if (!SayiOku(SezonBakiye, out var deger)) { Mesaj = "Bakiye için sayı yazın."; return; }
        List<string> tcler;
        string kapsam;
        if (SezonYalnizSecili)
        {
            if (SeciliSatir is null) { Mesaj = "Seçili personel yok."; return; }
            tcler = [SeciliSatir.TcKimlikNo];
            kapsam = SeciliSatir.AdSoyad;
        }
        else
        {
            tcler = Satirlar.Select(s => s.TcKimlikNo).ToList();
            kapsam = $"Listedeki {tcler.Count} personel";
        }
        if (tcler.Count == 0) { Mesaj = "Önce listeyi doldurun (tesis seçin)."; return; }
        var ack = string.IsNullOrWhiteSpace(SezonAciklama) ? "Sezon kapama" : SezonAciklama!.Trim();
        if (!ArayuzYardimcisi.Onayla($"{kapsam}: {d:dd.MM.yyyy} gün sonu bakiyesi {IzinBakiyeHesaplayici.Metin(deger, false)} yapılacak.\n\n" +
                                     (tcler.Count > 1 ? "Farklı olanları (örneğin devreden 5 gün) sonra izin kartından tek tek düzeltebilirsiniz.\n" : "") +
                                     "Yanlış olursa \"Son sezon kapamayı geri al\" ile geri alabilirsiniz. Devam edilsin mi?", "Sezon kapama")) return;
        try
        {
            await _servis.TopluKesinBakiyeAsync(tcler, d, deger, ack, Kullanici);
            Mesaj = $"{kapsam} için sezon kapama kaydedildi.";
            await KisiYenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task SezonGeriAlAsync()
    {
        try
        {
            var son = (await _servis.IslemListesiAsync("SEZON", 50)).FirstOrDefault(l => l.GeriAlinabilir);
            if (son is null) { Mesaj = "Geri alınacak sezon kapama yok."; return; }
            if (!ArayuzYardimcisi.Onayla($"{son.Islem}\n({son.Zaman:dd.MM.yyyy HH:mm})\n\nBu sezon kapama geri alınsın mı?", "Sezon kapama")) return;
            await _servis.GeriAlAsync(son.Id, Kullanici);
            Mesaj = "Sezon kapama geri alındı.";
            await KisiYenileAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    // ------------------------------------------------------------------ izin süresi hesaplama (VBA BK_IzinBitisHesapla)

    [RelayCommand]
    private async Task IzinSuresiHesaplaAsync()
    {
        HesapGunleri.Clear();
        if (HesapBaslangic is not { } b || !int.TryParse(HesapIsGunu, NumberStyles.Integer, Tr, out var n) || n < 1 || n > 60)
        { HesapSonuc = "Başlangıç tarihi ve 1-60 arası iş günü girin."; return; }
        try
        {
            var r = await _servis.IzinBitisHesaplaAsync(DateOnly.FromDateTime(b), n);
            if (r is null) return;
            HesapSonuc = $"{n} gün izin: son gün {r.SonGun:dd.MM.yyyy dddd}, işbaşı {r.IseBaslama:dd.MM.yyyy dddd}. " +
                         $"Arada {r.Pazar} pazar ve {r.ResmiTatil} resmi tatil; toplam {r.TakvimGunu} takvim günü.";
            foreach (var (g, a) in r.Gunler) HesapGunleri.Add($"{g.ToString("dd.MM.yyyy ddd", Tr)}  {a}");
        }
        catch (Exception ex) { HesapSonuc = ex.Message; }
    }

    [RelayCommand]
    private void IzinSuresiniForma()
    {
        if (HesapBaslangic is null || !int.TryParse(HesapIsGunu, out var n)) return;
        IslemTuru = 6;
        IslemTarih = HesapBaslangic;
        IslemGun = n.ToString(Tr);
        Mesaj = "Yıllık izin kullanımı formu dolduruldu; kontrol edip Kaydet'e basın.";
    }

    // ------------------------------------------------------------------ kontroller ve dışa aktarım

    [RelayCommand]
    private async Task CiftDusumAsync()
    {
        try
        {
            var l = await _servis.CiftDusumSupheleriAsync();
            if (l.Count == 0) { Mesaj = "Çift düşüm şüphesi yok: elle girilen yıllık izinlerin tarihlerinde puantajda Yİ bulunmadı."; return; }
            Mesaj = $"{l.Count} olası çift düşüm (aynı izin hem puantajda Yİ hem izin kartında elle girilmiş): " +
                    string.Join("; ", l.Take(8).Select(x => $"{x.AdSoyad} {x.Tarih:dd.MM.yyyy}-{x.Bitis:dd.MM.yyyy} ({x.PuantajYIGunu} gün Yİ)")) +
                    (l.Count > 8 ? " …" : "") + ". Veri değiştirilmedi; izin kartından düzeltin.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private void Excel()
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("İzin ve Bakiye");
        string[] basliklar = ["Ad Soyad", "Tesis", "İzin günü", "Güncel bakiye", "Hafta tatili", "Resmi tatil", "RT dahil", "Yıllık izin hak", "Yıllık izin kullanılan", "Yıllık izin kalan", "Kesin bakiye tarihi", "Uyarı", "Durum", "TCKN"];
        for (var i = 0; i < basliklar.Length; i++) ws.Cell(1, i + 1).Value = basliklar[i];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var s in Satirlar)
        {
            var k = s.S;
            ws.Cell(r, 1).Value = k.AdSoyad;
            ws.Cell(r, 2).Value = k.Tesis;
            ws.Cell(r, 3).Value = k.HaftaGunMetni;
            if (k.Toplam is { } t) ws.Cell(r, 4).Value = t;
            if (k.HT is { } h) ws.Cell(r, 5).Value = h;
            if (k.RT is { } rt) ws.Cell(r, 6).Value = rt;
            ws.Cell(r, 7).Value = k.RtDahil ? "Evet" : "Hayır";
            if (k.YIHak is { } yh) ws.Cell(r, 8).Value = yh;
            ws.Cell(r, 9).Value = k.YIKullanilan;
            if (k.YIKalan is { } yk) ws.Cell(r, 10).Value = yk;
            if (k.KesinBakiyeTarihi is { } kb) { ws.Cell(r, 11).Value = kb.ToDateTime(TimeOnly.MinValue); ws.Cell(r, 11).Style.DateFormat.Format = "dd.MM.yyyy"; }
            ws.Cell(r, 12).Value = k.Uyari;
            ws.Cell(r, 13).Value = s.Durum;
            ws.Cell(r, 14).Value = k.TcKimlikNo;
            r++;
        }
        ws.Range(2, 4, Math.Max(2, r - 1), 10).Style.NumberFormat.Format = "#,##0.0";
        ws.Columns().AdjustToContents(1, Math.Min(r, 300), 8, 50);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var yol = ArayuzYardimcisi.Kaydet(ms.ToArray(), $"Izin-ve-Bakiye-{DateTime.Now:yyyyMMdd}.xlsx");
        if (yol is not null) Mesaj = $"Kaydedildi: {yol}";
    }

    [RelayCommand]
    private void DefterExcel()
    {
        if (Kart is null) return;
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Defter");
        ws.Cell(1, 1).Value = $"{Kart.AdSoyad} — izin hareket defteri ({DateTime.Today:dd.MM.yyyy})";
        ws.Cell(1, 1).Style.Font.Bold = true;
        string[] b = ["Tarih", "Açıklama", "Etki", "Bakiye"];
        for (var i = 0; i < b.Length; i++) ws.Cell(3, i + 1).Value = b[i];
        ws.Row(3).Style.Font.Bold = true;
        var r = 4;
        foreach (var d in _defterTum)
        {
            ws.Cell(r, 1).Value = d.Tarih.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 1).Style.DateFormat.Format = "dd.MM.yyyy";
            ws.Cell(r, 2).Value = d.Aciklama;
            ws.Cell(r, 3).Value = d.EtkiMetni;
            ws.Cell(r, 4).Value = d.Bakiye;
            r++;
        }
        ws.Columns().AdjustToContents(1, Math.Min(r, 300), 8, 80);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var yol = ArayuzYardimcisi.Kaydet(ms.ToArray(), $"Izin-Defteri-{Kart.AdSoyad}-{DateTime.Now:yyyyMMdd}.xlsx");
        if (yol is not null) Mesaj = $"Kaydedildi: {yol}";
    }

    public string Kurallar => IzinBakiyeKurallari.Metin(Politika);
}
