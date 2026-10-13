#pragma once

#ifndef VENOM_H
#define VENOM_H

#include "compatibility/export.h"
#include "game/actors/baddy.h"
#include "engine/assets/reloc.h"

class CVenomHitSpark : public CQuadBit
{
	public:
		EXPORT CVenomHitSpark(const CVector *);
		EXPORT virtual ~CVenomHitSpark(void) OVERRIDE;

		EXPORT virtual void Move(void) OVERRIDE;
};

class CVenom : public CBaddy 
{

	public:

		EXPORT CVenom(i32*, i32);
		EXPORT virtual ~CVenom(void) OVERRIDE;

		EXPORT void Shouldnt_DoPhysics_Be_Virtual(void);
		EXPORT void DoPhysics(void);

		EXPORT i32* KillCommandBlock(i32*);
		EXPORT void KillAllCommandBlocks(void);
		EXPORT i32* GetNewCommandBlock(u32);

		EXPORT u8 TugImpulse(CVector *, CVector *, CVector *);
		EXPORT void PlayNextFootstepSFX(void);
		EXPORT i32 GetTargetPosFromNode(CVector *, i32);
		EXPORT void Lookaround(void);
		EXPORT void TugWeb(void);
		EXPORT void AdjustWaterModel(void);
		EXPORT void PulseL6A4Node(bool);
		EXPORT void VenomDie(void);
		EXPORT void ResolveSwitchNodes(void);

		EXPORT virtual void CreateCombatImpactEffect(CVector *,i32) OVERRIDE;

		EXPORT virtual void EnterWaitState(void);
		EXPORT virtual void ExitWaitState(u32, u32);

		PADDING(4);

		// Chase-bar animation frame index (0-5), selects one of the six
		// "VenomChase_Bar_0X" textures. Read (never written) by
		// Venom_DisplayProgressBar (0x4E7E10: *(_DWORD*)(venom+0x328) used
		// to index the loaded texture array). Nothing in the decompiled
		// code so far writes it; presumably some other, not yet
		// decompiled, per-frame update advances it using field_32C below.
		i32 field_328;

		// Chase-bar animation time accumulator. Written by
		// Venom_DisplayProgressBar (0x4E7E10: venom->field_32C +=
		// venom->field_80, gated on gPostWaterEffect == 0); field_80 is
		// CBody's existing per-frame delta field. Never read back in that
		// function, so its consumer (presumably whatever advances
		// field_328) is elsewhere, not yet decompiled.
		i32 field_32C;

		i32 field_330;

		// @FIXME
		CClass *field_334;

		i32 field_338;
		u8 field_33C;
		u8 field_33D;

		SHandle field_340;
		i32 field_348;

		PADDING(1);

		u8 field_34D;

		PADDING(0x358 - 0x34D - 1);

		i32 field_358;
		i32* field_35C;

		PADDING(0x37C-0x35C-4);

		i32 field_37C;
		i32 field_380;
		i32 field_384;

		// read (!= 0 check) by Panel_DisplayHealthBar (panel.cpp, offset 0x388
		// relative to a CBody* boss pointer) to pick between the default and
		// an alternate health-bar icon texture; meaning otherwise unknown.
		i32 field_388;

		PADDING(4);

		// @FIXME
		void *field_390;

		PADDING(4);

		// @FIXME
		CClass *field_398;

		i32 field_39C;
		i32 field_3A0;
		i32 field_3A4;
		i32 field_3A8;
		
		PADDING(0x3B0-0x3A8-4);

		i32 field_3B0;
		i32 field_3B4;

		i32 field_3B8;
		i32 field_3BC[4];
		u32 field_3CC;
		CItem *field_3D0[5];

		// @FIXME
		CClass *field_3E4;

		i32 field_3E8;
		i32 field_3EC;
		i32 field_3F0;

		PADDING(0xC);

		i32 field_400;
		i32 field_404;
		i32 field_408;
		i32 field_40C;
		i32 field_410;
		i32 field_414;
		i32 field_418;
		i32 field_41C;
		i32 field_420;

		PADDING(4);

		// @FIXME
		CClass *field_428;

		PADDING(4);

		i32 field_430;
		CQuadBit *field_434;

		PADDING(0x20-4);


		i32 field_454;
		i32 field_458;
		i32 field_45C;
		i32 field_460;
		i32 field_464;
		
		// @TODO venom has two secret virtual fucnctions
};

class CVenomWrap : public CNonRenderedBit
{
	public:
		EXPORT void Die(void);
		PADDING(0x20);
};

class CVenomElectrified : public CNonRenderedBit
{
	public:
		EXPORT CVenomElectrified(CSuper*);
		SHandle field_3C;

		PADDING(4);
};

void validate_CVenom(void);
void validate_CVenomWrap(void);
void validate_CVenomElectrified(void);
void validate_CVenomHitSpark(void);

EXPORT void Venom_CreateVenom(const u32 *stack, u32 *result);
EXPORT void Venom_DisplayProgressBar(const u32*, u32*);

EXPORT void Venom_RelocatableModuleInit(reloc_mod *);
EXPORT void Venom_RelocatableModuleClear(void);
void patch_venom(void);



#endif
