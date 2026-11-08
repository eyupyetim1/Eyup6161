#include "bluecore/sensor_hub.hpp"

namespace bluecore {

SensorHub::SensorHub() {
    empty_.status = SensorStatus::NotConnected;
    empty_.origin = DataOrigin::None;
    empty_.error = ErrorCode::NotConnected;
    empty_.timeQuality = TimeQuality::UptimeOnly;
}

void SensorHub::bind(ISensor& sensor) {
    const int index = static_cast<int>(sensor.id());
    if (index < 0 || index >= kSensorCount) {
        return;
    }
    sensors_[index] = &sensor;
}

void SensorHub::initializeAll() {
    for (int i = 0; i < kSensorCount; ++i) {
        if (sensors_[i] != nullptr) {
            sensors_[i]->initialize();
        }
    }
}

void SensorHub::update(uint32_t nowMs) {
    for (int i = 0; i < kSensorCount; ++i) {
        if (sensors_[i] != nullptr) {
            sensors_[i]->read(nowMs);
        }
    }
}

const Sample& SensorHub::get(SensorId id) const {
    const int index = static_cast<int>(id);
    if (index < 0 || index >= kSensorCount || sensors_[index] == nullptr) {
        return empty_;
    }
    return sensors_[index]->getLastMeasurement();
}

ISensor* SensorHub::sensor(SensorId id) {
    const int index = static_cast<int>(id);
    if (index < 0 || index >= kSensorCount) {
        return nullptr;
    }
    return sensors_[index];
}

const ISensor* SensorHub::sensor(SensorId id) const {
    const int index = static_cast<int>(id);
    if (index < 0 || index >= kSensorCount) {
        return nullptr;
    }
    return sensors_[index];
}

}  // namespace bluecore
