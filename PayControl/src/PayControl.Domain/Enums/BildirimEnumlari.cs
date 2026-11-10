namespace PayControl.Domain.Enums;

public enum BildirimKanali { Eposta = 0, WhatsApp = 1, EpostaVeWhatsApp = 2 }

public enum BildirimDurumu { Bekliyor = 0, Gonderildi = 1, Hata = 2, ElleAcildi = 3 }

public enum BildirimSikligi { Gunluk = 0, Haftalik = 1, Aylik = 2 }

public enum BildirimTetikleyici
{
    /// <summary>Belirlenen gün/saatte rapor gönderimi.</summary>
    ZamanlanmisRapor = 0,
    /// <summary>Uyarı merkezindeki kritik uyarıların özeti.</summary>
    UyariOzeti = 1,
    DonemKapandi = 2,
    MutabakatFarkiOlustu = 3,
    YedekHatasi = 4,
    /// <summary>Yeni çıkış talimatı verildi (İK'ya iş ataması).</summary>
    CikisTalimatiVerildi = 5,
    /// <summary>Çıkış talimatının son günü geçti, çıkış kaydı hâlâ yok (günlük hatırlatma).</summary>
    CikisTalimatiGecikti = 6
}

public enum RaporBicimi { Excel = 0, Pdf = 1, Csv = 2 }

public enum RaporTuru
{
    DonemBordroOzeti = 0,
    TesisKarsilastirma = 1,
    PersonelMaliyetTrendi = 2,
    OdenmemisHakEdisYaslandirma = 3,
    AvansDurumu = 4,
    FazlaOdemeVeTahsilat = 5,
    GirisCikisDevirHizi = 6,
    SgkCikisDurumu = 7,
    KesintiRaporu = 8,
    MutabakatFarklari = 9,
    OdemeKontrolIstisnalari = 10,
    BankaKasaOdemeDokumu = 11,
    MaasDegisiklikleri = 12,
    DenetimIzi = 13,
    KapanisOzetleri = 14,
    PersonelBakiyeListesi = 15,
    IcraDosyalari = 16,
    TesisPersonelListesi = 17,
    CikisTalimatTakibi = 18
}
