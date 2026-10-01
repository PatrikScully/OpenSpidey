#include "shell.h"
#include "ps2m3d.h"
#include "utils.h"
#include "ps2lowsfx.h"
#include "effects.h"
#include "spool.h"
#include "panel.h"
#include "front.h"
#include "PCGfx.h"
#include "mess.h"
#include "ps2pad.h"
#include "dcshellutils.h"
#include "utils.h"
#include "PCShell.h"
#include "PCInput.h"
#include "DXsound.h"
#include "SpideyDX.h"
#include "powerup.h"
#include "pshell.h"
#include "spidey.h"
#include "ps2m3d.h"
#include "init.h"
#include "ps2redbook.h"
#include "m3dutils.h"
#include "m3dinit.h"
#include "db.h"
#include "camera.h"
#include "ps2funcs.h"
#include "tweak.h"
#include "ps2gamefmv.h"
#include "bmr.h"
#include "ps2card.h"
#include "dcmemcard.h"
#include "dcfileio.h"
#include "DXinit.h"
#include "scorpion.h"
#include "bullet.h"
#include "bit.h"
#include "bit2.h"
#include "exp.h"

#include <cstring>

#include "validate.h"

EXPORT SLight M3d_RudeSpideyLight =
{
  { { -2047, -2896, -2047 }, { 0, 0, 4096 }, { 0, 0, -4096 } },
  0,
  { { 1440, 1920, 1760 }, { 1440, 1920, 1760 }, { 1440, 1920, 1760 } },
  0,
  { 1600, 1600, 1600 }
};

EXPORT SLight M3d_SpideyCIconLight =
{
  { { -2047, -2896, -2047 }, { 0, 0, 4096 }, { 0, 0, -4096 } },

  0,
  { { 2048, 2048, 2048 }, { 2048, 2048, 2048 }, { 2048, 2048, 2048 } },
  0,

  { 2048, 2048, 2048 }
};



// @FIXME
#ifndef SPIDEY_STANDALONE
EXPORT SRecords gGlobalRecords;
#else
extern SRecords gGlobalRecords;
#endif

EXPORT i32 dword_6A7788[16];
#ifndef SPIDEY_STANDALONE
EXPORT void* gBiographies;
#else
extern void* gBiographies;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT i32 gPshellArmorRealted;
#else
extern i32 gPshellArmorRealted;
#endif

// @FIXME
#ifndef SPIDEY_STANDALONE
EXPORT SRecordRelated gChallenges[NUM_CHALLS];
#else
extern SRecordRelated gChallenges[NUM_CHALLS];
#endif

#ifndef SPIDEY_STANDALONE
EXPORT u16 OTPushback[3];
#else
extern u16 OTPushback[3];
#endif
EXPORT u8 gPShellCleanup = 1;
EXPORT i32 gShellFromGame;
#ifndef SPIDEY_STANDALONE
EXPORT i32 gShellInitialized;
#else
extern i32 gShellInitialized;
#endif


EXPORT u8 gCurrentCostume;

CBody *MiscList;

// @FIXME
#ifndef SPIDEY_STANDALONE
EXPORT SSkinGooSource gVenomSkinGooSource;
#else
extern SSkinGooSource gVenomSkinGooSource;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT SSkinGooParams gVenomSkinGooParams;
#else
extern SSkinGooParams gVenomSkinGooParams;
#endif

// @FIXME
#ifndef SPIDEY_STANDALONE
EXPORT SSkinGooSource gCarnageSkinGooSourceShell;
#else
extern SSkinGooSource gCarnageSkinGooSourceShell;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT SSkinGooParams gCarnageSkinGooParams;
#else
extern SSkinGooParams gCarnageSkinGooParams;
#endif

// @FIXME
#ifndef SPIDEY_STANDALONE
EXPORT SSkinGooSource gSuperDocOckSkinGooSource;
#else
extern SSkinGooSource gSuperDocOckSkinGooSource;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT SSkinGooParams gSuperDocOckSkinGooParams;
#else
extern SSkinGooParams gSuperDocOckSkinGooParams;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT i32 gShellMysterioRelated;
#else
extern i32 gShellMysterioRelated;
#endif
extern SPSXRegion PSXRegion[];

SAnimFrame* gBackgroundAnimFrame;

const i32 NUM_SAVE_GAME_SLOTS = 8;
EXPORT SSaveGame gSaveGameSlots[NUM_SAVE_GAME_SLOTS];

// sin/cos pair table, i16[2*n] = sin(n), i16[2*n+1] = cos(n), n = angle & 0xFFF
static i16 * const word_610C48 = (i16*)0x610C48;

// Tentative names, found in CShellMysterioHeadCircle::Move (0x492FE0). Read as
// plain scalars (no array indexing), unlike word_610C48 above, so these look
// like single current values rather than a table; guess is some kind of
// camera-relative tilt/pitch used to skew the mysterio head circle's 4 hook
// corners toward the camera. Not confirmed against the maintainer's IDB.
static i16 * const gShellCircleTiltB = (i16*)0x61460A;
static i16 * const gShellCircleTiltA = (i16*)0x614608;

// @Ok
// @Matching
void Shell_RelocatableModuleClear(void)
{
}

// @Ok
// @Matching
void Shell_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Shell_RelocatableModuleClear;
	pMod->field_C[0] = Shell_DoShell;
	pMod->field_C[1] = Shell_SaveGame;
}

// @Ok
void Shell_AddGameSlots(CMenu* pMenu)
{
	print_if_false(pMenu->mNumLines == 0, "Tried to add slots to non-empty menu");

	for (i32 i = 0; i < NUM_SAVE_GAME_SLOTS; i++)
	{
		i32 v6 = 0;

		if (gSaveGameSlots[i].mChecksum)
		{
			u32 checksum = Shell_CalculateGameChecksum(&gSaveGameSlots[i]);

			if (checksum == gSaveGameSlots[i].mChecksum)
			{
				pMenu->AddEntry(gSaveGameSlots[i].field_3F);
			}
			else
			{
				pMenu->AddEntry("read error");
				v6 = 1;
			}
		}
		else
		{
				pMenu->AddEntry("---- empty ----");
				v6 = 1;
		}

		if (v6)
		{
			pMenu->mEntry[i].unk_c = 80;
			pMenu->mEntry[i].unk_d = 16;
			pMenu->mEntry[i].unk_e = 36;
			pMenu->mEntry[i].field_11 = 40;
			pMenu->mEntry[i].field_12 = 8;

			pMenu->mEntry[i].field_13 = 0x12;
			pMenu->mEntry[i].field_14 = 0x50;

			pMenu->mEntry[i].field_15 = 0x10;
			pMenu->mEntry[i].field_16 = 0x24;

			pMenu->mEntry[i].field_17 = 0x28;
			pMenu->mEntry[i].field_18 = 8;

			pMenu->mEntry[i].field_19 = 0x12;
		}
	}
}

// @Ok
// PowerPC version implies that mSize of SSaveGame is not a field but part of the array
// i don't like it
INLINE u32 Shell_CalculateGameChecksum(SSaveGame* pSave)
{
	u32 checksum = 0;
	print_if_false(1u, "Size of SSaveGame not a multiple of 4");

	u32* fields = &reinterpret_cast<u32*>(pSave)[1];

	for (i32 i = 0; i<0x2E; i++)
	{
		if (checksum & 0x80000000)
		{
			checksum = checksum * 2 + 1;
		}
		else
		{
			checksum <<= 1;
		}

		checksum += fields[i];
	}

	return checksum | 1;
}

// @Ok
// @Note: at the end it does add esp 2 times instead of one time but it's the same
void Shell_Cheats(void)
{
	const char *pDesc = 0;
	char v3[12];

	v3[0] = 0;

	while (Shell_InputName(v3, 1, 1, pDesc))
	{
		i32 res = PShell_ActivateCheat(v3);
		if (res != -1)
		{
			SFX_Play(0x1D, 0x2000, 0);
			pDesc = gCheats[res].pDescription;
		}
		else
		{
			SFX_Play(0x1B, 0x2000, 0);
			pDesc = 0;
		}

		v3[0] = 0;
	}

	SFX_Play(0x23, 0x2000, 0);

	if (gCurrentCostume != 5)
	{
		Spidey_BagHead(4096, gCurrentCostume != 9 ? 0 : 2);
	}
	else
	{
		Spidey_BagHead(4096, 1);
	}
}

// shared per-frame ease value for the title bar shake on some Shell_ menu screens
// (ScreenAdjust, ShowRecord, ChooseSurvivalArena all use it). tentative name, no idb
// match (0x5512EC). distinct from PCShell.cpp's PCSHELL_DoDisplayOptions/
// DoControllerConfig, which use a stack local for the same easing idiom. original
// static initial value confirmed 0x200 from the exe's raw .data bytes at 0x5512EC
// (Shell_ChooseSurvivalArena reads/updates it without resetting it first, so the
// initializer is load-bearing there even though ScreenAdjust/ShowRecord both
// overwrite it before first use).
EXPORT i32 gShellMenuEase = 0x200;

// tentative name, no idb match (0x54D38C, checked right after Pad_Update() in several
// Shell_ menu loops; guessed to gate an early abort, e.g. game shutting down. nearest
// idb_globals.txt neighbour is SymBurnRegion at 0x54D388).
static u8 * const gShellMenuAbort = (u8*)0x54D38C;

// tentative name, no idb match (0x0054B764). A short table of status-line strings read by
// pointer (const char*), confirmed by reading each entry with IDA get_string against the
// original binary. Index 0/1 are the two lines CheckForPadUnplugged shows; xrefs show this
// same table is also read (at different indices) by two other, not yet decompiled, screens
// (0x440AF0, 0x441D40), which is why it holds unrelated-looking strings past index 1.
EXPORT const char *gShellStatusStrings[5] =
{
	"A controller has been removed",
	"or a VMU is being detected.",
	"Demo Play",
	"enter cheat",
	"skip to restart",
};

// Reverse engineered 2026-08-31 (CheckForPadUnplugged chain, functional decompile session).
// Address 0x48E4B0 (439 bytes, unnamed in names.json; class declared in shell.h). Builds the
// two "cweb" CQuadBit sprites and the CKnottedWeb strand joining them, then places them via
// SetWebPositions.
// @Ok
CDropDownController::CDropDownController(void)
{
	this->mTopAnchor.vx = 0;
	this->mTopAnchor.vy = 0;
	this->mTopAnchor.vz = 0;

	// raw fixed-point constants from the disasm (mPos.vy=-380.0, mPos.vz=768.0 in Q12): the
	// widget's initial spawn position, established before M3d_BuildTransform below and never
	// touched again except by AI()'s own drop animation on mPos.vy.
	this->mPos.vy = -1556480;
	this->mPos.vz = 3145728;

	this->mAngles.vz = 80;

	this->InitItem("control");

	this->mFlags |= 0x482;
	this->mpLight = &M3d_SpideyCIconLight;

	this->RunAnim(0, 0, -1);

	this->mState = 0;

	M3d_BuildTransform(this);

	SHook hook;
	hook.Part.vx = 0;
	hook.Part.vy = -1600;
	hook.Part.vz = -600;
	hook.Offset = 0;
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&this->mTopAnchor), this, &hook);

	this->mpFrame0 = new CQuadBit();
	this->mpFrame0->SetTexture("cweb", 0);
	this->mpFrame0->SetSemiTransparent();
	this->mpFrame0->mProtected = 1;

	this->mpFrame1 = new CQuadBit();
	this->mpFrame1->SetTexture("cweb", 1);
	this->mpFrame1->SetSemiTransparent();
	this->mpFrame1->mProtected = 1;

	CVector bottomAnchor;
	bottomAnchor.vx = this->mTopAnchor.vx;
	bottomAnchor.vy = this->mTopAnchor.vy + 3297280;
	bottomAnchor.vz = this->mTopAnchor.vz;

	this->mpWeb = new CKnottedWeb(this->mTopAnchor, bottomAnchor);
	this->mpWeb->mProtected = 1;

	this->SetWebPositions();
}

// @Ok
CDropDownController::~CDropDownController(void)
{
	if (this->mpFrame0)
		delete this->mpFrame0;

	if (this->mpFrame1)
		delete this->mpFrame1;

	if (this->mpWeb)
		delete this->mpWeb;
}

// address 0x48E710, name from names.json is missing; found while investigating
// CDropDownController's constructor (0x48E4B0) call graph. Places the two CQuadBit sprite
// corners on the hooked bone (offsets {-1000,-1600,-600} and {0,-1600,-600} for frame 0,
// {1100,-1600,-600} for frame 1), derives the remaining corners from CVector arithmetic
// (confirmed operator names via names.json: 0x4E7760=operator-, 0x4E77A0=operator*(int),
// 0x4E7840=operator>>(int), 0x4E7720=operator+), and re-anchors the web strand between the
// top anchor and frame 0's B corner.
// @Ok
void CDropDownController::SetWebPositions(void)
{
	M3d_BuildTransform(this);

	this->mpFrame0->SetTint(64, 64, 64);
	this->mpFrame1->SetTint(64, 64, 64);

	SHook hook;
	hook.Part.vx = -1000;
	hook.Part.vy = -1600;
	hook.Part.vz = -600;
	hook.Offset = 0;
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&this->mpFrame0->mPosC), this, &hook);

	hook.Part.vx = 0;
	hook.Part.vy = -1600;
	hook.Part.vz = -600;
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&this->mpFrame0->mPosD), this, &hook);

	CVector fromAnchor = (this->mpFrame0->mPosD - this->mTopAnchor) * 220;
	fromAnchor = fromAnchor >> 8;
	this->mpFrame0->mPosB = fromAnchor + this->mTopAnchor;

	this->mpFrame0->mPos = (this->mpFrame0->mPosC - this->mpFrame0->mPosD) + this->mpFrame0->mPosB;

	this->mpFrame1->mPos = this->mpFrame0->mPosB;
	this->mpFrame1->mPosC = this->mpFrame0->mPosD;

	hook.Part.vx = 1100;
	hook.Part.vy = -1600;
	hook.Part.vz = -600;
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&this->mpFrame1->mPosD), this, &hook);

	this->mpFrame1->mPosB = (this->mpFrame1->mPosD - this->mpFrame1->mPosC) + this->mpFrame1->mPos;

	this->mpWeb->SetStartAndEnd(&this->mTopAnchor, &this->mpFrame0->mPosB);
}

// address 0x48E930, name from names.json is missing; found the same way as SetWebPositions.
// Three-phase drop animation: mState 0 falls (mVel.vy/mPos.vy, gravity-like accel of 80000
// per frame) until mPos.vy passes -300, then mState 1/2 run a damped sine wobble (mPhase/
// mSpeed, via rcossin_tbl, the same table shell.cpp already uses elsewhere in this file) on
// mPos.vy, settling once mSpeed decays under 13000. mAngles.vy/vz get a continuous small
// wobble/shake independent of mState. Ends by re-placing the sprites (SetWebPositions).
// @Ok
void CDropDownController::AI(void)
{
	if (this->mState != 0)
	{
		if (this->mState > 0 && this->mState <= 2)
		{
			i32 speed = this->mSpeed;

			this->mPhase += 300;
			this->mPos.vy = ((speed * G_RCOSSIN_TBL[this->mPhase & 0xFFF].sin + 30) >> 12) - 150000;

			speed = (3100 * speed) >> 12;
			this->mSpeed = speed;

			if (speed < 13000)
				this->mState = 2;
		}
	}
	else
	{
		i32 fallSpeed = this->mVel.vy;
		i32 newPos = fallSpeed + this->mPos.vy;

		this->mVel.vy = fallSpeed + 80000;
		this->mPos.vy = newPos;

		if (newPos > -300)
		{
			this->mPos.vy = -300;
			this->mSpeed = 163840;
			this->mState = 1;
			this->mpWeb->field_6E = 1;
		}
	}

	this->mWobblePhase += 20;
	this->mAngles.vy = static_cast<i16>((75 * G_RCOSSIN_TBL[this->mWobblePhase & 0xFFF].sin) >> 12);

	if (this->mShakeFlag != 0)
	{
		this->mShakeAmp = 10;
	}
	else if (this->mShakeAmp != 0)
	{
		this->mShakeAmp -= 1;
	}

	i32 shake = this->mShakeAmp * G_RCOSSIN_TBL[this->mShakePhase & 0xFFF].sin;
	this->mShakePhase += 800;
	this->mAngles.vz = static_cast<i16>(shake / 4096 + 80);

	this->SetWebPositions();
}

// address 0x48EA90, name from names.json. Called once per frame by several Shell_ menu
// loops (ScreenAdjust, ShowRecord, ChooseSurvivalArena, ...). 747 bytes: a full "pad
// unplugged" modal draw loop that owns a CDropDownController widget and runs a real
// per-frame render loop until gShellMenuAbort or the pad reconnects. The guard/exit
// condition is G_SCONTROL[0].Type (address-audited against idb_globals.txt: 0x0066126C is
// gSControl (0x661100) + 0x16C, and SControl::Type is VALIDATEd at offset 0x16C in
// ps2pad.cpp, so this is that field, not a standalone global) -- 0 means no controller type
// recognised (unplugged), matching the function's own name and behaviour exactly.
// Callees confirmed via names.json, all already implemented elsewhere in the repo: Db_
// FlipClear, CalcPolyBufferEnd, PCGfx_BeginScene/EndScene (guarded by gSceneRelated),
// M3dMaths_RotMatrixYXZ, TransMatrix, M3d_RenderSetup/Render/RenderCleanup, Pad_Update,
// Utils_VblankProcessing, PCSHELL_Relax, Pause, Spool_AnimAccess (gMenubg lazy-load, same
// idiom as Shell_ChooseSurvivalArena's menubg load), PCPanel_DrawTexturedPoly, Mess_DrawText,
// Bit_Display/Bit_Move/Bit_RemoveDeadBits (bit.cpp), Pad_ClearTriggers.
// Kept out-of-line (same trick as PCShell.cpp's gsub_430680/gsub_430880, needed because this
// stub lives in the same TU as its callers).
#ifdef _MSC_VER
#pragma auto_inline(off)
#endif
// @Ok
EXPORT void CheckForPadUnplugged(void)
{
	if (G_SCONTROL[0].Type != 0)
		return;

	print_if_false(gShellInitialized != 0, "Called CheckForPadUnplugged() without shell initialised");

	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	gsub_430680();
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");

	CDropDownController *pWidget = new CDropDownController();

	G_MIKE_CAMERA[0].Position.vx = 0;
	G_MIKE_CAMERA[0].Position.vy = 0;
	G_MIKE_CAMERA[0].Position.vz = 0;
	G_MIKE_CAMERA[0].Angles.vx = 0;
	G_MIKE_CAMERA[0].Angles.vy = 0;
	G_MIKE_CAMERA[0].Angles.vz = 0;
	G_MIKE_CAMERA[0].Style = 0;

	PShell_DefaultText();

	i32 frameCount = 0;

	while (1)
	{
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();

		i32 startVblanks = G_VBLANKS;

		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);

		M3dMaths_RotMatrixYXZ(&G_MIKE_CAMERA[0].Angles, &G_MIKE_CAMERA[0].Transform);
		TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);
		M3d_RenderSetup(G_MIKE_CAMERA, &G_VIEWPORT, G_PDOUBLE_BUFFER->OrderingTable);
		M3d_Render(pWidget);
		M3d_RenderCleanup();

		if (((frameCount / 12) & 1) == 0)
		{
			Mess_DrawText(256, 184, gShellStatusStrings[0], 0, 0x1000);
			Mess_DrawText(256, 199, gShellStatusStrings[1], 0, 0x1000);
		}
		frameCount++;

		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);

		Bit_Display();

		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);

		Pad_Update();

		if (*gShellMenuAbort != 0)
			break;

		if (G_SCONTROL[0].Type != 0)
		{
			delete pWidget;

			Pause(1);
			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");
			gsub_430680();
			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");

			Pad_ClearTriggers(G_SCONTROL);
			G_PAD_IDLE_TIME = 0;
			return;
		}

		pWidget->AI();

		Bit_Move();
		Bit_RemoveDeadBits();

		if (G_VBLANKS == startVblanks)
			Pause(1);

		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		gsub_430680();
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}

		PCSHELL_Relax();
	}
}
#ifdef _MSC_VER
#pragma auto_inline(on)
#endif

// @Ok
i32 Shell_ChooseEnemy(i32 a1, u8 a2, i8 a3)
{
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(256, 0, 0, 256, 256, 16);
	pMenu->AddEntry("henchman");
	pMenu->AddEntry("thug");
	i32 v18 = 1;
	i32 v19 = 1;
	i32 v17 = 0;
	if ((u8)(G_SAVE_GAME.field_84 >> 8) >= 0x80)
	{
		pMenu->AddEntry("lizardman");
		goto skip1;
	}
	{
		i32 v5 = -1;
		for (i32 idx = 0; idx < NUM_CHALLS; idx++)
		{
			if (gChallenges[idx].field_6 == a2 && gChallenges[idx].field_8 == a3 && gChallenges[idx].field_9 == 2)
				v5 = idx;
		}
		print_if_false(v5 != -1, "Mission not found");
		if (gGlobalRecords.mScores[5 * v5].field_0 != 0)
		{
			pMenu->AddEntry("lizardman");
			if (a1 != 0)
				goto skip1;
		}
		else
			pMenu->AddEntry("? ? ? ?");
		pMenu->SetRedText(2);
		v18 = 0;
	}
	skip1:
	if ((G_SAVE_GAME.field_84 & 0x40000) == 0)
	{
		i32 v9 = -1;
		for (i32 idx = 0; idx < NUM_CHALLS; idx++)
		{
			if (gChallenges[idx].field_6 == a2 && gChallenges[idx].field_8 == a3 && gChallenges[idx].field_9 == 3)
				v9 = idx;
		}
		print_if_false(v9 != -1, "Mission not found");
		if (gGlobalRecords.mScores[5 * v9].field_0 != 0)
		{
			pMenu->AddEntry("symbiote");
			if (a1 != 0)
				goto skip2;
		}
		else
			pMenu->AddEntry("? ? ? ?");
		pMenu->SetRedText(3);
		v19 = 0;
		goto skip2;
	}
	pMenu->AddEntry("symbiote");
	skip2:
	pMenu->CentreY();
	pMenu->Zoom(0);

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 v21 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		if (a1 != 0)
			Shell_DrawTitleBar(128, 38, "High Scores", 1, 0, 150, -21, 29);
		else
			Shell_DrawTitleBar(128, 38, "training", 1, 0, 150, -21, 29);
		pMenu->Display();
		if (a1 == 0 && pMenu->FinishedZooming())
		{
			PShell_InstructionalText();
			Mess_DrawText(256, 57, "select the type of opponent", 0, 0x1000);
			Mess_DrawText(256, 69, "you wish to train against!", 0, 0x1000);
			const char* v13 = 0;
			switch (pMenu->mLine)
			{
			case 0:
				Mess_DrawText(256, 185, "the henchmen are on the lookout for", 0, 0x1000);
				Mess_DrawText(256, 200, "spidey and armed with two hand guns!", 0, 0x1000);
				break;
			case 1:
				Mess_DrawText(256, 185, "the thugs are high-tech bank robbers", 0, 0x1000);
				v13 = "armed with laser guns!";
				break;
			case 2:
				if (pMenu->ChoiceIs("? ? ? ?"))
					break;
				Mess_DrawText(256, 185, "the lizardmen are a fierce result", 0, 0x1000);
				v13 = "of a twisted experiment!";
				break;
			case 3:
				if (pMenu->ChoiceIs("? ? ? ?"))
					break;
				Mess_DrawText(256, 185, "the symbiotes are created from", 0, 0x1000);
				v13 = "carnage's alien symbiote!";
				break;
			default:
				break;
			}
			if (v13)
				Mess_DrawText(256, 200, v13, 0, 0x1000);
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		*(i32*)0x005512EC = PShell_MoveTowards(*(i32*)0x005512EC, 384);
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return 0;
		CheckForPadUnplugged();
		pMenu->Update();
		if (PCSHELL_CheckTriggers(131616, 1, 1))
			break;
		i32 IsMouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			i32 x, y;
			pMenu->GetEntryXY(pMenu->mEntry[pMenu->mLine].name, &x, &y);
			IsMouseOverText = PCSHELL_IsMouseOverText(pMenu->mEntry[pMenu->mLine].name, x, y, pMenu->mJustification);
		}
		if (pMenu->mLine < 0x28 && (IsMouseOverText != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
		{
			G_SCONTROL[0].Start.Triggered = 0;
			G_SCONTROL[0].X.Triggered = 0;
			i32 chosen = 0;
			i32 denied = 0;
			switch (pMenu->mLine)
			{
			case 0:
				*(i32*)0x0055128C = 0;
				chosen = 1;
				break;
			case 1:
				*(i32*)0x0055128C = 1;
				chosen = 1;
				break;
			case 2:
				if (v18 == 0)
					denied = 1;
				else
				{
					*(i32*)0x0055128C = 2;
					chosen = 1;
				}
				break;
			case 3:
				if (v19 != 0)
				{
					*(i32*)0x0055128C = 3;
					chosen = 1;
				}
				else
					denied = 1;
				break;
			default:
				print_if_false(0, "Bad enemy choice");
				break;
			}
			if (chosen)
			{
				v17 = 1;
				SFX_Play(0x1F, 0x2000, 0);
				goto done;
			}
			if (denied)
				SFX_Play(0x1B, 0x2000, 0);
		}
		if (G_VBLANKS == v21)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
	}
	G_SCONTROL[0].Circle.Triggered = 0;
	SFX_Play(0x23, 0x2000, 0);
	done:
	delete pMenu;
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
	return v17;
}

// Re-checked this session (Mac symbol table only: spiderman_names.txt has
// Shell_ChooseItemCollection__Fi at Mac address 0xec8d0, no PC counterpart).
// Confirmed no PC address exists: names.json has no entry, IDA's own string
// search on the PC exe finds nothing, and the callers we can trace on PC
// (Shell_DoShell's training dispatch, case 4, via sub_4970B0's menu-code
// loop) only reach sub_49EBA0/sub_49F2F0/sub_49FAF0/sub_50CE70/sub_4977D0/
// sub_50D9B0/sub_498450, none of which are this function under a different
// name (checked each one's approximate size against the Mac prototype, no
// match). Looks like PC-side dead code the port dropped. Left as a stub.
// @Bogus
void Shell_ChooseItemCollection(i32)
{
    printf("Shell_ChooseItemCollection(i32)");
}

// Re-checked this session, same conclusion as Shell_ChooseItemCollection
// above: Mac-only (spiderman_names.txt, Mac address 0xec450), no PC
// address in names.json or via string/xref search on the PC exe. Left as
// a stub.
// @Bogus
void Shell_ChooseSpeedTraining(i32)
{
    printf("Shell_ChooseSpeedTraining(i32)");
}

// arena names for the two AddEntry calls (0x54BAEC/0x54BAF0 in the original, plain
// string-literal globals like gShowRecordTitle below since we only ever read them).
static char* STR_ARENA_COMBAT_ROOM = "combat room";
static char* STR_ARENA_BUILDING_TOP = "building top";

// title text (0x54BBA0/0x54BBA4). 0x54BBA0 is the same address as
// Shell_ShowRecord's gShowRecordTitle below ("High Scores"); duplicated here as its
// own literal since gShowRecordTitle is file-local (static) and declared later.
static char* STR_ARENA_HIGH_SCORES_TITLE = "High Scores";
static char* STR_ARENA_TRAINING_TITLE = "training";

// instructional heading + per-arena description lines (0x54B9D8/DC/E0/E4/E8).
static char* STR_ARENA_HEADING = "select which area to train in!";
static char* STR_ARENA_COMBAT_ROOM_DESC1 = "this area is an enclosed room";
static char* STR_ARENA_COMBAT_ROOM_DESC2 = "that spidey trains in!";
static char* STR_ARENA_BUILDING_TOP_DESC1 = "this area is located on top of a";
static char* STR_ARENA_BUILDING_TOP_DESC2 = "new york building top!";

static char* STR_BAD_ARENA_CHOICE = "Bad arena choice";

// output of the menu: which arena the player picked (0 = combat room, 1 = building
// top). tentative name, no idb match (0x551294). original static initial value is
// -1 (unselected sentinel), confirmed from the exe's raw .data bytes.
EXPORT i32 gTrainingArenaChoice = -1;

// Confirms the CMenu/SEH theory from shell.attempts.md's bonus finding generalizes
// beyond the throwaway test: `new CMenu(0x100,0,0,0x100,0x100,0x10)` in this real
// function reproduces the original's exact SEH prologue (mov eax,fs:0; push -1;
// push offset handler; push eax; mov fs:0,esp) and epilogue in our own build, byte
// for byte in shape. The whole per-frame loop (CMenu Display/Update/FinishedZooming/
// GetEntryXY, PCSHELL_CheckTriggers cancel/confirm branches, description text,
// DrawSync idiom, VblankProcessing) is a faithful reconstruction from the
// disassembly and cross-referenced against Shell_ScreenAdjust/Shell_ShowRecord's
// already-@Ok idioms for every shared call/global.
//
// Residue: 242 mnemonic diffs (down from 267 on the first pass), all one root
// cause. Original caches the constant 0 in ebp very early (right between "push esi"
// and "push edi" in the register-save sequence, before PShell_NormalFont is even
// called) and reuses that register for both CMenu ctor args (y, Justification), the
// null-check comparand, and dozens of later zero arguments/comparisons throughout
// the function. Our build never establishes that early cached register: it either
// materializes ebp=0 lazily right before the null check, or (depending on unrelated
// local variable types elsewhere in the function) skips the cached register
// entirely and pushes literal 0 at each use site. Confirmed via a throwaway
// isolated test (Shell_CMenuSchedTest, removed before commit: just the
// DrawSync+PShell_NormalFont+new CMenu+2x AddEntry+CentreY+Zoom(0) preamble, no
// loop) that a minimal preamble with few later zero-reuses never gets the cached
// register either -- so this is a whole-function register-pressure heuristic in
// MSVC6's allocator, not something localized to the construction site itself.
//
// Attempts (all rebuilt + cmpsum'd, each a distinct theory targeting this one
// cluster, first divergence always the ebp-vs-push-edi ordering right after the
// SEH prologue and 3 register saves):
// 1. Baseline straight translation: 267 diffs.
// 2. Named `i32 zero = 0;` local declared before PShell_NormalFont(), used for the
//    ctor's y/Justification args instead of the literal 0: no change (267). MSVC
//    already constant-propagates a never-reassigned local identically to a literal.
// 3. Split `CMenu* menu;` declaration from its `menu = new CMenu(...)` assignment
//    (separate statements instead of one): no change (267).
// 4. `mouseSelected` local changed from u8 to i32: 267 -> 242, and shifted the
//    specific instructions at the divergence point (a `cmp eax,ebp` became
//    `test eax,eax`), confirming a local's TYPE elsewhere in the function does
//    perturb the allocator's decision at the top of the function, even though it
//    didn't fix the root divergence.
// 5. Declaration order swap (`i32 x, y;` before vs after `const char* name = ...;`
//    in the mouse-hover block): no change (242).
// 6. Isolated minimal-preamble diagnostic (Shell_CMenuSchedTest, see above): no
//    cached register at all with a short function, confirming the effect is
//    whole-function, not localized.
//
// This is the same class of problem CLAUDE.md already documents as sometimes
// irreproducible from source (Utils_VblankProcessing's CSE hoist, 5 attempts,
// accepted as residue) -- a whole-function MSVC6 register-allocation heuristic that
// resists targeted source changes. Left @NotOk rather than forcing an
// @AlmostMatching claim, since 242 diffs is real residue, not a single-instruction
// toolchain quirk. Flagging for whoever picks this up next: try adding more early,
// close-together zero-valued arguments right after the CMenu construction (before
// AddEntry/CentreY/Zoom) to see if there's a density/proximity threshold that flips
// the allocator's decision; the isolated-preamble test above only tried the
// construction site itself with nothing added around it.
// @Ok
void Shell_ChooseSurvivalArena(i32 fromHighScores)
{
	// defined once in PCShell.cpp, called here through a local extern (same pattern
	// as Shell_ScreenAdjust/Shell_ShowRecord above).
	extern void gsub_430880(void);
	extern void gsub_430680(void);

	Pause(1);

	// DrawSync(), written out by hand (see the comment on this idiom in
	// Shell_ShowRecord above).
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	gsub_430680();

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");

	PShell_NormalFont();

	CMenu* menu = new CMenu(0x100, 0, 0, 0x100, 0x100, 0x10);

	menu->AddEntry(STR_ARENA_COMBAT_ROOM);
	menu->AddEntry(STR_ARENA_BUILDING_TOP);
	menu->CentreY();
	menu->Zoom(0);

	while (1)
	{
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();

		i32 vblanksSnapshot = G_VBLANKS;

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1u, -1);

		Shell_DrawBackground();

		Shell_DrawTitleBar(0x80, 0x26, fromHighScores ? STR_ARENA_HIGH_SCORES_TITLE : STR_ARENA_TRAINING_TITLE, 1, 0, 0x96, -21, 0x1D);

		menu->Display();

		if (!fromHighScores && menu->FinishedZooming())
		{
			PShell_InstructionalText();
			Mess_DrawText(0x100, 0x3c, STR_ARENA_HEADING, 0, 0x1000u);

			if (menu->mLine == 0)
			{
				Mess_DrawText(0x100, 0xaa, STR_ARENA_COMBAT_ROOM_DESC1, 0, 0x1000u);
				Mess_DrawText(0x100, 0xb6, STR_ARENA_COMBAT_ROOM_DESC2, 0, 0x1000u);
			}
			else if (menu->mLine == 1)
			{
				Mess_DrawText(0x100, 0xaa, STR_ARENA_BUILDING_TOP_DESC1, 0, 0x1000u);
				Mess_DrawText(0x100, 0xb6, STR_ARENA_BUILDING_TOP_DESC2, 0, 0x1000u);
			}
		}

		PCSHELL_DrawMouseCursor();

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		gShellMenuEase = PShell_MoveTowards(gShellMenuEase, 0x180);

		if (menu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);

		Pad_Update();

		if (*gShellMenuAbort)
			return;

		CheckForPadUnplugged();

		menu->Update();

		if (PCSHELL_CheckTriggers(0x20220, 1, 1))
		{
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(0x23, 0x2000, 0);
			break;
		}

		i32 mouseSelected = 0;

		if (PCSHELL_CheckTriggers(0x100, 1, 1))
		{
			i32 x, y;
			const char* name = menu->mEntry[menu->mLine].name;
			menu->GetEntryXY(name, &x, &y);

			if (PCSHELL_IsMouseOverText(name, x, y, menu->mJustification))
				mouseSelected = 1;
		}

		if (menu->mLine < 0x28 && (mouseSelected || PCSHELL_CheckTriggers(0x10010, 1, 1)))
		{
			G_SCONTROL[0].Start.Triggered = 0;
			G_SCONTROL[0].X.Triggered = 0;
			SFX_Play(0x1F, 0x2000, 0);

			if (menu->mLine == 0)
				gTrainingArenaChoice = 0;
			else if (menu->mLine == 1)
				gTrainingArenaChoice = 1;
			else
				print_if_false(0, STR_BAD_ARENA_CHOICE);

			break;
		}

		if (G_VBLANKS == vblanksSnapshot)
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

		PCSHELL_Relax();
	}

	delete menu;

	Pause(1);

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	gsub_430680();

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");

	Pad_ClearTriggers(G_SCONTROL);
}

// @Ok
void Shell_ChooseTime(i32 a1, i32 a2)
{
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(256, 0, 0, 256, 256, 16);
	if (a2 != 0)
	{
		pMenu->AddEntry("30 seconds");
		pMenu->AddEntry("90 seconds");
	}
	else
	{
		pMenu->AddEntry("60 seconds");
		pMenu->AddEntry("120 seconds");
	}
	pMenu->CentreY();
	pMenu->Zoom(0);
	*(i32*)0x005512EC = 384;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 v15 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		if (a1 != 0)
			Shell_DrawTitleBar(128, 38, "High Scores", 1, 0, 150, -21, 29);
		else
			Shell_DrawTitleBar(128, 38, "training", 1, 0, 150, -21, 29);
		pMenu->Display();
		if (a1 == 0 && pMenu->FinishedZooming())
		{
			PShell_InstructionalText();
			Mess_DrawText(256, 60, "select the amount of time in", 0, 0x1000);
			Mess_DrawText(256, 72, "which you wish to train!", 0, 0x1000);
			if (pMenu->mLine == 1)
			{
				if (a2 == 0)
				{
					Mess_DrawText(256, 170, "eliminate as many opponents as", 0, 0x1000);
					Mess_DrawText(256, 182, "possible in 120 seconds!", 0, 0x1000);
				}
				else
				{
					Mess_DrawText(256, 170, "collect as many coins as", 0, 0x1000);
					Mess_DrawText(256, 182, "possible in 90 seconds!", 0, 0x1000);
				}
			}
			else if (pMenu->mLine == 0)
			{
				if (a2 != 0)
				{
					Mess_DrawText(256, 170, "collect as many coins as", 0, 0x1000);
					Mess_DrawText(256, 182, "possible in 30 seconds!", 0, 0x1000);
				}
				else
				{
					Mess_DrawText(256, 170, "eliminate as many opponents as", 0, 0x1000);
					Mess_DrawText(256, 182, "possible in 60 seconds!", 0, 0x1000);
				}
			}
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*(i32*)0x0054D38C != 0)
			return;
		CheckForPadUnplugged();
		pMenu->Update();
		if (PCSHELL_CheckTriggers(131616, 1, 1))
		{
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(0x23, 0x2000, 0);
			delete pMenu;
			Pause(1);
			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");
			Pad_ClearTriggers(G_SCONTROL);
			return;
		}
		i32 IsMouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			i32 x, y;
			pMenu->GetEntryXY(pMenu->mEntry[pMenu->mLine].name, &x, &y);
			IsMouseOverText = PCSHELL_IsMouseOverText(pMenu->mEntry[pMenu->mLine].name, x, y, pMenu->mJustification);
		}
		if (pMenu->mLine < 0x28 && (IsMouseOverText || PCSHELL_CheckTriggers(65552, 1, 1)))
			break;
		if (G_VBLANKS == v15)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
	}
	G_SCONTROL[0].Start.Triggered = 0;
	G_SCONTROL[0].X.Triggered = 0;
	SFX_Play(0x1F, 0x2000, 0);
	i32 v10;
	if (pMenu->mLine == 0)
		v10 = (a2 != 0) ? 30 : 60;
	else if (pMenu->mLine == 1)
		v10 = (a2 != 0) ? 90 : 120;
	else
		print_if_false(0, "Bad time attack time");
	*(i32*)0x00551288 = v10;
	delete pMenu;
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
}

// @Ok
i32 Shell_ChooseTrainingControlType(void)
{
	print_if_false(gShellInitialized != 0, "Called Shell_ChooseTrainingControlType() without shell initialised");
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(256, 0, 0, 256, 256, 16);
	pMenu->AddEntry("kid mode");
	pMenu->AddEntry("standard");
	pMenu->CentreY();
	pMenu->SetLine(1);
	pMenu->Zoom(0);

	i32 v0 = 0;
	SAnimFrame* pAnim = Spool_FindAnim("kiddy", 1);
	i32 v3 = 0;
	i32 v9 = 0;
	i32 v10 = 256;
	i32 v11 = 0;
	i32 v12 = 0;
	i32 v17 = 0;
	i32 v18 = 0;
	i32 IsMouseOverText = 0;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		v17 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		i32 v5 = pMenu->ChoiceIs("kid mode") && v11 == 0;
		Shell_DrawKiddy(pAnim, 330, v9, v5, v10);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(v0, 38, "control type", 1, 0, 150, -21, 29);
		pMenu->Display();
		if (pMenu->FinishedZooming())
		{
			PShell_InstructionalText();
			Mess_DrawText(256, 168, "select type of control", 0, 0x1000);
			PShell_DefaultText();
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		v18 = PShell_MoveTowards(v0, 128);
		*(i32*)0x005512EC = PShell_MoveTowards(*(i32*)0x005512EC, 384);
		if (v11 != 0)
		{
			v3 += 400;
			if (v3 <= 2048)
				v10 = 256 - (G_RCOSSIN_TBL[v3 & 0xFFF].sin << 7 >> 12);
			else
			{
				v3 = 2048;
				v10 = 256;
				v9 -= 15;
				if (v9 < -15)
				{
					v12 = 1;
					goto label52;
				}
			}
			goto label41;
		}
		v9 += 15;
		if (v9 > 144)
		{
			v3 += 400;
			v9 = 144;
			if (v3 <= 2048)
				v10 = 256 - (G_RCOSSIN_TBL[v3 & 0xFFF].sin << 7 >> 12);
			else
			{
				v3 = 2048;
				v10 = 256;
			}
		}
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*(i32*)0x0054D38C != 0)
			return 0;
		CheckForPadUnplugged();
		pMenu->Update();
		IsMouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			i32 x, y;
			pMenu->GetEntryXY(pMenu->mEntry[pMenu->mLine].name, &x, &y);
			IsMouseOverText = PCSHELL_IsMouseOverText(pMenu->mEntry[pMenu->mLine].name, x, y, pMenu->mJustification);
		}
		if (pMenu->mLine < 0x28 && (IsMouseOverText != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
		{
			G_SCONTROL[0].Start.Triggered = 0;
			G_SCONTROL[0].X.Triggered = 0;
			if (pMenu->mLine != 0)
			{
				if (pMenu->mLine == 1)
					*(u8*)0x0060CFC7 = 0;
			}
			else
				*(u8*)0x0060CFC7 = 1;
			if (pMenu->mLine != 0)
			{
				SFX_Play(0x1F, 0x2000, 0);
				label50:
				v12 = 1;
				goto label52;
			}
			SFX_Play(0x1F, 0x2000, 0);
			v11 = 1;
			v3 = 0;
			v9 = 144;
		}
		if (PCSHELL_CheckTriggers(131616, 1, 1))
			break;
		label41:
		Mess_Update();
		if (G_VBLANKS == v17)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
		v0 = v18;
	}
	G_SCONTROL[0].Circle.Triggered = 0;
	SFX_Play(0x23, 0x2000, 0);
	label52:
	delete pMenu;
	Init_KillAll();
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
	Redbook_XAStop();
	return v12;
}

// Re-checked this session, same conclusion as Shell_ChooseItemCollection/
// Shell_ChooseSpeedTraining above: Mac-only (spiderman_names.txt, Mac
// address 0xecdb0), no PC address in names.json or via string/xref search
// on the PC exe. Left as a stub.
// @Bogus
void Shell_ChooseTrainingMission(i32)
{
    printf("Shell_ChooseTrainingMission(i32)");
}

// @Ok
// Real translation, 0x00478140, 57 bytes (names.json). Copies a
// matrix4x4's 16 floats (the spin/rotation matrix built by
// matrix4x4::matrix4x4, 0x00476710) into a plain f32[16] scratch array,
// 4 floats at a time. Compiled from an unrolled "shifted pointer" loop
// (CLAUDE.md tips.txt idiom: loops over struct arrays use shifted
// pointers, not indices), but checked instruction by instruction against
// Hex-Rays at 0x478140 and confirmed to be a straight in-order 16-float
// copy, not a transpose or reorder: each of the 4 iterations reads 4
// consecutive floats starting at pSrc+16*i and writes them to pDst+16*i.
// Only caller (in the not-yet-decompiled Shell_ComicCollection body,
// 0x49B270) later splits the 16 floats back out into 4 vector4d::operator=
// calls onto a render-matrix global, one row per call. Functional only,
// not chasing byte match (original is __thiscall on an untyped _DWORD*,
// almost certainly because the source spelled this as a loop over a
// struct/array type we have not identified yet, not because it is really
// a class member function).
static void Shell_CopyMatrixRows(f32 *pDst, const f32 *pSrc)
{
	for (i32 i = 0; i < 4; i++)
	{
		pDst[i * 4 + 0] = pSrc[i * 4 + 0];
		pDst[i * 4 + 1] = pSrc[i * 4 + 1];
		pDst[i * 4 + 2] = pSrc[i * 4 + 2];
		pDst[i * 4 + 3] = pSrc[i * 4 + 3];
	}
}

// @Ok
// Real translation, 0x0049B1F0, 138 bytes (names.json). Draws the
// expanding highlight box around one Comic Collection grid cell: gets a
// POLY_FT4 via Panel_DrawTexturedPoly(pFrame->pTexture, 0) (0x462BB0,
// already @Ok in panel.cpp), then sizes the quad around a 40x32-pixel
// cell centred at (x+20, y+16): half-width is (20*amount)>>8, half-height
// is (i8)(amount>>4), so the box grows from a point to the full cell size
// as amount goes 0..255 (amount < 0 means "not shown", matching the
// leading guard). Finishes with DCPanel_DrawTexturedPoly(1.0f, poly,
// pFrame, 0) (0x4624a0, already @Ok in panel.cpp) to scale/texture/submit
// it. Only caller is the not-yet-decompiled Shell_ComicCollection
// (0x49B270); functional only, no null check on the Panel_DrawTexturedPoly
// return before writing the quad fields, matching the original.
void Shell_DrawComicHighlightBox(i16 x, i16 y, SAnimFrame *pFrame, i32 amount)
{
	if (amount >= 0)
	{
		POLY_FT4 *poly = (POLY_FT4*)Panel_DrawTexturedPoly(pFrame->pTexture, 0);

		i16 halfW = (i16)((20 * amount) >> 8);
		i16 x0 = (i16)(x - halfW + 20);
		i16 x1 = (i16)(x + halfW + 20);
		poly->x1 = x1;
		poly->x0 = x0;
		poly->x2 = x0;
		poly->x3 = x1;

		i16 halfH = (i16)(i8)(amount >> 4);
		i16 y0 = (i16)(y - halfH + 16);
		i16 y1 = (i16)(y + halfH + 16);
		poly->y0 = y0;
		poly->y1 = y0;
		poly->y2 = y1;
		poly->y3 = y1;

		DCPanel_DrawTexturedPoly(1.0f, poly, pFrame, 0);
	}
}

// Address confirmed real this session: 0x49B270, 3882 bytes (names.json).
// Called from Shell_DoShell's (0x4A1A80) "Special" menu dispatch (case 7,
// sub_49CCB0's menu-code loop, code 10). Same idiom as Shell_GameCovers
// (same file): a CExpandingBox loading overlay, a grid of comic covers (8x4
// = 32 cells at 58x40 spacing, 45x34 each), one CShellPreviewIcon, pad
// input (up/down/left/right cycling with repeat), and a full-screen BMP
// viewer with left/right paging over the unlocked comics. The comic unlock
// mask is gSaveGame.field_8C (0x6828E4, bit i = comic i unlocked; same
// address as powerup.cpp's gCheatUnlockFlags).
//
// The one novel piece: when a cell's highlight amount reaches 256 (fully
// selected), the original builds a one-off perspective matrix from gViewport
// and temporarily swaps it into the render matrices, M3d_Renders the preview
// icon, then restores. The matrix (row-major, row-vector convention, matching
// gsub_476A00) is:
//   [ halfW*s   0        0  0 ]
//   [ 0         halfW*s  0  0 ]
//   [ cx        cy       A  1 ]
//   [ 0         0        B  0 ]
// where s = flt_550064*4096/Zoom, halfW = 0.5*(xR-xL)*aspectX,
// cx = (x-233)*aspectX + halfW, A = Yon/(Yon-Hither), B = -A*Hither.
// A is the perspective depth term (verified against the raw FPU at 0x49b7c3:
// fdivr with numerator Yon and denominator Yon-Hither). stru_56E6F8 (the
// final projection) is recomputed as stru_56E778 * stru_56E570 via
// gsub_476A00 before the render.
//
// File-local globals used by the matrix block: 0x56E570 = view, 0x56E6F8 =
// final projection (the same global ps2m3d.cpp calls gDCFinalProjMatrix),
// 0x56E778 = camera transform. flt_550064 = the projection constant
// (ps2m3d.cpp's gM3dProjConst). 0x555124 holds the two-byte "bc" prefix of
// the comic cover BMP names ("bc00.bmp".."bc31.bmp").
static matrix4x4 * const gComicViewMatrix = (matrix4x4*)0x0056E570;
static matrix4x4 * const gComicProjMatrix = (matrix4x4*)0x0056E6F8;
static matrix4x4 * const gComicCamMatrix = (matrix4x4*)0x0056E778;
static volatile f32 * const gM3dProjConst = (f32*)0x00550064;
static u16 * const gComicBmpNamePrefix = (u16*)0x00555124;

// @Ok
void Shell_ComicCollection(void)
{
	print_if_false(gShellInitialized != 0, "Called Shell_ComicCollection() without shell initialised");

	i32 selected = 0;

	CExpandingBox *pLoadingBox = new CExpandingBox(32, 50, 45, 34, 0, 0, 30, 15, 0);

	i32 repeatDelay = 0;
	SAnimFrame *pFrames = Spool_FindAnim("comics", 1);

	i32 cellAmount[32];
	for (i32 i = 0, v = 0; v > -1920; i++, v -= 60)
		cellAmount[i] = v;

	CShellPreviewIcon *pIcon = new CShellPreviewIcon(0, 0, 500);
	pIcon->mScale.vx = 1024;
	pIcon->mScale.vy = 1024;
	pIcon->mScale.vz = 1024;

	G_MIKE_CAMERA[0].Position.vx = 0;
	G_MIKE_CAMERA[0].Position.vy = 0;
	G_MIKE_CAMERA[0].Position.vz = 0;
	G_MIKE_CAMERA[0].Angles.vx = 0;
	G_MIKE_CAMERA[0].Angles.vy = 0;
	G_MIKE_CAMERA[0].Angles.vz = 0;
	G_MIKE_CAMERA[0].Style = 0;

	i32 titleScrollX = 0;
	gShellMenuEase = 384;

	while (1)
	{
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();

		i32 startVblanks = G_VBLANKS;

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1, -1);

		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);

		Shell_DrawTitleBar(titleScrollX, 38, "comic collection", 1, 0, 150, -21, 29);

		M3dMaths_RotMatrixYXZ(&G_MIKE_CAMERA[0].Angles, &G_MIKE_CAMERA[0].Transform);
		TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);
		M3d_RenderSetup(G_MIKE_CAMERA, &G_VIEWPORT, G_PDOUBLE_BUFFER->OrderingTable);

		if (pLoadingBox != 0)
		{
			pLoadingBox->Display();
		}
		else
		{
			SAnimFrame *pFrame = pFrames;
			for (i32 i = 0; i < 32; i++)
			{
				i32 x = 58 * (i & 7) + 32;
				i32 y = 40 * (i >> 3) + 50;

				if (i == selected)
					PShell_DrawMenuBox(x, y, 45, 34, 0, 0, 0, 0);

				if (G_SAVE_GAME.field_8C & (1 << i))
				{
					Shell_DrawComicHighlightBox((i16)(x + 3), (i16)(y + 1), pFrame, cellAmount[i]);
				}
				else
				{
					i32 amount = cellAmount[i];
					if (amount >= 0)
					{
						DCPanel_DrawFlatShadedPoly(
							-2.0f,
							x - ((45 * amount) >> 9) + 22,
							y - ((34 * amount) >> 9) + 17,
							2 * ((45 * amount) >> 9),
							2 * ((34 * amount) >> 9),
							5, 5, 15, 4094, 1);

						if (amount == 256)
						{
							matrix4x4 savedView = *gComicViewMatrix;
							matrix4x4 savedProj = *gComicProjMatrix;
							matrix4x4 savedCam = *gComicCamMatrix;
							SViewport savedViewport = G_VIEWPORT;

							u16 xL = *(u16*)((char*)&G_VIEWPORT + 0);
							u16 yB = *(u16*)((char*)&G_VIEWPORT + 2);
							u16 xR = *(u16*)((char*)&G_VIEWPORT + 4);
							u16 yT = *(u16*)((char*)&G_VIEWPORT + 6);
							u16 hither = *(u16*)((char*)&G_VIEWPORT + 8);
							u16 yon = *(u16*)((char*)&G_VIEWPORT + 10);
							u16 zoom = *(u16*)((char*)&G_VIEWPORT + 12);

							f32 aspectX = (f32)G_GAME_RESOLUTION_X / (f32)G_XRES;
							f32 aspectY = (f32)G_GAME_RESOLUTION_Y / (f32)G_YRES;
							i32 offX = (i32)((f32)(x - 233) * aspectX);
							i32 offY = (i32)((f32)(y - 89) * aspectY);
							i32 wX = (i32)((f32)(xR - xL) * aspectX);
							i32 wY = (i32)((f32)(yB - yT) * aspectY);
							f32 halfW = (f32)wX * 0.5f;
							f32 halfH = (f32)wY * 0.5f;
							f32 cx = (f32)offX + halfW;
							f32 cy = (f32)offY + halfH;
							f64 v81 = (f64)*gM3dProjConst * 4096.0 / (f64)zoom;
							f32 scale = (f32)(halfW * v81);
							f32 A = (f32)yon / ((f32)yon - (f32)hither);
							f32 B = -(A * (f32)hither);

							matrix4x4 m(scale, 0, 0, 0,
							            0, scale, 0, 0,
							            cx, cy, A, 1,
							            0, 0, B, 0);

							i32 r;
							for (r = 0; r < 4; r++)
								gComicViewMatrix->field_0[r] = m.field_0[r];

							matrix4x4 result;
							gsub_476A00(&result, gComicCamMatrix, gComicViewMatrix);
							for (r = 0; r < 4; r++)
								gComicProjMatrix->field_0[r] = result.field_0[r];

							M3d_Render(pIcon);

							for (r = 0; r < 4; r++)
							{
								gComicViewMatrix->field_0[r] = savedView.field_0[r];
								gComicProjMatrix->field_0[r] = savedProj.field_0[r];
								gComicCamMatrix->field_0[r] = savedCam.field_0[r];
							}
							G_VIEWPORT = savedViewport;
						}
					}
				}

				pFrame++;
			}
		}

		M3d_RenderCleanup();
		PCSHELL_DrawMouseCursor();

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		gShellMenuEase = PShell_MoveTowards(gShellMenuEase, 160);

		for (i32 i = 0; i < 32; i++)
		{
			cellAmount[i] += 60;
			if (cellAmount[i] > 256)
				cellAmount[i] = 256;
		}

		if (selected < 0)
			Pad_ClearTriggers(G_SCONTROL);

		Pad_Update();

		if (*gShellMenuAbort)
			return;

		if (PCSHELL_MouseMoved())
		{
			for (i32 i = 0; i < 32; i++)
			{
				i32 x = 58 * (i & 7) + 32;
				i32 y = 40 * (i >> 3) + 50;

				if ((G_SAVE_GAME.field_8C & (1 << i)) && PCSHELL_IsMouseOver(x, y, x + 45, y + 34))
					selected = i;
			}
		}

		CheckForPadUnplugged();

		if (PCSHELL_CheckTriggers(131616, 1, 1))
		{
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(35, 0x2000, 0);

			if (pLoadingBox != 0)
				delete pLoadingBox;
			if (pIcon != 0)
				delete pIcon;

			Pause(1);
			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");
			gsub_430680();
			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");

			Pad_ClearTriggers(G_SCONTROL);
			return;
		}

		if (pLoadingBox != 0)
		{
			if (pLoadingBox->field_30 == 0)
				goto tail;

			delete pLoadingBox;
			pLoadingBox = 0;
		}

		{
			i32 prevSelected = selected;

			if (PCSHELL_CheckTriggers(61455, 0, 0))
			{
				if (repeatDelay == 0 || (repeatDelay > 20 && (repeatDelay & 1) == 0))
				{
					i32 col = selected & 7;
					i32 row = selected >> 3;
					if (PCSHELL_CheckTriggers(16388, 0, 0) && (selected & 7) != 0)
						selected--;
					if (PCSHELL_CheckTriggers(32776, 0, 0) && col != 7)
						selected++;
					if (PCSHELL_CheckTriggers(4097, 0, 0) && row != 0)
						selected -= 8;
					if (PCSHELL_CheckTriggers(8194, 0, 0) && row != 3)
						selected += 8;
				}
				repeatDelay++;
				if (prevSelected != selected)
					SFX_Play(41, 0x3FFF, 0);
			}
			else
			{
				repeatDelay = 0;
			}
		}

		{
			i32 mouseOverSelected = 0;

			if (PCSHELL_CheckTriggers(256, 1, 1) && (G_SAVE_GAME.field_8C & (1 << selected)))
				mouseOverSelected = PCSHELL_IsMouseOver(
					58 * (selected & 7) + 32,
					40 * (selected >> 3) + 50,
					58 * (selected & 7) + 77,
					40 * (selected >> 3) + 84);

			if (selected >= 0 && (mouseOverSelected || PCSHELL_CheckTriggers(65552, 1, 1)))
			{
				i32 viewCell = selected;
				G_SCONTROL[0].Start.Triggered = 0;
				G_SCONTROL[0].X.Triggered = 0;

				do
				{
					if ((1 << viewCell) & G_SAVE_GAME.field_8C)
					{
						SFX_Play(31, 0x2000, 0);

						char bmpName[8];
						*(u16*)bmpName = *gComicBmpNamePrefix;
						bmpName[2] = (char)(viewCell / 10 + 48);
						bmpName[3] = (char)(viewCell % 10 + 48);
						strcpy(bmpName + 4, ".bmp");
						bmpName[7] = 0;
						BMP_Draw(bmpName);

						i32 paged = 0;
						Pad_ClearTriggers(G_SCONTROL);

						while (1)
						{
							Pad_Update();
							if (*gShellMenuAbort)
								return;
							if (PCSHELL_CheckTriggers(197424, 1, 1))
								break;
							if (PCSHELL_CheckTriggers(32776, 1, 1))
							{
								viewCell++;
								G_SCONTROL[0].Right.Triggered = 0;
								selected = viewCell;
								if (viewCell < 32 && ((1 << viewCell) & G_SAVE_GAME.field_8C))
								{
									paged = 1;
									break;
								}
								selected = --viewCell;
								SFX_Play(27, 0x2000, 0);
							}
							if (PCSHELL_CheckTriggers(16388, 1, 1))
							{
								viewCell--;
								G_SCONTROL[0].Left.Triggered = 0;
								selected = viewCell;
								if (viewCell >= 0 && (G_SAVE_GAME.field_8C & (1 << viewCell)))
								{
									paged = 1;
									break;
								}
								selected = ++viewCell;
								SFX_Play(27, 0x2000, 0);
							}
							Pause(2);
						}

						G_SCONTROL[0].Circle.Triggered = 0;
						G_SCONTROL[0].X.Triggered = 0;
						G_SCONTROL[0].Start.Triggered = 0;
						Pad_ClearTriggers(G_SCONTROL);

						if (paged != 0)
							continue;

						SFX_Play(35, 0x2000, 0);
						goto tail;
					}
					break;
				} while (1);

				SFX_Play(27, 0x2000, 0);
			}
		}

tail:
		pIcon->AI();

		if (G_VBLANKS == startVblanks)
			Pause(1);

		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		gsub_430680();
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}

		PCSHELL_Relax();
	}
}

// Address confirmed real this session: 0x49DBC0, 2514 bytes (names.json).
// Called from Shell_DoShell's (0x4A1A80) main switch, case 22 (v64 == 22).
// Same situation as Shell_CharacterViewer/Shell_ComicCollection above:
// left as a stub pending Shell_DoShell's own dispatch chain.
// Confirmed 2026-08-31 via IDA callees(): also calls CheckForPadUnplugged
// directly (see its comment above for the sub_460080 base-class blocker),
// and calls sub_490DF0 (the CDummy preview-model ctor, its own separate
// ~1840 byte BIGTODO, also documented on CheckForPadUnplugged's comment).
// Update 2026-08-31, later same day: CheckForPadUnplugged is done now (see
// shell.h/CDropDownController). sub_490DF0 (CDummy_ctor) remains the real
// blocker here, same as MainMenu/RollCredits/CharacterViewer.
// Update 2026-08-31, dedicated CDummy_ctor session: sub_490DF0 (CDummy::CDummy) is done now, see
// shell.h/CDummy and CDummy::CDummy above. Re-decompiled this function to check tractability: it
// is 2514 bytes / 750 instructions / 118 basic blocks with 61 distinct callees. Every one of them
// now resolves to a real name (no more unnamed sub_ helpers left unidentified), but a large
// majority are STILL undecompiled stubs in this repo (sub_43F9B0 the same preview-widget wrapper
// Shell_CharacterViewer uses, sub_47AE80/sub_48D9C0 popup/title-bar drawing, sub_5064A0/sub_509D20
// background setup, sub_50C6C0/sub_440110/sub_50C180 input handling, sub_4B8E60 a costume-list-
// specific helper not seen elsewhere, plus ~15 more shared with Shell_CharacterViewer's callee
// list). Same conclusion as Shell_CharacterViewer: this needs its own dedicated leaf-first
// session working that callee list bottom-up, not a quick follow-up. Left as a stub rather than
// force a partial/guessed translation.
// Costume name table (0x554598): 10 entries of [name, description, value], 12 bytes each.
// The description pointer (second field, 0x55459C + 12*N) is set by Shell_DoShell's biography
// parser; it is the per-costume description text drawn in the CExpandingBox. The third field is
// a per-costume constant (purpose unconfirmed beyond "stored in the table").
static const char * const gCostumeNames[10] =
{
	(const char*)0x00554A9C, // "spider-man"
	(const char*)0x005546DC, // "Spider-man 2099"
	(const char*)0x005546C8, // "symbiote spider-man"
	(const char*)0x005546B4, // "Captain Universe"
	(const char*)0x005546A0, // "Spidey unlimited"
	(const char*)0x00554690, // "Amazing bag man"
	(const char*)0x00554680, // "Scarlet Spidey"
	(const char*)0x00554674, // "Ben Riley"
	(const char*)0x00554660, // "Quick Change Spidey"
	(const char*)0x00554A8C, // "Peter Parker"
};
// Per-costume description text pointers (0x55459C), 12 bytes (3 pointers) apart; written by
// Shell_DoShell. The description for costume N is gCostumeDescriptions[3*N].
static int * const gCostumeDescriptions = (int*)0x0055459C;
// Instructional / title / locked strings.
static const char * const gCostumeInstrRotate  = (const char*)0x0054C96C; // "rotate"
static const char * const gCostumeInstrZoomIn  = (const char*)0x0054C964; // "zoom in"
static const char * const gCostumeInstrZoomOut = (const char*)0x0054C958; // "zoom out"
static const char * const gCostumeTitle        = (const char*)0x0054BC70; // "costume viewer"
static const char * const gCostumeLocked       = (const char*)0x0054BF98; // "??????"
// CDummy animation track lists (u16 animation ids, 0xffff terminated).
static const u16 gCostumeTrackA[] = { 0x0000, 0x0001, 0x0015, 0x0015, 0x0015, 0x000B, 0xFFFF };
static const u16 gCostumeTrackB[] = { 0x0000, 0x0004, 0x0032, 0x0033, 0x0032, 0x0033, 0x0032, 0x0033, 0x0014, 0xFFFF };
static const u16 gCostumeTrackC[] = { 0x0122, 0x0123, 0x0124, 0xFFFF };
// Alternative texture set table (0x552830), shared with Shell_CharacterViewer.
static u32 * const gAltTextureSet = (u32*)0x00552830;
// PC icon texture ids (PCTex.cpp), used for the on-screen control hints.
EXPORT extern i32 gPcIcons[5];
// @Ok
void Shell_CostumeViewer(void)
{
	print_if_false(gShellInitialized != 0, "Called Shell_CostumeViewer() without shell initialised");
	print_if_false(G_SAVE_GAME.field_7C < 10, "Bad GameState.CurrentCostume");

	Mess_SetScale(256);
	Mess_SetCurrentFont("sp_fnt03.fnt");

	CMenu* pMenu = new CMenu(24, 75, 1, 192, 192, 10);
	pMenu->scrollbar_one = 1;
	pMenu->scrollbar_zero = 0;
	pMenu->AdjustWidth(5);

	for (i32 i = 0; i < 10; i++)
	{
		if (G_SAVE_GAME.field_80 & (1 << i))
		{
			pMenu->AddEntry(gCostumeNames[i]);
			pMenu->mEntry[i].field_14 = 0x80;
			pMenu->mEntry[i].field_15 = 0x80;
			pMenu->mEntry[i].field_16 = 0x80;
			pMenu->mEntry[i].field_17 = 0x45;
			pMenu->mEntry[i].field_18 = 0x3C;
			pMenu->mEntry[i].field_19 = 0x6B;
		}
		else if (1 << i != 32)
		{
			pMenu->AddEntry(gCostumeLocked);
			pMenu->SetRedText(pMenu->mNumLines - 1);
		}
	}

	pMenu->Zoom(1);
	pMenu->SetLine(0);

	// find the current costume's line in the menu
	if (pMenu->mNumLines != 0)
	{
		i32 line = 0;
		SEntry* pEntry = pMenu->mEntry;
		while (!Utils_CompareStrings(pEntry->name, gCostumeNames[G_SAVE_GAME.field_7C]))
		{
			++line;
			++pEntry;
			if (line >= pMenu->mNumLines)
				break;
		}
		pMenu->SetLine(line);
	}

	gShellMenuEase = 384;

	i32 boxDelay = 5;
	CExpandingBox* pDescBox = 0;

	CDummy* pDummy = new CDummy("spidey", 50, 4096, -32, 0,
		(u16*)gCostumeTrackA, (u16*)gCostumeTrackB, (u16*)gCostumeTrackC, 0, 0, 0, 0);

	i32 texTransition = 0;
	pDummy->field_1D4 = 1;

	G_MIKE_CAMERA[0].Position.vx = 0;
	G_MIKE_CAMERA[0].Position.vy = 0;
	G_MIKE_CAMERA[0].Position.vz = 0;
	G_MIKE_CAMERA[0].Angles.vx = 0;
	G_MIKE_CAMERA[0].Angles.vy = 0;
	G_MIKE_CAMERA[0].Angles.vz = 0;
	G_MIKE_CAMERA[0].Style = 0;

	i32 zoom = 454;
	i32 titleScrollX = 0;

	while (1)
	{
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();

		i32 startVblanks = G_VBLANKS;

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1, -1);

		Mess_SetSort(4095);

		if (pMenu->FinishedZooming())
		{
			Mess_SetScale(256);
			Mess_SetCurrentFont("sp_fnt03.fnt");
			Mess_SetRGB(0x64, 0x64, 0x64, 0);
			Mess_SetRGBBottom(0x64, 100, 100);
			Mess_SetShadowRGB(0xFF);
			Mess_SetTextJustify(1);
			Mess_DrawText(75, 194, gCostumeInstrRotate, 0, 0x1000);
			Mess_DrawText(75, 211, gCostumeInstrZoomIn, 0, 0x1000);
			Mess_DrawText(75, 228, gCostumeInstrZoomOut, 0, 0x1000);
			PCGfx_DrawTexture2D(gPcIcons[0], 17, 181, 1.0f, 0xFF808080, 8, -3.0f);
			PCGfx_DrawTexture2D(gPcIcons[1], 45, 181, 1.0f, 0xFF808080, 8, -3.0f);
			PCGfx_DrawTexture2D(gPcIcons[3], 45, 198, 1.0f, 0xFF808080, 8, -3.0f);
			PCGfx_DrawTexture2D(gPcIcons[4], 45, 215, 1.0f, 0xFF808080, 8, -3.0f);
		}

		Mess_SetScale(256);
		Mess_SetCurrentFont("sp_fnt03.fnt");
		pMenu->Display();

		if (texTransition == 0)
		{
			M3dMaths_RotMatrixYXZ(&G_MIKE_CAMERA[0].Angles, &G_MIKE_CAMERA[0].Transform);
			TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);
			M3d_RenderSetup(G_MIKE_CAMERA, &G_VIEWPORT, G_PDOUBLE_BUFFER->OrderingTable);
			M3d_Render(pDummy);
			M3d_RenderCleanup();
			Bit_Display();
		}

		if (pDescBox != 0)
		{
			char* pDesc = (char*)gCostumeDescriptions[3 * (u8)G_SAVE_GAME.field_7C];
			if (pDesc != 0)
			{
				Mess_SetTextJustify(1);
				Mess_SetRGB(0x45, 0x3C, 0x6B, 0);
				i32 lineCount = 0;
				i32 y = 70;
				i32 page = 0;
				i32 colorCycle = 2;
				i32 colorVal = 2;
				if (--colorCycle != 0)
				{
					if (colorVal == 0)
						goto drawDesc;
					colorVal = colorVal - 1;
				}
				else
				{
					++page;
					colorCycle = 2;
					colorVal = 1;
				}
drawDesc:
				for (;;)
				{
					i8 b = (i8)*pDesc;
					if (*pDesc == 1)
						break;
					if (b == -1 || lineCount >= page)
						break;
					if (b == 2)
					{
						Mess_SetRGB(pDesc[1], pDesc[2], pDesc[3], 0);
						pDesc += 4;
					}
					else
					{
						if (colorVal != 0 && lineCount == page - 1)
							Mess_SetRGB(0xFF, 0xFF, 0xFF, 0);
						Mess_DrawText(325, y, pDesc, 0, 0x1000);
						y += 10;
						++pDesc;
						if (pDesc[-1] != 0)
							pDesc += strlen(pDesc) + 1;
						++lineCount;
					}
				}
			}
			pDescBox->Display();
		}

		Shell_DrawTitleBar(titleScrollX, 38, gCostumeTitle, 1, 0, 150, -21, 29);

		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);

		PCSHELL_DrawMouseCursor();

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		titleScrollX = PShell_MoveTowards(titleScrollX, 128);

		if (boxDelay != 0 && --boxDelay == 0)
		{
			pDescBox = new CExpandingBox(318, 58, 172, 117, 0, 0, 30, 15, 0);
		}

		if (texTransition != 0)
		{
			texTransition--;
			if (texTransition == 0)
				Spidey_LoadAlternativeTextureSet(gAltTextureSet, (u8)G_SAVE_GAME.field_7C + 1);
		}

		Mess_Update();

		if (texTransition == 0)
		{
			if (pMenu->mLine > 0x28)
				Pad_ClearTriggers(G_SCONTROL);
			Pad_Update();
			if (*gShellMenuAbort)
				return;
			CheckForPadUnplugged();
		}

		if (PCSHELL_CheckTriggers(131616, 1, 1))
			break;

		Mess_SetScale(256);
		Mess_SetCurrentFont("sp_fnt03.fnt");
		pMenu->Update();

		i32 mouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			const char* pName = pMenu->mEntry[pMenu->mLine].name;
			u8 just = pMenu->mJustification;
			i32 ex, ey;
			pMenu->GetEntryXY(pName, &ex, &ey);
			mouseOverText = PCSHELL_IsMouseOverText(pName, ex, ey, just);
		}

		if (pMenu->mLine < 0x28 && (mouseOverText || PCSHELL_CheckTriggers(65552, 1, 1)))
		{
			G_SCONTROL[0].Start.Triggered = 0;
			G_SCONTROL[0].X.Triggered = 0;

			i32 idx = 0;
			while (!Utils_CompareStrings(pMenu->mEntry[pMenu->mLine].name, gCostumeNames[idx]))
			{
				++idx;
				if (idx >= 10)
					goto denied;
			}

			if (idx == -1 || !(G_SAVE_GAME.field_80 & (1 << idx)))
			{
denied:
				SFX_Play(0x1B, 0x2000, 0);
			}
			else if (idx != (u8)G_SAVE_GAME.field_7C)
			{
				texTransition = 2;
				G_SAVE_GAME.field_7C = (u8)idx;
				SFX_Play(0x1F, 0x2000, 0);
			}
		}

		pDummy->AI();
		Bit_Move();
		Bit_RemoveDeadBits();

		i32 z = zoom;
		if (PCSHELL_CheckTriggers(0x100000, 0, 0))
		{
			z = zoom - 10;
			zoom -= 10;
		}
		if (PCSHELL_CheckTriggers(0x200000, 0, 0))
		{
			z += 10;
			zoom = z;
		}
		if (z >= 300)
		{
			if (z > 800)
				z = 800;
		}
		else
		{
			z = 300;
		}
		zoom = z;

		i32 rot = G_MIKE_CAMERA[0].Angles.vy;
		if (PCSHELL_CheckTriggers(16388, 0, 0))
			rot = G_MIKE_CAMERA[0].Angles.vy - 64;
		else if (PCSHELL_CheckTriggers(32776, 0, 0))
			rot = G_MIKE_CAMERA[0].Angles.vy + 64;
		rot &= 0xFFF;
		G_MIKE_CAMERA[0].Angles.vy = rot;

		i32 cosA = G_RCOSSIN_TBL[rot & 0xFFF].cos;
		G_MIKE_CAMERA[0].Position.vx = -(zoom * G_RCOSSIN_TBL[rot & 0xFFF].sin) >> 12;
		G_MIKE_CAMERA[0].Position.vz = -(zoom * cosA) >> 12;

	ail:
		if (G_VBLANKS == startVblanks)
			Pause(1);

		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		gsub_430680();
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}

		PCSHELL_Relax();
	}

	G_SCONTROL[0].Circle.Triggered = 0;
	SFX_Play(0x23, 0x2000, 0);
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	gsub_430680();
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);

	if (pMenu != 0)
		delete pMenu;
	if (pDescBox != 0)
		delete pDescBox;
	if (pDummy != 0)
		delete pDummy;

	Init_KillAll();
	PShell_NormalFont();
}

// @Ok
// The per-frame AI pass over one CItem list, as it is inlined into Shell_CharacterViewer
// (0x00496C7D and 0x00496D37). An item flagged 0x40 is on its way out: the first frame only
// sets 0x80, the next one deletes it. Everything else gets its interleaved AI step.
INLINE void CallAI(CBody *pList)
{
	CBody* pCur = pList;
	if (pCur)
	{
		for (
				CBody *pNext = reinterpret_cast<CBody*>(pCur->mNextItem);
				;
				pNext = reinterpret_cast<CBody*>(pNext->mNextItem))
		{
			if (pCur->mCBodyFlags & 0x40)
			{
				if (pCur->mCBodyFlags & 0x80)
				{
					delete pCur;
				}
				else
				{
					pCur->mCBodyFlags |= 0x80;
				}
			}
			else
			{
				pCur->InterleaveAI();
			}

			pCur = pNext;
			if (!pNext)
				break;
		}
	}
}

// 0x00553D18: the character viewer's table, 27 rows. It stays in game memory because
// Shell_DoShell fills the Description pointers there at runtime.
static SCharacterEntry * const gCharacters = (SCharacterEntry*)0x00553D18;
static const i32 NUM_CHARACTERS = 27;

// Pointer slots in game memory holding the on-screen control hints. The English image has
// "rotate" (0x0054C96C), "zoom in" (0x0054C964) and "zoom out" (0x0054C958) in them.
static char ** const gShellStrRotate = (char**)0x0054B918;
static char ** const gShellStrZoomIn = (char**)0x0054B91C;
static char ** const gShellStrZoomOut = (char**)0x0054B920;
// "character viewer" (0x0054BCB8) and the locked-entry label "? ? ? ?" (0x0054BF98).
static char ** const gShellStrCharacterViewer = (char**)0x0054BB98;
static char ** const gShellStrLockedCharacter = (char**)0x0054BAA4;

// The two render-distance floats this screen saves on entry and restores on exit. Same two
// addresses ps2m3d.cpp calls gM3dSuperScaleDist and bit.cpp calls gGlowNearThreshold; they are
// duplicated here because those are file-local statics in files this change does not own.
// Whoever owns ps2m3d.h should hoist them into one shared declaration.
static f32 * const gShellSuperScaleDist = (f32*)0x0055009C;
static f32 * const gShellGlowNearThreshold = (f32*)0x00547E3C;

// 0x0060D004, named JoelJewtCheatCode in the maintainer's IDB. Nonzero unlocks the
// "j james jewett" row (bit 26) of the character list.
static i32 * const gJoelJewtCheatCode = (i32*)0x0060D004;

// @Ok
// 0x4962D0, 3497 bytes. The Special menu's character viewer: a scrolling list of the 27
// characters on the left, the selected one previewed as a spinning CDummy in the middle and its
// description in an expanding box on the right. Reached from Shell_DoShell's Special dispatch.
// The layout mirrors Shell_CostumeViewer above; the differences are the per-character table
// (gCharacters), the per-mType extra render work in the switch, and the fact that picking a new
// character tears the CDummy down and builds a new one three frames later.
void Shell_CharacterViewer(void)
{
	print_if_false(gShellInitialized != 0, "Called Shell_CharacterViewer() without shell initialised");

	// both are restored on the way out, and by the switch below for every character except
	// Mysterio, who needs a much bigger draw distance for his head effect
	f32 savedSuperScaleDist = *gShellSuperScaleDist;
	f32 savedGlowNearThreshold = *gShellGlowNearThreshold;

	// the Joel Jewett cheat toggles the last row of the list on and off
	if (*gJoelJewtCheatCode != 0)
		G_SAVE_GAME.field_84 |= 0x4000000;
	else
		G_SAVE_GAME.field_84 &= ~0x4000000;

	Mess_SetScale(256);
	Mess_SetCurrentFont("sp_fnt03.fnt");

	CMenu* pMenu = new CMenu(40, 72, 1, 192, 192, 9);
	pMenu->scrollbar_one = 1;
	pMenu->field_1B = 12;
	pMenu->scrollbar_zero = 0;

	for (i32 i = 0; i < NUM_CHARACTERS; i++)
	{
		if (G_SAVE_GAME.field_84 & (1 << i))
		{
			pMenu->AddEntry(gCharacters[i].Name);
			pMenu->mEntry[pMenu->mNumLines - 1].unk_c = 0x69;
			pMenu->mEntry[pMenu->mNumLines - 1].unk_d = 0x69;
			pMenu->mEntry[pMenu->mNumLines - 1].unk_e = 0;
		}
		else if ((1 << i) != 0x4000000 && (1 << i) != 0x2000000)
		{
			// the two hidden rows stay out of the list entirely instead of showing as locked
			pMenu->AddEntry(*gShellStrLockedCharacter);
			pMenu->SetRedText(pMenu->mNumLines - 1);
		}
	}

	pMenu->NonGouraud();
	pMenu->Zoom(2);

	i32 transition = 0;
	i32 descPage = 0;
	i32 colorCycle = 2;
	i32 colorVal = 2;
	i32 titleScrollX = 0;
	i32 boxDelay = 5;
	CExpandingBox* pDescBox = 0;
	CDummy* pDummy = 0;
	i32 charIndex = 0;

	// the screen always opens on Spider-Man, found by his mType rather than by name
	for (i32 c = 0; c < NUM_CHARACTERS; c++)
	{
		if (gCharacters[c].Type == 50)
		{
			pDummy = new CDummy(gCharacters[c].ModelName,
					static_cast<i16>(gCharacters[c].Type), 4096,
					gCharacters[c].PosY, gCharacters[c].DefaultAnim,
					gCharacters[c].TrackA, gCharacters[c].TrackB, gCharacters[c].TrackC,
					gCharacters[c].TrackD, gCharacters[c].TrackE,
					gCharacters[c].CtorA12, gCharacters[c].CtorA13);
			pDummy->field_1D8 = 1;
			charIndex = c;
			break;
		}
	}

	print_if_false(pDummy != 0, "No Spiderman in Character[] array");

	G_MIKE_CAMERA[0].Position.vx = 0;
	G_MIKE_CAMERA[0].Position.vy = 0;
	G_MIKE_CAMERA[0].Position.vz = 0;
	G_MIKE_CAMERA[0].Angles.vx = 0;
	G_MIKE_CAMERA[0].Angles.vy = 0;
	G_MIKE_CAMERA[0].Angles.vz = 0;
	G_MIKE_CAMERA[0].Style = 0;

	i32 zoom = gCharacters[charIndex].Zoom;

	*(i32*)0x005512EC = 384;

	while (1)
	{
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();

		i32 startVblanks = G_VBLANKS;

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1, -1);

		Mess_SetSort(4095);

		if (pMenu->FinishedZooming())
		{
			Mess_SetScale(256);
			Mess_SetCurrentFont("sp_fnt03.fnt");
			Mess_SetRGB(0x64, 0x64, 0x64, 0);
			Mess_SetRGBBottom(0x64, 0x64, 0x64);
			Mess_SetShadowRGB(0xFF);
			Mess_SetTextJustify(1);
			Mess_DrawText(75, 194, *gShellStrRotate, 0, 0x1000);
			Mess_DrawText(75, 211, *gShellStrZoomIn, 0, 0x1000);
			Mess_DrawText(75, 228, *gShellStrZoomOut, 0, 0x1000);
			PCGfx_DrawTexture2D(gPcIcons[0], 17, 181, 1.0f, 0xFF808080, 8, -3.0f);
			PCGfx_DrawTexture2D(gPcIcons[1], 45, 181, 1.0f, 0xFF808080, 8, -3.0f);
			PCGfx_DrawTexture2D(gPcIcons[3], 45, 198, 1.0f, 0xFF808080, 8, -3.0f);
			PCGfx_DrawTexture2D(gPcIcons[4], 45, 215, 1.0f, 0xFF808080, 8, -3.0f);
		}

		Mess_SetScale(256);
		Mess_SetCurrentFont("sp_fnt03.fnt");
		pMenu->Display();

		if (transition == 0)
		{
			M3dMaths_RotMatrixYXZ(&G_MIKE_CAMERA[0].Angles, &G_MIKE_CAMERA[0].Transform);
			TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);
			M3d_RenderSetup(G_MIKE_CAMERA, &G_VIEWPORT, G_PDOUBLE_BUFFER->OrderingTable);

			// the Human Torch's flames are a wibbly-texture effect
			if (pDummy->mType == 704)
				M3d_PreprocessWibblyTextures(pDummy->mRegion);

			M3d_Render(pDummy);

			switch (pDummy->mType)
			{
				case 308:
				case 309:
					// Doc Ock and monster-Ock carry their four tentacles as separate items
					if (pDummy->field_214[0] != 0)
						M3d_Render(pDummy->field_214[0]);
					if (pDummy->field_214[1] != 0)
						M3d_Render(pDummy->field_214[1]);
					if (pDummy->field_214[2] != 0)
						M3d_Render(pDummy->field_214[2]);
					if (pDummy->field_214[3] != 0)
						M3d_Render(pDummy->field_214[3]);
					break;

				case 310:
					// the Scorpion's tail geometry is rebuilt and drawn every frame
					pDummy->TailRenderer();
					break;

				case 311:
					// Mysterio's head glow reaches far past the model
					*gShellGlowNearThreshold = 3850.0f;
					*gShellSuperScaleDist = 3850.0f;
					break;

				case 313:
					break;

				case 324:
					// the symbiote costume animates both its textures and its palette
					if (pDummy->field_1EC != -1)
					{
						M3d_PreprocessWibblyTextures(pDummy->field_1EC);
						M3d_PreprocessPulsingColours(pDummy->field_1EC);
					}
					break;

				default:
					*gShellSuperScaleDist = savedSuperScaleDist;
					*gShellGlowNearThreshold = savedGlowNearThreshold;
					break;
			}

			M3d_Render(BulletList);
			M3d_Render(MiscList);
			M3d_RenderCleanup();
			Bit_Display();
		}

		if (pDescBox != 0)
		{
			if (pDescBox->field_30 != 0 && gCharacters[charIndex].Description != 0)
			{
				Mess_SetTextJustify(1);
				Mess_SetRGB(0x45, 0x3C, 0x6B, 0);
				Mess_SetRGBBottom(0x45, 0x3C, 0x6B);

				char* pDesc = gCharacters[charIndex].Description;
				i32 lineCount = 0;
				i32 y = 70;

				// one more line of the description appears every other frame, and the newest
				// line is drawn white for a frame or two
				if (colorCycle != 0 && --colorCycle == 0)
				{
					++descPage;
					colorCycle = 2;
					colorVal = 1;
				}
				else if (colorVal != 0)
				{
					--colorVal;
				}

				for (;;)
				{
					i8 c = static_cast<i8>(*pDesc);

					if (c == 1)
						break;
					if (c == -1)
						break;
					if (static_cast<u32>(lineCount) >= static_cast<u32>(descPage))
						break;

					if (c == 2)
					{
						Mess_SetRGB(static_cast<u8>(pDesc[1]), static_cast<u8>(pDesc[2]),
								static_cast<u8>(pDesc[3]), 0);
						pDesc += 4;
					}
					else
					{
						if (colorVal != 0 && lineCount == descPage - 1)
							Mess_SetRGB(0xFF, 0xFF, 0xFF, 0);

						Mess_DrawText(320, y, pDesc, 0, 0x1000);
						y += 10;

						char first = *pDesc;
						++pDesc;
						if (first != 0)
						{
							while (*pDesc != 0)
								++pDesc;
							++pDesc;
						}

						++lineCount;
					}
				}
			}

			pDescBox->Display();
		}

		Shell_DrawTitleBar(titleScrollX, 38, *gShellStrCharacterViewer, 1, 0, 150, -21, 29);

		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);

		PCSHELL_DrawMouseCursor();

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		titleScrollX = PShell_MoveTowards(titleScrollX, 160);

		if (boxDelay != 0 && --boxDelay == 0)
		{
			pDescBox = new CExpandingBox(315, 58, 177, 117, 0, 0, 30, 15, 0);
		}

		// three frames after a new character is picked the old model is thrown away and the
		// new one built
		if (transition != 0 && --transition == 0)
		{
			if (pDummy != 0)
				delete pDummy;

			Init_KillAll();

			pDummy = new CDummy(gCharacters[charIndex].ModelName,
					static_cast<i16>(gCharacters[charIndex].Type), 4096,
					gCharacters[charIndex].PosY, gCharacters[charIndex].DefaultAnim,
					gCharacters[charIndex].TrackA, gCharacters[charIndex].TrackB,
					gCharacters[charIndex].TrackC, gCharacters[charIndex].TrackD,
					gCharacters[charIndex].TrackE,
					gCharacters[charIndex].CtorA12, gCharacters[charIndex].CtorA13);
			pDummy->field_1D8 = 1;

			zoom = gCharacters[charIndex].Zoom;

			G_MIKE_CAMERA[0].Position.vx = 0;
			G_MIKE_CAMERA[0].Position.vy = 0;
			G_MIKE_CAMERA[0].Position.vz = 0;
			G_MIKE_CAMERA[0].Angles.vx = 0;
			G_MIKE_CAMERA[0].Angles.vy = 0;
			G_MIKE_CAMERA[0].Angles.vz = 0;
			G_MIKE_CAMERA[0].Style = 0;
		}

		Mess_Update();

		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*(i32*)0x0054D38C != 0)
			return;
		CheckForPadUnplugged();

		if (PCSHELL_CheckTriggers(131616, 1, 1))
			break;

		Mess_SetScale(256);
		Mess_SetCurrentFont("sp_fnt03.fnt");
		pMenu->Update();

		i32 mouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			const char* pName = pMenu->mEntry[pMenu->mLine].name;
			u8 just = pMenu->mJustification;
			i32 ex, ey;
			pMenu->GetEntryXY(pName, &ex, &ey);
			mouseOverText = PCSHELL_IsMouseOverText(pName, ex, ey, just);
		}

		if (pMenu->mLine < 0x28 && (mouseOverText || PCSHELL_CheckTriggers(65552, 1, 1)))
		{
			G_SCONTROL[0].Start.Triggered = 0;
			G_SCONTROL[0].X.Triggered = 0;

			const char* pPicked = pMenu->mEntry[pMenu->mLine].name;

			i32 idx = 0;
			while (!Utils_CompareStrings(gCharacters[idx].Name, pPicked))
			{
				++idx;
				if (idx >= NUM_CHARACTERS)
					goto denied;
			}

			// idx can never be -1 out of the loop above, but the original still tests for it
			if (idx == -1 || !(G_SAVE_GAME.field_84 & (1 << idx))
					|| *gCharacters[idx].ModelName == 0)
			{
denied:
				SFX_Play(0x1B, 0x2000, 0);
			}
			else if (gCharacters[idx].Type != pDummy->mType)
			{
				transition = 3;
				charIndex = idx;
				descPage = 0;
				colorCycle = 2;
				colorVal = 2;
				SFX_Play(0x1F, 0x2000, 0);
			}
		}

		pDummy->AI();

		CallAI(BulletList);
		CallAI(MiscList);

		Bit_Move();
		Bit_RemoveDeadBits();

		if (PCSHELL_CheckTriggers(0x100000, 0, 0))
			zoom -= gCharacters[charIndex].ZoomStep;
		if (PCSHELL_CheckTriggers(0x200000, 0, 0))
			zoom += gCharacters[charIndex].ZoomStep;

		if (zoom < gCharacters[charIndex].MinZoom)
			zoom = gCharacters[charIndex].MinZoom;
		if (zoom > gCharacters[charIndex].MaxZoom)
			zoom = gCharacters[charIndex].MaxZoom;

		i32 rot = G_MIKE_CAMERA[0].Angles.vy;
		if (PCSHELL_CheckTriggers(16388, 0, 0))
		{
			rot = (G_MIKE_CAMERA[0].Angles.vy - 64) & 0xFFF;
			G_MIKE_CAMERA[0].Angles.vy = static_cast<i16>(rot);
		}
		else if (PCSHELL_CheckTriggers(32776, 0, 0))
		{
			rot = (G_MIKE_CAMERA[0].Angles.vy + 64) & 0xFFF;
			G_MIKE_CAMERA[0].Angles.vy = static_cast<i16>(rot);
		}
		rot &= 0xFFF;

		G_MIKE_CAMERA[0].Position.vx = -(zoom * G_RCOSSIN_TBL[rot].sin) >> 12;
		G_MIKE_CAMERA[0].Position.vz = -(zoom * G_RCOSSIN_TBL[rot].cos) >> 12;

		if (G_VBLANKS == startVblanks)
			Pause(1);

		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		gsub_430680();
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}

		PCSHELL_Relax();
	}

	G_SCONTROL[0].Circle.Triggered = 0;
	SFX_Play(0x23, 0x2000, 0);

	*gShellSuperScaleDist = savedSuperScaleDist;
	*gShellGlowNearThreshold = savedGlowNearThreshold;

	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	gsub_430680();
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");

	// two more background-only frames so the model is gone before the screen hands over
	for (i32 f = 0; f < 2; f++)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		gsub_430680();
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
	}

	Pad_ClearTriggers(G_SCONTROL);

	if (pMenu != 0)
		delete pMenu;
	if (pDescBox != 0)
		delete pDescBox;
	if (pDummy != 0)
		delete pDummy;

	Init_KillAll();
	PShell_NormalFont();
}

// @NotOk
// Not compared against 0x497690 yet, written from its disassembly.
// Tentative name. sub_497690 in tools/names.json (the Mac build only keeps the
// "kiddy" string). Draws the little Spider-Man that hangs next to the
// difficulty and training menus out of the "kiddy" anim frames: frame 0 is
// the web line, frame 1 the stretched body (its bottom edge moves with the
// scaled height of frame 1), frames 3 and 4 the kid mode pose, frame 2 the
// normal pose. x, y and the scale (0..256) come from the caller.
void Shell_DrawKiddy(SAnimFrame* pAnim, i32 x, i32 y, i32 kidMode, i32 scale)
{
	i32 stretch = (pAnim[1].Height * scale) >> 8;
	i32 top = y - stretch;

	POLY_FT4* pLine = (POLY_FT4*)Panel_DrawTexturedPoly(&pAnim[0], x + 8, top + 7, 0);
	DCPanel_DrawTexturedPoly(2.0f, pLine, &pAnim[0], x + 8, top + 7, 0x1C, 0x1B, 0, 0);

	POLY_FT4* pBody = (POLY_FT4*)Panel_DrawTexturedPoly(&pAnim[1], x - 4, top + 0x22, 0);
	if (pBody)
	{
		pBody->y2 = (i16)(pBody->y0 + stretch);
		pBody->y3 = (i16)(pBody->y1 + stretch);
		DCPanel_DrawTexturedPoly(2.0f, pBody, &pAnim[1], x + 2, top + 0x22, 0x24, 0x1A, 0, 0);
	}

	if (kidMode)
	{
		POLY_FT4* p = (POLY_FT4*)Panel_DrawTexturedPoly(&pAnim[3], x - 0xC, top + 0x11, 0);
		DCPanel_DrawTexturedPoly(2.0f, p, &pAnim[3], x - 0xC, top + 0x11, 0x14, 0x12, 0, 0);
		p = (POLY_FT4*)Panel_DrawTexturedPoly(&pAnim[4], x - 0x2E, top + 0xD, 0);
		DCPanel_DrawTexturedPoly(2.0f, p, &pAnim[4], x - 0x2E, top + 0xD, 0x16, 0xC, 0, 0);
	}
	else
	{
		POLY_FT4* p = (POLY_FT4*)Panel_DrawTexturedPoly(&pAnim[2], x - 6, top + 0x10, 0);
		DCPanel_DrawTexturedPoly(2.0f, p, &pAnim[2], x - 6, top + 0x10, 0xE, 0x12, 0, 0);
	}
}

// @Ok
i32 Shell_Difficulty(i32 a1)
{
	print_if_false(gShellInitialized != 0, "Called Shell_MainMenu() without shell initialised");
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(256, 0, 0, 256, 256, 16);
	pMenu->AddEntry("kid mode");
	pMenu->AddEntry("easy");
	pMenu->AddEntry("normal");
	pMenu->AddEntry("hard");
	pMenu->CentreY();
	pMenu->SetLine(2);
	pMenu->mY = 93;
	pMenu->Zoom(0);

	i32 v3 = 0;
	i32 v4 = 0;
	SAnimFrame* pAnim = Spool_FindAnim("kiddy", 1);
	i32 v32 = 0;
	i32 v30 = 0;
	i32 v31 = 256;
	i32 v28 = 0;
	i32 v35 = 0;
	i32 v34 = 0;
	i32 v40 = 0;
	i32 v6 = 0;
	i32 v19 = 0;
	i32 mLine = 0;
	i32 IsMouseOverText = 0;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		v40 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		v6 = pMenu->ChoiceIs("kid mode") && v32 == 0;
		Shell_DrawKiddy(pAnim, 321, v4, v6, v31);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		if (a1 != 0)
			Shell_DrawTitleBar(v3, 38, "new game", 1, 0, 150, -21, 29);
		else
			Shell_DrawTitleBar(v3, 38, "difficulty", 1, 0, 150, -21, 29);
		pMenu->Display();
		if (pMenu->FinishedZooming())
		{
			PShell_InstructionalText();
			Mess_DrawText(256, 190, "please select difficulty level", 0, 0x1000);
			if (a1 != 0 && v4 == 124)
			{
				i32 v7 = 0;
				while (G_SAVE_GAME.field_56[v7] == 0)
				{
					if (++v7 >= 34)
						goto label36;
				}
				Mess_SetTextJustify(1);
				i32 v8 = v35 & 0xFFF;
				v35 += 200;
				i32 sin = G_RCOSSIN_TBL[v8].sin;
				i32 v10 = ((68 * sin) >> 13) + 94;
				i32 v11 = 350 * (((59 * sin) >> 13) + 98) / 256;
				if (v11 > 255) v11 = -1;
				i32 v13 = 350 * v10 / 256;
				if (350 * v10 / 256 > 255) v13 = -1;
				i32 v14 = 350 * (((21 * sin) >> 13) + 117) / 256;
				if (v14 > 255) v14 = -1;
				Mess_SetRGB(v11, v13, v14, 0);
				i32 v15 = ((45 * sin) >> 13) + 84;
				i32 v16 = 350 * (((29 * sin) >> 13) + 54) / 256;
				if (v16 > 255) v16 = -1;
				i32 v17 = 350 * (((25 * sin) >> 13) + 47) / 256;
				if (v17 > 255) v17 = -1;
				i32 v18 = 350 * v15 / 256;
				if (v18 > 255) v18 = -1;
				Mess_SetRGBBottom(v16, v17, v18);
				Mess_DrawText(230, 28, "Warning!", 0, 0x1000);
				Mess_DrawText(230, 40, "Proceeding will erase", 0, 0x1000);
				Mess_DrawText(230, 52, "unsaved game progress!", 0, 0x1000);
			}
			label36:
			PShell_DefaultText();
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		v3 = PShell_MoveTowards(v3, 128);
		*(i32*)0x005512EC = PShell_MoveTowards(*(i32*)0x005512EC, 384);
		if (v32 == 0)
			break;
		v19 = v28 + 400;
		v28 += 400;
		if (v28 <= 2048)
			v31 = 256 - (G_RCOSSIN_TBL[v19 & 0xFFF].sin << 7 >> 12);
		else
		{
			v4 -= 15;
			v28 = 2048;
			v31 = 256;
			v30 = v4;
			if (v4 < -15)
			{
				v34 = 1;
				goto label85;
			}
		}
		label69:
		Mess_Update();
		if (G_VBLANKS == v40)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
		continue;
	}
	v30 = v4 + 15;
	if (v4 + 15 > 124)
	{
		v30 = 124;
		i32 v20 = v28 + 400;
		v28 += 400;
		if (v28 <= 2048)
			v31 = 256 - (G_RCOSSIN_TBL[v20 & 0xFFF].sin << 7 >> 12);
		else
		{
			v28 = 2048;
			v31 = 256;
		}
	}
	if (pMenu->mLine > 0x28)
		Pad_ClearTriggers(G_SCONTROL);
	Pad_Update();
	if (*(i32*)0x0054D38C != 0)
		return 0;
	CheckForPadUnplugged();
	mLine = (u8)pMenu->mLine;
	pMenu->Update();
	if (mLine != (u8)pMenu->mLine && pMenu->mLine == 0 && *(u8*)0x00682770 == 0)
	{
		i32 v22 = Rnd(10);
		Redbook_XAPlay(((i32*)0x00554610)[2 * v22], ((i32*)0x00554614)[2 * v22], 0);
	}
	if (PCSHELL_CheckTriggers(256, 1, 1))
	{
		i32 x, y;
		pMenu->GetEntryXY(pMenu->mEntry[pMenu->mLine].name, &x, &y);
		IsMouseOverText = PCSHELL_IsMouseOverText(pMenu->mEntry[pMenu->mLine].name, x, y, pMenu->mJustification);
	}
	// 0x4A161D: the slide position takes the clamped value every frame,
	// this is what moves the little Spider-Man down into view.
	v4 = v30;
	if (pMenu->mLine < 0x28 && (IsMouseOverText != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
	{
		G_SCONTROL[0].Start.Triggered = 0;
		G_SCONTROL[0].X.Triggered = 0;
		i32 v25;
		switch (pMenu->mLine)
		{
		case 0:
			v25 = 0;
			*(u8*)0x0060CFC7 = 1;
			DifficultyLevel = 0;
			goto label65;
		case 1:
			v25 = 1;
			goto label63;
		case 2:
			v25 = 2;
			goto label63;
		case 3:
			v25 = 3;
			label63:
			DifficultyLevel = v25;
			goto label64;
		default:
			v25 = DifficultyLevel;
			if (DifficultyLevel != 0)
				label64:
				*(u8*)0x0060CFC7 = 0;
			else
				*(u8*)0x0060CFC7 = 1;
			label65:
			G_SAVE_GAME.mDifficulty = v25;
			if (pMenu->mLine != 0)
			{
				SFX_Play(0x1F, 0x2000, 0);
				v34 = 1;
				goto label84;
			}
			SFX_Play(0x1F, 0x2000, 0);
			v32 = 1;
			v28 = 0;
			v30 = 124;
			break;
		}
	}
	if (!PCSHELL_CheckTriggers(131616, 1, 1))
		goto label69;
	G_SCONTROL[0].Circle.Triggered = 0;
	SFX_Play(0x23, 0x2000, 0);
	label84:
	label85:
	delete pMenu;
	Init_KillAll();
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
	Redbook_XAStop();
	if (v34 != 0)
		G_SAVE_GAME.field_78 = 1;
	return v34;
}

// @Ok
void Shell_DisplayGameInfo(
		i32 a1,
		i32 a2,
		SSaveGame* pSave)
{
	if (pSave->mChecksum)
	{
		u32 checksum = Shell_CalculateGameChecksum(pSave);

		if (pSave->mChecksum == checksum)
		{
			SLevel* pLevel = Front_FindLevel(pSave->field_4);
			if (pLevel)
			{
				Mess_SetTextJustify(1);
				Mess_DrawText(a1, a2, pLevel->mDisplayName, 0, 0x1000);

				const char* v9 = G_RENDER_BUF;

				switch (pSave->mDifficulty)
				{
					case 0:
						v9 = "kid mode";
						break;
					case 1:
						v9 = "easy";
						break;
					case 2:
						v9 = "normal";
						break;
					case 3:
						v9 = "hard";
						break;
				}

				Mess_DrawText(a1, a2 + 15, "difficulty:", 0, 0x1000);
				Mess_DrawText(a1 + 150, a2 + 15, v9, 0, 0x1000);
			}
		}
	}
}

// Investigated this session (address confirmed real: 0x4A1A80, 2334 bytes,
// names.json). This is the top-level game/menu state machine: sets up the
// costume-name string table (charbio.dat parsing), then runs the main
// switch that dispatches to Shell_MainMenu (sub_493990), the survival
// arena chooser (sub_49A3B0, also confirmed as the "Special" costume-code
// entry screen), the training menu (sub_4970B0's code loop, calling
// sub_49EBA0/sub_49F2F0/sub_49FAF0/sub_50CE70/sub_4977D0/sub_50D9B0/
// sub_498450), the "Special" menu (sub_49CCB0's code loop, calling
// Shell_CharacterViewer/sub_49D230/Shell_ComicCollection/Shell_GameCovers/
// sub_49D6E0), and a second switch for Shell_CostumeViewer/sub_49E5A0/
// Shell_RollCredits/sub_4A0AD0/sub_4A0F40. Confirmed this is genuinely the
// missing link between main.cpp's SpideyMain() stub and the real menu
// system (per the maintainer's flag on this being valuable), but decompiling
// it needs roughly 15 more large, entirely undecompiled functions first
// (sub_4970B0, sub_49EBA0, sub_49F2F0, sub_49FAF0, sub_50CE70, sub_4977D0,
// sub_50D9B0, sub_498450, sub_49A3B0, sub_49ACE0, sub_49CCB0, sub_49D230,
// sub_49D6E0, sub_49E5A0, sub_4A0AD0, sub_4A0F40, sub_4A1930, sub_4A17A0),
// none named in names.json, most 1-2KB each by their disasm size. That is
// a multi-session undertaking on its own, well past this session's budget.
// Left as a stub with this map so the next session does not have to
// re-trace the whole dispatch tree from scratch.

// Shell_DoShell data (original .rdata/.bss addresses).
static i32 * const gDoShellSaveLevelCode = (i32*)0x00550E0C;   // dword_550E0C
static const char * const gDoShellLevelCodeStr = (const char*)0x00550E4F; // byte_550E4F
static i32 * const gDoShellSaveA = (i32*)0x00550E58;           // dword_550E58
static i32 * const gDoShellSaveB = (i32*)0x00550E5C;           // dword_550E5C
static i32 * const gDoShellSaveC = (i32*)0x00550E60;           // dword_550E60
static u8 * const gDoShellSaveD = (u8*)0x00550E89;             // byte_550E89
static u8 * const gDoShellSaveE = (u8*)0x00550E8A;             // byte_550E8A
static i32 * const gDoShellForceLevelExit = (i32*)0x0068293C;  // = gPshellForceLevelExit
static u8 * const gDoShellQuitFlag = (u8*)0x0060CF88;          // byte_60CF88
static i32 * const gDoShellSpecialFlag = (i32*)0x0060CFD8;     // dword_60CFD8
static u8 * const gDoShellKidModeFlag = (u8*)0x0060CFC7;       // byte_60CFC7
static i32 * const gDoShellShowTitle = (i32*)0x0054D38C;       // dword_54D38C
static SMovieDetails * const gMovieDetails = (SMovieDetails*)0x0054F2E8; // movieDetails

// sub_47A350: returns NUMCOSTUMES (10). Forward to original.
typedef i32 (*DoShell_NumCostumes_t)(void);
// @Bogus
static i32 DoShell_NumCostumes(void)
{
#ifdef SPIDEY_STANDALONE
	return 10;   // the whole of sub_47A350: "mov eax,0Ah; ret"
#else
	DoShell_NumCostumes_t func = (DoShell_NumCostumes_t)0x0047A350;
	return func();
#endif
}

// sub_49A3B0: training/arena chooser. Forward to original.
typedef i32 (*DoShell_TrainingChooser_t)(i32);
// @Bogus
static i32 DoShell_TrainingChooser(i32 a1)
{
#ifdef SPIDEY_STANDALONE
	// @TODO Phase 2: sub_49A3B0 (2326 bytes, the training/arena menu) is
	// not decompiled yet. Acts as "player backed out".
	printf("DoShell_TrainingChooser(%d): not available in the standalone build yet\n", a1);
	return 0;
#else
	DoShell_TrainingChooser_t func = (DoShell_TrainingChooser_t)0x0049A3B0;
	return func(a1);
#endif
}

// Find the description for the given name in charbio.dat.
// Returns the offset into gBiographies of the description, or 0 if not found.
// @Bogus
static i32 DoShell_FindBioDesc(const char *pName)
{
	const char *pBio = (const char*)gBiographies;
	i32 result = 0;
	i8 type = *pBio;
	if (type != 0xFF)
	{
		i32 found = 0;
		while (1)
		{
			++pBio;
			if (type == 1 && Utils_CompareStrings(pBio, pName))
			{
				found = 1;
				break;
			}
			type = *pBio;
			if (type == 0xFF)
				break;
		}
		if (found)
		{
			i8 first = *pBio;
			result = (i32)(pBio + 1);
			if (first != 0)
				result += strlen((const char*)result) + 1;
		}
	}
	return result;
}

// @Ok
void Shell_DoShell(const u32 *a1,u32 *)
{
	// The original takes a single pointer param which points to an array of
	// two flags: *a1 = fromGame, *(a1+1) = rollCredits. The repo stub uses
	// (const u32*,u32*) for the function table; we read the two flags from
	// the first param and ignore the second.
	const i32 *pFlags = (const i32*)a1;
	i32 fromGame = pFlags[0];
	i32 rollCredits = pFlags[1];

	DXINIT_SetDisplayOptions(640, 480, 16, gLowGraphics, gBrightnessRelated);
	PCINPUT_SetMouseBounds(0, 0, 608, 448);
	PCINPUT_SetMousePosition(304, 224);
	gShellFromGame = 0;
	PShell_Initialise();
	Spidey_LoadAlternativeTextureSet(gAltTextureSet, (i32)(u8)G_SAVE_GAME.field_7C + 1);
	print_if_false(gBiographies == 0, "pBios not NULL?");

	i32 fileHandle = FileIO_Open("charbio.dat");
	gBiographies = DCMem_New((u32)fileHandle, 1, 1, 0, 1);
	FileIO_Load(gBiographies);
	FileIO_Sync();

	// Parse biographies (27-entry table, 68-byte stride, name at offset -8).
	{
		i32 *pDesc = (i32*)0x00553D24;
		const i32 *pEnd = (i32*)0x00554450;
		while (pDesc < pEnd)
		{
			const char *pName = (const char*)*(pDesc - 2);
			*pDesc = DoShell_FindBioDesc(pName);
			pDesc += 17;
		}
	}

	i32 numCostumes = DoShell_NumCostumes();
	print_if_false(numCostumes == 10, "NUMCOSTUMES mismatch");
	print_if_false(1, "Bad NUMCOSTUMES");

	// Parse the 10 costume descriptions from charbio.dat.
	{
		static const char *gCostumeDescNames[10] = {
			"spider-man", "Spider-man 2099", "symbiote spider-man", "Captain Universe",
			"Spidey unlimited", "Amazing bag man", "Scarlet Spidey", "Ben Riley",
			"Quick Change Spidey", "Peter Parker"
		};
		i32 *pCostumeDesc = (i32*)0x0055459C;  // costume_descriptions
		for (i32 i = 0; i < 10; i++)
		{
			pCostumeDesc[3 * i] = DoShell_FindBioDesc(gCostumeDescNames[i]);
		}
	}

	// Movie-name matching (21-entry table at 0x55444C, 16-byte stride).
	// Each entry: [level_movie_code, movie_index, count, description].
	// Match movieDetails[i].name against the level_movie_code (case-insensitive).
	{
		i32 *pTable = (i32*)0x0055444C;
		const i32 *pEnd = (i32*)0x0055459C;  // costume_descriptions
		while (pTable < pEnd)
		{
			i32 movieIndex = 0;
			i32 numMovies = GameFMV_GetNumMovies();
			if (numMovies > 0)
			{
				const SMovieDetails *pMovie = gMovieDetails;
				while (movieIndex < numMovies)
				{
					const char *pName = pMovie->name;
					if (pName[0] != 0)
					{
						const char *pCode = (const char*)pTable[0];
						i32 match = 1;
						for (i32 i = 0; pCode[i] != 0 && pName[i] != 0; i++)
						{
							i8 c1 = pCode[i];
							i8 c2 = pName[i];
							if (c1 >= 'A' && c1 <= 'Z') c1 += 32;
							if (c2 >= 'A' && c2 <= 'Z') c2 += 32;
							if (c1 != c2)
							{
								match = 0;
								break;
							}
						}
						// Both strings must end at the same time.
						if (match && pCode[strlen(pCode)] != pName[strlen(pName)])
							match = 0;
						if (match)
						{
							pTable[1] = movieIndex;
							break;
						}
					}
					pMovie++;
					movieIndex++;
				}
			}
			pTable += 4;
		}
	}

	// Credits / force-exit setup.
	if (*gDoShellForceLevelExit != 0)
	{
		*gDoShellShowTitle = 1;
		G_SAVE_GAME.field_7B = (u8)*gDoShellSaveLevelCode;
	}

	i32 v56 = 0;
	*gDoShellForceLevelExit = 0;
	G_PAD_IDLE_TIME = 0;
	gPshellArmorRealted = 0;
	i32 v57 = (G_SAVE_GAME.field_78 != 0) + 1;
	Shell_LegalScreen();

	while (1)
	{
		// Show title screen if needed.
		if (*gDoShellShowTitle != 0)
		{
			*gDoShellShowTitle = 0;
			*(u8*)&Levels[35].mName = 1;
			Shell_TitleScreen();
			*(u8*)&Levels[35].mName = 0;
		}
		// If v56 != 0, enter the level.
		if (v56 != 0)
			goto enterLevel;
		// Get the menu selection.
		if (fromGame != 0)
			v57 = 5;
		else
			v57 = Shell_MainMenu((EShellResult)v57);
		switch (v57)
		{
			case 1:  // new game
				if (Shell_Difficulty(1) == 0)
					continue;  // retry menu
				Utils_CopyString("l1a1_t", G_SAVE_GAME.field_4, 9);
				Utils_CopyString("Re_Start_death", G_SAVE_GAME.mRestartPointName, 50);
				Utils_CopyString(gDoShellLevelCodeStr, (char*)&G_SAVE_GAME.field_3F, 9);
				*(i32*)((u8*)&G_SAVE_GAME + 0x4C) = *gDoShellSaveB;
				*(i32*)((u8*)&G_SAVE_GAME + 0x48) = *gDoShellSaveA;
				*(i32*)((u8*)&G_SAVE_GAME + 0x50) = *gDoShellSaveC;
				((u8*)&G_SAVE_GAME)[0x7A] = *gDoShellSaveE;
				((u8*)&G_SAVE_GAME)[0x79] = *gDoShellSaveD;
				memset(&G_SAVE_GAME.field_56[0], 0, 0x20);
				*(i16*)((u8*)&G_SAVE_GAME + 0x76) = 0;
				G_SAVE_GAME.mDifficulty = (i8)DifficultyLevel;
				G_SAVE_GAME.field_78 = 1;
				Spidey_LoadAlternativeTextureSet(gAltTextureSet, (i32)(u8)G_SAVE_GAME.field_7C + 1);
				goto enterLevel;
			case 2:  // continue
				goto enterLevel;
			case 4:  // options
			{
				i32 v58 = Shell_Options((EShellResult)15);
				if (v58 == 0)
					continue;  // retry menu
				do
				{
					switch (v58)
					{
						case 3:
						{
							i32 v59 = 20;
							while (1)
							{
								i32 v60 = Shell_MemoryCard((EShellResult)v59);
								v59 = v60;
								if (v60 == 0)
									break;
								if (v60 == 20 && Shell_LoadGame() != 0)
								{
									v57 = 0;
									goto optionsLoop;
								}
								if (v59 == 21)
								{
									i32 saveFlag = 0;
									i32 rcFlag = rollCredits;
									Shell_SaveGame((const u32*)&saveFlag, (u32*)&rcFlag);
									if (rcFlag != 0)
										goto optionsLoop;
								}
							}
							break;
						}
						case 14:
							PCSHELL_DoControllerConfig(1);
							break;
						case 15:
							PCSHELL_DoControllerConfig(0);
							break;
						case 16:
							Shell_SFXMusic();
							break;
						case 17:
							PCSHELL_DoDisplayOptions();
							break;
						case 19:
							Shell_ScreenAdjust();
							break;
						default:
							break;
					}
					optionsLoop:
					v58 = Shell_Options((EShellResult)v58);
				}
				while (v58 != 0);
				continue;  // retry menu
			}
			case 5:  // training
			{
				i32 v61 = 0;
				while (v61 != 0 || Shell_ChooseTrainingControlType() != 0)
				{
					v61 = DoShell_TrainingChooser(0);
					if (v61 != 0)
					{
						if (G_SAVE_GAME.field_7C != 0)
						{
							G_SAVE_GAME.field_7C = 0;
							Spidey_LoadAlternativeTextureSet(gAltTextureSet, 1);
						}
						v56 = 1;
						gPshellArmorRealted = 1;
						break;
					}
				}
				continue;  // retry menu
			}
			case 6:
				DoShell_TrainingChooser(1);
				continue;  // retry menu
			case 7:  // gallery
			{
				i32 j = Shell_Gallery((EShellResult)8);
				while (j != 0)
				{
					switch (j)
					{
						case 8:
							Shell_CharacterViewer();
							break;
						case 9:
							Shell_MovieViewer();
							break;
						case 10:
							Shell_ComicCollection();
							break;
						case 11:
							Shell_GameCovers();
							break;
						case 12:
							Shell_StoryBoards();
							break;
						default:
							break;
					}
					j = Shell_Gallery((EShellResult)j);
				}
				continue;  // retry menu
			}
			case 13:  // special
			{
				i32 v62 = Shell_Special((EShellResult)22);
				if (v62 == 0)
					continue;  // retry menu
				while (1)
				{
					switch (v62)
					{
						case 22:
							Shell_CostumeViewer();
							goto specialLoop;
						case 23:
							Shell_Cheats();
							goto specialLoop;
						case 24:
							Shell_RollCredits();
							goto specialLoop;
						case 25:
							if (*gDoShellSpecialFlag == 0)
							{
								if (Shell_LevelSelect() != 0)
									goto enterLevel;
								goto specialLoop;
							}
							if (Shell_LevelSelect() == 0)
							{
								specialLoop:
								v62 = Shell_Special((EShellResult)v62);
								if (v62 == 0)
									break;  // retry main menu
								continue;
							}
							while (Shell_Difficulty(0) == 0)
							{
								if (Shell_LevelSelect() == 0)
									goto specialLoop;
							}
							goto enterLevel;
						default:
							goto specialLoop;
					}
				}
				continue;  // retry menu
			}
			case 18:  // quit
				*gDoShellQuitFlag = 1;
				PShell_Cleanup();
				return;
			default:
				continue;  // retry menu
		}
	}

enterLevel:
	if (*gDoShellForceLevelExit != 0)
	{
		i32 v65 = (u8)G_SAVE_GAME.field_7B;
		G_SAVE_GAME.field_7B = 0;
		*gDoShellSaveLevelCode = v65;
	}
	if (gPshellArmorRealted == 0)
		*gDoShellKidModeFlag = (DifficultyLevel == 0);
	Utils_InitialRand(G_VBLANKS);
	for (i32 k = 10000; k != 0; --k)
		Rnd(10);
	PShell_Cleanup();
}

// @Ok
// @Matching
void Shell_DrawBackground(void)
{
	if (!gBackgroundAnimFrame)
		Spool_AnimAccess("menubg", &gBackgroundAnimFrame);

	PCPanel_DrawTexturedPoly(-1.0, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
}

// @Ok
// @Matching
void Shell_DrawTitleBar(
		i32 a1,
		i32 a2,
		const char * a3,
		i32 a4,
		i32 a5,
		i32 a6,
		i32 a7,
		i32 a8)
{
	Mess_SetTextJustify(0);
	Mess_SetRGB(0x80u, 0x80u, 0x80u, 0);
	Mess_SetRGBBottom(0x45u, 60, 107);
	if (a4)
		PShell_BigFont();
	Mess_SetSort(4094);
	Mess_DrawText(a1, a2, a3, 0, 0x1000u);

	i32 v8;
	if ( a5 < a1 )
		v8 = a6 + a1 + Mess_TextWidth(a3) / 2 - a5;
	else
		v8 = a1 - Mess_TextWidth(a3) / 2 - a5 - a6;
	PShell_DrawHighlight(a5, a7 + a2, v8, a8);
	Mess_SetSort(0);
	if (a4)
		PShell_NormalFont();
}

// @Ok
i32 Shell_Gallery(EShellResult a1)
{
	print_if_false(gShellInitialized != 0, "Called Shell_MainMenu() without shell initialised");
	i32 v9 = 0;
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(256, 0, 0, 256, 256, 16);
	pMenu->AddEntry("Character viewer");
	pMenu->AddEntry("movie viewer");
	pMenu->AddEntry("comic collection");
	pMenu->AddEntry("game covers");
	pMenu->AddEntry("storyboards");
	pMenu->CentreY();
	pMenu->Zoom(0);
	switch (a1)
	{
	case 8:
		pMenu->SetLine(0);
		break;
	case 9:
		pMenu->SetLine(1);
		break;
	case 10:
		pMenu->SetLine(2);
		break;
	case 11:
		pMenu->SetLine(3);
		break;
	case 12:
		pMenu->SetLine(4);
		break;
	default:
		print_if_false(0, "Bad default");
		break;
	}
	if (G_SAVE_GAME.mCheatStoryboardFlag == 0)
	{
		pMenu->SetRedText(4);
		pMenu->mEntry[4].unk_c = 100;
		pMenu->mEntry[4].field_11 = 64;
		pMenu->mEntry[4].field_14 = 100;
		pMenu->mEntry[4].field_17 = 64;
	}

	i32 v4 = 0;
	i32 circleExit = 0;
	*(i32*)0x005512EC = 0;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 v12 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(v4, 38, "gallery", 1, 0, 150, -21, 29);
		pMenu->Display();
		if (pMenu->FinishedZooming())
		{
			PShell_InstructionalText();
			switch (pMenu->mLine)
			{
			case 0:
				Mess_DrawText(256, 187, "view character models from the game", 0, 0x1000);
				break;
			case 1:
				Mess_DrawText(256, 187, "view movies from the game", 0, 0x1000);
				break;
			case 2:
				Mess_DrawText(256, 187, "view comics collected from the game", 0, 0x1000);
				break;
			case 3:
				Mess_DrawText(256, 187, "view the game covers", 0, 0x1000);
				break;
			case 4:
				Mess_DrawText(256, 187, "view the original storyboards for", 0, 0x1000);
				Mess_DrawText(256, 200, "creating the game movies", 0, 0x1000);
				break;
			default:
				break;
			}
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		v4 = PShell_MoveTowards(v4, 128);
		*(i32*)0x005512EC = PShell_MoveTowards(*(i32*)0x005512EC, 384);
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return 0;
		CheckForPadUnplugged();
		pMenu->Update();
		if (PCSHELL_CheckTriggers(131616, 1, 1))
		{
			circleExit = 1;
			break;
		}
		i32 IsMouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			u8 mJustification = pMenu->mJustification;
			const char* name = pMenu->mEntry[pMenu->mLine].name;
			i32 x, y;
			pMenu->GetEntryXY(name, &x, &y);
			IsMouseOverText = PCSHELL_IsMouseOverText(name, x, y, mJustification);
		}
		if (pMenu->mLine < 0x28 && (IsMouseOverText != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
		{
			G_SCONTROL[0].Start.Triggered = 0;
			G_SCONTROL[0].X.Triggered = 0;
			switch (pMenu->mLine)
			{
			case 0:
				v9 = 8;
				break;
			case 1:
				v9 = 9;
				break;
			case 2:
				v9 = 10;
				break;
			case 3:
				v9 = 11;
				break;
			case 4:
				if (G_SAVE_GAME.mCheatStoryboardFlag != 0)
					v9 = 12;
				break;
			default:
				break;
			}
			if (v9 != 0)
			{
				SFX_Play(0x1F, 0x2000, 0);
				break;
			}
			SFX_Play(0x1B, 0x2000, 0);
		}
		if (G_VBLANKS == v12)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
	}

	if (circleExit)
	{
		G_SCONTROL[0].Circle.Triggered = 0;
		SFX_Play(0x23, 0x2000, 0);
	}
	delete pMenu;
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
	return v9;
}

// @Ok
// Real translation, 0x0049C1A0, 122 bytes (names.json). Same shape as
// Shell_DrawComicHighlightBox above, but for the (larger) Game Covers
// grid: cell is 80x64 pixels, centred at (x+40, y+32), half-width is
// (40*amount)>>8, half-height is (i8)(amount>>3). Gets its POLY_FT4
// through the SAnimFrame* overload of Panel_DrawTexturedPoly (0x462B90,
// "Panel_DrawTexturedPoly_0" in names.json, forwards to the Texture*
// overload internally) rather than dereferencing pFrame->pTexture
// directly like the Comic Collection helper does -- confirmed from
// Hex-Rays to be a real difference between the two original functions
// (they pass different arguments to two different real addresses), not a
// transcription slip. Only caller is the not-yet-decompiled
// Shell_GameCovers (0x49C220); functional only, no null check on the
// Panel_DrawTexturedPoly return, matching the original.
void Shell_DrawGameCoverHighlightBox(i16 x, i16 y, SAnimFrame *pFrame, i32 amount)
{
	if (amount >= 0)
	{
		POLY_FT4 *poly = (POLY_FT4*)Panel_DrawTexturedPoly(pFrame, 0);

		i16 halfW = (i16)((40 * amount) >> 8);
		i16 x0 = (i16)(x - halfW + 40);
		i16 x1 = (i16)(x + halfW + 40);
		poly->x1 = x1;
		poly->x0 = x0;
		poly->x2 = x0;
		poly->x3 = x1;

		i16 halfH = (i16)(i8)(amount >> 3);
		i16 y0 = (i16)(y - halfH + 32);
		i16 y1 = (i16)(y + halfH + 32);
		poly->y0 = y0;
		poly->y1 = y0;
		poly->y2 = y1;
		poly->y3 = y1;

		DCPanel_DrawTexturedPoly(1.0f, poly, pFrame, 0);
	}
}

// Address confirmed real this session: 0x49C220, 2675 bytes (names.json).
// Called from Shell_DoShell's (0x4A1A80) "Special" menu dispatch (case 7,
// sub_49CCB0's menu-code loop, code 11). Full functional translation
// 2026-08-31, once CShellPreviewIcon (shell.h) was reverse engineered: the
// six previously-undiscovered 420-byte objects are `new CShellPreviewIcon(x,
// y, 500)`, positions read straight off the disassembly (already <<12
// shifted immediates, divided back out here): (-166,-15), (3,-15),
// (172,-15) for the top row and (-168,126), (0,126), (172,126) for the
// bottom row, all at depth 500. Grid is a 3x2 layout (column = index%3 via
// "index>2 ? index-3 : index", row y = 50 or 128, x = 150*column+62).
// pLoadingBox is a one-shot "loading overlay" CExpandingBox, sized to match
// grid cell 0 (62/50/90/70). While it is non-null the grid itself doesn't
// draw or take input at all -- just Display()s the box and waits for its
// field_30 completion flag -- matching the same overlay idiom already
// described above on the not-yet-implemented Shell_ComicCollection. Once
// field_30 is set the box is deleted and never recreated for the rest of
// this function (confirmed against the real 0x49C220 disassembly: there is
// only this one CExpandingBox construction in the whole function).
// dword_6828E8 is the per-cover "unlocked" bitmask. That address is
// gSaveGame + 0x90, so it is G_SAVE_GAME.field_90 (front.h), not a global of
// its own; same role/shape as Shell_ComicCollection's field_8C for comics.
// "l*cov.bmp" cover art filenames use a fixed per-slot digit
// ('1','2','4','5','7','8') baked into the switch below, matching the
// original's literal byte patches (not a linear index -- reproduced as-is,
// not "fixed").

// @Ok
void Shell_GameCovers(void)
{
	// Hex-Rays rendered the first call as a zero-arg "nullsub_1();", but the
	// raw disasm at 0x49c220 shows it is really print_if_false(gShellInitialized
	// != 0, "Called Shell_GameCovers() without shell initialised") -- same
	// idiom as CheckForPadUnplugged above (Hex-Rays just doesn't know
	// nullsub_1/print_if_false's real prototype, so it drops the pushed
	// args from the pseudocode even though they're really there). Caught by
	// the cmpsum sanity check (591 diffs starting at instruction 1) per
	// CLAUDE.md's "verify the hypothesis... re-read the disassembly" rule.
	print_if_false(gShellInitialized != 0, "Called Shell_GameCovers() without shell initialised");

	i32 selected = 0;

	CExpandingBox *pLoadingBox = new CExpandingBox(62, 50, 90, 70, 0, 0, 30, 15, 0);

	i32 repeatDelay = 0;
	SAnimFrame *pFrames = Spool_FindAnim("covers", 1);

	i32 cellAmount[6];
	for (i32 i = 0, v = 0; v > -360; i++, v -= 60)
		cellAmount[i] = v;

	CShellPreviewIcon *pIcons[6];
	pIcons[0] = new CShellPreviewIcon(-166, -15, 500);
	pIcons[1] = new CShellPreviewIcon(3, -15, 500);
	pIcons[2] = new CShellPreviewIcon(172, -15, 500);
	pIcons[3] = new CShellPreviewIcon(-168, 126, 500);
	pIcons[4] = new CShellPreviewIcon(0, 126, 500);
	pIcons[5] = new CShellPreviewIcon(172, 126, 500);

	G_MIKE_CAMERA[0].Position.vx = 0;
	G_MIKE_CAMERA[0].Position.vy = 0;
	G_MIKE_CAMERA[0].Position.vz = 0;
	G_MIKE_CAMERA[0].Angles.vx = 0;
	G_MIKE_CAMERA[0].Angles.vy = 0;
	G_MIKE_CAMERA[0].Angles.vz = 0;
	G_MIKE_CAMERA[0].Style = 0;

	i32 titleScrollX = 0;
	gShellMenuEase = 384;

	while (1)
	{
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();

		i32 startVblanks = G_VBLANKS;

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1, -1);

		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);

		Shell_DrawTitleBar(titleScrollX, 38, "game covers", 1, 0, 150, -21, 29);

		M3dMaths_RotMatrixYXZ(&G_MIKE_CAMERA[0].Angles, &G_MIKE_CAMERA[0].Transform);
		TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);
		M3d_RenderSetup(G_MIKE_CAMERA, &G_VIEWPORT, G_PDOUBLE_BUFFER->OrderingTable);

		CExpandingBox *pActiveBox;

		if (pLoadingBox != 0)
		{
			pLoadingBox->Display();
			pActiveBox = pLoadingBox;
		}
		else
		{
			SAnimFrame *pFrame = pFrames;
			for (i32 i = 0; i < 6; i++)
			{
				i32 col = (i > 2) ? i - 3 : i;
				i32 y = (i > 2) ? 128 : 50;
				i32 x = 150 * col + 62;

				if (i == selected)
					PShell_DrawMenuBox(x, y, 90, 70, 0, 0, 0, 0);

				if (G_SAVE_GAME.field_90 & (1 << i))
				{
					Shell_DrawGameCoverHighlightBox((i16)(x + 5), (i16)(y + 3), pFrame, cellAmount[i]);
				}
				else
				{
					i32 amount = cellAmount[i];
					if (amount >= 0)
					{
						Panel_DrawFlatShadedPoly(
							x - ((90 * amount) >> 9) + 45,
							y - ((70 * amount) >> 9) + 35,
							2 * ((90 * amount) >> 9),
							2 * ((70 * amount) >> 9),
							5, 5, 15, 4094, 1);
					}

					if (amount == 256 && pIcons[i] != 0)
						M3d_Render(pIcons[i]);
				}

				pFrame++;
			}

			pActiveBox = 0;
		}

		M3d_RenderCleanup();
		PCSHELL_DrawMouseCursor();

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		gShellMenuEase = PShell_MoveTowards(gShellMenuEase, 128);

		for (i32 i = 0; i < 6; i++)
		{
			cellAmount[i] += 40;
			if (cellAmount[i] > 256)
				cellAmount[i] = 256;
		}

		if (selected < 0)
			Pad_ClearTriggers(G_SCONTROL);

		Pad_Update();

		if (*gShellMenuAbort)
			return;

		if (PCSHELL_MouseMoved())
		{
			for (i32 i = 0; i < 6; i++)
			{
				i32 col = (i > 2) ? i - 3 : i;
				i32 y = (i > 2) ? 128 : 50;
				i32 x = 150 * col + 62;

				if ((G_SAVE_GAME.field_90 & (1 << i)) && PCSHELL_IsMouseOver(x, y, x + 90, y + 70))
					selected = i;
			}
		}

		CheckForPadUnplugged();

		if (PCSHELL_CheckTriggers(131616, 1, 1))
		{
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(35, 0x2000, 0);

			if (pActiveBox != 0)
				delete pActiveBox;

			for (i32 j = 0; j < 6; j++)
			{
				if (pIcons[j] != 0)
					delete pIcons[j];
			}

			Pause(1);
			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");
			gsub_430680();
			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");

			Pad_ClearTriggers(G_SCONTROL);
			return;
		}

		i32 sameCellRepeat = 0;

		if (pActiveBox != 0)
		{
			if (pActiveBox->field_30 == 0)
				goto tail;

			delete pActiveBox;
			pLoadingBox = 0;
		}

		{
			i32 prevSelected = selected;

			if (PCSHELL_CheckTriggers(49164, 0, 0))
			{
				if (repeatDelay == 0 || (repeatDelay > 20 && (repeatDelay & 1) == 0))
				{
					if (PCSHELL_CheckTriggers(16388, 0, 0) && selected != 0 && selected != 3)
						selected--;
					if (PCSHELL_CheckTriggers(32776, 0, 0) && selected != 2 && selected != 5)
						selected++;
				}
				sameCellRepeat = repeatDelay + 1;
			}
			repeatDelay = sameCellRepeat;

			if (PCSHELL_CheckTriggers(4097, 1, 1))
			{
				G_SCONTROL[0].Up.Triggered = 0;
				if (selected > 2)
					selected -= 3;
			}

			if (PCSHELL_CheckTriggers(8194, 1, 1))
			{
				G_SCONTROL[0].Down.Triggered = 0;
				if (selected < 3)
					selected += 3;
			}

			if (prevSelected != selected)
				SFX_Play(41, 0x3FFF, 0);
		}

		{
			i32 mouseOverSelected = 0;

			if (PCSHELL_CheckTriggers(256, 1, 1))
			{
				for (i32 i = 0; i < 6; i++)
				{
					i32 col = (i > 2) ? i - 3 : i;
					i32 x = 150 * col + 62;
					i32 y = (i > 2) ? 128 : 50;

					if (i == selected && (G_SAVE_GAME.field_90 & (1 << i)) && PCSHELL_IsMouseOver(x, y, x + 90, y + 70))
						mouseOverSelected = 1;
				}
			}

			if (selected >= 0 && (mouseOverSelected || PCSHELL_CheckTriggers(65552, 1, 1)))
			{
				G_SCONTROL[0].Start.Triggered = 0;
				G_SCONTROL[0].X.Triggered = 0;

				if (G_SAVE_GAME.field_90 & (1 << selected))
				{
					SFX_Play(31, 0x2000, 0);

					char bmpName[13];
					strcpy(bmpName, "l*cov.bmp");
					switch (selected)
					{
						case 0: bmpName[1] = '1'; break;
						case 1: bmpName[1] = '2'; break;
						case 2: bmpName[1] = '4'; break;
						case 3: bmpName[1] = '5'; break;
						case 4: bmpName[1] = '7'; break;
						case 5: bmpName[1] = '8'; break;
					}
					BMP_Draw(bmpName);

					do
					{
						Pad_Update();
					} while (!PCSHELL_CheckTriggers(197424, 1, 1));

					G_SCONTROL[0].Circle.Triggered = 0;
					G_SCONTROL[0].X.Triggered = 0;
					G_SCONTROL[0].Start.Triggered = 0;
					SFX_Play(35, 0x2000, 0);
					Pad_ClearTriggers(G_SCONTROL);
				}
				else
				{
					SFX_Play(27, 0x2000, 0);
				}
			}
		}

tail:
		for (i32 k = 0; k < 6; k++)
		{
			if (pIcons[k] != 0)
				pIcons[k]->AI();
		}

		if (G_VBLANKS == startVblanks)
			Pause(1);

		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		gsub_430680();
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}

		PCSHELL_Relax();
	}
}

// word lists are defined further down in this file
extern char *gBadWords[30];
extern char *gGoodWords[30];

// @Ok
i32 Shell_InputName(char *pName,i32 a2,i32 a3, const char *pDesc)
{
	print_if_false(gShellInitialized != 0, "Called Shell_InputName() without shell initialised");

	i32 result = 0;
	i32 textEase = 0;
	i32 textCountdown = 0;
	i32 textSlide = 0;
	i32 textOffset = 0;
	char originalName[9];
	char nameSave[9];
	char badWord[12];
	char keyName[32];
	CRudeWordHitterSpidey *pSpidey = 0;
	i32 nameLen = 0;
	i32 titleX = 0;
	i32 goodLen = 0;
	i32 foundBadWord = 0;
	i32 w = 0;
	char *j = 0;
	i32 c = 0;
	i32 key = 0;
	i32 i = 0;
	u32 vblankAtDraw = 0;

	if (pDesc != 0)
	{
		textEase = 32;
		textOffset = 32;
		textCountdown = 90;
	}

	Utils_InitialRand(G_VBLANKS);
	for (i = 10000; i != 0; --i)
		Rnd(10);

	Pause(1);
	DrawSync();

	memcpy(originalName, pName, 9);

	nameLen = (i32)strlen(pName);
	if (nameLen < 8)
	{
		memset(&pName[nameLen], '.', 8 - nameLen);
		nameLen = 0;
	}

	pName[8] = 0;

	G_MIKE_CAMERA[0].Position.vx = 0;
	G_MIKE_CAMERA[0].Position.vy = 0;
	G_MIKE_CAMERA[0].Position.vz = 0;
	G_MIKE_CAMERA[0].Angles.vx = 0;
	G_MIKE_CAMERA[0].Angles.vy = 0;
	G_MIKE_CAMERA[0].Angles.vz = 0;
	G_MIKE_CAMERA[0].Style = 0;

	if (a2 == 0)
	{
		titleX = 128;
		*(i32*)0x005512EC = 256;
	}

	while (2)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();

		vblankAtDraw = G_VBLANKS;

		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);

		Mess_SetRGB(0x45, 0x3C, 0x6B, 0);
		Mess_SetSort(4095);
		Mess_DrawText(textOffset + 256, 196, pName, 0, 0x1000);

		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);

		if (a3 != 0)
			Shell_DrawTitleBar(titleX, 38, "enter cheat", 1, 0, 150, -21, 29);
		else
			Shell_DrawTitleBar(titleX, 38, "Input name", 1, 0, 150, -21, 29);

		Mess_SetRGB(0x80, 0x80, 0x80, 0);
		Mess_DrawText(textOffset + 256, 167, "Finish", 0, 0x1000);

		if (pSpidey != 0)
		{
			M3dMaths_RotMatrixYXZ(&G_MIKE_CAMERA[0].Angles, &G_MIKE_CAMERA[0].Transform);
			TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);
			M3d_RenderSetup(G_MIKE_CAMERA, &G_VIEWPORT, G_PDOUBLE_BUFFER->OrderingTable);
			M3d_Render(pSpidey);
			M3d_RenderCleanup();
		}

		if (textCountdown != 0 && pDesc != 0)
		{
			PShell_SmallFont();
			Mess_SetRGB(0x64, 0x64, 0x64, 0);
			Mess_SetRGBBottom(0x64, 100, 100);
			Mess_SetShadowRGB(0xFF);
			Mess_SetTextJustify(0);
			Mess_DrawText(256, 55, pDesc, 0, 0x1000);
			PShell_NormalFont();
			PShell_DefaultText();
			Mess_SetShadowRGB(0x29);
		}

		PCSHELL_DrawMouseCursor();

		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);

		titleX = PShell_MoveTowards(titleX, 128);
		*(i32*)0x005512EC = PShell_MoveTowards(*(i32*)0x005512EC, 256);

		if (textEase - 2 < 0)
			textEase = 0;
		else
			textEase -= 2;
		textSlide += 690;
		textOffset = textEase * G_RCOSSIN_TBL[textSlide & 0xFFF].cos / 4096;

		if (textCountdown != 0)
			--textCountdown;

		++TTime;
		Pad_Update();

		if (*gShellMenuAbort != 0)
			return -1;

		if (a3 == 0)
		{
			Card_CheckStatus(0, 0);
			if (CardStatus == -1)
			{
				result = -1;
				memcpy(pName, originalName, 9);
				goto done;
			}
		}

		if (pSpidey != 0)
			Pad_Clear(G_SCONTROL);

		for (key = 0; key < 256; ++key)
		{
			if (PCINPUT_IsKeyPressed(key, 1) == 0)
				continue;

			DXINPUT_GetKeyName(key, keyName);

			if (strlen(keyName) == 1)
			{
				if ((keyName[0] >= 'A' && keyName[0] <= 'Z' || keyName[0] >= '0' && keyName[0] <= '9') && nameLen < 8)
				{
					pName[nameLen++] = keyName[0];
					SFX_Play(0x1F, 0x2000, 0);
				}
			}
			else
			{
				if (strcmpi(keyName, "BACK") == 0)
				{
					if (nameLen == 0)
						continue;
					pName[--nameLen] = '.';
					SFX_Play(0x29, 0x3FFF, 0);
					continue;
				}

				if (strcmpi(keyName, "SPACE") == 0 && nameLen < 8)
				{
					pName[nameLen++] = ' ';
					SFX_Play(0x29, 0x3FFF, 0);
					continue;
				}
			}
		}

		if (PCSHELL_CheckTriggers(65552, 1, 1) != 0)
		{
			memcpy(nameSave, pName, 9);

			foundBadWord = 0;
			for (w = 0; w < 29; ++w)
			{
				Utils_CopyString(gBadWords[w], badWord, 9);
				for (j = badWord; *j; j++)
					*j -= 1;
				if (pName[0] != 0 && Shell_ContainsSubString(pName, badWord))
				{
					foundBadWord = 1;
					break;
				}
			}

			if (foundBadWord != 0)
			{
				goodLen = Utils_CopyString(gGoodWords[Rnd(30)], nameSave, 9);
				if (goodLen < 8)
					memset(&nameSave[goodLen], '.', 8 - goodLen);
				if (goodLen != 0)
				{
					if (pSpidey != 0)
						delete pSpidey;
					pSpidey = new CRudeWordHitterSpidey;
					goto restOfLoop;
				}
			}

			if (pName[0] == '.')
				goto restOfLoop;

			for (c = 0; pName[c] != '.'; ++c)
			{
				if (c >= 8)
				{
					result = 1;
					goto done;
				}
			}
			pName[c] = 0;
			result = 1;
			goto done;
		}

	restOfLoop:
		if (PCSHELL_CheckTriggers(131616, 1, 1) != 0)
		{
			result = -1;
			memcpy(pName, originalName, 9);
			goto done;
		}

		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}

		if (pSpidey != 0)
		{
			pSpidey->AI();

			if (pSpidey->mAnim == 100 && pSpidey->mFrame == 6)
				memcpy(pName, nameSave, 9);

			if (pSpidey->mPos.vy > 0x104000)
			{
				delete pSpidey;
				pSpidey = 0;
			}
		}

		if (G_VBLANKS == vblankAtDraw)
			Pause(1);

		DoVblankProcessing = 0;
		Pause(1);
		DrawSync();
		PCSHELL_Relax();
	}

done:
	if (pSpidey != 0)
		delete pSpidey;

	Pause(1);
	DrawSync();
	DrawSync();
	Pad_ClearTriggers(G_SCONTROL);

	return result;
}

EXPORT u8 gInLegalScreen;

// @Ok
void Shell_LegalScreen(void)
{
	if (!gInLegalScreen)
	{
		gInLegalScreen = 1;
		Front_ClearScreen();

		DrawSync();
		Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		Pad_ClearTriggers(G_SCONTROL);

		Sprite2* v0 = new Sprite2("LegalPC.bmp", 1, 0, 0, 3);
		u32 v3 = G_VBLANKS + 180;
		while ( 1 )
		{
			if (!G_SCENE_RELATED)
				PCGfx_BeginScene(1u, -1);

			v0->draw(
				0,
				0,
				8,
				-1.0f);
			if (G_SCENE_RELATED)
				PCGfx_EndScene(1);
			++TTime;
			Pad_Update();

			if (G_VBLANKS > v3)
				break;

			PCSHELL_Relax();
		}

		delete v0;
		Mess_DeleteAll();
		Front_ClearScreen();

		DrawSync();
		Pad_ClearTriggers(G_SCONTROL);
	}
}

static i32 * const gShowAllLevels = (i32*)0x0060CFD8;

// @Ok
i32 Shell_LevelSelect(void)
{
	print_if_false(gShellInitialized != 0, "Called Shell_MainMenu() without shell initialised");
	i32 v18 = 0;
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(0, 0, 1, 256, 256, 13);
	pMenu->AdjustWidth(10);

	// find the lowest completion byte among the 34 levels
	i32 v2 = 1000000;
	for (i32 i = 0; i < 34; i++)
	{
		if ((u8)G_SAVE_GAME.field_56[i] < v2)
			v2 = (u8)G_SAVE_GAME.field_56[i];
	}
	// find the first level with that (lowest) completion byte
	i32 v4 = 0;
	i32 v5 = 0;
	while ((u8)G_SAVE_GAME.field_56[v5] != v2)
	{
		if (++v5 >= 34)
			break;
	}
	v4 = v5;

	for (i32 v6 = 0; v6 < 34; v6++)
	{
		print_if_false(*Levels[v6].mDisplayName != 0, "Bad level name");
		if (*gShowAllLevels != 0 || G_SAVE_GAME.field_56[v6] != 0 || v6 == v4)
			pMenu->AddEntry(Levels[v6].mDisplayName);
	}
	pMenu->scrollbar_one = 1;
	pMenu->field_1B = 9;
	pMenu->scrollbar_zero = 0;
	pMenu->CentreX();
	pMenu->CentreY();
	if (pMenu->mNumLines > 9)
		pMenu->Zoom(2);
	else
		pMenu->Zoom(1);

	i32 v9 = 0;
	const char* v14 = 0;
	*(i32*)0x005512EC = 384;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 v22 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(v9, 38, "level select", 1, 0, 150, -21, 29);
		pMenu->Display();
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		v9 = PShell_MoveTowards(v9, 128);
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return 0;
		CheckForPadUnplugged();
		pMenu->Update();
		if (PCSHELL_CheckTriggers(131616, 1, 1))
		{
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(0x23, 0x2000, 0);
			goto done;
		}
		i32 IsMouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			u8 mJustification = pMenu->mJustification;
			const char* name = pMenu->mEntry[pMenu->mLine].name;
			i32 x, y;
			pMenu->GetEntryXY(name, &x, &y);
			IsMouseOverText = PCSHELL_IsMouseOverText(name, x, y, mJustification);
		}
		if (pMenu->mLine < 0x28 && (IsMouseOverText != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
			break;
		if (G_VBLANKS == v22)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
	}

	// a level was selected
	G_SCONTROL[0].Start.Triggered = 0;
	G_SCONTROL[0].X.Triggered = 0;
	v14 = pMenu->mEntry[pMenu->mLine].name;
	G_SAVE_GAME.field_4[0] = 0;
	if (*Levels[0].mDisplayName != 0)
	{
		i32 idx = -1;
		for (i32 li = 0; li < 34; li++)
		{
			if (Utils_CompareStrings(v14, Levels[li].mDisplayName) == 0)
			{
				idx = li;
				break;
			}
			if (*Levels[li + 1].mDisplayName == 0)
				break;
		}
		if (idx != -1)
			Utils_CopyString(Levels[idx].mName, G_SAVE_GAME.field_4, 9);
	}
	G_SAVE_GAME.mRestartPointName[0] = 0;
	*(i32*)((char*)&G_SAVE_GAME + 0x50) = 0;
	*(u8*)((char*)&G_SAVE_GAME + 0x79) = 0;
	*(i32*)((char*)&G_SAVE_GAME + 0x48) = 0;
	*(i32*)((char*)&G_SAVE_GAME + 0x4C) = 0;
	v18 = 1;
	SFX_Play(0x1F, 0x2000, 0);

done:
	delete pMenu;
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
	return v18;
}

// @Ok
// buffer the Merge loop writes merged records into, then copies back
static char * const gMergeBuffer = (char*)0x00610790;
// mouse-related flag used by the input helper below
static u8 * const gMouseRelated = (u8*)0x005FAE9D;

// static helper inlining sub_440E40: splits pad/mouse triggers into per-button flags
// @Bogus
static void Shell_ReadTriggers(i32 *pSelect, i32 *pBack, i32 *pAny, i32 *pMouse)
{
	i32 mask = 0x40000;
	*pMouse = 0;
	*pAny = 0;
	*pBack = 0;
	*pSelect = 0;
	if (*gMouseRelated != 0)
		mask = 262208;
	*pMouse = PCSHELL_CheckTriggers(mask, 1, 1);
	*pSelect = PCSHELL_CheckTriggers(65552, 1, 1) || *pMouse;
	*pBack = PCSHELL_CheckTriggers(131616, 1, 1);
	*pAny = *pBack || *pSelect;
	if (*pSelect)
		*pBack = 0;
}

// @Ok
i32 Shell_LoadGame(void)
{
	print_if_false(gShellInitialized != 0, "Called Shell_LoadGame() without shell initialised");
	i32 v0 = 0;
	i32 state = 0;
	i32 exiting = 0;
	CMenu* pMenu = 0;
	i32 introCount = 0;
	i32 delay = 0;
	i32 result = 0;
	*(i32*)0x005512EC = 384;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 vblanks = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(v0, 38, "load game", 1, 0, 150, -21, 29);
		Mess_SetRGB(0x6B, 0x5D, 0xA7, 0);
		Mess_SetRGBBottom(0x3E, 54, 96);
		Mess_SetSort(0);
		switch (state)
		{
		case 2:
			Mess_DrawText(256, 96, "save game file contains", 0, 0x1000);
			Mess_DrawText(256, 113, "no spider-man game save.", 0, 0x1000);
			Mess_DrawText(256, 130, "press esc key", 0, 0x1000);
			Mess_DrawText(256, 147, "to cancel.", 0, 0x1000);
			break;
		case 3:
			Mess_DrawText(256, 90, "error reading save game file", 0, 0x1000);
			Mess_DrawText(256, 124, "press esc key", 0, 0x1000);
			Mess_DrawText(256, 141, "to cancel.", 0, 0x1000);
			break;
		case 5:
			if (pMenu->FinishedZooming())
			{
				Mess_SetTextJustify(0);
				Mess_SetRGB(0x45, 0x3C, 0x6B, 0);
				Mess_SetRGBBottom(0x28, 35, 62);
				Shell_DisplayGameInfo(190, 70, &gSaveGameSlots[pMenu->mLine]);
			}
			print_if_false(pMenu != 0, "No games menu?");
			pMenu->Display();
			break;
		case 6:
			PShell_BigFont();
			Mess_DrawText(256, 101, "load successful", 0, 0x1000);
			PShell_NormalFont();
			Mess_DrawText(256, 143, "press enter to continue.", 0, 0x1000);
			break;
		default:
			break;
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		i32 v25 = PShell_MoveTowards(v0, 128);
		if ((++TTime & 1) != 0)
			Card_CheckStatus(0, 0);
		if (pMenu != 0 && pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return 0;
		CheckForPadUnplugged();
		i32 v22, v30, v23, v26;
		Shell_ReadTriggers(&v22, &v30, &v23, &v26);
		if (pMenu != 0 && pMenu->mLine >= 0x28)
		{
			v22 = 0;
			v26 = 0;
		}
		i32 IsMouseOverText = 0;
		switch (state)
		{
		case 0:
			if (introCount != 0)
			{
				if (--introCount == 0)
				{
					delay = 150;
					state = 1;
				}
			}
			else
			{
				introCount = 10;
			}
			goto vblank;
		case 1:
			switch (CardStatus)
			{
			case -2:
				state = 4;
				break;
			case -1:
				state = 7;
				break;
			case 1:
			{
				i32 v5 = Card_Load();
				if (v5 != 0)
				{
					if (v5 != 1)
					{
						state = 3;
						break;
					}
					state = 2;
					break;
				}
				print_if_false(pMenu == 0, "Already got games menu?");
				PShell_NormalFont();
				pMenu = new CMenu(90, 70, 0, 256, 256, 15);
				*(u8*)((char*)pMenu + 0x18) = 1;
				Shell_AddGameSlots(pMenu);
				pMenu->Zoom(0);
				state = 5;
				break;
			}
			default:
				if (delay == 0)
				{
					state = 2;
					break;
				}
				--delay;
				break;
			}
			goto vblank;
		case 2:
		case 3:
		case 4:
			if (CardStatus == -1 || CardStatus == 2)
			{
				state = 0;
				goto vblank;
			}
			if (v23 != 0 || PCSHELL_CheckTriggers(256, 1, 1))
			{
				SFX_Play(0x23, 0x2000, 0);
				exiting = 1;
			}
			goto vblank;
		case 5:
			print_if_false(pMenu != 0, "No games menu?");
			if (CardStatus == -1)
			{
				delete pMenu;
				pMenu = 0;
				state = 7;
			}
			else
			{
				pMenu->Update();
				if (PCSHELL_CheckTriggers(256, 1, 1))
				{
					i32 mLine = pMenu->mLine;
					u8 mJust = pMenu->mJustification;
					const char* name = pMenu->mEntry[mLine].name;
					i32 x, y;
					pMenu->GetEntryXY(name, &x, &y);
					IsMouseOverText = PCSHELL_IsMouseOverText(name, x, y, mJust);
				}
				if (v22 || IsMouseOverText)
				{
					char* slot = (char*)&gSaveGameSlots[pMenu->mLine];
					if (*(u32*)slot == 0)
					{
						SFX_Play(0x1B, 0x2000, 0);
					}
					else
					{
						print_if_false(1, "Size of SSaveGame not a multiple of 4");
						i32 checksum = 0;
						u32* data = (u32*)(slot + 4);
						for (i32 i = 0; i < 46; i++)
						{
							u32 c = (u32)checksum;
							u32 doubled = (c << 1) | (c >> 31);
							checksum = (i32)(data[i] + doubled);
						}
						checksum |= 1;
						if (*(u32*)slot == (u32)checksum)
						{
							SFX_Play(0x1F, 0x2000, 0);
							memcpy(&G_SAVE_GAME, slot, sizeof(G_SAVE_GAME));
							for (i32 j = 0; j < NUM_CHALLS; j++)
							{
								Merge((SScore*)(gMergeBuffer + j * 25 + 3), &gGlobalRecords.mScores[j * 5], gChallenges[j].field_C);
							}
							gGlobalRecords = *(SRecords*)gMergeBuffer;
							PShell_ApplyGameState();
							Spidey_LoadAlternativeTextureSet(gAltTextureSet, G_SAVE_GAME.field_7C + 1);
							state = 6;
							Pad_ClearTriggers(G_SCONTROL);
						}
						else
						{
							SFX_Play(0x1B, 0x2000, 0);
						}
					}
				}
			}
			goto vblank;
		case 6:
			if (v23 != 0 || PCSHELL_CheckTriggers(256, 1, 1))
			{
				SFX_Play(0x1F, 0x2000, 0);
				result = 1;
				exiting = 1;
			}
			goto vblank;
		case 7:
			if (CardStatus == -2 || (CardStatus > 0 && CardStatus <= 2))
			{
				state = 0;
				goto vblank;
			}
			else if (G_SCONTROL[0].Circle.Triggered != 0)
			{
				G_SCONTROL[0].Circle.Triggered = 0;
				SFX_Play(0x23, 0x2000, 0);
				exiting = 1;
			}
			goto vblank;
		}
	vblank:
		if (v30 != 0 && exiting == 0)
		{
			SFX_Play(0x23, 0x2000, 0);
			exiting = 1;
		}
		if (G_VBLANKS == vblanks)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
		if (exiting == 0)
		{
			v0 = v25;
			continue;
		}
		delete pMenu;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		Pad_ClearTriggers(G_SCONTROL);
		return result;
	}
}

// Investigated this session (address confirmed real: 0x493990, 2284 bytes,
// names.json). This is the real main-menu screen (New Game/Continue/
// Training/Options/Special/Quit icon row + 3D spidey model preview),
// called from Shell_DoShell (see the comment there). Every callee's real
// name is now known (all resolve to already-implemented functions:
// Db_FlipClear, CalcPolyBufferEnd, PCGfx_BeginScene/EndScene,
// M3dMaths_RotMatrixYXZ, TransMatrix, M3d_RenderSetup/Render/RenderCleanup,
// PShell_DefaultText, Mess_SetRGB/SetRGBBottom/DrawText/SetTextJustify,
// PCSHELL_CheckTriggers/IsMouseOverText/MouseMoved/DrawMouseCursor,
// CDummy::SelectNewTrack, Shell_DrawTitleBar, PShell_SmallFont/NormalFont,
// Mess_ShadowsOn/Off, DCCard_Exists, Utils_CopyString, Pad_Update,
// Db_UpdateSky, Front_ClearScreen, SFX_Play, Redbook_XAPlay/XAStop,
// Init_KillAll, Utils_VblankProcessing, PCSHELL_Relax, Pause,
// CheckForPadUnplugged) except Bit_Display (0x411CF0) and Bit_Move
// (0x411B30), which have real names in names.json but are not declared
// anywhere in the repo yet (belong to bit.cpp/bit.h, out of scope for a
// shell.cpp-only session), CDummy_ctor (0x490DF0, an unnamed CDummy
// constructor overload used to spawn the preview model), and this file's
// own sub_493860 (the survival-arena-name copy helper, needs decompiling
// first). The menu item table itself (dword_552AA8/AAC/AB0/AB4, stride 10
// dwords/8 icons: x, y, icon pointer, unlock state, ...) has no idb_globals
// entry and no struct declared, would need its own investigation. Left as
// a stub: CheckForPadUnplugged (a direct same-TU dependency) is also still
// a stub (see above), so this cannot even be leaf-first attempted yet.
// Re-checked 2026-08-31: CDummy_ctor (sub_490DF0) was decompiled this
// session (IDA Hex-Rays) and is confirmed genuinely large, ~1840 bytes with
// a long per-costume special-case chain (Carnage/Mysterio/Scorpion/
// SuperOck/goldfish-bubble spawning), its own BIGTODO. Also, CheckForPad
// Unplugged's remaining blocker is now narrowed down to one shared base
// class (sub_460080/sub_460720, same function a parallel carnage.cpp
// session hit independently the same day from CSymbioteBlade); see the
// long comment above CheckForPadUnplugged for details.
// Update 2026-08-31, later same day: CheckForPadUnplugged, its
// CDropDownController widget, and Bit_Display/Bit_Move are all done now
// (real code, functional decompile session; see shell.h/
// CDropDownController, bit2.h/CKnottedWeb, bit.h/Bit_Move+Bit_Display).
// The remaining real blocker for this function is CDummy_ctor
// (sub_490DF0) and this file's own sub_493860, both still BIGTODO/
// undecompiled.
// Addendum 2026-08-31 (from the Shell_ComicCollection/Shell_GameCovers
// session): func_profile on sub_490DF0 confirms it is not a quick pickup
// even leaf-first -- 512 instructions, 85 basic blocks, 27 distinct
// callees (about 16 of them still unnamed/undecompiled), plus an
// ___CxxFrameHandler SEH frame (the "new T(...) needs a cleanup frame
// when the ctor isn't visible in the same TU" pattern from this file's
// CLAUDE.md notes). This function (sub_493990) is also confirmed via IDA
// xrefs_to on off_53BFC0 to construct the same class documented above
// Shell_ComicCollection and now fully implemented as CShellPreviewIcon
// (shell.h) -- that half of this function's blocker is RESOLVED, see
// Shell_GameCovers for the same construction idiom already working. The
// remaining, harder blocker is CDummy_ctor (sub_490DF0) itself: still
// undecompiled, 27 callees, out of scope for this session per the
// leaf-first rule (most of its callees would need their own sessions
// first). Recommend a dedicated session working sub_490DF0's callee list
// bottom-up before attempting this function again.
// Update 2026-08-31, dedicated CDummy_ctor session: sub_490DF0 (CDummy::CDummy) is done now
// (real callee count was 27 distinct addresses, not ~16 unnamed as guessed above -- every one
// of them turned out to already have a real name in this repo, including CShellMysterioHeadGlow/
// CShellGoldFish/CShellMysterioHeadCircle/CVertexWobble, all pre-existing @Ok classes reused
// as-is; see shell.h/CDummy and CDummy::CDummy for the full writeup). Re-decompiled this
// function (sub_493990) itself to check tractability now that its two named blockers are gone:
// it is 2284 bytes / 683 instructions / 142 basic blocks with 54 distinct callees, all real
// names, but this file's own sub_493860 (survival-arena-name copy helper) is STILL undecompiled,
// plus a large majority of the other 53 callees (menu-item table dword_552AA8 walk, popup/title-
// bar drawing sub_48D9C0/sub_47AE80, background setup sub_509D20/sub_4E65E0, input handling
// sub_50C180/sub_50C6C0/sub_50C5D0, camera-lerp sub_472DC0/sub_46E730/sub_46CFA0, object-list
// cleanup sub_4739A0, and more). Same conclusion as Shell_CharacterViewer/Shell_CostumeViewer:
// needs its own dedicated leaf-first session, not a quick follow-up.
//
// Implemented 2026-09-01 (functional decompile, IDA Hex-Rays): full main-menu
// screen logic. Two-column icon list (Continue/New Game/Options/Quit |
// Training/High Scores/Special/Gallery), a rotating Spidey preview icon
// (Spidey_CIcon) and a CDummy whose animation track changes per highlighted
// entry. Returns the highlighted entry's type on selection, 0 on abort.
//
// Menu table (original dword_552AA8, 8 entries x 10 ints; this function only
// reads x/y/text/type, the rest stored for fidelity).
struct SMainMenuEntry
{
	i32 x;
	i32 y;
	const char *text;
	i32 type;
	i32 field_10;
	i32 field_14;
	i32 field_18;
	i32 field_1C;
	i32 field_20;
	i32 field_24;
};

static const SMainMenuEntry gMainMenuTable[8] =
{
	{ 105, 46, "continue", 2, 0x005487F8, 5, 0, 0, 0, 0 },
	{ 86, 98, "new game", 1, 0x005487F8, 5, 0, 0, 20, 512 },
	{ 86, 153, "options", 4, 0x00554AA8, 1, 0, 0, 0, 700 },
	{ 120, 202, "quit", 18, 0x0056EB54, -1, 0, 0, 0, 0 },
	{ 400, 46, "training", 5, 0x005487F8, 5, 0, 0, -30, 512 },
	{ 430, 98, "High Scores", 6, 0x00554AA8, 1, 0, 0, 0, 700 },
	{ 430, 153, "special", 13, 0x00554AA8, 2, 0, 0, -4, 490 },
	{ 400, 202, "gallery", 7, 0x00554AA8, 0, 0, 256, -16, 700 },
};

// CDummy animation tracks (original .rdata 0x552F2C..0x552FE0), u16 lists
// terminated by 0xffff. case1 = idle (constructor + "new game"), case2 = walk
// ("continue"), case5 = "training".
static const u16 gMainMenuTrack1A[] = { 0x0000, 0x0021, 0x0022, 0x0022, 0x0022, 0x0022, 0x0022, 0x0024, 0xffff, 0x0000 };
static const u16 gMainMenuTrack1B[] = { 0x0000, 0x0021, 0x0022, 0x0022, 0x0022, 0x0022, 0x0022, 0x0023, 0x0022, 0x0022, 0x0024, 0xffff, 0x0000 };
static const u16 gMainMenuTrack1C[] = { 0x0125, 0x0125, 0x0125, 0x0126, 0x0126, 0x0125, 0xffff, 0x0000 };
static const u16 gMainMenuTrack2A[] = { 0x0001, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x000b, 0x0125, 0x0126, 0xffff, 0x0000 };
static const u16 gMainMenuTrack2B[] = { 0x0001, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x000b, 0x0125, 0xffff };
static const u16 gMainMenuTrack2C[] = { 0x0001, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x0015, 0x000b, 0x0126, 0xffff, 0x0000 };
static const u16 gMainMenuTrack5A[] = { 0x0064, 0x006b, 0x0091, 0x00d2, 0x00d3, 0x00d5, 0xffff, 0x0000 };
static const u16 gMainMenuTrack5B[] = { 0x00d6, 0x00d7, 0x00d8, 0x00d5, 0xffff, 0x0000 };
static const u16 gMainMenuTrack5C[] = { 0x00fa, 0x00fb, 0x00fc, 0x00fd, 0xffff, 0x0000 };

// 0x5512EC, written to 512 here (front.cpp holds a file-local macro for the
// same address, gFrontMysteryValueOne).
static i32 * const gMainMenuMysteryValue = (i32*)0x005512EC;
// 0x6A777C, the DCCard-exists flag (4 bytes before gBackgroundAnimFrame).
static u8 * const gMainMenuDCCardFlag = (u8*)0x006A777C;

// @Ok
i32 Shell_MainMenu(EShellResult a1)
{
	print_if_false(gShellInitialized != 0, "Called Shell_MainMenu() without shell initialised");

	// Find the entry whose type matches a1; default to line 2 if none does.
	i32 line = 0;
	const i32 *pType = &gMainMenuTable[0].type;
	while (line < 8 && *pType != a1)
	{
		pType += 10;
		++line;
	}
	if (line >= 8)
		line = 2;

	// Min completion value across the 34 level slots, and the first slot that
	// holds it (the level "continue" would resume from).
	i32 minComplete = 1000000;
	for (i32 i = 0; i < 34; i++)
	{
		if ((u8)G_SAVE_GAME.field_56[i] < minComplete)
			minComplete = (u8)G_SAVE_GAME.field_56[i];
	}
	i32 continueLevel = 0;
	while ((u8)G_SAVE_GAME.field_56[continueLevel] != minComplete)
	{
		if (++continueLevel >= 34)
			break;
	}
	Utils_CopyString(Levels[continueLevel].mName, G_SAVE_GAME.field_4, 9);

	// allComplete is 1 only when no level slot is still 0.
	i32 allComplete = 1;
	for (i32 j = 0; j < 34; j++)
	{
		if (G_SAVE_GAME.field_56[j] == 0)
			allComplete = 0;
	}

	Mess_SetTextJustify(0);
	G_MIKE_CAMERA[0].Position.vx = 0;
	G_MIKE_CAMERA[0].Position.vy = 0;
	G_MIKE_CAMERA[0].Position.vz = 0;
	G_MIKE_CAMERA[0].Angles.vx = 0;
	G_MIKE_CAMERA[0].Angles.vy = 0;
	G_MIKE_CAMERA[0].Angles.vz = 0;
	G_MIKE_CAMERA[0].Style = 0;

	Spidey_CIcon *pIcon = new Spidey_CIcon(line);
	CDummy *pDummy = new CDummy("spidey", 50, 4096, -32, 0,
		(u16*)gMainMenuTrack1A, (u16*)gMainMenuTrack1B, (u16*)gMainMenuTrack1C, 0, 0, 0, 0);
	pDummy->mPos.vz = 0x198000;
	pDummy->mAngles.vy = 3584;

	*gMainMenuMysteryValue = 512;
	Redbook_XAPlay(78, 10, 0);

	i32 moveRepeat = 0;
	i32 startVblanks = 0;
	i32 curType = 0;
	i32 mouseOverText = 0;
	i32 v20 = 0;
	i32 v21 = 0;
	i32 lineAtStart = 0;
	i32 k = 0;

	while (1)
	{
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();
		startVblanks = G_VBLANKS;

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1, -1);

		M3dMaths_RotMatrixYXZ(&G_MIKE_CAMERA[0].Angles, &G_MIKE_CAMERA[0].Transform);
		TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);
		M3d_RenderSetup(G_MIKE_CAMERA, &G_VIEWPORT, G_PDOUBLE_BUFFER->OrderingTable);

		curType = gMainMenuTable[line].type;
		if (curType == 1 || curType == 5 || (curType == 2 && G_SAVE_GAME.field_78 != 0))
			M3d_Render(pDummy);
		else if (curType != 2)
			M3d_Render(pIcon);

		M3d_RenderCleanup();
		Bit_Display();
		PShell_DefaultText();
		Mess_SetRGB(0x6B, 0x5D, 0xA7, 0);
		Mess_SetRGBBottom(62, 54, 96);

		if (curType == 2)
		{
			if (allComplete == 0 || continueLevel != 0)
				Mess_DrawText(256, 178, Levels[continueLevel].mDisplayName, 0, 0x1000);
			else
				Mess_DrawText(256, 162, "costume viewer", 0, 0x1000);
		}

		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);

		PShell_SmallFont();
		Mess_ShadowsOff();
		*gMainMenuDCCardFlag = DCCard_Exists(0);

		for (k = 0; k < 8; k++)
		{
			if (k != 0 || G_SAVE_GAME.field_78 != 0)
			{
				Mess_SetTextJustify(0);
				Mess_SetRGB(0x45, 0x3C, 0x6B, 0);
				Mess_SetRGBBottom(0x28, 35, 62);
			}
			else
			{
				Mess_SetRGB(0x1A, 0x17, 0x29, 0);
				Mess_SetRGBBottom(0xF, 13, 24);
			}
			if (k != line)
				Mess_DrawText(gMainMenuTable[k].x, gMainMenuTable[k].y, gMainMenuTable[k].text, 0, 0x1000);
		}

		PShell_NormalFont();
		PShell_DefaultText();
		Mess_ShadowsOn();
		Shell_DrawTitleBar(gMainMenuTable[line].x, gMainMenuTable[line].y, gMainMenuTable[line].text, 0,
			line >= 4 ? 512 : 0, 80, -20, 29);

		PCSHELL_DrawMouseCursor();

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		Pad_Update();
		if (*gShellMenuAbort)
			return 0;
		CheckForPadUnplugged();

		if (Redbook_XAStat() == 4)
		{
			Redbook_XAStop();
			gCarnageXaRelatedTwo = 0;
			Redbook_XAPlay(78, 10, 0);
		}

		mouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
			mouseOverText = PCSHELL_IsMouseOverText(gMainMenuTable[line].text, gMainMenuTable[line].x, gMainMenuTable[line].y, 0);

		if (PCSHELL_CheckTriggers(16, 1, 1) || mouseOverText)
			break;

	nav:
		v20 = line;
		v21 = line;
		lineAtStart = line;

		if (PCSHELL_MouseMoved())
		{
			i32 over = -1;
			for (i32 m = 0; m < 8; m++)
			{
				if (PCSHELL_IsMouseOverText(gMainMenuTable[m].text, gMainMenuTable[m].x, gMainMenuTable[m].y, 0))
					over = m;
			}
			if (over >= 0)
				v20 = over;
			line = v20;
		}

		if (PCSHELL_CheckTriggers(12291, 0, 0))
		{
			if (moveRepeat == 0 || (moveRepeat > 20 && (moveRepeat & 1) == 0))
			{
				if (PCSHELL_CheckTriggers(4097, 0, 0))
				{
					if (v20 == 0 || ((--v20, line = v20, v20 == 0) && G_SAVE_GAME.field_78 == 0))
					{
						v20 = 7;
						line = 7;
					}
				}
				if (PCSHELL_CheckTriggers(8194, 0, 0))
				{
					if (v20 >= 7)
					{
						v20 = 0;
						line = 0;
						if (G_SAVE_GAME.field_78 == 0)
						{
							v20 = 1;
							line = 1;
						}
					}
					else
					{
						line = ++v20;
					}
				}
			}
			++moveRepeat;
			v21 = lineAtStart;
		}
		else
		{
			moveRepeat = 0;
		}

		if (PCSHELL_CheckTriggers(32776, 1, 1))
		{
			G_SCONTROL[0].Right.Triggered = 0;
			if (v20 < 4)
			{
				v20 += 4;
				line = v20;
			}
		}

		if (PCSHELL_CheckTriggers(16388, 1, 1))
		{
			G_SCONTROL[0].Left.Triggered = 0;
			if (v20 >= 4)
			{
				v20 -= 4;
				line = v20;
				if (v20 == 2 && *gMainMenuDCCardFlag == 0)
					line = ++v20;
				else if (v20 == 0 && G_SAVE_GAME.field_78 == 0)
					line = ++v20;
			}
		}

		// Resolve the final line and whether a move sound plays.
		i32 playMoveSound = 0;
		if (v20 != 0 || G_SAVE_GAME.field_78 != 0)
		{
			if (v21 != v20)
				playMoveSound = 1;
		}
		else
		{
			if (v21 != 0)
			{
				v20 = v21;
				line = v21;
			}
			else
			{
				v20 = 1;
				line = 1;
			}
		}

		if (playMoveSound)
			SFX_Play(0x29, 0x3FFF, 0);

		switch (gMainMenuTable[v20].type)
		{
			case 1:
				if (pDummy->mType != 1001)
				{
					pDummy->field_1A4 = (u16*)gMainMenuTrack1A;
					pDummy->field_1A8 = (u16*)gMainMenuTrack1B;
					pDummy->field_1AC = (u16*)gMainMenuTrack1C;
					pDummy->SelectNewTrack(1);
					pDummy->mType = 1001;
				}
				break;
			case 2:
				if (pDummy->mType != 1000)
				{
					pDummy->field_1A4 = (u16*)gMainMenuTrack2A;
					pDummy->field_1A8 = (u16*)gMainMenuTrack2B;
					pDummy->field_1AC = (u16*)gMainMenuTrack2C;
					pDummy->SelectNewTrack(1);
					pDummy->mType = 1000;
				}
				break;
			case 5:
				if (pDummy->mType != 1002)
				{
					pDummy->field_1A4 = (u16*)gMainMenuTrack5A;
					pDummy->field_1A8 = (u16*)gMainMenuTrack5B;
					pDummy->field_1AC = (u16*)gMainMenuTrack5C;
					pDummy->SelectNewTrack(1);
					pDummy->mType = 1002;
				}
				break;
			default:
				break;
		}

		pDummy->AI();
		pIcon->SetIcon(v20);
		pIcon->AI();
		Bit_Move();
		Bit_RemoveDeadBits();

		if (G_VBLANKS == startVblanks)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		gsub_430680();
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
	}

	// Selection path: accept unless the highlighted entry is unavailable.
	i32 selType = gMainMenuTable[line].type;
	i32 accepted = 0;
	if ((gRenderTest & 0x80) != 0)
	{
		i32 v19 = selType - 1;
		if (v19 == 0 || (v19 == 1 && G_SAVE_GAME.field_78 != 0))
			accepted = 1;
	}
	else
	{
		if (selType != 2 || G_SAVE_GAME.field_78 != 0)
			accepted = 1;
	}

	if (accepted)
	{
		SFX_Play(0x1F, 0x2000, 0);
		delete pIcon;
		delete pDummy;
		Init_KillAll();
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		gsub_430680();
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		Pad_ClearTriggers(G_SCONTROL);
		gsub_430880();
		Redbook_XAStop();
		return gMainMenuTable[line].type;
	}

	// Denied: stay in the menu, re-enter at the navigation step.
	SFX_Play(0x1B, 0x2000, 0);
	goto nav;
}

// @Ok
i32 Shell_MemoryCard(EShellResult a1)
{
	print_if_false(gShellInitialized != 0, "Called Shell_MemoryCard() without shell initialised");
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(256, 0, 0, 256, 256, 16);
	pMenu->AddEntry("load game data");
	pMenu->AddEntry("Save game data");
	pMenu->CentreY();
	pMenu->Zoom(0);
	if (a1 == 20)
		pMenu->SetLine(0);
	else if (a1 == 21)
		pMenu->SetLine(1);
	else
		print_if_false(0, "Bad default sent to Shell_MemoryCard()");

	i32 v4 = 0;
	i32 v13 = 0;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 v11 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(v4, 38, "FILE SYSTEM", 1, 0, 150, -21, 29);
		pMenu->Display();
		if (pMenu->FinishedZooming())
		{
			PShell_InstructionalText();
			Mess_DrawText(256, 168, "please select to load", 0, 0x1000);
			Mess_DrawText(256, 184, "or save game data", 0, 0x1000);
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		v4 = PShell_MoveTowards(v4, 128);
		*(i32*)0x005512EC = PShell_MoveTowards(*(i32*)0x005512EC, 384);
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return 0;
		CheckForPadUnplugged();
		pMenu->Update();
		i32 IsMouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			u8 mJustification = pMenu->mJustification;
			const char* name = pMenu->mEntry[pMenu->mLine].name;
			i32 x, y;
			pMenu->GetEntryXY(name, &x, &y);
			IsMouseOverText = PCSHELL_IsMouseOverText(name, x, y, mJustification);
		}
		if (pMenu->mLine < 0x28 && (IsMouseOverText != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
		{
			G_SCONTROL[0].Start.Triggered = 0;
			G_SCONTROL[0].X.Triggered = 0;
			SFX_Play(0x1F, 0x2000, 0);
			if (pMenu->ChoiceIs("load game data"))
			{
				v13 = 20;
				goto done;
			}
			if (pMenu->ChoiceIs("Save game data"))
			{
				v13 = 21;
				goto done;
			}
		}
		if (PCSHELL_CheckTriggers(49164, 1, 1))
		{
			G_SCONTROL[0].Right.Triggered = 0;
			G_SCONTROL[0].Left.Triggered = 0;
		}
		if (PCSHELL_CheckTriggers(131616, 1, 1))
			break;
		if ((G_SCONTROL[0].X.Triggered != 0 || G_SCONTROL[0].Start.Triggered != 0) && G_VBLANKS == v11)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
	}
	G_SCONTROL[0].Circle.Triggered = 0;
	SFX_Play(0x23, 0x2000, 0);

done:
	delete pMenu;
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
	return v13;
}

struct SMovieEntry
{
	char* pDisplayName;
	char* pFileName;
	i32 mUnlockId;
	i32 field_C;
};
static SMovieEntry* const gMovieTable = (SMovieEntry*)0x00554448;

// @Ok
void Shell_MovieViewer(void)
{
	print_if_false(gShellInitialized != 0, "Called Shell_MainMenu() without shell initialised");
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(188, 73, 1, 256, 256, 13);
	pMenu->AdjustWidth(10);
	print_if_false(gMovieTable[0].mUnlockId != -1, "First movie not found!");
	pMenu->AddEntry(gMovieTable[0].pDisplayName);
	for (i32 i = 1; i < 21; i++)
	{
		if (gMovieTable[i].mUnlockId != -1 && ((1 << gMovieTable[i].mUnlockId) & G_SAVE_GAME.field_88) != 0)
			pMenu->AddEntry(gMovieTable[i].pDisplayName);
	}
	pMenu->scrollbar_one = 1;
	pMenu->field_1B = 9;
	pMenu->scrollbar_zero = 0;
	pMenu->CentreX();
	pMenu->CentreY();
	if (pMenu->mNumLines > 9)
		pMenu->Zoom(2);
	else
		pMenu->Zoom(1);

	i32 v0 = 0;
	i32 cdNotFound = 0;
	i32 exitMenu = 0;
	i32 playId = -1;
	*(i32*)0x005512EC = 384;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 v15 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(v0, 38, "movie viewer", 1, 0, 150, -21, 29);
		pMenu->Display();
		if (cdNotFound)
		{
			Mess_SetRGB(0x80, 0x80, 0x80, 0);
			Mess_SetTextJustify(0);
			Mess_DrawText(256, 210, "cd not found - insert the cd", 0, 0x1000);
			Mess_DrawText(256, 225, "then select a movie to view", 0, 0x1000);
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		v0 = PShell_MoveTowards(v0, 128);
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return;
		CheckForPadUnplugged();
		pMenu->Update();
		if (PCSHELL_CheckTriggers(131616, 1, 1))
		{
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(0x23, 0x2000, 0);
			exitMenu = 1;
		}
		else
		{
			i32 IsMouseOverText = 0;
			if (PCSHELL_CheckTriggers(256, 1, 1))
			{
				u8 mJustification = pMenu->mJustification;
				const char* name = pMenu->mEntry[pMenu->mLine].name;
				i32 x, y;
				pMenu->GetEntryXY(name, &x, &y);
				IsMouseOverText = PCSHELL_IsMouseOverText(name, x, y, mJustification);
			}
			if (pMenu->mLine < 0x28 && (IsMouseOverText != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
			{
				G_SCONTROL[0].Start.Triggered = 0;
				G_SCONTROL[0].X.Triggered = 0;
				playId = -1;
				const char* selName = pMenu->mEntry[pMenu->mLine].name;
				for (i32 i = 0; i < 21; i++)
				{
					if (Utils_CompareStrings(selName, gMovieTable[i].pDisplayName) == 0)
						playId = gMovieTable[i].mUnlockId;
				}
				if (playId != -1)
					SFX_Play(0x1F, 0x2000, 0);
				else
					SFX_Play(0x1B, 0x2000, 0);
			}
		}
		if (exitMenu)
			break;
		if (playId != -1)
		{
			u8 r = GameFMV_PlayMovie((u8)playId, 1, 1, 1.0f);
			G_PAD_IDLE_TIME = 0;
			cdNotFound = (r == 0);
			playId = -1;
			continue;
		}
		if (G_VBLANKS == v15)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
	}

	delete pMenu;
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
}

// @Ok
i32 Shell_Options(EShellResult a1)
{
	print_if_false(gShellInitialized != 0, "Called Shell_MainMenu() without shell initialised");
	i32 v9 = 0;
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(254, 97, 0, 256, 256, 16);
	pMenu->AddEntry("keyboard configuration");
	pMenu->AddEntry("joystick configuration");
	pMenu->AddEntry("music and sound");
	pMenu->AddEntry("display options");
	pMenu->AddEntry("file system");
	i32 v4 = 18;
	if (PCINPUT_GetNumControllerButtons() == 0)
	{
		pMenu->EntryOff("joystick configuration");
		v4 = 0;
	}
	switch (a1)
	{
	case 3:
		pMenu->SetLine(4);
		break;
	case 14:
		pMenu->SetLine(0);
		break;
	case 15:
		if (pMenu->mEntry[1].unk_b != 0)
			pMenu->SetLine(1);
		else
			pMenu->SetLine(0);
		break;
	case 16:
		pMenu->SetLine(2);
		break;
	case 17:
		pMenu->SetLine(3);
		break;
	default:
		print_if_false(0, "Bad default sent to Shell_Config");
		break;
	}
	pMenu->Zoom(0);

	i32 v1 = 0;
	*(i32*)0x005512EC = 0;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 v12 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(v1, 38, "options", 1, 0, 150, -21, 29);
		pMenu->Display();
		if (pMenu->FinishedZooming())
		{
			PShell_InstructionalText();
			switch (pMenu->mLine)
			{
			case 0:
				Mess_DrawText(256, v4 + 180, "change keyboard configuration", 0, 0x1000);
				break;
			case 1:
				Mess_DrawText(256, v4 + 180, "change button configuration", 0, 0x1000);
				Mess_DrawText(256, v4 + 193, "and controller settings", 0, 0x1000);
				break;
			case 2:
				Mess_DrawText(256, v4 + 180, "adjust volume settings", 0, 0x1000);
				Mess_DrawText(256, v4 + 193, "for music and sound", 0, 0x1000);
				break;
			case 3:
				Mess_DrawText(256, v4 + 180, "change display settings", 0, 0x1000);
				break;
			case 4:
				Mess_DrawText(256, v4 + 180, "load or save a game", 0, 0x1000);
				break;
			default:
				break;
			}
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		i32 v13 = PShell_MoveTowards(v1, 128);
		*(i32*)0x005512EC = PShell_MoveTowards(*(i32*)0x005512EC, 384);
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return 0;
		CheckForPadUnplugged();
		PShell_NormalFont();
		pMenu->Update();
		if (PCSHELL_CheckTriggers(131616, 1, 1))
		{
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(0x23, 0x2000, 0);
			goto done;
		}
		i32 IsMouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			const char* name = pMenu->mEntry[pMenu->mLine].name;
			u8 mJustification = pMenu->mJustification;
			i32 x, y;
			pMenu->GetEntryXY(name, &x, &y);
			IsMouseOverText = PCSHELL_IsMouseOverText(name, x, y, mJustification);
		}
		if (pMenu->mLine < 0x28 && (IsMouseOverText != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
			break;
		if (G_VBLANKS == v12)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
		v1 = v13;
	}

	G_SCONTROL[0].Start.Triggered = 0;
	G_SCONTROL[0].X.Triggered = 0;
	SFX_Play(0x1F, 0x2000, 0);
	if (pMenu->ChoiceIs("keyboard configuration"))
		v9 = 14;
	if (pMenu->ChoiceIs("display options"))
		v9 = 17;
	if (pMenu->ChoiceIs("file system"))
		v9 = 3;
	if (pMenu->ChoiceIs("joystick configuration"))
		v9 = 15;
	if (pMenu->ChoiceIs("music and sound"))
		v9 = 16;

done:
	delete pMenu;
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
	return v9;
}

// Update 2026-08-31, functional-decompile session (PLAN.md relaxed bar): all 41 callees turned
// out to already be real implemented functions elsewhere in the repo (verified individually via
// tools/names.json + grepping every .cpp for their definitions), so the "roughly 30 still
// undecompiled" note above is stale. Full disasm (504 instructions, cross-checked against the
// Hex-Rays decompile via idalib) confirms:
// - credits.txt tokenizer: FileIO_Open("credits.txt") + Mem_New(fileSize) + FileIO_Load +
//   FileIO_Sync, then a hand-rolled parser for up to 520 "(r,g,b)name" lines into a
//   Mem_New(520*43) byte buffer, 43 bytes/record (R,G,B then a 40-char null-terminated name --
//   confirmed record layout from the store addresses at 0x4933c3-0x4933d6). A record with no
//   leading "(r,g,b)" tag keeps the PREVIOUS record's colour (R/G/B locals are seeded 0x80 once,
//   before the loop, and only overwritten inside the '(' branch) -- reproduced as-is. The two
//   nullsub_1() calls in the tokenizer ("Missing ) in rgb definition", "Excess left chars") are
//   the confirmed-empty print_if_false stub already omitted at call sites elsewhere in this repo
//   (see CSwinger_SwingBack in web.cpp); the name-copy loop has NO real bounds enforcement in the
//   original (the store happens before the useless assert), so a >40-char name in credits.txt
//   would spill into the next record's R byte -- a genuine original defect, reproduced not fixed.
// - Per-frame loop: identical shell-tick boilerplate to CheckForPadUnplugged above (same
//   Db_FlipClear/CalcPolyBufferEnd/PCGfx_BeginScene-EndScene/M3dMaths_RotMatrixYXZ/TransMatrix/
//   M3d_RenderSetup-Render-RenderCleanup/Pad_Update/DoVblankProcessing+Utils_VblankProcessing/
//   PCSHELL_Relax/Pause/gShellMenuAbort tail, confirmed address-for-address: dword_54D38C really
//   is gShellMenuAbort, dword_5598B8 really is DoVblankProcessing (idb_globals.txt), dword_66129C
//   really is Pad_IdleTime (idb_globals.txt) -- all already-established shell.cpp globals, no new
//   naming needed there), plus the credits-specific bits: a smooth 15-vblank-per-line scroll
//   (frame timer counts 0 down to -61440 in -4096 steps, i.e. "timer>>12" walks 0..-14 as a
//   per-line pixel Y offset before the next record is due; confirmed 61440/4096=15 rows of scroll
//   per 15px-tall line) and the "spidey" preview CDummy model, built and updated exactly like
//   CheckForPadUnplugged's CDropDownController widget (new via CClass::operator new + placement
//   new, matches decomp.me-adjacent front.cpp/web.cpp/pshell.cpp precedent for objects allocated
//   this way in the original).
// - gMikeCamera[0] fields (0x56F1B0 base, confirmed against panel.cpp's existing
//   "qword_56F1B4/dword_56F1BC = gMikeCamera[0].Position" comment and SCamera's field offsets):
//   Style/Position/Angles all zeroed once before the loop, then rebuilt every frame exactly like
//   CheckForPadUnplugged does (M3dMaths_RotMatrixYXZ -> TransMatrix -> M3d_RenderSetup).
// - Redbook_XAStat() (0x479D20) is COMDAT-folded with std::ctype<char>::_Term in this binary --
//   tools/names.json already has the real name for this address (same class of gotcha as
//   pshell.cpp's CClass/CItem::operator new folding, CLAUDE.md's "Link-time duplicate
//   elimination"). Used here to auto-restart the credits music track (67,14,0, same args as the
//   initial Redbook_XAPlay) once Redbook_XAStat()==4 (track finished); the "restart" flag it
//   clears is G_CARNAGE_XA_RELATED_TWO (0x68276C), already a shared macro in ps2redbook.h.
// - CDummy field pokes after construction (0x4936b0-0x4936c7, unconditional even on a failed
//   allocation -- same class of reproduced defect as CSwinger_SwingBack in web.cpp): mPos =
//   (50, 0, 350) in 4096-scaled units (204800/4096=50, 1433600/4096=350), mAngles.vy = 3584, and
//   a byte poke at offset 0x1D8 that was pure PADDING in shell.h before this session (now named
//   CDummy::field_1D8, see shell.h and its new VALIDATE entry).
// - Three small u16 arrays (0x553CE8/553CF8/553D0C) feed the CDummy ctor's pTrackA/B/C
//   parameters for the "spidey" costume; read directly via idalib get_bytes and confirmed
//   0xFFFF-terminated (same sentinel CDummy::SelectNewTrack already expects per shell.h), each
//   with exactly one xref in the whole binary (this function) via idalib xrefs_to. These are
//   almost certainly one row of the larger per-costume off_553Dxx table the CDummy::CDummy
//   comment above says Shell_CharacterViewer will eventually need to decode generically, but
//   Shell_RollCredits always wants the "spidey"/mType 50 row specifically, so it hardcodes these
//   addresses instead of doing a table lookup. Named gSpideyPreviewTrackA/B/C below.
// - G_SCONTROL[0].Type (dword_66126C) reused as the "pad still recognised" check, matching the
//   established idiom already used by CheckForPadUnplugged above -- NOTE this conflicts with
//   front.cpp's own unrelated tentative name for the same address (gFrontCardSlotChoice); kept
//   the shell.cpp-local SControl::Type interpretation for consistency within this file, since
//   that one is corroborated by ps2pad.cpp's own VALIDATEd offset (0x16C) and by this exact
//   read/clear/delete-on-unplug pattern, not just address adjacency.
struct SCreditsEntry
{
	u8 R;
	u8 G;
	u8 B;
	char Name[40];
};

// "spidey" (mType 50) preview-model track-id lists for the CDummy ctor's pTrackA/B/C. See the
// long comment above Shell_RollCredits for the evidence (raw bytes read via idalib, single xref).
// gSpideyPreviewTrackA = {0, 1, 21, 21, 21, 11, 0xFFFF}
// gSpideyPreviewTrackB = {294, 33, 34, 34, 34, 35, 34, 36, 293, 0xFFFF}
// gSpideyPreviewTrackC = {0, 290, 291, 292, 0xFFFF}
static u16 * const gSpideyPreviewTrackA = (u16*)0x553CE8;
static u16 * const gSpideyPreviewTrackB = (u16*)0x553CF8;
static u16 * const gSpideyPreviewTrackC = (u16*)0x553D0C;

// Same address/evidence as spidey.cpp's gPlayerSynthTickScratch (0x6B4CA4, address-adjacent to
// Vblanks/0x6B4CA0 and gTimerRelated/0x6B4CA8); spidey.cpp's own comment already names this exact
// function (0x4931E0) as one of the two other zeroers. Duplicated here as a plain file-local
// static, same convention as other repo-wide raw-address globals.
static i32 * const gPlayerSynthTickScratch = (i32*)0x6B4CA4;

// @Bogus
// Plain non-throwing placement new, same convention as front.cpp's/web.cpp's own file-local copy
// (the original builds the preview CDummy via a raw CClass::operator new call plus a manual
// constructor call, not a plain "new CDummy(...)" expression).
inline void* operator new(size_t, void* location)
{
	return location;
}

// Address 0x4931E0, 1602 bytes. See the long comment above for the session's findings.
// @Ok
void Shell_RollCredits(void)
{
	G_DB_SKY_COLOR = 0xFF000000;
	Db_UpdateSky();
	Front_ClearScreen();
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");

	i32 fileSize = FileIO_Open("credits.txt");
	char* fileBuf = static_cast<char*>(Mem_New(fileSize));
	FileIO_Load(fileBuf);
	FileIO_Sync();

	SCreditsEntry* records = static_cast<SCreditsEntry*>(Mem_New(520 * sizeof(SCreditsEntry)));

	char* p = fileBuf;
	u8 r = 0x80, g = 0x80, b = 0x80;
	i32 numRecords = 0;

	for (; numRecords < 520; numRecords++)
	{
		if (*p == '#')
			break;

		if (*p == '(')
		{
			++p;
			while (*p == ' ' || *p == '\t' || *p == '\r' || *p == '\n' || *p == ',')
				++p;

			i32 v = 0;
			while (*p >= '0' && *p <= '9') { v = v * 10 + (*p - '0'); ++p; }
			r = static_cast<u8>(v);
			while (*p == ' ' || *p == '\t' || *p == '\r' || *p == '\n' || *p == ',')
				++p;

			v = 0;
			while (*p >= '0' && *p <= '9') { v = v * 10 + (*p - '0'); ++p; }
			g = static_cast<u8>(v);
			while (*p == ' ' || *p == '\t' || *p == '\r' || *p == '\n' || *p == ',')
				++p;

			v = 0;
			while (*p >= '0' && *p <= '9') { v = v * 10 + (*p - '0'); ++p; }
			b = static_cast<u8>(v);
			while (*p == ' ' || *p == '\t' || *p == '\r' || *p == '\n' || *p == ',')
				++p;

			// Original: nullsub_1(*p == ')', "Missing ) in rgb definition in credits text").
			// nullsub_1 (0x4015B0) is the confirmed-empty print_if_false stub this repo already
			// omits at call sites; only the semantic assertion is preserved as this comment.
			++p;
		}

		records[numRecords].R = r;
		records[numRecords].G = g;
		records[numRecords].B = b;

		// Original: nullsub_1(nameLen < 40, "Excess left chars") every iteration, another no-op
		// print_if_false assertion (see above) -- the store below is unconditional in the
		// original, so a name of 40+ chars spills into the next record's R byte. Reproduced as-is
		// rather than adding a bounds check the original doesn't have.
		i32 nameLen = 0;
		while (*p != '\r')
		{
			records[numRecords].Name[nameLen] = *p;
			++nameLen;
			++p;
		}
		records[numRecords].Name[nameLen] = 0;
		p += 2;
	}

	Mem_Delete(fileBuf);

	G_MIKE_CAMERA[0].Position.vx = 0;
	G_MIKE_CAMERA[0].Position.vy = 0;
	G_MIKE_CAMERA[0].Position.vz = 0;
	G_MIKE_CAMERA[0].Angles.vx = 0;
	G_MIKE_CAMERA[0].Angles.vy = 0;
	G_MIKE_CAMERA[0].Angles.vz = 0;
	G_MIKE_CAMERA[0].Style = 0;

	i32 scrollIndex = 0;
	i32 rowTimer = 0;
	CDummy* pDummy = 0;

	for (;;)
	{
		*gPlayerSynthTickScratch = 0;
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();

		u32 startVblanks = G_VBLANKS;

		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);

		M3dMaths_RotMatrixYXZ(&G_MIKE_CAMERA[0].Angles, &G_MIKE_CAMERA[0].Transform);
		TransMatrix(&G_MIKE_CAMERA[0].Transform, &G_MIKE_CAMERA[0].Position);
		M3d_RenderSetup(G_MIKE_CAMERA, &G_VIEWPORT, G_PDOUBLE_BUFFER->OrderingTable);

		if (pDummy)
			M3d_Render(pDummy);

		M3d_RenderCleanup();
		Bit_Display();
		PShell_DefaultText();
		Mess_SetTextJustify(1);

		i32 rowY = rowTimer >> 12;
		if (rowY < 260)
		{
			i32 idx = scrollIndex;
			while (idx < numRecords && rowY < 260)
			{
				SCreditsEntry* rec = &records[idx];
				if (rec->Name[0] != 0)
				{
					Mess_SetRGB(rec->R, rec->G, rec->B, 0);
					Mess_SetRGBBottom(150 * rec->R / 256, 150 * rec->G / 256, 150 * rec->B / 256);
					Mess_DrawText(40, rowY, rec->Name, 0, 0x1000);
				}
				rowY += 15;
				idx++;
			}
		}

		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);

		Pad_Update();

		if (*gShellMenuAbort != 0)
			return;

		if (G_SCONTROL[0].Type != 0)
		{
			// keep the existing preview model
		}
		else
		{
			if (pDummy)
				delete pDummy;
			pDummy = 0;
			CheckForPadUnplugged();
		}

		if (PCSHELL_CheckTriggers(0x70330, 1, 1))
		{
			G_SCONTROL[0].X.Triggered = 0;
			G_SCONTROL[0].Triangle.Triggered = 0;
			G_SCONTROL[0].Circle.Triggered = 0;
			G_SCONTROL[0].Square.Triggered = 0;
			G_SCONTROL[0].Start.Triggered = 0;
			goto finished;
		}

		rowTimer -= 4096;
		if (rowTimer <= -61440)
		{
			rowTimer = 0;
			scrollIndex++;
			if (scrollIndex >= numRecords)
				goto finished;
		}

		if (!pDummy)
		{
			void* mem = CClass::operator new(sizeof(CDummy));
			if (mem)
			{
				pDummy = ::new (mem) CDummy("spidey", 50, 4096, -32, 0,
						gSpideyPreviewTrackA, gSpideyPreviewTrackB, gSpideyPreviewTrackC,
						0, 0, 0, 0);
			}
			else
			{
				pDummy = 0;
			}

			// Original writes these fields unconditionally, even down the "operator new
			// returned null" path (a genuine defect, reproduced not fixed -- same class of bug
			// as CSwinger_SwingBack in web.cpp).
			pDummy->mPos.vx = 204800;
			pDummy->mPos.vy = 0;
			pDummy->mPos.vz = 1433600;
			pDummy->mAngles.vy = 3584;
			pDummy->field_1D8 = 1;

			Redbook_XAPlay(67, 14, 0);
		}

		pDummy->AI();

		Bit_Move();
		Bit_RemoveDeadBits();

		if (Redbook_XAStat() == 4)
		{
			Redbook_XAStop();
			G_CARNAGE_XA_RELATED_TWO = 0;
			Redbook_XAPlay(67, 14, 0);
		}

		if (G_VBLANKS == startVblanks)
			Pause(1);

		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		gsub_430680();
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}

		PCSHELL_Relax();
	}

finished:
	if (pDummy)
		delete pDummy;
	Mem_Delete(records);
	Init_KillAll();
	G_PAD_IDLE_TIME = 0;
	Redbook_XAStop();
	Mess_DeleteAll();
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	gsub_430680();
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
	Front_ClearScreen();
}

// Real translation (0x498060, 472B). Draws a horizontal slider bar (used by
// Shell_SFXMusic for the music/voice/movie level bars): a1/a2 are the
// screen x/y of the bar, a3 is the value (0..256-ish range, clamped), a4 is
// a "highlighted" flag that picks a lighter (0x40) vs darker (0x80)
// grey tint for every piece.
//
// Gets its four pieces from a single Spool_FindAnim("slider", 1) call: the
// disassembly indexes off that one SAnimFrame* with byte offsets +0/+8/
// +16/+24 (sizeof(SAnimFrame) == 8), i.e. 4 consecutive frames in the
// "slider" anim starting at index 1. Draw order and geometry (not any
// string evidence) suggest: +24 is the moving knob (its x tracks `value`,
// 14x14), +0 is the left end cap (fixed x = a1+5, 10x10), +8 is the track
// background (stretched to a fixed 180x10 bar), +16 is the right end cap
// (fixed x = a1+195, 10x10). Kept as raw byte-offset pointers (not array
// indexing through a named base) to stay directly traceable to the
// disassembly's `ebp+N` shape (MSVC6 "shifted pointer into inner
// struct/array" idiom, see CLAUDE.md tips.txt).
//
// Each piece: get a POLY_FT4* via one of the two Panel_DrawTexturedPoly
// overloads, tint it grey (no null check before the field writes, matching
// the original -- a full poly buffer would already crash here in the real
// game), then stretch/position it with DCPanel_DrawTexturedPoly. The track
// piece skips Panel_DrawTexturedPoly's own quad setup (the 2-arg overload
// only builds the poly, not its geometry) and instead pokes the POLY_FT4
// fields directly: a 180x10 bar from (a1+15,a2) to (a1+195,a2+10), with
// u1/u3 (the right-edge U texcoords) each decremented by one texel (avoids
// sampling the next frame's texture in the tiled sheet at the exact right
// edge).
// @Ok
EXPORT void DrawSlider(i32 a1, i32 a2, i32 a3, i32 a4)
{
	SAnimFrame *pFrames = Spool_FindAnim("slider", 1);
	SAnimFrame *pLeftEnd = pFrames;
	SAnimFrame *pTrack = (SAnimFrame*)((u8*)pFrames + 8);
	SAnimFrame *pRightEnd = (SAnimFrame*)((u8*)pFrames + 16);
	SAnimFrame *pKnob = (SAnimFrame*)((u8*)pFrames + 24);

	i32 value = a3;
	if (value < 0)
		value = 0;
	else if (value > 256 || value == 255)
		value = 256;

	i32 leftEndX = a1 + 5;
	i32 knobX = ((180 * value) >> 8) + a1 + 5 + 4;

	POLY_FT4 *pKnobPoly = (POLY_FT4*)Panel_DrawTexturedPoly(pKnob, knobX, a2 - 2, 0);
	if (a4 != 0 && pKnobPoly != 0)
	{
		pKnobPoly->r0 = 0x40;
		pKnobPoly->g0 = 0x40;
		pKnobPoly->b0 = 0x40;
	}
	else
	{
		pKnobPoly->r0 = 0x80;
		pKnobPoly->g0 = 0x80;
		pKnobPoly->b0 = 0x80;
	}
	DCPanel_DrawTexturedPoly(1.0f, pKnobPoly, pKnob, knobX, a2 - 2, 14, 14, 0, 0);

	POLY_FT4 *pLeftEndPoly = (POLY_FT4*)Panel_DrawTexturedPoly(pLeftEnd, leftEndX, a2, 0);
	if (a4 != 0 && pLeftEndPoly != 0)
	{
		pLeftEndPoly->r0 = 0x40;
		pLeftEndPoly->g0 = 0x40;
		pLeftEndPoly->b0 = 0x40;
	}
	else
	{
		pLeftEndPoly->r0 = 0x80;
		pLeftEndPoly->g0 = 0x80;
		pLeftEndPoly->b0 = 0x80;
	}
	DCPanel_DrawTexturedPoly(2.0f, pLeftEndPoly, pLeftEnd, leftEndX, a2, 10, 10, 0, 0);

	POLY_FT4 *pTrackPoly = (POLY_FT4*)Panel_DrawTexturedPoly(pTrack, 0);
	if (a4 != 0)
	{
		pTrackPoly->r0 = 0x40;
		pTrackPoly->g0 = 0x40;
		pTrackPoly->b0 = 0x40;
	}
	else
	{
		pTrackPoly->r0 = 0x80;
		pTrackPoly->g0 = 0x80;
		pTrackPoly->b0 = 0x80;
	}
	pTrackPoly->u1--;
	pTrackPoly->u3--;
	pTrackPoly->y2 = (i16)(a2 + 10);
	pTrackPoly->y3 = (i16)(a2 + 10);
	pTrackPoly->x0 = (i16)(a1 + 15);
	pTrackPoly->x1 = (i16)(a1 + 195);
	pTrackPoly->x2 = (i16)(a1 + 15);
	pTrackPoly->x3 = (i16)(a1 + 195);
	pTrackPoly->y0 = (i16)a2;
	pTrackPoly->y1 = (i16)a2;
	DCPanel_DrawTexturedPoly(2.0f, pTrackPoly, pTrack, a1 + 15, a2, 180, 10, 0, 0);

	POLY_FT4 *pRightEndPoly = (POLY_FT4*)Panel_DrawTexturedPoly(pRightEnd, a1 + 195, a2, 0);
	u8 color = (a4 != 0 && pRightEndPoly != 0) ? 0x40 : 0x80;
	pRightEndPoly->r0 = color;
	pRightEndPoly->g0 = color;
	pRightEndPoly->b0 = color;
	DCPanel_DrawTexturedPoly(2.0f, pRightEndPoly, pRightEnd, a1 + 195, a2, 10, 10, 0, 0);
}
// Real translation (0x497F80, 219B). Mouse-drag helper for the same slider
// drawn by DrawSlider: a1/a2 are the bar's screen x/y (same as DrawSlider),
// a3 is the current value. Recomputes the knob's [left,right) hit box the
// same way DrawSlider positions the knob (left = ((180*value)>>8)+a1+9,
// right = left+14), then:
//  - if the mouse is over that box and the left button was just pressed,
//    latches the drag flag at *(u8*)0x006A7784 (same raw address already
//    dereferenced this way in this file, see the mouse-over-slider check
//    a few lines below in Shell_SFXMusic, so keeping the same style here);
//  - if the drag flag isn't latched, returns 0 (not dragging);
//  - if the left mouse button was released, clears the drag flag and
//    returns 0;
//  - otherwise, if the mouse actually moved this frame, reads its
//    position and converts it from PC screen space to the 512x240 "DC"
//    space DrawSlider's geometry is in, then returns -1/1/0 depending on
//    whether that position is left of, right of, or inside the knob box
//    (the caller uses this to nudge the value left/right or leave it).
// @Ok
EXPORT i32 SliderDrag(i32 a1, i32 a2, i32 a3)
{
	i32 value = a3;
	if (value < 0)
		value = 0;
	else if (value > 256 || value == 255)
		value = 256;

	i32 left = ((180 * value) >> 8) + a1 + 9;
	i32 right = left + 14;

	if (PCSHELL_IsMouseOver(left, a2 - 2, right, a2 - 2 + 14) != 0 && PCINPUT_IsMouseButtonPressed(0, 1) != 0)
	{
		*(u8*)0x006A7784 = 1;
	}
	else if (*(u8*)0x006A7784 == 0)
	{
		return 0;
	}

	if (PCINPUT_IsMouseButtonReleased(0) != 0)
	{
		*(u8*)0x006A7784 = 0;
		return 0;
	}

	if (*(u8*)0x006A7784 == 0 || PCSHELL_MouseMoved() == 0)
		return 0;

	i32 mouseX, mouseY;
	PCINPUT_GetMouseHotspotPosition(&mouseX, &mouseY);
	PCSHELL_CoordsPCtoDC(&mouseX, &mouseY);

	if (mouseX < left)
		return -1;
	return mouseX > right;
}

// @Ok
void Shell_SFXMusic(void)
{
	print_if_false(gShellInitialized != 0, "Called Shell_MainMenu() without shell initialised");
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(270, 90, 2, 256, 256, 20);
	pMenu->AddEntry("music and sfx level");
	pMenu->AddEntry("voice level");
	pMenu->AddEntry("movie level");
	pMenu->AddEntry("audio");
	pMenu->AddEntry("initial settings");
	pMenu->scrollbar_zero = 0;
	pMenu->Zoom(0);

	i32 v25 = 0;
	i32 v24 = 0;
	i32 v3 = 0;
	*(i32*)0x005512EC = 512;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 v29 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(v3, 38, "music and sound", 1, 0, 150, -21, 29);
		if (pMenu->FinishedZooming())
		{
			DrawSlider(270, 81, (gGameState[12] << 8) / 0x3FFF, pMenu->mLine != 0);
			DrawSlider(270, 101, gGameState[13], pMenu->mLine != 1);
			DrawSlider(270, 121, gGameState[11], pMenu->mLine != 2);
			if (pMenu->mLine == 3)
				PShell_DefaultText();
			else
			{
				Mess_SetTextJustify(0);
				Mess_SetRGB(0x45, 0x3C, 0x6B, 0);
				Mess_SetRGBBottom(0x28, 35, 62);
			}
			PShell_SmallFont();
			if (G_BOOT_ROM_SOUND_MODE != 0)
				Mess_DrawText(368, 149, "mono", 0, 0x1000);
			else
				Mess_DrawText(368, 149, "stereo", 0, 0x1000);
			PShell_NormalFont();
		}
		pMenu->Display();
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		i32 v30 = PShell_MoveTowards(v3, 150);
		*(i32*)0x005512EC = PShell_MoveTowards(*(i32*)0x005512EC, 384);
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Mess_Update();
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return;
		CheckForPadUnplugged();
		i32 v23 = 0;
		i32 v5 = 0;
		if (PCSHELL_CheckTriggers(256, 1, 0))
		{
			const char* name = pMenu->mEntry[pMenu->mLine].name;
			u8 mJustification = pMenu->mJustification;
			i32 x, y;
			pMenu->GetEntryXY(name, &x, &y);
			if (PCSHELL_IsMouseOverText(name, x, y, mJustification))
				v5 = 1;
		}
		if (pMenu->mLine < 0x28 && (v5 != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
		{
			G_SCONTROL[0].X.Triggered = 0;
			v23 = 1;
		}
		if (PCSHELL_CheckTriggers(131616, 1, 1))
		{
			G_SCONTROL[0].Start.Triggered = 0;
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(0x23, 0x2000, 0);
			Mess_DeleteAll();
			delete pMenu;
			Pause(1);
			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");
			Pad_ClearTriggers(G_SCONTROL);
			*(i32*)0x005512EC = 512;
			SPIDEYDX_SaveSettings();
			return;
		}
		PShell_NormalFont();
		pMenu->Update();
		i32 v7 = 0;
		if (*(u8*)0x006A7784 == 0)
		{
			i32 v8 = 0;
			for (i32 i = 81; i < 141; i += 20)
			{
				if (PCSHELL_IsMouseOver(270, i, 480, i + 10))
					pMenu->SetLine(v8);
				v8++;
			}
		}
		i32 mLine = pMenu->mLine;
		if (mLine != 0)
		{
			if (mLine == 1)
				v7 = SliderDrag(270, 101, gGameState[13]);
			else if (mLine == 2)
				v7 = SliderDrag(270, 121, gGameState[11]);
		}
		else
			v7 = SliderDrag(270, 81, (gGameState[12] << 8) / 0x3FFF);

		if (PCSHELL_CheckTriggers(49164, 0, 0))
		{
			if (v24 == 0 || (v24 > 20 && (v24 & 1) == 0))
			{
				if (PCSHELL_CheckTriggers(32776, 0, 0))
					v7 = 1;
				if (PCSHELL_CheckTriggers(16388, 0, 0))
					v7 = -1;
			}
			v24++;
		}
		else
			v24 = 0;

		if (pMenu->mLine != 1 && v25 != 0)
			Redbook_XAStop();

		switch (pMenu->mLine)
		{
		case 0:
			if (v7 != 0)
			{
				i32 v12 = (v7 << 14) / 16;
				i32 v14 = v12 + gGameState[12];
				gGameState[12] += v12;
				if (v14 < 0)
					v14 = 0;
				else if (v14 > 0x3FFF)
					v14 = 0x3FFF;
				gGameState[12] = v14;
				G_SAVE_GAME.field_94 = v14;
			}
			if (v23 != 0)
				SFX_Play(0x1C, 0x2000, 0);
			break;
		case 1:
			if (v7 != 0)
			{
				i32 v16 = (v7 << 8) / 16;
				i32 v15 = v16 + gGameState[13];
				gGameState[13] += v16;
				if (v15 < 0)
					v15 = 0;
				else if (v15 > 255)
					v15 = 255;
				gGameState[13] = v15;
				G_SAVE_GAME.field_9C = v15;
				v25 = 1;
			}
			if (v23 != 0 || v25 == 0)
			{
				if (Rnd(2) != 0)
					Redbook_XAPlay(58, 8, 0);
				else
					Redbook_XAPlay(64, 12, 0);
			}
			break;
		case 2:
			{
				i32 v16 = (v7 << 8) / 16;
				i32 v17 = v16 + gGameState[11];
				gGameState[11] += v16;
				if (v17 < 0)
					v17 = 0;
				else if (v17 > 255)
					v17 = 255;
				gGameState[11] = v17;
				G_SAVE_GAME.field_98 = v17;
			}
			break;
		case 3:
			if (v23 != 0 || v7 != 0)
			{
				SFX_Play(0x1F, 0x2000, 0);
				DCSetBootROMSoundMode(G_BOOT_ROM_SOUND_MODE == 0);
				G_SAVE_GAME.field_A0 = G_BOOT_ROM_SOUND_MODE;
			}
			break;
		case 4:
			if (v23 != 0)
			{
				SFX_Play(0x1F, 0x2000, 0);
				gGameState[12] = 11087;
				gGameState[11] = 177;
				gGameState[13] = 201;
				G_SAVE_GAME.field_94 = 11087;
				G_SAVE_GAME.field_98 = 177;
				G_SAVE_GAME.field_9C = 201;
			}
			break;
		default:
			break;
		}
		if (pMenu->mLine != 1)
			v25 = 0;
		if (G_VBLANKS == v29)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
		v3 = v30;
	}
}

// @Ok
// Memory card save machine. States: 0 intro, 1 card load, 4 format confirm,
// 7 card error, 8 slot menu, 9 overwrite confirm, 10 writing, 11 write failed,
// 12 save complete, 13 card full, 14 formatting, 15 format failed, 16 format
// complete. States 13-16 draw no text (original defect, only the title bar).
// The gShellMenuAbort check returns WITHOUT writing *pResult or cleaning up,
// exactly like the original (jump straight to the plain epilogue).
void Shell_SaveGame(const u32 *pFromGame, u32 *pResult)
{
	// defined once in PCShell.cpp, called here through local externs (same
	// pattern as Shell_ScreenAdjust/Shell_ShowRecord above).
	extern void gsub_430880(void);
	extern void gsub_430680(void);

	i32 fromGame = *pFromGame;
	gShellFromGame = fromGame;
	i32 savedSkyColor = 0;
	if (fromGame != 0)
	{
		PShell_Initialise();
		M3d_FadeColour = 0xFFFFFF;
		M3dInit_SetFoggingParams(0, 0x1770, 0x800);
		savedSkyColor = G_DB_SKY_COLOR;
		G_DB_SKY_COLOR = 0;
		G_BFOGGING_RELATED = 1;
		Db_UpdateSky();
	}

	// checksum of the save being written, 46 dwords after mChecksum
	G_SAVE_GAME.mChecksum = Shell_CalculateGameChecksum(&G_SAVE_GAME);

	PShell_NormalFont();

	CMenu* pMenu = new CMenu(256, 0, 0, 256, 256, 16);
	pMenu->AddEntry("no");
	pMenu->AddEntry("yes");

	i32 state = 0;
	CMenu* pSlotMenu = 0;
	i32 titleEase = 0;
	i32 introCount = 0;
	i32 writeCount = 0;
	i32 retryCount = 0;
	i32 writeFailed = 0;
	i32 result = 0;
	*(i32*)0x005512EC = 384;

	while (1)
	{
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 vblanks = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(titleEase, 38, "save game", 1, 0, 150, -21, 29);
		Mess_SetRGB(0x6B, 0x5D, 0xA7, 0);
		Mess_SetRGBBottom(0x3E, 54, 96);
		Mess_SetSort(0);
		switch (state)
		{
		case 8:
			if (pSlotMenu != 0 && pSlotMenu->FinishedZooming())
			{
				SSaveGame* pSlot = &gSaveGameSlots[pSlotMenu->mLine];
				i32 slotValid = 0;
				if (pSlot->mChecksum != 0 && pSlot->mChecksum == Shell_CalculateGameChecksum(pSlot))
				{
					slotValid = 1;
					Mess_SetTextJustify(0);
					Mess_SetRGB(0x45, 0x3C, 0x6B, 0);
					Mess_SetRGBBottom(0x28, 35, 62);
					Mess_SetTextJustify(1);
					Mess_DrawText(216, 70, "Okay to overwrite", 0, 0x1000);
					Mess_SetRGB(0x80, 0x80, 0x80, 0);
					Mess_SetRGBBottom(0x45, 60, 107);
					Shell_DisplayGameInfo(190, 85, pSlot);
				}
				Mess_SetTextJustify(0);
				Mess_SetRGB(0x45, 0x3C, 0x6B, 0);
				Mess_SetRGBBottom(0x28, 35, 62);
				Mess_SetTextJustify(1);
				if (slotValid != 0)
					Mess_DrawText(216, 115, "previously saved game", 0, 0x1000);
				else
					Mess_DrawText(190, 70, "in this save slot ?", 0, 0x1000);
				Mess_SetRGB(0x80, 0x80, 0x80, 0);
				Mess_SetRGBBottom(0x45, 60, 107);
				Shell_DisplayGameInfo(190, slotValid != 0 ? 130 : 85, &G_SAVE_GAME);
			}
			if (pSlotMenu != 0)
				pSlotMenu->Display();
			break;
		case 9:
			Mess_DrawText(256, 83, "Okay to overwrite", 0, 0x1000);
			Mess_DrawText(256, 100, "previously saved game", 0, 0x1000);
			Mess_DrawText(256, 117, "in this save slot ?", 0, 0x1000);
			pMenu->mY = 148;
			pMenu->Display();
			break;
		case 10:
			Mess_DrawText(256, 98, "Now Saving data", 0, 0x1000);
			break;
		case 11:
			Mess_DrawText(256, 110, "attempt to write to", 0, 0x1000);
			Mess_DrawText(256, 127, "save file failed !", 0, 0x1000);
			Mess_DrawText(256, 170, "press enter to continue.", 0, 0x1000);
			break;
		case 12:
			Mess_DrawText(256, 110, "save completed", 0, 0x1000);
			Mess_DrawText(256, 140, "press enter to continue.", 0, 0x1000);
			break;
		default:
			break;
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		titleEase = PShell_MoveTowards(titleEase, 128);
		if ((++TTime & 1) != 0)
			Card_CheckStatus(0, 0);
		if ((pSlotMenu != 0 && pSlotMenu->mLine > 0x28) || pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return;
		i32 vSelect, vBack, vAny, vMouse;
		Shell_ReadTriggers(&vSelect, &vBack, &vAny, &vMouse);
		if (state == 9)
		{
			if (pMenu != 0 && pMenu->mLine >= 0x28)
			{
				vSelect = 0;
				vMouse = 0;
			}
		}
		else if (pSlotMenu != 0 && pSlotMenu->mLine >= 0x28)
		{
			vSelect = 0;
			vMouse = 0;
		}
		if (writeFailed != 0)
		{
			vSelect = 0;
			vBack = 0;
			vAny = 0;
			vMouse = 0;
			writeFailed = 0;
		}
		i32 IsMouseOverText = 0;
		i32 exiting = 0;
		switch (state)
		{
		case 0:
			if (introCount != 0)
			{
				if (--introCount == 0)
					state = 1;
			}
			else
			{
				introCount = 10;
			}
			break;
		case 1:
			if (CardStatus == -2)
			{
				state = 4;
				pMenu->Reset();
			}
			else if (CardStatus == -1)
			{
				state = 7;
			}
			else if (CardStatus == 1)
			{
				if (Card_Load() != 0)
				{
					Pause(1);
					state = Card_GetFreeBlocks(0, 0) >= 1 ? 8 : 13;
				}
				else
				{
					state = 8;
				}
			}
			break;
		case 4:
			pMenu->Update();
			if (CardStatus == -1)
			{
				state = 7;
			}
			else if (CardStatus > 0 && CardStatus <= 2)
			{
				state = 0;
			}
			if (vSelect != 0)
			{
				if (pMenu->ChoiceIs("yes"))
				{
					SFX_Play(0x1F, 0x2000, 0);
					state = 14;
					writeCount = 4;
					retryCount = 4;
				}
				else
				{
					exiting = 1;
				}
			}
			break;
		case 7:
			if (pSlotMenu != 0)
			{
				delete pSlotMenu;
				pSlotMenu = 0;
			}
			if (CardStatus == -2 || (CardStatus > 0 && CardStatus <= 2))
			{
				state = 0;
			}
			else if (G_SCONTROL[0].Circle.Triggered != 0)
			{
				G_SCONTROL[0].Circle.Triggered = 0;
			}
			break;
		case 8:
		{
			if (CardStatus == -1)
			{
				state = 7;
				break;
			}
			if (pSlotMenu == 0)
			{
				PShell_NormalFont();
				pSlotMenu = new CMenu(90, 70, 0, 256, 256, 15);
				Shell_AddGameSlots(pSlotMenu);
				pSlotMenu->Zoom(0);
			}
			pSlotMenu->Update();
			if (PCSHELL_CheckTriggers(256, 1, 1))
			{
				i32 mLine = pSlotMenu->mLine;
				u8 mJust = pSlotMenu->mJustification;
				const char* name = pSlotMenu->mEntry[mLine].name;
				i32 x, y;
				pSlotMenu->GetEntryXY(name, &x, &y);
				IsMouseOverText = PCSHELL_IsMouseOverText(name, x, y, mJust);
			}
			if (vSelect == 0 && IsMouseOverText == 0)
				break;
			SFX_Play(0x1F, 0x2000, 0);
			SSaveGame* pSlot = &gSaveGameSlots[pSlotMenu->mLine];
			if (pSlot->mChecksum != 0 && pSlot->mChecksum == Shell_CalculateGameChecksum(pSlot))
			{
				state = 9;
				pMenu->Reset();
				Pad_ClearTriggers(G_SCONTROL);
				break;
			}
			i32 v34 = Shell_InputName(G_SAVE_GAME.field_3F, 1, 0, 0);
			if (v34 == -1)
			{
				state = 7;
				if (pSlotMenu != 0)
				{
					delete pSlotMenu;
					pSlotMenu = 0;
				}
			}
			else if (v34 != 0)
			{
				if (v34 == 1)
				{
					SFX_Play(0x1F, 0x2000, 0);
					G_SAVE_GAME.mChecksum = Shell_CalculateGameChecksum(&G_SAVE_GAME);
					memcpy(pSlot, &G_SAVE_GAME, sizeof(SSaveGame));
					for (i32 j = 0; j < NUM_CHALLS; j++)
						Merge((SScore*)(gMergeBuffer + j * 25 + 3), &gGlobalRecords.mScores[j * 5], gChallenges[j].field_C);
					state = 10;
					gGlobalRecords = *(SRecords*)gMergeBuffer;
					writeCount = 4;
					retryCount = 4;
				}
				else
				{
					print_if_false(0, "Bad return value from Shell_InputName");
				}
			}
			else
			{
				state = 8;
			}
			break;
		}
		case 9:
		{
			if (CardStatus == -1)
			{
				state = 7;
				break;
			}
			pMenu->Update();
			if (PCSHELL_CheckTriggers(256, 1, 1))
			{
				i32 mLine = pMenu->mLine;
				u8 mJust = pMenu->mJustification;
				const char* name = pMenu->mEntry[mLine].name;
				i32 x, y;
				pMenu->GetEntryXY(name, &x, &y);
				IsMouseOverText = PCSHELL_IsMouseOverText(name, x, y, mJust);
			}
			if (vSelect != 0 || IsMouseOverText != 0)
			{
				if (pMenu->ChoiceIs("yes"))
				{
					SFX_Play(0x1F, 0x2000, 0);
					if (pSlotMenu != 0)
					{
						// prefill the name prompt with the existing save's name
						Utils_CopyString(gSaveGameSlots[pSlotMenu->mLine].field_3F, G_SAVE_GAME.field_3F, 9);
						G_SAVE_GAME.mChecksum = Shell_CalculateGameChecksum(&G_SAVE_GAME);
						i32 v48 = Shell_InputName(G_SAVE_GAME.field_3F, 1, 0, 0);
						if (v48 == -1)
						{
							state = 7;
							delete pSlotMenu;
							pSlotMenu = 0;
							Pad_ClearTriggers(G_SCONTROL);
						}
						else if (v48 != 0)
						{
							if (v48 == 1)
							{
								SFX_Play(0x1F, 0x2000, 0);
								G_SAVE_GAME.mChecksum = Shell_CalculateGameChecksum(&G_SAVE_GAME);
								memcpy(&gSaveGameSlots[pSlotMenu->mLine], &G_SAVE_GAME, sizeof(SSaveGame));
								for (i32 k = 0; k < NUM_CHALLS; k++)
									Merge((SScore*)(gMergeBuffer + k * 25 + 3), &gGlobalRecords.mScores[k * 5], gChallenges[k].field_C);
								gGlobalRecords = *(SRecords*)gMergeBuffer;
								state = 10;
								writeCount = 4;
								retryCount = 4;
								Pad_ClearTriggers(G_SCONTROL);
							}
							else
							{
								print_if_false(0, "Bad return value from Shell_InputName");
								Pad_ClearTriggers(G_SCONTROL);
							}
						}
						else
						{
							SFX_Play(0x23, 0x2000, 0);
							state = 8;
							Pad_ClearTriggers(G_SCONTROL);
						}
					}
				}
				else
				{
					SFX_Play(0x23, 0x2000, 0);
					state = 8;
					Pad_ClearTriggers(G_SCONTROL);
				}
			}
			// back button: fall back to the slot menu (runs even on the same
			// frame as a menu selection, overwriting the state set above)
			if (vBack != 0)
			{
				SFX_Play(0x23, 0x2000, 0);
				state = 8;
				vBack = 0;
				Pad_ClearTriggers(G_SCONTROL);
			}
			break;
		}
		case 10:
			if (CardStatus == -1)
			{
				state = 7;
			}
			else if (pSlotMenu != 0)
			{
				if (--writeCount < 0 && CardStatus != 0)
				{
					if (Card_Write() == 0)
					{
						delete pSlotMenu;
						pSlotMenu = 0;
						state = 12;
						writeFailed = 1;
					}
					else if (--retryCount >= 0)
					{
						writeCount = 60 * (4 - retryCount);
					}
					else
					{
						state = 11;
						i32 v56 = pSlotMenu->mLine;
						writeFailed = 1;
						gSaveGameSlots[v56].mChecksum = 0;
					}
				}
			}
			break;
		case 11:
			if (vAny == 0 && !PCSHELL_CheckTriggers(256, 1, 1))
				break;
			SFX_Play(0x1F, 0x2000, 0);
			state = 8;
			break;
		case 12:
			if (vAny == 0 && !PCSHELL_CheckTriggers(256, 1, 1))
				break;
			SFX_Play(0x1F, 0x2000, 0);
			result = 1;
			exiting = 1;
			break;
		case 13:
			switch (CardStatus)
			{
			case -2:
				state = 0;
				break;
			case -1:
				state = 7;
				break;
			case 2:
				state = 0;
				break;
			default:
				break;
			}
			if (vAny != 0 || PCSHELL_CheckTriggers(256, 1, 1))
				exiting = 1;
			break;
		case 14:
			if (CardStatus == -1)
			{
				state = 7;
			}
			else if (--writeCount < 0 && CardStatus != 0)
			{
				if (Card_FormatCard(0, 0) == 1)
				{
					state = 16;
					writeFailed = 1;
				}
				else if (--retryCount >= 0)
				{
					writeCount = 60 * (4 - retryCount);
				}
				else
				{
					state = 15;
					writeFailed = 1;
				}
			}
			break;
		case 15:
			if (CardStatus == -1)
			{
				state = 7;
			}
			else if (vAny != 0 || PCSHELL_CheckTriggers(256, 1, 1))
			{
				exiting = 1;
			}
			break;
		case 16:
			if (vAny == 0 && !PCSHELL_CheckTriggers(256, 1, 1))
				break;
			SFX_Play(0x1F, 0x2000, 0);
			state = 0;
			break;
		default:
			break;
		}
		if (exiting != 0)
			SFX_Play(0x23, 0x2000, 0);
		if (exiting == 0)
		{
			if (G_VBLANKS == vblanks)
				Pause(1);
			DoVblankProcessing = 0;
			Pause(1);
			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");
			gsub_430680();
			if (DoVblankProcessing == 0)
			{
				Utils_VblankProcessing();
				DoVblankProcessing = 1;
			}
			PCSHELL_Relax();
			continue;
		}
		if (pMenu != 0)
			delete pMenu;
		if (pSlotMenu != 0)
			delete pSlotMenu;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		gsub_430680();
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		Pad_ClearTriggers(G_SCONTROL);
		if (fromGame != 0)
			PShell_Cleanup();
		*pResult = result;
		if (fromGame != 0)
		{
			G_BFOGGING_RELATED = 1;
			G_DB_SKY_COLOR = savedSkyColor;
			Db_UpdateSky();
		}
		return;
	}
}

// Shell_ScreenAdjust and Shell_ShowRecord call these as real out-of-line functions
// in the original, keep the MSVC inliner away (same trick as PCShell.cpp's
// gsub_430680/gsub_430880, needed because these stubs live in the same
// TU as their callers). CheckForPadUnplugged and gShellMenuEase/gShellMenuAbort now
// declared earlier in this file (before the Shell_Choose* family), see there.
#ifdef _MSC_VER
#pragma auto_inline(off)
#endif
// footer label text pointers, same string-pointer table idiom as pshell.cpp's
// gText* macros. Contents confirmed against the exe's raw .data (0x54B89C ->
// "select", 0x54B8A4 -> "back", 0x54B98C -> "Option 1").
#define gTextSelect (*reinterpret_cast<char**>(0x0054B89C))
#define gTextBack (*reinterpret_cast<char**>(0x0054B8A4))
#define gTextOption1 (*reinterpret_cast<char**>(0x0054B98C))

// unnamed helper called once per screen adjust / show record frame, address 0x498240.
// Draws the footer bar: select/back/Option 1 labels, the two xtri button icons
// (gAnimTable[23], "xtri") plus one Buttons icon (gAnimTable[3], "Buttons "),
// and the highlight strip behind them.
// @Ok
// @Matching
EXPORT void gsub_498240(i32 x, i32 y)
{
	PShell_SmallFont();
	Mess_SetShadowRGB(0xFF);
	Mess_SetTextJustify(0);
	Mess_SetRGB(0x80, 0x80, 0x80, 0);
	Mess_SetRGBBottom(0x45, 0x3C, 0x6B);
	Mess_DrawText(x - 25, y, gTextSelect, 0, 0x1000);
	Mess_DrawText(x + 59, y, gTextBack, 0, 0x1000);
	Mess_DrawText(x - 128, y, gTextOption1, 0, 0x1000);

	POLY_FT4* pPoly = (POLY_FT4*)Panel_DrawTexturedPoly(gAnimTable[23], x - 80, y - 10, G_SORT);
	print_if_false(pPoly != 0, "error");
	if (pPoly)
	{
		pPoly->b0 = 0x80;
		pPoly->g0 = 0x80;
		pPoly->r0 = 0x80;
		DCPanel_DrawTexturedPoly(1.0f, pPoly, gAnimTable[23], x - 85, y - 11, 20, 12, G_SORT, 0);
	}

	pPoly = (POLY_FT4*)Panel_DrawTexturedPoly(gAnimTable[23] + 1, x + 14, y - 10, G_SORT);
	print_if_false(pPoly != 0, "error");
	if (pPoly)
	{
		pPoly->b0 = 0x80;
		pPoly->g0 = 0x80;
		pPoly->r0 = 0x80;
		DCPanel_DrawTexturedPoly(1.0f, pPoly, gAnimTable[23] + 1, x + 9, y - 11, 20, 12, G_SORT, 0);
	}

	pPoly = (POLY_FT4*)Panel_DrawTexturedPoly(gAnimTable[3] + 1, x - 189, y - 10, G_SORT);
	print_if_false(pPoly != 0, "error");
	if (pPoly)
	{
		pPoly->b0 = 0x80;
		pPoly->g0 = 0x80;
		pPoly->r0 = 0x80;
		DCPanel_DrawTexturedPoly(1.0f, pPoly, gAnimTable[3] + 1, x - 194, y - 11, 20, 12, G_SORT, 0);
	}

	PShell_DrawHighlight(0x200, y - 17, x - 832, 24);
	PShell_NormalFont();
	Mess_SetShadowRGB(0x29);
}
#ifdef _MSC_VER
#pragma auto_inline(on)
#endif

static char* STR_SCREEN_ADJUST_TITLE = "screen adjust";
static char* STR_SCREEN_ADJUST_LINE1 = "Use the directional";
static char* STR_SCREEN_ADJUST_LINE2 = "buttons to center";
static char* STR_SCREEN_ADJUST_LINE3 = "the screen";

// @Ok
// @Matching
void Shell_ScreenAdjust(void)
{
	// defined once in PCShell.cpp, called here through a local extern (same pattern as
	// Shell_TitleScreen's gsub_430880 call below).
	extern void gsub_430880(void);
	extern void gsub_430680(void);

	print_if_false(gShellInitialized != 0, "Called Shell_MainMenu() without shell initialised");

	i32 savedX = G_DOUBLE_BUFFER[0].Disp.screen.x;
	i32 savedY = G_DOUBLE_BUFFER[0].Disp.screen.y;
	i32 cancelled = 0;

	i32 titleEase = 0;
	gShellMenuEase = 0x2C8;

	while (1)
	{
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();

		i32 vblanksSnapshot = G_VBLANKS;

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1u, -1);

		gsub_498240(gShellMenuEase, 0xDE);

		Shell_DrawBackground();

		Shell_DrawTitleBar(titleEase, 0x26, STR_SCREEN_ADJUST_TITLE, 1, 0, 0x96, -21, 0x1D);

		PShell_DefaultText();
		Mess_SetRGB(0x6Bu, 0x5Du, 0xA7u, 0);
		Mess_SetRGBBottom(0x3Eu, 0x36, 0x60);
		Mess_DrawText(0x100, 0x64, STR_SCREEN_ADJUST_LINE1, 0, 0x1000u);
		Mess_DrawText(0x100, 0x75, STR_SCREEN_ADJUST_LINE2, 0, 0x1000u);
		Mess_DrawText(0x100, 0x86, STR_SCREEN_ADJUST_LINE3, 0, 0x1000u);

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		titleEase = PShell_MoveTowards(titleEase, 0x80);
		gShellMenuEase = PShell_MoveTowards(gShellMenuEase, 0x180);

		Pad_Update();

		if (*gShellMenuAbort)
			return;

		CheckForPadUnplugged();

		if (G_SCONTROL[0].Right.Pressed && G_DOUBLE_BUFFER[0].Disp.screen.x < 0x20)
		{
			G_DOUBLE_BUFFER[0].Disp.screen.x++;
			G_DOUBLE_BUFFER[1].Disp.screen.x++;
		}

		if (G_SCONTROL[0].Left.Pressed && G_DOUBLE_BUFFER[0].Disp.screen.x > 0)
		{
			G_DOUBLE_BUFFER[0].Disp.screen.x--;
			G_DOUBLE_BUFFER[1].Disp.screen.x--;
		}

		if (G_SCONTROL[0].Up.Pressed && G_DOUBLE_BUFFER[0].Disp.screen.y > 0)
		{
			G_DOUBLE_BUFFER[0].Disp.screen.y--;
			G_DOUBLE_BUFFER[1].Disp.screen.y--;
			G_SCONTROL[0].Up.Triggered = 0;
		}

		if (G_SCONTROL[0].Down.Pressed && G_DOUBLE_BUFFER[0].Disp.screen.y < 0x20)
		{
			G_DOUBLE_BUFFER[0].Disp.screen.y++;
			G_DOUBLE_BUFFER[1].Disp.screen.y++;
			G_SCONTROL[0].Down.Triggered = 0;
		}

		if (G_SCONTROL[0].Circle.Triggered)
		{
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(0x23, 0x2000, 0);
			cancelled = 1;
			break;
		}

		if (G_SCONTROL[0].X.Triggered || G_SCONTROL[0].Start.Triggered)
		{
			SControl* pad = G_SCONTROL;
			pad[0].Start.Triggered = 0;
			pad[0].X.Triggered = 0;
			SFX_Play(0x1F, 0x2000, 0);
			break;
		}

		if (G_VBLANKS == vblanksSnapshot)
			Pause(1);

		*(volatile i32*)&DoVblankProcessing = 0;
		Pause(1);
		DrawSync();
		gsub_430680();

		if (!*(volatile i32*)&DoVblankProcessing)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}

		PCSHELL_Relax();
	}

	Pause(1);
	DrawSync();
	gsub_430680();
	DrawSync();
	Pad_ClearTriggers(G_SCONTROL);

	if (cancelled)
	{
		G_DOUBLE_BUFFER[0].Disp.screen.x = savedX;
		G_DOUBLE_BUFFER[0].Disp.screen.y = savedY;
		G_DOUBLE_BUFFER[1].Disp.screen.x = savedX;
		G_DOUBLE_BUFFER[1].Disp.screen.y = savedY;
	}

	*reinterpret_cast<i32*>(&G_SAVE_GAME.field_A4) = G_DOUBLE_BUFFER[0].Disp.screen.x;
	*reinterpret_cast<i32*>(&G_SAVE_GAME.field_A8) = G_DOUBLE_BUFFER[0].Disp.screen.y;

	gShellMenuEase = 0x200;
}

// CRecordBox is now declared in shell.h (moved 2026-08-27, pshell.cpp needs
// it too for the end-of-training record box; see the comment on the class).
// The constructor/destructor moved to pshell.cpp on 2026-08-27 (see the
// comment there): IDA on the real exe showed Shell_ShowRecord's SEH cleanup
// frame around `new CRecordBox(...)` needs the constructor's body to be
// INVISIBLE to the compiler at the call site, not just un-inlined.

// column header / score-unit label strings, read directly out of the
// original SpideyPC.exe .data section (they're stored as real char*
// globals, not inline literals: the original loads them with
// "mov eax,[54B8xxh]", a dereference, not "push offset"). Addresses:
// Place 0x54B8C8, Name 0x54B8CC, Time 0x54B8D0, Kills 0x54B8D4,
// Items 0x54B8D8, Points 0x54B8DC, "---" 0x54B8E0. We don't pin our own
// globals to those addresses (relocated data addresses are an accepted
// diff per the matching discipline), just keep the same string content.
static char* gRecordPlaceLabel = "Place";
static char* gRecordNameLabel = "Name";
static char* gRecordTimeLabel = "Time";
static char* gRecordKillsLabel = "Kills";
static char* gRecordItemsLabel = "Items";
static char* gRecordPointsLabel = "Points";
static char* gRecordDashesLabel = "---";

// @Ok
// @Matching
void CRecordBox::Display(void)
{
	Mess_SetSort(0);

	if (field_30)
	{
		Mess_SetRGB(0x45, 0x3C, 0x6B, 0);

		Mess_DrawText(field_1C + 0x27, field_20 + 0xF, gRecordPlaceLabel, 0, 0x1000u);
		Mess_DrawText(field_1C + 0x8B, field_20 + 0xF, gRecordNameLabel, 0, 0x1000u);

		switch (field_3C->mScoreUnits)
		{
			case 0:
				Mess_DrawText(field_1C + 0xEF, field_20 + 0xF, gRecordTimeLabel, 0, 0x1000u);
				break;
			case 1:
				Mess_DrawText(field_1C + 0xEF, field_20 + 0xF, gRecordKillsLabel, 0, 0x1000u);
				break;
			case 2:
				Mess_DrawText(field_1C + 0xEF, field_20 + 0xF, gRecordItemsLabel, 0, 0x1000u);
				break;
			case 3:
				Mess_DrawText(field_1C + 0xEF, field_20 + 0xF, gRecordPointsLabel, 0, 0x1000u);
				break;
			default:
				print_if_false(0, "Bad ScoreUnits");
				break;
		}

		i32 y = field_20 + 0x21;

		if (field_34)
			field_34--;

		if (field_35)
			field_35--;

		if (!field_35 && field_36 < 5)
		{
			field_35 = 3;
			field_36++;
			field_34 = 2;
			SFX_Play(0x29, 0x3FFF, 0);
		}

		i32 missionIndex = (reinterpret_cast<u8*>(field_3C) - reinterpret_cast<u8*>(gChallenges)) >> 4;
		SScore* pRow = &gGlobalRecords.mScores[missionIndex * NUM_RECORDS_PER_CHALL];

		for (i32 row = 1; row <= field_36; row++)
		{
			if (row == field_36 && field_34)
				Mess_SetRGB(0xFF, 0xFF, 0xFF, 0);
			else
				Mess_SetRGB(0x60, 0x60, 0x60, 0);

			char rowStr[0xC];
			rowStr[0] = static_cast<char>(row + 0x30);
			rowStr[1] = 0;
			Mess_DrawText(field_1C + 0x27, y, rowStr, 0, 0x1000u);

			if (pRow->field_0)
			{
				char letterBuf[0xC];

				letterBuf[0] = pRow->field_0;
				letterBuf[1] = 0;
				Mess_DrawText(field_1C + 0x76, y, letterBuf, 0, 0x1000u);

				letterBuf[0] = pRow->field_1;
				letterBuf[1] = 0;
				Mess_DrawText(field_1C + 0x8A, y, letterBuf, 0, 0x1000u);

				letterBuf[0] = pRow->field_2;
				letterBuf[1] = 0;
				Mess_DrawText(field_1C + 0x9E, y, letterBuf, 0, 0x1000u);

				DisplayScore(field_1C + 0xEF, y, (pRow->field_4 << 8) + pRow->field_3, field_3C->mScoreUnits);
			}
			else
			{
				Mess_DrawText(field_1C + 0x8B, y, gRecordDashesLabel, 0, 0x1000u);
				Mess_DrawText(field_1C + 0xEF, y, gRecordDashesLabel, 0, 0x1000u);
			}

			y += 0xE;
			pRow++;
		}
	}

	if (field_40 && (*reinterpret_cast<volatile u8*>(0x6B4CA0) & 0x10))
	{
		char cursorStr[0xC];
		cursorStr[0] = '_';
		cursorStr[1] = 0;

		Mess_DrawText(field_1C + mLetterIndex * 20 + 0x76, field_20 + field_39 * 14 + 0x24, cursorStr, 0, 0x1000u);
	}

	// original calls the CExpandingBox widget-frame draw out of line
	// (0x47AF10, CExpandingBox_Display in tools/names.json). CRecordBox's
	// own fields (field_4..field_2C) are laid out exactly like
	// CExpandingBox's (see the CRecordBox ctor comment), so a plain
	// pointer cast reaches the same object; this is a real call, not
	// inlined (CExpandingBox::Display lives in pshell.cpp, a different
	// TU).
	reinterpret_cast<CExpandingBox*>(this)->Display();
}

// @Ok
// @Matching
void CRecordBox::Update(void)
{
	print_if_false(mLetterIndex < 3, "Bad mLetterIndex");

	if (!field_40)
		return;

	if (!field_30)
		return;

	i32 missionIndex = (reinterpret_cast<u8*>(field_3C) - reinterpret_cast<u8*>(gChallenges)) >> 4;
	SScore* pScores = &gGlobalRecords.mScores[missionIndex * NUM_RECORDS_PER_CHALL];

	for (i32 key = 0; key < 0x100; key++)
	{
		if (!PCINPUT_IsKeyPressed(static_cast<u8>(key), 1))
			continue;

		char keyName[0x20];
		DXINPUT_GetKeyName(static_cast<u8>(key), keyName);

		if (strlen(keyName) != 1)
			continue;

		if (keyName[0] < 'A' || keyName[0] > 'Z')
			continue;

		if (static_cast<u8>(mLetterIndex) > 2)
			continue;

		reinterpret_cast<u8*>(&pScores[field_39])[mLetterIndex] = keyName[0];

		mLetterIndex++;

		SFX_Play(0x29, 0x2000, 0);

		if (static_cast<u8>(mLetterIndex) > 2)
			mLetterIndex = 0;
	}

	if (PCSHELL_CheckTriggers(0x50010, 1, 1))
	{
		reinterpret_cast<u8*>(&gGlobalRecords)[0] = reinterpret_cast<u8*>(&pScores[field_39])[0];
		reinterpret_cast<u8*>(&gGlobalRecords)[1] = reinterpret_cast<u8*>(&pScores[field_39])[1];
		reinterpret_cast<u8*>(&gGlobalRecords)[2] = reinterpret_cast<u8*>(&pScores[field_39])[2];

		field_40 = 0;

		SFX_Play(0x1F, 0x2000, 0);
	}

	if (PCSHELL_CheckTriggers(0x4004, 1, 1))
	{
		G_SCONTROL[0].Left.Triggered = 0;

		if (mLetterIndex != 0)
		{
			mLetterIndex--;
			SFX_Play(0x29, 0x2000, 0);
		}
	}

	if (PCSHELL_CheckTriggers(0x8008, 1, 1))
	{
		G_SCONTROL[0].Right.Triggered = 0;

		if (static_cast<u8>(mLetterIndex) < 2)
		{
			mLetterIndex++;
			SFX_Play(0x29, 0x2000, 0);
		}
	}
}

// 2026-08-31 re-re-attempt, confirmed dead on PC (not just "unlocated").
// Re-checked the byte range with idalib on the real exe: CRecordBox::Display
// is 0x47B240..0x47B550 (784 bytes), CRecordBox::Update is 0x47B560..0x47B712
// (434 bytes, confirmed by xrefs_to on the "Bad mLetterIndex" string), then
// 14 bytes of 0-alignment padding, then PShell_EndTrainingInit starts cold
// at 0x47B720 (confirmed by xrefs_to on the "Bad row sent to NameEntryOn()"
// string at 0x551AA4: both its xrefs resolve inside PShell_EndTrainingInit,
// size 0x32C, exact match against tools/functions/4699936.bin -- so that
// string is just reused debug text inside PShell_EndTrainingInit, not
// evidence of a NameEntryOn function). There is no gap anywhere in this
// cluster big enough for an 80-byte function (Mac size, prototypes.json).
// Also re-confirmed NameEntryOn has no call site anywhere on PC (not from
// CRecordBox::CRecordBox, not from Shell_ShowRecord, not from
// PShell_EndTrainingUpdate -- the only three places a CRecordBox is used).
// It is not virtual (CRecordBox's vtable has one real slot, the scalar
// deleting destructor), so nothing forces it to exist for the vtable
// either. Most likely explanation: Release builds use per-function COMDATs
// (/Gy) plus linker dead-code stripping (/OPT:REF), and a non-virtual,
// never-called member function like this one gets stripped out of the
// final binary entirely -- there is no PC address to decompile because the
// function was never linked in, not because it is merely hard to find.
// Left as an honest stub rather than guessing an implementation from the
// Mac-only prototype and the sibling Update/Display field usage.
// @Bogus
void CRecordBox::NameEntryOn(u8)
{
	printf("CRecordBox::NameEntryOn(u8)");
}
#ifdef _MSC_VER
#pragma auto_inline(on)
#endif

// tentative name, no idb_globals.txt match near 0x6A7ADC. Holds the CRecordBox
// widget for the current Shell_ShowRecord call. Not a stack local: the
// original reads/writes it via a fixed address across the whole per-frame
// loop, so it has to be a real global (or MSVC would have kept it in a
// register/stack slot).
static CRecordBox* gShowRecordBox;

// tentative default title text, no idb_globals.txt match near 0x54BBA0.
static char* gShowRecordTitle = "High Scores";

// @Ok
// @Matching
// SEH mystery SOLVED 2026-08-27 with IDA (Hex-Rays on the real exe): the
// missing frame was never about ctor complexity (see the 7 failed
// hypotheses this comment used to list, kept in shell.attempts.md).
// CRecordBox::CRecordBox is a trivial straight-line ctor (no calls at all,
// confirmed by decompiling 0x47B1E0), yet the real exe still wraps this
// `new CRecordBox(...)` in the unwind-state frame. The actual trigger:
// whether the constructor's DEFINITION is visible to the compiler in the
// same translation unit as the `new` call. Same-TU visibility (even with
// `#pragma auto_inline(off)`, which only blocks literal inlining, not this
// separate throw analysis) lets MSVC6 prove the ctor can't throw and drop
// the protection; a declaration-only (different-TU) ctor is opaque, so
// MSVC6 always protects it. Reordering the ctor's position WITHIN one TU
// (one of the 7 failed attempts) does not hide it either, since the whole
// TU is visible regardless of declaration order. Fix: moved
// CRecordBox::CRecordBox/~CRecordBox out of shell.cpp into pshell.cpp (a
// different TU), matching the class-precedent CMenu already had by
// accident (CMenu::CMenu lives in front.cpp, never in the same TU as any
// of its callers, which is why Shell_ChooseSurvivalArena's `new CMenu(...)`
// already reproduced the frame). cmpsum: 0 mnemonic diffs.
void Shell_ShowRecord(char const *, char const *, STrainingMission* pMission)
{
	// same pattern as Shell_ScreenAdjust/Shell_TitleScreen above: defined once
	// in PCShell.cpp, called here through a local extern.
	extern void gsub_430880(void);
	extern void gsub_430680(void);

	print_if_false(gShellInitialized != 0, "Called Shell_ShowRecord() without shell initialised");

	Pause(1);

	// DrawSync(), written out by hand instead of calling the ps2funcs.h
	// INLINE version: that one calls export.h's static stubbed_printf, which
	// our compiler inlines away (no varargs to block it, unlike
	// print_if_false), so it would never emit the original's real
	// "call 0046CB90h". gsub_46CB90 (panel.cpp) is the actual out-of-line
	// implementation at that address, already @Ok/@Matching there.
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	gsub_430680();

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");

	gShowRecordBox = new CRecordBox(0x75, 0x50, pMission);

	gShellMenuEase = 0x200;

	while (1)
	{
		gsub_430880();
		Db_FlipClear();
		CalcPolyBufferEnd();

		i32 vblanksSnapshot = G_VBLANKS;

		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1u, -1);

		Shell_DrawBackground();

		Shell_DrawTitleBar(0x80, 0x26, gShowRecordTitle, 1, 0, 0x96, -21, 0x1D);

		Mess_SetRGB(0x60u, 0x60u, 0x60u, 0);
		Mess_SetTextJustify(0);
		Mess_DrawText(0x100, 0x41, pMission->field_0, 0, 0x1000u);

		gShowRecordBox->Display();

		PCSHELL_DrawMouseCursor();

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		gShellMenuEase = PShell_MoveTowards(gShellMenuEase, 0x180);

		Pad_Update();

		if (*gShellMenuAbort)
			return;

		CheckForPadUnplugged();

		if (PCSHELL_CheckTriggers(0x20220, 1, 1))
			break;

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

		PCSHELL_Relax();
	}

	G_SCONTROL[0].Circle.Triggered = 0;
	SFX_Play(0x23, 0x2000, 0);

	delete gShowRecordBox;

	Pause(1);

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	gsub_430680();

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");

	Pad_ClearTriggers(G_SCONTROL);

	gShellMenuEase = 0x200;
}

// @Ok
i32 Shell_Special(EShellResult a1)
{
	print_if_false(gShellInitialized != 0, "Called Shell_Special() without shell initialised");
	i32 v10 = 0;
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(256, 0, 0, 256, 256, 16);
	pMenu->AddEntry("costumes");
	pMenu->AddEntry("view credits");
	pMenu->AddEntry("cheats");
	i32 v4 = *gShowAllLevels;
	for (i32 i = 0; i < 34; i++)
	{
		if (G_SAVE_GAME.field_56[i] != 0)
		{
			v4 = 1;
			break;
		}
	}
	if (v4 != 0)
		pMenu->AddEntry("level select");
	pMenu->CentreY();
	pMenu->Zoom(0);
	switch (a1)
	{
	case 22:
		pMenu->SetLine(0);
		break;
	case 23:
		pMenu->SetLine(2);
		break;
	case 24:
		pMenu->SetLine(1);
		break;
	case 25:
		print_if_false(v4 != 0, "Bugger");
		pMenu->SetLine(3);
		break;
	default:
		print_if_false(0, "Bad default sent to Shell_Special");
		break;
	}

	i32 v1 = 0;
	*(i32*)0x005512EC = 0;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 v13 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(v1, 38, "special", 1, 0, 150, -21, 29);
		pMenu->Display();
		if (pMenu->FinishedZooming())
		{
			PShell_InstructionalText();
			switch (pMenu->mLine)
			{
			case 0:
				Mess_DrawText(256, 180, "change in-game spider-man outfit", 0, 0x1000);
				Mess_DrawText(256, 193, "and gain special abilities", 0, 0x1000);
				break;
			case 1:
				Mess_DrawText(256, 180, "view credits of those who", 0, 0x1000);
				Mess_DrawText(256, 193, "worked on this game", 0, 0x1000);
				break;
			case 2:
				Mess_DrawText(256, 180, "enter cheat codes", 0, 0x1000);
				break;
			case 3:
				Mess_DrawText(256, 180, "select which level to play", 0, 0x1000);
				break;
			default:
				break;
			}
		}
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		v1 = PShell_MoveTowards(v1, 128);
		*(i32*)0x005512EC = PShell_MoveTowards(*(i32*)0x005512EC, 384);
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return 0;
		CheckForPadUnplugged();
		pMenu->Update();
		if (PCSHELL_CheckTriggers(131616, 1, 1))
		{
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(0x23, 0x2000, 0);
			goto done;
		}
		i32 IsMouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			u8 mJustification = pMenu->mJustification;
			const char* name = pMenu->mEntry[pMenu->mLine].name;
			i32 x, y;
			pMenu->GetEntryXY(name, &x, &y);
			IsMouseOverText = PCSHELL_IsMouseOverText(name, x, y, mJustification);
		}
		if (pMenu->mLine < 0x28 && (IsMouseOverText != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
			break;
		if (G_VBLANKS == v13)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
	}

	G_SCONTROL[0].Start.Triggered = 0;
	G_SCONTROL[0].X.Triggered = 0;
	SFX_Play(0x1F, 0x2000, 0);
	switch (pMenu->mLine)
	{
	case 0:
		v10 = 22;
		break;
	case 1:
		v10 = 24;
		break;
	case 2:
		v10 = 23;
		break;
	case 3:
		v10 = 25;
		break;
	default:
		break;
	}

done:
	delete pMenu;
	Pause(1);
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	Pad_ClearTriggers(G_SCONTROL);
	return v10;
}

// @Ok
void Shell_StoryBoards(void)
{
	print_if_false(gShellInitialized != 0, "Called Shell_MainMenu() without shell initialised");
	PShell_NormalFont();

	CMenu* pMenu = new CMenu(188, 73, 1, 256, 256, 13);
	pMenu->AdjustWidth(10);
	for (i32 i = 0; i < 21; i++)
	{
		if (gMovieTable[i].mUnlockId != -1)
			pMenu->AddEntry(gMovieTable[i].pDisplayName);
	}
	pMenu->scrollbar_one = 1;
	pMenu->field_1B = 9;
	pMenu->scrollbar_zero = 0;
	pMenu->CentreX();
	pMenu->CentreY();
	if (pMenu->mNumLines > 9)
		pMenu->Zoom(2);
	else
		pMenu->Zoom(1);

	i32 v5 = 0;
	*(i32*)0x005512EC = 384;

	while (1)
	{
		Db_FlipClear();
		CalcPolyBufferEnd();
		i32 v16 = G_VBLANKS;
		if (G_SCENE_RELATED == 0)
			PCGfx_BeginScene(1, -1);
		if (gBackgroundAnimFrame == 0)
			Spool_AnimAccess("menubg", &gBackgroundAnimFrame);
		PCPanel_DrawTexturedPoly(-1.0f, gBackgroundAnimFrame->pTexture, 0, 0, 512, 240, 128);
		Shell_DrawTitleBar(v5, 38, "storyboards", 1, 0, 150, -21, 29);
		pMenu->Display();
		PCSHELL_DrawMouseCursor();
		if (G_SCENE_RELATED != 0)
			PCGfx_EndScene(1);
		i32 v17 = PShell_MoveTowards(v5, 128);
		if (pMenu->mLine > 0x28)
			Pad_ClearTriggers(G_SCONTROL);
		Pad_Update();
		if (*gShellMenuAbort != 0)
			return;
		CheckForPadUnplugged();
		pMenu->Update();
		if (PCSHELL_CheckTriggers(131616, 1, 1))
		{
			G_SCONTROL[0].Circle.Triggered = 0;
			SFX_Play(0x23, 0x2000, 0);
			delete pMenu;
			Pause(1);
			if (!gPrintStubbed)
				gsub_46CB90((void*)"stubbed out: DrawSync");
			Pad_ClearTriggers(G_SCONTROL);
			return;
		}
		i32 IsMouseOverText = 0;
		if (PCSHELL_CheckTriggers(256, 1, 1))
		{
			u8 mJustification = pMenu->mJustification;
			const char* name = pMenu->mEntry[pMenu->mLine].name;
			i32 x, y;
			pMenu->GetEntryXY(name, &x, &y);
			IsMouseOverText = PCSHELL_IsMouseOverText(name, x, y, mJustification);
		}
		if (pMenu->mLine < 0x28 && (IsMouseOverText != 0 || PCSHELL_CheckTriggers(65552, 1, 1)))
		{
			G_SCONTROL[0].Start.Triggered = 0;
			G_SCONTROL[0].X.Triggered = 0;
			SFX_Play(0x1F, 0x2000, 0);
			const SMovieEntry* pSel = 0;
			const char* selName = pMenu->mEntry[pMenu->mLine].name;
			for (i32 i = 0; i < 21; i++)
			{
				if (Utils_CompareStrings(selName, gMovieTable[i].pDisplayName) == 0)
					pSel = &gMovieTable[i];
			}
			print_if_false(pSel != 0, "Storyboard not found");
			if (pSel != 0 && pSel->field_C != 0)
			{
				char bmpName[13];
				strcpy(bmpName, "l*s*_*.bmp");
				bmpName[1] = pSel->pFileName[1];
				bmpName[3] = pSel->pFileName[3];
				i32 page = 1;
				bmpName[5] = page + 48;
				BMP_Draw(bmpName);
				while (1)
				{
					Pad_Update();
					if (PCSHELL_CheckTriggers(16388, 1, 1))
					{
						--page;
						G_SCONTROL[0].Left.Triggered = 0;
						if (page < 1)
							page = pSel->field_C;
						bmpName[5] = page + 48;
						BMP_Draw(bmpName);
					}
					else if (PCSHELL_CheckTriggers(32776, 1, 1))
					{
						++page;
						G_SCONTROL[0].Right.Triggered = 0;
						if (page > pSel->field_C)
							page = 1;
						bmpName[5] = page + 48;
						BMP_Draw(bmpName);
					}
					else if (PCSHELL_CheckTriggers(131104, 1, 1))
						break;
				}
				SFX_Play(0x23, 0x2000, 0);
				G_SCONTROL[0].Circle.Triggered = 0;
				G_SCONTROL[0].X.Triggered = 0;
				G_SCONTROL[0].Start.Triggered = 0;
			}
		}
		if (G_VBLANKS == v16)
			Pause(1);
		DoVblankProcessing = 0;
		Pause(1);
		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: DrawSync");
		if (DoVblankProcessing == 0)
		{
			Utils_VblankProcessing();
			DoVblankProcessing = 1;
		}
		PCSHELL_Relax();
		v5 = v17;
	}
}

// @Ok
// @Matching
void Shell_TitleScreen(void)
{
	Front_ClearScreen();
	DrawSync();
	Pad_ClearTriggers(G_SCONTROL);
	Pad_Update();
	Pad_ClearTriggers(G_SCONTROL);

	Sprite2* v0 = new Sprite2("title.bmp", 1, 0, 0, 3);

	// same address as gsub_430880 (nullsub_3), declared and defined in
	// PCShell.cpp; cast to accept the (unused) dummy arg this call site passes.
	extern void gsub_430880(void);
	((void(*)(i32))gsub_430880)(3);

	Redbook_XAPlay(0x43, 0xD, 0);

	while (1)
	{
		if (!G_SCENE_RELATED)
			PCGfx_BeginScene(1u, -1);

		v0->screenHeight();

		v0->draw(0, 0, 8, -1.0f);

		Front_MiniUpdate();

		if (G_SCENE_RELATED)
			PCGfx_EndScene(1);

		++TTime;
		Pad_Update();

		if (PCSHELL_CheckTriggers(0x40010, 1, 1))
			break;

		gsub_430880();
		PCSHELL_Relax();
	}

	G_SCONTROL[0].Start.Triggered = 0;
	delete v0;

	Redbook_XAStop();
	Mess_DeleteAll();

	Utils_InitialRand(G_VBLANKS);

	for (i32 i = 10000; i > 0; i--)
		Rnd(10);

	// tentative: 9 i32 game-address array, no name in idb_globals.txt (nearest
	// neighbours are gTrainingSeconds 0x551288 and gCheats 0x5513E0)
	static i32 * const gTitleScreenShuffleTable = (i32*)0x5512A0;
	Utils_Jumble(gTitleScreenShuffleTable, 9);

	Front_ClearScreen();
	DrawSync();
	Pad_ClearTriggers(G_SCONTROL);
}

// @Ok
INLINE void Shell_VerySmallFont(void)
{
	Mess_SetScale(256);
	Mess_SetCurrentFont("sp_fnt03.fnt");
}


// @Ok
// Read straight out of the original at 0x552AB8 (named SpideyIcons in the maintainer's
// IDB). Element stride 0x28 confirmed from Spidey_CIcon::SetIcon's index scaling.
// Entry 3 has IconModel -1, so SetIcon bails before touching its Name; the original
// Name pointer there is 0x56EB54, a zeroed slot at the tail of .data, i.e. an empty
// string, so "" is used here.
// The original table stops at 0x552BE8: the last entry only has its first 0x18 bytes,
// the bytes after that are a different global (a 0x38-stride table of camera/light
// setups, referenced from 0x490FC5 and 0x493EF0). Nothing reads SpideyIcons past
// offset 0x14, so the last entry's tail is written as zero here.
#ifndef SPIDEY_STANDALONE
EXPORT SpideyIconRelated SpideyIcons[8] =
{
	{ "items",  5, 0, 0, 0, 0,     0,     0, 0x056, 0x062, "new game",    1 },
	{ "items",  5, 0, 0, 0, 0,  0x14, 0x200, 0x056, 0x099, "options",     4 },
	{ "icons",  1, 0, 0, 0, 0,     0, 0x2BC, 0x078, 0x0CA, "quit",     0x12 },
	{ "",      -1, 0, 0, 0, 0,     0,     0, 0x190, 0x02E, "training",    5 },
	{ "items",  5, 0, 0, 0, 0,   -30, 0x200, 0x1AE, 0x062, "High Scores", 6 },
	{ "icons",  1, 0, 0, 0, 0,     0, 0x2BC, 0x1AE, 0x099, "special",  0x0D },
	{ "icons",  2, 0, 0, 0, 0,    -4, 0x1EA, 0x190, 0x0CA, "gallery",     7 },
	{ "icons",  0, 0, 0, 0x100, 0, -16, 0x2BC,   0,     0, 0,             0 },
};
#else
extern SpideyIconRelated SpideyIcons[8];
#endif


const i32 NUM_LEVELS = 34;
EXPORT u8 LevelIndexes[NUM_LEVELS];

// @Ok
// @Test
i32 CalcIndexOfContinueLevel(void)
{
	i32 bar = 1000000;
	for (i32 i = 0; i < NUM_LEVELS; i++)
	{
		if (LevelIndexes[i] < bar)
			bar = LevelIndexes[i];
	}

	i32 index;
	for (index = 0; index < NUM_LEVELS; index++)
	{
		if (LevelIndexes[index] == bar)
			break;
	}

	return index;
}

// @Ok
void Spidey_CIcon::SetIcon(i32 option)
{
	print_if_false(option >= 0 && (u32)option < 8, "Bad option");

	if (SpideyIcons[option].IconModel < 0)
	{
		this->mFlags |= 1;
		return;
	}

	this->InitItem(SpideyIcons[option].Name);

	i32 iconModel = SpideyIcons[option].IconModel;
	if (iconModel < 0)
	{
		this->mFlags |= 1;
	}
	else
	{
		print_if_false(iconModel < reinterpret_cast<i32>(PSXRegion[this->mRegion].ppModels[-1]),
				"Bad icon model");

		this->mFlags &= 0xFFFE;
		this->mModel = iconModel;
	}


	if (PSXRegion[this->mRegion].Filename[9])
	{
		this->mFlags |= 0x482;
		this->mpLight = &M3d_SpideyCIconLight;
		this->RunAnim(0, 0, -1);
	}
	else
	{
		this->mFlags &= 0xFB7D;
		this->mpLight = 0;
	}

	this->mPos.vy = SpideyIcons[option].field_10 << 12;
	this->mPos.vz = SpideyIcons[option].field_14 << 12;

	this->mAngles.vx = SpideyIcons[option].field_8;
	this->mAngles.vz = SpideyIcons[option].field_C;
}

// @Ok
Spidey_CIcon::Spidey_CIcon(i32 icon)
{
	this->SetIcon(icon);
}

// @Ok
// @Test
Spidey_CIcon::Spidey_CIcon(i32 a2, i32 a3, i32 a4)
{
	this->mPos.vx = a2 << 12;
	this->mPos.vy = a3 << 12;
	this->mPos.vz = a4 << 12;

	this->InitItem("items");

	this->mModel = 5;
	this->mFlags &= 0xFB7D;

	this->mpLight = 0;
	this->mFlags |= 0x200;
	this->mScale.vx = 2048;
	this->mScale.vy = 2048;
	this->mScale.vz = 2048;
}

// @Ok
// See the long writeup on CShellPreviewIcon in shell.h for how this class and its three
// members were reverse engineered. Constructor body confirmed identical (field for field,
// value for value) across all seven call sites checked: Shell_ComicCollection's one instance
// (0x49B270, mPos = (0,0,2048000)<<-shifted-already/12) and Shell_GameCovers's six instances
// (0x49C220, mPos varies per grid cell, mPos.vz always 2048000).
CShellPreviewIcon::CShellPreviewIcon(i32 x, i32 y, i32 z)
{
	this->mPos.vx = x << 12;
	this->mPos.vy = y << 12;
	this->mPos.vz = z << 12;

	this->InitItem("items");

	this->mModel = 5;
	this->mFlags &= 0xFB7D;

	this->mpLight = 0;

	this->mFlags |= 0x200;
	this->mScale.vx = 2048;
	this->mScale.vy = 2048;
	this->mScale.vz = 2048;
}

// @Ok
// 0x493830 (thunk) / 0x493850 (real body): no added fields, so nothing to clean up beyond
// what the compiler already does for us (reset vfptr, chain to CSuper::~CSuper). Empty body.
CShellPreviewIcon::~CShellPreviewIcon(void)
{
}

// @Ok
// 0x493970. mAngles.vy += 50 is an unconditional constant spin; the mFlags&2 branch
// (UpdateFrame + M3d_BuildTransform) is dead at every known call site (the constructor above
// always clears bit 2), but is reproduced for fidelity in case some other call site sets it.
void CShellPreviewIcon::AI(void)
{
	this->mAngles.vy += 50;

	if (this->mFlags & 2)
	{
		this->UpdateFrame();
		M3d_BuildTransform(this);
	}
}


// @Ok
void CShellMysterioHeadCircle::Move(void)
{
	CDummy *pDummy = static_cast<CDummy*>(Mem_RecoverPointer(&this->field_84));

	if (!pDummy)
	{
		this->Die();
		return;
	}

	M3d_BuildTransform(reinterpret_cast<CSuper*>(pDummy));

	this->field_8C += this->field_90;

	i32 idx = this->field_8C & 0xFFF;
	i16 *tbl = word_610C48 + 2 * idx;
	i32 sinV = tbl[0];
	i32 cosV = tbl[1];

	i32 tilt = (6500 * *gShellCircleTiltB) >> 13;

	SHook hook0;
	hook0.Offset = 1;
	hook0.Part.vx = (3250 * sinV + cosV * -tilt) >> 12;
	hook0.Part.vy = -9700;
	hook0.Part.vz = ((3250 * cosV - sinV * -tilt) >> 12) - 2500;

	SHook hook1;
	hook1.Offset = 1;
	hook1.Part.vx = (cosV * tilt + 3250 * sinV) >> 12;
	hook1.Part.vy = ((6500 * *gShellCircleTiltA) >> 12) - 9700;
	hook1.Part.vz = ((3250 * cosV - sinV * tilt) >> 12) - 2500;

	SHook hook2;
	hook2.Offset = 1;
	hook2.Part.vx = (-3250 * sinV + cosV * -tilt) >> 12;
	hook2.Part.vy = -9700;
	hook2.Part.vz = ((-3250 * cosV - sinV * -tilt) >> 12) - 2500;

	SHook hook3;
	hook3.Offset = 1;
	hook3.Part.vx = (cosV * tilt - 3250 * sinV) >> 12;
	hook3.Part.vy = hook1.Part.vy;
	hook3.Part.vz = ((-3250 * cosV - sinV * tilt) >> 12) - 2500;

	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&this->mPos), reinterpret_cast<CSuper*>(pDummy), &hook0);
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&this->mPosB), reinterpret_cast<CSuper*>(pDummy), &hook1);
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&this->mPosC), reinterpret_cast<CSuper*>(pDummy), &hook2);
	M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&this->mPosD), reinterpret_cast<CSuper*>(pDummy), &hook3);
}

// @Ok
CShellMysterioHeadCircle::~CShellMysterioHeadCircle(void)
{
	--gShellMysterioRelated;
}

// @Ok
CShellMysterioHeadCircle::CShellMysterioHeadCircle(CDummy *pDummy)
{
	this->field_84 = Mem_MakeHandle(reinterpret_cast<void*>(pDummy));

	this->SetTexture(0xB968C0FD);
	this->SetSemiTransparent();

	this->field_90 = Rnd(100) + 100 * gShellMysterioRelated + 50;

	if (gShellMysterioRelated & 1)
		this->field_90 *= -1;

	++gShellMysterioRelated;
}

// @Ok
// Functional-only bar (session override): logic verified correct against the
// disassembly (sin/cos table lookups, both hook offsets, the field_110 Rnd
// reset polarity, the CSuper* reuse for M3d_BuildTransform/GetDynamicHookPosition).
// Previous session left this @NotOk chasing a pure register-allocation residue
// (63 mnemonic diffs, all downstream of one masked-index-read-twice pattern);
// that is a byte-match concern only, not a functional one. See git history for
// the register-allocation attempt log if resuming byte-match work later.
void CShellGoldFish::AI(void)
{
	CDummy *pDummy = static_cast<CDummy*>(Mem_RecoverPointer(&this->field_F8));

	if (!pDummy)
	{
		this->Die();
		return;
	}

	M3d_BuildTransform(reinterpret_cast<CSuper*>(pDummy));

	if (this->field_10C)
		this->field_10C--;

	if (!this->field_10C)
	{
		this->field_10C = Rnd(0x190) + 0x14;
		this->mAngVel.vy = -this->mAngVel.vy;
	}

	if (this->field_110)
		this->field_110--;

	if (!this->field_110)
	{
		this->field_110 = 0x14;

		if (this->mAngVel.vy < 0)
			this->mAngVel.vy = -0x28 - Rnd(0x5A);
		else
			this->mAngVel.vy = Rnd(0x5A) + 0x28;
	}

	if (this->field_100)
		this->mAngVel.vy <<= 1;

	this->field_114 += this->mAngVel.vy;
	i16 *tblHeading = word_610C48 + 2 * (this->field_114 & 0xFFF);
	i32 sinH = tblHeading[0];
	i32 cosH = tblHeading[1];

	i32 phase108 = this->field_108;
	i32 idx108 = phase108 & 0xFFF;
	i32 magVal = word_610C48[2 * idx108];
	this->field_108 = phase108 + 0xA;

	i32 phase104 = this->field_104;
	i32 idx104 = phase104 & 0xFFF;
	i32 bobVal = word_610C48[2 * idx104];
	this->field_104 = phase104 + 0x50;

	SHook hook;
	hook.Offset = 1;

	i32 mag = (magVal * 500) / 4096 + 0x8FC;
	hook.Part.vx = (mag * sinH) >> 12;
	hook.Part.vz = (mag * cosH) >> 12;
	hook.Part.vz += 0xF63C;

	hook.Offset = bobVal * 600 / 4096 - 0x5DC;
	hook.Offset += 0xDA1C;

	M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&this->mPos),
			reinterpret_cast<CSuper*>(pDummy),
			&hook);

	if (this->mAngVel.vy > 0)
		this->mAngles.vy = this->field_114;
	else
		this->mAngles.vy = this->field_114 + 0x800;
}

// @Ok
CShellGoldFish::~CShellGoldFish(void)
{
	this->DeleteFrom(&MiscList);
}

// @Ok
CShellGoldFish::CShellGoldFish(CDummy *pDummy)
{
	this->field_F8 = Mem_MakeHandle(reinterpret_cast<void*>(pDummy));

	this->InitItem("goldfish");
	this->mType = 506;
	this->AttachTo(&MiscList);

	this->mFlags |= 0x200;
	this->mAngVel.vy = 50;

	this->mScale.vz = 10000;
	this->mScale.vy = 10000;
	this->mScale.vx = 10000;
}

// @Ok
// Functional decompile (session-wide bar 2026-08-30: correctness, not byte
// match). Verified against IDA decompile of 0x4908E0 (517 bytes). CDummy
// field offsets confirmed against VALIDATE entries: mExtraFlags 0x12C
// (CSuper), mFlags 0x4/mPos 0x8/mRGB 0x24/mRegion 0x1F/mType 0x38 (CItem).
// offset 0x156 has no declared field yet (it sits in the PADDING(2) gap
// between CSuper's field_154 and field_158 in ob.h), so it is written with
// a raw offset cast rather than editing the shared ob.h struct (same
// approach Spidey_SwapSuitTextures in spidey.cpp used for undeclared
// fields). field_44 is the DCMem_New'd CVector array of computed hook
// positions, one per mesh piece for the dummy's region (count from
// word_6B2478[34*region], same table Spidey_SwapSuitTextures uses). Each
// position is found via a random link picked from CItemRelatedList
// (ob.h, 0x6B2454) for that mesh piece, packed into an SHook (m3dutils.h)
// and resolved with M3dUtils_GetDynamicHookPosition (still a printf stub
// in m3dutils.cpp, out of scope for this file; called cross-TU the same
// way blackcat.cpp/carnage.cpp/mysterio.cpp/effects.cpp already do while
// tagged @Ok).
CShellSimbyFireDeath::CShellSimbyFireDeath(CDummy *pDummy)
{
	print_if_false(pDummy != 0, "NULL pSimby?");
	print_if_false(pDummy->mType == 324, "Non symbiote sent to CShellSimbyFireDeath");

	this->field_3C = Mem_MakeHandle(reinterpret_cast<void*>(pDummy));

	pDummy->mExtraFlags |= 8;
	pDummy->mFlags |= 0x408;

	*reinterpret_cast<i16*>(reinterpret_cast<u8*>(pDummy) + 0x156) =
		static_cast<i16>((pDummy->mPos.vy >> 12) + 100);

	pDummy->mRGB = 0x202020;

	i32 count = word_6B2478[34 * pDummy->mRegion];

	this->field_44 = reinterpret_cast<CVector*>(DCMem_New(12 * count, 0, 1, 0, 1));

	i32 **pRegionTable = CItemRelatedList[pDummy->mRegion * 17];

	for (i32 i = 0; i < count; i++)
	{
		u8 *pEntry = reinterpret_cast<u8*>(pRegionTable[i]);
		i32 idx = Rnd(*reinterpret_cast<u16*>(pEntry + 2));

		SHook hook;
		hook.Part = *reinterpret_cast<CSVector*>(pEntry + 8 * idx + 28);
		hook.Offset = static_cast<i16>(i);

		M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&this->field_44[i]),
			reinterpret_cast<CSuper*>(pDummy),
			&hook);
	}
}

// @Ok
// @Test
void CShellSimbyMeltSplat::Move(void)
{
	switch (this->field_84)
	{
		case 0:
			this->field_88 += 10;
			if (this->field_88 >= this->field_8C)
				this->field_84 = 1;

			break;
		case 1:
			Bit_ReduceRGB(&this->mTint, 10);
			if (!(0xFFFFFF & this->mTint))
					this->Die();
			break;
	}

	CVector a3 = (this->field_88 * this->field_9C);
	CVector v11 = (this->field_88 * this->field_A8);

	this->mPos = (this->field_90 - a3) - v11;

	this->mPosB = (this->field_90 + a3) - v11;

	this->mPosC = (this->field_90 - a3) + v11;

	this->mPosD = (this->field_90 + a3) + v11;
}

// @Ok
CShellSimbyMeltSplat::CShellSimbyMeltSplat(CVector* pVec)
{
	this->field_90.vx = 0;
	this->field_90.vy = 0;
	this->field_90.vz = 0;

	this->field_9C.vx = 0;
	this->field_9C.vy = 0;
	this->field_9C.vz = 0;

	this->field_A8.vx = 0;
	this->field_A8.vy = 0;
	this->field_A8.vz = 0;

	this->SetTexture(0x3AF6DFF);
	this->SetSemiTransparent();
	this->SetTint(0xFF, 0, 0);

	this->field_8C = Rnd(50) + 70;
	this->field_90 = *pVec;

	SVECTOR v11;
	v11.vx = 0;
	v11.vy = -4096;
	v11.vz = 0;

	this->OrientUsing(&this->field_90, &v11, 1, 1, Rnd(4096));

	this->field_9C = (this->mPosB - this->mPos) >> 1;
	this->field_A8 = (this->mPosC - this->mPos) >> 1;

	this->Move();
	this->mType = 21;
}

// @Ok
// Functional-only bar (session override): logic verified correct (spiral
// position update, fade-out of R/G/B intensities, flicker-scaled color
// repack). Previous session left this @NotOk chasing pure register-allocation
// residue (load order of field_80 vs mVel.vy/mPos.vy, and the mCodeBGR byte
// repack instruction shape); that is a byte-match concern only, not a
// functional one. See git history for the attempt log if resuming byte-match
// work later.
void CShellEmber::Move(void)
{
	this->mPos.vy -= this->mVel.vy;
	i32 idx80 = this->field_80 & 0xFFF;
	this->field_80 += 100;
	i32 amp = (this->field_78 * word_610C48[2 * idx80]) >> 12;

	i32 phase7c = this->field_7C;
	i32 idx7c = phase7c & 0xFFF;
	this->mPos.vx = amp * word_610C48[2 * idx7c] + this->field_68;
	this->mPos.vz = amp * word_610C48[2 * idx7c + 1] + this->field_70;
	this->field_7C = phase7c + 100;

	if (this->field_74)
	{
		this->field_74--;
	}
	else
	{
		i32 v84 = this->field_84 < 15 ? 0 : this->field_84 - 15;
		this->field_84 = v84;

		i32 v88 = this->field_88 < 15 ? 0 : this->field_88 - 15;
		this->field_88 = v88;

		i32 v8c = this->field_8C < 15 ? 0 : this->field_8C - 15;
		this->field_8C = v8c;

		if (!(v84 | v88 | v8c))
			this->Die();
	}

	i32 flicker = Rnd(0x100);
	this->mCodeBGR = (((this->field_8C * flicker) >> 8) << 16)
		| (((this->field_88 * flicker) >> 8) << 8)
		| ((this->field_84 * flicker) >> 8)
		| (this->mCodeBGR & 0xFF000000u);
}

// @Ok
// @Test
CShellEmber::CShellEmber(
		CVector* pVec,
		i32 a3)
{
	this->field_68 = 0;
	this->field_6C = 0;
	this->field_70 = 0;

	this->mPos = *pVec;
	this->field_68 = this->mPos.vx;
	this->field_70 = this->mPos.vz;

	this->field_78 = Rnd(10) + 10;
	this->field_7C = Rnd(4096);
	this->field_80 = Rnd(4096);

	this->SetTexture(0x13C0A001);
	this->mScale = Rnd(200) + 350;

	this->field_84 = 255;
	this->field_88 = 128;
	this->field_8C = 0;

	this->SetTint(0xFF, 128, 0);
	this->SetSemiTransparent();

	this->field_74 = (a3 * (Rnd(5) + 5)) > 8;
	this->mVel.vy = (a3 * (Rnd(5) + 6)) << 12 >> 8;
}

// @Ok
CShellMysterioHeadGlow::CShellMysterioHeadGlow(void)
	: CWobblyGlow(&ZeroVector, 150, 120, 90, 255, 255, 255, 0x80u, 0, 0xFFu)
{
}

// @Ok
CWobblyGlow::CWobblyGlow(
		CVector* Pos,
		i32 InnerRadius,
		i32 FringeRadius,
		i32 Amp,
		u8 r0,
		u8 g0,
		u8 b0,
		u8 r1,
		u8 g1,
		u8 b1)
	: CGlow(Pos, InnerRadius, FringeRadius, r0, g0, b0, r1, g1, b1)
{
	this->mAmplitude = Amp * InnerRadius / 256;

	this->mInnerRadius = InnerRadius;

	for (u32 i = 0; i < this->mNumSections; i++)
	{
		this->mInc[i] = Rnd(4096);
		this->mT[i] = Rnd(50) + 200;
	}
}

// @Ok
// @Test
void CShellRhinoNasalSteam::Move(void)
{
	if (this->mAnimSpeed)
	{
		i16 v3 = (this->mFrame << 8) | this->mFrameFrac;
		v3 += this->mAnimSpeed;

		this->mFrameFrac = v3;
		v3 >>= 8;

		this->mFrame = v3;

		if (this->mFrame >= this->mNumFrames)
		{
			this->mAnimSpeed = 0;
			this->mFrame = this->mNumFrames - 1;
		}

		this->mpPSXFrame = &this->mpPSXAnim[this->mFrame];
	}

	this->mPos += this->mVel;

	this->mVel.vy -= 1024;
	if (++this->mAge > 30)
	{
		this->Die();
	}
	else
	{
		this->SetTransparency(64 - 2 * (0xFF & this->mAge));
		this->SetScale(Rnd(4) + 4 *(this->mAge + 32));
	}
}

// @Ok
CShellRhinoNasalSteam::CShellRhinoNasalSteam(
		CVector* a2,
		CVector* a3)
{
	this->mPos = *a2;
	this->mVel = *a3;

	this->SetAnim(1);
	this->SetSemiTransparent();
	this->SetTransparency(0x40);
	this->SetAnimSpeed(128);
	this->SetScale(128);
	this->mAngle = Rnd(4096);
}

// @Ok
// skin goo params are not okay
void CShellSuperDocOckElectrified::Move(void)
{
	CSuper *pSuper = static_cast<CSuper*>(Mem_RecoverPointer(&this->field_3C));

	if (!pSuper)
	{
		this->Die();
		return;
	}

	M3d_BuildTransform(pSuper);

	if (++this->field_44 > 0)
	{
		new CSkinGoo(pSuper, &gSuperDocOckSkinGooSource, 19, &gSuperDocOckSkinGooParams);
		this->field_44 = 0;
	}

}

// @Ok
CShellSuperDocOckElectrified::CShellSuperDocOckElectrified(CSuper* pSuper)
{
	print_if_false(pSuper != 0, "NULL pointer");
	print_if_false(pSuper->mType == 309, "Non SuperDocOck");

	this->field_3C = Mem_MakeHandle(reinterpret_cast<void*>(pSuper));
}

// @Ok
// skin goo params are not okay
void CShellCarnageElectrified::Move(void)
{
	CSuper *pSuper = static_cast<CSuper*>(Mem_RecoverPointer(&this->field_3C));

	if (!pSuper)
	{
		this->Die();
		return;
	}

	M3d_BuildTransform(pSuper);

	if (++this->field_44 > 0)
	{
		new CSkinGoo(pSuper, &gCarnageSkinGooSourceShell, 19, &gCarnageSkinGooParams);
		this->field_44 = 0;
	}

}

// @Ok
CShellCarnageElectrified::CShellCarnageElectrified(CSuper* pSuper)
{
	print_if_false(pSuper != 0, "NULL pSuper sent to CShellCarnageElectrified");
	print_if_false(pSuper->mType == 314, "Non carnage sent to CShellCarnageElectrified");

	this->field_3C = Mem_MakeHandle(reinterpret_cast<void*>(pSuper));
}


// @Ok
// Verified against the disassembly at 0x48F350: field offsets (field_3C
// SHandle, field_44 counter), mType check, and the gVenomSkinGooSource /
// gVenomSkinGooParams args to CSkinGoo's ctor all match. Same shape as the
// already-@Ok CShellCarnageElectrified::Move and
// CShellSuperDocOckElectrified::Move siblings just above.
void CShellVenomElectrified::Move(void)
{
	CSuper *pSuper = static_cast<CSuper*>(Mem_RecoverPointer(&this->field_3C));

	if (!pSuper)
	{
		this->Die();
		return;
	}

	M3d_BuildTransform(pSuper);

	if (++this->field_44 > 0)
	{
		new CSkinGoo(pSuper, &gVenomSkinGooSource, 19, &gVenomSkinGooParams);
		this->field_44 = 0;
	}

}

// @Ok
CShellVenomElectrified::CShellVenomElectrified(CSuper* pSuper)
{
	print_if_false(pSuper != 0, "NULL pSuper sent to CVenomWrap");
	print_if_false(pSuper->mType == 313, "Non venom sent to CShellVenomElectrified");

	this->field_3C = Mem_MakeHandle(reinterpret_cast<void*>(pSuper));
}

// mpLight presets for CDummy_ctor's per-mType switch (0x552Cxx-0x552Fxx, all unnamed .rdata in
// idb_globals.txt). Named after the mType numeric case they belong to, since only some of them
// have an obvious character identity; see the switch in CDummy::CDummy for the exact mapping.
static SLight * const gLightType703 = reinterpret_cast<SLight*>(0x552C20);
static SLight * const gLightType704 = reinterpret_cast<SLight*>(0x552C58);
static SLight * const gLightType50 = reinterpret_cast<SLight*>(0x552C90);
static SLight * const gLightType319 = reinterpret_cast<SLight*>(0x552CC8);
static SLight * const gLightType307 = reinterpret_cast<SLight*>(0x552D38);
// Carnage (mType 314): print_if_false(..., "Carnage not lit?") right before this is set.
static SLight * const gLightCarnage = reinterpret_cast<SLight*>(0x552D70);
static SLight * const gLightType701 = reinterpret_cast<SLight*>(0x552DA8);
// SuperOck (mType 309): print_if_false(..., "SuperOck not lit?") right before this is set.
static SLight * const gLightSuperOck = reinterpret_cast<SLight*>(0x552DE0);
static SLight * const gLightType700 = reinterpret_cast<SLight*>(0x552E18);
// shared by mType 303 and mType 719 (both branch to the same LABEL_63 in the original).
static SLight * const gLightType303_719 = reinterpret_cast<SLight*>(0x552EC0);
// Scorpion claw (mType 308): also sets field_234=455, Spool_PSX("claw", 0).
static SLight * const gLightScorpionClaw = reinterpret_cast<SLight*>(0x552EF8);
// Scorpion (mType 310, "scimpact"): print_if_false(..., "Scorpion not lit?") right before.
static SLight * const gLightScorpion = reinterpret_cast<SLight*>(0x552FE0);
// Read (not written) by CDummy_ctor when the model has any spark-flagged face part (mFlags bit
// 0x400/0x80 set via the PSXRegion walk below); a genuine global VARIABLE holding an SLight*
// (the original reads its value, not its address), presumably filled in by another subsystem
// not investigated this session.
static SLight ** const gLightElectrifiedSlot = reinterpret_cast<SLight**>(0x552BE8);

// Utils_Jumble(gDummyTrackShuffle, 5) is called unconditionally, once, by every CDummy_ctor
// call. Purpose beyond "a 5-entry table gets shuffled here" not investigated this session (not
// read anywhere else in this file).
static i32 * const gDummyTrackShuffle = reinterpret_cast<i32*>(0x550DF8);

// @Ok
// Functional decompile, 2026-08-31 session. sub_490DF0 (0x490DF0, 0x746 bytes / 512
// instructions / 85 basic blocks / 27 callees). Every callee resolves to an already-implemented
// function (including CShellMysterioHeadCircle/CShellGoldFish/CShellMysterioHeadGlow/
// CVertexWobble, all pre-existing @Ok classes matched field-for-field against this constructor's
// inline construction sequences) except the new CDummy struct fields this session added (see
// shell.h). Field layout for CDummy's own 0x1A4-0x240 region (previously mostly raw PADDING) is
// now fully accounted for by this function plus ~CDummy plus Shell_CharacterViewer's own
// per-mType switch; see shell.h for the evidence on each field.
//
// Parameter mapping confirmed against a real call site (Shell_CharacterViewer, 0x4962D0): the
// caller reads all of pTrackA/B/C/D/E, a12 and a13 from the same parallel off_553Dxx table row
// used for pName/mTypeArg/scale/posY/defaultAnim, so all 12 constructor arguments come from one
// per-costume table entry.
CDummy::CDummy(const char* pName, i16 mTypeArg, i16 scale, i32 posY, i32 defaultAnim,
               u16* pTrackA, u16* pTrackB, u16* pTrackC, u16* pTrackD, u16* pTrackE,
               i32 a12, i32 a13)
{
	// CSuper::CSuper(), the two embedded CItem sub-objects (field_240/field_288) and the three
	// CVector arrays (field_2D4/field_304/field_418, all default-constructed to zero) are all
	// automatic via C++ member/base initialization -- matches the original's explicit
	// CItem-ctor calls and zero loops exactly (CVector's default ctor zeroes vx/vy/vz).

	Redbook_XAStop();

	this->field_1C4 = a12;
	this->field_1DC = a13;
	this->field_1C8 = G_VBLANKS;

	Utils_Jumble(gDummyTrackShuffle, 5);

	this->field_1D0 = Rnd(300) + 300;

	Spool_PSX(pName, 0);
	this->field_1D4 = 1;

	this->InitItem(pName);
	this->mType = mTypeArg;

	this->field_1A4 = pTrackA;
	this->field_1A8 = pTrackB;
	this->field_1AC = pTrackC;
	this->field_1B0 = pTrackD;
	this->field_1B4 = pTrackE;
	this->field_1C0 = defaultAnim;

	this->SelectNewTrack(0);

	this->mScale.vx = scale;
	this->mScale.vy = scale;
	this->mScale.vz = scale;

	this->mFlags |= 0x200;

	// "has any spark-flagged face part" walk over every model in this costume's PSX region
	// (SModel format already established in spool.h; face records are a compressed PSX
	// primitive format with no named struct here, same walk idiom as elsewhere in the repo --
	// see CLAUDE.md's "Walk idiom for PSX section lists").
	u8 region = this->mRegion;
	bool hasSpark = false;
	i32 modelCount = reinterpret_cast<i32*>(PSXRegion[region].ppModels)[-1];
	if (modelCount > 0)
	{
		SModel* pModel = PSXRegion[region].ppModels[0];
		for (i32 m = modelCount; m != 0; --m)
		{
			i32 numFaces = pModel->NumFaces;
			u32* pFace = reinterpret_cast<u32*>(&pModel->Vertices)
			            + 2 * pModel->NumVertices + 2 * pModel->NumNormals;
			for (i32 f = 0; f < numFaces; f++)
			{
				if ((*pFace & 4) != 0)
					hasSpark = true;
				pFace += *pFace >> 18;
			}
			pModel = reinterpret_cast<SModel*>(pFace);
		}
	}

	this->mPos.vy = posY << 12;

	if (hasSpark)
	{
		this->mpLight = *gLightElectrifiedSlot;
		this->mFlags |= 0x480;
	}

	this->field_1EC = -1;

	switch (mTypeArg)
	{
		case 703:
			this->mpLight = gLightType703;
			break;

		case 704:
			this->mpLight = gLightType704;
			break;

		case 50:
			this->field_194 |= 0x420;
			this->mpLight = gLightType50;
			break;

		case 319:
			this->mpLight = gLightType319;
			break;

		case 307:
			this->mpLight = gLightType307;
			break;

		case 303:
		case 719:
			this->mpLight = gLightType303_719;
			break;

		case 308: // Scorpion claw
			this->mpLight = gLightScorpionClaw;
			this->field_234 = 455;
			this->field_194 = static_cast<i32>(0xFFFC0000); // -262144
			this->field_198 = 0x3FFF;
			Spool_PSX("claw", 0);
			break;

		case 309: // SuperOck
			print_if_false(hasSpark != 0, "SuperOck not lit?");
			this->mpLight = gLightSuperOck;
			this->field_234 = 455;
			this->field_194 = static_cast<i32>(0xFFFE0000); // -131072
			this->field_198 = 0x1FFF;
			Spool_PSX("claw", 0);
			break;

		case 310: // Scorpion (scimpact)
			print_if_false(hasSpark != 0, "Scorpion not lit?");
			this->mpLight = gLightScorpion;
			this->field_194 = 0x00FE0000;
			this->field_240.mRegion = 0xFF;
			this->field_288.mRegion = 0xFF;
			Spool_PSX("scimpact", 0);
			break;

		case 311: // Mysterio
		{
			print_if_false(hasSpark != 0, "Mysterio not lit?");

			this->field_210 = new CShellMysterioHeadGlow();
			this->field_210->field_A4 = 30;
			this->field_210->mProtected = 1;

			if (gWhatIf)
			{
				Spool_PSX("goldfish", 0);
				new CShellGoldFish(this);
			}
			else
			{
				// two decorative "impact spark" objects, already fully implemented (same
				// gShellMysterioRelated-based decay offset/sign logic this session had
				// independently reverse engineered before finding this class already existed).
				new CShellMysterioHeadCircle(this);
				new CShellMysterioHeadCircle(this);
			}
			break;
		}

		case 324: // fire/symbiote
		{
			this->field_1EC = Spool_PSX("fire", 0);
			this->field_194 |= 0x8000;

			u8 arr1[6] = { 2, 3, 4, 9, 8, 7 };
			i32 symbiRegion1 = Spool_FindRegion("symbi_02");
			this->field_1E4 = new CVertexWobble(symbiRegion1, 1, 6, arr1, 53, 53, 133, 100);
			this->field_1E4->mProtected = 1;

			u8 arr2[6] = { 11, 10, 0, 1, 6, 5 };
			i32 symbiRegion2 = Spool_FindRegion("symbi_02");
			this->field_1E8 = new CVertexWobble(symbiRegion2, 1, 6, arr2, 53, 53, 133, 100);
			this->field_1E8->mProtected = 1;
			break;
		}

		case 700:
			this->mpLight = gLightType700;
			break;

		case 701:
			this->mpLight = gLightType701;
			break;

		default:
			break;
	}
}

// 0x00553014 and 0x0055302C. The CDummy copy of the Scorpion tail hook tables (the values are
// identical to scorpion.cpp's gTailBaseRingHooks/gTailBaseHook, but they are a separate copy in
// the binary): a circle of radius 240 around (0, 368, 464), all on bone 2.
static const i16 gDummyTailBaseRingHooks[4][3] = {
	{ 0, 368, 704 },
	{ 240, 368, 464 },
	{ 0, 368, 224 },
	{ -239, 368, 464 }
};
static const i16 gDummyTailBaseHook[3] = { 0, 368, 464 };

// 0x00553034 and 0x0055304C. The same for the tail tip: a circle of radius 120 around
// (0, -1, 0), all on bone 0.
static const i16 gDummyTailTipRingHooks[4][3] = {
	{ 120, -1, 0 },
	{ 0, -1, -120 },
	{ -119, -1, 0 },
	{ 0, -1, 120 }
};
static const i16 gDummyTailTipHook[3] = { 0, -1, 0 };

// @Ok
// 0x495970, 1862 bytes. The Mac build names it .TailRenderer__6CDummyFv, and it is the same
// code as CScorpion::TailRenderer (0x489810, scorpion.cpp) with CDummy's own field offsets:
// field_240 is the tail item, field_304[23] are the tail nodes and mpTailGeometry is the
// geometry buffer. Only caller is Shell_CharacterViewer, for the Scorpion preview (mType 310).
// Rebuilds a ring of four vertices and four normals around each of the 23 tail nodes every
// frame, then hands the tail item to M3d_Render. The first and the last ring come from model
// hooks; the ones in between are swept with a Frenet style frame kept square by two GTE cross
// products, with the ring radius tapering off along the tail.
void CDummy::TailRenderer(void)
{
	if (this->field_240.mRegion == 0xFF)
		return;

	// the tail item sits at the midpoint of the first and the last node, so every vertex can be
	// stored relative to it as an i16
	this->field_240.mPos.vx = this->field_304[0].vx
			+ (this->field_304[22].vx - this->field_304[0].vx) / 2;
	this->field_240.mPos.vy = this->field_304[0].vy
			+ (this->field_304[22].vy - this->field_304[0].vy) / 2;
	this->field_240.mPos.vz = this->field_304[0].vz
			+ (this->field_304[22].vz - this->field_304[0].vz) / 2;

	i32 firstX = (this->field_304[0].vx - this->field_240.mPos.vx) >> 12;
	i32 firstY = (this->field_304[0].vy - this->field_240.mPos.vy) >> 12;
	i32 firstZ = (this->field_304[0].vz - this->field_240.mPos.vz) >> 12;
	i32 lastX = (this->field_304[22].vx - this->field_240.mPos.vx) >> 12;
	i32 lastY = (this->field_304[22].vy - this->field_240.mPos.vy) >> 12;
	i32 lastZ = (this->field_304[22].vz - this->field_240.mPos.vz) >> 12;

	STailGeometry *pGeom = this->mpTailGeometry;

	// low half is the bigger of the two, high half the smaller
	if (firstX >= lastX)
		pGeom->BoundsX = ((lastX & 0xFFFF) << 16) | (firstX & 0xFFFF);
	else
		pGeom->BoundsX = ((firstX & 0xFFFF) << 16) | (lastX & 0xFFFF);

	if (firstY >= lastY)
		pGeom->BoundsY = ((lastY & 0xFFFF) << 16) | (firstY & 0xFFFF);
	else
		pGeom->BoundsY = ((firstY & 0xFFFF) << 16) | (lastY & 0xFFFF);

	if (firstZ >= lastZ)
		pGeom->BoundsZ = ((lastZ & 0xFFFF) << 16) | (firstZ & 0xFFFF);
	else
		pGeom->BoundsZ = ((firstZ & 0xFFFF) << 16) | (lastZ & 0xFFFF);

	CVector normal;
	CVector binormal;

	normal.vx = 0;
	normal.vy = 0;
	normal.vz = 0;
	binormal.vx = 0;
	binormal.vy = 0;
	binormal.vz = 0;

	for (u32 node = 0; node < 23; node++)
	{
		if (node == 0)
		{
			SHook hook;
			CVector centre;

			hook.Part.vx = gDummyTailBaseHook[0];
			hook.Part.vy = gDummyTailBaseHook[1];
			hook.Part.vz = gDummyTailBaseHook[2];
			hook.Offset = 2;

			centre.vx = 0;
			centre.vy = 0;
			centre.vz = 0;
			M3dUtils_GetDynamicHookPosition(
					reinterpret_cast<VECTOR*>(&centre), this, &hook);

			for (u32 i = 0; i < 4; i++)
			{
				CVector pos;

				pos.vx = 0;
				pos.vy = 0;
				pos.vz = 0;

				hook.Part.vx = gDummyTailBaseRingHooks[i][0];
				hook.Part.vy = gDummyTailBaseRingHooks[i][1];
				hook.Part.vz = gDummyTailBaseRingHooks[i][2];

				M3dUtils_GetDynamicHookPosition(
						reinterpret_cast<VECTOR*>(&pos), this, &hook);

				CVector out = (pos - centre) >> 6;
				VectorNormal(reinterpret_cast<VECTOR*>(&out),
						reinterpret_cast<VECTOR*>(&out));

				pGeom->Normals[i].vx = static_cast<i16>(out.vx);
				pGeom->Normals[i].vy = static_cast<i16>(out.vy);
				pGeom->Normals[i].vz = static_cast<i16>(out.vz);
				pGeom->Normals[i].pad = 0;

				pGeom->Vertices[i].vx = static_cast<i16>(
						(pos.vx - this->field_240.mPos.vx) >> 12);
				pGeom->Vertices[i].vy = static_cast<i16>(
						(pos.vy - this->field_240.mPos.vy) >> 12);
				pGeom->Vertices[i].vz = static_cast<i16>(
						(pos.vz - this->field_240.mPos.vz) >> 12);
				pGeom->Vertices[i].pad = 0;

				if (i == 0)
					normal = out;
			}

			continue;
		}

		// the tangent along the tail, the last node uses the chord behind it because there is
		// no node after it
		const CVector *pNode;
		CVector tangent;

		tangent.vx = 0;
		tangent.vy = 0;
		tangent.vz = 0;

		if (node == 22)
		{
			pNode = &this->field_304[22];
			tangent = (this->field_304[22] - this->field_304[21]) >> 6;
		}
		else
		{
			pNode = &this->field_304[node];
			tangent = (this->field_304[node + 1] - this->field_304[node]) >> 6;
		}

		VectorNormal(reinterpret_cast<VECTOR*>(&tangent),
				reinterpret_cast<VECTOR*>(&tangent));

		// binormal = tangent x normal, then normal = binormal x tangent, so the frame stays
		// square as the tail bends
		gte_ldopv1(reinterpret_cast<VECTOR*>(&tangent));
		gte_ldopv2(reinterpret_cast<VECTOR*>(&normal));
		gte_op12();
		gte_stlvnl(reinterpret_cast<VECTOR*>(&binormal));

		VectorNormal(reinterpret_cast<VECTOR*>(&binormal),
				reinterpret_cast<VECTOR*>(&binormal));

		gte_ldopv1(reinterpret_cast<VECTOR*>(&binormal));
		gte_ldopv2(reinterpret_cast<VECTOR*>(&tangent));
		gte_op12();
		gte_stlvnl(reinterpret_cast<VECTOR*>(&normal));

		i32 taper = 16 - ((G_RCOSSIN_TBL[(42 * (node + 1)) & 0xFFF].sin * 8) >> 12);

		if (node == 22)
		{
			// the tip ring is not swept, it comes from model hooks like the first one does
			// (the frame built above goes unused here)
			SHook hook;
			CVector centre;

			hook.Part.vx = gDummyTailTipHook[0];
			hook.Part.vy = gDummyTailTipHook[1];
			hook.Part.vz = gDummyTailTipHook[2];
			hook.Offset = 0;

			centre.vx = 0;
			centre.vy = 0;
			centre.vz = 0;
			M3dUtils_GetDynamicHookPosition(
					reinterpret_cast<VECTOR*>(&centre), this, &hook);

			for (u32 i = 0; i < 4; i++)
			{
				CVector pos;

				pos.vx = 0;
				pos.vy = 0;
				pos.vz = 0;

				hook.Part.vx = gDummyTailTipRingHooks[i][0];
				hook.Part.vy = gDummyTailTipRingHooks[i][1];
				hook.Part.vz = gDummyTailTipRingHooks[i][2];

				M3dUtils_GetDynamicHookPosition(
						reinterpret_cast<VECTOR*>(&pos), this, &hook);

				CVector out = (pos - centre) >> 6;

				// unlike the first ring this one is normalised straight into the i16 slot
				VectorNormalS(reinterpret_cast<VECTOR*>(&out),
						&pGeom->Normals[22 * 4 + i]);
				pGeom->Normals[22 * 4 + i].pad = 0;

				pGeom->Vertices[22 * 4 + i].vx = static_cast<i16>(
						(pos.vx - this->field_240.mPos.vx) >> 12);
				pGeom->Vertices[22 * 4 + i].vy = static_cast<i16>(
						(pos.vy - this->field_240.mPos.vy) >> 12);
				pGeom->Vertices[22 * 4 + i].vz = static_cast<i16>(
						(pos.vz - this->field_240.mPos.vz) >> 12);
				pGeom->Vertices[22 * 4 + i].pad = 0;
			}

			continue;
		}

		// sweep four points a quarter turn apart around the node
		for (u32 i = 0; i < 4; i++)
		{
			i32 angle = (i << 10) & 0xFFF;
			i32 sinA = G_RCOSSIN_TBL[angle].sin;
			i32 cosA = G_RCOSSIN_TBL[angle].cos;

			i32 nx = ((sinA * binormal.vx) >> 12) + ((cosA * normal.vx) >> 12);
			i32 ny = ((binormal.vy * sinA) >> 12) + ((normal.vy * cosA) >> 12);
			i32 nz = ((binormal.vz * sinA) >> 12) + ((normal.vz * cosA) >> 12);

			pGeom->Normals[node * 4 + i].vx = static_cast<i16>(nx);
			pGeom->Normals[node * 4 + i].vy = static_cast<i16>(ny);
			pGeom->Normals[node * 4 + i].vz = static_cast<i16>(nz);
			pGeom->Normals[node * 4 + i].pad = 0;

			// the original shifts the offset node position right with shr while it shifts the
			// tail centre with sar, so a node behind the origin wraps instead of going
			// negative. Kept as it is
			u32 vx = static_cast<u32>(
					static_cast<i16>(nx) * taper + pNode->vx) >> 12;
			u32 vy = static_cast<u32>(
					static_cast<i16>(ny) * taper + pNode->vy) >> 12;
			u32 vz = static_cast<u32>(
					static_cast<i16>(nz) * taper + pNode->vz) >> 12;

			pGeom->Vertices[node * 4 + i].vx = static_cast<i16>(
					static_cast<i32>(vx) - (this->field_240.mPos.vx >> 12));
			pGeom->Vertices[node * 4 + i].vy = static_cast<i16>(
					static_cast<i32>(vy) - (this->field_240.mPos.vy >> 12));
			pGeom->Vertices[node * 4 + i].vz = static_cast<i16>(
					static_cast<i32>(vz) - (this->field_240.mPos.vz >> 12));
			pGeom->Vertices[node * 4 + i].pad = 0;
		}
	}

	*gM3dNoDcModelData = 1;
	M3d_Render(&this->field_240);
	*gM3dNoDcModelData = 0;
}

// spool.cpp owns both of these but spool.h does not declare them, so they are declared here the
// same way effects.cpp and spidey.cpp already declare CurrentSuit.
extern i32 CurrentSuit;
EXPORT extern char SuitNames[11][32];

// @Ok
// sub_491560, entered through the scalar-deleting-destructor thunk at 0x491540. Drops the nine
// polymorphic members, then unloads whatever the preview model pulled in: the model's own PSX
// region (unless it is the one the player's current suit still needs) and, per mType, the extra
// regions and sub-items each costume spawned.
CDummy::~CDummy(void)
{
	if (this->field_1E0) delete reinterpret_cast<CBit*>(this->field_1E0);
	if (this->field_200) delete reinterpret_cast<CBit*>(this->field_200);
	if (this->field_204) delete reinterpret_cast<CBit*>(this->field_204);
	if (this->field_238) delete reinterpret_cast<CBit*>(this->field_238);
	if (this->field_210) delete this->field_210;
	if (this->field_1E4) delete this->field_1E4;
	if (this->field_1E8) delete this->field_1E8;
	if (this->field_1F0) delete reinterpret_cast<CBit*>(this->field_1F0);
	if (this->field_1F4) delete reinterpret_cast<CBit*>(this->field_1F4);

	if (this->field_1D4 != 0)
	{
		const char* pRegionName = PSXRegion[this->mRegion].Filename;

		// on the normal path the suit the player is wearing must stay loaded; on low graphics
		// the test is against plain "spidey" instead. The original does not fall through from
		// one test to the other, each side has its own answer
		if (gLowGraphics == 0)
		{
			if (!Utils_CompareStrings(pRegionName, SuitNames[CurrentSuit]))
			{
				Spool_ClearPSX(pRegionName);
				gsub_430880();
			}
		}
		else if (!Utils_CompareStrings(pRegionName, "spidey"))
		{
			Spool_ClearPSX(pRegionName);
			gsub_430880();
		}
	}

	switch (this->mType)
	{
		case 308:
		case 309:
		{
			// Doc Ock and monster-Ock: the claws and the four tentacle item pairs
			Spool_ClearPSX("claw");
			for (i32 i = 0; i < 4; i++)
			{
				if (this->field_224[i])
					delete reinterpret_cast<CBit*>(this->field_224[i]);
				if (this->field_214[i])
					delete reinterpret_cast<CBit*>(this->field_214[i]);
			}
			break;
		}

		case 310:
		{
			// the Scorpion built two PSX regions by hand (the tail and the stinger), so they
			// are torn back down field by field instead of through Spool_ClearPSX
			u8 tailRegion = this->field_240.mRegion;
			if (tailRegion != 0xFF)
			{
				PSXRegion[tailRegion].Filename[0] = 0;
				PSXRegion[tailRegion].Usable = 0;
				PSXRegion[tailRegion].Protected = 0;
				Mem_Delete(this->mpTailGeometry);
				PSXRegion[tailRegion].ppModels = 0;
				Mem_Delete(PSXRegion[tailRegion].pColourTable);
				PSXRegion[tailRegion].pColourTable = 0;
				PSXRegion[tailRegion].NumParts = 0;
			}

			u8 stingerRegion = this->field_288.mRegion;
			if (stingerRegion != 0xFF)
			{
				PSXRegion[stingerRegion].Filename[0] = 0;
				PSXRegion[stingerRegion].Usable = 0;
				PSXRegion[stingerRegion].Protected = 0;
				Mem_Delete(this->field_2D0);
				PSXRegion[stingerRegion].ppModels = 0;
				Mem_Delete(PSXRegion[stingerRegion].pColourTable);
				PSXRegion[stingerRegion].pColourTable = 0;
				PSXRegion[stingerRegion].NumParts = 0;
			}

			Spool_ClearPSX("scimpact");
			break;
		}

		case 311:
			// Mysterio only loaded the goldfish in the what-if mode
			if (gWhatIf)
				Spool_ClearPSX("goldfish");
			break;

		case 324:
			Spool_ClearPSX("fire");
			break;

		default:
			break;
	}

	if (this->field_1C4)
		Redbook_XAStop();

	// field_240 / field_288 (CItem) and the base class are destructed automatically.
}

// The face records the two hand built tail models are made of. The layout is confirmed by
// DCModel_CreateFromSModel (dcmodel.cpp), which parses exactly these fields back out of an
// SModel: a dword whose low word is the poly type and whose high word is the record size,
// four byte vertex indices, four colour bytes, then per-type data.
struct STailFace
{
	// 0x887: textured (bits 0x3 set), quad (bit 0x10 clear), colours already converted
	// (bit 0x800)
	u16 mFlags;
	u16 mSize;

	// low byte the first index, high byte the second
	u16 mVertices01;
	u16 mVertices23;

	u16 mColour01;
	u16 mColour23;

	// never written here, and never read back for this poly type
	PADDING(4);

	Texture* mpTexture;

	// low byte u, high byte v, one per corner
	u16 mUV[4];
};

struct STailSweepFace
{
	// 0x8C0: untextured (bits 0x3 clear), quad, colours already converted
	u16 mFlags;
	u16 mSize;

	u16 mVertices01;
	u16 mVertices23;

	u16 mColour01;
	u16 mColour23;

	PADDING(4);
};

// @Ok
// 0x4953C0, 768 bytes. Mac symbol .InitialiseTailPSX__6CDummyFv (0xE72B0). The PC body is
// the same code as CScorpion::InitialiseTailPSX (0x489050, still a stub in scorpion.cpp)
// with CDummy's own offsets: field_240 is the tail item where CScorpion has field_3F8,
// exactly 0x1B8 lower, and the light differs (gLightScorpion here, M3d_ScorpionLight
// there). Everything else matches instruction for instruction.
//
// Takes the first free PSX region, names it "S" and fills it with a one part model built by
// hand: an SModel header, 92 vertices and 92 normals (rewritten every frame by
// TailRenderer), a flat grey 256 entry colour table, and 88 textured quads, four per ring
// for the 22 gaps between the 23 tail nodes. The quads are mapped onto a 4x4 grid of the
// "new_scorp_tail" texture, the column from the corner index and the row from the ring
// index mod 4.
void CDummy::InitialiseTailPSX(void)
{
	i32 region = 0;
	while (region < MAXPSX && PSXRegion[region].Filename[0] != 0)
		region++;

	// if every region is taken the original falls straight through here, leaving mRegion
	// at the 0xFF the constructor put there
	if (region < MAXPSX)
	{
		this->field_240.mRegion = static_cast<u8>(region);

		SPSXRegion* pRegion = &PSXRegion[region];
		pRegion->Filename[0] = 'S';
		pRegion->Filename[1] = 0;
		pRegion->IsSuper = 0;
		pRegion->Usable = 1;
		pRegion->Protected = 1;
		pRegion->pPSX = 0;
		pRegion->pAnimFile = 0;
		pRegion->pHierarchy = 0;
		pRegion->pTexWibData = 0;
		pRegion->pColourPulseData = 0;
		pRegion->NumParts = 1;
		pRegion->Pad = 0;
		pRegion->pHooks = 0;
	}

	print_if_false(this->field_240.mRegion != 0xFF, "No free region");

	this->field_240.mFlags = 0x480;
	this->field_240.mAngles.vz = 0;
	this->field_240.mAngles.vy = 0;
	this->field_240.mAngles.vx = 0;
	this->field_240.mModel = 0;
	this->field_240.mNextItem = 0;
	this->field_240.mpLight = gLightScorpion;

	u32* pClut = static_cast<u32*>(DCMem_New(1024, 0, 1, 0, true));
	PSXRegion[this->field_240.mRegion].pColourTable = pClut;
	for (i32 c = 0; c < 256; c++)
		pClut[c] = 0x808080;

	// 28 byte SModel header + 92 vertices + 92 normals of 8 bytes each (1500 bytes, which
	// is exactly STailGeometry) + 88 face records of 28 bytes = 3964. The original asks for
	// eight bytes more than it uses; kept as the literal it passes.
	STailGeometry* pGeom = static_cast<STailGeometry*>(DCMem_New(3972, 0, 1, 0, true));
	this->field_280 = 1;
	this->mpTailGeometry = pGeom;
	PSXRegion[this->field_240.mRegion].ppModels =
			reinterpret_cast<SModel**>(&this->mpTailGeometry);

	// the first 28 bytes of STailGeometry are an SModel header
	SModel* pModel = reinterpret_cast<SModel*>(pGeom);
	pModel->Flags = 8;
	pModel->NumVertices = 92;
	pModel->NumNormals = 92;
	pModel->NumFaces = 88;
	pModel->Radius = 0x200000;
	pModel->zMax = 0x7FFF;
	pModel->NextLOD = 0xFFFF;

	Texture* pTexture = Spool_FindTextureEntry("new_scorp_tail");
	print_if_false(pTexture != 0, "No texture");

	i32 uOrigin = pTexture->u0;
	// the original works the u step out unsigned (shr) and the v step signed (sar)
	i32 uStep = static_cast<i32>(static_cast<u32>(pTexture->u3 - pTexture->u0) >> 2);
	i32 vStep = (pTexture->v3 - pTexture->v0) / 4;

	STailFace* pFace = reinterpret_cast<STailFace*>(
			reinterpret_cast<u8*>(pGeom) + 1500);

	for (i32 ring = 0; ring < 22; ring++)
	{
		i32 row = ring & 3;
		i32 vLo = row * vStep;
		i32 vHi = (row + 1) * vStep;

		for (i32 corner = 0; corner < 4; corner++)
		{
			pFace->mFlags = 0x887;
			pFace->mSize = 28;

			// this corner of the ring and the same corner one ring along, then the next
			// corner of both rings, wrapping back to corner 0 on the last quad
			i32 thisCorner = corner + 4 * ring;
			i32 nextCorner = (corner == 3) ? (4 * ring) : (thisCorner + 1);

			pFace->mVertices01 = static_cast<u16>(thisCorner | ((thisCorner + 4) << 8));
			pFace->mVertices23 = static_cast<u16>(nextCorner | ((nextCorner + 4) << 8));
			pFace->mColour01 = 0;
			pFace->mColour23 = 0;
			pFace->mpTexture = pTexture;

			i32 uLo = uOrigin + corner * uStep;
			i32 uHi = uOrigin + (corner + 1) * uStep;

			pFace->mUV[0] = static_cast<u16>(uLo | ((pTexture->v0 + vLo) << 8));
			pFace->mUV[1] = static_cast<u16>(uLo | ((pTexture->v0 + vHi) << 8));
			pFace->mUV[2] = static_cast<u16>(uHi | ((pTexture->v0 + vLo) << 8));
			pFace->mUV[3] = static_cast<u16>(uHi | ((pTexture->v0 + vHi) << 8));

			pFace++;
		}
	}
}

// @Ok
// 0x4956C0, 688 bytes. Mac symbol .InitialiseTailSweepPSX__6CDummyFv (0xE76E0), again the
// same code as the CScorpion version with CDummy's offsets. Builds the second hand made PSX
// region, named "SW", for the trail the tail sweeps behind it: a 4 by 23 grid of vertices
// (the four generations of tail nodes BuildTail keeps in field_418), no normals, and 132
// untextured quads. Each of the three strips gets its 22 quads twice, wound both ways, so
// the sweep is visible from either side. The colour table is a green ramp and the corner
// colours fade the strips out from 0x60 down to 0x00 along the sweep.
void CDummy::InitialiseTailSweepPSX(void)
{
	i32 region = 0;
	while (region < MAXPSX && PSXRegion[region].Filename[0] != 0)
		region++;

	if (region < MAXPSX)
	{
		this->field_288.mRegion = static_cast<u8>(region);

		SPSXRegion* pRegion = &PSXRegion[region];
		pRegion->Filename[0] = 'S';
		pRegion->Filename[1] = 'W';
		pRegion->Filename[2] = 0;
		pRegion->IsSuper = 0;
		pRegion->Usable = 1;
		pRegion->Protected = 1;
		pRegion->pPSX = 0;
		pRegion->pAnimFile = 0;
		pRegion->pHierarchy = 0;
		pRegion->pTexWibData = 0;
		pRegion->pColourPulseData = 0;
		pRegion->NumParts = 1;
		pRegion->Pad = 0;
		pRegion->pHooks = 0;
	}

	print_if_false(this->field_288.mRegion != 0xFF, "No free region");

	this->field_288.mFlags = 0;
	this->field_288.mAngles.vz = 0;
	this->field_288.mAngles.vy = 0;
	this->field_288.mAngles.vx = 0;
	this->field_288.mModel = 0;
	this->field_288.mNextItem = 0;
	this->field_288.mpLight = 0;

	u32* pClut = static_cast<u32*>(DCMem_New(1024, 0, 1, 0, true));
	PSXRegion[this->field_288.mRegion].pColourTable = pClut;

	// (r, g, b) = (c / 2, c, c / 2): a green ramp. The blue term is written as
	// (c & 0xFFFFFFFE) << 15, which is the same as (c >> 1) << 16 for every c.
	for (u32 c = 0; c < 256; c++)
		pClut[c] = (c >> 1) | (c << 8) | ((c & 0xFFFFFFFE) << 15);

	// 28 byte header + 92 vertices of 8 bytes + 132 face records of 16 = 2876, and again
	// the original asks for eight bytes more than it uses.
	void* pSweep = DCMem_New(2884, 0, 1, 0, true);
	this->field_2CC = 1;
	this->field_2D0 = pSweep;
	PSXRegion[this->field_288.mRegion].ppModels =
			reinterpret_cast<SModel**>(&this->field_2D0);

	SModel* pModel = static_cast<SModel*>(pSweep);
	pModel->Flags = 8;
	pModel->NumVertices = 92;
	pModel->NumNormals = 0;
	pModel->NumFaces = 132;
	pModel->Radius = 0x200000;
	pModel->zMax = 0x7FFF;
	pModel->NextLOD = 0xFFFF;

	// 28 byte header plus 92 vertices, and no normals this time
	STailSweepFace* pFace = reinterpret_cast<STailSweepFace*>(
			static_cast<u8*>(pSweep) + 764);

	for (i32 strip = 0; strip < 3; strip++)
	{
		i32 base = 23 * strip;

		u16 colour01;
		u16 colour23;
		if (strip == 0)
		{
			colour01 = 0x6060;
			colour23 = 0x4040;
		}
		else if (strip == 1)
		{
			colour01 = 0x4040;
			colour23 = 0x2020;
		}
		else
		{
			colour01 = 0x2020;
			colour23 = 0;
		}

		for (i32 quad = 0; quad < 22; quad++)
		{
			pFace->mFlags = 0x8C0;
			pFace->mSize = 16;
			pFace->mVertices01 =
					static_cast<u16>((base + quad) | ((base + quad + 1) << 8));
			pFace->mVertices23 =
					static_cast<u16>((base + quad + 23) | ((base + quad + 24) << 8));
			pFace->mColour01 = colour01;
			pFace->mColour23 = colour23;
			pFace++;
		}

		// the same 22 quads again with the two index pairs swapped, so they face the
		// other way
		for (i32 back = 0; back < 22; back++)
		{
			pFace->mFlags = 0x8C0;
			pFace->mSize = 16;
			pFace->mVertices01 =
					static_cast<u16>((base + back + 1) | ((base + back) << 8));
			pFace->mVertices23 =
					static_cast<u16>((base + back + 24) | ((base + back + 23) << 8));
			pFace->mColour01 = colour01;
			pFace->mColour23 = colour23;
			pFace++;
		}
	}
}

// @Ok
// 0x489660. The PC linker folded two identical bodies onto this one address:
// CDummy::ScorpionUniformCurveTesselator (Mac 0xE7B50) and CScorpion::UniformCurveTesselator
// (Mac 0xD8890, which is the name tools/names.json carries for it). Neither of them reads
// its own object, which is why the two bodies came out the same. Kept here as the CDummy
// method because CDummy::BuildTail is its only decompiled caller; CScorpion::BuildTail, when
// somebody writes it, gets the identical body under the CScorpion name.
//
// Walks a cubic bezier through the four control points and writes numPoints evenly spaced
// positions into pOut. The two ends come straight from the outer control points; every point
// in between is a GTE weighted sum of all four with the Bernstein weights held in 12.12.
// The control points come in shifted down 12, so the shift back up is what puts the output
// in world units.
void CDummy::ScorpionUniformCurveTesselator(CVector* pControl, u32 numPoints, CVector* pOut)
{
	u32 step = 4096 / (numPoints - 1);

	pOut[0] = pControl[0] << 12;
	pOut[numPoints - 1] = pControl[3] << 12;

	u32 remaining = numPoints - 1;
	if (remaining > 1)
	{
		CVector* pPoint = &pOut[1];
		i32 t = static_cast<i32>(step);
		i32 s = 4096 - static_cast<i32>(step);

		for (u32 left = remaining - 1; left != 0; left--)
		{
			i32 x = 0;
			i32 y = 0;
			i32 z = 0;

			i32 weight = (s * ((s * s) >> 12)) >> 12;

			for (u32 c = 0; c < 4; c++)
			{
				gte_ldlvl(reinterpret_cast<VECTOR*>(&pControl[c]));
				gte_lddp(weight);
				gte_gpf0();

				// the weight for the NEXT control point, worked out while the GTE runs
				if (c == 0)
					weight = (3 * t * ((s * s) >> 12)) >> 12;
				else if (c == 1)
					weight = (t * ((3 * t * s) >> 12)) >> 12;
				else if (c == 2)
					weight = (t * ((t * t) >> 12)) >> 12;

				CVector scaled;
				gte_stlvnl(reinterpret_cast<VECTOR*>(&scaled));
				x += scaled.vx;
				y += scaled.vy;
				z += scaled.vz;
			}

			pPoint->vx = x;
			pPoint->vy = y;
			pPoint->vz = z;
			pPoint++;

			t += static_cast<i32>(step);
			s -= static_cast<i32>(step);
		}
	}
}

// @Ok
// 0x494A60, 384 bytes. The same fold again: CDummy::DocOckUniformCurveTesselator (Mac
// 0xE6970) and CDummy::SuperOckUniformCurveTesselator (Mac 0xE6030) have identical bodies,
// so the PC build has one copy, called eight times each from DocOckBuildArms (0x494BE0) and
// SuperOckBuildArms (0x494280). Same cubic bezier as ScorpionUniformCurveTesselator above,
// but it does the weighting in plain integer maths instead of the GTE, writes into a ribbon
// spine (28 byte SSimpleRibbonParams, position first) instead of bare CVectors, and takes
// its parameter step from field_234 rather than working it out from numPoints.
void CDummy::DocOckUniformCurveTesselator(CVector* pControl, u32 numPoints,
		SSimpleRibbonParams* pOut)
{
	pOut[0].mPos = pControl[0] << 12;
	pOut[numPoints - 1].mPos = pControl[3] << 12;

	u32 remaining = numPoints - 1;
	if (remaining > 1)
	{
		SSimpleRibbonParams* pPoint = &pOut[1];
		i32 t = 0;

		for (u32 left = remaining - 1; left != 0; left--)
		{
			i32 x = 0;
			i32 y = 0;
			i32 z = 0;

			t += this->field_234;

			i32 s = 4096 - t;
			i32 s2 = (s * s) >> 12;
			i32 weight = ((4096 - t) * s2) >> 12;

			for (u32 c = 0; c < 4; c++)
			{
				CVector* pPos = &pControl[c];
				x += pPos->vx * weight;
				z += pPos->vz * weight;
				y += pPos->vy * weight;

				if (c == 0)
					weight = (3 * t * s2) >> 12;
				else if (c == 1)
					weight = (t * ((3 * t * s) >> 12)) >> 12;
				else if (c == 2)
					weight = (t * ((t * t) >> 12)) >> 12;
			}

			pPoint->mPos.vx = x;
			pPoint->mPos.vy = y;
			pPoint->mPos.vz = z;
			pPoint++;
		}
	}
}

// @Ok
// 0x4960C0, 528 bytes. Mac symbol .BuildTail__6CDummyFv (0xE8420). Rebuilds the Scorpion
// preview's 23 tail nodes every frame out of the model's own bones: hooks 17 to 20 give the
// four control points of the first half of the tail, hooks 20 to 23 the second half (the
// two halves share hook 20, so the tail is one smooth curve), and each half is tesselated
// into 12 nodes that overlap at node 11. Then the sweep history rolls along by a frame.
void CDummy::BuildTail(void)
{
	SHook hook;
	hook.Part.vx = 0;
	hook.Part.vy = 0;
	hook.Part.vz = 0;

	hook.Offset = 17;
	M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&this->field_2D4[0]), this, &hook);
	this->field_2D4[0] >>= 12;

	hook.Offset = 18;
	M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&this->field_2D4[1]), this, &hook);
	this->field_2D4[1] >>= 12;

	hook.Offset = 19;
	M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&this->field_2D4[2]), this, &hook);
	this->field_2D4[2] >>= 12;

	hook.Offset = 20;
	M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&this->field_2D4[3]), this, &hook);
	this->field_2D4[3] >>= 12;

	this->ScorpionUniformCurveTesselator(this->field_2D4, 12, &this->field_304[0]);

	// the second half starts where the first one ended
	hook.Offset = 21;
	this->field_2D4[0] = this->field_2D4[3];
	M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&this->field_2D4[1]), this, &hook);
	this->field_2D4[1] >>= 12;

	hook.Offset = 22;
	M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&this->field_2D4[2]), this, &hook);
	this->field_2D4[2] >>= 12;

	hook.Offset = 23;
	M3dUtils_GetDynamicHookPosition(
			reinterpret_cast<VECTOR*>(&this->field_2D4[3]), this, &hook);
	this->field_2D4[3] >>= 12;

	this->ScorpionUniformCurveTesselator(this->field_2D4, 12, &this->field_304[11]);

	// Roll the four sweep generations along, oldest one dropped. The original shifts 24
	// slots although there are only 23 tail nodes: the last pass reads one CVector past
	// field_304, which is field_418[0][0], and by then that slot already holds
	// field_304[0]. Reproduced as written. The extra column is never drawn, the sweep mesh
	// only uses 23 of each generation.
	CVector* pNodes = this->field_304;
	for (i32 i = 0; i < 24; i++)
	{
		this->field_418[3][i] = this->field_418[2][i];
		this->field_418[2][i] = this->field_418[1][i];
		this->field_418[1][i] = this->field_418[0][i];
		this->field_418[0][i] = pNodes[i];
	}
}


// The hook (bone) offsets each of monster-Ock's four arms is built from: the four control
// points of the first half of the arm, then the three more that finish the second half.
// The two halves share the fourth hook, so each arm is one smooth curve. Read straight out
// of the unrolled original at 0x494280. The arms are not in bone order: arm 1 uses the
// higher block and arm 2 the lower one.
static const i16 gSuperOckArmHooks[4][7] = {
	{ 17, 18, 19, 20, 21, 22, 23 },
	{ 31, 32, 33, 34, 35, 36, 37 },
	{ 24, 25, 26, 27, 28, 29, 30 },
	{ 38, 39, 40, 41, 42, 43, 44 }
};

// Doc Ock's own list (0x494BE0). It is the SuperOck list with one added to every entry,
// nothing else differs between the two functions.
static const i16 gDocOckArmHooks[4][7] = {
	{ 18, 19, 20, 21, 22, 23, 24 },
	{ 32, 33, 34, 35, 36, 37, 38 },
	{ 25, 26, 27, 28, 29, 30, 31 },
	{ 39, 40, 41, 42, 43, 44, 45 }
};

// The texture both arm builders put on the ribbons, by checksum.
static const u32 gOckArmTextureChecksum = 0x9809FFF5;

// @Ok
// 0x494280, 2016 bytes. Mac symbol .SuperOckBuildArms__6CDummyFv (0xE6230). Builds the four
// tentacles of the monster-Ock preview costume (mType 309). Each arm is a
// CSimpleTexturedRibbon of 18 faces (19 spine points, all 10 units wide) whose spine is a
// pair of cubic beziers through seven of the model's bones, and a "claw" CBody sitting on
// the last spine point, aimed along the last segment. The four claws are chained through
// mNextItem so CDummy's renderer can draw them from field_214[0] in one M3d_Render call,
// the same way CDocOc::RenderClaws draws its own field_570[0].
//
// The original writes the whole thing out unrolled, four arms by two halves by three or
// four hooks. It is written as a loop over gSuperOckArmHooks above, which is the same work
// in the same order.
void CDummy::SuperOckBuildArms(void)
{
	for (i32 ribbon = 0; ribbon < 4; ribbon++)
	{
		if (this->field_224[ribbon] == 0)
		{
			this->field_224[ribbon] = new CSimpleTexturedRibbon(18);
			this->field_224[ribbon]->SetTexture(
					Spool_FindTextureEntry(gOckArmTextureChecksum));
			this->field_224[ribbon]->SetOpaque();
			this->field_224[ribbon]->SetRGB(128, 128, 128);

			for (i32 point = 0; point < 19; point++)
				this->field_224[ribbon]->field_44[point].mWidth = 10;
		}
	}

	CVector control[4];
	for (i32 c = 0; c < 4; c++)
	{
		control[c].vx = 0;
		control[c].vy = 0;
		control[c].vz = 0;
	}

	SHook hook;
	hook.Part.vx = 0;
	hook.Part.vy = 0;
	hook.Part.vz = 0;

	for (i32 arm = 0; arm < 4; arm++)
	{
		const i16* pArmHooks = gSuperOckArmHooks[arm];
		SSimpleRibbonParams* pSpine = this->field_224[arm]->field_44;

		for (i32 h = 0; h < 4; h++)
		{
			hook.Offset = pArmHooks[h];
			M3dUtils_GetDynamicHookPosition(
					reinterpret_cast<VECTOR*>(&control[h]), this, &hook);
			control[h] >>= 12;
		}

		this->DocOckUniformCurveTesselator(control, 10, pSpine);

		// the second half starts where the first one ended
		control[0] = control[3];

		for (i32 h2 = 0; h2 < 3; h2++)
		{
			hook.Offset = pArmHooks[h2 + 4];
			M3dUtils_GetDynamicHookPosition(
					reinterpret_cast<VECTOR*>(&control[h2 + 1]), this, &hook);
			control[h2 + 1] >>= 12;
		}

		this->DocOckUniformCurveTesselator(control, 10, &pSpine[9]);
	}

	// backwards, because each claw points at the next one and the last one ends the chain
	for (i32 claw = 3; claw >= 0; claw--)
	{
		if (this->field_214[claw] == 0)
		{
			this->field_214[claw] = new CBody();
			this->field_214[claw]->InitItem("claw");
			this->field_214[claw]->mFlags &= ~2u;

			if (claw == 3)
				this->field_214[3]->mNextItem = 0;
			else
				this->field_214[claw]->mNextItem = this->field_214[claw + 1];
		}

		SSimpleRibbonParams* pClawSpine = this->field_224[claw]->field_44;
		this->field_214[claw]->mPos = pClawSpine[18].mPos;
		Utils_CalcAim(&this->field_214[claw]->mAngles,
				&pClawSpine[17].mPos, &pClawSpine[18].mPos);
	}
}

// @Ok
// 0x494BE0, 2016 bytes. Mac symbol .DocOckBuildArms__6CDummyFv (0xE6B70). Instruction for
// instruction the same as SuperOckBuildArms above, for the Doc Ock preview costume (mType
// 308); the only difference in the whole function is that every hook offset is one higher.
void CDummy::DocOckBuildArms(void)
{
	for (i32 ribbon = 0; ribbon < 4; ribbon++)
	{
		if (this->field_224[ribbon] == 0)
		{
			this->field_224[ribbon] = new CSimpleTexturedRibbon(18);
			this->field_224[ribbon]->SetTexture(
					Spool_FindTextureEntry(gOckArmTextureChecksum));
			this->field_224[ribbon]->SetOpaque();
			this->field_224[ribbon]->SetRGB(128, 128, 128);

			for (i32 point = 0; point < 19; point++)
				this->field_224[ribbon]->field_44[point].mWidth = 10;
		}
	}

	CVector control[4];
	for (i32 c = 0; c < 4; c++)
	{
		control[c].vx = 0;
		control[c].vy = 0;
		control[c].vz = 0;
	}

	SHook hook;
	hook.Part.vx = 0;
	hook.Part.vy = 0;
	hook.Part.vz = 0;

	for (i32 arm = 0; arm < 4; arm++)
	{
		const i16* pArmHooks = gDocOckArmHooks[arm];
		SSimpleRibbonParams* pSpine = this->field_224[arm]->field_44;

		for (i32 h = 0; h < 4; h++)
		{
			hook.Offset = pArmHooks[h];
			M3dUtils_GetDynamicHookPosition(
					reinterpret_cast<VECTOR*>(&control[h]), this, &hook);
			control[h] >>= 12;
		}

		this->DocOckUniformCurveTesselator(control, 10, pSpine);

		control[0] = control[3];

		for (i32 h2 = 0; h2 < 3; h2++)
		{
			hook.Offset = pArmHooks[h2 + 4];
			M3dUtils_GetDynamicHookPosition(
					reinterpret_cast<VECTOR*>(&control[h2 + 1]), this, &hook);
			control[h2 + 1] >>= 12;
		}

		this->DocOckUniformCurveTesselator(control, 10, &pSpine[9]);
	}

	for (i32 claw = 3; claw >= 0; claw--)
	{
		if (this->field_214[claw] == 0)
		{
			this->field_214[claw] = new CBody();
			this->field_214[claw]->InitItem("claw");
			this->field_214[claw]->mFlags &= ~2u;

			if (claw == 3)
				this->field_214[3]->mNextItem = 0;
			else
				this->field_214[claw]->mNextItem = this->field_214[claw + 1];
		}

		SSimpleRibbonParams* pClawSpine = this->field_224[claw]->field_44;
		this->field_214[claw]->mPos = pClawSpine[18].mPos;
		Utils_CalcAim(&this->field_214[claw]->mAngles,
				&pClawSpine[17].mPos, &pClawSpine[18].mPos);
	}
}


// 0x0056EFE4, the CBody list every effect and projectile object attaches itself to. Named
// gEffectBodyList in carnage.cpp, which holds the same file-local pointer; the maintainer's IDB
// calls the same address BulletList.
static CBody ** const gEffectBodyList = reinterpret_cast<CBody**>(0x0056EFE4);

// @Ok
// 0x48ED80, 310 bytes. names.json calls it CTailRing_CTailRing. One ring of the Scorpion
// stinger explosion. It is a plain CBody on the "scimpact" region, put on the shared effect
// list, with the growth parameters CScorpExplosion picks stored straight into its own fields.
// The odd one out is the last: the caller's duration is turned into a per-frame step by
// scaling it into 12.12 over 450.
CTailRing::CTailRing(CVector* pPos, i32 a3, i32 a4, i32 a5, i32 a6, i32 a7,
		u8 a8, u8 a9, i32 a10, i32 a11)
{
	this->AttachTo(gEffectBodyList);
	this->InitItem("scimpact");

	// The region's first face carries the poly flags (the face list starts after the 28 byte
	// SModel header, the vertices and the normals, 8 bytes each). If it is not marked semi
	// transparent yet, mask the flag in across the whole region.
	SModel* pModel = PSXRegion[this->mRegion].ppModels[0];
	u32* pWords = reinterpret_cast<u32*>(pModel);
	if ((pWords[2 * (pModel->NumVertices + pModel->NumNormals) + 7] & 0x200) == 0)
		Spool_MaskFaceFlags(this->mRegion, 0x200, 0xFFFFEFFF);

	this->mPos = *pPos;

	this->field_108 = a9;
	this->field_10C = a10;
	this->field_104 = a8;
	this->field_F8 = a3;
	this->field_100 = a7;
	this->field_110 = a11;
	this->field_11C = a5;
	this->field_118 = a4;

	this->mFlags |= 0x601;

	this->field_120 = (a6 << 12) / 450;
}

// @Ok
// 0x48F040. names.json calls it CScorpExplosion_CScorpExplosion_0. The burst CDummy::AI fires
// for the Scorpion preview (mType 310) at the end of the sting anim: two CTailRings (kept as
// handles so whatever runs the explosion can find them again), a CGrenadeWave shock ring, and
// ten CPingLines thrown out around the impact point at random yaws.
CScorpExplosion::CScorpExplosion(CVector* pPos)
{
	CVector pos = *pPos;

	this->field_3C = Mem_MakeHandle(
			new CTailRing(&pos, 0, 4096, 2048, 350, 6, 0, 8, 30, 105));
	this->field_44 = Mem_MakeHandle(
			new CTailRing(&pos, 0, 4096, 2048, 199, 7, 0, 8, 30, 105));

	CGrenadeWave* pWave = new CGrenadeWave(&pos, 255, 200, 0, 250, 7);
	pWave->mMask = 0xFFFFFFC0;
	pWave->mAngle = 1024;

	// the ping line direction. Its vy doubles as the scratch the random yaw is kept in, so it
	// changes every time round the loop below; the original reuses the same stack slot for both.
	CSVector dir;
	dir.vy = 0;
	dir.vx = -312;

	CVector spot;
	spot.vx = 0;
	spot.vz = 0;
	spot.vy = pos.vy;

	for (i32 i = 10; i != 0; i--)
	{
		dir.vy = static_cast<i16>(Rnd(4096));

		i32 sinYaw = G_RCOSSIN_TBL[dir.vy & 0xFFF].sin;
		i32 cosYaw = 5 * G_RCOSSIN_TBL[dir.vy & 0xFFF].cos;

		spot.vx = pPos->vx - 80 * sinYaw;
		spot.vz = pPos->vz - 16 * cosYaw;

		new CPingLine(&spot, &dir, 255, 128, 0, 50, 200, Rnd(50) + 30);
	}
}

// @Ok
// 0x48FD50. names.json calls it CShellSimbySlimeBase_SetQuadCoords. Spreads the four outer
// corners in field_C0 over the four quads of the puddle. Each quad gets one outer corner, the
// midpoints of the two edges that meet there, and the centre of the whole patch, so the four
// of them tile the square without a seam.
void CShellSimbySlimeBase::SetQuadCoords(void)
{
	this->mPos = this->field_C0[0];
	this->field_B4->mPosB = this->field_C0[1];
	this->field_B8->mPosC = this->field_C0[2];
	this->field_BC->mPosD = this->field_C0[3];

	CVector midAB = (this->field_C0[0] + this->field_C0[1]) >> 1;
	CVector midBD = (this->field_C0[1] + this->field_C0[3]) >> 1;
	CVector midCD = (this->field_C0[2] + this->field_C0[3]) >> 1;
	CVector midAC = (this->field_C0[0] + this->field_C0[2]) >> 1;
	CVector centre = (midBD + midAC) >> 1;

	this->mPosD = centre;
	this->field_B4->mPosC = centre;
	this->field_B8->mPosB = centre;
	this->field_BC->mPos = centre;

	this->mPosB = midAB;
	this->field_B4->mPos = midAB;

	this->field_B4->mPosD = midBD;
	this->field_BC->mPosB = midBD;

	this->field_BC->mPosC = midCD;
	this->field_B8->mPosD = midCD;

	this->mPosC = midAC;
	this->field_B8->mPos = midAC;
}

// @Ok
// 0x48F8A0. names.json calls it CShellSimbySlimeBase_CShellSimbySlimeBase. Builds the slime
// puddle under the symbiote costume preview (mType 324): four "Effects" quads (this one plus
// three it owns), with the outer corners laid out 80 units either way along the two axes the
// given yaw points down, then handed to SetQuadCoords.
CShellSimbySlimeBase::CShellSimbySlimeBase(CVector* pPos, CSVector* pAngles, i32 a4)
{
	this->field_84.vx = 0;
	this->field_84.vy = 0;
	this->field_84.vz = 0;
	this->field_90.vx = 0;
	this->field_90.vy = 0;
	this->field_90.vz = 0;

	for (i32 c = 0; c < 4; c++)
	{
		this->field_C0[c].vx = 0;
		this->field_C0[c].vy = 0;
		this->field_C0[c].vz = 0;
	}

	SAnimFrame* pAnim = Spool_FindAnim("Effects", 1);

	this->SetTexture(pAnim[8].pTexture);
	this->SetOpaque();

	this->field_B4 = new CQuadBit();
	this->field_B4->mProtected = 1;
	this->field_B4->SetTexture(pAnim[9].pTexture);
	this->field_B4->SetOpaque();
	this->field_B4->mType = 39;

	this->field_B8 = new CQuadBit();
	this->field_B8->mProtected = 1;
	this->field_B8->SetTexture(pAnim[10].pTexture);
	this->field_B8->SetOpaque();
	this->field_B8->mType = 39;

	this->field_BC = new CQuadBit();
	this->field_BC->mProtected = 1;
	this->field_BC->SetTexture(pAnim[11].pTexture);
	this->field_BC->SetOpaque();
	this->field_BC->mType = 39;

	this->field_9C = a4;

	// the two 80 unit axes of the patch, worked out from the yaw alone
	i32 yaw = pAngles->vy;
	i32 cosYaw = G_RCOSSIN_TBL[yaw & 0xFFF].cos;
	i32 sinNegYaw = G_RCOSSIN_TBL[(-yaw) & 0xFFF].sin;

	CVector axisU;
	CVector axisV;
	axisU.vx = 80 * sinNegYaw;
	axisU.vy = 0;
	axisU.vz = -80 * cosYaw;
	axisV.vx = 80 * cosYaw;
	axisV.vy = 0;
	axisV.vz = axisU.vx;

	this->field_C0[0] = (*pPos - axisU) - axisV;
	this->field_C0[1] = (*pPos + axisU) - axisV;
	this->field_C0[2] = (*pPos - axisU) + axisV;
	this->field_C0[3] = (*pPos + axisU) + axisV;

	this->SetQuadCoords();

	this->field_84 = *pPos;
	this->field_90 = *pAngles;

	for (i32 p = 0; p < 4; p++)
		this->field_F0[p] = Rnd(4096);

	for (i32 q = 0; q < 4; q++)
		this->field_100[q] = Rnd(120) + 30;

	this->mType = 11;
}

// 0x682934, the cursor into gDummyTrackShuffle below. CDummy::AI steps it once per idle
// timeout and reshuffles the table every time it wraps past 5. It sits four bytes before
// gPshellArmorRealted (0x682940) and is not in the maintainer's IDB, so the name is my guess.
static u8 * const gDummyTrackShuffleCursor = reinterpret_cast<u8*>(0x682934);

// 0x682770, nonzero while an XA track is already playing. spidey.cpp holds the same address
// under the name gRedbookXaPlayingMaybe; the maintainer's IDB names the byte right after it
// (0x682771) Redbook_XAPaused, so this is part of the redbook state block.
static u8 * const gRedbookXaPlaying = reinterpret_cast<u8*>(0x682770);

// The two CDummy animation tracks CDummy::AI recognises by address for Venom (mType 313), and
// the one it recognises for the symbiote costume (mType 324). All three are 0xFFFF terminated
// u16 lists in the original .rdata; the AI only compares field_1BC against them, it never
// reads them here.
//  - 0x553714 { 0x1f 0x20 0x20 0x20 0x21 0x28 0x29 0xffff }: the wrap track, the one Venom is
//    electrified during.
//  - 0x553724 { 0x01 0x0d 0x0c 0x0a 0x04 0x05 0x07 0x0b 0x09 0x0d 0x0c 0x02 0xffff }: the
//    fade track. Step 1 of it is anim 13 and step 10 is anim 12, which is exactly what the
//    two print_if_false checks below assert.
//  - 0x5537E4 { 0x2c 0x2d 0x2d 0x2d 0x2d 0x2d 0x2d 0x2d 0xffff }: the symbiote death track.
static const u16 * const gDummyVenomWrapTrack = reinterpret_cast<const u16*>(0x553714);
static const u16 * const gDummyVenomFadeTrack = reinterpret_cast<const u16*>(0x553724);
static const u16 * const gDummySymbioteDeathTrack = reinterpret_cast<const u16*>(0x5537E4);

// @Ok
// 0x491A10, 0x123E bytes (4670). CDummy's vtable slot 2 (off_53BFAC), so the only way in is the
// item list the costume viewer and the main menu previews run every frame. It is the whole per
// frame behaviour of a preview model, in four parts: the XA music the costume plays, the
// animation track the player steps through with triangle and square, the outline fade toggle on
// L1+L2+R1+R2, and then one arm per mType for whatever effects that costume owns.
//
// The original inlines CDummy::SelectNewAnim, FadeAway, FadeBack and the constructors of the
// four CShell* effect classes that live in this same file. They are written as calls here.
void CDummy::AI(void)
{
	SHook hook;
	hook.Part.vx = 0;
	hook.Part.vy = 0;
	hook.Part.vz = 0;

	// --- the costume's own intro track, once the model has been up for 30 vblanks ---
	if (this->field_1C4 != 0 && this->field_1CC == 0
			&& static_cast<u32>(G_VBLANKS - this->field_1C8) > 30)
	{
		Redbook_XAPlay(this->field_1C4 >> 16, static_cast<u16>(this->field_1C4), 0);
		this->field_1CC = 1;
	}

	// --- idle timer: every 300..599 frames, another random track out of the shuffle ---
	if (this->field_1D0 != 0)
	{
		this->field_1D0--;
		if (this->field_1D0 == 0)
		{
			this->field_1D0 = Rnd(300) + 300;

			if (this->field_1DC != 0 && *gRedbookXaPlaying == 0)
			{
				const SDummyXATrack* pTracks =
						reinterpret_cast<const SDummyXATrack*>(this->field_1DC);

				u8 cursor = *gDummyTrackShuffleCursor;
				i32 trackA = 0;
				i32 trackB = 0;
				i32 tries = 0;

				while (trackB == 0)
				{
					cursor++;
					*gDummyTrackShuffleCursor = cursor;
					if (cursor >= 5)
					{
						*gDummyTrackShuffleCursor = 0;
						Utils_Jumble(gDummyTrackShuffle, 5);
						cursor = *gDummyTrackShuffleCursor;
					}

					tries++;

					i32 slot = gDummyTrackShuffle[cursor];
					trackA = pTracks[slot + 2].TrackA;
					trackB = pTracks[slot + 2].TrackB;

					// ten goes at finding a row that has anything in it, then give up
					if (tries > 10)
						break;
					if (trackA != 0)
						break;
				}

				if (trackA != 0 || trackB != 0)
					Redbook_XAPlay(trackA, trackB, 0);
			}
		}
	}

	if (this->mAnimFinished != 0)
		this->SelectNewAnim();

	// --- triangle and square step to the next track; the symbiote death effect locks it out ---
	if (this->field_1F0 == 0)
	{
		if (G_SCONTROL[0].Triangle.Triggered != 0)
		{
			G_SCONTROL[0].Triangle.Triggered = 0;

			u16* pTrack = this->field_1B0;
			if (pTrack != 0 && *pTrack != 0xFFFF)
			{
				this->field_1B8 = pTrack;
				this->field_1BC = pTrack;
				this->RunAnim(*pTrack, 0, -1);
			}

			if (this->field_1DC != 0 && this->field_1CC != 0 && *gRedbookXaPlaying == 0)
			{
				const SDummyXATrack* pTracks =
						reinterpret_cast<const SDummyXATrack*>(this->field_1DC);
				if (pTracks[1].TrackA != 0 || pTracks[1].TrackB != 0)
					Redbook_XAPlay(pTracks[1].TrackA, pTracks[1].TrackB, 0);
			}
		}

		if (this->mType != 324 && G_SCONTROL[0].Square.Triggered != 0)
		{
			G_SCONTROL[0].Square.Triggered = 0;

			u16* pTrack = this->field_1B4;
			if (pTrack != 0 && *pTrack != 0xFFFF)
			{
				this->field_1B8 = pTrack;
				this->field_1BC = pTrack;
				this->RunAnim(*pTrack, 0, -1);
			}

			const SDummyXATrack* pTracks =
					reinterpret_cast<const SDummyXATrack*>(this->field_1DC);
			if (pTracks != 0 && this->field_1CC != 0 && *gRedbookXaPlaying == 0
					&& (pTracks[0].TrackA != 0 || pTracks[0].TrackB != 0))
				Redbook_XAPlay(pTracks[0].TrackA, pTracks[0].TrackB, 0);
		}
	}

	this->UpdateFrame();

	// --- L1+L2+R1+R2 toggles the outline fade, once per press ---
	if (this->field_1D8 != 0)
	{
		if (G_SCONTROL[0].LeftOne.Pressed != 0 && G_SCONTROL[0].LeftTwo.Pressed != 0
				&& G_SCONTROL[0].RightOne.Pressed != 0 && G_SCONTROL[0].RightTwo.Pressed != 0)
		{
			if (this->field_1D9 == 0)
			{
				// only these seven costumes have an outline to fade; everything else just
				// takes the latch and does nothing
				u16 type = this->mType;
				bool canFade;
				if (type <= 319)
					canFade = (type == 319 || type == 50 || type == 303 || type == 316);
				else
					canFade = (type == 700 || type == 704 || type == 719);

				if (canFade)
				{
					if (this->field_1F8 != 0)
						this->FadeBack();
					else
						this->FadeAway();
				}
			}

			this->field_1D9 = 1;
		}
		else
		{
			this->field_1D9 = 0;
		}
	}

	// --- the two fade ramps, one step per frame ---
	if (this->field_1F8 != 0)
	{
		i32 level = static_cast<u8>(this->mRGB);
		if (level != 0)
			level--;

		this->mRGB = level | ((level | (level << 8)) << 8);

		// the original only clears the low byte of this when it goes negative, which comes to
		// the same thing because SetOutlineRGB takes it a byte at a time
		i32 outline = 128 - 4 * level;
		if (outline < 0)
			outline = 0;

		this->SetOutlineRGB(static_cast<u8>(outline), static_cast<u8>(outline),
				static_cast<u8>(outline));
	}

	if (this->field_1FC != 0)
	{
		i32 level = static_cast<u8>(this->mRGB);
		if (level == 32)
		{
			this->field_1FC = 0;
			this->mFlags = static_cast<u16>((this->mFlags & 0xF7FF) | 0x80);
			this->OutlineOff();
		}
		else
		{
			level++;
			if (level > 32)
				level = 32;

			this->mRGB = level | ((level | (level << 8)) << 8);

			i32 outline = 128 - 4 * level;
			if (outline < 0)
				outline = 0;

			this->SetOutlineRGB(static_cast<u8>(outline), static_cast<u8>(outline),
					static_cast<u8>(outline));
		}
	}

	// the Scorpion builds its two hand made regions the first frame it runs
	if (this->mType == 310)
	{
		if (this->field_240.mRegion == 0xFF)
			this->InitialiseTailPSX();

		if (this->field_288.mRegion == 0xFF)
			this->InitialiseTailSweepPSX();
	}

	M3d_BuildTransform(this);

	// --- one arm per costume. The original splits this into an "above 311" jump table, a
	// separate test for 311, a pair for 309/310 and an if chain for the rest; it is one switch
	// here because the arms do not share any code.
	switch (this->mType)
	{
		case 50:
		{
			// the spidey preview hangs off a web strand for the two hanging anims
			u16 anim = this->mAnim;
			if ((anim == 290 && this->mFrame >= 4) || anim == 291)
			{
				CVector top;
				CVector bottom;
				top.vx = 0;
				top.vy = 0;
				top.vz = 0;
				bottom.vx = 0;
				bottom.vy = 0;
				bottom.vz = 0;

				hook.Offset = 11;
				M3dUtils_GetDynamicHookPosition(
						reinterpret_cast<VECTOR*>(&top), this, &hook);

				bottom.vy = top.vy - 1978368;
				bottom.vx = top.vx;
				bottom.vz = top.vz;

				if (this->field_1E0 == 0)
				{
					this->field_1E0 = new CKnottedWeb(top, bottom);
					this->field_1E0->mProtected = 1;
					this->field_1E0->field_6E = 1;
				}

				this->field_1E0->SetStartAndEnd(&top, &bottom);
			}
			else if (this->field_1E0 != 0)
			{
				delete this->field_1E0;
				this->field_1E0 = 0;
			}
			break;
		}

		case 307:
		{
			// the Rhino: one foot stomp per landing, then steam out of both nostrils while he
			// is snorting
			if (this->mAnim == 19 && this->mFrame == 19)
			{
				if (this->field_20C == 0)
				{
					CVector stomp;
					stomp.vx = this->mPos.vx;
					stomp.vy = this->mPos.vy + 450560;
					stomp.vz = this->mPos.vz;

					Effects_FootStomp(&stomp, 0x0F2354AC);
					this->field_20C = 1;
				}
			}
			else
			{
				this->field_20C = 0;
			}

			u16 anim = this->mAnim;
			if ((anim == 0 && this->mFrame >= 21 && this->mFrame <= 38)
					|| (anim == 9 && static_cast<u16>(this->mFrame) < 10)
					|| (anim == 15 && this->mFrame >= 1 && this->mFrame <= 12))
			{
				CVector nostril;
				CVector puff;
				nostril.vx = 0;
				nostril.vy = 0;
				nostril.vz = 0;
				puff.vx = 0;
				puff.vy = 0;
				puff.vz = 0;

				// left nostril: the hook itself is the position, and the step from it to a
				// second hook a little further out is the puff's velocity
				hook.Part.vx = -32;
				hook.Part.vy = 128;
				hook.Part.vz = -640;
				hook.Offset = 15;
				M3dUtils_GetDynamicHookPosition(
						reinterpret_cast<VECTOR*>(&nostril), this, &hook);

				hook.Part.vy += 48;
				hook.Part.vz -= 32;
				hook.Part.vx = -48;
				M3dUtils_GetDynamicHookPosition(
						reinterpret_cast<VECTOR*>(&puff), this, &hook);

				puff -= nostril;
				new CShellRhinoNasalSteam(&nostril, &puff);

				// right nostril, mirrored
				hook.Part.vx = 32;
				hook.Part.vy = 128;
				hook.Part.vz = -640;
				hook.Offset = 15;
				M3dUtils_GetDynamicHookPosition(
						reinterpret_cast<VECTOR*>(&nostril), this, &hook);

				hook.Part.vz -= 32;
				hook.Part.vy += 48;
				hook.Part.vx = 48;
				M3dUtils_GetDynamicHookPosition(
						reinterpret_cast<VECTOR*>(&puff), this, &hook);

				puff -= nostril;
				new CShellRhinoNasalSteam(&nostril, &puff);
			}
			break;
		}

		case 308:
			this->DocOckBuildArms();
			break;

		case 309:
		{
			this->SuperOckBuildArms();

			// the electrified crackle comes and goes on a random timer
			if (this->field_23C != 0)
			{
				this->field_23C--;
				if (this->field_238 == 0)
					this->field_238 = new CShellSuperDocOckElectrified(this);
			}
			else
			{
				if (this->field_238 != 0)
				{
					delete this->field_238;
					this->field_238 = 0;
				}

				if (Rnd(200) == 0)
					this->field_23C = Rnd(30) + 40;
			}
			break;
		}

		case 310:
		{
			this->BuildTail();

			switch (this->mAnim)
			{
				case 5:
				case 6:
				case 8:
				case 9:
				case 25:
				case 27:
				case 29:
					this->field_2C8 = 1;
					break;
				default:
					this->field_2C8 = 0;
					break;
			}

			// the sting lands on anim 29 frame 4
			if (this->mAnim == 29 && this->mFrame == 4)
			{
				CVector impact;
				impact.vx = 0;
				impact.vy = 0;
				impact.vz = 0;

				hook.Part.vx = 0;
				hook.Part.vy = 0;
				hook.Part.vz = 0;
				hook.Offset = 23;
				M3dUtils_GetDynamicHookPosition(
						reinterpret_cast<VECTOR*>(&impact), this, &hook);

				impact.vy = this->mPos.vy + 471040;

				new CScorpExplosion(&impact);
			}
			break;
		}

		case 311:
			// Mysterio's head glow rides a hook inside the helmet
			hook.Part.vx = 0;
			hook.Part.vy = -11000;
			hook.Part.vz = -2500;
			hook.Offset = 1;
			M3dUtils_GetDynamicHookPosition(
					reinterpret_cast<VECTOR*>(&this->field_210->mPos), this, &hook);
			break;

		case 312:
		{
			// the henchman: a muzzle flash on the two firing anims, and a spray of sparks off
			// the gun on the reload
			CVector muzzle;
			muzzle.vx = 0;
			muzzle.vy = 0;
			muzzle.vz = 0;

			if (this->mAnim == 8)
			{
				if (this->mFrame == 6)
				{
					hook.Offset = 14;
					hook.Part.vx = 0;
					hook.Part.vy = 1000;
					hook.Part.vz = -160;
					M3dUtils_GetDynamicHookPosition(
							reinterpret_cast<VECTOR*>(&muzzle), this, &hook);

					new CGlowFlash(&muzzle, 6, 255, 255, 255, 0, 255, 128, 0, 0,
							7, 0, 1, 10, 32, 5, 16, 1, 1);
				}

				if (this->mFrame == 15)
				{
					hook.Offset = 9;
					hook.Part.vx = 0;
					hook.Part.vy = 1000;
					hook.Part.vz = -160;
					M3dUtils_GetDynamicHookPosition(
							reinterpret_cast<VECTOR*>(&muzzle), this, &hook);

					new CGlowFlash(&muzzle, 6, 255, 255, 255, 0, 255, 128, 0, 0,
							7, 0, 1, 10, 32, 5, 16, 1, 1);
				}
			}

			if (this->mAnim == 21 && this->mFrame == 30)
			{
				hook.Part.vx = -120;
				hook.Part.vy = 200;
				hook.Part.vz = -400;
				hook.Offset = 13;

				// a unit step 90 degrees off the way he is facing
				CSVector aim;
				aim.vx = this->mAngles.vx;
				aim.vy = static_cast<i16>(this->mAngles.vy + 0x400);
				aim.vz = this->mAngles.vz;

				CVector step;
				step.vx = 0;
				step.vy = 0;
				step.vz = 0;
				Utils_GetVecFromMagDir(&step, -256, &aim);
				step >>= 8;

				M3dUtils_GetDynamicHookPosition(
						reinterpret_cast<VECTOR*>(&muzzle), this, &hook);

				for (i32 spark = 6; spark != 0; spark--)
				{
					CVector vel;
					vel.vx = 0;
					vel.vy = 0;
					vel.vz = 0;

					i32 speed = Rnd(3) + 4;
					vel.vx = step.vx * speed;
					vel.vy = 0;
					vel.vz = speed * step.vz;

					CGLineParticle* pSpark = new CGLineParticle(muzzle, vel, 20, 1);
					pSpark->SetRGB0(48, 96, 48);
					pSpark->SetRGB1(0, 0, 0);
					pSpark->mCodeBGR0 |= 0x2000000;
				}
			}
			break;
		}

		case 313:
		{
			// Venom: electrified for as long as the wrap track is running
			if (this->field_1BC == gDummyVenomWrapTrack)
			{
				if (this->field_200 == 0)
					this->field_200 = new CShellVenomElectrified(this);
			}
			else if (this->field_200 != 0)
			{
				delete this->field_200;
				this->field_200 = 0;
			}

			// the fade track drives the outline by hand instead of through the button toggle
			if (this->field_1BC == gDummyVenomFadeTrack)
			{
				i32 step = this->field_1B8 - this->field_1BC;
				if (step == 0)
				{
					this->field_1FC = 0;
					this->mFlags = static_cast<u16>((this->mFlags & 0xF7FF) | 0x80);
					this->mRGB = 0x202020;
					this->OutlineOff();
				}
				else if (step == 1)
				{
					print_if_false(this->mAnim == 13, "Unexpected anim for venom");
					if (this->mFrame == 45)
						this->FadeAway();
				}
				else if (step == 10)
				{
					print_if_false(this->mAnim == 12, "Unexpected anim for venom");
					if (this->mFrame == 2)
						this->FadeBack();
				}
			}
			else
			{
				this->FadeBack();
			}
			break;
		}

		case 314:
		{
			// Carnage: the tendril bits switch between two sets, and the electrified effect
			// comes and goes on the same random timer monster-Ock uses
			if (this->mAnim == 13)
				this->field_194 = (this->field_194 & 0xFFF99FFF) | 0x22000;
			else
				this->field_194 = (this->field_194 & 0xFFF99FFF) | 0x44000;

			if (this->field_208 != 0)
			{
				this->field_208--;
				if (this->field_204 == 0)
					this->field_204 = new CShellCarnageElectrified(this);
			}
			else
			{
				if (this->field_204 != 0)
				{
					delete this->field_204;
					this->field_204 = 0;
				}

				if (Rnd(200) == 0)
					this->field_208 = Rnd(30) + 40;
			}
			break;
		}

		case 324:
		{
			// the symbiote costume: a slime puddle underneath it until the death track starts,
			// then the fire death effect instead
			if (this->field_1BC == gDummySymbioteDeathTrack && this->field_1F0 == 0)
				this->field_1F0 = new CShellSimbyFireDeath(this);

			if (this->field_1F0 != 0)
			{
				if (this->field_1F0->field_50 != 0)
				{
					delete this->field_1F0;
					this->field_1F0 = 0;
					this->SelectNewTrack(0);
					G_SCONTROL[0].Circle.Triggered = 0;
					G_SCONTROL[0].Square.Triggered = 0;
				}

				if (this->field_1F4 != 0)
				{
					delete this->field_1F4;
					this->field_1F4 = 0;
				}
			}
			else if (this->field_1F4 == 0)
			{
				CVector base;
				base.vx = this->mPos.vx;
				base.vy = this->mPos.vy + 409600;
				base.vz = this->mPos.vz;

				this->field_1F4 = new CShellSimbySlimeBase(&base, &this->mAngles, 256);
				this->field_1F4->mProtected = 1;
			}
			break;
		}

		default:
			break;
	}
}

// @Ok
void CDummy::SelectNewAnim(void)
{
	if (this->field_1B8)
	{
		this->field_1B8++;
		if (*this->field_1B8 == 0xFFFF)
		{
			this->SelectNewTrack(0);
		}
		else
		{
			this->RunAnim(*this->field_1B8, 0, -1);
		}
	}
	else
	{
		this->RunAnim(this->field_1C0, 0, -1);
	}
}

// @Ok
void CDummy::SelectNewTrack(int a2)
{
	this->field_1B8 = 0;
	this->field_1BC = 0;

	if (this->field_1A4 || this->field_1A8 || this->field_1AC)
	{
		do
		{
			switch(Rnd(3))
			{
				case 0:
					this->field_1B8 = this->field_1A4;
					break;
				case 1:
					this->field_1B8 = this->field_1A8;
					break;
				case 2:
					this->field_1B8 = this->field_1AC;
					break;
			}
		}
		while(!this->field_1B8);

		print_if_false(*this->field_1B8 != 0xFFFF, "First anim must not be 0xFFFF");

		if (a2)
		{
			u16 *v7 = this->field_1B8;
			i32 i = 0;
			for (i = 0; *v7 != 0xFFFF; i++)
				v7++;

			i32 v9 = 0;
			i32 v10;
			do
			{
				v10 = Rnd(i);
				v9++;
			}
			while (this->field_1B8[v10] == this->mAnim && v9 < 100);

			if (this->field_1B8[v10] != this->mAnim)
				this->field_1B8 = &this->field_1B8[v10];
		}

		this->field_1BC = this->field_1B8;
		this->RunAnim(*this->field_1BC, 0, -1);
	}
	else
	{
		this->RunAnim(this->field_1C0, 0, -1);
	}
}

// @Ok
void INLINE CDummy::FadeAway(void)
{
	this->field_1F8 = 1;
	this->field_1FC = 0;

	this->mFlags &= 0xFF7F;
	this->mFlags |= 0x800;

	this->mRGB = 0x202020;

	this->OutlineOn();
	this->SetOutlineSemiTransparent();
	this->SetOutlineRGB(0, 0, 0);
}

// @Ok
void INLINE CDummy::FadeBack(void)
{
	this->field_1FC = 1;
	this->field_1F8 = 0;
}

// @Ok
// Verified against the disassembly at 0x48F7B0. The original does the phase
// accumulate (mT[i] += mInc[i]) in full 32-bit, but computes the table index
// from a 16-bit truncated sum of the OLD mT[i] and mInc[i] read BEFORE that
// update. Truncation to 16 bits distributes over addition, so the low 16 bits
// (and therefore the low 12 bits used by the &0xFFF mask below) are identical
// either way; this source (mInc[8+i] aliasing mT[i], updated then masked) is
// functionally equivalent, just a different intermediate type/order.
void INLINE CWobblyGlow::Move(void)
{
	for (u32 i = 0; i < this->mNumSections; i++)
	{
		this->mInc[8+i] += this->mInc[i];
		int v3 = this->mInc[8+i];
		this->mpSections[i].Radius = this->mInnerRadius + this->mAmplitude * word_610C48[2 * (v3 & 0xFFF)] / 4096;
	}
}

// @Ok
void CShellMysterioHeadGlow::Move(void)
{
	CWobblyGlow::Move();
	this->mAngle += this->field_A4;
}

// @Ok
void Spidey_CIcon::AI(void)
{
	this->mAngles.vy += 50;
	if (this->mFlags & 2)
	{
		this->UpdateFrame();
		M3d_BuildTransform(this);
	}
}

// @Ok
// Inlined into its one call site in the original (Shell_InputName, 0x48DC10),
// verified field-by-field against the disassembly there: operator new(428)
// size matches sizeof(CRudeWordHitterSpidey), InitItem("spidey"), mFlags|=0x480,
// mpLight assignment, field_194|=0x420, RunAnim(0,0,-1), mFrame=18, and all
// four mPos/mAngles constants (0xFFF92000, 0x104000, 0x1F4000, 0xFD76) match
// exactly.
CRudeWordHitterSpidey::CRudeWordHitterSpidey(void)
{
	this->InitItem("spidey");
	this->mFlags |= 0x480;

	this->mpLight = &M3d_RudeSpideyLight;

	this->field_194 |= 0x420;

	this->RunAnim(0, 0, -1);

	this->mFrame = 18;
	this->mPos.vx = 0xFFF92000;
	this->mPos.vy = 0x104000;
	this->mPos.vz = 0x1F4000;
	this->mAngles.vy = 0xFD76;
}

// @Ok
void CRudeWordHitterSpidey::AI(void)
{
	this->field_1A8++;
	if (this->field_1A8 > 60)
	{
		this->mPos.vy += 0x14000;
	}
	else
	{
		this->mPos.vy -= 0x14000;
		if (this->mPos.vy < 0x91000)
		{
			this->mPos.vy = 0x91000;
		}
	}

	if (this->mAnimFinished)
	{
		if (!this->mAnim)
		{
			this->RunAnim(0x64, 0, -1);
		}
		else
		{
			this->RunAnim(0, 0, -1);
		}
	}

	this->UpdateFrame();

	if (this->mFrame == 7 && !this->field_1A4)
	{
		switch (Rnd(4))
		{
			case 0:
				SFX_Play(0xE, 0x2000, 0);
				break;
			case 1:
				SFX_Play(0xF, 0x2000, 0);
				break;
			case 2:
				SFX_Play(0x10, 0x2000, 0);
				break;
			case 3:
				SFX_Play(0x11, 0x2000, 0);
				break;
			default:
				break;
		}

		this->field_1A4 = 1;
	}

	M3d_BuildTransform(this);
}


// @Ok
CShellSymBurn::CShellSymBurn(CVector* pVector)
{
	this->mPos = *pVector;
	this->InitItem("fire");
	this->mFlags |= 0x602;
	this->mScale.vy = 0;
	this->mRGB = 0xFFFFFF;
	this->AttachTo(&MiscList);
}

SVECTOR gYAnglesRelated;

// @Ok
// Verified against the disassembly at 0x490650: mAngles.vy/mScale offsets,
// the mRGB 3-byte-repeat pack, the field_1A4 branch (equivalent ++ > 60 vs
// <= 60 with swapped branches), the Die() vtable call and the trailing
// M3d_BuildTransform all match.
void CShellSymBurn::AI(void)
{
	this->mAngles.vy = gYAnglesRelated.vy + 2048;
	this->mScale.vx = 3000;
	this->mScale.vz = 3000;

	if (++this->field_1A4 > 60)
	{
		i32 v3 = (this->mRGB & 0xFF) - 4;
		if (v3 < 0)
			v3 = 0;

		this->mScale.vy -= 75;
		this->mRGB = v3 | ((v3 | (v3 << 8)) << 8);

		if (this->mScale.vy < 0)
			this->mScale.vy = 0;

		if (!v3 || !this->mScale.vy)
		{
			this->Die();
		}
	}
	else
	{
		i32 v5 = (this->mRGB & 0xFF) - 129;
		if (v5 < 128)
			v5 = 128;

		this->mScale.vy += 800;
		this->mRGB = v5 | ((v5 | (v5 << 8)) << 8);

		if (this->mScale.vy > 4096)
			this->mScale.vy = 4096;
	}

	M3d_BuildTransform(this);
}

char *gBadWords[30] =
{
	"sjnkpc",
	"cmpxkpc",
	"bstf",
	"\x62\x74\x74\x00",
	"gvdl",
	"cvhhfs",
	"xbol",
	"\x75\x6A\x75\x00"
	"dvou",
	"tobudi",
	"qvttz",
	"tiju",
	"qjtt",
	"\x64\x76\x6E\x00",
	"\x77\x62\x68\x00",
	"gfmudi",
	"tqvol",
	"\x6B\x6A\x7B\x00",
	"dpdl",
	"gjtujoh",
	"ovutbd",
	"bobm",
	"ejmep",
	"cbtubse",
	"dpdl",
	"cvuu",
	"qfojt",
	"uxbu",
	"cjudi",
};

char *gGoodWords[30] = 
{
	"flower",
	"happy",
	"pretty",
	"puppy",
	"bunny",
	"donut",
	"lolly",
	"love",
	"nice",
	"cake",
	"poppy",
	"fluffy",
	"cloud",
	"rainbow",
	"icecream",
	"sugar",
	"windmill",
	"iowa",
	"toffee",
	"taffy",
	"candy",
	"sodapop",
	"bubble",
	"cinnamon",
	"dinosaur",
	"balloon",
	"lobster",
	"honey",
	"potato",
	"spice",
};

// @Ok
i32 Shell_DeRudify(char inp[INPUT_MAX_SIZE])
{
	char buffer[9];

	for (i32 i = 0; ; i++)
	{
		if (i >= 29)
			return 0;

		Utils_CopyString(gBadWords[i], buffer, 9);
		for (char *j = buffer; *j; j++)
			--*j;

		if (Shell_ContainsSubString(inp, buffer))
			break;
	}

	i32 result = Utils_CopyString(gGoodWords[Rnd(30)], inp, 9);
	for (i32 k = result; k < 8; k++)
		inp[k] = '.';

	return result;

}

// @Ok
// Fixed real bug found this session: the inner-loop haystack read indexed
// from the START of hay (hay[needlePtr-needle]) instead of from the current
// sliding window (hayPtr[needlePtr-needle]), so it only ever compared needle
// against hay[0..len(needle)) no matter where hayPtr had advanced to. Case
// folding and the match-on-full-needle-consumed logic were already correct.
INLINE i32 Shell_ContainsSubString(const char* hay, const char* needle)
{
	for (const char *hayPtr = hay; *hayPtr; hayPtr++)
	{
		const char *needlePtr = needle;
		for (; *needlePtr; needlePtr++)
		{
			char needleChar = *needlePtr;
			char hayChar = hayPtr[needlePtr-needle];

			if (needleChar >= 'A' && needleChar <= 'Z')
				needleChar += ' ';

			if (hayChar >= 'A' && hayChar <= 'Z')
				hayChar += ' ';

			if (hayChar != needleChar)
				break;
		}

		if (!*needlePtr)
			return 1;
	}

	return 0;
}

// @Ok
// @AlmostMatching: no nullsub
void PShell_Cleanup(void)
{
	if ( gShellInitialized )
	{
		if (!gPshellArmorRealted)
		{
			PShell_BigFont();
			Mess_UnloadFont();
		}

		PShell_SmallFont();
		Mess_UnloadFont();

		Shell_VerySmallFont();
		Mess_UnloadFont();

		PShell_NormalFont();

		Spool_ClearPSX("control");
		Spool_ClearPSX("icons");
		Spool_ClearPSX("vmu");
		Spool_ClearPSX("shell");
		SFX_SpoolOutLevelSFX();

		if (gBiographies)
		{
			Mem_Delete(gBiographies);
			gBiographies = 0;
		}

		Pad_ClearAll();
		Front_ClearScreen();

		DrawSync();
		Init_KillAll();
		dword_6A7788[2] = 0;
		dword_6A7788[3] = 0;
		dword_6A7788[4] = 0;

		dword_6A7788[5] = 0;
		dword_6A7788[6] = 0;
		dword_6A7788[7] = 0;
		dword_6A7788[8] = 0;
		dword_6A7788[9] = 0;
		dword_6A7788[10] = 0;
		dword_6A7788[11] = 0;
		dword_6A7788[12] = 0;
		dword_6A7788[13] = 0;
		dword_6A7788[14] = 0;
		dword_6A7788[15] = 0;
		dword_6A7788[1] = 0;
		dword_6A7788[0] = 0;

		//nullsub_3(v0);

		gPShellCleanup = 1;
		OTPushback[0] = 1;

		OTPushback[1] = 1;
		gShellInitialized = 0;
	}
}

// @Ok
// @Matching
void PShell_Initialise(void)
{
	if (gShellFromGame)
		print_if_false(gShellInitialized == 0, "Shell initialised twice, fromgame");
	else
		print_if_false(gShellInitialized == 0, "Shell initialised twice, not fromgame");

	gPShellCleanup = 0;

	Spool_PSX("shell", 0);
	Spool_PSX("icons", 0);
	Spool_PSX("vmu", 0);
	Spool_PSX("control", 0);
	Mess_LoadFont("font_big.fnt", -1, -1, -1);
	Mess_LoadFont("sp_fnt02.fnt", -1, -1, -1);
	Mess_LoadFont("sp_fnt03.fnt", -1, -1, -1);
	SFX_SpoolInLevelSFX("menu");
	PShell_NormalFont();
	Spool_AnimAccess("menubg", &gBackgroundAnimFrame);

	OTPushback[0] = 1;
	OTPushback[1] = -60;

	PShell_MaybeUnlockStuff();
	PCSHELL_Initialize();
	gShellInitialized = 1;
}

// @Ok
// @Matching
INLINE void PShell_LowText(void)
{
	Mess_SetTextJustify(0);
	Mess_SetRGB(0x45u, 0x3Cu, 0x6Bu, 0);
	Mess_SetRGBBottom(0x28u, 35, 62);
}

// @Ok
// gChallenges is SRecordRelated[NUM_CHALLS] (shell.cpp), and field_6/field_8/
// field_9 are the same fields already used the same way at gChallenges[idx]
// lookups elsewhere in this file (search for "field_9 == 2"/"== 3"), so this
// compiles and matches the established field usage. No original address found
// for this function (INLINE, no caller in the current source tree) so it
// cannot be checked against a real disassembly.
INLINE i32 RecordsExist(
		u8 a1,
		i8 a2,
		i8 a3)
{
	i32 v3 = -1;

	for (i32 i = 0; i < NUM_CHALLS; i++)
	{
		if (a1 == gChallenges[i].field_6
				&& a2 == gChallenges[i].field_8
				&& a3 == gChallenges[i].field_9)
		{
			v3 = i;
		}
	}
	print_if_false(v3 != -1, "Mission not found");
	return gGlobalRecords.mScores[5*v3].field_0;
}

// @Ok
INLINE i32 IsBetter(
		i32 a1,
		i32 a2,
		i32 a3)
{
	if (a3)
		return a2 > a1;

	return a1 < a2;
}

// @Ok
// Trivial wrapper: calls the already-verified Merge(SScore*, const SScore*, i32)
// overload once per challenge slot. No original address of its own (INLINE,
// no standalone caller found), so it can only be checked for logical
// correctness, which it has.
INLINE void Merge(SRecords *a1, const SRecords *a2)
{
	for (i32 i = 0; i < NUM_CHALLS; i++)
	{
		Merge(&a1->mScores[5*i],
				&a2->mScores[5*i],
				gChallenges[i].field_C);
	}
}

// @Ok
// @Test
void Merge(
		SScore *a1,
		const SScore *a2,
		i32 a3)
{

	for (i32 i = 0; i < NUM_RECORDS_PER_CHALL; i++)
	{
		i32 v7 = 1;

		for (i32 j = 0; j < NUM_RECORDS_PER_CHALL; j++)
		{
			if (SameScore(&a1[i], &a2[j]))
			{
				v7 = 0;
			}
		}

		if (!a2[i].field_0)
		{
			v7 = 0;
		}

		if (v7)
		{
			i16 v11 = a2[i].field_3 + (a2[i].field_4 << 8);
			for (i32 v10 = 0; v10 < NUM_RECORDS_PER_CHALL; v10++)
			{
				i16 calc = (a1[v10].field_3 + (a1[v10].field_4 << 8));
				if (!a1[v10].field_0 ||
						IsBetter(v11, calc, a3))
				{
					for (i32 k = 4; k > v10; k--)
					{
						memcpy(&a1[k], &a1[k - 1], sizeof(SScore));
					}

					memcpy(&a1[v10], &a2[i], sizeof(SScore));
					break;
				}
			}
			
		}
	}
}

// @Ok
// @NotMatching: weeeeeeird codegen
INLINE i32 SameScore(
		const SScore *a1,
		const SScore *a2)
{
	if ( !a1->field_0 && !a2->field_0)
		return 1;
	return a1->field_0 == a2->field_0
		&& a1->field_1 == a2->field_1
		&& a1->field_2 == a2->field_2
		&& a1->field_3 == a2->field_3
		&& a1->field_4 == a2->field_4;
}

void validate_CRudeWordHitterSpidey(void){
	VALIDATE_SIZE(CRudeWordHitterSpidey, 0x1AC);

	
	VALIDATE(CRudeWordHitterSpidey, field_194, 0x194);
	VALIDATE(CRudeWordHitterSpidey, field_1A4, 0x1A4);
	VALIDATE(CRudeWordHitterSpidey, field_1A8, 0x1A8);
}

void validate_CDummy(void){
	VALIDATE_SIZE(CDummy, 0xA18);

	VALIDATE(CDummy, field_1A4, 0x1A4);
	VALIDATE(CDummy, field_1A8, 0x1A8);
	VALIDATE(CDummy, field_1AC, 0x1AC);
	VALIDATE(CDummy, field_1B0, 0x1B0);
	VALIDATE(CDummy, field_1B4, 0x1B4);
	VALIDATE(CDummy, field_1B8, 0x1B8);
	VALIDATE(CDummy, field_1BC, 0x1BC);
	VALIDATE(CDummy, field_1C0, 0x1C0);

	VALIDATE(CDummy, field_1C4, 0x1C4);
	VALIDATE(CDummy, field_1C8, 0x1C8);

	VALIDATE(CDummy, field_1D0, 0x1D0);
	VALIDATE(CDummy, field_1D4, 0x1D4);
	VALIDATE(CDummy, field_1D8, 0x1D8);

	VALIDATE(CDummy, field_1DC, 0x1DC);

	VALIDATE(CDummy, field_1E0, 0x1E0);
	VALIDATE(CDummy, field_1E4, 0x1E4);
	VALIDATE(CDummy, field_1E8, 0x1E8);
	VALIDATE(CDummy, field_1EC, 0x1EC);
	VALIDATE(CDummy, field_1F0, 0x1F0);
	VALIDATE(CDummy, field_1F4, 0x1F4);

	VALIDATE(CDummy, field_1CC, 0x1CC);
	VALIDATE(CDummy, field_1D9, 0x1D9);
	VALIDATE(CDummy, field_1F8, 0x1F8);
	VALIDATE(CDummy, field_1FC, 0x1FC);

	VALIDATE(CDummy, field_200, 0x200);
	VALIDATE(CDummy, field_204, 0x204);

	VALIDATE(CDummy, field_208, 0x208);
	VALIDATE(CDummy, field_20C, 0x20C);
	VALIDATE(CDummy, field_210, 0x210);
	VALIDATE(CDummy, field_214, 0x214);
	VALIDATE(CDummy, field_224, 0x224);
	VALIDATE(CDummy, field_234, 0x234);
	VALIDATE(CDummy, field_238, 0x238);
	VALIDATE(CDummy, field_23C, 0x23C);

	VALIDATE(CDummy, field_280, 0x280);
	VALIDATE(CDummy, mpTailGeometry, 0x284);
	VALIDATE(CDummy, field_240, 0x240);
	VALIDATE(CDummy, field_288, 0x288);
	VALIDATE(CDummy, field_2C8, 0x2C8);
	VALIDATE(CDummy, field_2CC, 0x2CC);
	VALIDATE(CDummy, field_2D0, 0x2D0);

	VALIDATE(CDummy, field_2D4, 0x2D4);
	VALIDATE(CDummy, field_304, 0x304);
	VALIDATE(CDummy, field_418, 0x418);

	// main.cpp holds the list of validate functions the game calls, and this branch does not
	// touch main.cpp, so the three classes added with CDummy::AI are checked from here.
	validate_CTailRing();
	validate_CScorpExplosion();
	validate_CShellSimbySlimeBase();
}

void validate_CTailRing(void)
{
	VALIDATE_SIZE(CTailRing, 0x124);

	VALIDATE(CTailRing, field_F8, 0xF8);
	VALIDATE(CTailRing, field_100, 0x100);
	VALIDATE(CTailRing, field_104, 0x104);
	VALIDATE(CTailRing, field_108, 0x108);
	VALIDATE(CTailRing, field_10C, 0x10C);
	VALIDATE(CTailRing, field_110, 0x110);
	VALIDATE(CTailRing, field_118, 0x118);
	VALIDATE(CTailRing, field_11C, 0x11C);
	VALIDATE(CTailRing, field_120, 0x120);
}

void validate_CScorpExplosion(void)
{
	VALIDATE_SIZE(CScorpExplosion, 0x4C);

	VALIDATE(CScorpExplosion, field_3C, 0x3C);
	VALIDATE(CScorpExplosion, field_44, 0x44);
}

void validate_CShellSimbySlimeBase(void)
{
	VALIDATE_SIZE(CShellSimbySlimeBase, 0x110);

	VALIDATE(CShellSimbySlimeBase, field_84, 0x84);
	VALIDATE(CShellSimbySlimeBase, field_90, 0x90);
	VALIDATE(CShellSimbySlimeBase, field_9C, 0x9C);
	VALIDATE(CShellSimbySlimeBase, field_B4, 0xB4);
	VALIDATE(CShellSimbySlimeBase, field_B8, 0xB8);
	VALIDATE(CShellSimbySlimeBase, field_BC, 0xBC);
	VALIDATE(CShellSimbySlimeBase, field_C0, 0xC0);
	VALIDATE(CShellSimbySlimeBase, field_F0, 0xF0);
	VALIDATE(CShellSimbySlimeBase, field_100, 0x100);
}


void validate_CDropDownController(void)
{
	VALIDATE_SIZE(CDropDownController, 0x1D8);

	VALIDATE(CDropDownController, mPhase, 0x1A4);
	VALIDATE(CDropDownController, mSpeed, 0x1A8);
	VALIDATE(CDropDownController, mWobblePhase, 0x1AC);
	VALIDATE(CDropDownController, mShakeAmp, 0x1B0);
	VALIDATE(CDropDownController, mShakePhase, 0x1B4);
	VALIDATE(CDropDownController, mShakeFlag, 0x1B8);

	VALIDATE(CDropDownController, mpFrame0, 0x1BC);
	VALIDATE(CDropDownController, mpFrame1, 0x1C0);
	VALIDATE(CDropDownController, mTopAnchor, 0x1C4);
	VALIDATE(CDropDownController, mpWeb, 0x1D0);
	VALIDATE(CDropDownController, mState, 0x1D4);
}

void validate_CShellMysterioHeadGlow(void)
{
	VALIDATE_SIZE(CShellMysterioHeadGlow, 0xA8);

	VALIDATE(CShellMysterioHeadGlow, field_A4, 0xA4);
}


void validate_CWobblyGlow(void)
{
	VALIDATE_SIZE(CWobblyGlow, 0xA4);

	VALIDATE(CWobblyGlow, mInc, 0x5C);
	VALIDATE(CWobblyGlow, mT, 0x7C);

	VALIDATE(CWobblyGlow, mAmplitude, 0x9C);
	VALIDATE(CWobblyGlow, mInnerRadius, 0xA0);
}

void validate_Spidey_CIcon(void)
{
	VALIDATE_SIZE(Spidey_CIcon, 0x1A4);
}

void validate_CShellPreviewIcon(void)
{
	VALIDATE_SIZE(CShellPreviewIcon, 0x1A4);
}

void validate_CShellSymBurn(void)
{
	VALIDATE_SIZE(CShellSymBurn, 0x1A8);

	VALIDATE(CShellSymBurn, field_1A4, 0x1A4);
}

void validate_CShellVenomElectrified(void)
{
	VALIDATE_SIZE(CShellVenomElectrified, 0x48);

	VALIDATE(CShellVenomElectrified, field_3C, 0x3C);
	VALIDATE(CShellVenomElectrified, field_44, 0x44);
}

void validate_CShellCarnageElectrified(void)
{
	VALIDATE_SIZE(CShellVenomElectrified, 0x48);

	VALIDATE(CShellVenomElectrified, field_3C, 0x3C);
	VALIDATE(CShellVenomElectrified, field_44, 0x44);
}

void validate_CShellSuperDocOckElectrified(void)
{
	VALIDATE_SIZE(CShellVenomElectrified, 0x48);

	VALIDATE(CShellVenomElectrified, field_3C, 0x3C);
	VALIDATE(CShellVenomElectrified, field_44, 0x44);
}

void validate_CShellRhinoNasalSteam(void)
{
	VALIDATE_SIZE(CShellRhinoNasalSteam, 0x68);
}

void validate_CShellEmber(void)
{
	VALIDATE_SIZE(CShellEmber, 0x90);

	VALIDATE(CShellEmber, field_68, 0x68);
	VALIDATE(CShellEmber, field_6C, 0x6C);
	VALIDATE(CShellEmber, field_70, 0x70);
	VALIDATE(CShellEmber, field_74, 0x74);
	VALIDATE(CShellEmber, field_78, 0x78);
	VALIDATE(CShellEmber, field_7C, 0x7C);
	VALIDATE(CShellEmber, field_80, 0x80);
	VALIDATE(CShellEmber, field_84, 0x84);
	VALIDATE(CShellEmber, field_88, 0x88);
	VALIDATE(CShellEmber, field_8C, 0x8C);
}

void validate_CShellSimbyMeltSplat(void)
{
	VALIDATE_SIZE(CShellSimbyMeltSplat, 0xB4);

	VALIDATE(CShellSimbyMeltSplat, field_84, 0x84);
	VALIDATE(CShellSimbyMeltSplat, field_88, 0x88);
	VALIDATE(CShellSimbyMeltSplat, field_8C, 0x8C);

	VALIDATE(CShellSimbyMeltSplat, field_90, 0x90);
	VALIDATE(CShellSimbyMeltSplat, field_9C, 0x9C);
	VALIDATE(CShellSimbyMeltSplat, field_A8, 0xA8);
}

void validate_CShellSimbyFireDeath(void)
{
	VALIDATE_SIZE(CShellSimbyFireDeath, 0x54);

	VALIDATE(CShellSimbyFireDeath, field_3C, 0x3C);
	VALIDATE(CShellSimbyFireDeath, field_44, 0x44);
}

void validate_CShellGoldFish(void)
{
	VALIDATE_SIZE(CShellGoldFish, 0x118);

	VALIDATE(CShellGoldFish, field_F8, 0xF8);
	VALIDATE(CShellGoldFish, field_100, 0x100);
	VALIDATE(CShellGoldFish, field_104, 0x104);
	VALIDATE(CShellGoldFish, field_108, 0x108);
	VALIDATE(CShellGoldFish, field_10C, 0x10C);
	VALIDATE(CShellGoldFish, field_110, 0x110);
	VALIDATE(CShellGoldFish, field_114, 0x114);
}

void validate_CShellMysterioHeadCircle(void)
{
	VALIDATE_SIZE(CShellMysterioHeadCircle, 0x94);

	VALIDATE(CShellMysterioHeadCircle, field_84, 0x84);
	VALIDATE(CShellMysterioHeadCircle, field_8C, 0x8C);
	VALIDATE(CShellMysterioHeadCircle, field_90, 0x90);
}

void validate_SCharacterEntry(void)
{
	VALIDATE_SIZE(SCharacterEntry, 0x44);

	VALIDATE(SCharacterEntry, Name, 0x0);
	VALIDATE(SCharacterEntry, ModelName, 0x4);
	VALIDATE(SCharacterEntry, Type, 0x8);
	VALIDATE(SCharacterEntry, Description, 0xC);
	VALIDATE(SCharacterEntry, Zoom, 0x10);
	VALIDATE(SCharacterEntry, MinZoom, 0x14);
	VALIDATE(SCharacterEntry, MaxZoom, 0x18);
	VALIDATE(SCharacterEntry, ZoomStep, 0x1C);
	VALIDATE(SCharacterEntry, PosY, 0x20);
	VALIDATE(SCharacterEntry, DefaultAnim, 0x24);
	VALIDATE(SCharacterEntry, TrackA, 0x28);
	VALIDATE(SCharacterEntry, TrackB, 0x2C);
	VALIDATE(SCharacterEntry, TrackC, 0x30);
	VALIDATE(SCharacterEntry, TrackD, 0x34);
	VALIDATE(SCharacterEntry, TrackE, 0x38);
	VALIDATE(SCharacterEntry, CtorA12, 0x3C);
	VALIDATE(SCharacterEntry, CtorA13, 0x40);
}

void validate_SpideyIconRelated(void)
{
	VALIDATE_SIZE(SpideyIconRelated, 0x28);

	VALIDATE(SpideyIconRelated, Name, 0x0);
	VALIDATE(SpideyIconRelated, IconModel, 0x4);
	VALIDATE(SpideyIconRelated, field_8, 0x8);
	VALIDATE(SpideyIconRelated, field_C, 0xC);
	VALIDATE(SpideyIconRelated, field_10, 0x10);
	VALIDATE(SpideyIconRelated, field_14, 0x14);
	VALIDATE(SpideyIconRelated, field_18, 0x18);
	VALIDATE(SpideyIconRelated, field_1C, 0x1C);
	VALIDATE(SpideyIconRelated, field_20, 0x20);
	VALIDATE(SpideyIconRelated, field_24, 0x24);

	// main.cpp owns the list of validators to run and is not ours to edit, so the character
	// table's check rides along with the other shell.cpp table here.
	validate_SCharacterEntry();
}

void validate_SSaveGame(void)
{
	VALIDATE_SIZE(SSaveGame, 0xBC);

	VALIDATE(SSaveGame, mChecksum, 0x0);
	VALIDATE(SSaveGame, field_4, 0x4);

	VALIDATE(SSaveGame, mRestartPointName, 0xD);

	VALIDATE(SSaveGame, field_3F, 0x3F);

	VALIDATE(SSaveGame, mDifficulty, 0x54);
	VALIDATE(SSaveGame, mCheatStoryboardFlag, 0x55);
	VALIDATE(SSaveGame, field_56, 0x56);
	VALIDATE(SSaveGame, field_78, 0x78);

	VALIDATE(SSaveGame, field_7B, 0x7B);
	VALIDATE(SSaveGame, field_7C, 0x7C);

	VALIDATE(SSaveGame, field_80, 0x80);
	VALIDATE(SSaveGame, field_84, 0x84);
	VALIDATE(SSaveGame, field_88, 0x88);
	VALIDATE(SSaveGame, field_8C, 0x8C);
	VALIDATE(SSaveGame, field_90, 0x90);

	VALIDATE(SSaveGame, field_94, 0x94);
	VALIDATE(SSaveGame, field_98, 0x98);
	VALIDATE(SSaveGame, field_9C, 0x9C);
	VALIDATE(SSaveGame, field_A0, 0xA0);
	VALIDATE(SSaveGame, field_A4, 0xA4);
	VALIDATE(SSaveGame, field_A8, 0xA8);
	VALIDATE(SSaveGame, mDigitalMapping, 0xAC);
	VALIDATE(SSaveGame, mAnalogueMapping, 0xB4);
}

void validate_SScore(void)
{
	VALIDATE_SIZE(SScore, 5);

	VALIDATE(SScore, field_0, 0x0);
	VALIDATE(SScore, field_1, 0x1);
	VALIDATE(SScore, field_2, 0x2);
	VALIDATE(SScore, field_3, 0x3);
	VALIDATE(SScore, field_4, 0x4);
}

void validate_SRecords(void)
{
	VALIDATE_SIZE(SRecords, 0x242);

	VALIDATE(SRecords, mScores, 0x3);
}

void validate_STrainingMission(void)
{
	VALIDATE_SIZE(STrainingMission, 0x10);

	VALIDATE(STrainingMission, field_0, 0x0);
	VALIDATE(STrainingMission, mAreaId, 0x7);
	VALIDATE(STrainingMission, mScoreUnits, 0xB);
	VALIDATE(STrainingMission, mLowerIsBetter, 0xC);
}

void validate_CRecordBox(void)
{
	VALIDATE_SIZE(CRecordBox, 0x44);

	VALIDATE(CRecordBox, field_4, 0x4);
	VALIDATE(CRecordBox, field_8, 0x8);
	VALIDATE(CRecordBox, field_C, 0xC);
	VALIDATE(CRecordBox, field_10, 0x10);
	VALIDATE(CRecordBox, field_14, 0x14);
	VALIDATE(CRecordBox, field_18, 0x18);
	VALIDATE(CRecordBox, field_1C, 0x1C);
	VALIDATE(CRecordBox, field_20, 0x20);
	VALIDATE(CRecordBox, field_24, 0x24);
	VALIDATE(CRecordBox, field_28, 0x28);
	VALIDATE(CRecordBox, field_2C, 0x2C);
	VALIDATE(CRecordBox, field_30, 0x30);
	VALIDATE(CRecordBox, field_34, 0x34);
	VALIDATE(CRecordBox, field_35, 0x35);
	VALIDATE(CRecordBox, field_36, 0x36);
	VALIDATE(CRecordBox, mLetterIndex, 0x38);
	VALIDATE(CRecordBox, field_39, 0x39);
	VALIDATE(CRecordBox, field_3C, 0x3C);
	VALIDATE(CRecordBox, field_40, 0x40);
}

void validate_SRecordRelated(void)
{
	VALIDATE_SIZE(SRecordRelated, 0x10);

	VALIDATE(SRecordRelated, pName, 0x0);
	VALIDATE(SRecordRelated, field_6, 0x6);

	VALIDATE(SRecordRelated, field_8, 0x8);
	VALIDATE(SRecordRelated, field_9, 0x9);

	VALIDATE(SRecordRelated, field_C, 0xC);
}
