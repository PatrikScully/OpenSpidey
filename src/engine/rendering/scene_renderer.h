#pragma once

#ifndef PS2M3D_H
#define PS2M3D_H

#include "compatibility/export.h"
#include "engine/world/ob.h"
#include "game/cinematics/camera.h"
#include "engine/rendering/dcmodel.h"

class matrix4x4
{

	public:
		vector4d field_0[4];

		// empty default ctor: needed so a plain "matrix4x4 result;" local can
		// be declared without running the 16-float ctor below. Compiles to no
		// code either way, so it can't be verified against the binary on its
		// own; added because gsub_476A00 needs an uninitialized local it can
		// fill in field-by-field.
		matrix4x4() { }

		EXPORT matrix4x4(
				f32,
				f32,
				f32,
				f32,
				f32,
				f32,
				f32,
				f32,
				f32,
				f32,
				f32,
				f32,
				f32,
				f32,
				f32,
				f32);

		EXPORT vector4d& operator[](i32);
};

// address 0x476A00. Not present in tools/names.json (only as sub_476A00), so
// named per the gsub_<addr> convention rather than guessing a name. Found by
// disassembling the call sites in M3d_RenderSetup, M3d_Render and
// RenderSuperItem, all of which push 3 pointers (dest, a, b) before the call.
// Computes a standard 4x4 matrix product dest = a * b. Old commented-out
// "__ml" declaration near the top of ps2m3d.cpp guessed at this but had the
// wrong arg count (2 instead of 3) and no dest/return.
EXPORT matrix4x4* gsub_476A00(matrix4x4* dest, matrix4x4 const* a, matrix4x4 const* b);

// address 0x4024A0, named ConvertSMatrixTomatrix4x4 in the maintainer's IDB
// (spideypc_names.txt). Converts an SMatrix (i16 3x3 rotation, fixed-point
// /4096, + i16 translation) into a matrix4x4 (row-major, row-vector*matrix
// convention matching gsub_476A00): rows 0-2 hold the TRANSPOSED rotation
// (row r = source column r) scaled by 1/4096 with a 0 in column 3; row 3
// holds the raw (unscaled) translation with 1.0 in column 3. Traced this
// session as a prerequisite leaf for RenderSuperItem (per-bone pose ->
// render matrix conversion).
EXPORT void ConvertSMatrixTomatrix4x4(SMatrix const* pIn, matrix4x4* pOut);

EXPORT void M3d_BuildTransform(CSuper*);

// addresses 0x46D7E0/0x46D810/0x46E250/0x46FAD0. Per-frame view-frustum
// visibility cull, called from M3d_Render (still forwarded, see its own
// comment). See the long comment on M3dAsm_BoundingSpherePreprocessing in
// ps2m3d.cpp for the full evidence per field/flag.
EXPORT i16* M3dAsm_LoadClipTableA(void const* pSrc);
EXPORT i16* M3dAsm_LoadClipTableB(void const* pSrc);
EXPORT i32 M3dAsm_SetCullSphereOffset(i32 x, i32 y, i32 z);
EXPORT void M3dAsm_BoundingSpherePreprocessing(CItem* pList);

// 0x00660F98. Set => render the raw PSX model and skip the DCModelData
// path. Lives here because scorpion.cpp raises it around its own
// M3d_Render call as well.
static i32 * const gM3dNoDcModelData = (i32*)0x00660F98;

EXPORT void M3d_Render(void*);
EXPORT void DCModel_RenderModel(SModel const *,DCModelData *,matrix4x4 const *);
EXPORT void DC_PSXModel_RenderModel(SModel const *,matrix4x4 const *,void const *,DCModelData *);
EXPORT void M3d_PreprocessPulsingColours(i32);
EXPORT void M3d_PreprocessWibblyTextures(i32);
EXPORT void M3d_RenderBackground(void *);
EXPORT void M3d_RenderCleanup(void);
EXPORT void M3d_RenderSetup(SCamera *,SViewport *,u32 *);
EXPORT void RenderSuperItem(CItem *,bool);

EXPORT extern i32 gWideScreen;
EXPORT extern char gRenderBuf[4];
// The shared debug string buffer the render code sprintfs into and hands to
// the print helper at 0x46CB90. M3d_RenderCleanup (0x473816) pushes it as the
// immediate 56EB54h and Shell_DisplayGameInfo (0x49F1A0) loads the same
// address. The real thing runs to 0x56EEFC (0x3A8 bytes), our array is a 4
// byte placeholder, which is one more reason to read the exe's copy.
//#define G_RENDER_BUF (gRenderBuf)
#define G_RENDER_BUF (reinterpret_cast<char*>(0x0056EB54))

// current colour table pointer, set by M3d_PreprocessPulsingColours from
// PSXRegion[Region].pColourTable, read by the DC/PSX model renderers.
// named pColourTable in the PS2 source (m3d.mik).
EXPORT extern u32* pColourTable;
//#define G_COLOUR_TABLE (pColourTable)
#define G_COLOUR_TABLE (*reinterpret_cast<u32**>(0x0064F5D0))

void validate_matrix4x4(void);
void patch_ps2m3d(void);

#endif
