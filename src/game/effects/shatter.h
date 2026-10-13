#pragma once

#ifndef SHATTER_H
#define SHATTER_H

#include "compatibility/export.h"
#include "engine/world/ob.h"

EXPORT extern i32 gGlassShatterSound;

// 0x6A75F8: CVector, world position of the face being shattered. Set by Shatter_Face (still a
// stub, not yet decompiled) and consumed by CShatterBit::SetPos (bit.cpp) to compute a shard's
// outward "explosion" velocity; also used by Exp_GlowFlash per Shatter_Face's own decompilation
// notes further down in this file. Guess name, no idb_globals.txt entry for this address.
#define G_SHATTER_FACE_CENTER (*reinterpret_cast<CVector*>(0x006A75F8))

// 0x6A768C: holds a POINTER to an i16[3] table (confirmed: every use dereferences it a second
// time, e.g. *(i16*)(dword_6A768C + 4)). Read as a per-axis velocity/jitter scale:
// CShatterBit::SetPos (bit.cpp) uses all 3 axes to bias a freshly-built shard's initial
// velocity; Split (below) uses only axis 2 (z) to jitter a shattered triangle's corner deltas
// before constructing the CShatterBit.
// WRITE SITE FOUND 2026-08-31 (fresh IDA trace of Shatter_Face, 0x48C0D0, superseding the old
// "no write site found" guess above): Shatter_Face sets this, unconditionally, once per call,
// to the address of the current face's NORMAL vector (an SVECTOR inside the model's vertex
// table, `vertexTable + 8 * (faceNormalIndex)`, faceNormalIndex read from the face record and
// shifted right 3 since the table stride is 8 bytes = 2^3). So this is not a fixed table at
// all: it is "the current face's normal, as an i16[3]", re-pointed every Shatter_Face call.
// That actually makes both consumers make more physical sense: CShatterBit::SetPos scaling a
// shard's outward velocity by the face normal (fly away from the surface) and Split jittering
// along the normal's z axis are both exactly what you would want a face normal for. Name kept
// (it still accurately describes the ROLE at both use sites), only the origin was wrong.
#define G_SHATTER_VELOCITY_SCALE (*reinterpret_cast<i16**>(0x006A768C))

// 0x6A75D8 and 0x6A7688: two u16 globals, both written only inside Shatter_Face (0x48C0D0,
// still a stub, not yet decompiled) and read only inside Split (below), confirmed by xrefs.
// Forwarded as CChunkBit::SetUVs's first two (ushort,ushort) parameters. Per SetUVs's own
// disasm (0x40B910), only the FIRST of the two is actually read inside SetUVs (stored into
// CChunkBit's own undeclared field at +0xB4); the second is a dead parameter there (same class
// of unused-but-present param as CShatterBit's own ctor 5th arg, see bit.h). Names and exact
// semantics are our guess; likely a texture/page id (used one) and a leftover/unused value.
#define G_SHATTER_UV_TEX_ID (*reinterpret_cast<u16*>(0x006A75D8))
#define G_SHATTER_UV_UNUSED (*reinterpret_cast<u16*>(0x006A7688))

EXPORT void Shatter_MaybeMakeGlassShatterSound(void);
EXPORT void CalcRGB(i32,u32,i32,u32 *);
EXPORT i32 Shatter_Face(CItem *,u32 *,i32,i32,i32,i32,i32);
EXPORT i32 Shatter_Glass(i32,CVector const *,CVector const *,CVector const *,CVector const *,u8,u8,u8);
EXPORT i32 Shatter_Item(CItem *,i32,i32);
EXPORT void Split(CVector const *,CVector const *,CVector const *,i32,i32,i32,i32,i32,i32,u32,i32);
EXPORT void TransformVertex(CVector *,SVECTOR *,u8 *,i32);

#endif
