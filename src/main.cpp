#include <Arduino.h>

#include "bluecore/app.hpp"
#include "bluecore/null_canvas.hpp"
#include "bluecore/text_report.hpp"
#include "bluecore/ui_renderer.hpp"

// Cihaz girişi.
//
// Görev: seri kapıdan tuş kabul edip ekran modelini boş yüzeye çizmek.
// İlişkili dosyalar: App, NullCanvas, text_report.
// Donanım: USB seri. TFT, sensör, radyo ve pil ADC'si başlatılmaz.
// Etki: bu dosyaya pin kurulumu eklenirse doğrulanmamış karta enerji gidebilir.

namespace {

bluecore::App app;
bluecore::NullCanvas canvas;
char report[1600];
uint32_t lastBeatMs = 0;

bluecore::OperatingMode firmwareMode() {
#if defined(BLUECORE_FIELD_MODE)
    return bluecore::OperatingMode::Field;
#else
    return bluecore::OperatingMode::Demo;
#endif
}

}  // namespace

void setup() {
    Serial.begin(115200);
#if defined(ARDUINO_USB_CDC_ON_BOOT) && ARDUINO_USB_CDC_ON_BOOT
    const uint32_t start = millis();
    while (!Serial && (millis() - start) < 1500) {
        delay(10);
    }
#endif
    app.begin(firmwareMode(), millis());
    bluecore::formatBootBanner(app.snapshot(), report, sizeof(report));
    Serial.println();
    Serial.println(report);
    bluecore::render(canvas, app.model());
    app.consumeRenderRequest();
    lastBeatMs = millis();
}

void loop() {
    const uint32_t now = millis();
    while (Serial.available() > 0) {
        const char ch = static_cast<char>(Serial.read());
        app.pushDebugChar(ch, now);
    }
    app.tick(now);
    if (app.consumeRenderRequest()) {
        bluecore::formatTextReport(app.snapshot(), app.model(), report, sizeof(report));
        Serial.println(report);
        bluecore::render(canvas, app.model());
        lastBeatMs = now;
    } else if ((now - lastBeatMs) >= 5000) {
        Serial.print("BC-01 yaşam ");
        Serial.print(app.snapshot().uptimeMs);
        Serial.println(" ms");
        lastBeatMs = now;
    }
}
