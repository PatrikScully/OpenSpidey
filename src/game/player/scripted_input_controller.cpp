#include "game/cinematics/script_command_list.h"
#include "game/player/scripted_input_controller.h"
#include "game/player/spidey.h"
#include "game/player/player_animation_controller.h"
#include "game/cinematics/camera.h"
#include "game/application/SpideyDX.h"
#include "engine/world/trig.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/audio/ps2redbook.h"
#include "core/utils.h"
#include "compatibility/my_assert.h"
#include "compatibility/slot_zero_deletable.h"

namespace {
// Original initialized fixed-point heading table, interleaved cosine/sine.
const i16* const headingCosine = reinterpret_cast<i16*>(0x610C4A);
const i16* const headingSine = reinterpret_cast<i16*>(0x610C48);
// The scratch value is reset by the original VM; its external use is unknown.
i32* const gPlayerSynthTickScratch = reinterpret_cast<i32*>(0x6B4CA4);
i32* const gPshellForceLevelExit = reinterpret_cast<i32*>(0x68293C);
u8* const gSynthInputScriptFlag = reinterpret_cast<u8*>(0x60F770);
u8* const gSubmarinerDieRelated = reinterpret_cast<u8*>(0x0060CFC4);

// Only the first three bytes of each original 16-byte button record are
// synthesized here; the rest retain their original storage and ownership.
enum { RegisterCount = 20, RegisterStride = 16, RegisterBankOffset = 0x1C0 };

enum ScriptOpcode {
    Teleport = 1,
    WalkToTarget = 2,
    HoldInput = 3,
    PlayVoice = 4,
    MoveToTarget = 5,
    HoldAnimation = 6,
    StandAfterDelay = 7,
    HoldPlayerValue = 8,
    Wait = 9,
    WaitForAnimationSignal = 15,
    SetCheckpoint = 16,
    HoldInteraction = 17,
    SetScriptFlag = 18,
    Stop = 255,
};
} // namespace

namespace spidey {
namespace player {
using cinematics::ScriptCommandView;

void ScriptedInputController::update() {
    prepareInput();
    executeDueInstructions();
    i32 heldMask = updateCommands();
    publishInput(heldMask);
    finishIfIdle();
}

void ScriptedInputController::prepareInput() {
    *gPlayerSynthTickScratch = 0;
    player_.mScriptElapsedTicks += player_.mElapsedTicks;

    u8 *pad = reinterpret_cast<u8*>(player_.field_E0C);
    player_.field_EA6 = 0;

    static const i32 clearedSlots[16] = {0,1,2,3,4,5,6,7,8,9,10,11,16,17,18,19};
    for (i32 i = 0; i < 16; i++)
    {
        *reinterpret_cast<i16*>(pad + RegisterStride * clearedSlots[i]) = 0;
    }

    if (*gSubmarinerDieRelated)
    {
        player_.mScriptBytecodeActive = 0;
        player_.KillAllCommandBlocks();
    }

}

i32* ScriptedInputController::createCommand(i32 type, u32 parameterCount) {
    cinematics::ScriptCommandList commands(player_.mScriptCommandHead,
        cinematics::scriptCommandAllocator());
    return commands.create(type, parameterCount);
}

void ScriptedInputController::executeDueInstructions() {
    if (!player_.mScriptBytecodeActive)
        return;

    do
    {
        i16* stream = reinterpret_cast<i16*>(player_.mScriptCursorAddress);
        i16 dueTime = stream[0];
        if (dueTime != -1 && dueTime > player_.mScriptElapsedTicks)
            break;

        player_.mScriptElapsedTicks = 0;
        stream++;
        i16 opcode = stream[0];
        stream++;
        player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

        switch (opcode)
        {
            case Teleport:
            {
                i32 trigId = stream[0];
                stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                teleportToNode(trigId);
                continue;
            }

            case WalkToTarget:
            {
                i32 trigId = stream[0];
                stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                CVector target;
                target.vx = 0; target.vy = 0; target.vz = 0;
                Trig_GetPosition(&target, trigId);

                ScriptCommandView command(createCommand(WalkToTarget, 2));
                command.parameter(0) = target.vx;
                command.parameter(1) = target.vz;

                player_.mScriptBytecodeActive = 0;
                continue;
            }

            case HoldInput:
            {
                i32 index = stream[0];
                stream++;
                i32 duration = stream[0];
                stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                ScriptCommandView command(createCommand(HoldInput, 2));
                command.parameter(0) = index;
                command.parameter(1) = duration + player_.mElapsedTicks;
                continue;
            }

            case PlayVoice:
            {
                i16 p1 = stream[0]; stream++;
                i16 p2 = stream[0]; stream++;
                i16 p3 = stream[0]; stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                Redbook_XAPlay(p1, p2, p3);
                continue;
            }

            case MoveToTarget:
            {
                i32 trigId = stream[0]; stream++;
                i32 duration = stream[0]; stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                CVector target;
                target.vx = 0; target.vy = 0; target.vz = 0;
                Trig_GetPosition(&target, trigId);

                ScriptCommandView command(createCommand(MoveToTarget, 4));
                command.parameter(0) = target.vx;
                command.parameter(1) = target.vy;
                command.parameter(2) = target.vz;
                command.parameter(3) = duration;

                player_.mScriptBytecodeActive = 0;
                continue;
            }

            case HoldAnimation:
            {
                i32 animId = stream[0]; stream++;
                i32 duration = stream[0]; stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                ScriptCommandView command(createCommand(HoldAnimation, 2));
                command.parameter(0) = animId;
                command.parameter(1) = duration + player_.mElapsedTicks;
                continue;
            }

            case StandAfterDelay:
            {
                i32 duration = stream[0];
                stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                ScriptCommandView command(createCommand(StandAfterDelay, 1));
                command.parameter(0) = duration + player_.mElapsedTicks;
                continue;
            }

            case HoldPlayerValue:
            {
                i32 value = stream[0]; stream++;
                i32 duration = stream[0]; stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                ScriptCommandView command(createCommand(HoldPlayerValue, 2));
                command.parameter(0) = value;
                command.parameter(1) = duration + player_.mElapsedTicks;
                continue;
            }

            case Wait:
            {
                i32 duration = stream[0];
                stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                ScriptCommandView command(createCommand(Wait, 1));
                command.parameter(0) = duration + player_.mElapsedTicks;

                player_.mScriptBytecodeActive = 0;
                continue;
            }

            case WaitForAnimationSignal:
            {
                i32 animId = stream[0];
                stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                ScriptCommandView command(createCommand(WaitForAnimationSignal, 0));

                player_.mScriptBytecodeActive = 0;
                if (player_.mAnim != animId || player_.mAnimFinished)
                    player_.RunAnim(animId, 0, -1);
                continue;
            }

            case SetCheckpoint:
            {
                player_.mScriptCheckpointNode = stream[0];
                stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);
                continue;
            }

            case HoldInteraction:
            {
                if (player_.mIsCrawling)
                {
                    ScriptCommandView command(createCommand(HoldInput, 2));
                    command.parameter(0) = 16;
                    command.parameter(1) = player_.mElapsedTicks;
                }
                continue;
            }

            case SetScriptFlag:
            {
                i32 param = stream[0];
                stream++;
                player_.mScriptCursorAddress = reinterpret_cast<i32>(stream);

                *gSynthInputScriptFlag = (param != 0);
                continue;
            }

            case Stop:
                player_.mScriptBytecodeActive = 0;
                continue;

            default:
                continue;
        }
    }
    while (player_.mScriptBytecodeActive);
}

i32 ScriptedInputController::updateCommands() {
    i32 heldMask = 0;
    i32* block = player_.mScriptCommandHead;
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

                if (Utils_XZDist(&target, &player_.mPos) < 64)
                {
                    block = player_.KillCommandBlock(block);
                    player_.mScriptBytecodeActive = 1;
                    break;
                }

                i32 dz = (command.parameter(1) - player_.mPos.vz) >> 12;
                i32 dx = (command.parameter(0) - player_.mPos.vx) >> 12;
                i32 idx = 2 * ((1024 - ratan2(dz, dx) - G_CAMERA_LIST->field_23A) & 0xFFF);

                i32 x = headingCosine[idx] / 32;
                if (x > 127) x = 127;
                else if (x < -127) x = -127;
                player_.field_E2D = static_cast<char>(x);

                i32 y = headingSine[idx] / -32;
                if (y > 127) y = 127;
                else if (y < -127) y = -127;
                player_.field_E2E = static_cast<char>(y);

                block = command.next();
                break;
            }

            case HoldInput:
            {
                i32 index = command.parameter(0);
                i32 remaining = command.parameter(1) - player_.mElapsedTicks;

                if (remaining < 0)
                {
                    block = player_.KillCommandBlock(block);
                    break;
                }

                command.parameter(1) = remaining;
                heldMask |= 1 << index;

                switch (index)
                {
                    case 0: case 1: case 2: case 3:
                    case 4: case 5: case 6: case 7:
                    case 16: case 17: case 18: case 19:
                    {
                        u8* slot = buttonState(index);
                        if (slot[0] == 0)
                            slot[1] = 1;
                        slot[0] = 1;
                        block = command.next();
                        break;
                    }

                    case HoldPlayerValue:
                        block = command.next();
                        buttonState(8)[0] = 1;
                        player_.field_E2E = -127;
                        break;

                    case Wait:
                        block = command.next();
                        buttonState(9)[0] = 1;
                        player_.field_E2E = 127;
                        break;

                    case 10:
                        block = command.next();
                        buttonState(10)[0] = 1;
                        player_.field_E2D = -127;
                        break;

                    case 11:
                        block = command.next();
                        buttonState(11)[0] = 1;
                        player_.field_E2D = 127;
                        break;

                    default:
                        print_if_false(0, "Bad register index");
                        block = command.next();
                        break;
                }
                break;
            }

            case MoveToTarget:
            {
                CVector target;
                target.vx = command.parameter(0);
                target.vy = command.parameter(1);
                target.vz = command.parameter(2);

                if (Utils_Dist(target, player_.mPos) < 64)
                {
                    block = player_.KillCommandBlock(block);
                    player_.mScriptBytecodeActive = 1;
                    break;
                }

                CVector delta = target;
                delta -= player_.mPos;
                delta >>= 12;
                VectorNormal(
                        reinterpret_cast<VECTOR*>(&delta),
                        reinterpret_cast<VECTOR*>(&player_.mVel));

                block = command.next();
                break;
            }

            case HoldAnimation:
            {
                i32 animId = command.parameter(0);
                i32 remaining = command.parameter(1) - player_.mElapsedTicks;

                if (remaining < 0)
                {
                    block = player_.KillCommandBlock(block);
                    break;
                }

                command.parameter(1) = remaining;

                if (player_.mAnimFinished || player_.mAnim != animId)
                {
                    PlayerAnimationController(player_).play(animId);
                }

                block = command.next();
                break;
            }

            case StandAfterDelay:
            {
                i32 remaining = command.parameter(0) - player_.mElapsedTicks;

                if (remaining < 0)
                {
                    block = player_.KillCommandBlock(block);
                    player_.SwitchToStandMode();
                }
                else
                {
                    command.parameter(0) = remaining;
                    player_.mActionState = 0x40000000;
                    block = command.next();
                }
                break;
            }

            case HoldPlayerValue:
            {
                i32 remaining = command.parameter(1) - player_.mElapsedTicks;

                if (remaining < 0)
                {
                    block = player_.KillCommandBlock(block);
                    player_.field_E00 = 0;
                }
                else
                {
                    i32 value = command.parameter(0);
                    command.parameter(1) = remaining;
                    block = command.next();
                    player_.field_E00 = value;
                }
                break;
            }

            case Wait:
            {
                i32 remaining = command.parameter(0) - player_.mElapsedTicks;

                if (remaining < 0)
                {
                    block = player_.KillCommandBlock(block);
                    player_.mScriptBytecodeActive = 1;
                }
                else
                {
                    command.parameter(0) = remaining;
                    block = command.next();
                }
                break;
            }

            case WaitForAnimationSignal:
            {
                if (player_.mInputFlags & 1)
                {
                    player_.mInputFlags &= ~1;
                    block = player_.KillCommandBlock(block);
                    player_.mScriptBytecodeActive = 1;
                }
                else
                {
                    if (player_.mAnimFinished)
                        player_.RunAnim(player_.mAnim, 0, -1);

                    block = command.next();
                }
                break;
            }

            default:
                print_if_false(0, "Bad command");
                break;
        }
    }

    return heldMask;
}

u8* ScriptedInputController::buttonState(i32 index) {
    return reinterpret_cast<u8*>(&player_) + RegisterBankOffset + RegisterStride * index;
}

void ScriptedInputController::publishInput(i32 heldMask) {
    u8* controlStates = reinterpret_cast<u8*>(player_.field_E0C);
    for (i32 index = 0; index < RegisterCount; ++index) {
        // Start/Select remain under the physical control system.
        if (index == 14 || index == 15)
            continue;

        u8* button = buttonState(index);
        u8* control = controlStates + RegisterStride * index;
        if (!(heldMask & (1 << index)))
            button[0] = 0;
        if (button[0] == 0) {
            button[1] = 0;
            button[2] = 0;
        }
        control[0] = button[0];
        control[1] = button[1];
        control[2] = button[2];
    }
}

void ScriptedInputController::finishIfIdle() {
    if (player_.mScriptCommandHead || player_.mScriptBytecodeActive)
        return;

    // 31900664/34009330/4915200 are plain fixed-point world-space
    // constants (IDA mislabeled them unk_1E3CBF8/unk_20532F2/
    // loc_4B0000 because they happen to look like valid addresses).
    if (Trig_GetLevelId() != 1795 || !G_MECHLIST
            || my_abs(31900664 + G_MECHLIST->mPos.vx) >= 4915200
            || my_abs(G_MECHLIST->mPos.vz - 34009330) >= 4915200)
    {
        player_.mSynthesizedInputActive = 0;
    }

    if (*gPshellForceLevelExit)
        gLevelStatus = 7;

    if (*gSubmarinerDieRelated && player_.mScriptCheckpointNode)
    {
        Trig_GetPosition(&player_.mPos, player_.mScriptCheckpointNode);
    }
}

void ScriptedInputController::teleportToNode(i32 nodeId) {
    Trig_GetPosition(&player_.mPos, nodeId);
    player_.field_E8 = player_.mPos;

    if (player_.field_54C)
    {
        player_.field_54C = 0;
        G_CAMERA_LIST->field_12C = -1;
    }

    if (player_.field_E64)
    {
        delete reinterpret_cast<SVTableSlot0Deletable*>(player_.field_E64);
    }
    player_.field_E64 = 0;

    if (player_.field_E6C)
    {
        delete reinterpret_cast<SVTableSlot0Deletable*>(player_.field_E6C);
    }
    player_.field_E6C = 0;

    if (player_.field_5E4)
    {
        SFX_Stop(player_.field_5E4);
        player_.field_5E4 = 0;
    }

    i32 normalY = player_.field_A8.vy;
    if (normalY > 3400)
    {
        player_.mOrientationReferenceDirection = player_.mForwardDirection;
        player_.field_A8.vx = 0;
        player_.field_A8.vy = -4096;
        player_.field_A8.vz = 0;
        player_.OrientToNormal(true, &player_.mOrientationReferenceDirection);
    }
    else if (normalY >= -2600)
    {
        player_.mOrientationReferenceDirection = player_.mUpDirection;
        player_.field_A8.vx = 0;
        player_.field_A8.vy = -4096;
        player_.field_A8.vz = 0;
        player_.OrientToNormal(true, &player_.mOrientationReferenceDirection);
    }
    else
    {
        player_.OrientToNormal(false, &ZeroVector);
    }

    player_.mIsCrawling = 0;

    player_.mVel.vx = 0;
    player_.mVel.vy = 0;
    player_.mVel.vz = 0;

    i32 groundHeight = Utils_GetGroundHeight(&player_.mPos, 0, 256, 0);
    if (groundHeight == -1)
    {
        player_.mActionState = 4;
    }
    else
    {
        player_.mActionState = 1;
        player_.mPos.vy = groundHeight - (player_.mSurfaceClearance << 12);
    }
}


} // namespace player
} // namespace spidey
