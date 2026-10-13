#pragma once

#include "game/cinematics/script_command_list.h"

class CBaddy;

namespace spidey {
namespace cinematics {

// References into the original actor layout. Checkpoints are optional: the
// clone's field at the corresponding offset is gravity, not a checkpoint.
struct ActorScriptState {
    i32& elapsedTicks;
    u8& decodingActive;
    i32& cursorAddress;
    i32*& commandHead;
    u8& running;
    i32& lookTargetNode;
    i32* checkpointNode;
};

struct ActorScriptProfile {
    enum SignalSource { BodyFlags, InputFlags };
    i32 walkSpeed;
    i32 walkStartAnimation;
    i32 walkLoopAnimation;
    i32 walkEndAnimation;
    bool scaleMoveVelocity;
    SignalSource signalSource;
    static ActorScriptProfile blackCat() { return {3, 2, 3, 4, true, BodyFlags}; }
    static ActorScriptProfile spiderClone() { return {8, 1, 21, 13, false, InputFlags}; }
};

// Shared actor bytecode/timed-command behavior; state and allocation are borrowed.
class ActorScriptController {
public:
    ActorScriptController(CBaddy& actor, ActorScriptState state,
        ActorScriptProfile profile, core::MemoryAllocator& allocator)
        : actor_(actor), state_(state), profile_(profile),
          commands_(state_.commandHead, allocator) {}
    void update();

private:
    void prepareFrame();
    void executeDueInstructions();
    void updateCommands();
    void finishIfIdle();
    bool consumeAnimationSignal();
    CBaddy& actor_;
    ActorScriptState state_;
    ActorScriptProfile profile_;
    ScriptCommandList commands_;
};

} // namespace cinematics
} // namespace spidey
