#pragma once

#include "core/types.h"

namespace spidey {
namespace input {

// Value object for the game's buffered input encoding. A hardware snapshot
// reports only down/up; advance() restores the press and release edges.
class ButtonState {
public:
    enum Encoding { Up = 0x00, Held = 0x7F, Released = 0x80, Pressed = 0xFF };

    explicit ButtonState(u8 encoded = Up) : encoded_(encoded) {}
    bool isDown() const { return (encoded_ & 0x7F) != 0; }
    bool wasPressed() const { return encoded_ == Pressed; }
    bool wasReleased() const { return encoded_ == Released; }
    u8 encoded() const { return encoded_; }

    ButtonState advance(bool down) const {
        if (down) return ButtonState(isDown() ? Held : Pressed);
        return ButtonState(isDown() ? Released : Up);
    }

private:
    u8 encoded_;
};

} // namespace input
} // namespace spidey
