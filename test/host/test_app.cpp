#include "bluecore/app.hpp"
#include "bluecore/board.hpp"
#include "bluecore/demo_scenario.hpp"
#include "bluecore/sensor.hpp"
#include "bluecore/sensor_hub.hpp"
#include "bluecore/text_report.hpp"
#include "test/host/check.hpp"

namespace {

class FakeSensor : public bluecore::ISensor {
 public:
    explicit FakeSensor(bluecore::SensorId id, bluecore::SensorStatus status, bluecore::ErrorCode error)
        : id_(id), status_(status), error_(error) {}

    bluecore::SensorId id() const override { return id_; }
    const char* name() const override { return "deneme"; }
    const char* part() const override { return "yok"; }
    void initialize() override { ++inits; }
    void read(uint32_t nowMs) override {
        ++reads;
        sample_ = bluecore::Sample{};
        sample_.id = id_;
        sample_.status = status_;
        sample_.error = error_;
        sample_.origin = bluecore::DataOrigin::None;
        sample_.monotonicMs = nowMs;
    }
    bluecore::CalibrationResponse calibrate(const bluecore::CalibrationRequest&) override {
        return {};
    }
    bluecore::SensorStatus getStatus() const override { return sample_.status; }
    const bluecore::Sample& getLastMeasurement() const override { return sample_; }
    bluecore::ErrorCode getError() const override { return sample_.error; }

    int inits = 0;
    int reads = 0;

 private:
    bluecore::SensorId id_;
    bluecore::SensorStatus status_;
    bluecore::ErrorCode error_;
    bluecore::Sample sample_{};
};

}  // namespace

void runAppTests() {
    CHECK(!bluecore::boardIdentityConfirmed());
    CHECK(!bluecore::displayPinsConfirmed());
    CHECK(!bluecore::gpioPinsConfirmed());
    CHECK(bluecore::boardKind() == bluecore::BoardKind::Unverified);

    bluecore::App demo;
    demo.begin(bluecore::OperatingMode::Demo, 1000);
    CHECK(demo.snapshot().screen == bluecore::ScreenId::Boot);
    CHECK(demo.consumeRenderRequest());
    demo.tick(1000 + bluecore::kBootHoldMs);
    const bluecore::AppSnapshot& shown = demo.snapshot();
    CHECK(shown.screen == bluecore::ScreenId::Live);
    CHECK(shown.mode == bluecore::OperatingMode::Demo);
    CHECK(shown.displayed[0].origin == bluecore::DataOrigin::DemoScenario);
    CHECK(shown.displayed[0].hasValue == 1);
    CHECK(shown.displayed[0].value == bluecore::demo_values::kTemperatureC);
    CHECK(shown.displayed[1].value == bluecore::demo_values::kPh);
    CHECK(shown.displayed[2].value == bluecore::demo_values::kDissolvedOxygenMgL);
    CHECK(shown.displayed[3].value == bluecore::demo_values::kConductivityUs);
    CHECK(shown.hardwareStatus[0] == bluecore::SensorStatus::NotConnected);
    CHECK(shown.hardwareError[0] == bluecore::ErrorCode::HardwareUnverified);
    CHECK(shown.power.percentKnown == 0);
    CHECK(shown.wifi.status == bluecore::WifiStatus::Disabled);
    CHECK(shown.timeQuality == bluecore::TimeQuality::UptimeOnly);
    CHECK(shown.displayConfigured == 0);
    CHECK(shown.storageEnabled == 0);
    CHECK_STREQ(demo.model().rows[0].value, "22.6");
    CHECK_STREQ(demo.model().rows[1].value, "7.42");
    CHECK_STREQ(demo.model().rows[2].value, "8.14");
    CHECK_STREQ(demo.model().rows[3].value, "485");
    CHECK_STREQ(demo.model().pill, "DEMO");
    CHECK(demo.model().noticeWarning == 1);

    const bluecore::SelfCheck demoCheck = bluecore::runSelfCheck(shown);
    CHECK(demoCheck.demoSeparated == 1);
    CHECK(demoCheck.hardwareSensorsAbsent == 1);
    CHECK(demoCheck.displayUnconfigured == 1);
    CHECK(demoCheck.gpioUnconfigured == 1);
    CHECK(demoCheck.radioOff == 1);
    CHECK(demoCheck.clockUnverified == 1);
    CHECK(demoCheck.storageOff == 1);
    CHECK(demoCheck.powerUnknown == 1);

    char report[1600];
    bluecore::formatTextReport(shown, demo.model(), report, sizeof(report));
    CHECK(std::strstr(report, "22.6") != nullptr);
    CHECK(std::strstr(report, "SENARYO") != nullptr);
    CHECK(std::strstr(report, "Donanım sensörleri: 0/5") != nullptr);
    CHECK(std::strstr(report, "TFT: başlatılmadı") != nullptr);

    bluecore::App field;
    field.begin(bluecore::OperatingMode::Field, 0);
    field.tick(bluecore::kBootHoldMs);
    const bluecore::AppSnapshot& fieldSnap = field.snapshot();
    CHECK(fieldSnap.mode == bluecore::OperatingMode::Field);
    for (int i = 0; i < bluecore::kSensorCount; ++i) {
        CHECK(fieldSnap.displayed[i].hasValue == 0);
        CHECK(fieldSnap.displayed[i].origin == bluecore::DataOrigin::None);
        CHECK(fieldSnap.hardwareStatus[i] == bluecore::SensorStatus::NotConnected);
        CHECK_STREQ(field.model().rows[i].value, "—");
        CHECK(std::strcmp(field.model().rows[i].value, "22.6") != 0);
    }
    CHECK_STREQ(field.model().pill, "SAHA");
    CHECK_STREQ(field.model().rows[0].status, "BAĞLI DEĞİL");
    bluecore::formatTextReport(fieldSnap, field.model(), report, sizeof(report));
    CHECK(std::strstr(report, "22.6") == nullptr);
    CHECK(std::strstr(report, "7.42") == nullptr);
    CHECK(std::strstr(report, "8.14") == nullptr);
    CHECK(std::strstr(report, "485") == nullptr);
    const bluecore::SelfCheck fieldCheck = bluecore::runSelfCheck(fieldSnap);
    CHECK(fieldCheck.demoSeparated == 1);

    CHECK(field.pushDebugChar('m', 2000));
    CHECK(field.snapshot().screen == bluecore::ScreenId::Menu);
    CHECK(field.pushDebugChar('d', 2001));
    CHECK(field.snapshot().menuIndex == 1);
    CHECK(field.pushDebugChar('o', 2002));
    CHECK(field.snapshot().screen == bluecore::ScreenId::History);
    CHECK(field.pushDebugChar('b', 2003));
    CHECK(field.snapshot().screen == bluecore::ScreenId::Menu);
    CHECK(!field.pushDebugChar('x', 2004));
    char tiny[1];
    bluecore::formatBootBanner(fieldSnap, tiny, 0);
    bluecore::formatBootBanner(fieldSnap, tiny, 1);
    CHECK(tiny[0] == '\0');

    FakeSensor broken(bluecore::SensorId::Temperature, bluecore::SensorStatus::ReadError,
                      bluecore::ErrorCode::ReadFailed);
    FakeSensor quiet(bluecore::SensorId::Ph, bluecore::SensorStatus::NotConnected,
                     bluecore::ErrorCode::NotConnected);
    bluecore::SensorHub hub;
    hub.bind(broken);
    hub.bind(quiet);
    hub.initializeAll();
    hub.update(10);
    hub.update(20);
    CHECK(broken.inits == 1);
    CHECK(quiet.inits == 1);
    CHECK(broken.reads == 2);
    CHECK(quiet.reads == 2);
    CHECK(hub.get(bluecore::SensorId::Temperature).status == bluecore::SensorStatus::ReadError);
    CHECK(hub.get(bluecore::SensorId::Ph).status == bluecore::SensorStatus::NotConnected);
    CHECK(hub.get(bluecore::SensorId::Ph).hasValue == 0);
    CHECK(hub.get(bluecore::SensorId::Conductivity).status == bluecore::SensorStatus::NotConnected);
}
