#include "game/player/player_frame_preparation.h"
#include "compatibility/player_behavior_bridge.h"

namespace spidey {
namespace player {

void PlayerFramePreparation::updateLookaroundInput()
{
    CPlayer* pPlayer = &player_;

    pPlayer->ReadAnalogueInput();

    {
        u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
        u8 padFire;
        i32 usedDirs;

        if (pPad[0x100] == 0)
        {
            pPlayer->field_E8D = 1;
        }

        usedDirs = pPlayer->field_8E4;
        if (usedDirs != 0)
        {
            if ((usedDirs & 1) != 0 && pPlayer->field_E2D <= 0)
            {
                pPlayer->field_8E4 = usedDirs & ~1;
            }

            usedDirs = pPlayer->field_8E4;
            if ((usedDirs & 2) != 0 && pPlayer->field_E2D >= 0)
            {
                pPlayer->field_8E4 = usedDirs & ~2;
            }

            usedDirs = pPlayer->field_8E4;
            if ((usedDirs & 4) != 0 && pPlayer->field_E2E <= 0)
            {
                pPlayer->field_8E4 = usedDirs & ~4;
            }

            usedDirs = pPlayer->field_8E4;
            if ((usedDirs & 8) != 0 && pPlayer->field_E2E >= 0)
            {
                pPlayer->field_8E4 = usedDirs & ~8;
            }
        }

        if (pPlayer->field_550 != 0 && pPad[0x70] == 0)
        {
            pPlayer->field_550 = 0;
        }

        // only these states let the lookaround button be held
        if ((pPlayer->mActionState & 0x3EBE3FCE) != 0)
        {
            pPad[0x40] = 0;
        }
        else if ((pPlayer->mActionState & 0x1C000) != 0)
        {
            if (pPlayer->field_8EA == 0)
            {
                pPad[0x40] = 0;
            }
        }
        else if (PLR_I32(pPlayer, 0xE48) != 0 || pPlayer->mGroundContactGraceUpdates != 4)
        {
            pPad[0x40] = 0;
        }

        padFire = pPad[0x40];

        // 0x8EB: lookaround allowed this tick.
        if (padFire != 0 && PLR_U8(pPlayer, 0x8EB) != 0)
        {
            pPlayer->field_56C += pPlayer->mElapsedTicks;
        }
        else
        {
            pPlayer->field_56C = 0;
        }

        if ((u32)pPlayer->field_56C > 0xF)
        {
            if (pPlayer->field_8EA == 0)
            {
                ECameraMode mode = G_CAMERA_LIST->mCameraMode;

                if (mode != CAMERAMODE_START
                    && mode != CAMERAMODE_FAR
                    && mode != CAMERAMODE_OVERHEAD
                    && mode != CAMERAMODE_NOTHING)
                {
                    pPlayer->EnterLookaroundMode();
                }
            }
            else if (padFire == 0)
            {
                pPlayer->ExitLookaroundMode();
            }
        }
        else if (pPlayer->field_8EA != 0 && padFire == 0)
        {
            pPlayer->ExitLookaroundMode();
        }

        if (pPlayer->field_8EA != 0 && (pPlayer->field_CE4 | pPlayer->field_CB4) == 0)
        {
            u8 pressed;

            if (*gPracticeDifficultyFlag != 0)
            {
                pressed = pPad[0x101];
            }
            else
            {
                pressed = pPad[0x71];
            }

            if (pressed != 0 && pPlayer->field_E64 == 0)
            {
                pPad[0x101] = 0;
                pPlayer->field_54F = 1;
                pPad[0x71] = 0;
                pPlayer->field_2C1 = 0;
                // 0x231: cleared together with field_2C1 here.
                PLR_U8(pPlayer, 0x231) = 0;
            }

            if (pPlayer->field_E2D != 0 && pPlayer->field_8E0 == 0)
            {
                i32 delta = ((pPlayer->field_E2D >> 3) * pPlayer->field_8F0 * pPlayer->mElapsedTicks) >> 8;
                i32 was = *gLookaroundActiveCamAngle;
                i32 now;

                // G_GAMESTATE[14] flips the pitch direction (look inversion).
                if (G_GAMESTATE[14] == 0)
                {
                    now = was + delta;
                }
                else
                {
                    now = was - delta;
                }

                *gLookaroundActiveCamAngle = now;
                if ((now < 0 ? -now : now) > 0x400)
                {
                    *gLookaroundActiveCamAngle = was;
                }
            }

            if (pPlayer->field_E2E != 0 && pPlayer->field_8E0 == 0)
            {
                i32 delta = ((pPlayer->field_E2E >> 3) * pPlayer->field_8F0 * pPlayer->mElapsedTicks) >> 8;
                i32 now = *gLookaroundYawOffset + delta;

                *gLookaroundYawOffset = now;
                if ((now < 0 ? -now : now) > 0x400)
                {
                    i32 clamped = (now > 0x400) ? (now - 0x400) : (now + 0x400);
                    i16 heading = pPlayer->GetEffectiveHeading();

                    if (pPlayer->field_8E9 != 0)
                    {
                        pPlayer->SetTargetTorsoAngle((i16)((heading - clamped) & 0xFFF), false);
                    }
                    else
                    {
                        pPlayer->SetTargetTorsoAngle((i16)((clamped + heading) & 0xFFF), false);
                    }
                }
            }
        }
        else
        {
            *gLookaroundYawOffset = 0;
            *gLookaroundPitchSmoothed = 0;
            *gLookaroundYawSmoothed = 0;
        }
    }
}

} // namespace player
} // namespace spidey
