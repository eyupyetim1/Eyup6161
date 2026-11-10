using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PayControl.Application.Dtos;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Enums;
using PayControl.Domain.Services;

namespace PayControl.Desktop.ViewModels;

public sealed record SecenekOgesi<T>(string Ad, T? Deger);

/// <summary>Personel detayındaki Hesap Ekstresi sekmesi.</summary>
public partial class HesapEkstresiPanelModel : ObservableObject
{
    private readonly IHesapEkstresiServisi _servis;
    private readonly IMaliHareketServisi _defter;
    private readonly IBankaOdemeServisi _banka;
    private readonly OturumServisi _oturum;
    private Guid _personelId;

    [ObservableProperty] private EkstreOzeti? _ozet;
    [ObservableProperty] private EkstreOzeti? _genelOzet;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private string? _filtreAciklamasi;

    // Filtreler
    [ObservableProperty] private DateTime? _baslangic;
    [ObservableProperty] private DateTime? _bitis;
    [ObservableProperty] private string? _sirket;
    [ObservableProperty] private string? _isyeri;
    [ObservableProperty] private SecenekOgesi<MaliIslemTuru?>? _seciliIslemTuru;
    [ObservableProperty] private SecenekOgesi<Guid?>? _seciliCalismaDonemi;

    // Manuel işlem girişi
    [ObservableProperty] private string? _girisTuru;
    [ObservableProperty] private DateTime _girisTarihi = DateTime.Today;
    [ObservableProperty] private int _girisDonemYil = DateTime.Today.Year;
    [ObservableProperty] private int _girisDonemAy = DateTime.Today.Month;
    [ObservableProperty] private decimal _girisTutar;
    [ObservableProperty] private string? _girisAciklama;
    [ObservableProperty] private string? _girisBelge;

    [ObservableProperty] private MaliHareketDto? _seciliHareket;
    [ObservableProperty] private string? _tersKayitNedeni;

    public ObservableCollection<MaliHareketDto> Hareketler { get; } = new();
    public ObservableCollection<DonemBakiyesi> DonemBakiyeleri { get; } = new();
    public ObservableCollection<CalismaDonemiOzetDto> CalismaDonemiOzetleri { get; } = new();
    public ObservableCollection<SecenekOgesi<Guid?>> CalismaDonemiSecenekleri { get; } = new();

    public IReadOnlyList<SecenekOgesi<MaliIslemTuru?>> IslemTuruSecenekleri { get; } =
        new[] { new SecenekOgesi<MaliIslemTuru?>("Tüm işlemler", null) }
            .Concat(Enum.GetValues<MaliIslemTuru>().Select(t => new SecenekOgesi<MaliIslemTuru?>(MaliHareketKurallari.TurEtiketi(t), t)))
            .ToList();

    public IReadOnlyList<string> GirisTurleri { get; } =
    [
        "Kasa ödemesi", "Prim (bordro dışı)", "İkramiye (bordro dışı)", "Fazla mesai (bordro dışı)",
        "Kıdem tazminatı (bordro dışı)", "İhbar tazminatı (bordro dışı)", "İzin ücreti (bordro dışı)",
        "İade alındı (kesin geri ödemeden)", "İade alındı (şüpheli fazla ödemeden)",
        "Devreden alacak", "Devreden borç (geri ödeme)", "Fazla ödemeyi kesinleştir"
    ];

    public HesapEkstresiPanelModel(IHesapEkstresiServisi servis, IMaliHareketServisi defter, IBankaOdemeServisi banka, OturumServisi oturum)
    {
        _servis = servis;
        _defter = defter;
        _banka = banka;
        _oturum = oturum;
        _seciliIslemTuru = IslemTuruSecenekleri[0];
    }

    private string Kullanici => _oturum.KullaniciAdi ?? "sistem";

    public async Task YukleAsync(Guid personelId)
    {
        _personelId = personelId;
        await UygulaAsync();
    }

    private HesapEkstresiFiltre Filtre() => new()
    {
        Baslangic = Baslangic.HasValue ? DateOnly.FromDateTime(Baslangic.Value) : null,
        Bitis = Bitis.HasValue ? DateOnly.FromDateTime(Bitis.Value) : null,
        Sirket = string.IsNullOrWhiteSpace(Sirket) ? null : Sirket,
        Isyeri = string.IsNullOrWhiteSpace(Isyeri) ? null : Isyeri,
        IslemTuru = SeciliIslemTuru?.Deger,
        CalismaDonemiId = SeciliCalismaDonemi?.Deger
    };

    [RelayCommand]
    private async Task UygulaAsync()
    {
        if (_personelId == Guid.Empty) return;
        try
        {
            var e = await _servis.GetirAsync(_personelId, Filtre());
            Ozet = e.Ozet;
            GenelOzet = e.GenelOzet;
            FiltreAciklamasi = e.FiltreAciklamasi;
            Doldur(Hareketler, e.Hareketler);
            Doldur(DonemBakiyeleri, e.DonemBakiyeleri);
            Doldur(CalismaDonemiOzetleri, e.CalismaDonemiOzetleri);

            var secili = SeciliCalismaDonemi?.Deger;
            CalismaDonemiSecenekleri.Clear();
            CalismaDonemiSecenekleri.Add(new SecenekOgesi<Guid?>("Tüm çalışma dönemleri", null));
            foreach (var c in e.CalismaDonemiOzetleri)
                CalismaDonemiSecenekleri.Add(new SecenekOgesi<Guid?>(c.Etiket, c.Donem.Id));
            SeciliCalismaDonemi = CalismaDonemiSecenekleri.FirstOrDefault(c => c.Deger == secili) ?? CalismaDonemiSecenekleri[0];
        }
        catch (Exception ex)
        {
            Mesaj = ex.Message;
        }
    }

    [RelayCommand]
    private async Task FiltreTemizleAsync()
    {
        Baslangic = null;
        Bitis = null;
        Sirket = null;
        Isyeri = null;
        SeciliIslemTuru = IslemTuruSecenekleri[0];
        SeciliCalismaDonemi = null;
        await UygulaAsync();
    }

    [RelayCommand]
    private async Task KaydetAsync()
    {
        if (_personelId == Guid.Empty || string.IsNullOrWhiteSpace(GirisTuru))
        {
            Mesaj = "İşlem türünü seçin.";
            return;
        }

        try
        {
            var tarih = DateOnly.FromDateTime(GirisTarihi);
            var aciklama = GirisAciklama ?? "";
            switch (GirisTuru)
            {
                case "Kasa ödemesi":
                    await _banka.KasaOdemesiKaydetAsync(_personelId, tarih, GirisTutar, GirisDonemYil, GirisDonemAy, aciklama, GirisBelge, Kullanici);
                    break;
                case "Prim (bordro dışı)": await HakEdis(MaliIslemTuru.Prim); break;
                case "İkramiye (bordro dışı)": await HakEdis(MaliIslemTuru.Ikramiye); break;
                case "Fazla mesai (bordro dışı)": await HakEdis(MaliIslemTuru.FazlaMesai); break;
                case "Kıdem tazminatı (bordro dışı)": await HakEdis(MaliIslemTuru.KidemTazminati); break;
                case "İhbar tazminatı (bordro dışı)": await HakEdis(MaliIslemTuru.IhbarTazminati); break;
                case "İzin ücreti (bordro dışı)": await HakEdis(MaliIslemTuru.IzinUcreti); break;
                case "İade alındı (kesin geri ödemeden)":
                    await _defter.IadeAlAsync(_personelId, tarih, GirisTutar, BakiyeTuru.KesinlesmisGeriOdeme, aciklama, GirisBelge, Kullanici);
                    break;
                case "İade alındı (şüpheli fazla ödemeden)":
                    await _defter.IadeAlAsync(_personelId, tarih, GirisTutar, BakiyeTuru.SupheliFazlaOdeme, aciklama, GirisBelge, Kullanici);
                    break;
                case "Devreden alacak":
                    await _defter.DevredenBakiyeAsync(_personelId, false, BakiyeTuru.MaasAlacagi, tarih, GirisDonemYil, GirisDonemAy, GirisTutar, aciklama, GirisBelge, Kullanici);
                    break;
                case "Devreden borç (geri ödeme)":
                    await _defter.DevredenBakiyeAsync(_personelId, true, BakiyeTuru.KesinlesmisGeriOdeme, tarih, null, null, GirisTutar, aciklama, GirisBelge, Kullanici);
                    break;
                case "Fazla ödemeyi kesinleştir":
                    await _defter.FazlaOdemeKesinlestirAsync(_personelId, GirisTutar, aciklama, Kullanici);
                    break;
            }
            Mesaj = $"{GirisTuru} kaydedildi.";
            GirisTutar = 0;
            GirisAciklama = null;
            GirisBelge = null;
            await UygulaAsync();
        }
        catch (Exception ex)
        {
            Mesaj = ex.Message;
        }

        Task HakEdis(MaliIslemTuru tur) => _defter.BordroDisiHakEdisAsync(_personelId, tur, DateOnly.FromDateTime(GirisTarihi),
            GirisDonemYil, GirisDonemAy, GirisTutar, GirisAciklama ?? "", GirisBelge, Kullanici);
    }

    [RelayCommand]
    private async Task TersKayitAsync()
    {
        if (SeciliHareket is null)
        {
            Mesaj = "Ters kaydı alınacak hareketi seçin.";
            return;
        }
        try
        {
            await _defter.TersKayitAsync(SeciliHareket.Id, TersKayitNedeni ?? "", Kullanici);
            Mesaj = "Ters kayıt oluşturuldu; orijinal hareket korunuyor.";
            TersKayitNedeni = null;
            await UygulaAsync();
        }
        catch (Exception ex)
        {
            Mesaj = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ExcelAktarAsync()
    {
        if (_personelId == Guid.Empty) return;
        var dlg = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = "hesap-ekstresi.xlsx" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            await File.WriteAllBytesAsync(dlg.FileName, await _servis.ExcelOlarakAktarAsync(_personelId, Filtre()));
            Mesaj = "Excel kaydedildi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task PdfAktarAsync()
    {
        if (_personelId == Guid.Empty) return;
        var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = "hesap-ekstresi.pdf" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            await File.WriteAllBytesAsync(dlg.FileName, await _servis.PdfOlarakAktarAsync(_personelId, Filtre()));
            Mesaj = "PDF kaydedildi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private static void Doldur<T>(ObservableCollection<T> hedef, IEnumerable<T> kaynak)
    {
        hedef.Clear();
        foreach (var x in kaynak) hedef.Add(x);
    }
}
