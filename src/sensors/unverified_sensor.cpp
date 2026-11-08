#include "bluecore/sensors/unverified_sensor.hpp"

#include "bluecore/calibration.hpp"
#include "bluecore/catalog.hpp"

namespace bluecore {

UnverifiedSensor::UnverifiedSensor(SensorId id) : id_(id) { clear(0); }

SensorId UnverifiedSensor::id() const { return id_; }

const char* UnverifiedSensor::name() const { return catalog(id_).shortName; }

const char* UnverifiedSensor::part() const { return catalog(id_).plannedPart; }

void UnverifiedSensor::initialize() { clear(0); }

void UnverifiedSensor::read(uint32_t nowMs) { clear(nowMs); }

CalibrationResponse UnverifiedSensor::calibrate(const CalibrationRequest&) {
    return refuseUnverified(id_);
}

SensorStatus UnverifiedSensor::getStatus() const { return sample_.status; }

const Sample& UnverifiedSensor::getLastMeasurement() const { return sample_; }

ErrorCode UnverifiedSensor::getError() const { return sample_.error; }

void UnverifiedSensor::clear(uint32_t nowMs) {
    sample_ = Sample{};
    sample_.id = id_;
    sample_.unit = catalog(id_).unit;
    sample_.status = SensorStatus::NotConnected;
    sample_.origin = DataOrigin::None;
    sample_.timeQuality = TimeQuality::UptimeOnly;
    sample_.error = ErrorCode::HardwareUnverified;
    sample_.monotonicMs = nowMs;
}

}  // namespace bluecore
