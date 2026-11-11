using System.Data;

namespace PayControl.Application.Interfaces;

public sealed record PysTabloTanimi(string Tablo, string Baslik, string Grup, string Aciklama);

public interface IEskiVeriServisi
{
    /// <summary>VBA menüsüne göre gruplanmış tablolar.</summary>
    IReadOnlyList<PysTabloTanimi> Tanimlar { get; }
    Task<IReadOnlyDictionary<string, int>> SatirSayilariAsync(CancellationToken ct = default);
    /// <summary>Tabloyu düzenlenebilir DataTable olarak verir; TCKN/PersonelID varsa "Personel" adı sütunu eklenir (salt okunur).</summary>
    Task<DataTable> TabloAsync(string tablo, CancellationToken ct = default);
    /// <summary>DataTable'daki eklenen/değişen/silinen satırları kaydeder; kaydedilen satır sayısını döner.</summary>
    Task<int> KaydetAsync(string tablo, DataTable tablo2, string kullanici, CancellationToken ct = default);
    /// <summary>Access sütun adının Türkçe başlığı.</summary>
    string SutunBasligi(string sutun);
}
