using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Entities;
using PayControl.Domain.Services;

namespace PayControl.Desktop.ViewModels;

/// <summary>Satırdaki günlük notlar: Binding "Not[5]".</summary>
public sealed class NotIndeksi
{
    private readonly Dictionary<int, string> _n;
    public NotIndeksi(Dictionary<int, string> n) => _n = n;
    public string? this[int gun] => _n.TryGetValue(gun, out var s) ? s : null;
}

public partial class PuantajSatirVM : ObservableObject
{
    private readonly int _yil, _ay;
    private readonly Dictionary<int, string> _kodlar;
    private readonly Dictionary<int, string> _notlar;

    public PuantajSatirVM(PuantajSatiri s, int yil, int ay)
    {
        _yil = yil; _ay = ay;
        TcKimlikNo = s.TcKimlikNo; AdSoyad = s.AdSoyad; Unvan = s.Unvan; PersonelId = s.PersonelId;
        _kodlar = new Dictionary<int, string>(s.Kodlar);
        _notlar = new Dictionary<int, string>(s.Notlar);
        Ozet = s.Ozet;
    }

    public Guid? PersonelId { get; }
    public string TcKimlikNo { get; }
    public string AdSoyad { get; }
    public string? Unvan { get; }
    [ObservableProperty] private AylikPuantajOzeti _ozet;
    public NotIndeksi Not => new(_notlar);

    public string this[int gun]
    {
        get => _kodlar.TryGetValue(gun, out var k) ? k : "";
        set
        {
            if (string.IsNullOrEmpty(value)) _kodlar.Remove(gun); else _kodlar[gun] = value;
            OnPropertyChanged("Item[]");
            Ozet = PuantajHesaplayici.Hesapla(_yil, _ay, _kodlar);
        }
    }

    public string? NotOku(int gun) => _notlar.TryGetValue(gun, out var s) ? s : null;

    public void NotYaz(int gun, string? not)
    {
        if (string.IsNullOrWhiteSpace(not)) _notlar.Remove(gun); else _notlar[gun] = not.Trim();
        OnPropertyChanged(nameof(Not));
    }
}

public sealed record KodDugmesi(string Kod, string Ad, string Renk, string YaziRengi)
{
    public string Ipucu => $"{Kod} — {Ad}";
}

public partial class PuantajViewModel : ObservableObject
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly IPuantajServisi _servis;
    private readonly OturumServisi _oturum;
    private readonly Dictionary<(string Tc, int Gun), string?> _degisiklikler = new();

    public PuantajViewModel(IPuantajServisi servis, OturumServisi oturum)
    {
        _servis = servis;
        _oturum = oturum;
        var bugun = DateTime.Today;
        Yil = bugun.Year;
        Ay = bugun.Month;
    }

    private string Kullanici => _oturum.KullaniciAdi ?? "";

    public IReadOnlyList<KodDugmesi> Kodlar { get; } = PuantajHesaplayici.Kodlar.Select(k => new KodDugmesi(k.Kod, k.Ad, k.Renk, k.YaziRengi)).ToList();
    public IReadOnlyList<Secenek<int>> Aylar { get; } = Enumerable.Range(1, 12).Select(a => new Secenek<int>(a, Tr.DateTimeFormat.GetMonthName(a))).ToList();
    public IReadOnlyList<int> Yillar { get; } = Enumerable.Range(DateTime.Today.Year - 6, 8).ToList();
    public ObservableCollection<string> Tesisler { get; } = new();
    public ObservableCollection<PuantajSatirVM> Satirlar { get; } = new();
    public ObservableCollection<ResmiTatil> Tatiller { get; } = new();

    [ObservableProperty] private string? _seciliTesis;
    [ObservableProperty] private int _yil;
    [ObservableProperty] private int _ay;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private string _durum = "";
    [ObservableProperty] private PuantajTablosu? _tablo;
    [ObservableProperty] private string? _seciliHucreMetni;
    [ObservableProperty] private string? _notMetni;
    [ObservableProperty] private string? _arama;

    // Resmi tatil formu
    [ObservableProperty] private int _tatilYili = DateTime.Today.Year;
    [ObservableProperty] private DateTime _yeniTatilTarihi = DateTime.Today;
    [ObservableProperty] private string? _yeniTatilAdi;
    [ObservableProperty] private bool _yeniTatilYarim;
    [ObservableProperty] private ResmiTatil? _seciliTatil;

    public (PuantajSatirVM Satir, int Gun)? SeciliHucre { get; private set; }
    public int KaydedilmemisSayisi => _degisiklikler.Count;

    /// <summary>Tablo yeniden kurulunca pencere gün sütunlarını oluşturur.</summary>
    public event Action? TabloYenilendi;

    partial void OnSeciliTesisChanged(string? value) { if (value is not null) _ = YukleAsync(); }
    partial void OnAyChanged(int value) => _ = YukleAsync();
    partial void OnYilChanged(int value) => _ = YukleAsync();
    partial void OnAramaChanged(string? value) => Suz();
    partial void OnTatilYiliChanged(int value) => _ = TatilleriYukleAsync();

    public async Task IlkYukleAsync()
    {
        try
        {
            Tesisler.Clear();
            foreach (var t in await _servis.TesislerAsync()) Tesisler.Add(t);
            await TatilleriYukleAsync();
            SeciliTesis ??= Tesisler.FirstOrDefault();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private bool _yukleniyor;

    [RelayCommand]
    private async Task YukleAsync()
    {
        if (string.IsNullOrWhiteSpace(SeciliTesis) || _yukleniyor) return;
        if (_degisiklikler.Count > 0 && !ArayuzYardimcisi.Onayla($"{_degisiklikler.Count} kaydedilmemiş değişiklik var. Kaydetmeden devam edilsin mi?")) return;
        _yukleniyor = true;
        try
        {
            _degisiklikler.Clear();
            Tablo = await _servis.TabloAsync(SeciliTesis, Yil, Math.Clamp(Ay, 1, 12));
            Suz();
            TabloYenilendi?.Invoke();
            DurumGuncelle();
            Mesaj = Tablo.Satirlar.Count == 0 ? $"{SeciliTesis} için {Ay:D2}/{Yil} döneminde personel yok." : null;
        }
        catch (Exception ex) { Mesaj = ex.Message; }
        finally { _yukleniyor = false; }
    }

    private void Suz()
    {
        Satirlar.Clear();
        if (Tablo is null) return;
        foreach (var s in Tablo.Satirlar.Where(s => AramaKurallari.Eslesir(Arama, s.AdSoyad, s.TcKimlikNo, s.Unvan)))
            Satirlar.Add(new PuantajSatirVM(s, Tablo.Yil, Tablo.Ay));
    }

    private void DurumGuncelle()
    {
        OnPropertyChanged(nameof(KaydedilmemisSayisi));
        Durum = Tablo is null ? "" :
            $"{Satirlar.Count} personel · {Tablo.Gunler.Count} gün · {Tablo.Gunler.Count(g => g.ResmiTatil is not null)} resmi tatil" +
            (_degisiklikler.Count > 0 ? $" · {_degisiklikler.Count} kaydedilmemiş değişiklik" : " · kaydedildi");
    }

    public void HucreSecildi(PuantajSatirVM? satir, int? gun)
    {
        SeciliHucre = satir is not null && gun is { } g ? (satir, g) : null;
        if (SeciliHucre is { } h && Tablo is not null)
        {
            var gb = Tablo.Gunler[h.Gun - 1];
            SeciliHucreMetni = $"{h.Satir.AdSoyad} · {gb.Tarih:dd MMMM yyyy, dddd}" + (gb.ResmiTatil is null ? "" : $" · {gb.ResmiTatil}");
            NotMetni = h.Satir.NotOku(h.Gun);
        }
        else { SeciliHucreMetni = null; NotMetni = null; }
    }

    /// <summary>Seçili hücrelere kod yazar (null/boş: siler).</summary>
    public void KodUygula(IEnumerable<(PuantajSatirVM Satir, int Gun)> hucreler, string? kod)
    {
        var n = 0;
        foreach (var (s, g) in hucreler)
        {
            if (s[g] == (kod ?? "")) continue;
            s[g] = kod ?? "";
            _degisiklikler[(s.TcKimlikNo, g)] = kod;
            n++;
        }
        if (n > 0) DurumGuncelle();
    }

    /// <summary>VBA "toplu giriş": seçili satırların boş günlerini doldurur — hafta içi Ç, pazar HT, resmi tatil RT.</summary>
    public void BoslariDoldur(IEnumerable<PuantajSatirVM> satirlar)
    {
        if (Tablo is null) return;
        var hucreler = new List<(PuantajSatirVM, int, string)>();
        foreach (var s in satirlar)
            foreach (var g in Tablo.Gunler)
                if (s[g.Gun].Length == 0)
                    hucreler.Add((s, g.Gun, g.ResmiTatil is not null && g.TatilHakki >= 1 ? "RT" : PuantajHesaplayici.HaftaGunu(g.Tarih) == 7 ? "HT" : "Ç"));
        foreach (var grup in hucreler.GroupBy(h => h.Item3))
            KodUygula(grup.Select(h => (h.Item1, h.Item2)), grup.Key);
        Mesaj = hucreler.Count == 0 ? "Doldurulacak boş gün yok." : $"{hucreler.Count} boş gün dolduruldu (kaydetmeyi unutmayın).";
    }

    [RelayCommand]
    private async Task KaydetAsync()
    {
        if (Tablo is null || _degisiklikler.Count == 0) { Mesaj = "Kaydedilecek değişiklik yok."; return; }
        try
        {
            var n = await _servis.KaydetAsync(Tablo.Tesis, Tablo.Yil, Tablo.Ay,
                _degisiklikler.Select(d => new PuantajDegisikligi(d.Key.Tc, d.Key.Gun, d.Value)).ToList(), Kullanici);
            _degisiklikler.Clear();
            DurumGuncelle();
            Mesaj = $"{n} hücre kaydedildi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task NotKaydetAsync()
    {
        if (SeciliHucre is not { } h || Tablo is null) { Mesaj = "Not için bir gün hücresi seçin."; return; }
        try
        {
            await _servis.NotKaydetAsync(Tablo.Tesis, h.Satir.TcKimlikNo, Tablo.Yil, Tablo.Ay, h.Gun, NotMetni, Kullanici);
            h.Satir.NotYaz(h.Gun, NotMetni);
            Mesaj = string.IsNullOrWhiteSpace(NotMetni) ? "Not silindi." : "Not kaydedildi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private void OncekiAy() { if (Ay == 1) { Yil--; Ay = 12; } else Ay--; }

    [RelayCommand]
    private void SonrakiAy() { if (Ay == 12) { Yil++; Ay = 1; } else Ay++; }

    private async Task TatilleriYukleAsync()
    {
        Tatiller.Clear();
        foreach (var t in await _servis.ResmiTatillerAsync(TatilYili)) Tatiller.Add(t);
    }

    [RelayCommand]
    private async Task TatilKaydetAsync()
    {
        try
        {
            await _servis.ResmiTatilKaydetAsync(DateOnly.FromDateTime(YeniTatilTarihi), YeniTatilAdi ?? "", YeniTatilYarim ? 0.5m : 1m, Kullanici);
            YeniTatilAdi = null;
            YeniTatilYarim = false;
            await TatilleriYukleAsync();
            Mesaj = "Resmi tatil kaydedildi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task TatilSilAsync()
    {
        if (SeciliTatil is null) return;
        if (!ArayuzYardimcisi.Onayla($"{SeciliTatil.Tarih:dd.MM.yyyy} {SeciliTatil.Ad} silinsin mi?")) return;
        try
        {
            await _servis.ResmiTatilSilAsync(SeciliTatil.Id, Kullanici);
            await TatilleriYukleAsync();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }
}
