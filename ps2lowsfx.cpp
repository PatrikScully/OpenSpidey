#include "ps2lowsfx.h"
#include "DXsound.h"
#include "ps2redbook.h"
#include "my_assert.h"
#include "PCGfx.h"
#include "pcdcMem.h"
#include "stubs.h"
#include "utils.h"
#include "tweak.h"
#include "spidey.h"
#include "dcfileio.h"

#include <cmath>

#include "validate.h"

// #define VALIDATE_PARSESFX


// ---------------------------------------------------------------------------
// The sound effect state is shared between this repo and the exe, so it gets
// G_ macros.  Every enemy, weapon and cutscene file plays sounds through
// SFX_Play / SFX_PlayPos, so as soon as any of those files is hooked its
// hooked half allocates voices out of our private gSfxEntries while the exe
// keeps using its own, and the two voice tables drift apart.
//
// Every address below was read out of the original disassembly
// (tools/functions/*.bin), not taken from a name list:
//
//   gSfxArrayOne                0x0054F6F0  SFX_Play      lea edi,[eax*4+54F6F0h]
//   gVoiceIndex                 0x0054FAF0  playSFX       mov dword [54FAF0h],1
//   gSfxGlobal                  0x0060D86C  SFX_Unpause   mov [60D86Ch],eax (6666)
//   gSfxRelatedOutLevel         0x00615308  playSFX       mov ecx,615308h
//   gSoundBank                  0x00616D70  SFX_Init      push 616D70h (SFX_LoadBank)
//   gSfxEntries                 0x00618730  SFX_FreeVoice lea eax,[eax*8+618730h]
//   gSfxPlayRelated             0x00618C3C  SFX_Play      mov [618C3Ch],esi
//   SFXLevelSpecificAliasArray  0x00618C7C  SFX_SpoolInLevelSFX push 618C7Ch
//   gSfxArraAliasyOne           0x00618D00  SFX_Init      push 618D00h
//   gSfxSomething               0x00618D80  SFX_ShutDown  mov edi,618D80h; rep stosd
//   SFXLevelSpecificArray       0x00618D9C  SFX_SpoolInLevelSFX push 618D9Ch
//   SFXFalloffArray             0x0061909C  SFX_ModifyPos mov ebx,61909Ch
//   SFXPaused                   0x0061919C  SFX_LevelStart mov byte [61919Ch],0
//   gBootRomSoundMode           0x0061919D  SFX_ModifyPos mov dl,[61919Dh]
//   gReverbType                 0x0061919E  SFX_ReverbOff mov byte [61919Eh],0
//   gNumVoices                  0x006191A0  SFX_FreeVoice mov [6191A0h],ecx
//   gSfxInited                  0x006191A4  SFX_ShutDown  mov byte [6191A4h],0
//
// gSfxVolArr (0x0054F9F0) and gStubSfx (0x0054D342) stay repo-local.  A scan of
// the whole .text finds only reads of both (three of gSfxVolArr, one of
// gStubSfx) and no writes at all, and the exe's .data bytes for gSfxVolArr are
// byte for byte the table below, so the two copies can never disagree.
// ---------------------------------------------------------------------------

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT u32 gSfxPlayRelated;
#else
extern u32 gSfxPlayRelated;
#endif
//#define G_SFX_PLAY_RELATED (gSfxPlayRelated)
#define G_SFX_PLAY_RELATED (*reinterpret_cast<u32*>(0x00618C3C))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gSfxGlobal;
#else
extern i32 gSfxGlobal;
#endif

// @Ok
#ifndef SPIDEY_STANDALONE
u32 SFXFalloffArray[32];
#else
extern u32 SFXFalloffArray[32];
#endif
//#define G_SFX_FALLOFF_ARRAY (SFXFalloffArray)
#define G_SFX_FALLOFF_ARRAY (reinterpret_cast<u32*>(0x0061909C))

// @Ok
#ifndef SPIDEY_STANDALONE
u32 SFXLevelSpecificArray[192];
#else
extern u32 SFXLevelSpecificArray[192];
#endif
//#define G_SFX_LEVEL_SPECIFIC_ARRAY (SFXLevelSpecificArray)
#define G_SFX_LEVEL_SPECIFIC_ARRAY (reinterpret_cast<u32*>(0x00618D9C))
// @Ok
#ifndef SPIDEY_STANDALONE
u16 SFXLevelSpecificAliasArray[64];
#else
extern u16 SFXLevelSpecificAliasArray[64];
#endif
//#define G_SFX_LEVEL_SPECIFIC_ALIAS_ARRAY (SFXLevelSpecificAliasArray)
#define G_SFX_LEVEL_SPECIFIC_ALIAS_ARRAY (reinterpret_cast<u16*>(0x00618C7C))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT bool gBootRomSoundMode;
#else
extern bool gBootRomSoundMode;
#endif

// @Ok
// Read only: nothing in the whole binary writes it, so our copy and the
// exe's copy at 0x0054F9F0 always hold the same table.
#ifndef SPIDEY_STANDALONE
EXPORT u8 gSfxVolArr[256] =
{

  0xFF, 0xFB, 0xF7, 0xF3, 0xEF, 0xEB, 0xE7, 0xE3, 0xDF, 0xDB, 
  0xD7, 0xD1, 0xCB, 0xC5, 0xC1, 0xBB, 0xB7, 0xB3, 0xB1, 0xAD, 
  0xA9, 0xA5, 0xA3, 0x9F, 0x9D, 0x99, 0x97, 0x95, 0x93, 0x91, 
  0x8F, 0x8D, 0x8B, 0x89, 0x87, 0x85, 0x83, 0x81, 0x7F, 0x7D, 
  0x7B, 0x7A, 0x79, 0x77, 0x75, 0x74, 0x73, 0x71, 0x6F, 0x6E, 
  0x6D, 0x6C, 0x6B, 0x69, 0x67, 0x66, 0x65, 0x64, 0x63, 0x62, 
  0x61, 0x60, 0x5F, 0x5E, 0x5D, 0x5C, 0x5B, 0x5A, 0x59, 0x58, 
  0x57, 0x56, 0x55, 0x54, 0x53, 0x52, 0x51, 0x50, 0x4F, 0x4E, 
  0x4D, 0x4D, 0x4C, 0x4C, 0x4B, 0x4A, 0x49, 0x48, 0x47, 0x47, 
  0x46, 0x46, 0x45, 0x44, 0x43, 0x42, 0x41, 0x41, 0x40, 0x40, 
  0x3F, 0x3E, 0x3D, 0x3D, 0x3C, 0x3C, 0x3B, 0x3B, 0x3A, 0x3A, 
  0x39, 0x38, 0x37, 0x37, 0x36, 0x36, 0x35, 0x35, 0x34, 0x34, 
  0x33, 0x33, 0x32, 0x32, 0x31, 0x31, 0x30, 0x30, 0x2F, 0x2F, 
  0x2E, 0x2E, 0x2D, 0x2D, 0x2C, 0x2C, 0x2B, 0x2B, 0x2A, 0x2A, 

  0x29, 0x29, 0x28, 0x28, 0x27, 0x27, 0x26, 0x26, 0x25, 0x25, 
  0x24, 0x24, 0x23, 0x23, 0x23, 0x22, 0x22, 0x22, 0x21, 0x21, 
  0x20, 0x20, 0x1F, 0x1F, 0x1E, 0x1E, 0x1D, 0x1D, 0x1D, 0x1C, 
  0x1C, 0x1C, 0x1B, 0x1B, 0x1B, 0x1A, 0x1A, 0x1A, 0x19, 0x19, 
  0x18, 0x18, 0x17, 0x17, 0x17, 0x16, 0x16, 0x16, 0x15, 0x15, 
  0x15, 0x14, 0x14, 0x14, 0x13, 0x13, 0x13, 0x12, 0x12, 0x12, 
  0x11, 0x11, 0x11, 0x10, 0x10, 0x10, 0x0F, 0x0F, 0x0F, 0x0E, 
  0x0E, 0x0E, 0x0D, 0x0D, 0x0D, 0x0D, 0x0C, 0x0C, 0x0C, 0x0C, 
  0x0B, 0x0B, 0x0B, 0x0A, 0x0A, 0x0A, 0x09, 0x09, 0x09, 0x08, 
  0x08, 0x08, 0x07, 0x07, 0x07, 0x07, 0x06, 0x06, 0x06, 0x06, 
  0x05, 0x05, 0x05, 0x05, 0x04, 0x04, 0x04, 0x04, 0x03, 0x03, 
  0x02, 0x02, 0x01, 0x01, 0x00, 0x00
};
#else
extern u8 gSfxVolArr[256];
#endif

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT u8 gReverbType;
#else
extern u8 gReverbType;
#endif
//#define G_REVERB_TYPE (gReverbType)
#define G_REVERB_TYPE (*reinterpret_cast<u8*>(0x0061919E))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT u8 gSfxInited;
#else
extern u8 gSfxInited;
#endif
//#define G_SFX_INITED (gSfxInited)
#define G_SFX_INITED (*reinterpret_cast<u8*>(0x006191A4))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gVoiceIndex = 1;
#else
extern i32 gVoiceIndex;
#endif
//#define G_VOICE_INDEX (gVoiceIndex)
#define G_VOICE_INDEX (*reinterpret_cast<i32*>(0x0054FAF0))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT SSfxRelated gSfxSomething;
#else
extern SSfxRelated gSfxSomething;
#endif
//#define G_SFX_SOMETHING (gSfxSomething)
#define G_SFX_SOMETHING (*reinterpret_cast<SSfxRelated*>(0x00618D80))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gNumVoices;
#else
extern i32 gNumVoices;
#endif
//#define G_NUM_VOICES (gNumVoices)
#define G_NUM_VOICES (*reinterpret_cast<i32*>(0x006191A0))

#ifndef SPIDEY_STANDALONE
EXPORT u8 SFXPaused;
#else
extern u8 SFXPaused;
#endif
//#define G_SFX_PAUSED (SFXPaused)
#define G_SFX_PAUSED (*reinterpret_cast<u8*>(0x0061919C))
#define LEN_SFX_ENTRIES 32
#ifndef SPIDEY_STANDALONE
EXPORT SSfxEntry gSfxEntries[LEN_SFX_ENTRIES];
#else
extern SSfxEntry gSfxEntries[LEN_SFX_ENTRIES];
#endif
//#define G_SFX_ENTRIES (gSfxEntries)
#define G_SFX_ENTRIES (reinterpret_cast<SSfxEntry*>(0x00618730))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT SSFXBank gSoundBank;
#else
extern SSFXBank gSoundBank;
#endif
//#define G_SOUND_BANK (gSoundBank)
#define G_SOUND_BANK (*reinterpret_cast<SSFXBank*>(0x00616D70))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT SSFXBank gSfxRelatedOutLevel;
#else
extern SSFXBank gSfxRelatedOutLevel;
#endif
//#define G_SFX_RELATED_OUT_LEVEL (gSfxRelatedOutLevel)
#define G_SFX_RELATED_OUT_LEVEL (*reinterpret_cast<SSFXBank*>(0x00615308))


// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT u32 gSfxArrayOne[192] =
{
  1u,
  234881084u,
  8192u,
  2u,
  234881084u,
  8192u,
  4u,
  234881084u,
  8192u,
  8u,
  234881084u,
  8192u,

  16u,
  201326652u,
  8192u,
  32u,
  201326652u,
  8192u,

  64u,
  201326652u,
  8192u,
  128u,
  201326655u,
  8192u,
  256u,
  201326652u,
  8192u,
  512u,

  201326652u,

  8192u,
  1024u,
  201326652u,
  8192u,
  2048u,
  201326652u,
  8192u,
  4096u,
  201326652u,
  8192u,
  8192u,

  201326652u,

  8192u,
  16384u,
  201326652u,
  8192u,
  32768u,
  201326652u,
  8192u,
  65537u,
  201326652u,
  8192u,

  65538u,
  201326652u,
  8192u,
  65540u,
  201326652u,
  8192u,
  65544u,
  201326652u,
  8192u,
  65552u,
  201326652u,
  8192u,
  65568u,
  201326652u,

  8192u,
  65600u,
  201326652u,
  8192u,
  65664u,
  201326652u,
  8192u,
  65792u,
  201326652u,
  8192u,

  2147549696u,
  201326652u,
  8192u,
  66560u,

  201326652u,

  8192u,
  67584u,
  201326652u,
  8192u,
  69632u,
  201326652u,
  8192u,
  73728u,
  201326652u,
  8192u,

  81920u,
  268435516u,
  8192u,
  98304u,
  201326652u,
  8192u,
  2147614721u,
  201326652u,
  8192u,
  131074u,
  201326652u,
  8192u,
  131076u,
  201326652u,
  8192u,
  131080u,
  201326652u,
  8192u,
  131088u,
  201326652u,
  8192u,
  131104u,
  201326652u,
  8192u,
  131136u,
  201326652u,
  8192u,
  131200u,
  201326652u,
  8192u,
  131328u,
  234881084u,
  8192u,
  131584u,
  234881084u,
  8192u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u,
  0u
};
#endif
//#define G_SFX_ARRAY_ONE (gSfxArrayOne)
#define G_SFX_ARRAY_ONE (reinterpret_cast<u32*>(0x0054F6F0))


// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT u16 gSfxArraAliasyOne[64];
#else
extern u16 gSfxArraAliasyOne[64];
#endif
//#define G_SFX_ARRAY_ALIAS_ONE (gSfxArraAliasyOne)
#define G_SFX_ARRAY_ALIAS_ONE (reinterpret_cast<u16*>(0x00618D00))

// @Bogus
// @Note: exists purely for matching purposes
static void nullsub_3(void)
{
}

// @Bogus
// @Note: exists purely for matching purposes
static void nullsub_one_arg(i32)
{
}

// @Ok
INLINE void CopyFilenameDefaultExtension(
		char* dst,
		i32 len,
		const char* pFileName,
		const char* pExtension)
{
	strncpy(dst, pFileName, len);

	char *dot = strrchr(dst, '.');
	if (dot)
		*dot = 0;
	strcat(dst, pExtension);
}

// @Ok
// @Matching
INLINE i32 DCSFX_AdjustVol(i32 a1)
{
	DoAssert((a1 >= 0 || (a1 = -a1, a1 >= 0)) && a1 < 0x4000, "DCSFX_AdjustVol: bad volume");

	a1 >>= 5;
	if (a1 > 255)
		a1 = 255;

	return 255 - gSfxVolArr[a1];
}

// @Ok
// @Matching
void DCSetBootROMSoundMode(bool a1)
{
	if (a1 != G_BOOT_ROM_SOUND_MODE)
	{
		G_BOOT_ROM_SOUND_MODE = a1;
		void *v1 = syMalloc(0x4000u);

		i32 v2 = syCfgInit(v1);
		DoAssert(v2 == 0, "Problem with configuration init.");

		i32 v3 = syCfgSetSoundMode(a1 != false);
		DoAssert(v3 == 0, "Problem with syCfgGetSoundMode");


		i32 v4 = syCfgExit();
		DoAssert(v4 == 0, "Problem with syCfgExit");

		syFree(v1);
	}
}

// @Ok
// @AlmostMatching: after the log function there's an extra spill in memory that I cannot match
// don't know if it's due to float consistency thingy, but can't repro this shit for some reason
INLINE i32 PSXPitchToDCPitch(i32 a1)
{
	if (a1 <= 0)
		return 0;

	f32 v16 = 1.442695f * logf(a1);
	f32 v17 = (v16 - 10.0f) * 1200.0f;

	return v17;
}

// @Ok
// @AlmostMatching: only used once and code gen is slightly different
INLINE i32 SFX_AllocVoice(i32 a1, bool a2)
{
	i32 v8 = G_VOICE_INDEX;
	i32 v9 = 0;
	for (i32 i = G_VOICE_INDEX; i < 32; i++)
	{

		if (!DXSOUND_IsPlaying(i))
		{
			SFX_KillVoice(i);
			G_VOICE_INDEX = i + 1;
			if (i == 31)
				G_VOICE_INDEX = 1;
			v9 = 1;
			break;
		}
	}

	if (!v9)
	{
		for (i32 j = 1; j < v8; j++)
		{
			if (!DXSOUND_IsPlaying(j))
			{
				SFX_KillVoice(j);
				G_VOICE_INDEX = j + 1;
				if (j == 31)
					G_VOICE_INDEX = 1;
				break;
			}
		}
	}

	i32 k;
	for (k = 1; k < 32; k++)
	{
		if (!G_SFX_ENTRIES[k].field_1A && !G_SFX_ENTRIES[k].field_1B)
			break;
	}

	if (k < 32)
	{
		G_SFX_ENTRIES[k].field_0 = a1;
		G_SFX_ENTRIES[k].field_1A = 1;
		G_SFX_ENTRIES[k].field_1B = a2;
		G_SFX_ENTRIES[k].field_4 = 0;
		G_SFX_ENTRIES[k].field_8 = 0;
		G_SFX_ENTRIES[k].field_C = 0;
		G_SFX_ENTRIES[k].field_10 = 0;
		G_SFX_ENTRIES[k].field_1C = 0;

		DoAssert(G_NUM_VOICES++ <= 32, "voice allocation error"); 
		return k;
	}

	error("SFX out of voices.");
    nullsub_3();
	return -1;
}

// @Ok
// @AlmostmAtching: something about amheapFree is making it not match
// it dumps the result into the stack before pushing
INLINE void SFX_CloseBank(SSFXBank *pBank)
{
	SFX_StopAll();

	if (pBank->field_4)
	{
		DXSOUND_Unload(pBank->field_8, 0);
		
		if (pBank == &G_SOUND_BANK && G_SFX_RELATED_OUT_LEVEL.field_4)
			SFX_CloseBank(&G_SFX_RELATED_OUT_LEVEL);

		DebugPrintfX("unloading sound bank %s.", pBank->field_8);

		i32 res = amHeapFree(pBank->field_4);
		DoAssert(!!res, "amHeapFree failed");
		pBank->field_4 = 0;
		pBank->mNumAssets = 0;

		if (G_SFX_SOMETHING.field_0)
			Mem_AlignedDelete(G_SFX_SOMETHING.field_0);

		memset(&G_SFX_SOMETHING, 0, sizeof(G_SFX_SOMETHING));
	}
}

// @Ok
// @Matching
INLINE void SFX_FreeVoice(i32 a1)
{
	nullsub_3();
	SSfxEntry *pEntry = &G_SFX_ENTRIES[a1];

	pEntry->field_1A = 0;
	pEntry->field_1B = 0;
	pEntry->field_10 = 0;
	pEntry->field_1C = 0;
	pEntry->field_24 = 0;

	DoAssert(--G_NUM_VOICES >= 0, "voice deallocation error");
	nullsub_3();
}

// @Ok
void SFX_Init(char* pSfxBankName)
{
	char buf[56];
	DoAssert(pSfxBankName && *pSfxBankName, "bad sfx bank filename");

	CopyFilenameDefaultExtension(buf, sizeof(buf), pSfxBankName, ".kat");
	SFX_LoadBank(buf, &G_SOUND_BANK);
	CopyFilenameDefaultExtension(buf, sizeof(buf), pSfxBankName, ".sfx");

	SFX_ParseSFXFile(buf, G_SFX_ARRAY_ONE, G_SFX_ARRAY_ALIAS_ONE, 64, 0);
}

// @Ok
// 2026-09-01: full translation from a fresh IDA decompile+disasm of 0x470FD0 (452 bytes).
// gSfxInited is byte_6191A4 (checked at entry, set here) and gNumVoices is dword_6191A0
// (stored right after gSfxInited, matches the 4-byte gap between the two in the disasm).
// gSfxEntries is dword_618730 (memset of 0x140 dwords = 0x500 = sizeof(gSfxEntries)) and
// gSfxSomething is dword_618D80 (memset of 7 dwords = 0x1C = sizeof(SSfxRelated)).
//
// The rest of the original is real Dreamcast AICA sound-driver init: it loads the
// "thehall.fpb" DSP program bank and the "amulti.fob" FX program bank, then "installs"
// each into AICA sound RAM at fixed offsets (0x4880 and 0x5880). On this PC build every
// install/config call in that sequence goes through sub_505B90, which tools/names.json
// names syRtcInit and which is a confirmed literal `xor eax,eax; ret` no-op (stubs.cpp,
// @Ok @Matching; see bit.cpp's investigation of the same function) -- it never reads or
// writes its arguments or any global. Every check built from its return value therefore
// also goes nowhere, since nullsub_1 (0x4015B0) is the confirmed-empty print_if_false
// stub this repo already omits everywhere else it shows up (dcfileio.cpp, ps2m3d.cpp,
// panel.cpp, shell.cpp, etc). That leaves: a "strange heap size" sanity check built from
// a stack slot that is never actually written by anything live (it is pre-loaded with the
// address of sub_470E70, a per-voice cleanup callback, but nothing ever reads that address
// back through a call, only through the dead no-op chain above) and two "install program
// bank" calls whose arguments are only ever forwarded into the no-op. None of that writes
// any persistent field or global, so it is omitted here. The only observable effect of the
// whole function beyond the memsets/flags above is: open+load then immediately free
// "thehall.fpb" and "amulti.fob" via FileIO_Unk/FileIO_Unk2 (dcfileio.cpp, both @Ok
// @Matching, exact-address matches for sub_430C30/sub_430CA0 respectively).
void SFX_InitAtStart(void)
{
	if (!G_SFX_INITED)
	{
		memset(G_SFX_ENTRIES, 0, sizeof(SSfxEntry) * LEN_SFX_ENTRIES);
		memset(&G_SFX_SOMETHING, 0, sizeof(G_SFX_SOMETHING));
		G_SFX_INITED = 1;
		G_NUM_VOICES = 0;

		i32 dspBankSize;
		void *dspBank = FileIO_Unk("thehall.fpb", &dspBankSize);
		FileIO_Unk2(dspBank);

		i32 fxBankSize;
		void *fxBank = FileIO_Unk("amulti.fob", &fxBankSize);
		FileIO_Unk2(fxBank);
	}
}

// @Ok
INLINE void SFX_KillVoice(u32 a1)
{
	if (G_SFX_ENTRIES[a1].field_1A)
	{
		DXSOUND_Stop(a1);
		DXSOUND_Close(a1);
		SFX_FreeVoice(a1);
	}
}

// @Ok
// @AlmostMatching: KillVoice is not inlined here for some reason, tiny reg allocation diff
// diffs come also from fileSize update but that's not used so idgaf
void SFX_LoadBank(
		const char *pName,
		SSFXBank *pBank)
{
	if (pBank->field_4 && !strcmp(pBank->field_8, pName))
		return;

	SFX_CloseBank(pBank);

	if (!FileIO_FileExists(pName))
	{
		DebugPrintfX("could not find sound bank %s...", pName);
		return;
	}

	DebugPrintfX("loading sound bank %s...", pName);
	i32 fileSize = FileIO_Open(pName);
	DoAssert(fileSize != 0, "couldn't find bank file");
#ifdef SPIDEY_STANDALONE
	if (fileSize < static_cast<i32>(sizeof(i32)))
		return;
#endif

	void *fileBuf = DCMem_New(fileSize, 0, 1, 0, 1);
	FileIO_Load(fileBuf);
	FileIO_Sync();

	pBank->mNumAssets = static_cast<i32*>(fileBuf)[0];
	DoAssert(pBank->mNumAssets < NUM_ASSETS_PER_BANK, "Too many assets in bank.");
#ifdef SPIDEY_STANDALONE
	if (pBank->mNumAssets <= 0 || pBank->mNumAssets >= NUM_ASSETS_PER_BANK ||
			fileSize < static_cast<i32>(sizeof(i32) + sizeof(SSfxAsset) * pBank->mNumAssets))
	{
		Mem_AlignedDelete(fileBuf);
		pBank->mNumAssets = 0;
		return;
	}
#endif

	u8 v17 = 0;
	memcpy(
			pBank->mAssets,
			&static_cast<i32*>(fileBuf)[1],
			sizeof(SSfxAsset) * pBank->mNumAssets);

	i32 mNumAssets = pBank->mNumAssets;
	SSfxAsset *pAsset = &pBank->mAssets[pBank->mNumAssets];

	while (--pAsset >= pBank->mAssets)
	{

		DoAssert(--mNumAssets >= 0, "too many streams in the bank");

		DoAssert(
				pAsset->field_C >= 0x63 && pAsset->field_C <= 0xF423F,
				"bogus sample rate");

		DoAssert(
				pAsset->field_14 == 4 || pAsset->field_14 == 8 || pAsset->field_14 == 16,
				"bogus bit depth");
		if (pAsset->field_8 >= 0x20000 && pBank != &G_SOUND_BANK)
		{
			DoAssert(1, "all streaming samples must be at end of bank");

			G_SFX_SOMETHING.field_4 = pAsset->field_8;
			G_SFX_SOMETHING.field_4 &= 0xFFFFFFFC;
			G_SFX_SOMETHING.field_4 = pAsset->field_8;

			i32 v13 = 4 - (pAsset->field_4 & 3);
			G_SFX_SOMETHING.field_C += 13;

			if (!G_SFX_SOMETHING.field_0)
			{
				G_SFX_SOMETHING.field_0 = DCMem_New(G_SFX_SOMETHING.field_4, 0, 1, 0, 1);
				DoAssert(
						!!G_SFX_SOMETHING.field_0,
						"could not allocate main mem stream buffer");
				memcpy(
						G_SFX_SOMETHING.field_0,
						&reinterpret_cast<u8*>(fileBuf)[G_SFX_SOMETHING.field_C],
						G_SFX_SOMETHING.field_4);
			}

			G_SFX_SOMETHING.field_10 = 0xC000;

			switch (pAsset->field_14)
			{
				case 4:
					G_SFX_SOMETHING.field_14 = G_SFX_SOMETHING.field_10;
					break;
				case 8:
					G_SFX_SOMETHING.field_14 = G_SFX_SOMETHING.field_10 >> 1;
					break;
				case 16:
					G_SFX_SOMETHING.field_14 = G_SFX_SOMETHING.field_10 >> 2;
					break;
				default:
					DoAssert(0, "?");
				break;
			}

			v17 = 1;
			pAsset->field_8 = G_SFX_SOMETHING.field_10 + v13;

			fileSize += pAsset->field_8 - G_SFX_SOMETHING.field_4;
			fileSize += 3;
			fileSize &= 0xFFFFFFFC;
		}
	}

#ifndef SPIDEY_STANDALONE
	u32 *v20;
	if (!amHeapAlloc(&v20, fileSize, 32, 2, 0))
		error("unable to allocate %d bytes of sound memory for %s", fileSize, pName);


	i32 acRes = acG2Write(v20, fileBuf, fileSize);
	DoAssert(!!acRes, "acG2Write failed");

	if (v17)
		G_SFX_SOMETHING.field_C += reinterpret_cast<i32>(v20);
#endif

	Mem_AlignedDelete(fileBuf);
#ifndef SPIDEY_STANDALONE
	pBank->field_4 = reinterpret_cast<i32>(v20);
#endif
	strncpy(pBank->field_8, pName, 56);

	DXSOUND_Load(pBank->field_8);
#ifdef SPIDEY_STANDALONE
	// Original PC allocation stubs leave v20 unset (0x4717A2/0x4717C8).
	// Native readiness follows the WAV buffers, including partial loads.
	extern IDirectSoundBuffer* gDxSoundBuffers[0x80];
	extern i32 AUDIOGROUPS_GetGroup(char*);
	i32 first = AUDIOGROUPS_GetGroup(pBank->field_8) == 1 ? 0 : 0x40;
	for (i32 i = first; i < first + 0x40; i++)
	{
		if (gDxSoundBuffers[i])
		{
			pBank->field_4 = 1;
			break;
		}
	}
#endif
}

// @Ok
// @AlmostMatching: inline of PSXPitchToDCPitch not matching
void SFX_ModifyPos(
		u32 voice_id,
		const CVector *pos,
		i32 delta_dist)
{
	if (voice_id)
	{
		for (i32 i = 0; i < 32; i++)
		{
			if (voice_id & (1<<i))
			{
				u32 v11 = Utils_CalculateSpatialAttenuation(
						pos,
						(G_SFX_FALLOFF_ARRAY[i] >> 2) & 0x3FFF,
						(G_SFX_FALLOFF_ARRAY[i] & 0xFFFF) << 1);

				i32 v13 = v11 & 0xFFF;
				v13 <<= 2;
				v13 *= G_GAMESTATE[12];
				v13 >>= 14;

				i32 v14 = (v11 >> 16) & 0xFFF;
				v14 <<= 2;
				v14 *= G_GAMESTATE[12];
				v14 >>= 14;

				if (G_BOOT_ROM_SOUND_MODE)
				{
					v14 = (v13 + v14) / 2;
					v13 = v14;
				}

				SFX_ModifyVol(1 << i, v13, v14);
				

				if (delta_dist)
				{
					i32 v16 = G_SFX_ENTRIES[i].field_14;
					i32 v17 = v16 * delta_dist / 0x2000;
					i32 v18 = v16 - v17;
					if (v18 > v16 && v18 - v16 > v16 + (v16 >> 1))
						v18 = v16 + (v16 >> 1);

					G_SFX_ENTRIES[i].field_20 = PSXPitchToDCPitch(v18);
					DXSOUND_SetPitch(i, G_SFX_ENTRIES[i].field_20);
				}
			}
		}
	}
}

// @Ok
// @Matching
void SFX_ModifyVol(
		u32 voice_id,
		i32 vl,
		i32 vr)
{
	i32 v10 = 0;
	while (voice_id)
	{
		if (voice_id & 1)
		{
			if (G_SFX_ENTRIES[v10].field_1A)
			{
				if (vl > 0x3FFF)
				{
					vl = 0x3FFF;
				}
				else if (vl < -16383)
				{
					vl = -16383;
				}

				if (vr > 0x3FFF)
				{
					vr = 0x3FFF;
				}
				else if (vr < -16383)
				{
					vr = -16383;
				}

				i32 v6 = (vr + vl) >> 1;
				i32 v7;
				if (vl == vr)
					v7 = 15;
				else
					v7 = 31 * vl / (vr + vl);

				DoAssert(1u, "pan out of range");

				DXSOUND_SetPan(v10, v7);
				G_SFX_ENTRIES[v10].field_16 = v6;

				if (v6 > 0x3FFF)
				{
					v6 = 0x3FFF;
				}
				else if (v6 < -16383)
				{
					v6 = -16383;
				}

				i32 vol = DCSFX_AdjustVol(v6);
				DXSOUND_SetVolume(v10, vol);
			}
		}

		voice_id >>= 1;
		v10++;
	}
}

// Read only: 0x0054D342, read once by SFX_Off and written nowhere in the
// binary, so it stays repo-local.
#ifndef SPIDEY_STANDALONE
EXPORT u8 volatile gStubSfx;
#else
extern u8 volatile gStubSfx;
#endif

// @Ok
// @Matching
void SFX_Off(void)
{
	if (!gStubSfx)
		stubbed_printf("Stubbed: SFX_Off( void )\n");
}

u8 testBuf[0x800];

// @Ok
// @Matching
// @Validated
void SFX_ParseSFXFile(
		char *p_name,
		u32 *array,
		u16 *aliasArray,
		i32 maxEntries,
		i32 mask)
{
	char v20[20];

	DoAssert(p_name != 0, "Invalid SFX filename");
	DoAssert(array != 0, "No sfx array to fill");
	DoAssert(aliasArray != 0, "No sfx alias array to fill");

	sprintf(v20, p_name);

#ifndef VALIDATE_PARSESFX
	u32 *pSfxFile = static_cast<u32*>(DCMem_New(0x800u, 0, 1, 0, 1));
	i32 fileSize = FileIO_Open(v20);
	DoAssert(fileSize <= 2048, "Bad size for .SFX file");


	FileIO_Load(pSfxFile);
	FileIO_Sync();
#else
	u32 *pSfxFile = reinterpret_cast<u32*>(testBuf);
#endif

	i32 sfxIndex = 0;
	i32 arrayIndex = 0;
	i32 aliasIndex = 0;

	u32 cur = pSfxFile[sfxIndex];
	u32 *p = &pSfxFile[sfxIndex+1];

	while (cur != 0xFFFFFFFF && arrayIndex < 192)
	{

		if ((cur & 0xFF) == 0xFE)
			array[arrayIndex] = mask | (1 << 31);
		else 
			array[arrayIndex] = mask;

		array[arrayIndex++] |= ((cur & 0x0000FF00) << 8) | 1 << ((cur / 65536) & 0xFF);

		array[arrayIndex] = (cur & 0xFF000000) >> 0x18;

		u32 nextVal = p[sfxIndex++];

		array[arrayIndex++] |= (nextVal & 0xFFFF0000);
		array[arrayIndex++] = nextVal & 0xFFFF;

		aliasArray[aliasIndex++] = p[sfxIndex++];

		sfxIndex++;

		cur = p[sfxIndex++];
	}

	if (aliasIndex < maxEntries)
		aliasArray[aliasIndex] = -1;

#ifndef VALIDATE_PARSESFX
	Mem_Delete(pSfxFile);
#endif
}

// @Ok
// @Matching
void SFX_Pause(void)
{
	if (!G_SFX_PAUSED)
	{
		G_SFX_PAUSED = 1;

		for (i32 i = 0; i < LEN_SFX_ENTRIES; i++)
		{
			if (G_SFX_ENTRIES[i].field_1A)
				DXSOUND_SetVolume(i, 0);
		}

		Redbook_XAPause(true);
	}
}

// @Ok
// @Matching
void SFX_SetVoiceVolume(u32 a1, i16 a2)
{
	if (a1)
	{
		for (i32 i = 0; i < 32; i++)
		{
			if (a1 & (1<< i))
			{
				G_SFX_ENTRIES[i].field_16 = a2;

				SFX_ModifyVol(
						1 << i,
						G_SFX_ENTRIES[i].field_16,
						G_SFX_ENTRIES[i].field_16);
				break;
			}
		}
	}
}

// @Ok
// @NotMatching: the null subs are weird are probably fucked the inlining
void SFX_ShutDown(void)
{
	if (G_SFX_INITED)
	{
		SFX_CloseBank(&G_SFX_RELATED_OUT_LEVEL);
		SFX_CloseBank(&G_SOUND_BANK);

		nullsub_3();
		nullsub_3();

		G_SFX_INITED = 0;
	}
}

// @Ok
// @AlmostMatching: SFX_CloseBank in the original doesn't inline SFX_Stop for some reason
void SFX_SpoolInLevelSFX(const char *p_name)
{
	DoAssert(p_name && *p_name, "bad level sfx bank filename");

	SFX_CloseBank(&G_SFX_RELATED_OUT_LEVEL);

	char v5[56];
	CopyFilenameDefaultExtension(v5, sizeof(v5), p_name, ".kat");
	SFX_LoadBank(v5, &G_SFX_RELATED_OUT_LEVEL);

	CopyFilenameDefaultExtension(v5, sizeof(v5), p_name, ".sfx");
	SFX_ParseSFXFile(v5, G_SFX_LEVEL_SPECIFIC_ARRAY, G_SFX_LEVEL_SPECIFIC_ALIAS_ARRAY, 64, 0x40000000);
}

// @Ok
// @AlmostMatching: inlining diffs by 4 bytes due to 2 inlined functions diffing 2 bytes
void SFX_SpoolOutLevelSFX(void)
{
	SFX_CloseBank(&G_SFX_RELATED_OUT_LEVEL);
}

// @Ok
// @AlmostMatching: same size and reg allocation, just shifted pointer diff offsets for gSfxEntries
void SFX_Unpause(void)
{
	if (G_SFX_PAUSED)
	{
		G_SFX_PAUSED = 0;
		for (i32 i = 0; i < 32; i++)
		{
			if (G_SFX_ENTRIES[i].field_1B)
			{
				u8 v5;
				if (!(G_SFX_ENTRIES[i].field_24 & 0x4000) && G_REVERB_TYPE)
					v5 = 0;
				else
					v5 = 1;


				i32 v6 = (G_GAMESTATE[12] * ((G_SFX_ENTRIES[i].field_18 * G_SFX_ENTRIES[i].field_16) >> 12)) >> 14;
				if (v6 > 0x3FF)
				{
					v6 = 0x3FFF;
				}
				else if (v6 < -16383)
				{
					v6 = -16383;
				}

				i32 v7 = DCSFX_AdjustVol(v6);
				if (v5 == 0)
					acDspSetMixerChannel(i, 0, 15, 10);

				DXSOUND_SetVolume(i, v7);
				DXSOUND_SetPan(i, 15);
				DXSOUND_SetPitch(i, G_SFX_ENTRIES[i].field_20);
				DXSOUND_Play(i, 1);
			}
		}

		G_SFX_GLOBAL = 6666;
		G_MECHLIST_PLAYER->field_530 = 6666;
		Redbook_XAPause(0);
		nullsub_one_arg(2);
	}
}

// @NotOk
// Original pitch and call fixtures pass. Matching remains unfinished:
// 198 instruction diffs against 0x472490, including inlining and registers.
u32 playSFX(
		u32 sfx,
		u8 pitch,
		i16 vl,
		i16 vr,
		i32 pitch_offset,
		u16 volMod)
{
	bool looped = (sfx & 0x80000000) != 0;
	u16 assetIndex = (sfx >> 0x10) & 0x7F;

	SSFXBank *pBank = &G_SFX_RELATED_OUT_LEVEL;
	if (!(sfx & 0x40000000))
		pBank = &G_SOUND_BANK;

	if (!pBank->field_4)
		return -1;

	DoAssert(
			pBank->mAssets[assetIndex].field_C >= 0x63 && pBank->mAssets[assetIndex].field_C <= 0xF423F,
			"bogus sample rate");

	i32 waveFormat = pBank->mAssets[assetIndex].field_14;
	if (waveFormat != 4 && waveFormat != 8 && waveFormat != 16)
	{
		error("invalid wave format.");
		return -1;
	}

	i32 v14 = (vl + vr) >> 1;
	if (v14 < -16383)
	{
		v14 = -16383;
	}
	else if (v14 > 0x3FFF)
	{
		v14 = 0x3FFF;
	}

	i32 vla;
	if ( vl == vr )
		vla = 15;
	else
		vla = 31 * vl / (vl + vr);

	DoAssert(1, "pan out of range");

	i32 v19 = SFX_AllocVoice(
			assetIndex,
			looped);
	if (v19 < 0)
		return 0;

	G_SFX_ENTRIES[v19].field_18 = volMod;
	G_SFX_ENTRIES[v19].field_24 = sfx;
	G_SFX_ENTRIES[v19].field_16 = v14;
	G_SFX_ENTRIES[v19].field_14 = pitch;

	u8 newPitch = PSXPitchToDCPitch((100 * (pitch - 60)) & 0xFF);
	G_SFX_ENTRIES[v19].field_20 = newPitch;
	i32 newVol = DCSFX_AdjustVol(v14);
	DXSOUND_Open(
			v19,
			assetIndex,
			sfx & 0x40000000);
	
	if ((sfx & 0xA0000000) == 0xA0000000)
	{
		DXSOUND_SetVolume(v19, newVol);
		DXSOUND_SetPan(v19, vla);

		DXSOUND_SetPitch(v19, newPitch);
		DXSOUND_Play(v19, 1);
	}
	else if (sfx & 0x80000000)
	{
		DXSOUND_SetVolume(v19, newVol);
		DXSOUND_SetPan(v19, vla);
		DXSOUND_SetPitch(v19, newPitch);
		DXSOUND_Play(v19, 1);
	}
	else
	{
		DXSOUND_SetVolume(v19, newVol);
		DXSOUND_SetPan(v19, vla);
		DXSOUND_SetPitch(v19, newPitch);
		DXSOUND_Play(v19, 0);
	}

	return 1 << v19;
}

// @Ok
// @Matching
INLINE u32 translateLevelSpecificAliasToIndex(u32 alias)
{
	for (i32 i = 0; i < 64; i++)
	{

		if (G_SFX_LEVEL_SPECIFIC_ALIAS_ARRAY[i] == 0xFFFF)
			return 0xFFFFFFFF;

		if (alias == G_SFX_LEVEL_SPECIFIC_ALIAS_ARRAY[i])
			return i;
	}

	return 0xFFFFFFFF;
}

// @Ok
// @Matching
INLINE void SFX_StopAll(void)
{
	for (i32 i = 0; i < 24; i++)
	{
		SFX_Stop(1 << i);
	}
}

// @Ok
// @AlmostMatching: the v10 assingment was originally optimized but i managed to make it a little better
// also the last call to playSFX is not perfect matching but fine
// @Validate
u32 SFX_PlayPos(
		u32 sound,
		CVector *pos,
		i32 pitch_offset)
{
	if (!sound)
		return 0;

	u8 v4 = (sound >> 15) & 1;
	u8 v5 = (sound >> 14) & 1;
	u32 sfxIndex = sound & 0xFFFF3FFF;

	u32 v9;
	if (v4)
	{
		v9 = translateLevelSpecificAliasToIndex(sfxIndex);
		if (v9 == 0xFFFFFFFF)
			return 0;
	}
	else
	{
		DoAssert(sfxIndex < 0x40, "SFX index out of bounds");
		v9 = sfxIndex - 1;
	}

	u32 *v10 = &G_SFX_LEVEL_SPECIFIC_ARRAY[3 * v9];
	if (!v4)
		v10 = &G_SFX_ARRAY_ONE[3 * v9];

	u32 soundTwo = v10[2];
	u32 v11 = Utils_CalculateSpatialAttenuation(
			pos,
			soundTwo >> 2,
			2 * soundTwo);

	u32 v12 = v10[1];
	u32 v13 = v12 >> 0x10;

	i32 v14 = v11 & 0xFFF;
	v14 <<= 2;
	v14 *= G_GAMESTATE[12];
	v14 >>= 14;

	i32 v15 = (v11 >> 16) & 0xFFF;
	v15 <<= 2;
	v15 *= G_GAMESTATE[12];
	v15 >>= 14;

	if (G_BOOT_ROM_SOUND_MODE)
	{
		v15 = (v14 + v15) / 2;
		v14 = v15;
	}

	G_SFX_PLAY_RELATED = (100 * v13) >> 12;

	u32 result = playSFX(
			v10[0] | (v5 ? 0x4000 : 0),
			v12,
			v14,
			v15,
			pitch_offset,
			v13 & 0xFFFF);

	for (i32 i = 0; i < 32; i++)
	{
		if (result & (1 << i))
		{
			G_SFX_FALLOFF_ARRAY[i] = soundTwo | (v13 << 16);
			break;
		}
	}


	return result;
}

// @Ok
void SFX_LevelStart(void)
{
	G_SFX_PAUSED = 0;
}

// @Ok
// @Matching
void INLINE SFX_SetReverbType(unsigned char reverb)
{
	G_REVERB_TYPE = reverb != 0;
}

// @Ok
void SFX_ReverbOff(void)
{
	SFX_SetReverbType(0);
}

// @Ok
// @Matching
INLINE void SFX_Stop(u32 a1)
{
	u32 v2 = 0;
	while (a1)
	{
		if (a1 & 1)
		{
			if (!G_SFX_ENTRIES[v2].field_1A)
				return;
			SFX_KillVoice(v2);
		}

		a1 >>= 1;
		++v2;
	}
}

// @Ok
// @Validate
// @AlmostMatching: similar to SFX_PlayPos, the args being pushed are weird too
u32 SFX_Play(
		u32 sound,
		i16 vol,
		i32 pitch_offset)
{

	u8 v4 = (sound >> 15) & 1;
	u8 d4 = (sound >> 14) & 1;
	u32 sfxIndex = sound & 0xFFFF3FFF;

	if (!sound)
		return 0;

	u32 v9;
	if (v4)
	{
		v9 = translateLevelSpecificAliasToIndex(sfxIndex);
		if (v9 == 0xFFFFFFFF)
			return 0;
	}
	else
	{
		DoAssert(sfxIndex < 0x40, "SFX index out of bounds");
		v9 = sfxIndex - 1;
	}

	u32 *v10 = &G_SFX_LEVEL_SPECIFIC_ARRAY[3 * v9];
	if (!v4)
		v10 = &G_SFX_ARRAY_ONE[3 * v9];


	u32 v12 = v10[1] & 0xFFFF;
	u32 firstValue = v10[0];
    u32 v13 = (v10[1] >> 0x10) & 0xFFFF;

	i32 realVol = (vol * G_GAMESTATE[12]) >> 14;
	G_SFX_PLAY_RELATED = (100 * v13) >> 12;

	return playSFX(
			firstValue | (d4 ? 0x4000 : 0),
			v12,
			realVol,
			realVol,
			pitch_offset,
			v13 & 0xFFFF);
}

char* sfx_names[62] = {
	"dem1.sfx",
	"dem2.sfx",
	"dem3.sfx",
	"dem4.sfx",
	"l1a1.sfx",
	"l1a2.sfx",
	"l1a3.sfx",
	"l1a4.sfx",
	"l2a1.sfx",
	"l2a2.sfx",
	"l3a1.sfx",
	"l3a2.sfx",
	"l3a3.sfx",
	"l3a4.sfx",
	"l3a5.sfx",
	"l4a1.sfx",
	"l5a1.sfx",
	"l5a2.sfx",
	"l5a3.sfx",
	"l5a4.sfx",
	"l5a5.sfx",
	"l5a6.sfx",
	"l5a7.sfx",
	"l6a1.sfx",
	"l6a2.sfx",
	"l6a3.sfx",
	"l6a4.sfx",
	"l7a1.sfx",
	"l7a2.sfx",
	"l7a3.sfx",
	"l7a4.sfx",
	"l7a5.sfx",
	"l8a1.sfx",
	"l8a2.sfx",
	"l8a3.sfx",
	"l8a4.sfx",
	"l8a5.sfx",
	"l8a6.sfx",
	"l9a1.sfx",
	"l9a2.sfx",
	"l9a3.sfx",
	"l9a4.sfx",
	"laa1.sfx",
	"laa2.sfx",
	"laa3.sfx",
	"laa4.sfx",
	"lba1.sfx",
	"lba2.sfx",
	"lba3.sfx",
	"lba4.sfx",
	"lca1.sfx",
	"lca2.sfx",
	"lca3.sfx",
	"lca4.sfx",
	"lda1.sfx",
	"lda2.sfx",
	"lda3.sfx",
	"lfa1.sfx",
	"lga1.sfx",
	"lha1.sfx",
	"menu.sfx",
	"zArt.sfx",
};

u32 myAray[192];
u16 myAliasAray[64];

i32 validate_sfx(char *name, i32 mask)
{
	memset(testBuf, 0, 0x800);

	FILE *fp = fopen(name, "rb");

	if (!fp)
	{
		printf("Failed opening file for %s\n", name);
		return 1;
	}

	fseek(fp, 0, SEEK_END);
	i32 size = ftell(fp);
	fseek(fp, 0, SEEK_SET);

	if (!fread(testBuf, size, 1, fp))
	{
		printf("Failed reading file %s\n", name);
		return 2;
	}

	fclose(fp);

	memset(myAray, 0, 192*4);
	memset(myAliasAray, 0, 64*2);


	SFX_ParseSFXFile(name, myAray, myAliasAray, 64, mask);

	u32 tmpArray[192];


	char path[256];
	path[0] = '\0';
	strcpy(path, name);
	strcat(path, "_array.bin");

	fp = fopen(path, "rb");
	if (!fp)
	{
		return 3;
	}

	if (!fread(tmpArray, 192*4, 1, fp))
	{
		return 4;
	}

	fclose(fp);


	u16 tmpAlias[64];

	path[0] = '\0';
	strcpy(path, name);
	strcat(path, "_alias.bin");

	fp = fopen(path, "rb");
	if (!fp)
	{
		return 5;
	}

	if (!fread(tmpAlias, 64*2, 1, fp))
	{
		return 6;
	}

	fclose(fp);

	if (memcmp(tmpArray, myAray, 192*4))
	{
		FILE *fp_my = fopen("my_oops.bin", "wb");
		fwrite(myAray, 192*4, 1, fp_my);
		fclose(fp_my);
		printf("SFX: Array validation failed for: %s\n", name);
		return 6;
	}

	if (memcmp(tmpAlias, myAliasAray, 64*2))
	{
		printf("SFX: Alias validation failed for: %s\n", name);
		return 7;
	}


	printf("Done for %s\n", name);

	return 0;
}

extern i32 FAIL_VALIDATION;

void validate_SFXParse(void)
{
	if (validate_sfx("spidey.sfx", 0))
	{
		puts("failed for spidey");
		FAIL_VALIDATION = 1;
		return;
	}

	for (i32 i = 0; i < sizeof(sfx_names) / sizeof (char*); i++)
	{
		if (validate_sfx(sfx_names[i], 0x40000000))
		{
			printf("failed for %s\n", sfx_names[i]);
			FAIL_VALIDATION = 1;
			return;
		}
	}


}

void validate_SSFXBank(void)
{
	VALIDATE_SIZE(SSFXBank, 0x19B0);

	VALIDATE(SSFXBank, mNumAssets, 0x0);
	VALIDATE(SSFXBank, field_4, 0x4);
	VALIDATE(SSFXBank, field_8, 0x8);

	VALIDATE(SSFXBank, mAssets, 0x40);

#ifdef VALIDATE_PARSESFX
	validate_SFXParse();
#endif
}

void validate_SSfxEntry(void)
{
	VALIDATE_SIZE(SSfxEntry, 0x28);

	VALIDATE(SSfxEntry, field_0, 0x0);
	VALIDATE(SSfxEntry, field_4, 0x4);
	VALIDATE(SSfxEntry, field_8, 0x8);
	VALIDATE(SSfxEntry, field_C, 0xC);

	VALIDATE(SSfxEntry, field_18, 0x18)

	VALIDATE(SSfxEntry, field_1A, 0x1A);
	VALIDATE(SSfxEntry, field_1B, 0x1B);
	VALIDATE(SSfxEntry, field_1C, 0x1C);

	VALIDATE(SSfxEntry, field_20, 0x20);

	VALIDATE(SSfxEntry, field_24, 0x24);
}

void validate_SSfxRelated(void)
{
	VALIDATE_SIZE(SSfxRelated, 0x1C);

	VALIDATE(SSfxRelated, field_0, 0x0);
	VALIDATE(SSfxRelated, field_4, 0x4);
	VALIDATE(SSfxRelated, field_8, 0x8);
	VALIDATE(SSfxRelated, field_C, 0xC);

	VALIDATE(SSfxRelated, field_10, 0x10);
	VALIDATE(SSfxRelated, field_14, 0x14);
	VALIDATE(SSfxRelated, field_18, 0x18);
}

void validate_SSfxAsset(void)
{
	VALIDATE_SIZE(SSfxAsset, 0x2C);

	VALIDATE(SSfxAsset, field_4, 0x4);
	VALIDATE(SSfxAsset, field_8, 0x8);
	VALIDATE(SSfxAsset, field_C, 0xC);

	VALIDATE(SSfxAsset, field_14, 0x14);
}
