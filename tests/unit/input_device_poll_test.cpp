#include "engine/input/input_devices.h"
#include "platform/plat.h"
#include <cassert>
#include <cstring>

namespace {
u8 keyboard[256] = {};
u8 mouse[3] = {};
u8 controller[32] = {};
bool controllerPresent = true;
}

void Plat_InputPollKeyboard(u8* states) { std::memcpy(states, keyboard, sizeof(keyboard)); }
void Plat_InputPollMouse(i32* x, i32* y, u8* buttons) {
    *x = 12;
    *y = -7;
    std::memcpy(buttons, mouse, sizeof(mouse));
}
i32 Plat_InputPollController(i32* x, i32* y, u32* pov, u8* buttons, i32* count) {
    if (!controllerPresent) return 0;
    *x = -8000;
    *y = 16000;
    *pov = 9000;
    *count = 4;
    std::memcpy(buttons, controller, sizeof(controller));
    return 1;
}
void Plat_InputRumble(i32, f32) {}

int main() {
    DXINPUT_Initialize(nullptr, 0);
    keyboard[57] = 0x80;
    assert(DXINPUT_PollKeyboard() == 1 && DXINPUT_GetKeyState(57) == 0xFF);
    assert(DXINPUT_PollKeyboard() == 1 && DXINPUT_GetKeyState(57) == 0x7F);
    assert(DXINPUT_PollKeyboard() == 0 && DXINPUT_GetKeyState(57) == 0x7F);
    keyboard[57] = 0;
    assert(DXINPUT_PollKeyboard() == 1 && DXINPUT_GetKeyState(57) == 0x80);
    assert(DXINPUT_PollKeyboard() == 1 && DXINPUT_GetKeyState(57) == 0);
    assert(DXINPUT_PollKeyboard() == 0);

    i32 x, y, pov;
    mouse[0] = 0x80;
    assert(DXINPUT_PollMouse(&x, &y) == 1 && x == 12 && y == -7);
    assert(DXINPUT_GetMouseButtonState(0) == 0xFF);
    DXINPUT_PollMouse(&x, &y);
    assert(DXINPUT_GetMouseButtonState(0) == 0x7F);
    mouse[0] = 0;
    DXINPUT_PollMouse(&x, &y);
    assert(DXINPUT_GetMouseButtonState(0) == 0x80);
    DXINPUT_PollMouse(&x, &y);
    assert(DXINPUT_GetMouseButtonState(0) == 0);

    controller[3] = 0x80;
    assert(DXINPUT_PollController(&x, &y, &pov) == 1);
    assert(x == -8000 && y == 16000 && pov == 9000);
    assert(DXINPUT_GetNumControllerButtons() == 4);
    assert(DXINPUT_GetControllerButtonState(3) == 0xFF);
    DXINPUT_PollController(&x, &y, &pov);
    assert(DXINPUT_GetControllerButtonState(3) == 0x7F);
    controller[3] = 0;
    DXINPUT_PollController(&x, &y, &pov);
    assert(DXINPUT_GetControllerButtonState(3) == 0x80);
    DXINPUT_PollController(&x, &y, &pov);
    assert(DXINPUT_GetControllerButtonState(3) == 0);
    controllerPresent = false;
    assert(DXINPUT_PollController(&x, &y, &pov) == 0);

    DXINPUT_SetKeyState(42, 0xFF);
    DXINPUT_SetMouseButtonState(1, 0xFF);
    DXINPUT_Initialize(nullptr, 0);
    assert(DXINPUT_GetKeyState(42) == 0 && DXINPUT_GetMouseButtonState(1) == 0);
    assert(DXINPUT_GetNumControllerButtons() == 0);
}
