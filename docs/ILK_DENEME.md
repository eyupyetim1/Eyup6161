# İlk cihaz denemesi

Amaç: işlemcinin açıldığını ve seri metni verdiğini görmek. Sensör, ekran ve pil bu adımda bağlanmaz.

## Önce

1. Modülün üzerindeki yazıyı oku. ESP32 ile ESP32-S3 farklı yazılımdır.
2. USB soketinin yanında UART köprüsü (ayrı USB-seri çip) mi var, yoksa doğrudan USB mi yazıyor, not et.
3. Kartın ısınmadığından ve kısa devre olmadığından emin ol.
4. Veri ileten bir USB kablo kullan.

## Ortam

| Gözlem | Komut |
| --- | --- |
| Klasik ESP32 modülü | `pio run -e esp32dev -t upload` |
| ESP32-S3, UART köprülü USB | `pio run -e esp32-s3-devkitc-1 -t upload` |
| ESP32-S3, doğal USB ve seri sessiz kaldıysa | `pio run -e esp32-s3-usb -t upload` |

Yanlış aile genellikle yüklemeyi reddeder. Yine de yazıyı okumadan ortam seçilmez. Yükleme hızı 115200.

## Seri

115200 8N1. Beklenen açılış metninde şunlar bulunur:

- `BLUECORE BC-01`
- `Sürüm 0.1.0`
- `Mod: DEMO` (field ortamında `SAHA`)
- `kart doğrulanmadı`
- `TFT başlatılmadı`
- `GPIO kullanılmıyor`
- `Canlı sayılar senaryo verisidir` veya saha cümlesi

Tuşlar: `m` menü, `u` yukarı, `d` aşağı, `o` seç, `b` geri. İlk `m` veya `o` açılışı geçer. Beş saniyede bir `BC-01 yaşam` satırı gelir.

Ekranda çizim kodu çalışır fakat panele gitmez. Panelin boş kalması bu sürümün beklenen sonucudur.

## Dur

- Kart ısınıyorsa USB'yi çek.
- Sürekli reset veya brownout varsa beslemeyi ve kabloyu kontrol et, sensör takarak "düzeltmeye" çalışma.
- Seri metin demo sayıyı gerçek prob ölçümü gibi anlatmıyorsa sürüm karışmış demektir; `SENARYO` veya saha tire'ı görünmeli.

Bu listeden sonra TFT veya prob bağlamak için `BAGLANTI_SEMASI.md` doldurulmuş olmalıdır. 18650 bu denemeye girmez.
