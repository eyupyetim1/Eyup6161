namespace PayControl.Application.Interfaces;

public sealed class AccessAktarimSonucu
{
    public int Tesis { get; set; }
    public int PersonelYeni { get; set; }
    public int PersonelGuncellenen { get; set; }
    public int Puantaj { get; set; }
    public int PuantajNotu { get; set; }
    public int AylikOzet { get; set; }
    public int Nakil { get; set; }
    public int IzinAyari { get; set; }
    public int CikisYapilan { get; set; }
    public Dictionary<string, int> DigerTablolar { get; } = new();
    public List<string> Uyarilar { get; } = new();
    public List<string> AktarilmayanTablolar { get; } = new();
    public override string ToString() =>
        $"{Tesis} tesis, {PersonelYeni} yeni / {PersonelGuncellenen} güncellenen personel ({CikisYapilan} çıkışlı), {Puantaj:N0} puantaj günü, " +
        $"{PuantajNotu} puantaj notu, {AylikOzet} aylık özet, {Nakil} nakil, {IzinAyari} kişisel izin ayarı" +
        (DigerTablolar.Count == 0 ? "" : $"; ayrıca {DigerTablolar.Count} tabloda {DigerTablolar.Values.Sum():N0} kayıt (izin, belge, sözleşme, İSG, zimmet, eğitim, avans, şirket…)") + " aktarıldı.";
}

public interface IAccessAktarimServisi
{
    /// <summary>Access (.accdb/.mdb) dosyasının tablolarını CSV klasörüne çıkarır (Windows: Access sürücüsü, 64/32 bit denenir).</summary>
    Task<string> CsvyeCikarAsync(string accessDosyasi, CancellationToken ct = default);

    /// <summary>CSV klasöründeki Personel Yönetim Sistemi tablolarını içe aktarır. Tekrar çalıştırılabilir (kayıtlar güncellenir, çoğaltılmaz).</summary>
    Task<AccessAktarimSonucu> IceAktarAsync(string csvKlasoru, string kullanici, CancellationToken ct = default);
}
