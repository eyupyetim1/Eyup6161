# Kalibrasyon

`CalibrationService` tek bir genel katsayı tutmaz. `apply()` bu sürümde false döner ve çıkış değerini değiştirmez. `hasCoefficients()` false'tur. `factoryReset()` saklı katsayı olmadığı için boş varsayılanı korur.

`start()` her sensör için aynı cümleyi döndürür: donanım doğrulanmadan kalibrasyon başlamaz. Bu, prosedürün çalıştığı anlamına gelmez.

## Sensöre göre ihtiyaç

- Sıcaklık: DS18B20 için kullanıcı kalibrasyonu planlanmaz. Parça ve 1-Wire hattı doğrulanmadan "fabrika kalibrasyonu geçerli" denmez.
- pH: kullanıcı kalibrasyonu gerekir. Tipik laboratuvar işi iki veya üç tamponladır. Bu projedeki probun tampon noktaları, sıcaklık telafisi ve eğim denklemi model bilinmeden yazılmaz.
- EC: hücre sabiti ve sıcaklık telafisi proba aittir. Register haritası yokken katsayı uydurulmaz.
- DO: doygunluk veya sıfır noktası, membran ve modül talimatına bağlıdır. Talimat yokken adım listesi yazılmaz.
- Bulanıklık: model doğrulanana kadar NTU sayısı gösterilmez. Kalibrasyon ihtiyacı `Unknown`.

Kalibrasyon yokken arayüz kesin doğruluk iddia etmez. Kalibrasyon ekranı "Kesinlik iddia edilmez" der.

İleride her sensörün kendi kaydı olur: tarih, operatör adımı, referans değer, sonuç, katsayılar, NVS. Ortak bir çarpan dosyası olmayacak. Başarısız kalibrasyon bir önceki geçerli katsayıyı sessizce bozmamalıdır. Geçerli katsayı yoksa ölçüm sayı olarak sunulmaz.
