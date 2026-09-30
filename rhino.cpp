#include "rhino.h"
#include "validate.h"
#include "utils.h"
#include "panel.h"
#include "ps2pad.h"
#include "spidey.h"
#include "ps2lowsfx.h"
#include "ps2redbook.h"
#include "camera.h"
#include "ai.h"
#include "my_assert.h"
#include "m3dcolij.h"
#include "m3dzone.h"
#include "effects.h"
#include "zrhinog.h"
#include "web.h"
#include "mem.h"
#include "spool.h"
#include "chunk.h"
#include "ps2m3d.h"
#include "message.h"
// for G_SAVE_GAME. rhino.cpp used to keep its own "u8 gActuatorRelated", but
// the byte it stands for is 0x006828D3, which is gSaveGame + 0x7B (field_7B,
// the vibration option). Shell_DoShell writes it and is not hooked, so the
// reads in CRhino::ShakePad and CRhino::SetUpStuckHorn must see game memory.
#include "front.h"


// Fixed game address, confirmed by the maintainer's IDB (idb_globals.txt:
// 0x5520B8 gRhinoStrangeInitData). The constructor reads it directly from
// game memory (dword_5520B8/dword_5520BC), not from a repo-local array, so
// this must point at the real address rather than hold guessed values.
static i32 * const gRhinoStrangeInitData = reinterpret_cast<i32*>(0x005520B8);

// Stays repo-local on purpose: the initialiser below matches the exe bytes at
// 0x00552080 exactly (IDB name M3d_RhinoLight), and a scan of every function
// in tools/functions finds no write anywhere in the binary, so both copies
// hold the same constants.
#ifndef SPIDEY_STANDALONE
EXPORT SLight M3d_RhinoLight =
{
  { { -2430, -2228, -2430 }, { 2509, -2896, 1447 }, { -648, -3711, -1607 } },
  0,
  { { 4800, 1560, 3072 }, { 4080, 2400, 2880 }, { 3600, 3840, 3072 } },
  0,
  { 1800, 1800, 1440 }
};
#else
extern SLight M3d_RhinoLight;
#endif



// Fixed game address, confirmed by the maintainer's IDB (idb_globals.txt:
// 0x552208 gRhinoData). PlayXAPlease and the constructor's checksum assert
// both need the real XA track table here, not a disconnected repo array.
#define LEN_RHINO_DATA 0x17
static SRhinoData * const gRhinoData = reinterpret_cast<SRhinoData*>(0x00552208);

#define LEN_RHINO_DAZED_DATA 0x5

// The last footstep sound played, so the next one picks a different sample.
// CRhino::PlaySounds (0x481550) is the only reader and the only writer in the
// whole binary ("mov [682C4Ch],eax" at 0x4816B4, compared again in the retry
// loops above every footstep call), and the maintainer's IDB names 0x00682C4C
// gRhinoSound.
#ifndef SPIDEY_STANDALONE
EXPORT u32 gRhinoSound;
#else
extern u32 gRhinoSound;
#endif
//#define G_RHINO_SOUND (gRhinoSound)
#define G_RHINO_SOUND (*reinterpret_cast<u32*>(0x00682C4C))

// Rhino's state flag table, 0x38 bytes of i16 pairs that end exactly where
// gRhinoData starts at 0x00552208. CRhino::AI (0x4819C0) and CRhino::Hit
// (sub_47EE70) push 0x005521D0 into CBaddy::CheckStateFlags. Nothing writes
// it, but our copy has no initializer, so a hooked reader would see zeros.
#ifndef SPIDEY_STANDALONE
EXPORT SStateFlags gRhinoStateFlags;
#else
extern SStateFlags gRhinoStateFlags;
#endif
//#define G_RHINO_STATE_FLAGS (gRhinoStateFlags)
#define G_RHINO_STATE_FLAGS (*reinterpret_cast<SStateFlags*>(0x005521D0))

#include "camera.h"

// @Ok
// @Matching
void Rhino_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Rhino_RelocatableModuleClear;
	pMod->field_C[0] = Rhino_CreateRhino;
}

// @Ok
void CRhino::AI(void)
{
	if (this->field_3D0)
	{
		this->field_3D0 -= this->field_80;
		if (this->field_3D0 < 0)
		{
			this->field_3D0 = 0;
		}
	}

	if (this->field_31C.bothFlags == 2)
	{
		this->field_3D4 += this->field_80;
		if (this->field_3D4 > 0x168)
		{
			this->field_31C.bothFlags = 0x14;
			this->dumbAssPad = 0;
		}
	}
	else
	{
		this->field_3D4 = 0;
	}

	if (!this->field_3D8 && !G_MECHLIST_PLAYER->field_E18)
	{
		G_CAMERA_LIST->SetMode(CAMERAMODE_USER);
		print_if_false(1, "bad value send to BossCamSpinRate");
		G_CAMERA_LIST->field_2A4 = 0x100;
		print_if_false(1, "bad value send to BossCamStationaryRadius");
		G_CAMERA_LIST->field_2A8 = 0x100;

		gBossRelated = reinterpret_cast<i32>(this);
		this->field_3D8 = 1;
	}

	this->field_35C++;

	if (!(this->CheckStateFlags(&G_RHINO_STATE_FLAGS, 0xD) & 4))
	{
		this->DoPhysics(0);
	}

	if (this->CheckStateFlags(&G_RHINO_STATE_FLAGS, 0xD) & 0x40)
	{
		if (this->field_324)
		{
			i32 dy = this->mPos.vy - G_MECHLIST_PLAYER->mPos.vy;
			i32 sign = dy >> 31;
			i32 absDy = (dy ^ sign) - sign;

			if (absDy > 0x64000)
			{
				this->RunTimer(&this->field_324);
				if (!this->field_324)
				{
					this->field_31C.bothFlags = 0xD;
					this->dumbAssPad = 0;
				}
			}
			else
			{
				this->field_324 = 0;
			}
		}

		if (this->field_328)
		{
			this->RunTimer(&this->field_328);
			if (!this->field_328)
			{
				this->field_31C.bothFlags = 0xD;
				this->dumbAssPad = 0;
			}
		}

		if (this->field_32C)
		{
			this->RunTimer(&this->field_32C);
			if (!this->field_32C)
			{
				this->PlayXAPlease(0xF, 3, 1);
			}
		}
	}
	else
	{
		this->field_328 = 0;
		this->field_324 = 0;
	}

	if (this->pMessage)
	{
		for (CMessage *msg = this->pMessage; msg; msg = msg->mNext)
		{
			if (this->field_31C.bothFlags != 0x15 || msg->field_14 >= 0xB)
			{
				switch (msg->field_14)
				{
					case 5:
						if (this->CheckStateFlags(&G_RHINO_STATE_FLAGS, 0xD) & 0x10)
							break;
						if (this->field_31C.bothFlags != 0x10)
						{
							this->Neutralize();
							this->field_31C.bothFlags = 0x10;
							this->dumbAssPad = 0;
						}
						else if (this->field_348 & 1)
						{
							this->field_1F8 = 0;
							this->dumbAssPad = 1;
						}
						break;
					case 17:
						Effects_UnElectrify(this);
						if (this->field_338)
						{
							SFX_Stop(this->field_338);
						}
						this->field_338 = 0;
						break;
					case 14:
					{
						void *p = Mem_RecoverPointer(&this->field_104);
						if (p)
						{
							reinterpret_cast<CTrapWebEffect*>(p)->Burst();
						}
						break;
					}
				}
			}

			msg->field_10 |= 1;
		}

		this->CleanUpMessages(0, 0);
	}

	if (this->mAIProcList)
	{
		this->mAIProcList->Execute();
		this->CleanUpAIProcList(0);
	}

	this->field_348 = 0;

	if (this->CheckStateFlags(&G_RHINO_STATE_FLAGS, 0xD) & 0x10)
	{
		this->field_2A8 |= 0x10000;
	}
	else
	{
		this->field_2A8 &= ~0x10000;
	}

	if (this->field_31C.bothFlags == 0x16)
	{
		this->mCBodyFlags |= 0x10;
		this->field_310 = 0;

		if (!this->DetermineFightState(0))
		{
			this->field_31C.bothFlags = 1;
			this->dumbAssPad = 0;
		}
	}
	else if ((this->field_31C.bothFlags == 2 || this->field_31C.bothFlags == 1)
		&& !(G_ATTACK_RELATED & 3))
	{
		if (this->DetermineFightState(0))
		{
			this->field_328 = 0;
			this->field_324 = 0;
		}
		else if (!this->field_328)
		{
			this->field_328 = Utils_GetValueFromDifficultyLevel(0x2710, 0x1B58, 0x1388, 0xBB8);
		}
	}

	this->PlaySounds();

	if (this->field_31C.bothFlags)
	{
		this->SetHeight(0, 0x64, 0x258);
	}

	switch (this->field_31C.bothFlags)
	{
		case 0:
			this->RhinoInit();
			this->SetHeight(0, 0x64, 0x258);
			break;
		case 2:
			this->field_1F0 = 0;
			switch (this->dumbAssPad)
			{
				case 0:
					this->Neutralize();
					this->dumbAssPad++;
					break;
				case 1:
					if (this->mAnim)
					{
						this->PlaySingleAnim(0, 0, -1);
					}
					break;
				default:
					print_if_false(0, "Unknown substate.");
					break;
			}
			break;
		case 12:
			this->GetShocked();
			break;
		case 10:
		case 11:
			this->StuckInWall();
			break;
		case 1:
			this->FollowWaypoints();
			break;
		case 7:
		case 8:
			this->field_358 = G_ATTACK_RELATED;
			this->field_2A8 |= 0x20000000;
			this->ChargePlayer();
			break;
		case 6:
			this->field_358 = G_ATTACK_RELATED;
			this->field_2A8 |= 0x20000000;
			this->AttackPlayer();
			break;
		case 9:
			this->HitWall();
			break;
		case 14:
			this->field_358 = G_ATTACK_RELATED;
			this->TakeHit();
			break;
		case 3:
			this->Laugh();
			break;
		case 15:
			this->GetLaunched();
			break;
		case 4:
			this->field_358 = G_ATTACK_RELATED;
			this->field_2A8 |= 0x20000000;
			this->ChargePlayer();
			break;
		case 5:
			this->field_358 = G_ATTACK_RELATED;
			this->ChasePlayer(1);
			break;
		case 16:
		case 17:
			this->field_358 = G_ATTACK_RELATED;
			this->GetTrapped();
			break;
		case 18:
			this->field_358 = G_ATTACK_RELATED;

			if (!(this->field_2A8 & 8))
			{
				this->field_31C.bothFlags = 0x16;
				this->dumbAssPad = 0;
			}
			else
			{
				switch (this->dumbAssPad)
				{
					case 0:
						this->Neutralize();
						this->dumbAssPad++;
						break;
					case 1:
						if (this->mAnim)
						{
							this->PlaySingleAnim(0, 0, -1);
						}
						break;
					default:
						print_if_false(0, "Unknown substate.");
						break;
				}
			}
			break;
		case 13:
		case 20:
			this->field_358 = G_ATTACK_RELATED;
			this->StompGround();
			break;
		case 21:
			this->DieRhino();
			break;
		default:
			print_if_false(0, "Unknown state!");
			break;
	}

	if (this->mAnimFinished)
	{
		if (this->mAnim == 0)
		{
			this->PlaySingleAnim(0, 0, -1);
		}
		else if (this->mAnim == 2)
		{
			this->PlaySingleAnim(2, 0, -1);
		}
		else if (this->mAnim == 5)
		{
			this->PlaySingleAnim(5, 0, -1);
		}
	}

	if (this->field_348 & 2)
	{
		this->mTransform.t[1] = 0x8000;
	}
	else
	{
		this->mTransform.t[1] = 0;
	}

	M3d_BuildTransform(this);
	this->DoMGSShadow();
	this->DoDazedEffect();

	if ((this->mAnim == 0 && this->mFrame >= 0x15 && this->mFrame <= 0x26)
		|| (this->mAnim == 9 && this->mFrame >= 0 && this->mFrame <= 9)
		|| (this->mAnim == 0xF && this->mFrame >= 1 && this->mFrame <= 0xC))
	{
		if (!(this->field_218 & 4))
		{
			SFX_PlayPos(((G_ATTACK_RELATED & 1) == 0 ? 1 : 0) | 0x8040, &this->mPos, 0);
			this->field_218 |= 4;
		}

		CVector pos0, vel0, pos1, vel1;
		SHook hook;

		// First puff (left nostril)
		hook.Part.vx = -32;
		hook.Part.vy = 128;
		hook.Part.vz = -640;
		hook.Offset = 15;
		M3dUtils_GetDynamicHookPosition((VECTOR*)&pos0, this, &hook);
		hook.Part.vx = -48;
		hook.Part.vy += 48;
		hook.Part.vz -= 32;
		M3dUtils_GetDynamicHookPosition((VECTOR*)&vel0, this, &hook);
		vel0 -= pos0;

		new CRhinoNasalSteam(&pos0, &vel0);

		// Second puff (right nostril)
		hook.Part.vx = 32;
		hook.Part.vy = 128;
		hook.Part.vz = -640;
		hook.Offset = 15;
		M3dUtils_GetDynamicHookPosition((VECTOR*)&pos1, this, &hook);
		hook.Part.vx = 48;
		hook.Part.vy += 48;
		hook.Part.vz -= 32;
		M3dUtils_GetDynamicHookPosition((VECTOR*)&vel1, this, &hook);
		vel1 -= pos1;

		new CRhinoNasalSteam(&pos1, &vel1);
	}
}

// @Ok
// Verified field-by-field and branch-by-branch against the disasm (0x480170):
// case 0 (Neutralize + look at player), case 1's field_330 timer with the
// SFX_PlayPos(0x80C8-ish random) call, the field_360 sub-state dispatch, and
// case 2's inlined CheckIfPlayerHit body (SHitInfo build, mPos delta via
// operator-, Hit call) all match. The original dispatches field_360 with an
// if/else-if chain, not a switch; both compile to the same behavior.
void CRhino::AttackPlayer(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_310 = 0x64;
			this->Neutralize();

			new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 2, 0x50, 0);

			this->field_330 = Rnd(30) + 0x5A;
			this->dumbAssPad++;
			break;
		case 1:
		{
			this->field_330 -= this->field_80;

			if (this->field_288 & 2)
			{
				this->field_288 &= ~2;
			}
			else
			{
				if (this->field_330 > 0)
				{
					return;
				}
			}

			this->field_330 = 0;

			SFX_PlayPos((~G_ATTACK_RELATED & 1) | 0x80C8, &this->mPos, 0);

			switch (this->field_360)
			{
				case 0:
					this->PlaySingleAnim(0xA, 0, -1);
					new CAIProc_MonitorAttack(this, 7, 0x7000, 6, 0x10);
					this->dumbAssPad = 2;
					this->field_360 = 1;
					break;
				case 1:
					this->PlaySingleAnim(0xB, 0, -1);
					new CAIProc_MonitorAttack(this, 7, 0xE00, 6, 0x10);
					this->dumbAssPad = 2;
					this->field_360 = 0;
					break;
				default:
					print_if_false(0, "What in the name of God above?");
					break;
			}
			break;
		}
		case 2:
			if (this->CheckIfPlayerHit())
			{
				this->dumbAssPad++;
			}
			else if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		case 3:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 3;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// Verified case-by-case against the disasm (0x47F800). Fixed two real bugs
// found by comparing against the ground truth: (1) case 2's early-wait
// check had the condition inverted (must return/wait while field_288 bit 2
// is clear AND field_330 is still positive, not the reverse); (2) case 5's
// gNumDomes branch was swapped (gNumDomes != 0 is the small-radius "already
// impaled" case that sets bothFlags=9, gNumDomes == 0 is the full
// SHitInfo/Hit-call case, not the other way around), and its final
// mAnimFinished-or-close-enough branch was missing the actual state
// transition entirely (was just an early "break", but the original sets
// field_330=10, dumbAssPad=0xA and conditionally calls PlayXAPlease(0,3,1)
// there instead of doing nothing).
void CRhino::ChargePlayer(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_310 = 0x64;
			this->Neutralize();

			new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 2, 0x50, 0);

			this->field_330 = Rnd(30) + 0x5A;

			if (this->field_31C.bothFlags == 8)
			{
				this->dumbAssPad = 2;
			}
			else
			{
				this->PlaySingleAnim(9, 0, -1);
				SFX_PlayPos(0x8049, &this->mPos, 0);
				SFX_PlayPos(0x804A, &this->mPos, 0);
				this->field_334 = Rnd(30) + 0x3C;
				this->dumbAssPad++;
			}
			break;
		case 1:
			this->RunTimer(&this->field_334);
			this->DoPhysics(0);

			if (!this->mAnimFinished)
				break;

			if (Utils_GetValueFromDifficultyLevel(1, 1, 0, 0) == 0
				&& !this->LineOfSightCheck(&G_MECHLIST_PLAYER->mPos, 1))
			{
				this->field_31C.bothFlags = 0x16;
				this->dumbAssPad = 0;
				break;
			}

			if (this->field_334)
			{
				this->PlaySingleAnim(9, 0, -1);
				SFX_PlayPos(0x8049, &this->mPos, 0);
			}
			else
			{
				this->dumbAssPad++;
			}
			break;
		case 2:
			this->DoPhysics(0);
			this->field_330 -= this->field_80;

			if (this->field_288 & 2)
			{
				this->field_288 &= ~2;
			}
			else if (this->field_330 > 0)
			{
				return;
			}

			this->field_330 = 0;

			if (G_MECHLIST_PLAYER->field_AD4)
			{
				this->field_31C.bothFlags = 0xD;
				this->dumbAssPad = 0;
				break;
			}

			this->PlaySingleAnim(4, 0, -1);
			SFX_PlayPos(0x8042, &this->mPos, 0);
			this->field_31C.bothFlags = 8;
			this->dumbAssPad++;
			break;
		case 3:
			if (!this->mAnimFinished)
				break;

			this->PlaySingleAnim(5, 0, -1);

			new CAIProc_AccZ(this,
				Utils_GetValueFromDifficultyLevel(0x50, 0x50, 0x50, 0x50),
				-Utils_GetValueFromDifficultyLevel(500, 500, 500, 600),
				8);

			this->field_218 &= ~2;
			this->field_334 = Utils_CrapXZDist(this->mPos, G_MECHLIST_PLAYER->mPos);
			this->dumbAssPad = 5;
			break;
		case 5:
			if (this->GonnaHitWall(1) & 0xD)
				break;

			if (this->mAnimFinished && (this->mAnim == 5 || this->mAnim == 0xC))
			{
				this->PlaySingleAnim(5, 0, -1);
			}

			{
				i32 dist = Utils_CrapXZDist(this->mPos, G_MECHLIST_PLAYER->mPos);

				if (dist < Utils_GetValueFromDifficultyLevel(0x258, 0x190, 0x154, 0x12C))
				{
					this->MarkAIProcList(0, 0x100, 0);
					this->mAngVel.vy = 0;
					this->mAngAcc.vy = 0;
				}

				CVector unused(0, 0, 0);

				if (gNumDomes)
				{
					if (M3dColij_LineToSphere(&this->field_2FC, &this->mPos, &unused, G_MECHLIST_PLAYER, 0, 0x2AF8))
					{
						this->field_31C.bothFlags = 9;
						this->dumbAssPad = 0;
						return;
					}
				}
				else if (M3dColij_LineToSphere(&this->field_2FC, &this->mPos, &unused, G_MECHLIST_PLAYER, 0, 0x1800))
				{
					SHitInfo hit;
					hit.field_C = G_MECHLIST_PLAYER->mPos - this->mPos;
					hit.field_0 = 0xE;
					hit.field_4 = 0xC;
					hit.field_8 = 0x1E;

					G_MECHLIST_PLAYER->Hit(&hit);

					this->PlaySingleAnim(0xC, 0, -1);
					this->field_218 |= 1;
					this->MarkAIProcList(0, 0x100, 0);
					this->dumbAssPad = 0xA;
					this->field_330 = 5;
					this->field_344 = G_ATTACK_RELATED;
					return;
				}

				if (this->field_288 & 8)
				{
					this->field_288 &= ~8;
					this->field_218 |= 2;
				}

				if (this->mAnimFinished || ((this->field_218 & 2) && this->field_334 < dist))
				{
					this->field_330 = 10;

					if (this->field_334 < 300)
					{
						this->PlayXAPlease(0, 3, 1);
					}

					this->dumbAssPad = 0xA;
					this->field_218 &= ~1;
				}
				else
				{
					this->field_330 += this->field_80;
					this->field_334 = dist;

					if (this->field_330 >= 0x12C)
					{
						this->PlaySingleAnim(8, 0, -1);
						new CAIProc_AccZ(this, 0x50, 0, 8);
						this->dumbAssPad = 7;
					}
				}
			}
			break;
		case 7:
			if (this->GonnaHitWall(1) & 0xD)
				break;

			if (!(this->field_288 & 8))
				break;

			this->field_288 &= ~8;
			this->Neutralize();
			this->mCBodyFlags |= 0x10;
			this->field_31C.bothFlags = 2;
			this->dumbAssPad = 0;
			break;
		case 10:
			if (this->GonnaHitWall(1) & 0xD)
				break;

			this->field_330 -= this->field_80;

			if (this->field_330 > 0)
				break;

			this->PlaySingleAnim(8, 0, -1);
			new CAIProc_AccZ(this, 0x50, 0, 8);
			this->dumbAssPad++;
			break;
		case 11:
			if (this->GonnaHitWall(1) & 0xD)
				break;

			if (!(this->field_288 & 8))
				break;

			this->field_288 &= ~8;

			if (!(this->field_218 & 1))
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
				break;
			}

			this->Neutralize();
			new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 2, 0x1E, 0);
			this->PlaySingleAnim(0x14, 0, -1);
			SFX_PlayPos(((G_ATTACK_RELATED & 1) == 0 ? 1 : 0) | 0x8046, &this->mPos, 0);
			this->mCBodyFlags |= 0x10;
			this->dumbAssPad++;
			break;
		case 12:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @Matching
void CRhino::ChasePlayer(i32 a2)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_310 = 0x64;
			this->Neutralize();

			new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 2, 0x50, 0);

			this->field_330 = 0;
			this->dumbAssPad++;
			break;
		case 1:
			this->DoPhysics(0);

			if (this->field_288 & 2)
			{
				this->field_288 &= ~2;

				new CAIProc_AccZ(this, 0xF0, -0x28, 0);
				this->PlaySingleAnim(1, 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 2:
			if (this->GonnaHitWall(0) & 0xD)
			{
				this->Neutralize();
				this->field_31C.bothFlags = 0x16;
				this->dumbAssPad = 0;
			}
			else
			{
				if (this->mAnim == 1 && this->mAnimFinished)
				{
					this->PlaySingleAnim(2, 0, -1);
				}

				if (!this->LineOfSightCheck(&G_MECHLIST_PLAYER->mPos, 1))
				{
					this->field_31C.bothFlags = 0x16;
					this->dumbAssPad = 0;
				}

				i32 dist2 = Utils_CrapXZDist(this->mPos, G_MECHLIST_PLAYER->mPos);

				if (dist2 < (a2 == 2 ? 5000 : 200))
				{
					new CAIProc_AccZ(this, 0xA0, 0, 8);
					this->PlaySingleAnim(3, 0, -1);
					this->dumbAssPad++;
				}
			}
			break;
		case 3:
			if (this->mAnimFinished && this->mAnim == 3)
			{
				this->PlaySingleAnim(0, 0, -1);
			}

			if (this->GonnaHitWall(0))
			{
				this->Neutralize();
				goto setChaseFlag2;
			}
			else if (this->field_288 & 8)
			{
				this->field_288 &= ~8;
				this->Neutralize();

				i32 dist = Utils_CrapXZDist(this->mPos, G_MECHLIST_PLAYER->mPos);

				if (dist < 0xC8)
				{
					if (G_MECHLIST_PLAYER->field_AD4)
					{
						this->field_31C.bothFlags = 0xD;
						this->dumbAssPad = 0;
					}
					else if (G_MECHLIST_PLAYER->field_E1C & 0x800000)
					{
						this->RunAnim(this->field_298.Bytes[0], 0, -1);
						this->dumbAssPad++;
					}
					else
					{
						this->field_31C.bothFlags = 6;
						this->dumbAssPad = 0;
					}
				}
				else if (dist <= 0x1F4)
				{
					goto setChaseFlag2;
				}
				else
				{
					this->field_31C.bothFlags = 7;
					this->dumbAssPad = 0;
				}
			}
			break;
		case 4:
			if (this->mAnimFinished)
			{
setChaseFlag2:
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}

	this->field_330 += this->field_80;

	if (a2 == 1 && this->field_330 >= 0x3C)
	{
		this->field_31C.bothFlags = 7;
		this->dumbAssPad = 0;
	}
}

// @Ok
// Confirmed inlined into AttackPlayer's case 2 on PC (0x480170): the same
// field_288 bit-0x10 test, SHitInfo build (14, 11, MechList->mPos-mPos, 15)
// and Hit call appear directly there, matching this body exactly.
INLINE i32 CRhino::CheckIfPlayerHit(void)
{
	i32 v4;

	if (this->field_288 & 0x10)
	{
		this->field_288 &= 0xFFFFFFEF;
		v4 = 1;
	}
	else
	{
		v4 = 0;
	}

	if (v4)
	{
		SHitInfo v9;

		v9.field_0 = 14;
		v9.field_4 = 11;

		v9.field_C = G_MECHLIST_PLAYER->mPos - this->mPos;
		this->field_344 = G_ATTACK_RELATED;
		v9.field_8 = 15;

		if (G_MECHLIST_PLAYER->Hit(&v9))
		{
			return 1;
		}
	}

	return 0;
}

// @Ok
// @Matching
void CRhino::DieRhino(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			SFX_PlayPos(0x8043, &this->mPos, 0);
			this->field_310 = 0;
			this->Neutralize();
			this->mCBodyFlags &= ~0x10;
			this->field_2A8 |= 0x1000;
			this->field_1F8 = 0;
			this->StopMyXA();
			this->PlaySingleAnim(0x1D, 0, -1);
			this->PlayXAPlease(0x16, 1, 0);
			this->dumbAssPad++;

			G_MECHLIST_PLAYER->SetIgnoreInputTimer(0x8000);

			{
				CCamera *camera = G_CAMERA_LIST;
				if (camera)
				{
					camera->SetMode(CAMERAMODE_DEMO);
					camera->field_100 = 1;
					camera->mTripod = this;
					camera->field_140 = 1;
					camera->field_13C = this;
					camera->SetCamXOffset(0, 0);
					camera->SetCamYOffset(0, 0);
					camera->SetCamZOffset(0, 0);
					camera->SetCamXZDistance(0x1A8, 0);
					camera->SetCamYDistance(-0x68, 0);
				}
			}
			break;
		case 1:
			if (G_CAMERA_LIST)
			{
				i16 angle = this->field_80;
				angle <<= 5;
				angle += G_CAMERA_LIST->field_236;
				G_CAMERA_LIST->SetCamAngle(angle, 16);
			}

			if (this->mAnimFinished)
			{
				this->dumbAssPad = 4;
			}
			break;
		case 3:
			break;
		case 4:
			this->field_1F8 += this->field_80;
			if (this->field_1F8 > 0x3C)
			{
				this->Die(0);
				this->dumbAssPad = 3;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

static i16 * const gRhinoDazedStarAngle = reinterpret_cast<i16*>(0x00552070);
static i16 * const gRhinoDazedStarSpeed = reinterpret_cast<i16*>(0x00682B64);

// rcossin_tbl, the shared fixed point sin/cos table (repo-wide convention,
// see bit.cpp/camera.cpp/manipob.cpp/mysterio.cpp/quat.cpp/shell.cpp/
// spidey.cpp, confirmed by the maintainer's IDB as 0x610C48). word_610C4A
// is the same table offset by one i16 (the cos half of each interleaved
// sin/cos pair), same idiom as mysterio.cpp.
static i16 * const word_610C48 = reinterpret_cast<i16*>(0x00610C48);
static i16 * const word_610C4A = reinterpret_cast<i16*>(0x00610C4A);

// @Ok
// Rewritten from a fresh IDA decompile+disasm of 0x480480 (2026-08-31); the
// previous draft's "3 independent loops, each managing its own array" shape
// was wrong. Real structure, confirmed instruction by instruction:
// - Whole function gated on mAnim==0x11 || mAnim==0x12 (doFirst) ||
//   field_3E4[0]!=0. field_3E4/field_3F8/field_40C are contiguous CFT4Bit*
//   arrays of 5 (offsets 0x3E4/0x3F8/0x40C, 0x14 bytes apart each).
// - Loop over field_40C runs only if field_3F8[0]!=0; each slot's SetPos
//   reads directly from field_3F8[i]->mPos (CBit::mPos sits at offset 0x10,
//   confirmed via the disasm's `[esi-0x14]+0x10` address math), not this
//   object's own array. So field_40C trails field_3F8's position.
// - Loop over field_3F8 runs only if field_3E4[0]!=0, same shape, trails
//   field_3E4->mPos. So field_3F8 trails field_3E4's position.
// - Loop over field_3E4 has no extra gate (always runs once the outer gate
//   passed). Each slot computes a star-orbit hook offset from
//   gRhinoDazedStarAngle[i]/gRhinoDazedStarSpeed[i] via rcossin_tbl, calls
//   M3dUtils_GetDynamicHookPosition, and SetPos's the raw result (no extra
//   post-call offset, unlike the previous draft).
// - All three loops share the SAME doFirst flag for alloc-vs-fade (there is
//   no separate "doSecond"); the fade branch condition was inverted in the
//   previous draft too: delete when mFrame<=4, else SetTint(mFrame-4,
//   (mFrame-4)>>1, 0) (not a constant color, and not mFrame>4 for delete).
// - Loop3's alloc path also differs from the other two: SetScale(0xC0),
//   SetAnimSpeed(0) (not SetTransparency(0x40)/SetAnimSpeed(0x80)),
//   SetSemiTransparent(), SetTint(0x80,0x40,0).
void CRhino::DoDazedEffect(void)
{
	i16 anim = this->mAnim;
	bool doFirst = anim == 0x11 || anim == 0x12;
	i32 i;

	if (!doFirst && this->field_3E4[0] == 0)
		return;

	if (this->field_3F8[0] != 0)
	{
		for (i = 0; i < 5; i++)
		{
			CFT4Bit *&p = reinterpret_cast<CFT4Bit*&>(this->field_40C[i]);

			if (doFirst)
			{
				if (!p)
				{
					p = new CFT4Bit();
					p->SetAnim(0);
					p->SetScale(0x80);
					p->SetSemiTransparent();
					p->SetTint(0x40, 0x20, 0);
				}
			}
			else if (p)
			{
				if (p->mFrame <= 4)
				{
					delete p;
					p = 0;
				}
				else
				{
					u8 fade = p->mFrame - 4;
					p->SetTint(fade, fade >> 1, 0);
				}
			}

			if (p)
			{
				CFT4Bit *neighbor = reinterpret_cast<CFT4Bit*&>(this->field_3F8[i]);
				p->SetPos(neighbor->mPos);
			}
		}
	}

	if (this->field_3E4[0] != 0)
	{
		for (i = 0; i < 5; i++)
		{
			CFT4Bit *&p = reinterpret_cast<CFT4Bit*&>(this->field_3F8[i]);

			if (doFirst)
			{
				if (!p)
				{
					p = new CFT4Bit();
					p->SetAnim(0);
					p->SetScale(0xA0);
					p->SetSemiTransparent();
					p->SetTint(0x60, 0x30, 0);
				}
			}
			else if (p)
			{
				if (p->mFrame <= 4)
				{
					delete p;
					p = 0;
				}
				else
				{
					u8 fade = p->mFrame - 4;
					p->SetTint(fade, fade >> 1, 0);
				}
			}

			if (p)
			{
				CFT4Bit *neighbor = reinterpret_cast<CFT4Bit*&>(this->field_3E4[i]);
				p->SetPos(neighbor->mPos);
			}
		}
	}

	for (i = 0; i < 5; i++)
	{
		CFT4Bit *&p = reinterpret_cast<CFT4Bit*&>(this->field_3E4[i]);

		i16 oldAngle = gRhinoDazedStarAngle[i];
		i16 sinA = word_610C48[2 * (oldAngle & 0xFFF)];
		i16 cosA = word_610C4A[2 * (oldAngle & 0xFFF)];
		i16 sinSpeed = word_610C48[2 * (gRhinoDazedStarSpeed[i] & 0xFFF)];

		SHook hook;
		hook.Part.vx = (768 * sinA) >> 12;
		hook.Part.vy = ((sinSpeed << 7) >> 12) - 320;
		hook.Part.vz = ((768 * cosA) >> 12) - 416;
		hook.Offset = 0xF;

		gRhinoDazedStarAngle[i] = (oldAngle + (this->field_80 << 6)) & 0xFFF;
		gRhinoDazedStarSpeed[i] = (gRhinoDazedStarSpeed[i] + Rnd(64) + 64) & 0xFFF;

		VECTOR hookPos = {0, 0, 0, 0};
		M3dUtils_GetDynamicHookPosition(&hookPos, this, &hook);

		if (doFirst)
		{
			if (!p)
			{
				p = new CFT4Bit();
				p->SetAnim(0);
				p->SetScale(0xC0);
				p->SetAnimSpeed(0);
				p->SetSemiTransparent();
				p->SetTint(0x80, 0x40, 0);
			}
		}
		else if (p)
		{
			if (p->mFrame <= 4)
			{
				delete p;
				p = 0;
			}
			else
			{
				u8 fade = p->mFrame - 4;
				p->SetTint(fade, fade >> 1, 0);
			}
		}

		if (p)
			p->SetPos(*reinterpret_cast<CVector*>(&hookPos));
	}
}

// @Ok
void CRhino::DoMGSShadow(void)
{
	CVector points[4];
	SHook h;

	h.Part.vx = 0; h.Part.vy = 0; h.Part.vz = 0;
	h.Offset = 3;
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&points[0]), this, &h);

	h.Offset = 6;
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&points[1]), this, &h);

	h.Offset = 0xB;
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&points[2]), this, &h);

	h.Offset = 0xE;
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&points[3]), this, &h);

	CVector plane(0, this->field_21E << 12, 0);
	points[0] -= this->mPos;
	points[1] -= this->mPos;
	points[2] -= this->mPos;
	points[3] -= this->mPos;

	MATRIX localMtx;
	M3dMaths_TransposeMatrix1(&this->mTransform, &localMtx);
	gte_SetRotMatrix(&localMtx);

	i32 maxX = 0x20, maxZ = 0x40, minX = -0x20, minZ = -0x40;
	i32 i;

	for (i = 0; i < 4; i++)
	{
		points[i] >>= 12;
		gte_ldlvl(reinterpret_cast<VECTOR*>(&points[i]));
		gte_rtir();
		gte_stlvnl(reinterpret_cast<VECTOR*>(&points[i]));

		if (points[i].vx > maxX) maxX = points[i].vx;
		else if (points[i].vx < minX) minX = points[i].vx;

		if (points[i].vz > maxZ) maxZ = points[i].vz;
		else if (points[i].vz < minZ) minZ = points[i].vz;
	}

	plane >>= 12;
	gte_ldlvl(reinterpret_cast<VECTOR*>(&plane));
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&plane));

	points[0].vy = plane.vy;
	points[1].vy = plane.vy;
	points[2].vy = plane.vy;
	points[3].vy = plane.vy;
	points[0].vx = minX; points[0].vz = maxZ;
	points[1].vx = minX; points[1].vz = minZ;
	points[2].vx = maxX; points[2].vz = maxZ;
	points[3].vx = maxX; points[3].vz = minZ;

	gte_SetRotMatrix(&this->mTransform);
	for (i = 0; i < 4; i++)
	{
		gte_ldlvl(reinterpret_cast<VECTOR*>(&points[i]));
		gte_rtir();
		gte_stlvnl(reinterpret_cast<VECTOR*>(&points[i]));
		points[i] <<= 12;
		points[i] += this->mPos;
	}

	if (!this->field_3E0)
	{
		this->field_3E0 = reinterpret_cast<u32>(new CQuadBit());
		CQuadBit *shadow = reinterpret_cast<CQuadBit*>(this->field_3E0);
		shadow->SetTexture(0, 0);
		shadow->mFrigDeltaZ = 0x20;
		shadow->SetSemiTransparent();
		shadow->SetSubtractiveTransparency();
	}

	CQuadBit *shadow = reinterpret_cast<CQuadBit*>(this->field_3E0);
	shadow->SetTransparency(0x40);
	shadow->SetCorners(points[0], points[1], points[2], points[3]);
}

// @Ok
// would love to remove the goto
void CRhino::FollowWaypoints(void)
{
	i32 v6;
	SMoveToInfo v8;
	v8.field_0.vx = 0;
	v8.field_0.vy = 0;
	v8.field_0.vz = 0;

	this->RunAppropriateAnim();

	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->field_310 = 0;

			if (this->field_1F0)
			{
				this->field_2A8 &= ~0x10000000;
				v8.field_0 = this->field_1A8[this->field_1F0];

				v8.field_C = 40;
				v8.field_10 = 40;
				v8.field_14 = 500;

				new CAIProc_MoveTo(this, &v8, 1);
				this->dumbAssPad++;
			}
			else
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		case 1:

			if (this->field_288 & 1)
			{
				this->field_288 &= ~1;
				v6 = 1;
			}
			else
			{
				v6 = 0;
			}

			if (v6)
			{
				if (this->field_1F0)
				{
					this->field_1F0--;
					// @FIXME this makes it match but i don't like it
					goto force_match;
				}
				else if (this->GetNextWaypoint())
				{
force_match:
					if ((this->field_2F0 & 2) == 0)
					{
						this->dumbAssPad = 0;
					}
				}
				else
				{
					this->field_31C.bothFlags = 2;
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
void CRhino::GetLaunched(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->dumbAssPad = 1;
			this->PlaySingleAnim(15, 0, -1);

			new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 2, 80, 0);
			this->field_230 = Utils_GetValueFromDifficultyLevel(40, 30, 21, 21);

		case 1:
			this->DoPhysics(1);
			this->RunTimer(&this->field_230);
			if (!this->field_1F8)
			{
				this->dumbAssPad++;
				this->mVel.vx = 0;
				this->mVel.vy = 0;
				this->mVel.vz = 0;
			}

			break;
		case 2:
			this->RunTimer(&this->field_230);
			if (!this->field_230)
			{
				if (this->DetermineFightState(1))
				{
					if (this->DistanceToPlayer(0) > 500)
					{
						if (this->field_31C.bothFlags == 5 || this->field_31C.bothFlags == 4)
						{
								this->field_31C.bothFlags = 8;
								this->dumbAssPad = 0;
						}
					}
					else if (this->field_31C.bothFlags == 8)
					{
						this->field_31C.bothFlags = 5;
						this->dumbAssPad = 0;
					}
				}
				else
				{
					this->PlaySingleAnim(0, 0, -1);
					this->field_31C.bothFlags = 22;
					this->dumbAssPad = 0;
				}
			}

			break;
		default:
			print_if_false(0, "Unknown substate");
			break;
	}
}

// @Ok
// @Matching
void CRhino::GetShocked(void)
{
	this->field_3D0 = 0x1E;

	switch (this->dumbAssPad)
	{
		case 0:
			this->mCBodyFlags &= ~0x10;
			this->field_348 |= 2;

			Effects_Electrify(this);
			new CAIProc_StateSwitchSendMessage(this, 0x11);

			if (!this->field_338)
			{
				this->field_338 = SFX_PlayPos(0x80CC, &this->mPos, 0);
			}

			this->CycleAnim(0x1E, 1);

			this->field_34C = Utils_GetValueFromDifficultyLevel(300, 300, 300, 300);
			this->field_420 = (Utils_GetValueFromDifficultyLevel(250, 175, 125, 75) << 12) / this->field_34C;
			this->field_354 = this->mHealth;

			if (Rnd(4))
			{
				this->PlayXAPlease(0xB, 1, 0);
			}
			else
			{
				this->PlayXAPlease(0xC, 3, 0);
			}

			this->dumbAssPad++;
			break;
		case 1:
		{
			this->RunTimer(&this->field_34C);
			this->field_348 |= 2;

			i32 damage = this->GetShockDamage();
			i16 v = (this->field_420 * this->field_34C) >> 0xC;
			v += this->field_354;
			this->mHealth = v - damage;

			if (!this->field_34C)
			{
				this->RunAnim(0x1E, this->mFrame, -1);
			}

			if (this->mAnimFinished)
			{
				if (this->field_338)
				{
					SFX_Stop(this->field_338);
				}
				this->field_338 = 0;

				if (this->mHealth <= 0)
				{
					this->field_31C.bothFlags = 0x15;
					this->dumbAssPad = 0;
				}
				else
				{
					this->mCBodyFlags |= 0x10;
					this->CycleAnim(this->field_298.Bytes[0], 1);
					this->PlayXAPlease(6, 3, 1);
					this->field_31C.bothFlags = 2;
					this->dumbAssPad = 0;
				}
			}
			break;
		}
	}
}

// @Ok
// Verified field-by-field and branch-by-branch against the disasm
// (0x480A40). Fixed one real bug: case 2 was missing "this->field_348 |= 1"
// (the original sets it unconditionally at the top of case 2, same as
// cases 1 and 3). Mem_RecoverPointer(&this->field_104)'s pointee (+0x44
// then +0x3C) matches the disasm's double indirection exactly; the real
// struct/class name is still unknown, kept as raw casts.
void CRhino::GetTrapped(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			new CAIProc_StateSwitchSendMessage(this, 0xE);
			this->RunAnim(0x1A, 0, -1);
			this->field_1F8 = 5;
			this->field_34C = 0;
			this->field_350 = 0;
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
			{
				this->CycleAnim(0x1B, 1);
			}

			this->field_348 |= 1;

			if (this->field_350 > 0)
			{
				this->field_350--;
			}

			this->field_1F8--;

			if (this->field_1F8 <= 0)
			{
				void *p = Mem_RecoverPointer(&this->field_104);

				if (!p)
				{
					goto common_inc;
				}

				{
					char *inner = *reinterpret_cast<char**>(static_cast<char*>(p) + 0x44);
					i32 v = *reinterpret_cast<i32*>(inner + 0x3C);

					if (v == this->field_34C)
					{
						goto common_inc;
					}

					this->field_350 += ((v - this->field_34C) * 125 * 32) >> 0xC;
					this->field_1F8 = 5;
					this->field_34C = v;
				}
			}
			break;
		case 2:
			this->field_348 |= 1;
			this->RunTimer(&this->field_350);

			if (this->field_350 <= 0)
			{
				this->RunAnim(0x1B, this->mAnim == 0x1B ? this->mFrame : 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 3:
			this->field_348 |= 1;

			if (this->mAnimFinished)
			{
				this->RunAnim(0x1C, 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 4:
			if (this->mFrame < 0xA)
			{
				this->field_348 |= 1;
				break;
			}

			if (this->field_104.pWhatever)
			{
				void *p = Mem_RecoverPointer(&this->field_104);
				if (p)
				{
					reinterpret_cast<CTrapWebEffect*>(p)->Burst();
				}
				this->field_104.pWhatever = 0;
			}

			this->field_31C.bothFlags = 0x11;
			goto common_inc;
		case 5:
			if (this->mAnimFinished)
			{
				if (this->DetermineFightState(1))
				{
					if (this->field_31C.bothFlags == 5 || this->field_31C.bothFlags == 4)
					{
						if (this->DistanceToPlayer(0) > 500)
						{
							this->field_31C.bothFlags = 8;
							this->dumbAssPad = 0;
						}
					}
				}
				else
				{
					this->PlaySingleAnim(0, 0, -1);
					this->field_31C.bothFlags = 0x16;
					this->dumbAssPad = 0;
				}
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
	return;

common_inc:
	this->dumbAssPad++;
}

// Fixed game addresses for the four wall-checksum allow-lists, confirmed by
// reading the exe's own data segment directly (arrays are packed back to
// back with their counts, exact bounds, no guessing): chunk1 at 0x55ACF8
// (count 8 at 0x55AD58), chunk0 at 0x55AD18 (count 3 at 0x55AD5C), chunk2 at
// 0x55AD24 (count 8 at 0x55AD54), chunk3 at 0x55AD44 (count 4 at 0x55AD60).
static i32 * const gRhinoWallChunk1 = reinterpret_cast<i32*>(0x0055ACF8);
static i32 * const gRhinoWallChunk1Count = reinterpret_cast<i32*>(0x0055AD58);
static i32 * const gRhinoWallChunk0 = reinterpret_cast<i32*>(0x0055AD18);
static i32 * const gRhinoWallChunk0Count = reinterpret_cast<i32*>(0x0055AD5C);
static i32 * const gRhinoWallChunk2 = reinterpret_cast<i32*>(0x0055AD24);
static i32 * const gRhinoWallChunk2Count = reinterpret_cast<i32*>(0x0055AD54);
static i32 * const gRhinoWallChunk3 = reinterpret_cast<i32*>(0x0055AD44);
static i32 * const gRhinoWallChunk3Count = reinterpret_cast<i32*>(0x0055AD60);

// @Ok
// Rewritten from the disasm (0x47E8A0); the earlier draft had several real
// bugs, not just residue: StartCoords must be the pre-physics field_2FC
// snapshot, not the post-DoPhysics mPos; MinCoords/MaxCoords must be zeroed,
// not set to field_2FC; the a2 parameter (not lineInfo fields) gates every
// health/reaction branch; mPos must be rolled back to field_2FC before the
// chunk1-3 reactions (the original does this once, right after the chunk0
// loop); chunk2 was missing its Chunk_ChunkItemByChecksum call and its a2
// gate; the type-401 (barrel-ish) branch's health reaction was inverted
// (Neutralize+bothFlags=9 belongs to the mHealth>0 case, bothFlags=0x15
// belongs to mHealth<=0); chunk3 and the no-match tail must return 1 (not
// 8) once mHealth drops to 0 or below. The vtable call at slot 5 is
// CBaddy::PlayerIsVisible (confirmed by baddy.cpp's own
// VALIDATE_VTABLE(CBaddy, PlayerIsVisible, 5)), same as FuckUpSomeBarrels
// and StompGround's identical barrel loop. Residue: VectorNormal is
// declared void in ps2funcs.h but the original checks its return value to
// skip the whole probe on a degenerate (zero-length) direction; not
// reproduced since fixing the signature is a repo-wide header change.
i32 CRhino::GonnaHitWall(i32 a2)
{
	this->field_2FC = this->mPos;
	this->DoPhysics(0);

	CVector delta = this->mPos - this->field_2FC;
	CVector dir = delta >> 12;
	dir.vy = 0;

	VectorNormal(reinterpret_cast<VECTOR*>(&dir), reinterpret_cast<VECTOR*>(&dir));

	CVector probe = this->mPos + (dir / 0x80);

	SLineInfo lineInfo;

	lineInfo.StartCoords = this->field_2FC;
	lineInfo.EndCoords = probe;
	lineInfo.MinCoords.vx = 0;
	lineInfo.MinCoords.vy = 0;
	lineInfo.MinCoords.vz = 0;
	lineInfo.MaxCoords.vx = 0;
	lineInfo.MaxCoords.vy = 0;
	lineInfo.MaxCoords.vz = 0;
	lineInfo.Position.vx = 0;
	lineInfo.Position.vy = 0;
	lineInfo.Position.vz = 0;
	lineInfo.Normal.vx = 0;
	lineInfo.Normal.vy = 0;
	lineInfo.Normal.vz = 0;

	M3dColij_InitLineInfo(&lineInfo);
	M3dZone_LineToItem(&lineInfo, 1);

	if (!lineInfo.pItem)
	{
		return 0;
	}

	if ((lineInfo.pItem->mFlags & 0x10) && lineInfo.pItem->mType == 0x191
		&& lineInfo.pItem != *reinterpret_cast<CItem**>(reinterpret_cast<u8*>(G_MECHLIST_PLAYER) + 0xE48))
	{
		reinterpret_cast<CBaddy*>(lineInfo.pItem)->PlayerIsVisible();

		if (a2 == 0)
		{
			return 1;
		}

		this->mHealth -= 100;

		if (this->mHealth <= 0)
		{
			this->field_31C.bothFlags = 0x15;
			this->dumbAssPad = 0;
		}
		else
		{
			this->Neutralize();
			this->mCBodyFlags |= 0x10;
			this->field_31C.bothFlags = 9;
			this->dumbAssPad = 0;
		}

		return 1;
	}

	u32 checksum = Spool_GetModelChecksum(lineInfo.pItem);
	i32 i;

	for (i = 0; i < *gRhinoWallChunk0Count; i++)
	{
		if (checksum == static_cast<u32>(gRhinoWallChunk0[i]))
		{
			Chunk_ChunkItemByChecksum(checksum);
			return 2;
		}
	}

	this->mPos = this->field_2FC;

	for (i = 0; i < *gRhinoWallChunk1Count; i++)
	{
		if (checksum == static_cast<u32>(gRhinoWallChunk1[i]))
		{
			Chunk_ChunkItemByChecksum(checksum);

			if (a2 == 0)
			{
				return 1;
			}

			this->mHealth -= 100;

			if (this->mHealth <= 0)
			{
				this->field_31C.bothFlags = 0x15;
				this->dumbAssPad = 0;
			}
			else
			{
				this->Neutralize();
				this->mCBodyFlags |= 0x10;
				this->field_31C.bothFlags = 9;
				this->dumbAssPad = 0;
			}

			return 1;
		}
	}

	for (i = 0; i < *gRhinoWallChunk2Count; i++)
	{
		if (checksum == static_cast<u32>(gRhinoWallChunk2[i]))
		{
			Chunk_ChunkItemByChecksum(checksum);

			if (a2 != 0)
			{
				this->SetUpStuckHorn(&lineInfo, 0);
				this->field_31C.bothFlags = 0xC;
				this->dumbAssPad = 0;
			}

			return 4;
		}
	}

	for (i = 0; i < *gRhinoWallChunk3Count; i++)
	{
		if (checksum == static_cast<u32>(gRhinoWallChunk3[i]))
		{
			if (a2 != 0)
			{
				this->mHealth -= 10;

				if (this->mHealth <= 0)
				{
					this->field_31C.bothFlags = 0x15;
					this->dumbAssPad = 0;
				}
			}

			if (this->mHealth <= 0)
			{
				return 1;
			}

			this->PlayXAPlease(3, 3, 1);
			this->SetUpStuckHorn(&lineInfo, 0);
			return 8;
		}
	}

	if (a2 != 0)
	{
		this->mHealth -= 10;

		if (this->mHealth <= 0)
		{
			this->field_31C.bothFlags = 0x15;
			this->dumbAssPad = 0;
		}
	}

	if (this->mHealth <= 0)
	{
		return 1;
	}

	this->PlayXAPlease(3, 3, 1);
	this->SetUpStuckHorn(&lineInfo, 1);
	return 8;
}

// @Ok
// Rewritten from the disasm (0x4823C0); the earlier draft was a rough
// single-raycast stand-in, missing most of the real behavior. The original
// probes a 64-unit-wide corridor (two parallel rays offset +/-32 units to
// each side of the direct mPos->*a2 line, perpendicular via the normalized
// direction's (dz,-dx) rotation), and for each probe, if it hits an item
// whose model checksum is in the gRhinoWallChunk0 allow-list (same list
// GonnaHitWall uses), retries the same probe starting just past the hit
// point (Position + 32*dir) instead of stopping; any other hit, or a3==0
// while blocked, fails the whole check. Only when both probes come back
// clear does it return true.
u8 CRhino::LineOfSightCheck(CVector const *a2, i32 a3)
{
	CVector delta = *a2 - this->mPos;
	CVector dir = delta >> 12;

	VectorNormal(reinterpret_cast<VECTOR*>(&dir), reinterpret_cast<VECTOR*>(&dir));

	SLineInfo lineInfo;

	lineInfo.MinCoords.vx = 0;
	lineInfo.MinCoords.vy = 0;
	lineInfo.MinCoords.vz = 0;

	lineInfo.MaxCoords.vx = 0;
	lineInfo.MaxCoords.vy = 0;
	lineInfo.MaxCoords.vz = 0;

	lineInfo.Position.vx = 0;
	lineInfo.Position.vy = 0;
	lineInfo.Position.vz = 0;

	lineInfo.Normal.vx = 0;
	lineInfo.Normal.vy = 0;
	lineInfo.Normal.vz = 0;

	i32 pass;

	for (pass = 0; pass < 2; pass++)
	{
		CVector offset;

		if (pass == 0)
		{
			offset.vx = 32 * dir.vz;
			offset.vy = 0;
			offset.vz = -32 * dir.vx;
		}
		else
		{
			offset.vx = -32 * dir.vz;
			offset.vy = 0;
			offset.vz = 32 * dir.vx;
		}

		lineInfo.StartCoords = this->mPos + offset;
		lineInfo.EndCoords = *a2 + offset;

		for (;;)
		{
			M3dColij_InitLineInfo(&lineInfo);
			M3dZone_LineToItem(&lineInfo, 1);

			if (!lineInfo.pItem)
			{
				break;
			}

			u32 checksum = Spool_GetModelChecksum(lineInfo.pItem);

			if (!a3)
			{
				return 0;
			}

			i32 i;
			i32 passthrough = 0;

			for (i = 0; i < *gRhinoWallChunk0Count; i++)
			{
				if (checksum == static_cast<u32>(gRhinoWallChunk0[i]))
				{
					passthrough = 1;
					break;
				}
			}

			if (!passthrough)
			{
				return 0;
			}

			lineInfo.StartCoords.vx = lineInfo.Position.vx + 32 * dir.vx;
			lineInfo.StartCoords.vy = lineInfo.Position.vy + 32 * dir.vy;
			lineInfo.StartCoords.vz = lineInfo.Position.vz + 32 * dir.vz;
		}
	}

	return lineInfo.pItem == 0;
}

// @Ok
// @Matching
void CRhino::PlaySounds(void)
{
	switch (this->mAnim)
	{
		case 12:
			if (!(this->field_388 & 1) && this->mFrame >= 3)
			{
				SFX_PlayPos(G_RHINO_SOUND = this->GetNextFootstepSFX(), &this->mPos, 0);
				this->field_388 |= 1;
			}
			else if (!(this->field_388 & 2) && this->mFrame >= 0xD)
			{
				SFX_PlayPos(G_RHINO_SOUND = this->GetNextFootstepSFX(), &this->mPos, 0);
				this->field_388 |= 2;
			}
			else if (!(this->field_388 & 4) && this->mFrame >= 0x15)
			{
				SFX_PlayPos(G_RHINO_SOUND = this->GetNextFootstepSFX(), &this->mPos, 0);
				this->field_388 |= 4;
			}
			else if (!(this->field_388 & 8) && this->mFrame >= 0x1E)
			{
				SFX_PlayPos(G_RHINO_SOUND = this->GetNextFootstepSFX(), &this->mPos, 0);
				this->field_388 |= 8;
			}
			break;
		case 5:
			if (!(this->field_388 & 1) && this->mFrame >= 0)
			{
				SFX_PlayPos(G_RHINO_SOUND = this->GetNextFootstepSFX(), &this->mPos, 0);
				this->field_388 |= 1;
			}
			else if (!(this->field_388 & 2) && this->mFrame >= 8)
			{
				SFX_PlayPos(G_RHINO_SOUND = this->GetNextFootstepSFX(), &this->mPos, 0);
				this->field_388 |= 2;
			}
			break;
		case 2:
			if (!(this->field_388 & 1) && this->mFrame >= 0)
			{
				SFX_PlayPos(G_RHINO_SOUND = this->GetNextFootstepSFX(), &this->mPos, 0);
				this->field_388 |= 1;
			}
			else if (!(this->field_388 & 2) && this->mFrame >= 0xF)
			{
				SFX_PlayPos(G_RHINO_SOUND = this->GetNextFootstepSFX(), &this->mPos, 0);
				this->field_388 |= 2;
			}
			break;
		case 18:
			if (!(this->field_388 & 1) && this->mFrame >= 0x15)
			{
				this->field_388 |= 1;
			}
			break;
	}
}

// @Ok
// @AlmostMatching: slightly diff code gen
void CRhino::PlayXAPlease(
		i32 a2,
		i32 a3,
		i32 a4)
{
	i32 v5 = Rnd(a3 + a4);
	i32 v6 = this->field_3DC;

	if (v5 < a3)
	{
		if (a3 > 1)
		{
			if ( ((v6 >> a2) & (1 << v5)) != 0 && ++v5 >= a3 )
			{
				v5 = 0;
			}

			for (i32 i = 0; i < a3; i++)
			{
				v6 &= ~(1 << (i + a2));
			}

			v6 |= 1 << (v5 + a2);
		}

		i32 v8 = a2 + v5;
		if ( gRhinoData[v8].field_4 )
		{
			if (Redbook_XAPlayPos(
				gRhinoData[v8].field_0,
				gRhinoData[v8].field_2,
				&this->mPos,
				gRhinoData[v8].field_6) )

			{
				this->AttachXA(gRhinoData[v8].field_0, gRhinoData[v8].field_2);
				this->field_3DC = v6;
			}
		}
		else if (G_MECHLIST_PLAYER->CanITalkRightNow() && Redbook_XAPlayPos(
				gRhinoData[v8].field_0,
				gRhinoData[v8].field_2,
				&G_MECHLIST_PLAYER->mPos,
				gRhinoData[v8].field_6) )
		{
			G_MECHLIST_PLAYER->AttachXA(gRhinoData[v8].field_0, gRhinoData[v8].field_2);
			this->field_3DC = v6;
		}
	}
}

// @Ok
// Rewritten from the disasm (0x47E1F0). Real fixes over the earlier draft:
// the CVector<<int shift (0x4E7870) is "normal << 14", not "normal * 14";
// there is a CAIProc_MoveTo toward a2->Position ({200,0,500}) that was
// missing entirely; CRhinoWallImpact is only constructed when a3 != 0 (the
// earlier draft built it unconditionally); this->field_388 is reset right
// before RunAnim, matching the PlaySingleAnim idiom used elsewhere; the
// camera shake call (0x416880, confirmed CCamera::Shake by name) happens
// right after CRhinoWallImpact, not via ShakePad. One call is not
// reproduced: normal/120 is computed and passed to CVector::operator+=
// (0x4E7590) but its target and result are both unused afterward in the
// disasm (no field or local is ever read back from it), so it looks like
// dead/redundant code the original compiler kept; omitting it should have
// no observable effect.
void CRhino::SetUpStuckHorn(SLineInfo *a2, i32 a3)
{
	CVector normal(a2->Normal.vx, a2->Normal.vy, a2->Normal.vz);
	CVector aimPoint = this->mPos - (normal << 14);

	CSVector aimAngles;
	Utils_CalcAim(&aimAngles, &this->mPos, &aimPoint);

	new CAIProc_LookAt(this, aimAngles.vy, 0, 0x37, 0xC8);

	SMoveToInfo moveInfo;
	moveInfo.field_0 = a2->Position;
	moveInfo.field_C = 200;
	moveInfo.field_10 = 0;
	moveInfo.field_14 = 500;

	new CAIProc_MoveTo(this, &moveInfo, 1);

	this->field_388 = 0;
	this->RunAnim(0x18, 0, -1);

	if (a3 != 0)
	{
		new CRhinoWallImpact(a2);
		G_CAMERA_LIST->Shake(this->mPos, CAMERASHAKE_BIG);
	}

	if (G_SAVE_GAME.field_7B)
	{
		if (Pad_GetActuatorTime(0, 0) <= 2)
		{
			Pad_ActuatorOn(0, 6, 0, 1);
		}
		if (Pad_GetActuatorTime(0, 1) <= 2)
		{
			Pad_ActuatorOn(0, 0xA, 1, 0xC8);
		}
	}

	SFX_PlayPos(0x804B, &this->mPos, 0);

	this->field_31C.bothFlags = 0xA;
	this->dumbAssPad = 0;
}

// @Ok
// Found the real function at 0x47F190 (not in tools/names.json; called from
// the not-yet-decompiled CRhino::Hit) and rewrote against it. The earlier
// draft had several real bugs: the result==0 branch only set field_1F8, but
// the original also sets mVel = delta*a3 and transitions
// field_31C.bothFlags/dumbAssPad there, same as the result==2 branch; the
// result==2 ratio's numerator is PathCheck's own CVector out-param (its
// safe/blocked position) minus this->mPos, not a4->vx/a4->vz; the final
// mVel is that same vector times (ratio+1) via CVector::operator*
// (0x4E7800), not "delta << field_1F8" (field_1F8 itself is never
// overwritten with ratio+1 in the original, it stays as the plain ratio).
void CRhino::SlideFromHit(i32 a2, i32 a3, CVector *a4)
{
	CVector delta = *a4 / a2;
	CVector target = this->mPos + delta;
	CVector pathOut(0, 0, 0);

	i32 result = this->PathCheck(&this->mPos, &target, &pathOut, 0x37);

	if (result == 0)
	{
		this->field_1F8 = a3;
		this->mVel = delta * a3;
		this->field_31C.bothFlags = 0xF;
		this->dumbAssPad = 0;
	}
	else if (result == 2)
	{
		if (a4->vx || a4->vz)
		{
			CVector deltaPath = pathOut - this->mPos;

			i32 signX = a4->vx >> 31;
			i32 absX = (a4->vx ^ signX) - signX;

			i32 signZ = a4->vz >> 31;
			i32 absZ = (a4->vz ^ signZ) - signZ;

			i32 ratio;

			if (absX > absZ)
			{
				ratio = (a3 * deltaPath.vx) / delta.vx;
			}
			else
			{
				ratio = (a3 * deltaPath.vz) / delta.vz;
			}

			this->field_1F8 = ratio;

			if (ratio > 1)
			{
				this->mVel = deltaPath * (ratio + 1);
				this->field_31C.bothFlags = 0xF;
				this->dumbAssPad = 0;
			}
		}
	}
}

// @Ok
// Verified case-by-case against the disasm (0x4810C0). Fixed two real bugs:
// (1) the call order in case 1 was wrong (the original does SFX_PlayPos,
// then the camera shake, then ShakePad's actuator rumble, then
// Effects_RhinoStomp, before reading field_31C.bothFlags at all); (2) the
// three-way outcome (barrel loop / Hit MechList / neither) was collapsed
// into an if/else, so dumbAssPad got force-set to 3 even when the Hit
// branch ran; the original only forces dumbAssPad=3 in the third case
// (neither condition true), leaving it at the post-increment value (2) for
// both the barrel and the Hit branches.
void CRhino::StompGround(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->field_324 = 0;
			this->field_328 = 0;
			this->PlayXAPlease(0x13, 1, 1);
			this->PlaySingleAnim(0x13, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mFrame < 0x11)
			{
				return;
			}

			SFX_PlayPos(0x804B, &this->mPos, 0);
			G_CAMERA_LIST->Shake(this->mPos, CAMERASHAKE_BIG);
			this->ShakePad();
			Effects_RhinoStomp(this);

			{
				i32 oldFlags = this->field_31C.bothFlags;
				this->dumbAssPad++;

				if (oldFlags == 0x14)
				{
					i32 barrels = 0;

					for (
							CBody *cur = G_ENVIRONMENTAL_OBJECT_LIST;
							cur && barrels < 2;
							cur = reinterpret_cast<CBody*>(cur->mNextItem))
					{
						if (cur->mType == 401)
						{
							if (Utils_CrapDist(this->mPos, cur->mPos) < 0x2BC && cur != G_MECHLIST_PLAYER->mHeldObject)
							{
								reinterpret_cast<CBaddy*>(cur)->PlayerIsVisible();
								barrels++;
							}
						}
					}
				}
				else if (G_MECHLIST_PLAYER->field_AD4 && (G_MECHLIST_PLAYER->field_8E8 || G_MECHLIST_PLAYER->field_8E9))
				{
					SHitInfo hit;
					hit.field_C.vx = 0;
					hit.field_C.vy = 0;
					hit.field_C.vz = 0;
					hit.field_0 = 6;
					hit.field_4 = 0xD;
					hit.field_8 = 0x1E;

					G_MECHLIST_PLAYER->Hit(&hit);
					G_MECHLIST_PLAYER->KnockSpideyFromCrawlPosition();
				}
				else
				{
					this->dumbAssPad = 3;
				}
			}
			break;
		case 2:
			if (this->mAnimFinished)
			{
				if (this->field_31C.bothFlags == 0x14)
				{
					this->field_31C.bothFlags = 2;
				}
				else
				{
					this->field_31C.bothFlags = 3;
				}
				this->dumbAssPad = 0;
			}
			break;
		case 3:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// @Matching
void CRhino::StuckInWall(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_348 |= 2;
			this->field_218 &= ~8;
			new CAIProc_StateSwitchSendMessage(this, 0x0C);
			this->dumbAssPad++;
		case 1:
			if (this->field_288 & 1)
			{
				this->field_288 &= ~1;
				this->field_230 = G_MECHLIST_PLAYER->field_E18 ? 900 : Utils_GetValueFromDifficultyLevel(200, 150, 120, 90);
				this->dumbAssPad++;
			}
			break;
		case 2:
			this->RunTimer(&this->field_230);
			if (this->field_230 > 0x3C)
			{
				if (!G_MECHLIST_PLAYER->field_E18)
				{
					this->field_230 = 0x3C;
				}
			}

			this->field_348 |= 2;
			if (this->mAnimFinished)
			{
				if (this->field_230)
				{
					this->PlaySingleAnim(0x16, 0, -1);
				}
				else
				{
					this->PlaySingleAnim(0x19, 0, -1);
					this->dumbAssPad++;
					this->field_31C.bothFlags = 0xB;
					SFX_PlayPos(0x8048, &this->mPos, 0);

					if (this->field_218 & 8)
					{
						this->PlayXAPlease(9, 2, 1);
					}
				}
			}
			break;
		case 3:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 2;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
CRhinoNasalSteam::~CRhinoNasalSteam(void)
{
}

// @Ok
// @Matching
void Rhino_RelocatableModuleClear(void)
{
	CItem *pSearch = G_BADDY_LIST;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 307)
			delete pSearch;

		pSearch = pNext;
	}
}


// @Ok
// Re-verified instruction by instruction against the disasm (0x481300) with
// the now-complete LineOfSightCheck/GonnaHitWall: every field store, branch
// and the a2-gated bothFlags=7/8 choice matches. Residue: this->mPlayerDist
// is declared u16 in ob.h (CBody, offset 0xE4), but the original reads it
// with the full 32-bit eax register; it is shared CBody state used
// elsewhere (ob.cpp, powerup.cpp), so its type is not changed from here.
i32 CRhino::DetermineFightState(i32 a2)
{
	i32 dy = this->mPos.vy - G_MECHLIST_PLAYER->mPos.vy;
	i32 originalFlags = this->field_31C.bothFlags;
	i32 sign = dy >> 31;
	i32 absDy = (dy ^ sign) - sign;

	if (absDy > 0x64000)
	{
		if (!this->field_324)
		{
			this->field_324 = Utils_GetValueFromDifficultyLevel(400, 200, 120, 120);
		}
		return 0;
	}

	if (!this->LineOfSightCheck(&G_MECHLIST_PLAYER->mPos, 1))
	{
		if (this->field_1F0)
		{
			return 0;
		}

		CVector v;
		v.vx = 0;
		v.vy = 0;
		v.vz = 0;
		this->GetWaypointNearTarget(&G_MECHLIST_PLAYER->mPos, 0x12C000, this->field_21D, &v);
		this->field_21D++;

		if (Utils_CrapXZDist(this->mPos, v) < 0x104)
		{
			return 0;
		}

		if (this->PathCheck(&this->mPos, &v, 0, 100))
		{
			return 0;
		}

		this->field_1A8[1] = v;
		this->field_1F0 = 1;
		this->field_31C.bothFlags = 1;
		this->dumbAssPad = 0;
		this->field_218 |= 0x10;
		return 1;
	}
	else
	{
		if (this->field_218 & 0x10)
		{
			this->PlayXAPlease(0x12, 1, 1);
			this->field_218 &= ~0x10;
		}

		if (G_ATTACK_RELATED - this->field_358 < 0x96 && G_MECHLIST_PLAYER->field_AD4)
		{
			this->field_31C.bothFlags = 0xD;
			this->dumbAssPad = 0;
		}
		else if (this->mPlayerDist < 0x1388)
		{
			if (G_MECHLIST_PLAYER->field_E1C & 0x80)
			{
				return 0;
			}

			if (G_MECHLIST_PLAYER->field_E1C & 0x800000)
			{
				if (this->mPlayerDist < 0xC8)
				{
					return 0;
				}
				this->field_31C.bothFlags = 5;
			}
			else if (this->mPlayerDist < 0xC8)
			{
				this->field_31C.bothFlags = 6;
			}
			else if (this->mPlayerDist < 0x1F4)
			{
				this->field_31C.bothFlags = 5;
			}
			else
			{
				this->field_31C.bothFlags = a2 ? 8 : 7;
			}

			this->dumbAssPad = 0;
		}
	}

	if (originalFlags != this->field_31C.bothFlags)
	{
		this->Baddy_SendSignal();
	}
	return originalFlags != this->field_31C.bothFlags;
}

// @Ok
void CRhino::TakeHit(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_310 = 0;
			new CAIProc_LookAt(this, G_MECHLIST_PLAYER, 0, 0, 80, 200);
			this->PlaySingleAnim(0xFu, 0, -1);
			this->field_230 = Utils_GetValueFromDifficultyLevel(40, 30, 21, 21);
			this->dumbAssPad++;
			break;
		case 1:
			this->RunTimer(&this->field_230);
			if (!this->field_230)
			{
				if (this->DetermineFightState(1))
				{
					if (this->DistanceToPlayer(0) > 500)
					{
						if (this->field_31C.bothFlags == 5 || this->field_31C.bothFlags == 4)
						{
							this->field_31C.bothFlags = 8;
							this->dumbAssPad = 0;
						}
					}
					else if (this->field_31C.bothFlags == 8)
					{
						this->field_31C.bothFlags = 5;
						this->dumbAssPad = 0;
					}
				}
				else
				{
					this->PlaySingleAnim(0, 0, -1);
					this->field_31C.bothFlags = 22;
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
void CRhino::HitWall(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->ShakePad();
			G_CAMERA_LIST->Shake(this->mPos, CAMERASHAKE_BIG);
			this->Neutralize();
			this->mCBodyFlags &= ~0x10;
			this->PlaySingleAnim(17, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
			{
				if ( this->mHealth <= 0 )
				{
					this->field_31C.bothFlags = 21;
					this->dumbAssPad = 0;
				}
				else
				{
					this->PlaySingleAnim(0x12u, 0, -1);
				}
			}
			break;
		case 2:

			if ( this->mAnimFinished )
			{
				if ( this->mAnim == 18 )
				{
					this->mAngles.vy = (this->mAngles.vy - 2048) & 0xFFF;
					this->PlaySingleAnim(0x15u, 0, -1);
				}
				else
				{
					this->mCBodyFlags |= 0x10u;
					this->PlaySingleAnim(0, 0, -1);
	
					this->field_31C.bothFlags = 2;
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
// Verified against the raw disasm (0x47DFE0). It writes RhinoVtable at
// entry, then calls two zero/fixed-arg helpers that map cleanly onto
// DeleteFrom(&BaddyList) (same &dword_56E990/BaddyList address the
// constructor's AttachTo call uses) and Panel_DestroyHealthBar(), then does
// the field_3E0/field_3E4/field_3F8/field_40C delete-and-clear cleanup
// (walked as one shifted-pointer loop over all three arrays in the
// original, done here as three ordinary indexed loops for the same
// effect), then sets gBossRelated (confirmed at 0x56E998 by the
// maintainer's IDB) back to 0 before chaining to the base class
// destructor. Hex-Rays mistypes this function's "this" as
// std::locale::_Locimp in its decompiled view (its base-class-destructor
// tail call happens to be byte-identical to _Locimp's own trivial
// destructor and got folded with it at link time); the raw instruction
// listing settles it, since it writes CRhino's own vtable at offset 0.
CRhino::~CRhino(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
	Panel_DestroyHealthBar();

	if (this->field_3E0)
		delete reinterpret_cast<CItem*>(this->field_3E0);

	for (i32 i = 0; i < 5; i++)
	{
		if (this->field_3E4[i])
			delete reinterpret_cast<CItem*>(this->field_3E4[i]);
		this->field_3E4[i] = 0;

		if (this->field_3F8[i])
			delete reinterpret_cast<CItem*>(this->field_3F8[i]);
		this->field_3F8[i] = 0;

		if (this->field_40C[i])
			delete reinterpret_cast<CItem*>(this->field_40C[i]);
		this->field_40C[i] = 0;
	}

	gBossRelated = 0;
}

// @Ok
void CRhino::Laugh(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_230 = 100;
			this->PlaySingleAnim(20, 0, -1);
			SFX_PlayPos(((G_ATTACK_RELATED & 1) == 0 ? 1 : 0) | 0x8046, &this->mPos, 0);
			this->dumbAssPad++;

			break;
		case 1:
			this->RunTimer(&this->field_230);
			if (this->mAnimFinished)
			{
				if (!this->field_230)
				{
					this->field_31C.bothFlags = 2;
					this->dumbAssPad = 0;
				}
				else
				{
					this->PlaySingleAnim(0, 0, -1);
				}
			}
			break;
		default:
			print_if_false(0, "Unknown substate.");
			break;
	}
}

// @Ok
void CRhino::RhinoInit(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_358 = G_ATTACK_RELATED - 155;
			i32 GroundHeight;
			GroundHeight = Utils_GetGroundHeight(&this->mPos, 300, 300, 0);
			if ( GroundHeight != -1 )
			{
				this->mPos.vy = GroundHeight - (this->field_21E << 12);
				this->field_29C = this->mPos.vy;
				this->field_2A0 = GroundHeight;
				this->dumbAssPad++;

				this->PlaySingleAnim(0, 0, -1);
			}
			break;
		case 1:
			this->field_31C.bothFlags = 8;
			this->dumbAssPad = 0;
			break;
		default:
			print_if_false(0, "Unknown sub-state!");
			break;
	}
}

// @Ok
// Confirmed: the vtable call here is CBaddy::PlayerIsVisible (baddy.cpp's
// own VALIDATE_VTABLE(CBaddy, PlayerIsVisible, 5) puts it at vtable offset
// 0x14, matching the slot used by this loop's original disasm and by the
// identical loop inlined into GonnaHitWall's type-401 branch and
// StompGround's case 1). No separate address found in tools/names.json for
// this function on PC; StompGround has its own independent copy of the same
// loop rather than calling this one, which matches the Mac build listing it
// as a small (176 byte) standalone function while nothing in this TU calls
// it directly.
void CRhino::FuckUpSomeBarrels(void)
{
	i32 barrels = 0;

	for (
			CBody* cur = G_ENVIRONMENTAL_OBJECT_LIST;
			cur && barrels < 2;
			cur = reinterpret_cast<CBody*>(cur->mNextItem))
	{
		if (cur->mType == 401)
		{
			if (Utils_CrapDist(this->mPos, cur->mPos) < 0x2BC && cur != G_MECHLIST_PLAYER->mHeldObject)
			{
				reinterpret_cast<CBaddy*>(cur)->PlayerIsVisible();
				barrels++;
			}
		}
	}
}

// @Ok
void CRhino::StandStill(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->dumbAssPad++;
		case 1:
			if (this->mAnim)
				this->PlaySingleAnim(0, 0, -1);
			break;
		default:
			print_if_false(0, "Unknown substate.");
			break;
	}
}

// @Ok
INLINE void CRhino::ShakePad(void)
{
	if ( G_SAVE_GAME.field_7B )
	{
		if ( Pad_GetActuatorTime(0, 0) <= 2u )
			Pad_ActuatorOn(0, 6u, 0, 1u);
		if ( Pad_GetActuatorTime(0, 1u) <= 2u )
			Pad_ActuatorOn(0, 0xAu, 1, 0xC8u);
	}
}

// @Ok
// Confirmed inlined into GetShocked's case 1 (0x47E420): the exact same
// switch on DifficultyLevel (dword_54D474) with 175/125/75/0-default values
// appears there directly, no separate call, same as ShakePad/PlaySingleAnim.
INLINE i32 CRhino::GetShockDamage(void)
{
	switch ( G_DIFFICULTY_LEVEL )
	{
		case 0:
		case 1:
			return 175;
		case 2:
			return 125;
		case 3:
			return 75;
		default:
			print_if_false(0, "Unknown difficulty level!");
			return 0;
	}
}

// @Ok
// Confirmed inlined into PlaySounds (0x481550): the exact same
// (Rnd(3)+76)|0x8000 retry-until-different-from-gRhinoSound loop appears
// inline at every footstep call site there, no separate call.
INLINE u32 CRhino::GetNextFootstepSFX(void)
{
	u32 res;
	for (res = (Rnd(3) + 76) | 0x8000; res == G_RHINO_SOUND; res = (Rnd(3) + 76) | 0x8000)
		;

	return res;
}

// @Ok
INLINE void CRhino::PlaySingleAnim(u32 a2, i32 a3, i32 a4)
{
	this->field_388 = 0;
	this->RunAnim(a2, a3, a4);
}

// @Ok
// Verified against the raw disasm (0x47DE40). Real fix: the dazed-star
// speed init loop wrote into a disconnected repo-local gRhinoDazedData
// array instead of the actual game memory at 0x682B64 (gRhinoDazedStarSpeed,
// the same global DoDazedEffect reads), so DoDazedEffect never saw the
// values this constructor generated. gRhinoStrangeInitData and gRhinoData
// are now fixed-address game pointers (see their declarations near the top
// of the file) instead of guessed repo-local values, matching what this
// constructor actually reads (dword_5520B8/dword_5520BC) and what the
// checksum-assert loop actually walks (a shifted pointer into
// gRhinoData[j].field_8, stride 0xC, base 0x552208, matching the
// maintainer's IDB name). sub_402C00 at function entry is the implicit
// CBaddy base-class constructor call the compiler emits automatically; it
// needs no explicit source line here.
CRhino::CRhino(i16* a2, i32 a3)
{
	i16 *v5 = this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2)));
	this->InitItem("rhino");

	this->mFlags |= 0x480;
	// @FIXME
	this->mpLight = &M3d_RhinoLight;
	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->field_21E = 100;
	this->field_1F4 = a3;
	this->mNode = a3;
	this->mRMinor = 175;

	this->field_230 = 0;
	this->field_216 = 32;

	this->mPushVal = 64;
	this->field_31C.bothFlags = 0;
	this->field_2A8 |= 1;
	this->field_2A8 |= 0x200;
	this->field_2A8 |= 0x2000000;

	this->mType = 307;
	this->mHealth = Utils_GetValueFromDifficultyLevel(1400, 1400, 1400, 1400);

	this->field_294.Int = gRhinoStrangeInitData[0];
	this->field_298.Int = gRhinoStrangeInitData[1];

	this->field_344 = G_ATTACK_RELATED - 240;

	for (i32 i = 0; i < LEN_RHINO_DAZED_DATA; i++)
	{
		gRhinoDazedStarSpeed[i] = Rnd(4096);
	}

	for (i32 j = 0; j < LEN_RHINO_DATA; j++)
	{
		if (gRhinoData[j].field_8 != j)
			DoAssert(0, "Fire Matt, he fucked up the rhino XA.  Actually, kick him in the nuts first.");
	}

	this->ParseScript(reinterpret_cast<u16*>(v5));
	Panel_CreateHealthBar(this, 307);
}

// @Ok
// No separate PC address: confirmed fully inlined into Rhino_CreateRhino's
// else branch (0x47DD40, taken when the stack's a3 is 0), same shape and
// field stores (InitItem("rhino"), mFlags|=0x480, mpLight=&M3d_RhinoLight,
// mType=307), no AttachTo/health/script setup at all, matching this body.
CRhino::CRhino(void)
{
	this->InitItem("rhino");
	this->mFlags |= 0x480;
	// @FIXME
	this->mpLight = &M3d_RhinoLight;
	this->mType = 307;
}

// @Ok
void Rhino_CreateRhino(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(*stack);
	i32 v3 = static_cast<i32>(stack[1]);

	if (v2)
	{
		*result = reinterpret_cast<u32>(new CRhino(v2, v3));
	}
	else
	{
		*result = reinterpret_cast<u32>(new CRhino());
	}
}


// @Ok
CRhinoNasalSteam::CRhinoNasalSteam(CVector* a2, CVector* a3)
{
	this->mPos = *a2;
	this->mVel = *a3;

	this->SetAnim(1);
	this->SetSemiTransparent();
	this->SetTransparency(64);
	this->SetAnimSpeed(128);
	this->SetScale(128);
	this->mAngle = Rnd(4096);
}

// @Ok
// minor decomp diff
void CRhinoNasalSteam::Move(void)
{

	i16 mAnimSpeed = this->mAnimSpeed;

	if (mAnimSpeed)
	{
		u16 v3 = (this->mFrame << 8) | this->mFrameFrac;
		u16 v4 = mAnimSpeed + v3;

		this->mFrameFrac = v4;
		v4 >>= 8;
		this->mFrame = v4;

		if ( (char)v4 >= (i32)this->mNumFrames)
		{
			this->mAnimSpeed = 0;
			this->mFrame = this->mNumFrames - 1;
		}

		i32 index = this->mFrame;
		this->mpPSXFrame = &this->mpPSXAnim[index];
	}

	this->mPos += this->mVel;

	bool v7 = ++this->mAge <= 30;
	this->mVel.vy -= 1024;

	if (!v7 )
	{
		this->Die();
	}
	else
	{
		this->SetTransparency(64 - 2 * (this->mAge & 0xFF));
		this->SetScale(Rnd(4) + 4 * (this->mAge + 32));
	}
}

void validate_CRhino(void){
	VALIDATE_SIZE(CRhino, 0x424);

	VALIDATE(CRhino, field_324, 0x324);
	VALIDATE(CRhino, field_328, 0x328);
	VALIDATE(CRhino, field_32C, 0x32C);
	VALIDATE(CRhino, field_330, 0x330);
	VALIDATE(CRhino, field_334, 0x334);

	VALIDATE(CRhino, field_338, 0x338);

	VALIDATE(CRhino, field_344, 0x344);
	VALIDATE(CRhino, field_348, 0x348);
	VALIDATE(CRhino, field_34C, 0x34C);
	VALIDATE(CRhino, field_350, 0x350);
	VALIDATE(CRhino, field_354, 0x354);

	VALIDATE(CRhino, field_358, 0x358);
	VALIDATE(CRhino, field_35C, 0x35C);
	VALIDATE(CRhino, field_360, 0x360);
	VALIDATE(CRhino, field_388, 0x388);

	VALIDATE(CRhino, field_3D0, 0x3D0);
	VALIDATE(CRhino, field_3D4, 0x3D4);
	VALIDATE(CRhino, field_3D8, 0x3D8);

	VALIDATE(CRhino, field_3DC, 0x3DC);
	VALIDATE(CRhino, field_3E0, 0x3E0);
	VALIDATE(CRhino, field_3E4, 0x3E4);
	VALIDATE(CRhino, field_3F8, 0x3F8);
	VALIDATE(CRhino, field_40C, 0x40C);
	VALIDATE(CRhino, field_420, 0x420);
}

void validate_CRhinoNasalSteam(void)
{
	VALIDATE_SIZE(CRhinoNasalSteam, 0x68);
}

void validate_SRhinoData(void)
{
	VALIDATE_SIZE(SRhinoData, 0xC);

	VALIDATE(SRhinoData, field_0, 0x0);
	VALIDATE(SRhinoData, field_2, 0x2);

	VALIDATE(SRhinoData, field_4, 0x4);
	VALIDATE(SRhinoData, field_6, 0x6);

	VALIDATE(SRhinoData, field_8, 0x8);
}

#include "my_patch.h"

// Not hooked, and why:
//
// 1. CRhino::AI (0x4819C0) and CRhino::GetShocked (0x47E420). GetShocked calls
//    Effects_Electrify, which does "new CElectrify". Our CElectrify has no
//    Move (0x00439410) and no destructor (0x004390E0), so a shocked rhino
//    would get an object with our vtable and lose both. AI goes out with it,
//    it calls GetShocked directly.
//
// 2. Rhino_RelocatableModuleInit (0x47DCF0) and Rhino_CreateRhino (0x47DD40).
//    The original inlines "new CRhino" into Rhino_CreateRhino, which stamps a
//    vtable. Our CRhino has no Hit override, the exe's slot 3 is sub_47EE70,
//    so a rhino built by our code would answer Hit with CBody::Hit. Init only
//    stores the pointer to CreateRhino, so hooking it has the same effect.
//
// 3. CRhinoNasalSteam::Move. The original merged its body with
//    CShellRhinoNasalSteam::Move at 0x48F5E0, which is shell.cpp territory.
//
// 4. CheckIfPlayerHit, FuckUpSomeBarrels, StandStill, ShakePad,
//    GetShockDamage, GetNextFootstepSFX, PlaySingleAnim and both CRhino
//    constructors have no standalone address, the original inlines them.
//
// @Bogus
void patch_rhino(void)
{
	PATCH_PUSH_RET(0x0047DD10, Rhino_RelocatableModuleClear);

	PATCH_PUSH_RET_POLY(0x0047DFE0, CRhino::~CRhino, "??1CRhino@@UAE@XZ");

	PATCH_PUSH_RET(0x0047E0A0, CRhino::PlayXAPlease);
	PATCH_PUSH_RET(0x0047E1F0, CRhino::SetUpStuckHorn);
	PATCH_PUSH_RET(0x0047E680, CRhino::StuckInWall);
	PATCH_PUSH_RET(0x0047E8A0, CRhino::GonnaHitWall);
	PATCH_PUSH_RET(0x0047ECA0, CRhino::GetLaunched);

	// sub_47F190 in names.json. It is the only unclaimed function left in the
	// file and CRhino::Hit calls it at 0x47F170, the same way CScorpion::Hit
	// calls CScorpion::SlideFromHit.
	PATCH_PUSH_RET(0x0047F190, CRhino::SlideFromHit);

	PATCH_PUSH_RET(0x0047F320, CRhino::FollowWaypoints);
	PATCH_PUSH_RET(0x0047F490, CRhino::ChasePlayer);
	PATCH_PUSH_RET(0x0047F800, CRhino::ChargePlayer);
	PATCH_PUSH_RET(0x00480090, CRhino::Laugh);
	PATCH_PUSH_RET(0x00480170, CRhino::AttackPlayer);
	PATCH_PUSH_RET(0x00480480, CRhino::DoDazedEffect);
	PATCH_PUSH_RET(0x00480820, CRhino::TakeHit);
	PATCH_PUSH_RET(0x00480A40, CRhino::GetTrapped);
	PATCH_PUSH_RET(0x00480DA0, CRhino::DieRhino);
	PATCH_PUSH_RET(0x00480F50, CRhino::HitWall);
	PATCH_PUSH_RET(0x004810C0, CRhino::StompGround);
	PATCH_PUSH_RET(0x00481300, CRhino::DetermineFightState);
	PATCH_PUSH_RET(0x00481550, CRhino::PlaySounds);
	PATCH_PUSH_RET(0x00481910, CRhino::RhinoInit);
	PATCH_PUSH_RET(0x004823C0, CRhino::LineOfSightCheck);
	PATCH_PUSH_RET(0x004825E0, CRhino::DoMGSShadow);

	PATCH_PUSH_RET_POLY(0x00482920, CRhinoNasalSteam::~CRhinoNasalSteam, "??1CRhinoNasalSteam@@UAE@XZ");
}
