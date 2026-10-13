#pragma once

#ifndef M3DUTILS_H
#define M3DUTILS_H

#include "engine/world/ob.h"
#include "compatibility/export.h"

struct SHook
{
	CSVector Part;
	i16 Offset;
};

EXPORT void M3dUtils_ReadLinksPacket(CSuper*, void*);
EXPORT void M3dUtils_InBetween(CSuper*);
EXPORT void M3dUtils_BuildPose(CSuper*);
// @Note: the 4th param was typed CItem* before this was decompiled; the
// original reads mFrame/mAnim/mAnimMode (CSuper-only fields, see the
// definition) through it, and every call site already passes a CSuper*,
// so this is retyped to CSuper* here.
EXPORT void M3dUtils_InterpolateVectors(i32, i32, u32*, CSuper*, i32, i32);
EXPORT i32 M3dUtils_GetPartAngles(CSuper*, i32, CSVector*, CSVector*);
EXPORT void M3dUtils_GetHookPosition(VECTOR*, CSuper*, int);
EXPORT void M3dUtils_GetDynamicHookPosition(VECTOR*, CSuper*, SHook*);
EXPORT void M3dUtils_ReadHooksPacket(CSuper*, void*);

void validate_SHook(void);
void patch_m3dutils(void);

#endif
