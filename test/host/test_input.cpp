#include "bluecore/input.hpp"
#include "bluecore/navigation.hpp"
#include "test/host/check.hpp"

namespace {

bool take(bluecore::ButtonLogic& buttons, bluecore::ButtonEvent& event) { return buttons.next(event); }

}  // namespace

void runInputTests() {
    bluecore::ButtonLogic buttons;
    bluecore::ButtonEvent event{};
    buttons.setRaw(bluecore::ButtonId::Up, true, 0);
    buttons.poll(29);
    CHECK(!take(buttons, event));
    buttons.poll(30);
    CHECK(take(buttons, event));
    CHECK(event.id == bluecore::ButtonId::Up);
    CHECK(event.type == bluecore::ButtonEventType::Click);
    CHECK(!take(buttons, event));

    buttons.poll(629);
    CHECK(!take(buttons, event));
    buttons.poll(630);
    CHECK(take(buttons, event));
    CHECK(event.type == bluecore::ButtonEventType::LongPress);
    buttons.poll(780);
    CHECK(take(buttons, event));
    CHECK(event.type == bluecore::ButtonEventType::Repeat);
    buttons.setRaw(bluecore::ButtonId::Up, false, 800);
    buttons.poll(830);
    CHECK(!take(buttons, event));

    bluecore::ButtonLogic ok;
    ok.setRaw(bluecore::ButtonId::Ok, true, 0);
    ok.poll(30);
    CHECK(!take(ok, event));
    ok.setRaw(bluecore::ButtonId::Ok, false, 100);
    ok.poll(130);
    CHECK(take(ok, event));
    CHECK(event.type == bluecore::ButtonEventType::Click);

    bluecore::ButtonLogic held;
    held.setRaw(bluecore::ButtonId::Ok, true, 0);
    held.poll(30);
    held.poll(630);
    CHECK(take(held, event));
    CHECK(event.type == bluecore::ButtonEventType::LongPress);
    held.setRaw(bluecore::ButtonId::Ok, false, 700);
    held.poll(730);
    CHECK(!take(held, event));

    bluecore::ButtonLogic bounce;
    bounce.setRaw(bluecore::ButtonId::Down, true, 0);
    bounce.setRaw(bluecore::ButtonId::Down, false, 10);
    bounce.setRaw(bluecore::ButtonId::Down, true, 20);
    bounce.poll(49);
    CHECK(!take(bounce, event));
    bounce.poll(50);
    CHECK(take(bounce, event));
    CHECK(event.id == bluecore::ButtonId::Down);
    CHECK(event.type == bluecore::ButtonEventType::Click);

    bluecore::Navigation navigation;
    navigation.start(0);
    CHECK(navigation.screen() == bluecore::ScreenId::Boot);
    navigation.tick(1599);
    CHECK(navigation.screen() == bluecore::ScreenId::Boot);
    navigation.tick(1600);
    CHECK(navigation.screen() == bluecore::ScreenId::Live);

    navigation.start(100);
    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Ok, bluecore::ButtonEventType::Click, 120});
    CHECK(navigation.screen() == bluecore::ScreenId::Live);
    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Menu, bluecore::ButtonEventType::Click, 130});
    CHECK(navigation.screen() == bluecore::ScreenId::Menu);
    CHECK(navigation.menuIndex() == 0);
    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Down, bluecore::ButtonEventType::Click, 140});
    CHECK(navigation.menuIndex() == 1);
    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Up, bluecore::ButtonEventType::Repeat, 150});
    CHECK(navigation.menuIndex() == 0);
    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Up, bluecore::ButtonEventType::LongPress, 160});
    CHECK(navigation.menuIndex() == 0);
    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Ok, bluecore::ButtonEventType::Click, 170});
    CHECK(navigation.screen() == bluecore::ScreenId::Live);

    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Menu, bluecore::ButtonEventType::Click, 180});
    for (int i = 0; i < 3; ++i) {
        navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Down, bluecore::ButtonEventType::Click,
                                                static_cast<uint32_t>(200 + i)});
    }
    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Ok, bluecore::ButtonEventType::Click, 210});
    CHECK(navigation.screen() == bluecore::ScreenId::Calibration);
    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Back, bluecore::ButtonEventType::Click, 220});
    CHECK(navigation.screen() == bluecore::ScreenId::Menu);
    CHECK(navigation.menuIndex() == 3);
    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Back, bluecore::ButtonEventType::Click, 230});
    CHECK(navigation.screen() == bluecore::ScreenId::Live);
    navigation.handle(bluecore::ButtonEvent{bluecore::ButtonId::Back, bluecore::ButtonEventType::Click, 240});
    CHECK(navigation.screen() == bluecore::ScreenId::Live);
}
