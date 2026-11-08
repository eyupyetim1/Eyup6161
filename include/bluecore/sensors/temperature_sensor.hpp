#pragma once

#include "bluecore/sensors/unverified_sensor.hpp"

// Sıcaklık sensörü.
//
// Görev: DS18B20 su sıcaklığını ölçüm modeline taşımak.
// İlişkili dosyalar: catalog, sensor_hub, canlı ölçüm satırı.
// Donanım: DS18B20, 1-Wire. Parça ve pin doğrulanmadı; veri hattı açılmaz.
// Etki: okuma eklendiğinde hub, kayıt ve SICAKLIK satırı etkilenir.

namespace bluecore {

class TemperatureSensor : public UnverifiedSensor {
 public:
    TemperatureSensor();
};

}  // namespace bluecore
