#pragma once

#include "bluecore/sensor.hpp"

// Demo senaryosu.
//
// Görev: ekran yerleşimini gerçek prob olmadan göstermek.
// İlişkili dosyalar: App, policy, ui_model.
// Donanım: yok. Sensör sürücüsü değildir ve hub'a yazmaz.
// Etki: buradaki sayılar saha kaynağına kopyalanırsa demo/saha ayrımı bozulur.

namespace bluecore {
namespace demo_values {

constexpr float kTemperatureC = 22.6f;
constexpr float kPh = 7.42f;
constexpr float kDissolvedOxygenMgL = 8.14f;
constexpr float kConductivityUs = 485.0f;
constexpr float kTurbidityNtu = 2.4f;

}  // namespace demo_values

class DemoScenario : public IMeasurementSource {
 public:
    DemoScenario();
    void update(uint32_t nowMs) override;
    const Sample& get(SensorId id) const override;

 private:
    Sample samples_[kSensorCount] = {};
    Sample empty_{};
};

}  // namespace bluecore
