#include "game/ui/PCShell.h"
#include "game/ui/dcshellutils.h"
#include "engine/input/PCInput.h"
#include "game/application/SpideyDX.h"
#include "game/ui/front.h"
#include "engine/input/input_devices.h"
#include "game/ui/pshell.h"
#include "engine/rendering/PCGfx.h"
#include "game/ui/shell.h"
#include "game/ui/mess.h"
#include "engine/input/ps2pad.h"
#include "engine/rendering/render_buffers.h"
#include "game/application/main.h"
#include "core/utils.h"
#include "engine/audio/ps2lowsfx.h"
#include "game/application/DXinit.h"
#include "engine/rendering/ps2funcs.h"
#include "game/ui/panel.h"

#include <cstring>

#include "compatibility/validate.h"

EXPORT Sprite2* gCursorSprite;

EXPORT i32 gShellMouseX;
EXPORT i32 gShellMouseY;

EXPORT i32 gShellMouseOffsetX;
EXPORT i32 gShellMouseOffsetY;

const i32 MOUSE_TRIGGER_COUNT = 18;
EXPORT u8 gMouseTriggerRelated[MOUSE_TRIGGER_COUNT];

const i32 ACTION_MAP_COUNT = 11;
#ifndef SPIDEY_STANDALONE
EXPORT SActionMap gActionMaps[ACTION_MAP_COUNT];
#else
extern SActionMap gActionMaps[ACTION_MAP_COUNT];
#endif
EXPORT char gKeyNames[ACTION_MAP_COUNT][32];

EXPORT CMenu* gControllerMenu;
EXPORT CMenu* gControllerMenuTwo;

EXPORT i32 gActionMapRelated;

char* STR_RESTORE_DEFAULTS = "restore default settings";
char* STR_KB_CONFIG = "keyboard configuration";
char* STR_JOY_CONFIG = "joystick configuration";

EXPORT i32 gShellTitleBarRelated;

// set while the controller screen waits for the player to press a new key or button
EXPORT i32 gShellWaitingForInput;

// the controller button being captured on the controller config screen;
// 0x4000 = none, -1 = done. 0xAC1224 in the original.
EXPORT i32 gShellControllerButton;

// the keyboard key being captured on the controller config screen;
// 0x4000 = none, -1 = done. 0xAC1248 in the original.
EXPORT i32 gShellKeyboardKey;

// @Ok
// @Matching
u8 PCSHELL_CheckTriggers(u32 mask, i32 a2, i32 a3)
{
	i32 result = 0;
	u32 button;

	if (mask & 0x1)
		result |= PCINPUT_IsKeyPressed(0xC8, a2);

	if (mask & 0x2)
		result |= PCINPUT_IsKeyPressed(0xD0, a2);

	if (mask & 0x4)
		result |= PCINPUT_IsKeyPressed(0xCB, a2);

	if (mask & 0x8)
		result |= PCINPUT_IsKeyPressed(0xCD, a2);

	if (mask & 0x10)
	{
		if (!gMouseTriggerRelated[4])
		{
			result |= PCINPUT_IsKeyPressed(0x1C, a2);
			if (a3)
				gMouseTriggerRelated[4] = 1;
		}
	}

	if (mask & 0x20)
	{
		if (!gMouseTriggerRelated[5])
		{
			result |= PCINPUT_IsKeyPressed(0x1, a2);
			if (a3)
				gMouseTriggerRelated[5] = 1;
		}
	}

	if (mask & 0x40)
		result |= PCINPUT_IsKeyPressed(0x1, a2);

	if (!(gRenderTest & 0x10))
	{
		if (mask & 0x100)
		{
			if (!gMouseTriggerRelated[7])
			{
				result |= PCINPUT_IsMouseButtonPressed(0, a2);
				if (a3)
					gMouseTriggerRelated[7] = 1;
			}
		}

		if (mask & 0x200)
		{
			if (!gMouseTriggerRelated[8])
			{
				result |= PCINPUT_IsMouseButtonPressed(1, a2);
				if (a3)
					gMouseTriggerRelated[8] = 1;
			}
		}
	}

	if (mask & 0x1000)
	{
		if (PCINPUT_GetControllerDirections() & 1)
		{
			result |= 1;
			if (a3)
				PCINPUT_FreezeControllerAxes();
		}
	}

	if (mask & 0x2000)
	{
		if (PCINPUT_GetControllerDirections() & 2)
		{
			result |= 1;
			if (a3)
				PCINPUT_FreezeControllerAxes();
		}
	}

	if (mask & 0x4000)
	{
		if (PCINPUT_GetControllerDirections() & 4)
		{
			result |= 1;
			if (a3)
				PCINPUT_FreezeControllerAxes();
		}
	}

	if (mask & 0x8000)
	{
		if (PCINPUT_GetControllerDirections() & 8)
		{
			result |= 1;
			if (a3)
				PCINPUT_FreezeControllerAxes();
		}
	}

	if (mask & 0x10000)
	{
		if (!gMouseTriggerRelated[13])
		{
			PCINPUT_GetControllerMappingForAction(0x10, &button);
			if (button == 0x4000)
				button = 0;
			result |= PCINPUT_IsControllerButtonPressed(button, a2);
			if (a3)
				gMouseTriggerRelated[13] = 1;
		}
	}

	if (mask & 0x20000)
	{
		if (!gMouseTriggerRelated[14])
		{
			PCINPUT_GetControllerMappingForAction(0x20, &button);
			if (button == 0x4000)
				button = 1;
			result |= PCINPUT_IsControllerButtonPressed(button, a2);
			if (a3)
				gMouseTriggerRelated[14] = 1;
		}
	}

	if (mask & 0x40000)
	{
		if (!gMouseTriggerRelated[15])
		{
			PCINPUT_GetControllerMappingForAction(0x1000, &button);
			if (button != 0x4000)
				result |= PCINPUT_IsControllerButtonPressed(button, a2);
			if (a3)
				gMouseTriggerRelated[15] = 1;
		}
	}

	if (mask & 0x100000)
	{
		result |= PCINPUT_IsKeyPressed(0xD, a2);
		result |= PCINPUT_IsKeyPressed(0x4E, a2);
	}

	if (mask & 0x200000)
	{
		result |= PCINPUT_IsKeyPressed(0xC, a2);
		result |= PCINPUT_IsKeyPressed(0x4A, a2);
	}

	if (mask & 0x1000000)
		result |= PCINPUT_IsKeyPressed(0x1C, a2);

	if (mask & 0x2000000)
		result |= PCINPUT_IsKeyPressed(0x1, a2);

	if (mask & 0x4000000)
		result |= PCINPUT_IsKeyPressed(0x39, a2);

	return result != 0;
}

// @Ok
 void PCSHELL_CoordsDCtoPC(i32* pX, i32* pY)
{
	*pX = (f64)*pX / 512.0 * (f64)gDxResolutionX;
	*pY = (f64)*pY / 240.0 * (f64)gDxResolutionY;
}

// @Ok
// @Matching
 void PCSHELL_CoordsPCtoDC(i32* pX, i32* pY)
{
	*pX = (f32)(*pX * 512) / (f32)gDxResolutionX;
	*pY = (f32)(*pY * 240) / (f32)gDxResolutionY;
}

// forward declaration, defined further down with displayControllerScreen
void shell_optimized_func(i32, i32, i32);

// unnamed helpers called from PCSHELL_DoControllerConfig; bodies defined further down
// so they are not visible for same-TU inlining at their call sites (matches the original,
// which has real "call" instructions at each site, not inlined bodies).
EXPORT void gsub_430680(void);
EXPORT void gsub_430880(void);

// @Ok
// @Matching
void PCSHELL_DoControllerConfig(bool isKeyboard)
{
	gControllerMenu = new CMenu(30, 60, 1u, 256, 256, 15);
	gControllerMenuTwo = new CMenu(332, 60, 1u, 256, 256, 15);

	gActionMapRelated = isKeyboard == 0;
	gShellWaitingForInput = 0;
	gShellTitleBarRelated = 0;

	initActionMaps();

	gControllerMenuTwo->scrollbar_zero = 1;
	gControllerMenu->scrollbar_zero = 1;

	gControllerMenu->Zoom(0);
	gControllerMenuTwo->Zoom(0);

	gControllerMenuTwo->mLine = isKeyboard != 0 ? 0 : 4;
	gControllerMenu->mLine = gControllerMenuTwo->mLine;

	u8 done;
	do
	{
		i32 startVblanks = G_VBLANKS;

		gsub_430880();

		Db_FlipClear();
		CalcPolyBufferEnd();

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1, -1);

		PShell_NormalFont();

		gControllerMenu->Display();
		gControllerMenuTwo->Display();

		shell_optimized_func(384, 222, 0);

		char* configName = !gActionMapRelated ? STR_KB_CONFIG : STR_JOY_CONFIG;

		Shell_DrawTitleBar(gShellTitleBarRelated, 25, configName, 1, 0, 150, -21, 29);
		Shell_DrawBackground();

		if (!(gRenderTest & 0x10) && gCursorSprite && PCINPUT_GetMouseStatus())
		{
			gCursorSprite->draw(gShellMouseX, gShellMouseY, 0, 0);
		}

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		done = processControllerScreen();

		gsub_430680();
		WinYield();
		Sleep(10);

		Pause(startVblanks - G_VBLANKS + 2);
	} while (!done);

	gsub_430680();
	Pad_ClearTriggers(G_SCONTROL);

	delete gControllerMenu;
	delete gControllerMenuTwo;
	gControllerMenu = 0;
	gControllerMenuTwo = 0;

	SPIDEYDX_SaveSettings();
}

// PCSHELL_DoControllerConfig's original calls this as a real out-of-line function, keep the MSVC inliner away
#ifdef _MSC_VER
#pragma auto_inline(off)
#endif
// unnamed helper called once per controller config screen frame, address 0x430680,
// names.json calls it "optimized_unused_garbage". Original bytes are three
// "if (!gPrintStubbed) call stubbed-print(str)" blocks, same idiom as
// gsub_46CB90's other callers (panel.cpp, shell.cpp Shell_ShowRecord): our
// export.h stubbed_printf is static and gets inlined away, so we call
// gsub_46CB90 (panel.cpp), the real out-of-line implementation at 0x46CB90,
// instead. String addresses are unverified (no access to the original data
// section).
// @Ok
// @Matching
EXPORT void gsub_430680(void)
{
	if (!gPrintStubbed)
		gsub_46CB90((void*)0x549668);

	if (!gPrintStubbed)
		gsub_46CB90((void*)0x549650);

	if (!gPrintStubbed)
		gsub_46CB90((void*)0x549638);
}

// unnamed helper called once at the top of every controller config screen frame, address 0x430880 (named "nullsub_3" in the IDA export)
// original bytes are a single ret, no body.
// @Ok
// @Matching
EXPORT void gsub_430880(void)
{
}

#ifdef _MSC_VER
#pragma auto_inline(on)
#endif

// menu line text, addresses 0x54BBCC / 0x54BBD4 / 0x54BBD8. content unverified
// (no access to the original data section), text is a guess based on what the
// three lines do (resolution, colour depth, brightness).
static char* STR_DISPLAY_RESOLUTION = "resolution";
static char* STR_DISPLAY_COLOR_DEPTH = "colour depth";
static char* STR_DISPLAY_BRIGHTNESS = "brightness";

// title bar text, address 0x54B7CC. content unverified, guess.
static char* STR_DISPLAY_OPTIONS_TITLE = "display options";

// sprintf format strings, addresses 0x568868 (resolution line, 2 ints) and
// 0x568864 (colour depth and brightness lines, reused for both, 1 int each).
// content unverified, guesses based on the values passed to sprintf.
static char* FMT_DISPLAY_RESOLUTION = "%dx%d";
static char* FMT_DISPLAY_NUMBER = "%d";

// pending colour depth chosen in the display options menu, applied on Confirm.
// address 0x2E098E4, tentative name. Nearest idb_globals.txt neighbours are
// 0x2E09810 gDisplayDeviceIndex, 0x2E09814 gMMXSupport and 0x2E098E8
// gMissingCD; this address does not fall inside any of them.
// not static any more: SpideyMain (main.cpp) applies these three when the
// shell returns, so they have to be reachable from another translation unit.
EXPORT u32 gPendingColorDepth;

// pending resolution chosen in the display options menu, applied on Confirm.
// addresses 0x2E096F8 / 0x2E0970C, tentative names. They fall between
// idb_globals.txt's 0x2E096D8 gLowGraphicsRelated and 0x2E09710 "Data"
// (our gDisplayModeContext), not inside either.
EXPORT u32 gPendingResolutionX;
EXPORT u32 gPendingResolutionY;

// @Ok
void PCSHELL_DoDisplayOptions(void)
{
	CMenu* menu = new CMenu(0x10E, 0x6E, 2u, 0x100, 0x100, 0x14);

	i32 brightness = gBrightnessRelated;

	// eases the shell box towards x=384 every frame, starting at x=512
	// (slides in from further right). the volatile write forces MSVC to
	// store the immediate directly to the stack slot right here (matching
	// the original), instead of homing it in a register for the whole
	// function, which changes unrelated register allocation.
	i32 easeX;
	*(volatile i32*)&easeX = 0x200;

	u32 depth = DXINIT_GetNextColorDepth(0);

	if (depth != gPendingColorDepth)
	{
		u32 first = depth;

		do
		{
			depth = DXINIT_GetNextColorDepth(depth);
		} while (depth != first && depth != gPendingColorDepth);
	}

	gPendingColorDepth = depth;

	menu->AddEntry(STR_DISPLAY_RESOLUTION);
	menu->AddEntry(STR_DISPLAY_COLOR_DEPTH);
	menu->AddEntry(STR_DISPLAY_BRIGHTNESS);

	menu->scrollbar_zero = 1;
	menu->Zoom(0);
	menu->SetLine(0);

	if (!g3DAccelator || !gMMXSupport)
		menu->EntryEnable(1, 0);

	gShellTitleBarRelated = 0;

	for (;;)
	{
		i32 startVblanks = G_VBLANKS;

		gsub_430880();

		Db_FlipClear();
		CalcPolyBufferEnd();

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1, -1);

		shell_optimized_func(easeX, 0xDE, 0);

		Shell_DrawBackground();
		Shell_DrawTitleBar(gShellTitleBarRelated, 0x26, STR_DISPLAY_OPTIONS_TITLE, 1, 0, 0x96, -21, 29);

		if (menu->FinishedZooming())
		{
			char text[0x40];

			PShell_SmallFont();

			sprintf(text, FMT_DISPLAY_RESOLUTION, gPendingResolutionX, gPendingResolutionY);
			Mess_DrawText(0x15E, 0x6D, text, 0, 0x1000);

			if (!g3DAccelator || !gMMXSupport)
				Mess_SetRGB(26, 23, 41, 0);

			Mess_SetRGB(0x80, 0x80, 0x80, 0);

			sprintf(text, FMT_DISPLAY_NUMBER, gPendingColorDepth);
			Mess_DrawText(0x15E, 0x81, text, 0, 0x1000);

			Mess_SetRGB(0x80, 0x80, 0x80, 0);

			sprintf(text, FMT_DISPLAY_NUMBER, brightness);
			Mess_DrawText(0x15E, 0x95, text, 0, 0x1000);

			PShell_NormalFont();
		}

		menu->Display();

		if (!(gRenderTest & 0x10) && gCursorSprite && PCINPUT_GetMouseStatus())
		{
			gCursorSprite->draw(gShellMouseX, gShellMouseY, 0, 0);
		}

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		Pad_Update();

		gShellTitleBarRelated = PShell_MoveTowards(gShellTitleBarRelated, 0xA4);
		easeX = PShell_MoveTowards(easeX, 0x180);

		menu->Update();

		if (menu->FinishedZooming())
		{
			if (PCSHELL_CheckTriggers(0x10110, 1, 1))
			{
				G_SCONTROL->Start.Triggered = 0;
				G_SCONTROL->X.Triggered = 0;

				SFX_Play(0x23, 0x2000, 0);
				DXINIT_SetDisplayOptions(gDxResolutionX, gDxResolutionY, gColorCount, 0, brightness);
			}

			if (PCSHELL_CheckTriggers(0x20220, 1, 1))
			{
				G_SCONTROL->Start.Triggered = 0;
				G_SCONTROL->Circle.Triggered = 0;

				SFX_Play(0x23, 0x2000, 0);
				DXINIT_SetDisplayOptions(gDxResolutionX, gDxResolutionY, gColorCount, 0, brightness);

				PCINPUT_SetMouseBounds(0, 0, gDxResolutionX - 32, gDxResolutionY - 32);
				PCINPUT_SetMousePosition((gDxResolutionX - 32) >> 1, (gDxResolutionY - 32) >> 1);

				break;
			}

			i32 line = menu->mLine;

			if (line == 0)
			{
				if (PCSHELL_CheckTriggers(0xC00C, 1, 0))
				{
					G_SCONTROL->Right.Triggered = 0;
					G_SCONTROL->Left.Triggered = 0;

					u8 found;

					if (PCSHELL_CheckTriggers(0x4004, 1, 1))
					{
						found = DXINIT_GetPrevResolution(&gPendingResolutionX, &gPendingResolutionY, gPendingColorDepth, 0, 0);
					}
					else
					{
						found = DXINIT_GetNextResolution(&gPendingResolutionX, &gPendingResolutionY, gPendingColorDepth, 0, 0);
					}

					if (found)
						SFX_Play(0x1F, 0x2000, 0);
				}
			}

			if (line == 1)
			{
				if (PCSHELL_CheckTriggers(0xC00C, 1, 0))
				{
					if (PCSHELL_CheckTriggers(0x4004, 1, 1))
					{
						gPendingColorDepth = DXINIT_GetPrevColorDepth(gPendingColorDepth);
					}
					else if (PCSHELL_CheckTriggers(0x8008, 1, 1))
					{
						gPendingColorDepth = DXINIT_GetNextColorDepth(gPendingColorDepth);
					}

					SFX_Play(0x1F, 0x2000, 0);

					if (!DXINIT_GetNextResolution(&gPendingResolutionX, &gPendingResolutionY, gPendingColorDepth, 0, 1))
					{
						DXINIT_GetPrevResolution(&gPendingResolutionX, &gPendingResolutionY, gPendingColorDepth, 0, 1);
					}
				}
			}

			if (line == 2)
			{
				if (PCSHELL_CheckTriggers(0x4004, 1, 1) && brightness > 0)
				{
					brightness--;
					SFX_Play(0x1F, 0x2000, 0);
				}
				else if (PCSHELL_CheckTriggers(0x8008, 1, 1) && brightness < 9)
				{
					brightness++;
					SFX_Play(0x1F, 0x2000, 0);
				}
			}
		}

		gsub_430680();
		WinYield();
		Sleep(10);

		Pause(startVblanks - G_VBLANKS + 2);
	}

	Pad_ClearTriggers(G_SCONTROL);

	delete menu;

	SPIDEYDX_SaveSettings();
}

// @Ok
void PCSHELL_DrawMouseCursor(void)
{
	if (!(gRenderTest & 0x10) && gCursorSprite)
	{
		gCursorSprite->draw(gShellMouseX, gShellMouseY, 0, 0);
	}
}

// @Ok
void PCSHELL_Initialize(void)
{
	if (!gCursorSprite)
	{
		gCursorSprite = new Sprite2("lti\\cursor.bmp", 0, 0, 0, 33);

		PCINPUT_SetMouseHotspot(15, 15);
		PCINPUT_SetMouseBounds(0, 0, gDxResolutionX - 32, gDxResolutionY - 32);
		PCINPUT_SetMousePosition((gDxResolutionX - 32) >> 1, (gDxResolutionY - 32) >> 1);
	}

	PCINPUT_GetMousePosition(&gShellMouseX, &gShellMouseY);
	PCSHELL_CoordsPCtoDC(&gShellMouseX, &gShellMouseY);
}

// @Ok
u8 PCSHELL_IsMouseOver(
		i32 a1,
		i32 a2,
		i32 a3,
		i32 a4)
{
	i32 s1 = a1;
	i32 s2 = a2;
	i32 s3 = a3;
	i32 s4 = a4;

	if (gRenderTest & 0x10)
		return 0;

	PCSHELL_CoordsDCtoPC(&s1, &s2);
	PCSHELL_CoordsDCtoPC(&s3, &s4);

	return PCINPUT_IsMouseOver(s1, s2, s3, s4);
}

// @Ok
// @Matching
u8 PCSHELL_IsMouseOverText(const char* pText, i32 x, i32 y, i32 justification)
{
	if (gRenderTest & 0x10)
		return 0;

	i32 width = Mess_TextWidth(pText);
	i32 height = Mess_TextHeight((char*)pText);
	i32 x1;

	switch (justification)
	{
	case 0:
		x1 = x - (width >> 1);
		break;
	case 1:
		x1 = x;
		break;
	case 2:
		x1 = x - width;
		break;
	}

	i32 y1 = y - height;

	return PCSHELL_IsMouseOver(x1, y1, x1 + width, y1 + height);
}

// @Ok
// @Matching
i32 PCSHELL_MouseMoved(void)
{
	return gShellMouseOffsetX || gShellMouseOffsetY;
}

// @Ok
// @Matching
void PCSHELL_Relax(void)
{
	WinYield();
	Sleep(10);
}

// @Ok
// @Matching
void PCSHELL_Shutdown(void)
{
	if (gCursorSprite)
	{
		delete gCursorSprite;
		gCursorSprite = 0;
	}
}

// @Ok
u8 PCSHELL_UpdateMouse(void)
{
	for (i32 i = 0;
			i < MOUSE_TRIGGER_COUNT;
			i++)
	{
		gMouseTriggerRelated[i] = 0;
	}

	if (!(gRenderTest & 0x10))
	{
		if (PCINPUT_UpdateMouse())
		{
			i32 oldMouseX = gShellMouseX;
			i32 oldMouseY = gShellMouseY;

			PCINPUT_GetMousePosition(&gShellMouseX, &gShellMouseY);
			PCSHELL_CoordsPCtoDC(&gShellMouseX, &gShellMouseY);
			
			gShellMouseOffsetX = gShellMouseX - oldMouseX;
			gShellMouseOffsetY = gShellMouseY - oldMouseY;

			return 1;
		}
		else
		{
			gShellMouseOffsetX = 0;
			gShellMouseOffsetY = 0;
		}
	}
	
	return 0;
}

// PCSHELL_DoControllerConfig's original calls this as a real out-of-line function, keep the MSVC inliner away
#ifdef _MSC_VER
#pragma auto_inline(off)
#endif
// @Bogus
void shell_optimized_func(i32, i32, i32)
{
	printf("void shell_optimized_func(i32, i32, i32)");
}
#ifdef _MSC_VER
#pragma auto_inline(on)
#endif

// @Ok
void displayControllerScreen(void)
{
	if (!G_SCENE_RELATED)
		PCGfx_BeginScene(1, -1);

	PShell_NormalFont();

	gControllerMenu->Display();
	gControllerMenuTwo->Display();

	shell_optimized_func(384, 222, 0);


	char *configName = STR_KB_CONFIG;
	if (gActionMapRelated)
		configName = STR_JOY_CONFIG;

	Shell_DrawTitleBar(gShellTitleBarRelated, 25, configName, 1, 0, 150, -21, 29);
	Shell_DrawBackground();

	PCSHELL_DrawMouseCursor();

	if (G_SCENE_RELATED)
		PCGfx_EndScene(1);
}

// @Ok
// @Matching
// The Mac build places CMenu::EntryEnable in PCShell.cpp, initActionMaps inlines it.
void CMenu::EntryEnable(u32 a2, u32 a3)
{
	this->mEntry[a2].what = a3 == 0;
	if (a3)
	{
		this->SetNormalColor(a2, 69, 60, 107);
		this->SetSelColor(a2, 128, 128, 128);
	}
	else
	{
		this->SetNormalColor(a2, 26, 23, 41);
		this->SetSelColor(a2, 26, 23, 41);
	}
}

// original address 0x4015b0, IDA calls it nullsub_1: a genuinely empty
// function in the game binary (just a ret), not one of our printf stubs.
// CMenu::ProcessMouse calls it once per scrollbar hit with a debug string,
// but the body does nothing with it. Kept out of line (not just dropped) to
// mirror the original call shape.
// @Ok
EXPORT void gsub_4015B0(const char*)
{
}

// scrollbar debug strings, addresses and content read directly from the
// original exe via IDA (0x568828, 0x568810, 0x568840, 0x5687F8, 0x5687E0).
static char* STR_SCROLLBAR_LINE_UP = "Scrollbar - Line Up.\r\n";
static char* STR_SCROLLBAR_LINE_DOWN = "Scrollbar - Line Down\r\n";
static char* STR_SCROLLBAR_THUMB = "Scrollbar - Thumb\r\n";
static char* STR_SCROLLBAR_PAGE_UP = "Scrollbar - Page Up\r\n";
static char* STR_SCROLLBAR_PAGE_DOWN = "Scrollbar - Page Down\r\n";

// Mac symbol ProcessMouse__5CMenuFv, address 0x50C8A0. Same-TU rule as
// EntryEnable above: CMenu::Update (front.cpp) calls this with a direct
// call, so it must live out of line here, not as a printf stub in front.cpp
// (that would get auto-inlined under /Ob2 and corrupt CMenu::Update's match).
//
// Handles mouse input for a menu: dragging the scrollbar thumb, clicking the
// scrollbar arrows/page areas, and hovering/clicking a text line. Returns 1
// if a scrollbar action was taken, 2 if the highlighted line changed because
// of hover, 0 otherwise.
// @Ok
i32 CMenu::ProcessMouse(void)
{
	if (gRenderTest & 0x10)
		return 0;

	u8 savedLine = this->mLine;

	if (this->field_30 != 0 && PCSHELL_CheckTriggers(0x100, 0, 1))
	{
		// currently dragging the scrollbar thumb: convert the mouse Y motion
		// this frame into a line-by-line scroll, using field_34 as the pixel
		// distance per line and field_38 as the leftover drag distance.
		this->field_38 = this->field_38 + (f32)gShellMouseOffsetY;

		if (this->field_38 < this->field_34)
		{
			do
			{
				if (this->mCursorLine < this->mNumLines - this->field_1B)
					this->mCursorLine++;

				this->field_38 = this->field_38 - this->field_34;
			} while (this->field_38 < this->field_34);
		}

		f32 limit = (f32)(i32)(-this->field_34);

		if (this->field_38 < limit)
		{
			do
			{
				if (this->mCursorLine != 0)
					this->mCursorLine--;

				this->field_38 = this->field_38 + this->field_34;
			} while (this->field_38 < limit);
		}

		return 1;
	}

	this->field_30 = 0;

	if (this->ptr_to != 0 && PCSHELL_CheckTriggers(0x100, 1, 1))
	{
		i32 x, y;
		PCINPUT_GetMouseHotspotPosition(&x, &y);
		PCSHELL_CoordsPCtoDC(&x, &y);

		switch (this->ptr_to->ScrollBarHitTest(x, y))
		{
		case 1: // up arrow
			gsub_4015B0(STR_SCROLLBAR_LINE_UP);

			if (this->mCursorLine != 0)
			{
				this->mCursorLine--;
				return 1;
			}
			break;

		case 2: // down arrow
		{
			gsub_4015B0(STR_SCROLLBAR_LINE_DOWN);

			i32 limit = this->field_32 - this->field_1B;

			if (this->mCursorLine < limit)
			{
				do
				{
					this->mCursorLine++;
				} while (this->mCursorLine < limit && this->mEntry[this->mCursorLine].unk_b == 0);

				return 1;
			}
			break;
		}

		case 3: // thumb grab
		{
			gsub_4015B0(STR_SCROLLBAR_THUMB);

			CExpandingBox* box = this->ptr_to;
			this->field_30 = 1;

			i32 range = this->mNumLines - this->field_1B;
			f32 boxHeight = (f32)(box->field_8 - box->field_2C - 21);

			this->field_38 = 0.0f;
			this->field_34 = (boxHeight / (f32)range / 512.0f) * (f32)gDxResolutionX;

			return 1;
		}

		case 4: // page up
			gsub_4015B0(STR_SCROLLBAR_PAGE_UP);

			if (this->mCursorLine != 0)
			{
				i32 pageSize = this->field_1B - 1;
				u8 count = 0;

				while (count < pageSize)
				{
					if (this->mCursorLine == 0)
						break;

					this->mCursorLine--;

					if (this->mEntry[this->mCursorLine].unk_b != 0)
						count++;
				}

				return 1;
			}
			break;

		case 5: // page down
		{
			gsub_4015B0(STR_SCROLLBAR_PAGE_DOWN);

			i32 limit = this->field_32 - this->field_1B;

			if (this->mCursorLine < limit)
			{
				i32 pageSize = this->field_1B - 1;
				u8 count = 0;

				while (count < pageSize)
				{
					if (this->mCursorLine >= limit)
						break;

					this->mCursorLine++;

					if (this->mEntry[this->mCursorLine].unk_b != 0)
						count++;
				}

				return 1;
			}
			break;
		}
		}
	}

	// no scrollbar action taken this frame: reset the left-click trigger
	// latch and, if the mouse moved, check whether it is now hovering a
	// different (enabled) text line.
	gMouseTriggerRelated[7] = 0;

	if (PCSHELL_MouseMoved())
	{
		i32 y = this->mY;

		for (i32 i = this->mCursorLine;
				i < this->mNumLines && i < (this->mCursorLine + this->field_1B);
				i++)
		{
			y += this->mEntry[i].unk_a;

			if (this->mEntry[i].unk_b)
			{
				if (PCSHELL_IsMouseOverText(this->mEntry[i].name, this->mX, y, this->mJustification))
				{
					if (this->mEntry[i].what == 0)
						this->SetLine((char)i);
				}

				y += this->mLineSep;
			}
		}
	}

	return this->mLine != savedLine ? 2 : 0;
}

// @Ok
// @Matching
void initActionMaps(void)
{
	for (
			i32 i = 0;
			i < ACTION_MAP_COUNT; 
			i++)
	{
		SActionMap *pMap = &gActionMaps[i];
		PCINPUT_GetKeyboardMappingForAction(pMap->field_0, &pMap->field_14);
		PCINPUT_GetControllerMappingForAction(pMap->field_0, &pMap->field_18);
		gControllerMenu->AddEntry(pMap->field_4);

		if (!gActionMapRelated)
		{
			if (pMap->field_14 == 0x4000)
			{
				gControllerMenuTwo->SetNormalColor(i, 90, 20, 6);
				strcpy(gKeyNames[i], "none");
			}
			else
			{
				DXINPUT_GetKeyName(pMap->field_14, gKeyNames[i]);
			}
		}
		else
		{
			if (i < 4)
			{
				gControllerMenu->EntryEnable(i, 0);
				gControllerMenuTwo->EntryEnable(i, 0);
				strcpy(gKeyNames[i], pMap->field_4);
			}
			else
			{

				if (pMap->field_18 == 0x4000)
				{
					gControllerMenuTwo->SetNormalColor(i, 90, 20, 6);
					strcpy(gKeyNames[i], "none");
				}
				else
				{
					sprintf(gKeyNames[i], "button %i", pMap->field_18);
				}
			}
		}

		gControllerMenuTwo->AddEntry(gKeyNames[i]);
	}

	gControllerMenu->AddEntry(STR_RESTORE_DEFAULTS);
	gControllerMenuTwo->AddEntry("");
}

// PCSHELL_DoControllerConfig's original calls this as a real out-of-line function, keep the MSVC inliner away
#ifdef _MSC_VER
#pragma auto_inline(off)
#endif
// @Ok
u8 processControllerScreen(void)
{
	u32 mLine = gControllerMenu->mLine;

	gShellTitleBarRelated = PShell_MoveTowards(gShellTitleBarRelated, 200);
	Pad_Update();

	if (gShellWaitingForInput != 0)
	{
		if (gActionMapRelated == 0)
		{
			// keyboard mode: wait for a key press. keys 1, 28, 156 are reserved
			// (the original skips them via a 3-entry table at 0x5687C4).
			static const i32 skipKeys[3] = { 1, 28, 156 };
			for (i32 key = 0; key < 256; key++)
			{
				i32 skipped = 0;
				for (i32 s = 0; s < 3; s++)
				{
					if (key == skipKeys[s])
					{
						skipped = 1;
						break;
					}
				}
				if (!skipped && PCINPUT_IsKeyPressed((u8)key, 1))
				{
					gShellKeyboardKey = key;
					gShellWaitingForInput = 0;
					DXINPUT_GetKeyName((u8)key, gKeyNames[mLine]);
					break;
				}
			}

			if (gShellWaitingForInput != 0)
			{
				if (PCSHELL_CheckTriggers(544, 1, 1))
				{
					SFX_Play(0x23, 0x2000, 0);
					gShellWaitingForInput = 0;
					gShellKeyboardKey = 0x4000;
				}
				else
				{
					return 0;
				}
			}

			if (gShellKeyboardKey != 0x4000)
			{
				PCINPUT_SetKeyboardMappingForAction(gActionMaps[mLine].field_0, gShellKeyboardKey);
				gActionMaps[mLine].field_14 = gShellKeyboardKey;

				// if another line already uses this key, clear it
				for (i32 i = 0; i < ACTION_MAP_COUNT; i++)
				{
					if (i != mLine && gActionMaps[i].field_14 == gShellKeyboardKey)
					{
						gActionMaps[i].field_14 = 0x4000;
						PCINPUT_SetKeyboardMappingForAction(gActionMaps[i].field_0, 0x4000);
						gControllerMenuTwo->SetNormalColor(i, 90, 20, 6);
						strcpy(gKeyNames[i], "none");
					}
				}
			}
			else if (gActionMaps[mLine].field_14 != 0x4000)
			{
				DXINPUT_GetKeyName((u8)gActionMaps[mLine].field_14, gKeyNames[mLine]);
			}
			else
			{
				gControllerMenuTwo->SetNormalColor(mLine, 90, 20, 6);
				strcpy(gKeyNames[mLine], "none");
			}

			*reinterpret_cast<u8*>(&gControllerMenu->field_1E) = 0;
			*reinterpret_cast<u8*>(&gControllerMenuTwo->field_1E) = 0;
			gShellControllerButton = -1;
			gShellKeyboardKey = -1;
			return 0;
		}

		// controller mode: wait for a button press
		i32 numButtons = PCINPUT_GetNumControllerButtons();
		for (i32 button = 0; button < numButtons; button++)
		{
			if (PCINPUT_IsControllerButtonPressed(button, 1))
			{
				gShellControllerButton = button;
				gShellWaitingForInput = 0;
				sprintf(gKeyNames[mLine], "button %i", button);
				break;
			}
		}

		if (gShellWaitingForInput != 0)
		{
			if (PCSHELL_CheckTriggers(544, 1, 1))
			{
				SFX_Play(0x23, 0x2000, 0);
				gShellWaitingForInput = 0;
				gShellControllerButton = 0x4000;
			}
			else
			{
				return 0;
			}
		}

		if (gShellControllerButton != 0x4000)
		{
			PCINPUT_SetControllerMappingForAction(gActionMaps[mLine].field_0, gShellControllerButton);
			gActionMaps[mLine].field_18 = gShellControllerButton;

			// if another line already uses this button, clear it
			for (i32 i = 0; i < ACTION_MAP_COUNT; i++)
			{
				if (i != mLine && gActionMaps[i].field_18 == gShellControllerButton)
				{
					gActionMaps[i].field_18 = 0x4000;
					PCINPUT_SetControllerMappingForAction(gActionMaps[i].field_0, 0x4000);
					gControllerMenuTwo->SetNormalColor(i, 90, 20, 6);
					strcpy(gKeyNames[i], "none");
				}
			}
		}
		else if (gActionMaps[mLine].field_18 != 0x4000)
		{
			sprintf(gKeyNames[mLine], "button %i", gActionMaps[mLine].field_18);
		}
		else
		{
			gControllerMenuTwo->SetNormalColor(mLine, 90, 20, 6);
			strcpy(gKeyNames[mLine], "none");
		}

		*reinterpret_cast<u8*>(&gControllerMenu->field_1E) = 0;
		*reinterpret_cast<u8*>(&gControllerMenuTwo->field_1E) = 0;
		gShellControllerButton = -1;
		gShellKeyboardKey = -1;
		return 0;
	}

	// not waiting: menu navigation
	gControllerMenu->Update();
	gControllerMenuTwo->mLine = gControllerMenu->mLine;

	if (!gControllerMenu->FinishedZooming())
	{
		return 0;
	}

	if (gActionMapRelated == 1 && gControllerMenu->mLine < 4)
	{
		gControllerMenu->SetLine(4);
		gControllerMenuTwo->SetLine(4);
	}

	if (PCSHELL_CheckTriggers(131616, 1, 1))
	{
		G_SCONTROL[0].Circle.Triggered = 0;
		SFX_Play(0x23, 0x2000, 0);
		return 1;
	}

	if (gControllerMenu->mLine >= 0x28 || !PCSHELL_CheckTriggers(65808, 1, 1))
	{
		return 0;
	}

	if (gControllerMenu->mLine >= 0xB)
	{
		if (gActionMapRelated != 0)
			PCINPUT_RestoreDefaultControllerSettings();
		else
			PCINPUT_RestoreDefaultKeyboardSettings();
		SFX_Play(0x1F, 0x2000, 0);
		resetActionMaps(gActionMapRelated == 0);
		return 0;
	}

	G_SCONTROL[0].Start.Triggered = 0;
	G_SCONTROL[0].X.Triggered = 0;
	SFX_Play(0x1F, 0x2000, 0);
	gShellWaitingForInput = 1;
	gShellControllerButton = 0x4000;
	gShellKeyboardKey = 0x4000;
	gControllerMenuTwo->SetNormalColor(gControllerMenu->mLine, 69, 60, 107);
	strcpy(gKeyNames[gControllerMenu->mLine], "???");
	*reinterpret_cast<u8*>(&gControllerMenu->field_1E) = 1;
	*reinterpret_cast<u8*>(&gControllerMenuTwo->field_1E) = 1;
	return 0;
}
#ifdef _MSC_VER
#pragma auto_inline(on)
#endif

// @Ok
// @Matching
void resetActionMaps(bool a1)
{
	delete gControllerMenu;
	delete gControllerMenuTwo;

	gControllerMenu = new CMenu(30, 60, 1u, 256, 256, 15);
	gControllerMenuTwo = new CMenu(332, 60, 1u, 256, 256, 15);

	initActionMaps();

	gControllerMenuTwo->scrollbar_zero = 0;
	gControllerMenu->scrollbar_zero = 0;

	gControllerMenu->Zoom(0);
	gControllerMenuTwo->Zoom(0);
	
	gControllerMenuTwo->mLine = a1 != 0 ? 0 : 4;
	gControllerMenu->mLine = gControllerMenuTwo->mLine;
}

void validate_SActionMap(void)
{
	VALIDATE_SIZE(SActionMap, 0x1C);

	VALIDATE(SActionMap, field_0, 0x0);
	VALIDATE(SActionMap, field_4, 0x4);
	VALIDATE(SActionMap, field_14, 0x14);
	VALIDATE(SActionMap, field_18, 0x18);
}
