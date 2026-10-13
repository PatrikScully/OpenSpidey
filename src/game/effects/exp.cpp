#include "game/effects/exp.h"
#include "core/utils.h"
#include "core/game_heap.h"
#include "game/objects/bullet.h"
#include "engine/assets/spool.h"
#include "game/objects/crate.h"
#include "game/effects/shatter.h"

#include "compatibility/validate.h"

#ifndef SPIDEY_STANDALONE
i32 g3DExplosions;
#else
extern i32 g3DExplosions;
#endif
#ifndef SPIDEY_STANDALONE
i32 gWibblingExpCount;
#else
extern i32 gWibblingExpCount;
#endif

// Only exp.cpp touches this counter (16 references in the original, all inside
// the CWibbling3DExplosion constructor, its destructor and Exp_Big3DExplosion,
// e.g. mov ecx,[5FA954h] at 0x0043CF43). It is still a mutable global, so it
// goes through the exe's copy while the subsystem is only half ours.
//#define G_WIBBLING_EXP_COUNT (gWibblingExpCount)
#define G_WIBBLING_EXP_COUNT (*reinterpret_cast<i32*>(0x005FA954))

// @Ok
// @Matching
void C3DExplosion::AI(void)
{
	i32 v4;
	switch (this->field_F8)
	{
		case 0:
			if (this->field_FC)
			{
				this->field_FC -= 1;
			}
			else
			{
				this->mFlags &= ~1u;
				this->field_F8 = 1;
			}
			break;
		case 1:
			this->mPos.vy += this->mVel.vy;

			this->mScale.vx += this->field_104;
			this->mScale.vy = (this->mScale.vx * this->field_108) >> 8;
			this->mScale.vz = this->mScale.vx;
			if (++this->field_100 >= this->field_10C)
				this->field_F8 = 2;
			break;
		case 2:
			this->mPos.vy += this->mVel.vy;

			v4 = (this->mRGB & 0xFF) - this->field_110;
			if (v4 <= 0)
			{
				v4 = 0;
				this->Die();
			}

			this->mRGB = v4 | (v4 << 16) | (v4 << 8);
			this->mScale.vx += this->field_104;
			this->mScale.vy = (this->mScale.vx * this->field_108) >> 8;
			this->mScale.vz = this->mScale.vx;

			break;
		default:
			print_if_false(0, "Bad C3DExplosion mode");
	}
}

// @Ok
// @Test
C3DExplosion::C3DExplosion(
		const CVector* a2,
		char* a3,
		i32 a4,
		i32 a5,
		i32 a6,
		i32 a7,
		i32 a8,
		i32 a9,
		i32 a10,
		i32 a11,
		i32 a12)
{
	this->AttachTo(&G_BULLET_LIST);
	this->InitItem(a3);
	this->mModel = a4;
	this->mPos = *a2;
	this->mFlags |= 0x601u;
	this->mRGB = 0x808080;

	this->field_FC = a5;

	this->mScale.vx = a6;
	this->mScale.vy = (a8 * a6) >> 8;
	this->mScale.vz = a6;

	this->field_108 = a8;
	this->field_104 = a7;
	this->field_10C = a9;
	this->field_110 = a10;

	print_if_false(a10 != 0, "Zero faderate sent to C3DExplosion");
	this->field_F8 = 0;
	if (!a5)
	{
		this->mFlags &= ~1u;
		this->field_F8 = 1;
	}

	this->mVel.vy = -4096 * (a11 + Rnd(a12));
}

// @Ok
// @Matching
 C3DExplosion::~C3DExplosion(void)
{
	this->DeleteFrom(&G_BULLET_LIST);
}

// @Ok
// @Matching
void CGlowFlash::ChooseRadii(void)
{
	for (u32 i = 0; i < this->mNumSections; i++)
	{
		if (i & 1)
			this->mpSections[i].Radius = this->field_62 + Rnd(this->field_64);
		else
			this->mpSections[i].Radius = this->field_66 + Rnd(this->field_68);
	}

	if (this->field_62 >= this->field_6A)
		this->field_62 -= this->field_6A;

	if (this->field_64 >= this->field_6A)
		this->field_64 -= this->field_6A;

	if (this->field_66 >= this->field_6A)
		this->field_66 -= this->field_6A;

	if (this->field_68 >= this->field_6A)
		this->field_68 -= this->field_6A;
}

// @Ok
// @Matching
CGlowFlash::~CGlowFlash(void)
{
}

// @Ok
// @Matching
CGrenadeExplosion::CGrenadeExplosion(const CVector* a2)
{
	Rnd(5);

	C3DExplosion* pExp = new C3DExplosion(a2, "expgrnd", 0, 0, 300, 789, 256, 0, 13, 0, 0);
	this->hExp = Mem_MakeHandle(pExp);

	new C3DExplosion(a2, "expgrnd", 1, 0, 0, 500, 256, 0, 7, 0, 0);
	++G_3D_EXPLOSIONS;
}

// @Ok
// @Matching
void CGrenadeExplosion::Move(void)
{
	if (!Mem_RecoverPointer(&this->hExp))
	{
		this->Die();
	}
}

// @Ok
// @Matching
CGrenadeExplosion::~CGrenadeExplosion(void)
{
	--G_3D_EXPLOSIONS;
}

// @Ok
// @Matching
CGrenadeWave::CGrenadeWave(
		const CVector *a2,
		u8 a3,
		u8 a4,
		u8 a5,
		i32 a6,
		i32 a7)
	: CRipple(a2, a3, a4, a5, 0, 0, 0, 6)
{
	this->field_6C = a6 / a7;

	u8 v13 = a3;
	if (a5 > a3)
		v13 = a5;
	if (a4 > v13)
		v13 = a4;

	this->field_60 = v13 / a7;

	print_if_false(this->field_60 != 0, "Got a zero fade rate for CGrenadeWave");
}

// @Ok
// @Matching
void CGrenadeWave::Move(void)
{
	CRipple::Move();
	this->SetFringeWidth(0, this->field_6C + this->mpFringes->Width);
	this->SetFringeWidth(1u, 50);
}

// @Ok
// @Matching
CGrenadeWave::~CGrenadeWave(void)
{
}

// @Ok
// @Matching
CItemFrag::CItemFrag(u32 *pFace, CVector *pVertices, CVector *pVel, i32 a4)
{
	this->SetSemiTransparent();

	this->mTint = 0x808080;

	this->field_70 = pFace[3];
	this->field_74 = pFace[4];
	this->field_78 = pFace[5];
	this->field_7C = pFace[6];
	this->field_80 = pFace[7];

	u32 idx = pFace[1];
	u8 idx0 = idx & 0xFF;
	u8 idx1 = (idx >> 8) & 0xFF;
	u8 idx2 = (idx >> 16) & 0xFF;
	u8 idx3 = idx >> 24;

	this->field_88 = pVertices[idx0];
	this->field_94 = pVertices[idx1];
	this->field_A0 = pVertices[idx2];
	this->field_AC = pVertices[idx3];

	this->mVel = *pVel;

	i32 lifetime = Rnd(30);
	this->field_84 = a4;

	this->mLifetime = lifetime + 1;
	this->mType = 0x17;
}

// @Ok
// @Matching
void CItemFrag::Move(void)
{
	this->field_88 += this->mVel;
	this->field_94 += this->mVel;
	this->field_A0 += this->mVel;
	this->field_AC += this->mVel;

	this->mVel.vy += 0x2000;

	if (this->field_88.vy > this->field_84)
	{
		i32 delta = this->field_84 - this->field_88.vy;

		this->field_88.vy += delta;
		this->field_94.vy += delta;
		this->field_A0.vy += delta;
		this->field_AC.vy += delta;

		this->mVel.vy = -this->mVel.vy >> 2;
		this->mVel.vx >>= 1;
		this->mVel.vz >>= 1;
	}

	this->mPos = this->field_88;
	this->mPosB = this->field_94;
	this->mPosC = this->field_A0;
	this->mPosD = this->field_AC;

	this->mPos.vx += (Rnd(41) - 20) << 12;
	this->mPos.vz += (Rnd(41) - 20) << 12;
	this->mPosB.vx += (Rnd(41) - 20) << 12;
	this->mPosB.vz += (Rnd(41) - 20) << 12;
	this->mPosC.vx += (Rnd(41) - 20) << 12;
	this->mPosC.vz += (Rnd(41) - 20) << 12;
	this->mPosD.vx += (Rnd(41) - 20) << 12;
	this->mPosD.vz += (Rnd(41) - 20) << 12;

	++this->mAge;
	if (this->mAge >= this->mLifetime)
	{
		this->Die();
		return;
	}

	i32 v = ((this->mLifetime - this->mAge) << 7) / this->mLifetime;
	this->mTint = v | (v << 8) | (v << 16);
}

// @Ok
// @Matching
CItemFrag::~CItemFrag(void)
{
}

// @Ok
CRipple::CRipple(
		const CVector* a2,
		u8 a3,
		u8 a4,
		u8 a5,
		i32 a6,
		i32 a7,
		i32 a8,
		i32 a9) : CGlow(a9, 2)
{
	this->mPos = *a2;

	this->field_5C[0] = a3;
	this->field_5C[1] = a4;
	this->field_5C[2] = a5;

	this->field_60 = a7;
	this->field_64 = 0;
	this->field_68 = a6;

	this->mSkipTriangles = 1;

	this->SetCentreRGB(0, 0, 0);
	this->SetRGB(0, 0, 0);
	this->SetRadius(0);
	this->SetFringeWidth(0, a8);
	this->SetFringeWidth(1u, a8);
	this->SetFringeRGB(0, this->field_5C[0], this->field_5C[1], this->field_5C[2]);
	this->SetFringeRGB(1u, 0, 0, 0);
	this->mAngle = Rnd(1024);
}

// @Ok
// @Matching
void CRipple::Move(void)
{
	this->field_64 += this->field_68;

	if (this->field_5C[0] >= this->field_60)
		this->field_5C[0] -= this->field_60;
	else
		this->field_5C[0] = 0;

	if (this->field_5C[1] >= this->field_60)
		this->field_5C[1] -= this->field_60;
	else
		this->field_5C[1] = 0;

	if (this->field_5C[2] >= this->field_60)
		this->field_5C[2] -= this->field_60;
	else
		this->field_5C[2] = 0;

	if ( !(this->field_5C[2] | (this->field_5C[1] | this->field_5C[0])) )
		this->Die();

	this->SetRadius( this->field_64);
	this->SetFringeRGB(0, this->field_5C[0], this->field_5C[1], this->field_5C[2]);
}

// @Ok
// @Matching
CRipple::~CRipple(void)
{
}

// @Ok
// @Matching
 CWibbling3DExplosion::CWibbling3DExplosion(
		const CVector * a2,
		char * a3,
		i32 a4,
		i32 a5,
		i32 a6,
		i32 a7,
		i32 a8,
		i32 a9,
		i32 a10,
		i32 a11,
		i32 a12)
	: C3DExplosion(a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12)
{
	G_WIBBLING_EXP_COUNT++;
}

// @Ok
// @Matching
CWibbling3DExplosion::~CWibbling3DExplosion(void)
{
	G_WIBBLING_EXP_COUNT--;
}

// @Ok
void Exp_Big3DExplosion(const CVector *a1)
{
	CVector v12;

	GetRandomPosition(a1, &v12, 5);
	new CWibbling3DExplosion(&v12, "expgrnd", 1, 0, 2048, 1750, 256, 0, 15, 2, 2);

	GetRandomPosition(a1, &v12, 5);
	new CWibbling3DExplosion(&v12, "expgrnd", 1, 0, 2048, 1750, 256, 1, 27, 10, 10);

	GetRandomPosition(a1, &v12, 5);
	new CWibbling3DExplosion(&v12, "expgrnd", 1, 0, 2048, 1500, 256, 1, 20, 10, 10);

	GetRandomPosition(a1, &v12, 40);
	new CWibbling3DExplosion(&v12, "expgrnd", 1, 0, 3500, 100, 256, 1, 7, 5, 1);

	GetRandomPosition(a1, &v12, 50);
	new CWibbling3DExplosion(&v12, "expgrnd", 1, 0, 2500, 100, 256, 1, 7, 10, 5);

	GetRandomPosition(a1, &v12, 55);
	new CWibbling3DExplosion(&v12, "expgrnd", 1, 0, 2000, 100, 256, 1, 9, 15, 5);

	GetRandomPosition(a1, &v12, 60);
	new CWibbling3DExplosion(&v12, "expgrnd", 1, 0, 2250, 100, 256, 1, 8, 20, 5);

	GetRandomPosition(a1, &v12, 65);
	new CWibbling3DExplosion(&v12, "expgrnd", 1, 0, 2500, 150, 256, 1, 7, 25, 5);

	GetRandomPosition(a1, &v12, 50);
	new CWibbling3DExplosion(&v12, "expgrnd", 1, 0, 3000, 200, 256, 1, 7, 30, 2);
}

// @Ok
// @Matching
void GetRandomPosition(
		const CVector *a1,
		CVector *a2,
		i32 a3)
{
	i32 v3 = 2 * a3 + 1;

	a2->vx = a1->vx + ((Rnd(v3) - a3) << 12);
	a2->vy = a1->vy + ((Rnd(v3) - a3) << 12);
	a2->vz = a1->vz + ((Rnd(v3) - a3) << 12);
}

// @Ok
void Exp_SmallExplosion(CVector* a1)
{
	if ( !LowMemory )
	{
		Exp_Frag(a1, Rnd(20), 6u, 750, 80);
		Exp_GlowFlash(a1, 70, 0xF0u, 0xC8u, 0, 5, 0, 100);
	}
}

// @Ok
void Exp_Frag(CVector* a1, i32 a2, i32 a3, i32 a4, i32 a5)
{
	if (!LowMemory)
	{
		for (i32 i = 0; i < a2; i++)
		{
			new CFrag(a1, 0x80u, 0x80u, 0x80u, a3, a4, 1, a5, 12288, 30);
		}
	}
}

// @Ok
void Exp_BigExplosion(CVector *a1)
{
	if ( !LowMemory )
	{
		Exp_Frag(a1, Rnd(20), 6u, 750, 80);
		Exp_GlowFlash(a1, 200, 0xF0u, 0xC8u, 0, 5, 0, 100);
	}
}

// @Ok
// field_40 holds 20 heap pointers to CItem-derived objects. The original
// deletes each one through the virtual destructor (vtable slot 0, called
// with flag 1), which is exactly what a plain `delete` on a CItem* emits
// since CItem has a virtual destructor. Confirmed against 0x43C2C0.
CFlameExplosion::~CFlameExplosion(void)
{
	for (i32 i = 0; i < 20; i++)
	{
		delete reinterpret_cast<CItem*>(field_40[i]);
	}
}

// @Ok
CFlameExplosion::CFlameExplosion(
		const CVector* a2,
		i32 a3,
		i32 a4,
		i32 a5)
{
	this->mPos = *a2;
	this->mLifetime = a3;

	this->field_3E = a5;
	this->field_3F = a4;
}

// @Ok
CGlowFlash::CGlowFlash(
		CVector* a2,
		i32 a3,
		u8 a4,
		u8 a5,
		u8 a6,
		i32 a7,
		u8 a8,
		u8 a9,
		u8 a10,
		i32 a11,
		i32 a12,
		i32 a13,
		i32 a14,
		i32 a15,
		i32 a16,
		i32 a17,
		i32 a18,
		i32 a19,
		i32 a20)
	: CGlow(a3, 1)
{
	this->mPos = *a2;
	this->SetCentreRGB(a4, a5, a6);
	this->field_5C = a7;
	this->SetRGB(a8, a9, a10);
	this->field_5E = a11;
	this->SetFringeRGB(0, 0, 0, 0);
	this->SetFringeWidth(0, a12);
	this->field_60 = a13;
	this->field_6C = a14;
	this->field_64 = a16;
	this->field_62 = a15;
	this->field_6A = a19;
	this->field_66 = a17;
	this->field_68 = a18;
	this->mLifetime = a20;
	print_if_false(a20 || this->field_5C, "CGlowFlash centrefaderate and lifetime both zero.");
	this->ChooseRadii();
}

// @Ok
// @Matching
void CGlowFlash::Move(void)
{
	for (u32 i = 0; i < this->mNumSections; i++)
	{
		Bit_ReduceRGB(&this->mpSections[i].PadBGR, this->field_5E);
		Bit_ReduceRGB(&this->mCentreCodeBGR, this->field_5C);
	}

	if (this->field_6C)
		this->ChooseRadii();

	if (this->mLifetime)
	{
		if (++this->mAge <= this->mLifetime)
			return;
	}
	else if (0xFFFFFF & this->mCentreCodeBGR)
	{
		return;
	}

	this->Die();
}

// @Ok
// @Note: Just like Crate_Destroy there's some flag shenanigans that are
// optimized out.
void Exp_HitEnvItem(CItem* pItem, u32* pFace, i32 Damage)
{
	if (pItem && (pItem->mFlags & 1) == 0)
	{
		CItem* pScan = G_ENVIRO_LIST;
		while (pScan)
		{
			if (pScan == pItem)
				break;
			pScan = pScan->mNextItem;
		}

		if (!pScan)
			return;

		print_if_false(G_PSXREGION[pItem->mRegion].Usable != 0, "Eh? Env item spooled out??");
		SModel* v4 = G_PSXREGION[pItem->mRegion].ppModels[pItem->mModel];

		if (v4->Flags)
		{
			if (Damage == 0xFFFF)
				Crate_Destroy(pItem);
		}
		else if (pFace)
		{
			Shatter_Face(pItem, pFace, 1, 1, 1, 1, 1);
		}
	}
}

// @Ok
void Exp_GlowFlash(
		CVector* a1,
		i32 a2,
		u8 a3,
		u8 a4,
		u8 a5,
		i32 a6,
		i32 a7,
		i32 a8)
{
	if ( !LowMemory )
	{
		if ( a7 )
		{
			new CGlowFlash(
				a1,
				a6,
				a3,
				a4,
				a5,
				2,
				a3,
				a4,
				a5,
				20,
				a8,
				0,
				0,
				a2,
				a2,
				a2 / 2,
				a2 / 2,
				0,
				0);
		}
		else
		{
			new CGlowFlash(a1, a6, a3, a4, a5, 2, a3, a4, a5, 20, a8, 0, 0, a2, 0, a2, 0, 0, 0);
		}
	}
}


void validate_CItemFrag(void)
{
	VALIDATE_SIZE(CItemFrag, 0xB8);

	VALIDATE(CItemFrag, field_84, 0x84);
	VALIDATE(CItemFrag, field_88, 0x88);
	VALIDATE(CItemFrag, field_94, 0x94);
	VALIDATE(CItemFrag, field_A0, 0xA0);
	VALIDATE(CItemFrag, field_AC, 0xAC);
}

void validate_CGlowFlash(void)
{
	VALIDATE_SIZE(CGlowFlash, 0x70);

	VALIDATE(CGlowFlash, field_5C, 0x5C);
	VALIDATE(CGlowFlash, field_5E, 0x5E);
	VALIDATE(CGlowFlash, field_60, 0x60);
	VALIDATE(CGlowFlash, field_62, 0x62);
	VALIDATE(CGlowFlash, field_64, 0x64);
	VALIDATE(CGlowFlash, field_66, 0x66);
	VALIDATE(CGlowFlash, field_68, 0x68);
	VALIDATE(CGlowFlash, field_6A, 0x6A);
	VALIDATE(CGlowFlash, field_6C, 0x6C);
}

void validate_CFlameExplosion(void)
{
	VALIDATE_SIZE(CFlameExplosion, 0x90);

	VALIDATE(CFlameExplosion, field_3E, 0x3E);
	VALIDATE(CFlameExplosion, field_3F, 0x3F);

	VALIDATE(CFlameExplosion, field_40, 0x40);
}

void validate_CWibbling3DExplosion(void)
{
	VALIDATE_SIZE(CWibbling3DExplosion, 0x114);
}

void validate_C3DExplosion(void)
{
	VALIDATE_SIZE(C3DExplosion, 0x114);

	VALIDATE(C3DExplosion, field_F8, 0xF8);
	VALIDATE(C3DExplosion, field_FC, 0xFC);

	VALIDATE(C3DExplosion, field_100, 0x100);

	VALIDATE(C3DExplosion, field_104, 0x104);
	VALIDATE(C3DExplosion, field_108, 0x108);
	VALIDATE(C3DExplosion, field_10C, 0x10C);
	VALIDATE(C3DExplosion, field_110, 0x110);
}

void validate_CGrenadeWave(void)
{
	VALIDATE_SIZE(CGrenadeWave, 0x70);

	VALIDATE(CGrenadeWave, field_6C, 0x6C);
}

void validate_CGrenadeExplosion(void)
{
	VALIDATE_SIZE(CGrenadeExplosion, 0x4C);

	VALIDATE(CGrenadeExplosion, hExp, 0x3C);
}

void validate_CRipple(void)
{
	VALIDATE_SIZE(CRipple, 0x6C);

	VALIDATE(CRipple, field_5C, 0x5C);

	VALIDATE(CRipple, field_60, 0x60);
	VALIDATE(CRipple, field_64, 0x64);
	VALIDATE(CRipple, field_68, 0x68);
}

#include "compatibility/my_patch.h"

// @Bogus
void patch_exp(void)
{
	PATCH_PUSH_RET_POLY(0x0043BEE0, CGlowFlash::CGlowFlash, "??0CGlowFlash@@QAE@PAVCVector@@HEEEHEEEHHHHHHHHHH@Z");
	PATCH_PUSH_RET_POLY(0x0043C030, CGlowFlash::~CGlowFlash, "??1CGlowFlash@@UAE@XZ");
	PATCH_PUSH_RET(0x0043C040, CGlowFlash::ChooseRadii);
	PATCH_PUSH_RET_POLY(0x0043C0D0, CGlowFlash::Move, "?Move@CGlowFlash@@UAEXXZ");

	PATCH_PUSH_RET(0x0043C150, Exp_GlowFlash);

	PATCH_PUSH_RET_POLY(0x0043C250, CFlameExplosion::CFlameExplosion, "??0CFlameExplosion@@QAE@PBVCVector@@HHH@Z");
	PATCH_PUSH_RET_POLY(0x0043C2C0, CFlameExplosion::~CFlameExplosion, "??1CFlameExplosion@@UAE@XZ");

	PATCH_PUSH_RET(0x0043C330, Exp_Frag);
	PATCH_PUSH_RET(0x0043C3E0, Exp_BigExplosion);
	PATCH_PUSH_RET(0x0043C430, Exp_SmallExplosion);
	PATCH_PUSH_RET(0x0043C480, Exp_HitEnvItem);

	PATCH_PUSH_RET_POLY(0x0043C580, CRipple::CRipple, "??0CRipple@@QAE@PBVCVector@@EEEHHHH@Z");
	PATCH_PUSH_RET_POLY(0x0043C6B0, CRipple::Move, "?Move@CRipple@@UAEXXZ");

	// The linker folded ~CRipple and ~CGrenadeWave into one body at 0x0043C7E0
	// (both are empty, and the vtable store MSVC puts at the top of
	// ~CGrenadeWave is dead because ~CRipple overwrites it right away). The
	// exe stores CRipple's vtable 0x0053B800 there, and CRipple's scalar
	// deleting destructor at 0x0043C690 serves both classes, so ~CRipple is
	// the one that goes in. ~CGrenadeWave has no address of its own.
	PATCH_PUSH_RET_POLY(0x0043C7E0, CRipple::~CRipple, "??1CRipple@@UAE@XZ");

	PATCH_PUSH_RET_POLY(0x0043C750, CGrenadeWave::CGrenadeWave, "??0CGrenadeWave@@QAE@PBVCVector@@EEEHH@Z");
	PATCH_PUSH_RET_POLY(0x0043C7F0, CGrenadeWave::Move, "?Move@CGrenadeWave@@UAEXXZ");

	PATCH_PUSH_RET_POLY(0x0043C820, CItemFrag::CItemFrag, "??0CItemFrag@@QAE@PAIPAVCVector@@1H@Z");
	PATCH_PUSH_RET_POLY(0x0043C9E0, CItemFrag::~CItemFrag, "??1CItemFrag@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x0043C9F0, CItemFrag::Move, "?Move@CItemFrag@@UAEXXZ");

	PATCH_PUSH_RET_POLY(0x0043CBF0, C3DExplosion::C3DExplosion, "??0C3DExplosion@@QAE@PBVCVector@@PADHHHHHHHHH@Z");
	PATCH_PUSH_RET_POLY(0x0043CD30, C3DExplosion::~C3DExplosion, "??1C3DExplosion@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x0043CD90, C3DExplosion::AI, "?AI@C3DExplosion@@UAEXXZ");

	PATCH_PUSH_RET_POLY(0x0043CEA0, CWibbling3DExplosion::CWibbling3DExplosion, "??0CWibbling3DExplosion@@QAE@PBVCVector@@PADHHHHHHHHH@Z");
	PATCH_PUSH_RET_POLY(0x0043CF20, CWibbling3DExplosion::~CWibbling3DExplosion, "??1CWibbling3DExplosion@@UAE@XZ");

	PATCH_PUSH_RET(0x0043CF90, Exp_Big3DExplosion);
	PATCH_PUSH_RET(0x0043D490, GetRandomPosition);

	PATCH_PUSH_RET_POLY(0x0043D500, CGrenadeExplosion::CGrenadeExplosion, "??0CGrenadeExplosion@@QAE@PBVCVector@@@Z");
	PATCH_PUSH_RET_POLY(0x0043D620, CGrenadeExplosion::~CGrenadeExplosion, "??1CGrenadeExplosion@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x0043D640, CGrenadeExplosion::Move, "?Move@CGrenadeExplosion@@UAEXXZ");
}
