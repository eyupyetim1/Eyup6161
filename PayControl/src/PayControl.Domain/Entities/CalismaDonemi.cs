using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

/// <summary>
/// Personelin bir çalışma dönemi. Yeniden işe alımda yeni dönem açılır; geçmiş aynı kartta kalır.
/// </summary>
public class CalismaDonemi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PersonelId { get; set; }
    public Personel? Personel { get; set; }

    public DateOnly IseGirisTarihi { get; set; }
    public DateOnly? IstenCikisTarihi { get; set; }

    public string? Isyeri { get; set; }
    public string? Gorevi { get; set; }
    public string? Sirket { get; set; }

    public CalismaDurumu Durum { get; set; } = CalismaDurumu.Aktif;
    public string? Aciklama { get; set; }

    public ICollection<IstenCikisKaydi> IstenCikisKayitlari { get; set; } = new List<IstenCikisKaydi>();
}
