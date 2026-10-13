#include "game/cinematics/actor_script_controller.h"
#include "game/actors/baddy.h"
#include "engine/world/trig.h"
#include "engine/audio/ps2redbook.h"
#include "engine/world/collision.h"
#include "core/utils.h"
#include "compatibility/my_assert.h"

namespace {
u8* const gSubmarinerDieRelated = reinterpret_cast<u8*>(0x0060CFC4);
enum ActorScriptOpcode {
    Teleport = 1,
    WalkToTarget = 2,
    PlayVoice = 4,
    MoveToTarget = 5,
    HoldAnimation = 6,
    LookAtNode = 8,
    WaitForAnimationSignal = 15,
    SetCheckpoint = 16,
    Stop = 255,
};
} // namespace

namespace spidey {
namespace cinematics {

void ActorScriptController::update() {
    prepareFrame();
    executeDueInstructions();
    updateCommands();
    finishIfIdle();
}

bool ActorScriptController::consumeAnimationSignal() {
    if (profile_.signalSource == ActorScriptProfile::BodyFlags) {
        if (!(actor_.mCBodyFlags & 1)) return false;
        actor_.mCBodyFlags &= ~1;
    } else {
        if (!(actor_.mInputFlags & 1)) return false;
        actor_.mInputFlags &= ~1;
    }
    return true;
}

void ActorScriptController::prepareFrame() {
    state_.elapsedTicks += actor_.mElapsedTicks;
    if (!state_.checkpointNode || !*gSubmarinerDieRelated)
        return;

    state_.decodingActive = 0;
    commands_.clear();
    actor_.mVel.vx = 0;
    actor_.mVel.vy = 0;
    actor_.mVel.vz = 0;
    if (!*state_.checkpointNode)
        return;

    Trig_GetPosition(&actor_.mPos, *state_.checkpointNode);
    u16* links = Trig_GetLinksPointer(*state_.checkpointNode);
    if (links[0] == 0)
        return;

    CVector target;
    target.vx = target.vy = target.vz = 0;
    i32 nextLink = static_cast<u16>(links[1]);
    Trig_GetPosition(&target, nextLink);
    target.vy = actor_.mPos.vy;
    Utils_CalcAim(&actor_.mAngles, &actor_.mPos, &target);
}

void ActorScriptController::executeDueInstructions() {
    while (state_.decodingActive)
    {
        i16* stream = reinterpret_cast<i16*>(state_.cursorAddress);
        i16 dueTime = stream[0];
        if (dueTime != -1 && dueTime > state_.elapsedTicks)
            break;

        state_.elapsedTicks = 0;
        stream++;
        i16 opcode = stream[0];
        stream++;
        state_.cursorAddress = reinterpret_cast<i32>(stream);

        switch (opcode)
        {
            case Teleport:
            {
                i16 trigId = stream[0];
                stream++;
                state_.cursorAddress = reinterpret_cast<i32>(stream);

                CVector target;
                target.vx = 0; target.vy = 0; target.vz = 0;
                Trig_GetPosition(&target, trigId);

                i32 groundHeight = Utils_GetGroundHeight(&target, 0, 0x800, 0);
                i32 height = actor_.field_21E << 12;

                actor_.mPos.vx = target.vx;
                actor_.mPos.vz = target.vz;
                actor_.mPos.vy = groundHeight - height;
                continue;
            }

            case WalkToTarget:
            {
                commands_.removeByType(HoldAnimation);

                i16 trigId = stream[0];
                stream++;
                state_.cursorAddress = reinterpret_cast<i32>(stream);

                CVector trigPos;
                trigPos.vx = 0; trigPos.vy = 0; trigPos.vz = 0;
                Trig_GetPosition(&trigPos, trigId);

                ScriptCommandView command(commands_.create(WalkToTarget, 2));
                command.parameter(0) = trigPos.vx;
                command.parameter(1) = trigPos.vz;

                state_.decodingActive = 0;
                actor_.RunAnim(profile_.walkStartAnimation, 0, -1);
                continue;
            }

            case PlayVoice:
            {
                i16 p1 = stream[0]; stream++;
                i16 p2 = stream[0]; stream++;
                i16 p3 = stream[0]; stream++;
                state_.cursorAddress = reinterpret_cast<i32>(stream);

                Redbook_XAPlay(p1, p2, p3);
                continue;
            }

            case MoveToTarget:
            {
                i16 trigId = stream[0]; stream++;
                i16 duration = stream[0]; stream++;
                state_.cursorAddress = reinterpret_cast<i32>(stream);

                CVector target;
                target.vx = 0; target.vy = 0; target.vz = 0;
                Trig_GetPosition(&target, trigId);

                ScriptCommandView command(commands_.create(MoveToTarget, 4));
                command.parameter(0) = target.vx;
                command.parameter(1) = target.vy;
                command.parameter(2) = target.vz;
                command.parameter(3) = duration;

                state_.decodingActive = 0;
                continue;
            }

            case HoldAnimation:
            {
                i16 animationId = stream[0]; stream++;
                i16 duration = stream[0]; stream++;
                state_.cursorAddress = reinterpret_cast<i32>(stream);

                ScriptCommandView command(commands_.create(HoldAnimation, 2));
                command.parameter(0) = animationId;
                command.parameter(1) = duration + actor_.mElapsedTicks;
                continue;
            }

            case LookAtNode:
            {
                i16 nodeId = stream[0]; stream++;
                i16 duration = stream[0]; stream++;
                state_.cursorAddress = reinterpret_cast<i32>(stream);

                ScriptCommandView command(commands_.create(LookAtNode, 2));
                command.parameter(0) = nodeId;
                command.parameter(1) = duration + actor_.mElapsedTicks;
                continue;
            }

            case WaitForAnimationSignal:
            {
                i16 animId = stream[0];
                stream++;
                state_.cursorAddress = reinterpret_cast<i32>(stream);

                commands_.create(WaitForAnimationSignal, 0);

                state_.decodingActive = 0;
                if (actor_.mAnim != animId)
                    actor_.RunAnim(animId, 0, -1);
                continue;
            }

            case SetCheckpoint:
                if (state_.checkpointNode) {
                    *state_.checkpointNode = stream[0];
                    stream++;
                    state_.cursorAddress = reinterpret_cast<i32>(stream);
                }
                continue;

            case Stop:
                state_.decodingActive = 0;
                continue;

            default:
                continue;
        }
    }


}

void ActorScriptController::updateCommands() {
    i32* block = state_.commandHead;
    while (block)
    {
        ScriptCommandView command(block);
        switch (command.type())
        {
            case WalkToTarget:
            {
                CVector target;
                target.vx = command.parameter(0);
                target.vy = 0;
                target.vz = command.parameter(1);

                if (Utils_XZDist(&target, &actor_.mPos) >= 64)
                {
                    CVector flatPos;
                    flatPos.vx = actor_.mPos.vx;
                    flatPos.vy = 0;
                    flatPos.vz = actor_.mPos.vz;

                    CSVector dir;
                    Utils_CalcAim(&dir, &flatPos, &target);
                    Utils_GetVecFromMagDir(&actor_.mVel, profile_.walkSpeed, &dir);

                    Utils_TurnTowards(actor_.mAngles, &actor_.mAngVel, &actor_.mAngAcc, dir, 8);

                    if (actor_.mAnim == profile_.walkStartAnimation && actor_.mAnimFinished)
                        actor_.CycleAnim(profile_.walkLoopAnimation, 1);

                    block = command.next();
                }
                else
                {
                    block = commands_.remove(block);

                    actor_.mVel.vx = 0;
                    actor_.mVel.vy = 0;
                    actor_.mVel.vz = 0;

                    actor_.mAngVel.vx = 0;
                    actor_.mAngVel.vy = 0;
                    actor_.mAngVel.vz = 0;
                    actor_.mAngAcc.vx = 0;
                    actor_.mAngAcc.vy = 0;
                    actor_.mAngAcc.vz = 0;

                    state_.decodingActive = 1;
                    actor_.RunAnim(profile_.walkEndAnimation, 0, -1);
                }
                break;
            }

            case MoveToTarget:
            {
                CVector target;
                target.vx = command.parameter(0);
                target.vy = command.parameter(1);
                target.vz = command.parameter(2);

                if (Utils_Dist(target, actor_.mPos) < 64)
                {
                    block = commands_.remove(block);
                    state_.decodingActive = 1;
                    break;
                }

                CVector delta = target;
                delta -= actor_.mPos;
                delta >>= 12;
                VectorNormal(reinterpret_cast<VECTOR*>(&delta), reinterpret_cast<VECTOR*>(&actor_.mVel));
                // 0x414948: the unit vector is scaled by the block's speed
                if (profile_.scaleMoveVelocity)
                    actor_.mVel *= command.parameter(3);

                block = command.next();
                break;
            }

            case HoldAnimation:
            {
                i32 animId = command.parameter(0);
                i32 remaining = command.parameter(1) - actor_.mElapsedTicks;

                if (remaining < 0)
                {
                    block = commands_.remove(block);
                }
                else
                {
                    command.parameter(1) = remaining;
                    if (actor_.mAnimFinished || actor_.mAnim != animId)
                        actor_.RunAnim(animId, 0, -1);

                    block = command.next();
                }
                break;
            }

            case LookAtNode:
            {
                i32 remaining = command.parameter(1) - actor_.mElapsedTicks;

                if (remaining < 0)
                {
                    block = commands_.remove(block);
                    state_.lookTargetNode = 0;
                }
                else
                {
                    actor_.mFlags |= 4;
                    command.parameter(1) = remaining;
                    state_.lookTargetNode = command.parameter(0);

                    block = command.next();
                }
                break;
            }

            case WaitForAnimationSignal:
            {
                if (consumeAnimationSignal())
                {
                    block = commands_.remove(block);
                    state_.decodingActive = 1;
                }
                else
                {
                    if (actor_.mAnimFinished)
                        actor_.RunAnim(actor_.mAnim, 0, -1);

                    block = command.next();
                }
                break;
            }

            default:
                print_if_false(0, "Bad command");
                break;
        }
    }


}

void ActorScriptController::finishIfIdle() {
    if (state_.commandHead || state_.decodingActive)
        return;
    state_.running = 0;
    if (state_.checkpointNode && *gSubmarinerDieRelated && *state_.checkpointNode)
        Trig_GetPosition(&actor_.mPos, *state_.checkpointNode);
}

} // namespace cinematics
} // namespace spidey
