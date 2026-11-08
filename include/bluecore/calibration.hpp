#pragma once

#include "bluecore/types.hpp"

// Kalibrasyon servisi.
//
// Görev: sensöre özel kalibrasyonun kapısını tutmak. Ortak bir katsayı uygulamaz.
// İlişkili dosyalar: src/calibration/calibration.cpp, sensör calibrate() çağrıları.
// Donanım: prosedür, prob modeli doğrulanmadan başlatılmaz.
// Etki: apply() gerçek katsayı döndürmeye başlarsa canlı pH, EC ve DO ekranı değişir.

namespace bluecore {

class CalibrationService {
 public:
    CalibrationResponse start(const CalibrationRequest& request) const;
    CalibrationState state(SensorId id) const;
    bool hasCoefficients(SensorId id) const;
    bool apply(SensorId id, float raw, float& scaled) const;
    bool factoryReset();
};

CalibrationResponse refuseUnverified(SensorId id);

}  // namespace bluecore
