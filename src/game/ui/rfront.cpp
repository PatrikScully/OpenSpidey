#include "game/ui/rfront.h"

#include "game/ui/front.h"
#include "game/application/main.h"
#include "engine/rendering/render_buffers.h"
#include "game/ui/mess.h"
#include "game/ui/panel.h"
#include "engine/rendering/ps2funcs.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/input/ps2pad.h"
#include "engine/rendering/PCGfx.h"
#include "game/ui/PCShell.h"
#include "core/utils.h"

// This translation unit holds exactly one function, the same way music.cpp
// does. The Mac symbol table proves it: Front_ContinueExit sits on its own
// between __sinit_reloc_g_cpp and __sinit_rfront_cpp, and
// tools/prototypes.json lists it under "rfront" (792 bytes there, 768 here).
// The PC address, 0x47D830, is past reloc.cpp's functions, which is why
// tools/names.json makes it look like it belongs to reloc.cpp.

// text pointers filled in by the localised string table. 0x54B748 is the same
// address front.cpp calls gFrontHintText; in the shipped English data it reads
// "game over" at this point. Tentative names, our own guesses, taken from the
// strings the pointers hold.
#define gContinueGameOverText (*reinterpret_cast<char**>(0x0054B748))
#define gContinueRetryText (*reinterpret_cast<char**>(0x0054BA94))
#define gContinueQuitText (*reinterpret_cast<char**>(0x0054BAA0))

// same two addresses (and names) screen.cpp uses. Cleared while the menu owns
// the screen and set again on the way out.
#define gDbBusyFlagOne (*reinterpret_cast<u8*>(0x0056FB78))
#define gDbBusyFlagTwo (*reinterpret_cast<u8*>(0x0056FBF4))

// @Ok
// 0x0047D830, 768 bytes. The "game over - retry or quit" prompt. SpideyMain
// runs it for end codes 2 and 9 (the player died or asked to leave) and takes
// a 1 back as "reload the level", a 0 as "give up and go back to the shell".
// It drives its own frame loop, so nothing else in the game runs while it is
// up.
i32 Front_ContinueExit(void)
{
	Pad_Update();
	Pad_ClearTriggers(G_SCONTROL);
	Mess_SetScale(256);

	i32 result = 0;

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: MoveImage");

	gDbBusyFlagOne = 0;
	gDbBusyFlagTwo = 0;

	CMenu *pMenu = new CMenu(256, 112, 0, 256, 256, 16);

	pMenu->AddEntry(gContinueRetryText);
	pMenu->AddEntry(gContinueQuitText);

	pMenu->scrollbar_zero = 0;
	pMenu->field_1E = 1;

	i32 frame = 0;

	for (;;)
	{
		gsub_430880();

		Db_FlipClear();
		CalcPolyBufferEnd();

		u32 frameStart = G_VBLANKS;

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1, -1);

		pMenu->Display();

		Mess_SetRGB(127, 25, 33, 0);
		Mess_DrawText(256, 64, gContinueGameOverText, 0, 0x1000);

		if (frame > 2)
		{
			// dead debug draw at 460,173: nullsub_3 takes the two coordinates
			// and does nothing in the shipped build.
			((void(*)(i32, i32))gsub_430880)(460, 173);
		}

		frame++;

		PCSHELL_DrawMouseCursor();

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		Pad_Update();

		i32 activated;
		i32 goBack;
		i32 anyButton;
		i32 start;
		Front_GetButtons(&activated, &goBack, &anyButton, &start);

		pMenu->Update();

		u8 clicked = 0;

		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			const char *pText = pMenu->mEntry[pMenu->mLine].name;
			u8 scale = pMenu->field_16;

			i32 textX;
			i32 textY;
			pMenu->GetEntryXY(pText, &textX, &textY);

			if (PCSHELL_IsMouseOverText(pText, textX, textY, scale))
				clicked = 1;
		}

		if (activated || clicked)
			break;

		if (G_VBLANKS == frameStart)
			Pause(1);

		DoVblankProcessing = 0;

		Pause(1);

		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");

		gsub_430680();

		if (!DoVblankProcessing)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
	}

	SFX_Play(21, 0x2000, 0);

	if (pMenu->ChoiceIs(gContinueRetryText))
		result = 1;

	Pause(1);

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");

	gsub_430680();

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");

	delete pMenu;

	gDbBusyFlagOne = 1;
	gDbBusyFlagTwo = 1;

	Db_FlipClear();

	G_GAME_FADE = 80;

	while (G_GAME_FADE)
		;

	return result;
}
