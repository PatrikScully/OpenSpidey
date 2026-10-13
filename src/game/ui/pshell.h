#pragma once

#ifndef PSHELL_H
#define PSHELL_H

#include "compatibility/export.h"
#include "engine/world/ob.h"
#include "game/application/main.h"

struct SCheat
{
	const char* pCode;
	const char* pDescription;
};

class CExpandingBox : public CClass
{

public: 

	i32 field_4;
	i32 field_8;
	i32 field_C;
	i32 field_10;
	i32 field_14;
	i32 field_18;
	i32 field_1C;
	i32 field_20;
	i32 field_24;
	i32 field_28;
	i32 field_2C;
	i32 field_30;

	EXPORT CExpandingBox(i32 a2,i32 a3,i32 a4,i32 a5,i32 a6,i32 a7,i32 a8,i32 a9,i32 a10);
	EXPORT virtual ~CExpandingBox(void) OVERRIDE;

	EXPORT i32 Display();
	EXPORT i32 ScrollBarHitTest(i32 a2, i32 a3);
};

EXPORT i32 PShell_GetNumCostumePSXs(void);
EXPORT void PShell_BigCheat(void);
EXPORT void PShell_NormalFont(void);
EXPORT void PShell_DefaultText(void);
EXPORT void PShell_SmallFont(void);
EXPORT void PShell_InstructionalText(void);
EXPORT void PShell_Initialise(void);

EXPORT i32 ActivateCheat(i32);
EXPORT void DisplayScore(i32,i32,i32,i32);
EXPORT i32 PShell_ActivateCheat(char *);
EXPORT void PShell_ApplyGameState(void);
EXPORT void PShell_BigFont(void);
EXPORT void PShell_DrawHighlight(i32,i32,i32,i32);
EXPORT void PShell_EndTrainingDisplay(void);
EXPORT void PShell_EndTrainingInit(void);
EXPORT void PShell_EndTrainingUpdate(void);
EXPORT void PShell_MaybeSaveGame(void);
EXPORT void PShell_MaybeUnlockStuff(void);
EXPORT i32 PShell_MoveTowards(i32,i32);
EXPORT i32 PShell_DrawMenuBox(i32, i32, i32, i32, i32, i32, i32, i32);

void validate_CExpandingBox(void);
void validate_SCheat(void);


static const i32 NUM_CHEATS = 24;
EXPORT extern SCheat gCheats[NUM_CHEATS];

void patch_pshell(void);

// The HUD/screen master switch at 0x0054D47C. 0 means "draw no HUD at all":
// Panel_Display reads it to bail out to just the compass, and PShell training
// clears it and restores it around the end-of-training screens
// (PShell_EndTrainingInit / PShell_EndTrainingUpdate).
//
// Defined once here rather than per-file: it is used by both pshell.cpp and
// panel.cpp, and the two previously carried separate definitions of the same
// address under the same name but with DIFFERENT types (pshell.cpp an i32
// lvalue macro, panel.cpp an i32* pointer), which is a trap.
//#define G_SCREEN_MODE_FLAG (gScreenModeFlag)
#define G_SCREEN_MODE_FLAG (*reinterpret_cast<i32*>(0x0054D47C))

#endif