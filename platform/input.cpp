#include "plat.h"
#include "../spidey.h"
#include "../PCInput.h"
#include "../front.h"
#include "../ps2gamefmv.h"

// @Bogus
void Plat_InputMapCutsceneSkip(u8 dikState[256])
{
	CPlayer* player = G_MECHLIST_PLAYER;
	// CPlayer::AI (0x4C65C0) uses Circle/X for normal scene skips.
	// The force-exit path at 0x4C6662 instead uses Start.
	static const i32* const forceExit = (i32*)0x0068293C;
	if (!dikState[1] || !player || !player->field_1AC ||
		!player->field_1A4 || gPausedMenu || G_GAME_FMV_ACTIVE || *forceExit)
		return;
	u32 key;
	PCINPUT_GetKeyboardMappingForAction(0x20, &key);
	if (key < 256 && key != 1)
	{
		dikState[1] = 0;
		dikState[key] = 0x80;
	}
}
