#include "game/player/player_frame_finalizer.h"
#include "compatibility/player_behavior_bridge.h"

namespace spidey {
namespace player {

void PlayerFrameFinalizer::updateCameraFacing(u8* input)
{
    CPlayer* pPlayer = &player_;
    u8* pPad = input;

    // --- camera facing, 0x4B6E8C ---
    // 0xAD6: "the camera may be auto-steered this tick".
    if (PLR_U8(pPlayer, 0xAD6) != 0
        && PLR_U8(pPlayer, 0x8EC) == 0
        && pPlayer->field_54C == 0)
    {
        if (pPlayer->field_8E8 != 0)
        {
            pPlayer->field_551 = 0;
            if ((pPlayer->mActionState & 0x3000) == 0)
            {
                pPlayer->PutCameraBehind(0x1E);
            }
        }
        // 0xE70: SHandle of the object the camera locks onto.
        else if (pPad[0x50] != 0 && PLR_I32(pPlayer, 0xE70) != 0)
        {
            CSVector aim;
            void *pLockTarget = Mem_RecoverPointer(
                reinterpret_cast<SHandle*>(reinterpret_cast<char*>(pPlayer) + 0xE70));

            Utils_CalcAim(&aim, &pPlayer->mPos,
                reinterpret_cast<CVector*>(reinterpret_cast<char*>(pLockTarget) + 8));
            G_CAMERA_LIST->SetCamAngle(aim.vy, 0);
            pPlayer->field_551 = 1;
        }
        else if (pPlayer->field_EA6 == 0)
        {
            pPlayer->field_551 = 0;

            // the original calls this and throws the result away
            pPlayer->GetEffectiveHeading();

            if (pPad[0x41] != 0)
            {
                pPad[0x41] = 0;
                pPlayer->field_201 = 0;
                pPlayer->PutCameraBehind(0);
                pPlayer->field_56C = 0;
            }
            else if (pPad[0x111] != 0
                && pPlayer->field_8EA == 0
                && (pPlayer->mActionState & 0x2001C001) != 0)
            {
                pPad[0x111] = 0;
                // 0x2D1: cleared with the camera reset, like field_201.
                PLR_U8(pPlayer, 0x2D1) = 0;
                pPlayer->PutCameraBehind(0);
            }
            else if ((u8)(pPlayer->field_E2E | pPlayer->field_E2D) != 0
                && pPlayer->mActionState == 0x10)
            {
                i16 camAngle = G_CAMERA_LIST->field_23A;
                i16 want = (i16)(pPlayer->field_E32 + camAngle);
                i32 delta = ((i32)camAngle - (i32)want) & 0xFFF;

                if (delta < 0x4B0 || delta > 0xB50)
                {
                    G_CAMERA_LIST->SetCamAngle(want, 0x3C);
                }
                else if (delta > 0x7F0 && delta < 0x810)
                {
                    G_CAMERA_LIST->SetCamAngle(G_CAMERA_LIST->field_236, 0);
                }
                else
                {
                    // maps 0x4B0..0xB50 onto a full 0..0x800 sweep
                    i32 t = ((delta - 0x4B0) << 11) / 0x6A0;
                    i32 pitch = ((i32)Sine(t & 0xFFF) * 128 >> 12) + 0x3C;

                    G_CAMERA_LIST->SetCamAngle(want, (u16)pitch);
                }
            }
            else
            {
                G_CAMERA_LIST->SetCamAngle(G_CAMERA_LIST->field_236, 0);
            }
        }
        else
        {
            if (pPad[0x41] != 0)
            {
                pPad[0x41] = 0;
                pPlayer->field_201 = 0;
                pPlayer->PutCameraBehind(0);
                pPlayer->field_56C = 0;
                pPlayer->field_551 = 1;
            }

            if (pPlayer->field_551 == 0)
            {
                pPlayer->PutCameraBehind(0x14);
                pPlayer->field_551 = 1;
            }
        }
    }
}

i32 PlayerFrameFinalizer::updateTorsoAim()
{
    CPlayer* pPlayer = &player_;
    u8 hasMovementInput;
    i32 torsoTurnSpeed;
    i32 cameraHeading;
    i32 state;

    // --- torso aim, 0x4B70D5 ---
    hasMovementInput = (u8)(pPlayer->field_E2E | pPlayer->field_E2D);
    torsoTurnSpeed = 0;
    if (hasMovementInput != 0)
    {
        i16 aim = pPlayer->field_E32;

        if (aim > 0x100 && aim < 0xF00)
        {
            torsoTurnSpeed = ((i32)aim - 0x100) / 512 + 2;
        }
        else
        {
            torsoTurnSpeed = 1;
        }
    }

    cameraHeading = (G_CAMERA_LIST != 0) ? (i32)G_CAMERA_LIST->field_23A : 0;
    state = pPlayer->mActionState;

    if ((state & 0x10) != 0)
    {
        if (pPlayer->field_8E8 != 0)
        {
            pPlayer->SetTargetTorsoAngle(pPlayer->field_E32, false);
        }
        else
        {
            pPlayer->SetTargetTorsoAngle((i16)(pPlayer->field_E32 + cameraHeading),
                pPlayer->mSynthesizedInputActive != 0);
        }
    }
    else if (state == 1)
    {
        if (hasMovementInput == 0)
        {
            pPlayer->LockTargetTorsoAngle();
        }
    }
    else if ((state & 0x4E) != 0)
    {
        if (pPlayer->field_AE6 != 0 || torsoTurnSpeed == 0)
        {
            pPlayer->LockTargetTorsoAngle();
        }
        else
        {
            pPlayer->SetTargetTorsoAngle((i16)(pPlayer->field_E32 + cameraHeading), false);
        }
    }
    return cameraHeading;
}

} // namespace player
} // namespace spidey
