# Test planı

## Bu sürümde çalıştırılanlar

Host, kart olmadan:

```bash
make test
make preview
```

`make test` sınır denetimini de çalıştırır: `src` ve `include` içinde GPIO, SPI, Wi-Fi veya panel sürücüsü çağrısı varsa durur. `Arduino.h` yalnız `src/main.cpp` içindedir.

Kapsanan davranış:

- Demo sayısı saha ekranına çıkmaz; saha sayısı demo ekranına çıkmaz
- Demo modunda hub durumu bağlı değil kalır
- Kökeni boş, aralık dışı, NaN, kalibrasyonsuz pH ve modeli belirsiz bulanıklık sayı olmaz
- EC 1000 µS/cm eşiğinde gösterim mS/cm olur; örnek kanonik kalır
- Demo kayıt reddedilir; doğrulanmamış takvim alanları silinir
- Kalibrasyon `apply` çıkışı değiştirmez
- Saat `Verified` üretmez
- Pil yüzdesi ve radyo kapalıdır
- Debounce, uzun basma, tekrar ve menü gezinmesi
- Bir sensör hatası diğerinin okumasını kesmez
- Menüdeki son satır ve sistem sayfasındaki son satır alt bilgiyle çakışmaz
- Arayüz dizeleri gömülü fontun içindedir
- Açılış şeridi tema rengindedir; demo ve saha kareleri farklıdır

Cihaz derlemesi PlatformIO ile ayrıca koşulur. Sonuç `GELISTIRME_GUNLUGU.md` içindedir. Yükleme yapılmadı. "Cihazda görüldü" denmez.

## Donanım gelince

- Doğru `pio` ortamı ile yükleme, seri açılış metni
- TFT ancak pin ve gerilim doğrulandıktan sonra, tek geçiş boyama, titreme kontrolü
- Her prob tek başına: bağlı değil, hata, geçerli okuma
- Prob çıkarılınca arayüz ayakta kalır
- Kalibrasyonsuz pH/EC/DO sayı basmaz
- Güç kesilince yarım kayıt CRC ile düşer
- Düşük pil davranışı, ancak bölücü oranı ölçülünce
- Wi-Fi tarama, kimlik bilgisi cihaz dışı bir yerde düz metin loglanmadan

Watchdog, bloklayan sürücü yazılınca açılır.
