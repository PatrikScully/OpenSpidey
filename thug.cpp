#include "thug.h"
#include "validate.h"
#include "trig.h"
#include "m3dutils.h"
#include "message.h"
#include "ps2lowsfx.h"
#include "utils.h"
#include "ai.h"
#include "ps2redbook.h"
#include "spidey.h"
#include "exp.h"
#include "web.h"
#include "m3dzone.h"
#include "camera.h"
#include <cmath>
#include <new>
#include "spool.h"
#include "powerup.h"
#include "ps2m3d.h"

// @Ok
// @Matching
// 0x4D5B80
void CThug::TossGrenade(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->field_32C = 1200;
			new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 2, 100, 0);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->field_288 & 2)
			{
				this->field_288 &= ~2;
				this->RunAnim(31, 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 2:
			if (this->mFrame >= 37)
			{
				CVector pos;
				SHook hook;
				hook.Part.vx = 0;
				hook.Part.vy = 0;
				hook.Part.vz = 0;
				hook.Offset = 14;
				M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&pos), this, &hook);
				new CGrenade(this->mPos, G_MECHLIST_PLAYER->mPos,
						1024 / (Rnd(8) + 24), this->field_388, 512,
						reinterpret_cast<CBody**>(&G_BADDY_LIST), 1989287857, 0, 0);
				this->field_218 |= 1;
				this->field_3B4 = 450 - Rnd(150);
				this->dumbAssPad++;
			}
			break;
		case 3:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @NotOk
// 0x4DBBF0
// Native comparison passed 40000 trajectory cases; instruction matching is pending.
CGrenade::CGrenade(const CVector& start, const CVector& target, i32 speed, i32 damage, i32 radius, CBody** targets, u32 checksum, u8 contact, u8 flag)
{
	this->field_104 = damage;
	this->CommonInit(targets, radius, checksum, contact, flag);
	this->mPos = start;
	i32 dx = (target.vx - start.vx) >> 12;
	i32 dz = (target.vz - start.vz) >> 12;
	i32 distance = M3dMaths_SquareRoot0(dx * dx + dz * dz);
	if (distance)
	{
		i32 horizontalSpeed = 9 * speed / 10;
		this->mVel.vy = (speed * ((target.vy - start.vy) >> 12) / distance - 8 * distance / (2 * speed)) << 12;
		this->mVel.vx = horizontalSpeed * ((dx << 12) / distance);
		this->mVel.vz = horizontalSpeed * ((dz << 12) / distance);
	}
	else
	{
		this->mVel.vx = 0;
		this->mVel.vy = -81920;
		this->mVel.vz = 0;
	}
}

// @Ok
// @Matching
// 0x4DBD40
CGrenade::~CGrenade(void)
{
	delete this->mpGlow;
	this->DeleteFrom(&G_BULLET_LIST);
}

// @NotOk
// 0x4DBDB0
// Native comparison passed 50000 cases; instruction matching is pending.
void CGrenade::AI(void)
{
	i32 detonate = 0;
	this->mPos.vy += 81920;
	if (!this->mSettled)
	{
		G_LINE_INFO.StartCoords = this->mPos;
		G_LINE_INFO.EndCoords.vx = this->mPos.vx + 2 * this->mVel.vx;
		G_LINE_INFO.EndCoords.vy = this->mPos.vy + 2 * this->mVel.vy;
		G_LINE_INFO.EndCoords.vz = this->mPos.vz + 2 * this->mVel.vz;
		i32 speed = this->mVel.Length();
		M3dColij_InitLineInfo(&G_LINE_INFO);
		G_LINE_OF_SIGHT_CHECK = 1;
		M3dZone_LineToItem(&G_LINE_INFO, 1);
		G_LINE_OF_SIGHT_CHECK = 0;
		this->mPos += this->mVel;
		this->mVel.vy += 0x8000;
		if (G_LINE_INFO.pItem && G_LINE_INFO.Distance <= speed)
		{
			this->mPos = G_LINE_INFO.Position;
			SFX_PlayPos(0x2A, &this->mPos, 0);
			if (this->mExplodeOnContact)
				detonate = 1;
			i32 x = this->mVel.vx;
			i32 y = this->mVel.vy;
			i32 z = this->mVel.vz;
			i32 dot = (G_LINE_INFO.Normal.vx * (x >> 6)
					+ G_LINE_INFO.Normal.vy * (y >> 6)
					+ G_LINE_INFO.Normal.vz * (z >> 6)) >> 12;
			this->mVel.vx = x - ((dot * G_LINE_INFO.Normal.vx) >> 6);
			this->mVel.vy = y - ((dot * G_LINE_INFO.Normal.vy) >> 6);
			this->mVel.vz = z - ((dot * G_LINE_INFO.Normal.vz) >> 6);
			this->mVel >>= 1;
			this->mVel.vx -= (dot * G_LINE_INFO.Normal.vx) >> 7;
			this->mVel.vy -= (dot * G_LINE_INFO.Normal.vy) >> 7;
			this->mVel.vz -= (dot * G_LINE_INFO.Normal.vz) >> 7;
			this->mVel.vx += (Rnd(11) - 5) << 12;
			this->mVel.vz += (Rnd(11) - 5) << 12;
			if (this->mVel.vy > -61440 && G_LINE_INFO.Normal.vy < -3500)
			{
				this->mSettled = 1;
				if (Trig_GetLevelId() == 0x1101)
				{
					i32 height = Utils_GetGroundHeight(&this->mPos, 50, 50, 0);
					this->mVel.vz = this->mVel.vy = this->mVel.vx = 0;
					if (height != -1)
						this->mPos.vy = height + 81920;
				}
			}
		}
		this->mAngles.vx += 410 * this->mVel.Length() / 128;
		i32 vx = this->mVel.vx >> 12;
		i32 vz = this->mVel.vz >> 12;
		if (vz)
		{
			if (vz > 0)
				this->mAngles.vy = 2048 - catan(-((vx << 12) / vz));
			else
				this->mAngles.vy = catan((vx << 12) / vz);
		}
		else
			this->mAngles.vy = vx > 0 ? -1024 : 1024;
	}
	G_LINE_INFO.StartCoords = this->mPos;
	G_LINE_INFO.EndCoords.vx = this->mPos.vx;
	G_LINE_INFO.EndCoords.vy = this->mPos.vy + 0xBB8000;
	G_LINE_INFO.EndCoords.vz = this->mPos.vz;
	M3dColij_InitLineInfo(&G_LINE_INFO);
	M3dZone_LineToItem(&G_LINE_INFO, 1);
	if (G_LINE_INFO.pItem)
	{
		this->mShadowPos = G_LINE_INFO.Position;
		this->mShadowDist = (G_LINE_INFO.Position.vy - this->mPos.vy) >> 12;
		this->mShadowScale = 50 - 21 * this->mShadowDist / 128;
		if (this->mShadowScale)
			this->ShadowOn();
		else
			this->KillShadow();
	}
	else
		this->KillShadow();
	i32 age = G_TIMER_RELATED - this->mStartTime;
	if (age > 90)
	{
		if (G_TTIME & 2)
		{
			u8 red = this->mRed <= 127 ? 2 * this->mRed : 255;
			u8 green = this->mGreen <= 127 ? 2 * this->mGreen : 255;
			u8 blue = this->mBlue <= 127 ? 2 * this->mBlue : 255;
			this->mpGlow->SetFringeRGB(0, red, green, blue);
		}
		else
			this->mpGlow->SetFringeRGB(0, this->mRed, this->mGreen, this->mBlue);
	}
	if (age > 120 || detonate)
	{
		this->GiveScaledDamageToObjects(*static_cast<CBody**>(this->field_100), this->field_106, 100, 10, ALWAYS_TWENTY_NINE);
		this->GiveScaledDamageToObjects(G_ENVIRONMENTAL_OBJECT_LIST, this->field_106, 100, 10, ALWAYS_TWENTY_NINE);
		if (this->field_100 != reinterpret_cast<void*>(&G_MECHLIST_PLAYER))
			this->GiveScaledDamageToObjects(G_MECHLIST_PLAYER, this->field_106, 100, 10, ALWAYS_TWENTY_NINE);
		this->GiveScaledDamageToEnviro(this->field_106);
		CVector pos;
		pos.vx = Rnd(50) << 12;
		pos.vy = Rnd(50) << 12;
		pos.vz = Rnd(50) << 12;
		pos += this->mPos;
		new CGrenadeExplosion(&pos);
		pos.vx = Rnd(50) << 12;
		pos.vy = Rnd(50) << 12;
		pos.vz = Rnd(50) << 12;
		pos += this->mPos;
		new CGrenadeExplosion(&pos);
		SFX_PlayPos(0x8029, &this->mPos, 0);
		new CGlowFlash(&this->mPos, 5, 255, 255, 255, 1, 128, 100, 0, 1, 60, 0, 1, 100, 120, 80, 100, 10, 4);
		this->Die();
	}
	this->mPos.vy -= 81920;
	this->mpGlow->SetPos(this->mPos);
}

// Shared region byte used by Spidey_SwapSuit and the default grenade model.
static u8* const gGrenadeDefaultRegion = reinterpret_cast<u8*>(0x006B4678);

// @NotOk
// 0x4DB9C0
// Native comparison passed 40000 cases; operand matching is pending.
void CGrenade::CommonInit(CBody** targets, i32 radius, u32 checksum, u8 explodeOnContact, u8 flag)
{
	this->AttachTo(&G_BULLET_LIST);
	if (checksum)
	{
		this->mModel = Spool_GetModel(checksum, G_OBJ_FILE_REGION);
		this->mRegion = G_OBJ_FILE_REGION;
	}
	else
	{
		this->mModel = Spool_GetModel(0x51E46AAF, *gGrenadeDefaultRegion);
		this->mRegion = *gGrenadeDefaultRegion;
	}
	this->field_108 = ALWAYS_TWENTY_NINE;
	this->mStartTime = G_TIMER_RELATED;
	if (Trig_GetLevelId() == 0x1101)
		this->mStartTime += Rnd(480) + 120;
	this->mExplodeOnContact = explodeOnContact;
	i32 type = (targets != reinterpret_cast<CBody**>(&G_MECHLIST_PLAYER)) + 63;
	this->field_106 = radius;
	this->mShadowThreshold = 300;
	this->field_100 = targets;
	this->field_15C = flag;
	this->mType = type;
	if (targets == reinterpret_cast<CBody**>(&G_MECHLIST_PLAYER))
		this->mCBodyFlags &= ~0x10;
	this->mpGlow = new CGlow(4, 2);
	this->mpGlow->mSkipTriangles = 1;
	this->mpGlow->SetFringeWidth(0, 16);
	this->mpGlow->SetFringeWidth(1, 16);
	this->mpGlow->SetCentreRGB(0, 0, 0);
	this->mpGlow->SetRGB(0, 0, 0);
	this->mRed = 100;
	this->mGreen = 50;
	this->mBlue = 0;
	this->mpGlow->SetFringeRGB(0, 100, 50, 0);
	this->mpGlow->SetFringeRGB(1, 0, 0, 0);
	this->mpGlow->mAngle = Rnd(1024);
	this->mpGlow->SetRadius(0);
	this->mpGlow->SetPos(this->mPos);
	this->mpGlow->mProtected = 1;
}

// 0x00682C50 and 0x00682C54 in the exe (gGlobalThug / gThugList in the
// maintainer's IDB). CThug_Hit, CThug_Fall, CThug_ProcessMessages and
// CThug_AI write both of them and none of those are hooked, so the exe's
// copies are the only real ones.
#ifndef SPIDEY_STANDALONE
EXPORT CThug* gGlobalThug;
#else
extern CThug* gGlobalThug;
#endif
//#define G_GLOBAL_THUG (gGlobalThug)
#define G_GLOBAL_THUG (*reinterpret_cast<CThug**>(0x00682C50))

#ifndef SPIDEY_STANDALONE
EXPORT CThug* gThugList;
#else
extern CThug* gThugList;
#endif
//#define G_THUG_LIST (gThugList)
#define G_THUG_LIST (*reinterpret_cast<CThug**>(0x00682C54))

// gAttackFlagRelated is shared with cop.cpp, so its macro lives in baddy.h.

// @FIXME
// 0x00557CA0. A read only state table the exe ships in its .data (pairs of
// i16 built at compile time); our copy has no initialiser at all, so it is
// all zeros and CheckStateFlags would never match a state. Read from the
// exe until somebody writes the real table out.
#ifndef SPIDEY_STANDALONE
EXPORT SStateFlags gThugStateFlags;
#else
extern SStateFlags gThugStateFlags;
#endif
//#define G_THUG_STATE_FLAGS (&gThugStateFlags)
#define G_THUG_STATE_FLAGS (reinterpret_cast<SStateFlags*>(0x00557CA0))

// @NotOk
// Native comparison passed 100000 cases; instruction matching is pending.
// 0x4DB280
void CThug::AI(void)
{
	if (this->mType != 304 && this->field_3B8 == 0x1101 && this->field_31C.bothFlags != 5)
		this->field_3B4 += this->field_80;
	if (this->field_31C.bothFlags)
		this->DoAISwitchLogic();
	if (!(this->CheckStateFlags(G_THUG_STATE_FLAGS, 17) & 4))
		this->DoPhysics(0);
	if (!(this->mFlags & 4))
		M3d_BuildTransform(this);
	this->PlaySounds();
	if (this->field_3AC & 2)
	{
		if (this->mShadowScale >= 5)
			this->mShadowScale -= 5;
		else
			this->mShadowScale = 0;
	}
	else
	{
		this->mShadowScale += 10;
		if (this->mShadowScale > 50)
			this->mShadowScale = 50;
		this->mShadowPos.vx = this->mPos.vx;
		this->mShadowPos.vy = this->mPos.vy + (this->field_21E << 12);
		this->mShadowPos.vz = this->mPos.vz;
	}
	if (this->pMessage)
		this->ProcessMessages();
	this->field_3AC = 0;
	if (this->mAIProcList)
	{
		this->mAIProcList->Execute();
		this->CleanUpAIProcList(0);
	}
	if (this->field_31C.bothFlags == 28)
	{
		this->mCBodyFlags |= 0x10;
		this->field_310 = 0;
		if (!this->DetermineFightState())
		{
			if (G_MECHLIST_PLAYER->field_57C && this->field_330)
				this->field_31C.bothFlags = 25;
			else
				this->field_31C.bothFlags = 2;
			this->dumbAssPad = 0;
		}
	}
	else if ((this->CheckStateFlags(G_THUG_STATE_FLAGS, 17) & 0x40)
			&& !((G_ATTACK_RELATED + static_cast<u8>(this->field_21D)) & this->field_36C))
		this->DetermineFightState();
	if (this->field_330 && !((G_ATTACK_RELATED + this->field_21D) & 31)
			&& this->PlayerIsVisible())
		this->WarnOtherThugs();
	this->field_2A8 &= ~0x20;
	switch (this->field_31C.bothFlags)
	{
		case 0:
			switch (this->dumbAssPad)
			{
				case 0:
					this->field_330 = 0;
					if (G_ATTACK_RELATED - this->field_3B0 <= 10 || !(G_ATTACK_RELATED & 15))
					{
						i32 ground = Utils_GetGroundHeight(&this->mPos, 300, 300, 0);
						if (ground != -1)
						{
							this->mPos.vy = ground - (this->field_21E << 12);
							this->field_29C = this->mPos.vy;
							this->field_2A0 = ground;
							this->SetHeight(1, 100, 600);
							this->dumbAssPad++;
							this->CycleAnim(static_cast<u8>(this->field_298.Bytes[0]), 1);
						}
					}
					break;
				case 1:
					if (this->field_1F0 || this->GetNextWaypoint())
						this->field_31C.bothFlags = 2;
					else
						this->field_31C.bothFlags = 1;
					this->dumbAssPad = 0;
					break;
				default: print_if_false(0, "Unknown sub-state!"); break;
			}
			break;
		case 1: this->Guard(); break;
		case 3:
			this->field_330 = 600;
			this->field_2A8 |= 0x20;
			this->Caution();
			this->SetHeight(0, 100, 600);
			break;
		case 4:
			this->field_2A8 |= 0x20;
			this->field_330 = 600;
			this->ChasePlayer(0);
			this->SetHeight(0, 100, 600);
			break;
		case 10:
			this->field_2A8 |= 0x20;
			switch (this->dumbAssPad)
			{
				case 0:
					this->RunAnim(9, 0, -1);
					this->dumbAssPad++;
					break;
				case 1:
					if (this->mAnimFinished)
					{
						this->field_31C.bothFlags = 28;
						this->dumbAssPad = 0;
					}
					break;
				default: print_if_false(0, "Unknown substate!"); break;
			}
			break;
		case 2:
		case 24:
			this->FollowWaypoints();
			this->SetHeight(0, 100, 600);
			break;
		case 5:
			this->field_2A8 |= 0x20;
			this->field_330 = 600;
			this->AttackPlayer();
			this->SetHeight(0, 100, 600);
			break;
		case 23: this->LookForPlayer(); break;
		case 11:
			this->field_2A8 |= 0x20;
			this->field_330 = 600;
			this->TossGrenade();
			break;
		case 8: this->ShootHostage(); break;
		case 9:
			this->field_330 = 600;
			this->field_2A8 |= 0x20;
			this->ShootPlayer();
			break;
		case 12:
			this->field_2A8 |= 0x20;
			this->TakeHit();
			this->field_330 = 600;
			break;
		case 25: this->LookConfused(); break;
		case 22: this->Fall(); break;
		case 14:
		case 15:
			this->field_2A8 |= 0x20;
			this->GetYankedBySpidey();
			this->field_330 = 600;
			break;
		case 16: this->GetWhippedLikeTheWhoreYouAre(); break;
		case 17:
			this->field_2A8 |= 0x20;
			this->FlyAcrossRoom();
			break;
		case 18:
			this->field_330 = 600;
			this->field_2A8 |= 0x20;
			this->GetTrapped();
			break;
		case 13: this->BackpedalPlease(); break;
		case 19:
			this->field_330 = 600;
			this->field_2A8 |= 0x20;
			if (this->field_2A8 & 8)
			{
				if (!this->dumbAssPad)
				{
					this->Neutralize();
					this->CycleAnim(this->mType != 304 ? 15 : 17, 1);
					this->dumbAssPad++;
				}
				else if (this->dumbAssPad != 1) print_if_false(0, "Unknown substate.");
			}
			else
			{
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}
			break;
		case 20:
			this->field_330 = 600;
			this->field_2A8 |= 0x20;
			if (this->field_2A8 & 0x40)
				this->GettingGrabbed();
			else
			{
				SFX_PlayPos(0x802D, &this->mPos, 0);
				this->field_31C.bothFlags = 13;
				this->dumbAssPad = 0;
			}
			break;
		case 21: this->Acknowledge(); break;
		case 26: this->DieThug(0); break;
		case 27: this->DieThug(1); break;
		default: print_if_false(0, "Unknown state!"); break;
	}
}

// @NotOk
// 0x4D7C30
// Native comparison passed 80000 cases; instruction matching is pending.
void CThug::FlyAcrossRoom(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
		{
			this->field_3AC |= 2;
			this->ClearAttackFlags();
			this->field_310 = 0;
			if (this->mHealth <= 0)
				this->mCBodyFlags &= ~0x10;
			i32 previous = this->field_318;
			if (previous == 1 || previous == 2 || previous == 6 || previous == 23)
				this->mRMinor = 0;
			i32 speed = this->mVel.vx;
			if (my_abs(speed) <= my_abs(this->mVel.vz))
				speed = this->mVel.vz;
			if (my_abs(speed * this->field_1F8) < 819200)
				this->RunAppropriateHitAnim();
			else
				this->CycleAnim(this->mType == 304 ? 33 : 15, 1);
			if (this->mHealth <= 0 || ((this->field_218 & 0x80000) && this->mHealth <= 50))
				this->dumbAssPad = 10;
			else
				this->dumbAssPad = 1;
			break;
		}
		case 1:
		{
			this->field_3AC |= 2;
			this->field_2FC = this->mPos;
			this->DoPhysics(1);
			CBody* body = this->StruckGameObject(0, 1);
			if (body)
			{
				this->field_2A8 &= ~0x10;
				if (this->field_1F8 > 1 && (body->mType == 312 || body->mType == 304))
					this->ElasticCollision(this, body, this->mVel, this->field_1F8 >> 1);
			}
			if (!this->field_1F8 || body)
			{
				this->mVel.vz = this->mVel.vy = this->mVel.vx = 0;
				if (this->ShouldFall(200, 389120))
				{
					this->field_31C.bothFlags = 22;
					this->dumbAssPad = 0;
					this->field_218 &= ~2;
				}
				else
				{
					this->SetHeight(1, 100, 600);
					this->PlayHitWallSound();
					switch (this->field_318)
					{
						case 1:
						case 2:
						case 6:
						case 23:
						case 25:
							this->CheckFallBack();
							this->RunAnim((this->field_2A8 & 0x10) ? 26 : (this->mType == 304 ? 14 : 19), 0, -1);
							this->dumbAssPad++;
							break;
						default:
							if (this->mAnim == (this->mType == 304 ? 33 : 15))
							{
								this->field_31C.bothFlags = 28;
								this->dumbAssPad = 0;
							}
							this->dumbAssPad = 5;
							break;
					}
				}
			}
			break;
		}
		case 2:
			this->SetHeight(0, 100, 600);
			if (this->mAnimFinished)
			{
				this->mRMinor = this->mType == 304 ? 96 : 150;
				this->RunAnim(this->mType == 304 ? 15 : 20, 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 3:
			this->SetHeight(0, 100, 600);
			if (this->mFrame < 22)
				this->field_3AC |= 1;
			if (this->mAnimFinished)
			{
				if (this->mHealth > 0)
				{
					this->mCBodyFlags |= 0x10;
					this->field_31C.bothFlags = 2;
					this->dumbAssPad = 0;
				}
				else
					this->DieAfterFlyingAcrossRoom();
			}
			break;
		case 5:
			if (this->SetHeight(0, 100, 600) == 2 && this->mAnimFinished)
			{
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}
			break;
		case 10:
		{
			this->field_2FC = this->mPos;
			this->DoPhysics(1);
			this->field_3AC |= 2;
			if (this->field_218 & 0x40000)
			{
				this->mAngles.vx += static_cast<i16>(this->field_80) * static_cast<i16>(this->field_324);
				this->mAngles.vy += static_cast<i16>(this->field_80) * static_cast<i16>(this->field_328);
			}
			// The PC entry saves ECX in its local stack slot. At 0x4D825A
			// the collision-delay path reloads that saved this pointer.
			CBody* body = this;
			if (!this->field_340)
			{
				body = this->StruckGameObject(0, 1);
				if (body)
				{
					this->field_2A8 &= ~0x10;
					if (this->field_1F8 > 1 && (body->mType == 312 || body->mType == 304))
					{
						this->ElasticCollision(this, body, this->mVel, this->field_1F8 >> 1);
						if (this->field_218 & 0x40000)
						{
							this->field_340 = 10;
							body = 0;
						}
					}
				}
			}
			if (!this->field_1F8 || body)
			{
				this->mVel.vz = this->mVel.vy = this->mVel.vx = 0;
				this->mAngles.vx = 0;
				this->DieAfterFlyingAcrossRoom();
			}
			break;
		}
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @NotOk
// 0x4D85C0
// Native comparison passed 60000 cases; instruction matching is pending.
void CThug::GetYankedBySpidey(void)
{
	if (!(G_ATTACK_RELATED & 3))
	{
		CThug* buddy = static_cast<CThug*>(this->GetClosest(304, 0));
		if (!buddy)
			buddy = static_cast<CThug*>(this->GetClosest(312, 0));
		if (buddy && !buddy->field_330)
			new CMessage(this, buddy, 7, 0);
	}
	if (this->field_31C.bothFlags == 14)
	{
		this->mPos.vy += 6144 * this->field_350;
		this->mAngles.vx -= 8 * this->field_350;
	}
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_310 = 0;
			this->ClearAttackFlags();
			this->mCBodyFlags &= ~0x10;
			this->field_218 &= ~0x20;
			this->field_350 = 0;
			new CAIProc_StateSwitchSendMessage(this, 19);
			this->RunAnim(this->mType == 304 ? 18 : 16, 0, -1);
			if (this->field_218 & 0x100)
				this->dumbAssPad = 9;
			else
				this->dumbAssPad = this->mHealth <= 0 ? 10 : 1;
			this->field_3AC |= 2;
			break;
		case 1:
		{
			this->field_3AC |= 2;
			if (this->mAnimFinished)
				this->CycleAnim(this->mType == 304 ? 36 : 17, 1);
			CBody* body = 0;
			this->field_2FC = this->mPos;
			this->DoPhysics(1);
			if (this->field_1F8 > 0)
			{
				body = this->StruckGameObject(1, 1);
				if (!body)
					break;
				if (body)
				{
					if (body->mType == 50)
					{
						SHitInfo hit;
						hit.field_0 = 2;
						hit.field_4 = 10;
						this->mPos = this->field_2FC;
						body->Hit(&hit);
					}
					if (this->field_1F8 > 1 && (body->mType == 312 || body->mType == 304))
						this->ElasticCollision(this, body, this->mVel, this->field_1F8 >> 1);
				}
			}
			if (this->field_1F8 <= 0 || body)
			{
				this->field_31C.bothFlags = 15;
				this->mVel.vz = this->mVel.vy = this->mVel.vx = 0;
				if (this->ShouldFall(200, 389120))
				{
					this->field_31C.bothFlags = 22;
					this->dumbAssPad = 0;
					this->field_218 &= ~2;
				}
				else
				{
					if (body)
					{
						this->field_1F8 = 0;
						this->field_218 |= 0x400;
					}
					this->PlayHitWallSound();
					this->RunAnim(this->mType == 304 ? 14 : 19, 0, -1);
					this->dumbAssPad++;
				}
			}
			break;
		}
		case 2:
			if (this->mAnimFinished)
			{
				this->RunAnim(this->mType == 304 ? 15 : 20, 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 3:
			if (this->mAnimFinished)
			{
				this->mCBodyFlags |= 0x10;
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		case 9:
			this->field_3AC |= 2;
			if (this->mHealth <= 0)
				this->field_2A8 |= 0x10;
			this->dumbAssPad = 10;
			// Fall through.
		case 10:
		{
			if (this->mAnimFinished)
				this->CycleAnim(this->mType == 304 ? 36 : 17, 1);
			this->field_2FC = this->mPos;
			this->DoPhysics(1);
			CBody* body = this->StruckGameObject(1, 1);
			if (body)
			{
				this->field_2A8 &= ~0x10;
				if (body->mType == 50)
				{
					SHitInfo hit;
					this->mPos = this->field_2FC;
					hit.field_0 = 2;
					hit.field_4 = 10;
					body->Hit(&hit);
				}
				if (this->field_1F8 > 1 && (body->mType == 312 || body->mType == 304))
					this->ElasticCollision(this, body, this->mVel, this->field_1F8 >> 1);
			}
			if (this->field_1F8 <= 0 || body)
			{
				this->field_31C.bothFlags = 15;
				this->mVel.vz = this->mVel.vy = this->mVel.vx = 0;
				if (this->ShouldFall(200, 389120))
				{
					this->field_31C.bothFlags = 22;
					this->dumbAssPad = 0;
					this->field_218 &= ~2;
				}
				else if (body || !(this->field_218 & 0x100))
				{
					this->field_1F8 = 0;
					this->field_218 |= 0x400;
					if (this->mHealth <= 0)
					{
						this->PlayHitWallSound();
						this->field_31C.bothFlags = 26;
						this->dumbAssPad = 0;
					}
					else
					{
						this->PlayHitWallSound();
						SFX_PlayPos(0x801B, &this->mPos, 0);
						this->RunAnim(this->mType == 304 ? 14 : 19, 0, -1);
						this->dumbAssPad = 2;
					}
				}
				else
				{
					this->RunAnim(this->mType == 304 ? 37 : 32, 0, -1);
					this->dumbAssPad = 11;
				}
			}
			break;
		}
		case 11:
			if (this->mAnimFinished)
			{
				if (this->mHealth <= 0)
				{
					this->field_31C.bothFlags = 26;
					this->dumbAssPad = 0;
				}
				else
				{
					this->RunAnim(this->mType == 304 ? 15 : 20, 0, -1);
					this->dumbAssPad = 3;
				}
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
	if (this->field_31C.bothFlags == 14 && this->dumbAssPad > 0)
	{
		if (this->field_218 & 0x20)
		{
			if (this->field_350)
			{
				this->field_350 -= this->field_80;
				if (this->field_350 < 0)
					this->field_350 = 0;
			}
		}
		else
		{
			this->field_350 += this->field_80;
			if (this->field_350 >= 40)
				this->field_218 |= 0x20;
		}
		this->mPos.vy -= 6144 * this->field_350;
		this->mAngles.vx += 8 * this->field_350;
	}
}

// @NotOk
// 0x4DABF0
// Native comparison passed 60000 message sequences; instruction matching is pending.
void CThug::ProcessMessages(void)
{
	for (CMessage* message = this->pMessage; message; message = message->mNext)
	{
		if (this->field_31C.bothFlags != 26 || message->field_14 >= 11)
		{
			switch (message->field_14)
			{
				case 10:
					if (this->field_31C.bothFlags == 2 || this->field_31C.bothFlags == 1)
						this->RunToWhereTheActionIs(reinterpret_cast<CVector*>(message->mVects));
					this->field_330 = 600;
					break;
				case 20:
					if (this->field_31C.bothFlags != 26 && message->field_40++ < 8)
					{
						this->mFlags |= 4;
						if (this->mpJoints)
						{
							if (message->field_40 >= 4)
							{
								this->mpJoints[3].Angles.vz >>= 1;
								this->mpJoints[6 + (this->mType == 304)].Angles.vz >>= 1;
							}
							else
							{
								this->mpJoints[3].Angles.vz += 800 >> message->field_40;
								this->mpJoints[6 + (this->mType == 304)].Angles.vz -= 800 >> message->field_40;
							}
						}
						this->ApplyPose(reinterpret_cast<i16*>(this->mType == 304 ? 0x557CE4 : 0x557DBC));
						continue;
					}
					if (!this->mpJoints)
						this->ApplyPose(reinterpret_cast<i16*>(this->mType == 304 ? 0x557CE4 : 0x557DBC));
					this->mpJoints[3].Angles.vz = 0;
					this->mpJoints[6 + (this->mType == 304)].Angles.vz = 0;
					this->mFlags &= ~4;
					break;				case 19:
					if (this->mAngles.vx)
					{
						this->mAngles.vx = 0;
						this->mPos.vy += 6144 * this->field_350;
					}
					this->field_350 = 0;
					this->field_218 &= ~0x20;
					break;
				case 13:
					if (this->field_31C.bothFlags == 8 || (this->CheckStateFlags(G_THUG_STATE_FLAGS, 17) & 1) || (this->field_2F0 & 0x10))
					{
						if (Mem_RecoverPointer(&message->mHandle))
							new CMessage(this, reinterpret_cast<CBaddy*>(message->mHandle.pWhatever), 1, 0);
						message->field_10 |= 1;
					}
					else
						this->GetReadyToShootHostage(message);
					continue;
				case 16:
					if (Mem_RecoverPointer(&this->mHandle))
						new CMessage(this, reinterpret_cast<CBaddy*>(this->mHandle.pWhatever), 1, 0);
					break;
				case 17:
					delete this->field_3A0;
					this->field_3A0 = 0;
					break;
				case 12:
					this->ApplyPose(reinterpret_cast<i16*>(this->mType == 304 ? 0x557CE4 : 0x557DBC));
					if (this->field_31C.bothFlags != 26)
					{
						this->mpJoints[1].Angles.vz >>= 1;
						if (this->mpJoints[1].Angles.vz > 1)
							continue;
					}
					this->mpJoints[1].Angles.vz = 0;
					if (G_THUG_LIST == this && this->field_31C.bothFlags != 9)
						G_THUG_LIST = 0;
					this->mFlags &= ~4;
					break;
				case 14:
					if (!(this->field_218 & 0x40))
					{
						CTrapWebEffect* web = static_cast<CTrapWebEffect*>(Mem_RecoverPointer(&this->field_104));
						if (web)
							web->Burst();
						SFX_PlayPos(0x802D, &this->mPos, 0);
					}
					break;
				case 15:
					if (this->field_31C.bothFlags == 14)
						new CAIProc_StateSwitchSendMessage(this, 15);
					else
					{
						CTrapWebEffect* web = static_cast<CTrapWebEffect*>(Mem_RecoverPointer(&this->field_10C));
						if (web)
							web->Burst();
					}
					break;
				case 5:
					if (this->field_31C.bothFlags != 18)
					{
						this->Neutralize();
						this->field_31C.bothFlags = 18;
						this->dumbAssPad = 0;
					}
					else
					{
						if (this->dumbAssPad != 3 && this->dumbAssPad != 2)
							continue;
						this->field_1F8 = 0;
						this->dumbAssPad = 2;
					}
					break;
				case 6:
					if (this->field_31C.bothFlags != 19)
					{
						this->Neutralize();
						SFX_PlayPos(0x800E, &this->mPos, 0);
						this->field_31C.bothFlags = 19;
						this->dumbAssPad = 0;
						new CAIProc_StateSwitchSendMessage(this, 15);
					}
					break;
				case 7:
					if (this->field_31C.bothFlags == 2 || this->field_31C.bothFlags == 1)
					{
						CItem* item = static_cast<CItem*>(Mem_RecoverPointer(&message->mHandle));
						if (item)
							this->RunToWhereTheActionIs(&item->mPos);
					}
					break;

			}
		}
		message->field_10 |= 1;
	}
	this->CleanUpMessages(0, 0);
}

// @NotOk
// 0x4D96D0
// Native comparison passed 40000 cases; instruction matching is pending.
void CThug::GettingGrabbed(void)
{
	this->SetAttacker();
	if (G_MECHLIST_PLAYER->mAnim == 124)
	{
		i32 anim = this->mType == 304 ? 31 : 28;
		if (this->mAnim != anim)
			this->RunAnim(anim, 0, -1);
	}
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->RunAnim(this->mType == 304 ? 34 : 29, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
			{
				if (G_MECHLIST_PLAYER->field_E1C == 0x8000000)
				{
					i32 heldAnim = this->mType == 304 ? 38 : 33;
					i32 alternateAnim = this->mType == 304 ? 30 : 27;
					if (this->mAnim == heldAnim || this->mAnim == alternateAnim)
						this->RunAnim(heldAnim, 0, -1);
					else
						this->RunAnim(alternateAnim, 0, -1);
					this->field_218 |= 4;
				}
				else
				{
					this->RunAnim(this->mType == 304 ? 35 : 30, 0, -1);
					this->field_218 &= ~4;
				}
			}
			if (my_abs(this->mPos.vx - this->field_1A8[this->field_1F0].vx) < 20
					&& my_abs(this->mPos.vz - this->field_1A8[this->field_1F0].vz) < 20)
			{
				this->mAngVel.vy = 0;
				this->mAngAcc.vy = 0;
				this->mAngles.vy = G_MECHLIST_PLAYER->mAngles.vy;
				this->field_1F0--;
				this->dumbAssPad++;
			}
			else
			{
				this->YawTowards(G_MECHLIST_PLAYER->mAngles.vy, 100);
				this->mPos.vx += (this->field_1A8[this->field_1F0].vx - this->mPos.vx) >> 1;
				this->mPos.vz += (this->field_1A8[this->field_1F0].vz - this->mPos.vz) >> 1;
			}
			break;
		case 2:
			if (!G_MECHLIST_PLAYER->GrabUpdate(&this->mPos, &this->mAngles.vy))
				this->field_2A8 &= ~0x40;
			if (this->mAnimFinished)
			{
				if (G_MECHLIST_PLAYER->field_E1C == 0x8000000)
				{
					i32 heldAnim = this->mType == 304 ? 38 : 33;
					i32 alternateAnim = this->mType == 304 ? 30 : 27;
					if (this->mAnim == heldAnim || this->mAnim == alternateAnim)
						this->RunAnim(heldAnim, 0, -1);
					else
						this->RunAnim(alternateAnim, 0, -1);
					this->field_218 |= 4;
				}
				else
				{
					this->RunAnim(this->mType == 304 ? 35 : 30, 0, -1);
					this->field_218 &= ~4;
					SFX_PlayPos((Rnd(2) + 43) | 0x8000, &this->mPos, 0);
				}
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @Matching
// 0x42BFA0, shared with CCop::Victorious in the original vtable.
void CThug::Victorious(void)
{
	SFX_PlayPos(0x8024, &this->mPos, 0);
}

// @NotOk
// 0x4D6C90
// Native comparison passed 50000 cases; instruction matching is pending.
void CThug::ShootPlayer(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_330 = 600;
			this->field_310 = 160;
			this->Neutralize();
			if (this->SetAnimMode(1, 1))
				this->dumbAssPad = 6;
			else
			{
				this->field_218 &= ~0x7000;
				new CAIProc_StateSwitchSendMessage(this, 12);
				new CAIProc_StateSwitchSendMessage(this, 17);
				new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 2, 130, 4);
				this->CycleAnim(this->mType != 304 ? 1 : 4, 1);
				this->field_1F8 = 120;
				this->dumbAssPad++;
			}
			break;
		case 1:
			if (this->RunTimer(&this->field_1F8))
			{
				if (G_MECHLIST_PLAYER->mHealth <= 0 || G_MECHLIST_PLAYER->mHeldObject)
					goto finishShooting;
				this->DrawLaserSiteThingie(1, 0);
				if (this->field_288 & 2)
				{
					this->field_288 &= ~2;
					this->dumbAssPad++;
					this->field_3A8 = G_MECHLIST_PLAYER->mPos.vy;
					this->field_230 = 15;
					this->field_1F8 = Rnd(3) + 1;
				}
			}
			else
			{
				this->Neutralize();
				this->field_31C.bothFlags = 4;
				this->dumbAssPad = 0;
			}
			break;
		case 2:
			if (G_MECHLIST_PLAYER->mHealth <= 0 || G_MECHLIST_PLAYER->mHeldObject)
				goto finishShooting;
			this->DrawLaserSiteThingie(1, 0);
			{
				i32 timer = this->field_230--;
				if (timer == 7)
				{
					if (!G_MECHLIST_PLAYER->mHeldObject)
						SFX_PlayPos(0x801D, &this->mPos, 500);
				}
				else if (timer == 1)
				{
					this->dumbAssPad++;
					if (this->mType == 304)
						this->RunAnim(8, this->mAnim == 8 ? this->mFrame : 0, -1);
					else
						this->RunAnim(7, this->mAnim == 7 ? this->mFrame : 0, -1);
				}
			}
			break;
		case 3:
			if (this->mAnimFinished)
			{
				this->RunAnim((this->mType != 304) + 7, 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 4:
			if (this->mAnimFinished)
			{
				if (--this->field_1F8 > 0 && this->PlayerIsVisible())
				{
					if (this->mType != 304 && this->DistanceToPlayer(2) < 650
							&& my_abs(G_MECHLIST_PLAYER->mPos.vy - this->field_29C - 0x4000) < 409600)
					{
						this->dumbAssPad++;
						break;
					}
					this->field_218 &= ~0x7000;
					this->RunAnim((this->mType != 304) + 7, 0, -1);
				}
				else
				{
					this->dumbAssPad++;
					break;
				}
			}
			if (G_MECHLIST_PLAYER->mHeldObject)
				goto finishShooting;
			if (this->DrawLaserSiteThingie(1, 0))
			{
				SHitInfo hit;
				hit.field_0 = 4;
				hit.field_8 = this->field_38C;
				G_MECHLIST_PLAYER->Hit(&hit);
				if (G_MECHLIST_PLAYER->mHealth <= 0)
					this->Victorious();
			}
			if (this->mType == 304 && !(G_ATTACK_RELATED & 3)
					&& this->DistanceToPlayer(2) < 650
					&& my_abs(G_MECHLIST_PLAYER->mPos.vy - this->field_29C - 0x4000) < 409600)
				this->dumbAssPad++;
			break;
		case 5:
			delete this->field_3A0;
			this->field_3A0 = 0;
finishShooting:
			this->field_31C.bothFlags = 10;
			this->dumbAssPad = 0;
			break;
		case 6:
			if (this->mAnimFinished)
				this->dumbAssPad = 0;
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @Matching
// 0x4D7260
void CThug::ShootHostage(void)
{
	CBaddy** host = reinterpret_cast<CBaddy**>(&this->mHandle.pWhatever);
	if (!Mem_RecoverPointer(&this->mHandle) || ((*host)->field_2A8 & 0x1000))
	{
		this->Neutralize();
		this->field_31C.bothFlags = 28;
		this->dumbAssPad = 0;
		return;
	}
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			new CAIProc_StateSwitchSendMessage(this, 17);
			this->field_310 = 160;
			if (this->mType == 304)
				this->RunAnim(8, this->mAnim == 8 ? this->mFrame : 0, -1);
			else
				this->RunAnim(7, this->mAnim == 7 ? this->mFrame : 0, -1);
			new CAIProc_LookAt(this, *host, 0, 2, 70, 200);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->field_288 & 2)
			{
				this->field_288 &= ~2;
				SFX_PlayPos((Rnd(3) + 14) | 0x8000, &this->mPos, 0);
				this->dumbAssPad++;
			}
			break;
		case 2:
			if (this->mAnimFinished)
			{
				if (Utils_LineOfSight(&this->mPos, &(*host)->mPos, 0, 0))
				{
					new CMessage(this, *host, 2, 0);
					this->field_230 = G_DIFFICULTY_LEVEL == 2 ? Rnd(50) + 75 : Rnd(32) + 49;
					this->dumbAssPad++;
					this->CycleAnim(this->mType == 304 ? 4 : 1, 1);
				}
				else
				{
					this->field_31C.bothFlags = 28;
					this->dumbAssPad = 0;
				}
			}
			break;
		case 3:
			this->DrawLaserSiteThingie(0, *host);
			if (--this->field_230 <= 0)
			{
				if (!Mem_RecoverPointer(&this->mHandle) || !Utils_LineOfSight(&this->mPos, &(*host)->mPos, 0, 0))
				{
					this->field_31C.bothFlags = 28;
					this->dumbAssPad = 0;
					break;
				}
				CVector positions[2];
				positions[0] = this->mPos;
				positions[1] = (*host)->mPos;
				Camera_SelectOptimumViewingNode(2, positions);
				this->RunAnim((this->mType != 304) + 7, 0, -1);
				new CMessage(this, *host, 3, 0);
				this->dumbAssPad++;
			}
			break;
		case 4:
			this->DrawLaserSiteThingie(0, *host);
			if (this->mAnimFinished)
			{
				SFX_PlayPos(0x8024, &this->mPos, 0);
				this->field_31C.bothFlags = 10;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @NotOk
// Native comparison passed 40000 cases; instruction matching is pending.
// 0x4D67F0
// Original shot frame table at 0x552168 contains 2, 6, 10 and 14.
i32 CThug::DrawLaserSiteThingie(i32 checkPlayer, CBaddy* target)
{
	static const u16 shotFrames[4] = {2, 6, 10, 14};
	CVector direction;
	SLineInfo line;
	delete this->field_3A0;
	this->field_3A0 = 0;
	if (this->mType != 304)
		return this->DrawLaserSiteThingieForAlternateModel(checkPlayer);
	M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&line.StartCoords), this, 1);
	M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&line.EndCoords), this, 0);
	direction = line.EndCoords - line.StartCoords;
	line.StartCoords += direction;
	direction <<= 6;
	line.EndCoords += direction;
	if (checkPlayer)
		this->RotateTorsoToAimAtPlayer(line.StartCoords);
	if (this->mAnim != 7)
		return 0;
	i32 shot = (static_cast<u32>(this->field_218) >> 12) & 7;
	if (shot >= 4 || this->mFrame < shotFrames[shot])
		return 0;
	if (!this->field_338)
		this->field_344 = 66;
	SFX_PlayPos((Rnd(2) + 2) | 0x8000, &this->mPos, 500);
	this->field_218 = (this->field_218 & ~0x7000) | ((shot + 1) << 12);
	if (checkPlayer)
		this->AutoAimPlease(&line.StartCoords, &line.EndCoords, &direction, this->field_380);
	else if (target)
	{
		line.EndCoords = target->mPos;
		line.EndCoords.vy -= 40 << 12;
		direction = line.EndCoords - line.StartCoords;
	}
	i32 hit = this->LaserCollision(&line, &direction, checkPlayer);
	if (hit == -1)
	{
		delete this->field_3A0;
		this->field_3A0 = 0;
		return 0;
	}
	if (target)
		new CThugBulletTracer(line.StartCoords, line.EndCoords, target, 0, 100, 100, 255);
	else if (hit)
		new CThugBulletTracer(line.StartCoords, line.EndCoords, G_MECHLIST_PLAYER, 0, 100, 100, 255);
	else
		new CThugBulletTracer(line.StartCoords, line.EndCoords, 0, &line, 100, 100, 255);
	if (!(this->mFlags & 0x8000))
		this->DrawBarrelFlash(&line.StartCoords, &line.EndCoords, &line, 100, 100, 255);
	return hit;
}

// @NotOk
// Native comparison passed 40000 cases; instruction matching is pending.
// 0x4D6330
// Original shot frame table at 0x552170 contains 6 and 11.
i32 CThug::DrawLaserSiteThingieForAlternateModel(i32 checkPlayer)
{
	static const u16 shotFrames[2] = {6, 11};
	CVector direction;
	SLineInfo line;
	i32 firstHit = 0;
	i32 secondHit = 0;
	if (this->mAnim != 8)
		return 0;
	i32 shot = (static_cast<u32>(this->field_218) >> 12) & 7;
	if (shot >= 2 || this->mFrame < shotFrames[shot])
		return 0;
	if (!this->field_338)
		this->field_344 = 66;
	SFX_PlayPos((Rnd(2) + 9) | 0x8000, &this->mPos, 500);
	shot++;
	this->field_218 = (this->field_218 & ~0x7000) | (shot << 12);
	if (shot == 1)
	{
		M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&line.StartCoords), this, 2);
		M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&line.EndCoords), this, 3);
		direction = line.EndCoords - line.StartCoords;
		line.StartCoords += direction * 3;
		direction <<= 8;
		line.EndCoords += direction;
		this->AutoAimPlease(&line.StartCoords, &line.EndCoords, &direction, this->field_380);
		firstHit = this->LaserCollision(&line, &direction, checkPlayer);
		if (firstHit == -1)
			return secondHit;
		if (firstHit)
			new CThugBulletTracer(line.StartCoords, line.EndCoords, G_MECHLIST_PLAYER, 0, 255, 128, 0);
		else
			new CThugBulletTracer(line.StartCoords, line.EndCoords, 0, &line, 255, 128, 0);
		if (this->mFlags & 0x8000)
			return firstHit | secondHit;
		new CGlowFlash(&line.StartCoords, 5, 255, 128, 0, 32, 0, 0, 0, 0, 50, 20, 1, 20, 10, 40, 20, 10, 1);
	}
	else
	{
		M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&line.StartCoords), this, 0);
		M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&line.EndCoords), this, 1);
		direction = line.EndCoords - line.StartCoords;
		line.StartCoords += direction * 3;
		direction <<= 8;
		line.EndCoords += direction;
		this->AutoAimPlease(&line.StartCoords, &line.EndCoords, &direction, this->field_380);
		secondHit = this->LaserCollision(&line, &direction, checkPlayer);
		if (secondHit == -1)
			return firstHit;
		new CThugBulletTracer(line.StartCoords, line.EndCoords, 0, &line, 255, 128, 0);
		if (this->mFlags & 0x8000)
			return firstHit | secondHit;
		new CGlowFlash(&line.StartCoords, 5, 255, 128, 0, 32, 0, 0, 0, 0, 50, 20, 1, 20, 10, 40, 20, 10, 1);
	}
	this->SetUpLaser(&this->field_3A0, &line.StartCoords, &line.EndCoords);
	return firstHit | secondHit;
}

// @NotOk
// 0x4D5FA0. Native comparison passed 30000 cases.
// Instruction matching and full AI runtime checks are pending.
i32 CThug::LaserCollision(SLineInfo* line, CVector* direction, i32 checkPlayer)
{
	SLineInfo* info = line;
	i32 hitPlayer = 0;
	CVector start = info->StartCoords;
	CVector end = info->EndCoords;
	info->EndCoords = start;
	info->StartCoords = this->mPos;
	M3dColij_InitLineInfo(info);
	M3dZone_LineToItem(info, 1);
	if (info->pItem)
		return -1;
	info->StartCoords = start;
	info->EndCoords = end;
	if (checkPlayer && Utils_CheckObjectCollision(&info->StartCoords, &info->EndCoords, G_MECHLIST_PLAYER, 0))
	{
		CVector toPlayer;
		toPlayer = G_MECHLIST_PLAYER->mPos - info->StartCoords;
		i32 distance = toPlayer.Length() << 12;
		i32 scale = distance / direction->Length();
		*direction >>= 12;
		*direction *= scale;
		hitPlayer = 1;
		info->EndCoords = info->StartCoords + *direction;
		// The original reuses the first argument slot for this shift at
		// 0x4D60D1, then compares the hit item to that slot at 0x4D6138.
		line = reinterpret_cast<SLineInfo*>(12);
	}
	M3dColij_InitLineInfo(info);
	M3dZone_LineToItem(info, 1);
	if (info->pItem && info->pItem != reinterpret_cast<CItem*>(line)
		&& info->pItem != G_MECHLIST_PLAYER && info->pItem != this)
	{
		info->EndCoords = info->Position;
		return 0;
	}
	if (!hitPlayer)
	{
		*direction >>= 3;
		info->EndCoords = info->StartCoords + *direction;
		return 0;
	}
	SFX_PlayPos(((this->mType != 304 ? 37 : 6) + Rnd(2)) | 0x8000, &G_MECHLIST_PLAYER->mPos, 0);
	return hitPlayer;
}

// @NotOk
// 0x4D5E40. Native comparison passed 40000 cases.
// Instruction matching and full AI runtime checks are pending.
void CThug::AutoAimPlease(CVector* origin, CVector* target, CVector* direction, i32 spread)
{
	*direction = G_MECHLIST_PLAYER->mPos - *origin;
	this->field_3A8 = ((G_MECHLIST_PLAYER->mPos.vy - this->field_3A8) >> ((this->mType != 304) + 1)) + this->field_3A8;
	direction->vy += this->field_3A8 - G_MECHLIST_PLAYER->mPos.vy;
	while ((my_abs(direction->vz)) + (my_abs(direction->vx)) < 0xBB8000)
	{
		*direction <<= 1;
		spread <<= 1;
	}
	if (direction->vx > direction->vz)
		direction->vz += (Rnd(spread) - (spread >> 1)) << 12;
	else
		direction->vx += (Rnd(spread) - (spread >> 1)) << 12;
	direction->vy += (Rnd(spread) - (spread >> 1)) << 12;
	*target = *origin + *direction;
}

// @NotOk
// 0x4D6BC0. Native comparison passed 30000 cases.
// Instruction matching and full AI runtime checks are pending.
void CThug::RotateTorsoToAimAtPlayer(CVector& origin)
{
	i32 height = (G_MECHLIST_PLAYER->mPos.vy - origin.vy) >> 12;
	i32 angle;
	if (!height)
		angle = 0;
	else
	{
		u32 distance = Utils_CrapDist(G_MECHLIST_PLAYER->mPos, origin);
		angle = Utils_ArcCos((this->DistanceToPlayer(1) << 12) / distance);
		if (height < 0)
			angle = -angle;
	}
	SJoint* joints = this->mpJoints;
	if (!joints)
	{
		this->mFlags |= 4;
		i16* pose = reinterpret_cast<i16*>(0x557CE4);
		if (this->mType != 304)
			pose = reinterpret_cast<i16*>(0x557DBC);
		this->ApplyPose(pose);
		return;
	}
	i32 change = angle - joints[1].Angles.vz;
	if (change > 8)
		change >>= 3;
	joints[1].Angles.vz += change;
	if (this->mpJoints[1].Angles.vz)
	{
		this->mFlags |= 4;
		i16* pose = reinterpret_cast<i16*>(0x557CE4);
		if (this->mType != 304)
			pose = reinterpret_cast<i16*>(0x557DBC);
		this->ApplyPose(pose);
	}
	else
		this->mFlags &= ~4;
}

// @NotOk
// 0x4D78D0. Native comparison passed 40000 cases.
// Instruction matching and full AI runtime checks are pending.
void CThug::GetTrapped(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
		{
			new CAIProc_StateSwitchSendMessage(this, 14);
			CThug* buddy = reinterpret_cast<CThug*>(this->GetClosest(304, 0));
			if (!buddy)
				buddy = reinterpret_cast<CThug*>(this->GetClosest(312, 0));
			if (buddy && !buddy->field_330)
				new CMessage(this, buddy, 7, 0);
			SFX_PlayPos(0x800E, &this->mPos, 0);
			this->field_310 = 0;
			this->ClearAttackFlags();
			this->RunAnim(this->mType != 304 ? 14 : 16, 0, -1);
			++this->dumbAssPad;
			break;
		}
		case 1:
			if (this->mAnimFinished)
			{
				this->field_364 = 0;
				this->field_368 = 0;
				this->field_1F8 = 5;
				this->dumbAssPad = 2;
				this->CycleAnim(this->mType != 304 ? 15 : 17, 1);
				SFX_PlayPos((Rnd(2) + 43) | 0x8000, &this->mPos, 0);
			}
			break;
		case 2:
			if (this->field_368 > 0)
				--this->field_368;
			if (--this->field_1F8 <= 0)
			{
				CTrapWebEffect* web = reinterpret_cast<CTrapWebEffect*>(Mem_RecoverPointer(&this->field_104));
				if (!web || web->field_44->mNumSegs == this->field_364)
					++this->dumbAssPad;
				else
				{
					this->field_368 += (8000 * (web->field_44->mNumSegs - this->field_364)) >> 12;
					i32 strands = web->field_44->mNumSegs;
					this->field_1F8 = 5;
					this->field_364 = strands;
					if (web->field_44->mNumSegs >= 80)
					{
						this->field_31C.bothFlags = 26;
						this->dumbAssPad = 0;
						this->SendDeathPulse();
						this->mHealth = 0;
						CPlayer* player = G_MECHLIST_PLAYER;
						i32 value = player->field_528;
						player->field_534 = 360;
						player->field_52C = (value + 11) << 10;
						this->field_218 |= 0xC0;
					}
				}
			}
			break;
		case 3:
			if (--this->field_368 <= 0)
			{
				CTrapWebEffect* web = reinterpret_cast<CTrapWebEffect*>(Mem_RecoverPointer(&this->field_104));
				if (web)
					web->Burst();
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @Matching
// 0x4D8320. Native comparison passed 40000 cases.
void CThug::GetWhippedLikeTheWhoreYouAre(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			++this->dumbAssPad;
			this->RunAnim(this->mType != 304 ? 32 : 37, 0, -1);
			this->field_1F8 = 0;
			// fall through
		case 1:
		{
			this->field_3AC |= 2;
			print_if_false(this->field_3A4 != 0, "Pointer to mpThrowPoints is NULL.");
			i32 path = this->PathCheck(&this->mPos, &this->field_3A4[this->field_1F8], 0, 55);
			if (path || !this->AddPointToPath(&this->field_3A4[this->field_1F8], 0)
				|| ++this->field_1F8 >= 8)
			{
				i32 fall = this->ShouldFall(200, 389120);
				if (fall)
				{
					if (fall == -1 || fall > 1024000)
					{
						this->field_31C.bothFlags = 22;
						this->dumbAssPad = 0;
						this->field_218 |= 2;
					}
					else
						this->dumbAssPad = 4;
				}
				else
				{
					if (path)
						this->RunAnim(this->mType != 304 ? 19 : 14, 0, -1);
					++this->dumbAssPad;
				}
			}
			else
				this->mPos = this->field_3A4[this->field_1F8 - 1];
			break;
		}
		case 2:
			if (this->mAnimFinished)
			{
				this->mHealth -= 50;
				if (this->mHealth > 0)
				{
					this->RunAnim(this->mType != 304 ? 20 : 15, 0, -1);
					++this->dumbAssPad;
				}
				else
				{
					this->field_31C.bothFlags = 26;
					this->dumbAssPad = 0;
				}
			}
			break;
		case 3:
			if (this->mAnimFinished)
			{
				this->mCBodyFlags |= 0x10;
				this->field_31C.bothFlags = 0;
				this->dumbAssPad = 0;
			}
			break;
		case 4:
			this->mPos.vy += 409600;
			if (this->mPos.vy > this->field_308)
			{
				SFX_PlayPos(0x802E, &this->mPos, 0);
				this->mPos.vy = this->field_308;
				this->dumbAssPad = 2;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @NotOk
// 0x4D9200. Native comparison passed 40000 cases.
// Instruction matching and full AI runtime checks are pending.
void CThug::Fall(void)
{
	this->field_3AC |= 2;
	switch (this->dumbAssPad)
	{
		case 0:
			this->ClearAttackFlags();
			this->field_310 = 0;
			if (!(this->field_218 & 0x40002) && !Rnd(3))
			{
				this->dumbAssPad = 3;
				return;
			}
			// fall through
		case 1:
			this->mAcc.vy = 147456;
			new CAIProc_Fall(this, 4);
			if (!(this->field_218 & 2))
				this->CycleAnim(this->mType != 304 ? 15 : 33, 1);
			this->field_2A8 &= ~1;
			++this->dumbAssPad;
			this->field_1F8 = 65;
			if (!(this->field_2A8 & 0x400) || this->field_308 > this->mPos.vy + 1024000)
			{
				CVector sound;
				sound.vx = ((1600 * this->mPos.vx) >> 12) + ((2400 * G_MECHLIST_PLAYER->mPos.vx) >> 12);
				sound.vy = ((1600 * this->mPos.vy) >> 12) + ((2400 * G_MECHLIST_PLAYER->mPos.vy) >> 12);
				sound.vz = ((1600 * this->mPos.vz) >> 12) + ((2400 * G_MECHLIST_PLAYER->mPos.vz) >> 12);
				SFX_PlayPos(0x8018, &sound, 0);
			}
			return;
		case 2:
			this->mAngles.vx += 40;
			if (this->mVel.vy >= 1228800)
				this->mVel.vy = 1228800;
			this->RunTimer(&this->field_1F8);
			if (this->field_288 & 4)
			{
				this->field_288 &= ~4;
				this->Neutralize();
				this->SendDeathPulse();
				this->mHealth = 0;
				this->field_2A8 |= 0x8000;
				SFX_Play(0x802E, 0x2000, 0);
				this->field_1F8 = 50;
				this->dumbAssPad = 5;
			}
			else if (!this->field_1F8)
			{
				this->Neutralize();
				this->SendDeathPulse();
				this->mHealth = 0;
				SFX_Play(0x802E, 0x2000, 0);
				this->field_1F8 = 50;
				this->dumbAssPad = 6;
			}
			return;
		case 3:
			this->field_1F8 = 0;
			SFX_PlayPos(0x8010, &this->mPos, 0);
			this->CycleAnim(this->field_298.Bytes[0], 1);
			++this->dumbAssPad;
			return;
		case 4:
			if (this->mpJoints)
			{
				i16* joints = reinterpret_cast<i16*>(this->mpJoints);
				if (!this->field_1F8)
				{
					joints[6] += 90;
					if (joints[6] >= 900)
					{
						this->field_1F8 = 1;
						return;
					}
				}
				else
				{
					joints[6] -= 90;
					if (joints[6] <= 0)
					{
						this->mFlags &= ~4;
						this->dumbAssPad = 1;
						return;
					}
				}
			}
			this->mFlags |= 4;
			// Pose packets selected by model type in the original.
			this->ApplyPose(reinterpret_cast<i16*>(this->mType != 304 ? 0x557DBC : 0x557CE4));
			return;
		case 5:
			this->mFlags |= 1;
			this->RunTimer(&this->field_1F8);
			if (!this->field_1F8)
			{
				SFX_PlayPos(0x802C, &this->mPos, 0);
				this->field_31C.bothFlags = 27;
				this->dumbAssPad = 0;
			}
			return;
		case 6:
			this->mFlags |= 1;
			this->RunTimer(&this->field_1F8);
			if (!this->field_1F8)
			{
				SFX_PlayPos(0x802C, &this->mPos, 0);
				this->field_2A8 |= 0x8000;
				this->field_31C.bothFlags = 27;
				this->dumbAssPad = 0;
			}
			return;
		default:
			print_if_false(0, "Unknown substate!");
			return;
	}
}

// @NotOk
// 0x4D8E50. Native comparison passed 40000 cases.
// Instruction matching and full AI runtime checks are pending.
void CThug::DieThug(i32 instant)
{
	switch (this->dumbAssPad)
	{
		case 0:
			G_MECHLIST_PLAYER->NotifyKill(this->mType);
			this->SetHeight(1, 100, 600);
			this->ClearAttackFlags();
			this->field_310 = 0;
			this->Neutralize();
			this->mCBodyFlags &= ~0x10;
			this->field_2A8 |= 0x41000;
			if (instant)
			{
				this->Die(0);
				this->dumbAssPad = 3;
			}
			else if (this->field_218 & 0x80)
			{
				this->RunAnim(this->mType != 304 ? 29 : 39, 0, -1);
				this->dumbAssPad = 5;
			}
			else
			{
				u16 anim = this->mAnim;
				if (this->mType == 304)
				{
					if (anim != 26 && anim != 14 && anim != 37)
					{
						this->CheckFallBack();
						this->RunAnim(this->field_2A8 & 0x10 ? 26 : 14, 0, -1);
					}
				}
				else if (anim != 26 && anim != 19 && anim != 32)
				{
					this->CheckFallBack();
					this->RunAnim(this->field_2A8 & 0x10 ? 26 : 19, 0, -1);
				}
				this->field_1F8 = 0;
				++this->dumbAssPad;
			}
			break;
		case 1:
			this->SetHeight(0, 100, 600);
			if (this->mAnimFinished)
			{
				this->mRMinor = 0;
				this->Die(1);
				this->field_1F8 = 0;
				++this->dumbAssPad;
			}
			break;
		case 2:
			this->SetHeight(0, 100, 600);
			if (this->Die(2))
			{
				this->Die(3);
				++this->dumbAssPad;
			}
			break;
		case 3:
			break;
		case 5:
			if (this->mAnimFinished)
			{
				this->RunAnim(this->mType != 304 ? 35 : 40, 0, -1);
				++this->dumbAssPad;
			}
			break;
		case 6:
			if (this->mAnimFinished)
			{
				this->field_1F8 = 0;
				this->dumbAssPad = 7;
			}
			break;
		case 7:
			this->field_1F8 += this->field_80;
			if (this->field_1F8 >= 10)
			{
				if (!(this->field_2A8 & 0x4000))
					this->SendDeathPulse();
				this->CycleAnim(this->mType != 304 ? 36 : 41, 1);
				this->field_1F8 = 120;
				this->field_34C = Rnd(300) + 150;
				++this->dumbAssPad;
			}
			break;
		case 8:
			this->RunTimer(&this->field_1F8);
			this->RunTimer(&this->field_34C);
			if (!this->field_1F8)
			{
				this->RunAnim(this->mAnim, 0, -1);
				this->dumbAssPad = 1;
			}
			else if (!this->field_34C)
			{
				this->field_34C = Rnd(300) + 150;
				SFX_PlayPos((Rnd(2) + 43) | 0x8000, &this->mPos, 0);
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @NotOk
// 0x4D3B00. Native comparison passed 40000 cases.
// Instruction matching and full AI runtime checks are pending.
void CThug::SlideFromHit(i32 distance, i32 frames, CVector& direction)
{
	CVector start;
	CVector velocity;
	CVector target;
	CVector hit;
	print_if_false(frames != 0, "Illegal to specify 0 frames for HITINFO slide frames.");
	u32 flags = this->field_2A8 & ~0x10;
	this->field_1F8 = frames;
	this->field_2A8 = flags;
	velocity = direction;
	velocity.vx *= distance;
	velocity.vz *= distance;
	velocity.vy = 0;
	start = this->mPos;
	start.vy += (this->field_21E - 20) << 12;
	target = this->mPos + velocity;
	velocity.vx /= this->field_1F8;
	velocity.vz /= this->field_1F8;
	i32 result = this->PathCheck(&start, &target, &hit, 55);
	if (result == 2)
	{
		i32 x = hit.vx - start.vx;
		x = my_abs(x);
		i32 z = hit.vz - start.vz;
		z = my_abs(z);
		if (x > z)
			this->field_1F8 = x / (my_abs(velocity.vx));
		else
			this->field_1F8 = z / (my_abs(velocity.vz));
		if (this->field_1F8 < 4)
			return;
		target.vx = this->mPos.vx + velocity.vx * this->field_1F8;
		target.vy = this->mPos.vy;
		target.vz = this->mPos.vz + velocity.vz * this->field_1F8;
	}
	else if (result == 4)
		return;
	if (this->AddPointToPath(&this->mPos, 0) && this->AddPointToPath(&target, 0))
		this->field_2A8 &= ~0x10000000;
	else
		this->mHealth = 0;
	this->Neutralize();
	if (this->mHealth <= 0 && this->field_1F8 >= 8)
	{
		if (this->field_218 & 0x30000)
		{
			this->field_218 |= 0x40000;
			CSVector angles;
			Utils_CalcAim(&angles, &target, &this->mPos);
			i32 angle = angles.vy - this->mAngles.vy;
			if (angle < -2048)
				angle += 4096;
			else if (angle > 2048)
				angle -= 4096;
			this->field_324 = 0;
			if (this->field_218 & 0x10000)
				this->field_328 = (angle - 4096) / this->field_1F8;
			else
				this->field_328 = (angle + 4096) / this->field_1F8;
		}
		else
		{
			i32 anim = G_MECHLIST_PLAYER->mAnim;
			if (anim == 106 || anim == 113 || anim == 284)
			{
				this->field_218 |= 0x40000;
				this->field_1F8 *= 2;
				velocity /= 2;
				this->field_324 = 4096 / this->field_1F8;
				this->field_328 = 0;
				CVector look;
				if (Rnd(2))
				{
					this->field_324 = -this->field_324;
					look = this->mPos - velocity * (2 * this->field_1F8);
				}
				else
					look = this->mPos + velocity * (2 * this->field_1F8);
				new CAIProc_LookAt(this, 0, &look, 0, 80, 200);
			}
		}
	}
	CVector* motion = &this->mVel;
	this->field_31C.bothFlags = 17;
	this->dumbAssPad = 0;
	*motion = velocity;
}

// @NotOk
// 0x4D4680. Native comparison passed 60000 cases.
// Instruction matching and full AI runtime checks are pending.
void CThug::ChasePlayer(i32 depth)
{
	SMoveToInfo move;
	CVector target;
	CVector hit;
	i32 result;
	i32 height;
	i32 timed;
	u32 distance;
	print_if_false(depth < 5, "Matt needs to fix ChasePlayer.");
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->mCBodyFlags |= 0x10;
			this->field_364 = 0;
			if (this->mType != 304 && this->field_3B4 > 600)
			{
				this->field_31C.bothFlags = 11;
				this->dumbAssPad = 0;
				break;
			}
			// fall through
		case 1:
			this->field_310 = 160;
			if (this->SetAnimMode(1, 1))
			{
				this->dumbAssPad = 5;
				break;
			}
			this->SetAttackFlags();
			this->GetAttackPosition(&target);
			if (this->DistanceToPlayer(2) < (this->mType != 304 ? 190 : 140))
			{
				height = this->mPos.vy - G_MECHLIST_PLAYER->mPos.vy;
				if (my_abs(height) < 204800)
				{
					if (this->field_3BC & 1)
						goto attackOrGuard;
					this->dumbAssPad = 20;
					break;
				}
			}
			result = this->PathCheck(&this->mPos, &target, &hit, 55);
			if (result == 2)
			{
				if (Utils_CrapDist(hit, G_MECHLIST_PLAYER->mPos)
					< (this->mType != 304 ? 190 : 140))
				{
					target = hit;
					goto addTarget;
				}
			}
			else if (!result)
			{
addTarget:
				if (this->AddPointToPath(&target, 0)
					|| (this->AddPointToPath(&this->mPos, 0) && this->AddPointToPath(&target, 0)))
				{
					this->mAcc.vz = 0;
					this->field_27C.vz = 0;
					goto advance;
				}
			}
			++this->field_3BE;
			this->Neutralize();
			if (G_MECHLIST_PLAYER->field_57C || !(this->field_3BC & 1)
				|| G_THUG_LIST || G_MECHLIST_PLAYER->mHeldObject
				|| this->DistanceToPlayer(2) <= 650 || this->DistanceToPlayer(2) >= 1500)
				this->field_31C.bothFlags = 1;
			else
			{
				G_THUG_LIST = this;
				this->field_31C.bothFlags = 9;
			}
			this->dumbAssPad = 0;
			this->ClearAttackFlags();
			break;
		case 2:
			print_if_false(this->field_1F0 != 0, "What, no waypoint in cache?");
			move.field_0 = this->field_1A8[this->field_1F0];
			move.field_C = this->mType != 304 ? 144 : 192;
			move.field_10 = 70;
			move.field_14 = 193;
			new CAIProc_MoveTo(this, &move, 1);
advance:
			++this->dumbAssPad;
			if (this->field_31C.bothFlags == 4)
				this->ChasePlayer(depth + 1);
			break;
		case 3:
			if ((this->field_3BC & 1)
				&& this->DistanceToPlayer(0) < (this->mType != 304 ? 190 : 140))
			{
				this->MarkAIProcList(1, 0, 0);
				this->mAcc.vz = 0;
				this->mAcc.vy = 0;
				this->mAcc.vx = 0;
				this->field_31C.bothFlags = 5;
				this->dumbAssPad = 0;
				break;
			}
			this->RunAppropriateAnim();
			if (this->field_288 & 1)
			{
				this->field_288 &= ~1;
				goto advance;
			}
			if (this->field_1F8++ <= 6)
			{
				timed = 0;
				if (this->DistanceToPlayer(2) >= (this->mType != 304 ? 285 : 210))
					break;
			}
			else
				timed = 1;
			this->SetAttackFlags();
			if (this->DistanceToPlayer(2) < 800)
			{
				if (timed)
					++this->field_364;
				if (this->field_364 >= 6)
				{
					--this->field_364;
					if (!G_MECHLIST_PLAYER->field_57C && !G_THUG_LIST && !G_MECHLIST_PLAYER->mHeldObject)
						goto shoot;
				}
			}
			if (!(this->field_3BC & 1))
				break;
			if (this->DistanceToPlayer(2) < (this->mType != 304 ? 190 : 140))
			{
				height = this->mPos.vy - G_MECHLIST_PLAYER->mPos.vy;
				if (my_abs(height) < 204800)
				{
attackOrGuard:
					this->dumbAssPad = 0;
					this->field_31C.bothFlags = G_MECHLIST_PLAYER->field_57C ? 1 : 5;
					break;
				}
			}
			distance = Utils_CrapXZDist(this->mPos, this->field_1A8[this->field_1F0]);
			if (this->DistanceToPlayer(2) + 50 < distance)
			{
				if (G_MECHLIST_PLAYER->field_57C || G_MECHLIST_PLAYER->mHeldObject
					|| G_THUG_LIST || this->DistanceToPlayer(2) <= 650 || Rnd(5))
				{
					this->MarkAIProcList(1, 0, 0);
					this->mAcc.vz = 0;
					this->mAcc.vy = 0;
					this->mAcc.vx = 0;
					this->dumbAssPad = 1;
				}
				else
					goto shoot;
			}
			this->field_1F8 = 0;
			break;
		case 4:
			if ((this->field_3BC & 1)
				&& this->DistanceToPlayer(2) < (this->mType != 304 ? 190 : 140))
			{
				height = this->mPos.vy - G_MECHLIST_PLAYER->mPos.vy;
				if (my_abs(height) < 204800)
				{
					this->Neutralize();
					this->RunAnim(this->field_298.Bytes[0], 0, -1);
					this->dumbAssPad = 0;
					this->field_31C.bothFlags = G_MECHLIST_PLAYER->field_57C ? 1 : 5;
					break;
				}
			}
			this->CycleAnim(this->field_298.Bytes[0], 1);
			this->dumbAssPad = 20;
			break;
		case 5:
			if (this->mAnimFinished)
				this->dumbAssPad = 1;
			break;
		case 20:
			this->field_310 = 160;
			this->Neutralize();
			this->CycleAnim(this->field_298.Bytes[0], 1);
			new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 2, 100, 200);
			++this->dumbAssPad;
			break;
		case 21:
			this->field_3AC |= 4;
			if (this->field_288 & 2)
			{
				this->field_1F8 = 30;
				this->field_288 &= ~2;
				this->dumbAssPad = 22;
			}
			break;
		case 22:
			this->RunTimer(&this->field_1F8);
			if (this->field_1F8)
			{
				this->field_3AC |= 4;
				break;
			}
			if (G_MECHLIST_PLAYER->field_57C || G_THUG_LIST || this->DistanceToPlayer(2) >= 2000
				|| G_MECHLIST_PLAYER->mHeldObject)
			{
				this->dumbAssPad = 1;
				break;
			}
			if (this->DistanceToPlayer(2) <= 650)
			{
				height = G_MECHLIST_PLAYER->mPos.vy - this->field_29C - 0x4000;
				if (my_abs(height) <= 409600)
				{
					this->dumbAssPad = 1;
					break;
				}
			}
shoot:
			this->Neutralize();
			G_THUG_LIST = this;
			this->field_31C.bothFlags = 9;
			this->dumbAssPad = 0;
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @NotOk
// 0x4D34A0. Native comparison passed 50000 cases.
// Instruction matching is pending.
i32 CThug::SetAttackFlags(void)
{
	this->ClearAttackFlags();
	if (G_MECHLIST_PLAYER->field_57C)
		return 0;
	i32 distance = this->DistanceToPlayer(2);
	if (distance < 819200)
	{
		CThug* attacker = G_GLOBAL_THUG;
		if (attacker)
		{
			i32 height = G_MECHLIST_PLAYER->mPos.vy - attacker->field_29C - 0x4000;
			if (my_abs(height) < 409600)
			{
				if (distance < attacker->DistanceToPlayer(2) - 150)
				{
					if (G_GLOBAL_THUG->field_31C.bothFlags == 4
						|| G_GLOBAL_THUG->field_31C.bothFlags == 5)
						G_GLOBAL_THUG->DetermineFightState();
					G_GLOBAL_THUG->ClearAttackFlags();
				}
				attacker = G_GLOBAL_THUG;
			}
			if (attacker)
				goto waitingSlot;
		}
		i32 height = G_MECHLIST_PLAYER->mPos.vy - this->field_29C - 0x4000;
		if (my_abs(height) < 409600)
		{
			G_GLOBAL_THUG = this;
			this->field_3BC = 1;
			return 1;
		}
	}
waitingSlot:
	i32 positiveX = this->mPos.vx - G_MECHLIST_PLAYER->mPos.vx > 0;
	i32 positiveZ = this->mPos.vz - G_MECHLIST_PLAYER->mPos.vz > 0;
	i32 direction = 1 << (this->field_3BE & 7);
	if (positiveX)
	{
		if (positiveZ)
		{
			if (direction & 0x70)
				this->field_3BE = 7;
		}
		else if (direction & 0xC1)
			this->field_3BE = 1;
	}
	else if (positiveZ)
	{
		if (direction & 0x1C)
			this->field_3BE = 5;
	}
	else if (direction & 7)
		this->field_3BE = 3;
	i32 count = 0;
	u8 slot = this->field_3BE;
	i32 index;
	for (;; ++slot)
	{
		index = slot & 7;
		if (!((1 << index) & G_ATTACK_FLAG_RELATED))
			break;
		if (++count >= 8)
		{
			this->field_3BC = 0;
			return 0;
		}
	}
	this->field_3BE = index;
	this->field_3BD = 1 << index;
	G_ATTACK_FLAG_RELATED |= 1 << index;
	this->field_3BC = 2;
	this->SetAnimMode(2, 0);
	return 1;
}

// @Ok
// @AlmostMatching: the attack-mask AND/store use DL instead of AL,
// adding one byte. Both have 281 instructions. Tried 12 mask expressions.
// 0x4D5110. Native comparison passed 3584 patrol cases.
void CThug::FollowWaypoints(void)
{
	SMoveToInfo move;
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->mCBodyFlags |= 0x10;
			this->field_2A8 &= ~0x800;
			this->field_310 = 0;
			if (this->field_1F0)
			{
				this->field_2A8 &= ~0x10000000;
				move.field_0 = this->field_1A8[this->field_1F0];
				this->field_2F0 = (this->field_2F0 & ~2) | 1;
			}
			else
			{
				this->field_2A8 |= 0x10000000;
				Trig_GetPosition(&move.field_0, this->field_1F4);
				if (G_GLOBAL_THUG == this)
					G_GLOBAL_THUG = 0;
				else if (this->field_3BC & 2)
					G_ATTACK_FLAG_RELATED &= ~this->field_3BD;
				this->field_3BC = 0;
				this->field_3BD = 0;
			}
			if (this->field_2F0 & 1)
			{
				this->SetAnimMode(1, 0);
				move.field_10 = 70;
				move.field_14 = 193;
				move.field_C = this->mType != 304 ? 144 : 192;
				this->field_2F0 &= ~1;
			}
			else
			{
				this->SetAnimMode(0, 1);
				move.field_10 = 70;
				move.field_14 = 500;
				move.field_C = this->mType != 304 ? 48 : 120;
			}
			new CAIProc_MoveTo(this, &move, 1);
			++this->dumbAssPad;
			break;
		case 1:
			this->RunAppropriateAnim();
			if (!(this->field_288 & 1))
				return;
			this->field_288 &= ~1;
			if (this->field_31C.bothFlags == 24)
			{
				if (this->field_330)
				{
					this->field_31C.bothFlags = 23;
					this->dumbAssPad = 0;
					return;
				}
				this->field_31C.bothFlags = 2;
			}
			if (this->field_1F0)
				--this->field_1F0;
			else
			{
				this->field_218 &= ~1;
				if (!this->GetNextWaypoint())
				{
					this->field_31C.bothFlags = 1;
					this->dumbAssPad = 0;
					return;
				}
			}
			if (!this->field_330 || !this->DetermineFightState())
			{
				if (!(this->field_2F0 & 2))
				{
					this->dumbAssPad = 0;
					return;
				}
				if (this->mType == 304)
					this->RunAnim(10, 0, -1);
				else
				{
					if (Rnd(2))
					{
						this->RunAnim(21, 0, -1);
						this->dumbAssPad = 5;
						return;
					}
					this->RunAnim(22, 0, -1);
				}
				++this->dumbAssPad;
			}
			break;
		case 2:
			if (this->mAnimFinished)
			{
				this->CycleAnim(this->mType == 304 ? 11 : 0, 1);
				this->field_230 = 50;
				++this->dumbAssPad;
			}
			break;
		case 3:
			if (this->field_230-- <= 0)
			{
				if (this->mType == 304)
				{
					this->RunAnim(12, 0, -1);
					++this->dumbAssPad;
				}
				else
					this->dumbAssPad = 0;
			}
			break;
		case 4:
			if (this->mAnimFinished)
				this->dumbAssPad = 0;
			break;
		case 5:
			if (this->MonitorSpitPlease())
				this->dumbAssPad = 2;
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @Matching
// 0x4D5720
void CThug::AttackPlayer(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_310 = 160;
			this->Neutralize();
			this->SetAnimMode(3, 1);
			this->mCBodyFlags |= 0x10;
			++this->dumbAssPad;
			// fall through
		case 1:
			if (G_MECHLIST_PLAYER->mHealth <= 0)
			{
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
				break;
			}
			new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 2, 100, 0);
			this->field_1F8 = 120;
			++this->dumbAssPad;
			break;
		case 2:
			if (this->mAnimFinished)
				this->CycleAnim(this->field_298.Bytes[0], 1);
			if (!this->RunTimer(&this->field_1F8))
			{
				this->Neutralize();
				this->field_31C.bothFlags = 4;
				this->dumbAssPad = 0;
				return;
			}
			if (this->field_288 & 2)
			{
				this->field_288 &= ~2;
				this->MarkAIProcList(0, 256, 0);
				this->mAngVel.vy = 0;
				this->mAngAcc.vy = 0;
				if (this->mType == 304)
				{
					if (this->DistanceToPlayer(0) < 120)
					{
						this->RunAnim(5, 0, -1);
						new CAIProc_MonitorAttack(this, 0, 136, 5, 0);
					}
					else if (Rnd(2))
					{
						this->RunAnim(22, 0, -1);
						new CAIProc_MonitorAttack(this, 4, 10, 10, 0);
					}
					else
					{
						this->RunAnim(23, 0, -1);
						new CAIProc_MonitorAttack(this, 3, 4096, 7, 0);
					}
				}
				else
				{
					this->RunAnim(13, 0, -1);
					new CAIProc_MonitorAttack(this, 11, 23552, 10, 0);
				}
				SFX_PlayPos(0x8014, &this->mPos, 0);
				++this->dumbAssPad;
			}
			return;
		case 3:
			if (!this->mAnimFinished)
				return;
			this->CycleAnim(this->field_298.Bytes[0], 1);
			++this->dumbAssPad;
			if (this->TooCloseToSpidey())
				return;
			// fall through
		case 4:
		{
			if (this->DistanceToPlayer(2) < (this->mType != 304 ? 190 : 140))
			{
				i32 height = this->mPos.vy - G_MECHLIST_PLAYER->mPos.vy;
				if (my_abs(height) < 204800)
				{
					this->dumbAssPad = 1;
					break;
				}
			}
			this->field_31C.bothFlags = 28;
			this->dumbAssPad = 0;
			break;
		}
		case 5:
			if (this->mAnimFinished)
				this->CycleAnim(this->field_298.Bytes[0], 1);
			if (this->field_230-- <= 0)
				this->dumbAssPad = 1;
			break;
		case 6:
			if (this->mAnimFinished)
				this->CycleAnim(this->field_294.Bytes[1], 1);
			if (--this->field_1F8)
			{
				this->mPos.vx += (this->field_1A8[this->field_1F0].vx - this->mPos.vx) >> 1;
				this->mPos.vz += (this->field_1A8[this->field_1F0].vz - this->mPos.vz) >> 1;
			}
			else
				this->dumbAssPad = 4;
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @AlmostMatching: EBX/EDX swap in four offset calculations after 16
// expression and temporary variants. Same instruction count and branches.
// 0x4D5540. Native comparison passed 8800 cases.
i32 CThug::TooCloseToSpidey(void)
{
	if (this->mType == 304)
	{
		if (this->DistanceToPlayer(2) >= 105)
			return 0;
		CVector target = this->mPos;
		target.vx += (((target.vx - G_MECHLIST_PLAYER->mPos.vx) * (120 - this->DistanceToPlayer(2))) * 17) >> 12;
		target.vz += (((target.vz - G_MECHLIST_PLAYER->mPos.vz) * (120 - this->DistanceToPlayer(2))) * 17) >> 12;
		if (this->PathCheck(&this->mPos, &target, 0, 55))
			return 0;
		if (this->AddPointToPath(&target, 0))
		{
			this->dumbAssPad = 6;
			this->field_1F8 = 3;
			return 1;
		}
	}
	else
	{
		if (this->DistanceToPlayer(2) >= 162)
			return 0;
		CVector target = this->mPos;
		target.vx += (((target.vx - G_MECHLIST_PLAYER->mPos.vx) * (185 - this->DistanceToPlayer(2))) * 11) >> 12;
		target.vz += (((target.vz - G_MECHLIST_PLAYER->mPos.vz) * (185 - this->DistanceToPlayer(2))) * 11) >> 12;
		if (this->PathCheck(&this->mPos, &target, 0, 55))
			return 0;
		if (this->AddPointToPath(&target, 0))
		{
			this->dumbAssPad = 6;
			this->field_1F8 = 3;
			return 1;
		}
	}
	return 0;
}

// @Ok
// @Matching
// 0x4D3340
void CThug::GetAttackPosition(CVector* target)
{
	*target = G_MECHLIST_PLAYER->mPos;
	if (this->field_3BC & 1)
	{
		CVector direction;
		direction = *target - this->mPos;
		direction >>= this->DistanceToPlayer(2) > 100 ? 12 : 8;
		direction.vy = 0;
		VectorNormal(reinterpret_cast<VECTOR*>(&direction), reinterpret_cast<VECTOR*>(&direction));
		direction *= (3200 * (this->mType != 304 ? 190 : 140)) >> 12;
		*target -= direction;
	}
	else if (this->field_3BC & 2)
	{
		if (this->field_3BD & 0xEE)
		{
			i32 distance = this->field_3BD & 0xAA ? 1228800 : 1638400;
			target->vx += (this->field_3BD > 8 ? -1 : 1) * distance;
		}
		if (this->field_3BD & 0xBB)
			target->vz += (this->field_3BD & 0x83 ? 1 : -1)
				* (this->field_3BD & 0xAA ? 1228800 : 1638400);
	}
}

// @Ok
// @Matching
// 0x4DB090
void CThug::DoAISwitchLogic(void)
{
	if ((this->field_3AC & 4) && (this->field_3BC & 1)
		&& this->DistanceToPlayer(0) < (this->mType != 304 ? 190 : 140))
	{
		this->Neutralize();
		this->field_31C.bothFlags = 5;
		this->dumbAssPad = 0;
	}
	if ((this->field_218 & 0x80000) && this->field_31C.bothFlags != 14)
	{
		SHitInfo hit;
		hit.field_0 = 4;
		hit.field_8 = 50;
		this->Hit(&hit);
		this->field_218 &= ~0x80000;
	}
	i32* timer = &this->field_32C;
	for (i32 count = 6; count; --count)
	{
		this->RunTimer(timer);
		++timer;
	}
	if (!this->field_330 && this->BumpedIntoSpidey(100))
	{
		this->field_31C.bothFlags = 3;
		this->dumbAssPad = 0;
	}
	if (this->field_348)
	{
		this->RunTimer(&this->field_348);
		if (!this->field_348 && this->DistanceToPlayer(2) < 300)
		{
			i32 height = G_MECHLIST_PLAYER->mPos.vy - this->mPos.vy;
			if (my_abs(height) < 819200
				&& (this->CheckStateFlags(G_THUG_STATE_FLAGS, 17) & 0x40)
				&& !this->field_330)
			{
				this->field_31C.bothFlags = 3;
				this->dumbAssPad = 0;
			}
		}
	}
	if (this->field_344)
	{
		this->RunTimer(&this->field_344);
		if (!this->field_344)
		{
			this->field_338 = 166;
			SFX_PlayPos((Rnd(2) + 30) | 0x8000, &this->mPos, 0);
		}
	}
	if ((this->field_218 & 0x8000) && !this->field_33C
		&& (this->CheckStateFlags(G_THUG_STATE_FLAGS, 17) & 0x20)
		&& !(G_ATTACK_RELATED & 0xF))
		this->StrikeUpConversation();
	this->RunTimer(&this->field_1A4);
}

// @Ok
// @Matching
void Thug_RelocatableModuleClear(void)
{
	for (CBody* cur = G_BADDY_LIST; cur; )
	{
		CBody* next = reinterpret_cast<CBody*>(cur->mNextItem);
		if (cur->mType == 304 || cur->mType == 312)
		{
			delete cur;
		}

		cur = next;
	}
}

// @Ok
// @Matching
void Thug_RelocatableModuleInit(reloc_mod* pMod)
{
	pMod->mClearFunc = Thug_RelocatableModuleClear;
	pMod->field_C[0] = Thug_CreateThug;
}

// @Ok
// @Test
void CThug::TakeHit(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->mCBodyFlags &= 0xFFEFu;
			this->field_310 = 0;
			this->ClearAttackFlags();

			if (this->field_318 == 1)
			{
				this->mRMinor = 0;
				this->RunAnim(this->mType != 304 ? 19 : 14, 0, -1);
				this->dumbAssPad = 2;
			}
			else
			{
				new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 0, 80, 200);
				this->RunAppropriateHitAnim();
				this->dumbAssPad = 3;
			}

			break;
		case 1:
			if ( this->mAnimFinished )
			{
				this->field_318 = 0;
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}
			break;
		case 2:
			if ( this->mAnimFinished )
			{
				this->mRMinor = this->mType == 304 ? 96 : 150;
				this->RunAnim(this->mType == 304 ? 15 : 20, 0, -1);
				this->dumbAssPad = 1;
			}

			break;
		case 3:
			if ( this->mAnimFinished )
			{
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}

			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
void CThug::BackpedalPlease(void)
{
	CVector v10;
	v10.vx = 0;
	v10.vy = 0;
	v10.vz = 0;

	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->field_1F8 = 0;
			v10 = this->mPos;

			v10.vx += 6 * ((this->mPos.vx - G_MECHLIST_PLAYER->mPos.vx) >> 2);
			v10.vz += 6 * ((this->mPos.vz - G_MECHLIST_PLAYER->mPos.vz) >> 2);

			if (this->AddPointToPath(&this->mPos, 0) && this->AddPointToPath(&v10, 0))
			{

				this->mVel = (this->mPos - G_MECHLIST_PLAYER->mPos) >> 2;
				this->mVel.vy = 0;
			}

			this->RunAppropriateHitAnim();
			this->dumbAssPad++;

			break;
		case 1:
			this->DoPhysics(1);
			if (this->field_1F8 <= 0)
				this->Neutralize();
			if (this->mAnimFinished)
			{
				this->Neutralize();
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
void CThug::LookForPlayer(void)
{
	CVector v5;
	v5.vx = 0;
	v5.vy = 0;
	v5.vz = 0;

	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->CycleAnim(this->field_298.Bytes[0], 1);
			this->dumbAssPad++;
			this->field_21D = Rnd(64);
			this->field_1F8 = 0;
			break;
		case 1:
			CVector *mPos;

			if (this->field_1F8 < 64)
				mPos = &G_MECHLIST_PLAYER->mPos;
			else
				mPos = &this->mPos;

			this->GetWaypointNearTarget(mPos, 409600, this->field_21D, &v5);
			this->field_21D++;
			if ( !this->PathCheck(&this->mPos, &v5, 0, 55)
				&& (this->AddPointToPath(&v5, 0)
				|| this->AddPointToPath(&this->mPos, 0)
				&& this->AddPointToPath(&v5, 0)) )
			{
				this->field_31C.bothFlags = 24;
				this->dumbAssPad = 0;
			}
			else
			{
				if ( this->field_1F8++ > 128 )
				{
					this->field_330 = 0;
					this->field_31C.bothFlags = 2;
					this->dumbAssPad = 0;
				}
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// Rewritten from a fresh IDA decompile+disasm of 0x4d9a40 (2026-08-31).
// All callees turned out to already be real, named, implemented functions
// (DistanceToPlayer, Utils_LineOfSight, PathCheck, AddPointToPath,
// CheckSightCone, Neutralize, Baddy_SendSignal), and gGlobalThug/gThugList/
// gAttackFlagRelated (already declared in this file, same globals
// ClearAttackFlags/SetAttacker use) turned out to be exactly the globals
// this function needed, so no new globals were required.
// field_1A4 (CBaddy, shared by every subclass), field_32C, field_348 and
// field_36C were unnamed padding read/written here; carved out of their
// PADDING blocks in baddy.h/thug.h (same byte ranges, nothing else shifts).
// Kept as a goto state machine matching the original's shared tail labels
// (LABEL_80/81/82/83 in the decompile) rather than restructuring into
// nested if/else, to avoid introducing a logic mistake in a function this
// tangled.
i32 CThug::DetermineFightState(void)
{
	i32 initialBothFlags = this->field_31C.bothFlags;
	i32 dist = this->DistanceToPlayer(2);
	i32 cachedPath = -1;

	if (this->mHealth <= 0 || G_MECHLIST_PLAYER->mHealth <= 0 || G_MECHLIST_PLAYER->field_57C != 0)
		return false;

	if (dist <= 1000)
		this->field_36C = 3;
	else if (dist <= 2000)
		this->field_36C = 15;
	else
		this->field_36C = 31;

	if (this->field_330 == 0
			&& dist < 300
			&& this->field_348 == 0
			&& my_abs(G_MECHLIST_PLAYER->mPos.vy - this->mPos.vy) < 819200)
	{
		this->field_348 = 60;
	}

	if (this->field_1A4 != 0)
		return false;

	if (Utils_LineOfSight(&this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 0) == 0)
	{
		if (this->field_2A8 & 0x800)
		{
			this->field_2A8 &= ~0x800;

			if (this->PathCheck(&this->mPos, &this->field_1A8[0], 0, 55) == 0
					&& this->AddPointToPath(&this->mPos, 0) != 0
					&& this->AddPointToPath(&this->field_1A8[0], 0) != 0)
			{
				this->field_31C.bothFlags = 24;
				this->dumbAssPad = 0;
				return true;
			}
		}
		else if (this->field_330 != 0)
		{
			i32 bothFlags = this->field_31C.bothFlags;

			if (bothFlags != 23 && bothFlags != 24)
			{
				this->field_31C.bothFlags = 23;
				this->dumbAssPad = 0;
				return true;
			}
		}

		return false;
	}

	if (this->field_330 != 0 && dist < this->field_370)
	{
		cachedPath = this->PathCheck(&this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 55);

		if (cachedPath == 0)
		{
			this->field_31C.bothFlags = 4;
			goto setDumbAssPadZero;
		}
	}

	if (this->CheckSightCone(this->field_378, this->field_374, this->field_370, this->field_37C, G_MECHLIST_PLAYER) == 0)
	{
		i32 pathResult;

		if (G_MECHLIST_PLAYER->field_57C != 0
				|| G_THUG_LIST != 0
				|| G_MECHLIST_PLAYER->mHeldObject != 0
				|| this->field_330 == 0
				|| dist >= 1500)
		{
			goto checkStateChanged;
		}

		pathResult = (cachedPath == -1) ? this->PathCheck(&this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 55) : cachedPath;

		if (pathResult == 0)
			goto checkStateChanged;

		this->Neutralize();
		G_THUG_LIST = this;

		if (G_GLOBAL_THUG == this)
		{
			G_GLOBAL_THUG = 0;
			goto clearAttackAndFight;
		}

		if (this->field_3BC & 2)
			G_ATTACK_FLAG_RELATED = ~this->field_3BD & G_ATTACK_FLAG_RELATED;

		goto clearAttackAndFight;
	}

	if (this->field_330 == 0)
	{
		this->field_31C.bothFlags = 3;
		goto setDumbAssPadZero;
	}

	if (cachedPath == -1)
		cachedPath = this->PathCheck(&this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 55);

	if (cachedPath == 0)
	{
		this->field_31C.bothFlags = 4;
		goto setDumbAssPadZero;
	}

	if (this->mType != 304
			&& this->field_32C == 0
			&& this->DistanceToPlayer(4) > 500
			&& this->DistanceToPlayer(4) < this->field_384)
	{
		this->field_31C.bothFlags = 11;
		goto setDumbAssPadZero;
	}

	if (this->field_31C.bothFlags == 1)
	{
		i32 pathResult;

		if (G_MECHLIST_PLAYER->field_57C != 0
				|| G_THUG_LIST != 0
				|| G_MECHLIST_PLAYER->mHeldObject != 0
				|| this->field_330 == 0
				|| dist >= 1500)
		{
			goto checkStateChanged;
		}

		pathResult = (cachedPath == -1) ? this->PathCheck(&this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 55) : cachedPath;

		if (pathResult == 0)
			goto checkStateChanged;

		this->Neutralize();
		G_THUG_LIST = this;

		if (G_GLOBAL_THUG == this)
		{
			G_GLOBAL_THUG = 0;
			goto clearAttackAndFight;
		}

		if (this->field_3BC & 2)
			G_ATTACK_FLAG_RELATED = ~this->field_3BD & G_ATTACK_FLAG_RELATED;

		goto clearAttackAndFight;
	}

	this->field_31C.bothFlags = 4;
	this->dumbAssPad = 0;

	if (G_MECHLIST_PLAYER->field_57C == 0
			&& G_THUG_LIST == 0
			&& G_MECHLIST_PLAYER->mHeldObject == 0
			&& this->field_330 != 0
			&& dist < 1500)
	{
		i32 pathResult = (cachedPath == -1) ? this->PathCheck(&this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 55) : cachedPath;

		if (pathResult != 0)
		{
			this->Neutralize();
			G_THUG_LIST = this;

			if (G_GLOBAL_THUG == this)
				G_GLOBAL_THUG = 0;
			else if (this->field_3BC & 2)
				G_ATTACK_FLAG_RELATED &= ~this->field_3BD;

			goto clearAttackAndFight;
		}
	}

	goto checkStateChanged;

clearAttackAndFight:
	this->field_3BC = 0;
	this->field_3BD = 0;
	this->field_31C.bothFlags = 9;

setDumbAssPadZero:
	this->dumbAssPad = 0;

checkStateChanged:
	if (initialBothFlags != this->field_31C.bothFlags)
	{
		this->mCBodyFlags |= 0x10;
		this->Baddy_SendSignal();
	}

	return initialBothFlags != this->field_31C.bothFlags;
}

// @Ok
void CThug::Caution(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_310 = 0;
			this->Neutralize();

			SFX_PlayPos( (Rnd(3) + 14) | 0x8000, &this->mPos, 0);

			if ((abs(G_MECHLIST_PLAYER->mPos.vy - this->field_29C - 0x4000) < 409600)
				&& this->PlayerIsVisible())
			{
				if (!this->ShouldIShootPlayer())
				{
					this->field_31C.bothFlags = G_MECHLIST_PLAYER->field_57C != 0 ? 1 : 4;
					this->dumbAssPad = 0;
				}
			}
			else if (!this->DetermineFightState())
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
void CThug::CreateCombatImpactEffect(CVector* a2, i32 a3)
{
	if ( this->mType == 304 )
	{
		new CCombatImpactRing(a2, 0x12u, 18, 108, 384, 1792, 144);
		new CCombatImpactRing(a2, 0x48u, 72, 144, 192, 896, 72);
	}
	else
	{
		new CCombatImpactRing(a2, 0x6Cu, 108, 18, 384, 1792, 144);
		new CCombatImpactRing(a2, 0x90u, 144, 72, 192, 896, 72);
	}
}

// @Ok
// @Test
// two's complement a lil diff
INLINE i32 CThug::ShouldIShootPlayer(void)
{
	if ( G_MECHLIST_PLAYER->field_57C
		|| G_THUG_LIST
		|| this->DistanceToPlayer(2) >= 2000
		|| G_MECHLIST_PLAYER->mHeldObject
		|| this->DistanceToPlayer(2) <= 650
		&& (abs(G_MECHLIST_PLAYER->mPos.vy - this->field_29C - 0x4000) <= 409600) )
	{
		return 0;
	}

	this->Neutralize();
	G_THUG_LIST = this;
	this->field_31C.bothFlags = 9;
	this->dumbAssPad = 0;
	return 1;
}

// @Ok
CThug::~CThug(void)
{
	if (this->field_3A0)
		delete this->field_3A0;
	this->field_3A0 = 0;

	if (this->field_3A4)
		Mem_Delete(this->field_3A4);
	this->field_3A4 = 0;

	if (G_THUG_LIST == this)
		G_THUG_LIST = 0;

	this->ClearAttackFlags();
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
}

// @Ok
// @Matching
void CThug::SetUpLaser(
		CGPolyLine **a2,
		CVector *a3,
		CVector *a4)
{
	if (!*a2)
	{
		*a2 = new CGPolyLine(1);

		(*a2)->mStartB = 80;
		(*a2)->mSegs->r = 0;
		(*a2)->mSegs->g = 0;
		(*a2)->mStartG = 0;
		(*a2)->mStartR = 0;
		(*a2)->SetSemiTransparent();
	}

	i32 v6 = Utils_CrapDist(*a3, *a4);

	if (v6 >= 1000)
		(*a2)->mSegs->b = 0;
	else
		(*a2)->mSegs->b = (320 * (1000 - v6)) >> 12;

	(*a2)->SetStartAndEnd(a3, a4);
}

// @Ok
INLINE void CThug::DrawBarrelFlash(
		CVector *a2,
		CVector *a3,
		SLineInfo *a4,
		u8 a5,
		u8 a6,
		u8 a7)
{
	new CGlowFlash(a2, 5, a5, a6, a7, 32, 0, 0, 0, 0, 50, 20, 1, 20, 10, 40, 20, 10, 1);
	this->SetUpLaser(&this->field_3A0, a2, a3);
}

// @Ok
// INLINE, so it never gets its own address on PC (confirmed against the Mac
// build symbol table, CheckToShoot__5CThugFii at 0x00138250, which has no
// PC-side counterpart in names.json). Field usage (field_218 bit 0x800 as
// the "always shoot in range" flag set by SetParamByIndex case 11,
// field_37C as the range set by case 4, field_330 as the "already alert"
// state) is consistent with how those same fields are used everywhere else
// in this file, so verified by code review rather than a byte diff.
INLINE void CThug::CheckToShoot(i32 a2, i32 a3)
{
	if ( G_MECHLIST_PLAYER->field_57C && !G_THUG_LIST && !G_MECHLIST_PLAYER->mHeldObject)
	{
		if ( ((this->field_218 & 0x800) && a2 < this->field_37C)
				||
			 (this->field_330 && a2 < 1500 && (a3 != -1 || this->PathCheck(&this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 55))))
		{
			this->Neutralize();
			G_THUG_LIST = this;
			this->ClearAttackFlags();
			this->field_31C.bothFlags = 9;
			this->dumbAssPad = 0;
		}
	}
}

// @Ok
// Verified against IDA decompile of 0x4d5db0: GetClosest(304,0) then
// GetClosest(312,0) via the vtable, nearest->field_330 (offset 0x330, same
// field CThug uses on itself elsewhere) gated on 0, then a plain
// CMessage(this, nearest, 7, 0). No missing checks; the reinterpret_cast
// pattern already matches the established CCop::WarnOtherCops idiom in
// cop.cpp.
void CThug::WarnOtherThugs(void)
{
	CThug *nearest = reinterpret_cast<CThug*>(this->GetClosest(304, 0));
	if ( (nearest || ((nearest = reinterpret_cast<CThug*>(this->GetClosest(312, 0))) != 0))
			&& !nearest->field_330)
	{
		new CMessage(this, nearest, 7, 0);
	}
}

// @Ok
// INLINE, no separate PC address (Mac has AdjustPosPlaySound__5CThugFi at
// 0x001378f0). The 2400/1600 weighted blend of MechList->mPos and mPos is
// confirmed byte-for-byte against the same formula inlined a second time in
// CThug_Fall's case 1 (0x4d9200: (1600*mPos.x>>12)+(2400*MechList->mPos.x>>12),
// same SFX_PlayPos(id, &blendedPos, 0) call shape), so this is the real
// extracted source of that idiom, not a guess.
INLINE i32 CThug::AdjustPosPlaySound(i32 a2)
{
	CVector v4;
	v4.vx = 0;
	v4.vy = 0;
	v4.vz = 0;

	v4.vx = ((2400 * G_MECHLIST_PLAYER->mPos.vx) >> 12) + ((1600 * this->mPos.vx) >> 12);
	v4.vy = ((2400 * G_MECHLIST_PLAYER->mPos.vy) >> 12) + ((1600 * this->mPos.vy) >> 12);
	v4.vz = ((2400 * G_MECHLIST_PLAYER->mPos.vz) >> 12) + ((1600 * this->mPos.vz) >> 12);

	return SFX_PlayPos(a2, &v4, 0);
}


// @Ok
u8 CThug::Grab(CVector* a2)
{
	if ( (this->CheckStateFlags(G_THUG_STATE_FLAGS, 17) & 2)
		|| !this->AddPointToPath(a2, 0) )
	{
		return 0;
	}

	this->field_31C.bothFlags = 20;


	this->dumbAssPad = 0;
	this->field_2A8 |= 0x40;
	return 1;
}

// @Ok
INLINE void CThug::SetAttacker(void)
{
	if (G_GLOBAL_THUG != this)
	{
		this->ClearAttackFlags();
		if (G_GLOBAL_THUG)
		{
			G_GLOBAL_THUG->ClearAttackFlags();
		}

		G_GLOBAL_THUG = this;
		this->field_3BC = 1;
	}
}

// @Ok
INLINE i32 CThug::SpideyAnimUppercut(void)
{
	return G_MECHLIST_PLAYER->mAnim == 106
		|| G_MECHLIST_PLAYER->mAnim == 113
		|| G_MECHLIST_PLAYER->mAnim == 284;
}

// @Ok
// @Matching
// 0x4DA300
// Verified against IDA decompile of 0x4da300: every case (frame guard,
// sound id, MakeSpriteRing vector and its component order) matches.
void CThug::PlaySounds(void)
{
	if (this->mType == 304)
	{
		switch ( this->mAnim )
		{
			case 0xEu:
				if ( this->mFrame == 15 )
				{
					CVector v11;
					v11.vz = 409600;
					v11.vx = 0;
					v11.vy = 0;
					this->MakeSpriteRing(&v11);
				}
				break;
			case 0x1Au:
				if ( this->mFrame == 23 )
				{
					CVector v11;
					v11.vx = 0;
					v11.vy = 0;
					v11.vz = -122880;
					this->MakeSpriteRing(&v11);
				}
				break;
			case 1u:
				if ( this->mFrame == 27 || this->mFrame == 13 )
				{
					SFX_PlayPos((Rnd(4) + 32) | 0x8000, &this->mPos, 300);
				}
				return;
			case 0x1Cu:
				if ( this->mFrame == 4 || this->mFrame == 14)
					SFX_PlayPos((Rnd(4) + 32) | 0x8000, &this->mPos, 50);
				break;			case 8u:
				if ( this->mFrame == 5 )
					SFX_PlayPos(0x801D, &this->mPos, 500);
				break;

		}
	}
	else
	{
		switch ( this->mAnim )
		{
			case 0x1Au:
				if ( this->mFrame == 28 )
				{
					CVector v11;
					v11.vz = -204800;
					v11.vx = 0;
					v11.vy = 0;
					this->MakeSpriteRing(&v11);
				}
				break;			case 0x13u:
				if ( this->mFrame == 15 )
				{
					CVector v11;
					v11.vx = 0;
					v11.vy = 0;
					v11.vz = 204800;
					this->MakeSpriteRing(&v11);
				}
				break;
			case 5u:
				if ( this->mFrame == 1 || this->mFrame == 21 )
				{
					SFX_PlayPos((Rnd(4) + 32) | 0x8000, &this->mPos, 300);
				}
				break;
			case 0x18u:
				if ( this->mFrame == 1 || this->mFrame == 12)
					SFX_PlayPos((Rnd(4) + 32) | 0x8000, &this->mPos, 50);
				break;
			case 7u:
				if ( this->mFrame == 3 )
					SFX_PlayPos(0x801D, &this->mPos, 500);
				break;

		}
	}

}

// @Ok
// Spit attack particles: 6 short-lived CGLineParticle spawned from a
// mouth hook (SHook offset 13), heading roughly forward+90 degrees.
// Fixed against IDA decompile of 0x4d4f30 (two real bugs, not scheduling):
// (1) mFlags is a u16 at offset 4; "this+5" in the decompile is the HIGH
// byte of mFlags, so the guard bit is 0x8000, not 0x80 (0x8000 is used the
// same way elsewhere, e.g. spidey.cpp:314, PCTex.cpp). (2) the allocation
// size passed to CBit::operator new is the constant sizeof(CGLineParticle)
// (0x60, VALIDATE_SIZE'd in bit2.cpp), not a computed value; the two
// Rnd(3)+4 calls the old code folded into a bogus size formula actually
// build a fresh per-particle direction vector (dir scaled on x/z only) that
// is what gets passed into the constructor instead of the plain "dir".
i32 CThug::MonitorSpitPlease(void)
{
	if (this->mFrame < 0x1E)
	{
		return 0;
	}

	if ((this->mFlags & 0x8000) && Utils_CrapDist(this->mPos, G_MECHLIST_PLAYER->mPos) < 0xFA0)
	{
		SFX_PlayPos(0x8011, &this->mPos, 0);

		CSVector angle = this->mAngles;
		angle.vy += 0x400;

		CVector dir;
		Utils_GetVecFromMagDir(&dir, -0x100, &angle);
		dir >>= 8;

		SHook hook;
		hook.Part.vx = 0;
		hook.Part.vy = 0x14;
		hook.Part.vz = static_cast<i16>(0xFF9C);
		hook.Offset = 13;

		VECTOR hookPos;
		M3dUtils_GetDynamicHookPosition(&hookPos, this, &hook);

		i32 count = 6;
		do
		{
			CVector particleDir;
			particleDir.vx = dir.vx * (Rnd(3) + 4);
			particleDir.vy = 0;
			particleDir.vz = dir.vz * (Rnd(3) + 4);

			CGLineParticle *particle = new CGLineParticle(
					*reinterpret_cast<CVector*>(&hookPos),
					particleDir,
					0x14,
					1);

			particle->SetRGB0(0x30, 0x60, 0x30);
			particle->SetRGB1(0, 0, 0);
			particle->mCodeBGR0 |= 0x2000000;

			count--;
		} while (count != 0);
	}

	return 1;
}

// @Ok
void CThug::CycleOrContinueAnim(
		i32 a2,
		i32 a3,
		i32 a4,
		i32 a5)
{
	if ( this->mAnim != a2
			&& this->mAnim != a3
			&& this->mAnim != a4
			&& this->mAnim != a5 )
		this->CycleAnim(a2, 1);
}

// @Ok
void CThug::Guard(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->mCBodyFlags |= 0x10;
			if (this->mType != 304)
			{
				this->RunAnim(this->field_298.Bytes[0], 0, -1);
				this->field_1F8 = Rnd(5) + 8;
				this->dumbAssPad = 5;
				break;
			}

			this->dumbAssPad++;
			if (this->mAnim == 11)
			{
				this->mAnimFinished = 1;
				break;
			}
			else if (this->mAnim != 10)
			{
				this->RunAnim(0xAu, 0, -1);
				break;
			}

			this->RunAnim(0xA, this->mFrame, -1);
		case 1:
			if ( this->mAnimFinished )
			{
				this->CycleOrContinueAnim(11, -1, -1, -1);
				this->field_230 = Rnd(30) + 20;
				this->dumbAssPad++;
			}
			break;
		case 2:
			if ( this->field_230-- <= 0 )
			{
				this->RunAnim(0xCu, 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 3:
			if ( this->mAnimFinished )
			{
				this->CycleAnim(3, 1);
				this->field_230 = Rnd(30) + 20;
				this->dumbAssPad++;
			}
			break;
		case 4:
			if ( this->field_230-- <= 0 )
				this->dumbAssPad = 0;
			break;
		case 5:
			if ( this->mAnimFinished )
			{
				if ( --this->field_1F8 == 0 )
				{
					this->field_1F8 = Rnd(5) + 8;
					this->RunAnim(0x15, 0, -1);
					this->dumbAssPad++;
				}
				else
				{
					this->RunAnim(this->field_298.Bytes[0], 0, -1);
				}
			}
			break;
		case 6:
			if ( this->MonitorSpitPlease() )
				this->dumbAssPad = 5;
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
INLINE i32 CThug::CanAck(
		i32 a2,
		i32 a3,
		i32 a4,
		CThug* pThug)
{
		if (!this->field_330
			&& !this->field_33C
			&& (this->CheckStateFlags(G_THUG_STATE_FLAGS, 17) & 0x20))
		{
			this->mHandleTwo = Mem_MakeHandle(pThug);
			this->field_398 = a3;
			this->field_39C = a4;
			this->field_33C = a2;

			this->field_31C.bothFlags = 21;
			this->dumbAssPad = 0;

			return 1;
		}

		return 0;
}

// @Ok
void CThug::StrikeUpConversation(void)
{
	if (this->DistanceToPlayer(2) <= 1792)
	{
		CThug *pThug = reinterpret_cast<CThug*>(this->GetClosest(304, 1));

		if ((pThug || (pThug = reinterpret_cast<CThug*>(this->GetClosest(312, 1))))
				&& Utils_CrapDist(this->mPos, pThug->mPos) < 0x800)
		{

			i32 v5;
			i32 v4 = 4;
			this->field_398 = Rnd(7);
			this->field_39C = 4;
			i32 v6 = 4;
			switch ( this->field_398 )
			{
			case 0:
				this->field_398 = -3;
				v5 = 160;
				break;
			case 1:
				this->field_398 = -5;
				v4 = 6;
				v5 = 160;
				break;
			case 2:
				this->field_398 = -7;
				v4 = 8;
				v5 = 160;
				break;
			case 3:
				this->field_398 = -9;
				v4 = 10;
				v5 = 160;
				this->field_39C = 3;
				v6 = 3;
				break;
			case 4:
				this->field_398 = -11;
				v4 = 12;
				v5 = 160;
				this->field_39C = 3;
				v6 = 3;
				break;
			case 5:
				this->field_398 = -13;
				v4 = 14;
				v5 = 160;
				this->field_39C = 3;
				v6 = 3;
				break;
			case 6:
				this->field_398 = -15;
				v4 = 14;
				v5 = 160;
				this->field_39C = 3;
				break;
			default:
				print_if_false(0, "Too many ack response types.");
				v4 = 4;
				v5 = 4;
				break;
			}


			if (pThug->CanAck(v5, v4, v6, this))
			{
				this->field_33C = v5;
				this->mHandleTwo = Mem_MakeHandle(pThug);
				this->field_31C.bothFlags = 21;
				this->dumbAssPad = 0;
			}
		}
	}
}

// @Ok
void CThug::Acknowledge(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->mCBodyFlags |= 0x10;
			this->Neutralize();

			if (this->field_398 < 0)
			{
				this->RunAnim(this->mType != 304 ? 39 : 32, 0, -1);
				Redbook_XAPlayPos(this->field_39C, -this->field_398, &this->mPos, 20);
			}

			if (Mem_RecoverPointer(&this->mHandleTwo))
			{
				new CAIProc_LookAt(
						this,
						reinterpret_cast<CBody*>(this->mHandleTwo.pWhatever),
						0,
						2,
						70,
						200);
			}
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
				this->CycleAnim(this->field_298.Bytes[0], 1);

			if (!this->field_33C)
			{
				if (this->field_398 > 0)
				{
					Redbook_XAPlayPos(this->field_39C, this->field_398, &this->mPos, 20);
					this->RunAnim(this->mType != 304 ? 40 : 32, 0, -1);
					this->dumbAssPad++;
					return;
				}

				this->field_33C = 2000;
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}

			break;
		case 2:
			if (this->mAnimFinished)
			{
				this->field_33C = 2000;
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}

			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
void CThug::LookConfused(void)
{
	i32 v3;

	switch ( this->dumbAssPad )
	{
		case 0:
			this->field_1F8 = Rnd(3) + 2;
			this->dumbAssPad++;
		case 1:
			this->Neutralize();
			if ( !this->field_1F8 )
			{
				this->field_330 = 0;
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}
			else
			{
				this->dumbAssPad = 5;
				this->field_1F8 = this->field_1F8 - 1;
			}
			break;
		case 5:
			v3 = Rnd(1000) + 1000;
			if ( Rnd(2) )
				v3 = -v3;

			v3 += this->mAngles.vy;

			new CAIProc_LookAt(this, v3, 2, 80, 200);
			this->dumbAssPad++;

			break;
		case 6:
			this->RunAppropriateAnim();
			if ( (this->field_288 & 2) != 0 )
			{
				this->dumbAssPad = 1;
				this->field_288 &= 0xFFFFFFFD;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// Same logic as CCop::WallHitCheck (cop.cpp). Confirmed via IDA decompile of
// 0x4292c0 (the address GetLaunched calls into): every field this function
// touches (mPos, field_21E, field_218, field_1F8, mHealth, PathCheck,
// AddPointToPath) lives on the shared CBaddy/CBody base, so the compiler
// merged the two identical bodies at link time and names.json happens to
// label the shared address as CCop's. Functional only, not verified byte
// for byte (no separate CThug address exists to compare against).
i32 CThug::WallHitCheck(CVector* a2, CVector* a3, i32 a4)
{
	i32 result = 1;

	i32 adjY = this->mPos.vy + ((this->field_21E - 20) << 12);

	CVector v1;
	v1.vx = a2->vx;
	v1.vy = adjY;
	v1.vz = a2->vz;

	CVector v2;
	v2.vx = this->mPos.vx;
	v2.vy = adjY;
	v2.vz = this->mPos.vz;

	CVector posBuf;
	posBuf.vx = 0;
	posBuf.vy = 0;
	posBuf.vz = 0;

	this->field_218 &= ~0x500;

	i32 pathResult = this->PathCheck(&v2, &v1, &posBuf, 55);

	if (pathResult == 4)
	{
		this->field_1F8 = 0;
		return 3;
	}

	if (pathResult != 2)
	{
		this->field_218 |= 0x100;
		this->field_1F8 = a4 - 1;

		if (this->mHealth > 0)
		{
			if (this->AddPointToPath(&this->mPos, 0) && this->AddPointToPath(a2, 0))
			{
				this->field_2A8 &= ~0x10000000;
				return result;
			}

			this->mHealth = 0;
		}

		return result;
	}

	i32 dx = posBuf.vx - this->mPos.vx;
	i32 dxSign = dx >> 31;
	i32 absDx = (dx ^ dxSign) - dxSign;

	i32 dz = posBuf.vz - this->mPos.vz;
	i32 dzSign = dz >> 31;
	i32 absDz = (dz ^ dzSign) - dzSign;

	i32 dividend;
	i32 divisor;

	if (absDx <= absDz)
	{
		i32 a3zSign = a3->vz >> 31;
		divisor = (a3->vz ^ a3zSign) - a3zSign;
		dividend = absDz;
	}
	else
	{
		i32 a3xSign = a3->vx >> 31;
		divisor = (a3->vx ^ a3xSign) - a3xSign;
		dividend = absDx;
	}

	this->field_1F8 = dividend / divisor;

	if (this->field_1F8 > a4)
	{
		this->field_1F8 = a4;
	}
	else
	{
		result = 2;
	}

	posBuf.vx = a3->vx * this->field_1F8 + this->mPos.vx;
	posBuf.vy = this->mPos.vy;
	posBuf.vz = a3->vz * this->field_1F8 + this->mPos.vz;

	this->field_218 |= 0x400;

	if (this->mHealth > 0)
	{
		if (this->AddPointToPath(&this->mPos, 0) && this->AddPointToPath(&posBuf, 0))
		{
			this->field_2A8 &= ~0x10000000;
			return result;
		}

		this->mHealth = 0;
	}

	return result;
}

// @Ok
i32 CThug::GetLaunched(
		CVector* a2,
		i32 a3,
		i32 a4,
		i32 a5)
{
	i32 v10; // ebx
	CVector v13; // [esp+10h] [ebp-18h] BYREF

	v13.vy = this->mPos.vy;
	v13.vx = (a4 + 1) * a2->vx + this->mPos.vx;
	v13.vz = (a4 + 1) * a2->vz + this->mPos.vz;

	v10 = this->WallHitCheck(&v13, a2, a4);

	if ( v10 == 3 )
		return 0;

	this->Neutralize();
	this->mVel = *a2;
	this->mVel.vy = 0;

	if ( !a3 || v10 == 2 )
	{
		new CAIProc_LookAt(this, 0, &v13, 0, 80, 200);
	}

	return v10;
}

// @Ok
u8 CThug::TugImpulse(CVector* a2, CVector* a3, CVector* a4)
{
	this->field_2A8 &= 0xFFFFFFF7;
	if (a4)
	{
		this->field_31C.bothFlags = 16;
		this->dumbAssPad = 0;
		if (this->field_3A4)
			Mem_Delete(reinterpret_cast<void*>(this->field_3A4));

		this->field_3A4 = a4;
		return 1;
	}

	i32 Launched = this->GetLaunched(a3, 0, 80, 0);
	if (!Launched)
	{
		return 0;
	}

	if (this->mHealth <= 50)
	{
		if (Launched == 1)
		{
			this->field_2A8 |= 0x10;
		}
		else
		{
			this->field_2A8 &= ~0x10;
		}
	}
	this->field_218 |= 0x80000;
	SFX_PlayPos(0x800F, &this->mPos, 0);
	this->field_31C.bothFlags = 14;
	this->dumbAssPad = 0;
	return 1;
}

// @Ok
// Functional: fall-back check, logic verified against Hex-Rays at 0x4d8d70.
// Builds a 75-unit forward vector (flipped to -75 when field_2A8 & 0x10),
// rotates it by mAngles.vy, and if PathCheck(mPos, mPos+rotated, 0, 55) == 2
// spawns a CAIProc_RotY(this, 2047, 4, 0). (The 44 mnemonic diffs from the
// byte-match phase are scheduling; the logic is equivalent.)
void CThug::CheckFallBack(void)
{
	CVector v6;
	CVector a3;

	a3.vx = 0;
	a3.vy = 0;
	a3.vz = 0;

	v6.vz = ((this->field_2A8 & 0x10) ? -75 : 75) << 12;
	v6.vy = 0;
	v6.vx = 0;

	Utils_RotateY(&a3, &v6, this->mAngles.vy);

	if ( this->PathCheck(&this->mPos, &(this->mPos + a3), 0, 55) == 2 )
	{
		new CAIProc_RotY(this, 2047, 4, 0);
	}
}

// @Ok
void CThug::GetReadyToShootHostage(CMessage *pMessage)
{
	CItem *pItem = reinterpret_cast<CItem*>(Mem_RecoverPointer(&pMessage->mHandle));
	if ( pItem )
	{
		if ( Utils_LineOfSight(&this->mPos, &pItem->mPos, 0, 0) )
		{
			this->mHandle = pMessage->mHandle;

			this->field_31C.bothFlags = 8;
			this->dumbAssPad = 0;

			this->Neutralize();
			new CAIProc_StateSwitchSendMessage(this, 16);
		}
		else
		{
			new CMessage(this, reinterpret_cast<CBaddy*>(pMessage->mHandle.pWhatever), 1, 0);
		}

		pMessage->field_10 |= 1;
	}
}

// @Ok
// @Test
void CThug::SetParamByIndex(i32 Index, i32 Param)
{
	switch ( Index )
	{
		case 1:
			this->field_374 = Param;
			break;
		case 2:
			this->field_370 = Param;
			break;
		case 3:
			this->field_378 = Param;
			break;
		case 4:
			this->field_37C = Param;
			break;
		case 5:
			this->field_380 = Param;
			break;
		case 6:
			this->field_384 = Param;
			break;
		case 7:
			this->field_388 = Param;
			break;
		case 8:
			this->field_38C = Param;
			break;
		case 9:
			this->field_1FC = Param;
			break;
		case 10:
			this->field_394 = Param;
			break;
		case 11:
			if (Param)
				this->field_218 |= 0x800;
			else
				this->field_218 &= 0xFFFFF7FF;
			break;
		case 12:
			this->field_218 |= 0x8000;
			break;
		default:
			print_if_false(0, "Unknown index in C_SET_PARAMETER_BY_INDEX"); 
			break;
	}
}

// @Ok
void CThug::SetHitDirectionFlag(CVector* pVector)
{
	CSVector v4;
	v4.vx = 0;
	v4.vy = 0;
	v4.vz = 0;

	Utils_CalcAim(&v4, &(this->mPos + (*pVector * 300)), &this->mPos);

	i32 v3 = v4.vy - this->mAngles.vy;
	if (v3 < -2048)
	{
		v3 += 4096;
	}
	else
	{
		if (v3 > 2048)
			v3 -= 4096;
	}

	if (v3 < -455)
	{
		this->field_218 |= 0x20000;
	}
	else
	{
		if (v3 > 455)
			this->field_218 |= 0x10000;
	}
}

// @NotOk
// Native comparison passed 60000 cases; instruction matching is pending.
// 0x4D3F50
i32 CThug::Hit(SHitInfo* hit)
{
	if (this->field_31C.bothFlags == 26)
	{
		if (this->dumbAssPad == 8)
		{
			SFX_PlayPos((Rnd(3) + 25) | 0x8000, &this->mPos, 0);
			SFX_PlayPos(0x8019, &this->mPos, 0);
		}
		return 0;
	}
	if (hit->field_0 & 4)
		this->mHealth -= hit->field_8;
	if (this->mHealth <= 0)
		this->SendDeathPulse();
	i32 attack = (hit->field_0 & 2) ? hit->field_4 : 0;
	this->field_330 = 600;
	if (!(hit->field_0 & 4) || this->mHealth > 0 || (hit->field_0 & 8))
		SFX_PlayPos((Rnd(3) + 25) | 0x8000, &this->mPos, 0);
	else if (this->field_31C.bothFlags != 26)
	{
		SFX_PlayPos(0x8019, &this->mPos, 0);
		SFX_PlayPos((Rnd(3) + 21) | 0x8000, &this->mPos, 0);
	}
	if (attack == 3)
	{
		if (this->mHealth > 0)
		{
			new CMessage(this, this, 20, 0);
			this->CycleAnim(this->mType == 304 ? 38 : 33, 1);
			SFX_PlayPos(0x801C, &this->mPos, 0);
			return 0;
		}
	}
	else if (attack == 29 && this->field_31C.bothFlags == 20 && this->mHealth > 0)
		return 0;
	this->SetAnimMode(3, 0);
	this->field_318 = attack;
	CThug* buddy = static_cast<CThug*>(this->GetClosest(304, 0));
	if (!buddy)
		buddy = static_cast<CThug*>(this->GetClosest(312, 0));
	if (buddy && !buddy->field_330)
		new CMessage(this, buddy, 7, 0);
	this->field_218 &= ~0x30000;
	if ((this->CheckStateFlags(G_THUG_STATE_FLAGS, 17) & 0x10)
			|| (this->mHealth > 0 && (this->field_3AC & 1)))
		return 0;
	this->Neutralize();
	this->field_31C.bothFlags = this->mHealth > 0 ? 12 : 26;
	this->dumbAssPad = 0;
	this->field_218 &= ~0x30000;
	if (hit->field_0 & 8)
	{
		CSVector angle;
		angle.vx = 0;
		angle.vy = 0;
		angle.vz = 0;
		Utils_CalcAim(&angle, &(this->mPos + hit->field_C * 300), &this->mPos);
		i32 delta = angle.vy - this->mAngles.vy;
		if (delta < -2048)
			delta += 4096;
		else if (delta > 2048)
			delta -= 4096;
		if (delta < -455)
			this->field_218 |= 0x20000;
		else if (delta > 455)
			this->field_218 |= 0x10000;
	}
	if (hit->field_0 & 0x18)
	{
		this->SlideFromHit(hit->field_18, hit->field_1A, hit->field_C);
		this->RunAnim(this->mType == 304 ? 24 : 14, 0, -1);
	}
	return 1;
}


// @Ok
CThug::CThug(i16 *a2, i32 a3)
{
	i16 *v5 = this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2)));

	this->field_3B8 = Trig_GetLevelID();

	this->ShadowOn();
	this->mShadowScale = 50;
	this->field_3B0 = G_ATTACK_RELATED;
	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->field_1F4 = a3;
	this->mNode = a3;

	this->field_230 = 0;
	this->field_216 = 32;
	this->field_2A8 |= 1;


	this->mRMinor = this->mType != 304 ? 150 : 96;
	this->mPushVal = 64;
	this->field_380 = this->mType != 304 ? 400 : 300;
	this->field_31C.bothFlags = 0;


	this->field_370 = 3500;
	this->field_374 = 400;
	this->field_378 = 2047;
	this->field_37C = 100;
	this->field_384 = 1500;
	this->field_388 = 10;
	this->field_38C = 10;

	this->field_1FC = 10;
	this->field_394 = 2000;
	this->ParseScript(reinterpret_cast<u16*>(v5));
	this->field_212 = 60;
}

// @Ok
void Thug_CreateThug(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(*stack);
	i32 v3 = static_cast<i32>(stack[1]);

	*result = reinterpret_cast<u32>(new CThug(v2, v3));
}

// @Ok
// @Matching
// 0x4D2710
CThugBulletTracer::CThugBulletTracer(
		const CVector& start, const CVector& end, CSuper* host, SLineInfo* line,
		u8 red, u8 green, u8 blue)
{
	print_if_false(!host || !line, "pSuper and pLineInfo are both non-NULL in call to CThugBulletTracer");
	if (line)
		CreateThugRicochet(line, red, green, blue);
	if (host)
	{
		SHook hook;
		if (Web_CollideWithSuper(host, &start, &end, &hook, 4096))
			new CThugPing(host, &hook);
	}
	CVector from = start;
	u32 distance = Utils_Dist(from, end);
	if (distance > 1000)
		from = end + (((from - end) / distance) * 1000);
	this->mpRibbon = new CGouraudRibbon(5, 0);
	this->mpRibbon->mProtected = 1;
	this->mpRibbon->SetRGB(red, green, blue);
	this->mpRibbon2 = new CGouraudRibbon(5, 0);
	this->mpRibbon2->mProtected = 1;
	this->mpRibbon2->SetRGB(255, 255, 255);
	this->mMaxWidth = 3;
	this->SetWidth();
	CVector direction = end - from;
	for (i32 i = 0; i < 5; i++)
	{
		this->mpRibbon->mpPoints[i].Pos = from + ((i * direction) / 4);
		this->mpRibbon2->mpPoints[i].Pos = this->mpRibbon->mpPoints[i].Pos;
	}
}

// @Ok
// @Matching
// 0x4D2A30
CThugBulletTracer::~CThugBulletTracer(void)
{
	delete this->mpRibbon;

	delete this->mpRibbon2;

	this->mpRibbon = 0;
	this->mpRibbon2 = 0;
}

// @Ok
// @Matching
// 0x4286B0, shared with CCopBulletTracer::Move.
void CThugBulletTracer::Move(void)
{
	this->SetWidth();

	if (this->mMaxWidth)
		this->mMaxWidth = mMaxWidth - 1;

	this->mpRibbon->mpPoints[this->mAge].r = 0;
	this->mpRibbon->mpPoints[this->mAge].g = 0;
	this->mpRibbon->mpPoints[this->mAge].b = 0;
	this->mpRibbon2->mpPoints[this->mAge].r = 0;
	this->mpRibbon2->mpPoints[this->mAge].g = 0;
	this->mpRibbon2->mpPoints[this->mAge].b = 0;

	if ( this->mAge < 4 )
		this->mAge++;
	else
		this->Die();
}

// @Ok
// @Matching
// 0x4D2660, shared with CCopBulletTracer::SetWidth.
void CThugBulletTracer::SetWidth(void)
{
	print_if_false(this->mpRibbon && this->mpRibbon2, "NULL mpRibbon and mpRibbon2");

	for (i32 i = 0; i < 5; i++)
	{
		this->mpRibbon->mpPoints[i].Width = (u16)(this->mMaxWidth) * 2 + Rnd(this->mMaxWidth);

		this->mpRibbon2->mpPoints[i].Width = Rnd(this->mMaxWidth);
	}


	this->mpRibbon->mpPoints[0].Width = 0;
	this->mpRibbon->mpPoints[4].Width = Rnd(this->mMaxWidth);
	this->mpRibbon2->mpPoints[0].Width = 0;
	this->mpRibbon2->mpPoints[4].Width = 0;
}

// @Ok
// @Matching
// 0x4D2F10
void CreateThugRicochet(SLineInfo* line, u8 red, u8 green, u8 blue)
{
	CVector position(line->Position.vx, line->Position.vy, line->Position.vz);
	SFX_PlayPos(((red != 255 ? 12 : 39) + Rnd(2)) | 0x8000, &position, 0);
	CVector direction(line->StartCoords.vx, line->StartCoords.vy, line->StartCoords.vz);
	CVector end(line->EndCoords.vx, line->EndCoords.vy, line->EndCoords.vz);
	direction = (direction - end) >> 12;
	VectorNormal(reinterpret_cast<VECTOR*>(&direction), reinterpret_cast<VECTOR*>(&direction));
	i32 twiceDot = 2 * ((direction.vx * line->Normal.vx
			+ direction.vy * line->Normal.vy + direction.vz * line->Normal.vz) >> 12);
	direction.vx = ((twiceDot * line->Normal.vx) >> 12) - direction.vx;
	direction.vy = ((twiceDot * line->Normal.vy) >> 12) - direction.vy;
	direction.vz = ((twiceDot * line->Normal.vz) >> 12) - direction.vz;
	u32 absX = my_abs(direction.vx);
	u32 absY = my_abs(direction.vy);
	u32 absZ = my_abs(direction.vz);
	CVector cross;
	CVector perpendicular;
	if (absY >= absX)
	{
		if (absX <= absZ)
		{
			perpendicular.vx = 0;
			perpendicular.vy = -direction.vz;
			perpendicular.vz = direction.vy;
			goto havePerpendicular;
		}
		if (absY > absX)
			goto useXY;
	}
	if (absY <= absZ)
	{
		perpendicular.vx = direction.vz;
		perpendicular.vy = 0;
		perpendicular.vz = -direction.vx;
	}
	else
	{
useXY:
		perpendicular.vx = -direction.vy;
		perpendicular.vy = direction.vx;
		perpendicular.vz = 0;
	}
havePerpendicular:
	gte_ldopv1(reinterpret_cast<VECTOR*>(&direction));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&perpendicular));
	gte_op12();
	new CThugLaserPing(position, direction, perpendicular, red, green, blue);
	gte_stlvnl(reinterpret_cast<VECTOR*>(&cross));
	new CThugLaserPing(position, direction, cross, red, green, blue);
}

// @Ok
// @Matching
// 0x4D2DD0
CThugLaserPing::CThugLaserPing(
		const CVector& position, CVector& direction, CVector& perpendicular,
		u8 red, u8 green, u8 blue)
{
	this->field_88 = position;
	this->field_94 = direction;
	this->field_A0 = perpendicular;
	this->field_84 = 0;
	this->SetTexture(10, 1);
	this->SetSemiTransparent();
	this->SetTint(red, green, blue);
	this->mType = 34;
}

// @Ok
// @Matching
// 0x428B90, shared with CCopLaserPing::Move in the original binary.
void CThugLaserPing::Move(void)
{
	this->field_84 += 8;
	if (this->field_84 > 32)
	{
		this->Die();
		return;
	}
	this->mPosC.vx = this->field_88.vx + this->field_A0.vx * this->field_84;
	this->mPosC.vy = this->field_88.vy + this->field_A0.vy * this->field_84;
	this->mPosC.vz = this->field_88.vz + this->field_A0.vz * this->field_84;
	this->mPosD.vx = this->field_88.vx - this->field_A0.vx * this->field_84;
	this->mPosD.vy = this->field_88.vy - this->field_A0.vy * this->field_84;
	this->mPosD.vz = this->field_88.vz - this->field_A0.vz * this->field_84;
	this->mPos.vx = this->mPosC.vx + this->field_94.vx * (this->field_84 * 4);
	this->mPos.vy = this->mPosC.vy + this->field_94.vy * (this->field_84 * 4);
	this->mPos.vz = this->mPosC.vz + this->field_94.vz * (this->field_84 * 4);
	this->mPosB.vx = this->mPosD.vx + this->field_94.vx * (this->field_84 * 4);
	this->mPosB.vy = this->mPosD.vy + this->field_94.vy * (this->field_84 * 4);
	this->mPosB.vz = this->mPosD.vz + this->field_94.vz * (this->field_84 * 4);
}

// @Ok
void INLINE CThugPing::SetPosition(void)
{
	CSuper* v2 = reinterpret_cast<CSuper*>(Mem_RecoverPointer(&this->field_70));

	if (!v2)
		this->Die();
	else
		M3dUtils_GetDynamicHookPosition(
				reinterpret_cast<VECTOR*>(&this->mPos),
				v2,
				&this->field_78);
}

// @Ok
// @Matching
// 0x4D2510
CThugPing::CThugPing(CSuper* host, SHook* hook)
	: CSimpleAnim(&ZeroVector, 10, 0, 1, 0, -1)
{
	print_if_false(host != 0, "NULL pHost");
	print_if_false(hook != 0, "NULL pHook");
	this->field_78 = *hook;
	this->field_70 = Mem_MakeHandle(host);
	this->SetFrame(1);
	this->SetTint(128, 128, 128);
	this->mAngle = Rnd(1024);
	if (Rnd(2))
		this->mAngle = -this->mAngle;
	this->mScale = 400;
	this->SetPosition();
}

// @Ok
void CThugPing::Move(void)
{
	this->SetPosition();
	Bit_ReduceRGB(&this->mCodeBGR, 7);

	if ((this->mCodeBGR & 0xFFFFFF) == 0)
		this->Die();
}

// @Ok
void CThug::ClearAttackFlags(void)
{
	if ( G_GLOBAL_THUG == this )
	{
		G_GLOBAL_THUG = 0;
	}
	else if (this->field_3BC & 2)
	{
		G_ATTACK_FLAG_RELATED &= ~this->field_3BD;
	}

	this->field_3BC = 0;
	this->field_3BD = 0;
}

EXPORT i32 gThugTypeRelatedFirstFirst[2] = { 0x0D0D0100, 3 };

// @FIXME - add data
EXPORT u8 gThugTypeRelatedFirstThird[1];

EXPORT i32 gThugTypeRelatedSecondFirst[2] = { 0x6040504, 0 };

// The four animation modes start at these named tables in the PC exe.
// Each mode contains the two words copied to field_294 and field_298.
#define G_THUG_ANIM_MODES (reinterpret_cast<i32 (*)[2]>(0x00552178))
#define G_HENCHMAN_ANIM_MODES (reinterpret_cast<i32 (*)[2]>(0x00552198))

// @Ok
// @Matching
// 0x4D3190
i32 CThug::SetAnimMode(i32 mode, i32 playAnim)
{
	u8 oldMode = this->field_298.Bytes[1];
	i32 anim = -1;
	if (this->field_298.Bytes[1] != mode)
	{

		if (this->mType == 304)
		{
			switch (mode)
			{
				case 0:
					if (oldMode == 1)
						anim = 0;
					else if (oldMode == 2)
						anim = 27;
					this->field_294.Int = G_THUG_ANIM_MODES[0][0];
					this->field_298.Int = G_THUG_ANIM_MODES[0][1];
					break;
				case 1:
					this->field_294.Int = G_THUG_ANIM_MODES[1][0];
					this->field_298.Int = G_THUG_ANIM_MODES[1][1];
					break;
				case 2:
					if (oldMode == 1)
						anim = 8;
					else if (oldMode == 3)
						anim = 21;
					this->field_294.Int = G_THUG_ANIM_MODES[2][0];
					this->field_298.Int = G_THUG_ANIM_MODES[2][1];
					break;
				case 3:
					if (oldMode == 2)
						anim = 19;
					this->field_294.Int = G_THUG_ANIM_MODES[3][0];
					this->field_298.Int = G_THUG_ANIM_MODES[3][1];
					break;
				default:
					print_if_false(0, "Unknown anim mode.");
					break;
			}
		}
		else
		{
			switch (mode)
			{
				case 0:
					if (oldMode == 1 || oldMode == 2)
						anim = 3;
					this->field_294.Int = G_HENCHMAN_ANIM_MODES[0][0];
					this->field_298.Int = G_HENCHMAN_ANIM_MODES[0][1];
					break;
				case 1:
					this->field_294.Int = G_HENCHMAN_ANIM_MODES[1][0];
					this->field_298.Int = G_HENCHMAN_ANIM_MODES[1][1];
					break;
				case 2:
					this->field_294.Int = G_HENCHMAN_ANIM_MODES[2][0];
					this->field_298.Int = G_HENCHMAN_ANIM_MODES[2][1];
					break;
				case 3:
					this->field_294.Int = G_HENCHMAN_ANIM_MODES[3][0];
					this->field_298.Int = G_HENCHMAN_ANIM_MODES[3][1];
					break;
				default:
					print_if_false(0, "Unknown anim mode.");
					break;
			}
		}

		if (anim != -1)
		{
			if (playAnim)
				this->RunAnim(anim, 0, -1);
			return 1;
		}
	}
	return 0;
}

// @FIXME - add data
#ifndef SPIDEY_STANDALONE
EXPORT u8 gThugTypeRelatedSecondThird[1];
#else
extern u8 gThugTypeRelatedSecondThird[1];
#endif

// @Ok
// @Matching
void CThug::SetThugType(int type)
{
	this->mType = type;
	switch (type)
	{
		case 304:
			this->InitItem("thug");
			this->field_21E = 100;

			this->field_294.Int = gThugTypeRelatedFirstFirst[0];
			this->field_298.Int = gThugTypeRelatedFirstFirst[1];
			M3dUtils_ReadHooksPacket(this, gThugTypeRelatedFirstThird);

			break;
		case 312:
			if (Trig_GetLevelID() == 513)
				this->InitItem("henchngt");
			else
				this->InitItem("henchman");

			this->field_21E = 100;
			this->field_294.Int = gThugTypeRelatedSecondFirst[0];
			this->field_298.Int = gThugTypeRelatedSecondFirst[1];
			M3dUtils_ReadHooksPacket(this, gThugTypeRelatedSecondThird);
			break;
		default:
			print_if_false(0, "Unknown thug type!");
	}

}

// @Ok
// Reconstructed from IDA decompile of 0x4da560. Builds a point 100 units
// past the target (away from us) on x and z, keeping our own y. Tries the
// direct path to MechList first, then the far point, then (if the far
// point is blocked) falls back to CThug::TryAddingCollidePointToPath, which
// is the exact same >=100-unit-check-then-scale-by-3700 idiom this function
// inlines by hand in the original (confirmed by comparing against the
// already-decompiled TryAddingCollidePointToPath body below).
void CThug::RunToWhereTheActionIs(CVector* a2)
{
	if (Utils_CrapDist(this->mPos, *a2) > 3500)
		return;

	if (!this->AddPointToPath(&this->mPos, 0))
		return;

	CVector farPoint;
	farPoint.vx = a2->vx + ((a2->vx - this->mPos.vx <= 0) ? 409600 : -409600);
	farPoint.vy = this->mPos.vy;
	farPoint.vz = a2->vz + ((a2->vz - this->mPos.vz <= 0) ? 409600 : -409600);

	i32 addedPoint = 0;

	if (!G_MECHLIST_PLAYER->field_57C
			&& this->PathCheck(&this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 55) == 0
			&& this->AddPointToPath(&G_MECHLIST_PLAYER->mPos, 0))
	{
		addedPoint = 1;
	}
	else
	{
		i32 pathResult = this->PathCheck(&this->mPos, &farPoint, &farPoint, 55);

		if (pathResult == 0)
		{
			if (!this->AddPointToPath(&farPoint, 0))
				return;

			addedPoint = 1;
		}
		else if (pathResult == 2)
		{
			addedPoint = this->TryAddingCollidePointToPath(&farPoint);
		}
	}

	if (addedPoint)
	{
		this->Neutralize();
		this->field_2F0 |= 1;
		this->field_2A8 &= ~0x10000000;
		this->field_330 = 60;
		this->field_31C.bothFlags = 2;
		this->dumbAssPad = 0;
	}
}

// @Ok
void CThug::HelpOutBuddy(CMessage *pMessage)
{
	if (this->field_31C.bothFlags == 2 || this->field_31C.bothFlags == 1)
	{
		CItem *pItem = reinterpret_cast<CItem*>(Mem_RecoverPointer(&pMessage->mHandle));

		if (pItem)
			this->RunToWhereTheActionIs(&pItem->mPos);
	}
}

// @Ok
INLINE void CThug::PlayHitWallSound(void)
{
	if (!this->field_1F8)
	{
		if (this->field_218 & 0x400)
		{
			SFX_PlayPos(0x802F, &this->mPos, 0);
		}
		else
		{
			SFX_PlayPos(0x802E, &this->mPos, 0);
		}
	}
}

// @Ok
INLINE void CThug::StandStill(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->CycleAnim(this->mType != 304 ? 15 : 17, 1);
			this->dumbAssPad++;

			break;
		default:
			print_if_false(0, "Unknown substate.");
			break;
	}
}

// @Ok
void CThug::DieAfterFlyingAcrossRoom(void)
{
	if (this->ShouldFall(200, 0x5F000))
	{
		this->field_218 &= 0xFFFFFFFD;
		this->field_31C.bothFlags = 22;
	}
	else
	{
		this->SetHeight(1, 100, 600);
		this->PlayHitWallSound();
		this->field_31C.bothFlags = 26;
	}

	this->dumbAssPad = 0;
}

// @Ok
INLINE void CThug::RunAppropriateHitAnim(void)
{
	i32 anim;
	if (this->field_218 & 0x10000)
	{
		anim = this->mType != 304 ? 38 : 43;
	}
	else if (this->field_218 & 0x20000)
	{
		anim = this->mType != 304 ? 37 : 42;
	}
	else
	{
		anim = this->mType != 304 ? 18 : 25;
	}

	this->RunAnim(anim, 0, -1);
}

// @Ok
INLINE void CThug::StopShooting(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->RunAnim(9, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
i32 CThug::TryAddingCollidePointToPath(CVector* pVector)
{
	if (Utils_CrapDist(*pVector, this->mPos) < 0x64)
		return 0;

	CVector v5 = *pVector - this->mPos;
	v5 >>= 0xC;
	v5 *= 0xE74;
	v5 += this->mPos;
	return this->AddPointToPath(&v5, 0);
}


void validate_CThug(void){

	VALIDATE_SIZE(CGrenade, 0x160);
	VALIDATE(CGrenade, field_108, 0x108);
	VALIDATE(CGrenade, mStartTime, 0x14C);
	VALIDATE(CGrenade, mSettled, 0x150);
	VALIDATE(CGrenade, mpGlow, 0x154);
	VALIDATE(CGrenade, mRed, 0x158);
	VALIDATE(CGrenade, mExplodeOnContact, 0x15B);
	VALIDATE(CGrenade, field_15C, 0x15C);

	VALIDATE_SIZE(CThug, 0x3C0);

	VALIDATE(CThug, field_32C, 0x32C);
	VALIDATE(CThug, field_330, 0x330);
	VALIDATE(CThug, field_348, 0x348);
	VALIDATE(CThug, field_350, 0x350);
	VALIDATE(CThug, field_33C, 0x33C);

	VALIDATE(CThug, mHandle, 0x354);
	VALIDATE(CThug, mHandleTwo, 0x35C);

	VALIDATE(CThug, field_36C, 0x36C);
	VALIDATE(CThug, field_370, 0x370);
	VALIDATE(CThug, field_374, 0x374);
	VALIDATE(CThug, field_378, 0x378);
	VALIDATE(CThug, field_37C, 0x37C);
	VALIDATE(CThug, field_380, 0x380);
	VALIDATE(CThug, field_384, 0x384);
	VALIDATE(CThug, field_388, 0x388);
	VALIDATE(CThug, field_38C, 0x38C);

	VALIDATE(CThug, field_394, 0x394);
	VALIDATE(CThug, field_398, 0x398);
	VALIDATE(CThug, field_39C, 0x39C);

	VALIDATE(CThug, field_3A0, 0x3A0);
	VALIDATE(CThug, field_3A4, 0x3A4);

	VALIDATE(CThug, field_3B0, 0x3B0);
	VALIDATE(CThug, field_3B8, 0x3B8);

	VALIDATE_SIZE(IntToBytes, 0x4);
}

void validate_CThugPing(void)
{
	VALIDATE_SIZE(CThugPing, 0x80);

	VALIDATE(CThugPing, field_70, 0x70);
	VALIDATE(CThugPing, field_78, 0x78);
}

#include "my_patch.h"

// @Bogus
// Left out on purpose:
//   CThug::CThug (0x004D2AB0). Hooking a constructor stamps our vtable on the
//     object. AI and Hit are now restored for the standalone engine, but their
//     callees still need a Windows global and allocator audit before replacing
//     the original game's vtable.
//   Everything with no address of its own (ShouldIShootPlayer, DrawBarrelFlash,
//     CheckToShoot, AdjustPosPlaySound, SetAttacker, SpideyAnimUppercut,
//     CanAck, SetHitDirectionFlag, ClearAttackFlags, HelpOutBuddy,
//     PlayHitWallSound, StandStill, DieAfterFlyingAcrossRoom,
//     RunAppropriateHitAnim, StopShooting, TryAddingCollidePointToPath,
//     CThugPing::SetPosition) was inlined by the original compiler.
//   CThugPing::Move is the same body as CCopPing::Move (0x004282A0) after link
//     time folding, and cop.cpp already hooks it.
void patch_thug(void)
{
	PATCH_PUSH_RET(0x004D2440, Thug_RelocatableModuleInit);
	PATCH_PUSH_RET(0x004D2460, Thug_RelocatableModuleClear);
	PATCH_PUSH_RET(0x004D2490, Thug_CreateThug);
	PATCH_PUSH_RET(0x004D3800, CThug::GetLaunched);
	PATCH_PUSH_RET(0x004D3A10, CThug::GetReadyToShootHostage);
	PATCH_PUSH_RET(0x004D42D0, CThug::StrikeUpConversation);
	PATCH_PUSH_RET(0x004D4520, CThug::Caution);
	PATCH_PUSH_RET(0x004D4F30, CThug::MonitorSpitPlease);
	PATCH_PUSH_RET(0x004D5DB0, CThug::WarnOtherThugs);
	PATCH_PUSH_RET(0x004D6260, CThug::SetUpLaser);
	PATCH_PUSH_RET(0x004D7670, CThug::TakeHit);
	PATCH_PUSH_RET(0x004D8D70, CThug::CheckFallBack);
	PATCH_PUSH_RET(0x004D9A40, CThug::DetermineFightState);
	PATCH_PUSH_RET(0x004D9F30, CThug::Acknowledge);
	PATCH_PUSH_RET(0x004DA100, CThug::Guard);
	PATCH_PUSH_RET(0x004DA300, CThug::PlaySounds);
	PATCH_PUSH_RET(0x004DA560, CThug::RunToWhereTheActionIs);
	PATCH_PUSH_RET(0x004DA760, CThug::LookForPlayer);
	PATCH_PUSH_RET(0x004DA8B0, CThug::BackpedalPlease);
	PATCH_PUSH_RET(0x004DAA60, CThug::LookConfused);
	// the linker put this one in baddy.cpp's address range.
	PATCH_PUSH_RET(0x00403C00, CThug::CycleOrContinueAnim);

	PATCH_PUSH_RET_POLY(0x004D2C40, CThug::SetThugType, "?SetThugType@CThug@@UAEXH@Z");
	PATCH_PUSH_RET_POLY(0x004D2D00, CThug::~CThug, "??1CThug@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004D36E0, CThug::SetParamByIndex, "?SetParamByIndex@CThug@@UAEXHH@Z");
	PATCH_PUSH_RET_POLY(0x004D38E0, CThug::Grab, "?Grab@CThug@@UAEEPAVCVector@@@Z");
	PATCH_PUSH_RET_POLY(0x004D3940, CThug::TugImpulse, "?TugImpulse@CThug@@UAEEPAVCVector@@00@Z");
	PATCH_PUSH_RET_POLY(0x004DB890, CThug::CreateCombatImpactEffect, "?CreateCombatImpactEffect@CThug@@UAEXPAVCVector@@H@Z");
}
