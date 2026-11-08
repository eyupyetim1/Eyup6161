#pragma once

#include "bluecore/sensors/unverified_sensor.hpp"

// Elektriksel iletkenlik sensörü.
//
// Görev: EC probunu µS/cm kanonik biriminde modele taşımak.
// İlişkili dosyalar: calibration, policy (mS/cm gösterimi), catalog.
// Donanım: RS485 / Modbus EC probu. Adres, hat hızı ve alıcı-verici doğrulanmadı.
// Etki: hat sürücüsü eklenmeden A/B uçları için bağlantı önerilmez.

namespace bluecore {

class ConductivitySensor : public UnverifiedSensor {
 public:
    ConductivitySensor();
};

}  // namespace bluecore
