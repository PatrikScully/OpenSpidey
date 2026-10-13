#pragma once

#ifndef PCSHELL_H
#define PCSHELL_H

#include "compatibility/export.h"

struct SActionMap
{
	i32 field_0;
	char field_4[1];

	PADDING(0x14-0x4-1);

	u32 field_14;
	u32 field_18;
};

EXPORT u8 PCSHELL_CheckTriggers(u32,i32,i32);
EXPORT void PCSHELL_CoordsDCtoPC(i32 *,i32 *);
EXPORT void PCSHELL_CoordsPCtoDC(i32 *,i32 *);
EXPORT void PCSHELL_DoControllerConfig(bool);
EXPORT void PCSHELL_DoDisplayOptions(void);
EXPORT void PCSHELL_DrawMouseCursor(void);
EXPORT void PCSHELL_Initialize(void);
EXPORT u8 PCSHELL_IsMouseOver(i32,i32,i32,i32);
EXPORT u8 PCSHELL_IsMouseOverText(char const *,i32,i32,i32);
EXPORT i32 PCSHELL_MouseMoved(void);
EXPORT void PCSHELL_Relax(void);
EXPORT void PCSHELL_Shutdown(void);
EXPORT u8 PCSHELL_UpdateMouse(void);
EXPORT void displayControllerScreen(void);
// unnamed helper, address 0x430680, original bytes disassemble to an empty
// function (no args used). Declared here so other TUs (e.g. screen.cpp) can
// call it without duplicating the forward declaration.
EXPORT void gsub_430680(void);
// unnamed helper, address 0x430880 (named "nullsub_3" in the IDA export). Declared here
// (was PCShell.cpp-local) so shell.cpp's CheckForPadUnplugged can call it too, same reason
// as gsub_430680 above.
EXPORT void gsub_430880(void);

// display options chosen in the shell, applied by SpideyMain (main.cpp) on the
// way into a level. Defined in PCShell.cpp. Tentative names, see the comment
// there for the address evidence.
EXPORT extern u32 gPendingResolutionX;
EXPORT extern u32 gPendingResolutionY;
EXPORT extern u32 gPendingColorDepth;
EXPORT void initActionMaps(void);
EXPORT u8 processControllerScreen(void);
EXPORT void resetActionMaps(bool);

void validate_SActionMap(void);

#endif
