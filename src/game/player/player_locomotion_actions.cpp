#include "game/player/player_behavior_controller.h"
#include "game/player/player_action.h"
#include "compatibility/player_behavior_bridge.h"
#include "game/player/locomotion_animation_policy.h"

namespace spidey {
namespace player {

void PlayerBehaviorController::updateIdle()
{
    CPlayer* pPlayer = &player_;
    {
        u8 *pPad;
        u8 anyInput;
        u8 xaPlaying;
        u16 anim;
        i32 wantAnim;
        i32 roll;
        i32 webChance;
        i32 midChance;
        CWeb *pWeb;

        if (pPlayer->CheckGroundGone() != 0) goto L_ReleaseWeb;

        anim = pPlayer->mAnim;
        if (anim == 0x124) return;

        if ((anim == 0x122 && pPlayer->field_E6C != 0) || anim == 0x123)
        {
            if (pPlayer->mFrame > 0x1C && pPlayer->field_E6C != 0)
            {
                reinterpret_cast<CWeb*>(pPlayer->field_E6C)->field_102 = 2;
            }

            pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
            anyInput = (u8)(pPad[0x70] | pPad[0x60] | pPad[0x50] | pPad[0x40]
                | pPad[0x30] | pPad[0x20] | pPad[0x10] | pPad[0]
                | (u8)pPlayer->field_E2D | (u8)pPlayer->field_E2E);
            xaPlaying = *gRedbookXaPlayingMaybe;

            if (anyInput != 0)
            {
                if (xaPlaying != 0)
                {
                    if (gRedbookXaRelatedOne == 0x20)
                    {
                        if (gRedbookXaRelatedTwo != 9)
                        {
                            pPlayer->PlaySingleAnim(0x124, 0, -1);
                            goto L_ReleaseWeb;
                        }
                        Redbook_XAStop();
                    }
                    else if (gRedbookXaRelatedOne == 0x21 && gRedbookXaRelatedTwo == 0)
                    {
                        Redbook_XAStop();
                    }
                }

                pPlayer->PlaySingleAnim(0x124, 0, -1);
                goto L_ReleaseWeb;
            }

            if (xaPlaying != 0) return;
            if (Rnd(0x40) != 0) return;

            if (Rnd(2) != 0)
            {
                Redbook_XAPlay(0x21, 0, 0);
            }
            else
            {
                Redbook_XAPlay(0x20, 9, 0);
            }
            return;
        }

        if (pPlayer->CheckWebShot() != 0) goto L_ReleaseWeb;
        if (pPlayer->CheckJumpingR1ZipWeb() != 0) goto L_ReleaseWeb;
        if (pPlayer->CheckKick() != 0) goto L_ReleaseWeb;
        if (pPlayer->CheckCeilingJumpingSmashPunch() != 0) goto L_ReleaseWeb;
        if (pPlayer->CheckJumpingSwingWeb() != 0) goto L_ReleaseWeb;
        if (pPlayer->CheckJumpingR2ZipWeb() != 0) goto L_ReleaseWeb;
        if (pPlayer->CheckJump() != 0) goto L_ReleaseWeb;
        if (pPlayer->CheckForwards(true) != 0) goto L_ReleaseWeb;

        if (pPlayer->field_8EA == 0)
        {
            pPlayer->field_EA6 = (i16)(pPlayer->field_EA6 + (u16)pPlayer->mElapsedTicks);
        }

        anim = pPlayer->mAnim;
        if (pPlayer->mHeadingTurnTicksRemaining == 0)
        {
            if (anim == 0x1B || anim == 0x1C || anim == 0x1D || anim == 0x1E)
            {
                pPlayer->PlaySingleAnim(pPlayer->mIsCrawling != 0 ? 0x13 : 0, 0, -1);
            }
        }
        else
        {
            pPlayer->field_EA6 = 0;

            if (anim != 0xC8 && anim != 0xC2)
            {
                if (pPlayer->mIsCrawling != 0)
                {
                    if (pPlayer->mHeadingTurnStep > 0)
                    {
                        if (anim != 0x1D) pPlayer->PlaySingleAnim(0x1D, 0, -1);
                    }
                    else
                    {
                        if (anim != 0x1E) pPlayer->PlaySingleAnim(0x1E, 0, -1);
                    }
                }
                else
                {
                    if (pPlayer->mHeadingTurnStep > 0)
                    {
                        if (anim != 0x1B) pPlayer->PlaySingleAnim(0x1B, 0, -1);
                    }
                    else
                    {
                        if (anim != 0x1C) pPlayer->PlaySingleAnim(0x1C, 0, -1);
                    }
                }
            }
        }

        // the idle "shoot a web at the ceiling" animation reaching its
        // release frame: spawn the web and fire it at the spot the
        // lookaround raycast below stored in 0x514/0x520.
        if (pPlayer->mAnim == 0x122
            && pPlayer->mFrame >= 4
            && pPlayer->field_E6C == 0)
        {
            pWeb = new CWeb();
            pWeb->field_F8 = (u8)pPlayer->field_5E8;
            pPlayer->field_E6C = reinterpret_cast<i32*>(pWeb);
            pWeb->field_102 = 0;
            // 0x510: "the idle web is out" flag.
            PLR_U8(pPlayer, 0x510) = 1;
            pWeb->Fire(pPlayer->mPos,
                *reinterpret_cast<CVector*>(reinterpret_cast<char*>(pPlayer) + 0x514),
                0,
                true,
                *reinterpret_cast<CSVector*>(reinterpret_cast<char*>(pPlayer) + 0x520));

            // field_12C was retyped u8* -> CKnottedWeb* while this was being
            // written; the original stores a byte at field_12C+0x6C, which is
            // CKnottedWeb::field_6C (bit2.h). Same address, same width.
            reinterpret_cast<CWeb*>(pPlayer->field_E6C)->field_12C->field_6C = 0;
            SFX_PlayPos(0x15, &pPlayer->mPos, 0);
        }

        if (pPlayer->field_8EA != 0)
        {
            wantAnim = (pPlayer->mIsCrawling != 0) ? 0x13 : 0;
            if (pPlayer->mAnim != (u16)wantAnim)
            {
                pPlayer->PlaySingleAnim(wantAnim, 0, -1);
            }
            goto L_ReleaseWeb;
        }

        if ((u16)completedAnimation_ != 0) return;
        if ((u16)pPlayer->field_EA6 <= 0xF0) return;

        roll = Rnd(0x64);
        webChance = 0;

        if (pPlayer->mIsCrawling == 0 && (u16)pPlayer->field_EA6 > 0xE10)
        {
            SLineInfo lineInfo;

            lineInfo.StartCoords = pPlayer->mPos;
            lineInfo.EndCoords.vx = pPlayer->mPos.vx;
            lineInfo.EndCoords.vy = pPlayer->mPos.vy - 0x800000;
            lineInfo.EndCoords.vz = pPlayer->mPos.vz;
            lineInfo.MinCoords.vx = 0;
            lineInfo.MinCoords.vy = 0;
            lineInfo.MinCoords.vz = 0;
            lineInfo.MaxCoords.vx = 0;
            lineInfo.MaxCoords.vy = 0;
            lineInfo.MaxCoords.vz = 0;
            lineInfo.Position.vx = 0;
            lineInfo.Position.vy = 0;
            lineInfo.Position.vz = 0;
            lineInfo.Normal.vx = 0;
            lineInfo.Normal.vy = 0;
            lineInfo.Normal.vz = 0;

            M3dColij_InitLineInfo(&lineInfo);
            M3dZone_LineToItem(&lineInfo, 1);

            if (lineInfo.pItem != 0 && (lineInfo.pFace[3] & 0x40000) == 0)
            {
                CVector *pHitPos = reinterpret_cast<CVector*>(reinterpret_cast<char*>(pPlayer) + 0x514);
                CSVector *pHitNormal = reinterpret_cast<CSVector*>(reinterpret_cast<char*>(pPlayer) + 0x520);

                pHitPos->vx = lineInfo.Position.vx;
                pHitPos->vy = lineInfo.Position.vy;
                pHitPos->vz = lineInfo.Position.vz;
                pHitNormal->vx = lineInfo.Normal.vx;
                pHitNormal->vy = lineInfo.Normal.vy;
                pHitNormal->vz = lineInfo.Normal.vz;
                webChance = 0x32;
            }
        }

        if (roll < webChance)
        {
            pPlayer->PlaySingleAnim(0x122, 0, -1);
            return;
        }

        midChance = webChance + ((0x64 - webChance) / 2);
        if (roll < midChance)
        {
            pPlayer->PlaySingleAnim(0x125, 0, -1);
            return;
        }

        pPlayer->PlaySingleAnim(0x126, 0, -1);
        return;

    L_ReleaseWeb:
        // 0x4B24D5, shared with several other states
        if (pPlayer->field_E6C != 0)
        {
            reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
            pPlayer->field_E6C = 0;
        }
        return;
    }
}

void PlayerBehaviorController::updateJumping()
{
    CPlayer* pPlayer = &player_;
    {
        u8 *pPad;

        pPlayer->field_EA6 = 0;

        if (pPlayer->CheckStickToCeiling() != 0) return;
        if (pPlayer->CheckJumpingSmashKick() != 0) return;
        if (pPlayer->CheckJumpingSwingWeb() != 0) return;
        if (pPlayer->CheckJumpingR1ZipWeb() != 0) return;
        if (pPlayer->CheckJumpingR2ZipWeb() != 0) return;

        if (pPlayer->mAnimFinished != 0)
        {
            if (pPlayer->mAnim == 0xE7)
            {
                pPlayer->PlaySingleAnim(0xE8, 0, -1);
            }
            else if (pPlayer->mAnim == 0xD7)
            {
                pPlayer->PlaySingleAnim(0xD8, 0, -1);
            }
        }

        pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
        if (pPad[0x100] == 0
            && (DifficultyLevel != 0 || Trig_GetLevelId() != 0x705))
        {
            pPlayer->field_E88 = 0;
        }

        if ((pPlayer->field_504 & 2) != 0
            && (u32)(G_TIMER_RELATED - (i32)pPlayer->field_500) < 6)
        {
            if (pPlayer->ShouldPlayerDropFlail() == 0) return;

            pPlayer->PlaySingleAnim(0xAF, 0, -1);
            pPlayer->mVel.vz = 0;
            pPlayer->mVel.vx = 0;
            pPlayer->mActionState = actionWord(PlayerAction::KnockdownRecovery);
            // 0x54E: cleared with every flail drop.
            PLR_U8(pPlayer, 0x54E) = 0;
            return;
        }

        if (pPlayer->field_E84 > 0) return;
        if (pPlayer->field_E88 > 0) return;

        pPlayer->mActionState = actionWord(PlayerAction::Falling);
        return;
    }
}

void PlayerBehaviorController::updateFalling()
{
    CPlayer* pPlayer = &player_;
    {

        pPlayer->field_EA6 = 0;

        if (pPlayer->mHealth <= 0)
        {
            if (pPlayer->CheckLanded() != 0)
            {
                pPlayer->SwitchToDeathMode(false);
            }
            return;
        }

        if (pPlayer->CheckLanded() != 0) return;
        if (pPlayer->CheckStickToWall() != 0) return;
        if (pPlayer->CheckStickToCeiling() != 0) return;
        if (pPlayer->CheckJumpingSmashKick() != 0) return;
        if (pPlayer->CheckJumpingSwingWeb() != 0) return;
        if (pPlayer->CheckJumpingR1ZipWeb() != 0) return;
        if (pPlayer->CheckJumpingR2ZipWeb() != 0) return;

        // fallen out of the world for long enough
        if (PLR_U16(pPlayer, 0xE8E) > 0x1E0)
        {
            gLevelStatus = 8;
        }

        if (pPlayer->mAnimFinished != 0 && pPlayer->mAnim == 0xD7)
        {
            pPlayer->PlaySingleAnim(0xD8, 0, -1);
        }
        if (pPlayer->mAnimFinished != 0 && pPlayer->mAnim == 0x7F)
        {
            pPlayer->PlaySingleAnim(0xD8, 0, -1);
        }

        if ((pPlayer->field_504 & 4) != 0
            && (u32)(G_TIMER_RELATED - (i32)pPlayer->field_500) < 6
            && pPlayer->ShouldPlayerDropFlail() != 0)
        {
            pPlayer->PlaySingleAnim(0xAF, 0, -1);
            pPlayer->mActionState = actionWord(PlayerAction::KnockdownRecovery);
            PLR_U8(pPlayer, 0x54E) = 0;
        }

        const FallAnimationPlan plan = LocomotionAnimationPolicy::falling(
            pPlayer->mAnim, verticalVelocityBeforePhysics_, pPlayer->mVel.vy,
            pPlayer->mPendingWebReleaseAnimation != 0);
        if (plan.resetReference) {
            pPlayer->mFallReferenceY = pPlayer->mPos.vy;
            pPlayer->mFallReferenceTick = static_cast<i32>(G_TIMER_RELATED);
            if (plan.nextAnimation != LocomotionAnimationPolicy::KeepAnimation) {
                pPlayer->PlaySingleAnim(plan.nextAnimation, 0, -1);
            }
            if (plan.clearWebRelease) pPlayer->mPendingWebReleaseAnimation = 0;
        }

        if (pPlayer->field_AE5 != 0
            && (u32)(G_TIMER_RELATED - pPlayer->mFallReferenceTick) > 0x1E)
        {
            pPlayer->field_AE5 = 0;
            pPlayer->field_AE6 = 0;
        }
        return;
    }
}

void PlayerBehaviorController::updateWallAttached()
{
    CPlayer* pPlayer = &player_;
    {
        u8 senseFlag;
        u8 *pPad;
        CBody *pTarget;
        i32 e84;

        pPlayer->field_EA6 = 0;

        if (pPlayer->CheckGroundGone() != 0) return;

        if (pPlayer->mAnim != 0xB2 && pPlayer->mAnim != 0xB4)
        {
            if (pPlayer->CheckForwards(true) != 0) return;
            if (pPlayer->CheckJump() != 0) return;
            if (pPlayer->CheckKick() != 0) return;
            if (pPlayer->CheckJumpingSwingWeb() != 0) return;
            if (pPlayer->CheckJumpingR1ZipWeb() != 0) return;
            if (pPlayer->CheckJumpingR2ZipWeb() != 0) return;
        }

        senseFlag = pPlayer->field_E8C;
        pPlayer->field_AE4 = (u8)(senseFlag != 0);

        if ((u16)completedAnimation_ == 0xFFFF) return;
        if ((u16)completedAnimation_ == 0xB2) return;

        if (senseFlag == 0)
        {
            // 0x4B682F
            pPlayer->SwitchToStandMode();
            return;
        }

        pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
        if (pPad[0x100] == 0)
        {
            // 0x4B6010
            pPlayer->mActionState = actionWord(PlayerAction::GroundMovement);
            return;
        }

        pTarget = pPlayer->mSupportBody;
        pPlayer->field_E80 = (i32)0xFFFC4000;
        if (pTarget != 0)
        {
            i32 targetVelY = pTarget->mVel.vy;

            if (targetVelY >= (i32)0xFFFC0000)
            {
                pPlayer->field_E80 = targetVelY + (i32)0xFFFC4000;
            }
            else
            {
                pPlayer->field_E80 = targetVelY * 2 - 0x3C000;
            }
        }

        pPlayer->PlaySingleAnim(0xDF, 0, -1);

        e84 = pPlayer->field_E84;
        pPlayer->field_E8D = 0;
        pPlayer->mActionState = actionWord(PlayerAction::WallJumpWindup);
        pPlayer->field_E88 = (e84 >> 1) + e84;

        if ((pPlayer->mCollision & 1) != 0)
        {
            pPlayer->field_EBC = 0;
        }
        return;
    }
}

void PlayerBehaviorController::updateGroundMovement()
{
    CPlayer* pPlayer = &player_;
    {
        u16 followOn;
        i32 aimDelta;
        i16 heading;

        pPlayer->field_EA6 = 0;

        if (pPlayer->mHeldObject != 0 && (pPlayer->mCollision & 0x40) != 0)
        {
            CManipOb *pHeld;

            pPlayer->PlaySingleAnim(0x15, 0, -1);
            pHeld = pPlayer->mHeldObject;
            pPlayer->mHeldObject = 0;
            pHeld->Smash();
        }

        if (pPlayer->CheckGroundGone() != 0) return;
        if (pPlayer->CheckWebShot() != 0) return;
        if (pPlayer->CheckJump() != 0) return;
        if (pPlayer->CheckCeilingJumpingSmashPunch() != 0) return;
        if (pPlayer->CheckKick() != 0) return;
        if (pPlayer->CheckWebShot() != 0) return;
        if (pPlayer->CheckRunIntoWall() != 0) return;

        if (pPlayer->mIsCrawling == 0)
        {
            if (pPlayer->CheckJumpingSwingWeb() != 0) return;
            if (pPlayer->CheckJumpingR1ZipWeb() != 0) return;
            if (pPlayer->CheckJumpingR2ZipWeb() != 0) return;
        }

        if (pPlayer->CheckInteriorSurfaceTransition() != 0
            || pPlayer->CheckExteriorSurfaceTransition() != 0
            || pPlayer->CheckFenceSurfaceTransition() != 0)
        {
            // the original calls the out of line CVector zero helper at
            // 0x4B8B70 on mVel here
            pPlayer->mVel.vx = 0;
            pPlayer->mVel.vy = 0;
            pPlayer->mVel.vz = 0;
            return;
        }

        if (pPlayer->field_8EA != 0)
        {
            // 0x4B682F
            pPlayer->SwitchToStandMode();
            return;
        }

        if ((u8)(pPlayer->field_E2E | pPlayer->field_E2D) == 0)
        {
            pPlayer->SwitchToStandMode();
            return;
        }

        followOn = (u16)completedAnimation_;
        if (followOn == 0x34)
        {
            if (pPlayer->mUsesHardSurfaceTransitions == 0)
            {
                pPlayer->PlaySingleAnim(0x32, 0, -1);
            }
        }
        else if (followOn == 0x33 || followOn == 0x55)
        {
            if (pPlayer->mUsesHardSurfaceTransitions != 0)
            {
                pPlayer->PlaySingleAnim(0x34, 0, -1);
            }
        }

        if (pPlayer->field_8E8 != 0)
        {
            heading = pPlayer->GetEffectiveHeading();
            aimDelta = ((u16)pPlayer->field_E32 - (i32)(u16)heading) & 0xFFF;
        }
        else
        {
            i32 turned = ((u16)pPlayer->field_E32 + (i32)G_CAMERA_LIST->field_23A) & 0xFFF;

            heading = pPlayer->GetEffectiveHeading();
            aimDelta = (turned - (i32)(u16)heading) & 0xFFF;
        }

        if (aimDelta <= 0x600) return;
        if (aimDelta >= 0xA00) return;

        const u16 newAnim = LocomotionAnimationPolicy::reverseMovement(pPlayer->mAnim);
        if (newAnim == LocomotionAnimationPolicy::KeepAnimation) return;

        pPlayer->PlaySingleAnim(newAnim, 0, -1);
        pPlayer->mVel.vx = 0;
        pPlayer->mVel.vy = 0;
        pPlayer->mVel.vz = 0;
        pPlayer->LockTargetTorsoAngle();
        pPlayer->mActionState = actionWord(PlayerAction::Rolling);
        return;
    }
}

void PlayerBehaviorController::updateWallJumpWindup()
{
    CPlayer* pPlayer = &player_;
    {
        u16 anim = pPlayer->mAnim;

        pPlayer->field_EA6 = 0;

        if (anim == 0xD2)
        {
            if (pPlayer->mAnimFinished == 0) return;
            pPlayer->PlaySingleAnim(0xD3, 0, -1);
        }
        else if (anim == 0xDF)
        {
            if (pPlayer->mAnimFinished == 0) return;
            pPlayer->PlaySingleAnim(Rnd(2) != 0 ? 0xE7 : 0xE0, 0, -1);
        }
        else
        {
            return;
        }

        pPlayer->field_E84 = 0x70000;
        pPlayer->field_E80 = (i32)0xFFFC4000;
        if (pPlayer->mSupportBody != 0)
        {
            pPlayer->field_E80 = pPlayer->mSupportBody->mVel.vy - 0x3C000;
        }
        pPlayer->mActionState = actionWord(PlayerAction::Jumping);
        pPlayer->field_E88 = 0xA8000;
        return;
    }
}

void PlayerBehaviorController::updateDead()
{
    CPlayer* pPlayer = &player_;
    {
        i32 elapsed = 0;

        pPlayer->field_EA6 = 0;

        if (pPlayer->mAnimFinished != 0)
        {
            if (*gSpideyDeathTimer >= 0x78)
            {
                gInitRelatedTwo = 0;
                gLevelStatus = 2;
                Flash_Reset();
            }
            elapsed = *gSpideyDeathTimer + pPlayer->mElapsedTicks;
        }

        *gSpideyDeathTimer = elapsed;
        return;
    }
}

void PlayerBehaviorController::updateSurfaceLanding()
{
    CPlayer* pPlayer = &player_;
    {
        u16 lastAnim = (u16)completedAnimation_;
        i32 slide;
        i16 nx;
        i16 ny;
        i16 nz;
        CVector hook;
        CVector normal;

        pPlayer->field_EA6 = 0;

        if (lastAnim == 0xFFFF)
        {
            // 0x4B3FB8: eat the queued slide 8 units at a time
            i32 left = PLR_I32(pPlayer, 0xC58);

            if (left == 0) return;

            if (left >= 8)
            {
                pPlayer->mPos += (pPlayer->mForwardDirection * 8);
                PLR_I32(pPlayer, 0xC58) = left - 8;
            }
            else
            {
                pPlayer->mPos += (pPlayer->mForwardDirection * left);
                PLR_I32(pPlayer, 0xC58) = 0;
            }
            return;
        }

        if (lastAnim == 0x57)
        {
            slide = 0xC;
        }
        else if (lastAnim == 0x58)
        {
            slide = 0x10;
        }
        else
        {
            slide = (lastAnim == 0x3C) ? 0x42 : 0x37;
        }

        nx = PLR_I16(pPlayer, 0xB84);
        ny = PLR_I16(pPlayer, 0xB86);
        nz = PLR_I16(pPlayer, 0xB88);

        PLR_U8(pPlayer, 0xAD6) = 1;
        pPlayer->field_A8.vx = nx;
        pPlayer->field_A8.vy = ny;
        pPlayer->field_A8.vz = nz;

        if (ny < (i16)0xF5D8)
        {
            pPlayer->field_8E9 = 0;
            pPlayer->field_8E8 = 0;
            pPlayer->mIsCrawling = 0;
            pPlayer->SwitchToStandMode();

            if (lastAnim != 0x57 && lastAnim != 0x58)
            {
                pPlayer->PlaySingleAnim(0x14, 0, -1);
                pPlayer->mActionState = actionWord(PlayerAction::Idle);
            }
        }
        else
        {
            pPlayer->mActionState = actionWord(PlayerAction::GroundMovement);

            if (ny > 0xD48)
            {
                if (pPlayer->field_ADA == 0)
                {
                    pPlayer->field_AD8 = 1;
                }
                pPlayer->field_8E8 = 0;
                pPlayer->field_8E9 = 1;
            }
            else
            {
                if (pPlayer->field_8E8 == 0 && pPlayer->field_ADB != 0)
                {
                    pPlayer->field_AD9 = 1;
                }
                pPlayer->field_8E8 = 1;
                pPlayer->field_8E9 = 0;
            }
        }

        hook.vx = 0;
        hook.vy = 0;
        hook.vz = 0;
        M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hook), pPlayer, 2);
        pPlayer->mPos = hook;

        normal.vx = -pPlayer->mUpDirection.vx;
        pPlayer->mOrientationReferenceDirection.vx = normal.vx;
        normal.vy = -pPlayer->mUpDirection.vy;
        pPlayer->mOrientationReferenceDirection.vy = normal.vy;
        normal.vz = -pPlayer->mUpDirection.vz;
        pPlayer->mOrientationReferenceDirection.vz = normal.vz;

        pPlayer->OrientToNormal(true, &normal);
        pPlayer->LockTargetTorsoAngle();
        pPlayer->mPos += (pPlayer->mUpDirection * slide);
        return;
    }
}

void PlayerBehaviorController::updateSurfaceTransition()
{
    CPlayer* pPlayer = &player_;
    {
        CVector normal;
        CVector hookPos;
        i32 pushOut;

        pPlayer->field_EA6 = 0;

        if ((u16)completedAnimation_ == 0xFFFF)
        {
            // 0x4B4E1B: eat the queued slide along mForwardDirection, 8 units at
            // a time. 0xC54 is a second slide counter next to the 0xC58
            // one state 0x1000 uses, both inside a spidey.h PADDING run.
            i32 left = PLR_I32(pPlayer, 0xC54);

            if (left == 0) return;

            if (left >= 8)
            {
                pPlayer->mPos += (pPlayer->mForwardDirection * 8);
                PLR_I32(pPlayer, 0xC54) = left - 8;
            }
            else
            {
                pPlayer->mPos += (pPlayer->mForwardDirection * left);
                PLR_I32(pPlayer, 0xC54) = 0;
            }
            return;
        }

        pushOut = ((u16)completedAnimation_ == 0x3F) ? 0x42 : 0x37;

        hookPos.vx = 0;
        hookPos.vy = 0;
        hookPos.vz = 0;
        pPlayer->field_AD6 = 1;
        M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hookPos), pPlayer, 2);

        pPlayer->mPos.vx = hookPos.vx;
        pPlayer->mPos.vy = hookPos.vy;
        pPlayer->mPos.vz = hookPos.vz;

        normal.vx = pPlayer->mUpDirection.vx;
        normal.vy = pPlayer->mUpDirection.vy;
        normal.vz = pPlayer->mUpDirection.vz;

        pPlayer->mOrientationReferenceDirection.vx = pPlayer->mUpDirection.vx;
        pPlayer->mOrientationReferenceDirection.vy = pPlayer->mUpDirection.vy;
        pPlayer->mOrientationReferenceDirection.vz = pPlayer->mUpDirection.vz;

        pPlayer->LockTargetTorsoAngle();

        if (pPlayer->mLineInfo.Normal.vy < (i16)0xF5D8)
        {
            // pointing far enough down: we are back on our feet.
            pPlayer->field_8E9 = 0;
            pPlayer->field_8E8 = 0;
            pPlayer->field_A8.vx = 0;
            pPlayer->field_A8.vy = (i16)0xF000;
            pPlayer->field_A8.vz = 0;
            pPlayer->OrientToNormal(true, &normal);
            pPlayer->mIsCrawling = 0;

            if ((u16)completedAnimation_ == 0x3B
                || (u16)completedAnimation_ == 0x56
                || (u16)completedAnimation_ == 0x5D)
            {
                SFX_PlayPos(9, &pPlayer->mPos, 0);
                pushOut = 0;
                pPlayer->SwitchToStandMode();
            }
            else
            {
                pPlayer->PlaySingleAnim(0x14, 0, -1);
                pPlayer->mActionState = actionWord(PlayerAction::Idle);
            }

            if (pushOut != 0)
            {
                pPlayer->mPos += (pPlayer->mUpDirection * pushOut);
                pushOut = 0;
            }

            pPlayer->TidyUpZipWebLandingPosition(8);

            {
                i32 height = Utils_GetGroundHeight(&pPlayer->mPos, 0, 0xC8, 0);

                if (height != -1)
                {
                    pPlayer->mPos.vy = height - ((i32)pPlayer->mSurfaceClearance << 12);
                }
                else
                {
                    // 0x4B4C24: no ground under us, so step sideways
                    // along mRightDirection (a CVector living in three i32
                    // members in spidey.h), further every other try,
                    // until some ground turns up.
                    i32 step = 8;
                    i32 i;

                    for (i = 0; i < 0x12; i++)
                    {
                        CVector probe;

                        probe.vx = pPlayer->mPos.vx;
                        probe.vy = pPlayer->mPos.vy;
                        probe.vz = pPlayer->mPos.vz;

                        if ((i & 1) != 0)
                        {
                            probe += (*reinterpret_cast<CVector*>(&pPlayer->mRightDirection) * step);
                        }
                        else
                        {
                            probe -= (*reinterpret_cast<CVector*>(&pPlayer->mRightDirection) * step);
                        }

                        height = Utils_GetGroundHeight(&probe, 0, 0xC8, 0);

                        if (height != -1)
                        {
                            pPlayer->mPos.vx = probe.vx;
                            pPlayer->mPos.vy = height - ((i32)pPlayer->mSurfaceClearance << 12);
                            pPlayer->mPos.vz = probe.vz;
                            break;
                        }

                        if ((i & 1) != 0)
                        {
                            step = step * 2;
                        }
                    }
                }
            }
        }
        else
        {
            // 0x4B4D05: still on a wall or a ceiling, so keep the
            // surface normal and pick which of the two we are on.
            pPlayer->field_A8.vx = pPlayer->mLineInfo.Normal.vx;
            pPlayer->field_A8.vy = pPlayer->mLineInfo.Normal.vy;
            pPlayer->field_A8.vz = pPlayer->mLineInfo.Normal.vz;
            pPlayer->OrientToNormal(true, &normal);
            pPlayer->mActionState = actionWord(PlayerAction::GroundMovement);

            if (pPlayer->field_A8.vy > 0xD48)
            {
                if (pPlayer->field_8E8 != 0 && pPlayer->field_ADA != 0)
                {
                    pPlayer->field_AD8 = 1;
                }

                pPlayer->field_8E8 = 0;
                pPlayer->field_8E9 = 1;
            }
            else
            {
                if (pPlayer->field_8E9 != 0 && pPlayer->field_ADC != 0)
                {
                    pPlayer->field_AD9 = 1;
                }

                pPlayer->field_8E8 = 1;
                pPlayer->field_8E9 = 0;
            }

            if (pushOut != 0)
            {
                pPlayer->mPos += (pPlayer->mUpDirection * pushOut);
                pushOut = 0;
            }

            pPlayer->TidyUpZipWebLandingPosition(0x10);
        }

        // 0x4B4DED
        if (pushOut != 0)
        {
            pPlayer->mPos += (pPlayer->mUpDirection * pushOut);
        }
        return;
    }
}

void PlayerBehaviorController::updateRolling()
{
    CPlayer* pPlayer = &player_;
    {
        i32 frame;

        if (pPlayer->CheckGroundGone() != 0) return;

        pPlayer->mAnimSpeed = 0x20000;

        if ((u16)completedAnimation_ != 0xFFFF)
        {
            u8 *pAnimEntry;
            i32 numFrames;

            frame = pPlayer->mFrame;
            if (pPlayer->CheckJump() == 0) return;

            // Animations[region * 17] (export.h) is really
            // gCostumeRegionEntries[region * 17 + 2]; +0x100 is the
            // animation's frame count.
            pAnimEntry = reinterpret_cast<u8*>(Animations[(i32)pPlayer->mRegion * 17]);
            numFrames = *reinterpret_cast<i32*>(pAnimEntry + 0x100);

            pPlayer->mHeadingDelta = (i32)((u32)(frame << 11) / (u32)(numFrames - 1));
            pPlayer->OrientToNormal(false, &ZeroVector);
            pPlayer->mHeadingDelta = 0;
            return;
        }

        pPlayer->mHeadingDelta = 0x800;
        pPlayer->OrientToNormal(false, &ZeroVector);
        pPlayer->mHeadingDelta = 0;

        if (pPlayer->CheckJump() != 0) return;

        if ((u8)(pPlayer->field_E2E | pPlayer->field_E2D) == 0)
        {
            // 0x4B682F
            pPlayer->SwitchToStandMode();
            return;
        }

        // 0x4B6010
        pPlayer->mActionState = actionWord(PlayerAction::GroundMovement);
        return;
    }
}

void PlayerBehaviorController::updateKnockdownRecovery()
{
    CPlayer* pPlayer = &player_;
    {
        u16 lastAnim = (u16)completedAnimation_;
        u16 anim;

        pPlayer->field_EA6 = 0;

        // 0x54E: the flail-drop chain is running.
        if (PLR_U8(pPlayer, 0x54E) != 0)
        {
            if (lastAnim == 0xAF)
            {
                pPlayer->PlaySingleAnim(0xB1, 0, -1);
            }
            else if (lastAnim == 0xB1)
            {
                pPlayer->mActionState = actionWord(PlayerAction::Falling);
                PLR_U8(pPlayer, 0x54E) = 0;
                return;
            }
        }

        if (PLR_U16(pPlayer, 0xE8E) > 0x1E0)
        {
            gLevelStatus = 8;
        }

        if (pPlayer->mAnim == 0x11D
            && pPlayer->mFrame >= 7
            && (u32)(G_TIMER_RELATED - pPlayer->field_5B4) > 0x3C)
        {
            pPlayer->field_5AC = 5;
            pPlayer->field_5B0 = 0;
            pPlayer->field_5B4 = (i32)G_TIMER_RELATED;
            SFX_PlayPos(0x16, &pPlayer->mPos, 0);
        }

        if (lastAnim == 0xB4 || lastAnim == 0xAE)
        {
            pPlayer->field_36C = 0x1E;
            pPlayer->field_AE5 = 0;
            pPlayer->SwitchToStandMode();
            return;
        }

        if (lastAnim == 0xAA || lastAnim == 0xBB || lastAnim == 0xB5
            || lastAnim == 0x11D)
        {
            // 0x4B6597
            pPlayer->SwitchToStandMode();
            return;
        }

        anim = pPlayer->mAnim;
        if (anim != 0xAF && anim != 0xB0) return;
        if ((pPlayer->mCollision & 2) == 0) return;

        pPlayer->PlaySingleAnim(0xB2, 0, -1);
        if (pPlayer->mHealth > 0) return;

        pPlayer->SwitchToDeathMode(false);
        return;
    }
}

} // namespace player
} // namespace spidey
