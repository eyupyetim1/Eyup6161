#pragma once

#include <cstddef>

#include "bluecore/app.hpp"

// Seri rapor.
//
// Görev: panel yokken ekran durumunu ve donanım gerçeğini metin olarak vermek.
// İlişkili dosyalar: src/ui/text_report.cpp, src/main.cpp.
// Donanım: UART/USB seri kapısı main.cpp içinden yazılır.
// Etki: metin değişirse ilk cihaz denemesindeki gözlem de değişir.

namespace bluecore {

void formatBootBanner(const AppSnapshot& snapshot, char* out, size_t capacity);
void formatTextReport(const AppSnapshot& snapshot, const UiModel& model, char* out, size_t capacity);

}  // namespace bluecore
