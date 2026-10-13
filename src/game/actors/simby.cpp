#include <new>
#include "game/actors/simby.h"
#include "compatibility/validate.h"
#include "engine/world/trig.h"
#include "core/utils.h"
#include "core/game_heap.h"
#include "engine/audio/ps2redbook.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/world/ai.h"
#include "core/utils.h"
#include "engine/rendering/m3dutils.h"
#include "game/player/spidey.h"
#include "engine/rendering/scene_renderer.h"
#include "game/player/web.h"
#include "game/cinematics/camera.h"
#include "engine/world/m3dzone.h"
#include "compatibility/my_assert.h"
#include "engine/assets/spool.h"
#include "game/effects/effects.h"
#include "game/effects/exp.h"

// Simby's state flag table. The IDB names 0x00555564 gSimbyFlags, and
// CSimby::AI (0x4AE3D0) and CSimby::Hit (0x4A8AB0) push that address into
// CBaddy::CheckStateFlags, which is where PlayGruntSound got inlined. Nothing
// in the binary writes the table, but our copy has no initialiser, so a hooked
// reader would see zeros instead of the real flags.
#ifdef SPIDEY_STANDALONE
#define gSimbyFlags (*reinterpret_cast<SStateFlags*>(0x00555564))
#else
static SStateFlags gSimbyFlags;
#endif
//#define G_SIMBY_FLAGS (gSimbyFlags)
#define G_SIMBY_FLAGS (*reinterpret_cast<SStateFlags*>(0x00555564))

// MiscList belongs to shell.cpp and is still a plain repo global, so the two
// CSymBurn functions that touch it are left out of patch_simby().
extern CBody *MiscList;
#include "game/cinematics/camera.h"

// guess: counts constructed CSimby instances, gates the one-time MakeVertexWibbler call.
// sits right after gShellMysterioRelated (0x682C58) in idb_globals.txt, before gSimbyAttackData (0x682C60).
static i32 * const gSimbyCount = reinterpret_cast<i32*>(0x682C5C);

// guess: reset flag adjacent to gSimbyAttackData (0x682C60, idb_globals.txt), purpose unclear.
static i32 * const gSimbyCountResetFlag = reinterpret_cast<i32*>(0x682C64);

// Stays repo-local: the two words below are the exe bytes at 0x005521C0
// (05 05 08 07, 05 01 00 00) and nothing in the binary writes them.
EXPORT i32 gSimbySetup[2] = { 84215815, 261 };

// @Ok
// @AlmostMatching: same as SpideyAI_WaitForSimbyGrab
void SpideyAI_ThrownBySimby(CPlayer *pPlayer)
{
	G_MECHLIST_PLAYER->mFlags &= ~0x800u;

	if (G_MECHLIST_PLAYER->mAnimFinished)
	{
		G_MECHLIST_PLAYER->PlaySingleAnim(0xBAu, 0, -1);
	}

	if (pPlayer->mFlags & 4)
	{
		pPlayer->ApplyPose(G_UNK_POSE);
	}
	else
	{
		M3d_BuildTransform(pPlayer);
	}

	i32 GroundHeight = Utils_GetGroundHeight(&pPlayer->mPos, 0, 0x2000, 0);
	if (GroundHeight != -1)
	{
		pPlayer->field_158 = 1;

		pPlayer->mShadowPos.vz = pPlayer->mPos.vz;
		pPlayer->mShadowPos.vx = pPlayer->mPos.vx;
		pPlayer->mShadowPos.vy = GroundHeight;

	}
	else
	{
		pPlayer->field_158 = 0;
	}

	pPlayer->DoMGSShadow();
}

// @Ok
// @AlmostMatching: assignment to field_158 is slightly off :(
void SpideyAI_WaitForSimbyGrab(CPlayer *pPlayer)
{
	G_MECHLIST_PLAYER->mFlags &= ~0x800u;

	if (pPlayer->mFlags & 4)
	{
		pPlayer->ApplyPose(G_UNK_POSE);
	}
	else
	{
		M3d_BuildTransform(pPlayer);
	}

	i32 GroundHeight = Utils_GetGroundHeight(&pPlayer->mPos, 0, 0x2000, 0);
	if (GroundHeight != -1)
	{
		pPlayer->field_158 = 1;

		pPlayer->mShadowPos.vz = pPlayer->mPos.vz;
		pPlayer->mShadowPos.vx = pPlayer->mPos.vx;
		pPlayer->mShadowPos.vy = GroundHeight;

	}
	else
	{
		pPlayer->field_158 = 0;
	}

	pPlayer->DoMGSShadow();
}

// @Ok
// @Matching
void CSimby::SimbyKnockSpideyDown(i32 a2)
{
	SHitInfo v10;

	CVector v7;
	CVector v6;

	v6 = this->mPos - G_MECHLIST_PLAYER->mPos;

	v6 >>= 12;
	VectorNormal(
			reinterpret_cast<VECTOR*>(&v6),
			reinterpret_cast<VECTOR*>(&v6));

	v7 = G_MECHLIST_PLAYER->mPos + (v6 * 50);

	G_MECHLIST_PLAYER->CreateCombatImpactEffect(&v7, 0);
	v10.field_0 = 6;
	v10.field_8 = a2;
	v10.field_4 = 14;

	G_MECHLIST_PLAYER->Hit(&v10);
}

// @Ok
// @Matching
void Simby_TestDrop(const u32 *, u32 *)
{
}

// @Ok
// @Matching
void Simby_CreateFlamingImpactWeb(const u32* stack,u32 *)
{
	CVector v6;
	v6.vx = stack[0];
	v6.vy = stack[1];
	v6.vz = stack[2];

	CSVector v5;
	v5.vx = stack[3];
	v5.vy = stack[4];
	v5.vz = stack[5];

	new CFlamingImpactWeb(&v6, &v5, stack[6]);
}

// @Ok
// @Matching
void Simby_RelocatableModuleClear(void)
{
	CItem *pSearch = G_BADDY_LIST;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 324)
			delete pSearch;

		pSearch = pNext;
	}
}

// @Ok
// @Matching
void Simby_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Simby_RelocatableModuleClear;
	pMod->field_C[0] = Simby_CreateSimby;
	pMod->field_C[1] = Simby_CreateSimbyDroplet;
	pMod->field_C[2] = Simby_CreateFlamingImpactWeb;
	pMod->field_C[3] = Simby_TestDrop;
	pMod->field_C[4] = Simby_CreatePunchOb;
	pMod->field_C[5] = Simby_CreateEmber;
}

// @Ok
// @AlmostMatching: EndCoords dudes get refetched 3 times, which makes me believe there's some crazy inline
CFlamingImpactWeb::CFlamingImpactWeb(
		CVector *a2,
		CSVector *a3,
		i32 a4)
{
	this->field_6C = a4;
	this->mPos = *a2;
	this->field_70 = G_TIMER_RELATED;

	Utils_GetVecFromMagDir(&this->mVel, 32, a3);

	this->mLifetime = 120;

	G_LINE_INFO.StartCoords = this->mPos;

	// @FIXME - all get fetched 3 times wth is going
	// setters it makes it better but still weird
	G_LINE_INFO.EndCoords.SetX(this->mPos.vx + this->mVel.vx * this->mLifetime);
	G_LINE_INFO.EndCoords.SetY(this->mPos.vy + this->mVel.vy * this->mLifetime);
	G_LINE_INFO.EndCoords.SetZ(this->mPos.vz + this->mVel.vz * this->mLifetime);

	M3dColij_InitLineInfo(&G_LINE_INFO);

	G_LINE_OF_SIGHT_CHECK = 1;
	M3dZone_LineToItem(&G_LINE_INFO, 0);
	G_LINE_OF_SIGHT_CHECK = 0;

	if ( G_LINE_INFO.pItem )
	{
		this->pItem = G_LINE_INFO.pItem;

		this->pFace = G_LINE_INFO.pFace;
		this->mLinePos = G_LINE_INFO.Position;
		this->mLineNormal = G_LINE_INFO.Normal;
		this->mLifetime = G_LINE_INFO.Distance / 32;

		DoAssert((this->pItem->mFlags & 0x10) == 0, "Hit env obj!");
	}

	if (this->mLifetime > 0x78u)
	{
		this->mLifetime = 120;
	}

	this->SetAnim(0x10u);
	this->SetSemiTransparent();
	this->mAngle = Rnd(4096);

	this->SetTransparency(0x64u);
	this->SetScale(0);

	this->mPostScale = 0xA001000;
	this->field_5A = Rnd(2) != 0 ? 768 : -768;
}

// @Ok
CFlamingImpactWeb::~CFlamingImpactWeb(void)
{
}

// @Ok
CEmber::~CEmber(void)
{
}

// @Ok
void Simby_CreatePunchOb(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(stack[0]);
	i32 v3 = static_cast<i32>(stack[1]);

	*result = reinterpret_cast<u32>(new CPunchOb(v2, v3));
}

// @Ok
void Simby_CreateSimbyDroplet(const u32 *stack, u32 *result)
{
	i16* v2 = reinterpret_cast<i16*>(stack[0]);
	i32 v3 = static_cast<i32>(stack[1]);

	*result = reinterpret_cast<u32>(new CSimbyDroplet(v2, v3));
}

// @Ok
void CSymBurn::AI(void)
{
	if ( G_CAMERA_LIST )
		this->mAngles.vy = G_CAMERA_LIST->field_236 + 2048;
	this->mScale.vx = 3000;
	this->mScale.vz = 3000;

	if ( ++this->field_1A4 > 60 )
	{
		i32 v4 = (this->mRGB & 0xFF) - 4;
		if ( v4 < 0 )
			v4 = 0;
		this->mRGB = v4 | ((v4 | (v4 << 8)) << 8);

		this->mScale.vy -= 75;
		if ( this->mScale.vy < 0 )
			this->mScale.vy = 0;

		if ( !v4 || !this->mScale.vy )
		{
			this->Die();
		}
	}
	else
	{
		i32 v7 = (this->mRGB & 0xFF) - 129;
		if ( v7 < 128 )
			v7 = 128;

		this->mRGB = v7 | ((v7 | (v7 << 8)) << 8);

		this->mScale.vy += 800;
		if ( this->mScale.vy > 4096 )
			this->mScale.vy = 4096;
	}

	M3d_BuildTransform(this);
}

// guess: live CSymBurn instance counter, no idb_globals.txt entry nearby.
// Found because CSymBurn::CSymBurn has no standalone address in the
// original: it is fully inlined into CSimbyFireDeath::CSimbyFireDeath
// (sub_4A3640, not yet in this repo), which is where this address was found.
static i32 * const gSymBurnCount = reinterpret_cast<i32*>(0x60CF94);

// @Ok
// The destructor (sub_4A31B0) has its own address, verified directly.
CSymBurn::~CSymBurn(void)
{
	this->DeleteFrom(&MiscList);
	(*gSymBurnCount) -= 1;
}


// @Ok
// No standalone address in the original: fully inlined into
// CSimbyFireDeath::CSimbyFireDeath (sub_4A3640, not yet in this repo).
// Verified this body's statement order and field offsets against that
// inlined code.
CSymBurn::CSymBurn(CVector *a2)
{
	this->mPos = *a2;
	this->InitItem("fire");
	this->mFlags |= 0x602u;
	this->mScale.vy = 0;
	this->mRGB = 0xFFFFFF;

	this->AttachTo(&MiscList);
	(*gSymBurnCount) += 1;
}

// @Ok
void CSimbyDroplet::Move(void)
{
	this->mPos.vy += this->mVel.vy;
	this->mVel.vy += this->field_6A;
	this->mAge++;

	if (this->mPos.vy > this->field_6C || this->mAge > 60)
	{
		Trig_SendPulse(reinterpret_cast<u16*>(&G_OFFSETLIST[this->field_68][3]));
		this->Die();
	}
}

// @Ok
CSimbyDroplet::~CSimbyDroplet(void)
{
}

// @Ok
// verified against IDA decompile of sub_4A3B50 (0x4A3B50), statement order
// and field offsets match. print_if_false compiles to a real call to an
// empty function in the original (an SEH-protected constructor, see
// CLAUDE.md new-T-with-cleanup-frame note), so it is a functional no-op here too.
CSimbyDroplet::CSimbyDroplet(i16* a2, i32 NodeIndex)
{
	print_if_false(NodeIndex != 0xFFFF, "Bad NodeIndex sent to CSimbyDroplet");
	this->field_68 = NodeIndex;

	CVector *pPos = reinterpret_cast<CVector*>(a2);
	this->mPos.vx = pPos->vx << 12;
	this->mPos.vy = pPos->vy << 12;
	this->mPos.vz = pPos->vz << 12;

	this->SetTexture(*(Texture **)(*reinterpret_cast<i32*>(0x56EAC4) + 12));

	this->field_6A = a2[12];
	this->SetScale(800);
	this->SetSemiTransparent();
	this->field_6C = Web_GetGroundY(&this->mPos);
	this->mType = 14;
}

// @Ok
void CFireySpark::Move(void)
{
	this->mPos.vx += this->mVel.vx;
	this->mPos.vy += this->mVel.vy;
	this->mPos.vz += this->mVel.vz;

	if ( this->mPos.vy > this->field_4C )
	{
		this->mPos.vy = this->field_4C;
		this->mVel.vx >>= 1;
		this->mVel.vz >>= 1;
	}

	this->mVel.vy += 29584;

	if ( this->r0 >= this->field_48 )
		this->r0 -= this->field_48;
	else
		this->r0 = 0;

	if ( this->b0 >= this->field_48 )
		this->b0 -= this->field_48;
	else
		this->b0 = 0;

	if ( this->g0 >= this->field_48 )
		this->g0 -= this->field_48;
	else
		this->g0 = 0;

	if ( !(this->r0 | (this->g0 | this->b0)) )
		this->Die();
}

// @Ok
CFireySpark::~CFireySpark(void)
{
}

// @Ok
CFireySpark::CFireySpark(CVector* a2, CVector* a3, i32 a4)
{
	this->mPos = *a2;
	this->mVel = *a3;

	this->field_4C = a4;

	if (Rnd(2))
	{
		this->code = 106;
		this->tag = 0x2000000;
		this->mWidthHeight = 1;
	}
	else
	{
		this->code = 98;
		this->tag = 50331648;
		i32 v6 = Rnd(2) + 2;
		this->mWidthHeight = (v6 << 16) | (v6 + 1);
	}

	this->r0 = -1;
	this->g0 = 0x80;
	this->b0 = 0;
	this->field_48 = Rnd(10) + 10;
	this->mType = 16;
}

// @Ok
void CPunchOb::AI(void)
{
	if (this->pMessage)
		this->CleanUpMessages(1, 0);

	if (this->mAnimFinished)
		this->RunAnim(0, 0, -1);

	M3d_BuildTransform(this);

	if ( !(G_ATTACK_RELATED & 0xF)
			&& this->field_31C.bothFlags != 1
			&& Mem_RecoverPointer(&this->field_104))
	{
		this->field_31C.bothFlags = 1;
		this->dumbAssPad = 0;
	}

	switch (this->field_31C.bothFlags)
	{
		case 1:
			switch (this->dumbAssPad)
			{
				case 0:
					this->field_1F8 += this->mElapsedTicks;
					if (this->field_1F8 > 180)
					{
						this->mFlags |= 0x200;
						this->dumbAssPad = 1;
					}
					break;
				case 1:
					this->mScale.vz += 512;

					this->mScale.vy = this->mScale.vz;
					this->mScale.vx = this->mScale.vz;

					if (this->mScale.vz >= 6144)
					{
						CTrapWebEffect *pTrap = reinterpret_cast<CTrapWebEffect*>(Mem_RecoverPointer(&this->field_104));

						if (pTrap)
							pTrap->Burst();

						this->dumbAssPad++;
					}
					break;
				case 2:
					this->mScale.vz -= 256;

					this->mScale.vy = this->mScale.vz;
					this->mScale.vx = this->mScale.vz;

					if (this->mScale.vz <= 4096)
					{
						this->mFlags &= 0xFDFF;
						this->field_31C.bothFlags = 0;
						this->dumbAssPad = 0;
					}

					break;
				default:
					print_if_false(0, "Unknown substate.");
					break;
			}
			break;
	}

	if (this->mInputFlags & 1)
	{
		SHitInfo v9;
		v9.field_8 = this->mHealth;
		v9.field_C.vx = 0;
		v9.field_C.vy = 0;
		v9.field_C.vz = 0;

		v9.field_0 = 4;
		this->Hit(&v9);
	}
}

// @Ok
// verified against IDA decompile+disasm of sub_4A57E0 (0x4A57E0). Not a
// named function in tools/names.json ("CSimbyDrop" is our own guess, going
// off this file's naming convention for small debris/particle classes);
// mType=13 and the CQuadBit-derived base ctor call are directly confirmed.
CSimbyDrop::CSimbyDrop(CVector *a2, CVector *a3, i32 a4, i32 a5)
{
	this->SetTexture(*reinterpret_cast<Texture**>(*reinterpret_cast<i32*>(0x56EAC4) + 4));
	this->SetTint(0x3B, 0xB, 0x37);
	this->SetSemiTransparent();

	this->field_88 = a4;

	this->mVel = *a3;

	this->mPosC = *a2;
	this->mPos = *a2;

	CVector facing;
	Utils_CalcUnitFacingCamera(&this->mPosC, &this->mPos, &facing);

	this->mPosD = this->mPosC + facing * 10;
	this->mPosB = this->mPos + facing * 10;

	this->field_84 = 1;
	this->field_8C = a5;
	this->mType = 13;
}

// @Ok
// verified against IDA decompile+disasm of sub_4A6D50 (0x4A6D50). Spawns
// one CGlowFlash at the impact point plus a3 CSimbyDrop debris particles
// scattered in a cone around the a2 direction, using the same rcossin_tbl
// two-rotation scatter idiom already established elsewhere in this repo
// (carnage.cpp CSymbioteBlade::GenerateControlPoints; bit.cpp): the first
// rotation picks a random point on a circle perpendicular to a2 (using the
// Utils_CalcWallPerps basis), the second tilts that back toward a2 by a
// second random angle, giving a random unit-ish direction inside a cone
// around a2. Both allocations are the original's manual operator-new +
// null-check + placement-new (see CSwinger_SwingBack in web.cpp for the
// established pattern in this repo), matching the original exactly:
// unlike CSimbyDrop above, the CGlowFlash allocation failure path is NOT
// guarded before the mAngle write that follows it in the original disasm
// (same in the very similar CSimbyShot::Move, sub_4A6520, not yet in this
// repo) -- reproduced faithfully rather than "fixed", per this repo's
// dead-code-preservation convention.
void Simby_SplattyExplosion(CVector *a1, CVector *a2, i32 a3)
{
	CGlowFlash *pFlash = static_cast<CGlowFlash*>(CBit::operator new(sizeof(CGlowFlash)));

	if (pFlash)
	{
		::new (pFlash) CGlowFlash(
				a1, 5, 255, 255, 255, 0, 100, 0, 100, 0, 9, 0, 1, 36, 120, 18, 60, 1, 2);
	}

	pFlash->mAngle = Rnd(4096);

	CVector perpUp;
	CVector perpSide;
	perpUp.vx = 0; perpUp.vy = 0; perpUp.vz = 0;
	perpSide.vx = 0; perpSide.vy = 0; perpSide.vz = 0;
	Utils_CalcWallPerps(a2, &perpUp, &perpSide);

	CVector groundCheckPos = *a1 + (*a2 * 100);
	i32 groundY = Web_GetGroundY(&groundCheckPos);

	i32 surfaceVal = 0;
	if (G_LINE_INFO.pItem && (G_LINE_INFO.pItem->mFlags & 0x100))
	{
		surfaceVal = *reinterpret_cast<i32*>(reinterpret_cast<u8*>(G_LINE_INFO.pItem) + 100);
	}

	for (i32 i = a3; i != 0; i--)
	{
		i32 angle1 = (Rnd(1601) - 1824) & 0xFFF;

		i32 sin1 = G_RCOSSIN_TBL[angle1].sin;
		i32 cos1 = G_RCOSSIN_TBL[angle1].cos;

		CVector circleOffset = ((sin1 * perpSide) + (cos1 * perpUp)) >> 12;

		i32 angle2 = Rnd(512) & 0xFFF;

		i32 sin2 = G_RCOSSIN_TBL[angle2].sin;
		i32 cos2 = G_RCOSSIN_TBL[angle2].cos;

		CVector coneOffset = ((sin2 * (*a2)) + (cos2 * circleOffset)) >> 12;

		coneOffset *= (Rnd(20) + 20);

		CSimbyDrop *pDrop = static_cast<CSimbyDrop*>(CBit::operator new(sizeof(CSimbyDrop)));

		if (pDrop)
		{
			::new (pDrop) CSimbyDrop(a1, &coneOffset, groundY, surfaceVal);
		}
	}
}

// @Ok
i32 CPunchOb::Hit(SHitInfo* pHitInfo)
{
	if (this->mHealth <= 0)
		return 0;

	if (pHitInfo->field_0 & 2 && pHitInfo->field_4 == 7)
		pHitInfo->field_8 = this->mHealth;

	if (pHitInfo->field_0 & 4)
	{
		this->mHealth -= pHitInfo->field_8;

		i32 v7 = 8;

		CVector v9;
		v9.vx = 0;
		v9.vy = 0;
		v9.vz = 0;

		Utils_GetVecFromMagDir(&v9, 4096, &this->mAngles);
		v9 >>= 12;

		if (this->mHealth <= 0)
		{
			v7 = 16;
			this->SendPulse();
			this->Die(0);
		}
		Simby_SplattyExplosion(&this->mPos, &v9, v7);
	}

	return 1;
}

// @Ok
CPunchOb::~CPunchOb(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
}

// @Ok
CPunchOb::CPunchOb(
		i16* a2,
		i32 a3)
{
	this->InitItem("sym_gen");
	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->mCBodyFlags |= 0x10;
	this->mNode = a3;

	this->mType = 412;
	this->field_2A8 |= 2;
	this->mRMinor = 100;

	u16* v6 = reinterpret_cast<u16*>(
			this->SquirtAngles(reinterpret_cast<i16*>(
				this->SquirtPos(a2))
			));
	this->RunAnim(0, 0, -1);
	this->ParseScript(v6);
}

// guess: pointer to a table of Texture* entries used by simby "shot"/
// "splat" effects. Same double-deref access pattern as G_TEXTURE_RELATED
// in effects.cpp (dereference the global once to get a table base, then
// index+dereference again), just a different table 0x20 bytes earlier in
// the binary. Evidence: every xref to 0x56EA7C is one of CSimbyShot::
// CSimbyShot (sub_4A5FD0), CSimbyShotSplat::CSimbyShotSplat (sub_4A5C70),
// CSimbyEmergeSplat::CSimbyEmergeSplat (sub_4A2BA0) and CSimbyMeltSplat::
// CSimbyMeltSplat (sub_4A32E0), all reading *(Texture**)(table + 4).
static i32 * const gSimbyShotTextureTable = reinterpret_cast<i32*>(0x56EA7C);

// @Ok
// verified against IDA decompile+disasm of sub_4A5FD0 (0x4A5FD0): every
// field offset, the raycast-toward-MechList setup (identical gLineInfo/
// LineOfSightCheck idiom to CFlamingImpactWeb::CFlamingImpactWeb above),
// and the mPos/mPosB/mPosC/mPosD quad construction. CSimbyShot::Move
// (sub_4A6520, not yet in this repo) independently confirms every field
// offset used here (it re-derives mPos/mPosC from field_94/field_A0/
// field_AC/field_B0/field_B4 the same way every frame).
CSimbyShot::CSimbyShot(CVector *a2)
{
	this->field_88.vx = 0;
	this->field_88.vy = 0;
	this->field_88.vz = 0;

	this->field_94.vx = 0;
	this->field_94.vy = 0;
	this->field_94.vz = 0;

	this->field_A0.vx = 0;
	this->field_A0.vy = 0;
	this->field_A0.vz = 0;

	if (!G_MECHLIST_PLAYER)
	{
		this->Die();
		return;
	}

	this->SetTexture(*reinterpret_cast<Texture**>(*gSimbyShotTextureTable + 4));
	this->SetSemiTransparent();
	this->SetTint(0x64, 0, 100);

	CVector toMech = G_MECHLIST_PLAYER->mPos - *a2;
	i32 length = toMech.Length();
	this->field_B4 = length;

	if (length == 0)
	{
		this->Die();
		return;
	}

	CVector unitDir = toMech / length;
	this->field_A0 = unitDir;

	CVector hitPoint = *a2 + unitDir * 5000;

	G_LINE_INFO.StartCoords = *a2;
	G_LINE_INFO.EndCoords = hitPoint;

	M3dColij_InitLineInfo(&G_LINE_INFO);

	G_LINE_OF_SIGHT_CHECK = 1;
	M3dZone_LineToItem(&G_LINE_INFO, 0);
	G_LINE_OF_SIGHT_CHECK = 0;

	if (G_LINE_INFO.pItem)
	{
		print_if_false((G_LINE_INFO.pItem->mFlags & 0x10) == 0, "Hit env obj!");

		CItem *pEnviro;
		for (pEnviro = EnviroList; pEnviro; pEnviro = pEnviro->mNextItem)
		{
			if (pEnviro == G_LINE_INFO.pItem)
				break;
		}
		print_if_false(pEnviro != 0, "Not in list");

		this->field_84 = 1;
		this->field_88.vx = G_LINE_INFO.Normal.vx;
		this->field_88.vy = G_LINE_INFO.Normal.vy;
		this->field_88.vz = G_LINE_INFO.Normal.vz;

		hitPoint = G_LINE_INFO.Position;
	}

	CVector toHit = hitPoint - *a2;
	length = toHit.Length();
	this->field_B4 = length;

	if (length == 0)
	{
		this->Die();
		return;
	}

	this->field_B0 = -Rnd(200);
	this->field_AC = this->field_B0 - 250;

	this->field_94 = *a2;

	if (this->field_AC >= 0)
	{
		i32 dist = (this->field_AC <= this->field_B4) ? this->field_AC : this->field_B4;
		this->mPos = this->field_94 + this->field_A0 * dist;
	}
	else
	{
		this->mPos = *a2;
	}

	if (this->field_B0 >= 0)
	{
		i32 dist = (this->field_B0 <= this->field_B4) ? this->field_B0 : this->field_B4;
		this->mPosC = this->field_94 + this->field_A0 * dist;
	}
	else
	{
		this->mPosC = this->field_94;
	}

	CVector facing;
	Utils_CalcUnitFacingCamera(&this->mPos, &this->mPosC, &facing);

	this->mPosB = this->mPos + facing * 20;
	this->mPosD = this->mPosC + facing * 20;

	this->mType = 22;
}

// @Ok
void CSimby::SetUpHandPos(void)
{
	SHook v8;
	v8.Offset = 14;

	CVector a3;
	a3.vx = 0;
	a3.vy = 0;
	a3.vz = 0;

	CVector a2;
	a2.vx = 0;
	a2.vy = 0;
	a2.vz = 0;

	v8.Part.vz = 0;
	v8.Part.vy = 0;
	v8.Part.vx = 0;

	M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&a3),
			this,
			&v8);
	v8.Offset = 11;
	M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&a2),
			this,
			&v8);

	this->field_3DC = (a2 + a3) >> 1;
}

// @Ok
// verified against IDA decompile of sub_4ACFB0 (0x4ACFB0), all four
// substates match (offsets, constants, call args). Case 2 has SetUpHandPos's
// body inlined in the original; kept as a call to the already-decompiled
// SetUpHandPos here, functionally identical.
void CSimby::Shoot(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->Neutralize();
			this->CycleAnim(this->field_298.Bytes[0], 1);
			new CAIProc_LookAt(
					this,
					G_MECHLIST_PLAYER,
					0,
					2,
					80,
					0);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->field_288 & 2)
			{
				this->field_288 &= 0xFFFFFFFD;
				this->RunAnim(0x2B, 0, -1);
				this->dumbAssPad++;
			}
			break;
		case 2:
			if (this->mFrame >= 14)
			{
				this->SetUpHandPos();
				new CSimbyShot(&this->field_3DC);
				SFX_PlayPos(0x815C, &this->mPos, 0);
				this->dumbAssPad++;
			}
			break;
		case 3:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 4;
				this->field_324 = 450 - Rnd(150);
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate.");
			break;
	}
}

// @Ok
void CSimby::TakeHit(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_310 = 0;

			new CAIProc_LookAt(
					this,
					G_MECHLIST_PLAYER,
					0,
					0,
					80,
					200);

			this->RunAppropriateHitAnim();
			this->field_230 = 10;
			this->dumbAssPad++;
			break;
		case 1:
			this->RunTimer(&this->field_230);
			if (!this->field_230)
			{
				this->RunAnim(this->field_298.Bytes[0], 0, -1);
				this->field_31C.bothFlags = 4;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate!");
			break;
	}
}

// @Ok
// No standalone address in the original: fully inlined into CSimby::AI
// (sub_4AE3D0, not yet in this repo). Verified statement order and field
// offsets (field_34C at 0x34C, gSimbyFlags check bit 25) against that
// inlined block.
void CSimby::PlayGruntSound(void)
{
	if (this->CheckStateFlags(&G_SIMBY_FLAGS, 25) & 0x2000)
	{
		this->RunTimer(&this->field_34C);

		if (!this->field_34C)
		{
			this->field_34C = Rnd(180) + 180;

			u32 song;
			switch (Rnd(3))
			{
				case 0:
					song = 349;
					break;
				case 1:
					song = 350;
					break;
				case 2:
					song = 351;
					break;
				default:
					print_if_false(0, "Who's been smokin' crack today?");
					break;
			}

			SFX_PlayPos(song | 0x8000, &this->mPos, 0);
		}
	}
}

// @Ok
i32 CSimby::PlayAndAttachXAPlease(
		i32 a2,
		i32 a3,
		CBody *pBody,
		i32 a5)
{
	if (Redbook_XAPlayPos(a2, a3, &pBody->mPos, a5))
	{
		pBody->AttachXA(a2, a3);
		return 1;
	}

	return 0;
}

// @Ok
// No standalone address in the original: inlined into CSimby::Trapped
// (sub_4A87B0, not yet in this repo, the caller's dumbAssPad > 0 check
// lives there, not here). Verified this function's own body against the
// inlined check (Mem_RecoverPointer at field_104, v2[1048], threshold 40).
i32 CSimby::FireTrappedToDeath(void)
{
	u8 *v2 = reinterpret_cast<u8*>(Mem_RecoverPointer(&this->field_104));
	if (!v2 || !v2[1048] || *(i32*)(*((u32*)v2 + 17) + 60) <= 40)
		return 0;
	this->field_31C.bothFlags = 23;
	this->dumbAssPad = 0;
	return 1;
}

// @Ok
void CSimby::RunAppropriateHitAnim(void)
{
	if (this->field_218 & 0x10)
		this->RunAnim(17, 0, -1);
	else if (this->field_218 & 0x20)
		this->RunAnim(18, 0, -1);
	else
		this->RunAnim(19, 0, -1);
}

// @Ok
void CSimby::SetUpJumpData(i32 a2, i32 a3)
{
	i32 v3;
	if(a3 <= a2)
	{
		v3 = 1;
	}
	else
	{
		v3 = a3 / a2;
	}

	this->field_3CC = v3;

	if (this->field_3CC < 14)
		this->field_3CC = 14;

	this->field_3D0 = 4096 / this->field_3CC;
}

// @Ok
void CSimby::SetAlertModeTimer(int timer)
{
	if (this->field_348 < timer)
		this->field_348 = timer;
}

// @Ok
// Session note (2026-08-30): under the functional-decompilation bar this
// was re-verified field by field against IDA decompile of sub_4A7870
// (0x4A7870): every zeroed field, every constant, every conditional, the
// SquirtPos/SquirtAngles call chain, InitItem/AttachTo args, and the
// gSimbyCount/gSimbyCountResetFlag tail all match. The only known residue
// from prior byte-matching attempts (field_3DC's implicit CVector default
// constructor scheduled between field_394 and field_3F8 in the original,
// vs grouped by this build) is a pure instruction-scheduling artifact, not
// a logic difference, so it does not block @Ok here.
CSimby::CSimby(int* a2, int a3)
{
	this->field_350 = 0;
	this->field_354 = 0;
	this->field_358 = 0;
	this->field_35C = 0;
	this->field_360 = 0;
	this->field_364 = 0;
	this->field_368 = 0;
	this->field_36C = 0;
	this->field_370 = 0;
	this->field_374 = 0;
	this->field_378 = 0;
	this->field_37C = 0;
	this->field_380 = 0;
	this->field_384 = 0;
	this->field_388 = 0;
	this->field_38C = 0;
	this->field_390 = 0;
	this->field_394 = 0;

	this->field_3F8 = 0;
	this->field_3FC = 0;
	this->field_400 = 0;
	this->field_404 = 0;
	this->field_408 = 0;
	this->field_40C = 0;

	u16* v6 = reinterpret_cast<u16*>(
			this->SquirtAngles(reinterpret_cast<i16*>(
				this->SquirtPos(reinterpret_cast<i16*>(a2)))
			));

	this->field_344 = Trig_GetLevelID();
	this->InitItem(this->field_344 == 0x803 ? "sym_dark" : "symbi_02");
	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));

	this->field_2A8 |= 0x201;
	this->field_21E = 0x64;
	this->field_1F4 = a3;
	this->mNode = a3;
	this->mRMinor = 0x8C;
	this->field_230 = 0;
	this->field_216 = 0x20;
	this->mPushVal = 0x40;
	this->field_31C.bothFlags = 0;
	this->mType = 324;
	this->mHealth = 0x320;

	this->field_294.Int = gSimbySetup[0];
	this->field_298.Int = gSimbySetup[1];

	this->field_3EC = G_ATTACK_RELATED - 155;

	this->field_34C = Rnd(300);

	this->field_3B8 = 0xDAC;
	this->field_3BC = 0x190;
	this->field_3C0 = 0x555;

	this->field_30C = 0x64;

	M3dUtils_ReadLinksPacket(this, reinterpret_cast<void*>(0x5554A0));

	this->ParseScript(v6);

	if (this->field_218 & 0x100000)
	{
		this->field_3A0 |= 0x10;
		this->mFlags |= 1;
	}

	if (Trig_GetLevelID() == 0x702)
		this->field_218 |= 0x400000;

	if (!*gSimbyCount)
		MakeVertexWibbler();

	i32 v7 = G_ATTACK_RELATED;
	(*gSimbyCount)++;

	if (v7 < 0x3C)
		*gSimbyCountResetFlag = 0;
}

// @Ok
// verified against IDA decompile of sub_4A7740 (0x4A7740): this default
// constructor only zeroes fields, sets InitItem("symbi_02") unconditionally
// (no level-id branch, unlike the other constructor), sets mType, and does
// the gSimbyCount/MakeVertexWibbler dance. No AttachTo call in the
// original either, matches.
CSimby::CSimby(void)
{
	this->field_350 = 0;
	this->field_354 = 0;
	this->field_358 = 0;
	this->field_35C = 0;
	this->field_360 = 0;
	this->field_364 = 0;

	this->field_368 = 0;
	this->field_36C = 0;
	this->field_370 = 0;

	this->field_374 = 0;
	this->field_378 = 0;
	this->field_37C = 0;
	this->field_380 = 0;
	this->field_384 = 0;
	this->field_388 = 0;
	this->field_38C = 0;
	this->field_390 = 0;
	this->field_394 = 0;
	this->field_3DC.vx = 0;
	this->field_3DC.vy = 0;
	this->field_3DC.vz = 0;

	this->field_3F8 = 0;
	this->field_3FC = 0;
	this->field_400 = 0;
	this->field_404 = 0;
	this->field_408 = 0;
	this->field_40C = 0;

	this->InitItem("symbi_02");
	this->mType = 324;

	if (!*gSimbyCount)
		MakeVertexWibbler();

	(*gSimbyCount)++;
}

// @Ok
void Simby_CreateSimby(const unsigned int *stack, unsigned int *result)
{
	int* v2 = reinterpret_cast<int*>(*stack);
	int v3 = static_cast<int>(stack[1]);

	if (v2)
	{
		*result = reinterpret_cast<unsigned int>(new CSimby(v2, v3));
	}
	else
	{
		*result = reinterpret_cast<unsigned int>(new CSimby());
	}
}

// @Ok
// @Matching
void MakeVertexWibbler(void)
{
	u8 v1[6];
	v1[0] = 2;
	v1[1] = 3;
	v1[2] = 4;
	v1[3] = 9;
	v1[4] = 8;
	v1[5] = 7;

	new CVertexWobble(
			Spool_FindRegion(Trig_GetLevelID() == 0x803 ? "sym_dark" : "symbi_02"),
			1, 6, v1, 0x50, 0x50, 0xC8, 0x96);

	u8 v2[6];
	v2[0] = 0xB;
	v2[1] = 0xA;
	v2[2] = 0;
	v2[3] = 1;
	v2[4] = 6;
	v2[5] = 5;

	new CVertexWobble(
			Spool_FindRegion(Trig_GetLevelID() == 0x803 ? "sym_dark" : "symbi_02"),
			1, 6, v2, 0x50, 0x50, 0xC8, 0x96);
}

// @Ok
// verified the BYTE0/1/2 color-blend formula against IDA decompile of
// sub_4AF3B0 (0x4AF3B0) term by term, matches. The decompile shows the
// per-byte adds masked/truncated explicitly (u8 cast, u16 cast, &0xFF0000),
// this source relies on the same truncation via the BYTE0/1/2 macros.
void CSimby::FlashUpdate(void)
{
	
#define BYTE0(x) ((x) & 0xFF)
#define BYTE1(x) (BYTE0((x >> 8)))
#define BYTE2(x) (BYTE0((x >> 16)))

	if (this->field_328)
	{
		this->mFlags |= 0x400;

		/*
		this->field_24 = ((this->field_32A + this->field_24) & 0xFF) | (((this->field_32E + (this->field_24 >> 0x10)) << 16) & 0xFF0000) | ((((this->field_24 >> 8) + this->field_32C) << 8) & 0xFF00);
		*/

		this->mRGB = BYTE0(this->mRGB + this->field_32A) | ((BYTE1(this->mRGB) + this->field_32C) << 8) | ((BYTE2(this->mRGB) + this->field_32E) << 16) ;

																							                            

		if (!--this->field_328)
		{

			if (this->field_330 & 0x2000000)
			{
				this->mFlags |= 0x400;
			}
			else
			{
				this->mFlags &= 0xFBFF;
			}

			this->mRGB = this->field_330;
			this->field_330 = 0;
		}
	}
}

// @Ok
// 0x4AF260 (326 bytes), CSimby vtable slot 17, Mac Flash__6CSimbyFiUcUcUc.
// Starts a colour flash that FlashUpdate above plays back: frames > 0
// fades from (r,g,b) back to the 0x20 grey over `frames` steps and saves
// the current mRGB (plus its 0x400 flag state in bit 25) in field_330 so
// FlashUpdate can restore it; frames < 0 fades from grey towards (r,g,b);
// frames == 0 sets the colour with no fade. All divisions are signed
// (cdq/idiv) and the negative branch divides by the u16-truncated
// negation, reproduced as written.
void CSimby::Flash(i32 frames, u8 r, u8 g, u8 b)
{
	if (frames == 0)
	{
		this->field_328 = 0;
		this->mFlags |= 0x400;
		this->mRGB = r | (g << 8) | (b << 16);
		return;
	}

	if (frames < 0)
	{
		this->mRGB = 0x202020;
		i32 steps = static_cast<u16>(-frames);
		this->field_328 = static_cast<i16>(-frames);
		this->field_32A = static_cast<u16>((r - 32) / steps);
		this->field_32C = static_cast<u16>((g - 32) / steps);
		this->field_32E = static_cast<i16>((b - 32) / steps);
		this->mFlags |= 0x400;
		return;
	}

	this->field_328 = static_cast<i16>(frames + 1);
	this->field_32A = static_cast<u16>((32 - r) / frames);
	this->field_32C = static_cast<u16>((32 - g) / frames);
	this->field_32E = static_cast<i16>((32 - b) / frames);

	if ((this->field_330 & 0x1000000) == 0)
	{
		this->field_330 = (this->mRGB & 0xFFFFFF) | 0x1000000;
		if (this->mFlags & 0x400)
			this->field_330 |= 0x2000000;
	}

	this->mFlags |= 0x400;
	this->mRGB = r | (g << 8) | (b << 16);
}

// @Ok
// 0x4A7E70 (16 bytes), CSimby vtable slot 18. Just raises bit 0x8000 of
// field_218 (the CBaddy state flags); CSimby::AI picks the flag up. The
// original returns the new flag word in eax, which no caller reads.
void CSimby::ExplosionReaction(void)
{
	this->field_218 |= 0x8000;
}

// @Ok
void CSimbySlimeBase::ScaleUp(void)
{
	this->field_A4 = 32;
}

// @Ok
void CSimbySlimeBase::ScaleDown(void)
{
	this->field_A4 = -32;
}

// @Ok
void CSimbySlimeBase::ScaleDownAndDie(void)
{
	this->ScaleDown();
	this->field_9C = 1;
	this->mProtected = 0;
}

// gSimbyAttackData, confirmed in idb_globals.txt at 0x682C60.
static i32 * const gSimbyAttackData = reinterpret_cast<i32*>(0x682C60);

// @Ok
// No standalone address in the original: inlined into CSimby::AI
// (sub_4AE3D0, not yet in this repo, "dword_682C60 &= ~this->field_3F0"
// at 0x4ae6d7). Verified this function's body against that inlined code.
// Was a plain repo-local static before; switched to the confirmed fixed
// game address since it is shared with CSimby::AI.
void CSimby::ClearAttackData(void)
{
	*gSimbyAttackData &= ~this->field_3F0;
	this->field_3F0 = 0;
}

// @Ok
 void CPunchOb::SendPulse(void)
{
	if (!this->field_328)
	{
		this->field_328 = 1;
		Trig_SendPulse(reinterpret_cast<u16*>(Trig_GetLinksPointer(this->mNode)));
	}
}

// @Ok
void CSimby::SetUpUnitFromDirection(CVector* a2, i32 a3)
{
	CSVector v4;
	v4.vy = a3;
	v4.vx = 0;
	v4.vz = 0;

	Utils_GetVecFromMagDir(a2, 1, &v4);
}


// @Ok
// verified against IDA decompile of sub_4A28C0 (0x4A28C0): field_68.vx was
// wrongly copied from mPos.vz, fixed to mPos.vx. SetTexture call was missing,
// added back (reads a Texture* from a table at 0x56EA9C, offset 44, same
// pattern as CSimbyDroplet's texture lookup at 0x56EAC4).
CEmber::CEmber(const CVector* a2, i32 a3)
{
	this->field_68.vx = 0;
	this->field_68.vy = 0;
	this->field_68.vz = 0;

	this->mPos = *a2;

	this->field_68.vx = this->mPos.vx;
	this->field_68.vz = this->mPos.vz;

	this->field_78.vx = Rnd(10) + 10;
	this->field_78.vy = Rnd(4096);
	this->field_78.vz = Rnd(4096);

	this->SetTexture(*(Texture **)(*reinterpret_cast<i32*>(0x56EA9C) + 44));

	this->mScale = Rnd(200) + 350;
	this->field_84 = 255;
	this->field_88 = 128;
	this->field_8C = 0;

	this->SetTint(0xFFu, 128, 0);
	this->SetSemiTransparent();
	this->field_74 = (a3 * (Rnd(5) + 5)) >> 8;

	this->mVel.vy = (a3 * (Rnd(5) + 6)) << 12 >> 8;
}

// @Ok
void Simby_CreateEmber(const u32* a1, u32*)
{
	const CVector *vec = reinterpret_cast<const CVector*>(a1);
	new CEmber(vec, vec->vy);
}

// @Ok
// @Validate
CSkidMark::CSkidMark(void)
{
	this->SetTexture(gAnimTable[25]->pTexture);
	this->SetSemiTransparent();
	this->SetTint(0x2Fu, 9, 44);
	this->mType = 18;
}

// @Ok
void CSkidMark::Move(void)
{
	if (++this->mAge > 40)
	{
		Bit_ReduceRGB(&this->mTint, 2);
		if (!(0xFFFFFF & this->mTint))
			this->Die();
	}
}

void validate_CPunchOb(void){
	VALIDATE_SIZE(CPunchOb, 0x32C);

	VALIDATE(CPunchOb, field_328, 0x328);
}

void validate_CSimbyDrop(void){
	VALIDATE_SIZE(CSimbyDrop, 0x90);

	VALIDATE(CSimbyDrop, field_84, 0x84);
	VALIDATE(CSimbyDrop, field_88, 0x88);
	VALIDATE(CSimbyDrop, field_8C, 0x8C);
}

void validate_CSimby(void){
	VALIDATE_SIZE(CSimby, 0x460);
	VALIDATE_VTABLE(CSimby, Flash, 17);
	VALIDATE_VTABLE(CSimby, ExplosionReaction, 18);

	VALIDATE(CSimby, field_324, 0x324);

	VALIDATE(CSimby, field_328, 0x328);
	VALIDATE(CSimby, field_32A, 0x32A);
	VALIDATE(CSimby, field_32C, 0x32C);
	VALIDATE(CSimby, field_32E, 0x32E);
	VALIDATE(CSimby, field_330, 0x330);

	VALIDATE(CSimby, field_344, 0x344);

	VALIDATE(CSimby, field_348, 0x348);
	VALIDATE(CSimby, field_34C, 0x34C);

	VALIDATE(CSimby, field_350, 0x350);
	VALIDATE(CSimby, field_354, 0x354);
	VALIDATE(CSimby, field_358, 0x358);
	VALIDATE(CSimby, field_35C, 0x35C);
	VALIDATE(CSimby, field_360, 0x360);
	VALIDATE(CSimby, field_364, 0x364);

	VALIDATE(CSimby, field_368, 0x368);
	VALIDATE(CSimby, field_36C, 0x36C);
	VALIDATE(CSimby, field_370, 0x370);

	VALIDATE(CSimby, field_374, 0x374);
	VALIDATE(CSimby, field_378, 0x378);
	VALIDATE(CSimby, field_37C, 0x37C);
	VALIDATE(CSimby, field_380, 0x380);
	VALIDATE(CSimby, field_384, 0x384);
	VALIDATE(CSimby, field_388, 0x388);
	VALIDATE(CSimby, field_38C, 0x38C);
	VALIDATE(CSimby, field_390, 0x390);
	VALIDATE(CSimby, field_394, 0x394);

	VALIDATE(CSimby, field_398, 0x398);

	VALIDATE(CSimby, field_39C, 0x39C);

	VALIDATE(CSimby, field_3A0, 0x3A0);

	VALIDATE(CSimby, field_3B8, 0x3B8);
	VALIDATE(CSimby, field_3BC, 0x3BC);
	VALIDATE(CSimby, field_3C0, 0x3C0);

	VALIDATE(CSimby, field_3CC, 0x3CC);
	VALIDATE(CSimby, field_3D0, 0x3D0);

	VALIDATE(CSimby, field_3DC, 0x3DC);

	VALIDATE(CSimby, field_3EC, 0x3EC);

	VALIDATE(CSimby, field_3F0, 0x3F0);

	VALIDATE(CSimby, field_3F8, 0x3F8);
	VALIDATE(CSimby, field_3FC, 0x3FC);
	VALIDATE(CSimby, field_400, 0x400);
	VALIDATE(CSimby, field_404, 0x404);
	VALIDATE(CSimby, field_408, 0x408);
	VALIDATE(CSimby, field_40C, 0x40C);
}

void validate_CSimbyBase(void){
	VALIDATE_SIZE(CSimbyBase, 0x334);
}

void validate_CSimbySlimeBase(void)
{
	VALIDATE_SIZE(CSimbySlimeBase, 0x114);
	
	VALIDATE(CSimbySlimeBase, field_9C, 0x9C);
	VALIDATE(CSimbySlimeBase, field_A4, 0xA4);
}

void validate_CEmber(void)
{
	VALIDATE_SIZE(CEmber, 0x90);

	VALIDATE(CEmber, field_68, 0x68);
	VALIDATE(CEmber, field_74, 0x74);
	VALIDATE(CEmber, field_78, 0x78);
	VALIDATE(CEmber, field_84, 0x84);
	VALIDATE(CEmber, field_88, 0x88);
	VALIDATE(CEmber, field_8C, 0x8C);
}

void validate_CSimbyShot(void)
{
	VALIDATE_SIZE(CSimbyShot, 0xB8);

	VALIDATE(CSimbyShot, field_84, 0x84);
	VALIDATE(CSimbyShot, field_88, 0x88);
	VALIDATE(CSimbyShot, field_94, 0x94);
	VALIDATE(CSimbyShot, field_A0, 0xA0);
	VALIDATE(CSimbyShot, field_AC, 0xAC);
	VALIDATE(CSimbyShot, field_B0, 0xB0);
	VALIDATE(CSimbyShot, field_B4, 0xB4);
}

void validate_CSkidMark(void)
{
	VALIDATE_SIZE(CSkidMark, 0x84);
}

void validate_CFireySpark(void)
{
	VALIDATE_SIZE(CFireySpark, 0x50);

	VALIDATE(CFireySpark, field_48, 0x48);

	VALIDATE(CFireySpark, field_4C, 0x4C);
}

void validate_CSimbyDroplet(void)
{
	VALIDATE_SIZE(CSimbyDroplet, 0x70);

	VALIDATE(CSimbyDroplet, field_68, 0x68);
	VALIDATE(CSimbyDroplet, field_6A, 0x6A);
	VALIDATE(CSimbyDroplet, field_6C, 0x6C);
}

void validate_CSymBurn(void)
{
	VALIDATE_SIZE(CSymBurn, 0x1A8);

	VALIDATE(CSymBurn, field_1A4, 0x1A4);

	VALIDATE_VTABLE(CSymBurn, Die, 1);
	VALIDATE_VTABLE(CSymBurn, AI, 2);
}

void validate_CFlamingImpactWeb(void)
{
	VALIDATE_SIZE(CFlamingImpactWeb, 0x90);

	VALIDATE(CFlamingImpactWeb, field_6C, 0x6C);
	VALIDATE(CFlamingImpactWeb, field_70, 0x70);

	VALIDATE(CFlamingImpactWeb, pItem, 0x74);
	VALIDATE(CFlamingImpactWeb, pFace, 0x78);

	VALIDATE(CFlamingImpactWeb, mLinePos, 0x7C);
	VALIDATE(CFlamingImpactWeb, mLineNormal, 0x88);
}

#include "compatibility/my_patch.h"

// Not hooked, and why:
//
// 1. A constructor stamps our vtable, so any class with a virtual missing on
//    our side is out. CSimby has no AI (exe slot 2, 0x4AE3D0) and no Hit
//    (slot 3, 0x4A8AB0). CEmber, CFlamingImpactWeb, CSimbyDrop and
//    CSimbyShot have no Move. That rules out both CSimby constructors,
//    Simby_CreateSimby, Simby_CreateEmber, Simby_CreateFlamingImpactWeb,
//    Simby_SplattyExplosion (it does "new CSimbyDrop") and the CEmber,
//    CFlamingImpactWeb, CSimbyDrop and CSimbyShot constructors.
//    Simby_RelocatableModuleInit goes with them, it stores the pointers to
//    Simby_CreateSimby, Simby_CreateEmber and Simby_CreateFlamingImpactWeb.
//    MakeVertexWibbler does "new CVertexWobble" and effects.cpp still has no
//    CVertexWobble destructor (0x0043BA70).
//
// 2. CSymBurn::~CSymBurn (0x4A31B0) unlinks from MiscList, which shell.cpp
//    still keeps as a plain repo global. CSymBurn::AI does not touch it, so
//    that one is fine.
//
// 3. CSimby::~CSimby (0x4A7AE0) is real work in the exe but our class has no
//    destructor at all, the compiler generates an empty one.
//
// 4. Simby_TestDrop is empty and the original points it at 0x430880, a shared
//    empty function other modules use too.
//
// 5. SetUpHandPos, Shoot, PlayGruntSound, PlayAndAttachXAPlease,
//    FireTrappedToDeath, RunAppropriateHitAnim, SetUpJumpData,
//    SetAlertModeTimer, ClearAttackData, SetUpUnitFromDirection,
//    CPunchOb::SendPulse, the three CSimbySlimeBase scale helpers and the
//    CSymBurn and CSkidMark constructors have no standalone address, the
//    original inlines them.
//
// @Bogus
void patch_simby(void)
{
	PATCH_PUSH_RET(0x004A2470, Simby_RelocatableModuleClear);
	PATCH_PUSH_RET(0x004A2560, Simby_CreatePunchOb);
	PATCH_PUSH_RET(0x004A25E0, Simby_CreateSimbyDroplet);

	PATCH_PUSH_RET_POLY(0x004A2A10, CEmber::~CEmber, "??1CEmber@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004A3210, CSymBurn::AI, "?AI@CSymBurn@@UAEXXZ");

	PATCH_PUSH_RET_POLY(0x004A3B50, CSimbyDroplet::CSimbyDroplet, "??0CSimbyDroplet@@QAE@PAFH@Z");
	PATCH_PUSH_RET_POLY(0x004A3C40, CSimbyDroplet::~CSimbyDroplet, "??1CSimbyDroplet@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004A3C50, CSimbyDroplet::Move, "?Move@CSimbyDroplet@@UAEXXZ");

	PATCH_PUSH_RET_POLY(0x004A3CB0, CFireySpark::CFireySpark, "??0CFireySpark@@QAE@PAVCVector@@0H@Z");
	PATCH_PUSH_RET_POLY(0x004A3DB0, CFireySpark::~CFireySpark, "??1CFireySpark@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004A3DC0, CFireySpark::Move, "?Move@CFireySpark@@UAEXXZ");

	PATCH_PUSH_RET_POLY(0x004A40C0, CFlamingImpactWeb::~CFlamingImpactWeb, "??1CFlamingImpactWeb@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004A46A0, CSkidMark::Move, "?Move@CSkidMark@@UAEXXZ");

	PATCH_PUSH_RET_POLY(0x004A73B0, CPunchOb::CPunchOb, "??0CPunchOb@@QAE@PAFH@Z");
	PATCH_PUSH_RET_POLY(0x004A7490, CPunchOb::~CPunchOb, "??1CPunchOb@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004A74F0, CPunchOb::Hit, "?Hit@CPunchOb@@UAEHPAUSHitInfo@@@Z");
	PATCH_PUSH_RET_POLY(0x004A75E0, CPunchOb::AI, "?AI@CPunchOb@@UAEXXZ");

	PATCH_PUSH_RET(0x004A8680, SpideyAI_ThrownBySimby);
	PATCH_PUSH_RET(0x004A8720, SpideyAI_WaitForSimbyGrab);
	PATCH_PUSH_RET(0x004AA660, CSimby::SimbyKnockSpideyDown);
	PATCH_PUSH_RET(0x004ADB00, CSimby::TakeHit);
	PATCH_PUSH_RET(0x004AF3B0, CSimby::FlashUpdate);
}
