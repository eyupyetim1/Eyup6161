namespace PayControl.Application.Interfaces;

public sealed class GorevCalismaSonucu
{
    public bool YedekAlindi { get; init; }
    public int CalisanKural { get; init; }
    public int YenidenDenenen { get; init; }
    public IReadOnlyList<string> Hatalar { get; init; } = Array.Empty<string>();
    public override string ToString() =>
        $"Yedek: {(YedekAlindi ? "alındı" : "gerekmedi")}, zamanlanmış kural: {CalisanKural}, yeniden denenen bildirim: {YenidenDenenen}" +
        (Hatalar.Count == 0 ? "" : $", hata: {string.Join(" | ", Hatalar)}");
}

/// <summary>
/// Arka plan işleri: otomatik yedek, zamanlanmış rapor/uyarı bildirimleri, başarısız bildirimlerin yeniden denenmesi.
/// Masaüstü uygulaması açıkken zamanlayıcıyla, kapalıyken Windows Görev Zamanlayıcısı ile (PayControl.exe --zamanlanmis-gorevler) çalışır.
/// Her adım bağımsızdır; biri hata verirse diğerleri yine çalışır.
/// </summary>
public interface IZamanlanmisGorevServisi
{
    Task<GorevCalismaSonucu> CalistirAsync(DateTime simdi, CancellationToken ct = default);
}
