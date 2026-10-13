#pragma once

#ifndef CARNAGE_H
#define CARNAGE_H

#include "compatibility/export.h"
#include "game/actors/baddy.h"
#include "engine/assets/reloc.h"
#include "engine/world/bit2.h"
#include "game/application/main.h"

// thrown blade projectile (CCarnage::ThrowBlades). Allocated via operator new(0x13C)
// (0x455390 in the disasm). Name and ctor signature come from the Mac build symbols
// (tools/prototypes.json: "CSymbioteBlade::CSymbioteBlade((CVector const &,CVector const &))",
// size 0x13C from the new(0x13C) call).
//
// SOLVED 2026-08-31: the ctor's first call, sub_460080, is CBody::CBody(). Confirmed via
// idbs/spideypc_names.txt ("00460080: ??0CBody@@QAE@XZ") and idb_globals.txt (the vtable it
// ends on, off_53BBD4, is named "cbody_vtable" there). 0x455390 is CItem::operator new (same
// body as CItem::operator new in ob.cpp, not a separate CClass version; the old comment here
// calling it "CClass::operator new" was a guess based on a merged/identical function body,
// see CLAUDE.md's link-time duplicate elimination note). CBody::CBody, CItem::CItem and
// CSuper::CSuper are ALL already implemented and tagged @Ok in ob.cpp: the "unmodeled base
// object" this stub used to describe already has a home, just in ob.h/ob.cpp, a file this
// investigation had not checked. So CSymbioteBlade derives from CBody, not CClass.
//
// This also unblocks shell.cpp's CheckForPadUnplugged chain (sub_48E4B0 -> sub_460720 ->
// sub_460080): sub_460720 is CSuper::CSuper (0x460720 = "??0CSuper@@QAE@XZ" in
// spideypc_names.txt), also already @Ok in ob.cpp. A parallel session working shell.cpp found
// the same sub_460080 independently (30+ callers binary-wide: villains, items, widgets,
// CDummy); all of them are CBody (or CBody-derived) constructors.
//
// CSymbioteBlade's own fields (past CBody's 0xF4 bytes), from the 0x41AE40 disasm:
// - field_F8 (1 byte, zeroed only).
// - mCurveNumSteps (i32 at 0x100) / mCurveStepDelta (i32 at 0x104): filled in by
//   GenerateControlPoints (see below), consumed by the sibling CSymbioteBlade::DerivePosition
//   (0x41B280, not part of this task, decompiled far enough to confirm the field meaning: it
//   evaluates the curve at t = stepIndex * mCurveStepDelta using the classic cubic Bezier
//   Bernstein basis (1-t)^3, 3t(1-t)^2, 3t^2(1-t), t^3 in Q12 fixed point over mCurvePts[0..3]).
// - mCurvePts[4] (CVector array at 0x108): zeroed, then mCurvePts[0] = the ctor's first
//   CVector arg (also copied into the inherited mPos), mCurvePts[3] = the second arg. These are
//   the 4 control points of a cubic Bezier curve (confirmed via DerivePosition's Bernstein-basis
//   evaluation, not a Catmull-Rom spline or plain interpolation as earlier sessions guessed from
//   the shape alone); mCurvePts[1]/[2] are filled in by GenerateControlPoints.
// - field_138 (i32): a handle to an optional ~88 byte secondary sub-object (own vtable
//   off_53B400, built only if sub_4088A0(88) succeeds), not decompiled.
//
// SOLVED 2026-08-31: sub_41AFF0 is CSymbioteBlade::GenerateControlPoints(void) (confirmed by
// idbs/spideypc_names.txt "0041aff0: CSymbioteBlade_GenerateControlPoints" and the Mac symbol
// idbs/spiderman_names.txt "GenerateControlPoints__14CSymbioteBladeFv"). It is a thiscall member
// function with no extra arguments (called exactly once, from the constructor, right after
// mCurvePts[0]/[3] are set), NOT a free-standing curve-eval helper. What it actually does,
// instruction by instruction:
// 1. delta = (mCurvePts[3] - mCurvePts[0]) >> 12 (chord vector, descaled from Q12 to plain
//    world units); dist = Utils_Dist(mCurvePts[0], mCurvePts[3]) (chord length, plain units);
//    unitDir = VectorNormal(delta) (chord direction, renormalized to Q12).
// 2. mCurvePts[1] = mCurvePts[0] + unitDir * (dist*1365>>12)  (~1/3 of the way along the chord)
//    mCurvePts[2] = mCurvePts[0] + unitDir * (dist*2731>>12)  (~2/3 of the way along the chord)
//    (1365/4096 and 2731/4096 are the closest 12-bit fixed-point approximations of 1/3 and 2/3).
// 3. mCurveNumSteps = dist/32; mCurveStepDelta = 4096/(mCurveNumSteps-1) (Q12 per-step t
//    increment for DerivePosition's later walk).
// 4. Utils_CalcPerps(unitDir, perp2, perp1) builds an orthonormal-ish basis perpendicular to the
//    chord. Then, for i in {1,2}: pick a random angle (Rnd(4096)) and a random magnitude
//    (Rnd(dist/2)), look up cos/sin from the shared rcossin_tbl (word_610C48/610C4A, see
//    rhino.cpp/mysterio.cpp/etc for the same idiom), and add
//    (cos*perp1>>12)*randMag + (sin*perp2>>12)*randMag to mCurvePts[i] -- a random offset in the
//    plane perpendicular to the chord, magnitude up to half the chord length. Finally clamp
//    mCurvePts[i].vy so it never exceeds mCurvePts[3].vy (the end point's height): the randomized
//    control point can bow inward toward the camera/ground but never past the target's height.
// This IS a real randomized-arc curve builder, same idea earlier sessions guessed from the name,
// now implemented and verified against the disasm rather than assumed. All 10 helper calls in
// the original (sub_4E7760/4E7840/4E77D0/4E7720/4E77A0/4E5E20/4E5DA0/4E7590/470430/4E6150) are
// already implemented and named in the repo: CVector::operator-/>>/+/+=  and the two commutative
// operator* overloads (vector.cpp), Utils_CalcPerps/Utils_Dist/Rnd (utils.cpp),
// VectorNormal/M3dMaths_SquareRoot0 (ps2funcs.cpp).
class CSymbioteBlade : public CBody
{
	public:
		// @NotOk
		// residue: field_138's optional trail sub-object (sub_4088A0/sub_410F50/sub_410E80, none
		// decompiled) is skipped entirely (left null) instead of being conditionally allocated.
		// Everything else (base CBody::CBody, mPos/mCurvePts[0]/[3], InitItem, mModel via
		// Spool_GetModel, AttachTo, and the real GenerateControlPoints curve builder below) is
		// implemented against the real disasm.
		EXPORT CSymbioteBlade(const CVector&, const CVector&);

		// see the long comment above the class for what this does and how it was verified.
		EXPORT void GenerateControlPoints(void);

		PADDING(0xF8 - 0xF4);

		u8 field_F8;

		PADDING(0x100 - 0xF9);

		i32 mCurveNumSteps;
		i32 mCurveStepDelta;

		CVector mCurvePts[4];

		i32 field_138;
};

class CSonicRipple : public CGPolyLine
{
	public:
		EXPORT CSonicRipple(const CVector *,i32,i32,i32,i32,i32,i32,i32,i32,u8,u8,u8,i32,i32);
		EXPORT void CalcPos(CVector *,i16,i32);

		EXPORT virtual void Move(void);
		EXPORT virtual ~CSonicRipple(void);

		i16 field_58;
		i16 field_5A;
		i16 field_5C;

		i16 field_5E;

		i16 field_60;

		i16 field_62;
		i16 field_64;

		PADDING(2);

		CVector field_68;
		CVector field_74;

		i16 field_80;

		i16 field_82;
		i16 field_84;
		i16 field_86;
};

class CCarnage : public CBaddy {
public:
	EXPORT CCarnage(i16*, i32);
	EXPORT ~CCarnage(void);

	EXPORT void PulseL8A5Node(void);
	EXPORT void DieCarnage(void);
	EXPORT void PlayXA(i32, i32, i32);
	EXPORT void Laugh(void);
	EXPORT void CheckSlideParams(void);
	EXPORT void Initialise(void);
	EXPORT void SnapArenaPosition(CVector *);
	EXPORT void GetArenaPositionFromAngleOffset(i32,CVector *);
	EXPORT i32 CalculateAngleDelta(void);
	EXPORT void TugWebTrapped(void);
	EXPORT void GetTrapped(void);
	EXPORT void DoPhysics(void);
	EXPORT void TakeHit(void);
	EXPORT void StretchJumpFlow(void);
	EXPORT void DoubleAxeHandSlash(void);
	EXPORT void StretchJumpAdvance(void);
	EXPORT void DoMGSShadow(void);
	EXPORT void DoSonicBubbleProcessing(void);
	EXPORT void GetYankedBySpidey(void);
	EXPORT void BurnInBubble(void);
	EXPORT void SelectAttack(void);
	EXPORT void AxeHandSlash(void);
	EXPORT void ThrowBlades(void);
	EXPORT void GettingGrabbed(void);

	EXPORT void Shouldnt_DoPhysics_Be_Virtual(void);

	EXPORT virtual void AI(void);
	EXPORT virtual i32 Hit(SHitInfo*);
	EXPORT virtual u8 Grab(CVector*);
	EXPORT virtual void CreateCombatImpactEffect(CVector*, i32);
	EXPORT virtual void MakeSonicRipple(CVector*);
	EXPORT virtual u8 TugImpulse(CVector*, CVector*, CVector*);

	i32 field_324;


	i32 field_328;
	void* field_32C;

	i32 field_330;
	CVector field_334;

	i32 field_340;

	CVector *field_344;

	SHandle hBubble;

	i32 field_350;

	i32 field_354;
	i32 field_358;
	i32 field_35C;

	i32 field_360;

	i32 field_364;
	CQuadBit* field_368;

	u8 field_36C;

	CVector field_370;
};

class CSonicBubble : public CBody
{
	public:
		EXPORT CSonicBubble(void);
		EXPORT ~CSonicBubble(void);
		EXPORT void SetScale(i32);

		i32 field_F4;
};

class CCarnageElectrified : public CNonRenderedBit
{
	public:
		EXPORT CCarnageElectrified(CSuper*);
		EXPORT virtual ~CCarnageElectrified(void);

		SHandle field_3C;
		i32 field_44;
};

class CCarnageHitSpark : public CQuadBit
{
	public:
		EXPORT CCarnageHitSpark(CVector*);

		EXPORT virtual void Move(void);

		EXPORT virtual ~CCarnageHitSpark(void);
};


void validate_CCarnage(void);
void validate_CSonicBubble(void);
void validate_CCarnageElectrified(void);
void validate_CCarnageHitSpark(void);
void validate_CSonicRipple(void);
void validate_CSymbioteBlade(void);

EXPORT void CreateSonicBubbleVertexWobbler(void);
EXPORT void Carnage_CreateCarnage(const u32 *stack, u32 *result);
EXPORT void Carnage_RelocatableModuleInit(reloc_mod *);
EXPORT void Carnage_RelocatableModuleClear(void);
EXPORT void SetTheCarnageGooSourcesChecksums(void);

void patch_carnage(void);

#endif
