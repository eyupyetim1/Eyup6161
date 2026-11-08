# Mimari

BlueCore BC-01 sürüm 0.1.0, ölçüm işini panel ve prob sürücülerinden ayırır. Donanım kimliği doğrulanmadan sürücü başlatılmaz.

```
Arayüz (ui)
    ekran modeli ve çizim
Uygulama (core/app)
    mod, döngü, özdenetim
Ölçüm kaynakları          Girdi            Güç / radyo / saat / kayıt
    DemoScenario              ButtonLogic      hepsi "bilinmiyor" veya "kapalı"
    SensorHub                 Navigation
        beş sensör yuvası
Çizim yüzeyi
    NullCanvas   cihaz, pin açmaz
    Framebuffer  yalnız host önizlemesi
```

## Çerçeve kararı

Faz 1 **Arduino-ESP32** ve **PlatformIO** kullanır. Gerekçe:

- ESP32 ve ESP32-S3 aynı uygulama kaynağıyla ayrı `platformio.ini` ortamlarında derlenir.
- İleride doğrulanacak TFT ve prob kütüphanelerinin olgun halleri bu çerçevededir.
- Uygulama çekirdeği `Arduino.h` içermez. `src/main.cpp` tek cihaz girişidir. Çekirdek daha sonra ESP-IDF görevlerine taşınabilir.

ESP-IDF, seri üretimde görev ayrımı, bölümlenmiş flash ve güvenlik için daha uygun tabandır. Kart modeli ve panel pinleri bilinmeden o geçiş yapılmadı. Karar geçicidir; kart doğrulanınca yeniden değerlendirilir.

Platform sabiti: `espressif32@8.0.0`.

## Demo ve saha

İki ölçüm kaynağı vardır ve aynı hücreye yazmaz.

| | Demo | Saha |
| --- | --- | --- |
| Kaynak | `DemoScenario` | `SensorHub` |
| `DataOrigin` | `DemoScenario` | canlı prob `LiveSensor`, yoksa `None` |
| Sayı | senaryo değerleri | yalnız politika izin verirse |
| Donanım durumu | hub ayrı tutulur, bu sürümde bağlı değil | aynı |

Canlı ekran, demo modunda yalnız `DemoScenario` kökenli sayıyı, saha modunda yalnız `LiveSensor` kökenli sayıyı basar. Politika ayrıca şunları sayı saymaz:

- `hasValue == 0`
- sonsuz veya aralık dışı değer
- kökeni `None` olan dolu örnek
- kullanıcı kalibrasyonu gereken pH, EC veya DO, katsayı yokken
- modeli doğrulanmamış bulanıklık

Sıcaklıkta kullanıcı kalibrasyonu gerekmez. Parça doğrulanıp gerçek okuma gelene kadar yuva yine `bağlı değil` kalır.

## Zaman

`UptimeClock` yalnız açılıştan beri milisaniye verir. `TimeQuality::Verified` üreten sınıf yoktur. Doğrulanmamış kayıtta takvim alanları sıfırlanır.

## Kayıt

Cihaz `UnavailableLogStore` kullanır; flash yazılmaz. Testlerdeki `MemoryLogStore` demo sayısını ve kalibrasyonsuz pH/EC/DO sayısını kayda koymaz. İleride ayarlar seyrek NVS yazımı, ölçümler ise CRC'li ekleme günlüğü olarak ayrı bölüme gider. Saniyede bir flash yazımı planlanmaz.

## Güç ve radyo

`UnverifiedPowerMonitor` gerilim ve yüzde bilmez. `DormantRadio` Wi-Fi başlatmaz. Ağ yokken ölçüm kararı yereldedir; bu sürümde saha ölçümü zaten sensörsüzdür.

## İzleme

Watchdog açılmaz. Gerekçe: bloklayan sürücü yoktur; ilk seri denemede görev zaman aşımı yanlış reset üretir. Bir sürücü beklemeye başlarsa `read()` için 100 ms bütçe ve yaklaşık 5 sn görev watchdog'u ayrıca ele alınır.

Ekran modeli değişmeden yeniden çizim istenmez. Panel bağlanınca boyama tek geçişte yapılmalıdır; glyph arasında panel silinmez.

## Gelecek JSON modeli

Uç yoktur. Alanlar, ekrandaki ayrımla aynı kalmalıdır:

```json
{
  "product": "BLUECORE",
  "model": "BC-01",
  "firmware": "0.1.0",
  "mode": "field",
  "time": { "quality": "uptime", "monotonicMs": 0 },
  "power": { "percentKnown": false, "voltageKnown": false },
  "radio": { "status": "disabled" },
  "samples": [
    {
      "id": "temperature",
      "origin": "none",
      "status": "not_connected",
      "value": null,
      "unit": "C",
      "calibrationValid": false
    }
  ]
}
```

Kimlik doğrulama ve TLS, radyo açılmadan tasarım notu olarak kalır. Cihaz kimliği için çip eFuse veya üretimde yazılmış bir kayıt düşünülür; bu sürümde okunmaz.

## Lisanslar

- Ürün kaynak lisansı seçilmedi.
- Bitmap yazı tipi Inter 4.001 alt kümesidir, SIL OFL 1.1. Ayrılmış ad kullanılmaz.
- Arduino-ESP32 PlatformIO ile gelir, Apache-2.0. Depoya kopyalanmadı.
- OneWire kütüphanesinin LGPL koşulları ticari statik bağlama için ayrıca karar ister. Bu sürümde bağlanmadı.
- TFT_eSPI ve LovyanGFX bağlanmadı. Panel doğrulanınca lisans metni kaynaktan okunur.
