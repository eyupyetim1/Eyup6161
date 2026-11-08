#pragma once

#include "bluecore/sensors/unverified_sensor.hpp"

// pH sensörü.
//
// Görev: pH probunu ölçüm modeline taşımak.
// İlişkili dosyalar: calibration, catalog, canlı ölçüm satırı.
// Donanım: pH modülü ve prob. Model, gerilim ve analog/dijital arayüz doğrulanmadı.
// Etki: kalibrasyonsuz sayı gösterilmez. Sürücü eklenirse kalibrasyon servisi zorunlu kalır.

namespace bluecore {

class PhSensor : public UnverifiedSensor {
 public:
    PhSensor();
};

}  // namespace bluecore
