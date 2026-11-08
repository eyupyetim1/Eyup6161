#pragma once

#include "bluecore/canvas.hpp"
#include "bluecore/ui_model.hpp"

// Ekran çizici.
//
// Görev: açılış, canlı ölçüm, menü ve bilgi ekranlarını tema ile çizmek.
// İlişkili dosyalar: src/ui/ui_renderer.cpp, ui_model.
// Donanım: Canvas gerçekleştirimine bağlıdır. NullCanvas pin açmaz.
// Etki: yerleşim değişirse 320×240 panel ve host önizlemesi birlikte değişir.
// Titreşim: App modeli değişmeden yeniden çizim istemez. Panel sürücüsü
// tek geçişte boyamalıdır; glyph arasında paneli silmemelidir.

namespace bluecore {

void render(Canvas& canvas, const UiModel& model);

}  // namespace bluecore
