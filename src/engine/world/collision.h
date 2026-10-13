#pragma once

#ifndef M3DCOLIJ_H
#define M3DCOLIJ_H


#include "compatibility/export.h"
#include "core/fixed_vector.h"
#include "engine/rendering/ps2funcs.h"

class CBody;

struct SLineInfo
{
	CVector StartCoords;
	CVector EndCoords;

	CVector MinCoords;
	CVector MaxCoords;

	// offset: 0030
	i32 iLo;
	// offset: 0034
	i32 iHi;
	// offset: 0038
	i32 jLo;
	// offset: 003C
	i32 jHi;

	i32 Distance;
	i32 Length;

	MATRIX WorldCst;
	CItem* pItem;

	CVector Position; // should be Vector?
	CSVector Normal; // should be SVector?

	u32 *pFace;
	i32 Model;

	u8 RecordTriggerZoneHits;
	u8 DropDown;

	u16 Inquiry;
	i32 tNear;

	// offset: 0090
	u32 tNumtrLo;
	// offset: 0094
	i32 tNumtrHi;
	// offset: 0098
	u32 tDenomLo;
	// offset: 009C
	i32 tDenomHi;
	// offset: 00A0
	u32 NormalOffset;
};


void validate_SLineInfo(void);
void validate_Vector(void);

EXPORT void M3dColij_InitLineInfo(SLineInfo *);
// signature confirmed via thps2-stuff/decls.h (THPS2 PSX demo symbols):
// M3dColij_LineToSphere__FRC7CVectorT0R7CVectorP5CBodyT3i, returning CBody*.
EXPORT CBody * M3dColij_LineToSphere(CVector*, CVector*, CVector*, CBody*, CBody*, i32);

EXPORT void M3dColij_LineToItem(CItem*, SLineInfo*);
EXPORT void M3dColij_LineToThisItem(CItem*, SLineInfo*);
EXPORT i32 M3dColij_GetLineInfo(SLineInfo *);
EXPORT void M3dColij_LineInfoFixup(SLineInfo *);
EXPORT void M3dColij_LineToItemZoned(CItem **,SLineInfo *);
EXPORT void NextInquiry(void);

// The collision globals below are all shared between the exe and us, so every use
// site goes through a G_* macro on the exe's address. Addresses proved from the
// disassembly of M3dZone_LineToItem (0x4549A0), which reads or writes all six of the
// flag/mask globals in one run:
//   mov [5FBDA8h],esi              -> M3dColij_OneMask = 0
//   mov dword [5FBDDCh],0FFFFFFFFh -> M3dColij_ZeroMask = -1
//   mov eax,[5FBEE4h]              -> if (LineOfSightCheck)
//   cmp [5FBEE0h],esi              -> if (!BaddyCollisionCheck)
//   cmp [5FBEDCh],esi              -> if (CameraCollisionCheck)
//   cmp [5FBEE8h],esi              -> if (TriggerCollisionCheck)
// All six also match idb_globals.txt.
//
// M3dColij_LineToItem/LineToItemZoned/LineInfoFixup are already hooked, and the
// masks reach the hooked path through TestItemFaces, while the only writer
// (M3dZone_LineToItem) is not hooked. So the macros must stay on game memory.

EXPORT extern i32 LineOfSightCheck;
//#define G_LINE_OF_SIGHT_CHECK (LineOfSightCheck)
#define G_LINE_OF_SIGHT_CHECK (*reinterpret_cast<i32*>(0x005FBEE4))

EXPORT extern u32 M3dColij_OneMask;
//#define G_M3DCOLIJ_ONE_MASK (M3dColij_OneMask)
#define G_M3DCOLIJ_ONE_MASK (*reinterpret_cast<u32*>(0x005FBDA8))

EXPORT extern u32 M3dColij_ZeroMask;
//#define G_M3DCOLIJ_ZERO_MASK (M3dColij_ZeroMask)
#define G_M3DCOLIJ_ZERO_MASK (*reinterpret_cast<u32*>(0x005FBDDC))

EXPORT extern i32 BaddyCollisionCheck;
//#define G_BADDY_COLLISION_CHECK (BaddyCollisionCheck)
#define G_BADDY_COLLISION_CHECK (*reinterpret_cast<i32*>(0x005FBEE0))

EXPORT extern i32 CameraCollisionCheck;
//#define G_CAMERA_COLLISION_CHECK (CameraCollisionCheck)
#define G_CAMERA_COLLISION_CHECK (*reinterpret_cast<i32*>(0x005FBEDC))

EXPORT extern i32 TriggerCollisionCheck;
//#define G_TRIGGER_COLLISION_CHECK (TriggerCollisionCheck)
#define G_TRIGGER_COLLISION_CHECK (*reinterpret_cast<i32*>(0x005FBEE8))

// The one shared SLineInfo every ground/line-of-sight query fills in. Web_GetGroundY
// (0x4F54A0) does "push 5FBE38h; call 4524C0h" (M3dColij_InitLineInfo) and
// "push 1; push 5FBE38h; call 4549A0h" (M3dZone_LineToItem), then reads pItem at
// [5FBEA0h] (0x5FBE38 + 0x68) and Position.vy at [5FBEA8h] (0x5FBE38 + 0x70).
// initGlineInfo (0x452460) zeroes it from 0x5FBE38 up. Also in idb_globals.txt.
EXPORT extern SLineInfo gLineInfo;
//#define G_LINE_INFO (gLineInfo)
#define G_LINE_INFO (*reinterpret_cast<SLineInfo*>(0x005FBE38))

// Shared pose buffer handed to every CSuper::ApplyPose call. idb_globals.txt names
// 0x005564E4 gUnkPose, and the exe pushes that address at every call site (for
// example SpideyAI_WaitForSimbyGrab 0x4A8737 "push offset unk_5564E4").
// CSuper::ApplyPose (0x460E80) and M3dUtils_ReadLinksPacket (0x453C50, the function
// that resolves the parent indices in place) are both hooked already, so this has to
// be the exe's array or the two halves resolve different copies.
EXPORT extern i16 gUnkPose[74];
//#define G_UNK_POSE (gUnkPose)
#define G_UNK_POSE (reinterpret_cast<i16*>(0x005564E4))

void patch_m3dcolij(void);
#endif
