#include "framebuffer.hpp"

#include <cstdio>

namespace bluecore {

FramebufferCanvas::FramebufferCanvas() { fillRect(0, 0, kScreenWidth, kScreenHeight, 0); }

void FramebufferCanvas::setPixel(int x, int y, uint16_t color) {
    if (x < 0 || y < 0 || x >= kScreenWidth || y >= kScreenHeight) {
        return;
    }
    pixels_[y * kScreenWidth + x] = color;
}

void FramebufferCanvas::fillRect(int x, int y, int w, int h, uint16_t color) {
    if (w <= 0 || h <= 0) {
        return;
    }
    int x0 = x < 0 ? 0 : x;
    int y0 = y < 0 ? 0 : y;
    int x1 = x + w;
    int y1 = y + h;
    if (x1 > kScreenWidth) {
        x1 = kScreenWidth;
    }
    if (y1 > kScreenHeight) {
        y1 = kScreenHeight;
    }
    for (int yy = y0; yy < y1; ++yy) {
        for (int xx = x0; xx < x1; ++xx) {
            pixels_[yy * kScreenWidth + xx] = color;
        }
    }
}

void FramebufferCanvas::drawText(int x, int y, const char* utf8, FontId font, uint16_t color) {
    rasterizeText(*this, x, y, utf8, font, color);
}

uint16_t FramebufferCanvas::pixel(int x, int y) const {
    if (x < 0 || y < 0 || x >= kScreenWidth || y >= kScreenHeight) {
        return 0;
    }
    return pixels_[y * kScreenWidth + x];
}

bool FramebufferCanvas::writePpm(const char* path) const {
    FILE* file = std::fopen(path, "wb");
    if (file == nullptr) {
        return false;
    }
    std::fprintf(file, "P6\n%d %d\n255\n", kScreenWidth, kScreenHeight);
    for (int i = 0; i < kScreenWidth * kScreenHeight; ++i) {
        const uint16_t color = pixels_[i];
        const unsigned r5 = (color >> 11) & 0x1F;
        const unsigned g6 = (color >> 5) & 0x3F;
        const unsigned b5 = color & 0x1F;
        const uint8_t rgb[3] = {
            static_cast<uint8_t>((r5 * 255) / 31),
            static_cast<uint8_t>((g6 * 255) / 63),
            static_cast<uint8_t>((b5 * 255) / 31),
        };
        if (std::fwrite(rgb, 1, 3, file) != 3) {
            std::fclose(file);
            return false;
        }
    }
    std::fclose(file);
    return true;
}

}  // namespace bluecore
