namespace PayControl.Domain.Entities;

/// <summary>Günlük puantaj kodu (Access: PuantajGunluk_&lt;Tesis&gt; tabloları tek tabloda).</summary>
public class PuantajGunu
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TesisAdi { get; set; } = string.Empty;
    public int Yil { get; set; }
    public int Ay { get; set; }
    public int Gun { get; set; }
    public string TcKimlikNo { get; set; } = string.Empty;
    public Guid? PersonelId { get; set; }
    /// <summary>Ç, HT, Yi, RT, Üİ, R, M, YG, G, Gİ, Hİ, İİ</summary>
    public string Kod { get; set; } = string.Empty;
    public string? Kullanici { get; set; }
    public DateTime Zaman { get; set; } = DateTime.UtcNow;
}

/// <summary>Puantaj hücresine yazılan not (Access: PuantajNot).</summary>
public class PuantajNotu
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TesisAdi { get; set; } = string.Empty;
    public string TcKimlikNo { get; set; } = string.Empty;
    public int Yil { get; set; }
    public int Ay { get; set; }
    public int Gun { get; set; }
    public string? Kod { get; set; }
    public string NotMetni { get; set; } = string.Empty;
    public string? AdSoyad { get; set; }
    public DateTime Zaman { get; set; } = DateTime.UtcNow;
}

/// <summary>Resmi tatil; arifeler 0,5 gün (Excel: Ayarlar sayfası).</summary>
public class ResmiTatil
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Tarih { get; set; }
    public string Ad { get; set; } = string.Empty;
    public decimal Hak { get; set; } = 1m;
}

/// <summary>Tesisler arası personel aktarımı (Access: PersonelNakil).</summary>
public class PersonelNakil
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? PersonelId { get; set; }
    public string TcKimlikNo { get; set; } = string.Empty;
    public string? AdSoyad { get; set; }
    public string? EskiTesis { get; set; }
    public string YeniTesis { get; set; } = string.Empty;
    public DateOnly NakilTarihi { get; set; }
    public string? Aciklama { get; set; }
    public string? Kullanici { get; set; }
    public DateTime Zaman { get; set; } = DateTime.UtcNow;
}

/// <summary>Kişiye özel izin ayarı (Access: PersonelIzinAyar).</summary>
public class PersonelIzinAyari
{
    public string TcKimlikNo { get; set; } = string.Empty;
    /// <summary>Resmi tatiller izin bakiyesine dahil mi.</summary>
    public bool RtDahil { get; set; } = true;
    /// <summary>Kişinin hafta tatili günleri (1=Pzt … 7=Paz), boşsa genel kural.</summary>
    public string? HaftaTatili { get; set; }
}

/// <summary>Access'ten aktarılan geçmiş aylık özet (karşılaştırma ve geçmiş için).</summary>
public class AylikOzetKaydi
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TesisAdi { get; set; } = string.Empty;
    public int Yil { get; set; }
    public int Ay { get; set; }
    public string TcKimlikNo { get; set; } = string.Empty;
    public string? AdSoyad { get; set; }
    public decimal HaftasonuHakki { get; set; }
    public decimal KullanilanHaftalikIzin { get; set; }
    public decimal Calisilan { get; set; }
    public decimal YillikIzin { get; set; }
    public decimal Raporlu { get; set; }
    public decimal ResmiTatil { get; set; }
    public decimal UcretsizIzin { get; set; }
    public decimal HT { get; set; }
    public decimal AylikToplam { get; set; }
    public decimal IdariIzin { get; set; }
    public decimal DevredenIzin { get; set; }
    public decimal Gorev { get; set; }
    public decimal GorevIzni { get; set; }
}
