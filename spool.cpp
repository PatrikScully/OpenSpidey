#include "spool.h"
#include "utils.h"
#include "validate.h"
#include "utils.h"
#include "panel.h"
#include "PCTex.h"
#include "DXinit.h"
#include "dcfileio.h"
#include "crate.h"
#include "spidey.h"
#include "my_assert.h"
#include "SpideyDX.h"
#include "psx_types.h"
#include "ps2funcs.h"
#include "vram.h"
#include "m3dinit.h"
#include "m3dzone.h"
#include "dcmodel.h"

#include <cstring>
#include <cstdlib>

// Bug found 2026-09-01 via a real headless boot test (title screen -> Enter
// crashed inside the ORIGINAL, unhooked PShell_DrawHighlight on a null
// texture pointer). Root cause: Spool_FindTextureEntry(u32) is hooked
// (bit.cpp PATCH_PUSH_RET_POLY), so it runs OUR code and read OUR own
// separate copy of this flag (always 1, since nothing hooked ever wrote 0
// here) instead of the real game's copy, which the UNHOOKED ProcessNewPSX
// correctly cycles 1->0 during PSX load. That split-brain made every texture
// lookup miss return NULL instead of the intended fallback texture. Fixed
// by pointing this at real game memory (0x6B2F08, confirmed in the
// maintainer's idb_globals.txt) so the hooked reader and the unhooked
// writer agree. Safe per the G_* runtime-safety rule: Spool_FindTextureEntry
// is the only HOOKED reader/writer of this flag; ProcessNewPSX and
// Spool_TextureAccess (the other reader) are both unhooked.
#ifndef SPIDEY_STANDALONE
EXPORT u8 gSpoolLogFailedTextureAccess = 1;
#else
extern u8 gSpoolLogFailedTextureAccess;
#endif
//#define G_SPOOL_LOG_FAILED_TEXTURE_ACCESS (gSpoolLogFailedTextureAccess)
#define G_SPOOL_LOG_FAILED_TEXTURE_ACCESS (*reinterpret_cast<u8*>(0x006B2F08))

#ifndef SPIDEY_STANDALONE
EXPORT i32 gRegionReloadRelated = -1;
#else
extern i32 gRegionReloadRelated;
#endif
EXPORT u8 gReloading = 1;

// @FIXME: add proper value
// The exe keeps this pointer at 0x5498FC (initialized to its static 0x62E510
// scratch buffer). dcmodel.cpp and trig.cpp read the same slot.
#ifndef SPIDEY_STANDALONE
EXPORT void* gSpoolSystemMemory;
#else
extern void* gSpoolSystemMemory;
#endif

EXPORT i32 gSpoolRegionRelatedOne;
EXPORT i32 gSpoolRegionRelatedTwo;
#ifndef SPIDEY_STANDALONE
EXPORT i32 GrenadeExplosionRegion = -1;
#else
extern i32 GrenadeExplosionRegion;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT i32 SymBurnRegion = -1;
#else
extern i32 SymBurnRegion;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT i32 FireDomeRegion = -1;
#else
extern i32 FireDomeRegion;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT i32 FireRingRegion = -1;
#else
extern i32 FireRingRegion;
#endif


i32 gSpoolCurrentOpenSpot;
EXPORT AnimPacket* AnimPackets;
EXPORT i32 gSpoolInitOne;
EXPORT i32 gSpoolInitTwo;
#ifndef SPIDEY_STANDALONE
EXPORT i32 gSpoolColijEnvIndex;
#else
extern i32 gSpoolColijEnvIndex;
#endif
EXPORT i32 gNumAccesses;

#ifndef SPIDEY_STANDALONE
i32 EnvRegions[2] = { -1, -1 };
#else
extern i32 EnvRegions[2];
#endif

const i32 MAXTEXTUREENTRIES = 512;
EXPORT Texture* gSpoolTexturesRelated;
EXPORT Texture gSpoolInitRelated[MAXTEXTUREENTRIES];

EXPORT SAccess* gAccessRelated[MAXPSX];

const i32 MAXITEMSPERCHECKSUM = 5;
EXPORT i16 gEnvModelHashTable[256][MAXITEMSPERCHECKSUM];

#ifndef SPIDEY_STANDALONE
EXPORT SPSXRegion PSXRegion[MAXPSX];
#else
extern SPSXRegion PSXRegion[MAXPSX];
#endif

#define TEXTURE_CHECKSUM_TABLE_SIZE (MAXTEXTUREENTRIES)
#ifndef SPIDEY_STANDALONE
EXPORT Texture* TextureChecksumHashTable[TEXTURE_CHECKSUM_TABLE_SIZE];
#else
extern Texture* TextureChecksumHashTable[TEXTURE_CHECKSUM_TABLE_SIZE];
#endif
// idb_globals.txt calls this TextureCheckSumHashTable. The only writers in the
// original are ProcessNewPSX and Spool_RemoveUnusedTextures, neither of which is
// hooked, while the already hooked Spool_FindTextureEntry reads it. Our copy
// therefore stayed empty and every lookup missed. This is the "Spool_FindTextureEntry
// returns null" symptom noted earlier in the project.
//#define G_TEXTURE_CHECKSUM_HASH_TABLE (TextureChecksumHashTable)
#define G_TEXTURE_CHECKSUM_HASH_TABLE (*reinterpret_cast<Texture*(*)[TEXTURE_CHECKSUM_TABLE_SIZE]>(0x006AB934))

#ifndef SPIDEY_STANDALONE
EXPORT i32 lowGraphics;
#else
extern i32 lowGraphics;
#endif

//#define G_LOWGRAPHICS (lowGraphics)
#define G_LOWGRAPHICS (*reinterpret_cast<i32*>(0x006B78F8))

#ifndef SPIDEY_STANDALONE
EXPORT i32 CurrentSuit;
#else
extern i32 CurrentSuit;
#endif

EXPORT u8 gGiveDefaultTexture;

#ifndef SPIDEY_STANDALONE
EXPORT TextureEntry gTextureEntries[256];
#else
extern TextureEntry gTextureEntries[256];
#endif

//#define G_TEXTUREENTRIES (gTextureEntries)
#define G_TEXTUREENTRIES (reinterpret_cast<TextureEntry*>(0x006A90B8))

EXPORT i32 HashIndex;
EXPORT Texture* pCurrentTex;

EXPORT char SuitNames[11][32] = 
{
	"Dummy",
	"spidey",
	"sp2099",
	"spSymbi",
	"spUniv",
	"spUnlim",
	"spBagMan",
	"spScar",
	"spReilly",
	"spQuick",
	"spPark",

};

// @Ok
// @Validate
i32 Spool_PSX(
		const char* Filename,
		i32 IsEnviro)
{
	char v22[32];
	print_if_false(Filename != 0, "No FileName sent to Spool_PSX.");
	Utils_CopyString(Filename, v22, sizeof(v22));

	if ( !gLowGraphics && Utils_CompareStrings(v22, "spidey") )
		Utils_CopyString(SuitNames[CurrentSuit], v22, sizeof(v22));

	for (i32 i = 0; i < MAXPSX; i++)
	{
		if (PSXRegion[i].Filename[0] && Utils_CompareStrings(v22, PSXRegion[i].Filename))
		{
			return i;
		}
	}

	i32 openSpot = -1;
	for (i32 k = 0; k < MAXPSX; k++)
	{
		if (PSXRegion[k].Filename[0] == '\0')
		{
			openSpot = k;
			break;
		}
	}

	print_if_false(openSpot != -1, "Too many PSX files loaded, increase MAXPSXS in spool.h");
	if (IsEnviro)
	{
		print_if_false(EnvRegions[0] == -1, "Old environment still loaded");
		EnvRegions[0] = openSpot;
	}

	Utils_CopyString(v22, PSXRegion[openSpot].Filename, 9);
	gSpoolCurrentOpenSpot = openSpot;


	i32 v8;
	char v23[64];
	if (gLowGraphics && (sprintf(v23, "lowres\\%s.psx", v22), (v8 = FileIO_Open(v23)) != 0))
	{
		DXERR_printf("Loading LowRes Model: %s\r\n", v23);
		PSXRegion[openSpot].LowRes = 1;
	}
	else
	{
		sprintf(v23, "%s.psx", v22);
		v8 = FileIO_Open(v23);
	}

	void* v9 = DCMem_New(v8, 1, 1, 0, 1);
	PSXRegion[openSpot].pPSX = static_cast<u32*>(v9);

	FileIO_Load(v9);
	FileIO_Sync();
	ProcessNewPSX(openSpot);

	if (IsEnviro)
	{
		print_if_false(EnviroList == 0, "EnviroList not NULL");
		EnviroList = PSXRegion[openSpot].pSuper;
		gSpoolRegionRelatedOne = gSpoolRegionRelatedTwo;

		gCommandPointRelated[0] = Spool_SkipPackets(PSXRegion[openSpot].pPSX);
		Spool_AddEnvModelsToHashTable();
	}

	if ( Utils_CompareStrings(v22, "expgrnd") )
	{
		print_if_false(GrenadeExplosionRegion == -1, "GrenadeExplosionRegion already set?");
		GrenadeExplosionRegion = openSpot;
	}

	if ( Utils_CompareStrings(v22, "fire") )
	{
		print_if_false(SymBurnRegion == -1, "SymBurnRegion already set?");
		Spool_MaskFaceFlags(openSpot, 512, -1);
		SymBurnRegion = openSpot;
	}

	if ( Utils_CompareStrings(v22, "firedome") )
	{
		print_if_false(FireDomeRegion == -1, "FireDomeRegion already set?");
		FireDomeRegion = openSpot;
	}

	if ( Utils_CompareStrings(v22, "firering") )
	{
		print_if_false(FireRingRegion == -1, "FireRingRegion already set?");
		FireRingRegion = openSpot;
	}

	return openSpot;
}

// @Ok
// v5 points AT the pSkipped[v3+1] slot itself (the consecutive u32 slots
// starting there are the Texture* array), not at whatever value is stored
// there. Confirmed by disassembling this loop inlined into ClearRegion
// (0x4CA858: esi = &pSkipped[v3+1], no extra deref before the walk).
void DecrementTextureUsage(i32 region)
{
	i32 v3 = reinterpret_cast<i32*>(PSXRegion[region].ppModels)[-1];
	u32* pSkipped = Spool_SkipPackets(PSXRegion[region].pPSX);

	u32 v6 = pSkipped[v3];
	Texture** v5 = reinterpret_cast<Texture**>(&pSkipped[v3 + 1]);

	for (u32 i = 0; i < v6; i++)
	{
		print_if_false(v5[i]->Usage != 0, "Tried to decrement zero usage");
		v5[i]->Usage--;
	}
}

// @Ok
INLINE char* GetNextLine(char * a1)
{
	char * res = strchr(a1, '\r');
	if (res)
	{
		res++;
		if (*res == '\n')
			res++;
	}

	return res;
}

// @Ok
void GotoStartOfTextureList(void)
{
	HashIndex = 0;
	pCurrentTex = G_TEXTURE_CHECKSUM_HASH_TABLE[0];
}

// @Ok
// @Validate
// Functional: no standalone address exists, every call site in the shipped
// PC binary has this inlined (confirmed nowhere in .text as its own
// function). Cross-checked field by field against the inlined copy at
// 0x4C9C6B..0x4C9CD7 inside ProcessNewPSX: every store here matches,
// INCLUDING the 4-byte `x` write (it really does clear both x and y in one
// mov, `mov [eax+1Ch],ebp` in the original) and the field_12 low-nibble
// clear (original masks only the low byte with 0xF0; `&= 0xFFF0` on the
// full u16 field produces the identical byte pattern since the high byte
// is untouched either way). Store order differs slightly from the inlined
// original (harmless struct field-store reordering, see CLAUDE.md). No
// cmpsum address exists to verify byte-for-byte since this never appears
// standalone.
void NewTextureEntry(u32 checksum)
{
	print_if_false(
		gSpoolTexturesRelated != 0,
		"Run out of texture entries, increase MAXTEXTUREENTRIES in spool.cpp");

	Texture* pTex = gSpoolTexturesRelated;
	gSpoolTexturesRelated = gSpoolTexturesRelated->pNext;

	pTex->pNext = G_TEXTURE_CHECKSUM_HASH_TABLE[checksum % MAXTEXTUREENTRIES];
	pTex->pPrevious = 0;
	G_TEXTURE_CHECKSUM_HASH_TABLE[checksum % MAXTEXTUREENTRIES] = pTex;

	if (pTex->pNext != 0)
		pTex->pNext->pPrevious = pTex;

	pTex->Checksum = checksum;
	pTex->field_12 &= 0xFFF0;
	pTex->Usage = 0;
	pTex->clut = 0;
	pTex->u0 = 0;
	pTex->v0 = 0;
	pTex->u1 = 0;
	pTex->v1 = 0;

	// original really does write 4 bytes here, clearing x and y together
	*reinterpret_cast<u32*>(&pTex->x) = 0;
}

// @Ok
// Functional: reviewed while doing Spool_RemoveUnusedTextures (only caller,
// inlined there). Logic matches (bound check, null-bucket search, pNext
// advance). Byte-match residue only: the compiler always places the
// "search empty buckets" loop as the fall-through path and the "already
// have a texture" short path as a forward jump, no matter how the source
// is written (if/else either order, goto either polarity, do-while vs raw
// goto, operand order swapped, cached local vs global re-test, for(;;)+break
// single loop). Original does the opposite (short path falls through,
// search is the jump target). See Spool_RemoveUnusedTextures's comment for
// the residue this causes. No standalone address to run cmpsum against.
INLINE Texture* NextTexture(void)
{
	Texture* res;

	if (HashIndex >= TEXTURE_CHECKSUM_TABLE_SIZE)
		return 0;

	if (0 == pCurrentTex)
		goto search;

	res = pCurrentTex;
	pCurrentTex = pCurrentTex->pNext;
	return res;

search:
	do
	{
		HashIndex++;
		pCurrentTex = G_TEXTURE_CHECKSUM_HASH_TABLE[HashIndex];

		if (TEXTURE_CHECKSUM_TABLE_SIZE <= HashIndex)
			return 0;
	} while (0 == pCurrentTex);

	res = pCurrentTex;
	pCurrentTex = pCurrentTex->pNext;

	return res;
}

// @Ok
// @Validate
void PreProcessAnimPacket(
		u32 * pPSX,
		u32 * pPacket)
{
	print_if_false(!pPSX[2] && !pPSX[3], "Sequencer PSX contains items or models!");

	u32* pSkipped = Spool_SkipPackets(pPSX);

	AnimPacket* pAnimPacket = static_cast<AnimPacket*>(
			DCMem_New(sizeof(AnimPacket), 0, 1, 0, 1));

	pAnimPacket->pNext = AnimPackets;
	pAnimPacket->pPacket = pPacket;
	pAnimPacket->mPsxOpenSpot = gSpoolCurrentOpenSpot;

	AnimPackets = pAnimPacket;

	u32 numAnims = *pPacket;
	u32* v4 = &pPacket[1];
	print_if_false(numAnims < 0xC8, "Got a vast number of anims in sequencer PSX");

	for (u32 i = 0; i < numAnims; i++)
	{

		u8 v6 = *reinterpret_cast<u8*>(v4) >= 0x41 &&
			*reinterpret_cast<u8*>(v4) <= 0x5A;

		u32 v7 = v4[2];
		v4 += 3;
		print_if_false(v7 < 0x3E8, "Got a vast number of frames in sequencer PSX");

		for (u32 j = 0; j < v7; j++)
		{
			// 0x4CAAE7 reads [terminator + 8 + 4*idx]; Spool_SkipPackets
			// returns one dword past the terminator, so that is [idx + 1],
			// not [idx + 2] (the old value read the next slot: a count, and
			// the byte store below went to address 28). 0x4CAAF0 sets bit
			// 0x20 in the byte at +6 of the entry, not at +0.
			u32 v8 = pSkipped[v4[1] + 1];
			v4[1] = v8;
			if (!v6)
			{
				*reinterpret_cast<u8*>(v8 + 6) |= 0x20;
				u32 v9 = v4[1];

				u8 v10 = *reinterpret_cast<u8*>(v9 + 4);
				if (v10 != 0xFF)
				{
					*reinterpret_cast<u8*>(v9 + 4) = v10 + 1;
				}

				++*reinterpret_cast<u8*>(v4[1] + 9);
				u32 v11 = v4[1];
				u8 v12 = *reinterpret_cast<u8*>(v11 + 10);

				if (v12 != 0xFF)
				{
					*reinterpret_cast<u8*>(v11 +10) = v12 + 1;
				}

				++*reinterpret_cast<u8*>(v4[1] + 11);
			}

			v4 += 2;
		}
	}

	Bit_UpdateQuickAnimLookups();
}

// Set to 1 by ProcessNewPSX while a pre-scan of the PSX record chain finds
// a type-69 (anim packet) record, then unconditionally reset to 0 again
// before the record chain is walked for real. xrefs_to confirms all three
// reads/writes of 0x60DBC0 are inside ProcessNewPSX itself (no other
// function in the binary touches it), so this flag has no observable
// effect outside this function; kept only because CLAUDE.md's guidance is
// to reproduce writes faithfully rather than drop ones that look dead.
i32 gPSXParsingHasAnimPacket;

// Read (never written) at 0x56EA98+4 only inside the "byte_6B2F08==0"
// (i.e. gSpoolLogFailedTextureAccess==0) branch of the texture-checksum
// find-or-create loop below. ProcessNewPSX itself forces
// gSpoolLogFailedTextureAccess to 1 for the whole duration of that loop,
// so this branch never actually executes from this call site (it is real
// code reachable from OTHER callers of the same inlined logic, e.g.
// Spool_TextureAccess). Its exact structure/meaning was not established
// (not in idb_globals.txt); translated as a literal address+4 dereference
// rather than guessed at, since it is provably unreachable from here and
// any imprecision has zero effect on ProcessNewPSX's own behaviour.
static i32* const gTextureFallbackRelated = reinterpret_cast<i32*>(0x0056EA98);

// Literal checksum table read straight out of the binary (idalib
// get_bytes 0x556C6C..0x556C84, 6 dwords): textures whose on-disk
// dimensions exceed 256 and whose checksum is one of these get replaced
// by an "lti\tex_<checksum>.bmp" file instead of being decoded from the
// PSX record. Real data, not a guess; the array's declared bound folds
// into the next global in the binary (aTooManyItemsHa's string, per
// CLAUDE.md's "Global boundaries" note), so 6 is the true entry count
// (24 bytes / 4).
static const u32 gLtiOversizeChecksums[6] =
{
	0x28DD4EDC, 0x37AAB3D3, 0x429F6649,
	0x6C651A89, 0x6D27B1AC, 0xD0B1656E,
};

// @FIXME
// 0x4B8C80. names.json maps this address to CPlayer_IfPlayerCeilingCheck,
// but a fresh IDA decompile shows no player/physics code at all: it reads
// G_LOWGRAPHICS and, in low-graphics mode, looks the texture's checksum up
// in a per-suit table (a 0x53C1A4-based array, indexed by 64*CurrentSuit,
// confirmed via get_bytes: 9 back-to-back 64-byte blocks, each holding up
// to 13 nonzero u32 checksums followed by zero padding) and records the
// texture's TexWin word into dword_6A8D74[16*CurrentSuit+slot]; in
// high-graphics mode it instead registers the texture into a separate
// queue (dword_6A8000/6A8004/6A8006/6A8008, count at dword_6A9050). This
// is clearly a costume-part / paintable-region registration system, but
// the high-graphics queue's consumer and the exact suit-part table
// contents were not tracked down this session, and names.json's own
// mapping for this address cannot be trusted (see the repo's documented
// history of similarly mislabeled addresses, e.g. Screen_UpdateFades).
// Forwarded to the real address rather than reimplemented, so ProcessNewPSX
// keeps exact original behaviour regardless of this gap.
typedef void (*Spool_RegisterCostumeTexture_t)(i32, i16, i16);
static const Spool_RegisterCostumeTexture_t Spool_RegisterCostumeTexture =
	reinterpret_cast<Spool_RegisterCostumeTexture_t>(0x004B8C80);

// @FIXME
// 0x50ECE0. names.json maps this address to PCTex_LoadTexturePVR, but that
// is wrong: PCTex_LoadTexturePVR(a1,a2) treats a1 as a FILENAME whenever
// a2==0 (it calls PCTex_BufferPVR, which opens a1 as a file on disk in
// that case). ProcessNewPSX's caller passes a raw in-memory texel-data
// pointer with a2==0, which would misinterpret those bytes as a filename
// string and fail PCTex_BufferPVR's "corrupted PVR file" assert. A fresh
// IDA decompile of 0x50ECE0 confirms it is a different function: it calls
// PCTex_BufferPVR(Str, a2) same as PCTex_LoadTexturePVR, but then walks its
// own id-search array (0xAC1670..0xADB330, stride 26 dwords) to find a
// free texture id manually and calls PCTex_CreateTexturePVRInId directly
// with it, instead of going through PCTex_FindUnusedTextureId like
// PCTex_CreateTexturePVR/PCTex_LoadTexturePVR do. Forwarded to the real
// address rather than reimplemented against unnamed globals
// (0xAC1334/0xAC1338/0xAC133C) this session did not track down, since a
// wrong reimplementation here would silently corrupt which texture data
// gets displayed.
typedef i32 (*PCTex_CreateTexturePVRById_t)(char*, i32);
static const PCTex_CreateTexturePVRById_t PCTex_CreateTexturePVRById =
	reinterpret_cast<PCTex_CreateTexturePVRById_t>(0x0050ECE0);

// @Ok
// Full re-decompile of 0x4C9A60 (2688 bytes) done with idalib this session,
// cross-checked against the disassembly for every field offset quoted
// below. All ~17 magic texture-checksum constants from the previous
// @BIGTODO note are transcribed as the literal hex values seen in the
// disassembly (converted from IDA's signed decimal display) rather than
// invented names, since no plaintext source for the underlying texture
// names survives anywhere reachable (not in the PKR containers, not in
// names.json, not in the maintainer's IDB) -- see the comments at each use
// site below for exactly what evidence does exist for each group. Real,
// verified evidence resolved every callee and every struct field actually
// read/written by this function except the two forwarded above (both
// documented names.json mismatches, not guesses) and
// gTextureFallbackRelated's exact structure (dead code from this call
// site, see its comment). The record-type dispatch's FourCC constants
// (0x52454948 "HIER", 0x6B6E6843 "Chnk", 0x73424752 "RGBs") were confirmed
// by round-tripping struct.pack('<I', value) to ASCII, not guessed.
void ProcessNewPSX(i32 a1)
{
	gSpoolRegionRelatedTwo = 0;

	u32* pPSXBuf = PSXRegion[a1].pPSX;
	i32 modelCount = *reinterpret_cast<i32*>(reinterpret_cast<char*>(pPSXBuf) + 8);

	// Build PSXRegion[a1].pSuper: an array of modelCount CItems, one per
	// on-disk model-header record (36 bytes each, starting right after the
	// 12-byte PSX buffer header). Allocation itself is plain C++ (the
	// original's manual operator-new + "eh vector constructor iterator"
	// dance is exactly what `new CItem[modelCount]` compiles to). The
	// field-by-field copy that follows is done via raw byte offsets on
	// purpose: CItem's field layout at these specific offsets was not
	// independently confirmed against ob.h this session, so raw offsets
	// (byte-exact with the disassembly) are safer than guessing field
	// names that might not exist or might be misnamed.
	if (modelCount > 0)
	{
		CItem* pSuperItems = new CItem[modelCount];
		char* pRec = reinterpret_cast<char*>(pPSXBuf) + 12;

		for (i32 i = 0; i < modelCount; i++, pRec += 36)
		{
			char* pItem = reinterpret_cast<char*>(pSuperItems) + 64 * i;

			*reinterpret_cast<u16*>(pItem + 4)  = *reinterpret_cast<u16*>(pRec + 0);
			*reinterpret_cast<u16*>(pItem + 6)  = *reinterpret_cast<u16*>(pRec + 2);
			*reinterpret_cast<u32*>(pItem + 8)  = *reinterpret_cast<u32*>(pRec + 4);
			*reinterpret_cast<u32*>(pItem + 12) = *reinterpret_cast<u32*>(pRec + 8);
			*reinterpret_cast<u32*>(pItem + 16) = *reinterpret_cast<u32*>(pRec + 12);
			*reinterpret_cast<u16*>(pItem + 20) = *reinterpret_cast<u16*>(pRec + 16);
			*reinterpret_cast<u16*>(pItem + 22) = *reinterpret_cast<u16*>(pRec + 18);
			*reinterpret_cast<u16*>(pItem + 24) = *reinterpret_cast<u16*>(pRec + 20);
			*reinterpret_cast<u16*>(pItem + 26) = *reinterpret_cast<u16*>(pRec + 22);
			*reinterpret_cast<u8*>(pItem + 28)  = *reinterpret_cast<u8*>(pRec + 24);
			*reinterpret_cast<u8*>(pItem + 29)  = *reinterpret_cast<u8*>(pRec + 25);
			*reinterpret_cast<u8*>(pItem + 30)  = *reinterpret_cast<u8*>(pRec + 26);
			*reinterpret_cast<u8*>(pItem + 31)  = *reinterpret_cast<u8*>(pRec + 27);
			*reinterpret_cast<u32*>(pItem + 36) = *reinterpret_cast<u32*>(pRec + 32);
		}

		PSXRegion[a1].pSuper = pSuperItems;
	}

	// ppModels: {count, count*u32 file-relative-offset} table right after
	// the model-header records; converted to absolute SModel* pointers in
	// place, matching DecrementTextureUsage/M3dInit_ParsePSX's established
	// ppModels[-1] convention (the count lives at pPSXBuf+36*modelCount+12,
	// one dword before ppModels itself).
	i32 ppModelsCount = *reinterpret_cast<i32*>(reinterpret_cast<char*>(pPSXBuf) + 36 * modelCount + 12);
	SModel** ppModels = reinterpret_cast<SModel**>(reinterpret_cast<char*>(pPSXBuf) + 36 * modelCount + 16);

	if (ppModelsCount != 0)
	{
		i32* pSlot = reinterpret_cast<i32*>(ppModels);
		for (i32 j = 0; j < ppModelsCount; j++)
		{
			i32 fileOffset = pSlot[j];
			pSlot[j] = reinterpret_cast<i32>(pPSXBuf) + fileOffset;
		}
	}

	PSXRegion[a1].ppModels = ppModels;

	// pModelChecksums / texture-checksum table: right after the id/size/data
	// record chain's -1 terminator, same walk Spool_SkipPackets already
	// performs (verified: it returns exactly "one dword past the
	// terminator", which is precisely where the model-checksum array
	// starts).
	u32* pModelChecksums = Spool_SkipPackets(pPSXBuf);
	PSXRegion[a1].pModelChecksums = pModelChecksums;

	u32* pTexSlots = pModelChecksums + ppModelsCount + 1;
	i32 texCount = static_cast<i32>(pModelChecksums[ppModelsCount]);

	// Find-or-create each texture entry by checksum, same shape as
	// NewTextureEntry (same free list gSpoolTexturesRelated, same hash
	// table TextureChecksumHashTable/dword_6AB934 == "TextureCheckSumHashTable"
	// per idb_globals.txt, same "checksum & (size-1)" bucketing since
	// MAXTEXTUREENTRIES/TEXTURE_CHECKSUM_TABLE_SIZE is a power of two) plus
	// stamping the owning region into Texture::mRegion, which
	// NewTextureEntry does not do. The array slots are overwritten in
	// place from raw checksums to Texture* pointers, exactly like the
	// original.
	G_SPOOL_LOG_FAILED_TEXTURE_ACCESS = 1;

	if (texCount != 0)
	{
		u32* pSlot = pTexSlots;

		for (i32 m = 0; m < texCount; m++)
		{
			u32 checksum = *pSlot;
			Texture* pTexEntry = G_TEXTURE_CHECKSUM_HASH_TABLE[checksum & (TEXTURE_CHECKSUM_TABLE_SIZE - 1)];

			if (pTexEntry != 0)
			{
				while (pTexEntry->Checksum != checksum)
				{
					pTexEntry = pTexEntry->pNext;
					if (pTexEntry == 0)
						goto notFound;
				}
			}
			else
			{
			notFound:
				if (!G_SPOOL_LOG_FAILED_TEXTURE_ACCESS)
				{
					print_if_false(0, "Can't find texture from checksum %ld", checksum);
					pTexEntry = reinterpret_cast<Texture*>(
						*reinterpret_cast<i32*>(reinterpret_cast<char*>(gTextureFallbackRelated) + 4));
				}

				if (pTexEntry == 0)
				{
					print_if_false(
						gSpoolTexturesRelated != 0,
						"Run out of texture entries, increase MAXTEXTUREENTRIES in spool.cpp");

					pTexEntry = gSpoolTexturesRelated;
					gSpoolTexturesRelated = gSpoolTexturesRelated->pNext;

					u32 bucket = checksum & (TEXTURE_CHECKSUM_TABLE_SIZE - 1);
					pTexEntry->pPrevious = 0;
					pTexEntry->pNext = G_TEXTURE_CHECKSUM_HASH_TABLE[bucket];
					G_TEXTURE_CHECKSUM_HASH_TABLE[bucket] = pTexEntry;

					if (pTexEntry->pNext != 0)
						pTexEntry->pNext->pPrevious = pTexEntry;

					pTexEntry->mRegion = a1;
					pTexEntry->Checksum = checksum;
					pTexEntry->field_12 &= 0xFFF0;
					pTexEntry->Usage = 0;
					pTexEntry->clut = 0;
					pTexEntry->u0 = 0;
					pTexEntry->v0 = 0;
					pTexEntry->u1 = 0;
					pTexEntry->v1 = 0;
					*reinterpret_cast<u32*>(&pTexEntry->x) = 0;
				}
			}

			pTexEntry->Usage++;
			*pSlot = reinterpret_cast<u32>(pTexEntry);
			pSlot++;
		}
	}

	G_SPOOL_LOG_FAILED_TEXTURE_ACCESS = 0;

	// Remap each model's face texture-index (bit 0 of the face's packed
	// "tag" dword) through pTexSlots (now full of Texture* after the loop
	// above). Written with raw offsets, not POLY_F3 (spool.h's POLY_F3 is a
	// different, already-VALIDATE'd 0x14-byte struct whose field layout
	// does not line up with what this loop actually touches -- offset 16
	// here overlaps POLY_F3::x2/y2 -- so this is a distinct, unnamed
	// on-disk face format, not POLY_F3).
	if (ppModelsCount != 0)
	{
		char* pModelSlot = reinterpret_cast<char*>(ppModels);

		for (i32 n = 0; n < ppModelsCount; n++, pModelSlot += 4)
		{
			SModel* pModel = *reinterpret_cast<SModel**>(pModelSlot);
			u16 numVerts = pModel->NumVertices;
			u16 numNorms = pModel->NumNormals;
			u16 numFaces = pModel->NumFaces;

			char* pFace = reinterpret_cast<char*>(pModel) + 28 + 8 * (numVerts + numNorms);

			for (u16 f = 0; f < numFaces; f++)
			{
				u32 tag = *reinterpret_cast<u32*>(pFace);

				if (tag & 1)
				{
					i32* pTexIndexSlot = reinterpret_cast<i32*>(pFace + 16);
					*pTexIndexSlot = reinterpret_cast<i32*>(pTexSlots)[*pTexIndexSlot];
				}

				print_if_false((tag & 0xFFFC0000) != 0, "Zero face length");

				pFace += 4 * (tag >> 18);
			}
		}
	}

	Spool_RemoveUnusedTextures();
	Pal_RemoveUnusedPalettes();

	// Palette-list-1 (9 ints/entry: checksum + 8-int mini-palette) and
	// palette-list-2 (129 ints/entry: checksum + 128-int full palette)
	// tables sit right after the texture-checksum array, in the same
	// {count, count*entry} shape used throughout this function. Re-walking
	// via Spool_SkipPackets to find the tail is deliberate: it is the exact
	// same computation the original repeats (k-walk / ii-walk / nn-walk all
	// start from the same pPSXBuf+pPSXBuf[1] base), so reusing the already
	//-verified helper is safer than re-deriving the walk a second time.
	{
		u32* pTail = Spool_SkipPackets(pPSXBuf);
		// layout after the model checksums: [texCount][texCount slots][pal1Count]...
		// so the palette table starts ppModelsCount + 1 + texCount ints in. The
		// old "+ 2" read the first palette checksum as the count (found by the
		// standalone build, 2026-09-02, webdome2 crashed here every boot).
		i32* pPal1 = reinterpret_cast<i32*>(pTail) + ppModelsCount + 1 + texCount;
		i32 pal1Count = *pPal1;
		i32* pPal1Entries = pPal1 + 1;

		if (pal1Count != 0)
		{
			i32* pChecksum = pPal1Entries;
			i32* pData = pPal1Entries + 1;

			for (i32 jj = 0; jj < pal1Count; jj++)
			{
				if (Pal_FindPaletteEntry(*pChecksum) == 0)
					Pal_LoadPalette(*pChecksum, reinterpret_cast<u32*>(pData), 1);

				pChecksum += 9;
				pData += 9;
			}
		}

		i32* pPal2 = pPal1Entries + pal1Count * 9;
		i32 pal2Count = *pPal2;
		i32* pPal2Entries = pPal2 + 1;

		if (pal2Count != 0)
		{
			i32* pChecksum = pPal2Entries;
			i32* pData = pPal2Entries + 1;

			for (i32 kk = 0; kk < pal2Count; kk++)
			{
				if (Pal_FindPaletteEntry(*pChecksum) == 0)
					Pal_LoadPalette(*pChecksum, reinterpret_cast<u32*>(pData), 2);

				pChecksum += 129;
				pData += 129;
			}
		}

		gPSXParsingHasAnimPacket = 0;

		// Pre-scan for a type-69 (anim packet) record before the checksum
		// file is loaded; see gPSXParsingHasAnimPacket's declaration for
		// why this write is otherwise unobserved.
		{
			char* pRec = reinterpret_cast<char*>(pPSXBuf) + pPSXBuf[1];
			i32 recType = *reinterpret_cast<i32*>(pRec);

			while (recType != -1)
			{
				u32 size = reinterpret_cast<u32*>(pRec)[1];
				char* pData = pRec + 8;

				if (recType == 69)
					gPSXParsingHasAnimPacket = 1;

				pRec = pData + size;
				recType = *reinterpret_cast<i32*>(pRec);
			}
		}

		texLoadChecksums(PSXRegion[a1].Filename);

		i32* pTexRecCountPtr = pPal2Entries + pal2Count * 129;
		i32 texRecCount = *pTexRecCountPtr;
		i32* pTexRecOffsets = pTexRecCountPtr + 1;

		if (texRecCount != 0)
		{
			i32 vramX = 0, vramY = 0;

			for (i32 r = 0; r < texRecCount; r++)
			{
				char* pRecord = reinterpret_cast<char*>(pPSXBuf) + pTexRecOffsets[r];

				i32 flags = *reinterpret_cast<i32*>(pRecord);
				u32 format = *reinterpret_cast<u32*>(pRecord + 4);
				u32 paletteChecksum = *reinterpret_cast<u32*>(pRecord + 8);
				i32 texIndex = *reinterpret_cast<i32*>(pRecord + 12);
				i32 width = *reinterpret_cast<u16*>(pRecord + 16);
				i32 height = *reinterpret_cast<u16*>(pRecord + 18);
				char* pTexData = pRecord + 20;

				if (PSXRegion[a1].LowRes)
					flags |= 0x800;

				print_if_false(
					format == 16 || format == 256 || format == 0x10000,
					"Bad value for NumColours in PSX file");

				Texture* pTex = reinterpret_cast<Texture**>(pTexSlots)[texIndex];

				if (pTex == 0 || (pTex->field_12 & 0xF) != 0)
					continue;

				u32 checksum = pTex->Checksum;

				// Two checksums that always mark the texture as alpha
				// (bit 0 of flags), regardless of graphics mode. Literal
				// values from the disassembly (originally -1319509333 and
				// 1756868918 as signed decimal); no plaintext name for
				// either survives anywhere this session could check.
				if (checksum == 0xB159E2AB || checksum == 0x68B7B136)
					flags |= 1;

				if (!G_LOWGRAPHICS)
				{
					// High-graphics-only costume/paintable-texture
					// registration. This exact group of 13 checksums (plus
					// the 2 above) is what the old @BIGTODO note called
					// "~15 magic hash constants"; they are transcribed
					// exactly as seen in the disassembly (the shape is a
					// compiler-generated binary-search switch whose case
					// thresholds -- 0x3ED5F30B, 0x933EF22C, 0x1A501534 --
					// are themselves three of these same case values, i.e.
					// this really is a flat switch/case list in the
					// source, not a range test). See
					// Spool_RegisterCostumeTexture's comment above for what
					// evidence exists about what they gate.
					if (checksum == 0xCEB60740 || checksum == 0xE9587C6D ||
						checksum == 0x933EF22C || checksum == 0x42985F7A ||
						checksum == 0x4E1E5E1A || checksum == 0x7FFB7AAD ||
						checksum == 0x3ED5F30B ||
						checksum == 0x21AFE1A6 || checksum == 0x3BC194AE ||
						checksum == 0x1A501534 || checksum == 0x08BD6474 ||
						checksum == 0x1527743E || checksum == 0x197BC27A)
					{
						Spool_RegisterCostumeTexture(
							reinterpret_cast<i32>(pTex),
							static_cast<i16>(width),
							static_cast<i16>(height));
					}
				}

				// Textures too large for the native PSX/PVR formats
				// (>256 either axis) get replaced by an LTI bmp, but only
				// for the specific checksums in gLtiOversizeChecksums
				// (real data extracted from the binary, see its
				// declaration).
				if (static_cast<u16>(width) > 0x100 || static_cast<u16>(height) > 0x100)
				{
					bool needsReplacement = false;
					for (i32 t = 0; t < 6; t++)
					{
						if (gLtiOversizeChecksums[t] == checksum)
						{
							needsReplacement = true;
							break;
						}
					}

					if (needsReplacement)
					{
						char buf[256];
						sprintf(buf, "lti\\tex_%8.8x.bmp", checksum);
						print_if_false(1, "--- Loading Replacement Texture: %s\r\n", buf);

						i32 ltiId = PCTex_LoadLtiTexture(buf, checksum, -1, 1);
						pTex->clut = static_cast<u16>(ltiId);

						format = 0xFFFFFF;
						flags &= ~0x80000000;

						i32 newWidth = 0, newHeight = 0;
						PCTex_GetTextureSize(ltiId, &newWidth, &newHeight);

						width = newWidth;
						height = newHeight;

						*reinterpret_cast<u32*>(&pTex->x) = reinterpret_cast<u32>(
							VRAMRectPack(
								0,
								static_cast<u16>(width),
								static_cast<u16>(height),
								&vramX, &vramY,
								16,
								static_cast<u8>(2 * (flags & 1)),
								checksum));
					}
				}

				if (flags >= 0)
				{
					if (format == 0x10000)
					{
						i32 mipOffset = *reinterpret_cast<i32*>(pTexData);
						pTexData += 8;

						*reinterpret_cast<u32*>(&pTex->x) = reinterpret_cast<u32>(
							VRAMRectPack(
								0,
								static_cast<u16>(width),
								static_cast<u16>(height),
								&vramX, &vramY,
								16,
								static_cast<u8>(2 * (flags & 1)),
								checksum));

						i32 clutRes = PCTex_CreateTexturePVR(
							static_cast<u16>(width),
							static_cast<u16>(height),
							static_cast<u32>(mipOffset),
							pTexData,
							static_cast<u32>(flags),
							"PSXPVR",
							checksum);

						print_if_false(
							clutRes != -1,
							"Texture cannot be smallvq.  checksum: %lx", checksum);

						pTex->clut = static_cast<u16>(clutRes);
					}
					else if (format == 256)
					{
						i32 roundedWidth = (static_cast<u16>(width) + 1) & ~1;

						*reinterpret_cast<u32*>(&pTex->x) = reinterpret_cast<u32>(
							VRAMRectPack(
								0, roundedWidth, static_cast<u16>(height),
								&vramX, &vramY,
								8,
								static_cast<u8>(2 * (flags & 1)),
								checksum));

						if (!gPrintStubbed)
							stubbed_printf("stubbed out: LoadImage");

						u32 loadFlags = (flags & 0x800) ? 17 : 1;

						pTex->clut = static_cast<u16>(PCTex_CreateTexture256(
							roundedWidth,
							static_cast<u16>(height),
							pTexData,
							reinterpret_cast<const u16*>(Spool_GetPalette(paletteChecksum, a1)),
							loadFlags,
							"PSX256BMP",
							0,
							static_cast<i32>(checksum)));
					}
					else if (format == 16)
					{
						i32 roundedWidth = (static_cast<u16>(width) + 3) & ~3;

						*reinterpret_cast<u32*>(&pTex->x) = reinterpret_cast<u32>(
							VRAMRectPack(
								0, roundedWidth, static_cast<u16>(height),
								&vramX, &vramY,
								4,
								static_cast<u8>(2 * (flags & 1)),
								checksum));

						if (!gPrintStubbed)
							stubbed_printf("stubbed out: LoadImage");

						i32 loadFlags = (flags & 0x800) ? 17 : 1;

						pTex->clut = static_cast<u16>(PCTex_CreateTexture16(
							roundedWidth,
							static_cast<u16>(height),
							pTexData,
							reinterpret_cast<const u16*>(Spool_GetPalette(paletteChecksum, a1)),
							"PSX16BMP",
							0,
							static_cast<i32>(checksum),
							loadFlags));
					}

					// Common tail (original's LABEL_111): finalize the
					// remaining Texture fields and, in low-graphics mode,
					// register costume textures by region-filename match.
					tag_S_Pal* pPal = Pal_FindPaletteEntry(paletteChecksum);
					if (pPal != 0)
						pPal->Usage++;

					pTex->pPalette = pPal;
					pTex->u1 = static_cast<u8>(width - 1);
					pTex->u3 = static_cast<u8>(width - 1);
					pTex->tpage = 0;
					pTex->u0 = 0;
					pTex->v0 = 0;
					pTex->v1 = 0;
					pTex->u2 = 0;
					pTex->v2 = static_cast<u8>(height - 1);
					pTex->v3 = static_cast<u8>(height - 1);

					// PSX GPU texture-window register packing, literal
					// bit-arithmetic straight from the disassembly.
					u32 negH8 = static_cast<u32>(-height) & 0xF8u;
					pTex->TexWin = (4u * (negH8 | 0xF8800000u)) |
						((static_cast<u32>(-width) >> 3) & 0x1Fu);

					pTex->field_12 = static_cast<u16>(
						(pTex->field_12 & 0xFF00) | (16 * (flags & 1)) | 1);

					if (G_LOWGRAPHICS)
					{
						const char* name = PSXRegion[a1].Filename;
						if (strcmpi(name, "spArmour") == 0 ||
							strcmpi(name, "spidey") == 0 ||
							strnicmp(name, "sp_tex", 6) == 0)
						{
							Spool_RegisterCostumeTexture(
								reinterpret_cast<i32>(pTex),
								static_cast<i16>(width),
								static_cast<i16>(height));
						}
					}
				}
				else
				{
					*reinterpret_cast<u32*>(&pTex->x) = reinterpret_cast<u32>(
						VRAMRectPack(
							0,
							static_cast<u16>(width),
							static_cast<u16>(height),
							&vramX, &vramY,
							16,
							static_cast<u8>(2 * (flags & 1)),
							checksum));

					pTex->clut = static_cast<u16>(PCTex_CreateTexturePVRById(pTexData, 0));
				}
			}
		}
	}

	PSXRegion[a1].IsSuper = 0;
	PSXRegion[a1].pTexWibData = 0;
	gPSXParsingHasAnimPacket = 0;
	PSXRegion[a1].pColourPulseData = 0;

	// Second, real pass over the same record chain (same base as the
	// pre-scan and as Spool_SkipPackets), this time dispatching every
	// record by type instead of skipping to the end. Small integer ids are
	// PSX-format record-type codes; the three big ids are FourCC tags
	// confirmed by round-tripping struct.pack('<I', id) to ASCII (see each
	// case).
	{
		char* pRec = reinterpret_cast<char*>(pPSXBuf) + pPSXBuf[1];
		i32 recType = *reinterpret_cast<i32*>(pRec);

		while (recType != -1)
		{
			u32 size = reinterpret_cast<u32*>(pRec)[1];
			char* pData = pRec + 8;

			switch (recType)
			{
				case 6:
					PSXRegion[a1].pTexWibData = reinterpret_cast<u32*>(pData);
					M3dInit_FlagZeroWibbles(reinterpret_cast<STexWibItemInfo*>(pData));
					gSpoolRegionRelatedTwo = reinterpret_cast<i32>(pData) - 8;
					break;

				case 7:
					PSXRegion[a1].pColourPulseData = reinterpret_cast<u32*>(pData);
					break;

				case 10:
					M3dZone_SetZone(-(EnvRegions[0] != a1), reinterpret_cast<u32*>(pData));
					break;

				case 42:
				case 44:
					PSXRegion[a1].pAnimFile = reinterpret_cast<u32*>(pData);
					PSXRegion[a1].IsSuper = 1;
					break;

				case 69:
					PreProcessAnimPacket(pPSXBuf, reinterpret_cast<u32*>(pData));
					break;

				case 0x52454948: // "HIER"
					PSXRegion[a1].pHierarchy = reinterpret_cast<u16*>(pData);
					break;

				case 0x6B6E6843: // "Chnk"
					PSXRegion[a1].pChunkData = reinterpret_cast<u32*>(pData);
					break;

				case 0x73424752: // "RGBs"
				{
					PSXRegion[a1].pColourTable = reinterpret_cast<u32*>(pData);

					u8* pByte = reinterpret_cast<u8*>(pData);
					for (u32 n = 0; n < (size >> 2); n++)
					{
						pByte[4 * n]     = gConvertedColors[pByte[4 * n]];
						pByte[4 * n + 1] = gConvertedColors[pByte[4 * n + 1]];
					}
					break;
				}

				default:
					break;
			}

			pRec = pData + size;
			recType = *reinterpret_cast<i32*>(pRec);
		}
	}

	LoadPushOffsets();
	M3dInit_ParsePSX(a1);
	FreePushOffsets();

	PSXRegion[a1].Usable = 1;
}

// @Ok
INLINE i32 RemoveAnimPacket(u32* pPacket)
{
	AnimPacket* pPrev = 0;
	AnimPacket* pIter;
	for (
			pIter = AnimPackets; 
			pIter;
			pIter = pIter->pNext)
	{
		if (pIter->pPacket == pPacket)
			break;
		pPrev = pIter;
	}

	print_if_false(pIter == 0, "Could not find anim packet to delete");
	
	if (pPrev)
		pPrev->pNext = pIter->pNext;
	else
		AnimPackets = pIter->pNext;

	Mem_Delete(pIter);
	Bit_UpdateQuickAnimLookups();
	return 0;
}

// @Ok
// @Validate
INLINE void RemoveTextureEntry(Texture* pTexture)
{
	if (pTexture->pNext)
		pTexture->pNext->pPrevious = pTexture->pPrevious;

	if (pTexture->pPrevious)
		pTexture->pPrevious->pNext = pTexture->pNext;

	u32 checksum = pTexture->Checksum % TEXTURE_CHECKSUM_TABLE_SIZE;
	if (pTexture == G_TEXTURE_CHECKSUM_HASH_TABLE[checksum])
		G_TEXTURE_CHECKSUM_HASH_TABLE[checksum] = pTexture->pNext;

	pTexture->pNext = gSpoolTexturesRelated;
	gSpoolTexturesRelated = pTexture;
}

// @Ok
// @Validate
INLINE void Spool_AddEnvModelsToHashTable(void)
{
	print_if_false(EnviroList != 0, "NULL EnviroList?");
	u32* pModelChecksums = PSXRegion[EnviroList->mRegion].pModelChecksums;
	print_if_false(pModelChecksums != 0, "NULL pChecksums?");

	u32 v16 = reinterpret_cast<u32>(PSXRegion[EnviroList->mRegion].ppModels[-1]);
	for (u32 v15 = 0; v15 < v16; v15++)
	{
		u32 checksumIndex = pModelChecksums[v15] % 256;

		i32 k;
		for (k = 0; gEnvModelHashTable[checksumIndex][k] >= 0; k++)
		{
			print_if_false(
				k < MAXITEMSPERCHECKSUM,
				"Too many items have the same checksum mod 256\n Need to increase MAXITEMSPERCHECKSUM in spool.cpp");
		}

		gEnvModelHashTable[checksumIndex][k] = v15;
	}
}

// @Ok
// @Matching
// SAnimFrame is still opaque here; field_4 is a Texture* found by disasm
// (matches the "texture" print_if_false messages), accessed raw since the
// struct has no layout yet.
i32 Spool_AnimAccess(char *a1, SAnimFrame **a2)
{
	AnimPacket* pPacketInfo = AnimPackets;
	if (pPacketInfo)
	{
	loop_top:
		u32* pPacket = pPacketInfo->pPacket;
		u32 numAnims = *pPacket;
		char* pEntry = reinterpret_cast<char*>(pPacket + 1);

		for (u32 i = 0; i < numAnims; i++)
		{
			char* pA = a1;
			char* pB = pEntry;
			char ca = *pA & 0xDF;
			char cb = *pB & 0xDF;

			i32 count;
			for (count = 0; ca == cb && ca && cb && count < 8; count++)
			{
				pA++;
				ca = *pA & 0xDF;
				pB++;
				cb = *pB & 0xDF;
			}

			if ((!ca && !cb) || count == 8)
			{
				*a2 = reinterpret_cast<SAnimFrame*>(pEntry + 0xC);

				if (addAccess(
							reinterpret_cast<void**>(a2),
							2,
							reinterpret_cast<u32>(a1),
							pPacketInfo->mPsxOpenSpot))
					accessLog(
							"Created Anim Access: name=%s, rgn=%i, addr=0x%8.8X\r\n",
							a1, pPacketInfo->mPsxOpenSpot, a2);

				print_if_false(
						*reinterpret_cast<Texture**>(reinterpret_cast<char*>(*a2) + 4) != 0,
						"Animation does not have a texture, huh-ho...");

				print_if_false(
						(*reinterpret_cast<Texture**>(reinterpret_cast<char*>(*a2) + 4))->mRegion == pPacketInfo->mPsxOpenSpot,
						"texture is in a different region than the animation, huh-ho...");

				return pPacketInfo->mPsxOpenSpot;
			}

			u32 numFrames = *reinterpret_cast<u32*>(pEntry + 8);
			pEntry += numFrames * 8 + 0xC;
		}

		pPacketInfo = pPacketInfo->pNext;
		if (pPacketInfo)
			goto loop_top;
	}

	accessLog(
			"Created Anim Access Fails [NOT FOUND]: name=%s, addr=0x%8.8X\r\n",
			a1, a2);
	*a2 = 0;
	return -1;
}

// @Ok
// @Matching
void Spool_ClearEnvironmentRegions(void)
{
	ClearRegion(EnvRegions[0], 1);
}

// @Ok
SAnimFrame* Spool_FindAnim(char *a1, i32 a2)
{
	AnimPacket* pPacketInfo = AnimPackets;
	if (pPacketInfo)
	{
	loop_top:
		u32* pPacket = pPacketInfo->pPacket;
		u32 numAnims = *pPacket;
		char* pEntry = reinterpret_cast<char*>(pPacket + 1);

		for (u32 i = 0; i < numAnims; i++)
		{
			char* pA = a1;
			char* pB = pEntry;
			char ca = *pA & 0xDF;
			char cb = *pB & 0xDF;

			i32 count;
			for (count = 0; ca == cb && ca && cb && count < 8; count++)
			{
				pA++;
				ca = *pA & 0xDF;
				pB++;
				cb = *pB & 0xDF;
			}

			if ((!ca && !cb) || count == 8)
				return reinterpret_cast<SAnimFrame*>(pEntry + 0xC);

			u32 numFrames = *reinterpret_cast<u32*>(pEntry + 8);
			pEntry += numFrames * 8 + 0xC;
		}

		pPacketInfo = pPacketInfo->pNext;
		if (pPacketInfo)
			goto loop_top;
	}

	return 0;
}

// @Ok
// @Matching
i32 Spool_GetEnvIndex(i32 a1)
{
	return (a1 != EnvRegions[0]) ? -1 : 0;
}

// @Ok
u32* Spool_GetPalette(u32 Checksum, i32 Region)
{
	i32 numModels = reinterpret_cast<i32*>(G_PSXREGION[Region].ppModels)[-1];
	// pModelChecksums has one extra trailing word after its numModels entries
	u32 skip = G_PSXREGION[Region].pModelChecksums[numModels];
	u32* pPSX = G_PSXREGION[Region].pPSX;

	u32 *i;
	for ( i = (u32 *)((char *)pPSX + pPSX[1]); *i != -1; )
	{
		i++;
		i = (u32 *)((char *)i + i[0] + 4);
	}

	u32* pPalette = &i[skip + numModels + 2];
	u32 numPalettes = *pPalette++;

	for (u32 n16 = 0; n16 < numPalettes; n16++)
	{
		if (*pPalette == Checksum)
			return pPalette + 1;

		pPalette = (u32*)((char*)pPalette + 0x24);
	}

	numPalettes = *pPalette++;

	for (u32 n256 = 0; n256 < numPalettes; n256++)
	{
		if (*pPalette == Checksum)
			return pPalette + 1;

		pPalette = (u32*)((char*)pPalette + 0x204);
	}

	print_if_false(0, "Unable to find palette.");
	return 0;
}

// @Ok
// @Matching
void Spool_Init(void)
{
	print_if_false(1u, "MAXPSXS is too big");
	PCTex_ReleaseAllTextures();
	PCTex_InitSystemTextures();

	for (i32 i = 0; i < MAXPSX; i++)
	{
		PSXRegion[i].Filename[0] = 0;
		PSXRegion[i].Protected = 0;
		PSXRegion[i].Usable = 0;
		PSXRegion[i].ppModels = 0;

		delete[] PSXRegion[i].pSuper;

		PSXRegion[i].pSuper = 0;
		PSXRegion[i].pPSX = 0;
		PSXRegion[i].pAnimFile = 0;
		PSXRegion[i].pHierarchy = 0;
		PSXRegion[i].pColourPulseData = 0;
		PSXRegion[i].pTexWibData = 0;
		PSXRegion[i].pHooks = 0;

		while(gAccessRelated[i])
		{
			SAccess* pCur = gAccessRelated[i];
			SAccess* next = pCur->pNext;
			gAccessRelated[i] = next;

			*pCur->pLst = 0;
			delete pCur;
		}
	}

	gSpoolTexturesRelated = &gSpoolInitRelated[0];
	for (i32 j = 0; j < TEXTURE_CHECKSUM_TABLE_SIZE; j++)
		G_TEXTURE_CHECKSUM_HASH_TABLE[j] = 0;

	for (i32 k = 0; k < 511; k++)
	{
		gSpoolInitRelated[k].pNext = &gSpoolInitRelated[k+1];
	}

	gSpoolInitRelated[511].pNext = 0;
	Spool_InitialiseEnvModelHashTable();

	AnimPackets = 0;
	gSpoolInitOne = 0;
	gSpoolInitTwo = 0;
	EnviroList = 0;
	gSpoolColijEnvIndex = 0;
	gNumAccesses = 0;

	EnvRegions[0] = -1;
	EnvRegions[1] = -1;

}

// @Ok
INLINE void Spool_InitialiseEnvModelHashTable(void)
{
	for (i32 i = 0; i < 256; i++)
	{
		for (i32 j = 0; j < MAXITEMSPERCHECKSUM; j++)
		{
			gEnvModelHashTable[i][j] = -1;
		}
	}
}

// @Ok
// @Validate
void Spool_MaskFaceFlags(
		i32 region,
		u32 a2,
		u32 a3)
{
	print_if_false(region < 0 || region >= 40, "Bad region number sent to Spool_MaskFaceFlags");

	print_if_false(PSXRegion[region].Usable != 0, "PSX not usable in call to Spool_MaskFaceFlags");

	i32* v4 = *reinterpret_cast<i32**>(PSXRegion[region].ppModels);
	i32 v5 = v4[-1];

	for (i32 i = 0; i < v5; i++)
	{
		i32 v6 = reinterpret_cast<u16*>(v4)[3];

		v4 += 2 * reinterpret_cast<u16*>(v4)[1] + 2 * reinterpret_cast<u16*>(v4)[2] + 7;

		for (i32 j = 0; j < v6; j++)
		{
			u32 v8 = a3 & (*v4 | a2);
			*v4 = v8;

			v4 += v8 >> 18;
		}
	}
}

// @Ok
// @Validate
void Spool_ReloadAll(void)
{
	i32 currentSuit = CurrentSuit;
	gReloading = 0;

	for (i32 i = 0; i < MAXPSX; i++)
	{
		i32 IsEnviro = EnvRegions[0] == i;

		if (PSXRegion[i].Filename[0])
		{
			char a1[12];
			u8 backupProtected = PSXRegion[i].Protected;

			strncpy(a1, PSXRegion[i].Filename, 8);
			a1[8] = 0;
			if (gLowGraphics && Utils_CompareStrings(a1, SuitNames[CurrentSuit]))
			{
				Utils_CopyString("spidey", a1, 9);
				CurrentSuit = 1;
			}

			PSXRegion[i].Protected = 0;
			ClearRegion(i, 1);
			Spool_PSX(a1, IsEnviro);
			PSXRegion[i].Protected = backupProtected;
			restoreRegionAccess(i);
		}
	}

	if (currentSuit != CurrentSuit)
		Spidey_LoadAlternativeTextureSet(0, currentSuit);

	gReloading = 1;
	if (!gLowGraphics && gRegionReloadRelated >= 0)
	{
		PSXRegion[gRegionReloadRelated].Protected = 0;
		ClearRegion(gRegionReloadRelated, 1);
		gRegionReloadRelated = -1;
	}
}

// @Ok
// @Matching
void Spool_RemoveAccess(void **pLst, i32 region)
{
	SAccess* pPrev = 0;
	SAccess* pIter;

	for (pIter = gAccessRelated[region]; pIter; pIter = pIter->pNext)
	{
		if (pIter->pLst == pLst)
			break;

		pPrev = pIter;
	}

	if (pIter)
	{
		if (pPrev)
			pPrev->pNext = pIter->pNext;
		else
			gAccessRelated[region] = pIter->pNext;

		free(pIter);
		gNumAccesses--;
		*pLst = 0;
	}
}

// @Ok
void Spool_Sync(void)
{
}

// @Ok
// @Matching
i32 Spool_TextureAccess(
		u32 checksum,
		Texture **ppTexture)
{
	Texture* pTexture;
	for (
			pTexture = G_TEXTURE_CHECKSUM_HASH_TABLE[checksum % TEXTURE_CHECKSUM_TABLE_SIZE];
			pTexture;
			pTexture = pTexture->pNext)
	{
		if (pTexture->Checksum == checksum)
			break;
	}

	if (pTexture)
	{
		*ppTexture = pTexture;

		if (addAccess(
					reinterpret_cast<void**>(ppTexture),
					3,
					checksum,
					pTexture->mRegion))
			accessLog(
					"Created Texture Access: csum=%8.8X, rgn=%i, addr=0x%8.8X\r\n",
						checksum, pTexture->mRegion, ppTexture);
		return pTexture->mRegion;
	}

	if (!G_SPOOL_LOG_FAILED_TEXTURE_ACCESS)
	{
		print_if_false(0, "Can't find texture from checksum %ld", checksum);
		*ppTexture = G_ANIM_TABLE[13]->pTexture;
		accessLog(
				"Create Texture Access Fails [NOT FOUND]: csum=%8.8X, rgn=%i, addr=0x%8.8X\r\n",
				checksum, G_ANIM_TABLE[13]->pTexture->mRegion, ppTexture);
		return G_ANIM_TABLE[13]->pTexture->mRegion;
	}

	accessLog("Create Texture Access Fails [NOT FOUND]: csum=%8.8X, addr=0x%8.8X\r\n", checksum, ppTexture);
	return -1;
}

// @Bogus
// Not in the PC binary. Checked tools/names.json and the maintainer's PC
// IDB name list (idbs/spideypc_names.txt, ~3970 code names) for
// "SwapPSXFile": no match. No caller anywhere in the PC source tree
// either (grep for SwapPSXFile only finds spool.h/spool.cpp). It exists
// only on the Mac PowerPC build (idbs/spiderman_names.txt:
// 001244b0 SwapPSXFile__FPUl, 728 bytes there per prototypes.json). This
// is a byte-swap routine: PSX resource files are little-endian, so a
// big-endian PowerPC build needs to swap every multi-byte field after
// loading, while PC (x86, also little-endian) does not, so the original
// source almost certainly compiles this out on PC with an #ifdef on
// platform endianness. Left as a stub: there is no PC address to verify
// against, so any implementation here would be unverifiable guesswork, and
// a working byte swap would actively CORRUPT data on this little-endian
// build if anything ever called it.
// Re-verified in the 2026-09-01 audit (idalib session 7fdcc76e):
// lookup_funcs "SwapPSXFile" returns Not found, and the whole PC binary
// contains ZERO bswap instructions (search_text over .text), so no endian
// swapping of any kind was compiled into this build. Retagged
// @MEDIUMTODO -> @Bogus: no PC code exists, so it can never become @Ok and
// should not sit in the remaining-work count.
void SwapPSXFile(u32 *)
{
    printf("SwapPSXFile(u32 *)");
}

// @Bogus
// Same situation as SwapPSXFile above: not in names.json, not in the PC
// IDB name list, no PC caller, only present on Mac
// (idbs/spiderman_names.txt: 00123e70 SwapPSXPacketData__FPUl, 1296 bytes
// on Mac). Byte-swap routine, dead on a little-endian PC build. Left as a
// stub, no PC address to verify against. Re-verified and retagged in the
// 2026-09-01 audit, see SwapPSXFile above for the evidence.
void SwapPSXPacketData(u32 *)
{
    printf("SwapPSXPacketData(u32 *)");
}

// @Bogus
// Same situation as SwapPSXFile/SwapPSXPacketData: not in names.json, not
// in the PC IDB name list, no PC caller, only present on Mac
// (idbs/spiderman_names.txt: 001243b0
// SwapPSXTextureData__FPUlPP7TexturePUl, 196 bytes on Mac). Byte-swap
// routine, dead on a little-endian PC build. Left as a stub, no PC
// address to verify against. Re-verified and retagged in the 2026-09-01
// audit, see SwapPSXFile above for the evidence.
void SwapPSXTextureData(u32 *,Texture **,u32 *)
{
    printf("SwapPSXTextureData(u32 *,Texture **,u32 *)");
}

// @Ok
// @Matching
// release build strips the logger body; folds to the same address as every
// other no-op debug call (nullsub_5, 0x502D50), confirmed by disassembling
// Spool_TextureAccess's 3 accessLog call sites in the original binary.
void accessLog(char *, ...)
{
}

// @Ok
// @Matching
i32 addAccess(
		void** pLst,
		u32 type,
		u32 nameOrChecksum,
		i32 region)
{
	SAccess* pIter;
	for (pIter = PSXRegion[region].pAccess; pIter; pIter = pIter->pNext)
	{
		if (pIter->pLst == pLst)
			break;
	}

	if (pIter)
		return 0;

	SAccess* pAccess = static_cast<SAccess*>(malloc(sizeof(SAccess)));
	if (!pAccess)
	{
		return 0;
	}

	pAccess->pNext = gAccessRelated[region];
	pAccess->pLst = pLst;
	pAccess->mType = type;

	if ( type == 2 || type == 4 )
		strncpy(pAccess->mName, reinterpret_cast<char*>(nameOrChecksum), 8u);
	else
		pAccess->mChecksum = nameOrChecksum;

	gAccessRelated[region] = pAccess;
	gNumAccesses++;
	return 1;
}

// @Ok
INLINE void restoreRegionAccess(i32 region)
{
	for (
			SAccess* pAccess = gAccessRelated[region];
			pAccess;
			pAccess = pAccess->pNext)
	{
		switch (pAccess->mType)
		{
			case 0:
				pAccess->pLst = reinterpret_cast<void**>(Spool_GetModel(pAccess->mChecksum, region));
				break;
			case 1:
				print_if_false(region == EnvRegions[0], "Non-Enviro Item Accessed!");
				pAccess->pLst = reinterpret_cast<void**>(Spool_FindEnviroItem(pAccess->mChecksum));
				break;
			case 2:
				pAccess->pLst = reinterpret_cast<void**>(Spool_FindAnim(pAccess->mName, 1));
				break;
			case 3:
				pAccess->pLst = reinterpret_cast<void**>(Spool_FindTextureEntry(pAccess->mChecksum));
				break;
			case 4:
				pAccess->pLst = reinterpret_cast<void**>(Spool_FindTextureEntry(pAccess->mName));
				break;
			default:
				pAccess->pLst = 0;
				break;
		}

		print_if_false(pAccess->pLst != 0, "Unable to restore previous PSX access!");
	}
}

// @Ok
// @Validate
void texClearChecksums(char* pTexName)
{
	char v9[16];
	sprintf(v9, "%s.tex", pTexName);
	
	i32 v1 = FileIO_Open(v9);
	if (v1)
	{
		char* v2 = reinterpret_cast<char*>(gSpoolSystemMemory);
		print_if_false(v2 != 0, "Out of system memory.");
		FileIO_Load(v2);
		FileIO_Sync();

		v2[v1] = 0;

		while (v2)
		{
			u32 v8;   // %lx needs 4 bytes
			char v10[256];

			sscanf(v2, "%lx %s", &v8, v10);
			strlwr(v10);

			for (i32 i = 0; i < 256; i++)
			{
				if(gTextureEntries[i].Active &&
						strstr(v10, gTextureEntries[i].Name))
				{
					gTextureEntries[i].Active = 0;
					break;
				}
			}
			v2 = GetNextLine(v2);
		}
	}
}

// @Ok
// @Validate
void texLoadChecksums(char *pTexName)
{
	i32 checksumIndex = 0;
	char v12[16];
	sprintf(v12, "%s.tex", pTexName);

	i32 v2 = FileIO_Open(v12);

	char v13[256];

	if (v2)
	{
		char* v3 = reinterpret_cast<char*>(gSpoolSystemMemory);
		print_if_false(v3 != 0, "Out of system memory.");
		FileIO_Load(v3);
		FileIO_Sync();

		v3[v2] = 0;

		while (v3)
		{
			print_if_false(checksumIndex < 256, "Too many checksums.");
			if (!gTextureEntries[checksumIndex].Active)
			{
				// 0x4C98DD pushes &Checksum (esi+24h); the old call passed the
				// value and sscanf wrote through address 0 (standalone crash).
				sscanf(
						v3,
						"%lx %s",
						&gTextureEntries[checksumIndex].Checksum,
						v13);

				strlwr(v13);

				// 0x4C990E: v13 + (strrchr(v13, '\\') - v13) + 1, i.e. the
				// name after the last backslash
				char* pNameStart = strrchr(v13, '\\') + 1;

				for (i32 i = 0; i < 32; i++)
				{
					char c = pNameStart[i];
					gTextureEntries[checksumIndex].Name[i] = c;

					if (c == ' ')
					{
						if (pNameStart[i+1] < 'a' ||
								pNameStart[i+1] > 'z')
							break;
					}
				}
			}

			gTextureEntries[checksumIndex].Active = 1;

			checksumIndex++;
			v3 = GetNextLine(v3);
		}
	}
}


// @Ok
// @Matching
u32 Spool_GetModelChecksum(CItem *pItem)
{
	return G_PSXREGION[pItem->mRegion].pModelChecksums[pItem->mModel];
}

// @Ok
// @Matching
CItem* Spool_FindEnviroItem(u32 Checksum)
{
	CItem* pList = EnviroList;
	if (pList)
	{
		u32* pModelChecksums = PSXRegion[EnviroList->mRegion].pModelChecksums;
		print_if_false(pModelChecksums != 0, "NULL pChecksums?");

		i16* pHashes = gEnvModelHashTable[Checksum % 256];

		for (i32 i = 0, curHash = pHashes[i]; curHash >= 0; curHash = pHashes[i])
		{
			if (Checksum == pModelChecksums[curHash])
			{
				return
					reinterpret_cast<CItem*>(&reinterpret_cast<char*>(pList)[0x40 * curHash]);
			}
			
			if (++i >= 5)
				break;
		}
	}

	return 0;
}

// @Ok
// @Matching
i32 Spool_FindRegion(const char *pName)
{
	char fileName[0x20];
	ASSERT(pName != 0, "No FileName sent to Spool_PSX.");
	Utils_CopyString(pName, fileName, sizeof(fileName));

	if (!G_LOWGRAPHICS)
	{
		if (Utils_CompareStrings(fileName, "spidey"))
			Utils_CopyString(SuitNames[G_CURRENTSUIT], fileName, sizeof(fileName));
	}

	for (i32 i = 0; i < MAXPSX; i++)
	{
		if (Utils_CompareStrings(fileName, G_PSXREGION[i].Filename))
		{
			return i;
		}
	}

	return -1;
}

// @Ok
// @Matching
u32 Spool_GetModel(u32 Checksum, i32 Region)
{
	print_if_false(Region >= 0 && Region < MAXPSX, "Bad region number sent to Spool_GetModel");
	print_if_false(G_PSXREGION[Region].Usable != 0, "PSX not usable in call to Spool_GetModel");

	u32* pChecksum = G_PSXREGION[Region].pModelChecksums;
	u32 numModels = reinterpret_cast<u32*>(G_PSXREGION[Region].ppModels)[-1];

	for (u32 i = 0; i < numModels; i++)
	{
		if (*pChecksum == Checksum)
			return i;

		pChecksum++;
	}

	print_if_false(0, "Model checksum not found in call to Spool_GetModel");
	return 0;
}

// @Ok
// Functional: walks id/size/data records terminated by -1, same walk idiom
// as Spool_GetPalette (see CLAUDE.md matching tricks). Returns one dword
// past the terminator, matching every caller's use of the result (start of
// the packed Texture* array right after the record list). Checked against
// this loop inlined into ClearRegion's DecrementTextureUsage call
// (0x4CA83E): next = pRecord + 8 + size in both, our split walk
// (i++; i=(u32*)((char*)i+i[0]+4);) reaches the same address as the
// combined form, the original just emits it as a separate "add eax,4" then
// "lea" instead of one folded lea. Instruction-shape residue only.
INLINE u32 *Spool_SkipPackets(u32 *pPSX)
{
	u32 *i; // r4
	for ( i = (u32 *)((char *)pPSX + pPSX[1]); *i != -1; )
	{
		i++;
		i = (u32 *)((char *)i + i[0] + 4);
	}

	return i + 1;
}

// @Ok
// Functional: region clear, logic verified against Hex-Rays at 0x4ca7a0.
// Clears the region's texture usage, pSuper, and fields, walks the PSX
// packets (RemoveAnimPacket for 0x45 records), frees the access list, and
// calls DCClearRegion. (The 42 mnemonic diffs from the byte-match phase are
// the RemoveAnimPacket packet-walk pointer-advance shape; the logic is
// equivalent.)
void ClearRegion(i32 region, i32 a2)
{
	if (region == -1)
		return;

	print_if_false(region >= 0 && region < MAXPSX, "Bad region number sent to ClearRegion");

	if (!PSXRegion[region].Filename[0])
		return;

	if (region == GrenadeExplosionRegion)
		GrenadeExplosionRegion = -1;

	if (region == SymBurnRegion)
		SymBurnRegion = -1;

	if (region == FireDomeRegion)
		FireDomeRegion = -1;

	if (region == FireRingRegion)
		FireRingRegion = -1;

	DecrementTextureUsage(region);

	delete[] PSXRegion[region].pSuper;
	PSXRegion[region].pSuper = 0;
	PSXRegion[region].pAnimFile = 0;
	PSXRegion[region].pColourPulseData = 0;
	PSXRegion[region].pTexWibData = 0;
	PSXRegion[region].Protected = 0;
	PSXRegion[region].Usable = 0;
	PSXRegion[region].pHooks = 0;
	PSXRegion[region].ppModels = 0;

	texClearChecksums(PSXRegion[region].Filename);

	PSXRegion[region].Filename[0] = 0;
	PSXRegion[region].LowRes = 0;

	if (region == EnvRegions[0])
	{
		M3dZone_FreePSX(0);
		EnvRegions[0] = -1;
		gSpoolRegionRelatedOne = 0;
		EnviroList = 0;
		Spool_InitialiseEnvModelHashTable();
	}

	u32* pPSX = PSXRegion[region].pPSX;
	u32* pRecord = reinterpret_cast<u32*>(reinterpret_cast<char*>(pPSX) + pPSX[1]);

	while (*pRecord != 0xFFFFFFFF)
	{
		u32 size = pRecord[1];
		u32* pData = pRecord + 2;

		if (pRecord[0] == 0x45)
			RemoveAnimPacket(pData);

		pRecord = reinterpret_cast<u32*>(reinterpret_cast<char*>(pData) + size);
	}

	if (gReloading)
	{
		while (gAccessRelated[region])
		{
			SAccess* pAccess = gAccessRelated[region];
			gAccessRelated[region] = pAccess->pNext;
			*pAccess->pLst = 0;
			free(pAccess);
			gNumAccesses--;
		}
	}

	Mem_Delete(PSXRegion[region].pPSX);
	PSXRegion[region].pPSX = 0;

	DCClearRegion(region);

	if (a2)
		Spool_RemoveUnusedTextures();
}

// @Ok
// Functional: remove unused textures, logic verified against Hex-Rays at
// 0x4c9680. Walks the texture list, and for each texture with no usage and
// no format bits, decrements the palette usage, releases the texture,
// unpacks the VRAM rect, and removes it from the list (pushing it onto the
// gSpoolTexturesRelated free list). (The 61 mnemonic diffs from the
// byte-match phase are the NextTexture() null-check fall-through shape; the
// logic is equivalent.)
void Spool_RemoveUnusedTextures(void)
{
	GotoStartOfTextureList();

	Texture* pTex;
	while ((pTex = NextTexture()) != 0)
	{
		if ((pTex->field_12 & 0xF) == 0 || pTex->Usage != 0)
			continue;

		if (pTex->pPalette)
		{
			print_if_false(pTex->pPalette->Usage != 0, "Palette usage counter error!");
			pTex->pPalette->Usage--;
		}

		if (!gClearImagePrint)
			stubbed_printf("stubbed out: ClearImage");

		PCTex_ReleaseTexture(pTex->clut, true);

		// original really does read x and y together as one 32-bit pointer
		// into the packed VRAM rect, same idiom as NewTextureEntry's x/y clear
		VRAMRectUnpack(*reinterpret_cast<tagSVRAMRect**>(&pTex->x));

		RemoveTextureEntry(pTex);
	}
}

// @Ok
// @Matching
void Spool_ClearPSX(const char* Filename)
{

	char v3[32]; // [esp+8h] [ebp-40h] BYREF
	char v4[32]; // [esp+28h] [ebp-20h] BYREF

	ASSERT(Filename != 0, "No FileName sent to Spool_PSX.");
	Utils_CopyString(Filename, v3, sizeof(v3));

	if ( !G_LOWGRAPHICS && Utils_CompareStrings(v3, "spidey") )
		Utils_CopyString(SuitNames[G_CURRENTSUIT], v3, 32);

	ASSERT(v3 != 0, "No FileName sent to Spool_PSX.");
	Utils_CopyString(v3, v4, sizeof(v4));

	if ( !G_LOWGRAPHICS && Utils_CompareStrings(v4, "spidey") )
		Utils_CopyString(SuitNames[G_CURRENTSUIT], v4, 32);

	i32 index = 0;

	while (1)
	{
		if (Utils_CompareStrings(v4, G_PSXREGION[index].Filename))
			break;

		index++;
		if (index > MAXPSX)
		{
			index = -1;
			break;
		}
	}

	ClearRegion(index, 1);
}

// @Ok
// @Matching
void Spool_ClearAllPSXs(void)
{
	for (i32 i = 0; i < MAXPSX; i++)
	{
		if (!G_PSXREGION[i].Protected)
			ClearRegion(i, 1);
	}

	Spool_RemoveUnusedTextures();
}

// @Ok
// @Matching
// Walks the checksum hash bucket for the texture. If nothing is found and
// gSpoolLogFailedTextureAccess is 0, logs the miss and hands back the
// default texture (G_ANIM_TABLE[13]->pTexture), same idiom as the other
// spool "not found" fallbacks. Original at 0x4C9460.
// Bug found 2026-09-01 (same headless keypress test as the
// G_SPOOL_LOG_FAILED_TEXTURE_ACCESS fix above): this function is hooked, so
// it ran with our own never-populated gAnimTable[] instead of the real
// game's table (bit.h already has G_ANIM_TABLE for exactly this, just
// wasn't used here). Switched every gAnimTable[13] in this file to
// G_ANIM_TABLE[13].
Texture *Spool_FindTextureEntry(u32 checksum)
{
	Texture *pSearch;
	for (pSearch = G_TEXTURE_CHECKSUM_HASH_TABLE[checksum & 511];
			pSearch;
			pSearch = pSearch->pNext)
	{
		if (pSearch->Checksum == checksum)
			break;
	}

	if (!pSearch)
	{
		if (!G_SPOOL_LOG_FAILED_TEXTURE_ACCESS)
		{
			print_if_false(0, "Can't find texture from checksum %ld", checksum);
			return G_ANIM_TABLE[13]->pTexture;
		}
	}

	return pSearch;
}

// @Ok
Texture *Spool_FindTextureEntry(char *name)
{
	char localName[256];
	strcpy(localName, name);
	strlwr(localName);

	i32 index;
	for (index = 0; index < 256; index++)
	{
		TextureEntry *currentEntry = &gTextureEntries[index];
		if (!strcmp(currentEntry->Name, localName) && currentEntry->Active)
			break;
	}

	if (index >= 256)
		return G_ANIM_TABLE[13]->pTexture;

	return Spool_FindTextureEntry(gTextureEntries[index].Checksum);
}

// @Ok
// @Matching
u32 Spool_FindTextureChecksum(char *name)
{
	char localName[256];
	strcpy(localName, name);
	strlwr(localName);

	i32 index;
	for (index = 0; index < 256; index++)
	{
		if (!strcmp(G_TEXTUREENTRIES[index].Name, localName) && G_TEXTUREENTRIES[index].Active)
			break;
	}

	if (index < 256)
		return G_TEXTUREENTRIES[index].Checksum;

	return 0;
}

void validate_SPSXRegion(void)
{
	VALIDATE_SIZE(SPSXRegion, 0x44);

	VALIDATE(SPSXRegion, Filename, 0x0);
	VALIDATE(SPSXRegion, IsSuper, 0x9);
	VALIDATE(SPSXRegion, Usable, 0xA);
	VALIDATE(SPSXRegion, Protected, 0xB);
	VALIDATE(SPSXRegion, pModelChecksums, 0xC);
	VALIDATE(SPSXRegion, pSuper, 0x10);
	VALIDATE(SPSXRegion, ppModels, 0x14);
	VALIDATE(SPSXRegion, pPSX, 0x18);
	VALIDATE(SPSXRegion, pAnimFile, 0x1C);
	VALIDATE(SPSXRegion, pHierarchy, 0x20);
	VALIDATE(SPSXRegion, pHooks, 0x24);
	VALIDATE(SPSXRegion, pColourTable, 0x28);
	VALIDATE(SPSXRegion, pTexWibData, 0x2C);
	VALIDATE(SPSXRegion, pColourPulseData, 0x30);
	VALIDATE(SPSXRegion, pChunkData, 0x34);

	VALIDATE(SPSXRegion, NumParts, 0x38);

	VALIDATE(SPSXRegion, LowRes, 0x3B);

	VALIDATE(SPSXRegion, pAccess, 0x3C);
}

void validate_TextureEntry(void)
{
	VALIDATE_SIZE(TextureEntry, 0x28);

	VALIDATE(TextureEntry, Active, 0x0);
	VALIDATE(TextureEntry, Name, 0x1);
	VALIDATE(TextureEntry, Checksum, 0x24);
}

void validate_SAccess(void)
{
	VALIDATE_SIZE(SAccess, 0x14);

	VALIDATE(SAccess, pNext, 0x0);
	VALIDATE(SAccess, pLst, 0x4);
	VALIDATE(SAccess, mType, 0x8);
	VALIDATE(SAccess, mName, 0xC);
	VALIDATE(SAccess, mChecksum, 0xC);
}

void validate_AnimPacket(void)
{
	VALIDATE_SIZE(AnimPacket, 0xC);

	VALIDATE(AnimPacket, pPacket, 0x0);
	VALIDATE(AnimPacket, pNext, 0x4);
	VALIDATE(AnimPacket, mPsxOpenSpot, 0x8);
}

void validate_SModel(void)
{
	VALIDATE_SIZE(SModel, 36);

	VALIDATE(SModel, Flags, 0);
	VALIDATE(SModel, NumNormals, 4);

	VALIDATE(SModel, NumFaces, 6);
	VALIDATE(SModel, Radius, 8);
	VALIDATE(SModel, Box, 0xC);

	VALIDATE(SModel, zMax, 0x18);
	VALIDATE(SModel, NextLOD, 0x1A);

	VALIDATE(SModel, Vertices, 0x1C);
}

void validate_POLY_F3(void)
{
	VALIDATE_SIZE(POLY_F3, 0x14);

	VALIDATE(POLY_F3, tag, 0x0);
	VALIDATE(POLY_F3, r0, 0x4);
	VALIDATE(POLY_F3, g0, 0x5);
	VALIDATE(POLY_F3, b0, 0x6);
	VALIDATE(POLY_F3, code, 0x7);

	VALIDATE(POLY_F3, x0, 0x8);
	VALIDATE(POLY_F3, y0, 0xA);

	VALIDATE(POLY_F3, x1, 0xC);
	VALIDATE(POLY_F3, y1, 0xE);

	VALIDATE(POLY_F3, x2, 0x10);
	VALIDATE(POLY_F3, y2, 0x12);
}


#include "my_patch.h"


// @Bogus
void patch_spool(void)
{
	PATCH_PUSH_RET(0x004CA5A0, Spool_FindRegion);
	PATCH_PUSH_RET(0x004C9430, Spool_GetModelChecksum);
	PATCH_PUSH_RET(0x004CA750, Spool_ClearAllPSXs);
	PATCH_PUSH_RET(0x004CA640, Spool_ClearPSX);
	PATCH_PUSH_RET(0x004C95C0, Spool_FindTextureChecksum);
	PATCH_PUSH_RET(0x004C97B0, Spool_GetPalette);
}
