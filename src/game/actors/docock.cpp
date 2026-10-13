#include "game/actors/docock.h"
#include "compatibility/my_patch.h"
#include "compatibility/validate.h"
#include "engine/world/trig.h"
#include "game/ui/panel.h"
#include "engine/rendering/scene_renderer.h"
#include "game/player/web.h"
#include "core/game_heap.h"
#include "core/utils.h"
#include "game/player/spidey.h"
#include "game/ui/panel.h"

#include <cstring>

EXPORT SLight M3d_DocOckLight =
{
  { { -2430, -2228, -2430 }, { 2509, -2896, 1447 }, { -648, -3711, -1607 } },

  0,
  { { 3200, 1040, 2048 }, { 2720, 1600, 1920 }, { 2400, 2560, 2048 } },
  0,

  { 1200, 1200, 960 }
};


// @Ok
// @Matching
void DocOck_RelocatableModuleClear(void)
{
	CItem *pSearch = G_BADDY_LIST;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 308)
			delete pSearch;

		pSearch = pNext;
	}
}

// @Ok
// @Matching
void DocOck_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = DocOck_RelocatableModuleClear;
	pMod->field_C[0] = DocOck_CreateDocOck;
}

// @Ok
 i32* CDocOc::GetNewCommandBlock(u32 a1)
{
	i32* res = static_cast<i32*>(DCMem_New(4 * a1, 0, 1, 0, 1));
	res[a1 - 1] = 0;

	if (!this->field_4AC)
	{
		this->field_4AC = res;
	}
	else
	{
		i32* it = this->field_4AC;
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
CDocOc::~CDocOc(void)
{
	gBossRelated = 0;
	Panel_DestroyHealthBar();
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
	this->KillAllCommandBlocks();

	for (i32 i = 0; i<4; i++)
	{
		if (this->field_530[i])
			Mem_Delete(this->field_530[i]);

		delete reinterpret_cast<CClass*>(this->field_580[i]);
		delete reinterpret_cast<CClass*>(this->field_570[i]);
	}
}

// @Ok
// @Matching
CDocOc::CDocOc(i16 *a2, i32 a3)
{
	u16 *LinksPointer = reinterpret_cast<u16*>(Trig_GetLinksPointer(a3));
	print_if_false(*LinksPointer == 1, "Error");
	Trig_GetPosition(&this->field_50C, LinksPointer[1]);

	this->field_4A8 = reinterpret_cast<i32>(
			this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2))));

	this->InitItem("docock");

	this->mFlags |= 0x480;
	this->mCBodyFlags &= 0xFFEF;

	this->mpLight = &M3d_DocOckLight;

	this->mHealth = 600;
	this->mRMinor = 0;
	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->field_1F4 = a3;
	this->mNode = a3;
	this->mType = 308;
	this->field_31C.bothFlags = 1;

	this->field_554 = 4;
	this->field_4EC = 1;
	this->field_324 = 25;
	this->field_21E = 100;

	if (gWhatIf)
	{
		this->mFlags |= 0x200;
		this->mScale.vx = 2048;
		this->mScale.vy = 2048;
		this->mScale.vz = 2048;

		this->field_21E = 50;
	}

	Panel_CreateHealthBar(this, 308);
	this->field_194 = 0xFFFC0000;
	this->field_198 = 0x3FFF;
}

// @Ok
void DocOck_CreateDocOck(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(*stack);
	i32 v3 = static_cast<i32>(stack[1]);

	*result = reinterpret_cast<u32>(new CDocOc(v2, v3));
}

// @Ok
void CDocOc::RenderClaws(void)
{
	M3d_Render(this->field_570[0]);
}

// @Ok
void CDocOc::PlaySingleAnim(unsigned int a2, int a3, int a4)
{
	this->field_4C4 = 0;
	this->RunAnim(a2, a3, a4);
}


// @Ok
void CDocOc::PlayIdleOrGloatAnim(void)
{
	if ((this->field_218 & 8) == 0)
	{
		if (!this->field_554)
		{
			this->PlaySingleAnim(32, 0, -1);
		}
		else
		{
			this->CalculateStage();
			if (this->field_55C <= 600 && G_MECHLIST_PLAYER->mHealth > 0)
			{
				if (this->field_550 == 1)
					this->PlaySingleAnim(5, 0, -1);
				else
					this->PlaySingleAnim(0, 0, -1);
			}
			else
			{
				if (this->field_550 == 1)
					this->PlaySingleAnim(35, 0, -1);
				else
					this->PlaySingleAnim(1, 0, -1);

				this->field_55C = 0;
				this->field_31C.bothFlags = 0x8000;
				this->dumbAssPad = 0;
			}
		}
	}
}

// @Ok
 void CDocOc::Gloat(void)
{
	if (this->mAnimFinished)
	{
		this->field_31C.bothFlags = 2;
		this->dumbAssPad = 0;
		this->PlayIdleOrGloatAnim();
	}
}

// @Ok
// @Matching
 i32* CDocOc::KillCommandBlock(i32* a1)
{
	i32* res = reinterpret_cast<i32*>(a1[a1[1]-1]);

	if (this->field_4AC == a1)
	{
		this->field_4AC = res;
	}
	else
	{
		i32* it = this->field_4AC;

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
 void CDocOc::KillAllCommandBlocks(void)
{
	for (int* cur = this->field_4AC; cur; cur = this->KillCommandBlock(cur))
		;
	this->field_4AC = 0;
}

// @Ok
void CDocOc::TakeHit(void)
{
	CTrapWebEffect *pWeb;
	switch (this->dumbAssPad)
	{
		case 0:

			pWeb = reinterpret_cast<CTrapWebEffect*>(
					Mem_RecoverPointer(&this->field_104));

			if (pWeb)
				pWeb->Burst();

			this->field_104.pWhatever = 0;

			if (this->field_218 & 0x10)
			{
				this->PlaySingleAnim(0x1C, 0, -1);
			}
			else if (this->field_218 & 0x20)
			{
				this->PlaySingleAnim(0x1D, 0, -1);
			}
			else if (this->field_218 & 0x40)
			{
				this->PlaySingleAnim(0x1E, 0, -1);
			}
			else
			{
				this->PlaySingleAnim(0x1B, 0, -1);
			}

			this->dumbAssPad++;

			break;
		case 1:
			if (this->mAnimFinished)
			{
				this->PlayIdleOrGloatAnim();
				this->field_31C.bothFlags = 1024;
				this->dumbAssPad = 1;
			}
			break;
	}
}

// @Ok
 void CDocOc::CalculateStage(void)
{
	if (this->mHealth > 400)
	{
		this->field_550 = 1;
	}
	else if (this->mHealth > 200)
	{
		this->field_550 = 2;
	}
	else
	{
		this->field_550 = 3;
	}
}

// @Ok
void CDocOc::HangAndGetBeaten(void)
{

	if (this->mAnimFinished)
	{
		if (this->mAnim == 23 || this->mAnim == 32)
		{
			this->PlaySingleAnim(0x17, 0, -1);
		}
		else
		{
			this->PlaySingleAnim(0x20, 0, -1);
		}
	}

	switch(this->dumbAssPad)
	{
		case 0:
			this->mCBodyFlags |= 0x10;
			this->mRMinor = 100;
			this->dumbAssPad = 1;
			break;
		case 1:
			if (!this->field_4B8)
			{
				this->mCBodyFlags &= 0xFFEF;
				this->mRMinor = 0;

				if (this->field_104.pWhatever)
				{
					CTrapWebEffect *pWeb = reinterpret_cast<CTrapWebEffect*>(
							Mem_RecoverPointer(&this->field_104));

					if (pWeb)
						pWeb->Burst();

					this->field_104.pWhatever = 0;
				}

				this->PlaySingleAnim(0x18, 0, -1);
				this->field_31C.bothFlags = 2048;
				this->dumbAssPad = 0;
			}
			break;
	}
}

// @Ok
// @Note: verified against IDA decompile of CDocOc::Initialise (0x436980, in
// spideypc_names.txt). The original fully inlines this walk of ControlBaddyList into
// Initialise (there is no standalone machine code for ResolveSwitches to compare
// against), same as the other single-caller CDocOc members in this file, so this is
// marked INLINE like them. mNextItem (offset 0x20) and mType (offset 0x38) match CItem
// in ob.h.
 void CDocOc::ResolveSwitches(void)
{
	i32 index = 0;
	for (CBaddy* pBaddy = reinterpret_cast<CBaddy*>(G_CONTROL_BADDY_LIST);
			pBaddy;
			pBaddy = reinterpret_cast<CBaddy*>(pBaddy->mNextItem))
	{
		if (pBaddy->mType == 407)
		{
			this->field_4FC[index++] = pBaddy;
		}
	}

	print_if_false(index == 4, "Error");
}

// @Ok
// @Note: verified field by field against IDA decompile of 0x436980, including the
// inlined ResolveSwitches loop, the field_31C/dumbAssPad/mShadowPos order, and the
// CalculateStage thresholds.
void CDocOc::Initialise(void)
{
	this->field_518 = 455;
	this->ResolveSwitches();
	this->field_4EC = 1;
	this->field_4F4 = Rnd(4);

	print_if_false(this->field_4FC[0]->field_100 == 1, "Error0");
	print_if_false(this->field_4FC[1]->field_100 == 1, "Error1");
	print_if_false(this->field_4FC[2]->field_100 == 1, "Error2");
	print_if_false(this->field_4FC[3]->field_100 == 1, "Error3");

	this->field_31C.bothFlags = 0x10000;
	this->dumbAssPad = 0;
	this->mShadowPos.vy = Utils_GetGroundHeight(&this->mPos, 0, 0x2000, 0);
	if (this->mShadowPos.vy != -1)
	{
		this->mPos.vy = this->mShadowPos.vy - (this->field_21E << 12);
	}

	this->CalculateStage();
}

void validate_CDocOc(void){
	VALIDATE_SIZE(CDocOc, 0x590);

	VALIDATE(CDocOc, field_324, 0x324);
	VALIDATE(CDocOc, field_328, 0x328);

	VALIDATE(CDocOc, field_334, 0x334);

	VALIDATE(CDocOc, field_4A8, 0x4A8);
	VALIDATE(CDocOc, field_4B8, 0x4B8);
	VALIDATE(CDocOc, field_4AC, 0x4AC);
	VALIDATE(CDocOc, field_4BC, 0x4BC);

	VALIDATE(CDocOc, field_4C4, 0x4C4);

	VALIDATE(CDocOc, field_4EC, 0x4EC);

	VALIDATE(CDocOc, field_4F4, 0x4F4);

	VALIDATE(CDocOc, field_4FC, 0x4FC);

	VALIDATE(CDocOc, field_50C, 0x50C);
	VALIDATE(CDocOc, field_518, 0x518);

	VALIDATE(CDocOc, field_530, 0x530);

	VALIDATE(CDocOc, field_550, 0x550);

	VALIDATE(CDocOc, field_554, 0x554);
	VALIDATE(CDocOc, field_55C, 0x55C);
	VALIDATE(CDocOc, field_570, 0x570);
	VALIDATE(CDocOc, field_580, 0x580);
}

// @Bogus
// Three things stay in the exe here.
// The constructor, because our CDocOc is missing two virtuals the original
// vtable at 0x53B740 has: AI (0x435610, slot 2) and Hit (0x4389A0, slot 3).
// Stamping our vtable would give the boss CBaddy::AI and CBaddy::Hit.
// DocOck_CreateDocOck and DocOck_RelocatableModuleInit go with it, they build
// (or hand out the pointer that builds) that same object.
// The destructor, because it clears gBossRelated (0x56E998, baddy.cpp) and
// calls Panel_DestroyHealthBar, which clears gHealthBarOne/gHealthBarTwo
// (panel.cpp). None of those three globals is on a G_ macro yet, so our copy
// would go to zero while the exe kept a pointer to the freed boss.
void patch_docock(void)
{
	PATCH_PUSH_RET(0x00434480, DocOck_RelocatableModuleClear);

	PATCH_PUSH_RET_POLY(0x00435390, CDocOc::RenderClaws, "?RenderClaws@CDocOc@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004367E0, CDocOc::TakeHit, "?TakeHit@CDocOc@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x00436890, CDocOc::PlayIdleOrGloatAnim, "?PlayIdleOrGloatAnim@CDocOc@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x00436980, CDocOc::Initialise, "?Initialise@CDocOc@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004384E0, CDocOc::HangAndGetBeaten, "?HangAndGetBeaten@CDocOc@@QAEXXZ");
}
