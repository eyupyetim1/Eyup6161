#include "bluecore/clock.hpp"

#include <cstdio>

#include "bluecore/text_util.hpp"

namespace bluecore {

TimeQuality UptimeClock::quality() const { return TimeQuality::UptimeOnly; }

bool UptimeClock::civil(CivilTime&) const { return false; }

bool formatCivil(const IClock& clock, char* out, size_t capacity) {
    CivilTime time{};
    if (clock.quality() != TimeQuality::Verified || !clock.civil(time) || time.year < 2024) {
        copyText(out, capacity, "doğrulanmadı");
        return false;
    }
    std::snprintf(out, capacity, "%04u-%02u-%02u %02u:%02u:%02u", time.year, time.month, time.day,
                  time.hour, time.minute, time.second);
    return true;
}

}  // namespace bluecore
