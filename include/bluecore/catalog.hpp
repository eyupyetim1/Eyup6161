#pragma once

#include <cstddef>

#include "bluecore/types.hpp"

// Sensör kataloğu.
//
// Görev: ad, birim, kalibrasyon ihtiyacı ve akla yatkınlık sınırını tek tabloda tutmak.
// İlişkili dosyalar: policy, demo senaryosu, canlı ölçüm ekranı.
// Donanım: tablo planlanan parçaları adlandırır; sürücü başlatmaz.
// Etki: sınır veya kalibrasyon ihtiyacı değişirse gösterim, alarm ve kayıt birlikte değişir.
//
// sanityMin/sanityMax kalibrasyon noktası değildir. Datasheet gelene kadar
// ürünün kabul edebileceği geçici aralıktır.

namespace bluecore {

struct SensorCatalogEntry {
    SensorId id;
    const char* label;
    const char* shortName;
    const char* unitText;
    Unit unit;
    CalibrationNeed calibration;
    float sanityMin;
    float sanityMax;
    const char* plannedPart;
};

inline constexpr SensorCatalogEntry kCatalog[] = {
    {SensorId::Temperature, "SICAKLIK", "Sıcaklık", "°C", Unit::Celsius,
     CalibrationNeed::NotRequired, -2.0f, 60.0f, "DS18B20"},
    {SensorId::Ph, "pH", "pH", "", Unit::Ph, CalibrationNeed::Required, 0.0f, 14.0f,
     "pH modülü"},
    {SensorId::DissolvedOxygen, "OKSİJEN", "Oksijen", "mg/L", Unit::MilligramsPerLiter,
     CalibrationNeed::Required, 0.0f, 20.0f, "DO modülü"},
    {SensorId::Conductivity, "EC", "İletkenlik", "µS/cm", Unit::MicrosiemensPerCentimeter,
     CalibrationNeed::Required, 0.0f, 200000.0f, "RS485 EC probu"},
    {SensorId::Turbidity, "BULANIKLIK", "Bulanıklık", "NTU", Unit::Ntu,
     CalibrationNeed::Unknown, 0.0f, 1000.0f, "bulanıklık modülü"},
};

static_assert(sizeof(kCatalog) / sizeof(kCatalog[0]) == static_cast<size_t>(kSensorCount),
              "katalog satırı sensör sayısıyla aynı olmalı");

inline const SensorCatalogEntry& catalog(SensorId id) {
    const int index = static_cast<int>(id);
    if (index < 0 || index >= kSensorCount) {
        return kCatalog[0];
    }
    return kCatalog[index];
}

inline const char* statusLabel(SensorStatus status) {
    switch (status) {
        case SensorStatus::Ready:
            return "HAZIR";
        case SensorStatus::Measuring:
            return "ÖLÇÜLÜYOR";
        case SensorStatus::NotConnected:
            return "BAĞLI DEĞİL";
        case SensorStatus::CalibrationRequired:
            return "KALİBRASYON GEREKLİ";
        case SensorStatus::ReadError:
            return "OKUMA HATASI";
        case SensorStatus::InvalidReading:
            return "GEÇERSİZ ÖLÇÜM";
    }
    return "BİLİNMİYOR";
}

}  // namespace bluecore
