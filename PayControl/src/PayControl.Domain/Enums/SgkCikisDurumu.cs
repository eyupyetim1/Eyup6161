namespace PayControl.Domain.Enums;

/// <summary>
/// SGK işten ayrılış bildirgesi durumu.
/// Program kendiliğinden "Verildi" varsaymaz; yalnızca kullanıcı onayı veya belge ile işaretlenir.
/// </summary>
public enum SgkCikisDurumu
{
    Belirsiz = 0,
    Verilmedi = 1,
    Verildi = 2
}
