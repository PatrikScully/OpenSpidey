#pragma once

#ifndef PCGFX_H
#define PCGFX_H

#include "engine/world/ob.h"
#include "engine/rendering/texture.h"
#include "compatibility/export.h"
#include "engine/rendering/polygon_pipeline.h"

enum DCGfx_RenderParameter
{
	DCGfx_RenderParameter_0  = 0,
	DCGfx_RenderParameter_1  = 1,
	DCGfx_RenderParameter_2  = 2,
	DCGfx_RenderParameter_3  = 3,
	DCGfx_RenderParameter_4  = 4,
};

enum DCGfx_RenderSetting
{
	DCGfx_RenderSetting_0  = 0x0,
	DCGfx_RenderSetting_1  = 0x1,
	DCGfx_RenderSetting_2  = 0x2,
	DCGfx_RenderSetting_3  = 0x3,
	DCGfx_RenderSetting_4  = 0x4,
	DCGfx_RenderSetting_5  = 0x5,
	DCGfx_RenderSetting_6  = 0x6,
	DCGfx_RenderSetting_7  = 0x7,
	DCGfx_RenderSetting_8  = 0x8,
	DCGfx_RenderSetting_9  = 0x9,
	DCGfx_RenderSetting_a  = 0xA,
	DCGfx_RenderSetting_b  = 0xB,
	DCGfx_RenderSetting_c  = 0xC,
	DCGfx_RenderSetting_d  = 0xD,
	DCGfx_RenderSetting_e  = 0xE,
	DCGfx_RenderSetting_MAX  = DCGfx_RenderSetting_e,
};

enum DCGfx_BlendingMode
{
	DCGfx_BlendingMode_0  = 0,
	DCGfx_BlendingMode_1  = 1,
	DCGfx_BlendingMode_2  = 2,
	DCGfx_BlendingMode_3  = 3,
	DCGfx_BlendingMode_4  = 4,
	DCGfx_BlendingMode_MAX  = DCGfx_BlendingMode_4,
};

// Source alias for the original transformed vertex name. The meaningful
// screen-space fields and 28-byte layout live in screen_vertex.h.
typedef spidey::rendering::ScreenVertex _DXVERT;

// tagKMVERTEX3: layout guessed from PCGfx_ClipSendIndexedVertList's (0x506980)
// positional field reads only, not cross checked against any struct dump.
// Stride is 0x20 bytes (index is shifted left by 5 to index the array).
// field_0 is never read by that function so its meaning is unknown.
struct tagKMVERTEX3
{
	i32 field_0;
	f32 field_4;
	f32 field_8;
	f32 field_C;
	f32 field_10;
	f32 field_14;
	u32 field_18;
	// 32 byte records: PCGfx_ClipSendIndexedVertList (0x506980) indexes the
	// array with 32 * index and DCModel_RenderModel (0x476D00) writes 8
	// floats per vertex. The struct used to stop at 0x1C (28 bytes), so
	// every indexed read of the vertex pool landed between records and the
	// standalone build clipped away all level geometry (2026-09-03).
	i32 field_1C;
};

#define _tagKMSTRIPHEAD i32

EXPORT void PCGfx_BeginScene(u32,i32);
#ifdef _WIN32
EXPORT void PCGfx_CopyGameResolution(void);
#endif
EXPORT void PCGfx_ClipSendIndexedVertList(tagKMVERTEX3 const *,i32,u16 const *,i32);
EXPORT void PCGfx_ClipTriToNearPlane(_DXVERT **,_DXVERT *const *);
EXPORT void PCGfx_DoModelPreview(void);
EXPORT void PCGfx_DrawLine(f32,f32,f32,u32,f32,f32,f32,u32,f32);
EXPORT void PCGfx_DrawQPoly2D(f32,f32,f32,f32,u32,f32,f32,f32,f32,u32,f32,f32,f32,f32,u32,f32
,f32,f32,f32,u32,f32);
EXPORT void PCGfx_DrawQPoly3D(f32,f32,f32,f32,f32,u32,f32,f32,f32,f32,f32,u32,f32,f32,f32,f32,f32,u32,f32,f32,f32,f32,f32,u32);
EXPORT void PCGfx_DrawQuad2D(f32,f32,f32,f32,f32,f32,f32,f32,u32,f32,bool);
EXPORT void PCGfx_DrawTPoly2D(f32,f32,f32,f32,u32,f32,f32,f32,f32,u32,f32,f32,f32,f32,u32,f32);
EXPORT void PCGfx_DrawTPoly3D(f32,f32,f32,f32,f32,u32,f32,f32,f32,f32,f32,u32,f32,f32,f32,f32,f32,u32);
EXPORT void PCGfx_DrawTexture2D(i32,i32,i32,f32,u32,u32,f32);
EXPORT void PCGfx_EndScene(i32);
EXPORT void PCGfx_Exit(void);
EXPORT f32 PCGfx_GetZLayerFurthest(void);
EXPORT f32 PCGfx_GetZLayerNearest(void);
EXPORT void PCGfx_IncZLayerFurthest(void);
EXPORT void PCGfx_IncZLayerNearest(void);
EXPORT void PCGfx_InitAtStart(void);
EXPORT u8 PCGfx_IsInScene(void);
EXPORT void PCGfx_ProcessTexture(_tagKMSTRIPHEAD *,i32,DCGfx_BlendingMode);
EXPORT void PCGfx_RenderInit(f32,f32,f32);
EXPORT void PCGfx_RenderModelPreview(CSuper *,char const *,i32);
EXPORT void PCGfx_SetBrightness(i32);
EXPORT void PCGfx_SetFogParams(f32,f32,u32);
EXPORT void PCGfx_SetRenderParameter(DCGfx_RenderParameter,DCGfx_RenderSetting);
EXPORT void PCGfx_SetSkyColor(u32);
EXPORT void PCGfx_UseTexture(i32,DCGfx_BlendingMode);
EXPORT void PCPanel_DrawTexturedPoly(f32,Texture const *,i32,i32,i32,i32,u8);
EXPORT void ZCLIP_VERT(_DXVERT *,_DXVERT *,_DXVERT *,f32);
EXPORT CSuper* createSuperItem(CItem *);
EXPORT void submitPoly(_DXVERT **,i32);
EXPORT u32 gsub_506D70(u32,f32);
EXPORT void gsub_509400(tagKMVERTEX3 const *,_DXVERT *);

EXPORT i32 amHeapFree(i32);
EXPORT i32 acDspSetMixerChannel(i32, i32, i32, i32);

EXPORT i32 STDCALL kmSetPALEXTCallback(void*, i32);
EXPORT i32 STDCALL kmSetDisplayMode(i32, i32, i32, i32);
EXPORT i32 STDCALL kmInitDevice(i32);
EXPORT i32 STDCALL kmSetWaitVsyncCount(i32);
EXPORT i32 STDCALL kmUnloadDevice(void);

EXPORT i32 amHeapAlloc(u32**, i32, i32, i32, i32);
EXPORT i32 acG2Write(void*, void*, i32);

// The four PCGfx globals that non DirectX code reads. PCGfx.cpp itself is not
// hooked and is due to be rewritten, but the exe keeps writing these every
// frame, so game side readers have to see the exe's copies. The rest of
// PCGfx.cpp's state is only touched inside PCGfx.cpp and stays repo local.

EXPORT extern u8 gSceneRelated;
// PCGfx_IsInScene (0x509570) is two instructions: "mov al,[0AC08C4h]; ret".
//#define G_SCENE_RELATED (gSceneRelated)
#define G_SCENE_RELATED (*reinterpret_cast<u8*>(0x00AC08C4))

EXPORT extern u32 gPcGfxSkyColor;
// Db_UpdateSky (0x430336) compares against [0AC08C8h] and stores to it.
//#define G_PCGFX_SKY_COLOR (gPcGfxSkyColor)
#define G_PCGFX_SKY_COLOR (*reinterpret_cast<u32*>(0x00AC08C8))

EXPORT extern u8 gBFoggingRelated;
// Db_UpdateSky (0x430348) writes "mov byte ptr [0AC08C5h],1" right after the
// sky colour store.
//#define G_BFOGGING_RELATED (gBFoggingRelated)
#define G_BFOGGING_RELATED (*reinterpret_cast<u8*>(0x00AC08C5))

EXPORT extern i32 gUseTextureRelated;
// PCGfx_UseTexture (0x506458) reads [568170h] and 0x506472 writes it back.
// The exe has -1 there in the raw .data, same as our initialiser.
//#define G_USE_TEXTURE_RELATED (gUseTextureRelated)
#define G_USE_TEXTURE_RELATED (*reinterpret_cast<i32*>(0x00568170))

#endif
