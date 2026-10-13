#include "game/application/init.h"
#include "core/game_heap.h"
#include "engine/rendering/PCGfx.h"
#include "engine/assets/dcfileio.h"
#include "engine/persistence/dcmemcard.h"
#include "game/application/tweak.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/assets/pack.h"
#include "engine/audio/ps2redbook.h"
#include "engine/rendering/m3dinit.h"
#include "engine/assets/spool.h"
#include "engine/input/ps2pad.h"
#include "game/cinematics/ps2gamefmv.h"
#include "engine/rendering/pal.h"
#include "engine/persistence/ps2card.h"
#include "game/ui/front.h"
#include "game/ui/mess.h"
#include "game/ui/FontTools.h"
#include "engine/world/trig.h"
#include "engine/world/bit.h"
#include "core/utils.h"
#include "game/ui/panel.h"
#include "game/player/spidey.h"
#include "game/application/DXinit.h"
#include "engine/rendering/render_buffers.h"
#include "game/ui/shell.h"
#include "game/player/web.h"
#include "game/cinematics/camera.h"
#include "game/actors/baddy.h"
#include "game/objects/powerup.h"
#include "engine/rendering/backgrnd.h"
#include "game/objects/bullet.h"
#include "game/player/weapons.h"
#include "game/effects/flash.h"
#include "engine/world/physics.h"
#include "engine/world/m3dzone.h"
#include "engine/assets/reloc.h"
#include "game/application/SpideyDX.h"

#ifndef SPIDEY_STANDALONE
EXPORT Texture* gShadowBit;
#else
extern Texture* gShadowBit;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT i32 gPsxSpArmorIndex;
#else
extern i32 gPsxSpArmorIndex;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT i32 gPsxRingIndex;
#else
extern i32 gPsxRingIndex;
#endif

EXPORT i32 gInitRelatedOne;
#ifndef SPIDEY_STANDALONE
EXPORT i32 gInitRelatedTwo;
#else
extern i32 gInitRelatedTwo;
#endif
EXPORT i32 gInitRelatedThree;
#ifndef SPIDEY_STANDALONE
EXPORT i32 gSpideyMainRelated;
#else
extern i32 gSpideyMainRelated;
#endif

// @Ok
// can't get too match, because they wrote in an obtuse way
// can get closer with for loop but don't think it's worth it
 void DeleteList(CBody *pLst)
{
	CBody* pCur = pLst;

	while (pCur)
	{
		CBody *pNext = reinterpret_cast<CBody*>(pCur->mNextItem);

		// 0x20 is the "protected" bit: Init_KillAll (0x4437E0) inlines this
		// loop eleven times and every copy deletes the member only when
		// `(mCBodyFlags & 0x20) == 0`. An earlier revision had the test
		// inverted, so a level restart kept the old CPlayer and every
		// unprotected object alive ("Leftover protected members", then
		// "2 or more CPlayers" and a crash in DoMGSShadow); found 2026-09-03.
		if (!(pCur->mCBodyFlags & 0x20))
		{
			delete pCur;
		}

		pCur = pNext;
	}
}

// @Ok
void Init_AtEnd(void)
{
	DXERR_printf("\nStart of Init_AtEnd\n");
	Spool_Sync();
	DXERR_printf("1,");

	DrawSync();

	DXERR_printf("2,");
	Pause(1);

	DoVblankProcessing = 0;
	SFX_Off();
	DXERR_printf("3,");
	Card_Stop();
	Pause(1);
	DXERR_printf("6,");

	SetDispMask();
	DXERR_printf("7,");
	PCGfx_Exit();
	Port_Exit();
	DXERR_printf("13a,");
	DXERR_printf("14,");
	DXERR_printf("17,");
	DXERR_printf("\nEnd of Init_AtEnd\n");
}

EXPORT i32 dword_60D218;

// @Ok
u8 Init_AtStart(i32)
{
	Port_InitAtStart();
	*reinterpret_cast<u8**>(0x00628618) = reinterpret_cast<u8*>(0x00628690);
	*reinterpret_cast<u8**>(0x00654F54) = reinterpret_cast<u8*>(0x0062A5D0);
	HeapDefs[0][0] = gPortRelatedOne;
	HeapDefs[0][1] = gPortRelatedOne + 64;
	HeapDefs[1][0] = gPortRelatedOne + 64;
	HeapDefs[1][1] = gPortRelatedOne + gSomeSize;

	Mem_Init();

	SetDispMask();

	Db_Init();
	// Db_Init allocates the persistent render buffers from this heap.
	// Resetting it here lets later assets reuse those live allocations.

	ClearImage2();
	DrawSync();
	SetDispMask();

	PCGfx_InitAtStart();

	FileIO_Init();
	Card_Init(1);
	Pad_InitAtStart();

	Spool_Init();

	gSpideyMainRelated = 20;
	SFX_InitAtStart();
	GameFMV_Init();
	M3dInit_InitAtStart();
	Redbook_XAInitAtStart();
	Tweak_Init();

	Pack_Init();
	Pal_Init();

	Mess_LoadFont("sp_fnt00.fnt", -1, -1, -1);
	Mess_LoadFont("sp_fnt01.fnt", -1, -1, -1);
	Front_Init();
	FontManager::ResetCharMaps();
	SFX_Init("spidey.kat");
	G_ISRESTARTDEATH = 0;

	PutDispEnv();

	Bit_Init();
	i32 v5 = G_VBLANKS;

	DrawSync();
	PSXRegion[Spool_PSX("webdome2", 0)].Protected = 1;
	PSXRegion[Spool_PSX("webdome3", 0)].Protected = 1;
	gInitRelatedThree = 0;

	i32 v8 = Spool_PSX("spidey", 0);
	gSpideyPsxIndex = v8;
	PSXRegion[v8].Protected = 1;
	Spidey_CopyHeadModel(v8);
	PSXRegion[Spool_PSX("bits", 0)].Protected = 1;
	Utils_InitLoadIcons();

	gPsxSpArmorIndex = Spool_PSX("spArmor", 0);
	PSXRegion[gPsxSpArmorIndex].Protected = 1;
	PSXRegion[Spool_PSX("shell_dc", 0)].Protected = 1;
	PSXRegion[Spool_PSX("costarm", 0)].Protected = 1;

	i32 v9 = Spool_PSX("items", 0);
	gPsxItemsIndex = v9;
	PSXRegion[v9].Protected = 1;
	gPsxRingIndex = Spool_PSX("ring", 0);

	PSXRegion[gPsxRingIndex].Protected = 1;
	if (!gLowGraphics)
		PSXRegion[Spool_PSX("sparmour", 0)].Protected = 1;
	Spool_Sync();

	G_VBLANKS = v5 + 300;

	gInitRelatedTwo = 5;
	gInitRelatedOne = 5;

	Panel_Init();
	Spool_TextureAccess(0xB79DD55B, &gShadowBit);
	print_if_false(gShadowBit != 0, "No shadow bit");

	gLevelStatus = 0;
	return 0;
}

EXPORT i32 gMovieIndex = 0xFFFFFFFF;
#ifndef SPIDEY_STANDALONE
EXPORT i32 gInitBaddyRelated;
#else
extern i32 gInitBaddyRelated;
#endif

// @Ok
void Init_Cleanup(i32 a1)
{
	Pad_ActuatorOff(0, 0);
	Pad_ActuatorOff(0, 1);

	SFX_LevelStart();

	G_VIEWPORT.field_E = 256;
	Redbook_XAExit();
	GameFMV_StopFMV();
	Init_KillAll();
	Trig_DeleteCommandPoints();

	// 0x443B0F: `mov ecx,[pYesNoMenu]; test; je; call 0x43FB00`, and
	// 0x43FB00 is CMenu::KillBox (delete the zoom box at +4, null it), NOT a
	// delete of the menu: the yes/no menu lives from Init_AtStart until the
	// virtual delete at the end of SpideyMain. An earlier revision wrote
	// `delete pYesNoMenu;` here, which freed the menu after the intro
	// movies; the block got reused (a font glyph image), freed and
	// scribbled, and the next Init_Cleanup (level restart) crashed reading
	// the stale object (found 2026-09-03 in the standalone build).
	if (pYesNoMenu)
		pYesNoMenu->KillBox();

	if (a1 == 3)
	{
		for (i32 i = 0; i < MAXPSX; i++)
		{
			PSXRegion[i].Protected = 0;
		}
		Spidey_FreeHeadModel();

		a1 = 0;
	}

	if (a1)
	{
		if (a1 == 1)
		{
			Spool_ClearEnvironmentRegions();
			M3dZone_Init();
		}
	}
	else
	{
		SFX_SpoolOutLevelSFX();
		SFX_ReverbOff();
		Spool_ClearAllPSXs();
		Trig_DeleteTrigFile();

		G_SAVE_GAME.mRestartPointName[0] = 0;
		Reloc_UnloadAll();
		M3dZone_Init();
	}


	Trig_ClearTrigMenu();
	Flash_Reset();
	Panel_Init();

	CVector v6;
	v6.vx = 0;
	v6.vy = 0x8000;
	v6.vz = 0;
	Physics_SetGravity(&v6);
	TTime = 0;
	Pad_ClearAll();

	gInitBaddyRelated = 0;
	SuspendedDistance = 13000;
	Levels[35].field_C = 0xFFEC;

	if ( gMovieIndex != -1 )
		GameFMV_PlayMovie(gMovieIndex, 1, 1, 1.0);
		gMovieIndex = -1;
	Redbook_XAInit();
}

// @Ok
void Init_KillAll(void)
{
	DeleteList(G_MECHLIST_PLAYER);
	DeleteList(G_CAMERA_LIST);
	DeleteList(BaddyList);
	DeleteList(ControlBaddyList);
	DeleteList(G_ENVIRONMENTAL_OBJECT_LIST);
	DeleteList(BackgroundList);
	DeleteList(PowerUpList);
	DeleteList(BulletList);
	DeleteList(SuspendedList);

	DeleteList(MiscList);
	DeleteList(WebList);

	print_if_false(G_MECHLIST_PLAYER == 0, "Leftover protected members of G_MECHLIST_PLAYER");
	print_if_false(G_CAMERA_LIST == 0, "Leftover protected members of G_CAMERA_LIST");
	print_if_false(BaddyList == 0, "Leftover protected members of BaddyList");
	print_if_false(ControlBaddyList == 0, "Leftover protected members of ControlBaddyList");
	print_if_false(BackgroundList == 0, "Leftover protected members of BackgroundList");
	print_if_false(G_ENVIRONMENTAL_OBJECT_LIST == 0, "Leftover protected members of G_ENVIRONMENTAL_OBJECT_LIST");
	print_if_false(PowerUpList == 0, "Leftover protected members of PowerUpList");
	print_if_false(BulletList == 0, "Leftover protected members of BulletList");
	print_if_false(SuspendedList == 0, "Leftover protected members of SuspendedList");
	print_if_false(MiscList == 0, "Leftover protected members of MiscList");
	print_if_false(WebList == 0, "Leftover protected members of WebList");
	print_if_false(SpideyAdditionalBodyPartsList == 0, "Leftover protected members of SpideyAdditionalBodyPartsList");
	print_if_false(MiscellaneousRenderingList == 0, "Leftover protected members of MiscellaneousRenderingList");
	print_if_false(CWeapons == 0, "Left over CWeapons");

	Bit_DeleteAll();
	Mess_DeleteAll();
}
