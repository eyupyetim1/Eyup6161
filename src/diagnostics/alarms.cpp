#include "bluecore/alarms.hpp"

#include "bluecore/policy.hpp"

namespace bluecore {

int evaluateAlarms(const AlarmRule* rules, int ruleCount, const Sample* samples, int sampleCount,
                   AlarmEvent* out, int maxOut) {
    if (ruleCount <= 0 || rules == nullptr || samples == nullptr || sampleCount <= 0 || maxOut <= 0 ||
        out == nullptr) {
        return 0;
    }
    int written = 0;
    for (int r = 0; r < ruleCount; ++r) {
        if (rules[r].enabled == 0) {
            continue;
        }
        const Sample* sample = nullptr;
        for (int s = 0; s < sampleCount; ++s) {
            if (samples[s].id == rules[r].id) {
                sample = &samples[s];
                break;
            }
        }
        if (sample == nullptr || sample->origin != DataOrigin::LiveSensor ||
            !numericClaimAllowed(*sample)) {
            continue;
        }
        uint8_t high = 0;
        bool hit = false;
        if (sample->value > rules[r].high) {
            hit = true;
            high = 1;
        } else if (sample->value < rules[r].low) {
            hit = true;
            high = 0;
        }
        if (!hit || written >= maxOut) {
            continue;
        }
        out[written].id = sample->id;
        out[written].high = high;
        ++written;
    }
    return written;
}

}  // namespace bluecore
