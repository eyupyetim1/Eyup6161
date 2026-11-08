#pragma once

#include <cstdint>

// BlueCore renkleri. Koyu lacivert zemin, kontrollü mavi vurgu.
// Değerler RGB565'tir; panel sürücüsü bağlanınca aynı sabitler kullanılır.

namespace bluecore {
namespace theme {

constexpr uint16_t rgb565(unsigned r, unsigned g, unsigned b) {
    return static_cast<uint16_t>(((r & 0xF8u) << 8) | ((g & 0xFCu) << 3) | (b >> 3));
}

constexpr uint16_t bg = rgb565(11, 22, 36);
constexpr uint16_t surface = rgb565(16, 32, 48);
constexpr uint16_t surfaceHigh = rgb565(24, 52, 82);
constexpr uint16_t accent = rgb565(47, 126, 216);
constexpr uint16_t text = rgb565(232, 238, 244);
constexpr uint16_t textDim = rgb565(154, 173, 192);
constexpr uint16_t line = rgb565(42, 62, 82);
constexpr uint16_t warn = rgb565(224, 179, 74);
constexpr uint16_t danger = rgb565(208, 92, 92);
constexpr uint16_t ok = rgb565(72, 184, 146);

}  // namespace theme
}  // namespace bluecore
