#include "bluecore/navigation.hpp"

namespace bluecore {

void Navigation::start(uint32_t nowMs) {
    screen_ = ScreenId::Boot;
    menuIndex_ = 0;
    bootStartMs_ = nowMs;
    bootDone_ = 0;
}

void Navigation::finishBoot() {
    bootDone_ = 1;
    screen_ = ScreenId::Live;
}

bool Navigation::tick(uint32_t nowMs) {
    if (bootDone_ != 0) {
        return false;
    }
    if ((nowMs - bootStartMs_) >= kBootHoldMs) {
        finishBoot();
        return true;
    }
    return false;
}

void Navigation::openMenu() {
    for (int i = 0; i < kMenuCount; ++i) {
        if (kMenuEntries[i].screen == screen_) {
            menuIndex_ = i;
            break;
        }
    }
    screen_ = ScreenId::Menu;
}

void Navigation::handle(const ButtonEvent& event) {
    if (event.type == ButtonEventType::LongPress) {
        return;
    }
    if (screen_ == ScreenId::Boot) {
        if (event.type == ButtonEventType::Click &&
            (event.id == ButtonId::Ok || event.id == ButtonId::Menu || event.id == ButtonId::Back)) {
            finishBoot();
        }
        return;
    }
    if (event.id == ButtonId::Menu && event.type == ButtonEventType::Click) {
        if (screen_ == ScreenId::Menu) {
            screen_ = ScreenId::Live;
        } else {
            openMenu();
        }
        return;
    }
    if (event.id == ButtonId::Back && event.type == ButtonEventType::Click) {
        if (screen_ == ScreenId::Menu || screen_ == ScreenId::Live) {
            screen_ = ScreenId::Live;
        } else {
            openMenu();
        }
        return;
    }
    const bool step = event.type == ButtonEventType::Click || event.type == ButtonEventType::Repeat;
    if (screen_ == ScreenId::Menu && step &&
        (event.id == ButtonId::Up || event.id == ButtonId::Down)) {
        if (event.id == ButtonId::Up && menuIndex_ > 0) {
            --menuIndex_;
        }
        if (event.id == ButtonId::Down && menuIndex_ + 1 < kMenuCount) {
            ++menuIndex_;
        }
        return;
    }
    if (screen_ == ScreenId::Menu && event.id == ButtonId::Ok && event.type == ButtonEventType::Click) {
        screen_ = kMenuEntries[menuIndex_].screen;
    }
}

}  // namespace bluecore
