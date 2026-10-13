#pragma once


#ifndef EFFECTS_H
#define EFFECTS_H

#include "engine/world/ob.h"
#include "engine/world/bit2.h"
#include "engine/world/collision.h"
#include "compatibility/export.h"

class CBouncingRock : public CFlatBit
{
	public:
		EXPORT CBouncingRock(CVector*, i32, u32);
		EXPORT virtual ~CBouncingRock(void);

		EXPORT virtual void Move(void);

		i32 field_68;
		i32 field_6C;
};

class CChunkSmoke : public CFlatBit
{
	public:
		EXPORT CChunkSmoke(CVector*, CVector*, i32);
		EXPORT virtual ~CChunkSmoke(void);

		EXPORT virtual void Move(void);

		CVector field_68;
		i32 field_74;
		i32 field_78;
		i32 field_7C;
};

// Short coloured line particle, used for the dust puffs a heavy foot throws
// up. mStart is the spawn point, mEnd is that point pushed along a direction,
// and both move by mVel every frame. The line fades out by dropping mFadeRate
// from each channel of the CGLine end colour (mPadBGR1) per frame, and the bit
// kills itself once that colour reaches black. Layout from the two
// constructors (0x439E20, 0x439F20) and Move (0x43A040): only one new field,
// the fade rate byte at 0x5C, and CBit::operator new is called with 96 bytes
// at every call site, which matches sizeof 0x60 after padding.
class CPingLine : public CGLine
{
	public:
		EXPORT CPingLine(CVector*, CSVector*, i32, i32, i32, i32, i32, i32);
		EXPORT CPingLine(CVector*, CVector*, i32, i32, i32, i32, i32, i32);
		EXPORT virtual ~CPingLine(void) OVERRIDE;

		EXPORT virtual void Move(void) OVERRIDE;

		u8 mFadeRate;
};

class CFootprint : public CQuadBit
{
	public:
		EXPORT CFootprint(CVector*, i32);
		EXPORT virtual ~CFootprint(void);

		EXPORT virtual void Move(void);

		i32 field_84;
};

class CRhinoWallImpact : public CQuadBit
{
	public:
		EXPORT CRhinoWallImpact(SLineInfo*);
		EXPORT virtual ~CRhinoWallImpact(void);

		EXPORT virtual void Move(void);

		PADDING(4);
};

class CElectrify : public CSimpleTexturedRibbon
{
	public:
		EXPORT CElectrify(CSuper*, int a2);

		EXPORT void ChooseRandomPositions(i32, i32);

		// @FIXME guessed layout, entries are 8 bytes, only the field at +6
		// (u16) is written by the constructor
		void *field_4C;

		i32 field_50;

		// array of field_50 CVector entries
		CVector *field_54;

		i32 field_58;

		// SHandle wrapping the owning CSuper
		void *field_5C;
		u32 field_60;
};

// field_0 is 4 packed bytes (model index, vertex A index, vertex B index,
// a flip flag the constructor toggles on every use), field_4/field_8 are
// texture checksums. Layout worked out from CSkinGoo::CSkinGoo(CSuper*,
// SSkinGooSource*, i32, SSkinGooParams*), see effects.cpp.
struct SSkinGooSource
{
	u32 field_0;

	u32 field_4;
	u32 field_8;
};

// Layout worked out from CSkinGoo::CSkinGoo(CSuper*, SSkinGooSource2*, i32,
// SSkinGooParams*): field_0 is the same packed 4 bytes as SSkinGooSource
// (model index, vertex A index, vertex B index, flip flag), but field_4 and
// field_24 are two 32-byte texture NAME strings (picked 50/50, looked up
// through the new CQuadBit::SetTexture(char*) overload) instead of
// checksums. 4 + 32 + 32 = 0x44 total.
struct SSkinGooSource2
{
	u32 field_0;
	char field_4[32];
	char field_24[32];
};

// @FIXME guessed field names, layout worked out from
// CSkinGoo::CSkinGoo(CSuper*, SSkinGooSource*, i32, SSkinGooParams*): a
// base/range pair used twice for two independent rolls (field_54.cpp calls
// this an X/Z spread), a second base/range pair, and a range used
// symmetrically (Rnd(2*x+1)-x) for all three axes of a launch velocity.
struct SSkinGooParams
{
	u8 mOffsetXBase;
	u8 mOffsetXRange;
	u8 mOffsetZBase;
	u8 mOffsetZRange;
	u8 mVelRange;
};

class CSkinGoo : public CQuadBit
{
	public:
		EXPORT CSkinGoo(CSuper*, SSkinGooSource*, i32, SSkinGooParams*);
		EXPORT CSkinGoo(CSuper*, SSkinGooSource2*, i32, SSkinGooParams*);

		// SHandle wrapping the owning CSuper
		void *field_84;
		u32 field_88;

		// @FIXME unknown, zeroed by the constructor and never written again
		// in either overload seen so far
		i32 field_8C;
		i32 field_90;
		i32 field_94;
		i32 field_98;
		i32 field_9C;
		i32 field_A0;

		// snapshot of the model's vertex A position at construction time
		i16 field_A4;
		i16 field_A6;
		i16 field_A8;

		u16 field_AA;

		// snapshot of the model's vertex B position at construction time
		i16 field_AC;
		i16 field_AE;
		i16 field_B0;

		u16 field_B2;

		PADDING(8);

		i32 field_BC;
		i32 field_C0;
		i32 field_C4;

		PADDING(4);

		// launch velocity, fixed point (<<12)
		i32 field_CC;
		i32 field_D0;
		i32 field_D4;
};

class CElectro : public CSimpleTexturedRibbon
{
	public:
		EXPORT CElectro(void);
		EXPORT virtual ~CElectro(void);

		EXPORT void Setup(i32, i32, u32*, u8, u8, u8, u16, u16);

		i32 field_4C;

		// @FIXME
		void *field_50;
		void *field_54;

		CVector field_58;
};

class CElectroLine : public CElectro
{
	public:
		EXPORT CElectroLine(u16, u16, u16, u8, u8 ,u8, i32, i32, i32, i32, i32, u32*);
		EXPORT virtual ~CElectroLine(void);

		// offset 0x64: CElectro::Setup's a8 (u16), written through a raw
		// offset cast in Setup, not named here to keep Setup untouched.
		PADDING(4);

		u16 field_68;
		u16 field_6A;
};

class CVertexWobble : public CBit
{
	public:
		EXPORT CVertexWobble(u32, u32, u32, u8*, i32, i32, i32, i32);
		EXPORT virtual void Move(void);

		// SHandle wrapping G_PSXREGION[Region].pPSX
		void *field_3C;
		u32 field_40;

		PADDING(8);

		// SModel* for the chosen model piece (G_PSXREGION[Region].ppModels[a2])
		void *field_4C;

		// number of wobbling vertices
		i32 field_50;

		// array of field_50 SVertexWobbleEntry (22 bytes each), see effects.cpp
		void *field_54;

		// average/centre position of the wobbling vertices
		CSVector field_58;

		PADDING(2);
};

void validate_CVertexWobble(void);
void validate_CElectrify(void);
void validate_CSkinGoo(void);
void validate_SSkinGooSource(void);
void validate_SSkinGooSource2(void);
void validate_SSkinGooParams(void);
void validate_CRhinoWallImpact(void);
void validate_CFootprint(void);
void validate_CPingLine(void);
void validate_CChunkSmoke(void);
void validate_CBouncingRock(void);
void validate_CElectro(void);
void validate_CElectroLine(void);

EXPORT void Effects_Electrify(CSuper*);
EXPORT void Effects_UnElectrify(CSuper*);
// The second parameter is a checksum the PC build never reads (the Mac symbol
// is Effects_FootStomp(CVector const&, ulong) and the only call site passes
// 0xC16175F4). Kept in the signature because the original still takes it.
EXPORT void Effects_FootStomp(CVector*, u32);
// 0x43B410 (Mac: Effects_LaserFlash(CVector const&, int, int, uchar, uchar,
// uchar, uchar)). Called by CBaddy::ExecuteCommand for opcodes 0x42A7/0x42A8.
EXPORT void Effects_LaserFlash(CVector*, i32, i32, u8, u8, u8, u8);
// 0x43B740 (Mac: Effects_MakeRocks(CVector const&, ulong)). Called by
// CBaddy::ExecuteCommand and CPlatform::ExecuteCommand for opcode 0x430A.
EXPORT void Effects_MakeRocks(CVector*, u32);
// 0x439CC0 (Mac: Effects_CreateElectroLines(int)). Called by Trig_CreateNodeItem
// for node type 409.
EXPORT void Effects_CreateElectroLines(i32);

void patch_effects(void);

#endif
