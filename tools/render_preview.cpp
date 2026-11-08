#include <cstdio>
#include <cstring>

#include "bluecore/app.hpp"
#include "bluecore/ui_renderer.hpp"
#include "framebuffer.hpp"

namespace {

void renderMode(bluecore::OperatingMode mode, const char* bootPath, const char* livePath,
                const char* menuPath, const char* systemPath) {
    static bluecore::FramebufferCanvas canvas;
    bluecore::App app;
    app.begin(mode, 0);
    bluecore::render(canvas, app.model());
    canvas.writePpm(bootPath);
    app.tick(bluecore::kBootHoldMs);
    bluecore::render(canvas, app.model());
    canvas.writePpm(livePath);
    app.pushDebugChar('m', bluecore::kBootHoldMs + 1);
    bluecore::render(canvas, app.model());
    canvas.writePpm(menuPath);
    for (int i = 0; i < 7; ++i) {
        app.pushDebugChar('d', bluecore::kBootHoldMs + 2 + i);
    }
    app.pushDebugChar('o', bluecore::kBootHoldMs + 20);
    bluecore::render(canvas, app.model());
    canvas.writePpm(systemPath);
}

}  // namespace

int main(int argc, char** argv) {
    const char* dir = argc > 1 ? argv[1] : "build/preview";
    char path[256];
    std::snprintf(path, sizeof(path), "%s/boot_demo.ppm", dir);
    char live[256];
    std::snprintf(live, sizeof(live), "%s/live_demo.ppm", dir);
    char menu[256];
    std::snprintf(menu, sizeof(menu), "%s/menu_demo.ppm", dir);
    char system[256];
    std::snprintf(system, sizeof(system), "%s/system_demo.ppm", dir);
    renderMode(bluecore::OperatingMode::Demo, path, live, menu, system);

    std::snprintf(path, sizeof(path), "%s/boot_field.ppm", dir);
    std::snprintf(live, sizeof(live), "%s/live_field.ppm", dir);
    std::snprintf(menu, sizeof(menu), "%s/menu_field.ppm", dir);
    std::snprintf(system, sizeof(system), "%s/system_field.ppm", dir);
    renderMode(bluecore::OperatingMode::Field, path, live, menu, system);
    std::printf("onizleme yazildi: %s\n", dir);
    return 0;
}
