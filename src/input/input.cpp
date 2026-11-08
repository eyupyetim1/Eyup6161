#include "bluecore/input.hpp"

namespace bluecore {

ButtonLogic::ButtonLogic() = default;

bool ButtonLogic::repeatable(ButtonId id) { return id == ButtonId::Up || id == ButtonId::Down; }

bool ButtonLogic::push(ButtonId id, ButtonEventType type, uint32_t nowMs) {
    if (count_ >= 16) {
        ++dropped_;
        return false;
    }
    queue_[tail_] = ButtonEvent{id, type, nowMs};
    tail_ = static_cast<uint8_t>((tail_ + 1) % 16);
    ++count_;
    return true;
}

void ButtonLogic::setRaw(ButtonId id, bool pressed, uint32_t nowMs) {
    const int index = static_cast<int>(id);
    if (index < 0 || index >= kButtonCount) {
        return;
    }
    Channel& channel = channels_[index];
    const uint8_t raw = pressed ? 1 : 0;
    if (channel.raw == raw) {
        return;
    }
    channel.raw = raw;
    channel.rawChangedMs = nowMs;
}

void ButtonLogic::poll(uint32_t nowMs) {
    for (int i = 0; i < kButtonCount; ++i) {
        Channel& channel = channels_[i];
        const auto id = static_cast<ButtonId>(i);
        if (channel.raw != channel.stable && (nowMs - channel.rawChangedMs) >= kDebounceMs) {
            channel.stable = channel.raw;
            if (channel.stable != 0) {
                channel.pressedMs = nowMs;
                channel.longFired = 0;
                channel.nextRepeatMs = 0;
                if (repeatable(id)) {
                    push(id, ButtonEventType::Click, nowMs);
                }
            } else if (channel.longFired == 0 && !repeatable(id)) {
                push(id, ButtonEventType::Click, nowMs);
            }
        }
        if (channel.stable != 0 && channel.longFired == 0 &&
            (nowMs - channel.pressedMs) >= kLongPressMs) {
            channel.longFired = 1;
            channel.nextRepeatMs = nowMs + kRepeatMs;
            push(id, ButtonEventType::LongPress, nowMs);
        }
        if (channel.stable != 0 && channel.longFired != 0 && repeatable(id) &&
            channel.nextRepeatMs != 0 && nowMs >= channel.nextRepeatMs) {
            push(id, ButtonEventType::Repeat, nowMs);
            channel.nextRepeatMs = nowMs + kRepeatMs;
        }
    }
}

bool ButtonLogic::next(ButtonEvent& out) {
    if (count_ == 0) {
        return false;
    }
    out = queue_[head_];
    head_ = static_cast<uint8_t>((head_ + 1) % 16);
    --count_;
    return true;
}

}  // namespace bluecore
