#pragma once

#ifndef POWERUP_H
#define POWERUP_H

#include "engine/world/ob.h"
#include "compatibility/export.h"

// volatile to match the one definition in bit.cpp. Without it MSVC mangled
// this as a different symbol (?TTime@@3HA vs ?TTime@@3HC) and powerup.cpp
// ended up with its own second, never incremented TTime.
EXPORT extern volatile i32 TTime;
// bit.cpp also defines TTime (duplicate storage, not touched here) and had
// this macro, but only inside bit.cpp itself where nothing outside that TU
// could see it. This file already hosts the extern every other file
// (front.cpp, shell.cpp, platform.cpp, main.cpp, init.cpp) declares TTime
// through, so the macro belongs here. Address from bit.cpp's comment,
// matches the maintainer's IDB (idb_globals.txt: 0x0060CFA8 TTime).
//#define G_TTIME (TTime)
#define G_TTIME (*reinterpret_cast<volatile i32*>(0x0060CFA8))

class CPlayer;

class CPowerUp : public CBody
{
	public:
		EXPORT virtual ~CPowerUp(void) OVERRIDE;
		EXPORT CPowerUp(u16, CVector*, CVector*, u32, i32, i32);
		EXPORT void SetGravity(i32, i32);
		EXPORT void SetNode(i32);
		EXPORT void CreateBit(void);
		EXPORT void CheckAge(void);
		EXPORT void DoPhysics(void);
		EXPORT u8 TakeEffect(CPlayer*);

		EXPORT virtual void Die(void);
		EXPORT virtual void AI(void);
		EXPORT virtual void DeleteStuff(void);

		PADDING(4);

		CGlow *mpGlow;

		PADDING(4);

		u8 mHasNode;
		u8 mIs3d;
		u8 mDropping;

		// guess: DoPhysics falling/landing state. field_103 = currently
		// falling (drives the whole physics branch); field_104 = ground
		// check already done this fall; field_105 = landing threshold
		// multiplier (shifted by 12 against field_10C).
		u8 field_103;
		u8 field_104;
		u8 field_105;

		u16 mNodeIndex;
		u16 field_108;

		PADDING(0x10C-0x108-2);

		i32 field_10C;

		// guess: DoPhysics working position, integrated from mVel each
		// frame and copied back into mPos at the end of DoPhysics.
		CVector field_110;

		PADDING(2);


		u16 field_11E;
		u16 field_120;

		// guess: random model variant offset, set in the constructor from
		// Rnd(20) + 90. Only ever written there, no other reader found.
		i16 field_122;

		u8 field_124;

		PADDING(0x128-0x124-1);

		i32 field_128;
		u16 mLifetime;

		PADDING(2);

		CFlatBit* pPickupBit;

		// G_TIMER_RELATED value of the last "already full" XA line TakeEffect
		// played for this pickup, it waits 300 ticks between two lines
		i32 mLastXATime;
};

EXPORT extern CBody* PowerUpList;
// Address from the maintainer's IDB (idb_globals.txt: 0x0060FB94
// PowerUpList). CPowerUp objects are spawned from trig.cpp's level loader
// (unhooked), and walked by main.cpp's Logic (decompiled but not hooked
// over the real game loop yet). Either side can be the one still running,
// so a hooked constructor/destructor needs game memory here.
//#define G_POWER_UP_LIST (PowerUpList)
#define G_POWER_UP_LIST (*reinterpret_cast<CBody**>(0x0060FB94))
void validate_CPowerUp(void);
void patch_powerup(void);

#ifdef SPIDEY_STANDALONE
// 0x46BD80 (names.json: PowerUp_Create). The body is trig.cpp's file-local
// Trig_CreatePowerUp; this is the externally callable name the standalone
// build uses from CBaddy::ExecuteCommand (opcode 0x42A0) instead of the
// forward-to-original pointer call. Standalone only so the normal build's
// trig.cpp codegen does not change.
EXPORT CBody* PowerUp_Create(i32, CVector*, i32, i32, i32);
#endif
#endif
