#include "engine/rendering/polygon_queue.h"
#include "compatibility/legacy_log.h"
#include "engine/rendering/polygon_pipeline.h"
#include "game/application/DXinit.h"
#include "game/application/SpideyDX.h"
#include "compatibility/validate.h"
#include "engine/assets/pcdcFile.h"
#ifdef SPIDEY_STANDALONE
#include "platform/plat.h"
#endif
#include <cstring>
#include <cstdlib>

#if defined(SPIDEY_STANDALONE) && !defined(_WIN32)
#include <execinfo.h>
#endif
#ifdef SPIDEY_STANDALONE
i32 gDbgFanBlend, gDbgFanTex, gDbgFanBucket;
#endif

// The currently-bound D3D texture surface, tracked by DXPOLY_SetTexture to
// skip redundant SetTexture calls. Same class of bug as G_D3DDEVICE7 in
// DXinit.h: DXPOLY_SetTexture is called directly from hooked game logic, so
// this has to be the exe's copy too. Address from the same disassembly,
// "mov [6B7A74h],edi" at the end of DXPOLY_SetTexture.
#ifndef SPIDEY_STANDALONE
EXPORT LPDIRECTDRAWSURFACE7 gDDSurface7;
#else
extern LPDIRECTDRAWSURFACE7 gDDSurface7;
#endif
//#define G_DD_SURFACE7 (gDDSurface7)
#define G_DD_SURFACE7 (*reinterpret_cast<LPDIRECTDRAWSURFACE7*>(0x006B7A74))
EXPORT bool gTexAlpha = false;
EXPORT u32 dword_6B7A8C;
EXPORT f32 flt_56817C = 10.0f;
EXPORT i32 dword_568184;
EXPORT DXPOLY* gSceneBuffer[0x1001];
static spidey::rendering::PolygonQueue gPolygonQueue(gSceneBuffer, 0x1001);
EXPORT u8 gInBeginScene;
EXPORT i32 gScreenshotNumber;
EXPORT u32 gCurrentBlendMode;
EXPORT char* gD3DDepthCompareNames[9] =
{
	"",
    "D3DCMP_NEVER",
    "D3DCMP_LESS",
    "D3DCMP_EQUAL",
    "D3DCMP_LESSEQUAL",
    "D3DCMP_GREATER",
    "D3DCMP_NOTEQUAL",
    "D3DCMP_GREATEREQUAL",
    "D3DCMP_ALWAYS",
};

EXPORT D3DVALUE gFlDepthCompare = 1.0f;
EXPORT u32 gDepthCompareIndex;
EXPORT u8 gDepthBuffering;
EXPORT DWORD gMagFilters[2] = { 1, 2 };
EXPORT DWORD gMinFilters[2] = { 1, 2 };

EXPORT u32 gCurrentFilterIndex;

EXPORT bool gDepthWriting;
#ifndef SPIDEY_STANDALONE
EXPORT bool gDxPolyRelated;
#else
extern bool gDxPolyRelated;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT i32 gHudOffset;
#else
extern i32 gHudOffset;
#endif
EXPORT f32 gFlHudOffset = 1.0f;

EXPORT D3DCOLOR gDxPolyBackgroundColor = 0x0FF000000;
EXPORT u32 gDxOutlineColor = 0x0FF00FF00;

// Tentative globals for the low graphics scanline setup below. Names and
// guessed roles are ours, not confirmed against his IDB (idb_globals.txt has
// nothing at these addresses except gLowGraphicsRelated, already known).
static void* gLowGraphicsSurface;             // 0x2E096D4, cached lpSurface
static i32 gLowGraphicsPixelCount;            // 0x2E096DC, width * height
static u8* const gLowGraphicsPaletteDirty = (u8*)0x2E096E1; // set 1 here, cleared by gsub_514ED0
static i32* const gLowGraphicsColor16 = (i32*)0x2E096D0;    // packed 16 bit color, read first thing in gsub_514ED0
static i32 gLowGraphicsWidth;                 // 0x568F90, cached every call
static i32 gLowGraphicsHeight;                // 0x568F98, cached, used to detect a size change
static i32 gLowGraphicsPitch;                 // 0x568F94, cached every call, read back as a row stride in gsub_514ED0
static f32 gLowGraphicsFadeColor;             // 0x2E04568
static i32 gLowGraphicsColorRelated;          // 0x282854C
static f32 gLowGraphicsHalfWidth;             // 0xADC4EC
static f32 gLowGraphicsHalfHeight;            // 0xADC4F0
// Two small descriptor structs the fog/backdrop code reads back elsewhere
// (not in this file). Field layout is a guess from the store pattern only.
static void* gLowGraphicsFadeDescPtrA;        // 0xADC4E0, set to a fixed address
static void* gLowGraphicsFadeDescPtrB;        // 0xADC4E4, set to a fixed address
static i32 gLowGraphicsFadeDescUnused14;      // 0xADC4F4
static i32 gLowGraphicsViewWidth;             // 0x2828548
static i32 gLowGraphicsViewUnused8;           // 0x2828550
static i32 gLowGraphicsViewHeight;            // 0x2828554

// @Ok
// Functional: low-graphics scanline table setup, logic verified against
// Hex-Rays at 0x514DB0. Reallocates gLowGraphicsRelated (16 bytes per
// scanline) only when the height changes; caches pitch for gsub_514ED0.
// Fixed a condition bug found while verifying: the guard was
// (width>=0 && height<0), original is (width<0 || height<0) (a dead-code
// check, width/height are always >= 0 in practice).
EXPORT void gsub_514DB0(
		LPVOID lpSurface,
		i32 width,
		i32 height,
		LONG pitch,
		u32 color16,
		f32 fadeColor,
		i32 colorRelated,
		f32 halfWidth,
		f32 halfHeight)
{
	gLowGraphicsFadeColor = fadeColor;
	gLowGraphicsColorRelated = colorRelated;
	gLowGraphicsHalfHeight = halfHeight;
	gLowGraphicsWidth = width;
	gLowGraphicsSurface = lpSurface;
	gLowGraphicsHalfWidth = halfWidth;
	*gLowGraphicsPaletteDirty = 1;
	gLowGraphicsPitch = pitch;

	if (height != gLowGraphicsHeight)
	{
		void* oldBuf = gLowGraphicsRelated;
		gLowGraphicsHeight = height;

		if (oldBuf)
			free(oldBuf);

		gLowGraphicsRelated = malloc(gLowGraphicsHeight * 0x10);
		memset(gLowGraphicsRelated, 0, gLowGraphicsHeight * 0x10);
	}

	gLowGraphicsFadeDescPtrA = (void*)0x2828558;
	gLowGraphicsFadeDescPtrB = (void*)0xADC4F8;
	gLowGraphicsFadeDescUnused14 = 0;
	gLowGraphicsViewWidth = gLowGraphicsWidth;
	gLowGraphicsViewUnused8 = 0;
	gLowGraphicsViewHeight = gLowGraphicsHeight;

	if (gLowGraphicsWidth < 0 || gLowGraphicsHeight < 0)
	{
		gLowGraphicsFadeDescUnused14 = 0;
		gLowGraphicsViewWidth = 0;
		gLowGraphicsViewUnused8 = 0;
		gLowGraphicsViewHeight = 0;
	}

	gLowGraphicsPixelCount = gLowGraphicsWidth * gLowGraphicsHeight;
	*gLowGraphicsColor16 = color16;
}

// @Ok
void DXPOLY_BeginScene(void)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	print_if_false(gInBeginScene == 0, "nested BeginScene() calls!");
	gPolygonQueue.clear();
	gInBeginScene = 1;

	if (gLowGraphics)
	{
		DDSURFACEDESC2 v13;
		memset(&v13, 0, sizeof(v13));
		v13.dwSize = sizeof(v13);

		HRESULT hr = g_pDDS_Scene->Lock(0, &v13, 2048, 0);
		D3D_ERROR_LOG_AND_QUIT(hr);

		i32 width, height;
		DXINIT_GetCurrentResolution(&width, &height);
		f32 v9 = (f32)height;
		f32 v8 = v9 * 0.5f;
		f32 v10 = (f32)width;
		f32 v7 = v10 * 0.5f;
		gsub_514DB0(
			v13.lpSurface,
			width,
			height,
			v13.lPitch,
			(gDxPolyBackgroundColor & 0xF8 | ((gDxPolyBackgroundColor & 0xFC00 | (gDxPolyBackgroundColor >> 3) & 0x1F0000) >> 2)) >> 3,
			flt_56817C,
			dword_568184,
			v7,
			v8);
	}
	else
	{
		HRESULT hr = G_D3DDEVICE7->BeginScene();
		D3D_ERROR_LOG_AND_QUIT(hr);

		if (gDxPolyRelated)
			hr = G_D3DDEVICE7->Clear(0, 0, 3, gDxPolyBackgroundColor, gFlDepthCompare, 0);
		else
			hr = G_D3DDEVICE7->Clear(0, 0, 1, gDxPolyBackgroundColor, gFlDepthCompare, 0);
		D3D_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	print_if_false(gInBeginScene == 0, "nested BeginScene() calls!");
	gPolygonQueue.clear();
	gInBeginScene = 1;
	Plat_GfxBeginScene(gDxPolyBackgroundColor, gDxPolyRelated);
#endif
}

// base value for the depth bucket math below, tentative name/purpose guess.
static i32* const gDxPolyDepthBucketBase = (i32*)0x6BBAA8;

#ifdef SPIDEY_STANDALONE
static i32 gDumpFrames = -1;
static u32 gDumpAt;
static void dumpPoly(const DXPOLY* packet, i32 bucket)
{
	printf("POLY bucket=%d n=%lu tex=%p blend=%u flags=%x", bucket, (unsigned long)packet->vertexCount,
			packet->textureHandle, packet->blendMode, packet->flags);
	for (u32 k = 0; k < packet->vertexCount && k < 4; k++)
		printf(" [%.1f,%.1f,%.4f rhw=%.4f c=%08x uv=%.3f,%.3f]",
				packet->vertices[k].x, packet->vertices[k].y, packet->vertices[k].depth,
				packet->vertices[k].reciprocalW, packet->vertices[k].colorArgb,
				packet->vertices[k].u, packet->vertices[k].v);
	printf("\n");
}
#endif

// @Ok
// Behaviour: with low graphics on and not on render pass 1, a near plane
// visibility test on the poly's first/second/last (and, for 4+ verts,
// third/fourth) vertex runs first and can discard the poly outright. Then:
// a forced bucket (forcedBucket >= 0) always goes straight into that gSceneBuffer slot;
// otherwise, if gDxPolyRelated is set and the poly has no blend mode, it
// draws right now instead of queueing; otherwise it goes into a depth-sorted
// bucket derived from depth and depthBias.
// Verified logic against Hex-Rays at 0x503100 this session, fixed two real
// bugs: cross2 (the third/fourth vertex cull term) had dx3/dy3 swapped
// against dx2/dy2, computing the negated cross product and inverting cull2
// for any nonzero case; and the immediate-draw path called
// DXPOLY_SetFilterMode, which the original does not do at all here (only
// SetTexture, SetBlendMode, address U/V and tex alpha are set before
// DrawPrimitive). 199 mnemonic diffs left before this pass, register
// scheduling only, see dxsound.attempts.md.
void DXPOLY_DrawPoly(
		DXPOLY* packet,
		i32 forcedBucket,
		i32 depthBias,
		f32 depth)
{
	if (!gInBeginScene)
	{
		DXERR_printf("drawing outside scene\r\n");
#ifdef SPIDEY_STANDALONE
#ifndef _WIN32
		// print the first caller once so the stray draw can be found
		static i32 shown = 0;
		if (!shown)
		{
			shown = 1;
			void* frames[16];
			i32 n = backtrace(frames, 16);
			printf("OUTSIDE backtrace:");
			for (i32 k = 0; k < n; k++)
				printf(" %p", frames[k]);
			printf("\n");
		}
#endif
#endif
	}

	if (gLowGraphics && dword_6B7A8C != 1)
	{
		f32 dx1 = packet->vertices[1].x - packet->vertices[0].x;
		f32 dy1 = packet->vertices[1].y - packet->vertices[0].y;
		i32 lastIdx = packet->vertexCount - 1;
		f32 dxN = packet->vertices[lastIdx].x - packet->vertices[0].x;
		f32 dyN = packet->vertices[lastIdx].y - packet->vertices[0].y;

		f32 dx2 = 0.0f, dy2 = 0.0f, dx3 = 0.0f, dy3 = 0.0f;
		if (lastIdx > 2)
		{
			dx2 = packet->vertices[1].x - packet->vertices[2].x;
			dy2 = packet->vertices[1].y - packet->vertices[2].y;
			dx3 = packet->vertices[3].x - packet->vertices[2].x;
			dy3 = packet->vertices[3].y - packet->vertices[2].y;
		}

		f32 cross1 = dyN * dx1 - dxN * dy1;
		u8 cull1 = (dword_6B7A8C == 3) ? (cross1 >= 0.0f) : (cross1 <= 0.0f);

		if (packet->vertexCount > 3)
		{
			f32 cross2 = dx3 * dy2 - dy3 * dx2;
			u8 cull2 = (dword_6B7A8C == 3) ? (cross2 >= 0.0f) : (cross2 <= 0.0f);

			if (cull1 != cull2)
				return;
		}
		else if (!cull1)
		{
			return;
		}
	}

#ifdef SPIDEY_STANDALONE
	// The original flags occupy the low bits. Remember submission context
	// for HUD text and blended pieces regardless of their sorting bucket.
	packet->flags = (packet->flags & ~spidey::rendering::HudSubmission) | (Plat_GfxGetHudView() ? spidey::rendering::HudSubmission : 0);
#endif

	if (forcedBucket >= 0)
	{
		print_if_false(forcedBucket <= 4096, "Invalid forced slot number!");
		gPolygonQueue.enqueue(forcedBucket, *packet);
	}
	else if (gDxPolyRelated && packet->blendMode == 0)
	{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
		DXPOLY_SetTexture(packet->textureHandle);
		DXPOLY_SetBlendMode(packet->blendMode);

		DXPOLY_SetAddressUAndV(
				(packet->flags & spidey::rendering::WrapTextureU) ? 1 : 3,
				(packet->flags & spidey::rendering::WrapTextureV) ? 1 : 3);

		DXPOLY_EnableTexAlpha((packet->flags & spidey::rendering::TextureAlpha) != 0);

		G_D3DDEVICE7->DrawPrimitive(
				D3DPT_TRIANGLEFAN,
				324,
				&packet->vertices[0],
				packet->vertexCount,
				0);
#elif defined(SPIDEY_STANDALONE)
		if (gDumpFrames > 0 && Plat_Ticks() >= gDumpAt)
			dumpPoly(packet, -1);
		DXPOLY_SetTexture(packet->textureHandle);
		DXPOLY_SetBlendMode(packet->blendMode);
		DXPOLY_SetAddressUAndV(
				(packet->flags & spidey::rendering::WrapTextureU) ? 1 : 3,
				(packet->flags & spidey::rendering::WrapTextureV) ? 1 : 3);
		DXPOLY_EnableTexAlpha((packet->flags & spidey::rendering::TextureAlpha) != 0);
		gDbgFanBlend = packet->blendMode; gDbgFanTex = (i32)(size_t)packet->textureHandle; gDbgFanBucket = -1;
		Plat_GfxDrawFan(packet->vertices, packet->vertexCount);
#endif
	}
	else
	{
		i32 bucket = *gDxPolyDepthBucketBase - (i32)(depth * -4096.0f) + depthBias;
		if (bucket < 0)
			bucket = 0;
		else if (bucket > 0x1000)
			bucket = 0x1000;

		gPolygonQueue.enqueue(bucket, *packet);
	}
}

// @Ok
void DXPOLY_EnableTexAlpha(bool enabled)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (enabled != gTexAlpha)
	{
		gTexAlpha = enabled;
		G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_ALPHAOP, enabled ? 4 : 3);
	}
#elif defined(SPIDEY_STANDALONE)
	if (enabled != gTexAlpha)
	{
		gTexAlpha = enabled;
		Plat_GfxSetTexAlpha(enabled);
	}
#endif
}

// Two callees this function needs that are outside our assigned range
// (0x513FF0, 0x511860). Forwarded to the original rather than guessed at,
// same pattern as ClearRegion in spool.cpp.
typedef i32 (*func_513FF0_t)(i32, i32, void*, i32);
static const func_513FF0_t gsub_513FF0 = (func_513FF0_t)0x00513FF0;
typedef void (*func_511860_t)(i32, i32);
static const func_511860_t gsub_511860 = (func_511860_t)0x00511860;

// One entry of the per scanline table gsub_514DB0 allocates into
// gLowGraphicsRelated (16 bytes/row). mReady gates whether the row
// participates in the copy below; mTexA/mTexB are two chains of unknown
// "texture" objects (offset 0x40 = next, 0x44 = a row-write callback taking
// (node, dest), 0x48 = a u16 word count used to size the write). Struct
// layout guessed from the disasm only, not confirmed against his IDB.
struct SLowGraphicsScanline
{
	u8 mReady;
	u8 pad[7];
	void* mTexA;
	void* mTexB;
};
struct SLowGraphicsTexNode
{
	u8 pad0[0x40];
	SLowGraphicsTexNode* pNext;
	void (*mWriteRow)(SLowGraphicsTexNode*, void*);
	u16 mRowWords;
};

// @Ok
// Low graphics frame flush: builds an 8 bit RGB fade color from the packed
// 16 bit gLowGraphicsColor16 (skips everything if it is negative), hands a
// small local table of it to gsub_513FF0/gsub_511860 (both out of scope, not
// attempted, real purpose unclear beyond "fog/fade related"), then walks
// gLowGraphicsRelated's per scanline texture node chains and MMX-copies each
// node's 0x40 byte row into the destination surface at gLowGraphicsSurface.
// Verified the scanline copy loop (the part that actually writes visible
// pixels) against Hex-Rays at 0x514ED0 this session and fixed two bugs: the
// copy size per row was rounded UP to the next 64 byte block, the original
// rounds DOWN (plain integer division, `2*width/64*64`, no +0x3F); and the
// mReady gate only tests bit 0 of the scanline flags byte (`& 1`), not the
// whole byte's truth value. The fade table section that feeds
// gsub_513FF0/gsub_511860 is still a best effort translation (its exact
// field layout is only confirmed for the color/fade values that matter to
// the copy loop, not the two forwarded callees themselves, which are out of
// this session's assigned range and were not decompiled). See
// dxsound.attempts.md.
void gsub_514ED0(void)
{
	i32 color = *gLowGraphicsColor16;

	if (color >= 0)
	{
		i32 color16 = color & 0xFFFF;

		i32 r8 = ((color16 >> 11) & 0x1F) * 255 / 31;
		i32 g8 = ((color16 >> 5) & 0x3F) * 255 / 63;
		i32 b8 = (color16 & 0x1F) * 255 / 31;

		f32 fadeShade = gLowGraphicsFadeColor / 128000.0f;

		// Best effort only, exact field usage of this local table is not
		// confirmed (see dxsound.attempts.md).
		i32 fadeTable[5][7] = {0};
		for (i32 i = 0; i < 5; i++)
		{
			fadeTable[i][0] = (i32)fadeShade;
			fadeTable[i][1] = r8;
			fadeTable[i][2] = g8;
		}

		if (gLowGraphicsPixelCount > 0)
		{
			for (i32 i = 3; i >= 0; i--)
				fadeTable[i][3] = b8;

			i32 result = gsub_513FF0(0, 0, fadeTable, 4);
			if (result >= 3)
			{
				stateLog("%s", (char*)0x563D88);
			}

			gsub_511860(0, 0);
		}
	}

	// This alignment nudge (round up to the next multiple of 8, unless
	// already aligned) matches the disasm but its purpose here is unclear.
	u8* alignedScratch = (u8*)0x2E086D0;
	if (((u32)alignedScratch & 7) != 0)
		alignedScratch = (u8*)(((u32)alignedScratch & ~7) + 8);

	SLowGraphicsScanline* scanlines = (SLowGraphicsScanline*)gLowGraphicsRelated;
	u8* dest = (u8*)gLowGraphicsSurface;

	for (i32 y = 0; y < gLowGraphicsHeight; y++)
	{
		u8* rowDest = dest;

		if (*gLowGraphicsColor16 >= 0 && (scanlines[y].mReady & 1))
			rowDest = alignedScratch;

		for (i32 which = 0; which < 2; which++)
		{
			SLowGraphicsTexNode* node = which == 0
					? (SLowGraphicsTexNode*)scanlines[y].mTexA
					: (SLowGraphicsTexNode*)scanlines[y].mTexB;

			while (node)
			{
				node->mWriteRow(node, rowDest + node->mRowWords * 2);
				node = node->pNext;
			}
		}

		if (*gLowGraphicsColor16 >= 0 && (scanlines[y].mReady & 1))
		{
			// MMX 64 byte block copy, alignedScratch -> dest, matches the
			// original's 8x movq loop. The original rounds the block count
			// DOWN (plain integer division), not up.
			memcpy(dest, alignedScratch, (gLowGraphicsWidth * 2 / 0x40) * 0x40);
		}

		dest += gLowGraphicsPitch;
	}

	*gLowGraphicsPaletteDirty = 0;
}

// @Ok
void DXPOLY_EndScene(bool presentFrame)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gInBeginScene)
	{
		gInBeginScene = 0;
		renderScene();
		if (gLowGraphics)
		{
			gsub_514ED0();
			g_pDDS_Scene->Unlock(0);
		}
		else
		{
			HRESULT hr = G_D3DDEVICE7->EndScene();
			D3D_ERROR_LOG_AND_QUIT(hr);
		}

		if (presentFrame)
		{
			DXPOLY_Flip();
		}
	}
#elif defined(SPIDEY_STANDALONE)
	if (gInBeginScene)
	{
		gInBeginScene = 0;
		renderScene();
		Plat_GfxEndScene();
		if (presentFrame)
			DXPOLY_Flip();
	}
#endif
}

// @Ok
// @Matching
void DXPOLY_Flip(void)
{
#ifdef SPIDEY_STANDALONE
	// SPIDEY_SHOTS="ms,ms,...": save scrnNNNN.bmp at those times (test aid)
	{
		static i32 parsed;
		static u32 shotAt[16];
		static i32 shotCount, shotNext;
		if (!parsed)
		{
			parsed = 1;
			const char* env = getenv("SPIDEY_SHOTS");
			while (env && *env && shotCount < 16)
			{
				shotAt[shotCount++] = (u32)strtoul(env, 0, 10);
				const char* c = strchr(env, ',');
				env = c ? c + 1 : 0;
			}
		}
		if (shotNext < shotCount && Plat_Ticks() >= shotAt[shotNext])
		{
			shotNext++;
			DXPOLY_SaveScreen();
		}
	}
	Plat_GfxFlip();
	// the Windows build pumps its message queue from several places the
	// standalone build does not have; once per presented frame is enough
	WinYield();
#endif
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gDxOptionRelated)
	{
		DDBLTFX v4;
		memset(&v4, 0, sizeof(v4));
		v4.dwSize = sizeof(v4);

		HRESULT hr = g_pDDS_SaveScreen->Blt(&gRect, g_pDDS_Scene, 0, 0x1000000, &v4);
		D3D_ERROR_LOG_AND_QUIT(hr);
	}
	else
	{
		HRESULT hr = g_pDDS_SaveScreen->Flip(0, 1);
		D3D_ERROR_LOG_AND_QUIT(hr);
	}
#endif
}

EXPORT u8 byte_6B7A80 = 0;

EXPORT f32 gFogStart;
EXPORT f32 gFogEnd;
EXPORT u32 gFogColor;
EXPORT DWORD gAddressU;
EXPORT DWORD gAddressV;

u32 dword_568F98;
u32 dword_568F94;
u32 dword_568F90;
u32 dword_2E096D4;

// @Ok
EXPORT void gsub_515270(void)
{
	dword_568F98 = 0;
	dword_568F94 = 0;
	dword_568F90 = 0;
	dword_2E096D4 = 0;
	gLowGraphicsRelated = 0;
}

// @Ok
// Verified field by field against Hex-Rays at 0x502220 this session: every
// constant, every SetRenderState/SetTextureStageState argument and value,
// and the gMagFilters/gMinFilters[gCurrentFilterIndex] pair at the end all
// match. gDxPolyRelated is (a1>>1)&1 stored once and reused (not (a1&2)!=0
// recomputed per use), and it is the same global 0x6BBAA5 DXPOLY_DrawPoly's
// immediate-draw check reads (fixed there too, was a separate invented
// gDxPolyImmediateDraw pointer). Most of the SetRenderState/
// SetTextureStageState arguments below are raw D3DRENDERSTATETYPE/
// D3DTEXTURESTAGESTATETYPE numbers, not enum names, since Hex-Rays only
// resolves them as integers too. 396 mnemonic diffs, whole-function
// register allocation/scheduling only (the very first instructions already
// diverge), no logic difference found.
void DXPOLY_Init(u32 deviceFlags)
{
	if ( gLowGraphics )
		gsub_515270();

	gDxPolyRelated = (deviceFlags >> 1) & 1;
	gDepthCompareIndex = 4;
	byte_6B7A80 = 0;
	gDepthBuffering = gDxPolyRelated;
	gDepthWriting = gDxPolyRelated;
	gTexAlpha = false;
	gCurrentFilterIndex = 1;
	gFogStart = 0.1f;
	gFogEnd = 0.99000001f;
	gFogColor = 0xFFFFFF;
	dword_6B7A8C = 3;
	gAddressU = 3;
	gAddressV = 3;
	gCurrentBlendMode = 0;
	G_DD_SURFACE7 = 0;

#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ANTIALIAS, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)4, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)7, gDxPolyRelated != 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)8, 3);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)9, 2);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)10, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)14, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)15, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)16, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)19, 2);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)20, 1);
	if ( gLowGraphics )
		G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)22, 1);
	else
		G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)22, 3);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)23, 4);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)24, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)25, 8);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)26, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)27, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)28, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)29, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)33, 0);
	G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_FOGCOLOR, gFogColor);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)35, 0);
	G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_FOGSTART, gFogStart);
	G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_FOGEND, gFogEnd);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)38, 1065353216);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)40, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)41, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)47, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)48, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)52, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)53, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)54, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)55, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)56, 8);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)57, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)58, -1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)59, -1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)60, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)128, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)129, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)130, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)131, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)132, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)133, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)134, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)135, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)136, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)137, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)138, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)139, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)140, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)141, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)142, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)143, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)144, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)145, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)146, 2);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)147, 2);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)148, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)151, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)152, 0);

	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)1, 4);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)2, 2);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)3, 0);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)4, 4);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)5, 2);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)6, 0);
	G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_ADDRESSU, gAddressU);
	G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_ADDRESSV, gAddressV);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)16, gMagFilters[gCurrentFilterIndex]);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)17, gMinFilters[gCurrentFilterIndex]);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)18, 1);
#elif defined(SPIDEY_STANDALONE)
	// the same defaults as the SetRenderState block above, in platform terms
	Plat_GfxSetDepthTest(gDxPolyRelated);     // 7  ZENABLE
	Plat_GfxSetDepthWrite(1);                 // 14 ZWRITEENABLE
	Plat_GfxSetDepthFunc(4);                  // 23 ZFUNC = LESSEQUAL
	Plat_GfxSetBlendMode(0);                  // 19/20/27 ONE, ZERO, no blend
	Plat_GfxSetFog(0, gFogColor, gFogStart, gFogEnd);
	Plat_GfxSetAddress(gAddressU, gAddressV);
	Plat_GfxSetFilter(gCurrentFilterIndex);
	Plat_GfxSetTexAlpha(1);                   // ALPHAOP = MODULATE
	Plat_GfxSetTexture(0);
#endif
}

// @Ok
// @Matching
void DXPOLY_SaveScreen(void)
{
#ifdef SPIDEY_STANDALONE
	// 24 bit BMP straight from the platform layer's frame buffer
	{
		char name[32];
		sprintf(name, "scrn%4.4i.bmp", ++gScreenshotNumber);
		i32 width, height;
		DXINIT_GetCurrentResolution(&width, &height);
		i32 rowBytes = (width * 3 + 3) & ~3;
		u8* pixels = static_cast<u8*>(malloc(rowBytes * height));
		memset(pixels, 0, rowBytes * height);
		if (Plat_GfxReadPixels(pixels, width, height))
		{
			FILE* f = fopen(name, "wb");
			if (f)
			{
				u32 fileSize = 54 + rowBytes * height;
				u8 hdr[54];
				memset(hdr, 0, sizeof(hdr));
				hdr[0] = 'B'; hdr[1] = 'M';
				memcpy(hdr + 2, &fileSize, 4);
				u32 off = 54; memcpy(hdr + 10, &off, 4);
				u32 dib = 40; memcpy(hdr + 14, &dib, 4);
				memcpy(hdr + 18, &width, 4);
				i32 negHeight = -height;   // top down rows
				memcpy(hdr + 22, &negHeight, 4);
				u16 planes = 1, bpp = 24;
				memcpy(hdr + 26, &planes, 2);
				memcpy(hdr + 28, &bpp, 2);
				u32 imgSize = rowBytes * height; memcpy(hdr + 34, &imgSize, 4);
				fwrite(hdr, 1, 54, f);
				// Plat_GfxReadPixels packs rows at width*3, the BMP wants
				// rows padded to 4 bytes
				for (i32 y = 0; y < height; y++)
				{
					fwrite(pixels + y * width * 3, 1, width * 3, f);
					if (rowBytes != width * 3)
						fwrite(hdr + 50, 1, rowBytes - width * 3, f);   // zero pad
				}
				fclose(f);
				printf("Saved %s\n", name);
			}
		}
		free(pixels);
	}
	return;
#endif
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	char v7[32];
	sprintf(v7, "scrn%4.4i.bmp", ++gScreenshotNumber);

	DDSURFACEDESC2 v6;
	memset(&v6, 0, sizeof(v6));
	v6.dwSize = sizeof(v6);

	if (gDxOptionRelated)
	{
		u32 width, height;
		DXINIT_GetCurrentResolution(&width, &height);

		HRESULT hr = g_pDDS_SaveScreen->Lock(&gRect, &v6, 16, 0);
		D3D_ERROR_LOG_AND_QUIT(hr);

		DXPOLY_SaveSurfaceAsBMP(
				v7,
				v6.lpSurface,
				width,
				height,
				v6.lPitch,
				&v6.ddpfPixelFormat,
				false);
		g_pDDS_SaveScreen->Unlock(&gRect);
	}
	else
	{
		HRESULT hr = g_pDDS_SaveScreen->Lock(0, &v6, 16, 0);
		D3D_ERROR_LOG_AND_QUIT(hr);

		DXPOLY_SaveSurfaceAsBMP(
				v7,
				v6.lpSurface,
				v6.dwWidth,
				v6.dwHeight,
				v6.lPitch,
				&v6.ddpfPixelFormat,
				false);
		g_pDDS_SaveScreen->Unlock(0);
	}

	DXERR_printf("Saved Screenshot %i.\r\n", gScreenshotNumber);
#endif
}

// @Ok
// Writes a bottom-up 24 bit BGR BMP of a surface to disk. Understands 3
// 16 bit source pixel formats (555 with a top 1 bit alpha, 565, 4444) plus a
// 32 bit 0x00RRGGBB path. `flag` picks an alternate per pixel source read:
// for the 32 bit path it takes the top byte only, replicated to R/G/B; for
// the two 16 bit formats that carry an alpha mask (555+alpha, 4444) it does
// the same thing with the alpha bits instead of R/G/B. 565 has no alpha
// mask, so the original bails out early (returns without writing anything)
// if flag is set for a 565 source, instead of reading garbage alpha fields.
// Verified logic against Hex-Rays at 0x503560 this session: the disasm's
// early "if (flag) return" branch belongs to 565, not to a distinct "555
// without alpha" format like a previous draft had it (there is no such
// format branch in the original at all: a 555 source with alpha mask 0 does
// not match 555+alpha, 565 or 4444, and the real disasm just falls through
// reading a stale, never-written stack slot for the blue bit count, i.e.
// genuine undefined behaviour on an input DirectDraw does not actually
// hand out in practice; we print the same "unknown format" error instead
// of reproducing that garbage read). Any other format prints an error and
// returns without writing, same as the true 32 bit mismatch path.
void DXPOLY_SaveSurfaceAsBMP(
		char* filename,
		void* pData,
		i32 width,
		i32 height,
		i32 pitch,
		_DDPIXELFORMAT* pf,
		bool flag)
{
#ifdef _WIN32
	u8 use32BitSrc = 0;
	i32 rShift = 0, gShift = 0, bShift = 0, aShift = 0;
	i32 rBits = 0, gBits = 0, bBits = 0, aBits = 0;
	u32 rMask = 0, gMask = 0, bMask = 0, aMask = 0;

	if (pf->dwRGBBitCount == 16)
	{
		u32 r = pf->dwRBitMask;
		u32 g = pf->dwGBitMask;
		u32 b = pf->dwBBitMask;
		u32 a = pf->dwRGBAlphaBitMask;

		if (r == 0x7C00 && g == 0x3E0 && b == 0x1F && a == 0x8000)
		{
			rMask = r; gMask = g; bMask = b; aMask = a;
			rShift = 10; gShift = 5; bShift = 0; aShift = 15;
			rBits = 5; gBits = 5; bBits = 5; aBits = 1;
		}
		else if (r == 0xF800 && g == 0x7E0 && b == 0x1F && a == 0)
		{
			if (flag)
				return;

			rMask = r; gMask = g; bMask = b;
			rShift = 11; gShift = 5; bShift = 0;
			rBits = 5; gBits = 6; bBits = 5;
		}
		else if (r == 0xF00 && g == 0xF0 && b == 0xF && a == 0xF000)
		{
			rMask = r; gMask = g; bMask = b; aMask = a;
			rShift = 8; gShift = 4; bShift = 0; aShift = 12;
			rBits = 4; gBits = 4; bBits = 4; aBits = 4;
		}
		else
		{
			print_if_false(0, "SaveTex(): Unknown format = [%8.8X, %8.8X, %8.8X, %8.8X]\r\n", r, g, b, a);
			return;
		}
	}
	else if (pf->dwRGBBitCount == 0x20 &&
			pf->dwRBitMask == 0xFF0000 && pf->dwGBitMask == 0xFF00 && pf->dwBBitMask == 0xFF)
	{
		use32BitSrc = 1;
	}
	else
	{
		print_if_false(0, "SaveTex(): Unknown format = [%8.8X, %8.8X, %8.8X, %8.8X]\r\n",
				pf->dwRBitMask, pf->dwGBitMask, pf->dwBBitMask, pf->dwRGBAlphaBitMask);
		return;
	}

	i32 rowBytes = ((width * 3 + 3) / 4) * 4;
	i32 imageSize = rowBytes * height;

	FILE* f = fopen(filename, "wb");
	if (!f)
	{
		print_if_false(0, "SaveSurfaceAsBMP(): Problems creating: %s...\r\n", filename);
		return;
	}

	BITMAPFILEHEADER bmfh;
	memset(&bmfh, 0, sizeof(bmfh));
	bmfh.bfType = 0x4D42;
	bmfh.bfSize = imageSize + 0x36;
	bmfh.bfOffBits = 0x36;
	fwrite(&bmfh, 0xE, 1, f);

	BITMAPINFOHEADER bmih;
	memset(&bmih, 0, sizeof(bmih));
	bmih.biSize = 0x28;
	bmih.biWidth = width;
	bmih.biHeight = height;
	bmih.biPlanes = 1;
	bmih.biBitCount = 0x18;
	fwrite(&bmih, 0x28, 1, f);

	u8* row = (u8*)malloc(rowBytes);
	u8* srcRow = (u8*)pData + (height - 1) * pitch;

	for (i32 y = height; y > 0; y--)
	{
		u8* dst = row;
		u16* src16 = (u16*)srcRow;
		u32* src32 = (u32*)srcRow;

		for (i32 x = width; x > 0; x--)
		{
			u32 r, g, b;

			if (use32BitSrc)
			{
				u32 px = *src32++;
				if (flag)
				{
					b = (px >> 24) & 0xFF;
					g = b;
					r = b;
				}
				else
				{
					r = (px >> 16) & 0xFF;
					g = (px >> 8) & 0xFF;
					b = px & 0xFF;
				}
			}
			else
			{
				u32 px = *src16++;
				if (flag)
				{
					r = g = b = ((px & aMask) >> aShift) << (8 - aBits);
				}
				else
				{
					r = ((px & rMask) >> rShift) << (8 - rBits);
					g = ((px & gMask) >> gShift) << (8 - gBits);
					b = ((px & bMask) >> bShift) << (8 - bBits);
				}
			}

			*dst++ = (u8)b;
			*dst++ = (u8)g;
			*dst++ = (u8)r;
		}

		fwrite(row, rowBytes, 1, f);
		srcRow -= pitch;
	}

	fclose(f);
	free(row);
#endif
}

// @Ok
// @Matching
void DXPOLY_SetBackgroundColor(u32 color)
{
	gDxPolyBackgroundColor = color;
}

// @Ok
void DXPOLY_SetBlendMode(u32 blendMode)
{

#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	u32 newBlendMode = blendMode;
	if (gCurrentBlendMode != blendMode)
	{
		switch(blendMode)
		{
			default:
				DXERR_printf("ERROR: Invalid blend mode passed to DXPOLY_SetBlendMode(): %lu\r\n", blendMode);
				newBlendMode = 0;
			case 0:
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_SRCBLEND, 2);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_DESTBLEND, 1);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ALPHABLENDENABLE, 0);
				DXPOLY_SetDepthWriting(1);
				break;
			case 3:
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_SRCBLEND, 1);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_DESTBLEND, 4);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ALPHABLENDENABLE, 1);
				DXPOLY_SetDepthWriting(0);
				break;
			case 1:
			case 5:
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_SRCBLEND, 5);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_DESTBLEND, 6);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ALPHABLENDENABLE, 1);
				DXPOLY_SetDepthWriting(0);
				break;
			case 2:
			case 4:
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_SRCBLEND, 5);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_DESTBLEND, 2);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ALPHABLENDENABLE, 1);
				DXPOLY_SetDepthWriting(0);
				break;

		}

		gCurrentBlendMode = newBlendMode;
	}
#elif defined(SPIDEY_STANDALONE)
	u32 newBlendMode = blendMode;
	if (gCurrentBlendMode != blendMode)
	{
		if (blendMode > 5)
		{
			DXERR_printf("ERROR: Invalid blend mode passed to DXPOLY_SetBlendMode(): %lu\r\n", blendMode);
			newBlendMode = 0;
		}
		Plat_GfxSetBlendMode(newBlendMode);
		DXPOLY_SetDepthWriting(newBlendMode == 0);
		gCurrentBlendMode = newBlendMode;
	}
#endif
}

// @Ok
// @Matching
// The ZENABLE flag here is gDepthBuffering (0x6B7A98, the IDB's gDepthBUffering),
// not gDepthWriting (0x6B7A89, ZWRITEENABLE, owned by DXPOLY_SetDepthWriting).
// With the two merged, enabling the depth test marked writes as enabled too and
// the next DXPOLY_SetDepthWriting(1) was skipped: opaque world polys were drawn
// with the depth mask off and the far backdrop painted over them (found with
// the SDL3 build, 2026-09-03).
void DXPOLY_SetDepthCompare(u32 compareMode)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gDxPolyRelated)
	{
		if (!compareMode)
		{
			if (gDepthBuffering)
			{
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZENABLE, 0);
				gDepthBuffering = 0;
				DXERR_printf("Depth Buffering Disabled.\r\n");
			}

			return;
		}

		if (!gDepthBuffering)
		{
			G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZENABLE, 1);
			gDepthBuffering = 1;
			DXERR_printf("Depth Buffering Enabled.\r\n");
		}

		if (compareMode != gDepthCompareIndex)
		{
			G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZFUNC, compareMode);
			char* status = gD3DDepthCompareNames[compareMode];
			gDepthCompareIndex = compareMode;

			DXERR_printf("Depth Compare = %s\r\n", status);
			if ( compareMode == 5 || (gFlDepthCompare = 1.0f, compareMode == 7) )
				gFlDepthCompare = 0.0f;
		}
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxPolyRelated)
	{
		if (!compareMode)
		{
			if (gDepthBuffering)
			{
				Plat_GfxSetDepthTest(0);
				gDepthBuffering = 0;
			}
			return;
		}

		if (!gDepthBuffering)
		{
			Plat_GfxSetDepthTest(1);
			gDepthBuffering = 1;
		}

		if (compareMode != gDepthCompareIndex)
		{
			Plat_GfxSetDepthFunc(compareMode);
			gDepthCompareIndex = compareMode;
			if ( compareMode == 5 || (gFlDepthCompare = 1.0f, compareMode == 7) )
				gFlDepthCompare = 0.0f;
		}
	}
#endif
}

// @Ok
// @Matching
void DXPOLY_SetDepthWriting(bool enabled)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gDxPolyRelated && enabled != gDepthWriting)
	{
		G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZWRITEENABLE, enabled);
		gDepthWriting = enabled;

		char *status = "Enabled";
		if (!enabled)
			status = "Disabled";

		DXERR_printf("Depth Buffer Writes %s.\r\n", status);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxPolyRelated && enabled != gDepthWriting)
	{
		Plat_GfxSetDepthWrite(enabled);
		gDepthWriting = enabled;
	}
#endif
}

// @Ok
// @Matching
void DXPOLY_SetFilterMode(u32 filterIndex)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (filterIndex != gCurrentFilterIndex)
	{
		G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_MAGFILTER, gMagFilters[filterIndex]);
		G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_MINFILTER, gMinFilters[filterIndex]);

		gCurrentFilterIndex = filterIndex;

		char *status = "PointSample";
		if (filterIndex)
			status = "Bilinear";
		DXERR_printf("Filter %s.\r\n", status);
	}
#elif defined(SPIDEY_STANDALONE)
	if (filterIndex != gCurrentFilterIndex)
	{
		Plat_GfxSetFilter(filterIndex);
		gCurrentFilterIndex = filterIndex;
	}
#endif
}

// @Ok
// @Matching
void DXPOLY_SetHUDOffset(i32 hudOffset)
{
	gHudOffset = hudOffset;
	f32 worldBucketCount = (f32)(4096 - hudOffset);
	gFlHudOffset = worldBucketCount / 4096.0f;
}

// @Ok
// @Matching
void DXPOLY_SetOutlineColor(u32 colorArgb)
{
	gDxOutlineColor = colorArgb;
}

// @Ok
// @Matching
void DXPOLY_SetTexture(LPDIRECTDRAWSURFACE7 textureHandle)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (textureHandle != G_DD_SURFACE7)
	{
		HRESULT hr = G_D3DDEVICE7->SetTexture(0, textureHandle);
		D3D_ERROR_LOG_AND_QUIT(hr);
		G_DD_SURFACE7 = textureHandle;
	}
#elif defined(SPIDEY_STANDALONE)
	if (textureHandle != G_DD_SURFACE7)
	{
		Plat_GfxSetTexture(reinterpret_cast<PlatTexture*>(textureHandle));
		G_DD_SURFACE7 = textureHandle;
	}
#endif
}

// @Ok
// @Matching
void initialSettings(void)
{
    printf("initialSettings(void)");
}

void DXPOLY_SetAddressUAndV(DWORD addressU, DWORD addressV)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (addressU != gAddressU)
	{
		G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_ADDRESSU, addressU);
		gAddressU = addressU;
	}

	if (addressV != gAddressV)
	{
		G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_ADDRESSV, addressV);
		gAddressV = addressV;
	}
#elif defined(SPIDEY_STANDALONE)
	if (addressU != gAddressU || addressV != gAddressV)
	{
		Plat_GfxSetAddress(addressU, addressV);
		gAddressU = addressU;
		gAddressV = addressV;
	}
#endif
}

// @Ok
// No standalone PC address (fully inlined into DXPOLY_EndScene, same as
// loadWAV into DXSOUND_Load), not verifiable on its own. Decompiled
// DXPOLY_EndScene itself (0x502A40) this session to check this: the real
// non low graphics render pass walks gSceneBuffer BACKWARDS, from index
// 4096 down to 0 inclusive (a previous session had flipped this to count
// up, that was wrong: the forward 0-to-4096 walk belongs to the LOW
// GRAPHICS branch instead, a different algorithm entirely that calls two
// unnamed helpers (0x514B10, 0x514C60, chosen by a bit in packet->field_8)
// and a third (0x514DA0) per polygon, none of which are in this session's
// assigned range, so that branch is still a stub). The per polygon draw
// sequence in the real (non low graphics) branch does call SetFilterMode
// (unlike DXPOLY_DrawPoly's immediate-draw path, which does not), so this
// part of the translation was already right.
void renderScene(void)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gLowGraphics)
	{
		DXERR_printf("DO ME PLEASE renderScene");
	}
	else
	{
		for (
				i32 i = 4096;
				i >= 0;
				i--)
		{

			DXPOLY* packet = gPolygonQueue.first(i);
			if (gDxPolyRelated && gHudOffset > 0 && i == gHudOffset)
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZENABLE, 0);

			while (packet)
			{
				DXPOLY_SetTexture(packet->textureHandle);
				DXPOLY_SetBlendMode(packet->blendMode);

				DXPOLY_SetAddressUAndV(
						(packet->flags & spidey::rendering::WrapTextureU) ? 1 : 3,
						(packet->flags & spidey::rendering::WrapTextureV) ? 1 : 3);

				DXPOLY_EnableTexAlpha((packet->flags & spidey::rendering::TextureAlpha) != 0);
				DXPOLY_SetFilterMode((packet->flags & spidey::rendering::PointSampling) == 0);

				G_D3DDEVICE7->DrawPrimitive(
						D3DPT_TRIANGLEFAN,
						324,
						&packet->vertices[0],
						packet->vertexCount,
						0);

				packet = packet->next;
			}
		}

		if (gDxPolyRelated && gHudOffset > 0)
			G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZENABLE, 1);
	}
#elif defined(SPIDEY_STANDALONE)
	// SPIDEY_DUMPPOLYS=N (+ SPIDEY_DUMPPOLYS_AT=ms): print every fan of N
	// frames once that time is reached
	if (gDumpFrames < 0)
	{
		const char* d = getenv("SPIDEY_DUMPPOLYS");
		const char* at = getenv("SPIDEY_DUMPPOLYS_AT");
		gDumpFrames = d ? atoi(d) : 0;
		gDumpAt = at ? (u32)atoi(at) : 0;
	}
	i32 dumping = gDumpFrames > 0 && Plat_Ticks() >= gDumpAt;
	if (dumping)
		gDumpFrames--;

	for (i32 i = 4096; i >= 0; i--)
	{
		DXPOLY* packet = gPolygonQueue.first(i);
		if (gDxPolyRelated && gHudOffset > 0 && i == gHudOffset)
			Plat_GfxSetDepthTest(0);

		while (packet)
		{
			if (dumping)
				dumpPoly(packet, i);
			DXPOLY_SetTexture(packet->textureHandle);
			DXPOLY_SetBlendMode(packet->blendMode);
			DXPOLY_SetAddressUAndV(
					(packet->flags & spidey::rendering::WrapTextureU) ? 1 : 3,
					(packet->flags & spidey::rendering::WrapTextureV) ? 1 : 3);
			DXPOLY_EnableTexAlpha((packet->flags & spidey::rendering::TextureAlpha) != 0);
			DXPOLY_SetFilterMode((packet->flags & spidey::rendering::PointSampling) == 0);
			Plat_GfxSetHudView((packet->flags & spidey::rendering::HudSubmission) != 0);
			gDbgFanBlend = packet->blendMode; gDbgFanTex = (i32)(size_t)packet->textureHandle; gDbgFanBucket = i;
			Plat_GfxDrawFan(packet->vertices, packet->vertexCount);
			packet = packet->next;
		}
	}

	if (gDxPolyRelated && gHudOffset > 0)
		Plat_GfxSetDepthTest(1);
	Plat_GfxSetHudView(0);
#endif
}

void validate_DXPOLY(void)
{
	VALIDATE_SIZE(DXPOLY, 0xF0);

	VALIDATE(DXPOLY, next, 0x0);
	VALIDATE(DXPOLY, textureHandle, 0x4);
	VALIDATE(DXPOLY, blendMode, 0x8);
	VALIDATE(DXPOLY, flags, 0xA);

	VALIDATE(DXPOLY, vertexCount, 0xC);
	VALIDATE(DXPOLY, vertices, 0x10);
}

void validate_SDXPolyField(void)
{
	VALIDATE_SIZE(SDXPolyField, 0x1C);

	VALIDATE(SDXPolyField, x, 0x0);
	VALIDATE(SDXPolyField, y, 0x4);
	VALIDATE(SDXPolyField, depth, 0x8);
	VALIDATE(SDXPolyField, reciprocalW, 0xC);
	VALIDATE(SDXPolyField, colorArgb, 0x10);
	VALIDATE(SDXPolyField, u, 0x14);
	VALIDATE(SDXPolyField, v, 0x18);
}
