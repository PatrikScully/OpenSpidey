#pragma once

#ifndef SCORPION_H
#define SCORPION_H

#include "compatibility/export.h"
#include "game/actors/baddy.h"
#include "engine/assets/reloc.h"


// The tail geometry buffer CScorpion::InitialiseTailPSX (0x00489050) hangs off
// the tail item (CScorpion::mpTailGeometry). Only the three parts named here
// are known: CScorpion::TailRenderer (0x00489810) rebuilds them every frame,
// one ring of four vertices and four normals per tail node. The rest of the
// buffer is model header data nothing in the repo reads yet.
struct STailGeometry
{
	PADDING(0xC);

	// (min << 16) | max of the tail bounding box in world units >> 12,
	// relative to the tail item's own position
	i32 BoundsX;
	i32 BoundsY;
	i32 BoundsZ;

	PADDING(0x1C-0x14-4);

	SVECTOR Vertices[23*4];
	SVECTOR Normals[23*4];
};


class CScorpion : public CBaddy {
	public:

		EXPORT CScorpion(i16 *,i32);
		EXPORT CScorpion(void);
		EXPORT void NextRoom(void);
		EXPORT void* GetCurrentTarget(void);
		EXPORT CSuper* FindJonah(void);
		EXPORT i32 SetJonahHandle(SHandle*);
		EXPORT void DoIntroSequence(void);
		EXPORT void Gloat(void);
		EXPORT void DetermineTarget(void);
		EXPORT void TakeHit(void);
		EXPORT void GetTrapped(void);
		EXPORT void PlayXA_NoRepeat(i32, i32, i32, i32*, CBody*);
		EXPORT i32 ScorpPathCheck(CVector*, CVector*, CVector*, i32);
		EXPORT i32 PathLooksGood(CVector*);
		EXPORT u32 WhatShouldIDo(void);
		EXPORT void TargetPlayer(i32);
		EXPORT i32 GetEnvironmentalObjectTarget(void);

		// CBaddy vtable slot 17 (byte offset 0x44), the per frame "draw my own
		// extra geometry" hook. Display (main.cpp) calls it for every scorpion
		// on the baddy list, the same slot CDocOc and CSuperDocOck fill with
		// RenderClaws. The repo's CBaddy does not declare that slot yet (it
		// has 12 virtuals, the original has at least 16), so this is a plain
		// member for now.
		EXPORT void TailRenderer(void);


		i32 field_324;

		PADDING(0x3EC-0x324-4);

		// read (!= 0 check) by Panel_DisplayHealthBar (panel.cpp, offset 0x3EC
		// relative to a CBody* boss pointer) to pick between the default and
		// an alternate health-bar icon texture; meaning otherwise unknown.
		i32 field_3EC;

		PADDING(0x3F8-0x3EC-4);

		// Two embedded CItem sub-objects (confirmed against the disasm of both
		// constructors at 0x483290/0x483450: each is default-constructed with a
		// real call to CItem::CItem(), this+0x3F8 and this+0x440, then the ctor
		// pokes mRegion to 0xFF right after construction). Meaning unclear (a
		// guess: a pair of scorpion-specific marker items, maybe used by the
		// still-undecompiled tail code), left as plain field_XXX names.
		CItem field_3F8;

		PADDING(0x43C-0x3F8-sizeof(CItem));

		// 0x43C. Written by CScorpion::InitialiseTailPSX (0x00489050) and
		// rebuilt every frame by TailRenderer.
		STailGeometry* mpTailGeometry;

		CItem field_440;

		// Both constructors zero this whole range (0x480-0xBD4) with three
		// dword-triple loops in the disasm (this+0x48C x4, this+0x4BC x23,
		// this+0x5D0 x128); no struct is known for the parts still left as
		// padding, so the ctors zero it with raw memset calls instead of
		// guessing field names.
		PADDING(0x4BC-0x440-sizeof(CItem));

		// 0x4BC, the "x23" loop above. The tail node positions. TailRenderer
		// puts the tail item at the midpoint of the first and the last one and
		// builds a ring of four vertices around each of them.
		CVector mTailNodes[23];

		PADDING(0xBD4-0x4BC-sizeof(CVector)*23);

		i32 field_BD4;
		i32 field_BD8;

		PADDING(0xBE8-0xBD8-4);

		i32 field_BE8;
		CSuper* field_BEC;
		SHandle hCurrentTarget;

		i32 field_BF8;

		PADDING(0xC00-0xBF8-4);

		// @FIXME guess type: candidate environmental object position
		CVector field_C00;

		// field_C0C is explicitly cleared at the end of the (i16*,i32) ctor
		// (the void ctor leaves it untouched); no other reference to it in
		// this file yet, name is a placeholder.
		u8 field_C0C;

		PADDING(0xC10-0xC00-sizeof(CVector)-1);

		i32 field_C10;
		i32 field_C14;
		i32 field_C18;

		PADDING(4);

		i32 field_C20;

		PADDING(0xC28-0xC20-4);
};

class CConstantLaser : public CNonRenderedBit
{
	public:

		EXPORT CConstantLaser(i32);
		EXPORT virtual ~CConstantLaser(void) OVERRIDE;
		EXPORT void SetRGB(u8, u8, u8, u8, u8, u8, u8, u8, u8, u8, u8, u8);

		void* field_3C;
		void* field_40;

		PADDING(8);

		u8 field_4C[12];

		PADDING(4);

		void* field_5C;
		i32 field_60;
};

EXPORT void Scorpion_GetCurrentTarget(const u32*, u32*);
EXPORT void Scorpion_RelocatableModuleClear(void);
EXPORT void Scorpion_RelocatableModuleInit(reloc_mod*);
EXPORT void Scorpion_CreateScorpion(const u32*, u32*);

void validate_CScorpion(void);
void validate_CConstantLaser(void);

void patch_scorpion(void);

#endif
