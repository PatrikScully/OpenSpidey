#include "game/objects/level_one_bomb.h"
#include "compatibility/validate.h"
#include "engine/world/trig.h"
#include "game/player/spidey.h"
#include "game/cinematics/camera.h"
#include "core/utils.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/world/collision.h"
#include "engine/world/m3dzone.h"
#include "engine/rendering/ps2funcs.h"

#ifndef SPIDEY_STANDALONE
u32 gBombRelated;
#else
extern u32 gBombRelated;
#endif
#ifndef SPIDEY_STANDALONE
u8 gBombDieRelatedOne;
#else
extern u8 gBombDieRelatedOne;
#endif
#ifndef SPIDEY_STANDALONE
u8 gBombDieRelatedTwo;
#else
extern u8 gBombDieRelatedTwo;
#endif
#ifndef SPIDEY_STANDALONE
u32 gBombDieTimerRelated;
#else
extern u32 gBombDieTimerRelated;
#endif
#ifndef SPIDEY_STANDALONE
u32 gBombAIRelated;
#else
extern u32 gBombAIRelated;
#endif

extern i32 DifficultyLevel;

// @Ok
CL1A3Bomb::~CL1A3Bomb(void)
{
	gBombDieRelatedOne = 0;
	gBombDieRelatedTwo = 0;
	gBombDieTimerRelated = G_TIMER_RELATED;
}

// @Ok
// address 0x4470a0 (names.json), disassembled with IDA. Functional decomp
// only, not byte matched (209 mnemonic diffs on cmpsum, mostly register
// allocation and one confirmed-equivalent codegen fold: the original does
// "sar 12; shl 1" for a doubled dot product term, our build folds that into
// "sar 11; and ~1", which is the exact same value). One real bug caught and
// fixed during verification: forgot the "if (gBombRelated < 0x2000)
// gBombRelated = 0x2000;" clamp on first landing, it showed up as a whole
// missing instruction block in compare.py.
// Standard CManipOb physics step for the bomb prop: integrate velocity into
// position for field_80 substeps, snap to ground height with a one-shot
// camera shake + SFX on first landing, apply a region-local "push away from
// center" minimum horizontal speed inside a fixed coordinate box (probably
// a vent/fan trigger area in the l1a3 level), zero out a near-stopped
// velocity, then trace a short line along the velocity direction and bounce
// off anything it hits (reflect velocity around the hit normal, half
// magnitude), unless the hit surface is floor/ceiling-like (near-vertical
// normal), in which case it just makes sure vertical velocity is not
// downward.
void CL1A3Bomb::DoPhysics(void)
{
	CVector capturedPos = this->mPos;

	if (this->mElapsedTicks > 0)
	{
		i32 n = this->mElapsedTicks;
		do
		{
			this->mVel += this->mAcc;
			this->mPos += this->mVel;
		} while (--n);
	}

	i32 groundHeight = Utils_GetGroundHeight(&this->mPos, 100, 72, 0);
	if (groundHeight == -1)
	{
		this->field_12A = 0;
	}
	else
	{
		if (!this->field_12A)
		{
			G_CAMERA_LIST->Shake(this->mPos, CAMERASHAKE_SMALL);

			if (gBombRelated < 0x2000)
				gBombRelated = 0x2000;

			SFX_PlayPos(0x819C, &this->mPos, 0);
			this->field_12A = 1;
		}

		this->mPos.vy = groundHeight - 294912;
		this->mVel.vy = 0;

		// friction: damp horizontal velocity by 220/256 each tick
		this->mVel.vx = (220 * this->mVel.vx) >> 8;
		this->mVel.vz = (220 * this->mVel.vz) >> 8;

		// inside a fixed coordinate box (vent/fan trigger region), give
		// horizontal velocity a minimum magnitude of 8192 so the bomb keeps
		// moving away from the middle instead of settling there.
		if (this->mPos.vx >= -16384000 && this->mPos.vx <= -11673600 &&
				this->mPos.vz >= 0x79E000 && this->mPos.vz <= 0xAF0000)
		{
			if (this->mVel.vx > 0)
			{
				if (this->mVel.vx < 0x2000)
					this->mVel.vx = 0x2000;
			}
			else if (this->mVel.vx > -8192)
			{
				this->mVel.vx = -8192;
			}
		}
	}

	if (my_abs(this->mVel.vx) < 0x2000 && my_abs(this->mVel.vz) < 0x2000)
	{
		this->mVel.vz = 0;
		this->mFlags &= ~0x20;
		this->mVel.vx = 0;
		this->field_10C &= ~1;
	}

	{
		SLineInfo line;

		CVector dir = (this->mPos - capturedPos) >> 12;
		VectorNormal(reinterpret_cast<VECTOR*>(&dir), reinterpret_cast<VECTOR*>(&dir));

		line.StartCoords.vx = capturedPos.vx - (dir.vx << 7);
		line.StartCoords.vy = capturedPos.vy - (dir.vy << 7);
		line.StartCoords.vz = capturedPos.vz - (dir.vz << 7);

		line.EndCoords.vx = capturedPos.vx + (dir.vx << 6);
		line.EndCoords.vy = this->mPos.vy + (dir.vy << 6);
		line.EndCoords.vz = capturedPos.vz + (dir.vz << 6);

		M3dColij_InitLineInfo(&line);
		M3dZone_LineToItem(&line, 1);

		if (line.pItem)
		{
			G_CAMERA_LIST->Shake(this->mPos, CAMERASHAKE_SMALL);
			this->mPos = capturedPos;
			SFX_PlayPos(0x819C, &this->mPos, 0);

			if (line.Normal.vy >= -3800)
			{
				if (line.Normal.vy <= 3800)
				{
					// wall-like hit: reflect velocity around the hit normal.
					i32 halfLen = this->mVel.Length() / 2;

					this->mVel >>= 6;
					VectorNormal(reinterpret_cast<VECTOR*>(&this->mVel), reinterpret_cast<VECTOR*>(&this->mVel));

					i32 dot = ((line.Normal.vz * -this->mVel.vz) +
							(line.Normal.vx * -this->mVel.vx) +
							(line.Normal.vy * -this->mVel.vy)) >> 12;
					i32 factor = dot << 1;

					i32 newVx = ((line.Normal.vx * factor) >> 12) + this->mVel.vx;
					i32 newVy = ((line.Normal.vy * factor) >> 12) + this->mVel.vy;
					i32 newVz = ((line.Normal.vz * factor) >> 12) + this->mVel.vz;

					this->mVel.vx = newVx * halfLen;
					this->mVel.vy = newVy * halfLen;
					this->mVel.vz = newVz * halfLen;

					if (this->mVel.vy < 0)
						this->mVel.vy = 0;
				}
				else
				{
					// floor/ceiling-like hit: never keep moving downward.
					if (this->mVel.vy < 0)
						this->mVel.vy = -this->mVel.vy;
				}
			}
		}
	}
}

// @Ok
void CL1A3Bomb::AI(void)
{
	if (this->field_128 && !gBombAIRelated)
	{
		if (!this->field_129)
		{
			Trig_SendSignalToLinks(Trig_GetLinksPointer(this->mNode));
			this->field_129 = 1;
		}

		return;
	}
	else if (this->mInputFlags & 1)
	{
		this->mInputFlags &= 0xFFFE;

		if (!this->field_128)
		{
			this->field_128 = 1;
			gBombDieRelatedOne = 1;
			gBombDieRelatedTwo = 1;
			gBombAIRelated = DifficultyLevel != 3 ? 7260 : 4260;
			gBombDieTimerRelated = G_TIMER_RELATED;
		}
	}

	if (this->field_10C & 1)
		this->DoPhysics();
}

// @Ok
CL1A3Bomb::CL1A3Bomb(
		i16* a2,
		i32 a3)
	: CManipOb(a2, a3)
{
	this->field_128 = 0;
	this->field_129 = 0;

	gBombRelated = 4096;
}

// @Ok
void CL1A3Bomb::Die(void)
{
	Trig_SendPulse(Trig_GetLinksPointer(this->mNode));
	gBombDieRelatedOne = 0;
	gBombDieRelatedTwo = 0;
	gBombDieTimerRelated = G_TIMER_RELATED;
}

// @Ok
void CL1A3Bomb::Smash(void)
{
	CVector v9;
	v9.vx = 0;
	v9.vy = 0;
	v9.vz = 0;

	i32 vz = 0;

	if (G_MECHLIST_PLAYER)
	{
		CVector v5 = (-24 * G_MECHLIST_PLAYER->mForwardDirection) + (14 * G_MECHLIST_PLAYER->mUpDirection);
		v9.vx = v5.vx;
		v9.vy = v5.vy;
		vz = v5.vz;
	}
	else
	{
		v9.vx = 0;
		v9.vy = -57344;
	}

	v9.vz = vz;

	this->Throw(&v9);
}

void validate_CL1A3Bomb(void)
{
	VALIDATE_SIZE(CL1A3Bomb, 0x12C);

	VALIDATE(CL1A3Bomb, field_128, 0x128);
	VALIDATE(CL1A3Bomb, field_129, 0x129);
	VALIDATE(CL1A3Bomb, field_12A, 0x12A);
}
