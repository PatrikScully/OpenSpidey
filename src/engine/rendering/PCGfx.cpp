#include "engine/rendering/PCGfx.h"
#include "engine/rendering/PCTex.h"
#include "engine/rendering/polygon_pipeline.h"
#include "game/application/SpideyDX.h"
#include "game/application/DXinit.h"
#include "game/cinematics/camera.h"
#include "game/ui/mess.h"
#include "engine/rendering/scene_renderer.h"
#include "game/ui/pshell.h"
#include "engine/assets/spool.h"
#include "engine/input/ps2pad.h"
#include "engine/input/PCInput.h"
#include "engine/rendering/m3dinit.h"
#include "engine/rendering/render_buffers.h"

#include <cmath>
#include <cstring>

// my_malloc/my_free live in main.cpp (0x52A227/0x52A3C0, already
// PATCH_PUSH_RET'd there), not declared in any header. Plain extern
// declarations, not a redefinition.
extern void *my_malloc(size_t s);
extern void my_free(void *block);

#ifdef _WIN32
// Copied from G_GAME_RESOLUTION_X/Y in PCGfx_CopyGameResolution (called from
// runtime_patches, inside the game process). Reading the exe address in a
// static initializer made LoadLibrary fail in any other host, e.g. the
// tobey_validator run of the CI ("Couldn't load the file").
EXPORT i32 gAnotherGameResolutionX = 640;
EXPORT i32 gAnotherGameResolutionY = 480;

void PCGfx_CopyGameResolution(void)
{
	gAnotherGameResolutionX = G_GAME_RESOLUTION_X;
	gAnotherGameResolutionY = G_GAME_RESOLUTION_Y;
}
#else
// the exe data block does not exist yet at static init time (it crashed the
// sanity build); RealWinMain/main_standalone set 640x480 anyway
EXPORT i32 gAnotherGameResolutionX = 640;
EXPORT i32 gAnotherGameResolutionY = 480;
#endif

EXPORT i32 gDrawTexture2DRelatedOne;
EXPORT i32 gDrawTexture2DRelatedTwo;

// u8, not i8: submitPoly rebuilds the colour from three table reads shifted
// into place. The original reads the bytes zero extended (0x509400), a
// signed table sign extends any channel of 128 or more into the bytes above
// it and saturates the whole colour (found on the sky dome of level 1).
EXPORT u8 gPcGfxBrightnessValues[256];

EXPORT u8 gProcessTextureRelated;
#ifndef SPIDEY_STANDALONE
EXPORT u16 gChosenBlendingMode;
#else
extern u16 gChosenBlendingMode;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT u16 gProcessedTextureFlags;
#else
extern u16 gProcessedTextureFlags;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT i32 gUseTextureRelated = 0x0FFFFFFFF;
#else
extern i32 gUseTextureRelated;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT DCGfx_BlendingMode gTextureBlendingMode;
#else
extern DCGfx_BlendingMode gTextureBlendingMode;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT i32 gPcGfxBlendModeRelated;
#else
extern i32 gPcGfxBlendModeRelated;
#endif
EXPORT DXPOLY gDxPolys[15360];

#ifndef SPIDEY_STANDALONE
EXPORT f32 gRenderInitOne[3] = {  10.0f, 8048.0f, 276.0f };
#else
extern f32 gRenderInitOne[3];
#endif
#ifndef SPIDEY_STANDALONE
EXPORT f32 gRenderInitTwo[2] = { 8038.0f, 1.9624f };
#else
extern f32 gRenderInitTwo[2];
#endif

#ifndef SPIDEY_STANDALONE
EXPORT i32 gPcGfxDrawRelated = 1;
#else
extern i32 gPcGfxDrawRelated;
#endif
EXPORT i32 gPcGfxSlotNumber =  0x0FFFFFFFF;

#ifndef SPIDEY_STANDALONE
EXPORT u8 gIsRenderSettingE = 1;
#else
extern u8 gIsRenderSettingE;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT u8 gNonRendderSettingE;
#else
extern u8 gNonRendderSettingE;
#endif

#ifndef SPIDEY_STANDALONE
u8 gSceneRelated;
#else
extern u8 gSceneRelated;
#endif

EXPORT f32 gZLayerFurthest;
EXPORT f32 gZLayerNearest;

EXPORT f32 gFlFoggingParamOne = 100000.0f;
EXPORT f32 gFlFoggingParamTwo = 100005.0f;
EXPORT u32 gU32FoggingParamThree;

#ifndef SPIDEY_STANDALONE
u8 gBFoggingRelated;
#else
extern u8 gBFoggingRelated;
#endif

#ifndef SPIDEY_STANDALONE
u32 gPcGfxSkyColor;
#else
extern u32 gPcGfxSkyColor;
#endif

EXPORT i32 gEndSceneRelated = -1;
#ifndef SPIDEY_STANDALONE
EXPORT i32 gEndSceneRelatedTwo;
#else
extern i32 gEndSceneRelatedTwo;
#endif

// 0xAC08F0, set to 1 by PCGfx_ClipTriToNearPlane whenever it actually cuts a
// triangle. Not in the maintainer's IDB globals list, name is our guess.
u8 gTriWasClipped;

// @Ok
// @Matching
i32 acG2Write(void*, void*, i32)
{
	return 0;
}

// @Ok
// @Matching
i32 amHeapAlloc(u32**, i32, i32, i32, i32)
{
	return 0;
}

// @Ok
// @Matching
i32 acDspSetMixerChannel(i32, i32, i32, i32)
{
	return 0;
}

// @Ok
// @Matching
i32 amHeapFree(i32)
{
	return 0;
}

// The 4 per channel fog blend tables built by PCGfx_BeginScene's inlined
// setupFog block (see the comment on PCGfx_BeginScene below). Each is
// [row=0..255][col=0..511]: row is a raw brightness/distance level, col is
// a fog blend factor (col/511.0). gFogTableA is read with row = the color's
// top (alpha) byte and is just a clamped copy of the row index (alpha is
// never faded toward the fog color); gFogTableR/G/B are read with row = the
// matching color byte and hold lerp(row, fogColorChannel, col/511.0)
// clamped to a byte. Real game addresses, confirmed by decompiling
// PCGfx_BeginScene's fill loop at 0x505e00: gFogTableA=0x6BC6C0 (gets the
// row passthrough), gFogTableR=0x6FC6DC (gets the red blend), then
// gFogTableG=0x71C75C, gFogTableB=0x6DC6C0 (R and A were swapped in an
// earlier version of this comment; the repo arrays below are file local
// statics either way, so this only matters once/if this code gets bound to
// the real addresses for hooking).
static u8 gFogTableR[256][512];
static u8 gFogTableA[256][512];
static u8 gFogTableG[256][512];
static u8 gFogTableB[256][512];

// 0x568160, f32, default 1.0 in the binary's data segment (no write site
// found anywhere in SpideyPC.exe via IDA xrefs, only read by gsub_506D70 and
// gsub_509400's inlined copy of it, and by the sky color gamma correction
// below). Scales the 1/z depth value before it is compared against
// gFlFoggingParamOne/Two. Sits in the same settings data block as
// gGameResolutionX/Y and gIsRenderSettingE; tentative name.
EXPORT f32 gPcGfxFogDepthScale = 1.0f;

// Defined further down with PCGfx_SetBrightness, at its real address 0x5681A0.
extern EXPORT f32 gPcGfxBrightnessPower[8];

// @Ok
// setupFog does not exist as a separate function in the binary. It has only
// one call site anywhere (this one, guarded by gBFoggingRelated), in the
// same TU, so MSVC6 inlines it completely: confirmed via IDA decompile of
// 0x505E00, the whole 4 table fog blend build runs inline with no call to a
// separate function. Same class of issue as the documented Screen_UpdateFades
// mislabeling (see CLAUDE.md). Fix applied: setupFog's body is pasted in
// below in place of the old setupFog() call, and the standalone setupFog
// function/declaration is removed (PCGfx.h/PCGfx.cpp).
// Confirmed field for field against the IDA decompile of 0x505e00 this
// session: the row/col loop builds the same 4 channel blend (row passthrough
// for alpha, lerp(row,fogChannel,col/511) for r/g/b, same clamp shape via a
// pointer range check that is dead in practice since row never leaves
// 0..255), the sky color gamma correction matches channel for channel
// (pow(channel/255, 1/gPcGfxBrightnessPower[gBrightnessRelated])*255+0.5,
// alpha passthrough, same b|(g<<8)|(r<<16)|(a<<24) pack), and the trailing
// gZLayerNearest/gZLayerFurthest inits (0.0099999998 / -0.2) match
// PCGfx_IncZLayerNearest/Furthest's confirmed +0.001/-10.0 step sizes
// (dword_73C77C=gZLayerNearest, flt_AC07B8=gZLayerFurthest).
void PCGfx_BeginScene(u32 a1, i32 a2)
{
	if (G_SCENE_RELATED)
		return;

	if (G_BFOGGING_RELATED)
	{
		f32 fogRf = (f32)((gU32FoggingParamThree >> 16) & 0xFF);
		f32 fogGf = (f32)((gU32FoggingParamThree >> 8) & 0xFF);
		f32 fogBf = (f32)(gU32FoggingParamThree & 0xFF);

		for (i32 row = 0; row < 256; row++)
		{
			for (i32 col = 0; col < 512; col++)
			{
				f32 f = (f32)col / 511.0f;
				f32 rowF = (f32)row;
				f32 base = (1.0f - f) * rowF;

				i32 r = (i32)(f * fogRf + base);
				i32 g = (i32)(f * fogGf + base);
				i32 b = (i32)(f * fogBf + base);
				i32 a = row;

				if (a > 255) a = -1; else if (a < 0) a = 0;
				if (r > 255) r = -1; else if (r < 0) r = 0;
				if (g > 255) g = -1; else if (g < 0) g = 0;
				if (b > 255) b = -1; else if (b < 0) b = 0;

				gFogTableA[row][col] = (u8)a;
				gFogTableR[row][col] = (u8)r;
				gFogTableG[row][col] = (u8)g;
				gFogTableB[row][col] = (u8)b;
			}
		}

		f32 invPower = 1.0f / gPcGfxBrightnessPower[gBrightnessRelated];

		u8 skyB = (u8)G_PCGFX_SKY_COLOR;
		u8 skyA = (u8)(G_PCGFX_SKY_COLOR >> 24);
		u8 skyG = (u8)(G_PCGFX_SKY_COLOR >> 8);
		u8 skyR = (u8)(G_PCGFX_SKY_COLOR >> 16);

		i32 newR = (i32)(pow((f64)((f32)skyR / 255.0f), (f64)invPower) * 255.0 + 0.5);
		i32 newG = (i32)(pow((f64)((f32)skyG / 255.0f), (f64)invPower) * 255.0 + 0.5);
		i32 newB = (i32)(pow((f64)((f32)skyB / 255.0f), (f64)invPower) * 255.0 + 0.5);

		G_PCGFX_SKY_COLOR = newB | ((newG | ((newR | (skyA << 8)) << 8)) << 8);

		DXPOLY_SetBackgroundColor(G_PCGFX_SKY_COLOR);
		G_BFOGGING_RELATED = 0;
	}

	PCGfx_ProcessTexture(0, -1, DCGfx_BlendingMode_0);
	DXPOLY_BeginScene();
	G_SCENE_RELATED = 1;
	gZLayerNearest = 0.0099999998;
	gZLayerFurthest = -0.2;
}

#ifdef SPIDEY_STANDALONE
// @Bogus
// SPIDEY_NOFOG=1: skip the per vertex fog blend (debugging aid)
static i32 PCGfx_DbgNoFog(void)
{
	static i32 v = -1;
	if (v < 0)
		v = getenv("SPIDEY_NOFOG") ? 1 : 0;
	return v;
}
#endif

// @Ok
// Functional: fog color blend, logic verified against Hex-Rays at 0x506d70.
// Blends a1 (ABGR color) toward the fog color for a2 (1/z depth) via the 4
// tables filled in PCGfx_BeginScene; returns b|(g<<8)|(r<<16)|(a<<24).
// Note: the 4 gFogTable* are repo-local static arrays (original keeps them
// at fixed addresses 0x6BC6C0/0x6DC6C0/0x6FC6DC/0x71C75C). Neither this
// nor PCGfx_BeginScene is hooked yet, so the original uses its own tables;
// if either gets hooked, bind the tables to those addresses first.
EXPORT u32 gsub_506D70(u32 a1, f32 a2)
{
#ifdef SPIDEY_STANDALONE
	if (PCGfx_DbgNoFog())
		return a1;
#endif
	f32 depth = gPcGfxFogDepthScale * a2;
	if (depth < gFlFoggingParamOne)
		return a1;

	f32 t = (depth - gFlFoggingParamOne) / (gFlFoggingParamTwo - gFlFoggingParamOne);
	if (t > 1.0f)
		t = 1.0f;

	i32 col = (i32)(t * 511.0f);

	u8 r = gFogTableR[(u8)(a1 >> 16)][col];
	u8 a = gFogTableA[(u8)(a1 >> 24)][col];
	u8 g = gFogTableG[(u8)(a1 >> 8)][col];
	u8 b = gFogTableB[(u8)a1][col];

	return b | (g << 8) | (r << 16) | (a << 24);
}

// @Ok
// Builds 3 temporary _DXVERT vertices from raw tagKMVERTEX3 records addressed
// through the u16 index array (3 indices per triangle), including a per
// channel color brighten step (kept as is when the low 3 bytes of the color
// are already 0, else (c>>1 & 0x7F7F7F) + 0x0F0F0F with the top byte kept),
// calls PCGfx_ClipTriToNearPlane on them, then processes verts[0..2]
// (verts[3] only if clipping produced a 4th vertex) with a per vertex fog
// color blend and a fog depth remap identical in shape to PCGfx_DrawQuad2D's
// existing (v24 - gRenderInitOne[0]) / gRenderInitTwo[0] idiom, before
// calling submitPoly with count 3 or 4. Skips the whole triangle (no
// submitPoly call) when verts[0] is null, which happens when
// PCGfx_ClipTriToNearPlane's countBehind == 3 case zeroed all 3 verts.
// The 2 print_if_false asserts and their strings ("verts[1] is null!",
// "verts[2] is null!" at 0x5682A0/0x56828C) are confirmed from the binary.
// Confirmed field for field against the IDA decompile of 0x506980 this
// session, including the bias term (gPcGfxBlendModeRelated*gRenderInitTwo[1]
// when gPcGfxBlendModeRelated!=0 and !gLowGraphics) and the fog depth remap
// order (field_C computed from the raw field_8 BEFORE field_8 gets
// overwritten with the remapped value). Fixed a bug this session: the
// field_14/field_18 *= field_C step is gated on gLowGraphics being TRUE, not
// on !gLowGraphics as the previous version had it (same fix applied to
// PCGfx_DrawQPoly3D and PCGfx_DrawTPoly3D, which share this exact idiom).
// gsub_506D70, gRenderInitOne/Two, gPcGfxBlendModeRelated, gNonRendderSettingE,
// gPcGfxDrawRelated and gEndSceneRelatedTwo's game addresses (0x506d70,
// 0x56817C/0x568184/0x568190/0x568194, 0xAC08E0, 0xAC08D0, 0x568178,
// 0xAC08F4) all matched an existing repo global 1:1 against
// idb_globals.txt. tagKMVERTEX3's field layout is a positional guess (see
// PCGfx.h) and the a2 parameter is genuinely never read in the
// disassembly, kept unused to match.
void PCGfx_ClipSendIndexedVertList(tagKMVERTEX3 const *vertArray, i32 a2, u16 const *indices, i32 indexCount)
{
	_DXVERT temp[3];
	_DXVERT *verts[4];
	_DXVERT out0, out1;
	_DXVERT *out[2] = { &out0, &out1 };

	u16 const *idx = indices;
	u16 const *end = indices + indexCount;

	if (idx == end)
		return;

	do
	{
		for (i32 k = 0; k < 3; k++)
		{
			tagKMVERTEX3 const *src = &vertArray[*idx];
			idx++;

			temp[k].x = src->field_4;
			temp[k].y = src->field_8;
			temp[k].depth = 1.0f / src->field_C;
			temp[k].u = src->field_10;
			temp[k].v = src->field_14;

			u32 c = src->field_18;
			if ((c & 0xFFFFFF) == 0)
			{
				temp[k].colorArgb = c;
			}
			else
			{
				temp[k].colorArgb = (c & 0xFF000000) | (((c >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);
			}
		}

		verts[0] = &temp[0];
		verts[1] = &temp[1];
		verts[2] = &temp[2];

		gTriWasClipped = 0;
		PCGfx_ClipTriToNearPlane(verts, out);

		if (verts[0])
		{
			f32 bias = 0.0f;
			if (gPcGfxBlendModeRelated && !gLowGraphics)
				bias = (f32)gPcGfxBlendModeRelated * gRenderInitTwo[1];

			i32 count = verts[3] ? 4 : 3;

			for (i32 k = 0; k < count; k++)
			{
				_DXVERT *v = verts[k];

				if (gNonRendderSettingE)
					v->colorArgb = gsub_506D70(v->colorArgb, v->depth);

				v->reciprocalW = gRenderInitOne[2] / v->depth;
				v->depth = (bias + v->depth - gRenderInitOne[0]) / gRenderInitTwo[0];

				if (gLowGraphics)
				{
					v->u *= v->reciprocalW;
					v->v *= v->reciprocalW;
				}
			}

			print_if_false(verts[1] != 0, "verts[1] is null!");
			print_if_false(verts[2] != 0, "verts[2] is null!");

			gPcGfxDrawRelated |= 4;

			submitPoly(verts, count);
		}
	}
	while (idx != end);
}

// @Ok
// verts holds the 3 input triangle vertex pointers, plus a spare 4th slot
// (verts[3]) used when clipping turns the triangle into a quad. out[0]/out[1]
// are the two spare _DXVERT slots the caller passes in to receive the newly
// interpolated vertices. Confirmed against IDA decompile of 0x506e40:
// prevIdx/nextIdx match the game's dword_53C850/dword_53C84C tables exactly
// ({2,0,1} and {1,2,0}, read directly from the binary), and the countBehind
// 1/2/3 branches match field for field, including the shift direction for
// countBehind==1 and the ZCLIP_VERT argument order for both branches.
// Residue is only the countBehind dispatch shape (original tests it with a
// dec/je chain, ours with separate ifs), a codegen detail, not a logic
// difference.
void PCGfx_ClipTriToNearPlane(_DXVERT **verts, _DXVERT *const *out)
{
	static const i32 prevIdx[3] = {2, 0, 1};
	static const i32 nextIdx[3] = {1, 2, 0};

	i32 countBehind = 0;
	i32 behindIdx = -1;
	verts[3] = 0;
	i32 frontIdx = -1;

	for (i32 i = 2; i >= 0; i--)
	{
		if (verts[i]->depth < gRenderInitOne[0])
		{
			behindIdx = i;
			countBehind++;
		}
		else
		{
			frontIdx = i;
		}
	}

	if (countBehind == 1)
	{
		ZCLIP_VERT(out[0], verts[prevIdx[behindIdx]], verts[behindIdx], gRenderInitOne[0]);
		ZCLIP_VERT(out[1], verts[nextIdx[behindIdx]], verts[behindIdx], gRenderInitOne[0]);

		for (i32 i = 2; i > behindIdx; i--)
			verts[i + 1] = verts[i];

		verts[behindIdx] = out[0];
		verts[behindIdx + 1] = out[1];
		gTriWasClipped = 1;
	}
	if (countBehind == 2)
	{
		ZCLIP_VERT(out[0], verts[frontIdx], verts[prevIdx[frontIdx]], gRenderInitOne[0]);
		ZCLIP_VERT(out[1], verts[frontIdx], verts[nextIdx[frontIdx]], gRenderInitOne[0]);

		verts[prevIdx[frontIdx]] = out[0];
		verts[nextIdx[frontIdx]] = out[1];
		gTriWasClipped = 1;
	}
	if (countBehind == 3)
	{
		verts[2] = 0;
		verts[1] = 0;
		verts[0] = 0;
	}
}

// @Ok
// @Validate
void PCGfx_DoModelPreview(void)
{
	i32 totalSomething = 0;
	CSuper* SuperItem = 0;
	CSuper* SuperItemNext = 0;
	u8 doModelSwap = 0;
	DWORD modelTickUpdate = 0;

	PShell_Initialise();
	i32 freeIndex = 0;
	for (; freeIndex < MAXPSX; freeIndex++)
	{
		if (!PSXRegion[freeIndex].Filename[0])
			break;
	}


	i32 v24[40];

	for (i32 j = 0; j < freeIndex; j++)
	{
		// @FIXME
		// remove
		break;

		i32 v8 = PSXRegion[j].pPSX[2];
		v24[j] = v8;
		totalSomething += v8;
	}

	//if (totalSomething && freeIndex)
	if (true)
	{
		PCGfx_SetSkyColor(0xFF800080);
		i32 idx = 0;

		i32 v9;
		for (v9 = 0; v9 < freeIndex; v9++)
		{
			//@FIXME
			break;
			if (v24[v9] > 0)
			{
				if (!PSXRegion[v9].IsSuper)
				{
					SuperItem = reinterpret_cast<CSuper*>(PSXRegion[v9].pSuper);
					SuperItemNext = reinterpret_cast<CSuper*>(SuperItem->mNextItem);
					SuperItem->mNextItem = 0;
				}

				SuperItem = createSuperItem(PSXRegion[v9].pSuper);
			}
		}

		//@FIXME
		/*
		G_MIKE_CAMERA[0].Position.vx = SuperItem->mPos.vx >> 12;
		G_MIKE_CAMERA[0].Position.vy = SuperItem->mPos.vy >> 12;
		G_MIKE_CAMERA[0].Position.vz = (SuperItem->mPos.vz >> 12) - 1;
		*/

		G_MIKE_CAMERA[0].Angles.vx = 0;
		G_MIKE_CAMERA[0].Angles.vy = 0;
		G_MIKE_CAMERA[0].Angles.vz = 0;
		G_MIKE_CAMERA[0].Style = 0;

		i32 stop = 0;
		while (!stop)
		{
			Pad_Update();
			if (G_SCONTROL[0].Left.Pressed)
			{
				G_MIKE_CAMERA[0].Angles.vy -= 16;
				G_MIKE_CAMERA[0].Angles.vy &= 0xFFF;
			}
			else if (G_SCONTROL[0].Right.Pressed)
			{
				G_MIKE_CAMERA[0].Angles.vy -= 16;
				G_MIKE_CAMERA[0].Angles.vy &= 0xFFF;
			}

			if (G_SCONTROL[0].Up.Pressed)
			{
				if (!PCINPUT_IsKeyPressed(0x42, 0) && !PCINPUT_IsKeyPressed(0x36, 0))
				{
					i32 v14 = G_MIKE_CAMERA[0].Angles.vy & 0xFFF;
					G_MIKE_CAMERA[0].Position.vx += (32 * G_RCOSSIN_TBL[v14].sin) >> 12;
					G_MIKE_CAMERA[0].Position.vz += (32 * G_RCOSSIN_TBL[v14].cos) >> 12;
					G_MIKE_CAMERA[0].Position.vy -= (32 * G_RCOSSIN_TBL[G_MIKE_CAMERA[0].Angles.vx & 0xFFF].sin) >> 12;
				}
				else
				{
					G_MIKE_CAMERA[0].Angles.vx += 16;
					G_MIKE_CAMERA[0].Angles.vx &= 0xFFF;
				}
			}
			else if (G_SCONTROL[0].Down.Pressed)
			{
				if (!PCINPUT_IsKeyPressed(0x2Au, 0) && !PCINPUT_IsKeyPressed(0x36u, 0))
				{
					 G_MIKE_CAMERA[0].Angles.vx = (G_MIKE_CAMERA[0].Angles.vx - 16) & 0xFFF;
				}
				else
				{
					i32 v15 = G_MIKE_CAMERA[0].Angles.vy & 0xFFF;
					G_MIKE_CAMERA[0].Position.vx -= (32 * G_RCOSSIN_TBL[v15].sin) >> 12;
					G_MIKE_CAMERA[0].Position.vy += (32 * G_RCOSSIN_TBL[G_MIKE_CAMERA[0].Angles.vx & 0xFFF].sin) >> 12;
					G_MIKE_CAMERA[0].Position.vz -= (32 * G_RCOSSIN_TBL[v15].cos) >> 12;
				}
			}

			if (GetTickCount() - modelTickUpdate > 250)
			{
				if (G_SCONTROL[0].Square.Pressed)
				{
					u8 IsSuper = PSXRegion[v9].IsSuper;
					if (idx && !IsSuper)
					{
						idx--;
					}
					else
					{
						do
						{
							if (--v9 < 0)
								v9 = freeIndex - 1;
						}
						while(v24[v9] <= 0);

						if (IsSuper)
							idx = 0;
						else
							idx = v24[v9] - 1;
					}

					doModelSwap = 1;
				}
				else if (G_SCONTROL[0].Circle.Pressed)
				{
					idx = (idx + 1) % v24[v9];
					if (!idx || PSXRegion[v9].IsSuper)
					{
						do
						{
							v9 = (v9 + 1) % freeIndex;
						}
						while (v24[v9] <= 0);
					}
					doModelSwap = 1;
				}
			}

			
			if (PCINPUT_IsKeyPressed(0x10, 0))
			{
				stop = 1;
			}
			else
			{
				// @FIXME
				/*
				if (doModelSwap)
				{
					if (PSXRegion[SuperItem->mRegion].IsSuper)
					{
						delete SuperItem;
					}
					else
					{
						SuperItem->mNextItem = SuperItemNext;
					}

					if (PSXRegion[v9].IsSuper)
					{
						SuperItem = createSuperItem(&PSXRegion[v9].pSuper[idx]);
					}
					else
					{
						SuperItem = reinterpret_cast<CSuper*>(&PSXRegion[v9].pSuper[idx]);
						SuperItemNext = reinterpret_cast<CSuper*>(SuperItem->mNextItem);
						SuperItem->mNextItem = 0;
					}

					G_MIKE_CAMERA[0].Position.vx = SuperItem->mPos.vx >> 12;
					G_MIKE_CAMERA[0].Position.vy = SuperItem->mPos.vy >> 12;
					G_MIKE_CAMERA[0].Position.vz = (SuperItem->mPos.vz >> 12) - 1;

					G_MIKE_CAMERA[0].Angles.vx = 0;
					G_MIKE_CAMERA[0].Angles.vy = 0;
					G_MIKE_CAMERA[0].Angles.vz = 0;

					modelTickUpdate = GetTickCount();
					doModelSwap = 0;
				}
				*/

				PCGfx_RenderModelPreview(SuperItem, PSXRegion[v9].Filename, idx);
			}
		}

		if (PSXRegion[v9].IsSuper)
		{
			delete SuperItem;
		}
		else
		{
			SuperItem->mNextItem = SuperItemNext;
		}
	}
}

// @Ok
// Builds one _DXVERT from a corner record shaped like tagKMVERTEX3
// (field_4..field_18 only, field_0 unused). Runs the same field_8/field_C
// formulas as PCGfx_DrawTPoly3D, plus gsub_506D70's real fog blend math
// inlined here directly (confirmed via IDA: no call instruction at 0x509400,
// same table reads as gsub_506D70 with a2 = 1/field_C, not the transformed
// field_8). Everything up through the color brighten step matches the
// original (instruction count 102 both sides, confirmed identical). The
// gLowGraphics gated field_14/field_18 *= field_C step at the end matches
// PCGfx_DrawTPoly3D's confirmed decompile (0x5081f0): the scale applies
// when gLowGraphics is true, not false.
EXPORT void gsub_509400(tagKMVERTEX3 const *corner, _DXVERT *out)
{
	out->x = corner->field_4;
	out->y = corner->field_8;
	out->depth = (1.0f / corner->field_C - gRenderInitOne[0]) / gRenderInitTwo[0];
	out->reciprocalW = gRenderInitOne[2] * corner->field_C;
	out->u = corner->field_10;
	out->v = corner->field_14;

	u32 c = corner->field_18;
	if ((c & 0xFFFFFF) == 0)
		out->colorArgb = c;
	else
		out->colorArgb = (c & 0xFF000000) | (((c >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);

	if (gNonRendderSettingE
#ifdef SPIDEY_STANDALONE
			&& !PCGfx_DbgNoFog()
#endif
			)
	{
		u32 a1 = corner->field_18;
		f32 depth = gPcGfxFogDepthScale * (1.0f / corner->field_C);

		if (depth >= gFlFoggingParamOne)
		{
			f32 t = (depth - gFlFoggingParamOne) / (gFlFoggingParamTwo - gFlFoggingParamOne);
			if (t > 1.0f)
				t = 1.0f;

			i32 col = (i32)(t * 511.0f);

			u8 r = gFogTableR[(u8)(a1 >> 16)][col];
			u8 a = gFogTableA[(u8)(a1 >> 24)][col];
			u8 g = gFogTableG[(u8)(a1 >> 8)][col];
			u8 b = gFogTableB[(u8)a1][col];

			a1 = b | (g << 8) | (r << 16) | (a << 24);
		}

		out->colorArgb = a1;
	}

	if (gLowGraphics)
	{
		out->u = out->reciprocalW * out->u;
		out->v = out->v * out->reciprocalW;
	}
}

// @Ok
// Draws a thick line as a quad with square/extended end caps. Reworked this
// session after tracing the raw disasm instruction by instruction (the
// pseudocode reuses locals too much to follow directly): dy=y2-y1, dx=x2-x1,
// length=sqrt(dx^2+dy^2) (calls _sqrt at 0x529A44). If length != 0.0f, a
// vector PARALLEL to the line, (offX,offY) = (width*0.5f/length)*(dx,dy), is
// used both to retract endpoint 1 backward (base1 = (x1,y1)-off) and extend
// endpoint 2 forward (base2 = (x2,y2)+off) along the line direction (square
// end cap extension, not just a perpendicular width quad); if length==0.0f
// the disasm falls back to offX=0, offY=width*0.5f. The perpendicular half
// width offset used for the two long edges is perp=(offY,-offX) (a 90
// degree rotation of the parallel offset). The 4 corners, confirmed by
// tracing each one back to its source registers, are: base1+perp (z1+bias,
// color1, uv (1,0)), base2+perp (z2+bias, color2, uv (1,1)), base2-perp
// (z2+bias, color2, uv (0,1)), base1-perp (z1+bias, color1, uv (0,0)), in
// that order (matches submitPoly's expected quad winding). Both z1 and z2
// get a flat bias of 7.0710678f (=10/sqrt(2), constant at 0x53C844) added
// before being stored; the previous version of this function did not
// reproduce the end cap extension, the z bias, or the per corner UV
// coordinates, only a plain perpendicular-offset quad with no cap
// extension.
void PCGfx_DrawLine(f32 x1, f32 y1, f32 z1, u32 color1, f32 x2, f32 y2, f32 z2, u32 color2, f32 width)
{
	f32 dy = y2 - y1;
	f32 dx = x2 - x1;
	f32 length = sqrt(dx * dx + dy * dy);

	f32 offX, offY;
	if (length != 0.0f)
	{
		f32 s = (width * 0.5f) / length;
		offX = s * dx;
		offY = s * dy;
	}
	else
	{
		offX = 0.0f;
		offY = width * 0.5f;
	}

	f32 perpX = offY;
	f32 perpY = -offX;

	f32 base1X = x1 - offX;
	f32 base1Y = y1 - offY;
	f32 base2X = x2 + offX;
	f32 base2Y = y2 + offY;

	f32 zBias1 = z1 + 7.0710678f;
	f32 zBias2 = z2 + 7.0710678f;

	tagKMVERTEX3 corners[4];
	memset(corners, 0, sizeof(corners));

	corners[0].field_4 = base1X + perpX;
	corners[0].field_8 = base1Y + perpY;
	corners[0].field_C = zBias1;
	corners[0].field_10 = 1.0f;
	corners[0].field_14 = 0.0f;
	corners[0].field_18 = color1;

	corners[1].field_4 = base2X + perpX;
	corners[1].field_8 = base2Y + perpY;
	corners[1].field_C = zBias2;
	corners[1].field_10 = 1.0f;
	corners[1].field_14 = 1.0f;
	corners[1].field_18 = color2;

	corners[2].field_4 = base2X - perpX;
	corners[2].field_8 = base2Y - perpY;
	corners[2].field_C = zBias2;
	corners[2].field_10 = 0.0f;
	corners[2].field_14 = 1.0f;
	corners[2].field_18 = color2;

	corners[3].field_4 = base1X - perpX;
	corners[3].field_8 = base1Y - perpY;
	corners[3].field_C = zBias1;
	corners[3].field_10 = 0.0f;
	corners[3].field_14 = 0.0f;
	corners[3].field_18 = color1;

	_DXVERT vtx[4];
	_DXVERT *verts[4] = { &vtx[0], &vtx[1], &vtx[2], &vtx[3] };

	gsub_509400(&corners[0], &vtx[0]);
	gsub_509400(&corners[1], &vtx[1]);
	gsub_509400(&corners[2], &vtx[2]);
	gsub_509400(&corners[3], &vtx[3]);

	submitPoly(verts, 4);
}

// @Ok
// Same shape as PCGfx_DrawTPoly2D (screen space poly, own DXPOLY build,
// direct DXPOLY_DrawPoly call, manual gDxPolys pointer walk), just 4
// vertices instead of 3 (field_C=4, loop count 4). Reuses the zOffset
// preamble fix found on DrawTPoly2D this session (if/else computing v13 in
// each branch so the compiler CSEs the shared gRenderInitTwo[1]*zOffset
// multiply after the compare, `if (zOffset >= 0.0f)` branch order). One
// notable difference from DrawTPoly2D confirmed in the disasm: the
// print_if_false("invalid zOffset!") call here has NO "push edi" register
// save before it (DrawTPoly2D's does), consistent with different register
// pressure between the two functions, not a copy paste error.
// cmpsum: 207 mnemonic diffs at 0x507910. The zOffset preamble and the
// print_if_false call both matched cleanly this time (confirms the
// DrawTPoly2D fixes generalise and that function's residue really was
// register pressure specific, not a fundamental block). The residue here
// starts well into the per vertex field copy block: our field_0/field_4/
// field_14/field_18/field_10/field_8/field_C assignment order per vertex
// (declared in that order for all 4 vertices) does not match the original's
// scattered store order (it interleaves stores across all 4 vertices'
// structs rather than finishing one vertex before the next, visible in the
// disasm as stores to +0x34,+0x38,...+0x88 in a non-monotonic sequence).
// One attempt at the vertex struct fields (declaration order matching field
// layout, not the original's interleaved store order); short of the 10+
// hypotheses per cluster bar for a >1000 byte function, logged in
// pcgfx.attempts.md.
void PCGfx_DrawQPoly2D(
		f32 x0, f32 y0, f32 u0, f32 v0, u32 color0,
		f32 x1, f32 y1, f32 u1, f32 v1, u32 color1,
		f32 x2, f32 y2, f32 u2, f32 v2, u32 color2,
		f32 x3, f32 y3, f32 u3, f32 v3, u32 color3,
		f32 zOffset)
{
	gPcGfxDrawRelated &= 0xFFFFFFFB;

	if (zOffset <= 6.0f)
		gPcGfxSlotNumber = (i32)zOffset;

	f32 v13;
	if (zOffset >= 0.0f)
		v13 = gRenderInitTwo[1] * zOffset + gRenderInitOne[0];
	else
		v13 = gRenderInitTwo[1] * zOffset + gRenderInitOne[1];

	f32 v24 = v13;
	print_if_false(v24 > 0.0f, "invalid zOffset!");

	f32 v27 = (v24 - gRenderInitOne[0]) / gRenderInitTwo[0];
	f32 v32 = gRenderInitOne[2] / v24;

	SDXPolyField dxPolyFields[4];

	dxPolyFields[0].x = x0;
	dxPolyFields[0].y = y0;
	dxPolyFields[0].u = u0;
	dxPolyFields[0].v = v0;
	dxPolyFields[0].colorArgb = color0;
	dxPolyFields[0].depth = v27;
	dxPolyFields[0].reciprocalW = v32;

	dxPolyFields[1].x = x1;
	dxPolyFields[1].y = y1;
	dxPolyFields[1].u = u1;
	dxPolyFields[1].v = v1;
	dxPolyFields[1].colorArgb = color1;
	dxPolyFields[1].depth = v27;
	dxPolyFields[1].reciprocalW = v32;

	// The callers hand the corners in PSX POLY_FT4 order (top left, top
	// right, bottom left, bottom right). The poly is drawn as a triangle
	// fan, so the original (0x507910) stores corner 3 into the third vertex
	// and corner 2 into the fourth. Without the swap every HUD quad and the
	// compass came out as a bow tie.
	dxPolyFields[2].x = x3;
	dxPolyFields[2].y = y3;
	dxPolyFields[2].u = u3;
	dxPolyFields[2].v = v3;
	dxPolyFields[2].colorArgb = color3;
	dxPolyFields[2].depth = v27;
	dxPolyFields[2].reciprocalW = v32;

	dxPolyFields[3].x = x2;
	dxPolyFields[3].y = y2;
	dxPolyFields[3].u = u2;
	dxPolyFields[3].v = v2;
	dxPolyFields[3].colorArgb = color2;
	dxPolyFields[3].depth = v27;
	dxPolyFields[3].reciprocalW = v32;

	if (gEndSceneRelatedTwo >= 15360)
	{
		gEndSceneRelatedTwo++;
		return;
	}

	DXPOLY *p = &gDxPolys[gEndSceneRelatedTwo++];
	i32 blendMode = gPcGfxBlendModeRelated;
	f32 fogMax = 0.0f;

	if (gLowGraphics)
	{
		p->textureHandle = (LPDIRECTDRAWSURFACE7)(i32)G_USE_TEXTURE_RELATED;
		*(i32*)&p->blendMode = gPcGfxDrawRelated;
		if (G_USE_TEXTURE_RELATED < 0)
			*(i32*)&p->blendMode = gPcGfxDrawRelated & 0xFFFFFFFB;

		p->vertexCount = 4;

		for (i32 i = 0; i < 4; i++)
		{
			SDXPolyField *dst = &p->vertices[i];
			memcpy(dst, &dxPolyFields[i], sizeof(SDXPolyField));

			if (!(p->blendMode & 4))
				dst->reciprocalW = 1.0f; // rhw, the original stores to +0xC (0x5071B0: *((DWORD*)v6 + 1) with v6 = dst + 8)

			i32 alpha;
			if (gProcessTextureRelated)
				alpha = 128;
			else
				alpha = (dst->colorArgb >> 24) & 0xFF;

			dst->colorArgb =
				gPcGfxBrightnessValues[dst->colorArgb & 0xFF] |
				gPcGfxBrightnessValues[(dst->colorArgb >> 8) & 0xFF] << 8 |
				gPcGfxBrightnessValues[(dst->colorArgb >> 16) & 0xFF] << 16 |
				alpha << 24;

			if (dst->depth < 0.0f)
			{
				dst->depth = 0.0f;
			}
			else if (dst->depth > 0.99989998f)
			{
				dst->depth = 0.99989998f;
				fogMax = dst->depth;
			}
			else if (fogMax < dst->depth)
			{
				fogMax = dst->depth;
			}
		}
	}
	else
	{
		LPDIRECTDRAWSURFACE7 Direct3DTexture;
		if (G_USE_TEXTURE_RELATED < 0)
			Direct3DTexture = 0;
		else
			Direct3DTexture = PCTex_GetDirect3DTexture(G_USE_TEXTURE_RELATED);
		p->textureHandle = Direct3DTexture;
		p->blendMode = gChosenBlendingMode;
		p->flags = gProcessedTextureFlags;
		p->vertexCount = 4;

		for (i32 i = 0; i < 4; i++)
		{
			SDXPolyField *dst = &p->vertices[i];
			memcpy(dst, &dxPolyFields[i], sizeof(SDXPolyField));

			i32 alpha;
			if (gProcessTextureRelated)
				alpha = 128;
			else
				alpha = (dst->colorArgb >> 24) & 0xFF;

			dst->colorArgb =
				gPcGfxBrightnessValues[dst->colorArgb & 0xFF] |
				gPcGfxBrightnessValues[(dst->colorArgb >> 8) & 0xFF] << 8 |
				gPcGfxBrightnessValues[(dst->colorArgb >> 16) & 0xFF] << 16 |
				alpha << 24;

			if (dst->depth < 0.0f)
			{
				dst->depth = 0.0f;
			}
			else if (dst->depth > 0.99989998f)
			{
				dst->depth = 0.99989998f;
				fogMax = dst->depth;
			}
			else if (fogMax < dst->depth)
			{
				fogMax = dst->depth;
			}
		}

		if (gChosenBlendingMode)
		{
			blendMode = 0;
		}
	}

	DXPOLY_DrawPoly(p, gPcGfxSlotNumber, blendMode, fogMax);
	gPcGfxSlotNumber = -1;
}

// @Ok
// A world space quad (4x (x,y,z,w,uv,color)), split into 2 triangles and
// each clipped against the near plane before submitting. Confirmed from
// IDA decompile of 0x508550: PCGfx_ClipTriToNearPlane (0x506E40) is called
// TWICE, and the per vertex post clip processing (fog blend via
// gsub_506D70 gated on gNonRendderSettingE, field_C=gRenderInitOne[2]/
// field_8, field_8 remapped to (field_8-gRenderInitOne[0])/gRenderInitTwo[0],
// field_14/field_18 *= field_C gated on !gLowGraphics) is EXACTLY
// PCGfx_ClipSendIndexedVertList's post clip loop. The pre clip vertex build
// also matches ClipSendIndexedVertList (not DrawTPoly3D): field_8 starts as
// 1.0f/z (raw invZ, BEFORE the clip), and the color brighten step runs
// before the clip with no fog blend yet.
// Fixed this session (was wrong): the SECOND triangle is (v0, v3, v2), not
// (v0, v2, v3), and v2 gets rebuilt fresh from its raw args for the second
// triangle, same as v0 and v3. Confirmed via decompile of the two helpers:
// sub_508B40(rawCornerStruct, dest) at 0x508B40 is called once with vertex3's
// raw args and once with vertex2's raw args right before the second
// PCGfx_ClipTriToNearPlane call; it builds a DXVERT the same way gsub_509400
// does (x/y/invZ/w/uv/brightened color) but without the fog blend or low
// graphics scale steps, i.e. exactly what temp[0]/temp[3]'s rebuild already
// does inline here. The earlier version of this function reused triangle 1's
// already post processed temp[2] (with field_8 already remapped and fog
// blended) as the second triangle's v1, which is wrong: the original always
// rebuilds all 3 second-triangle vertices from the untouched raw function
// arguments. print_if_false is only called around the first triangle's
// submit (confirmed, no calls to it appear around the second clip/submit in
// the disasm).
void PCGfx_DrawQPoly3D(
		f32 x0, f32 y0, f32 z0, f32 w0, f32 uv0, u32 color0,
		f32 x1, f32 y1, f32 z1, f32 w1, f32 uv1, u32 color1,
		f32 x2, f32 y2, f32 z2, f32 w2, f32 uv2, u32 color2,
		f32 x3, f32 y3, f32 z3, f32 w3, f32 uv3, u32 color3)
{
	_DXVERT temp[4];
	_DXVERT *verts[4];
	_DXVERT out0, out1;
	_DXVERT *out[2] = { &out0, &out1 };

	gPcGfxDrawRelated |= 4;

	temp[0].x = x0;
	temp[0].y = y0;
	temp[0].depth = 1.0f / z0;
	temp[0].u = w0;
	temp[0].v = uv0;
	if ((color0 & 0xFFFFFF) == 0)
		temp[0].colorArgb = color0;
	else
		temp[0].colorArgb = (color0 & 0xFF000000) | (((color0 >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);

	temp[1].x = x1;
	temp[1].y = y1;
	temp[1].depth = 1.0f / z1;
	temp[1].u = w1;
	temp[1].v = uv1;
	if ((color1 & 0xFFFFFF) == 0)
		temp[1].colorArgb = color1;
	else
		temp[1].colorArgb = (color1 & 0xFF000000) | (((color1 >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);

	temp[2].x = x2;
	temp[2].y = y2;
	temp[2].depth = 1.0f / z2;
	temp[2].u = w2;
	temp[2].v = uv2;
	if ((color2 & 0xFFFFFF) == 0)
		temp[2].colorArgb = color2;
	else
		temp[2].colorArgb = (color2 & 0xFF000000) | (((color2 >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);

	verts[0] = &temp[0];
	verts[1] = &temp[1];
	verts[2] = &temp[2];

	PCGfx_ClipTriToNearPlane(verts, out);

	if (verts[0])
	{
		i32 count = verts[3] ? 4 : 3;

		for (i32 k = 0; k < count; k++)
		{
			_DXVERT *v = verts[k];

			if (gNonRendderSettingE)
				v->colorArgb = gsub_506D70(v->colorArgb, v->depth);

			v->reciprocalW = gRenderInitOne[2] / v->depth;
			v->depth = (v->depth - gRenderInitOne[0]) / gRenderInitTwo[0];

			if (gLowGraphics)
			{
				v->u *= v->reciprocalW;
				v->v *= v->reciprocalW;
			}
		}

		print_if_false(verts[1] != 0, "verts[1] is null!");
		print_if_false(verts[2] != 0, "verts[2] is null!");

		gPcGfxDrawRelated |= 4;

		submitPoly(verts, count);
	}

	// second triangle (v0, v3, v2): all 3 vertices are rebuilt fresh from
	// the raw function args (v0 and v3 were already overwritten in place by
	// the first triangle's post clip loop above; v2 is rebuilt too even
	// though nothing wrote over temp[2], because the original does not
	// reuse it, see the function comment).
	temp[0].x = x0;
	temp[0].y = y0;
	temp[0].depth = 1.0f / z0;
	temp[0].u = w0;
	temp[0].v = uv0;
	if ((color0 & 0xFFFFFF) == 0)
		temp[0].colorArgb = color0;
	else
		temp[0].colorArgb = (color0 & 0xFF000000) | (((color0 >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);

	temp[2].x = x2;
	temp[2].y = y2;
	temp[2].depth = 1.0f / z2;
	temp[2].u = w2;
	temp[2].v = uv2;
	if ((color2 & 0xFFFFFF) == 0)
		temp[2].colorArgb = color2;
	else
		temp[2].colorArgb = (color2 & 0xFF000000) | (((color2 >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);

	temp[3].x = x3;
	temp[3].y = y3;
	temp[3].depth = 1.0f / z3;
	temp[3].u = w3;
	temp[3].v = uv3;
	if ((color3 & 0xFFFFFF) == 0)
		temp[3].colorArgb = color3;
	else
		temp[3].colorArgb = (color3 & 0xFF000000) | (((color3 >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);

	verts[0] = &temp[0];
	verts[1] = &temp[3];
	verts[2] = &temp[2];

	PCGfx_ClipTriToNearPlane(verts, out);

	if (verts[0])
	{
		i32 count = verts[3] ? 4 : 3;

		for (i32 k = 0; k < count; k++)
		{
			_DXVERT *v = verts[k];

			if (gNonRendderSettingE)
				v->colorArgb = gsub_506D70(v->colorArgb, v->depth);

			v->reciprocalW = gRenderInitOne[2] / v->depth;
			v->depth = (v->depth - gRenderInitOne[0]) / gRenderInitTwo[0];

			if (gLowGraphics)
			{
				v->u *= v->reciprocalW;
				v->v *= v->reciprocalW;
			}
		}

		submitPoly(verts, count);
	}
}

// @Ok
// Confirmed against IDA decompile of 0x507470. Fixed a real bug this
// session: the zOffset preamble computed v13*a10 (a10 squared) instead of
// gRenderInitTwo[1]*a10; the original is
// `a10 < 0.0f ? gRenderInitTwo[1]*a10 + gRenderInitOne[1] : gRenderInitTwo[1]*a10 + gRenderInitOne[0]`.
// This is almost certainly why the whole rest of the function diverged
// (wrong zOffset feeds every later field_8/field_C computation). The low
// graphics branch (mirrors submitPoly's low graphics branch) and the
// hardware branch (PCTex_GetDirect3DTexture, gChosenBlendingMode,
// gProcessedTextureFlags) both checked field for field against the
// decompile and match.
void PCGfx_DrawQuad2D(
		f32 a1,
		f32 a2,
		f32 a3,
		f32 a4,
		f32 a5,
		f32 a6,
		f32 a7,
		f32 a8,
		u32 color,
		f32 a10,
		bool)
{
	gPcGfxDrawRelated &= 0xFFFFFFFB;

	if (a10 <= 6.0f)
		gPcGfxSlotNumber = a10;

	f32 v13;
	if (a10 < 0.0f)
		v13 = gRenderInitTwo[1] * a10 + gRenderInitOne[1];
	else
		v13 = gRenderInitTwo[1] * a10 + gRenderInitOne[0];

	f32 v24 = v13;
	print_if_false(v24 > 0.0f, "invalid zOffset!");

	SDXPolyField *pDxPolyFields[4];
	SDXPolyField dxPolyFields[4];

	dxPolyFields[0].v = a6;
	dxPolyFields[0].y = a2;
	dxPolyFields[1].y = a2;
	dxPolyFields[0].colorArgb = color;
	dxPolyFields[1].colorArgb = color;
	dxPolyFields[2].colorArgb = color;
	dxPolyFields[3].colorArgb = color;
	dxPolyFields[0].x = a1;
	dxPolyFields[0].u = a5;
	dxPolyFields[3].x = a1;
	dxPolyFields[3].u = a5;
	pDxPolyFields[2] = &dxPolyFields[2];

	f32 v27 = (v24 - gRenderInitOne[0]) / gRenderInitTwo[0];
	dxPolyFields[0].depth = v27;
	dxPolyFields[1].depth = v27;
	dxPolyFields[2].depth = v27;
	dxPolyFields[3].depth = v27;

	pDxPolyFields[0] = dxPolyFields;
	pDxPolyFields[3] = &dxPolyFields[3];

	f32 v32 = gRenderInitOne[2] / v24;
	dxPolyFields[0].reciprocalW = v32;
	dxPolyFields[1].reciprocalW = v32;
	dxPolyFields[2].reciprocalW = v32;
	dxPolyFields[3].reciprocalW = v32;

	f32 v28 = a1 + a3;
	dxPolyFields[1].x = v28;
	dxPolyFields[2].x = v28;
	pDxPolyFields[1] = &dxPolyFields[1];

	f32 v25 = a5 + a7;
	dxPolyFields[1].u = v25;
	dxPolyFields[1].v = a6;
	dxPolyFields[2].u = v25;

	f32 v29 = a2 + a4;
	dxPolyFields[2].y = v29;
	dxPolyFields[3].y = v29;

	f32 v30 = a6 + a8;
	dxPolyFields[2].v = v30;
	dxPolyFields[3].v = v30;

	if (gEndSceneRelatedTwo >= 15360)
	{
		gEndSceneRelatedTwo++;
		return;
	}

	f32 v26 = 0.0;
	DXPOLY* v16 = &gDxPolys[gEndSceneRelatedTwo++];
	i32 v31 = gPcGfxBlendModeRelated;

	if (gLowGraphics)
	{
		v16->textureHandle = (LPDIRECTDRAWSURFACE7)(i32)G_USE_TEXTURE_RELATED;
		*(i32*)&v16->blendMode = gPcGfxDrawRelated;
		if (G_USE_TEXTURE_RELATED < 0)
			*(i32*)&v16->blendMode = gPcGfxDrawRelated & 0xFFFFFFFB;

		v16->vertexCount = 4;

		SDXPolyField **v21 = pDxPolyFields;
		SDXPolyField *v22 = v16->vertices;

		for (i32 i = 0; i < 4; i++)
		{
			memcpy(&v22[i], v21[i], sizeof(SDXPolyField));

			if (!(v16->blendMode & 4))
				v22[i].y = 1.0f;

			i32 v23;
			if (gProcessTextureRelated)
				v23 = 128;
			else
				v23 = (v22[i].colorArgb >> 24) & 0xFF;

			v22[i].colorArgb =
				gPcGfxBrightnessValues[v22[i].colorArgb & 0xFF] |
				gPcGfxBrightnessValues[(v22[i].colorArgb >> 8) & 0xFF] << 8 |
				gPcGfxBrightnessValues[(v22[i].colorArgb >> 16) & 0xFF] << 16 |
				v23 << 24;

			if (v22[i].depth < 0.0f)
			{
				v22[i].depth = 0.0f;
			}
			else if (v22[i].depth > 0.99989998f)
			{
				v22[i].depth = 0.99989998f;
				v26 = v22[i].depth;
			}
			else if (v26 < v22[i].depth)
			{
				v26 = v22[i].depth;
			}
		}
	}
	else
	{
		LPDIRECTDRAWSURFACE7 Direct3DTexture;
		if (G_USE_TEXTURE_RELATED < 0)
			Direct3DTexture = 0;
		else
			Direct3DTexture = PCTex_GetDirect3DTexture(G_USE_TEXTURE_RELATED);
		v16->textureHandle = Direct3DTexture;
		v16->blendMode = gChosenBlendingMode;
		v16->flags = gProcessedTextureFlags;
		v16->vertexCount = 4;

		SDXPolyField **v21 = pDxPolyFields;
		SDXPolyField *v22 = v16->vertices;

		for (i32 i = 0; i < 4; i++)
		{
			memcpy(&v22[i], v21[i], sizeof(SDXPolyField));
			
			i32 v23;
			if (gProcessTextureRelated)
				v23 = 128;
			else
				v23 = (v22[i].colorArgb >> 24) & 0xFF;

			v22[i].colorArgb =
				gPcGfxBrightnessValues[v22[i].colorArgb & 0xFF] |
				gPcGfxBrightnessValues[(v22[i].colorArgb >> 8) & 0xFF] << 8 |
				gPcGfxBrightnessValues[(v22[i].colorArgb >> 16) & 0xFF] << 16 |
				v23 << 24;

			if (v22[i].depth < 0.0f)
			{
				v22[i].depth = 0.0f;
			}
			else if (v22[i].depth > 0.99989998f)
			{
				v22[i].depth = 0.99989998f;
				v26 = v22[i].depth;
			}
			else if (v26 < v22[i].depth)
			{
				v26 = v22[i].depth;
			}
		}
		if ( gChosenBlendingMode )
		{
			v31 = 0;
		}
	}

	DXPOLY_DrawPoly(v16, gPcGfxSlotNumber, v31, v26);
	gPcGfxSlotNumber = -1;
}

// @Ok
// A screen space triangle (3x (x,y,u,v,color) plus one shared zOffset),
// builds its own DXPOLY inline and calls DXPOLY_DrawPoly (0x503100) directly
// instead of going through submitPoly, confirmed from the disasm: manual
// pointer walk into gDxPolys (ebp = &gDxPolys[gEndSceneRelatedTwo] via
// idx*0xF0), not the array-of-pointers shape submitPoly/DrawQuad2D use. The
// zOffset preamble (gPcGfxDrawRelated &=~4, conditional gPcGfxSlotNumber set,
// v13=gRenderInitTwo[1]*zOffset + gRenderInitOne[0 or 1] depending on sign,
// print_if_false "invalid zOffset!" at 0x568304) and the per vertex color
// brighten/fog clamp loop are the exact same idioms as PCGfx_DrawQuad2D and
// submitPoly (reused verbatim), field_8/field_C are shared across all 3
// vertices (single v27/v32, matches DrawQuad2D, this is a 2D/flat triangle
// not a perspective one). One real difference from DrawQuad2D confirmed by
// the disasm: the low graphics branch has the "if (!(mBlendMode&4))
// field_4=1.0f" step per vertex, the hardware branch here does NOT (DrawQuad2D
// has it in both branches), kept as is since the bytes say so. 0x71C720/
// 0x71C734 read as gChosenBlendingMode/gProcessedTextureFlags (u16 loads,
// matches their repo types) rather than new globals. 0x50F3C0 is
// PCTex_GetDirect3DTexture (same call shape as DrawQuad2D's texture lookup),
// not a new unnamed helper as an earlier session guessed.
// cmpsum: 208 mnemonic diffs at 0x507da0. 4 hypotheses tried, 2 confirmed
// fixes kept: (1) writing the zOffset sign check as a genuine if/else
// computing v13 in each branch let the compiler CSE the shared
// gRenderInitTwo[1]*zOffset multiply out after the compare, matching the
// original's compare-then-multiply order (a plain "cache the multiply in a
// local, then branch on a separate bool" version materialized the bool into
// al with extra movs and did not match); (2) flipping the branch to
// `if (zOffset >= 0.0f)` (add gRenderInitOne[0] first) instead of
// `if (zOffset < 0.0f)` matched the original's fall through/jump sense
// exactly, fixing one instruction level diff. The remaining residue starts
// at the print_if_false("invalid zOffset!") call: our build still calls it
// out of line here (register allocation differs enough that edi is not live
// the same way, so the call shape and everything downstream shifts), which
// is the repo wide print_if_false inlining problem CLAUDE.md documents
// under "Matching discipline" (static in export.h, gets inlined at some call
// sites and not others depending on register pressure); fixing that needs a
// real out of line print_if_false, not a change local to this function.
// 4 hypotheses is short of the 10+ per cluster bar for a >1000 byte
// function, logged in pcgfx.attempts.md.
void PCGfx_DrawTPoly2D(
		f32 x0, f32 y0, f32 u0, f32 v0, u32 color0,
		f32 x1, f32 y1, f32 u1, f32 v1, u32 color1,
		f32 x2, f32 y2, f32 u2, f32 v2, u32 color2,
		f32 zOffset)
{
	gPcGfxDrawRelated &= 0xFFFFFFFB;

	if (zOffset <= 6.0f)
		gPcGfxSlotNumber = (i32)zOffset;

	f32 v13;
	if (zOffset >= 0.0f)
		v13 = gRenderInitTwo[1] * zOffset + gRenderInitOne[0];
	else
		v13 = gRenderInitTwo[1] * zOffset + gRenderInitOne[1];

	f32 v24 = v13;
	print_if_false(v24 > 0.0f, "invalid zOffset!");

	f32 v27 = (v24 - gRenderInitOne[0]) / gRenderInitTwo[0];
	f32 v32 = gRenderInitOne[2] / v24;

	SDXPolyField dxPolyFields[3];

	dxPolyFields[0].x = x0;
	dxPolyFields[0].y = y0;
	dxPolyFields[0].u = u0;
	dxPolyFields[0].v = v0;
	dxPolyFields[0].colorArgb = color0;
	dxPolyFields[0].depth = v27;
	dxPolyFields[0].reciprocalW = v32;

	dxPolyFields[1].x = x1;
	dxPolyFields[1].y = y1;
	dxPolyFields[1].u = u1;
	dxPolyFields[1].v = v1;
	dxPolyFields[1].colorArgb = color1;
	dxPolyFields[1].depth = v27;
	dxPolyFields[1].reciprocalW = v32;

	dxPolyFields[2].x = x2;
	dxPolyFields[2].y = y2;
	dxPolyFields[2].u = u2;
	dxPolyFields[2].v = v2;
	dxPolyFields[2].colorArgb = color2;
	dxPolyFields[2].depth = v27;
	dxPolyFields[2].reciprocalW = v32;

	if (gEndSceneRelatedTwo >= 15360)
	{
		gEndSceneRelatedTwo++;
		return;
	}

	DXPOLY *p = &gDxPolys[gEndSceneRelatedTwo++];
	i32 blendMode = gPcGfxBlendModeRelated;
	f32 fogMax = 0.0f;

	if (gLowGraphics)
	{
		p->textureHandle = (LPDIRECTDRAWSURFACE7)(i32)G_USE_TEXTURE_RELATED;
		*(i32*)&p->blendMode = gPcGfxDrawRelated;
		if (G_USE_TEXTURE_RELATED < 0)
			*(i32*)&p->blendMode = gPcGfxDrawRelated & 0xFFFFFFFB;

		p->vertexCount = 3;

		for (i32 i = 0; i < 3; i++)
		{
			SDXPolyField *dst = &p->vertices[i];
			memcpy(dst, &dxPolyFields[i], sizeof(SDXPolyField));

			if (!(p->blendMode & 4))
				dst->reciprocalW = 1.0f; // rhw, the original stores to +0xC (0x5071B0: *((DWORD*)v6 + 1) with v6 = dst + 8)

			i32 alpha;
			if (gProcessTextureRelated)
				alpha = 128;
			else
				alpha = (dst->colorArgb >> 24) & 0xFF;

			dst->colorArgb =
				gPcGfxBrightnessValues[dst->colorArgb & 0xFF] |
				gPcGfxBrightnessValues[(dst->colorArgb >> 8) & 0xFF] << 8 |
				gPcGfxBrightnessValues[(dst->colorArgb >> 16) & 0xFF] << 16 |
				alpha << 24;

			if (dst->depth < 0.0f)
			{
				dst->depth = 0.0f;
			}
			else if (dst->depth > 0.99989998f)
			{
				dst->depth = 0.99989998f;
				fogMax = dst->depth;
			}
			else if (fogMax < dst->depth)
			{
				fogMax = dst->depth;
			}
		}
	}
	else
	{
		LPDIRECTDRAWSURFACE7 Direct3DTexture;
		if (G_USE_TEXTURE_RELATED < 0)
			Direct3DTexture = 0;
		else
			Direct3DTexture = PCTex_GetDirect3DTexture(G_USE_TEXTURE_RELATED);
		p->textureHandle = Direct3DTexture;
		p->blendMode = gChosenBlendingMode;
		p->flags = gProcessedTextureFlags;
		p->vertexCount = 3;

		for (i32 i = 0; i < 3; i++)
		{
			SDXPolyField *dst = &p->vertices[i];
			memcpy(dst, &dxPolyFields[i], sizeof(SDXPolyField));

			i32 alpha;
			if (gProcessTextureRelated)
				alpha = 128;
			else
				alpha = (dst->colorArgb >> 24) & 0xFF;

			dst->colorArgb =
				gPcGfxBrightnessValues[dst->colorArgb & 0xFF] |
				gPcGfxBrightnessValues[(dst->colorArgb >> 8) & 0xFF] << 8 |
				gPcGfxBrightnessValues[(dst->colorArgb >> 16) & 0xFF] << 16 |
				alpha << 24;

			if (dst->depth < 0.0f)
			{
				dst->depth = 0.0f;
			}
			else if (dst->depth > 0.99989998f)
			{
				dst->depth = 0.99989998f;
				fogMax = dst->depth;
			}
			else if (fogMax < dst->depth)
			{
				fogMax = dst->depth;
			}
		}

		if (gChosenBlendingMode)
		{
			blendMode = 0;
		}
	}

	DXPOLY_DrawPoly(p, gPcGfxSlotNumber, blendMode, fogMax);
	gPcGfxSlotNumber = -1;
}

// @Ok
// A world space triangle (3x (x,y,z,w,uv,color)) fed through the same fog
// pipeline as PCGfx_ClipSendIndexedVertList (no near plane clip here, just
// straight per vertex processing), then submitPoly(verts,3). Confirmed field
// mapping from the disasm: field_0=x, field_4=y, field_14=w, field_18=uv,
// field_8=(1/z - gRenderInitOne[0])/gRenderInitTwo[0], field_C=gRenderInitOne[2]*z
// (the ORIGINAL z, not 1/z, re-read from the arg after the fdiv already
// consumed it, confirmed for all 3 vertices), field_10=color after the same
// brighten step as ClipSendIndexedVertList ((c>>1&0x7F7F7F)+0x0F0F0F, skipped
// when the low 3 bytes are already 0), then gsub_506D70(1/z,color) when
// gNonRendderSettingE, then field_14/field_18 *= field_C when !gLowGraphics.
// gsub_506D70 is now a real decompiled function (see its @Ok comment above),
// not a forward stub, so this uses the real fog table math.
void PCGfx_DrawTPoly3D(
		f32 x1, f32 y1, f32 z1, f32 w1, f32 uv1, u32 color1,
		f32 x2, f32 y2, f32 z2, f32 w2, f32 uv2, u32 color2,
		f32 x3, f32 y3, f32 z3, f32 w3, f32 uv3, u32 color3)
{
	_DXVERT vtx[3];
	_DXVERT *verts[3];

	verts[0] = &vtx[0];
	verts[1] = &vtx[1];
	gPcGfxDrawRelated |= 4;
	verts[2] = &vtx[2];

	{
		f32 invZ = 1.0f / z1;
		vtx[0].x = x1;
		vtx[0].y = y1;
		vtx[0].u = w1;
		vtx[0].v = uv1;
		vtx[0].depth = (invZ - gRenderInitOne[0]) / gRenderInitTwo[0];
		vtx[0].reciprocalW = gRenderInitOne[2] * z1;
		if ((color1 & 0xFFFFFF) == 0)
			vtx[0].colorArgb = color1;
		else
			vtx[0].colorArgb = (color1 & 0xFF000000) | (((color1 >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);
		if (gNonRendderSettingE)
			vtx[0].colorArgb = gsub_506D70(vtx[0].colorArgb, invZ);
		if (gLowGraphics)
		{
			vtx[0].u *= vtx[0].reciprocalW;
			vtx[0].v *= vtx[0].reciprocalW;
		}
	}

	{
		f32 invZ = 1.0f / z2;
		vtx[1].x = x2;
		vtx[1].y = y2;
		vtx[1].u = w2;
		vtx[1].v = uv2;
		vtx[1].depth = (invZ - gRenderInitOne[0]) / gRenderInitTwo[0];
		vtx[1].reciprocalW = gRenderInitOne[2] * z2;
		if ((color2 & 0xFFFFFF) == 0)
			vtx[1].colorArgb = color2;
		else
			vtx[1].colorArgb = (color2 & 0xFF000000) | (((color2 >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);
		if (gNonRendderSettingE)
			vtx[1].colorArgb = gsub_506D70(vtx[1].colorArgb, invZ);
		if (gLowGraphics)
		{
			vtx[1].u *= vtx[1].reciprocalW;
			vtx[1].v *= vtx[1].reciprocalW;
		}
	}

	{
		f32 invZ = 1.0f / z3;
		vtx[2].x = x3;
		vtx[2].y = y3;
		vtx[2].u = w3;
		vtx[2].v = uv3;
		vtx[2].depth = (invZ - gRenderInitOne[0]) / gRenderInitTwo[0];
		vtx[2].reciprocalW = gRenderInitOne[2] * z3;
		if ((color3 & 0xFFFFFF) == 0)
			vtx[2].colorArgb = color3;
		else
			vtx[2].colorArgb = (color3 & 0xFF000000) | (((color3 >> 1) & 0x7F7F7Fu) + 0x0F0F0Fu);
		if (gNonRendderSettingE)
			vtx[2].colorArgb = gsub_506D70(vtx[2].colorArgb, invZ);
		if (gLowGraphics)
		{
			vtx[2].u *= vtx[2].reciprocalW;
			vtx[2].v *= vtx[2].reciprocalW;
		}
	}

	submitPoly(verts, 3);
}

// @Ok
// Session 2026-08-30: reversed the viewport clamp branch via Hex-Rays on
// 0x5064a0. The four globals were already named: dword_AC08E8/dword_AC08EC
// are the existing gDrawTexture2DRelatedOne/gDrawTexture2DRelatedTwo (an
// unconditional min-x/min-y clamp on the draw origin, already used below).
// dword_73C794/dword_73C790 match idb_globals.txt verbatim:
// gAnotherGameResolutionX/gAnotherGameResolutionY (already declared at the
// top of this file, unused until now) are a max-x/max-y clamp on the far
// edge of the rect, applied only when the caller passes a6 & 1 (viewport
// clip flag).
// The disasm shape: the right/bottom edge is computed from the ORIGINAL
// (unclamped) x/a3, then clamped against gAnotherGameResolutionX/Y only if
// (a6 & 1). u0/v0 are how much of the texture got cut off by the min-x/
// min-y clamp (always applied); uScale/vScale are the remaining visible UV
// extent up to the (possibly max-clamped) right/bottom edge, minus u0/v0.
// The on-screen rect's left/top always comes from clampedLeft/clampedTop *
// scaleX/scaleY (scaleX/scaleY = gGameResolutionX/Xres, gGameResolutionY/
// Yres, same idiom as PCGfx_UseTexture's caller PCPanel_DrawTexturedPoly).
// The right/bottom screen edge has two paths keyed on the SAME a6 & 1 bit:
// with the flag set it is clampedRight/clampedBottom * scaleX/scaleY; with
// it clear it is screenLeft/screenTop plus adjusted_width/height *
// gGameResolutionX/640 (resp. /480), a literal 640x480 divide (matches the
// same idiom already in PCInput.cpp's mouse-hotspot code, not Xres/Yres) --
// this is a real quirk of the original, reproduced as-is, not "fixed".
// Also found and fixed while tracing this: the z-layer select below had
// a6 & 8 and the "neither" case swapped (a6 & 8 should pass a7 straight
// through; "neither 2 nor 8" should read/advance gZLayerNearest, matching
// sub_505E00 calls in the disasm at 0x506830, not 0x506827), and v25's
// type was i32 where PCGfx_GetZLayerFurthest/Nearest return f32 -- storing
// the float return into an i32 local performs an actual float->int
// conversion (destroying the value, e.g. -0.2f truncates to 0), not a
// bit-preserving reinterpret; fixed by making it f32 throughout.
// Verified: MSVC6 clean build, cmpsum.sh sanity pass (see commit), no
// zero-diff attempted per this session's functional-decomp bar.
void PCGfx_DrawTexture2D(
		i32 a1,
		i32 x,
		i32 a3,
		f32 drawScale,
		u32 color,
		u32 a6,
		f32 a7)
{
	print_if_false(drawScale > 0.0f, "Improbable draw scale");
	print_if_false(drawScale < 256.0f, "Improbable draw scale");

	i32 textureWidth;
	i32 textureHeight;

	PCTex_GetTextureSize(a1, &textureWidth, &textureHeight);

	if (textureWidth <= gMaxTextureWidth && textureHeight <= gTextureHeight)
	{
		i32 adjusted_width = ((f32)textureWidth * drawScale);
		i32 adjusted_height = ((f32)textureHeight * drawScale);

		if (adjusted_width && adjusted_height)
		{
			PCGfx_UseTexture(a1,
					a6 & 4 ? DCGfx_BlendingMode_2 : DCGfx_BlendingMode_0);

			f32 TextureWScale = PCTex_GetTextureWScale(a1);
			f32 TextureHScale = PCTex_GetTextureHScale(a1);

			// Unclamped rect edges, in design-resolution pixel units.
			i32 right = x + adjusted_width;
			i32 bottom = a3 + adjusted_height;

			// Min-x/min-y clamp on the draw origin. Always applied,
			// independent of the a6 & 1 viewport-clip flag.
			i32 clampedLeft = x;
			if (clampedLeft < gDrawTexture2DRelatedOne)
			{
				clampedLeft = gDrawTexture2DRelatedOne;
			}

			i32 clampedTop = a3;
			if (clampedTop < gDrawTexture2DRelatedTwo)
			{
				clampedTop = gDrawTexture2DRelatedTwo;
			}

			// Max-x/max-y clamp on the far edge. Only applied when the
			// caller asks for viewport clipping (a6 & 1).
			i32 clampedRight = right;
			i32 clampedBottom = bottom;
			if (a6 & 1)
			{
				if (clampedRight > gAnotherGameResolutionX)
				{
					clampedRight = gAnotherGameResolutionX;
				}
				if (clampedBottom > gAnotherGameResolutionY)
				{
					clampedBottom = gAnotherGameResolutionY;
				}
			}

			// UV offset (how much got cut off by the min clamp) and the
			// full visible UV extent up to the (possibly max-clamped)
			// right/bottom edge; uScale/vScale is what remains once the
			// min-clamp offset is removed.
			f32 u0 = (f32)(clampedLeft - x) * TextureWScale / (f32)adjusted_width;
			f32 v0 = (f32)(clampedTop - a3) * TextureHScale / (f32)adjusted_height;
			f32 uFull = (f32)(clampedRight - x) * TextureWScale / (f32)adjusted_width;
			f32 vFull = (f32)(clampedBottom - a3) * TextureHScale / (f32)adjusted_height;
			f32 uScale = uFull - u0;
			f32 vScale = vFull - v0;

			f32 scaleX = (f32)G_GAME_RESOLUTION_X / (f32)G_XRES;
			f32 scaleY = (f32)G_GAME_RESOLUTION_Y / (f32)G_YRES;

			i32 screenLeft = (i32)((f32)clampedLeft * scaleX);
			i32 screenTop = (i32)((f32)clampedTop * scaleY);

			i32 screenRight;
			i32 screenBottom;
			if (a6 & 1)
			{
				screenRight = (i32)((f32)clampedRight * scaleX);
				screenBottom = (i32)((f32)clampedBottom * scaleY);
			}
			else
			{
				screenRight = screenLeft + adjusted_width * G_GAME_RESOLUTION_X / 640;
				screenBottom = screenTop + adjusted_height * G_GAME_RESOLUTION_Y / 480;
			}

			f32 z;
			if (a6 & 2)
			{
				z = PCGfx_GetZLayerFurthest();
				PCGfx_IncZLayerFurthest();
			}
			else if (a6 & 8)
			{
				z = a7;
			}
			else
			{
				z = PCGfx_GetZLayerNearest();
				PCGfx_IncZLayerNearest();
			}

			PCGfx_DrawQuad2D(
					(f32)screenLeft,
					(f32)screenTop,
					(f32)(screenRight - screenLeft),
					(f32)(screenBottom - screenTop),
					u0,
					v0,
					uScale,
					vScale,
					color,
					z,
					0);
		}
	}
	else
	{
		i32 x_off = x;
		i32 y_off = a3;

		print_if_false(x_off == 0, "Split texture drawn with x != 0");
		i32 splitCount = PCTex_GetTextureSplitCount(a1);

		for (i32 i = 0; i < splitCount; i++)
		{
			i32 TextureSplitID = PCTex_GetTextureSplitID(a1, i);

			i32 cur_width;
			i32 cur_height;
			PCTex_GetTextureSize(TextureSplitID, &cur_width, &cur_height);
			PCGfx_DrawTexture2D(TextureSplitID, x_off, y_off, drawScale, color, a6, a7);

			x_off += cur_width;
			if (x_off >= textureWidth)
			{
				x_off = 0;
				y_off = cur_height;
			}
		}
	}
}

// @Ok
// @Matching
// @Note powerpc has fps counter here and fog level
 void PCGfx_EndScene(i32 a1)
{
	if (G_SCENE_RELATED)
	{
		DXPOLY_EndScene(a1 != 0);
		PCGfx_ProcessTexture(0, -1, DCGfx_BlendingMode_0);
		gEndSceneRelated = -1;
		G_SCENE_RELATED = 0;
		gEndSceneRelatedTwo = 0;
	}
}

// @Ok
// @Matching
void PCGfx_Exit(void)
{
	PCGfx_ProcessTexture(0, -1, DCGfx_BlendingMode_0);
	PCTex_ReleaseAllTextures();
	print_if_false(PCTex_CountActiveTextures() == 0, "some textures still allocated!");
}

// @Ok
f32 PCGfx_GetZLayerFurthest(void)
{
	if ( !G_SCENE_RELATED )
		PCGfx_BeginScene(3, -1);

	return gZLayerFurthest;
}

// @Ok
f32 PCGfx_GetZLayerNearest(void)
{
	if ( !G_SCENE_RELATED )
		PCGfx_BeginScene(3, -1);

	return gZLayerNearest;
}

// @Ok
void PCGfx_IncZLayerFurthest(void)
{
	gZLayerFurthest -= 10.0f;
}

// @Ok
void PCGfx_IncZLayerNearest(void)
{
	gZLayerNearest += 0.001f;
}

// @Ok
void PCGfx_InitAtStart(void)
{
	DXPOLY_SetOutlineColor(0xFF00FF00);
	DXPOLY_SetHUDOffset(7);
	PCGfx_ProcessTexture(0, -1, DCGfx_BlendingMode_0);
	PCGfx_SetBrightness(gBrightnessRelated);
}

// @Ok
// @Matching
u8 PCGfx_IsInScene(void)
{
	return G_SCENE_RELATED;
}

EXPORT i32 gBlendingModes[DCGfx_BlendingMode_MAX + 1] =
{
	0, 1, 2, 3, 4
};

// @Ok
// Functional: logic verified line by line against Hex-Rays at 0x5062c0.
// The 6 mnemonic diffs from the byte-match phase are MSVC scheduling of
// (gChosenBlendingMode-1)<<4 (subtract-then-shift vs shift-then-subtract);
// the logic is equivalent.
void PCGfx_ProcessTexture(
		_tagKMSTRIPHEAD *,
		i32 a2,
		DCGfx_BlendingMode a3)
{
	i32 TextureFlags = PCTex_GetTextureFlags(a2);
	gProcessTextureRelated = (TextureFlags & 0x1000) != 0 && (a3 == DCGfx_BlendingMode_0 || a3 == DCGfx_BlendingMode_1);

	i32 curBlendingMode;
	if (a3 || a2 < 0 || !PCTex_TextureHasAlpha(a2))
	{
		curBlendingMode = gBlendingModes[a3];
	}
	else
	{
		curBlendingMode = gProcessTextureRelated ? 1 : 5;
	}

	gChosenBlendingMode = curBlendingMode;
	gNonRendderSettingE = (TextureFlags & 0x20) == 0 ? gIsRenderSettingE : 0;

	if (gLowGraphics)
	{
		gPcGfxDrawRelated &= 0xFFFFFF85;

		if (a2 >= 0)
		{
			gPcGfxDrawRelated |= 2;

			if (PCTex_TextureHasAlpha(a2))
				gPcGfxDrawRelated |= 0x40;
		}

		if (gChosenBlendingMode >= 1 && gChosenBlendingMode <= 4)
		{
			gPcGfxDrawRelated |= ((gChosenBlendingMode - 1) << 4) | 8;
		}
	}
	else if (a2 < 0)
	{
		DXPOLY_SetTexture(0);
		gProcessedTextureFlags = 0;
	}
	else
	{
		IDirectDrawSurface7* Direct3DTexture = PCTex_GetDirect3DTexture(a2);
		DXPOLY_SetTexture(Direct3DTexture);
		gProcessedTextureFlags = 0;

		if (PCTex_TextureHasAlpha(a2))
			gProcessedTextureFlags |= 8;

		i32 v9 = PCTex_GetTextureFlags(a2);
		if ((v9 & 8) != 0)
			gProcessedTextureFlags |= 0x10;

		if ((v9 & 0x20) != 0)
			gProcessedTextureFlags |= 1;

		if ((v9 & 2) == 0)
			gProcessedTextureFlags |= 2;

		if ((v9 & 4) == 0)
			gProcessedTextureFlags |= 4;
	}
}

// @Ok
// @Matching
void PCGfx_RenderInit(f32 a1, f32 a2, f32 a3)
{
	gRenderInitOne[2] = a3;
	gRenderInitOne[0] = a1;
	gRenderInitOne[1] = a2;
	gRenderInitTwo[0] = a2 - a1;
	gRenderInitTwo[1] = gRenderInitTwo[0] / 4096.0f;
}

// @Ok
void PCGfx_RenderModelPreview(
		CSuper* a1,
		char const* a2,
		i32 a3)
{
	char v3[128];

	M3dMaths_RotMatrixYXZ(&G_MIKE_CAMERA[0].Angles, &G_MIKE_CAMERA[0].Transform);
	TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);
	PCGfx_BeginScene(1u, -1);

	M3d_RenderSetup(&G_MIKE_CAMERA[0], &G_VIEWPORT, (u32*)&G_PDOUBLE_BUFFER[1].Draw.tpage);
	M3d_Render(a1);
	M3d_RenderCleanup();
	Mess_SetSort(4095);
	PShell_SmallFont();
	Mess_SetRGB(0xFFu, 0xFFu, 0xFFu, 0);
	Mess_SetRGBBottom(0xFFu, 255, 255);
	Mess_SetShadowRGB(0xFFu);
	Mess_SetTextJustify(1);

	sprintf(v3, "PSX: %s", a2);
	Mess_DrawText(20, 20, v3, 0, 0x1000u);
	sprintf(v3, "IDX: %i", a3);
	Mess_DrawText(220, 20, v3, 0, 0x1000u);
	sprintf(v3, "CAM: %i %i %i", G_MIKE_CAMERA[0].Position.vx, G_MIKE_CAMERA[0].Position.vy, G_MIKE_CAMERA[0].Position.vz);
	Mess_DrawText(20, 45, v3, 0, 0x1000u);
	sprintf(v3, "ITM: %i %i %i", a1->mPos.vx >> 12, a1->mPos.vy >> 12, a1->mPos.vz >> 12);
	Mess_DrawText(220, 45, v3, 0, 0x1000u);

	if (G_SCENE_RELATED != 0)
	{
		DXPOLY_EndScene(1);
		PCGfx_ProcessTexture(0, -1, DCGfx_BlendingMode_0);
		gEndSceneRelated = -1;
		G_SCENE_RELATED = 0;
		gEndSceneRelatedTwo = 0;
	}
}

#ifndef SPIDEY_STANDALONE
EXPORT f32 gPcGfxBrightnessPower[8] =
{
	0.80000001f,
	0.85000002f,
	0.89999998f,
	0.94999999f,
	1.0f,
	1.05f,
	1.1f,
	1.15f
};
#else
extern f32 gPcGfxBrightnessPower[8];
#endif

// @Ok
// @Test
 void PCGfx_SetBrightness(i32 a1)
{
	f32 v8 = 1.0f / gPcGfxBrightnessPower[a1];
	for (i32 i = 0; i < 256; i++)
	{

		f32 v9 = i;
		f32 v6 = v9 / 255.0f;
		f32 v7 = pow(v6, v8);

		i32 v3 = (v7 * 255.0f + 0.5f);
		if (v3 >= 128)
		{
			v3 = 255;
		}
		else
		{
			v3 *= 2;
		}

		gPcGfxBrightnessValues[i] = v3;
	}
}

// @Ok
// @Matching
void PCGfx_SetFogParams(
		f32 a1,
		f32 a2,
		u32 a3)
{
	u32 three = a3;
	if ((a3 & 0xFF000000) == 0x80000000)
	{
		three = 0xFFFFFF & a3;
		a1 = a2 * 0.88999999f;
	}

	gFlFoggingParamOne = a1;
	gFlFoggingParamTwo = a2;
	gU32FoggingParamThree = three;
	G_BFOGGING_RELATED = 1;
}

u32 gDepthCompareValues[DCGfx_RenderSetting_7 + 1] =
{
	0, 2, 3, 4, 5, 6, 7, 8,
};

u32 gFilterModeValues[DCGfx_RenderSetting_d - DCGfx_RenderSetting_a + 1] =
{
	0, 1 ,1 ,1
};

// @Ok
// @Matching
void PCGfx_SetRenderParameter(
		DCGfx_RenderParameter a1,
		DCGfx_RenderSetting a2)
{
	if ( !gLowGraphics )
	{
		switch ( a1 )
		{
			case DCGfx_RenderParameter_0:
				print_if_false(a2 >= DCGfx_RenderSetting_0, "Invalid render setting.");
				print_if_false(a2 <= DCGfx_RenderSetting_7, "Invalid render setting.");
				DXPOLY_SetDepthCompare(gDepthCompareValues[a2]);
				break;
			case DCGfx_RenderParameter_1:
				print_if_false(a2 >= DCGfx_RenderSetting_8, "Invalid render setting.");
				print_if_false(a2 <= DCGfx_RenderSetting_9, "Invalid render setting.");
				DXPOLY_SetDepthWriting(a2 == DCGfx_RenderSetting_8);
				break;
			case DCGfx_RenderParameter_2:
				break;
			case DCGfx_RenderParameter_3:
				print_if_false(a2 >= DCGfx_RenderSetting_a, "Invalid render setting.");
				print_if_false(a2 <= DCGfx_RenderSetting_d, "Invalid render setting.");
				DXPOLY_SetFilterMode(gFilterModeValues[a2-DCGfx_RenderSetting_a]);
				break;
			case DCGfx_RenderParameter_4:
				gIsRenderSettingE = a2 == DCGfx_RenderSetting_e;
				if (!gIsRenderSettingE)
					gNonRendderSettingE = gIsRenderSettingE;
				break;
			default:
				print_if_false(0, "Invalid render parameter.");
				break;
		}
	}
}

// @Ok
 void PCGfx_SetSkyColor(u32 a1)
{
	G_PCGFX_SKY_COLOR = a1;
	DXPOLY_SetBackgroundColor(a1 | 0xFF000000);
}

// @Ok
// @Matching
 void PCGfx_UseTexture(i32 a1, DCGfx_BlendingMode a2)
{
	i32 v2 = a1;
	if ( a1 <= 2 )
	{
		v2 = -1;
		gNonRendderSettingE = gIsRenderSettingE;
	}
	if ( G_USE_TEXTURE_RELATED != v2 || gTextureBlendingMode != a2 )
	{
		G_USE_TEXTURE_RELATED = v2;
		gTextureBlendingMode = a2;
		if (!G_SCENE_RELATED)
		{
			PCGfx_BeginScene(3u, -1);
		}
		PCGfx_ProcessTexture(0, v2, a2);
	}
}

// @Ok
// Naming work done this session (nearest neighbor check against
// idb_globals.txt, see pcgfx.attempts.md): every global this function
// touches turned out to already be a named repo global at a different
// address than we thought: 0xAC08E0-style constants were not involved here,
// instead 0x568158/0x628614=gGameResolutionY/Yres, 0x568154/0x61B5FC=
// gGameResolutionX/Xres (m3dinit.h), 0xAC08DC=gTextureBlendingMode,
// 0xAC08C4=gSceneRelated, 0x56815C=gIsRenderSettingE, 0xADB3A8/0xADB3AC=
// gMaxTextureWidth/gTextureHeight all matched 1:1. Call targets identified
// the same way: 0x50F0E0=PCTex_GetTextureSize (real call, not inlined,
// matches its 3 arg shape), 0x510170/0x510190=PCTex_GetTextureSplitCount/
// PCTex_GetTextureSplitID (same pair PCGfx_DrawTexture2D already uses),
// 0x52A227/0x52A3C0=my_malloc/my_free (main.cpp already PATCH_PUSH_RETs
// these two exact addresses). The malloc size is splitCount * 44, and 44 is
// exactly sizeof(Texture), so the split path allocates a Texture[splitCount]
// and recurses into itself once per piece, the same shape as
// PCGfx_DrawTexture2D's split loop (same assert string "Split texture drawn
// with x != 0." at 0x568348, confirmed from the binary). The kind <= 2 /
// gUseTextureRelated / gTextureBlendingMode block at the top of the single
// texture path is PCGfx_UseTexture(kind, DCGfx_BlendingMode_0) inlined
// (matches PCGfx_UseTexture's body instruction for instruction); called
// here instead of reproducing the inline, since PCGfx_UseTexture is already
// @Ok.
// Fixed this session (was wrong): decompiled 0x509d20 directly to resolve
// the a3/a4/a5/a6 mapping the previous attempt could not pin down.
// PCGfx_DrawQuad2D(a1..a8,color,a10) takes (x,y,width,height,u0,v0,uScale,
// vScale,color,zOffset). In the single texture branch: x=a3*scaleX,
// y=a4*scaleY, width=a5*scaleX, height=a6*scaleY, u0=v0=0, uScale=vScale=1,
// and a10 (zOffset) is this function's own "scale" parameter passed through
// unchanged, NOT used as a width/height multiplier (the earlier version
// wrongly did `w = a5*scale` and also swapped the x/y/width/height argument
// order in the call).
// In the split branch, each piece's target width/height is
// subWidth/width*a5 and subHeight/height*a6 (using the caller's own a5/a6,
// not the game/design resolution ratio), and xAccum/yAccum advance by those
// same computed values, not by the raw subWidth/subHeight pixel sizes
// (confirmed from the disasm: v10 += the just computed scaled width, not
// subWidth).
void PCPanel_DrawTexturedPoly(f32 scale, Texture const *tex, i32 a3, i32 a4, i32 a5, i32 a6, u8 tint)
{
	print_if_false(tex != 0, "no texture for draw texture poly.");

	u16 kind = tex->clut;
	i32 width, height;
	PCTex_GetTextureSize(kind, &width, &height);

	if (width <= gMaxTextureWidth && height <= gTextureHeight)
	{
		PCGfx_UseTexture(kind, DCGfx_BlendingMode_0);

		f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;
		f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;

		u32 t = tint;
		u32 color = 0xFF000000u | (t << 16) | (t << 8) | t;

		f32 x = (f32)a3 * scaleX;
		f32 y = (f32)a4 * scaleY;
		f32 w = (f32)a5 * scaleX;
		f32 h = (f32)a6 * scaleY;

		PCGfx_DrawQuad2D(x, y, w, h, 0.0f, 0.0f, 1.0f, 1.0f, color, scale, 0);
	}
	else
	{
		i32 splitCount = PCTex_GetTextureSplitCount(kind);
		Texture *pieces = (Texture *)my_malloc(splitCount * sizeof(Texture));

		print_if_false(a3 == 0, "Split texture drawn with x != 0.");

		f32 fa5 = (f32)a5;
		f32 fa6 = (f32)a6;

		i32 xAccum = 0;
		i32 yAccum = a4;

		for (i32 i = 0; i < splitCount; i++)
		{
			i32 splitId = PCTex_GetTextureSplitID(kind, i);
			pieces[i].clut = (u16)splitId;

			i32 subWidth, subHeight;
			PCTex_GetTextureSize(splitId, &subWidth, &subHeight);

			i32 subScaleW = (i32)((f32)subWidth / (f32)width * fa5);
			i32 subScaleH = (i32)((f32)subHeight / (f32)height * fa6);

			PCPanel_DrawTexturedPoly(scale, &pieces[i], xAccum, yAccum, subScaleW, subScaleH, tint);

			xAccum += subScaleW;
			if (xAccum >= width)
			{
				yAccum += subScaleH;
				xAccum = 0;
			}
		}

		my_free(pieces);
	}
}

// @Ok
void ZCLIP_VERT(_DXVERT *out, _DXVERT *b, _DXVERT *c, f32 t)
{
	print_if_false(c->depth != b->depth, "Zero denominator computing scale!");
	print_if_false(t != 0.0f, "Zero denominator computing clip inverse!");

	f32 frac = (t - b->depth) / (c->depth - b->depth);
	out->depth = t;

	f32 invT = 1.0f / t;

	f32 bx = b->x * b->depth;
	f32 by = b->y * b->depth;
	f32 cx = c->x * c->depth;
	f32 cy = c->y * c->depth;

	out->reciprocalW = gRenderInitOne[2] * invT;
	out->x = ((cx - bx) * frac + bx) * invT;
	out->y = ((cy - by) * frac + by) * invT;

	out->u = (c->u - b->u) * frac + b->u;
	out->v = (c->v - b->v) * frac + b->v;

	u32 colorB = b->colorArgb;
	u32 colorC = c->colorArgb;

	i32 chB_A = (colorB >> 24) & 0xFF;
	i32 chC_A = (colorC >> 24);
	i32 newA = (i32)((f32)(chC_A - chB_A) * frac) + chB_A;

	i32 chB_R = (colorB >> 16) & 0xFF;
	i32 chC_R = (colorC >> 16) & 0xFF;
	i32 newR = (i32)((f32)(chC_R - chB_R) * frac) + chB_R;

	i32 chB_G = (colorB >> 8) & 0xFF;
	i32 chC_G = (colorC >> 8) & 0xFF;
	i32 newG = (i32)((f32)(chC_G - chB_G) * frac) + chB_G;

	i32 chB_B = colorB & 0xFF;
	i32 chC_B = colorC & 0xFF;
	i32 newB = (i32)((f32)(chC_B - chB_B) * frac) + chB_B;

	out->colorArgb = ((newA & 0xFF) << 24) | ((newR & 0xFF) << 16) | ((newG & 0xFF) << 8) | (newB & 0xFF);
}

// @Ok
// @AlmostMatching: CVector and CSVector assingment is different, goddamn I don't understand
CSuper* createSuperItem(CItem *pItem)
{
	CSuper *pSuper = new CSuper();

	pSuper->mFlags |= pItem->mFlags;
	pSuper->mInquiry = pItem->mInquiry;

	pSuper->mPos = pItem->mPos;

	pSuper->mAngles = pItem->mAngles;

	pSuper->mModel = pItem->mModel;

	pSuper->mDummyFrame = pItem->mDummyFrame;
	pSuper->mTintIndex = pItem->mTintIndex;
	pSuper->mDummyAnim = pItem->mDummyAnim;

	pSuper->mRegion = pItem->mRegion;
	pSuper->mNextItem = pItem->mNextItem;
	pSuper->mRGB = pItem->mRGB;

	pSuper->mScale = pItem->mScale;

	pSuper->mTRN = pItem->mTRN;
	pSuper->mPreviousItem = pItem->mPreviousItem;
	pSuper->mType = pItem->mType;
	pSuper->mpLight = pItem->mpLight;

	pSuper->mFrame = 0;
	pSuper->mAnim = 0;

	return pSuper;
}

// @Ok
// Confirmed against IDA decompile of 0x5071b0. Fixed a bug this session:
// the low graphics branch set field_C to a hardcoded 4 instead of count;
// the original writes count in both branches (the low graphics/hardware
// split only changes how field_4/mBlendMode/field_A are sourced, not
// field_C). gLowGraphics true/false otherwise take the same shape (copy
// vertex fields, per vertex color brighten step, fog clamp loop).
void submitPoly(_DXVERT **verts, i32 count)
{
	i32 idx = gEndSceneRelatedTwo;
	if (idx >= 15360)
	{
		gEndSceneRelatedTwo = idx + 1;
		return;
	}

	DXPOLY *p = &gDxPolys[idx];
	gEndSceneRelatedTwo = idx + 1;
	i32 blendMode = gPcGfxBlendModeRelated;
	f32 fogMax = 0.0f;

	if (gLowGraphics)
	{
		p->textureHandle = (LPDIRECTDRAWSURFACE7)(i32)G_USE_TEXTURE_RELATED;
		*(i32*)&p->blendMode = gPcGfxDrawRelated;
		if (G_USE_TEXTURE_RELATED < 0)
			*(i32*)&p->blendMode = gPcGfxDrawRelated & 0xFFFFFFFB;

		p->vertexCount = count;

		if (count > 0)
		{
			for (i32 i = 0; i < count; i++)
			{
				SDXPolyField *dst = &p->vertices[i];
				memcpy(dst, verts[i], sizeof(SDXPolyField));

				if (!(p->blendMode & 4))
					dst->reciprocalW = 1.0f; // rhw, the original stores to +0xC (0x5071B0: *((DWORD*)v6 + 1) with v6 = dst + 8)

				i32 v23;
				if (gProcessTextureRelated)
					v23 = 128;
				else
					v23 = (dst->colorArgb >> 24) & 0xFF;

				dst->colorArgb =
					gPcGfxBrightnessValues[dst->colorArgb & 0xFF] |
					gPcGfxBrightnessValues[(dst->colorArgb >> 8) & 0xFF] << 8 |
					gPcGfxBrightnessValues[(dst->colorArgb >> 16) & 0xFF] << 16 |
					v23 << 24;

				if (dst->depth < 0.0f)
				{
					dst->depth = 0.0f;
				}
				else if (dst->depth > 0.99989998f)
				{
					dst->depth = 0.99989998f;
					fogMax = dst->depth;
				}
				else if (fogMax < dst->depth)
				{
					fogMax = dst->depth;
				}
			}
		}
	}
	else
	{
		LPDIRECTDRAWSURFACE7 Direct3DTexture;
		if (G_USE_TEXTURE_RELATED < 0)
			Direct3DTexture = 0;
		else
			Direct3DTexture = PCTex_GetDirect3DTexture(G_USE_TEXTURE_RELATED);
		p->textureHandle = Direct3DTexture;
		p->blendMode = gChosenBlendingMode;
		p->flags = gProcessedTextureFlags;
		p->vertexCount = count;

		if (count > 0)
		{
			for (i32 i = 0; i < count; i++)
			{
				SDXPolyField *dst = &p->vertices[i];
				memcpy(dst, verts[i], sizeof(SDXPolyField));

				i32 v23;
				if (gProcessTextureRelated)
					v23 = 128;
				else
					v23 = (dst->colorArgb >> 24) & 0xFF;

				dst->colorArgb =
					gPcGfxBrightnessValues[dst->colorArgb & 0xFF] |
					gPcGfxBrightnessValues[(dst->colorArgb >> 8) & 0xFF] << 8 |
					gPcGfxBrightnessValues[(dst->colorArgb >> 16) & 0xFF] << 16 |
					v23 << 24;

				if (dst->depth < 0.0f)
				{
					dst->depth = 0.0f;
				}
				else if (dst->depth > 0.99989998f)
				{
					dst->depth = 0.99989998f;
					fogMax = dst->depth;
				}
				else if (fogMax < dst->depth)
				{
					fogMax = dst->depth;
				}
			}
		}

		if (gChosenBlendingMode)
		{
			blendMode = 0;
		}
	}

	DXPOLY_DrawPoly(p, gPcGfxSlotNumber, blendMode, fogMax);
	gPcGfxSlotNumber = -1;
}

// @Ok
i32 STDCALL kmSetPALEXTCallback(void*, i32)
{
	return 120;
}

// @Ok
i32 STDCALL kmSetDisplayMode(i32, i32, i32, i32)
{
	return 120;
}

// @Ok
i32 STDCALL kmInitDevice(i32)
{
	return 0;
}

// @Ok
i32 STDCALL kmSetWaitVsyncCount(i32)
{
	return 120;
}

// @Ok
i32 STDCALL kmUnloadDevice(void)
{
	return 120;
}
