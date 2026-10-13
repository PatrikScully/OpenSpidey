#pragma once
#include "core/types.h"

class CPlayer;

namespace spidey {
namespace player {

// Runs cutscene/demo player input bytecode and its concurrent commands.
// Borrows the player's original state; the player owns command lifetimes.
class ScriptedInputController {
public:
    explicit ScriptedInputController(CPlayer& player) : player_(player) {}
    void update();

private:
    void prepareInput();
    void executeDueInstructions();
    i32* createCommand(i32 type, u32 parameterCount);
    i32 updateCommands();
    void publishInput(i32 heldMask);
    u8* buttonState(i32 index);
    void finishIfIdle();
    void teleportToNode(i32 nodeId);
    CPlayer& player_;
};

} // namespace player
} // namespace spidey
