#pragma once

#include "bluecore/canvas.hpp"

// Boş çizim yüzeyi.
//
// Görev: panel pinleri doğrulanmadan çizim kodunu çalıştırmak.
// İlişkili dosyalar: src/display/null_canvas.cpp, src/main.cpp.
// Donanım: SPI, GPIO ve arka ışık açılmaz.
// Etki: setPixel gerçek panele giderse bu sınıfın yerine panel sürücüsü konur.

namespace bluecore {

class NullCanvas : public Canvas {
 public:
    int width() const override { return kScreenWidth; }
    int height() const override { return kScreenHeight; }
    void setPixel(int x, int y, uint16_t color) override;
    void fillRect(int x, int y, int w, int h, uint16_t color) override;
    void drawText(int x, int y, const char* utf8, FontId font, uint16_t color) override;
};

}  // namespace bluecore
