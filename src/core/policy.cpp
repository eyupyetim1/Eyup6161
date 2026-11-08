#include "bluecore/policy.hpp"

#include <cstdio>

#include "bluecore/catalog.hpp"
#include "bluecore/text_util.hpp"

namespace bluecore {

bool valueInSanity(SensorId id, float value) {
    if (!(value == value)) {
        return false;
    }
    if (value > 1.0e9f || value < -1.0e9f) {
        return false;
    }
    const SensorCatalogEntry& entry = catalog(id);
    return value >= entry.sanityMin && value <= entry.sanityMax;
}

bool numericClaimAllowed(const Sample& sample) {
    if (!sample.hasValue) {
        return false;
    }
    if (sample.origin != DataOrigin::LiveSensor && sample.origin != DataOrigin::DemoScenario) {
        return false;
    }
    if (sample.status != SensorStatus::Ready && sample.status != SensorStatus::Measuring) {
        return false;
    }
    if (!valueInSanity(sample.id, sample.value)) {
        return false;
    }
    if (sample.origin == DataOrigin::DemoScenario) {
        return true;
    }
    const CalibrationNeed need = catalog(sample.id).calibration;
    if (need == CalibrationNeed::Unknown) {
        return false;
    }
    if (need == CalibrationNeed::Required && sample.calibrationValid == 0) {
        return false;
    }
    return true;
}

namespace {

const char* deniedStatus(const Sample& sample, bool demoScreen) {
    if (demoScreen && sample.origin == DataOrigin::LiveSensor) {
        return "SAHA ÖLÇÜMÜ";
    }
    if (!demoScreen && sample.origin == DataOrigin::DemoScenario) {
        return "SENARYO";
    }
    if (sample.hasValue && !valueInSanity(sample.id, sample.value)) {
        return "GEÇERSİZ ÖLÇÜM";
    }
    if (sample.origin == DataOrigin::LiveSensor && sample.hasValue &&
        catalog(sample.id).calibration == CalibrationNeed::Required &&
        sample.calibrationValid == 0) {
        return "KALİBRASYON GEREKLİ";
    }
    if (sample.origin == DataOrigin::LiveSensor && sample.hasValue &&
        catalog(sample.id).calibration == CalibrationNeed::Unknown) {
        return "MODEL DOĞRULANMADI";
    }
    return statusLabel(sample.status);
}

int decimalsFor(SensorId id, float value) {
    switch (id) {
        case SensorId::Temperature:
            return 1;
        case SensorId::Ph:
            return 2;
        case SensorId::DissolvedOxygen:
            return 2;
        case SensorId::Conductivity:
            return value >= 1000.0f ? 2 : 0;
        case SensorId::Turbidity:
            return 1;
        case SensorId::Count:
            break;
    }
    return 2;
}

}  // namespace

void formatFixed(char* out, size_t capacity, float value, int decimals) {
    if (capacity == 0) {
        return;
    }
    if (out == nullptr) {
        return;
    }
    if (!(value == value)) {
        copyText(out, capacity, "—");
        return;
    }
    if (decimals < 0) {
        decimals = 0;
    }
    if (decimals > 3) {
        decimals = 3;
    }
    const bool negative = value < 0.0f;
    double magnitude = negative ? -static_cast<double>(value) : static_cast<double>(value);
    int scale = 1;
    for (int i = 0; i < decimals; ++i) {
        scale *= 10;
    }
    const long scaled = static_cast<long>(magnitude * static_cast<double>(scale) + 0.5);
    const long integer = decimals == 0 ? scaled : scaled / scale;
    const long fraction = decimals == 0 ? 0 : scaled % scale;
    if (decimals == 0) {
        std::snprintf(out, capacity, "%s%ld", negative ? "-" : "", integer);
    } else {
        std::snprintf(out, capacity, "%s%ld.%0*ld", negative ? "-" : "", integer, decimals, fraction);
    }
}

Presentation present(const Sample& sample, bool demoScreen) {
    Presentation out{};
    copyText(out.value, sizeof(out.value), "—");
    const bool modeMatches = demoScreen ? sample.origin == DataOrigin::DemoScenario
                                         : sample.origin == DataOrigin::LiveSensor;
    if (!modeMatches || !numericClaimAllowed(sample)) {
        copyText(out.status, sizeof(out.status), deniedStatus(sample, demoScreen));
        return out;
    }

    out.showNumber = 1;
    float shown = sample.value;
    const char* unit = catalog(sample.id).unitText;
    if (sample.id == SensorId::Conductivity && sample.value >= 1000.0f) {
        shown = sample.value / 1000.0f;
        unit = "mS/cm";
    }
    formatFixed(out.value, sizeof(out.value), shown, decimalsFor(sample.id, sample.value));
    copyText(out.unit, sizeof(out.unit), unit);
    if (sample.status == SensorStatus::Measuring) {
        copyText(out.status, sizeof(out.status), statusLabel(sample.status));
    }
    return out;
}

}  // namespace bluecore
