using PayControl.Domain.Enums;

namespace PayControl.Domain.Entities;

/// <summary>
/// Bordro satırındaki tek bir ödeme veya kesinti kalemi.
/// </summary>
public class MaasKalemi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaasDonemKaydiId { get; set; }
    public MaasDonemKaydi? MaasDonemKaydi { get; set; }

    public MaasKalemTuru Tur { get; set; }
    public string Ad { get; set; } = string.Empty;
    public decimal Tutar { get; set; }

    /// <summary>
    /// true ise bu kalem Net İstihkak içinde zaten dahildir; toplamda ikinci kez eklenmez.
    /// </summary>
    public bool NetIstihkakaDahil { get; set; }

    public bool KesintiMi { get; set; }
}
