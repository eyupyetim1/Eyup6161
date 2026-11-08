#pragma once

#include <cstdint>

// BlueCore genel tipleri.
//
// Görev: ölçüm, zaman, güç ve ekran kimliklerini tek yerde tutmak.
// İlişkili dosyalar: policy, sensörler, kayıt, arayüz.
// Donanım: yok. Bu başlık GPIO veya veri yolu açmaz.
// Etki: alan eklemek kayıt düzenini, ekran modelini ve testleri birlikte etkiler.

namespace bluecore {

enum class SensorId : uint8_t {
    Temperature = 0,
    Ph,
    DissolvedOxygen,
    Conductivity,
    Turbidity,
    Count
};

constexpr int kSensorCount = static_cast<int>(SensorId::Count);

enum class Unit : uint8_t {
    None = 0,
    Celsius,
    Ph,
    MilligramsPerLiter,
    MicrosiemensPerCentimeter,
    MillisiemensPerCentimeter,
    Ntu
};

enum class SensorStatus : uint8_t {
    Ready = 0,
    Measuring,
    NotConnected,
    CalibrationRequired,
    ReadError,
    InvalidReading
};

// Bir sayının nereden geldiği. Demo ve saha aynı alanda taşınmaz.
enum class DataOrigin : uint8_t {
    None = 0,
    LiveSensor,
    DemoScenario
};

// Verified bu sürümde hiçbir saat kaynağı tarafından üretilmez.
enum class TimeQuality : uint8_t {
    Unknown = 0,
    UptimeOnly,
    Verified
};

enum class ErrorCode : uint16_t {
    None = 0,
    HardwareUnverified,
    NotConnected,
    InvalidRange,
    NonFinite,
    CalibrationMissing,
    SensorModelUnknown,
    ReadFailed,
    DemoNotLoggable,
    StorageUnavailable,
    PowerUnknown
};

enum class OperatingMode : uint8_t {
    Demo = 0,
    Field = 1
};

enum class ScreenId : uint8_t {
    Boot = 0,
    Live,
    Menu,
    History,
    Sensors,
    Calibration,
    Alarms,
    Wifi,
    Power,
    System
};

enum class CalibrationNeed : uint8_t {
    NotRequired = 0,
    Required,
    Unknown
};

enum class CalibrationState : uint8_t {
    NotAvailable = 0,
    Required,
    Valid,
    Failed
};

enum class ButtonId : uint8_t {
    Menu = 0,
    Up,
    Down,
    Ok,
    Back,
    Count
};

constexpr int kButtonCount = static_cast<int>(ButtonId::Count);

enum class ButtonEventType : uint8_t {
    Click = 0,
    LongPress,
    Repeat
};

enum class PowerStatus : uint8_t {
    Unknown = 0,
    Discharging,
    Charging,
    Full,
    Low,
    Critical
};

enum class WifiStatus : uint8_t {
    Disabled = 0,
    Disconnected,
    Connecting,
    Connected,
    Error
};

enum class BoardKind : uint8_t {
    Unverified = 0,
    Esp32Classic,
    Esp32S3
};

struct Sample {
    SensorId id;
    Unit unit;
    SensorStatus status;
    DataOrigin origin;
    TimeQuality timeQuality;
    ErrorCode error;
    float value;
    uint32_t monotonicMs;
    uint8_t hasValue;
    uint8_t calibrationValid;
};

struct CivilTime {
    uint16_t year;
    uint8_t month;
    uint8_t day;
    uint8_t hour;
    uint8_t minute;
    uint8_t second;
};

struct PowerReport {
    PowerStatus status;
    DataOrigin origin;
    float voltageV;
    uint8_t percent;
    uint8_t percentKnown;
    uint8_t voltageKnown;
};

struct WifiReport {
    WifiStatus status;
    DataOrigin origin;
    char ssid[33];
};

struct ButtonEvent {
    ButtonId id;
    ButtonEventType type;
    uint32_t atMs;
};

struct CalibrationRequest {
    SensorId id;
    float reference;
};

struct CalibrationResponse {
    uint8_t accepted;
    CalibrationState state;
    const char* message;
};

}  // namespace bluecore
