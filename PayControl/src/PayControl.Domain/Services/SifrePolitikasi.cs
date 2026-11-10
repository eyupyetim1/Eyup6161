namespace PayControl.Domain.Services;

public static class SifrePolitikasi
{
    public const string VarsayilanAdminSifresi = "Admin123!";
    public const int AzamiHataliDeneme = 5;
    public static readonly TimeSpan KilitSuresi = TimeSpan.FromMinutes(15);

    /// <summary>En az 8 karakter, harf ve rakam içermeli; kullanıcı adı veya varsayılan şifre olamaz.</summary>
    public static IReadOnlyList<string> Denetle(string? sifre, string kullaniciAdi)
    {
        var hatalar = new List<string>();
        if (string.IsNullOrEmpty(sifre) || sifre.Length < 8) hatalar.Add("Şifre en az 8 karakter olmalıdır.");
        if (sifre is not null && !sifre.Any(char.IsLetter)) hatalar.Add("Şifre en az bir harf içermelidir.");
        if (sifre is not null && !sifre.Any(char.IsDigit)) hatalar.Add("Şifre en az bir rakam içermelidir.");
        if (sifre is not null && sifre.Contains(kullaniciAdi, StringComparison.OrdinalIgnoreCase)) hatalar.Add("Şifre kullanıcı adını içeremez.");
        if (sifre == VarsayilanAdminSifresi) hatalar.Add("Varsayılan şifre kullanılamaz.");
        return hatalar;
    }
}
