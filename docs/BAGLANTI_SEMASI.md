# Bağlantı politikası

Bu belge bağlantı şeması değildir. Pin numarası, gerilim ve direnç değeri yazılmaz. Elektriksel özellikler doğrulanmadan çizilen şema kartı veya probu bozabilir.

## Şimdi bağlanmayanlar

Firmware GPIO, SPI, I2C, ADC, 1-Wire, RS485 ve panel arka ışığını açmaz. İlk yüklemede karta yalnız USB kablo takılır. Sensör, TFT ve 18650 bağlı değildir.

## Bir uç önerilmeden toplanacaklar

Her sinyal için:

1. Kaynağın boşta ve yük altındaki gerilimi, bir ölçer ile
2. Ortak toprak
3. ESP32 tarafının 3,3 V toleransı ile kaynağın seviyesi
4. Çektiği akım
5. Üretici pin sırasının fotoğrafı veya veri kağıdı
6. Varsa yön kontrolü (RS485 DE/RE) ve analog çıkışın en yüksek voltajı

İşlev adları, henüz bir pine bağlanmadan durur:

- Panel: saat, veri, seçim, veri/komut, reset, arka ışık
- Sıcaklık: 1-Wire veri
- pH: modülün kendi çıkışı
- Bulanıklık: modülün kendi çıkışı
- EC: RS485 A, B ve alıcı-verici yönü
- DO: modülün kendi hattı
- Tuşlar: soyut Menü, Yukarı, Aşağı, OK, Geri
- Pil: bölücü orta noktası, ancak oran ve şarj entegresi bilindikten sonra

## Siparişte görülen güç parçaları

12 V 2 A adaptör yalnız LM2596-5V kartının girişine gidebilir. ESP32'nin 5 V veya 3,3 V pinine, TFT'ye ve DS18B20'ye 12 V bağlanmaz.

LM2596 çıkışı, başka hiçbir karta bağlı değilken UT33B+ ile ölçülür. Beklenen boşta değer yaklaşık 5,0 V'tur. 5,3 V üzerindeyse modül kullanılmaz.

İlk açılış Micro USB veri kablosuyladır. Adaptör ve regülatör o denemeye girmez.

## Hasar riski

- 5 V çıkışlı bir modül, 3,3 V GPIO'ya doğrudan bağlanmaz.
- Analog çıkış, ADC'ye girmeden önce en yüksek gerilimi ölçülür.
- RS485 A/B uçları, alıcı-verici ve yön pinleri tanınmadan işlemciye lehimlenmez.
- 1-Wire hattının çekme gerilimi, işlemci pin gerilimi ile aynı ailede olmalıdır.
- Li-ion hücre, koruma ve şarj entegresi şemadan okunmadan yuvaya takılmaz. Ters polarite ve korumasız şarj yangın riskidir.
- TFT arka ışığı, gerilimi ve akımı bilinmeden bir GPIO'ya bağlanmaz.

Ölçüler ve conta notu: `hardware/OLCULER.md`. 3D baskı kasa tek başına IP65 belgesi değildir.
