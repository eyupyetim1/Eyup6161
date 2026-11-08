#pragma once

#include "bluecore/types.hpp"

// Radyo.
//
// Görev: Wi-Fi durumunu bildirmek. Bu sürüm radyoyu başlatmaz.
// İlişkili dosyalar: src/connectivity/radio.cpp, Wi-Fi ekranı.
// Donanım: ESP32 radyo kapalı kalır. Ağ taraması yapılmaz.
// Etki: begin() eklenirse güç tüketimi ve saha/demo ayrımı yeniden test edilmelidir.

namespace bluecore {

class IRadio {
 public:
    virtual ~IRadio() = default;
    virtual WifiReport read() const = 0;
};

class DormantRadio : public IRadio {
 public:
    WifiReport read() const override;
};

}  // namespace bluecore
