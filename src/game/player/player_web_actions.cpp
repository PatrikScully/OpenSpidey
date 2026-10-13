#include "game/player/player_behavior_controller.h"
#include "game/player/player_action.h"
#include "compatibility/player_behavior_bridge.h"

namespace spidey {
namespace player {

void PlayerBehaviorController::updateSwingWindup()
{
    CPlayer* pPlayer = &player_;
    {
        pPlayer->field_EA6 = 0;
        pPlayer->field_54C = 1;

        // ebx still holds the 13 loaded at 0x4B211A here
        if (pPlayer->mFrame < 13) return;

        pPlayer->field_E80 = (i32)0xFFFC4000;
        if (pPlayer->mSupportBody != 0)
        {
            pPlayer->field_E80 = pPlayer->mSupportBody->mVel.vy - 0x3C000;
        }

        pPlayer->mActionState = actionWord(PlayerAction::SwingRelease);
        pPlayer->field_E20 = 0;
        SFX_PlayPos(9, &pPlayer->mPos, 0);

        pPlayer->field_E84 = 0x70000;
        pPlayer->field_E88 = 0xA8000;
        pPlayer->field_A8.vx = 0;
        pPlayer->field_A8.vy = (i16)0xF000;
        pPlayer->field_A8.vz = 0;

        if ((pPlayer->field_DAC.vx | pPlayer->field_DAC.vy | pPlayer->field_DAC.vz) != 0)
        {
            CVector normal;

            normal.vx = (pPlayer->mPos.vx - pPlayer->field_DAC.vx) >> 12;
            normal.vy = 0;
            normal.vz = (pPlayer->mPos.vz - pPlayer->field_DAC.vz) >> 12;
            VectorNormal(reinterpret_cast<VECTOR*>(&normal), reinterpret_cast<VECTOR*>(&normal));
            pPlayer->OrientToNormal(true, &normal);
            pPlayer->field_8E8 = 0;
        }
        else
        {
            pPlayer->OrientToNormal(false, &ZeroVector);
        }
        return;
    }
}

void PlayerBehaviorController::updateSwingRelease()
{
    CPlayer* pPlayer = &player_;
    {
        pPlayer->field_EA6 = 0;

        if (pPlayer->field_E20 == 0)
        {
            G_CAMERA_LIST->field_12C = 2;
            pPlayer->PutCameraBehind(0x10);
            pPlayer->field_E20++;
        }

        if ((pPlayer->field_504 & 0x200) != 0
            && (u32)(G_TIMER_RELATED - (i32)pPlayer->field_500) < 6)
        {
            if (pPlayer->ShouldPlayerDropFlail() != 0)
            {
                pPlayer->PlaySingleAnim(0xAF, 0, -1);
                pPlayer->mVel.vz = 0;
                pPlayer->mVel.vx = 0;
                pPlayer->mActionState = actionWord(PlayerAction::KnockdownRecovery);
                PLR_U8(pPlayer, 0x54E) = 0;
            }
            else
            {
                pPlayer->PlaySingleAnim(0xD8, 0, -1);
                pPlayer->mActionState = actionWord(PlayerAction::Falling);
            }
            pPlayer->mPendingWebReleaseAnimation = 0;
        }

        if (pPlayer->CheckStickToCeiling() != 0) return;

        if (pPlayer->mVel.vy < 0)
        {
            pPlayer->field_E2D = (char)0x81;
            pPlayer->field_E2E = 0;
            pPlayer->field_EBC = 0x10;
            return;
        }

        pPlayer->mActionState = actionWord(PlayerAction::Swinging);
        pPlayer->PlaySingleAnim(0x113, 0, -1);
        SFX_PlayPos(0x19, &pPlayer->mPos, 0);
        pPlayer->DecreaseWebbing(0x80);

        PLR_I32(pPlayer, 0xD7C) = (i32)Utils_Dist(pPlayer->mPos, pPlayer->field_D64);
        pPlayer->CalculateSwingWebParameters(&pPlayer->field_D64);

        print_if_false(pPlayer->field_E64 == 0, "already swinging");

        {
            void *pMem = CItem::operator new(0x190);

            if (pMem != 0)
            {
                gCSwinger_ctor(pMem, &pPlayer->field_D64,
                    PLR_I32(pPlayer, 0xD7C), &pPlayer->field_D80,
                    &pPlayer->field_DA0);
            }

            pPlayer->field_E64 = reinterpret_cast<i32*>(pMem);
            // the original writes this even when the allocation failed
            *reinterpret_cast<i32*>(reinterpret_cast<char*>(pMem) + 0xF8) =
                (u8)pPlayer->field_5E8;
        }

        SFX_Play(Rnd(3) + 0x15, 0x2000, 0);
        return;
    }
}

void PlayerBehaviorController::updateSwinging()
{
    CPlayer* pPlayer = &player_;
    {
        // var at [esp+0x13]: the swing has to stop this tick. Seeded from
        // CheckJumpingR1ZipWeb's return, which is 0 on every path that
        // gets here.
        u8 endSwing = 0;
        CBody *pSwinger;

        pPlayer->field_EA6 = 0;

        if (pPlayer->CheckJumpingSmashKick() != 0) return;
        if (pPlayer->CheckJumpingR1ZipWeb() != 0) return;

        pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);

        if (pSwinger != 0)
        {
            i32 frames;

            if (reinterpret_cast<CSwinger*>(pSwinger)->IsOneTimeToDie() != 0)
            {
                if (pPlayer->field_D60 != 0)
                {
                    // 0x4B32E4: the far attach point is the live one, so
                    // throw the next web and start a new CSwinger on it.
                    void *pMem;

                    pPlayer->PlaySingleAnim(0x118, 0, -1);
                    pPlayer->mPos -= (pPlayer->mForwardDirection * 0xC0);
                    pPlayer->DecreaseWebbing(0x80);

                    pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
                    CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
                    if (pSwinger != 0)
                    {
                        delete pSwinger;
                    }

                    PLR_I32(pPlayer, 0xD7C) =
                        (i32)Utils_Dist(pPlayer->mPos, pPlayer->field_D70);
                    pPlayer->CalculateSwingWebParameters(&pPlayer->field_D70);

                    pMem = CItem::operator new(0x190);

                    if (pMem != 0)
                    {
                        gCSwinger_ctor(pMem, &pPlayer->field_D70,
                            PLR_I32(pPlayer, 0xD7C), &pPlayer->field_D80,
                            &pPlayer->field_DA0);
                    }

                    pPlayer->field_E64 = reinterpret_cast<i32*>(pMem);
                    // the original writes this even when the allocation
                    // failed, the same defect state 0x200 has
                    *reinterpret_cast<i32*>(reinterpret_cast<char*>(pMem) + 0xF8) =
                        (u8)pPlayer->field_5E8;

                    SFX_Play(Rnd(3) + 0x15, 0x2000, 0);

                    // Animations[region * 17] + 8 * anim + 8 is the
                    // animation's frame count, the same table lookup
                    // spidey.cpp uses. Read full width here, no & 0xFFFF.
                    frames = *reinterpret_cast<i32*>(
                        reinterpret_cast<char*>(Animations[(i32)pPlayer->mRegion * 17])
                        + 8 * (i32)pPlayer->mAnim + 8);

                    // and the same missing null check on the new swinger
                    reinterpret_cast<CSwinger*>(pPlayer->field_E64)
                        ->SetSpideyAnimFrame(frames - 1);

                    pPlayer->field_D60 = 0;
                    SFX_PlayPos(0x19, &pPlayer->mPos, 0);
                }
                else
                {
                    // 0x4B3435: nothing left to swing on.
                    endSwing = 1;
                }
            }
            else
            {
                // 0x4B343C: hold the last frame of the swing animation.
                pPlayer->mAnimSpeed = 0;

                frames = *reinterpret_cast<i32*>(
                    reinterpret_cast<char*>(Animations[(i32)pPlayer->mRegion * 17])
                    + 8 * (i32)pPlayer->mAnim + 8);

                reinterpret_cast<CSwinger*>(pSwinger)->SetSpideyAnimFrame(frames - 1);
            }
        }

        // 0x4B346B: about to stop swinging in mid air, so look for the
        // surface under the hook and snap onto it.
        if (endSwing != 0 && (pPlayer->mCollision & 3) == 0)
        {
            SLineInfo hookLine;

            hookLine.StartCoords.vx = 0;
            hookLine.StartCoords.vy = 0;
            hookLine.StartCoords.vz = 0;
            hookLine.EndCoords.vx = 0;
            hookLine.EndCoords.vy = 0;
            hookLine.EndCoords.vz = 0;
            hookLine.MinCoords.vx = 0;
            hookLine.MinCoords.vy = 0;
            hookLine.MinCoords.vz = 0;
            hookLine.MaxCoords.vx = 0;
            hookLine.MaxCoords.vy = 0;
            hookLine.MaxCoords.vz = 0;
            hookLine.Position.vx = 0;
            hookLine.Position.vy = 0;
            hookLine.Position.vz = 0;
            hookLine.Normal.vx = 0;
            hookLine.Normal.vy = 0;
            hookLine.Normal.vz = 0;

            M3dUtils_GetHookPosition(
                reinterpret_cast<VECTOR*>(&hookLine.StartCoords), pPlayer, 2);

            hookLine.EndCoords.vx =
                hookLine.StartCoords.vx - (pPlayer->mForwardDirection.vx << 8);
            hookLine.EndCoords.vy =
                hookLine.StartCoords.vy - (pPlayer->mForwardDirection.vy << 8);
            hookLine.EndCoords.vz =
                hookLine.StartCoords.vz - (pPlayer->mForwardDirection.vz << 8);

            M3dColij_InitLineInfo(&hookLine);
            M3dZone_LineToItem(&hookLine, 1);

            if (hookLine.pItem != 0)
            {
                i32 standOff = pPlayer->mSurfaceClearance;

                pPlayer->mLineInfo.Normal.vx = hookLine.Normal.vx;
                pPlayer->mLineInfo.Normal.vy = hookLine.Normal.vy;
                pPlayer->mLineInfo.Normal.vz = hookLine.Normal.vz;

                pPlayer->mCollision |= 1;

                pPlayer->mPos.vx =
                    hookLine.Position.vx + (i32)hookLine.Normal.vx * standOff;
                pPlayer->mPos.vy =
                    hookLine.Position.vy + (i32)hookLine.Normal.vy * standOff;
                pPlayer->mPos.vz =
                    hookLine.Position.vz + (i32)hookLine.Normal.vz * standOff;
            }
        }

        // 0x4B35FD
        if ((pPlayer->mCollision & 3) != 0)
        {
            pPlayer->mVel.vx = 0;
            pPlayer->mVel.vy = 0;
            pPlayer->mVel.vz = 0;

            SFX_PlayPos(9, &pPlayer->mPos, 0);

            pPlayer->field_AE5 = 0;

            if (pPlayer->mLineInfo.pItem != 0)
            {
                i16 hitNormalY = pPlayer->mLineInfo.Normal.vy;
                u8 land = 0;

                if (hitNormalY < (i16)0xF5D8)
                {
                    // 0x4B3648: hit something facing steeply down, so
                    // drop off the web stunned.
                    pPlayer->mIsCrawling = 0;

                    pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
                    if (pSwinger != 0)
                    {
                        CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
                        delete pSwinger;
                        pPlayer->field_E64 = 0;
                    }

                    pPlayer->field_A8.vx = pPlayer->mLineInfo.Normal.vx;
                    pPlayer->field_A8.vy = hitNormalY;
                    pPlayer->field_A8.vz = pPlayer->mLineInfo.Normal.vz;

                    pPlayer->field_54C = 0;
                    pPlayer->field_550 = 1;
                    pPlayer->PlaySingleAnim(0xD5, 0, -1);
                    pPlayer->mActionState = actionWord(PlayerAction::Idle);
                    G_CAMERA_LIST->field_12C = -1;
                    return;
                }

                if (hitNormalY > 0xD48)
                {
                    if ((pPlayer->mLineInfo.pFace[3] & 0x40000) != 0)
                    {
                        // 0x4B376B
                        endSwing = 1;
                    }
                    else
                    {
                        // 0x4B36EA: land on a wall, facing along the
                        // travel direction.
                        pPlayer->mIsCrawling = 1;

                        pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
                        if (pSwinger != 0)
                        {
                            CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
                            delete pSwinger;
                            pPlayer->field_E64 = 0;
                        }

                        pPlayer->field_A8.vx = pPlayer->mLineInfo.Normal.vx;
                        pPlayer->field_A8.vy = hitNormalY;
                        pPlayer->field_A8.vz = pPlayer->mLineInfo.Normal.vz;

                        pPlayer->mOrientationReferenceDirection.vx = pPlayer->mForwardDirection.vx;
                        pPlayer->mOrientationReferenceDirection.vy = pPlayer->mForwardDirection.vy;
                        pPlayer->mOrientationReferenceDirection.vz = pPlayer->mForwardDirection.vz;

                        pPlayer->OrientToNormal(true, &pPlayer->mOrientationReferenceDirection);
                        pPlayer->LockTargetTorsoAngle();
                        land = 1;
                    }
                }
                else if ((pPlayer->mLineInfo.pFace[3] & 0x40000) == 0)
                {
                    // 0x4B3789: land on the floor, standing upright.
                    CVector up(0, 0x1000, 0);

                    pPlayer->mIsCrawling = 1;

                    pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
                    if (pSwinger != 0)
                    {
                        CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
                        delete pSwinger;
                        pPlayer->field_E64 = 0;
                    }

                    pPlayer->field_A8.vx = pPlayer->mLineInfo.Normal.vx;
                    pPlayer->field_A8.vy = hitNormalY;
                    pPlayer->field_A8.vz = pPlayer->mLineInfo.Normal.vz;

                    pPlayer->OrientToNormal(true, &up);
                    land = 1;
                }
                else if ((pPlayer->mLineInfo.pFace[3] & 0x8000000) != 0)
                {
                    // 0x4B3852: a fence. Walk the ray along the surface
                    // normal from face to face until it leaves the fence,
                    // then stick there and hand over to the fence
                    // surface transition.
                    SLineInfo fenceLine;
                    i32 standOff = pPlayer->mSurfaceClearance;
                    i32 normalX;
                    i32 normalY;
                    i32 normalZ;
                    i32 posX;
                    i32 posY;
                    i32 posZ;

                    pPlayer->field_A8.vx = pPlayer->mLineInfo.Normal.vx;
                    pPlayer->field_A8.vy = pPlayer->mLineInfo.Normal.vy;
                    pPlayer->field_A8.vz = pPlayer->mLineInfo.Normal.vz;

                    posX = pPlayer->mPos.vx;
                    posY = pPlayer->mPos.vy;
                    posZ = pPlayer->mPos.vz;

                    normalX = pPlayer->mLineInfo.Normal.vx;
                    normalY = pPlayer->mLineInfo.Normal.vy;
                    normalZ = pPlayer->mLineInfo.Normal.vz;

                    for (;;)
                    {
                        fenceLine.StartCoords.vx = 0;
                        fenceLine.StartCoords.vy = 0;
                        fenceLine.StartCoords.vz = 0;
                        fenceLine.EndCoords.vx = 0;
                        fenceLine.EndCoords.vy = 0;
                        fenceLine.EndCoords.vz = 0;
                        fenceLine.MinCoords.vx = 0;
                        fenceLine.MinCoords.vy = 0;
                        fenceLine.MinCoords.vz = 0;
                        fenceLine.MaxCoords.vx = 0;
                        fenceLine.MaxCoords.vy = 0;
                        fenceLine.MaxCoords.vz = 0;
                        fenceLine.Position.vx = 0;
                        fenceLine.Position.vy = 0;
                        fenceLine.Position.vz = 0;
                        fenceLine.Normal.vx = 0;
                        fenceLine.Normal.vy = 0;
                        fenceLine.Normal.vz = 0;

                        fenceLine.StartCoords.vx = posX + normalX * 40;
                        fenceLine.StartCoords.vy = posY + normalY * 40 + 0x4000;
                        fenceLine.StartCoords.vz = posZ + normalZ * 40;
                        fenceLine.EndCoords.vx = posX - normalX * 140;
                        fenceLine.EndCoords.vy = posY - normalY * 140 + 0x4000;
                        fenceLine.EndCoords.vz = posZ - normalZ * 140;

                        M3dColij_InitLineInfo(&fenceLine);
                        M3dZone_LineToItem(&fenceLine, 1);

                        if (fenceLine.pItem == 0) break;
                        if ((fenceLine.pFace[3] & 0x8000000) == 0) break;

                        standOff = pPlayer->mSurfaceClearance;

                        posX = fenceLine.Position.vx
                            + (i32)fenceLine.Normal.vx * standOff;
                        posY = fenceLine.Position.vy
                            + (i32)fenceLine.Normal.vy * standOff;
                        posZ = fenceLine.Position.vz
                            + (i32)fenceLine.Normal.vz * standOff;
                    }

                    // 0x4B3A88
                    pPlayer->mIsCrawling = 1;
                    pPlayer->mFenceSurfaceDetected = 1;
                    pPlayer->field_54C = 0;

                    pPlayer->mPos.vx = posX;
                    pPlayer->mPos.vy = posY;
                    pPlayer->mPos.vz = posZ;

                    pPlayer->field_550 = 1;
                    pPlayer->field_8E8 = 1;

                    pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
                    if (pSwinger != 0)
                    {
                        CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
                        delete pSwinger;
                        pPlayer->field_E64 = 0;
                    }

                    G_CAMERA_LIST->field_12C = -1;

                    print_if_false(pPlayer->CheckFenceSurfaceTransition() != 0,
                        "fence error");
                    return;
                }
                else
                {
                    // 0x4B3B07
                    endSwing = 1;
                }

                if (land != 0)
                {
                    // 0x4B37F4
                    pPlayer->TidyUpZipWebLandingPosition(0x20);
                    G_CAMERA_LIST->field_12C = -1;
                    pPlayer->field_54C = 0;
                    pPlayer->field_550 = 1;
                    pPlayer->PlaySingleAnim(0x119, 0, -1);
                    pPlayer->mActionState = actionWord(PlayerAction::Idle);
                    return;
                }
            }
        }

        // 0x4B3B0C
        if ((pPlayer->field_504 & 0x400) != 0
            && (u32)(G_TIMER_RELATED - (i32)pPlayer->field_500) < 6)
        {
            pPlayer->PlaySingleAnim(0xD8, 0, -1);
            pPlayer->mActionState = actionWord(PlayerAction::Falling);
            pPlayer->mPendingWebReleaseAnimation = 0;
            endSwing = 1;
        }

        // 0x4B3B4F
        {
            u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
            u8 padFire = pPad[0x101];

            if (padFire != 0)
            {
                pPlayer->field_E8D = 0;
            }

            // 0x4B3B66: the swing keeps going unless the fire button (or,
            // in practice mode, one of the other three) says to stop.
            if (endSwing == 0
                && !((*gPracticeDifficultyFlag == 0 || pPlayer->mSynthesizedInputActive != 0)
                    && padFire != 0))
            {
                if (*gPracticeDifficultyFlag == 0) return;

                if (pPad[0x111] == 0 && pPad[0x121] == 0
                    && pPad[0x131] == 0)
                {
                    return;
                }
            }

            // 0x4B3BB2: bleed off the swing and drop into the fall state.
            pPlayer->mVel.vx >>= 1;
            pPlayer->mVel.vz >>= 1;
            pPlayer->mAnimSpeed = 0x10000;

            if (pPlayer->mActionState != 4)
            {
                pPlayer->mActionState = actionWord(PlayerAction::Falling);
                pPlayer->mPendingWebReleaseAnimation = 1;
            }

            G_CAMERA_LIST->field_12C = -1;
            pPlayer->field_54C = 0;
            pPlayer->field_550 = 1;

            pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
            if (pSwinger != 0)
            {
                CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
                delete pSwinger;
                pPlayer->field_E64 = 0;
            }
        }
        return;
    }
}

void PlayerBehaviorController::updateWebAttackWindup()
{
    CPlayer* pPlayer = &player_;
    {
        u16 anim = pPlayer->mAnim;
        // bl at 0x4B44DD. The airborne variants of every animation in
        // this state are 10 ids higher than the standing ones.
        u8 airborne = (anim == 0x104) ? 1 : 0;
        // reached 0x4B47D8, the "the stick picked nothing" path
        u8 plainWeb = 0;

        pPlayer->field_EA6 = 0;

        if (airborne == 0 && anim != 0xFA)
        {
            if (anim != 0xFB && anim != 0x105) return;

            // 0x4B44FA: the recovery animation waits itself out.
            if (pPlayer->CheckJump() != 0) return;
            if (pPlayer->mAnimFinished == 0) return;

            pPlayer->field_8E1 = 0;
            pPlayer->SwitchToStandMode();
            return;
        }

        // 0x4B452A
        if (pPlayer->mFrame >= 5)
        {
            plainWeb = 1;
        }
        else
        {
            char leanX = pPlayer->field_E2D;

            if (leanX > 0 && (pPlayer->field_8E4 & 1) == 0)
            {
                // 0x4B4553: right, so the right tug web.
                char leanY;

                pPlayer->field_8F8 = 2;
                pPlayer->field_552 = 0;
                pPlayer->mActionState = actionWord(PlayerAction::WebFiring);
                pPlayer->PlaySingleAnim(airborne != 0 ? 0x106 : 0xFC, 0, -1);

                leanY = pPlayer->field_E2E;

                if (leanY > 0)
                {
                    pPlayer->field_544 = 2;
                }
                else if (leanY < 0)
                {
                    pPlayer->field_544 = 1;
                }
                else
                {
                    pPlayer->field_544 = 0;
                }

                PLR_U8(pPlayer, 0xAE) &= 0xFE;
                return;
            }

            if (leanX < 0 && (pPlayer->field_8E4 & 2) == 0)
            {
                // 0x4B45EB: left, so the left tug web.
                pPlayer->field_8F8 = 4;
                pPlayer->field_552 = 0;
                pPlayer->mActionState = actionWord(PlayerAction::WebAttachWindup);
                pPlayer->PlaySingleAnim(airborne != 0 ? 0x109 : 0xFF, 0, -1);
                PLR_U8(pPlayer, 0xAE) &= 0xFE;
                return;
            }

            // 0x4B4631
            if (airborne != 0)
            {
                plainWeb = 1;
            }
            else
            {
                char leanY = pPlayer->field_E2E;

                if (leanY < 0)
                {
                    if ((pPlayer->field_8E4 & 8) == 0
                        && (u32)(G_TIMER_RELATED - pPlayer->field_5B4) > 0x1E
                        && pPlayer->mIsCrawling == 0)
                    {
                        // 0x4B4669: down, so the knock down web.
                        if (pPlayer->DecreaseWebbing(0x400) == 0) return;

                        pPlayer->PlaySingleAnim(0x11D, 0, -1);
                        pPlayer->mActionState = actionWord(PlayerAction::KnockdownRecovery);
                        return;
                    }
                }
                else if (leanY > 0 && (pPlayer->field_8E4 & 4) == 0)
                {
                    // 0x4B46B1: up, so the web dome.
                    if (pPlayer->mIsCrawling != 0)
                    {
                        plainWeb = 1;
                    }
                    else if (pPlayer->DecreaseWebbing(0xC00) == 0)
                    {
                        plainWeb = 1;
                    }
                    else
                    {
                        SFX_PlayPos(0x22, &pPlayer->mPos, 0);
                        pPlayer->mActionState = actionWord(PlayerAction::WebDomeTrapped);
                        pPlayer->PlaySingleAnim(0x11B, 0, -1);
                        pPlayer->field_374 = (i32)G_TIMER_RELATED;
                        pPlayer->field_AB8 = Mem_MakeHandle(
                            new CDome(pPlayer, (u8)pPlayer->field_5E8));
                        return;
                    }
                }

                if (plainWeb == 0)
                {
                    // 0x4B475F: no lean, so try the zip web.
                    u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

                    if (pPlayer->field_8EA != 0)
                    {
                        plainWeb = 1;
                    }
                    else if (pPad[0x120] == 0 && pPad[0x130] == 0)
                    {
                        plainWeb = 1;
                    }
                    else
                    {
                        // 0x4B4783
                        pPlayer->field_DD8 = Mem_MakeHandle(
                            pPlayer->SelectTargetBaddy(0xBE, -0x1000, 0x1000, 0));
                        pPlayer->field_DE0 = (i32)G_TIMER_RELATED;
                        pPlayer->mActionState = actionWord(PlayerAction::ZipDive);
                        pPlayer->PlaySingleAnim(0x78, 0, -1);
                        return;
                    }
                }
            }
        }

        if (plainWeb == 0) return;

        // 0x4B47D8
        if (pPlayer->mAnimFinished != 0)
        {
            CWeb *pWeb = reinterpret_cast<CWeb*>(pPlayer->field_E6C);

            if (pWeb != 0)
            {
                if (pPlayer->field_5E4 != 0)
                {
                    SFX_Stop((u32)pPlayer->field_5E4);
                    pPlayer->field_5E4 = 0;
                }

                pWeb->SwitchToBlob();
                pPlayer->field_E6C = 0;
            }

            pPlayer->PlaySingleAnim(airborne != 0 ? 0x105 : 0xFB, 0, -1);
            return;
        }

        // 0x4B4831
        if (pPlayer->mFrame < 5) return;

        if (pPlayer->field_E6C == 0)
        {
            // 0x4B484F: throw the web.
            CWeb *pWeb;
            i32 result;

            if (pPlayer->field_552 != 0) return;

            pPlayer->field_552 = 1;

            // the same missing null check the other web spawns have
            pWeb = new CWeb();
            pPlayer->field_E6C = reinterpret_cast<i32*>(pWeb);
            pWeb->field_102 = 0;
            pWeb->field_F8 = (u8)pPlayer->field_5E8;

            if (pPlayer->field_8ED != 0)
            {
                CSVector aim;

                aim.vx = (i16)pPlayer->field_DA0.vx;
                aim.vy = (i16)pPlayer->field_DA0.vy;
                aim.vz = (i16)pPlayer->field_DA0.vz;

                result = pPlayer->FireWeb(false, 0x100, &pPlayer->mInteractionTargetPosition,
                    true, &aim);
            }
            else
            {
                result = pPlayer->FireWeb(true, 0x100, &ZeroVector, false,
                    &gTrajectoryVector);
            }

            if ((result & 1) != 0)
            {
                if (pPlayer->field_E6C != 0)
                {
                    delete reinterpret_cast<CWeb*>(pPlayer->field_E6C);
                }
                pPlayer->field_E6C = 0;

                if (pPlayer->field_5E4 != 0)
                {
                    SFX_Stop((u32)pPlayer->field_5E4);
                    pPlayer->field_5E4 = 0;
                }
                return;
            }

            if ((result & 2) != 0) return;

            // 0x4B495A: nothing was hit, drop the web as a blob.
            if (pPlayer->field_5E4 != 0)
            {
                SFX_Stop((u32)pPlayer->field_5E4);
                pPlayer->field_5E4 = 0;
            }

            reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
            pPlayer->field_E6C = 0;
            return;
        }

        // 0x4B4993: a web is already out, so keep re-firing it while the
        // button is held and hold the animation on frame 5.
        {
            CWeb *pWeb = reinterpret_cast<CWeb*>(pPlayer->field_E6C);
            u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
            i32 result;

            if (pPad[0x110] == 0) return;

            if (reinterpret_cast<CBody*>(Mem_RecoverPointer(
                    reinterpret_cast<SHandle*>(&pWeb->field_134)))
                != pPlayer->field_DCC)
            {
                if (pPlayer->field_5E4 != 0)
                {
                    SFX_Stop((u32)pPlayer->field_5E4);
                    pPlayer->field_5E4 = 0;
                }

                pWeb->SwitchToBlob();
                pPlayer->field_E6C = 0;
            }

            if (pPlayer->field_E6C == 0) return;

            if (pPlayer->field_8ED != 0)
            {
                result = pPlayer->FireWeb(false, 0x80, &pPlayer->mInteractionTargetPosition,
                    false, &gTrajectoryVector);
            }
            else
            {
                result = pPlayer->FireWeb(true, 0x80, &ZeroVector, false,
                    &gTrajectoryVector);
            }

            if ((result & 1) != 0)
            {
                if (pPlayer->field_E6C != 0)
                {
                    delete reinterpret_cast<CWeb*>(pPlayer->field_E6C);
                }
                pPlayer->field_E6C = 0;

                if (pPlayer->field_5E4 != 0)
                {
                    SFX_Stop((u32)pPlayer->field_5E4);
                    pPlayer->field_5E4 = 0;
                }
            }
            else if ((result & 2) == 0)
            {
                if (pPlayer->field_5E4 != 0)
                {
                    SFX_Stop((u32)pPlayer->field_5E4);
                    pPlayer->field_5E4 = 0;
                }

                reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
                pPlayer->field_E6C = 0;
            }

            // 0x4B4A90
            if (pPlayer->field_E6C == 0) return;

            pPlayer->mFrame = 5;
        }
        return;
    }
}

void PlayerBehaviorController::updateWebFiring()
{
    CPlayer* pPlayer = &player_;
    {
        u16 anim = pPlayer->mAnim;

        pPlayer->field_EA6 = 0;

        if (anim == 0x94)
        {
            CWeb *pWeb;
            i32 result;

            if (pPlayer->field_552 != 0)
            {
                // 0x4B414E: hold the last frame of the throw.
                if (pPlayer->mFrame < 7) return;
                pPlayer->RunAnim(0x9A, (i32)(i16)pPlayer->mFrame, -1);
                return;
            }

            if (pPlayer->mFrame < 5) return;
            if (pPlayer->field_E6C != 0) return;

            pPlayer->field_552 = 1;
            PLR_I16(pPlayer, 0xDD4) = 0x28;

            // Original defect, kept: the new web is written through
            // without a null check, so a failed allocation faults.
            pWeb = new CWeb();
            pPlayer->field_E6C = reinterpret_cast<i32*>(pWeb);
            pWeb->field_F8 = (u8)pPlayer->field_5E8;
            pWeb->field_102 = 0;

            result = pPlayer->FireWeb(true, 0x258, &ZeroVector, false,
                &gTrajectoryVector);

            if ((result & 1) != 0)
            {
                if (pPlayer->field_E6C != 0)
                {
                    delete reinterpret_cast<CWeb*>(pPlayer->field_E6C);
                }
                pPlayer->field_E6C = 0;
                return;
            }

            if ((result & 8) == 0 && pPlayer->field_DCC != 0)
            {
                // 0x4B4117
                pPlayer->mActionState = actionWord(PlayerAction::WebTugAttack);
                pPlayer->field_E20 = 0;
                return;
            }

            if (pPlayer->field_E6C == 0) return;

            reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
            pPlayer->field_E6C = 0;
            return;
        }

        if (anim == 0xFC || anim == 0x106)
        {
            i32 releaseFrame = (anim == 0xFC) ? 5 : 9;
            i32 webTime = (anim == 0xFC) ? 0x28 : 0x40;
            i32 loseAnim = (anim == 0xFC) ? 0xFD : 0x107;
            i32 targetAnim = (anim == 0xFC) ? 0xFE : 0x108;

            if (pPlayer->field_552 == 0
                && pPlayer->mFrame >= releaseFrame
                && pPlayer->field_E6C == 0)
            {
                CWeb *pWeb;
                i32 result;

                PLR_I16(pPlayer, 0xDD4) = (i16)webTime;
                pPlayer->field_552 = 1;

                // same missing null check as the 0x94 branch above.
                pWeb = new CWeb();
                pPlayer->field_E6C = reinterpret_cast<i32*>(pWeb);
                pWeb->field_102 = 1;
                pWeb->field_F8 = (u8)pPlayer->field_5E8;

                if (pPlayer->field_8ED != 0)
                {
                    CSVector aim;

                    aim.vx = (i16)pPlayer->field_DA0.vx;
                    aim.vy = (i16)pPlayer->field_DA0.vy;
                    aim.vz = (i16)pPlayer->field_DA0.vz;

                    result = pPlayer->FireWeb(false, 0x258,
                        &pPlayer->mInteractionTargetPosition, true, &aim);
                }
                else
                {
                    result = pPlayer->FireWeb(true, 0x258, &ZeroVector,
                        false, &gTrajectoryVector);
                }

                if ((result & 1) != 0)
                {
                    if (pPlayer->field_E6C != 0)
                    {
                        delete reinterpret_cast<CWeb*>(pPlayer->field_E6C);
                    }
                    pPlayer->field_E6C = 0;
                }
            }

            // 0x4B4282
            if (pPlayer->mAnimFinished == 0) return;

            if (pPlayer->field_DCC != 0 && pPlayer->field_DCC->mType == 0x197)
            {
                pPlayer->field_DCC = 0;
            }

            PLR_I16(pPlayer, 0xDD4) -= (i16)pPlayer->mElapsedTicks;

            if (pPlayer->field_E6C == 0)
            {
                pPlayer->PlaySingleAnim(loseAnim, 0, -1);
                return;
            }

            if (pPlayer->field_DCC != 0)
            {
                pPlayer->PlaySingleAnim(targetAnim, 0, -1);
                // 0x4B4117
                pPlayer->mActionState = actionWord(PlayerAction::WebTugAttack);
                pPlayer->field_E20 = 0;
                return;
            }

            reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
            pPlayer->field_E6C = 0;
            pPlayer->PlaySingleAnim(loseAnim, 0, -1);
            return;
        }

        // 0x4B44AC: the recovery animations just wait themselves out.
        if (anim != 0xFD && anim != 0x107 && anim != 0x9A) return;

        // 0x4B5085
        if (pPlayer->CheckJump() != 0) return;
        if (pPlayer->mAnimFinished == 0) return;
        pPlayer->SwitchToStandMode();
        return;
    }
}

void PlayerBehaviorController::updateWebAttachWindup()
{
    CPlayer* pPlayer = &player_;
    // 0x4B4E9B. Web-swing/attach wind up: three animations that
    // each fire a web on their release frame and drip webbing.
    u16 anim = pPlayer->mAnim;

    pPlayer->field_EA6 = 0;

    if (anim == 0x8B)
    {
        if (pPlayer->mAnimFinished != 0)
        {
            // 0x4B6597
            pPlayer->SwitchToStandMode();
            return;
        }

        // ebp still holds the 3 loaded at 0x4B2124
        if (pPlayer->mFrame < 3) return;

        if (pPlayer->field_552 == 0)
        {
            pPlayer->field_552 = 1;
            if (pPlayer->field_8ED != 0)
            {
                pPlayer->FireWeb(false, 0x384, &pPlayer->mInteractionTargetPosition, false, &gTrajectoryVector);
            }
            else
            {
                pPlayer->FireWeb(true, 0x384, &ZeroVector, false, &gTrajectoryVector);
            }
        }
        else if (pPlayer->CheckJump() != 0)
        {
            return;
        }

        if (pPlayer->mFrame > 5) return;
        pPlayer->CreateWebDrips(true, false);
        return;
    }

    if (anim == 0xFF || anim == 0x109)
    {
        if (pPlayer->mFrame < 5) return;

        if (pPlayer->field_552 == 0)
        {
            pPlayer->field_552 = 1;
            if (pPlayer->field_8ED != 0)
            {
                pPlayer->FireWeb(false, 0x384, &pPlayer->mInteractionTargetPosition, false, &gTrajectoryVector);
            }
            else
            {
                pPlayer->FireWeb(true, 0x384, &ZeroVector, false, &gTrajectoryVector);
            }
        }

        if (pPlayer->mFrame <= 0xB)
        {
            pPlayer->CreateWebDrips(true, true);
        }

        if (pPlayer->mAnimFinished == 0) return;

        pPlayer->PlaySingleAnim(anim == 0xFF ? 0x100 : 0x10A, 0, -1);
        return;
    }

    if (anim == 0x100 || anim == 0x10A)
    {
        if (pPlayer->CheckJump() != 0) return;
        if (pPlayer->mAnimFinished == 0) return;
        pPlayer->SwitchToStandMode();
    }
}

void PlayerBehaviorController::updateWebTugAttack()
{
    CPlayer* pPlayer = &player_;
    {
        pPlayer->field_EA6 = 0;

        if (pPlayer->field_E20 != 0 && pPlayer->CheckJump() != 0) return;

        if (pPlayer->field_E6C != 0)
        {
            CVector dir;
            CVector *pPath = 0;
            u16 anim = pPlayer->mAnim;
            u8 snap = 0;

            if (anim == 0x94)
            {
                if (pPlayer->mFrame >= 0xE)
                {
                    pPath = reinterpret_cast<CVector*>(
                        pPlayer->CalculateTugWebPathPoints());
                    dir = (pPlayer->mForwardDirection * 0x20)
                        + (pPlayer->mUpDirection * 0x10);
                    snap = 1;
                }
            }
            else if (anim == 0x102 || anim == 0x10C)
            {
                if (pPlayer->mFrame >= 9)
                {
                    dir = ((*reinterpret_cast<CVector*>(&pPlayer->mRightDirection) * 0x18)
                        + (pPlayer->mUpDirection * 0x10))
                        + (pPlayer->mForwardDirection * 0x18);
                    snap = 1;
                }
            }
            else if (anim == 0x101 || anim == 0x10B)
            {
                if (pPlayer->mFrame >= 9)
                {
                    dir = ((*reinterpret_cast<CVector*>(&pPlayer->mRightDirection) * -0x18)
                        + (pPlayer->mUpDirection * 0x10))
                        + (pPlayer->mForwardDirection * 0x18);
                    snap = 1;
                }
            }
            else if (anim == 0x103 || anim == 0x10D)
            {
                if (pPlayer->mFrame >= 9)
                {
                    dir = (pPlayer->mForwardDirection * 0x20)
                        + (pPlayer->mUpDirection * 0x10);
                    snap = 1;
                }
            }

            if (snap != 0)
            {
                // 0x4B5BF2
                gCWeb_SwitchToSnap(reinterpret_cast<CWeb*>(pPlayer->field_E6C),
                    dir, pPath);
                pPlayer->field_E6C = 0;
                pPlayer->field_E20 = 1;
            }
        }

        // 0x4B5C0A
        if (pPlayer->mAnimFinished == 0) return;

        if (pPlayer->mAnim == 0x94)
        {
            CVector back;

            back.vx = -pPlayer->mForwardDirection.vx;
            back.vy = -pPlayer->mForwardDirection.vy;
            back.vz = -pPlayer->mForwardDirection.vz;
            pPlayer->OrientToNormal(true, &back);
        }

        if (pPlayer->mAnim == 0xFE)
        {
            if (pPlayer->field_544 == 0)
            {
                pPlayer->PlaySingleAnim(0x103, 0, -1);
            }
            else if (pPlayer->field_544 == 1)
            {
                pPlayer->PlaySingleAnim(0x102, 0, -1);
            }
            else
            {
                pPlayer->PlaySingleAnim(0x101, 0, -1);
            }
            return;
        }

        if (pPlayer->mAnim != 0x108)
        {
            // 0x4B682F
            pPlayer->SwitchToStandMode();
            return;
        }

        if (pPlayer->field_544 == 0)
        {
            pPlayer->PlaySingleAnim(0x10D, 0, -1);
        }
        else if (pPlayer->field_544 == 1)
        {
            pPlayer->PlaySingleAnim(0x10C, 0, -1);
        }
        else
        {
            pPlayer->PlaySingleAnim(0x10B, 0, -1);
        }
        return;
    }
}

void PlayerBehaviorController::updateZipWebPull()
{
    CPlayer* pPlayer = &player_;
    {
        u16 anim = pPlayer->mAnim;

        pPlayer->field_EA6 = 0;

        if ((anim == 0x104 || anim == 0xFA) && pPlayer->mAnimFinished != 0)
        {
            CWeb *pWeb;
            CSVector aim;

            pPlayer->PlaySingleAnim(0x10E, 0, -1);

            // the same missing null check the other web spawns have
            pWeb = new CWeb();
            pPlayer->field_E6C = reinterpret_cast<i32*>(pWeb);
            pWeb->field_F8 = (u8)pPlayer->field_5E8;
            pWeb->field_102 = 0;

            aim.vx = (i16)pPlayer->field_DA0.vx;
            aim.vy = (i16)pPlayer->field_DA0.vy;
            aim.vz = (i16)pPlayer->field_DA0.vz;

            pPlayer->field_8F8 = 8;
            pPlayer->field_E10 = 1;
            pPlayer->FireWeb(false, 0x80, &pPlayer->mInteractionTargetPosition, true, &aim);
            pPlayer->field_E10 = 0;
            return;
        }

        // 0x4B529E. 13 is ebx, loaded at 0x4B211A before the dispatch.
        if ((anim == 0x10E && pPlayer->mFrame >= 13) || anim == 0x10F)
        {
            CVector toAnchor;
            i32 approach;

            if (anim == 0x10E)
            {
                pPlayer->mIsCrawling = 0;

                if (pPlayer->field_E6C != 0)
                {
                    reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
                    pPlayer->field_E6C = 0;
                }
            }

            // 0x4B52D9
            if ((pPlayer->field_504 & 0x40000) != 0
                && (u32)(G_TIMER_RELATED - (i32)pPlayer->field_500) < 6)
            {
                pPlayer->PlaySingleAnim(0xAF, 0, -1);
                pPlayer->mActionState = actionWord(PlayerAction::KnockdownRecovery);
                pPlayer->mPendingWebReleaseAnimation = 0;
                pPlayer->mVel.vx = 0;
                pPlayer->mVel.vy = 0;
                pPlayer->mVel.vz = 0;
                PLR_U8(pPlayer, 0x54E) = 0;
                return;
            }

            // 0x4B532D
            toAnchor = (pPlayer->mPos - pPlayer->mInteractionTargetPosition) >> 0xC;

            approach = pPlayer->field_DA0.vx * toAnchor.vx
                + pPlayer->field_DA0.vz * toAnchor.vz
                + pPlayer->field_DA0.vy * toAnchor.vy;

            if (approach >= 0
                && Utils_Dist(pPlayer->mPos, pPlayer->mInteractionTargetPosition) >= 0x80)
            {
                // 0x4B56DF: still travelling. Let go on the fire button
                // (or, in practice mode, any of the other three), else
                // keep steering the velocity at the anchor.
                u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
                u8 practice = *gPracticeDifficultyFlag;
                u8 letGo = 0;
                CSVector aimAngles;

                if (practice == 0 || pPlayer->mSynthesizedInputActive != 0)
                {
                    if (pPad[0x101] != 0)
                    {
                        letGo = 1;
                    }
                }

                if (letGo == 0 && practice != 0)
                {
                    if (pPad[0x111] != 0 || pPad[0x121] != 0
                        || pPad[0x131] != 0)
                    {
                        letGo = 1;
                    }
                }

                if (letGo == 0)
                {
                    // 0x4B573A
                    aimAngles.vx = 0;
                    aimAngles.vy = 0;
                    aimAngles.vz = 0;

                    Utils_CalcAim(&aimAngles, &pPlayer->mPos,
                        &pPlayer->mInteractionTargetPosition);
                    Utils_GetVecFromMagDir(&pPlayer->mVel, 0xF0, &aimAngles);

                    if (pPlayer->mVel.vy >= 0) return;

                    {
                        i32 velY = pPlayer->mVel.vy;
                        i32 velX = pPlayer->mVel.vx;
                        i32 velZ = pPlayer->mVel.vz;

                        if ((velY < 0 ? -velY : velY)
                            <= (velX < 0 ? -velX : velX)) return;
                        if ((velY < 0 ? -velY : velY)
                            <= (velZ < 0 ? -velZ : velZ)) return;
                    }

                    if (pPlayer->CheckJumpingSwingWeb() == 0) return;

                    pPlayer->mVel.vz = 0;
                    pPlayer->mVel.vy = 0;
                    pPlayer->mVel.vx = 0;
                    return;
                }

                // 0x4B57D7: let go and fall.
                pPlayer->mVel.vx = 0;
                pPlayer->mVel.vy = 0;
                pPlayer->mVel.vz = 0;

                pPlayer->field_A8.vx = 0;
                pPlayer->field_A8.vz = 0;
                pPlayer->field_A8.vy = (i16)0xF000;

                pPlayer->mActionState = actionWord(PlayerAction::Falling);
                pPlayer->mPendingWebReleaseAnimation = 1;
                pPlayer->field_550 = 1;

                pPlayer->mOrientationReferenceDirection.vx = pPlayer->mForwardDirection.vx;
                pPlayer->mOrientationReferenceDirection.vy = pPlayer->mForwardDirection.vy;
                pPlayer->mOrientationReferenceDirection.vz = pPlayer->mForwardDirection.vz;

                pPlayer->OrientToNormal(true, &pPlayer->mOrientationReferenceDirection);
                return;
            }

            // 0x4B53BD: arrived at the anchor, snap onto it.
            G_CAMERA_LIST->field_100 = 1;
            G_CAMERA_LIST->mTripod = pPlayer;

            pPlayer->field_AE5 = 0;

            pPlayer->mPos = pPlayer->mInteractionTargetPosition
                + (pPlayer->field_DA0 * (i32)pPlayer->mSurfaceClearance);

            pPlayer->mVel.vy = 0;
            pPlayer->mVel.vz = 0;
            pPlayer->mVel.vx = 0;

            if (pPlayer->field_DA0.vy <= 0xD48
                && pPlayer->field_DA0.vy >= -0xA28)
            {
                // 0x4B544A: the anchor normal is near horizontal, so work
                // out which way the surface really faces.
                CVector surface = (pPlayer->field_558 - pPlayer->mInteractionTargetPosition) >> 0xC;
                i32 align;

                VectorNormal(reinterpret_cast<VECTOR*>(&surface),
                    reinterpret_cast<VECTOR*>(&surface));

                align = ((pPlayer->field_DA0.vx * surface.vx) >> 12)
                    + ((pPlayer->field_DA0.vy * surface.vy) >> 12)
                    + ((pPlayer->field_DA0.vz * surface.vz) >> 12);

                if (align < 0x800)
                {
                    pPlayer->mOrientationReferenceDirection.vx = surface.vx;
                    pPlayer->mOrientationReferenceDirection.vy = surface.vy;
                    pPlayer->mOrientationReferenceDirection.vz = surface.vz;

                    pPlayer->field_A8.vx = (i16)pPlayer->field_DA0.vx;
                    pPlayer->field_A8.vy = (i16)pPlayer->field_DA0.vy;
                    pPlayer->field_A8.vz = (i16)pPlayer->field_DA0.vz;

                    pPlayer->OrientToNormal(true, &pPlayer->mOrientationReferenceDirection);
                }
                else if (((i32)pPlayer->field_A8.vy < 0
                    ? -(i32)pPlayer->field_A8.vy
                    : (i32)pPlayer->field_A8.vy) > 0x800)
                {
                    // 0x4B553B
                    CVector flipped;

                    flipped.vx = -(i32)pPlayer->field_A8.vx;
                    flipped.vy = -(i32)pPlayer->field_A8.vy;
                    flipped.vz = -(i32)pPlayer->field_A8.vz;

                    pPlayer->field_A8.vx = (i16)pPlayer->field_DA0.vx;
                    pPlayer->field_A8.vy = (i16)pPlayer->field_DA0.vy;
                    pPlayer->field_A8.vz = (i16)pPlayer->field_DA0.vz;

                    pPlayer->OrientToNormal(true, &flipped);
                }
                else
                {
                    // 0x4B5583
                    pPlayer->field_A8.vx = (i16)pPlayer->field_DA0.vx;
                    pPlayer->field_A8.vy = (i16)pPlayer->field_DA0.vy;
                    pPlayer->field_A8.vz = (i16)pPlayer->field_DA0.vz;

                    pPlayer->OrientToNormal(false, &ZeroVector);
                }
            }
            else
            {
                // 0x4B55AB: steep anchor, so use the travel direction
                // unless it is near vertical.
                i32 travelY = pPlayer->mForwardDirection.vy;

                if ((travelY < 0 ? -travelY : travelY) < 0x800)
                {
                    pPlayer->mOrientationReferenceDirection.vx = pPlayer->mForwardDirection.vx;
                    pPlayer->mOrientationReferenceDirection.vy = pPlayer->mForwardDirection.vy;
                    pPlayer->mOrientationReferenceDirection.vz = pPlayer->mForwardDirection.vz;
                }
                else
                {
                    CVector surface = (pPlayer->field_558 - pPlayer->mInteractionTargetPosition) >> 0xC;

                    VectorNormal(reinterpret_cast<VECTOR*>(&surface),
                        reinterpret_cast<VECTOR*>(&surface));

                    if (surface.vy < 0x800)
                    {
                        pPlayer->mOrientationReferenceDirection.vx = surface.vx;
                        pPlayer->mOrientationReferenceDirection.vy = surface.vy;
                        pPlayer->mOrientationReferenceDirection.vz = surface.vz;
                    }
                    else
                    {
                        pPlayer->mOrientationReferenceDirection.vx = 0;
                        pPlayer->mOrientationReferenceDirection.vy = 0;
                        pPlayer->mOrientationReferenceDirection.vz = 0x1000;
                    }
                }

                // 0x4B5675
                pPlayer->field_A8.vx = (i16)pPlayer->field_DA0.vx;
                pPlayer->field_A8.vy = (i16)pPlayer->field_DA0.vy;
                pPlayer->field_A8.vz = (i16)pPlayer->field_DA0.vz;

                pPlayer->OrientToNormal(true, &pPlayer->mOrientationReferenceDirection);
                pPlayer->LockTargetTorsoAngle();
            }

            // 0x4B56A9
            pPlayer->mIsCrawling = 1;
            pPlayer->PlaySingleAnim(0x110, 0, -1);
            SFX_Play(9, 0x2000, 0);
            pPlayer->TidyUpZipWebLandingPosition(0x20);
            return;
        }

        // 0x4B583E: the stuck-on-the-anchor animation.
        if (anim != 0x110) return;

        if (pPlayer->field_8E8 == 0 && pPlayer->field_8E9 == 0)
        {
            pPlayer->mIsCrawling = 0;
        }

        if (pPlayer->CheckJump() != 0) return;
        if (pPlayer->mAnimFinished == 0) return;

        if (pPlayer->field_8E8 == 0 && pPlayer->field_8E9 == 0)
        {
            pPlayer->PlaySingleAnim(0x14, 0, -1);
        }

        pPlayer->field_550 = 1;
        pPlayer->SwitchToStandMode();
        return;
    }
}

void PlayerBehaviorController::updateZipDive()
{
    CPlayer* pPlayer = &player_;
    {
        u16 lastAnim = (u16)completedAnimation_;
        CBody *pTarget;

        pPlayer->field_EA6 = 0;

        if (lastAnim == 0x7A || lastAnim == 0x79)
        {
            // 0x4B682F
            pPlayer->SwitchToStandMode();
            return;
        }

        if (lastAnim == 0x78)
        {
            pTarget = reinterpret_cast<CBody*>(
                Mem_RecoverPointer(&pPlayer->field_DD8));

            if (pTarget != 0)
            {
                i32 dx = (pTarget->mPos.vx - pPlayer->mPos.vx) >> 12;
                i32 dz = (pTarget->mPos.vz - pPlayer->mPos.vz) >> 12;
                i32 facing = pPlayer->mForwardDirection.vz * dz + dx * pPlayer->mForwardDirection.vx;

                if (facing < 0
                    && Utils_Dist(pPlayer->mPos, pTarget->mPos) <= 0xBE)
                {
                    // virtual slot 10 on the target ("grab me here?"),
                    // reached with the __fastcall shim because this build
                    // rejects the __thiscall keyword.
                    typedef u8 (FASTCALL *SlotTenFn)(void*, void*, CVector*);
                    CVector dest = pPlayer->mPos - (pPlayer->mForwardDirection * 0x20);
                    SlotTenFn fn = *reinterpret_cast<SlotTenFn*>(
                        *reinterpret_cast<char**>(pTarget) + 0x28);

                    if (fn(pTarget, 0, &dest) != 0)
                    {
                        return;
                    }
                }
            }

            pPlayer->PlaySingleAnim(0x79, 0, -1);
            pPlayer->field_DD8.pWhatever = 0;
            return;
        }

        if (pPlayer->mAnim != 0x7B) return;

        pTarget = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_DD8));

        if (pPlayer->CheckJump() != 0)
        {
            PLR_I32(pTarget, 0x2A8) &= ~0x40;
            pPlayer->field_DD8.pWhatever = 0;
            return;
        }

        pPlayer->CheckForwards(false);

        if ((u8)(pPlayer->field_E2E | pPlayer->field_E2D) == 0)
        {
            pPlayer->LockTargetTorsoAngle();
        }

        {
            u16 targetType = pTarget->mType;
            i32 holdTime = (targetType == 0x13A)
                ? Utils_GetValueFromDifficultyLevel(0x1A4, 0x78, 0x78, 0x3C)
                : 0x12C;
            u8 *pPad;

            if ((u32)(G_TIMER_RELATED - pPlayer->field_DE0) > (u32)holdTime)
            {
                PLR_I32(pTarget, 0x2A8) &= ~0x40;
                pPlayer->PlaySingleAnim(0x7A, 0, -1);
                pPlayer->field_DD8.pWhatever = 0;
                return;
            }

            pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
            if (pPad[0x121] != 0 && targetType != 0x13A)
            {
                pPlayer->mActionState = actionWord(PlayerAction::ZipDiveKick);
                pPlayer->PlaySingleAnim(0x7D, 0, -1);
                pPlayer->field_DE0 = (i32)G_TIMER_RELATED;
                return;
            }

            if (pPad[0x131] == 0) return;

            pPlayer->mActionState = actionWord(PlayerAction::ZipDivePunch);
            pPlayer->PlaySingleAnim(0x7C, 0, -1);
        }
        return;
    }
}

void PlayerBehaviorController::updateZipDivePunch()
{
    CPlayer* pPlayer = &player_;
    {
        CBody *pTarget;
        SHitInfo hit;

        pPlayer->field_EA6 = 0;

        if ((u16)completedAnimation_ == 0x7C)
        {
            // 0x4B682F
            pPlayer->SwitchToStandMode();
            return;
        }

        if (pPlayer->mAnim != 0x7C) return;
        if (pPlayer->mFrame < 0x10) return;

        pTarget = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_DD8));
        if (pTarget == 0) return;

        hit.field_C.vx = 0;
        hit.field_C.vy = 0;
        hit.field_C.vz = 0;
        hit.field_0 = 0x1E;
        hit.field_8 = (u16)pPlayer->GetDamageInflictedFromDifficulty(0x46);
        hit.field_C.vx = -pPlayer->mForwardDirection.vx;
        hit.field_4 = 2;
        hit.field_C.vy = 0;
        hit.field_C.vz = -pPlayer->mForwardDirection.vz;
        hit.field_18 = 0x258;
        hit.field_1A = 0x10;

        pTarget->Hit(&hit);

        pPlayer->field_DD8.pWhatever = 0;
        pPlayer->field_534 = 0x168;
        pPlayer->field_52C = (pPlayer->field_528 + 0xB) << 10;
        SFX_PlayPos(0x10, &pPlayer->mPos, 0);
        return;
    }
}

void PlayerBehaviorController::updateZipDiveKick()
{
    CPlayer* pPlayer = &player_;
    {
        CBody *pTarget;
        u8 *pPad;

        if (pPlayer->mAnim == 0x7D)
        {
            pPlayer->field_2C1 = 0;
            reinterpret_cast<u8*>(pPlayer->field_E0C)[0x101] = 0;
        }

        pPlayer->field_EA6 = 0;
        pPlayer->field_A80 = 0;

        pTarget = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_DD8));
        print_if_false(pTarget != 0, "Lost grab target");

        // Original defect, kept: mHealth is read straight after the
        // print_if_false above without acting on a null target, so a lost
        // target faults here.
        if (pTarget->mHealth > 0
            && (u32)(G_TIMER_RELATED - pPlayer->field_DE0) <= 0x12C
            && reinterpret_cast<u8*>(pPlayer->field_E0C)[0x101] == 0)
        {
            if ((u16)completedAnimation_ == 0x7E)
            {
                if (pTarget != 0)
                {
                    SHitInfo hit;
                    CVector dest;

                    hit.field_C.vx = 0;
                    hit.field_C.vy = 0;
                    hit.field_C.vz = 0;
                    hit.field_0 = 6;
                    hit.field_8 = (u16)pPlayer->GetDamageInflictedFromDifficulty(0x14);
                    hit.field_4 = 3;

                    pTarget->Hit(&hit);

                    pPlayer->field_52C = (pPlayer->field_528 + 0xB) << 10;
                    pPlayer->field_534 = 0x168;

                    dest = (pPlayer->mPos - (pPlayer->mForwardDirection * 0x20))
                        + (pPlayer->mUpDirection * 0x40);

                    pPlayer->CreateCombatImpactEffect(&dest, 0);
                    SFX_PlayPos(0x10, &pPlayer->mPos, 0);
                }
            }
            else if (pPlayer->mAnim == 0x80
                && reinterpret_cast<u8*>(pPlayer->field_E0C)[0x121] != 0)
            {
                pPlayer->PlaySingleAnim(0x7E, 0, -1);
            }
        }
        else
        {
            // 0x4B67BB. Target dead, held too long, or the kick key came
            // back up: throw the player off into the falling state.
            pPlayer->field_A80 = 1;
            pPlayer->mActionState = actionWord(PlayerAction::Falling);
            pPlayer->PlaySingleAnim(0x7F, 0, -1);

            pPlayer->mVel.vx += pPlayer->mForwardDirection.vx * 24;
            pPlayer->mVel.vy += -0x40000;
            pPlayer->mVel.vz += pPlayer->mForwardDirection.vz * 24;
            pPlayer->field_DD8.pWhatever = 0;
        }

        pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
        pPad[0x121] = 0;
        return;
    }
}

} // namespace player
} // namespace spidey
