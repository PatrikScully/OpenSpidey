#include "game/player/player_frame_finalizer.h"
#include "core/wrapping_math.h"
#include "compatibility/player_behavior_bridge.h"

namespace spidey {
namespace player {

void PlayerFrameFinalizer::update()
{
    u8* input = reinterpret_cast<u8*>(player_.field_E0C);
    SLineInfo collisionQuery;
    updateCameraFacing(input);
    const i32 cameraYaw = updateTorsoAim();
    updateHeadingAndGravity();
    updateMovementVelocity(cameraYaw);
    rideSupportBody(collisionQuery);
    updateVerticalImpulse();
    resolveActorContacts();
    collectPowerUps();
    separateStandingPlayerFromActors();
    sweepTriggerZones(collisionQuery);
    buildPose();
    updateHeadTracking();
    PlayerHandHooks hooks;
    updateAdditionalBodyParts(hooks);
    updateFlamingFists(hooks);
    updateAttachments(hooks);
    syncFistsAndFinishInput(input);
}

void PlayerFrameFinalizer::updateHeadingAndGravity()
{
    CPlayer* pPlayer = &player_;
    i16 heading;
    i32 headingBefore;
    i32 turnLeft;

    // --- heading and the scripted turn, 0x4B717F ---
    heading = pPlayer->GetEffectiveHeading();
    turnLeft = pPlayer->mHeadingTurnTicksRemaining;
    pPlayer->mAngles.vy = heading;
    headingBefore = (i32)heading;
    pPlayer->mHeadingDelta = headingBefore;

    if (turnLeft != 0)
    {
        i32 dt = pPlayer->mElapsedTicks;

        if (turnLeft > dt)
        {
            i16 step = (i16)(pPlayer->mHeadingTurnStep * dt);

            pPlayer->mAngles.vy = (i16)(((i32)step + headingBefore) & 0xFFF);
            pPlayer->mHeadingTurnTicksRemaining = turnLeft - dt;
        }
        else
        {
            // 0xDF0: the angle the scripted turn ends on.
            pPlayer->mHeadingTurnTicksRemaining = 0;
            pPlayer->mAngles.vy = spidey::core::signedHalfWord(static_cast<u32>(pPlayer->mTargetHeading));
        }

        if (pPlayer->field_A8.vy > 0xD48)
        {
            i32 idx = (headingBefore - (i32)pPlayer->mAngles.vy) & 0xFFF;
            CVector dir(Sine(idx), 0, Cosine(idx));

            gte_SetRotMatrix(&pPlayer->mTransform);
            gte_ldlvl(reinterpret_cast<VECTOR*>(&dir));
            gte_rtir();
            gte_stlvnl(reinterpret_cast<VECTOR*>(&pPlayer->mOrientationReferenceDirection));
        }
    }

    pPlayer->mHeadingDelta = (i32)pPlayer->mAngles.vy - pPlayer->mHeadingDelta;

    pPlayer->mAcc.vz = 0;
    pPlayer->mAcc.vx = 0;
    if (pPlayer->mActionState == 0x1000000)
    {
        // dead store, the next test overwrites it either way
        pPlayer->mAcc.vy = 0;
    }
    if (pPlayer->mIsCrawling != 0 || pPlayer->mAnim == 0x115)
    {
        pPlayer->mAcc.vy = 0;
    }
    else
    {
        pPlayer->mAcc.vy = 0xA000;
    }
}

void PlayerFrameFinalizer::updateMovementVelocity(i32 cameraYaw)
{
    CPlayer* pPlayer = &player_;
    i32 cameraHeading = cameraYaw;
    i16 heading;
    CVector localVelocity;
    i32 velocityChanged;

    // --- move input into an acceleration, 0x4B72AD ---
    velocityChanged = 0;
    {
        MATRIX identityA;
        MATRIX identityB;

        if (pPlayer->field_8E8 != 0)
        {
            gte_SetRotMatrix(reinterpret_cast<MATRIX*>(
                reinterpret_cast<char*>(pPlayer) + 0x89C));
        }
        else
        {
            print_if_false(G_CAMERA_LIST != 0, "no camera");
            M3dMaths_SetIdentityRotation(&identityA);
            M3dMaths_SetIdentityRotation(&identityB);
            gte_SetRotMatrix(&identityB);
        }

        localVelocity = (pPlayer->mVel >> 6);
        gte_ldlvl(reinterpret_cast<VECTOR*>(&localVelocity));
        gte_rtir();
        gte_stlvnl(reinterpret_cast<VECTOR*>(&localVelocity));
        localVelocity <<= 6;

        {
            // 0xEF4/0xEF8: the "perpendicularising" run around a boss.
            u8 wasPerpendicularising = pPlayer->mArenaConstraintActive;
            i32 moveMag = 0;
            i32 moveScale = 0;
            i32 yaw = 0;

            pPlayer->mArenaConstraintActive = 0;

            if (pPlayer->field_8EA == 0
                && pPlayer->field_AE4 != 0
                && (pPlayer->mActionState & 0xBFF7F8C1) == 0)
            {
                if ((u8)(pPlayer->field_E2E | pPlayer->field_E2D) != 0)
                {
                    i32 sideways = ((i32)pPlayer->field_E2E << 12) / 64;
                    i32 forwards = ((i32)pPlayer->field_E2D << 12) / 64;

                    if (sideways < 0) sideways = -sideways;
                    if (forwards < 0) forwards = -forwards;

                    moveMag = sideways + forwards;
                    if (moveMag > 0x1000)
                    {
                        moveMag = 0x1000;
                    }

                    if (pPlayer->mAnim == 0x32 || pPlayer->mAnim == 0x33
                        || pPlayer->mAnim == 0x34)
                    {
                        i32 speed = ((moveMag >> 6) << 16) >> 6;

                        pPlayer->mAnimSpeed = speed;
                        if (speed < 0x8000)
                        {
                            pPlayer->mAnimSpeed = 0x8000;
                        }
                    }

                    if (pPlayer->mIsCrawling != 0)
                    {
                        moveScale = 0x1A;
                    }
                    else if (pPlayer->mHeldObject != 0)
                    {
                        moveScale = ((pPlayer->mHeldObject->field_10C >> 3) & 1) ? 0x0A : 0x18;
                    }
                    else
                    {
                        moveScale = 0x28;
                        moveMag = 0x1000;
                    }

                    if (pPlayer->field_8E8 != 0)
                    {
                        i32 idx;
                        i32 c;

                        heading = pPlayer->GetEffectiveHeading();
                        idx = ((i32)heading - (i32)(u16)pPlayer->field_E32) & 0xFFF;
                        c = Cosine(idx);
                        if (c < 0) c = -c;
                        c = (c * moveMag) >> 12;
                        c = c * moveScale;
                        c = c * pPlayer->field_EBC;
                        localVelocity.vz = -(c / 16);
                        velocityChanged = 1;
                    }
                    else
                    {
                        heading = pPlayer->GetEffectiveHeading();
                        yaw = (i32)heading;

                        if (G_CAMERA_LIST->mCameraMode == CAMERAMODE_LOOKAROUND)
                        {
                            CVector *pAnchor = reinterpret_cast<CVector*>(
                                reinterpret_cast<char*>(gBossRelated) + 8);
                            i32 dist = Utils_XZDist(&pPlayer->mPos, pAnchor);

                            if (dist > 0x300)
                            {
                                CSVector aim;

                                Utils_CalcAim(&aim, &pPlayer->mPos, pAnchor);
                                yaw = ((i32)(u16)pPlayer->field_E32 + (i32)aim.vy) & 0xFFF;

                                if (pPlayer->field_E32 == 0x400
                                    || pPlayer->field_E32 == (i16)0xC00)
                                {
                                    i32 radius = pPlayer->GetPerpendicularisationRadius();
                                    u8 keepDistance;

                                    if (dist > radius)
                                    {
                                        dist -= pPlayer->mElapsedTicks;
                                        keepDistance = 0;
                                    }
                                    else
                                    {
                                        keepDistance = wasPerpendicularising;
                                    }

                                    pPlayer->mArenaConstraintActive = 1;
                                    if (keepDistance == 0)
                                    {
                                        pPlayer->mArenaConstraintRadius = dist;
                                    }
                                }
                            }
                        }
                        else if ((pPlayer->mActionState & 6) != 0 && pPlayer->field_E8C == 0)
                        {
                            yaw = ((i32)(u16)pPlayer->field_E32 + cameraHeading) & 0xFFF;
                        }

                        if (pPlayer->field_AE5 == 0)
                        {
                            i32 idx = yaw & 0xFFF;
                            i32 ebc = pPlayer->field_EBC;
                            i32 c = (((i32)Cosine(idx) * moveMag) >> 12) * ebc * moveScale;
                            i32 sn = (((i32)Sine(idx) * moveMag) >> 12) * ebc * moveScale;

                            localVelocity.vz = -(c / 16);
                            localVelocity.vx = -(sn / 16);
                            velocityChanged = 1;
                        }
                    }
                }
                else if ((pPlayer->mActionState & 0x40040004) == 0)
                {
                    localVelocity.vx = 0;
                    localVelocity.vz = 0;
                    velocityChanged = 1;
                }
            }
        }

        // --- apply it, 0x4B763C ---
        if (pPlayer->field_8E8 != 0)
        {
            gte_SetRotMatrix(&pPlayer->mTransform);
        }
        else
        {
            gte_SetRotMatrix(&identityA);
        }

        if (velocityChanged != 0)
        {
            pPlayer->mVel = (localVelocity >> 6);
            gte_ldlvl(reinterpret_cast<VECTOR*>(&pPlayer->mVel));
            gte_rtir();
            gte_stlvnl(reinterpret_cast<VECTOR*>(&pPlayer->mVel));
            pPlayer->mVel <<= 6;
        }
    }
}

void PlayerFrameFinalizer::rideSupportBody(SLineInfo& lineInfo)
{
    CPlayer* pPlayer = &player_;
    CBody* pPlatform;
    CVector previousPosition;

    // --- ride the platform under us, 0x4B76C0 ---
    pPlatform = pPlayer->mSupportBody;
    if (pPlatform != 0)
    {
        i32 dz = pPlatform->mVel.vz;
        bool moved;

        previousPosition = pPlayer->mPos;

        if (PLR_U16(pPlayer, 0xE8E) != 0)
        {
            i32 dx = pPlatform->mVel.vx;

            moved = ((dx | dz) != 0);
            if (moved)
            {
                pPlayer->mPos.vx += dx;
            }
        }
        else
        {
            i32 dy = pPlatform->mVel.vy;
            i32 dx = pPlatform->mVel.vx;

            moved = ((dy | dx | dz) != 0);
            if (moved)
            {
                pPlayer->mPos.vx += dx;
                pPlayer->mPos.vy += dy;
            }
        }

        if (moved)
        {
            pPlayer->mPos.vz += dz;

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
            lineInfo.StartCoords = previousPosition;
            lineInfo.EndCoords = pPlayer->mPos;

            M3dColij_InitLineInfo(&lineInfo);
            M3dZone_LineToItem(&lineInfo, 1);

            if (lineInfo.pItem != 0)
            {
                pPlayer->mPos = previousPosition;
            }
        }
    }
}

void PlayerFrameFinalizer::updateVerticalImpulse()
{
    CPlayer* pPlayer = &player_;
    i32 impulsePhase;

    // --- vertical impulse, 0x4B77B8 ---
    if ((pPlayer->mCollision & 0x100) != 0)
    {
        pPlayer->field_E88 = 0;
    }

    impulsePhase = pPlayer->field_E84;
    if (impulsePhase > 0 || pPlayer->field_E88 > 0)
    {
        if (impulsePhase < 0x10000 && pPlayer->field_E88 == 0)
        {
            pPlayer->mVel.vy = (((pPlayer->field_E80 >> 12) * impulsePhase) >> 4) & (i32)0xFFFFF000;
        }
        else
        {
            pPlayer->mVel.vy = pPlayer->field_E80;
        }
    }

    if ((i32)(impulsePhase & (i32)0xFFFF0000) > 0 && pPlayer->mActionState == 0x200)
    {
        pPlayer->mVel.vy = pPlayer->field_E80;
    }

    pPlayer->ProcessSFXArray();

    if (pPlayer->field_E88 > 0)
    {
        pPlayer->field_E88 -= (pPlayer->mElapsedTicks << 16) / 2;
        if (pPlayer->field_E88 < 0)
        {
            pPlayer->field_E88 = 0;
        }
    }

    if (pPlayer->field_E84 > 0)
    {
        pPlayer->field_E84 -= (pPlayer->mElapsedTicks << 16) / 2;
        if (pPlayer->field_E84 < 0)
        {
            pPlayer->field_E84 = 0;
        }
    }

    if (pPlayer->mIsCrawling != 0 || (pPlayer->mActionState & 0x3E3FF780) != 0)
    {
        pPlayer->field_5AC = 0;
    }
}

void PlayerFrameFinalizer::resolveActorContacts()
{
    CPlayer* pPlayer = &player_;

    // --- ran into a baddy, 0x4B78A8 ---
    if (pPlayer->field_E8 != pPlayer->mPos)
    {
        CVector hitPos;

        if ((pPlayer->mActionState & 0x1000000) != 0 && pPlayer->field_8D8 == 0)
        {
            CVector lineStart;
            CVector lineEnd;
            CBody *pIgnore = 0;
            CBody *pHit;

            lineStart.vx = pPlayer->field_E8.vx;
            lineStart.vy = pPlayer->mPos.vy;
            lineStart.vz = pPlayer->field_E8.vz;
            lineEnd = pPlayer->mPos + (pPlayer->mPos - pPlayer->field_E8);

            for (;;)
            {
                pHit = M3dColij_LineToSphere(&lineStart, &lineEnd, &hitPos,
                    reinterpret_cast<CBody*>(BaddyList), pIgnore, 0x1000);
                if (pHit == 0)
                {
                    break;
                }

                {
                    i32 kind = (i32)pHit->mType - 304;

                    if ((u32)kind > 0x14)
                    {
                        break;
                    }

                    // jpt_4B7987: the 21 mTypes 304..324 pick one of three
                    // paths through byte_4B8778.
                    if (kind == 1 || kind == 11)
                    {
                        // types 305 and 315: pass straight through, carry
                        // on from the hit point ignoring this one
                        lineStart = hitPos;
                        pIgnore = pHit;
                        continue;
                    }

                    if (kind == 4 || kind == 5 || kind == 7 || kind == 12
                        || kind == 14 || kind == 15 || kind == 17
                        || kind == 18 || kind == 19)
                    {
                        // types 308, 309, 311, 316, 318, 319, 321..323
                        break;
                    }
                }

                // everything else: smash into it
                {
                    SHitInfo hit;

                    hit.field_C.vx = 0;
                    hit.field_C.vy = 0;
                    hit.field_C.vz = 0;
                    hit.field_0 = 0x1E;
                    hit.field_8 = (u16)pPlayer->GetDamageInflictedFromDifficulty(0x14);
                    hit.field_4 = 2;
                    hit.field_C.vx = -pPlayer->mForwardDirection.vx;
                    hit.field_C.vy = 0;
                    hit.field_C.vz = -pPlayer->mForwardDirection.vz;
                    hit.field_18 = 0x200;
                    hit.field_1A = 0xF;
                    VectorNormal(reinterpret_cast<VECTOR*>(&hit.field_C),
                        reinterpret_cast<VECTOR*>(&hit.field_C));

                    pHit->Hit(&hit);
                    SFX_PlayPos(0x10, &pPlayer->mPos, 0);

                    // 0x4B8BE0, inlined: the knockback timer pair.
                    pPlayer->field_534 = 0x168;
                    pPlayer->field_52C = (pPlayer->field_528 + 0xB) << 10;

                    pPlayer->mVel.vz = 0;
                    pPlayer->mVel.vx = 0;
                    pPlayer->field_8D8 = 1;
                }
                break;
            }
        }
        else if ((pPlayer->mActionState & 0x14) != 0)
        {
            CBody *pHit = M3dColij_LineToSphere(&pPlayer->field_E8, &pPlayer->mPos,
                &hitPos, reinterpret_cast<CBody*>(BaddyList), 0, 0x1000);

            if (pHit != 0)
            {
                pPlayer->CollideWithObject(pHit);
            }
        }
    }
}

void PlayerFrameFinalizer::collectPowerUps()
{
    CPlayer* pPlayer = &player_;

    // --- pick up power-ups, 0x4B7A74 ---
    {
        CBody *pPowerUp = reinterpret_cast<CBody*>(PowerUpList);

        while (pPowerUp != 0)
        {
            if ((pPowerUp->mCBodyFlags & 0x40) == 0
                && spidey::math::approximateDistance(pPlayer->mPos, pPowerUp->mPos) < (i32)pPowerUp->mRMinor)
            {
                if (pPowerUp->mType == 0xB)
                {
                    if ((pPlayer->mActionState & 0x11) != 0)
                    {
                        static_cast<CPowerUp*>(pPowerUp)->TakeEffect(pPlayer);
                    }
                }
                else if (static_cast<CPowerUp*>(pPowerUp)->TakeEffect(pPlayer) == 0)
                {
                    // 0x4B8C00, inlined
                    PLR_U8(pPowerUp, 0x124) = 1;

                    // 0x370: "the pickup jingle is free to play again".
                    if (PLR_U8(pPlayer, 0x370) != 0)
                    {
                        u16 kind = pPowerUp->mType;

                        if (kind == 8 || (kind > 0xD && kind <= 0x10))
                        {
                            SFX_Play(0x28, 0x2000, 0);
                            PLR_U8(pPlayer, 0x370) = 0;
                        }
                    }
                }
                break;
            }

            pPowerUp = reinterpret_cast<CBody*>(pPowerUp->mNextItem);
        }

        if (pPowerUp == 0)
        {
            PLR_U8(pPlayer, 0x370) = 1;
        }
    }
}

void PlayerFrameFinalizer::separateStandingPlayerFromActors()
{
    CPlayer* pPlayer = &player_;

    // --- push away from baddies while standing, 0x4B7B1E ---
    if ((pPlayer->mActionState & 1) != 0 && pPlayer->field_E6C == 0)
    {
        CBody *pBaddy = reinterpret_cast<CBody*>(BaddyList);

        while (pBaddy != 0)
        {
            if (pBaddy->mRMinor != 0)
            {
                CVector away = pPlayer->mPos - pBaddy->mPos;
                i32 dist = away.Length();

                if (dist < (((i32)pBaddy->mRMinor * 3) << 10 >> 12))
                {
                    i32 strength = 10;

                    away.vy = 0;
                    away.vx = away.vx / dist;
                    away.vz = away.vz / dist;
                    pPlayer->mVel += (away * strength);
                }
            }

            pBaddy = reinterpret_cast<CBody*>(pBaddy->mNextItem);
        }
    }
}

void PlayerFrameFinalizer::sweepTriggerZones(SLineInfo& lineInfo)
{
    CPlayer* pPlayer = &player_;

    // --- trigger-zone sweep along the movement, 0x4B7BF0 ---
    {
        // function-local static in the original (guard byte 0x6A7F24,
        // object 0x6A7EF8); the registered atexit handler is a bare retn.
        static CVector sLastMoveDirection;

        CVector moveDir = (pPlayer->mPos - pPlayer->field_E8) >> 12;

        if (pPlayer->mActionState != 0x80)
        {
            CVector normal;
            CVector back;

            if ((moveDir.vz | moveDir.vy | moveDir.vx) != 0)
            {
                sLastMoveDirection = moveDir;
            }

            VectorNormal(reinterpret_cast<VECTOR*>(&sLastMoveDirection),
                reinterpret_cast<VECTOR*>(&normal));

            back = pPlayer->field_E8 - (normal * 0x10);

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
            lineInfo.StartCoords = back;
            lineInfo.EndCoords = pPlayer->mPos;

            M3dColij_InitLineInfo(&lineInfo);

            G_TRIGGER_COLLISION_CHECK = 0;
            lineInfo.RecordTriggerZoneHits = 1;
            M3dZone_LineToItem(&lineInfo, 1);
            G_TRIGGER_COLLISION_CHECK = 0;
        }
    }
}

} // namespace player
} // namespace spidey
