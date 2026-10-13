#include "engine/input/button_state.h"
#include <cassert>

int main() {
    using spidey::input::ButtonState;
    ButtonState button;
    assert(!button.isDown() && !button.wasPressed() && !button.wasReleased());
    button = button.advance(true);
    assert(button.wasPressed() && button.isDown());
    button = button.advance(true);
    assert(button.encoded() == ButtonState::Held && button.isDown() && !button.wasPressed());
    button = button.advance(false);
    assert(button.wasReleased() && !button.isDown());
    button = button.advance(false);
    assert(button.encoded() == ButtonState::Up && !button.wasReleased());
    button = button.advance(true);
    assert(button.wasPressed());

    // Some scripts supply raw states with fewer held bits. Preserve the
    // original rule across all 256 values, including those scripted states.
    for (int encoded = 0; encoded < 256; ++encoded) {
        const ButtonState previous(static_cast<u8>(encoded));
        const bool previouslyDown = (encoded & 0x7F) != 0;
        assert(previous.advance(true).encoded() == (previouslyDown ? 0x7F : 0xFF));
        assert(previous.advance(false).encoded() == (previouslyDown ? 0x80 : 0x00));
    }
}
