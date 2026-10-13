#include "game/player/player_behavior_controller.h"
#include "game/player/player_action.h"
#include "compatibility/player_behavior_bridge.h"

namespace spidey {
namespace player {

void PlayerBehaviorController::updateScriptedStandCompletion()
{
    CPlayer* pPlayer = &player_;
    {
        CVector hook;
        CVector up;

        pPlayer->field_EA6 = 0;
        if (pPlayer->mAnimFinished == 0) return;

        pPlayer->mIsCrawling = 1;

        hook.vx = 0;
        hook.vy = 0;
        hook.vz = 0;
        M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hook), pPlayer, 2);
        pPlayer->mPos = hook;

        pPlayer->mOrientationReferenceDirection.vx = 0;
        pPlayer->mOrientationReferenceDirection.vy = 0x1000;
        pPlayer->mOrientationReferenceDirection.vz = 0;

        // 0xB84: the CSVector angles the scripted move ends on.
        pPlayer->field_A8.vx = PLR_I16(pPlayer, 0xB84);
        pPlayer->field_A8.vy = PLR_I16(pPlayer, 0xB86);
        pPlayer->field_A8.vz = PLR_I16(pPlayer, 0xB88);

        up.vx = 0;
        up.vy = 0x1000;
        up.vz = 0;
        pPlayer->OrientToNormal(true, &up);
        pPlayer->LockTargetTorsoAngle();
        pPlayer->SwitchToStandMode();
        return;
    }
}

void PlayerBehaviorController::updatePickupReach()
{
    CPlayer* pPlayer = &player_;
    {
        u16 anim;
        CManipOb *pObject;

        pPlayer->field_EA6 = 0;

        if (pPlayer->mAnimFinished != 0)
        {
            pPlayer->SwitchToStandMode();
            return;
        }

        if (pPlayer->mHeldObject != 0) return;

        anim = pPlayer->mAnim;
        if (anim == 0)
        {
            // 0x4B6597
            pPlayer->SwitchToStandMode();
            return;
        }

        if (!((anim == 0xC4 && pPlayer->mFrame >= 0xA)
            || (anim == 0xBE && pPlayer->mFrame >= 0xA)))
        {
            return;
        }

        // 0xE4C: SHandle of the object the reach was started for.
        pObject = reinterpret_cast<CManipOb*>(Mem_RecoverPointer(
            reinterpret_cast<SHandle*>(reinterpret_cast<char*>(pPlayer) + 0xE4C)));
        if (pObject == 0) return;

        if (spidey::math::approximateDistance(pPlayer->mPos, pObject->mPos) >= 0x300) return;

        pPlayer->mHeldObject = pObject;
        pObject->Pickup();
        SFX_Play(7, 0x2000, 0);
        return;
    }
}

void PlayerBehaviorController::updateObjectThrow()
{
    CPlayer* pPlayer = &player_;
    {
        u16 anim;
        u8 isHardThrow;
        u8 doThrow;

        pPlayer->field_EA6 = 0;

        if (pPlayer->mAnimFinished != 0)
        {
            pPlayer->SwitchToStandMode();
            return;
        }

        if (pPlayer->mHeldObject == 0)
        {
            pPlayer->CheckJump();
            return;
        }

        anim = pPlayer->mAnim;
        isHardThrow = 0;

        if (anim == 0xC9 && pPlayer->mFrame >= 0x12)
        {
            isHardThrow = 1;
            doThrow = 1;
        }
        else if (anim == 0xC3 && pPlayer->mFrame >= 13)
        {
            isHardThrow = 0;
            doThrow = 1;
        }
        else
        {
            doThrow = 0;
        }

        if (pPlayer->mFrame >= 6)
        {
            pPlayer->PutCameraBehind(0);
        }

        if (doThrow == 0) return;

        {
            CVector dir;

            dir.vx = 0;
            dir.vy = 0;
            dir.vz = 0;

            if (pPlayer->field_DCC != 0)
            {
                i32 dist = (i32)spidey::math::approximateHorizontalDistance(pPlayer->mPos,
                    pPlayer->field_DCC->mPos);

                if (dist >= 0x400)
                {
                    pPlayer->mHeldObject->ThrowPos(&pPlayer->field_DCC->mPos,
                        dist / 32);
                }
                else
                {
                    i32 speed;

                    if (isHardThrow != 0)
                    {
                        speed = (dist << 12) / 24;
                        dir = -0x18 * pPlayer->mForwardDirection;
                    }
                    else
                    {
                        speed = (dist << 12) / 32;
                        dir = -0x20 * pPlayer->mForwardDirection;
                    }

                    if (speed < 0x14000)
                    {
                        speed = 0x14000;
                    }

                    dir.vy += (pPlayer->field_DCC->mPos.vy - pPlayer->mPos.vy
                        + 0x80000) / speed - speed / 2;

                    pPlayer->mHeldObject->Throw(&dir);
                }
            }
            else
            {
                if (isHardThrow != 0)
                {
                    dir = (-0x18 * pPlayer->mForwardDirection)
                        + (0xE * pPlayer->mUpDirection);
                }
                else
                {
                    dir = (-0x20 * pPlayer->mForwardDirection)
                        + (0x10 * pPlayer->mUpDirection);
                }

                pPlayer->mHeldObject->Throw(&dir);
            }
        }

        // 0x4B62DB
        if (isHardThrow != 0)
        {
            SFX_PlayPos(0x26, &pPlayer->mPos, 0);
        }
        else
        {
            SFX_PlayPos(0x25, &pPlayer->mPos, 0);
        }

        pPlayer->mHeldObject = 0;
        return;
    }
}

void PlayerBehaviorController::updateWebDomeTrapped()
{
    CPlayer* pPlayer = &player_;
    {
        u8 *pPad;

        pPlayer->mRMinor = 0x96;

        if ((u16)completedAnimation_ == 0x11C)
        {
            pPlayer->SwitchToStandMode();
            pPlayer->mRMinor = 0x64;
            return;
        }

        if (pPlayer->mAnim == 0x11C) return;

        pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
        if ((u32)(G_TIMER_RELATED - pPlayer->field_374) <= 0x96
            && pPad[0x120] == 0 && pPad[0x130] == 0 && pPad[0x100] == 0)
        {
            return;
        }

        pPlayer->PlaySingleAnim(0x11C, 0, -1);
        SFX_PlayPos(0x23, &pPlayer->mPos, 0);

        {
            CDome *pDome = reinterpret_cast<CDome*>(
                Mem_RecoverPointer(&pPlayer->field_AB8));

            if (pDome != 0)
            {
                pDome->Burst();
                pPlayer->field_AB8 = Mem_MakeHandle(0);
            }
        }
        return;
    }
}

void PlayerBehaviorController::updateReserved40000000()
{
    CPlayer* pPlayer = &player_;
    pPlayer->field_EA6 = 0;
    return;
}

void PlayerBehaviorController::updateSwitchPull()
{
    CPlayer* pPlayer = &player_;
    {
        CSwitch *pSwitch;

        if (pPlayer->field_E20 != 0)
        {
            // 0x4B509C
            if ((u16)completedAnimation_ != 0xFFFF)
            {
                pPlayer->SwitchToStandMode();
            }
            return;
        }

        if (pPlayer->mFrame < 0xC) return;

        // 0xE54: SHandle of the switch being pulled.
        pSwitch = reinterpret_cast<CSwitch*>(Mem_RecoverPointer(
            reinterpret_cast<SHandle*>(reinterpret_cast<char*>(pPlayer) + 0xE54)));
        if (pSwitch != 0)
        {
            pSwitch->Flick();
        }

        pPlayer->field_E20++;
        return;
    }
}

void PlayerBehaviorController::updateGrabbed()
{
    CPlayer* pPlayer = &player_;
    {
        u8 *pPad;

        pPlayer->field_EA6 = 0;

        if ((u16)completedAnimation_ == 0x99)
        {
            // 0x4B6597
            pPlayer->SwitchToStandMode();
            return;
        }

        if (pPlayer->field_894 != 0 && Rnd(8) == 0)
        {
            pPlayer->field_894 = pPlayer->field_894 - 1;
        }

        // left/right on the stick, counted on every sign change.
        if (pPlayer->field_87C != 0 && pPlayer->field_E2E < 0)
        {
            pPlayer->field_87C = 0;
            pPlayer->field_894++;
        }
        else if (pPlayer->field_87C == 0 && pPlayer->field_E2E > 0)
        {
            pPlayer->field_87C = 1;
            pPlayer->field_894++;
        }

        // 0x880: the same latch for up/down (field_E2D). It sits in a
        // spidey.h PADDING run, so it is reached by offset here.
        if (PLR_I32(pPlayer, 0x880) != 0 && pPlayer->field_E2D < 0)
        {
            PLR_I32(pPlayer, 0x880) = 0;
            pPlayer->field_894++;
        }
        else if (PLR_I32(pPlayer, 0x880) == 0 && pPlayer->field_E2D > 0)
        {
            PLR_I32(pPlayer, 0x880) = 1;
            pPlayer->field_894++;
        }

        // 0x884, 0x88C, 0x888 and 0x890: one "button is down" latch per
        // face button, all in the same PADDING run. Each pad entry is a
        // held byte followed by a just-pressed byte.
        pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

        if (PLR_I32(pPlayer, 0x884) != 0 && pPad[0x10] != 0)
        {
            PLR_I32(pPlayer, 0x884) = 0;
        }
        else if (PLR_I32(pPlayer, 0x884) == 0 && pPad[0x11] != 0)
        {
            pPad[0x11] = 0;
            PLR_I32(pPlayer, 0x884) = 1;
            pPlayer->field_894++;
        }

        pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

        if (PLR_I32(pPlayer, 0x88C) != 0 && pPad[0] != 0)
        {
            PLR_I32(pPlayer, 0x88C) = 0;
        }
        else if (PLR_I32(pPlayer, 0x88C) == 0 && pPad[1] != 0)
        {
            pPad[1] = 0;
            PLR_I32(pPlayer, 0x88C) = 1;
            pPlayer->field_894++;
        }

        pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

        if (PLR_I32(pPlayer, 0x888) != 0 && pPad[0x20] != 0)
        {
            PLR_I32(pPlayer, 0x888) = 0;
        }
        else if (PLR_I32(pPlayer, 0x888) == 0 && pPad[0x21] != 0)
        {
            pPad[0x21] = 0;
            PLR_I32(pPlayer, 0x888) = 1;
            pPlayer->field_894++;
        }

        pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

        if (PLR_I32(pPlayer, 0x890) != 0 && pPad[0x30] != 0)
        {
            PLR_I32(pPlayer, 0x890) = 0;
        }
        else if (PLR_I32(pPlayer, 0x890) == 0 && pPad[0x31] != 0)
        {
            pPad[0x31] = 0;
            PLR_I32(pPlayer, 0x890) = 1;
            pPlayer->field_894++;
        }

        if (pPlayer->field_894 > 8 && pPlayer->mAnim != 0x99)
        {
            pPlayer->PlaySingleAnim(0x99, 0, -1);
        }

        // slide halfway towards the grabber every tick.
        pPlayer->mPos.vx += (pPlayer->field_EE0.vx - pPlayer->mPos.vx) >> 1;
        pPlayer->mPos.vz += (pPlayer->field_EE0.vz - pPlayer->mPos.vz) >> 1;

        if (pPlayer->field_564 != 0)
        {
            pPlayer->field_564 = 0;
            if (pPlayer->mAnim != 0x99)
            {
                pPlayer->PlaySingleAnim(0x99, 0, -1);
            }
        }
        return;
    }
}

} // namespace player
} // namespace spidey
