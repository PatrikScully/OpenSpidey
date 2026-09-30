#include "hostage.h"
#include "my_patch.h"
#include "validate.h"
#include "utils.h"
#include "ps2redbook.h"
#include "mem.h"
#include "ai.h"
#include "message.h"
#include "spidey.h"
#include "trig.h"
#include "ps2m3d.h"

// guess: random XA speech sub-id table, picked with Rnd(5) when the player
// gets close to a waiting hostage in CHostage::FollowWaypoints.
EXPORT i32 gHostageXaSubIds[5] = { 0, 1, 2, 4, 0xC };

// @Ok
// @Matching
void Hostage_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Hostage_RelocatableModuleClear;
	pMod->field_C[0] = Hostage_CreateHostage;
}

// @Ok
// Message loop over pMessage, mAIProcList->Execute(), then a 7-way switch on
// field_324 whose bodies are exactly CHostage::WaitForPlayer/FollowWaypoints/
// BegMotherfucker/GetUp/DisappearBitch (inlined by the original compiler,
// same-TU calls) and DieHostage (real call, separate function, matches
// names.json 0x442650). Traced the whole thing against the disasm at
// 0x442D10 (672 bytes).
// Found a real logic bug in the message loop guard while tracing this: the
// disasm shows the switch(pMsg->field_14) body only ever runs its case 1/2/3
// when NEITHER (field_2A8 & 0x1000) NOR field_324==6 hold (the pMsg->field_14
// >= 0xB compare is only reached, and only matters, on the branch where one
// of those two IS set, and since the cases only match field_14 1/2/3, all
// below 0xB, that compare can never let a case body run on that branch; it
// is dead code carried over from the source, kept as-is per the "reproduce
// the bug, don't fix it" rule). A flat `A || B || C` OR (as previously
// written here) computes the opposite gating on (A || B) and is wrong.
// The correct guard is the negation of the first two terms, OR'd with the
// third: `!((field_2A8 & 0x1000) || field_324 == 6) || pMsg->field_14 >= 0xB`.
// Also confirmed still correct from a previous pass: the field_40 "call for
// help" counter compares against the OLD value before incrementing
// (pMsg->field_40++ < 0xF), not the pre-incremented value.
void CHostage::AI(void)
{
	this->DoPhysics(0);
	M3d_BuildTransform(this);

	for (CMessage* pMsg = this->pMessage; pMsg; pMsg = pMsg->mNext)
	{
		if (!((this->field_2A8 & 0x1000) || this->field_324 == 6) || pMsg->field_14 >= 0xB)
		{
			switch (pMsg->field_14)
			{
				case 1:
					if (this->field_324 == 3)
					{
						pMsg->field_40 = -60;
						continue;
					}

					if (pMsg->field_40++ < 0xF)
						continue;

					this->TellSomebodyToShootMe();
					break;

				case 2:
					this->field_324 = 3;
					this->dumbAssPad = 0;
					this->field_32C = pMsg->mHandle;
					break;

				case 3:
					this->field_324 = 6;
					this->dumbAssPad = 0;
					break;
			}
		}

		pMsg->field_10 |= 1;
	}

	this->CleanUpMessages(0, 0);

	if (this->mAIProcList)
	{
		this->mAIProcList->Execute();
		this->CleanUpAIProcList(0);
	}

	switch (this->field_324)
	{
		case 0:
		{
			i32 groundHeight = Utils_GetGroundHeight(&this->mPos, 300, 300, 0);
			if (groundHeight != -1)
			{
				this->field_2A0 = groundHeight;
				this->mPos.vy = groundHeight - (this->field_21E << 12);
				this->field_29C = this->mPos.vy;
			}

			this->CycleAnim(0, 1);
			this->field_324 = 1;
			this->dumbAssPad = 0;
			this->field_230 = Rnd(120) + 120;
			break;
		}
		case 1:
			this->WaitForPlayer();
			break;
		case 2:
			this->FollowWaypoints();
			break;
		case 3:
			this->BegMotherfucker();
			break;
		case 4:
			this->GetUp();
			break;
		case 5:
			this->DisappearBitch();
			break;
		case 6:
			this->DieHostage();
			break;
	}

	this->mShadowPos.vx = this->mPos.vx;
	this->mShadowPos.vy = this->mPos.vy + (this->field_21E << 12);
	this->mShadowPos.vz = this->mPos.vz;
}

// @Ok
// @Matching
void CHostage::DieHostage(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->mCBodyFlags &= ~0x10u;
			this->MarkAIProcList(1, 0, 0);
			this->mAcc.vz = 0;
			this->mAcc.vy = 0;
			this->mAcc.vx = 0;
			this->Neutralize();
			this->mFlags |= 0x800;
			this->mTRN = 128;
			this->KillShadow();

			this->mFlags |= 0x400;
			this->field_328 = 0;

			this->RunAnim(this->mType == 315 ? 10 : 3, 0, -1);
			this->StopMyXA();
			this->dumbAssPad++;
		case 1:
			this->field_328++;
			this->mRGB = ((this->field_328 & 1) ? 0xC03030 : 0) + 0x3F0F0F;

			if (this->field_328 > 7)
			{
				this->dumbAssPad++;
				this->field_328 = 0;
			}
			break;
		case 2:
			if (this->field_328++ >= 20)
			{
				this->dumbAssPad = 3;
				this->Die(0);
			}

			i32 diff;
			diff = (20 - this->field_328);
			diff += (diff << 4);
			diff *= 3;
			diff = (diff << 10) >> 12;

			this->mRGB = (((diff << 10) | (diff & 0xFFFFFFFC)) << 6) | (diff >> 2);

			break;
		case 3:
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @Matching
void CHostage::FollowWaypoints(void)
{
	SMoveToInfo moveInfo;
	moveInfo.field_0.vx = 0;
	moveInfo.field_0.vy = 0;
	moveInfo.field_0.vz = 0;

	if ((this->field_218 & 1) && !(this->field_218 & 2))
	{
		if (this->DistanceToPlayer(10) < 0x200)
		{
			this->field_218 |= 2;
			Redbook_XAPlayPos(7, gHostageXaSubIds[Rnd(5)], &this->mPos, 100);
		}
	}

	switch (this->dumbAssPad)
	{
		case 0:
			Trig_GetPosition(&moveInfo.field_0, this->field_1F4);
			moveInfo.field_C = 0xF0;
			moveInfo.field_10 = 0x50;
			moveInfo.field_14 = 0x1C7;

			new CAIProc_MoveTo(this, &moveInfo, 1);

			this->SetHeight(1, 0x64, 0x258);
			this->dumbAssPad++;

			if (this->field_2F0 & 8)
				goto done;
		case 1:
			this->SetHeight(0, 0x64, 0x258);

			if (this->field_288 & 1)
			{
				this->field_288 &= ~1;

				if (this->GetNextWaypoint())
				{
					this->dumbAssPad = 0;
					return;
				}

				this->CycleAnim(this->field_298.Bytes[0], 1);

done:
				this->dumbAssPad = 0;
				this->field_324 = 5;
			}
			else
			{
				this->RunAppropriateAnim();
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

EXPORT i32 gMaleHostageOne[2] = { 0x3030404, 4 };

EXPORT i32 gFemaleHostageOne[2] = { 0x4040504, 3 };

// @Ok
void CHostage::SetHostageType(i32 a2)
{
	this->mType = a2;

	switch (this->mType)
	{
		case 305:
			this->InitItem("hostage");
			this->field_21E = 100;
			this->field_294.Int = gMaleHostageOne[0];
			this->field_298.Int = gMaleHostageOne[1];
			break;
		case 315:
			this->InitItem("hostagef");
			this->field_21E = 100;
			this->field_294.Int = gFemaleHostageOne[0];
			this->field_298.Int = gFemaleHostageOne[1];
			break;
		default:
			print_if_false(0, "Unknown hostage type!");
			break;
	}
}

// @Ok
CHostage::~CHostage(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
}

// @Ok
void Hostage_RelocatableModuleClear(void)
{
	for (CBody* cur = G_BADDY_LIST; cur; )
	{
		CBody* next = reinterpret_cast<CBody*>(cur->mNextItem);
		if (cur->mType == 305)
		{
			delete cur;
		}

		cur = next;
	}
}

// @Ok
void CHostage::BegMotherfucker(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();

			if (Mem_RecoverPointer(&this->field_32C))
			{
				new CAIProc_LookAt(
						this,
						reinterpret_cast<CBody*>(this->field_32C.pWhatever),
						0,
						0,
						80,
						200);
			}

			if (G_DIFFICULTY_LEVEL == 2)
			{
				this->field_230 = 100;
			}
			else if (G_DIFFICULTY_LEVEL == 3)
			{
				this->field_230 = 65;
			}

			this->CycleAnim((this->mType == 315) + 5, 1);
			this->dumbAssPad++;
			this->HostageXAPlay(7, Rnd(3) + 9, 50);

			break;
		case 1:
			if (--this->field_230 <= 0)
			{
				this->field_324 = 2;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// Confirmed against the inlined call site in CHostage::BegMotherfucker
// (0x442B10, disasm around 0x442C1E-0x442C5B): Rnd(3) result added to 9 for
// a3, args pushed for Redbook_XAPlayPos(7, a3, &mPos, 50) in the right cdecl
// order, then AttachXA(7, a3) called only on success. No standalone address
// in names.json, this gets inlined into every caller same as the original.
void INLINE CHostage::HostageXAPlay(i32 a2, i32 a3, i32 a4)
{
	if (Redbook_XAPlayPos(a2, a3, &this->mPos, a4))
		this->AttachXA(a2, a3);
}


// @Ok
void CHostage::TellSomebodyToShootMe(void)
{
	if (G_DIFFICULTY_LEVEL != 1 && G_DIFFICULTY_LEVEL)
	{
		CBaddy *pBaddy = this->GetClosest(304, 0);

		if (pBaddy)
		{
			new CMessage(this, pBaddy, 13, 0);
		}
		else
		{
			new CMessage(this, this, 1, 0);
		}
	}
}

// @Ok
// Confirmed against the inlined call sites in CHostage::WaitForPlayer
// (0x442960, disasm 0x4429A1-0x442A7D and 0x442A1C-0x442A7D): Utils_CrapDist
// takes (MechList->mPos, this->mPos) with MechList read from the fixed
// address 0x6A9038, offset +8 for mPos matches CBody::mPos layout, the
// distance/mInputFlags OR-condition and the DifficultyLevel 0/1 gate on
// field_218 |= 1 match exactly, and the call chain ends in
// Baddy_SendSignal() then TellSomebodyToShootMe() (0x442C70) in that order.
// No standalone address in names.json, this gets inlined into every caller
// same as the original.
INLINE void CHostage::CheckIfFreed(void)
{
	if (Utils_CrapDist(G_MECHLIST_PLAYER->mPos, this->mPos) < 0xC8 || this->mInputFlags & 1)
	{
		if (G_DIFFICULTY_LEVEL == 1 || G_DIFFICULTY_LEVEL == 0)
			this->field_218 |= 1;
		this->Baddy_SendSignal();
		this->field_324 = 4;
		this->dumbAssPad = 0;
		this->TellSomebodyToShootMe();
	}
}

// @Ok
void CHostage::WaitForPlayer(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			if (--this->field_230 <= 0)
			{
				this->RunAnim(1, 0, -1);
				this->dumbAssPad++;
				this->field_230 = Rnd(120) + 120;
			}

			this->CheckIfFreed();
			break;
		case 1:

			if (this->mAnimFinished)
			{
				this->CycleAnim(0, 1);
				this->dumbAssPad  = 0;
			}

			this->CheckIfFreed();
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
void INLINE CHostage::DisappearBitch(void)
{
	switch (this->dumbAssPad)
	{
		case 2:
			if (this->Die(2))
			{
				this->Die(3);
				this->dumbAssPad++;
			}
			else
			{
				this->SetHeight(0, 100, 600);
			}
			break;
		case 0:
			this->Neutralize();
			this->dumbAssPad++;
		case 1:
			this->mCBodyFlags &= 0xFFEF;
			this->field_2A8 |= 0x5000;
			this->mRMinor = 0;
			this->Die(1);
			this->dumbAssPad++;
			break;
		case 3:
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
void INLINE CHostage::GetUp(void)
{
	switch(this->dumbAssPad)
	{
		case 0:
			this->RunAnim(2, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
			{
				if (this->GetNextWaypoint())
				{
					this->field_324 = 2;
					this->dumbAssPad = 0;
				}
				else
				{
					this->field_324 = 5;
					this->dumbAssPad = 1;
				}
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @Matching
CHostage::CHostage(i16* a2, i32 a3)
{
	i16 *afterAngles = this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2)));

	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));
	this->ShadowOn();

	this->mShadowScale = 48;
	this->field_1F4 = a3;
	this->mNode = a3;
	this->mRMinor = 128;
	this->field_230 = 0;
	this->field_216 = 32;

	this->mPushVal = 64;
	this->field_324 = 0;

	this->field_2A8 |= 1;

	this->field_294.Int = gMaleHostageOne[0];
	this->field_298.Int = gMaleHostageOne[1];

	this->mCBodyFlags &= 0xFFEF;
	this->ParseScript(reinterpret_cast<u16*>(afterAngles));
}

// @Ok
void Hostage_CreateHostage(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(*stack);
	i32 v3 = static_cast<i32>(stack[1]);

	*result = reinterpret_cast<u32>(new CHostage(v2, v3));
}

void validate_CHostage(void){
	VALIDATE_SIZE(CHostage, 0x334);


	VALIDATE(CHostage, field_324, 0x324);
	VALIDATE(CHostage, field_328, 0x328);
	VALIDATE(CHostage, field_32C, 0x32C);
}

// @Bogus
// The constructor, destructor and Hostage_CreateHostage (it just does
// `new CHostage(...)`) stay in the exe. hostageVtable (0x53B874, 18
// entries, confirmed against the maintainer's IDB name) has slot 17
// (0x442A80) pointing at SetHostageType, so the original calls it through
// the vtable. SetHostageType uses the same slot in our standalone objects.
// CheckIfFreed, HostageXAPlay, GetUp and DisappearBitch have no
// standalone address, they get inlined into their callers in the
// original same as here.
void patch_hostage(void)
{
	PATCH_PUSH_RET(0x00442410, Hostage_RelocatableModuleInit);
	PATCH_PUSH_RET(0x00442430, Hostage_RelocatableModuleClear);

	PATCH_PUSH_RET_POLY(0x00442D10, CHostage::AI, "?AI@CHostage@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x00442650, CHostage::DieHostage, "?DieHostage@CHostage@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004427A0, CHostage::FollowWaypoints, "?FollowWaypoints@CHostage@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x00442960, CHostage::WaitForPlayer, "?WaitForPlayer@CHostage@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x00442A80, CHostage::SetHostageType, "?SetHostageType@CHostage@@UAEXH@Z");
	PATCH_PUSH_RET_POLY(0x00442B10, CHostage::BegMotherfucker, "?BegMotherfucker@CHostage@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x00442C70, CHostage::TellSomebodyToShootMe, "?TellSomebodyToShootMe@CHostage@@QAEXXZ");
}
