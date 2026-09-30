#include "baddy.h"
#include "validate.h"
#include "utils.h"
#include "mem.h"
#include "message.h"
#include "web.h"
#include <cmath>
#include "trig.h"
#include "ai.h"
#include "ps2lowsfx.h"
#include "my_assert.h"
#include "spool.h"
#include "m3dcolij.h"
#include "mysterio.h"
#include "ps2gamefmv.h"
// CPowerUp::SetGravity is called on the object PowerUp_Create returns in
// both builds (ExecuteCommand case 0x42A0)
#include "powerup.h"
// CCarnage::MakeSonicRipple is the vtable slot 17 call in ExecuteCommand
// case 0x430B, both builds
#include "carnage.h"
// Panel_DestroyCompass for ~CScriptOnlyBaddy
#include "panel.h"
#include "simby.h"
#include "spidey.h"

// @Ok
// @Matching
// 0x404700
void CBaddy::ElasticCollision(CBody* source, CBody* target, CVector& velocity, i32 strength)
{
	SHitInfo hit;
	CVector direction;
	if (target->mHealth > 0)
	{
		direction = target->mPos - (source->mPos - velocity * 2);
		direction.vy = 0;
		direction >>= 10;
		VectorNormal(reinterpret_cast<VECTOR*>(&direction), reinterpret_cast<VECTOR*>(&direction));
		hit.field_C = direction;
		hit.field_1A = strength;
		hit.field_18 = strength * velocity.Length();
		hit.field_4 = 25;
		hit.field_0 = 42;
		target->Hit(&hit);
	}
}
#ifdef SPIDEY_STANDALONE
// headers of the real implementations behind the ExecuteCommand
// forward-to-original helpers (standalone build only, see below)
#include "bit.h"
#include "exp.h"
#include "flash.h"
#include "camera.h"
#include "ps2redbook.h"
#include "chunk.h"
#include "effects.h"
#include "ps2funcs.h"
#endif


#ifndef SPIDEY_STANDALONE
CBody* ControlBaddyList;
#else
extern CBody* ControlBaddyList;
#endif
#ifndef SPIDEY_STANDALONE
CBaddy* BaddyList;
#else
extern CBaddy* BaddyList;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT CSVector gTrajectoryVector;
#else
extern CSVector gTrajectoryVector;
#endif
#ifndef SPIDEY_STANDALONE
i32 gAttackRelated;
#else
extern i32 gAttackRelated;
#endif
#ifndef SPIDEY_STANDALONE
u8 gObjFileRegion;
#else
extern u8 gObjFileRegion;
#endif

// @FIXME - incorrect type
#ifndef SPIDEY_STANDALONE
i32 gBossRelated;
#else
extern i32 gBossRelated;
#endif

// @Ok
// @Matching
INLINE i16 CBaddy::GetScriptValue(void)
{
	u16 result = *this->field_24C;
	this->field_24C++;

	if ((result & 0x8000u) == 0 && ((result & 0x2000u)))
		return this->GetVariable(result);

	return result;
}

// @Ok
void CBaddy::GetWaypointNearTarget(
		CVector* a2,
		i32 a3,
		i32 a4,
		CVector* a5)
{
	i32 unk;
	i32 v5; // eax
	i32 v6; // edi
	i32 v7; // ecx

	unk = a4 & 0x3F;
	v5 = 1 << (unk & 7);
	*a5 = *a2;
	v6 = unk & 8 ? 2 : 1;
	v7 = unk & 0x10 ? 2 : 1;

	if ( unk > 0x1F )
	{
		v6 += 2;
		v7 += 2;
	}
	if ( (v5 & 0xEE) != 0 )
		a5->vx += a3 * v6 * (v5 > 8 ? -1 : 1);
	if ( (v5 & 0xBB) != 0 )
		a5->vz += a3 * v7 * ((v5 & 0x83) != 0 ? 1 : -1);
}

// @Ok
// Fixed bug: PathCheck returns 0 when the path is clear (same polarity as
// PlayerIsVisible's "!this->PathCheck(...)" a few functions down). Both
// calls here used the return value the wrong way round (nonzero treated as
// "clear"), confirmed against the original disasm at 0x4048f0.
// PathCheckGuts is now decompiled for real (see below), so these calls
// compile to real code again instead of the dead-code-folded stub path.
i32 CBaddy::AddPointToPath(
		CVector* pVec,
		i32 a3)
{
	CVector v21;
	v21.vx = 0;
	v21.vy = 0;
	v21.vz = 0;

	CVector v20;
	v20 = *pVec;
	v20.vy = this->mPos.vy;

	if (this->field_1F4 > 0)
	{
		Trig_GetPosition(&v21, this->field_1F4);
		v21.vy = this->field_29C;

		if ((!a3 || Utils_CrapDist(v21, v20) < a3) && !this->PathCheck(&v21, &v20, 0, 55))
		{
			this->field_1F0 = 1;
			this->field_1A8[1] = v20;
			this->field_2A8 |= 0x20000000;
			return 1;
		}
	}
	else if (!this->field_1F0)
	{
		this->field_1F0 = 1;
		this->field_1A8[1] = v20;
		return 1;
	}

	for (i32 i = 0; i<4 && i < this->field_1F0; i++)
	{
		if ( (!a3 || Utils_CrapDist(this->field_1A8[1+i], v20) < a3)
				&& !this->PathCheck(&this->field_1A8[1+i], &v20, 0, 55))
		{
			this->field_1F0 = i + 2;
			this->field_1A8[i+2] = v20;
			this->field_2A8 |= 0x20000000;
			return 1;
		}
	}

	return 0;
}


// @Ok
i32 CBaddy::GetNextWaypoint(void)
{
	if (this->field_1F4 >= 0)
	{
		u16 *LinksPointer = reinterpret_cast<u16*>(Trig_GetLinksPointer(this->field_1F4));
		if (!*LinksPointer)
			return 0;

		this->field_1F4 = LinksPointer[1];

		u16* v4 = reinterpret_cast<u16*>(G_OFFSETLIST[this->field_1F4]);
		this->field_2F0 = 0;

		if (*v4 == 1000 || *v4 == 1002)
		{
			CVector v9;
			v9.vx = 0;
			v9.vy = 0;
			v9.vz = 0;

			u16 *position = Trig_GetPosition(&v9, this->field_1F4);
			this->field_2F4 = 0;

			this->ParseScript(&position[3]);
		}

		return 1;
	}

	return 0;
}

// @Ok
// Checked against the original disasm at 0x404b60. Structure differs
// slightly (original compares Bytes[0] vs Bytes[1] first and skips
// straight to the CycleAnim tail when they are equal) but that is
// equivalent to the if/if shape below (when Bytes[0]==Bytes[1] the second
// if is unconditionally true). No functional bug found.
void CBaddy::RunAppropriateAnim(void)
{
	if (this->field_2AC & 0x40000)
	{
		u8 v2 = this->field_294.Bytes[0];
		u8 v3 = this->field_294.Bytes[1];

		if (v2 != v3)
		{
			if (this->mAnim != v3
					&& this->mAnim != v2)
			{
				this->RunAnim(this->field_294.Bytes[0], 0, -1);
				return;
			}
		}

		if (v2 == v3
				|| this->mAnim != v2
				|| this->mAnimFinished)
		{
			this->CycleAnim(this->field_294.Bytes[1], 1);
		}
	}
	else if (this->field_2AC & 0x10000)
	{
		if (this->mAnim != this->field_294.Bytes[2] || this->mAnimFinished)
				this->RunAnim(this->field_294.Bytes[2], 0, -1);
	}
	else if (this->field_2AC & 0x20000)
	{
		if (this->mAnim != this->field_294.Bytes[3] || this->mAnimFinished)
				this->RunAnim(this->field_294.Bytes[3], 0, -1);
	}
	else
	{
		this->CycleAnim(this->field_298.Bytes[0], 1);
	}
}

// @Ok
// @Test
// @Note: offsets on the a3[v19] thingy are -4 somehow
i32 CBaddy::SmackSpidey(
		i32 a2,
		CVector *a3,
		i32 a4,
		i32 a5)
{
	SHook v21, v22;

	v21.Part.vx = 0;
	v21.Part.vy = 0;
	v21.Part.vz = 0;
	v22.Part.vx = 0;
	v22.Part.vy = 0;
	v22.Part.vz = 0;

	i32 v19 = 0;

	CVector firstVec;

	if (!G_NUM_DOMES)
	{
		for (i32 i = 0; ; i++)
		{
			if (i >= 32)
				return 0;

			if (a2 & (1 << i))
			{
				v21.Offset = i;
				v21.Part.vz = 0;
				v21.Part.vy = 0;
				v21.Part.vx = 0;

				firstVec = a3[v19];

				M3dUtils_GetDynamicHookPosition(
						reinterpret_cast<VECTOR*>(&a3[v19++]),
						this,
						&v21);

				if (a4)
				{
					CVector secondVec;
					secondVec = a3[v19 - 1];

					secondVec += (secondVec - firstVec) >> 1;

					if (Web_CollideWithSuper(G_MECHLIST_PLAYER, &firstVec, &secondVec, &v22, 0x2000))
						break;
				}
			}
		}

		this->CreateCombatImpactEffect(&a3[v19 - 1], 0);

		if (this->field_1FC < 0xA)
		{
			SFX_PlayPos(0xFu, &G_MECHLIST_PLAYER->mPos, 0);
		}
		else if (this->field_1FC < 0x14)
		{
			SFX_PlayPos(0x10u, &G_MECHLIST_PLAYER->mPos, 0);
		}
		else
		{
			SFX_PlayPos(0x11u, &G_MECHLIST_PLAYER->mPos, 0);
		}

		if (!a5)
		{
			SHitInfo v27;

			v27.field_C.vx = 0;
			v27.field_C.vy = 0;
			v27.field_C.vz = 0;

			v27.field_0 = 15;
			v27.field_4 = 8;
			v27.field_8 = this->field_1FC;
			v27.field_1 = v22.Offset;

			v27.field_C.vz = (G_MECHLIST_PLAYER->mPos.vz - this->mPos.vz) >> 12;
			v27.field_C.vx = (G_MECHLIST_PLAYER->mPos.vx - this->mPos.vx) >> 12;
			v27.field_C.vy = 0;

			VectorNormal(
					reinterpret_cast<VECTOR*>(&v27.field_C),
					reinterpret_cast<VECTOR*>(&v27.field_C));

			G_MECHLIST_PLAYER->Hit(&v27);

			if (G_MECHLIST_PLAYER->mHealth <= 0)
				this->Victorious();
		}
		return 1;
	}
	return 0;
}

// @Ok
// @Matching
INLINE i32 CBaddy::DistanceToPlayer(i32 a2){

	if (this->field_208 && G_ATTACK_RELATED - this->field_208 <= a2 )
		return this->field_204;

	this->field_208 = G_ATTACK_RELATED;
	this->field_204 = Utils_CrapXZDist(this->mPos, G_MECHLIST_PLAYER->mPos);

	return this->field_204;
}

// @Ok
int CBaddy::TrapWeb(void){
	if((this->field_2A8 & 0x10000) || (this->mHealth <= 0)){
		return 0;
	}

	new CMessage(NULL, this, 5, 0);

	return 1;
}

// @Ok
INLINE void CBaddy::CleanUpMessages(i32 a2, i32 a3)
{

	CMessage *pMessage = this->pMessage;
	while (pMessage)
	{
		CMessage *curMessage = pMessage;
		pMessage = pMessage->mNext;
		if (
				curMessage->field_10 & 1
				|| a2
				|| curMessage->field_14 == a3)
		{
			delete curMessage;
		}
	}
}

// @Ok
u16 CBaddy::CheckStateFlags(SStateFlags *sFlags, int a3){

	if(this->field_314 < 0){
		if (this->field_31C.bothFlags == -this->field_314)
			return 0;
	}
	else if(this->field_31C.bothFlags == sFlags[this->field_314].flags[0]){
		print_if_false(sFlags[this->field_314].flags[1] != 0, "This shouldn't be zero.  Remove state from table.");
		return sFlags[this->field_314].flags[1];
	}

	for (i32 i = 0; i< a3; i++){

		if (this->field_31C.bothFlags != (u16) sFlags[i].flags[0]){
			continue;
		}

		this->field_314 = i;
		print_if_false(sFlags[i].flags[1] != 0, "This shouldn't be zero.  Remove state from table.");
		return sFlags[i].flags[1];
	}

	this->field_314 = -this->field_31C.flags[0];
	return 0;	
}

// @Ok
// Fixed bug: the non-snap branch returned the pre-adjustment delta (v4).
// The original computes v4 -= v5 before the sign check and returns that
// (the remaining delta after this frame's turn), confirmed against the
// original disasm at 0x4030c0.
int CBaddy::YawTowards(int a2, int a3){

	int vy; // edi
	int v4; // eax
	int v5; // edx


	vy = this->mAngles.vy;
	v4 = a2 - vy;

	if ( a2 - vy < -2048 )
		v4 += 4096;
	if ( v4 > 2048 )
		v4 -= 4096;

	if ( !v4 )
	{
		this->mAngAcc.vy = 0;
		this->mAngVel.vy = 0;
		return v4;
	}

	v5 = (a3 * v4) >> 8;
	this->mAngles.vy += v5;
	v4 -= v5;
	if ( v5 && ((int)this->mAngles.vy - a2 > 0) != (vy - a2> 0))
	{
		return v4;
	}

	this->mAngles.vy = a2;
	this->mAngAcc.vy = 0;
	this->mAngVel.vy = 0;
	return 0;
}

// @Ok
int CBaddy::RunTimer(int *a2)
{
	*a2 -= this->field_80;
	if ( *a2 < 0 )
		*a2 = 0;
	return *a2;
}

// @Ok
// Decompiled from the original disasm at 0x403350 (1661 bytes, 505
// instructions), read via IDA/Hex-Rays and cross-checked against the raw
// disasm for every call site (Hex-Rays prints the pushed stack argument,
// not the thiscall ecx, for the CVector member operators here, so each
// one was resolved by hand: which local is "this" and which is the
// pushed operand). All callees already exist in the repo (CVector free
// operators in vector.cpp, VectorNormal, Utils_LineOfSight,
// Utils_GetGroundHeight, print_if_false).
//
// pStart/pEnd are the two endpoints of the path segment. pHitOut is an
// optional out-param for the exact blocked point; when null a local
// scratch CVector is used instead, so the internal logic always has a
// valid pointer. flags is passed straight through to Utils_LineOfSight
// and Utils_GetGroundHeight.
//
// Shape: extend the segment past both ends by half of mRMinor along its
// own (normalized, Y-zeroed unless field_2A8 bit 0x20000 is set)
// direction, giving farPoint/nearPoint. Test farPoint->nearPoint with
// Utils_LineOfSight; if blocked, snap nearPoint to the exact hit. Then
// pick a perpendicular test offset (xStep/zStep, derived from mRMinor
// and whichever axis the segment leans on) and test the segment shifted
// by +half that offset, then by -half (a 3-line "capsule" test: center,
// plus-side, minus-side). If the whole capsule is clear, walk the
// collision grid in subdivided steps via Utils_GetGroundHeight
// (halving the step until each segment is under ~1024000 units) and
// return 0 (found ground the whole way) or 1 (hit a gap). Otherwise
// interpolate a precise hit point along the near/far chord, write it to
// *pHitOut, and fall through to a final check: the hit point must lie
// on the same side of pStart as pEnd on both axes, or the result is
// forced to 4; otherwise 2.
i32 CBaddy::PathCheckGuts(CVector* pStart, CVector* pEnd, CVector* pHitOut, i32 flags)
{
	CVector fallbackHit;
	fallbackHit.vx = 0;
	fallbackHit.vy = 0;
	fallbackHit.vz = 0;

	CVector* pHit = pHitOut ? pHitOut : &fallbackHit;
	i32 wantHitPoint = (pHitOut != 0);
	i32 losFlags = this->field_2A8 & 0x100000;

	CVector alongDir = (*pEnd - *pStart) >> 12;
	if ((this->field_2A8 & 0x20000) == 0)
		alongDir.vy = 0;
	VectorNormal(reinterpret_cast<VECTOR*>(&alongDir), reinterpret_cast<VECTOR*>(&alongDir));
	alongDir *= (this->mRMinor >> 1);

	CVector nearPoint = *pEnd + alongDir;
	CVector farPoint = *pStart - alongDir;
	if ((this->field_2A8 & 0x20000) == 0)
		nearPoint.vy = farPoint.vy;

	i32 gotHit = 0;
	if (Utils_LineOfSight(&farPoint, &nearPoint, pHit, losFlags) == 0)
	{
		gotHit = 1;
		nearPoint = *pHit;
	}

	i32 dz = farPoint.vz - nearPoint.vz;
	i32 dx = farPoint.vx - nearPoint.vx;
	i32 absDx = my_abs(dx);
	i32 absDz = my_abs(dz);

	i32 xStep = 0;
	i32 zStep = 0;
	if (absDz <= (absDx >> 1))
	{
		if (absDx <= (absDz >> 1))
		{
			zStep = this->mRMinor << 11;
			xStep = zStep;
		}
		else
		{
			zStep = this->mRMinor << 12;
		}
	}
	else
	{
		xStep = this->mRMinor << 12;
	}

	CVector testA = farPoint;
	CVector testB = nearPoint;

	if ((this->field_2A8 & 0x20000) == 0)
		print_if_false(nearPoint.vy == farPoint.vy, "Hmmm... these aren't equal!  Fire Matt immediately.");

	testA.vx += xStep >> 1;
	testA.vz += zStep >> 1;
	testB.vx += xStep >> 1;
	testB.vz += zStep >> 1;

	CVector chord;
	i32 result = 0;

	if (Utils_LineOfSight(&testA, &testB, pHit, losFlags) == 0)
	{
		if (!wantHitPoint)
			goto finalCheck;

		chord = testA - *pHit;
		gotHit = 0;

		testA.vx -= xStep;
		testA.vz -= zStep;
		testB.vx = pHit->vx - xStep;
		testB.vy = pHit->vy;
		testB.vz = pHit->vz - zStep;

		if (Utils_LineOfSight(&testA, &testB, pHit, losFlags) != 0)
			goto haveChord;
		goto recomputeChord;
	}

	testA.vx -= xStep;
	testA.vz -= zStep;
	testB.vx -= xStep;
	testB.vz -= zStep;

	if (Utils_LineOfSight(&testA, &testB, pHit, losFlags) == 0)
	{
		gotHit = 0;
	}
	else if (gotHit == 0)
	{
		if (this->field_2A8 & 0x2020000)
			return 0;

		i32 targetY = this->field_29C + (this->field_21E << 12);

		i32* pDominant = (absDz <= absDx) ? &dx : &dz;
		i32 steps = 1;
		for (i32 i = 1; my_abs(*pDominant) > 1024000; i *= 2)
		{
			steps += i;
			dz >>= 1;
			dx >>= 1;
		}

		if (steps == 0)
			return 0;

		CVector walkPos;
		walkPos.vx = nearPoint.vx;
		walkPos.vy = targetY;
		walkPos.vz = nearPoint.vz;

		i32 remaining = steps - 1;
		while (Utils_GetGroundHeight(&walkPos, flags, flags, 0) != -1)
		{
			walkPos.vx += dx;
			walkPos.vz += dz;
			if (remaining-- == 0)
				return 0;
		}
		return 1;
	}

	if (!(wantHitPoint && gotHit == 0))
		goto finalCheck;

recomputeChord:
	chord = testA - *pHit;

haveChord:
	if (gotHit == 0)
	{
		CVector chordFull = nearPoint - farPoint;
		i32 lenNear = chord.Length();
		i32 lenChordFull = chordFull.Length();
		i32 divisor = (this->mRMinor >> 1) + lenChordFull;

		chordFull >>= 12;
		chordFull *= lenNear;
		chordFull /= divisor;
		chordFull <<= 12;

		*pHit = farPoint + chordFull;
	}

finalCheck:
	if ((pHit->vx - pStart->vx > 0) != (pEnd->vx - pStart->vx > 0))
		return 4;

	result = 2;
	if ((pHit->vz - pStart->vz > 0) != (pEnd->vz - pStart->vz > 0))
		return 4;

	return result;
}

// @Ok
// @Matching
INLINE i32 CBaddy::PathCheck(CVector* a2, CVector* a3, CVector* a4, i32 a5)
{

	i32 v5 = G_BADDY_COLLISION_CHECK;

	if ((this->field_2A8 & 0x2000))
		G_BADDY_COLLISION_CHECK = 1;

	i32 result = this->PathCheckGuts(a2, a3, a4, a5);
	G_BADDY_COLLISION_CHECK = v5;
	return result;
}


// @Ok
// @Matching
CBody* CBaddy::StruckGameObject(i32 a2, i32 a3)
{
	CBody *result;
	  if ( !a2
			|| (result = Utils_CheckObjectCollision(
				&this->field_2FC,
				&this->mPos,
				G_MECHLIST_PLAYER,
				this)) == 0 )
	  {
		  if (a3 && (result = Utils_CheckObjectCollision(&this->field_2FC, &this->mPos, G_BADDY_LIST, this)))
		  {
			  DoAssert(result != this, "smoething's wrong in the state of denmark");
			  return result;
		  }

		  return NULL;
	  }

	  return result;
}

// @Ok
void CBaddy::Neutralize(void)
{
	this->MarkAIProcList(1, 0, 0);

	this->mAcc.vz = 0;
	this->mAcc.vy = 0;
	this->mAcc.vz = 0;

	this->mVel.vz = 0;
	this->mVel.vy = 0;
	this->mVel.vx = 0;

	this->mAcc.vz = 0;
	this->mAcc.vy = 0;
	this->mAcc.vx = 0;

	this->mAngVel.vz = 0;
	this->mAngVel.vy = 0;
	this->mAngVel.vx = 0;

	this->mAngAcc.vz = 0;
	this->mAngAcc.vy = 0;
	this->mAngAcc.vx = 0;

	this->field_27C.vz = 0;
	this->field_27C.vy = 0;
	this->field_27C.vx = 0;

	this->field_2A8 &= 0xB7FFFFFB;
}

// @Ok
// Checked against the original disasm at 0x403ef0 (SEH-protected, matches
// the CMessage-constructor-throws frame class from CLAUDE.md). Condition,
// Mem_RecoverPointer call and Burst call all match; mHealth's offset 0xE2
// is confirmed in ob.cpp's VALIDATE(CBody, mHealth, 0xE2).
int CBaddy::TugWeb(void)
{
	if ( (this->field_2A8 & 0x200) || this->mHealth <= 0)
	{

		CTrapWebEffect *trapWeb = reinterpret_cast<CTrapWebEffect*>(
				Mem_RecoverPointer(reinterpret_cast<SHandle*>(&this->field_10C)));

		if (trapWeb)
			trapWeb->Burst();

		return 0;
	}

	new CMessage(0, this, 6, 0);
	return 1;
}

// @Ok
INLINE void CBaddy::GetLocalPos(CVector *a2, CVector *a3, CSVector *a4)
{
	MATRIX v7;

	if (a4)
	{
		M3dMaths_RotMatrixYXZ(
				reinterpret_cast<SVECTOR*>(a4),
				&v7);
	}
	else
	{
		M3dMaths_RotMatrixYXZ(
				reinterpret_cast<SVECTOR*>(&this->mAngles),
				&v7);
	}

	gte_SetRotMatrix(&v7);
	*a3 = *a2;
	gte_ldlvl(reinterpret_cast<VECTOR*>(a3));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(a3));

}

// @Ok
int CBaddy::MakeSpriteRing(CVector* arg0)
{
	CVector mPos;
	mPos.vx = 0;
	mPos.vy = 0;
	mPos.vz = 0;
	
	if (!arg0)
	{
		mPos = this->mPos;
	}
	else
	{
		CVector tmp = *arg0;

		tmp >>= 12;
		this->GetLocalPos(&tmp, &mPos, 0);

		mPos <<= 12;
		mPos += this->mPos;
	}

	mPos.vy = Utils_GetGroundHeight(&this->mPos, 300, 600, 0) - 0xA000;
	return Bit_MakeSpriteRing(&mPos, 24, 8, 1, 512, 32, 16, 0);
}

// @Ok
// @AlmostMatching: vector assingment is different
int CBaddy::SetHeight(int a2, int a3, int a4)
{
	if (this->field_2A8 & 0x8000)
	{
		return 1;
	}

	if (this->field_2A4
			|| a2
			|| (this->field_2A8 & 0x40000) != 0
			|| (this->field_2A8 & 0x8000000) != 0 && ((this->field_2A8 & 0x20000000) != 0 || (this->field_2F0 & 4) != 0) )
	{
		u8 v9;

		if (a2 || ( 
					v9 = this->field_80, v9 += this->field_213,
				v9 >= this->field_212))
		{
			CVector v16;
			v16.vx = this->mPos.vx;
			v16.vy = this->mPos.vy + (this->field_21E << 12);
			v16.vz = this->mPos.vz;

			i32 height = Utils_GetGroundHeight(&v16, a3, a4, &this->field_2A4);
			if (height == -1)
			{
				return 0;
			}

			if (this->field_2A4)
			{
				if (this->field_2A4->mVel.vy == 0)
				{
					this->field_2A4 = 0;
				}
				else
				{
					height += this->field_2A4->mVel.vy;
				}
			}

			this->field_2A0 = height;
			if (!this->field_212)
				this->field_212 = 30;
			this->field_213 = 0;
		}
	}

	i32 v13 = this->field_2A0 - (this->field_21E << 12);

	if (v13 != this->mPos.vy)
	{
		this->mPos.vy += ((v13 - this->mPos.vy) >> 2);
		if (my_abs(this->mPos.vy - v13) <= (this->field_2A4 == 0 ? 12288 : 122880))
		{
			this->mPos.vy = v13;
		}
		else
		{
			return 1;
		}
		
	}

	return 2;
}


// @Ok
void INLINE CBaddy::SendDeathPulse(void)
{

	if (!this->field_211 && this->mNode != 0xFFFF)
	{
		this->field_211 = 1;
		Trig_SendPulse(
				reinterpret_cast<u16*>(Trig_GetLinksPointer(this->mNode & 0xFFFF)));
	}
}

// @Ok
int CBaddy::Die(int a2)
{
	if(!this->IsDead())
	{
		int v8;
		int v9;
		switch (a2)
		{
			case 0:
				if (!(this->field_2A8 & 0x4000))
				{
					this->SendDeathPulse();
				}
			case 3:
				this->mCBodyFlags |= 0x40;
				this->mFlags |= 1;
				break;
			case 1:
				if (!(this->field_2A8 & 0x4000))
				{
					this->SendDeathPulse();
				}

				this->mFlags |= 0x800;
				this->mTRN = 128;
				this->KillShadow();

				this->mFlags |= 0x400;
				this->mRGB = 0x404040;
				this->field_1F8 = 0;
				break;
			case 2:
				v8 = this->field_1F8;
				v9 = v8 + 1;
				this->field_1F8 = v9;
				if ( v8 >= 40 )
					return 1;

				v9 = ((6553 * (40 - v9)) >> 12);
				v8 = v9;

				v8 <<= 8;
				v8 |= v9;
				v8 <<= 8;
				v8 |= v9;

				this->mRGB =  v8;
				break;


			default:
				print_if_false(0, "Unknown die state");
		}
	}

	return 0;
}

// @Ok
INLINE void CBaddy::CleanUpAIProcList(i32 a2)
{
	CAIProc *pProc = this->mAIProcList;
	while (pProc)
	{
		CAIProc *curProc = pProc;
		pProc = pProc->mNext;
		if (
				 a2
				|| curProc->field_10 & 1)
		{
			delete curProc;
		}
	}
}

// @Ok
// @Matching
i32 CBaddy::BumpedIntoSpidey(i32 a2)
{
	i32 v4;

	if (this->field_208 && G_ATTACK_RELATED - this->field_208 <= 4)
	{
		v4 = this->field_204;
	}
	else
	{
		this->field_208 = G_ATTACK_RELATED;
		v4 = Utils_CrapXZDist(this->mPos, G_MECHLIST_PLAYER->mPos);
		this->field_204 = v4;
	}

	if (v4 < a2)
	{
		i32 res = this->field_21E - G_MECHLIST_PLAYER->field_EA8 - (G_MECHLIST_PLAYER->mPos.vy >> 12) + (this->mPos.vy >> 12);

		if (my_abs(res) < 200)
			return 1;
	}

	return 0;
}


// @Ok
// @AlmostMatching: vector assingment is different
i32 CBaddy::PlayerIsVisible()
{
	if (!G_MECHLIST_PLAYER->IsDead() &&
			Utils_LineOfSight(&this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 0)
			)
	{
		if (!this->PathCheck( &this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 55))
		{
			this->field_1A8[0] = G_MECHLIST_PLAYER->mPos;
			this->field_2A8 |= 0x800;
		}
		return 1;
	}

	return 0;
}

// @Ok
int CBaddy::ShouldFall(int a2, int a3)
{
	int GroundHeight = Utils_GetGroundHeight(&this->mPos, a2, 4096, 0);
	if (GroundHeight == -1)
	{
		this->field_308 = this->mPos.vy - 100;
		this->KillShadow();

		this->field_2A8 &= 0xFFFFFBFF;

		return GroundHeight;
	}

	int v7 = GroundHeight - (this->field_21E << 12);

	if (v7 - this->mPos.vy > a3)
	{
		this->KillShadow();

		this->field_308 = v7;
		this->field_2A8 |= 0x400;

		return v7 - this->mPos.vy;
	}

	return 0;
}

// @Ok
// @Matching
i32 CBaddy::CheckSightCone(i32 a2, i32 a3, i32 a4, i32 a5, CBody *a6)
{
	CSVector v16;
	if (!a3)
		return 0;


	i32 v12 = a6->mPos.vy - this->mPos.vy;
	if (my_abs(v12) > (a3 << 12))
		return 0;

	i32 v13;
	if (a6 == G_MECHLIST_PLAYER)
	{
		v13 = this->DistanceToPlayer(2);
	}
	else
	{
		v13 = Utils_CrapXZDist(this->mPos, a6->mPos);
	}

	if (v13 < a5)
		return 1;

	if (v13 > a4)
		return 0;

	Utils_CalcAim(&v16, &this->mPos, &a6->mPos);
	i32 v14 = v16.vy - this->mAngles.vy;
	if (v14 < -2048)
	{
		v14 += 4096;
	}
	else if (v14 >= -2048)
	{
		if (v14 > 2048)
		{
			v14 -= 4096;
		}
	}

	return my_abs(v14) <= (a2 >> 1);
}

// byte right before gWhatIf (0x60CFC5, ob.cpp). Name from spideypc_names.txt
// (maintainer's IDB extraction), tentative.
static u8 * const gSubmarinerDieRelated = (u8*)0x60CFC4;

#ifdef SPIDEY_STANDALONE
static u16 gTraceLastScriptCmd = 0;   // SPIDEY_TRACE_SCRIPT debugging aid
#endif

// @Ok
// @Matching
void CBaddy::ParseScript(u16 *a2)
{
	this->field_24C = reinterpret_cast<i16*>(a2);

	while (*this->field_24C != 0x4100)
	{
		u16 opcode = *this->field_24C;
		this->field_24C++;

		print_if_false((opcode & 0x6000) != 0, "Bad script command");
#ifdef SPIDEY_STANDALONE
		if ((opcode & 0x6000) == 0 && getenv("SPIDEY_TRACE_SCRIPT"))
			fprintf(stderr, "SCRIPT bad word %#x after cmd %#x (ParseScript, type %d)\n", opcode, gTraceLastScriptCmd, this->mType);
		else if (opcode & 0x4000)
			gTraceLastScriptCmd = opcode;
#endif

		if (opcode & 0x4000)
		{
			if (!this->ExecuteCommand(opcode))
				return;
		}
		else if (opcode & 0x2000)
		{
			this->SetVariable(opcode);
		}
	}

	if (this->field_234 && *gSubmarinerDieRelated)
	{
		this->SendDeathPulse();
		this->field_2A8 |= 0x4000;
	}

	this->field_20C = 0;
}

// @Ok
// 0x4075B0, 353 bytes (IDB: .__ct__16CScriptOnlyBaddyFPsi). Trig_CreateObject
// type 203. Same shape as the other baddy constructors: squirt position and
// angles, flags, list, node, then the command script through ParseScript
// (its loop, the death pulse and the field_20C reset are all inline in the
// original, and identical to CBaddy::ParseScript).
CScriptOnlyBaddy::CScriptOnlyBaddy(i16* a2, i32 a3)
{
	i16 *v5 = this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2)));

	this->mFlags |= 1;
	this->mCBodyFlags &= ~0x10;
	this->field_32C = -1;
	this->AttachTo(&G_CONTROL_BADDY_LIST);

	this->mRMinor = 0;
	this->mType = 203;
	this->mNode = static_cast<u16>(a3);
	this->field_20C = 1;

	this->ParseScript(reinterpret_cast<u16*>(v5));
}

// @Ok
// 0x407740 (0x407720 is MSVC's scalar deleting thunk around it; neither is
// named in the IDB, found through CScriptOnlyBaddy's vtable 0x53B2E8 slot 0).
// Unlinks from ControlBaddyList, stops the script's sound, deletes the owned
// object and the compass, then falls into ~CBaddy/~CBody. Without this the
// class had no destructor at all, so Init_KillAll left its list pointing
// at freed memory and Reloc_UnloadAll crashed on the next level restart
// (found 2026-09-03 in the standalone build).
CScriptOnlyBaddy::~CScriptOnlyBaddy(void)
{
	this->DeleteFrom(&G_CONTROL_BADDY_LIST);

	if (this->field_328)
		SFX_Stop(this->field_328);

	if (this->field_324)
		delete this->field_324;

	if (this->field_218 & 0x80)
		Panel_DestroyCompass();
}

// Compass flash timer, same address panel.cpp names gCompassFlashTimer
// (0x60F768, written by Panel_DisplayCompass and this AI).
static i32 * const gCompassFlashTimer = (i32*)0x0060F768;

// @Ok
// 0x407840, 505 bytes (unnamed in the IDB, found through vtable 0x53B2E8
// slot 2). The cutscene actor's whole behaviour: a trimmed CBaddy script
// runner. Per frame: latch mInputFlags into field_214 (bit 0 also bumps
// field_210 and, for a compass owner, restarts the compass flash), then
// while the script is active either count down the wait (field_230 in
// frame units, field_238 in frames) or run script commands until one
// returns 0, and when the list ends fire the death pulse (Venom levels
// only) and go inactive; an inactive actor dies. Afterwards a 0x100 actor
// pokes every CSimby (type 324) within field_32E every 4th frame, and a
// looping sound (field_32C != -1) is re-positioned every field_32C frames.
void CScriptOnlyBaddy::AI(void)
{
	u16 inputFlags = this->mInputFlags;
	this->field_214 = inputFlags;

	if (inputFlags & 1)
	{
		this->mInputFlags = inputFlags & 0xFFFE;
		this->field_210++;

		if (this->field_218 & 0x80)
			*gCompassFlashTimer = 300;
	}

	if (this->field_20C != 0)
	{
		if (this->field_234 != 0 && *gSubmarinerDieRelated != 0)
		{
			this->field_238 = 0;
			this->field_230 = 0;
		}

		i32 wait = this->field_230;

		if (wait != 0)
		{
			i32 step = this->field_80;

			if (wait < step)
				this->field_230 = 0;
			else
				this->field_230 = wait - step;
		}
		else if (this->field_238 != 0)
		{
			this->field_238--;
		}
		else
		{
			while (*this->field_24C != 0x4100)
			{
				i16 opcode = *this->field_24C;
				this->field_24C++;

				print_if_false((opcode & 0x6000) != 0, "Bad script command");
#ifdef SPIDEY_STANDALONE
				if ((opcode & 0x6000) == 0 && getenv("SPIDEY_TRACE_SCRIPT"))
					fprintf(stderr, "SCRIPT bad word %#x after cmd %#x (AI, type %d)\n", (u16)opcode, gTraceLastScriptCmd, this->mType);
				else if (opcode & 0x4000)
					gTraceLastScriptCmd = (u16)opcode;
#endif

				if (opcode & 0x4000)
				{
					if (this->ExecuteCommand(opcode) == 0)
						goto tail;
				}
				else if (opcode & 0x2000)
				{
					this->SetVariable(opcode);
				}
			}

			if (this->field_234 != 0 && *gSubmarinerDieRelated != 0)
			{
				if (this->field_211 == 0 && this->mNode != 0xFFFF)
				{
					this->field_211 = 1;
					Trig_SendPulse(Trig_GetLinksPointer(this->mNode));
				}

				this->field_2A8 |= 0x4000;
			}

			this->field_20C = 0;
		}
	}
	else
	{
		this->Die(0);
	}

tail:
	if ((this->field_218 & 0x100) && (G_TTIME & 3) == 0)
	{
		for (CBaddy *pBaddy = G_BADDY_LIST; pBaddy; pBaddy = reinterpret_cast<CBaddy*>(pBaddy->mNextItem))
		{
			if (pBaddy->mType == 324
				&& Utils_XZDist(&this->mPos, &pBaddy->mPos) < this->field_32E)
			{
				static_cast<CSimby*>(pBaddy)->ExplosionReaction();
			}
		}
	}

	i16 sfxPeriod = this->field_32C;

	if (sfxPeriod != -1)
	{
		if (G_TTIME % sfxPeriod == 0)
			SFX_ModifyPos(this->field_328, &this->mPos, 0);
	}
}

// @Ok
// 0x407A50, 1077 bytes (IDB: CScriptOnlyBaddy_ExecuteCommand). The actor's
// own script commands; everything else goes to CBaddy::ExecuteCommand.
// Operands are read from field_24C as 16 bit words. Sound handles live in
// field_328, field_32C is the re-position period for the looping sound,
// field_324 the owned smoke jet, field_32E the CSimby poke radius.
int CScriptOnlyBaddy::ExecuteCommand(u16 cmd)
{
	CPlayer *pPlayer = reinterpret_cast<CPlayer*>(G_MECHLIST);

	switch (cmd)
	{
		// SmokeJetOn (two variants) and SmokeJetOff.
		case 0x429B:
		case 0x42A1:
		{
			print_if_false(this->field_324 == 0, "CSmokeJet on command when a smoke jet already exists");

			// @TODO Phase 2: the original does
			//   new(CBit::operator new(0x164)) CSmokeJet(&mPos, &mAngles,
			//       p[0], 0xFFFF, p[1], p[2], p[3], p[4], *(u8*)&p[5], ...)
			// (0x4AF470) and for 0x429B sets the jet's byte at +0x3A to 1.
			// CSmokeJet::CSmokeJet is not decompiled yet (smoke.h is padding
			// only), so the effect is skipped; the six operand words are
			// still consumed so the script stays in sync.
			this->field_324 = 0;
			this->field_24C += 6;
			return 1;
		}

		case 0x429F:
			print_if_false(this->field_324 != 0, "CSmokeJet off command when no smoke jet exists");
			if (this->field_324)
				delete this->field_324;
			this->field_324 = 0;
			return 1;

		// PlaySoundLooped: id | 0x8000 played without a position (0x428D)
		// or at the actor (0x450B, followed by the re-position period).
		case 0x428D:
		case 0x450B:
		{
			if (this->field_328)
				SFX_Stop(this->field_328);

			i16 id = *this->field_24C;
			this->field_24C++;

			if (cmd != 0x428D)
			{
				this->field_328 = SFX_PlayPos(id | 0x8000, &this->mPos, 0);
				this->field_32C = *this->field_24C;
				this->field_24C++;
				return 1;
			}

			this->field_328 = SFX_Play(id | 0x8000, 0x2000, 0);
			this->field_32C = -1;
			return 1;
		}

		case 0x4503:
			print_if_false(0, "No longer supported script command");
			return 1;

		// MoveToPlayer.
		case 0x4506:
			if (G_MECHLIST)
				this->mPos = G_MECHLIST->mPos;
			return 1;

		// PlaySound (no position).
		case 0x4507:
		{
			if (this->field_328)
				SFX_Stop(this->field_328);

			u16 id = *reinterpret_cast<u16*>(this->field_24C);
			this->field_24C++;

			this->field_328 = SFX_Play(id, 0x2000, 0);
			this->field_32C = -1;
			return 1;
		}

		// PlaySoundPos: id, then the re-position period.
		case 0x4508:
		{
			if (this->field_328)
				SFX_Stop(this->field_328);

			u16 id = *reinterpret_cast<u16*>(this->field_24C);
			this->field_24C++;
			u16 period = *reinterpret_cast<u16*>(this->field_24C);
			this->field_24C++;

			this->field_32C = period;
			this->field_328 = SFX_PlayPos(id, &this->mPos, 0);
			return 1;
		}

		// StopSound.
		case 0x4509:
			if (this->field_328)
				SFX_Stop(this->field_328);
			this->field_328 = 0;
			return 1;

		// CreateCompass at the actor.
		case 0x450C:
			Panel_CreateCompass(&this->mPos);
			this->field_218 |= 0x80;
			return 1;

		// SimbyPokeRadius: enables the CSimby ExplosionReaction sweep in AI.
		case 0x4513:
			this->field_218 |= 0x100;
			this->field_32E = *this->field_24C;
			this->field_24C++;
			return 1;

		// PlayerIgnoreInput(frames).
		case 0x470D:
		{
			u16 frames = *reinterpret_cast<u16*>(this->field_24C);
			this->field_24C++;
			pPlayer->SetIgnoreInputTimer(frames);
			return 1;
		}

		// PlayerCutSceneScript: the rest of the stream drives the player,
		// the actor's own cursor jumps past it.
		case 0x470E:
			this->field_24C = pPlayer->SwitchToSynthesizedInput(this->field_24C);
			return 1;

		default:
			return CBaddy::ExecuteCommand(cmd);
	}
}

// @Ok
// 0x407A40 (IDB: CScriptOnlyBaddy::SetVariable): plain forward to the
// CBaddy version.
void CScriptOnlyBaddy::SetVariable(u16 a2)
{
	CBaddy::SetVariable(a2);
}

// How many CBaddy objects are alive. The exe owns it at 0x0056E98C, right in
// front of BaddyList (0x0056E990) and ControlBaddyList (0x0056E994), which
// already have their macros in baddy.h.
//
// Address read out of the disassembly, and both writers agree. CBaddy::CBaddy
// (0x402C00) ends with "mov al,[56E98Ch]" at 0x402CE6 for field_21D, then
// "mov ecx,[56E98Ch]; inc ecx; mov [56E98Ch],ecx" at 0x402CF1..0x402CFD.
// CBaddy::~CBaddy (0x402D60) reads the same address at 0x402D84, hands the
// "Negative NumBaddies" string (0x547B20) to print_if_false, then decrements
// it with "mov [56E98Ch],ecx" at 0x402DAB.
//
// It has to point at game memory. Every enemy constructor in the exe runs
// CBaddy::CBaddy and bumps the exe's counter, and CBaddy::CBaddy is not hooked,
// so a repo-local copy would count only the baddies our own hooked code makes.
// field_21D (the per-baddy index) would then collide across objects.
//
// Only this file touches it today, so the macro is file-local. Move it to
// baddy.h if another .cpp ever needs it.
#ifndef SPIDEY_STANDALONE
i32 NumBaddies;
#else
extern i32 NumBaddies;
#endif
//#define G_NUM_BADDIES (NumBaddies)
#define G_NUM_BADDIES (*reinterpret_cast<i32*>(0x0056E98C))

// @Ok
// Checked against the original disasm at 0x402c00: every field init here
// (field_1A8 array, field_240/27C/2B8/2C4/2C8/2CC/2D0/2DC/2DE/2E0/2E2/2E4
// /2E6/2E8/2FC, field_21D=NumBaddies++, mCBodyFlags|=0x200, mRMinor=128,
// field_F4=128, mNode=-1, field_216=32, mPushVal=64) matches the
// original line for line. The base CSuper constructor call is implicit
// (C++ base init) and matches the original's first call. Remaining
// residue: the original compiles the six field_1A8[i] zero-inits as
// one tight loop (single decrementing counter) while our source (index 0
// by itself, then a for over 1..5) compiles to unrolled stores, so the
// built function is noticeably longer (444 vs 309 bytes) despite writing
// the exact same fields. Not a functional bug, just byte-matching
// residue for a future pass.
CBaddy::CBaddy(void)
{
	this->field_1A8[0].vx = 0;
	this->field_1A8[0].vy = 0;
	this->field_1A8[0].vz = 0;

	for(int i=0; i<5; i++)
	{
		this->field_1A8[1+i].vx = 0;
		this->field_1A8[1+i].vy = 0;
		this->field_1A8[1+i].vz = 0;
	}

	this->field_240.vx = 0;
	this->field_240.vy = 0;
	this->field_240.vz = 0;
	this->field_27C.vx = 0;
	this->field_27C.vy = 0;
	this->field_27C.vz = 0;
	this->field_2B8.vx = 0;
	this->field_2B8.vy = 0;
	this->field_2B8.vz = 0;
	this->field_2C4.vx = 0;
	this->field_2C4.vy = 0;
	this->field_2C4.vz = 0;
	this->field_2D0.vx = 0;
	this->field_2D0.vy = 0;
	this->field_2D0.vz = 0;
	this->field_2DC.vx = 0;
	this->field_2DC.vy = 0;
	this->field_2DC.vz = 0;
	this->field_2E2.vx = 0;
	this->field_2E2.vy = 0;
	this->field_2E2.vz = 0;
	this->field_2E8.vx = 0;
	this->field_2E8.vy = 0;
	this->field_2E8.vz = 0;
	this->field_2FC.vx = 0;
	this->field_2FC.vy = 0;
	this->field_2FC.vz = 0;

	this->field_21D = G_NUM_BADDIES++;
	this->mCBodyFlags |= 0x200;

	this->mRMinor = 128;
	this->field_F4 = 128;
	this->mNode = -1;
	this->field_216 = 32;
	this->mPushVal = 64;
}

// @Ok
void CBaddy::CreateCombatImpactEffect(CVector*, int)
{
}

// @Ok
u8 CBaddy::TugImpulse(CVector*, CVector*, CVector* a4)
{
	if (a4)
		Mem_Delete(a4);
	return 0;
}

// @Ok
void CBaddy::Victorious()
{
}

// @Ok
void CBaddy::SetParamByIndex(i32, i32)
{
}

// @Ok
u8 CBaddy::Grab(CVector*)
{
	return 0;
}

// @Ok
void CBaddy::Shouldnt_DoPhysics_Be_Virtual(void)
{
	this->DoPhysics(0);
}

// @Ok
// Vtable slot 12 (verified via ??_7CBaddy@@6B@ + 12*4 = 0x407f60). The
// original function body is a single "retn 4" (IDA's nullsub_14), no
// side effects at all, so the empty stub here is already correct. Name
// is still unknown (not in idb_globals.txt or spideypc_names.txt as a
// real member function name), left as a placeholder.
void CBaddy::UnknownCBaddyFunctionFive(int)
{
}

// @Ok
CBaddy* CBaddy::GetClosest(i32 baddyType, i32 inSight)
{
	i32 distance = 10656;
	CBaddy* result = 0;

	for ( CBaddy* i = G_BADDY_LIST; i; i = reinterpret_cast<CBaddy*>(i->mNextItem))
	{
		if ( (!baddyType || i->mType == baddyType) && i != this )
		{
			i32 v9 = Utils_CrapXZDist(this->mPos, i->mPos);
			if ( distance > v9
				&& (!inSight || Utils_LineOfSight(&i->mPos, &this->mPos, 0, 0)) )
			{
				distance = v9;
				result = i;
			}
		}
	}

	return result;
}

// ---- CBaddy::ExecuteCommand support ----
//
// Full opcode map worked out 2026-08-31 from a clean Hex-Rays decompile of
// 0x4050B0 (1855 instructions, 351 basic blocks, confirmed via func_profile;
// an earlier session's byte-count/case-count estimate in this comment's
// prior revision was rougher, this one is read off the actual switch).
// ParseScript (@Ok, above) already establishes the calling convention:
// field_24C has been advanced PAST the opcode word itself before
// ExecuteCommand is called, so every case here only reads its OPERANDS.
// Two new CBaddy fields were added for this (baddy.h): labelArr[8] (fills
// what used to be an unexplained 32-byte PADDING block exactly) and the
// bytecode-pointer/GetVariable-redirect idiom used by C_ADD/C_SUB.
//
// Around 35 case bodies call leaf helpers with no repo declaration and no
// tools/names.json entry (particle/model spawners, camera, sound, a
// stateful particle-system setup sequence, and a handful of others) spread
// across subsystems well outside baddy.cpp. The strict leaf-first/
// byte-match discipline would block on decompiling all of those first (a
// multi-file undertaking); this session's bar is functional correctness,
// so each of those is a real, addressed @FIXME forward-to-original call
// instead (same pattern as Decomp_GetAnimTransform in decomp.cpp) --
// argument count, order and type for each was read directly off this
// function's own call sites, so the MARSHALING is not a guess even where
// the callee's internal behavior is opaque. Never hooked, so forwarding
// them has no runtime-safety concern (see the G_* macro notes elsewhere in
// this repo -- that caution is about shared mutable globals between hooked
// and unhooked code, not applicable here since nothing here is hooked).
//
// Opcode value -> meaning (tentative names where noted uncertain), and
// whether it's implemented with real logic or forwarded:
//   0x4101 C_GOTO                    unconditional jump to labelArr[op]
//   0x4102 C_GOTO_2 (uncertain name) unconditional jump + gated bool return
//   0x4103, 0x4108-0x410F            unhandled (no-op, matches the original's
//                                    own default case for these values)
//   0x4104 C_SET_SCRIPT_LABEL        labelArr[op] = current field_24C
//   0x4105 C_DEFINE_LABELS           reads a run of (opcode,value) pairs up
//                                    to 0x4100, applying any embedded
//                                    C_SET_SCRIPT_LABEL along the way
//   0x4106 (uncertain name/purpose)  mechanically faithful: resolves one
//                                    operand, forwards to sub_4E3940, and
//                                    (as the original does) stores the
//                                    result+6 into field_24C -- kept as
//                                    written even though the "why" is not
//                                    understood, see comment on the case
//   0x4107 C_YIELD                   return false (stop this tick)
//   0x4110/0x4111 C_ADD / C_SUB      add/subtract into a variable via
//                                    GetVariable/SetVariable, relaying any
//                                    bytes GetVariable itself consumed
//   0x4112 C_IF_GREATER, 0x4113 C_IF_LESS, 0x4114 C_IF_EQUAL,
//   0x4115 C_IF_FLAG_SET, 0x4116 C_IF_FLAG_CLEAR, 0x4117 C_IF_WHATIF,
//   0x4119 C_IF_SYMBIOTE (uncertain name)
//                                    all: if condition holds, continue
//                                    (return true); else skip forward to
//                                    the matching 0x4120, respecting nested
//                                    0x4112-0x4119 blocks (shared helper
//                                    CBaddy_SkipToMatchingEndif)
//   0x4118 C_IF_MECH_IN_RANGE (uncertain name) -- same shape as the above,
//                                    but the condition itself calls
//                                    sub_4C9180(MechList, x, z), a thiscall
//                                    function forwarded via a member-
//                                    function-pointer cast (same trick as
//                                    a plain @FIXME forward, just needed
//                                    here because this ONE callee is
//                                    thiscall not cdecl -- confirmed from
//                                    Hex-Rays showing "this:" only for
//                                    this call, none of the others)
//   0x4120 C_ENDIF                   block terminator, no-op when reached
//                                    directly (return true)
//   0x4200 C_SET_NAME                InitItem() on a NUL-terminated string
//                                    embedded in the bytecode, then skips
//                                    past it (odd/even alignment exactly as
//                                    coded, not "fixed" to look sensible)
//   0x4201, 0x4205                   forward (sub_460D00, sub_404320)
//   0x4202                           this->RunAnim(op, 0, -1) -- confirmed
//                                    real method, same call shape already
//                                    used by CMysterio::CMysterio
//   0x4203/0x4204 C_SET/CLEAR_FLAG1  this->mFlags &= ~1 / |= 1
//   0x4226 C_ZERO_VELOCITY           this->mVel = {0,0,0}
//   0x4227 (uncertain name)          field_212/field_213/field_2F0 pokes,
//                                    mechanically faithful
//   0x4240                           field_20C = 0; return false (yield)
//   0x4280 (uncertain name/purpose)  the largest, least understood case:
//                                    snapshots/restores several CBaddy
//                                    fields around a loop that calls
//                                    Shouldnt_DoPhysics_Be_Virtual() and 5
//                                    forwarded helpers (sub_4E7A40/7900/
//                                    7760/7800/79F0/7AE0) building CVector
//                                    pieces. Implemented mechanically
//                                    (every read/write/call in the
//                                    original, in order) since the operand
//                                    count matters for bytecode sync, but
//                                    the actual PURPOSE was not recovered
//   0x4281 C_WAIT_FOR_CONDITION      rewinds 2 bytes and yields (retries
//                                    next tick) until the mType==203 gate
//                                    or field_214 bit 0 is set
//   0x428E/0x4290, 0x428F/0x4291     forward (sub_471C50, sub_471EA0),
//                                    gated the same way as 0x4281
//   0x4292                           particle-system setup loop, forwards
//                                    sub_40F3D0/3F0/410/440/460/480,
//                                    guarded by TotalBitUsage<200 (already
//                                    an established repo global, bit.h)
//   0x4293, 0x4296/0x4297            skip a dword/word operand (debug-only
//                                    in the original: calls nullsub_3,
//                                    confirmed a true no-op via disasm)
//   0x4294                           forwards sub_4E3880/sub_4E3940 in a
//                                    loop; nullsub_3 confirmed no-op
//   0x4295                           forward (sub_470950, no args)
//   0x4298                           forward (sub_416880), gated on
//                                    CameraList (already established)
//   0x4299                           forward (sub_4708B0)
//   0x429A/0x42A6-0x42A8 (uncertain name, "expgrnd" particle effect)
//                                    reads a sub-opcode then forwards to
//                                    sub_43C250/sub_43CEA0/sub_43B410;
//                                    0x42A6-0x42A8 always take the
//                                    this->mPos branch (the original's own
//                                    `a2==17050` guard can never be true
//                                    there -- dead code kept as written)
//   0x429C                           forward (sub_43D830), reads a packed
//                                    10-byte operand block
//   0x429D                           no-op (2x nullsub_3, confirmed no-op)
//   0x429E (uncertain global name)   gBaddyScriptPosY = this->mPos.vy
//   0x42A0                           forward (sub_46BD80, sub_46B450)
//   0x42A2                           gInitBaddyRelated = one u16 operand
//   0x42A3/0x450A C_PLAY_FX (uncertain name) forward (sub_479EE0/479D30)
//   0x42B0                           skip an embedded string (shares the
//                                    alignment logic with 0x4200)
//   0x42B1/0x42B2                    forward (sub_4E3880 then
//                                    sub_4DFFE0/sub_4DFD30)
//   0x42B3/0x42B4                    forward (sub_4DFFB0 x3 against
//                                    ControlBaddyList/BaddyList/
//                                    EnvironmentalObjectList, or
//                                    sub_4DFC20)
//   0x42B5                           forward (sub_4273D0)
//   0x42B6 C_SET_TARGET_FRAME (uncertain name) field_230 = resolved
//                                    operand, gated like 0x4281
//   0x42B7                           field_234 = 1 (scene skip flag)
//   0x42B8                           walks a checksum list via sub_4C9230,
//                                    pokes byte 28/30 of each hit (a
//                                    CItem-derived object whose real type
//                                    is not established here -- offsets
//                                    kept raw with a comment, not named)
//   0x42B9/0x42BA                    gWideScreen / a shadow copy of it =
//                                    one u16 operand; forward (sub_4273D0
//                                    already covers 0x42B5, this is a
//                                    plain field write)
//   0x430A                           forward (sub_43B740), reads a
//                                    dword-aligned operand
//   0x430B/0x430C/0x430D             walk BaddyList for mType==314 and
//                                    call an unnamed vtable slot on it
//                                    (raw vtable offset, not resolvable to
//                                    a declared virtual -- see the case),
//                                    or forward (sub_438EE0/438E20) on
//                                    MechList
//   0x450D C_CAMERA_TRAJECTORY (uncertain name, "BossCamStationaryRadius")
//                                    forward (sub_4E6150, sub_470430),
//                                    gated on MechList != 0
//   0x450E                           forward (sub_416880) via CameraList
//   0x450F/0x4511/0x4512             walk BaddyList for mNode==k, poke
//                                    mFlags/two u16 fields (offsets kept
//                                    raw, not named -- see the case)
//   0x4601/0x4602/0x4603             field_218 |= 0x100000/0x200000/
//                                    0x400000 (0x4601 also sets mFlags|=1)
//   0x4702-0x4709, 0x470F            field_2F0 |= single bit each
//                                    (0x4703=1,0x4704/0x4706=8,0x4705=0x10,
//                                    0x4708=0x20,0x4709=0x40); 0x470F sets
//                                    field_2A8 |= 0x2000000 instead
//   0x4707                           skips 2 embedded bytes (LABEL_325,
//                                    shared with 0x4200/0x42B0's tail)
//   0x470B                           field_1A4 = one u16 operand
//   0x470C                           this->SetParamByIndex(op1, op2) --
//                                    confirmed real virtual, vtable+36 ==
//                                    slot 9 matches the header exactly
//   default (any other value)        no-op, matches the original's own
//                                    unhandled-opcode fallthrough
//
// Globals used here that are new to the repo: gTrigNodes (0x6B466C, real
// IDB name), gWideScreen (0x660F80, real IDB name), CurrentSuit (0x5559DC,
// real IDB name, already referenced by name in shell.cpp comments),
// gInitBaddyRelated (0x5FCDA4, real IDB name). gSubmarinerDieRelated
// (0x60CFC4) already exists above in this file, reused as-is. Two globals
// have no IDB entry, tentative names below with their evidence.
static i32 * const gTrigNodes = reinterpret_cast<i32*>(0x6B466C);
static i32 * const gWideScreen = reinterpret_cast<i32*>(0x660F80);
static i32 * const CurrentSuit = reinterpret_cast<i32*>(0x5559DC);
static i32 * const gInitBaddyRelated = reinterpret_cast<i32*>(0x5FCDA4);

// tentative: written immediately after gWideScreen with the exact same
// value in case 0x42BA below, no IDB entry. Looks like a shadow/duplicate
// copy, not independently read anywhere in this function.
static i32 * const gWideScreenShadow = reinterpret_cast<i32*>(0x60F76C);

// tentative: stores this->mPos.vy in case 0x429E below, no IDB entry, no
// other reader/writer found in this function.
static i32 * const gBaddyScriptPosY = reinterpret_cast<i32*>(0x5FCDA8);

// tentative: tested alongside CurrentSuit==4 in the 0x4119 IF-condition, no
// IDB entry.
static i32 * const gSymbioteRelated = reinterpret_cast<i32*>(0x60CFC8);

// resolves one u16 bytecode operand: raw read, advance field_24C, then (the
// idiom repeated at nearly every operand read in the original) redirect
// through GetVariable() when bit 0x2000 is set.
// @Bogus
static u16 CBaddy_ResolveOperand(CBaddy *self)
{
	u16 val = static_cast<u16>(*self->field_24C);
	self->field_24C++;

	if (val & 0x2000)
		val = static_cast<u16>(self->GetVariable(val));

	return val;
}

// raw operand read, no GetVariable redirect (used where the operand is
// itself an index/count, e.g. label indices, not a value expression).
// @Bogus
static u16 CBaddy_ReadOperand(CBaddy *self)
{
	u16 val = static_cast<u16>(*self->field_24C);
	self->field_24C++;
	return val;
}

// shared tail of every C_IF_* opcode's false branch: skip forward past the
// current conditional block to its matching 0x4120 (C_ENDIF), keeping
// count of nested 0x4112-0x4119 blocks skipped along the way. A bare
// 0x4100 hit while skipping is treated as malformed script (falls through
// to the same debug no-op the original's default case uses).
// @Bogus
static void CBaddy_SkipToMatchingEndif(CBaddy *self)
{
	i32 depth = 0;

	for (;;)
	{
		u16 op;

		for (;;)
		{
			op = CBaddy_ReadOperand(self);

			if (op == 0x4120)
				break;

			if (op >= 0x4112 && op <= 0x4119)
				depth++;

			if (op == 0x4100)
				return;
		}

		if (depth != 0)
		{
			depth--;
			continue;
		}

		return;
	}
}

// @FIXME forward-to-original helpers for ExecuteCommand's ~35 leaf callees
// with no repo declaration (see the big comment above). Argument count,
// order and type read off this function's own call sites in the Hex-Rays
// decompile, not guessed independently.
//
// Standalone build (SPIDEY_STANDALONE): none of the original exe is in
// memory, so every helper below calls the repo implementation instead. The
// address -> function mapping comes from the maintainer's IDB (mangled
// names) plus the Mac symbol list for the two unnamed effects helpers. The
// non-standalone branches are untouched.
//
// Six of the targets are __thiscall methods or constructors and the
// helper has no object to call them on (the original passes it in ecx or
// gets it from operator new). Those helpers only exist in the normal build
// and their ExecuteCommand call sites carry their own SPIDEY_STANDALONE
// branch calling the method on the right object: 0x460D00 CSuper::CycleAnim
// (this), 0x404320 CBaddy::Die (this), 0x40F480 CSpark::CSpark, 0x43CEA0
// CWibbling3DExplosion::CWibbling3DExplosion, 0x43C250
// CFlameExplosion::CFlameExplosion (all three via new).
//
// Not forwarded at all (both builds call the repo code): the CVector /
// CSVector operators case 0x4280 uses (0x4E7A40, 0x4E7900, 0x4E7760,
// 0x4E7800, 0x4E79F0, 0x4E7AE0 are all @Ok in vector.cpp and declared
// out of line in vector.h, so the calls come out the same) and 0x46B450
// CPowerUp::SetGravity (@Ok in powerup.cpp, called on the CPowerUp
// PowerUp_Create just returned, case 0x42A0).
// @Bogus
static i32 gsub_4E3940(i32 *outBuf, u16 nodeId)
{
#ifdef SPIDEY_STANDALONE
	// Trig_GetPosition(CVector*, i32) -> u16*, callers add 6 to skip the position
	return reinterpret_cast<i32>(Trig_GetPosition(reinterpret_cast<CVector*>(outBuf), nodeId));
#else
	typedef i32 (*func_ptr)(i32*, u16);
	func_ptr func = (func_ptr)0x4E3940;
	return func(outBuf, nodeId);
#endif
}

#ifndef SPIDEY_STANDALONE
// 0x460D00 = CSuper::CycleAnim(i32, i8), __thiscall on `this` (ecx = esi =
// the CBaddy at 0x4057F8). Standalone: see case 0x4201.
// @Bogus
static void gsub_460D00(u16 a2, u16 a3)
{
	typedef void (*func_ptr)(u16, u16);
	func_ptr func = (func_ptr)0x460D00;
	func(a2, a3);
}

// 0x404320 = CBaddy::Die(int), __thiscall on `this` (0x405839 mov ecx,esi).
// Standalone: see case 0x4205.
// @Bogus
static void gsub_404320(i32 a2)
{
	typedef void (*func_ptr)(i32);
	func_ptr func = (func_ptr)0x404320;
	func(a2);
}
#endif

// @Bogus
static i32 gsub_4DE770(void)
{
#ifdef SPIDEY_STANDALONE
	return Trig_GetLevelID();
#else
	typedef i32 (*func_ptr)(void);
	func_ptr func = (func_ptr)0x4DE770;
	return func();
#endif
}

// @Bogus
static void gsub_471C50(i32 a2, i32 a3, i32 a4)
{
#ifdef SPIDEY_STANDALONE
	SFX_Play(a2, static_cast<i16>(a3), a4);
#else
	typedef void (*func_ptr)(i32, i32, i32);
	func_ptr func = (func_ptr)0x471C50;
	func(a2, a3, a4);
#endif
}

// @Bogus
static void gsub_471EA0(i32 a2, CVector *a3, i32 a4)
{
#ifdef SPIDEY_STANDALONE
	SFX_PlayPos(a2, a3, a4);
#else
	typedef void (*func_ptr)(i32, CVector*, i32);
	func_ptr func = (func_ptr)0x471EA0;
	func(a2, a3, a4);
#endif
}

// @Bogus
static void gsub_40F3D0(void *a2)
{
#ifdef SPIDEY_STANDALONE
	Bit_SetSparkTrajectory(static_cast<const CSVector*>(a2));
#else
	typedef void (*func_ptr)(void*);
	func_ptr func = (func_ptr)0x40F3D0;
	func(a2);
#endif
}

// @Bogus
static void gsub_40F3F0(i16 *a2)
{
#ifdef SPIDEY_STANDALONE
	// the caller's i16[3] is the cone CSVector
	Bit_SetSparkTrajectoryCone(reinterpret_cast<const CSVector*>(a2));
#else
	typedef void (*func_ptr)(i16*);
	func_ptr func = (func_ptr)0x40F3F0;
	func(a2);
#endif
}

// @Bogus
static void gsub_40F410(i32 a2)
{
#ifdef SPIDEY_STANDALONE
	Bit_SetSparkSize(a2);
#else
	typedef void (*func_ptr)(i32);
	func_ptr func = (func_ptr)0x40F410;
	func(a2);
#endif
}

// @Bogus
static void gsub_40F440(i32 a2, i32 a3, i32 a4)
{
#ifdef SPIDEY_STANDALONE
	Bit_SetSparkRGB(static_cast<u8>(a2), static_cast<u8>(a3), static_cast<u8>(a4));
#else
	typedef void (*func_ptr)(i32, i32, i32);
	func_ptr func = (func_ptr)0x40F440;
	func(a2, a3, a4);
#endif
}

// @Bogus
static void gsub_40F460(i32 a2, i32 a3, i32 a4)
{
#ifdef SPIDEY_STANDALONE
	Bit_SetSparkFadeRGB(static_cast<u8>(a2), static_cast<u8>(a3), static_cast<u8>(a4));
#else
	typedef void (*func_ptr)(i32, i32, i32);
	func_ptr func = (func_ptr)0x40F460;
	func(a2, a3, a4);
#endif
}

#ifndef SPIDEY_STANDALONE
// 0x40F480 = CSpark::CSpark(CVector&, i32, i32, i32), a constructor run on
// the block CBit::operator new(76) just returned. Standalone: see case 0x4292.
// @Bogus
static void gsub_40F480(CVector *a2, i32 a3, i32 a4, i32 a5)
{
	typedef void (*func_ptr)(CVector*, i32, i32, i32);
	func_ptr func = (func_ptr)0x40F480;
	func(a2, a3, a4, a5);
}

// 0x43CEA0 = CWibbling3DExplosion::CWibbling3DExplosion, a constructor run
// on the block CClass::operator new(276) just returned. Standalone: see
// case 0x429A sub-op 3.
// @Bogus
static void gsub_43CEA0(i32 *a2, const char *a3, i32 a4, i32 a5, i32 a6, i32 a7, i32 a8, i32 a9, i32 a10, i16 a11, i16 a12)
{
	typedef void (*func_ptr)(i32*, const char*, i32, i32, i32, i32, i32, i32, i32, i16, i16);
	func_ptr func = (func_ptr)0x43CEA0;
	func(a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12);
}
#endif

// @Bogus
static void gsub_43D830(i32 a2, i32 a3, i32 a4, i32 a5, i32 a6, i32 a7)
{
#ifdef SPIDEY_STANDALONE
	Flash_Screen(static_cast<u8>(a2), static_cast<u8>(a3), static_cast<u8>(a4), a5, static_cast<u8>(a6), a7);
#else
	typedef void (*func_ptr)(i32, i32, i32, i32, i32, i32);
	func_ptr func = (func_ptr)0x43D830;
	func(a2, a3, a4, a5, a6, a7);
#endif
}

// 0x46BD80 = PowerUp_Create(type, CVector *pos, flags, param1, param2)
// -> CPowerUp*. The position argument is a CVector* (0x4058E3 push esi with
// esi = this+8, i.e. &this->mPos); an earlier revision passed the CBaddy.
// @Bogus
static CPowerUp *gsub_46BD80(i32 a2, CVector *a3, u16 a4, u16 a5, i32 a6)
{
#ifdef SPIDEY_STANDALONE
	return reinterpret_cast<CPowerUp*>(PowerUp_Create(a2, a3, a4, a5, a6));
#else
	typedef CPowerUp* (*func_ptr)(i32, CVector*, u16, u16, i32);
	func_ptr func = (func_ptr)0x46BD80;
	return func(a2, a3, a4, a5, a6);
#endif
}

// 0x43B410 = Effects_LaserFlash. The first two operands are zero-extended
// (0x40596D/0x4059BD and eax/edx,0FFFFh), the other four are plain 16-bit
// register loads.
// @Bogus
static void gsub_43B410(i32 *a2, u16 a3, u16 a4, i16 a5, i16 a6, i16 a7, i16 a8)
{
#ifdef SPIDEY_STANDALONE
	// Effects_LaserFlash (effects.cpp), the original passes a5..a8 on to
	// u8 CGlowFlash colour params
	Effects_LaserFlash(reinterpret_cast<CVector*>(a2), a3, a4,
			static_cast<u8>(a5), static_cast<u8>(a6), static_cast<u8>(a7), static_cast<u8>(a8));
#else
	typedef void (*func_ptr)(i32*, u16, u16, i16, i16, i16, i16);
	func_ptr func = (func_ptr)0x43B410;
	func(a2, a3, a4, a5, a6, a7, a8);
#endif
}

// @Bogus
static void *gsub_4E3880(u16 a2)
{
#ifdef SPIDEY_STANDALONE
	return Trig_GetLinksPointer(a2);
#else
	typedef void* (*func_ptr)(u16);
	func_ptr func = (func_ptr)0x4E3880;
	return func(a2);
#endif
}

// @Bogus
static void gsub_470950(void)
{
#ifdef SPIDEY_STANDALONE
	GameFMV_StopFMV();
#else
	typedef void (*func_ptr)(void);
	func_ptr func = (func_ptr)0x470950;
	func();
#endif
}

// @Bogus
static void gsub_416880(CVector *a2, i32 a3)
{
#ifdef SPIDEY_STANDALONE
	// CCamera::Shake(CVector&, EShakeType), __thiscall on CameraList
	// (0x405D19 mov ecx,dword_56F3B8); the caller already null-checks it.
	G_CAMERA_LIST->Shake(*a2, static_cast<EShakeType>(a3));
#else
	typedef void (*func_ptr)(CVector*, i32);
	func_ptr func = (func_ptr)0x416880;
	func(a2, a3);
#endif
}

// @Bogus
static void gsub_4708B0(i32 a2)
{
#ifdef SPIDEY_STANDALONE
	GameFMV_SetStartTrack(static_cast<u8>(a2));
#else
	typedef void (*func_ptr)(i32);
	func_ptr func = (func_ptr)0x4708B0;
	func(a2);
#endif
}

#ifndef SPIDEY_STANDALONE
// 0x43C250 = CFlameExplosion::CFlameExplosion(const CVector*, i32, i32, i32),
// a constructor run on the block CBit::operator new(144) just returned.
// Standalone: see case 0x429A sub-op 2.
// @Bogus
static void gsub_43C250(i32 *a2, i16 a3, i32 a4, i16 a5)
{
	typedef void (*func_ptr)(i32*, i16, i32, i16);
	func_ptr func = (func_ptr)0x43C250;
	func(a2, a3, a4, a5);
}
#endif

// @Bogus
static void gsub_4DFFE0(void *a2)
{
#ifdef SPIDEY_STANDALONE
	Trig_SendSignalToLinks(static_cast<u16*>(a2));
#else
	typedef void (*func_ptr)(void*);
	func_ptr func = (func_ptr)0x4DFFE0;
	func(a2);
#endif
}

// @Bogus
static void gsub_4DFD30(void *a2)
{
#ifdef SPIDEY_STANDALONE
	Trig_SendPulse(static_cast<u16*>(a2));
#else
	typedef void (*func_ptr)(void*);
	func_ptr func = (func_ptr)0x4DFD30;
	func(a2);
#endif
}

// @Bogus
static void gsub_4DFFB0(void *a2, u16 a3)
{
#ifdef SPIDEY_STANDALONE
	// SendSignalToNode(CBody* listHead, i32 node); the callers pass the
	// three object list heads
	SendSignalToNode(static_cast<CBody*>(a2), a3);
#else
	typedef void (*func_ptr)(void*, u16);
	func_ptr func = (func_ptr)0x4DFFB0;
	func(a2, a3);
#endif
}

// @Bogus
static void gsub_4DFC20(u16 a2)
{
#ifdef SPIDEY_STANDALONE
	Trig_SendPulseToNode(a2);
#else
	typedef void (*func_ptr)(u16);
	func_ptr func = (func_ptr)0x4DFC20;
	func(a2);
#endif
}

// @Bogus
static void gsub_438EE0(void *a2)
{
#ifdef SPIDEY_STANDALONE
	Effects_UnElectrify(static_cast<CSuper*>(a2));
#else
	typedef void (*func_ptr)(void*);
	func_ptr func = (func_ptr)0x438EE0;
	func(a2);
#endif
}

// @Bogus
static void gsub_438E20(void *a2)
{
#ifdef SPIDEY_STANDALONE
	Effects_Electrify(static_cast<CSuper*>(a2));
#else
	typedef void (*func_ptr)(void*);
	func_ptr func = (func_ptr)0x438E20;
	func(a2);
#endif
}

// @Bogus
static void gsub_43B740(i32 *a2, i32 a3)
{
#ifdef SPIDEY_STANDALONE
	Effects_MakeRocks(reinterpret_cast<CVector*>(a2), a3);
#else
	typedef void (*func_ptr)(i32*, i32);
	func_ptr func = (func_ptr)0x43B740;
	func(a2, a3);
#endif
}

// @Bogus
static i32 gsub_4C9230(i32 a2)
{
#ifdef SPIDEY_STANDALONE
	return reinterpret_cast<i32>(Spool_FindEnviroItem(a2));
#else
	typedef i32 (*func_ptr)(i32);
	func_ptr func = (func_ptr)0x4C9230;
	return func(a2);
#endif
}

// @Bogus
static i32 gsub_4E6150(CVector *a2, i32 *a3)
{
#ifdef SPIDEY_STANDALONE
	return Utils_Dist(*a2, *reinterpret_cast<const CVector*>(a3));
#else
	typedef i32 (*func_ptr)(CVector*, i32*);
	func_ptr func = (func_ptr)0x4E6150;
	return func(a2, a3);
#endif
}

// @Bogus
static void gsub_470430(i32 *a2, i32 *a3)
{
#ifdef SPIDEY_STANDALONE
	VectorNormal(reinterpret_cast<VECTOR*>(a2), reinterpret_cast<VECTOR*>(a3));
#else
	typedef void (*func_ptr)(i32*, i32*);
	func_ptr func = (func_ptr)0x470430;
	func(a2, a3);
#endif
}

// @Bogus
static void gsub_479EE0(i16 a2, i16 a3, i16 a4)
{
#ifdef SPIDEY_STANDALONE
	Redbook_XAPlay(a2, a3, a4);
#else
	typedef void (*func_ptr)(i16, i16, i16);
	func_ptr func = (func_ptr)0x479EE0;
	func(a2, a3, a4);
#endif
}

// @Bogus
static void gsub_479D30(i16 a2, i16 a3, CVector *a4, i16 a5)
{
#ifdef SPIDEY_STANDALONE
	Redbook_XAPlayPos(a2, a3, a4, a5);
#else
	typedef void (*func_ptr)(i16, i16, CVector*, i16);
	func_ptr func = (func_ptr)0x479D30;
	func(a2, a3, a4, a5);
#endif
}

// @Bogus
static void gsub_4273D0(i32 a2)
{
#ifdef SPIDEY_STANDALONE
	Chunk_ChunkItemByChecksum(a2);
#else
	typedef void (*func_ptr)(i32);
	func_ptr func = (func_ptr)0x4273D0;
	func(a2);
#endif
}

// sub_4C9180 is the one callee here that is genuinely __thiscall (Hex-Rays
// shows "this:" only for this call, cdecl "a1:" for every other one). This
// build's compiler flags reject the __thiscall keyword directly (error
// C4234, see the SVTableSlot0Deletable comment in spidey.cpp for the same
// issue elsewhere), so the calling convention is captured through an
// actual non-virtual member function pointer instead: MSVC lays out a
// pointer-to-plain-member-function as a single code address using thiscall,
// so this reproduces the real call shape rather than guessing at it.
struct SMechRangeCheckAdapter
{
	i32 Check(i32, i32);
};

// @Bogus
static i32 gsub_4C9180(CPlayer *pMech, i32 x, i32 z)
{
#ifdef SPIDEY_STANDALONE
	// 0x4C9180 = CPlayer::IfPlayerCeilingCheck(i32, i32), on the player
	return pMech->IfPlayerCeilingCheck(x, z);
#else
	typedef i32 (SMechRangeCheckAdapter::*memfn)(i32, i32);
	union { memfn m; void *p; } u;
	u.p = (void*)0x4C9180;
	return (reinterpret_cast<SMechRangeCheckAdapter*>(pMech)->*u.m)(x, z);
#endif
}

// @Ok
// @Note: retagged @Ok under the functional-correctness bar (2026-08-31
// session). Every opcode value the original switch handles is covered here
// (no silently-dropped case), and every operand read/write and call has a
// mechanically faithful translation. The only open questions are the
// PURPOSE of a few opcodes (0x4102, 0x4106, 0x4118, 0x4227, 0x4280,
// 0x429A/0x42A6-0x42A8, 0x42A3/0x450A, 0x42B8, 0x450D) beyond "what the
// bytes do", which does not affect functional correctness. The only
// opcodes not individually named are 0x4103 and 0x4108-0x410F, which the
// original ITSELF routes to the same unhandled-opcode default. See the big
// comment above for the full opcode map.
int CBaddy::ExecuteCommand(u16 cmd)
{
	switch (cmd)
	{
		case 0x4101: // C_GOTO
		{
			u16 idx = CBaddy_ReadOperand(this);
			this->field_24C = this->labelArr[idx];
			return true;
		}

		case 0x4102: // C_GOTO_2 (uncertain name)
		{
			u16 idx = CBaddy_ReadOperand(this);
			bool cond = this->mType == 203;
			this->field_24C = this->labelArr[idx];
			return cond && this->field_234 != 0 && *gSubmarinerDieRelated != 0;
		}

		case 0x4104: // C_SET_SCRIPT_LABEL
		{
			u16 idx = CBaddy_ReadOperand(this);
			print_if_false(idx < 8, "Label exceeds MAX_BADDY_LABELS");
			this->labelArr[idx] = this->field_24C;
			return true;
		}

		case 0x4105: // C_DEFINE_LABELS
		{
			i16 *saved = this->field_24C;

			for (u16 op = CBaddy_ReadOperand(this); op != 0x4100; op = CBaddy_ReadOperand(this))
			{
				if (op == 0x4104)
				{
					u16 idx = CBaddy_ReadOperand(this);
					print_if_false(idx < 8, "Label exceeds MAX_BADDY_LABELS");
					this->labelArr[idx] = this->field_24C;
				}
			}

			this->field_24C = saved;
			return true;
		}

		case 0x4106: // uncertain name/purpose, see the class comment above
		{
			i32 buf[3] = { 0, 0, 0 };
			u16 val = CBaddy_ResolveOperand(this);
			this->field_24C = reinterpret_cast<i16*>(gsub_4E3940(buf, val) + 6);
			return true;
		}

		case 0x4107: // C_YIELD
			return false;

		case 0x4110: // C_ADD
		case 0x4111: // C_SUB
		{
			u16 idx = CBaddy_ReadOperand(this);
			i16 *runStart = this->field_24C;
			i16 old = this->GetVariable(idx);
			i16 *runEnd = this->field_24C;

			i16 relay[32];
			i32 relayCount = 0;

			if (runStart != runEnd)
			{
				for (i16 *p = runStart; p != runEnd; p++)
					relay[relayCount++] = *p;
			}

			u16 rhs = CBaddy_ReadOperand(this);
			if (rhs & 0x2000)
				rhs = static_cast<u16>(this->GetVariable(rhs));

			i16 result = (cmd == 0x4110) ? static_cast<i16>(old + rhs) : static_cast<i16>(old - rhs);
			relay[relayCount] = result;

			i16 *afterRead = this->field_24C;
			this->field_24C = relay;
			this->SetVariable(idx);
			this->field_24C = afterRead;

			return true;
		}

		case 0x4112: // C_IF_GREATER
		{
			u16 a = CBaddy_ResolveOperand(this);
			u16 b = CBaddy_ResolveOperand(this);
			if (static_cast<i16>(a) > static_cast<i16>(b))
				return true;
			CBaddy_SkipToMatchingEndif(this);
			return true;
		}

		case 0x4113: // C_IF_LESS
		{
			u16 a = CBaddy_ResolveOperand(this);
			u16 b = CBaddy_ResolveOperand(this);
			if (static_cast<i16>(a) < static_cast<i16>(b))
				return true;
			CBaddy_SkipToMatchingEndif(this);
			return true;
		}

		case 0x4114: // C_IF_EQUAL
		{
			u16 a = CBaddy_ResolveOperand(this);
			u16 b = CBaddy_ResolveOperand(this);
			if (a == b)
				return true;
			CBaddy_SkipToMatchingEndif(this);
			return true;
		}

		case 0x4115: // C_IF_FLAG_SET
		{
			u16 mask = CBaddy_ReadOperand(this);
			if ((this->field_218 & mask) == mask)
				return true;
			CBaddy_SkipToMatchingEndif(this);
			return true;
		}

		case 0x4116: // C_IF_FLAG_CLEAR
		{
			u16 mask = CBaddy_ReadOperand(this);
			if ((this->field_218 & mask) == 0)
				return true;
			CBaddy_SkipToMatchingEndif(this);
			return true;
		}

		case 0x4117: // C_IF_WHATIF
			if (gWhatIf)
				return true;
			CBaddy_SkipToMatchingEndif(this);
			return true;

		case 0x4118: // C_IF_MECH_IN_RANGE (uncertain name)
		{
			i16 x = static_cast<i16>(CBaddy_ReadOperand(this));
			i16 z = static_cast<i16>(CBaddy_ReadOperand(this));
			if (G_MECHLIST_PLAYER != 0 && gsub_4C9180(G_MECHLIST_PLAYER, x << 12, z << 12))
				return true;
			CBaddy_SkipToMatchingEndif(this);
			return true;
		}

		case 0x4119: // C_IF_SYMBIOTE (uncertain name)
			if (*CurrentSuit == 4 || *gSymbioteRelated != 0)
				return true;
			CBaddy_SkipToMatchingEndif(this);
			return true;

		case 0x4120: // C_ENDIF
			return true;

		case 0x4200: // C_SET_NAME
		{
			this->InitItem(reinterpret_cast<char*>(this->field_24C));

			u8 *p = reinterpret_cast<u8*>(this->field_24C);
			if (*p != 0)
			{
				do { p++; } while (*p != 0);
			}

			if ((reinterpret_cast<i32>(p) & 1) == 0)
				this->field_24C = reinterpret_cast<i16*>(p + 2);
			else
				this->field_24C = reinterpret_cast<i16*>(p + 1);

			return true;
		}

		case 0x4201:
		{
			u16 a2 = CBaddy_ReadOperand(this);
			u16 a3 = CBaddy_ReadOperand(this);
#ifdef SPIDEY_STANDALONE
			// __thiscall on this (0x4057F8 mov ecx,esi)
			this->CycleAnim(a2, static_cast<i8>(a3));
#else
			gsub_460D00(a2, a3);
#endif
			return true;
		}

		case 0x4202:
		{
			u16 a2 = CBaddy_ReadOperand(this);
			this->RunAnim(a2, 0, -1);
			return true;
		}

		case 0x4203:
			this->mFlags &= ~1;
			return true;

		case 0x4204:
			this->mFlags |= 1;
			return true;

		case 0x4205:
#ifdef SPIDEY_STANDALONE
			// CBaddy::Die(int), __thiscall on this (0x405839 mov ecx,esi)
			this->Die(0);
#else
			gsub_404320(0);
#endif
			return true;

		case 0x4226: // C_ZERO_VELOCITY
			this->mVel.vx = 0;
			this->mVel.vy = 0;
			this->mVel.vz = 0;
			return true;

		case 0x4227: // uncertain name, see class comment
		{
			u16 a2 = CBaddy_ReadOperand(this);
			this->field_2F0 |= 4;
			this->field_212 = static_cast<u8>(a2);
			this->field_213 = 0;
			return true;
		}

		case 0x4240:
			this->field_20C = 0;
			return false;

		case 0x4280: // uncertain name/purpose, see class comment
		{
			// 0x405F75..0x406238. Measures how far the body travels over the
			// next field_230 physics steps: remember mPos/mAngles in
			// field_2B8/field_2DC, run Shouldnt_DoPhysics_Be_Virtual()
			// field_230 times with field_80 forced to 2 (accumulating
			// mAngVel*2 into a LOCAL copy of mAngles), then store the
			// per-step average (mPos after - mPos before) / (2*val) in
			// field_2D0 and (angles accumulated - mAngles before) / field_2B4
			// in field_2E8, and put mPos/mAngles back.
			u16 val = CBaddy_ResolveOperand(this);

			if (this->mType == 203 && this->field_234 != 0 && *gSubmarinerDieRelated != 0)
				return true;

			this->field_230 = val;

			// 0x405FDF jne 0x406236: neither level -> return with no store
			if (gsub_4DE770() != 2051 && gsub_4DE770() != 258)
				return false;

			// 0x405FED..0x406015: the body is moving (mVel | mAngVel, offsets
			// 0x60..0x68 and 0x88..0x8C), not attributeArr/field_240
			if (val != 0 && (this->mAngVel.vz | this->mAngVel.vy | this->mVel.vz
					| this->mVel.vy | this->mVel.vx | this->mAngVel.vx) != 0)
			{
				// 0x40601B..0x40603A
				print_if_false((this->field_2B0 | this->field_230 | this->field_2B4) == 0,
						"New timer started before old timer finished!");

				this->field_2B8 = this->mPos;    // 0x40605C, mPos before
				this->field_2DC = this->mAngles; // 0x406078, mAngles before
				CSVector angAcc = this->mAngles; // 0x406086, local copy at [esp+3Ch]
				i32 savedField80 = this->field_80;
				this->field_80 = 2;

				for (i32 i = 0; i < this->field_230; i++)
				{
					// 0x4060CA operator*(CSVector, int) on mAngVel ([esi+88h],
					// not mAcc), 0x4060D7 CSVector::operator+= with
					// ecx = the local copy (0x4060D2 lea ecx,[esp+3Ch])
					i32 two = 2;
					angAcc += this->mAngVel * two;
					this->Shouldnt_DoPhysics_Be_Virtual();
				}

				this->field_80 = savedField80;

				i32 twiceVal = 2 * val;
				this->field_2B0 = 0;             // 0x406109
				this->field_2B4 = twiceVal;      // 0x406119
				this->field_2C4 = this->mPos;    // 0x406125, mPos after
				this->field_2E2 = this->mAngles; // 0x406141, mAngles after

				// 0x406168 operator-(CVector, CVector)(field_2C4, field_2B8),
				// 0x406179 operator/(CVector, int) by 2*val, stored at 0x2D0
				this->field_2D0 = (this->field_2C4 - this->field_2B8) / twiceVal;

				// 0x4061B8 operator-(CSVector, CSVector)(angAcc, field_2DC),
				// 0x4061C6 operator/(CSVector, int) by field_2B4, stored at 0x2E8
				i32 divisor = this->field_2B4;
				this->field_2E8 = (angAcc - this->field_2DC) / divisor;

				this->mPos = this->field_2B8;    // 0x4061E1
				this->mAngles = this->field_2DC; // 0x4061FA

				return false;
			}
			else if (gsub_4DE770() == 258 && this->mType == 402) // 0x406214 cmp word ptr [esi+38h],192h
			{
				this->field_2B0 = 2 * val + 40; // 0x406220
				return false;
			}
			else
			{
				this->field_2B0 = 2 * val; // 0x406230
				return false;
			}
		}

		case 0x4281: // C_WAIT_FOR_CONDITION (uncertain name)
			if ((this->mType == 203 && this->field_234 != 0 && *gSubmarinerDieRelated != 0)
					|| (this->field_214 & 1) != 0)
			{
				return true;
			}
			this->field_24C -= 1;   // 0x406272: add eax,-2 on the byte pointer, one word back to the opcode itself
			return false;

		case 0x428E:
		case 0x4290:
		{
			u16 val = CBaddy_ReadOperand(this);
			if (!(this->mType == 203 && this->field_234 != 0 && *gSubmarinerDieRelated != 0))
			{
				i32 flags = (cmd == 0x428E) ? 0x8000 : 0;
				if (val >= 300 && val <= 309)
					flags |= 0x4000;
				gsub_471C50(flags | val, 0x2000, 0);
			}
			return true;
		}

		case 0x428F:
		case 0x4291:
		{
			u16 val = CBaddy_ReadOperand(this);
			if (!(this->mType == 203 && this->field_234 != 0 && *gSubmarinerDieRelated != 0))
				gsub_471EA0(val | ((cmd == 0x428F) ? 0x8000 : 0), &this->mPos, 0);
			return true;
		}

		case 0x4292:
		{
			u16 count = CBaddy_ReadOperand(this);

			if (G_TOTALBITUSAGE < 200)
			{
				gsub_40F3D0(&this->mAngles);
				i16 params[3] = { 512, 4096, 0 };
				gsub_40F3F0(params);
				gsub_40F410(1);
				gsub_40F440(128, 128, 128);
				gsub_40F460(4, 4, 4);

				G_TOTALBITUSAGE = 0;

				for (i32 i = 0; i < count; i++)
				{
#ifdef SPIDEY_STANDALONE
					// CSpark::CSpark(CVector&, i32, i32, i32) on the freshly
					// allocated bit (sizeof(CSpark) == 76)
					new CSpark(this->mPos, 32, 0x2000, 32);
#else
					void *p = CBit::operator new(76);
					if (p != 0)
						gsub_40F480(&this->mPos, 32, 0x2000, 32);
#endif
				}

				G_TOTALBITUSAGE = 1;
			}

			return true;
		}

		case 0x4293:
		{
			i32 aligned = (reinterpret_cast<i32>(this->field_24C) + 3) & ~3;
			this->field_24C = reinterpret_cast<i16*>(aligned + 4);
			return true;
		}

		case 0x4294:
		{
			void *list = gsub_4E3880(this->mNode);
			u16 *entries = reinterpret_cast<u16*>(list);
			u16 n = entries[0];

			for (i32 i = 0; i < n; i++)
			{
				i32 buf[3] = { 0, 0, 0 };
				gsub_4E3940(buf, entries[1 + i]);
			}

			return true;
		}

		case 0x4295:
			gsub_470950();
			return true;

		case 0x4296:
		case 0x4297:
			this->field_24C += 2;
			return true;

		case 0x4298:
		{
			u16 val = CBaddy_ResolveOperand(this);
			if (G_CAMERA_LIST != 0)
			{
				i32 mode;
				if (val == 0)
					mode = 2;
				else if (val == 1)
					mode = 1;
				else if (val == 2)
					mode = 0;
				else
					mode = 3;
				gsub_416880(&this->mPos, mode);
			}
			return true;
		}

		case 0x4299:
		{
			u16 val = CBaddy_ReadOperand(this);
			gsub_4708B0(val);
			return true;
		}

		case 0x429A:
		case 0x42A6:
		{
			i32 posBuf[3];

			if (cmd == 0x429A)
			{
				u16 val = CBaddy_ResolveOperand(this);
				gsub_4E3940(posBuf, val);
			}
			else
			{
				// the original's own guard here checks `a2==17050`, which
				// can never be true for these opcode values -- dead code,
				// kept as written rather than "fixed" (CLAUDE.md guidance).
				posBuf[0] = this->mPos.vx;
				posBuf[1] = this->mPos.vy;
				posBuf[2] = this->mPos.vz;
			}

			u16 sub = CBaddy_ReadOperand(this);

			switch (sub)
			{
				case 0:
				case 1:
					break;

				case 2:
				{
					i16 a = static_cast<i16>(*this->field_24C);
					this->field_24C++;
					if (!(a & 0x8000) && (a & 0x2000))
						a = this->GetVariable(a);
					i16 b = *reinterpret_cast<i16*>(reinterpret_cast<char*>(this->field_24C) + 2);
					this->field_24C = reinterpret_cast<i16*>(reinterpret_cast<char*>(this->field_24C) + 2);
					i16 c = *this->field_24C;
					this->field_24C++;
#ifdef SPIDEY_STANDALONE
					// CFlameExplosion::CFlameExplosion(const CVector*, i32,
					// i32, i32) on the freshly allocated bit (sizeof == 144)
					new CFlameExplosion(reinterpret_cast<CVector*>(posBuf), a, c != 0, c);
#else
					void *p = CBit::operator new(144);
					if (p != 0)
						gsub_43C250(posBuf, a, c != 0, c);
#endif
					break;
				}

				case 3:
				{
					i16 v110 = *this->field_24C; this->field_24C++;
					i16 v112 = *this->field_24C; this->field_24C++;
					i16 v113 = *this->field_24C; this->field_24C++;
					i16 v114 = *this->field_24C; this->field_24C++;
					i16 v115 = *this->field_24C; this->field_24C++;
					i16 v116 = *this->field_24C; this->field_24C++;
					i16 v117 = *this->field_24C; this->field_24C++;
					i16 v118 = *this->field_24C; this->field_24C++;
					i16 v119 = *this->field_24C; this->field_24C++;
					i16 v120 = *this->field_24C; this->field_24C++;

					i32 jitter = 2 * v120 + 1;
					i32 buf[3];
					buf[0] = posBuf[0] + ((Rnd(jitter) - v120) << 12);
					buf[1] = posBuf[1] + ((Rnd(jitter) - v120) << 12);
					buf[2] = posBuf[2] + ((Rnd(jitter) - v120) << 12);

#ifdef SPIDEY_STANDALONE
					// CWibbling3DExplosion ctor on the freshly allocated
					// object (sizeof == 276); the ctor takes a non-const
					// char* name like every other caller in exp.cpp
					new CWibbling3DExplosion(reinterpret_cast<CVector*>(buf), const_cast<char*>("expgrnd"),
							v110, v112, v113, v114, v115, v116, v117, v118, v119);
#else
					void *p = CClass::operator new(276);
					if (p != 0)
						gsub_43CEA0(buf, "expgrnd", v110, v112, v113, v114, v115, v116, v117, v118, v119);
#endif
					break;
				}

				default:
					return true;
			}

			return true;
		}

		case 0x42A7:
		case 0x42A8:
		{
			// 0x405939..0x405A0F: six 16-bit operands, then Effects_LaserFlash
			// (0x43B410) at this baddy's position. This is NOT part of the
			// 0x429A/0x42A6 explosion sub-switch above: that one reads a
			// single sub-opcode word, so sharing its body left the script
			// cursor up to five words short of the next opcode ("Bad script
			// command" in ParseScript).
			i32 posBuf[3];

			if (cmd == 0x429A)
			{
				// the original's own guard (dead for these opcode values),
				// kept as written
				u16 val = CBaddy_ResolveOperand(this);
				gsub_4E3940(posBuf, val);
			}
			else
			{
				posBuf[0] = this->mPos.vx;
				posBuf[1] = this->mPos.vy;
				posBuf[2] = this->mPos.vz;
			}

			u16 a3 = *reinterpret_cast<u16*>(this->field_24C); this->field_24C++;
			u16 a4 = *reinterpret_cast<u16*>(this->field_24C); this->field_24C++;
			i16 a5 = *this->field_24C; this->field_24C++;
			i16 a6 = *this->field_24C; this->field_24C++;
			i16 a7 = *this->field_24C; this->field_24C++;
			i16 a8 = *this->field_24C; this->field_24C++;

			gsub_43B410(posBuf, a3, a4, a5, a6, a7, a8);
			return true;
		}

		case 0x429C:
		{
			u8 *p = reinterpret_cast<u8*>(this->field_24C);
			i32 a2 = p[6];
			i32 a3 = p[4];
			i32 a4 = p[2];
			i32 a5 = p[0];
			gsub_43D830(a5, a4, a5, a2, 0, a2);
			this->field_24C = reinterpret_cast<i16*>(p + 10);
			return true;
		}

		case 0x429D:
			return true;

		case 0x429E:
			*gBaddyScriptPosY = this->mPos.vy;
			return true;

		case 0x42A0:
		{
			u16 val = CBaddy_ResolveOperand(this);
			u16 a4 = *reinterpret_cast<u16*>(this->field_24C);
			this->field_24C = reinterpret_cast<i16*>(reinterpret_cast<u16*>(this->field_24C) + 1);
			u16 a5 = *reinterpret_cast<u16*>(this->field_24C);
			this->field_24C = reinterpret_cast<i16*>(reinterpret_cast<u16*>(this->field_24C) + 1);
			u16 a6 = *reinterpret_cast<u16*>(this->field_24C);
			this->field_24C = reinterpret_cast<i16*>(reinterpret_cast<u16*>(this->field_24C) + 1);

			// 0x4058E3: the position argument is &this->mPos (esi = this+8
			// in this block), not the CBaddy. CPowerUp::SetGravity(i32, i32)
			// is __thiscall on the CPowerUp just returned (0x4058FB mov ecx,eax).
			CPowerUp *pPowerUp = gsub_46BD80(val, &this->mPos, a4, a5, -1);
			if (pPowerUp != 0)
				pPowerUp->SetGravity(a6 << 12, 5);

			return true;
		}

		case 0x42A2:
			*gInitBaddyRelated = *reinterpret_cast<u16*>(this->field_24C);
			this->field_24C += 1;
			return true;

		case 0x42A3:
		case 0x450A: // C_PLAY_FX (uncertain name)
		{
			i16 v196 = static_cast<i16>(CBaddy_ReadOperand(this));
			i16 v197 = static_cast<i16>(CBaddy_ReadOperand(this));
			i16 v198 = static_cast<i16>(CBaddy_ReadOperand(this));

			if (!(this->mType == 203 && this->field_234 != 0 && *gSubmarinerDieRelated != 0))
			{
				if (cmd == 0x42A3)
					gsub_479EE0(v196, v197, v198);
				else
					gsub_479D30(v196, v197, &this->mPos, v198);
			}

			return true;
		}

		case 0x42B0: // skip embedded string (shares tail with 0x4200/0x4707)
		{
			u8 *p = reinterpret_cast<u8*>(this->field_24C);
			if (*p != 0)
			{
				do { p++; } while (*p != 0);
			}
			if ((reinterpret_cast<i32>(p) & 1) == 0)
				this->field_24C = reinterpret_cast<i16*>(p + 2);
			else
				this->field_24C = reinterpret_cast<i16*>(p + 1);
			return true;
		}

		case 0x42B1:
		case 0x42B2:
		{
			u16 val = CBaddy_ResolveOperand(this);
			void *entry = gsub_4E3880(val);
			if (cmd == 0x42B1)
				gsub_4DFFE0(entry);
			else
				gsub_4DFD30(entry);
			return true;
		}

		case 0x42B3:
		case 0x42B4:
		{
			u16 val = CBaddy_ResolveOperand(this);
			if (cmd == 0x42B3)
			{
				gsub_4DFFB0(G_CONTROL_BADDY_LIST, val);
				gsub_4DFFB0(G_BADDY_LIST, val);
				gsub_4DFFB0(G_ENVIRONMENTAL_OBJECT_LIST, val);
			}
			else
			{
				gsub_4DFC20(val);
			}
			return true;
		}

		case 0x42B5:
		{
			i32 aligned = (reinterpret_cast<i32>(this->field_24C) + 3) & ~3;
			i32 *p = reinterpret_cast<i32*>(aligned);
			gsub_4273D0(*p);
			this->field_24C = reinterpret_cast<i16*>(p + 1);
			return true;
		}

		case 0x42B6: // C_SET_TARGET_FRAME (uncertain name)
		{
			u16 val = CBaddy_ResolveOperand(this);
			if (this->mType == 203 && this->field_234 != 0 && *gSubmarinerDieRelated != 0)
				return true;
			this->field_230 = val;
			return false;
		}

		case 0x42B7:
			this->field_234 = 1;
			return true;

		case 0x42B8:
		{
			i16 v175 = *this->field_24C; this->field_24C++;
			i16 v176 = *this->field_24C; this->field_24C++;

			i32 aligned = (reinterpret_cast<i32>(this->field_24C) + 3) & ~3;
			i32 *p = reinterpret_cast<i32*>(aligned);

			for (; *p != 0; p++)
			{
				i32 hit = gsub_4C9230(*p);
				if (hit != 0)
				{
					// the hit object's real type is not established here;
					// offsets kept raw (matches the original, which also
					// pokes an untyped pointer).
					if (v175 == 1)
						*reinterpret_cast<u8*>(hit + 28) = static_cast<u8>(v176);
					else
						*reinterpret_cast<u8*>(hit + 30) = static_cast<u8>(v176);
				}
			}

			this->field_24C = reinterpret_cast<i16*>(p + 1);
			return true;
		}

		case 0x42B9:
			*gWideScreen = *reinterpret_cast<u16*>(this->field_24C);
			this->field_24C += 1;
			*gWideScreenShadow = *gWideScreen;
			return true;

		case 0x42BA:
		{
			u16 val = CBaddy_ReadOperand(this);
			// the original stashes this straight into a MechList-family
			// object at offset 2283, whose real type/name is not
			// established here.
			if (G_MECHLIST_PLAYER != 0)
				*(reinterpret_cast<u8*>(G_MECHLIST_PLAYER) + 2283) = (val != 0);
			return true;
		}

		case 0x430A:
		{
			i16 v183 = *this->field_24C; this->field_24C++;
			i32 aligned = (reinterpret_cast<i32>(this->field_24C) + 3) & ~3;
			i32 *p = reinterpret_cast<i32*>(aligned);

			i32 buf[3];
			buf[0] = this->mPos.vx;
			buf[1] = (v183 << 12) + this->mPos.vy;
			buf[2] = this->mPos.vz;

			gsub_43B740(buf, *p);
			this->field_24C = reinterpret_cast<i16*>(p + 1);
			return true;
		}

		case 0x430B:
		{
			CBaddy *node = G_BADDY_LIST;
			while (node != 0 && node->mType != 314)
				node = static_cast<CBaddy*>(node->mNextItem);

			if (node != 0)
			{
				// vtable slot 17 (offset 0x44) on the type 314 baddy. Type
				// 314 is CCarnage (Trig_CreateObject case 314 ->
				// Carnage_CreateCarnage) and slot 17 of its vtable
				// (0x53B52C) is CCarnage::MakeSonicRipple, already declared
				// virtual in carnage.h and validated at slot 17, so this is
				// a plain virtual call. The previous raw-slot member
				// pointer trick indexed MSVC slot numbering, which is off
				// by one under GCC (two destructor entries per vtable), and
				// a member function pointer is 8 bytes there, so it broke
				// the standalone build.
				static_cast<CCarnage*>(node)->MakeSonicRipple(&this->mPos);
			}

			return true;
		}

		case 0x430C:
			gsub_438E20(G_MECHLIST_PLAYER);
			return true;

		case 0x430D:
			gsub_438EE0(G_MECHLIST_PLAYER);
			return true;

		case 0x450D: // C_CAMERA_TRAJECTORY (uncertain name, "BossCamStationaryRadius")
		{
			i16 v188 = *this->field_24C; this->field_24C++;
			u16 v189 = *reinterpret_cast<u16*>(this->field_24C); this->field_24C++;
			u16 v190 = *reinterpret_cast<u16*>(this->field_24C); this->field_24C++;
			u16 v191 = *reinterpret_cast<u16*>(this->field_24C); this->field_24C++;

			if (G_MECHLIST_PLAYER != 0)
			{
				i32 dist = gsub_4E6150(&this->mPos, reinterpret_cast<i32*>(&G_MECHLIST_PLAYER->mPos));

				if (dist <= static_cast<i32>(v190))
				{
					i32 buf[3];
					buf[0] = (G_MECHLIST_PLAYER->mPos.vx - this->mPos.vx) >> 12;
					buf[1] = 0;
					buf[2] = (G_MECHLIST_PLAYER->mPos.vz - this->mPos.vz) >> 12;

					gsub_470430(buf, buf);

					i32 result;
					if (dist > static_cast<i32>(v189))
						result = v188 * (dist - v189) / (v190 - v189);
					else
						result = v188;

					print_if_false(true, "bad value send to BossCamStationaryRadius");
					(void)result;
					(void)v191;
				}
			}

			return true;
		}

		case 0x450E:
		{
			u16 val = CBaddy_ReadOperand(this);
			// The original does not call Shake here: it stores the operand
			// into CameraList+680 (0x4068BF mov [esi+2A8h],edi, esi =
			// CameraList), i.e. CCamera::field_2A8. The only Shake call in
			// this function is in case 0x4298. The assert (0x4068AA test
			// edi,edi; setge cl) is on the zero-extended operand, so it can
			// never fire, same as the 0x450D copy above.
			if (G_CAMERA_LIST != 0)
			{
				print_if_false(true, "bad value send to BossCamStationaryRadius");
				G_CAMERA_LIST->field_2A8 = val;
			}
			return true;
		}

		case 0x450F:
		case 0x4511:
		case 0x4512:
		{
			u16 val = CBaddy_ReadOperand(this);
			void *list = gsub_4E3880(this->mNode);
			u16 *entries = reinterpret_cast<u16*>(list);
			u16 n = entries[0];

			for (i32 i = 0; i < n; i++)
			{
				u16 k = entries[1 + i];
				for (CBaddy *node = G_BADDY_LIST; node != 0; node = static_cast<CBaddy*>(node->mNextItem))
				{
					if (node->mNode != k)
						continue;

					if (cmd == 0x450F)
					{
						if (val != 0)
							node->mFlags &= ~1;
						else
							node->mFlags |= 1;
					}
					else if (cmd == 0x4511)
					{
						*reinterpret_cast<u16*>(reinterpret_cast<char*>(node) + 256) = val;
					}
					else
					{
						*reinterpret_cast<u16*>(reinterpret_cast<char*>(node) + 258) = val;
					}
				}
			}

			return true;
		}

		case 0x4601:
			this->field_218 |= 0x100000;
			this->mFlags |= 1;
			return true;

		case 0x4602:
			this->field_218 |= 0x200000;
			return true;

		case 0x4603:
			this->field_218 |= 0x400000;
			return true;

		case 0x4702: // this is the real "a2==18178" special case, separate
			         // from the 0x4601 switch case above despite the
			         // similar-looking values -- confirmed field_2F0 |= 1,
			         // not field_218, by re-checking the exact pseudocode
			this->field_2F0 |= 1;
			return true;

		case 0x4703:
			this->field_2F0 |= 2;
			return true;

		case 0x4704:
		case 0x4706:
			this->field_2F0 |= 8;
			return true;

		case 0x4705:
			this->field_2F0 |= 0x10;
			return true;

		case 0x4707: // reads one u16 operand into field_2F4 (NOT a string
			         // skip -- only shares the final "field_24C = p + 2"
			         // shape with 0x4200/0x42B0's tail, the source value is
			         // different: those scan past an embedded NUL string
			         // first, this does not)
		{
			u16 *p = reinterpret_cast<u16*>(this->field_24C);
			this->field_2F4 = *p;
			this->field_24C = reinterpret_cast<i16*>(p + 1);
			return true;
		}

		case 0x4708:
			this->field_2F0 |= 0x20;
			return true;

		case 0x4709:
			this->field_2F0 |= 0x40;
			return true;

		case 0x470B:
			this->field_1A4 = CBaddy_ReadOperand(this);
			return true;

		case 0x470C:
		{
			u16 a2 = CBaddy_ReadOperand(this);
			u16 a3 = CBaddy_ReadOperand(this);
			this->SetParamByIndex(a2, a3);
			return true;
		}

		case 0x470F:
			this->field_2A8 |= 0x2000000;
			return true;

		case 0x4103:
		case 0x4108:
		case 0x4109:
		case 0x410A:
		case 0x410B:
		case 0x410C:
		case 0x410D:
		case 0x410E:
		case 0x410F:
		default:
			// 0x406AAC: the original's unhandled-opcode tail. It consumes no
			// operands, so a script using an opcode this exe does not
			// implement (level 1 has 0x450B with two operands, seven times)
			// then feeds those operand words to ParseScript as opcodes,
			// which trips its "Bad script command" assert. Same in the
			// original, where both asserts are release no-ops.
			print_if_false(0, "Unknown script command");
#ifdef SPIDEY_STANDALONE
			if (getenv("SPIDEY_TRACE_SCRIPT"))
				fprintf(stderr, "SCRIPT unknown command %#x (type %d)\n", cmd, this->mType);
#endif
			return true;
	}
}

// @Ok
// @AlmostMatching: for the case 2120 the code for GetScriptVariable was inlined differently
// dunno why, already spent an un-godly amount of time matching others (cough cough 2121)
void CBaddy::SetVariable(u16 a2)
{
	switch (a2)
	{
		case 0x2103:
			this->field_1FC = *this->field_24C;
			this->field_24C++;
			break;
		case 0x2106:
			this->field_1FE = *this->field_24C;
			this->field_24C++;
			break;
		case 0x2135:
			{
				u32 *v6 = reinterpret_cast<u32*>((reinterpret_cast<i32>(this->field_24C) + 3) & 0xFFFFFFFC);

				this->field_23C = *v6;
				this->field_24C = reinterpret_cast<i16*>(&v6[1]);
			}
			break;
		case 0x212F:
			{
				u32 *v7 = reinterpret_cast<u32*>((reinterpret_cast<u32>(this->field_24C) + 3) & 0xFFFFFFFC);
				u16 Model = Spool_GetModel(*v7, this->mRegion);
				this->field_24C = reinterpret_cast<i16*>(&v7[1]);
				this->mModel = Model;
			}
			break;
		case 0x2140:
			this->mPos.vx = this->GetScriptValue() << 12;
			break;
		case 0x2141:
			this->mPos.vy = this->GetScriptValue() << 12;
			break;
		case 0x2142:
			this->mPos.vz = this->GetScriptValue() << 12;
			break;
		case 0x2131:
			if (*(this->field_24C++))
				this->mFlags |= 8;
			else
				this->mFlags &= ~8;
			break;
		case 0x212E:
			this->field_216 = *this->field_24C;
			this->field_24C++;
			break;
		case 0x212C:

			this->field_210 = *this->field_24C;
			this->field_24C++;
			break;
		case 0x212D:
			this->field_20F = *this->field_24C;
			this->field_24C++;
			break;
		case 0x2100:
			this->mHealth = *this->field_24C;
			this->field_24C++;
			break;
		case 0x2101:
			this->mRMinor = *this->field_24C;
			this->field_24C++;
			break;
		case 0x2125:
			{
				u16 v20 = *this->field_24C;
				this->field_24C++;
				DoAssert(v20 < 6u, "Attribute index out of bounds");
				this->attributeArr[v20] = *this->field_24C++;
			}
			break;
		case 0x2120:
			{
				i16 v20 = *this->field_24C++;
				i16 v21 = this->GetScriptValue();

				DoAssert((u8)v20 < 6, "Bad register index");

				this->realRegisterArr[v20 & 0xFF] = v21;
			}
			break;

		case 0x2138:
			this->field_24C = reinterpret_cast<i16*>((reinterpret_cast<i32>(this->field_24C) + 3 & 0xFFFFFFFC) + 4);
			break;





		case 0x2121:
			{
				// @Note: wow
				i16 *tmp = this->field_24C;
				i16 tmp2 = *tmp;
				this->field_24C++;
				this->mAnimSpeed = tmp2;
			}
			break;

		case 0x2122:
			this->field_21E = *this->field_24C;
			this->field_24C++;
			break;


		default:
			DoAssert(0, "Unknown script variable");
#ifdef SPIDEY_STANDALONE
			if (getenv("SPIDEY_TRACE_SCRIPT"))
				fprintf(stderr, "SCRIPT unknown variable (DoAssert site) %#x (type %d)\n", a2, this->mType);
#endif
			break;
	}
}

// @Ok
// @Matching
i16 CBaddy::GetVariable(u16 a2)
{
	switch (a2)
	{
		case 0x2139:
			return G_DIFFICULTY_LEVEL;

		case 0x2136:
			return G_GAME_FMV_PAD;

		case 0x2132:
		{
			if (G_MECHLIST_PLAYER)
			{
				u32 dist = Utils_XZDist(&this->mPos, &G_MECHLIST_PLAYER->mPos);
				return dist > 0x1FFF ? 0x1FFF : dist;
			}

			return 0x1FFF;
		}

		case 0x2133:
		{
			if (!G_MECHLIST_PLAYER)
				return 0;

			CVector v = this->mPos;
			v.vy -= this->field_220 << 12;

			return Utils_LineOfSight(&v, &G_MECHLIST_PLAYER->mPos, 0, 0);
		}

		case 0x212E:
			return this->field_216;

		case 0x212C:
			return this->field_210;

		case 0x212D:
			return this->field_20F;

		case 0x2140:
			return this->mPos.vx >> 12;

		case 0x2141:
			return this->mPos.vy >> 12;

		case 0x2142:
			return this->mPos.vz >> 12;

		case 0x2150:
			if (!G_MECHLIST_PLAYER)
				return 0;
			return G_MECHLIST_PLAYER->mPos.vx >> 12;

		case 0x2151:
			if (!G_MECHLIST_PLAYER)
				return 0;
			return G_MECHLIST_PLAYER->mPos.vy >> 12;

		case 0x2152:
			if (!G_MECHLIST_PLAYER)
				return 0;
			return G_MECHLIST_PLAYER->mPos.vz >> 12;

		case 0x2100:
			return this->mHealth;

		case 0x2129:
			return Rnd((u16)*this->field_24C++);

		case 0x2120:
		{
			u8 idx = (u8)*this->field_24C;
			this->field_24C++;
			print_if_false(idx < 6, "Bad register index");
			return this->realRegisterArr[idx];
		}

		case 0x212A:
			print_if_false(this->mNode != 0xFFFF, "V_MY_NODE in script object with no node");
			return this->mNode;

		case 0x212B:
		{
			u16 opcode = *this->field_24C;
			this->field_24C++;

			if (opcode & 0x2000)
				opcode = this->GetVariable(opcode);

			u16 *links = reinterpret_cast<u16*>(Trig_GetLinksPointer(opcode & 0xFFFF));
			if (!*links)
				return 0;

			return links[1];
		}

		default:
			print_if_false(0, "Unknown script variable");
#ifdef SPIDEY_STANDALONE
			if (getenv("SPIDEY_TRACE_SCRIPT"))
				fprintf(stderr, "SCRIPT unknown variable %#x (type %d)\n", a2, this->mType);
#endif
			return 0;
	}
}

// @Ok
// Decompiled from the original disasm at 0x404c50 (878 bytes, 273
// instructions), read via IDA/Hex-Rays. Field offsets cross-checked
// against the already-validated CBody layout (mVel 0x60, mAcc 0x6C,
// mFric 0x78, field_80 0x80, mAngVel 0x88, mAngAcc 0x8E, mAngFric 0x94,
// field_A8 0xA8, mCollision 0xE0) and CBaddy's own already-validated
// fields (field_1F8, field_230, field_27C, field_2A8, field_2AC,
// field_2B0, field_2B4, field_2B8, field_2C4, field_2D0, field_2DC,
// field_2E2, field_2E8).
//
// Shape: two early branches handle a running position/angle "teleport
// blend" already in progress (field_2B4 = duration, field_2B0 = elapsed
// so far, field_230 = active flag, field_2B8/field_2D0 = start position
// and per-tick rate, field_2C4 = final position (CVector),
// field_2DC = base angle, field_2E8 = per-tick angular rate,
// field_2E2 = final angle (CSVectors); on completion or abort it snaps to
// the final pos/angle and clears the state). Once both field_2B4 and
// field_2B0 are zero, the remaining block re-derives this frame's
// elapsed ticks: callers passing 0 (see Shouldnt_DoPhysics_Be_Virtual's
// this->DoPhysics(0)) just use field_80 (the per-frame delta, same field
// CBaddy::RunTimer subtracts elsewhere in this file); callers passing a
// nonzero value drain field_1F8, a "startup" countdown, by up to
// field_80 per call and use whatever it drained. It then does either
// standard mVel/mAcc/mFric integration (field_2A8 bit 0 clear; same
// idiom as CLizMan::DoLizmanPhysics in lizman.cpp and CPlatform::DoPhysics
// in platform.cpp) or a GTE rotate-translate pose blend of field_27C
// (field_2A8 bit 0 set, guarded by field_2AC bit 0 so it only runs every
// other call) using the same M3dMaths_RotMatrixYXZ / gte_SetRotMatrix /
// gte_ldlvl / gte_rtir / gte_stlvnl sequence as CBaddy::GetLocalPos
// above. gTrajectoryVector is the same global already used identically
// in CLizMan::DoLizmanPhysics and CPlatform::DoPhysics.
void CBaddy::DoPhysics(i32 a2)
{
	if (this->field_2B4 != 0)
	{
		i32 elapsed = this->field_2B0 + this->field_80;
		this->field_2B0 = elapsed;

		if (static_cast<u32>(elapsed) < static_cast<u32>(this->field_2B4) && this->field_230 != 0)
		{
			this->mPos = this->field_2B8 + this->field_2D0 * elapsed;

			this->mAngles.vx = this->field_2DC.vx + static_cast<i16>(this->field_2E8.vx * elapsed);
			this->mAngles.vy = this->field_2DC.vy + static_cast<i16>(this->field_2E8.vy * elapsed);
			this->mAngles.vz = this->field_2DC.vz + static_cast<i16>(this->field_2E8.vz * elapsed);
			this->mAngles.Mask();
		}
		else
		{
			this->field_2B0 = 0;
			this->field_2B4 = 0;
			this->field_230 = 0;

			this->mPos.vx = this->field_2C4.vx;
			this->mPos.vy = this->field_2C4.vy;
			this->mPos.vz = this->field_2C4.vz;

			this->mAngles.vx = this->field_2E2.vx;
			this->mAngles.vy = this->field_2E2.vy;
			this->mAngles.vz = this->field_2E2.vz;
		}

		return;
	}

	if (this->field_2B0 != 0)
	{
		if (static_cast<u32>(this->field_80) < static_cast<u32>(this->field_2B0) && this->field_230 != 0)
		{
			this->field_2B0 -= this->field_80;
		}
		else
		{
			this->field_230 = 0;
			this->field_2B0 = 0;
		}

		return;
	}

	this->field_A8 = G_TRAJECTORY_VECTOR;
	this->mCollision = 0;

	i32 elapsed;

	if (a2 == 0)
	{
		elapsed = this->field_80;
	}
	else
	{
		elapsed = this->field_1F8 > this->field_80 ? this->field_80 : this->field_1F8;

		if (elapsed == 0)
			return;

		this->field_1F8 -= elapsed;
	}

	if (this->field_2A8 & 1)
	{
		if (this->field_2AC & 1)
		{
			this->field_2AC = 0;
			return;
		}

		this->field_27C += this->mAcc;

		MATRIX rotMat;
		M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR*>(&this->mAngles), &rotMat);
		gte_SetRotMatrix(&rotMat);

		this->mAngles += this->mAngVel;
		this->mAngles.Mask();

		CVector scaled = this->field_27C >> 6;
		gte_ldlvl(reinterpret_cast<VECTOR*>(&scaled));
		gte_rtir();

		CVector rotated;
		gte_stlvnl(reinterpret_cast<VECTOR*>(&rotated));
		rotated <<= 6;

		this->mPos += rotated * elapsed;
		this->mPos += this->mVel * elapsed;

		this->field_2AC = 0;
	}
	else
	{
		this->mVel += this->mAcc;
		this->mVel %= this->mFric;
		this->mVel.KillSmall();

		this->mPos += this->mVel * elapsed;

		this->mAngles.vx += static_cast<i16>(this->mAngVel.vx * elapsed);
		this->mAngles.vy += static_cast<i16>(this->mAngVel.vy * elapsed);
		this->mAngles.vz += static_cast<i16>(this->mAngVel.vz * elapsed);
		this->mAngles.Mask();

		this->mAngVel += this->mAngAcc;
		this->mAngVel %= this->mAngFric;
		this->mAngVel.KillSmall();

		this->field_2AC = 0;
	}
}

// @Ok
void CBaddy::Baddy_SendSignal(void)
{
	u16 *ptr = reinterpret_cast<u16*>(
			Trig_GetLinksPointer(this->mNode));
	if (ptr)
		Trig_SendSignalToLinks(ptr);
}

// @Ok
// @Matching
CBaddy* FindBaddyOfType(i32 type)
{
	CItem *current = G_BADDY_LIST;

	while (current)
	{
		if (current->mType == type)
		{
			return reinterpret_cast<CBaddy*>(current);
		}

		current = current->mNextItem;
	}

	return 0;
}

// @Ok
// @Matching
void CBaddy::MarkAIProcList(i32 a2, i32 a3, i32 a4)
{
	CAIProc* it = this->mAIProcList;

	while (it)
	{
		CAIProc *current = it;
		it = it->mNext;


		if (a2 && (current->mAIProcType & AI_TYPE_UNK_20000) == 0 ||
				a3 && (current->mAIProcType & 0xFF00) == a3)
		{
			current->field_10 |= 1;
		}
		else if (a4)
		{
			if ((current->mAIProcType & 0xFF00) == a4)
			{
				current->field_10 |= 4;
			}
		}
	}
}

// @Ok
CBaddy::~CBaddy(void)
{
	print_if_false(G_NUM_BADDIES > 0, "Negative NumBaddies");
	--G_NUM_BADDIES;

	this->CleanUpAIProcList(1);
	this->CleanUpMessages(1, 0);
}

void validate_CBaddy(void){
	VALIDATE_SIZE(CBaddy, 0x324);

	VALIDATE(CBaddy, field_194, 0x194);
	VALIDATE(CBaddy, field_198, 0x198);

	VALIDATE(CBaddy, field_1A4, 0x1A4);
	VALIDATE(CBaddy, field_1A8, 0x1A8);

	VALIDATE(CBaddy, field_1F0, 0x1F0);
	VALIDATE(CBaddy, field_1F4, 0x1F4);
	VALIDATE(CBaddy, field_1F8, 0x1F8);
	VALIDATE(CBaddy, field_1FC, 0x1FC);
	VALIDATE(CBaddy, field_1FE, 0x1FE);

	VALIDATE(CBaddy, field_204, 0x204);
	VALIDATE(CBaddy, field_208, 0x208);
	VALIDATE(CBaddy, field_20C, 0x20C);
	VALIDATE(CBaddy, field_20E, 0x20E);
	VALIDATE(CBaddy, field_20F, 0x20F);
	VALIDATE(CBaddy, field_210, 0x210);


	VALIDATE(CBaddy, field_211, 0x211);
	VALIDATE(CBaddy, field_212, 0x212);
	VALIDATE(CBaddy, field_213, 0x213);
	VALIDATE(CBaddy, field_214, 0x214);

	VALIDATE(CBaddy, field_216, 0x216);
	VALIDATE(CBaddy, field_218, 0x218);
	VALIDATE(CBaddy, field_21D, 0x21D);
	VALIDATE(CBaddy, field_21E, 0x21E);

	VALIDATE(CBaddy, field_220, 0x220);
	VALIDATE(CBaddy, realRegisterArr, 0x222);

	VALIDATE(CBaddy, field_230, 0x230);
	VALIDATE(CBaddy, field_234, 0x234);
	VALIDATE(CBaddy, field_238, 0x238);
	VALIDATE(CBaddy, field_23C, 0x23C);

	VALIDATE(CBaddy, field_240, 0x240);

	VALIDATE(CBaddy, field_24C, 0x24C);

	VALIDATE(CBaddy, labelArr, 0x250);

	VALIDATE(CBaddy, attributeArr, 0x270);
	VALIDATE(CBaddy, field_27C, 0x27C);

	VALIDATE(CBaddy, field_288, 0x288);

	VALIDATE(CBaddy, mAIProcList, 0x28C);
	VALIDATE(CBaddy, pMessage, 0x290);

	VALIDATE(CBaddy, field_294, 0x294);
	VALIDATE(CBaddy, field_298, 0x298);
	VALIDATE(CBaddy, field_29C, 0x29C);

	VALIDATE(CBaddy, field_2A0, 0x2A0);
	VALIDATE(CBaddy, field_2A4, 0x2A4);
	VALIDATE(CBaddy, field_2A8, 0x2A8);
	VALIDATE(CBaddy, field_2AC, 0x2AC);

	VALIDATE(CBaddy, field_2B0, 0x2B0);
	VALIDATE(CBaddy, field_2B4, 0x2B4);
	VALIDATE(CBaddy, field_2B8, 0x2B8);


	VALIDATE(CBaddy, field_2C4, 0x2C4);

	VALIDATE(CBaddy, field_2D0, 0x2D0);

	VALIDATE(CBaddy, field_2DC, 0x2DC);
	VALIDATE(CBaddy, field_2E2, 0x2E2);
	VALIDATE(CBaddy, field_2E8, 0x2E8);


	VALIDATE(CBaddy, field_2F0, 0x2F0);
	VALIDATE(CBaddy, field_2F4, 0x2F4);
	VALIDATE(CBaddy, field_2F8, 0x2F8);
	VALIDATE(CBaddy, field_2FC, 0x2FC);


	VALIDATE(CBaddy, field_308, 0x308);
	VALIDATE(CBaddy, field_30C, 0x30C);
	VALIDATE(CBaddy, field_310, 0x310);
	VALIDATE(CBaddy, field_314, 0x314);
	VALIDATE(CBaddy, field_318, 0x318);
	VALIDATE(CBaddy, field_31C, 0x31C);
	VALIDATE(CBaddy, dumbAssPad, 0x320);

	VALIDATE_VTABLE(CBaddy, AI, 2);
	VALIDATE_VTABLE(CBaddy, Hit, 3);
	VALIDATE_VTABLE(CBaddy, PlayerIsVisible, 5);
	VALIDATE_VTABLE(CBaddy, CreateCombatImpactEffect, 6);
	VALIDATE_VTABLE(CBaddy, TugImpulse, 7);
	VALIDATE_VTABLE(CBaddy, Victorious, 8);

	VALIDATE_VTABLE(CBaddy, SetParamByIndex, 9);
	VALIDATE_VTABLE(CBaddy, Grab, 10);

	VALIDATE_VTABLE(CBaddy, Shouldnt_DoPhysics_Be_Virtual, 11);
	VALIDATE_VTABLE(CBaddy, GetClosest, 13);
	VALIDATE_VTABLE(CBaddy, ExecuteCommand, 14);
	VALIDATE_VTABLE(CBaddy, SetVariable, 15);
	VALIDATE_VTABLE(CBaddy, GetVariable, 16);
}


void validate_CScriptOnlyBaddy(void){

	VALIDATE_SIZE(CScriptOnlyBaddy, 0x330);
	VALIDATE(CScriptOnlyBaddy, field_324, 0x324);
	VALIDATE_VTABLE(CScriptOnlyBaddy, AI, 2);
	VALIDATE_VTABLE(CScriptOnlyBaddy, ExecuteCommand, 14);
	VALIDATE_VTABLE(CScriptOnlyBaddy, SetVariable, 15);
	VALIDATE(CScriptOnlyBaddy, field_328, 0x328);
	VALIDATE(CScriptOnlyBaddy, field_32C, 0x32C);
	VALIDATE(CScriptOnlyBaddy, field_32E, 0x32E);
}

void validate_SStateFlags(void){
	VALIDATE_SIZE(SStateFlags, 0x4);
}

// tentative: shared zero-initialized CVector at 0x60D9D0, confirmed all-zero
// BSS via IDA and referenced from well over a dozen unrelated bit.cpp
// functions (0x411D50 .. 0x4478A9) -- a common "pass the origin" scratch
// global reused across many CGlow/CBit-family constructors, not Mysterio-
// specific. Just this file's local handle on it.
static CVector * const gGlowZeroPos = reinterpret_cast<CVector*>(0x60D9D0);

// @Ok
// Decompiled from 0x45AAA0 (0x307 bytes, SEH-protected because of the two
// nested "new" calls below). See the class comment in mysterio.h for how
// the CGlow base class and the 0x5C+ field layout were worked out (the
// class is NOT CQuadBit-derived despite the header's old guess: no
// CQuadBit::CQuadBit/AttachTo(&QuadBitList) call happens for "this", but a
// real call to the CVector*+ints+6xu8 CGlow ctor overload does, and
// SetRGB(128,0,255) near the end is CGlow::SetRGB called on "this").
// gWhatIf selects the "goldfish" visual (What If / alternate-story mode)
// instead of the normal twin "Ken'sCircle" glow rings; CGoldFish::CGoldFish
// and CMysterioHeadCircle::CMysterioHeadCircle (mysterio.cpp) are both
// inlined at their call sites in the real binary, this repo gives them real
// out-of-line constructors instead (functional-only bar, no behavior
// change). mProtected/mCBodyFlags on the freshly built child are set here,
// not inside the child's own ctor, matching the original doing it
// unconditionally (even on a hypothetical allocation failure) right after
// storing the pointer.
CMysterioHeadGlow::CMysterioHeadGlow(CMysterio *owner)
	: CGlow(gGlowZeroPos, 150, 120, 255, 255, 255, 128, 0, 255)
{
	this->mOwnerHandle = Mem_MakeHandle(owner);

	if (gWhatIf)
	{
		CGoldFish *goldfish = new CGoldFish();
		this->field_B8 = goldfish;
		goldfish->mCBodyFlags |= 0x20;
	}
	else
	{
		CMysterioHeadCircle *circle1 = new CMysterioHeadCircle();
		this->field_B0 = circle1;
		circle1->mProtected = 1;

		CMysterioHeadCircle *circle2 = new CMysterioHeadCircle();
		this->field_B4 = circle2;
		circle2->mProtected = 1;
	}

	this->field_A4 = 0;

	this->SetRGB(128, 0, 255);

	for (u32 i = 0; i < this->mNumSections; i++)
	{
		this->mSectionPhase[i] = Rnd(4096);
		this->mSectionPeriod[i] = Rnd(50) + 200;
	}

	if (this->field_B0 && this->field_B4)
	{
		this->field_B0->NormalMode();
		this->field_B4->NormalMode();
	}
	else if (this->field_B8)
	{
		this->field_B8->NormalMode();
	}

	this->mFrigDeltaZ = -160;
}

// @Ok
// @Matching
// Decompiled from CSoftSpot_CSoftSpot at 0x45F700 (369 bytes). Params
// recovered from the disasm, not from a header: owner is the CBaddy that
// this soft spot belongs to (only used to shuffle owner in and out of
// BaddyList, and for Mem_MakeHandle), health becomes mHealth, node becomes
// mNode, type becomes field_324 and picks the branch. InitItem uses the original asset names at
// 0x54E2F8 and 0x54E2EC. 0x56E990 is
// BaddyList per idbs/idb_globals.txt, 0x54D474 is DifficultyLevel same
// source (already used elsewhere in this file).
CSoftSpot::CSoftSpot(CBaddy* owner, i32 health, i32 node, i32 type)
{
	if (type >= 6)
	{
		this->InitItem("softeyes");

		if (type == 10)
			this->mFlags |= 1;
	}
	else
	{
		this->InitItem("softspot");
		this->mFlags |= 0x400;

		i32 grey = Rnd(110) + 20;
		this->mRGB = (((grey << 8) | grey) << 8) | grey;

		this->field_328 = Rnd(3) + 4;
		this->field_32c = 100;
		this->field_194 = 12;
	}

	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	print_if_false(owner != 0, "no owner for soft spot");

	owner->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
	owner->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->field_330 = Mem_MakeHandle(owner);

	this->mHealth = static_cast<i16>(health);
	this->mType = 0x149;
	this->mNode = static_cast<u16>(node);
	this->field_324 = type;
	this->mRMinor = 0x96;

	if (type == 10)
	{
		this->mCBodyFlags &= 0xFFEF;
		this->mRMinor = 0;
	}
	else
	{
		this->mCBodyFlags |= 0x10;
	}

	if (G_DIFFICULTY_LEVEL)
		this->field_2A8 |= 0x10000;

	this->field_2A8 |= 0x200;
}

#include "my_patch.h"

// @Bogus
// Left out on purpose:
//   CBaddy::Grab (0x00403A80) is only 5 bytes ("xor al,al; ret 4"), so a
//     6 byte push/ret does not fit inside the function.
//   CBaddy::CreateCombatImpactEffect and CBaddy::SetParamByIndex both fold
//     into the shared 3 byte "ret 8" stub at 0x00407F30, Victorious into the
//     1 byte "ret" at 0x004015B0 and UnknownCBaddyFunctionFive into the
//     3 byte "ret 4" at 0x00407F60. All too small, and all shared with other
//     classes.
//   CBaddy::GetLocalPos and CBaddy::SendDeathPulse have no out of line copy
//     in the exe, the original inlined them everywhere.
//   CSoftSpot::CSoftSpot (0x0045F700) and CMysterioHeadGlow::CMysterioHeadGlow
//     (0x0045AAA0): hooking a constructor stamps our vtable on the object, and
//     our versions of these two classes are missing overrides the exe has
//     (CSoftSpot::AI 0x0045FC10 and CSoftSpot::Hit 0x0045F940;
//     CMysterioHeadGlow::Move 0x0045AE50 and ~CMysterioHeadGlow 0x0045ADB0).
//     Hooking them would silently drop those.
void patch_baddy(void)
{
	PATCH_PUSH_RET(0x00402BE0, CBaddy::RunTimer);
	PATCH_PUSH_RET(0x00402F60, CBaddy::GetNextWaypoint);
	PATCH_PUSH_RET(0x004030C0, CBaddy::YawTowards);
	PATCH_PUSH_RET(0x00403160, CBaddy::CheckStateFlags);
	PATCH_PUSH_RET(0x00403230, CBaddy::Baddy_SendSignal);
	PATCH_PUSH_RET(0x00403310, CBaddy::PathCheck);
	PATCH_PUSH_RET(0x00403350, CBaddy::PathCheckGuts);
	PATCH_PUSH_RET(0x004039D0, CBaddy::CleanUpAIProcList);
	PATCH_PUSH_RET(0x00403A10, CBaddy::MarkAIProcList);
	PATCH_PUSH_RET(0x00403A90, CBaddy::CleanUpMessages);
	PATCH_PUSH_RET(0x00403B60, CBaddy::GetWaypointNearTarget);
	PATCH_PUSH_RET(0x00403C30, CBaddy::SmackSpidey);
	PATCH_PUSH_RET(0x00403E70, CBaddy::TrapWeb);
	PATCH_PUSH_RET(0x00403EF0, CBaddy::TugWeb);
	PATCH_PUSH_RET(0x00403F90, FindBaddyOfType);
	PATCH_PUSH_RET(0x00403FC0, CBaddy::MakeSpriteRing);
	PATCH_PUSH_RET(0x004040E0, CBaddy::Neutralize);
	PATCH_PUSH_RET(0x00404170, CBaddy::DistanceToPlayer);
	PATCH_PUSH_RET(0x004041C0, CBaddy::SetHeight);
	PATCH_PUSH_RET(0x00404320, CBaddy::Die);
	PATCH_PUSH_RET(0x00404470, CBaddy::BumpedIntoSpidey);
	PATCH_PUSH_RET(0x00404510, CBaddy::CheckSightCone);
	PATCH_PUSH_RET(0x00404810, CBaddy::ShouldFall);
	PATCH_PUSH_RET(0x004048F0, CBaddy::AddPointToPath);
	PATCH_PUSH_RET(0x00404AE0, CBaddy::StruckGameObject);
	PATCH_PUSH_RET(0x00404B60, CBaddy::RunAppropriateAnim);
	PATCH_PUSH_RET(0x00404C50, CBaddy::DoPhysics);
	PATCH_PUSH_RET(0x00404FD0, CBaddy::ParseScript);
	PATCH_PUSH_RET(0x00406E50, CBaddy::GetScriptValue);

	// virtuals, the constructor and the destructor go through the export
	// table, a member pointer to a virtual is a vcall thunk and would loop
	// straight back into the patched address.
	PATCH_PUSH_RET_POLY(0x00402C00, CBaddy::CBaddy, "??0CBaddy@@QAE@XZ");
	// 0x00402D60, not the 0x00460780 tools/names.json points at: 0x00402D60
	// stores CBaddy's vtable (0x0053B2A4) and decrements NumBaddies, and it is
	// what CBaddy's deleting destructor at 0x00402D40 calls. 0x00460780 stores
	// 0x0053BBE8 and is CSuper::~CSuper.
	PATCH_PUSH_RET_POLY(0x00402D60, CBaddy::~CBaddy, "??1CBaddy@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x00403AD0, CBaddy::GetClosest, "?GetClosest@CBaddy@@UAEPAV1@HH@Z");
	PATCH_PUSH_RET_POLY(0x00404650, CBaddy::PlayerIsVisible, "?PlayerIsVisible@CBaddy@@UAEHXZ");
	PATCH_PUSH_RET_POLY(0x00404C40, CBaddy::Shouldnt_DoPhysics_Be_Virtual, "?Shouldnt_DoPhysics_Be_Virtual@CBaddy@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x004050B0, CBaddy::ExecuteCommand, "?ExecuteCommand@CBaddy@@UAEHG@Z");
	PATCH_PUSH_RET_POLY(0x00406EE0, CBaddy::SetVariable, "?SetVariable@CBaddy@@UAEXG@Z");
	PATCH_PUSH_RET_POLY(0x004072A0, CBaddy::GetVariable, "?GetVariable@CBaddy@@UAEFG@Z");
	PATCH_PUSH_RET_POLY(0x00407F40, CBaddy::TugImpulse, "?TugImpulse@CBaddy@@UAEEPAVCVector@@00@Z");
}
