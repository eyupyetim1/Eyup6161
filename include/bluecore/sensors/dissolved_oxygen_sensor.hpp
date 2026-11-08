#pragma once

#include "bluecore/sensors/unverified_sensor.hpp"

// Çözünmüş oksijen sensörü.
//
// Görev: DO ölçümünü mg/L olarak modele taşımak.
// İlişkili dosyalar: calibration, catalog, canlı ölçüm satırı.
// Donanım: DO modülü. Protokol, gerilim ve prob modeli doğrulanmadı.
// Etki: sürücü eklenirse kalibrasyon ve sıcaklık telafisi aynı anda ele alınmalıdır.

namespace bluecore {

class DissolvedOxygenSensor : public UnverifiedSensor {
 public:
    DissolvedOxygenSensor();
};

}  // namespace bluecore
