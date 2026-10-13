#pragma once

#ifndef SHELL_H
#define SHELL_H

#include "engine/world/ob.h"
#include "compatibility/export.h"
#include "engine/persistence/save_game_state.h"
#include "engine/assets/reloc.h"
#include "game/application/main.h"
#include "engine/world/bit2.h"

EXPORT extern CBody *MiscList;

// defined in effects.h (CBit-derived); only used here as a pointer member of CDummy.
class CVertexWobble;

// defined in scorpion.h; only used here as a pointer member of CDummy.
struct STailGeometry;
struct SSimpleRibbonParams;
class CSimpleTexturedRibbon;
class CKnottedWeb;
class CShellVenomElectrified;
class CShellCarnageElectrified;
class CShellSuperDocOckElectrified;
class CShellSimbyFireDeath;
class CShellSimbySlimeBase;

// One row of the XA track table a CDummy is built with (constructor parameter a13, kept in
// field_1DC). CDummy::AI plays row 0 on the square button, row 1 on the triangle button, and
// picks one of rows 2..6 at random whenever the idle timer runs out. A row of two zeroes means
// "no track", which is what the random picker skips over.
struct SDummyXATrack
{
	i32 TrackA;
	i32 TrackB;
};

struct SRecordRelated
{
	char* pName;

	PADDING(2);

	u8 field_6;

	PADDING(1);

	i8 field_8;
	i8 field_9;

	PADDING(0xC-0x9-1);

	i8 field_C;

	PADDING(0x10-0xC-1);
};

struct SScore
{
	u8 field_0;
	u8 field_1;
	u8 field_2;
	u8 field_3;
	u8 field_4;
};

#define NUM_CHALLS 23
#define NUM_RECORDS_PER_CHALL 5
struct SRecords
{
	PADDING(3);

	SScore mScores[NUM_CHALLS * NUM_RECORDS_PER_CHALL];
};


class CWobblyGlow : public CGlow
{
	public:
		EXPORT CWobblyGlow(CVector*, i32, i32, i32, u8, u8, u8, u8, u8, u8);
		EXPORT virtual void Move(void);

		i32 mInc[8];
		i32 mT[8];

		i32 mAmplitude;
		i32 mInnerRadius;
};

class CShellMysterioHeadGlow : public CWobblyGlow
{
	public:
		EXPORT CShellMysterioHeadGlow(void);
		EXPORT virtual void Move(void);

		u16 field_A4;

		PADDING(0xA8-0xA4-2);
};

class CRudeWordHitterSpidey : public CSuper
{
public:
	EXPORT CRudeWordHitterSpidey(void);
	EXPORT void AI(void);

	i32 field_1A4;
	i32 field_1A8;

};

// Reverse engineered 2026-08-31 (CDummy_ctor session, functional decompile). CDummy is the 3D
// preview-model object shown by Shell_MainMenu/Shell_CharacterViewer/Shell_CostumeViewer/
// Shell_RollCredits. Its constructor is sub_490DF0 (0x490DF0, 0x746 bytes / 512 instructions);
// its destructor is sub_491560 (0x491560, entered via the small scalar-deleting-destructor thunk
// at 0x491540) and its AI is sub_491A10 (0x491A10, 0x123E bytes -- a separate, much bigger
// function, out of scope for this session, still a stub below, see CDummy::AI).
//
// Field layout from 0x1A4 (CDummy's own region, right after CSuper ends) to 0x240 (where the two
// embedded CItem sub-objects start) was mostly still raw PADDING before this session; every dword
// in that range is written by the constructor or read/deleted by the destructor (cross-checked
// against both, plus Shell_CharacterViewer's own per-mType switch which reads several of the same
// offsets directly). Fields with no evidence of their own name/purpose beyond "the constructor
// writes an argument here" or "the destructor deletes a pointer here" keep the field_XXX
// convention with a comment; nothing here is a guess at final behaviour.
class CDummy : public CSuper {
public:
	EXPORT CDummy(const char* pName, i16 mTypeArg, i16 scale, i32 posY, i32 defaultAnim,
	              u16* pTrackA, u16* pTrackB, u16* pTrackC, u16* pTrackD, u16* pTrackE,
	              i32 a12, i32 a13);
	EXPORT virtual ~CDummy(void) OVERRIDE;
	// 0x491A10, 0x123E bytes. Reached only through the vtable (off_53BFAC slot 2), never by a
	// direct call, so the item list the costume viewer and the main menu previews run is the
	// only way in.
	EXPORT virtual void AI(void) OVERRIDE;

	// 0x495970, the CDummy copy of CScorpion::TailRenderer (Mac symbol
	// .TailRenderer__6CDummyFv). Only caller is Shell_CharacterViewer's per-mType switch.
	EXPORT void TailRenderer(void);

	EXPORT void FadeBack(void);
	EXPORT void FadeAway(void);
	EXPORT void SelectNewTrack(i32);
	EXPORT void SelectNewAnim(void);

	// The Scorpion preview costume (mType 310). Mac symbols
	// .InitialiseTailPSX__6CDummyFv / .InitialiseTailSweepPSX__6CDummyFv /
	// .BuildTail__6CDummyFv / .ScorpionUniformCurveTesselator__6CDummyFP7CVectorUiP7CVector.
	EXPORT void InitialiseTailPSX(void);
	EXPORT void InitialiseTailSweepPSX(void);
	EXPORT void BuildTail(void);
	EXPORT void ScorpionUniformCurveTesselator(CVector* pControl, u32 numPoints,
			CVector* pOut);

	// Doc Ock (mType 308) and monster-Ock (mType 309). Mac symbols
	// .DocOckBuildArms__6CDummyFv / .SuperOckBuildArms__6CDummyFv and the two
	// UniformCurveTesselator overloads, which the PC linker folded onto one body.
	EXPORT void DocOckUniformCurveTesselator(CVector* pControl, u32 numPoints,
			SSimpleRibbonParams* pOut);
	EXPORT void DocOckBuildArms(void);
	EXPORT void SuperOckBuildArms(void);

	// three "track" (animation id list) pointers SelectNewTrack picks randomly between (Rnd(3)).
	u16* field_1A4;
	u16* field_1A8;
	u16* field_1AC;

	// two more constructor-supplied pointers (a10/a11) in the same table row as field_1A4/1A8/1AC
	// (same off_553Dxx parallel-array shape at every call site), but not read by SelectNewTrack or
	// any other currently-decompiled CDummy method -- purpose beyond "another track-like list"
	// unconfirmed.
	u16* field_1B0;
	u16* field_1B4;

	// current track pointer / track start pointer (SelectNewTrack, SelectNewAnim).
	u16* field_1B8;
	u16* field_1BC;
	// default/idle animation id, used when no track list exists (SelectNewTrack, SelectNewAnim).
	i32 field_1C0;

	// Constructor parameter a12. The costume's intro XA track, packed as (TrackA << 16) |
	// TrackB: CDummy::AI hands the two halves straight to Redbook_XAPlay once the model has
	// been on screen for 30 vblanks. Zero means the costume has no intro track, which is also
	// why ~CDummy only calls Redbook_XAStop() when it is nonzero.
	i32 field_1C4;
	// Vblanks value at construction time (spawn timestamp).
	i32 field_1C8;

	// set to 1 by CDummy::AI once the intro track above has been started, so it only fires
	// once. The button-triggered tracks also check it, so nothing plays before the intro.
	i32 field_1CC;

	// Rnd(300) + 300 (a randomised 300..599 value; likely a decay/idle timer, not read by any
	// currently-decompiled CDummy method).
	i32 field_1D0;
	// constant 1, written unconditionally by the constructor.
	i32 field_1D4;

	// Not written by the constructor itself, but Shell_RollCredits (0x4931E0) pokes byte
	// field_1D8=1 directly on its own "spidey" preview CDummy right after construction, same
	// idiom as Front_Init's pYesNoMenu->field_1E poke. Purpose unconfirmed beyond that one
	// caller; the other 3 bytes of this dword are still unconfirmed padding.
	u8 field_1D8;

	// L1+L2+R1+R2 latch. CDummy::AI sets it while all four shoulder buttons are held and
	// clears it when any of them comes up, so the outline fade toggles once per press.
	u8 field_1D9;

	PADDING(2);

	// Constructor parameter a13, stored as-is. It is an SDummyXATrack table (see above);
	// CDummy::AI casts it and reads rows 0, 1 and 2..6 out of it.
	i32 field_1DC;

	// The web strand the spidey preview (mType 50) hangs from during anims 290/291. Created
	// and re-aimed every frame by CDummy::AI, deleted as soon as the anim ends.
	CKnottedWeb* field_1E0;

	// "symbi_02" region CVertexWobble effects spawned for the fire/symbiote costume (mType 324);
	// null for every other costume. Deleted polymorphically in ~CDummy.
	CVertexWobble* field_1E4;
	CVertexWobble* field_1E8;

	// sound/spool handle, -1 by default; set to Spool_PSX("fire", 0) for the fire/symbiote costume.
	i32 field_1EC;

	// The symbiote costume's (mType 324) death effect and the slime puddle under it, both run
	// by CDummy::AI: the puddle exists whenever the death effect does not, and the death
	// effect reports back through its own field_50 when it is finished.
	CShellSimbyFireDeath* field_1F0;
	CShellSimbySlimeBase* field_1F4;

	// FadeAway()/FadeBack() flags, already established before this session.
	i32 field_1F8;
	i32 field_1FC;

	// Venom (mType 313) and Carnage (mType 314) electrified effects, both run by CDummy::AI.
	CShellVenomElectrified* field_200;
	CShellCarnageElectrified* field_204;

	// Carnage's electrified countdown. While it is nonzero the effect above exists and the
	// counter runs down; at zero the effect is dropped and Rnd(200) decides when to restart it.
	i32 field_208;

	// Rhino's (mType 307) one-shot latch for the foot stomp on anim 19 frame 19.
	i32 field_20C;

	// Mysterio costume (mType 311) glow effect. Deleted polymorphically in ~CDummy.
	CShellMysterioHeadGlow* field_210;

	// The four claws and the four tentacle ribbons of the Doc Ock (mType 308) and monster-Ock
	// (mType 309) costumes, built by DocOckBuildArms / SuperOckBuildArms and deleted together
	// by ~CDummy. The claws are chained through mNextItem, so Shell_CharacterViewer renders
	// the whole set from field_214[0]. Not set by the constructor.
	CBody* field_214[4];
	CSimpleTexturedRibbon* field_224[4];

	// constant 455, written for the Scorpion-claw (mType 308) and SuperOck (mType 309)
	// costumes. It is the bezier parameter step DocOckUniformCurveTesselator adds per
	// output point, in 12.12 (455 * 9 == 4095, one short of a whole curve, so the last
	// interior point stops just before the end control point).
	i32 field_234;

	// monster-Ock's (mType 309) electrified effect, and its countdown. Same shape as Carnage's
	// field_204/field_208 pair above.
	CShellSuperDocOckElectrified* field_238;
	i32 field_23C;

	CItem field_240;

	// InitialiseTailPSX sets this to 1 immediately before it points the region's ppModels
	// at mpTailGeometry below. Nothing in the repo reads it. It sits right in front of the
	// model pointer, so it looks like a one entry model list count.
	i32 field_280;


	// Tail geometry buffer for the Scorpion preview model (mType 310), rebuilt every frame by
	// TailRenderer. Exactly the same buffer layout CScorpion::mpTailGeometry uses (scorpion.h).
	STailGeometry* mpTailGeometry;

	CItem field_288;

	// Set by CDummy::AI for the Scorpion preview (mType 310): 1 while the current anim is one
	// of the seven that swing the tail, 0 otherwise. Nothing in the repo reads it yet; the
	// tail sweep renderer is the obvious consumer.
	u8 field_2C8;

	PADDING(3);

	// the same "1" InitialiseTailPSX writes at field_280, written by
	// InitialiseTailSweepPSX in front of field_2D0.
	i32 field_2CC;


	// Second dynamically built geometry buffer, the one that belongs to field_288.
	// InitialiseTailSweepPSX builds it: an SModel header, 92 vertices, no normals and 132
	// untextured quads (the tail sweep trail). Kept untyped because the vertex data is
	// variable length, the same way spool.h leaves SModel's own arrays untyped.
	void* field_2D0;


	// the four control points BuildTail reads out of the model's bones, two curves' worth
	CVector field_2D4[4];

	// the 23 tail node positions, tesselated out of field_2D4 by BuildTail and turned into
	// tail geometry by TailRenderer
	CVector field_304[23];

	// Four generations of the tail node positions, oldest last, rolled along one frame at a
	// time by BuildTail. The banks are 32 CVectors apart and BuildTail copies 24 of each,
	// which is what fixes the shape as [4][32] rather than a flat [128].
	CVector field_418[4][32];
};

// Reverse engineered 2026-08-31 (CheckForPadUnplugged chain, functional decompile session).
// Address of the constructor is 0x48E4B0 (439 bytes, unnamed in names.json); the class name
// comes from the Mac build symbol table (idbs/spiderman_names.txt,
// ".__ct__19CDropDownControllerFv", right before ".CheckForPadUnplugged__Fv" and
// prototypes.json's "CDropDownController::CDropDownController((void)): 540"). This is the
// little UI widget CheckForPadUnplugged (shell.cpp) shows on screen: two "cweb" CQuadBit
// sprites joined by a CKnottedWeb strand, dropped down from the top of the screen with a
// damped bounce (CDropDownController::AI) whenever a controller gets unplugged mid-game.
// This class derives from CSuper (base ctor confirmed via ??0CSuper@@QAE@XZ at 0x460720),
// and reuses several inherited CItem/CBody fields as scratch animation counters instead of
// declaring new ones (mPos.vy/vz for the drop height, mAngles.vy/vz for the wobble angle,
// mVel.vy for the fall speed) -- same idiom already seen elsewhere in this codebase
// (CBody::field_98/9C/A0 for XA sound timers).
class CDropDownController : public CSuper
{
public:

	EXPORT CDropDownController(void);
	EXPORT virtual ~CDropDownController(void) OVERRIDE;
	EXPORT virtual void AI(void) OVERRIDE;
	EXPORT void SetWebPositions(void);

	// drop-wobble accumulator/speed (CDropDownController::AI, mState 1/2)
	i32 mPhase;
	i32 mSpeed;

	// mAngles.vy oscillation accumulator (CDropDownController::AI, runs every state)
	i32 mWobblePhase;

	// continuous small shake amplitude/phase; mShakeFlag is read but never written by any
	// function in this chain, so its setter is out of scope (probably a rumble/vibration
	// check elsewhere in the binary)
	i32 mShakeAmp;
	i32 mShakePhase;
	i32 mShakeFlag;

	CQuadBit *mpFrame0;
	CQuadBit *mpFrame1;

	// top attach point for the web, computed once in the constructor via
	// M3dUtils_GetDynamicHookPosition
	CVector mTopAnchor;

	CKnottedWeb *mpWeb;

	// 0 = dropping, 1 = just landed (starts the wobble), 2 = wobble settled
	i32 mState;
};

class Spidey_CIcon : public CSuper
{
	public:
		EXPORT Spidey_CIcon(i32, i32, i32);
		EXPORT Spidey_CIcon(i32);
		EXPORT void AI(void);
		EXPORT void SetIcon(i32);
};

// Reverse engineered 2026-08-31 (functional decompile session). Constructed via a shared
// idiom in Shell_ComicCollection (0x49B270), Shell_GameCovers (0x49C220, six instances) and
// Shell_MainMenu (sub_493990, confirmed via xrefs_to on off_53BFC0): operator new(420) ->
// CSuper::CSuper() -> vtable pointer overwritten from CSuper's own vtable (off_53BBE8) to a
// separate vtable (off_53BFC0) -> CItem::InitItem(this, "items") -> caller-supplied mPos
// (each coordinate pre-shifted <<12) plus hardcoded mModel=5, mFlags=(mFlags&0xFB7D)|0x200,
// mpLight=0, mScale=(2048,2048,2048). This is byte-for-byte the same field-assignment
// sequence as Spidey_CIcon::Spidey_CIcon(i32,i32,i32) above (same base fields, same constant
// values), but it is a distinct C++ type: off_53BFC0 is a different vtable from Spidey_CIcon's
// own, confirmed by diffing off_53BFC0 against CSuper's own vtable (off_53BBE8) slot by slot.
// CSuper/CBody/CItem only contribute 5 virtual slots (~CItem, Die, AI, Hit, DeleteStuff, in
// that vtable order); off_53BFC0 matches off_53BBE8 in the Die/Hit/DeleteStuff slots (all
// inherited unchanged) and differs only in the destructor and AI slots:
//  - destructor (0x493830, thunk; real body 0x493850): trivial compiler-generated derived
//    dtor, just resets the vtable pointer back to off_53BFC0 (standard MSVC pattern for a
//    derived class whose own destructor body is empty -- no added fields means no cleanup of
//    its own) then tail-calls the inherited CSuper::~CSuper (0x460780; names.json's
//    "CBaddy::~CBaddy" label on that same address is a link-time identical-body fold per
//    CLAUDE.md, not a real relationship -- CBaddy is a different, larger, unrelated class).
//  - AI (0x493970): this->mAngles.vy += 50 every frame (a constant spin, matching this
//    class's use as an on-screen rotating 3D preview icon for comic/game-cover art and the
//    main menu item), and only if mFlags&2 is set, this->UpdateFrame() then
//    M3d_BuildTransform(this). mFlags bit 2 is explicitly cleared by the constructor's
//    mFlags=(mFlags&0xFB7D)|0x200 (0xFB7D excludes bit 2, and 0x200 doesn't set it either), so
//    that branch is dead for every instance seen in the three known call sites -- reproduced
//    for fidelity to the original anyway, since some other unseen call site could plausibly
//    set the bit before AI() runs.
// No new fields: same size as CSuper (VALIDATE_SIZE(CSuper, 0x1A4) in ob.cpp). Name is our own
// guess (no matching class name found in idbs/spiderman_names.txt near the comic/cover/menu
// preview functions there -- DrawSmallComic/DrawSmallGameCover/Shell_ComicCollection/
// Shell_GameCovers all appear, but not a wrapping class name for this icon object).
class CShellPreviewIcon : public CSuper
{
	public:
		EXPORT CShellPreviewIcon(i32, i32, i32);
		EXPORT virtual ~CShellPreviewIcon(void) OVERRIDE;
		EXPORT virtual void AI(void) OVERRIDE;
};

class CShellSymBurn : public CSuper
{
	public:
		EXPORT CShellSymBurn(CVector*);
		EXPORT void AI(void);

		i32 field_1A4;
};

class CShellVenomElectrified : public CNonRenderedBit
{
	public:
		EXPORT CShellVenomElectrified(CSuper*);
		EXPORT virtual void Move(void);

		SHandle field_3C;
		i32 field_44;
};

class CShellCarnageElectrified : public CNonRenderedBit
{
	public:
		EXPORT CShellCarnageElectrified(CSuper*);
		EXPORT virtual void Move(void);

		SHandle field_3C;
		i32 field_44;
};

class CShellSuperDocOckElectrified : public CNonRenderedBit
{
	public:
		EXPORT CShellSuperDocOckElectrified(CSuper*);
		EXPORT virtual void Move(void);

		SHandle field_3C;
		i32 field_44;
};

class CShellRhinoNasalSteam : public CFlatBit
{
	public:
		EXPORT CShellRhinoNasalSteam(CVector*, CVector*);
		EXPORT virtual void Move(void);
};

class CShellEmber : public CFlatBit
{
	public:
		EXPORT CShellEmber(CVector*, i32);
		EXPORT virtual void Move(void);

		i32 field_68;
		i32 field_6C;
		i32 field_70;
		i32 field_74;
		i32 field_78;
		i32 field_7C;
		i32 field_80;
		i32 field_84;
		i32 field_88;
		i32 field_8C;
};

class CShellSimbyMeltSplat : public CQuadBit
{
	public:
		EXPORT CShellSimbyMeltSplat(CVector*);
		EXPORT virtual void Move(void);


		i32 field_84;
		i32 field_88;
		i32 field_8C;
		CVector field_90;
		CVector field_9C;
		CVector field_A8;
};

class CShellSimbyFireDeath : public CNonRenderedBit
{
	public:
		EXPORT CShellSimbyFireDeath(CDummy*);

		SHandle field_3C;
		CVector *field_44;

		PADDING(8);

		// "finished" flag. The constructor at 0x4908E0 never touches it (so its Move, which is
		// not decompiled yet, must set it); CDummy::AI polls it every frame and deletes the
		// effect as soon as it goes nonzero. Was the tail of a blanket PADDING(0xC).
		i32 field_50;
};

// Reverse engineered from the constructor at 0x48ED80 (names.json calls it
// CTailRing_CTailRing). One ring of the Scorpion stinger explosion CScorpExplosion builds: a
// plain CBody on the "scimpact" region whose own AI (vtable off_53BF10, not decompiled here)
// grows it. Every field below is written by that constructor; the two gaps are not.
class CTailRing : public CBody
{
	public:
		EXPORT CTailRing(CVector* pPos, i32 a3, i32 a4, i32 a5, i32 a6, i32 a7,
				u8 a8, u8 a9, i32 a10, i32 a11);

		PADDING(4);

		i32 field_F8;

		PADDING(4);

		i32 field_100;
		i32 field_104;
		i32 field_108;
		i32 field_10C;
		i32 field_110;

		PADDING(4);

		i32 field_118;
		i32 field_11C;

		// (a6 << 12) / 450, the only value the constructor computes rather than copies
		i32 field_120;
};

// Reverse engineered from the constructor at 0x48F040 (names.json calls it
// CScorpExplosion_CScorpExplosion_0). The burst CDummy::AI fires for the Scorpion preview
// (mType 310) on anim 29 frame 4: two CTailRings, a CGrenadeWave and ten CPingLines thrown out
// in random directions. It keeps handles to the two rings and nothing else.
class CScorpExplosion : public CNonRenderedBit
{
	public:
		EXPORT CScorpExplosion(CVector* pPos);

		SHandle field_3C;
		SHandle field_44;
};

// Reverse engineered from the constructor at 0x48F8A0 and SetQuadCoords at 0x48FD50 (both
// named that way in names.json). The slime puddle under the symbiote costume preview (mType
// 324). It is one CQuadBit itself plus three more it owns, so the puddle is a 2x2 grid of
// quads sharing a centre point; the four outer corners live in field_C0 and SetQuadCoords
// spreads them and their midpoints over the four quads.
class CShellSimbySlimeBase : public CQuadBit
{
	public:
		EXPORT CShellSimbySlimeBase(CVector* pPos, CSVector* pAngles, i32 a4);
		EXPORT void SetQuadCoords(void);

		// the puddle centre and the angles it was built at, both copies of the constructor
		// arguments
		CVector field_84;
		CSVector field_90;

		PADDING(6);

		// constructor argument a4 (CDummy::AI passes 256). Not read by the constructor or by
		// SetQuadCoords, so it must be for the Move that is not decompiled yet.
		i32 field_9C;

		PADDING(0xB4 - 0x9C - 4);

		// the other three quads of the puddle
		CQuadBit* field_B4;
		CQuadBit* field_B8;
		CQuadBit* field_BC;

		// the four outer corners, in the order (-u-v, +u-v, -u+v, +u+v) around the centre
		CVector field_C0[4];

		// per-corner wobble phase (Rnd(4096)) and speed (Rnd(120) + 30)
		i32 field_F0[4];
		i32 field_100[4];
};

class CShellGoldFish : public CBody
{
	public:
		EXPORT CShellGoldFish(CDummy*);
		EXPORT virtual ~CShellGoldFish(void);
		EXPORT virtual void AI(void);

		PADDING(4);

		SHandle field_F8;
		i32 field_100;
		i32 field_104;
		i32 field_108;
		i32 field_10C;
		i32 field_110;
		i32 field_114;
};

class CShellMysterioHeadCircle : public CQuadBit
{
	public:
		EXPORT CShellMysterioHeadCircle(CDummy*);
		EXPORT virtual ~CShellMysterioHeadCircle(void);
		EXPORT virtual void Move(void);

		SHandle field_84;

		// Real field, not padding: read/written by Move (this+35 in the original,
		// i.e. offset 0x8C) as a plain i32 phase accumulator, right before
		// field_90 (this+36, the per-frame increment added to it).
		i32 field_8C;

		i32 field_90;
};

// Element size 0x28 is proven by Spidey_CIcon::SetIcon (0x493860), which scales the
// index with "lea edi,[edi+edi*4]; shl edi,3" (index * 5 * 8). The two i16 fields are
// read 16 bits at a time there ("mov cx,[edi+552AC0h]"), so the halves above them are
// alignment padding; they are zero in every entry of the real table at 0x552AB8.
// The four fields from 0x18 on are never read anywhere in the PC build (no code
// reference to any address inside 0x552AB8..0x552BE8 other than offsets 0x0..0x14),
// but the table does carry data there: an x/y screen position, a menu caption string
// and a menu option id. Kept as field_* because nothing confirms that reading.
// 0x00553D18, 27 entries of 0x44 bytes each (the table ends at 0x00554444, which is exactly
// where Shell_CharacterViewer's own loop bounds stop). One row per character-viewer entry.
// Field purposes come from the two consumers: Shell_CharacterViewer (0x004962D0) reads every
// field, and Shell_DoShell (0x004A1B44) is the only writer, filling Description at runtime
// (it is 0 in the image, like the costume viewer's description table).
struct SCharacterEntry
{
	// menu label, e.g. "spider-man"
	char* Name;
	// CDummy model name, e.g. "spidey". Empty string means the entry cannot be selected.
	char* ModelName;
	// CItem mType of the preview model, also the CDummy constructor's second argument
	i32 Type;
	// description text block, filled in at runtime by Shell_DoShell
	char* Description;
	// starting camera distance, and the clamp/step used by the zoom in/out triggers
	i32 Zoom;
	i32 MinZoom;
	i32 MaxZoom;
	i32 ZoomStep;
	// the remaining CDummy constructor arguments (4, 5 and 6 through 12); the third
	// argument, the scale, is the constant 4096 at both call sites
	i32 PosY;
	i32 DefaultAnim;
	u16* TrackA;
	u16* TrackB;
	u16* TrackC;
	u16* TrackD;
	u16* TrackE;
	i32 CtorA12;
	i32 CtorA13;
};

struct SpideyIconRelated
{
	char *Name;
	i32 IconModel;
	i16 field_8;
	i16 pad_A;
	i16 field_C;
	i16 pad_E;
	i32 field_10;
	i32 field_14;
	i32 field_18;
	i32 field_1C;
	char *field_20;
	i32 field_24;
};

// Size 0x10 confirmed by PShell_EndTrainingDisplay (pshell.cpp): the game's
// gChallenges table (0x00551118, named in idb_globals.txt) is 23 entries of
// this struct back to back, ending exactly at the next named global
// (gTrainingSeconds, 0x00551288: 0x551118 + 23*0x10 == 0x551288).
struct STrainingMission
{
	// tentative: read as a char* by Shell_ShowRecord (Mess_DrawText title arg)
	// and by PShell_EndTrainingDisplay through CRecordBox::field_3C (the
	// challenge/mission name).
	char* field_0;

	// Added 2026-08-27, found in PShell_EndTrainingInit (0x47B720, pshell.cpp):
	// the entry-search loop there reads this word for every gChallenges
	// entry (cmp word ptr [&gChallenges[i]+4], ax) and compares it against
	// the low 16 bits of Trig_GetLevelID()'s return value, still resident in
	// ax at that point. Only entries whose mLevelId matches get checked
	// further (against mAreaId below). Our own guess at the field's
	// purpose; not confirmed against the maintainer's IDB.
	i16 mLevelId;

	PADDING(0x7 - 0x6);

	// Added 2026-08-27, found in PShell_EndTrainingInit (0x47B720, pshell.cpp):
	// the entry-search loop there reads this byte for every gChallenges
	// entry (mov dl,[ecx] with ecx = &gChallenges[i] + 7) and compares it
	// against gTrainingSeconds (0x00551288, the value read once at the top
	// of that function) or the sentinel 0xFF (matches any). This picks
	// which gChallenges entry is "the one for the level that just ended".
	// Our own guess at the field's purpose (which training area/level this
	// challenge belongs to); not confirmed against the maintainer's IDB, no
	// per-field struct names are available from the 9.2 IDB export.
	i8 mAreaId;

	PADDING(0xB - 0x7 - 1);

	// renamed from field_B (2026-08-27): print_if_false in CRecordBox::Display
	// (0x47B240) reads this same byte through CRecordBox::field_3C and asserts
	// it with the literal string "Bad ScoreUnits" (confirmed by reading the
	// .data section of the original SpideyPC.exe at 0x551A80, the string the
	// assert pushes). Sign-extended, passed as DisplayScore's last argument;
	// used as a 4-way switch in Display (0=Time, 1=Kills, 2=Items, 3=Points
	// column header, strings at 0x54B8D0/D4/D8/DC in the original .data).
	i8 mScoreUnits;

	// Added 2026-08-27, found in PShell_EndTrainingInit (0x47B720, pshell.cpp):
	// the score-insertion loop there reads this byte (gChallenges[i]+0xC) once
	// per challenge and uses it to pick the comparison direction against the
	// new score (flag==0: insert where the new score is greater, "jg";
	// flag!=0: insert where the new score is less, "jl"). Our own guess:
	// whether a lower value ranks better for this challenge (true for
	// mScoreUnits==0, Time, false otherwise). Not confirmed against the
	// maintainer's IDB.
	i8 mLowerIsBetter;

	PADDING(0x10 - 0xC - 1);
};

// Widget class from the "pshell" Mac module. Moved here from shell.cpp (was
// file-local, comment said "only caller found this session") because
// pshell.cpp's PShell_EndTraining* functions use a second CRecordBox instance
// (own global pointer, own STrainingMission source) for the end-of-training
// score display, so the class is shared between the two TUs now. Derives from
// CClass: the constructor never calls a base ctor (CClass has none) and never
// sets up more than one vtable slot, matching Shell_ShowRecord's cleanup call
// (vtable[0](1), the scalar deleting destructor; CClass::operator new/delete
// already cover the alloc/free side). Field layout is read off the
// constructor's writes (sub_47B1E0, the 96 bytes right before Display in the
// original) plus Display (0x47B240) and Update (0x47B560), both confirmed by
// address in tools/names.json.
// 2026-08-27 correction: an earlier session's comment here attributed
// __ct__10CRecordBoxFiiP16STrainingMission/__dt__10CRecordBoxFv/
// NameEntryOn__10CRecordBoxFUc to specific addresses "from spiderman_names.txt"
// (the Mac IDB). That IDB actually has these at 0xc7b60/0xc7c10/0xc8320 (checked
// against idbs/spiderman_names.txt directly), nothing like the PC-looking hex
// quoted before. The quoted addresses were real PC addresses (0x47B1E0 is the
// ctor, confirmed working) but 0x47AF00 and 0x47B830 are NOT the destructor or
// NameEntryOn: 0x47AF00 is a tiny unrelated sub inside the neighbouring
// CExpandingBox functions, and 0x47B830 falls inside PShell_EndTrainingInit's
// own byte range. Left ~CRecordBox and NameEntryOn as todo stubs rather than
// decompiling against the wrong bytes; their real PC addresses are still
// unconfirmed as of this note.
// 2026-08-31 re-attempt (~CRecordBox is now done, only NameEntryOn left):
// ruled out several more candidate locations. IDA's own function-boundary
// detection confirms 0x47B560 (Update, 434 bytes per tools/functions) runs
// right up to 0x47B712, then 16 bytes of alignment, then 0x47B720 starts
// straight into PShell_EndTrainingInit's real prologue (push ebx/ebp/esi/
// edi) with no room for an 80-byte function (Mac size, prototypes.json,
// "CRecordBox::NameEntryOn((uchar))": 80) in between. The string "Bad row
// sent to NameEntryOn()" (0x551AA4) that IDA's function-boundary detector
// attributes to sub_47B720 is misleading: that address is confirmed
// PShell_EndTrainingInit (exact 812-byte size match against tools/
// functions/4699936.bin), so the string is just reused debug text inside
// that function, not evidence of NameEntryOn's own location. Also checked
// whether NameEntryOn is actually called anywhere on PC: it is not, not
// from CRecordBox::CRecordBox (pshell.cpp, plain field-store ctor, no
// calls), not from Shell_ShowRecord (shell.cpp, only calls ->Display()),
// not from PShell_EndTrainingUpdate (pshell.cpp, only calls ->Update()).
// Likely dead code the PC port dropped (matches the Shell_ChooseItem
// Collection/ChooseSpeedTraining/ChooseTrainingMission pattern above:
// Mac-only symbols with no live PC call site). Left as a stub.
// field_30..3B layout (2026-08-27, read off Update/Display): field_30 is read
// as a plain 4-byte int (both functions test it non-zero before doing
// anything, most likely a CExpandingBox-style "active/visible" flag rather
// than something CRecordBox owns outright -- see the CExpandingBox comment on
// the constructor). field_38 is the one CRecordBox::Update's print_if_false
// names "Bad mLetterIndex" via the string at 0x551A90 in the original .data,
// confirmed the same way as mScoreUnits above: the current typed letter
// position (0..2) into the 3-letter high score name. field_39 is the currently
// selected score row (0..NUM_RECORDS_PER_CHALL-1), no string evidence for a
// name yet. field_34/35/36 drive Display's row-highlight blink and have no
// string evidence either. field_37/3A/3B are never read or written by
// Update/Display; kept as padding.
class CRecordBox : public CClass
{
	public:
		EXPORT CRecordBox(i32, i32, STrainingMission*);
		EXPORT virtual ~CRecordBox(void);
		EXPORT void Display(void);
		EXPORT void Update(void);
		EXPORT void NameEntryOn(u8);

		i32 field_4;
		i32 field_8;
		i32 field_C;
		i32 field_10;
		i32 field_14;
		i32 field_18;
		i32 field_1C;
		i32 field_20;
		i32 field_24;
		i32 field_28;
		i32 field_2C;
		i32 field_30;
		u8 field_34;
		u8 field_35;
		u8 field_36;
		PADDING(1);
		u8 mLetterIndex;
		u8 field_39;
		PADDING(2);
		STrainingMission* field_3C;
		i32 field_40;
};

enum EShellResult
{
};

class CMenu;

static const i32 INPUT_MAX_SIZE = 9;

EXPORT i32 Shell_DeRudify(char[INPUT_MAX_SIZE]);
EXPORT i32 Shell_ContainsSubString(const char*, const char*);
EXPORT void CallAI(CBody *);
EXPORT i32 CalcIndexOfContinueLevel(void);

EXPORT void Shell_AddGameSlots(CMenu *);
EXPORT u32 Shell_CalculateGameChecksum(SSaveGame *);
EXPORT void Shell_CharacterViewer(void);
EXPORT void Shell_Cheats(void);
EXPORT i32 Shell_ChooseEnemy(i32,u8,signed char);
EXPORT void Shell_ChooseItemCollection(i32);
EXPORT void Shell_ChooseSpeedTraining(i32);
EXPORT void Shell_ChooseSurvivalArena(i32);
EXPORT void Shell_ChooseTime(i32,i32);
EXPORT i32 Shell_ChooseTrainingControlType(void);
EXPORT void Shell_ChooseTrainingMission(i32);
EXPORT void Shell_DrawComicHighlightBox(i16,i16,SAnimFrame*,i32);
EXPORT void Shell_ComicCollection(void);
EXPORT void Shell_CostumeViewer(void);
EXPORT void Shell_DrawKiddy(SAnimFrame*, i32, i32, i32, i32);
EXPORT i32 Shell_Difficulty(i32);
EXPORT void Shell_DisplayGameInfo(i32,i32,SSaveGame *);
EXPORT void Shell_DoShell(const u32 *,u32 *);
EXPORT void Shell_DrawBackground(void);
EXPORT void Shell_DrawTitleBar(i32,i32,const char *,i32,i32,i32,i32,i32);
EXPORT i32 Shell_Gallery(EShellResult);
EXPORT void Shell_DrawGameCoverHighlightBox(i16,i16,SAnimFrame*,i32);
EXPORT void Shell_GameCovers(void);
EXPORT i32 Shell_InputName(char *,i32,i32, const char *);
EXPORT void Shell_LegalScreen(void);
EXPORT i32 Shell_LevelSelect(void);
EXPORT i32 Shell_LoadGame(void);
EXPORT i32 Shell_MainMenu(EShellResult);
EXPORT i32 Shell_MemoryCard(EShellResult);
EXPORT void Shell_MovieViewer(void);
EXPORT i32 Shell_Options(EShellResult);
EXPORT void Shell_RollCredits(void);
EXPORT void Shell_SFXMusic(void);
// helpers used by Shell_SFXMusic (forwarded to original)
EXPORT void DrawSlider(i32, i32, i32, i32);
EXPORT i32 SliderDrag(i32, i32, i32);
EXPORT void Shell_SaveGame(const u32 *,u32 *);
EXPORT void Shell_ScreenAdjust(void);
EXPORT void Shell_ShowRecord(char const *,char const *,STrainingMission *);
EXPORT i32 Shell_Special(EShellResult);
EXPORT void Shell_StoryBoards(void);
EXPORT void Shell_TitleScreen(void);
EXPORT void Shell_VerySmallFont(void);
EXPORT void PShell_Cleanup(void);
EXPORT void PShell_Initialise(void);
EXPORT void PShell_LowText(void);
EXPORT i32 RecordsExist(u8,i8, i8);
EXPORT i32 SameScore(const SScore *,const SScore *);
EXPORT void Merge(SRecords *, const SRecords *);
EXPORT void Merge(SScore *, const SScore *,i32);
EXPORT i32 IsBetter(i32, i32, i32);

EXPORT void Shell_RelocatableModuleInit(reloc_mod *);

EXPORT extern SAnimFrame* gBackgroundAnimFrame;

void validate_CRudeWordHitterSpidey(void);
void validate_CDummy(void);
void validate_CDropDownController(void);
void validate_CWobblyGlow(void);
void validate_CShellMysterioHeadGlow(void);
void validate_Spidey_CIcon(void);
void validate_CShellPreviewIcon(void);
void validate_CShellSymBurn(void);
void validate_CShellVenomElectrified(void);
void validate_CShellCarnageElectrified(void);
void validate_CShellSuperDocOckElectrified(void);
void validate_CShellRhinoNasalSteam(void);
void validate_CShellEmber(void);
void validate_CShellSimbyMeltSplat(void);
void validate_CShellSimbyFireDeath(void);
void validate_CTailRing(void);
void validate_CScorpExplosion(void);
void validate_CShellSimbySlimeBase(void);
void validate_CShellGoldFish(void);
void validate_CShellMysterioHeadCircle(void);
void validate_SCharacterEntry(void);
void validate_SpideyIconRelated(void);
void validate_SSaveGame(void);
void validate_SScore(void);
void validate_SRecords(void);
void validate_STrainingMission(void);
void validate_SRecordRelated(void);
void validate_CRecordBox(void);

#endif
