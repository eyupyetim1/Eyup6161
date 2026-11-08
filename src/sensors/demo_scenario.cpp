#include "bluecore/demo_scenario.hpp"

#include "bluecore/catalog.hpp"
#include "bluecore/policy.hpp"

namespace bluecore {

DemoScenario::DemoScenario() {
    empty_.status = SensorStatus::NotConnected;
    empty_.origin = DataOrigin::None;
    empty_.error = ErrorCode::NotConnected;
    update(0);
}

void DemoScenario::update(uint32_t nowMs) {
    const float values[kSensorCount] = {
        demo_values::kTemperatureC, demo_values::kPh, demo_values::kDissolvedOxygenMgL,
        demo_values::kConductivityUs, demo_values::kTurbidityNtu,
    };
    for (int i = 0; i < kSensorCount; ++i) {
        Sample& sample = samples_[i];
        sample = Sample{};
        sample.id = static_cast<SensorId>(i);
        sample.unit = catalog(sample.id).unit;
        sample.value = values[i];
        sample.hasValue = 1;
        sample.status = SensorStatus::Ready;
        sample.origin = DataOrigin::DemoScenario;
        sample.timeQuality = TimeQuality::UptimeOnly;
        sample.calibrationValid = 0;
        sample.error = ErrorCode::None;
        sample.monotonicMs = nowMs;
        if (!valueInSanity(sample.id, sample.value)) {
            sample.hasValue = 0;
            sample.value = 0.0f;
            sample.status = SensorStatus::InvalidReading;
            sample.error = ErrorCode::InvalidRange;
        }
    }
}

const Sample& DemoScenario::get(SensorId id) const {
    const int index = static_cast<int>(id);
    if (index < 0 || index >= kSensorCount) {
        return empty_;
    }
    return samples_[index];
}

}  // namespace bluecore
