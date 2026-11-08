#include "bluecore/ui_model.hpp"

#include <cstdio>
#include <cstring>

#include "bluecore/app.hpp"
#include "bluecore/catalog.hpp"
#include "bluecore/policy.hpp"
#include "bluecore/text_util.hpp"
#include "bluecore/version.hpp"

namespace bluecore {
namespace {

const char* statusSentence(SensorStatus status) {
    switch (status) {
        case SensorStatus::Ready:
            return "hazır";
        case SensorStatus::Measuring:
            return "ölçülüyor";
        case SensorStatus::NotConnected:
            return "bağlı değil";
        case SensorStatus::CalibrationRequired:
            return "kalibrasyon gerekli";
        case SensorStatus::ReadError:
            return "okuma hatası";
        case SensorStatus::InvalidReading:
            return "geçersiz ölçüm";
    }
    return "bilinmiyor";
}

void formatUptime(char* out, size_t capacity, uint32_t uptimeMs) {
    const unsigned long total = uptimeMs / 1000u;
    const unsigned long minutes = total / 60u;
    const unsigned long seconds = total % 60u;
    if (minutes == 0) {
        std::snprintf(out, capacity, "Süre: %lu sn", seconds);
    } else {
        std::snprintf(out, capacity, "Süre: %lu dk %02lu sn", minutes, seconds);
    }
}

void addBody(UiModel& model, const char* line) {
    if (model.bodyCount >= 8 || line == nullptr) {
        return;
    }
    copyText(model.body[model.bodyCount], sizeof(model.body[model.bodyCount]), line);
    ++model.bodyCount;
}

const char* menuTitle(ScreenId screen) {
    for (int i = 0; i < kMenuCount; ++i) {
        if (kMenuEntries[i].screen == screen) {
            return kMenuEntries[i].title;
        }
    }
    return "BLUECORE";
}

void fillRows(const AppSnapshot& snapshot, UiModel& model) {
    const bool demo = snapshot.mode == OperatingMode::Demo;
    for (int i = 0; i < kSensorCount; ++i) {
        const auto id = static_cast<SensorId>(i);
        const Presentation view = present(snapshot.displayed[i], demo);
        copyText(model.rows[i].label, sizeof(model.rows[i].label), catalog(id).label);
        copyText(model.rows[i].value, sizeof(model.rows[i].value), view.value);
        copyText(model.rows[i].unit, sizeof(model.rows[i].unit), view.unit);
        copyText(model.rows[i].status, sizeof(model.rows[i].status), view.status);
        model.rows[i].numeric = view.showNumber;
    }
}

}  // namespace

void buildUiModel(const AppSnapshot& snapshot, UiModel& model) {
    std::memset(&model, 0, sizeof(model));
    const bool demo = snapshot.mode == OperatingMode::Demo;
    model.screen = snapshot.screen;
    model.demoMode = demo ? 1 : 0;
    copyText(model.pill, sizeof(model.pill), demo ? "DEMO" : "SAHA");
    model.menuCount = kMenuCount;
    model.menuIndex = snapshot.menuIndex;
    if (model.menuIndex < 0) {
        model.menuIndex = 0;
    }
    if (model.menuIndex >= kMenuCount) {
        model.menuIndex = kMenuCount - 1;
    }
    for (int i = 0; i < kMenuCount; ++i) {
        copyText(model.menu[i], sizeof(model.menu[i]), kMenuEntries[i].title);
    }

    int connected = 0;
    for (int i = 0; i < kSensorCount; ++i) {
        if (snapshot.hardwareStatus[i] == SensorStatus::Ready ||
            snapshot.hardwareStatus[i] == SensorStatus::Measuring) {
            ++connected;
        }
    }
    copyText(model.footerLabel[0], sizeof(model.footerLabel[0]), "PİL");
    copyText(model.footerValue[0], sizeof(model.footerValue[0]), "—");
    copyText(model.footerLabel[1], sizeof(model.footerLabel[1]), "Wi-Fi");
    copyText(model.footerValue[1], sizeof(model.footerValue[1]), "—");
    copyText(model.footerLabel[2], sizeof(model.footerLabel[2]), "Sens");
    std::snprintf(model.footerValue[2], sizeof(model.footerValue[2]), "%d/5", connected);
    copyText(model.footerLabel[3], sizeof(model.footerLabel[3]), "Kayıt");
    copyText(model.footerValue[3], sizeof(model.footerValue[3]), "—");

    switch (snapshot.screen) {
        case ScreenId::Boot:
            copyText(model.subtitle, sizeof(model.subtitle), "BC-01");
            copyText(model.notice, sizeof(model.notice), "Donanım doğrulanmadı");
            break;
        case ScreenId::Live:
            copyText(model.subtitle, sizeof(model.subtitle), "BC-01 | CANLI ÖLÇÜM");
            if (demo) {
                copyText(model.notice, sizeof(model.notice), "SENARYO — sensör okuması değil");
            } else {
                copyText(model.notice, sizeof(model.notice), "Sensör bağlı değil");
            }
            model.noticeWarning = 1;
            fillRows(snapshot, model);
            break;
        case ScreenId::Menu:
            copyText(model.subtitle, sizeof(model.subtitle), "MENÜ");
            copyText(model.heading, sizeof(model.heading), "MENÜ");
            copyText(model.hint, sizeof(model.hint), "OK seçer    GERİ ölçüm");
            break;
        case ScreenId::History:
            copyText(model.subtitle, sizeof(model.subtitle), menuTitle(ScreenId::History));
            addBody(model, "Kayıtlı ölçüm yok.");
            addBody(model, "Kalıcı kayıt kapalı.");
            addBody(model, "Saat kaynağı doğrulanmadı.");
            copyText(model.hint, sizeof(model.hint), "GERİ menüye döner");
            break;
        case ScreenId::Sensors: {
            copyText(model.subtitle, sizeof(model.subtitle), menuTitle(ScreenId::Sensors));
            for (int i = 0; i < kSensorCount; ++i) {
                char line[64];
                std::snprintf(line, sizeof(line), "%s: %s", catalog(static_cast<SensorId>(i)).shortName,
                              statusSentence(snapshot.hardwareStatus[i]));
                addBody(model, line);
            }
            copyText(model.hint, sizeof(model.hint), "GERİ menüye döner");
            break;
        }
        case ScreenId::Calibration:
            copyText(model.subtitle, sizeof(model.subtitle), menuTitle(ScreenId::Calibration));
            addBody(model, "Kayıtlı katsayı yok.");
            addBody(model, "pH, EC ve DO kalibrasyon ister.");
            addBody(model, "Kesinlik iddia edilmez.");
            addBody(model, "Sıcaklık probu doğrulanmadı.");
            copyText(model.hint, sizeof(model.hint), "GERİ menüye döner");
            break;
        case ScreenId::Alarms:
            copyText(model.subtitle, sizeof(model.subtitle), menuTitle(ScreenId::Alarms));
            addBody(model, "Etkin alarm kuralı yok.");
            addBody(model, "Eksik sensör alarm üretmez.");
            copyText(model.hint, sizeof(model.hint), "GERİ menüye döner");
            break;
        case ScreenId::Wifi:
            copyText(model.subtitle, sizeof(model.subtitle), menuTitle(ScreenId::Wifi));
            addBody(model, "Radyo başlatılmadı.");
            addBody(model, "Ağ olmadan yerel ölçüm sürer.");
            addBody(model, "Tarama bu sürümde kapalı.");
            copyText(model.hint, sizeof(model.hint), "GERİ menüye döner");
            break;
        case ScreenId::Power:
            copyText(model.subtitle, sizeof(model.subtitle), menuTitle(ScreenId::Power));
            addBody(model, "Pil gerilimi ölçülmüyor.");
            addBody(model, "Yüzde ve şarj durumu yok.");
            copyText(model.hint, sizeof(model.hint), "GERİ menüye döner");
            break;
        case ScreenId::System: {
            copyText(model.subtitle, sizeof(model.subtitle), menuTitle(ScreenId::System));
            char line[64];
            std::snprintf(line, sizeof(line), "Sürüm %s", kFirmwareVersion);
            addBody(model, line);
            std::snprintf(line, sizeof(line), "Hedef: %s", snapshot.boardText);
            addBody(model, line);
            std::snprintf(line, sizeof(line), "Mod: %s", demo ? "DEMO" : "SAHA");
            addBody(model, line);
            formatUptime(line, sizeof(line), snapshot.uptimeMs);
            addBody(model, line);
            std::snprintf(line, sizeof(line), "Saat: %s", snapshot.clockText);
            addBody(model, line);
            addBody(model, snapshot.displayConfigured ? "TFT: yapılandırıldı" : "TFT: başlatılmadı");
            addBody(model, snapshot.gpioConfigured ? "GPIO: kullanılıyor" : "GPIO: kullanılmıyor");
            copyText(model.hint, sizeof(model.hint), "GERİ menüye döner");
            break;
        }
    }
}

}  // namespace bluecore
