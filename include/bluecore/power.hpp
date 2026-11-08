#pragma once

#include "bluecore/types.hpp"

// Pil gözlemcisi.
//
// Görev: doğrulanmış bir bölücü veya şarj tümleşiği yokken yüzde üretmemek.
// İlişkili dosyalar: src/power/power.cpp, alt bilgi çubuğu.
// Donanım: 18650 ve şarj yolu planlıdır; devre doğrulanmadı. ADC açılmaz.
// Etki: percentKnown true yapılırsa kullanıcı arayüzü bu yüzdeyi gerçek sanır.

namespace bluecore {

class IPowerMonitor {
 public:
    virtual ~IPowerMonitor() = default;
    virtual PowerReport read() const = 0;
};

class UnverifiedPowerMonitor : public IPowerMonitor {
 public:
    PowerReport read() const override;
};

}  // namespace bluecore
