#pragma once

#include "bluecore/sensor.hpp"

// Doğrulanmamış prob tabanı.
//
// Görev: planlanan sensör yuvasını, ölçüm uydurmadan "bağlı değil" durumunda tutmak.
// İlişkili dosyalar: beş sensör sınıfı, sensor_hub.
// Donanım: kullanılmaz. Parça adı katalogdaki plandan gelir.
// Etki: buradan gerçek okuma eklenirse kalibrasyon ve kayıt kuralları da devreye girer.

namespace bluecore {

class UnverifiedSensor : public ISensor {
 public:
    explicit UnverifiedSensor(SensorId id);

    SensorId id() const override;
    const char* name() const override;
    const char* part() const override;
    void initialize() override;
    void read(uint32_t nowMs) override;
    CalibrationResponse calibrate(const CalibrationRequest& request) override;
    SensorStatus getStatus() const override;
    const Sample& getLastMeasurement() const override;
    ErrorCode getError() const override;

 private:
    void clear(uint32_t nowMs);

    SensorId id_;
    Sample sample_{};
};

}  // namespace bluecore
