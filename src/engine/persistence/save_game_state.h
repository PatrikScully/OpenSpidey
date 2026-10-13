#pragma once

#include "core/types.h"

// Original 0xBC-byte save-file record. Keep its layout and recovered type
// name until persistence, menu and compatibility callers migrate together.
struct SSaveGame
{
	u32 mChecksum;

	// Sized from Front_LoadGame (front.cpp): the highest byte written/read
	// there is mLevelName[7] (offsets 0x68285D through 0x682863 relative to
	// gSaveGame at 0x682858), still inside the old padding block before
	// mRestartPointName at 0xD, so this does not move any other field.
	char mLevelName[9];

	char mRestartPointName[50];

	// @FIXME: figure proper size
	char field_3F[1];

	PADDING(0x54-0x3F-1);

	i8 mDifficulty;

	// ActivateCheat CHEAT_ROBRTSON ("Storyboard Viewer") toggle
	u8 mCheatStoryboardFlag;

	// PShell_MaybeUnlockStuff loops over 0x22 (34) of these checking completion
	u8 mLevelCompletionCounts[0x22];

	// ActivateCheat CHEAT_LEANEST ("everything") sets this to 1
	u8 field_78;

	PADDING(0x7B-0x79);

	u8 field_7B;

	// PShell_MaybeUnlockStuff stores its "which unlock popup" result here
	u8 field_7C;
	PADDING(0x80-0x7C-1);

	// ActivateCheat costume unlock bitmask (SECRTWAR/MIGUELOH/TRISNTNL/
	// SYNOPTIC/XILRTRNS/KICKME/MRWATSON/SMLVIII/CLUBNOIR cheats)
	i32 field_80;

	// ActivateCheat CHEAT_RGSGLLRY ("Character Viewer") = -1
	i32 field_84;

	// movie viewer unlock bitmask; also used directly as gSaveGame.field_88
	// in ps2gamefmv.cpp ("|= 1 << a1"). ActivateCheat CHEAT_CINEMA sets -1
	i32 field_88;

	// ActivateCheat CHEAT_FANBOY / CHEAT_LEANEST; same address as
	// powerup.cpp's gCheatUnlockFlags standalone global
	i32 field_8C;

	// ActivateCheat CHEAT_KIRBYFAN ("Game Comic Covers") = 0x3F
	i32 field_90;

	// PShell_ApplyGameState reads these and writes them into gGameState[11..13]
	i32 field_94;
	i32 field_98;
	i32 field_9C;

	// PShell_ApplyGameState writes gBootRomSoundMode here
	u8 field_A0;
	PADDING(0xA4-0xA0-1);

	// PShell_ApplyGameState copies this into DoubleBuffer[0/1].Disp.screen.x
	i16 field_A4;
	PADDING(0xA8-0xA4-2);

	// PShell_ApplyGameState copies this into DoubleBuffer[0/1].Disp.screen.y
	i16 field_A8;
	PADDING(0xAC-0xA8-2);

	// applied via Pad_SetDigitalMapping through gGameState[0..3]
	i16 mDigitalMapping[4];

	// applied via Pad_SetAnalogueMapping through gGameState[4..7]
	i16 mAnalogueMapping[4];
};
