#pragma once


#ifndef BULLET_H
#define BULLET_H

#include "compatibility/export.h"
#include "engine/world/ob.h"
#include "game/player/weapons.h"

EXPORT extern CBody* BulletList;

// The exe owns this list head. SpideyMain runs Ob_AI(&BulletList) and
// M3d_Render(BulletList) on the exe's copy every frame and neither is hooked,
// so anything we attach to our own copy would never move or draw. Address read
// out of the original: C3DExplosion's constructor pushes 56EFE4h into
// CBody::AttachTo at 0x0043CC13, its destructor pushes the same into
// CBody::DeleteFrom at 0x0043CD53. Confirmed as BulletList in idb_globals.txt.
//#define G_BULLET_LIST (BulletList)
#define G_BULLET_LIST (*reinterpret_cast<CBody**>(0x0056EFE4))
enum HitId
{
	ALWAYS_TWENTY_NINE = 29,
};

class CBullet : public CBody
{
	public:
		EXPORT CBullet(void);
		EXPORT virtual ~CBullet(void);

		EXPORT void BlowUp(void);
		EXPORT void GiveScaledDamageToEnviro(i32);
		EXPORT void GiveScaledDamageToObjects(CBody *,i32,i32,i32,HitId);

		PADDING(0x100-0xF4);

		// Guess: doubles as a "target list" tag. CBullet::BlowUp
		// compares it for identity against &MechList (the player
		// global, spidey.h) to pick between a "hit the player"
		// explosive (smoke puff + splash to MechList) and a plain
		// CFireyExplosion.
		void* field_100;

		u16 field_104;

		// Guess: blast radius used by BlowUp's MechList splash
		// damage falloff (field_104 * dist / field_106).
		u16 field_106;

		HitId field_108;

		void* field_10C;

		PADDING(4);

		i32 field_114;
		i32 field_118;
		i32 field_11C;
		void *field_120;

		// Guess: gates whether BlowUp notifies an env obj at all.
		i32 field_124;

		// Guess: env obj this bullet is tied to (CItem*), used both
		// directly and re-looked-up via field_140's handle.
		void* field_128;

		// Guess: extra u32* passed through to Exp_HitEnvItem.
		void* field_12C;

		i32 field_130;
		i32 field_134;
		i32 field_138;

		// Guess: selects between the field_128 direct path and the
		// field_140 handle re-lookup path in BlowUp.
		u8 field_13C;

		PADDING(0x140-0x13C-1);

		// Handle used to re-recover field_128's pointer via
		// Mem_RecoverPointer (mem.h).
		SHandle field_140;
};

class CSmokePuff : public CSmokeRing
{
	public:
		EXPORT CSmokePuff(CVector*);
		EXPORT virtual ~CSmokePuff(void) OVERRIDE;

		EXPORT virtual void Move(void) OVERRIDE;
};

void validate_CSmokePuff(void);
void validate_CBullet(void);
#endif
