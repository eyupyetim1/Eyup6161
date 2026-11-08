#pragma once

#include <cstddef>

#include "bluecore/types.hpp"

// Ölçümün ekrana ve kayda çıkma kuralları.
//
// Görev: demo sayısını saha ölçümü gibi, kalibrasyonsuz pH/EC/DO değerini kesin sonuç gibi
// göstermeyi engellemek.
// İlişkili dosyalar: src/core/policy.cpp, ui_model, storage.
// Donanım: yok.
// Etki: kural gevşetilirse canlı ekran, alarm ve kayıt aynı anda güvenilirliğini kaybeder.

namespace bluecore {

struct Presentation {
    uint8_t showNumber;
    char value[16];
    char unit[16];
    char status[32];
};

bool valueInSanity(SensorId id, float value);
bool numericClaimAllowed(const Sample& sample);
Presentation present(const Sample& sample, bool demoScreen);

void formatFixed(char* out, size_t capacity, float value, int decimals);

}  // namespace bluecore
