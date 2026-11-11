namespace PayControl.Domain.Services;

/// <summary>VBA BK_Kurallar: hesap kuralları metni (hesap koduyla birebir uyumlu).</summary>
public static class IzinBakiyeKurallari
{
    private const string Sablon = """
PUANTAJ VE İZİN BAKİYESİ NASIL HESAPLANIR

1. BAKİYENİN TANIMI
   • Bakiye = Hafta tatili (HT) bakiyesi + Resmi tatil (RT) bakiyesi.
   • RT, personelin izin kartında “RT bakiyeye dahil” işaretliyse toplama eklenir; değilse ayrı gösterilir.
   • Artı değer ALACAK, eksi değer BORÇ demektir.
   • Yıllık izin bu bakiyeden AYRI izlenir (bölüm 7).

2. HESABIN BAŞLANGICI VE BİTİŞİ
   • Hesap işe giriş tarihinden başlar. İşe giriş tarihi yoksa bakiye hesaplanmaz.
   • Kesin bakiye girildiyse hesap o tarihin ertesi gününden başlar (bölüm 6).
   • Hesap bugüne kadar yapılır; işten çıkanlarda çıkış tarihinde biter. Gelecek günler sayılmaz.
   • Pasif olup çıkış tarihi girilmemiş personelin bakiyesi hesaplanmaz.

3. HAFTA TATİLİ (HT)
   • Her hafta tatili günü için +1 gün hak oluşur.
   • Hafta tatili günü: personelin izin kartında tanımlı gün; tanımlı değilse genel ayar (şu an: {0}).
   • Aynı haftada 2 veya daha fazla Üİ (ücretsiz izin) varsa o haftanın hakkı yanar (+1 verilmez).
   • Puantajdaki her HT kodu -1, her YG (yarım gün) -0,5 düşer.
   • Yıllık izne denk gelen PAZAR yıllık izinden değil HT’den düşer (-1).
   • Yıllık izin bitip hemen ardından gelen boş pazar (izin dönüşü pazarı) HT kullanımı sayılır (-1).
   • İzin kartından girilen HT düzeltmesi (+ / -) aynen eklenir.

4. AYLIK KESİNTİLER
   • Rapor (R): her ay, her 7 R günü için 1 gün; kalan 6 veya fazlaysa 1 gün daha kesilir.
      Örnek: ayda 9 R → 1 gün, 13 R → 2 gün kesinti.
   • Rapor kesintisi o ayın hafta tatili hakkını geçemez (aylık hak eksiye düşmez).
   • Görev izni (Gİ) ve hasat izni (Hİ) kesintileri: izin kartında o ay için girilen gün kadar, ay sonunda düşülür.
   • Bir ayın hakkı elle girildiyse (manuel düzeltme) o ayın otomatik hakkı yerine elle girilen değer geçerlidir.

5. RESMİ TATİL (RT)
   • Ayarlar > Resmi tatiller listesindeki her tatil için “Hak” değeri kadar eklenir (yarım gün 0,5).
   • Yalnızca PAZAR gününe denk gelen resmi tatil hak vermez (cumartesi ya da kişinin izin günü fark etmez).
   • İşe girişten önceki ve çıkıştan sonraki tatiller sayılmaz.
   • RT kullanımı, ek hak ve düşürme yalnızca izin kartından elle girilir.

6. KESİN BAKİYE, SEZON KAPAMA, YENİ DÖNEM
   • Kesin bakiye: girilen değer o günün gün sonu bakiyesidir; o güne kadarki her şeyin yerine geçer, hesap ertesi günden devam eder.
   • Sezon kapama: seçilen personele aynı tarihte kesin bakiye yazar (genellikle 0). Aynı butonla geri alınabilir.
   • Yeni dönem: kesim tarihindeki bakiye kesin bakiye olarak devredilir; sonraki günler o bakiyeden devam eder.

7. YILLIK İZİN (bakiyeden ayrı)
   • Hak, her hizmet yılı dolduğunda (işe giriş yıl dönümü) eklenir; kullanılmayan izin birikir.
   • Yıllık hak (İş Kanunu md. 53): 1-5 yıl (5 dahil) 14 gün, 6-14 yıl 20 gün, 15 yıl ve üzeri 26 gün.
   • 18 yaş ve altı ile 50 yaş ve üstü: en az 20 gün (doğum tarihi girilmişse).
   • Kullanılan = puantajdaki Yİ günleri (pazara düşen Yİ hariç) + izin kartından elle girilen yıllık izin kullanımı + eski manuel düzeltmeler.
   • “Yıllık izin ekle / düş” hakkı artırır ya da azaltır.
   • Kalan = hak - kullanılan.
   • “Yıllık izin kalan (kesin)” girildiyse kalan o tarihte bu değere eşitlenir; önceki yıllar yeniden sayılmaz.
   • Aynı izni hem puantaja Yİ olarak hem izin kartına elle girmeyin: iki kez düşülür (program uyarır).

8. İZİN SÜRESİ HESAPLAMA (Yıllık izin hesaplama ekranı)
   • Pazarlar ve tam gün resmi tatiller izinden sayılmaz; izin o kadar uzar.
   • Yıllık izinde personelin kendi izin günü değil her zaman pazar esas alınır.
   • Yarım gün tatil (arife) izin günü sayılır.
   • İzin bittikten sonra gelen pazar ve resmi tatil atlanır: cumartesi biten izinde işbaşı pazartesidir.
   • “Puantaja kaydet” yalnızca izin günlerine Yİ yazar; kutu işaretliyse aradaki pazarlara da yazar (onlar HT’den düşer).
""";

    public static string Metin(HaftaTatiliPolitikasi politika) => Sablon.Replace("{0}", IzinBakiyeHesaplayici.PolitikaMetni(politika));
}
