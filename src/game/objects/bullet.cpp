#include "game/objects/bullet.h"
#include "core/utils.h"
#include "game/effects/exp.h"
#include "core/game_heap.h"
#include "game/player/spidey.h"

#include "compatibility/validate.h"

#include <cmath>


#ifndef SPIDEY_STANDALONE
CBody* BulletList;
#else
extern CBody* BulletList;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT i32 gBullets;
#else
extern i32 gBullets;
#endif



// @Ok
void CBullet::BlowUp(void)
{
	if (this->field_100 == reinterpret_cast<void*>(&G_MECHLIST_PLAYER))
	{
		new CSmokePuff(&this->mPos);

		if (G_MECHLIST_PLAYER)
		{
			u32 dist = Utils_Dist(this->mPos, G_MECHLIST_PLAYER->mPos);

			if (dist < this->field_106)
			{
				SHitInfo hit;
				hit.field_0 = 4;
				hit.field_C.vx = 0;
				hit.field_C.vy = 0;
				hit.field_C.vz = 0;
				hit.field_8 = dist * this->field_104 / this->field_106;

				G_MECHLIST_PLAYER->Hit(&hit);
			}
		}
	}
	else
	{
		new CFireyExplosion(&this->mPos);
	}

	if (this->field_124)
	{
		if (this->field_13C)
		{
			void *pObj = Mem_RecoverPointer(&this->field_140);

			if (pObj)
			{
				print_if_false(
						(reinterpret_cast<CItem*>(pObj)->mFlags & 0x10) != 0,
						"Eh? Recovered pointer not an env obj");
				print_if_false(
						pObj == this->field_128,
						"Eh? Recovered pointer does not match original");

				SHitInfo hit;
				hit.field_0 = 14;
				hit.field_4 = 1;
				hit.field_8 = this->field_104;
				hit.field_C.vx = -this->field_A8.vx;
				hit.field_C.vy = -this->field_A8.vy;
				hit.field_C.vz = -this->field_A8.vz;

				reinterpret_cast<CBody*>(pObj)->Hit(&hit);
			}
		}
		else
		{
			Exp_HitEnvItem(
					reinterpret_cast<CItem*>(this->field_128),
					reinterpret_cast<u32*>(this->field_12C),
					this->field_104);
		}
	}

	this->Die();
}


// @Ok
// @Matching
void CBullet::GiveScaledDamageToEnviro(i32 a2)
{
	CItem *pItem = EnviroList;

	while (pItem)
	{
		i32 dX = pItem->mPos.vx - this->mPos.vx;
		if (dX < 0)
		{
			dX = -dX;
		}

		if (dX < a2 << 12)
		{
			i32 dZ = pItem->mPos.vz - this->mPos.vz;
			if (dZ < 0)
			{
				dZ = -dZ;
			}

			if (dZ < a2 << 12)
			{
				CVector v7;
				v7.vx = pItem->mPos.vx;
				v7.vy = pItem->mPos.vy;
				v7.vz = pItem->mPos.vz;

				// @FIXME - CrapDist should be a u32 why the fuck it's a i32 here
				if (static_cast<i32>(spidey::math::approximateDistance(this->mPos, v7)) < a2)
				{
					Exp_HitEnvItem(pItem, 0, this->field_104);
				}
			}
		}

		pItem = pItem->mNextItem;
	}
}

// @Ok
// @Matching
void CBullet::GiveScaledDamageToObjects(
		CBody *a2,
		i32 a3,
		i32 a4,
		i32 a5,
		HitId a6)
{
	while (a2)
	{
		CVector v10 = a2->mPos - this->mPos;
		i32 vecLen = v10.Length();

		if (vecLen <= a3)
		{
			SHitInfo v13;
			v13.field_0 = 14;
			v13.field_4 = a6;

			v13.field_8 = this->field_104 - (this->field_104 >> 1) * vecLen / a3;

			v13.field_C = v10 / vecLen;
			
			v13.field_18 = a4 - a4 * vecLen / a3;
			v13.field_1A = a5 - a5 * vecLen / a3;

			if (!v13.field_1A)
			{
				v13.field_1A = 1;
			}

			a2->Hit(&v13);
		}

		a2 = reinterpret_cast<CBody*>(a2->mNextItem);
	}
}

// @Ok
// @NotMatching: validate when inlined, it's inlined in both PowerPC and Windows so
// I had to guess the params
 CSmokePuff::CSmokePuff(CVector* pVec)
	: CSmokeRing(12, 0xB159E2AB)
{
	this->mVel.Set(0, -32768, 0);

	this->SetParams(pVec, 0, 0);

	this->mLifetime = 16;
	this->field_68 = 32;
	this->field_6A = this->field_68 / 2;
}

// @Ok
// @Matching
void CSmokePuff::Move(void)
{
	if (++this->mAge > this->mLifetime)
	{
		this->Die();
	}
	else
	{
		this->mPos += this->mVel;

		this->SetParams(
			&this->mPos,
			this->field_50 + this->field_68,
			this->field_54 + this->field_6A);

		i32 v3 = 255 * (this->mLifetime - this->mAge) / this->mLifetime;
		this->SetRGB(v3, v3, v3 >> 1);
	}
}

// @Ok
// @Matching
CSmokePuff::~CSmokePuff(void)
{
}

// @Ok
CBullet::~CBullet(void)
{
	--gBullets;

	delete reinterpret_cast<CItem*>(this->field_10C);

	if (this->field_120)
	{
		reinterpret_cast<u8*>(this->field_120)[58] = 0;
		reinterpret_cast<u32*>(this->field_120)[21] = 1;
	}
}

// @Ok
CBullet::CBullet(void)
{
	this->field_114 = 0;
	this->field_118 = 0;
	this->field_11C = 0;

	this->field_130 = 0;
	this->field_134 = 0;
	this->field_138 = 0;

	gBullets++;
	this->InitItem("items");

	this->mScale.vx = 2048;
	this->mScale.vy = 2048;
	this->mScale.vz = 2048;

	this->mFlags |= 0x200;
	this->mCBodyFlags &= 0xFFFD;
}

void validate_CBullet(void)
{
	VALIDATE_SIZE(CBullet, 0x148);

	VALIDATE(CBullet, field_100, 0x100);
	VALIDATE(CBullet, field_104, 0x104);
	VALIDATE(CBullet, field_106, 0x106);

	VALIDATE(CBullet, field_10C, 0x10C);

	VALIDATE(CBullet, field_114, 0x114);
	VALIDATE(CBullet, field_118, 0x118);
	VALIDATE(CBullet, field_11C, 0x11C);
	VALIDATE(CBullet, field_120, 0x120);
	VALIDATE(CBullet, field_124, 0x124);
	VALIDATE(CBullet, field_128, 0x128);
	VALIDATE(CBullet, field_12C, 0x12C);

	VALIDATE(CBullet, field_130, 0x130);
	VALIDATE(CBullet, field_134, 0x134);
	VALIDATE(CBullet, field_138, 0x138);
	VALIDATE(CBullet, field_13C, 0x13C);
	VALIDATE(CBullet, field_140, 0x140);
}

void validate_CSmokePuff(void)
{
	VALIDATE_SIZE(CSmokePuff, 0x6C);
}
