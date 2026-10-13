#include <new>
#include "game/objects/manipob.h"
#include "core/utils.h"
#include "compatibility/validate.h"
#include "engine/world/trig.h"
#include "core/game_heap.h"
#include "engine/assets/spool.h"
#include "game/actors/baddy.h"
#include "engine/world/m3dzone.h"
#include "engine/audio/ps2lowsfx.h"
#include "game/ui/message.h"
#include "game/effects/exp.h"
#include "game/effects/effects.h"
#include "game/cinematics/camera.h"
#include "game/actors/simby.h"

static i16 * const word_610C48 = (i16*)0x610C48;

// @Ok
void CManipObChunk::AI(void)
{
	this->field_FC -= this->mElapsedTicks;

	if (this->field_FC <= 0)
	{
		this->Die();
	}
	else
	{
		this->DoPhysics();
	}
}

// @Ok
// @Matching
CManipObChunk::CManipObChunk(u32 a1, CVector *a2, CVector *a3)
{
	this->InitItem(G_OBJ_FILE);
	this->AttachTo(reinterpret_cast<CBody**>(&BaddyList));

	this->mFlags &= 0xFFFD;
	this->mRMinor = 0;
	this->mType = 401;

	this->mFric.vx = 12;
	this->mFric.vy = 12;
	this->mFric.vz = 12;

	this->mPos = *a2;
	this->mVel = *a3;

	this->mAngles.vx = Rnd(4096);
	this->mAngles.vy = Rnd(4096);

	this->mAcc.vy = 4096;

	this->mAngVel.vx = 64 - Rnd(32);
	this->mAngVel.vy = 64 - Rnd(32);

	this->mModel = Spool_GetModel(a1, gObjFileRegion);

	this->field_FC = Rnd(30) + 60;
}

// @Ok
// Address 0x456630 (0x4574d0 in names.json is a different, unrelated
// function; the real CManipObChunk::DoPhysics starts right after
// CManipObChunk::AI's tail jump at 0x456630 and runs to 0x456918, in the
// gap names.json leaves between CManipObChunk_AI and the CManipOb
// constructor). Substeps mVel/mPos/mAngles field_80 times, casts a probe
// line 64 units past the new position along the normalized movement
// direction, and on the first frame that line hits something, reflects
// mVel off the hit normal and teleports mPos back to the hit point; on any
// later hit it just dies.
void CManipObChunk::DoPhysics(void)
{
	CVector oldPos = this->mPos;

	for (i32 i = this->mElapsedTicks; i > 0; i--)
	{
		this->mVel += this->mAcc;
		this->mVel %= this->mFric;
		this->mPos += this->mVel;
		this->mAngles += this->mAngVel;
	}

	CVector delta = this->mPos - oldPos;
	CVector dir = delta >> 12;

	VectorNormal(reinterpret_cast<VECTOR*>(&dir), reinterpret_cast<VECTOR*>(&dir));

	CVector offset = 64 * dir;

	SLineInfo lineInfo;
	lineInfo.StartCoords = oldPos;
	lineInfo.EndCoords = this->mPos + offset;
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
		return;
	}

	i32 hitCount = ++this->field_F8;

	if (hitCount < 1)
	{
		this->mAngVel.vx = Rnd(128) - 64;
		this->mAngVel.vy = Rnd(2 * this->mAngVel.vy) - this->mAngVel.vy;

		this->mPos = lineInfo.Position;
		this->mPos -= offset;

		i32 length = this->mVel.Length();
		this->mVel /= length;

		i32 dot = -(lineInfo.Normal.vx * this->mVel.vx
		           + lineInfo.Normal.vy * this->mVel.vy
		           + lineInfo.Normal.vz * this->mVel.vz) >> 12;
		i32 scale = dot * 2;

		this->mVel.vx = (((lineInfo.Normal.vx * scale) >> 12) + this->mVel.vx) * length;
		this->mVel.vy = (((lineInfo.Normal.vy * scale) >> 12) + this->mVel.vy) * length;
		this->mVel.vz = (((lineInfo.Normal.vz * scale) >> 12) + this->mVel.vz) * length;
	}
	else
	{
		this->Die();
	}
}

// @Ok
// @Matching
CManipObChunk::~CManipObChunk(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&BaddyList));
}

// @Ok
// @AlmostMatching: vector assingment not zero based
u8 CManipOb::TugImpulse(
		CVector *a2,
		CVector *a3)
{
	this->field_110 = *a2 - this->mPos;

	this->mVel = *a3;
	this->mAcc.vy = 4096;

	this->mAngVel.vx = 64 - Rnd(128);
	this->mAngVel.vy = 64 - Rnd(128);
	this->field_10C |= 5;
	return 1;
}

// @Ok
// @Matching
void CManipOb::GetAttachPoint(CVector *a2)
{
	*a2 = this->mPos + this->field_110;
}

// @Ok
// @AlmostMatching: vector assingment is zero based on the og for mVel
void CManipOb::Drop(CVector *a2)
{
	this->mFlags &= 0xF7FFu;
	this->field_10C |= 1u;
	this->field_10C &= 0xFFFFFFF7;

	this->mVel = *a2;

	this->mAcc.vy = 4096;
	this->mAngVel.vx = Rnd(32) + 64;
}

// @Ok
CManipOb::~CManipOb(void)
{
	this->DeleteFrom(&G_ENVIRONMENTAL_OBJECT_LIST);

	if (this->pVectors)
	{
		Mem_Delete(reinterpret_cast<void*>(this->pVectors));
		this->pVectors = 0;
	}
}

// @Ok
CManipOb::CManipOb(
		i16* a2,
		i32 a3)
{
	this->InitItem(G_OBJ_FILE);
	this->AttachTo(&G_ENVIRONMENTAL_OBJECT_LIST);

	this->mFlags = this->mFlags & 0x0FFFD | 0x10;
	this->mType = 401;

	this->mNode = a3;

	this->mFric.vx = 12;
	this->mFric.vy = 12;
	this->mFric.vz = 12;

	u32* v6 = reinterpret_cast<u32*>(
			(reinterpret_cast<u32>(
								   this->SquirtAngles(reinterpret_cast<i16*>(this->SquirtPos(a2)))) + 3)
			& 0xFFFFFFFC);

	i32 *v8 = reinterpret_cast<i32*>(v6 + 1);
	this->mModel = Spool_GetModel(*v6, gObjFileRegion);

	if (*v8)
	{
		this->field_11C = Spool_FindEnviroItem(*v8++);
	}

	this->field_120 = reinterpret_cast<i32>(v8);

	i32 *v10 = v8;
	while (*v10++);

	u16 v12 = *reinterpret_cast<u16*>(v10);

	if (*reinterpret_cast<u16*>(v10) & 1)
		this->field_10C |= 8;


	if (v12 & 2)
	{
		this->field_10C |= 0x20;
	}

	this->field_108 = *(reinterpret_cast<u16*>(v10) + 1);
	this->field_104 = *(reinterpret_cast<u16*>(v10) + 2);

}

// @Ok
// Verified against 0x456B60. word_610C48 is the shared rcossin_tbl fixed
// point sin/cos table (repo-wide convention, see bit.cpp/camera.cpp/
// quat.cpp/shell.cpp/spidey.cpp/mysterio.cpp, confirmed by the maintainer's
// IDB). NumVectors<=1 divides by zero in v17's computation same as the
// original; that is a preserved original defect, not a bug here.
void CManipOb::CalculateThrowPositionArray(CVector* pVector, int NumVectors)
{

	this->mNumVectors = NumVectors;

	i32 v15 = (pVector->vx - this->mPos.vx) / NumVectors;
	i32 v16 = (pVector->vy - this->mPos.vy) / NumVectors;
	i32 v1 = (pVector->vz - this->mPos.vz) / NumVectors;

	this->pVectors = reinterpret_cast<CVector*>(DCMem_New(12 * NumVectors, 0, 1, 0, 1));


	i32 v9 = v15 + this->mPos.vx;
	i32 v10 = v16 + this->mPos.vy;
	i32 v11 = v1 + this->mPos.vz;

	i32 v17 = 2048 / (this->mNumVectors - 1);
	i32 v14 = 0;

	i32 index = 0;
	while (index < this->mNumVectors)
	{
		this->pVectors[index].vx = v9;
		this->pVectors[index].vy = v10 - (word_610C48[2 * (v14 & 0xFFF)] << 8);
		this->pVectors[index].vz = v11;

		v9 += v15;
		v10 += v16;
		v11 += v1;
		v14 += v17;
		index++;
	}

	this->field_100 = 0;
}

// @Ok
void CManipOb::ThrowPos(CVector *a2, int a3)
{
	this->CalculateThrowPositionArray(a2, a3);

	this->mFlags &= 0xF7FF;
	this->field_10C |= 1;
	this->mAngVel.vx = Rnd(32) + 64;
}

// @Ok
void CManipOb::Throw(CVector *a2)
{
	this->mFlags &= 0xF7FF;
	this->field_10C |= 1;
	this->mVel = *a2;
	this->mAcc.vy = 4096;
	this->mAngVel.vx = Rnd(32) + 64;
}

// @Ok
// Verified against 0x456D40. The original writes mVel.vx=0 after building
// the local CVector/SLineInfo temporaries (scheduling only, same end
// state before the Chunk call). TurnOffShadow/SendPulse are INLINE so
// their bodies show up inlined here in the original too; that is expected
// and not a divergence. cmpsum still shows diffs around the Chunk() call
// site because Chunk is still a printf stub in this TU and gets inlined
// there; that residue goes away once Chunk (see its own @BIGTODO) is
// implemented, it is not a bug in Smash.
void CManipOb::Smash(void)
{
	CVector v3;
	v3.vx = 0;
	v3.vy = 4096;
	v3.vz = 0;

	SLineInfo v4;
	v4.StartCoords.vx = 0;
	v4.StartCoords.vy = 0;
	v4.StartCoords.vz = 0;
	v4.EndCoords.vx = 0;
	v4.EndCoords.vy = 0;
	v4.EndCoords.vz = 0;

	v4.MinCoords.vx = 0;
	v4.MinCoords.vy = 0;
	v4.MinCoords.vz = 0;

	v4.MaxCoords.vx = 0;
	v4.MaxCoords.vy = 0;
	v4.MaxCoords.vz = 0;

	v4.Position.vx = 0;
	v4.Position.vy = 0;
	v4.Position.vz = 0;

	v4.Normal.vx = 0;
	v4.Normal.vy = (i16)0xF000;
	v4.Normal.vz = 0;

	this->mVel.vx = 0;
	this->mVel.vy = 0x20000;
	this->mVel.vz = 0x20000;

	this->Chunk(&v4, &v3);
	this->TurnOffShadow();
	this->SendPulse();

	this->Die();
}

// Session note: every helper below WAS resolvable via tools/names.json (a
// previous session's @BIGTODO comment on this function said none were --
// that was wrong, they just needed cross-checking address by address). See
// each helper's own comment for the evidence.

// @Ok
// was sub_403250 (0x403250). Sends a CBaddy-targeted CMessage (type 10, from
// nullptr) to every BaddyList member within `radius` on the XZ plane
// (Utils_CrapXZDist) and within 400 units (1638400 >> 12) vertically, that
// has mCBodyFlags bit 0x200 set. The original PC instructions at 0x403276,
// 0x40327E and 0x40328A supply no scalar parameters and one vector: pos.
static void SendExplosionMessageToNearby(CVector *pos, i32 radius)
{
	for (CBaddy *pBaddy = BaddyList; pBaddy; pBaddy = static_cast<CBaddy*>(pBaddy->mNextItem))
	{
		if (!(pBaddy->mCBodyFlags & 0x200))
			continue;

		if (spidey::math::approximateHorizontalDistance(*pos, pBaddy->mPos) >= static_cast<u32>(radius))
			continue;

		i32 dy = pBaddy->mPos.vy - pos->vy;
		if (dy < 0)
			dy = -dy;

		if (dy >= 1638400)
			continue;

		SMessageData msgData;
		msgData.mNumParams = 0;
		msgData.mNumVects = 1;
		msgData.pVects[0] = &pos->vx;

		new CMessage(0, pBaddy, 10, &msgData);
	}
}

// @Ok
// was sub_43B2A0 (0x43B2A0). Spawns `count` CChunkSmoke puffs around `pos`,
// each offset by a random magnitude (0..spread) along a random angle in the
// dirA/dirB plane; camera-relative sign (gMikeCamera[0].Position, the
// resolved name for what decompiles as qword_56F1B4/dword_56F1BC -- see
// weapons.cpp's Transform()/CSmokeRing::Display comment for the same
// address pair) picks which CChunkSmoke variant (front/back-facing, via the
// ctor's a4 sign) to use. sub_4088A0(128)/sub_43B020 are CBit::operator
// new(128) [uses a base-class allocator, not CClass's, unlike the chunks
// themselves] and CChunkSmoke::CChunkSmoke (0x43B020, confirmed via
// names.json).
static void SpawnChunkSmokeBurst(CVector *pos, CVector *dirA, CVector *dirB, i32 spread, i32 count)
{
	i32 camX = (pos->vx >> 12) - G_MIKE_CAMERA[0].Position.vx;
	i32 camZ = (pos->vz >> 12) - G_MIKE_CAMERA[0].Position.vz;

	for (i32 i = count; i != 0; i--)
	{
		i32 angle = Rnd(4096) & 0xFFF;
		i32 mag = Rnd(spread);

		i32 offX = (mag * G_RCOSSIN_TBL[angle].sin) >> 12;
		i32 offZ = (mag * G_RCOSSIN_TBL[angle].cos) >> 12;

		CVector smokePos;
		smokePos.vx = pos->vx + offX * dirA->vx + offZ * dirB->vx;
		smokePos.vy = pos->vy + offX * dirA->vy + offZ * dirB->vy;
		smokePos.vz = pos->vz + offX * dirA->vz + offZ * dirB->vz;

		i32 side = camX * (offZ >> 12) - camZ * (offX >> 12) >= 0 ? 1 : -1;

		CChunkSmoke *pSmoke = static_cast<CChunkSmoke*>(CBit::operator new(sizeof(CChunkSmoke)));
		if (pSmoke)
		{
			::new (pSmoke) CChunkSmoke(&smokePos, pos, side);
		}
	}
}

// @Ok
// was sub_456E90 (0x456E90, ~1600 bytes). Full trace of the resolved
// helpers: sub_471EA0=SFX_PlayPos, sub_4E6840=Utils_GetGroundHeight,
// sub_43CF90=Exp_Big3DExplosion, sub_4E6220=Utils_CrapDist,
// sub_470430=VectorNormal, sub_4E61A0=Utils_XZDist, sub_4E6150=Utils_Dist,
// sub_4E5E20=Utils_CalcPerps, sub_456450=CManipObChunk::CManipObChunk,
// sub_4E3880=Trig_GetLinksPointer, sub_4DFD30=Trig_SendPulse,
// dword_56E990=BaddyList (idb_globals.txt), dword_60D9D0=ZeroVector,
// this+96=mVel, this+100=mVel.vy, this+260=field_104, this+268=field_10C
// (low byte), this+288=field_120, this+292=field_124. The `SLineInfo*`
// parameter is read as raw i16s at element index 60/61/62, i.e. byte offset
// 120/122/124, which is exactly SLineInfo::Normal (CSVector, offset 0x78) --
// so this promotes the hit surface normal to a CVector for the perpendicular
// basis. The `CVector*` second parameter is never read anywhere in this
// function (confirmed by scanning the full decompile for any use); reproduced
// faithfully as unused, matching CManipOb::Smash's caller passing an unused
// value for it. The mType==324 (CSimby) reaction is a distinct virtual (not
// Hit) at vtable slot 18 -- confirmed by reading CSimby's own vtable bytes
// (off_53C0B4) directly and decompiling what's there (sub_4A7E70):
// `this->field_218 |= 0x8000; return field_218;`. It is now declared as
// CSimby::ExplosionReaction (simby.h) and called as a normal virtual. The
// earlier raw `pVtable[18]` + fastcall dispatch used MSVC slot numbering
// and put `this` in ecx, both wrong for the GCC standalone build (two
// destructor slots per vtable, `this` on the stack).
void CManipOb::Chunk(SLineInfo *pLineInfo, CVector*)
{
	CVector normal;
	normal.vx = pLineInfo->Normal.vx;
	normal.vy = pLineInfo->Normal.vy;
	normal.vz = pLineInfo->Normal.vz;

	u16 songId = static_cast<u16>(this->field_104);
	if (songId != 0)
	{
		SFX_PlayPos(songId | 0x8000u, &this->mPos, 0);
	}
	else
	{
		SFX_PlayPos((this->field_10C & 8) ? 28 : 27, &this->mPos, 0);
	}

	CVector shakePos = this->mPos;
	i32 groundY = Utils_GetGroundHeight(&this->mPos, 0, 700, 0);
	if (groundY != -1)
	{
		shakePos.vy = groundY - 204800;
	}

	SendExplosionMessageToNearby(&shakePos, 2000);

	if (this->field_10C & 0x20)
	{
		Exp_Big3DExplosion(&this->mPos);

		i32 mechDy = (this->mPos.vy - G_MECHLIST->mPos.vy) >> 12;
		if (mechDy > -300 && mechDy < 300)
		{
			i32 mechDist = spidey::math::approximateDistance(this->mPos, G_MECHLIST->mPos);
			if (mechDist < 1024)
			{
				SHitInfo hitInfo;
				hitInfo.field_0 = 12;
				hitInfo.field_8 = static_cast<u16>(30 * mechDist / 1024);

				hitInfo.field_C.vx = (G_MECHLIST->mPos.vx - this->mPos.vx) >> 12;
				hitInfo.field_C.vy = 0;
				hitInfo.field_C.vz = (G_MECHLIST->mPos.vz - this->mPos.vz) >> 12;
				VectorNormal(reinterpret_cast<VECTOR*>(&hitInfo.field_C), reinterpret_cast<VECTOR*>(&hitInfo.field_C));

				G_MECHLIST->Hit(&hitInfo);
			}
		}

		for (CBaddy *pBaddy = BaddyList; pBaddy; pBaddy = static_cast<CBaddy*>(pBaddy->mNextItem))
		{
			if (!(pBaddy->mCBodyFlags & 0x1000))
				continue;

			i32 dy = (this->mPos.vy - pBaddy->mPos.vy) >> 12;
			if (dy <= -300 || dy >= 300)
				continue;

			i32 dist = spidey::math::approximateDistance(this->mPos, pBaddy->mPos);

			if (pBaddy->mType == 324)
			{
				if (dist <= 1024)
					static_cast<CSimby*>(pBaddy)->ExplosionReaction();
			}
			else if (dist < 1024)
			{
				SHitInfo hitInfo;
				hitInfo.field_0 = 28;
				hitInfo.field_8 = static_cast<u16>(50 * dist / 1024);

				hitInfo.field_C.vx = (pBaddy->mPos.vx - this->mPos.vx) >> 12;
				hitInfo.field_C.vy = (pBaddy->mPos.vy - this->mPos.vy) >> 12;
				hitInfo.field_C.vz = (pBaddy->mPos.vz - this->mPos.vz) >> 12;
				VectorNormal(reinterpret_cast<VECTOR*>(&hitInfo.field_C), reinterpret_cast<VECTOR*>(&hitInfo.field_C));

				hitInfo.field_18 = (this->field_10C & 0x20) ? 500 : 300;
				hitInfo.field_1A = 15;

				pBaddy->Hit(&hitInfo);
			}
		}
	}

	i32 modelCount = 0;
	i32 *pModelIds = reinterpret_cast<i32*>(this->field_120);
	while (pModelIds[modelCount])
		modelCount++;

	if (modelCount != 0)
	{
		if (normal.vy >= -3000)
		{
			i32 halfXZSpeed = Utils_XZDist(&ZeroVector, &this->mVel) >> 1;
			i32 halfSpeed = Utils_Dist(ZeroVector, this->mVel) >> 1;

			CVector perpUp;
			CVector perpSide;
			perpUp.vx = 0; perpUp.vy = 0; perpUp.vz = 0;
			perpSide.vx = 0; perpSide.vy = 0; perpSide.vz = 0;
			Utils_CalcPerps(&normal, &perpUp, &perpSide);

			for (i32 angle = 0; angle < 4092; angle += 682)
			{
				i32 idx = angle & 0xFFF;

				CVector vel = (halfSpeed * ((G_RCOSSIN_TBL[idx].sin * perpUp) >> 12));
				vel += halfXZSpeed * normal;
				vel += halfSpeed * ((G_RCOSSIN_TBL[idx].cos * perpSide) >> 12);

				CManipObChunk *pChunk = static_cast<CManipObChunk*>(CClass::operator new(sizeof(CManipObChunk)));
				if (pChunk)
				{
					::new (pChunk) CManipObChunk(pModelIds[Rnd(modelCount)], &this->mPos, &vel);
				}
			}

			SpawnChunkSmokeBurst(&this->mPos, &perpUp, &perpSide, 300, 24);
		}
		else
		{
			i32 halfXZSpeed = Utils_XZDist(&ZeroVector, &this->mVel) >> 1;

			for (i32 angle = 0; angle < 4092; angle += 682)
			{
				i32 idx = angle & 0xFFF;

				CVector vel;
				vel.vx = halfXZSpeed * G_RCOSSIN_TBL[idx].sin;
				vel.vy = -this->mVel.vy;
				vel.vz = halfXZSpeed * G_RCOSSIN_TBL[idx].cos;

				CManipObChunk *pChunk = static_cast<CManipObChunk*>(CClass::operator new(sizeof(CManipObChunk)));
				if (pChunk)
				{
					::new (pChunk) CManipObChunk(pModelIds[Rnd(modelCount)], &this->mPos, &vel);
				}
			}

			CVector zAxis;
			zAxis.vx = 0; zAxis.vy = 0; zAxis.vz = 4096;
			CVector xAxis;
			xAxis.vx = 4096; xAxis.vy = 0; xAxis.vz = 0;

			SpawnChunkSmokeBurst(&this->mPos, &zAxis, &xAxis, 300, 24);
		}
	}

	this->SendPulse();
}

// @Ok
 void CManipOb::TurnOffShadow(void)
{
	CItem *v1 = this->field_11C;
	if (v1)
	{
		SModel *model = G_PSXREGION[v1->mRegion].ppModels[v1->mModel];
		model->Flags |= 0x20;
	}
}

// @Ok
 void CManipOb::SendPulse(void)
{
	if(!this->field_124)
	{
		this->field_124 = 1;
		Trig_SendPulse(Trig_GetLinksPointer(this->mNode));
	}
}

// @Ok
void CManipOb::Pickup(void)
{
	this->mFlags |= 0x20;
	this->mFlags |= 0x800;

	this->TurnOffShadow();
	this->SendPulse();
}

void validate_CManipOb(void)
{
	VALIDATE_SIZE(CManipOb, 0x128);

	VALIDATE(CManipOb, mNumVectors, 0xF8);
	VALIDATE(CManipOb, pVectors, 0xFC);
	VALIDATE(CManipOb, field_100, 0x100);

	VALIDATE(CManipOb, field_104, 0x104);
	VALIDATE(CManipOb, field_108, 0x108);

	VALIDATE(CManipOb, field_10C, 0x10C);

	VALIDATE(CManipOb, field_110, 0x110);

	VALIDATE(CManipOb, field_11C, 0x11C);
	VALIDATE(CManipOb, field_120, 0x120);
	VALIDATE(CManipOb, field_124, 0x124);

	VALIDATE_VTABLE(CManipOb, Smash, 5);
}

void validate_CManipObChunk(void)
{
	VALIDATE_SIZE(CManipObChunk, 0x100);

	VALIDATE(CManipObChunk, field_F8, 0xF8);
	VALIDATE(CManipObChunk, field_FC, 0xFC);
}

#include "compatibility/my_patch.h"

// @Bogus
// Only three functions here have a call closure that stays on game memory.
//
// What blocks the rest:
//
// 1. Rnd (utils.cpp) runs on three file-local statics that nothing seeds on
//    our side, because Utils_InitialRand is not hooked, so it returns 0 in
//    hooked code. That rules out CManipOb::TugImpulse, Drop, Throw, ThrowPos
//    and CManipObChunk::DoPhysics (and CManipObChunk::AI, which calls it).
//
// 2. BaddyList (0x0056E990, baddy.cpp), EnvironmentalObjectList (0x0060DAAC,
//    ob.cpp), gObjFile (0x006B4674) and gObjFileRegion (0x006B3824) are still
//    plain repo globals. Both constructors and both destructors attach or
//    detach through them, and CManipOb::Chunk walks BaddyList and MechList
//    (0x006A9038, spidey.cpp).
//
// 3. CManipOb::Smash calls Chunk, so it goes with it.
void patch_manipob(void)
{
	PATCH_PUSH_RET(0x00456B00, CManipOb::Pickup);
	PATCH_PUSH_RET(0x00456B60, CManipOb::CalculateThrowPositionArray);
	PATCH_PUSH_RET(0x00457AA0, CManipOb::GetAttachPoint);
}
