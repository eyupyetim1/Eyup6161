#include "bluecore/app.hpp"

#include <cstring>

#include "bluecore/board.hpp"
#include "bluecore/policy.hpp"
#include "bluecore/text_util.hpp"

namespace bluecore {

App::App() {
    hub_.bind(temperature_);
    hub_.bind(ph_);
    hub_.bind(dissolvedOxygen_);
    hub_.bind(conductivity_);
    hub_.bind(turbidity_);
}

void App::begin(OperatingMode mode, uint32_t nowMs) {
    mode_ = mode;
    startMs_ = nowMs;
    hub_.initializeAll();
    hub_.update(nowMs);
    if (mode_ == OperatingMode::Demo) {
        demo_.update(nowMs);
    }
    navigation_.start(nowMs);
    buttons_ = ButtonLogic();
    begun_ = 1;
    hasModel_ = 0;
    renderRequested_ = 0;
    refresh(nowMs);
}

void App::tick(uint32_t nowMs) {
    if (begun_ == 0) {
        return;
    }
    buttons_.poll(nowMs);
    ButtonEvent event{};
    while (buttons_.next(event)) {
        navigation_.handle(event);
    }
    navigation_.tick(nowMs);
    hub_.update(nowMs);
    if (mode_ == OperatingMode::Demo) {
        demo_.update(nowMs);
    }
    refresh(nowMs);
}

void App::setRawButton(ButtonId id, bool pressed, uint32_t nowMs) {
    if (begun_ == 0) {
        return;
    }
    buttons_.setRaw(id, pressed, nowMs);
}

bool App::pushDebugChar(char ch, uint32_t nowMs) {
    if (begun_ == 0) {
        return false;
    }
    ButtonId id;
    switch (ch) {
        case 'm':
        case 'M':
            id = ButtonId::Menu;
            break;
        case 'u':
        case 'U':
            id = ButtonId::Up;
            break;
        case 'd':
        case 'D':
            id = ButtonId::Down;
            break;
        case 'o':
        case 'O':
            id = ButtonId::Ok;
            break;
        case 'b':
        case 'B':
            id = ButtonId::Back;
            break;
        default:
            return false;
    }
    navigation_.handle(ButtonEvent{id, ButtonEventType::Click, nowMs});
    refresh(nowMs);
    return true;
}

bool App::consumeRenderRequest() {
    const bool requested = renderRequested_ != 0;
    renderRequested_ = 0;
    return requested;
}

void App::refresh(uint32_t nowMs) {
    snapshot_ = AppSnapshot{};
    snapshot_.mode = mode_;
    snapshot_.screen = navigation_.screen();
    snapshot_.menuIndex = navigation_.menuIndex();
    snapshot_.uptimeMs = nowMs - startMs_;
    const IMeasurementSource& shown = mode_ == OperatingMode::Demo
                                          ? static_cast<const IMeasurementSource&>(demo_)
                                          : static_cast<const IMeasurementSource&>(hub_);
    for (int i = 0; i < kSensorCount; ++i) {
        const auto id = static_cast<SensorId>(i);
        snapshot_.displayed[i] = shown.get(id);
        const Sample& hardware = hub_.get(id);
        snapshot_.hardwareStatus[i] = hardware.status;
        snapshot_.hardwareError[i] = hardware.error;
        const ISensor* sensor = hub_.sensor(id);
        snapshot_.hardwarePart[i] = sensor != nullptr ? sensor->part() : "";
    }
    snapshot_.power = powerMonitor_.read();
    snapshot_.wifi = radio_.read();
    snapshot_.timeQuality = clock_.quality();
    snapshot_.storageEnabled = 0;
    snapshot_.displayConfigured = displayPinsConfirmed() ? 1 : 0;
    snapshot_.gpioConfigured = gpioPinsConfirmed() ? 1 : 0;
    snapshot_.logCount = log_.count();
    snapshot_.alarmCount = 0;
    formatCivil(clock_, snapshot_.clockText, sizeof(snapshot_.clockText));
    copyText(snapshot_.boardText, sizeof(snapshot_.boardText), boardKindText());

    UiModel next{};
    buildUiModel(snapshot_, next);
    if (hasModel_ == 0 || std::memcmp(&next, &model_, sizeof(UiModel)) != 0) {
        model_ = next;
        hasModel_ = 1;
        renderRequested_ = 1;
    }
}

SelfCheck runSelfCheck(const AppSnapshot& snapshot) {
    SelfCheck check{};
    check.displayUnconfigured = snapshot.displayConfigured == 0 ? 1 : 0;
    check.gpioUnconfigured = snapshot.gpioConfigured == 0 ? 1 : 0;
    check.radioOff = snapshot.wifi.status == WifiStatus::Disabled ? 1 : 0;
    check.clockUnverified = snapshot.timeQuality == TimeQuality::Verified ? 0 : 1;
    check.storageOff = snapshot.storageEnabled == 0 ? 1 : 0;
    check.powerUnknown = (snapshot.power.percentKnown == 0 && snapshot.power.voltageKnown == 0) ? 1 : 0;
    check.hardwareSensorsAbsent = 1;
    for (int i = 0; i < kSensorCount; ++i) {
        if (snapshot.hardwareStatus[i] == SensorStatus::Ready ||
            snapshot.hardwareStatus[i] == SensorStatus::Measuring) {
            check.hardwareSensorsAbsent = 0;
        }
    }
    check.demoSeparated = 1;
    for (int i = 0; i < kSensorCount; ++i) {
        const Sample& sample = snapshot.displayed[i];
        if (snapshot.mode == OperatingMode::Demo) {
            if (sample.hasValue != 0 && sample.origin != DataOrigin::DemoScenario) {
                check.demoSeparated = 0;
            }
        } else if (sample.origin == DataOrigin::DemoScenario ||
                   (sample.hasValue != 0 && sample.origin != DataOrigin::LiveSensor)) {
            check.demoSeparated = 0;
        }
    }
    if (snapshot.mode == OperatingMode::Demo && check.hardwareSensorsAbsent == 0) {
        check.demoSeparated = 0;
    }
    return check;
}

}  // namespace bluecore
