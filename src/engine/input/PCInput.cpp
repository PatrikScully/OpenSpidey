#include "engine/input/PCInput.h"
#include "game/application/SpideyDX.h"
#include "engine/input/input_devices.h"
#include "engine/rendering/polygon_pipeline.h"
#include "compatibility/non_win32.h"
#include "game/application/DXinit.h"

#include <cstring>

#include "compatibility/validate.h"

EXPORT u8 gMouseStatus;

EXPORT i32 gMouseBoundOne;
EXPORT i32 gMouseBoundTwo;
EXPORT i32 gMouseBoundThree;
EXPORT i32 gMouseBoundFour;

EXPORT i32 gMouseX;
EXPORT i32 gMouseY;

EXPORT i32 gMouseHotSpotX;
EXPORT i32 gMouseHotSpotY;

EXPORT u32 gPcInputTickRelated;

EXPORT i32 gOldMouseX;
EXPORT i32 gOldMouseY;

EXPORT i32 gControllerX;
EXPORT i32 gControllerY;

EXPORT u8 gDefaultKeyboardMappings[0x70] =
{
  0x01, 0x00, 0x00, 0x00, 0xC8, 0x00, 0x00, 0x00, 0x02, 0x00, 
  0x00, 0x00, 0xD0, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 
  0xCB, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0xCD, 0x00, 
  0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x50, 0x00, 0x00, 0x00, 
  0x20, 0x00, 0x00, 0x00, 0x4D, 0x00, 0x00, 0x00, 0x40, 0x00, 
  0x00, 0x00, 0x4B, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 
  0x48, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x47, 0x00, 
  0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x51, 0x00, 0x00, 0x00, 
  0x00, 0x04, 0x00, 0x00, 0x49, 0x00, 0x00, 0x00, 0x00, 0x10, 
  0x00, 0x00, 0x1C, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 
  0x0F, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 
  0x00, 0x00
};

EXPORT u8 gKeyboardMappings[0x70] =
{
  0x01, 0x00, 0x00, 0x00, 0xC8, 0x00, 0x00, 0x00, 0x02, 0x00, 
  0x00, 0x00, 0xD0, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 
  0xCB, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0xCD, 0x00, 
  0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x50, 0x00, 0x00, 0x00, 
  0x20, 0x00, 0x00, 0x00, 0x4D, 0x00, 0x00, 0x00, 0x40, 0x00, 
  0x00, 0x00, 0x4B, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 
  0x48, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x47, 0x00, 
  0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x51, 0x00, 0x00, 0x00, 
  0x00, 0x04, 0x00, 0x00, 0x49, 0x00, 0x00, 0x00, 0x00, 0x10, 
  0x00, 0x00, 0x1C, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 
  0x0F, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 
  0x00, 0x00
};

EXPORT u8 gControllerMappings[0x70] =
{
  0x01, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x02, 0x00, 
  0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 
  0x00, 0x40, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x40, 
  0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 
  0x20, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x40, 0x00, 
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 
  0x01, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x06, 0x00, 
  0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 
  0x00, 0x04, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x00, 0x10, 
  0x00, 0x00, 0x0B, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 
  0x00, 0x40, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 
  0x00, 0x00
};

EXPORT u8 gDefaultControllerMappings[0x70] =
{
  0x01, 0x00, 0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x02, 0x00, 
  0x00, 0x00, 0x00, 0x40, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 
  0x00, 0x40, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x40, 
  0x00, 0x00, 0x10, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00, 
  0x20, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x40, 0x00, 
  0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 
  0x01, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x06, 0x00, 
  0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x09, 0x00, 0x00, 0x00, 
  0x00, 0x04, 0x00, 0x00, 0x07, 0x00, 0x00, 0x00, 0x00, 0x10, 
  0x00, 0x00, 0x0B, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 
  0x00, 0x40, 0x00, 0x00, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 
  0x00, 0x00
};

// @Ok
// @Matching
i32 PCINPUT_GetMouseStatus(void)
{
	return gMouseStatus;
}


// @Ok
// @Matching
void PCINPUT_SetMouseBounds(i32 a1, i32 a2, i32 a3, i32 a4)
{
	gMouseBoundOne = a1;
	gMouseBoundTwo = a2;
	gMouseBoundThree = a3;
	gMouseBoundFour = a4;
}

// @Ok
// @Matching
void PCINPUT_GetMousePosition(i32* x, i32* y)
{
	*x = gMouseX;
	*y = gMouseY;
}

EXPORT u8 gControllerAxesRelatedOne;
EXPORT i32 gControllerAxesRelatedTwo = -1;

// @Ok
// @Matching
void PCINPUT_FreezeControllerAxes(void)
{
	gControllerAxesRelatedOne = 1;
	gControllerAxesRelatedTwo = -1;
}

// @Ok
// @Matching
 void PCINPUT_ClearKeyState(u8 a1)
{
	DXINPUT_SetKeyState(a1, 0);
}

// @Ok
// Not in the PC binary (only the Mac build has it), same shape as PCINPUT_ClearKeyState
 void PCINPUT_ClearMouseState(u8 a1)
{
	DXINPUT_SetMouseButtonState(a1, 0);
}

// @Ok
u8 PCINPUT_GetControllerDirections(void)
{
  u8 result = 0;

  if (!gControllerAxesRelatedOne)
  {
    if (gControllerX < -250)
      result = 4;
    if (gControllerX > 250)
      result |= 8u;
    if (gControllerY < -250)
      result |= 1u;
    if (gControllerY > 250)
      result |= 2u;
  }

  if (gControllerAxesRelatedTwo != -1)
  {
    if (gControllerAxesRelatedTwo > 18000)
      result |= 4u;

    if (gControllerAxesRelatedTwo > 0 && gControllerAxesRelatedTwo < 18000)
      result |= 8u;

    if (gControllerAxesRelatedTwo < 9000 || gControllerAxesRelatedTwo > 27000)
      result |= 1u;

    if (gControllerAxesRelatedTwo > 9000 && gControllerAxesRelatedTwo < 27000)
      result |= 2;
  }

  return result;
}

// @Ok
void PCINPUT_GetControllerMappingForAction(u32 a1, u32* a2)
{
	SMapping* mappings = reinterpret_cast<SMapping*>(gControllerMappings);
	for(i32 i = 0;; i++)
	{
		if (mappings[i].field_0 == 0x8000)
			break;

		if (a1 == mappings[i].field_0)
		{
			*a2 = mappings[i].field_4;
			return;
		}
	}

	*a2 = 0x4000;
}

// @Ok
void PCINPUT_GetKeyboardMappingForAction(
		u32 a1,
		u32* a2)
{
	SMapping* mappings = reinterpret_cast<SMapping*>(gKeyboardMappings);
	for(i32 i = 0;; i++)
	{
		if (mappings[i].field_0 == 0x8000)
			break;

		if (a1 == mappings[i].field_0)
		{
			*a2 = mappings[i].field_4;
			return;
		}
	}

	*a2 = 0x4000;
}

// @Ok
// Reworked from the earlier @NotOk draft. The old version was missing three
// things found by reading the original disasm at 0x50a190: (1) it never
// zeroed *a1/*a2 before the loops, (2) it used = instead of |= so only the
// last matching mapping survived instead of every pressed action being
// OR-ed into the bitmask, (3) it never added the analog stick / POV
// direction bits (the original inlines PCINPUT_PollController and then ORs
// in the same four bits that PCINPUT_GetControllerDirections computes, since
// PCINPUT_PollController is INLINE and only ever called from this TU).
void PCINPUT_GetMappedStates(u32* a1, u32* a2)
{
	*a1 = 0;
	*a2 = 0;

	if (PCINPUT_PollKeyboard())
	{
		SMapping* kbMappings = reinterpret_cast<SMapping*>(gKeyboardMappings);
		for (i32 i = 0; ; i++)
		{
			if (kbMappings[i].field_0 == 0x8000)
				break;

			if (kbMappings[i].field_4 == 0x4000)
				continue;

			if (PCINPUT_IsKeyPressed(kbMappings[i].field_4, 0))
			{
				*a1 |= kbMappings[i].field_0;
			}

			if (PCINPUT_IsKeyPressed(kbMappings[i].field_4, 1))
			{
				*a2 |= kbMappings[i].field_0;
			}
		}
	}

	if (PCINPUT_PollController())
	{
		*a1 |= PCINPUT_GetControllerDirections();

		SMapping* controllerMappings = reinterpret_cast<SMapping*>(gControllerMappings);
		for (i32 j = 0; ; j++)
		{
			if (controllerMappings[j].field_0 == 0x8000)
				break;

			if (controllerMappings[j].field_4 == 0x4000)
				continue;

			if (PCINPUT_IsControllerButtonPressed(controllerMappings[j].field_4, 0))
			{
				*a1 |= controllerMappings[j].field_0;
			}

			if (PCINPUT_IsControllerButtonPressed(controllerMappings[j].field_4, 1))
			{
				*a2 |= controllerMappings[j].field_0;
			}
		}
	}
}

// @Ok
// @Matching
void PCINPUT_GetMouseHotspotPosition(i32* a1, i32* a2)
{
	*a1 = gMouseX + gMouseHotSpotX * gGameResolutionX / 640;
	*a2 = gMouseY + gMouseHotSpotY * gGameResolutionY / 480;
}

// @Ok
i32 PCINPUT_GetNumControllerButtons(void)
{
	return DXINPUT_GetNumControllerButtons();
}

// @Ok
u8 PCINPUT_Initialize(void)
{
	DXINPUT_Initialize(gDirectInputRelated, gHwnd);
	if (!DXINPUT_SetupKeyboard((gRenderTest & 1) == 0, 1))
	{
		DXERR_printf("A keyboard device could not be set up.\n");
		return 0;
	}

	if ((gRenderTest & 0x10) == 0)
	{
		if (!DXINPUT_SetupMouse((gRenderTest & 1) == 0))
		{
			DXERR_printf("A mouse device could not be set up.\n");
			return 0;
		}

		gMouseStatus = 1;
	}

	if (!DXINPUT_SetupController())
		DXERR_printf("A game controller device could not be set up.\n");

	return 1;
}

// @Ok
// @Matching
i32 PCINPUT_IsControllerButtonPressed(u8 a1, i32 a2)
{
	if (a2)
		return DXINPUT_GetControllerButtonState(a1) == 255;
	else
		return DXINPUT_GetControllerButtonState(a1) & 0x7F;
}

// @Ok
// @Matching
 i32 PCINPUT_IsKeyPressed(u8 a1, i32 a2)
{
	if (a2)
		return DXINPUT_GetKeyState(a1) == 255;
	else
		return DXINPUT_GetKeyState(a1) & 0x7F;
}

// @Ok
// @Matching
i32 PCINPUT_IsMouseButtonPressed(u8 a1, i32 a2)
{
	// 0x50A7E7: cmp al,0FFh. The state is a u8, so comparing it with -1
	// (an int) is always false and no click was ever seen.
	if ( a2 )
		return DXINPUT_GetMouseButtonState(a1) == 0xFF;
	else
		return DXINPUT_GetMouseButtonState(a1) & 0x7F;
}

// @Ok
// @Matching
i32 PCINPUT_IsMouseButtonReleased(u8 button)
{
	return DXINPUT_GetMouseButtonState(button) == 0x80;
}

// @Ok
// @Matching
i32 PCINPUT_IsMouseOver(
		i32 a1,
		i32 a2,
		i32 a3,
		i32 a4)
{
	i32 mouseX = gMouseX + gMouseHotSpotX * G_GAME_RESOLUTION_X / 640;
	i32 mouseY = gMouseY + gMouseHotSpotY * G_GAME_RESOLUTION_Y / 480;

	return mouseX > a1 && mouseX < a3 && mouseY > a2 && mouseY < a4;
}

// @Ok
 u8 PCINPUT_PollController(void)
{
	if (!DXINPUT_PollController(&gControllerX, &gControllerY, &gControllerAxesRelatedTwo))
		return 0;

	if (gControllerAxesRelatedOne &&
			gControllerX < 250 &&
			gControllerX > -250 &&
			gControllerY < 250 &&
			gControllerY > -250)
	{
		gControllerAxesRelatedOne = 0;
	}

	return 1;
}

// @Ok
// @Matching
 u8 PCINPUT_PollKeyboard(void)
{
	if (DXINPUT_PollKeyboard() < 0)
		return 0;

	checkDebugKeypress();
	return 1;
}

// @Ok
void PCINPUT_RestoreDefaultControllerSettings(void)
{
	memcpy(gControllerMappings, gDefaultControllerMappings, sizeof(gControllerMappings));
}

// @Ok
// @Matching
void PCINPUT_RestoreDefaultKeyboardSettings(void)
{
	memcpy(gKeyboardMappings, gDefaultKeyboardMappings, sizeof(gKeyboardMappings));
}

// @Ok
void PCINPUT_SetControllerMappingForAction(u32 a1, u32 a2)
{
	SMapping* mappings = reinterpret_cast<SMapping*>(gControllerMappings);
	for(i32 i = 0;; i++)
	{
		if (mappings[i].field_0 == 0x8000)
			break;

		if (a1 == mappings[i].field_0)
		{
			mappings[i].field_4 = a2;
			return;
		}
	}
}

// @Ok
void PCINPUT_SetKeyboardMappingForAction(u32 a1, u32 a2)
{
	SMapping* mappings = reinterpret_cast<SMapping*>(gKeyboardMappings);
	for(i32 i = 0;; i++)
	{
		if (mappings[i].field_0 == 0x8000)
			break;

		if (a1 == mappings[i].field_0)
		{
			mappings[i].field_4 = a2;
			return;
		}
	}
}

// @Ok
// @Matching
void PCINPUT_SetMouseHotspot(
		i32 a1,
		i32 a2)
{
	gMouseHotSpotX = a1;
	gMouseHotSpotY = a2;
}

// @Ok
 void PCINPUT_SetMousePosition(
		i32 newMouseX,
		i32 newMouseY)
{
	i32 adjustedX;
	i32 adjustedY;

	adjustedX = newMouseX;
	if ( newMouseX < gMouseBoundOne )
		adjustedX = gMouseBoundOne;
	if ( adjustedX > gMouseBoundThree )
		adjustedX = gMouseBoundThree;

	adjustedY = newMouseY;
	if ( adjustedY < gMouseBoundTwo )
		adjustedY = gMouseBoundTwo;
	if ( adjustedY > gMouseBoundFour )
		adjustedY = gMouseBoundFour;

	gMouseX = adjustedX;
	gMouseY = adjustedY;
}

// @Ok
// @Matching
u8 PCINPUT_SetupForceFeedbackSineEffect(i32 a1, f32 a2)
{
	if (gRenderTest & 0x20)
		return 1;

	return DXINPUT_SetupForceFeedbackSineEffect(a1, a2) != 0;
}

// @Ok
void PCINPUT_Shutdown(void)
{
	DXINPUT_Release();
}

// @Ok
// @Matching
u8 PCINPUT_StartForceFeedbackEffect(void)
{
	if (gRenderTest & 0x20)
		return 1;

	return DXINPUT_StartForceFeedbackEffect() != 0;
}

// @Ok
// @Matching
u8 PCINPUT_StopForceFeedbackEffect(void)
{
	if (gRenderTest & 0x20)
		return 1;

	return DXINPUT_StopForceFeedbackEffect() != 0;
}

EXPORT f32 ONE_FLOAT = 1.0f;

// @Ok
u8 PCINPUT_UpdateMouse(void)
{
	i32 v5 = 0;
	i32 v6 = 0;
	if ( !gMouseStatus || !DXINPUT_PollMouse(&v5, &v6) )
		return 0;

	gOldMouseX = gMouseX;
	gOldMouseY = gMouseY;

	// 0x50A8D4: the first out param of DXINPUT_PollMouse (the X delta)
	// goes to gMouseX, the second to gMouseY.
	gMouseX += (i32)(v5 * ONE_FLOAT);
	gMouseY += (i32)(v6 * ONE_FLOAT);

	PCINPUT_SetMousePosition(gMouseX, gMouseY);

	return gOldMouseX != gMouseX || gOldMouseY != gMouseY;
}

// @Ok
// @Matching
 void checkDebugKeypress(void)
{
	if (GetTickCount() >= gPcInputTickRelated &&
			PCINPUT_IsKeyPressed(0x43, 0))
	{
		DXPOLY_SaveScreen();
		PCINPUT_ClearKeyState(0x1F);
		gPcInputTickRelated = GetTickCount() + 500;
	}
}

void validate_SMapping(void)
{
	VALIDATE_SIZE(SMapping, 0x8);

	VALIDATE(SMapping, field_0, 0x0);
	VALIDATE(SMapping, field_4, 0x4);
}
