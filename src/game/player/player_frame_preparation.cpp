#include "game/player/player_frame_preparation.h"
#include "compatibility/player_behavior_bridge.h"

namespace spidey {
namespace player {

bool PlayerFramePreparation::update(i32& verticalVelocityBeforePhysics)
{
    player_.field_AE4 = 1;
    updateCostumeVisibility();
    updateWaterEffect();
    updateDamageTimers();
    updateSenseEffect();
    updateFistBonus();
    initializeGroundContact();
    regenerateWebbing();
    updateSwingLine();
    prepareSupportAndCountdown();
    if (player_.mBehaviorSuspended) return false;
    updateFramePhysics(verticalVelocityBeforePhysics);
    selectSurfaceCamera();
    updateFriction();
    updateTargetingAndAirborneClock();
    updateLookaroundInput();
    updateMovementRamp();
    return true;
}

void PlayerFramePreparation::initializeGroundContact()
{
    CPlayer* pPlayer = &player_;

    // 0x553: one-shot "first tick" flag; drops the player onto the floor.
    if (PLR_U8(pPlayer, 0x553) == 0)
    {
        i32 groundY;

        PLR_U8(pPlayer, 0x553) = 1;
        groundY = Utils_GetGroundHeight(&pPlayer->mPos, 0, 0x800, 0);
        if (groundY != -1)
        {
            pPlayer->mPos.vy = groundY - (pPlayer->mSurfaceClearance << 12);
            pPlayer->mActionState = 1;
            pPlayer->field_A8.vx = 0;
            pPlayer->field_A8.vy = (i16)0xF000;
            pPlayer->field_A8.vz = 0;
            pPlayer->OrientToNormal(false, &ZeroVector);
            pPlayer->SetFloorCamera(0);
            pPlayer->PutCameraBehind(0);
        }
    }

}

void PlayerFramePreparation::regenerateWebbing()
{
    if (player_.field_5D8 == 0 && player_.field_5E8 == 0) {
        if (player_.mWebbing < 0x200) {
            player_.IncreaseWebbing(player_.mElapsedTicks * 4);
        } else if (player_.mWebbing < 0x555) {
            player_.IncreaseWebbing(player_.mElapsedTicks);
        }
    }
}

void PlayerFramePreparation::updateSwingLine()
{
    CPlayer* pPlayer = &player_;

    if (pPlayer->field_E64 != 0)
    {
        if ((pPlayer->mActionState & 0x400) != 0)
        {
            reinterpret_cast<CBody*>(pPlayer->field_E64)->AI();
        }
        else
        {
            delete reinterpret_cast<CBody*>(pPlayer->field_E64);
            pPlayer->field_E64 = 0;
        }
    }
}

void PlayerFramePreparation::prepareSupportAndCountdown()
{
    CPlayer* pPlayer = &player_;

    if (pPlayer->mSupportBody != 0 && (pPlayer->mSupportBody->mCBodyFlags & 0x40) != 0)
    {
        pPlayer->mSupportBody = 0;
    }

    G_BADDY_COLLISION_CHECK = 0;

    // 0xE28: a plain countdown, nothing in the prologue reads it.
    if (pPlayer->mBehaviorCountdownTicks != 0)
    {
        pPlayer->mBehaviorCountdownTicks -= pPlayer->mElapsedTicks;
        if (pPlayer->mBehaviorCountdownTicks < 0)
        {
            pPlayer->mBehaviorCountdownTicks = 0;
        }
    }

    pPlayer->mAnimSpeed = 0x10000;
}

void PlayerFramePreparation::updateFramePhysics(i32& verticalVelocityBeforePhysics)
{
    CPlayer* pPlayer = &player_;

    print_if_false(G_MECHLIST->mNextItem == 0, "2+ elements in G_MECHLIST_PLAYER");

    pPlayer->mFlags |= 4;
    pPlayer->field_E8 = pPlayer->mPos;

    verticalVelocityBeforePhysics = pPlayer->mVel.vy;

    if ((pPlayer->mActionState & 0x83000) == 0)
    {
        // Original player physics entry point at 0x466CE0.
        pPlayer->DoPhysics();
    }

    if (pPlayer->field_A8.vy > 0xD48)
    {
        pPlayer->OrientToNormal(true, &pPlayer->mOrientationReferenceDirection);
        pPlayer->field_8E8 = 0;
        pPlayer->field_8E9 = 1;
    }
    else
    {
        pPlayer->field_8E9 = 0;
        pPlayer->OrientToNormal(false, &ZeroVector);
        pPlayer->field_8E8 = (u8)(pPlayer->field_A8.vy >= (i16)0xF5D8);
    }
}

void PlayerFramePreparation::selectSurfaceCamera()
{
    CPlayer* pPlayer = &player_;

    {
        i32 state = pPlayer->mActionState;
        // Fall timing reference is refreshed outside the falling action.
        i32 lastAirborneTime = pPlayer->mFallReferenceTick;
        i32 camKind;

        if (state != 4)
        {
            pPlayer->mFallReferenceTick = (i32)G_TIMER_RELATED;
        }

        camKind = pPlayer->field_540;

        if ((state & 0x3000) == 0)
        {
            if (state == 4)
            {
                if (camKind != 5
                    && pPlayer->mVel.vy > 0
                    && (u32)(G_TIMER_RELATED - lastAirborneTime) > 0x3C)
                {
                    pPlayer->SetFallingCamera(15);
                }
            }
            else if (camKind != 4 && pPlayer->field_54C != 0)
            {
                pPlayer->SetSwingCamera(15);
            }
            else if (camKind != 2 && pPlayer->field_8E9 != 0)
            {
                pPlayer->SetCeilingCamera(16);
            }
            else if (camKind != 1 && pPlayer->field_8E8 != 0 && pPlayer->field_54C == 0)
            {
                pPlayer->SetWallCamera(16);
            }
            else if (camKind != 0
                && pPlayer->field_8E8 == 0
                && pPlayer->field_8E9 == 0
                && pPlayer->field_54C == 0)
            {
                pPlayer->SetFloorCamera(16);
            }
        }
    }
}

void PlayerFramePreparation::updateFriction()
{
    CPlayer* pPlayer = &player_;

    if (pPlayer->mIsCrawling != 0)
    {
        pPlayer->mFric.vx = 1;
        pPlayer->mFric.vy = 1;
        pPlayer->mFric.vz = 1;
    }
    else if ((pPlayer->mActionState & 0x1000000) != 0)
    {
        pPlayer->mFric.vx = 0x1F;
        pPlayer->mFric.vy = 0x1F;
        pPlayer->mFric.vz = 0x1F;
    }
    else if ((pPlayer->mActionState & 6) != 0)
    {
        pPlayer->mFric.vx = 4;
        pPlayer->mFric.vy = 4;
        pPlayer->mFric.vz = 4;
    }
    else
    {
        pPlayer->mFric.vx = 1;
        pPlayer->mFric.vy = ((pPlayer->mActionState & 0x40000) != 0) ? 1 : 4;
        pPlayer->mFric.vz = 1;
    }
}

void PlayerFramePreparation::updateTargetingAndAirborneClock()
{
    CPlayer* pPlayer = &player_;

    pPlayer->SelectAutoAimTarget();
    pPlayer->UpdateOffscreenSpideySenseIndicatorList();
    pPlayer->BuildOffscreenSpideySenseIndicatorList();

    pPlayer->mRMinor = 0x64;

    // 0xE8E: elapsed time in falling/knockdown actions without
    // ground contact, as a 16-bit accumulator of elapsed ticks.
    if ((pPlayer->mActionState & 0x800004) != 0 && (pPlayer->mCollision & 2) == 0)
    {
        PLR_U16(pPlayer, 0xE8E) = (u16)(PLR_U16(pPlayer, 0xE8E) + (u16)pPlayer->mElapsedTicks);
    }
    else
    {
        PLR_U16(pPlayer, 0xE8E) = 0;
    }
}

void PlayerFramePreparation::updateMovementRamp()
{
    CPlayer* pPlayer = &player_;

    // field_EBC ramps up to 16 while there is any move input and back down to
    // 0 when there is none.
    if ((pPlayer->field_E2E | pPlayer->field_E2D) != 0)
    {
        if (pPlayer->field_EBC < 0x10)
        {
            pPlayer->field_EBC += pPlayer->mElapsedTicks;
            if (pPlayer->field_EBC > 0x10)
            {
                pPlayer->field_EBC = 0x10;
            }
        }
    }
    else
    {
        if (pPlayer->field_EBC != 0)
        {
            pPlayer->field_EBC -= pPlayer->mElapsedTicks;
        }
        if (pPlayer->field_EBC < 0)
        {
            pPlayer->field_EBC = 0;
        }
    }
}

} // namespace player
} // namespace spidey
