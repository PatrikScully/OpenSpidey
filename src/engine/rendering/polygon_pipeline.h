#pragma once

#include "compatibility/export.h"
#include "compatibility/non_win32.h"
#include "compatibility/my_dx.h"

#include "engine/rendering/screen_vertex.h"

// Source compatibility for callers using the recovered record type name.
typedef spidey::rendering::ScreenVertex SDXPolyField;

namespace spidey {
namespace rendering {

// Original queue packet layout. Named fields make submission and traversal
// readable while retaining the original 0xF0 stride and reserved tail.
struct PolygonPacket {
    PolygonPacket* next;
    LPDIRECTDRAWSURFACE7 textureHandle;
    u16 blendMode;
    u16 flags;
    DWORD vertexCount;
    ScreenVertex vertices[4];
    u8 reserved[0x6D];
};

enum PolygonFlag {
    WrapTextureU = 0x2,
    WrapTextureV = 0x4,
    TextureAlpha = 0x8,
    PointSampling = 0x10,
    HudSubmission = 0x8000
};

} // namespace rendering
} // namespace spidey

typedef spidey::rendering::PolygonPacket DXPOLY;

EXPORT void DXPOLY_BeginScene(void);
EXPORT void DXPOLY_DrawPoly(DXPOLY*,i32,i32,f32);
EXPORT void DXPOLY_EnableTexAlpha(bool);
EXPORT void DXPOLY_EndScene(bool);
EXPORT void DXPOLY_Flip(void);
EXPORT void DXPOLY_Init(u32);
EXPORT void DXPOLY_SaveScreen(void);
EXPORT void DXPOLY_SaveSurfaceAsBMP(char *,void *,i32,i32,i32,_DDPIXELFORMAT *,bool);
EXPORT void DXPOLY_SetBackgroundColor(u32);
EXPORT void DXPOLY_SetBlendMode(u32);
EXPORT void DXPOLY_SetDepthCompare(u32);
EXPORT void DXPOLY_SetDepthWriting(bool);
EXPORT void DXPOLY_SetFilterMode(u32);
EXPORT void DXPOLY_SetHUDOffset(i32);
EXPORT void DXPOLY_SetOutlineColor(u32);
EXPORT void DXPOLY_SetTexture(LPDIRECTDRAWSURFACE7);
EXPORT void initialSettings(void);
EXPORT void renderScene(void);
EXPORT void gsub_514ED0(void);
EXPORT void DXPOLY_SetAddressUAndV(DWORD, DWORD);


void validate_DXPOLY();
void validate_SDXPolyField();
