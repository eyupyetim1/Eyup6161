#include "bluecore/storage.hpp"

#include "bluecore/policy.hpp"

namespace bluecore {
namespace {

void sanitizeSample(Sample& sample, uint8_t& stripped) {
    const bool claim = sample.hasValue && numericClaimAllowed(sample) &&
                       sample.origin == DataOrigin::LiveSensor;
    if (claim) {
        return;
    }
    if (sample.hasValue != 0) {
        ++stripped;
    }
    const ErrorCode previous = sample.error;
    const SensorStatus status = sample.status;
    const DataOrigin origin = sample.origin;
    sample.hasValue = 0;
    sample.value = 0.0f;
    sample.status = status;
    if (origin == DataOrigin::DemoScenario) {
        sample.origin = DataOrigin::DemoScenario;
        sample.error = ErrorCode::DemoNotLoggable;
        return;
    }
    sample.origin = origin == DataOrigin::LiveSensor ? DataOrigin::LiveSensor : DataOrigin::None;
    if (previous != ErrorCode::None) {
        sample.error = previous;
    }
}

}  // namespace

bool UnavailableLogStore::append(LogRecord) { return false; }

uint32_t UnavailableLogStore::count() const { return 0; }

bool UnavailableLogStore::get(uint32_t, LogRecord&) const { return false; }

bool MemoryLogStore::append(LogRecord record) {
    uint8_t stripped = 0;
    bool live = false;
    for (int i = 0; i < kSensorCount; ++i) {
        sanitizeSample(record.samples[i], stripped);
        if (record.samples[i].hasValue != 0) {
            live = true;
        }
    }
    record.stripped = stripped;
    if (!live) {
        return false;
    }
    if (record.timeQuality != TimeQuality::Verified) {
        record.year = 0;
        record.month = 0;
        record.day = 0;
        record.hour = 0;
        record.minute = 0;
        record.second = 0;
        if (record.timeQuality == TimeQuality::Unknown) {
            record.timeQuality = TimeQuality::UptimeOnly;
        }
    }
    if (count_ >= static_cast<uint32_t>(kCapacity)) {
        return false;
    }
    record.id = nextId_++;
    records_[count_++] = record;
    return true;
}

uint32_t MemoryLogStore::count() const { return count_; }

bool MemoryLogStore::get(uint32_t index, LogRecord& out) const {
    if (index >= count_) {
        return false;
    }
    out = records_[index];
    return true;
}

}  // namespace bluecore
