#pragma once

#include "bluecore/types.hpp"

// Kart profili.
//
// Görev: derleme hedefini kimlik doğrulamasından ayrı tutmak.
// İlişkili dosyalar: src/main.cpp, docs/DONANIM_ENVANTERI.md, docs/BAGLANTI_SEMASI.md.
// Donanım: GPIO, SPI ve ADC açılmaz. Pin numarası bu sürümde yoktur.
// Etki: displayPinsConfirmed() veya gpioPinsConfirmed() değiştirilirse
// ekran ve tuş bağlama işi başlar; elektriksel doğrulama olmadan açılmamalıdır.

namespace bluecore {

inline BoardKind boardKind() {
#if defined(BLUECORE_TARGET_ESP32S3)
    return BoardKind::Esp32S3;
#elif defined(BLUECORE_TARGET_ESP32)
    return BoardKind::Esp32Classic;
#else
    return BoardKind::Unverified;
#endif
}

// Derleme hedefi, modülün üzerindeki yazının okunduğu anlamına gelmez.
inline bool boardIdentityConfirmed() { return false; }

inline bool displayPinsConfirmed() { return false; }

inline bool gpioPinsConfirmed() { return false; }

inline const char* boardKindText() {
    switch (boardKind()) {
        case BoardKind::Esp32Classic:
            return "ESP32 hedefi, kart doğrulanmadı";
        case BoardKind::Esp32S3:
            return "ESP32-S3 hedefi, kart doğrulanmadı";
        case BoardKind::Unverified:
            break;
    }
    return "doğrulanmadı";
}

}  // namespace bluecore
