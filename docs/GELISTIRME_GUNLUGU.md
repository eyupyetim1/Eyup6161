# Geliştirme günlüğü

## 2026-11-08 — Faz 1 iskelet

Depo boştu. BlueCore BC-01 için derlenebilir çekirdek kuruldu.

Yapılanlar:

- PlatformIO ortamları: `esp32dev`, `esp32dev-field`, `esp32-s3-devkitc-1`, `esp32-s3-usb`, `esp32-s3-usb-field`
- Saf C++ çekirdek: ölçüm politikası, demo senaryosu, sensör yuvaları, kalibrasyon kapısı, kayıt süzgeci, saat, pil, radyo, alarm, tuş ve gezinme
- 320×240 açılış, canlı ölçüm, menü ve bilgi ekranları
- Cihazda `NullCanvas`; pin açılmaz
- Host testleri ve PNG önizleme
- Donanım envanteri ve bağlantı politikası; şema üretilmedi

Bilinçli olarak yapılmayanlar:

- Gerçek sensör okuması, TFT, Wi-Fi, pil ADC, flash kayıt
- Kasa CAD'i
- Kart üzerine yükleme

Host testi bu oturumda `make test` ile geçti. Çıktı: `tum kontroller gecti`. `make preview` 320×240 PNG üretti.

Cihaz derlemesi PlatformIO `espressif32@8.0.0` ile geçti. Yükleme yapılmadı; kart bağlı değildi.

| Ortam | Sonuç | RAM | Flash |
| --- | --- | --- | --- |
| esp32dev | geçti | 26148 / 327680 bayt | 329092 / 1310720 bayt |
| esp32dev-field | geçti | 26148 / 327680 bayt | 329092 / 1310720 bayt |
| esp32-s3-devkitc-1 | geçti | 25624 / 327680 bayt | 329993 / 3342336 bayt |
| esp32-s3-usb | geçti | 26360 / 327680 bayt | 354566 / 3342336 bayt |
| esp32-s3-usb-field | geçti | 26360 / 327680 bayt | 354566 / 3342336 bayt |

Sonraki iş, kartın ipek baskısı ve panel header fotoğrafı geldikten sonra tek bir doğrulanmış pin profili açmaktır. Ondan önce sürücü eklenmez.

## 2026-11-08 — İlk sipariş görüntüleri

Teslim edilmiş sipariş ekranından şu parçalar kayda geçti: 30 pin ESP-32S geliştirme kartı (2), ILI9341 2,8 inç dokunmatik TFT (1), su geçirmez DS18B20 (2), LM2596-5V (2), 12 V 2 A adaptör, 6×6 mm tuş (10), mikro SD modülü, breadboard, jumper, direnç ve UT33B+ multimetre.

pH, EC, çözünmüş oksijen, bulanıklık, 18650 ve şarj kartı bu görüntülerde yok. Kullanıcı başka siparişler olduğunu söyledi; onlar gelmeden envanter tamamlanmış sayılmaz.

Pin atanmadı. 12 V adaptörün ESP32'ye doğrudan bağlanmayacağı bağlantı politikasına yazıldı. LM2596 çıkışı ölçülmeden kullanılmayacak.

Aynı gün parça adlarını ve lehimden önceki ilk dersi `docs/SIFIRDAN_ILK_DERS.md` içine yazdım. İlk fiziksel iş fotoğraf; kablo ve havya yok.
