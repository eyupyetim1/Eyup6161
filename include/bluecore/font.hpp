#pragma once

#include <cstddef>
#include <cstdint>

// Gömülü 1-bit yazı tipi.
//
// Görev: arayüz metnini Türkçe karakterlerle ölçmek ve çizmek.
// İlişkili dosyalar: src/display/font_data.cpp, src/display/text_draw.cpp, tools/gen_font.py.
// Donanım: yok. Bitmap, Inter 4.001'den üretilmiş bir alt kümedir (SIL OFL 1.1).
// Etki: karakter kümesi daralırsa canlı ölçüm ve menü metinleri eksik glyph ile çizilir.

namespace bluecore {

enum class FontId : uint8_t {
    Small = 0,
    Body,
    Value,
    Title,
    Count
};

struct Glyph {
    uint16_t codepoint;
    uint8_t width;
    uint8_t height;
    int8_t xOffset;
    int8_t yOffset;
    uint8_t advance;
    uint32_t bitmapOffset;
};

struct FontFace {
    const Glyph* glyphs;
    uint16_t glyphCount;
    uint8_t lineHeight;
    const uint8_t* bitmap;
    uint32_t bitmapBytes;
};

const FontFace& face(FontId id);
const Glyph* findGlyph(const FontFace& font, uint32_t codepoint);
int measureText(FontId id, const char* utf8);
bool fontCovers(FontId id, const char* utf8);

}  // namespace bluecore
