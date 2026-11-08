#pragma once

#include <cstring>

#include "bluecore/canvas.hpp"
#include "bluecore/text_util.hpp"

struct RecordingCanvas : bluecore::Canvas {
    struct Draw {
        int x;
        int y;
        bluecore::FontId font;
        uint16_t color;
        char text[72];
    };

    int width() const override { return bluecore::kScreenWidth; }
    int height() const override { return bluecore::kScreenHeight; }
    void setPixel(int, int, uint16_t) override {}
    void fillRect(int, int, int, int, uint16_t) override {}
    void drawText(int x, int y, const char* utf8, bluecore::FontId font, uint16_t color) override {
        if (count >= 96) {
            overflow = true;
            return;
        }
        Draw& draw = draws[count++];
        draw.x = x;
        draw.y = y;
        draw.font = font;
        draw.color = color;
        bluecore::copyText(draw.text, sizeof(draw.text), utf8);
    }

    bool contains(const char* text) const {
        for (int i = 0; i < count; ++i) {
            if (std::strcmp(draws[i].text, text) == 0) {
                return true;
            }
        }
        return false;
    }

    Draw draws[96]{};
    int count = 0;
    bool overflow = false;
};
