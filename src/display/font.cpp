#include "bluecore/font.hpp"

#include <cstring>

#include "bluecore/utf8.hpp"

namespace bluecore {

const Glyph* findGlyph(const FontFace& font, uint32_t codepoint) {
    if (codepoint > 0xFFFFu || font.glyphs == nullptr) {
        return nullptr;
    }
    int lo = 0;
    int hi = static_cast<int>(font.glyphCount) - 1;
    const auto target = static_cast<uint16_t>(codepoint);
    while (lo <= hi) {
        const int mid = lo + (hi - lo) / 2;
        const uint16_t found = font.glyphs[mid].codepoint;
        if (found == target) {
            return &font.glyphs[mid];
        }
        if (found < target) {
            lo = mid + 1;
        } else {
            hi = mid - 1;
        }
    }
    return nullptr;
}

int measureText(FontId id, const char* utf8) {
    if (utf8 == nullptr) {
        return 0;
    }
    const FontFace& font = face(id);
    const size_t length = std::strlen(utf8);
    size_t index = 0;
    int width = 0;
    while (index < length) {
        const uint32_t cp = nextCodepoint(utf8, length, index);
        if (cp == 0) {
            break;
        }
        const Glyph* glyph = findGlyph(font, cp);
        width += glyph != nullptr ? glyph->advance : 4;
    }
    return width;
}

bool fontCovers(FontId id, const char* utf8) {
    if (utf8 == nullptr) {
        return false;
    }
    const FontFace& font = face(id);
    const size_t length = std::strlen(utf8);
    size_t index = 0;
    while (index < length) {
        const uint32_t cp = nextCodepoint(utf8, length, index);
        if (cp == 0) {
            break;
        }
        if (cp < 32) {
            continue;
        }
        if (findGlyph(font, cp) == nullptr) {
            return false;
        }
    }
    return true;
}

}  // namespace bluecore
