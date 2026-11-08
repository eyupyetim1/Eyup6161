#pragma once

#include "bluecore/navigation.hpp"
#include "bluecore/types.hpp"

// Ekran modeli.
//
// Görev: iş mantığını çizilecek metne çevirmek.
// İlişkili dosyalar: src/ui/ui_model.cpp, ui_renderer, text_report.
// Donanım: yok.
// Etki: satır metni değişirse TFT ve seri aynası aynı modeli kullanır.

namespace bluecore {

struct MeasureRow {
    char label[24];
    char value[16];
    char unit[16];
    char status[32];
    uint8_t numeric;
};

struct UiModel {
    ScreenId screen;
    uint8_t demoMode;
    char pill[12];
    char subtitle[40];
    char notice[56];
    uint8_t noticeWarning;
    MeasureRow rows[kSensorCount];
    char footerLabel[4][16];
    char footerValue[4][20];
    char menu[8][32];
    int menuCount;
    int menuIndex;
    char heading[40];
    char body[8][64];
    int bodyCount;
    char hint[48];
};

struct AppSnapshot;

void buildUiModel(const AppSnapshot& snapshot, UiModel& model);

}  // namespace bluecore
