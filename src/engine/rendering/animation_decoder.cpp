#include "engine/rendering/animation_decoder.h"
#include "engine/world/ob.h"
#include "engine/rendering/ps2funcs.h"
#include "engine/assets/spool.h"
#include "engine/rendering/m3dutils.h"
#include "core/game_heap.h"
#include "compatibility/my_assert.h"

// Scratch buffer for a tweened ("in-between") animation frame. Same address
// as m3dutils.cpp's gTweenBuffer (0x5FC250): confirmed here too, since this
// function returns exactly that address right after calling
// M3dUtils_InBetween (see the "zapped, tweened" branch below). Duplicated
// as a file-local static per repo convention for statics shared across TUs.
static SMatrix* const gTweenBuffer = reinterpret_cast<SMatrix*>(0x5FC250);

// Mode-nibble -> decode-scheme lookup table read directly out of the
// original binary at 0x00433D44 (byte_433D44, 16 bytes):
// {0,1,1,1,1,1,1,1,1,1,1,1,1,1,2,3}.
static const u8 gDecompressStreamModeTable[16] =
{
	0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2, 3
};

// Decodes one interleaved "channel" of compressed keyframe data (one of the
// 6 raw i16 fields -- 3 Euler angle components then 3 translation
// components -- that Decomp_GetAnimTransform stores per part per frame)
// into pDest[0], pDest[Stride], pDest[2*Stride], ..., NumSamples entries
// total (Stride in i16 units). Returns the advanced stream pointer.
// Confirmed named "DecompressStream" in the maintainer's IDB, address
// 0x00433A60; only caller is Decomp_GetAnimTransform. Decompiled fresh
// from Hex-Rays pseudocode cross-checked against the raw disassembly.
//
// The low nibble of the first stream byte selects the scheme via
// gDecompressStreamModeTable; the high nibble (+1) is the interpolation
// block size (BlockSize): every BlockSize-th sample is stored exactly (an
// explicit "breakpoint"), the samples in between are linearly interpolated
// with a single per-step delta (avoids drift accumulating over a long
// animation). A final, possibly-shorter block covers the samples left over
// after dividing (NumSamples - 1) by BlockSize.
//   table value 0 -> case 0: breakpoints stored as raw absolute i16 values.
//   table value 1 -> case 1: breakpoints stored as a bit-packed signed
//                    delta (from the block's start value), delta width in
//                    bits is (mode nibble + 1).
//   table value 2 -> case 2: constant fill (one i16 value repeated
//                    NumSamples times, no block/breakpoint scheme).
//   table value 3 -> case 3: zero fill.
// @Ok
static u8* DecompressStream(u8* pStream, i16* pDest, i32 Stride, i32 NumSamples)
{
	u8 Header = *pStream;
	u8* pNext = pStream + 1;
	i32 Mode = Header & 0xF;
	i32 BlockSize = (Header >> 4) + 1;

	// 0x433A86: eax = NumSamples - 1 and the divide is skipped for a block
	// size of 1, so every sample is its own breakpoint (FullBlocks =
	// NumSamples - 1, Remainder = 0). Setting FullBlocks to 0 here read only
	// the first value and left every later channel of the anim misaligned.
	i32 FullBlocks = NumSamples - 1;
	i32 Remainder = 0;
	if (BlockSize > 1)
	{
		FullBlocks = (NumSamples - 1) / BlockSize;
		Remainder = (NumSamples - 1) - BlockSize * FullBlocks;
	}

	switch (gDecompressStreamModeTable[Mode])
	{
	case 0:
	{
		i16 Value = static_cast<i16>(pNext[0] | (pNext[1] << 8));
		pNext += 2;
		*pDest = Value;
		i16* pOut = pDest + Stride;

		for (i32 b = 0; b < FullBlocks; b++)
		{
			i16 EndValue = static_cast<i16>(pNext[0] | (pNext[1] << 8));
			pNext += 2;
			i32 Delta = (EndValue - Value) / BlockSize;

			for (i32 s = 0; s < BlockSize - 1; s++)
			{
				Value = static_cast<i16>(Value + Delta);
				*pOut = Value;
				pOut += Stride;
			}

			*pOut = EndValue;
			pOut += Stride;
			Value = EndValue;
		}

		if (Remainder != 0)
		{
			i16 EndValue = static_cast<i16>(pNext[0] | (pNext[1] << 8));
			pNext += 2;
			i32 Delta = (EndValue - Value) / Remainder;

			for (i32 s = 0; s < Remainder - 1; s++)
			{
				Value = static_cast<i16>(Value + Delta);
				*pOut = Value;
				pOut += Stride;
			}

			*pOut = EndValue;
		}

		return pNext;
	}

	case 1:
	{
		i32 BitWidth = Mode + 1;
		i16 Value = static_cast<i16>(pNext[0] | (pNext[1] << 8));
		pNext += 2;
		*pDest = Value;
		i16* pOut = pDest + Stride;
		i32 BitPos = 0;

		for (i32 b = 0; b < FullBlocks; b++)
		{
			i32 Raw = (pNext[0] << 8 | pNext[1]) << 8 | pNext[2];
			i32 Delta = (Raw << (BitPos + 8)) >> (32 - BitWidth);
			i32 NewBitPos = BitPos + BitWidth;
			pNext += NewBitPos >> 3;
			BitPos = NewBitPos & 7;

			i16 EndValue = static_cast<i16>(Value + Delta);
			i32 Step = Delta / BlockSize;

			for (i32 s = 0; s < BlockSize - 1; s++)
			{
				Value = static_cast<i16>(Value + Step);
				*pOut = Value;
				pOut += Stride;
			}

			Value = EndValue;
			*pOut = EndValue;
			pOut += Stride;
		}

		if (Remainder != 0)
		{
			i32 Raw = (pNext[0] << 8 | pNext[1]) << 8 | pNext[2];
			i32 Delta = (Raw << (BitPos + 8)) >> (32 - BitWidth);
			i32 NewBitPos = BitPos + BitWidth;
			pNext += NewBitPos >> 3;
			BitPos = NewBitPos & 7;

			i16 EndValue = static_cast<i16>(Value + Delta);
			i32 Step = Delta / Remainder;

			for (i32 s = 0; s < Remainder - 1; s++)
			{
				Value = static_cast<i16>(Value + Step);
				*pOut = Value;
				pOut += Stride;
			}

			*pOut = EndValue;
		}

		return (BitPos == 0) ? pNext : (pNext + 1);
	}

	case 2:
	{
		i16 Value = static_cast<i16>(pNext[0] | (pNext[1] << 8));
		pNext += 2;
		i16* pOut = pDest;
		for (i32 s = 0; s < NumSamples; s++)
		{
			*pOut = Value;
			pOut += Stride;
		}
		return pNext;
	}

	case 3:
	default:
	{
		i16* pOut = pDest;
		for (i32 s = 0; s < NumSamples; s++)
		{
			*pOut = 0;
			pOut += Stride;
		}
		return pNext;
	}
	}
}

// @Ok
// Decompiled fresh from Hex-Rays + raw disasm of 0x00433D60 (1416 bytes,
// matches prototypes.json's Mac size 1392). Resolves the per-part pose
// matrix array for pSuper's CURRENT anim/frame. pAnimFile's per-anim table
// entry (G_PSXREGION[region].pAnimFile, 2 i32 per anim starting at index 2)
// carries {streamByteOffset, rawFrameCountOrIntervalBits}; a signature i32
// stored 2 elements before pAnimFile ('*'=42 or ','=44, read the same way
// M3dUtils_ReadLinksPacket/BuildPose read a header just before mpLinks)
// selects between two totally different encodings:
//  - tag '*' (zapped/single-source PSX): if the table entry's high 16 bits
//    are set, it means this anim needs inter-frame tweening, so it defers
//    to M3dUtils_InBetween and returns gTweenBuffer (matches
//    RenderSuperItemShadow's mik reference: "if posed use mpPoseBuffer,
//    else Decomp_GetAnimTransform", and M3dUtils_InBetween's own frame/
//    interval math); otherwise it returns a pointer straight into the raw
//    pAnimFile blob (frame-major array of SMatrix, PSX-part-count wide,
//    computed from G_PSXREGION[region].pPSX[2] as the part count and the
//    table entry's low bytes as the per-anim data offset).
//  - tag ',' (full hierarchy): builds pSuper->mpCalculationOrder (a
//    breadth-first traversal order over G_PSXREGION[region].pHierarchy,
//    a parent-index-per-part array where a part whose own index is its own
//    parent is the root) and pSuper->mRoot the first time it is called for
//    this CSuper, then whenever the animation number changes decompresses
//    every part's per-frame Euler-angle + translation samples (via
//    DecompressStream above) into a raw cache living past the end of the
//    returned SMatrix[NumParts] array inside the SAME allocation
//    (mpDecompressedFrame), and whenever the frame number changes (or the
//    cache was just rebuilt) composes the returned array: the root part's
//    rotation comes straight from RotMatrixYXZ(rootAngles) and its
//    translation is copied as-is; every other part (walked in calculation
//    order, so a part's parent is always already computed) gets its own
//    rotation the same way, and its translation is its parent's rotation
//    matrix applied to its own local translation sample (via the
//    already-decompiled gte_mvmva/gte_stsv, sf=1 so the >>12 fixed-point
//    shift happens) plus the parent's translation -- the same rigid-body
//    hierarchy composition M3dUtils_BuildPose does in software, here done
//    through the GTE-emulation helpers (gte_SetRotMatrix/MTC2/gte_mvmva/
//    gte_stsv, all already @Ok in ps2funcs.cpp).
// NumParts itself is read from a hidden i32 stored immediately before
// G_PSXREGION[region].ppModels's pointed-to array, the same "count just
// before the array" idiom already used for mpLinks/NumJoints in
// M3dUtils_ReadLinksPacket/BuildPose.
SMatrix* Decomp_GetAnimTransform(CSuper* pSuper)
{
	print_if_false(pSuper != NULL, "NULL pSuper sent to Decomp_GetAnimTransform");

	u8 Region = pSuper->mRegion;
	SPSXRegion& Rgn = G_PSXREGION[Region];

	i32* pAnim = reinterpret_cast<i32*>(Rgn.pAnimFile);
	i32 NumParts = *(reinterpret_cast<u32*>(Rgn.ppModels) - 1);
	i32 PsxNumParts = static_cast<i32>(Rgn.pPSX[2]);

	print_if_false(pAnim != NULL, "NULL pAnimFile in pSuper");
	print_if_false(pSuper->mAnim < static_cast<u32>(pAnim[0]), "Bad anim number in pSuper");
	print_if_false(
		static_cast<u32>(static_cast<i32>(pSuper->mFrame)) < static_cast<u32>(pAnim[2 * pSuper->mAnim + 2]),
		"Bad frame number in pSuper");

	i32 PacketTag = pAnim[-2];

	if (PacketTag == 42) // '*' - zapped/single-source packet
	{
		if ((pAnim[2 * pSuper->mAnim + 2] & 0xFFFF0000) != 0)
		{
			M3dUtils_InBetween(pSuper);
			return gTweenBuffer;
		}

		return reinterpret_cast<SMatrix*>(
			reinterpret_cast<char*>(pAnim)
			+ 24 * PsxNumParts * pSuper->mFrame
			+ pAnim[2 * pSuper->mAnim + 1]);
	}

	if (PacketTag != 44) // ',' - full hierarchy packet
	{
		print_if_false(0, "Bad packet");
		return NULL;
	}

	print_if_false(NumParts == PsxNumParts, "LOD models in zapped PSXs not supported yet");

	bool DidRebuildBuffers = false;

	if (pSuper->mpDecompressedFrame == NULL)
	{
		i32 MaxFrameCount = 0;
		i32 NumAnims = pAnim[0];
		for (i32 AnimIdx = 0; AnimIdx < NumAnims; AnimIdx++)
		{
			i32 Raw = pAnim[2 * AnimIdx + 2];
			if (Raw > MaxFrameCount)
				MaxFrameCount = Raw;
		}

		pSuper->mpDecompressedFrame = static_cast<SMatrix*>(
			DCMem_New(12 * NumParts * (MaxFrameCount + 2), 1, 1, NULL, true));

		print_if_false(Rgn.pHierarchy != NULL, "Hierarchy required to decompress anim");
		print_if_false(pSuper->mpCalculationOrder == NULL, "CalculationOrder array already exists?");

		pSuper->mpCalculationOrder = static_cast<u16*>(Mem_New(2 * NumParts));

		pSuper->mRoot = 0xFFFF;
		for (u32 i = 0; i < static_cast<u32>(NumParts); i++)
		{
			if (Rgn.pHierarchy[i] == i)
			{
				print_if_false(pSuper->mRoot == 0xFFFF, "More than one root in hierarchy");
				pSuper->mRoot = static_cast<u16>(i);
			}
		}
		print_if_false(pSuper->mRoot != 0xFFFF, "No root found in hierarchy");

		pSuper->mpCalculationOrder[0] = pSuper->mRoot;

		if (NumParts != 1)
		{
			u16* pPlaced = pSuper->mpCalculationOrder;
			u16* pWrite = pSuper->mpCalculationOrder + 1;
			i32 Remaining = NumParts - 1;

			do
			{
				for (u32 j = 0; j < static_cast<u32>(NumParts); j++)
				{
					if (j != pSuper->mRoot && Rgn.pHierarchy[j] == *pPlaced)
					{
						*pWrite++ = static_cast<u16>(j);
						--Remaining;
						print_if_false(Remaining >= 0, "Error in calculating order");
					}
				}
				pPlaced++;
			} while (Remaining != 0);
		}

		DidRebuildBuffers = true;
	}

	bool DidDecompressAnim = false;

	if (pSuper->mDecompressedAnim != pSuper->mAnim || DidRebuildBuffers)
	{
		i32 NumFrames = pAnim[2 * pSuper->mAnim + 2];
		u8* pStream = reinterpret_cast<u8*>(pAnim) + pAnim[2 * pSuper->mAnim + 1];
		i32 Stride = 6 * NumParts;

		i16* pRawCache = reinterpret_cast<i16*>(pSuper->mpDecompressedFrame) + 2 * Stride;

		for (i32 Part = 0; Part < NumParts; Part++)
		{
			i16* pRawPart = pRawCache + Part * 6;
			pStream = DecompressStream(pStream, pRawPart + 0, Stride, NumFrames);
			pStream = DecompressStream(pStream, pRawPart + 1, Stride, NumFrames);
			pStream = DecompressStream(pStream, pRawPart + 2, Stride, NumFrames);
			pStream = DecompressStream(pStream, pRawPart + 3, Stride, NumFrames);
			pStream = DecompressStream(pStream, pRawPart + 4, Stride, NumFrames);
			pStream = DecompressStream(pStream, pRawPart + 5, Stride, NumFrames);
		}

#ifdef SPIDEY_STANDALONE
		// SPIDEY_TRACE_PARTS=1: check that the decoder consumed exactly the
		// bytes the anim table says this anim occupies
		if (getenv("SPIDEY_TRACE_PARTS"))
		{
			i32 NumAnimsDbg = pAnim[0];
			u8* pEnd = reinterpret_cast<u8*>(pAnim) + ((pSuper->mAnim + 1 < NumAnimsDbg) ? pAnim[2 * (pSuper->mAnim + 1) + 1] : 0);
			fprintf(stderr, "DECOMP region=%d anim=%d frames=%d numParts=%d psxParts=%d consumed=%d expected=%d%s\n", Region, pSuper->mAnim, NumFrames, NumParts, PsxNumParts,
				(i32)(pStream - (reinterpret_cast<u8*>(pAnim) + pAnim[2 * pSuper->mAnim + 1])),
				(pSuper->mAnim + 1 < NumAnimsDbg) ? (i32)(pEnd - (reinterpret_cast<u8*>(pAnim) + pAnim[2 * pSuper->mAnim + 1])) : -1,
				(pSuper->mAnim + 1 < NumAnimsDbg && pStream != pEnd) ? "  MISMATCH" : "");
		}
#endif

		pSuper->mDecompressedAnim = pSuper->mAnim;
		DidDecompressAnim = true;
	}

	if (pSuper->mDecompressedFrame != pSuper->mFrame || DidDecompressAnim)
	{
		i32 Stride = 6 * NumParts;
		i16* pRawCache = reinterpret_cast<i16*>(pSuper->mpDecompressedFrame) + 2 * Stride;
		i16* pRawFrame = pRawCache + pSuper->mFrame * Stride;

		SMatrix* pOutput = pSuper->mpDecompressedFrame;

		i16* pRootRaw = pRawFrame + pSuper->mRoot * 6;
		SMatrix& RootOut = pOutput[pSuper->mRoot];

		M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR*>(pRootRaw), reinterpret_cast<MATRIX*>(&RootOut));
		RootOut.t[0] = pRootRaw[3];
		RootOut.t[1] = pRootRaw[4];
		RootOut.t[2] = pRootRaw[5];

		if (NumParts > 1)
		{
			u16* pOrder = pSuper->mpCalculationOrder + 1;
			for (i32 n = 0; n < NumParts - 1; n++)
			{
				i32 Part = pOrder[n];
				i32 Parent = Rgn.pHierarchy[Part];

				SMatrix& ParentOut = pOutput[Parent];
				SMatrix& ChildOut = pOutput[Part];
				i16* pChildRaw = pRawFrame + Part * 6;

				M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR*>(pChildRaw), reinterpret_cast<MATRIX*>(&ChildOut));

				print_if_false(1, "Bad px offset in SEulerPivot");

				gte_SetRotMatrix(reinterpret_cast<MATRIX*>(&ParentOut));

				MTC2(static_cast<i32>(static_cast<u16>(pChildRaw[3])) | (static_cast<i32>(pChildRaw[4]) << 16), GT_ZERO);
				MTC2(pChildRaw[5], GT_ONE);

				gte_mvmva(1, 0, 0, 3, 0);
				gte_stsv(reinterpret_cast<SVECTOR*>(ChildOut.t));

				ChildOut.t[0] += ParentOut.t[0];
				ChildOut.t[1] += ParentOut.t[1];
				ChildOut.t[2] += ParentOut.t[2];
			}
		}

		pSuper->mDecompressedFrame = pSuper->mFrame;
	}

	return pSuper->mpDecompressedFrame;
}
