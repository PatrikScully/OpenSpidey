#include "engine/rendering/m3dinit.h"
#include "engine/world/collision.h"
#include "compatibility/validate.h"
#ifdef SPIDEY_STANDALONE
i32 gDbgColij;   // set by CrawlFrameTrace while tracing the ground ray
#endif
#include "engine/world/ob.h"
#include "engine/world/trig.h"
#include "engine/assets/spool.h"

#include "engine/rendering/ps2funcs.h"

// @Ok
#ifndef SPIDEY_STANDALONE
u16	Inquiry=0xFFFF;
#else
extern u16 Inquiry;
#endif
// M3dColij_InitLineInfo (0x4524C0) does "inc word [54D014h]" then
// "mov word [54D014h],1" on wrap, and hands the value out as pInfo->Inquiry
// ("mov ax,[54D014h]; mov [esi+8Ah],ax"). Not in idb_globals.txt, but no named
// object covers 0x0054D014 either. Only this file uses it. The exe keeps
// bumping its own counter while parts of this file are hooked, so the token has
// to come from the exe's copy or hooked and unhooked queries hand out clashing
// tokens for the same frame.
//#define G_INQUIRY (Inquiry)
#define G_INQUIRY (*reinterpret_cast<u16*>(0x0054D014))

// @Ok
#ifndef SPIDEY_STANDALONE
SLineInfo gLineInfo;
#else
extern SLineInfo gLineInfo;
#endif

#ifndef SPIDEY_STANDALONE
i32 LineOfSightCheck;
#else
extern i32 LineOfSightCheck;
#endif

// address 0x005FBD38 in the original, right before M3dColij_OneMask (0x005FBDA8).
// M3dColij_LineToSphere resets this to 0x7FFFFFFF ("nothing found yet") at the top of
// every call, then keeps the best (lowest) line-parameter-t found while scanning items.
// No idb_globals.txt entry, name is our guess.
// Proved by M3dColij_LineToSphere at 0x452C9E: "mov dword [5FBD38h],7FFFFFFFh".
// NOTE: this is the same address as gLineColijLastT below (see the comment there);
// two names, one global.
#ifndef SPIDEY_STANDALONE
i32 gLineToSphereBestT;
#else
extern i32 gLineToSphereBestT;
#endif
//#define G_LINE_TO_SPHERE_BEST_T (gLineToSphereBestT)
#define G_LINE_TO_SPHERE_BEST_T (*reinterpret_cast<i32*>(0x005FBD38))

#ifndef SPIDEY_STANDALONE
u32 M3dColij_OneMask;
#else
extern u32 M3dColij_OneMask;
#endif
#ifndef SPIDEY_STANDALONE
u32 M3dColij_ZeroMask;
#else
extern u32 M3dColij_ZeroMask;
#endif

// address 0x005FBDE0 in the original, right after M3dColij_ZeroMask (0x005FBDDC ends
// here). M3dColij_LineToSphere resets this to 0 at the top of every call, then keeps the
// CBody* that currently owns gLineToSphereBestT; it is also the function's return value.
// No idb_globals.txt entry, name is our guess.
// Proved by M3dColij_LineToSphere at 0x452C94: "mov dword [5FBDE0h],0", and at
// 0x452FD0 "mov [5FBDE0h],edi" for the keep-this-body case.
#ifndef SPIDEY_STANDALONE
CBody * gLineToSphereBestBody;
#else
extern CBody * gLineToSphereBestBody;
#endif
//#define G_LINE_TO_SPHERE_BEST_BODY (gLineToSphereBestBody)
#define G_LINE_TO_SPHERE_BEST_BODY (*reinterpret_cast<CBody**>(0x005FBDE0))

#ifndef SPIDEY_STANDALONE
i32 BaddyCollisionCheck;
#else
extern i32 BaddyCollisionCheck;
#endif
#ifndef SPIDEY_STANDALONE
i32 CameraCollisionCheck;
#else
extern i32 CameraCollisionCheck;
#endif
#ifndef SPIDEY_STANDALONE
i32 TriggerCollisionCheck;
#else
extern i32 TriggerCollisionCheck;
#endif

// address 0x005FBEF0 in the original, right after TriggerCollisionCheck (0x005FBEE8 ends
// at 0x005FBEEC, small gap). Same family as the CollisionCheck flags above (debug/editor
// visualization toggles); M3dColij_LineToSphere skips its actual radius-distance check
// when this is set, accepting any item whose bounding box overlapped the line. No
// idb_globals.txt entry, name is our guess.
// Proved by M3dColij_LineToSphere at 0x452EBE/0x452F36/0x452FA7: "mov eax,[5FBEF0h]".
#ifndef SPIDEY_STANDALONE
i32 gLineToSphereIgnoreRadius;
#else
extern i32 gLineToSphereIgnoreRadius;
#endif
//#define G_LINE_TO_SPHERE_IGNORE_RADIUS (gLineToSphereIgnoreRadius)
#define G_LINE_TO_SPHERE_IGNORE_RADIUS (*reinterpret_cast<i32*>(0x005FBEF0))

// @Ok
// Real object at 0x005564E4 in the PC binary (name confirmed by the maintainer's IDB:
// idbs/idb_globals.txt "0x005564E4 gUnkPose"), pushed as the argument of every
// CSuper::ApplyPose(gUnkPose) call site (14 data xrefs, e.g. SpideyAI_WaitForSimbyGrab
// 0x4A8737 "push offset unk_5564E4"). It was a 1 entry placeholder before; the real size and
// contents are read straight out of the exe here.
//
// Layout comes from CSuper::ApplyPose (0x460E80) -> sub_453C50, which reads the u16 at +2 as a
// joint count, treats the bytes from +4 on as that many 12 byte (6 x i16) joint records, and
// resolves the records in place: word0 is a joint id, word1 is the parent joint's id, and word5
// gets the parent's INDEX (or -1 when word1 matches no record's word0, which is the root case).
// Words 2..4 are zero in the file. Count is 12, so the object is 4 + 12*12 = 148 bytes = 74 i16,
// and the next global starts right after it at 0x00556578. Values below are the exe's bytes
// verbatim. Not const: sub_453C50 writes word5 of every record back into this array.
//
// G_* WATCH (verified 2026-09-01): this is a WRITTEN global reached through an already-hooked
// function (CSuper::ApplyPose, patched in ob.cpp). It is safe as a plain repo array ONLY because
// none of its callers are hooked yet, so the hooked ApplyPose only ever receives the game's
// pointer 0x005564E4 and never this copy. There is no patch_blackcat / patch_simby /
// patch_spclone today. The moment any function containing an ApplyPose(gUnkPose) call site gets
// hooked (blackcat.cpp:269, simby.cpp:52 and :85, spclone.cpp:212 and :246), this array must
// become a G_* macro on 0x005564E4, or the game's copy and ours will hold separately resolved
// parent indices. Same bug class as G_SPOOL_LOG_FAILED_TEXTURE_ACCESS in spool.cpp.
#ifndef SPIDEY_STANDALONE
i16 gUnkPose[74] = {
	0, 12,

	 0, -1, 0, 0, 0, 0,
	 2,  0, 0, 0, 0, 0,
	 1,  2, 0, 0, 0, 0,
	 7,  1, 0, 0, 0, 0,
	 4,  1, 0, 0, 0, 0,
	 3,  4, 0, 0, 0, 0,
	 6,  3, 0, 0, 0, 0,
	 5,  3, 0, 0, 0, 0,
	 9,  1, 0, 0, 0, 0,
	 8,  9, 0, 0, 0, 0,
	11,  8, 0, 0, 0, 0,
	10,  8, 0, 0, 0, 0
};
#endif

// guess: scratch rotation matrix, sits right before gLineInfo in the binary (0x5FBE18,
// gLineInfo is 0x5FBE38, exactly sizeof(MATRIX)=0x20 apart) but is not part of the
// SLineInfo struct itself (SLineInfo::WorldCst already validated at its own offset 0x48).
// No idb_globals.txt entry for this address, name and boundary are our guess.
static MATRIX * const gLineColijRotMatrix = (MATRIX*)0x005FBE18;

// guess: pointer to the current "eye" vector used for the line-of-sight GTE view setup.
// No idb_globals.txt entry, name is our guess.
#define G_CURRENT_COLIJ_VECTOR (*reinterpret_cast<SVECTOR**>(0x005FBD20))

// @Ok
// @Matching
// Return type fixed from void to i32 (the original returns 0 if pInfo->pItem is null,
// 1 otherwise).
i32 M3dColij_GetLineInfo(SLineInfo *pInfo)
{
	if (pInfo->pItem)
	{
		M3dMaths_RotMatrixYXZ((SVECTOR*)&pInfo->pItem->mAngles, gLineColijRotMatrix);
		gte_SetRotMatrix(gLineColijRotMatrix);
		gte_ldv0(G_CURRENT_COLIJ_VECTOR);
		gte_rtv0();
		gte_stsv((SVECTOR*)&pInfo->Normal);
		return 1;
	}

	return 0;
}

// @Ok
// @Matching
void M3dColij_LineInfoFixup(SLineInfo *pInfo)
{
	i32 v2 = M3dMaths_MulDiv64(pInfo->Distance, 0x4000, pInfo->Length);
	i32 v3 = pInfo->EndCoords.vx - pInfo->StartCoords.vx;
	i32 v5 = pInfo->EndCoords.vy - pInfo->StartCoords.vy;
	i32 v4 = pInfo->EndCoords.vz - pInfo->StartCoords.vz;

	i32 v8 = (v3 < 0 ? -1 : 1) * M3dMaths_MulDiv64(my_abs(v3), v2, 0x4000);
	i32 v6 = (v5 < 0 ? -1 : 1) * M3dMaths_MulDiv64(my_abs(v5), v2, 0x4000);
	i32 v7 = (v4 < 0 ? -1 : 1) * M3dMaths_MulDiv64(my_abs(v4), v2, 0x4000);

	pInfo->Position.vx = v8 + pInfo->StartCoords.vx;
	pInfo->Position.vy = v6 + pInfo->StartCoords.vy;
	pInfo->Position.vz = v7 + pInfo->StartCoords.vz;
}

// @Ok
// @Leak
// @Matching
void M3dColij_LineToItemZoned(CItem **ppItem,SLineInfo *pInfo)
{
	if	(!ppItem) return;

	if (pInfo->Length==0)
		return;

	gte_SetRotMatrix(&pInfo->WorldCst);

	M3dAsm_LineColijPreprocessItemsZoned(ppItem,0,pInfo,pInfo->Inquiry);

	for (	; *ppItem; ppItem++)
		if	((*ppItem)->mInquiry != pInfo->Inquiry)
		{
			(*ppItem)->mInquiry =	pInfo->Inquiry;


			M3dColij_LineToThisItem(*ppItem, pInfo);
		}
}

// @Ok
// @Leak
 void NextInquiry(void)
{
	// increment inquiry
	// if it's 0, set to 1 and set all objects' inquiry fields to 0.
	// If this weren't done, a rare bug may occur where the previous collision test with a particular object was
	// performed 65536 tests ago, and the current test would automatically fail.
	if	(!++G_INQUIRY)
	{
		CItem	*pItem;
		G_INQUIRY=1;
		for (pItem=G_ENVIRO_LIST; pItem; pItem=pItem->mNextItem)
			pItem->mInquiry=0;
		for (pItem=G_ENVIRONMENTAL_OBJECT_LIST; pItem; pItem=pItem->mNextItem)
			pItem->mInquiry=0;
	}
}


// @Ok
// Sets up a fresh SLineInfo for a line-vs-world collision query: resets the
// nearest-hit trackers, clears the hit result fields, builds the line's
// bounding box, builds the combined rotation matrix M3dAsm_LineColijPreprocessItems(Zoned)
// uses to transform vertices into line-aligned space, and hands out a fresh
// Inquiry token.
//
// The original builds the sin/cos-like terms via a leading-zero-count
// normalize (gte_ldlzc/gte_stlzc), a table-precision M3dMaths_SquareRoot0
// call on the normalized value, then a shift-back (PS2 hardware sqrt needs a
// normalized operand for full precision). Our M3dMaths_SquareRoot0 already
// does a real double sqrt with full precision (sqrt(x << 2k) == sqrt(x) << k
// for the even shift counts used there, so the normalize/shift-back cancels
// out algebraically), so we can call it directly on the plain squared
// distance and get the same result.
void M3dColij_InitLineInfo(SLineInfo *pInfo)
{
	pInfo->Distance = 0x7FFFFFFF;
	pInfo->tNear = 0x7FFFFFFF;

	pInfo->pItem = NULL;
	pInfo->pFace = NULL;
	pInfo->Model = -1;

	pInfo->RecordTriggerZoneHits = 0;
	pInfo->DropDown = 0;

	i32 dx = pInfo->EndCoords.vx - pInfo->StartCoords.vx;
	i32 dy = pInfo->EndCoords.vy - pInfo->StartCoords.vy;
	i32 dz = pInfo->EndCoords.vz - pInfo->StartCoords.vz;

	i16 sdx = (i16)(dx >> 12);
	i16 sdz = (i16)(dz >> 12);
	i16 sdy = (i16)(dy >> 12);

	i32 horiz2 = sdz * sdz + sdx * sdx;

	if (horiz2 != 0)
	{
		i32 horizMag = M3dMaths_SquareRoot0(horiz2);

		i16 sinYaw = (i16)((i32)sdx * 4096 / horizMag);
		i16 cosYaw = (i16)((i32)sdz * 4096 / horizMag);

		gLineColijRotMatrix->m[0][0] = cosYaw;
		gLineColijRotMatrix->m[0][1] = 0;
		gLineColijRotMatrix->m[0][2] = -sinYaw;
		gLineColijRotMatrix->m[1][0] = 0;
		gLineColijRotMatrix->m[1][1] = 4096;
		gLineColijRotMatrix->m[1][2] = 0;
		gLineColijRotMatrix->m[2][0] = sinYaw;
		gLineColijRotMatrix->m[2][1] = 0;
		gLineColijRotMatrix->m[2][2] = cosYaw;

		i32 full2 = horiz2 + sdy * sdy;
		i32 fullMag = M3dMaths_SquareRoot0(full2);

		pInfo->Length = fullMag;

		i16 sinPitch = (i16)((i32)sdy * 4096 / fullMag);
		i16 cosPitch = (i16)(horizMag * 4096 / fullMag);

		pInfo->WorldCst.m[0][0] = 4096;
		pInfo->WorldCst.m[0][1] = 0;
		pInfo->WorldCst.m[0][2] = 0;
		pInfo->WorldCst.m[1][0] = 0;
		pInfo->WorldCst.m[1][1] = cosPitch;
		pInfo->WorldCst.m[1][2] = -sinPitch;
		pInfo->WorldCst.m[2][0] = 0;
		pInfo->WorldCst.m[2][1] = sinPitch;
		pInfo->WorldCst.m[2][2] = cosPitch;

		MulMatrix(&pInfo->WorldCst, gLineColijRotMatrix);
	}
	else
	{
		i16 sign;

		if (sdy >= 0)
		{
			sign = 4096;
			pInfo->Length = sdy;
			pInfo->DropDown = 1;
		}
		else
		{
			sign = -4096;
			pInfo->Length = -sdy;
		}

		pInfo->WorldCst.m[0][0] = 4096;
		pInfo->WorldCst.m[0][1] = 0;
		pInfo->WorldCst.m[0][2] = 0;
		pInfo->WorldCst.m[1][0] = 0;
		pInfo->WorldCst.m[1][1] = 0;
		pInfo->WorldCst.m[1][2] = -sign;
		pInfo->WorldCst.m[2][0] = 0;
		pInfo->WorldCst.m[2][1] = sign;
		pInfo->WorldCst.m[2][2] = 0;
	}

	gte_SetRotMatrix(&pInfo->WorldCst);

	pInfo->MinCoords.vx = pInfo->StartCoords.vx < pInfo->EndCoords.vx ? pInfo->StartCoords.vx : pInfo->EndCoords.vx;
	pInfo->MaxCoords.vx = pInfo->StartCoords.vx < pInfo->EndCoords.vx ? pInfo->EndCoords.vx : pInfo->StartCoords.vx;

	pInfo->MinCoords.vy = pInfo->StartCoords.vy < pInfo->EndCoords.vy ? pInfo->StartCoords.vy : pInfo->EndCoords.vy;
	pInfo->MaxCoords.vy = pInfo->StartCoords.vy < pInfo->EndCoords.vy ? pInfo->EndCoords.vy : pInfo->StartCoords.vy;

	pInfo->MinCoords.vz = pInfo->StartCoords.vz < pInfo->EndCoords.vz ? pInfo->StartCoords.vz : pInfo->EndCoords.vz;
	pInfo->MaxCoords.vz = pInfo->StartCoords.vz < pInfo->EndCoords.vz ? pInfo->EndCoords.vz : pInfo->StartCoords.vz;

	NextInquiry();
	pInfo->Inquiry = G_INQUIRY;
}

// @Ok
// Original at 0x452C30, 1047 bytes. Re-checked 2026-08-31: every callee that blocked this
// (see the previous BIGTODO note, kept below the divider for the record) is now decompiled:
// CVector::operator- (0x4E7760), operator>> (0x4E7840), operator<< (0x4E7870), operator/
// (0x4E7800), operator>>= (0x4E7680), operator+= (0x4E7590) all got moved out of the
// wrongly-INLINE vector.h into vector.cpp already; the GTE helper cluster (gte_sqr0,
// gte_stlvnl, gte_op12, gte_ldopv1, gte_ldopv2, gte_ldlv0, gte_ldlvl, gte_stlvnl0, gte_lddp,
// M3dMaths_SquareRoot0) all exist in ps2funcs.cpp; the only 3 genuinely missing leaves
// (sub_46D930, sub_46DEB0, sub_46E010) are confirmed via IDA xrefs to be used ONLY by this
// function, so they are decompiled alongside it as gsub_46D930/gsub_46DEB0/gte_gpf in
// ps2funcs.cpp (see the comments there).
//
// Behavior (matches the disassembly move for move): normalizes (pEnd-pStart) into a
// fixed-point (Q12) direction, stashes it as the "row 0" of the GTE scratch matrix used by
// gsub_46DEB0, then walks the pFirst item chain. Per item: skip if mCBodyFlags bit 0x40 is
// set or the item is pExclude; reject on an axis-aligned bounding-box test against the
// line's start/end extents inflated by the item's (scaled) radius; then, unless
// gLineToSphereIgnoreRadius is set, reject unless the line passes within the item's radius
// (via the cross-product perpendicular-distance-squared test the original does with
// gte_op12/gte_sqr0). For a surviving item, project it onto the line (gsub_46DEB0) to get a
// line parameter t; if t is closer than the current best, accept it outright when
// 0 <= t <= line length, or fall back to a plain point-radius test against the segment's
// start (t<0) or end (t>length) point. The nearest accepted item becomes the new best.
// After the scan, if anything was found, *pOutPos is set to pStart + (direction * bestT),
// using the same double->12 shift the original does (gte_gpf then CVector::operator>>=(12)).
// Returns the found CBody* (or NULL).
//
// Old blocker note (2026-08-27, resolved above): needed CVector::operator- (0x4E7760) and
// operator>> (0x4E7840) out-of-line (CLAUDE.md's wrongly-INLINE vector.h issue), plus an
// unnamed GTE-area helper cluster (sub_46DD00, sub_46D790, sub_46D930, sub_46D700,
// sub_46DEB0, sub_46DF60, sub_46D990, sub_46E010).
CBody * M3dColij_LineToSphere(CVector *pStart, CVector *pEnd, CVector *pOutPos, CBody *pFirst, CBody *pExclude, i32 radiusScale)
{
	CVector delta = *pEnd - *pStart;
	CVector deltaShifted = delta >> 12;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&deltaShifted));
	gte_sqr0();

	VECTOR deltaSq;
	gte_stlvnl(&deltaSq);

	i32 length = M3dMaths_SquareRoot0(deltaSq.vx + deltaSq.vy + deltaSq.vz);

	G_LINE_TO_SPHERE_BEST_T = 0x7FFFFFFF;
	G_LINE_TO_SPHERE_BEST_BODY = 0;

	if (length == 0)
		return 0;

	CVector dirQ12 = (deltaShifted << 12) / length;

	gte_ldopv1(reinterpret_cast<VECTOR*>(&dirQ12));

	SVECTOR dirShort;
	dirShort.vx = (i16)dirQ12.vx;
	dirShort.vy = (i16)dirQ12.vy;
	dirShort.vz = (i16)dirQ12.vz;
	gsub_46D930(&dirShort);

	i32 minX = pStart->vx < pEnd->vx ? pStart->vx : pEnd->vx;
	i32 maxX = pStart->vx < pEnd->vx ? pEnd->vx : pStart->vx;
	i32 minY = pStart->vy < pEnd->vy ? pStart->vy : pEnd->vy;
	i32 maxY = pStart->vy < pEnd->vy ? pEnd->vy : pStart->vy;
	i32 minZ = pStart->vz < pEnd->vz ? pStart->vz : pEnd->vz;
	i32 maxZ = pStart->vz < pEnd->vz ? pEnd->vz : pStart->vz;

	for (CBody *item = pFirst; item; item = (CBody*)item->mNextItem)
	{
		if (item->mCBodyFlags & 0x40)
			continue;
		if (item == pExclude)
			continue;

		i32 rad = radiusScale * item->mRMinor;

		if (item->mPos.vx + rad < minX) continue;
		if (item->mPos.vx - rad > maxX) continue;
		if (item->mPos.vy + rad < minY) continue;
		if (item->mPos.vy - rad > maxY) continue;
		if (item->mPos.vz + rad < minZ) continue;
		if (item->mPos.vz - rad > maxZ) continue;

		CVector itemFromStart;
		itemFromStart.vx = (item->mPos.vx - pStart->vx) >> 12;
		itemFromStart.vy = (item->mPos.vy - pStart->vy) >> 12;
		itemFromStart.vz = (item->mPos.vz - pStart->vz) >> 12;

		gte_ldopv2(reinterpret_cast<VECTOR*>(&itemFromStart));
		gte_op12();
		gte_sqr0();

		VECTOR crossSq;
		gte_stlvnl(&crossSq);

		i32 radShifted = rad >> 12;
		i32 radSq = radShifted * radShifted;

		if (!G_LINE_TO_SPHERE_IGNORE_RADIUS)
		{
			if (crossSq.vx + crossSq.vy + crossSq.vz >= radSq)
				continue;
		}

		gte_ldlv0(reinterpret_cast<VECTOR*>(&itemFromStart));
		gsub_46DEB0();

		i32 t;
		gte_stlvnl0(&t);

		if (t >= G_LINE_TO_SPHERE_BEST_T)
			continue;

		if (t >= 0)
		{
			if (t > length)
			{
				CVector itemFromEnd;
				itemFromEnd.vx = (item->mPos.vx - pEnd->vx) >> 12;
				itemFromEnd.vy = (item->mPos.vy - pEnd->vy) >> 12;
				itemFromEnd.vz = (item->mPos.vz - pEnd->vz) >> 12;

				gte_ldlvl(reinterpret_cast<VECTOR*>(&itemFromEnd));
				gte_sqr0();

				VECTOR endSq;
				gte_stlvnl(&endSq);

				if (!G_LINE_TO_SPHERE_IGNORE_RADIUS)
				{
					if (endSq.vx + endSq.vy + endSq.vz >= radSq)
						continue;
				}
			}
		}
		else
		{
			gte_ldlvl(reinterpret_cast<VECTOR*>(&itemFromStart));
			gte_sqr0();

			VECTOR startSq;
			gte_stlvnl(&startSq);

			if (!G_LINE_TO_SPHERE_IGNORE_RADIUS)
			{
				if (startSq.vx + startSq.vy + startSq.vz >= radSq)
					continue;
			}
		}

		G_LINE_TO_SPHERE_BEST_T = t;
		G_LINE_TO_SPHERE_BEST_BODY = item;
	}

	if (G_LINE_TO_SPHERE_BEST_BODY)
	{
		gte_ldlvl(reinterpret_cast<VECTOR*>(&dirQ12));
		gte_lddp(G_LINE_TO_SPHERE_BEST_T);
		gte_gpf();
		gte_stlvnl(reinterpret_cast<VECTOR*>(pOutPos));
		*pOutPos >>= 12;
		*pOutPos += *pStart;
	}

	return G_LINE_TO_SPHERE_BEST_BODY;
}

// ===================================================================================
// "Coarse GTE" per-item collision subsystem used only by M3dColij_LineToThisItem below.
// Fully scoped and decompiled 2026-09-01 by cross-checking IDA's Hex-Rays decompile
// against the raw disasm for every callee (Hex-Rays garbles some of this - one callee,
// see gSetCoarseTranslationVector below, is flat-out mismapped in tools/names.json).
//
// Layout confirmed via IDA: 0x00610B60 is a SECOND, distinct 3x3 i16 rotation matrix,
// separate from gRotMatrix (0x00610B20, ps2funcs.cpp). It has its own small "load
// matrix / load translation / transform point" trio mirroring gte_SetRotMatrix /
// M3dAsm_SetTransVector / FixedXForm, plus its own per-face-vertex clip test
// (ClipQuadAgainstCoarseMatrix, sub_0x0046F6B0) and per-face plane/cross-product hit
// test (TestItemFaces, sub_0x0046F1F0). All addresses below are cross-checked against
// both mcp decompile() and disasm() output; where the two matched exactly we call it
// confirmed, not guessed.
//
// The two originally-scoped "biggest blockers" turned out to touch NO unvalidated
// CItem fields at all: every CItem/SLineInfo offset TestItemFaces reads or writes
// (CItem::mModel at 0x1A, SLineInfo::Distance/Length/pItem/pFace/Model/
// RecordTriggerZoneHits) is already a named, validated field in this file. The
// "unvalidated CItem +64/+68/+104/+108/+112/+116 offsets" the previous stub worried
// about were a misattribution: those offsets are on SLineInfo (a3 in the disasm, not
// CItem), and they match SLineInfo::Distance(0x40)/Length(0x44)/pItem(0x68)/
// pFace(0x80)/Model(0x84)/RecordTriggerZoneHits(0x88) exactly. The per-model "coarse
// mesh" table (vertex/face records looked up via the already-named CItemRelatedList,
// ob.h) is a genuinely separate, pre-existing game-data format; its field offsets are
// confirmed byte-for-byte against the disasm but most individual fields' semantic
// *meaning* (beyond what is used here) is not known, so we access it via raw offsets
// rather than inventing a named struct for data we cannot fully verify.

// address 0x00610B60 in the original. Plain repo global (no other file in this repo
// touches this address, confirmed via grep), matching how ps2funcs.cpp's own
// gRotMatrix/translationVector/gGeneralLongVector are plain globals rather than
// address-bound pointers.
// Bound to the exe's address for the same reason as gCoarseTranslationVector
// below: the exe has its own copy of this coarse trio and can load the matrix
// from an unhooked path before our hooked TestItemFaces reads it.
static i16 (* const gCoarseRotMatrix)[3] = reinterpret_cast<i16(*)[3]>(0x00610B60);

// address 0x00610BB0..BB8 in the original: a translation-like vector distinct from
// translationVector (0x00610B34, ps2funcs.cpp), fed by gSetCoarseTranslationVector and
// consumed only by CoarseTransformPoint below. The original's setter also copies a 4th
// dword (0x00610BBC) that is never read back by anything we can find (a harmless
// over-read into whatever follows the caller's 3-dword vector in memory); we only keep
// the 3 components that are actually used.
// This is the same memory as ps2funcs' vertexRegister (the GTE V0 register,
// 0x00610BB0). The coarse pipeline mirrors the GTE trio and reuses its V0
// scratch, so ours has to alias it too now that vertexRegister points at the
// exe. A repo-local copy here would silently unalias the two.
static CVector * const gCoarseTranslationVector = (CVector*)0x00610BB0;

// address 0x005FBE2C in the original: a 3-dword (CVector) scratch vector sitting
// between gLineColijRotMatrix (0x5FBE18) and gLineInfo (0x5FBE38 = this + 0xC).
// M3dColij_LineToThisItem computes the item's start-relative position here
// (DropDown-aware axis handling) and round-trips it through the coarse GTE pipeline.
// No idb_globals.txt entry, name/boundary are our guess (same evidence style as
// gLineColijRotMatrix above).
static CVector * const gLineColijRelPos = (CVector*)0x005FBE2C;

// The old gLineColijExcludeMask pointer that used to live here was 0x005FBDA8, which
// is M3dColij_OneMask itself (idb_globals.txt, and M3dZone_LineToItem writes it at
// 0x4549C9). It was a second name for one global, so TestItemFaces now reads
// G_M3DCOLIJ_ONE_MASK. The writer (M3dZone_LineToItem) is still unhooked, which is
// exactly why the macro has to stay on game memory.

// addresses 0x005FBD1C/0x005FBD20/0x005FBD38 in the original. TestItemFaces's "keep as
// new nearest hit" branch writes these as a duplicate of pInfo->pFace / a pointer into
// the per-model normal table / the found hit parameter, IN ADDITION to writing
// pInfo->pFace/pInfo->Distance directly. This is a DIFFERENT reuse of the same
// addresses as two globals this file already names: 0x005FBD38 is
// gLineToSphereBestT (M3dColij_LineToSphere's own tracker) and 0x005FBD20 is the slot
// G_CURRENT_COLIJ_VECTOR reads, so those two write through the macros now. Only
// 0x005FBD1C has no other name. No reader for the three has turned up anywhere in the
// current repo; kept only for fidelity (this runs in-process against real game
// memory) in case other not-yet-decompiled code reads them.
static i32 * const gLineColijLastFacePtr = (i32*)0x005FBD1C;

// address 0x006AC20C in the original: per-environment array of u32 "trigger command
// point id" tables, indexed [envIndex][pItem->mModel] (Spool_GetEnvIndex gives
// envIndex). Consumed by TestItemFaces's "trigger zone" branch and
// M3dColij_LineToThisItem's tail (Trig_TriggerCommandPoint takes a plain u32 id, per
// trig.h, matching this table holding ids rather than pointers). No idb_globals.txt
// entry, name is our guess.
static u32 ** const gEnvTriggerCommandIds = (u32**)0x006AC20C;

// address 0x006B2F00 in the original, named gSpoolColijEnvIndex in idb_globals.txt:
// written (never read anywhere we can find, in this function or elsewhere in the
// repo) as envIndex^1 whenever a trigger-zone hit resolves. Already bound to the
// exe's address, so nothing to convert; renamed to the IDB's name. Nothing else in
// the repo touches 0x006B2F00.
static i32 * const gSpoolColijEnvIndex = (i32*)0x006B2F00;

// sub_0x0046D810 (47 bytes): copies a caller-supplied 3x3 i16 matrix into
// gCoarseRotMatrix - a gte_SetRotMatrix analogue restricted to the coarse matrix.
// @Ok
static void SetCoarseRotMatrix(const MATRIX *pSrc)
{
	const i16 *src = reinterpret_cast<const i16*>(pSrc);
	i16 *dst = &gCoarseRotMatrix[0][0];

	for (i32 i = 0; i < 9; i++)
		dst[i] = src[i];
}

// sub_0x0046D620 (31 bytes): copies 3 dwords, at byte offsets 0x14/0x18/0x1C from the
// caller-supplied pointer, into translationVector (ps2funcs.cpp's already-named GTE
// translation register). Only call site passes &gLineColijRotMatrix, whose own byte
// offsets 0x14/0x18/0x1C are exactly gLineColijRelPos (see the derivation on that
// global above), so this is really "translationVector = *gLineColijRelPos" for the one
// caller that exists; written generically (byte offset from a caller pointer) to match
// the original's signature.
// @Ok
static void SetTranslationVectorFromOffset(const u8 *pBase)
{
	const i32 *p = reinterpret_cast<const i32*>(pBase + 0x14);
	G_TRANSLATION_VECTOR.vx = p[0];
	G_TRANSLATION_VECTOR.vy = p[1];
	G_TRANSLATION_VECTOR.vz = p[2];
}

// The REAL sub_0x0046D840 (24 bytes). WARNING: tools/names.json maps this address to
// gte_ldlv0, but that is wrong - confirmed via raw disasm (not just Hex-Rays), this
// function copies a1[0..3] into gCoarseTranslationVector (0x00610BB0..BBC), it does
// NOT touch vertexRegister. Do not call the repo's existing gte_ldlv0() here; it is a
// different function that happens to share a superficially similar "load a vector"
// shape but writes somewhere else entirely.
// @Ok
static void SetCoarseTranslationVector(const CVector *pSrc)
{
	gCoarseTranslationVector->vx = pSrc->vx;
	gCoarseTranslationVector->vy = pSrc->vy;
	gCoarseTranslationVector->vz = pSrc->vz;
}

// sub_0x0046DD40 (168 bytes): dot-products gCoarseTranslationVector against each row
// of gCoarseRotMatrix (>>12) into gGeneralLongVector (ps2funcs.cpp's GTE accumulator
// register - confirmed via IDA: the real sub_0x0046D790, which tools/names.json
// correctly maps to gte_stlvnl, reads its result straight back out of the SAME
// dword_00610BA0/A4/A8 addresses gGeneralLongVector.vx/vy/vz already model). The
// original also stores an uninitialized stack dword into gGeneralLongVector.pad's
// address (0x00610BAC); we do not reproduce that (never-consumed garbage).
// @Ok
static void CoarseTransformPoint(void)
{
	i32 tx = gCoarseTranslationVector->vx;
	i32 ty = gCoarseTranslationVector->vy;
	i32 tz = gCoarseTranslationVector->vz;

	G_GENERAL_LONG_VECTOR.vx = (tx * gCoarseRotMatrix[0][0] + ty * gCoarseRotMatrix[0][1] + tz * gCoarseRotMatrix[0][2]) >> 12;
	G_GENERAL_LONG_VECTOR.vy = (tx * gCoarseRotMatrix[1][0] + ty * gCoarseRotMatrix[1][1] + tz * gCoarseRotMatrix[1][2]) >> 12;
	G_GENERAL_LONG_VECTOR.vz = (tx * gCoarseRotMatrix[2][0] + ty * gCoarseRotMatrix[2][1] + tz * gCoarseRotMatrix[2][2]) >> 12;
}

// sub_0x0046E4D0 (24 bytes): thin wrapper, confirmed via IDA to just be
// MulMatrix0(&gCoarseRotMatrix-as-MATRIX, a1, a2) (sub_0x0046CD90 is already decompiled
// in ps2funcs.cpp as MulMatrix0). Both call sites pass the same pointer for a1 and a2
// (&gLineColijRotMatrix), i.e. "gLineColijRotMatrix = gCoarseRotMatrix * gLineColijRotMatrix".
// @Ok
static void MulCoarseRotMatrix(MATRIX *a1, MATRIX *a2)
{
	MulMatrix0(reinterpret_cast<MATRIX*>(&gCoarseRotMatrix[0][0]), a1, a2);
}

// sub_0x0046F6B0 (359 bytes). For each of pFaceTable->vertCount local vertices
// (8 bytes/vertex: i16 x,y,z + 1 pad i16, at pFaceTable+0x1C): adds the (i16-truncated)
// fixedOffset, transforms by gCoarseRotMatrix, adds translationVector, computes a
// PS1-GTE-style clip outcode (bit0 z<0, bit1 y<0, bit2 x<0, bit3 z>lengthBound,
// bit9 y>0, bit10 x>0) and writes {x,y,z,outcode} into pScratchOut (same layout,
// 8 bytes/vertex). Returns the AND-reduce of every vertex's outcode (classic
// trivial-reject test: nonzero means every vertex shares an out-of-bounds side).
// pFaceTable and pScratchOut are the SAME kind of pointer at different call sites
// (pFaceTable's own header is only used here for its vertCount field at +2); pFaceTable
// is looked up via CItemRelatedList (see TestItemFaces/M3dColij_LineToThisItem below),
// pScratchOut is one of the two fixed scratch buffers M3dColij_LineToThisItem selects
// by vertex count.
// @Ok
static i32 ClipQuadAgainstCoarseMatrix(const u8 *pFaceTable, i16 *pScratchOut, i32 lengthBound, const CVector *pFixedOffset)
{
	u16 vertCount = *reinterpret_cast<const u16*>(pFaceTable + 2);
	const i16 *pLocalVert = reinterpret_cast<const i16*>(pFaceTable + 0x1C);

	if (vertCount == 0)
		return 1551; // 0x60F, matches the original's "no vertices" early-out value

	i16 offX = static_cast<i16>(pFixedOffset->vx);
	i16 offY = static_cast<i16>(pFixedOffset->vy);
	i16 offZ = static_cast<i16>(pFixedOffset->vz);

	i32 andAccum = 1551;

	for (u16 i = 0; i < vertCount; i++)
	{
		i16 x = static_cast<i16>(offX + pLocalVert[0]);
		i16 y = static_cast<i16>(offY + pLocalVert[1]);
		i16 z = static_cast<i16>(offZ + pLocalVert[2]);

		i16 tx = static_cast<i16>((x * gCoarseRotMatrix[0][0] + y * gCoarseRotMatrix[0][1] + z * gCoarseRotMatrix[0][2]) >> 12);
		i16 ty = static_cast<i16>((x * gCoarseRotMatrix[1][0] + y * gCoarseRotMatrix[1][1] + z * gCoarseRotMatrix[1][2]) >> 12);
		i16 tz = static_cast<i16>((x * gCoarseRotMatrix[2][0] + y * gCoarseRotMatrix[2][1] + z * gCoarseRotMatrix[2][2]) >> 12);

		tx = static_cast<i16>(tx + G_TRANSLATION_VECTOR.vx);
		ty = static_cast<i16>(ty + G_TRANSLATION_VECTOR.vy);
		tz = static_cast<i16>(tz + G_TRANSLATION_VECTOR.vz);

		i32 outcode = 0;
		if (tx < 0) outcode |= 4;
		if (ty < 0) outcode |= 2;
		if (tz < 0) outcode |= 1;
		if (tz > lengthBound) outcode |= 8;
		if (tx > 0) outcode |= 0x400;
		if (ty > 0) outcode |= 0x200;

		pScratchOut[0] = tx;
		pScratchOut[1] = ty;
		pScratchOut[2] = tz;
		pScratchOut[3] = static_cast<i16>(outcode);

		andAccum &= outcode;

		pLocalVert += 4;
		pScratchOut += 4;
	}

	return andAccum;
}

// sub_0x0046F1F0 (1176 bytes). Walks pFaceTable's face-record array (variable-length
// records, own size field at +2 of each record, pFaceTable->faceCount records at +6),
// testing each quad face (4 vertex indices at record+4/5/6/7, into pScratch, the SAME
// {x,y,z,outcode} array ClipQuadAgainstCoarseMatrix just filled) against the line.
// Per face: reject via M3dColij_OneMask/M3dColij_ZeroMask/a fixed 0x30000 category
// test on a packed dword at record+12; reject if all 4 vertices' outcodes share an
// out-of-bounds bit; a 2D cross-product convexity test (falls back to the record+4
// vertex substituted by the record+7 vertex when the "flags&0x10" bit allows it, exact
// arithmetic confirmed against raw disasm, not just Hex-Rays) that must pass; then a
// plane-distance test against the face's normal (looked up via (record+12 low 16
// bits)>>3 into pFaceTable's normal table, right after its face-record array) that the
// hit point's line-parameter must fall inside. A pass on a "trigger zone" face
// (record+14 bit 2) records pItem->mModel into dword_5FBDBC when
// pInfo->RecordTriggerZoneHits is set; otherwise it updates pInfo->Distance/pItem/
// pFace/Model as the new nearest hit (all offsets already validated fields on this
// file's own SLineInfo, see validate_SLineInfo below - the "unvalidated CItem offsets"
// the old stub worried about turned out to be these, not CItem fields at all). Finally,
// if a hit survived, interpolates pInfo->Position along the line using the found
// Distance/Length ratio, same math idiom as M3dColij_LineInfoFixup above.
// @Ok
static void TestItemFaces(const u8 *pFaceTable, i16 *pScratch, SLineInfo *pInfo, CItem *pItem, i32 *pFoundToken)
{
	if (pInfo->Length == 0)
		return;

	i32 excludeMask = static_cast<i32>(G_M3DCOLIJ_ONE_MASK);
	i32 zeroMask = G_M3DCOLIJ_ZERO_MASK;

	u16 vertCount = *reinterpret_cast<const u16*>(pFaceTable + 2);
	u16 extraCount = *reinterpret_cast<const u16*>(pFaceTable + 4);
	u16 faceCount = *reinterpret_cast<const u16*>(pFaceTable + 6);

	const u8 *pNormalTable = pFaceTable + 0x1C + 8 * vertCount;
	const u8 *pRecord = pNormalTable + 8 * extraCount;

	for (u16 faceIdx = 0; faceIdx < faceCount; faceIdx++)
	{
		u16 recFlags = *reinterpret_cast<const u16*>(pRecord + 0);
		u16 recSize = *reinterpret_cast<const u16*>(pRecord + 2);
		u8 idx4 = pRecord[4];
		u8 idx5 = pRecord[5];
		u8 idx6 = pRecord[6];
		u8 idx7 = pRecord[7];
		u32 packed = *reinterpret_cast<const u32*>(pRecord + 12);
		u8 triggerFlags = pRecord[14];

		bool reject = false;

		if ((packed & static_cast<u32>(excludeMask)) != 0)
			reject = true;
		else if ((static_cast<u32>(zeroMask) | packed) != 0xFFFFFFFFu)
			reject = true;
		else if (((packed ^ 0x10000u) & 0x30000u) == 0)
			reject = true;

#ifdef SPIDEY_STANDALONE
		i32 dbgMaskReject = reject;
#endif
		if (!reject)
		{
			const i16 *pV4 = pScratch + idx4 * 4;
			const i16 *pV5 = pScratch + idx5 * 4;
			const i16 *pV6 = pScratch + idx6 * 4;
			const i16 *pV7 = pScratch + idx7 * 4;

			u16 oc = static_cast<u16>(pV7[3] & pV4[3] & pV5[3] & pV6[3]);
			if (oc & 0x60F)
				reject = true;
		}
#ifdef SPIDEY_STANDALONE
		if (gDbgColij == 2)
		{
			const i16 *pV4 = pScratch + idx4 * 4;
			const i16 *pV5 = pScratch + idx5 * 4;
			const i16 *pV6 = pScratch + idx6 * 4;
			const i16 *pV7 = pScratch + idx7 * 4;
			fprintf(stderr, "    ALLFACE %d flags=%#x packed=%#x idx=%d,%d,%d,%d v4=(%d,%d,%d|%#x) v5=(%d,%d,%d|%#x) v6=(%d,%d,%d|%#x) v7=(%d,%d,%d|%#x) %s\n", faceIdx, recFlags, packed, idx4, idx5, idx6, idx7,
				pV4[0], pV4[1], pV4[2], (u16)pV4[3], pV5[0], pV5[1], pV5[2], (u16)pV5[3], pV6[0], pV6[1], pV6[2], (u16)pV6[3], pV7[0], pV7[1], pV7[2], (u16)pV7[3],
				dbgMaskReject ? "MASKREJECT" : (reject ? "OUTCODEREJECT" : "geom"));
		}
#endif

		if (!reject)
		{
			const i16 *pV4 = pScratch + idx4 * 4;
			const i16 *pV5 = pScratch + idx5 * 4;
			const i16 *pV6 = pScratch + idx6 * 4;
			const i16 *pV7 = pScratch + idx7 * 4;

			i32 v39 = static_cast<i32>(pV5[0]) * pV6[1] - static_cast<i32>(pV5[1]) * pV6[0];

			i16 refX, refY;
			if (v39 >= 0)
			{
				refX = pV4[0];
				refY = pV4[1];
			}
			else if (recFlags & 0x10)
			{
				// 0x46F399: a triangle (flag 0x10) has no second half, so a
				// point on the far side of the v5 v6 diagonal is outside it
				reject = true;
				refX = refY = 0;
			}
			else
			{
				// a quad: test the point against the second triangle (v7 side)
				refX = pV7[0];
				refY = pV7[1];
			}

			if (!reject)
			{
				// Hex-Rays at 0x46F1F0: the two edge tests have opposite
				// orientation depending on which triangle of the quad is
				// used. First triangle (ref = v4): A = P6.x*P4.y - P4.x*P6.y,
				// B = P4.x*P5.y - P5.x*P4.y. Second (ref = v7): A = P7.x*P6.y -
				// P6.x*P7.y, B = P5.x*P7.y - P7.x*P5.y. (A single formula for
				// both had one sign flipped each way and rejected every face,
				// found by the standalone build: no floor collision at all.)
				i32 crossA, crossB;
				if (v39 >= 0)
				{
					crossA = static_cast<i32>(pV6[0]) * refY - static_cast<i32>(refX) * pV6[1];
					crossB = static_cast<i32>(refX) * pV5[1] - static_cast<i32>(pV5[0]) * refY;
				}
				else
				{
					crossA = static_cast<i32>(refX) * pV6[1] - static_cast<i32>(pV6[0]) * refY;
					crossB = static_cast<i32>(pV5[0]) * refY - static_cast<i32>(refX) * pV5[1];
				}

				if (crossA < 0 || crossB < 0)
					reject = true;

#ifdef SPIDEY_STANDALONE
				if (gDbgColij)
					fprintf(stderr, "    FACE %d flags=%#x idx=%d,%d,%d,%d v4=(%d,%d,%d) v5=(%d,%d,%d) v6=(%d,%d,%d) v7=(%d,%d,%d) v39=%d crossA=%d crossB=%d %s\n", faceIdx, recFlags, idx4, idx5, idx6, idx7,
						pV4[0], pV4[1], pV4[2], pV5[0], pV5[1], pV5[2], pV6[0], pV6[1], pV6[2], pV7[0], pV7[1], pV7[2], v39, crossA, crossB, reject ? "reject" : "PASS-edges");
#endif

				if (!reject)
				{
					u32 normalIdx = (packed & 0xFFFFu) >> 3;
					const i16 *pNormal = reinterpret_cast<const i16*>(pNormalTable + 8 * normalIdx);
					i32 nx = pNormal[0];
					i32 ny = pNormal[1];
					i32 nz = pNormal[2];

					i32 rowDot0 = (gCoarseRotMatrix[0][0] * nx + gCoarseRotMatrix[0][1] * ny + gCoarseRotMatrix[0][2] * nz) >> 12;
					i32 rowDot1 = (gCoarseRotMatrix[1][0] * nx + gCoarseRotMatrix[1][1] * ny + gCoarseRotMatrix[1][2] * nz) >> 12;
					i32 rowDot2 = (gCoarseRotMatrix[2][0] * nx + gCoarseRotMatrix[2][1] * ny + gCoarseRotMatrix[2][2] * nz) >> 12;

					i32 p4x = pV4[0];
					i32 p4y = pV4[1];
					i32 p4z = pV4[2];

					i32 numerator = p4x * rowDot0 + p4y * rowDot1 + p4z * rowDot2;
					i32 denom = (pInfo->Length + 2) * rowDot2;

					if (numerator > 0 || numerator < denom)
						reject = true;
#ifdef SPIDEY_STANDALONE
					if (gDbgColij)
						fprintf(stderr, "      plane num=%d denom=%d rowDot=(%d,%d,%d) %s\n", numerator, denom, rowDot0, rowDot1, rowDot2, reject ? "reject" : "HIT");
#endif

					if (!reject)
					{
						if (triggerFlags & 2)
						{
							if (pInfo->RecordTriggerZoneHits)
								*pFoundToken = pItem->mModel;
						}
						else
						{
							i32 planeZ = rowDot2;
							if (my_abs(planeZ) < 5)
								planeZ = -5;

							i32 t = numerator / planeZ;

							if (t < pInfo->Distance)
							{
								pInfo->pItem = pItem;
								pInfo->Distance = t;
								pInfo->pFace = reinterpret_cast<u32*>(const_cast<u8*>(pRecord));
								pInfo->Model = pItem->mModel;

								G_LINE_TO_SPHERE_BEST_T = t;
								G_CURRENT_COLIJ_VECTOR = reinterpret_cast<SVECTOR*>(const_cast<u8*>(pNormalTable) + 8 * normalIdx);
								*gLineColijLastFacePtr = reinterpret_cast<i32>(pInfo->pFace);
							}
						}
					}
				}
			}
		}

		pRecord += recSize;
	}

	if (pInfo->Distance != 0x7FFFFFFF)
	{
		i32 t = M3dMaths_MulDiv64(pInfo->Distance, 4096, pInfo->Length);
		pInfo->Position.vx = pInfo->StartCoords.vx + M3dMaths_MulDiv64(pInfo->EndCoords.vx - pInfo->StartCoords.vx, t, 4096);
		pInfo->Position.vy = pInfo->StartCoords.vy + M3dMaths_MulDiv64(pInfo->EndCoords.vy - pInfo->StartCoords.vy, t, 4096);
		pInfo->Position.vz = pInfo->StartCoords.vz + M3dMaths_MulDiv64(pInfo->EndCoords.vz - pInfo->StartCoords.vz, t, 4096);
	}
}

// Two fixed scratch buffers the original picks between by vertex count (>0x80 uses the
// bigger one), reusing the SAME addresses other systems already use as general-purpose
// scratch RAM elsewhere in this repo (0x614CD4 = bit.cpp's gGlowRingBase / weapons.cpp's
// gGouraudRibbonScreenPoints scratch; 0x628618 holds a POINTER, set at init time in
// init.cpp to &unk_628690, to the larger buffer - confirmed via disasm: the small buffer
// is used as `offset dword_614CD4` [address-of], the large one as `dword_628618`
// [value-of, i.e. a pointer variable]).
static i16 * const gCoarseFaceScratchSmall = reinterpret_cast<i16*>(0x00614CD4);
static i16 ** const gCoarseFaceScratchLargePtr = reinterpret_cast<i16**>(0x00628618);

// @Ok
// Original at 0x4529C0, 617 bytes. Fully decompiled 2026-09-01 after resolving the
// "coarse GTE" subsystem above; see that block's comments for the evidence behind every
// new helper/global it uses. Two shapes, both ending in the same shared tail
// (ClipQuadAgainstCoarseMatrix then, unless trivially rejected, TestItemFaces):
//  - "fast" shape (pItem->mAngles all zero AND !(mFlags&0x200)): skips building a real
//    rotation matrix entirely, just uses (pItem->mPos - pInfo->StartCoords)>>12 as the
//    fixed per-vertex offset and resets translationVector to 0.
//  - "full" shape (rotation and/or the 0x200 scale flag): computes a DropDown-aware
//    start-relative position vector, round-trips it through the coarse GTE pipeline to
//    get translationVector, then builds gCoarseRotMatrix either as
//    identity*(optional scale) or as the item's real YXZ rotation*(optional scale)
//    depending on whether mAngles is actually zero (it can still reach the "full" shape
//    via the 0x200 flag alone), and uses a zero fixed-offset vector (translationVector
//    already carries the position).
// A hit found by TestItemFaces (via *pFoundToken, only set on the "trigger zone"
// branch) resolves through Spool_GetEnvIndex(pItem->mRegion)/gEnvTriggerCommandIds and
// fires Trig_TriggerCommandPoint; the "nearest hit" branch already wrote
// pInfo->pItem/Distance/pFace/Model/Position directly inside TestItemFaces.
void M3dColij_LineToThisItem(CItem* pItem, SLineInfo* pInfo)
{
	SetCoarseRotMatrix(reinterpret_cast<MATRIX*>(&pInfo->WorldCst));

	const i32 *pModel = CItemRelatedList[pItem->mRegion * 17][pItem->mModel];
	const u8 *pFaceTable = reinterpret_cast<const u8*>(pModel);

	u16 vertCount = *reinterpret_cast<const u16*>(pFaceTable + 2);
	print_if_false(vertCount != 0, "Collision check on a model with\tno\tvertices");

	i16 *pScratch = (vertCount > 0x80) ? *gCoarseFaceScratchLargePtr : gCoarseFaceScratchSmall;

	CVector fixedOffset(0, 0, 0);
	i32 foundToken = -1;

	bool hasGeometry = (pItem->mAngles.vx != 0) || (pItem->mAngles.vy != 0) || (pItem->mAngles.vz != 0)
	                 || (pItem->mFlags & 0x200);

	if (!hasGeometry)
	{
		fixedOffset.vx = (pItem->mPos.vx - pInfo->StartCoords.vx) >> 12;
		fixedOffset.vy = (pItem->mPos.vy - pInfo->StartCoords.vy) >> 12;
		fixedOffset.vz = (pItem->mPos.vz - pInfo->StartCoords.vz) >> 12;

		m3d_ZeroTransVector();
	}
	else
	{
		if (pInfo->DropDown)
		{
			gLineColijRelPos->vx = (pItem->mPos.vx - pInfo->StartCoords.vx) >> 12;
			gLineColijRelPos->vy = (pInfo->StartCoords.vz - pItem->mPos.vz) >> 12;
			gLineColijRelPos->vz = (pItem->mPos.vy - pInfo->StartCoords.vy) >> 12;
		}
		else
		{
			gLineColijRelPos->vx = (pItem->mPos.vx - pInfo->StartCoords.vx) >> 12;
			gLineColijRelPos->vy = (pItem->mPos.vy - pInfo->StartCoords.vy) >> 12;
			gLineColijRelPos->vz = (pItem->mPos.vz - pInfo->StartCoords.vz) >> 12;
		}

		// 0x4529C0: only the non DropDown branch runs the relative position
		// through the coarse GTE, the DropDown branch uses it as is
		if (!pInfo->DropDown)
		{
			SetCoarseTranslationVector(gLineColijRelPos);
			CoarseTransformPoint();
			gte_stlvnl(reinterpret_cast<VECTOR*>(gLineColijRelPos));
		}

		SetTranslationVectorFromOffset(reinterpret_cast<u8*>(gLineColijRotMatrix));

		if (pItem->mAngles.vx == 0 && pItem->mAngles.vy == 0 && pItem->mAngles.vz == 0)
		{
			M3dMaths_SetIdentityRotation(gLineColijRotMatrix);
		}
		else
		{
			M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR*>(&pItem->mAngles), gLineColijRotMatrix);
		}

		if (pItem->mFlags & 0x200)
		{
			M3dMaths_ScaleMatrix(pItem, gLineColijRotMatrix);
		}

		MulCoarseRotMatrix(gLineColijRotMatrix, gLineColijRotMatrix);
		SetCoarseRotMatrix(gLineColijRotMatrix);

		fixedOffset.vx = 0;
		fixedOffset.vy = 0;
		fixedOffset.vz = 0;
	}

	i32 outcodeMask = ClipQuadAgainstCoarseMatrix(pFaceTable, pScratch, pInfo->Length, &fixedOffset);

#ifdef SPIDEY_STANDALONE
	if (gDbgColij)
	{
		CItem* before = pInfo->pItem;
		if (!(outcodeMask & 0x60F))
			TestItemFaces(pFaceTable, pScratch, pInfo, pItem, &foundToken);
		fprintf(stderr, "  COLIJ item=%p model=%d region=%d pos=(%d,%d,%d) ang=(%d,%d,%d) flags=%#x outcode=%#x %s%s\n", (void*)pItem, pItem->mModel, pItem->mRegion,
			pItem->mPos.vx >> 12, pItem->mPos.vy >> 12, pItem->mPos.vz >> 12, pItem->mAngles.vx, pItem->mAngles.vy, pItem->mAngles.vz, pItem->mFlags, outcodeMask,
			(outcodeMask & 0x60F) ? "BOXREJECT" : "faces", (pInfo->pItem != before) ? " HIT" : "");
		if (outcodeMask & 0x60F)
			return;
	}
	else
	{
#endif
	if (outcodeMask & 0x60F)
		return;

	TestItemFaces(pFaceTable, pScratch, pInfo, pItem, &foundToken);
#ifdef SPIDEY_STANDALONE
	}
#endif

	if (foundToken == -1)
		return;

	i32 envIndex = Spool_GetEnvIndex(pItem->mRegion);
	print_if_false(envIndex != -1, "Found a trigger\tzone not\tin\tthe environment");

	G_TRIGGER_COLLISION_CHECK = 0;
	*gSpoolColijEnvIndex = envIndex ^ 1;

	u32 commandId = gEnvTriggerCommandIds[envIndex][foundToken];
	Trig_TriggerCommandPoint(commandId, true);
}

// @Ok
// @Leak
// @Matching
void M3dColij_LineToItem(
		CItem* pItem,
		SLineInfo* pInfo)
{

	if	(!pItem)	return;

	if (pInfo->Length==0)
		return;

	gte_SetRotMatrix(&pInfo->WorldCst);
	M3dAsm_LineColijPreprocessItems(pItem, 0, pInfo, pInfo->Inquiry);

	for (	; pItem;	pItem=pItem->mNextItem)
		if	(pItem->mInquiry != pInfo->Inquiry)
		{
			pItem->mInquiry	= pInfo->Inquiry;
			M3dColij_LineToThisItem(pItem, pInfo);
		}
}

void validate_Vector(void)
{
	VALIDATE_SIZE(Vector, 0xC);

	VALIDATE(Vector, vx, 0x0);
	VALIDATE(Vector, vy, 0x4);
	VALIDATE(Vector, vz, 0x8);
}

void validate_SLineInfo(void)
{
	VALIDATE_SIZE(SLineInfo, 0xA4);

	VALIDATE(SLineInfo, StartCoords, 0x0);
	VALIDATE(SLineInfo, EndCoords, 0xC);


	VALIDATE(SLineInfo, MinCoords, 0x18);

	VALIDATE(SLineInfo, MaxCoords, 0x24);

	VALIDATE(SLineInfo, iLo, 0x30);
	VALIDATE(SLineInfo, iHi, 0x34);
	VALIDATE(SLineInfo, jLo, 0x38);
	VALIDATE(SLineInfo, jHi, 0x3C);

	VALIDATE(SLineInfo, Distance, 0x40);
	VALIDATE(SLineInfo, Length, 0x44);

	VALIDATE(SLineInfo, WorldCst, 0x48);

	VALIDATE(SLineInfo, pItem, 0x68);

	VALIDATE(SLineInfo, Position, 0x6C);

	VALIDATE(SLineInfo, Normal, 0x78);

	VALIDATE(SLineInfo, pFace, 0x80);
	VALIDATE(SLineInfo, Model, 0x84);

	VALIDATE(SLineInfo, RecordTriggerZoneHits, 0x88);
	VALIDATE(SLineInfo, DropDown, 0x89);

	VALIDATE(SLineInfo, Inquiry, 0x8A);
	VALIDATE(SLineInfo, tNear, 0x8C);

	VALIDATE(SLineInfo, tNumtrLo, 0x90);
	VALIDATE(SLineInfo, tNumtrHi, 0x94);
	VALIDATE(SLineInfo, tDenomLo, 0x98);
	VALIDATE(SLineInfo, tDenomHi, 0x9C);
	VALIDATE(SLineInfo, NormalOffset, 0xA0);
}

#include "compatibility/my_patch.h"

// @Bogus
void patch_m3dcolij(void)
{
	PATCH_PUSH_RET(0x004527C0, M3dColij_LineToItem);
	PATCH_PUSH_RET(0x00452820, M3dColij_LineToItemZoned);
	PATCH_PUSH_RET(0x004528E0, M3dColij_LineInfoFixup);
}
