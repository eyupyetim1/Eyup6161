using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Entities;

namespace PayControl.Desktop.ViewModels;

/// <summary>Personel kartının icra/BES/tahsilat, belge ve ödeme (iptal) sekmeleri.</summary>
public partial class PersonelEkPanelModel : ObservableObject
{
    private readonly IKesintiServisi _kesinti;
    private readonly IBelgeServisi _belge;
    private readonly IBankaOdemeServisi _banka;
    private readonly OturumServisi _oturum;
    private Guid _personelId;

    public PersonelEkPanelModel(IKesintiServisi kesinti, IBelgeServisi belge, IBankaOdemeServisi banka, OturumServisi oturum)
    {
        _kesinti = kesinti;
        _belge = belge;
        _banka = banka;
        _oturum = oturum;
    }

    private string Kullanici => _oturum.KullaniciAdi ?? "";
    /// <summary>Kesinti/ödeme değişikliği sonrası ekstreyi yenilemek için.</summary>
    public Func<Task>? Degisti { get; set; }

    public ObservableCollection<IcraDosyasi> IcraDosyalari { get; } = new();
    public ObservableCollection<BesTanimi> BesTanimlari { get; } = new();
    public ObservableCollection<TahsilatPlani> TahsilatPlanlari { get; } = new();
    public ObservableCollection<TahsilatTaksiti> SeciliPlanTaksitleri { get; } = new();
    public ObservableCollection<Belge> Belgeler { get; } = new();
    public ObservableCollection<BankaOdemeKaydi> Odemeler { get; } = new();

    [ObservableProperty] private string? _mesaj;

    // İcra
    [ObservableProperty] private string? _icraDosyaNo;
    [ObservableProperty] private string? _icraDairesi;
    [ObservableProperty] private string? _icraAlacakli;
    [ObservableProperty] private decimal _icraBorc;
    [ObservableProperty] private decimal _icraOraniYuzde = 25;
    [ObservableProperty] private int _icraSira = 1;
    [ObservableProperty] private DateTime _icraBaslangic = DateTime.Today;
    [ObservableProperty] private IcraDosyasi? _seciliIcra;
    [ObservableProperty] private string? _icraKapatmaNedeni;

    // BES
    [ObservableProperty] private decimal _besOraniYuzde = 3;
    [ObservableProperty] private DateTime _besBaslangic = DateTime.Today;
    [ObservableProperty] private DateTime _besCayma = DateTime.Today;

    // Tahsilat
    [ObservableProperty] private decimal _tahsilatTutar;
    [ObservableProperty] private int _tahsilatTaksit = 3;
    [ObservableProperty] private int _tahsilatYil = DateTime.Today.Year;
    [ObservableProperty] private int _tahsilatAy = DateTime.Today.Month;
    [ObservableProperty] private string? _tahsilatAciklama;
    [ObservableProperty] private TahsilatPlani? _seciliPlan;

    // Belge
    [ObservableProperty] private string? _belgeBaslik;
    [ObservableProperty] private string? _belgeAciklama;
    [ObservableProperty] private Belge? _seciliBelge;
    [ObservableProperty] private string? _belgeGecersizNedeni;

    // Ödeme iptali
    [ObservableProperty] private BankaOdemeKaydi? _seciliOdeme;
    [ObservableProperty] private string? _iptalNedeni;

    partial void OnSeciliPlanChanged(TahsilatPlani? value)
    {
        SeciliPlanTaksitleri.Clear();
        foreach (var t in value?.Taksitler.OrderBy(t => t.DonemYil).ThenBy(t => t.DonemAy) ?? Enumerable.Empty<TahsilatTaksiti>())
            SeciliPlanTaksitleri.Add(t);
    }

    public async Task YukleAsync(Guid personelId)
    {
        _personelId = personelId;
        try
        {
            Doldur(IcraDosyalari, await _kesinti.IcraDosyalariAsync(personelId));
            Doldur(BesTanimlari, await _kesinti.BesTanimlariAsync(personelId));
            Doldur(TahsilatPlanlari, await _kesinti.TahsilatPlanlariAsync(personelId));
            SeciliPlan = TahsilatPlanlari.FirstOrDefault();
            Doldur(Belgeler, await _belge.ListeleAsync(personelId: personelId));
            Doldur(Odemeler, (await _banka.PersonelOdemeleriAsync(personelId)).OrderByDescending(o => o.OdemeTarihi).ToList());
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private static void Doldur<T>(ObservableCollection<T> hedef, IEnumerable<T> kaynak)
    {
        hedef.Clear();
        foreach (var x in kaynak) hedef.Add(x);
    }

    private Task Bildir(string m)
    {
        Mesaj = m;
        return Task.CompletedTask;
    }

    private async Task Islem(Func<Task> is_, string basari, bool ekstreyiYenile = false)
    {
        try
        {
            await is_();
            Mesaj = basari;
            await YukleAsync(_personelId);
            if (ekstreyiYenile && Degisti is not null) await Degisti();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private Task IcraEkleAsync() => Islem(async () =>
    {
        await _kesinti.IcraDosyasiKaydetAsync(_personelId, IcraDosyaNo ?? "", IcraDairesi, IcraAlacakli, IcraBorc, IcraOraniYuzde / 100m, IcraSira,
            DateOnly.FromDateTime(IcraBaslangic), Kullanici);
        IcraDosyaNo = null; IcraDairesi = null; IcraAlacakli = null; IcraBorc = 0;
    }, "İcra dosyası kaydedildi. Bordrodaki icra kesintisi sıraya göre bu dosyaya işlenir.");

    [RelayCommand]
    private Task IcraKapatAsync() => SeciliIcra is null
        ? Bildir("Kapatılacak icra dosyasını seçin.")
        : Islem(() => _kesinti.IcraDosyasiKapatAsync(SeciliIcra.Id, IcraKapatmaNedeni ?? "", Kullanici), "İcra dosyası kapatıldı.");

    [RelayCommand]
    private Task BesTanimlaAsync() => Islem(() => _kesinti.BesTanimlaAsync(_personelId, BesOraniYuzde / 100m, DateOnly.FromDateTime(BesBaslangic), Kullanici),
        "BES tanımı kaydedildi; ödeme öncesi kontrolde BES kesintisi beklenir.");

    [RelayCommand]
    private Task BesCaymaAsync() => Islem(() => _kesinti.BesCaymaAsync(_personelId, DateOnly.FromDateTime(BesCayma), Kullanici), "BES cayma tarihi işlendi.");

    [RelayCommand]
    private Task TahsilatPlaniAsync() => Islem(async () =>
    {
        await _kesinti.TahsilatPlaniOlusturAsync(_personelId, TahsilatTutar, TahsilatTaksit, TahsilatYil, TahsilatAy, TahsilatAciklama ?? "Fazla ödeme geri tahsilatı", Kullanici);
        TahsilatTutar = 0;
        TahsilatAciklama = null;
    }, "Tahsilat planı oluşturuldu. Bordrodaki 'FAZLA ÖDEME KESİNTİSİ' kolonu taksitlere işlenir.", true);

    [RelayCommand]
    private async Task BelgeEkleAsync()
    {
        var dosya = ArayuzYardimcisi.DosyaSec("Belgeler|*.pdf;*.jpg;*.jpeg;*.png;*.xlsx;*.docx;*.txt|Tüm dosyalar|*.*", "Personel belgesi ekle");
        if (dosya is null) return;
        await Islem(async () =>
        {
            await _belge.EkleAsync("Personel", _personelId, _personelId, dosya, string.IsNullOrWhiteSpace(BelgeBaslik) ? Path.GetFileNameWithoutExtension(dosya) : BelgeBaslik,
                BelgeAciklama, Kullanici);
            BelgeBaslik = null;
            BelgeAciklama = null;
        }, "Belge eklendi (kopyası güvenli depoya alındı, SHA-256 ile korunuyor).");
    }

    [RelayCommand]
    private async Task BelgeAcAsync()
    {
        if (SeciliBelge is null) { Mesaj = "Açılacak belgeyi seçin."; return; }
        try { ArayuzYardimcisi.Ac(await _belge.DosyaYoluAsync(SeciliBelge.Id)); }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private Task BelgeGecersizAsync() => SeciliBelge is null
        ? Bildir("Belgeyi seçin.")
        : Islem(() => _belge.GecersizKilAsync(SeciliBelge.Id, BelgeGecersizNedeni ?? "", Kullanici), "Belge geçersiz işaretlendi (silinmez, denetim izi korunur).");

    [RelayCommand]
    private async Task OdemeIptalAsync()
    {
        if (SeciliOdeme is null) { Mesaj = "İptal edilecek ödemeyi seçin."; return; }
        if (string.IsNullOrWhiteSpace(IptalNedeni)) { Mesaj = "İptal nedeni zorunludur."; return; }
        if (!ArayuzYardimcisi.Onayla($"{SeciliOdeme.OdemeTarihi:dd.MM.yyyy} tarihli {SeciliOdeme.Tutar:N2} ₺ ödeme iptal edilecek.\nKayıt silinmez; hesap ekstresinde ters kayıt oluşur. Devam edilsin mi?"))
            return;
        await Islem(async () =>
        {
            await _banka.IptalEtAsync(SeciliOdeme.Id, IptalNedeni!, Kullanici);
            IptalNedeni = null;
        }, "Ödeme iptal edildi; ters kayıt oluşturuldu. Doğru ödeme yeniden girilebilir.", true);
    }
}
