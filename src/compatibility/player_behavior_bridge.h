#pragma once

// Original fixed-address services and ABI adapters shared by behavior phases.
// Pointer constants refer to the same game storage in every translation unit.
#ifdef SPIDEY_STANDALONE
#include <new>
#endif
#include "game/player/spidey.h"

#include "engine/world/bit.h"
#include "game/actors/baddy.h"
#include "game/cinematics/camera.h"
#include "engine/rendering/render_buffers.h"
#include "game/effects/effects.h"
#include "game/effects/flash.h"
#include "engine/world/collision.h"
#include "engine/rendering/m3dutils.h"
#include "engine/world/m3dzone.h"
#include "game/objects/powerup.h"
#include "engine/assets/reloc.h"
#include "engine/assets/spool.h"
#include "game/objects/switch.h"
#include "engine/rendering/scene_renderer.h"
#include "core/game_heap.h"
#include "engine/world/ob.h"
#include "game/objects/manipob.h"
#include "engine/input/PCInput.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/input/ps2pad.h"
#include "engine/audio/ps2redbook.h"
#include "game/application/tweak.h"
#include "engine/rendering/ps2funcs.h"
#include "engine/world/trig.h"
#include "core/utils.h"
#include "core/fixed_vector.h"
#include "game/player/web.h"

// Untyped player fields still awaiting verified names. These accesses retain
// original record offsets; move them into CPlayer as their contracts are proven.
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

// Original Redbook playback state shared with the player and shell.
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

// Sorted {animation, follow-on animation} pairs, initialized by CPlayer.
static u16 * const gAnimFollowOnData = reinterpret_cast<u16*>(0x00555C6C);


inline void gM3dUtils_GetPartAngles(CSuper *pSuper, i32 part, CSVector *pAngles, i32 a4)
{
#ifdef SPIDEY_STANDALONE
	M3dUtils_GetPartAngles(pSuper, part, pAngles, reinterpret_cast<CSVector*>(a4));
#else
	typedef void (*func_ptr)(CSuper*, i32, CSVector*, i32);
	func_ptr func = (func_ptr)0x00454040;
	func(pSuper, part, pAngles, a4);
#endif
}

// Original Windows member-call adapters. Standalone builds call the recovered
// methods directly; compatibility builds retain the executable entry points.
struct SSwingerRenderEndAdapter
{
	void SetRenderEnd(CVector &pos);
};

struct SWebFirePosAdapter
{
	void SetFirePos(CVector &pos);
};

inline void gCSwinger_SetRenderEnd(void *pSwinger, CVector &pos)
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
inline void gCWeb_SetFirePos(CWeb *pWeb, CVector &pos)
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

// CWeb::SwitchToSnap at 0x004F69F0.
struct SWebSwitchToSnapAdapter
{
	void SwitchToSnap(CVector &dir, CVector *pPath);
};

inline void gCWeb_SwitchToSnap(CWeb *pWeb, CVector &dir, CVector *pPath)
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

inline i32 gCPlayer_UpdateAndTrackCombo(CPlayer *pPlayer)
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

// CSwinger construction at 0x004F6F00.
struct SSwingerCtorAdapter
{
	void Construct(CVector *pAnchor, i32 length, CSVector *pAngles, CVector *pEnd);
};

inline void gCSwinger_ctor(void *pSwinger, CVector *pAnchor, i32 length,
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
inline void SpideyAI0_ApplyCheatScale(CBody *pBody, i32 stickmanHalves)
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
