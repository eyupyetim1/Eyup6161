namespace PayControl.Domain;

/// <summary>Kapatılmış maaş dönemine doğrudan müdahale veya değiştirilemez kayıt ihlali.</summary>
public sealed class KapaliDonemIhlaliException : InvalidOperationException
{
    public KapaliDonemIhlaliException(string message) : base(message) { }
}

/// <summary>Kullanıcının rolü işlem için yetersiz.</summary>
public sealed class YetkiHatasiException : InvalidOperationException
{
    public YetkiHatasiException(string message) : base(message) { }
}
