#pragma once

#ifndef SPIDEYDX_H
#define SPIDEYDX_H

#include "compatibility/export.h"
#include "compatibility/non_win32.h"

EXPORT extern i32 gRenderTest;

// The game's logical resolution, written by DXINIT_SetDisplayOptions (not hooked)
// and read from 17 files. Same problem as Xres/Yres in m3dinit.h.
// Addresses from Flash_Display's disassembly (0x0043DAB1 reads X, 0x0043DA37
// reads Y).
EXPORT extern i32 gGameResolutionX;
//#define G_GAME_RESOLUTION_X (gGameResolutionX)
#define G_GAME_RESOLUTION_X (*reinterpret_cast<i32*>(0x00568154))
EXPORT extern i32 gGameResolutionY;
//#define G_GAME_RESOLUTION_Y (gGameResolutionY)
#define G_GAME_RESOLUTION_Y (*reinterpret_cast<i32*>(0x00568158))

EXPORT extern u32 gDxResolutionX;
EXPORT extern u32 gDxResolutionY;

EXPORT extern u8 gMMXSupport;
EXPORT extern u8 g3DAccelator;

EXPORT void BuildTwiddleTable(void);
EXPORT u32 CalcUntwiddledPos(u32,u32,u32,u32);
EXPORT void ComputeMaskShift(u32,u32,u32 &,u32 &);
EXPORT void SPIDEYDX_DisplayDeviceSettings(char *);
EXPORT void SPIDEYDX_LoadSettings(void);
EXPORT void SPIDEYDX_SaveSettings(void);
EXPORT void SPIDEYDX_Shutdown(void);
EXPORT void DXERR_printf(const char*, ...);

EXPORT LRESULT CALLBACK SpideyWndProc(HWND, UINT, WPARAM, LPARAM);
EXPORT i32 WinYield(void);

EXPORT void debugSettings(void);
EXPORT i32 mipmapOffset(u32,u32,f32);
EXPORT void parseCommandLine(char *);

EXPORT u16* PVR_ConvertTwiddledToBMP(u32, u32, const u16*, u32);
EXPORT u16* PVR_ConvertVQToBMP(u32, u32, const u16*, u32);

EXPORT i32 WINAPI RealWinMain(HINSTANCE, HINSTANCE, LPSTR, i32);

EXPORT extern HWND gHwnd;

EXPORT extern i32 gBrightnessRelated;

EXPORT extern u32 gSavedResolutionX;
EXPORT extern u32 gSavedResolutionY;
EXPORT extern u32 gSavedColorDepth;
EXPORT extern char gDisplayDeviceName[128];

EXPORT extern i32 gSavedSFXVolume;
EXPORT extern i32 gSavedMusicVolume;
EXPORT extern i32 gSavedXAVolume;
EXPORT extern bool gSavedSoundMode;
EXPORT void validate_TwiddleStuff(void);


#endif
