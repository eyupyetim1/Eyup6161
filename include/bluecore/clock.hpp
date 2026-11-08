#pragma once

#include <cstddef>

#include "bluecore/types.hpp"

// Saat kaynağı.
//
// Görev: takvim saati doğrulanmadan tarih yazılmasını engellemek.
// İlişkili dosyalar: src/core/clock.cpp, kayıt, sistem ekranı.
// Donanım: RTC yok sayılır. NTP bu sürümde başlatılmaz.
// Etki: quality() Verified dönerse kayıt tarihi kullanıcıya gerçek saat gibi sunulur.

namespace bluecore {

class IClock {
 public:
    virtual ~IClock() = default;
    virtual TimeQuality quality() const = 0;
    virtual bool civil(CivilTime& out) const = 0;
};

class UptimeClock : public IClock {
 public:
    TimeQuality quality() const override;
    bool civil(CivilTime& out) const override;
};

bool formatCivil(const IClock& clock, char* out, size_t capacity);

}  // namespace bluecore
