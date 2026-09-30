#include "cop.h"
#include "validate.h"
#include "ps2lowsfx.h"
#include "mem.h"
#include "utils.h"
#include "spidey.h"
#include "exp.h"
#include "ai.h"
#include "web.h"
#include "m3dcolij.h"

#include <cstring>


// 0x00682C3C and 0x00682C40 in the exe (gCopGlobal / gCopList in the
// maintainer's IDB). CCop_Hit, CCop_Fall, CCop_ChasePlayer and CCop_AI write
// both and none of those are hooked, so the exe's copies are the real ones.
#ifndef SPIDEY_STANDALONE
EXPORT CCop* gCopGlobal;
#else
extern CCop* gCopGlobal;
#endif
//#define G_COP_GLOBAL (gCopGlobal)
#define G_COP_GLOBAL (*reinterpret_cast<CCop**>(0x00682C3C))

#ifndef SPIDEY_STANDALONE
EXPORT CCop* gCopList;
#else
extern CCop* gCopList;
#endif
//#define G_COP_LIST (gCopList)
#define G_COP_LIST (*reinterpret_cast<CCop**>(0x00682C40))

// 0x00682C18. cop.cpp used to call this gAttackFlagsRelated and thug.cpp had
// its own private static of the same thing, but the exe has one global that
// both files share. Declared in baddy.h with its G_ macro, defined here
// because 0x00682C18 sits just before gCopGlobal in the exe's data.
#ifndef SPIDEY_STANDALONE
EXPORT u8 gAttackFlagRelated;
#else
extern u8 gAttackFlagRelated;
#endif

// Per-species state flags, address 0x549220 in the original (used by
// CCop::Grab's CheckStateFlags call). Same idiom as gThugStateFlags
// (thug.cpp) and gRhinoStateFlags (rhino.cpp). Read only pairs of i16 that
// the exe ships in its .data; our copy has no initialiser, so it is all
// zeros and has to be read from the exe.
#ifndef SPIDEY_STANDALONE
EXPORT SStateFlags gCopStateFlags;
#else
extern SStateFlags gCopStateFlags;
#endif
//#define G_COP_STATE_FLAGS (&gCopStateFlags)
#define G_COP_STATE_FLAGS (reinterpret_cast<SStateFlags*>(0x00549220))

// Fight animation choices and hand hooks read by SetCopType at 0x4288D0.
static i32* const gPoliceFightAnims = (i32*)0x00551E88;
static i32* const gSwatFightAnims = (i32*)0x00551EA8;
static void* const gPoliceHooksPacket = (void*)0x00549328;
static void* const gSwatHooksPacket = (void*)0x00549408;

// @Ok
// @Matching
void CCop::SetCopType(i32 type)
{
	this->mType = type;
	switch (type)
	{
		case 306:
			this->InitItem("police");
			this->field_21E = 100;
			this->field_294.Int = gPoliceFightAnims[0];
			this->field_298.Int = gPoliceFightAnims[1];
			M3dUtils_ReadHooksPacket(this, gPoliceHooksPacket);
			break;
		case 320:
			this->InitItem("swat");
			this->field_21E = 100;
			this->field_294.Int = gSwatFightAnims[0];
			this->field_298.Int = gSwatFightAnims[1];
			M3dUtils_ReadHooksPacket(this, gSwatHooksPacket);
			break;
		default:
			print_if_false(0, "Unknown cop type!");
			break;
	}
}

// @Ok
// @Matching
// 0x428EE0. Native comparison passed 13824 cases.
i32 CCop::SetAnimMode(i32 mode, i32 playAnim)
{
	u8 oldMode = this->field_298.Bytes[1];
	i32 anim = -1;
	if (this->field_298.Bytes[1] != mode)
	{

		if (this->mType == 306)
		{
			switch (mode)
			{
				case 0:
					if (oldMode == 1)
						anim = 13;
					else if (oldMode == 2)
						anim = 6;
					this->field_294.Int = gPoliceFightAnims[0];
					this->field_298.Int = gPoliceFightAnims[1];
					break;
				case 1:
					this->field_294.Int = gPoliceFightAnims[2];
					this->field_298.Int = gPoliceFightAnims[3];
					break;
				case 2:
					if (oldMode == 1)
						anim = 4;
					else if (oldMode == 3)
						anim = 7;
					this->field_294.Int = gPoliceFightAnims[4];
					this->field_298.Int = gPoliceFightAnims[5];
					break;
				case 3:
					if (oldMode == 2)
						anim = 12;
					this->field_294.Int = gPoliceFightAnims[6];
					this->field_298.Int = gPoliceFightAnims[7];
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
						anim = 13;
					this->field_294.Int = gSwatFightAnims[0];
					this->field_298.Int = gSwatFightAnims[1];
					break;
				case 1:
					this->field_294.Int = gSwatFightAnims[2];
					this->field_298.Int = gSwatFightAnims[3];
					break;
				case 2:
					this->field_294.Int = gSwatFightAnims[4];
					this->field_298.Int = gSwatFightAnims[5];
					break;
				case 3:
					this->field_294.Int = gSwatFightAnims[6];
					this->field_298.Int = gSwatFightAnims[7];
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

// @Ok
// @Matching
// 0x42AC60. Native comparison passed 100000 cases.
i32 CCop::TooCloseToSpidey(void)
{
	if (this->mType == 306)
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
// 0x42ED60
void CCop::PlaySounds(void)
{
	if (this->mType == 306)
	{
		switch (this->mAnim)
		{
			case 22:
				if (this->mFrame == 15)
				{
					CVector offset;
					offset.vz = 409600;
					offset.vx = 0;
					offset.vy = 0;
					this->MakeSpriteRing(&offset);
				}
				break;
			case 24:
				if (this->mFrame == 23)
				{
					CVector offset;
					offset.vx = 0;
					offset.vy = 0;
					offset.vz = -122880;
					this->MakeSpriteRing(&offset);
				}
				break;			case 14:
				if (this->mFrame == 27 || this->mFrame == 13)
					SFX_PlayPos((Rnd(4) + 32) | 0x8000, &this->mPos, 300);
				break;
			case 8:
				if (this->mFrame == 4 || this->mFrame == 14)
					SFX_PlayPos((Rnd(4) + 32) | 0x8000, &this->mPos, 50);
				break;
			case 10:
				if (this->mFrame == 5)
					SFX_PlayPos(0x801D, &this->mPos, 100);
				break;

		}
	}
	else
	{
		switch (this->mAnim)
		{
			case 24:
				if (this->mFrame == 28)
				{
					CVector offset;
					offset.vz = -204800;
					offset.vx = 0;
					offset.vy = 0;
					this->MakeSpriteRing(&offset);
				}
				break;			case 22:
				if (this->mFrame == 15)
				{
					CVector offset;
					offset.vx = 0;
					offset.vy = 0;
					offset.vz = 204800;
					this->MakeSpriteRing(&offset);
				}
				break;
			case 14:
				if (this->mFrame == 2 || this->mFrame == 12)
					SFX_PlayPos((Rnd(4) + 32) | 0x8000, &this->mPos, 300);
				break;
			case 8:
				if (this->mFrame == 13 || this->mFrame == 4)
					SFX_PlayPos((Rnd(4) + 32) | 0x8000, &this->mPos, 50);
				break;
			case 10:
				if (this->mFrame == 3)
					SFX_PlayPos(0x801D, &this->mPos, 100);
				break;
			case 11:
				if (this->mFrame == 5)
					SFX_PlayPos((Rnd(2) + 30) | 0x8000, &this->mPos, 0);
				break;

		}
	}
}

// @NotOk
// 0x42F310, instruction matching is pending.
void CCop::BackpedalPlease(void)
{
	CVector target;
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->field_1F8 = 0;
			target = this->mPos;
			target.vx += 6 * ((this->mPos.vx - G_MECHLIST_PLAYER->mPos.vx) >> 2);
			target.vz += 6 * ((this->mPos.vz - G_MECHLIST_PLAYER->mPos.vz) >> 2);
			if (this->AddPointToPath(&this->mPos, 0) && this->AddPointToPath(&target, 0))
			{
				this->mVel = (this->mPos - G_MECHLIST_PLAYER->mPos) >> 2;
				this->mVel.vy = 0;
			}
			this->RunAnim(20, 0, -1);
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

// @NotOk
// Native comparison passed 100000 cases; instruction matching is pending.
// 0x42C570
void CCop::TakeHit(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->mCBodyFlags &= ~0x10;
			this->field_310 = 0;
			this->ClearAttackFlags();
			if (this->field_318 == 1)
			{
				this->mRMinor = 0;
				this->RunAnim(22, 0, -1);
				this->dumbAssPad = 2;
			}
			else
			{
				new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 0, 80, 200);
				this->RunAnim(20, 0, -1);
				this->dumbAssPad = 3;
			}
			break;
		case 1:
			if (this->mAnimFinished)
			{
				this->field_318 = 0;
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}
			break;
		case 2:
			if (this->mAnimFinished)
			{
				this->mRMinor = this->mType != 306 ? 150 : 96;
				this->RunAnim(23, 0, -1);
				this->dumbAssPad = 1;
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
// 0x42D060, instruction matching is pending.
void CCop::GetWhippedLikeTheWhoreYouAre(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->dumbAssPad++;
			this->RunAnim(22, 0, -1);
			this->field_1F8 = 0;
			// fall through
		case 1:
		{
			this->field_38C |= 2;
			print_if_false(this->field_384 != 0, "Pointer to mpThrowPoints is NULL.");
			this->field_2FC = this->mPos;
			i32 path = this->PathCheck(&this->mPos, &this->field_384[this->field_1F8], 0, 55);
			if (path || !this->AddPointToPath(&this->field_384[this->field_1F8], 0)
				|| ++this->field_1F8 >= 8)
			{
				if (this->ShouldFall(200, 389120))
				{
					this->mPos = this->field_2FC;
					this->SetHeight(1, 100, 600);
					this->field_31C.bothFlags = 26;
					this->dumbAssPad = 0;
				}
				else
				{
					if (path)
						this->RunAnim(22, 0, -1);
					this->dumbAssPad++;
				}
			}
			else
				this->mPos = this->field_384[this->field_1F8 - 1];
			break;
		}
		case 2:
			if (this->mAnimFinished)
			{
				this->mHealth -= 50;
				if (this->mHealth <= 0)
				{
					this->field_31C.bothFlags = 26;
					this->dumbAssPad = 0;
				}
				else
				{
					this->RunAnim(23, 0, -1);
					this->dumbAssPad++;
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

// @Ok
// @Matching
// 0x42EBD0
void CCop::LookConfused(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_1F8 = Rnd(3) + 2;
			this->dumbAssPad++;
			// fall through
		case 1:
			this->Neutralize();
			if (!this->field_1F8)
			{
				this->field_324 = 0;
				this->field_31C.bothFlags = 28;
				this->dumbAssPad = 0;
			}
			else
			{
				this->dumbAssPad = 5;
				this->field_1F8--;
			}
			break;
		case 5:
			{
				i32 angle = Rnd(1000) + 1000;
				if (Rnd(2))
					angle = -angle;
				angle += this->mAngles.vy;
				new CAIProc_LookAt(this, angle, 2, 80, 200);
				this->dumbAssPad++;
			}
			break;
		case 6:
			this->RunAppropriateAnim();
			if (this->field_288 & 2)
			{
				this->dumbAssPad = 1;
				this->field_288 &= ~2;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @Matching
// 0x42EA60
void CCop::Guard(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->mCBodyFlags |= 0x10;
			this->dumbAssPad++;
			if (this->mAnim != 31)
			{
				this->RunAnim(this->mType != 306 ? 33 : 12, 0, -1);
				break;
			}
			this->mAnimFinished = 1;
			// fall through
		case 1:
			if (this->mAnimFinished)
			{
				this->CycleAnim(31, 1);
				this->field_230 = Rnd(30) + 20;
				this->dumbAssPad++;
			}
			break;
		case 2:
			if (this->field_230-- <= 0)
			{
				this->RunAnim(this->mType != 306 ? 32 : 7, 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 3:
			if (this->mAnimFinished)
			{
				this->CycleAnim(static_cast<u8>(this->field_298.Bytes[0]), 1);
				this->field_230 = Rnd(30) + 20;
				this->dumbAssPad++;
			}
			break;
		case 4:
			if (this->field_230-- <= 0)
				this->dumbAssPad = 0;
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @Matching
void Cop_RelocatableModuleClear(void)
{
	CItem *pSearch = G_BADDY_LIST;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 320 ||
				pSearch->mType == 306)
			delete pSearch;

		pSearch = pNext;
	}
}

// @Ok
// @Matching
void Cop_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Cop_RelocatableModuleClear;
	pMod->field_C[0] = Cop_CreateCop;
}

// @Ok
CCopLaserPing::CCopLaserPing(
		CVector* a2,
		CVector* a3,
		CVector* a4,
		u8 a5,
		u8 a6,
		u8 a7) 
{
	this->field_88 = *a2;
	this->field_94 = *a3;
	this->field_A0 = *a4;
	this->field_84 = 0;
	this->SetTexture(10, 1);
	this->SetSemiTransparent();
	this->SetTint(a5, a6, a7);
	this->mType = 34;
}

// @Ok
CCopLaserPing::~CCopLaserPing(void)
{
}

// @Ok
// Verified field-for-field against the IDA decompile of 0x4292c0: PathCheck
// arg order (v2, v1, posBuf, 55), field_218/field_1F8/field_2A8 bit ops, the
// dividend/divisor selection (absDx vs absDz picks a3->vz or a3->vx), and
// both AddPointToPath fallbacks all match. Only difference is stack layout
// (own locals vs the original reusing the a2/a3 argument slots as scratch),
// which does not change behavior.
i32 CCop::WallHitCheck(CVector* a2, CVector* a3, i32 a4)
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
void CCop::Acknowledge(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->mCBodyFlags |= 0x10u;
			this->Neutralize();
			this->RunAnim(2, 0, -1);

			if ( Mem_RecoverPointer(&this->field_34C) )
			{
				new CAIProc_LookAt(
						this,
						reinterpret_cast<CBody*>(this->field_34C.pWhatever),
						0,
						2,
						70,
						200);
			}

			SFX_PlayPos(0x800Eu, &this->mPos, 0);
			this->dumbAssPad++;
			break;
		case 1:
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
INLINE void CCop::CheckToShoot(i32 a2, i32 a3)
{
	if ( G_MECHLIST_PLAYER->field_57C && !G_COP_LIST && !G_MECHLIST_PLAYER->mHeldObject)
	{
		if ( ((this->field_218 & 0x800) && a2 < this->field_37C)
				||
			 (this->field_324 && a2 < 1500 && (a3 != -1 || this->PathCheck(&this->mPos, &G_MECHLIST_PLAYER->mPos, 0, 55))))
		{
			this->Neutralize();
			G_COP_LIST = this;
			this->field_31C.bothFlags = 9;
			this->dumbAssPad = 0;
		}
	}
}

// @Ok
// @Matching
void CCop::SetUpLaser(
		CGPolyLine** a2,
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

	u32 v6 = Utils_CrapDist(*a3, *a4);
	if (v6 >= 1000)
		(*a2)->mSegs->b = 0;
	else
	{
		i32 tmp = 1000 - v6;
		tmp *= 5;
		tmp <<= 6;
		(*a2)->mSegs->b = tmp >> 12;
	}

	(*a2)->SetStartAndEnd(a3, a4);
}

// @Ok
INLINE void CCop::DrawBarrelFlash(
		CVector *a2,
		CVector *a3,
		SLineInfo *a4,
		u8 a5,
		u8 a6,
		u8 a7)
{
	new CGlowFlash(a2, 5, a5, a6, a7, 32, 0, 0, 0, 0, 50, 20, 1, 20, 10, 40, 20, 10, 1);
	this->SetUpLaser(&this->field_380, a2, a3);
}

// @Ok
// @Test
void CCop::GetAttackPosition(CVector* pAttackerPos)
{
	*pAttackerPos = G_MECHLIST_PLAYER->mPos;
	if (!(this->field_390 & 1) && (this->field_390 & 2))
	{
		if (this->field_391 & 0xEE)
		{
			pAttackerPos->vx += (this->field_391 & 0xAA ? 0x12C000 : 0x190000) * (this->field_391 > 8 ? 0xFFFFFFFF : 1);
		}

		if (this->field_391 & 0xBB)
		{
			pAttackerPos->vz += (this->field_391 & 0x83 ? 1 : -1) * (this->field_391 & 0xAA ? 0x12C000 : 0x190000);
		}
	}
}

// @Ok
void CCop::SetParamByIndex(i32 Index, i32 Param)
{
	switch ( Index )
	{
		case 1:
			this->field_364 = Param;
			break;
		case 2:
			this->field_360 = Param;
			break;
		case 3:
			this->field_368 = Param;
			break;
		case 4:
			this->field_36C = Param;
			break;
		case 5:
			this->field_370 = Param;
			break;
		case 8:
			this->field_374 = Param;
			break;
		case 9:
			this->field_1FC = Param;
			break;
		case 10:
			this->field_37C = Param;
			break;
		case 11:
			if ( Param )
				this->field_218 |= 0x800;
			else
				this->field_218 &= 0xFFFFF7FF;
			break;
		default:
			print_if_false(0, "Unknown index in C_SET_PARAMETER_BY_INDEX");
			break;
	}
}

// @Ok
INLINE i32 CCop::TryAddingCollidePointToPath(CVector *a2)
{
	if (Utils_CrapDist(*a2, this->mPos) < 0x64)
		return 0;

	CVector v6 = *a2 - this->mPos;
	v6 >>= 12;
	v6 *= 3700;
	v6 += this->mPos;

	return this->AddPointToPath(&v6, 0);
}

// @Ok
// field_324 is CCop-specific (declared in cop.h, confirmed by
// RunToWhereTheActionIs), not a CBaddy field, so the nearest cop's field_324
// is checked directly here. Verified against the IDA decompile of 0x42b270:
// GetClosest(306,0) then GetClosest(320,0) fallback, then the nearest
// pointer's offset 0x324 gated the CMessage(this, nearest, 7, 0) construct.
void CCop::WarnOtherCops(void)
{
	CCop *nearest = reinterpret_cast<CCop*>(this->GetClosest(306, 0));
	if ( (nearest || ((nearest = reinterpret_cast<CCop*>(this->GetClosest(320, 0))) != 0))
			&& !nearest->field_324)
	{
		new CMessage(this, nearest, 7, 0);
	}
}

// @Ok
INLINE void CCop::StopShooting(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->RunAnim(12, 0, -1);
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
// Same shape as CThug::Grab/CRhino's CheckStateFlags callers: a per-species
// SStateFlags global, named gCopStateFlags to match gThugStateFlags
// (thug.cpp) / gRhinoStateFlags (rhino.cpp). Verified against the IDA
// decompile of 0x4296b0: CheckStateFlags(&unk_549220, 17) & 2, then
// AddPointToPath, then field_31C.bothFlags=20 / field_2A8|=0x40 /
// dumbAssPad=0 (field-store order differs, functionally identical).
u8 CCop::Grab(CVector* a2)
{
	if ( (this->CheckStateFlags(G_COP_STATE_FLAGS, 17) & 2)
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
void CCop::CreateCombatImpactEffect(CVector *a2, i32)
{
	new CCombatImpactRing(a2, 0x6Cu, 108, 18, 384, 1792, 144);
	new CCombatImpactRing(a2, 0x90u, 144, 72, 192, 896, 72);
}

// @Ok
void CCop::DieAfterFlyingAcrossRoom(void)
{
	if (this->ShouldFall(200, 0x5F000))
	{
		this->field_218 &= 0xFFFFFFFD;
		this->field_31C.bothFlags = 22;
	}
	else
	{
		this->PlayHitWallSound();
		this->SetHeight(1, 100, 600);
		this->field_31C.bothFlags = 26;
	}

	this->dumbAssPad = 0;
}

// @Ok
INLINE void CCop::StandStill(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->CycleAnim(17, 1);
			this->dumbAssPad++;

			break;
		default:
			print_if_false(0, "Unknown substate.");
			break;
	}
}

// @Ok
void CCop::PlayHitWallSound(void)
{
	if ( !this->field_1F8 )
	{
		if ( (this->field_218 & 0x400) != 0 )
			SFX_PlayPos(0x802Fu, &this->mPos, 0);
		else
			SFX_PlayPos(0x802Eu, &this->mPos, 0);
	}
}

// @Ok
INLINE void CCop::SetAttacker(void)
{
	if (G_COP_GLOBAL != this)
	{
		this->ClearAttackFlags();
		if (G_COP_GLOBAL)
		{
			G_COP_GLOBAL->ClearAttackFlags();
		}

		G_COP_GLOBAL = this;
		this->field_390 = 1;
	}
}

// @Ok
// Reconstructed from IDA decompile of 0x42efc0. Same shape as the already-
// decompiled CThug::RunToWhereTheActionIs (thug.cpp): builds a point 100
// units past the target (away from us) on x and z, keeping our own y. Tries
// the direct path to MechList first, then the far point, then (if the far
// point is blocked) falls back to CCop::TryAddingCollidePointToPath, which
// this function inlines by hand in the original (confirmed field-for-field
// against the already-decompiled TryAddingCollidePointToPath body above).
// CCop uses field_324 (CCop-specific, cop.h) where CThug uses field_330 for
// the same role; field_2F0/field_2A8/field_31C/dumbAssPad are shared CBaddy
// fields at identical offsets in both classes.
void CCop::RunToWhereTheActionIs(CVector* a2)
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
		this->field_324 = 60;
		this->field_31C.bothFlags = 2;
		this->dumbAssPad = 0;
	}
}

// @Ok
void CCop::HelpOutBuddy(CMessage *pMessage)
{
	if (this->field_31C.bothFlags == 2 || this->field_31C.bothFlags == 1)
	{
		CItem *pItem = reinterpret_cast<CItem*>(Mem_RecoverPointer(&pMessage->mHandle));

		if (pItem)
			this->RunToWhereTheActionIs(&pItem->mPos);
	}
}

// @Ok
INLINE i32 CCop::SpideyAnimUppercut(void)
{
	return G_MECHLIST_PLAYER->mAnim == 106
		|| G_MECHLIST_PLAYER->mAnim == 113
		|| G_MECHLIST_PLAYER->mAnim == 284;
}

// @Ok
// field_380's type (CGPolyLine*) was already established by SetUpLaser above
// (this->SetUpLaser(&this->field_380, ...)); the delete here goes through
// CGPolyLine's (inherited CBit) virtual destructor, matching the original's
// vtable-slot-0 scalar-deleting-destructor call. Verified against the IDA
// decompile of 0x428980: field_380 delete, field_384 Mem_Delete, gCopList
// check, then ClearAttackFlags's body inlined verbatim, then DeleteFrom.
// Bug fixed here: DeleteFrom must take the address of the list head
// (&BaddyList, matching every other DeleteFrom(&BaddyList) call in the
// repo), not the value of BaddyList.
CCop::~CCop(void)
{
	if (this->field_380)
		delete this->field_380;
	this->field_380 = 0;

	if (this->field_384)
		Mem_Delete(static_cast<void*>(this->field_384));
	this->field_384 = 0;

	if (G_COP_LIST == this)
		G_COP_LIST = 0;

	this->ClearAttackFlags();
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
}

// @Ok
i32 CCop::GetLaunched(CVector* a2, i32 a3, i32 a4, i32 a5)
{
	CVector v13;
	v13.vx = 0;
	v13.vy = 0;
	v13.vz = 0;

	v13 = this->mPos;
	v13.vx += a2->vx * (a4 + 1);
	v13.vz += a2->vz * (a4 + 1);

	i32 v10 = this->WallHitCheck(&v13, a2, a4);
	if (v10 == 3)
		return 0;

	this->Neutralize();
	this->mVel = *a2;
	this->mVel.vy = 0;
	if (!a3 || v10 == 2)
	{
		new CAIProc_LookAt(this, 0, &v13, 0, 80, 200);
	}

	return 10;
}

// @Ok
u8 CCop::TugImpulse(
		CVector* a2,
		CVector* a3,
		CVector* a4)
{
	this->field_2A8 &= ~8;

	if (a4)
	{
			this->field_31C.bothFlags = 16;
			this->dumbAssPad = 0;

			if ( this->field_384 )
				Mem_Delete(this->field_384);

			this->field_384 = a4;
			return 1;
	}

	i32 Launched = this->GetLaunched(a3, 0, 80, 0);
	if (!Launched)
	{
		return 0;
	}

	if ( this->mHealth <= 50 )
	{
		if ( Launched == 1 )
			this->field_2A8 |= 0x10;
		else
			this->field_2A8 &= 0xFFFFFFEF;
	}

	this->field_31C.bothFlags = 14;
	this->field_218 |= 0x4000;
	this->dumbAssPad = 0;
	return 1;
}

// @Ok
void CCop::LookForPlayer(void)
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
					this->field_324 = 0;
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
// @Matching
void CCopBulletTracer::Move(void)
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
void CCopBulletTracer::SetWidth(void)
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
// @AlmostMatching: last loop slight different but overall awesome
CCopBulletTracer::CCopBulletTracer(
		CVector* a2,
		CVector* a3,
		CSuper* pSuper,
		SLineInfo* pLineInfo,
		u8 a6,
		u8 a7,
		u8 a8)
{
	print_if_false(!pSuper || !pLineInfo, "pSuper and pLineInfo are both non-NULL in call to CCopBulletTracer");

	if (pLineInfo)
		CreateCopRicochet(pLineInfo, a6, a7, a8);

	if (pSuper)
	{
		SHook v27;
		v27.Part.vx = 0;
		v27.Part.vy = 0;
		v27.Part.vz = 0;

		if (Web_CollideWithSuper(pSuper, a2, a3, &v27, 4096))
		{
			new CCopPing(pSuper, &v27);
		}

		SFX_PlayPos((Rnd(2) + 37) | 0x8000, &this->mPos, 0);
	}

	CVector v37 = *a2;
	u32 v18 = Utils_Dist(v37, *a3);

	if (v18 > 0x3E8)
	{
		v37 = *a3 + (((v37 - *a3) / v18) * 1000);
	}

	this->mpRibbon = new CGouraudRibbon(5, 0);
	this->mpRibbon->mProtected = 1;
	this->mpRibbon->SetRGB(a6, a7, a8);

	this->mpRibbon2 = new CGouraudRibbon(5, 0);
	this->mpRibbon2->mProtected = 1;
	this->mpRibbon->SetRGB(255, 255, 255);

	this->mMaxWidth = 3;

	this->SetWidth();

	CVector v40 = *a3 - v37;
	for (i32 i = 0; i < 5; i++)
	{
		CVector fV = v37 + ((i * v40) / 4);

		this->mpRibbon->mpPoints[i].Pos = fV;
		this->mpRibbon2->mpPoints[i].Pos = this->mpRibbon->mpPoints[i].Pos;
	}

}

// @Ok
CCopBulletTracer::~CCopBulletTracer(void)
{
	delete this->mpRibbon;

	delete this->mpRibbon2;

	this->mpRibbon = 0;
	this->mpRibbon2 = 0;
}

// @Ok
CCopPing::CCopPing(CSuper* pHost, SHook* pHook)
	: CSimpleAnim(&ZeroVector, 10, 0, 1, 0, -1)
{
	this->field_78.Part.vx = 0;
	this->field_78.Part.vy = 0;
	this->field_78.Part.vz = 0;

	print_if_false(pHost != 0, "NULL pHost");
	print_if_false(pHook != 0, "NULL pHook");

	this->field_78 = *pHook;
	this->field_70 = Mem_MakeHandle(pHost);

	this->SetFrame(1);
	this->SetTint(128, 128, 128);
	this->mAngle = Rnd(1024);

	if (Rnd(2))
		this->mAngle *= -1;

	this->mScale = 400;
	this->SetPosition();
}

// @Ok
CCopPing::~CCopPing(void)
{
}

// @Ok
CCop::CCop(i16* a2, i32 a3)
{
	i16 *v5 = this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2)));
	this->ShadowOn();
	this->mShadowScale = 50;
	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));


	this->field_230 = 0;
	this->field_1F4 = a3;
	this->mNode = a3;

	this->field_216 = 32;
	this->mPushVal = 64;
	this->field_31C.bothFlags = 0;
	this->mRMinor = this->mType != 306 ? 150 : 96;

	this->field_2A8 |= 1;

	this->field_370 = this->mType != 306 ? 400 : 300;
	this->field_340 = G_ATTACK_RELATED;
	this->field_360 = 3500;
	this->field_364 = 400;
	this->field_368 = 2047;
	this->field_36C = 100;
	this->field_374 = 10;
	this->field_1FC = 10;
	this->field_37C = 2000;

	this->ParseScript(reinterpret_cast<u16*>(v5));
	this->field_212 = 60;
}

// @Ok
void Cop_CreateCop(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(*stack);
	i32 v3 = static_cast<i32>(stack[1]);

	*result = reinterpret_cast<u32>(new CCop(v2, v3));
}

// @Ok
void CCop::Victorious(void)
{
	SFX_PlayPos(0x8024, &this->mPos, 0);
}

// @Ok
void INLINE CCopPing::SetPosition(void)
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
void CCopPing::Move(void)
{
	this->SetPosition();
	Bit_ReduceRGB(&this->mCodeBGR, 7);

	if ((this->mCodeBGR & 0xFFFFFF) == 0)
		this->Die();
}

// @Ok
// Same shape as the already-verified CThug::ClearAttackFlags (thug.cpp):
// gCopGlobal/gAttackFlagRelated here play the role of
// gGlobalThug/gAttackFlagRelated there. Also confirmed inlined verbatim
// inside the original ~CCop (0x428980) and matches this file's own
// SetAttacker, which calls it on both this and the previous gCopGlobal.
void CCop::ClearAttackFlags(void)
{
	if (G_COP_GLOBAL == this)
	{
		G_COP_GLOBAL = NULL;
	}
	else if((this->field_390 & 2))
	{
		G_ATTACK_FLAG_RELATED &= ~this->field_391;
	}

	this->field_390 = 0;
	this->field_391 = 0;
}

// @Ok
// Reconstructed from IDA decompile of 0x428c80 (register-level trace of the
// gte_ldopv1/gte_ldopv2/gte_op12/gte_stlvnl calls, misdecompiled by
// Hex-Rays as qt_register_signal_spy_callbacks, same class of false-positive
// signature match noted elsewhere in this repo, e.g. chopper.cpp). Builds
// the line's incoming direction (StartCoords - EndCoords, shifted and
// normalized), reflects it off the surface normal, then picks a vector
// perpendicular to the reflection (smallest-abs-component axis trick) and
// its GTE cross product with the reflection, and fires two CCopLaserPing
// sparks off the line's Position: one using the perpendicular vector, one
// using the cross product. Simplified the original's nested-if/goto
// perpendicular-axis selection into equivalent if/else (verified truth
// table by hand: the two forms agree on every branch).
void CreateCopRicochet(SLineInfo *pLine, u8 a2, u8 a3, u8 a4)
{
	SFX_PlayPos((Rnd(2) + 39) | 0x8000, &pLine->Position, 0);

	CVector dir = pLine->StartCoords - pLine->EndCoords;
	dir >>= 12;
	VectorNormal(reinterpret_cast<VECTOR*>(&dir), reinterpret_cast<VECTOR*>(&dir));

	i32 dot2 = 2 * ((dir.vx * pLine->Normal.vx + dir.vy * pLine->Normal.vy + dir.vz * pLine->Normal.vz) >> 12);

	CVector reflect;
	reflect.vx = ((dot2 * pLine->Normal.vx) >> 12) - dir.vx;
	reflect.vy = ((dot2 * pLine->Normal.vy) >> 12) - dir.vy;
	reflect.vz = ((dot2 * pLine->Normal.vz) >> 12) - dir.vz;

	i32 absX = my_abs(reflect.vx);
	i32 absY = my_abs(reflect.vy);
	i32 absZ = my_abs(reflect.vz);

	CVector perp;
	if (absY >= absX)
	{
		if (absX <= absZ)
		{
			perp.vx = 0;
			perp.vy = -reflect.vz;
			perp.vz = reflect.vy;
		}
		else
		{
			perp.vx = -reflect.vy;
			perp.vy = reflect.vx;
			perp.vz = 0;
		}
	}
	else if (absY > absZ)
	{
		perp.vx = -reflect.vy;
		perp.vy = reflect.vx;
		perp.vz = 0;
	}
	else
	{
		perp.vx = reflect.vz;
		perp.vy = 0;
		perp.vz = -reflect.vx;
	}

	gte_ldopv1(reinterpret_cast<VECTOR*>(&reflect));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&perp));
	gte_op12();

	new CCopLaserPing(&pLine->Position, &reflect, &perp, a2, a3, a4);

	CVector cross;
	gte_stlvnl(reinterpret_cast<VECTOR*>(&cross));

	new CCopLaserPing(&pLine->Position, &reflect, &cross, a2, a3, a4);
}

void validate_CCop(void){
	VALIDATE_SIZE(CCop, 0x394);

	VALIDATE(CCop, field_324, 0x324);

	VALIDATE(CCop, field_340, 0x340);

	VALIDATE(CCop, field_34C, 0x34C);

	VALIDATE(CCop, field_360, 0x360);
	VALIDATE(CCop, field_364, 0x364);
	VALIDATE(CCop, field_368, 0x368);
	VALIDATE(CCop, field_36C, 0x36C);
	VALIDATE(CCop, field_370, 0x370);
	VALIDATE(CCop, field_374, 0x374);

	VALIDATE(CCop, field_37C, 0x37C);

	VALIDATE(CCop, field_380, 0x380);
	VALIDATE(CCop, field_384, 0x384);

	VALIDATE(CCop, field_390, 0x390);
	VALIDATE(CCop, field_391, 0x391);
}

void validate_CCopPing(void)
{
	VALIDATE_SIZE(CCopPing, 0x80);

	VALIDATE(CCopPing, field_70, 0x70);
	VALIDATE(CCopPing, field_78, 0x78);
}

void validate_CCopBulletTracer(void)
{
	VALIDATE_SIZE(CCopBulletTracer, 0x48);

	VALIDATE(CCopBulletTracer, mpRibbon, 0x3C);
	VALIDATE(CCopBulletTracer, mpRibbon2, 0x40);
	VALIDATE(CCopBulletTracer, mMaxWidth, 0x44);
}

void validate_CCopLaserPing(void)
{
	VALIDATE_SIZE(CCopLaserPing, 0xAC);

	VALIDATE(CCopLaserPing, field_84, 0x84);

	VALIDATE(CCopLaserPing, field_88, 0x88);
	VALIDATE(CCopLaserPing, field_94, 0x94);
	VALIDATE(CCopLaserPing, field_A0, 0xA0);
}

#include "my_patch.h"

// @Bogus
// Left out on purpose:
//   CCop::CCop (0x00428750) stays in the exe. The standalone cop now has
//     its original SetCopType slot (0x004288D0) for police and SWAT setup.
//   CCopLaserPing::CCopLaserPing (0x00428A50). Our CCopLaserPing has no Move,
//     so it inherits CQuadBit's, while the exe's vtable (0x0053B6B8) points
//     slot 1 at CCopLaserPing::Move (0x00428B90), the function that ages the
//     ping and kills it after 4 steps. Hooking the constructor would leave
//     every ricochet spark alive forever.
//   Everything else in the file with no address of its own (CheckToShoot,
//     DrawBarrelFlash, StopShooting, StandStill, SetAttacker,
//     SpideyAnimUppercut, TryAddingCollidePointToPath, ClearAttackFlags,
//     HelpOutBuddy, GetAttackPosition, PlayHitWallSound,
//     DieAfterFlyingAcrossRoom, CCopPing::SetPosition) was inlined by the
//     original compiler and has nothing to patch.
void patch_cop(void)
{
	PATCH_PUSH_RET(0x00428080, Cop_RelocatableModuleInit);
	PATCH_PUSH_RET(0x004280A0, Cop_RelocatableModuleClear);
	PATCH_PUSH_RET(0x004280D0, Cop_CreateCop);
	PATCH_PUSH_RET(0x00428C80, CreateCopRicochet);
	PATCH_PUSH_RET(0x004292C0, CCop::WallHitCheck);
	PATCH_PUSH_RET(0x004295D0, CCop::GetLaunched);
	PATCH_PUSH_RET(0x0042B270, CCop::WarnOtherCops);
	PATCH_PUSH_RET(0x0042B690, CCop::SetUpLaser);
	PATCH_PUSH_RET(0x0042E950, CCop::Acknowledge);
	PATCH_PUSH_RET(0x0042EFC0, CCop::RunToWhereTheActionIs);
	PATCH_PUSH_RET(0x0042F1C0, CCop::LookForPlayer);
	// the linker folded CThugBulletTracer::SetWidth into this one, so the
	// single copy sits in thug.cpp's address range.
	PATCH_PUSH_RET(0x004D2660, CCopBulletTracer::SetWidth);

	PATCH_PUSH_RET_POLY(0x00428150, CCopPing::CCopPing, "??0CCopPing@@QAE@PAVCSuper@@PAUSHook@@@Z");
	PATCH_PUSH_RET_POLY(0x00428290, CCopPing::~CCopPing, "??1CCopPing@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004282A0, CCopPing::Move, "?Move@CCopPing@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x004282F0, CCopBulletTracer::CCopBulletTracer, "??0CCopBulletTracer@@QAE@PAVCVector@@0PAVCSuper@@PAUSLineInfo@@EEE@Z");
	PATCH_PUSH_RET_POLY(0x00428630, CCopBulletTracer::~CCopBulletTracer, "??1CCopBulletTracer@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004286B0, CCopBulletTracer::Move, "?Move@CCopBulletTracer@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x00428980, CCop::~CCop, "??1CCop@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x00428B80, CCopLaserPing::~CCopLaserPing, "??1CCopLaserPing@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004294E0, CCop::SetParamByIndex, "?SetParamByIndex@CCop@@UAEXHH@Z");
	PATCH_PUSH_RET_POLY(0x004296B0, CCop::Grab, "?Grab@CCop@@UAEEPAVCVector@@@Z");
	PATCH_PUSH_RET_POLY(0x00429710, CCop::TugImpulse, "?TugImpulse@CCop@@UAEEPAVCVector@@00@Z");
	// the exe uses this one body for CCop's, CThug's and CThugPing's
	// Victorious slot, so this hook covers all three, same as the original.
	PATCH_PUSH_RET_POLY(0x0042BFA0, CCop::Victorious, "?Victorious@CCop@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x0042FF10, CCop::CreateCombatImpactEffect, "?CreateCombatImpactEffect@CCop@@UAEXPAVCVector@@H@Z");
}
