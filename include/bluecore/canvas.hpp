#pragma once

#include <cstdint>

#include "bluecore/font.hpp"

// Çizim yüzeyi.
//
// Görev: arayüzü panelden ayırmak.
// İlişkili dosyalar: ui_renderer, null_canvas, tools/framebuffer.cpp.
// Donanım: somut yüzey belirler. NullCanvas pin açmaz.
// Etki: drawText sözleşmesi değişirse açılış ve canlı ölçüm birlikte değişir.

namespace bluecore {

constexpr int kScreenWidth = 320;
constexpr int kScreenHeight = 240;

class Canvas {
 public:
    virtual ~Canvas() = default;
    virtual int width() const = 0;
    virtual int height() const = 0;
    virtual void setPixel(int x, int y, uint16_t color) = 0;
    virtual void fillRect(int x, int y, int w, int h, uint16_t color) = 0;
    virtual void drawText(int x, int y, const char* utf8, FontId font, uint16_t color) = 0;
};

void rasterizeText(Canvas& canvas, int x, int y, const char* utf8, FontId font, uint16_t color);

}  // namespace bluecore
