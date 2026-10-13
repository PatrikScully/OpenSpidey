#include "game/player/player_animation_controller.h"
#include "game/player/spidey.h"

namespace spidey {
namespace player {

void PlayerAnimationController::play(int animationId, int startFrame) {
    // Original initialized animation-to-SFX table. The data bridge remains
    // here until the animation library owns these records independently.
    i32** soundScripts = reinterpret_cast<i32**>(0x006A82B8);
    i32* soundScript = soundScripts[animationId];
    player_.field_350 = soundScript;
    if (soundScript) {
        for (i32* event = soundScript; *event != -1; ++event) {
            *event &= 0xFFFF;
        }
    }
    player_.RunAnim(animationId, startFrame, -1);
}

} // namespace player
} // namespace spidey
