#include "game/objects/turret.h"
#include "compatibility/my_patch.h"
#include "engine/rendering/m3dutils.h"
#include "compatibility/validate.h"

// @Ok
// Functional: turret constructor, logic verified against Hex-Rays.
// 2 mnemonic diffs are pure scheduling swap (instruction count matches).
CTurret::CTurret(i16 *a2, i32 a3)
{
	this->field_35C = 0;
	this->field_35E = 0;
	this->field_360 = 0;
	this->field_362 = 0;
	this->field_364 = 0;
	this->field_366 = 0;
	this->field_368 = 0;
	this->field_36A = 0;
	this->field_36C = 0;

	i16 *pCursor = this->SquirtAngles(this->SquirtPos(a2));

	this->mNode = static_cast<u16>(a3);
	this->field_24C = pCursor;
	this->field_20C = 1;

	this->InitItem("turret");

	M3dUtils_ReadHooksPacket(this, reinterpret_cast<void*>(0x55984C));

	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->mType = 325;
	this->mRMinor = 0x80;
	this->mHealth = 10;
	this->field_2A8 |= 0x10200;

	this->RunAnim(-1, 0, 0);
}

// @Ok
// @Matching
void Turret_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Turret_RelocatableModuleClear;
	pMod->field_C[0] = Turret_CreateTurret;
}

// @Ok
// @Matching
void Turret_CreateTurret(const u32 *stack, u32 * result)
{
	i16 *v2 = reinterpret_cast<i16*>(stack[0]);
	i32 v3 = stack[1];

	*result = reinterpret_cast<u32>(new CTurret(v2, v3));
}

// @Ok
// @Matching
void Turret_RelocatableModuleClear(void)
{
	CItem *pSearch = G_BADDY_LIST;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 325)
			delete pSearch;

		pSearch = pNext;
	}
}

// @Ok
void CTurret::TargetLockAbsolute(const CVector &a1){
	this->field_344 = a1;
	this->field_218 |= 4;
}

// @Ok
void CTurret::TargetLockDynamic(CBody *a2)
{
	if ( !a2->IsDead() )
	{
		this->field_378 = a2;
		this->field_218 |= 0x10u;
	}
}

// @Ok
void CTurretLaser::SetDamage(int damage)
{
	this->field_4C = damage;
}

// @Ok
void CTurret::ClearTargetLock(void)
{
	this->field_218 &= 0xFFFFFFE3;
}

void validate_CTurret(void)
{
	VALIDATE_SIZE(CTurret, 0x37C);

	VALIDATE(CTurret, field_324, 0x324);
	VALIDATE(CTurret, field_344, 0x344);
	VALIDATE(CTurret, field_378, 0x378);
}

void validate_CTurretBase(void)
{
		VALIDATE_SIZE(CTurretBase, 0xFC);
}

void validate_CTurretLaser(void)
{
		VALIDATE_SIZE(CTurretLaser, 0x64);

		VALIDATE(CTurretLaser, field_4C, 0x4C);

}

// @Bogus
// Big gap here, found while mapping addresses, not fixed this session:
// CTurret's vtable (0x53C648) has FOUR real overrides beyond CBaddy's
// defaults that our header does not declare at all: dtor (0x4E48E0), AI
// (0x4E49F0, unnamed in names.json), Hit (0x4E4C70, names.json:
// ".Hit__7CTurretFR8SHitInfo") and DeleteStuff (0x4E49A0, unnamed). There
// is also a real CTurret_ProcessPattern (0x4E5200) not implemented
// anywhere, and CTurretBase (our repo's version is just an 0x8-byte
// padding stub) has its own real AI override
// (".AI__11CTurretBaseFv", 0x4E4650), and CTurretLaser has a real
// constructor (CTurretLaser_ctor, 0x4E3BD0) we never implemented either
// (only SetDamage exists here, and it has no standalone address in
// names.json, presumably inlined into that missing constructor).
// Building a CTurret with our vtable would silently drop all four
// overrides, so the constructor and Turret_CreateTurret (it does
// `new CTurret(...)`) stay in the exe.
//
// TargetLockAbsolute, TargetLockDynamic and ClearTargetLock have no
// standalone address either, inlined at their call sites in the original
// (those call sites are not in this file, so unverified which callers).
void patch_turret(void)
{
	PATCH_PUSH_RET(0x004E3B00, Turret_RelocatableModuleInit);
	PATCH_PUSH_RET(0x004E3B20, Turret_RelocatableModuleClear);
}

