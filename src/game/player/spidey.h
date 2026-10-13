#pragma once


#ifndef SPIDEY_H
#define SPIDEY_H

#include "compatibility/export.h"
#include "engine/world/ob.h"
#include "game/objects/manipob.h"
#include "core/quaternion.h"
#include "compatibility/psx_types.h"
#include "engine/world/collision.h"

struct SAnimFrame;

EXPORT extern u8 gSpideyPsxIndex;

struct SIndicator
{
	// direction (already local-space, normalized) from the player towards
	// the offscreen threat this entry represents; written by
	// CPlayer::BuildOffscreenSpideySenseIndicatorList.
	CVector mDirection;

	SHandle field_C;

	// The four flat triangles this indicator draws: index 0 is the arrow at
	// its position this frame, 1..3 hold the three previous frames'
	// positions, which CPlayer::DrawOffscreenSpideySenseIndicatorList shifts
	// along every call to make a short motion trail. Only the vertex fields
	// are shifted; the colour and code bytes live in entry 0.
	POLY_F3 mPoly[4];

	// age of this entry in frame ticks (CSuper::field_80 is added per draw).
	// Below 30 the arrow fades/slides in, from 30 on it is parked and the
	// trail is retired one triangle at a time.
	i32 mInUse;
};

// One entry of CPlayer's active-combo part list (CPlayer+0x95C). Built by
// CPlayer::InitiateCombo from the parts array of the move record and walked
// every tick by CPlayer::UpdateAndTrackCombo, which matches the player's
// button presses against each part's input stream. The list is terminated
// by an entry whose mInput is null, which is why the array has one slot
// more than the 16 parts a move can have.
struct SComboPart
{
	// entry is in use (InitiateCombo sets it, UpdateAndTrackCombo clears it
	// when the part is dropped).
	u8 mActive;

	// still waiting for the first press of this part's input stream.
	u8 mWaitingFirst;

	// the part's move record has a nonzero field at 0x22; picks which of the
	// two frame windows (field_902/field_904 or field_906/field_908) the
	// press has to fall in.
	u8 mUseLateWindow;

	// set once the part's first input has been matched.
	u8 mStarted;

	// field_84 (the animation clock) at the last accepted press.
	i32 mLastPressTime;

	// 0, or 768 when the parts entry had its high word set. Purpose not
	// confirmed; InitiateCombo is the only writer found so far.
	i32 mFlags;

	// cursor into the part's input byte stream (the move record's tail
	// pointer to start with), 0xFF terminated. Null marks the end of the
	// list.
	u8 *mInput;
};

class CPlayer : public CSuper 
{
	public:
        CSVector& crawlSurfaceNormal() { return field_A8; }
        const CSVector& crawlSurfaceNormal() const { return field_A8; }


		// CPlayer::AI: nonzero gate for the submariner-die check
		// (0x1A4 must be nonzero, along with mSynthesizedInputActive, to run the check).
		u8 field_1A4;

		PADDING(3);

		// Trigger node used by conditional end-of-script checkpoint restoration.
		i32 mScriptCheckpointNode;
		// Use cutscene/demo script input in place of physical player input.
		char mSynthesizedInputActive;

		PADDING(0x1B0-0x1AC-1);

		// elapsed-time accumulator for SynthesizeAnalogueInput's bytecode
		// VM (mElapsedTicks added per tick), same role as
		// CSpClone::field_340/CBlackCat's analog.
		i32 mScriptElapsedTicks;

		// phase-1 (bytecode) active flag for SynthesizeAnalogueInput,
		// same role as CSpClone::field_344.
		u8 mScriptBytecodeActive;

		PADDING(3);

		// bytecode stream pointer (cast to i16* at use sites) for
		// SynthesizeAnalogueInput, same role as CSpClone::field_348.
		i32 mScriptCursorAddress;

		// Variable-length concurrent command list; next pointer is its last word.
		i32* mScriptCommandHead;

		PADDING(0x201-0x1BC-4);

		// set to 1 by CheckJumpingSwingWeb once a swing web is committed.
		u8 field_201;

		PADDING(0x211-0x201-1);

		// set by ReadAnalogueInput when the move input returns to centre
		// while field_AD8 was pending.
		u8 field_211;

		PADDING(0x2C1-0x211-1);

		// cleared at the top of CheckJump on every jump-button edge
		u8 field_2C1;

		PADDING(0x2E1-0x2C1-1);

		// both cleared by CheckKick on the kick/punch button edge
		u8 field_2E1;

		PADDING(0x2F1-0x2E1-1);

		u8 field_2F1;

		PADDING(0x34C-0x2F1-1);

		// ProcessSFXArray: non-zero selects the (Rnd(4)+80)|0x8000 SFX range,
		// zero selects the Rnd(4)+1 range, in the mAnim 0x15/0xC0/0xC6 case.
		i32 field_34C;

		i32 *field_350;

		u8 field_354;
		PADDING(3);

		i32 field_358;
		u32 field_35C;

		// CPlayer::AI: three-value rolling accumulator for the 1365x shift
		// (field_360 = last tick delta, field_364 = tick before that,
		// field_368 = (360+364+field_80) * 1365).
		i32 field_360;
		i32 field_364;
		i32 field_368;

		// CPlayer::Hit gate: any nonzero value makes the player immune (the
		// hit is dropped before anything else happens).
		i32 field_36C;

		PADDING(0x374-0x36C-4);

		// gTimerRelated when the web dome was thrown (CheckWebShot).
		i32 field_374;

		// set to 1 by CPlayer::InitiateCombo and cleared again by the first
		// UpdateAndTrackCombo tick of the move: while it is set the collision
		// parts snapshot their current position instead of sweeping.
		u8 field_378;

		PADDING(0x37C-0x378-1);

		// world position of each active collision part this tick, refreshed
		// by UpdateAndTrackCombo with M3dUtils_GetDynamicHookPosition. The
		// collision parts stream (field_954) indexes it, so at most 16 are
		// ever used before field_43C starts.
		CVector field_37C[16];

		// the same 16 positions from the previous tick; the pair gives the
		// swept segment UpdateAndTrackCombo hands Web_CollideWithSuper.
		CVector field_43C[16];

		PADDING(0x500-0x43C-16*12);

		// gTimerRelated at the moment of the last hit, and the mActionState
		// state the player was in when it landed. Both written by
		// CPlayer::Hit, nothing in the repo reads them back yet.
		u32 field_500;
		i32 field_504;

		// electrocution: set for hit type 26, with field_50C as the
		// countdown (120) that goes with it.
		u8 field_508;

		PADDING(3);

		i32 field_50C;

		PADDING(0x514-0x510);

		// zeroed by CPlayer::CPlayer; a position/angle pair, nothing in the
		// repo writes them again yet.
		CVector field_514;
		CSVector field_520;

		PADDING(0x528-0x520-6);

		i32 field_528;
		i32 field_52C;
		i32 field_530;

		// set to 240 by CPlayer::Hit alongside field_52C.
		i32 field_534;

		u32 field_538;

		// CPlayer::AI: nonzero gate for the one-time floor-camera setup
		// (0x53C is set to 1 after the first AI tick that ran the camera code).
		u8 field_53C;

		PADDING(3);

		i32 field_540;

		// 0, 1 or 2, picked from the sign of field_E2E when the left web
		// shot starts (CheckWebShot).
		i32 field_544;

		// twist-around-Y correction angle (PS1 GTE units), applied on top of
		// the normal-aligned basis by CPlayer::OrientToNormal via
		// M3dMaths_RotMatrixYXZ + MulMatrix when nonzero.
		i32 mHeadingDelta;

		u8 field_54C;

		// cleared together with field_54C when the player lands or starts a
		// jumping smash kick (CPlayer::CheckLanded, CheckJumpingSmashKick).
		u8 mPendingWebReleaseAnimation;

		PADDING(0x54F-0x54D-1);

		// set nonzero by some other (not yet decompiled) caller to request a
		// zip-web/switch/swing-web lock-on the next time
		// CPlayer::SetupLookaroundCamera runs; SetupLookaroundCamera reads
		// it to gate the lock-on and always clears it back to 0 before
		// returning. offset/evidence: IDA disasm of SetupLookaroundCamera
		// (0x4C38A0), "cmp [ebp+54Fh], bl" / "mov [ebp+54Fh], bl".
		u8 field_54F;

		// blocks CheckJumpingSwingWeb while set.
		u8 field_550;

		// cleared by CheckForwards whenever it retargets the torso angle
		u8 field_551;

		// cleared whenever CheckWebShot starts a web shot animation.
		u8 field_552;

		// cleared by CPlayer::CPlayer.
		u8 field_553;

		// CPlayer::AI: free-function callback, called as field_554(this)
		// near the end of every AI tick when nonzero.
		void (*field_554)(CPlayer*);

		// player-position snapshot taken by CPlayer::SetupLookaroundCamera
		// right before playing the zip-web-target lock-on animation
		// (offset 0x558, IDA disasm of 0x4C38A0).
		CVector field_558;

		// cleared by CPlayer::CheckSwitchToGrabbedMode when the grab starts
		u8 field_564;

		PADDING(0x568-0x564-1);

		i32 field_568;
		i32 field_56C;

		u32 field_570;

		// seeded by CPlayer::CPlayer to 208 / 160 / 256 next to field_570's
		// 208; trig.cpp reaches 0x574 / 0x578 by raw offset as the shadow
		// RGB / body RGB pair.
		i32 field_574;
		i32 field_578;

		i8 field_57C;

		PADDING(0x580-0x57C-1);

		i32 field_580;
		CSmokeTrail* field_584;
		CSmokeTrail* field_588;
		CSmokeTrail* field_58C;
		CSmokeTrail* field_590;

		// Per-hook previous trail position (CVector) used by UpdateTrails to
		// compute intermediate trail steps. [0] = hook 1, [1] = hook 0.
		CVector field_594[2];

		// counts down while a "double damage" combo bonus is active
		// (UpdateAndTrackCombo halves it away one hit at a time); cleared by
		// PriorToVenomDistanceAttack together with field_5B0.
		i32 field_5AC;
		i32 field_5B0;

		// gTimerRelated of the last web shot; CheckWebShot refuses to fire
		// again until more than 30 ticks have passed.
		i32 field_5B4;

		// two extra body parts (the fists, created by CPlayer::CreateFists)
		// hanging off SpideyAdditionalBodyPartsList; ~CPlayer unlinks and
		// deletes both.
		SHandle field_5B8[2];

		// round-robin cursors into the two swing-web probe angle tables,
		// see CPlayer::CheckJumpingSwingWeb. Both count 0..5 and wrap.
		i32 field_5C8;
		i32 field_5CC;


		i32 field_5D0;
		i32 mWebbing;
		i32 field_5D8;
		i32 field_5DC;

		i32 field_5E0;

		// SFX handle, stopped (SFX_Stop) and cleared by
		// SynthesizeAnalogueInput's opcode 1 (teleport) handler.
		i32 field_5E4;

		char field_5E8;
		bool field_5E9;

		PADDING(0x5EC-0x5E9-sizeof(bool));
		
		i32 field_5EC;

		SIndicator field_5F0[6];


		PADDING(0x878-0x5F0-(sizeof(SIndicator)*6));

		// SelectAutoAimTarget: current auto-aim target CBody (owned, deleted on switch)
		CBody *field_878;

		// both cleared by CPlayer::CheckSwitchToGrabbedMode
		i32 field_87C;

		PADDING(0x894-0x87C-4);

		i32 field_894;

		// gTimerRelated stamp taken by CheckKick when it starts a combo
		i32 field_898;

		MATRIX field_89C;

		// running max/min CBody::mPlayerDist across the qualifying baddies
		// found this pass, set up in BuildOffscreenSpideySenseIndicatorList
		u32 field_8BC;
		u32 field_8C0;

		i32 field_8C4;
		i32 field_8C8;
		CVector field_8CC;

		u8 field_8D8;

		PADDING(3);

		i32 field_8DC;

		// web-shot button latch (CheckWebShot): field_8E0 is "the button is
		// down", field_8E1 "this press may aim a directional web", and
		// field_8E4 the bitmask of directions that were already used up
		// (bit 0 right, 1 left, 2 up, 3 down).
		u8 field_8E0;
		u8 field_8E1;

		PADDING(2);

		i32 field_8E4;


		u8 field_8E8;
		u8 field_8E9;
		u8 field_8EA;

		// set to 1 by CPlayer::CPlayer.
		u8 field_8EB;

		u8 gCamAngleLock; //8EC

		// set to 1 by CPlayer::SetupLookaroundCamera (0x4C38A0) whenever it
		// commits to a zip-web/swing-web lock-on animation; offset 0x8ED
		// per its IDA disasm ("mov byte ptr [ebp+8EDh], 1").
		u8 field_8ED;

		PADDING(0x8F0-0x8ED-1);

		// ReadAnalogueInput: accumulates +32 per tick while the move input is
		// non-centre (clamped at 256), reset to 0 when it centres.
		i32 field_8F0;

		// set to 135 by CPlayer::CPlayer.
		i32 field_8F4;

		// which web shot is running: 1 forward, 2 right, 4 left
		// (CheckWebShot, CPlayer::FireWeb).
		u8 field_8F8;

		// set to 7 by CPlayer::CPlayer.
		u8 field_8F9;

		PADDING(2);

		// --- active combo state, all set up by CPlayer::InitiateCombo and
		// --- driven by CPlayer::UpdateAndTrackCombo.

		// id of the move being played, an index into gComboMoves.
		u16 field_8FC;

		// frame windows copied out of the move record (offsets 0x0C, 0x0E,
		// 0x12, 0x14, 0x16 and 0x18 of the record), all in the same units as
		// field_910, i.e. animation clock ticks since the move started.
		// 8FE..900 is the collision window, 902..904 and 906..908 the two
		// follow-on input windows.
		u16 field_8FE;
		u16 field_900;
		u16 field_902;
		u16 field_904;
		u16 field_906;
		u16 field_908;

		// frame and move id of the follow-on the player has queued up.
		u16 field_90A;
		u16 field_90C;

		PADDING(2);

		// field_84 (the animation clock) when the move started, minus the
		// caller's head start. Elapsed time is field_84 - field_910.
		i32 field_910;

		// animation the follow-on will switch to, and how far into the
		// distance byte stream UpdateAndTrackCombo has already walked.
		u16 field_914;
		u16 field_916;

		// gTimerRelated of the last accepted button press.
		i32 field_918;

		// hook offsets subtracted from the four slide hook positions
		// (UpdateAndTrackCombo's case 1..4), three i32 each.
		i32 field_91C;
		i32 field_920;
		i32 field_924;
		i32 field_928;
		i32 field_92C;
		i32 field_930;
		i32 field_934;
		i32 field_938;
		i32 field_93C;
		i32 field_940;
		i32 field_944;
		i32 field_948;

		// the move has parts to track at all.
		u8 field_94C;

		// the move is still running.
		u8 field_94D;

		PADDING(2);

		// per-frame animation frame numbers, indexed by elapsed time / 2 and
		// 0xFF terminated. GetComboFrameInfoPointer's result.
		u8 *field_950;

		// collision parts byte stream, 0xFF terminated.
		// GetComboPartsInfoPointer's result.
		u8 *field_954;

		// the parts entry that matched, i.e. the move record of the
		// follow-on InitiateCombo will start next.
		u16 *field_958;

		SComboPart field_95C[17];

		// up to four bodies already hit by this move, so one swing cannot
		// hit the same baddy twice, and how many of the four are used.
		CBody *field_A6C[4];
		i32 field_A7C;

		// set to 1 by CPlayer::CPlayer.
		u8 field_A80;

		PADDING(0xAA4-0xA80-1);

		// CPlayer::CPlayer asserts this is still null ("Bad") right after
		// building the body transform; nothing in the repo writes it yet.
		i32 field_AA4;

		PADDING(0xAB8-0xAA4-4);

		SHandle field_AB8;

		// CPlayer::DoMGSShadow: lazily-allocated CQuadBit for the MGS shadow
		// (132 bytes via CBit::operator new), created when field_158 is set
		// and deleted (vtable[0](this,1)) when field_158 is cleared.
		CQuadBit *field_AC0;

		// lazily allocated CQuadBit for the ceiling shadow cast by
		// CPlayer::DoShadowCheck
		CQuadBit *field_AC4;

		// Reference direction used when orienting to a contacted surface.
		CVector mOrientationReferenceDirection;

		// Surface-attached movement mode, including walls and ceilings.
		u8 mIsCrawling;

		// CheckInteriorSurfaceTransition: picks the "hard"/alternative set of
		// surface transition animations when set.
		u8 mUsesHardSurfaceTransitions;

		u8 field_AD6;

		u8 field_AD7;

		// ReadAnalogueInput: one-shot "move input just centred" flags.
		u8 field_AD8;
		u8 field_AD9;

		// HandleControlsForSurfaceTransition: "player is pushing away from
		// this surface" flags, one per transition direction.
		u8 field_ADA;
		u8 field_ADB;

		// CheckExteriorSurfaceTransition: same "pushing away" flag as
		// field_ADA, for the wall transition.
		u8 field_ADC;

		PADDING(0xAE4-0xADC-1);

		u8 field_AE4;
		u8 field_AE5;
		u8 field_AE6;

		PADDING(0xB08-0xAE6-1);

		// blocks CheckInteriorSurfaceTransition when set
		u8 mExteriorCornerDetected;

		// gate for CPlayer::CheckFenceSurfaceTransition: the player is
		// standing on/near a fence surface
		u8 mFenceSurfaceDetected;

		PADDING(0xB0C-0xB09-1);

		// The player's own SLineInfo raycast scratch block, 0xB0C..0xBB0.
		// Identified 2026-09-01 from CPlayer::DoSwingingPhysics (0x467D20)
		// and CPlayer::DoCrawlingPhysics (0x467FD0), which pass &this[0xB0C]
		// straight to M3dColij_InitLineInfo / M3dZone_LineToItem and then
		// read the hit back out of it. Every field this file already knew
		// lines up: 0xB0C/0xB18 are the ray's start/end (StartCoords /
		// EndCoords), 0xB4C is the hit distance (negative = no hit), 0xB74
		// is the hit item, 0xB78..0xB80 the hit position, 0xB84 the surface
		// normal and 0xB8C the hit face pointer. sizeof(SLineInfo) is 0xA4,
		// which ends the block exactly at 0xBB0, where the next known field
		// starts.
		SLineInfo mLineInfo;

		// A second SLineInfo scratch block, 0xBB0..0xC54, same reasoning as
		// mLineInfo above. CPlayer::DoPhysics (0x466CE0) casts a "am I about
		// to land on something" ray straight down through &this[0xBB0]:
		// 0xBB0/0xBBC are its start/end, and the already-known 0xC18 (hit
		// item), 0xC1C (hit position), 0xC28 (surface normal) and 0xC30 (hit
		// face) sit exactly at SLineInfo's pItem/Position/Normal/pFace, with
		// the block ending at 0xC54.
		SLineInfo mLineInfo2;

		// CPlayer::DoCrawlingPhysics: distance push-back for the "player ran
		// into an interior surface" case, set to mLineInfo.Distance-8 (0 when
		// the hit is 16 units or closer).
		i32 mCornerTransitionDistance;

		// CPlayer::DoCrawlingPhysics: the same push-back for the sideways
		// crawl ray, set to 88-mLineInfo.Distance (0 at 80 units or more).
		i32 mForwardCollisionClearance;

		// CPlayer::AI: nonzero gate for the field_C64 accumulator update
		// (0xC5C must be nonzero to run the clamp logic).
		u8 field_C5C;

		PADDING(0xC60-0xC5C-1);

		// set to 300 by CPlayer::CPlayer; trig.cpp reaches it by raw offset
		// as the fight-music timer.
		i32 field_C60;

		// CPlayer::AI: accumulator clamped to [0, 0x1000], adjusted by
		// field_80*64 per tick when field_C68 (decrement) or field_C69 (increment).
		i32 field_C64;

		// CPlayer::AI: nonzero selects the decrement path for field_C64.
		u8 field_C68;

		// CPlayer::AI: nonzero selects the increment path for field_C64.
		u8 field_C69;

		PADDING(0xC6C-0xC69-1);

		// Cached transform forward axis, original offset 0xC6C.
		CVector mForwardDirection;

		// the player's local "right" axis: CPlayer::UpdateFrameVectors fills
		// it from the first column of mTransform (m[0][0]/m[1][0]/m[2][0]),
		// and CPlayer::DoCrawlingPhysics (0x467FD0) passes &this[0xC78]
		// straight into operator*(const CVector&, const int&). Was three
		// separate i32 fields.
		CVector mRightDirection;

		// Negative transform Y axis, original offset 0xC84.
		CVector mUpDirection;
		i32 field_C90;

		// player body orientation as a quaternion (from MToQ(mTransform)),
		// used by EnterLookaroundMode to seed the lookaround camera path.
		CQuat field_C94;

		// inverse of the active camera's orientation quaternion, the other
		// endpoint of the EnterLookaroundMode Quat_Slerp path.
		CQuat field_CA4;

		i32 field_CB4;

		// camera position snapshot taken by EnterLookaroundMode.
		CVector field_CB8;

		CQuat field_CC4;

		// per-frame smoothed lookaround camera orientation quaternion,
		// written by CPlayer::SetupLookaroundCamera (MToQ of its working
		// matrix) while not mid-exit-transition. offset 0xCD4, IDA disasm
		// of 0x4C38A0.
		CQuat field_CD4;

		i32 field_CE4;

		// SetupLookaroundCamera's grid-search anchor result, snapshotted
		// here whenever field_CE4 (the exit-transition countdown) is 0;
		// used together with field_CF4 as the two lerp endpoints while
		// field_CE4 is counting down. offset 0xCE8, IDA disasm of 0x4C38A0.
		CVector field_CE8;

		// the other endpoint of the field_CE8 exit-transition lerp; never
		// written by SetupLookaroundCamera itself, so presumably set by
		// whichever (not yet decompiled) function requests the exit.
		// offset 0xCF4, IDA disasm of 0x4C38A0.
		CVector field_CF4;

		// hook-8 world position plus mUpDirection*0x80, used by
		// EnterLookaroundMode as the lookaround camera anchor.
		CVector field_D00;

		// mUpDirection*0x80, stashed by EnterLookaroundMode.
		CVector field_D0C;

		// -field_A8 (negated surface normal, long-vector/GTE-width, pad
		// word included), cached by CPlayer::OrientToNormal every call.
		VECTOR field_D18;

		PADDING(0xD2C-0xD18-sizeof(VECTOR));

		// set to 0x202020 by CPlayer::CPlayer.
		i32 field_D2C;

		CVector field_D30;

		CVector field_D3C;

		CSVector field_D48;

		CSVector field_D4E;

		CVector field_D54;

		// 0 = near web-attach point (field_D64) is valid, 1 = far one
		// (field_D70) is valid. Set by CheckSwingWebAvailability.
		u8 field_D60;

		PADDING(0xD64-0xD60-1);

		// candidate swing-web attach points, computed by
		// CheckSwingWebAvailability. field_D60 selects which is active.
		CVector field_D64;
		CVector field_D70;

		PADDING(0xD80-0xD70-sizeof(CVector));

		CSVector field_D80;
		CSVector field_D86;
		CSVector field_D8C;

		PADDING(0xDA0-0xD8C-sizeof(CSVector));

		CVector field_DA0;

		// SetupLookaroundCamera's swing-web-lock fallback target position
		// (either lineInfo.Position when mIsCrawling was set, or ZeroVector
		// otherwise). offset 0xDAC, IDA disasm of 0x4C38A0.
		CVector field_DAC;

		i32 field_DB8;

		// body the player is standing on; CheckJump adds its mVel.vy into
		// the take-off velocity
		CBody *mSupportBody;

		// Shared web/interaction target in world coordinates, offset 0xDC0.
		CVector mInteractionTargetPosition;

		// @FIXME - type
		CBody *field_DCC;

		// SelectAutoAimTarget: cleared at the start of each auto-aim pass
		// the auto-aim target position of the switch CPlayer::FireWeb picked,
		// straight out of SelectTargetSwitch's return value. Cleared at the
		// start of every SelectAutoAimTarget pass.
		CVector *field_DD0;

		PADDING(0xDD8-0xDD0-4);

		// grab target handle, recovered via Mem_RecoverPointer in GrabUpdate
		SHandle field_DD8;

		// gTimerRelated when the zip web towards field_DD8 was started.
		i32 field_DE0;

		char field_DE4;

		PADDING(0xDE8-0xDE4-1);

		// reticle color: low 3 bytes are packed r0/g0/b0, OR'd with 0x2C
		// (poly tag/code byte) at use in DrawReticle.
		i32 field_DE8;

		// reticle sprite: OffX/OffY/Width/Height + Texture*, read by
		// DrawReticle via Panel_DrawTexturedPoly(SAnimFrame*, i32).
		SAnimFrame *field_DEC;

		i32 mTargetHeading;
		i32 mHeadingTurnStep;

		i32 mHeadingTurnTicksRemaining;
		i32 field_DFC;

		i32 field_E00;

		// SelectAutoAimTarget: three words cleared at the start of each pass
		u16 field_E04;
		u16 field_E06;
		u16 field_E08;

		PADDING(0xE0C-0xE08-2);

		// CPlayer::AI: pointer to a struct of 16 i16-pair fields at 0x10-byte
		// intervals (0x00..0xB0, 0x100..0x130) plus byte flags at 0x21/0x31/0xE1/0x101.
		// AI zeroes the 16 pairs and reads/writes the byte flags.
		i32* field_E0C;
		char field_E10;

		i16 field_E12;

		// set to 1 by CPlayer::CPlayer.
		u8 field_E14;

		PADDING(3);

		i32 field_E18;
		// One-hot PlayerAction tag; mask tests also select groups of actions.
		i32 mActionState;

		i32 field_E20;

		u8 mBehaviorSuspended;
		PADDING(3);
		i32 mBehaviorCountdownTicks;
		PADDING(1);

		char field_E2D;
		char field_E2E;

		// ReadAnalogueInput: previous-tick copy of field_E2D/field_E2E.
		char field_E2F;
		char field_E30;

		PADDING((0xE32-0xE30)-0x1);

		// used as an index (masked to 0xFFF, then scaled) into
		// word_610C4A/word_610C48 in CPlayer::PutCameraBehind.
		i16 field_E32;

		// ReadAnalogueInput: previous aim angle; field_E32 is the new angle.
		i16 field_E34;

		PADDING((0xE38-0xE34)-0x2);

		i32 mFallReferenceY;

		i32 mFallReferenceTick;

		// fall-damage window, both in world units (CPlayer::CheckLanded):
		// field_E40 is the drop height at which a landing starts to hurt,
		// field_E44 the drop height that costs a full mMaxHealth. The hit
		// strength is scaled linearly between the two.
		i32 field_E40;
		i32 field_E44;

		CManipOb* mHeldObject;

		// CheckKick targets: environmental object, auto-aim switch, baddy
		SHandle field_E4C;
		SHandle field_E54;
		SHandle field_E5C;

		// @FIXME guess the type, used as a scalar-deleting-destructor
		// pointer (vtable[0](1)) in CPlayer::SwitchToDeathMode
		i32* field_E64;

		PADDING(0xE6C-0xE64-4);

		// @FIXME guess the type, same scalar-deleting-destructor idiom
		// as field_E64 (vtable[0](1)), used by
		// SynthesizeAnalogueInput's opcode 1 (teleport) handler.
		i32* field_E6C;

		SHandle hLockTarget;

		PADDING(0xE80-0xE70-sizeof(SHandle));

		// jump take-off vertical velocity, set by CheckJump
		i32 field_E80;

		i32 field_E84;
		i32 field_E88;

		u8 field_E8C;
		u8 field_E8D;

		PADDING(0xE90-0xE8D-1);

		// CPlayer::DoPhysics: angle (rcossin_tbl index, masked to 12 bits)
		// of the random bounce direction it picks when the mActionState == 1
		// state cannot find a wall to bounce off.
		u16 mWallProbeHeading;

		PADDING(0xE94-0xE90-2);

		CVector mWallProbeDirection;

		// ReadAnalogueInput: aim correction ratio (field_EA0/field_EA2).
		u16 field_EA0;
		u16 field_EA2;

		u8 mGroundContactGraceUpdates;

		PADDING(1);

		// zeroed at the top of every SynthesizeAnalogueInput call; exact
		// purpose unclear.
		i16 field_EA6;

		// World-unit distance to keep the body away from a contacted surface.
		u16 mSurfaceClearance;

		// set to 70 by CPlayer::CPlayer next to mSurfaceClearance's 96.
		u16 mCollisionProbeReach;

		CVector field_EAC;

		PADDING(0xEBC-0xEAC-12);

		// cleared by CheckJump when the player was touching something
		i32 field_EBC;

		// set to 1 in BuildOffscreenSpideySenseIndicatorList when at least
		// one qualifying baddy was found this pass
		u8 field_EC0;

		PADDING(0xECC-0xEC0-1);

		// water-effect state and the looping SFX handle that goes with it,
		// both torn down by PriorToVenomDistanceAttack.
		i32 field_ECC;
		u32 field_ED0;

		// another body part on SpideyAdditionalBodyPartsList, unlinked and
		// deleted by ~CPlayer the same way as field_5B8.
		SHandle field_ED4;

		PADDING(0xEE0-0xED4-sizeof(SHandle));

		// position of the thing that grabbed the player, copied in by
		// CPlayer::CheckSwitchToGrabbedMode
		CVector field_EE0;

		// gTimerRelated when CPlayer::Hit last played a hurt grunt; the
		// grunt only replays once more than 30 ticks have passed.
		u32 field_EEC;

		i32 mMaxHealth;

		// cleared by PriorToVenomDistanceAttack. Read by CPlayer::DoPhysics
		// (0x466CE0): while set, the frame's movement is bent so the player
		// stays on a circle of radius mArenaConstraintRadius around gBossRelated.
		u8 mArenaConstraintActive;

		PADDING(0xEF8-0xEF4-1);

		// default perpendicularisation radius (fallback value used by
		// GetPerpendicularisationRadius outside the special zone-1797 case).
		i32 mArenaConstraintRadius;


		EXPORT void SetCamAngleLock(u16);
		EXPORT void ExitLookaroundMode(void);
		EXPORT void SetIgnoreInputTimer(i32);
		EXPORT void PutCameraBehind(i32);
		EXPORT void SetSpideyLookaroundCamValue(u16, u16, i16);
		EXPORT void SetTargetTorsoAngleToThisPoint(CVector *a2);

		EXPORT i16 GetEffectiveHeading(void);
		EXPORT char DecreaseWebbing(i32);
		EXPORT void RenderLookaroundReticle(void);
		EXPORT void SetTargetTorsoAngle(i16, bool);
		EXPORT void CreateJumpingSmashKickTrail(void);
		EXPORT void PlaySingleAnim(i32, i32, i32);
		EXPORT void CutSceneSkipCleanup(void);
		EXPORT void OrientToNormal(bool, CVector*);
		EXPORT void PriorToVenomDistanceAttack(CVector);
		EXPORT void SwitchToStandMode(void);
		EXPORT void TidyUpZipWebLandingPosition(i32);
		EXPORT void CreateFists(u8);
		EXPORT u8 CanITalkRightNow(void);
		EXPORT u8 SetFireWebbing(void);
		EXPORT void GetHookPosition(CVector*, u8);
		EXPORT void DestroyJumpingSmashKickTrail(void);
		EXPORT void DestroyHandTrails(void);
		EXPORT void DeleteStuff(void);
		EXPORT void StopAlertMusic(void);
		EXPORT void KillAllCommandBlocks(void);
		EXPORT i32* KillCommandBlock(i32*);
		EXPORT void Die(void);
		EXPORT void SetStartOrientation(CSVector*);
		EXPORT u8 IncreaseWebbing(i32);

		EXPORT void AI(void);
		EXPORT void AdjustBrightness(u16);
		EXPORT void BuildOffscreenSpideySenseIndicatorList(void);
		EXPORT CPlayer(void);
		EXPORT i32 CalculateIntermediateTrailSteps(CVector *,CVector *,CVector *);
		EXPORT void CalculateSwingWebParameters(CVector *);
		EXPORT i32 *CalculateTugWebPathPoints(void);
		EXPORT u8 CheckCeilingJumpingSmashPunch(void);
		EXPORT i32 CheckExteriorSurfaceTransition(void);
		EXPORT i32 CheckFenceSurfaceTransition(void);
		EXPORT i32 CheckForwards(bool);
		EXPORT i32 CheckGroundGone(void);
		EXPORT i32 CheckInteriorSurfaceTransition(void);
		EXPORT i32 CheckJump(void);
		EXPORT u8 CheckJumpingR1ZipWeb(void);
		EXPORT u8 CheckJumpingR2ZipWeb(void);
		EXPORT u8 CheckJumpingSmashKick(void);
		EXPORT u8 CheckJumpingSwingWeb(void);
		EXPORT i32 CheckKick(void);
		EXPORT i32 CheckLanded(void);
		EXPORT i32 CheckRunIntoWall(void);
		EXPORT i32 CheckStickToCeiling(void);
		EXPORT i32 CheckStickToWall(void);
		EXPORT u8 CheckSwingWebAvailability(SLineInfo *);
		EXPORT u8 CheckSwitchToGrabbedMode(CVector const *,CVector *);
		EXPORT i32 CheckWebShot(void);
		EXPORT u8 CheckZipWebAvailability(SLineInfo *,i32);
		EXPORT void CollideWithObject(CBody *);
		EXPORT void CreateCombatImpactEffect(CVector *,i32);
		EXPORT void CreateWebDrips(bool,bool);
		EXPORT void DoCrawlingPhysics(void);
		EXPORT void DoMGSShadow(void);
		// 0x466CE0, 0x467D20 and 0x467FD0. All three live in physics.cpp,
		// which is the translation unit the Mac build puts them in (next to
		// Physics_SetGravity), not in spidey.cpp.
		EXPORT void DoPhysics(void);
		EXPORT void DoShadowCheck(void);
		EXPORT void DoSwingingPhysics(void);
		EXPORT void DrawOffscreenSpideySenseIndicatorList(void);
		EXPORT void DrawReticle(u16,u16,u32);
		EXPORT void EnterLookaroundMode(void);
		EXPORT i32 FireWeb(bool,i32,CVector *,bool,CSVector *);
		EXPORT void GetComboFrameInfoPointer(u16);
		EXPORT void GetComboPartsInfoPointer(u16);
		EXPORT i32 GetDamageInflictedFromDifficulty(i32);
		EXPORT void GetEnterExitFrameInfoPointer(u16);
		EXPORT i32 GetFreeIndicatorListEntry(void);
		EXPORT i32* GetNewCommandBlock(u32);
		EXPORT i32 GetPerpendicularisationRadius(void);
		EXPORT u8 GrabUpdate(CVector *,i16 *);
		EXPORT void HandleControlsForSurfaceTransition(bool);
		EXPORT i32 Hit(SHitInfo *) OVERRIDE;
		EXPORT u8 IfPlayerCeilingCheck(i32,i32);
		EXPORT i32 IncHealth(i32);
		EXPORT void InitialiseOffscreenSpideySenseIndicatorList(void);
		EXPORT void InitialiseSFXArray(void);
		EXPORT void InitiateCombo(u16,i32);
		EXPORT u8 IsInIndicatorList(SHandle &);
		EXPORT u8 KnockSpideyFromCrawlPosition(void);
		EXPORT void LockTargetTorsoAngle(void);
		EXPORT void NotifyKill(u16);
		EXPORT void ParseFightData(void);
		EXPORT i32 ProcessSFXArray(void);
		EXPORT void ReadAnalogueInput(void);
		EXPORT u8 SelectAutoAimTarget(void);
		EXPORT CBody *SelectTargetBaddy(i32,i32,i32,i32);
		EXPORT CVector *SelectTargetSwitch(i32,i32,SHandle *,i32,i32);
		EXPORT u8 SetArmor(bool);
		EXPORT void SetCeilingCamera(i32);
		EXPORT void SetFallingCamera(i32);
		EXPORT void SetFirstContactDetails(void);
		EXPORT void SetFloorCamera(i32);
		EXPORT void SetFocusLockTarget(CBody const *);
		EXPORT void SetSpideyCamValue(u16,u16,i16,u16,u16);
		EXPORT void SetSwingCamera(i32);
		EXPORT void SetWallCamera(i32);
		EXPORT void SetupLookaroundCamera(void);
		EXPORT u8 ShouldPlayerDropFlail(void);
		EXPORT void SortAnimationFollowOnData(void);
		EXPORT void SortFistsData(void);
		EXPORT void SwitchToDeathMode(bool);
		EXPORT i16* SwitchToSynthesizedInput(i16 *);
		EXPORT void SynthesizeAnalogueInput(void);
		EXPORT i32 UpdateAndTrackCombo(void);
		EXPORT void UpdateOffscreenSpideySenseIndicatorList(void);
		EXPORT void UpdateTrails(void);
		EXPORT ~CPlayer(void);
		EXPORT void nullsub_one(i32);
		EXPORT void ResetSFXArrayEntry(u32);
};

// defined in spidey.cpp below CPlayer::SetArmor; declared here because
// CPlayer::Hit, defined earlier in the same file, drops the armour too.
EXPORT extern u8 gSpideyArmorSet;
// 0x006A9040, "gSpideyArmorSet" in idb_globals.txt, confirmed by the stores in
// CPlayer::SetArmor (0x004BAEC0).
//#define G_SPIDEY_ARMOR_SET (gSpideyArmorSet)
#define G_SPIDEY_ARMOR_SET (*reinterpret_cast<u8*>(0x006A9040))

// The player. This is the SAME storage as G_MECHLIST in ob.h (0x006A9038): the
// repo grew two variables for one global, spidey.cpp's CPlayer* MechList and
// ob.cpp's CBody* RealMechList, and 826 references in the original all go to
// 0x006A9038. CPlayer::CPlayer pushes that address into CBody::AttachTo at
// 0x004BA51E and ~CPlayer into DeleteFrom at 0x004BACDD; nothing in our source
// ever assigns it, the exe maintains the list.
//
// Two macros for one address is normally wrong, but these are two type views of
// one object, not two slots: ob.h keeps the CBody* view for the object-list code
// and this is the CPlayer* view the game logic wants. Merging them would change
// which non-virtual overload every one of 405 call sites picks, so they stay
// apart on purpose.
EXPORT extern CPlayer* MechList;
//#define G_MECHLIST_PLAYER (MechList)
#define G_MECHLIST_PLAYER (*reinterpret_cast<CPlayer**>(0x006A9038))
EXPORT extern CItem* SpideyAdditionalBodyPartsList;
// 0x006A903C, "SpideyAdditionalBodyPartsList" in idb_globals.txt. The exe links
// the fists and the buzz bit onto it and M3d_Render walks it every frame.
//#define G_SPIDEY_ADDITIONAL_BODY_PARTS_LIST (SpideyAdditionalBodyPartsList)
#define G_SPIDEY_ADDITIONAL_BODY_PARTS_LIST (*reinterpret_cast<CItem**>(0x006A903C))

EXPORT extern CItem* MiscellaneousRenderingList;
// 0x0060DAB0, "MiscellaneousRenderingList" in idb_globals.txt. Same deal, the
// auto aim marker lives on it.
//#define G_MISCELLANEOUS_RENDERING_LIST (MiscellaneousRenderingList)
#define G_MISCELLANEOUS_RENDERING_LIST (*reinterpret_cast<CItem**>(0x0060DAB0))

// Defined in spidey.cpp (0x0060F750 / 0x0060F754 per idb_globals.txt, both
// written by Spidey_LoadAlternativeHealthIcon and CPlayer::SetArmor).
EXPORT extern SAnimFrame *gSpideyAnim;
//#define G_SPIDEY_ANIM (gSpideyAnim)
#define G_SPIDEY_ANIM (*reinterpret_cast<SAnimFrame**>(0x0060F750))
EXPORT extern SAnimFrame *gSpideyAnimTwo;
//#define G_SPIDEY_ANIM_TWO (gSpideyAnimTwo)
#define G_SPIDEY_ANIM_TWO (*reinterpret_cast<SAnimFrame**>(0x0060F754))


EXPORT void Bruce_Sync(void);

EXPORT void Spidey_SetUserFunction(const char *, u32);
EXPORT void Spidey_FreeHeadModel(void);
EXPORT void Spidey_CopyHeadModel(i32);

EXPORT void Spidey_BagHead(i32,i32);
EXPORT void Spidey_DoArmorVRAMProcessing(bool);
EXPORT void Spidey_LoadAlternativeHealthIcon(i32);
EXPORT void Spidey_LoadAlternativeTextureSet(u32 const *,i32);
EXPORT void Spidey_StoreTextureEntry(Texture const *,i16,i16);
EXPORT void Spidey_SwapSuitTextures(i32,i32);
EXPORT void spideyLog(char *,...);

void patch_spidey(void);

void validate_CPlayer(void);
void validate_SIndicator(void);

#endif
