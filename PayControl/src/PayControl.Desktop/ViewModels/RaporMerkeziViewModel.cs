using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Entities;
using PayControl.Domain.Enums;

namespace PayControl.Desktop.ViewModels;

public sealed record OzetOgesi(string Ad, string Deger);

public partial class SecilebilirAlici : ObservableObject
{
    public SecilebilirAlici(BildirimAlicisi a) => Alici = a;
    public BildirimAlicisi Alici { get; }
    [ObservableProperty] private bool _secili;
    public string Etiket => $"{Alici.AdSoyad} · {Alici.Rol}{(Alici.Tesis is null ? "" : " · " + Alici.Tesis)}";
    public string Iletisim => string.Join("  ", new[] { Alici.Eposta, Alici.Telefon }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

public partial class RaporMerkeziViewModel : ObservableObject
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private readonly IRaporServisi _rapor;
    private readonly ITesisServisi _tesis;
    private readonly IBildirimServisi _bildirim;
    private readonly OturumServisi _oturum;
    private RaporSonucu? _sonuc;

    public RaporMerkeziViewModel(IRaporServisi rapor, ITesisServisi tesis, IBildirimServisi bildirim, OturumServisi oturum)
    {
        _rapor = rapor;
        _tesis = tesis;
        _bildirim = bildirim;
        _oturum = oturum;
        Tanimlar = rapor.Tanimlar;
        SeciliTanim = Tanimlar.FirstOrDefault();
        var bas = DateTime.Today.AddMonths(-2);
        BaslangicYil = bas.Year; BaslangicAy = bas.Month;
        BitisYil = DateTime.Today.Year; BitisAy = DateTime.Today.Month;
    }

    public IReadOnlyList<RaporTanimi> Tanimlar { get; }
    public ObservableCollection<string> Tesisler { get; } = new();
    public ObservableCollection<SecilebilirAlici> Alicilar { get; } = new();
    public ObservableCollection<OzetOgesi> Ozet { get; } = new();

    public IReadOnlyList<Secenek<RaporBicimi>> Bicimler { get; } =
        ArayuzYardimcisi.EnumSecenekleri((RaporBicimi.Excel, "Excel (.xlsx)"), (RaporBicimi.Pdf, "PDF"), (RaporBicimi.Csv, "CSV"));
    public IReadOnlyList<Secenek<BildirimKanali>> Kanallar { get; } =
        ArayuzYardimcisi.EnumSecenekleri((BildirimKanali.Eposta, "E-posta"), (BildirimKanali.WhatsApp, "WhatsApp"), (BildirimKanali.EpostaVeWhatsApp, "E-posta + WhatsApp"));

    [ObservableProperty] private RaporTanimi? _seciliTanim;
    [ObservableProperty] private int _baslangicYil;
    [ObservableProperty] private int _baslangicAy;
    [ObservableProperty] private int _bitisYil;
    [ObservableProperty] private int _bitisAy;
    [ObservableProperty] private string _seciliTesis = "Tüm tesisler";
    [ObservableProperty] private string? _sirket;
    [ObservableProperty] private DataView? _tablo;
    [ObservableProperty] private string? _raporBasligi;
    [ObservableProperty] private string? _raporAciklamasi;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private bool _mesgul;
    [ObservableProperty] private RaporBicimi _gonderimBicimi = RaporBicimi.Excel;
    [ObservableProperty] private BildirimKanali _gonderimKanali = BildirimKanali.Eposta;
    [ObservableProperty] private string? _notMetni;
    [ObservableProperty] private string _satirSayisi = "—";

    public bool RaporVar => _sonuc is not null;

    partial void OnSeciliTanimChanged(RaporTanimi? value)
    {
        if (value is not null) _ = OlusturAsync();
    }

    public async Task YukleAsync()
    {
        Tesisler.Clear();
        Tesisler.Add("Tüm tesisler");
        foreach (var t in (await _tesis.ListeleAsync()).Where(t => t.Tesis.Aktif).Select(t => t.Tesis.Ad).OrderBy(a => a, StringComparer.Create(Tr, true)))
            Tesisler.Add(t);
        Alicilar.Clear();
        foreach (var a in (await _bildirim.AlicilarAsync()).Where(a => a.Aktif))
            Alicilar.Add(new SecilebilirAlici(a));
        await OlusturAsync();
    }

    private RaporParametreleri Parametreler() => new()
    {
        BaslangicYil = BaslangicYil, BaslangicAy = Math.Clamp(BaslangicAy, 1, 12), BitisYil = BitisYil, BitisAy = Math.Clamp(BitisAy, 1, 12),
        Tesis = SeciliTesis == "Tüm tesisler" ? null : SeciliTesis,
        Sirket = string.IsNullOrWhiteSpace(Sirket) ? null : Sirket.Trim()
    };

    [RelayCommand]
    private async Task OlusturAsync()
    {
        if (SeciliTanim is null || Mesgul) return;
        Mesgul = true;
        Mesaj = null;
        try
        {
            _sonuc = await _rapor.OlusturAsync(SeciliTanim.Tur, Parametreler());
            RaporBasligi = _sonuc.Baslik;
            RaporAciklamasi = $"{_sonuc.Aciklama}  ·  {_sonuc.ParametreMetni}";
            Ozet.Clear();
            foreach (var o in _sonuc.Ozet) Ozet.Add(new OzetOgesi(o.Ad, o.Deger));
            Tablo = TabloyaCevir(_sonuc).DefaultView;
            SatirSayisi = $"{_sonuc.Satirlar.Count:N0} satır";
        }
        catch (Exception ex) { Mesaj = ex.Message; _sonuc = null; Tablo = null; }
        finally { Mesgul = false; OnPropertyChanged(nameof(RaporVar)); }
    }

    private static DataTable TabloyaCevir(RaporSonucu r)
    {
        var dt = new DataTable();
        var adlar = new HashSet<string>();
        foreach (var k in r.Kolonlar)
        {
            var ad = k.Baslik;
            var i = 2;
            while (!adlar.Add(ad)) ad = $"{k.Baslik} ({i++})";
            dt.Columns.Add(ad, typeof(string));
        }
        void Ekle(object?[] satir)
        {
            var row = dt.NewRow();
            for (var i = 0; i < r.Kolonlar.Count && i < satir.Length; i++) row[i] = Bicimle(satir[i], r.Kolonlar[i].Tur);
            dt.Rows.Add(row);
        }
        foreach (var s in r.Satirlar) Ekle(s);
        if (r.Toplam is not null) Ekle(r.Toplam);
        return dt;
    }

    private static string Bicimle(object? v, RaporKolonTuru tur) => v switch
    {
        null => "",
        decimal d when tur == RaporKolonTuru.Yuzde => (d * 100).ToString("N1", Tr) + " %",
        decimal d => d.ToString(tur == RaporKolonTuru.Sayi ? "N0" : "N2", Tr),
        double d => d.ToString("N2", Tr),
        int i => i.ToString("N0", Tr),
        DateOnly t => t.ToString("dd.MM.yyyy", Tr),
        DateTime t => t.ToString(t.TimeOfDay == TimeSpan.Zero ? "dd.MM.yyyy" : "dd.MM.yyyy HH:mm", Tr),
        bool b => b ? "Evet" : "Hayır",
        _ => Convert.ToString(v, Tr) ?? ""
    };

    [RelayCommand] private void Excel() => DisaAktar(RaporBicimi.Excel);
    [RelayCommand] private void Pdf() => DisaAktar(RaporBicimi.Pdf);
    [RelayCommand] private void Csv() => DisaAktar(RaporBicimi.Csv);

    private void DisaAktar(RaporBicimi bicim)
    {
        if (_sonuc is null) { Mesaj = "Önce raporu oluşturun."; return; }
        try
        {
            var yol = ArayuzYardimcisi.Kaydet(_rapor.DisaAktar(_sonuc, bicim), _rapor.DosyaAdi(_sonuc, bicim));
            if (yol is not null) Mesaj = $"Kaydedildi: {yol}";
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    [RelayCommand]
    private async Task GonderAsync()
    {
        if (SeciliTanim is null) return;
        var secilen = Alicilar.Where(a => a.Secili).Select(a => a.Alici.Id).ToList();
        if (secilen.Count == 0) { Mesaj = "En az bir alıcı seçin (alıcılar Bildirimler ekranından tanımlanır)."; return; }
        Mesgul = true;
        try
        {
            var kayitlar = await _bildirim.RaporGonderAsync(SeciliTanim.Tur, Parametreler(), GonderimBicimi, GonderimKanali, secilen, NotMetni,
                _oturum.KullaniciAdi ?? "");
            var basarili = kayitlar.Count(k => k.Durum == BildirimDurumu.Gonderildi);
            var hatalar = kayitlar.Where(k => k.Durum == BildirimDurumu.Hata).Select(k => $"{k.Alici}: {k.Hata}").ToList();
            Mesaj = $"{basarili}/{kayitlar.Count} gönderim başarılı." + (hatalar.Count > 0 ? Environment.NewLine + string.Join(Environment.NewLine, hatalar.Take(5)) +
                Environment.NewLine + "Başarısız gönderimler otomatik olarak en fazla 3 kez yeniden denenir." : "");
        }
        catch (Exception ex) { Mesaj = ex.Message; }
        finally { Mesgul = false; }
    }
}
