#ifdef SPIDEY_STANDALONE
#include <new>
#endif
#include "spid_ai0.h"

#include "bit.h"
#include "baddy.h"
#include "camera.h"
#include "db.h"
#include "effects.h"
#include "flash.h"
#include "m3dcolij.h"
#include "m3dutils.h"
#include "m3dzone.h"
#include "powerup.h"
#include "reloc.h"
#include "spool.h"
#include "switch.h"
#include "ps2m3d.h"
#include "mem.h"
#include "ob.h"
#include "manipob.h"
#include "PCInput.h"
#include "ps2lowsfx.h"
#include "ps2pad.h"
#include "ps2redbook.h"
#include "tweak.h"
#include "ps2funcs.h"
#include "trig.h"
#include "utils.h"
#include "vector.h"
#include "web.h"

// ---------------------------------------------------------------------------
// CPlayer fields this file needs that spidey.h does not name yet. They all sit
// inside existing PADDING runs; spidey.h belongs to another worktree, so they
// are reached by byte offset here and reported for it to adopt. Evidence for
// each one is on the line where it is used.
// ---------------------------------------------------------------------------
#define PLR_U8(p, off)  (*reinterpret_cast<u8*>(reinterpret_cast<char*>(p) + (off)))
#define PLR_I8(p, off)  (*reinterpret_cast<i8*>(reinterpret_cast<char*>(p) + (off)))
#define PLR_I16(p, off) (*reinterpret_cast<i16*>(reinterpret_cast<char*>(p) + (off)))
#define PLR_U16(p, off) (*reinterpret_cast<u16*>(reinterpret_cast<char*>(p) + (off)))
#define PLR_I32(p, off) (*reinterpret_cast<i32*>(reinterpret_cast<char*>(p) + (off)))

// gWaterEffect is the repo global from post.cpp (spidey.cpp reaches it the
// same way). Note trig.cpp instead points a file-local pointer at the game
// address 0x0060FA9C for the same thing; the two readings disagree and should
// be unified once post.h grows a declaration.
extern i32 gWaterEffect;

// init.cpp's global, no header declares it yet.
extern i32 gInitRelatedTwo;

// baddy.cpp's global (real IDB name, 0x0060D9E0), no header declares it yet.
extern CSVector gTrajectoryVector;

// Death/respawn countdown, tentative name. The only cross reference in the
// whole binary is SpideyAI0's state 128 case (a scan of every
// tools/functions/*.bin for the little endian address finds this function and
// nothing else): it accumulates field_80 once the death animation has
// finished, and at 0x78 or more resets the flash and sets gLevelStatus to 2.
static i32 * const gSpideyDeathTimer = reinterpret_cast<i32*>(0x006A7F98);

// gSaveGame + 0x7B, the "vibration on" option byte. Same pointer spidey.cpp
// uses under this name in two places.
static u8 * const gSaveGameVibration = reinterpret_cast<u8*>(0x006828D3);

// Cheat flags. Same addresses (and names) pshell.cpp defines; they scale the
// extra body parts SpideyAI0 spawns.
#define G_PULSATING_HEAD_FLAG (*reinterpret_cast<i32*>(0x0060CFF0))
#define G_TOON_SPIDEY_FLAG (*reinterpret_cast<i32*>(0x02E09BF0))
#define G_STICKMAN_FLAG (*reinterpret_cast<i32*>(0x02E09BF4))

// Lookaround camera accumulators, same file-local pointers (and names)
// spidey.cpp uses for CPlayer::EnterLookaroundMode / SetupLookaroundCamera.
static i32 * const gLookaroundYawOffset = reinterpret_cast<i32*>(0x006A7FFC);
static i32 * const gLookaroundActiveCamAngle = reinterpret_cast<i32*>(0x006A818C);
static i32 * const gLookaroundPitchSmoothed = reinterpret_cast<i32*>(0x006A82B4);
static i32 * const gLookaroundYawSmoothed = reinterpret_cast<i32*>(0x006A8D54);

// gSaveGame practice-difficulty byte, same pointer/name pshell.cpp and
// spidey.cpp already use.
static u8 * const gPracticeDifficultyFlag = reinterpret_cast<u8*>(0x0060CFC7);

// {u16 anim, u16 followOnAnim} x 200, sorted into animation order by
// CPlayer::SortAnimationFollowOnData (spidey.cpp, whose comment already points
// at this read site, 0x4B204A).
// gSaveGame + 0x?? style byte that spidey.cpp already calls
// gRedbookXaPlayingMaybe at this address; kept the same name.
static char * const gRedbookXaPlayingMaybe = reinterpret_cast<char*>(0x00682770);

// real IDB names
static i32 * const pgRedbookXaRelatedOne = reinterpret_cast<i32*>(0x00681D1C);
static i32 * const pgRedbookXaRelatedTwo = reinterpret_cast<i32*>(0x006612C0);
#define gRedbookXaRelatedOne (*pgRedbookXaRelatedOne)
#define gRedbookXaRelatedTwo (*pgRedbookXaRelatedTwo)

// The pose CPlayer::ApplyPose runs while the 4 flag is set on mFlags. Not
// named in the IDB; the address is only referenced from here.
static i16 * const gSpideyFixedPose = reinterpret_cast<i16*>(0x005564E4);

// The two Reloc_CallUserFunction effect names the fists spawn (both are
// plain strings in .rdata, read straight from the exe).
static char * const gFistEffectNameLeft = reinterpret_cast<char*>(0x005559C4);
static char * const gFistEffectNameRight = reinterpret_cast<char*>(0x005559CC);

// The camera/boss anchor object SpideyAI0 aims the lookaround camera at;
// real IDB name.
static i32 * const pgBossRelated = reinterpret_cast<i32*>(0x0056E998);
#define gBossRelated (*pgBossRelated)

static u16 * const gAnimFollowOnData = reinterpret_cast<u16*>(0x00555C6C);


// @Bogus
static void gM3dUtils_GetPartAngles(CSuper *pSuper, i32 part, CSVector *pAngles, i32 a4)
{
#ifdef SPIDEY_STANDALONE
	M3dUtils_GetPartAngles(pSuper, part, pAngles, reinterpret_cast<CSVector*>(a4));
#else
	typedef void (*func_ptr)(CSuper*, i32, CSVector*, i32);
	func_ptr func = (func_ptr)0x00454040;
	func(pSuper, part, pAngles, a4);
#endif
}

// CSwinger::SetRenderEnd (0x004F74B0) and CWeb::SetFirePos (0x004F6170) are
// real names from tools/names.json, but web.h declares neither, so both are
// reached through the same adapter trick. They are __thiscall with one
// CVector reference argument.
struct SSwingerRenderEndAdapter
{
	void SetRenderEnd(CVector &pos);
};

struct SWebFirePosAdapter
{
	void SetFirePos(CVector &pos);
};

// @Bogus
static void gCSwinger_SetRenderEnd(void *pSwinger, CVector &pos)
{
#ifdef SPIDEY_STANDALONE
	static_cast<CSwinger*>(pSwinger)->SetRenderEnd(pos);
#else
	typedef void (SSwingerRenderEndAdapter::*memfn)(CVector&);
	union { memfn m; void *p; } u;
	u.p = (void*)0x004F74B0;
	(reinterpret_cast<SSwingerRenderEndAdapter*>(pSwinger)->*u.m)(pos);
#endif
}
// @Bogus
static void gCWeb_SetFirePos(CWeb *pWeb, CVector &pos)
{
#ifdef SPIDEY_STANDALONE
	pWeb->SetFirePos(pos);
#else
	typedef void (SWebFirePosAdapter::*memfn)(CVector&);
	union { memfn m; void *p; } u;
	u.p = (void*)0x004F6170;
	(reinterpret_cast<SWebFirePosAdapter*>(pWeb)->*u.m)(pos);
#endif
}

// CWeb::SwitchToSnap (0x004F69F0, real name in the maintainer's IDB, and the
// Mac symbol .SwitchToSnap__4CWebFR7CVectorP7CVector gives the arguments) is
// not declared in web.h either, so it uses the same adapter.
struct SWebSwitchToSnapAdapter
{
	void SwitchToSnap(CVector &dir, CVector *pPath);
};

// @Bogus
static void gCWeb_SwitchToSnap(CWeb *pWeb, CVector &dir, CVector *pPath)
{
#ifdef SPIDEY_STANDALONE
	pWeb->SwitchToSnap(dir, pPath);
#else
	typedef void (SWebSwitchToSnapAdapter::*memfn)(CVector&, CVector*);
	union { memfn m; void *p; } u;
	u.p = (void*)0x004F69F0;
	(reinterpret_cast<SWebSwitchToSnapAdapter*>(pWeb)->*u.m)(dir, pPath);
#endif
}

// @Bogus
static i32 gCPlayer_UpdateAndTrackCombo(CPlayer *pPlayer)
{
#ifdef SPIDEY_STANDALONE
	return pPlayer->UpdateAndTrackCombo();
#else
	typedef i32 (CPlayer::*memfn)(void);
	union { memfn m; void *p; } u;
	u.p = (void*)0x004C7120;
	return (pPlayer->*u.m)();
#endif
}

// CSwinger's constructor (0x004F6F00, real name from tools/names.json) is not
// declared in web.h, so the allocation is spelled out and the constructor
// reached through an adapter, the same way the two web helpers above are.
struct SSwingerCtorAdapter
{
	void Construct(CVector *pAnchor, i32 length, CSVector *pAngles, CVector *pEnd);
};

// @Bogus
static void gCSwinger_ctor(void *pSwinger, CVector *pAnchor, i32 length,
	CSVector *pAngles, CVector *pEnd)
{
#ifdef SPIDEY_STANDALONE
	::new (pSwinger) CSwinger(pAnchor, length, reinterpret_cast<MATRIX*>(pAngles), pEnd);
#else
	typedef void (SSwingerCtorAdapter::*memfn)(CVector*, i32, CSVector*, CVector*);
	union { memfn m; void *p; } u;
	u.p = (void*)0x004F6F00;
	(reinterpret_cast<SSwingerCtorAdapter*>(pSwinger)->*u.m)(pAnchor, length, pAngles, pEnd);
#endif
}
// Scales the two extra body parts (spidey sense buzz, fists) the way the
// cheats ask for. Reproduces the original's three separate inline copies:
// they only differ in what the stickman cheat does, a halve for the fists
// (0x4B1862, 0x4B18CF) and a *3/4 for the buzz (0x4B16DE).
// @Bogus
static void SpideyAI0_ApplyCheatScale(CBody *pBody, i32 stickmanHalves)
{
	if (G_TOON_SPIDEY_FLAG != 0)
	{
		pBody->mScale.vx = (i16)(pBody->mScale.vx * 3 >> 1);
		pBody->mScale.vy = (i16)(pBody->mScale.vy * 3 >> 1);
		pBody->mScale.vz = (i16)(pBody->mScale.vz * 3 >> 1);
	}
	else if (G_STICKMAN_FLAG != 0)
	{
		if (stickmanHalves != 0)
		{
			pBody->mScale.vx = (i16)(pBody->mScale.vx >> 1);
			pBody->mScale.vy = (i16)(pBody->mScale.vy >> 1);
			pBody->mScale.vz = (i16)(pBody->mScale.vz >> 1);
		}
		else
		{
			pBody->mScale.vx = (i16)(pBody->mScale.vx * 3 >> 2);
			pBody->mScale.vy = (i16)(pBody->mScale.vy * 3 >> 2);
			pBody->mScale.vz = (i16)(pBody->mScale.vz * 3 >> 2);
		}
	}
}

// @Ok
// Original at 0x4B13F0, 0x72DA bytes / 7655 instructions / 1482 basic blocks,
// the largest function in the game. Identity confirmed: the maintainer's IDB
// (idbs/spideypc_names.txt line 1652) and the Mac build
// (.SpideyAI0__FP7CPlayer, 0x109820) agree with tools/names.json, and on the
// Mac SpideyAI0 is the ONLY function in spid_ai0.cpp (the next symbol is
// __sinit_spid_ai0_cpp).
//
// FULLY PORTED. What is below:
//   * the per-frame prologue, 0x4B13F0..0x4B211F
//   * the state dispatch itself, 0x4B211F..0x4B215D
//   * all 31 states (see the table)
//   * the shared tail def_4B215D, 0x4B6E8C..0x4B86B1, which every state's
//     `break` falls into and which runs the camera, the torso aim, the move
//     input, the platform ride, the baddy/power-up collisions, the pose build
//     and the extra body parts
//
// STRUCTURE. A one-hot state machine on CPlayer::field_E1C, which holds a
// single set bit, not a small ordinal. The dispatch at 0x4B211F is a chain of
// UNSIGNED compares (ja/je), which matters for 0x80000000:
//        > 0x10000  -> 0x4B50AE   == 0x10000 -> 0x4B4E9B
//        > 0x100    -> 0x4B307C   == 0x100   -> 0x4B2F80
//   otherwise switch(field_E1C - 1) over jpt_4B86CC with the byte index table
//   at 0x4B86EC (read out of the exe: slots 0..7 are 0x4B2164, 0x4B274D,
//   0x4B28D9, 0x4B2BF3, 0x4B2D43, 0x4B269B, 0x4B2892, 0x4B6E8C, and the index
//   table picks slot 7 (the default) for everything except the powers of two).
//   0x4B307C, 0x4B400D, 0x4B50AE, 0x4B5DBA, 0x4B65B2 and 0x4B6D68 are five
//   more compare chains, not tables.
//
//   state       entry      status
//   ---------------------------------------------------------------
//   1           0x4B2164   done   idle: lean anims, idle Redbook track,
//                                 the idle web shot at the ceiling
//   2           0x4B274D   done   airborne after a jump
//   4           0x4B28D9   done   falling
//   8           0x4B2BF3   done   on a wall
//   16          0x4B2D43   done   on the ground
//   64          0x4B269B   done   wall-jump wind up
//   128         0x4B2892   done   dead, respawn countdown
//   0x100       0x4B2F80   done   starting a swing
//   0x200       0x4B3C33   done   swing web released, spawns the CSwinger
//   0x400       0x4B329A   done   swinging on a web
//   0x800       0x4B30AE   done   punching / combo tracking
//   0x1000      0x4B3DF9   done   landing out of a surface transition
//   0x2000      0x4B4AAA   done   a surface transition finished
//   0x4000      0x4B44C9   done   the web-attack wind up
//   0x8000      0x4B402E   done   firing a web
//   0x10000     0x4B4E9B   done   web-swing wind up (3 anims that fire a web)
//   0x20000     0x4B58B4   done   the tug-web attacks
//   0x40000     0x4B51B2   done   the tug-web pull-in
//   0x80000     0x4B50F1   done   end of a scripted "stand up here" move
//   0x100000    0x4B5CFE   done   reaching down for a pickup
//   0x200000    0x4B601F   done   throwing the held object
//   0x400000    0x4B5F4D   done   rolling
//   0x800000    0x4B5DDB   done   knocked down / getting back up
//   0x1000000   0x4B631E   done   the jumping smash kick
//   0x2000000   0x4B6901   done   riding a zip-web dive onto a target
//   0x4000000   0x4B6820   done   the punch that ends a zip-web dive
//   0x8000000   0x4B65E4   done   the kick that ends a zip-web dive
//   0x10000000  0x4B6B1E   done   grabbed, wiggle free
//   0x20000000  0x4B6DD8   done   held in a web dome
//   0x40000000  0x4B6DCA   done   one store
//   0x80000000  0x4B6D81   done   pulling a switch
//
// Small blocks several states jump into, all already inlined where used:
//   0x4B4E56  mPos += field_C6C * 8;  field_C54 -= 8;  break
//   0x4B4E7F  mPos += field_C6C * field_C54;  field_C54 = 0;  break
//   0x4B509C  if (animJustFinished != 0xFFFF) SwitchToStandMode();  break
//   0x4B6010  field_E1C = 0x10;  break
//   0x4B6597  SwitchToStandMode();  break
//   0x4B682F  SwitchToStandMode();  break
//   0x4B4134  if (field_E6C) { field_E6C->SwitchToBlob(); field_E6C = 0; } break
//
// The tail also has a second switch at 0x4B7971 over CItem::mType 304..324
// (jpt_4B7987 / byte_4B8778, both read out of the exe): types 305 and 315
// pass through and the raycast is retried from the hit point; 308, 309, 311,
// 316, 318, 319 and 321..323 do nothing; every other type takes the hit.
//
// The tiny functions at 0x4B8A00..0x4B8C70 are the out-of-line copies of this
// translation unit's inline helpers, disassembled and inlined here rather than
// called: 0x4B8B70 zeroes a CVector, 0x4B8B80 is CVector::CVector(x,y,z),
// 0x4B8BA0 sets CSuper::mAnimSpeed, 0x4B8BB0 is abs(int), 0x4B8BC0 reads bit 3
// of CManipOb+0x10C, 0x4B8BD0 reads CWeb+0x102, 0x4B8BE0 sets the knockback
// timers, 0x4B8C00 sets CPowerUp+0x124, 0x4B8C10 sets CCamera::field_12C to -1,
// 0x4B8C20 zeroes SHitInfo::field_C, 0x4B8C30 zeroes an SLineInfo, 0x4B8C70
// reads CCamera::mCameraMode. 0x4B8A00..0x4B8B60 are one-time initialisers for
// globals this function never touches.
//
// ADDRESS FINDINGS made while porting this (all cross-checked against Mac
// symbol ORDER, since tools/names.json and the maintainer's IDB have neither):
//   * 0x466CE0 is CPlayer::DoPhysics. PLAN.md records its PC address as
//     unlocated and spidey.cpp carries a printf stub for it. The Mac build
//     orders Physics_SetGravity (0xA7270), DoPhysics (0xA7340),
//     DoSwingingPhysics (0xA82A0), DoCrawlingPhysics (0xA8640); the PC has
//     Physics_SetGravity (0x466C70), sub_466CE0, sub_467D20,
//     CPlayer_DoCrawlingPhysics (0x467FD0) in the same order, and sub_466CE0
//     dispatches to both sub_467D20 and 0x467FD0 exactly the way DoPhysics
//     picks the swinging and crawling variants.
//   * 0x467D20 is CPlayer::DoSwingingPhysics, by the same ordering.
//   * 0x4BD510 is CPlayer::ReadAnalogueInput (already @Ok in spidey.cpp with
//     no address recorded). Its first instructions copy field_E2D/field_E2E
//     into field_E2F/field_E30, clear both and bail on field_E18 with
//     field_1AC, which is what the implemented body does.
//   * 0x4C0EE0 CheckJumpingR1ZipWeb, 0x4C1460 CheckJumpingR2ZipWeb,
//     0x4C18A0 CheckJumpingSwingWeb, 0x4C1EB0 CheckJumpingSmashKick,
//     0x4C2090 CheckJump, 0x4C24E0 CheckLanded, 0x4C0510 CheckWebShot,
//     0x4BF8A0 CheckForwards, 0x4BE8C0 CheckFenceSurfaceTransition,
//     0x4BEA90 HandleControlsForSurfaceTransition,
//     0x4BEB70 CheckInteriorSurfaceTransition,
//     0x4BF070 CheckExteriorSurfaceTransition, 0x4C6960 LockTargetTorsoAngle,
//     0x4C2B40 DoShadowCheck, 0x4C8F40 ProcessSFXArray,
//     0x4B9390 GetPerpendicularisationRadius. Every one of these is already
//     declared (and mostly implemented) in spidey.h under that name; the
//     addresses were simply never recorded.
//   * 0x4C8410 is CPlayer::SelectTargetBaddy. tools/names.json still calls it
//     sub_4C8410 and the maintainer's IDB has no name for it, but spidey.cpp
//     already decompiled it under that name; state 0x4000 calls it.
//   * 0x4BFEC0 is CheckStickToWall, not CPlayer::DoPhysics. This confirms the
//     correction PLAN.md already made against names.json and the IDB.
//
// SPIDEY.H ITEMS FOR ITS OWNER (all reached by byte offset below):
//   CPlayer::UpdateAndTrackCombo returns i32, not void: state 0x800 switches
//   on it (0 = combo ended, 2/3/6 = which follow-up move to start).
//   Unnamed CPlayer fields this file uses: 0x231, 0x370, 0x510 (u8),
//   0x514 (CVector), 0x520 (CSVector), 0x553, 0x54E, 0x8EB, 0x8EC, 0xA80,
//   0xAB4, 0xAD6, 0xAE, 0xB84 (CSVector), 0xC58, 0xD7C, 0xDF0 (i16),
//   0x1C1 (u8[20][16] stride 16), 0x2D1, 0xE24, 0xE28, 0xE3C, 0xE48 is
//   mHeldObject already, 0xE4C (SHandle), 0xE54 (SHandle), 0xE70 (SHandle),
//   0xE8E (u16), 0xEC8.
void SpideyAI0(CPlayer *pPlayer)
{
	u32 keyMappingA;
	u32 keyMappingB;

	// var_49C: mVel.vy as it was before DoPhysics ran, compared against the
	// post-physics value by state 4.
	i32 velYBeforePhysics = 0;

	// var_4A0: the animation whose follow-on was just resolved, 0xFFFF when
	// no animation finished this tick. Only ever read 16 bits wide.
	i32 animJustFinished = 0xFFFF;

	pPlayer->field_AE4 = 1;

	PCINPUT_GetKeyboardMappingForAction(0x40, &keyMappingA);
	PCINPUT_GetKeyboardMappingForAction(0x100, &keyMappingB);

	// PC-only: with the "suit 5" costume the two mapped keys together toggle
	// field_57C, which drives the 0x800 CItem flag right below.
	if (G_CURRENTSUIT == 5
		&& PCINPUT_IsKeyPressed((u8)keyMappingA, 1) != 0
		&& PCINPUT_IsKeyPressed((u8)keyMappingB, 1) != 0
		&& pPlayer->field_1AC == 0
		&& pPlayer->field_E18 == 0)
	{
		i8 wasSet = pPlayer->field_57C;

		reinterpret_cast<u8*>(pPlayer->field_E0C)[0x51] = 0;
		pPlayer->field_57C = (i8)(wasSet == 0);
		pPlayer->field_570 = 0;
		SFX_PlayPos(0x1F, &pPlayer->mPos, 0);
	}

	if (pPlayer->field_57C != 0)
	{
		pPlayer->mFlags |= 0x800;
	}
	else
	{
		pPlayer->mFlags &= 0xF7FF;
	}

	if (gWaterEffect == 1)
	{
		G_DB_SKY_COLOR = G_DB_SKY_COLOR_TARGET;
		Db_UpdateSky();
		gWaterEffect = 0;
	}

	if (pPlayer->field_36C != 0)
	{
		pPlayer->field_36C -= pPlayer->field_80;
		if (pPlayer->field_36C < 0)
		{
			pPlayer->field_36C = 0;
		}
	}

	if (pPlayer->field_534 != 0)
	{
		pPlayer->field_534 -= pPlayer->field_80;
		if (pPlayer->field_534 < 0)
		{
			pPlayer->field_534 = 0;
		}
	}

	// electrocution timer: keeps the pad buzzing while it runs down.
	if (pPlayer->field_50C != 0)
	{
		if (*gSaveGameVibration != 0)
		{
			Pad_ActuatorOn(0, (u16)pPlayer->field_50C, 1, 0xDB);
		}

		pPlayer->field_50C -= pPlayer->field_80;
		if (pPlayer->field_50C <= 0)
		{
			pPlayer->field_50C = 0;
			Effects_UnElectrify(pPlayer);
		}
	}

	// spidey-sense buzz timer (field_ECC), its SFX handle (field_ED0) and the
	// extra body part it shows (field_ED4).
	if (pPlayer->field_ECC != 0)
	{
		i32 buzzLeft = pPlayer->field_ECC - pPlayer->field_80;

		pPlayer->field_ECC = buzzLeft;
		if (buzzLeft <= 0)
		{
			pPlayer->field_ECC = 0;
		}
		else
		{
			CBody *pBuzz;

			// 0xEC8: the buzz kind. 1 also triggers the water effect.
			if (PLR_I32(pPlayer, 0xEC8) == 1)
			{
				gWaterEffect = 1;
			}

			if (pPlayer->field_ED0 == 0)
			{
				pPlayer->field_ED0 = SFX_Play(0x1A, 0x2000, 0);
			}

			if ((i32)(Pad_GetActuatorTime(0, 1) & 0xFFFF) < buzzLeft
				&& *gSaveGameVibration != 0)
			{
				Pad_ActuatorOn(0, (u16)pPlayer->field_ECC, 1, 0x3C);
			}

			if (Mem_RecoverPointer(&pPlayer->field_ED4) == 0)
			{
				pBuzz = new CBody();
				pPlayer->field_ED4 = Mem_MakeHandle(pBuzz);
				pBuzz->InitItem("items");
				pBuzz->mFlags |= 0x200;
				pBuzz->mModel = 4;
				pBuzz->mType = 0x1F8;
				pBuzz->AttachTo(reinterpret_cast<CBody**>(&SpideyAdditionalBodyPartsList));

				if (G_PULSATING_HEAD_FLAG != 0)
				{
					pBuzz->mScale.vx = (i16)(pBuzz->mScale.vx << 1);
					pBuzz->mScale.vy = (i16)(pBuzz->mScale.vy << 1);
					pBuzz->mScale.vz = (i16)(pBuzz->mScale.vz << 1);
				}

				SpideyAI0_ApplyCheatScale(pBuzz, 0);
			}
		}
	}

	if (pPlayer->field_ECC == 0)
	{
		CBody *pBuzz;

		if (pPlayer->field_ED0 != 0)
		{
			SFX_Stop(pPlayer->field_ED0);
			pPlayer->field_ED0 = 0;
		}

		pBuzz = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_ED4));
		if (pBuzz != 0)
		{
			pBuzz->DeleteFrom(reinterpret_cast<CBody**>(&SpideyAdditionalBodyPartsList));
			delete pBuzz;
			pPlayer->field_ED4 = Mem_MakeHandle(0);
		}
	}

	// the two fists (field_5B8), sized from the combo bonus timer field_5B0.
	if (pPlayer->field_5AC != 0)
	{
		i32 fist;

		if (pPlayer->field_5B0 < 0x2000)
		{
			pPlayer->field_5B0 += pPlayer->field_80 * 384;
		}
		else
		{
			pPlayer->field_5B0 = 0x2000;
		}

		for (fist = 0; fist < 2; fist++)
		{
			CBody *pFist = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_5B8[fist]));

			if (pFist == 0)
			{
				pFist = new CBody();
				pPlayer->field_5B8[fist] = Mem_MakeHandle(pFist);
				pFist->InitItem("items");
				pFist->mModel = 8;
				pFist->mType = 0x1F9;
				pFist->AttachTo(reinterpret_cast<CBody**>(&SpideyAdditionalBodyPartsList));
				SpideyAI0_ApplyCheatScale(pFist, 1);
				pFist->mFlags |= 0x200;
			}

			if (pPlayer->field_5B0 < 0x2000)
			{
				i16 scale = (i16)(pPlayer->field_5B0 / 2);

				pFist->mFlags |= 0x200;
				pFist->mScale.vx = scale;
				pFist->mScale.vy = scale;
				pFist->mScale.vz = scale;
				SpideyAI0_ApplyCheatScale(pFist, 1);
			}
		}
	}
	else if (pPlayer->field_5B0 > 0)
	{
		i32 half;
		i32 fist;

		pPlayer->field_5B0 -= pPlayer->field_80 * 512;
		if (pPlayer->field_5B0 < 0)
		{
			pPlayer->field_5B0 = 0;
		}

		half = pPlayer->field_5B0 / 2;

		for (fist = 0; fist < 2; fist++)
		{
			CBody *pFist = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_5B8[fist]));

			if (pFist != 0)
			{
				if (half != 0)
				{
					pFist->mFlags |= 0x200;
					pFist->mScale.vx = (i16)half;
					pFist->mScale.vy = (i16)half;
					pFist->mScale.vz = (i16)half;
				}
				else
				{
					pFist->DeleteFrom(reinterpret_cast<CBody**>(&SpideyAdditionalBodyPartsList));
					delete pFist;
					// the original only clears the pointer half of the handle
					pPlayer->field_5B8[fist].pWhatever = 0;
				}
			}
		}
	}

	// 0x553: one-shot "first tick" flag; drops the player onto the floor.
	if (PLR_U8(pPlayer, 0x553) == 0)
	{
		i32 groundY;

		PLR_U8(pPlayer, 0x553) = 1;
		groundY = Utils_GetGroundHeight(&pPlayer->mPos, 0, 0x800, 0);
		if (groundY != -1)
		{
			pPlayer->mPos.vy = groundY - (pPlayer->field_EA8 << 12);
			pPlayer->field_E1C = 1;
			pPlayer->field_A8.vx = 0;
			pPlayer->field_A8.vy = (i16)0xF000;
			pPlayer->field_A8.vz = 0;
			pPlayer->OrientToNormal(false, &ZeroVector);
			pPlayer->SetFloorCamera(0);
			pPlayer->PutCameraBehind(0);
		}
	}

	if (pPlayer->field_5D8 == 0 && pPlayer->field_5E8 == 0)
	{
		if (pPlayer->mWebbing < 0x200)
		{
			pPlayer->IncreaseWebbing(pPlayer->field_80 * 4);
		}
		else if (pPlayer->mWebbing < 0x555)
		{
			pPlayer->IncreaseWebbing(pPlayer->field_80);
		}
	}

	if (pPlayer->field_E64 != 0)
	{
		if ((pPlayer->field_E1C & 0x400) != 0)
		{
			reinterpret_cast<CBody*>(pPlayer->field_E64)->AI();
		}
		else
		{
			delete reinterpret_cast<CBody*>(pPlayer->field_E64);
			pPlayer->field_E64 = 0;
		}
	}

	if (pPlayer->field_DBC != 0 && (pPlayer->field_DBC->mCBodyFlags & 0x40) != 0)
	{
		pPlayer->field_DBC = 0;
	}

	G_BADDY_COLLISION_CHECK = 0;

	// 0xE28: a plain countdown, nothing in the prologue reads it.
	if (PLR_I32(pPlayer, 0xE28) != 0)
	{
		PLR_I32(pPlayer, 0xE28) -= pPlayer->field_80;
		if (PLR_I32(pPlayer, 0xE28) < 0)
		{
			PLR_I32(pPlayer, 0xE28) = 0;
		}
	}

	pPlayer->mAnimSpeed = 0x10000;

	// 0xE24: "AI suspended" gate; the whole tick is skipped.
	if (PLR_U8(pPlayer, 0xE24) != 0)
	{
		return;
	}

	print_if_false(G_MECHLIST->mNextItem == 0, "2+ elements in G_MECHLIST_PLAYER");

	pPlayer->mFlags |= 4;
	pPlayer->field_E8 = pPlayer->mPos;

	velYBeforePhysics = pPlayer->mVel.vy;

	if ((pPlayer->field_E1C & 0x83000) == 0)
	{
		// 0x466CE0, identified as CPlayer::DoPhysics (see the note above).
		pPlayer->DoPhysics();
	}

	if (pPlayer->field_A8.vy > 0xD48)
	{
		pPlayer->OrientToNormal(true, &pPlayer->field_AC8);
		pPlayer->field_8E8 = 0;
		pPlayer->field_8E9 = 1;
	}
	else
	{
		pPlayer->field_8E9 = 0;
		pPlayer->OrientToNormal(false, &ZeroVector);
		pPlayer->field_8E8 = (u8)(pPlayer->field_A8.vy >= (i16)0xF5D8);
	}

	{
		i32 state = pPlayer->field_E1C;
		// 0xE3C: gTimerRelated stamp of the last tick spent in state 4.
		i32 lastAirborneTime = PLR_I32(pPlayer, 0xE3C);
		i32 camKind;

		if (state != 4)
		{
			PLR_I32(pPlayer, 0xE3C) = (i32)G_TIMER_RELATED;
		}

		camKind = pPlayer->field_540;

		if ((state & 0x3000) == 0)
		{
			if (state == 4)
			{
				if (camKind != 5
					&& pPlayer->mVel.vy > 0
					&& (u32)(G_TIMER_RELATED - lastAirborneTime) > 0x3C)
				{
					pPlayer->SetFallingCamera(15);
				}
			}
			else if (camKind != 4 && pPlayer->field_54C != 0)
			{
				pPlayer->SetSwingCamera(15);
			}
			else if (camKind != 2 && pPlayer->field_8E9 != 0)
			{
				pPlayer->SetCeilingCamera(16);
			}
			else if (camKind != 1 && pPlayer->field_8E8 != 0 && pPlayer->field_54C == 0)
			{
				pPlayer->SetWallCamera(16);
			}
			else if (camKind != 0
				&& pPlayer->field_8E8 == 0
				&& pPlayer->field_8E9 == 0
				&& pPlayer->field_54C == 0)
			{
				pPlayer->SetFloorCamera(16);
			}
		}
	}

	if (pPlayer->field_AD4 != 0)
	{
		pPlayer->mFric.vx = 1;
		pPlayer->mFric.vy = 1;
		pPlayer->mFric.vz = 1;
	}
	else if ((pPlayer->field_E1C & 0x1000000) != 0)
	{
		pPlayer->mFric.vx = 0x1F;
		pPlayer->mFric.vy = 0x1F;
		pPlayer->mFric.vz = 0x1F;
	}
	else if ((pPlayer->field_E1C & 6) != 0)
	{
		pPlayer->mFric.vx = 4;
		pPlayer->mFric.vy = 4;
		pPlayer->mFric.vz = 4;
	}
	else
	{
		pPlayer->mFric.vx = 1;
		pPlayer->mFric.vy = ((pPlayer->field_E1C & 0x40000) != 0) ? 1 : 4;
		pPlayer->mFric.vz = 1;
	}

	pPlayer->SelectAutoAimTarget();
	pPlayer->UpdateOffscreenSpideySenseIndicatorList();
	pPlayer->BuildOffscreenSpideySenseIndicatorList();

	pPlayer->mRMinor = 0x64;

	// 0xE8E: how long the player has been in a wall-crawl/swing state without
	// a collision, as a 16 bit accumulator of field_80.
	if ((pPlayer->field_E1C & 0x800004) != 0 && (pPlayer->mCollision & 2) == 0)
	{
		PLR_U16(pPlayer, 0xE8E) = (u16)(PLR_U16(pPlayer, 0xE8E) + (u16)pPlayer->field_80);
	}
	else
	{
		PLR_U16(pPlayer, 0xE8E) = 0;
	}

	pPlayer->ReadAnalogueInput();

	{
		u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
		u8 padFire;
		i32 usedDirs;

		if (pPad[0x100] == 0)
		{
			pPlayer->field_E8D = 1;
		}

		usedDirs = pPlayer->field_8E4;
		if (usedDirs != 0)
		{
			if ((usedDirs & 1) != 0 && pPlayer->field_E2D <= 0)
			{
				pPlayer->field_8E4 = usedDirs & ~1;
			}

			usedDirs = pPlayer->field_8E4;
			if ((usedDirs & 2) != 0 && pPlayer->field_E2D >= 0)
			{
				pPlayer->field_8E4 = usedDirs & ~2;
			}

			usedDirs = pPlayer->field_8E4;
			if ((usedDirs & 4) != 0 && pPlayer->field_E2E <= 0)
			{
				pPlayer->field_8E4 = usedDirs & ~4;
			}

			usedDirs = pPlayer->field_8E4;
			if ((usedDirs & 8) != 0 && pPlayer->field_E2E >= 0)
			{
				pPlayer->field_8E4 = usedDirs & ~8;
			}
		}

		if (pPlayer->field_550 != 0 && pPad[0x70] == 0)
		{
			pPlayer->field_550 = 0;
		}

		// only these states let the lookaround button be held
		if ((pPlayer->field_E1C & 0x3EBE3FCE) != 0)
		{
			pPad[0x40] = 0;
		}
		else if ((pPlayer->field_E1C & 0x1C000) != 0)
		{
			if (pPlayer->field_8EA == 0)
			{
				pPad[0x40] = 0;
			}
		}
		else if (PLR_I32(pPlayer, 0xE48) != 0 || pPlayer->field_EA4 != 4)
		{
			pPad[0x40] = 0;
		}

		padFire = pPad[0x40];

		// 0x8EB: lookaround allowed this tick.
		if (padFire != 0 && PLR_U8(pPlayer, 0x8EB) != 0)
		{
			pPlayer->field_56C += pPlayer->field_80;
		}
		else
		{
			pPlayer->field_56C = 0;
		}

		if ((u32)pPlayer->field_56C > 0xF)
		{
			if (pPlayer->field_8EA == 0)
			{
				ECameraMode mode = G_CAMERA_LIST->mCameraMode;

				if (mode != CAMERAMODE_START
					&& mode != CAMERAMODE_FAR
					&& mode != CAMERAMODE_OVERHEAD
					&& mode != CAMERAMODE_NOTHING)
				{
					pPlayer->EnterLookaroundMode();
				}
			}
			else if (padFire == 0)
			{
				pPlayer->ExitLookaroundMode();
			}
		}
		else if (pPlayer->field_8EA != 0 && padFire == 0)
		{
			pPlayer->ExitLookaroundMode();
		}

		if (pPlayer->field_8EA != 0 && (pPlayer->field_CE4 | pPlayer->field_CB4) == 0)
		{
			u8 pressed;

			if (*gPracticeDifficultyFlag != 0)
			{
				pressed = pPad[0x101];
			}
			else
			{
				pressed = pPad[0x71];
			}

			if (pressed != 0 && pPlayer->field_E64 == 0)
			{
				pPad[0x101] = 0;
				pPlayer->field_54F = 1;
				pPad[0x71] = 0;
				pPlayer->field_2C1 = 0;
				// 0x231: cleared together with field_2C1 here.
				PLR_U8(pPlayer, 0x231) = 0;
			}

			if (pPlayer->field_E2D != 0 && pPlayer->field_8E0 == 0)
			{
				i32 delta = ((pPlayer->field_E2D >> 3) * pPlayer->field_8F0 * pPlayer->field_80) >> 8;
				i32 was = *gLookaroundActiveCamAngle;
				i32 now;

				// G_GAMESTATE[14] flips the pitch direction (look inversion).
				if (G_GAMESTATE[14] == 0)
				{
					now = was + delta;
				}
				else
				{
					now = was - delta;
				}

				*gLookaroundActiveCamAngle = now;
				if ((now < 0 ? -now : now) > 0x400)
				{
					*gLookaroundActiveCamAngle = was;
				}
			}

			if (pPlayer->field_E2E != 0 && pPlayer->field_8E0 == 0)
			{
				i32 delta = ((pPlayer->field_E2E >> 3) * pPlayer->field_8F0 * pPlayer->field_80) >> 8;
				i32 now = *gLookaroundYawOffset + delta;

				*gLookaroundYawOffset = now;
				if ((now < 0 ? -now : now) > 0x400)
				{
					i32 clamped = (now > 0x400) ? (now - 0x400) : (now + 0x400);
					i16 heading = pPlayer->GetEffectiveHeading();

					if (pPlayer->field_8E9 != 0)
					{
						pPlayer->SetTargetTorsoAngle((i16)((heading - clamped) & 0xFFF), false);
					}
					else
					{
						pPlayer->SetTargetTorsoAngle((i16)((clamped + heading) & 0xFFF), false);
					}
				}
			}
		}
		else
		{
			*gLookaroundYawOffset = 0;
			*gLookaroundPitchSmoothed = 0;
			*gLookaroundYawSmoothed = 0;
		}
	}

	// field_EBC ramps up to 16 while there is any move input and back down to
	// 0 when there is none.
	if ((pPlayer->field_E2E | pPlayer->field_E2D) != 0)
	{
		if (pPlayer->field_EBC < 0x10)
		{
			pPlayer->field_EBC += pPlayer->field_80;
			if (pPlayer->field_EBC > 0x10)
			{
				pPlayer->field_EBC = 0x10;
			}
		}
	}
	else
	{
		if (pPlayer->field_EBC != 0)
		{
			pPlayer->field_EBC -= pPlayer->field_80;
		}
		if (pPlayer->field_EBC < 0)
		{
			pPlayer->field_EBC = 0;
		}
	}

	// animation follow-on: when the anim that just finished has an entry in
	// the sorted follow-on table, chain straight into it.
	if (pPlayer->mAnimFinished != 0)
	{
		u16 anim = pPlayer->mAnim;

		if (gAnimFollowOnData[anim * 2] != 0)
		{
			animJustFinished = anim;
			pPlayer->PlaySingleAnim(gAnimFollowOnData[anim * 2 + 1], 0, -1);
		}
		else if (anim == 0)
		{
			animJustFinished = 0;
			pPlayer->PlaySingleAnim(0, 0, -1);
		}
		else if (anim == 0x32)
		{
			animJustFinished = 0x32;
			pPlayer->PlaySingleAnim(Rnd(2) != 0 ? 0x33 : 0x55, 0, -1);
		}
		else if (anim == 0x33 || anim == 0x55)
		{
			animJustFinished = anim;
			pPlayer->PlaySingleAnim(0x32, 0, -1);
		}
		else if (anim == 0x39)
		{
			animJustFinished = 0x39;
			pPlayer->PlaySingleAnim(pPlayer->mAnimDir == 1 ? 0x32 : 0x13, 0, -1);
		}
		else if (anim == 0x3A)
		{
			animJustFinished = 0x3A;
			pPlayer->PlaySingleAnim(pPlayer->mAnimDir == 1 ? 0x34 : 0x13, 0, -1);
		}
	}

	// -----------------------------------------------------------------------
	// state dispatch, 0x4B211F..0x4B215D. Every case's `break` is the
	// original's `jmp def_4B215D`, the shared tail at 0x4B6E8C, which is not
	// ported yet.
	// -----------------------------------------------------------------------
	switch ((u32)pPlayer->field_E1C)
	{
		// 0x4B2164. Standing still: the idle/lean animations, the idle
		// Redbook track, and the idle web-shot at the ceiling.
		case 1:
		{
			u8 *pPad;
			u8 anyInput;
			u8 xaPlaying;
			u16 anim;
			i32 wantAnim;
			i32 roll;
			i32 webChance;
			i32 midChance;
			CWeb *pWeb;

			if (pPlayer->CheckGroundGone() != 0) goto L_ReleaseWeb;

			anim = pPlayer->mAnim;
			if (anim == 0x124) break;

			if ((anim == 0x122 && pPlayer->field_E6C != 0) || anim == 0x123)
			{
				if (pPlayer->mFrame > 0x1C && pPlayer->field_E6C != 0)
				{
					reinterpret_cast<CWeb*>(pPlayer->field_E6C)->field_102 = 2;
				}

				pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
				anyInput = (u8)(pPad[0x70] | pPad[0x60] | pPad[0x50] | pPad[0x40]
					| pPad[0x30] | pPad[0x20] | pPad[0x10] | pPad[0]
					| (u8)pPlayer->field_E2D | (u8)pPlayer->field_E2E);
				xaPlaying = *gRedbookXaPlayingMaybe;

				if (anyInput != 0)
				{
					if (xaPlaying != 0)
					{
						if (gRedbookXaRelatedOne == 0x20)
						{
							if (gRedbookXaRelatedTwo != 9)
							{
								pPlayer->PlaySingleAnim(0x124, 0, -1);
								goto L_ReleaseWeb;
							}
							Redbook_XAStop();
						}
						else if (gRedbookXaRelatedOne == 0x21 && gRedbookXaRelatedTwo == 0)
						{
							Redbook_XAStop();
						}
					}

					pPlayer->PlaySingleAnim(0x124, 0, -1);
					goto L_ReleaseWeb;
				}

				if (xaPlaying != 0) break;
				if (Rnd(0x40) != 0) break;

				if (Rnd(2) != 0)
				{
					Redbook_XAPlay(0x21, 0, 0);
				}
				else
				{
					Redbook_XAPlay(0x20, 9, 0);
				}
				break;
			}

			if (pPlayer->CheckWebShot() != 0) goto L_ReleaseWeb;
			if (pPlayer->CheckJumpingR1ZipWeb() != 0) goto L_ReleaseWeb;
			if (pPlayer->CheckKick() != 0) goto L_ReleaseWeb;
			if (pPlayer->CheckCeilingJumpingSmashPunch() != 0) goto L_ReleaseWeb;
			if (pPlayer->CheckJumpingSwingWeb() != 0) goto L_ReleaseWeb;
			if (pPlayer->CheckJumpingR2ZipWeb() != 0) goto L_ReleaseWeb;
			if (pPlayer->CheckJump() != 0) goto L_ReleaseWeb;
			if (pPlayer->CheckForwards(true) != 0) goto L_ReleaseWeb;

			if (pPlayer->field_8EA == 0)
			{
				pPlayer->field_EA6 = (i16)(pPlayer->field_EA6 + (u16)pPlayer->field_80);
			}

			anim = pPlayer->mAnim;
			if (pPlayer->field_DF8 == 0)
			{
				if (anim == 0x1B || anim == 0x1C || anim == 0x1D || anim == 0x1E)
				{
					pPlayer->PlaySingleAnim(pPlayer->field_AD4 != 0 ? 0x13 : 0, 0, -1);
				}
			}
			else
			{
				pPlayer->field_EA6 = 0;

				if (anim != 0xC8 && anim != 0xC2)
				{
					if (pPlayer->field_AD4 != 0)
					{
						if (pPlayer->field_DF4 > 0)
						{
							if (anim != 0x1D) pPlayer->PlaySingleAnim(0x1D, 0, -1);
						}
						else
						{
							if (anim != 0x1E) pPlayer->PlaySingleAnim(0x1E, 0, -1);
						}
					}
					else
					{
						if (pPlayer->field_DF4 > 0)
						{
							if (anim != 0x1B) pPlayer->PlaySingleAnim(0x1B, 0, -1);
						}
						else
						{
							if (anim != 0x1C) pPlayer->PlaySingleAnim(0x1C, 0, -1);
						}
					}
				}
			}

			// the idle "shoot a web at the ceiling" animation reaching its
			// release frame: spawn the web and fire it at the spot the
			// lookaround raycast below stored in 0x514/0x520.
			if (pPlayer->mAnim == 0x122
				&& pPlayer->mFrame >= 4
				&& pPlayer->field_E6C == 0)
			{
				pWeb = new CWeb();
				pWeb->field_F8 = (u8)pPlayer->field_5E8;
				pPlayer->field_E6C = reinterpret_cast<i32*>(pWeb);
				pWeb->field_102 = 0;
				// 0x510: "the idle web is out" flag.
				PLR_U8(pPlayer, 0x510) = 1;
				pWeb->Fire(pPlayer->mPos,
					*reinterpret_cast<CVector*>(reinterpret_cast<char*>(pPlayer) + 0x514),
					0,
					true,
					*reinterpret_cast<CSVector*>(reinterpret_cast<char*>(pPlayer) + 0x520));

				// field_12C was retyped u8* -> CKnottedWeb* while this was being
				// written; the original stores a byte at field_12C+0x6C, which is
				// CKnottedWeb::field_6C (bit2.h). Same address, same width.
				reinterpret_cast<CWeb*>(pPlayer->field_E6C)->field_12C->field_6C = 0;
				SFX_PlayPos(0x15, &pPlayer->mPos, 0);
			}

			if (pPlayer->field_8EA != 0)
			{
				wantAnim = (pPlayer->field_AD4 != 0) ? 0x13 : 0;
				if (pPlayer->mAnim != (u16)wantAnim)
				{
					pPlayer->PlaySingleAnim(wantAnim, 0, -1);
				}
				goto L_ReleaseWeb;
			}

			if ((u16)animJustFinished != 0) break;
			if ((u16)pPlayer->field_EA6 <= 0xF0) break;

			roll = Rnd(0x64);
			webChance = 0;

			if (pPlayer->field_AD4 == 0 && (u16)pPlayer->field_EA6 > 0xE10)
			{
				SLineInfo lineInfo;

				lineInfo.StartCoords = pPlayer->mPos;
				lineInfo.EndCoords.vx = pPlayer->mPos.vx;
				lineInfo.EndCoords.vy = pPlayer->mPos.vy - 0x800000;
				lineInfo.EndCoords.vz = pPlayer->mPos.vz;
				lineInfo.MinCoords.vx = 0;
				lineInfo.MinCoords.vy = 0;
				lineInfo.MinCoords.vz = 0;
				lineInfo.MaxCoords.vx = 0;
				lineInfo.MaxCoords.vy = 0;
				lineInfo.MaxCoords.vz = 0;
				lineInfo.Position.vx = 0;
				lineInfo.Position.vy = 0;
				lineInfo.Position.vz = 0;
				lineInfo.Normal.vx = 0;
				lineInfo.Normal.vy = 0;
				lineInfo.Normal.vz = 0;

				M3dColij_InitLineInfo(&lineInfo);
				M3dZone_LineToItem(&lineInfo, 1);

				if (lineInfo.pItem != 0 && (lineInfo.pFace[3] & 0x40000) == 0)
				{
					CVector *pHitPos = reinterpret_cast<CVector*>(reinterpret_cast<char*>(pPlayer) + 0x514);
					CSVector *pHitNormal = reinterpret_cast<CSVector*>(reinterpret_cast<char*>(pPlayer) + 0x520);

					pHitPos->vx = lineInfo.Position.vx;
					pHitPos->vy = lineInfo.Position.vy;
					pHitPos->vz = lineInfo.Position.vz;
					pHitNormal->vx = lineInfo.Normal.vx;
					pHitNormal->vy = lineInfo.Normal.vy;
					pHitNormal->vz = lineInfo.Normal.vz;
					webChance = 0x32;
				}
			}

			if (roll < webChance)
			{
				pPlayer->PlaySingleAnim(0x122, 0, -1);
				break;
			}

			midChance = webChance + ((0x64 - webChance) / 2);
			if (roll < midChance)
			{
				pPlayer->PlaySingleAnim(0x125, 0, -1);
				break;
			}

			pPlayer->PlaySingleAnim(0x126, 0, -1);
			break;

		L_ReleaseWeb:
			// 0x4B24D5, shared with several other states
			if (pPlayer->field_E6C != 0)
			{
				reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
				pPlayer->field_E6C = 0;
			}
			break;
		}

		// 0x4B274D. Airborne after a jump: the whole "can I grab something"
		// chain, then the landing timeout.
		case 2:
		{
			u8 *pPad;

			pPlayer->field_EA6 = 0;

			if (pPlayer->CheckStickToCeiling() != 0) break;
			if (pPlayer->CheckJumpingSmashKick() != 0) break;
			if (pPlayer->CheckJumpingSwingWeb() != 0) break;
			if (pPlayer->CheckJumpingR1ZipWeb() != 0) break;
			if (pPlayer->CheckJumpingR2ZipWeb() != 0) break;

			if (pPlayer->mAnimFinished != 0)
			{
				if (pPlayer->mAnim == 0xE7)
				{
					pPlayer->PlaySingleAnim(0xE8, 0, -1);
				}
				else if (pPlayer->mAnim == 0xD7)
				{
					pPlayer->PlaySingleAnim(0xD8, 0, -1);
				}
			}

			pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
			if (pPad[0x100] == 0
				&& (DifficultyLevel != 0 || Trig_GetLevelId() != 0x705))
			{
				pPlayer->field_E88 = 0;
			}

			if ((pPlayer->field_504 & 2) != 0
				&& (u32)(G_TIMER_RELATED - (i32)pPlayer->field_500) < 6)
			{
				if (pPlayer->ShouldPlayerDropFlail() == 0) break;

				pPlayer->PlaySingleAnim(0xAF, 0, -1);
				pPlayer->mVel.vz = 0;
				pPlayer->mVel.vx = 0;
				pPlayer->field_E1C = 0x800000;
				// 0x54E: cleared with every flail drop.
				PLR_U8(pPlayer, 0x54E) = 0;
				break;
			}

			if (pPlayer->field_E84 > 0) break;
			if (pPlayer->field_E88 > 0) break;

			pPlayer->field_E1C = 4;
			break;
		}

		// 0x4B28D9. Falling.
		case 4:
		{
			i32 velY;
			u16 anim;
			bool retarget;

			pPlayer->field_EA6 = 0;

			if (pPlayer->mHealth <= 0)
			{
				if (pPlayer->CheckLanded() != 0)
				{
					pPlayer->SwitchToDeathMode(false);
				}
				break;
			}

			if (pPlayer->CheckLanded() != 0) break;
			if (pPlayer->CheckStickToWall() != 0) break;
			if (pPlayer->CheckStickToCeiling() != 0) break;
			if (pPlayer->CheckJumpingSmashKick() != 0) break;
			if (pPlayer->CheckJumpingSwingWeb() != 0) break;
			if (pPlayer->CheckJumpingR1ZipWeb() != 0) break;
			if (pPlayer->CheckJumpingR2ZipWeb() != 0) break;

			// fallen out of the world for long enough
			if (PLR_U16(pPlayer, 0xE8E) > 0x1E0)
			{
				gLevelStatus = 8;
			}

			if (pPlayer->mAnimFinished != 0 && pPlayer->mAnim == 0xD7)
			{
				pPlayer->PlaySingleAnim(0xD8, 0, -1);
			}
			if (pPlayer->mAnimFinished != 0 && pPlayer->mAnim == 0x7F)
			{
				pPlayer->PlaySingleAnim(0xD8, 0, -1);
			}

			if ((pPlayer->field_504 & 4) != 0
				&& (u32)(G_TIMER_RELATED - (i32)pPlayer->field_500) < 6
				&& pPlayer->ShouldPlayerDropFlail() != 0)
			{
				pPlayer->PlaySingleAnim(0xAF, 0, -1);
				pPlayer->field_E1C = 0x800000;
				PLR_U8(pPlayer, 0x54E) = 0;
			}

			velY = pPlayer->mVel.vy;
			anim = pPlayer->mAnim;
			retarget = true;

			if (((velY ^ velYBeforePhysics) & 0x80000000) == 0
				&& (anim == 0xD3 || anim == 0xD4 || anim == 0xE0 || anim == 0xE1
					|| anim == 0xE7 || anim == 0xE8 || anim == 0xE2 || anim == 0xE4
					|| anim == 0xE9 || anim == 0xEB || anim == 0xAF || anim == 0xB0
					|| anim == 0x7F || anim == 0xEE || anim == 0xF1 || anim == 0xF0
					|| anim == 0xEF || anim == 0xD7 || anim == 0xD8 || anim == 0xDD
					|| anim == 0xDA || anim == 0xDE || anim == 0xDB || anim == 0x116
					|| anim == 0x114))
			{
				retarget = (pPlayer->field_54D != 0 && velY > 0);
			}

			if (retarget)
			{
				pPlayer->field_E38 = pPlayer->mPos.vy;
				// 0xE3C: gTimerRelated stamp of the last fall retarget.
				PLR_I32(pPlayer, 0xE3C) = (i32)G_TIMER_RELATED;

				anim = pPlayer->mAnim;
				if (anim != 0xAF && anim != 0xB0)
				{
					if (pPlayer->field_54D != 0)
					{
						pPlayer->PlaySingleAnim(anim == 0x118 ? 0x116 : 0x114, 0, -1);
						pPlayer->field_54D = 0;
					}
					else if (anim == 0xE7)
					{
						pPlayer->PlaySingleAnim(0xE8, 0, -1);
					}
					else if (anim == 0xE0)
					{
						pPlayer->PlaySingleAnim(0xE1, 0, -1);
					}
					else if (anim == 0xDA)
					{
						pPlayer->PlaySingleAnim(0xDB, 0, -1);
					}
					else if (anim == 0xDD)
					{
						pPlayer->PlaySingleAnim(0xDE, 0, -1);
					}
					else if (anim == 0xD7)
					{
						pPlayer->PlaySingleAnim(0xD8, 0, -1);
					}
					else if (anim != 0xE8 && anim != 0xD8)
					{
						pPlayer->PlaySingleAnim(0xD4, 0, -1);
					}
				}
			}

			if (pPlayer->field_AE5 != 0
				&& (u32)(G_TIMER_RELATED - PLR_I32(pPlayer, 0xE3C)) > 0x1E)
			{
				pPlayer->field_AE5 = 0;
				pPlayer->field_AE6 = 0;
			}
			break;
		}

		// 0x4B2BF3. On a wall.
		case 8:
		{
			u8 senseFlag;
			u8 *pPad;
			CBody *pTarget;
			i32 e84;

			pPlayer->field_EA6 = 0;

			if (pPlayer->CheckGroundGone() != 0) break;

			if (pPlayer->mAnim != 0xB2 && pPlayer->mAnim != 0xB4)
			{
				if (pPlayer->CheckForwards(true) != 0) break;
				if (pPlayer->CheckJump() != 0) break;
				if (pPlayer->CheckKick() != 0) break;
				if (pPlayer->CheckJumpingSwingWeb() != 0) break;
				if (pPlayer->CheckJumpingR1ZipWeb() != 0) break;
				if (pPlayer->CheckJumpingR2ZipWeb() != 0) break;
			}

			senseFlag = pPlayer->field_E8C;
			pPlayer->field_AE4 = (u8)(senseFlag != 0);

			if ((u16)animJustFinished == 0xFFFF) break;
			if ((u16)animJustFinished == 0xB2) break;

			if (senseFlag == 0)
			{
				// 0x4B682F
				pPlayer->SwitchToStandMode();
				break;
			}

			pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
			if (pPad[0x100] == 0)
			{
				// 0x4B6010
				pPlayer->field_E1C = 0x10;
				break;
			}

			pTarget = pPlayer->field_DBC;
			pPlayer->field_E80 = (i32)0xFFFC4000;
			if (pTarget != 0)
			{
				i32 targetVelY = pTarget->mVel.vy;

				if (targetVelY >= (i32)0xFFFC0000)
				{
					pPlayer->field_E80 = targetVelY + (i32)0xFFFC4000;
				}
				else
				{
					pPlayer->field_E80 = targetVelY * 2 - 0x3C000;
				}
			}

			pPlayer->PlaySingleAnim(0xDF, 0, -1);

			e84 = pPlayer->field_E84;
			pPlayer->field_E8D = 0;
			pPlayer->field_E1C = 0x40;
			pPlayer->field_E88 = (e84 >> 1) + e84;

			if ((pPlayer->mCollision & 1) != 0)
			{
				pPlayer->field_EBC = 0;
			}
			break;
		}

		// 0x4B2D43. Standing / running on the ground.
		case 16:
		{
			u16 followOn;
			i32 aimDelta;
			i16 heading;
			u16 anim;
			i32 newAnim;

			pPlayer->field_EA6 = 0;

			if (pPlayer->mHeldObject != 0 && (pPlayer->mCollision & 0x40) != 0)
			{
				CManipOb *pHeld;

				pPlayer->PlaySingleAnim(0x15, 0, -1);
				pHeld = pPlayer->mHeldObject;
				pPlayer->mHeldObject = 0;
				pHeld->Smash();
			}

			if (pPlayer->CheckGroundGone() != 0) break;
			if (pPlayer->CheckWebShot() != 0) break;
			if (pPlayer->CheckJump() != 0) break;
			if (pPlayer->CheckCeilingJumpingSmashPunch() != 0) break;
			if (pPlayer->CheckKick() != 0) break;
			if (pPlayer->CheckWebShot() != 0) break;
			if (pPlayer->CheckRunIntoWall() != 0) break;

			if (pPlayer->field_AD4 == 0)
			{
				if (pPlayer->CheckJumpingSwingWeb() != 0) break;
				if (pPlayer->CheckJumpingR1ZipWeb() != 0) break;
				if (pPlayer->CheckJumpingR2ZipWeb() != 0) break;
			}

			if (pPlayer->CheckInteriorSurfaceTransition() != 0
				|| pPlayer->CheckExteriorSurfaceTransition() != 0
				|| pPlayer->CheckFenceSurfaceTransition() != 0)
			{
				// the original calls the out of line CVector zero helper at
				// 0x4B8B70 on mVel here
				pPlayer->mVel.vx = 0;
				pPlayer->mVel.vy = 0;
				pPlayer->mVel.vz = 0;
				break;
			}

			if (pPlayer->field_8EA != 0)
			{
				// 0x4B682F
				pPlayer->SwitchToStandMode();
				break;
			}

			if ((u8)(pPlayer->field_E2E | pPlayer->field_E2D) == 0)
			{
				pPlayer->SwitchToStandMode();
				break;
			}

			followOn = (u16)animJustFinished;
			if (followOn == 0x34)
			{
				if (pPlayer->field_AD5 == 0)
				{
					pPlayer->PlaySingleAnim(0x32, 0, -1);
				}
			}
			else if (followOn == 0x33 || followOn == 0x55)
			{
				if (pPlayer->field_AD5 != 0)
				{
					pPlayer->PlaySingleAnim(0x34, 0, -1);
				}
			}

			if (pPlayer->field_8E8 != 0)
			{
				heading = pPlayer->GetEffectiveHeading();
				aimDelta = ((u16)pPlayer->field_E32 - (i32)(u16)heading) & 0xFFF;
			}
			else
			{
				i32 turned = ((u16)pPlayer->field_E32 + (i32)G_CAMERA_LIST->field_23A) & 0xFFF;

				heading = pPlayer->GetEffectiveHeading();
				aimDelta = (turned - (i32)(u16)heading) & 0xFFF;
			}

			if (aimDelta <= 0x600) break;
			if (aimDelta >= 0xA00) break;

			anim = pPlayer->mAnim;
			if (anim == 0x15)
			{
				newAnim = 0x19;
			}
			else if (anim == 0x34)
			{
				newAnim = 0x5B;
			}
			else if (anim == 0x32 || anim == 0x33)
			{
				newAnim = 0x59;
			}
			else
			{
				break;
			}

			pPlayer->PlaySingleAnim(newAnim, 0, -1);
			pPlayer->mVel.vx = 0;
			pPlayer->mVel.vy = 0;
			pPlayer->mVel.vz = 0;
			pPlayer->LockTargetTorsoAngle();
			pPlayer->field_E1C = 0x400000;
			break;
		}

		// 0x4B269B. Wall jump wind up.
		case 64:
		{
			u16 anim = pPlayer->mAnim;

			pPlayer->field_EA6 = 0;

			if (anim == 0xD2)
			{
				if (pPlayer->mAnimFinished == 0) break;
				pPlayer->PlaySingleAnim(0xD3, 0, -1);
			}
			else if (anim == 0xDF)
			{
				if (pPlayer->mAnimFinished == 0) break;
				pPlayer->PlaySingleAnim(Rnd(2) != 0 ? 0xE7 : 0xE0, 0, -1);
			}
			else
			{
				break;
			}

			pPlayer->field_E84 = 0x70000;
			pPlayer->field_E80 = (i32)0xFFFC4000;
			if (pPlayer->field_DBC != 0)
			{
				pPlayer->field_E80 = pPlayer->field_DBC->mVel.vy - 0x3C000;
			}
			pPlayer->field_E1C = 2;
			pPlayer->field_E88 = 0xA8000;
			break;
		}

		// 0x4B2892. Dead, waiting out the respawn countdown.
		case 128:
		{
			i32 elapsed = 0;

			pPlayer->field_EA6 = 0;

			if (pPlayer->mAnimFinished != 0)
			{
				if (*gSpideyDeathTimer >= 0x78)
				{
					gInitRelatedTwo = 0;
					gLevelStatus = 2;
					Flash_Reset();
				}
				elapsed = *gSpideyDeathTimer + pPlayer->field_80;
			}

			*gSpideyDeathTimer = elapsed;
			break;
		}

		// 0x4B2F80. Starting a swing.
		case 0x100:
		{
			pPlayer->field_EA6 = 0;
			pPlayer->field_54C = 1;

			// ebx still holds the 13 loaded at 0x4B211A here
			if (pPlayer->mFrame < 13) break;

			pPlayer->field_E80 = (i32)0xFFFC4000;
			if (pPlayer->field_DBC != 0)
			{
				pPlayer->field_E80 = pPlayer->field_DBC->mVel.vy - 0x3C000;
			}

			pPlayer->field_E1C = 0x200;
			pPlayer->field_E20 = 0;
			SFX_PlayPos(9, &pPlayer->mPos, 0);

			pPlayer->field_E84 = 0x70000;
			pPlayer->field_E88 = 0xA8000;
			pPlayer->field_A8.vx = 0;
			pPlayer->field_A8.vy = (i16)0xF000;
			pPlayer->field_A8.vz = 0;

			if ((pPlayer->field_DAC.vx | pPlayer->field_DAC.vy | pPlayer->field_DAC.vz) != 0)
			{
				CVector normal;

				normal.vx = (pPlayer->mPos.vx - pPlayer->field_DAC.vx) >> 12;
				normal.vy = 0;
				normal.vz = (pPlayer->mPos.vz - pPlayer->field_DAC.vz) >> 12;
				VectorNormal(reinterpret_cast<VECTOR*>(&normal), reinterpret_cast<VECTOR*>(&normal));
				pPlayer->OrientToNormal(true, &normal);
				pPlayer->field_8E8 = 0;
			}
			else
			{
				pPlayer->OrientToNormal(false, &ZeroVector);
			}
			break;
		}

		// 0x4B50F1. The end of a scripted "stand up here" move: teleport to
		// hook 2 and face the stored normal.
		case 0x80000:
		{
			CVector hook;
			CVector up;

			pPlayer->field_EA6 = 0;
			if (pPlayer->mAnimFinished == 0) break;

			pPlayer->field_AD4 = 1;

			hook.vx = 0;
			hook.vy = 0;
			hook.vz = 0;
			M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hook), pPlayer, 2);
			pPlayer->mPos = hook;

			pPlayer->field_AC8.vx = 0;
			pPlayer->field_AC8.vy = 0x1000;
			pPlayer->field_AC8.vz = 0;

			// 0xB84: the CSVector angles the scripted move ends on.
			pPlayer->field_A8.vx = PLR_I16(pPlayer, 0xB84);
			pPlayer->field_A8.vy = PLR_I16(pPlayer, 0xB86);
			pPlayer->field_A8.vz = PLR_I16(pPlayer, 0xB88);

			up.vx = 0;
			up.vy = 0x1000;
			up.vz = 0;
			pPlayer->OrientToNormal(true, &up);
			pPlayer->LockTargetTorsoAngle();
			pPlayer->SwitchToStandMode();
			break;
		}

		// 0x4B5CFE. Reaching down for a pickup.
		case 0x100000:
		{
			u16 anim;
			CManipOb *pObject;

			pPlayer->field_EA6 = 0;

			if (pPlayer->mAnimFinished != 0)
			{
				pPlayer->SwitchToStandMode();
				break;
			}

			if (pPlayer->mHeldObject != 0) break;

			anim = pPlayer->mAnim;
			if (anim == 0)
			{
				// 0x4B6597
				pPlayer->SwitchToStandMode();
				break;
			}

			if (!((anim == 0xC4 && pPlayer->mFrame >= 0xA)
				|| (anim == 0xBE && pPlayer->mFrame >= 0xA)))
			{
				break;
			}

			// 0xE4C: SHandle of the object the reach was started for.
			pObject = reinterpret_cast<CManipOb*>(Mem_RecoverPointer(
				reinterpret_cast<SHandle*>(reinterpret_cast<char*>(pPlayer) + 0xE4C)));
			if (pObject == 0) break;

			if (Utils_CrapDist(pPlayer->mPos, pObject->mPos) >= 0x300) break;

			pPlayer->mHeldObject = pObject;
			pObject->Pickup();
			SFX_Play(7, 0x2000, 0);
			break;
		}

		// 0x4B5F4D. Rolling: the roll angle is driven straight off the frame
		// number, so the player spins exactly once over the animation.
		case 0x400000:
		{
			i32 frame;

			if (pPlayer->CheckGroundGone() != 0) break;

			pPlayer->mAnimSpeed = 0x20000;

			if ((u16)animJustFinished != 0xFFFF)
			{
				u8 *pAnimEntry;
				i32 numFrames;

				frame = pPlayer->mFrame;
				if (pPlayer->CheckJump() == 0) break;

				// Animations[region * 17] (export.h) is really
				// gCostumeRegionEntries[region * 17 + 2]; +0x100 is the
				// animation's frame count.
				pAnimEntry = reinterpret_cast<u8*>(Animations[(i32)pPlayer->mRegion * 17]);
				numFrames = *reinterpret_cast<i32*>(pAnimEntry + 0x100);

				pPlayer->field_548 = (i32)((u32)(frame << 11) / (u32)(numFrames - 1));
				pPlayer->OrientToNormal(false, &ZeroVector);
				pPlayer->field_548 = 0;
				break;
			}

			pPlayer->field_548 = 0x800;
			pPlayer->OrientToNormal(false, &ZeroVector);
			pPlayer->field_548 = 0;

			if (pPlayer->CheckJump() != 0) break;

			if ((u8)(pPlayer->field_E2E | pPlayer->field_E2D) == 0)
			{
				// 0x4B682F
				pPlayer->SwitchToStandMode();
				break;
			}

			// 0x4B6010
			pPlayer->field_E1C = 0x10;
			break;
		}

		// 0x4B5DDB. Knocked down / getting back up.
		case 0x800000:
		{
			u16 lastAnim = (u16)animJustFinished;
			u16 anim;

			pPlayer->field_EA6 = 0;

			// 0x54E: the flail-drop chain is running.
			if (PLR_U8(pPlayer, 0x54E) != 0)
			{
				if (lastAnim == 0xAF)
				{
					pPlayer->PlaySingleAnim(0xB1, 0, -1);
				}
				else if (lastAnim == 0xB1)
				{
					pPlayer->field_E1C = 4;
					PLR_U8(pPlayer, 0x54E) = 0;
					break;
				}
			}

			if (PLR_U16(pPlayer, 0xE8E) > 0x1E0)
			{
				gLevelStatus = 8;
			}

			if (pPlayer->mAnim == 0x11D
				&& pPlayer->mFrame >= 7
				&& (u32)(G_TIMER_RELATED - pPlayer->field_5B4) > 0x3C)
			{
				pPlayer->field_5AC = 5;
				pPlayer->field_5B0 = 0;
				pPlayer->field_5B4 = (i32)G_TIMER_RELATED;
				SFX_PlayPos(0x16, &pPlayer->mPos, 0);
			}

			if (lastAnim == 0xB4 || lastAnim == 0xAE)
			{
				pPlayer->field_36C = 0x1E;
				pPlayer->field_AE5 = 0;
				pPlayer->SwitchToStandMode();
				break;
			}

			if (lastAnim == 0xAA || lastAnim == 0xBB || lastAnim == 0xB5
				|| lastAnim == 0x11D)
			{
				// 0x4B6597
				pPlayer->SwitchToStandMode();
				break;
			}

			anim = pPlayer->mAnim;
			if (anim != 0xAF && anim != 0xB0) break;
			if ((pPlayer->mCollision & 2) == 0) break;

			pPlayer->PlaySingleAnim(0xB2, 0, -1);
			if (pPlayer->mHealth > 0) break;

			pPlayer->SwitchToDeathMode(false);
			break;
		}

		// 0x4B6DD8. Holding a web dome: pop it when the player struggles.
		case 0x20000000:
		{
			u8 *pPad;

			pPlayer->mRMinor = 0x96;

			if ((u16)animJustFinished == 0x11C)
			{
				pPlayer->SwitchToStandMode();
				pPlayer->mRMinor = 0x64;
				break;
			}

			if (pPlayer->mAnim == 0x11C) break;

			pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
			if ((u32)(G_TIMER_RELATED - pPlayer->field_374) <= 0x96
				&& pPad[0x120] == 0 && pPad[0x130] == 0 && pPad[0x100] == 0)
			{
				break;
			}

			pPlayer->PlaySingleAnim(0x11C, 0, -1);
			SFX_PlayPos(0x23, &pPlayer->mPos, 0);

			{
				CDome *pDome = reinterpret_cast<CDome*>(
					Mem_RecoverPointer(&pPlayer->field_AB8));

				if (pDome != 0)
				{
					pDome->Burst();
					pPlayer->field_AB8 = Mem_MakeHandle(0);
				}
			}
			break;
		}

		// 0x4B6DCA
		case 0x40000000:
			pPlayer->field_EA6 = 0;
			break;

		// 0x4B6D81. Pulling a switch.
		case 0x80000000u:
		{
			CSwitch *pSwitch;

			if (pPlayer->field_E20 != 0)
			{
				// 0x4B509C
				if ((u16)animJustFinished != 0xFFFF)
				{
					pPlayer->SwitchToStandMode();
				}
				break;
			}

			if (pPlayer->mFrame < 0xC) break;

			// 0xE54: SHandle of the switch being pulled.
			pSwitch = reinterpret_cast<CSwitch*>(Mem_RecoverPointer(
				reinterpret_cast<SHandle*>(reinterpret_cast<char*>(pPlayer) + 0xE54)));
			if (pSwitch != 0)
			{
				pSwitch->Flick();
			}

			pPlayer->field_E20++;
			break;
		}

		// 0x4B3C33. The frame a swing web is released on: spawn the CSwinger
		// that actually carries the player.
		case 0x200:
		{
			pPlayer->field_EA6 = 0;

			if (pPlayer->field_E20 == 0)
			{
				G_CAMERA_LIST->field_12C = 2;
				pPlayer->PutCameraBehind(0x10);
				pPlayer->field_E20++;
			}

			if ((pPlayer->field_504 & 0x200) != 0
				&& (u32)(G_TIMER_RELATED - (i32)pPlayer->field_500) < 6)
			{
				if (pPlayer->ShouldPlayerDropFlail() != 0)
				{
					pPlayer->PlaySingleAnim(0xAF, 0, -1);
					pPlayer->mVel.vz = 0;
					pPlayer->mVel.vx = 0;
					pPlayer->field_E1C = 0x800000;
					PLR_U8(pPlayer, 0x54E) = 0;
				}
				else
				{
					pPlayer->PlaySingleAnim(0xD8, 0, -1);
					pPlayer->field_E1C = 4;
				}
				pPlayer->field_54D = 0;
			}

			if (pPlayer->CheckStickToCeiling() != 0) break;

			if (pPlayer->mVel.vy < 0)
			{
				pPlayer->field_E2D = (char)0x81;
				pPlayer->field_E2E = 0;
				pPlayer->field_EBC = 0x10;
				break;
			}

			pPlayer->field_E1C = 0x400;
			pPlayer->PlaySingleAnim(0x113, 0, -1);
			SFX_PlayPos(0x19, &pPlayer->mPos, 0);
			pPlayer->DecreaseWebbing(0x80);

			PLR_I32(pPlayer, 0xD7C) = (i32)Utils_Dist(pPlayer->mPos, pPlayer->field_D64);
			pPlayer->CalculateSwingWebParameters(&pPlayer->field_D64);

			print_if_false(pPlayer->field_E64 == 0, "already swinging");

			{
				void *pMem = CItem::operator new(0x190);

				if (pMem != 0)
				{
					gCSwinger_ctor(pMem, &pPlayer->field_D64,
						PLR_I32(pPlayer, 0xD7C), &pPlayer->field_D80,
						&pPlayer->field_DA0);
				}

				pPlayer->field_E64 = reinterpret_cast<i32*>(pMem);
				// the original writes this even when the allocation failed
				*reinterpret_cast<i32*>(reinterpret_cast<char*>(pMem) + 0xF8) =
					(u8)pPlayer->field_5E8;
			}

			SFX_Play(Rnd(3) + 0x15, 0x2000, 0);
			break;
		}

		// 0x4B329A. Swinging on a web. Keeps the CSwinger alive (re-attaching
		// to the far anchor when the current one runs out), and when the
		// player hits geometry decides between a stunned drop, a landing, a
		// crawl onto a fence, or simply letting the swing die out.
		case 0x400:
		{
			// var at [esp+0x13]: the swing has to stop this tick. Seeded from
			// CheckJumpingR1ZipWeb's return, which is 0 on every path that
			// gets here.
			u8 endSwing = 0;
			CBody *pSwinger;

			pPlayer->field_EA6 = 0;

			if (pPlayer->CheckJumpingSmashKick() != 0) break;
			if (pPlayer->CheckJumpingR1ZipWeb() != 0) break;

			pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);

			if (pSwinger != 0)
			{
				i32 frames;

				if (reinterpret_cast<CSwinger*>(pSwinger)->IsOneTimeToDie() != 0)
				{
					if (pPlayer->field_D60 != 0)
					{
						// 0x4B32E4: the far attach point is the live one, so
						// throw the next web and start a new CSwinger on it.
						void *pMem;

						pPlayer->PlaySingleAnim(0x118, 0, -1);
						pPlayer->mPos -= (pPlayer->field_C6C * 0xC0);
						pPlayer->DecreaseWebbing(0x80);

						pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
						CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
						if (pSwinger != 0)
						{
							delete pSwinger;
						}

						PLR_I32(pPlayer, 0xD7C) =
							(i32)Utils_Dist(pPlayer->mPos, pPlayer->field_D70);
						pPlayer->CalculateSwingWebParameters(&pPlayer->field_D70);

						pMem = CItem::operator new(0x190);

						if (pMem != 0)
						{
							gCSwinger_ctor(pMem, &pPlayer->field_D70,
								PLR_I32(pPlayer, 0xD7C), &pPlayer->field_D80,
								&pPlayer->field_DA0);
						}

						pPlayer->field_E64 = reinterpret_cast<i32*>(pMem);
						// the original writes this even when the allocation
						// failed, the same defect state 0x200 has
						*reinterpret_cast<i32*>(reinterpret_cast<char*>(pMem) + 0xF8) =
							(u8)pPlayer->field_5E8;

						SFX_Play(Rnd(3) + 0x15, 0x2000, 0);

						// Animations[region * 17] + 8 * anim + 8 is the
						// animation's frame count, the same table lookup
						// spidey.cpp uses. Read full width here, no & 0xFFFF.
						frames = *reinterpret_cast<i32*>(
							reinterpret_cast<char*>(Animations[(i32)pPlayer->mRegion * 17])
							+ 8 * (i32)pPlayer->mAnim + 8);

						// and the same missing null check on the new swinger
						reinterpret_cast<CSwinger*>(pPlayer->field_E64)
							->SetSpideyAnimFrame(frames - 1);

						pPlayer->field_D60 = 0;
						SFX_PlayPos(0x19, &pPlayer->mPos, 0);
					}
					else
					{
						// 0x4B3435: nothing left to swing on.
						endSwing = 1;
					}
				}
				else
				{
					// 0x4B343C: hold the last frame of the swing animation.
					pPlayer->mAnimSpeed = 0;

					frames = *reinterpret_cast<i32*>(
						reinterpret_cast<char*>(Animations[(i32)pPlayer->mRegion * 17])
						+ 8 * (i32)pPlayer->mAnim + 8);

					reinterpret_cast<CSwinger*>(pSwinger)->SetSpideyAnimFrame(frames - 1);
				}
			}

			// 0x4B346B: about to stop swinging in mid air, so look for the
			// surface under the hook and snap onto it.
			if (endSwing != 0 && (pPlayer->mCollision & 3) == 0)
			{
				SLineInfo hookLine;

				hookLine.StartCoords.vx = 0;
				hookLine.StartCoords.vy = 0;
				hookLine.StartCoords.vz = 0;
				hookLine.EndCoords.vx = 0;
				hookLine.EndCoords.vy = 0;
				hookLine.EndCoords.vz = 0;
				hookLine.MinCoords.vx = 0;
				hookLine.MinCoords.vy = 0;
				hookLine.MinCoords.vz = 0;
				hookLine.MaxCoords.vx = 0;
				hookLine.MaxCoords.vy = 0;
				hookLine.MaxCoords.vz = 0;
				hookLine.Position.vx = 0;
				hookLine.Position.vy = 0;
				hookLine.Position.vz = 0;
				hookLine.Normal.vx = 0;
				hookLine.Normal.vy = 0;
				hookLine.Normal.vz = 0;

				M3dUtils_GetHookPosition(
					reinterpret_cast<VECTOR*>(&hookLine.StartCoords), pPlayer, 2);

				hookLine.EndCoords.vx =
					hookLine.StartCoords.vx - (pPlayer->field_C6C.vx << 8);
				hookLine.EndCoords.vy =
					hookLine.StartCoords.vy - (pPlayer->field_C6C.vy << 8);
				hookLine.EndCoords.vz =
					hookLine.StartCoords.vz - (pPlayer->field_C6C.vz << 8);

				M3dColij_InitLineInfo(&hookLine);
				M3dZone_LineToItem(&hookLine, 1);

				if (hookLine.pItem != 0)
				{
					i32 standOff = pPlayer->field_EA8;

					pPlayer->mLineInfo.Normal.vx = hookLine.Normal.vx;
					pPlayer->mLineInfo.Normal.vy = hookLine.Normal.vy;
					pPlayer->mLineInfo.Normal.vz = hookLine.Normal.vz;

					pPlayer->mCollision |= 1;

					pPlayer->mPos.vx =
						hookLine.Position.vx + (i32)hookLine.Normal.vx * standOff;
					pPlayer->mPos.vy =
						hookLine.Position.vy + (i32)hookLine.Normal.vy * standOff;
					pPlayer->mPos.vz =
						hookLine.Position.vz + (i32)hookLine.Normal.vz * standOff;
				}
			}

			// 0x4B35FD
			if ((pPlayer->mCollision & 3) != 0)
			{
				pPlayer->mVel.vx = 0;
				pPlayer->mVel.vy = 0;
				pPlayer->mVel.vz = 0;

				SFX_PlayPos(9, &pPlayer->mPos, 0);

				pPlayer->field_AE5 = 0;

				if (pPlayer->mLineInfo.pItem != 0)
				{
					i16 hitNormalY = pPlayer->mLineInfo.Normal.vy;
					u8 land = 0;

					if (hitNormalY < (i16)0xF5D8)
					{
						// 0x4B3648: hit something facing steeply down, so
						// drop off the web stunned.
						pPlayer->field_AD4 = 0;

						pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
						if (pSwinger != 0)
						{
							CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
							delete pSwinger;
							pPlayer->field_E64 = 0;
						}

						pPlayer->field_A8.vx = pPlayer->mLineInfo.Normal.vx;
						pPlayer->field_A8.vy = hitNormalY;
						pPlayer->field_A8.vz = pPlayer->mLineInfo.Normal.vz;

						pPlayer->field_54C = 0;
						pPlayer->field_550 = 1;
						pPlayer->PlaySingleAnim(0xD5, 0, -1);
						pPlayer->field_E1C = 1;
						G_CAMERA_LIST->field_12C = -1;
						break;
					}

					if (hitNormalY > 0xD48)
					{
						if ((pPlayer->mLineInfo.pFace[3] & 0x40000) != 0)
						{
							// 0x4B376B
							endSwing = 1;
						}
						else
						{
							// 0x4B36EA: land on a wall, facing along the
							// travel direction.
							pPlayer->field_AD4 = 1;

							pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
							if (pSwinger != 0)
							{
								CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
								delete pSwinger;
								pPlayer->field_E64 = 0;
							}

							pPlayer->field_A8.vx = pPlayer->mLineInfo.Normal.vx;
							pPlayer->field_A8.vy = hitNormalY;
							pPlayer->field_A8.vz = pPlayer->mLineInfo.Normal.vz;

							pPlayer->field_AC8.vx = pPlayer->field_C6C.vx;
							pPlayer->field_AC8.vy = pPlayer->field_C6C.vy;
							pPlayer->field_AC8.vz = pPlayer->field_C6C.vz;

							pPlayer->OrientToNormal(true, &pPlayer->field_AC8);
							pPlayer->LockTargetTorsoAngle();
							land = 1;
						}
					}
					else if ((pPlayer->mLineInfo.pFace[3] & 0x40000) == 0)
					{
						// 0x4B3789: land on the floor, standing upright.
						CVector up(0, 0x1000, 0);

						pPlayer->field_AD4 = 1;

						pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
						if (pSwinger != 0)
						{
							CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
							delete pSwinger;
							pPlayer->field_E64 = 0;
						}

						pPlayer->field_A8.vx = pPlayer->mLineInfo.Normal.vx;
						pPlayer->field_A8.vy = hitNormalY;
						pPlayer->field_A8.vz = pPlayer->mLineInfo.Normal.vz;

						pPlayer->OrientToNormal(true, &up);
						land = 1;
					}
					else if ((pPlayer->mLineInfo.pFace[3] & 0x8000000) != 0)
					{
						// 0x4B3852: a fence. Walk the ray along the surface
						// normal from face to face until it leaves the fence,
						// then stick there and hand over to the fence
						// surface transition.
						SLineInfo fenceLine;
						i32 standOff = pPlayer->field_EA8;
						i32 normalX;
						i32 normalY;
						i32 normalZ;
						i32 posX;
						i32 posY;
						i32 posZ;

						pPlayer->field_A8.vx = pPlayer->mLineInfo.Normal.vx;
						pPlayer->field_A8.vy = pPlayer->mLineInfo.Normal.vy;
						pPlayer->field_A8.vz = pPlayer->mLineInfo.Normal.vz;

						posX = pPlayer->mPos.vx;
						posY = pPlayer->mPos.vy;
						posZ = pPlayer->mPos.vz;

						normalX = pPlayer->mLineInfo.Normal.vx;
						normalY = pPlayer->mLineInfo.Normal.vy;
						normalZ = pPlayer->mLineInfo.Normal.vz;

						for (;;)
						{
							fenceLine.StartCoords.vx = 0;
							fenceLine.StartCoords.vy = 0;
							fenceLine.StartCoords.vz = 0;
							fenceLine.EndCoords.vx = 0;
							fenceLine.EndCoords.vy = 0;
							fenceLine.EndCoords.vz = 0;
							fenceLine.MinCoords.vx = 0;
							fenceLine.MinCoords.vy = 0;
							fenceLine.MinCoords.vz = 0;
							fenceLine.MaxCoords.vx = 0;
							fenceLine.MaxCoords.vy = 0;
							fenceLine.MaxCoords.vz = 0;
							fenceLine.Position.vx = 0;
							fenceLine.Position.vy = 0;
							fenceLine.Position.vz = 0;
							fenceLine.Normal.vx = 0;
							fenceLine.Normal.vy = 0;
							fenceLine.Normal.vz = 0;

							fenceLine.StartCoords.vx = posX + normalX * 40;
							fenceLine.StartCoords.vy = posY + normalY * 40 + 0x4000;
							fenceLine.StartCoords.vz = posZ + normalZ * 40;
							fenceLine.EndCoords.vx = posX - normalX * 140;
							fenceLine.EndCoords.vy = posY - normalY * 140 + 0x4000;
							fenceLine.EndCoords.vz = posZ - normalZ * 140;

							M3dColij_InitLineInfo(&fenceLine);
							M3dZone_LineToItem(&fenceLine, 1);

							if (fenceLine.pItem == 0) break;
							if ((fenceLine.pFace[3] & 0x8000000) == 0) break;

							standOff = pPlayer->field_EA8;

							posX = fenceLine.Position.vx
								+ (i32)fenceLine.Normal.vx * standOff;
							posY = fenceLine.Position.vy
								+ (i32)fenceLine.Normal.vy * standOff;
							posZ = fenceLine.Position.vz
								+ (i32)fenceLine.Normal.vz * standOff;
						}

						// 0x4B3A88
						pPlayer->field_AD4 = 1;
						pPlayer->field_B09 = 1;
						pPlayer->field_54C = 0;

						pPlayer->mPos.vx = posX;
						pPlayer->mPos.vy = posY;
						pPlayer->mPos.vz = posZ;

						pPlayer->field_550 = 1;
						pPlayer->field_8E8 = 1;

						pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
						if (pSwinger != 0)
						{
							CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
							delete pSwinger;
							pPlayer->field_E64 = 0;
						}

						G_CAMERA_LIST->field_12C = -1;

						print_if_false(pPlayer->CheckFenceSurfaceTransition() != 0,
							"fence error");
						break;
					}
					else
					{
						// 0x4B3B07
						endSwing = 1;
					}

					if (land != 0)
					{
						// 0x4B37F4
						pPlayer->TidyUpZipWebLandingPosition(0x20);
						G_CAMERA_LIST->field_12C = -1;
						pPlayer->field_54C = 0;
						pPlayer->field_550 = 1;
						pPlayer->PlaySingleAnim(0x119, 0, -1);
						pPlayer->field_E1C = 1;
						break;
					}
				}
			}

			// 0x4B3B0C
			if ((pPlayer->field_504 & 0x400) != 0
				&& (u32)(G_TIMER_RELATED - (i32)pPlayer->field_500) < 6)
			{
				pPlayer->PlaySingleAnim(0xD8, 0, -1);
				pPlayer->field_E1C = 4;
				pPlayer->field_54D = 0;
				endSwing = 1;
			}

			// 0x4B3B4F
			{
				u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
				u8 padFire = pPad[0x101];

				if (padFire != 0)
				{
					pPlayer->field_E8D = 0;
				}

				// 0x4B3B66: the swing keeps going unless the fire button (or,
				// in practice mode, one of the other three) says to stop.
				if (endSwing == 0
					&& !((*gPracticeDifficultyFlag == 0 || pPlayer->field_1AC != 0)
						&& padFire != 0))
				{
					if (*gPracticeDifficultyFlag == 0) break;

					if (pPad[0x111] == 0 && pPad[0x121] == 0
						&& pPad[0x131] == 0)
					{
						break;
					}
				}

				// 0x4B3BB2: bleed off the swing and drop into the fall state.
				pPlayer->mVel.vx >>= 1;
				pPlayer->mVel.vz >>= 1;
				pPlayer->mAnimSpeed = 0x10000;

				if (pPlayer->field_E1C != 4)
				{
					pPlayer->field_E1C = 4;
					pPlayer->field_54D = 1;
				}

				G_CAMERA_LIST->field_12C = -1;
				pPlayer->field_54C = 0;
				pPlayer->field_550 = 1;

				pSwinger = reinterpret_cast<CBody*>(pPlayer->field_E64);
				if (pSwinger != 0)
				{
					CSwinger_SwingBack(reinterpret_cast<CSwinger*>(pSwinger));
					delete pSwinger;
					pPlayer->field_E64 = 0;
				}
			}
			break;
		}

		// 0x4B30AE. Punching: hand the tick to the combo tracker and start
		// whatever follow-up move it picks.
		case 0x800:
		{
			u16 attackKind;
			i32 comboState;
			CBody *pAimAt;

			pPlayer->field_EA6 = 0;
			if (pPlayer->CheckGroundGone() != 0) break;
			if (pPlayer->CheckJump() != 0) break;

			attackKind = pPlayer->field_8FC;
			if (attackKind == 0 || attackKind == 1)
			{
				u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

				if (pPad[0x110] != 0
					&& (pPad[0x120] != 0 || pPad[0x130] != 0)
					&& (u32)(G_TIMER_RELATED - pPlayer->field_898) <= 4)
				{
					CBody *pTarget = pPlayer->SelectTargetBaddy(0xBE, -0x1000, 0x1000, 0);

					pPlayer->field_DD8 = Mem_MakeHandle(pTarget);
					pPlayer->field_DE0 = (i32)G_TIMER_RELATED;
					pPlayer->field_E1C = 0x2000000;
					pPlayer->PlaySingleAnim(0x78, 0, -1);
					break;
				}
			}

			comboState = gCPlayer_UpdateAndTrackCombo(pPlayer);
			if (comboState == 0)
			{
				pPlayer->field_548 = 0;
				pPlayer->SwitchToStandMode();
				break;
			}

			if (comboState == 2)
			{
				pAimAt = pPlayer->field_DCC;
				pPlayer->field_8F8 = 1;
				pPlayer->field_552 = 0;
				pPlayer->field_E1C = 0x4000;
				pPlayer->field_8ED = 0;
			}
			else if (comboState == 3)
			{
				pAimAt = pPlayer->field_DCC;
				pPlayer->field_8F8 = 2;
				pPlayer->field_552 = 0;
				pPlayer->field_E1C = 0x8000;

				if (pPlayer->field_E2E > 0)
				{
					pPlayer->field_544 = 2;
				}
				else if (pPlayer->field_E2E < 0)
				{
					pPlayer->field_544 = 1;
				}
				else
				{
					pPlayer->field_544 = 0;
				}
				pPlayer->field_8ED = 0;
			}
			else if (comboState == 6)
			{
				pAimAt = pPlayer->field_DCC;
				pPlayer->field_8F8 = 2;
				pPlayer->field_552 = 0;
				pPlayer->field_E1C = 0x10000;
				pPlayer->field_8ED = 0;
			}
			else
			{
				break;
			}

			if (pAimAt == 0)
			{
				pPlayer->LockTargetTorsoAngle();
			}
			else
			{
				pPlayer->SetTargetTorsoAngleToThisPoint(&pAimAt->mPos);
			}
			PLR_U8(pPlayer, 0xAE) &= 0xFE;
			break;
		}

		// 0x4B3DF9. Landing out of a surface transition: snap to hook 2 and
		// then slide the stored distance along field_C84.
		case 0x1000:
		{
			u16 lastAnim = (u16)animJustFinished;
			i32 slide;
			i16 nx;
			i16 ny;
			i16 nz;
			CVector hook;
			CVector normal;

			pPlayer->field_EA6 = 0;

			if (lastAnim == 0xFFFF)
			{
				// 0x4B3FB8: eat the queued slide 8 units at a time
				i32 left = PLR_I32(pPlayer, 0xC58);

				if (left == 0) break;

				if (left >= 8)
				{
					pPlayer->mPos += (pPlayer->field_C6C * 8);
					PLR_I32(pPlayer, 0xC58) = left - 8;
				}
				else
				{
					pPlayer->mPos += (pPlayer->field_C6C * left);
					PLR_I32(pPlayer, 0xC58) = 0;
				}
				break;
			}

			if (lastAnim == 0x57)
			{
				slide = 0xC;
			}
			else if (lastAnim == 0x58)
			{
				slide = 0x10;
			}
			else
			{
				slide = (lastAnim == 0x3C) ? 0x42 : 0x37;
			}

			nx = PLR_I16(pPlayer, 0xB84);
			ny = PLR_I16(pPlayer, 0xB86);
			nz = PLR_I16(pPlayer, 0xB88);

			PLR_U8(pPlayer, 0xAD6) = 1;
			pPlayer->field_A8.vx = nx;
			pPlayer->field_A8.vy = ny;
			pPlayer->field_A8.vz = nz;

			if (ny < (i16)0xF5D8)
			{
				pPlayer->field_8E9 = 0;
				pPlayer->field_8E8 = 0;
				pPlayer->field_AD4 = 0;
				pPlayer->SwitchToStandMode();

				if (lastAnim != 0x57 && lastAnim != 0x58)
				{
					pPlayer->PlaySingleAnim(0x14, 0, -1);
					pPlayer->field_E1C = 1;
				}
			}
			else
			{
				pPlayer->field_E1C = 0x10;

				if (ny > 0xD48)
				{
					if (pPlayer->field_ADA == 0)
					{
						pPlayer->field_AD8 = 1;
					}
					pPlayer->field_8E8 = 0;
					pPlayer->field_8E9 = 1;
				}
				else
				{
					if (pPlayer->field_8E8 == 0 && pPlayer->field_ADB != 0)
					{
						pPlayer->field_AD9 = 1;
					}
					pPlayer->field_8E8 = 1;
					pPlayer->field_8E9 = 0;
				}
			}

			hook.vx = 0;
			hook.vy = 0;
			hook.vz = 0;
			M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hook), pPlayer, 2);
			pPlayer->mPos = hook;

			normal.vx = -pPlayer->field_C84.vx;
			pPlayer->field_AC8.vx = normal.vx;
			normal.vy = -pPlayer->field_C84.vy;
			pPlayer->field_AC8.vy = normal.vy;
			normal.vz = -pPlayer->field_C84.vz;
			pPlayer->field_AC8.vz = normal.vz;

			pPlayer->OrientToNormal(true, &normal);
			pPlayer->LockTargetTorsoAngle();
			pPlayer->mPos += (pPlayer->field_C84 * slide);
			break;
		}

		// 0x4B6901. Riding a zip-web dive onto a target: hold on, then either
		// time out, punch (0x7C) or kick (0x7D).
		case 0x2000000:
		{
			u16 lastAnim = (u16)animJustFinished;
			CBody *pTarget;

			pPlayer->field_EA6 = 0;

			if (lastAnim == 0x7A || lastAnim == 0x79)
			{
				// 0x4B682F
				pPlayer->SwitchToStandMode();
				break;
			}

			if (lastAnim == 0x78)
			{
				pTarget = reinterpret_cast<CBody*>(
					Mem_RecoverPointer(&pPlayer->field_DD8));

				if (pTarget != 0)
				{
					i32 dx = (pTarget->mPos.vx - pPlayer->mPos.vx) >> 12;
					i32 dz = (pTarget->mPos.vz - pPlayer->mPos.vz) >> 12;
					i32 facing = pPlayer->field_C6C.vz * dz + dx * pPlayer->field_C6C.vx;

					if (facing < 0
						&& Utils_Dist(pPlayer->mPos, pTarget->mPos) <= 0xBE)
					{
						// virtual slot 10 on the target ("grab me here?"),
						// reached with the __fastcall shim because this build
						// rejects the __thiscall keyword.
						typedef u8 (FASTCALL *SlotTenFn)(void*, void*, CVector*);
						CVector dest = pPlayer->mPos - (pPlayer->field_C6C * 0x20);
						SlotTenFn fn = *reinterpret_cast<SlotTenFn*>(
							*reinterpret_cast<char**>(pTarget) + 0x28);

						if (fn(pTarget, 0, &dest) != 0)
						{
							break;
						}
					}
				}

				pPlayer->PlaySingleAnim(0x79, 0, -1);
				pPlayer->field_DD8.pWhatever = 0;
				break;
			}

			if (pPlayer->mAnim != 0x7B) break;

			pTarget = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_DD8));

			if (pPlayer->CheckJump() != 0)
			{
				PLR_I32(pTarget, 0x2A8) &= ~0x40;
				pPlayer->field_DD8.pWhatever = 0;
				break;
			}

			pPlayer->CheckForwards(false);

			if ((u8)(pPlayer->field_E2E | pPlayer->field_E2D) == 0)
			{
				pPlayer->LockTargetTorsoAngle();
			}

			{
				u16 targetType = pTarget->mType;
				i32 holdTime = (targetType == 0x13A)
					? Utils_GetValueFromDifficultyLevel(0x1A4, 0x78, 0x78, 0x3C)
					: 0x12C;
				u8 *pPad;

				if ((u32)(G_TIMER_RELATED - pPlayer->field_DE0) > (u32)holdTime)
				{
					PLR_I32(pTarget, 0x2A8) &= ~0x40;
					pPlayer->PlaySingleAnim(0x7A, 0, -1);
					pPlayer->field_DD8.pWhatever = 0;
					break;
				}

				pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
				if (pPad[0x121] != 0 && targetType != 0x13A)
				{
					pPlayer->field_E1C = 0x8000000;
					pPlayer->PlaySingleAnim(0x7D, 0, -1);
					pPlayer->field_DE0 = (i32)G_TIMER_RELATED;
					break;
				}

				if (pPad[0x131] == 0) break;

				pPlayer->field_E1C = 0x4000000;
				pPlayer->PlaySingleAnim(0x7C, 0, -1);
			}
			break;
		}

		// 0x4B6820. The punch at the end of a zip-web dive.
		case 0x4000000:
		{
			CBody *pTarget;
			SHitInfo hit;

			pPlayer->field_EA6 = 0;

			if ((u16)animJustFinished == 0x7C)
			{
				// 0x4B682F
				pPlayer->SwitchToStandMode();
				break;
			}

			if (pPlayer->mAnim != 0x7C) break;
			if (pPlayer->mFrame < 0x10) break;

			pTarget = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_DD8));
			if (pTarget == 0) break;

			hit.field_C.vx = 0;
			hit.field_C.vy = 0;
			hit.field_C.vz = 0;
			hit.field_0 = 0x1E;
			hit.field_8 = (u16)pPlayer->GetDamageInflictedFromDifficulty(0x46);
			hit.field_C.vx = -pPlayer->field_C6C.vx;
			hit.field_4 = 2;
			hit.field_C.vy = 0;
			hit.field_C.vz = -pPlayer->field_C6C.vz;
			hit.field_18 = 0x258;
			hit.field_1A = 0x10;

			pTarget->Hit(&hit);

			pPlayer->field_DD8.pWhatever = 0;
			pPlayer->field_534 = 0x168;
			pPlayer->field_52C = (pPlayer->field_528 + 0xB) << 10;
			SFX_PlayPos(0x10, &pPlayer->mPos, 0);
			break;
		}

		// 0x4B44C9. The web-attack wind up. Before frame 5 the stick picks
		// which move the throw turns into (left/right tug web, down knock
		// down web, up web dome, or the zip web); otherwise the plain web
		// goes out on frame 5 and the recovery animation plays.
		case 0x4000:
		{
			u16 anim = pPlayer->mAnim;
			// bl at 0x4B44DD. The airborne variants of every animation in
			// this state are 10 ids higher than the standing ones.
			u8 airborne = (anim == 0x104) ? 1 : 0;
			// reached 0x4B47D8, the "the stick picked nothing" path
			u8 plainWeb = 0;

			pPlayer->field_EA6 = 0;

			if (airborne == 0 && anim != 0xFA)
			{
				if (anim != 0xFB && anim != 0x105) break;

				// 0x4B44FA: the recovery animation waits itself out.
				if (pPlayer->CheckJump() != 0) break;
				if (pPlayer->mAnimFinished == 0) break;

				pPlayer->field_8E1 = 0;
				pPlayer->SwitchToStandMode();
				break;
			}

			// 0x4B452A
			if (pPlayer->mFrame >= 5)
			{
				plainWeb = 1;
			}
			else
			{
				char leanX = pPlayer->field_E2D;

				if (leanX > 0 && (pPlayer->field_8E4 & 1) == 0)
				{
					// 0x4B4553: right, so the right tug web.
					char leanY;

					pPlayer->field_8F8 = 2;
					pPlayer->field_552 = 0;
					pPlayer->field_E1C = 0x8000;
					pPlayer->PlaySingleAnim(airborne != 0 ? 0x106 : 0xFC, 0, -1);

					leanY = pPlayer->field_E2E;

					if (leanY > 0)
					{
						pPlayer->field_544 = 2;
					}
					else if (leanY < 0)
					{
						pPlayer->field_544 = 1;
					}
					else
					{
						pPlayer->field_544 = 0;
					}

					PLR_U8(pPlayer, 0xAE) &= 0xFE;
					break;
				}

				if (leanX < 0 && (pPlayer->field_8E4 & 2) == 0)
				{
					// 0x4B45EB: left, so the left tug web.
					pPlayer->field_8F8 = 4;
					pPlayer->field_552 = 0;
					pPlayer->field_E1C = 0x10000;
					pPlayer->PlaySingleAnim(airborne != 0 ? 0x109 : 0xFF, 0, -1);
					PLR_U8(pPlayer, 0xAE) &= 0xFE;
					break;
				}

				// 0x4B4631
				if (airborne != 0)
				{
					plainWeb = 1;
				}
				else
				{
					char leanY = pPlayer->field_E2E;

					if (leanY < 0)
					{
						if ((pPlayer->field_8E4 & 8) == 0
							&& (u32)(G_TIMER_RELATED - pPlayer->field_5B4) > 0x1E
							&& pPlayer->field_AD4 == 0)
						{
							// 0x4B4669: down, so the knock down web.
							if (pPlayer->DecreaseWebbing(0x400) == 0) break;

							pPlayer->PlaySingleAnim(0x11D, 0, -1);
							pPlayer->field_E1C = 0x800000;
							break;
						}
					}
					else if (leanY > 0 && (pPlayer->field_8E4 & 4) == 0)
					{
						// 0x4B46B1: up, so the web dome.
						if (pPlayer->field_AD4 != 0)
						{
							plainWeb = 1;
						}
						else if (pPlayer->DecreaseWebbing(0xC00) == 0)
						{
							plainWeb = 1;
						}
						else
						{
							SFX_PlayPos(0x22, &pPlayer->mPos, 0);
							pPlayer->field_E1C = 0x20000000;
							pPlayer->PlaySingleAnim(0x11B, 0, -1);
							pPlayer->field_374 = (i32)G_TIMER_RELATED;
							pPlayer->field_AB8 = Mem_MakeHandle(
								new CDome(pPlayer, (u8)pPlayer->field_5E8));
							break;
						}
					}

					if (plainWeb == 0)
					{
						// 0x4B475F: no lean, so try the zip web.
						u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

						if (pPlayer->field_8EA != 0)
						{
							plainWeb = 1;
						}
						else if (pPad[0x120] == 0 && pPad[0x130] == 0)
						{
							plainWeb = 1;
						}
						else
						{
							// 0x4B4783
							pPlayer->field_DD8 = Mem_MakeHandle(
								pPlayer->SelectTargetBaddy(0xBE, -0x1000, 0x1000, 0));
							pPlayer->field_DE0 = (i32)G_TIMER_RELATED;
							pPlayer->field_E1C = 0x2000000;
							pPlayer->PlaySingleAnim(0x78, 0, -1);
							break;
						}
					}
				}
			}

			if (plainWeb == 0) break;

			// 0x4B47D8
			if (pPlayer->mAnimFinished != 0)
			{
				CWeb *pWeb = reinterpret_cast<CWeb*>(pPlayer->field_E6C);

				if (pWeb != 0)
				{
					if (pPlayer->field_5E4 != 0)
					{
						SFX_Stop((u32)pPlayer->field_5E4);
						pPlayer->field_5E4 = 0;
					}

					pWeb->SwitchToBlob();
					pPlayer->field_E6C = 0;
				}

				pPlayer->PlaySingleAnim(airborne != 0 ? 0x105 : 0xFB, 0, -1);
				break;
			}

			// 0x4B4831
			if (pPlayer->mFrame < 5) break;

			if (pPlayer->field_E6C == 0)
			{
				// 0x4B484F: throw the web.
				CWeb *pWeb;
				i32 result;

				if (pPlayer->field_552 != 0) break;

				pPlayer->field_552 = 1;

				// the same missing null check the other web spawns have
				pWeb = new CWeb();
				pPlayer->field_E6C = reinterpret_cast<i32*>(pWeb);
				pWeb->field_102 = 0;
				pWeb->field_F8 = (u8)pPlayer->field_5E8;

				if (pPlayer->field_8ED != 0)
				{
					CSVector aim;

					aim.vx = (i16)pPlayer->field_DA0.vx;
					aim.vy = (i16)pPlayer->field_DA0.vy;
					aim.vz = (i16)pPlayer->field_DA0.vz;

					result = pPlayer->FireWeb(false, 0x100, &pPlayer->field_DC0,
						true, &aim);
				}
				else
				{
					result = pPlayer->FireWeb(true, 0x100, &ZeroVector, false,
						&gTrajectoryVector);
				}

				if ((result & 1) != 0)
				{
					if (pPlayer->field_E6C != 0)
					{
						delete reinterpret_cast<CWeb*>(pPlayer->field_E6C);
					}
					pPlayer->field_E6C = 0;

					if (pPlayer->field_5E4 != 0)
					{
						SFX_Stop((u32)pPlayer->field_5E4);
						pPlayer->field_5E4 = 0;
					}
					break;
				}

				if ((result & 2) != 0) break;

				// 0x4B495A: nothing was hit, drop the web as a blob.
				if (pPlayer->field_5E4 != 0)
				{
					SFX_Stop((u32)pPlayer->field_5E4);
					pPlayer->field_5E4 = 0;
				}

				reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
				pPlayer->field_E6C = 0;
				break;
			}

			// 0x4B4993: a web is already out, so keep re-firing it while the
			// button is held and hold the animation on frame 5.
			{
				CWeb *pWeb = reinterpret_cast<CWeb*>(pPlayer->field_E6C);
				u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
				i32 result;

				if (pPad[0x110] == 0) break;

				if (reinterpret_cast<CBody*>(Mem_RecoverPointer(
						reinterpret_cast<SHandle*>(&pWeb->field_134)))
					!= pPlayer->field_DCC)
				{
					if (pPlayer->field_5E4 != 0)
					{
						SFX_Stop((u32)pPlayer->field_5E4);
						pPlayer->field_5E4 = 0;
					}

					pWeb->SwitchToBlob();
					pPlayer->field_E6C = 0;
				}

				if (pPlayer->field_E6C == 0) break;

				if (pPlayer->field_8ED != 0)
				{
					result = pPlayer->FireWeb(false, 0x80, &pPlayer->field_DC0,
						false, &gTrajectoryVector);
				}
				else
				{
					result = pPlayer->FireWeb(true, 0x80, &ZeroVector, false,
						&gTrajectoryVector);
				}

				if ((result & 1) != 0)
				{
					if (pPlayer->field_E6C != 0)
					{
						delete reinterpret_cast<CWeb*>(pPlayer->field_E6C);
					}
					pPlayer->field_E6C = 0;

					if (pPlayer->field_5E4 != 0)
					{
						SFX_Stop((u32)pPlayer->field_5E4);
						pPlayer->field_5E4 = 0;
					}
				}
				else if ((result & 2) == 0)
				{
					if (pPlayer->field_5E4 != 0)
					{
						SFX_Stop((u32)pPlayer->field_5E4);
						pPlayer->field_5E4 = 0;
					}

					reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
					pPlayer->field_E6C = 0;
				}

				// 0x4B4A90
				if (pPlayer->field_E6C == 0) break;

				pPlayer->mFrame = 5;
			}
			break;
		}

		// 0x4B402E. Firing a web: three wind up animations (0x94, 0xFC and
		// 0x106) each spawn a CWeb on their release frame, and what the web
		// hit decides the follow on animation.
		case 0x8000:
		{
			u16 anim = pPlayer->mAnim;

			pPlayer->field_EA6 = 0;

			if (anim == 0x94)
			{
				CWeb *pWeb;
				i32 result;

				if (pPlayer->field_552 != 0)
				{
					// 0x4B414E: hold the last frame of the throw.
					if (pPlayer->mFrame < 7) break;
					pPlayer->RunAnim(0x9A, (i32)(i16)pPlayer->mFrame, -1);
					break;
				}

				if (pPlayer->mFrame < 5) break;
				if (pPlayer->field_E6C != 0) break;

				pPlayer->field_552 = 1;
				PLR_I16(pPlayer, 0xDD4) = 0x28;

				// Original defect, kept: the new web is written through
				// without a null check, so a failed allocation faults.
				pWeb = new CWeb();
				pPlayer->field_E6C = reinterpret_cast<i32*>(pWeb);
				pWeb->field_F8 = (u8)pPlayer->field_5E8;
				pWeb->field_102 = 0;

				result = pPlayer->FireWeb(true, 0x258, &ZeroVector, false,
					&gTrajectoryVector);

				if ((result & 1) != 0)
				{
					if (pPlayer->field_E6C != 0)
					{
						delete reinterpret_cast<CWeb*>(pPlayer->field_E6C);
					}
					pPlayer->field_E6C = 0;
					break;
				}

				if ((result & 8) == 0 && pPlayer->field_DCC != 0)
				{
					// 0x4B4117
					pPlayer->field_E1C = 0x20000;
					pPlayer->field_E20 = 0;
					break;
				}

				if (pPlayer->field_E6C == 0) break;

				reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
				pPlayer->field_E6C = 0;
				break;
			}

			if (anim == 0xFC || anim == 0x106)
			{
				i32 releaseFrame = (anim == 0xFC) ? 5 : 9;
				i32 webTime = (anim == 0xFC) ? 0x28 : 0x40;
				i32 loseAnim = (anim == 0xFC) ? 0xFD : 0x107;
				i32 targetAnim = (anim == 0xFC) ? 0xFE : 0x108;

				if (pPlayer->field_552 == 0
					&& pPlayer->mFrame >= releaseFrame
					&& pPlayer->field_E6C == 0)
				{
					CWeb *pWeb;
					i32 result;

					PLR_I16(pPlayer, 0xDD4) = (i16)webTime;
					pPlayer->field_552 = 1;

					// same missing null check as the 0x94 branch above.
					pWeb = new CWeb();
					pPlayer->field_E6C = reinterpret_cast<i32*>(pWeb);
					pWeb->field_102 = 1;
					pWeb->field_F8 = (u8)pPlayer->field_5E8;

					if (pPlayer->field_8ED != 0)
					{
						CSVector aim;

						aim.vx = (i16)pPlayer->field_DA0.vx;
						aim.vy = (i16)pPlayer->field_DA0.vy;
						aim.vz = (i16)pPlayer->field_DA0.vz;

						result = pPlayer->FireWeb(false, 0x258,
							&pPlayer->field_DC0, true, &aim);
					}
					else
					{
						result = pPlayer->FireWeb(true, 0x258, &ZeroVector,
							false, &gTrajectoryVector);
					}

					if ((result & 1) != 0)
					{
						if (pPlayer->field_E6C != 0)
						{
							delete reinterpret_cast<CWeb*>(pPlayer->field_E6C);
						}
						pPlayer->field_E6C = 0;
					}
				}

				// 0x4B4282
				if (pPlayer->mAnimFinished == 0) break;

				if (pPlayer->field_DCC != 0 && pPlayer->field_DCC->mType == 0x197)
				{
					pPlayer->field_DCC = 0;
				}

				PLR_I16(pPlayer, 0xDD4) -= (i16)pPlayer->field_80;

				if (pPlayer->field_E6C == 0)
				{
					pPlayer->PlaySingleAnim(loseAnim, 0, -1);
					break;
				}

				if (pPlayer->field_DCC != 0)
				{
					pPlayer->PlaySingleAnim(targetAnim, 0, -1);
					// 0x4B4117
					pPlayer->field_E1C = 0x20000;
					pPlayer->field_E20 = 0;
					break;
				}

				reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
				pPlayer->field_E6C = 0;
				pPlayer->PlaySingleAnim(loseAnim, 0, -1);
				break;
			}

			// 0x4B44AC: the recovery animations just wait themselves out.
			if (anim != 0xFD && anim != 0x107 && anim != 0x9A) break;

			// 0x4B5085
			if (pPlayer->CheckJump() != 0) break;
			if (pPlayer->mAnimFinished == 0) break;
			pPlayer->SwitchToStandMode();
			break;
		}

		// 0x4B58B4. The tug-web attacks: each animation snaps its web off on
		// a given frame, and the wind up animations 0xFE and 0x108 pick one
		// of three follow ups from field_544.
		case 0x20000:
		{
			pPlayer->field_EA6 = 0;

			if (pPlayer->field_E20 != 0 && pPlayer->CheckJump() != 0) break;

			if (pPlayer->field_E6C != 0)
			{
				CVector dir;
				CVector *pPath = 0;
				u16 anim = pPlayer->mAnim;
				u8 snap = 0;

				if (anim == 0x94)
				{
					if (pPlayer->mFrame >= 0xE)
					{
						pPath = reinterpret_cast<CVector*>(
							pPlayer->CalculateTugWebPathPoints());
						dir = (pPlayer->field_C6C * 0x20)
							+ (pPlayer->field_C84 * 0x10);
						snap = 1;
					}
				}
				else if (anim == 0x102 || anim == 0x10C)
				{
					if (pPlayer->mFrame >= 9)
					{
						dir = ((*reinterpret_cast<CVector*>(&pPlayer->field_C78) * 0x18)
							+ (pPlayer->field_C84 * 0x10))
							+ (pPlayer->field_C6C * 0x18);
						snap = 1;
					}
				}
				else if (anim == 0x101 || anim == 0x10B)
				{
					if (pPlayer->mFrame >= 9)
					{
						dir = ((*reinterpret_cast<CVector*>(&pPlayer->field_C78) * -0x18)
							+ (pPlayer->field_C84 * 0x10))
							+ (pPlayer->field_C6C * 0x18);
						snap = 1;
					}
				}
				else if (anim == 0x103 || anim == 0x10D)
				{
					if (pPlayer->mFrame >= 9)
					{
						dir = (pPlayer->field_C6C * 0x20)
							+ (pPlayer->field_C84 * 0x10);
						snap = 1;
					}
				}

				if (snap != 0)
				{
					// 0x4B5BF2
					gCWeb_SwitchToSnap(reinterpret_cast<CWeb*>(pPlayer->field_E6C),
						dir, pPath);
					pPlayer->field_E6C = 0;
					pPlayer->field_E20 = 1;
				}
			}

			// 0x4B5C0A
			if (pPlayer->mAnimFinished == 0) break;

			if (pPlayer->mAnim == 0x94)
			{
				CVector back;

				back.vx = -pPlayer->field_C6C.vx;
				back.vy = -pPlayer->field_C6C.vy;
				back.vz = -pPlayer->field_C6C.vz;
				pPlayer->OrientToNormal(true, &back);
			}

			if (pPlayer->mAnim == 0xFE)
			{
				if (pPlayer->field_544 == 0)
				{
					pPlayer->PlaySingleAnim(0x103, 0, -1);
				}
				else if (pPlayer->field_544 == 1)
				{
					pPlayer->PlaySingleAnim(0x102, 0, -1);
				}
				else
				{
					pPlayer->PlaySingleAnim(0x101, 0, -1);
				}
				break;
			}

			if (pPlayer->mAnim != 0x108)
			{
				// 0x4B682F
				pPlayer->SwitchToStandMode();
				break;
			}

			if (pPlayer->field_544 == 0)
			{
				pPlayer->PlaySingleAnim(0x10D, 0, -1);
			}
			else if (pPlayer->field_544 == 1)
			{
				pPlayer->PlaySingleAnim(0x10C, 0, -1);
			}
			else
			{
				pPlayer->PlaySingleAnim(0x10B, 0, -1);
			}
			break;
		}

		// 0x4B51B2. The tug-web pull-in: fires the web when the wind up
		// animation ends, rides the line to the anchor point, and sticks the
		// player onto whatever the anchor sits on.
		case 0x40000:
		{
			u16 anim = pPlayer->mAnim;

			pPlayer->field_EA6 = 0;

			if ((anim == 0x104 || anim == 0xFA) && pPlayer->mAnimFinished != 0)
			{
				CWeb *pWeb;
				CSVector aim;

				pPlayer->PlaySingleAnim(0x10E, 0, -1);

				// the same missing null check the other web spawns have
				pWeb = new CWeb();
				pPlayer->field_E6C = reinterpret_cast<i32*>(pWeb);
				pWeb->field_F8 = (u8)pPlayer->field_5E8;
				pWeb->field_102 = 0;

				aim.vx = (i16)pPlayer->field_DA0.vx;
				aim.vy = (i16)pPlayer->field_DA0.vy;
				aim.vz = (i16)pPlayer->field_DA0.vz;

				pPlayer->field_8F8 = 8;
				pPlayer->field_E10 = 1;
				pPlayer->FireWeb(false, 0x80, &pPlayer->field_DC0, true, &aim);
				pPlayer->field_E10 = 0;
				break;
			}

			// 0x4B529E. 13 is ebx, loaded at 0x4B211A before the dispatch.
			if ((anim == 0x10E && pPlayer->mFrame >= 13) || anim == 0x10F)
			{
				CVector toAnchor;
				i32 approach;

				if (anim == 0x10E)
				{
					pPlayer->field_AD4 = 0;

					if (pPlayer->field_E6C != 0)
					{
						reinterpret_cast<CWeb*>(pPlayer->field_E6C)->SwitchToBlob();
						pPlayer->field_E6C = 0;
					}
				}

				// 0x4B52D9
				if ((pPlayer->field_504 & 0x40000) != 0
					&& (u32)(G_TIMER_RELATED - (i32)pPlayer->field_500) < 6)
				{
					pPlayer->PlaySingleAnim(0xAF, 0, -1);
					pPlayer->field_E1C = 0x800000;
					pPlayer->field_54D = 0;
					pPlayer->mVel.vx = 0;
					pPlayer->mVel.vy = 0;
					pPlayer->mVel.vz = 0;
					PLR_U8(pPlayer, 0x54E) = 0;
					break;
				}

				// 0x4B532D
				toAnchor = (pPlayer->mPos - pPlayer->field_DC0) >> 0xC;

				approach = pPlayer->field_DA0.vx * toAnchor.vx
					+ pPlayer->field_DA0.vz * toAnchor.vz
					+ pPlayer->field_DA0.vy * toAnchor.vy;

				if (approach >= 0
					&& Utils_Dist(pPlayer->mPos, pPlayer->field_DC0) >= 0x80)
				{
					// 0x4B56DF: still travelling. Let go on the fire button
					// (or, in practice mode, any of the other three), else
					// keep steering the velocity at the anchor.
					u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
					u8 practice = *gPracticeDifficultyFlag;
					u8 letGo = 0;
					CSVector aimAngles;

					if (practice == 0 || pPlayer->field_1AC != 0)
					{
						if (pPad[0x101] != 0)
						{
							letGo = 1;
						}
					}

					if (letGo == 0 && practice != 0)
					{
						if (pPad[0x111] != 0 || pPad[0x121] != 0
							|| pPad[0x131] != 0)
						{
							letGo = 1;
						}
					}

					if (letGo == 0)
					{
						// 0x4B573A
						aimAngles.vx = 0;
						aimAngles.vy = 0;
						aimAngles.vz = 0;

						Utils_CalcAim(&aimAngles, &pPlayer->mPos,
							&pPlayer->field_DC0);
						Utils_GetVecFromMagDir(&pPlayer->mVel, 0xF0, &aimAngles);

						if (pPlayer->mVel.vy >= 0) break;

						{
							i32 velY = pPlayer->mVel.vy;
							i32 velX = pPlayer->mVel.vx;
							i32 velZ = pPlayer->mVel.vz;

							if ((velY < 0 ? -velY : velY)
								<= (velX < 0 ? -velX : velX)) break;
							if ((velY < 0 ? -velY : velY)
								<= (velZ < 0 ? -velZ : velZ)) break;
						}

						if (pPlayer->CheckJumpingSwingWeb() == 0) break;

						pPlayer->mVel.vz = 0;
						pPlayer->mVel.vy = 0;
						pPlayer->mVel.vx = 0;
						break;
					}

					// 0x4B57D7: let go and fall.
					pPlayer->mVel.vx = 0;
					pPlayer->mVel.vy = 0;
					pPlayer->mVel.vz = 0;

					pPlayer->field_A8.vx = 0;
					pPlayer->field_A8.vz = 0;
					pPlayer->field_A8.vy = (i16)0xF000;

					pPlayer->field_E1C = 4;
					pPlayer->field_54D = 1;
					pPlayer->field_550 = 1;

					pPlayer->field_AC8.vx = pPlayer->field_C6C.vx;
					pPlayer->field_AC8.vy = pPlayer->field_C6C.vy;
					pPlayer->field_AC8.vz = pPlayer->field_C6C.vz;

					pPlayer->OrientToNormal(true, &pPlayer->field_AC8);
					break;
				}

				// 0x4B53BD: arrived at the anchor, snap onto it.
				G_CAMERA_LIST->field_100 = 1;
				G_CAMERA_LIST->mTripod = pPlayer;

				pPlayer->field_AE5 = 0;

				pPlayer->mPos = pPlayer->field_DC0
					+ (pPlayer->field_DA0 * (i32)pPlayer->field_EA8);

				pPlayer->mVel.vy = 0;
				pPlayer->mVel.vz = 0;
				pPlayer->mVel.vx = 0;

				if (pPlayer->field_DA0.vy <= 0xD48
					&& pPlayer->field_DA0.vy >= -0xA28)
				{
					// 0x4B544A: the anchor normal is near horizontal, so work
					// out which way the surface really faces.
					CVector surface = (pPlayer->field_558 - pPlayer->field_DC0) >> 0xC;
					i32 align;

					VectorNormal(reinterpret_cast<VECTOR*>(&surface),
						reinterpret_cast<VECTOR*>(&surface));

					align = ((pPlayer->field_DA0.vx * surface.vx) >> 12)
						+ ((pPlayer->field_DA0.vy * surface.vy) >> 12)
						+ ((pPlayer->field_DA0.vz * surface.vz) >> 12);

					if (align < 0x800)
					{
						pPlayer->field_AC8.vx = surface.vx;
						pPlayer->field_AC8.vy = surface.vy;
						pPlayer->field_AC8.vz = surface.vz;

						pPlayer->field_A8.vx = (i16)pPlayer->field_DA0.vx;
						pPlayer->field_A8.vy = (i16)pPlayer->field_DA0.vy;
						pPlayer->field_A8.vz = (i16)pPlayer->field_DA0.vz;

						pPlayer->OrientToNormal(true, &pPlayer->field_AC8);
					}
					else if (((i32)pPlayer->field_A8.vy < 0
						? -(i32)pPlayer->field_A8.vy
						: (i32)pPlayer->field_A8.vy) > 0x800)
					{
						// 0x4B553B
						CVector flipped;

						flipped.vx = -(i32)pPlayer->field_A8.vx;
						flipped.vy = -(i32)pPlayer->field_A8.vy;
						flipped.vz = -(i32)pPlayer->field_A8.vz;

						pPlayer->field_A8.vx = (i16)pPlayer->field_DA0.vx;
						pPlayer->field_A8.vy = (i16)pPlayer->field_DA0.vy;
						pPlayer->field_A8.vz = (i16)pPlayer->field_DA0.vz;

						pPlayer->OrientToNormal(true, &flipped);
					}
					else
					{
						// 0x4B5583
						pPlayer->field_A8.vx = (i16)pPlayer->field_DA0.vx;
						pPlayer->field_A8.vy = (i16)pPlayer->field_DA0.vy;
						pPlayer->field_A8.vz = (i16)pPlayer->field_DA0.vz;

						pPlayer->OrientToNormal(false, &ZeroVector);
					}
				}
				else
				{
					// 0x4B55AB: steep anchor, so use the travel direction
					// unless it is near vertical.
					i32 travelY = pPlayer->field_C6C.vy;

					if ((travelY < 0 ? -travelY : travelY) < 0x800)
					{
						pPlayer->field_AC8.vx = pPlayer->field_C6C.vx;
						pPlayer->field_AC8.vy = pPlayer->field_C6C.vy;
						pPlayer->field_AC8.vz = pPlayer->field_C6C.vz;
					}
					else
					{
						CVector surface = (pPlayer->field_558 - pPlayer->field_DC0) >> 0xC;

						VectorNormal(reinterpret_cast<VECTOR*>(&surface),
							reinterpret_cast<VECTOR*>(&surface));

						if (surface.vy < 0x800)
						{
							pPlayer->field_AC8.vx = surface.vx;
							pPlayer->field_AC8.vy = surface.vy;
							pPlayer->field_AC8.vz = surface.vz;
						}
						else
						{
							pPlayer->field_AC8.vx = 0;
							pPlayer->field_AC8.vy = 0;
							pPlayer->field_AC8.vz = 0x1000;
						}
					}

					// 0x4B5675
					pPlayer->field_A8.vx = (i16)pPlayer->field_DA0.vx;
					pPlayer->field_A8.vy = (i16)pPlayer->field_DA0.vy;
					pPlayer->field_A8.vz = (i16)pPlayer->field_DA0.vz;

					pPlayer->OrientToNormal(true, &pPlayer->field_AC8);
					pPlayer->LockTargetTorsoAngle();
				}

				// 0x4B56A9
				pPlayer->field_AD4 = 1;
				pPlayer->PlaySingleAnim(0x110, 0, -1);
				SFX_Play(9, 0x2000, 0);
				pPlayer->TidyUpZipWebLandingPosition(0x20);
				break;
			}

			// 0x4B583E: the stuck-on-the-anchor animation.
			if (anim != 0x110) break;

			if (pPlayer->field_8E8 == 0 && pPlayer->field_8E9 == 0)
			{
				pPlayer->field_AD4 = 0;
			}

			if (pPlayer->CheckJump() != 0) break;
			if (pPlayer->mAnimFinished == 0) break;

			if (pPlayer->field_8E8 == 0 && pPlayer->field_8E9 == 0)
			{
				pPlayer->PlaySingleAnim(0x14, 0, -1);
			}

			pPlayer->field_550 = 1;
			pPlayer->SwitchToStandMode();
			break;
		}

		// 0x4B4AAA. A surface transition finished: snap to hook 2, take the
		// new surface normal and decide whether we ended up standing, on a
		// wall or on a ceiling.
		case 0x2000:
		{
			CVector normal;
			CVector hookPos;
			i32 pushOut;

			pPlayer->field_EA6 = 0;

			if ((u16)animJustFinished == 0xFFFF)
			{
				// 0x4B4E1B: eat the queued slide along field_C6C, 8 units at
				// a time. 0xC54 is a second slide counter next to the 0xC58
				// one state 0x1000 uses, both inside a spidey.h PADDING run.
				i32 left = PLR_I32(pPlayer, 0xC54);

				if (left == 0) break;

				if (left >= 8)
				{
					pPlayer->mPos += (pPlayer->field_C6C * 8);
					PLR_I32(pPlayer, 0xC54) = left - 8;
				}
				else
				{
					pPlayer->mPos += (pPlayer->field_C6C * left);
					PLR_I32(pPlayer, 0xC54) = 0;
				}
				break;
			}

			pushOut = ((u16)animJustFinished == 0x3F) ? 0x42 : 0x37;

			hookPos.vx = 0;
			hookPos.vy = 0;
			hookPos.vz = 0;
			pPlayer->field_AD6 = 1;
			M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hookPos), pPlayer, 2);

			pPlayer->mPos.vx = hookPos.vx;
			pPlayer->mPos.vy = hookPos.vy;
			pPlayer->mPos.vz = hookPos.vz;

			normal.vx = pPlayer->field_C84.vx;
			normal.vy = pPlayer->field_C84.vy;
			normal.vz = pPlayer->field_C84.vz;

			pPlayer->field_AC8.vx = pPlayer->field_C84.vx;
			pPlayer->field_AC8.vy = pPlayer->field_C84.vy;
			pPlayer->field_AC8.vz = pPlayer->field_C84.vz;

			pPlayer->LockTargetTorsoAngle();

			if (pPlayer->mLineInfo.Normal.vy < (i16)0xF5D8)
			{
				// pointing far enough down: we are back on our feet.
				pPlayer->field_8E9 = 0;
				pPlayer->field_8E8 = 0;
				pPlayer->field_A8.vx = 0;
				pPlayer->field_A8.vy = (i16)0xF000;
				pPlayer->field_A8.vz = 0;
				pPlayer->OrientToNormal(true, &normal);
				pPlayer->field_AD4 = 0;

				if ((u16)animJustFinished == 0x3B
					|| (u16)animJustFinished == 0x56
					|| (u16)animJustFinished == 0x5D)
				{
					SFX_PlayPos(9, &pPlayer->mPos, 0);
					pushOut = 0;
					pPlayer->SwitchToStandMode();
				}
				else
				{
					pPlayer->PlaySingleAnim(0x14, 0, -1);
					pPlayer->field_E1C = 1;
				}

				if (pushOut != 0)
				{
					pPlayer->mPos += (pPlayer->field_C84 * pushOut);
					pushOut = 0;
				}

				pPlayer->TidyUpZipWebLandingPosition(8);

				{
					i32 height = Utils_GetGroundHeight(&pPlayer->mPos, 0, 0xC8, 0);

					if (height != -1)
					{
						pPlayer->mPos.vy = height - ((i32)pPlayer->field_EA8 << 12);
					}
					else
					{
						// 0x4B4C24: no ground under us, so step sideways
						// along field_C78 (a CVector living in three i32
						// members in spidey.h), further every other try,
						// until some ground turns up.
						i32 step = 8;
						i32 i;

						for (i = 0; i < 0x12; i++)
						{
							CVector probe;

							probe.vx = pPlayer->mPos.vx;
							probe.vy = pPlayer->mPos.vy;
							probe.vz = pPlayer->mPos.vz;

							if ((i & 1) != 0)
							{
								probe += (*reinterpret_cast<CVector*>(&pPlayer->field_C78) * step);
							}
							else
							{
								probe -= (*reinterpret_cast<CVector*>(&pPlayer->field_C78) * step);
							}

							height = Utils_GetGroundHeight(&probe, 0, 0xC8, 0);

							if (height != -1)
							{
								pPlayer->mPos.vx = probe.vx;
								pPlayer->mPos.vy = height - ((i32)pPlayer->field_EA8 << 12);
								pPlayer->mPos.vz = probe.vz;
								break;
							}

							if ((i & 1) != 0)
							{
								step = step * 2;
							}
						}
					}
				}
			}
			else
			{
				// 0x4B4D05: still on a wall or a ceiling, so keep the
				// surface normal and pick which of the two we are on.
				pPlayer->field_A8.vx = pPlayer->mLineInfo.Normal.vx;
				pPlayer->field_A8.vy = pPlayer->mLineInfo.Normal.vy;
				pPlayer->field_A8.vz = pPlayer->mLineInfo.Normal.vz;
				pPlayer->OrientToNormal(true, &normal);
				pPlayer->field_E1C = 0x10;

				if (pPlayer->field_A8.vy > 0xD48)
				{
					if (pPlayer->field_8E8 != 0 && pPlayer->field_ADA != 0)
					{
						pPlayer->field_AD8 = 1;
					}

					pPlayer->field_8E8 = 0;
					pPlayer->field_8E9 = 1;
				}
				else
				{
					if (pPlayer->field_8E9 != 0 && pPlayer->field_ADC != 0)
					{
						pPlayer->field_AD9 = 1;
					}

					pPlayer->field_8E8 = 1;
					pPlayer->field_8E9 = 0;
				}

				if (pushOut != 0)
				{
					pPlayer->mPos += (pPlayer->field_C84 * pushOut);
					pushOut = 0;
				}

				pPlayer->TidyUpZipWebLandingPosition(0x10);
			}

			// 0x4B4DED
			if (pushOut != 0)
			{
				pPlayer->mPos += (pPlayer->field_C84 * pushOut);
			}
			break;
		}

		// 0x4B601F. Throwing the held object: anim 0xC3 lobs it, anim 0xC9
		// hurls it. With an auto-aim target close by the throw is aimed at
		// it, otherwise it goes straight ahead.
		case 0x200000:
		{
			u16 anim;
			u8 isHardThrow;
			u8 doThrow;

			pPlayer->field_EA6 = 0;

			if (pPlayer->mAnimFinished != 0)
			{
				pPlayer->SwitchToStandMode();
				break;
			}

			if (pPlayer->mHeldObject == 0)
			{
				pPlayer->CheckJump();
				break;
			}

			anim = pPlayer->mAnim;
			isHardThrow = 0;

			if (anim == 0xC9 && pPlayer->mFrame >= 0x12)
			{
				isHardThrow = 1;
				doThrow = 1;
			}
			else if (anim == 0xC3 && pPlayer->mFrame >= 13)
			{
				isHardThrow = 0;
				doThrow = 1;
			}
			else
			{
				doThrow = 0;
			}

			if (pPlayer->mFrame >= 6)
			{
				pPlayer->PutCameraBehind(0);
			}

			if (doThrow == 0) break;

			{
				CVector dir;

				dir.vx = 0;
				dir.vy = 0;
				dir.vz = 0;

				if (pPlayer->field_DCC != 0)
				{
					i32 dist = (i32)Utils_CrapXZDist(pPlayer->mPos,
						pPlayer->field_DCC->mPos);

					if (dist >= 0x400)
					{
						pPlayer->mHeldObject->ThrowPos(&pPlayer->field_DCC->mPos,
							dist / 32);
					}
					else
					{
						i32 speed;

						if (isHardThrow != 0)
						{
							speed = (dist << 12) / 6;
							dir = CVector(-0x18) * pPlayer->field_C6C;
						}
						else
						{
							speed = (dist << 12) / 32;
							dir = CVector(-0x20) * pPlayer->field_C6C;
						}

						if (speed < 0x14000)
						{
							speed = 0x14000;
						}

						dir.vy += (pPlayer->field_DCC->mPos.vy - pPlayer->mPos.vy
							+ 0x80000) / speed - speed / 2;

						pPlayer->mHeldObject->Throw(&dir);
					}
				}
				else
				{
					if (isHardThrow != 0)
					{
						dir = (CVector(-0x18) * pPlayer->field_C6C)
							+ (CVector(0xE) * pPlayer->field_C84);
					}
					else
					{
						dir = (CVector(-0x20) * pPlayer->field_C6C)
							+ (CVector(0x10) * pPlayer->field_C84);
					}

					pPlayer->mHeldObject->Throw(&dir);
				}
			}

			// 0x4B62DB
			if (isHardThrow != 0)
			{
				SFX_PlayPos(0x26, &pPlayer->mPos, 0);
			}
			else
			{
				SFX_PlayPos(0x25, &pPlayer->mPos, 0);
			}

			pPlayer->mHeldObject = 0;
			break;
		}

		// 0x4B631E. The jumping smash kick: anims 0x81/0x85 trail a smoke
		// ribbon, and the kick ends either by slamming into a surface
		// (0x82/0x86) or by being bounced off it (0xAF).
		case 0x1000000:
		{
			CVector normal;

			pPlayer->field_EA6 = 0;

			if (pPlayer->field_8DC != 0)
			{
				if (pPlayer->mAnim != 0) break;

				pPlayer->field_8DC = 0;
				// 0x4B6589
				pPlayer->field_AE4 = 1;
				pPlayer->field_AE5 = 0;
				pPlayer->SwitchToStandMode();
				break;
			}

			if ((pPlayer->mAnim == 0x81 || pPlayer->mAnim == 0x85)
				&& pPlayer->mAnimFinished != 0
				&& pPlayer->field_584 == 0)
			{
				pPlayer->CreateJumpingSmashKickTrail();
			}

			if ((pPlayer->field_504 & 0x1000000) != 0
				&& (u32)(G_TIMER_RELATED - pPlayer->field_500) < 6)
			{
				// bounced off: flip upright, play the recover animation and
				// drop into the falling state.
				pPlayer->field_A8.vx = 0;
				pPlayer->field_A8.vy = (i16)0xF000;
				pPlayer->field_A8.vz = 0;
				pPlayer->OrientToNormal(false, &ZeroVector);
				pPlayer->PlaySingleAnim(0xAF, 0, -1);
				pPlayer->DestroyJumpingSmashKickTrail();
				pPlayer->mVel.vx = 0;
				pPlayer->mVel.vy = 0;
				pPlayer->mVel.vz = 0;
				pPlayer->field_E8C = 0;
				pPlayer->field_E1C = 4;
				break;
			}

			normal = (pPlayer->field_8CC - pPlayer->mPos) >> 12;

			if ((pPlayer->mCollision & 2) == 0 && normal.vy > 0)
			{
				// still flying at the target: steer straight at it.
				CVector up(-normal.vx, 0, -normal.vz);

				VectorNormal(reinterpret_cast<VECTOR*>(&normal),
					reinterpret_cast<VECTOR*>(&normal));
				VectorNormal(reinterpret_cast<VECTOR*>(&up),
					reinterpret_cast<VECTOR*>(&up));

				pPlayer->field_A8.vx = (i16)-normal.vx;
				pPlayer->field_A8.vy = (i16)-normal.vy;
				pPlayer->field_A8.vz = (i16)-normal.vz;
				pPlayer->OrientToNormal(true, &up);

				if (pPlayer->field_8D8 != 0)
				{
					pPlayer->mVel.vx = 0;
					pPlayer->mVel.vy = 0x30000;
					pPlayer->mVel.vz = 0;
					break;
				}

				{
					CVector vel = normal * 0x60;
					pPlayer->mVel.vx = vel.vx;
					pPlayer->mVel.vy = vel.vy;
					pPlayer->mVel.vz = vel.vz;
				}
				break;
			}

			// 0x4B651B. Arrived (or hit something): land the kick.
			pPlayer->field_A8.vx = 0;
			pPlayer->field_A8.vy = (i16)0xF000;
			pPlayer->field_A8.vz = 0;
			pPlayer->OrientToNormal(false, &ZeroVector);
			pPlayer->mVel.vx = 0;
			pPlayer->mVel.vy = 0;
			pPlayer->mVel.vz = 0;
			pPlayer->DestroyJumpingSmashKickTrail();
			pPlayer->PlaySingleAnim(pPlayer->mAnim == 0x81 ? 0x82 : 0x86, 0, -1);

			if (pPlayer->field_8C4 - pPlayer->field_8C8 < 0x78)
			{
				pPlayer->field_8DC = 0x29A;
				break;
			}

			pPlayer->field_8DC = 0;
			// 0x4B6589
			pPlayer->field_AE4 = 1;
			pPlayer->field_AE5 = 0;
			pPlayer->SwitchToStandMode();
			break;
		}

		// 0x4B65E4. The kick that ends a zip-web dive (anim 0x7D), then the
		// stomp its follow on animation 0x7E lands and the 0x80 hold.
		case 0x8000000:
		{
			CBody *pTarget;
			u8 *pPad;

			if (pPlayer->mAnim == 0x7D)
			{
				pPlayer->field_2C1 = 0;
				reinterpret_cast<u8*>(pPlayer->field_E0C)[0x101] = 0;
			}

			pPlayer->field_EA6 = 0;
			pPlayer->field_A80 = 0;

			pTarget = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_DD8));
			print_if_false(pTarget != 0, "Lost grab target");

			// Original defect, kept: mHealth is read straight after the
			// print_if_false above without acting on a null target, so a lost
			// target faults here.
			if (pTarget->mHealth > 0
				&& (u32)(G_TIMER_RELATED - pPlayer->field_DE0) <= 0x12C
				&& reinterpret_cast<u8*>(pPlayer->field_E0C)[0x101] == 0)
			{
				if ((u16)animJustFinished == 0x7E)
				{
					if (pTarget != 0)
					{
						SHitInfo hit;
						CVector dest;

						hit.field_C.vx = 0;
						hit.field_C.vy = 0;
						hit.field_C.vz = 0;
						hit.field_0 = 6;
						hit.field_8 = (u16)pPlayer->GetDamageInflictedFromDifficulty(0x14);
						hit.field_4 = 3;

						pTarget->Hit(&hit);

						pPlayer->field_52C = (pPlayer->field_528 + 0xB) << 10;
						pPlayer->field_534 = 0x168;

						dest = (pPlayer->mPos - (pPlayer->field_C6C * 0x20))
							+ (pPlayer->field_C84 * 0x40);

						pPlayer->CreateCombatImpactEffect(&dest, 0);
						SFX_PlayPos(0x10, &pPlayer->mPos, 0);
					}
				}
				else if (pPlayer->mAnim == 0x80
					&& reinterpret_cast<u8*>(pPlayer->field_E0C)[0x121] != 0)
				{
					pPlayer->PlaySingleAnim(0x7E, 0, -1);
				}
			}
			else
			{
				// 0x4B67BB. Target dead, held too long, or the kick key came
				// back up: throw the player off into the falling state.
				pPlayer->field_A80 = 1;
				pPlayer->field_E1C = 4;
				pPlayer->PlaySingleAnim(0x7F, 0, -1);

				pPlayer->mVel.vx += pPlayer->field_C6C.vx * 24;
				pPlayer->mVel.vy += -0x40000;
				pPlayer->mVel.vz += pPlayer->field_C6C.vz * 24;
				pPlayer->field_DD8.pWhatever = 0;
			}

			pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
			pPad[0x121] = 0;
			break;
		}

		// 0x4B6B1E. Grabbed (CheckSwitchToGrabbedMode puts us here): the
		// player is dragged towards the grabber and has to wiggle the stick
		// and mash the four face buttons to break free. field_894 counts the
		// wiggles and bleeds one back down every eight ticks on average.
		case 0x10000000:
		{
			u8 *pPad;

			pPlayer->field_EA6 = 0;

			if ((u16)animJustFinished == 0x99)
			{
				// 0x4B6597
				pPlayer->SwitchToStandMode();
				break;
			}

			if (pPlayer->field_894 != 0 && Rnd(8) == 0)
			{
				pPlayer->field_894 = pPlayer->field_894 - 1;
			}

			// left/right on the stick, counted on every sign change.
			if (pPlayer->field_87C != 0 && pPlayer->field_E2E < 0)
			{
				pPlayer->field_87C = 0;
				pPlayer->field_894++;
			}
			else if (pPlayer->field_87C == 0 && pPlayer->field_E2E > 0)
			{
				pPlayer->field_87C = 1;
				pPlayer->field_894++;
			}

			// 0x880: the same latch for up/down (field_E2D). It sits in a
			// spidey.h PADDING run, so it is reached by offset here.
			if (PLR_I32(pPlayer, 0x880) != 0 && pPlayer->field_E2D < 0)
			{
				PLR_I32(pPlayer, 0x880) = 0;
				pPlayer->field_894++;
			}
			else if (PLR_I32(pPlayer, 0x880) == 0 && pPlayer->field_E2D > 0)
			{
				PLR_I32(pPlayer, 0x880) = 1;
				pPlayer->field_894++;
			}

			// 0x884, 0x88C, 0x888 and 0x890: one "button is down" latch per
			// face button, all in the same PADDING run. Each pad entry is a
			// held byte followed by a just-pressed byte.
			pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

			if (PLR_I32(pPlayer, 0x884) != 0 && pPad[0x10] != 0)
			{
				PLR_I32(pPlayer, 0x884) = 0;
			}
			else if (PLR_I32(pPlayer, 0x884) == 0 && pPad[0x11] != 0)
			{
				pPad[0x11] = 0;
				PLR_I32(pPlayer, 0x884) = 1;
				pPlayer->field_894++;
			}

			pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

			if (PLR_I32(pPlayer, 0x88C) != 0 && pPad[0] != 0)
			{
				PLR_I32(pPlayer, 0x88C) = 0;
			}
			else if (PLR_I32(pPlayer, 0x88C) == 0 && pPad[1] != 0)
			{
				pPad[1] = 0;
				PLR_I32(pPlayer, 0x88C) = 1;
				pPlayer->field_894++;
			}

			pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

			if (PLR_I32(pPlayer, 0x888) != 0 && pPad[0x20] != 0)
			{
				PLR_I32(pPlayer, 0x888) = 0;
			}
			else if (PLR_I32(pPlayer, 0x888) == 0 && pPad[0x21] != 0)
			{
				pPad[0x21] = 0;
				PLR_I32(pPlayer, 0x888) = 1;
				pPlayer->field_894++;
			}

			pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);

			if (PLR_I32(pPlayer, 0x890) != 0 && pPad[0x30] != 0)
			{
				PLR_I32(pPlayer, 0x890) = 0;
			}
			else if (PLR_I32(pPlayer, 0x890) == 0 && pPad[0x31] != 0)
			{
				pPad[0x31] = 0;
				PLR_I32(pPlayer, 0x890) = 1;
				pPlayer->field_894++;
			}

			if (pPlayer->field_894 > 8 && pPlayer->mAnim != 0x99)
			{
				pPlayer->PlaySingleAnim(0x99, 0, -1);
			}

			// slide halfway towards the grabber every tick.
			pPlayer->mPos.vx += (pPlayer->field_EE0.vx - pPlayer->mPos.vx) >> 1;
			pPlayer->mPos.vz += (pPlayer->field_EE0.vz - pPlayer->mPos.vz) >> 1;

			if (pPlayer->field_564 != 0)
			{
				pPlayer->field_564 = 0;
				if (pPlayer->mAnim != 0x99)
				{
					pPlayer->PlaySingleAnim(0x99, 0, -1);
				}
			}
			break;
		}

		default:
			if ((u32)pPlayer->field_E1C > 0x10000)
			{
				// 0x4B50AE. Every state this compare chain dispatches
				// (0x20000 and up) has its own case label above, so only a
				// value that is not a state gets here, and the original's
				// chain drops such a value into the tail the same way.
			}
			else if (pPlayer->field_E1C == 0x10000)
			{
				// 0x4B4E9B. Web-swing/attach wind up: three animations that
				// each fire a web on their release frame and drip webbing.
				u16 anim = pPlayer->mAnim;

				pPlayer->field_EA6 = 0;

				if (anim == 0x8B)
				{
					if (pPlayer->mAnimFinished != 0)
					{
						// 0x4B6597
						pPlayer->SwitchToStandMode();
						break;
					}

					// ebp still holds the 3 loaded at 0x4B2124
					if (pPlayer->mFrame < 3) break;

					if (pPlayer->field_552 == 0)
					{
						pPlayer->field_552 = 1;
						if (pPlayer->field_8ED != 0)
						{
							pPlayer->FireWeb(false, 0x384, &pPlayer->field_DC0, false, &gTrajectoryVector);
						}
						else
						{
							pPlayer->FireWeb(true, 0x384, &ZeroVector, false, &gTrajectoryVector);
						}
					}
					else if (pPlayer->CheckJump() != 0)
					{
						break;
					}

					if (pPlayer->mFrame > 5) break;
					pPlayer->CreateWebDrips(true, false);
					break;
				}

				if (anim == 0xFF || anim == 0x109)
				{
					if (pPlayer->mFrame < 5) break;

					if (pPlayer->field_552 == 0)
					{
						pPlayer->field_552 = 1;
						if (pPlayer->field_8ED != 0)
						{
							pPlayer->FireWeb(false, 0x384, &pPlayer->field_DC0, false, &gTrajectoryVector);
						}
						else
						{
							pPlayer->FireWeb(true, 0x384, &ZeroVector, false, &gTrajectoryVector);
						}
					}

					if (pPlayer->mFrame <= 0xB)
					{
						pPlayer->CreateWebDrips(true, true);
					}

					if (pPlayer->mAnimFinished == 0) break;

					pPlayer->PlaySingleAnim(anim == 0xFF ? 0x100 : 0x10A, 0, -1);
					break;
				}

				if (anim == 0x100 || anim == 0x10A)
				{
					if (pPlayer->CheckJump() != 0) break;
					if (pPlayer->mAnimFinished == 0) break;
					pPlayer->SwitchToStandMode();
				}
			}
			else if ((u32)pPlayer->field_E1C > 0x100)
			{
				// 0x4B307C. Same as above for the 0x200..0x8000 chain: every
				// state it dispatches has its own case label, so a value
				// reaching here is not a state and falls into the tail.
			}
			break;
	}


	// -----------------------------------------------------------------------
	// def_4B215D, 0x4B6E8C..0x4B86B1. The shared tail: every state's `break`
	// above lands here, and so does the state-machine default.
	// -----------------------------------------------------------------------
	{
		u8 *pPad = reinterpret_cast<u8*>(pPlayer->field_E0C);
		i32 camYaw;
		i32 state;
		u8 anyMove;
		i32 torsoSpeed;
		i16 heading;
		i32 headingBefore;
		i32 turnLeft;
		CVector accel;
		i32 bAccelSet;
		CBody *pPlatform;
		CVector prevPos;
		i32 riseSpeed;
		SLineInfo lineInfo;
		CVector hookA;
		CVector hookB;

		// --- camera facing, 0x4B6E8C ---
		// 0xAD6: "the camera may be auto-steered this tick".
		if (PLR_U8(pPlayer, 0xAD6) != 0
			&& PLR_U8(pPlayer, 0x8EC) == 0
			&& pPlayer->field_54C == 0)
		{
			if (pPlayer->field_8E8 != 0)
			{
				pPlayer->field_551 = 0;
				if ((pPlayer->field_E1C & 0x3000) == 0)
				{
					pPlayer->PutCameraBehind(0x1E);
				}
			}
			// 0xE70: SHandle of the object the camera locks onto.
			else if (pPad[0x50] != 0 && PLR_I32(pPlayer, 0xE70) != 0)
			{
				CSVector aim;
				void *pLockTarget = Mem_RecoverPointer(
					reinterpret_cast<SHandle*>(reinterpret_cast<char*>(pPlayer) + 0xE70));

				Utils_CalcAim(&aim, &pPlayer->mPos,
					reinterpret_cast<CVector*>(reinterpret_cast<char*>(pLockTarget) + 8));
				G_CAMERA_LIST->SetCamAngle(aim.vy, 0);
				pPlayer->field_551 = 1;
			}
			else if (pPlayer->field_EA6 == 0)
			{
				pPlayer->field_551 = 0;

				// the original calls this and throws the result away
				pPlayer->GetEffectiveHeading();

				if (pPad[0x41] != 0)
				{
					pPad[0x41] = 0;
					pPlayer->field_201 = 0;
					pPlayer->PutCameraBehind(0);
					pPlayer->field_56C = 0;
				}
				else if (pPad[0x111] != 0
					&& pPlayer->field_8EA == 0
					&& (pPlayer->field_E1C & 0x2001C001) != 0)
				{
					pPad[0x111] = 0;
					// 0x2D1: cleared with the camera reset, like field_201.
					PLR_U8(pPlayer, 0x2D1) = 0;
					pPlayer->PutCameraBehind(0);
				}
				else if ((u8)(pPlayer->field_E2E | pPlayer->field_E2D) != 0
					&& pPlayer->field_E1C == 0x10)
				{
					i16 camAngle = G_CAMERA_LIST->field_23A;
					i16 want = (i16)(pPlayer->field_E32 + camAngle);
					i32 delta = ((i32)camAngle - (i32)want) & 0xFFF;

					if (delta < 0x4B0 || delta > 0xB50)
					{
						G_CAMERA_LIST->SetCamAngle(want, 0x3C);
					}
					else if (delta > 0x7F0 && delta < 0x810)
					{
						G_CAMERA_LIST->SetCamAngle(G_CAMERA_LIST->field_236, 0);
					}
					else
					{
						// maps 0x4B0..0xB50 onto a full 0..0x800 sweep
						i32 t = ((delta - 0x4B0) << 11) / 0x6A0;
						i32 pitch = ((i32)Sine(t & 0xFFF) * 128 >> 12) + 0x3C;

						G_CAMERA_LIST->SetCamAngle(want, (u16)pitch);
					}
				}
				else
				{
					G_CAMERA_LIST->SetCamAngle(G_CAMERA_LIST->field_236, 0);
				}
			}
			else
			{
				if (pPad[0x41] != 0)
				{
					pPad[0x41] = 0;
					pPlayer->field_201 = 0;
					pPlayer->PutCameraBehind(0);
					pPlayer->field_56C = 0;
					pPlayer->field_551 = 1;
				}

				if (pPlayer->field_551 == 0)
				{
					pPlayer->PutCameraBehind(0x14);
					pPlayer->field_551 = 1;
				}
			}
		}

		// --- torso aim, 0x4B70D5 ---
		anyMove = (u8)(pPlayer->field_E2E | pPlayer->field_E2D);
		torsoSpeed = 0;
		if (anyMove != 0)
		{
			i16 aim = pPlayer->field_E32;

			if (aim > 0x100 && aim < 0xF00)
			{
				torsoSpeed = ((i32)aim - 0x100) / 512 + 2;
			}
			else
			{
				torsoSpeed = 1;
			}
		}

		camYaw = (G_CAMERA_LIST != 0) ? (i32)G_CAMERA_LIST->field_23A : 0;
		state = pPlayer->field_E1C;

		if ((state & 0x10) != 0)
		{
			if (pPlayer->field_8E8 != 0)
			{
				pPlayer->SetTargetTorsoAngle(pPlayer->field_E32, false);
			}
			else
			{
				pPlayer->SetTargetTorsoAngle((i16)(pPlayer->field_E32 + camYaw),
					pPlayer->field_1AC != 0);
			}
		}
		else if (state == 1)
		{
			if (anyMove == 0)
			{
				pPlayer->LockTargetTorsoAngle();
			}
		}
		else if ((state & 0x4E) != 0)
		{
			if (pPlayer->field_AE6 != 0 || torsoSpeed == 0)
			{
				pPlayer->LockTargetTorsoAngle();
			}
			else
			{
				pPlayer->SetTargetTorsoAngle((i16)(pPlayer->field_E32 + camYaw), false);
			}
		}

		// --- heading and the scripted turn, 0x4B717F ---
		heading = pPlayer->GetEffectiveHeading();
		turnLeft = pPlayer->field_DF8;
		pPlayer->mAngles.vy = heading;
		headingBefore = (i32)heading;
		pPlayer->field_548 = headingBefore;

		if (turnLeft != 0)
		{
			i32 dt = pPlayer->field_80;

			if (turnLeft > dt)
			{
				i16 step = (i16)(pPlayer->field_DF4 * dt);

				pPlayer->mAngles.vy = (i16)(((i32)step + headingBefore) & 0xFFF);
				pPlayer->field_DF8 = turnLeft - dt;
			}
			else
			{
				// 0xDF0: the angle the scripted turn ends on.
				pPlayer->field_DF8 = 0;
				pPlayer->mAngles.vy = PLR_I16(pPlayer, 0xDF0);
			}

			if (pPlayer->field_A8.vy > 0xD48)
			{
				i32 idx = (headingBefore - (i32)pPlayer->mAngles.vy) & 0xFFF;
				CVector dir(Sine(idx), 0, Cosine(idx));

				gte_SetRotMatrix(&pPlayer->mTransform);
				gte_ldlvl(reinterpret_cast<VECTOR*>(&dir));
				gte_rtir();
				gte_stlvnl(reinterpret_cast<VECTOR*>(&pPlayer->field_AC8));
			}
		}

		pPlayer->field_548 = (i32)pPlayer->mAngles.vy - pPlayer->field_548;

		pPlayer->mAcc.vz = 0;
		pPlayer->mAcc.vx = 0;
		if (pPlayer->field_E1C == 0x1000000)
		{
			// dead store, the next test overwrites it either way
			pPlayer->mAcc.vy = 0;
		}
		if (pPlayer->field_AD4 != 0 || pPlayer->mAnim == 0x115)
		{
			pPlayer->mAcc.vy = 0;
		}
		else
		{
			pPlayer->mAcc.vy = 0xA000;
		}

		// --- move input into an acceleration, 0x4B72AD ---
		bAccelSet = 0;
		{
			MATRIX identityA;
			MATRIX identityB;

			if (pPlayer->field_8E8 != 0)
			{
				gte_SetRotMatrix(reinterpret_cast<MATRIX*>(
					reinterpret_cast<char*>(pPlayer) + 0x89C));
			}
			else
			{
				print_if_false(G_CAMERA_LIST != 0, "no camera");
				M3dMaths_SetIdentityRotation(&identityA);
				M3dMaths_SetIdentityRotation(&identityB);
				gte_SetRotMatrix(&identityB);
			}

			accel = (pPlayer->mVel >> 6);
			gte_ldlvl(reinterpret_cast<VECTOR*>(&accel));
			gte_rtir();
			gte_stlvnl(reinterpret_cast<VECTOR*>(&accel));
			accel <<= 6;

			{
				// 0xEF4/0xEF8: the "perpendicularising" run around a boss.
				u8 wasPerpendicularising = pPlayer->field_EF4;
				i32 moveMag = 0;
				i32 moveScale = 0;
				i32 yaw = 0;

				pPlayer->field_EF4 = 0;

				if (pPlayer->field_8EA == 0
					&& pPlayer->field_AE4 != 0
					&& (pPlayer->field_E1C & 0xBFF7F8C1) == 0)
				{
					if ((u8)(pPlayer->field_E2E | pPlayer->field_E2D) != 0)
					{
						i32 sideways = ((i32)pPlayer->field_E2E << 12) / 64;
						i32 forwards = ((i32)pPlayer->field_E2D << 12) / 64;

						if (sideways < 0) sideways = -sideways;
						if (forwards < 0) forwards = -forwards;

						moveMag = sideways + forwards;
						if (moveMag > 0x1000)
						{
							moveMag = 0x1000;
						}

						if (pPlayer->mAnim == 0x32 || pPlayer->mAnim == 0x33
							|| pPlayer->mAnim == 0x34)
						{
							i32 speed = ((moveMag >> 6) << 16) >> 6;

							pPlayer->mAnimSpeed = speed;
							if (speed < 0x8000)
							{
								pPlayer->mAnimSpeed = 0x8000;
							}
						}

						if (pPlayer->field_AD4 != 0)
						{
							moveScale = 0x1A;
						}
						else if (pPlayer->mHeldObject != 0)
						{
							moveScale = ((pPlayer->mHeldObject->field_10C >> 3) & 1) ? 0x0A : 0x18;
						}
						else
						{
							moveScale = 0x28;
							moveMag = 0x1000;
						}

						if (pPlayer->field_8E8 != 0)
						{
							i32 idx;
							i32 c;

							heading = pPlayer->GetEffectiveHeading();
							idx = ((i32)heading - (i32)(u16)pPlayer->field_E32) & 0xFFF;
							c = Cosine(idx);
							if (c < 0) c = -c;
							c = (c * moveMag) >> 12;
							c = c * moveScale;
							c = c * pPlayer->field_EBC;
							accel.vz = -(c / 16);
							bAccelSet = 1;
						}
						else
						{
							heading = pPlayer->GetEffectiveHeading();
							yaw = (i32)heading;

							if (G_CAMERA_LIST->mCameraMode == CAMERAMODE_LOOKAROUND)
							{
								CVector *pAnchor = reinterpret_cast<CVector*>(
									reinterpret_cast<char*>(gBossRelated) + 8);
								i32 dist = Utils_XZDist(&pPlayer->mPos, pAnchor);

								if (dist > 0x300)
								{
									CSVector aim;

									Utils_CalcAim(&aim, &pPlayer->mPos, pAnchor);
									yaw = ((i32)(u16)pPlayer->field_E32 + (i32)aim.vy) & 0xFFF;

									if (pPlayer->field_E32 == 0x400
										|| pPlayer->field_E32 == (i16)0xC00)
									{
										i32 radius = pPlayer->GetPerpendicularisationRadius();
										u8 keepDistance;

										if (dist > radius)
										{
											dist -= pPlayer->field_80;
											keepDistance = 0;
										}
										else
										{
											keepDistance = wasPerpendicularising;
										}

										pPlayer->field_EF4 = 1;
										if (keepDistance == 0)
										{
											pPlayer->field_EF8 = dist;
										}
									}
								}
							}
							else if ((pPlayer->field_E1C & 6) != 0 && pPlayer->field_E8C == 0)
							{
								yaw = ((i32)(u16)pPlayer->field_E32 + camYaw) & 0xFFF;
							}

							if (pPlayer->field_AE5 == 0)
							{
								i32 idx = yaw & 0xFFF;
								i32 ebc = pPlayer->field_EBC;
								i32 c = (((i32)Cosine(idx) * moveMag) >> 12) * ebc * moveScale;
								i32 sn = (((i32)Sine(idx) * moveMag) >> 12) * ebc * moveScale;

								accel.vz = -(c / 16);
								accel.vx = -(sn / 16);
								bAccelSet = 1;
							}
						}
					}
					else if ((pPlayer->field_E1C & 0x40040004) == 0)
					{
						accel.vx = 0;
						accel.vz = 0;
						bAccelSet = 1;
					}
				}
			}

			// --- apply it, 0x4B763C ---
			if (pPlayer->field_8E8 != 0)
			{
				gte_SetRotMatrix(&pPlayer->mTransform);
			}
			else
			{
				gte_SetRotMatrix(&identityA);
			}

			if (bAccelSet != 0)
			{
				pPlayer->mVel = (accel >> 6);
				gte_ldlvl(reinterpret_cast<VECTOR*>(&pPlayer->mVel));
				gte_rtir();
				gte_stlvnl(reinterpret_cast<VECTOR*>(&pPlayer->mVel));
				pPlayer->mVel <<= 6;
			}
		}

		// --- ride the platform under us, 0x4B76C0 ---
		pPlatform = pPlayer->field_DBC;
		if (pPlatform != 0)
		{
			i32 dz = pPlatform->mVel.vz;
			bool moved;

			prevPos = pPlayer->mPos;

			if (PLR_U16(pPlayer, 0xE8E) != 0)
			{
				i32 dx = pPlatform->mVel.vx;

				moved = ((dx | dz) != 0);
				if (moved)
				{
					pPlayer->mPos.vx += dx;
				}
			}
			else
			{
				i32 dy = pPlatform->mVel.vy;
				i32 dx = pPlatform->mVel.vx;

				moved = ((dy | dx | dz) != 0);
				if (moved)
				{
					pPlayer->mPos.vx += dx;
					pPlayer->mPos.vy += dy;
				}
			}

			if (moved)
			{
				pPlayer->mPos.vz += dz;

				lineInfo.MinCoords.vx = 0;
				lineInfo.MinCoords.vy = 0;
				lineInfo.MinCoords.vz = 0;
				lineInfo.MaxCoords.vx = 0;
				lineInfo.MaxCoords.vy = 0;
				lineInfo.MaxCoords.vz = 0;
				lineInfo.Position.vx = 0;
				lineInfo.Position.vy = 0;
				lineInfo.Position.vz = 0;
				lineInfo.Normal.vx = 0;
				lineInfo.Normal.vy = 0;
				lineInfo.Normal.vz = 0;
				lineInfo.StartCoords = prevPos;
				lineInfo.EndCoords = pPlayer->mPos;

				M3dColij_InitLineInfo(&lineInfo);
				M3dZone_LineToItem(&lineInfo, 1);

				if (lineInfo.pItem != 0)
				{
					pPlayer->mPos = prevPos;
				}
			}
		}

		// --- vertical impulse, 0x4B77B8 ---
		if ((pPlayer->mCollision & 0x100) != 0)
		{
			pPlayer->field_E88 = 0;
		}

		riseSpeed = pPlayer->field_E84;
		if (riseSpeed > 0 || pPlayer->field_E88 > 0)
		{
			if (riseSpeed < 0x10000 && pPlayer->field_E88 == 0)
			{
				pPlayer->mVel.vy = (((pPlayer->field_E80 >> 12) * riseSpeed) >> 4) & (i32)0xFFFFF000;
			}
			else
			{
				pPlayer->mVel.vy = pPlayer->field_E80;
			}
		}

		if ((i32)(riseSpeed & (i32)0xFFFF0000) > 0 && pPlayer->field_E1C == 0x200)
		{
			pPlayer->mVel.vy = pPlayer->field_E80;
		}

		pPlayer->ProcessSFXArray();

		if (pPlayer->field_E88 > 0)
		{
			pPlayer->field_E88 -= (pPlayer->field_80 << 16) / 2;
			if (pPlayer->field_E88 < 0)
			{
				pPlayer->field_E88 = 0;
			}
		}

		if (pPlayer->field_E84 > 0)
		{
			pPlayer->field_E84 -= (pPlayer->field_80 << 16) / 2;
			if (pPlayer->field_E84 < 0)
			{
				pPlayer->field_E84 = 0;
			}
		}

		if (pPlayer->field_AD4 != 0 || (pPlayer->field_E1C & 0x3E3FF780) != 0)
		{
			pPlayer->field_5AC = 0;
		}

		// --- ran into a baddy, 0x4B78A8 ---
		if (pPlayer->field_E8 != pPlayer->mPos)
		{
			CVector hitPos;

			if ((pPlayer->field_E1C & 0x1000000) != 0 && pPlayer->field_8D8 == 0)
			{
				CVector lineStart;
				CVector lineEnd;
				CBody *pIgnore = 0;
				CBody *pHit;

				lineStart.vx = pPlayer->field_E8.vx;
				lineStart.vy = pPlayer->mPos.vy;
				lineStart.vz = pPlayer->field_E8.vz;
				lineEnd = pPlayer->mPos + (pPlayer->mPos - pPlayer->field_E8);

				for (;;)
				{
					pHit = M3dColij_LineToSphere(&lineStart, &lineEnd, &hitPos,
						reinterpret_cast<CBody*>(BaddyList), pIgnore, 0x1000);
					if (pHit == 0)
					{
						break;
					}

					{
						i32 kind = (i32)pHit->mType - 304;

						if ((u32)kind > 0x14)
						{
							break;
						}

						// jpt_4B7987: the 21 mTypes 304..324 pick one of three
						// paths through byte_4B8778.
						if (kind == 1 || kind == 11)
						{
							// types 305 and 315: pass straight through, carry
							// on from the hit point ignoring this one
							lineStart = hitPos;
							pIgnore = pHit;
							continue;
						}

						if (kind == 4 || kind == 5 || kind == 7 || kind == 12
							|| kind == 14 || kind == 15 || kind == 17
							|| kind == 18 || kind == 19)
						{
							// types 308, 309, 311, 316, 318, 319, 321..323
							break;
						}
					}

					// everything else: smash into it
					{
						SHitInfo hit;

						hit.field_C.vx = 0;
						hit.field_C.vy = 0;
						hit.field_C.vz = 0;
						hit.field_0 = 0x1E;
						hit.field_8 = (u16)pPlayer->GetDamageInflictedFromDifficulty(0x14);
						hit.field_4 = 2;
						hit.field_C.vx = -pPlayer->field_C6C.vx;
						hit.field_C.vy = 0;
						hit.field_C.vz = -pPlayer->field_C6C.vz;
						hit.field_18 = 0x200;
						hit.field_1A = 0xF;
						VectorNormal(reinterpret_cast<VECTOR*>(&hit.field_C),
							reinterpret_cast<VECTOR*>(&hit.field_C));

						pHit->Hit(&hit);
						SFX_PlayPos(0x10, &pPlayer->mPos, 0);

						// 0x4B8BE0, inlined: the knockback timer pair.
						pPlayer->field_534 = 0x168;
						pPlayer->field_52C = (pPlayer->field_528 + 0xB) << 10;

						pPlayer->mVel.vz = 0;
						pPlayer->mVel.vx = 0;
						pPlayer->field_8D8 = 1;
					}
					break;
				}
			}
			else if ((pPlayer->field_E1C & 0x14) != 0)
			{
				CBody *pHit = M3dColij_LineToSphere(&pPlayer->field_E8, &pPlayer->mPos,
					&hitPos, reinterpret_cast<CBody*>(BaddyList), 0, 0x1000);

				if (pHit != 0)
				{
					pPlayer->CollideWithObject(pHit);
				}
			}
		}

		// --- pick up power-ups, 0x4B7A74 ---
		{
			CBody *pPowerUp = reinterpret_cast<CBody*>(PowerUpList);

			while (pPowerUp != 0)
			{
				if ((pPowerUp->mCBodyFlags & 0x40) == 0
					&& Utils_CrapDist(pPlayer->mPos, pPowerUp->mPos) < (i32)pPowerUp->mRMinor)
				{
					if (pPowerUp->mType == 0xB)
					{
						if ((pPlayer->field_E1C & 0x11) != 0)
						{
							static_cast<CPowerUp*>(pPowerUp)->TakeEffect(pPlayer);
						}
					}
					else if (static_cast<CPowerUp*>(pPowerUp)->TakeEffect(pPlayer) == 0)
					{
						// 0x4B8C00, inlined
						PLR_U8(pPowerUp, 0x124) = 1;

						// 0x370: "the pickup jingle is free to play again".
						if (PLR_U8(pPlayer, 0x370) != 0)
						{
							u16 kind = pPowerUp->mType;

							if (kind == 8 || (kind > 0xD && kind <= 0x10))
							{
								SFX_Play(0x28, 0x2000, 0);
								PLR_U8(pPlayer, 0x370) = 0;
							}
						}
					}
					break;
				}

				pPowerUp = reinterpret_cast<CBody*>(pPowerUp->mNextItem);
			}

			if (pPowerUp == 0)
			{
				PLR_U8(pPlayer, 0x370) = 1;
			}
		}

		// --- push away from baddies while standing, 0x4B7B1E ---
		if ((pPlayer->field_E1C & 1) != 0 && pPlayer->field_E6C == 0)
		{
			CBody *pBaddy = reinterpret_cast<CBody*>(BaddyList);

			while (pBaddy != 0)
			{
				if (pBaddy->mRMinor != 0)
				{
					CVector away = pPlayer->mPos - pBaddy->mPos;
					i32 dist = away.Length();

					if (dist < (((i32)pBaddy->mRMinor * 3) << 10 >> 12))
					{
						i32 strength = 10;

						away.vy = 0;
						away.vx = away.vx / dist;
						away.vz = away.vz / dist;
						pPlayer->mVel += (away * strength);
					}
				}

				pBaddy = reinterpret_cast<CBody*>(pBaddy->mNextItem);
			}
		}

		// --- trigger-zone sweep along the movement, 0x4B7BF0 ---
		{
			// function-local static in the original (guard byte 0x6A7F24,
			// object 0x6A7EF8); the registered atexit handler is a bare retn.
			static CVector sLastMoveDirection;

			CVector moveDir = (pPlayer->mPos - pPlayer->field_E8) >> 12;

			if (pPlayer->field_E1C != 0x80)
			{
				CVector normal;
				CVector back;

				if ((moveDir.vz | moveDir.vy | moveDir.vx) != 0)
				{
					sLastMoveDirection = moveDir;
				}

				VectorNormal(reinterpret_cast<VECTOR*>(&sLastMoveDirection),
					reinterpret_cast<VECTOR*>(&normal));

				back = pPlayer->field_E8 - (normal * 0x10);

				lineInfo.MinCoords.vx = 0;
				lineInfo.MinCoords.vy = 0;
				lineInfo.MinCoords.vz = 0;
				lineInfo.MaxCoords.vx = 0;
				lineInfo.MaxCoords.vy = 0;
				lineInfo.MaxCoords.vz = 0;
				lineInfo.Position.vx = 0;
				lineInfo.Position.vy = 0;
				lineInfo.Position.vz = 0;
				lineInfo.Normal.vx = 0;
				lineInfo.Normal.vy = 0;
				lineInfo.Normal.vz = 0;
				lineInfo.StartCoords = back;
				lineInfo.EndCoords = pPlayer->mPos;

				M3dColij_InitLineInfo(&lineInfo);

				G_TRIGGER_COLLISION_CHECK = 0;
				lineInfo.RecordTriggerZoneHits = 1;
				M3dZone_LineToItem(&lineInfo, 1);
				G_TRIGGER_COLLISION_CHECK = 0;
			}
		}

		// --- build this frame's pose, 0x4B7D5A ---
		if ((pPlayer->mFlags & 4) != 0)
		{
			pPlayer->ApplyPose(gSpideyFixedPose);
		}
		else
		{
			M3d_BuildTransform(pPlayer);
		}

		pPlayer->DoShadowCheck();

		if (pPlayer->field_94D != 0)
		{
			M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pPlayer->field_91C), pPlayer, 6);
			M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pPlayer->field_928), pPlayer, 5);
			M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pPlayer->field_934), pPlayer, 1);
			M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pPlayer->field_940), pPlayer, 0);
		}

		pPlayer->UpdateTrails();

		if (pPlayer->field_584 != 0)
		{
			CVector hookPos;

			M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hookPos), pPlayer, 5);
			pPlayer->field_584->SetPos(hookPos);
		}

		if (pPlayer->field_588 != 0)
		{
			CVector hookPos;

			M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hookPos), pPlayer, 6);
			pPlayer->field_588->SetPos(hookPos);
		}

		// --- head look-at, 0x4B7E32 ---
		// 0xA80: "the head tracks something this tick".
		if (PLR_U8(pPlayer, 0xA80) != 0)
		{
			CVector target;
			bool bHaveTarget = true;

			if (pPlayer->field_E00 != 0)
			{
				Trig_GetPosition(&target, pPlayer->field_E00);
			}
			else if (pPlayer->field_8E8 != 0
				|| pPlayer->field_8E9 != 0
				|| pPlayer->field_DCC == 0
				|| pPlayer->field_8EA != 0)
			{
				bHaveTarget = false;
			}
			else
			{
				target = pPlayer->field_DCC->mPos;
			}

			if (bHaveTarget)
			{
				CVector headHook;
				CVector look;

				M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&headHook), pPlayer, 8);

				look.vx = (target.vx - headHook.vx) >> 12;
				look.vy = (target.vy - headHook.vy) >> 12;
				look.vz = (target.vz - headHook.vz) >> 12;

				gte_SetRotMatrix(reinterpret_cast<MATRIX*>(
					reinterpret_cast<char*>(pPlayer) + 0x89C));
				gte_ldlvl(reinterpret_cast<VECTOR*>(&look));
				gte_rtir();
				gte_stlvnl(reinterpret_cast<VECTOR*>(&look));
				look <<= 12;

				Utils_CalcAim(reinterpret_cast<CSVector*>(
						reinterpret_cast<char*>(pPlayer) + 0xE04),
					&ZeroVector, &look);

				// clamp the head yaw out of the 0x200..0xE00 dead zone
				{
					i16 *pHeadYaw = reinterpret_cast<i16*>(
						reinterpret_cast<char*>(pPlayer) + 0xE04);
					i16 *pHeadPitch = reinterpret_cast<i16*>(
						reinterpret_cast<char*>(pPlayer) + 0xE06);
					i32 yawVal = *pHeadYaw;
					i32 pitchVal = *pHeadPitch;

					if (yawVal > 0x800)
					{
						if (yawVal < 0xE00) *pHeadYaw = 0xE00;
					}
					else if (yawVal > 0x200)
					{
						*pHeadYaw = 0x200;
					}

					if (pitchVal > 0x800)
					{
						if (pitchVal < 0xD00) *pHeadPitch = 0xD00;
					}
					else if (pitchVal > 0x300)
					{
						*pHeadPitch = 0x300;
					}
				}
			}
		}
		else
		{
			PLR_I16(pPlayer, 0xE04) = 0;
			PLR_I16(pPlayer, 0xE06) = 0;
		}

		// --- ease the head joint towards the target angles, 0x4B7FB9 ---
		if (pPlayer->mpJoints != 0)
		{
			i16 *pJointAngles = reinterpret_cast<i16*>(
				reinterpret_cast<char*>(pPlayer->mpJoints) + 0x24);
			i32 axis;

			for (axis = 0; axis < 2; axis++)
			{
				i16 cur = pJointAngles[axis];
				i32 diff = (i32)PLR_I16(pPlayer, 0xE04 + axis * 2) - (i32)cur;

				if (diff > 0x800)
				{
					diff -= 0x1000;
				}
				else if (diff < -0x800)
				{
					diff += 0x1000;
				}

				if (diff != 0)
				{
					if (diff > 0x40)
					{
						diff = 0x40;
					}
					else if (diff < -0x40)
					{
						diff = -0x40;
					}

					pJointAngles[axis] = (i16)(diff + cur);
				}
			}
		}

		if (pPlayer->mHealth < 0)
		{
			pPlayer->mHealth = 0;
		}

		// --- the spidey-sense buzz and the two fists, 0x4B806B ---
		M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hookA), pPlayer, 1);
		M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&hookB), pPlayer, 0);

		{
			CBody *pBuzz = reinterpret_cast<CBody*>(Mem_RecoverPointer(&pPlayer->field_ED4));

			if (pBuzz != 0)
			{
				i32 pulse;

				M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pBuzz->mPos), pPlayer, 8);
				gM3dUtils_GetPartAngles(pPlayer, 7, &pBuzz->mAngles, 0);

				pulse = Sine(((i32)G_TIMER_RELATED << 6) & 0xFFF);
				if (pulse < 0) pulse = -pulse;
				pulse = pulse / 2 + 0x800;

				pBuzz->mScale.vx = (i16)pulse;
				pBuzz->mScale.vy = (i16)pulse;
				pBuzz->mScale.vz = (i16)pulse;

				if (G_PULSATING_HEAD_FLAG != 0)
				{
					pBuzz->mScale.vx = (i16)(pBuzz->mScale.vx << 1);
					pBuzz->mScale.vy = (i16)(pBuzz->mScale.vy << 1);
					pBuzz->mScale.vz = (i16)(pBuzz->mScale.vz << 1);
				}

				SpideyAI0_ApplyCheatScale(pBuzz, 0);
			}
		}

		{
			i32 fist;

			for (fist = 0; fist < 2; fist++)
			{
				CBody *pFist = reinterpret_cast<CBody*>(
					Mem_RecoverPointer(&pPlayer->field_5B8[fist]));

				if (pFist != 0)
				{
					i32 dist;

					M3dUtils_GetHookPosition(reinterpret_cast<VECTOR*>(&pFist->mPos),
						pPlayer, (fist != 0) ? 3 : 4);
					gM3dUtils_GetPartAngles(pPlayer, (fist != 0) ? 5 : 10,
						&pFist->mAngles, 0);

					dist = Utils_CrapDist(pFist->mPos, G_CAMERA_LIST->mPos);
					if (dist < 0x100)
					{
						pFist->mFlags |= 0xC00;
						if (dist < 0xAA)
						{
							pFist->mRGB = 0;
						}
						else
						{
							u32 level = (u32)(dist - 0xAA);

							pFist->mRGB = (((level << 8) | level) << 8) | level;
						}
					}
					else
					{
						pFist->mFlags &= 0xF3FF;
						pFist->mRGB = 0x808080;
					}

					if (pPlayer->field_5E8 != 0)
					{
						u32 blue = pFist->mRGB & 0xFF;

						pFist->mFlags |= 0x400;
						pFist->mRGB = (u32)((Rnd((i32)blue) / 2) << 8) | blue;
					}
				}
			}
		}

		// --- the flaming-fists effect, 0x4B826A ---
		if (pPlayer->field_5E8 != 0 && pPlayer->field_5AC != 0)
		{
			u16 anim = pPlayer->mAnim;
			u32 args[2];
			i32 count;
			i32 i;

			if (anim == 0x66 || anim == 0x68 || anim == 0x6A || anim == 0xD5)
			{
				count = 3;
			}
			else
			{
				count = (Rnd(4) != 0) ? 0 : 1;
			}

			args[0] = (u32)&hookA;
			args[1] = 0x100;
			for (i = 0; i < count; i++)
			{
				Reloc_CallUserFunction(gFistEffectNameLeft, 5, args, 0);
			}

			if (anim == 0x64 || anim == 0x6A || anim == 0xD5)
			{
				count = 3;
			}
			else
			{
				count = (Rnd(4) != 0) ? 0 : 1;
			}

			args[0] = (u32)&hookB;
			for (i = 0; i < count; i++)
			{
				Reloc_CallUserFunction(gFistEffectNameRight, 5, args, 0);
			}
		}

		// --- swing line, web and held object render ends, 0x4B8346 ---
		if (pPlayer->field_E64 != 0)
		{
			if (pPlayer->mAnim == 0x118)
			{
				hookA -= (pPlayer->field_C6C * 8);
				gCSwinger_SetRenderEnd(pPlayer->field_E64, hookA);
			}
			else
			{
				hookB -= (pPlayer->field_C6C * 8);
				gCSwinger_SetRenderEnd(pPlayer->field_E64, hookB);
			}
		}

		if (pPlayer->field_E6C != 0)
		{
			CWeb *pWeb = reinterpret_cast<CWeb*>(pPlayer->field_E6C);
			CVector firePos;

			if (pWeb->field_102 == 0)
			{
				firePos = hookB;
			}
			else if (pWeb->field_102 == 1)
			{
				firePos = hookA;
			}
			else
			{
				firePos = hookA + ((hookB - hookA) / 2);
			}

			gCWeb_SetFirePos(pWeb, firePos);
		}

		if (pPlayer->mHeldObject != 0)
		{
			CManipOb *pHeld = pPlayer->mHeldObject;
			CVector mid = hookA + ((hookB - hookA) / 2);
			CVector dir = (mid - pPlayer->mPos) >> 6;
			i32 reach;

			VectorNormal(reinterpret_cast<VECTOR*>(&dir),
				reinterpret_cast<VECTOR*>(&dir));

			reach = (i32)pHeld->field_108;
			pHeld->mPos = (mid + (dir * reach)) + (pPlayer->field_C6C * 0x20);
			pHeld->mAngles.vy = (i16)(pHeld->mAngles.vy + (i16)pPlayer->field_548);
		}

		// --- keep the fists model in step with the animation, 0x4B8638 ---
		// 0xAB4: the animation id the current fists model was built for.
		if (pPlayer->mAnim != (u16)PLR_U8(pPlayer, 0xAB4))
		{
			// gFistsData[anim] >> 14 is the fists variant, see
			// CPlayer::SortFistsData in spidey.cpp.
			u16 *pFistsData = reinterpret_cast<u16*>(0x00555A14);

			pPlayer->CreateFists((u8)(pFistsData[pPlayer->mAnim] >> 14));
			PLR_U8(pPlayer, 0xAB4) = (u8)pPlayer->mAnim;
		}

		pPlayer->SetupLookaroundCamera();
		pPlayer->DrawOffscreenSpideySenseIndicatorList();

		// clear the pad's per-frame edge flags, skipping entries 14 and 15
		{
			i32 i;

			for (i = 0; i < 20; i++)
			{
				if (i != 14 && i != 15)
				{
					pPad[i * 16 + 1] = 0;
					PLR_U8(pPlayer, 0x1C1 + i * 16) = 0;
				}
			}
		}
	}
}
