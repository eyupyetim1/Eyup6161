# Donanım envanteri

Bu liste satın alma planıdır, stok sayımı değildir. Durum sütunu `DOĞRULANMADI` iken yazılım o parçayı sürüyormuş gibi davranmaz.

Her parça için şu alanlar doldurulmadan sürücü ve kablo işi açılmaz: model / ipek baskı, besleme gerilimi, akım, haberleşme, mantık seviyesi, gerekli uçlar, kütüphane ve lisansı, koruma, kalibrasyon yöntemi.

| Parça | Planlanan rol | Doğrulanacak bilgi | Durum |
| --- | --- | --- | --- |
| Kontrol kartı | Ana işlemci | ESP32 mi ESP32-S3 mi? Modül yazısı (ör. WROOM). Geliştirme kartı mı özel kart mı? USB-UART köprüsü mü doğal USB mi? 3,3 V regülatörün akımı. 5 V pini var mı? Resmi pin şeması | DOĞRULANMADI |
| TFT | 2,8 inç 320×240 | Sürücü çipi gerçekten ILI9341 mi? Mantık gerilimi. Arka ışık gerilimi ve akımı. Header uç sırası. SPI saat yönü. Dokunmatik var mı? | DOĞRULANMADI |
| DS18B20 | Su sıcaklığı | Su geçirmez prob mu? Kablo boyu. VDD beslemeli mi, parazit güç mü? Çekme direnci değeri ve gerilimi | DOĞRULANMADI |
| pH modülü ve prob | pH | Modülün tam adı. Besleme. Çıkış analog mu, seri mi? Çıkış gerilim aralığı. BNC. Sıcaklık telafisi var mı? | DOĞRULANMADI |
| Bulanıklık modülü | NTU | Modül adı. Besleme. Analog mü dijital mi? Ölçüm aralığı. Işık kaynağı | DOĞRULANMADI |
| EC probu | İletkenlik | Üretici, Modbus adresi, baud, register haritası. RS485 alıcı-vericinin gerilimi ve yön uçları | DOĞRULANMADI |
| DO modülü | Çözünmüş oksijen | Modül adı, protokol, besleme, membran tipi, sıcaklık telafisi | DOĞRULANMADI |
| 18650 | Taşınabilir besleme | Korumalı hücre mi? Kapasite. Üretici | DOĞRULANMADI |
| Şarj ve güç yolu | Şarj, koruma, regülasyon | Entegre adı. Şarj akımı. Ayrı koruma var mı? Güç yolu (power-path) var mı? Çıkış 3,3 V mu 5 V mu? USB-C CC dirençleri | DOĞRULANMADI |
| Tuşlar | Menü, yukarı, aşağı, OK, geri | Adet, anlık mı kilitli mi, basılı seviye, ortak uç | DOĞRULANMADI |
| Kasa | Taşınabilir gövde | Ölçüler `hardware/OLCULER.md` dolmadan CAD yok | DOĞRULANMADI |

ESP32 ile ESP32-S3 aynı GPIO numarasına sahip değildir. Eski bir kartın pin tablosu bu ürüne taşınmaz.

Yazılım hedefi ile kart kimliği ayrıdır. `esp32dev` ortamı "ESP32 hedefi, kart doğrulanmadı" yazar. `boardIdentityConfirmed()` bu sürümde false döner.
