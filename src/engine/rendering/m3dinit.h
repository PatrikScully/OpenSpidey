#ifndef M3DINIT_H
#define M3DINIT_H

#pragma once

#include "compatibility/export.h"

EXPORT extern u32 M3d_FadeColour;
// The render target size, written by m3dinit's own setup and read from 15 files.
// The writers are not hooked, so a repo-local copy stays 0 and every
// gGameResolution/Xres aspect ratio comes out as 0/0. The already hooked
// Flash_Display divides by both, so its fade quad silently never drew.
// Addresses from Flash_Display's disassembly (0x0043DA57 reads Yres,
// 0x0043DA6A reads Xres).
EXPORT extern u32 Xres;
//#define G_XRES (Xres)
#define G_XRES (*reinterpret_cast<u32*>(0x0061B5FC))
EXPORT extern u32 Yres;
//#define G_YRES (Yres)
#define G_YRES (*reinterpret_cast<u32*>(0x00628614))

union ItemOffsetUnion
{
	u32 Full;
	u8 Byte[4];
};

union UnionItemInfo
{
	u16 Full;
	u8 Byte[2];
};

struct STexWibItemInfo
{
	ItemOffsetUnion ItemOffset;

	PADDING(2);


	u8 field_6;
	u8 field_7;
	u8 field_8;
	u8 field_9;

	PADDING(2);

	UnionItemInfo field_C;

	u8 ZeroUAmplitudes;
	u8 ZeroVAmplitudes;
};


EXPORT void M3dInit_FlagZeroWibbles(STexWibItemInfo *pTexWibItemInfo);
EXPORT void DCClearRegion(i32);
EXPORT void M3dInit_InitAtStart(void);
EXPORT void M3dInit_ParsePSX(i32);
EXPORT void M3dInit_SetFoggingParams(long,long,u32);
EXPORT void M3dInit_SetResolution(u32,u32);
EXPORT void alloc_dc_models(i32,i32);
EXPORT void setup_pulsing_colors(i32);

void validate_STexWibItemInfo();

#endif
