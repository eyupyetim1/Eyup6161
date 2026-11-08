#include "bluecore/null_canvas.hpp"

namespace bluecore {

void NullCanvas::setPixel(int, int, uint16_t) {}

void NullCanvas::fillRect(int, int, int, int, uint16_t) {}

void NullCanvas::drawText(int x, int y, const char* utf8, FontId font, uint16_t color) {
    rasterizeText(*this, x, y, utf8, font, color);
}

}  // namespace bluecore
