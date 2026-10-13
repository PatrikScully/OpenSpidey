#include <cstdlib>

#include "game/objects/powerup.h"
#include "compatibility/my_patch.h"
#include "engine/assets/spool.h"
#include "engine/world/trig.h"
#include "game/effects/exp.h"
#include "compatibility/my_assert.h"
#include "core/utils.h"

#include "compatibility/validate.h"
#include "engine/world/bit.h"
#include "game/player/spidey.h"
#include "game/ui/front.h"
#include "engine/audio/ps2redbook.h"
#include "engine/audio/ps2lowsfx.h"

#ifndef SPIDEY_STANDALONE
CBody* PowerUpList;
#else
extern CBody* PowerUpList;
#endif

// @Ok
// @Matching
void CPowerUp::DoPhysics(void)
{
	this->mAngles.vy = (this->mAngles.vy + this->mAngVel.vy * (i16)this->mElapsedTicks) & 0xFFF;

	if (this->field_103)
	{
		if (!this->field_104 && this->field_10C < 0 && this->field_10C > -5)
		{
			if (G_TTIME & 1)
			{
				i32 height = Utils_GetGroundHeight(&this->field_110, 0, 0x1F40, 0);
				if (height == -1)
				{
					this->field_10C--;
					if (this->field_10C <= -5)
					{
						this->field_104 = 1;
					}
					return;
				}

				this->field_10C = height;
				return;
			}
			return;
		}

		CVector* pVel = &this->mVel;
		CVector* pWork = &this->field_110;
		*pWork += *pVel;

		this->mVel.vy += this->mAcc.vy;
		this->mVel.vy -= this->mVel.vy >> this->mFric.vy;

		if (!this->field_104)
		{
			i32 threshold = this->field_10C - (this->field_105 << 12);
			if (this->field_110.vy > threshold && this->mVel.vy > 0)
			{
				this->field_110.vy = threshold;
				pVel->vx = 0;
				pVel->vy = 0;
				pVel->vz = 0;
				this->field_103 = 0;
			}
		}

		this->mPos = this->field_110;
		return;
	}

	this->mPos = this->field_110;
}

// @Ok
void CPowerUp::AI(void)
{
	if ( (this->mFlags & 1) && this->mIs3d)
		this->mShadowScale = 0;
	else
		this->mShadowScale = 32;

	if (this->field_124)
	{
		this->mRGB = 0x101010;
		this->mFlags |= 0xC00;
		this->field_124 = 0;
	}
	else
	{
		this->mFlags &= 0xF3FF;
	}


	if (this->field_128)
	{
		if (this->field_128 > 2)
		{
			this->mFlags |= 0x400;

			this->field_128 -= 2;

			u32 tmp = (255 * this->field_128 / 64);
			tmp |= ((tmp << 8) | tmp) << 8;
			this->mRGB = tmp;
		}
		else
		{
			this->Die();
			return;
		}
	}

	this->CheckAge();

	if (this->mPlayerDist > 0x1F40)
	{
		this->DeleteStuff();
	}
	else
	{
		if (this->field_10C != 0xFFFFFFFF)
		{
			this->ShadowOn();
		}

		this->DoPhysics();

		if (!this->mIs3d)
		{
			if (!this->pPickupBit)
				this->CreateBit();
			DoAssert(this->pPickupBit != 0, "Non-3d pickup has no pickup bit");
			this->pPickupBit->SetPos(this->mPos);
		}
	}
}

// @Ok
void CPowerUp::CheckAge(void)
{
	if (this->mLifetime != 0xFFFF)
	{
		if (this->mLifetime)
			this->mLifetime--;

		if (this->mLifetime < 0x3C)
			this->field_120 = this->mLifetime * this->field_11E / 60;

		if (this->mLifetime < 0x3C && this->mLifetime > 0x1E)
		{
				if (G_TTIME & 2)
					this->mDropping = 1;
				else
					this->mDropping = 0;
		}
		if (this->mLifetime <= 0x1E)
		{
			if (G_TTIME & 1)
				this->mDropping = 1;
			else
				this->mDropping = 0;
		}

		if (this->mIs3d)
		{
			if (this->mDropping)
				this->mFlags &= 0xFFFE;
			else
				this->mFlags |= 1;
		}

		if (!this->mLifetime)
		{
			if (this->mPlayerDist <= 0x1F40)
				Exp_GlowFlash(&this->mPos, 20, 0xFF, 0xFF, 0xFF, 4, 1, 20);
			this->Die();
		}
	}
}

// @Ok
CPowerUp::~CPowerUp(void)
{
	this->DeleteFrom(&G_POWER_UP_LIST);
	this->DeleteStuff();
}

// @Ok
void CPowerUp::Die(void)
{
	if (!this->IsDead())
	{
		if (this->mType != 11 && this->mHasNode)
		{
			Trig_SendPulse(Trig_GetLinksPointer(this->mNodeIndex));
		}

		if (this->field_108 != 0xFFFF)
		{
			u16 LinkInfo[2];
			LinkInfo[0] = 1;
			LinkInfo[1] = this->field_108;

			Trig_SendSignalToLinks(LinkInfo);
		}

		this->mCBodyFlags |= 0x40;
		this->mFlags |= 1;
	}
}

// guess: current costume/tint index, used as the 2nd arg to
// Spool_GetModel below. Also referenced (same address) by
// M3d_RenderSetup and Init_AtStart, neither decompiled yet to confirm.
static u8 * const gCostumeId = (u8*)0x6B4678;

// guess: cheat/unlock bitmask, tested as (1 << mHealth) & this. Also
// referenced (same address) by ActivateCheat, PShell_MaybeUnlockStuff,
// Shell_ComicCollection and CPowerUp::TakeEffect (which uses G_SAVE_GAME.field_8C).
static i32 * const gCheatUnlockFlags = (i32*)0x6828E4;

// @Ok
// Verified 2026-08-31 against the real disassembly via IDA (function
// starts at 0x46AFC0, matches tools/functions/4632512.bin). The switch's
// jump table (jpt_46B1C7) and every case body were read directly from
// the binary, fixing two real bugs in the previous draft:
// (1) the switch writes byte [this+0x101], which is mIs3d, not mHasNode
// (mHasNode is [this+0x100] and is only ever set by SetNode elsewhere in
// this file; the constructor never touches it, so a fresh CPowerUp
// starts with mHasNode as whatever the allocator gave it).
// (2) the type->case mapping was wrong. Confirmed real mapping (switch
// key is mType-8, range 8..18): 8 -> mHealth=0x1000, model 0x17646B0D;
// 9 -> falls to default (invalid); 10 -> model 0xA092D785, then
// print_if_false(mLifetime<32,"Comic cover out of range"),
// mHealth=mLifetime, mLifetime=-1; 11 -> model 0x7F648179, no mHealth
// write; 12 -> model 0x12820A41, no mHealth write; 13 -> model
// 0xC6739C3B, no mHealth write; 14 -> mHealth=0x14, model 0x7E74F3D4;
// 15 -> mHealth=0x32, model 0x7E74F3D4; 16 -> mHealth=0x64, model
// 0x7E74F3D4; 17 -> falls to default (invalid); 18 -> mIs3d=0, and if
// mLifetime!=-1 then mHealth=mLifetime, mLifetime=-1 (no model call);
// default (includes 9, 17, and anything outside 8..18) ->
// print_if_false(0, "Bad powerup type"), no field writes at all.
// Also fixed the ground-height/shadow block: mShadowPos is only ever
// written on the else-branch path (flags with neither bit 0x10 nor bit
// 4 set) when Utils_GetGroundHeight did not return -1; the flags&0x10
// and flags&4 branches skip straight past it. The mType==18 ground-snap
// distance check (`abs(diff)<64`) only gates the snap when mType==18;
// for every other mType reaching that point the snap happens
// unconditionally (source is `if (mType != 18 || abs(diff) < 64)`,
// confirmed from the raw jnz at 0x46b16f: mType!=18 jumps straight to
// the snap block, skipping the abs/cmp entirely). Reproduced as found,
// not "fixed", per tips.txt.
// Confirmed helper identities via names.json: sub_460080=CBody::CBody,
// sub_4E5DA0=Rnd, sub_460560=ShadowOn, sub_4E6840=Utils_GetGroundHeight,
// sub_460570=KillShadow, sub_460020=InitItem, sub_4C93B0=Spool_GetModel,
// nullsub_1=print_if_false, sub_460260=AttachTo, sub_460700=IsDead,
// sub_4E3880=Trig_GetLinksPointer, sub_4DFD30=Trig_SendPulse,
// sub_4DFFE0=Trig_SendSignalToLinks. This is functional-decomp only
// (session bar): logic, offsets and signedness verified against the
// disassembly; not chased for a byte-identical build.
CPowerUp::CPowerUp(
		u16 a1,
		CVector* pos,
		CVector* vel,
		u32 flags,
		i32 a5,
		i32 a6)
{
	this->field_110.vx = 0;
	this->field_110.vy = 0;
	this->field_110.vz = 0;

	this->field_124 = 0;
	this->mType = a1;
	this->mLifetime = (u16)a5;
	this->field_108 = (u16)a6;
	this->field_105 = 0x80;
	this->mDropping = 1;

	this->field_110 = *pos;
	this->mPos = *pos;

	if (flags & 8)
	{
		this->mVel.vy = (i32)0xFFFC4000;
	}
	else
	{
		this->mVel = *vel;
	}

	this->mFric.vx = 5;
	this->mFric.vy = 5;
	this->mFric.vz = 5;

	this->mAngVel.vy = 0x18;
	this->mAcc.vy = 0x8000;

	this->field_122 = (i16)(Rnd(20) + 0x5A);

	this->field_11E = 0x10;
	this->field_120 = 0x10;
	this->mShadowScale = 0x20;

	this->ShadowOn();

	// mShadowPos and the mType==18 snap are only computed on the plain
	// else-branch (neither flags&0x10 nor flags&4) and only when the
	// ground height lookup succeeds; the other two branches jump straight
	// past all of it (confirmed from the raw jumps at 0x46b0ef/0x46b127,
	// which both go to 0x46b197, skipping the block that writes
	// mShadowPos entirely).
	if (flags & 0x10)
	{
		this->field_103 = 1;
		this->field_104 = 1;
	}
	else if (flags & 4)
	{
		this->field_103 = 1;
		i32 height = Utils_GetGroundHeight(&this->field_110, 0x200, 0x1F40, 0);
		this->field_10C = height;
		if (height == -1)
		{
			this->KillShadow();
		}
	}
	else
	{
		i32 height = Utils_GetGroundHeight(&this->field_110, 0x200, 0x1F40, 0);
		this->field_10C = height;
		if (height == -1)
		{
			this->KillShadow();
		}
		else
		{
			this->mShadowPos.vx = this->mPos.vx;
			this->mShadowPos.vy = height;
			this->mShadowPos.vz = this->mPos.vz;

			// for mType==18 the snap only happens when close to the ground;
			// for every other mType reaching here it always happens.
			if (this->mType != 18 || (i32)abs((this->mPos.vy - height) >> 12) < 0x40)
			{
				i32 snapped = height + (i32)0xFFFC0000;
				this->field_110.vy = snapped;
				this->mPos.vy = snapped;
			}
		}
	}

	this->mRMinor = 0x80;
	this->mShadowThreshold = 0x12C;

	this->InitItem("items");

	switch (this->mType)
	{
		case 8:
			this->mHealth = 0x1000;
			this->mIs3d = 1;
			this->mModel = (u16)Spool_GetModel(0x17646B0D, *gCostumeId);
			break;

		case 10:
			this->mIs3d = 1;
			this->mModel = (u16)Spool_GetModel(0xA092D785, *gCostumeId);
			print_if_false(this->mLifetime < 0x20, "Comic cover out of range");
			this->mHealth = this->mLifetime;
			this->mLifetime = 0xFFFF;
			break;

		case 11:
			this->mIs3d = 1;
			this->mModel = (u16)Spool_GetModel(0x7F648179, *gCostumeId);
			break;

		case 12:
			this->mIs3d = 1;
			this->mModel = (u16)Spool_GetModel(0x12820A41, *gCostumeId);
			break;

		case 13:
			this->mIs3d = 1;
			this->mModel = (u16)Spool_GetModel(0xC6739C3B, *gCostumeId);
			break;

		case 14:
			this->mHealth = 0x14;
			this->mIs3d = 1;
			this->mModel = (u16)Spool_GetModel(0x7E74F3D4, *gCostumeId);
			break;

		case 15:
			this->mHealth = 0x32;
			this->mIs3d = 1;
			this->mModel = (u16)Spool_GetModel(0x7E74F3D4, *gCostumeId);
			break;

		case 16:
			this->mHealth = 0x64;
			this->mIs3d = 1;
			this->mModel = (u16)Spool_GetModel(0x7E74F3D4, *gCostumeId);
			break;

		case 18:
			this->mIs3d = 0;
			if (this->mLifetime != 0xFFFF)
			{
				this->mHealth = this->mLifetime;
				this->mLifetime = 0xFFFF;
			}
			break;

		default:
			// mType 9 and 17 also land here (the jump table points them at
			// this same block), as well as anything outside 8..18.
			print_if_false(0, "Bad powerup type");
			break;
	}

	if (!this->mIs3d)
	{
		this->mFlags |= 1;
	}

	if (this->mType == 10)
	{
		if ((1 << this->mHealth) & *gCheatUnlockFlags)
		{
			if (!this->IsDead())
			{
				if (this->mType != 11 && this->mHasNode)
				{
					Trig_SendPulse(Trig_GetLinksPointer(this->mNodeIndex));
				}

				if (this->field_108 != 0xFFFF)
				{
					u16 LinkInfo[2];
					LinkInfo[0] = 1;
					LinkInfo[1] = this->field_108;
					Trig_SendSignalToLinks(LinkInfo);
				}

				this->mCBodyFlags |= 0x40;
				this->mFlags |= 1;
			}
		}
	}

	this->AttachTo(reinterpret_cast<CBody**>(&G_POWER_UP_LIST));
}

// @Ok
// @Matching
void CPowerUp::DeleteStuff(void)
{
	this->KillShadow();

	if (this->pPickupBit)
		delete this->pPickupBit;
	this->pPickupBit = 0;

	if (this->mpGlow)
		delete this->mpGlow;

	this->mpGlow = 0;
}

// @Ok
void CPowerUp::CreateBit(void)
{
	G_TOTALBITUSAGE = 0; 
	if (this->mType == 18)
	{
		this->pPickupBit = new CFlatBit();

		Texture *pTexture = Spool_FindTextureEntry("TrainingPick-Up_01");
		this->pPickupBit->SetTexture(pTexture);
		this->pPickupBit->SetSemiTransparent();

		switch (this->mHealth)
		{
			case 0xA:
				this->pPickupBit->SetTint(0x6Au, 212, 105);
				break;
			case 0x14:
				this->pPickupBit->SetTint(0x85u, 133, 255);
				break;
			case 0x28:
				this->pPickupBit->SetTint(0xFFu, 77, 77);
				break;
			case 0x50:
				this->pPickupBit->SetTint(0xD8u, 216, 216);
				break;
			default:
				this->pPickupBit->SetTint(0x80u, 128, 32);
				break;
		}
	}

	if (this->pPickupBit)
		this->pPickupBit->mProtected = 1;

	G_TOTALBITUSAGE = 1; 
}

// @Ok
void CPowerUp::SetGravity(i32 g, i32 fric)
{
	this->mAcc.vy = g;
	this->mFric.vy = fric;
}

// @Ok
// @Matching
void CPowerUp::SetNode(i32 NodeIndex)
{
	this->mHasNode = 1;
	this->mNodeIndex = NodeIndex;
}

// @Ok
// 0x0046B860. Applies a picked up power up to the player. mHealth is the
// amount (health percent, webbing, comic index...). The switch is on the
// type, 8 to 18, the case blocks are in the original's code order. The
// "already full" XA lines wait 300 ticks between two plays and never play
// while the redbook is busy. Type 11 (node pulse) does its own effect and
// never counts as taken.
// 2 mnemonic diffs left: in case 10 the original stores the unlock bit
// after it loads the redbook flag (0x46BA35 load, 0x46BA3C store), we store
// first. Tried (5): sfx uninitialised, single exit, case blocks in code
// order, case 11 through the common tail (these four got 166 -> 2), and the
// flag read into a local before the store (moves the load too early).
u8 CPowerUp::TakeEffect(CPlayer* pPlayer)
{
	u8 taken = 0;
	i32 sfx;

	switch (this->mType)
	{
		case 14:
		case 15:
		case 16:
			taken = pPlayer->IncHealth(this->mHealth * pPlayer->mMaxHealth / 100) != 0;
			sfx = 0x1D;
			if (!taken && !G_REDBOOK_BUSY
					&& static_cast<u32>(G_TIMER_RELATED - this->mLastXATime) > 300)
			{
				Redbook_XAPlay(0x20, 4, 0);
				this->mLastXATime = G_TIMER_RELATED;
			}
			break;

		case 8:
			taken = pPlayer->IncreaseWebbing(this->mHealth);
			sfx = 0x1E;
			if (!taken && !G_REDBOOK_BUSY
					&& static_cast<u32>(G_TIMER_RELATED - this->mLastXATime) > 300)
			{
				Redbook_XAPlay(0x20, Rnd(3) + 12, 0);
				this->mLastXATime = G_TIMER_RELATED;
			}
			break;

		case 12:
			taken = pPlayer->SetFireWebbing();
			sfx = 0x1E;
			break;

		case 13:
			if (Trig_GetLevelId() == 0x1601)
			{
				G_SAVE_GAME.field_80 |= 0x200;
				taken = 1;
			}
			else
			{
				taken = pPlayer->SetArmor(1);
				if (taken && !G_REDBOOK_BUSY
						&& static_cast<u32>(G_TIMER_RELATED - this->mLastXATime) > 300)
				{
					Redbook_XAPlay(0x20, Rnd(2), 0);
					this->mLastXATime = G_TIMER_RELATED;
				}
			}
			sfx = 0x1E;
			break;

		case 18:
			pPlayer->field_5D0 += this->mHealth;
			taken = 1;
			sfx = 0x1E;
			break;

		case 10:
			G_SAVE_GAME.field_8C |= 1 << static_cast<u8>(this->mHealth);
			taken = 1;
			sfx = 0x1F;
			if (!G_REDBOOK_BUSY
					&& static_cast<u32>(G_TIMER_RELATED - this->mLastXATime) > 300)
			{
				Redbook_XAPlay(0x20, 6, 0);
			}
			break;

		case 11:
			if (this->field_128 == 0 && this->mHasNode)
			{
				Trig_SendPulse(Trig_GetLinksPointer(this->mNodeIndex));
				this->Die();
				this->mHasNode = 0;
				SFX_Play(0x1F, 0x2000, 0);
			}
			Exp_GlowFlash(&this->mPos, 0xC0, 0x40, 0x40, 0xA0, 4, 1, 100);
			break;

		default:
			break;
	}

	if (taken && this->mType != 11)
	{
		this->Die();
		SFX_Play(sfx, 0x2000, 0);
	}
	return taken;
}

void validate_CPowerUp(void)
{
	VALIDATE_SIZE(CPowerUp, 0x138);

	VALIDATE(CPowerUp, mpGlow, 0xF8);

	VALIDATE(CPowerUp, mHasNode, 0x100);
	VALIDATE(CPowerUp, mIs3d, 0x101);
	VALIDATE(CPowerUp, mDropping, 0x102);

	VALIDATE(CPowerUp, field_103, 0x103);
	VALIDATE(CPowerUp, field_104, 0x104);
	VALIDATE(CPowerUp, field_105, 0x105);

	VALIDATE(CPowerUp, mNodeIndex, 0x106);
	VALIDATE(CPowerUp, field_108, 0x108);

	VALIDATE(CPowerUp, field_10C, 0x10C);

	VALIDATE(CPowerUp, field_110, 0x110);

	VALIDATE(CPowerUp, field_11E, 0x11E);
	VALIDATE(CPowerUp, field_120, 0x120);
	VALIDATE(CPowerUp, field_122, 0x122);
	VALIDATE(CPowerUp, field_124, 0x124);
	VALIDATE(CPowerUp, field_128, 0x128);

	VALIDATE(CPowerUp, mLifetime, 0x12C);
	VALIDATE(CPowerUp, mLastXATime, 0x134);

	VALIDATE_VTABLE(CPowerUp, DeleteStuff, 4);
}

// @Bogus
// Vtable check first: CPowerUp's vtable (0x53BC5C) has 5 entries (dtor,
// Die, AI, Hit, DeleteStuff, CItem's whole set), all covered by our class
// (dtor/Die/AI/DeleteStuff declared, Hit matches the shared default seen
// on every other class in this wave). No gap, so the constructor and
// destructor are hookable.
//
// PowerUpList/TTime both needed new G_* macros (added to powerup.h this
// session): CPowerUp objects are spawned from trig.cpp's level loader
// (`new CPowerUp(...)`, unhooked) and TTime's writer is main.cpp's Logic,
// which is fully decompiled but not yet hooked over the real game loop, so
// both sides could still be the one actually running. Every @Ok function
// in this file hooks cleanly once those point at game memory.
//
// Not implemented anywhere in this repo, found while mapping addresses:
// a free function named PowerUp_Create (0x46BD80, different from
// trig.cpp's own inline `new CPowerUp(...)` at its line 405, not
// investigated further).
void patch_powerup(void)
{
	PATCH_PUSH_RET_POLY(0x0046AFC0, CPowerUp::CPowerUp, "??0CPowerUp@@QAE@GPAVCVector@@0IHH@Z");
	PATCH_PUSH_RET_POLY(0x0046B660, CPowerUp::~CPowerUp, "??1CPowerUp@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x0046B450, CPowerUp::SetGravity, "?SetGravity@CPowerUp@@QAEXHH@Z");
	PATCH_PUSH_RET_POLY(0x0046B470, CPowerUp::CreateBit, "?CreateBit@CPowerUp@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x0046B600, CPowerUp::DeleteStuff, "?DeleteStuff@CPowerUp@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x0046B640, CPowerUp::SetNode, "?SetNode@CPowerUp@@QAEXH@Z");
	PATCH_PUSH_RET_POLY(0x0046B6F0, CPowerUp::Die, "?Die@CPowerUp@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x0046B760, CPowerUp::CheckAge, "?CheckAge@CPowerUp@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x0046BB20, CPowerUp::AI, "?AI@CPowerUp@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x0046BC30, CPowerUp::DoPhysics, "?DoPhysics@CPowerUp@@QAEXXZ");
}
