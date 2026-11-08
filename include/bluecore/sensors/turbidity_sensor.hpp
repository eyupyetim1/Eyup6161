#pragma once

#include "bluecore/sensors/unverified_sensor.hpp"

// Bulanıklık sensörü.
//
// Görev: NTU ölçümünü modele taşımak.
// İlişkili dosyalar: catalog (kalibrasyon ihtiyacı Unknown), policy.
// Donanım: bulanıklık modülü. Model doğrulanmadı; canlı NTU iddiası kapalıdır.
// Etki: model doğrulanmadan sayısal iddia açılırsa policy testi kırılır.

namespace bluecore {

class TurbiditySensor : public UnverifiedSensor {
 public:
    TurbiditySensor();
};

}  // namespace bluecore
