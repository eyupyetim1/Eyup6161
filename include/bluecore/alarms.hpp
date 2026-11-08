#pragma once

#include "bluecore/types.hpp"

// Alarm değerlendirme.
//
// Görev: yalnızca güvenilir canlı değerde eşik kontrolü yapmak.
// İlişkili dosyalar: src/diagnostics/alarms.cpp, policy.
// Donanım: yok. Bu sürümde etkin kural tanımlanmaz.
// Etki: eksik sensörü alarm saymak, boş probu arıza gibi gösterir.

namespace bluecore {

struct AlarmRule {
    uint8_t enabled;
    SensorId id;
    float low;
    float high;
};

struct AlarmEvent {
    SensorId id;
    uint8_t high;
};

int evaluateAlarms(const AlarmRule* rules, int ruleCount, const Sample* samples, int sampleCount,
                   AlarmEvent* out, int maxOut);

}  // namespace bluecore
