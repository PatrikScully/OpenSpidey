#pragma once

#include "compatibility/export.h"
#include "game/actors/baddy.h"

class CPlatform : public CBaddy
{
	public:
		EXPORT void AI(void);
		EXPORT void AdjustBruceHealth(void);
		EXPORT CPlatform(i16 *,i32);
		EXPORT i32 ExecuteCommand(u16) OVERRIDE;
		EXPORT i32 Hit(SHitInfo *) OVERRIDE;
		EXPORT void MoveTo(CVector *);
		EXPORT void NotifyTrodUpon(CBody *,CVector const *,CSVector const *);
		EXPORT void SetVariable(u16);
		EXPORT virtual ~CPlatform(void) OVERRIDE;

		EXPORT void Shouldnt_DoPhysics_Be_Virtual(void) OVERRIDE;
		EXPORT void DoPhysics(void);

		EXPORT i16 GetVariable(u16);

		u16 field_324;
		u16 field_326;
		u16 field_328;

		i16 field_32A;
		i16 field_32C;
		i16 field_32E;

		i16 field_330;
		i32 field_334;

		u32 field_338;

		u32 field_33C;
		i32 field_340;

		CVector field_344;
		CVector field_350;
};

void validate_CPlatform(void);
