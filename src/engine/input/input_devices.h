#pragma once

#include "compatibility/export.h"
#include "compatibility/non_win32.h"
#include "compatibility/my_dx.h"

EXPORT u8 DXINPUT_GetControllerButtonState(u8);
EXPORT u8 DXINPUT_GetKeyState(u8);
EXPORT u8 DXINPUT_GetMouseButtonState(u8);
EXPORT i32 DXINPUT_GetNumControllerButtons(void);
EXPORT void DXINPUT_Initialize(LPDIRECTINPUT8,HWND);
EXPORT i32 DXINPUT_PollController(i32 *,i32 *,i32 *);
EXPORT i32 DXINPUT_PollKeyboard(void);
EXPORT i32 DXINPUT_PollMouse(i32 *,i32 *);
EXPORT void DXINPUT_Release(void);
EXPORT void DXINPUT_SetKeyState(u8,u8);
EXPORT void DXINPUT_SetMouseButtonState(u8,u8);
EXPORT i32 DXINPUT_SetupController(void);
EXPORT i32 DXINPUT_SetupForceFeedbackSineEffect(i32,f32);
EXPORT i32 DXINPUT_SetupKeyboard(i32,i32);
EXPORT i32 DXINPUT_SetupMouse(i32);
EXPORT i32 DXINPUT_StartForceFeedbackEffect(void);
EXPORT i32 DXINPUT_StopForceFeedbackEffect(void);
EXPORT void DXINPUT_GetKeyName(u8, char*);

EXPORT BOOL CALLBACK EnumControllersCallback(const DIDEVICEINSTANCEA *,void *);

EXPORT extern LPDIRECTINPUT8 g_pDI;
