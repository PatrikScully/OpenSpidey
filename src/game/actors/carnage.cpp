#include "game/actors/carnage.h"
#include "compatibility/my_patch.h"
#include "compatibility/validate.h"
#include "engine/world/trig.h"
#include "game/ui/panel.h"
#include "engine/assets/spool.h"
#include "game/effects/effects.h"
#include "game/cinematics/camera.h"
#include "engine/rendering/ps2funcs.h"
#include "core/utils.h"
#include "compatibility/my_assert.h"
#include "engine/audio/ps2redbook.h"
#include "game/player/spidey.h"
#include "engine/world/m3dzone.h"
#include "engine/rendering/m3dutils.h"
#include "engine/rendering/scene_renderer.h"
#include "game/ui/message.h"
#include "game/player/web.h"
#include "engine/world/ai.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/audio/ps2redbook.h"
#include "engine/world/trig.h"

extern CBaddy *BaddyList;

// @Ok
EXPORT SLight M3d_CarnageLight =
{

  { { -2430, -2228, -2430 }, { 2509, -2896, 1447 }, { -648, -3711, -1607 } },

  0,
  { { 3200, 1040, 2048 }, { 2720, 1600, 1920 }, { 2400, 2560, 2048 } },
  0,
  { 1200, 1200, 960 }
};


// @Ok
EXPORT i32 gCarnageXa[4] = { 0x48, 5, 0x48, 5 };

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCarnageWhatIfXa[4] = { 0x11, 5, 0x11, 5};
#else
extern i32 gCarnageWhatIfXa[4];
#endif

// @Ok
EXPORT i32 gCarnageStretchJumpXaWhatIf[4] = { 0x11, 4, 0x11, 4};

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCarnageStretchJumpXa[4] = { 0x48, 4, 0x48, 4};
#else
extern i32 gCarnageStretchJumpXa[4];
#endif

// @Ok
EXPORT i32 gCarnageDoubleAxeWhatIfXa[4] = { 0x10, 2, 0x10, 3 };

// @Ok
EXPORT i32 gCarnageDoubleAxeXa[4] = { 0x47, 2, 0x47, 3 };

// @Ok
EXPORT i32 gCarnageStretchAdvXa[20] = 
{
	0x47, 0, 0x47, 1, 0x47, 8, 0x47, 9, 0x47, 0xC, 0x48, 1, 0x48,
	0x2, 0x48, 3, 0x48, 0x0D, 0x48, 0x0E
};

// @Ok
EXPORT i32 gCarnageStretchAdvWhatIfXa[20] =
{
	0x10, 1, 0x10, 0,
	0x10, 8, 0x10, 9,
	0x10, 0x0C, 0x11, 1,
	0x11, 2, 0x11, 3,
	0x11, 0x0A, 0x11, 0x0B
};

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCarnageBurnInBubbleWhatIfXa[32] =
{
	0x10, 0x4, 0x10, 0x5,
	0x10, 0x5, 0x4A, 0x0,
	0x4A, 0x1, 0x4A, 0x2,
	0x4A, 0x3, 0x4A, 0x4,
	0x4A, 0x5, 0x4A, 0x6,
	0x4A, 0x7, 0x4A, 0x8,
	0x4A, 0x0A, 0x4A, 0x0D,
	0x4A, 0x0E, 0x4A, 0x0F
};
#else
extern i32 gCarnageBurnInBubbleWhatIfXa[32];
#endif

// @Ok
EXPORT i32 gCarnageBurnInBubbleXa[32] = 
{
	0x47, 0x4, 0x47, 0x5,
	0x47, 0x6, 0x4A, 0x0,
	0x4A, 0x1, 0x4A, 0x2,
	0x4A, 0x3, 0x4A, 0x4,
	0x4A, 0x5, 0x4A, 0x6,
	0x4A, 0x7, 0x4A, 0x8,
	0x4A, 0x0A, 0x4A, 0x0D,
	0x4A, 0x0E, 0x4A, 0x0F
};

const u8 gCarnage10Two = 0x10;

const i32 gCarnageOne = 1;
const i32 gCarnageFour = 4;
const i32 gCarnage2 = 0x2;
const i32 gCarnage8 = 0x8;
const i32 gCarnage10 = 0x10;
const i32 gCarnage20 = 0x20;
const i32 gCarnage40 = 0x40;
const i32 gCarnage100 = 0x100;
const i32 gCarnage200 = 0x200;
const i32 gCarnage400 = 0x400;

#define NUM_CARNAGE_GOOS 19

// @Ok
// @Matching
#ifndef SPIDEY_STANDALONE
EXPORT SSkinGooSource gCarnageSkinGooSource[NUM_CARNAGE_GOOS] =
{
	{ 0x0F0E0A, 0x45F3EC38, 0x45F3EC38 },
	{ 0x1E1D0A, 0x45F3EC38, 0x45F3EC38 },
	{ 0x0A000D, 0x0D291D41B, 0x6CF38ACE },
	{ 0x0B050B, 0x0D291D41B, 0x6CF38ACE },
	{ 0x19160C, 0x0D291D41B, 0x6CF38ACE },
	{ 0x1C190C, 0x0D291D41B, 0x6CF38ACE },
	{ 0x131C0C, 0x0D291D41B, 0x6CF38ACE },
	{ 0x41108, 0x0D291D41B, 0x6CF38ACE },
	{ 0x42208, 0x0D291D41B, 0x6CF38ACE },
	{ 0x1C1D10, 0x0D291D41B, 0x6CF38ACE },
	{ 0x161910, 0x0D291D41B, 0x6CF38ACE },
	{ 0x0B0D0F, 0x0D291D41B, 0x6CF38ACE },
	{ 0x0F1311, 0x0D291D41B, 0x6CF38ACE },
	{ 0x151405, 0x0D291D41B, 0x6CF38ACE },
	{ 0x81405, 0x0D291D41B, 0x6CF38ACE },
	{ 0x40704, 0x0D291D41B, 0x6CF38ACE },
	{ 0x121302, 0x0D291D41B, 0x6CF38ACE },
	{ 0x51202, 0x0D291D41B, 0x6CF38ACE },
	{ 0x40701, 0x0D291D41B, 0x6CF38ACE },
};
#endif

// The exe still owns this table. CCarnageElectrified::Move (0x0041B910) is not in this repo and
// reads it every frame (push 548CC8h at 0x0041B986, push 548DB0h at 0x0041B97F, so the array
// really does start at 0x00548CC8 and hold 19 twelve byte entries), while our hooked
// SetTheCarnageGooSourcesChecksums writes the resolved texture checksums into it. Both halves
// have to see one copy, so the macro points at the exe's memory for now.
//#define G_CARNAGE_SKIN_GOO_SOURCE (gCarnageSkinGooSource)
#define G_CARNAGE_SKIN_GOO_SOURCE (reinterpret_cast<SSkinGooSource*>(0x00548CC8))

// @Ok
EXPORT CVector gCarnageVector;

// current item/model name pointer, read by many CItem-derived constructors right before their
// own InitItem call (IDA xrefs_to 0x6B4674: 12 hits, all InitItem-adjacent, CSymbioteBlade's
// ctor at 0x41AE40 included). Not in idb_globals.txt yet, tentative name.
static char ** const gCurrentItemName = (char**)0x006B4674;

// shared CBody linked list head, passed to CBody::AttachTo (ob.h) by many small
// effect/projectile object constructors (IDA xrefs_to 0x56EFE4: 15+ hits, CSymbioteBlade's
// ctor included). Not in idb_globals.txt yet, tentative name.
static CBody ** const gEffectBodyList = (CBody**)0x0056EFE4;

// region byte passed to Spool_GetModel from CSymbioteBlade::CSymbioteBlade. Already named this
// way in ps2m3d.cpp (M3d_PreprocessPulsingColours/M3d_PreprocessWibblyTextures); same address,
// duplicated per the repo's static-global convention.
static volatile u8 * const gM3dObjFileRegion = (u8*)0x006B3824;

// @Ok
// residue: field_138's optional trail sub-object (sub_4088A0/sub_410F50/sub_410E80, none
// decompiled) is skipped entirely (left null) rather than conditionally allocated -- see the
// @NotOk tag on the class declaration in carnage.h, which is the honest place to track that.
// Everything else here (the implicit CBody::CBody() base call, field_F8, mCurvePts[0]/[3], mPos,
// InitItem, mModel via Spool_GetModel, AttachTo, and the real GenerateControlPoints curve
// builder) is implemented directly off the 0x41AE40 disasm:
// - InitItem's name arg is *gCurrentItemName (read as a value, not the global's address).
// - the SFX/model index call (sub_4C93B0) is Spool_GetModel: same debug strings ("Bad region
//   number sent to Spool_GetModel", "Model checksum not found in call to Spool_GetModel")
//   as spool.cpp's already-implemented Spool_GetModel, called with checksum 0x90581B70 and
//   region *gM3dObjFileRegion, result stored into the inherited CItem::mModel.
// - AttachTo's list head is gEffectBodyList (address 0x56EFE4).
CSymbioteBlade::CSymbioteBlade(const CVector& a2, const CVector& a3)
{
	this->field_F8 = 0;

	for (i32 i = 0; i < 4; i++)
	{
		this->mCurvePts[i].vx = 0;
		this->mCurvePts[i].vy = 0;
		this->mCurvePts[i].vz = 0;
	}

	this->InitItem(*gCurrentItemName);
	this->mModel = static_cast<u16>(Spool_GetModel(0x90581B70, *gM3dObjFileRegion));

	this->AttachTo(gEffectBodyList);

	this->mCurvePts[0] = a2;
	this->mPos = a2;
	this->mCurvePts[3] = a3;

	this->GenerateControlPoints();

	// @FIXME the original conditionally builds an ~88 byte secondary trail sub-object here
	// (sub_4088A0/sub_410F50/sub_410E80, not decompiled) and stores its handle in field_138.
	this->field_138 = 0;
}

// @Ok
// sub_41AFF0, CSymbioteBlade::GenerateControlPoints(void) (name confirmed by
// idbs/spideypc_names.txt and the Mac symbol idbs/spiderman_names.txt). Builds the 4 cubic
// Bezier control points for the thrown blade's flight arc (mCurvePts[0]/[3] are already set by
// the caller to the start/end points; this fills mCurvePts[1]/[2] and the two step fields the
// sibling CSymbioteBlade::DerivePosition, 0x41B280, uses to walk the curve). See the long
// comment on the class declaration in carnage.h for the full instruction-by-instruction
// evidence trail (chord vector -> Utils_Dist/VectorNormal -> 1/3 and 2/3 points along the chord
// -> Utils_CalcPerps basis -> two random perpendicular offsets from Rnd()+rcossin_tbl, clamped
// so neither interior point's height (.vy) can exceed the end point's).
void CSymbioteBlade::GenerateControlPoints(void)
{
	CVector unitDir = (this->mCurvePts[3] - this->mCurvePts[0]) >> 12;
	VectorNormal(reinterpret_cast<VECTOR*>(&unitDir), reinterpret_cast<VECTOR*>(&unitDir));

	i32 dist = Utils_Dist(this->mCurvePts[0], this->mCurvePts[3]);

	this->mCurvePts[1] = this->mCurvePts[0] + unitDir * ((1365 * dist) >> 12);
	this->mCurvePts[2] = this->mCurvePts[0] + unitDir * ((2731 * dist) >> 12);

	this->mCurveNumSteps = dist / 32;
	this->mCurveStepDelta = 4096 / (this->mCurveNumSteps - 1);

	CVector perp2;
	CVector perp1;
	perp2.vx = 0; perp2.vy = 0; perp2.vz = 0;
	perp1.vx = 0; perp1.vy = 0; perp1.vz = 0;
	Utils_CalcPerps(&unitDir, &perp2, &perp1);

	CVector * pt = &this->mCurvePts[1];

	for (i32 i = 2; i != 0; i--)
	{
		i32 angle = Rnd(4096) & 0xFFF;
		i32 randMag = Rnd(dist / 2);

		i32 cosA = G_RCOSSIN_TBL[angle].cos;
		i32 sinA = G_RCOSSIN_TBL[angle].sin;

		CVector cosOffset = ((cosA * perp1) >> 12) * randMag;
		CVector sinOffset = ((sinA * perp2) >> 12) * randMag;

		*pt += sinOffset + cosOffset;

		if (pt->vy > this->mCurvePts[3].vy)
			pt->vy = this->mCurvePts[3].vy;

		pt++;
	}
}

// XA lines played while starting to be grabbed (CCarnage::GettingGrabbed case 0). Not in
// idb_globals.txt yet, but read straight out of the binary at 0x00548C34/0x00548C4C (IDA
// get_bytes), right after gCarnageBurnInBubbleWhatIfXa (0x548AF4) and before gCarnageStretchJumpXa
// (0x548C64). Previous values here were an unverified guess, now corrected.
EXPORT i32 gCarnageGettingGrabbedXa[6] = { 0x47, 0xD, 0x47, 0xE, 0x47, 0xF };
EXPORT i32 gCarnageGettingGrabbedWhatIfXa[6] = { 0x10, 0xD, 0x10, 0xE, 0x10, 0xF };

// XA line played once the grab lands and Carnage starts struggling (CCarnage::GettingGrabbed,
// the grab-succeeded branch). Read out of the binary at 0x00548CA4/0x00548CB4, right after
// gCarnageWhatIfXa (0x548C94), via IDA get_bytes. The source previously (wrongly) reused
// gCarnageXa/gCarnageWhatIfXa here; those are a different, already-named array (confirmed by
// content: gCarnageXa is {0x48,5,0x48,5}, this one is {0x48,0xA,0x48,0xC}).
EXPORT i32 gCarnageGrabbedHoldXa[4] = { 0x48, 0xA, 0x48, 0xC };
EXPORT i32 gCarnageGrabbedHoldWhatIfXa[4] = { 0x11, 0x7, 0x11, 0x9 };

// XA lines played while winding up to throw blades (CCarnage::ThrowBlades case 0). Not in
// idb_globals.txt yet, but read straight out of the binary at 0x00548C04/0x00548C1C (IDA
// get_bytes), right before gCarnageGettingGrabbedXa (0x548C34). Previous values here were an
// unverified guess, now corrected.
EXPORT i32 gCarnageThrowBladesXa[6] = { 0x47, 0xB, 0x4A, 0xB, 0x4A, 0xC };
EXPORT i32 gCarnageThrowBladesWhatIfXa[6] = { 0x10, 0xB, 0x4A, 0xB, 0x4A, 0xC };

// @Ok
// Found and fixed real bugs via IDA decompile+disasm of 0x41CFD0 (CCarnage_GettingGrabbed):
// - case 1 and case 2 bodies were swapped relative to the real switch(dumbAssPad). What used to be
//   written as case 1 (grab check, MonitorAttack, PlayXA) is really case 2; what used to be case 2
//   (mAnimFinished + angle copy) is really case 1. State machine correctness depends on the exact
//   numbers since dumbAssPad increments through them in order, so this was a real behavioural bug,
//   not cosmetic.
// - case 2's PlayXA (grab succeeded, RunAnim 0x22) used gCarnageXa/gCarnageWhatIfXa, the wrong
//   global (content {0x48,5,0x48,5}). The real array lives right after it in the binary at
//   0x548CA4/0x548CB4 ({0x48,0xA,0x48,0xC} / {0x11,7,0x11,9}), added as gCarnageGrabbedHoldXa/
//   gCarnageGrabbedHoldWhatIfXa. gCarnageGettingGrabbedXa/WhatIfXa (case 0) were also an unverified
//   guess, corrected to the real bytes at 0x548C34/0x548C4C.
// - case 3's Hit() call built field_C via (MechList->mPos - mPos) >> 12 then VectorNormal, but was
//   missing the explicit field_C.vy = 0 the original does before normalizing (horizontal-only hit
//   direction, same idea as the mFrame<0xA branch's delta.vy = 0).
// - case 3's sound-trigger and position-snap mFrame thresholds were 0xA, the original uses 0xC for
//   both (0xA is only correct for the later field_194 flip).
void CCarnage::GettingGrabbed(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
		{
			this->mVel.vz = 0;
			this->mVel.vy = 0;
			this->mVel.vx = 0;

			this->field_218 &= ~7;
			this->RunAnim(0x1Du, 0, -1);

			if (gWhatIf)
			{
				i32 idx = Rnd(6) & ~1;
				this->PlayXA(gCarnageGettingGrabbedWhatIfXa[idx], gCarnageGettingGrabbedWhatIfXa[idx + 1], 60);
			}
			else
			{
				i32 idx = Rnd(6) & ~1;
				this->PlayXA(gCarnageGettingGrabbedXa[idx], gCarnageGettingGrabbedXa[idx + 1], 60);
			}

			this->dumbAssPad++;
			break;
		}

		case 1:
		{
			if (this->mAnimFinished && G_MECHLIST_PLAYER->mActionState != 0x8000000)
				this->RunAnim(0x1Eu, 0, -1);

			this->mAngles.vy = G_MECHLIST_PLAYER->mAngles.vy;
			this->dumbAssPad++;
			break;
		}

		case 2:
		{
			typedef u8 (CPlayer::*GrabUpdateFn)(CVector*, i16*);
			union { GrabUpdateFn fn; u32 addr; } u;
			u.addr = 0x004BB810;
			u8 grabbed = (G_MECHLIST_PLAYER->*u.fn)(&this->mPos, &this->mAngles.vy);

			if (grabbed && (this->field_2A8 & 0x40))
			{
				if (G_MECHLIST_PLAYER->mActionState & 0x4000000)
				{
					if (this->mAnim != 0x1F)
						this->RunAnim(0x1F, 0, -1);
					return;
				}

				if (!this->mAnimFinished)
					return;

				if (G_MECHLIST_PLAYER->mActionState == 0x8000000)
					return;

				this->RunAnim(0x1E, 0, -1);
				return;
			}

			this->field_2A8 &= ~0x40;
			this->RunAnim(0x22u, 0, -1);

			this->field_324 = 0;
			new CAIProc_MonitorAttack(this, 0xD, 0x38000, 6, 0x10);

			if (gWhatIf)
			{
				i32 idx = Rnd(4) & ~1;
				this->PlayXA(gCarnageGrabbedHoldWhatIfXa[idx], gCarnageGrabbedHoldWhatIfXa[idx + 1], 60);
			}
			else
			{
				i32 idx = Rnd(4) & ~1;
				this->PlayXA(gCarnageGrabbedHoldXa[idx], gCarnageGrabbedHoldXa[idx + 1], 60);
			}

			this->dumbAssPad++;
			break;
		}

		case 3:
		{
			if (this->field_288 & 0x10)
			{
				this->field_288 &= ~0x10;

				SHitInfo hitInfo;
				hitInfo.field_C = (G_MECHLIST_PLAYER->mPos - this->mPos) >> 12;
				hitInfo.field_C.vy = 0;
				VectorNormal(reinterpret_cast<VECTOR*>(&hitInfo.field_C), reinterpret_cast<VECTOR*>(&hitInfo.field_C));
				hitInfo.field_0 = 0xE;
				hitInfo.field_4 = 0xB;
				hitInfo.field_8 = 0xC;

				G_MECHLIST_PLAYER->Hit(&hitInfo);
			}

			if (this->mFrame >= 0xC)
			{
				if (!(this->field_324 & 1))
				{
					this->field_324 |= 1;
					SFX_PlayPos((Rnd(6) + 0x1DC) | 0x8000, &this->mPos, 0);
				}
			}

			if (this->mFrame < 0xC)
			{
				CVector delta;
				delta.vx = this->mPos.vx - G_MECHLIST_PLAYER->mPos.vx;
				delta.vy = 0;
				delta.vz = this->mPos.vz - G_MECHLIST_PLAYER->mPos.vz;

				if (delta.Length() < 0x80)
				{
					CVector scaled = delta * 12;
					VectorNormal(reinterpret_cast<VECTOR*>(&scaled), reinterpret_cast<VECTOR*>(&scaled));
					CVector target = G_MECHLIST_PLAYER->mPos + scaled;
					this->mPos = target;
				}
			}

			if (this->mFrame > 0xA)
			{
				if (this->field_194 & 0x40000)
				{
					this->field_194 &= ~0x40000;
					this->field_194 |= 0x20000;
				}
			}

			if (!this->mAnimFinished)
				return;

			this->field_194 &= ~0x20000;
			this->field_194 |= 0x40000;
			this->RunAnim(0, 0, -1);

			this->field_31C.bothFlags = 2;
			this->dumbAssPad = 0;
			this->mAngles.vy = (this->mAngles.vy - 0x800) & 0xFFF;
			break;
		}

		default:
			DoAssert(0, "Unknown state");
			break;
	}
}

// @Ok
// Verified against IDA decompile of 0x41EA00 (CCarnage_ThrowBlades). All 6 states (mAnim/mFrame
// gates, hook offsets 0x11/0xD/0x11 for cases 0/2/3, shared SFX_PlayPos+dumbAssPad++ tail, the
// mActionState/angle-diff/Rnd(4) branch tree in case 4, the mAnimFinished checks in cases 1 and 5)
// already matched field for field. The one real bug: gCarnageThrowBladesXa/WhatIfXa (case 0's
// PlayXA, gated 60% by PlayXA's own Rnd(100)<=a4) were an unverified guess, corrected to the real
// bytes read from 0x548C04/0x548C1C via IDA get_bytes. Needs the CSymbioteBlade stub above (out of
// this file's assigned functions, only stubbed as a leaf callee).
void CCarnage::ThrowBlades(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
		{
			this->field_218 |= 4;

			if (this->mAnim != 0x25)
			{
				this->RunAnim(0x25u, 0, -1);

				if (gWhatIf)
				{
					i32 idx = Rnd(6) & ~1;
					this->PlayXA(gCarnageThrowBladesWhatIfXa[idx], gCarnageThrowBladesWhatIfXa[idx + 1], 60);
				}
				else
				{
					i32 idx = Rnd(6) & ~1;
					this->PlayXA(gCarnageThrowBladesXa[idx], gCarnageThrowBladesXa[idx + 1], 60);
				}

				return;
			}

			if (this->mFrame < 0xA)
				return;

			SHook hook;
			hook.Part.vx = 0;
			hook.Part.vy = 0;
			hook.Part.vz = 0;
			hook.Offset = 0x11;

			VECTOR hookPos;
			M3dUtils_GetDynamicHookPosition(&hookPos, this, &hook);

			new CSymbioteBlade(*reinterpret_cast<CVector*>(&hookPos), G_MECHLIST_PLAYER->mPos);

			SFX_PlayPos((Rnd(6) + 0x1DC) | 0x8000, &this->mPos, 0);

			this->dumbAssPad++;
			break;
		}

		case 1:
		{
			if (!this->mAnimFinished)
				return;

			this->RunAnim(5, 0, -1);
			this->dumbAssPad++;
			break;
		}

		case 2:
		{
			if (this->mFrame < 0xA)
				return;

			SHook hook;
			hook.Part.vx = 0;
			hook.Part.vy = 0;
			hook.Part.vz = 0;
			hook.Offset = 0xD;

			VECTOR hookPos;
			M3dUtils_GetDynamicHookPosition(&hookPos, this, &hook);

			new CSymbioteBlade(*reinterpret_cast<CVector*>(&hookPos), G_MECHLIST_PLAYER->mPos);

			SFX_PlayPos((Rnd(6) + 0x1DC) | 0x8000, &this->mPos, 0);

			this->dumbAssPad++;
			break;
		}

		case 3:
		{
			if (this->mFrame < 0x1A)
				return;

			SHook hook;
			hook.Part.vx = 0;
			hook.Part.vy = 0;
			hook.Part.vz = 0;
			hook.Offset = 0x11;

			VECTOR hookPos;
			M3dUtils_GetDynamicHookPosition(&hookPos, this, &hook);

			new CSymbioteBlade(*reinterpret_cast<CVector*>(&hookPos), G_MECHLIST_PLAYER->mPos);

			SFX_PlayPos((Rnd(6) + 0x1DC) | 0x8000, &this->mPos, 0);

			this->dumbAssPad++;
			break;
		}

		case 4:
		{
			if (!this->mAnimFinished)
				return;

			if (G_MECHLIST_PLAYER->mActionState & 0x800000)
			{
				this->field_218 &= ~4;
				this->RunAnim(0x26u, 0, -1);
				this->dumbAssPad++;
				break;
			}

			CSVector aim1;
			Utils_CalcAim(&aim1, &gCarnageVector, &this->mPos);

			CSVector aim2;
			Utils_CalcAim(&aim2, &gCarnageVector, &G_MECHLIST_PLAYER->mPos);

			i32 diff = aim2.vy - aim1.vy;
			if (diff > 0x800)
				diff -= 0x1000;
			else if (diff < -0x800)
				diff += 0x1000;

			if (my_abs(diff) < 0x17C && !G_MECHLIST_PLAYER->mIsCrawling)
			{
				this->RunAnim(0x26u, 0, -1);
				this->dumbAssPad++;
				break;
			}

			if (Rnd(4) != 0)
			{
				this->RunAnim(5, 0, -1);
				this->dumbAssPad -= 2;
			}
			else
			{
				this->field_218 &= ~4;
				this->dumbAssPad++;
			}
			break;
		}

		case 5:
		{
			if (!this->mAnimFinished)
				return;

			this->field_31C.bothFlags = 2;
			this->dumbAssPad = 0;
			break;
		}

		default:
			DoAssert(0, "Unknown state");
			break;
	}
}

// @Ok
// Verified against IDA decompile+disasm of 0x41C7F0 (CCarnage_AI). Fixed several real bugs:
// - the pending-message switch dispatched on msg->field_14 - 5 (grouping fake ranges 0/1, 2/3,
//   4/5/6). The original switches directly on msg->field_14 with exactly 4 handled values: 5, 6,
//   14 (0xE, the field_104/CTrapWebEffect burst, previously mis-attached to the wrong case), 15
//   (0xF, same for field_10C), everything else is a no-op (not the "bothFlags=0x100" catch-all
//   this had).
// - CleanUpMessages(0,0) and CleanUpAIProcList(0) were called unconditionally; the original only
//   calls them when there was a message list / an mAIProcList respectively (nested inside the
//   corresponding if).
// - the entire field_31C.bothFlags dispatch switch that calls into CheckSlideParams / Initialise /
//   SelectAttack / AxeHandSlash / DoubleAxeHandSlash / ThrowBlades / StretchJumpAdvance /
//   StretchJumpFlow / BurnInBubble / GettingGrabbed / Laugh / TugWebTrapped / GetYankedBySpidey /
//   TakeHit was missing entirely. bothFlags==0x800 (2048) is handled inline in the original with
//   the exact same shape as the already-@Ok CCarnage::DieCarnage() (mVel/field_218 reset + RunAnim
//   36 on dumbAssPad 0, PulseL8A5Node()+increment on dumbAssPad 1 once mAnimFinished), so this
//   calls DieCarnage() directly instead of duplicating that state machine.
// Residue: the wrap-bit tail (new CNonRenderedBit() path) is missing two field stores at offsets
// 60/64 into the 72-byte CNonRenderedBit object (a call to an unidentified helper, result and an
// unread local stored there) that this file has no declared struct for; left as-is, everything
// else in that tail (the field_328/field_80 threshold, the delete path) already matched.
void CCarnage::AI(void)
{
	if (this->field_340)
	{
		this->field_340 -= this->mElapsedTicks;
		if (this->field_340 < 0)
			this->field_340 = 0;
	}

	i32 state = this->field_31C.bothFlags;
	this->field_194 = 0x44000;

	print_if_false(1, "AI");

	if (state == 0x2000)
		G_CAMERA_LIST->field_2A8 = 0;
	else
		G_CAMERA_LIST->field_2A8 = 0x80;

	this->DoSonicBubbleProcessing();
	M3d_BuildTransform(this);
	this->DoPhysics();

	this->field_364 = this->mPos.vy + (this->field_21E << 12);
	this->DoMGSShadow();

	CMessage *msg = this->pMessage;
	if (msg)
	{
		do
		{
			i32 current = this->field_31C.bothFlags;
			if (current != 0x800)
			{
				switch (msg->field_14)
				{
					case 5:
						if (current != 0x80)
						{
							this->field_218 &= ~7;
							this->mVel.vz = 0;
							this->mVel.vy = 0;
							this->mVel.vx = 0;
							this->field_31C.bothFlags = 0x80;
							this->dumbAssPad = 0;
						}
						else
						{
							this->field_1F8 = 0;
							this->dumbAssPad = 2;
						}
						break;

					case 6:
						this->field_31C.bothFlags = 0x100;
						this->dumbAssPad = 0;
						break;

					case 14:
						if (this->field_104.pWhatever)
						{
							CTrapWebEffect *effect = reinterpret_cast<CTrapWebEffect*>(Mem_RecoverPointer(&this->field_104));
							if (effect)
								effect->Burst();
							this->field_104.pWhatever = 0;
						}
						break;

					case 15:
						if (this->field_10C.pWhatever)
						{
							CTrapWebEffect *effect = reinterpret_cast<CTrapWebEffect*>(Mem_RecoverPointer(&this->field_10C));
							if (effect)
								effect->Burst();
							this->field_10C.pWhatever = 0;
						}
						break;

					default:
						break;
				}
			}

			msg->field_10 |= 1;
			msg = msg->mNext;
		} while (msg);

		this->CleanUpMessages(0, 0);
	}

	if (this->mAIProcList)
	{
		this->mAIProcList->Execute();
		this->CleanUpAIProcList(0);
	}

	i32 flags = this->field_31C.bothFlags;
	if (flags <= 0x80)
	{
		switch (flags)
		{
			case 0x80: this->CheckSlideParams(); break;
			case 1: this->Initialise(); break;
			case 2: this->SelectAttack(); break;
			case 4: this->AxeHandSlash(); break;
			case 8: this->DoubleAxeHandSlash(); break;
			case 0x10: this->ThrowBlades(); break;
			case 0x20: this->StretchJumpAdvance(); break;
			case 0x40: this->StretchJumpFlow(); break;
			default: break;
		}
	}
	else if (flags > 0x800)
	{
		switch (flags)
		{
			case 0x1000: this->BurnInBubble(); break;
			case 0x2000: this->GettingGrabbed(); break;
			case 0x4000: this->Laugh(); break;
			default: break;
		}
	}
	else if (flags != 0x800)
	{
		switch (flags)
		{
			case 0x100: this->TugWebTrapped(); break;
			case 0x200: this->GetYankedBySpidey(); break;
			case 0x400: this->TakeHit(); break;
			default: break;
		}
	}
	else
	{
		this->DieCarnage();
	}

	if (this->field_328 > this->mElapsedTicks)
	{
		this->field_328 -= this->mElapsedTicks;

		if (!this->field_32C)
		{
			CNonRenderedBit *wrap = new CNonRenderedBit();
			if (wrap)
			{
				print_if_false(this != 0, "AI");
				print_if_false(this->mType == 0x13A, "AI");
				this->field_32C = wrap;
			}
			else
			{
				this->field_32C = 0;
			}
		}
	}
	else
	{
		this->field_328 = 0;

		if (this->field_32C)
		{
			delete reinterpret_cast<CNonRenderedBit*>(this->field_32C);
			this->field_32C = 0;
		}
	}
}

// @Ok
// Verified against IDA decompile of 0x41E0C0 (unnamed in names.json, between SelectAttack 0x41DB20
// and DoubleAxeHandSlash 0x41E6A0, called from AI's dispatch switch as the field_31C.bothFlags==4
// handler). realRegisterArr[0]/[1]/[2] (u16/u16/i16) are reused here as scratch state: [1] as a
// "already hit this swing" latch, [2] as a repeat-swing counter capped at 2.
void CCarnage::AxeHandSlash(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
		{
			this->RunAnim(0xCu, 0, -1);
			this->field_324 = 0;
			new CAIProc_MonitorAttack(this, 8, 0x58000, 8, 0x10);

			this->realRegisterArr[0] = 0;
			this->realRegisterArr[1] = 0;
			this->realRegisterArr[2] = 0;

			this->dumbAssPad++;
			break;
		}

		case 1:
		{
			this->field_194 = (this->field_194 & ~0x66000) | 0x22000;

			if (Utils_XZDist(&G_MECHLIST_PLAYER->mPos, &ZeroVector) < 700 && this->field_35C != 1)
				this->realRegisterArr[1] = 1;

			if (Utils_XZDist(&this->mPos, &G_MECHLIST_PLAYER->mPos) <= 150 || this->realRegisterArr[1])
			{
				this->field_218 &= ~1;
			}
			else
			{
				this->field_218 |= 1;

				CSVector dir;
				dir.vx = G_MECHLIST_PLAYER->mPos.vx - this->mPos.vx;
				dir.vy = 0;
				dir.vz = G_MECHLIST_PLAYER->mPos.vz - this->mPos.vz;
				VectorNormal(reinterpret_cast<VECTOR*>(&dir), reinterpret_cast<VECTOR*>(&dir));

				this->field_240.vx = G_MECHLIST_PLAYER->mPos.vx - 150 * dir.vx;
				this->field_240.vy = this->mPos.vy;
				this->field_240.vz = G_MECHLIST_PLAYER->mPos.vz - 150 * dir.vz;

				this->SnapArenaPosition(&this->field_240);
			}

			if (this->field_288 & 0x10)
			{
				this->field_288 &= ~0x10;
				this->realRegisterArr[1] = 1;

				SHitInfo hitInfo;
				hitInfo.field_C = (G_MECHLIST_PLAYER->mPos - this->mPos) >> 12;
				hitInfo.field_C.vy = 0;
				VectorNormal(reinterpret_cast<VECTOR*>(&hitInfo.field_C), reinterpret_cast<VECTOR*>(&hitInfo.field_C));
				hitInfo.field_0 = 0xE;
				hitInfo.field_4 = 0xB;
				hitInfo.field_8 = 0x1E;

				G_MECHLIST_PLAYER->Hit(&hitInfo);

				this->mVel.vz = 0;
				this->mVel.vy = 0;
				this->mVel.vx = 0;
				this->field_218 &= ~1;
			}

			i32 diff;
			if (G_MECHLIST_PLAYER->field_8E8 || G_MECHLIST_PLAYER->field_8E9)
			{
				diff = 0x600;
			}
			else
			{
				CSVector aim1;
				Utils_CalcAim(&aim1, &gCarnageVector, &this->mPos);
				CSVector aim2;
				Utils_CalcAim(&aim2, &gCarnageVector, &G_MECHLIST_PLAYER->mPos);

				diff = aim2.vy - aim1.vy;
				if (diff > 0x800)
					diff -= 0x1000;
				else if (diff < -0x800)
					diff += 0x1000;
			}

			if (my_abs(diff) > 512 && this->mAnimFinished)
			{
				this->mVel.vz = 0;
				this->mVel.vy = 0;
				this->mVel.vx = 0;
				this->field_218 &= ~1;
				this->RunAnim(0xEu, 0, -1);
				this->field_194 = (this->field_194 & ~0x66000) | 0x44000;
				this->dumbAssPad++;
				break;
			}

			if (this->mFrame >= 9 && !(this->field_324 & 1))
			{
				this->field_324 |= 1;
				SFX_PlayPos((Rnd(6) + 0x1DC) | 0x8000, &this->mPos, 0);
			}
			else if (this->mFrame >= 25 && !(this->field_324 & 2))
			{
				this->field_324 |= 2;
				SFX_PlayPos((Rnd(6) + 0x1DC) | 0x8000, &this->mPos, 0);
			}

			if (!this->mAnimFinished)
				break;

			if (this->realRegisterArr[1])
			{
				this->mVel.vz = 0;
				this->mVel.vy = 0;
				this->mVel.vx = 0;
				this->field_218 &= ~1;
				this->RunAnim(0xEu, 0, -1);
				this->field_194 = (this->field_194 & ~0x66000) | 0x44000;
				this->dumbAssPad++;
				break;
			}

			if (this->realRegisterArr[2] >= 2)
			{
				this->mVel.vz = 0;
				this->mVel.vy = 0;
				this->mVel.vx = 0;
				this->field_218 &= ~1;
				this->RunAnim(0xEu, 0, -1);
				this->field_194 = (this->field_194 & ~0x66000) | 0x44000;
				this->dumbAssPad++;
				break;
			}

			this->realRegisterArr[2]++;
			this->RunAnim(0xDu, 0, -1);
			this->field_324 = 0;
			this->field_194 = (this->field_194 & ~0x66000) | 0x22000;

			if (!this->realRegisterArr[0])
			{
				this->realRegisterArr[0] = 1;
				new CAIProc_MonitorAttack(this, 11, 0x5D800, 22, 0x10);
			}

			break;
		}

		case 2:
		{
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		}

		default:
			DoAssert(0, "Unknown state");
			break;
	}
}

// @Ok
// @Note: insane that my code is more optimzeid with all the mAccelor vector assingments
// @Test
void CCarnage::SelectAttack(void)
{
	i32 v8,v9;
	i32 v10, v11;
	switch (this->dumbAssPad)
	{
		case 0:
			if (this->field_35C == 4 &&
					Utils_XZDist(&this->mPos, &ZeroVector) < this->field_350)
			{
				this->field_31C.bothFlags = 4096;
				this->dumbAssPad = 0;
				break;
			}

			if (!G_MECHLIST_PLAYER->field_8E8 && !G_MECHLIST_PLAYER->field_8E9)
			{
				v10 = this->CalculateAngleDelta();
				v11 = my_abs(v10);

				if (v11 < 380)
				{
					if (G_MECHLIST_PLAYER->mActionState == 0x800000)
					{
						this->field_31C.bothFlags = 0x4000;
						this->dumbAssPad = 0;
					}
					else if (Utils_XZDist(&G_MECHLIST_PLAYER->mPos, &ZeroVector) >= 700 || this->field_35C == 1 )
					{
						if (Utils_XZDist(&this->mPos, &G_MECHLIST_PLAYER->mPos) < 180)
							this->field_31C.bothFlags = 8;
						else
							this->field_31C.bothFlags = 4;

						this->dumbAssPad = 0;
					}
					else
					{
						this->field_31C.bothFlags = 0x4000;
						this->dumbAssPad = 0;
					}



					break;
				}
				else if (v11 < 1000)
				{
					if (this->field_218 & 0x100)
					{
						this->mVel.vz = 0;
						this->mVel.vy = 0;
						this->mVel.vx = 0;

						this->field_218 &= 0xFFFFFEFE;
						this->field_31C.bothFlags = 16;
						this->dumbAssPad = 0;
					}
					else
					{
						this->GetArenaPositionFromAngleOffset(
								v10 < 0 ? -190 : 190,
								&this->field_240);
						this->field_218 |= gCarnageOne;
						if (this->mAnim != 2)
							this->RunAnim(1, 0, -1);
						this->dumbAssPad++;
					}
					break;
				}
				else if (v11 < 1536)
				{
					if ( (this->field_218 & 0x100) | Rnd(2))
					{
						this->mVel.vz = 0;
						this->mVel.vy = 0;
						this->mVel.vx = 0;

						this->field_218 &= 0xFFFFFEFE;
						this->field_31C.bothFlags = 16;
					}
					else
					{
						this->field_31C.bothFlags = 32;
					}

					this->dumbAssPad = 0;

					break;
				}
				else
				{
					this->mVel.vz = 0;
					this->mVel.vy = 0;
					this->mVel.vx = 0;

					this->GetArenaPositionFromAngleOffset(
							2048,
							&this->field_240);

					this->field_218 |= gCarnage2;
					this->field_218 &= 0xFFFFFFFE;

					this->field_31C.bothFlags = 64;
					this->dumbAssPad = 0;
					break;
				}
			}
			else
			{
				this->mVel.vz = 0;
				this->mVel.vy = 0;
				this->mVel.vx = 0;

				this->field_218 &= 0xFFFFFFFE;
				this->field_31C.bothFlags = 16;
				this->dumbAssPad = 0;
			}


			break;
		case 1:
			v8 = this->CalculateAngleDelta();

			if (this->mAnimFinished)
				this->RunAnim(2, 0, -1);

			v9 = my_abs(v8);
			if (v9 >= 380 && v9 <= 1000)
			{
				this->GetArenaPositionFromAngleOffset(
						v8 < 0 ? -190 : 190,
						&this->field_240);
				this->field_218 |= gCarnageOne;
			}
			else
			{
				this->dumbAssPad--;
				this->field_218 &= 0xFFFFFFFE;
			}

			break;
		case 2:

			if (static_cast<u32>(Utils_XZDist(&this->mPos, &this->field_240)) < 0x40u)
			{
				this->mVel.vz = 0;
				this->mVel.vy = 0;
				this->mVel.vx = 0;

				this->field_218 &= 0xFFFFFFFE;
				this->dumbAssPad = 0;
			}
			else
			{
				if (this->mAnimFinished)
					this->RunAnim(2u, 0, -1);
			}
			break;
	}
}

// @Ok
// Verified against IDA decompile of 0x41BD50 (?Hit@CCarnage@@UAEHPAUSHitInfo@@@Z, 876 bytes):
// found and fixed a real bug, the angle-wrap subtraction was "v13 -= 4906" (typo), original
// subtracts 4096 same as the other wrap branch. Everything else (early-return anim/frame gates,
// health subtraction, field_0 flag nesting for the stagger-direction/pushback/slide math) matches
// field for field. field_218 clearing is two ANDs here (&~7 then &~0x78) vs one AND &~0x7F in the
// original, same net bitmask, not chasing it under the functional bar.
i32 CCarnage::Hit(SHitInfo* pInfo)
{
	if (this->mHealth <= 0)
		return 0;

	if (this->mAnim == 20)
		return 0;
	if (this->mAnim == 12 && this->mFrame >= 10)
		return 0;

	if (this->mAnim == 13)
	{
		if ((this->mFrame >= 8 && this->mFrame <= 18) ||
				(this->mFrame >= 28 || this->mFrame <= 2))
			return 0;
	}
	if ( this->mAnim == 34 )
	{
		if (this->mFrame >= 12 && this->mFrame <= 22)
			return 0;
	}
	if ( this->mAnim == 35 )
	{
		if (this->mFrame >= 5 && this->mFrame <= 12)
			return 0;
	}

	this->field_340 = 30;
	this->mVel.vz = 0;
	this->mVel.vy = 0;
	this->mVel.vx = 0;
	this->field_218 &= 0xFFFFFFF8;
	this->field_218 &= 0xFFFFFF87;

	i32 v9;
	if (pInfo->field_0 & 2)
		v9 = pInfo->field_4;
	else
		v9 = 0;

	this->field_318 = v9;

	this->mHealth -= (pInfo->field_8 * Utils_GetValueFromDifficultyLevel(4096, 1024, 256, 256)) >> 12;

	if (this->mHealth <= 0)
	{
		this->field_31C.bothFlags = 2048;
		this->dumbAssPad = 0;

		return 1;
	}

	if (pInfo->field_0 & 8)
	{
		if (this->field_31C.bothFlags == 0x2000 && pInfo->field_4 == 2)
		{
			this->field_218 |= gCarnage40;
		}
		else
		{
			CSVector v20;

			Utils_CalcAim(&v20, &this->mPos, &(this->mPos + (pInfo->field_C << 12)));

			i32 v13 = v20.vy - this->mAngles.vy;

			if (v13 < -2048)
			{
				v13 += 4096;
			}
			else if (v13 > 2048)
			{
				v13 -= 4096;
			}

			if (v13 < -256)
			{
				this->field_218 |= gCarnage8;
			}
			else if (v13 > 256)
			{
				this->field_218 |= gCarnage10;
			}
		}

		if (pInfo->field_0 & 2)
		{
			if (pInfo->field_4 == 2 || pInfo->field_4 == 6)
			{
				this->field_328 = 60;
				pInfo->field_0 |= gCarnage10Two;

				pInfo->field_18 = 600;
				pInfo->field_1A = 16;

				VectorNormal(
						reinterpret_cast<VECTOR *>(&pInfo->field_C),
						reinterpret_cast<VECTOR *>(&pInfo->field_C));
			}
		}

		if (pInfo->field_0 & 0x10)
		{
			this->field_334 = (pInfo->field_C * pInfo->field_18) / pInfo->field_1A;
			this->field_330 = pInfo->field_1A;

			if (*reinterpret_cast<i16*>(&pInfo[1]) > 128)
			{
				if ((this->field_218 & 0x40) == 0)
				{
					this->field_218 |= gCarnage20;

					CVector v21 = this->mPos + (pInfo->field_C * 512);

					CSVector v20;
					Utils_CalcAim(&v20, &this->mPos, &v21);

					this->mAngles.vy = (v20.vy - 2048) & 0xFFF;
				}
			}
			this->CheckSlideParams();
		}


	}

	this->field_31C.bothFlags = 1024;
	this->dumbAssPad = 0;
	return 1;
}

// @Ok
// @AlmostMatching: if i fix the delta thingy with the XOR and shift bs it causes the stack to reduce to 8 instead of the
// expected 0xC. Additionally, it doesn't generate a jump to LABEL_37 aka setting field_31C to 2 and dumbasspad to 0
void CCarnage::BurnInBubble(void)
{
	this->field_340 = 30;
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_328 = 16;
			this->RunAnim(18, 0, -1);
			DoAssert(1, "Bad register index");

			if (gWhatIf)
			{
				i32 v6 = Rnd(32);
				v6 &= 0xFFFFFFFE;

				this->PlayXA(
						gCarnageBurnInBubbleWhatIfXa[v6],
						gCarnageBurnInBubbleWhatIfXa[v6 + 1],
						100);
			}
			else
			{
				i32 v7 = Rnd(32);
				v7 &= 0xFFFFFFFE;

				this->PlayXA(
						gCarnageBurnInBubbleXa[v7],
						gCarnageBurnInBubbleXa[v7 + 1],
						100);
			}

			if (!this->field_36C)
			{
				SendSignalToNode(ControlBaddyList, this->field_358);
				this->field_36C = 1;
			}
			this->dumbAssPad++;


			break;
		case 1:
			this->field_328 = 16;

			if (this->mAnimFinished)
			{
				DoAssert(1u, "Bad register index");

				if (this->realRegisterArr[0] == 2)
				{
					this->RunAnim(0xFu, 0, -1);
					this->dumbAssPad++;
				}
				else
				{
					DoAssert(1u, "Bad register index");
					this->realRegisterArr[0]++;
					this->RunAnim(0x13u, 0, -1);

				}
			}
			break;

		case 2:
			this->field_328 = 16;
			if (this->mAnimFinished && this->mAnim == 15)
			{
				this->RunAnim(0x10u, 0, -1);

				i32 v18 = 2048;
				do
				{
					CSVector v19;

					Utils_CalcAim(&v19, &gCarnageVector, &G_MECHLIST_PLAYER->mPos);
					v19.vy = (v19.vy + v18) & 0xFFF;

					Utils_GetVecFromMagDir(&this->field_240, 768, &v19);
					this->SnapArenaPosition(&this->field_240);

					v18 += 256;
				}
				while (!Utils_LineOfSight(&this->mPos, &this->field_240, 0, 0));

				this->field_218 |= gCarnageOne;
				this->dumbAssPad++;
			}
			break;
		case 3:
			this->field_328 = 16;
			if (this->mAnimFinished)
				this->RunAnim(16, 0, -1);

			if (static_cast<u32>(Utils_XZDist(&this->mPos, &this->field_240)) < 0x20u)
			{
				this->mVel.vz = 0;
				this->mVel.vy = 0;
				this->mVel.vx = 0;

				this->field_218 &= ~1u;
				this->RunAnim(0x11u, 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 4:
			this->field_328 = 16;

			if (this->mAnimFinished)
			{

				if (this->mAnim == 17)
				{
					// @FIXME - what is this all shifting logic wtf
					/*
					i32 delta = this->CalculateAngleDelta();

					i32 ecx = delta >= 0x600 ? 0 : 1;

					i32 eax = ecx;
					eax >>= 31;

					i32 edx = eax;
					edx ^= ecx;

					edx -= eax;

					if (!edx)
						*/
					if (this->CalculateAngleDelta() >= 0x600)
					{
						this->RunAnim(0x16u, 0, -1);
					}
					else
					{
						this->field_31C.bothFlags = 2;
						this->dumbAssPad = 0;
					}
				}
				else if (this->mAnim == 22)
				{
					this->RunAnim(0x17u, 0, -1);
				}
				else
				{
					this->RunAnim(0x18u, 0, -1);
					this->dumbAssPad++;
				}
			}
			break;
		case 5:
			this->field_328 = 16;
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
	}
}

// @Ok
// @AlmostMatching: different order assigning vectors and registerarr
// but the cast of Utils_XZDist to u32 is even weirder since it's a i32 on DoSonicBubbleProcessing
void CCarnage::GetYankedBySpidey(void)
{
	CVector mPoss;
	i32 v21;
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_328 = 16;
			this->RunAnim(0x21u, 0, -1);
			DoAssert(1u, "Bad register index");
			this->realRegisterArr[0] = 0;
			this->dumbAssPad++;
		case 1:
			this->field_328 = 16;
			if (this->mAnimFinished)
				this->RunAnim(33, 0, -1);

			mPoss = this->mPos;
			if (this->field_344)
			{
				DoAssert(1u, "Bad register index");

				u32 v4 = this->realRegisterArr[0] + this->mElapsedTicks / 2;

				if (v4 >= 7)
				{
					this->field_330 = 0;
					i32 GroundHeight = Utils_GetGroundHeight(&this->field_344[7], 0, 0x2000, 0);
					DoAssert(GroundHeight != -1, "Error");

					this->mPos.vx = this->field_344[7].vx;
					this->mPos.vy = GroundHeight - (this->field_21E << 12);
					this->mPos.vz = this->field_344[7].vz;
				}
				else
				{
					DoAssert(1u, "Bad register index");
					this->realRegisterArr[0] = v4;
					this->mPos = this->field_344[v4];
				}
			}
			else
			{
				if (this->field_330 >= this->mElapsedTicks)
				{
					this->mPos += this->mElapsedTicks * this->field_334;
					this->field_330 -= this->mElapsedTicks;
				}
				else
				{
					this->mPos += (this->field_330 * this->field_334);
					this->field_330 = 0;
				}
			}
			v21 = 0;

			CVector v24;
			if (M3dColij_LineToSphere(
						&mPoss,
						&this->mPos,
						&v24,
						G_MECHLIST_PLAYER,
						0,
						4096))
			{
				SHitInfo v28;

				v28.field_C.vx = 0;
				v28.field_C.vy = 0;
				v28.field_C.vz = 0;

				v28.field_0 = 14;
				v28.field_4 = 11;

				v28.field_C = (this->mPos - mPoss) >> 12;
				v28.field_C.vy = 0;

				VectorNormal(
						reinterpret_cast<VECTOR*>(&v28.field_C),
						reinterpret_cast<VECTOR*>(&v28.field_C));
				v28.field_8 = 30;

				G_MECHLIST_PLAYER->Hit(&v28);

				v21 = 1;
				this->mPos = mPoss;
				this->field_330 = 0;
			}



			if (!this->field_330)
			{
				CTrapWebEffect *v18 = static_cast<CTrapWebEffect *>(
						Mem_RecoverPointer(&this->field_10C));
				if (v18)
					v18->Burst();

				// @FIXME - casts are weird wtf
				if (this->field_35C == 4
					&& static_cast<u32>(Utils_XZDist(&this->mPos, &ZeroVector)) < static_cast<u32>(this->field_350))
				{
					this->field_31C.bothFlags = 4096;
					this->dumbAssPad = 0;
				}
				else
				{
					if (v21)
						this->field_31C.bothFlags = 0x4000;
					else
						this->field_31C.bothFlags = 2;
					this->dumbAssPad = 0;
				}
			}
			break;
	}
}

// @Ok
// @AlmostMatching: SetScale inline has the OR first for some reason
void CCarnage::DoSonicBubbleProcessing(void)
{
	CSonicBubble *v4;
	switch (this->field_35C)
	{
		case 1:
			this->field_354 += this->mElapsedTicks;
			if (this->field_354 > 240)
			{
				this->field_35C = 2;
				this->field_354 = 0;
				SendSignalToNode(ControlBaddyList, this->field_358);
			}
			break;
		case 2:
			v4 = static_cast<CSonicBubble *>(
					Mem_RecoverPointer(&this->hBubble));

			this->field_350 += 20 * this->mElapsedTicks;
			this->field_354 += this->mElapsedTicks;

			if (v4)
			{
				v4->SetScale((this->field_354 << 12) / 30);
			}

			if (this->field_354 > 30)
			{
				this->field_35C = 4;
				this->field_354 = 0;
				this->field_350 = 600;

				if (v4)
				{
					v4->SetScale(4096);
				}
			}

			break;
		case 4:
			if (this->field_354 > 300)
			{
				this->field_35C = 8;
				this->field_354 = 0;

				SendSignalToNode(
						ControlBaddyList,
						this->field_358);
			}
			else
			{
				if ((G_MECHLIST_PLAYER->mCollision & 2) != 0
					&& Utils_XZDist(&G_MECHLIST_PLAYER->mPos, &ZeroVector) < this->field_350)
				{
					SHitInfo v13;

					v13.field_C.vx = 0;
					v13.field_C.vy = 0;
					v13.field_C.vz = 0;

					v13.field_8 = 1;
					v13.field_0 = 4;
					if (G_MECHLIST_PLAYER->mHealth > 1)
					{
						v13.field_0 = 6;
						v13.field_4 = 16;
					}

					G_MECHLIST_PLAYER->Hit(&v13);
				}
			}
			break;
		case 8:
			this->field_36C = 0;
			v4 = static_cast<CSonicBubble*>(
					Mem_RecoverPointer(&this->hBubble));

			this->field_350 += -20 * this->mElapsedTicks;
			this->field_354 += this->mElapsedTicks;

			if (v4)
			{
				v4->SetScale(4096 - ((this->field_354 << 12) / 30));
			}

			if (this->field_354 > 30)
			{
				this->field_35C = 1;
				this->field_354 = 0;
				this->field_350 = 0;
				if (v4)
				{
					v4->SetScale(0);
				}
			}
			
			break;
		default:
			DoAssert(0, "Error");
			break;
	}

	i32 v12 = 0;
	if (this->field_350)
	{
		if (this->field_31C.bothFlags != 2048
				&& Utils_XZDist(&this->mPos, &ZeroVector) < this->field_350)
		{
			v12 = 1;
			this->mHealth -= 2 * this->mElapsedTicks;

			if (this->mHealth <= 0)
			{
				this->mHealth = 0;
				this->field_31C.bothFlags = 2048;
				this->dumbAssPad = 0;
			}
		}
	}
	
	if (v12)
	{
		this->field_360 += this->mElapsedTicks;
		if (this->field_360 > Utils_GetValueFromDifficultyLevel(300, 300, 240, 120)
				&& this->field_35C == 4)
			this->field_354 = 301;
	}
	else
	{
		this->field_360 = 0;
	}
}

// @Ok
// Same MGS-shadow idiom as CBlackCat::DoMGSShadow (blackcat.cpp): 4 hook positions rotated into
// local space to get an X/Z footprint box (min/max start at fixed +-0x20/+-0x40, not box[0], fixed
// against IDA disasm 0x420010), then the 4 box corners (combinations of minX/maxX/minZ/maxZ) are
// rotated back into world space by the (non-transposed) body matrix and translated by mPos to make
// the shadow quad's 4 corners, y forced to field_364. heightOffset is computed and GTE-transformed
// but never read again after that, dead code in the original too, left as-is on purpose. Applied to
// a lazily-created CQuadBit (field_368), mFrigDeltaZ set only at creation time, not every call.
// CCarnage guards on mFlags bit 0 and field_364==-1, tearing down field_368 and returning early when
// either is set (jump table target 0x42036B in the original). Hook ids are 3, 6, 0x11, 0xD (different
// from CBlackCat's 3, 6, 13, 9). Verified against IDA decompile+disasm of 0x420010, functional match.
void CCarnage::DoMGSShadow(void)
{
	if ((this->mFlags & 1) || this->field_364 == -1)
	{
		delete this->field_368;
		this->field_368 = 0;
		return;
	}

	SHook hook;
	VECTOR pos[4];
	pos[0].vx = pos[0].vy = pos[0].vz = 0;
	pos[1].vx = pos[1].vy = pos[1].vz = 0;
	pos[2].vx = pos[2].vy = pos[2].vz = 0;
	pos[3].vx = pos[3].vy = pos[3].vz = 0;

	hook.Part.vx = 0;
	hook.Part.vy = 0;
	hook.Part.vz = 0;

	hook.Offset = 3;
	M3dUtils_GetDynamicHookPosition(&pos[0], this, &hook);

	hook.Offset = 6;
	M3dUtils_GetDynamicHookPosition(&pos[1], this, &hook);

	hook.Offset = 0x11;
	M3dUtils_GetDynamicHookPosition(&pos[2], this, &hook);

	hook.Offset = 0xD;
	M3dUtils_GetDynamicHookPosition(&pos[3], this, &hook);

	i32 height = this->field_21E << 12;

	CVector box[4];
	box[0] = *reinterpret_cast<CVector*>(&pos[0]);
	box[0] -= this->mPos;
	box[1] = *reinterpret_cast<CVector*>(&pos[1]);
	box[1] -= this->mPos;
	box[2] = *reinterpret_cast<CVector*>(&pos[2]);
	box[2] -= this->mPos;
	box[3] = *reinterpret_cast<CVector*>(&pos[3]);
	box[3] -= this->mPos;

	MATRIX localMat;
	M3dMaths_TransposeMatrix1(&this->mTransform, &localMat);
	gte_SetRotMatrix(&localMat);

	i32 maxX = 0x20;
	i32 minX = -0x20;
	i32 maxZ = 0x40;
	i32 minZ = -0x40;
	i32 i;

	for (i = 0; i < 4; i++)
	{
		box[i] >>= 12;
		gte_ldlvl(reinterpret_cast<VECTOR*>(&box[i]));
		gte_rtir();
		gte_stlvnl(reinterpret_cast<VECTOR*>(&box[i]));

		if (box[i].vx > maxX)
		{
			maxX = box[i].vx;
		}
		else if (box[i].vx < minX)
		{
			minX = box[i].vx;
		}

		if (box[i].vz > maxZ)
		{
			maxZ = box[i].vz;
		}
		else if (box[i].vz < minZ)
		{
			minZ = box[i].vz;
		}
	}

	CVector heightOffset;
	heightOffset.vx = 0;
	heightOffset.vy = height;
	heightOffset.vz = 0;

	heightOffset >>= 12;
	gte_ldlvl(reinterpret_cast<VECTOR*>(&heightOffset));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&heightOffset));

	// footprint corners in local space, from the min/max X/Z box extents.
	// (heightOffset above is computed but never read again, dead in the original too.)
	CVector corners[4];
	corners[0].vx = minX; corners[0].vy = 0; corners[0].vz = maxZ;
	corners[1].vx = minX; corners[1].vy = 0; corners[1].vz = minZ;
	corners[2].vx = maxX; corners[2].vy = 0; corners[2].vz = maxZ;
	corners[3].vx = maxX; corners[3].vy = 0; corners[3].vz = minZ;

	gte_SetRotMatrix(&this->mTransform);

	i32 ry = this->field_364;

	for (i = 0; i < 4; i++)
	{
		gte_ldlvl(reinterpret_cast<VECTOR*>(&corners[i]));
		gte_rtir();
		gte_stlvnl(reinterpret_cast<VECTOR*>(&corners[i]));

		corners[i].vx += this->mPos.vx;
		corners[i].vy = ry;
		corners[i].vz += this->mPos.vz;
	}

	if (!this->field_368)
	{
		G_TOTALBITUSAGE = 0;
		this->field_368 = new CQuadBit();
		G_TOTALBITUSAGE = -1;

		this->field_368->SetTexture(0, 0);
		this->field_368->mFrigDeltaZ = 0x20;
	}

	this->field_368->SetTransparency(0x40);
	this->field_368->SetSubtractiveTransparency();
	this->field_368->SetCorners(corners[0], corners[1], corners[2], corners[3]);
}

// @Ok
// @AlmostMatching: mine has 4 more bytes on the stack
// and SnapArenaPosition is inlined both times, while for the OG was only inlined once
// @Test
void CCarnage::StretchJumpAdvance(void)
{
	i32 v9;
	i32 v6;
	switch (this->dumbAssPad)
	{
		case 0:
			this->GetArenaPositionFromAngleOffset(
					this->CalculateAngleDelta() < 0 ? -190 : 190,
					&this->field_240);

			this->field_218 |= gCarnageOne;
			this->RunAnim(6, 0, -1);
			DoAssert(1u, "Bad register index");

			this->realRegisterArr[0] = 0;
			this->dumbAssPad++;
			break;
		case 1:
			DoAssert(1u, "Bad register index");
			this->realRegisterArr[0] += this->mElapsedTicks;
			DoAssert(1u, "Bad register index");

			v9 = this->CalculateAngleDelta();

			if ( ((this->mVel.vx >> 12) * (G_MECHLIST_PLAYER->mVel.vx >> 12)
					+ (this->mVel.vz >> 12) * (G_MECHLIST_PLAYER->mVel.vz >> 12)) < 0
				&& Utils_XZDist(&this->mPos, &G_MECHLIST_PLAYER->mPos) < 768 )

			{
				v9 = 0;
			}

			v6 = my_abs(v9);

			if (v6 >= 380 && v6 <= 1536)
			{
				DoAssert(1u, "Bad register index");
				if (this->realRegisterArr[0] <= 300)
				{
					this->GetArenaPositionFromAngleOffset(
							v9 < 0 ? -190 : 190,
							&this->field_240);

					this->field_218 |= gCarnageOne;
					if (this->mAnimFinished)
						this->RunAnim(7, 0, -1);

					if (G_CARNAGE_XA_RELATED && !G_CARNAGE_XA_RELATED_TWO)
					{
						if (gWhatIf)
						{
							i32 tmp = Rnd(20);
							tmp &= 0xFFFFFFFE;

							this->PlayXA(
									gCarnageStretchAdvWhatIfXa[tmp],
									gCarnageStretchAdvWhatIfXa[tmp + 1],
									60);
						}
						else
						{
							i32 tmp = Rnd(20);
							tmp &= 0xFFFFFFFE;

							this->PlayXA(
									gCarnageStretchAdvXa[tmp],
									gCarnageStretchAdvXa[tmp + 1],
									60);
						}
					}
				}
				else
				{
					this->mVel.vz = 0;
					this->mVel.vy = 0;
					this->mVel.vx = 0;
					this->field_218 &= 0xFFFFFFFE;
					this->RunAnim(8, 0, -1);

					this->field_218 |= gCarnage100;
					this->dumbAssPad++;
				}
			}
			else
			{
				this->mVel.vz = 0;
				this->mVel.vy = 0;
				this->mVel.vx = 0;
				this->field_218 &= ~1u;
				this->RunAnim(8u, 0, -1);
				this->dumbAssPad++;
			}

			break;
		case 2:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		default:
			DoAssert(0, "Unknown state");
			break;
	}
}

// @Ok
// @AlmostMatching: vectors different order
void CCarnage::DoubleAxeHandSlash(void)
{
	i32 v13;
	switch (this->dumbAssPad)
	{
		case 0:
			this->mVel.vz = 0;
			this->mVel.vy = 0;
			this->mVel.vx = 0;

			this->field_218 |= gCarnageFour;
			this->field_218 &= 0xFFFFFFFE;

			this->RunAnim(0x23u, 0, -1);

			this->field_324 = 0;

			if (gWhatIf)
			{
				i32 v10 = Rnd(4);
				v10 &= 0xFFFFFFFE;

				this->PlayXA(
						gCarnageDoubleAxeWhatIfXa[v10],
						gCarnageDoubleAxeWhatIfXa[v10 + 1],
						60);
			}
			else
			{
				i32 v14 = Rnd(4);
				v14 &= 0xFFFFFFFE;

				this->PlayXA(
						gCarnageDoubleAxeXa[v14],
						gCarnageDoubleAxeXa[v14 + 1],
						60);
			}

			this->field_194 |= 0x22000;
			this->field_194 &= 0xFFFBBFFF;

			DoAssert(1u, "Bad register index");
			this->realRegisterArr[0] = 0;

			new CAIProc_MonitorAttack(this, 5, 243712, 6, 16);
			this->dumbAssPad++;

			break;
		case 1:
			this->field_194 |= 0x22000;
			this->field_194 &= 0xFFFBBFFF;

			if (this->field_288 & 0x10)
			{
				this->field_288 &= ~0x10;
				v13 = 1;
			}
			else
			{
				v13 = 0;
			}

			if (v13)
			{
				DoAssert(1u, "Bad register index");
				this->realRegisterArr[0] = 1;

				SHitInfo v17;
				v17.field_C.vx = 0;
				v17.field_C.vy = 0;
				v17.field_C.vz = 0;

				v17.field_0 = 14;
				v17.field_4 = 11;

				v17.field_C = (G_MECHLIST_PLAYER->mPos - this->mPos) >> 12;
				v17.field_C.vy = 0;

				VectorNormal(
						reinterpret_cast<VECTOR*>(&v17.field_C),
						reinterpret_cast<VECTOR*>(&v17.field_C));

				v17.field_8 = 30;

				G_MECHLIST_PLAYER->Hit(&v17);
			}
			else
			{
					if (this->mFrame >= 5)
					{
						if ((this->field_324 & 1) == 0)
						{
							this->field_324 |= 1;

							SFX_PlayPos(
									(Rnd(6) + 476) | 0x8000,
									&this->mPos,
									0);
						}
					}

				if (this->mAnimFinished)
				{
					this->field_194 |= 0x44000u;
					this->field_194 &= 0xFFFDDFFF;

					this->field_218 &= 0xFFFFFFFB;
					this->field_31C.bothFlags = 2;
					this->dumbAssPad = 0;
				}
			}



			break;
		default:
			DoAssert(0, "Unknown state");
			break;
	}
}

// @Ok
// @AlmostMatching: vector assignemnt and creation different order and rebased
void CCarnage::StretchJumpFlow(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_328 = 16;
			this->field_370 = this->mPos;
			this->RunAnim(0x14u, 0, -1);

			this->mRMinor = 0;
			this->mCBodyFlags &= 0xFFEFu;

			if ((this->field_218 & 0x400) == 0)
			{
				this->field_218 |= gCarnage400;

				if (gWhatIf)
				{
					i32 v11 = Rnd(4);
					v11 &= 0xFFFFFFFE;
					this->PlayXA(
							gCarnageStretchJumpXaWhatIf[v11],
							gCarnageStretchJumpXaWhatIf[v11 + 1],
							100);
				}
				else
				{
					i32 v14 = Rnd(4);
					v14 &= 0xFFFFFFFE;
					this->PlayXA(
							gCarnageStretchJumpXa[v14],
							gCarnageStretchJumpXa[v14 + 1],
							100);
				}
			}

			this->dumbAssPad++;

			break;
		case 1:
			this->field_328 = 16;
			if (this->mFrame > 8)
			{
				if (this->mFrame <= 38)
				{
					CVector a3 = ((this->field_240 - this->field_370) >> 12);

					i32 sin = G_RCOSSIN_TBL[(((this->mFrame - 8) << 10) / 30) & 0xFFF].sin;
					i32 v18 = (sin * sin) >> 12;

					a3 *= v18;

					this->mPos = this->field_370 + a3;
				}
				else
				{
					this->mPos = this->field_240;
					this->field_218 &= 0xFFFFFFFD;
					this->dumbAssPad = 2;
				}
			}
			break;
		case 2:
			this->field_328 = 16;

			if (this->mAnimFinished)
			{
				this->mRMinor = 160;
				this->mCBodyFlags |= 0x10u;
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		default:
			DoAssert(0, "Unknown state");
			break;
	}
}

// @Ok
// @AlmostMatching: diff assignet to vectors and ebx is not used by my code
// @Test
u8 CCarnage::TugImpulse(
		CVector *a2,
		CVector *a3,
		CVector *a4)
{
	if (this->field_31C.bothFlags != 256)
		return 0;

	this->field_2A8 &= ~8u;

	if (a4)
	{
		if (this->field_344)
			Mem_Delete(this->field_344);

		this->field_344 = a4;
		this->field_330 = 666;

		this->field_334 = ZeroVector;

		CVector v19;
		v19.vz = a4[7].vz - this->mPos.vz;
		v19.vx = a4[7].vx - this->mPos.vx;
		v19.vy = this->mPos.vy;

		Utils_CalcAim(&this->mAngles, &this->mPos, &v19);
	}
	else
	{
		a3->vy = 0;
		*a3 >>= 12;

		VectorNormal(
				reinterpret_cast<VECTOR*>(a3),
				reinterpret_cast<VECTOR*>(a3));

		this->field_334 = (*a3 * 1000) / 24;
		this->field_330 = 24;

		CVector v19 = (this->mPos + (*a3 * 1000));
		i32 v16 = Utils_XZDist(&v19, &ZeroVector);
		if (v16 < this->field_350 && v16 > 256)
		{
			CVector a1;
			a1.vx = v19.vx >> 12;
			a1.vy = 0;
			a1.vz = v19.vz >> 12;

			VectorNormal(
					reinterpret_cast<VECTOR *>(&a1),
					reinterpret_cast<VECTOR *>(&a1));

			a1 *= Rnd(96) + 128;
			a1.vy = v19.vy;

			this->field_334 = (a1 -  this->mPos) / 24;
		}
		
		Utils_CalcAim(&this->mAngles, &this->mPos, &v19);
		this->CheckSlideParams();
	}

	this->field_31C.bothFlags = 512;
	this->dumbAssPad = 0;

	return 1;
}

// @Ok
// @Matching
void CCarnage::TakeHit(void)
{
	CTrapWebEffect *v4;
	switch (this->dumbAssPad)
	{
		case 0:
			v4 = static_cast<CTrapWebEffect *>(
					Mem_RecoverPointer(&this->field_104));
			if (v4)
				v4->Burst();

			this->field_104.pWhatever = 0;

			if (this->field_218 & 0x40)
			{
				this->RunAnim(0x20u, 0, -1);
			}
			else if (this->field_218 & 0x20)
			{
				this->RunAnim(0x1Cu, 0, -1);
			}
			else if (this->field_218 & 8)
			{
				this->RunAnim(0x1Bu, 0, -1);
			}
			else if (this->field_218 & 0x10)
			{
				this->RunAnim(0x1Au, 0, -1);
			}
			else
			{
				this->RunAnim(0x19u, 0, -1);
			}
			this->dumbAssPad++;
		case 1:
			if (this->field_330 >= this->mElapsedTicks)
			{
				this->mPos += this->mElapsedTicks * this->field_334;
				this->field_330 -= this->mElapsedTicks;
			}
			else
			{
				this->mPos += (this->field_330 * this->field_334);

				this->field_330 = 0;
				if (this->mAnim == 32)
					this->mAnimFinished = 1;
			}

			if (this->mAnimFinished && !this->field_330)
			{
				if (this->field_35C == 4)
				{
					if (Utils_XZDist(&this->mPos, &ZeroVector) < this->field_350)
					{
						this->field_31C.bothFlags = 4096;
						this->dumbAssPad = 0;
						break;
					}

					if (gWhatIf)
					{
						i32 v7 = Rnd(4);
						v7 &= 0xFFFFFFFE;
						this->PlayXA(
							gCarnageWhatIfXa[v7],
							gCarnageWhatIfXa[v7 + 1],
							60);
					}
					else
					{
						i32 v8 = Rnd(4);
						v8 &= 0xFFFFFFFE;
						this->PlayXA(
							gCarnageXa[v8],
							gCarnageXa[v8 + 1],
							60);

					}
				}

				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
	}
}

// @Ok
// Found the real gap via IDA decompile of 0x41CD40 (j_CCarnage_DoPhysics/CCarnage_DoPhysics): the
// field_218&1 branch does more than TurnTowards, it also derives a forward speed from how fast the
// turn is (my_abs(mAngVel.vy), (64-turnRate)<<6 floored at 0, scaled x17/x6/x8 by mAnim==7/16/other)
// and feeds that into GetVecFromMagDir(&mVel, speed>>12, &sameAimVector) to set mVel. That whole
// block was missing before (this is what the old "one dead-looking read/store" note was circling).
// field_218&2/&4 branches only do TurnTowards, no such block, matches disasm.
void CCarnage::DoPhysics(void)
{
	if (this->field_218 & 1)
	{
		CSVector v1;
		Utils_CalcAim(&v1, &this->mPos, &this->field_240);
		Utils_TurnTowards(this->mAngles, &this->mAngVel, &this->mAngAcc, CSVector(0, v1.vy, 0), 10);

		i32 turnRate = my_abs(this->mAngVel.vy);
		i32 speed = (turnRate < 64) ? (64 - turnRate) << 6 : 0;

		i32 scaled;
		if (this->mAnim == 7)
			scaled = 17 * speed;
		else if (this->mAnim == 16)
			scaled = 6 * speed;
		else
			scaled = 8 * speed;

		Utils_GetVecFromMagDir(&this->mVel, scaled >> 12, &v1);
	}
	else if (this->field_218 & 2)
	{
		CSVector v1;
		Utils_CalcAim(&v1, &this->mPos, &this->field_240);
		Utils_TurnTowards(this->mAngles, &this->mAngVel, &this->mAngAcc, CSVector(0, v1.vy, 0), 8);
	}
	else if (this->field_218 & 4)
	{
		CSVector v1;
		Utils_CalcAim(&v1, &this->mPos, &G_MECHLIST_PLAYER->mPos);
		Utils_TurnTowards(this->mAngles, &this->mAngVel, &this->mAngAcc, CSVector(0, v1.vy, 0), 8);
	}
	else
	{
		this->mAngVel.vx = 0;
		this->mAngVel.vy = 0;
		this->mAngVel.vz = 0;
		this->mAngAcc.vx = 0;
		this->mAngAcc.vy = 0;
		this->mAngAcc.vz = 0;
	}

	this->mAngVel.vx = (this->mAngVel.vx + this->mAngAcc.vx) - ((this->mAngVel.vx + this->mAngAcc.vx) >> 2);
	this->mAngVel.vy = (this->mAngAcc.vy + this->mAngVel.vy) - ((this->mAngAcc.vy + this->mAngVel.vy) >> 2);
	this->mAngVel.KillSmall();

	for (i32 i = 0; i < this->mElapsedTicks; i++)
	{
		this->mPos += this->mVel;
		this->mAngles += this->mAngVel;
	}

	this->mAngles.Mask();
}

// @Ok
// @AlmostMatching: On case 2, when assigning to registerArr the registers ecx and edx are swaped
void CCarnage::GetTrapped(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_328 = 16;
			new CAIProc_StateSwitchSendMessage(this, 14);

			this->RunAnim(9u, 0, -1);
			this->dumbAssPad++;

			break;
		case 1:
			this->field_328 = 16;

			if (this->mAnimFinished)
			{
				this->RunAnim(0xAu, 0, -1);
				DoAssert(1u, "Bad register index");
				this->realRegisterArr[3] = 0;
				this->field_1F8 = 2;
				this->dumbAssPad++;
			}
			break;
		case 2:
			this->field_328 = 16;
			if (this->mAnim != 10 &&
					this->mAnim != 9 ||
					this->mAnimFinished )
			{
				this->RunAnim(0xAu, 0, -1);
			}

			if (--this->field_1F8 <= 0)
			{
				// @FIXME: assuming it's trap web effect because of other instances
				CTrapWebEffect *v6 = static_cast<CTrapWebEffect *>(
						Mem_RecoverPointer(&this->field_104));
				if ( !v6
					|| (DoAssert(1u, "Bad register index"), v6->field_44->mNumSegs == this->realRegisterArr[3]) )
				{
					this->dumbAssPad++;
				}
				else
				{
					i32 v7 = v6->field_44->mNumSegs;
					DoAssert(1u, "Bad register index");
					this->realRegisterArr[3] = v7;
					this->field_1F8 = 2;
				}
			}
			break;
		case 3:
			this->field_328 = 16;
			if (this->mAnim == 11)
			{
				this->RunAnim(0xBu, 0, -1);
				CTrapWebEffect *v9 = static_cast<CTrapWebEffect *>(
						Mem_RecoverPointer(&this->field_104));
				if (v9)
					v9->Burst();
			}
			else
			{
				if (this->mAnimFinished)
				{
					this->field_31C.bothFlags = 2;
					this->dumbAssPad = 0;
				}
			}
			break;
		default:
			DoAssert(0, "Unknown substate!");
			break;
	}
}


// @Ok
// @AlmostMatching: for some reason registerArr assignements are out of order
void CCarnage::TugWebTrapped(void)
{
	i32 v4;
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_328 = 16;
			this->RunAnim( 9u, 0, -1);
			this->field_218 &= 0xFFFFFFF8;

			this->mVel.vz = 0;
			this->mVel.vy = 0;
			this->mVel.vx = 0;

			DoAssert(1u, "Bad register index");
			this->realRegisterArr[0] = 0;
			this->dumbAssPad++;
		case 1:
			this->field_328 = 16;
			if (this->mAnimFinished)
				this->RunAnim(0xAu, 0, -1);
			DoAssert(1u, "Bad register index");

			v4 = this->realRegisterArr[0] + this->mElapsedTicks;

			if (v4 > 60)
			{
				this->RunAnim(0xBu, 0, -1);
				if (this->field_10C.pWhatever)
				{
					CTrapWebEffect *v5 = static_cast<CTrapWebEffect*>(Mem_RecoverPointer(&this->field_10C));
					if (v5)
						v5->Burst();
					this->field_10C.pWhatever = 0;
				}
				this->dumbAssPad++;
			}
			else
			{
				print_if_false(1u, "Bad register index");
				this->realRegisterArr[0] = v4;

				if (this->field_218 & 0x80u)
				{
					this->field_218 &= 0xFFFFFF7F;
					if (this->field_10C.pWhatever)
					{
						CTrapWebEffect *v7 = static_cast<CTrapWebEffect *>(
								Mem_RecoverPointer(&this->field_10C));
						if (v7)
							v7->Burst();
						this->field_10C.pWhatever = 0;
					}

					this->field_31C.bothFlags = 2;
					this->dumbAssPad = 0;
				}
			}
			break;
		case 2:
			this->field_328 = 16;
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
	}
}

// @Ok
// Verified fully inlined into ?SelectAttack@CCarnage (0x41DB20, IDA decompile): the aim-vy diff and
// wrap by +-4096 logic here is byte-for-byte the same shape as the inlined copy the original compiler
// produced at every call site (SelectAttack, ThrowBlades case 4). INLINE, so this has no standalone
// address to diff against.
 i32 CCarnage::CalculateAngleDelta(void)
{
	CSVector v5;

	Utils_CalcAim(&v5, &gCarnageVector, &this->mPos);
	i32 v3 = v5.vy;

	Utils_CalcAim(&v5, &gCarnageVector, &G_MECHLIST_PLAYER->mPos);
	i32 result = v5.vy - v3;

	if (result > 2048)
	{
		result -= 4096;
	}
	else if (result < -2048)
	{
		result += 4096;
	}

	return result;
}

// @Ok
// Verified fully inlined into ?SelectAttack@CCarnage (0x41DB20, IDA decompile): angle = CalcAim(this)
// + offset, wrapped to 0xFFF, magdir at radius 868, then SnapArenaPosition. Matches the inlined shape
// at the call site. INLINE, so this has no standalone address to diff against.
 void CCarnage::GetArenaPositionFromAngleOffset(
		i32 a2,
		CVector *a3)
{
	CSVector v6;

	Utils_CalcAim(&v6, &gCarnageVector, &this->mPos);
	v6.vy = (v6.vy + a2) & 0xFFF;
	Utils_GetVecFromMagDir(a3, 868, &v6);

	this->SnapArenaPosition(a3);
}

// @Ok
// @Matching
 void CCarnage::SnapArenaPosition(CVector *pVec)
{
	i32 GroundHeight = Utils_GetGroundHeight(pVec, 0, 0x2000, 0);
	DoAssert(GroundHeight != -1, "Error");

	if (GroundHeight != -1)
		pVec->vy = GroundHeight - (this->field_21E << 12);
}

// @Ok
// @AlmostMatching: CameraList seems to be volatile, but don't care enough to
// get it match, it'd require a lot of re-write for little gain
// also edi is saved later but overall it's gucci
void CCarnage::Initialise(void)
{
	i32 GroundHeight;
	CSonicBubble *pBubble;

	switch (this->dumbAssPad)
	{
		case 0:
			GroundHeight = Utils_GetGroundHeight(&this->mPos, 0, 0x2000, 0);
			if (GroundHeight != -1)
				this->mPos.vy = GroundHeight - (this->field_21E << 12);

			pBubble = new CSonicBubble();
			pBubble->SetScale(0);

			this->hBubble = Mem_MakeHandle(pBubble);
			this->RunAnim(4u, 0, -1);
			this->dumbAssPad++;
		case 1:
			if (this->mInputFlags & 1)
			{
				this->field_218 |= gCarnage200;
				this->mInputFlags &= 0xFFFEu;
			}

			if (this->mAnimFinished)
			{
				if (this->field_218 & 0x200)
				{
					if (G_MECHLIST_PLAYER)
						G_MECHLIST_PLAYER->ExitLookaroundMode();
					G_CAMERA_LIST->SetMode((ECameraMode)16);

					DoAssert(1u, "bad value send to BossCamSpinRate");

					CCamera *pCamera = G_CAMERA_LIST;
					pCamera->field_2A4 = 63;
					DoAssert(1, "bad value send to BossCamStationaryRadius");

					CCamera *pCamera2 = G_CAMERA_LIST;
					pCamera2->field_2A8 = 128;
					pCamera2->SetTripodInterpolation(4, 8, 4);

					// @FIXME
					gBossRelated = reinterpret_cast<i32>(this);

					this->field_31C.bothFlags = 2;
					this->dumbAssPad = 0;
				}
				else
				{
					this->RunAnim(4u, 0, -1);
				}
			}
				break;
	}
}
 
// @Ok
// @FIXME: The stack is 4 bytes shorter here because it seems
// they put the pointer of 330 on the stack and then reuse it when making assignments later which is weird af
void CCarnage::CheckSlideParams(void)
{
	CVector v5 = this->field_334 * this->field_330;
	CVector v6 = this->mPos + v5;

	SLineInfo v7;
	v7.StartCoords.vx = 0;
	v7.StartCoords.vy = 0;
	v7.StartCoords.vz = 0;
	v7.EndCoords.vx = 0;
	v7.EndCoords.vy = 0;
	v7.EndCoords.vz = 0;

	v7.MinCoords.vx = 0;
	v7.MinCoords.vy = 0;
	v7.MinCoords.vz = 0;
	v7.MaxCoords.vx = 0;
	v7.MaxCoords.vy = 0;
	v7.MaxCoords.vz = 0;
	v7.Position.vx = 0;
	v7.Position.vy = 0;
	v7.Position.vz = 0;
	v7.Normal.vx = 0;
	v7.Normal.vy = 0;
	v7.Normal.vz = 0;

	v7.StartCoords = this->mPos;
	v7.EndCoords = v6;
	M3dColij_InitLineInfo(&v7);
	M3dZone_LineToItem(&v7, 1);

	if (v7.pItem)
	{
		if (v7.Distance - 256 <= 0)
		{
			this->field_330 = 0;

			this->field_334.vx = 0;
			this->field_334.vy = 0;
			this->field_334.vz = 0;
		}
		else
		{
			this->field_330 = this->field_330 * (v7.Distance - 256) / v7.Length;
		}
	}

}

// @Ok
// @AlmostMatching: mVel is out of order for some reason
void CCarnage::Laugh(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->mVel.vz = 0;
			this->mVel.vy = 0;
			this->mVel.vx = 0;

			this->field_218 &= 0xFFFFFFFE;
			this->field_218 |= gCarnageFour;

			if (this->mAnim != 4 || this->mAnimFinished)
				this->RunAnim(4u, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
			{
				this->field_218 &= 0xFFFFFFFB;
				if (G_MECHLIST_PLAYER->mActionState != 0x800000)
				{
					this->field_31C.bothFlags = 2;
					this->dumbAssPad = 0;
				}
				else
				{
					this->RunAnim(4u, 0, -1);
				}
			}

			break;
	}
}

// @Ok
// @Matching
u8 CCarnage::Grab(CVector*)
{
	if (this->mHealth <= 0 || this->field_31C.bothFlags == 2048)
		return 0;

	this->field_2A8 |= 0x40;
	this->field_31C.bothFlags = 0x2000;
	this->dumbAssPad = 0;

	return 1;
}

// @Ok
// @Matching
CSonicRipple::CSonicRipple(
		const CVector* a2,
		i32 a3,
		i32 a4,
		i32 a5,
		i32 a6,
		i32 a7,
		i32 a8,
		i32 a9,
		i32 a10,
		u8 a11,
		u8 a12,
		u8 a13,
		i32 a14,
		i32 a15)
	: CGPolyLine(a7)
{
	this->mPos = *a2;

	this->field_58 = a3;
	this->field_5A = a4;
	this->field_5C = (a4 - a3) / a7;
	this->field_5E = a5;
	this->field_60 = a6;

	this->SetSemiTransparent();

	this->mStartR = 0;
	this->mStartG = 0;
	this->mStartB = 0;

	SLineSeg *pSeg = this->mSegs;

	for (i32 i = 0; i < a7 - 1; i++)
	{
		pSeg->r = a11;
		pSeg->g = a12;
		pSeg->b = a13;

		pSeg++;
	}

	pSeg->r = 0;
	pSeg->g = 0;
	pSeg->b = 0;

	this->field_62 = a14;
	this->field_64 = a15;
	this->field_84 = Rnd(4096);
	this->field_86 = (a8 << 12) / a7;

	this->field_80 = a10;
	this->field_82 = a9;
	this->field_68.vy = -4096;
}

// @Ok
// @Matching
void CSonicRipple::CalcPos(
		CVector *a2,
		i16 a3,
		i32 a4)
{
	i32 v4 = this->field_5E + ((this->field_80 * G_RCOSSIN_TBL[a3 & 0xFFF].sin) >> 12);

	a2->vx = this->mPos.vx + this->field_74.vx * ((v4 * G_RCOSSIN_TBL[a4 & 0xFFF].cos) >> 12);
	a2->vy = this->mPos.vy + this->field_74.vy * ((v4 * G_RCOSSIN_TBL[a4 & 0xFFF].cos) >> 12) - v4 * G_RCOSSIN_TBL[a4 & 0xFFF].sin;
	a2->vz = this->mPos.vz + this->field_74.vz * ((v4 * G_RCOSSIN_TBL[a4 & 0xFFF].cos) >> 12);
}

// @Ok
// @Matching
void CSonicRipple::Move(void)
{
	this->field_74.vx = G_MIKE_CAMERA[0].Position.vx - (this->mPos.vx >> 12);
	this->field_74.vy = G_MIKE_CAMERA[0].Position.vy - (this->mPos.vy >> 12);
	this->field_74.vz = G_MIKE_CAMERA[0].Position.vz - (this->mPos.vz >> 12);

	gte_ldopv1(reinterpret_cast<VECTOR*>(&this->field_68));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&this->field_74));
	gte_op0();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&this->field_74));

	CVector shifted;
	shifted.vx = this->field_74.vx >> 8;
	shifted.vy = this->field_74.vy >> 8;
	shifted.vz = this->field_74.vz >> 8;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&shifted));
	gte_sqr0();

	VECTOR squared;
	gte_stlvnl(&squared);

	i32 mag = M3dMaths_SquareRoot0(squared.vx + squared.vy + squared.vz);

	this->field_74.vx = (this->field_74.vx / mag) << 4;
	this->field_74.vy = (this->field_74.vy / mag) << 4;
	this->field_74.vz = (this->field_74.vz / mag) << 4;

	this->field_84 += this->field_82;

	i32 angle1 = this->field_84;

	this->CalcPos(&this->mStart, angle1, this->field_58);
	angle1 += this->field_86;

	i32 angle2 = this->field_58;
	SLineSeg *seg = this->mSegs;
	i32 i;
	for (i = 0; i < this->mNumSegs - 1; i++)
	{
		angle2 += this->field_5C;
		this->CalcPos(&seg->End, angle1, angle2);
		angle1 += this->field_86;
		seg++;
	}

	this->CalcPos(&seg->End, angle1, this->field_5A);

	this->field_5E += this->field_60;
	this->mAge++;

	if (this->mAge > this->field_62)
	{
		i32 fade = this->field_64;
		i32 r = this->mSegs[0].r;
		i32 g = this->mSegs[0].g;
		i32 b = this->mSegs[0].b;
		r = (r > fade) ? r - fade : 0;
		g = (g > fade) ? g - fade : 0;
		b = (b > fade) ? b - fade : 0;

		if ((b | g | r) == 0)
		{
			this->Die();
			return;
		}

		seg = this->mSegs;
		for (i = 0; i < this->mNumSegs; i++)
		{
			seg->r = r;
			seg->g = g;
			seg->b = b;
			seg++;
		}
	}
}

// @Ok
// @Matching
CSonicRipple::~CSonicRipple(void)
{
}

// @Ok
// @Matching
void CCarnage::MakeSonicRipple(CVector *a2)
{
	new CSonicRipple(a2, 0, 2048, 30, 6, 10, 2, 500, 6, 128, 128, 128, 5, 5);
}

// @Ok
 void CCarnage::PlayXA(
		i32 a2,
		i32 a3,
		i32 a4)
{
	if (Rnd(100) <= a4)
		Redbook_XAPlayPos(a2, a3, &this->mPos, 0);
}

// @Ok
// @Matching
void CCarnage::CreateCombatImpactEffect(CVector* pVec, i32)
{
	for (i32 i = 0; i < 16; i++)
	{
		new CCarnageHitSpark(pVec);
	}
}

// @Ok
 void CCarnage::DieCarnage(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->mVel.vz = 0;
			this->mVel.vy = 0;
			this->mVel.vx = 0;
			this->field_218 &= 0xFFFFFFF8;

			this->RunAnim(36, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
			{
				this->PulseL8A5Node();
				this->dumbAssPad++;
			}
			break;
	}
}


// @Ok
 void CCarnage::PulseL8A5Node(void)
{
	CVector v3;

	for (i32 i = 1 ; i < NumNodes; i++)
	{
		if (*G_OFFSETLIST[i] == 1)
		{
			Trig_GetPosition(&v3, i);
			if (!(v3.vz | v3.vx | v3.vy))
			{
				Trig_SendPulseToNode(i);
				return;
			}
		}
	}

	DoAssert(0, "Node not found");
}

// @Ok
// @Matching
void Carnage_RelocatableModuleClear(void)
{
	CItem *pSearch = BaddyList;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 314)
			delete pSearch;

		pSearch = pNext;
	}
}

// @Ok
// @Matching
void SetTheCarnageGooSourcesChecksums(void)
{
	for (i32 i = 0; i < NUM_CARNAGE_GOOS; i++)
	{
		if (G_CARNAGE_SKIN_GOO_SOURCE[i].field_4 == 0x45F3EC38)
		{
			G_CARNAGE_SKIN_GOO_SOURCE[i].field_4 = Spool_FindTextureChecksum("carnage_bit04_32");
		}
		else if (G_CARNAGE_SKIN_GOO_SOURCE[i].field_4 == 0xD291D41B)
		{
			G_CARNAGE_SKIN_GOO_SOURCE[i].field_4 = Spool_FindTextureChecksum("carnage_bit03_32");
		}
		else if (G_CARNAGE_SKIN_GOO_SOURCE[i].field_4 == 0x6CF38ACE)
		{
			G_CARNAGE_SKIN_GOO_SOURCE[i].field_4 = Spool_FindTextureChecksum("carnage_bit01_32");
		}

		if (G_CARNAGE_SKIN_GOO_SOURCE[i].field_8 == 0x45F3EC38)
		{
			G_CARNAGE_SKIN_GOO_SOURCE[i].field_8 = Spool_FindTextureChecksum("carnage_bit04_32");
		}
		else if (G_CARNAGE_SKIN_GOO_SOURCE[i].field_8 == 0xD291D41B)
		{
			G_CARNAGE_SKIN_GOO_SOURCE[i].field_8 = Spool_FindTextureChecksum("carnage_bit03_32");
		}
		else if (G_CARNAGE_SKIN_GOO_SOURCE[i].field_8 == 0x6CF38ACE)
		{
			G_CARNAGE_SKIN_GOO_SOURCE[i].field_8 = Spool_FindTextureChecksum("carnage_bit01_32");
		}
	}
}

// @Ok
// @Matching
void Carnage_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Carnage_RelocatableModuleClear;
	pMod->field_C[0] = Carnage_CreateCarnage;

	SetTheCarnageGooSourcesChecksums();
}

// @Ok
// Verified against IDA decompile+disasm of 0x419E60 (??0CCarnageHitSpark@@QAE@PAVCVector@@@Z, 936
// bytes incl. SEH cleanup frame): camera-facing normal, GTE cross for the tangent, three
// rcossin_tbl-scaled offset vectors (velocity, mPos, mPosD +-mPosC, mPosB), texture/tint/type at the
// end all match field-for-field. cmpsum shows 68 mnemonic diffs (register scheduling through the long
// vector-math chain), no structural mismatch found.
CCarnageHitSpark::CCarnageHitSpark(CVector* pVec)
{
	this->mPosC = *pVec;

	CVector v41;
	v41.vx = 0;
	v41.vy = -4096;
	v41.vz = 0;

	CVector v40;

	v40.vx = G_MIKE_CAMERA[0].Position.vx - (this->mPosC.vx >> 12);
	v40.vy = G_MIKE_CAMERA[0].Position.vy - (this->mPosC.vy >> 12);
	v40.vz = G_MIKE_CAMERA[0].Position.vz - (this->mPosC.vz >> 12);

	gte_ldopv1(reinterpret_cast<VECTOR*>(&v41));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&v40));
	gte_op0();

	gte_stlvnl(reinterpret_cast<VECTOR*>(&v40));

	CVector v39;
	v39.vx = v40.vx >> 8;
	v39.vy = v40.vy >> 8;
	v39.vz = v40.vz >> 8;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&v39));
	gte_sqr0();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&v39));

	i32 v8 = M3dMaths_SquareRoot0(v39.vx + v39.vy + v39.vz);
	v40.vx = 16 * (v40.vx / v8);
	v40.vy = 16 * (v40.vy / v8);
	v40.vz = 16 * (v40.vz / v8);


	i32 v9 = Rnd(4096);
	i32 v10 = Rnd(30);
	i32 v11 = (4 * v9) & 0x3FFC;

	i32 v12 = ((v10 + 5) * G_RCOSSIN_TBL[v11].cos) >> 12;
	i32 v22 = ((v10 + 5) * G_RCOSSIN_TBL[v11].sin) >> 12;

	this->mVel = (v12 * v41) + (v22 * v40);

	i32 v13 = Rnd(50) + 50;
	i32 v14 = (v13 * G_RCOSSIN_TBL[v11].cos) >> 12;
	i32 v20 = (v13 * G_RCOSSIN_TBL[v11].sin) >> 12;

	CVector v33 = (v14 * v41) + (v20 * v40);

	i32 v15 = (4 * (v9 + 1024)) & 0x3FFC;

	i32 v18 = (10 * G_RCOSSIN_TBL[v15].sin) >> 12;
	i32 v19 = (10 * G_RCOSSIN_TBL[v15].cos) >> 12;

	CVector v30 = (v19 * v41) + (v18 * v40);

	this->mPos = this->mPosC + v33;

	if (Rnd(2))
	{
		this->mPosD = this->mPosC + v30;
	}
	else
	{
		this->mPosD = this->mPosC - v30;
	}

	this->mPosB = this->mPosD + v33;


	this->SetTexture(0x877E63C8);
	this->SetSemiTransparent();
	this->SetTint(0xFF, 0x80u, 0);
	this->mType = 30;
}

// @Ok
// @Matching
void CCarnageHitSpark::Move(void)
{
	this->mPos.vx += this->mVel.vx;
	this->mPos.vy += this->mVel.vy;
	this->mPos.vz += this->mVel.vz;

	this->mPosB.vx += this->mVel.vx;
	this->mPosB.vy += this->mVel.vy;
	this->mPosB.vz += this->mVel.vz;

	this->mPosC.vx += this->mVel.vx;
	this->mPosC.vy += this->mVel.vy;
	this->mPosC.vz += this->mVel.vz;

	this->mPosD.vx += this->mVel.vx;
	this->mPosD.vy += this->mVel.vy;
	this->mPosD.vz += this->mVel.vz;

	if (++this->mAge > 0)
		Bit_ReduceRGB(&this->mTint, 30);

	if ((this->mTint & 0xFFFFFF) == 0)
		this->Die();
}

// @Ok
CCarnageHitSpark::~CCarnageHitSpark(void)
{
}

// @Ok
CCarnage::~CCarnage(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&BaddyList));
	Panel_DestroyHealthBar();

	if (this->field_32C)
		delete reinterpret_cast<CItem*>(this->field_32C);

	if (this->field_344)
		Mem_Delete(this->field_344);
}

// @Ok
// @Matching
CCarnage::CCarnage(i16 *a2, i32 a3)
{
	this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2)));
	this->InitItem("carnage");
	this->AttachTo(reinterpret_cast<CBody**>(&BaddyList));

	this->mFlags |= 0x480;
	this->mpLight = &M3d_CarnageLight;
	this->field_194 = 278528;

	this->mType = 314;
	this->field_31C.bothFlags = 1;

	this->mHealth = 3000;
	this->mRMinor = 160;
	this->field_21E = 100;
	this->field_35C = 1;
	this->field_354 = 241;

	u16 *LinksPointer = reinterpret_cast<u16*>(Trig_GetLinksPointer(a3));
	DoAssert(*LinksPointer == 1, "Error");
	this->field_358 = LinksPointer[1];
	Panel_CreateHealthBar(this, 314);
	CreateSonicBubbleVertexWobbler();
}

// @Ok
// @Matching
void CreateSonicBubbleVertexWobbler(void)
{
	u8 indices[0x25] =
	{
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
		13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
		25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36
	};

	new CVertexWobble(gObjFileRegion, Spool_GetModel(0xE9DD4877, gObjFileRegion), 0x25, indices, 0x14, 0xF, 0x12C, 0x12C);
}

// @Ok
void Carnage_CreateCarnage(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(*stack);
	i32 v3 = static_cast<i32>(stack[1]);

	*result = reinterpret_cast<u32>(new CCarnage(v2, v3));
}

// @Ok
// @Matching
 CSonicBubble::CSonicBubble(void)
{
	this->InitItem(G_OBJ_FILE);

	this->mModel = Spool_GetModel(0xE9DD4877, gObjFileRegion);
	this->AttachTo(reinterpret_cast<CBody**>(&BaddyList));

	this->mCBodyFlags &= 0xFFFFFFEF;
	this->mRMinor = 0;
}

// @Ok
 void CSonicBubble::SetScale(i32 scale)
{
	this->mScale.vx = scale;
	this->mScale.vy = scale;
	this->mScale.vz = scale;
	this->mFlags |= 0x200;
}

// @Ok
// One-liner, same DeleteFrom(&BaddyList) idiom as the already-matched CCarnage::~CCarnage first
// statement. INLINE (Mac .__dt__12CSonicBubbleFv), no standalone PC address to diff against.
CSonicBubble::~CSonicBubble(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&BaddyList));
}

// @Ok
void CCarnage::Shouldnt_DoPhysics_Be_Virtual(void)
{
	this->DoPhysics();
}

// @Ok
 CCarnageElectrified::CCarnageElectrified(CSuper* pSuper)
{
	print_if_false(pSuper != 0, "NULL pSuper sent to CVenomWrap");
	print_if_false(pSuper->mType == 314, "Non carnage sent to CCarnageElectrified");

	this->field_3C = Mem_MakeHandle(pSuper);
}

// @Ok
// @Matching
CCarnageElectrified::~CCarnageElectrified(void)
{
}

void validate_CCarnage(void){
	VALIDATE_SIZE(CCarnage, 0x37C);

	VALIDATE(CCarnage, field_324, 0x324);

	VALIDATE(CCarnage, field_328, 0x328);

	VALIDATE(CCarnage, field_32C, 0x32C);

	VALIDATE(CCarnage, field_330, 0x330);

	VALIDATE(CCarnage, field_334, 0x334);

	VALIDATE(CCarnage, field_340, 0x340);

	VALIDATE(CCarnage, field_344, 0x344);


	VALIDATE(CCarnage, hBubble, 0x348);

	VALIDATE(CCarnage, field_350, 0x350);

	VALIDATE(CCarnage, field_354, 0x354);
	VALIDATE(CCarnage, field_358, 0x358);
	VALIDATE(CCarnage, field_35C, 0x35C);

	VALIDATE(CCarnage, field_360, 0x360);

	VALIDATE(CCarnage, field_364, 0x364);
	VALIDATE(CCarnage, field_368, 0x368);

	VALIDATE(CCarnage, field_36C, 0x36C);
	VALIDATE(CCarnage, field_370, 0x370);

	VALIDATE_VTABLE(CCarnage, AI, 2);
	VALIDATE_VTABLE(CCarnage, Hit, 3);

	VALIDATE_VTABLE(CCarnage, CreateCombatImpactEffect, 6);
	VALIDATE_VTABLE(CCarnage, TugImpulse, 7);
	VALIDATE_VTABLE(CCarnage, Grab, 10);
	VALIDATE_VTABLE(CCarnage, MakeSonicRipple, 17);
}

void validate_CSonicBubble(void)
{
	VALIDATE_SIZE(CSonicBubble, 0xF8);

	VALIDATE(CSonicBubble, field_F4, 0xF4);
}

void validate_CCarnageElectrified(void)
{
	VALIDATE_SIZE(CCarnageElectrified, 0x48);

	VALIDATE(CCarnageElectrified, field_3C, 0x3C);
	VALIDATE(CCarnageElectrified, field_44, 0x44);
}

void validate_CCarnageHitSpark(void)
{
	VALIDATE_SIZE(CCarnageHitSpark, 0x84);
}

void validate_CSonicRipple(void)
{
	VALIDATE_SIZE(CSonicRipple, 0x88);

	VALIDATE(CSonicRipple, field_58, 0x58);
	VALIDATE(CSonicRipple, field_5A, 0x5A);
	VALIDATE(CSonicRipple, field_5C, 0x5C);

	VALIDATE(CSonicRipple, field_5E, 0x5E);
	VALIDATE(CSonicRipple, field_60, 0x60);

	VALIDATE(CSonicRipple, field_62, 0x62);
	VALIDATE(CSonicRipple, field_64, 0x64);

	VALIDATE(CSonicRipple, field_68, 0x68);
	VALIDATE(CSonicRipple, field_74, 0x74);

	VALIDATE(CSonicRipple, field_80, 0x80);

	VALIDATE(CSonicRipple, field_82, 0x82);
	VALIDATE(CSonicRipple, field_84, 0x84);
	VALIDATE(CSonicRipple, field_86, 0x86);
}

void validate_CSymbioteBlade(void)
{
	VALIDATE_SIZE(CSymbioteBlade, 0x13C);

	VALIDATE(CSymbioteBlade, field_F8, 0xF8);
	VALIDATE(CSymbioteBlade, mCurveNumSteps, 0x100);
	VALIDATE(CSymbioteBlade, mCurveStepDelta, 0x104);
	VALIDATE(CSymbioteBlade, mCurvePts, 0x108);
	VALIDATE(CSymbioteBlade, field_138, 0x138);
}


// @Bogus
void patch_carnage(void)
{
	// Only the functions whose whole call closure already shares the exe's memory are hooked.
	// Everything else in carnage.cpp reads MechList, BaddyList, ControlBaddyList, gBossRelated,
	// gWhatIf, gObjFile, gObjFileRegion, BulletList or NumNodes, and those are still plain repo
	// globals owned by other files, so a hook there would read our own zeroed copy instead of
	// the game's. See the notes in the commit message for the exact list.
	// TotalBitUsage, QuadBitList and GPolyLineList used to be on that list too, they now go
	// through G_TOTALBITUSAGE / G_QUADBIT_LIST / G_GPOLYLINE_LIST in bit.h.
	PATCH_PUSH_RET(0x00419B60, CSonicRipple::CalcPos);
	PATCH_PUSH_RET_POLY(0x00419C00, CSonicRipple::Move, "?Move@CSonicRipple@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x0041A240, CCarnageHitSpark::Move, "?Move@CCarnageHitSpark@@UAEXXZ");
	PATCH_PUSH_RET(0x0041AFF0, CSymbioteBlade::GenerateControlPoints);
	PATCH_PUSH_RET(0x0041B860, SetTheCarnageGooSourcesChecksums);
	PATCH_PUSH_RET_POLY(0x0041B900, CCarnageElectrified::~CCarnageElectrified, "??1CCarnageElectrified@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x0041C3B0, CCarnage::Grab, "?Grab@CCarnage@@UAEEPAVCVector@@@Z");
}
