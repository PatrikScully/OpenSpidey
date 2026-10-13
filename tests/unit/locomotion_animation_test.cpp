#include "game/player/locomotion_animation_policy.h"
#include "game/player/player_action.h"
#include <cassert>
#include <limits>
using namespace spidey::player;
int main() {
    assert(actionWord(PlayerAction::Falling)==4);
    assert(actionWord(PlayerAction::SwitchPull)==std::numeric_limits<i32>::min());
    auto plan=LocomotionAnimationPolicy::falling(0xD4,-1,-1,false);
    assert(!plan.resetReference && plan.nextAnimation==0xFFFF && !plan.clearWebRelease);
    plan=LocomotionAnimationPolicy::falling(0xD4,-1,0,false);
    assert(plan.resetReference && plan.nextAnimation==0xD4);
    plan=LocomotionAnimationPolicy::falling(0xD4,1,0,true);
    assert(!plan.resetReference && !plan.clearWebRelease);
    plan=LocomotionAnimationPolicy::falling(0xD4,0,1,true);
    assert(plan.resetReference && plan.nextAnimation==0x114 && plan.clearWebRelease);
    plan=LocomotionAnimationPolicy::falling(0x118,1,1,true);
    assert(plan.nextAnimation==0x116 && plan.clearWebRelease);
    plan=LocomotionAnimationPolicy::falling(0xAF,-1,1,true);
    assert(plan.resetReference && plan.nextAnimation==0xFFFF && !plan.clearWebRelease);
    plan=LocomotionAnimationPolicy::falling(0xE8,-1,1,false);
    assert(plan.resetReference && plan.nextAnimation==0xFFFF);
    plan=LocomotionAnimationPolicy::falling(0xFFFF,0,0,false);
    assert(plan.resetReference && plan.nextAnimation==0xD4);
    plan=LocomotionAnimationPolicy::falling(0xD7,std::numeric_limits<i32>::min(),
        std::numeric_limits<i32>::max(),false);
    assert(plan.resetReference && plan.nextAnimation==0xD8);
    assert(LocomotionAnimationPolicy::reverseMovement(0x15)==0x19);
    assert(LocomotionAnimationPolicy::reverseMovement(0x34)==0x5B);
    assert(LocomotionAnimationPolicy::reverseMovement(0x32)==0x59);
    assert(LocomotionAnimationPolicy::reverseMovement(0x33)==0x59);
    assert(LocomotionAnimationPolicy::reverseMovement(0x59)==0xFFFF);
}
