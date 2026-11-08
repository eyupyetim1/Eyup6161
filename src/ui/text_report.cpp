#include "bluecore/text_report.hpp"

#include <cstdio>

#include "bluecore/text_util.hpp"
#include "bluecore/version.hpp"

namespace bluecore {
namespace {

void line(char* out, size_t capacity, size_t& used, const char* text) {
    appendText(out, capacity, used, text);
    appendText(out, capacity, used, "\n");
}

void addHardware(const AppSnapshot& snapshot, char* out, size_t capacity, size_t& used) {
    line(out, capacity, used, "--- cihaz ---");
    char row[96];
    std::snprintf(row, sizeof(row), "Pil: %s",
                  snapshot.power.percentKnown ? "yüzde var" : "ölçülmüyor");
    line(out, capacity, used, row);
    line(out, capacity, used, snapshot.wifi.status == WifiStatus::Disabled ? "Wi-Fi: kapalı"
                                                                             : "Wi-Fi: açık");
    line(out, capacity, used, snapshot.storageEnabled ? "Kayıt: açık" : "Kayıt: kapalı");
    std::snprintf(row, sizeof(row), "Saat: %s", snapshot.clockText);
    line(out, capacity, used, row);
    line(out, capacity, used, snapshot.displayConfigured ? "TFT: yapılandırıldı" : "TFT: başlatılmadı");
    line(out, capacity, used, snapshot.gpioConfigured ? "GPIO: yapılandırıldı" : "GPIO: kullanılmıyor");
    int connected = 0;
    for (int i = 0; i < kSensorCount; ++i) {
        if (snapshot.hardwareStatus[i] == SensorStatus::Ready ||
            snapshot.hardwareStatus[i] == SensorStatus::Measuring) {
            ++connected;
        }
    }
    std::snprintf(row, sizeof(row), "Donanım sensörleri: %d/5", connected);
    line(out, capacity, used, row);
}

}  // namespace

void formatBootBanner(const AppSnapshot& snapshot, char* out, size_t capacity) {
    if (out == nullptr || capacity == 0) {
        return;
    }
    size_t used = 0;
    out[0] = '\0';
    line(out, capacity, used, "BLUECORE BC-01");
    char row[96];
    std::snprintf(row, sizeof(row), "Sürüm %s", kFirmwareVersion);
    line(out, capacity, used, row);
    std::snprintf(row, sizeof(row), "Mod: %s", snapshot.mode == OperatingMode::Demo ? "DEMO" : "SAHA");
    line(out, capacity, used, row);
    std::snprintf(row, sizeof(row), "Kart: %s", snapshot.boardText);
    line(out, capacity, used, row);
    line(out, capacity, used, "TFT başlatılmadı. Panel pini doğrulanmadı.");
    line(out, capacity, used, "GPIO kullanılmıyor. Sensör ve tuş bağlanmaz.");
    line(out, capacity, used, snapshot.mode == OperatingMode::Demo
                                  ? "Canlı sayılar senaryo verisidir."
                                  : "Canlı sayıda sensör değeri yoktur.");
    line(out, capacity, used, "Seri tuş: m menü, u yukarı, d aşağı, o seç, b geri");
    addHardware(snapshot, out, capacity, used);
}

void formatTextReport(const AppSnapshot& snapshot, const UiModel& model, char* out, size_t capacity) {
    if (out == nullptr || capacity == 0) {
        return;
    }
    size_t used = 0;
    out[0] = '\0';
    char row[120];
    std::snprintf(row, sizeof(row), "BLUECORE  %s", model.pill);
    line(out, capacity, used, row);
    line(out, capacity, used, model.subtitle);
    if (model.notice[0] != '\0') {
        line(out, capacity, used, model.notice);
    }
    if (model.screen == ScreenId::Live) {
        for (int i = 0; i < kSensorCount; ++i) {
            const MeasureRow& item = model.rows[i];
            if (item.numeric) {
                std::snprintf(row, sizeof(row), "%s  %s %s", item.label, item.value, item.unit);
            } else {
                std::snprintf(row, sizeof(row), "%s  %s  %s", item.label, item.value, item.status);
            }
            line(out, capacity, used, row);
        }
    } else if (model.screen == ScreenId::Menu) {
        for (int i = 0; i < model.menuCount; ++i) {
            std::snprintf(row, sizeof(row), "%s %s", i == model.menuIndex ? ">" : " ", model.menu[i]);
            line(out, capacity, used, row);
        }
    } else {
        for (int i = 0; i < model.bodyCount; ++i) {
            line(out, capacity, used, model.body[i]);
        }
    }
    if (model.hint[0] != '\0') {
        line(out, capacity, used, model.hint);
    }
    addHardware(snapshot, out, capacity, used);
}

}  // namespace bluecore
