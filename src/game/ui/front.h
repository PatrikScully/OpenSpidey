#pragma once

#ifndef FRONT_H
#define FRONT_H

#include "compatibility/export.h"
#include "engine/persistence/save_game_state.h"
#include "game/application/main.h"
#include "game/ui/pshell.h"
#include "game/ui/shell.h"

EXPORT extern SSaveGame gSaveGame;

// The save block the exe owns at 0x00682858, 0xBC bytes (SSaveGame in shell.h,
// which is exactly the gap to the next global at 0x00682914).  Proof from the
// disassembly: Shell_SaveGame writes the checksum with `mov [682858h],eax` at
// 0x49FB9E and hands the same base to Front_LoadGame with `push 682858h`, and
// SpideyMain pushes 682858h at 0x455F0C for the same call.
//
// This has to point at game memory.  Front_Init, Front_LoadGame, Shell_LoadGame
// and Shell_SaveGame are the writers and none of them are hooked, while
// PShell_ActivateCheat (0x0047C440) already is and writes field_78/80/84/88/8C/
// 90.  With a repo-local copy the cheats would land in our DLL and the game
// would never see them, and every hooked reader would see a zeroed save block.
//
// One macro for the whole block on purpose: every 0x006828xx address in the
// repo is gSaveGame plus an offset, so index this instead of naming a slot.
//#define G_SAVE_GAME (gSaveGame)
#define G_SAVE_GAME (*reinterpret_cast<SSaveGame*>(0x00682858))

struct SLevel
{
	char* mDisplayName;
	char* mName;

	// bit 1 (0x2) is tested by SpideyMain (main.cpp) when a level finishes to
	// decide whether the game auto-saves on the easier difficulties. Carved out
	// of the blind PADDING(0x4) that used to start here; same byte range.
	u8 field_8;

	PADDING(3);

	u16 field_C;

	PADDING(0x14-0xC-4);
};

struct SEntry
{
	const char* name;
	i16 val_a;
	i16 val_b;
	i16 field_8;
	i16 field_A;
	u8 unk_a;
	u8 unk_b;
	u8 unk_c;
	u8 unk_d;
	u8 unk_e;
	u8 field_11;
	u8 field_12;
	u8 field_13;
	u8 field_14;
	u8 field_15;
	u8 field_16;
	u8 field_17;
	u8 field_18;
	u8 field_19;
	u8 field_1A;
	u8 field_1B;
	i32 what;
};

class CMenu : public CClass
{

public:
	EXPORT virtual ~CMenu();

	EXPORT CMenu(i32 x,i32 y,u8 Justification,i32 HiScale,i32 LowScale, i32 LineSep);
	EXPORT void SetLine(char);
	EXPORT void Reset(void);
	EXPORT void SetRedText(u8);
	EXPORT i32 ChoiceIs(const char*);
	EXPORT void AdjustWidth(i32);
	EXPORT i32 FinishedZooming(void);
	EXPORT void CentreX(void);
	EXPORT i32 FindEntry(const char*);
	EXPORT void EntryOff(const char*);
	EXPORT void EntryOn(const char* a2);
	EXPORT void SetSelColor(u32, i32, i32, i32);
	EXPORT void SetNormalColor(u32, i32, i32, i32);
	EXPORT void NonGouraud(void);
	EXPORT i32 GetMenuHeight(void);
	EXPORT void CentreY(void);
	EXPORT void GetEntryXY(const char*, i32*, i32*);
	EXPORT void EntryEnable(u32, u32);
	EXPORT void AddEntry(const char*);
	EXPORT void Zoom(i32);
	EXPORT void Display(void);
	EXPORT void KillBox(void);
	EXPORT void Update(void);
	EXPORT i32 ProcessMouse(void);

	CExpandingBox* ptr_to;
	u16 menu_width;
	i16 text_val_b;
	i32 width_val_a;

	u8 scrollbar_one;
	char scrollbar_zero;

	u8 mJustification;

	PADDING(1);

	u8 mLine;
	u8 mCursorLine;

	// Found in CMenu::Display: field_16 gates a "just selected this entry"
	// highlight-particle spawn (bool-like), field_17 is compared against the
	// loop index to see which line it applies to (a "previously selected
	// line" index, guess).
	u8 field_16;
	u8 field_17;

	PADDING(2);

	u8 mNumLines;
	u8 field_1B;
	u8 mZoomBoxType;

	PADDING(1);

	i16 field_1E;

	i16 field_20;

	PADDING(2);

	i32 mX;
	i32 mY;
	i32 mLineSep;
	i16 field_30;
	u16 field_32;

	// scrollbar thumb drag state, set up in CMenu::ProcessMouse's thumb-grab
	// case (0x50c8a0): field_34 is the pixel step per visible line (drag speed),
	// field_38 is the accumulated mouse-Y delta since the last line change.
	// confirmed float (not int) by the fld/fadd/fcomp/fstp float ops on this
	// memory in the original disassembly.
	f32 field_34;
	f32 field_38;

	SEntry mEntry[40];
};

void validate_SEntry(void);
void validate_CMenu(void);

EXPORT void Front_GaugeOff(void);
EXPORT void Front_ClearScreen(void);
EXPORT void Front_Display(void);
EXPORT SLevel* Front_FindLevel(char *);
EXPORT void Front_GetButtons(i32 *,i32 *,i32 *,i32 *);
EXPORT i32 Front_GetLevelIndex(char *);
EXPORT void Front_Init(void);
EXPORT void Front_LoadGame(SSaveGame *,i32,bool);
EXPORT void Front_MiniUpdate(void);
EXPORT void Front_RGBRed(void);
EXPORT void Front_SaveGameState(void);
EXPORT void Front_Update(void);
EXPORT void PrintPaused(void);

EXPORT extern CMenu* pYesNoMenu;
EXPORT extern CMenu* gPausedMenu;
const i32 FRONT_NUM_LEVELS = 41;
EXPORT extern SLevel Levels[FRONT_NUM_LEVELS];

void validate_SLevel(void);

#endif
