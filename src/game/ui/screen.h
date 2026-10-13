#pragma once

#ifndef SCREEN_H
#define SCREEN_H

#include "compatibility/export.h"
#include "core/fixed_vector.h"

// guess: pointer to some "current view/clip" record, only known from the disasm (loaded
// as a pointer from this fixed address, then two u16 fields at +8/+0xA read as a Y-range
// / depth clip test). No idb_globals.txt entry for this address, name and layout are our
// guess. Shared by screen.cpp (Screen_DrawArrow) and bit.cpp (DisplayLinked2EndedBitListLeftover
// and friends) per the repo's G_* macro placement rule (one definition when 2+ files use it).
#define G_VIEW_CLIP_INFO (*reinterpret_cast<u8**>(0x0064E514))

EXPORT void Screen_TargetOn(bool);
EXPORT void Screen_DrawArrow(void);
EXPORT void Screen_DrawTarget(void);
EXPORT void Screen_SepiaFade(void);
EXPORT void Screen_SetTarget(CVector *,u16,i16);
EXPORT void Screen_StartCircularFadeIn(i32,i32);
EXPORT void Screen_UpdateFades(void);
void patch_screen(void);


#endif
