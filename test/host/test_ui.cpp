#include <cstdio>
#include <cstring>

#include "bluecore/app.hpp"
#include "bluecore/text_report.hpp"
#include "bluecore/calibration.hpp"
#include "bluecore/catalog.hpp"
#include "bluecore/font.hpp"
#include "bluecore/theme.hpp"
#include "bluecore/ui_renderer.hpp"
#include "framebuffer.hpp"
#include "test/host/check.hpp"
#include "test/host/recording_canvas.hpp"

namespace {

void cover(const char* text) {
    CHECK(text != nullptr);
    CHECK(bluecore::fontCovers(bluecore::FontId::Small, text));
    CHECK(bluecore::fontCovers(bluecore::FontId::Body, text));
    CHECK(bluecore::fontCovers(bluecore::FontId::Value, text));
    CHECK(bluecore::fontCovers(bluecore::FontId::Title, text));
}

void coverModel(const bluecore::UiModel& model) {
    cover(model.pill);
    cover(model.subtitle);
    cover(model.notice);
    cover(model.heading);
    cover(model.hint);
    for (int i = 0; i < bluecore::kSensorCount; ++i) {
        cover(model.rows[i].label);
        cover(model.rows[i].value);
        cover(model.rows[i].unit);
        cover(model.rows[i].status);
        if (model.rows[i].numeric) {
            const int need = 12 + bluecore::measureText(bluecore::FontId::Body, model.rows[i].label) + 8 +
                             bluecore::measureText(bluecore::FontId::Value, model.rows[i].value) + 6 +
                             bluecore::measureText(bluecore::FontId::Small, model.rows[i].unit) + 12;
            CHECK(need <= bluecore::kScreenWidth);
        }
    }
    for (int i = 0; i < 4; ++i) {
        cover(model.footerLabel[i]);
        cover(model.footerValue[i]);
        char cell[40];
        std::snprintf(cell, sizeof(cell), "%s %s", model.footerLabel[i], model.footerValue[i]);
        CHECK(bluecore::measureText(bluecore::FontId::Small, cell) <= 76);
    }
    for (int i = 0; i < model.menuCount; ++i) {
        cover(model.menu[i]);
    }
    for (int i = 0; i < model.bodyCount; ++i) {
        cover(model.body[i]);
    }
}

void visit(bluecore::App& app, uint32_t now) {
    coverModel(app.model());
    char report[1600];
    bluecore::formatTextReport(app.snapshot(), app.model(), report, sizeof(report));
    cover(report);
    (void)now;
}

}  // namespace

void runUiTests() {
    const bluecore::FontFace& small = bluecore::face(bluecore::FontId::Small);
    CHECK(small.lineHeight > 0);
    CHECK(bluecore::findGlyph(small, 0x00C7) != nullptr);
    CHECK(bluecore::findGlyph(small, 0x011E) != nullptr);
    CHECK(bluecore::findGlyph(small, 0x0130) != nullptr);
    CHECK(bluecore::findGlyph(small, 0x00D6) != nullptr);
    CHECK(bluecore::findGlyph(small, 0x015E) != nullptr);
    CHECK(bluecore::findGlyph(small, 0x00DC) != nullptr);
    CHECK(bluecore::findGlyph(small, 0x0131) != nullptr);
    CHECK(bluecore::findGlyph(small, 0x00B0) != nullptr);
    CHECK(bluecore::findGlyph(small, 0x00B5) != nullptr);
    CHECK(bluecore::findGlyph(small, 0x2014) != nullptr);
    cover("ÖLÇÜM");
    cover("ısı");
    cover("°C");
    cover("µS/cm");
    cover(bluecore::statusLabel(bluecore::SensorStatus::NotConnected));
    cover(bluecore::statusLabel(bluecore::SensorStatus::CalibrationRequired));
    cover(bluecore::statusLabel(bluecore::SensorStatus::InvalidReading));
    cover(bluecore::refuseUnverified(bluecore::SensorId::Ph).message);

    bluecore::App demo;
    demo.begin(bluecore::OperatingMode::Demo, 0);
    visit(demo, 0);
    RecordingCanvas boot;
    bluecore::render(boot, demo.model());
    CHECK(!boot.overflow);
    CHECK(boot.contains("BLUECORE"));
    CHECK(boot.contains("DEMO"));
    CHECK(!boot.contains("22.6"));

    demo.tick(bluecore::kBootHoldMs);
    visit(demo, bluecore::kBootHoldMs);
    RecordingCanvas live;
    bluecore::render(live, demo.model());
    CHECK(live.contains("22.6"));
    CHECK(live.contains("7.42"));
    CHECK(live.contains("8.14"));
    CHECK(live.contains("485"));
    CHECK(live.contains("DEMO"));
    CHECK(live.contains("SENARYO — sensör okuması değil"));
    CHECK(!live.contains("BAĞLI DEĞİL"));

    bluecore::App field;
    field.begin(bluecore::OperatingMode::Field, 0);
    field.tick(bluecore::kBootHoldMs);
    visit(field, bluecore::kBootHoldMs);
    RecordingCanvas fieldLive;
    bluecore::render(fieldLive, field.model());
    CHECK(fieldLive.contains("SAHA"));
    CHECK(fieldLive.contains("—"));
    CHECK(fieldLive.contains("BAĞLI DEĞİL"));
    CHECK(!fieldLive.contains("22.6"));
    CHECK(!fieldLive.contains("7.42"));
    CHECK(!fieldLive.contains("DEMO"));

    field.pushDebugChar('m', 3000);
    visit(field, 3000);
    RecordingCanvas menu;
    bluecore::render(menu, field.model());
    CHECK(menu.contains("Canlı Ölçüm"));
    CHECK(menu.contains("Kalibrasyon"));
    CHECK(menu.contains("Sistem Bilgileri"));
    int menuLastY = -1;
    int hintY = -1;
    for (int i = 0; i < menu.count; ++i) {
        if (std::strcmp(menu.draws[i].text, "Sistem Bilgileri") == 0) {
            menuLastY = menu.draws[i].y;
        }
        if (std::strcmp(menu.draws[i].text, "OK seçer    GERİ ölçüm") == 0) {
            hintY = menu.draws[i].y;
        }
    }
    CHECK(menuLastY >= 0);
    CHECK(hintY > menuLastY + bluecore::face(bluecore::FontId::Small).lineHeight);

    for (int i = 0; i < 7; ++i) {
        field.pushDebugChar('d', 3100 + i);
    }
    field.pushDebugChar('o', 3200);
    CHECK(field.snapshot().screen == bluecore::ScreenId::System);
    visit(field, 3200);
    CHECK(std::strcmp(field.model().body[4], "Saat: doğrulanmadı") == 0);
    RecordingCanvas system;
    bluecore::render(system, field.model());
    int gpioY = -1;
    int systemHintY = -1;
    for (int i = 0; i < system.count; ++i) {
        if (std::strcmp(system.draws[i].text, "GPIO: kullanılmıyor") == 0) {
            gpioY = system.draws[i].y;
        }
        if (std::strcmp(system.draws[i].text, "GERİ menüye döner") == 0) {
            systemHintY = system.draws[i].y;
        }
    }
    CHECK(gpioY >= 0);
    CHECK(systemHintY > gpioY + bluecore::face(bluecore::FontId::Small).lineHeight);

    static bluecore::FramebufferCanvas canvas;
    bluecore::render(canvas, demo.model());
    static uint16_t demoPixels[bluecore::kScreenWidth * bluecore::kScreenHeight];
    for (int i = 0; i < bluecore::kScreenWidth * bluecore::kScreenHeight; ++i) {
        demoPixels[i] = canvas.pixel(i % bluecore::kScreenWidth, i / bluecore::kScreenWidth);
    }
    CHECK(canvas.pixel(1, 1) == bluecore::theme::accent);
    int ink = 0;
    for (int y = 40; y < 200; ++y) {
        for (int x = 0; x < bluecore::kScreenWidth; ++x) {
            if (canvas.pixel(x, y) != bluecore::theme::bg) {
                ++ink;
            }
        }
    }
    CHECK(ink > 200);

    bluecore::render(canvas, field.model());
    bool differs = false;
    for (int i = 0; i < bluecore::kScreenWidth * bluecore::kScreenHeight; ++i) {
        const uint16_t pixel = canvas.pixel(i % bluecore::kScreenWidth, i / bluecore::kScreenWidth);
        if (pixel != demoPixels[i]) {
            differs = true;
            break;
        }
    }
    CHECK(differs);
    CHECK(canvas.writePpm("build/host/ui_probe.ppm"));
}
