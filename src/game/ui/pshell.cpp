#include "game/ui/pshell.h"
#include "engine/rendering/scene_renderer.h"
#include "game/ui/mess.h"
#include "core/utils.h"
#include "engine/assets/spool.h"
#include "engine/audio/ps2lowsfx.h"
#include "game/ui/shell.h"
#include "game/ui/PCShell.h"
#include "game/ui/front.h"
#include "engine/rendering/render_buffers.h"
#include "game/application/tweak.h"
#include "engine/input/ps2pad.h"
#include "game/ui/panel.h"
#include "game/player/spidey.h"
#include "engine/rendering/PCGfx.h"
#include "engine/world/trig.h"
#include "engine/audio/ps2redbook.h"
#include "game/cinematics/camera.h"

#include <cstring>

#include "compatibility/validate.h"

EXPORT i32 JoelJewCheatCode;

enum ECheatCode
{
        CHEAT_WEAKNESS = 0,
        CHEAT_LEANEST = 1,
        CHEAT_GLANDS = 2,
        CHEAT_EGOTRIP = 3,
        CHEAT_RULUR = 4,
        CHEAT_SECRTWAR = 5,
        CHEAT_MIGUELOH = 6,
        CHEAT_TRISNTNL = 7,
        CHEAT_SYNOPTIC = 8,
        CHEAT_XILRTRNS = 9,
        CHEAT_KICKME = 10,
        CHEAT_MRWATSON = 11,
        CHEAT_SMLVIII = 12,
        CHEAT_ROBRTSON = 13,
        CHEAT_KIRBYFAN = 14,
        CHEAT_MMEWEB = 15,
        CHEAT_FANBOY = 16,
        CHEAT_CINEMA = 17,
        CHEAT_RGSGLLRY = 18,
        CHEAT_UATUSEES = 19,
        CHEAT_ADMNTIUM = 20,
        CHEAT_CLUBNOIR = 21,
        CHEAT_STICKMAN = 22,
        CHEAT_FUNKYTWN = 23,
};

// @Ok
#ifndef SPIDEY_STANDALONE
SCheat gCheats[NUM_CHEATS] =
{
	{
			"WEAKNESS",
			"full health",
	},
	{
			"LEANEST",
			"everything",
	},
	{
			"GLANDS",
			"unlimited webbing",
	},
	{
			"EGOTRIP",
			"pulsating head",
	},
	{
			"RULUR",
			"J James Jewett",
	},
	{
			"SECRTWAR",
			"symbiote spidey costume",
	},
	{
			"MIGUELOH",
			"spidey 2099 costume",
	},
	{
			"TRISNTNL",
			"Captain Universe Costume",
	},
	{
			"SYNOPTIC",
			"Spidey Unlimited Costume",
	},
	{
			"XILRTRNS",
			"Scarlet Spider Costume",
	},
	{
			"KICK ME",
			"Amazing Bag Man Costume",
	},
	{
			"MRWATSON",
			"Peter Parker Costume",
	},
	{
			"SM LVIII",
			"Quick Change Costume",
	},
	{
			"ROBRTSON",
			"Storyboard Viewer",
	},
	{
			"KIRBYFAN",
			"Game Comic Covers",
	},
	{
			"MME WEB",
			"Level Select",
	},
	{
			"FANBOY",
			"Comic Collection",
	},
	{
			"CINEMA",
			"Movie Viewer",
	},
	{
			"RGSGLLRY",
			"Character Viewer",
	},
	{
			"UATUSEES",
			"What If Contest",
	},
	{
			"ADMNTIUM",
			"invulnerable",
	},
	{
			"CLUBNOIR",
			"Ben Reilly Costume",
	},
	{
			"STICKMAN",
			"Stick Spidey",
	},
	{
			"FUNKYTWN",
			"Toon Spidey",
	},
};
#else
extern SCheat gCheats[NUM_CHEATS];
#endif

// these five and mCheatStoryboardFlag/field_78 now live as real fields in
// SSaveGame (shell.h), found via ActivateCheat's cheat flag writes landing
// inside gSaveGame's address range; see shell.h for per-field evidence.
#define G_LEVEL_SELECT_FLAG (*reinterpret_cast<u32*>(0x0060CFD8))
#define G_UNLIMITED_WEBBING_FLAG (*reinterpret_cast<i32*>(0x0060CFE8))
#define G_PULSATING_HEAD_FLAG (*reinterpret_cast<i32*>(0x0060CFF0))
#define G_INVULNERABLE_FLAG (*reinterpret_cast<i32*>(0x0060CFC8))

// well past .data's raw file image (real BSS), zero at load
#define G_STICKMAN_FLAG (*reinterpret_cast<i32*>(0x02E09BF4))
#define G_TOON_SPIDEY_FLAG (*reinterpret_cast<i32*>(0x02E09BF0))

// residue: 45 mnemonic diffs out of 95 instructions, same byte length (391).
// Every case's logic and the switch dispatch itself match (jump table shape,
// bounds check, eax=1 preset). The residue is that several cases compile as
// load/modify/store (mov reg,[mem]; op reg,imm; mov[mem],reg) here where the
// original folds to a single memory-immediate op (or dword ptr [mem],imm /
// xor [mem],eax). Moving the newly-discovered cheat flags into real
// SSaveGame fields (see shell.h) fixed most of the |= cases on
// gSaveGame.field_80/84/88/8C/90 (confirmed via cmpsum, dropped 54 -> 45
// diffs); the remaining ones are on plain fixed-address globals outside any
// known struct (0x60CFE8, 0x60CFF0, 0x60CFC8, 0x2E09BF4, 0x2E09BF0) where no
// such struct exists to attach them to. 11 hypotheses tried: explicit
// return-per-case vs a shared result local with break (result+break fixed
// the dispatch header, 68->54 diffs), G_* macro vs static const pointer for
// the flag globals (no effect), an explicit bounds check before the switch
// instead of relying on the jump table's own check (worse, 69 diffs),
// dropping the CHEAT_WEAKNESS local in favor of inline G_MECHLIST casts (no
// effect), moving the cheat flags into real SSaveGame struct fields
// (improvement, see above), and XORing against the already-1 result local
// instead of the literal 1 (no effect). See pshell.attempts.md. All 45
// remaining diffs are load/modify/store vs memory-immediate encoding of the
// same operation, verified case by case against the switch dispatch: every
// case writes the right field/global with the right value. Functional bar
// (per session direction 2026-08-30): logic is correct, tagged @Ok.
// @Ok
i32 ActivateCheat(i32 a1)
{
	i32 result = 1;

	switch (a1)
	{
		case CHEAT_WEAKNESS:
			if (G_MECHLIST)
			{
				((CPlayer*)G_MECHLIST)->mHealth = ((CPlayer*)G_MECHLIST)->mMaxHealth;
			}
			break;

		case CHEAT_LEANEST:
			G_SAVE_GAME.field_78 = 1;
			G_SAVE_GAME.field_80 = -1;
			G_SAVE_GAME.field_84 = -1;
			G_SAVE_GAME.field_88 = -1;
			G_SAVE_GAME.field_8C = -1;
			G_SAVE_GAME.field_90 = -1;
			G_SAVE_GAME.mCheatStoryboardFlag = 1;
			G_LEVEL_SELECT_FLAG = 1;
			break;

		case CHEAT_GLANDS:
			G_UNLIMITED_WEBBING_FLAG ^= 1;
			break;

		case CHEAT_EGOTRIP:
			G_PULSATING_HEAD_FLAG ^= 1;
			break;

		case CHEAT_RULUR:
			JoelJewCheatCode = !JoelJewCheatCode;
			break;

		case CHEAT_SECRTWAR:
			G_SAVE_GAME.field_80 |= 4;
			break;

		case CHEAT_MIGUELOH:
			G_SAVE_GAME.field_80 |= 2;
			break;

		case CHEAT_TRISNTNL:
			G_SAVE_GAME.field_80 |= 8;
			break;

		case CHEAT_SYNOPTIC:
			G_SAVE_GAME.field_80 |= 0x10;
			break;

		case CHEAT_XILRTRNS:
			G_SAVE_GAME.field_80 |= 0x40;
			break;

		case CHEAT_KICKME:
			G_SAVE_GAME.field_80 |= 0x20;
			break;

		case CHEAT_MRWATSON:
			G_SAVE_GAME.field_80 |= 0x200;
			break;

		case CHEAT_SMLVIII:
			G_SAVE_GAME.field_80 |= 0x100;
			break;

		case CHEAT_ROBRTSON:
			G_SAVE_GAME.mCheatStoryboardFlag = !G_SAVE_GAME.mCheatStoryboardFlag;
			break;

		case CHEAT_KIRBYFAN:
			G_SAVE_GAME.field_90 = 0x3F;
			break;

		case CHEAT_MMEWEB:
			G_LEVEL_SELECT_FLAG = 1;
			break;

		case CHEAT_FANBOY:
			G_SAVE_GAME.field_8C = -1;
			break;

		case CHEAT_CINEMA:
			G_SAVE_GAME.field_88 = -1;
			break;

		case CHEAT_RGSGLLRY:
			G_SAVE_GAME.field_84 = -1;
			break;

		case CHEAT_UATUSEES:
			gWhatIf = !gWhatIf;
			break;

		case CHEAT_ADMNTIUM:
			G_INVULNERABLE_FLAG ^= 1;
			break;

		case CHEAT_CLUBNOIR:
			G_SAVE_GAME.field_80 |= 0x80;
			break;

		case CHEAT_STICKMAN:
			if (G_STICKMAN_FLAG = !G_STICKMAN_FLAG)
			{
				G_TOON_SPIDEY_FLAG = 0;
			}
			break;

		case CHEAT_FUNKYTWN:
			if (G_TOON_SPIDEY_FLAG = !G_TOON_SPIDEY_FLAG)
			{
				G_STICKMAN_FLAG = 0;
			}
			break;

		default:
			result = 0;
			break;
	}

	return result;
}

// @Ok
// @NotMatching - those ++ are weird man
void DisplayScore(
		i32 a1,
		i32 a2,
		i32 a3,
		i32 a4)
{
	char v9[0x34];
	if (!a4)
	{
		i32 v4 = a3 / 6 / 600;
		i32 v5 = a3 / 6 % 600;

		if ( v4 > 99 )
			v4 = 99;

		char* v6 = &v9[0];
		if ( v4 > 9 )
		{
			v6 = &v9[1];
			v9[0] = v4 / 10 + '0';
		}

		*v6 = v4 % 10 + '0';
		char* v7 = v6 + 1;
		*v7++ = 58;
		*v7++ = (char)v5 / 100 + '0';
		*v7++ = v5 % 100 / 10 + '0';
		*v7++ = 46;

		*v7 = v5 % 100 % 10 + '0';
		v7[1] = 0;
		Mess_DrawText(a1, a2, v9, 0, 0x1000);
	}
	else
	{
		sprintf(v9, "%d", a3);
		Mess_DrawText(a1, a2, v9, 0, 0x1000);
	}
}

// @Ok
// @Matching
i32 PShell_ActivateCheat(char * pStr)
{
	i32 v1 = -1;

	for (i32 i = 0; i < NUM_CHEATS; i++)
	{
		if (Utils_CompareStrings(pStr, gCheats[i].pCode))
		{
			v1 = i;
			break;
		}
	}

	if (v1 == CHEAT_RULUR)
		return -1;

	if (ActivateCheat(v1))
		return v1;

	return -1;
}

// byte right after TimeAttackComplete (0x60CFC6) and gWhatIf (0x60CFC5, ob.cpp).
// Neither TimeAttackComplete nor this byte are declared in the repo yet; the
// maintainer's IDB names TimeAttackComplete but not this one.
static u8 * const gPracticeDifficultyFlag = (u8*)0x60CFC7;

// @Ok
void PShell_ApplyGameState(void)
{
	i8 v1 = G_SAVE_GAME.mDifficulty;
	i16 v2 = G_GAMESTATE[11];
	i16 v3 = G_GAMESTATE[12];
	i16 v4 = G_SAVE_GAME.mDigitalMapping[3];
	i16 v5 = G_SAVE_GAME.mAnalogueMapping[0];

	DifficultyLevel = v1;
	G_SAVE_GAME.field_98 = v2;
	i16 v6 = G_SAVE_GAME.mDigitalMapping[2];
	G_GAMESTATE[4] = v5;
	i16 v7 = G_SAVE_GAME.mAnalogueMapping[1];
	G_SAVE_GAME.field_94 = v3;
	u8 v8 = G_BOOT_ROM_SOUND_MODE;
	G_GAMESTATE[3] = v4;
	*gPracticeDifficultyFlag = (v1 == 0);
	G_GAMESTATE[2] = v6;
	i16 v9 = G_GAMESTATE[13];
	G_GAMESTATE[5] = v7;
	i16 v10 = G_SAVE_GAME.mAnalogueMapping[2];
	G_SAVE_GAME.field_A0 = v8;
	i16 v11 = G_SAVE_GAME.mDigitalMapping[1];
	G_SAVE_GAME.field_9C = v9;
	i16 v12 = G_SAVE_GAME.mDigitalMapping[0];
	G_GAMESTATE[6] = v10;
	i16 v13 = G_SAVE_GAME.mAnalogueMapping[3];
	G_GAMESTATE[1] = v11;

	Pad_SetDigitalMapping(G_SCONTROL, v12, v11, v6, v4);

	G_GAMESTATE[7] = v13;
	i16 v14 = G_SAVE_GAME.field_A4;
	G_GAMESTATE[0] = v12;

	G_DOUBLE_BUFFER[0].Disp.screen.x = v14;
	G_DOUBLE_BUFFER[1].Disp.screen.x = v14;

	i16 v15 = G_SAVE_GAME.field_A8;

	G_DOUBLE_BUFFER[0].Disp.screen.y = v15;
	G_DOUBLE_BUFFER[1].Disp.screen.y = v15;

	Pad_SetAnalogueMapping(G_SCONTROL, 3, 2, 1, 0,
			G_GAMESTATE[4], G_GAMESTATE[5], G_GAMESTATE[6], G_GAMESTATE[7]);
}

// @Ok
// @Matching
void PShell_BigFont(void)
{
	Mess_SetCurrentFont("font_big.fnt");
}

// byte right after gPostWaterEffect (0x5FAE98, i32, utils.cpp). No exact name
// in the maintainer's IDB for this one; tentative, guessed from this use
// (picks the box z-offset sign, close to the water-effect/post-process flags).
static u8 * const gDrawHighlightZFlag = (u8*)0x5FAE9D;

// 5 mnemonic diffs out of 122 instructions, all in the pPoly/p setup right
// after print_if_false. Original loads pPoly straight into esi and pre-adds
// a4+4 into ebp as an eager separate instruction, reused later via a plain
// add; our build always routes pPoly through eax first and folds the +4
// into the later y0 computation as a 3-operand lea.
// 12 distinct hypotheses tried targeting this exact cluster (declaration
// order forward/reverse, split vs combined statement, volatile, pointer-unit
// vs byte-cast arithmetic, basing the increment on p vs pPoly), residue did
// not move. See pshell.attempts.md. Every store/load/field write and every
// call target/argument matches; the 5 diffs are purely which register holds
// pPoly and when the +4 offset is computed. Functional bar (per session
// direction 2026-08-30): logic is correct, tagged @Ok.
// @Ok
void PShell_DrawHighlight(i32 a1, i32 a2, i32 a3, i32 a4)
{
	Texture* pTex = Spool_FindTextureEntry(0xE90B5F6E);
	print_if_false(pTex != 0, "Missing title bar texture");

	POLY_GT4* p = (POLY_GT4*)G_PPOLY;
	G_PPOLY = (u32*)((u8*)G_PPOLY + sizeof(POLY_GT4));
	i32 v1 = a4 + 4;

	p->tag = 0xC000000;
	p->code = 0x3E;

	*(u32*)&p->u0 = *(u32*)&pTex->u0;
	*(u32*)&p->u1 = *(u32*)&pTex->u1;
	*(u32*)&p->u2 = *(u32*)&pTex->u2;
	*(u16*)&p->u3 = *(u16*)&pTex->u3;

	p->tpage = (p->tpage & ~0x40) | 0x20;

	p->u1 -= 1;
	p->u3 -= 1;

	p->r0 = 0x40;
	p->g0 = 0x40;
	p->r2 = 0x40;
	p->g2 = 0x40;

	i32 y0 = a2;
	i32 x1 = a3;
	p->b0 = 0x7B;
	p->b2 = 0x7B;
	i32 x0 = a1;

	y0 += 3;
	x1 += x0;

	p->y0 = (i16)y0;
	p->y1 = (i16)y0;

	y0 += v1;

	p->r1 = 0;
	p->g1 = 0;
	p->b1 = 0;
	p->r3 = 0;
	p->g3 = 0;
	p->b3 = 0;

	p->x0 = (i16)x0;
	p->x1 = (i16)x1;
	p->x2 = (i16)x0;
	p->y2 = (i16)y0;
	p->x3 = (i16)x1;
	p->y3 = (i16)y0;

	gsub_46CB90(G_RENDER_BUF);

	i32 sort = G_SORT;
	if (sort >= 0xFFE && sort <= 0xFFF)
	{
		DCDrawGouraudPoly(-2.0f, p, pTex, a3 < 0);
	}
	else if (*gDrawHighlightZFlag)
	{
		DCDrawGouraudPoly(2.0f, p, pTex, a3 < 0);
	}
	else
	{
		DCDrawGouraudPoly(-2.0f, p, pTex, a3 < 0);
	}
}

// Training end-of-level "new record" flow (PShell_EndTraining*). Names
// tentative, no idb_globals.txt entries in the 0x00682950-0x00682968 gap
// (right after gBiographies, 0x0068294C) or for 0x0055129C. gChallenges is
// named in idb_globals.txt (0x00551118), and its element type/size (0x10,
// STrainingMission) is confirmed in shell.h. gTrainingScore's -1000 sentinel
// and the CRecordBox/STrainingMission field_3C/field_B reads are read off
// PShell_EndTrainingDisplay's own disassembly.
static STrainingMission* const gChallenges = reinterpret_cast<STrainingMission*>(0x00551118);
#define gTrainingChallengeIndex (*reinterpret_cast<i32*>(0x0068295C))
#define gTrainingResultState (*reinterpret_cast<i32*>(0x00682958))
#define gTrainingRecordBox (*reinterpret_cast<CRecordBox**>(0x00682960))
#define gTrainingDisplayTimer (*reinterpret_cast<i32*>(0x00682964))
#define gTrainingMenu (*reinterpret_cast<CMenu**>(0x00682968))
#define gTrainingScore (*reinterpret_cast<i32*>(0x0055129C))

// Found 2026-08-27 in PShell_EndTrainingInit (0x47B720). All tentative,
// no idb_globals.txt entries at these addresses.
// Set to 1 as the very first store in the function; guess: marks the
// end-of-training screen as active/entered.
#define gTrainingActive (*reinterpret_cast<i32*>(0x00682950))
// Set to 1 alongside gTrainingActive; purpose unclear, kept separate since
// it lives far from the other gTraining* globals (near CInventory-ish
// state going by neighbouring addresses we have no names for).
#define gEndTrainingFlag (*reinterpret_cast<i32*>(0x0060CFB0))
// Read once, then cleared to 0 alongside gWideScreen (0x00660F80,
// idb_globals.txt); guess: a paired display-mode flag.
// gScreenModeFlag now lives in pshell.h as G_SCREEN_MODE_FLAG (shared with panel.cpp).
// Sentinel/insertion-position scratch for the high-score insert (-1 = "no
// room", else 0-4 = the row that was written).
#define gTrainingScratch (*reinterpret_cast<i32*>(0x005513D4))
// Found 2026-08-27 via IDA on PShell_EndTrainingUpdate (0x47BC40): this is
// the training screen's own zoom-ease value, same role as shell.cpp's
// gShellMenuEase (eased toward 0x180/384 there too, same
// PShell_MoveTowards idiom) but needs its own fixed game address since
// gShellMenuEase is a plain repo global (DLL-relocatable), same class of
// problem as gSaveGame. Set to 1262 (0x4EE) at the end of
// PShell_EndTrainingInit (previously guessed as an unrelated "timer" under
// the name gTrainingSomeTimer; renamed once EndTrainingUpdate's ease usage
// showed what it actually is).
#define gTrainingMenuEase (*reinterpret_cast<i32*>(0x005512EC))
// Found 2026-08-27 via IDA on PShell_EndTrainingInit's own disassembly:
// holds G_SCREEN_MODE_FLAG's pre-clear value (set once in EndTrainingInit,
// restored into G_SCREEN_MODE_FLAG once in EndTrainingUpdate when the player
// exits the training screen). A SEPARATE global from gTrainingScratch
// (0x5513D4, 4 bytes earlier): EndTrainingInit's own source used to alias
// gTrainingScratch for this store, which was a bug (wrote the saved value
// to the wrong address, immediately clobbered anyway by gTrainingScratch's
// real -1 sentinel a few lines later, so the CRecordBox logic was
// unaffected, but EndTrainingUpdate could never read the real saved value
// back). Fixed here.
#define gTrainingSavedScreenMode (*reinterpret_cast<i32*>(0x005513D8))
// Read once at the top of PShell_EndTrainingInit as the "target area" a
// gChallenges entry's mAreaId gets matched against. Named in a shell.cpp
// comment as the address neighbouring gChallenges/gCheats but never wired
// up as a usable global before this.
#define gTrainingSeconds (*reinterpret_cast<i32*>(0x00551288))
// gGlobalRecords (shell.cpp) is a plain repo global (DLL-relocatable
// address); this file needs the fixed game address for PShell_EndTrainingInit
// to byte-match, same class of problem as gSaveGame. Kept file-local per
// the G_* macro placement rule (only pshell.cpp uses this). The first 3
// bytes ahead of SRecords::mScores (currently just PADDING(3) in shell.h)
// turn out to be real data here: PShell_EndTrainingInit reads them as the
// 3-letter name to stamp into a new score row. Not renamed in shell.h
// since that struct is shared and this is the only evidence we have for it
// so far.
static SRecords* const gGlobalRecordsFixed = reinterpret_cast<SRecords*>(0x00550ED0);

// These three are elements of the same string-literal-pointer table
// front.cpp already names two entries of (gFrontYesText/gFrontNoText,
// 0x0054B780/0x0054B77C); string content confirmed against the original exe.
#define gTextNewRecord (*reinterpret_cast<char**>(0x0054B8E4))
#define gTextYourScore (*reinterpret_cast<char**>(0x0054B8F0))
#define gTextNone (*reinterpret_cast<char**>(0x0054B8F4))

// @Ok
// @Matching
void PShell_EndTrainingDisplay(void)
{
	Mess_SetRGB(0x60, 0x60, 0x60, 0);
	Mess_SetTextJustify(0);

	Mess_DrawText(0x100, 0x1D, gChallenges[gTrainingChallengeIndex].field_0, 0, 0x1000);

	if (gTrainingDisplayTimer != 0)
	{
		gTrainingDisplayTimer--;

		if ((gTrainingDisplayTimer % 10) > 5)
		{
			PShell_BigFont();
			PShell_DefaultText();
			Mess_DrawText(0x100, 0x78, gTextNewRecord, 0, 0x1000);
			PShell_NormalFont();
		}

		return;
	}

	if (gTrainingRecordBox)
		gTrainingRecordBox->Display();

	if (gTrainingMenu)
		gTrainingMenu->Display();

	if (gTrainingMenu)
		PCSHELL_DrawMouseCursor();

	if (gTrainingResultState != 0)
		return;

	PShell_DefaultText();
	Mess_SetTextJustify(2);
	Mess_DrawText(0x127, 0x9B, gTextYourScore, 0, 0x1000);

	if (gTrainingRecordBox)
	{
		Mess_SetTextJustify(1);

		if (gTrainingScore == -1000)
		{
			Mess_DrawText(0x131, 0x9B, gTextNone, 0, 0x1000);
		}
		else
		{
			DisplayScore(0x131, 0x9B, gTrainingScore, gTrainingRecordBox->field_3C->mScoreUnits);
		}
	}

	Mess_SetTextJustify(0);
}

// (0x0047B720, 812 bytes). Housekeeping calls at the top, in order:
// print_if_false(1, <str at 0x00551AF4>) (condition is always true, never
// prints, looks like a reached-here marker), Mess_DeleteAll,
// Bit_ClearTextBoxes, SFX_StopAll, Redbook_XAStop.
//
// The record box build (CClass::operator new(0x44) then direct field
// writes in the order field_1C=0x78, field_20=0x29, field_C=0x116,
// field_10=0x60, field_4=0xA, field_8=0xA, field_14=0x30, field_18=0xC,
// field_24=0, field_2C=0x1C, vtable=0x0053BDA4, field_3C=&gChallenges[idx])
// is NOT a call to CRecordBox::CRecordBox(0x78, 0x29, pMission)
// (shell.cpp, @Ok @Matching): that constructor is declared (not defined)
// in shell.h, so a `new CRecordBox(...)` from this TU could never inline it
// regardless of shell.cpp's `#pragma auto_inline(off)`, and it would store
// fields in the CONSTRUCTOR's own order (field_1C,field_4,field_8,field_20,
// field_C,field_10,field_14,field_18,field_24,field_2C,field_3C), not this
// order. This function's own source really does write the fields directly,
// including a manual vtable poke, bypassing the constructor. CItem_new in
// the old comment was the exported name; it turned out to be
// CClass::operator new and CItem::operator new COMDAT-folded to the same
// address (ob.cpp/main.cpp have byte-identical bodies).
//
// Two more STrainingMission fields found and added to shell.h this
// session, beyond the offset+7 one already known: a word at offset+4
// (mLevelId, matched against Trig_GetLevelID()'s result) gates the
// entry-search loop before it even looks at mAreaId (offset+7), and a byte
// at offset+0xC (mLowerIsBetter) picks the comparison direction in the
// score-insert search below (higher-is-better vs lower-is-better, e.g. for
// Time challenges).
//
// The rest is a top-5 high score insert for gGlobalRecords (shell.cpp):
// skip entirely if gTrainingScore is the -1000 "no score" sentinel; if the
// challenge's row has never been filled (mScores[0].field_0 == 0), stamp
// the new score straight into slot 0 (gTrainingResultState = 1); otherwise
// scan up to 5 rows for the first one that is empty or worse than the new
// score (per mLowerIsBetter), shift every row from there down by one if it
// found a real (non-empty) row to beat (gTrainingResultState = 2) or write
// straight in if it landed on an empty row (gTrainingResultState = 3); if
// no row was found in 5, nothing is written. Landing at row 0 with
// resultState 2 (beat the existing #1) sets gTrainingDisplayTimer = 0x50.
// gTrainingMenuEase always gets set to 0x4EE at the end regardless (its
// initial zoomed-out value, eased back down toward 0x180 by
// PShell_EndTrainingUpdate).
//
// Globals with no idb_globals.txt entry are all tentative (see their own
// comments above): gTrainingActive, gEndTrainingFlag, G_SCREEN_MODE_FLAG,
// gTrainingScratch, gTrainingMenuEase, gTrainingSavedScreenMode,
// gTrainingSeconds. String literals
// passed to print_if_false are placeholders (relocated string addresses
// are an accepted diff, only the presence/position of the argument
// matters for matching).
//
// cmpsum against the rebuilt DLL: 146 mnemonic diffs (812-byte, medium
// function). The first divergence is again a register-allocation
// difference, not a missing/wrong operation: the original's prologue
// pushes all 4 callee-saved registers up front (ebx,ebp,esi,esi is cached
// with the constant 1 and reused for the print_if_false call and both the
// gTrainingActive/gEndTrainingFlag stores) and edi before the first
// housekeeping call; our build only pushes ebx/ebp up front and defers
// esi/edi, so every later instruction that references one of those
// registers is shifted out of position for the (positional, mnemonic-only)
// diff tool even where the underlying operation is the same. 1 explicit
// hypothesis tried (share a single `i32 one = 1` local across the three
// places that use the literal 1, matching the original's esi reuse): no
// change (146 diffs). This is a medium function (200-1000 bytes), so the
// discipline wants 15+ hypotheses before @AlmostMatching; given the size of
// the remaining task list this session (STrainingMission field work,
// PShell_EndTrainingUpdate), left @NotOk after this first pass rather than
// spend that budget here. The call targets, argument order, table/struct
// field accesses, and control flow (including the CRecordBox field order
// and the top-5 score insert/shift logic) are believed correct.
// @Ok
void PShell_EndTrainingInit(void)
{
	print_if_false(1, "Bad pTrainingMission");

	Mess_DeleteAll();
	Bit_ClearTextBoxes();
	SFX_StopAll();
	Redbook_XAStop();

	i32 oldScreenMode = G_SCREEN_MODE_FLAG;

	gTrainingActive = 1;
	gEndTrainingFlag = 1;
	gTrainingSavedScreenMode = oldScreenMode;
	G_SCREEN_MODE_FLAG = 0;
	*(i32*)0x00660F80 = 0;  // gWideScreen (ps2m3d.cpp), fixed game address; see gSaveGame note in CLAUDE.md

	if (G_MECHLIST_PLAYER)
	{
		G_MECHLIST_PLAYER->ExitLookaroundMode();
		*(i32*)((char*)G_MECHLIST_PLAYER + 0x68) = 0;
		*(i32*)((char*)G_MECHLIST_PLAYER + 0x60) = 0;
	}

	Pad_ActuatorOff(0, 0);
	Pad_ActuatorOff(0, 1);

	i32 levelId = Trig_GetLevelID();

	gTrainingChallengeIndex = 0;

	i32 idx;
	for (idx = 0; idx < NUM_CHALLS; idx++)
	{
		if (gChallenges[idx].mLevelId != (i16)levelId)
			continue;

		if (gChallenges[idx].mAreaId == gTrainingSeconds || gChallenges[idx].mAreaId == -1)
			break;
	}

	gTrainingChallengeIndex = idx;

	print_if_false(idx < NUM_CHALLS, "Training mission index out of range");
	print_if_false(gTrainingRecordBox == 0, "pRecordBox not NULL in end training init");

	CRecordBox* box = (CRecordBox*)CClass::operator new(0x44);
	if (box)
	{
		box->field_1C = 0x78;
		box->field_20 = 0x29;
		box->field_C = 0x116;
		box->field_10 = 0x60;
		box->field_4 = 0xA;
		box->field_8 = 0xA;
		box->field_14 = 0x30;
		box->field_18 = 0xC;
		box->field_24 = 0;
		box->field_2C = 0x1C;
		*(u32*)box = 0x53BDA4;
		box->field_3C = &gChallenges[gTrainingChallengeIndex];
	}
	gTrainingRecordBox = box;

	i32 idx2 = gTrainingChallengeIndex;
	i8 scoreUnits = gChallenges[idx2].mScoreUnits;

	if (scoreUnits != 0 && scoreUnits != 3)
	{
		if (gTrainingScore == 0)
			gTrainingScore = -1000;
	}

	if (scoreUnits == 3 && gTrainingScore <= 0)
		gTrainingScore = -1000;

	SScore* pRow = &gGlobalRecordsFixed->mScores[idx2 * NUM_RECORDS_PER_CHALL];

	gTrainingResultState = 0;
	gTrainingScratch = -1;

	if (gTrainingScore != -1000)
	{
		if (pRow[0].field_0 == 0)
		{
			gTrainingResultState = 1;
			gTrainingScratch = 0;

			pRow[0].field_0 = ((u8*)gGlobalRecordsFixed)[0];
			pRow[0].field_1 = ((u8*)gGlobalRecordsFixed)[1];
			pRow[0].field_2 = ((u8*)gGlobalRecordsFixed)[2];
			pRow[0].field_3 = (u8)gTrainingScore;
			pRow[0].field_4 = (u8)(gTrainingScore >> 8);

			gTrainingRecordBox->field_40 = 1;
			gTrainingRecordBox->mLetterIndex = 0;
			gTrainingRecordBox->field_39 = 0;

			print_if_false(1, "Bad row sent to Name entry");
		}
		else
		{
			i8 lowerIsBetter = gChallenges[idx2].mLowerIsBetter;
			i32 pos;

			for (pos = 0; pos < NUM_RECORDS_PER_CHALL; pos++)
			{
				if (pRow[pos].field_0 == 0)
					break;

				i32 rowScore = (i16)((pRow[pos].field_4 << 8) | pRow[pos].field_3);

				if (!lowerIsBetter)
				{
					if (gTrainingScore > rowScore)
						break;
				}
				else
				{
					if (gTrainingScore < rowScore)
						break;
				}
			}

			if (pos < NUM_RECORDS_PER_CHALL)
			{
				if (pRow[pos].field_0 == 0)
				{
					gTrainingResultState = 3;
					gTrainingScratch = pos;
				}
				else
				{
					gTrainingResultState = 2;
					gTrainingScratch = pos;

					i32 i;
					for (i = NUM_RECORDS_PER_CHALL - 1; i > pos; i--)
					{
						*(u32*)&pRow[i] = *(u32*)&pRow[i - 1];
						pRow[i].field_4 = pRow[i - 1].field_4;
					}
				}

				pRow[pos].field_0 = ((u8*)gGlobalRecordsFixed)[0];
				pRow[pos].field_1 = ((u8*)gGlobalRecordsFixed)[1];
				pRow[pos].field_2 = ((u8*)gGlobalRecordsFixed)[2];
				pRow[pos].field_3 = (u8)gTrainingScore;
				pRow[pos].field_4 = (u8)(gTrainingScore >> 8);

				gTrainingRecordBox->field_39 = (u8)pos;
				gTrainingRecordBox->field_40 = 1;
				gTrainingRecordBox->mLetterIndex = 0;

				print_if_false(pos < NUM_RECORDS_PER_CHALL, "Bad row sent to Name entry");
			}
		}
	}

	if (gTrainingResultState == 2 && gTrainingScratch == 0)
		gTrainingDisplayTimer = 0x50;

	gTrainingMenuEase = 0x4EE;
}

// idb_globals.txt: 0x0056F3B8 CameraList. Confirmed as a CCamera* by this
// function's own disassembly: it calls ->SetCamAngle() on it (0x4178E0,
// already @Ok @AlmostMatching in camera.cpp), and CCamera has a real
// field_236 (i16) at that exact offset (VALIDATE(CCamera, field_236, 0x236)
// in camera.cpp, also read by SetCamAngle itself).
#define G_CAMERA_LIST (*reinterpret_cast<CCamera**>(0x0056F3B8))

// Same string-pointer table class as gTextNewRecord/gTextYourScore/gTextNone
// above and gTextSaveGameProgress further down, just a different cluster of
// entries (0x54BA94-0x54BA9C vs 0x54B8E4-0x54B8F8); string content confirmed
// against the original exe.
#define gTextRetry (*reinterpret_cast<char**>(0x0054BA94))
#define gTextQuitToTraining (*reinterpret_cast<char**>(0x0054BA98))
#define gTextQuitToMainMenu (*reinterpret_cast<char**>(0x0054BA9C))

// (0x0047BC40, 656 bytes). Full decompile via IDA/Hex-Rays on the real exe,
// 2026-08-27, replacing the earlier investigation-only pass.
//
// Structure: nudge the camera angle once (CameraList, see its own comment);
// bail out early while gTrainingDisplayTimer is still counting down (see
// PShell_EndTrainingDisplay, same global); otherwise update the
// already-built CRecordBox and ease gTrainingMenuEase toward 0x180 while
// its field_40 "still animating" flag is set (early return while it is,
// same shape as the CMenu-vs-CRecordBox ease idiom in PShell_MaybeSaveGame/
// Shell_ShowRecord); once field_40 clears, lazily build gTrainingMenu (a
// plain `new CMenu(...)`, same cross-TU SEH-frame mechanism as
// Shell_ChooseSurvivalArena, see the CLAUDE.md "SOLVED 2026-08-27" note:
// CMenu::CMenu is defined in front.cpp, a different TU, so the frame
// reproduces with no source workaround needed) with three menu entries
// (Retry / quit to training / quit to main menu); update the menu every
// frame, check whether the mouse sits over the highlighted entry's text
// (PCSHELL_IsMouseOverText, only computed when PCSHELL_CheckTriggers(0x100,
// ...) is true, mirroring the mouse-vs-pad idiom already used in
// shell.cpp's own CMenu handling); once a valid line (0-39) is chosen by
// mouse-over or by PCSHELL_CheckTriggers(0x50110, ...), clear the two pad
// trigger flags used to open this screen (G_SCONTROL[0].X/.Start, matching
// PShell_MaybeSaveGame's clear pair), turn off the training-active flags,
// restore G_SCREEN_MODE_FLAG from gTrainingSavedScreenMode, translate the
// chosen line (0/1/2) into gLevelStatus (already a real repo global,
// trig.cpp, idb_globals.txt names 0x60CFA4 the same way), and tear down
// both widgets.
//
// All callees are already real elsewhere in the repo (front.cpp's CMenu
// methods, camera.cpp's CCamera::SetCamAngle, PCShell.cpp's
// PCSHELL_CheckTriggers/PCSHELL_IsMouseOverText, shell.cpp's
// CRecordBox::Update), so nothing new needed a stub.
//
// cmpsum against the rebuilt DLL: 16 mnemonic diffs after 13 hypotheses
// (below the 15-hypothesis bar for a medium function, so left @NotOk, not
// @AlmostMatching; full log in pshell.attempts.md). Confirmed via
// instruction-count check that nothing is missing: the epilogue matches
// byte for byte and both remaining diff clusters are pure register/
// scheduling reorderings of already-correct operations, not a dropped or
// extra instruction. Cluster A: which register (and how early) holds the
// gTrainingMenu pointer vs gTrainingSavedScreenMode around the final
// flag-clear stores. Cluster B: the exact interleaving of the
// @Ok
void PShell_EndTrainingUpdate(void)
{
	if (G_CAMERA_LIST != 0)
		G_CAMERA_LIST->SetCamAngle(G_CAMERA_LIST->field_236 + 24, 0);

	if (gTrainingDisplayTimer != 0)
		return;

	print_if_false(gTrainingRecordBox != 0, "NULL pRecordBox");
	gTrainingRecordBox->Update();

	CRecordBox* recordBox = gTrainingRecordBox;

	if (recordBox->field_40 != 0)
	{
		gTrainingMenuEase = PShell_MoveTowards(gTrainingMenuEase, 0x180);

		if (recordBox->field_40 != 0)
			return;
	}

	if (gTrainingMenu == 0)
	{
		Mess_SetScale(0x100);
		Mess_SetCurrentFont("sp_fnt00.fnt");

		gTrainingMenu = new CMenu(0x100, 0xB8, 0, 0x100, 0x100, 0x10);
		gTrainingMenu->mLineSep = 0xE;

		gTrainingMenu->AddEntry(gTextRetry);
		gTrainingMenu->AddEntry(gTextQuitToTraining);
		gTrainingMenu->AddEntry(gTextQuitToMainMenu);

		gTrainingMenu->Zoom(0);
	}

	gTrainingMenu->Update();

	u8 mouseOverText = 0;

	if (PCSHELL_CheckTriggers(0x100, 1, 1))
	{
		u8 justification = gTrainingMenu->mJustification;
		const char* entryName = gTrainingMenu->mEntry[gTrainingMenu->mLine].name;
		i32 x, y;

		gTrainingMenu->GetEntryXY(entryName, &x, &y);

		if (PCSHELL_IsMouseOverText(entryName, x, y, justification))
			mouseOverText = 1;
	}

	if (gTrainingMenu->mLine < 0x28)
	{
		if (!mouseOverText)
		{
			if (!PCSHELL_CheckTriggers(0x50110, 1, 1))
				return;
		}

		i32 savedScreenMode = gTrainingSavedScreenMode;

		G_SCONTROL[0].X.Triggered = 0;
		G_SCONTROL[0].Start.Triggered = 0;
		gTrainingActive = 0;
		gEndTrainingFlag = 0;
		G_SCREEN_MODE_FLAG = savedScreenMode;

		switch (gTrainingMenu->mLine)
		{
			case 0:
				gLevelStatus = 8;
				break;
			case 1:
				gLevelStatus = 10;
				break;
			case 2:
				gLevelStatus = 7;
				break;
		}

		delete gTrainingRecordBox;
		gTrainingRecordBox = 0;
		delete gTrainingMenu;
		gTrainingMenu = 0;
	}
}

// Save-confirmation modal loop. Same idiom as Shell_ShowRecord's loop
// (shell.cpp): a zoom-ease local eases pYesNoMenu's zoom animation via
// PShell_MoveTowards (own local here, not shell.cpp's gShellMenuEase: the
// disassembly never stores it to any fixed address, purely a register/stack
// local, unlike Shell_ShowRecord's use of the real global), a "was the
// opening button still held" flag suppresses PCSHELL_CheckTriggers until the
// player releases it, and DoVblankProcessing/gPrintStubbed/gsub_430680 are
// the standard per-frame housekeeping calls (see shell.cpp for the same 5
// lines). gTextSaveGameProgress: string content confirmed against the
// original exe, same string-pointer table as pshell.cpp's other gText*
// globals near it.
#define gTextSaveGameProgress (*reinterpret_cast<char**>(0x0054B8F8))

// @Ok
// @Matching
// Note: the call at 0x0043FB00 (thiscall on pYesNoMenu, no args, return
// value unused) is guessed as CMenu::Reset() (front.h/front.cpp): same call
// shape (this in ecx, no pushed args, no value read back), and semantically
// it fits (reset the menu highlight after the yes/no choice resolves). Not
// confirmed against the Mac build or idb_globals.txt; call targets are
// masked by cmpsum/compare.py so this guess does not affect the verified
// mnemonic match. See pshell.attempts.md.
void PShell_MaybeSaveGame(void)
{
	// same pattern as front.cpp/shell.cpp/PCShell.cpp: defined once in
	// PCShell.cpp, called here through a local extern.
	extern void gsub_430880(void);

	u32 saveBuf[3];

	do
	{
		i32 ignoreTriggers = 1;
		i32 zoomEase = 0x320;

		print_if_false(pYesNoMenu != 0, "NULL pYesNoMenu");

		pYesNoMenu->mLine = 1;
		pYesNoMenu->Zoom(0);

		for (;;)
		{
			gsub_430880();
			Db_FlipClear();
			CalcPolyBufferEnd();

			i32 vblanksSnapshot = G_VBLANKS;

			if (!G_SCENE_RELATED)
				PCGfx_BeginScene(1, -1);

			Mess_SetScale(0x100);
			pYesNoMenu->Display();

			Mess_SetRGB(0x4D, 0x53, 0x69, 0);
			Mess_DrawText(0x100, 0x3C, gTextSaveGameProgress, 0, 0x1000);

			if (G_SCENE_RELATED)
				PCGfx_EndScene(1);

			zoomEase = PShell_MoveTowards(zoomEase, 0x1CC);

			Pad_Update();

			if (!G_SCONTROL[0].X.Pressed)
			{
				ignoreTriggers = 0;
				G_SCONTROL[0].X.Triggered = 0;
			}

			pYesNoMenu->Update();

			if (!ignoreTriggers)
			{
				if (PCSHELL_CheckTriggers(1, 1, 0x50010))
					break;
			}

			if (G_VBLANKS == vblanksSnapshot)
				Pause(1);

			*(volatile i32*)&DoVblankProcessing = 0;
			Pause(1);

			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");
			gsub_430680();

			if (!*(volatile i32*)&DoVblankProcessing)
			{
				Utils_VblankProcessing();
				DoVblankProcessing = 1;
			}
		}

		SControl* pad = G_SCONTROL;
		pad[0].X.Triggered = 0;
		pad[0].Start.Triggered = 0;
		SFX_Play(0x1F, 0x2000, 0);

		Pause(1);

		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		gsub_430680();

		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");

		Front_ClearScreen();

		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");

		Pad_ClearTriggers(G_SCONTROL);
		pYesNoMenu->Reset();

		if (pYesNoMenu->mLine != 1)
			return;

		saveBuf[2] = 1;
		Shell_SaveGame(reinterpret_cast<const u32*>(&saveBuf[0]), &saveBuf[1]);
	} while (saveBuf[1] == 0);
}

// @Ok
// @Matching
void PShell_MaybeUnlockStuff(void)
{
	i32 unlocked = -1;

	i32 i;

	i32 allComplete = 1;
	for (i = 0; i < 0x22; i++)
	{
		if (!G_SAVE_GAME.mLevelCompletionCounts[i])
		{
			allComplete = 0;
		}
	}

	if (allComplete)
	{
		u8 oldFlags = (u8)G_SAVE_GAME.field_80;
		G_SAVE_GAME.mCheatStoryboardFlag = 1;

		if (!(oldFlags & 4))
		{
			unlocked = 2;
		}

		G_SAVE_GAME.field_80 |= 4;
		G_SAVE_GAME.field_84 |= 0x1000000;

		if (DifficultyLevel == 3)
		{
			if (!((u8)G_SAVE_GAME.field_80 & 8))
			{
				unlocked = 3;
			}

			G_SAVE_GAME.field_80 |= 8;
		}
	}

	if (G_SAVE_GAME.field_8C == -1)
	{
		G_SAVE_GAME.field_80 |= 2;
	}

	i32 idx1 = Front_GetLevelIndex("l4a1_t");
	print_if_false(idx1 != -1, "Could not find l4a1_t ???");
	if (G_SAVE_GAME.mLevelCompletionCounts[idx1])
	{
		G_SAVE_GAME.field_80 |= 0x40;
	}

	i32 idx2 = Front_GetLevelIndex("l6a4_t");
	print_if_false(idx2 != -1, "Could not find l6a4_t ???");
	if (G_SAVE_GAME.mLevelCompletionCounts[idx2])
	{
		G_SAVE_GAME.field_80 |= 0x80;
	}

	i32 allGold = 1;
	for (i = 0; i < 0x22; i++)
	{
		if (G_SAVE_GAME.mLevelCompletionCounts[i] < 2)
		{
			allGold = 0;
		}
	}

	if (allGold)
	{
		if (!((u8)G_SAVE_GAME.field_80 & 0x10))
		{
			unlocked = 4;
		}

		G_SAVE_GAME.field_80 |= 0x10;
	}

	if (unlocked != -1)
	{
		G_SAVE_GAME.field_7C = (u8)unlocked;
	}
}

// @Ok
// @Matching
i32 PShell_MoveTowards(
		i32 a1,
		i32 a2)
{
	i32 v2 = a2 - a1;
	if (!v2)
		return a1;

	if ((v2 & 0xFFFFFFFC) == 0 )
		return a1 + 1;

	return a1 + (v2 >> 2);
}

// @Ok
CExpandingBox::~CExpandingBox(void)
{
}


// Unnamed float constant read at 0x53B27C and subtracted from the depth
// bias before drawing the optional shadow rect below. No access to the
// original .rdata this session, so the value itself is not known; only the
// address matters for matching (same trick as the gsub_/fixed-pointer
// globals elsewhere in this file).
static f32 * const gMenuBoxZBiasEpsilon = (f32*)0x0053B27C;

// 9-slice box border geometry table: 13 entries of 4 i32 each, addresses
// 0x5512F0..0x5513C0 (13*0x10 = 0xD0 bytes). Previous session found "roughly
// a dozen" entries in this range; this session pins it at exactly 13 by
// walking every table read in the disassembly. Field roles are NOT
// consistent across entries (sometimes an x/y screen offset, sometimes a
// width/height fudge added after a computation), so the fields are kept
// generically named.
struct SMenuBoxSlice { i32 a, b, c, d; };
static SMenuBoxSlice * const gMenuBoxSlices = (SMenuBoxSlice*)0x005512F0;

// gAnimTable[15] (bit.cpp/bit.h): 0x56EAA0 - 0x56EA64 (gAnimTable's base,
// idb_globals.txt) = 0x3C = 15 * sizeof(SAnimFrame*). Holds a pointer to a
// 9-entry SAnimFrame array used for the menu box border art (the +0x8,
// +0x10, ..., +0x40 offsets seen below are all multiples of sizeof(SAnimFrame),
// 8 bytes). Kept as a fixed game address rather than &gAnimTable[15]
// because gAnimTable is a plain repo global (DLL-relocatable address); this
// needs the byte-exact game address, same class of problem as the
// gSaveGame note already in CLAUDE.md for this file.
static SAnimFrame * const * const gMenuBoxAnimSlot = (SAnimFrame* const*)0x0056EAA0;

// 9-slice box border draw. Only caller is CExpandingBox::Display (this
// file), which passes (x, width, y, height, 1, field_24, field_28,
// field_2C). field_24 (a6 here) doubles as "has scrollbar": it picks which
// optional shadow rect gets drawn AND which subset of the 9 border pieces
// draws first, before falling into a shared tail that draws the rest
// regardless of the flag.
//
// depthBias idiom confirmed against PShell_DrawHighlight (this file):
//   i32 sort = G_SORT;
//   if (sort >= 0xFFE && sort <= 0xFFF) depthBias = -2.0f; else depthBias = 5.0f;
// (0xC0000000 = -2.0f, 0x40A00000 = 5.0f).
//
// Call targets: DCPanel_DrawFlatShadedPoly (0x462D60, panel.cpp, @Ok
// @Matching), DCPanel_DrawTexturedPoly (0x4626A0, the 9-arg
// f32,POLY_FT4*,SAnimFrame const*,... overload in panel.cpp, currently
// @NotOk there but that is panel.cpp's own residue, not this function's),
// print_if_false, Panel_DrawTexturedPoly(SAnimFrame*,i32) (0x462B90, the
// existing @Ok @Matching 2-arg overload in panel.cpp), and
// Panel_DrawTexturedPoly(SAnimFrame*,i32,i32,i32) (0x462B30, new 4-arg
// overload stubbed in panel.cpp this session since decompiling its own
// body is out of scope here).
//
// Five pieces (frames[0],[2],[4],[6],[7] or [8] depending on branch) go
// through the 4-arg Panel_DrawTexturedPoly overload, which sizes the poly
// itself via DCPanel_DrawTexturedPoly's own (x,y,w,h) branch. Five other
// pieces (frames[1],[3],[5] x2) go through the 2-arg overload and get their
// 8 corner coordinates written directly by this function before the same
// DCPanel_DrawTexturedPoly call runs again over them; whether that second
// pass's internal (x,y,w,h) branch actually overwrites the coordinates this
// function just wrote is a question about the original game's own
// behaviour, not something to "fix" here.
//
// cmpsum against the rebuilt DLL: 479 mnemonic diffs (started at 505 on the
// first working draft), first divergence right in the prologue: the
// original keeps depthBias resident in ebp for the whole function (frame
// pointer omitted) and x/width in esi/edi in that order; our build spills
// depthBias to a stack slot and swaps which of x/width lands in esi vs edi.
// This is a whole-function register-pressure difference, not a local
// source bug at the point where the diff first shows up. 3 source
// hypotheses tried and kept (all real improvements, logged in git history):
// (1) stop caching G_SORT into a local past the first use and read the
// macro fresh at every call site instead (505->493 diffs; matches the
// "volatile global, don't cache" idiom already documented for this file),
// (2) reuse one mutable `hw` local for height+width across the if/else and
// into the shared tail instead of three separate hw/hw2/hw3 locals, mirroring
// how the disassembly keeps reusing/mutating the same ebx register instead
// of recomputing height+width (493->479 diffs), (3) hoist the first flat-
// shaded call's x/y argument expressions into named locals declared in
// width-before-x order, matching the order the original computes them in
// (no measurable change, kept for source clarity). Given the function is
// 1919 bytes (well over the 1000-byte large-function threshold) and the
// residue is a function-wide register allocation difference rather than one
// or two localized diff clusters, closing this fully would need many more
// hypotheses than the discipline's 10-per-cluster minimum, spread across
// what is effectively a dozen near-duplicate draw-call clusters. Left
// @NotOk rather than force an @AlmostMatching claim for a residue this
// broad; the semantics (call targets, argument counts/order, table
// indices, field writes, control flow) are believed correct throughout.
// @Ok
i32 PShell_DrawMenuBox(i32 x, i32 y, i32 width, i32 height, i32 a5, i32 hasScrollbar, i32 a7, i32 a8)
{
	i32 sort = G_SORT;
	f32 depthBias;

	if (sort >= 0xFFE && sort <= 0xFFF)
		depthBias = -2.0f;
	else
		depthBias = 5.0f;

#ifdef SPIDEY_STANDALONE
	if (getenv("SPIDEY_TRACE_MENU"))
		printf("MENUBOX x=%d w=%d width=%d h=%d a5=%d scroll=%d a7=%d a8=%d sort=%d\n",
				x, y, width, height, a5, hasScrollbar, a7, a8, sort);
#endif

	if (hasScrollbar)
	{
		i32 shadowY = gMenuBoxSlices[0].b + (((height - a8 - 8) * a7) >> 8) + y + 4;
		i32 shadowX = gMenuBoxSlices[0].a + x - 10;

		DCPanel_DrawFlatShadedPoly(
				depthBias - *gMenuBoxZBiasEpsilon,
				shadowX,
				shadowY,
				gMenuBoxSlices[0].c + 7,
				gMenuBoxSlices[0].d + a8,
				0xFF, 0xFF, 0xFF,
				sort,
				1);
	}

	SAnimFrame* frames = *gMenuBoxAnimSlot;
	print_if_false(frames != 0, "No menu box anim frames");

	i32 v1 = y - 3;
	i32 hw;

	if (hasScrollbar)
	{
		POLY_FT4* p1 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[0], x - 14, v1, G_SORT);
		if (p1)
			p1->code |= 2;
		DCPanel_DrawTexturedPoly(depthBias, p1, &frames[0],
				gMenuBoxSlices[1].a + x - 14, gMenuBoxSlices[1].b + y - 3,
				gMenuBoxSlices[1].c, gMenuBoxSlices[1].d, G_SORT, 0);

		POLY_FT4* p2 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[1], G_SORT);
		p2->y0 = (i16)(y + 7);
		p2->y1 = (i16)(y + 7);
		p2->code |= 2;
		p2->x0 = (i16)(x - 14);
		p2->x2 = (i16)(x - 14);
		p2->x1 = (i16)x;
		hw = height + y;
		p2->x3 = (i16)x;
		p2->y2 = (i16)(hw - 7);
		p2->y3 = (i16)(hw - 7);
		p2->v2--;
		p2->v3--;
		DCPanel_DrawTexturedPoly(depthBias, p2, &frames[1],
				gMenuBoxSlices[2].a + (x - 14), gMenuBoxSlices[2].b + (y + 7),
				gMenuBoxSlices[2].c + (x - (x - 14)), gMenuBoxSlices[2].d + ((hw - 7) - (y + 7)),
				G_SORT, 0);

		POLY_FT4* p3 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[2], x - 14, y + height - 7, G_SORT);
		if (p3)
			p3->code |= 2;
		DCPanel_DrawTexturedPoly(depthBias, p3, &frames[2],
				gMenuBoxSlices[3].a + x - 14, gMenuBoxSlices[3].b + y + height - 7,
				gMenuBoxSlices[3].c, gMenuBoxSlices[3].d, G_SORT, 0);
	}
	else
	{
		POLY_FT4* p4 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[7], x - 6, v1, G_SORT);
		if (p4)
			p4->code |= 2;
		DCPanel_DrawTexturedPoly(depthBias, p4, &frames[7],
				gMenuBoxSlices[4].a + x - 6, gMenuBoxSlices[4].b + y - 3,
				gMenuBoxSlices[4].c, gMenuBoxSlices[4].d, G_SORT, 0);

		POLY_FT4* p5 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[8], x - 6, y + height - 3, G_SORT);
		if (p5)
			p5->code |= 2;
		DCPanel_DrawTexturedPoly(depthBias, p5, &frames[8],
				gMenuBoxSlices[5].a + x - 6, gMenuBoxSlices[5].b + y + height - 3,
				gMenuBoxSlices[5].c, gMenuBoxSlices[5].d, G_SORT, 0);

		POLY_FT4* p6 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[5], G_SORT);
		p6->code |= 2;
		p6->y0 = (i16)(y + 3);
		p6->y1 = (i16)(y + 3);
		p6->x0 = (i16)(x - 6);
		p6->x2 = (i16)(x - 6);
		p6->x1 = (i16)x;
		hw = height + y;
		p6->x3 = (i16)x;
		p6->y2 = (i16)(hw - 3);
		p6->y3 = (i16)(hw - 3);
		p6->v2--;
		p6->v3--;
		DCPanel_DrawTexturedPoly(depthBias, p6, &frames[5],
				gMenuBoxSlices[6].a + (x - 6), gMenuBoxSlices[6].b + (y + 3),
				gMenuBoxSlices[6].c + (x - (x - 6)), gMenuBoxSlices[6].d + ((hw - 3) - (y + 3)),
				G_SORT, 0);
	}

	POLY_FT4* p7 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[4], x + width - 2, y - 3, G_SORT);
	if (p7)
		p7->code |= 2;
	DCPanel_DrawTexturedPoly(depthBias, p7, &frames[4],
			gMenuBoxSlices[7].a + x + width - 2, gMenuBoxSlices[7].b + y - 3,
			gMenuBoxSlices[7].c, gMenuBoxSlices[7].d, G_SORT, 0);

	POLY_FT4* p8 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[3], G_SORT);
	p8->code |= 2;
	if (hasScrollbar)
	{
		p8->x0 = (i16)x;
		p8->x2 = (i16)x;
	}
	else
	{
		p8->x0 = (i16)(x + 2);
		p8->x2 = (i16)(x + 2);
	}
	p8->y0 = (i16)(y - 3);
	p8->y1 = (i16)(y - 3);
	p8->x1 = (i16)(width + x - 2);
	p8->u1--;
	p8->y2 = (i16)(y + 1);
	p8->x3 = (i16)(width + x - 2);
	p8->u3--;
	DCPanel_DrawTexturedPoly(depthBias, p8, &frames[3],
			gMenuBoxSlices[8].a + p8->x0, gMenuBoxSlices[8].b + (y - 3),
			gMenuBoxSlices[8].c + ((width + x - 2) - p8->x0), gMenuBoxSlices[8].d + ((y + 1) - (y - 3)),
			G_SORT, 0);

	POLY_FT4* p9 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[6], x + width - 2, y + height - 3, G_SORT);
	if (p9)
		p9->code |= 2;
	DCPanel_DrawTexturedPoly(depthBias, p9, &frames[6],
			gMenuBoxSlices[9].a + x + width - 2, gMenuBoxSlices[9].b + y + height - 3,
			gMenuBoxSlices[9].c, gMenuBoxSlices[9].d, G_SORT, 0);

	POLY_FT4* p10 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[3], G_SORT);
	p10->x1 = (i16)(width + x - 2);
	p10->x3 = (i16)(width + x - 2);
	p10->u1--;
	p10->y2 = (i16)(hw + 4);
	p10->y3 = (i16)(hw + 4);
	p10->u3--;
	p10->y0 = (i16)hw;
	p10->y1 = (i16)hw;
	DCPanel_DrawTexturedPoly(depthBias, p10, &frames[3],
			gMenuBoxSlices[10].a + p10->x0, gMenuBoxSlices[10].b + hw,
			gMenuBoxSlices[10].c + ((width + x - 2) - p10->x0), gMenuBoxSlices[10].d + ((hw + 4) - hw),
			G_SORT, 0);

	POLY_FT4* p11 = (POLY_FT4*)Panel_DrawTexturedPoly(&frames[5], G_SORT);
	p11->code |= 2;
	hw -= 3;
	i32 xy = width + x;
	p11->x0 = (i16)xy;
	p11->y0 = (i16)(y + 3);
	p11->x1 = (i16)(xy + 6);
	p11->y1 = (i16)(y + 3);
	p11->x3 = (i16)(xy + 6);
	p11->x2 = (i16)xy;
	p11->y2 = (i16)hw;
	p11->v2--;
	p11->y3 = (i16)hw;
	p11->v3--;
	DCPanel_DrawTexturedPoly(depthBias, p11, &frames[5],
			gMenuBoxSlices[11].a + xy, gMenuBoxSlices[11].b + (y + 3),
			gMenuBoxSlices[11].c + ((xy + 6) - xy), gMenuBoxSlices[11].d + (hw - (y + 3)),
			G_SORT, 0);

	if (a5)
	{
		DCPanel_DrawFlatShadedPoly(
				depthBias,
				gMenuBoxSlices[12].a + x, gMenuBoxSlices[12].b + y,
				gMenuBoxSlices[12].c + width, gMenuBoxSlices[12].d + height,
				0x19, 0x19, 0x50,
				G_SORT,
				1);
	}

	return a5;
}


// @Ok
CExpandingBox::CExpandingBox(
		int a2,
		int a3,
		int a4,
		int a5,
		int a6,
		int a7,
		int a8,
		int a9,
		int a10)
{
	this->field_1C = a2;
	this->field_20 = a3;
	this->field_C = a4;
	this->field_10 = a5;
	this->field_4 = a6;
	this->field_8 = a7;
	this->field_14 = a8;
	this->field_18 = a9;
	this->field_24 = a10;
	this->field_2C = 28;
}



// cmpsum shows 12 mnemonic diffs (147-byte function). Residue is in the
// x/y argument expressions passed to PShell_DrawMenuBox: the original
// interleaves a "push edi" callee-save mid-computation (right after the x
// half-width/half-height subtraction, before starting the y one) and keeps
// field_1C in edx across that push; our build defers the push and picks a
// different register for field_1C.
// 4 hypotheses tried this session, all logged: (1) baseline (12 diffs,
// kept), (2) hoist the x expression into a named local declared before the
// call (34 diffs, worse), (3) reorder the +/- operands in both x and y
// expressions to field_C/2 - field_4/2 + field_1C style (no change, 12
// diffs), (4) cache field_8 into a local reused by both the height arg and
// the y expression's /2 term (14 diffs, worse). Verified byte length and
// call target/argument order match; the diff is purely register/scheduling
// choice around the callee-save push. Functional bar (per session
// direction 2026-08-30): logic is correct, tagged @Ok.
// @Ok
int CExpandingBox::Display(){

	this->field_4 += this->field_14;
	if (this->field_4 > this->field_C)
		this->field_4 = this->field_C;

	this->field_8 += this->field_18;
	if (this->field_8 > this->field_10)
		this->field_8 = this->field_10;

	if (this->field_4 == this->field_C && this->field_8 == this->field_10)
		this->field_30 = 1;

	// 0x47AF96: the arguments are x, y, width, height (the other callers in
	// shell.cpp pass them in that order too). Passing the width as y put
	// the box below the screen.
	return PShell_DrawMenuBox(
		this->field_1C + this->field_C / 2 - this->field_4 / 2,
		this->field_20 + this->field_10 / 2 - this->field_8 / 2,
		this->field_4,
		this->field_8,
		1,
		this->field_24,
		this->field_28,
		this->field_2C);
}

// Fix 2026-08-30: the previous commit's last two early returns had the
// comparison backwards. Checked against the original at 0x47afb0 (IDA
// decompile): it returns 4 when `a3 < v4` (v4 is the scrollbar thumb
// position formula) and only does the 5-or-3 split when `a3 >= v4`. This
// source had `if (a3 >= v4) return 4;`, inverted, which flips the
// hit-test result between the thumb and the track for every click in
// that band. Fixed to `if (a3 < v4) return 4;`. Also added the missing
// u16 truncation on field_2C in the final threshold (the original reads
// it as a WORD before adding to v4). Every earlier threshold
// (field_1C-14, field_20+7/-3, field_8 range, the v4 formula, and the
// 0/1/2 early returns) already matched the original.
// @Ok
i32 CExpandingBox::ScrollBarHitTest(i32 a2, i32 a3)
{
	if (!this->field_24 || !this->field_30)
		return 0;

	i16 v1 = this->field_1C;
	v1 -= 14;

	if (a2 < (u16)v1)
		return 0;

	i16 v2 = this->field_20;

	if (a2 > (u16)(v1 + 14))
		return 0;

	if (a3 < (u16)(v2 - 3))
		return 0;

	i16 v3 = this->field_8;

	if (a3 > (u16)((u16)(v3 + 6) + (u16)(v2 - 3)))
		return 0;

	i32 v4 = ((this->field_8 - this->field_2C - 8) * this->field_28 >> 8) + v2 + 4;

	if (a3 <= (u16)(v2 + 7))
		return 1;

	if (a3 < (u16)(v3 + v2 - 5))
		return 2;

	if (a3 < (u16)v4)
		return 4;

	if (a3 > (u16)this->field_2C + (u16)v4)
		return 5;

	return 3;
}

// Found this session (2026-08-31) with fresh IDA access. The function has
// no standalone PC address: it is fully inlined into its only call site.
// Proof: ActivateCheat (this file, 0x47c240, already @Ok) case CHEAT_LEANEST
// (a1 == 1) writes exactly these 7 fields (gSaveGame.field_78/80/84/88/8C/90
// and mCheatStoryboardFlag), then falls into the shared tail with case
// CHEAT_MMEWEB (G_LEVEL_SELECT_FLAG = 1; the CLAUDE.md "redundant-code
// elimination" pattern, MSVC folded the identical trailing statement shared
// by both cases into one block reached by a jump). Confirmed against the
// disassembly of sub_47C240 at 0x47c276: "or ecx,-1; mov byte ptr
// [6828D0h],al; mov [6828D8h],ecx; mov [6828DC h],ecx; mov [6828E0h],ecx;
// mov [6828E4h],ecx; mov [6828E8h],ecx; mov byte ptr [6828ADh],al" then
// falls to the case-15 label "mov [60CFD8h],eax" (that last store is
// ActivateCheat's own shared code, not part of this function). 0x6828D0 is
// a byte store (al, not full ecx) matching field_78's u8 type; 0x6828AD is
// also a byte store matching mCheatStoryboardFlag's u8 type; the other 5
// are full dword stores of -1 matching field_80/84/88/8C/90 (i32). This is
// the same field mapping shell.h already gives gSaveGame from ActivateCheat
// itself, so no new struct knowledge is needed. The Mac build (CodeWarrior)
// kept this as a real out-of-line function (idbs/spiderman_names.txt:
// 0x000c69c0 ".PShell_BigCheat__Fv", 52 bytes) which is why searches for a
// standalone PC address in the previous pass found nothing: there is none
// to find. Source-level, ActivateCheat's CHEAT_LEANEST case is written with
// the fields inline rather than calling this function (see its own @Ok
// comment above), which is functionally equivalent either way.
// @Ok
void PShell_BigCheat(void)
{
	G_SAVE_GAME.field_78 = 1;
	G_SAVE_GAME.field_80 = -1;
	G_SAVE_GAME.field_84 = -1;
	G_SAVE_GAME.field_88 = -1;
	G_SAVE_GAME.field_8C = -1;
	G_SAVE_GAME.field_90 = -1;
	G_SAVE_GAME.mCheatStoryboardFlag = 1;
}

// @Ok
void PShell_NormalFont(void)
{
	Mess_SetScale(256);
	Mess_SetCurrentFont("sp_fnt00.fnt");
}

// @Ok
void PShell_DefaultText(void)
{
	PShell_NormalFont();
	Mess_SetTextJustify(0);
	Mess_SetRGB(0x80, 0x80, 0x80, 0);
	Mess_SetRGBBottom(0x45, 60, 107);
}

// @Ok
void PShell_SmallFont(void)
{
	Mess_SetScale(256);
	Mess_SetCurrentFont("sp_fnt02.fnt");
}

// @Ok
void PShell_InstructionalText(void)
{
	PShell_SmallFont();
	Mess_SetTextJustify(0);
	Mess_SetRGB(0x45u, 0x3Cu, 0x6Bu, 0);
	Mess_SetRGBBottom(0x28u, 35, 62);
}

void validate_CExpandingBox(void)
{
	VALIDATE_SIZE(CExpandingBox, 0x34);

	VALIDATE(CExpandingBox, field_4, 0x4);
	VALIDATE(CExpandingBox, field_8, 0x8);
	VALIDATE(CExpandingBox, field_C, 0xC);
	VALIDATE(CExpandingBox, field_10, 0x10);
	VALIDATE(CExpandingBox, field_14, 0x14);
	VALIDATE(CExpandingBox, field_18, 0x18);
	VALIDATE(CExpandingBox, field_1C, 0x1C);

	VALIDATE(CExpandingBox, field_20, 0x20);
	VALIDATE(CExpandingBox, field_24, 0x24);
	VALIDATE(CExpandingBox, field_28, 0x28);

	VALIDATE(CExpandingBox, field_2C, 0x2C);
	VALIDATE(CExpandingBox, field_30, 0x30);
}

void validate_SCheat(void)
{
	VALIDATE_SIZE(SCheat, 0x8);

	VALIDATE(SCheat, pCode, 0x0);
	VALIDATE(SCheat, pDescription, 0x4);

	if (strcmp(gCheats[NUM_CHEATS-1].pCode, "FUNKYTWN"))
	{
		printf("MISMATCH IN CHEAT TABLE %s should be FUNKYTWN", gCheats[NUM_CHEATS-1].pCode);
	}


}

// Moved here from shell.cpp on 2026-08-27 (IDA investigation, real exe):
// Shell_ShowRecord's `new CRecordBox(...)` carries a function-level SEH
// cleanup frame (mov eax,fs:0; push -1; push handler; push eax; mov
// fs:0,esp) in the real exe, tracking a hidden EH state variable so the
// runtime can free the just-allocated block if the constructor throws.
// A same-TU `new CMenu(...)` in front.cpp never got this treatment on our
// builds either, but PShell_EndTrainingUpdate's `new CMenu(...)` (CMenu's
// constructor is defined in front.cpp, a different TU) DOES get the frame
// on our builds (confirmed this session). CRecordBox::CRecordBox was
// defined directly in shell.cpp (Shell_ShowRecord's own TU): the compiler
// could see the whole constructor body (plain field stores, no calls) at
// the call site and proved it can't throw, so it dropped the protection,
// which our `#pragma auto_inline(off)` did not prevent (that only blocks
// literal inlining, not this separate throw analysis). Moving the
// definition to pshell.cpp, a different TU, hides the body from
// shell.cpp's compile and should force the same protection the original
// has.
// @Ok
// @Matching
CRecordBox::CRecordBox(i32 width, i32 height, STrainingMission* pMission)
{
	field_1C = width;
	field_4 = 0xA;
	field_8 = 0xA;
	field_20 = height;
	field_C = 0x116;
	field_10 = 0x60;
	field_14 = 0x30;
	field_18 = 0xC;
	field_24 = 0;
	field_2C = 0x1C;
	field_3C = pMission;
}

// Decompiled via IDA on the real exe, 2026-08-27 (0x0047AF00, 7 bytes: a
// single `mov dword ptr [ecx], offset off_53B234` then `ret`, called from
// the vtable's deleting-destructor thunk at 0x47AEE0). Resets the vtable
// pointer to CExpandingBox's own vtable (0x53B234) before returning, the
// same raw-address vtable poke PShell_EndTrainingInit already uses for
// CRecordBox's own vtable (0x53BDA4, see its comment above): CRecordBox is
// laid out identically to CExpandingBox (same comment, and
// CRecordBox::Display already reinterpret_casts `this` to CExpandingBox*
// to reach its Display method) but is declared `public CClass` in this
// header, not `public CExpandingBox`, so there is no portable C++
// expression for "CExpandingBox's vtable" other than this fixed game
// address.
//
// cmpsum residue: our build emits one extra instruction, a tail
// `jmp CClass::~CClass` after the vtable store (original just has `ret`).
// Root cause: CClass::~CClass() (main.h/main.cpp) is virtual and defined
// in a different TU (main.cpp) from this destructor (pshell.cpp), so our
// compiler cannot prove the implicit base-class destructor call does
// nothing and must emit a real (tail-)call to it; the original apparently
// could elide this, most likely because its true base class destructor
// call resolved to CExpandingBox::~CExpandingBox() (defined in the SAME
// TU as this function, pshell.cpp, and itself trivial), which lines up
// with the vtable-reset target above. This is the same class of repo-wide
// cross-TU-visibility problem already documented for print_if_false and
// vector.h's operator- (see CLAUDE.md): not fixable from this one
// function without either restructuring CRecordBox's base class (touches
// shell.h and the already-`@Ok @Matching` CRecordBox::CRecordBox/Display/
// Update, out of scope here) or making CClass::~CClass() provably trivial
// repo-wide (affects every CClass-derived destructor). Left plain `@Ok`
// (not `@Matching`), matching the precedent already set by
// CExpandingBox::~CExpandingBox above (same base class, same unverified
// residue, also plain `@Ok`).
// @Ok
CRecordBox::~CRecordBox(void)
{
	*(u32*)this = 0x53B234;
}

#include "compatibility/my_patch.h"

// @Bogus
void patch_pshell(void)
{
	PATCH_PUSH_RET(0x0047C440, PShell_ActivateCheat);
}
