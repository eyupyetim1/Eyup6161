#pragma once

#include "bluecore/types.hpp"

// Fiziksel tuş mantığı.
//
// Görev: ham seviyeden tıklama, uzun basma ve tekrar üretmek.
// İlişkili dosyalar: src/input/input.cpp, navigation, App.
// Donanım: GPIO okunmaz. Seviyeyi çağıran verir. Pin eşlemesi doğrulama sonrasına bırakıldı.
// Etki: eşik değişirse menü hızı ve yanlış tetiklenme birlikte değişir.

namespace bluecore {

constexpr uint32_t kDebounceMs = 30;
constexpr uint32_t kLongPressMs = 600;
constexpr uint32_t kRepeatMs = 150;

class ButtonLogic {
 public:
    ButtonLogic();

    void setRaw(ButtonId id, bool pressed, uint32_t nowMs);
    void poll(uint32_t nowMs);
    bool next(ButtonEvent& out);
    uint16_t dropped() const { return dropped_; }

 private:
    struct Channel {
        uint8_t raw;
        uint8_t stable;
        uint8_t longFired;
        uint32_t rawChangedMs;
        uint32_t pressedMs;
        uint32_t nextRepeatMs;
    };

    bool push(ButtonId id, ButtonEventType type, uint32_t nowMs);
    static bool repeatable(ButtonId id);

    Channel channels_[kButtonCount] = {};
    ButtonEvent queue_[16] = {};
    uint8_t head_ = 0;
    uint8_t tail_ = 0;
    uint8_t count_ = 0;
    uint16_t dropped_ = 0;
};

}  // namespace bluecore
