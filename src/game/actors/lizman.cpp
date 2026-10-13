#include "game/actors/lizman.h"
#include "compatibility/my_patch.h"
#include "compatibility/validate.h"
#include "game/ui/message.h"
#include "engine/rendering/ps2funcs.h"
#include "core/game_heap.h"
#include "engine/world/ai.h"
#include "engine/audio/ps2lowsfx.h"
#include "core/utils.h"
#include "engine/world/trig.h"
#include "engine/rendering/ps2funcs.h"
#include "game/player/spidey.h"
#include "engine/rendering/m3dutils.h"
#include <cmath>

// data seen at 0x552048/0x55204C in the original, used to set up
// field_294/field_298. Same pattern as gJonahSetup/gRhinoStrangeInitData.
EXPORT i32 gLizManSetup[2] = { 0x2020201, 0 };

// @Ok
// Field offsets, branch conditions and every store verified against the
// original disassembly (0x44aba0). Functional bar only this session: logic
// is correct, not chasing byte match.
CLizMan::CLizMan(i16* a1, i32 a2)
{
	i32 levelId = Trig_GetLevelID();

	if (levelId == 0x503 || levelId == 0x504 || levelId == 0x601 || levelId == 0x602)
		this->InitItem("lizman2");
	else
		this->InitItem("lizman");

	this->field_328 = levelId;
	i16* q = this->SquirtAngles(this->SquirtPos(a1));

	this->mType = 317;
	this->field_21E = 100;

	this->field_294.Int = gLizManSetup[0];
	this->field_298.Int = gLizManSetup[1];

	M3dUtils_ReadHooksPacket(this, const_cast<char*>(""));

	this->field_338 = 0x1000;
	this->field_32C = gAttackRelated;
	this->ShadowOn();

	this->mShadowScale = 0x30;
	this->field_3AC = 0x21;

	// @FIXME field_F4 is declared i32 (ob.h), but the original only stores
	// the low 16 bits here. Force a word store to match without touching
	// the shared header.
	*reinterpret_cast<i16*>(&this->field_F4) = 0x40;

	this->field_374 = G_TIMER_RELATED - 0x131;
	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->field_1F4 = a2;
	this->mNode = static_cast<u16>(a2);

	this->mRMinor = 0x80;
	this->field_230 = 0;
	this->field_216 = 0x20;
	this->mPushVal = 0x40;
	this->field_31C.bothFlags = 0;

	this->ParseScript(reinterpret_cast<u16*>(q));

	if (levelId == 0x505)
	{
		this->field_2A8 |= 0x2000000;
		this->field_212 = 0xF;
		this->field_398 = 0x32000;
		this->field_218 |= 0x8000;
	}
	else
	{
		this->field_398 = 0x3C000;
		this->field_212 = 0x1E;
	}
}

// @Ok
// @Matching
void LizMan_CreateLizMan(const u32* stack, u32* result)
{
	i16* v2 = reinterpret_cast<i16*>(stack[0]);
	u32 v3 = stack[1];

	*result = reinterpret_cast<u32>(new CLizMan(v2, v3));
}

// @Ok
// @Matching
void LizMan_RelocatableModuleClear(void)
{
	CItem *pSearch = G_BADDY_LIST;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 317)
			delete pSearch;

		pSearch = pNext;
	}
}

// @Ok
// @Matching
void LizMan_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = LizMan_RelocatableModuleClear;
	pMod->field_C[0] = LizMan_CreateLizMan;
}

// @Ok
// @AlmostMatching: the two register-zeroing instructions right before the
// loop (index register, angle register) come out in swapped order. Same
// mnemonics, same length, only the register operand differs. Tried 15
// source variants (for/while/do-while, declaration order and position,
// split vs combined init, increment order, comparison direction, signed
// vs unsigned index, mask operand order) with no change. Rest of the
// function matches instruction for instruction.
void CLizMan::CalculateJumpPositionArray(CVector* pTarget)
{
	i32 groundHeight = Utils_GetGroundHeight(pTarget, 0, 0x200, 0);

	i32 xDelta = (pTarget->vx - this->mPos.vx) / 48;
	i32 yDelta = (groundHeight - (this->field_21E << 12) - this->mPos.vy) / 48;
	i32 zDelta = (pTarget->vz - this->mPos.vz) / 48;

	if (this->field_3B4)
		Mem_Delete(this->field_3B4);

	this->field_3B4 = static_cast<CVector*>(DCMem_New(0x240, 0, 1, 0, 1));

	i32 x = this->mPos.vx + xDelta;
	i32 y = this->mPos.vy + yDelta;
	i32 z = this->mPos.vz + zDelta;

	i32 i = 0;
	i32 t = 0;
	do
	{
		this->field_3B4[i].vx = x;
		this->field_3B4[i].vy = y - (G_RCOSSIN_TBL[t & 0xFFF].sin << 8);
		this->field_3B4[i].vz = z;

		x += xDelta;
		y += yDelta;
		z += zDelta;
		t += 0x2B;
		i++;
	} while (t < 0x810);

	this->field_3B0 = 0;
}

// @Ok
// Logic verified against the original disassembly instruction by instruction
// (globals: G_OFFSETLIST/NumNodes for the trig node array, MechList for the
// player, BaddyList for the other-lizman check via mType==0x13D and
// field_3B4+0x234). Functional bar only this session: logic is correct, not
// chasing byte match.
i32 CLizMan::ScanNearbyNodesForJumpTarget(void)
{
	i32 result = 0;
	i32 bestDist = spidey::math::approximateDistance(this->mPos, G_MECHLIST_PLAYER->mPos) - 0x100;

	if (bestDist > 0)
	{
		for (i32 i = 1; i < NumNodes; i++)
		{
			i16* node = G_OFFSETLIST[i];
			if (*reinterpret_cast<u16*>(node) != 0x3E8)
				continue;

			i16 len = node[1];
			u8* unpacked = reinterpret_cast<u8*>(
					(reinterpret_cast<u32>(node) + len * 2 + 7) & ~3);

			if (*reinterpret_cast<u16*>(unpacked + 0x12) != 0x470A)
				continue;

			i32* pRaw = reinterpret_cast<i32*>(unpacked);
			CVector nodePos;
			nodePos.vx = pRaw[0] << 12;
			nodePos.vy = pRaw[1] << 12;
			nodePos.vz = pRaw[2] << 12;

			i32 d = spidey::math::approximateDistance(nodePos, this->mPos);
			if (d < 0x100 || d > 0xC00)
				continue;

			i32 distToPlayer = spidey::math::approximateDistance(nodePos, G_MECHLIST_PLAYER->mPos);
			if (distToPlayer >= bestDist)
				continue;

			CItem* pOther = G_BADDY_LIST;
			while (pOther)
			{
				if (pOther->mType == 0x13D)
				{
					if (spidey::math::approximateHorizontalDistance(nodePos, pOther->mPos) < 0x100)
						goto nextNode;

					CVector* pArr = reinterpret_cast<CLizMan*>(pOther)->field_3B4;
					if (pArr)
					{
						CVector* pMid = reinterpret_cast<CVector*>(
								reinterpret_cast<u8*>(pArr) + 0x234);
						if (spidey::math::approximateHorizontalDistance(nodePos, *pMid) < 0x100)
							goto nextNode;
					}
				}
				pOther = pOther->mNextItem;
			}

			result = i;
			bestDist = distToPlayer;

			nextNode:;
		}
	}

	return result;
}

extern CSVector gTrajectoryVector;

// The pending follow-on animation id shared by every lizman, 0xFFFF when
// there is none. CLizMan::HandleAnimationFollowOn (0x450B50, still in the
// exe) is the only writer (mov word [5FBC0Ch],0FFFFh at 0x450B50 and
// 0x450CA2, mov [5FBC0Ch],dx at 0x450B73), and about a dozen exe-side
// CLizMan states read it, so ours has to be the same memory.
#ifdef SPIDEY_STANDALONE
#define gLizManFollowOnAnim (*reinterpret_cast<u16*>(0x005fbc0c))
#else
static u16 gLizManFollowOnAnim;
#endif
//#define G_LIZMAN_FOLLOW_ON_ANIM (gLizManFollowOnAnim)
#define G_LIZMAN_FOLLOW_ON_ANIM (*reinterpret_cast<u16*>(0x005FBC0C))

// @Ok
void CLizMan::Guard(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_1F8 = 0;
			this->Neutralize();

			if (G_TIMER_RELATED - this->field_374 < 0x12C)
			{
				new CAIProc_LookAt(
						this,
						G_MECHLIST_PLAYER,
						0,
						0,
						128,
						200);
			}

			this->dumbAssPad++;
			break;
		case 1:

			// @FIXME - word??
			if (G_LIZMAN_FOLLOW_ON_ANIM != 0xFFFF && this->mAnim != 5)
			{
				this->PlaySingleAnim(5, 0, -1);
			}

			this->field_1F8 += this->mElapsedTicks;
			if (this->field_1F8 > 120)
			{
				this->field_1F8 = 0;
				i32 target = this->ScanNearbyNodesForJumpTarget();
				if (target)
				{
					CVector v6;
					v6.vx = 0;
					v6.vy = 0;
					v6.vz = 0;
					this->field_1F0 = 0;

					SFX_PlayPos((Rnd(2) + 134) | 0x80, &this->mPos, 0);
					Trig_GetPosition(&v6, target);
					this->field_1F4 = target;

					this->CalculateJumpPositionArray(&v6);
					this->field_31C.bothFlags = 23;
					this->dumbAssPad = 0;
				}
			}

			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
void CLizMan::Acknowledge(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->field_340 = 0;
			this->RunAnim(0, 0, -1);

			if (Mem_RecoverPointer(&this->hLizHandle))
			{
				new CAIProc_LookAt(
						this,
						reinterpret_cast<CBody*>(this->hLizHandle.pWhatever),
						0,
						2,
						70,
						200);
			}

			this->dumbAssPad++;
			break;
		case 1:

			if(this->mAnimFinished)
			{
				this->field_31C.bothFlags = 25;
				this->dumbAssPad = 0;
			}
			
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
void CLizMan::SwitchFromEulerToMatrix(void)
{
	if ((this->mExtraFlags & 1) == 0)
	{
		this->mExtraFlags |= 1;
		M3dMaths_RotMatrixYXZ(
				reinterpret_cast<SVECTOR*>(&this->mAngles),
				&this->mTransform);
	}
}

// @Ok
// Verified against the original disassembly (0x450270). Functional bar
// only this session: logic is correct, not chasing byte match (the
// project-wide inlined-operator- issue documented in CLAUDE.md would block
// a byte match anyway).
void CLizMan::RunToWhereActionIs(CVector* pTarget)
{
	if (spidey::math::approximateDistance(this->mPos, *pTarget) > 0x5DC)
		return;

	if (!this->AddPointToPath(&this->mPos, 0x5DC))
		return;

	i32 dx = pTarget->vx - this->mPos.vx;
	i32 biasX = (dx > 0) ? -0x64000 : 0x64000;
	i32 dz = pTarget->vz - this->mPos.vz;
	i32 biasZ = (dz > 0) ? -0x64000 : 0x64000;

	CVector adjustedTarget;
	adjustedTarget.vx = pTarget->vx + biasX;
	adjustedTarget.vy = this->mPos.vy;
	adjustedTarget.vz = pTarget->vz + biasZ;

	if (!G_MECHLIST_PLAYER->field_57C)
	{
		if (this->PathCheck(&this->mPos, &G_MECHLIST_PLAYER->mPos, NULL, 0x37) == 0)
		{
			if (this->AddPointToPath(&G_MECHLIST_PLAYER->mPos, 0x5DC))
				goto cleanup;
		}
	}

	{
		i32 result = this->PathCheck(&this->mPos, &adjustedTarget, NULL, 0x37);

		if (result == 0)
		{
			if (this->AddPointToPath(&adjustedTarget, 0x5DC))
				goto cleanup;
			return;
		}

		if (result != 2)
			return;

		if (spidey::math::approximateDistance(this->mPos, adjustedTarget) < 0x64)
			return;

		CVector delta = adjustedTarget - this->mPos;
		delta >>= 12;
		delta *= 0xE74;
		delta += this->mPos;

		if (!this->AddPointToPath(&delta, 0))
			return;
	}

cleanup:
	this->Neutralize();

	i32 flags = this->field_2F0;
	this->field_374 = G_TIMER_RELATED - 0xF0;
	this->field_31C.bothFlags = 2;
	this->field_2A8 &= ~0x10000000;
	*reinterpret_cast<u8*>(&flags) |= 1;
	this->field_2F0 = flags;
	this->dumbAssPad = 0;
}

// @Ok
void CLizMan::HelpOutBuddy(CMessage* pMessage)
{
	if (!this->field_390)
	{
		if (this->field_31C.bothFlags == 2 || this->field_31C.bothFlags == 1)
		{
			CItem *pItem = reinterpret_cast<CItem*>(
					Mem_RecoverPointer(&pMessage->mHandle));

			if (pItem)
				this->RunToWhereActionIs(&pItem->mPos);
		}
	}

}

// @Ok
void CLizMan::PlaySingleAnim(i16 a1, i32 a2, i32 a3)
{
	this->field_340 = 0;
	this->RunAnim(a1, a2, a3);
}

// @Ok
void CLizMan::StandStill(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->PlaySingleAnim(19, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			print_if_false(0, "Unknown substate.");
			break;
	}
}

// @Ok
void CLizMan::SwitchFromMatrixToEuler(void)
{
	if ( this->mExtraFlags & 1)
	{
		this->mExtraFlags &= 0xFFFFFFFE;
		this->mAngles.vz = 0;
		this->mAngles.vx = 0;
		this->mAngles.vy = (3072 - ratan2(-this->mTransform.m[2][2], -this->mTransform.m[0][2])) & 0xFFF;
	}
}

// @Ok
void CLizMan::StopClimbing(void)
{
	this->SwitchFromMatrixToEuler();
	this->field_390 = 0;
}

// @Ok
// Verified against the original disassembly (0x44dec0), case by case.
// Functional bar only this session: logic is correct, not chasing byte
// match.
void CLizMan::FlyAcrossRoom(void)
{
	switch(this->dumbAssPad)
	{
		case 0:
		{
			this->field_394 |= 2;
			this->ClearAttackFlags();

			this->field_310 = 0;
			if (this->field_318 == 1 || this->field_318 == 2)
				this->mRMinor = 0;

			i32 maxVel = (this->mVel.vx > this->mVel.vz) ? this->mVel.vx : this->mVel.vz;
			i32 velProduct = this->field_1F8 * maxVel;
			i32 velProductSign = velProduct >> 31;
			if ((velProduct ^ velProductSign) - velProductSign < 0x100000)
			{
				i32 animId;
				if (this->field_218 & 0x400)
					animId = 0x22;
				else
					animId = (this->field_218 & 0x800) ? 0x21 : 0xC;
				this->field_340 = 0;
				this->RunAnim(animId, 0, -1);
			}
			else
			{
				this->field_340 = 0;
				this->RunAnim(0x15, 0, -1);
			}

			if (this->field_1F8 == 0x29A)
				this->field_1F8 = 0x14;

			if (this->mHealth <= 0)
			{
				this->dumbAssPad = 10;
				this->field_394 |= 1;
			}
			else
			{
				this->dumbAssPad++;
			}
			break;
		}
		case 1:
			this->field_394 |= 2;
			this->DoLizmanPhysics();

			if (this->field_1F8 > this->mElapsedTicks)
			{
				this->field_1F8 -= this->mElapsedTicks;
			}
			else
			{
				this->field_1F8 = 0;
				this->mVel.vx = 0;
				this->mVel.vy = 0;
				this->mVel.vz = 0;
				this->mFric.vx = 1;
				this->mFric.vy = 1;
				this->mFric.vz = 1;

				if (this->ShouldFall(0xC8, this->field_398))
				{
					this->field_31C.bothFlags = 0x12;
					this->field_218 &= ~2;
					this->dumbAssPad = 0;
				}
				else
				{
					this->SetHeight(1, 0x64, 0x258);

					if (this->field_318 == 1 || this->field_318 == 2)
					{
						this->CheckFallBack();
						if (this->field_2A8 & 0x10)
						{
						}
						this->PlaySingleAnim(0xE, 0, -1);
						this->dumbAssPad++;
					}
					else if (this->mAnim == 0x15)
					{
						this->PlaySingleAnim(5, 0, -1);
						this->field_31C.bothFlags = 25;
						this->dumbAssPad = 0;
					}
					else
					{
						this->dumbAssPad = 5;
					}
				}
			}
			break;
		case 2:
			this->SetHeight(0, 0x64, 0x258);
			if (G_LIZMAN_FOLLOW_ON_ANIM != 0xFFFF)
			{
				this->mRMinor = 0x80;
				this->dumbAssPad++;
			}
			break;
		case 3:
			if (this->SetHeight(0, 0x64, 0x258) == 2 && G_LIZMAN_FOLLOW_ON_ANIM != 0xFFFF)
			{
				if (this->IsSafeToSwitchToFollowWaypoints())
				{
					this->field_31C.bothFlags = 2;
				}
				else
				{
					this->field_31C.bothFlags = 1;
				}
				this->dumbAssPad = 0;
			}
			break;
		case 5:
			if (this->SetHeight(0, 0x64, 0x258) == 2 && G_LIZMAN_FOLLOW_ON_ANIM != 0xFFFF)
			{
				this->field_31C.bothFlags = 25;
				this->dumbAssPad = 0;
			}
			break;
		case 10:
			this->field_394 |= 3;
			this->DoLizmanPhysics();

			if (this->field_218 & 0x1000)
			{
				i16 speed = *reinterpret_cast<i16*>(&this->mElapsedTicks);
				this->mAngles.vx += this->field_330 * speed;
				this->mAngles.vy += this->field_334 * speed;
			}

			if (this->field_1F8 > this->mElapsedTicks)
			{
				this->field_1F8 -= this->mElapsedTicks;
			}
			else
			{
				this->field_1F8 = 0;
				this->mVel.vx = 0;
				this->mVel.vy = 0;
				this->mVel.vz = 0;
				this->mFric.vx = 1;
				this->mFric.vy = 1;
				this->mFric.vz = 1;

				if (this->ShouldFall(0xC8, this->field_398))
				{
					this->field_31C.bothFlags = 0x12;
					this->field_218 &= ~2;
					this->dumbAssPad = 0;
				}
				else
				{
					this->Neutralize();
					this->SetHeight(1, 0x64, 0x258);
					this->field_31C.bothFlags = 0x14;
					this->dumbAssPad = 0;
				}
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// FlyAcrossRoom above calls these; MSVC inlines a same-TU stub call even
// when it is defined later in the file, which pollutes FlyAcrossRoom's
// codegen (leaf-first rule). Keep the MSVC inliner away, same fix as
// gsub_5027A0 in DXinit.cpp.
#ifdef _MSC_VER
#pragma auto_inline(off)
#endif
// @Ok
// Decompiled from 0x450920 (560 bytes) via IDA disassembly + Hex-Rays,
// cross-checked field by field against CBody's validated layout (mVel
// 0x60, mAcc 0x6C, mFric 0x78, field_80 0x80, mAngVel 0x88, mAngAcc 0x8E,
// mAngFric 0x94, field_A8 0xA8, mCollision 0xE0) and against CSuper's
// mFrame (0x128) / mAnim (0x12A). field_A8 = gTrajectoryVector matches the
// identical idiom already @Ok in CPlatform::DoPhysics (platform.cpp).
// rollFactor is a Q12 fixed-point "how much of mVel to apply this frame"
// value: 4096 (1.0) normally, and a sin-table-derived value while playing
// specific fly/roll anims (2, 8, 0x23) so the landing arc eases instead of
// snapping. The two operator* + operator>> calls in the original
// (((mVel * rollFactor) >> 12) * field_80) are the same expression on both
// sides of the original's rollFactor==4096 shortcut branch, so this is
// written as one expression without the branch.
void CLizMan::DoLizmanPhysics(void)
{
	this->field_A8 = gTrajectoryVector;
	this->mCollision = 0;

	i32 speed = this->mElapsedTicks;

	this->mVel += this->mAcc;
	this->mVel %= this->mFric;
	this->mVel.KillSmall();

	i32 rollFactor;
	switch (this->mAnim)
	{
		case 2:
			rollFactor = (my_abs(G_RCOSSIN_TBL[(this->mFrame << 7) & 0xFFF].sin) >> 1) + 1024;
			break;
		case 8:
			rollFactor = (my_abs(G_RCOSSIN_TBL[((this->mFrame << 12) / 20) & 0xFFF].sin) >> 2) + 2048;
			break;
		case 0x23:
			rollFactor = ((my_abs(G_RCOSSIN_TBL[((this->mFrame << 12) / 24) & 0xFFF].sin) * 3) >> 2) + 1024;
			break;
		default:
			rollFactor = 0x1000;
			break;
	}

	this->mPos += ((this->mVel * rollFactor) >> 12) * speed;

	i16 angSpeed = static_cast<i16>(speed);
	this->mAngles.vx += this->mAngVel.vx * angSpeed;
	this->mAngles.vy += this->mAngVel.vy * angSpeed;
	this->mAngles.vz += this->mAngVel.vz * angSpeed;
	this->mAngles.Mask();

	this->mAngVel += this->mAngAcc;
	this->mAngVel %= this->mAngFric;
	this->mAngVel.KillSmall();

	this->field_2AC = 0;
}

// @Ok
// @AlmostMatching: stack frame size and instruction count now match (0x28
// bytes, same 5 calls). Two residues left, both around register/address
// scheduling, not missing logic: (1) the address of `offset` is computed
// by a `lea` in a different spot (original hoists it right after the
// prologue; ours computes it right before the RotateY call), which shifts
// a couple of neighbouring instructions; (2) original clears eax
// (`xor eax,eax`) between the two epilogue pops, ours does not, since eax
// already holds 0 from CClass_new's null check by a different path. 16
// distinct source hypotheses tried and logged in
// CLizMan_CheckFallBack.attempts.md: compile-time-folded vs runtime shift
// for the +-75<<12 constant, declaration order of rotated/offset/backDist/
// angle (all permutations), extracting the angle read into a named local
// (fixed most of the diffs), CVector 3-arg constructor vs field-by-field
// init, nested/sibling block scoping to hint stack slot reuse, an
// anonymous `&CVector(...)` temporary as the call argument, reusing
// `offset`'s storage for the final sum instead of a separate `target`
// local (this fixed the stack frame size), if/else vs ternary for
// backDist, and matching CThug::CheckFallBack's known-good-looking
// structure verbatim (this made things worse; on inspection
// CThug::CheckFallBack's own @Ok tag is stale, cmpsum shows 44 mnemonic
// diffs on it too, reported separately, not fixed here).
void CLizMan::CheckFallBack(void)
{
	CVector rotated;
	CVector offset;
	i32 backDist = (this->field_2A8 & 0x10) ? -75 : 75;
	i32 angle = this->mAngles.vy;

	offset.vz = backDist << 12;

	Utils_RotateY(&rotated, &offset, angle);

	offset = this->mPos + rotated;

	if (this->PathCheck(&this->mPos, &offset, NULL, 0x37) == 2)
	{
		new CAIProc_RotY(this, 0x7FF, 4, 0);
	}
}
#ifdef _MSC_VER
#pragma auto_inline(on)
#endif

// @Ok
i32 CLizMan::IsSafeToSwitchToFollowWaypoints(void)
{
	if (this->field_1F0 || this->field_1F4 > 0)
		return 1;
	return 0;
}

// addresses confirmed against the maintainer's IDB (idb_globals.txt):
// gGlobalLizMan 0x682C44, gLizManAttackFlag 0x682B6E. Verified by walking
// the inlined copy of this function inside CLizMan::FlyAcrossRoom's case 0
// (0x44dec4-0x44df1b) instruction by instruction, both match exactly.
#ifdef SPIDEY_STANDALONE
#define gGlobalLizMan (*reinterpret_cast<CLizMan**>(0x00682c44))
#else
static CLizMan* gGlobalLizMan;
#endif
//#define G_GLOBAL_LIZ_MAN (gGlobalLizMan)
#define G_GLOBAL_LIZ_MAN (*reinterpret_cast<CLizMan**>(0x00682C44))
#ifdef SPIDEY_STANDALONE
#define gLizManAttackFlag (*reinterpret_cast<unsigned char*>(0x00682b6e))
#else
static unsigned char gLizManAttackFlag;
#endif
//#define G_LIZ_MAN_ATTACK_FLAG (gLizManAttackFlag)
#define G_LIZ_MAN_ATTACK_FLAG (*reinterpret_cast<unsigned char*>(0x00682B6E))

// @Ok
void CLizMan::ClearAttackFlags(void)
{
	if (G_GLOBAL_LIZ_MAN == this)
	{
		G_GLOBAL_LIZ_MAN = NULL;
	}
	else if ((this->field_39C & 2))
	{
		G_LIZ_MAN_ATTACK_FLAG &= ~this->field_39D;
	}

	this->field_39C = 0;
	this->field_39D = 0;
}

void validate_CLizMan(void){
	VALIDATE_SIZE(CLizMan, 0x3B8);


	VALIDATE(CLizMan, field_328, 0x328);
	VALIDATE(CLizMan, field_32C, 0x32C);
	VALIDATE(CLizMan, field_330, 0x330);
	VALIDATE(CLizMan, field_334, 0x334);
	VALIDATE(CLizMan, field_338, 0x338);
	VALIDATE(CLizMan, field_340, 0x340);

	VALIDATE(CLizMan, hLizHandle, 0x36C);

	VALIDATE(CLizMan, field_374, 0x374);
	VALIDATE(CLizMan, field_390, 0x390);
	VALIDATE(CLizMan, field_394, 0x394);
	VALIDATE(CLizMan, field_398, 0x398);

	VALIDATE(CLizMan, field_39C, 0x39C);
	VALIDATE(CLizMan, field_39D, 0x39D);

	VALIDATE(CLizMan, field_3AC, 0x3AC);
	VALIDATE(CLizMan, field_3B0, 0x3B0);
	VALIDATE(CLizMan, field_3B4, 0x3B4);
}

// @Bogus
// What stays in the exe here.
// The constructor: our CLizMan declares no virtuals at all, but the original
// vtable at 0x53B9AC overrides seven slots (dtor 0x44AD60, AI 0x451350, Hit
// 0x44BA00, CreateCombatImpactEffect 0x451AD0, TugImpulse 0x44B4F0,
// SetParamByIndex 0x44AF90, Grab 0x44B490). Stamping our vtable would fall
// back to CBaddy for all of them. LizMan_CreateLizMan and
// LizMan_RelocatableModuleInit go with it, they build (or hand out the
// pointer that builds) that same object.
// Guard and ScanNearbyNodesForJumpTarget: both walk the trig node array with
// NumNodes (0x6B4670, trig.h). Nothing in our dll ever writes NumNodes, so our
// copy is always 0 and the walk finds nothing.
// CheckFallBack: it does new CAIProc_RotY, and our CAIProc_RotY::Execute is an
// empty inline body while the exe has a real one at 0x401110 (see patch_ai).
void patch_lizman(void)
{
	PATCH_PUSH_RET(0x0044A5C0, LizMan_RelocatableModuleClear);

	PATCH_PUSH_RET_POLY(0x0044DEC0, CLizMan::FlyAcrossRoom, "?FlyAcrossRoom@CLizMan@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x0044F2E0, CLizMan::CalculateJumpPositionArray, "?CalculateJumpPositionArray@CLizMan@@QAEXPAVCVector@@@Z");
	PATCH_PUSH_RET_POLY(0x0044FFE0, CLizMan::Acknowledge, "?Acknowledge@CLizMan@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x00450270, CLizMan::RunToWhereActionIs, "?RunToWhereActionIs@CLizMan@@QAEXPAVCVector@@@Z");
	PATCH_PUSH_RET_POLY(0x00450920, CLizMan::DoLizmanPhysics, "?DoLizmanPhysics@CLizMan@@QAEXXZ");
}
