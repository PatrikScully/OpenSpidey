#include <new>
#include "game/ui/front.h"
#include "engine/rendering/scene_renderer.h"
#include "engine/rendering/render_buffers.h"
#include "compatibility/validate.h"
#include "core/utils.h"
#include "game/ui/mess.h"
#include "engine/rendering/ps2funcs.h"
#include "engine/world/trig.h"
#include "engine/input/ps2pad.h"
#include "game/objects/powerup.h"
#include "engine/persistence/dcmemcard.h"
#include "game/ui/PCShell.h"
#include "engine/audio/ps2lowsfx.h"
#include "game/ui/panel.h"
#include "game/player/spidey.h"
#include "game/cinematics/camera.h"
#include "game/ui/screen.h"
#include "engine/audio/ps2redbook.h"
#include "game/ui/pshell.h"
#include "game/effects/flash.h"
#include "engine/rendering/post.h"
#include "engine/persistence/ps2card.h"

#ifndef SPIDEY_STANDALONE
CMenu* pYesNoMenu;
#else
extern CMenu* pYesNoMenu;
#endif

// idb_globals.txt: 0x005FAED0 gPausedMenu.
#ifndef SPIDEY_STANDALONE
CMenu* gPausedMenu;
#else
extern CMenu* gPausedMenu;
#endif

EXPORT i32 gFrontGauge;

#ifndef SPIDEY_STANDALONE
SSaveGame gSaveGame;
#else
extern SSaveGame gSaveGame;
#endif

// @FIXME add content
#ifndef SPIDEY_STANDALONE
SLevel Levels[FRONT_NUM_LEVELS];
#else
extern SLevel Levels[FRONT_NUM_LEVELS];
#endif

// Tentative names, no idb_globals.txt entries. CMenu::Display spawns a
// small effect record (looks like a highlight/arrow indicator, ~0x28 bytes,
// many i16 position fields) out of a bump-allocated buffer when the
// selection moves to a new entry, guarded by these two globals: current
// write position and one-past-the-end of the buffer. Field layout of the
// record itself is not understood (no consumer of this buffer has been
// decompiled yet), so CMenu::Display below pokes it by raw byte offset
// instead of a named struct.

// idb_globals.txt: 0x0054D341 gPrintStubbed. Guards debug stubbed_printf
// calls in both CMenu::Display and Front_LoadGame below (not menu-specific,
// despite the guess made before the idb name turned up).
#define gPrintStubbed (*reinterpret_cast<u8*>(0x0054D341))

// @Ok
// @Matching
 void CMenu::KillBox(void)
{
	delete this->ptr_to;
	this->ptr_to = 0;
}

// @Ok
// Functional decompile, verified logic field-by-field against the IDA
// decompile and disassembly at 0x4401b0 (2026-08-31): the entry loop bounds
// (mCursorLine/mNumLines/field_1B), the SEntry stride (0x20, matches
// gouraud color fields), the fixed-point blend (weight from Sine(),
// (a+b)>>1 + ((weight*(a-b))>>13), *350/256 clamp to 255) and the
// highlight-record bump allocator (G_PPOLY/G_POLY_BUFFER_END, 0x28-byte
// record) all match the original. cmpsum still shows 288 mnemonic diffs,
// but built length matches the original exactly (1091 bytes, 358 decoded
// instructions), so this is register allocation / statement scheduling
// residue, not a logic gap. Per this session's functional-decomp bar, not
// pursuing further; byte-match attempt log in front.attempts.md.
// The "just selected this entry" effect writes ~15 fields into a
// bump-allocated ~0x28-byte record (G_PPOLY/G_POLY_BUFFER_END) whose struct
// is completely undocumented - no consumer of that buffer has been
// decompiled yet, so the fields below are raw offset pokes with guessed
// meanings (position pairs for what is probably a highlight arrow/box),
// not a named struct member list.
void CMenu::Display(void)
{
	// the box keeps expanding (0x4405CF calls CExpandingBox::Display) even
	// while the entries are not drawn yet; only the entry drawing is skipped
	if (!this->ptr_to || this->ptr_to->field_30 != 0)
	{
		Mess_SetTextJustify(this->mJustification);

		i32 y = this->mY;

		for (i32 i = this->mCursorLine;
				i < this->mNumLines && i < (this->mCursorLine + this->field_1B);
				i++)
		{
			y += this->mEntry[i].unk_a;

			if (!this->mEntry[i].unk_b)
				continue;

			if (this->mEntry[i].val_a <= 0)
			{
				y += this->mLineSep;
				continue;
			}

			if (i == this->mLine)
			{
				if (this->field_1E & 0xFF)
				{
					i32 weight = Sine(this->field_20);
					this->field_20 += 200;

					i32 r = ((this->mEntry[i].unk_c + this->mEntry[i].field_11) >> 1)
						+ (((this->mEntry[i].unk_c - this->mEntry[i].field_11) * weight) >> 13);
					i32 g = ((this->mEntry[i].unk_d + this->mEntry[i].field_12) >> 1)
						+ (((this->mEntry[i].unk_d - this->mEntry[i].field_12) * weight) >> 13);
					i32 b = ((this->mEntry[i].unk_e + this->mEntry[i].field_13) >> 1)
						+ (((this->mEntry[i].unk_e - this->mEntry[i].field_13) * weight) >> 13);

					r = r * 350 / 256;
					g = g * 350 / 256;
					b = b * 350 / 256;
					if (r > 255) r = 255;
					if (g > 255) g = 255;
					if (b > 255) b = 255;

					Mess_SetRGB(static_cast<u8>(r), static_cast<u8>(g), static_cast<u8>(b), 0);

					i32 sr = ((this->mEntry[i].field_14 + this->mEntry[i].field_17) >> 1)
						+ (((this->mEntry[i].field_14 - this->mEntry[i].field_17) * weight) >> 13);
					i32 sg = ((this->mEntry[i].field_15 + this->mEntry[i].field_18) >> 1)
						+ (((this->mEntry[i].field_15 - this->mEntry[i].field_18) * weight) >> 13);
					i32 sb = ((this->mEntry[i].field_16 + this->mEntry[i].field_19) >> 1)
						+ (((this->mEntry[i].field_16 - this->mEntry[i].field_19) * weight) >> 13);

					sr = sr * 350 / 256;
					sg = sg * 350 / 256;
					sb = sb * 350 / 256;
					if (sr > 255) sr = 255;
					if (sg > 255) sg = 255;
					if (sb > 255) sb = 255;

					Mess_SetRGBBottom(static_cast<u8>(sr), static_cast<u8>(sg), static_cast<u8>(sb));
				}
				else
				{
					Mess_SetRGB(this->mEntry[i].unk_c, this->mEntry[i].unk_d, this->mEntry[i].unk_e, 0);
					Mess_SetRGBBottom(this->mEntry[i].field_14, this->mEntry[i].field_15, this->mEntry[i].field_16);
				}
			}
			else
			{
				Mess_SetRGB(this->mEntry[i].field_11, this->mEntry[i].field_12, this->mEntry[i].field_13, 0);
				Mess_SetRGBBottom(this->mEntry[i].field_17, this->mEntry[i].field_18, this->mEntry[i].field_19);
			}

			Mess_TextWidth(this->mEntry[i].name);
			i32 drawResult = Mess_DrawText(this->mX, y, this->mEntry[i].name, 0, 0x1000);

			if (this->field_16 && i == this->field_17 && this->mJustification == 0)
			{
				i32 highlightOffset = (this->mEntry[i].val_a * drawResult) / 512
					+ (this->mEntry[i].val_a * 14) / 256;

				u8* rec = reinterpret_cast<u8*>(G_PPOLY);
				u8* next = rec + 0x28;

				if (next <= G_POLY_BUFFER_END)
				{
					G_PPOLY = reinterpret_cast<u32*>(next);

					if (!gPrintStubbed)
						stubbed_printf(reinterpret_cast<char*>(0x0054ABF0));
					if (!gPrintStubbed)
						stubbed_printf(reinterpret_cast<char*>(0x0054ABF0));

					i16 yMinus5 = static_cast<i16>(y - 5);
					i16 xBase = static_cast<i16>(this->mX - highlightOffset);
					i16 xBaseMinus20 = static_cast<i16>(xBase - 20);
					i16 yMinus11 = static_cast<i16>(yMinus5 - 6);
					i16 yPlus1 = static_cast<i16>(yMinus5 + 6);
					i16 highlightOffset2 = static_cast<i16>(highlightOffset + this->mX);
					i16 highlightOffset2Plus20 = static_cast<i16>(highlightOffset2 + 20);

					*reinterpret_cast<u8*>(rec + 4) = 0x96;
					*reinterpret_cast<u8*>(rec + 5) = 0;
					*reinterpret_cast<u8*>(rec + 6) = 0;
					*reinterpret_cast<i16*>(rec + 0xA) = yMinus5;
					*reinterpret_cast<i16*>(rec + 8) = xBase;
					*reinterpret_cast<i16*>(rec + 0xC) = xBaseMinus20;
					*reinterpret_cast<i16*>(rec + 0x10) = xBaseMinus20;
					*reinterpret_cast<i16*>(rec + 0xE) = yMinus11;
					*reinterpret_cast<i16*>(rec + 0x12) = yPlus1;
					*reinterpret_cast<u8*>(rec + 0x18) = 0x96;
					*reinterpret_cast<u8*>(rec + 0x19) = 0;
					*reinterpret_cast<u8*>(rec + 0x1A) = 0;
					*reinterpret_cast<i16*>(rec + 0x1E) = yMinus5;
					*reinterpret_cast<i16*>(rec + 0x1C) = highlightOffset2;
					*reinterpret_cast<i16*>(rec + 0x22) = yMinus11;
					*reinterpret_cast<i16*>(rec + 0x20) = highlightOffset2Plus20;
					*reinterpret_cast<i16*>(rec + 0x24) = highlightOffset2Plus20;
					*reinterpret_cast<i16*>(rec + 0x26) = yPlus1;

					stubbed_printf(G_RENDER_BUF);
					stubbed_printf(G_RENDER_BUF);
				}
			}

			y += this->mLineSep;
		}
	}

	if (this->ptr_to)
	{
		this->ptr_to->Display();

		if (this->mZoomBoxType == 2)
			this->ptr_to->field_24 = 1;
	}
}

// @Ok
// @Matching
void CMenu::Zoom(i32 zoomboxType)
{
	this->mZoomBoxType = zoomboxType;
	this->KillBox();

	switch (zoomboxType)
	{
		case 0:
			this->ptr_to = new CExpandingBox(
				0,
				this->mY - 18,
				512,
				this->GetMenuHeight() + 27,
				0,
				0,
				1000,
				15,
				0);
			break;
		case 1:
		case 2:
			i32 v16;
			i32 v17;
			if (Utils_CompareStrings(Mess_GetCurrentFont(), "sp_fnt03.fnt"))
			{
				v17 = 10;
				v16 = 14;
			}
			else
			{
				v17 = 12;
				v16 = 17;
			}

			this->ptr_to = new CExpandingBox(
				this->mX - 5,
				this->mY - v17,
				this->menu_width + 12,
				v16 + this->GetMenuHeight(),
				0,
				0,
				30,
				15,
				0);

			break;
		default:
			print_if_false(0, "Bad zoombox type");
			break;
	}
}

// @Ok
// @Matching
void CMenu::AddEntry(const char* pString)
{
	print_if_false(this->mNumLines < 40, "Too many entries added to menu");
	this->mEntry[this->mNumLines].name = pString;
	this->mEntry[this->mNumLines].unk_b = 1;
	this->mEntry[this->mNumLines].unk_a = 0;

	i32 width = Mess_TextWidth(this->mEntry[this->mNumLines].name);
	if (width > this->menu_width)
		this->menu_width = width + this->width_val_a;

	this->mNumLines++;
	this->field_32++;
}

// @Ok
// @Matching
void Front_ClearScreen(void)
{
	ClearImage();
	ClearImage();
}

// Tentative names, no name in idb_globals.txt for these three. Shared with
// CheckForPadUnplugged and Front_Display, which both load pointers from the
// same fixed addresses to draw the same blinking text. Not yet implemented
// anywhere else in the repo, so kept file-local for now.
#define gFrontPadTextOne (*reinterpret_cast<char**>(0x0054B764))
#define gFrontPadTextTwo (*reinterpret_cast<char**>(0x0054B768))
#define gFrontPadTextThree (*reinterpret_cast<char**>(0x0054BBC8))

// Tentative names/globals for Front_Display, no idb_globals.txt entries
// unless noted. gFrontDrawPolyFlag/gFrontShowTrainingTip/gFrontMysteryFlag
// are booleans gating one-off draw blocks; gFrontHintY is a persistent
// animated y-coordinate (this file's only writer); gFrontScreenState is the
// front-end screen-type switch selector; gFrontControllerTwoMenu is a
// second CMenu* right after the already-named gPausedMenu (0x5FAED0, from
// idb_globals.txt); the seven 0x54Bxxx/0x54BBC8 entries are char* text
// pointers, same pattern as gFrontYesText/gFrontPadTextOne above.
#define gFrontDrawPolyFlag (*reinterpret_cast<i32*>(0x0060CFE0))
#define gFrontShowTrainingTip (*reinterpret_cast<i32*>(0x0068293C))
#define gFrontHintY (*reinterpret_cast<i16*>(0x0054A7E0))
#define gFrontScreenState (*reinterpret_cast<i32*>(0x005FAECC))
#define gFrontControllerTwoMenu (*reinterpret_cast<CMenu**>(0x005FAED4))
#define gFrontTrainingTextOne (*reinterpret_cast<char**>(0x0054B76C))
#define gFrontTrainingTextTwo (*reinterpret_cast<char**>(0x0054B890))
#define gFrontPausedText (*reinterpret_cast<char**>(0x0054B74C))
#define gFrontHintText (*reinterpret_cast<char**>(0x0054B748))
#define gFrontYesNoPromptText (*reinterpret_cast<char**>(0x0054B784))
// Same address as gsub_430880 (nullsub_3), see PCShell.cpp/shell.cpp for
// precedent on calling it with a dummy arg via a function-pointer cast.
#define gFrontMysteryValueOne (*reinterpret_cast<i32*>(0x005512EC))
// idb_globals.txt: 0x005FB1B0 gInitRelatedTwo (already a plain, non-fixed
// repo global in init.cpp; only read here, so a file-local fixed-address
// macro is enough, no need to touch init.cpp/init.h for a single caller).
#define G_INIT_RELATED_TWO (*reinterpret_cast<i32*>(0x005FB1B0))

// Forward-declared instead of #include "game/player/spidey.h": Front_Display below
// only uses the pointer VALUE (cast to u8* for a raw offset read), so the
// full CPlayer definition is not needed and pulling in spidey.h's huge
// dependency graph is not worth it for one field read.
class CPlayer;

// @Ok
// Functional decompile, cmpsum residue down to 2 mnemonic diffs (from 145
// on the first honest pass; see front.attempts.md). Remaining diff is a
// tail-merge: the original keeps two separate `ret` instructions at the
// end of the two TTime-blink paths (different `add esp` cleanup, 0x28 vs
// 0x14), while this build's optimizer merges them into one shared `ret`
// regardless of source shape - a compiler-side scheduling decision, not a
// logic gap (instruction counts otherwise match, 220 vs 219). Per this
// session's functional-decomp bar, accepted as-is. The switch's jump table
// bytes are not in tools/functions/<addr>.bin (extraction stops at the
// last real instruction), so the case body order (screen states 1,2,3,4 in
// address order 0x440C40/0x440CB6/0x440CC9/0x440D51) is inferred from
// normal compiler layout, not read directly. `MechList->field_E2` is
// accessed by raw pointer offset instead of adding a new field to CPlayer
// in spidey.h - that struct is huge and shared with many other already-
// `@Ok` files, not safe to touch from a front.cpp-only session (same
// caution as the SLevel/init.cpp note on Front_LoadGame below).
void Front_Display(void)
{
	// same address as gsub_430880 (nullsub_3), declared and defined in
	// PCShell.cpp; see shell.cpp for the same local-extern precedent.
	extern void gsub_430880(void);

	if (gFrontDrawPolyFlag)
		Panel_DrawFlatShadedPoly(0x13, 0xBA, 0xC2, 0x2A, 0, 0, 0, 0, 0);

	if (G_POST_WATER_EFFECT)
		gsub_430880();

	Mess_SetTextJustify(0);

	if (gFrontShowTrainingTip && (G_VBLANKS & 0x20))
	{
		Mess_SetRGB(0x80, 0x80, 0x80, 0);
		Mess_SetScale(0x100);
		Mess_DrawText(0x100, 0x32, gFrontTrainingTextOne, 0, 0x1000);
		Mess_DrawText(0x100, 0xC8, gFrontTrainingTextTwo, 0, 0x1000);
	}

	if (G_MECHLIST_PLAYER
			&& *reinterpret_cast<i16*>(reinterpret_cast<u8*>(G_MECHLIST_PLAYER) + 0xE2) <= 0
			&& G_INIT_RELATED_TWO == 1)
	{
		Mess_SetRGB(0x80, 0x80, 0x80, 0);
		Mess_SetScale(0x180);
		Mess_DrawText(0x100, gFrontHintY, gFrontHintText, 0, 0x1000);

		gFrontHintY += 8;
		if (gFrontHintY > 0x78)
			gFrontHintY = 0x78;
	}

	Mess_SetScale(0x100);

	i32 screenState = gFrontScreenState;

	switch (screenState)
	{
		case 1:
			Mess_SetScale(0x100);
			Mess_SetRGB(0x7F, 0x19, 0x21, 0);
			Mess_DrawText(0x100, 0x32, gFrontPausedText, 0, 0x1000);
			((void(*)(i32,i32))gsub_430880)(gFrontMysteryValueOne, 0xAD);
			print_if_false(gPausedMenu != 0, reinterpret_cast<char*>(0x0054AC34));
			gPausedMenu->mX = 0x100;
			gPausedMenu->Display();
			PCSHELL_DrawMouseCursor();
			return;

		case 2:
			if (!gFrontControllerTwoMenu)
				return;
			gFrontControllerTwoMenu->Display();
			return;

		case 3:
			Mess_SetScale(0x100);
			Mess_SetRGB(0x7F, 0x19, 0x21, 0);
			Mess_DrawText(0x100, 0x32, gFrontPausedText, 0, 0x1000);
			((void(*)(i32,i32))gsub_430880)(gFrontMysteryValueOne, 0xAD);
			Mess_SetRGB(0x4D, 0x53, 0x69, 0);
			Mess_SetScale(0x100);
			Mess_DrawText(0x100, 0x4B, gFrontYesNoPromptText, 0, 0x1000);
			pYesNoMenu->Display();
			PCSHELL_DrawMouseCursor();
			return;

		case 4:
			Mess_SetScale(0x100);
			Mess_SetRGB(0x7F, 0x19, 0x21, 0);
			Mess_DrawText(0x100, 0x32, gFrontPausedText, 0, 0x1000);
			Front_RGBRed();
			break;

		default:
			return;
	}

	if (G_SCONTROL[0].Type == 0)
	{
		if ((TTime / 10) & 1)
		{
			Mess_DrawText(0x100, 0xB0, gFrontPadTextOne, 0, 0x1000);
			Mess_DrawText(0x100, 0xC0, gFrontPadTextTwo, 0, 0x1000);
		}

		return;
	}

	if ((TTime / 10) & 1)
		Mess_DrawText(0x100, 0xB8, gFrontPadTextThree, 0, 0x1000);
}

// @Ok
// it seems the game was using older obj file
// code looks debug
 SLevel* Front_FindLevel(char* pTRGName)
{
	for (i32 i = 0;
			i < FRONT_NUM_LEVELS;
			i++)
	{
		char *pName = Levels[i].mName;
		if (!pName[0])
			break;

		if (pName[1] == pTRGName[1] && pName[3] == pTRGName[3])
			return &Levels[i];
	}

	return 0;
}

// selects between two Start-button trigger bitmasks (0x40000 / 0x40040).
// Tentative name, our own guess. Read by Front_GetButtons below and by
// Front_Update further down, which is why it sits up here rather than with
// the other Front_Update globals.
#define gFrontUseAltTriggerMask (*reinterpret_cast<u8*>(0x005FAE9D))

// @Ok
// 0x00440E40, 167 bytes. FOUND 2026-09-01: this function DOES exist in the PC
// build, the earlier @Bogus verdict above it was wrong. It is names.json's
// unnamed sub_440E40, sitting right before Front_Update (0x440EF0) inside
// front.cpp's own address range. The earlier audit listed the range and saw
// every byte claimed by a named function or a sub_XXXXXX, and stopped there
// without reading the sub_XXXXXX bodies.
//
// What identifies it: the parameter shape is exactly the THPS2 one
// (thps2-stuff/decls.h, Front_GetButtons__FRiN30(int *Activated, int *GoBack,
// int *AnyButton, int *Start)), it fills four int out-parameters in that
// order, it derives AnyButton as Activated|GoBack, and it reads
// gFrontUseAltTriggerMask (0x5FAE9D) to pick the Start mask. The PC port did
// not drop it, it rewrote the body on top of PCSHELL_CheckTriggers, which is
// why the Mac version is 240 bytes and this one is 167.
//
// Trigger masks: 0x40000 (or 0x40040 with the alternate mask) is Start,
// 0x10010 is the accept button, 0x20220 is the back button.
void Front_GetButtons(i32 *pActivated, i32 *pGoBack, i32 *pAnyButton, i32 *pStart)
{
	i32 startMask = 0x40000;

	*pStart = 0;
	*pAnyButton = 0;
	*pGoBack = 0;
	*pActivated = 0;

	if (gFrontUseAltTriggerMask)
		startMask = 0x40040;

	*pStart = PCSHELL_CheckTriggers(startMask, 1, 1);

	*pActivated = (PCSHELL_CheckTriggers(0x10010, 1, 1) || *pStart) ? 1 : 0;

	*pGoBack = PCSHELL_CheckTriggers(0x20220, 1, 1);
	*pAnyButton = *pActivated | *pGoBack;

	// pressing accept wins over back when both come up in the same frame
	if (*pActivated)
		*pGoBack = 0;
}

// @Ok
i32 Front_GetLevelIndex(char* pTRGName)
{
	SLevel* pLevel = Front_FindLevel(pTRGName);
	if (!pLevel)
		return -1;

	i32 index =
		(reinterpret_cast<i32>(pLevel) - reinterpret_cast<i32>(&Levels[0]))/sizeof(SLevel);

	print_if_false(index >= 0 && index < 34, "Bad LevelIndex found by Front_GetLevelIndex");
	return index;
}

// Tentative names, no idb_globals.txt entries. gFrontCardExists holds the
// DCCard_Exists(0) result. gDefaultSaveGame is a template SSaveGame the game
// copies into gSaveGame at boot (size matches SSaveGame, 0xBC bytes).
// gFrontYesText/gFrontNoText are the two CMenu entries added to pYesNoMenu,
// guessed from the variable name (a yes/no confirmation menu).
static u8* const gFrontCardExists = (u8*)0x005FAD98;
#define G_DEFAULT_SAVE_GAME (*reinterpret_cast<SSaveGame*>(0x00550E10))
#define gFrontYesText (*reinterpret_cast<char**>(0x0054B780))
#define gFrontNoText (*reinterpret_cast<char**>(0x0054B77C))

// Tentative name, no idb_globals.txt entry (sits in the gap between named
// gPostWaterEffect 0x5FAE98 and pYesNoMenu 0x5FAEAC). Used by CMenu::Update
// as a held-direction repeat counter: reset to 0 when the mouse/trigger
// state resets, incremented once per frame the cursor does not move,
// checked against CMenu::scrollbar_one for the auto-repeat rate.
#define gMenuNavRepeatTimer (*reinterpret_cast<i32*>(0x005FAEA0))



// @Ok
// @Matching
void Front_Init(void)
{
	*gFrontCardExists = DCCard_Exists(0);
	G_SAVE_GAME = G_DEFAULT_SAVE_GAME;

	PShell_ApplyGameState();

	print_if_false(pYesNoMenu == 0, "pYesNoMenu already created");

	void* pMenuMem = CMenu::operator new(sizeof(CMenu));
	if (pMenuMem)
		pYesNoMenu = ::new (pMenuMem) CMenu(0x100, 0, 0, 0x100, 0x100, 0x10);
	else
		pYesNoMenu = 0;

	pYesNoMenu->scrollbar_zero = 1;
	// Original writes only the low byte of field_1E (mov [ecx+1Eh],bl in the
	// original bytes), not a full i16 store. Reproduce with a byte poke
	// instead of guessing field_1E is really two u8s, since nothing else in
	// the repo touches this field yet.
	*reinterpret_cast<u8*>(&pYesNoMenu->field_1E) = 1;

	pYesNoMenu->AddEntry(gFrontYesText);
	pYesNoMenu->AddEntry(gFrontNoText);

	pYesNoMenu->mY = 0x74;
}

// Tentative names/globals for Front_LoadGame, no idb_globals.txt entries
// unless noted. gFrontSlotCounter/gFrontSlotShuffleTable implement a
// non-repeating random digit picker (Utils_Jumble reshuffles the table of 9
// once every 9 draws); gFrontCameraModeFlagOne/Two are two byte flags set
// from the pre-call RestartNode value, meaning unclear without a consumer.
#define gFrontSlotCounter (*reinterpret_cast<i32*>(0x00682944))
static i32 gFrontSlotShuffleTable[9] = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
#define gFrontCameraModeFlagOne (*reinterpret_cast<u8*>(0x0056FB78))
#define gFrontCameraModeFlagTwo (*reinterpret_cast<u8*>(0x0056FBF4))

// @Ok
// Functional decompile, verified field-by-field against ground-truth
// disassembly at 0x441f90 (2026-08-31): confirmed the raw pointer math
// resolves to `&Levels[0]` at 0x54A518 (off_54A51C, IDA's confusing name
// for `&Levels[0].mName`, is base+4; the loop advances by 0x14 =
// sizeof(SLevel) per entry) and that `gSaveGame.field_84 |= Levels[i].field_10;
// gSaveGame.field_90 |= Levels[i].field_C;` matches the original's
// `or dword_6828DC(=gSaveGame+0x84), [eax+0x10]` /
// `or dword_6828E8(=gSaveGame+0x90), [eax+0xC]` exactly (gSaveGame's
// original fixed base is 0x682858). cmpsum residue: 156 mnemonic diffs
// (instruction counts close, 206 original vs 208 built). This function
// allocates and constructs a CPlayer and a CCamera with plain `new`
// expressions, which is exactly why the original has an SEH frame
// (exception-safe cleanup if a constructor throws, same reason
// CMenu::Zoom's `new CExpandingBox(...)` has one, see that comment).
// Matching MSVC6's SEH scope-table generation by hand is out of scope for
// functional decomp; per this session's bar, accepted as-is.
// SLevel's field_C/field_10 are read here as raw 32-bit offset pokes
// (`pLevel + 0xC` / `pLevel + 0x10`) instead of through named struct
// fields, since `Levels[35].field_C = 0xFFEC;` in init.cpp sits inside an
// already-`@Ok` function in a file this session does not own, and widening
// SLevel::field_C would change that store's instruction encoding - not
// safe to risk from here.
void Front_LoadGame(SSaveGame *pSave, i32 a2, bool /* a3, unused */)
{
	// same address as gsub_430880 (nullsub_3), declared and defined in
	// PCShell.cpp; see shell.cpp for the same local-extern precedent.
	extern void gsub_430880(void);

	i32 savedRestartNode = RestartNode;

	if (G_SAVE_GAME.mLevelName[1] == 'f' && G_SAVE_GAME.mLevelName[3] == '1')
	{
		print_if_false(gFrontSlotCounter < 9, reinterpret_cast<char*>(0x0054ACCC));

		G_SAVE_GAME.mLevelName[4] = static_cast<char>(gFrontSlotShuffleTable[gFrontSlotCounter] + '0');
		G_SAVE_GAME.mLevelName[5] = '_';
		G_SAVE_GAME.mLevelName[6] = 't';
		G_SAVE_GAME.mLevelName[7] = 0;
		G_SAVE_GAME.mRestartPointName[0] = 0;

		gFrontSlotCounter++;
		if (gFrontSlotCounter >= 9)
		{
			gFrontSlotCounter = 0;
			Utils_Jumble(gFrontSlotShuffleTable, 9);
		}
	}

	SLevel* pLevel = Front_FindLevel(G_SAVE_GAME.mLevelName);

	if (pLevel)
	{
		i32 field_10 = *reinterpret_cast<i32*>(reinterpret_cast<u8*>(pLevel) + 0x10);
		i32 field_C = *reinterpret_cast<i32*>(reinterpret_cast<u8*>(pLevel) + 0xC);

		G_SAVE_GAME.field_84 |= field_10;
		G_SAVE_GAME.field_90 |= field_C;

		if (*reinterpret_cast<u8*>(0x0060CFC5) && Utils_CompareStrings(G_SAVE_GAME.mLevelName, reinterpret_cast<char*>(0x0054A808)))
			G_SAVE_GAME.field_84 |= 0x2000000;
	}

	SFX_SpoolOutLevelSFX();

	char levelId[5];
	levelId[0] = G_SAVE_GAME.mLevelName[0];
	levelId[1] = G_SAVE_GAME.mLevelName[1];
	levelId[2] = G_SAVE_GAME.mLevelName[2];
	levelId[3] = G_SAVE_GAME.mLevelName[3];
	levelId[4] = 0;
	SFX_SpoolInLevelSFX(levelId);

	Spidey_SetUserFunction(0, 0);
	((void(*)(i32))gsub_430880)(2000);
	Trig_LoadTRG(G_SAVE_GAME.mLevelName);

	gFrontGauge = 0;
	((void(*)(i32))gsub_430880)(4);

	CPlayer* player = new CPlayer();

	if (a2)
	{
		RestartNode = savedRestartNode;
	}
	else if (pSave->mRestartPointName[0])
	{
		Trig_SetRestart(pSave->mRestartPointName);
	}

	new CCamera(player);

	if (!gPrintStubbed)
		stubbed_printf(reinterpret_cast<char*>(0x00549620));
	gFrontCameraModeFlagOne = 0;

	if (!gPrintStubbed)
		stubbed_printf(reinterpret_cast<char*>(0x00549620));
	gFrontCameraModeFlagTwo = 0;

	G_REDBOOK_XA_CURRENT_PRIORITY = -1;
	Trig_ExecuteRestart();

	CVector v;
	v.vx = 0;
	v.vy = 0;
	v.vz = 0;

	char *pName = &reinterpret_cast<char*>(Trig_GetPosition(&v, RestartNode))[6];
	char *pDestBuf = &G_SAVE_GAME.mRestartPointName[0];
	i32 i = 0;

	while (*pName)
	{
		i++;
		*pDestBuf++ = *pName++;
		print_if_false((u8)(i < 50), reinterpret_cast<char*>(0x0054ACA4));
	}

	*pDestBuf = 0;

	G_CAMERA_LIST->SetMode(CAMERAMODE_DEMO);
	Screen_StartCircularFadeIn(0x20, 8);
}

// @Ok
// @Matching
void Front_MiniUpdate(void)
{
	i32 type = G_SCONTROL[0].Type;

	if (type != -1)
	{
		if (type != 0)
			return;

		Mess_SetRGB(0xFF, 0, 0, 0);

		if ((TTime / 10) & 1)
		{
			Mess_DrawText(0x100, 0xB0, gFrontPadTextOne, 0, 0x1000);
			Mess_DrawText(0x100, 0xC0, gFrontPadTextTwo, 0, 0x1000);
		}

		return;
	}

	if ((TTime / 10) & 1)
		Mess_DrawText(0x100, 0xB8, gFrontPadTextThree, 0, 0x1000);
}

// @Ok
// @Matching
void Front_RGBRed(void)
{
	Mess_SetRGB(0xFF, 0, 0, 0);
}

// @Ok
void Front_SaveGameState(void)
{
	CVector v4;
	v4.vx = 0;
	v4.vy = 0;
	v4.vz = 0;

	char *pName = &reinterpret_cast<char*>(Trig_GetPosition(&v4, RestartNode))[6];
	char *pDestBuf = &G_SAVE_GAME.mRestartPointName[0];
	i32 i = 0;

	while (*pName)
	{
		i++;
		*pDestBuf++ = *pName++;
		print_if_false((u8)(i < 50), "Too many chars in restart point name");
	}

	*pDestBuf = 0;
}

// New globals for Front_Update, decompiled 2026-08-31 from Hex-Rays at
// 0x440ef0. None of these addresses are in idb_globals.txt; nearest
// neighbours were checked (0x6B4614/0x6B4664 sit in a real gap before
// gTrigNodes 0x6B466C/NumNodes 0x6B4670, so they are not part of those).
// Tentative names, our own guesses:
// - gFrontUpdateBlocked (0x682950, 0x14 bytes after gFrontShowTrainingTip):
//   ANDed into the same top-level guard as gFrontShowTrainingTip, so it
//   gates the whole state machine the same way. Guess: another "something
//   else owns the screen this frame" flag.
// - gFrontUseAltTriggerMask (0x5FAE9D): selects between two trigger
//   bitmasks (0x40000 / 0x40040) for the top-of-frame confirm poll.
// - gFrontCardExistsThisFrame (0x5FAE8C): written every frame from
//   DCCard_Exists(0), never read again in this function - likely read by
//   another (undecompiled) front-end function.
// - gFrontCardPollDelay (0x5FAE20): the memory-card-poll delay counter,
//   matching gFrontCardExists' (0x5FAD98) existing "wait a few frames"
//   pattern. gFrontCardSlotChoice (0x66126C) is its poll result, NOT an
//   independent scratch value: 0x66126C = gSControl (0x661100, ps2pad.h)
//   + 0x16C = SControl::Type. shell.cpp's CheckForPadUnplugged/
//   Shell_RollCredits confirm this offset via VALIDATE(SControl, Type,
//   0x16C) and read it the same way (G_SCONTROL[0].Type). The original
//   game reuses this field as pause-menu scratch state here (writes 0 at
//   0x441013/0x441030, reads/compares == 0 || == -1) despite it also
//   being the pad-hardware type field elsewhere; reproduced faithfully
//   as-is (confirmed via raw disasm, not just a coincidence of address
//   arithmetic). Correcting this session's own audit: the macro's
//   underlying address/access pattern was already correct, only the
//   "independent card-slot value" story above was wrong.
// - gFrontFadeGateOne (0x6611F0) / gFrontFadeGateTwo (0x6611E0): both must
//   be nonzero to arm a ~0x78-vblank timer (gFrontFadeTimerActive/
//   gFrontFadeTimerStart, 0x5FAED8/0x5FAEDC) that force-closes the pause
//   menu with gLevelStatus = 7 once it expires. Guess: some kind of
//   "cutscene/fade taking over" watchdog.
// - gFrontConfirmSeen (0x6611E1): cleared whenever the top-of-frame poll
//   sees a confirm press while gFrontScreenState == 0; not read elsewhere
//   in this function.
// - gFrontSkipToRestartEnabled (0x60CFDC): the "skip to restart" entry's
//   enable precondition, already flagged unnamed by the previous session.
// - gFrontSfxHandle (0x5FAE90): last SFX_Play() return value, not read
//   elsewhere in this function; kept as a real global since it is a fixed
//   original address, in case another undecompiled function reads it.
// - gFrontRestartNodeNames (0x6B4614) / gFrontRestartNodeCount (0x6B4664):
//   a pointer array + count this function iterates to build the "choose a
//   restart point" submenu. Genuinely new Trig-subsystem state, not owned
//   by front.cpp; the per-entry filtering (skip entries whose first 3
//   bytes match gFrontRestartFilterNode, 0x54AC44, itself another runtime
//   pointer with no static string behind it) is a best-effort read of the
//   disassembly, not confirmed against any Trig data. If this case turns
//   out wrong, the Trig side needs a real look, not just this file.
#define gFrontUpdateBlocked (*reinterpret_cast<i32*>(0x00682950))
#define gFrontCardExistsThisFrame (*reinterpret_cast<u8*>(0x005FAE8C))
#define gFrontCardPollDelay (*reinterpret_cast<i32*>(0x005FAE20))
#define gFrontCardSlotChoice (*reinterpret_cast<i32*>(0x0066126C))
#define gFrontFadeGateOne (*reinterpret_cast<u8*>(0x006611F0))
#define gFrontFadeGateTwo (*reinterpret_cast<u8*>(0x006611E0))
#define gFrontConfirmSeen (*reinterpret_cast<u8*>(0x006611E1))
#define gFrontFadeTimerActive (*reinterpret_cast<i32*>(0x005FAED8))
#define gFrontFadeTimerStart (*reinterpret_cast<u32*>(0x005FAEDC))
#define gFrontSfxHandle (*reinterpret_cast<i32*>(0x005FAE90))
#define gFrontSkipToRestartEnabled (*reinterpret_cast<i32*>(0x0060CFDC))
#define gFrontRestartNodeNames (reinterpret_cast<char**>(0x006B4614))
#define gFrontRestartNodeCount (*reinterpret_cast<i32*>(0x006B4664))
#define gFrontRestartFilterNode (*reinterpret_cast<char**>(0x0054AC44))

// The four gPausedMenu entry strings and the pYesNoMenu comparison text,
// same 0x54Bxxx pattern as gFrontYesText/gFrontPadTextOne above. Order
// confirmed against the disassembly's AddEntry sequence: Continue, skip to
// restart, Restart level, Quit.
#define gFrontContinueText (*reinterpret_cast<char**>(0x0054B750))
#define gFrontRestartLevelText (*reinterpret_cast<char**>(0x0054B754))
#define gFrontQuitText (*reinterpret_cast<char**>(0x0054B760))
#define gFrontSkipToRestartText (*reinterpret_cast<char**>(0x0054B774))
// pYesNoMenu's "yes" entry is compared against gFrontNoText (0x54B77C,
// front.cpp above) - yes, that constant really does hold "yes", not "no".
// The names look swapped versus gFrontYesText (0x54B780, which holds
// "no"). Pre-existing repo naming from Front_Init, not touched here.

// Shared by case 1 (gPausedMenu) and case 3 (pYesNoMenu): after Update()
// runs, a separate mouse-click confirm check walks the entries from
// mCursorLine forward - same y-accumulation as CMenu::Display's per-line
// unk_a/mLineSep loop - to find the on-screen Y of the currently selected
// entry (mLine), then hit-tests the mouse there via PCSHELL_IsMouseOverText.
// Inlined at both call sites in the original; pulled into one helper here
// since the functional-decomp bar this session does not require keeping
// the duplication.
// @Ok
static i32 Front_MenuMouseConfirmsSelection(CMenu* pMenu)
{
	if (!PCSHELL_CheckTriggers(0x100, 1, 1))
		return 0;

	const char* pSelectedName = pMenu->mEntry[pMenu->mLine].name;
	i32 y = pMenu->mY;
	i32 i = pMenu->mCursorLine;

	if (i < pMenu->mNumLines)
	{
		while (i < pMenu->mCursorLine + pMenu->field_1B)
		{
			y += pMenu->mEntry[i].unk_a;

			if (pMenu->mEntry[i].unk_b)
			{
				if (Utils_CompareStrings(pSelectedName, pMenu->mEntry[i].name))
					break;

				y += pMenu->mLineSep;
			}

			i++;
			if (i >= pMenu->mNumLines)
				break;
		}
	}

	return PCSHELL_IsMouseOverText(pSelectedName, pMenu->mX, y, pMenu->mJustification) != 0;
}

// @Ok
// Decompiled 2026-08-31 from Hex-Rays at 0x440ef0 (the small
// gFrontShowTrainingTip/gFrontUpdateBlocked guard) and 0x440f00 (the state
// machine itself). This is the in-game pause menu controller: builds and
// drives gPausedMenu (main pause menu), pYesNoMenu (quit confirmation) and
// gFrontControllerTwoMenu (restart-point picker) through gFrontScreenState.
// All CMenu member calls (AddEntry/EntryOn/EntryOff/ChoiceIs/Update/
// KillBox/Zoom/GetMenuHeight/Reset) are inlined in the original at their
// call sites here; mapped back to the real member functions (all already
// @Ok) instead of reproducing the inlined byte pattern, since this
// session's bar is functional correctness, not a byte match. Every
// callee this function needs turned out to already be decompiled and
// @Ok (Pad_ClearTriggers, SFX_Pause/Play/Unpause, Pad_ActuatorOff,
// Post_Do/UndoPausePaletteProcessing, PShell_MoveTowards,
// PCSHELL_CheckTriggers/IsMouseOverText, Utils_CompareStrings,
// Trig_SetRestart, Card_CheckStatus, DCCard_Exists, Flash_Update/
// FadeFinished, Redbook_XAPause) - the previous session's blocker (leaf
// helpers not yet decompiled) turned out to already be resolved by other
// work on this branch.
// No runtime path is available this session (see task brief), so this is
// verified by tracing the Hex-Rays decompile control-flow-by-control-flow
// against the built source, not by an actual playtest. The one genuinely
// weak spot is case 2 (the restart-point submenu): it needs a Trig-owned
// "named restart points" array that does not exist anywhere else in this
// repo yet, so gFrontRestartNodeNames/Count and the per-entry filter are
// best-effort reads of the disassembly, not confirmed. If case 2 turns
// out wrong, everything else in this function does not depend on it.
void Front_Update(void)
{
	if (gFrontShowTrainingTip != 0 || gFrontUpdateBlocked != 0)
		return;

	i32 closeMenu = 0;
	i32 confirmPressed = 0;
	i32 cancelTrigger = 0;
	i32 primaryConfirm;

	if (gFrontScreenState != 0)
	{
		i32 triggerMask = gFrontUseAltTriggerMask ? 0x40040 : 0x40000;
		primaryConfirm = PCSHELL_CheckTriggers(triggerMask, 1, 1);
		confirmPressed = PCSHELL_CheckTriggers(0x10010, 1, 1) || primaryConfirm;
		cancelTrigger = PCSHELL_CheckTriggers(0x20220, 1, 1);
		if (confirmPressed)
			cancelTrigger = 0;
	}
	else
	{
		primaryConfirm = PCSHELL_CheckTriggers(0x40040, 1, 1);
		if (primaryConfirm)
			gFrontConfirmSeen = 0;
		Pad_Update();
	}

	if (gFrontScreenState != 0 && TTime % 10 == 0)
		Card_CheckStatus(0, 0);

	u8 cardExists = DCCard_Exists(0);
	gFrontCardExistsThisFrame = cardExists;

	i32 cardChoice;
	if (*gFrontCardExists == 0 || cardExists != 0)
	{
		if (gFrontCardPollDelay-- <= 0)
		{
			cardChoice = gFrontCardSlotChoice;
		}
		else
		{
			cardChoice = 0;
			gFrontCardSlotChoice = 0;
		}
	}
	else
	{
		cardChoice = 0;
		gFrontCardPollDelay = 40;
		gFrontCardSlotChoice = 0;
	}
	*gFrontCardExists = cardExists;

	if (gFrontFadeGateOne != 0 && gFrontFadeGateTwo != 0)
	{
		if (gFrontFadeTimerActive == 0)
		{
			gFrontFadeTimerActive = 1;
			gFrontFadeTimerStart = G_VBLANKS;
		}

		if (static_cast<u32>(G_VBLANKS - gFrontFadeTimerStart) > 0x78)
		{
			gFrontFadeTimerActive = 0;
			closeMenu = 1;
			gLevelStatus = 7;
		}
	}
	else
	{
		gFrontFadeTimerActive = 0;
	}

	switch (gFrontScreenState)
	{
	case 0:
	{
		if ((primaryConfirm == 0 || gLevelStatus != 0) && cardChoice != 0 && cardChoice != -1)
			goto sharedExit;

		Pad_ClearTriggers(&G_SCONTROL[0]);
		SFX_Pause();
		gFrontSfxHandle = SFX_Play(30, 0x2000, 0);
		Redbook_XAPause(true);
		Pad_ActuatorOff(0, 0);
		Pad_ActuatorOff(0, 1);
		G_POST_WATER_EFFECT = 1;
		Post_DoPausePaletteProcessing();

		print_if_false(gPausedMenu == 0, "Already got a paused menu?");

		void* pMenuMem = CMenu::operator new(sizeof(CMenu));
		gPausedMenu = pMenuMem ? ::new (pMenuMem) CMenu(256, 0, 0, 256, 256, 16) : 0;

		gPausedMenu->AddEntry(gFrontContinueText);
		gPausedMenu->AddEntry(gFrontSkipToRestartText);

		if (gFrontSkipToRestartEnabled == 0)
			gPausedMenu->EntryOff(gFrontSkipToRestartText);

		gPausedMenu->AddEntry(gFrontRestartLevelText);
		gPausedMenu->AddEntry(gFrontQuitText);

		gPausedMenu->mY = (240 - gPausedMenu->GetMenuHeight()) / 2 + 5;

		gPausedMenu->Zoom(0);

		if (cardChoice != 0 && cardChoice != -1)
		{
			gFrontMysteryValueOne = 512;
			gFrontScreenState = 1;
			goto sharedExit;
		}

		gFrontScreenState = 4;
		goto sharedExit;
	}

	case 1:
	{
		gFrontMysteryValueOne = PShell_MoveTowards(gFrontMysteryValueOne, 460);

		if (G_POST_WATER_EFFECT == 0)
			closeMenu = 1;

		if (gFrontCardSlotChoice == 0 || gFrontCardSlotChoice == -1)
			gFrontScreenState = 4;

		print_if_false(gPausedMenu != 0, "No paused menu?");

		if (gFrontSkipToRestartEnabled != 0)
			gPausedMenu->EntryOn(gFrontSkipToRestartText);
		else
			gPausedMenu->EntryOff(gFrontSkipToRestartText);

		gPausedMenu->Update();

		i32 mouseConfirm = Front_MenuMouseConfirmsSelection(gPausedMenu);
		i32 sfxToPlay = 0;

		if (confirmPressed || mouseConfirm != 0)
		{
			if (primaryConfirm != 0)
			{
				Pad_ClearTriggers(&G_SCONTROL[0]);
				gFrontSfxHandle = SFX_Play(30, 0x2000, 0);
				goto sharedExit;
			}

			if (gPausedMenu->ChoiceIs(gFrontRestartLevelText))
			{
				closeMenu = 1;
				gLevelStatus = 8;
			}

			if (gPausedMenu->ChoiceIs(gFrontContinueText))
			{
				Pad_ClearTriggers(&G_SCONTROL[0]);
				sfxToPlay = 30;
				closeMenu = 1;
			}

			if (gPausedMenu->ChoiceIs(gFrontSkipToRestartText))
			{
				sfxToPlay = 31;
				gFrontScreenState = 2;
			}

			if (gPausedMenu->ChoiceIs(gFrontQuitText))
			{
				pYesNoMenu->Reset();
				sfxToPlay = 31;
				pYesNoMenu->Zoom(0);
				gFrontScreenState = 3;
				gFrontMysteryValueOne = 800;
			}

			if (closeMenu != 0)
			{
				gFrontSfxHandle = SFX_Play(sfxToPlay, 0x2000, 0);
				goto sharedExit;
			}
		}

		SFX_Play(sfxToPlay, 0x2000, 0);
		break;
	}

	case 2:
	{
		if (gFrontControllerTwoMenu == 0)
		{
			void* pMenuMem = CMenu::operator new(sizeof(CMenu));
			gFrontControllerTwoMenu = pMenuMem ? ::new (pMenuMem) CMenu(256, 0, 0, 256, 256, 16) : 0;

			print_if_false(gFrontRestartNodeCount != 0, "No restarts listed");

			for (i32 i = 0; i < gFrontRestartNodeCount; i++)
			{
				char* pName = gFrontRestartNodeNames[i];

				char code[4];
				code[0] = pName[0];
				code[1] = pName[1];
				code[2] = pName[2];
				code[3] = 0;

				if (!Utils_CompareStrings(code, gFrontRestartFilterNode))
					gFrontControllerTwoMenu->AddEntry(pName);
			}

			gFrontControllerTwoMenu->mY = (240 - gFrontControllerTwoMenu->GetMenuHeight()) / 2;
		}

		gFrontControllerTwoMenu->Update();

		if (confirmPressed)
		{
			Trig_SetRestart(const_cast<char*>(gFrontControllerTwoMenu->mEntry[gFrontControllerTwoMenu->mLine].name));
			delete gFrontControllerTwoMenu;
			gFrontControllerTwoMenu = 0;
			closeMenu = 1;
			gLevelStatus = 6;
			SFX_Play(31, 0x2000, 0);
		}

		if (cancelTrigger != 0)
		{
			delete gFrontControllerTwoMenu;
			gFrontControllerTwoMenu = 0;
			gFrontScreenState = 1;
			SFX_Play(35, 0x2000, 0);
		}

		goto sharedExit;
	}

	case 3:
	{
		gFrontMysteryValueOne = PShell_MoveTowards(gFrontMysteryValueOne, 350);

		if (gFrontCardSlotChoice == 0 || gFrontCardSlotChoice == -1)
		{
			gFrontScreenState = 4;
			goto sharedExit;
		}

		if (pYesNoMenu->mLine > 40)
			Pad_ClearTriggers(&G_SCONTROL[0]);

		i32 prevLine = pYesNoMenu->mLine;
		pYesNoMenu->Update();
		if (pYesNoMenu->mLine != prevLine)
			gFrontMysteryValueOne = 350;

		i32 mouseConfirm = Front_MenuMouseConfirmsSelection(pYesNoMenu);

		if (confirmPressed || mouseConfirm != 0)
		{
			if (primaryConfirm != 0)
			{
				closeMenu = 1;
				SFX_Play(30, 0x2000, 0);
			}
			else if (pYesNoMenu->ChoiceIs(gFrontNoText))
			{
				closeMenu = 1;
				gLevelStatus = 7;
				SFX_Play(31, 0x2000, 0);
			}
		}

		if (cancelTrigger == 0)
			goto sharedExit;

		SFX_Play(35, 0x2000, 0);
		if (gPausedMenu != 0)
			gPausedMenu->Zoom(0);
		gFrontScreenState = 1;
		gFrontMysteryValueOne = 512;
		goto sharedExit;
	}

	case 4:
		if (cardChoice != 0 && cardChoice != -1)
			gFrontScreenState = 1;
		goto sharedExit;

	case 9:
		Flash_Update();
		if (Flash_FadeFinished() == 0)
			goto sharedExit;
		gLevelStatus = 3;
		closeMenu = 1;
		goto sharedExit;

	default:
		goto sharedExit;
	}

sharedExit:
	if (closeMenu != 0)
	{
		G_POST_WATER_EFFECT = 0;
		Post_UndoPausePaletteProcessing();
		gFrontScreenState = 0;

		delete gPausedMenu;
		gPausedMenu = 0;

		pYesNoMenu->KillBox();

		if (gLevelStatus != 7 && gLevelStatus != 3)
		{
			SFX_Unpause();
			Redbook_XAPause(false);
		}
	}
}

// @Ok
// @Matching
 void PrintPaused(void)
{
	Mess_SetScale(256);
	Mess_SetRGB(0x7F, 0x19, 0x21, 0);
	Mess_DrawText(256, 50, "Paused", 0, 0x1000);
}

// @Ok
// Functional: turn off gauge, logic verified against Hex-Rays at 0x440ae0.
void Front_GaugeOff(void)
{
	gFrontGauge = 0;
}

// @Ok
CMenu::CMenu(int x,int y,unsigned char Justification,int HiScale,int LowScale, int LineSep)
{
	this->mX = x;
	this->mY = y;
	this->mJustification = Justification;
	this->mLineSep = LineSep;

	for (int i = 0; i<40; i++)
	{
		this->mEntry[i].unk_c = 0x80;
		this->mEntry[i].unk_d = 0x80;
		this->mEntry[i].unk_e = 0x80;
		this->mEntry[i].field_14 = 69;
		this->mEntry[i].field_15 = 60;
		this->mEntry[i].field_16 = 107;
		this->mEntry[i].field_11 = 69;
		this->mEntry[i].field_12 = 60;
		this->mEntry[i].field_13 = 107;
		this->mEntry[i].field_17 = 40;
		this->mEntry[i].field_18 = 35;
		this->mEntry[i].field_19 = 62;
		this->mEntry[i].field_8 = HiScale;
		this->mEntry[i].field_A = LowScale;
	}

	/*
	this->scrollbar_one = 2;

	this->field_1B = -1;
	this->scrollbar_zero = 1;
	this->field_32 = 0;
	this->field_30 = 0;
	this->field_34 = 0;
	this->field_38 = 0;
	*/

	this->scrollbar_one = 2;
	this->field_32 = 0;
	this->field_30 = 0;
	this->field_34 = 0;
	this->field_38 = 0;
	this->mLine = 0;
	this->field_1B = -1;
	this->scrollbar_zero = 1;

	this->Reset();
}

// @Ok
void CMenu::GetEntryXY(const char* entry, int* x, int* y)
{
	*x = this->mX;
	*y = this->mY;

	for ( int i = this->mCursorLine;
			i < this->mNumLines
				&& i < (this->mCursorLine + this->field_1B);
			i++ )
	{
		*y += this->mEntry[i].unk_a;
		if ( this->mEntry[i].unk_b )
		{
			if ( Utils_CompareStrings(entry, this->mEntry[i].name) )
				return;
			*y += this->mLineSep;
		}
	}
}

// @Ok
 i32 CMenu::GetMenuHeight(void)
{
  int v1 = 0;
  int v2 = 0;

  for ( int i = this->mCursorLine;
        i < this->mNumLines && i < (this->mCursorLine + this->field_1B);
        i++ )
  {
    if ( this->mEntry[i].unk_b )
    {
      v1 += this->mEntry[i].unk_a;
      v2++;
    }
  }

  return v1 + (v2 - 1) * this->mLineSep;
}

// @Ok
void CMenu::CentreY(void)
{
	this->mY = (240 - this->GetMenuHeight()) / 2;
}

// @Ok
void CMenu::NonGouraud(void)
{
	for(int i = 0; i < 40; i++)
	{
		this->mEntry[i].field_14 = this->mEntry[i].unk_c;
		this->mEntry[i].field_15 = this->mEntry[i].unk_d;
		this->mEntry[i].field_16 = this->mEntry[i].unk_e;
		this->mEntry[i].field_17 = this->mEntry[i].field_11;
		this->mEntry[i].field_18 = this->mEntry[i].field_12;
		this->mEntry[i].field_19 = this->mEntry[i].field_13;
	}
}

// @Ok
void CMenu::SetNormalColor(unsigned int a2, int a3, int a4, int a5)
{
	this->mEntry[a2].field_11 = a3;
	this->mEntry[a2].field_12 = a4;
	this->mEntry[a2].field_13 = a5;

	this->mEntry[a2].field_17 = 150 * a3 / 256;
	this->mEntry[a2].field_18 = 150 * a4 / 256;
	this->mEntry[a2].field_19 = 150 * a5 / 256;
}


// @Ok
void CMenu::SetSelColor(unsigned int a2, int a3, int a4, int a5)
{
	this->mEntry[a2].unk_c = a3;
	this->mEntry[a2].unk_d = a4;
	this->mEntry[a2].unk_e = a5;

	this->mEntry[a2].field_14 = 150 * a3 / 256;
	this->mEntry[a2].field_15 = 150 * a4 / 256;
	this->mEntry[a2].field_16 = 150 * a5 / 256;
}

// @Ok
int  CMenu::FindEntry(const char* a2)
{
	for(int i = 0; i<this->mNumLines; i++)
	{
		if (Utils_CompareStrings(a2, this->mEntry[i].name))
		{
			return i;
		};
	}
	
	print_if_false(0, "Entry not found");
	return 0;
}

// @Ok
// Bug fix (2026-08-31): was field_32-- (copy-paste from EntryOff). Verified
// against ground-truth disassembly of an inlined EntryOn call site in
// Front_Update (0x441576-0x44158c, `inc word ptr [ebp+32h]`), since EntryOn
// itself has no standalone address in the PC binary (always inlined at its
// few call sites, so there is no dedicated function bytes to diff against).
// field_32 tracks the enabled-entry count (AddEntry increments it,
// EntryOff decrements it); re-enabling an entry must increment it back,
// not decrement it again. The old code silently corrupted CMenu::Update's
// scrollbar fraction (field_32 - field_1B) on repeated enable/disable.
void CMenu::EntryOn(const char* a2)
{
	int res = this->FindEntry(a2);
	if (!this->mEntry[res].unk_b)
	{
		this->mEntry[res].unk_b = 1;
		this->field_32++;
	}
}

// @Ok
// On the assignement it's weird has it properly addresses the offset
// while for the if it does the weird jump
void CMenu::EntryOff(const char* a2)
{
	int res = this->FindEntry(a2);
	if (this->mEntry[res].unk_b)
	{
		this->mEntry[res].unk_b = 0;
		this->field_32--;
	}
}

// @Ok
// has the sub eax, 0 for some reason
void CMenu::CentreX(void)
{
	int just = this->mJustification;

	if (just)
	{
		switch(just)
		{
			case 1:
				this->mX = (512 - this->menu_width) / 2;
				return;
			case 2:
				this->mX = this->menu_width + (512 - this->menu_width) / 2;
				return;
		}
	}
	else
	{
		this->mX = 256;
	}
}

// @Ok
int CMenu::FinishedZooming(void)
{
	int *ptr_to = reinterpret_cast<int*>(this->ptr_to);
	if (!ptr_to)
	{
		return 1;
	}

	return ptr_to[12] != 0;
}

// @Ok
void CMenu::AdjustWidth(int width)
{
	this->width_val_a = width;
}

// @Ok
int CMenu::ChoiceIs(const char* pString)
{
	if (this->mLine < 0x28)
	{
		return Utils_CompareStrings(pString, this->mEntry[this->mLine].name);
	}

	return 0;
}

// @Ok
// slightly different registers but the same
void CMenu::SetRedText(unsigned char Line)
{
	print_if_false(Line < this->mNumLines, "Bad line sent to SetRedText");

	this->mEntry[Line].unk_c = 255;
	this->mEntry[Line].unk_d = 0;
	this->mEntry[Line].unk_d = -1;

	this->mEntry[Line].field_15 = 0;
	this->mEntry[Line].field_16 = 0;

	this->mEntry[Line].field_11 = 96;
	this->mEntry[Line].field_12 = 0;
	this->mEntry[Line].field_13 = 0;

	
	this->mEntry[Line].field_17 = 96;
	this->mEntry[Line].field_18 = 0;
	this->mEntry[Line].field_19 = 0;
}

// @Ok
void CMenu::Reset(void)
{
	this->SetLine(0);
}

// @Ok
// loop offset starts in field_8, instead of unk_b, but that's fine
void CMenu::SetLine(char Line)
{
	this->mLine = Line;

	SEntry *entries = this->mEntry;
	for(i32 i = 0; i < 40; i++)
	{
		i16 v5;

		if (i != this->mLine)
		{
			v5 = entries[i].field_A;
		}
		else
		{
			v5 = entries[i].field_8;
		}

		entries[i].val_b = v5;
		entries[i].val_a = v5;
	}
}

// CMenu::ProcessMouse (0x50C8A0) is defined in PCShell.cpp, not here: its
// address sits next to the PCSHELL_* functions, and the original CMenu::Update
// calls it with a plain direct call, same TU boundary as CMenu::EntryEnable
// (see the comment on that function in PCShell.cpp).

// Mac symbol Update__5CMenuFv, address 0x440600
// @Ok
// Functional decompile, verified field-by-field against the IDA decompile
// at 0x440600 (2026-08-31): every raw offset (mLine 0x14, mNumLines 0x1A,
// scrollbar_one 0x10, mEntry[i].unk_b/what/val_a/val_b/field_8/field_A,
// field_32 0x32, field_1B 0x1B, ptr_to->field_28 0x28) matches, including
// the skip-loop's two-stage val_a/val_b write pattern and the final
// scrollbar fraction computation. cmpsum residue: 182 mnemonic diffs, all
// one cascade from a single register choice in the repeat-timer check
// (original keeps the timer counter in edi across the whole "if
// (timer<=20) skip" / idiv-by-scrollbar_one block, reusing edi right after
// for the 0x400 constant; this build loads it into eax directly and never
// touches edi there). Everything before this point (leading push order,
// the disabled-entry skip loop, the ProcessMouse call site, the direction
// of the timer<=20 check, the up/down navigation do-whiles) matches. 7
// hypotheses tried at the byte-match bar, logged in front.attempts.md; per
// this session's functional-decomp bar, accepted as-is.
void CMenu::Update(void)
{
	if ((u8)this->mLine > 40)
		return;

	while (!(this->mEntry[this->mLine].unk_b && this->mEntry[this->mLine].what == 0))
	{
		this->mEntry[this->mLine].val_b = this->mEntry[this->mLine].field_A;
		this->mEntry[this->mLine].val_a = this->mEntry[this->mLine].field_A;

		this->mLine++;
		if (this->mLine >= this->mNumLines)
			this->mLine = 0;

		this->mEntry[this->mLine].val_b = this->mEntry[this->mLine].field_8;
		this->mEntry[this->mLine].val_a = this->mEntry[this->mLine].field_A;
	}

	i32 startLine = this->mLine;
	i32 mouseRes = this->ProcessMouse();

	if (mouseRes != 2 && PCSHELL_CheckTriggers(0x3003, 0, 0))
	{
		i32 timer = gMenuNavRepeatTimer;

		if (timer != 0)
		{
			if (timer <= 20)
				goto skipMove;
			if (timer % this->scrollbar_one != 0)
				goto skipMove;
		}

		{
			if (PCSHELL_CheckTriggers(0x1001, 0, 0))
			{
				G_SCONTROL[0].Up.Triggered = 0;
				this->mEntry[this->mLine].val_b = this->mEntry[this->mLine].field_A;

				do
				{
					if (this->mLine != 0)
					{
						this->mLine--;
						this->field_20 = 0x400;
					}
					else if (this->scrollbar_zero)
					{
						this->mLine = this->mNumLines - 1;
						this->field_20 = 0x400;
					}
				}
				while (!(this->mEntry[this->mLine].unk_b && this->mEntry[this->mLine].what == 0));

				this->mEntry[this->mLine].val_b = this->mEntry[this->mLine].field_8;
			}

			if (PCSHELL_CheckTriggers(0x2002, 0, 0))
			{
				G_SCONTROL[0].Down.Triggered = 0;
				this->mEntry[this->mLine].val_b = this->mEntry[this->mLine].field_A;

				do
				{
					this->mLine++;
					if (this->mLine < this->mNumLines)
					{
						this->field_20 = 0x400;
					}
					else if (this->scrollbar_zero)
					{
						this->mLine = 0;
						this->field_20 = 0x400;
					}
					else
					{
						this->mLine--;
					}
				}
				while (!(this->mEntry[this->mLine].unk_b && this->mEntry[this->mLine].what == 0));

				this->mEntry[this->mLine].val_b = this->mEntry[this->mLine].field_8;
			}
		}

skipMove:
		gMenuNavRepeatTimer++;
	}
	else
	{
		gMenuNavRepeatTimer = 0;
	}

	if (this->mLine != startLine)
		SFX_Play(0x29, 0x3FFF, 0);

	for (i32 i = 0; i < this->mNumLines; i++)
	{
		if (this->mEntry[i].val_a >= this->mEntry[i].val_b)
		{
			this->mEntry[i].val_a = this->mEntry[i].val_a + 40;
			if (this->mEntry[i].val_a > this->mEntry[i].val_b)
				this->mEntry[i].val_a = this->mEntry[i].val_b;
		}

		if (this->mEntry[i].val_a <= this->mEntry[i].val_b)
		{
			this->mEntry[i].val_a = this->mEntry[i].val_a - 40;
			if (this->mEntry[i].val_a < this->mEntry[i].val_b)
				this->mEntry[i].val_a = this->mEntry[i].val_b;
		}
	}

	if (mouseRes == 0 && this->mLine != startLine)
	{
		if (this->mLine < this->mCursorLine)
		{
			this->mCursorLine = this->mLine;
		}
		else
		{
			i32 activeCount = 0;

			if (this->mCursorLine <= this->mLine)
			{
				for (i32 k = this->mCursorLine; k <= this->mLine; k++)
					if (this->mEntry[k].unk_b)
						activeCount++;
			}

			if (activeCount > this->field_1B)
			{
				if (this->mLine == 0)
				{
					this->mCursorLine = 0;
				}
				else
				{
					i32 visible = 0;

					do
					{
						if (this->mEntry[this->mCursorLine].unk_b)
						{
							visible++;
							if (visible == this->field_1B)
								break;
						}

						this->mCursorLine--;
					}
					while (this->mCursorLine != 0);
				}
			}
		}
	}

	if (!this->ptr_to)
		return;

	if ((u16)this->field_32 <= this->field_1B)
	{
		this->ptr_to->field_28 = 0;
	}
	else
	{
		this->ptr_to->field_28 = (this->mCursorLine * 256) / (this->field_32 - this->field_1B);
	}
}

// @Ok
CMenu::~CMenu()
{
	this->KillBox();
}

void validate_CMenu(void)
{
	VALIDATE_SIZE(CMenu, 0x53C);

	VALIDATE(CMenu, ptr_to, 0x4);
	VALIDATE(CMenu, menu_width, 0x8);
	VALIDATE(CMenu, text_val_b, 0xA);
	VALIDATE(CMenu, width_val_a, 0xC);

	VALIDATE(CMenu, scrollbar_one, 0x10);
	VALIDATE(CMenu, scrollbar_zero, 0x11);
	VALIDATE(CMenu, mJustification, 0x12);
	VALIDATE(CMenu, mLine, 0x14);

	VALIDATE(CMenu, mCursorLine, 0x15);
	VALIDATE(CMenu, field_16, 0x16);
	VALIDATE(CMenu, field_17, 0x17);
	VALIDATE(CMenu, mNumLines,  0x1A);
	VALIDATE(CMenu, field_1B,  0x1B);
	VALIDATE(CMenu, mZoomBoxType,  0x1C);
	VALIDATE(CMenu, field_1E, 0x1E);
	VALIDATE(CMenu, field_20, 0x20);
	VALIDATE(CMenu, mX, 0x24);
	VALIDATE(CMenu, mY, 0x28);
	VALIDATE(CMenu, mLineSep, 0x2C);

	VALIDATE(CMenu, field_30, 0x30);
	VALIDATE(CMenu, field_32, 0x32);
	VALIDATE(CMenu, field_34, 0x34);
	VALIDATE(CMenu, field_38, 0x38);

	VALIDATE(CMenu, mEntry, 0x3C);
}

void validate_SEntry(void)
{
	VALIDATE_SIZE(SEntry, 0x20);

	VALIDATE(SEntry, name, 0x0);
	VALIDATE(SEntry, val_a, 0x4);
	VALIDATE(SEntry, val_b, 0x6);
	VALIDATE(SEntry, field_8, 0x8);
	VALIDATE(SEntry, field_A, 0xA);
	VALIDATE(SEntry, unk_a, 0xC);
	VALIDATE(SEntry, unk_b, 0xD);
	VALIDATE(SEntry, unk_c, 0xE);
	VALIDATE(SEntry, unk_d, 0xF);
	VALIDATE(SEntry, unk_e, 0x10);
	VALIDATE(SEntry, field_11, 0x11);
	VALIDATE(SEntry, field_12, 0x12);
	VALIDATE(SEntry, field_13, 0x13);
	VALIDATE(SEntry, field_14, 0x14);
	VALIDATE(SEntry, field_15, 0x15);
	VALIDATE(SEntry, field_16, 0x16);
	VALIDATE(SEntry, field_17, 0x17);
	VALIDATE(SEntry, field_18, 0x18);
	VALIDATE(SEntry, field_19, 0x19);
	VALIDATE(SEntry, field_1A, 0x1A);
	VALIDATE(SEntry, field_1B, 0x1B);
	VALIDATE(SEntry, what, 0x1C);
}

void validate_SLevel(void)
{
	VALIDATE_SIZE(SLevel, 0x14);

	VALIDATE(SLevel, mDisplayName, 0x0);
	VALIDATE(SLevel, mName, 0x4);

	VALIDATE(SLevel, field_C, 0xC);
}
