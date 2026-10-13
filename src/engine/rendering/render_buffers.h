#pragma once

#ifndef DB_H
#define DB_H

#include "compatibility/export.h"

// The render buffer globals. The exe owns them: Db_Init, Db_FlipClear and
// CalcPolyBufferEnd write them every frame and none of db.cpp is hooked, so
// every reader has to go through the exe's memory or it sees a dead buffer.
// Addresses proved from the disassembly, see the comments on each macro.

EXPORT extern u32* pPoly;
// Db_FlipClear (0x430630) ends with "mov [56FB04h],ecx" after masking
// pDoubleBuffer->Polys, and Db_DeleteOTsAndPolyBuffers (0x4305C0) stores 1 there.
//#define G_PPOLY (pPoly)
#define G_PPOLY (*reinterpret_cast<u32**>(0x0056FB04))

EXPORT extern u8* PolyBufferEnd;
// CalcPolyBufferEnd (0x4553E0) is five instructions and the last one is
// "mov [5FCD1Ch],ecx".
//#define G_POLY_BUFFER_END (PolyBufferEnd)
#define G_POLY_BUFFER_END (*reinterpret_cast<u8**>(0x005FCD1C))

// @Note: real name is RECT but don't want collisions with win32
struct DB_RECT {
	// offset: 0000
	i16 x;
	// offset: 0002
	i16 y;
	// offset: 0004
	i16 w;
	// offset: 0006
	i16 h;
};

struct DISPENV {
	// offset: 0000 (8 bytes)
	struct DB_RECT disp;
	// offset: 0008 (8 bytes)
	struct DB_RECT screen;
	// offset: 0010
	u8 isinter;
	// offset: 0011
	u8 isrgb24;
	// offset: 0012
	u8 pad0;
	// offset: 0013
	u8 pad1;
};

// size: 0x40
struct DR_ENV {
	// offset: 0000
	u32 tag;
	// offset: 0004 (60 bytes)
	u32 code[15];
};

struct DRAWENV {
	// offset: 0000 (8 bytes)
	struct DB_RECT clip;
	// offset: 0008 (4 bytes)
	i16 ofs[2];
	// offset: 000C (8 bytes)
	struct DB_RECT tw;
	// offset: 0014
	u16 tpage;
	// offset: 0016
	u8 dtd;
	// offset: 0017
	u8 dfe;
	// offset: 0018
	u8 isbg;
	// offset: 0019
	u8 r0;
	// offset: 001A
	u8 g0;
	// offset: 001B
	u8 b0;
	// offset: 001C (64 bytes)
	struct DR_ENV dr_env;
};

struct SDoubleBuffer
{
	struct DRAWENV Draw;

	struct DISPENV Disp;

	u32* OrderingTable;

	PADDING(4);

	u8* Polys;
};


EXPORT extern SDoubleBuffer* pDoubleBuffer;
// Db_FlipClear (0x430630) opens with "mov ecx,[56FB2Ch]" and writes it twice.
//#define G_PDOUBLE_BUFFER (pDoubleBuffer)
#define G_PDOUBLE_BUFFER (*reinterpret_cast<SDoubleBuffer**>(0x0056FB2C))

// No live code in the exe touches this one, only the dead block at
// 0x430680 (the IDB calls that range optimized_unused_garbage), and nothing
// in the repo reads it either. Left repo local on purpose.
EXPORT extern SDoubleBuffer* pOtherBuffer;

EXPORT extern SDoubleBuffer DoubleBuffer[2];
// Db_FlipClear (0x430636) loads the address as "mov eax,56FB60h" and the other
// half of the pair as the immediate 56FBDCh, which is 0x56FB60 + 0x7C, so the
// 0x7C stride in SDoubleBuffer is right too.
//#define G_DOUBLE_BUFFER (DoubleBuffer)
#define G_DOUBLE_BUFFER (reinterpret_cast<SDoubleBuffer*>(0x0056FB60))

EXPORT extern u32 Db_SkyColor;
// Db_UpdateSky (0x4302D0) opens with "mov ecx,[56FC74h]".
//#define G_DB_SKY_COLOR (Db_SkyColor)
#define G_DB_SKY_COLOR (*reinterpret_cast<u32*>(0x0056FC74))

// The dword right after Db_SkyColor. The SetSkyColor trigger command and
// SpideyAI0's water effect both write it with the colour they then push into
// Db_SkyColor, so it reads like the target half of a current/target pair, but
// the IDB does not name it and it may well be Db_SkyColor[1]. trig.cpp and
// spid_ai0.cpp each had their own copy of this pointer, now they share this.
EXPORT extern u32 Db_SkyColorTarget;
//#define G_DB_SKY_COLOR_TARGET (Db_SkyColorTarget)
#define G_DB_SKY_COLOR_TARGET (*reinterpret_cast<u32*>(0x0056FC78))

EXPORT void Db_CreateOTsAndPolyBuffers(void);
EXPORT void Db_DefaultScreenOffsets(void);
EXPORT void Db_DeleteOTsAndPolyBuffers(void);
EXPORT void Db_FlipClear(void);
EXPORT void Db_Init(void);
EXPORT void Db_UpdateSky(void);

void validate_SDoubleBuffer(void);
void validate_DB_RECT(void);
void validate_DR_ENV(void);
void validate_DRAWENV(void);
void validate_DISPENV(void);

#endif
