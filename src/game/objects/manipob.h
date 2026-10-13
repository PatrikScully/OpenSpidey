#pragma once

#ifndef MANIPOB_H
#define MANIPOB_H

#include "compatibility/export.h"
#include "engine/world/ob.h"
#include "engine/world/collision.h"

class CManipOb : public CBody
{
	public:
		EXPORT void CalculateThrowPositionArray(CVector*, int);
		EXPORT void ThrowPos(CVector *, int);
		EXPORT void Throw(CVector *);
		EXPORT void GetAttachPoint(CVector *);
		EXPORT u8 TugImpulse(CVector *,CVector *);

		EXPORT void Chunk(SLineInfo*, CVector*);
		EXPORT void TurnOffShadow(void);
		EXPORT void SendPulse(void);
		EXPORT void Pickup(void);
		EXPORT void Drop(CVector*);

		EXPORT CManipOb(i16*, i32);
		EXPORT virtual ~CManipOb(void);

		EXPORT virtual void Smash(void);

		PADDING(4);

		i32 mNumVectors;
		CVector *pVectors;
		i32 field_100;

		i16 field_104;
		PADDING(2);

		i16 field_108;

		i32 field_10C;

		CVector field_110;

		CItem *field_11C;
		u32 field_120;

		u8 field_124;

		PADDING(0x128-0x124-1);
};

class CManipObChunk : public CBody
{
	public:
		EXPORT CManipObChunk(u32,CVector *,CVector *);
		EXPORT ~CManipObChunk(void) OVERRIDE;

		EXPORT void AI(void) OVERRIDE;
		EXPORT void CManipObChunk::DoPhysics(void);

		PADDING(4);

		// Bounce/hit counter. 0 when spawned. DoPhysics increments it on the
		// first line-of-sight hit found each frame; while it stays below 1
		// the chunk bounces off the hit normal, once it reaches 1 the chunk
		// dies instead. Confirmed real (read/written as this+0xF8) by
		// disassembly of 0x4567d5-0x4567ed, not padding.
		i32 field_F8;

		i32 field_FC;

};

void validate_CManipOb(void);
void validate_CManipObChunk(void);

void patch_manipob(void);

#endif
