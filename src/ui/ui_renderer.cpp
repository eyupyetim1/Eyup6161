#include "bluecore/ui_renderer.hpp"

#include <cstdio>

#include "bluecore/text_util.hpp"
#include "bluecore/theme.hpp"
#include "bluecore/version.hpp"

namespace bluecore {
namespace {

int textY(int top, int box, FontId font) {
    const int height = face(font).lineHeight;
    if (box <= height) {
        return top;
    }
    return top + (box - height) / 2;
}

void drawPill(Canvas& canvas, int x, int y, const char* text, uint16_t background, uint16_t foreground) {
    const int small = face(FontId::Small).lineHeight;
    const int width = measureText(FontId::Small, text) + 14;
    const int height = small + 6;
    canvas.fillRect(x, y, width, height, background);
    canvas.drawText(x + 7, y + 3, text, FontId::Small, foreground);
}

int drawChrome(Canvas& canvas, const UiModel& model) {
    const int width = canvas.width();
    const int brand = face(FontId::Value).lineHeight;
    const int small = face(FontId::Small).lineHeight;
    canvas.fillRect(0, 0, width, 3, theme::accent);
    const int y = 6;
    canvas.drawText(12, y, kProductName, FontId::Value, theme::text);
    const int pillW = measureText(FontId::Small, model.pill) + 14;
    const int pillH = small + 6;
    const int pillX = width - 12 - pillW;
    const int pillY = y + (brand - pillH) / 2;
    if (model.demoMode) {
        drawPill(canvas, pillX, pillY, model.pill, theme::warn, theme::bg);
    } else {
        drawPill(canvas, pillX, pillY, model.pill, theme::accent, theme::text);
    }
    const int subY = y + brand + 1;
    canvas.drawText(12, subY, model.subtitle, FontId::Small, theme::textDim);
    return subY + small + 4;
}

void drawHint(Canvas& canvas, const UiModel& model) {
    if (model.hint[0] == '\0') {
        return;
    }
    const int height = canvas.height();
    const int small = face(FontId::Small).lineHeight;
    const int bar = small + 10;
    canvas.fillRect(0, height - bar, canvas.width(), bar, theme::surface);
    canvas.fillRect(0, height - bar, canvas.width(), 1, theme::line);
    canvas.drawText(12, height - bar + 6, model.hint, FontId::Small, theme::textDim);
}

void renderBoot(Canvas& canvas, const UiModel& model) {
    const int width = canvas.width();
    const int height = canvas.height();
    canvas.fillRect(0, 0, width, 3, theme::accent);
    canvas.fillRect(0, height - 3, width, 3, theme::accent);

    const char* brand = kProductName;
    const int brandW = measureText(FontId::Title, brand);
    int y = 62;
    canvas.drawText((width - brandW) / 2, y, brand, FontId::Title, theme::text);
    y += face(FontId::Title).lineHeight + 8;
    canvas.fillRect((width - 48) / 2, y, 48, 2, theme::accent);
    y += 14;
    const int modelW = measureText(FontId::Body, kModel);
    canvas.drawText((width - modelW) / 2, y, kModel, FontId::Body, theme::text);
    y += face(FontId::Body).lineHeight + 6;
    const char* tag = "Taşınabilir su kalitesi";
    const int tagW = measureText(FontId::Small, tag);
    canvas.drawText((width - tagW) / 2, y, tag, FontId::Small, theme::textDim);
    y += face(FontId::Small).lineHeight + 16;

    const int pillW = measureText(FontId::Small, model.pill) + 14;
    if (model.demoMode) {
        drawPill(canvas, (width - pillW) / 2, y, model.pill, theme::warn, theme::bg);
    } else {
        drawPill(canvas, (width - pillW) / 2, y, model.pill, theme::accent, theme::text);
    }

    char version[32];
    copyText(version, sizeof(version), "Sürüm ");
    size_t used = 0;
    while (version[used] != '\0') {
        ++used;
    }
    appendText(version, sizeof(version), used, kFirmwareVersion);
    const int verW = measureText(FontId::Small, version);
    canvas.drawText((width - verW) / 2, height - 42, version, FontId::Small, theme::textDim);
    const int noteW = measureText(FontId::Small, model.notice);
    canvas.drawText((width - noteW) / 2, height - 24, model.notice, FontId::Small, theme::textDim);
}

void renderLive(Canvas& canvas, const UiModel& model) {
    const int width = canvas.width();
    const int height = canvas.height();
    int y = drawChrome(canvas, model);
    const uint16_t noticeColor = model.noticeWarning ? theme::warn : theme::textDim;
    canvas.drawText(12, y, model.notice, FontId::Small, noticeColor);
    y += face(FontId::Small).lineHeight + 2;
    canvas.fillRect(12, y, width - 24, 1, theme::line);
    y += 3;

    const int small = face(FontId::Small).lineHeight;
    const int footer = small + 14;
    const int rowSpace = (height - footer) - y;
    const int rowH = rowSpace > 0 ? rowSpace / kSensorCount : 0;
    for (int i = 0; i < kSensorCount; ++i) {
        const MeasureRow& row = model.rows[i];
        const int rowY = y + i * rowH;
        canvas.drawText(12, textY(rowY, rowH, FontId::Body), row.label, FontId::Body, theme::textDim);
        int right = width - 12;
        if (row.numeric) {
            if (row.unit[0] != '\0') {
                const int unitW = measureText(FontId::Small, row.unit);
                right -= unitW;
                canvas.drawText(right, textY(rowY, rowH, FontId::Small), row.unit, FontId::Small,
                                theme::textDim);
                right -= 6;
            }
            const int valueW = measureText(FontId::Value, row.value);
            canvas.drawText(right - valueW, textY(rowY, rowH, FontId::Value), row.value, FontId::Value,
                            theme::text);
        } else {
            const char* status = row.status[0] != '\0' ? row.status : "—";
            const int statusW = measureText(FontId::Small, status);
            canvas.drawText(right - statusW, textY(rowY, rowH, FontId::Small), status, FontId::Small,
                            theme::warn);
            const int dashW = measureText(FontId::Value, row.value);
            canvas.drawText(right - statusW - 8 - dashW, textY(rowY, rowH, FontId::Value), row.value,
                            FontId::Value, theme::textDim);
        }
    }

    const int footerTop = height - footer;
    canvas.fillRect(0, footerTop, width, footer, theme::surface);
    canvas.fillRect(0, footerTop, width, 1, theme::line);
    const int cell = width / 4;
    for (int i = 0; i < 4; ++i) {
        char cellText[40];
        std::snprintf(cellText, sizeof(cellText), "%s %s", model.footerLabel[i], model.footerValue[i]);
        canvas.drawText(i * cell + 8, footerTop + 7, cellText, FontId::Small, theme::text);
    }
}

void renderMenu(Canvas& canvas, const UiModel& model) {
    int y = drawChrome(canvas, model);
    const int itemH = face(FontId::Small).lineHeight + 2;
    const int width = canvas.width();
    for (int i = 0; i < model.menuCount; ++i) {
        const int rowY = y + i * itemH;
        if (i == model.menuIndex) {
            canvas.fillRect(8, rowY, width - 16, itemH - 1, theme::surfaceHigh);
            canvas.fillRect(8, rowY, 3, itemH - 1, theme::accent);
        }
        const uint16_t color = i == model.menuIndex ? theme::text : theme::textDim;
        canvas.drawText(18, rowY + 2, model.menu[i], FontId::Small, color);
    }
    drawHint(canvas, model);
}

void renderInfo(Canvas& canvas, const UiModel& model) {
    int y = drawChrome(canvas, model);
    const int step = face(FontId::Small).lineHeight + 2;
    const int limit = canvas.height() - (model.hint[0] != '\0' ? face(FontId::Small).lineHeight + 14 : 8);
    for (int i = 0; i < model.bodyCount; ++i) {
        if (y + step > limit) {
            break;
        }
        canvas.drawText(12, y, model.body[i], FontId::Small, theme::text);
        y += step;
    }
    drawHint(canvas, model);
}

}  // namespace

void render(Canvas& canvas, const UiModel& model) {
    canvas.fillRect(0, 0, canvas.width(), canvas.height(), theme::bg);
    switch (model.screen) {
        case ScreenId::Boot:
            renderBoot(canvas, model);
            break;
        case ScreenId::Live:
            renderLive(canvas, model);
            break;
        case ScreenId::Menu:
            renderMenu(canvas, model);
            break;
        case ScreenId::History:
        case ScreenId::Sensors:
        case ScreenId::Calibration:
        case ScreenId::Alarms:
        case ScreenId::Wifi:
        case ScreenId::Power:
        case ScreenId::System:
            renderInfo(canvas, model);
            break;
    }
}

}  // namespace bluecore
