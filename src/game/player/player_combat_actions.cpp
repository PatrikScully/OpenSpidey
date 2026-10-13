#include "game/player/player_behavior_controller.h"
#include "game/player/player_action.h"
#include "compatibility/player_behavior_bridge.h"

namespace spidey {
namespace player {

void PlayerBehaviorController::updatePunchCombo()
{
    CPlayer* pPlayer = &player_;
    {
        u16 attackKind;
        i32 comboState;
        CBody *pAimAt;

        pPlayer->field_EA6 = 0;
        if (pPlayer->CheckGroundGone() != 0) return;
        if (pPlayer->CheckJump() != 0) return;

        attackKind = pPlayer->field_8FC;
        if (attackKind == 0 || attackKind == 1)
        {
            u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

            if (pPad[0x110] != 0
                && (pPad[0x120] != 0 || pPad[0x130] != 0)
                && (u32)(G_TIMER_RELATED - pPlayer->field_898) <= 4)
            {
                CBody *pTarget = pPlayer->SelectTargetBaddy(0xBE, -0x1000, 0x1000, 0);

                pPlayer->field_DD8 = Mem_MakeHandle(pTarget);
                pPlayer->field_DE0 = (i32)G_TIMER_RELATED;
                pPlayer->mActionState = actionWord(PlayerAction::ZipDive);
                pPlayer->PlaySingleAnim(0x78, 0, -1);
                return;
            }
        }

        comboState = gCPlayer_UpdateAndTrackCombo(pPlayer);
        if (comboState == 0)
        {
            pPlayer->mHeadingDelta = 0;
            pPlayer->SwitchToStandMode();
            return;
        }

        if (comboState == 2)
        {
            pAimAt = pPlayer->field_DCC;
            pPlayer->field_8F8 = 1;
            pPlayer->field_552 = 0;
            pPlayer->mActionState = actionWord(PlayerAction::WebAttackWindup);
            pPlayer->field_8ED = 0;
        }
        else if (comboState == 3)
        {
            pAimAt = pPlayer->field_DCC;
            pPlayer->field_8F8 = 2;
            pPlayer->field_552 = 0;
            pPlayer->mActionState = actionWord(PlayerAction::WebFiring);

            if (pPlayer->field_E2E > 0)
            {
                pPlayer->field_544 = 2;
            }
            else if (pPlayer->field_E2E < 0)
            {
                pPlayer->field_544 = 1;
            }
            else
            {
                pPlayer->field_544 = 0;
            }
            pPlayer->field_8ED = 0;
        }
        else if (comboState == 6)
        {
            pAimAt = pPlayer->field_DCC;
            pPlayer->field_8F8 = 2;
            pPlayer->field_552 = 0;
            pPlayer->mActionState = actionWord(PlayerAction::WebAttachWindup);
            pPlayer->field_8ED = 0;
        }
        else
        {
            return;
        }

        if (pAimAt == 0)
        {
            pPlayer->LockTargetTorsoAngle();
        }
        else
        {
            pPlayer->SetTargetTorsoAngleToThisPoint(&pAimAt->mPos);
        }
        PLR_U8(pPlayer, 0xAE) &= 0xFE;
        return;
    }
}

void PlayerBehaviorController::updateSmashKick()
{
    CPlayer* pPlayer = &player_;
    {
        CVector normal;

        pPlayer->field_EA6 = 0;

        if (pPlayer->field_8DC != 0)
        {
            if (pPlayer->mAnim != 0) return;

            pPlayer->field_8DC = 0;
            // 0x4B6589
            pPlayer->field_AE4 = 1;
            pPlayer->field_AE5 = 0;
            pPlayer->SwitchToStandMode();
            return;
        }

        if ((pPlayer->mAnim == 0x81 || pPlayer->mAnim == 0x85)
            && pPlayer->mAnimFinished != 0
            && pPlayer->field_584 == 0)
        {
            pPlayer->CreateJumpingSmashKickTrail();
        }

        if ((pPlayer->field_504 & 0x1000000) != 0
            && (u32)(G_TIMER_RELATED - pPlayer->field_500) < 6)
        {
            // bounced off: flip upright, play the recover animation and
            // drop into the falling state.
            pPlayer->field_A8.vx = 0;
            pPlayer->field_A8.vy = (i16)0xF000;
            pPlayer->field_A8.vz = 0;
            pPlayer->OrientToNormal(false, &ZeroVector);
            pPlayer->PlaySingleAnim(0xAF, 0, -1);
            pPlayer->DestroyJumpingSmashKickTrail();
            pPlayer->mVel.vx = 0;
            pPlayer->mVel.vy = 0;
            pPlayer->mVel.vz = 0;
            pPlayer->field_E8C = 0;
            pPlayer->mActionState = actionWord(PlayerAction::Falling);
            return;
        }

        normal = (pPlayer->field_8CC - pPlayer->mPos) >> 12;

        if ((pPlayer->mCollision & 2) == 0 && normal.vy > 0)
        {
            // still flying at the target: steer straight at it.
            CVector up(-normal.vx, 0, -normal.vz);

            VectorNormal(reinterpret_cast<VECTOR*>(&normal),
                reinterpret_cast<VECTOR*>(&normal));
            VectorNormal(reinterpret_cast<VECTOR*>(&up),
                reinterpret_cast<VECTOR*>(&up));

            pPlayer->field_A8.vx = (i16)-normal.vx;
            pPlayer->field_A8.vy = (i16)-normal.vy;
            pPlayer->field_A8.vz = (i16)-normal.vz;
            pPlayer->OrientToNormal(true, &up);

            if (pPlayer->field_8D8 != 0)
            {
                pPlayer->mVel.vx = 0;
                pPlayer->mVel.vy = 0x30000;
                pPlayer->mVel.vz = 0;
                return;
            }

            {
                CVector vel = normal * 0x60;
                pPlayer->mVel.vx = vel.vx;
                pPlayer->mVel.vy = vel.vy;
                pPlayer->mVel.vz = vel.vz;
            }
            return;
        }

        // 0x4B651B. Arrived (or hit something): land the kick.
        pPlayer->field_A8.vx = 0;
        pPlayer->field_A8.vy = (i16)0xF000;
        pPlayer->field_A8.vz = 0;
        pPlayer->OrientToNormal(false, &ZeroVector);
        pPlayer->mVel.vx = 0;
        pPlayer->mVel.vy = 0;
        pPlayer->mVel.vz = 0;
        pPlayer->DestroyJumpingSmashKickTrail();
        pPlayer->PlaySingleAnim(pPlayer->mAnim == 0x81 ? 0x82 : 0x86, 0, -1);

        if (pPlayer->field_8C4 - pPlayer->field_8C8 < 0x78)
        {
            pPlayer->field_8DC = 0x29A;
            return;
        }

        pPlayer->field_8DC = 0;
        // 0x4B6589
        pPlayer->field_AE4 = 1;
        pPlayer->field_AE5 = 0;
        pPlayer->SwitchToStandMode();
        return;
    }
}

} // namespace player
} // namespace spidey
