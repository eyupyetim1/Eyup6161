#include "bluecore/canvas.hpp"

#include <cstring>

#include "bluecore/utf8.hpp"

namespace bluecore {

void rasterizeText(Canvas& canvas, int x, int y, const char* utf8, FontId fontId, uint16_t color) {
    if (utf8 == nullptr) {
        return;
    }
    const FontFace& font = face(fontId);
    const size_t length = std::strlen(utf8);
    size_t index = 0;
    int pen = x;
    while (index < length) {
        const uint32_t cp = nextCodepoint(utf8, length, index);
        if (cp == 0) {
            break;
        }
        const Glyph* glyph = findGlyph(font, cp);
        if (glyph == nullptr) {
            pen += 4;
            continue;
        }
        const int bits = static_cast<int>(glyph->width) * static_cast<int>(glyph->height);
        for (int bit = 0; bit < bits; ++bit) {
            const uint32_t byteIndex = glyph->bitmapOffset + static_cast<uint32_t>(bit / 8);
            if (byteIndex >= font.bitmapBytes) {
                break;
            }
            const uint8_t byte = font.bitmap[byteIndex];
            if ((byte & static_cast<uint8_t>(0x80u >> (bit % 8))) == 0) {
                continue;
            }
            const int col = bit % glyph->width;
            const int row = bit / glyph->width;
            canvas.setPixel(pen + glyph->xOffset + col, y + glyph->yOffset + row, color);
        }
        pen += glyph->advance;
    }
}

}  // namespace bluecore
