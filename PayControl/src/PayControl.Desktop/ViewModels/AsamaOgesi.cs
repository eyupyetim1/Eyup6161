namespace PayControl.Desktop.ViewModels;

/// <summary>Adım göstergesi öğesi. Durum: "tamam", "aktif" veya "bekliyor".</summary>
public sealed record AsamaOgesi(int No, string Ad, string Durum, bool Son, string? Aciklama = null);
