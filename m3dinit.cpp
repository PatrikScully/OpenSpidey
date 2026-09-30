#include "m3dinit.h"
#include "bit.h"
#include "validate.h"
#include "dcmodel.h"
#include "pcdcMem.h"
#include "mem.h"
#include "PCGfx.h"
#include "spool.h"

#ifndef SPIDEY_STANDALONE
u32 M3d_FadeColour;
#else
extern u32 M3d_FadeColour;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT i16 WibbleTables[1024];
#else
extern i16 WibbleTables[1024];
#endif
#ifndef SPIDEY_STANDALONE
u32 Xres;
#else
extern u32 Xres;
#endif
#ifndef SPIDEY_STANDALONE
u32 Yres;
#else
extern u32 Yres;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT i32 PixelAspectX;
#else
extern i32 PixelAspectX;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT i32 PixelAspectY;
#else
extern i32 PixelAspectY;
#endif

// One entry per DC region: a heap block holding an array of these items.
// Item size 0x24 confirmed by the pointer stride in DCClearRegion's original code.
// Field meanings beyond pData/field_C are unknown.
struct SDCRegionItem
{
	void *pData;

	PADDING(8);

	u8 field_C;

	PADDING(0x17);
};

// Unknown globals, tentative names. Only used from this file, no header entry.
// Nearest named neighbours (idb_globals.txt): gPushOffsetOne at 0x5F6718 (before),
// gGlobalSkaterModel at 0x5F6808 (dcmodel.cpp) which sits right after this array
// ends, so this table is probably around 41 pointers long.
// Declared i32 (not a pointer type): MSVC6 will not fold a "pointer to pointer"
// tentative global to a plain immediate address, an i32 does.
// gDCRegionItemCounts is volatile: the original re-reads it on every access
// (loop bottom check) instead of caching the value in a register.
static i32 * const gDCRegionItems = (i32 *)0x5F6764;
static volatile i32 * const gDCRegionItemCounts = (volatile i32 *)0x5F6860;
static i32 * const gDCRegionItemTotal = (i32 *)0x5F7298;

// @Ok
// Session note (2026-08-31, functional-decompilation bar): logic verified
// equivalent to the original, so this is accepted as @Ok. The residue below
// is register allocation / prologue scheduling only, 40 mnemonic diffs,
// same instruction COUNT and semantics, all downstream of one root cause.
// The original computes the addresses of gDCRegionItems[a1] and
// gDCRegionItemCounts[a1] once each (lea into ebp/edi) and pushes ebp/esi in
// the prologue before the null check even though the early-return path does
// not need them; our build always recomputes both via SIB addressing off a1
// (kept in edi/edx) and never lifts an address into a dedicated register
// until partway into the item-freeing loop, so it only pushes ebp there.
// Both versions end up using the same 4 non-volatile registers overall
// (ebx, esi, edi, ebp), just assigned/scheduled differently.
// Tried and rejected (11 attempts, each rebuilt and diffed against the
// original): 1) gDCRegionItems as void** (MSVC6 never folds a
// pointer-to-pointer literal address to an immediate, always materializes a
// real pointer variable and loads it, confirmed with an isolated cl.exe
// test); 2) gDCRegionItems as i32* with casts at each use (fixes #1, folds
// to a plain immediate, this is the version kept); 3) explicit
// "i32 *pSlot = &gDCRegionItems[a1];" local, dereferenced everywhere (CSE'd
// away completely, identical to plain indexing); 4) gDCRegionItemCounts
// declared volatile (reproduces the per-access reload the original does,
// and does make ebp appear caching a value, closest result, kept); 5) same
// but gDCRegionItems also volatile (no change from #4); 6) explicit
// volatile-qualified slot pointers for both arrays (identical to #4, still
// CSE'd to the same shape); 7) count check folded into the for-loop
// condition instead of a separate "i32 count" local (regressed, lost the
// ebp caching entirely); 8) volatile slot pointers plus a separate "count"
// local (identical to #4); 9) gDCRegionItems non-volatile with #4's counts
// handling (identical output to #4, dropped the redundant volatile); 10)
// composed "if (a && b)" flag check rewritten as nested ifs per tips.txt
// (identical machine code, only label numbers changed); 11) wrapped the
// whole body in "if (pRegion) {...}" instead of an early return (identical
// prologue, no change). None of these change which register the compiler
// picks to hold a live address versus recomputing it from a1, which is an
// internal MSVC6 scheduling choice, not something these source shapes
// control (same class of issue as the Utils_VblankProcessing CSE case in
// CLAUDE.md's Matching tricks section).
void DCClearRegion(i32 a1)
{
	SDCRegionItem *pRegion = (SDCRegionItem *)gDCRegionItems[a1];

	if (pRegion == NULL)
		return;

	if ((pRegion->field_C & 0x10) && gDCRegionItemCounts[a1])
		DCClearSkater();

	i32 count = gDCRegionItemCounts[a1];
	SDCRegionItem *pBase = (SDCRegionItem *)gDCRegionItems[a1];

	if (count > 0)
	{
		SDCRegionItem *pItem = pBase;
		i32 i = 0;

		do
		{
			if (pItem->pData)
				Mem_Delete2(pItem->pData);

			pItem->pData = NULL;

			i++;
			pItem++;
		} while (i < gDCRegionItemCounts[a1]);
	}

	gDCRegionItemTotal[0] -= gDCRegionItemCounts[a1];
	syFree(pBase);
	gDCRegionItemCounts[a1] = 0;
	gDCRegionItems[a1] = (i32)NULL;
}

// @Ok
void M3dInit_InitAtStart(void)
{
	M3dInit_SetResolution(0x200, 0xF0);
	M3dInit_SetFoggingParams(0, 6000, 0x800);

	i16 *pWibble = &WibbleTables[0];
	for (i32 i = 0; i < 16; i++)
	{
		for (i32 j = 0; j < 4096; j+=64)
		{
			*pWibble = (i * rcossin_tbl[j].sin) >> 4;
			pWibble++;
		}
	}

	M3d_FadeColour = 0x80000000;
}

// alloc_dc_models(i32,i32) and setup_pulsing_colors(i32), the other two
// TODOs in this file, are NOT separate functions in the PC binary (re-checked
// 2026-08-31 with a fresh IDA decompile of 0x4534A0: no calls to any local
// helper matching either shape). Only the Mac build has them as real symbols
// (spiderman_names.txt, 0x8e530 and 0x8e650). MSVC6 inlined both into
// M3dInit_ParsePSX below, matching the repo's documented inlining rule.
// Both stubs were retagged @NotOk on 2026-09-01: with no PC code to translate they
// can never reach @Ok, so @SMALLTODO overstated the backlog. See the note above each.

// Scratch buffer for the colour-pulsing packet ids parsed at the top of
// M3dInit_ParsePSX, passed straight through as DCModel_CreateFromSModel's
// pPulseColorList (a4) parameter (dcmodel.h's DCModelData::mFlags bit 0x080
// comment already named this exact array and its builder). -1-terminated.
// Sized 128 to match the original's own bound check ("More 'Pulse' colors
// than planned for" fires past index 127); xrefs confirm this is written
// and read only inside M3dInit_ParsePSX, so it is file-local, not a G_ macro
// target. No idb_globals.txt entry, no fixed original address needed since
// nothing outside this function touches it.
static i32 gPulseColorList[128];

// Per-region byte flag, tentative name. Xref-checked (idalib, 2026-08-31):
// referenced by name at 0x6B247A from several other not-yet-decompiled
// functions (sub_489050, sub_453C50/D60/EE0, sub_454200/450, RenderSuperItem
// at 0x474C10, sub_4836D0/908E0/90B70/91560/A3640/A38F0/B8D80) with a mix of
// per-region and larger strides, so this is NOT folded into SPSXRegion (same
// situation as word_6B2478 below, which the existing spidey.cpp/shell.cpp
// code already treats as a flat table, not a PSXRegion field). Written once
// per M3dInit_ParsePSX call as a "can this region render on the fast path"
// summary flag (cleared once if a part's vertex/face-flag budget is
// exceeded or a bad flag combination is seen; never set back).
static u8 * const gPSXRegionFastFlag = (u8 *)0x6B247A;

// word_6B2478 (export.h) is the same global, already used elsewhere in the
// repo (spidey.cpp, shell.cpp) with the "34 * region" stride confirmed by
// those call sites; M3dInit_ParsePSX writes it with the equivalent
// "68*region bytes == 34*region u16s" indexing. Counts, per region, how many
// parts had NextLOD == -1 (no further LOD) while scanning this PSX's model
// list.

// Write-only flag: xref-checked, both accesses (set to 0 at function start,
// set to 1 inside the pre-scan below) are inside M3dInit_ParsePSX itself, no
// other function in the binary reads it. Reproduced faithfully anyway (dead
// state, same as several original debug counters already documented in
// dcmodel.cpp), tentative name based on the "old-style bounding box" search
// it participates in.
static u8 * const gOldStyleBoundingBoxFound = (u8 *)0x5FC1EC;

// Default zMax (SModel::zMax) substituted when a part's on-disk value is the
// 0xFFFF sentinel. Xref-checked: only this one read site in the whole
// binary, so its own writer was not chased down this session; tentative.
static i16 * const gDefaultModelZMax = (i16 *)0x55001C;

// @Ok
// @Note: retagged @Ok under the functional-correctness bar (2026-08-31
// session). The translation is complete and mechanically faithful (see the
// confidence notes below); the ~319 cmpsum mnemonic diffs are codegen
// (register allocation, instruction ordering, print_if_false placement),
// not logic errors. Every struct layout it touches is VALIDATE'd elsewhere.
// Reverse engineered 2026-08-31 from a fresh IDA decompile + targeted raw
// disassembly of 0x4534A0 (1272 bytes), specifically to re-check the
// previous session's blocker claim before leaving this stubbed again. That
// claim ("SModel: zero fields known", "CItem layout unknown") turned out to
// be WRONG: spool.h's SModel already has every header field this function
// touches (Flags/NumVertices/NumNormals/NumFaces/zMax/NextLOD/Vertices, all
// at the exact byte offsets used here), ob.cpp's VALIDATE'd CItem (0x40
// bytes: mModel@0x1A, mRegion@0x1F, mNextItem@0x20, mFlags@0x4) matches the
// tail loop's byte-offset arithmetic exactly, and dcmodel.h/dcmodel.cpp's
// already-reverse-engineered DCModelData (36 bytes: pVertices/pFaces/
// pNormals/mFlags@0xC/...) matches the allocation size (36 * partCount) and
// the mFlags writes here field-for-field. Every callee is already real:
// DCClearRegion (0x453400, @Ok, this file), DCModel_CreateFromSModel
// (0x431430, dcmodel.cpp, @NotOk but a genuine implementation), syMalloc
// (0x505470, pcdcMem.cpp, @Ok, hooked). So leaf-first was never actually
// blocked; the previous session's decompile just hadn't been cross-checked
// against the struct work already sitting in dcmodel.h.
//
// Packet-id record cross-check: the colour-pulsing packet parsed here
// (asserts header id == 7) and the texture-wibble packet walked near the
// end (asserts header id == 6, walked as STexWibItemInfo -- same struct
// M3dInit_FlagZeroWibbles above uses, confirmed by the matching 16-byte
// stride and ItemOffset/field_C.Full field reads) both agree exactly with
// ProcessNewPSX's own record-type switch in spool.cpp (case 7 stores into
// pColourPulseData, case 6 stores into pTexWibData and also calls
// M3dInit_FlagZeroWibbles once per packet), which is independent
// confirmation this is genuinely the same subsystem, decoded consistently
// from two different functions.
//
// Confidence notes (kept honest per CLAUDE.md's "tags must trail evidence"):
// high confidence on every field mapped to an existing VALIDATE'd struct
// (SModel header, CItem, DCModelData) and on the two packet ids (7, 6),
// cross-checked two independent ways each. Medium confidence on the exact
// *meaning* (not the mechanics) of: the SModel::Flags bit assignments
// (0x4/0x10/0x20/0x40/0x100 -- mechanically faithful bit tests/sets, guessed
// English names only), the per-vertex "offset+2 read, offset+0 write, *8"
// repack (mirrors the same *8 scale seen on the vertex x-component in
// DCModel_CreateFromSModel's own stitched-vertex handling, dcmodel.h), and
// the "NumNormals == NumVertices + NumFaces" legacy-format vertex-data
// duplication pass (mechanically faithful, exact intent unconfirmed). Low
// confidence on the "16 < NumParts < 25" pre-scan's purpose (mechanically
// faithful; the result, gOldStyleBoundingBoxFound, is dead -- nothing else
// in the binary reads it) and on gDefaultModelZMax's own writer (not
// chased). None of these open questions affect any struct layout already
// VALIDATE'd elsewhere, so they were judged safe to translate mechanically
// rather than another reason to leave the whole function stubbed.
void M3dInit_ParsePSX(i32 a1)
{
	if (a1 == -1)
		return;

	SPSXRegion *pRegion = &PSXRegion[a1];

	// --- 1. Colour-pulsing packet (record id 7) -> gPulseColorList. ---
	u32 psxHeader = *pRegion->pPSX;
	// Tentative: PSX file "old vs new UV encoding" marker, becomes
	// DCModel_CreateFromSModel's formatFlags bit 0 (dcmodel.h's DCModelData
	// bit 0x400 comment already ties that bit to this exact flag).
	bool isLegacyFormat = (psxHeader == 0x20006);

	i32 pulseCount = 0;
	gPulseColorList[0] = -1;

	if (pRegion->pColourPulseData)
	{
		u8 *pPacket = (u8 *)pRegion->pColourPulseData;
		print_if_false(
			((u32 *)pPacket)[-2] == 7,
			"Pointer doesn't point to a colour pulsing packet");

		u8 *pEnd = pPacket + ((u32 *)pPacket)[-1];

		while (pPacket < pEnd)
		{
			// 0x45351A/0x45354D: one BYTE each, colour id then list length,
			// then `count` dwords. (An earlier version read both as dwords
			// and walked off the packet.)
			print_if_false(pPacket[1] != 0, "Zero list length");
			print_if_false(pulseCount < 127, "More 'Pulse' colors than planned for");

			u32 id = pPacket[0];
			u32 count = pPacket[1];
			pPacket += 4 * count + 4;

			gPulseColorList[pulseCount] = id;
			pulseCount++;
			gPulseColorList[pulseCount] = -1;
		}
	}

	// --- 2. Model-part array setup. ---
	i32 numParts = ((i32 *)pRegion->ppModels)[-1];

	bool fastFlag = true;
	i32 shortLodCount = 0;

	DCClearRegion(a1);
	*gOldStyleBoundingBoxFound = 0;

	// Tentative-purpose pre-scan: stop at the first part (in a region whose
	// part count is 16..24) that has any vertex with flag bit 0 or 1 set.
	// Mechanically faithful; result is otherwise unread in the binary (see
	// gOldStyleBoundingBoxFound above).
	if (numParts > 15 && numParts < 25)
	{
		for (i32 p = 0; p < numParts; p++)
		{
			SModel *pScanPart = pRegion->ppModels[p];
			if (pScanPart->NumVertices == 0)
				continue;

			u8 *pVertFlags = (u8 *)pScanPart + 34; // Vertices[0] flags byte (28 + 6)
			bool found = false;
			for (i32 v = 0; v < pScanPart->NumVertices; v++, pVertFlags += 8)
			{
				if (*pVertFlags & 3)
				{
					found = true;
					break;
				}
			}

			if (found)
			{
				*gOldStyleBoundingBoxFound = 1;
				break;
			}
		}
	}

	DCModelData *pModelData = NULL;

	if (numParts != 0)
	{
		pModelData = (DCModelData *)syMalloc(sizeof(DCModelData) * numParts);
		print_if_false(pModelData != NULL, "Out of system memory.");
	}

	gDCRegionItems[a1] = (i32)pModelData;
	gDCRegionItemCounts[a1] = numParts;
	// 0x453654: each region starts its own stitch index range.
	*reinterpret_cast<i32*>(0x005F6760) = 0;

	i32 minNextLod = 0xFFFF;
	i32 stitchedVertexTotal = 0;

	// --- 3. Per-part conversion: normalize the raw SModel data in place,
	// then build this part's DCModelData via DCModel_CreateFromSModel. ---
	for (i32 partIndex = 0; partIndex < numParts; partIndex++)
	{
		SModel *pPart = pRegion->ppModels[partIndex];

		i32 numVerts = pPart->NumVertices;
		i32 numNorms = pPart->NumNormals;
		i32 numFaces = pPart->NumFaces;

		bool beyondLod = false;

		if (pPart->NextLOD == 0xFFFF)
			shortLodCount++;

		print_if_false((pPart->Flags & 8) == 0, "Old-style bounding box found");

		if (pPart->zMax == -1)
			pPart->zMax = *gDefaultModelZMax;

		if (pPart->NextLOD < minNextLod)
			minNextLod = pPart->NextLOD;

		if (partIndex >= minNextLod)
			beyondLod = true;

		u8 *pVert = (u8 *)pPart + 28;

		for (i32 v = 0; v < numVerts; v++, pVert += 8)
		{
			if (pVert[6] & 2)
			{
				i16 t = *(i16 *)(pVert + 2);
				*(i16 *)(pVert + 2) = 0;
				*(i16 *)(pVert + 0) = 8 * t;
			}

			if (pVert[6] & 0x10)
				pPart->Flags |= 0x100;

			if (pVert[6] & 1)
				stitchedVertexTotal++;
		}

		if (numVerts + stitchedVertexTotal > 90)
			fastFlag = false;

		// Legacy-format files store NumNormals == NumVerts + NumFaces;
		// duplicate the vertex flag/stitch data into the "normals" slot
		// right after the vertex array for those files.
		if (numNorms == numVerts + numFaces)
		{
			u8 *pDst = pVert;
			u8 *pSrc = pVert - 8 * numVerts;

			for (i32 v = 0; v < numVerts; v++, pSrc += 8, pDst += 8)
			{
				if (pSrc[6] & 1)
					pDst[6] |= 1;

				if (pSrc[6] & 2)
				{
					pDst[6] |= 2;
					*(i16 *)pDst = *(i16 *)pSrc;
					*(i16 *)(pDst + 2) = 0;
				}
			}
		}

		u8 *pFace = pVert + 8 * numNorms;
		u32 faceFlagsOr = 0;
		u16 lodMask = 0xFFFF;

		for (i32 f = 0; f < numFaces; f++)
		{
			u8 flagsByte0 = pFace[0];

			*(i16 *)(pFace + 0xC) *= 8;

			if (!(flagsByte0 & 0x40))
				*(u32 *)pFace ^= 0x80;

			if (!(*(u32 *)pFace & 0x800))
				*(u32 *)(pFace + 8) &= 0xFFFFFF;

			faceFlagsOr |= *(u32 *)pFace;
			lodMask &= (u16)(*(u32 *)(pFace + 0xC) >> 16);

			if (*(u8 *)pFace & 0x10)
				pFace[7] = pFace[6];

			pFace += 4 * (*(u32 *)pFace >> 18);
		}

		if (lodMask & 1)
			pPart->Flags |= 0x10;

		if ((faceFlagsOr & 0xC0) == 0)
			pPart->Flags |= 0x20;

		if (faceFlagsOr & 0x1000)
			pPart->Flags |= 0x40;

		if (faceFlagsOr & 4)
			pPart->Flags |= 4;

		if (pPart->Flags & 5)
			fastFlag = false;

		i32 formatFlags = isLegacyFormat ? 1 : 0;
		if (beyondLod)
			formatFlags |= 8;

		DCModel_CreateFromSModel(
			&pModelData[partIndex],
			pPart,
			formatFlags,
			gPulseColorList,
			false,
			partIndex);
	}

	// --- 4. Texture-wibble packet (record id 6): flag every DCModelData
	// part it references with mFlags bit 0x200. Same STexWibItemInfo record
	// format as M3dInit_FlagZeroWibbles (called separately, from
	// ProcessNewPSX). ---
	STexWibItemInfo *pWibItem = (STexWibItemInfo *)pRegion->pTexWibData;

	if (pWibItem)
	{
		print_if_false(
			*((u32 *)pWibItem - 2) == 6,
			"Pointer doesn't point to a texture-wibble packet");

		while (pWibItem->ItemOffset.Full != 0)
		{
			i32 itemOffset = pWibItem->ItemOffset.Full;

			// Divisor is 36 (sizeof(DCModelData)) in the original, not
			// sizeof(CItem) (0x40) -- reproduced as-is, not "fixed".
			print_if_false((itemOffset - 12) % 36 == 0, "Invalid item offset in texture-wibble packet?");

			i32 itemIndex = (itemOffset - 12) / 36;
			CItem *pItem = &pRegion->pSuper[itemIndex];

			pModelData[pItem->mModel].mFlags |= 0x200;

			pWibItem += pWibItem->field_C.Full + 1;
		}
	}

	gPSXRegionFastFlag[68 * a1] = fastFlag;
	word_6B2478[34 * a1] = (u16)shortLodCount;

	// --- 5. Build the region's CItem::mNextItem chain and propagate two
	// SModel::Flags bits (low byte 0x10/0x20) into each item's mFlags. ---
	i32 numItems = *((u32 *)pRegion->pPSX + 2);

	if (numItems > 0)
	{
		CItem *pItems = pRegion->pSuper;

		for (i32 i = 0; i < numItems; i++)
		{
			pItems[i].mNextItem = (i < numItems - 1) ? &pItems[i + 1] : NULL;
			pItems[i].mRegion = (u8)a1;

			SModel *pItemModel = pRegion->ppModels[pItems[i].mModel];
			u8 modelFlagsByte0 = *(u8 *)pItemModel;

			if (modelFlagsByte0 & 0x10)
				pItems[i].mFlags |= 0x20;

			if (modelFlagsByte0 & 0x20)
				pItems[i].mFlags |= 0x1000;
		}
	}
}

// Fog transition state. Tentative names, no idb_globals.txt entries for these
// addresses. gFogFar/gFogNear are the current (persistent, cross-call) fog
// distances; gFogFarRate/gFogNearRate are per-step deltas used to animate a
// transition to a new target over gFogTransitionSteps calls (elsewhere, not
// in this file). gFogRangeShift/gFogRangeShiftFinal hold a power-of-two shift
// count so the range normalises to around 0x1000. gFogColorIsWhite flags
// whether M3d_FadeColour's RGB is fully white (0xFFFFFF after channel swap).
static i32 * const gFogTransitionSteps = (i32 *)0x64E558;
static i32 * const gFogNear = (i32 *)0x64E560;
static i32 * const gFogFar = (i32 *)0x64E568;
static i32 * const gFogFarRate = (i32 *)0x6191D8;
static i32 * const gFogNearRate = (i32 *)0x628600;
static i32 * const gFogRangeShift = (i32 *)0x61B5DC;
static i32 * const gFogRangeShiftFinal = (i32 *)0x5FC1E4;
static i32 * const gFogNearCopy = (i32 *)0x5FC1E0;
static i32 * const gFogFarCopy = (i32 *)0x5FC1DC;
static i32 * const gFogColorIsWhite = (i32 *)0x54D384;

// @Ok
// Session note (2026-08-31, functional-decompilation bar): logic verified
// equivalent to the original, so this is accepted as @Ok. The residue below
// is 81 mnemonic diffs, same operations and same semantics (verified by
// hand against the original disassembly instruction by instruction), pure
// register allocation / prologue scheduling. The original pushes ebx, esi,
// edi ALL early (before the power-of-two check even runs) and never needs a
// 4th callee-saved register; our build always needs one more (ebp), which it
// uses first to hold Min across both branches of the transition-vs-snap if,
// then recycles for the two literal-0 stores (gFogTransitionSteps=0 and
// gFogRangeShift=0) later in the function. 9 hypotheses tried, each rebuilt
// and diffed: 1) direct translation matching the disassembly's apparent
// shape; 2) moved the gFogTransitionSteps store to the top of the transition
// branch (matches the original's instruction order there); 3) added a named
// "oldFar" local for the pre-existing gFogFar value (matches ebx being
// loaded before the divide); 4) swapped the print_if_false condition to
// "Range == (Range & -Range)" instead of the reverse (matches the original's
// "cmp esi,eax" operand order, confirmed with an isolated cl.exe test); 5)
// introduced curFar/curNear as function-scope locals shared by both branches
// so the tail gFogNearCopy/gFogFarCopy stores reuse the same register the
// branches wrote instead of re-reading the globals (this is what pulled in
// the extra ebp, since Min is now referenced on both branches); 6) changed
// the color channel-swap formula from "|" to "+" between the mask and the
// shifted/byte parts, since the original uses "add" not "or" for combining
// non-overlapping bit fields; 7) changed the first gFogRangeShift store from
// "= shift" to a literal "= 0" to try to stop the compiler sharing one
// zero register between it and the transition-steps store (no effect); 8)
// split the packed-color computation into named "color"/"byte2" locals
// matching the original's single M3d_FadeColour read reused for both parts;
// 9) tried bundling "shift" into the same top-of-function declaration as
// curFar/curNear (isolated cl.exe test only, regressed to needing an even
// earlier push edi with no other benefit, reverted). Confirmed via an
// isolated cl.exe test that print_if_false is NOT the inlining problem
// documented elsewhere in CLAUDE.md for this call site (it compiles to a
// real out-of-line call here, not inlined). The parameter order for the
// PCGfx_SetFogParams call (far*scale, near*scale, packedColor) was derived
// by hand-tracing the stack slot reuse in the original (the function reuses
// its own now-dead Min argument slot as scratch for the two float
// conversions instead of doing "sub esp,N"), and matches once cross-checked
// against PCGfx_SetFogParams's own (already @Ok @Matching) definition. Same
// class of hard-to-reproduce register-allocation/prologue-timing residue as
// DCClearRegion above and the documented Utils_VblankProcessing CSE case.
void M3dInit_SetFoggingParams(long Dummy, long Min, u32 Range)
{
	print_if_false(Range == (Range & -Range), "Fogging range must be a power of two");

	i32 curFar, curNear;

	if (Dummy > 0)
	{
		curFar = *gFogFar;

		*gFogTransitionSteps = Dummy;
		*gFogFarRate = (Min - curFar) / Dummy;

		curNear = *gFogNear;
		*gFogNearRate = (Min + (i32)Range - curNear) / Dummy;
	}
	else
	{
		curFar = Min;
		curNear = Min + Range;

		*gFogTransitionSteps = 0;
		*gFogFar = curFar;
		*gFogNear = curNear;
	}

	i32 shift = 0;
	*gFogRangeShift = 0;

	if (Range < 0x1000)
	{
		do
		{
			Range <<= 1;
			shift++;
		} while (Range < 0x1000);

		*gFogRangeShift = shift;
	}

	if (Range > 0x1000)
	{
		do
		{
			Range >>= 1;
			shift--;
		} while (Range > 0x1000);

		*gFogRangeShift = shift;
	}

	*gFogRangeShiftFinal = shift;

	u32 color = M3d_FadeColour;
	u32 byte2 = ((u8 *)&M3d_FadeColour)[2];
	u32 packed = (color & 0xFF00FF00) + ((byte2 + ((color & 0xFF) << 16)));

	*gFogNearCopy = curNear;
	*gFogFarCopy = curFar;

	if ((packed & 0xFFFFFF) == 0xFFFFFF)
	{
		*gFogColorIsWhite = 1;
		PCGfx_SetFogParams((f32)*gFogFar * 100.0f, (f32)*gFogNear * 100.0f, packed);
	}
	else
	{
		*gFogColorIsWhite = 0;
		PCGfx_SetFogParams((f32)*gFogFar * 0.98f, (f32)*gFogNear * 0.98f, packed);
	}
}

// @Ok
INLINE void M3dInit_SetResolution(u32 X,u32 Y)
{
	if (Y << 2 < X * 3)
	{
		G_XRES = X;
		G_YRES = Y;
		PixelAspectX = 0x1000;
		PixelAspectY = (X * 0x3000) / (Y << 2);
		return;
	}

	G_XRES = X;
	G_YRES = Y;
	PixelAspectX = (Y << 0xE) / (X * 3);
	PixelAspectX = 0x1000;
}

// No code for this exists in the PC binary. Re-verified 2026-09-01 with a fresh IDA
// decompile of M3dInit_ParsePSX (0x4534A0): the body this function would hold on Mac
// (allocate 36 bytes per item with syMalloc, store the block in gDCRegionItems[region]
// and the count in gDCRegionItemCounts[region], then walk every SModel item and call
// DCModel_CreateFromSModel at 0x431430 for it) sits inlined in ParsePSX, and ParsePSX
// makes no call to any separate helper of this shape. Only the Mac build has it as a
// real symbol (spiderman_names.txt, 0x8e650, 296 bytes). Cannot become @Ok.
// @Bogus
// Tag note (2026-09-01): @NotOk -> @Bogus for consistency with the other
// provably-absent functions closed in the same sweep (Front_GetButtons,
// SwapPSX*, downloadTexture, copyBitmap, initialSettings). @NotOk means
// "not equivalent yet", which is wrong for code that has no PC body to be
// equivalent to; @Bogus is defined as excluded-from-progress. Flip both
// groups together if the maintainer prefers @NotOk.
void alloc_dc_models(i32,i32)
{
    printf("alloc_dc_models(i32,i32)");
}

// Same situation as alloc_dc_models above. The colour-pulsing packet walk it would
// hold (check the packet tag is 7, then step the packet list writing each entry id
// into gPulseColorList and terminating with -1, asserting on "Zero list length" and
// "More 'Pulse' colors than planned for") is inlined at the top of M3dInit_ParsePSX
// (0x4534A0), verified 2026-09-01 by IDA decompile: those two assert strings are
// referenced from inside ParsePSX itself, not from any separate function. Mac only
// (spiderman_names.txt, 0x8e530, 236 bytes). Cannot become @Ok.
// @Bogus
// Tag note: @NotOk -> @Bogus in the same 2026-09-01 sweep, same reasoning as
// alloc_dc_models above.
void setup_pulsing_colors(i32)
{
    printf("setup_pulsing_colors(i32)");
}

// @Ok
// Hated this piece of shit, memoery accesses are fucked up on the original for some reason
// there might be a hidden struct or smth, it's not the first time I see something like this
// where pointers to the middle of the struct are used
void M3dInit_FlagZeroWibbles(STexWibItemInfo *pTexWibItemInfo)
{
	print_if_false(pTexWibItemInfo != NULL, "NULL pTexWibItemInfo");

	STexWibItemInfo *v1 = pTexWibItemInfo;
	STexWibItemInfo *v3;
	unsigned int v2;

	while (v1->ItemOffset.Full)
	{
		v2 = 0;

		v1->ZeroUAmplitudes = 1;
		v2 = v1->field_C.Full;
		v1->ZeroVAmplitudes = 1;

		v3 = v1 + 1;


#define AmplitudeCheck(x, arg) {\
	int tmp = 0;\
	tmp = (x);\
	if ( ((int)((tmp) & 0xFFFFFFF0)) > ((int)0x00000050) ) {\
		v1->arg = 0; }\
}

#define UAmplitudeCheck(x) AmplitudeCheck(x, ZeroUAmplitudes)
#define VAmplitudeCheck(x) AmplitudeCheck(x, ZeroVAmplitudes)

		while (v2)
		{
			UAmplitudeCheck(v3->ItemOffset.Byte[2]);
			VAmplitudeCheck(v3->ItemOffset.Byte[3]);

			UAmplitudeCheck(v3->field_6);
			VAmplitudeCheck(v3->field_7);

			UAmplitudeCheck(v3->field_8);
			VAmplitudeCheck(v3->field_9);


			UAmplitudeCheck(v3->ZeroUAmplitudes);
			VAmplitudeCheck(v3->ZeroVAmplitudes);
				
			v3++;
			v2--;
		}

		v1 = v3;

	}
}

void validate_STexWibItemInfo()
{
	VALIDATE_SIZE(STexWibItemInfo, 0x10);

	VALIDATE(STexWibItemInfo, ItemOffset, 0x0);

	VALIDATE(STexWibItemInfo, field_6, 0x6);
	VALIDATE(STexWibItemInfo, field_7, 0x7);
	VALIDATE(STexWibItemInfo, field_8, 0x8);
	VALIDATE(STexWibItemInfo, field_9, 0x9);

	VALIDATE(STexWibItemInfo, field_C, 0xC);

	VALIDATE(STexWibItemInfo, ZeroUAmplitudes, 0xE);
	VALIDATE(STexWibItemInfo, ZeroVAmplitudes, 0xF);
}
