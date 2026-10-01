#pragma once

#ifndef WEB_H
#define WEB_H

#include "bit.h"
#include "bit2.h"
#include "export.h"
#include "spidey.h"
#include "m3dutils.h"

struct SModel;

// Original 0x4F6290. A loose web strand with two moving endpoints.
class CKnottedWebFrag : public CKnottedWeb
{
	public:
		EXPORT CKnottedWebFrag(const CVector &start, const CVector &end,
			const CVector &startVelocity, const CVector &endVelocity);
		EXPORT virtual void Move(void) OVERRIDE;

		CVector mEndVelocity;
		CVector mEndPos;
		i32 mGroundY;
		u32 mRestDistance;
};

// The three live at 0x006B55A0..0x006B55AB, next to each other. CWeb::CWeb
// (0x4F5DA0) pushes 0x6B55A0 into CBody::AttachTo, and CDome::CDome /
// CDome::~CDome (0x4FA640 / 0x4FA770) bump 0x6B55A4 (the fire-dome count,
// only touched on the a3 != 0 path) and 0x6B55A8 (the total dome count).
// They get macros here in the owning header because baddy.cpp, rhino.cpp,
// main.cpp, spidey.cpp and init.cpp read them too.
EXPORT extern i32 gFireDomes;
//#define G_FIRE_DOMES (gFireDomes)
#define G_FIRE_DOMES (*reinterpret_cast<i32*>(0x006B55A4))

EXPORT extern i32 gNumDomes;
// 0x006B55A8, incremented and decremented by CDome::CDome / ~CDome.
//#define G_NUM_DOMES (gNumDomes)
#define G_NUM_DOMES (*reinterpret_cast<i32*>(0x006B55A8))

EXPORT extern CBody* WebList;
//#define G_WEB_LIST (WebList)
#define G_WEB_LIST (*reinterpret_cast<CBody**>(0x006B55A0))

// Address 0x4F7680. Axis-aligned-box-vs-line-segment clip test; see its own comment in
// web.cpp. pMin/pMax are a box's two opposite corners; on a hit, *pEnd is overwritten with
// the crossing point.
EXPORT i32 BoundingBoxCollisionCheck(SVECTOR const *pMin, SVECTOR const *pMax, SVECTOR const *pStart, SVECTOR *pEnd);

class CImpactWeb : public CFlatBit
{
	public:
		// Original 0x4F9940. Mac mangled name
		// __ct__10CImpactWebFRC7CVectorRC8CSVectoriii gives the parameter
		// list. The web splat left on a wall when a web shot misses.
		EXPORT CImpactWeb(const CVector &Pos, const CSVector &Normal, i32 Speed, i32 Damage, i32 Lifetime);

		// Fields recovered from the constructor's own stores (0x4F9940),
		// which is the only decompiled user of this class so far.

		// the Damage argument, run through
		// CPlayer::GetDamageInflictedFromDifficulty when a player exists
		i32 mDamage;

		// gTimerRelated at spawn time
		i32 mStartTime;

		// the item the forward ray hit, and the face on it. Both left
		// uninitialised when the ray misses (original defect: only the six
		// fields below are pre-zeroed).
		CItem *mpHitItem;
		u32 *mpHitFace;

		CVector mHitPos;
		CSVector mHitNormal;

		PADDING(2);
};


class CDomePiece : public CBody 
{
	public:
		EXPORT CDomePiece(CVector*, i32, i32, i32);
		EXPORT virtual ~CDomePiece(void) OVERRIDE;

		i32 field_F4;
		i32 field_F8;
		i32 field_FC;
};

class CDome : public CBody
{
	public:
		EXPORT CDome(CPlayer*, i32);
		EXPORT virtual ~CDome(void) OVERRIDE;

		PADDING(4);

		SHandle hPlayer;

		i32 field_100;
		i32 field_104;

		CClass *field_108;
		CClass *field_10C;
		CClass *field_110;
		CClass *field_114;
		CClass *field_118;

		// Original 0x4FAD50. Pops the dome: see the stub in web.cpp.
		EXPORT void Burst(void);
};

class CDomeRing : public CBody {
	public:

		// 0x4F5510 / 0x4F5720. Only one CDomeRing exists at a time; the
		// constructor deletes the previous one (gpCurrentDomeRing, web.cpp).
		EXPORT CDomeRing(const CVector *pPos, i32 bFire);
		EXPORT virtual ~CDomeRing(void) OVERRIDE;

		PADDING(4);

		// saved copy of the ring model's vertices, 3 i16 per vertex, so the
		// destructor can put the shared SModel geometry back
		i16 *field_F8;

		// per-vertex outward direction, also 3 i16 per vertex. Only [0] and
		// [2] are ever written; [1] is left uninitialised by the original
		// (original defect, kept).
		i16 *field_FC;

		// the ring's SModel (region model 0), whose vertices get animated
		// outwards in place
		SModel *field_100;

		i32 field_104;
		i32 field_108;

		// nonzero for the fire dome variant ("firering" instead of "ring")
		i32 field_10C;
};


// 0x4F5BC0. The blob of webbing left where a web shot lands. Only CWeb::Fire
// makes one. Base is CQuadBit (0x84), confirmed by the constructor's first
// call being CQuadBit::CQuadBit and by its SetTexture / SetSemiTransparent /
// OrientUsing calls; own fields start at 0x8C and the class ends at 0xB0,
// which is the size the original passes to CBit::operator new.
class CKnottedWebSplat : public CQuadBit
{
	public:
		EXPORT CKnottedWebSplat(const CVector *pPos, const CVector *pNormal);

		// 0x4A5E40. The linker folded this body together with
		// CSimbyShotSplat::Move (which is the name tools/names.json has for
		// that address), so the two classes share the same source; slot 1
		// of this class's own vtable (off_53C760) points at it, and the
		// constructor calls it once before it returns.
		EXPORT virtual void Move(void) OVERRIDE;

		// the size the splat grows to, set to 32 by the constructor
		i32 field_84;

		// current size. The constructor never writes it (CBit::operator new
		// zeroes the whole allocation), so the splat starts at nothing and
		// Move closes half the gap to field_84 every frame.
		i32 field_88;

		// splat centre: pPos pushed 10 units out along pNormal
		CVector field_8C;

		// the two corner offsets the constructor derives from the quad's
		// own mPosB / mPosC after OrientUsing
		CVector field_98;
		CVector field_A4;
};

class CWeb : public CBody
{
	public:

		EXPORT CWeb(void);
		EXPORT virtual ~CWeb(void);
		EXPORT virtual void AI(void);
		EXPORT void SetFirePos(CVector&);

		PADDING(4);

		i32 field_F8;

		PADDING(4);

		u16 field_100;

		// Written as a u16 (0) by CPlayer::CheckJumpingR1ZipWeb and
		// CheckJumpingR2ZipWeb right after the web is allocated, and read
		// back as a u16 by CPlayer::FireWeb, which passes it to
		// M3dUtils_GetHookPosition as the hook index the web leaves from.
		// field_100 was an i32 covering 0x100..0x104 before that was found;
		// nothing in the repo used it, so it is split into two u16 here.
		u16 field_102;

		i32 field_104;
		CVector field_108;

		CVector field_114;

		i32 field_120;
		i32 field_124;
		i32 field_128;
		// the drawn strand, made by CWeb::Fire
		CKnottedWeb *field_12C;

		i32 field_130;

		// SHandle (pWhatever/Id) of whatever this web is attached to; read
		// back with Mem_RecoverPointer by CWeb::SwitchToBlob and by
		// CPlayer::CalculateTugWebPathPoints (spidey.cpp).
		i32 field_134;
		i32 field_138;

		EXPORT void SwitchToBlob(void);
		EXPORT void BreakIntoBits(void);

		// Original 0x4F5ED0. Mac mangled name
		// Fire__4CWebFR7CVectorR7CVectorP5CBodybR8CSVector gives the
		// parameter list. Anchors the web between two points and spawns the
		// drawn strand; pTarget is whatever the web attached to.
		EXPORT void Fire(CVector &Hook, CVector &Target, CBody *pTarget, bool bSplat, CSVector &Normal);
};

// Original 0x4F8D10. Mac mangled name Web_Trap__FP6CSuperi. Wraps a baddy
// in a trap web.
EXPORT void Web_Trap(CSuper *pSuper, i32 a2);

// Fields at 0xF8/0xFC/0x17C reverse engineered 2026-08-31 while decompiling
// CSwinger_SwingBack (0x4F7550, web.cpp): that function reads all three
// through `this`, confirmed instruction-by-instruction against the disasm
// (field_F8 and field_FC feed a CVector subtraction and a copied-out i32;
// mpLine is asserted non-null ("No line?") and its CGPolyLine fields
// mNumSegs/mSegs/mStart are read through it at the offsets CGPolyLine
// already validates in bit2.cpp). field_F8's exact purpose is not
// confirmed (a plain i32 copied verbatim into a spawned effect object's
// own field_74, see bit2.h's CKnottedWeb); field_FC is a CVector, likely
// some kind of "last known hook/anchor offset" given how it is subtracted
// from the swing line's last segment endpoint, but that reading is our own
// guess, not confirmed.
class CSwinger : public CBody
{
	public:
		EXPORT CSwinger(CVector*, i32, const MATRIX*, CVector*);
		EXPORT virtual ~CSwinger(void);
		EXPORT virtual void DeleteStuff(void);
		EXPORT virtual void AI(void);
		EXPORT void SetRenderEnd(CVector&);
		EXPORT i32 IsOneTimeToDie(void);
		EXPORT void SetSpideyAnimFrame(i32);

		PADDING(0xF8-0xF4);
		i32 field_F8;
		CVector field_FC;
		i32 mLength;
		CVector mAnchor;
		MATRIX mBaseMatrix;
		MATRIX mSwingMatrix;
		CVector mForward;
		CVector mSide;
		CVector mDown;
		CGPolyLine *mpLine;
		i32 field_180;
		i32 mPhaseSpeed;
		i32 field_188;
		i32 field_18C;
};

// Called by CPlayer::CheckJumpingSmashKick to release the held web-swinging
// object before the smash kick animation starts.
EXPORT void CSwinger_SwingBack(CSwinger *a1);

class CSplat : public CQuadBit
{
	public:
};

// Reverse engineered 2026-08-31 (session working on CTrapWebEffect::Burst,
// 0x4F8600) via CWebFrag's constructor (0x4FA080, called from Burst with
// CBit::operator new(0x8C)). Mac mangled name from
// idbs/spiderman_names.txt confirms the parameter list:
// CWebFrag(int, const CVector&, const CVector&, const CVector&, const
// CVector&, int, int). Base is CGLine (bit2.h): the constructor's first
// call is CGLine::CGLine(), confirmed by name via
// idbs/spideypc_names.txt ("00412c00: ??0CGLine@@QAE@XZ") matching
// names.json's own "CGLine_CGLine" for 0x412C00, and CGLine's own size
// (0x5C, VALIDATE_SIZE in bit2.cpp) exactly matches where CWebFrag's own
// fields start (0x5C) and CGLine's mCodeBGR0 (0x3C)/mStart(0x44)/mEnd(0x50)
// are the fields the ctor writes through inherited offsets.
class CWebFrag : public CGLine
{
	public:
		EXPORT CWebFrag(
				i32 GroundY,
				const CVector &PrevPos,
				const CVector &HookA,
				const CVector &HookB,
				const CVector &SuperPos,
				i32 Speed,
				i32 Jitter);

		// Three copies of the same vector: (HookA - SuperPos) scaled to
		// length Speed (left at zero if HookA == SuperPos), with a random
		// (-10..10) fixed-point jitter added to the vy component of each
		// copy when Jitter == 0. Only the stores are confirmed from the
		// disasm; which reader (if any, e.g. a not-yet-decompiled Move())
		// uses which copy is not known, so these keep raw names.
		CVector field_5C;
		CVector field_68;
		CVector field_74;

		i32 mGroundY;

		// Second CGLine, heap-allocated and constructed by CWebFrag's own
		// ctor (same 0x2000000 mCodeBGR0 flag, CBit::mProtected = 1), whose
		// mStart/mEnd run HookA -> HookB. Together with this object's own
		// mStart/mEnd (PrevPos -> HookA), the pair draws a connected
		// two-segment "web strand" through the three points.
		CGLine *field_84;

		// Random small variant/index (1..3, or 6..8 with lower odds); exact
		// use (sprite frame? which of two shapes?) unknown.
		u8 field_88;
		PADDING(3);
};

// One triangle of webbing spanning three of the trap web's hooks. Written by
// CTrapWebEffect::AddAnotherStrand (0x4F83C0) and consumed every frame by
// CTrapWebEffect::Move (0x4F8860, not decompiled yet), which fills the quad
// from the three hook positions: mPos from mHookA, mPosB from mHookB, mPosC
// AND mPosD both from mHookC (so the quad draws as a triangle).
struct SWebStrand
{
	u8 mHookA;
	u8 mHookB;
	u8 mHookC;

	PADDING(1);

	CQuadBit *mpBit;
};

class CTrapWebEffect : public CNonRenderedBit
{
	public:
		// 0x4F7F40. Type goes into the inherited CBit::mType and selects
		// which CSuper handle slot points back at this effect (field_104
		// for 0, field_10C for 1), the same split CTrapWebEffect::Burst
		// already reads back.
		EXPORT CTrapWebEffect(CSuper *pSuper, i32 Type);

		// 0x4F83C0. Adds one strand of webbing to the effect.
		EXPORT void AddAnotherStrand(void);

		// 0x4F82A0. Advances the imaginary "projector" that shoots the
		// webbing at the baddy: raises/lowers it by field_41F and spins it
		// a sixteenth of a turn.
		EXPORT void MoveProjector(void);

		// 0x4F8190. Fires one ray from the projector at the baddy's
		// vertical axis and, if it hits, records the hit as hook HookIndex.
		EXPORT i32 CalcHook(i32 HookIndex);

		EXPORT void Burst(void);

		SHandle field_3C;

		// The hooks' own line: its mNumSegs is the hook count and its mSegs
		// array holds one hook position per segment (Burst and Move both
		// read it that way). Constructed with 80 segments and immediately
		// reset to 0 segments by the constructor.
		CGPolyLine *field_44;

		// One entry per hook, filled by CalcHook and fed one at a time to
		// M3dUtils_GetDynamicHookPosition by Burst and Move. The
		// constructor zeroes exactly 81 of them (0x48..0x2D0), which is
		// what fixes the size: 80 is the hook limit, plus the one extra
		// hook 0 that AddAnotherStrand lays down before the first strand.
		SHook field_48[81];

		// Projector height (field_420) at the moment each hook was
		// accepted. AddAnotherStrand uses the difference between two
		// entries as the "gap" it looks for when picking the third corner
		// of a strand.
		i16 field_2D0[81];

		PADDING(2);

		// Number of live entries in field_378, capped at 20.
		i32 field_374;

		SWebStrand field_378[20];

		u8 field_418;

		PADDING(1);

		// Both only used by Move (0x4F8860) on the burning (field_418 != 0)
		// variant: an angle that steps 80 units a frame and a 0..256 ramp
		// that steps 8 a frame.
		u16 field_41A;
		i16 field_41C;

		// Frames left before the projector picks a new target height.
		u8 field_41E;

		// Per-frame projector speed, read sign-extended (movsx) at 0x4F82A4.
		i8 field_41F;

		// Projector height above the baddy's origin, in whole units (the
		// world position shifts it left by 12).
		i32 field_420;

		// Projector distance from the baddy's axis, 400 from the ctor.
		i32 field_424;

		// Projector angle, kept in 0..0xFFF.
		i32 field_428;

		// Written by Move (0x4F8860) only: the hook count it last drew.
		i32 field_42C;
};

class CDomeShockWave : public CNonRenderedBit
{
	public:
		EXPORT CDomeShockWave(i32);
		EXPORT virtual ~CDomeShockWave(void);

		EXPORT void ResetHitFlags(CBody*);

		PADDING(8);
		CVector field_44;
		i32 field_50[16];
		i32 field_90;

		PADDING(4);
};

EXPORT int Web_GetGroundY(const CVector*);
EXPORT i32 Web_CollideWithSuper(CSuper *,CVector const *,CVector const *,SHook *,i32);


void validate_CImpactWeb(void);
void validate_CKnottedWebSplat(void);
void validate_CDomePiece(void);
void validate_CDome(void);
void validate_CDomeRing(void);
void patch_web(void);

void validate_CWeb(void);
void validate_CSwinger(void);
void validate_CSplat(void);
void validate_CTrapWebEffect(void);
void validate_CDomeShockWave(void);
void validate_CWebFrag(void);

#endif
