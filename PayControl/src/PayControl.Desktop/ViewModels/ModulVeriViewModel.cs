using System.Collections.ObjectModel;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ClosedXML.Excel;
using PayControl.Application.Interfaces;
using PayControl.Desktop.Services;
using PayControl.Domain.Services;

namespace PayControl.Desktop.ViewModels;

public sealed partial class ModulSekmesi : ObservableObject
{
    public ModulSekmesi(PysTabloTanimi t) => Tanim = t;
    public PysTabloTanimi Tanim { get; }
    [ObservableProperty] private int _satir;
    public string Baslik => Tanim.Baslik;
}

/// <summary>VBA menü grubunun (İzin ve bakiye, Personel dosyaları, İK işlemleri…) Access tablolarını gösterir ve düzenletir.</summary>
public partial class ModulVeriViewModel : ObservableObject
{
    public const string AramaSutunu = "__arama";
    private readonly IEskiVeriServisi _servis;
    private readonly OturumServisi _oturum;
    private DataTable? _tablo;

    public ModulVeriViewModel(IEskiVeriServisi servis, OturumServisi oturum)
    {
        _servis = servis;
        _oturum = oturum;
    }

    public IEskiVeriServisi Servis => _servis;
    public ObservableCollection<ModulSekmesi> Sekmeler { get; } = new();
    [ObservableProperty] private string _grup = "";
    [ObservableProperty] private string _grupAciklama = "";
    [ObservableProperty] private ModulSekmesi? _seciliSekme;
    [ObservableProperty] private DataView? _gorunum;
    [ObservableProperty] private string? _arama;
    [ObservableProperty] private string? _mesaj;
    [ObservableProperty] private string _durum = "";

    public static readonly IReadOnlyDictionary<string, string> GrupAciklamalari = new Dictionary<string, string>
    {
        ["İzin ve bakiye"] = "İzin hareketleri, HT bakiye sabitlemeleri, görev/hasat izin kesintileri, manuel düzeltmeler, izin işlem geçmişi",
        ["Personel dosyaları"] = "Kimde hangi belge tam, bekleniyor ya da eksik; belge tanımları",
        ["Sözleşmeler"] = "Şirket sözleşmeleri ve personelin imzaladığı sözleşmeler",
        ["İSG"] = "İşyeri İSG belgeleri, türleri ve bağlı personel",
        ["İK işlemleri"] = "Zimmet, eğitim, avans ve kesinti, fazla mesai, disiplin, görevlendirme, işe giriş kontrolü",
        ["Şirketler"] = "Şirketler, personel-şirket ve tesis-şirket eşleşmeleri",
        ["Sistem"] = "Eski programın kullanıcıları, yetkileri, ayarları ve günlükleri",
    };

    public async Task YukleAsync(string grup)
    {
        Grup = grup;
        GrupAciklama = GrupAciklamalari.GetValueOrDefault(grup, "");
        try
        {
            var sayilar = await _servis.SatirSayilariAsync();
            Sekmeler.Clear();
            foreach (var t in _servis.Tanimlar.Where(t => t.Grup == grup))
                Sekmeler.Add(new ModulSekmesi(t) { Satir = sayilar.GetValueOrDefault(t.Tablo) });
            SeciliSekme = Sekmeler.FirstOrDefault();
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    partial void OnSeciliSekmeChanged(ModulSekmesi? value)
    {
        if (value is not null) _ = TabloYukleAsync();
    }

    partial void OnAramaChanged(string? value) => Suz();

    [RelayCommand]
    private async Task TabloYukleAsync()
    {
        if (SeciliSekme is null) return;
        if (_tablo?.GetChanges() is { Rows.Count: > 0 } d && !ArayuzYardimcisi.Onayla($"{d.Rows.Count} kaydedilmemiş değişiklik var. Kaydetmeden devam edilsin mi?")) return;
        try
        {
            _tablo = await _servis.TabloAsync(SeciliSekme.Tanim.Tablo);
            var arama = _tablo.Columns.Add(AramaSutunu, typeof(string));
            foreach (DataRow r in _tablo.Rows)
                r[AramaSutunu] = AramaKurallari.Normalize(string.Join(" ", r.ItemArray.Where(x => x is not DBNull and not Guid).Select(x => x is DateTime t ? t.ToString("dd.MM.yyyy") : Convert.ToString(x))));
            _tablo.AcceptChanges();
            _tablo.RowChanged += (_, _) => DurumGuncelle();
            _tablo.RowDeleted += (_, _) => DurumGuncelle();
            Gorunum = _tablo.DefaultView;
            Suz();
            DurumGuncelle();
            Mesaj = null;
        }
        catch (Exception ex) { Mesaj = ex.Message; }
    }

    private void Suz()
    {
        if (Gorunum is null) return;
        var parcalar = AramaKurallari.Normalize(Arama).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Replace("'", "''").Replace("[", "[[]").Replace("%", "[%]").Replace("*", "[*]"));
        Gorunum.RowFilter = string.Join(" AND ", parcalar.Select(p => $"{AramaSutunu} LIKE '%{p}%'"));
        DurumGuncelle();
    }

    private void DurumGuncelle()
    {
        if (_tablo is null || Gorunum is null) { Durum = ""; return; }
        var degisen = _tablo.GetChanges()?.Rows.Count ?? 0;
        Durum = $"{Gorunum.Count} / {_tablo.Rows.Count} kayıt" + (degisen > 0 ? $" · {degisen} kaydedilmemiş değişiklik" : "");
    }

    [RelayCommand]
    private async Task KaydetAsync()
    {
        if (_tablo is null || SeciliSekme is null) return;
        try
        {
            var n = await _servis.KaydetAsync(SeciliSekme.Tanim.Tablo, _tablo, _oturum.KullaniciAdi ?? "");
            Mesaj = n == 0 ? "Kaydedilecek değişiklik yok." : $"{n} kayıt kaydedildi.";
            _tablo.AcceptChanges();
            await TabloYukleAsync();
            SeciliSekme.Satir = _tablo?.Rows.Count ?? 0;
        }
        catch (Exception ex) { Mesaj = "Kaydedilemedi: " + ex.Message; }
    }

    public void SatirlariSil(IEnumerable<DataRowView> satirlar)
    {
        var liste = satirlar.ToList();
        if (liste.Count == 0) { Mesaj = "Silinecek satırları seçin."; return; }
        if (!ArayuzYardimcisi.Onayla($"{liste.Count} kayıt silinecek (Kaydet'e basınca kalıcı olur). Devam edilsin mi?")) return;
        foreach (var r in liste) r.Delete();
        DurumGuncelle();
    }

    [RelayCommand]
    private void Excel()
    {
        if (Gorunum is null || SeciliSekme is null) return;
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(SeciliSekme.Baslik.Length > 31 ? SeciliSekme.Baslik[..31] : SeciliSekme.Baslik);
        var sutunlar = Gorunum.Table!.Columns.Cast<DataColumn>().Where(c => c.ColumnName is not ("PysId" or AramaSutunu)).ToList();
        for (var i = 0; i < sutunlar.Count; i++) ws.Cell(1, i + 1).Value = _servis.SutunBasligi(sutunlar[i].ColumnName);
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (DataRowView v in Gorunum)
        {
            for (var i = 0; i < sutunlar.Count; i++)
            {
                var x = v.Row[sutunlar[i]];
                var c = ws.Cell(r, i + 1);
                switch (x)
                {
                    case DBNull: break;
                    case DateTime t: c.Value = t; c.Style.DateFormat.Format = t.TimeOfDay == TimeSpan.Zero ? "dd.MM.yyyy" : "dd.MM.yyyy HH:mm"; break;
                    case decimal d: c.Value = d; break;
                    case int n: c.Value = n; break;
                    case bool b: c.Value = b ? "Evet" : "Hayır"; break;
                    default: c.Value = Convert.ToString(x); break;
                }
            }
            r++;
        }
        ws.Columns().AdjustToContents(1, Math.Min(r, 200), 8, 60);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        var yol = ArayuzYardimcisi.Kaydet(ms.ToArray(), $"{SeciliSekme.Baslik}-{DateTime.Now:yyyyMMdd}.xlsx");
        if (yol is not null) Mesaj = $"Kaydedildi: {yol}";
    }
}
