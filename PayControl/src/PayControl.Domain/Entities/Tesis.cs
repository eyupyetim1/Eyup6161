namespace PayControl.Domain.Entities;

/// <summary>Tesis / işyeri tanımı. Personel ve bordro satırlarındaki işyeri adı <see cref="Ad"/> ile eşleşir.</summary>
public class Tesis
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string? Sirket { get; set; }
    public string? Adres { get; set; }
    public string? SorumluAdSoyad { get; set; }
    public string? SorumluEposta { get; set; }
    public string? SorumluTelefon { get; set; }
    public bool Aktif { get; set; } = true;
    public DateTime OlusturmaZamani { get; set; } = DateTime.UtcNow;
}

/// <summary>Dönem kapanışından önce her tesisin sorumlu onayı.</summary>
public class DonemTesisOnayi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaasDonemKapanisiId { get; set; }
    public string TesisAd { get; set; } = string.Empty;
    public string Onaylayan { get; set; } = string.Empty;
    public DateTime OnayZamani { get; set; } = DateTime.UtcNow;
    public string? Not { get; set; }
}
