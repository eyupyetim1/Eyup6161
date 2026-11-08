#pragma once

#include "bluecore/alarms.hpp"
#include "bluecore/calibration.hpp"
#include "bluecore/clock.hpp"
#include "bluecore/demo_scenario.hpp"
#include "bluecore/input.hpp"
#include "bluecore/navigation.hpp"
#include "bluecore/power.hpp"
#include "bluecore/radio.hpp"
#include "bluecore/sensor_hub.hpp"
#include "bluecore/sensors/conductivity_sensor.hpp"
#include "bluecore/sensors/dissolved_oxygen_sensor.hpp"
#include "bluecore/sensors/ph_sensor.hpp"
#include "bluecore/sensors/temperature_sensor.hpp"
#include "bluecore/sensors/turbidity_sensor.hpp"
#include "bluecore/storage.hpp"
#include "bluecore/ui_model.hpp"

// Uygulama.
//
// Görev: açılış, ölçüm kaynağı, gezinme ve ekran modelini bir döngüde birleştirmek.
// İlişkili dosyalar: src/core/app.cpp, src/main.cpp.
// Donanım: sahip olduğu sensör nesneleri bu sürümde pin açmaz.
// Etki: kaynak seçimi değişirse demo sayıları saha ekranına karışabilir.

namespace bluecore {

struct AppSnapshot {
    OperatingMode mode;
    ScreenId screen;
    int menuIndex;
    uint32_t uptimeMs;
    Sample displayed[kSensorCount];
    SensorStatus hardwareStatus[kSensorCount];
    ErrorCode hardwareError[kSensorCount];
    const char* hardwarePart[kSensorCount];
    PowerReport power;
    WifiReport wifi;
    TimeQuality timeQuality;
    uint8_t storageEnabled;
    uint8_t displayConfigured;
    uint8_t gpioConfigured;
    uint32_t logCount;
    int alarmCount;
    char clockText[32];
    char boardText[48];
};

struct SelfCheck {
    uint8_t displayUnconfigured;
    uint8_t gpioUnconfigured;
    uint8_t radioOff;
    uint8_t clockUnverified;
    uint8_t storageOff;
    uint8_t powerUnknown;
    uint8_t hardwareSensorsAbsent;
    uint8_t demoSeparated;
};

class App {
 public:
    App();

    void begin(OperatingMode mode, uint32_t nowMs);
    void tick(uint32_t nowMs);
    void setRawButton(ButtonId id, bool pressed, uint32_t nowMs);
    bool pushDebugChar(char ch, uint32_t nowMs);
    bool consumeRenderRequest();

    const AppSnapshot& snapshot() const { return snapshot_; }
    const UiModel& model() const { return model_; }
    bool begun() const { return begun_ != 0; }

 private:
    void refresh(uint32_t nowMs);

    OperatingMode mode_ = OperatingMode::Demo;
    uint32_t startMs_ = 0;
    uint8_t begun_ = 0;
    uint8_t hasModel_ = 0;
    uint8_t renderRequested_ = 0;

    TemperatureSensor temperature_;
    PhSensor ph_;
    DissolvedOxygenSensor dissolvedOxygen_;
    ConductivitySensor conductivity_;
    TurbiditySensor turbidity_;
    SensorHub hub_;
    DemoScenario demo_;
    Navigation navigation_;
    ButtonLogic buttons_;
    UnverifiedPowerMonitor powerMonitor_;
    DormantRadio radio_;
    UptimeClock clock_;
    CalibrationService calibration_;
    UnavailableLogStore log_;

    AppSnapshot snapshot_{};
    UiModel model_{};
};

SelfCheck runSelfCheck(const AppSnapshot& snapshot);

}  // namespace bluecore
