namespace PayControl.Domain.Entities;

public class AvansMahsupKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AvansKaydiId { get; set; }
    public AvansKaydi? AvansKaydi { get; set; }

    public int DonemYil { get; set; }
    public int DonemAy { get; set; }
    public decimal PlanlananTutar { get; set; }
    public decimal? GerceklesenTutar { get; set; }
    public bool BordroyaGirildi { get; set; }
    public DateOnly? GerceklesmeTarihi { get; set; }
    public string? Aciklama { get; set; }

    /// <summary>Mahsup bordro içe aktarmasından geldiyse kaynak içe aktarma (geri almada iade edilir).</summary>
    public Guid? KaynakIceAktarmaId { get; set; }
    public string? GerceklestirenKullanici { get; set; }
}
