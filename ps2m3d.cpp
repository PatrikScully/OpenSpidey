#include "ps2m3d.h"
#include "ps2funcs.h"
#include "db.h"
#include "PCGfx.h"
#include "m3dinit.h"
#include "SpideyDX.h"
#include "spool.h"
#include "algebra.h"
#include "screen.h"
#include "decomp.h"
#include <math.h>
#include <string.h>

#include "validate.h"

// Same address as PCTex.cpp/spool.cpp/trig.cpp's own file-local
// G_LOWGRAPHICS copies (0x6B78F8, gLowGraphics per CLAUDE.md); this file
// needs its own copy too, following the existing repo pattern of a
// per-file macro rather than a shared header (that "never duplicate across
// files" rule is not consistently applied to this particular macro yet).
//#define G_LOWGRAPHICS (gLowGraphics)
#define G_LOWGRAPHICS (*reinterpret_cast<i32*>(0x006B78F8))

#ifndef SPIDEY_STANDALONE
i32 gWideScreen;
#else
extern i32 gWideScreen;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT u32* pColourTable;
#else
extern u32* pColourTable;
#endif

// XblanksNow / XblanksThen from the PS2 source (m3d.mik): vblank-based frame
// timer, updated elsewhere (not in this file). Only referenced (read) here.
// Not in idb_globals.txt, only ever read in ps2m3d.cpp functions.
//#define G_XBLANKS_NOW (gXblanksNow)
#define G_XBLANKS_NOW (*reinterpret_cast<u32*>(0x0065CFA4))
//#define G_XBLANKS_THEN (gXblanksThen)
#define G_XBLANKS_THEN (*reinterpret_cast<u32*>(0x00660F88))


// @Ok
// @Matching
vector4d& vector4d::operator=(const vector4d& other)
{
	this->field_0[0] = other.field_0[0];
	this->field_0[1] = other.field_0[1];
	this->field_0[2] = other.field_0[2];
	this->field_0[3] = other.field_0[3];

	return *this;
}

// @Ok
INLINE vector4d& matrix4x4::operator[](i32 index)
{
	return this->field_0[index];
}

// @Ok
// @Matching
matrix4x4::matrix4x4(
			f32 a1,
			f32 a2,
			f32 a3,
			f32 a4,
			f32 a5,
			f32 a6,
			f32 a7,
			f32 a8,
			f32 a9,
			f32 a10,
			f32 a11,
			f32 a12,
			f32 a13,
			f32 a14,
			f32 a15,
			f32 a16)
{
	this->field_0[0].field_0[0] = a1;
	this->field_0[0].field_0[1] = a2;
	this->field_0[0].field_0[2] = a3;
	this->field_0[0].field_0[3] = a4;
	
	this->field_0[1].field_0[0] = a5;
	this->field_0[1].field_0[1] = a6;
	this->field_0[1].field_0[2] = a7;
	this->field_0[1].field_0[3] = a8;
	
	this->field_0[2].field_0[0] = a9;
	this->field_0[2].field_0[1] = a10;
	this->field_0[2].field_0[2] = a11;
	this->field_0[2].field_0[3] = a12;
	
	this->field_0[3].field_0[0] = a13;
	this->field_0[3].field_0[1] = a14;
	this->field_0[3].field_0[2] = a15;
	this->field_0[3].field_0[3] = a16;
	
}

// @Ok
// @AlmostMatching: 48 positional mnemonic diffs out of 254 instructions,
// one instruction short of the original (253 vs 254). Frame size (0x80),
// pushes (ebx,esi,edi), all 16 dot products, the 16 temp slots, the
// inlined result stores and the whole row-copy loop body are mnemonic
// identical. The residue sits in the copy-loop setup only: the original
// walks the result rows in ecx and the dest rows in edx, and forms the
// diff as dest minus result, which needs an extra "mov esi,eax" to keep
// dest in eax for the return. Our build picks the mirrored roles (dest in
// ecx, result in edx, diff formed as result minus dest), which needs one
// instruction less, so the loop counter setup gets scheduled earlier into
// the FPU stream and the lines in between shift by one slot. 19 hypotheses
// tried in total (see the ps2m3d attempts log): 16-arg ctor with the
// expressions as args, ctor with named locals (goes out of line), direct
// field stores with void return, matrix4x4* return (fixed the prologue,
// frame size and push set), every combination of operator[] and .field_0
// indexing on both sides of the row copy, reference binding on either
// side, explicit walker pointers for source and dest, *dest = result,
// do/while loop shape, and declaration order swaps. The walker role choice
// never flipped.
//
// Not one of the file's original 7 stubs. Added because M3d_RenderSetup,
// M3d_Render and RenderSuperItem all call it (leaf-first dependency) to
// concatenate transforms; found via the maintainer's IDB (spideypc_names.txt
// calls it matrix4x4_ml), named gsub_476A00 here since tools/names.json only
// has it as sub_476A00.
// dest is written through a local first because dest may alias a or b (e.g.
// gsub_476A00(&m, &m, &n) to do "m = m * n" in place); writing straight into
// *dest while still reading it back for later cells would corrupt the
// result. Returns dest, like the original (eax holds dest at ret).
EXPORT matrix4x4* gsub_476A00(matrix4x4* dest, matrix4x4 const* a, matrix4x4 const* b)
{
	f32 m33 = a->field_0[3].field_0[2] * b->field_0[2].field_0[3] + a->field_0[3].field_0[1] * b->field_0[1].field_0[3] + a->field_0[3].field_0[0] * b->field_0[0].field_0[3] + b->field_0[3].field_0[3] * a->field_0[3].field_0[3];
	f32 m32 = b->field_0[3].field_0[2] * a->field_0[3].field_0[3] + a->field_0[3].field_0[1] * b->field_0[1].field_0[2] + a->field_0[3].field_0[2] * b->field_0[2].field_0[2] + a->field_0[3].field_0[0] * b->field_0[0].field_0[2];
	f32 m31 = a->field_0[3].field_0[2] * b->field_0[2].field_0[1] + a->field_0[3].field_0[0] * b->field_0[0].field_0[1] + b->field_0[3].field_0[1] * a->field_0[3].field_0[3] + a->field_0[3].field_0[1] * b->field_0[1].field_0[1];
	f32 m30 = a->field_0[3].field_0[1] * b->field_0[1].field_0[0] + b->field_0[3].field_0[0] * a->field_0[3].field_0[3] + a->field_0[3].field_0[2] * b->field_0[2].field_0[0] + a->field_0[3].field_0[0] * b->field_0[0].field_0[0];
	f32 m23 = a->field_0[2].field_0[2] * b->field_0[2].field_0[3] + a->field_0[2].field_0[3] * b->field_0[3].field_0[3] + a->field_0[2].field_0[0] * b->field_0[0].field_0[3] + a->field_0[2].field_0[1] * b->field_0[1].field_0[3];
	f32 m22 = a->field_0[2].field_0[0] * b->field_0[0].field_0[2] + a->field_0[2].field_0[3] * b->field_0[3].field_0[2] + a->field_0[2].field_0[2] * b->field_0[2].field_0[2] + a->field_0[2].field_0[1] * b->field_0[1].field_0[2];
	f32 m21 = a->field_0[2].field_0[3] * b->field_0[3].field_0[1] + a->field_0[2].field_0[2] * b->field_0[2].field_0[1] + b->field_0[1].field_0[1] * a->field_0[2].field_0[1] + a->field_0[2].field_0[0] * b->field_0[0].field_0[1];
	f32 m20 = b->field_0[1].field_0[0] * a->field_0[2].field_0[1] + a->field_0[2].field_0[0] * b->field_0[0].field_0[0] + a->field_0[2].field_0[3] * b->field_0[3].field_0[0] + a->field_0[2].field_0[2] * b->field_0[2].field_0[0];
	f32 m13 = a->field_0[1].field_0[2] * b->field_0[2].field_0[3] + a->field_0[1].field_0[3] * b->field_0[3].field_0[3] + a->field_0[1].field_0[0] * b->field_0[0].field_0[3] + a->field_0[1].field_0[1] * b->field_0[1].field_0[3];
	f32 m12 = a->field_0[1].field_0[0] * b->field_0[0].field_0[2] + b->field_0[3].field_0[2] * a->field_0[1].field_0[3] + a->field_0[1].field_0[2] * b->field_0[2].field_0[2] + a->field_0[1].field_0[1] * b->field_0[1].field_0[2];
	f32 m11 = a->field_0[1].field_0[3] * b->field_0[3].field_0[1] + a->field_0[1].field_0[2] * b->field_0[2].field_0[1] + b->field_0[1].field_0[1] * a->field_0[1].field_0[1] + a->field_0[1].field_0[0] * b->field_0[0].field_0[1];
	f32 m10 = b->field_0[1].field_0[0] * a->field_0[1].field_0[1] + a->field_0[1].field_0[0] * b->field_0[0].field_0[0] + a->field_0[1].field_0[3] * b->field_0[3].field_0[0] + a->field_0[1].field_0[2] * b->field_0[2].field_0[0];
	f32 m03 = a->field_0[0].field_0[2] * b->field_0[2].field_0[3] + a->field_0[0].field_0[3] * b->field_0[3].field_0[3] + a->field_0[0].field_0[0] * b->field_0[0].field_0[3] + b->field_0[1].field_0[3] * a->field_0[0].field_0[1];
	f32 m02 = b->field_0[0].field_0[2] * a->field_0[0].field_0[0] + b->field_0[3].field_0[2] * a->field_0[0].field_0[3] + b->field_0[2].field_0[2] * a->field_0[0].field_0[2] + b->field_0[1].field_0[2] * a->field_0[0].field_0[1];
	f32 m01 = a->field_0[0].field_0[3] * b->field_0[3].field_0[1] + a->field_0[0].field_0[2] * b->field_0[2].field_0[1] + b->field_0[1].field_0[1] * a->field_0[0].field_0[1] + a->field_0[0].field_0[0] * b->field_0[0].field_0[1];
	f32 m00 = b->field_0[1].field_0[0] * a->field_0[0].field_0[1] + a->field_0[0].field_0[0] * b->field_0[0].field_0[0] + a->field_0[0].field_0[3] * b->field_0[3].field_0[0] + a->field_0[0].field_0[2] * b->field_0[2].field_0[0];

	matrix4x4 result;
	result.field_0[0].field_0[0] = m00;
	result.field_0[0].field_0[1] = m01;
	result.field_0[0].field_0[2] = m02;
	result.field_0[0].field_0[3] = m03;
	result.field_0[1].field_0[0] = m10;
	result.field_0[1].field_0[1] = m11;
	result.field_0[1].field_0[2] = m12;
	result.field_0[1].field_0[3] = m13;
	result.field_0[2].field_0[0] = m20;
	result.field_0[2].field_0[1] = m21;
	result.field_0[2].field_0[2] = m22;
	result.field_0[2].field_0[3] = m23;
	result.field_0[3].field_0[0] = m30;
	result.field_0[3].field_0[1] = m31;
	result.field_0[3].field_0[2] = m32;
	result.field_0[3].field_0[3] = m33;

	for (i32 i = 0; i < 4; i++)
	{
		(*dest)[i] = result[i];
	}

	return dest;
}

/*
EXPORT void __ml(matrix4x4 const *,matrix4x4 const *);

EXPORT void matrix4x4::__vc(const(i32);
EXPORT matrix4x4::matrix4x4(f32,f32,f32,f32,f32,f32,f32,f32,f32,f32,f32,f32,f32,f32,f32,f32);

EXPORT void uWibble(STexWibVertInfo *);
EXPORT void vWibble(STexWibVertInfo *);

EXPORT void vector4d::__vc(const(i32);
*/


// @FIXME
#ifndef SPIDEY_STANDALONE
char gRenderBuf[4] = { 0, 0, 0, 0 };
#else
extern char gRenderBuf[4];
#endif

// @Ok
// @Matching
void M3d_BuildTransform(CSuper* pSuper)
{
	if ((pSuper->mExtraFlags & 1) == 0 )
	{
		M3dMaths_RotMatrixYXZ(
				reinterpret_cast<SVECTOR *>(&pSuper->mAngles),
				&pSuper->mTransform);
	}
	if (pSuper->mFlags & 0x200)
	{
		M3dMaths_ScaleMatrix(pSuper, &pSuper->mTransform);
	}

	pSuper->mTransform.t[0] = pSuper->mPos.vx >> 12;
	pSuper->mTransform.t[1] = pSuper->mPos.vy >> 12;
	pSuper->mTransform.t[2] = pSuper->mPos.vz >> 12;
}

// ---------------------------------------------------------------------
// M3dAsm_BoundingSpherePreprocessing (0x46FAD0) and its feeder leaves
// (0x46D7E0, 0x46D810, 0x46E250). Per-frame view-frustum visibility cull:
// walks the CItem list (mNextItem) and sets/clears CItem::mFlags bit
// 0x8000. Confirmed from the raw disasm (every "or ah,80h"/"mov [ebx+4],bp"
// site vs. the single "and eax,7FFFh"/"mov [ebx+4],ax" site at the very end
// of the function): bit 0x8000 SET means CULLED (do not render), CLEAR
// means visible. This refines M3d_Render's own comment below, which only
// knew the bit gated rendering, not which state meant what.
// ---------------------------------------------------------------------

// Two 9-i16 (3 packed xyz triples, NO padding -- confirmed by the raw
// disasm of the two loader functions below doing 9 sequential 2-byte
// stores with no gap between them, i.e. NOT 3 padded SVECTORs) scratch
// tables at fixed addresses feed the 6-plane frustum test. Filled once per
// frame by M3dAsm_LoadClipTableA/B with the camera-space plane normals
// M3d_RenderSetup's own gte_stsv loop writes at 0x628620/0x628648 (see
// M3d_RenderSetup, this file) -- M3dAsm_BoundingSpherePreprocessing itself
// only reads these tables, it does not call the loaders.
// This exact scratch memory is ALSO written/read by two completely
// unrelated routines elsewhere in the binary: ps2funcs.cpp's
// gLineToSphereDirMatrix (SVECTOR[3], WITH a pad word per entry -- a
// different, incompatible layout for the same address range, used by
// M3dColij_LineToSphere) and a generic collision-check helper
// (sub_4529C0, not part of this file, calls M3dAsm_LoadClipTableB
// directly). This is genuinely a shared per-frame scratch buffer reused by
// unrelated one-shot calculations, not a dedicated frustum-only table.
// Declared here as its own raw, unpadded i16[9] (matching the ACTUAL write
// pattern of the two loaders below), independent of ps2funcs.cpp's
// differently-typed declaration at the same address, per this repo's
// "duplicating static address globals across files is fine" convention.
static i16 * const gM3dClipScratchA = (i16*)0x00610B40;
static i16 * const gM3dClipScratchB = (i16*)0x00610B60;

// @Ok
// (0x46D7E0.) Copies 9 packed i16 (3 xyz triples) from pSrc into
// gM3dClipScratchA. Called from M3d_Render (still forwarded, 3 call sites
// per IDA xrefs) with pSrc pointing at the camera-space plane normals
// M3d_RenderSetup computes at 0x628620.
i16* M3dAsm_LoadClipTableA(void const* pSrc)
{
	i16 const* pSrc16 = (i16 const*)pSrc;
	for (i32 i = 0; i < 9; i++)
		gM3dClipScratchA[i] = pSrc16[i];
	return gM3dClipScratchA + 9;
}

// @Ok
// (0x46D810.) Same shape as M3dAsm_LoadClipTableA but writes
// gM3dClipScratchB. Per IDA xrefs, also called from two functions outside
// this file (a generic collision-check helper at 0x4529C0, and
// RenderSuperItem at 0x475290) with unrelated source pointers -- confirms
// this table really is shared scratch memory, not frustum-dedicated.
i16* M3dAsm_LoadClipTableB(void const* pSrc)
{
	i16 const* pSrc16 = (i16 const*)pSrc;
	for (i32 i = 0; i < 9; i++)
		gM3dClipScratchB[i] = pSrc16[i];
	return gM3dClipScratchB + 9;
}

static i32 * const gM3dCullSphereOffX = (i32*)0x00610BF0;
static i32 * const gM3dCullSphereOffY = (i32*)0x00610BF4;
static i32 * const gM3dCullSphereOffZ = (i32*)0x00610BF8;

// @Ok
// (0x46E250.) Trivial 3-int store: sets the culling-sphere-center offset
// M3dAsm_BoundingSpherePreprocessing reads (left-shifted by 12 there, i.e.
// this is a plain integer world-space offset, not already fixed-point
// scaled). Only known caller (not yet decompiled) is inside M3d_Render.
i32 M3dAsm_SetCullSphereOffset(i32 x, i32 y, i32 z)
{
	*gM3dCullSphereOffX = x;
	*gM3dCullSphereOffY = y;
	*gM3dCullSphereOffZ = z;
	return x;
}

// Same address as gM3dCameraPtr, declared textually later in this file (see
// M3d_RenderSetup) -- redeclared here for forward reference, matching this
// file's existing convention (e.g. gDCOverrideFlags/gDCFogEnabled above,
// which redeclare M3d_RenderSetup's/M3d_RenderBackground's statics for the
// same reason).
static i32 * const gM3dCameraPtrEarly = (i32*)0x00628640;

// dword_6B2454 in the raw disasm: read as dword_6B2454[17*region][model] to
// get a SModel*. 17 i32 units = 68 bytes = sizeof(SPSXRegion) (per the
// M3d_Render comment below, already cross-checked against NumParts's real
// offset there). Per CLAUDE.md's "Global boundaries" note, this address is
// most likely PSXRegion[0]'s ppModels field itself (the compiler folding
// the array base and the field offset together), not literally the region
// array's base address -- functionally irrelevant here, the indexed read
// is exactly equivalent either way.
static SModel *** const gM3dRegionModelArray = (SModel***)0x006B2454;

// (0x6150E8.) Gates whether the FIRST plane of the fine AABB-corner test
// (below) can early-out to "pass"; only appears in that one plane's
// condition in the raw disasm, planes 2-6 of the corner test always
// compute their real dot product. Meaning beyond that not investigated.
static i32 * const gM3dCornerTestBypass = (i32*)0x006150E8;

// @Ok
// (0x46FAD0, ~0x900 bytes.) Per-frame view-frustum visibility cull. Walks
// the CItem list starting at pList; for each item, does up to two 6-plane
// tests against the 6 clip-plane normals in gM3dClipScratchA/B (planes
// 0-2 carry a per-axis offset term, gM3dCullSphereOff{X,Y,Z} << 12; planes
// 3-5 do not -- reproduced exactly as read, not renamed to "near/far/etc"
// since that meaning was not independently confirmed):
//   1) A sphere test: item position (relative to the camera,
//      SCamera::Position) vs. a radius from the item's current SModel
//      (region+model indexed via gM3dRegionModelArray), SModel::Radius>>13,
//      further x8 and/or rescaled by other fields when specific mFlags
//      bits are set (see below). Any plane failing this sets mFlags bit
//      0x8000 (culled) and moves to the next item.
//   2) If the sphere test passes AND the item has no local rotation
//      (mAngles all zero) and mFlags bits 0x2/0x200 (sic, see below) are
//      clear, a tighter test against the model's own local bounding box
//      corners (SModel::Box; each axis packs {low:i16, high:i16} in one
//      i32 -- confirmed by the raw disasm treating Box.vx/vy/vz as two
//      independent sign-extended 16-bit halves via `(i16)x` and `x>>16`,
//      not a plain i32 value). Any plane failing this sets bit 0x8000;
//      passing all 6 clears it (visible).
// Two fast-exit paths force a cull without any of the above: mFlags bit
// 0x1 or 0x1000 set (unconditional), or the item-to-camera delta
// overflowing 15 bits on any axis (unconditional on X/Y, tolerated on Z
// only when mFlags bit 0x2000 is set).
//
// Session-wide override: functional decompilation, not byte-matching (see
// CLAUDE.md task header). Every branch and field offset below was cross-
// checked against the raw disassembly instruction-by-instruction (not just
// the Hex-Rays pseudocode -- which was NOT corrupted here, unlike the
// rotation/tint blocks in RenderSuperItem below). This sets real
// visibility state, so a wrong translation would make characters/objects
// invisible or wrongly visible; treated with matching care.
//
// One genuinely unresolved detail, kept faithful rather than guessed at:
// when mFlags bit 0x200 is set, the original reads pItem+0x18/+0x1A/+0x1C
// as three signed i16 and uses their max abs() value (if >=4096, or if all
// three are nonzero) to further rescale the radius. Those exact byte
// offsets land on CItem::mAngles.vz, CItem::mModel and
// CItem::mDummyFrame+mTintIndex (validated offsets, ob.cpp's
// validate_CItem) -- three unrelated fields read back-to-back, not one
// coherent 3-component field. Reproduced here as a raw 3xi16 read at that
// byte offset (not as named CItem fields, which would assert semantics
// that were not confirmed) rather than guessing what it "should" mean.
void M3dAsm_BoundingSpherePreprocessing(CItem* pList)
{
	if (pList == 0)
		return;

	SCamera* pCam = *(SCamera**)gM3dCameraPtrEarly;
	i32 camX = pCam->Position.vx;
	i32 camY = pCam->Position.vy;
	i32 camZ = pCam->Position.vz;

	// 6 plane normals, packed i16 xyz, straight from the scratch tables
	// (the original also copies these into on-stack temporaries first;
	// that copy has no observable effect since nothing else writes the
	// tables in between, so it is not reproduced here).
	i16 n0x = gM3dClipScratchA[0], n0y = gM3dClipScratchA[1], n0z = gM3dClipScratchA[2];
	i16 n1x = gM3dClipScratchA[3], n1y = gM3dClipScratchA[4], n1z = gM3dClipScratchA[5];
	i16 n2x = gM3dClipScratchA[6], n2y = gM3dClipScratchA[7], n2z = gM3dClipScratchA[8];
	i16 n3x = gM3dClipScratchB[0], n3y = gM3dClipScratchB[1], n3z = gM3dClipScratchB[2];
	i16 n4x = gM3dClipScratchB[3], n4y = gM3dClipScratchB[4], n4z = gM3dClipScratchB[5];
	i16 n5x = gM3dClipScratchB[6], n5y = gM3dClipScratchB[7], n5z = gM3dClipScratchB[8];

	// Per-plane "positive vertex" sign code (bit0 = X sign, bit1 = Y sign,
	// bit2 = Z sign of that plane's normal), used to pick which AABB
	// corner to test in the fine pass below.
	i32 sign0 = (n0x < 0 ? 1 : 0) | (n0y < 0 ? 2 : 0) | (n0z < 0 ? 4 : 0);
	i32 sign1 = (n1x < 0 ? 1 : 0) | (n1y < 0 ? 2 : 0) | (n1z < 0 ? 4 : 0);
	i32 sign2 = (n2x < 0 ? 1 : 0) | (n2y < 0 ? 2 : 0) | (n2z < 0 ? 4 : 0);
	i32 sign3 = (n3x < 0 ? 1 : 0) | (n3y < 0 ? 2 : 0) | (n3z < 0 ? 4 : 0);
	i32 sign4 = (n4x < 0 ? 1 : 0) | (n4y < 0 ? 2 : 0) | (n4z < 0 ? 4 : 0);
	i32 sign5 = (n5x < 0 ? 1 : 0) | (n5y < 0 ? 2 : 0) | (n5z < 0 ? 4 : 0);

	i32 offX = *gM3dCullSphereOffX << 12;
	i32 offY = *gM3dCullSphereOffY << 12;
	i32 offZ = *gM3dCullSphereOffZ << 12;

	for (CItem* pItem = pList; pItem != 0; pItem = pItem->mNextItem)
	{
		u16 flags = pItem->mFlags;

		if ((flags & 0x1001) != 0)
		{
			pItem->mFlags = flags | 0x8000;
			continue;
		}

		i32 dx = (pItem->mPos.vx >> 12) - camX;
		i32 dy = (pItem->mPos.vy >> 12) - camY;
		i32 dz = (pItem->mPos.vz >> 12) - camZ;

		if (my_abs(dx) > 0x7FFF || my_abs(dy) > 0x7FFF ||
			(my_abs(dz) > 0x7FFF && (flags & 0x2000) == 0))
		{
			pItem->mFlags = flags | 0x8000;
			continue;
		}

		i32 halfDx = dx >> 1;
		i32 halfDy = dy >> 1;
		i32 halfDz = dz >> 1;

		SModel* pModel = gM3dRegionModelArray[17 * pItem->mRegion][pItem->mModel];
		i32 radius = pModel->Radius >> 13;

		i32 curOffX = offX, curOffY = offY, curOffZ = offZ;

		if ((flags & 2) != 0)
		{
			// Original recomputes curOffX/Y/Z via an int -> float(*1.0f)
			// -> int roundtrip (the float constant used, flt_54F048, is
			// exactly 1.0f -- confirmed by reading its raw bytes,
			// 0x3F800000). That is a numeric no-op for values in this
			// range, so it is not reproduced; the x8 radius bump IS real
			// and is reproduced below.
			radius *= 8;
		}

		if ((flags & 0x200) != 0)
		{
			// See the function-header comment: these 3 bytes are not one
			// coherent field under the current CItem layout.
			i16 const* pRaw = (i16 const*)((u8*)pItem + 0x18);
			i32 a = my_abs(pRaw[0]);
			i32 b = my_abs(pRaw[1]);
			i32 c = my_abs(pRaw[2]);
			i32 maxAbs = (a >= b) ? ((a >= c) ? a : c) : ((b >= c) ? b : c);
			if (maxAbs >= 4096 || (a != 0 && b != 0 && c != 0))
				radius = (radius * maxAbs) >> 12;
		}

		i32 negRadius4096 = -4096 * radius;

		bool sphereVisible =
			(((flags & 0x2000) != 0) || (n0x * halfDx + n0y * halfDy + n0z * halfDz + curOffX >= negRadius4096)) &&
			(n1x * halfDx + n1y * halfDy + n1z * halfDz + curOffY >= negRadius4096) &&
			(n2x * halfDx + n2y * halfDy + n2z * halfDz + curOffZ >= negRadius4096) &&
			(((flags & 0x2000) != 0) || (n3x * halfDx + n3y * halfDy + n3z * halfDz >= negRadius4096)) &&
			(n4x * halfDx + n4y * halfDy + n4z * halfDz >= negRadius4096) &&
			(n5x * halfDx + n5y * halfDy + n5z * halfDz >= negRadius4096);

		if (!sphereVisible)
		{
			pItem->mFlags = flags | 0x8000;
			continue;
		}

		// No local rotation and no override flags set: trust the sphere
		// result, skip the fine AABB test.
		if (pItem->mAngles.vx != 0 || pItem->mAngles.vy != 0 || pItem->mAngles.vz != 0 ||
			(flags & 0x202) != 0)
		{
			pItem->mFlags = flags & 0x7FFF;
			continue;
		}

		i32 boxLoX = (i32)(i16)pModel->Box.vx, boxHiX = pModel->Box.vx >> 16;
		i32 boxLoY = (i32)(i16)pModel->Box.vy, boxHiY = pModel->Box.vy >> 16;
		i32 boxLoZ = (i32)(i16)pModel->Box.vz, boxHiZ = pModel->Box.vz >> 16;

		i32 cornerX[2] = { (dx + boxLoX) >> 1, (dx + boxHiX) >> 1 };
		i32 cornerY[2] = { (dy + boxLoY) >> 1, (dy + boxHiY) >> 1 };
		i32 cornerZ[2] = { (dz + boxLoZ) >> 1, (dz + boxHiZ) >> 1 };

		bool skipPlane0 = (*gM3dCornerTestBypass != 0) || ((flags & 0x2000) != 0);
		bool boxVisible =
			(skipPlane0 || (n0x * cornerX[sign0 & 1] + n0y * cornerY[(sign0 >> 1) & 1] + n0z * cornerZ[(sign0 >> 2) & 1] + curOffX >= 0)) &&
			(n1x * cornerX[sign1 & 1] + n1y * cornerY[(sign1 >> 1) & 1] + n1z * cornerZ[(sign1 >> 2) & 1] + curOffY >= 0) &&
			(n2x * cornerX[sign2 & 1] + n2y * cornerY[(sign2 >> 1) & 1] + n2z * cornerZ[(sign2 >> 2) & 1] + curOffZ >= 0) &&
			(((flags & 0x2000) != 0) || (n3x * cornerX[sign3 & 1] + n3y * cornerY[(sign3 >> 1) & 1] + n3z * cornerZ[(sign3 >> 2) & 1] >= 0)) &&
			(n4x * cornerX[sign4 & 1] + n4y * cornerY[(sign4 >> 1) & 1] + n4z * cornerZ[(sign4 >> 2) & 1] >= 0) &&
			(n5x * cornerX[sign5 & 1] + n5y * cornerY[(sign5 >> 1) & 1] + n5z * cornerZ[(sign5 >> 2) & 1] >= 0);

		pItem->mFlags = boxVisible ? (flags & 0x7FFF) : (flags | 0x8000);
	}
}

// ---------------------------------------------------------------------
// M3d_Render (0x004739A0) globals.
//
// The PS2 original of this function survives in thps2-stuff/m3d.mik
// (M3d_Render, line 1139) and names almost everything below. The
// maintainer's IDB (idbs/idb_globals.txt) independently confirms
// EnviroList (0x6B2EFC), EnvRegions (0x556C64), OTPushback (0x660F78) and
// M3d_FadeColour (0x652F38) at these exact addresses, which is what let me
// map the rest of the PS2 names onto PC addresses with confidence.
//
// The PSX "scratchpad" (0x1F800000) that the PS2 code writes through
// SCRATCH_* indices became, in the PC port, one 16-byte slot per index in
// a flat table starting at 0x00614F58. The slot addresses below are in
// that table and the names come straight from the PS2 SCRATCH_* names.
//
// Several of these addresses are also declared, under different names, by
// the DCModel_RenderModel / M3d_RenderBackground / M3d_RenderSetup static
// blocks further down this file. They are file-local `static ... const`
// pointers, so a second name for the same address is legal and is already
// this file's convention (see gDCOverrideFlags's own comment above); the
// duplicate is noted per line.
// ---------------------------------------------------------------------

// PS2: EnviroList / EnvRegions[2]. Both confirmed verbatim in idb_globals.txt.
static CItem ** const gM3dEnviroList  = (CItem**)0x006B2EFC;
static i32 * const gM3dEnvRegions     = (i32*)0x00556C64;

// PS2: FrustumMatrix1 / FrustumMatrix2 (the two 9-i16 clip tables the cull
// reads) and FrustumNormals[0].pad / FrustumNormals[1].pad (SVECTOR::pad,
// so base 0x65CEB8 + 6 and + 14). gM3dFrustumPadScale is PC only: a plain
// f32 global (1.0f in the image) the port multiplies the first pad by,
// almost certainly the widescreen/aspect fixup.
static void * const gM3dFrustumMatrix1   = (void*)0x00628648;
static void * const gM3dFrustumMatrix2   = (void*)0x00628620;
static i16 * const gM3dFrustumNormal0Pad = (i16*)0x0065CEBE;
static i16 * const gM3dFrustumNormal1Pad = (i16*)0x0065CEC6;
static f32 * const gM3dFrustumPadScale   = (f32*)0x00550070;

// The emulated scratchpad slots (PS2 SCRATCH_* names).
static i16 * const gM3dScratchDpqMin      = (i16*)0x00614F58;
static i16 * const gM3dScratchDpqShift    = (i16*)0x00614F68;
static i16 * const gM3dScratchOtPushback  = (i16*)0x00614F88; // == gDCEnvMapTableA
static i16 * const gM3dScratchOtPushback2 = (i16*)0x00614F98; // == gDCEnvMapTableB
static i16 * const gM3dScratchOtPushback3 = (i16*)0x00615018; // == gDCEnvMapTableC
static i16 * const gM3dScratchReflected   = (i16*)0x00614FA8;
static i32 * const gM3dScratchFadeColour  = (i32*)0x00615028;
static i32 * const gM3dScratchRefMapClut  = (i32*)0x00615038;
static i32 * const gM3dScratchRefMapTpage = (i32*)0x00615048;
static i32 * const gM3dScratchTint        = (i32*)0x006150B8;
// PS2: SCRATCH_GLOBALFACEFLAGS. In the PC port GlobalFaceFlags is not a
// local, it IS this global, written at every step of the flag build.
static i32 * const gM3dGlobalFaceFlags    = (i32*)0x00660F90; // == gDCOverrideFlags

// Sources copied into the scratchpad slots above (PS2 names).
static i32 * const gM3dDpqMin      = (i32*)0x0064E568; // == gM3dFadeDist (M3d_RenderSetup)
static i32 * const gM3dDpqShift    = (i32*)0x0061B5DC;
static i16 * const gM3dOtPushback  = (i16*)0x00660F78; // IDB: OTPushback, read as [0]/[1]/[2]
static i32 * const gM3dRefMapClut  = (i32*)0x0065CF70;
static i32 * const gM3dRefMapTpage = (i32*)0x0066072C;
static i32 * const gM3dFadeColourValue  = (i32*)0x00652F38; // IDB: M3d_FadeColour

// PC only. The ITEMFLAGS_RGB path gamma-corrects CItem::mRGB into a second
// set of colour globals for the float renderer, on top of the PS2's
// SCRATCH_TINT write. gM3dTintEnabled gates it downstream.
static i32 * const gM3dTintEnabled   = (i32*)0x00660F94; // == gDCTintFlag
static u32 * const gM3dTintPacked    = (u32*)0x0065F728; // mRGB with R and B swapped, then >>1 & 0x7F7F7F
static u32 * const gM3dTintB         = (u32*)0x00652F48;
static u32 * const gM3dTintG         = (u32*)0x00652F4C;
static u32 * const gM3dTintR         = (u32*)0x00652F50;
static f32 * const gM3dTintGamma     = (f32*)0x00550020;
static i32 * const gM3dTintOutB      = (i32*)0x00660F48; // == gDCTexAnimColorSrcA
static i32 * const gM3dTintOutG      = (i32*)0x00660F5C; // == gDCTexAnimColorSrcB
static i32 * const gM3dTintOutR      = (i32*)0x006191D4; // == gDCTexAnimColorSrcC
// Two bytes INSIDE gSaveGame (0x682858, front.cpp), at +5 and +7. Per
// CLAUDE.md's address-audit rule these are NOT given standalone names: the
// slots are part of the save block (the `lXaX_t` level code lives there),
// and the gamma the tint uses is picked from those two level-code
// characters. front.h now has G_SAVE_GAME for the whole block, but this file
// does not include front.h, so the raw address stays here with this note. The
// two bytes are G_SAVE_GAME.field_4[1] and field_4[3].
#define M3D_SAVEGAME_BYTE(i) (*reinterpret_cast<char*>(0x00682858 + (i)))

// PS2: TestTheWater, CurrentWaterLevel, WaterNormal.pad, pCurrentCamera,
// pCurrentViewport.
static i32 * const gM3dTestTheWater   = (i32*)0x0065CEAC;
static i32 * const gM3dWaterLevel     = (i32*)0x005FCDA8;
static i16 * const gM3dWaterNormalPad = (i16*)0x0062860E;

// PC only lighting state consumed by DCModel_RenderModel.
static i32 * const gM3dLightingEnabled    = (i32*)0x00660F9C; // == gDCTexAnimFlag
static i32 * const gM3dLightsAreDynamic   = (i32*)0x0065CEB0; // == gDCDebugLightFlag
static i32 * const gM3dDynamicLightSource = (i32*)0x00660FEC;
static u8  * const gM3dNoFogFlagEarly     = (u8*)0x00660FE1;  // == gDCNoFogFlag
static i32 * const gM3dPerItemRenderFlag  = (i32*)0x0065CF08; // cleared per item, role not confirmed
static i32 * const gM3dLightCount         = (i32*)0x0064E510; // == gDCLightCount
// The two per-light tables DCModel_RenderModel already reads through its
// GDC_LIGHT_VECTOR_TABLE / GDC_LIGHT_COLOR_TABLE macros (same addresses,
// 6456936 == 0x628668 and 6616488 == 0x64F5A8), 3 floats per light.
static f32 * const gM3dLightDirTable   = (f32*)0x00628668;
static f32 * const gM3dLightColorTable = (f32*)0x0064F5A8;
// SLight::BackColor as floats (PS2: M3dAsm_SetAmbient).
static f32 * const gM3dAmbientR = (f32*)0x00660F50; // == gDCTexAnimColorC
static f32 * const gM3dAmbientG = (f32*)0x00660F54; // == gDCTexAnimColorB
static f32 * const gM3dAmbientB = (f32*)0x00660F58; // == gDCTexAnimColorA
// Debug/override light: replaces light 0's colour and the ambient, points
// lights 1 and 2 the opposite way to light 0 and blacks their colour out.
static i32 * const gM3dLightOverride    = (i32*)0x00660FF4;
static f32 * const gM3dOverrideLightRgb = (f32*)0x006191C8;
static f32 * const gM3dOverrideAmbient  = (f32*)0x0065CF78;

// Default (identity) 4x4 float transform, 16 floats. Same address as
// gM3dIdentityOne, which M3d_RenderBackground declares further down.
static f32 * const gM3dDefaultTransform = (f32*)0x0064E518;
// Per-region base of the DCModelData array (36 bytes/entry, so
// base + 36*model). Same address as m3dinit.cpp's gDCRegionItems and
// M3d_RenderBackground's gM3dBackgroundModelData.
static u8 ** const gM3dRegionModelData = (u8**)0x005F6764;

// @Ok
// (0x004739A0, 3570 bytes.) Per-frame world renderer: culls the CItem list
// against the view frustum, then walks it and submits every visible item.
//
// Reconstructed against the PS2 original (thps2-stuff/m3d.mik line 1139),
// which matches the PC disassembly statement for statement apart from the
// PC-specific pieces called out below. Flag values recovered from that
// pairing: ITEMFLAGS_ISSUPER 0x2, ITEMFLAGS_INWATER 0x8, ITEMFLAGS_LIT
// 0x80, ITEMFLAGS_GOURAUD/ITEMFLAGS_RGB 0x400, ITEMFLAGS_TRN 0x800,
// ITEMFLAGS_SCALED 0x200, ITEMFLAGS_IGNOREDPQ 0x2000, ITEMFLAGS_CULL
// 0x8000; MODELFLAGS_LIT 0x4, MODELFLAGS_INVISIBLE 0x20; FACEFLAGS_TEXTURED
// 0x1, FACEFLAGS_RELATIVEUVS 0x2, FACEFLAGS_LIT 0x4, FACEFLAGS_GOURAUD 0x8,
// FACEFLAGS_TILED 0x20, FF_TRN 0x40, FF_TRL|FF_TRH 0x180. DEFZOOM is 191.
//
// Differences from the PS2 version, all confirmed in the disassembly:
//  - reflection mapping is gone. The PS2's `if (REFLECTIONMAPPED) ... else
//    ...` collapsed to just the else half (`&= ~FACEFLAGS_RELATIVEUVS;
//    |= (FACEFLAGS_TEXTURED|FACEFLAGS_TILED) << 16`), and the PS2's
//    M3dAsm_GetReflectionMapping call is not present at all, so the
//    lighting branch is entered on `MODELFLAGS_LIT || ITEMFLAGS_LIT` alone.
//  - the "Fast" path (RenderModelFast / RenderModelNonRotated /
//    RenderModel) is gone: the PC always builds a float matrix4x4 and hands
//    it to DCModel_RenderModel or DC_PSXModel_RenderModel.
//  - the ITEMFLAGS_RGB branch additionally gamma-corrects mRGB (three pow()
//    calls) into the float renderer's own colour globals, and the lighting
//    branch additionally converts the GTE light/colour matrices and the
//    ambient into floats for it.
//  - the negative-zMax LOD variant (PS2 `#if SK`) is not compiled in, and
//    the whole LOD step is skipped when gLowGraphics is set.
void M3d_Render(void* pList)
{
	CItem *pItem = static_cast<CItem*>(pList);

	if (pItem == 0)
		return;

	// bounding sphere preprocessing. PS2: gte_SetLightMatrix(&FrustumMatrix1),
	// gte_SetColorMatrix(&FrustumMatrix2), gte_ldbkdir(FrustumNormals[0].pad>>1,
	// FrustumNormals[1].pad>>1, 0). The GTE light/colour matrix registers are
	// reused as the cull's clip-plane scratch here, which is why this repo
	// named the two loaders M3dAsm_LoadClipTableA/B.
	M3dAsm_LoadClipTableA(gM3dFrustumMatrix1);
	M3dAsm_LoadClipTableB(gM3dFrustumMatrix2);
	M3dAsm_SetCullSphereOffset(
			static_cast<i32>(static_cast<f32>(*gM3dFrustumNormal0Pad >> 1) * *gM3dFrustumPadScale),
			*gM3dFrustumNormal1Pad >> 1,
			0);
	M3dAsm_BoundingSpherePreprocessing(pItem);
#ifdef SPIDEY_STANDALONE
	// Submit all loaded environment geometry by default. Keep near-plane
	// clipping and region validity checks in the polygon renderer.
	const char* fullMapOption = getenv("SPIDEY_FULL_MAP");
	bool fullMap = pItem == *gM3dEnviroList && (!fullMapOption || atoi(fullMapOption) != 0);
	if (fullMap || getenv("SPIDEY_NOSPHERECULL"))
		for (CItem* p = pItem; p; p = p->mNextItem)
			p->mFlags &= ~0x8000;
#endif

	// wibble the textures here so the bounding sphere cull has already run
	if (pItem == *gM3dEnviroList)
	{
		M3d_PreprocessWibblyTextures(gM3dEnvRegions[0]);
		M3d_PreprocessWibblyTextures(gM3dEnvRegions[1]);
	}

	*gM3dScratchDpqMin      = static_cast<i16>(*gM3dDpqMin);
	*gM3dScratchDpqShift    = static_cast<i16>(*gM3dDpqShift);
	*gM3dScratchOtPushback  = gM3dOtPushback[0];
	*gM3dScratchOtPushback2 = gM3dOtPushback[1];
	*gM3dScratchOtPushback3 = gM3dOtPushback[2];
	*gM3dScratchReflected   = 0;
	*gM3dScratchRefMapClut  = *gM3dRefMapClut;
	*gM3dScratchRefMapTpage = *gM3dRefMapTpage;
	*gM3dScratchFadeColour  = *gM3dFadeColourValue;

#ifdef SPIDEY_STANDALONE
	// SPIDEY_TRACE_CULL=1: once a second, how many EnviroList items the
	// bounding sphere pass culled vs. kept, plus the camera position
	{
		static i32 traceCull = -1;
		static i32 calls = 0;
		if (traceCull < 0)
			traceCull = getenv("SPIDEY_TRACE_CULL") ? 1 : 0;
		if (traceCull && pItem == *gM3dEnviroList && (++calls % 60) == 0)
		{
			i32 culled = 0, kept = 0, notUsable = 0;
			for (CItem* p = pItem; p; p = p->mNextItem)
			{
				if (p->mFlags & 0x8000) culled++;
				else if (G_PSXREGION[p->mRegion].Usable == 0) notUsable++;
				else kept++;
			}
			SCamera* pCam = *(SCamera**)gM3dCameraPtrEarly;
			fprintf(stderr, "CULL enviro culled=%d kept=%d notUsable=%d cam=(%d,%d,%d)\n", culled, kept, notUsable,
				pCam->Position.vx, pCam->Position.vy, pCam->Position.vz);
			static i32 scaleDumped = 0;
			if (!scaleDumped)
			{
				scaleDumped = 1;
				for (CItem* p = pItem; p; p = p->mNextItem)
					if ((p->mFlags & 0x200) || p->mAngles.vx || p->mAngles.vy || p->mAngles.vz)
						fprintf(stderr, "  ITEM %p model=%d region=%d pos=(%d,%d,%d) ang=(%d,%d,%d) flags=%#x scale=(%d,%d,%d)\n", (void*)p, p->mModel, p->mRegion,
							p->mPos.vx >> 12, p->mPos.vy >> 12, p->mPos.vz >> 12, p->mAngles.vx, p->mAngles.vy, p->mAngles.vz, p->mFlags, p->mScale.vx, p->mScale.vy, p->mScale.vz);
			}
		}
	}
#endif

	for ( ; pItem != 0; pItem = pItem->mNextItem)
	{
		if ((pItem->mFlags & 0x8000) != 0)
		{
			// culled. PS2: mpPolyStart = mpPolyEnd = NULL. Both are full
			// dwords in the original; ob.h currently models CSuper 0xF8 as
			// `u16 field_F8; PADDING(2);` and 0xFC as `i32 field_FC`, so a
			// plain field store would only clear half of the first one.
			if ((pItem->mFlags & 2) != 0)
			{
				*reinterpret_cast<i32*>(reinterpret_cast<u8*>(pItem) + 0xFC) = 0;
				*reinterpret_cast<i32*>(reinterpret_cast<u8*>(pItem) + 0xF8) = 0;
			}
			continue;
		}

		// don't draw if the PSX file is currently being spooled in
		if (G_PSXREGION[pItem->mRegion].Usable == 0)
			continue;

		// display modes for this item (lighting, tint, transparency)
		i32 faceFlags = static_cast<i32>(0xFFFF0000);
		*gM3dTintEnabled = 0;
		*gM3dGlobalFaceFlags = static_cast<i32>(0xFFFF0000);

		if ((pItem->mFlags & 0x80) != 0)
		{
			*gM3dGlobalFaceFlags = static_cast<i32>(0xFFFF0004);
			if ((pItem->mFlags & 0x400) != 0)
				faceFlags = static_cast<i32>(0xFFFF000C);
			else
				faceFlags = static_cast<i32>(0xFFF70004);
		}
		else if ((pItem->mFlags & 0x400) != 0)
		{
			*gM3dGlobalFaceFlags = static_cast<i32>(0xFFFB0008);

			u32 rgb = pItem->mRGB;

			*gM3dTintEnabled = 1;

			u32 tint = (rgb & 0xFFFF0000) << 3;
			tint = (tint | (rgb & 0xFF00)) << 3;
			tint = (tint | (rgb & 0xFF)) << 2;
			*gM3dScratchTint = static_cast<i32>(tint);

			// mRGB is packed b<<16 | g<<8 | r, so this swaps R and B round
			u32 packed = ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF);
			*gM3dTintPacked = packed;
			*gM3dTintB = packed & 0xFF;
			*gM3dTintR = (packed >> 16) & 0xFF;
			*gM3dTintG = (packed >> 8) & 0xFF;

			if (M3D_SAVEGAME_BYTE(5) == 'f' && M3D_SAVEGAME_BYTE(7) == '1')
				*gM3dTintGamma = 0.4f;
			else
				*gM3dTintGamma = 0.7f;

			f32 c = static_cast<f32>(*gM3dTintR) / 255.0f;
			c = static_cast<f32>(pow(c, *gM3dTintGamma));
			if (c > 1.0f)
				c = 1.0f;
			*gM3dTintOutR = static_cast<i32>(c * 255.0f);

			c = static_cast<f32>(*gM3dTintG) / 255.0f;
			c = static_cast<f32>(pow(c, *gM3dTintGamma));
			if (c > 1.0f)
				c = 1.0f;
			*gM3dTintOutG = static_cast<i32>(c * 255.0f);

			c = static_cast<f32>(*gM3dTintB) / 255.0f;
			c = static_cast<f32>(pow(c, *gM3dTintGamma));
			if (c > 1.0f)
				c = 1.0f;
			*gM3dTintOutB = static_cast<i32>(c * 255.0f);

			*gM3dTintPacked = (*gM3dTintPacked >> 1) & 0x7F7F7F;

			faceFlags = *gM3dGlobalFaceFlags;
		}

		// reflection mapping was dropped in the PC port, only the PS2's
		// "not reflection mapped" half of the test survives
		faceFlags = (faceFlags & ~2) | 0x210000;
		*gM3dGlobalFaceFlags = faceFlags;

		G_COLOUR_TABLE = G_PSXREGION[pItem->mRegion].pColourTable;

		if ((pItem->mFlags & 0x800) != 0)
			*gM3dGlobalFaceFlags = (faceFlags & ~0x180) | (static_cast<i32>(pItem->mTRN) & 0x180) | 0x40;

		*gM3dLightingEnabled = 0;
		*gM3dLightsAreDynamic = 0;
		*gM3dTestTheWater = (pItem->mFlags >> 3) & 1;
		*gM3dNoFogFlagEarly = 0;

		i32 *pCamera = reinterpret_cast<i32*>(*gM3dCameraPtrEarly);

		if ((pItem->mFlags & 2) != 0)
		{
			*gM3dWaterNormalPad = static_cast<i16>((*gM3dWaterLevel >> 12) - pCamera[2]);

			// the superitem carries its own OT pushback pair. Those two i16
			// live at CSuper 0x100/0x102, which ob.h currently models as the
			// single `i32 field_100` (PS2: mOTPushback1 / mOTPushback2).
			i16 savedPushback  = *gM3dScratchOtPushback;
			i16 savedPushback2 = *gM3dScratchOtPushback2;
			*gM3dScratchOtPushback  = *reinterpret_cast<i16*>(reinterpret_cast<u8*>(pItem) + 0x100);
			*gM3dScratchOtPushback2 = *reinterpret_cast<i16*>(reinterpret_cast<u8*>(pItem) + 0x102);

			RenderSuperItem(pItem, false);

			*gM3dScratchOtPushback  = savedPushback;
			*gM3dScratchOtPushback2 = savedPushback2;
			continue;
		}

		u16 modelIndex = pItem->mModel;
		SModel *pModel = G_PSXREGION[pItem->mRegion].ppModels[modelIndex];

		if ((pModel->Flags & 0x20) != 0)
			continue;

		// camera view transform, then transform the item's local origin
		gte_SetRotMatrix(reinterpret_cast<MATRIX*>(pCamera + 29));
		m3d_ZeroTransVector();

		VECTOR posnRelCam;
		posnRelCam.vx = (pItem->mPos.vx >> 12) - pCamera[1];
		posnRelCam.vy = (pItem->mPos.vy >> 12) - pCamera[2];
		posnRelCam.vz = (pItem->mPos.vz >> 12) - pCamera[3];
		gte_ldlv0(&posnRelCam);
		gte_rtv0();

		VECTOR position;
		gte_stlvnl(&position);

		u8 *pViewport = G_VIEW_CLIP_INFO;

		// level of detail
		if (G_LOWGRAPHICS == 0
#ifdef SPIDEY_STANDALONE
			&& !fullMap
#endif
			)
		{
			i32 zoom = *reinterpret_cast<u16*>(pViewport + 0x0E);
			while (position.vz > pModel->zMax * zoom / 191)
			{
				if (pModel->NextLOD == 0xFFFF)
					break;
				modelIndex = pModel->NextLOD;
				pModel = G_PSXREGION[pItem->mRegion].ppModels[modelIndex];
			}
		}

		DCModelData *pModelData = 0;
		if (*gM3dNoDcModelData == 0)
		{
			pModelData = reinterpret_cast<DCModelData*>(
					gM3dRegionModelData[pItem->mRegion] + 36 * modelIndex);
			print_if_false(pModelData != 0, "no dc model data");
			if (*gM3dNoDcModelData == 0 && *gM3dTintEnabled != 0)
				pModelData->mFlags |= 0x20;
		}

		// ran out of LODs and it is still too far away: drop the item
		if (G_LOWGRAPHICS == 0
#ifdef SPIDEY_STANDALONE
			&& !fullMap
#endif
			)
		{
			i32 zoom = *reinterpret_cast<u16*>(pViewport + 0x0E);
			if (position.vz > pModel->zMax * zoom / 191 && pModel->NextLOD == 0xFFFF)
				continue;
		}

		*gM3dPerItemRenderFlag = 0;

		if ((pModel->Flags & 4) != 0 || (pItem->mFlags & 0x80) != 0)
		{
			SLight *pLight = pItem->mpLight;

			// PS2: MATRIX WorldTransform, TempMatrix. The PC port dropped
			// the code that filled WorldTransform (the PS2 built it with
			// M3dMaths_RotMatrixYXZ / M3dMaths_ScaleMatrix and fed it to the
			// GTE; the PC builds a float matrix4x4 instead, further down),
			// but it kept both gte_MulMatrix0 calls that read it. So this
			// local really is passed in uninitialised in the original -- I
			// checked every write in the 890-instruction disassembly and
			// nothing touches [esp+158h] before the two calls. Kept exactly
			// as the original does it rather than "fixing" it.
			MATRIX worldTransform;
			MATRIX tempMatrix;

			if (pItem->mAngles.vx == 0 && pItem->mAngles.vy == 0 && pItem->mAngles.vz == 0)
			{
				M3dAsm_LoadClipTableA(pLight->LightMatrix);
			}
			else
			{
				MulMatrix0(reinterpret_cast<MATRIX*>(pLight), &worldTransform, &tempMatrix);
				M3dAsm_LoadClipTableA(&tempMatrix);
			}
			M3dAsm_LoadClipTableB(pLight->ColorMatrix);

			*gM3dLightingEnabled = 1;
			*gM3dLightsAreDynamic = (*gM3dDynamicLightSource != 0) ? 1 : 0;

			// PC only: the same product again, this time converted to floats
			// for the software/D3D renderer.
			MulMatrix0(reinterpret_cast<MATRIX*>(pLight), &worldTransform, &tempMatrix);

			for (i32 light = 0; light < 3; light++)
			{
				gM3dLightDirTable[3 * light + 0] = static_cast<f32>(tempMatrix.m[light][0]) / 4096.0f;
				gM3dLightDirTable[3 * light + 1] = static_cast<f32>(tempMatrix.m[light][1]) / 4096.0f;
				gM3dLightDirTable[3 * light + 2] = static_cast<f32>(tempMatrix.m[light][2]) / 4096.0f;

				// note the transpose: the colour matrix is read by column
				gM3dLightColorTable[3 * light + 0] = static_cast<f32>(pLight->ColorMatrix[0][light]) / 4096.0f;
				gM3dLightColorTable[3 * light + 1] = static_cast<f32>(pLight->ColorMatrix[1][light]) / 4096.0f;
				gM3dLightColorTable[3 * light + 2] = static_cast<f32>(pLight->ColorMatrix[2][light]) / 4096.0f;
			}

			*gM3dAmbientB = static_cast<f32>(pLight->BackColor[2]) / 4096.0f;
			*gM3dAmbientG = static_cast<f32>(pLight->BackColor[1]) / 4096.0f;
			*gM3dLightCount = 3;
			*gM3dAmbientR = static_cast<f32>(pLight->BackColor[0]) / 4096.0f;

			if (*gM3dLightOverride != 0)
			{
				f32 backX = -gM3dLightDirTable[0];
				f32 backY = -gM3dLightDirTable[1];
				f32 backZ = -gM3dLightDirTable[2];

				gM3dLightColorTable[0] = gM3dOverrideLightRgb[0];
				gM3dLightColorTable[1] = gM3dOverrideLightRgb[1];
				gM3dLightColorTable[2] = gM3dOverrideLightRgb[2];

				gM3dLightDirTable[3] = backX;
				gM3dLightDirTable[4] = backY;
				gM3dLightDirTable[5] = backZ;
				gM3dLightDirTable[6] = backX;
				gM3dLightDirTable[7] = backY;
				gM3dLightDirTable[8] = backZ;

				gM3dLightColorTable[3] = 0.0f;
				gM3dLightColorTable[4] = 0.0f;
				gM3dLightColorTable[5] = 0.0f;
				gM3dLightColorTable[6] = 0.0f;
				gM3dLightColorTable[7] = 0.0f;
				gM3dLightColorTable[8] = 0.0f;

				*gM3dAmbientR = gM3dOverrideAmbient[0];
				*gM3dAmbientG = gM3dOverrideAmbient[1];
				*gM3dAmbientB = gM3dOverrideAmbient[2];
			}
		}

		// ITEMFLAGS_IGNOREDPQ: no depth cueing and no distance clipping
		i16 savedYon = *reinterpret_cast<i16*>(pViewport + 0x0A);
		if ((pItem->mFlags & 0x2000) != 0)
		{
			*gM3dScratchReflected = 0;
			*gM3dScratchDpqMin = 0x7FFF;
			*reinterpret_cast<i16*>(pViewport + 0x0A) = 0x7FFF;
		}

		matrix4x4 transform;

		if (pItem->mAngles.vx == 0 && pItem->mAngles.vy == 0 && pItem->mAngles.vz == 0)
		{
			for (i32 row = 0; row < 4; row++)
			{
				transform.field_0[row].field_0[0] = gM3dDefaultTransform[4 * row + 0];
				transform.field_0[row].field_0[1] = gM3dDefaultTransform[4 * row + 1];
				transform.field_0[row].field_0[2] = gM3dDefaultTransform[4 * row + 2];
				transform.field_0[row].field_0[3] = gM3dDefaultTransform[4 * row + 3];
			}
		}
		else
		{
			f32 toRadians = 3.1415927f / 2048.0f;
			f32 angZ = static_cast<f32>(pItem->mAngles.vz) * toRadians;
			f32 angY = static_cast<f32>(pItem->mAngles.vy) * toRadians;
			f32 angX = static_cast<f32>(pItem->mAngles.vx) * toRadians;

			f32 sinX = static_cast<f32>(sin(angX));
			f32 cosX = static_cast<f32>(cos(angX));
			f32 sinY = static_cast<f32>(sin(angY));
			f32 cosY = static_cast<f32>(cos(angY));
			f32 sinZ = static_cast<f32>(sin(angZ));
			f32 cosZ = static_cast<f32>(cos(angZ));

			transform.field_0[0].field_0[0] = (sinZ * sinY) * sinX + (cosZ * cosY);
			transform.field_0[0].field_0[1] = sinZ * cosX;
			transform.field_0[0].field_0[2] = (sinZ * cosY) * sinX - (cosZ * sinY);
			transform.field_0[0].field_0[3] = 0.0f;
			transform.field_0[1].field_0[0] = (cosZ * sinY) * sinX - (sinZ * cosY);
			transform.field_0[1].field_0[1] = cosZ * cosX;
			transform.field_0[1].field_0[2] = (cosZ * cosY) * sinX + (sinZ * sinY);
			transform.field_0[1].field_0[3] = 0.0f;
			transform.field_0[2].field_0[0] = sinY * cosX;
			transform.field_0[2].field_0[1] = -sinX;
			transform.field_0[2].field_0[2] = cosY * cosX;
			transform.field_0[2].field_0[3] = 0.0f;
			transform.field_0[3].field_0[0] = 0.0f;
			transform.field_0[3].field_0[1] = 0.0f;
			transform.field_0[3].field_0[2] = 0.0f;
			transform.field_0[3].field_0[3] = 1.0f;
		}

		if ((pItem->mFlags & 0x200) != 0)
		{
			matrix4x4 scaleMatrix;
			scaleMatrix.field_0[0].field_0[0] = static_cast<f32>(pItem->mScale.vx) * 0.00024414062f;
			scaleMatrix.field_0[0].field_0[1] = 0.0f;
			scaleMatrix.field_0[0].field_0[2] = 0.0f;
			scaleMatrix.field_0[0].field_0[3] = 0.0f;
			scaleMatrix.field_0[1].field_0[0] = 0.0f;
			scaleMatrix.field_0[1].field_0[1] = static_cast<f32>(pItem->mScale.vy) * 0.00024414062f;
			scaleMatrix.field_0[1].field_0[2] = 0.0f;
			scaleMatrix.field_0[1].field_0[3] = 0.0f;
			scaleMatrix.field_0[2].field_0[0] = 0.0f;
			scaleMatrix.field_0[2].field_0[1] = 0.0f;
			scaleMatrix.field_0[2].field_0[2] = static_cast<f32>(pItem->mScale.vz) * 0.00024414062f;
			scaleMatrix.field_0[2].field_0[3] = 0.0f;
			scaleMatrix.field_0[3].field_0[0] = 0.0f;
			scaleMatrix.field_0[3].field_0[1] = 0.0f;
			scaleMatrix.field_0[3].field_0[2] = 0.0f;
			scaleMatrix.field_0[3].field_0[3] = 1.0f;

			// the original saves and restores element [3][3] around the
			// product. Both values are 1.0f so it is a no-op, kept for
			// faithfulness.
			f32 savedW = transform.field_0[3].field_0[3];
			matrix4x4 scaled;
			gsub_476A00(&scaled, &scaleMatrix, &transform);
			for (i32 scaledRow = 0; scaledRow < 4; scaledRow++)
			{
				transform.field_0[scaledRow].field_0[0] = scaled.field_0[scaledRow].field_0[0];
				transform.field_0[scaledRow].field_0[1] = scaled.field_0[scaledRow].field_0[1];
				transform.field_0[scaledRow].field_0[2] = scaled.field_0[scaledRow].field_0[2];
				transform.field_0[scaledRow].field_0[3] = scaled.field_0[scaledRow].field_0[3];
			}
			transform.field_0[3].field_0[3] = savedW;
		}

		transform.field_0[3].field_0[0] = static_cast<f32>(pItem->mPos.vx) * 0.00024414062f;
		transform.field_0[3].field_0[1] = static_cast<f32>(pItem->mPos.vy) * 0.00024414062f;
		transform.field_0[3].field_0[2] = static_cast<f32>(pItem->mPos.vz) * 0.00024414062f;

		if (*gM3dNoDcModelData != 0)
		{
			DC_PSXModel_RenderModel(pModel, &transform, 0, 0);
		}
		else
		{
			i32 modelDataFlags = pModelData->mFlags;
			if ((modelDataFlags & 0x100) == 0)
			{
				if ((modelDataFlags & 0x4000) != 0)
					DC_PSXModel_RenderModel(pModel, &transform, 0, pModelData);
				else
					// the original pushes a fourth argument here (0) that
					// DCModel_RenderModel never reads; ps2m3d.h declares the
					// three it does use.
					DCModel_RenderModel(pModel, pModelData, &transform);
			}
		}

		if ((pItem->mFlags & 0x2000) != 0)
		{
			*gM3dScratchDpqMin = static_cast<i16>(*gM3dDpqMin);
			*gM3dScratchReflected = 0;
			*reinterpret_cast<i16*>(pViewport + 0x0A) = savedYon;
		}
	}

	*gM3dTintEnabled = 0;
	*gM3dLightingEnabled = 0;
	*gM3dLightsAreDynamic = 0;
	*gM3dNoFogFlagEarly = 0;
}

typedef i32 (*gsub_509000_fn)(f32, f32, f32, i32, f32, f32, f32, i32, f32);

// @Bogus
// @FIXME forward to original: gsub_509000 (0x509000): draws a screen-space "line" as a colored quad
// billboard between two projected 3D points (a1,a2,a3)-(a5,a6,a7), width a9,
// color a8 (0xAARRGGBB). Traced this session for DCModel_RenderModel's
// debug-light-vector block: builds 4 tagKMVERTEX3-shaped corner records via
// sub_509740 (perp-offset helper, unnamed) and submits them through
// gsub_509400 (already @Ok in PCGfx.cpp) + sub_5071B0 (unnamed, looks like
// submitPoly-with-count). Both of those inner helpers are still
// undecompiled, so this is forwarded to the original instead of
// reimplemented (matches the ClearRegion/@FIXME forward precedent) -- it is
// only reached when the gDCDebugLightFlag global (0x65CEB0) is set, which
// looks like a debug/diagnostic toggle, not part of normal gameplay
// rendering.
EXPORT i32 gsub_509000(f32 x0, f32 y0, f32 z0, i32 a4, f32 x1, f32 y1, f32 z1, u32 color, f32 width)
{
#ifdef SPIDEY_STANDALONE
	// @TODO Phase 2: debug light line (only with gDCDebugLightFlag set)
	(void)x0; (void)y0; (void)z0; (void)a4; (void)x1; (void)y1; (void)z1; (void)color; (void)width;
	return 0;
#else
	gsub_509000_fn f = (gsub_509000_fn)0x00509000;
	return f(x0, y0, z0, a4, x1, y1, z1, color, width);
#endif
}

// New file-scope statics for DCModel_RenderModel / DC_PSXModel_RenderModel.
// Best-effort tentative names under the session-wide "functional
// decompilation, not byte-matching" bar -- not run through the full
// nearest-neighbor address audit CLAUDE.md normally requires (there are ~25
// of them from one function); flagged here so a future pass can do that
// properly. None of these collided with tools/names.json or
// idb_globals.txt except where noted.

// Per-call profiling counters, accumulated every DCModel_RenderModel call.
static volatile i32 * const gDCStatVertsOrig   = (i32*)0x00660FB0; // += SModel::NumVertices (via DCModelData::mNumVertices)
static volatile i32 * const gDCStatVertsWelded = (i32*)0x00660FB4; // += DCModelData::mVertexCount
static volatile i32 * const gDCStatFaces       = (i32*)0x00660FD0; // += DCModelData::mNumFaces
static volatile i32 * const gDCStatCalls       = (i32*)0x00660FD8; // ++ per call

// Same address M3d_RenderSetup already uses as a function-local
// gM3dFinalProjMatrix (matrix4x4*) -- the frame's camera+projection matrix,
// composed once per frame. DCModel_RenderModel multiplies the object's own
// transform (pTransform) by this to get the full object-to-screen matrix.
static matrix4x4 * const gDCFinalProjMatrix = (matrix4x4*)0x0056E6F8;
// Same address as algebra.cpp's file-local gGfxMatrix / bit.cpp's
// gFrameProjMatrix: the ACTIVE 16-float transform Algebra_Transform4 reads.
// DCModel_RenderModel writes the composed object-to-screen matrix here
// before doing any per-vertex projection.
static f32 * const gDCGfxMatrix = (f32*)0x0056E668;

// Shared screen-space vertex scratch pool (tagKMVERTEX3 records, PCGfx.h).
// DCModel_RenderModel and DC_PSXModel_RenderModel both index into it,
// offset by the model's own mVertexCount (reason for the offset is not
// understood, transliterated as-is from the disassembly).
static tagKMVERTEX3 * const gDCVertexPool = (tagKMVERTEX3*)0x0062E510;

// 16 bytes/entry, reverse-indexed (999-idx) precomputed screen positions,
// used when a DCVert's mFlags bit 0x2 is set (the "stitched" index packed
// into mFlags bits 16-23 by DCModel_CreateFromSModel, see dcmodel.h) --
// shares an exact screen position across welded copies of one source
// vertex so UV-seam splits don't crack at a shared edge.
// 0x62861C is a POINTER variable (0x476D00 reads `*(float*)(16*idx + dword_62861C)`),
// set by RenderSuperItem to 0x654FB8: the attach-point area that gDCAttachPointCursor
// fills downward from 0x658E28 (= 0x654FB8 + 16*999), so stitch index k reads attach
// point k. Reading 0x62861C as the table itself gave every stitched vertex zero /
// NaN screen coordinates (spike triangles on the menu Spidey model, 2026-09-03).
static u8 ** const gDCStitchPositionTable = (u8**)0x0062861C;

// Bump-decrementing (16 bytes/record) output cursor. Written whenever a
// DCVert's mFlags bit 0x1 is set, right after that vertex's screen position
// is computed. Tentative: "attachment point" list (a marked vertex whose
// resolved screen position gets recorded for some other system to read,
// e.g. an effect/weapon attach point), not cross-checked against a reader.
// 0x64F5CC holds the CURSOR (a pointer into the attach point stack, reset to
// its top 0x658E28 by RenderSuperItem below), it is not the buffer itself.
// The earlier "u8* cursor = (u8*)0x64F5CC" walked down from the pointer
// variable and overwrote everything below it (gDCLightCount at 0x64E510
// included, found by the standalone build).
static u8 ** const gDCAttachPointCursor = (u8**)0x0064F5CC;

// gFloatSuperRelated is already a repo-wide global (ob.h/ob.cpp, 0x54FFE0),
// used directly below instead of a new tentative pointer.
static volatile i32 * const gDCUseSuperScale  = (i32*)0x00660F74; // flag: use gFloatSuperRelated for the vertex scale
static f32 * const gDCFixedScaleConst         = (f32*)0x00550074; // raw float constant, alternate vertex scale
static volatile i32 * const gDCUseFixedScale  = (i32*)0x0060CF90; // flag: use gDCFixedScaleConst for the vertex scale

// Same addresses as the file-scope statics M3d_RenderBackground/
// M3d_RenderSetup declare further down this file (gM3dBackgroundDword,
// gM3dBackgroundFlagThree, gM3dFogFlag, gM3dProjNear, gM3dProjFar,
// gM3dProjScale) -- redeclared here (own file-local copies, same repo
// convention already used throughout ps2m3d.cpp) since those are declared
// textually AFTER this function.
static volatile i32 * const gDCOverrideFlags     = (i32*)0x00660F90; // == gM3dBackgroundDword
static volatile u8 *  const gDCHiResScaleFlag     = (u8*)0x00660FE2; // == gM3dBackgroundFlagThree
static volatile i32 * const gDCFogEnabled        = (i32*)0x0054D384; // == gM3dFogFlag
static volatile f32 * const gDCProjNear          = (f32*)0x00550078; // == gM3dProjNear
static volatile f32 * const gDCProjFar           = (f32*)0x0055007C; // == gM3dProjFar
static volatile f32 * const gDCProjScale         = (f32*)0x00550080; // == gM3dProjScale

static volatile i32 * const gDCTexAnimFlag    = (i32*)0x00660F9C; // gates the animated-texture-color block
static volatile i32 * const gDCTintFlag       = (i32*)0x00660F94; // gates a tint-recompute sub-step inside it
static volatile u8 * const  gDCNoFogFlag      = (u8*)0x00660FE1;  // byte, "skip the far-plane fog scan, always dither" flag

static volatile i32 * const gDCLightCount     = (i32*)0x0064E510; // count, shared by the tint block and the debug-light block
// 3 floats/entry RGB light colour table, gDCLightCount entries: the same
// table GDC_LIGHT_COLOR_TABLE_X/Y/Z index (0x64F5A8). 0x47762F loads
// 0x64F5B0 (entry 0's third float) and writes [eax-14h]/[eax-10h]/[eax-0Ch],
// so the table starts 8 bytes lower; with 0x64F5B0 as the base, entry 2
// landed on the attach point cursor at 0x64F5CC.
static f32 * const gDCLightTintTable          = (f32*)0x0064F5A8;
// Two big fixed-offset tables read by the per-light dot-product loop
// (per-light "direction"/normal-space vector and per-light "color", 3
// floats/12 bytes per entry each). The huge literal byte offsets in the
// disassembly (6456936, 6616488/92/96) are MSVC folding small-index array
// reads into neighbouring globals' base addresses (see CLAUDE.md's "Global
// boundaries" note) -- not independently resolved to a clean base address
// this session, kept as raw offsets.
#define GDC_LIGHT_VECTOR_TABLE_X(i) (*(f32*)(6456936 + 12 * (i)))
#define GDC_LIGHT_VECTOR_TABLE_Y(i) (*(f32*)(6456936 + 12 * (i) + 4))
#define GDC_LIGHT_VECTOR_TABLE_Z(i) (*(f32*)(6456936 + 12 * (i) + 8))
#define GDC_LIGHT_COLOR_TABLE_X(i)  (*(f32*)(6616488 + 12 * (i)))
#define GDC_LIGHT_COLOR_TABLE_Y(i)  (*(f32*)(6616488 + 12 * (i) + 4))
#define GDC_LIGHT_COLOR_TABLE_Z(i)  (*(f32*)(6616488 + 12 * (i) + 8))
// Stitched RGB table, indexed directly by the high word of the vertex flags.
// The original reads all three components from 0x64F5D8 (0x4776A4..0x4776B7).
static i32 * const gDCStitchColorIndexTable = (i32*)0x0064F5D8;
// 0x65DFA8 holds the CURSOR (a pointer, 12 bytes/record, DCVert mFlags bit
// 0x1), exactly like gDCAttachPointCursor above: the original reads
// [65DFA8], stores through it and writes the bumped pointer back
// (0x477770..0x477793), and RenderSuperItem re-points it at the stitch
// colour table 0x64F5D8 for every superitem (0x474C86, gM3dLitColourCursor
// below). An earlier revision declared this as `u8* cursor = 0x65DFA8`,
// i.e. the address itself as the buffer, never reset: it bump-walked 12
// bytes per attach-point vertex per frame from 0x65DFA8 up through the
// data segment and zeroed MechList (0x6A9038) about 20 s into level 1
// (Display then crashed in RenderLookaroundReticle, found 2026-09-03).
static u8 ** const gDCLitColorOutCursor = (u8**)0x0065DFA8;

static volatile i32 * const gDCTexAnimColorA    = (i32*)0x00660F58;
static volatile i32 * const gDCTexAnimColorB    = (i32*)0x00660F54;
static volatile i32 * const gDCTexAnimColorC    = (i32*)0x00660F50;
static volatile u32 * const gDCTexAnimColorSrcA = (u32*)0x00660F48;
static volatile u32 * const gDCTexAnimColorSrcB = (u32*)0x00660F5C;
static volatile u32 * const gDCTexAnimColorSrcC = (u32*)0x006191D4;

static volatile i32 * const gDCDebugLightFlag = (i32*)0x0065CEB0; // gates the sub_509000 debug-line block

static i32 * const gDCLastNoLightFlag  = (i32*)0x00AC08DC;
// PS2: SubObjectMasked (== gM3dSubObjectMasked further down). When it is
// set the whole fog remap and batch submission block is skipped (0x477F3D).
static i32 * const gDCSubObjectMasked  = (i32*)0x0065DFAC; // last "no-light" flag passed to PCGfx_UseTexture, batching cache
static i32 * const gDCFaceSortKey      = (i32*)0x00AC08E0; // PCGfx.cpp already references this address generically as an OT/sort key
static i32 * const gDCFaceSortKeyExtra = (i32*)0x00AC08E4;
static volatile i32 * const gDCForceNoTexFlag  = (i32*)0x00660FFC;
static volatile i32 * const gDCForceNoLightFlag = (i32*)0x00660FF8;
static volatile i32 * const gDCBatchCallCount   = (i32*)0x00660FD4;

static u16 * const gDCFaceTexIndexOut = (u16*)0x006191DC; // per-face resolved texture id, filled by the OT-build loop
static u8 *  const gDCFaceNoLightOut  = (u8*)0x0065F72C;  // per-face "no-light" flag, filled by the OT-build loop
static u16 * const gDCFaceSortBiasOut = (u16*)0x00652F54; // per-face env-map/sort adjustment, filled by the OT-build loop
// Per-face triangle index list the OT-build loop fills and the batch
// submission hands to PCGfx_ClipSendIndexedVertList: one 12 byte record of six
// u16 vertex slots per accepted face, [s0,s1,s2, s2,s1,s3]. 0x476D00 walks it
// with a pointer that starts at 0x62C512 (the record's second word) and reads
// it back as 12*face + 0x62C510 at the call site. (Was a nonexistent fixed
// "template" at 0x62C850, in BSS, so every triangle used vertex 0 and the
// standalone build rendered no level geometry at all, 2026-09-03.)
static u16 * const gDCTriIndexBuffer = (u16*)0x0062C510;

static i32 * const gDCForceTexIndex = (i32*)0x00660FE4; // per-model forced texture-index override (0 = use the face's own)
static i32 * const gDCEnvMapTableA  = (i32*)0x00614F88;
static i32 * const gDCEnvMapTableB  = (i32*)0x00614F98;
static u16 * const gDCEnvMapTableC  = (u16*)0x00615018;

// @Ok
// (0x00476D00, ~5.2KB.) The real DC-format model renderer: reads every
// field DCModel_CreateFromSModel wrote (dcmodel.h) and projects/lights/
// batches the model's faces for PCGfx submission. Session-wide override:
// functional decompilation, not byte-matching (see CLAUDE.md task header).
//
// Cross-validation against dcmodel.h's DCModelData/DCVert/DCFace field
// docs, found while implementing this (see the ps2m3d.attempts.md log and
// the task report for the full writeup):
//  - CONFIRMS DCModelData::mFlags 0x100 = skip render entirely.
//  - REFINES: mFlags 0x10 is ALSO tested here (`(mFlags&0x10)||(mFlags&0x100)`
//    both bail out before any work), not just 0x100 as the header's "bits
//    tested" list implied.
//  - CONFIRMS DCModelData::mVertexCount (a2[4]) is the vertex-loop bound.
//  - CONFIRMS DCModelData::mSortBiasNormal / mSortBiasLowGraphics (a2[7],
//    a2[8]) are selected by gLowGraphics and added into the per-face OT/
//    sort key (gDCFaceSortKey = bias + gDCFaceSortBiasOut[i]).
//  - CONFIRMS/EXPLAINS DCVert's documented "defect": when the source
//    per-vertex flag bit 0x10 is set, DCVert::x/DCVert::y are NOT floats,
//    they are raw i32 INDICES into the screen-space vertex scratch pool
//    (gDCVertexPool). This is not a bug -- it is a billboard/ribbon vertex
//    whose screen position is generated from two already-projected "anchor"
//    vertices (a perpendicular offset scaled by DCVert::z, which IS a real
//    float) instead of having its own 3D position at all.
//  - REFINES DCVert::mFlags bit 0x2 ("stitched index"): confirms it means
//    "share this exact screen position with another welded copy", read
//    from gDCStitchPositionTable via the byte-2 index packed by
//    DCModel_CreateFromSModel.
//  - New finding: DCVert::mFlags bit 0x1 marks an "attachment point"
//    vertex -- its resolved screen position also gets appended to a
//    separate bump-allocated list (gDCAttachPointCursor). Not documented
//    in dcmodel.h before this session.
//  - New finding: DCFace::field_34[0] bit 0x2 gates the OT-build loop's
//    per-face skip test, and field_34[0] bit 0x1 (with field_34[2..3] as a
//    u16) is a per-face RUNTIME texture-index override read back by the
//    renderer. Refines the header's "low confidence, runtime scratch"
//    guess for field_34 into something concrete.
void DCModel_RenderModel(SModel const *pModel, DCModelData *pData, matrix4x4 const *pTransform)
{
	if ((pModel->Flags & 0x20) != 0)
		return;
	if (pModel->NumFaces == 0)
		return;
	if (pData == 0)
		return;

	i32 flags = pData->mFlags;
	u8 flagsByte1 = (u8)((u32)flags >> 8);
	if ((flags & 0x10) != 0 || (flags & 0x100) != 0)
		return;

	i32 numVertsOrig = pModel->NumVertices;
	DCFace *pFaces = (DCFace*)pData->pFaces;
	i32 numVertsOrigClamped = (u16)numVertsOrig;
	DCVert *pVerts = pData->pVertices;

	gDCStatVertsOrig[0]   += pData->mNumVertices;
	i32 vertexCount = pData->mVertexCount;
	gDCStatVertsWelded[0] += pData->mVertexCount;
	gDCStatFaces[0]       += pData->mNumFaces;
	gDCStatCalls[0]++;

	// objToScreen = pTransform * gDCFinalProjMatrix (row-vector*matrix, same
	// convention as gsub_476A00, confirmed algebraically against the 16 dot
	// products in the original disassembly -- see the ps2m3d attempts log).
	matrix4x4 objToScreen;
	gsub_476A00(&objToScreen, pTransform, gDCFinalProjMatrix);
	memcpy(gDCGfxMatrix, &objToScreen, sizeof(matrix4x4));

	// Per-vertex projected-color/dither scale. Defaults to 1.0f in low
	// graphics mode; in normal graphics mode it is usually 4.0 (or 1.0 if
	// gM3dBackgroundFlagThree is clear), bumped 1.025x for a "transparent
	// face" model (mFlags bit 0x800), or overridden entirely by a Super's
	// float or a fixed constant when the matching global flags are set.
	f32 vertexScale = 1.0f;
	if (!G_LOWGRAPHICS)
	{
		vertexScale = 4.0f;
		if (*gDCHiResScaleFlag == 0)
			vertexScale = 1.0f;
		if ((flagsByte1 & 8) != 0)
			vertexScale = vertexScale * 1.025f;
		if (*gDCUseSuperScale != 0)
			vertexScale = gFloatSuperRelated;
		if (*gDCUseFixedScale != 0)
			vertexScale = *gDCFixedScaleConst;
	}

	i32 remainingVerts = numVertsOrig - 1;
	i32 remainingVertsSaved = numVertsOrig - 1;
	tagKMVERTEX3 *pScratchBase = gDCVertexPool + vertexCount;

	if (numVertsOrig - 1 >= 0)
	{
		f32 *pWrite = &pScratchBase->field_8;
		bool sawBillboardAnchor = false;

		for (i32 i = 0; i < numVertsOrig; i++)
		{
			DCVert *pv = &pVerts[i];
			i32 vf = pv->mFlags;

			if ((vf & 2) != 0)
			{
				i32 stitchIdx = 999 - (u8)((u32)vf >> 16);
				f32 *src = (f32*)(*gDCStitchPositionTable + 16 * stitchIdx);
				pWrite[-1] = src[0];
				pWrite[0]  = src[1];
				pWrite[1]  = src[2];
			}
			else if (!sawBillboardAnchor && (((u8)vf) & 0x10) == 0)
			{
				f32 in4[4] = { pv->x, pv->y, pv->z, 1.0f };
				f32 out4[4];
				Algebra_Transform4(out4, in4);

				f32 w = out4[3];
				f32 invW = (fabsf(w) > 0.0000000099999999f) ? (1.0f / w) : -1.0e12f;
				f32 sx = out4[0] * invW;
				f32 sy = out4[1] * invW;
				f32 sz = invW * vertexScale;

				pWrite[-1] = sx;
				pWrite[0]  = sy;
				pWrite[1]  = sz;

				if ((pv->mFlags & 1) != 0)
				{
					u8* pAttach = *gDCAttachPointCursor;
					*(f32*)(pAttach + 0) = sx;
					*(f32*)(pAttach + 4) = sy;
					*(f32*)(pAttach + 8) = sz;
					*gDCAttachPointCursor = pAttach - 16;
				}
			}
			else
			{
				// Billboard/ribbon vertex: x/y hold raw i32 indices into
				// the scratch pool (the documented DCVert "defect"); z is
				// a real float half-width. See the function-level comment.
				sawBillboardAnchor = true;

				i32 idx1 = *(i32*)&pv->x;
				i32 idx2 = *(i32*)&pv->y;
				f32 halfWidth = pv->z;

				tagKMVERTEX3 *anchor1 = pScratchBase + idx1;
				tagKMVERTEX3 *anchor2 = pScratchBase + idx2;

				f32 ax = anchor1->field_4, ay = anchor1->field_8, az = anchor1->field_C;
				f32 bx = anchor2->field_4, by = anchor2->field_8;

				f32 dx = bx - ax;
				f32 dy = ay - by;
				f32 lenSq = dy * dy + dx * dx;
				if (lenSq != 0.0f)
				{
					f32 invLen = 1.0f / sqrtf(lenSq);
					dx = invLen * dx;
					dy = invLen * dy;
				}

				f32 py = dy * halfWidth;
				f32 px = dx * halfWidth;
				f32 depthScale = az * 133.33333f;
				f32 offX = depthScale * py * 2.556f;
				f32 offY = depthScale * px;

				pWrite[-1] = offX + ax;
				pWrite[0]  = offY + ay;
				pWrite[1]  = az * vertexScale;
				// nullsub_1 (both branches) omitted, confirmed empty no-op.
			}

			pWrite += 8;
		}

		remainingVerts = remainingVertsSaved;
	}

	// Far-plane fog-dither scan: is at least one welded vertex within
	// (0, gM3dProjFar)? Skipped (treated as "yes") when gDCNoFogFlag is set.
	bool inFogRange = false;
	if (*gDCNoFogFlag == 0)
	{
		if (*gDCFogEnabled != 0 && !G_LOWGRAPHICS && remainingVerts >= 0)
		{
			f32 *pDepth = &pScratchBase->field_C;
			while (*pDepth >= *gDCProjFar || *pDepth <= 0.0f)
			{
				pDepth += 8;
				if (--remainingVerts < 0)
					goto fogScanDone;
			}
			inFogRange = true;
		}
	}
	else
	{
		inFogRange = true;
	}
fogScanDone:

	// Per-vertex dynamic-light tint block (only when gDCTexAnimFlag is set).
	// NEW FINDING, high confidence: this is the first confirmed READ of
	// DCModelData::pNormals by DCModel_RenderModel (dcmodel.h flagged that
	// field as unconfirmed at render time). It is walked in lock-step with
	// pVertices (same loop index, same iteration count), i.e. pNormals has
	// (at least) mVertexCount entries, one welded-vertex normal each --
	// not a separately-indexed per-source-normal array. Output is a
	// dedicated lit-color scratch buffer at 0x65DFB8 (gDCLitColorScratch,
	// 3 floats/vertex, walked in the SAME lock-step, NOT the tagKMVERTEX3
	// pool -- an earlier draft of this function wrongly conflated the two).
	if (*gDCTexAnimFlag != 0)
	{
		if ((u16)(pModel->NumVertices + pModel->NumFaces) <= pModel->NumNormals)
		{
			i32 lightCount = *gDCLightCount;
			DCNormal *pNormals = pData->pNormals;

			if (*gDCTintFlag != 0)
			{
				f32 scaleA = (f32)(u32)*gDCTexAnimColorSrcA / 255.0f;
				f32 scaleB = (f32)(u32)*gDCTexAnimColorSrcB / 255.0f;
				f32 scaleC = (f32)(u32)*gDCTexAnimColorSrcC / 255.0f;

				*(f32*)gDCTexAnimColorC = scaleA * *(f32*)gDCTexAnimColorC;
				*(f32*)gDCTexAnimColorB = scaleB * *(f32*)gDCTexAnimColorB;
				*(f32*)gDCTexAnimColorA = scaleC * *(f32*)gDCTexAnimColorA;

				if (lightCount > 0)
				{
					f32 *pLightTint = gDCLightTintTable;
					for (i32 li = 0; li < lightCount; li++)
					{
						f32 r = scaleA * pLightTint[0];
						f32 g = scaleB * pLightTint[1];
						f32 b = scaleC * pLightTint[2];
						pLightTint[0] = r;
						pLightTint[1] = g;
						pLightTint[2] = b;
						pLightTint += 3;
					}
				}
			}

			if (remainingVertsSaved >= 0)
			{
				f32 *pLitColor = (f32*)0x0065DFB8; // gDCLitColorScratch, 3 floats/vertex
				i32 vertsLeft = remainingVertsSaved + 1;

				for (i32 i = 0; i < vertsLeft; i++)
				{
					DCVert *pv = &pVerts[i];
					DCNormal *pn = &pNormals[i];

					if ((pv->mFlags & 2) != 0)
					{
						// Stitched/welded vertex: share the lit color from
						// a table indexed by the high word of mFlags, confirming the
						// "share across welded copies" idea also applies
						// to lighting, not just screen position.
						i32 stitchIdx = (u16)((u32)pv->mFlags >> 16);
						((i32*)pLitColor)[-2] = gDCStitchColorIndexTable[3 * stitchIdx];
						pLitColor[-1] = *(f32*)(gDCStitchColorIndexTable + 3 * stitchIdx + 1);
						pLitColor[0]  = *(f32*)(gDCStitchColorIndexTable + 3 * stitchIdx + 2);
					}
					else
					{
						pLitColor[-2] = *(f32*)gDCTexAnimColorC;
						pLitColor[-1] = *(f32*)gDCTexAnimColorB;
						pLitColor[0]  = *(f32*)gDCTexAnimColorA;

						for (i32 li = 0; li < lightCount; li++)
						{
							f32 dot = GDC_LIGHT_VECTOR_TABLE_X(li) * pn->x
									+ GDC_LIGHT_VECTOR_TABLE_Z(li) * pn->z
									+ GDC_LIGHT_VECTOR_TABLE_Y(li) * pn->y;
							if (dot >= 0.0f)
							{
								pLitColor[-2] += dot * GDC_LIGHT_COLOR_TABLE_X(li);
								pLitColor[-1] += dot * GDC_LIGHT_COLOR_TABLE_Y(li);
								pLitColor[0]  += dot * GDC_LIGHT_COLOR_TABLE_Z(li);
							}
						}

						if ((pv->mFlags & 1) != 0)
						{
							// Same "attachment point" bit as the position
							// loop above: also records this vertex's lit
							// color into a companion bump-allocated list.
							u8 *pOut = *gDCLitColorOutCursor;
							*(f32*)pOut = pLitColor[-2];
							*(f32*)(pOut + 4) = pLitColor[-1];
							*(f32*)(pOut + 8) = pLitColor[0];
							*gDCLitColorOutCursor = pOut + 12;
						}
					}
					pLitColor += 3;
				}
			}
		}
		else
		{
			*gDCTexAnimFlag = 0;
		}
	}

	// Main per-face OT-build/texture-batching loop.
	// mSortBiasNormal (index 7) / mSortBiasLowGraphics (index 8), selected
	// with the ORIGINAL's raw (not boolified) `a2[dword_6B78F8 + 7]`
	// indexing -- reproduced as-is per CLAUDE.md (don't "fix" a source
	// idiom that happens to rely on the flag being exactly 0 or 1).
	i32 sortBias = ((i32*)pData)[7 + G_LOWGRAPHICS];
	i32 batchCount = 0;

	if (numVertsOrig > 0) // numFacesOrig (SModel::NumFaces) guards the whole block below
	{
		i32 facesLeft = pModel->NumFaces;
		u8 *pFaceCursor = (u8*)&pFaces->mVertSlot[0]; // "v46": pFaces + 12
		i32 overrideMask = *gDCOverrideFlags;
		u16 *pIdxOut = gDCTriIndexBuffer;

		i32 packedFaceFlags = 0;
		u32 alphaMask = 0xFF000000u;
		i32 alphaMode = 0; // 0/1/2, from mFlags bits 0x40/0x80

		for (;;)
		{
			u16 faceFlags = *(u16*)(pFaceCursor - 12); // DCFace::mFlags
			packedFaceFlags = (u16)overrideMask | (faceFlags & ((u16)(overrideMask >> 16)));
			alphaMask = 0xFF000000u;
			alphaMode = 0;

			// 0x476D00 / 0x478180: the face-flag byte is masked with BYTE2 of the
			// override (bits 16..23), not byte 1. With the usual 0xFFFF0000 override
			// byte 1 is zero, which skipped every face (found by the standalone build:
			// no level geometry ever reached the renderer).
			u8 lowCombined = (u8)overrideMask | (u8)(faceFlags & (u8)(overrideMask >> 16));
			u8 fieldByte0 = *(pFaceCursor + 40); // DCFace::field_34[0]
			if ((lowCombined & 0xC0) != 0 && (fieldByte0 & 2) == 0)
				break;

			pFaceCursor += 56;
			facesLeft--;
			if (facesLeft == 0)
				goto afterFaceLoop;
		}

		if ((packedFaceFlags & 0x40) != 0)
		{
			alphaMode = ((packedFaceFlags & 0x80) != 0) ? 2 : 1;
			alphaMask = ((packedFaceFlags & 0x80) != 0) ? 0x80000000u : 0xFF000000u;
		}
		if ((overrideMask & 0x40) != 0)
		{
			alphaMode = 2;
			alphaMask = 0xFF000000u;
		}

		for (;;)
		{
			// Resolve this face's 4 corner slots (DCFace::mVertSlot), lazily
			// filling any not-yet-touched welded slot (index >= NumVertices)
			// from the corresponding corner's already-resolved neighbour.
			u16 cornerSlots[4];
			u16 *pSrcSlots = (u16*)pFaceCursor; // DCFace::mVertSlot[0..3]

			for (i32 c = 0; c < 4; c++)
			{
				i16 rawSlot = (i16)pSrcSlots[c];
				cornerSlots[c] = pSrcSlots[c];
				if (rawSlot < 0)
				{
					cornerSlots[c] = (u16)(pSrcSlots[c] & 0x7FFF);
				}
				else
				{
					i32 slot = cornerSlots[c];
					if (slot >= numVertsOrigClamped)
					{
						tagKMVERTEX3 *pDst = pScratchBase + slot;
						tagKMVERTEX3 *pSrc = pScratchBase + (u8)pFaceCursor[c - 8]; // DCFace::mVertIndex[c]
						pDst->field_4 = pSrc->field_4;
						pDst->field_8 = pSrc->field_8;
						pDst->field_C = pSrc->field_C;
					}
				}
			}

			// This face's two triangles for PCGfx_ClipSendIndexedVertList:
			// the four resolved corner slots, then the second triangle
			// (s2, s1, s3) appended, exactly the word shuffle 0x476D00 does
			// through its 0x62C512 cursor.
			pIdxOut[0] = cornerSlots[0];
			pIdxOut[1] = cornerSlots[1];
			pIdxOut[2] = cornerSlots[2];
			pIdxOut[3] = cornerSlots[2];
			pIdxOut[4] = cornerSlots[1];
			pIdxOut[5] = cornerSlots[3];
			pIdxOut += 6;

			// Resolve this face's texture index.
			u16 texIndex;
			if ((packedFaceFlags & 1) != 0)
			{
				u16 override;
				if (*gDCForceTexIndex != 0)
					override = (u16)*gDCForceTexIndex;
				else if ((*(pFaceCursor + 40) & 1) != 0) // DCFace::field_34[0] bit 0
					override = *(u16*)(pFaceCursor + 42); // DCFace::field_34[2..3]
				else
					override = *(u16*)(pFaceCursor - 10); // DCFace::mTexIndex

				// print_if_false("Non ARB Texture", ...) omitted (debug-only assert).

				for (i32 c = 0; c < 4; c++)
				{
					u16 slot = (u16)(cornerSlots[c] & 0x7FFF);
					pScratchBase[slot].field_10 = *(f32*)(pFaceCursor + 24 + 4 * c - 16); // DCFace::mU[c]
					pScratchBase[slot].field_14 = *(f32*)(pFaceCursor + 24 + 4 * c);      // DCFace::mV[c]
				}
				texIndex = override;
			}
			else
			{
				texIndex = 1;
			}

			gDCFaceTexIndexOut[batchCount] = texIndex;
			gDCFaceNoLightOut[batchCount] = (u8)alphaMode;
			batchCount++;

			// Per-face sort-bias output: 0 by default, overridden by an
			// environment-map-mode lookup only in low-graphics mode
			// (matches gDCEnvMapTableA/B/C's case values against the
			// original switch exactly -- 0x2000->A, 0x4000->B, 0x6000->C).
			u16 sortBiasOut = 0;
			if (G_LOWGRAPHICS)
			{
				switch (packedFaceFlags & 0x6000)
				{
					case 0x2000: sortBiasOut = (u16)*gDCEnvMapTableA; break;
					case 0x4000: sortBiasOut = (u16)*gDCEnvMapTableB; break;
					case 0x6000: sortBiasOut = *gDCEnvMapTableC; break;
					default: break;
				}
			}
			gDCFaceSortBiasOut[batchCount - 1] = sortBiasOut;

			// Vertex-color resolve for this face's 4 corners. Always runs
			// (NOT gated by G_LOWGRAPHICS -- only the sort-bias lookup
			// above is). Three paths: (1) the tex-anim block's per-vertex
			// lit output (gDCLitColorScratch, indexed by the ORIGINAL
			// mVertIndex, not the welded slot); (2) mFlags bit 0x800 --
			// NEW FINDING, refines dcmodel.h's "medium confidence:
			// transparent/blended face" guess into something concrete:
			// this path reads DCFace::mColor[c] as a PALETTE INDEX into
			// G_COLOUR_TABLE (pColourTable), not a direct RGB value, i.e.
			// 0x800 means "this face's color is a colour-table index", not
			// a blend flag; (3) the face's own baked mColor/mColorExtra
			// (already gamma-corrected by DCModel_CreateFromSModel per
			// dcmodel.h).
			if (*gDCTexAnimFlag != 0)
			{
				// The tint cursor writes RGB at -2, -1 and 0. The table starts here.
				f32 *pLitColor = (f32*)0x0065DFB0;
				for (i32 c = 0; c < 4; c++)
				{
					u16 slot = (u16)(cornerSlots[c] & 0x7FFF);
					u8 origVertIdx = pFaceCursor[c - 8]; // DCFace::mVertIndex[c]
					i32 ri = (i32)(pLitColor[origVertIdx * 3 + 0] * 255.0f);
					i32 gi = (i32)(pLitColor[origVertIdx * 3 + 1] * 255.0f);
					i32 bi = (i32)(pLitColor[origVertIdx * 3 + 2] * 255.0f);
					i32 orAll = ri | gi | bi;
					if ((orAll & (i32)0xFFFFFF00) != 0)
					{
						if (orAll < 0)
						{
							if (ri < 0) ri = 0;
							if (gi < 0) gi = 0;
							if (bi < 0) bi = 0;
						}
						if (((ri | gi | bi) & 0x7FFFFF00) != 0)
						{
							if (ri > 255) ri = 255;
							if (gi > 255) gi = 255;
							if (bi > 255) bi = 255;
						}
					}
					pScratchBase[slot].field_18 = alphaMask | (u32)bi | (((u32)gi | ((u32)ri << 8)) << 8);
					pScratchBase[slot].field_1C = 0;
				}
			}
			else if ((packedFaceFlags & 0x800) != 0)
			{
				for (i32 c = 0; c < 4; c++)
				{
					u16 slot = (u16)(cornerSlots[c] & 0x7FFF);
					u8 colorIdx = pFaceCursor[c - 4]; // DCFace::mColor[0..2] / mColorExtra, raw 4-byte walk
					u32 entry = alphaMask | ((u32*)G_COLOUR_TABLE)[colorIdx];
					u32 packed = (entry & 0xFF00FF00u) | ((u8)entry << 16) | (u8)(entry >> 16);
					pScratchBase[slot].field_18 = packed;
					pScratchBase[slot].field_1C = 0;
				}
			}
			else
			{
				u32 baseColor = alphaMask | *(u32*)(pFaceCursor - 4); // DCFace::mColor[0..2]+mColorExtra
				u32 bc = (baseColor & 0xFF00FF00u) | ((u8)baseColor << 16) | (u8)(baseColor >> 16);
				for (i32 c = 0; c < 4; c++)
				{
					u16 slot = (u16)(cornerSlots[c] & 0x7FFF);
					pScratchBase[slot].field_18 = bc;
					pScratchBase[slot].field_1C = 0;
				}
			}

			pFaceCursor += 56;
			facesLeft--;
			if (facesLeft == 0)
				goto afterFaceLoop;

			// find the next qualifying face (same gate as the search above)
			for (;;)
			{
				u16 nextFlags = *(u16*)(pFaceCursor - 12);
				packedFaceFlags = (u16)overrideMask | (nextFlags & (u16)(overrideMask >> 16));
				u8 nextLowCombined = (u8)overrideMask | (u8)(nextFlags & (u8)(overrideMask >> 16));
				if ((nextLowCombined & 0xC0) != 0 && (*(pFaceCursor + 40) & 2) == 0)
					break;
				pFaceCursor += 56;
				facesLeft--;
				if (facesLeft == 0)
					goto afterFaceLoop;
			}

			if ((packedFaceFlags & 0x40) != 0)
			{
				alphaMode = ((packedFaceFlags & 0x80) != 0) ? 2 : 1;
				alphaMask = ((packedFaceFlags & 0x80) != 0) ? 0x80000000u : 0xFF000000u;
			}
			if ((overrideMask & 0x40) != 0)
			{
				alphaMode = 2;
				alphaMask = 0xFF000000u;
			}
		}
	}
afterFaceLoop:

	// Debug light-vector visualization (only when gDCDebugLightFlag is set).
	if (*gDCDebugLightFlag != 0)
	{
		PCGfx_UseTexture(1, DCGfx_BlendingMode_0);

		f32 origin[4] = { 0.0f, 0.0f, 0.0f, 1.0f };
		f32 originOut[4];
		Algebra_Transform4(originOut, origin);
		f32 originInvW = (fabsf(originOut[3]) > 0.0000000099999999f) ? (1.0f / originOut[3]) : -1.0e12f;
		f32 originX = originOut[0] * originInvW;
		f32 originY = originOut[1] * originInvW;

		i32 lightCount = *gDCLightCount;
		if (lightCount > 0)
		{
			f32 *pLightTint = gDCLightTintTable;
			for (i32 li = 0; li < lightCount; li++)
			{
				f32 lx = *(f32*)0x00550068 * pLightTint[2];
				f32 ly = *(f32*)0x00550068 * pLightTint[1];
				f32 lz = *(f32*)0x00550068 * pLightTint[0];

				f32 in4[4] = { lz, ly, lx, 1.0f };
				f32 out4[4];
				Algebra_Transform4(out4, in4);
				f32 invW = (fabsf(out4[3]) > 0.0000000099999999f) ? (1.0f / out4[3]) : -1.0e12f;
				f32 px = out4[0] * invW;
				f32 py = out4[1] * invW;

				u32 color = 0xFFFFFF00u
					| (u8)(pLightTint[2] * 255.0)
					| (((u8)(pLightTint[1] * 255.0) | (((u8)(pLightTint[0] * 255.0)) << 8)) << 8);

				gsub_509000(originX, originY, invW, -1, px, py, invW, color, 2.0f);
				pLightTint += 3;
			}
		}
	}

	// Fog-distance / no-fog color remap pass over the whole scratch pool
	// range this call touched (pScratchBase[0..pData->mVertexCount)).
	if (*gDCSubObjectMasked == 0)   // 0x477F3D: mov eax,[65DFACh]; test eax,eax; jne epilogue
	{
		if (*gDCNoFogFlag != 0)
		{
			for (i32 i = 0; i < vertexCount; i++)
			{
				u32 c = pScratchBase[i].field_18;
				u32 blended = (*(u32*)0x00550060 * (c >> 8));
				pScratchBase[i].field_18 = blended ^ (0xFFFFFFu & (c ^ blended));
			}
		}
		else if (inFogRange
#ifdef SPIDEY_STANDALONE
				&& !getenv("SPIDEY_NOFOG")
#endif
				)
		{
			for (i32 i = 0; i < vertexCount; i++)
			{
				f32 depth = pScratchBase[i].field_C;
				if (depth < *gDCProjFar && depth > 0.0f)
				{
					i32 remap = 0;
					if (depth > *gDCProjNear)
						remap = (i32)((depth - *gDCProjNear) * *gDCProjScale);
					u32 c = pScratchBase[i].field_18;
					u32 blended = (u32)remap * (c >> 8);
					pScratchBase[i].field_18 = blended ^ (0xFFFFFFu & (c ^ blended));
				}
			}
		}

		// Texture-batch submission: group consecutive OT-build faces that
		// share (texture id, no-light flag, sort-bias), call PCGfx_UseTexture
		// once per batch (skipping the call when nothing changed), then
		// submit the batch's triangle indices from the fixed template.
		i32 idx = 0;
		if (batchCount > 0)
		{
			do
			{
				i32 noLight = inFogRange ? 1 : gDCFaceNoLightOut[idx];
				i32 lastTex = G_USE_TEXTURE_RELATED;
				u16 tex = gDCFaceTexIndexOut[idx];

				if (G_USE_TEXTURE_RELATED != tex || *gDCLastNoLightFlag != noLight)
				{
					if (*gDCForceNoTexFlag != 0)
					{
						PCGfx_UseTexture(1, DCGfx_BlendingMode_0);
					}
					else
					{
						i32 noLightArg = (*gDCForceNoLightFlag != 0) ? 0 : noLight;
						PCGfx_UseTexture(tex, (DCGfx_BlendingMode)noLightArg);
					}
					lastTex = G_USE_TEXTURE_RELATED;
					(*gDCBatchCallCount)++;
				}

				u16 bias = gDCFaceSortBiasOut[idx];
				i32 j = idx;
				*gDCFaceSortKey = sortBias + bias;
				do
				{
					j++;
				} while (j < batchCount
					&& lastTex == (i32)(u16)gDCFaceTexIndexOut[j]
					&& *gDCLastNoLightFlag == (i32)(u8)gDCFaceNoLightOut[j]
					&& bias == gDCFaceSortBiasOut[j]);

				PCGfx_ClipSendIndexedVertList(
					pScratchBase,
					4 * batchCount,
					gDCTriIndexBuffer + 6 * idx,
					6 * (j - idx));

				idx = j;
			} while (idx < batchCount);
		}

		*gDCFaceSortKey = 0;
		*gDCFaceSortKeyExtra = 0;
	}
}

// Debug/validity flag tested (and sometimes cleared) right after the vertex
// pass, but never read again inside this function -- no further observable
// effect on THIS call, kept for faithfulness. Not in idb_globals.txt.
static volatile u8 * const gDCPSXFrameValidFlag = (u8*)0x00660FE0;

// @Ok
// (0x00478180, ~0xE00 bytes). DC_PSXModel_RenderModel: renders a model
// directly from its raw PSX-packed SModel data (spool.h), without going
// through the pre-welded DCModelData vertex/face arrays that
// DCModel_RenderModel (already @Ok) consumes. Session-wide override:
// functional decompilation, not byte-matching (CLAUDE.md task header).
//
// Distinguishing shape vs DCModel_RenderModel, confirmed against the IDA
// decompile of 0x478180 this session:
//  - Reads SModel's raw vertex array directly (pModel+28, stride 8 bytes /
//    4 x i16 = x,y,z,pad), plain int-to-float, no welding, no
//    billboard/stitch handling at all -- DCVert's whole flag vocabulary
//    from dcmodel.h simply does not exist on this path. The scratch pool
//    (gDCVertexPool, same address DCModel_RenderModel uses) is indexed
//    directly by the ORIGINAL SModel vertex index here, with no
//    vertexCount offset and no welded-slot indirection.
//  - Reads SModel's raw face array directly too, starting right after the
//    vertex + normal arrays (pModel+28+8*(NumVertices+NumNormals)), in the
//    SAME byte layout DCModel_CreateFromSModel parses when building DCFace
//    (see that function's long comment in dcmodel.cpp): u32 flags @0
//    (mFlags = low 16 bits, record length in bytes = high 16 bits), u8
//    vertIndex[4] @4, u8 color[4] @8 (R,G,B,ColorExtra), a texture-info
//    pointer @16, then either 8 packed UV bytes @20 or two 4-entry u16
//    blocks @20/@28 for U/V.
//  - No batching: every qualifying face is submitted to
//    PCGfx_ClipSendIndexedVertList individually (2 triangles, 6 indices)
//    as soon as it's built, instead of DCModel_RenderModel's
//    group-by-texture OT/batch pass. No sort-key (gDCFaceSortKey) is ever
//    written on this path.
//  - pData (the DCModelData*) is OPTIONAL here (checked for null, unlike
//    DCModel_RenderModel which bails if null) and is read for exactly one
//    thing: its mFlags bit 0x400, to pick which of the two UV encodings a
//    face uses (byte-packed vs u16-packed) -- REFINES/CONFIRMS dcmodel.h's
//    "0x400 - low confidence, new-style UV encoding" guess into "0x400 =
//    this model's faces store UV as 8 packed bytes (PVRRect-style format)
//    rather than 4 u16 pairs", matching DCModel_CreateFromSModel's own
//    formatFlags-driven UV branch bit for bit (mFlags 0x400 is set there
//    exactly when formatFlags&1==0, which selects that same byte-UV branch
//    at load time).
//  - When pData is null (true "raw PSX" mode, no precomputed data at all),
//    a triangle's 4th corner/UV/color-extra gets duplicated from the 3rd
//    right here at render time (mFlags bit 0x10), matching
//    DCModel_CreateFromSModel's load-time duplication for the case where
//    that conversion never ran on this model.
//  - UV normalization divides by a range read from the texture-info
//    struct's own bytes (offsets 0/1 and 10/11: "range low", "range
//    high"), NOT the PVRRect pack-info table DCModel_CreateFromSModel's
//    byte-UV branch uses -- a genuinely different (simpler/older) UV scale
//    specific to this renderer, reproduced as traced.
//  - vertexScale defaults to 1.0f; only overridden (to gFloatSuperRelated)
//    when pData is null AND not low-graphics -- no hi-res-scale/
//    transparent-face bump like DCModel_RenderModel has.
//  - CONFIRMS DCModelData::mFlags is read at the SAME +0xC offset
//    (dcmodel.h) as DCModel_RenderModel/RenderSuperItem/
//    M3d_RenderBackground.
//  - gDCForceNoLightFlag (0x660FF8, already declared above for
//    DCModel_RenderModel) is reused here with DIFFERENT effect: it forces
//    blending mode 0 while keeping the real resolved texIndex, rather than
//    just overriding the "no light" argument like DCModel_RenderModel
//    does; DCModel_RenderModel's separate gDCForceNoTexFlag (0x660FFC,
//    forces texIndex to 1) is not read by this function at all.
//  - The matrix multiply that composes objToScreen and installs it as the
//    active transform (gDCGfxMatrix) is done in the original by 16 manual
//    dot products plus a same-shaped local-to-local copy (sub_478140, a
//    small thiscall matrix-copy helper -- confirmed by disassembly to just
//    copy 4 vector4d rows, no other side effect) instead of calling
//    gsub_476A00; both compute the identical product, so gsub_476A00 +
//    memcpy is reused here for consistency with DCModel_RenderModel
//    (functional decomp, not byte-match, per the session override).
void DC_PSXModel_RenderModel(SModel const *pModel, matrix4x4 const *pTransform, void const *pUnused, DCModelData *pData)
{
	if ((pModel->Flags & 0x20) != 0)
		return;
	if (pModel->NumFaces == 0)
		return;

	(*gDCStatCalls)++;

	matrix4x4 objToScreen;
	gsub_476A00(&objToScreen, pTransform, gDCFinalProjMatrix);
	memcpy(gDCGfxMatrix, &objToScreen, sizeof(matrix4x4));

	f32 vertexScale = 1.0f;
	if (pData == 0 && !G_LOWGRAPHICS)
		vertexScale = gFloatSuperRelated;

	tagKMVERTEX3 *pScratchBase = gDCVertexPool;

	// --- Vertex transform pass: plain int-to-float, no welding, no
	// billboard/stitch handling. Scratch slot == original vertex index. ---
	i32 numVerts = pModel->NumVertices;
	if (numVerts > 0)
	{
		i16 *pSrcVert = (i16*)((u8*)pModel + 28);
		f32 *pWrite = &pScratchBase->field_4;

		for (i32 i = 0; i < numVerts; i++)
		{
			f32 in4[4] = { (f32)pSrcVert[0], (f32)pSrcVert[1], (f32)pSrcVert[2], 1.0f };
			f32 out4[4];
			Algebra_Transform4(out4, in4);

			f32 w = out4[3];
			f32 invW = (fabsf(w) > 0.0000000099999999f) ? (1.0f / w) : -1.0e12f;

			pWrite[0] = out4[0] * invW; // field_4 = sx
			pWrite[1] = out4[1] * invW; // field_8 = sy
			pWrite[2] = invW * vertexScale; // field_C = sz

			pWrite += 8;
			pSrcVert += 4;
		}
	}

	if (*gDCPSXFrameValidFlag != 0)
	{
		if (pModel->NumVertices > pModel->NumNormals)
			*gDCPSXFrameValidFlag = 0;
		if (pUnused == 0)
			*gDCPSXFrameValidFlag = 0;
	}

	// --- Per-face immediate submission (no OT batching). ---
	i32 numFaces = pModel->NumFaces;
	if (numFaces > 0)
	{
		u8 *pFaceCursor = (u8*)pModel + 8 * (pModel->NumVertices + pModel->NumNormals) + 28;
		i32 overrideMask = *gDCOverrideFlags;
		i32 facesLeft = numFaces;
		i32 vertsSubmitted = 0;

		do
		{
			u32 srcFlags = *(u32*)pFaceCursor;
			u16 faceFlags = (u16)srcFlags;

			u8 vertIdx[4] = { pFaceCursor[4], pFaceCursor[5], pFaceCursor[6], pFaceCursor[7] };
			u32 colorWord = *(u32*)(pFaceCursor + 8);
			u8 color[4] = { pFaceCursor[8], pFaceCursor[9], pFaceCursor[10], pFaceCursor[11] };

			if ((faceFlags & 0x10) != 0 && pData == 0)
			{
				// Raw mode only: duplicate the 3rd corner into the 4th for
				// a triangle (DCModel_CreateFromSModel does this at load
				// time; here it has to happen per-render since there is no
				// converted DCFace to have already done it).
				vertIdx[3] = vertIdx[2];
				if (faceFlags & 0x800)
					color[3] = color[2];
				colorWord = *(u32*)color;
				*(u16*)(pFaceCursor + 26) = *(u16*)(pFaceCursor + 24); // dup corner-2's UV into corner-3
			}

			i32 packedFlags = (u16)overrideMask | (faceFlags & (u16)(overrideMask >> 16));
			// 0x476D00 / 0x478180: the face-flag byte is masked with BYTE2 of the
			// override (bits 16..23), not byte 1. With the usual 0xFFFF0000 override
			// byte 1 is zero, which skipped every face (found by the standalone build:
			// no level geometry ever reached the renderer).
			u8 lowCombined = (u8)overrideMask | (u8)(faceFlags & (u8)(overrideMask >> 16));

			if ((lowCombined & 0xC0) != 0)
			{
				u32 alphaMask = 0xFF000000u;
				i32 alphaMode = 0;
				if ((packedFlags & 0x40) != 0)
				{
					alphaMode = ((packedFlags & 0x80) != 0) ? 2 : 1;
					alphaMask = ((packedFlags & 0x80) != 0) ? 0x80000000u : 0xFF000000u;
				}
				if ((overrideMask & 0x40) != 0)
				{
					alphaMode = 2;
					alphaMask = 0x80000000u;
				}

				u16 texIndex = 1;
				bool haveUV = false;

				if ((packedFlags & 1) != 0)
				{
					u8 *pTexInfo = *(u8**)(pFaceCursor + 16);
					// print_if_false(pTexInfo != 0, "Texture pointer is zero.") omitted (debug-only assert).

					if (*gDCForceTexIndex != 0)
						texIndex = (u16)*gDCForceTexIndex;
					else
						texIndex = *(u16*)(pTexInfo + 2);
					// print_if_false((packedFlags&2)!=0, "Non ARB Texture") omitted (debug-only assert).

					f32 rangeLo0 = (f32)pTexInfo[0];
					f32 rangeLo1 = (f32)pTexInfo[1];
					f32 width  = (f32)pTexInfo[10] - rangeLo0;
					f32 height = (f32)pTexInfo[11] - rangeLo1;
					// print_if_false(width!=0 && height!=0, "Texture range is zero.") omitted.

					if (width != 0.0f && height != 0.0f)
					{
						f32 invWidth = 1.0f / width;
						f32 invHeight = 1.0f / height;
						haveUV = true;

						if (pData != 0 && (pData->mFlags & 0x400) == 0)
						{
							// u16-packed UV: U block @+20, V block @+28.
							u16 *pU = (u16*)(pFaceCursor + 20);
							u16 *pV = (u16*)(pFaceCursor + 28);
							for (i32 c = 0; c < 4; c++)
							{
								u16 slot = vertIdx[c];
								pScratchBase[slot].field_10 = ((f32)pU[c] - rangeLo0) * invWidth;
								pScratchBase[slot].field_14 = ((f32)pV[c] - rangeLo1) * invHeight;
							}
						}
						else
						{
							// byte-packed UV (PVRRect format): 8 bytes @+20
							// (U0,V0,U1,V1,U2,V2,U3,V3).
							for (i32 c = 0; c < 4; c++)
							{
								u16 slot = vertIdx[c];
								pScratchBase[slot].field_10 = ((f32)pFaceCursor[20 + 2 * c]     - rangeLo0) * invWidth;
								pScratchBase[slot].field_14 = ((f32)pFaceCursor[20 + 2 * c + 1] - rangeLo1) * invHeight;
							}
						}
					}
				}

				if (!haveUV)
				{
					for (i32 c = 0; c < 4; c++)
					{
						u16 slot = vertIdx[c];
						pScratchBase[slot].field_10 = 0.0f;
						pScratchBase[slot].field_14 = 0.0f;
					}
				}

				// Vertex-color resolve, same two paths DCModel_RenderModel
				// uses (palette-index-via-colour-table on 0x800, else the
				// face's own baked color), just applied directly by
				// original vertex index instead of a welded slot.
				if ((packedFlags & 0x800) != 0)
				{
					for (i32 c = 0; c < 4; c++)
					{
						u16 slot = vertIdx[c];
						u32 entry = alphaMask | ((u32*)G_COLOUR_TABLE)[color[c]];
						u32 packed = (entry & 0xFF00FF00u) | ((u8)entry << 16) | (u8)(entry >> 16);
						pScratchBase[slot].field_18 = packed;
						pScratchBase[slot].field_1C = 0;
					}
				}
				else
				{
					u32 baseColor = alphaMask | colorWord;
					u32 bc = (baseColor & 0xFF00FF00u) | ((u8)baseColor << 16) | (u8)(baseColor >> 16);
					for (i32 c = 0; c < 4; c++)
					{
						u16 slot = vertIdx[c];
						pScratchBase[slot].field_18 = bc;
						pScratchBase[slot].field_1C = 0;
					}
				}

				// Per-vertex tint multiply (PSX-specific; DCModel_RenderModel
				// has no equivalent of this exact pass). Reuses the same
				// tex-anim color-source globals DCModel_RenderModel's tint
				// block writes into.
				if (*gDCTintFlag != 0)
				{
					for (i32 c = 0; c < 4; c++)
					{
						u16 slot = vertIdx[c];
						u32 orig = pScratchBase[slot].field_18;
						u32 term1 = 0xFF0000u & ((u32)(*gDCTexAnimColorSrcB) * (orig & 0xFF00u));
						u32 term2 = ((u16)(*gDCTexAnimColorSrcA) * (u8)orig) & 0xFF00u;
						u32 term3 = ((u32)(*gDCTexAnimColorSrcC) * (0xFF0000u & orig)) & 0xFF0000FFu;
						pScratchBase[slot].field_18 = (orig & 0xFF000000u) | ((term1 | term2 | term3) >> 8);
						pScratchBase[slot].field_1C = 0;
					}
				}

				// Per-corner fog-distance color remap (same formula
				// DCModel_RenderModel uses, applied per corner here instead
				// of over the whole scratch-pool range).
				bool touchedFog = false;
				if (*gDCFogEnabled != 0 && !G_LOWGRAPHICS)
				{
					for (i32 c = 0; c < 4; c++)
					{
						u16 slot = vertIdx[c];
						f32 depth = pScratchBase[slot].field_C;
						if (depth < *gDCProjFar && depth > 0.0f)
						{
							touchedFog = true;
							i32 remap = (depth > *gDCProjNear) ? (i32)((depth - *gDCProjNear) * *gDCProjScale) : 0;
							u32 c_ = pScratchBase[slot].field_18;
							u32 blended = (u32)remap * (c_ >> 8);
							pScratchBase[slot].field_18 = blended ^ (0xFFFFFFu & (c_ ^ blended));
						}
					}
				}

				if (G_USE_TEXTURE_RELATED != texIndex || *gDCLastNoLightFlag != alphaMode)
				{
					if (*gDCForceNoLightFlag != 0)
						PCGfx_UseTexture(texIndex, DCGfx_BlendingMode_0);
					else if (!touchedFog)
						PCGfx_UseTexture(texIndex, (DCGfx_BlendingMode)alphaMode);
					else
						PCGfx_UseTexture(texIndex, DCGfx_BlendingMode_1);
					(*gDCBatchCallCount)++;
				}

				u16 indices[6] = {
					vertIdx[0], vertIdx[1], vertIdx[2],
					vertIdx[2], vertIdx[1], vertIdx[3],
				};
				PCGfx_ClipSendIndexedVertList(pScratchBase, 4 * vertsSubmitted, indices, 6);
				vertsSubmitted++;
			}

			pFaceCursor += (u16)(srcFlags >> 16);
			facesLeft--;
		} while (facesLeft != 0);
	}
}

struct SRGBI
{
	u8 r;
	u8 g;
	u8 b;
	u8 Interval;
};

struct SColourPulseInfo
{
	u8 VertexColourIndex;
	u8 ListLen;
	u8 ListPos;
	u8 t;
	SRGBI RGBs[1];
};

// @Ok
// (0x00476790). Advances the per-record colour-pulse phase by the frame
// delta (from the xblank counters) and wraps the list position. Logic
// verified against the IDB. The build is 11 bytes longer than the original
// because MSVC6 saves ebp in the prologue while the original defers it to
// the loop entry (a register save/restore timing difference only, no logic
// difference); left as-is in the functional phase.


void M3d_PreprocessPulsingColours(i32 Region)
{
	if (Region == -1)
	{
		return;
	}

	u32 *pData = PSXRegion[Region].pColourPulseData;
	if (!pData)
	{
		return;
	}

	print_if_false(pData[-2] == 7, "Pointer doesn't point to a colour pulsing packet");

	u32 *pEnd = reinterpret_cast<u32*>(reinterpret_cast<u8*>(pData) + pData[-1]);

	G_COLOUR_TABLE = PSXRegion[Region].pColourTable;
	print_if_false(G_COLOUR_TABLE != 0, "Pulsing a non-existent colour table");

	i32 dt;
	if (G_XBLANKS_NOW < G_XBLANKS_THEN)
	{
		dt = 1;
	}
	else
	{
		dt = G_XBLANKS_NOW - G_XBLANKS_THEN;
	}

	while (pData < pEnd)
	{
		SColourPulseInfo *pColourPulseInfo = reinterpret_cast<SColourPulseInfo*>(pData);

		print_if_false(pColourPulseInfo->ListLen != 0, "Zero list length");

		SRGBI *RGBs = pColourPulseInfo->RGBs;
		i32 ListLen = pColourPulseInfo->ListLen;
		i32 ListPos = pColourPulseInfo->ListPos;
		i32 t = pColourPulseInfo->t;

		t += dt;
		while (t >= RGBs[ListPos].Interval)
		{
			t -= RGBs[ListPos].Interval;
			ListPos++;
			if (ListPos == ListLen)
			{
				ListPos = 0;
			}
		}

		pColourPulseInfo->ListPos = ListPos;
		pColourPulseInfo->t = t;

		i32 rgb0[3];
		rgb0[0] = RGBs[ListPos].r;
		rgb0[1] = RGBs[ListPos].g;
		rgb0[2] = RGBs[ListPos].b;

		i32 OldListPos = ListPos;
		ListPos++;
		if (ListPos == ListLen)
		{
			ListPos = 0;
		}

		i32 drgb[3];
		drgb[0] = RGBs[ListPos].r;
		drgb[1] = RGBs[ListPos].g;
		drgb[2] = RGBs[ListPos].b;

		print_if_false(RGBs[ListPos].Interval != 0, "Zero interval");

		i32 interval = RGBs[OldListPos].Interval;

		i32 blue = rgb0[2] + t * (drgb[2] - rgb0[2]) / interval;
		i32 green = rgb0[1] + t * (drgb[1] - rgb0[1]) / interval;
		i32 red = rgb0[0] + t * (drgb[0] - rgb0[0]) / interval;

		u32 colour = gConvertedColors[red & 0xFF] | (gConvertedColors[green & 0xFF] << 8) | (gConvertedColors[blue & 0xFF] << 16);

		G_COLOUR_TABLE[pColourPulseInfo->VertexColourIndex] = colour;

		pData = reinterpret_cast<u32*>(pColourPulseInfo + 1) + ListLen - 1;
	}
}
// The wibble tables are i16 (0x47627F: movsx eax, word ptr [660748h + ...]).
static i16 * const gWibbleTables = (i16*)0x00660748;
static volatile i32 * const gM3dWibbleFrame = (i32*)0x0065CFA4;
static volatile i32 * const gM3dWibbleScroll = (i32*)0x0065F724;
static u8 * const * const gM3dWibbleModelData = (u8* const*)0x005F6764;

// @Ok
// (0x00475FB0, 1848 bytes). Preprocesses the wibble (texture animation)
// packets for a PSX region. For each wibble packet it reads the per-face
// wibble indices, looks them up in gWibbleTables (offset by the frame),
// adds the scroll offset, and writes the resulting texture coordinates
// (u,v) into the model's DCFace data, scaled by the texture's 1/width,1/height.
//
// Packet layout (bytes, from the disassembly):
//   +0  u32 item offset into the region's super item array (12 + 0x24 * index)
//   +4  i16 scroll x per frame, +6 i16 scroll y per frame, +8 i32 wibble speed
//   +12 u16 face count, then 16 bytes per face: 8 wibble index bytes at
//   +16..+23 (read via a pointer at +23 with -5,-4,-1,0,3,4,7,8), stride 16.
//
// Rewritten 2026-09-03 against 0x475FB0: the first translation used u32
// pointer arithmetic for byte offsets (packet + 12 read 48 bytes in), tested
// the item flag as a dword, used the wrong face base (model + 28 + 8 *
// (NumVertices + NumNormals)), read gWibbleTables as i32 and never advanced
// the DCFace output pointer between faces.
void M3d_PreprocessWibblyTextures(i32 region)
{
	if (region == -1)
		return;

	u8 *packet = reinterpret_cast<u8*>(PSXRegion[region].pTexWibData);
	if (packet == 0)
		return;

	print_if_false(*reinterpret_cast<i32*>(packet - 8) == 6, "Pointer doesn't point to a texture-wibble packet");

	f32 invWidth = 1.0f;
	f32 invHeight = 1.0f;
	f32 texX = 1.0f;
	f32 texY = 1.0f;

	while (*reinterpret_cast<u32*>(packet) != 0)
	{
		u32 itemOffset = *reinterpret_cast<u32*>(packet);
		print_if_false((itemOffset - 12) % 0x24 == 0, "PreProcessWibblyTextures(): itemIndex not computed correctly.");

		u8 *pItem = reinterpret_cast<u8*>(PSXRegion[region].pSuper) + 64 * ((itemOffset - 12) / 0x24);
		u8 *pNext = packet + 16;
		i32 numFaces = *reinterpret_cast<u16*>(packet + 12);

		// high byte of CItem::mFlags: skip items culled this frame (0x8000)
		if (*reinterpret_cast<i8*>(pItem + 5) >= 0)
		{
			u16 modelIndex = *reinterpret_cast<u16*>(pItem + 26);
			u8 itemRegion = pItem[31];
			SModel *pModel = PSXRegion[itemRegion].ppModels[modelIndex];
			// faces start after the header, the SVECTOR vertices and normals
			u8 *facePtr = reinterpret_cast<u8*>(pModel) + 28 + 8 * (pModel->NumVertices + pModel->NumNormals);
			u8 *pModelData = gM3dWibbleModelData[itemRegion] + 36 * modelIndex;
			u8 *pFaces = *reinterpret_cast<u8**>(pModelData + 4);

			i32 frame = (*gM3dWibbleFrame & 0x3FF) + 1;
			i32 scrollY = (frame * *reinterpret_cast<i16*>(packet + 6)) >> 4;
			i32 scrollX = (frame * *reinterpret_cast<i16*>(packet + 4)) >> 4;
			gM3dWibbleScroll[0] = (frame * *reinterpret_cast<i32*>(packet + 8)) >> 10;

			if (numFaces > 0)
			{
				f32 *pOut = reinterpret_cast<f32*>(pFaces + 36);
				u8 *pWib = packet + 23;
				pNext += 16 * numFaces;

				for (i32 f = numFaces; f != 0; f--)
				{
					print_if_false(*facePtr & 1, "Wibbling a non-textured face");
					print_if_false((scrollX == 0 && scrollY == 0) || (*facePtr & 0x20) != 0, "Scrolling a non-tiled texture");

					i32 tx0, ty0, tx1, ty1, tx2, ty2, tx3, ty3;
					if ((*reinterpret_cast<i32*>(pModelData + 12) & 0x400) != 0)
					{
						tx0 = facePtr[20] << 24;
						ty0 = facePtr[21] << 24;
						tx1 = facePtr[22] << 24;
						ty1 = facePtr[23] << 24;
						tx2 = facePtr[24] << 24;
						ty2 = facePtr[25] << 24;
						tx3 = facePtr[26] << 24;
						ty3 = facePtr[27] << 24;
					}
					else
					{
						tx0 = *reinterpret_cast<u16*>(facePtr + 20) << 8;
						ty0 = *reinterpret_cast<u16*>(facePtr + 28) << 8;
						tx1 = *reinterpret_cast<u16*>(facePtr + 22) << 8;
						ty1 = *reinterpret_cast<u16*>(facePtr + 30) << 8;
						tx2 = *reinterpret_cast<u16*>(facePtr + 24) << 8;
						ty2 = *reinterpret_cast<u16*>(facePtr + 32) << 8;
						tx3 = *reinterpret_cast<u16*>(facePtr + 26) << 8;
						ty3 = *reinterpret_cast<u16*>(facePtr + 34) << 8;
					}
					if (scrollX != 0)
					{
						tx0 += 2 * scrollX;
						tx1 += 2 * scrollX;
						tx2 += 2 * scrollX;
						tx3 += 2 * scrollX;
					}
					if (scrollY != 0)
					{
						ty0 += 2 * scrollY;
						ty1 += 2 * scrollY;
						ty2 += 2 * scrollY;
						ty3 += 2 * scrollY;
					}

					u8 scroll = static_cast<u8>(gM3dWibbleScroll[0]);
					i32 w;
					w = pWib[-5] >> 4;
					i32 cu0 = tx0 + (w ? gWibbleTables[64 * w + ((scroll + 4 * (pWib[-5] & 0xF)) & 0x3F)] : 0);
					w = pWib[-4] >> 4;
					i32 cv0 = ty0 + (w ? gWibbleTables[64 * w + ((scroll + 4 * (pWib[-4] & 0xF)) & 0x3F)] : 0);
					w = pWib[-1] >> 4;
					i32 cu1 = tx1 + (w ? gWibbleTables[64 * w + ((scroll + 4 * (pWib[-1] & 0xF)) & 0x3F)] : 0);
					w = pWib[0] >> 4;
					i32 cv1 = ty1 + (w ? gWibbleTables[64 * w + ((scroll + 4 * (pWib[0] & 0xF)) & 0x3F)] : 0);
					w = pWib[3] >> 4;
					i32 cu2 = tx2 + (w ? gWibbleTables[64 * w + ((scroll + 4 * (pWib[3] & 0xF)) & 0x3F)] : 0);
					w = pWib[4] >> 4;
					i32 cv2 = ty2 + (w ? gWibbleTables[64 * w + ((scroll + 4 * (pWib[4] & 0xF)) & 0x3F)] : 0);
					w = pWib[7] >> 4;
					i32 cu3 = tx3 + (w ? gWibbleTables[64 * w + ((scroll + 4 * (pWib[7] & 0xF)) & 0x3F)] : 0);
					w = pWib[8] >> 4;
					i32 cv3 = ty3 + (w ? gWibbleTables[64 * w + ((scroll + 4 * (pWib[8] & 0xF)) & 0x3F)] : 0);

					u8 *pTex = *reinterpret_cast<u8**>(facePtr + 16);
					print_if_false(pTex != 0, "No Texture data");
					if (pTex != 0)
					{
						u8 *pVram = *reinterpret_cast<u8**>(pTex + 28);
						print_if_false(pVram != 0, "Texture has no pVRAMRect info");
						print_if_false(*reinterpret_cast<i32*>(pVram + 4) != 0, "pVRAMRect info has no pack info");
						u8 vramType = *pVram;
						u8 *pPack = *reinterpret_cast<u8**>(pVram + 4);
						f32 width;
						if ((vramType & 8) != 0)
						{
							texX = static_cast<f32>(2 * (*pPack & 0x7F));
							texY = static_cast<f32>(pPack[2]);
							width = static_cast<f32>(2 * *reinterpret_cast<u16*>(pPack + 4));
						}
						else if ((vramType & 0x10) != 0)
						{
							texX = static_cast<f32>(*pPack);
							texY = static_cast<f32>(pPack[2]);
							width = static_cast<f32>(*reinterpret_cast<u16*>(pPack + 4));
						}
						else
						{
							print_if_false((vramType & 4) != 0, "Unexpected Texture bit depth");
							texX = static_cast<f32>(4 * (*pPack & 0x3F));
							texY = static_cast<f32>(pPack[2]);
							width = static_cast<f32>(4 * *reinterpret_cast<u16*>(pPack + 4));
						}
						f32 height = static_cast<f32>(*reinterpret_cast<u16*>(pPack + 6));
						print_if_false(width != 0.0f, "Zero Tex Width");
						print_if_false(height != 0.0f, "Zero Tex Height");
						invWidth = 1.0f / width;
						invHeight = 1.0f / height;
					}

					// DCFace: mU[4] at +0x14, mV[4] at +0x24, 0x38 bytes per face
					pOut[-4] = (static_cast<f32>(cu0 >> 8) - texX) * invWidth;
					pOut[0]  = (static_cast<f32>(cv0 >> 8) - texY) * invHeight;
					pOut[-3] = (static_cast<f32>(cu1 >> 8) - texX) * invWidth;
					pOut[1]  = (static_cast<f32>(cv1 >> 8) - texY) * invHeight;
					pOut += 14;
					pOut[-16] = (static_cast<f32>(cu2 >> 8) - texX) * invWidth;
					pOut[-12] = (static_cast<f32>(cv2 >> 8) - texY) * invHeight;
					pOut[-15] = (static_cast<f32>(cu3 >> 8) - texX) * invWidth;
					pOut[-11] = (static_cast<f32>(cv3 >> 8) - texY) * invHeight;

					facePtr += 4 * (*reinterpret_cast<u32*>(facePtr) >> 18);
					pWib += 16;
				}
			}
			packet = pNext;
		}
		else
		{
			packet += 16 + 16 * numFaces;
		}
	}
}

static volatile u8 * const gM3dBackgroundFlag = (u8*)0x00550024;
static volatile i32 * const gM3dBackgroundDword = (i32*)0x00660F90;
static u32 ** const gM3dBackgroundClut = (u32**)0x0064F5D0;
// Fixed 2026-09-01: was u32* const*, so the "+ 36 * modelIndex" below
// advanced 36 DWORDS (144 bytes) per model instead of the 36 BYTES the
// original advances (sizeof(DCModelData) == 0x24; the disassembly does
// lea ecx,[ecx+ecx*8]; lea ebp,[edx+ecx*4]).
static u8 * const * const gM3dBackgroundModelData = (u8* const*)0x005F6764;
static volatile i32 * const gM3dBackgroundSave = (i32*)0x0054D384;
static volatile f32 * const gM3dBackgroundScale = (f32*)0x00550090;
static volatile u8 * const gM3dBackgroundFlagTwo = (u8*)0x00652F3C;
static volatile u8 * const gM3dBackgroundFlagThree = (u8*)0x00660FE2;
// Fixed 2026-09-01: was i32*, which made the matrix4x4 construction
// below CONVERT the stored float bit patterns to float (1.0f became
// 1065353216.0f) instead of reading them as floats. The original does
// a plain dword copy out of this table.
static f32 * const gM3dIdentityOne = (f32*)0x0064E518;
static i32 * const gM3dIdentityTwo = (i32*)0x0064E51C;
static i32 * const gM3dIdentityThree = (i32*)0x0064E520;

// @Ok
// (0x004747C0, 1089 bytes). Renders the background models. Walks a linked
// list of background entries (next at +0x20) from the end, and for each
// entry that is usable and in a usable PSX region, builds a rotation matrix
// from the entry's angles, scales it, and renders the model.
void M3d_RenderBackground(void *pList)
{
	if (pList == 0)
		return;

	PCGfx_SetRenderParameter(DCGfx_RenderParameter_4, (DCGfx_RenderSetting)(DCGfx_RenderSetting_e | DCGfx_RenderSetting_1));
	PCGfx_SetRenderParameter(DCGfx_RenderParameter_1, DCGfx_RenderSetting_9);
	PCGfx_UseTexture(1, DCGfx_BlendingMode_0);

	u8 savedFlag = *gM3dBackgroundFlag;
	*gM3dBackgroundFlag = 1;
	*gM3dBackgroundFlagTwo = 1;

	// count the nodes in the linked list
	i32 count = 0;
	void *node = pList;
	while (node != 0)
	{
		node = *(void**)((u8*)node + 0x20);
		count++;
	}

	for (i32 i = count; i > 0; i--)
	{
		if (i >= 11 || *gM3dBackgroundFlagThree == 0)
		{
			// find the i-th node from the start
			void *v4 = pList;
			i32 v5 = i - 1;
			while (v5 != 0)
			{
				v4 = *(void**)((u8*)v4 + 0x20);
				v5--;
			}

			i8 entryFlag = *(i8*)((u8*)v4 + 5);
			u8 region = *(u8*)((u8*)v4 + 0x1F);
			if (entryFlag >= 0 && PSXRegion[region].Usable != 0)
			{
				*gM3dBackgroundDword = -65536;
				*gM3dBackgroundClut = PSXRegion[region].pColourTable;
				u16 modelIndex = *(u16*)((u8*)v4 + 0x1A);
				SModel *pModel = PSXRegion[region].ppModels[modelIndex];
				DCModelData *pModelData = (DCModelData*)(gM3dBackgroundModelData[region] + 36 * modelIndex);
				i16 angleX = *(i16*)((u8*)v4 + 0x14);
				i16 angleY = *(i16*)((u8*)v4 + 0x16);
				i16 angleZ = *(i16*)((u8*)v4 + 0x18);

				matrix4x4 v48;
				if (angleX != 0 || angleY != 0 || angleZ != 0)
				{
					f32 scale = 3.1415927f / 2048.0f;
					f32 z = (f32)angleZ * scale;
					f32 y = (f32)angleY * scale;
					f32 x = (f32)angleX * scale;
					f32 sinx = (f32)sin(x), cosx = (f32)cos(x);
					f32 siny = (f32)sin(y), cosy = (f32)cos(y);
					f32 sinz = (f32)sin(z), cosz = (f32)cos(z);
					// Fixed 2026-09-01: the old cell assignment here did not
					// come out orthonormal (for a pure X rotation it produced
					// rows (0,0,sinx) and (0,-sinx,sinx)). Re-derived from the
					// raw disassembly of the 16 constructor arguments at
					// 0x4749F7..0x474AB1, which is the same YXZ matrix
					// M3d_Render builds at 0x474359.
					f32 czcy = cosz * cosy;
					f32 szsy = sinz * siny;
					f32 czsy = cosz * siny;
					f32 szcy = sinz * cosy;
					v48 = matrix4x4(
						szsy * sinx + czcy, sinz * cosx, szcy * sinx - czsy, 0.0f,
						czsy * sinx - szcy, cosz * cosx, czcy * sinx + szsy, 0.0f,
						siny * cosx,        -sinx,       cosy * cosx,        0.0f,
						0.0f,               0.0f,        0.0f,               1.0f);
				}
				else
				{
					// identity matrix from the global tables
					v48 = matrix4x4(
						gM3dIdentityOne[0], gM3dIdentityOne[1], gM3dIdentityOne[2], gM3dIdentityOne[3],
						gM3dIdentityOne[4], gM3dIdentityOne[5], gM3dIdentityOne[6], gM3dIdentityOne[7],
						gM3dIdentityOne[8], gM3dIdentityOne[9], gM3dIdentityOne[10], gM3dIdentityOne[11],
						gM3dIdentityOne[12], gM3dIdentityOne[13], gM3dIdentityOne[14], gM3dIdentityOne[15]);
				}

				f32 s = *gM3dBackgroundScale;
#ifdef SPIDEY_STANDALONE
				// The sky stays behind the extended full-map depth range.
				const char* fullMapOption = getenv("SPIDEY_FULL_MAP");
				if (!fullMapOption || atoi(fullMapOption) != 0)
					s *= 1024.0f;
#endif
				matrix4x4 v49 = matrix4x4(s, 0, 0, 0, 0, s, 0, 0, 0, 0, s, 0, 0, 0, 0, 1.0f);

				matrix4x4 v50;
				gsub_476A00(&v50, &v48, &v49);
				memcpy(&v48, &v50, sizeof(matrix4x4));

				// 0x474B3A: the translation row comes from the camera matrix
				// (0x56E738 + 0x30, the camera position), so the backdrop is
				// always centred on the camera
				const f32* pCamRow = reinterpret_cast<const f32*>(0x0056E768);
				v48.field_0[3].field_0[0] = pCamRow[0];
				v48.field_0[3].field_0[1] = pCamRow[1];
				v48.field_0[3].field_0[2] = pCamRow[2];
				v48.field_0[3].field_0[3] = pCamRow[3];

				i32 saved = *gM3dBackgroundSave;
				*gM3dBackgroundSave = 0;
				i32 modelFlags = *(i32*)((u8*)pModelData + 0xC);
				if ((modelFlags & 0x100) == 0)
				{
					if ((modelFlags & 0x4000) != 0)
						DC_PSXModel_RenderModel(pModel, &v48, 0, pModelData);
					else
						DCModel_RenderModel(pModel, pModelData, &v48);
				}
				*gM3dBackgroundSave = saved;
			}
		}
	}

	*gM3dBackgroundFlagTwo = 0;
	*gM3dBackgroundFlag = savedFlag;
	PCGfx_SetRenderParameter(DCGfx_RenderParameter_1, DCGfx_RenderSetting_8);
	PCGfx_SetRenderParameter(DCGfx_RenderParameter_4, DCGfx_RenderSetting_e);
	PCGfx_UseTexture(1, DCGfx_BlendingMode_0);
	*gM3dBackgroundFlagThree = 0;
}

// @Ok
// @Test
// can't get it to match but that's fine, looks good tho
void M3d_RenderCleanup(void)
{
	SetDrawArea();
	G_PPOLY += 3;

	stubbed_printf(G_RENDER_BUF);

	if (gWideScreen)
	{
		PCGfx_UseTexture(1, DCGfx_BlendingMode_0);

		f32 v2 = (f32)G_GAME_RESOLUTION_Y;
		f32 v5 = (f32)(unsigned int)G_YRES;
		f32 v1 = v2 / v5;
		f32 v6 = (f32)gWideScreen;
		f32 v12 = v1 * v6;
		f32 v7 = (f32)G_GAME_RESOLUTION_X;
		f32 v3 = (f32)(unsigned int)G_XRES;
		f32 v8 = v7 / v3 * 512.0f;
		PCGfx_DrawQuad2D(
				0,
				0,
				v8,
				v12,
				0,
				0,
				1.0,
				1.0,
				0xFF000000,
				0.0,
				false);

		f32 v13 = (f32)G_GAME_RESOLUTION_Y;
		f32 v9 = (f32)(unsigned int)G_YRES;
		f32 v4 = v13 / v9;
		f32 v14 = (f32)gWideScreen;
		f32 v18 = v14 * v4;
		f32 v15 = (f32)G_GAME_RESOLUTION_X;
		f32 v10 = (f32)(unsigned int)G_XRES;
		f32 v11 = v15 / v10 * 512.0f;
		f32 v16 = (f32)(240 - gWideScreen);
		f32 v17 = v16 * v4;
		PCGfx_DrawQuad2D(
				0,
				v17,
				v11,
				v18,
				0,
				0,
				1.0,
				1.0,
				0xFF000000,
				0.0,
				false);
	}
}

typedef void (*ConvertMATRIXTomatrix4x4_fn)(MATRIX*, matrix4x4*);
typedef void (*gsub_470610_fn)(u16);
typedef void (*gsub_46D5D0_fn)(i16*, i16*);
typedef void (*gsub_4021D0_fn)(matrix4x4*);

// @Bogus
// @FIXME forward to original: ConvertMATRIXTomatrix4x4_0 (0x402400, 147B)
// converts a GTE MATRIX to a matrix4x4.
static void ConvertMATRIXTomatrix4x4_0(MATRIX *m, matrix4x4 *out)
{
#ifdef SPIDEY_STANDALONE
	// 0x402400 (147 bytes): out is 16 floats. Row c (c = 0..2) is column c
	// of the 3x3 (m[0][c], m[1][c], m[2][c], 0) scaled by 1/4096, row 3 is
	// (t[0], t[1], t[2], 1).
	const i16* pM = reinterpret_cast<const i16*>(m);
	const i32* pT = reinterpret_cast<const i32*>(reinterpret_cast<const u8*>(m) + 0x14);
	f32* pOut = reinterpret_cast<f32*>(out);
	for (i32 c = 0; c < 3; c++)
	{
		for (i32 r = 0; r < 3; r++)
			pOut[c * 4 + r] = static_cast<f32>(pM[r * 3 + c]) * (1.0f / 4096.0f);
		pOut[c * 4 + 3] = 0.0f;
		pOut[12 + c] = static_cast<f32>(pT[c]);
	}
	pOut[15] = 1.0f;
#else
	ConvertMATRIXTomatrix4x4_fn f = (ConvertMATRIXTomatrix4x4_fn)0x00402400; f(m, out);
#endif
}

// @Bogus
// @FIXME forward to original: sub_470610 (0x470610, 20B), sets a GTE register.
static void gsub_470610(u16 v)
{
#ifdef SPIDEY_STANDALONE
	// 0x470610 (20 bytes): the GTE projection distance (ps2funcs.cpp
	// G_RTPS_PROJ_DISTANCE, 0x54F03C) and a 94.0f constant at 0x54F04C.
	*reinterpret_cast<u32*>(0x0054F04C) = 0x42BC0000;
	*reinterpret_cast<i32*>(0x0054F03C) = v;
#else
	gsub_470610_fn f = (gsub_470610_fn)0x00470610; f(v);
#endif
}

// @Bogus
// @FIXME forward to original: sub_46D5D0 (0x46D5D0, 69B), sets the GTE
// geometry offset from the camera transform.
static void gsub_46D5D0(i16 *m, i16 *t)
{
#ifdef SPIDEY_STANDALONE
	// 0x46D5D0 (69 bytes): 3x3 transpose, t[r][k] = m[k][r]
	print_if_false(m != t, "TransposeMatrix: source and destination are the same");
	for (i32 r = 0; r < 3; r++)
		for (i32 k = 0; k < 3; k++)
			t[r * 3 + k] = m[k * 3 + r];
#else
	gsub_46D5D0_fn f = (gsub_46D5D0_fn)0x0046D5D0; f(m, t);
#endif
}

// @Bogus
// @FIXME forward to original: sub_4021D0 (0x4021D0, 545B), normalizes a
// matrix4x4 (called on the camera transform matrix).
static void gsub_4021D0(matrix4x4 *m)
{
#ifdef SPIDEY_STANDALONE
	// 0x4021D0 (545 bytes), thiscall on the matrix: inverts a scaled rigid
	// transform in place. Rows 0..2 become the transposed 3x3 divided by
	// |row0|^2, row 3 becomes -(t * R') with w = 1. Side effect kept from
	// the original: the 4x4 is also copied to gGfxMatrix (0x56E668), the
	// matrix sub_402700 multiplies with. A zero row 0 yields the identity
	// (the original copies the identity global at 0x56E5F0).
	f32* M = reinterpret_cast<f32*>(m);
	f32 lenSq = M[0] * M[0] + M[1] * M[1] + M[2] * M[2];
	if (lenSq == 0.0f)
	{
		for (i32 i = 0; i < 16; i++)
			M[i] = (i % 5 == 0) ? 1.0f : 0.0f;
		return;
	}
	f32 inv = 1.0f / lenSq;
	f32 t[3] = { M[12], M[13], M[14] };
	f32 n[16];
	for (i32 r = 0; r < 3; r++)
	{
		for (i32 c = 0; c < 3; c++)
			n[r * 4 + c] = inv * M[c * 4 + r];
		n[r * 4 + 3] = 0.0f;
	}
	n[12] = 0.0f; n[13] = 0.0f; n[14] = 0.0f; n[15] = 1.0f;
	memcpy(M, n, 12 * sizeof(f32));
	memcpy(reinterpret_cast<void*>(0x0056E668), n, sizeof(n));   // gGfxMatrix = this
	for (i32 c = 0; c < 4; c++)
		M[12 + c] = -(t[0] * n[c] + t[1] * n[4 + c] + t[2] * n[8 + c]);
	M[15] = 1.0f;
#else
	gsub_4021D0_fn f = (gsub_4021D0_fn)0x004021D0; f(m);
#endif
}

static volatile i32 * const gM3dFadeTimer = (i32*)0x0065CFA4;
static volatile i32 * const gM3dFadeTimerPrev = (i32*)0x00660F88;
static volatile i32 * const gM3dTimerRelated = (i32*)0x006B4CA8;
static volatile i32 * const gM3dFadeFrames = (i32*)0x00660F84;
static volatile i32 * const gM3dFadeCount = (i32*)0x0064E558;
static volatile i32 * const gM3dFadeDist = (i32*)0x0064E568;
static volatile i32 * const gM3dFadeStep = (i32*)0x006191D8;
static volatile i32 * const gM3dFadeNear = (i32*)0x0064E560;
static volatile i32 * const gM3dFadeNearStep = (i32*)0x00628600;
static volatile u32 * const gM3dFadeColour = (u32*)0x00652F38;
static volatile i32 * const gM3dFogFlag = (i32*)0x0054D384;
static volatile i32 * const gM3dCameraPtr = (i32*)0x00628640;
static volatile i32 * const gM3dViewportPtr = (i32*)0x0064E514;
static volatile i32 * const gM3dRenderArg = (i32*)0x00660F68;
static volatile u16 * const gM3dPixelAspectX = (u16*)0x00654F58;
static volatile u16 * const gM3dPixelAspectY = (u16*)0x00654F5C;
static volatile i16 * const gM3dProjMatrix = (i16*)0x0065CEB8;
static volatile u8 * const gM3dRenderFlag = (u8*)0x00660FE8;
static volatile i32 * const gM3dCamOffsetX = (i32*)0x00660730;
static volatile i32 * const gM3dCamOffsetY = (i32*)0x00660734;
static volatile i32 * const gM3dCamOffsetZ = (i32*)0x00660738;
static volatile f32 * const gM3dProjNear = (f32*)0x00550078;
static volatile f32 * const gM3dProjFar = (f32*)0x0055007C;
static volatile f32 * const gM3dProjScale = (f32*)0x00550080;
static volatile f32 * const gM3dProjConst = (f32*)0x00550064;
static volatile u8 * const gM3dObjFileRegion = (u8*)0x006B3824;
static volatile u8 * const gM3dRegionTwo = (u8*)0x006B4678;
static volatile i32 * const gM3dCamFocusX = (i32*)0x005FCDA8;

// @Ok
// (0x00472DC0, 2605 bytes). Sets up the 3D render for a frame: advances the
// fade timer, sets fog params, computes the viewport projection matrix from
// the SViewport (xL/xR/yT/yB/Zoom/Hither/Yon), sets the GTE rotation and
// geometry offset from the camera, preprocesses pulsing colours and wibble
// textures, builds the final projection matrix, and calls PCGfx_RenderInit.
void M3d_RenderSetup(SCamera *pCam, SViewport *pView, u32 *a3)
{
	*gM3dFadeTimerPrev = *gM3dFadeTimer;
	bool timerChanged = (*gM3dFadeTimer == *gM3dTimerRelated);
	*gM3dFadeTimer = *gM3dTimerRelated;
	if (!timerChanged)
		(*gM3dFadeFrames)++;

	if (*gM3dFadeCount > 0)
	{
		(*gM3dFadeDist) += *gM3dFadeStep;
		(*gM3dFadeCount)--;
		(*gM3dFadeNear) += *gM3dFadeNearStep;
		u32 c = *gM3dFadeColour;
		u32 v54 = ((u8)(c >> 16) << 16) + ((c >> 8) & 0xFF) + (c & 0xFF00FF00);
		if ((v54 & 0xFFFFFF) == 0xFFFFFF)
		{
			*gM3dFogFlag = 1;
			f32 nearF = (f32)(*gM3dFadeNear) * 100.0f;
			f32 farF = (f32)(*gM3dFadeDist) * 100.0f;
			PCGfx_SetFogParams(farF, nearF, v54);
		}
		else
		{
			*gM3dFogFlag = 0;
			f32 nearF = (f32)(*gM3dFadeNear) * 0.98f;
			f32 farF = (f32)(*gM3dFadeDist) * 0.98f;
			PCGfx_SetFogParams(farF, nearF, v54);
		}
	}

	*gM3dCameraPtr = (i32)pCam;
	*gM3dViewportPtr = (i32)pView;
	*(u16*)((char*)pView + 0x0A) = (u16)(*gM3dFadeNear);
	*gM3dRenderArg = (i32)a3;

	u32 *v5 = G_PPOLY;
	if (gPrintStubbed == 0)
		stubbed_printf((char*)"stubbed out: SetDrawArea");
	G_PPOLY = v5 + 12;
	if (gPrintStubbed == 0)
		stubbed_printf((char*)G_RENDER_BUF);

	u16 xL = *(u16*)((char*)pView + 0x00);
	u16 yB = *(u16*)((char*)pView + 0x02);
	u16 xR = *(u16*)((char*)pView + 0x04);
	u16 yT = *(u16*)((char*)pView + 0x06);
	u16 vpHither = *(u16*)((char*)pView + 0x08);
	u16 vpYon = *(u16*)((char*)pView + 0x0A);
	u16 zoom = *(u16*)((char*)pView + 0x0C);
	i32 v7 = (((u32)xL - xR) << 11) & 0xFFFFF000;
	i32 v8 = xL + xR;
	v7 = (v7 & 0xFFFF0000) | ((((v7 / zoom) << 12) / *gM3dPixelAspectY) & 0xFFFF);
	u16 fieldE = (u16)v7;
	*(u16*)((char*)pView + 0x10) = (u16)(v8 >> 1);
	*(u16*)((char*)pView + 0x0E) = fieldE;
	*(u16*)((char*)pView + 0x12) = (u16)((yB + yT) >> 1);

	volatile i16 *pm = gM3dProjMatrix;
	pm[0] = 0;
	pm[1] = 0;
	pm[2] = -4096;
	pm[4] = 0;
	pm[3] = vpYon;
	pm[5] = 0;
	pm[6] = 4096;
	pm[7] = -vpHither;

	// 0x472FC4..0x4731C6: six padded normals, then four debug normals.
	u32 v11 = *gM3dPixelAspectX * fieldE;
	u32 v12 = ((u32)xL + 0x1FFFFF * xR) << 11;
	u32 v13 = M3dMaths_SquareRoot0((v11 >> 12) * (v11 >> 12) + (v12 >> 12) * (v12 >> 12));
	pm[8] = 0;
	pm[11] = 0;
	pm[12] = 0;
	pm[9] = v11 / v13;
	pm[13] = -pm[9];
	pm[10] = v12 / v13;
	pm[14] = v12 / v13;
	pm[15] = 0;

	u32 v14 = *gM3dPixelAspectY * fieldE;
	u32 v15 = ((u32)xL + 0x1FFFFF * xR) << 11;
	u32 v16 = M3dMaths_SquareRoot0((v14 >> 12) * (v14 >> 12) + (v15 >> 12) * (v15 >> 12));
	pm[17] = 0;
	pm[19] = 0;
	pm[21] = 0;
	pm[23] = 0;
	pm[16] = v14 / v16;
	pm[20] = -pm[16];
	pm[18] = v15 / v16;
	pm[22] = v15 / v16;

	v14 = fieldE;
	i32 v17 = (yB - yT) >> 1;
	u32 v18 = M3dMaths_SquareRoot0(v17 * v17 + v14 * v14);
	pm[24] = 0;
	pm[27] = 0;
	pm[28] = 0;
	pm[31] = 0;
	pm[25] = (v14 << 12) / v18;
	pm[29] = -pm[25];
	pm[26] = (static_cast<u32>(v17) << 12) / v18;
	pm[30] = (static_cast<u32>(v17) << 12) / v18;

	i32 v19 = (xL - xR) >> 1;
	v14 = fieldE;
	u32 v20 = M3dMaths_SquareRoot0(v19 * v19 + v14 * v14);
	pm[33] = 0;
	pm[35] = 0;
	pm[37] = 0;
	pm[39] = 0;
	pm[32] = (v14 << 12) / v20;
	pm[36] = -pm[32];
	pm[34] = (static_cast<u32>(v19) << 12) / v20;
	pm[38] = (static_cast<u32>(v19) << 12) / v20;

	gte_SetRotMatrix(&pCam->Transform);
	SVECTOR *v21 = (SVECTOR*)(pm + 12);
	i32 v70 = 0;
	while ((int)v21 < (int)(pm + 24))
	{
		gte_ldv0(v21 - 3);
		gte_rtv0();
		gte_stsv((SVECTOR*)((char*)0x00628648 + v70));
		gte_ldv0(v21);
		gte_rtv0();
		gte_stsv((SVECTOR*)((char*)0x00628620 + v70));
		v21++;
		v70 += 6;
	}
	gsub_470610(fieldE);
	if (gPrintStubbed == 0)
		stubbed_printf((char*)"stubbed out: SetGeomOffset");
	gsub_46D5D0((i16*)pCam->Transform.m, (i16*)((char*)pCam + sizeof(SCamera)));

	i16 *p_pad = (i16*)((char*)pCam + sizeof(SCamera) + 0x20);
	for (i32 i = 3; i != 0; --i)
	{
		*p_pad = (*gM3dPixelAspectY * *(i16*)((char*)p_pad - 32)) >> 12;
		*(i16*)((char*)p_pad + 6) = (*gM3dPixelAspectX * *(i16*)((char*)p_pad - 26)) >> 12;
		*(i16*)((char*)p_pad + 12) = *(i16*)((char*)p_pad - 20);
		p_pad = (i16*)((char*)p_pad + 2);
	}

	M3d_PreprocessPulsingColours(EnvRegions[0]);
	M3d_PreprocessPulsingColours(EnvRegions[1]);
	M3d_PreprocessPulsingColours(*gM3dObjFileRegion);
	M3d_PreprocessPulsingColours(*gM3dRegionTwo);
	M3d_PreprocessWibblyTextures(*gM3dObjFileRegion);

	static volatile i16 * const gM3dGeomOffX = (i16*)0x00628608;
	static volatile i16 * const gM3dGeomOffY = (i16*)0x0062860A;
	static volatile i16 * const gM3dGeomOffZ = (i16*)0x0062860C;
	static volatile i16 * const gM3dGeomOffW = (i16*)0x0062860E;
	*gM3dGeomOffX = -*(i16*)((char*)*gM3dCameraPtr + 58);
	*gM3dGeomOffY = -*(i16*)((char*)*gM3dCameraPtr + 60);
	*gM3dGeomOffZ = -*(i16*)((char*)*gM3dCameraPtr + 62);
	*gM3dGeomOffW = (*gM3dCamFocusX >> 12) - *(i16*)((char*)*gM3dCameraPtr + 8);

	PCGfx_UseTexture(-1, DCGfx_BlendingMode_0);
	PCGfx_UseTexture(1, DCGfx_BlendingMode_0);

	if (*gM3dRenderFlag != 0)
	{
		pCam->Transform.t[0] = pCam->Position.vx;
		pCam->Transform.t[1] = pCam->Position.vy;
		pCam->Transform.t[2] = pCam->Position.vz;
	}
	pCam->Transform.t[0] += *gM3dCamOffsetX;
	pCam->Transform.t[1] = *gM3dCamOffsetY + pCam->Transform.t[1];
	pCam->Transform.t[2] = *gM3dCamOffsetZ + pCam->Transform.t[2];

	// 0x472DC0: the camera transform becomes a matrix4x4 at 0x56E738, is
	// copied to 0x56E778 (shell.cpp: gComicCamMatrix) and inverted there in
	// place; the frame's final matrix is that inverse times the projection
	// matrix kept at 0x56E570 (shell.cpp: gComicViewMatrix). The previous
	// version converted into a local it never read, inverted a stale copy of
	// 0x56E778 and multiplied the two the other way round, so the camera
	// never reached the renderer (the standalone drew the level upside down
	// from the world origin, 2026-09-03).
	static matrix4x4 * const gM3dCamMatrix = (matrix4x4*)0x0056E738;
	static matrix4x4 * const gM3dCamInvMatrix = (matrix4x4*)0x0056E778;
	static matrix4x4 * const gM3dProjMatrix = (matrix4x4*)0x0056E570;

	ConvertMATRIXTomatrix4x4_0(&pCam->Transform, gM3dCamMatrix);
	memcpy(gM3dCamInvMatrix, gM3dCamMatrix, sizeof(matrix4x4));
	gsub_4021D0(gM3dCamInvMatrix);

	f32 a1a = (f32)G_GAME_RESOLUTION_X / (f32)G_XRES;
	f32 a1b = (f32)G_GAME_RESOLUTION_Y / (f32)G_YRES;
	// 0x472DC0 reads the viewport as u16[]: the projection's left edge is
	// [2] (+4, the field this function calls xR) and its width is [0] - [2]
	// (+0 minus +4). With the two swapped the x scale came out negative and
	// the standalone drew the world mirrored and upside down.
	f32 left = (f32)xR * a1a;
	f32 right = (f32)(xL - xR) * a1a;
	f32 top = (f32)yT * a1b;
	f32 bottom = (f32)(yB - yT) * a1b;
	f32 yon = (f32)vpYon;
	f32 hither = (f32)vpHither;
	f32 invRange = yon / (yon - hither);
	f32 zoomF = (f32)zoom;
	f32 v91 = *gM3dProjConst * 4096.0f / zoomF;
	f32 v94 = -(invRange * hither);
	f32 v83 = right * 0.5f;
	f32 v72 = bottom * 0.5f;
	f32 v76 = left + v83;
	f32 v92 = top + v72;
	f32 v84 = v83 * v91;

	matrix4x4 v95 = matrix4x4(v84, 0, 0, 0, 0, v84, 0, 0, v76, v92, invRange, 1, 0, 0, v94, 0);

	// four vector4d::operator= (0x402600) row copies in the original
	memcpy(gM3dProjMatrix, &v95, sizeof(matrix4x4));

#ifdef SPIDEY_STANDALONE
	const char* fullMapOption = getenv("SPIDEY_FULL_MAP");
	bool fullMap = !fullMapOption || atoi(fullMapOption) != 0;
	// Keep all possible fixed-point world positions inside the depth range.
	PCGfx_RenderInit(hither, fullMap ? 2097152.0f : yon, (f32)fieldE);
	// Keep the original world-space offsets for decals and model parts.
	extern f32 gRenderInitTwo[2];
	gRenderInitTwo[1] = (yon - hither) / 4096.0f;
#else
	PCGfx_RenderInit(hither, yon, (f32)fieldE);
#endif

	static matrix4x4 * const gM3dFinalProjMatrix = (matrix4x4*)0x0056E6F8;
	gsub_476A00(gM3dFinalProjMatrix, gM3dCamInvMatrix, gM3dProjMatrix);

	i32 result = *gM3dFogFlag;
	if (*gM3dFogFlag != 0)
	{
		i32 v45 = *gM3dFadeDist;
		if (v45 < 10)
		{
			v45 = 10;
			*gM3dFadeDist = 10;
		}
		result = *gM3dFadeNear;
		if (*gM3dFadeNear < 15)
		{
			result = 15;
			*gM3dFadeNear = 15;
		}
		if (result <= v45)
			*gM3dFadeNear = v45 + 5;
		*gM3dProjNear = 1.0f / (f32)(*gM3dFadeNear);
		*gM3dProjFar = 1.0f / (f32)(*gM3dFadeDist);
		*gM3dProjScale = 255.0f / (*gM3dProjFar - *gM3dProjNear);
	}
	(void)result;
#ifdef SPIDEY_STANDALONE
	// Full-map mode also disables the original distance alpha fade.
	// Keeping a model past the cull is not enough if its alpha becomes zero.
	if (fullMap)
		*gM3dFogFlag = 0;
#endif
}

// @Ok
// (0x004024A0, confirmed real name ConvertSMatrixTomatrix4x4 in the
// maintainer's IDB.) Traced instruction-by-instruction this session as the
// prerequisite leaf for RenderSuperItem below (see its comment for the
// re-investigation that found this was the real remaining blocker, not a
// CSuper layout gap). Output row r (r=0..2) = source rotation COLUMN r
// (i.e. transposed) scaled by 1/4096 (fixed-point), with a 0 in column 3;
// output row 3 = the raw (unscaled) translation with 1.0 in column 3. This
// matches the row-vector*matrix convention gsub_476A00/matrix4x4 already
// use elsewhere in this file. Traced concretely: for each source row index
// i (0,1,2), out.field_0[i] = { m[0][i], m[1][i], m[2][i], 0 } / (4096 for
// the first 3) and out.field_0[3] = { t[0], t[1], t[2], 1.0f } (not
// per-row -- the translation is written once, row 3, in the same pass).
void ConvertSMatrixTomatrix4x4(SMatrix const* pIn, matrix4x4* pOut)
{
	for (i32 row = 0; row < 3; row++)
	{
		pOut->field_0[row].field_0[0] = (f32)pIn->m[0][row] * 0.00024414062f;
		pOut->field_0[row].field_0[1] = (f32)pIn->m[1][row] * 0.00024414062f;
		pOut->field_0[row].field_0[2] = (f32)pIn->m[2][row] * 0.00024414062f;
		pOut->field_0[row].field_0[3] = 0.0f;
	}

	pOut->field_0[3].field_0[0] = (f32)pIn->t[0];
	pOut->field_0[3].field_0[1] = (f32)pIn->t[1];
	pOut->field_0[3].field_0[2] = (f32)pIn->t[2];
	pOut->field_0[3].field_0[3] = 1.0f;
}

typedef void (*gsub_46E4B0_fn)(MATRIX*, MATRIX*);
typedef i32 (*ValidMATRIX_fn)(MATRIX*);

// @Bogus
// @FIXME forward to original: sub_46E4B0 (0x46E4B0). The PS2 source calls
// this M3dMaths_MulRotMatrix(&src, &dest): multiplies src by the GTE's
// current rotation matrix (set by gte_SetRotMatrix just before) and writes
// the result to dest. Not in tools/names.json under any name and it lives
// in ps2funcs.cpp's address range, which this branch does not own, so it is
// forwarded here rather than implemented.
static void gsub_46E4B0(MATRIX *pSrc, MATRIX *pDest)
{
#ifdef SPIDEY_STANDALONE
	// 0x46E4B0 (24 bytes): MulMatrix0(&gRotMatrix, pSrc, pDest)
	MulMatrix0(reinterpret_cast<MATRIX*>(0x00610B20), pSrc, pDest);
#else
	gsub_46E4B0_fn f = (gsub_46E4B0_fn)0x0046E4B0; f(pSrc, pDest);
#endif
}

// @Bogus
// @FIXME forward to original: ValidMATRIX (0x470320, named in
// tools/names.json). Row-magnitude sanity check on a MATRIX; its result
// only feeds a print_if_false here, so it gates no real behaviour.
static i32 ValidMATRIX(MATRIX *pMatrix)
{
#ifdef SPIDEY_STANDALONE
	// 0x470320 (265 bytes): the three row lengths (in 1/4096 units) must be
	// within a factor of two of their predecessor: 0.5*len[i] <= len[i-1]
	// <= 2*len[i]. Only used by asserts.
	const i16* pM = reinterpret_cast<const i16*>(pMatrix);
	f32 len[3];
	for (i32 r = 0; r < 3; r++)
	{
		f32 x = pM[r * 3] * (1.0f / 4096.0f);
		f32 y = pM[r * 3 + 1] * (1.0f / 4096.0f);
		f32 z = pM[r * 3 + 2] * (1.0f / 4096.0f);
		len[r] = sqrtf(x * x + y * y + z * z);
	}
	for (i32 i = 1; i < 3; i++)
	{
		if (len[i % 3] * 0.5f > len[i - 1])
			return 0;
		if (len[i % 3] * 2.0f < len[i - 1])
			return 0;
	}
	return 1;
#else
	ValidMATRIX_fn f = (ValidMATRIX_fn)0x00470320; return f(pMatrix);
#endif
}

// ---------------------------------------------------------------------
// RenderSuperItem (0x00474C10) globals. Same sourcing as the M3d_Render
// block above: the PS2 original is thps2-stuff/m3d.mik line 2285, and the
// maintainer's IDB (idbs/idb_globals.txt) confirms MechList,
// gPsxSpArmorIndex, gTimerRelated, gFloatSuperRelated and
// gSuperItemRelated at these addresses.
// ---------------------------------------------------------------------

static CItem ** const gM3dMechList        = (CItem**)0x006A9038; // IDB: MechList
static u8 * const gM3dCostumeRegionIndex  = (u8*)0x006B4679;     // spidey.cpp: gCurrentCostumeRegionIndex
static i32 * const gM3dPulsatingHead      = (i32*)0x0060CFF0;    // pshell.cpp: G_PULSATING_HEAD_FLAG
static i32 * const gM3dArmorRegionIndex   = (i32*)0x0054AE18;    // IDB: gPsxSpArmorIndex
static i32 * const gM3dPulseTimer         = (i32*)0x006B4CA8;    // IDB: gTimerRelated
static i32 * const gM3dSuperScaleEnabled  = (i32*)0x005500A4;    // IDB: gSuperItemRelated
static f32 * const gM3dSuperScale         = (f32*)0x0054FFE0;    // IDB: gFloatSuperRelated (ob.h)
static f32 * const gM3dSuperScaleDist     = (f32*)0x0055009C;    // 55.0f in the image
static f32 * const gM3dSuperScaleDistLow  = (f32*)0x005500A0;    // 20.0f in the image
static i32 * const gM3dSuperFixedScale    = (i32*)0x00550074;    // == gDCFixedScaleConst
static i32 * const gM3dUseSuperScale      = (i32*)0x00660F74;    // == gDCUseSuperScale
static i32 * const gM3dFogEnabled         = (i32*)0x0054D384;    // == gM3dFogFlag / gDCFogEnabled
static f32 * const gM3dFogDistScale       = (f32*)0x00550098;    // 1.1f in the image
static i32 * const gM3dFogAlpha           = (i32*)0x00550060;    // 0..255, read by DCModel_RenderModel
static i32 * const gM3dFogNear            = (i32*)0x0064E560;    // m3dinit.cpp: gFogNear
static f32 * const gM3dLodScale           = (f32*)0x00550094;    // 1.0f in the image
static i32 * const gM3dSubObjectMasked    = (i32*)0x0065DFAC;    // PS2: SubObjectMasked
static i32 * const gM3dLightEveryPart     = (i32*)0x00660FF0;    // 0 = only part 0 gets the dynamic-light flag
// Reset-per-superitem output cursors. DCModel_RenderModel declares three of
// these addresses itself (gDCStitchPositionTable, gDCAttachPointCursor,
// gDCLitColorOutCursor) as if they were the buffers; here they are clearly
// POINTERS that get re-pointed at the buffers below, which matches the PS2
// source's "FreeStitchVertex = &ProjectedVertices[MAXVERTICES-1]" reset.
static u32 * const gM3dStitchVertexCursor = (u32*)0x0062861C;
static u32 * const gM3dAttachPointCursor  = (u32*)0x0064F5CC;
static u32 * const gM3dLitColourCursor    = (u32*)0x0065DFA8;
static u32 * const gM3dStitchColourCursor = (u32*)0x0064E55C;
static u32 * const gM3dStitchColourStart  = (u32*)0x00550028;   // holds 0x0064F5D8
// Body-proportion cheats. Both default to 0 in the image and are only read
// when the item is in the player's costume region. A scales every part DOWN
// (head 0.7, limbs 0.35, rest 0.25), B scales the head/limbs UP (1.5) and
// the rest down (part 1 0.6, rest 0.45). Named from what the maths does; I
// could not find either address in the maintainer's IDB.
static i32 * const gM3dBodyScaleCheatA    = (i32*)0x02E09BF4;
static i32 * const gM3dBodyScaleCheatB    = (i32*)0x02E09BF0;

// @Ok
// (0x00474C10, 4752 bytes.) Renders one animated multi-part character:
// per-part pose matrix, LOD, lighting and the final model submit.
//
// Reconstructed against the PS2 original (thps2-stuff/m3d.mik line 2285),
// which lines up statement for statement with the disassembly apart from
// the PC-specific parts noted below. Extra flag values recovered from that
// pairing: EXTRAFLAGS_FLIPANIM 0x2, EXTRAFLAGS_OVERRIDEWATERLEVEL 0x8,
// ITEMFLAGS_POSED 0x4. CSuper::field_156 is the PS2's mWaterLevel and
// CSuper::field_194/field_198 are mSubObjectDisplayMask/2.
//
// Differences from the PS2 version, all confirmed in the disassembly:
//  - the Fast path, the Dewibble (near-object x16) path, the reflection
//    mapping branch, the ellipsoid/shadow pass and DrawModelOutline are all
//    gone; the PC always builds a float matrix4x4 per part and submits it
//    to DCModel_RenderModel / DC_PSXModel_RenderModel.
//  - a PC-only distance fade replaces the PSX depth cue: past gFogNear the
//    whole superitem is dropped (an early return that skips the flip-anim
//    and water-level restores at the end -- reproduced as the original has
//    it), and between the two limits a 0..255 alpha goes to 0x550060.
//  - a PC-only "super scale" (0x54FFE0), a per-part LOD that steps ONCE
//    using ppModels[0]->NextLOD as a fixed index stride (the PS2 walks a
//    NextLOD chain instead), and the three body-proportion cheats.
//  - lighting is converted to floats for the software/D3D renderer exactly
//    the way M3d_Render does it (same tables, same transpose on the colour
//    matrix).
//
// One thing I could not resolve from the disassembly: the matrix4x4
// temporary that receives partMatrix*partScale is constructed with a `this`
// register (ecx) that is never reloaded before the call, so it still holds
// whatever the previous matrix4x4 constructor left there. The surrounding
// code proves the 1/16 scale matrix must survive that construction (the
// body-scale cheat paths reuse it afterwards without rebuilding it), so a
// separate temporary is what the maths needs, and that is what is written
// here.
void RenderSuperItem(CItem *pItem, bool a2)
{
	CSuper *pSuper = static_cast<CSuper*>(pItem);
	u16 itemFlags = pItem->mFlags;
	bool isMechList = (pItem == *gM3dMechList);

	f32 posX = static_cast<f32>(pItem->mPos.vx) * 0.00024414062f;
	f32 posY = static_cast<f32>(pItem->mPos.vy) * 0.00024414062f;
	f32 posZ = static_cast<f32>(pItem->mPos.vz) * 0.00024414062f;

	*gM3dLitColourCursor    = 0x0064F5D8;
	*gM3dStitchColourCursor = *gM3dStitchColourStart;

	matrix4x4 itemTransform;
	ConvertMATRIXTomatrix4x4_0(&pSuper->mTransform, &itemTransform);
	itemTransform.field_0[3].field_0[0] = posX;
	itemTransform.field_0[3].field_0[1] = posY;
	itemTransform.field_0[3].field_0[2] = posZ;
	itemTransform.field_0[3].field_0[3] = 1.0f;

	i32 *pCamera = reinterpret_cast<i32*>(*gM3dCameraPtrEarly);

	// PS2: symbiotes melting into the ground need their own water level, so
	// the global gets overridden for this item and restored at the end.
	i16 savedWaterNormalPad = 0;
	if ((pSuper->mExtraFlags & 8) != 0)
	{
		savedWaterNormalPad = *gM3dWaterNormalPad;
		*gM3dWaterNormalPad = static_cast<i16>(pSuper->field_156 - static_cast<i16>(pCamera[2]));
	}

	i32 region = pItem->mRegion;
	bool isCostumeRegion = (region == static_cast<i32>(*gM3dCostumeRegionIndex));
	bool pulsatingHead = false;
	if (isCostumeRegion && *gM3dPulsatingHead != 0)
		pulsatingHead = true;

	// the armoured player costume lives in its own PSX region
	if (isMechList && G_LOWGRAPHICS == 0 && *(reinterpret_cast<u8*>(pItem) + 1513) != 0)
		region = *gM3dArmorRegionIndex;

	print_if_false(G_PSXREGION[region].Usable != 0, "Tried to render superitem\twithout PSX");

	if ((pSuper->mExtraFlags & 2) != 0)
	{
		pSuper->mTransform.m[0][0] = -pSuper->mTransform.m[0][0];
		pSuper->mTransform.m[1][0] = -pSuper->mTransform.m[1][0];
		pSuper->mTransform.m[2][0] = -pSuper->mTransform.m[2][0];
		*gM3dScratchReflected = 1;
	}
	else
	{
		*gM3dScratchReflected = 0;
	}

	// superitem transform in eye coords, then its local origin
	gte_SetRotMatrix(reinterpret_cast<MATRIX*>(pCamera + 29));

	MATRIX superTransform;
	gsub_46E4B0(&pSuper->mTransform, &superTransform);

	VECTOR posnRelCam;
	posnRelCam.vx = (pItem->mPos.vx >> 12) - pCamera[1];
	posnRelCam.vy = (pItem->mPos.vy >> 12) - pCamera[2];
	posnRelCam.vz = (pItem->mPos.vz >> 12) - pCamera[3];
	gte_ldlvl(&posnRelCam);
	gte_rtir();
	gte_stlvnl(reinterpret_cast<VECTOR*>(superTransform.t));

	i32 superDist = superTransform.t[2];

	if (*gM3dFogEnabled != 0)
	{
		f32 dist = static_cast<f32>(superDist) * *gM3dFogDistScale;
		if (dist > static_cast<f32>(*gM3dDpqMin))
		{
			if (dist <= static_cast<f32>(*gM3dFogNear))
				return;

			*gM3dNoFogFlagEarly = 1;

			f32 range = static_cast<f32>(*gM3dFogNear - *gM3dDpqMin);
			if (range < 1.0f)
				*gM3dFogAlpha = 255;
			else
				*gM3dFogAlpha = static_cast<i32>((static_cast<f32>(*gM3dFogNear) - dist) * 255.0f / range);
		}
	}

	*gM3dUseSuperScale = 1;
	if (*gM3dSuperScaleEnabled != 0 && G_LOWGRAPHICS == 0)
	{
		f32 dist = static_cast<f32>(superDist);

		if ((dist - *gM3dSuperScaleDist) < 10.0f || *gM3dSuperScaleDist < 1.0f)
			*gM3dSuperScale = 1.0f;
		else
			*gM3dSuperScale = *gM3dSuperScaleDist / dist + 1.0f;

		if (isMechList)
		{
			if (*(reinterpret_cast<u8*>(*gM3dMechList) + 2281) != 0 && posnRelCam.vy > 0)
			{
				if ((*gM3dSuperScaleDistLow + 10.0f) > dist || *gM3dSuperScaleDistLow < 1.0f)
					*gM3dSuperScale = 1.0f;
				else
					*gM3dSuperScale = 1.0f - *gM3dSuperScaleDistLow / dist;
			}
			*gM3dSuperFixedScale = *reinterpret_cast<i32*>(gM3dSuperScale);
		}
	}

	print_if_false(pItem->mModel == 0, "I thought the model number was always 0 for a superitem");

	SModel **ppModels = G_PSXREGION[region].ppModels;
	u32 numParts = G_PSXREGION[region].NumParts;

	SMatrix *pAnimTransform;
	if ((itemFlags & 4) != 0)
		pAnimTransform = pSuper->mpPoseBuffer;
	else
		pAnimTransform = Decomp_GetAnimTransform(pSuper);
	SMatrix *pFirstTransform = pAnimTransform;

	// PS2: SuperLightTransform. The PC computes it and never reads it back
	// (M3dAsm_SetSuperTransforms, its only consumer, is gone), kept because
	// the original still makes the call.
	MATRIX superLightTransform;
	MulMatrix0(reinterpret_cast<MATRIX*>(pItem->mpLight), &pSuper->mTransform, &superLightTransform);

	// PS2: Type = mEllipsoidRGB >> 24. The PC keeps only the top bit, as a
	// "do not draw this superitem's parts" flag. CItem+0xB0 is inside a
	// PADDING run in ob.h's CBody, so it is read through its raw offset.
	u32 partType = *reinterpret_cast<u32*>(reinterpret_cast<u8*>(pItem) + 0xB0) >> 24;
	f32 lodScale = *gM3dLodScale;

	*gM3dStitchVertexCursor = 0x00654FB8;
	*gM3dAttachPointCursor  = 0x00658E28;
	// 0x474C86: the lit-colour output cursor restarts at the stitch colour
	// table (gDCStitchColorIndexTable, 0x64F5D8) for every superitem.
	*gM3dLitColourCursor    = 0x0064F5D8;

	// the player's pose has two pairs of parts swapped while it renders
	SMatrix swapTemp;
	if (isMechList)
	{
		swapTemp = pFirstTransform[5];
		pFirstTransform[5] = pFirstTransform[6];
		pFirstTransform[6] = swapTemp;
		swapTemp = pFirstTransform[10];
		pFirstTransform[10] = pFirstTransform[11];
		pFirstTransform[11] = swapTemp;
	}

	for (u32 part = 0; part < numParts; part++, pAnimTransform++)
	{
		u32 modelIndex = part;

		if (part <= 31)
			*gM3dSubObjectMasked = ((pSuper->field_194 & (1 << part)) != 0) ? 1 : 0;
		else
			*gM3dSubObjectMasked = ((pSuper->field_198 & (1 << (part - 32))) != 0) ? 1 : 0;

		SModel *pModel = ppModels[part];

		if (G_LOWGRAPHICS == 0)
		{
			i32 zoom = *reinterpret_cast<u16*>(G_VIEW_CLIP_INFO + 0x0E);
			i32 nearThreshold = static_cast<i32>(
					static_cast<f32>(pModel->zMax * zoom / 191) * lodScale);

			// one step only: for a superitem the lower-detail part models
			// sit at a fixed index offset, held in part 0's NextLOD
			if (static_cast<i16>(superDist) > static_cast<i16>(nearThreshold)
					&& pModel->NextLOD != 0xFFFF)
			{
				modelIndex = part + ppModels[0]->NextLOD;
				pModel = ppModels[modelIndex];
			}

			if (static_cast<i16>(superDist) > pModel->zMax * zoom / 191
					&& pModel->NextLOD == 0xFFFF)
				break;
		}

		MATRIX worldTransform;
		MulMatrix0(&pSuper->mTransform, reinterpret_cast<MATRIX*>(pAnimTransform), &worldTransform);

		*gM3dLightingEnabled = 0;
		*gM3dLightsAreDynamic = 0;

		if ((pModel->Flags & 4) != 0 || (itemFlags & 0x80) != 0)
		{
			M3dAsm_LoadClipTableB(pItem->mpLight->ColorMatrix);

			SLight *pLight = pItem->mpLight;

			*gM3dLightingEnabled = 1;
			*gM3dLightsAreDynamic = (*gM3dDynamicLightSource != 0) ? 1 : 0;
			if (part > 0 && *gM3dLightEveryPart == 0)
				*gM3dLightsAreDynamic = 0;

			MATRIX lightTransform;
			MulMatrix0(reinterpret_cast<MATRIX*>(pLight), &worldTransform, &lightTransform);

			for (i32 light = 0; light < 3; light++)
			{
				gM3dLightDirTable[3 * light + 0] = static_cast<f32>(lightTransform.m[light][0]) / 4096.0f;
				gM3dLightDirTable[3 * light + 1] = static_cast<f32>(lightTransform.m[light][1]) / 4096.0f;
				gM3dLightDirTable[3 * light + 2] = static_cast<f32>(lightTransform.m[light][2]) / 4096.0f;

				// same transpose as in M3d_Render: read by column
				gM3dLightColorTable[3 * light + 0] = static_cast<f32>(pLight->ColorMatrix[0][light]) / 4096.0f;
				gM3dLightColorTable[3 * light + 1] = static_cast<f32>(pLight->ColorMatrix[1][light]) / 4096.0f;
				gM3dLightColorTable[3 * light + 2] = static_cast<f32>(pLight->ColorMatrix[2][light]) / 4096.0f;
			}

			*gM3dAmbientB = static_cast<f32>(pLight->BackColor[2]) / 4096.0f;
			*gM3dAmbientG = static_cast<f32>(pLight->BackColor[1]) / 4096.0f;
			*gM3dLightCount = 3;
			*gM3dAmbientR = static_cast<f32>(pLight->BackColor[0]) / 4096.0f;

			if (*gM3dLightOverride != 0)
			{
				f32 backX = -gM3dLightDirTable[0];
				f32 backY = -gM3dLightDirTable[1];
				f32 backZ = -gM3dLightDirTable[2];

				gM3dLightColorTable[0] = gM3dOverrideLightRgb[0];
				gM3dLightColorTable[1] = gM3dOverrideLightRgb[1];
				gM3dLightColorTable[2] = gM3dOverrideLightRgb[2];

				gM3dLightDirTable[3] = backX;
				gM3dLightDirTable[4] = backY;
				gM3dLightDirTable[5] = backZ;
				gM3dLightDirTable[6] = backX;
				gM3dLightDirTable[7] = backY;
				gM3dLightDirTable[8] = backZ;

				gM3dLightColorTable[3] = 0.0f;
				gM3dLightColorTable[4] = 0.0f;
				gM3dLightColorTable[5] = 0.0f;
				gM3dLightColorTable[6] = 0.0f;
				gM3dLightColorTable[7] = 0.0f;
				gM3dLightColorTable[8] = 0.0f;

				*gM3dAmbientR = gM3dOverrideAmbient[0];
				*gM3dAmbientG = gM3dOverrideAmbient[1];
				*gM3dAmbientB = gM3dOverrideAmbient[2];
			}
		}

		print_if_false(ValidMATRIX(reinterpret_cast<MATRIX*>(pAnimTransform)) != 0, "Invalid pAnimTransform.");
#ifdef SPIDEY_STANDALONE
		// SPIDEY_TRACE_PARTS=1: report part matrices that look broken
		if (getenv("SPIDEY_TRACE_PARTS"))
		{
			const i16* pm = &pAnimTransform->m[0][0];
			i32 bad = 0;
			for (i32 q = 0; q < 9; q++)
				if (pm[q] > 5000 || pm[q] < -5000) bad = 1;
			if (pAnimTransform->t[0] > 20000 || pAnimTransform->t[0] < -20000 || pAnimTransform->t[1] > 20000 || pAnimTransform->t[1] < -20000 || pAnimTransform->t[2] > 20000 || pAnimTransform->t[2] < -20000) bad = 2;
			if (pm[0] == 0 && pm[1] == 0 && pm[2] == 0 && pm[3] == 0 && pm[4] == 0 && pm[5] == 0) bad = 3;
			if (bad)
			{
				static i32 nbad = 0;
				if ((++nbad % 20) == 1)
					fprintf(stderr, "BADPART kind=%d region=%d part=%d/%d anim=%d frame=%d frac=%d mode=%d numFrames=%d flags4=%d m=[%d %d %d | %d %d %d | %d %d %d] t=(%d,%d,%d)\n", bad, region, part, numParts,
						pSuper->mAnim, pSuper->mFrame, pSuper->mFrameFrac, pSuper->mAnimMode, pSuper->mNumFrames, (itemFlags & 4) != 0,
						pm[0], pm[1], pm[2], pm[3], pm[4], pm[5], pm[6], pm[7], pm[8], pAnimTransform->t[0], pAnimTransform->t[1], pAnimTransform->t[2]);
			}
		}
#endif

		matrix4x4 partMatrix;
		ConvertSMatrixTomatrix4x4(pAnimTransform, &partMatrix);

		// the pose matrices are in 1/16 units
		matrix4x4 partScale(
				0.0625f, 0.0f, 0.0f, 0.0f,
				0.0f, 0.0625f, 0.0f, 0.0f,
				0.0f, 0.0f, 0.0625f, 0.0f,
				0.0f, 0.0f, 0.0f, 1.0f);

		matrix4x4 partWorld;
		gsub_476A00(&partWorld, &partMatrix, &partScale);

		if (isCostumeRegion)
		{
			if (*gM3dBodyScaleCheatA != 0)
			{
				f32 scale;
				if (part == 7)
					scale = 0.7f;
				else if (part == 5 || part == 6 || part == 10 || part == 11 || part == 17 || part == 14)
					scale = 0.35f;
				else
					scale = 0.25f;

				matrix4x4 cheatScale(
						scale, 0.0f, 0.0f, 0.0f,
						0.0f, scale, 0.0f, 0.0f,
						0.0f, 0.0f, scale, 0.0f,
						0.0f, 0.0f, 0.0f, 1.0f);

				matrix4x4 cheated;
				gsub_476A00(&cheated, &cheatScale, &partMatrix);
				gsub_476A00(&partWorld, &cheated, &partScale);
			}
			else if (*gM3dBodyScaleCheatB != 0)
			{
				f32 scale;
				if (part == 7 || part == 5 || part == 6 || part == 10 || part == 11 || part == 17 || part == 14)
					scale = 1.5f;
				else if (part == 1)
					scale = 0.6f;
				else
					scale = 0.45f;

				matrix4x4 cheatScale(
						scale, 0.0f, 0.0f, 0.0f,
						0.0f, scale, 0.0f, 0.0f,
						0.0f, 0.0f, scale, 0.0f,
						0.0f, 0.0f, 0.0f, 1.0f);

				matrix4x4 cheated;
				gsub_476A00(&cheated, &cheatScale, &partMatrix);
				gsub_476A00(&partWorld, &cheated, &partScale);
			}

			// pulsating head: part 7 breathes in and out on a timer
			if (pulsatingHead && part == 7)
			{
				i32 phase = static_cast<i32>(*gM3dPulseTimer & 0xFF) - 128;
				if (phase < 0)
					phase = -phase;
				f32 pulse = static_cast<f32>(phase) * 0.0046875002f + 1.4f;

				for (i32 headRow = 0; headRow < 4; headRow++)
				{
					partMatrix.field_0[headRow].field_0[0] = partWorld.field_0[headRow].field_0[0];
					partMatrix.field_0[headRow].field_0[1] = partWorld.field_0[headRow].field_0[1];
					partMatrix.field_0[headRow].field_0[2] = partWorld.field_0[headRow].field_0[2];
					partMatrix.field_0[headRow].field_0[3] = partWorld.field_0[headRow].field_0[3];
				}

				matrix4x4 headScale(
						pulse, 0.0f, 0.0f, 0.0f,
						0.0f, pulse, 0.0f, 0.0f,
						0.0f, 0.0f, pulse, 0.0f,
						0.0f, 0.0f, 0.0f, 1.0f);

				gsub_476A00(&partWorld, &partMatrix, &headScale);

				// keep the unscaled translation so the head does not move
				partWorld.field_0[3].field_0[0] = partMatrix.field_0[3].field_0[0];
				partWorld.field_0[3].field_0[1] = partMatrix.field_0[3].field_0[1];
				partWorld.field_0[3].field_0[2] = partMatrix.field_0[3].field_0[2];
				partWorld.field_0[3].field_0[3] = partMatrix.field_0[3].field_0[3];
			}
		}

		matrix4x4 finalTransform;
		gsub_476A00(&finalTransform, &partWorld, &itemTransform);

		DCModelData *pModelData = reinterpret_cast<DCModelData*>(
				gM3dRegionModelData[region] + 36 * modelIndex);
		print_if_false(pModelData != 0, "no dc model data");

		if ((partType & 0x80) == 0)
		{
			i32 modelDataFlags = pModelData->mFlags;
			if ((modelDataFlags & 0x100) == 0)
			{
				if ((modelDataFlags & 0x4000) != 0)
					DC_PSXModel_RenderModel(pModel, &finalTransform, 0, pModelData);
				else
					DCModel_RenderModel(pModel, pModelData, &finalTransform);
			}
		}
	}

	*gM3dSubObjectMasked = 0;
	*gM3dLightingEnabled = 0;
	*gM3dLightsAreDynamic = 0;
	*gM3dUseSuperScale = 0;

	if (isMechList)
	{
		swapTemp = pFirstTransform[5];
		pFirstTransform[5] = pFirstTransform[6];
		pFirstTransform[6] = swapTemp;
		swapTemp = pFirstTransform[10];
		pFirstTransform[10] = pFirstTransform[11];
		pFirstTransform[11] = swapTemp;
	}

	if ((pSuper->mExtraFlags & 2) != 0)
	{
		pSuper->mTransform.m[0][0] = -pSuper->mTransform.m[0][0];
		pSuper->mTransform.m[1][0] = -pSuper->mTransform.m[1][0];
		pSuper->mTransform.m[2][0] = -pSuper->mTransform.m[2][0];
	}

	if ((pSuper->mExtraFlags & 8) != 0)
		*gM3dWaterNormalPad = savedWaterNormalPad;
}

void validate_matrix4x4(void)
{
	VALIDATE_SIZE(matrix4x4, 64);

	VALIDATE(matrix4x4, field_0, 0x0);
}

#include "my_patch.h"

// @Bogus
void patch_ps2m3d(void)
{
	PATCH_PUSH_RET(0x00475F50, M3d_BuildTransform);
}
