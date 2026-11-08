#pragma once

#include "bluecore/types.hpp"

// Sensör sözleşmesi.
//
// Görev: her probun başlatma, okuma, kalibrasyon ve son ölçüm çağrılarını aynı biçimde sunmak.
// İlişkili dosyalar: src/sensors/*, sensor_hub, calibration.
// Donanım: arayüzün kendisi pin açmaz. Somut sınıflar bu sürümde de açmaz.
// Etki: read() yavaşlarsa canlı ekran da yavaşlar. Sözleşme, okumanın çabuk dönmesini ister.

namespace bluecore {

class ISensor {
 public:
    virtual ~ISensor() = default;

    virtual SensorId id() const = 0;
    virtual const char* name() const = 0;
    virtual const char* part() const = 0;
    virtual void initialize() = 0;
    virtual void read(uint32_t nowMs) = 0;
    virtual CalibrationResponse calibrate(const CalibrationRequest& request) = 0;
    virtual SensorStatus getStatus() const = 0;
    virtual const Sample& getLastMeasurement() const = 0;
    virtual ErrorCode getError() const = 0;
};

class IMeasurementSource {
 public:
    virtual ~IMeasurementSource() = default;
    virtual void update(uint32_t nowMs) = 0;
    virtual const Sample& get(SensorId id) const = 0;
};

}  // namespace bluecore
