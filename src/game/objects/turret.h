#pragma once

#ifndef TURRET_H
#define TURRET_H

#include "compatibility/export.h"
#include "game/actors/baddy.h"

#include "engine/assets/reloc.h"


class CTurret : public CBaddy
{
	public:
		EXPORT CTurret(i16 *,i32);

		EXPORT void TargetLockAbsolute(const CVector &);
		EXPORT void TargetLockDynamic(CBody *a2);
		EXPORT void ClearTargetLock(void);

		CVector field_324;
		CVector field_330;

		PADDING(0x344 - 0x33C);

		CVector field_344;
		CVector field_350;

		u16 field_35C;
		u16 field_35E;
		u16 field_360;
		u16 field_362;
		u16 field_364;
		u16 field_366;
		u16 field_368;
		u16 field_36A;
		u16 field_36C;

		PADDING(0x378 - 0x36E);

		CBody *field_378;
};

class CTurretBase : public CBody {
public:
	PADDING(0x8);
};

class CTurretLaser : public CNonRenderedBit
{
	public:
		EXPORT void SetDamage(int);

		PADDING(0x11-4);

		i32 field_4C;

		PADDING(0x64-0x4C-4);
};

void validate_CTurret(void);
void validate_CTurretBase(void);
void validate_CTurretLaser(void);

EXPORT void Turret_CreateTurret(const u32 *,u32 *);
EXPORT void Turret_RelocatableModuleClear(void);
EXPORT void Turret_RelocatableModuleInit(reloc_mod*);
void patch_turret(void);

#endif
