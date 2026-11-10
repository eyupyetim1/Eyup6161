using System.Collections.ObjectModel;
using System.Security.Cryptography;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;
using PayControl.Infrastructure.Import;

namespace PayControl.Desktop.ViewModels;

public partial class DonemKapanisViewModel : ObservableObject
{
    private readonly IDonemKapanisServisi _servis;
    private readonly IBankaOdemeServisi _banka;
    private readonly IPersonelServisi _personel;
    private readonly OturumServisi _oturum;

    [ObservableProperty] private int _donemYil = DateTime.Today.Year;
    [ObservableProperty] private int _donemAy = DateTime.Today.Month;
    [ObservableProperty] private MaasDonemKapanisi? _donem;
    [ObservableProperty] private KapanisRaporuDto? _rapor;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private string? _kapanisNotu;
    [ObservableProperty] private string? _yenidenAcmaNedeni;
    [ObservableProperty] private string? _karsilastirmaMetni;
    [ObservableProperty] private DonemKontrolMaddesi? _seciliMadde;
    [ObservableProperty] private string? _maddeNotu;
    [ObservableProperty] private MutabakatFarki? _seciliFark;
    [ObservableProperty] private string? _farkAciklama;

    // Düzeltme
    [ObservableProperty] private string? _duzeltmeTc;
    [ObservableProperty] private string _duzeltmeAlan = "NetIstihkak";
    [ObservableProperty] private string? _duzeltmeEski;
    [ObservableProperty] private string? _duzeltmeYeni;
    [ObservableProperty] private string? _duzeltmeNeden;

    public ObservableCollection<DonemKontrolMaddesi> Maddeler { get; } = new();
    public ObservableCollection<MutabakatFarki> Farklar { get; } = new();
    public ObservableCollection<DonemDegisiklikDetayi> Degisiklikler { get; } = new();
    public ObservableCollection<string> Istisnalar { get; } = new();
    public ObservableCollection<DonemDuzeltmeKaydi> Duzeltmeler { get; } = new();

    public DonemKapanisViewModel(IDonemKapanisServisi servis, IBankaOdemeServisi banka, IPersonelServisi personel, OturumServisi oturum)
    {
        _servis = servis;
        _banka = banka;
        _personel = personel;
        _oturum = oturum;
    }

    private string Kullanici => _oturum.KullaniciAdi ?? "sistem";

    [RelayCommand]
    private async Task YukleAsync()
    {
        try
        {
            Donem = await _servis.GetOrCreateAsync(DonemYil, DonemAy);
            await _servis.OzetHesaplaAsync(Donem.Id); // kapatılmış dönemde değer değiştirmez
            await _servis.KontrolListesiDegerlendirAsync(Donem.Id);
            Rapor = await _servis.KapanisRaporuAsync(Donem.Id);

            Doldur(Maddeler, Rapor.KontrolMaddeleri);
            Doldur(Farklar, Rapor.Farklar);
            Doldur(Istisnalar, Rapor.Istisnalar);
            Doldur(Duzeltmeler, Rapor.Duzeltmeler);
            Doldur(Degisiklikler, Rapor.Karsilastirma?.Detaylar ?? []);
            KarsilastirmaMetni = string.Join(Environment.NewLine, Rapor.Karsilastirma?.Nedenler ?? []);
            Mesaj = $"Durum: {Rapor.Durum}" + (Rapor.DondurulmusVeri ? " — kapanış anındaki dondurulmuş değerler gösteriliyor." : "");
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task BankayaIletildiAsync()
    {
        if (Donem is null) return;
        await Calistir(() => _servis.DurumGuncelleAsync(Donem.Id, MaasDonemDurumu.BankaOdemesiBekleniyor, Kullanici),
            "Durum: Banka ödemesi bekleniyor.");
    }

    [RelayCommand]
    private async Task BankaHareketIceAktarAsync()
    {
        var dlg = new OpenFileDialog { Filter = "Excel|*.xlsx;*.xlsm", Title = "Gerçekleşen banka ödemeleri (hesap hareketi dökümü)" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var bytes = await File.ReadAllBytesAsync(dlg.FileName);
            await using var ms = new MemoryStream(bytes);
            var satirlar = BankaHareketOkuyucu.Oku(ms);
            var sonuc = await _banka.HareketleriIceAktarAsync(DonemYil, DonemAy, satirlar, Path.GetFileName(dlg.FileName),
                Convert.ToHexString(SHA256.HashData(bytes)), Kullanici);
            await YukleAsync();
            Mesaj = $"{sonuc.Eklenen} hareket eklendi, {sonuc.MukerrerAtlanan} mükerrer atlandı, {sonuc.EslesmeyenKayit} eşleşmeyen." +
                    (sonuc.Mesajlar.Count > 0 ? Environment.NewLine + string.Join(Environment.NewLine, sonuc.Mesajlar.Take(10)) : "");
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task MutabakatYapAsync()
    {
        if (Donem is null) return;
        try
        {
            var farklar = await _servis.MutabakatYapAsync(Donem.Id, Kullanici);
            await YukleAsync();
            Mesaj = $"Mutabakat tamamlandı: {farklar.Count(f => f.Durum == FarkDurumu.Acik)} açık fark.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private Task FarkCozAsync() => FarkKapat(FarkDurumu.Cozuldu);

    [RelayCommand]
    private Task FarkIstisnaAsync() => FarkKapat(FarkDurumu.Istisna);

    private async Task FarkKapat(FarkDurumu durum)
    {
        if (SeciliFark is null) { Mesaj = "Fark seçin."; return; }
        await Calistir(() => _servis.FarkCozAsync(SeciliFark.Id, durum, FarkAciklama ?? "", Kullanici),
            durum == FarkDurumu.Istisna ? "Gerekçeli istisna kaydedildi." : "Fark çözüldü olarak işaretlendi.");
        FarkAciklama = null;
    }

    [RelayCommand]
    private async Task MaddeTamamlaAsync()
    {
        if (SeciliMadde is null) return;
        await Calistir(() => _servis.MaddeTamamlaAsync(SeciliMadde.Id, Kullanici, MaddeNotu), "Madde tamamlandı.");
        MaddeNotu = null;
    }

    [RelayCommand]
    private Task KapatAsync() => Donem is null ? Task.CompletedTask
        : Calistir(() => _servis.KapatAsync(Donem.Id, Kullanici, KapanisNotu), "Dönem kapatıldı. Kayıtlar kilitlendi; düzeltmeler düzeltme kaydı ile yapılır.");

    [RelayCommand]
    private Task YenidenAcAsync() => Donem is null ? Task.CompletedTask
        : Calistir(() => _servis.YenidenAcAsync(Donem.Id, Kullanici, YenidenAcmaNedeni ?? ""), "Dönem gerekçeyle yeniden açıldı (denetim geçmişine yazıldı).");

    [RelayCommand]
    private async Task DuzeltmeEkleAsync()
    {
        if (Donem is null) return;
        try
        {
            Guid? personelId = null;
            if (!string.IsNullOrWhiteSpace(DuzeltmeTc))
            {
                var bulunan = (await _personel.ListeleAsync(DuzeltmeTc.Trim())).Where(p => p.TcKimlikNo == DuzeltmeTc.Trim()).ToList();
                if (bulunan.Count != 1) { Mesaj = "TC ile tek bir personel bulunamadı."; return; }
                personelId = bulunan[0].Id;
            }
            var d = await _servis.DuzeltmeEkleAsync(Donem.Id, personelId, nameof(MaasDonemKaydi), null, DuzeltmeAlan,
                DuzeltmeEski, DuzeltmeYeni, DuzeltmeNeden ?? "", Kullanici);
            await YukleAsync();
            Mesaj = $"Düzeltme kaydedildi; fark {d.FarkTutari?.ToString("N2") ?? "-"} → {d.UygulanacakDonemYil}-{d.UygulanacakDonemAy:D2} dönemine devreder. Eski kayıt değişmedi.";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task RaporExcelAsync()
    {
        if (Donem is null) return;
        var dlg = new SaveFileDialog { Filter = "Excel|*.xlsx", FileName = $"kapanis-raporu-{DonemYil}-{DonemAy:D2}.xlsx" };
        if (dlg.ShowDialog() != true) return;
        await Calistir(async () => await File.WriteAllBytesAsync(dlg.FileName, await _servis.KapanisRaporuExcelAsync(Donem.Id)), "Rapor kaydedildi.");
    }

    private async Task Calistir(Func<Task> islem, string basari)
    {
        try
        {
            await islem();
            await YukleAsync();
            Mesaj = basari;
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private static void Doldur<T>(ObservableCollection<T> hedef, IEnumerable<T> kaynak)
    {
        hedef.Clear();
        foreach (var x in kaynak) hedef.Add(x);
    }
}
