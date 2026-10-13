#include "game/effects/shatter.h"
#include "core/utils.h"
#include "engine/world/collision.h"
#include "engine/world/m3dzone.h"
#include "engine/rendering/ps2funcs.h"
#include "engine/world/bit.h"
#include "game/cinematics/camera.h"
#include "core/game_heap.h"
#include "game/effects/exp.h"
#include <string.h>

#ifndef SPIDEY_STANDALONE
i32 gGlassShatterSound;
#else
extern i32 gGlassShatterSound;
#endif

// gGlassShatterSound is fixed at 0x6A7690 in the original binary (confirmed name and
// address from the maintainer's IDB, idb_globals.txt). Used only inside shatter.cpp.
//#define G_GLASS_SHATTER_SOUND (gGlassShatterSound)
#define G_GLASS_SHATTER_SOUND (*reinterpret_cast<i32*>(0x006A7690))

// guess: cached RGB color for the current shattered glass piece, only read/written inside
// shatter.cpp (CalcRGB writes it, Shatter_Face/Split presumably read it). Byte order (g,b,r
// at +0,+1,+2) is our guess from the CalcRGB store order, not confirmed against the maintainer's IDB.
struct SShatterColor
{
	u8 g;
	u8 b;
	u8 r;
};
static SShatterColor * const gShatterColor = (SShatterColor*)0x006A7684;

// @Ok
// @Matching
void Shatter_MaybeMakeGlassShatterSound(void)
{
	G_GLASS_SHATTER_SOUND = 0;
}

// @Ok
// 2026-08-31: session bar is functional decomp, not byte match (see task instructions).
// Logic verified by hand-tracing the original disassembly at 0x48CEB0 (314 bytes,
// tools/functions/4771504.bin, no tools/names.json entry, sits unnamed between Split
// 0x48C730 and Shatter_Item 0x48CFF0): count==0 stores color's low 3 bytes directly,
// otherwise averages table[] lookups of color's bytes 0/1/2, plus byte 3 too when mode==4,
// dividing by 3 or 4. Self-contained (no calls to other shatter.cpp functions), so its
// codegen is not polluted by any stub in this TU. cmpsum.sh 0x48CEB0 "?CalcRGB@@YAXHIHPAI@Z" .
// on the 2026-08-31 rebuild: 85 mnemonic diffs, all register/push-order scheduling from the
// top, same class of residue documented before; no missing/extra instructions (this is a
// byte-match residue only, out of scope this session).
void CalcRGB(i32 count, u32 color, i32 mode, u32 *table)
{
	if (count != 0)
	{
		u32 colorA = table[color & 0xFF];
		u32 colorB = table[(color >> 8) & 0xFF];
		u32 colorC = table[(color >> 16) & 0xFF];

		if (mode == 4)
		{
			u32 colorD = table[color >> 24];
			i32 sumR = (i32)(colorA & 0xFF) + (i32)(colorB & 0xFF) + (i32)(colorC & 0xFF) + (i32)(colorD & 0xFF);
			i32 sumG = (i32)((colorA >> 8) & 0xFF) + (i32)((colorB >> 8) & 0xFF) + (i32)((colorC >> 8) & 0xFF) + (i32)((colorD >> 8) & 0xFF);
			i32 sumB = (i32)((colorA >> 16) & 0xFF) + (i32)((colorB >> 16) & 0xFF) + (i32)((colorC >> 16) & 0xFF) + (i32)((colorD >> 16) & 0xFF);
			gShatterColor->r = (u8)(sumR / 4);
			gShatterColor->g = (u8)(sumG / 4);
			gShatterColor->b = (u8)(sumB / 4);
		}
		else
		{
			i32 sumR = (i32)(colorA & 0xFF) + (i32)(colorB & 0xFF) + (i32)(colorC & 0xFF);
			i32 sumG = (i32)((colorA >> 8) & 0xFF) + (i32)((colorB >> 8) & 0xFF) + (i32)((colorC >> 8) & 0xFF);
			i32 sumB = (i32)((colorA >> 16) & 0xFF) + (i32)((colorB >> 16) & 0xFF) + (i32)((colorC >> 16) & 0xFF);
			gShatterColor->r = (u8)(sumR / 3);
			gShatterColor->g = (u8)(sumG / 3);
			gShatterColor->b = (u8)(sumB / 3);
		}
	}
	else
	{
		gShatterColor->r = (u8)color;
		gShatterColor->g = (u8)(color >> 8);
		gShatterColor->b = (u8)(color >> 16);
	}
}

// guess: per-region array of per-model glass geometry pointers, indexed as
// gShatterRegionModelTable[item->mRegion * 17]. Stride 17 (region*16+region in the disasm)
// matches a struct-of-17-u32 per region; we don't know the other 16 slots, no idb_globals.txt
// entry for this address, name and stride guess are ours only. Moved above Shatter_Face
// (2026-08-31) since Shatter_Face now needs it too; was previously only above Shatter_Item.
static void ** const gShatterRegionModelTable = (void**)0x006B2454;

// guess: header of a per-model shatter face list. field_2/field_4 are added and used to
// offset into the face array (shifted-pointer struct-array pattern, see CLAUDE.md tips);
// mNumFaces is the loop trip count. Field names are ours, not confirmed anywhere. Moved above
// Shatter_Face (2026-08-31), see gShatterRegionModelTable's comment just above.
struct SShatterModelInfo
{
	u16 field_0;
	u16 field_2;
	u16 field_4;
	u16 mNumFaces;
};

// guess: per-region array of 17 u32* color tables, same region*17 stride as
// gShatterRegionModelTable (20 bytes after it in memory: 0x6B2454 + 0x14 = 0x6B2468). Each
// entry is a straight color lookup table pointer (indexed by byte inside CalcRGB), no
// per-model indexing, unlike gShatterRegionModelTable. No idb_globals.txt entry, name is ours.
static u32 ** const gShatterRegionColorTable = (u32**)0x006B2468;

// Per-face UV/data record (16 bytes), pointed to either by a pointer embedded in the face
// record itself (face+0x10, when face+0 bit0 is set) or, when that bit is clear, directly by
// the caller-supplied "markProcessed" parameter reinterpreted as this pointer type. This is
// the "per-face UV/data record read through an int-reinterpreted-as-pointer parameter" the
// prior session flagged as an unconfirmed guess (see Shatter_Face's own comment below) -- now
// confirmed field-by-field against the raw disassembly (0x48C0D0: the reads at v17+0/1/4/5/8/9
// forward straight into Split's a4..a9 UV args, and v17+2/+6 (u16) forward into
// G_SHATTER_UV_TEX_ID/G_SHATTER_UV_UNUSED, shatter.h). u3/v3 (offset 10/11) are only read by
// the quad path's second Split() call. mPackedColor (offset 12, dword) is read only when face+0
// bit 0x20 is set, and forwards straight into Split's a10 "shard color" arg either way (0 when
// that bit is clear) -- both the tri and quad branches read the exact same offset for it.
struct SShatterFaceUV
{
	u8 u0, v0;
	u16 texId;
	u8 u1, v1;
	u16 unused;
	u8 u2, v2;
	u8 u3, v3;
	u32 mPackedColor;
};

// @Ok
// 2026-08-31: session bar is functional decomp, not byte match (see task instructions).
// Implemented via a fresh IDA Hex-Rays decompile + raw disasm walk of 0x48C0D0 (1632 bytes,
// tools/functions/4767952.bin), re-verifying (not just transcribing) the prior session's
// parameter-role notes now that CShatterBit and Split are both real. All of that prior
// reconnaissance held up; the one flagged "guess" (the per-face UV record read through an
// int-reinterpreted-as-pointer parameter) is now confirmed field-by-field, see SShatterFaceUV
// above. One new finding this session: 0x6A768C (G_SHATTER_VELOCITY_SCALE, shatter.h) is not a
// fixed table -- Shatter_Face sets it itself, every call, to the current face's normal vector;
// see shatter.h's updated comment.
//
// Parameter roles: item, face as before. splitDepth forwards to Split's final "recursion
// depth" arg. doGlowFlash gates the Exp_GlowFlash call at the end. checkState, if 0, skips the
// face-state ("seen") bit checks entirely. markProcessed, when checkState is also nonzero,
// marks the face processed (state bit0) and clears face+0 bits 0x80/0x100; when the face's own
// UV-record pointer bit (face+0 bit0) is clear, markProcessed is reused directly as that
// pointer (see SShatterFaceUV). chanceFlag, if 0, returns 1 immediately (after any state-bit
// mutation above).
//
// Return value: 0 = face not eligible (state bit3 clear, or bit0 already set = already
// processed). 1 = default / recursive-split-via-Split() path (or an early chanceFlag==0
// return). 2 = "leaf" path: Shatter_Glass() called directly (count 15 for a triangle face, 30
// for a quad), no further splitting. Shatter_Item only increments its numShattered counter
// when the result is exactly 1, capping how many faces per call get the expensive recursive
// split.
//
// Body: two symmetric branches selected by face+0 bit 0x10 (SET = triangle, already-loaded 3
// vertices; CLEAR = quad, loads a 4th). Each sets up the rotation matrix / translation
// (M3dMaths_RotMatrixYXZ(item->mAngles) + gte_SetRotMatrix, M3dAsm_SetTransVector(item->mPos)),
// transforms its vertices via TransformVertex (below; fully inlined in the original, see its
// own comment), calls CalcRGB(face+0 & 0x800, faceColorDword, 3 or 4, gShatterRegionColorTable[
// item->mRegion*17]), averages the transformed corners into G_SHATTER_FACE_CENTER (shatter.h),
// then, gated by LowMemory == 0 (mem.h) and face+0 bits 0x40/0x1, either calls Split() (once
// for a triangle, twice for a quad -- one call per half, sharing the split diagonal's UV pair)
// or Shatter_Glass() directly using the face normal (G_SHATTER_VELOCITY_SCALE, shatter.h) and
// gShatterColor (set by the CalcRGB call just above). Falls through to the doGlowFlash-gated
// Exp_GlowFlash(&G_SHATTER_FACE_CENTER, 100, r, g, b, 4, 1, 100) at the end regardless of which
// sub-path ran.
//
// Field offsets used on CItem: mAngles@0x14, mPos@0x8, mModel@0x1A, mRegion@0x1F, confirmed via
// VALIDATE in ob.cpp (matches Shatter_Item's own reads). SShatterModelInfo/
// gShatterRegionModelTable lookup is identical to Shatter_Item's. info->field_2 * 8 + 0x1C gives
// a second table pointer used only for the face-normal lookup; the raw per-face byte vertex
// indices (face+4..+7) index directly into info+0x1C (the vertex table) with no field_2 offset,
// stride 8 (matches SVECTOR's 8-byte size, ps2funcs.h).
i32 Shatter_Face(CItem *item, u32 *faceArg, i32 splitDepth, i32 doGlowFlash, i32 checkState, i32 markProcessed, i32 chanceFlag)
{
	print_if_false(item != 0, "NULL pItem");
	print_if_false(faceArg != 0, "NULL pFace");

	u16 *face = (u16*)faceArg;

	void **regionModels = (void**)gShatterRegionModelTable[item->mRegion * 17];
	SShatterModelInfo *info = (SShatterModelInfo*)regionModels[item->mModel];
	u8 *vertexTableBase = (u8*)info + 0x1C;
	u8 *normalTableBase = vertexTableBase + 8 * info->field_2;

	if (checkState != 0)
	{
		u16 state = face[7]; // face+14: "seen" state word
		if ((state & 8) == 0)
			return 0;
		if ((state & 1) != 0)
			return 0;

		if (markProcessed != 0)
		{
			state |= 1;
			face[7] = state;
			face[0] &= 0xFE7F; // clear bits 0x80/0x100

			u16 flags = face[0];
			if (chanceFlag == 0)
			{
				if ((flags & 0x40) != 0 || (flags & 1) == 0)
					face[0] = flags & 0xFFBF; // clear bit 0x40
				return 1;
			}
		}
		else if (chanceFlag == 0)
		{
			return 1;
		}
	}
	else if (chanceFlag == 0)
	{
		return 1;
	}

	SShatterFaceUV *uvRecord;
	if (face[0] & 1)
	{
		uvRecord = *(SShatterFaceUV**)((u8*)face + 16);
		G_SHATTER_UV_TEX_ID = uvRecord->texId;
		G_SHATTER_UV_UNUSED = uvRecord->unused;
	}
	else
	{
		uvRecord = (SShatterFaceUV*)markProcessed;
	}

	u16 normalIdx = face[6] >> 3; // face+12
	G_SHATTER_VELOCITY_SCALE = (i16*)(normalTableBase + 8 * normalIdx);

	MATRIX rotMat;
	M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR*>(&item->mAngles), &rotMat);
	gte_SetRotMatrix(&rotMat);
	M3dAsm_SetTransVector(reinterpret_cast<VECTOR*>(&item->mPos));

	SVECTOR *vertexTable = (SVECTOR*)vertexTableBase;
	u8 *vertexIdx = (u8*)face + 4;

	CVector cornerA, cornerB, cornerC;
	TransformVertex(&cornerA, vertexTable, vertexIdx, 0);
	TransformVertex(&cornerB, vertexTable, vertexIdx, 1);
	TransformVertex(&cornerC, vertexTable, vertexIdx, 2);

	u16 flags = face[0];
	i32 result = 1;

	if (flags & 0x10)
	{
		// triangle face
		CalcRGB(flags & 0x800, *(u32*)((u8*)face + 8), 3, gShatterRegionColorTable[item->mRegion * 17]);

		G_SHATTER_FACE_CENTER.vx = (((cornerA.vx >> 12) + (cornerB.vx >> 12) + (cornerC.vx >> 12)) / 3) << 12;
		G_SHATTER_FACE_CENTER.vy = (((cornerA.vy >> 12) + (cornerB.vy >> 12) + (cornerC.vy >> 12)) / 3) << 12;
		G_SHATTER_FACE_CENTER.vz = (((cornerA.vz >> 12) + (cornerB.vz >> 12) + (cornerC.vz >> 12)) / 3) << 12;

		flags = face[0];
		if ((flags & 0x40) == 0 && (flags & 1) != 0)
		{
			u32 shardColor = (flags & 0x20) ? uvRecord->mPackedColor : 0;
			if (LowMemory == 0)
			{
				Split(&cornerA, &cornerB, &cornerC,
						uvRecord->u0, uvRecord->v0,
						uvRecord->u1, uvRecord->v1,
						uvRecord->u2, uvRecord->v2,
						shardColor, splitDepth);
			}
			result = 1;
		}
		else
		{
			if (LowMemory == 0)
			{
				i16 *normal = G_SHATTER_VELOCITY_SCALE;
				CVector normalCopy(normal[0], normal[1], normal[2]);
				Shatter_Glass(15, &cornerA, &cornerB, &cornerC, &normalCopy,
						gShatterColor->r, gShatterColor->g, gShatterColor->b);
				result = 2;
			}
			if (markProcessed != 0)
				face[0] &= ~0x40;
		}
	}
	else
	{
		// quad face
		CVector cornerD;
		TransformVertex(&cornerD, vertexTable, vertexIdx, 3);

		CalcRGB(face[0] & 0x800, *(u32*)((u8*)face + 8), 4, gShatterRegionColorTable[item->mRegion * 17]);

		G_SHATTER_FACE_CENTER.vx = (((cornerA.vx >> 12) + (cornerB.vx >> 12) + (cornerC.vx >> 12) + (cornerD.vx >> 12)) / 4) << 12;
		G_SHATTER_FACE_CENTER.vy = (((cornerA.vy >> 12) + (cornerB.vy >> 12) + (cornerC.vy >> 12) + (cornerD.vy >> 12)) / 4) << 12;
		G_SHATTER_FACE_CENTER.vz = (((cornerA.vz >> 12) + (cornerB.vz >> 12) + (cornerC.vz >> 12) + (cornerD.vz >> 12)) / 4) << 12;

		flags = face[0];
		if ((flags & 0x40) == 0 && (flags & 1) != 0)
		{
			u32 shardColor = (flags & 0x20) ? uvRecord->mPackedColor : 0;
			if (LowMemory == 0)
			{
				Split(&cornerA, &cornerB, &cornerC,
						uvRecord->u0, uvRecord->v0,
						uvRecord->u1, uvRecord->v1,
						uvRecord->u2, uvRecord->v2,
						shardColor, splitDepth);
				Split(&cornerB, &cornerC, &cornerD,
						uvRecord->u1, uvRecord->v1,
						uvRecord->u2, uvRecord->v2,
						uvRecord->u3, uvRecord->v3,
						shardColor, splitDepth);
			}
			result = 1;
		}
		else
		{
			if (LowMemory == 0)
			{
				i16 *normal = G_SHATTER_VELOCITY_SCALE;
				CVector normalCopy(normal[0], normal[1], normal[2]);
				Shatter_Glass(30, &cornerA, &cornerB, &cornerC, &normalCopy,
						gShatterColor->r, gShatterColor->g, gShatterColor->b);
				result = 2;
			}
			if (markProcessed != 0)
				face[0] &= ~0x40;
		}
	}

	if (doGlowFlash != 0 && LowMemory == 0)
	{
		Exp_GlowFlash(&G_SHATTER_FACE_CENTER, 100,
				gShatterColor->r, gShatterColor->g, gShatterColor->b, 4, 1, 100);
	}

	return result;
}

// @Ok
// 0x6A7658: last glass shatter position (for sound distance comparison).
static CVector gLastGlassShatterPos;

// @Ok
i32 Shatter_Glass(i32 count, CVector const *pA, CVector const *pB, CVector const *pC, CVector const *pNormal, u8 r, u8 g, u8 b)
{
    CVector center = (*pB + *pC) >> 1;

    SLineInfo lineInfo;
    memset(&lineInfo, 0, sizeof(lineInfo));

    i32 startX = center.vx + 500 * pNormal->vx;
    i32 startY = center.vy + 500 * pNormal->vy;
    i32 startZ = center.vz + 500 * pNormal->vz;

    lineInfo.StartCoords.vx = startX;
    lineInfo.StartCoords.vy = startY;
    lineInfo.StartCoords.vz = startZ;
    lineInfo.EndCoords.vx = startX;
    lineInfo.EndCoords.vy = startY;
    lineInfo.EndCoords.vz = startZ;

    M3dColij_InitLineInfo(&lineInfo);
    M3dZone_LineToItem(&lineInfo, 1);

    i32 groundY = 0x7FFFFFFF;
    if (lineInfo.pItem != 0)
        groundY = lineInfo.Position.vy;

    CVector dir1 = (*pC - *pA) >> 12;
    CVector dir2 = (*pB - *pA) >> 12;

    for (i32 i = 0; i < count; i++)
    {
        i32 rand1 = Rnd(4096);
        i32 rand2 = Rnd(4096);

        CVector offset1 = dir2 * rand2;
        CVector offset2 = dir1 * rand1;
        CVector pos = offset1 + offset2;
        CVector posNorm = pos >> 12;
        CVector posScaled = posNorm << 12;
        CVector posFinal = posScaled + *pA;
        CVector vel = posFinal - center;
        CVector velNorm = vel >> 12;
        VectorNormal((VECTOR*)&velNorm, (VECTOR*)&velNorm);

        i32 velRand1 = Rnd(40);
        i32 velRand2 = Rnd(40);

        CVector velOffset1 = *pNormal * velRand1;
        CVector velOffset2 = velNorm * velRand2;
        CVector velFinal = velOffset1 + velOffset2;

        new CGlassBit(&posFinal, &velFinal, groundY, r, g, b, 100, 100, 100);
    }

    if (gGlassShatterSound != 0)
    {
        CVector camPos;
        camPos.vx = G_MIKE_CAMERA[0].Position.vx;
        camPos.vy = G_MIKE_CAMERA[0].Position.vy;
        camPos.vz = G_MIKE_CAMERA[0].Position.vz;
        camPos <<= 12;
        i32 dist1 = spidey::math::approximateDistance(camPos, gLastGlassShatterPos);
        i32 dist2 = spidey::math::approximateDistance(camPos, center);
        if (dist2 < dist1)
        {
            gLastGlassShatterPos = center;
            return center.vz;
        }
    }
    else
    {
        gLastGlassShatterPos = center;
        gGlassShatterSound = 1;
        return center.vx;
    }

    return 0;
}

// @Ok
// 2026-08-31: retagged from @NotOk now that Shatter_Face (above) is a real function instead of
// a printf stub. gShatterRegionModelTable/SShatterModelInfo moved above Shatter_Face this
// session (it needs them too); see their comments there.
//
// Source itself is unchanged from the prior @NotOk attempt (the old note below explained why
// it was unverifiable, not why it was wrong): independently re-verified this session by a
// fresh IDA Hex-Rays decompile of 0x48CFF0 (212 bytes, tools/functions/4771824.bin). Confirms,
// field-for-field: the region/model lookup (identical idiom to Shatter_Face's), the face-list
// base (info->field_2 + info->field_4) * 8 + 0x1C, mNumFaces as the trip count, the per-face
// Shatter_Face(item, face, (Rnd(5)==0)?1:0, 0, checkState, markProcessed, chanceFlag) call
// (chanceFlag = numShattered<6), the anyShattered/numShattered bookkeeping (numShattered only
// increments when Shatter_Face returns exactly 1), the `face += *face >> 18` advance (matches
// the raw disasm's `v6 += 2 * (*(DWORD*)v6 >> 18)` on a __int16* view -- same byte advance,
// just a different element width), and the final `item->mFlags |= 1` gated on
// `markProcessed != 0 && anyShattered`.
//
// --- superseded note, kept for history ---
// residue: register allocation and field read order still differ a lot from the original
// (80 mnemonic diffs from the top). Logic derived by hand-tracing the disasm (region/model
// lookup table confirmed only by address+stride, not by any name source; Rnd and
// Shatter_Face's parameter count/order confirmed against tools/names.json and shatter.h).
// See shatter.attempts.md.
// 2026-08-27: confirmed by build evidence why this could not be retagged @Ok even under this
// session's functional-only bar. Shatter_Face was still a printf stub in this same TU that
// always returned the literal constant 0x12082024; since Shatter_Item tests the return value
// against 0 and 1 in the same TU, MSVC6 constant-folded those comparisons away entirely
// (0x12082024 != 0 is always true, == 1 is always false), which deleted and reshaped
// Shatter_Item's actual loop/branch logic in the rebuilt DLL. Checked directly: the rebuilt
// Shatter_Item (0x465C30 in the local build) was 49 instructions of completely different shape
// (no call to the Shatter_Face export at all, calls into unrelated printf-stub code instead)
// versus the original's 78 instructions that do call Shatter_Face. This is the leaf-first rule
// from CLAUDE.md/AGENT_BRIEF.md in concrete effect. Resolved now that Shatter_Face is real.
i32 Shatter_Item(CItem *item, i32 a2, i32 a3)
{
	u8 region = item->mRegion;
	u16 model = item->mModel;
	void **regionModels = (void**)gShatterRegionModelTable[region * 17];
	SShatterModelInfo *info = (SShatterModelInfo*)regionModels[model];
	u32 *face = (u32*)((char*)info + (info->field_2 + info->field_4) * 8 + 0x1C);
	i32 count = info->mNumFaces;
	i32 numShattered = 0;
	i32 anyShattered = 0;

	if (count > 0)
	{
		do
		{
			u8 chanceFlag = (numShattered < 6) ? 1 : 0;
			i32 result = Shatter_Face(item, face, (Rnd(5) == 0) ? 1 : 0, 0, a2, a3, chanceFlag);
			if (result != 0)
			{
				anyShattered = 1;
				if (result == 1)
					numShattered++;
			}
			face = face + (*face >> 18);
			count--;
		} while (count != 0);
	}

	if (a3 != 0 && anyShattered != 0)
		item->mFlags |= 1;

	return anyShattered;
}

// @Ok
// 2026-08-31: session bar is functional decomp, not byte match (see task instructions).
// Implemented via IDA Hex-Rays decompile + raw disasm of 0x48C730 (1920 bytes,
// tools/functions/4769584.bin). Supersedes the previous @BIGTODO note below (kept for
// history/evidence trail).
//
// Full trace, 2026-08-31: Split recurses on the longest of its 3 input corners' 3 edges,
// picking a random split point along that edge (Rnd(9), fraction = rnd/8), and calls itself
// twice with the split point substituted for one of the two corners on the chosen edge (the
// corner NOT on that edge is the "apex" shared by both sub-triangles, confirmed always the
// first argument of both recursive calls in the disasm). a4..a9 are 3 (scalar,scalar) pairs,
// one per corner in argument order (a1:a4,a5 / a2:a6,a7 / a3:a8,a9); whenever a corner is
// replaced by the split point in a recursive call, its pair is replaced by the same Rnd(9)/8
// interpolation applied to the two source corners' pairs. This all uses vector.h's already-@Ok
// CVector operators (operator-, operator+, operator*, operator>>, Length) -- the six
// "unnamed CVector helpers" the old note below worried about are exactly those operators,
// just unresolved by naive decompilation without type info; no new helpers were needed.
//
// When a11 (depth) reaches 0, the base case constructs a real **CShatterBit** (bit.h, newly
// declared this session, see its class comment there for the full field-mapping evidence):
// averages the 3 corners (converted from 12-bit fixed point via >>12) into a center point,
// computes each corner's delta from that center, adds a small random Z-only "jaggedness" jitter
// to each delta (scaled by G_SHATTER_VELOCITY_SCALE[2], shatter.h), constructs the CShatterBit
// (whose own ctor forwards the 3 deltas + center into CChunkBit's already-@Ok ctor), sets its
// color via CChunkBit::SetRGB from gShatterColor (set earlier by CalcRGB, see its own comment
// above), sets its UVs via CChunkBit::SetUVs forwarding a4-a9 plus two module globals written by
// Shatter_Face (not yet implemented, forwarded as-is, see shatter.h), and finally stores a10
// directly into the new object's mShardColor field. Confirmed by raw disasm that this last write
// happens AFTER construction returns and is NOT done by the constructor itself (the old note
// below mislabeled this field as "splitDepth"; it is actually Split's own a10 parameter, the
// u32 one, most likely a packed face/glass color forwarded from Shatter_Face).
// CChunkBit::SetRGB/SetUVs are real, already-implemented functions in this repo (bit.cpp);
// CChunkBit::CalculateWorldCoords (called by CShatterBit's own ctor and Move, not directly from
// here) is still a printf stub, see bit.cpp's CChunkBit_CalculateWorldCoords for why.
//
// Whole function gated by mem.h's LowMemory flag (dword_60D224 in the original disasm,
// confirmed to be the same global Shatter_Face's own notes already call gLowMemory / 0x60D224
// from the maintainer's IDB) -- if set, Split does nothing at all.
//
// --- superseded note, kept for history ---
// Investigated 2026-08-27 (byte-match blockers) and 2026-08-31 (deep dive via IDA Hex-Rays
// decompile of 0x48C730, 1920 bytes, tools/functions/4769584.bin), still not decompiled.
// [...] CShatterBit is a real, distinct class, NOT the existing CChunkBit (which is only
// 0x94/148 bytes per bit.cpp's VALIDATE_SIZE; the operator new call here allocates 216/0xD8
// bytes). Defining this whole class was judged a substantial separate task, out of scope for a
// shatter.cpp-only session. Both blockers (the CShatterBit class, and the "6 unnamed CVector
// helpers") are resolved above.
void Split(
		CVector const *a1,
		CVector const *a2,
		CVector const *a3,
		i32 a4, i32 a5, i32 a6, i32 a7, i32 a8, i32 a9,
		u32 a10,
		i32 depth)
{
	if (LowMemory != 0)
		return;

	if (depth != 0)
	{
		i32 lenBC = (*a2 - *a3).Length();
		i32 longestEdge = 0; // 0 = BC (a2,a3), 1 = AC (a1,a3), 2 = AB (a1,a2)
		i32 maxLen = lenBC;

		i32 lenAC = (*a1 - *a3).Length();
		if (lenAC > maxLen)
		{
			maxLen = lenAC;
			longestEdge = 1;
		}

		i32 lenAB = (*a1 - *a2).Length();
		if (lenAB > maxLen)
		{
			longestEdge = 2;
		}

		i32 frac = Rnd(9);

		if (longestEdge == 1)
		{
			CVector edgeDelta = *a3 - *a1;
			CVector scaledDelta = edgeDelta * frac;
			CVector shiftedDelta = scaledDelta >> 3;
			CVector splitPt = *a1 + shiftedDelta;

			i32 uvA = a4 + ((frac * (a8 - a4)) >> 3);
			i32 uvB = a5 + ((frac * (a9 - a5)) >> 3);

			Split(a2, a1, &splitPt, a6, a7, a4, a5, uvA, uvB, a10, depth - 1);
			Split(a2, a3, &splitPt, a6, a7, a8, a9, uvA, uvB, a10, depth - 1);
		}
		else if (longestEdge == 2)
		{
			CVector edgeDelta = *a2 - *a1;
			CVector scaledDelta = edgeDelta * frac;
			CVector shiftedDelta = scaledDelta >> 3;
			CVector splitPt = *a1 + shiftedDelta;

			i32 uvA = a4 + ((frac * (a6 - a4)) >> 3);
			i32 uvB = a5 + ((frac * (a7 - a5)) >> 3);

			Split(a3, a1, &splitPt, a8, a9, a4, a5, uvA, uvB, a10, depth - 1);
			Split(a3, a2, &splitPt, a8, a9, a6, a7, uvA, uvB, a10, depth - 1);
		}
		else
		{
			CVector edgeDelta = *a3 - *a2;
			CVector scaledDelta = edgeDelta * frac;
			CVector shiftedDelta = scaledDelta >> 3;
			CVector splitPt = *a2 + shiftedDelta;

			i32 uvA = a6 + ((frac * (a8 - a6)) >> 3);
			i32 uvB = a7 + ((frac * (a9 - a7)) >> 3);

			Split(a1, a2, &splitPt, a4, a5, a6, a7, uvA, uvB, a10, depth - 1);
			Split(a1, a3, &splitPt, a4, a5, a8, a9, uvA, uvB, a10, depth - 1);
		}

		return;
	}

	CVector cornerA = *a1 >> 12;
	CVector cornerB = *a2 >> 12;
	CVector cornerC = *a3 >> 12;

	CVector center;
	center.vx = (cornerA.vx + cornerB.vx + cornerC.vx) / 3;
	center.vy = (cornerA.vy + cornerB.vy + cornerC.vy) / 3;
	center.vz = (cornerA.vz + cornerB.vz + cornerC.vz) / 3;

	CSVector deltaA((i16)(cornerA.vx - center.vx), (i16)(cornerA.vy - center.vy), (i16)(cornerA.vz - center.vz));
	CSVector deltaB((i16)(cornerB.vx - center.vx), (i16)(cornerB.vy - center.vy), (i16)(cornerB.vz - center.vz));
	CSVector deltaC((i16)(cornerC.vx - center.vx), (i16)(cornerC.vy - center.vy), (i16)(cornerC.vz - center.vz));

	i16 jitterScale = G_SHATTER_VELOCITY_SCALE[2];

	CSVector jitterA(0, 0, (i16)((Rnd(100) * jitterScale) >> 12));
	deltaA += jitterA;
	CSVector jitterB(0, 0, (i16)((Rnd(100) * jitterScale) >> 12));
	deltaB += jitterB;
	CSVector jitterC(0, 0, (i16)((Rnd(100) * jitterScale) >> 12));
	deltaC += jitterC;

	CShatterBit *bit = new CShatterBit(deltaA, deltaB, deltaC, center, 0);

	// SetRGB/SetUVs are non-virtual on CChunkBit (not in its vtable), so this is a normal
	// statically-resolved call, matching the original's "not through a vtable" call shape
	// trivially. Both are currently @MEDIUMTODO printf stubs in bit.cpp (see their own
	// comments): they write into CChunkBit's own undeclared 0x94-0xC8 field range, out of
	// scope for this CShatterBit-focused session.
	bit->SetRGB(gShatterColor->r, gShatterColor->g, gShatterColor->b);
	bit->SetUVs(G_SHATTER_UV_TEX_ID, G_SHATTER_UV_UNUSED,
			(u8)a4, (u8)a5, (u8)a6, (u8)a7, (u8)a8, (u8)a9);

	bit->mShardColor = a10;
}

// @Ok
// 2026-08-31: session bar is functional decomp (see task instructions), so this is now
// implemented, but it is unverifiable against original bytes and currently has no real
// caller (Shatter_Face/Split, its only plausible callers, are still stubs, see their notes).
// No standalone address exists for this function: checked tools/names.json and the whole
// address range around the other shatter.cpp functions (between CShatterBit_CShatterBit
// 0x48BDC0 and Shatter_MaybeMakeGlassShatterSound 0x48D5B0), nothing unaccounted for; grep
// across the repo shows only shatter.cpp/shatter.h reference TransformVertex. It is fully
// inlined at every call site in the original binary (same-TU MSVC6 inlining, see CLAUDE.md).
// Body confirmed by hand-tracing the 3 identical inlined blocks inside Shatter_Face's original
// disassembly at 0x48C0D0 (see Shatter_Face's comment): each one does gte_ldv0 on an SVECTOR
// looked up by a per-face byte vertex index, gte_rtv0tr, then gte_stlvnl into a VECTOR-shaped
// output, exactly the pattern below. Caller must set up the rotation matrix
// (M3dMaths_RotMatrixYXZ + gte_SetRotMatrix) and translation (M3dAsm_SetTransVector) first,
// same as every other gte_ldv0/gte_rtv0tr/gte_stlvnl call site in this codebase (camera.cpp,
// baddy.cpp, blackcat.cpp use the identical 3-call sequence). thps2-stuff/decls.h confirms the
// PSX-era parameter names (CVector *a, SVECTOR *pVertices, u8 *pVertexNums, int Vertex) but its
// body is stripped there (declaration only), so it adds no logic beyond the above.
void TransformVertex(CVector *a, SVECTOR *pVertices, u8 *pVertexNums, i32 Vertex)
{
	gte_ldv0(&pVertices[pVertexNums[Vertex]]);
	gte_rtv0tr();
	gte_stlvnl((VECTOR*)a);
}
