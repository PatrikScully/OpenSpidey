#include "game/player/player_frame_finalizer.h"
#include "compatibility/player_behavior_bridge.h"

namespace spidey {
namespace player {

void PlayerFrameFinalizer::buildPose()
{
    CPlayer* pPlayer = &player_;

    // --- build this frame's pose, 0x4B7D5A ---
    if ((pPlayer->mFlags & 4) != 0)
    {
        pPlayer->ApplyPose(gSpideyFixedPose);
    }
    else
    {
        M3d_BuildTransform(pPlayer);
    }

    pPlayer->DoShadowCheck();

    if (pPlayer->field_94D != 0)
    {
        M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pPlayer->field_91C), pPlayer, 6);
        M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pPlayer->field_928), pPlayer, 5);
        M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pPlayer->field_934), pPlayer, 1);
        M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pPlayer->field_940), pPlayer, 0);
    }

    pPlayer->UpdateTrails();

    if (pPlayer->field_584 != 0)
    {
        CVector hookPos;

        M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hookPos), pPlayer, 5);
        pPlayer->field_584->SetPos(hookPos);
    }

    if (pPlayer->field_588 != 0)
    {
        CVector hookPos;

        M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hookPos), pPlayer, 6);
        pPlayer->field_588->SetPos(hookPos);
    }
}

void PlayerFrameFinalizer::updateHeadTracking()
{
    CPlayer* pPlayer = &player_;

    // --- head look-at, 0x4B7E32 ---
    // 0xA80: "the head tracks something this tick".
    if (PLR_U8(pPlayer, 0xA80) != 0)
    {
        CVector target;
        bool bHaveTarget = true;

        if (pPlayer->field_E00 != 0)
        {
            Trig_GetPosition(&target, pPlayer->field_E00);
        }
        else if (pPlayer->field_8E8 != 0
            || pPlayer->field_8E9 != 0
            || pPlayer->field_DCC == 0
            || pPlayer->field_8EA != 0)
        {
            bHaveTarget = false;
        }
        else
        {
            target = pPlayer->field_DCC->mPos;
        }

        if (bHaveTarget)
        {
            CVector headHook;
            CVector look;

            M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&headHook), pPlayer, 8);

            look.vx = (target.vx - headHook.vx) >> 12;
            look.vy = (target.vy - headHook.vy) >> 12;
            look.vz = (target.vz - headHook.vz) >> 12;

            gte_SetRotMatrix(reinterpret_cast<MATRIX*>(
                reinterpret_cast<char*>(pPlayer) + 0x89C));
            gte_ldlvl(reinterpret_cast<VECTOR*>(&look));
            gte_rtir();
            gte_stlvnl(reinterpret_cast<VECTOR*>(&look));
            look <<= 12;

            Utils_CalcAim(reinterpret_cast<CSVector*>(
                    reinterpret_cast<char*>(pPlayer) + 0xE04),
                &ZeroVector, &look);

            // clamp the head yaw out of the 0x200..0xE00 dead zone
            {
                i16 *pHeadYaw = reinterpret_cast<i16*>(
                    reinterpret_cast<char*>(pPlayer) + 0xE04);
                i16 *pHeadPitch = reinterpret_cast<i16*>(
                    reinterpret_cast<char*>(pPlayer) + 0xE06);
                i32 yawVal = *pHeadYaw;
                i32 pitchVal = *pHeadPitch;

                if (yawVal > 0x800)
                {
                    if (yawVal < 0xE00) *pHeadYaw = 0xE00;
                }
                else if (yawVal > 0x200)
                {
                    *pHeadYaw = 0x200;
                }

                if (pitchVal > 0x800)
                {
                    if (pitchVal < 0xD00) *pHeadPitch = 0xD00;
                }
                else if (pitchVal > 0x300)
                {
                    *pHeadPitch = 0x300;
                }
            }
        }
    }
    else
    {
        PLR_I16(pPlayer, 0xE04) = 0;
        PLR_I16(pPlayer, 0xE06) = 0;
    }

    // --- ease the head joint towards the target angles, 0x4B7FB9 ---
    if (pPlayer->mpJoints != 0)
    {
        i16 *pJointAngles = reinterpret_cast<i16*>(
            reinterpret_cast<char*>(pPlayer->mpJoints) + 0x24);
        i32 axis;

        for (axis = 0; axis < 2; axis++)
        {
            i16 cur = pJointAngles[axis];
            i32 diff = (i32)PLR_I16(pPlayer, 0xE04 + axis * 2) - (i32)cur;

            if (diff > 0x800)
            {
                diff -= 0x1000;
            }
            else if (diff < -0x800)
            {
                diff += 0x1000;
            }

            if (diff != 0)
            {
                if (diff > 0x40)
                {
                    diff = 0x40;
                }
                else if (diff < -0x40)
                {
                    diff = -0x40;
                }

                pJointAngles[axis] = (i16)(diff + cur);
            }
        }
    }

    if (pPlayer->mHealth < 0)
    {
        pPlayer->mHealth = 0;
    }
}

void PlayerFrameFinalizer::updateAdditionalBodyParts(PlayerHandHooks& hooks)
{
    CPlayer* pPlayer = &player_;

    // --- the spidey-sense buzz and the two fists, 0x4B806B ---
    M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hooks.left), pPlayer, 1);
    M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hooks.right), pPlayer, 0);

    {
        CBody *pBuzz = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_ED4));

        if (pBuzz != 0)
        {
            i32 pulse;

            M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pBuzz->mPos), pPlayer, 8);
            gM3dUtils_GetPartAngles(pPlayer, 7, &pBuzz->mAngles, 0);

            pulse = Sine(((i32)G_TIMER_RELATED << 6) & 0xFFF);
            if (pulse < 0) pulse = -pulse;
            pulse = pulse / 2 + 0x800;

            pBuzz->mScale.vx = (i16)pulse;
            pBuzz->mScale.vy = (i16)pulse;
            pBuzz->mScale.vz = (i16)pulse;

            if (G_PULSATING_HEAD_FLAG != 0)
            {
                pBuzz->mScale.vx = (i16)(pBuzz->mScale.vx << 1);
                pBuzz->mScale.vy = (i16)(pBuzz->mScale.vy << 1);
                pBuzz->mScale.vz = (i16)(pBuzz->mScale.vz << 1);
            }

            SpideyAI0_ApplyCheatScale(pBuzz, 0);
        }
    }

    {
        i32 fist;

        for (fist = 0; fist < 2; fist++)
        {
            CBody *pFist = reinterpret_cast<CBody*>(
                Mem_RecoverPointer(&pPlayer->field_5B8[fist]));

            if (pFist != 0)
            {
                i32 dist;

                M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pFist->mPos),
                    pPlayer, (fist != 0) ? 3 : 4);
                gM3dUtils_GetPartAngles(pPlayer, (fist != 0) ? 5 : 10,
                    &pFist->mAngles, 0);

                dist = spidey::math::approximateDistance(pFist->mPos, G_CAMERA_LIST->mPos);
                if (dist < 0x100)
                {
                    pFist->mFlags |= 0xC00;
                    if (dist < 0xAA)
                    {
                        pFist->mRGB = 0;
                    }
                    else
                    {
                        u32 level = (u32)(dist - 0xAA);

                        pFist->mRGB = (((level << 8) | level) << 8) | level;
                    }
                }
                else
                {
                    pFist->mFlags &= 0xF3FF;
                    pFist->mRGB = 0x808080;
                }

                if (pPlayer->field_5E8 != 0)
                {
                    u32 blue = pFist->mRGB & 0xFF;

                    pFist->mFlags |= 0x400;
                    pFist->mRGB = (u32)((Rnd((i32)blue) / 2) << 8) | blue;
                }
            }
        }
    }
}

void PlayerFrameFinalizer::updateFlamingFists(const PlayerHandHooks& hooks)
{
    CPlayer* pPlayer = &player_;

    // --- the flaming-fists effect, 0x4B826A ---
    if (pPlayer->field_5E8 != 0 && pPlayer->field_5AC != 0)
    {
        u16 anim = pPlayer->mAnim;
        u32 args[2];
        i32 count;
        i32 i;

        if (anim == 0x66 || anim == 0x68 || anim == 0x6A || anim == 0xD5)
        {
            count = 3;
        }
        else
        {
            count = (Rnd(4) != 0) ? 0 : 1;
        }

        args[0] = (u32)&hooks.left;
        args[1] = 0x100;
        for (i = 0; i < count; i++)
        {
            Reloc_CallUserFunction(gFistEffectNameLeft, 5, args, 0);
        }

        if (anim == 0x64 || anim == 0x6A || anim == 0xD5)
        {
            count = 3;
        }
        else
        {
            count = (Rnd(4) != 0) ? 0 : 1;
        }

        args[0] = (u32)&hooks.right;
        for (i = 0; i < count; i++)
        {
            Reloc_CallUserFunction(gFistEffectNameRight, 5, args, 0);
        }
    }
}

void PlayerFrameFinalizer::updateAttachments(PlayerHandHooks& hooks)
{
    CPlayer* pPlayer = &player_;

    // --- swing line, web and held object render ends, 0x4B8346 ---
    if (pPlayer->field_E64 != 0)
    {
        if (pPlayer->mAnim == 0x118)
        {
            hooks.left -= (pPlayer->mForwardDirection * 8);
            gCSwinger_SetRenderEnd(pPlayer->field_E64, hooks.left);
        }
        else
        {
            hooks.right -= (pPlayer->mForwardDirection * 8);
            gCSwinger_SetRenderEnd(pPlayer->field_E64, hooks.right);
        }
    }

    if (pPlayer->field_E6C != 0)
    {
        CWeb *pWeb = reinterpret_cast<CWeb*>(pPlayer->field_E6C);
        CVector firePos;

        if (pWeb->field_102 == 0)
        {
            firePos = hooks.right;
        }
        else if (pWeb->field_102 == 1)
        {
            firePos = hooks.left;
        }
        else
        {
            firePos = hooks.left + ((hooks.right - hooks.left) / 2);
        }

        gCWeb_SetFirePos(pWeb, firePos);
    }

    if (pPlayer->mHeldObject != 0)
    {
        CManipOb *pHeld = pPlayer->mHeldObject;
        CVector mid = hooks.left + ((hooks.right - hooks.left) / 2);
        CVector dir = (mid - pPlayer->mPos) >> 6;
        i32 reach;

        VectorNormal(reinterpret_cast<VECTOR*>(&dir),
            reinterpret_cast<VECTOR*>(&dir));

        reach = (i32)pHeld->field_108;
        pHeld->mPos = (mid + (dir * reach)) + (pPlayer->mForwardDirection * 0x20);
        pHeld->mAngles.vy = (i16)(pHeld->mAngles.vy + (i16)pPlayer->mHeadingDelta);
    }
}

void PlayerFrameFinalizer::syncFistsAndFinishInput(u8* input)
{
    CPlayer* pPlayer = &player_;
    u8* pPad = input;

    // --- keep the fists model in step with the animation, 0x4B8638 ---
    // 0xAB4: the animation id the current fists model was built for.
    if (pPlayer->mAnim != (u16)PLR_U8(pPlayer, 0xAB4))
    {
        // gFistsData[anim] >> 14 is the fists variant, see
        // CPlayer::SortFistsData in spidey.cpp.
        u16 *pFistsData = reinterpret_cast<u16*>(0x00555A14);

        pPlayer->CreateFists((u8)(pFistsData[pPlayer->mAnim] >> 14));
        PLR_U8(pPlayer, 0xAB4) = (u8)pPlayer->mAnim;
    }

    pPlayer->SetupLookaroundCamera();
    pPlayer->DrawOffscreenSpideySenseIndicatorList();

    // clear the pad's per-frame edge flags, skipping entries 14 and 15
    {
        i32 i;

        for (i = 0; i < 20; i++)
        {
            if (i != 14 && i != 15)
            {
                pPad[i * 16 + 1] = 0;
                PLR_U8(pPlayer, 0x1C1 + i * 16) = 0;
            }
        }
    }
}

} // namespace player
} // namespace spidey
