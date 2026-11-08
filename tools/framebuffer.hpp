#pragma once

#if !defined(BLUECORE_HOST)
#error Framebuffer yalnizca host derlemesinde kullanilir.
#endif

#include "bluecore/canvas.hpp"

// Host önizleme yüzeyi. Cihaz imajına bağlanmaz; 150 KB arabellek ESP32 RAM'ine konmaz.

namespace bluecore {

class FramebufferCanvas : public Canvas {
 public:
    FramebufferCanvas();
    int width() const override { return kScreenWidth; }
    int height() const override { return kScreenHeight; }
    void setPixel(int x, int y, uint16_t color) override;
    void fillRect(int x, int y, int w, int h, uint16_t color) override;
    void drawText(int x, int y, const char* utf8, FontId font, uint16_t color) override;
    uint16_t pixel(int x, int y) const;
    bool writePpm(const char* path) const;

 private:
    uint16_t pixels_[kScreenWidth * kScreenHeight];
};

}  // namespace bluecore
