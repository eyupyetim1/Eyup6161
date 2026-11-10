using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Entities;
using PayControl.Infrastructure.Import;

namespace PayControl.Desktop.ViewModels;

/// <summary>
/// Bankaya göndermeden önce: 1) bordro dosyası 2) banka ödeme dosyası 3) karşılaştırma 4) düzeltme/istisna
/// 5) onaylı liste. Program bankaya ödeme göndermez.
/// </summary>
public partial class OdemeKontrolViewModel : ObservableObject
{
    private readonly IOdemeOncesiKontrolServisi _servis;
    private readonly IBordroIceAktarmaServisi _bordro;
    private readonly OturumServisi _oturumServisi;

    [ObservableProperty] private int _donemYil = DateTime.Today.Year;
    [ObservableProperty] private int _donemAy = DateTime.Today.Month;
    [ObservableProperty] private string? _bordroDosyaYolu;
    [ObservableProperty] private string? _bankaDosyaYolu;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private string? _ozetMetni;
    [ObservableProperty] private OdemeKontrolOturumu? _kontrolOturumu;
    [ObservableProperty] private OdemeKontrolSatiri? _seciliSatir;
    [ObservableProperty] private string? _istisnaGerekce;

    // Düzeltme
    [ObservableProperty] private string? _duzeltIban;
    [ObservableProperty] private decimal? _duzeltTutar;
    [ObservableProperty] private bool _duzeltHaric;
    [ObservableProperty] private string? _duzeltGerekce;

    [ObservableProperty] private string _satirSayisi = "—";
    [ObservableProperty] private string _uygunSayisi = "—";
    [ObservableProperty] private string _kritikSayisi = "—";
    [ObservableProperty] private string _istisnaSayisi = "—";
    [ObservableProperty] private string _bankaToplami = "—";
    [ObservableProperty] private string? _bankaToplamAlt;
    [ObservableProperty] private IReadOnlyList<AsamaOgesi> _adimlar = Array.Empty<AsamaOgesi>();

    public ObservableCollection<OdemeKontrolSatiri> Satirlar { get; } = new();
    public ObservableCollection<OnayliOdemeListesi> Listeler { get; } = new();

    public ObservableCollection<string> Tesisler { get; } = new() { TumTesisler };
    private const string TumTesisler = "Tüm tesisler";
    [ObservableProperty] private string _seciliTesis = TumTesisler;

    public OdemeKontrolViewModel(IOdemeOncesiKontrolServisi servis, IBordroIceAktarmaServisi bordro, ITesisServisi tesis, OturumServisi oturum)
    {
        _servis = servis;
        _bordro = bordro;
        _oturumServisi = oturum;
        AdimlariGuncelle();
        _ = TesisleriYukleAsync(tesis);
    }

    private async Task TesisleriYukleAsync(ITesisServisi tesis)
    {
        try
        {
            foreach (var t in (await tesis.ListeleAsync()).Where(t => t.Tesis.Aktif).Select(t => t.Tesis.Ad)) Tesisler.Add(t);
        }
        catch { /* tesis listesi alınamazsa tüm tesisler ile çalışılır */ }
    }

    partial void OnBordroDosyaYoluChanged(string? value) => AdimlariGuncelle();
    partial void OnBankaDosyaYoluChanged(string? value) => AdimlariGuncelle();

    private void AdimlariGuncelle()
    {
        var oturumVar = KontrolOturumu is not null;
        var kritik = Satirlar.Count(s => s.KritikMi && !s.OnayliListeyeAlinabilir);
        var listeVar = Listeler.Any(l => l.Gecerli && KontrolOturumu is not null && l.OdemeKontrolOturumuId == KontrolOturumu.Id);
        var durumlar = new[]
        {
            ("Bordro", !string.IsNullOrWhiteSpace(BordroDosyaYolu) || KontrolOturumu?.BordroDosyaAdi is not null, "Ayın bordro dosyası (boş bırakılırsa dönemin son bordrosu)"),
            ("Banka dosyası", !string.IsNullOrWhiteSpace(BankaDosyaYolu) || oturumVar, "Bankaya gönderilecek ödeme Excel'i"),
            ("Kontrol", oturumVar, "Bordro, banka, personel kartı ve geçmiş işlemler karşılaştırılır"),
            ("İnceleme", oturumVar && kritik == 0, "Kritik satırlar düzeltilir veya gerekçeli istisna verilir"),
            ("Onaylı liste", listeVar, "Banka formatında onaylı dosya üretilir")
        };
        var aktifBulundu = false;
        Adimlar = durumlar.Select((d, i) =>
        {
            string durum;
            if (d.Item2) durum = "tamam";
            else if (!aktifBulundu) { durum = "aktif"; aktifBulundu = true; }
            else durum = "bekliyor";
            return new AsamaOgesi(i + 1, d.Item1, durum, i == durumlar.Length - 1, d.Item3);
        }).ToList();
    }

    private string Kullanici => _oturumServisi.KullaniciAdi ?? "sistem";

    partial void OnSeciliSatirChanged(OdemeKontrolSatiri? value)
    {
        DuzeltIban = value?.BankaIban;
        DuzeltTutar = value?.BankaOdemeTutari;
        DuzeltHaric = false;
    }

    [RelayCommand]
    private void BordroSec()
    {
        var dlg = new OpenFileDialog { Filter = "Excel|*.xlsx;*.xlsm|Tümü|*.*", Title = "Ayın bordro dosyası" };
        if (dlg.ShowDialog() == true) BordroDosyaYolu = dlg.FileName;
    }

    [RelayCommand]
    private void DosyaSec()
    {
        var dlg = new OpenFileDialog { Filter = "Excel|*.xlsx;*.xlsm|Tümü|*.*", Title = "Bankaya gönderilecek ödeme dosyası" };
        if (dlg.ShowDialog() == true) BankaDosyaYolu = dlg.FileName;
    }

    [RelayCommand]
    private async Task KontrolEtAsync()
    {
        Mesaj = null;
        if (string.IsNullOrWhiteSpace(BankaDosyaYolu) || !File.Exists(BankaDosyaYolu))
        {
            Mesaj = "Bankaya gönderilecek ödeme Excel dosyasını seçin.";
            return;
        }

        try
        {
            var notlar = new List<string>();
            Guid? bordroId = null;
            if (!string.IsNullOrWhiteSpace(BordroDosyaYolu))
            {
                var bordroBytes = await File.ReadAllBytesAsync(BordroDosyaYolu);
                await using var ms1 = new MemoryStream(bordroBytes);
                var onizleme = await _bordro.OnizleAsync(ms1, Path.GetFileName(BordroDosyaYolu), DonemYil, DonemAy);
                if (onizleme.MukerrerDosya)
                {
                    bordroId = onizleme.OncekiIceAktarmaId;
                    notlar.Add("Bordro dosyası daha önce içe aktarılmış; mevcut kayıt kullanıldı.");
                }
                else
                {
                    await using var ms2 = new MemoryStream(bordroBytes);
                    var sonuc = await _bordro.AktarAsync(ms2, Path.GetFileName(BordroDosyaYolu), DonemYil, DonemAy, Kullanici);
                    bordroId = sonuc.IceAktarmaId;
                    notlar.Add(sonuc.Mesaj);
                }
            }

            var bytes = await File.ReadAllBytesAsync(BankaDosyaYolu);
            await using var ms = new MemoryStream(bytes);
            var dosya = BankaTalimatOkuyucu.OkuDosya(ms);
            notlar.AddRange(dosya.Uyarilar);

            KontrolOturumu = await _servis.KontrolBaslatAsync(
                DonemYil, DonemAy, dosya, Path.GetFileName(BankaDosyaYolu),
                Convert.ToHexString(SHA256.HashData(bytes)), Kullanici, bordroId,
                tesis: SeciliTesis == TumTesisler ? null : SeciliTesis);
            await GosterAsync(KontrolOturumu.Id);
            notlar.Insert(0, "Kontrol tamamlandı. Program bankaya ödeme göndermez.");
            Mesaj = string.Join(Environment.NewLine, notlar);
        }
        catch (Exception ex)
        {
            Mesaj = ex.Message;
        }
    }

    [RelayCommand]
    private async Task DuzeltAsync()
    {
        if (KontrolOturumu is null || SeciliSatir?.TalimatSatirNolari is null)
        {
            Mesaj = "Düzeltmek için banka talimatı olan bir satır seçin.";
            return;
        }
        try
        {
            var satirNo = int.Parse(SeciliSatir.TalimatSatirNolari.Split(',')[0]);
            await _servis.TalimatDuzeltAsync(KontrolOturumu.Id, satirNo, DuzeltIban, DuzeltHaric ? null : DuzeltTutar,
                DuzeltHaric, DuzeltGerekce ?? "", Kullanici);
            DuzeltGerekce = null;
            await GosterAsync(KontrolOturumu.Id);
            Mesaj = DuzeltHaric ? $"Banka satırı {satirNo} listeden çıkarıldı; kontrol yenilendi." : $"Banka satırı {satirNo} düzeltildi; kontrol yenilendi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task YenidenDegerlendirAsync()
    {
        if (KontrolOturumu is null) return;
        try
        {
            await _servis.YenidenDegerlendirAsync(KontrolOturumu.Id, Kullanici);
            await GosterAsync(KontrolOturumu.Id);
            Mesaj = "Kontrol güncel verilerle yenilendi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task IstisnaOnaylaAsync()
    {
        if (SeciliSatir is null || KontrolOturumu is null) return;
        try
        {
            await _servis.IstisnaOnaylaAsync(SeciliSatir.Id, IstisnaGerekce ?? "", Kullanici);
            IstisnaGerekce = null;
            await GosterAsync(KontrolOturumu.Id);
            Mesaj = "Gerekçeli istisna onayı kaydedildi (kullanıcı, zaman ve gerekçe denetim geçmişinde).";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task OnayliListeUretAsync()
    {
        if (KontrolOturumu is null) return;
        try
        {
            var klasor = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PayControl", "OnayliOdemeListeleri");
            var liste = await _servis.OnayliListeUretAsync(KontrolOturumu.Id, Kullanici, klasor);
            await GosterAsync(KontrolOturumu.Id);
            Mesaj = $"Onaylı liste oluşturuldu (v{liste.Surum}, {liste.SatirSayisi} satır, {liste.ToplamTutar:N2} TL): {liste.DosyaYolu}" +
                    Environment.NewLine + "Dosyayı bankaya kendiniz iletmelisiniz; PayControl ödeme göndermez.";
        }
        catch (Exception ex)
        {
            Mesaj = ex.Message;
            if (KontrolOturumu is not null) await GosterAsync(KontrolOturumu.Id);
        }
    }

    private async Task GosterAsync(Guid oturumId)
    {
        KontrolOturumu = await _servis.GetirAsync(oturumId);
        Satirlar.Clear();
        foreach (var s in KontrolOturumu!.Satirlar.OrderByDescending(x => x.KritikMi && !x.OnayliListeyeAlinabilir)
                     .ThenByDescending(x => x.KritikMi).ThenBy(x => x.AdSoyad))
            Satirlar.Add(s);
        Listeler.Clear();
        foreach (var l in await _servis.DonemListeleriAsync(KontrolOturumu.DonemYil, KontrolOturumu.DonemAy))
            Listeler.Add(l);

        var kritik = Satirlar.Count(s => s.KritikMi && !s.OnayliListeyeAlinabilir);
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        SatirSayisi = Satirlar.Count.ToString("N0", tr);
        UygunSayisi = Satirlar.Count(s => !s.KritikMi).ToString("N0", tr);
        KritikSayisi = kritik.ToString("N0", tr);
        IstisnaSayisi = Satirlar.Count(s => s.IstisnaOnayi).ToString("N0", tr);
        BankaToplami = KontrolOturumu.Talimatlar.Where(t => !t.Haric).Sum(t => t.Tutar).ToString("N2", tr);
        BankaToplamAlt = $"{KontrolOturumu.Talimatlar.Count(t => !t.Haric)} talimat · bordro {Satirlar.Sum(s => s.BordroNetIstihkak ?? 0).ToString("N2", tr)}";
        AdimlariGuncelle();
        OzetMetni = $"Bordro: {KontrolOturumu.BordroDosyaAdi ?? "-"} | Banka: {KontrolOturumu.BankaDosyaAdi} | {Satirlar.Count} satır | " +
                    $"uygun {Satirlar.Count(s => !s.KritikMi)} | çözülmemiş kritik {kritik} | istisna {Satirlar.Count(s => s.IstisnaOnayi)} | " +
                    $"belirsiz eşleşme {Satirlar.Count(s => s.EslesmeBelirsiz)}";
    }
}
