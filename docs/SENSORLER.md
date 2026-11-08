# Sensörler

Her yuva `initialize`, `read`, `calibrate`, `getStatus`, `getLastMeasurement` ve `getError` sözleşmesini taşır. `read` çabuk döner. Bu sürümdeki sınıflar pin açmaz ve `NotConnected` / `HardwareUnverified` bırakır.

Hub bir yuvayı okuyamazsa diğer yuvanın örneğini silmez. Test bunu iki sahte sensörle doğrular.

Akla yatkınlık sınırları kalibrasyon noktası değildir. Datasheet gelene kadar ürünün reddedeceği geçici aralıktır.

| Kimlik | Ekran | Plan | Kullanıcı kalibrasyonu | Geçici aralık | Sürücü |
| --- | --- | --- | --- | --- | --- |
| Sıcaklık | SICAKLIK °C | DS18B20, 1-Wire | Gerekmez (fabrika). Parça yine doğrulanmadı | −2 … 60 °C | Kapalı |
| pH | pH | Modül ve prob, model yok | Gerekir | 0 … 14 | Kapalı |
| Oksijen | OKSİJEN mg/L | DO modülü, protokol yok | Gerekir | 0 … 20 mg/L | Kapalı |
| EC | EC, kanonik µS/cm | RS485 / Modbus prob | Gerekir | 0 … 200000 µS/cm | Kapalı |
| Bulanıklık | BULANIKLIK NTU | Modül adı yok | Model bilinmiyor; canlı NTU iddiası kapalı | 0 … 1000 NTU | Kapalı |

EC gösterimi 1000 µS/cm ve üzerinde mS/cm yazar. Kayıt kanonik µS/cm tutar. Bu yalnız gösterim ölçeğidir.

DallasTemperature / OneWire bu ağaca bağlanmadı. LGPL bağlama kararı ve gerçek prob doğrulaması birlikte gerekir.

Modbus kütüphanesi de bağlanmadı. Adres, baud ve register haritası prob kağıdından okunacak.

Demo senaryosu bir sensör sınıfı değildir. Hub'a yazmaz.
