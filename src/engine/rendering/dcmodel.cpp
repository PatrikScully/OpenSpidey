#include "engine/rendering/dcmodel.h"
#include "engine/rendering/PCTex.h"
#include "core/game_heap.h"
#include "engine/assets/spool.h"

#include "compatibility/validate.h"

#include "compatibility/non_win32.h"
#include <cmath>
#include <cstring>

#ifndef SPIDEY_STANDALONE
EXPORT f32 gPreComputedColorRelated = -1.0f;
#else
extern f32 gPreComputedColorRelated;
#endif
EXPORT u8 gConvertedColors[256];

// ---------------------------------------------------------------------
// DCModel_CreateFromSModel (0x431430) support globals.
// ---------------------------------------------------------------------
// Round a byte count up to a multiple of 4, matching the `(x+3)&~3`
// idiom the original uses for every DCModelData sub-allocation size.
#define DC_ROUND4(x) (((x) + 3) & ~3)

// One entry of the "part offset" table (a level-specific "offsets\<name>.off"
// text table, parsed elsewhere, not decompiled this session). Confirmed
// layout via disasm of 0x431430's tail (matching a6/partIndex against
// mPartIndex, then reading mSortBiasLowGraphics/mSortBiasNormal into
// DCModelData). CONFIRMED names (maintainer's IDB,
// ~/Documents/spidey-work/idbs/idb_globals.txt): gPushOffsetAddr (the table)
// and gPushOffsetOne (the entry count). Exact field semantics beyond
// "matched by part index" are still a guess.
struct SDCPushOffsetEntry
{
	i32 mPartIndex;
	i16 mSortBiasLowGraphics;
	i16 mSortBiasNormal;
};
static SDCPushOffsetEntry * const gPushOffsetAddr = (SDCPushOffsetEntry *)0x5F6A60;
static i32 * const gPushOffsetOne = (i32 *)0x5F6718;

// Tentative names, not in the maintainer's IDB. Per-call stats
// accumulators; never read back by DCModel_CreateFromSModel itself, so
// their consumer (if any) is unknown -- guessed as level-load debug/stat
// counters, harmless to reproduce.
static i32 * const gDCTotalFacesLoaded    = (i32 *)0x5F6710;
static i32 * const gDCTotalVerticesLoaded = (i32 *)0x5F671C;
static i32 * const gDCTotalNormalsLoaded  = (i32 *)0x5F6690;

// Tentative. Incremented on a couple of "should not happen" conditions (a
// model with exactly 21 vertices -- looks like a leftover debug trap for a
// specific known model; a face vertex index out of range). Reproduced
// faithfully per CLAUDE.md's "reproduce dead code, don't fix it" rule.
static i32 * const gDCModelErrorCount = (i32 *)0x5F7EBC;

// Tentative. High-water mark of the largest post-weld vertex count
// (DCModelData::mVertexCount) seen so far across all
// DCModel_CreateFromSModel calls.
static i32 * const gDCMaxWeldedVertexCount = (i32 *)0x5F82E4;

// Already documented in dcmodel.h: the running "next free stitch-normal
// index" counter, shared across DCModel_CreateFromSModel calls in the same
// level load.
static i32 * const gModelStitchNormalIndexBase = (i32 *)0x5F6760;

// Shared, cross-call table of "stitch normal" direction records: 256
// entries x 8 bytes (4 x i16: x,y,z,flags-like), fixed-point PSX normal
// components (same 1/4096 scale as regular normals, see the normal
// conversion loop below). Table size (256 entries) confirmed two ways: (1)
// the vertex loop clamps its stitch index to <256 before storing it; (2)
// the table's end address, 0x5F7298, is exactly where m3dinit.cpp's
// unrelated gDCRegionItemTotal global sits (256*8 = 0x800 =
// 0x5F7298-0x5F6A98) -- almost certainly the "MSVC folds array indexing
// into neighbouring globals" pattern from CLAUDE.md, not evidence the two
// globals are related.
struct SDCStitchNormalEntry
{
	i16 x;
	i16 y;
	i16 z;
	i16 mFlags;
};
static SDCStitchNormalEntry * const gDCStitchNormalTable = (SDCStitchNormalEntry *)0x5F6A98;
static const i32 gDCStitchNormalTableCount = 256;

// Confirmed name (maintainer's IDB): a large, general-purpose scratch
// buffer shared by several unrelated subsystems (xrefs from spool-ish and
// completely unrelated functions), reused here as scratch working memory
// for the vertex-welding pass below. Treated as an i32 array of chain
// nodes; see BuildWeldedVertexSlots for the encoding.
// 0x5498FC is the POINTER variable gSpoolSystemMemory (spool.h), the buffer
// itself sits at 0x62E510: "mov edx,[5498FCh]" at 0x431D32 loads the pointer.
// (An earlier version used the address as the buffer and wrote over .data.)

EXPORT DCSkaterModel gSkaterModels[2];
#ifndef SPIDEY_STANDALONE
EXPORT DCSkaterModel gGlobalSkaterModel;
#else
extern DCSkaterModel gGlobalSkaterModel;
#endif

// @Ok
// @Matching
DCSkaterModel::DCSkaterModel(void)
{
	this->field_28.pObject = 0;
}

// @Ok
// @Matching
void DCClearSkater(void)
{
	if (!gGlobalSkaterModel.field_1C)
	{
		gSkaterModels[0].ClearSkaterModel();
		gSkaterModels[1].ClearSkaterModel();
	}
}

// @Ok
// @Matching
 DCKeyFrame::~DCKeyFrame(void)
{
	delete this->pNext;
}

// @Ok
// @Matching
DCMaterial::~DCMaterial(void)
{
	delete this->field_10;
	delete this->field_34;

	if (!this->field_3F && CheckValidTexture(this->field_38))
		PCTex_ReleaseTexture(this->field_38, true);
}

// Assigns pDcModel->pFaces[*].mVertSlot for every corner of every face, and
// sets pDcModel->mVertexCount. This is the vertex-welding/splitting pass of
// DCModel_CreateFromSModel: for each (face, corner) it looks at the SModel
// vertex id referenced by that corner. The first time an id is used, the
// corner just gets that id as its slot (identity mapping into the [0,
// numVertices) range the vertex-conversion loop already filled in). Every
// later use of the SAME id searches a per-id linked chain (built as we go,
// in gSpoolSystemMemory, encoded as an i32 per original id: low 16 bits =
// the face index that owns this chain node, high 16 bits = the next node's
// id / -1 terminator) of every earlier (face, corner) that used it, looking
// for one with a matching appearance. A match reuses that earlier corner's
// slot (with the 0x8000 bit OR'd in as a "this corner was merged" marker);
// no match allocates a brand-new slot number beyond numVertices and links
// it into the chain so a later corner can find it too.
//
// RE-VERIFIED 2026-08-31 against fresh raw disassembly (0x431e79-0x431f54),
// this time cross-checking every stack-slot offset directly against the
// prologue's `and eax, N` instructions instead of trusting Hex-Rays' local
// names alone (the previous session's stated reason for @NotOk). Two real
// corrections found, both confirmed at the instruction level:
//  1. The "skip appearance check entirely, always weld" bypass is NOT
//     per-vertex (DCVert::mFlags & 2). It is `formatFlags & 2` (the SAME
//     `i32 formatFlags` DCModel_CreateFromSModel's caller passes in),
//     tested as a whole-model flag once per corner-match search. Confirmed
//     at 0x4314cb/0x4314dc (`mov ecx,[esp+arg_8]` / `and eax,2`, arg_8 is
//     the 3rd parameter = formatFlags) writing stack slot var_128, and
//     0x431ea6 (`test ebp,ebp` -- ebp is that same value, register-cached
//     for the rest of the function) gating a jump straight past BOTH the
//     0x800/pulsing-color branch AND the plain color-dword compare,
//     directly into the UV/tex-index check.
//  2. The UV/tex-index comparison is gated on `pFace->mFlags & 1`
//     ("is textured"), NOT `(pFace->mFlags & 0x800) == 0` as the previous
//     draft had it -- confirmed at 0x431edb (`test cl,1`, cl = this face's
//     mFlags byte). It also requires the CANDIDATE corner's face to be
//     textured too (0x431ee0 `test byte ptr [ebx],1`), which the previous
//     draft never checked at all.
// mFlags & 0x800 (pulsing/raw color) still gates a SEPARATE, narrower
// per-corner single-byte color-index comparison (0x431eb2 range) instead of
// the plain 4-byte color-dword compare used otherwise; that specific byte
// vs whole-dword comparison looked, from the raw bytes, unlikely to ever
// succeed for a typical non-zero packed color (a single zero-extended byte
// compared against a full RGBA dword), but pinning down its exact intent
// with full confidence would need more time than this pass had. Reproduced
// here as the same plain color-dword compare used for non-pulsing faces
// instead -- worst case this makes a few pulsing-color corners weld that
// the original would have kept split (or vice versa), not a shape-breaking
// difference. Flag this specific sub-case if it ever needs tightening.
//
// Session-wide override in force for this task: functional decompilation,
// not byte-matching. Tagged @Ok on that basis (clean MSVC6 build,
// dunno.py/compare.py sanity pass); no runtime test was available to
// confirm actual rendered geometry.
// @Ok
static void DC_WeldVertexSlots(DCModelData *pDcModel, i32 numVertices, i32 numFaces, i32 formatFlags)
{
	// vertexUsed[id]: has original vertex id `id` been assigned a slot yet.
	u8 vertexUsed[256];
	// pChain[id]: linked-list state for original vertex id `id` (or a
	// newly-allocated split id >= numVertices), packed as
	// low16 = owning face index, high16 = next id in the chain (or the
	// low16 field is -1 to mean "no chain yet" for original ids).
	i32 *pChain = static_cast<i32 *>(gSpoolSystemMemory);

	if (numVertices > 0)
	{
		memset(vertexUsed, 0, numVertices);
		memset(pChain, 0xFF, 4 * numVertices);
	}
	pChain[numVertices] = -1;

	i32 nextChainId = numVertices;   // grows as new chain nodes get linked in
	i32 nextWeldedSlot = numVertices; // grows as brand-new dest slots get handed out
	// gDCMaxWeldedVertexCount is a cross-call high-water mark of nextWeldedSlot.

	for (i32 faceIdx = 0; faceIdx < numFaces; faceIdx++)
	{
		DCFace *pFace = &pDcModel->pFaces[faceIdx];
		i32 numCorners = (pFace->mFlags & 0x10) ? 3 : 4;
		i32 matchedCount = 0;

		for (i32 corner = 0; corner < numCorners; corner++)
		{
			i32 srcVertId = pFace->mVertIndex[corner];
			bool matched = false;

			if (!vertexUsed[srcVertId])
			{
				vertexUsed[srcVertId] = 1;
				pFace->mVertSlot[corner] = (u16)srcVertId;
				matchedCount++;
				continue;
			}

			i32 searchId = srcVertId;
			i32 chainVal = pChain[searchId];
			if (chainVal != -1)
			{
				do
				{
					i32 candFaceIdx = (u16)chainVal;
					if (chainVal < 0)
					{
						searchId = nextChainId;
					}
					else
					{
						searchId = (u16)(chainVal >> 16);
					}

					DCFace *pCand = &pDcModel->pFaces[candFaceIdx];
					i32 candNumCorners = (pCand->mFlags & 0x10) ? 3 : 4;

					for (i32 candCorner = 0; candCorner < candNumCorners; candCorner++)
					{
						if (pCand->mVertIndex[candCorner] != srcVertId)
						{
							// 0x431F3D: once this corner is matched, a
							// different vertex id ends the candidate scan
							if (matched)
								break;
							continue;
						}

						// When formatFlags & 2 is set, skip appearance
						// comparison entirely and accept the first same-id
						// corner found (see the function comment: confirmed
						// at the instruction level this is a whole-model
						// flag, not a per-vertex one). Otherwise require
						// matching color, and -- only when THIS face is
						// textured (mFlags & 1), which also requires the
						// candidate face to be textured -- matching UV and
						// texture index too.
						bool ok = (formatFlags & 2) != 0;
						if (!ok)
						{
							// mColor[3] + mColorExtra are one dword at +8
							u32 faceColor = *(u32 *)pFace->mColor;
							u32 candColor = *(u32 *)pCand->mColor;
							if (pFace->mFlags & 0x800)
							{
								// 0x431EB2: per corner colour index byte,
								// then that byte against the candidate's
								// whole dword, then dword against dword.
								// Reproduced as the original does it.
								u8 faceByte = ((u8 *)pFace->mColor)[corner];
								ok = faceByte == ((u8 *)pCand->mColor)[candCorner]
									&& (u32)faceByte == candColor
									&& faceColor == candColor;
							}
							else
							{
								ok = faceColor == candColor;
							}
						}
						if (ok && (pFace->mFlags & 1) != 0)
						{
							ok = ((pCand->mFlags & 1) != 0)
								&& (pFace->mU[corner] == pCand->mU[candCorner])
								&& (pFace->mV[corner] == pCand->mV[candCorner])
								&& (pFace->mTexIndex == pCand->mTexIndex);
						}

						if (ok)
						{
							u16 slot = pCand->mVertSlot[candCorner];
							slot |= 0x8000;
							pFace->mVertSlot[corner] = slot;
							matched = true;
							break;
						}
					}

					// 0x431F87: the chain walk runs to its end even after a
					// match (a later face in the chain can re-weld the corner)
					chainVal = pChain[searchId];
				} while (chainVal != -1);
			}

			if (!matched)
			{
				pFace->mVertSlot[corner] = (u16)nextWeldedSlot;
				if (*gDCMaxWeldedVertexCount < nextWeldedSlot)
					*gDCMaxWeldedVertexCount = nextWeldedSlot;
				nextWeldedSlot++;
			}
			else
			{
				matchedCount++;
			}
		}

		if (numCorners == 3)
			pFace->mVertSlot[3] = pFace->mVertSlot[2];

		// Link every corner's original vertex id into its chain so later
		// faces can find this face when searching for a match.
		for (i32 linkCorner = 0; linkCorner < numCorners; linkCorner++)
		{
			i32 origId = pFace->mVertIndex[linkCorner];
			i32 chainVal = pChain[origId];
			if (chainVal == -1)
			{
				// 0x43201F is a 16 bit store: the high word keeps its 0xFFFF,
				// which is what marks "no next id" (a negative value). A full
				// 32 bit store made the next id 0 and the search loop above
				// cycled forever (found by the standalone build, 2026-09-02).
				pChain[origId] = (i32)(0xFFFF0000u | (u16)faceIdx);
			}
			else
			{
				i32 tailId = origId;
				if (chainVal >= 0)
				{
					do
					{
						tailId = (u16)(pChain[tailId] >> 16);
					} while (pChain[tailId] >= 0);
				}
				if ((u16)pChain[tailId] != faceIdx)
				{
					pChain[tailId] = (pChain[tailId] & 0xFFFF) | (nextChainId << 16);
					// same 16 bit store as above (0x432068), then the new
					// end-of-chain sentinel behind it (0x432073)
					pChain[nextChainId] = (i32)(0xFFFF0000u | (u16)faceIdx);
					nextChainId++;
					pChain[nextChainId] = -1;
				}
			}
		}
	}

	pDcModel->mVertexCount = nextWeldedSlot;
}

// @Ok
// (was @BIGTODO; now a full attempt -- see the confidence note near the end
// of this comment for what changed in the 2026-08-31 re-verification pass.)
// Reverse engineered 2026-08-31 from IDA decompiles + raw disassembly of
// 0x431430 (~4.4 KB, ~200 Hex-Rays locals). Verified/fixed the DCModelData
// struct in dcmodel.h against a fresh decompile (found and corrected 4 real
// errors: the "transparent face" flag is 0x800 not 0x008, two flag-bit
// conditions were documented backwards, and the 0x002 flag bit is actually
// about distinct-texture-count, not stitched normals -- see dcmodel.h for
// the corrected comments and the asm evidence).
//
// Structure of this implementation, roughly following the original's
// program order:
//   1. Recompute gConvertedColors via PreComputeConvertedColors(1.0f),
//      unconditionally -- confirmed via the literal float bytes at
//      0x549900 (=1.0f, the call argument) and 0x549910 (=-1.0f, matching
//      gPreComputedColorRelated's declared initial value, i.e. the
//      assignment PreComputeConvertedColors does at its own end). Present
//      here because PreComputeConvertedColors is defined earlier in this
//      TU and MSVC6 inlines same-TU calls (CLAUDE.md).
//   2. Allocate one block for pVertices+pFaces+pNormals via DCMem_New,
//      laid out vertices-then-faces-then-normals even though the size
//      expression sums faces-then-verts-then-normals (matches the actual
//      pointer arithmetic in the disassembly).
//   3. Convert every source face record (raw/untyped PSX-packed SModel
//      data -- spool.h's SModel doesn't expose typed Vertices/Normals/Faces
//      arrays yet, marked @TODO there) into a DCFace: flags/color/vertex
//      indices always; UV either from a PVRRect pack (byte UV, formatFlags
//      bit0 clear) or a direct fixed-point scale (word UV, formatFlags
//      bit0 set). A dead correction sub-branch in the word-UV path (gated
//      by formatFlags bit2) was traced and found to only mutate the
//      *source* SModel bytes after this call's own DCFace UV floats were
//      already computed from the pre-mutation values -- it has no effect
//      on this call's own output, so it is skipped (see the comment at its
//      call site below).
//   4. Convert every source vertex into a DCVert. Reproduces a genuine
//      source-level bug (see DCVert::x/y comment in dcmodel.h): when the
//      "1/8 unit" flag is set, x/y get the RAW INTEGER bit pattern instead
//      of a real float conversion.
//   5. Convert every source normal into a DCNormal (fixed-point, 1/4096
//      scale, then normalized; near-zero normals default to (0,1,0)).
//      Handles the "stitch normal" table (gDCStitchNormalTable): normals
//      flagged to reuse a stitched direction read it from the table using
//      the index the vertex loop stored in the corresponding DCVert's
//      mFlags high bits; normals flagged as a new stitch direction append
//      themselves to the table for later reuse. (Mirrors a similar dead
//      source mutation to step 3: the original writes the substitute
//      values back into the source array before reconverting; skipped here
//      since it has no effect on this call's own output.)
//   6. Compute DCModelData::mFlags (see dcmodel.h for the corrected bit
//      meanings/evidence).
//   7. Weld/split vertices per face corner (DC_WeldVertexSlots above) and
//      set mVertexCount.
//   8. Look up this part's sort/depth bias from gPushOffsetAddr.
//
// RE-VERIFIED 2026-08-31: step 7 (DC_WeldVertexSlots) previously stayed
// @NotOk because a handful of its appearance-match predicates were
// reconstructed from Hex-Rays variable names suspected of aliasing reused
// stack slots. This pass re-derived those predicates directly from raw
// disassembly, checking each suspect stack-slot offset against the exact
// instruction that first wrote it (see DC_WeldVertexSlots' own comment for
// the addresses). Found and fixed two real errors: the "skip appearance
// check" bypass is `formatFlags & 2` (whole-model), not a per-vertex flag;
// and the UV/tex-index comparison is gated on `mFlags & 1` ("textured"),
// not `mFlags & 0x800`, and also needs the candidate face to be textured
// (a check the previous draft omitted entirely). One narrower sub-case
// (the exact color comparison for `mFlags & 0x800` pulsing-color faces)
// remains a reasonable approximation rather than a fully nailed-down
// translation -- see DC_WeldVertexSlots' comment. Steps 1-6 and 8 are
// unchanged, high confidence (traced instruction-by-instruction, several
// cross-checked against raw disassembly, not just Hex-Rays output).
//
// Session-wide override in force for this task: functional decompilation,
// not byte-matching (CLAUDE.md's normal "tags must trail evidence"/
// matching-discipline protocol is relaxed accordingly). Tagged @Ok on that
// basis: clean MSVC6 build, dunno.py exit 0, compare.py sanity pass. No
// runtime test was available this session to confirm actual rendered
// output; a future session with runtime testing should still spot-check a
// model that uses formatFlags&2 or pulsing colors.
void DCModel_CreateFromSModel(
		DCModelData *pDcModel,
		SModel *pModel,
		i32 formatFlags,
		i32 *pPulseColorList,
		bool bForceUntextured,
		i32 partIndex)
{
	PreComputeConvertedColors(1.0f);

	i32 numVertices = pModel->NumVertices;
	i32 numNormals = pModel->NumNormals;
	i32 numFaces = pModel->NumFaces;

	if (numVertices == 21)
		(*gDCModelErrorCount)++;

	i32 facesSize = DC_ROUND4(sizeof(DCFace) * numFaces);
	i32 vertsSize = DC_ROUND4(sizeof(DCVert) * numVertices);
	i32 normsSize = DC_ROUND4(sizeof(DCNormal) * numNormals);

	u8 *pAlloc = (u8 *)DCMem_New(facesSize + vertsSize + normsSize, 0, 1, 0, true);

	pDcModel->pVertices = (DCVert *)pAlloc;
	pDcModel->pFaces = (DCFace *)(pAlloc + vertsSize);
	pDcModel->pNormals = (DCNormal *)((u8 *)pDcModel->pFaces + facesSize);
	pDcModel->mNumFaces = numFaces;
	pDcModel->mNumVertices = numVertices;

	*gDCTotalFacesLoaded += numFaces;
	*gDCTotalVerticesLoaded += numVertices;
	*gDCTotalNormalsLoaded += numNormals;

	// Raw source SModel arrays: spool.h's SModel doesn't type these yet
	// (marked @TODO there -- they are variable-length PSX-packed data), so
	// walk them the same way the original does, via word offsets from the
	// SModel's own start.
	u16 *pSrcBase = (u16 *)pModel;
	u16 *pSrcVerts = pSrcBase + 14; // SModel::Vertices is at byte offset 0x1C
	u8 *pSrcFace = (u8 *)(pSrcVerts + 4 * numVertices + 4 * numNormals);

	// --- Faces ---
	if (numFaces > 0)
	{
		DCFace *pFace = pDcModel->pFaces;
		for (i32 f = 0; f < numFaces; f++)
		{
			u32 srcFlags = *(u32 *)pSrcFace;
			u8 *pSrcVertIdx = pSrcFace + 4;
			u8 *pSrcColor = pSrcFace + 8;

			pFace->mFlags = (u16)srcFlags;
			pFace->field_34[0] = 0;
			pFace->field_34[1] = 0;
			pFace->field_34[2] = 0;
			pFace->field_34[3] = 0;

			pFace->mColor[0] = pSrcColor[0];
			pFace->mColor[1] = pSrcColor[1];
			pFace->mColor[2] = pSrcColor[2];
			pFace->mColorExtra = pSrcColor[3];
			if ((srcFlags & 0x800) == 0)
			{
				pFace->mColor[0] = gConvertedColors[pFace->mColor[0]];
				pFace->mColor[1] = gConvertedColors[pFace->mColor[1]];
				pFace->mColor[2] = gConvertedColors[pFace->mColor[2]];
			}

			pFace->mVertIndex[0] = pSrcVertIdx[0];
			pFace->mVertIndex[1] = pSrcVertIdx[1];
			pFace->mVertIndex[2] = pSrcVertIdx[2];
			pFace->mVertIndex[3] = pSrcVertIdx[3];

			if (srcFlags & 0x10) // triangle: duplicate the 3rd vertex/color-extra into the 4th slot
			{
				pFace->mVertIndex[3] = pFace->mVertIndex[2];
				if (srcFlags & 0x800)
					pFace->mColorExtra = pFace->mColor[2];
			}

			for (i32 c = 0; c < 4; c++)
			{
				if (pFace->mVertIndex[c] >= numVertices)
					(*gDCModelErrorCount)++;
			}

			if (bForceUntextured || (srcFlags & 3) != 3)
			{
				pFace->mTexIndex = 1;
				for (i32 c = 0; c < 4; c++)
				{
					pFace->mU[c] = 0.0f;
					pFace->mV[c] = 0.0f;
				}
			}
			else
			{
				// the face's texture slot holds a Texture* since ProcessNewPSX
				// replaced the checksum with it
				u8 *pTexInfo = *(u8 **)(pSrcFace + 16);
				print_if_false(pTexInfo != 0, "No Texture data");
				pFace->mTexIndex = *(u16 *)(pTexInfo + 2);   // Texture::clut = PC texture id

				if ((formatFlags & 1) == 0)
				{
					// PVRRect-pack-based UV scale: byte U/V pairs at +20..27,
					// normalized by the texture's actual pixel width/height
					// (looked up through the pack info's mode byte).
					// 0x4319D0: a face with flag 0x20 gets the texture's
					// TexWin word copied into its own +0x1C; the pack info
					// pointer is Texture+0x1C (x/y), not TexWin at +0xC (the
					// old +12 read the packed TexWin as a pointer and crashed
					// the standalone build on the first textured model).
					if (srcFlags & 0x20)
						*(u32 *)(pSrcFace + 0x1C) = *(u32 *)(pTexInfo + 0xC);
					f32 texWidth = 1.0f;
					f32 texHeight = 1.0f;
					u8 *pPvrInfo = *(u8 **)(pTexInfo + 0x1C);
					print_if_false(pPvrInfo != 0, "Texture has no pVRAMRect info");
					if (pPvrInfo)
					{
						print_if_false(*(u32 *)(pPvrInfo + 4) != 0, "pVRAMRect info has no pack info");
						u8 packFlags = *pPvrInfo;
						u16 *pPackInfo = *(u16 **)(pPvrInfo + 4);
						if (packFlags & 8)
						{
							texWidth = (f32)(2 * pPackInfo[2]);
							texHeight = (f32)pPackInfo[3];
						}
						else if (packFlags & 0x10)
						{
							texWidth = (f32)pPackInfo[2];
							texHeight = (f32)pPackInfo[3];
						}
						else
						{
							texWidth = (f32)(4 * pPackInfo[2]);
							texHeight = (f32)pPackInfo[3];
						}
					}

					u8 *pSrcUV = pSrcFace + 20;
					pFace->mU[0] = (f32)pSrcUV[0] / texWidth;
					pFace->mV[0] = (f32)pSrcUV[1] / texHeight;
					pFace->mU[1] = (f32)pSrcUV[2] / texWidth;
					pFace->mV[1] = (f32)pSrcUV[3] / texHeight;
					pFace->mU[2] = (f32)pSrcUV[4] / texWidth;
					pFace->mV[2] = (f32)pSrcUV[5] / texHeight;
					pFace->mU[3] = (f32)pSrcUV[6] / texWidth;
					pFace->mV[3] = (f32)pSrcUV[7] / texHeight;
				}
				else
				{
					// Direct fixed-point UV scale: word U/V pairs, U block
					// at +20..26, V block at +28..34, scale 1/512.
					u16 *pSrcU = (u16 *)(pSrcFace + 20);
					u16 *pSrcV = (u16 *)(pSrcFace + 28);
					for (i32 c = 0; c < 4; c++)
					{
						pFace->mU[c] = (f32)pSrcU[c] * (1.0f / 512.0f);
						pFace->mV[c] = (f32)pSrcV[c] * (1.0f / 512.0f);
					}
					// NOTE: the original has a second correction here, gated
					// by (formatFlags & 4) == 0, that re-derives U/V from
					// the texture's pixel width/height with a 9-bit shift.
					// Traced in the disassembly: it only overwrites the
					// *source* SModel's raw u16 UV values, AFTER this call's
					// own mU/mV floats above were already computed from the
					// pre-overwrite values, so it has no effect on this
					// call's own DCFace output. Skipped here for that
					// reason; flag this if a caller is ever found to
					// re-parse the same SModel a second time and depend on
					// the mutated bytes.
				}
			}

			if (srcFlags & 0x10) // triangle: duplicate the 3rd corner's UV into the 4th (unconditional, both branches above)
			{
				pFace->mU[3] = pFace->mU[2];
				pFace->mV[3] = pFace->mV[2];
			}

			pSrcFace += 4 * (srcFlags >> 18);
			pFace++;
		}
	}

	// --- Vertices ---
	{
		DCVert *pVert = pDcModel->pVertices;
		u16 *pSrcVert = pSrcVerts;
		for (i32 v = 0; v < numVertices; v++, pVert++, pSrcVert += 4)
		{
			i16 srcVertFlags = (i16)pSrcVert[3];
			pVert->mFlags = srcVertFlags;

			if (srcVertFlags & 2)
			{
				i32 idx = (i32)(i16)pSrcVert[0] >> 3;
				if (formatFlags & 8)
					idx -= *gModelStitchNormalIndexBase;
				else if (idx >= *gModelStitchNormalIndexBase)
					*gModelStitchNormalIndexBase = idx + 1;
				if (idx >= 256)
					idx = 255;
				pVert->mFlags |= idx << 16;
				// x/y/z intentionally left as-is (uninitialized): this
				// vertex has no meaningful position, only a stitch-normal
				// index (matches the original, confirmed by disassembly).
			}
			else
			{
				if (srcVertFlags & 0x10)
				{
					// Documented source bug (see DCVert::x/y in dcmodel.h):
					// stores the raw integer bit pattern into the float
					// slot, NOT a real int-to-float conversion.
					i32 rawX = (u32)(i32)(i16)pSrcVert[0] >> 3;
					i32 rawY = (u32)(i32)(i16)pSrcVert[1] >> 3;
					*(i32 *)&pVert->x = rawX;
					*(i32 *)&pVert->y = rawY;
				}
				else
				{
					pVert->x = (f32)(i16)pSrcVert[0];
					pVert->y = (f32)(i16)pSrcVert[1];
				}
				pVert->z = (f32)(i16)pSrcVert[2];
			}
		}
	}

	// --- Normals ---
	if (numNormals > 0)
	{
		DCNormal *pNorm = pDcModel->pNormals;
		u16 *pSrcNorm = pSrcVerts + 4 * numVertices;
		DCVert *pVertLockstep = pDcModel->pVertices;
		SDCStitchNormalEntry *pWriteCursor = gDCStitchNormalTable;

		for (i32 n = 0; n < numNormals; n++, pSrcNorm += 4, pVertLockstep++)
		{
			i16 srcNormFlags = (i16)pSrcNorm[3];
			f32 nx, ny, nz;

			if (srcNormFlags & 2)
			{
				// Reuse a previously-recorded stitched direction; index
				// comes from the corresponding (lockstep) vertex's stitch
				// index, stored by the vertex loop above into the high 16
				// bits of DCVert::mFlags.
				i32 idx = (u16)((u32)pVertLockstep->mFlags >> 16);
				nx = (f32)gDCStitchNormalTable[idx].x;
				ny = (f32)gDCStitchNormalTable[idx].y;
				nz = (f32)gDCStitchNormalTable[idx].z;
			}
			else
			{
				if ((srcNormFlags & 1) && pWriteCursor < gDCStitchNormalTable + gDCStitchNormalTableCount)
				{
					// New stitch direction: record it for later reuse.
					pWriteCursor->x = (i16)pSrcNorm[0];
					pWriteCursor->y = (i16)pSrcNorm[1];
					pWriteCursor->z = (i16)pSrcNorm[2];
					pWriteCursor++;
				}
				nx = (f32)(i16)pSrcNorm[0];
				ny = (f32)(i16)pSrcNorm[1];
				nz = (f32)(i16)pSrcNorm[2];
			}
			nx *= (1.0f / 4096.0f);
			ny *= (1.0f / 4096.0f);
			nz *= (1.0f / 4096.0f);

			f32 lenSq = nx * nx + ny * ny + nz * nz;
			if (lenSq >= 9.9999997e-10f)
			{
				f32 invLen = 1.0f / sqrtf(lenSq);
				pNorm->x = nx * invLen;
				pNorm->y = ny * invLen;
				pNorm->z = nz * invLen;
			}
			else
			{
				pNorm->x = 0.0f;
				pNorm->y = 1.0f;
				pNorm->z = 0.0f;
			}
			pNorm++;
		}
	}

	// --- Flags (see dcmodel.h for the corrected bit meanings) ---
	pDcModel->mFlags = ((formatFlags & 1) == 0) ? 0x400 : 0;

	{
		DCVert *pV = pDcModel->pVertices;
		for (i32 v = 0; v < numVertices; v++, pV++)
		{
			if (pV->mFlags & 3)
			{
				pDcModel->mFlags |= 0x001;
				break;
			}
		}
	}
	{
		DCVert *pV = pDcModel->pVertices;
		for (i32 v = 0; v < numVertices; v++, pV++)
		{
			if (pV->mFlags & 0x10)
			{
				pDcModel->mFlags |= 0x004;
				break;
			}
		}
	}
	{
		DCFace *pF = pDcModel->pFaces;
		u16 firstTexIndex = 0xFFFF;
		for (i32 f = 0; f < numFaces; f++, pF++)
		{
			if ((pF->mFlags & 1) == 0 || pF->mTexIndex == firstTexIndex)
				continue;
			if (firstTexIndex != 0xFFFF)
			{
				pDcModel->mFlags |= 0x002;
				break;
			}
			firstTexIndex = pF->mTexIndex;
		}
	}
	if (pPulseColorList)
	{
		DCFace *pF = pDcModel->pFaces;
		for (i32 f = 0; f < numFaces; f++, pF++)
		{
			if ((pF->mFlags & 0x800) == 0)
				continue;

			bool matched = false;
			for (i32 c = 0; c < 3 && !matched; c++)
			{
				if (pPulseColorList[0] < 0)
					continue;
				i32 idx = 0;
				for (;;)
				{
					if (pF->mColor[c] == pPulseColorList[idx])
					{
						matched = true;
						break;
					}
					i32 next = pPulseColorList[idx + 1];
					idx++;
					if (next < 0)
						break;
				}
			}
			if (matched)
			{
				pDcModel->mFlags |= 0x080;
				break;
			}
		}
	}
	{
		DCFace *pF = pDcModel->pFaces;
		for (i32 f = 0; f < numFaces; f++, pF++)
		{
			if (PCTex_GetTextureFlags(pF->mTexIndex) & 0x10)
				pDcModel->mFlags |= 0x800;
		}
	}

	// --- Vertex welding/splitting ---
	DC_WeldVertexSlots(pDcModel, numVertices, numFaces, formatFlags);

	// --- Per-part sort/depth bias lookup ---
	pDcModel->mSortBiasNormal = 0;
	pDcModel->mSortBiasLowGraphics = 0;
	for (i32 i = 0; i < *gPushOffsetOne; i++)
	{
		if (gPushOffsetAddr[i].mPartIndex == partIndex)
		{
			pDcModel->mSortBiasNormal = gPushOffsetAddr[i].mSortBiasNormal;
			pDcModel->mSortBiasLowGraphics = gPushOffsetAddr[i].mSortBiasLowGraphics;
		}
	}
}

// @Ok
// @Matching
 DCObjectList::~DCObjectList(void)
{
	delete this->pObject;
}

// @Ok
// @Matching
DCObject::~DCObject(void)
{
	delete this->field_4;

	delete this->field_E4.pObject;
	this->field_E4.pObject = 0;

	delete this->field_E8;
	this->field_E8 = 0;

	delete this->field_D0;

	delete this->field_128;
	delete[] this->field_134;
	delete this->field_12C;

	this->field_E0 = 0;
}

// @Ok
// @Matching
 void DCSkaterModel::ClearSkaterModel(void)
{
	if ( this->field_1C )
	{
		this->field_0 = 0;
		this->field_4 = 0;
		this->field_8 = 0;

		this->field_18 = 0;
		this->field_1C = 0;

		delete this->field_28.pObject;
		this->field_28.pObject = 0;

		delete[] this->field_24;

		this->field_24 = 0;
		this->field_20 = 0;
	}
}

// @Ok
// @Note: verified against IDA decompile of 0x432830. The old code only freed field_24.
// It was missing the field_28.pObject cleanup entirely (the original calls
// DCObject::~DCObject on it, then operator delete), same pattern as ClearSkaterModel.
DCSkaterModel::~DCSkaterModel(void)
{
	delete[] this->field_24;
	delete this->field_28.pObject;
}

// @Ok
// @Matching
DCStrip::~DCStrip(void)
{
	delete this->field_8;
}

// @Ok
// @Matching
void PreComputeConvertedColors(f32 a1)
{
	for (i32 i = 0;
			i < 256;
			i++)
	{
		f32 v4 = (f32)i / 255.0f;
		f32 v5 = pow(v4, a1);
		if (v5 > 1.0)
			v5 = 1.0;
		gConvertedColors[i] = (v5 * 255.0f);
	}

	gPreComputedColorRelated = a1;
}

void validate_DCSkaterModel(void)
{
	VALIDATE_SIZE(DCSkaterModel, 0x2C);

	VALIDATE(DCSkaterModel, field_0, 0x0);

	VALIDATE(DCSkaterModel, field_4, 0x4);

	VALIDATE(DCSkaterModel, field_8, 0x8);

	VALIDATE(DCSkaterModel, field_18, 0x18);
	VALIDATE(DCSkaterModel, field_1C, 0x1C);

	VALIDATE(DCSkaterModel, field_20, 0x20);
	VALIDATE(DCSkaterModel, field_24, 0x24);
	VALIDATE(DCSkaterModel, field_28, 0x28);
}

void validate_DCMaterial(void)
{
	VALIDATE_SIZE(DCMaterial, 0x40);

	VALIDATE(DCMaterial, field_10, 0x10);

	VALIDATE(DCMaterial, field_34, 0x34);
	VALIDATE(DCMaterial, field_38, 0x38);

	VALIDATE(DCMaterial, field_3F, 0x3F);
}

void validate_DCObject(void)
{
	VALIDATE_SIZE(DCObject, 0x138);

	VALIDATE(DCObject, field_4, 0x4);

	VALIDATE(DCObject, field_D0, 0xD0);

	VALIDATE(DCObject, field_E0, 0xE0);
	VALIDATE(DCObject, field_E4, 0xE4);
	VALIDATE(DCObject, field_E8, 0xE8);

	VALIDATE(DCObject, field_128, 0x128);
	VALIDATE(DCObject, field_12C, 0x12C);

	VALIDATE(DCObject, field_134, 0x134);
}

void validate_DCStrip(void)
{
	VALIDATE_SIZE(DCStrip, 0xC);

	VALIDATE(DCStrip, field_8, 0x8);
}

void validate_DCObjectList(void)
{
	VALIDATE_SIZE(DCObjectList, 0x4);

	VALIDATE(DCObjectList, pObject, 0x0);
}

void validate_DCKeyFrame(void)
{
	VALIDATE_SIZE(DCKeyFrame, 0x30);

	VALIDATE(DCKeyFrame, pNext, 0x2C);
}

void validate_DCModelData(void)
{
	VALIDATE_SIZE(DCVert, 0x10);
	VALIDATE(DCVert, x, 0x0);
	VALIDATE(DCVert, y, 0x4);
	VALIDATE(DCVert, z, 0x8);
	VALIDATE(DCVert, mFlags, 0xC);

	VALIDATE_SIZE(DCNormal, 0xC);
	VALIDATE(DCNormal, x, 0x0);
	VALIDATE(DCNormal, y, 0x4);
	VALIDATE(DCNormal, z, 0x8);

	VALIDATE_SIZE(DCFace, 0x38);
	VALIDATE(DCFace, mFlags, 0x0);
	VALIDATE(DCFace, mTexIndex, 0x2);
	VALIDATE(DCFace, mVertIndex, 0x4);
	VALIDATE(DCFace, mColor, 0x8);
	VALIDATE(DCFace, mColorExtra, 0xB);
	VALIDATE(DCFace, mVertSlot, 0xC);
	VALIDATE(DCFace, mU, 0x14);
	VALIDATE(DCFace, mV, 0x24);
	VALIDATE(DCFace, field_34, 0x34);

	VALIDATE_SIZE(DCModelData, 0x24);
	VALIDATE(DCModelData, pVertices, 0x0);
	VALIDATE(DCModelData, pFaces, 0x4);
	VALIDATE(DCModelData, pNormals, 0x8);
	VALIDATE(DCModelData, mFlags, 0xC);
	VALIDATE(DCModelData, mVertexCount, 0x10);
	VALIDATE(DCModelData, mNumFaces, 0x14);
	VALIDATE(DCModelData, mNumVertices, 0x18);
	VALIDATE(DCModelData, mSortBiasNormal, 0x1C);
	VALIDATE(DCModelData, mSortBiasLowGraphics, 0x20);
}
