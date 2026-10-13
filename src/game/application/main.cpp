#include "compatibility/non_win32.h"
#ifdef SPIDEY_STANDALONE
#include "platform/plat.h"
#endif

// #define BOOT_GAME
#define MODEL_PREVIEW

// dev-only: skip the CD-ROM disc check so the game runs under Wine without
// a real mixed-mode disc. Off by default. Never uncomment this on a branch
// meant for an upstream PR.
// #define SPIDEY_NO_CD_CHECK

// the standalone build always runs the real game path
#ifdef SPIDEY_STANDALONE
#undef MODEL_PREVIEW
#undef BOOT_GAME
#endif

#include <stdlib.h>

// #define LOCK_VALIDATION

#include "game/application/main.h"
#include "game/application/game_flow.h"
#include "game/application/gameplay_renderer.h"
#include "compatibility/my_assert.h"
#include "engine/world/ob.h"
#include "core/fixed_vector.h"
#include "core/friction.h"
#include "engine/world/bit.h"
#include "game/ui/front.h"
#include "game/ui/pshell.h"
#include "game/actors/baddy.h"
#include "game/actors/mj.h"
#include "game/actors/submarin.h"
#include "game/actors/venom.h"
#include "engine/rendering/ps2funcs.h"
#include "game/actors/blackcat.h"
#include "game/actors/torch.h"
#include "game/actors/hostage.h"
#include "game/actors/cop.h"
#include "game/actors/carnage.h"
#include "game/actors/chopper.h"
#include "game/actors/docock.h"
#include "game/actors/jonah.h"
#include "game/actors/lizard.h"
#include "game/actors/lizman.h"
#include "game/actors/mysterio.h"
#include "game/objects/platform.h"
#include "game/actors/rhino.h"
#include "game/actors/scorpion.h"
#include "game/actors/simby.h"
#include "game/actors/spclone.h"
#include "game/actors/superock.h"
#include "game/actors/thug.h"
#include "game/objects/turret.h"
#include "game/ui/shell.h"
#include "game/player/web.h"
#include "engine/world/bit2.h"
#include "game/cinematics/camera.h"
#include "core/quaternion.h"
#include "core/game_heap.h"
#include "game/effects/exp.h"
#include "engine/world/collision.h"
#include "engine/rendering/m3dinit.h"
#include "game/player/spidey.h"
#include "game/ui/message.h"
#include "game/objects/bullet.h"
#include "engine/world/trig.h"
#include "game/effects/effects.h"
#include "game/ui/FontTools.h"
#include "game/objects/wire.h"
#include "game/objects/powerup.h"
#include "game/objects/switch.h"
#include "game/objects/chain.h"
#include "engine/rendering/Image.h"
#include "engine/input/ps2pad.h"
#include "engine/rendering/bitmap256.h"
#include "engine/rendering/PCTex.h"
#include "game/effects/smoke.h"
#include "game/ui/panel.h"
#include "game/objects/manipob.h"
#include "game/ui/mess.h"
#include "engine/world/ai.h"
#include <cstring>
#include "engine/assets/spool.h"
#include "game/objects/level_one_bomb.h"
#include "engine/assets/chunk.h"
#include "game/player/weapons.h"
#include "engine/rendering/backgrnd.h"
#include "game/ui/dcshellutils.h"
#include "engine/assets/pkr.h"
#include "engine/assets/pcdcFile.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/input/PCInput.h"
#include "game/ui/PCShell.h"
#include "compatibility/stubs.h"
#include "game/application/SpideyDX.h"
#include "compatibility/directx/DXsound.h"
#include "game/application/DXinit.h"
#include "engine/assets/pack.h"
#include "engine/rendering/pal.h"
#include "engine/rendering/render_buffers.h"
#include "engine/rendering/scene_renderer.h"
#include "engine/rendering/PCGfx.h"
#include "game/cinematics/ps2gamefmv.h"
#include "game/application/init.h"
#include "core/utils.h"
#include "engine/assets/reloc.h"
#include "compatibility/my_bink.h"
#include "engine/assets/pcdcMem.h"
#include "engine/persistence/dcmemcard.h"
#include "engine/persistence/ps2card.h"
#include "engine/persistence/save_files.h"
#include "engine/input/pcdcPad.h"
#include "engine/rendering/vram.h"
#include "engine/world/m3dzone.h"
#include "engine/assets/PRE.h"
#include "engine/assets/dcfileio.h"
#include "game/cinematics/PCMovie.h"
#include "game/effects/flash.h"
#include "game/ui/screen.h"
#include "engine/rendering/post.h"
#include "game/application/PCTimer.h"
#include "engine/audio/music.h"
#include "game/effects/shatter.h"
#include "game/application/tweak.h"
#include "engine/audio/ps2redbook.h"
#include "game/ui/rfront.h"


#include "compatibility/my_patch.h"

extern int FAIL_VALIDATION;

const i32 POLYBUFFERSIZE = 0x17000;

// @Ok
// @Matching
void CalcPolyBufferEnd(void)
{
	G_POLY_BUFFER_END = reinterpret_cast<u8*>(
			(reinterpret_cast<u32>(G_PDOUBLE_BUFFER->Polys) + POLYBUFFERSIZE - 0x100) & 0x7FFFFFFF);
}

// Original display export; gameplay composition lives in GameplayRenderer.
void Display(void) {
    spidey::application::GameplayRenderer renderer;
    renderer.render();
}

// Original executable entry point; application transitions live in GameFlow.
void SpideyMain(void) {
#ifdef MODEL_PREVIEW
    const bool forceModelPreview = true;
#else
    const bool forceModelPreview = false;
#endif
    spidey::application::runGameFlow(forceModelPreview);
}

// @Ok
// @Matching
// @Leak
void* CClass::operator new(size_t size)
{
	void *pnew = Mem_New(size);

	// Ensure size is a multiple of 4.
	size = ( size + 3 ) & ~0x03;

	// Zero all the newly allocated memory
	u32 *p=(u32 *)pnew;
	for (i32 i=0; i<size/4; ++i) *p++=0;

	return pnew;
}

// @Ok
void CClass::operator delete(void *ptr)
{
	Mem_Delete(ptr);
}

// @Ok
CClass::~CClass()
{
}

template<bool b>

struct StaticAssert{};



template<>

struct StaticAssert<true>

{

	static void assert() {}
};

// @Bogus
void compile_time_assertions(){
	StaticAssert<sizeof(CVector)==12>::assert();
	StaticAssert<sizeof(CFriction)==3>::assert();
	//StaticAssert<sizeof(CBit) == 0x38>::assert();

	//StaticAssert<sizeof(CMenu)==0x53C>::assert();

	//StaticAssert<sizeof(CExpandingBox)==52>::assert();

	StaticAssert<sizeof(CSVector)==6>::assert();

	StaticAssert<sizeof(SVector)==6>::assert();

	StaticAssert<sizeof(CQuadBit)==0x84>::assert();

	//StaticAssert<sizeof(CMJ)==0x324>::assert();

	StaticAssert<sizeof(MATRIX)==0x20>::assert();

	StaticAssert<sizeof(u32)==4>::assert();
	StaticAssert<sizeof(u16)==2>::assert();
	StaticAssert<sizeof(u8)==1>::assert();

	StaticAssert<sizeof(i32)==4>::assert();
	StaticAssert<sizeof(i16)==2>::assert();
	StaticAssert<sizeof(i8)==1>::assert();
}

// @Bogus
extern "C" EXPORT int run_assertions(void)
{
	puts("[*] Starting validation");



	validate_CItem();
	validate_CVector();
	validate_CSVector();
	validate_CBit();
	validate_CQuadBit();
	validate_CFT4Bit();
	validate_CFlatBit();
	validate_CBody();
	validate_SVector();
	validate_CSuper();
	validate_CBaddy();
	validate_CMJ();
	validate_CSubmariner();
	validate_CVenom();
	validate_CBlackCat();
	validate_CTorch();
	validate_CHostage();
	validate_CScriptOnlyBaddy();
	validate_CCop();
	validate_CCarnage();
	validate_CChopper();
	validate_CDocOc();
	validate_CJonah();
	validate_CLizard();
	validate_CLizMan();
	validate_CMystFoot();
	validate_CMysterio();
	validate_CSoftSpot();
	validate_CPlatform();
	validate_CRhino();
	validate_CScorpion();
	validate_CPunchOb();
	validate_CSimbyDrop();
	validate_CSimby();
	validate_CSimbyBase();
	validate_CSpClone();
	validate_CSuperDocOck();
	validate_CThug();
	validate_CTurret();
	validate_MATRIX();
	validate_SMatrix();
	validate_SJoint();
	validate_CRudeWordHitterSpidey();
	validate_CBulletFrag();
	validate_CImpactWeb();
	validate_CKnottedWebSplat();
	validate_CDomePiece();
	validate_CDome();
	validate_CDomeRing();
	validate_CWeb();
	validate_CSwinger();
	validate_CTurretBase();
	validate_CDummy();
	validate_CDropDownController();
	validate_CSniperSplat();
	validate_SStateFlags();
	validate_CGPolyLine();
	validate_CKnottedWeb();
	validate_SKnottedWebSeg();
	validate_CCamera();
	validate_CQuat();
	validate_SBlockHeader();
	validate_SHandle();
	validate_CItemFrag();
	validate_SLineInfo();
	validate_STexWibItemInfo();
	validate_CPlayer();
	validate_CSmokeTrail();
	validate_CMessage();
	validate_CTrapWebEffect();
	validate_CWebFrag();
	validate_CMenu();
	validate_SEntry();
	validate_CBullet();
	validate_SLinkInfo();
	validate_CElectrify();
	validate_CSimbySlimeBase();
	validate_CMysterioLaser();
	validate_Font();
	validate_CTurretLaser();
	validate_CLaserFence();
	validate_CGoldFish();
	validate_CPowerUp();
	validate_CSwitch();
	validate_CChain();
	validate_CGLine();
	validate_SlicedImage2();
	validate_Image();
	validate_SControl();
	validate_Bitmap256();
	validate_SPCTexture();
	validate_CPolyLine();
	validate_CSonicBubble();
	validate_CGlow();
	validate_CLinked2EndedBit();
	validate_CRibbonBit();
	validate_CSniperTarget();
	validate_CVenomWrap();
	validate_CSmokeJet();
	validate_CTexturedRibbon();
	validate_CDomeShockWave();
	validate_CMysterioHeadCircle();
	validate_SAnimFrame();
	validate_CFadePalettes();
	validate_CSimpleTexturedRibbon();
	validate_CManipOb();
	validate_SimpleMessage();
	validate_CShellMysterioHeadGlow();
	validate_CWobblyGlow();
	validate_CSimpleAnim();
	validate_CCopPing();
	validate_SHook();
	validate_Spidey_CIcon();
	validate_CShellPreviewIcon();
	validate_CEmber();
	validate_CThugPing();
	validate_CAIProc();
	validate_CAIProc_LookAt();
	validate_Texture();
	validate_CRhinoNasalSteam();
	validate_CAIProc_RotY();
	validate_CAIProc_Fall();
	validate_CAIProc_StateSwitchSendMessage();
	validate_CAIProc_MonitorAttack();
	validate_CAIProc_AccZ();
	validate_SMoveToInfo();
	validate_CAIProc_MoveTo();
	validate_CNonRenderedBit();
	validate_SPSXRegion();
	validate_CSimbyShot();
	validate_CVenomElectrified();
	validate_CCarnageElectrified();
	validate_CConstantLaser();
	validate_CShellSymBurn();
	validate_CExpandingBox();
	validate_CL1A3Bomb();
	validate_CMotionBlur();
	validate_SHitInfo();
	validate_SCommandPoint();
	validate_PendingListEntry();
	validate_CSpecialDisplay();
	validate_CSkidMark();
	validate_TextureEntry();
	validate_CShellVenomElectrified();
	validate_CSkinGoo();
	validate_SSkinGooSource();
	validate_SSkinGooSource2();
	validate_SSkinGooParams();
	validate_CShellCarnageElectrified();
	validate_CShellSuperDocOckElectrified();
	validate_CShellRhinoNasalSteam();
	validate_CShellEmber();
	validate_CShellSimbyMeltSplat();
	validate_CShellSimbyFireDeath();
	validate_CShellGoldFish();
	validate_CShellMysterioHeadCircle();
	validate_SpideyIconRelated();
	validate_CGlowFlash();
	validate_SChainData();
	validate_CSearchlight();
	validate_SFlatBitVelocity();
	validate_CMachineGunBullet();
	validate_CChopperMissile();
	validate_CChunkControl();
	validate_SChunkEntry();
	validate_CGouraudRibbon();
	validate_CCopBulletTracer();
	validate_CCombatImpactRing();
	validate_SCamera();
	validate_SRibbonPoint();
	validate_CRhinoWallImpact();
	validate_CFootprint();
	validate_CChunkSmoke();
	validate_CBouncingRock();
	validate_CFlameExplosion();
	validate_CFrag();
	validate_CPixel();
	validate_CFireySpark();
	validate_CSimbyDroplet();
	validate_CSymBurn();
	validate_CBackground();
	validate_CAngrySpark();
	validate_CBitServer();
	validate_CCarnageHitSpark();
	validate_CChunkBit();
	validate_CShatterBit();
	validate_CTextBox();
	validate_CCopLaserPing();
	validate_CDamagedSoftSpotEffect();
	validate_CElectro();
	validate_CElectroLine();
	validate_CVertexWobble();
	validate_CFireyExplosion();
	validate_CFlamingImpactWeb();
	validate_CTripWire();
	validate_CSmokeRing();
	validate_CTexturedRibbon();
	validate_SLineSeg();
	validate_CWibbly();
	validate_SSmokeRingRelated();
	validate_Sprite2();
	validate_SBitServerEntry();
	validate_PKR_FILEINFO();
	validate_PKR_FOOTER();
	validate_PKR_DIRINFO();
	validate_LIBPKR_HANDLE();
	validate_NODE_DIRINFO();
	validate_PVRHeader();
	validate_ClutPC();
	validate_PKR_HEADER();
	validate_SGDOpenFile();
	validate_NODE_FILEINFO();
	validate_SSFXBank();
	validate_SMapping();
	validate_SActionMap();
	validate_SSaveGame();
	validate_MEMORY_ALLOC();
	validate_SMessageProg();
	validate_SLevel();
	validate_SMessage();
	validate_DXsound();
	validate_DXContext();
	validate_DXContextEntry();
	validate_SVideoMode();
	validate_DXVideoModeContext();
	validate_DxZBufferContext();
	validate_DXPOLY();
	validate_SFontEntry();
	validate_SDataGlyph();
	validate_POLY_FT4();
	validate_POLY_GT4();
	validate_SPack();
	validate_tag_S_Pal();
	validate_SViewport();
	validate_SDoubleBuffer();
	validate_SDXPolyField();
	validate_SPCTexPixelFormat();
	validate_SPCTexContainer();
	validate_SAccess();
	validate_AnimPacket();
	validate_SCalcBuffer();
	validate_SCheat();
	validate_SButton();
	validate_DDPIXELFORMAT();
	validate_ConvertPSXPaletteToPC();
	validate_BmpHeader();
	validate_Load8BitBMP2();
	validate_CWibbling3DExplosion();
	validate_C3DExplosion();
	validate_CGrenadeWave();
	validate_CGrenadeExplosion();
	validate_CRipple();
	validate_SSection();
	validate_SFringeQuad();
	validate_SModel();
	validate_SMessageData();
	validate_SSfxEntry();
	validate_reloc_mod();
	validate_SReloc();
	validate_SRelocEntry();
	validate_SMovieDetails();
	validate_BINKSUMMARY();
	validate_BINK();
	validate_matrix4x4();
	validate_vector3d();
	validate_vector4d();
	validate_SScore();
	validate_SRecords();
	validate_STrainingMission();
	validate_SRecordRelated();
	validate_CRecordBox();
	validate_SDCCardTime();
	validate_SCardHead();
	validate_SBackupFile();
	validate_SSaveFile();
	validate_SDCCardFullTime();
	validate_SPdPadBig();
	validate_SPdPadSmall();
	validate_tagSVRAMRect();
	validate_SZone();
	validate_DCSkaterModel();
	validate_DCMaterial();
	validate_DCObject();
	validate_DCStrip();
	validate_DCObjectList();
	validate_DCKeyFrame();
	validate_DCModelData();
	validate_PREManager();
	validate_CSonicRipple();
	validate_Vector();
	validate_SRhinoData();
	validate_SLight();
	validate_CManipObChunk();
	validate_DB_RECT();
	validate_DR_ENV();
	validate_DRAWENV();
	validate_DISPENV();
	validate_SSfxRelated();
	validate_SSfxAsset();
	validate_CRibbon();
	validate_CGlassBit();
	validate_CSmokeGenerator();
	validate_SDXSoundHolder();
	validate_SDxSomething();
	validate_DSBUFFERDESC();
	validate_TwiddleStuff();
	validate_CSmokePuff();
	validate_SRibbonTexture();
	validate_SSimpleRibbonParams();
	validate_CSpark();
	validate_SIndicator();
	validate_POLY_F3();
	validate_CVenomHitSpark();
	validate_SPushOffset();
	validate_SLink();

	puts("[*] Validation done!");

    return FAIL_VALIDATION;
}

// @Bogus
void runtime_assertions()
{
	int result = run_assertions();

	while(result)
		;
}

// @Bogus
void *my_malloc(size_t s)
{
	void* res = malloc(s);

	return res;
}

// @Bogus
void my_free(void* block)
{
	free(block);
}

// @Bogus
int my_atexit(
   void (MY_CDECL *func )( void )
)
{
	return atexit(func);
}

// @Bogus
void *my_realloc(void *m, size_t s)
{
	void* res = realloc(m, s);

	return res;
}

#ifdef _WIN32
// @Bogus
_onexit_t my_onexit(
   _onexit_t function
)
{
	return _onexit(function);
}
#endif

// @Bogus
void *my_new(size_t s)
{
	void* res = ::operator new(s);
	return res;
}

// @Bogus
void *my_calloc(size_t a, size_t b)
{
	return calloc(a, b);
}

// @Bogus
void patch_alloc(void)
{
	PATCH_PUSH_RET(0x0052A227, my_malloc);
	PATCH_PUSH_RET(0x0052A3C0, my_free);
	PATCH_PUSH_RET(0x00529C39, my_atexit);

	PATCH_PUSH_RET(0x0052F250, my_realloc);
#ifdef _WIN32
	PATCH_PUSH_RET(0x00529BBB, my_onexit);
#endif
	PATCH_PUSH_RET(0x00529BA2, my_new);

	PATCH_PUSH_RET(0x0052C044, my_calloc);
}

// @Bogus
static int my_video_player(const char*, i32)
{
	return 1;
}

// @Bogus
void game_patches(void)
{
	//PATCH_CALL(0x004707BE, my_video_player);

#ifdef SPIDEY_NO_CD_CHECK
	// dev-only: skip the CD-ROM disc check (WinMain check and the periodic
	// recheck both call this helper) so the game runs without a real
	// mixed-mode disc under Wine. Never merge this into an upstream PR,
	// stays on our fork's main branch only, off unless SPIDEY_NO_CD_CHECK
	// is defined at build time.
	*(unsigned char*)0x005163E0 = 0x32; // xor al,al
	*(unsigned char*)0x005163E1 = 0xC0;
	*(unsigned char*)0x005163E2 = 0xC3; // ret
#endif

	patch_alloc();

	patch_mem();
	patch_utils();
	patch_ps2funcs();

	patch_pkr();
	patch_pcdcMem();
	patch_pack();
	patch_vram();

	patch_CItem();
	patch_CBody();

	patch_spool();
	patch_trig();
	patch_pctex();
	patch_dcfileio();
	patch_PCMovie();

	patch_flash();
	patch_pshell();
	patch_FontTools();
	patch_mess();
	patch_m3dcolij();
	patch_CSuper();
	patch_ps2m3d();
	patch_m3dutils();
	patch_CBit();
	patch_CFT4Bit();

	patch_camera();
	patch_exp();
	patch_chopper();
	patch_carnage();
	patch_effects();
	patch_manipob();
	patch_vector();
	patch_bit();
	patch_bit2();
	patch_spidey();
	patch_baddy();
	patch_thug();
	patch_cop();
	patch_simby();
	patch_rhino();
	patch_scorpion();
	patch_web();
	patch_venom();
	patch_panel();
	patch_screen();
	patch_ai();
	patch_weapons();
	patch_docock();
	patch_superock();
	patch_lizman();
	patch_blackcat();
	patch_jonah();
	patch_hostage();
	patch_spclone();
	patch_switch();
	patch_torch();
	patch_powerup();
	patch_turret();
	patch_wire();
}

// @Bogus
void runtime_patches(void)
{
#ifdef _WIN32
	LPVOID text_start = (void*)0x00401000;

	SIZE_T text_size = 0x0053B000 - (int)text_start;

	DWORD text_protect;
	VirtualProtect(text_start, text_size, PAGE_EXECUTE_READWRITE, &text_protect);

	game_patches();

	DWORD t;
	VirtualProtect(text_start, text_size, text_protect, &t);
#endif
}

#include "game/application/runtime_version.h"

#ifndef RUNTIME_VERSION
#define RUNTIME_VERSION "LOCAL"

#endif

#ifndef _WIN32

// the standalone build has its own main in platform/main_standalone.cpp
#ifndef SPIDEY_STANDALONE
int main()
{
	compile_time_assertions();
	return run_assertions();
}
#endif


#else

HMODULE bink_dll;

BOOL WINAPI DllMain(
    HINSTANCE hinstDLL,
    DWORD fdwReason,
    LPVOID lpvReserved )
{
	compile_time_assertions();
    switch( fdwReason )
    {
        case DLL_PROCESS_ATTACH:

			if(GetModuleHandle("tobey_validator.exe") != NULL)
			{
				puts("In validator");
				break;
			}

			AllocConsole();
			SetConsoleTitle("spidey-decomp - " RUNTIME_VERSION);
			freopen("CONOUT$", "w", stdout);

			bink_dll = GetModuleHandleA("binkw32.dll");


			puts("spidey-decomp starting " RUNTIME_VERSION);

			runtime_assertions();
			PCGfx_CopyGameResolution();
			runtime_patches();

            break;

        case DLL_THREAD_ATTACH:
        case DLL_THREAD_DETACH:
        case DLL_PROCESS_DETACH:
            break;
    }

    return TRUE;
}
#endif

// @Bogus
void DoAssert(u8 cond, const char* str, ...)
{
	if (!cond)
		puts(str);
}
