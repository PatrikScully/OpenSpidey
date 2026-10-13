#include "game/player/player_frame_preparation.h"
#include "compatibility/player_behavior_bridge.h"

namespace spidey {
namespace player {

void PlayerFramePreparation::updateCostumeVisibility()
{
    CPlayer* pPlayer = &player_;
    u32 keyMappingA;
    u32 keyMappingB;

    PCINPUT_GetKeyboardMappingForAction(0x40, &keyMappingA);
    PCINPUT_GetKeyboardMappingForAction(0x100, &keyMappingB);

    // PC-only: with the "suit 5" costume the two mapped keys together toggle
    // field_57C, which drives the 0x800 CItem flag right below.
    if (G_CURRENTSUIT == 5
        && PCINPUT_IsKeyPressed((u8)keyMappingA, 1) != 0
        && PCINPUT_IsKeyPressed((u8)keyMappingB, 1) != 0
        && pPlayer->mSynthesizedInputActive == 0
        && pPlayer->field_E18 == 0)
    {
        i8 wasSet = pPlayer->field_57C;

        reinterpret_cast<u8*>(pPlayer->field_E0C)[0x51] = 0;
        pPlayer->field_57C = (i8)(wasSet == 0);
        pPlayer->field_570 = 0;
        SFX_PlayPos(0x1F, &pPlayer->mPos, 0);
    }

    if (pPlayer->field_57C != 0)
    {
        pPlayer->mFlags |= 0x800;
    }
    else
    {
        pPlayer->mFlags &= 0xF7FF;
    }
}

void PlayerFramePreparation::updateWaterEffect()
{
    CPlayer* pPlayer = &player_;

    if (gWaterEffect == 1)
    {
        G_DB_SKY_COLOR = G_DB_SKY_COLOR_TARGET;
        Db_UpdateSky();
        gWaterEffect = 0;
    }
}

void PlayerFramePreparation::updateDamageTimers()
{
    CPlayer* pPlayer = &player_;

    if (pPlayer->field_36C != 0)
    {
        pPlayer->field_36C -= pPlayer->mElapsedTicks;
        if (pPlayer->field_36C < 0)
        {
            pPlayer->field_36C = 0;
        }
    }

    if (pPlayer->field_534 != 0)
    {
        pPlayer->field_534 -= pPlayer->mElapsedTicks;
        if (pPlayer->field_534 < 0)
        {
            pPlayer->field_534 = 0;
        }
    }

    // electrocution timer: keeps the pad buzzing while it runs down.
    if (pPlayer->field_50C != 0)
    {
        if (*gSaveGameVibration != 0)
        {
            Pad_ActuatorOn(0, (u16)pPlayer->field_50C, 1, 0xDB);
        }

        pPlayer->field_50C -= pPlayer->mElapsedTicks;
        if (pPlayer->field_50C <= 0)
        {
            pPlayer->field_50C = 0;
            Effects_UnElectrify(pPlayer);
        }
    }
}

void PlayerFramePreparation::updateSenseEffect()
{
    CPlayer* pPlayer = &player_;

    // spidey-sense buzz timer (field_ECC), its SFX handle (field_ED0) and the
    // extra body part it shows (field_ED4).
    if (pPlayer->field_ECC != 0)
    {
        i32 buzzLeft = pPlayer->field_ECC - pPlayer->mElapsedTicks;

        pPlayer->field_ECC = buzzLeft;
        if (buzzLeft <= 0)
        {
            pPlayer->field_ECC = 0;
        }
        else
        {
            CBody *pBuzz;

            // 0xEC8: the buzz kind. 1 also triggers the water effect.
            if (PLR_I32(pPlayer, 0xEC8) == 1)
            {
                gWaterEffect = 1;
            }

            if (pPlayer->field_ED0 == 0)
            {
                pPlayer->field_ED0 = SFX_Play(0x1A, 0x2000, 0);
            }

            if ((i32)(Pad_GetActuatorTime(0, 1) & 0xFFFF) < buzzLeft
                && *gSaveGameVibration != 0)
            {
                Pad_ActuatorOn(0, (u16)pPlayer->field_ECC, 1, 0x3C);
            }

            if (Mem_RecoverPointer(&pPlayer->field_ED4) == 0)
            {
                pBuzz = new CBody();
                pPlayer->field_ED4 = Mem_MakeHandle(pBuzz);
                pBuzz->InitItem("items");
                pBuzz->mFlags |= 0x200;
                pBuzz->mModel = 4;
                pBuzz->mType = 0x1F8;
                pBuzz->AttachTo(reinterpret_cast<CBody**>(&SpideyAdditionalBodyPartsList));

                if (G_PULSATING_HEAD_FLAG != 0)
                {
                    pBuzz->mScale.vx = (i16)(pBuzz->mScale.vx << 1);
                    pBuzz->mScale.vy = (i16)(pBuzz->mScale.vy << 1);
                    pBuzz->mScale.vz = (i16)(pBuzz->mScale.vz << 1);
                }

                SpideyAI0_ApplyCheatScale(pBuzz, 0);
            }
        }
    }

    if (pPlayer->field_ECC == 0)
    {
        CBody *pBuzz;

        if (pPlayer->field_ED0 != 0)
        {
            SFX_Stop(pPlayer->field_ED0);
            pPlayer->field_ED0 = 0;
        }

        pBuzz = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_ED4));
        if (pBuzz != 0)
        {
            pBuzz->DeleteFrom(reinterpret_cast<CBody**>(&SpideyAdditionalBodyPartsList));
            delete pBuzz;
            pPlayer->field_ED4 = Mem_MakeHandle(0);
        }
    }
}

void PlayerFramePreparation::updateFistBonus()
{
    CPlayer* pPlayer = &player_;

    // the two fists (field_5B8), sized from the combo bonus timer field_5B0.
    if (pPlayer->field_5AC != 0)
    {
        i32 fist;

        if (pPlayer->field_5B0 < 0x2000)
        {
            pPlayer->field_5B0 += pPlayer->mElapsedTicks * 384;
        }
        else
        {
            pPlayer->field_5B0 = 0x2000;
        }

        for (fist = 0; fist < 2; fist++)
        {
            CBody *pFist = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_5B8[fist]));

            if (pFist == 0)
            {
                pFist = new CBody();
                pPlayer->field_5B8[fist] = Mem_MakeHandle(pFist);
                pFist->InitItem("items");
                pFist->mModel = 8;
                pFist->mType = 0x1F9;
                pFist->AttachTo(reinterpret_cast<CBody**>(&SpideyAdditionalBodyPartsList));
                SpideyAI0_ApplyCheatScale(pFist, 1);
                pFist->mFlags |= 0x200;
            }

            if (pPlayer->field_5B0 < 0x2000)
            {
                i16 scale = (i16)(pPlayer->field_5B0 / 2);

                pFist->mFlags |= 0x200;
                pFist->mScale.vx = scale;
                pFist->mScale.vy = scale;
                pFist->mScale.vz = scale;
                SpideyAI0_ApplyCheatScale(pFist, 1);
            }
        }
    }
    else if (pPlayer->field_5B0 > 0)
    {
        i32 half;
        i32 fist;

        pPlayer->field_5B0 -= pPlayer->mElapsedTicks * 512;
        if (pPlayer->field_5B0 < 0)
        {
            pPlayer->field_5B0 = 0;
        }

        half = pPlayer->field_5B0 / 2;

        for (fist = 0; fist < 2; fist++)
        {
            CBody *pFist = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_5B8[fist]));

            if (pFist != 0)
            {
                if (half != 0)
                {
                    pFist->mFlags |= 0x200;
                    pFist->mScale.vx = (i16)half;
                    pFist->mScale.vy = (i16)half;
                    pFist->mScale.vz = (i16)half;
                }
                else
                {
                    pFist->DeleteFrom(reinterpret_cast<CBody**>(&SpideyAdditionalBodyPartsList));
                    delete pFist;
                    // the original only clears the pointer half of the handle
                    pPlayer->field_5B8[fist].pWhatever = 0;
                }
            }
        }
    }
}

} // namespace player
} // namespace spidey
