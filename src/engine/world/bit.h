#pragma once

#ifndef BIT_H
#define BIT_H

#include "game/application/main.h"
#include "core/fixed_vector.h"
#include "engine/rendering/texture.h"
#include "engine/rendering/ps2funcs.h"

// @Note guessed the name
struct SSimpleRibbonParams
{
	// Raw fixed-point spine point position (CSimpleTexturedRibbon::Display reads this at
	// offset 0x0, confirmed against the raw disasm at 0x40aa00: v8[0..2], where v8 is
	// this->field_44 cast to int*).
	CVector mPos;
	PADDING(0xC);
	// Per-point ribbon half-width scale (confirmed by CSimpleTexturedRibbon::SetWidth/i,
	// which write it, and Display, which multiplies it by the per-segment unit-length
	// perpendicular vector from Utils_CalcUnitFacingCamera). Was field_18.
	u32 mWidth;
};

// @Note: guessed name
struct SRibbonTexture
{
	u16 field_0;
	u16 field_2;

	u8 field_4;
	u8 field_5;
	u8 field_6;
	u8 field_7;
	u8 field_8;
	u8 field_9;
	u8 field_A;
	u8 field_B;

	u16 field_C;
	u16 field_E;

	u8 u0;
	u8 v0;

	u16 mClut;

	u8 u1;
	u8 v1;

	u16 mPage;

	u8 u2;
	u8 v2;

	u8 u3;
	u8 v3;

	u32 mTexWin;
};

class vector3d
{
	public:
		f32 field_0[3];

		EXPORT vector3d(f32, f32, f32);
};

class vector4d
{
	public:
		f32 field_0[4];

		// @Ok
		EXPORT vector4d(void)
		{
		}

		EXPORT vector4d(const vector3d&, f32);
		EXPORT vector4d& operator=(const vector4d&);
};

struct SFringeQuad
{
	u32 Width;
	u32 CodeBGR;
};

struct SSection
{
	u32 Radius;
	u32 PadBGR;
};

struct SAnimFrame
{
	char OffX;
	char OffY;
	u8 Width;
	u8 Height;
	Texture *pTexture;
};


EXPORT extern u32 SparkSize;
// @FIXME - is it really volatile?
// Master game tick, bumped once per vblank by MyVSync (utils.cpp).  The exe
// owns it at 0x006B4CA8 and 175 sites in the original read it, almost none of
// which are hooked, so the macro has to point at game memory: a repo-local
// copy would sit at 0 forever because MyVSync itself is not hooked.
// EXPORT extern volatile i32 gTimerRelated;
//#define G_TIMER_RELATED (gTimerRelated)
#define G_TIMER_RELATED (*reinterpret_cast<volatile i32*>(0x006B4CA8))

struct SRibbonPoint {
	// offset: 0000 (12 bytes)
	CVector Pos;
	// offset: 000C
	u8 r;
	// offset: 000D
	u8 g;
	// offset: 000E
	u8 b;
	// offset: 0010
	u16 Width;
	// offset: 0012
	u16 WidthB;
	// offset: 0014
	u8 rB;
	// offset: 0015
	u8 gB;
	// offset: 0016
	u8 bB;
	// offset: 0018
	i32 Last1Scr;
	// offset: 001C
	i32 Last2Scr;
	// offset: 0020
	i32 Last3Scr;
};

class CBit
{
	public:
		CBit* mPrevious;
		CBit* mNext;

		i16 mAge;
		u16 mLifetime;

		CVector mPos;
		CVector mVel;
		CVector mAcc;
		CFriction mFric;
		u8 mDead;

		u16 mFrigDeltaZ;
		u8 mProtected;
		u8 mType;

		EXPORT CBit();
		EXPORT virtual ~CBit();
		EXPORT virtual void Move(void);
		EXPORT void* operator new(size_t size);
		EXPORT void operator delete(void*);
		EXPORT void Die();
		EXPORT void AttachTo(void*);
		EXPORT void SetPos(const CVector &pos);
		EXPORT void DeleteFrom(void*);
};

class CQuadBit : public CBit {

public:
	CVector mPosB;
	CVector mPosC;
	CVector mPosD;
	Texture* mpTexture;
	u32 mCodeBGR;

	u32 field_68;

	u32 mTint;

	u32 field_70;

	u32 field_74;
	u32 field_78;
	u32 field_7C;
	u32 field_80;

	EXPORT CQuadBit(void);
	EXPORT virtual ~CQuadBit(void);

	EXPORT void SetTint(u8 a2, u8 a3, u8 a4);
	EXPORT void SetSemiTransparent();
	EXPORT void SetOpaque();
	EXPORT void SetSubtractiveTransparency();
	EXPORT void SetCorners(const CVector &a2, const CVector &a3, const CVector &a4, const CVector &a5);
	EXPORT void SetTransparency(u8 a2);
	EXPORT void OrientUsing(CVector *, SVECTOR *, i32, i32);
	EXPORT void OrientUsing(CVector *, SVECTOR *, i32, i32, i32);
	EXPORT void SetTexture(i32, i32);
	EXPORT void SetTexture(u32);
	EXPORT void SetTexture(Texture*);
	EXPORT void SetTexture(char*, i32);
	EXPORT void SetTexture(char*);
};

class CFT4Bit : public CBit
{
	public:
		EXPORT CFT4Bit(void);
		EXPORT virtual ~CFT4Bit();
		EXPORT void SetAnimSpeed(short);
		EXPORT void SetScale(u16);
		EXPORT void SetSemiTransparent();
		EXPORT void SetTransparency(u8 t);
		EXPORT void SetAnim(i32);
		EXPORT void IncFrameWithWrap(void);
		EXPORT void SetFrame(i32);
		EXPORT void SetTint(u8, u8, u8);
		EXPORT void SetTexture(Texture*);
		EXPORT void SetTexture(u32);
		EXPORT int Fade(i32);
		EXPORT void SetTransDecay(i32);
		EXPORT void IncFrame(void);

		i16 mTransDecay;
		u16 field_3E;
		u32 mCodeBGR;

		u8 mDeleteAnimOnDestruction;
		PADDING(0x3);

		SAnimFrame *mpPSXAnim;
		SAnimFrame *mpPSXFrame;
		u8 mBitFlags;

		u8 mNumFrames;
		i8 mFrame;
		u8 mFrameFrac;

		i16 mAnimSpeed;
		i16 mScale;
};

class CFlatBit : public CFT4Bit {

public:

	EXPORT CFlatBit(void);
	EXPORT virtual ~CFlatBit(void) OVERRIDE;
	i16 mAngle;
	i16 field_5A;

	PADDING(0x2);

	u16 mAngFric;
	u32 mPostScale;

	PADDING(1);
	u8 mSemiTransparencyRate;

	// Confirmed via IDA disasm of DisplayFlatBitList (0x40dbd0): `mov cx,[ebx+66h]`
	// reads this as a u16 right after mSemiTransparencyRate. When nonzero it replaces
	// (not ORs into) the high 16 bits of mpPSXFrame->pTexture's first dword (which
	// normally holds Texture::clut there), i.e. a per-bit clut override for the
	// frame's baked-in texture. This also explains CFlatBit's real size: CMotionBlur/
	// CFrag (both plain CFlatBit with no extra fields) validate at 0x68, which is
	// exactly mSemiTransparencyRate (0x65, 1 byte) plus this u16 (0x66-0x67) with no
	// further implicit padding.
	u16 mClutOverride;
};

class CNonRenderedBit : public CBit {
	public:
		EXPORT CNonRenderedBit(void);
		EXPORT virtual ~CNonRenderedBit(void) OVERRIDE;
};

class CSpecialDisplay : public CBit
{
	public:
		EXPORT CSpecialDisplay(void);
		EXPORT ~CSpecialDisplay(void) OVERRIDE;
		EXPORT virtual void Display(void);
};

class CSimpleTexturedRibbon : public CSpecialDisplay
{
	public:
		EXPORT void SetRGB(u8, u8, u8);
		EXPORT void SetSemiTransparent(void);

		EXPORT CSimpleTexturedRibbon(void);
		EXPORT CSimpleTexturedRibbon(i32);
		EXPORT virtual void Display(void) OVERRIDE;
		EXPORT void SetNumFaces(i32);
		EXPORT void SetOpaque(void);
		EXPORT void SetTexture(Texture *);
		EXPORT void SetTexture(u32);
		EXPORT void SetTexturei(i32,Texture *);
		EXPORT void SetTexturei(i32,u32);
		EXPORT void SetWidth(u16);
		EXPORT void SetWidthi(i32,u16);
		EXPORT virtual ~CSimpleTexturedRibbon(void) OVERRIDE;

		u16 field_3C;

		u16 field_3E;

		SRibbonTexture *pTextures;

		SSimpleRibbonParams *field_44;

		// Per-point packed RGB colour (byte0=R, byte1=G, byte2=B), field_3C+1 entries. Was
		// tentatively (and wrongly) called "widths" by an earlier pass; resolved 2026-08-31
		// by cross-checking CSimpleTexturedRibbon::SetRGB (which packs r|(g<<8)|(b<<16) into
		// every entry) against the raw disasm of Display's final draw loop (0x40b2f1-0x40b3a0),
		// which reads this array per face and unpacks it byte-for-byte the same way into the
		// quad's flat vertex colour. See CSimpleTexturedRibbon::Display's comment for the
		// full trace.
		u32 *pColours;
};

class CGlow : public CBit
{
	public:
		EXPORT CGlow(u32, u32);
		EXPORT CGlow(CVector*, i32, i32, u8, u8, u8, u8, u8, u8);

		EXPORT virtual ~CGlow(void);

		EXPORT void SetCentreRGB(u8, u8, u8);
		EXPORT void SetRadius(i32);
		EXPORT void SetRGB(u8, u8, u8);

		EXPORT void SetFringeWidth(u32, u32);
		EXPORT void SetFringeRGB(u32, u8, u8, u8);

		SSection* mpSections;
		SFringeQuad* mpFringes;

		u32 mNumSections;
		u32 mNumFringes;
		u32 mCentreCodeBGR;
		i16 mStepAngle;
		u8 mSkipTriangles;

		u16 mAngle;
		u32 mMask;
};

class CLinked2EndedBit : public CFT4Bit
{
	public:
		EXPORT CLinked2EndedBit(void);
		EXPORT virtual ~CLinked2EndedBit(void) OVERRIDE;

		CVector field_58;
		CVector field_64;
};

class CRibbonBit : public CLinked2EndedBit
{
	public:
		EXPORT CRibbonBit(void);
		EXPORT virtual ~CRibbonBit(void) OVERRIDE;
		EXPORT virtual void Move(void);
};

class CRibbon : public CNonRenderedBit
{
	public:
		EXPORT CRibbon(CVector*, i32, i32, i32, i32, i32, i32);
		EXPORT virtual ~CRibbon(void) OVERRIDE;
		EXPORT void SetScale(i32);

		EXPORT void SetPos(CVector&);

		i32 mNumBits;
		i32 mPointsPerBit;
		i32 mNumPoints;

		i32 field_48;

		CVector *mPoints;
		CRibbonBit **mBits;
};

class CSmokeTrail : public CRibbon
{
	public:

		EXPORT CSmokeTrail(CVector*, i32, i32, i32, i32);
		EXPORT virtual void Move(void) OVERRIDE;
		EXPORT virtual ~CSmokeTrail(void) OVERRIDE;

		i32 mFadeAway;
};


/*
class CTexturedRibbon : public CSpecialDisplay
{
	public:
		EXPORT void SetOuterRGBi(int, u8, u8, u8);
		u8 topPad[0x60-0x3C];
		i32* field_60;
};
*/

class CSimpleAnim : public CFlatBit
{
	public:
		EXPORT CSimpleAnim(CVector*, i32, u16, i32, i32, i32);
		EXPORT virtual ~CSimpleAnim(void) OVERRIDE;
		EXPORT virtual void Move(void) OVERRIDE;

		i32 mDie;
		i32 mDieFrame;
};

class CMotionBlur : public CFlatBit
{
	public:
		EXPORT CMotionBlur(CVector*, CVector*, i32,i32,i32,i32);
		EXPORT virtual ~CMotionBlur(void) OVERRIDE;

		EXPORT virtual void Move(void);
};

class CCombatImpactRing : public CFlatBit
{
	public:
		EXPORT CCombatImpactRing(CVector*, u8, u8, u8, i32, i32, i32);
		EXPORT virtual ~CCombatImpactRing(void) OVERRIDE;
		EXPORT virtual void Move(void) OVERRIDE;

		i32 field_68;
		i32 field_6C;
		i32 field_70;
};

class CFrag : public CFlatBit
{
	public:
		EXPORT CFrag(CVector*, u8, u8, u8, i32, u16, i32, i32, i32, i32);
		EXPORT virtual ~CFrag(void) OVERRIDE;
		EXPORT virtual void Move(void) OVERRIDE;
};

class CPixel : public CBit
{
	public:
		EXPORT CPixel(void);
		EXPORT virtual ~CPixel(void);

		u32 tag;

		u8 r0;
		u8 g0;
		u8 b0;

		u8 code;

		u32 mWidthHeight;
};

struct SBitServerEntry
{
	void** field_0;
	void (*field_4)(void**);
};

class CBitServer : public CClass
{
	public:
		EXPORT CBitServer(void);
		EXPORT virtual ~CBitServer(void);
		EXPORT u32 RegisterSlot(void**, void (*)(void**));
		EXPORT void DisplayRegisteredSlots(void);

		u32 mNumEntries;
		SBitServerEntry mEntry[0x20];
};

class CChunkBit : public CBit
{
	public:
		EXPORT CChunkBit(CSVector*, CSVector*, CSVector*);
		EXPORT virtual ~CChunkBit(void);

		// Declared here (Mac prototypes confirm both names/signatures, tools/prototypes.json
		// group "bit") because CShatterBit's construction (Split, shatter.cpp) calls them
		// directly and non-virtually. Still stubbed in bit.cpp (needs sub_4E5DA0/sub_50F180
		// decoded first), but the fields they write (below) are now confirmed and declared,
		// found while decompiling DisplayChunkBitList (0x40bac0, 2026-08-31): SetRGB
		// (0x40B830) writes mColorA (undithered r|g<<8|b<<16) then mColorB/C/D (3 independently
		// Rnd(4096)-dithered variants, `*(this+46..49)` i.e. dword offsets 46-49 = byte
		// 0xB8-0xC4); SetUVs (0x40B910) writes mUV0/1/2 (six floats, `this+148..168` =
		// 0x94-0xA8) and mClut (`this+180` = 0xB4, a zero-extended-u16 dword store).
		EXPORT void SetRGB(u8, u8, u8);
		EXPORT void SetUVs(u16, u16, u8, u8, u8, u8, u8, u8);

		SVECTOR mPosA;
		SVECTOR mPosB;
		SVECTOR mPosC;
		SVECTOR mPosD;

		CVector mWorldPosA;
		CVector mWorldPosB;
		CVector mWorldPosC;
		CVector mWorldPosD;

		CSVector mAngles;

		// UV pairs used cyclically by DisplayChunkBitList's 4 tetrahedron-face draws: each
		// face's 3 triangle corners always pull UVs from these 3 pairs in slot order
		// (mUV0/mUV1/mUV2), regardless of which corner (A/B/C/D) sits in that slot. Confirmed
		// via DisplayChunkBitList (0x40bac0) reading *(f32*)(this+0x94/0x98/0x9C/0xA0/0xA4/0xA8);
		// SetUVs (0x40B910, still stubbed) writes them.
		f32 mUV0[2];   // 0x94/0x98
		f32 mUV1[2];   // 0x9C/0xA0
		f32 mUV2[2];   // 0xA4/0xA8

		// 0xAC-0xB3: unknown/unconfirmed, not read by DisplayChunkBitList or written by the
		// (still stubbed) SetRGB/SetUVs slices decoded so far. Left as padding rather than
		// guessed at.
		PADDING(0x8);

		// Texture/clut id. SetUVs (still stubbed) stores LOWORD(texId) as a full dword
		// (`*(DWORD*)(this+180) = LOWORD(a2)`, i.e. always zero-extended); DisplayChunkBitList
		// reads it back whole for PCGfx_UseTexture's clut argument.
		u32 mClut;     // 0xB4

		// Per-corner packed colors read by DisplayChunkBitList (0xB8/0xBC/0xC0/0xC4, dword
		// offsets 46-49 from SetRGB's own `_DWORD*` indexing) and applied to mWorldPosA/B/C/D
		// respectively (one color per tetrahedron corner, not per UV slot). mColorA is the
		// plain r|g<<8|b<<16 pack; mColorB/C/D are SetRGB's 3 independently Rnd(4096)-dithered
		// variants of the same base color (still stubbed, see SetRGB above).
		u32 mColorA;   // 0xB8
		u32 mColorB;   // 0xBC
		u32 mColorC;   // 0xC0
		u32 mColorD;   // 0xC4
};

// CShatterBit : public CChunkBit. Found while decompiling Shatter_Face/Split (shatter.cpp),
// the "shattered glass triangle fragment" object created by Split()'s base case (recursion
// depth 0) via operator new(216) + CShatterBit::CShatterBit (0x48BDC0). Not present anywhere
// in the repo before this session; grep confirmed zero prior references.
//
// Evidence (2026-08-31, IDA Hex-Rays decompile + raw disasm of 0x48BDC0 ctor, 0x48BEC0 dtor,
// 0x48BF20 SetPos, 0x48C060 Move, and their call site in Split at 0x48C730):
// - sizeof == 216 (0xD8), confirmed by the operator new(216) call size in Split.
// - The ctor forwards its first 3 args straight into CChunkBit::CChunkBit(CSVector*,CSVector*,
//   CSVector*) (0x40B570, already @Ok in this repo) via sub_40B570, then overwrites the vtable
//   pointer set by that base call with CShatterBit's own vtable (off_53BE88) -- exactly the
//   codegen a real `CShatterBit : public CChunkBit` with a ctor that base-inits CChunkBit(a,b,c)
//   would produce. Confirms the inheritance.
// - CChunkBit::SetRGB (0x40B830) and CChunkBit::SetUVs (0x40B910), called directly (not
//   virtually) from Split right after construction, write into CChunkBit's OWN fields at
//   0x94-0xC8 (six UV floats, a u16-as-dword clut id, four RGB-ish dwords). Those bytes are
//   legitimate CChunkBit territory; confirmed independently while decompiling
//   DisplayChunkBitList (bit.cpp, 2026-08-31) and now declared directly on CChunkBit
//   (mUV0/mUV1/mUV2, mClut, mColorA-D, see bit.h above) instead of as padding here.
// - Mac build prototypes (tools/prototypes.json, group "shatter") independently confirm the
//   class name, the 5-arg ctor signature (CSVector const&,CSVector const&,CSVector const&,
//   CVector const&,int), and that Move/SetPos/dtor are all real (non-inlined) member functions
//   on Mac too.
class CShatterBit : public CChunkBit
{
	public:
		EXPORT CShatterBit(CSVector const&, CSVector const&, CSVector const&, CVector const&, i32);
		EXPORT virtual ~CShatterBit(void) OVERRIDE;
		EXPORT virtual void Move(void) OVERRIDE;
		EXPORT void SetPos(const CVector& pos);

		// 0x94-0xC7 (52 bytes): CChunkBit's own UV/color fields (mUV0/1/2, mClut, mColorA-D),
		// written by CChunkBit::SetRGB/SetUVs on every CShatterBit from Split. Declared on
		// CChunkBit itself now (found while decompiling DisplayChunkBitList, 2026-08-31, see
		// bit.h's CChunkBit and bit.cpp's DisplayChunkBitList); no padding needed here anymore,
		// CShatterBit's own new fields below land at 0xC8 automatically as CChunkBit's tail.
		// 0xC8: confirmed by Split (0x48C730, `mov [esi+0C8h], ecx` right after the SetUVs call,
		// ecx loaded from Split's own arg_24 = its 10th parameter, the u32 one). NOT written by
		// the constructor itself (checked: the ctor's disasm never touches 0xC8). Every call site
		// found (Split's single base-case construction) passes through a caller-supplied u32
		// untouched, so this is very likely a packed face/glass color forwarded from
		// Shatter_Face's CalcRGB output; name is our guess.
		u32 mShardColor;

		// 0xCC: CSVector, set once in the ctor to a random per-axis "spin rate" (one axis gets
		// Rnd(160)+80, i.e. 80..239, the other two are 0, chosen by a coin flip between the first
		// two axes; the third axis is always 0), then added onto the inherited CChunkBit::mAngles
		// (0x8C) every Move() tick via CSVector::operator+= (confirmed: Move's disasm calls
		// sub_4E7900, our known CSVector::operator+=, with this+0x8C as the implicit `this` and
		// this+0xCC as the explicit argument). Name is our guess (never seen in idb_globals.txt,
		// this is a member field not a global).
		CSVector mSpinRate;

		// 0xD4: confirmed CRibbon* by name resolution alone -- Move and SetPos both gate a call
		// to sub_410EB0 on "this+0xD4 != 0", and sub_410EB0 resolves in tools/names.json to
		// `?SetPos@CRibbon@@QAEXAAVCVector@@@Z` (CRibbon::SetPos(CVector&), already @Ok/declared
		// in this file). The destructor (0x48BEC0) also checks this field and, if non-null, calls
		// through its vtable slot 0 with arg 1 (the standard MSVC "scalar deleting destructor"
		// pattern, i.e. `delete mTrailRibbon;`). Never written anywhere we found in this session's
		// tracing (not by the ctor, not by Split, not by SetPos/Move) -- so nothing in the traced
		// call graph ever actually attaches a ribbon; left as a real field for fidelity, always
		// nullptr along every path we decompiled. Name is our guess.
		CRibbon* mTrailRibbon;
};

class CTextBox : public CBit
{
	public:
		EXPORT CTextBox(i32, i32, i32, i32, u32, CFriction*);
		EXPORT virtual ~CTextBox(void);

		i32 field_3C;
		PADDING(4);
};

class CFireyExplosion : public CNonRenderedBit
{
	public:
		EXPORT CFireyExplosion(CVector*);
		EXPORT virtual ~CFireyExplosion(void);

		EXPORT virtual void Move(void) OVERRIDE;
};

class CGouraudRibbon : public CSpecialDisplay
{
	public:
		EXPORT CGouraudRibbon(i32, i32);
		EXPORT void Display(void);
		EXPORT void SetRGB(u8,u8,u8);
		EXPORT void SetWidth(u16);
		EXPORT ~CGouraudRibbon(void);

		i32 mTrail;
		i32 mNumPoints;
		SRibbonPoint* mpPoints;
};

class CWibbly : public CGouraudRibbon
{
	public:
		EXPORT CWibbly(u8,u8,u8,i32,i32,i32,i32,i32,i32,i32,i32,i32,i32);
		EXPORT virtual void Move(void) OVERRIDE;
		EXPORT void SetCore(u8,u8,u8,i32);
		EXPORT void SetEndPoints(CVector *,CVector *);
		EXPORT virtual ~CWibbly(void) OVERRIDE;

		CGouraudRibbon* field_48;
		CVector field_4C;
		CVector field_58;
		CVector field_64;
		CVector field_70;
		i32 field_7C;

		i32 field_80;
		i32 field_84;

		i32 field_88;
		i32 field_8C;
		i32 field_90;

		i32 field_94;
};

class CGlassBit : public CBit
{
	public:
		EXPORT CGlassBit(CVector const *,CVector const *,i32,u8,u8,u8,i32,i32,i32);
		EXPORT virtual void Move(void) OVERRIDE;
		EXPORT virtual ~CGlassBit(void) OVERRIDE;

		CVector mPosA;
		CVector mPosB;
		CVector mPosC;

		i32 mGroundY;

		u8 mDefaultR;
		u8 mDefaultG;
		u8 mDefaultB;

		u8 mR;
		u8 mG;
		u8 mB;

		u8 mFadeRate;

		PADDING(1);
};

class CSpark : public CPixel
{
	public:
		EXPORT CSpark(CVector&,i32,i32,i32);
		EXPORT virtual void Move(void) OVERRIDE;
		EXPORT ~CSpark(void) OVERRIDE;

		u8 mFadeR;
		u8 mFadeG;
		u8 mFadeB;

		PADDING(1);
};

EXPORT extern CBit* GPolyLineList;

EXPORT i32 Bit_MakeSpriteRing(CVector*, i32, i32, i32, i32, i32, i32, i32);
EXPORT void MoveList(CBit *);
EXPORT void Bit_SetSparkRGB(u8, u8, u8);
EXPORT void Bit_SetSparkFadeRGB(u8, u8, u8);
EXPORT void Bit_SetSparkTrajectory(const CSVector *);
EXPORT void Bit_CalculateSparkVelocity(CVector&,i32);

EXPORT void Bit_SetSparkTrajectoryCone(const CSVector *);

EXPORT void Bit_ReduceRGB(u32*, i32);
EXPORT void Bit_SetSparkSize(u32);

EXPORT void DisplaySpecialDisplayList(void**);
EXPORT void DisplayTextBoxList(void**);

EXPORT void Bit_Init(void);
EXPORT void Bit_DeleteAll(void);
EXPORT void Bit_ClearTextBoxes(void);
EXPORT void Bit_UpdateQuickAnimLookups(void);
EXPORT void RemoveDeadBits(CBit *);
EXPORT void Bit_RemoveDeadBits(void);
EXPORT void MoveBits(CBit *);
EXPORT void Bit_Move(void);
EXPORT void Bit_Display(void);

EXPORT void DeleteBitList(CBit*);

EXPORT extern CTextBox* TextBoxList;

#define NUM_ANIM_ENTRIES 0x1D
EXPORT extern SAnimFrame* gAnimTable[NUM_ANIM_ENTRIES];

//#define G_ANIM_TABLE (gAnimTable)
#define G_ANIM_TABLE (reinterpret_cast<SAnimFrame**>(0x0056EA64))

EXPORT extern i32 TotalBitUsage;

void validate_CFlatBit(void);
void validate_CFT4Bit(void);
void validate_CQuadBit(void);
void validate_CBit(void);
void validate_CNonRenderedBit(void);
void validate_CSmokeTrail(void);
void validate_CGlow(void);
void validate_CLinked2EndedBit(void);
void validate_CRibbonBit(void);
//void validate_CTexturedRibbon(void);
void validate_CSimpleTexturedRibbon(void);
void validate_CSimpleAnim(void);
void validate_CMotionBlur(void);
void validate_CSpecialDisplay(void);
void validate_SFlatBitVelocity(void);
void validate_CRibbon(void);
void validate_CCombatImpactRing(void);
void validate_SRibbonPoint(void);
void validate_CFrag(void);
void validate_CPixel(void);
void validate_CBitServer(void);
void validate_CChunkBit(void);
void validate_CShatterBit(void);
void validate_CTextBox(void);
void validate_CFireyExplosion(void);
void validate_CWibbly(void);
void validate_SBitServerEntry(void);
void validate_SSection(void);
void validate_SFringeQuad(void);
void validate_vector3d(void);
void validate_vector4d(void);
void validate_CGlassBit(void);
void validate_SRibbonTexture(void);
void validate_SSimpleRibbonParams(void);
void validate_CSpark(void);


EXPORT extern CBit* GLineList;
EXPORT extern CBit* PolyLineList;

// The bit display lists. Every visual effect attaches itself to one of these,
// and Bit_Move / Bit_RemoveDeadBits / Bit_Display / Bit_DeleteAll walk all of
// them once per frame, so hooked and unhooked code have to share the same head
// pointers. Addresses proved from Bit_Init (0x00407FC0), which zeroes the 14
// heads in source order, and from Bit_RemoveDeadBits (0x00408610), which loads
// them in the same order. The maintainer's idb_globals.txt agrees on all 14.
//#define G_CHUNKBIT_LIST (ChunkBitList)
#define G_CHUNKBIT_LIST (*reinterpret_cast<CChunkBit**>(0x0056E9A0))
//#define G_GLINE_LIST (GLineList)
#define G_GLINE_LIST (*reinterpret_cast<CBit**>(0x0056E9CC))
//#define G_GPOLYLINE_LIST (GPolyLineList)
#define G_GPOLYLINE_LIST (*reinterpret_cast<CBit**>(0x0056E9D8))
//#define G_TEXTBOX_LIST (TextBoxList)
#define G_TEXTBOX_LIST (*reinterpret_cast<CTextBox**>(0x0056E9DC))
//#define G_PIXEL_LIST (PixelList)
#define G_PIXEL_LIST (*reinterpret_cast<CPixel**>(0x0056E9E0))
//#define G_GLOW_LIST (GlowList)
#define G_GLOW_LIST (*reinterpret_cast<CGlow**>(0x0056E9E4))
//#define G_GLASS_LIST (GlassList)
#define G_GLASS_LIST (*reinterpret_cast<CBit**>(0x0056EA34))
//#define G_FLATBIT_LIST (FlatBitList)
#define G_FLATBIT_LIST (*reinterpret_cast<CFlatBit**>(0x0056EA50))
//#define G_QUADBIT_LIST (QuadBitList)
#define G_QUADBIT_LIST (*reinterpret_cast<CBit**>(0x0056EB2C))
//#define G_POLYLINE_LIST (PolyLineList)
#define G_POLYLINE_LIST (*reinterpret_cast<CBit**>(0x0056EB30))
//#define G_LINKED2ENDEDBIT_LIST_LEFTOVER (Linked2EndedBitListLeftover)
#define G_LINKED2ENDEDBIT_LIST_LEFTOVER (*reinterpret_cast<CBit**>(0x0056EB40))
//#define G_GENPOLY_LIST (GenPolyList)
#define G_GENPOLY_LIST (*reinterpret_cast<CBit**>(0x0056EB44))

// The registry Bit_Init fills with one display callback per list. main.cpp
// already reads the same address (0x0056EB50) to delete it at shutdown.
//#define G_BITSERVER (gBitServer)
#define G_BITSERVER (*reinterpret_cast<CBitServer**>(0x0056EB50))

// Set around a `new CSomeBit` to pick the heap the bit comes from. Written by
// eight other files, read by CBit::operator new. Address proved from
// CBit::operator new (??2CBit@@SAPAXI@Z) at 0x004088A0, mov eax,[547E40h].
//#define G_TOTALBITUSAGE (TotalBitUsage)
#define G_TOTALBITUSAGE (*reinterpret_cast<i32*>(0x00547E40))

void patch_CBit(void);
void patch_CFT4Bit(void);
void patch_bit(void);

#endif
