#pragma once

#include "bluecore/types.hpp"

// Ekran gezinmesi.
//
// Görev: açılış, menü ve alt sayfalar arasında tek düğme modeli yürütmek.
// İlişkili dosyalar: src/input/navigation.cpp, ui_model.
// Donanım: yok.
// Etki: tuş eşlemesi değişirse her ekranın gezinmesi değişir.

namespace bluecore {

struct MenuEntry {
    ScreenId screen;
    const char* title;
};

constexpr MenuEntry kMenuEntries[] = {
    {ScreenId::Live, "Canlı Ölçüm"},
    {ScreenId::History, "Ölçüm Geçmişi"},
    {ScreenId::Sensors, "Sensörler"},
    {ScreenId::Calibration, "Kalibrasyon"},
    {ScreenId::Alarms, "Alarmlar"},
    {ScreenId::Wifi, "Wi-Fi Ayarları"},
    {ScreenId::Power, "Pil ve Güç"},
    {ScreenId::System, "Sistem Bilgileri"},
};

constexpr int kMenuCount = static_cast<int>(sizeof(kMenuEntries) / sizeof(kMenuEntries[0]));
constexpr uint32_t kBootHoldMs = 1600;

class Navigation {
 public:
    void start(uint32_t nowMs);
    bool tick(uint32_t nowMs);
    void handle(const ButtonEvent& event);

    ScreenId screen() const { return screen_; }
    int menuIndex() const { return menuIndex_; }
    bool bootDone() const { return bootDone_; }

 private:
    void finishBoot();
    void openMenu();

    ScreenId screen_ = ScreenId::Boot;
    int menuIndex_ = 0;
    uint32_t bootStartMs_ = 0;
    uint8_t bootDone_ = 0;
};

}  // namespace bluecore
