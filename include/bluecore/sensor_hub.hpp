#pragma once

#include "bluecore/sensor.hpp"

// Sensör hub'ı.
//
// Görev: propları sırayla yoklamak. Birinin hatası diğerinin örneğini silmez.
// İlişkili dosyalar: src/sensors/sensor_hub.cpp, App.
// Donanım: bağlı somut sensörün donanımı. Bu sürümdeki sınıflar pin açmaz.
// Etki: update() içine bekleme konursa arayüz döngüsü de bekler.

namespace bluecore {

class SensorHub : public IMeasurementSource {
 public:
    SensorHub();

    void bind(ISensor& sensor);
    void initializeAll();
    void update(uint32_t nowMs) override;
    const Sample& get(SensorId id) const override;
    ISensor* sensor(SensorId id);
    const ISensor* sensor(SensorId id) const;

 private:
    ISensor* sensors_[kSensorCount] = {};
    Sample empty_{};
};

}  // namespace bluecore
