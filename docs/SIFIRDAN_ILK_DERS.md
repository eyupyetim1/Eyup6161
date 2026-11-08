# Sıfırdan ilk ders

Elektronik adı bilmeden de ilerlenir. Her ders tek iş verir. Lehim, devre breadboard üzerinde çalıştıktan sonra gösterilir.

## Elindeki parçaların adları

| Masada gördüğün | Adı | Ne işe yarar |
| --- | --- | --- |
| Siyah uzun kart, üstünde metal kutu, kenarında Micro USB | ESP32 geliştirme kartı | Cihazın beyni. Program buna yüklenir. |
| Dikdörtgen renkli ekran | TFT ekran, ILI9341 | Ölçümlerin yazılacağı ekran. |
| Ucu metal silindir, arkası kablo | DS18B20 | Suya sokulan sıcaklık ucu. |
| Üstünde vida olan küçük mavi veya kırmızı kart, LM2596 yazar | Voltaj düşürücü | 12 voltu yaklaşık 5 volta indirir. Ölçülmeden kullanılmaz. |
| Prize takılan siyah adaptör, 12 V 2 A yazar | Güç adaptörü | Prizden 12 volt verir. ESP32'ye doğrudan girmez. |
| Beyaz, üzerinde yüzlerce delik olan tahta | Breadboard | Lehimsiz deneme tahtası. Kablo deliğe sokulur. |
| Hazır, renkli, iki ucu plastik olan kablolar | Jumper | Breadboard ile kart arasındaki geçici kablo. |
| Küçük siyah kare, dört bacaklı | Push buton | Basınca temas eden tuş. |
| Renk şeritli küçük silindir | Direnç | Akımı sınırlar. 4,7 kΩ olanlar sıcaklık ucu içindir. |
| Kırmızı ölçü aleti, UT33B+ | Multimetre | Volt ölçer. Bağlamadan önce bununla bakılır. |
| Küçük mavi kart, SD kart yuvası | Mikro SD modülü | İleride kayıt için. Şimdi bağlanmaz. |
| Kısa siyah Micro USB kablo | Veri kablosu | ESP32'yi bilgisayara bağlar. |

## Lehim

Lehim, havya ile eritilen ince metal teldir. İki bakır ucu kalıcı olarak birleştirir. Sökmesi zordur.

Breadboard bunun geçici halidir. Deliklere kablo sokulur, çalışmazsa çıkarıp düzeltilir. Kalıcı kasa işine gelince lehim adım adım gösterilecek. Havya bu derste kullanılmaz.

## Bu dersin tek işi

Hiçbir kabloyu birbirine sokma. 12 V adaptörü prize takma. Ekranı, sıcaklık ucunu ve LM2596 kartını ESP32'ye değdirme.

1. ESP32 kartını masaya koy.
2. Metal kutunun üzerindeki yazının yakın fotoğrafını çek.
3. Kartın kenarındaki pin isimlerinin (3V3, GND, 5V gibi) fotoğrafını çek.
4. TFT ekranın pin yazılarının fotoğrafını çek.
5. Fotoğrafları sohbete bırak.

Metal kutuda ESP32 yazıyorsa bir sonraki ders yalnız USB kablosuyla bilgisayara bağlamaktır. ESP32-S3 yazıyorsa dur ve yazıyı gönder.
