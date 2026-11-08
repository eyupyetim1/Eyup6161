# Donanım envanteri

Sipariş başlığı, parçanın elde olduğunu gösterir. İpek baskı, pin sırası ve ölçülmüş gerilim gelmeden yazılım o parçayı sürüyormuş gibi davranmaz.

Her parça için şu alanlar doldurulmadan sürücü ve kablo işi açılmaz: model / ipek baskı, besleme gerilimi, akım, haberleşme, mantık seviyesi, gerekli uçlar, kütüphane ve lisansı, koruma, kalibrasyon yöntemi.

| Parça | Planlanan rol | Sipariş kaydı | Doğrulanacak bilgi | Durum |
| --- | --- | --- | --- | --- |
| Kontrol kartı | Ana işlemci | ESP32 ESP-32S Wi-Fi + Bluetooth geliştirme kartı, 30 pin, 2 adet | Metal kutu yazısı. USB-UART çipinin adı. 5 V ve 3,3 V pinlerinin ipek baskısı | SİPARİŞTE VAR |
| TFT | 2,8 inç 240×320 | SPI dokunmatik TFT, ILI9341 yazılı, 1 adet | Header’da basılı pin adları. VCC ve LED gerilimi, ölçülmüş | SİPARİŞTE VAR |
| DS18B20 | Su sıcaklığı | Su geçirmez dijital sıcaklık sensörü, 2 adet | Kablo renkleri. Besleme 3,3 V iken veri ucunun gerilimi | SİPARİŞTE VAR |
| pH modülü ve prob | pH | Bu ekran görüntülerinde yok | Modül adı, besleme, çıkış aralığı | BU LİSTEDE YOK |
| Bulanıklık modülü | NTU | Bu ekran görüntülerinde yok | Modül adı, besleme, analog veya dijital çıkış | BU LİSTEDE YOK |
| EC probu | İletkenlik | Bu ekran görüntülerinde yok | Üretici, Modbus adresi, baud, register haritası | BU LİSTEDE YOK |
| DO modülü | Çözünmüş oksijen | Bu ekran görüntülerinde yok | Modül adı, protokol, besleme | BU LİSTEDE YOK |
| 18650 ve şarj | Taşınabilir besleme | Bu ekran görüntülerinde yok | Korumalı hücre, şarj entegresi, koruma | BU LİSTEDE YOK |
| Tezgah beslemesi | İlk masa denemesi | LM2596-5V regülatör, 2 adet. 12 V 2 A adaptör, 5,5×2,1 mm. Dişi DC klemens, 12 adet | LM2596 çıkışı yüksüz ölçülür. 12 V doğrudan ESP32'ye girmez | SİPARİŞTE VAR |
| Tuşlar | Menü, yukarı, aşağı, OK, geri | 4 pinli 6×6×10 mm push buton, 10 adet | Beş işlev için beş tuş. Basılı seviye breadboard üzerinde ölçülür | SİPARİŞTE VAR |
| Mikro SD | İleride kayıt | Mikro SD kart modülü, 1 adet | Modülün 3,3 V mu 5 V mu olduğu. MISO seviyesi | SİPARİŞTE VAR, KAPALI |
| Kasa | Taşınabilir gövde | Yok | Ölçüler `hardware/OLCULER.md` dolmadan CAD yok | YOK |

Aynı siparişte ayrıca jumper setleri, iki büyük breadboard, direnç kiti, 50 adet 4,7 kΩ direnç, kondansatör seti, 24 AWG çok damar kablo, üç adet Micro USB veri kablosu ve Unit UT33B+ multimetre var. 4,7 kΩ, DS18B20 veri hattının çekme direnci için uygun değerdir. Direncin bir ucu veri hattına, diğer ucu probun 3,3 V beslemesine gider. Prob 5 V ile beslenirse veri ucu da 5 V'a çekilir; ESP32 GPIO buna dayanıklı değildir.

İlk yazılım hedefi bu siparişe göre `esp32dev` olur. Metal kutuda ESP32-S3 yazıyorsa yükleme durur. `boardIdentityConfirmed()` hâlâ false'tur; pin numarası üretilmedi.

ESP32 ile ESP32-S3 aynı GPIO numarasına sahip değildir. Eski bir kartın pin tablosu bu ürüne taşınmaz.

Yazılım hedefi ile kart kimliği ayrıdır. `esp32dev` ortamı "ESP32 hedefi, kart doğrulanmadı" yazar. `boardIdentityConfirmed()` bu sürümde false döner.
