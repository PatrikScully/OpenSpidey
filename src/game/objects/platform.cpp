#include <cstdlib>

#include "game/objects/platform.h"
#include "engine/audio/ps2lowsfx.h"
#include "core/utils.h"
#include "game/effects/shatter.h"
#include "game/player/spidey.h"
#include "engine/assets/spool.h"
#include "engine/world/trig.h"
#ifdef SPIDEY_STANDALONE
#include "game/effects/effects.h"
#endif

#include "compatibility/validate.h"

extern CSVector gTrajectoryVector;
extern volatile i32 TTime; // defined in bit.cpp

// gGravity (0x60F7B0): set by Physics_SetGravity (0x466C70, confirmed by scanning the original
// function bytes for this address), a CVector. Physics_SetGravity also caches its Length() at
// 0x60F7BC and a normalized copy at 0x60F888, neither of which we need here.
static CVector * const gGravity = (CVector*)0x60F7B0;

// @Ok
// Session bar is functional decomp, not byte match (see CLAUDE.md override
// 2026-08-30). Logic verified correct against the original (0x4690D0, 935
// bytes) in a prior session: 6 distinct hypotheses tried, full log in
// CPlatform_AI.attempts.md. Remaining residue after that work is purely
// toolchain/codegen (register/stack allocation, and CVector operator-/
// operator>> getting inlined here since they are declared INLINE in
// vector.h while the original calls them out of line), not a logic
// difference, so it does not block @Ok under the functional bar.
void CPlatform::AI(void)
{
	if (this->pMessage)
		this->CleanUpMessages(1, 0);

	i32 half = this->mElapsedTicks >> 1;

	if (this->field_324)
	{
		i32 amt = this->field_324;
		if (amt >= half)
			amt = half;
		this->field_324 -= amt;
		this->mScale.vx += this->field_32A * amt;
	}

	if (this->field_326)
	{
		i32 amt = this->field_326;
		if (amt >= half)
			amt = half;
		this->field_326 -= amt;
		this->mScale.vy += this->field_32C * amt;
	}

	if (this->field_328)
	{
		i32 amt = this->field_328;
		if (amt < half)
			half = amt;
		this->field_328 -= half;
		this->mScale.vz += this->field_32E * half;
	}

	this->mAcc = ZeroVector;

	if (!this->field_334)
		this->field_338++;

	this->field_214 = this->mInputFlags;
	if (this->mInputFlags & 1)
	{
		this->field_210++;
		this->mInputFlags &= ~1;
	}

	if (this->field_20C)
	{
		if (this->field_230)
			this->field_230--;
		else
			this->ParseScript(reinterpret_cast<u16*>(this->field_24C));
	}

	if (this->field_218 & 1)
	{
		CVector delta = (this->mPos - this->field_240) >> 12;

		if (this->field_344.vz * delta.vz + this->field_344.vy * delta.vy + this->field_344.vx * delta.vx < 0)
		{
			this->mPos = this->field_240;
		}

		if ((this->field_240 - this->mPos).Length() > this->attributeArr[0])
		{
			CSVector aim;
			Utils_CalcAim(&aim, &this->mPos, &this->field_240);
			Utils_GetVecFromMagDir(&this->mVel, this->attributeArr[0], &aim);
		}
		else
		{
			this->mPos = this->field_240;
			this->mVel.vx = 0;
			this->mVel.vy = 0;
			this->mVel.vz = 0;
			this->field_218 &= ~1;
		}
	}

	if (this->field_20E)
		this->mAcc += *gGravity;

	this->DoPhysics();

	if (this->field_340 != -1 && TTime % this->field_340 == 0)
	{
		SFX_ModifyPos(this->field_33C, &this->mPos, 0);
	}

	this->field_334 = 0;

	if ((this->field_218 & 4) && G_MECHLIST_PLAYER)
	{
		CVector *mechPos = &G_MECHLIST_PLAYER->mPos;
		i32 mx = mechPos->vx;
		i32 my = mechPos->vy;
		i32 mz = mechPos->vz;
		i32 xHi = this->mPos.vx + this->field_350.vx;

		if (mx < xHi)
		{
			i32 xLo = this->mPos.vx - this->field_350.vx;

			if (mx > xLo)
			{
				i32 zHi = this->mPos.vz + this->field_350.vz;

				if (mz < zHi)
				{
					i32 zLo = this->mPos.vz - this->field_350.vz;

					if (mz > zLo)
					{
						i32 yHi = this->mPos.vy + this->field_350.vy;

						if (my < yHi)
						{
							i32 yLo = this->mPos.vy - this->field_350.vy;

							if (my > yLo)
							{
								if (abs(this->mVel.vx) > abs(this->mVel.vz))
								{
									if (this->mVel.vx > 0)
										mechPos->vx = xHi + 0x40000;
									else
										mechPos->vx = xLo - 0x40000;
								}
								else if (this->mVel.vz)
								{
									if (this->mVel.vz > 0)
										mechPos->vz = zHi + 0x40000;
									else
										mechPos->vz = zLo - 0x40000;
								}
							}
						}
					}
				}
			}
		}
	}
}

// @Ok
// @Matching
void CPlatform::AdjustBruceHealth(void)
{
	i16 value = this->field_330;
	if (value)
	{
		if (value < 0)
		{
			G_MECHLIST_PLAYER->IncHealth(value - 1);
		}
		else
		{
			SHitInfo v2;
			v2.field_C.vx = 0;
			v2.field_C.vy = 0;
			v2.field_C.vz = 0;

			v2.field_8 = value;
			v2.field_0 = 4;

			G_MECHLIST_PLAYER->Hit(&v2);
		}
	}
}

// @Ok
CPlatform::CPlatform(i16 * a2,i32 a3)
{
	this->field_344.vx = 0;
	this->field_344.vy = 0;
	this->field_344.vz = 0;
	this->field_350.vx = 0;
	this->field_350.vy = 0;
	this->field_350.vz = 0;

	this->InitItem(G_OBJ_FILE);
	this->AttachTo(&G_ENVIRONMENTAL_OBJECT_LIST);

	this->mFlags |= 0x111;
	this->mFlags &= 0xFFFD;
	this->mType = 402;

	this->field_24C = this->SquirtAngles(this->SquirtPos(a2));

	this->field_340 = -1;
	this->mNode = a3;
	this->attributeArr[0] = 32;
	this->field_20C = 1;
}

// @Bogus
// @FIXME forward-to-original: 0x43B740 spawns 10 CBouncingRock particles
// (Web_GetGroundY + CBit::operator new(0x70) + CBouncingRock ctor in a loop).
// Not decompiled; forwarded so CPlatform::ExecuteCommand case 0x430A works.
static i32 gsub_43B740(CVector *a1, i32 a2)
{
#ifdef SPIDEY_STANDALONE
	// 0x43B740 is Effects_MakeRocks (effects.cpp); the original's return
	// value is a leftover eax nobody reads.
	Effects_MakeRocks(a1, a2);
	return 0;
#else
	typedef i32 (*func_ptr)(CVector*, i32);
	func_ptr func = (func_ptr)0x0043B740;
	return func(a1, a2);
#endif
}

// @Ok
// (0x00469690, 2239 bytes). Command dispatcher for platform scripts. The IDB
// decompiler misread the single u16 command arg as two args (a2/a3); the
// prologue reads exactly one stack arg (retn 4) and the default case pushes
// one arg to CBaddy::ExecuteCommand, so the repo's 1-arg signature is right.
i32 CPlatform::ExecuteCommand(u16 a2)
{
	if (a2 > 0x4305)
	{
		if (a2 <= 0x4507)
		{
			if (a2 == 0x4507)
			{
				if (this->field_33C != 0)
					SFX_Stop(this->field_33C);
				i16 sound = *this->field_24C;
				this->field_24C++;
				this->field_33C = SFX_Play(sound, 0x2000, 0);
				this->field_340 = -1;
				return 1;
			}
			switch (a2)
			{
			case 0x4306:
			{
				if ((this->mFlags & 0x200) == 0)
				{
					this->mFlags |= 0x200;
					this->mScale.vz = 4096;
					this->mScale.vy = 4096;
					this->mScale.vx = 4096;
				}
				i16 a = *this->field_24C;
				this->field_24C++;
				i16 b = *this->field_24C;
				this->field_24C++;
				this->field_324 = b;
				i16 scale;
				if (b != 0)
					scale = (a << 12) / (100 * b);
				else
					scale = (a << 12) / 100;
				this->field_32A = scale;
				this->mScale.vx += scale;
				return 1;
			}
			case 0x4307:
			{
				if ((this->mFlags & 0x200) == 0)
				{
					this->mFlags |= 0x200;
					this->mScale.vz = 4096;
					this->mScale.vy = 4096;
					this->mScale.vx = 4096;
				}
				i16 a = *this->field_24C;
				this->field_24C++;
				i16 b = *this->field_24C;
				this->field_24C++;
				this->field_326 = b;
				i16 scale;
				if (b != 0)
					scale = (a << 12) / (100 * b);
				else
					scale = (a << 12) / 100;
				this->field_32C = scale;
				this->mScale.vy += scale;
				return 1;
			}
			case 0x4308:
			{
				if ((this->mFlags & 0x200) == 0)
				{
					this->mFlags |= 0x200;
					this->mScale.vz = 4096;
					this->mScale.vy = 4096;
					this->mScale.vx = 4096;
				}
				i16 a = *this->field_24C;
				this->field_24C++;
				i16 b = *this->field_24C;
				this->field_24C++;
				this->field_328 = b;
				i16 scale;
				if (b != 0)
					scale = (a << 12) / (100 * b);
				else
					scale = (a << 12) / 100;
				this->field_32E = scale;
				this->mScale.vz += scale;
				return 1;
			}
			case 0x430A:
			{
				i16 off = *this->field_24C;
				this->field_24C++;
				CVector pos;
				pos.vx = this->mPos.vx;
				pos.vy = (off << 12) + this->mPos.vy;
				pos.vz = this->mPos.vz;
				i32 *p = (i32*)((((i32)this->field_24C) + 3) & 0xFFFFFFFC);
				gsub_43B740(&pos, *p);
				this->field_24C = (i16*)((i32)p + 8);
				return 1;
			}
			default:
				return CBaddy::ExecuteCommand(a2);
			}
		}
		switch (a2)
		{
		case 0x4508:
		{
			if (this->field_33C != 0)
				SFX_Stop(this->field_33C);
			i16 sound = *this->field_24C;
			this->field_24C++;
			i16 vol = *this->field_24C;
			this->field_24C++;
			this->field_340 = vol;
			this->field_33C = SFX_PlayPos(sound, &this->mPos, 0);
			return 1;
		}
		case 0x4509:
		{
			if (this->field_33C != 0)
				SFX_Stop(this->field_33C);
			this->field_33C = 0;
			return 1;
		}
		case 0x450B:
		{
			if (this->field_33C != 0)
				SFX_Stop(this->field_33C);
			i16 sound = *this->field_24C;
			this->field_24C++;
			i16 vol = *this->field_24C;
			this->field_24C++;
			this->field_340 = vol;
			this->field_33C = SFX_PlayPos(sound | 0x8000, &this->mPos, 0);
			return 1;
		}
		default:
			return CBaddy::ExecuteCommand(a2);
		}
	}
	if (a2 == 0x4305)
	{
		i16 flag = *this->field_24C;
		this->field_24C++;
		if (flag != 0)
			this->mFlags |= 8;
		else
			this->mFlags &= ~8;
		return 1;
	}
	if (a2 <= 0x4300)
	{
		if (a2 != 0x4300)
		{
			switch (a2)
			{
			case 0x4205:
				this->Die(0);
				return 0;
			case 0x4220:
			{
				CVector pos;
				i32 *p = (i32*)((((i32)this->field_24C) + 3) & 0xFFFFFFFC);
				pos.vx = *p;
				p++;
				pos.vy = *p;
				p++;
				pos.vz = *p;
				pos <<= 12;
				this->field_24C = (i16*)(p + 1);
				if (!(this->field_240 != pos))
					return 1;
				this->field_240 = pos;
				this->field_344 = (this->mPos - pos) >> 12;
				this->field_218 |= 1;
				return 1;
			}
			case 0x4221:
			{
				u16 trigId = *this->field_24C;
				this->field_24C++;
				if (trigId & 0x2000)
				{
					// vtable+64 is GetVariable (virtual); the original calls it with the
					// 0x2000 bit still set to resolve the real trig id.
					trigId = (u16)this->GetVariable(trigId);
				}
				CVector pos;
				Trig_GetPosition(&pos, trigId);
				if (!(this->field_240 != pos))
					return 1;
				this->field_240 = pos;
				this->field_344 = (this->mPos - pos) >> 12;
				this->field_218 |= 1;
				return 1;
			}
			case 0x4222:
			{
				CVector pos;
				i32 *p = (i32*)((((i32)this->field_24C) + 3) & 0xFFFFFFFC);
				pos.vx = *p;
				p++;
				pos.vy = *p;
				p++;
				pos.vz = *p;
				pos <<= 12;
				pos += this->mPos;
				this->field_24C = (i16*)(p + 1);
				if (!(this->field_240 != pos))
					return 1;
				this->field_240 = pos;
				this->field_344 = (this->mPos - pos) >> 12;
				this->field_218 |= 1;
				return 1;
			}
			default:
				return CBaddy::ExecuteCommand(a2);
			}
		}
		if (this->field_334 == 0)
		{
			--this->field_24C;
			return 0;
		}
		return 1;
	}
	switch (a2)
	{
	case 0x4301:
		{
			CPlatform **pMech = (CPlatform**)((char*)G_MECHLIST_PLAYER + 0xDBC);
			if (*pMech == this)
				*pMech = 0;
		}
		this->mFlags |= 1;
		this->Die(0);
		Shatter_Item((CItem*)this, 0, 0);
		return 0;
	case 0x4302:
	{
		i16 x = *this->field_24C;
		this->field_24C++;
		i16 y = *this->field_24C;
		this->field_24C++;
		i16 z = *this->field_24C;
		this->field_24C++;
		this->field_350.vx = (x << 12) >> 1;
		this->field_350.vy = (y << 12) >> 1;
		this->field_350.vz = (z << 12) >> 1;
		this->field_218 |= 4;
		return 1;
	}
	case 0x4303:
		this->field_330 = *this->field_24C;
		this->field_24C++;
		return 1;
	case 0x4304:
	{
		if ((this->mFlags & 0x200) == 0)
		{
			this->mFlags |= 0x200;
			this->mScale.vz = 4096;
			this->mScale.vy = 4096;
			this->mScale.vx = 4096;
		}
		i16 a = *this->field_24C;
		this->field_24C++;
		i16 b = *this->field_24C;
		this->field_24C++;
		this->field_324 = b;
		this->field_326 = b;
		this->field_328 = b;
		i16 scale;
		if (b != 0)
			scale = (a << 12) / (100 * b);
		else
			scale = (a << 12) / 100;
		this->field_32A = scale;
		this->field_32C = scale;
		this->field_32E = scale;
		this->mScale.vx += scale;
		this->mScale.vy += scale;
		this->mScale.vz += scale;
		return 1;
	}
	default:
		return CBaddy::ExecuteCommand(a2);
	}
}

// @Ok
// @Matching
i32 CPlatform::Hit(SHitInfo* a2)
{
	this->field_20F++;
	if (this->attributeArr[1])
	{

		if (this->mHealth > 0)
		{
			this->mHealth -= a2->field_8;
			if (this->mHealth <= 0)
			{
				this->Die(0);
				SFX_PlayPos(Rnd(2) + 1, &this->mPos, 0);
				Shatter_Item(this, 0, 1);
				this->mFlags |= 1;
			}
		}
	}

	return 1;
}

// @Ok
// @Validate
 void CPlatform::MoveTo(CVector* pVec)
{
	if (this->field_240 != *pVec)
	{
		this->field_240 = *pVec;
		this->field_344 = ((this->mPos - *pVec) >> 12);
		this->field_218 |= 1;
	}
}

// @Ok
// @Matching
void CPlatform::NotifyTrodUpon(CBody *,CVector const *,CSVector const *)
{
	this->field_334 = 1;
	this->field_338 = 0;
}

// @Ok
// @Matching
void CPlatform::SetVariable(u16 a2)
{
	switch (a2)
	{
	case 0x2123:
		this->field_20E = *this->field_24C;
		this->field_24C++;
		break;

	case 0x2124:
		this->mModel = *this->field_24C;
		this->field_24C++;
		if (*(u8*)((i32***)0x6B2454)[this->mRegion * 17][this->mModel] & 0x10)
			this->mFlags |= 0x20;
		else
			this->mFlags &= ~0x20;
		break;

	case 0x212F:
		{
			u32 *v6 = reinterpret_cast<u32*>((reinterpret_cast<u32>(this->field_24C) + 3) & 0xFFFFFFFC);
			u16 Model = Spool_GetModel(*v6, this->mRegion);

			this->mModel = Model;
			if (*(u8*)((i32***)0x6B2454)[this->mRegion * 17][Model] & 0x10)
				this->mFlags |= 0x20;
			else
				this->mFlags &= ~0x20;
			this->mFlags &= ~1;

			this->field_24C = reinterpret_cast<i16*>(&v6[1]);
		}
		break;

	case 0x2134:
		{
			i16 vx = this->GetScriptValue();
			i16 vy = this->GetScriptValue();
			i16 vz = this->GetScriptValue();

			this->mVel.vx = (i32)vx << 12;
			this->mVel.vy = (i32)vy << 12;
			this->mVel.vz = (i32)vz << 12;
		}
		break;

	case 0x2137:
		{
			i16 vx = this->GetScriptValue();
			i16 vy = this->GetScriptValue();
			i16 vz = this->GetScriptValue();

			this->mAngles.vx = vx;
			this->mAngles.vy = vy;
			this->mAngles.vz = vz;
		}
		break;

	case 0x2127:
		{
			i16 vx = this->GetScriptValue();
			i16 vy = this->GetScriptValue();
			i16 vz = this->GetScriptValue();

			this->mAngVel.vx = vx;
			this->mAngVel.vy = vy;
			this->mAngVel.vz = vz;
		}
		break;

	case 0x2128:
		{
			i16 vx = this->GetScriptValue();
			i16 vy = this->GetScriptValue();
			i16 vz = this->GetScriptValue();

			this->mAngAcc.vx = vx;
			this->mAngAcc.vy = vy;
			this->mAngAcc.vz = vz;
		}
		break;

	default:
		CBaddy::SetVariable(a2);
		break;
	}
}

// @Ok
CPlatform::~CPlatform(void)
{
	this->DeleteFrom(&G_ENVIRONMENTAL_OBJECT_LIST);
	if (this->field_33C)
	{
		SFX_Stop(this->field_33C);
		this->field_33C = 0;
	}
}
// @Ok
void CPlatform::Shouldnt_DoPhysics_Be_Virtual(void)
{
	this->DoPhysics();
}

// @Ok
// @Matching
void CPlatform::DoPhysics(void)
{
	this->field_A8 = gTrajectoryVector;

	if (this->field_2B0 | this->field_2B4)
	{
		CBaddy::DoPhysics(0);
		return;
	}

	if (this->attributeArr[2] == 0)
	{
		i32 step;
		if (Trig_GetLevelID() == 0x301 && this->mElapsedTicks >= 4)
			step = 2;
		else
			step = 1;

		for (i32 i = 0; i < this->mElapsedTicks; i += step)
		{
			this->mVel += this->mAcc;
			this->mVel.KillSmall();
			this->mPos += this->mVel;
			this->mAngles += this->mAngVel;
			this->mAngles.Mask();
			this->mAngVel += this->mAngAcc;
			this->mAngVel.KillSmall();
		}
	}
	else if (this->attributeArr[2] == 3)
	{
		this->mVel += this->mAcc;
		this->mVel.KillSmall();
		this->mPos += this->mVel;
		this->mAngles += this->mAngVel;
		this->mAngles.Mask();
		this->mAngVel += this->mAngAcc;
		this->mAngVel.KillSmall();
	}
}

// @Ok
i16 CPlatform::GetVariable(u16 a2)
{
	if (a2 != (u16)0x2200)
	{
		return CBaddy::GetVariable(a2);
	}

	return this->field_338;
}

void validate_CPlatform(void){
	VALIDATE_SIZE(CPlatform, 0x35C);

	VALIDATE(CPlatform, field_324, 0x324);
	VALIDATE(CPlatform, field_326, 0x326);
	VALIDATE(CPlatform, field_328, 0x328);

	VALIDATE(CPlatform, field_32A, 0x32A);
	VALIDATE(CPlatform, field_32C, 0x32C);
	VALIDATE(CPlatform, field_32E, 0x32E);

	VALIDATE(CPlatform, field_330, 0x330);

	VALIDATE(CPlatform, field_334, 0x334);
	VALIDATE(CPlatform, field_338, 0x338);
	VALIDATE(CPlatform, field_33C, 0x33C);

	VALIDATE(CPlatform, field_340, 0x340);

	VALIDATE(CPlatform, field_344, 0x344);
	VALIDATE(CPlatform, field_350, 0x350);
}

