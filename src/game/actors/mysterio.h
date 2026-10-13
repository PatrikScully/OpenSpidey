#pragma once

#ifndef MYSTERIO_H
#define MYSTERIO_H

#include "compatibility/export.h"
#include "game/actors/baddy.h"
#include "game/cinematics/camera.h"
#include "core/game_heap.h"

#include "engine/assets/reloc.h"

class CSmokeGenerator;

class CMystFoot : public CBaddy
{
	public:
		// no out-of-line address in names.json: gets inlined at both call
		// sites in CMysterio::CMysterio(i16*, i32), matches the CManipOb /
		// CManipObChunk InitItem+AttachTo idiom (manipob.cpp).
		 CMystFoot(void);
};

class CSoftSpot : public CBaddy
{
	public:
		EXPORT CSoftSpot(CBaddy*, i32, i32, i32);

		i32 field_324;
		i32 field_328;
		i32 field_32c;
		SHandle field_330;
};

class CMysterio;
class CGoldFish;
class CMysterioHeadCircle;

// field_324-owned sub-object of CMysterio (the head glow effect), ctor at
// 0x45AAA0 (188 bytes, matches operator new(188) at the call site in
// CMysterio::CMysterio). Real base class is CGlow, not CQuadBit: the ctor's
// disasm never calls CQuadBit::CQuadBit/CBit::CBit or AttachTo(&QuadBitList)
// for "this", but it DOES call the CVector*+ints+6xu8 CGlow ctor overload
// (ecx stays == incoming "this" across the "mov edi,ecx" spill, so the
// thiscall receiver is "this", not the CVector* arg) and later calls the
// inherited CGlow::SetRGB(128,0,255) on "this" too. CGlow's own size (0x5C,
// validate_CGlow) lines up exactly with where this class's own fields start
// (this+23 DWORD index == 0x5C). mProtected/mFrigDeltaZ writes on "this"
// land at CBit's real offsets (0x3A/0x38) via the CBit->CGlow chain.
class CMysterioHeadGlow : public CGlow
{
	public:
		EXPORT CMysterioHeadGlow(CMysterio*);

		// two parallel 8-entry arrays, randomized in the ctor once per
		// CGlow section (this->mNumSections, always 8 for the CVector-arg
		// CGlow overload used here): Rnd(4096) is the same 0x1000 angle-
		// table modulus used elsewhere in bit.cpp (rcossin_tbl), so this is
		// read as a per-section random flicker phase; the second array
		// (Rnd(50)+200) is read as a per-section timer/period. Guesses, not
		// confirmed against any reader elsewhere.
		i32 mSectionPhase[8]; // 0x5C
		i32 mSectionPeriod[8]; // 0x7C

		// unclear purpose; always zeroed in the ctor, no other reader/writer
		// found.
		i32 field_A4;

		// Mem_MakeHandle(owner) result, stored so the head glow can find its
		// parent CMysterio back.
		SHandle mOwnerHandle; // 0xA8

		// mutually exclusive with field_B8: populated (both) only when
		// gWhatIf is false, left null (both, zeroed by CBit::operator new)
		// otherwise.
		CMysterioHeadCircle *field_B0;
		CMysterioHeadCircle *field_B4;

		// mutually exclusive with field_B0/field_B4: populated only when
		// gWhatIf is true.
		CGoldFish *field_B8;
};

class CMysterio : public CBaddy {
	public:

	EXPORT CMysterio(i16*, i32);
	EXPORT CMysterio(void);
	EXPORT ~CMysterio(void);
	EXPORT u8 MystRedbook_XAPlayPos(i32, i32, CVector*, i32);
	EXPORT i32 CMysterio::PlayAndAttachXAPlease(i32, i32, CBody*, i32);
	EXPORT void ShakePad(void);
	EXPORT i32 CheckforCameraShake(i32);
	EXPORT void EnterP2(void);
	EXPORT i32 GetAttackRotSpeed(void);
	EXPORT void SummonAttack(void);
	EXPORT void LookMenacing(void);
	EXPORT void RotateToOptimalAttackAngle(i32, i32);
	EXPORT i32 MonitorAttack(i32, VECTOR*, i32);

	CItem* field_324;

	// read (!= 0 check) by Panel_DisplayHealthBar (panel.cpp, offset 0x328
	// relative to a CBody* boss pointer) to pick between the default and
	// an alternate health-bar icon texture; meaning otherwise unknown.
	i32 field_328;

	// walked by loop index (not by the trigger-link field code) in the
	// constructor's Trig_GetLinkInfoList loop; holds up to 8 CSoftSpot
	// pointers. field_38C[8..10] (see field_38C below) alias the 0xC bytes
	// right after field_3A8, still inside the class, just past this array.
	CSoftSpot* field_32C[8];

	i32 field_34C;
	i32 field_350;
	PADDING(4);

	i32 field_358;
	PADDING(4);


	SHandle field_360;
	SHandle field_368;
	PADDING(4);

	i32 field_374;
	i32 field_378;

	CVector field_37C;

	i32 field_388;

	i32 field_38C;

	PADDING(8);

	i32 field_398;
	i32 field_39C;
	i32 field_3A0;

	PADDING(4);


	i32 field_3A8;

	PADDING(0x3B8-0x3A8-4);

	u32 field_3B8;

	PADDING(0x3d0-0x3b8-4);

};

class CMysterioLaser : public CNonRenderedBit
{
	public:
		EXPORT void SetDamage(int);

		PADDING(0x11-4);

		i32 field_4C;

		PADDING(0x64-0x4C-4);
};

class CGoldFish : public CBody
{
	public:
		// no standalone address in names.json: inlined into
		// CMysterioHeadGlow::CMysterioHeadGlow (baddy.cpp), the only call
		// site (0x45AB3B). Read straight off that inlined block.
		EXPORT CGoldFish(void);

		EXPORT void AngryMode(void);
		EXPORT void NormalMode(void);

		PADDING(0xF8-0xF4);

		i32 field_F8;

		PADDING(0x110-0xF8-4);

};

class CMysterioHeadCircle : public CQuadBit
{
	public:
		// no standalone address in names.json: inlined into
		// CMysterioHeadGlow::CMysterioHeadGlow (baddy.cpp), the only two call
		// sites (0x45ABCF, 0x45AC6C). Read straight off those inlined blocks;
		// same gShellMysterioRelated-driven field_88 idiom as the menu-preview
		// twin CShellMysterioHeadCircle::CShellMysterioHeadCircle (shell.cpp).
		EXPORT CMysterioHeadCircle(void);

		EXPORT void NormalMode(void);
		EXPORT void AngryMode(void);

		PADDING(4);

		i32 field_88;
		i32 field_8C;
};

class CFadePalettes : public CNonRenderedBit
{
	public:
		EXPORT CFadePalettes(u8,u8,u8);

		EXPORT void FadeDown(void);
		EXPORT void Move(void);
		EXPORT ~CFadePalettes(void);

		// pointers to allocated fade-tracking blocks, one per active 16 colour
		// palette. Cap checked against 0xC0 in the constructor.
		void *field_3C[0xC0];

		// pointers to allocated fade-tracking blocks, one per active 256 colour
		// palette. Cap checked against 0x44 in the constructor.
		void *field_33C[0x44];

		// region-table index used by Move() to look up a validity flag
		// (print_if_false "Region became unusable"). Never written by the
		// code decompiled so far; guess based on offset math only.
		i32 field_44C;

		i32 field_450;
		i32 field_454;

		// Move() reads these with movsx (signed), unlike field_45D/E/F below
		// which are read as plain unsigned bytes; kept signed here to match.
		i8 field_458;
		i8 field_459;
		i8 field_45A;

		u8 field_45B;

		u8 field_45C;
		u8 field_45D;
		u8 field_45E;
		u8 field_45F;
};

class CAngrySpark : public CQuadBit
{
	public:
		EXPORT CAngrySpark(CVector*);
		EXPORT virtual ~CAngrySpark(void) OVERRIDE;
};

class CDamagedSoftSpotEffect : public CNonRenderedBit
{
	public:
		EXPORT CDamagedSoftSpotEffect(CBody*, i32);
		EXPORT virtual ~CDamagedSoftSpotEffect(void) OVERRIDE;

		SHandle field_3C;
		i32 field_44;

		CSmokeGenerator *field_48;
};

void validate_CMystFoot(void);
void validate_CMysterio(void);
void validate_CSoftSpot(void);
void validate_CMysterioLaser(void);
void validate_CGoldFish(void);
void validate_CMysterioHeadCircle(void);
void validate_CFadePalettes(void);
void validate_CAngrySpark(void);
void validate_CDamagedSoftSpotEffect(void);

EXPORT void Mysterio_CreateMysterio(const u32 *stack, u32 *result);
EXPORT void Mysterio_RelocatableModuleInit(reloc_mod *);
EXPORT void Mysterio_RelocatableModuleClear(void);

EXPORT void Mysterio_FadePalettesUp(const u32*, u32*);
EXPORT void Mysterio_FadePalettesDown(const u32*, u32*);
#endif
