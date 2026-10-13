#include "game/actors/torch.h"
#include "compatibility/my_patch.h"
#include "compatibility/export.h"
#include "compatibility/validate.h"
#include "engine/world/trig.h"
#include "core/game_heap.h"

EXPORT SLight M3d_TorchLight =
{
  { { -2430, -2228, -2430 }, { 2509, -2896, 1447 }, { -648, -3711, -1607 } },
  0,
  { { 3200, 1040, 2048 }, { 2720, 1600, 1920 }, { 2400, 2560, 2048 } },
  0,
  { 1200, 1200, 960 }
};


// @Ok
// @Matching
void Torch_RelocatableModuleClear(void)
{
	CItem *pSearch = G_BADDY_LIST;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 328)
			delete pSearch;

		pSearch = pNext;
	}
}

// @Ok
// @Matching
void Torch_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Torch_RelocatableModuleClear;
	pMod->field_C[0] = Torch_CreateTorch;
}

// @Ok
 i32* CTorch::GetNewCommandBlock(u32 a1)
{
	i32* res = static_cast<i32*>(DCMem_New(4 * a1, 0, 1, 0, 1));
	res[a1 - 1] = 0;

	if (!this->field_350)
	{
		this->field_350 = res;
	}
	else
	{
		i32* it = this->field_350;
		while (1)
		{
			if (!it[it[1] - 1])
				break;

			it = reinterpret_cast<i32*>(it[it[1] - 1]);
		}

		it[it[1] - 1] = reinterpret_cast<i32>(res);
	}

	return res;
}

// @Ok
// @Matching
CTorch::~CTorch(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
	delete this->gTorchQuadBit;
	this->KillAllCommandBlocks();
}

// @Ok
// Verified field by field against 0x4DC4F0: field_21E=100 (i16 store),
// field_330/334=2, RunAnim(4,0,-1), mFlags|=0x480, mpLight before
// AttachTo, mType=328 (0x148) then field_31C.bothFlags=1, mNode=a3,
// mRMinor=0, field_34C=SquirtAngles result, Die(0) guarded by
// G_SUBMARINER_DIE_RELATED (shared global at 0x60CFC4) and level id != 2051
// (0x803). All offsets and order match the original exactly.
CTorch::CTorch(i16* a2, i32 a3)
{
	this->InitItem("torch");
	i16 *v5 = this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2)));

	this->field_21E = 100;
	this->field_330 = 2;
	this->field_334 = 2;

	this->RunAnim(4, 0, -1);
	this->mFlags |= 0x480;

	this->mpLight = &M3d_TorchLight;

	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->mType = 328;
	this->field_31C.bothFlags = 1;

	this->mNode = a3;
	this->mRMinor = 0;
	this->field_34C = reinterpret_cast<i32>(v5);

	if (G_SUBMARINER_DIE_RELATED && Trig_GetLevelID() != 2051)
		this->Die(0);
}

// @Ok
void Torch_CreateTorch(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(*stack);
	i32 v3 = static_cast<i32>(stack[1]);

	*result = reinterpret_cast<u32>(new CTorch(v2, v3));
}

// @Ok
void CTorch::Shouldnt_DoPhysics_Be_Virtual(void)
{
	this->DoPhysics();
}

// @Ok
void CTorch::DoPhysics(void)
{
	this->mAcc.vy = this->field_32C - (this->mVel.vy / 16);
	this->mAcc.vx = 0;
	this->mAcc.vz = 0;

	CVector accStep = this->mAcc * 12;
	accStep >>= this->field_338;
	this->mVel += accStep;

	i32 dtSq = (this->field_338 * this->field_338) >> 12;
	CVector accTerm = (this->mAcc * dtSq) / 12;
	accTerm >>= this->field_338;

	CVector velTerm = this->mVel * 12;
	velTerm >>= this->field_338;

	CVector posStep = accTerm + velTerm;
	this->mPos += posStep;

	this->mAngles.vy += static_cast<i16>((this->mAngVel.vy * this->field_338) >> 12);
	this->mAngles.Mask();

	this->mAngVel.vy += static_cast<i16>((this->mAngAcc.vy * this->field_338) >> 12);

	this->mAngVel.vx -= this->mAngVel.vx >> this->mAngFric.vx;
	this->mAngVel.vy -= this->mAngVel.vy >> this->mAngFric.vy;
	this->mAngVel.vz -= this->mAngVel.vz >> this->mAngFric.vz;

	this->mAngVel.KillSmall();
}

// @Ok
// @Matching
 i32* CTorch::KillCommandBlock(i32* a1)
{
	i32* res = reinterpret_cast<i32*>(a1[a1[1]-1]);

	if (this->field_350 == a1)
	{
		this->field_350 = res;
	}
	else
	{
		i32* it = this->field_350;

		while (it)
		{
			if (a1 == reinterpret_cast<i32*>(it[it[1]-1]))
			{
				it[it[1]-1] = reinterpret_cast<i32>(res);
				break;
			}

			it = reinterpret_cast<i32*>(it[it[1]-1]);
		}
	}

	Mem_Delete(reinterpret_cast<void*>(a1));
	return res;
}

// @Ok
// @Matching
 void CTorch::KillAllCommandBlocks(void)
{
	for (i32* cur = this->field_350; cur; cur = this->KillCommandBlock(cur))
		;
	this->field_350 = 0;
}

void validate_CTorch(void){
	VALIDATE_SIZE(CTorch, 0x354);

	VALIDATE(CTorch, field_324, 0x324);
	VALIDATE(CTorch, field_328, 0x328);
	VALIDATE(CTorch, field_32C, 0x32C);
	VALIDATE(CTorch, field_330, 0x330);
	VALIDATE(CTorch, field_334, 0x334);
	VALIDATE(CTorch, field_338, 0x338);


	VALIDATE(CTorch, gTorchQuadBit, 0x33C);

	VALIDATE(CTorch, field_340, 0x340);
	VALIDATE(CTorch, field_344, 0x344);
	VALIDATE(CTorch, field_348, 0x348);

	VALIDATE(CTorch, field_34C, 0x34C);
	VALIDATE(CTorch, field_350, 0x350);
}

// @Bogus
// Big gap found here, not something this session can fix: CTorch's vtable
// (0x53C5BC) has slot 2 (AI) pointing at a real 1191 byte function at
// 0x4DC6B0 named "CTorch::AI" in names.json, and there is a matching
// "CTorch::SynthesizeAnalogueInput" (0x4DCB60, 2345 bytes) and
// "CTorch::DoMGSShadow" (0x4DD760, 842 bytes). None of the three exist
// anywhere in our torch.h/torch.cpp. The original CTorch looks like it has
// a full CSpClone-style walk-script AI (same slot 11
// Shouldnt_DoPhysics_Be_Virtual-jumps-to-DoPhysics shape as CSpClone too),
// our repo only ever implemented the physics/cleanup half. Per the
// constructor rule: since our class does not declare AI, stamping our
// vtable via the constructor would silently fall back to CBaddy's default
// AI and drop the torch's real behavior, so the constructor, destructor
// and Torch_CreateTorch (it does `new CTorch(...)`) all stay in the exe.
// Shouldnt_DoPhysics_Be_Virtual and DoPhysics are still safe to hook on
// their own addresses: dispatch to them goes through the exe's own
// (untouched) vtable regardless of who built the object.
// GetNewCommandBlock, KillCommandBlock and KillAllCommandBlocks have no
// standalone address, same inlined-at-call-site shape as CSpClone/
// CBlackCat's equivalents.
void patch_torch(void)
{
	PATCH_PUSH_RET(0x004DC400, Torch_RelocatableModuleInit);
	PATCH_PUSH_RET(0x004DC420, Torch_RelocatableModuleClear);

	PATCH_PUSH_RET_POLY(0x004DD5E0, CTorch::Shouldnt_DoPhysics_Be_Virtual, "?Shouldnt_DoPhysics_Be_Virtual@CTorch@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x004DD5F0, CTorch::DoPhysics, "?DoPhysics@CTorch@@QAEXXZ");
}
