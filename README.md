# BlueCore BC-01

Taşınabilir su kalitesi ölçer. Sürüm **0.1.0** derlenebilir ürün iskeletidir. Sensör, panel ve pil devresi bu sürümde sürülmez.

## Bu sürümde çalışanlar

- PlatformIO projesi, ESP32 ve ESP32-S3 hedefleri
- Donanımdan bağımsız uygulama çekirdeği
- Açılış, canlı ölçüm, menü ve bilgi ekranları
- Demo senaryosu ile saha ölçümünün ayrı kaynakları
- Tuş mantığı: debounce, uzun basma, tekrar
- Host üzerinde birim testleri ve 320×240 önizleme

## Bu sürümde kapalı olanlar

- TFT, GPIO, SPI, ADC ve Wi-Fi başlatılmaz
- Pil yüzdesi ve şarj durumu üretilmez
- Takvim saati doğrulanmış gibi gösterilmez
- Kalibrasyon katsayısı uygulanmaz
- Ölçüm flash'a yazılmaz
- Bağlı olmayan sensör için sayı uydurulmaz

Demo modundaki 22.6 °C, 7.42 pH, 8.14 mg/L, 485 µS/cm ve 2.4 NTU senaryo değeridir. Ekranda **DEMO** ve **SENARYO** yazar. Saha modunda aynı yerler `—` ve `BAĞLI DEĞİL` olur.

## Derleme ve test

Host testleri (kart gerekmez):

```bash
make test
make preview
```

Önizlemeler `docs/ui/` altına yazılır.

Cihaz derlemesi, modülün üzerindeki yazı okunmadan yüklenmez. Ortam seçimi `docs/ILK_DENEME.md` içindedir.

```bash
pio run -e esp32dev
pio run -e esp32-s3-devkitc-1
```

Saha modu derlemesi: `esp32dev-field`. Demo modu varsayılandır.

## Güvenlik

`docs/BAGLANTI_SEMASI.md` bağlantı şeması değildir. Gerilim, pin ve sensör modeli doğrulanmadan kablo önerilmez. 18650 hücresi, koruma ve şarj entegresi tanınmadan monte edilmez.

## Belgeler

- [Mimari](docs/MIMARI.md)
- [Donanım envanteri](docs/DONANIM_ENVANTERI.md)
- [Bağlantı politikası](docs/BAGLANTI_SEMASI.md)
- [Sensörler](docs/SENSORLER.md)
- [Kalibrasyon](docs/KALIBRASYON.md)
- [Arayüz](docs/ARAYUZ.md)
- [Test planı](docs/TEST_PLANI.md)
- [Geliştirme günlüğü](docs/GELISTIRME_GUNLUGU.md)
- [İlk cihaz denemesi](docs/ILK_DENEME.md)

Yazı tipi: Inter 4.001 bitmap alt kümesi, SIL OFL 1.1. Bakınız `third_party/inter/`.
Ürün yazılımının lisansı henüz seçilmedi.
