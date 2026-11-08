#include "bluecore/alarms.hpp"
#include "bluecore/calibration.hpp"
#include "bluecore/catalog.hpp"
#include "bluecore/clock.hpp"
#include "bluecore/policy.hpp"
#include "bluecore/power.hpp"
#include "bluecore/radio.hpp"
#include "bluecore/storage.hpp"
#include "test/host/check.hpp"

namespace {

bluecore::Sample sampleAt(bluecore::SensorId id, float value, bluecore::DataOrigin origin,
                          bluecore::SensorStatus status, bool hasValue, bool calibrated) {
    bluecore::Sample sample{};
    sample.id = id;
    sample.unit = bluecore::catalog(id).unit;
    sample.value = value;
    sample.origin = origin;
    sample.status = status;
    sample.hasValue = hasValue ? 1 : 0;
    sample.calibrationValid = calibrated ? 1 : 0;
    sample.timeQuality = bluecore::TimeQuality::UptimeOnly;
    sample.error = bluecore::ErrorCode::None;
    return sample;
}

}  // namespace

void runPolicyTests() {
    char text[32];
    bluecore::formatFixed(text, sizeof(text), 22.6f, 1);
    CHECK_STREQ(text, "22.6");
    bluecore::formatFixed(text, sizeof(text), 7.42f, 2);
    CHECK_STREQ(text, "7.42");
    bluecore::formatFixed(text, sizeof(text), 8.14f, 2);
    CHECK_STREQ(text, "8.14");
    bluecore::formatFixed(text, sizeof(text), 485.0f, 0);
    CHECK_STREQ(text, "485");
    bluecore::formatFixed(text, sizeof(text), 2.4f, 1);
    CHECK_STREQ(text, "2.4");
    bluecore::formatFixed(text, sizeof(text), -1.5f, 1);
    CHECK_STREQ(text, "-1.5");

    const auto liveTemp = sampleAt(bluecore::SensorId::Temperature, 22.6f, bluecore::DataOrigin::LiveSensor,
                                    bluecore::SensorStatus::Ready, true, false);
    auto view = bluecore::present(liveTemp, false);
    CHECK(view.showNumber == 1);
    CHECK_STREQ(view.value, "22.6");
    CHECK_STREQ(view.unit, "°C");

    const auto barePh = sampleAt(bluecore::SensorId::Ph, 7.42f, bluecore::DataOrigin::LiveSensor,
                                  bluecore::SensorStatus::Ready, true, false);
    view = bluecore::present(barePh, false);
    CHECK(view.showNumber == 0);
    CHECK_STREQ(view.value, "—");
    CHECK_STREQ(view.status, "KALİBRASYON GEREKLİ");

    const auto calibratedPh = sampleAt(bluecore::SensorId::Ph, 7.42f, bluecore::DataOrigin::LiveSensor,
                                        bluecore::SensorStatus::Ready, true, true);
    view = bluecore::present(calibratedPh, false);
    CHECK(view.showNumber == 1);
    CHECK_STREQ(view.value, "7.42");

    const auto turbidity = sampleAt(bluecore::SensorId::Turbidity, 2.4f, bluecore::DataOrigin::LiveSensor,
                                     bluecore::SensorStatus::Ready, true, true);
    view = bluecore::present(turbidity, false);
    CHECK(view.showNumber == 0);
    CHECK_STREQ(view.status, "MODEL DOĞRULANMADI");

    const auto dishonest = sampleAt(bluecore::SensorId::Ph, 7.42f, bluecore::DataOrigin::None,
                                     bluecore::SensorStatus::Ready, true, true);
    view = bluecore::present(dishonest, false);
    CHECK(view.showNumber == 0);

    const auto demoOnField = sampleAt(bluecore::SensorId::Temperature, 22.6f, bluecore::DataOrigin::DemoScenario,
                                       bluecore::SensorStatus::Ready, true, false);
    view = bluecore::present(demoOnField, false);
    CHECK(view.showNumber == 0);
    CHECK_STREQ(view.status, "SENARYO");

    const auto liveOnDemo = sampleAt(bluecore::SensorId::Temperature, 18.0f, bluecore::DataOrigin::LiveSensor,
                                      bluecore::SensorStatus::Ready, true, false);
    view = bluecore::present(liveOnDemo, true);
    CHECK(view.showNumber == 0);
    CHECK_STREQ(view.status, "SAHA ÖLÇÜMÜ");

    auto bad = sampleAt(bluecore::SensorId::Temperature, 99.0f, bluecore::DataOrigin::LiveSensor,
                         bluecore::SensorStatus::Ready, true, false);
    CHECK(!bluecore::valueInSanity(bluecore::SensorId::Temperature, bad.value));
    view = bluecore::present(bad, false);
    CHECK(view.showNumber == 0);
    CHECK_STREQ(view.status, "GEÇERSİZ ÖLÇÜM");

    bad.value = 0.0f / 0.0f;
    bad.hasValue = 1;
    view = bluecore::present(bad, false);
    CHECK(view.showNumber == 0);

    const auto ec = sampleAt(bluecore::SensorId::Conductivity, 485.0f, bluecore::DataOrigin::LiveSensor,
                              bluecore::SensorStatus::Ready, true, true);
    view = bluecore::present(ec, false);
    CHECK_STREQ(view.value, "485");
    CHECK_STREQ(view.unit, "µS/cm");
    const auto ecHigh = sampleAt(bluecore::SensorId::Conductivity, 1500.0f, bluecore::DataOrigin::LiveSensor,
                                  bluecore::SensorStatus::Ready, true, true);
    view = bluecore::present(ecHigh, false);
    CHECK_STREQ(view.value, "1.50");
    CHECK_STREQ(view.unit, "mS/cm");

    bluecore::UptimeClock clock;
    CHECK(clock.quality() == bluecore::TimeQuality::UptimeOnly);
    bluecore::CivilTime civil{};
    CHECK(!clock.civil(civil));
    CHECK(!bluecore::formatCivil(clock, text, sizeof(text)));
    CHECK_STREQ(text, "doğrulanmadı");

    bluecore::CalibrationService calibration;
    float scaled = 123.0f;
    CHECK(!calibration.apply(bluecore::SensorId::Ph, 1.5f, scaled));
    CHECK(scaled == 123.0f);
    CHECK(!calibration.hasCoefficients(bluecore::SensorId::Ph));
    CHECK(calibration.state(bluecore::SensorId::DissolvedOxygen) == bluecore::CalibrationState::NotAvailable);
    const auto refused = calibration.start(bluecore::CalibrationRequest{bluecore::SensorId::Ph, 7.0f});
    CHECK(refused.accepted == 0);
    CHECK(calibration.factoryReset());
    CHECK(!calibration.hasCoefficients(bluecore::SensorId::Conductivity));

    bluecore::MemoryLogStore store;
    bluecore::LogRecord record{};
    record.timeQuality = bluecore::TimeQuality::UptimeOnly;
    record.year = 2026;
    record.samples[0] = demoOnField;
    CHECK(!store.append(record));
    CHECK(store.count() == 0);

    record = bluecore::LogRecord{};
    record.timeQuality = bluecore::TimeQuality::UptimeOnly;
    record.year = 2026;
    record.month = 11;
    record.samples[0] = liveTemp;
    record.samples[1] = barePh;
    CHECK(store.append(record));
    bluecore::LogRecord stored{};
    CHECK(store.get(0, stored));
    CHECK(stored.samples[0].hasValue == 1);
    CHECK(stored.samples[0].value == 22.6f);
    CHECK(stored.samples[1].hasValue == 0);
    CHECK(stored.year == 0);
    CHECK(stored.month == 0);
    CHECK(stored.timeQuality == bluecore::TimeQuality::UptimeOnly);

    bluecore::UnavailableLogStore closed;
    CHECK(!closed.append(record));
    CHECK(closed.count() == 0);

    bluecore::AlarmRule rule{1, bluecore::SensorId::Temperature, 0.0f, 30.0f};
    bluecore::AlarmEvent events[2]{};
    const bluecore::Sample disconnected = sampleAt(bluecore::SensorId::Temperature, 99.0f,
                                                    bluecore::DataOrigin::None, bluecore::SensorStatus::NotConnected,
                                                    false, false);
    CHECK(bluecore::evaluateAlarms(&rule, 1, &disconnected, 1, events, 2) == 0);
    CHECK(bluecore::evaluateAlarms(&rule, 1, &demoOnField, 1, events, 2) == 0);
    CHECK(bluecore::evaluateAlarms(&rule, 1, &liveTemp, 1, events, 2) == 0);
    const auto hot = sampleAt(bluecore::SensorId::Temperature, 31.0f, bluecore::DataOrigin::LiveSensor,
                               bluecore::SensorStatus::Ready, true, false);
    CHECK(bluecore::evaluateAlarms(&rule, 1, &hot, 1, events, 2) == 1);
    CHECK(events[0].high == 1);

    bluecore::UnverifiedPowerMonitor power;
    const auto powerReport = power.read();
    CHECK(powerReport.percentKnown == 0);
    CHECK(powerReport.voltageKnown == 0);
    CHECK(powerReport.status == bluecore::PowerStatus::Unknown);

    bluecore::DormantRadio radio;
    const auto wifi = radio.read();
    CHECK(wifi.status == bluecore::WifiStatus::Disabled);
    CHECK(wifi.ssid[0] == '\0');
}
