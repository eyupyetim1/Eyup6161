#pragma once

#include <cstddef>
#include <cstdint>

namespace bluecore {

// Bir UTF-8 dizisinden sıradaki kod noktasını okur.
// Bozuk baytta U+FFFD döner ve bir bayt ilerler.
inline uint32_t nextCodepoint(const char* text, size_t length, size_t& index) {
    if (text == nullptr || index >= length) {
        return 0;
    }
    const auto byte = static_cast<uint8_t>(text[index]);
    if (byte < 0x80) {
        ++index;
        return byte;
    }
    int need = 0;
    uint32_t cp = 0;
    if ((byte & 0xE0) == 0xC0) {
        need = 1;
        cp = byte & 0x1F;
    } else if ((byte & 0xF0) == 0xE0) {
        need = 2;
        cp = byte & 0x0F;
    } else if ((byte & 0xF8) == 0xF0) {
        need = 3;
        cp = byte & 0x07;
    } else {
        ++index;
        return 0xFFFD;
    }
    if (index + static_cast<size_t>(need) >= length) {
        ++index;
        return 0xFFFD;
    }
    for (int i = 0; i < need; ++i) {
        const auto next = static_cast<uint8_t>(text[index + 1 + static_cast<size_t>(i)]);
        if ((next & 0xC0) != 0x80) {
            ++index;
            return 0xFFFD;
        }
        cp = (cp << 6) | (next & 0x3F);
    }
    index += static_cast<size_t>(need) + 1;
    return cp;
}

}  // namespace bluecore
