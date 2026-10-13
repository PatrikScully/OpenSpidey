#include "game/application/input_update_trace.h"
#include "engine/input/ps2pad.h"
#include "engine/world/ob.h"
#include <cstdio>

namespace spidey {
namespace application {

void InputUpdateTrace::beginUpdate(const CBody&) {
    upPressedBeforeUpdate_ = G_SCONTROL[0].Up.Pressed != 0;
}

void InputUpdateTrace::afterStage(const CBody& actor, world::ActorUpdateStage stage) {
    if (!upPressedBeforeUpdate_ || G_SCONTROL[0].Up.Pressed) return;
    switch (stage) {
    case world::ActorUpdateStage::AnimatedTiming:
        std::printf("PADCLR in EveryFrame type=%d\n", static_cast<i32>(actor.mType));
        break;
    case world::ActorUpdateStage::Animation:
        std::printf("PADCLR in UpdateFrame type=%d\n", static_cast<i32>(actor.mType));
        break;
    case world::ActorUpdateStage::Behavior:
        std::printf("PADCLR by type=%d flags=%#x\n",
                    static_cast<i32>(actor.mType), static_cast<u32>(actor.mFlags));
        break;
    }
}

} // namespace application
} // namespace spidey
