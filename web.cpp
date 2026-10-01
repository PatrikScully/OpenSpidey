#include "web.h"
#include "m3dinit.h"
#include "m3dcolij.h"
#include "m3dzone.h"
#include "ps2m3d.h"
#include "spidey.h"
#include "baddy.h"
#include "utils.h"
#include "camera.h"
#include "spool.h"
#include "decomp.h"
#include <string.h>
#include "ps2funcs.h"
#include "chain.h"
#include "manipob.h"

#include "validate.h"

#ifndef SPIDEY_STANDALONE
i32 gFireDomes;
#else
extern i32 gFireDomes;
#endif
#ifndef SPIDEY_STANDALONE
i32 gNumDomes;
#else
extern i32 gNumDomes;
#endif
#ifndef SPIDEY_STANDALONE
CBody* WebList;
#else
extern CBody* WebList;
#endif

EXPORT i32 gGetGroundDefaultValue;

extern CBody* MiscList;

// sin/cos table, same address and idiom as bit.cpp / camera.cpp
// (word_610C48[idx] = sin, word_610C48[idx + 1] = cos, idx = 2 * (angle & 0xFFF));
// file-local copy per repo convention.
static i16 * const word_610C48 = (i16*)0x610C48;

// Second table parallel to CItemRelatedList (ob.h, 0x6B2454), indexed the same way
// (region*17). Web_CollideWithSuper (0x4F7AE0) is the only known reader: it reads a hook
// COUNT from this table's per-region entry at offset+8
// (`*(DWORD*)(dword_6B2458[region*17]+8)`). No idb_globals.txt entry; name and every field
// beyond that one offset are our guess.
static i32 *** const gWebHookCountTable = (i32***)0x006B2458;

// @Ok
// 2026-08-31: functional decompile (session bar). Address 0x4F7680. Previously undeclared and
// unimplemented anywhere in the repo (tools/names.json's own tentative name). Self-contained
// axis-aligned-box-vs-line-segment clip test, independent of any mesh/SModel struct knowledge
// (all four parameters are raw SVECTOR triples): a coarse per-axis trivial-reject first, then
// 6 unrolled face-plane clips (classic fully-unrolled PS1-era collision macro expansion,
// confirmed instruction-by-instruction against the 0x4F7680 disasm). Returns 1 and overwrites
// *pEnd with the exact crossing point on the first face the segment actually crosses INSIDE
// the box's other two dimensions; returns 0 if the segment never crosses the box.
i32 BoundingBoxCollisionCheck(SVECTOR const *pMin, SVECTOR const *pMax, SVECTOR const *pStart, SVECTOR *pEnd)
{
	if (pStart->vy > pMin->vy && pEnd->vy > pMin->vy)
		return 0;

	if (pStart->vy < pMax->vy && pEnd->vy < pMax->vy)
		return 0;

	if (pStart->vx > pMin->vx && pEnd->vx > pMin->vx)
		return 0;

	if (pStart->vx < pMax->vx && pEnd->vx < pMax->vx)
		return 0;

	if (pStart->vz > pMin->vz && pEnd->vz > pMin->vz)
		return 0;

	if (pStart->vz < pMax->vz && pEnd->vz < pMax->vz)
		return 0;

	i32 dy = pEnd->vy - pStart->vy;
	i32 dz = pEnd->vz - pStart->vz;
	i32 dx = pEnd->vx - pStart->vx;

	// Face: x == pMin->vx
	if (pStart->vx > pMin->vx && pEnd->vx < pMin->vx)
	{
		i32 y = pStart->vy + dy * (pMin->vx - pStart->vx) / dx;
		i32 z = pStart->vz + dz * (pMin->vx - pStart->vx) / dx;

		if (y < pMin->vy && y > pMax->vy && z < pMin->vz && z > pMax->vz)
		{
			pEnd->vx = pMin->vx;
			pEnd->vy = static_cast<i16>(y);
			pEnd->vz = static_cast<i16>(z);
			return 1;
		}
	}

	// Face: x == pMax->vx
	if (pStart->vx < pMax->vx && pEnd->vx > pMax->vx)
	{
		i32 y = pStart->vy + dy * (pMax->vx - pStart->vx) / dx;
		i32 z = pStart->vz + dz * (pMax->vx - pStart->vx) / dx;

		if (y < pMin->vy && y > pMax->vy && z < pMin->vz && z > pMax->vz)
		{
			pEnd->vx = pMax->vx;
			pEnd->vy = static_cast<i16>(y);
			pEnd->vz = static_cast<i16>(z);
			return 1;
		}
	}

	// Face: y == pMin->vy
	if (pStart->vy > pMin->vy && pEnd->vy < pMin->vy)
	{
		i32 x = pStart->vx + dx * (pMin->vy - pStart->vy) / dy;
		i32 z = pStart->vz + dz * (pMin->vy - pStart->vy) / dy;

		if (x < pMin->vx && x > pMax->vx && z < pMin->vz && z > pMax->vz)
		{
			pEnd->vx = static_cast<i16>(x);
			pEnd->vy = pMin->vy;
			pEnd->vz = static_cast<i16>(z);
			return 1;
		}
	}

	// Face: y == pMax->vy
	if (pStart->vy < pMax->vy && pEnd->vy > pMax->vy)
	{
		i32 x = pStart->vx + dx * (pMax->vy - pStart->vy) / dy;
		i32 z = pStart->vz + dz * (pMax->vy - pStart->vy) / dy;

		if (x < pMin->vx && x > pMax->vx && z < pMin->vz && z > pMax->vz)
		{
			pEnd->vx = static_cast<i16>(x);
			pEnd->vy = pMax->vy;
			pEnd->vz = static_cast<i16>(z);
			return 1;
		}
	}

	// Face: z == pMin->vz
	if (pStart->vz > pMin->vz && pEnd->vz < pMin->vz)
	{
		i32 x = pStart->vx + dx * (pMin->vz - pStart->vz) / dz;
		i32 y = pStart->vy + dy * (pMin->vz - pStart->vz) / dz;

		if (x < pMin->vx && x > pMax->vx && y < pMin->vy && y > pMax->vy)
		{
			pEnd->vx = static_cast<i16>(x);
			pEnd->vy = static_cast<i16>(y);
			pEnd->vz = pMin->vz;
			return 1;
		}
	}

	// Face: z == pMax->vz
	if (pStart->vz >= pMax->vz)
		return 0;

	if (pEnd->vz <= pMax->vz)
		return 0;

	i32 x = pStart->vx + dx * (pMax->vz - pStart->vz) / dz;
	i32 y = pStart->vy + dy * (pMax->vz - pStart->vz) / dz;

	if (x >= pMin->vx || x <= pMax->vx || y >= pMin->vy || y <= pMax->vy)
		return 0;

	pEnd->vx = static_cast<i16>(x);
	pEnd->vy = static_cast<i16>(y);
	pEnd->vz = pMax->vz;
	return 1;
}

// @Ok
// 2026-08-31: functional decompile (session bar), address 0x4F7AE0 (1126 bytes). Supersedes
// the earlier "investigated, left as a stub" note this function used to carry (still visible
// in git history): BoundingBoxCollisionCheck (above) is now implemented, and the GTE
// world-to-local rotate is the same idiom Utils_RotateWorldToObject (utils.cpp) already uses,
// just run four times: once for pStart/pEnd against pSuper's own matrix, then again per hook
// against that hook's own local matrix, read straight out of Decomp_GetAnimTransform's
// SMatrix array (decomp.h/ob.h). SMatrix::m is bit-compatible with MATRIX::m (both i16[3][3],
// the only field M3dMaths_TransposeMatrix1 touches), confirmed by the disasm's own inner-loop
// call to it with a SMatrix* (0x4F7AE0's v16) in place of a MATRIX*.
//
// The one thing still not independently confirmed anywhere else in the repo is the exact
// record CItemRelatedList[region*17] (ob.h) points at per hook: only the one offset actually
// read here is used (+12: a packed, per-axis-interleaved i16[6] bounding box -- min0,max0,
// min1,max1,min2,max2, confirmed both by BoundingBoxCollisionCheck's own two-SVECTOR-corner
// signature and by the disasm's own stack packing of the 6 values into two interleaved
// triples). Reproduced below as raw offset arithmetic rather than invented as a named struct,
// since naming fields never read here would be a guess this repo's bar explicitly forbids.
// gWebHookCountTable (this file, own comment) is the same kind of parallel, only-partially-
// understood table.
//
// Walks pSuper's hook table for its region, skipping hook 0 and any hook whose bit is clear
// in pSuper->field_194 (CSuper, ob.h). For each remaining hook: offsets the pSuper-local
// segment by the hook's own SMatrix::t (>>4), rotates that offset segment again by the hook's
// own SMatrix::m (transposed), scales the hook's bounding box by a5/4096 (or takes it
// verbatim when a5==4096), and tests the doubly-local segment against that box with
// BoundingBoxCollisionCheck. Among every hook whose box the segment actually crosses, keeps
// the one whose hit point is closest to the segment start (Euclidean distance via
// gte_ldlvl+gte_sqr0+gte_stlvnl+M3dMaths_SquareRoot0, the same idiom M3dColij_LineToSphere
// already uses, m3dcolij.cpp) and fills pOut with that hook's hit position (scaled back up by
// <<4) and pOut->Offset with the winning hook's bit index. Returns 1 if any hook was found,
// 0 otherwise (pOut->Offset != -1).
i32 Web_CollideWithSuper(CSuper *pSuper, CVector const *pStart, CVector const *pEnd, SHook *pOut, i32 a5)
{
	MATRIX superMatrix;
	M3dMaths_TransposeMatrix1(&pSuper->mTransform, &superMatrix);
	memcpy(G_ROT_MATRIX, &superMatrix, sizeof(G_ROT_MATRIX));

	SVECTOR localStart;
	localStart.vx = static_cast<i16>((pStart->vx - pSuper->mPos.vx) >> 12);
	localStart.vy = static_cast<i16>((pStart->vy - pSuper->mPos.vy) >> 12);
	localStart.vz = static_cast<i16>((pStart->vz - pSuper->mPos.vz) >> 12);

	MTC2(*reinterpret_cast<i32*>(&localStart.vx), GT_ZERO);
	MTC2(*reinterpret_cast<i32*>(&localStart.vz), GT_ONE);
	gte_mvmva(1, 0, 0, 3, 0);
	gte_stsv(&localStart);

	SVECTOR localEnd;
	localEnd.vx = static_cast<i16>((pEnd->vx - pSuper->mPos.vx) >> 12);
	localEnd.vy = static_cast<i16>((pEnd->vy - pSuper->mPos.vy) >> 12);
	localEnd.vz = static_cast<i16>((pEnd->vz - pSuper->mPos.vz) >> 12);

	MTC2(*reinterpret_cast<i32*>(&localEnd.vx), GT_ZERO);
	MTC2(*reinterpret_cast<i32*>(&localEnd.vz), GT_ONE);
	gte_mvmva(1, 0, 0, 3, 0);
	gte_stsv(&localEnd);

	pOut->Offset = -1;

	u32 bestDist = 0xFFFFFFFFu;

	SMatrix *pHookMatrix = Decomp_GetAnimTransform(pSuper) + 1;

	i32 regionBase = pSuper->mRegion * 17;
	i32 **pRegionHooks = CItemRelatedList[regionBase] + 1;
	i32 hookCount = *reinterpret_cast<i32*>(reinterpret_cast<char*>(gWebHookCountTable[regionBase]) + 8);

	i32 bit = 2;

	for (i32 hookIndex = 1; hookIndex < hookCount; hookIndex++, bit <<= 1, pHookMatrix++, pRegionHooks++)
	{
		if (pSuper->field_194 & bit)
			continue;

		i16 offX = static_cast<i16>(pHookMatrix->t[0] >> 4);
		i16 offY = static_cast<i16>(pHookMatrix->t[1] >> 4);
		i16 offZ = static_cast<i16>(pHookMatrix->t[2] >> 4);

		SVECTOR hookStart;
		hookStart.vx = static_cast<i16>(localStart.vx - offX);
		hookStart.vy = static_cast<i16>(localStart.vy - offY);
		hookStart.vz = static_cast<i16>(localStart.vz - offZ);

		SVECTOR hookEnd;
		hookEnd.vx = static_cast<i16>(localEnd.vx - offX);
		hookEnd.vy = static_cast<i16>(localEnd.vy - offY);
		hookEnd.vz = static_cast<i16>(localEnd.vz - offZ);

		MATRIX hookMatrixT;
		M3dMaths_TransposeMatrix1(reinterpret_cast<MATRIX*>(pHookMatrix), &hookMatrixT);
		memcpy(G_ROT_MATRIX, &hookMatrixT, sizeof(G_ROT_MATRIX));

		MTC2(*reinterpret_cast<i32*>(&hookStart.vx), GT_ZERO);
		MTC2(*reinterpret_cast<i32*>(&hookStart.vz), GT_ONE);
		gte_mvmva(1, 0, 0, 3, 0);
		gte_stsv(&hookStart);

		MTC2(*reinterpret_cast<i32*>(&hookEnd.vx), GT_ZERO);
		MTC2(*reinterpret_cast<i32*>(&hookEnd.vz), GT_ONE);
		gte_mvmva(1, 0, 0, 3, 0);
		gte_stsv(&hookEnd);

		i16 *pBox = reinterpret_cast<i16*>(reinterpret_cast<char*>(*pRegionHooks) + 12);

		SVECTOR boxMin;
		SVECTOR boxMax;

		if (a5 == 4096)
		{
			boxMin.vx = pBox[0];
			boxMax.vx = pBox[1];
			boxMin.vy = pBox[2];
			boxMax.vy = pBox[3];
			boxMin.vz = pBox[4];
			boxMax.vz = pBox[5];
		}
		else
		{
			boxMin.vx = static_cast<i16>((a5 * pBox[0]) >> 12);
			boxMax.vx = static_cast<i16>((a5 * pBox[1]) >> 12);
			boxMin.vy = static_cast<i16>((a5 * pBox[2]) >> 12);
			boxMax.vy = static_cast<i16>((a5 * pBox[3]) >> 12);
			boxMin.vz = static_cast<i16>((a5 * pBox[4]) >> 12);
			boxMax.vz = static_cast<i16>((a5 * pBox[5]) >> 12);
		}

		SVECTOR hitPoint = hookEnd;

		if (BoundingBoxCollisionCheck(&boxMin, &boxMax, &hookStart, &hitPoint))
		{
			CVector delta;
			delta.vx = hookStart.vx - hitPoint.vx;
			delta.vy = hookStart.vy - hitPoint.vy;
			delta.vz = hookStart.vz - hitPoint.vz;

			gte_ldlvl(reinterpret_cast<VECTOR*>(&delta));
			gte_sqr0();

			VECTOR deltaSq;
			gte_stlvnl(&deltaSq);

			u32 dist = static_cast<u32>(M3dMaths_SquareRoot0(deltaSq.vx + deltaSq.vy + deltaSq.vz));

			if (dist < bestDist)
			{
				bestDist = dist;
				pOut->Part.vx = static_cast<i16>(hitPoint.vx << 4);
				pOut->Part.vy = static_cast<i16>(hitPoint.vy << 4);
				pOut->Part.vz = static_cast<i16>(hitPoint.vz << 4);
				pOut->Offset = static_cast<i16>(hookIndex);
			}
		}
	}

	return pOut->Offset != -1;
}

// @Ok
CDomeShockWave::CDomeShockWave(i32 a2)
{
	this->mType = 8;
	this->mPos = G_MECHLIST_PLAYER->mPos;
	this->mPos.vy += 204800;

	this->field_44.vx = 240;
	this->field_44.vy = 240;
	this->field_44.vz = 240;

	this->field_90 = a2;

	this->ResetHitFlags(G_BADDY_LIST);
	this->ResetHitFlags(G_ENVIRONMENTAL_OBJECT_LIST);

	for (i32 i = 0; i < 16; i++)
	{
		this->field_50[i] = Rnd(4096);
	}

	G_CAMERA_LIST->Shake(this->mPos, CAMERASHAKE_MEDIUM);
}

// @Ok
CDomeShockWave::~CDomeShockWave(void)
{
}

// @Ok
CDomePiece::CDomePiece(
		CVector* a2,
		i32 a3,
		i32 a4,
		i32 a5)
{
	this->mPos = *a2;
	this->field_F8 = a4;

	if (a5)
		this->InitItem("firedome");
	else
		this->InitItem("webdome3");

	print_if_false(a3 < reinterpret_cast<u32*>(G_PSXREGION[this->mRegion].ppModels)[-1], "Bad Model sent to CDomePiece");

	this->mModel = a3;
	this->AttachTo(&MiscList);
	this->mFlags |= 0x400;
	this->mRGB = 0;
	this->field_FC = 4;
}

// @Ok
CDomePiece::~CDomePiece(void)
{
	this->DeleteFrom(&MiscList);
}

// @Ok
CDome::CDome(
		CPlayer* pSpidey,
		i32 a3)
{
	print_if_false(pSpidey != 0, "NULL pSpidey");
	this->hPlayer = Mem_MakeHandle(pSpidey);

	this->mPos = pSpidey->mPos;

	this->mPos.vy += pSpidey->field_EA8 << 12;
	this->field_104 = a3;

	if ( a3 )
	{
		this->InitItem("firedome");
		this->mModel = 1;
		G_FIRE_DOMES++;
		this->mFlags |= 0x200;
		this->mScale.vy = 0;
	}
	else
	{
		this->InitItem("webdome2");
	}

	this->mFlags |= 1;
	this->AttachTo(&MiscList);
	this->field_100 = 0;
	++G_NUM_DOMES;
}

// @Ok
CDome::~CDome(void)
{
	this->DeleteFrom(&MiscList);

	delete this->field_108;
	delete this->field_10C;
	delete this->field_110;
	delete this->field_114;
	delete this->field_118;

	if (this->field_104)
		G_FIRE_DOMES--;
	G_NUM_DOMES--;
}

// The single live CDomeRing. Written by CDomeRing's constructor (which
// deletes the previous ring first) and cleared by its destructor, which also
// asserts it still points at itself. Only web.cpp touches 0x6B559C, apart
// from a level-teardown reset at 0x45566A, so it stays file-local here. No
// idb_globals.txt entry, the name is ours.
static CDomeRing ** const gpCurrentDomeRing = (CDomeRing**)0x006B559C;

// @Ok
// 0x4F5510, 494 bytes. Builds the expanding shockwave ring that a dome leaves
// behind when it pops. Only one may exist, so any previous one is deleted
// first. Saves the ring model's vertices into field_F8 (the destructor puts
// them back, because CDomeRing animates the SHARED region model in place) and
// precomputes a per-vertex outward direction into field_FC.
//
// Original defect kept: field_FC[3*i + 1] (the y component of the direction)
// is never written, so every ring runs off whatever the freshly allocated
// block happened to hold there.
CDomeRing::CDomeRing(const CVector *pPos, i32 bFire)
{
	if (*gpCurrentDomeRing != 0)
		delete *gpCurrentDomeRing;

	*gpCurrentDomeRing = this;

	this->field_10C = bFire;

	if (bFire != 0)
		this->InitItem("firering");
	else
		this->InitItem("ring");

	this->AttachTo(&MiscList);

	if (this->field_10C != 0)
		this->mFlags |= 0x200;
	else
		this->mFlags |= 0x600;

	this->mRGB = 0x808080;

	this->mPos = *pPos;
	this->field_104 = 100;
	this->mPos.vy -= 0x5A000;
	this->field_108 = 3;

	SModel *pModel = G_PSXREGION[this->mRegion].ppModels[0];
	this->field_100 = pModel;

	this->field_F8 = reinterpret_cast<i16*>(Mem_New(6 * pModel->NumVertices));
	this->field_FC = reinterpret_cast<i16*>(Mem_New(6 * pModel->NumVertices));

	i16 *pSaved = this->field_F8;
	i16 *pDir = this->field_FC;
	SVECTOR *pVert = reinterpret_cast<SVECTOR*>(reinterpret_cast<u8*>(pModel) + 28);

	i32 i = 0;

	if (pModel->NumVertices != 0)
	{
		do
		{
			pSaved[0] = pVert->vx;
			pSaved[1] = pVert->vy;
			pSaved[2] = pVert->vz;

			i32 length = M3dMaths_SquareRoot0(pVert->vz * pVert->vz + pVert->vx * pVert->vx);
			print_if_false(length != 0, "Divide by zero in CDomeRing!");

			pDir[0] = (i16)(pVert->vx * this->field_104 / length);
			pDir[2] = (i16)(pVert->vz * this->field_104 / length);

			pSaved += 3;
			pDir += 3;
			pVert++;
			i++;
		}
		while (i < this->field_100->NumVertices);
	}
}

// @Ok
// 0x4F5720, 215 bytes. Puts the shared region model's vertices back the way
// CDomeRing's constructor found them, frees the two scratch buffers and drops
// the singleton.
//
// Note: CDomeRing's own virtual overrides (vtable 0x53C73C slots 2, 5, 6 and
// 7 -- sub_4F5800 animates the model vertices outwards each frame, sub_4F58B0
// / sub_4F5960 / sub_4F9040 are three class-specific virtuals) are still not
// decompiled, so a repo-built CDomeRing gets CBody's vtable and never
// animates. The constructor and destructor above are faithful; the rest of
// the class is open work.
CDomeRing::~CDomeRing(void)
{
	this->DeleteFrom(&MiscList);

	SModel *pModel = this->field_100;
	i16 *pSaved = this->field_F8;
	SVECTOR *pVert = reinterpret_cast<SVECTOR*>(reinterpret_cast<u8*>(pModel) + 28);

	i32 i = 0;

	if (pModel->NumVertices != 0)
	{
		do
		{
			pVert->vx = pSaved[0];
			pVert->vy = pSaved[1];
			pVert->vz = pSaved[2];

			pSaved += 3;
			pVert++;
			i++;
		}
		while (i < this->field_100->NumVertices);
	}

	Mem_Delete(this->field_F8);
	Mem_Delete(this->field_FC);

	print_if_false(*gpCurrentDomeRing == this, "Eh?");

	*gpCurrentDomeRing = 0;
}

// @Ok
// @AlmostMatching: vector assignment is different, this one doesn't use esi either
i32 Web_GetGroundY(const CVector* a1)
{

	G_LINE_INFO.StartCoords = *a1;

	G_LINE_INFO.EndCoords.vx = a1->vx;
	G_LINE_INFO.EndCoords.vy = a1->vy + 0x1388000;
	G_LINE_INFO.EndCoords.vz = a1->vz;

	M3dColij_InitLineInfo(&G_LINE_INFO);
	M3dZone_LineToItem(&G_LINE_INFO, 1);

	if (!G_LINE_INFO.pItem)
		return G_LINE_INFO.EndCoords.vy;

	return G_LINE_INFO.Position.vy;
}

// @Ok
// 0x4F8190, 256 bytes. One shot of the imaginary web "projector" that walks
// around the trapped baddy: it fires a ray straight in at the baddy's
// vertical axis, from a point field_424 out at angle field_428 and field_420
// units up. Web_CollideWithSuper turns a hit into the part/offset pair in
// field_48[HookIndex]; the projector's height at that moment is kept in
// field_2D0[HookIndex] so AddAnotherStrand can measure the vertical gap
// between two hooks later. Returns 1 on a hit, 0 on a miss.
i32 CTrapWebEffect::CalcHook(i32 HookIndex)
{
	print_if_false(HookIndex <= 80, "Bad hook index");

	CSuper *pSuper = reinterpret_cast<CSuper*>(Mem_RecoverPointer(&this->field_3C));
	print_if_false(pSuper != 0, "pSuper NULL??");

	i32 Idx = (this->field_428 & 0xFFF) * 2;

	CVector Start;
	Start.vx = pSuper->mPos.vx + word_610C48[Idx + 1] * this->field_424;
	Start.vy = pSuper->mPos.vy + (this->field_420 << 12);
	Start.vz = pSuper->mPos.vz + word_610C48[Idx] * this->field_424;

	CVector End;
	End.vx = pSuper->mPos.vx;
	End.vy = pSuper->mPos.vy + (this->field_420 << 12);
	End.vz = pSuper->mPos.vz;

	if (!Web_CollideWithSuper(pSuper, &Start, &End, &this->field_48[HookIndex], 0x1000))
		return 0;

	this->field_2D0[HookIndex] = static_cast<i16>(this->field_420);

	return 1;
}

// @Ok
// 0x4F82A0, 285 bytes. Moves the projector one frame: up or down by
// field_41F, and a sixteenth of a turn around the baddy. Every time the
// field_41E countdown runs out it picks a new target height (a random spot
// on the baddy, in the baddy's own field_13E/field_13F units) and a new
// speed that gets there in six frames.
//
// Original defect kept: with any mType other than 0 or 1 the target height
// is never assigned (the original just leaves whatever was in the register),
// so the speed below is computed from garbage. Nothing can reach it, the
// constructor rejects those types with the same assert.
void CTrapWebEffect::MoveProjector(void)
{
	this->field_420 += this->field_41F;

	if (this->field_41E != 0)
		this->field_41E--;

	if (this->field_41E == 0)
	{
		CSuper *pSuper = reinterpret_cast<CSuper*>(Mem_RecoverPointer(&this->field_3C));
		print_if_false(pSuper != 0, "pSuper NULL??");

		i32 Target;

		if (this->mType == 0)
		{
			Target = Rnd(pSuper->field_13F + pSuper->field_13E) - pSuper->field_13E;
		}
		else if (this->mType == 1)
		{
			Target = Rnd(pSuper->field_13E * 2 / 3) - pSuper->field_13E / 3;
		}
		else
		{
			print_if_false(0, "Bad CTrapWebEffect type");
			Target = 0;
		}

		this->field_41E = 6;
		this->field_41F = static_cast<i8>((Target - this->field_420) / 6);
	}

	this->field_428 = (this->field_428 + 0x100) & 0xFFF;
}

// @Ok
// 0x4F7F40, 429 bytes. Hangs a new trap web on a baddy: takes a handle on
// it, hangs a handle on itself off the baddy (field_104 for a normal trap
// web, field_10C for the yank web), and sets the web "projector" up (see
// MoveProjector / CalcHook above) with a 400 unit radius and a first target
// height picked from the baddy's own size byte.
//
// The 81-entry zeroing loop over field_48 in the original is just the
// implicit construction of the SHook array member (CSVector's default
// constructor zeroes its three i16s, which is exactly the loop body), so it
// is not written out here. It is also what pins the array's length, and so
// the whole tail of the class: see the layout in web.h.
//
// Everything the constructor does not write (field_2D0, field_374,
// field_378, field_420, field_428) is left zero by CBit::operator new, which
// zeroes the whole allocation.
//
// Original defects kept: with a Type other than 0 or 1 the assert fires but
// the object is still built, with an uninitialised target height (the
// original just uses whatever was in the register, here 0); and the
// CGPolyLine is used without a null check, so an out-of-memory
// CBit::operator new dereferences null right after.
CTrapWebEffect::CTrapWebEffect(CSuper *pSuper, i32 Type)
{
	print_if_false(pSuper != 0, "NULL pBaddy sent to CTrapWebEffect");

	this->field_3C = Mem_MakeHandle(pSuper);
	this->mType = static_cast<u8>(Type);

	if (this->mType == 0)
	{
		print_if_false(Mem_RecoverPointer(&pSuper->field_104) == 0, "Baddy already has a web");
		pSuper->field_104 = Mem_MakeHandle(this);
	}
	else if (this->mType == 1)
	{
		print_if_false(Mem_RecoverPointer(&pSuper->field_10C) == 0, "Baddy already has a web");
		pSuper->field_10C = Mem_MakeHandle(this);
	}
	else
	{
		print_if_false(0, "Bad CTrapWebEffect type");
	}

	this->field_424 = 400;

	i32 Target;

	if (this->mType == 0)
	{
		Target = -(Rnd(pSuper->field_13E >> 1) + (pSuper->field_13E >> 1));
	}
	else if (this->mType == 1)
	{
		Target = pSuper->field_13E / -3;
	}
	else
	{
		print_if_false(0, "Bad CTrapWebEffect type");
		Target = 0;
	}

	this->field_41E = 6;
	this->field_41F = static_cast<i8>(Target / 6);

	this->field_44 = new CGPolyLine(80);
	this->field_44->SetSemiTransparent();
	this->field_44->mNumSegs = 0;
}

// @Ok
// 0x4F83C0, 435 bytes. One frame's worth of webbing: move the projector on,
// try to land one more hook on the baddy, and every so often join three
// hooks up with a triangle of webbing.
//
// The hooks live in the field_44 line's segments, so its mNumSegs doubles as
// the hook count, and 80 is the limit. Hook 0 is laid down on its own the
// very first time (nothing has been hooked yet, so there is no hook to move
// away from), which is why the SHook array is 81 long and not 80.
//
// A strand is only added one time in four, only once there are at least two
// hooks, and only up to 20 strands. Its three corners are the newest hook,
// the one two before it, and whichever earlier hook sits at the biggest
// height difference from the newest one (the projector's height at the time
// each hook was made is what field_2D0 keeps). CTrapWebEffect::Move
// (0x4F8860) turns that index triple into the quad's corners every frame.
void CTrapWebEffect::AddAnotherStrand(void)
{
	print_if_false(this->field_44->mNumSegs <= 80, "Bad mNumSegs");

	if (this->field_44->mNumSegs == 80)
		return;

	this->MoveProjector();

	if (this->field_44->mNumSegs == 0)
	{
		if (!this->CalcHook(0))
			return;

		this->MoveProjector();
	}

	if (!this->CalcHook(this->field_44->mNumSegs + 1))
		return;

	this->field_44->mNumSegs++;

	if (this->field_44->mNumSegs < 2)
		return;

	if (Rnd(4) != 0)
		return;

	if (this->field_374 >= 20)
		return;

	i32 Newest = this->field_44->mNumSegs;
	i32 Widest = Newest - 1;
	i32 Biggest = 0;

	for (i32 i = 0; i < Newest - 2; i++)
	{
		i32 Gap = this->field_2D0[Newest] - this->field_2D0[i];

		if (Gap < 0)
			Gap = -Gap;

		if (Gap > Biggest)
		{
			Biggest = Gap;
			Widest = i;
		}
	}

	this->field_378[this->field_374].mHookA = static_cast<u8>(Newest);
	this->field_378[this->field_374].mHookB = static_cast<u8>(Newest - 2);
	this->field_378[this->field_374].mHookC = static_cast<u8>(Widest);

	CQuadBit *pBit = new CQuadBit();

	pBit->SetTexture(0x35023093);
	pBit->SetSemiTransparent();

	this->field_378[this->field_374].mpBit = pBit;
	this->field_374++;
}

// @Ok
// Reverse engineered 2026-08-31 from the original disasm at 0x4F8600
// (~570 bytes, SEH-protected the same way the CLAUDE.md "new T(...) SEH
// frame" family is: this session did not try to reproduce that frame,
// session rule is functional decomp only). Blockers noted by the previous
// session are both resolved this session:
//  - M3dUtils_GetDynamicHookPosition is @Ok (done earlier the same day).
//  - CWebFrag is now declared/implemented (web.h), reverse engineered from
//    its constructor at 0x4FA080 (see the class comment in web.h and
//    CWebFrag::CWebFrag below for the field-by-field evidence).
// Field roles confirmed this session:
//  - this->field_44 points at an unnamed, unmapped structure (never
//    constructed anywhere in this repo, so its own layout beyond these
//    three offsets is out of scope): a hook count at +0x3C, a VECTOR*
//    pointer at +0x40 into an inline VECTOR[] (16-byte stride) starting at
//    +0x44. Confirmed by raw pointer arithmetic in the disasm, not
//    guessed: the loop's byte-offset accumulator advances by 0x10 (one
//    VECTOR) per hook and is added to the pointer read from +0x40.
//  - this->field_48 (SHook[122], see web.h) is fed one entry at a time as
//    the pHook argument; the count comes from the field_44 target.
//  - The "ground height" is exactly Web_GetGroundY's gLineInfo pattern
//    inlined in the original; called Web_GetGroundY directly here since
//    the two are functionally identical (session goal is functional
//    correctness, not matching MSVC6's inlining choice byte-for-byte).
//  - this->mType (inherited CBit::mType, tested at offset 0x3B in the
//    disasm) selects which CSuper attachment slot gets cleared to a
//    Mem_MakeHandle(NULL): field_104 for mType == 0, field_10C for
//    mType == 1 (both already declared on CSuper in ob.h, and both are
//    already used elsewhere in the repo, e.g. carnage.cpp, to recover a
//    CTrapWebEffect* back out via Mem_RecoverPointer). Any other mType
//    prints "Bad CTrapWebEffect type", matching the original's assert.
void CTrapWebEffect::Burst(void)
{
	CSuper *pSuper = reinterpret_cast<CSuper*>(Mem_RecoverPointer(&this->field_3C));
	print_if_false(pSuper != NULL, "pSuper NULL??");

	if (pSuper != NULL)
	{
		CGPolyLine *pLine = this->field_44;
		i32 HookCount = pLine->mNumSegs;

		if (HookCount != 0)
		{
			M3d_BuildTransform(pSuper);

			// one hook position per line segment; SLineSeg is 16 bytes with
			// its CVector first, so a VECTOR* walks it with the same stride
			// the original uses
			VECTOR *pPositions = reinterpret_cast<VECTOR*>(pLine->mSegs);
			i32 i;

			// index 0 is always computed once, unconditionally, before the
			// count-gated loop below (matches the disasm exactly: the
			// pre-loop call uses field_48[0], the loop's first pass then
			// re-writes positions[0] using field_48[1]).
			M3dUtils_GetDynamicHookPosition(&pPositions[0], pSuper, &this->field_48[0]);

			for (i = 0; i < HookCount; i++)
			{
				M3dUtils_GetDynamicHookPosition(&pPositions[i], pSuper, &this->field_48[i + 1]);
			}

			i32 GroundY = Web_GetGroundY(&pSuper->mPos);

			CVector Prev(pPositions[0].vx, pPositions[0].vy, pPositions[0].vz);

			for (i = 0; i < HookCount - 1; i += 2)
			{
				CVector HookA(pPositions[i].vx, pPositions[i].vy, pPositions[i].vz);
				CVector HookB(pPositions[i + 1].vx, pPositions[i + 1].vy, pPositions[i + 1].vz);

				new CWebFrag(GroundY, Prev, HookA, HookB, pSuper->mPos, 25, 0);

				Prev = HookB;
			}
		}

		if (this->field_418)
			pSuper->mFlags &= ~0x400;

		if (this->mType == 0)
		{
			pSuper->field_104 = Mem_MakeHandle(NULL);
		}
		else if (this->mType == 1)
		{
			pSuper->field_10C = Mem_MakeHandle(NULL);
		}
		else
		{
			print_if_false(0, "Bad CTrapWebEffect type");
		}
	}

	this->Die();
}

// @Ok
// See the class comment in web.h for the full field derivation. Order of
// operations here follows the disasm: base CGLine::CGLine() runs first
// (implicit), then the 9 new dwords (field_5C/68/74) are zeroed, then
// mCodeBGR0 gets its flag bit, mGroundY/mStart/mEnd are set, the second
// CGLine (field_84) is allocated and given the same flag bit plus
// mProtected = 1, then the direction/length/scale computation, then the
// three-way copy plus optional jitter, then the random variant byte.
CWebFrag::CWebFrag(
		i32 GroundY,
		const CVector &PrevPos,
		const CVector &HookA,
		const CVector &HookB,
		const CVector &SuperPos,
		i32 Speed,
		i32 Jitter)
{
	this->field_5C.vx = 0;
	this->field_5C.vy = 0;
	this->field_5C.vz = 0;

	this->field_68.vx = 0;
	this->field_68.vy = 0;
	this->field_68.vz = 0;

	this->field_74.vx = 0;
	this->field_74.vy = 0;
	this->field_74.vz = 0;

	this->mCodeBGR0 |= 0x2000000;

	this->mGroundY = GroundY;

	this->mStart = HookA;
	this->mEnd = PrevPos;

	this->field_84 = new CGLine();

	this->field_84->mCodeBGR0 |= 0x2000000;
	this->field_84->mProtected = 1;

	this->field_84->mStart = HookA;
	this->field_84->mEnd = HookB;

	CVector Dir = HookA - SuperPos;
	i32 Length = Dir.Length();

	if (Length != 0)
	{
		this->field_68 = Dir * Speed / Length;
	}

	this->field_5C = this->field_68;
	this->field_74 = this->field_68;

	if (Jitter == 0)
	{
		this->field_5C.vy += (Rnd(21) - 10) << 12;
		this->field_68.vy += (Rnd(21) - 10) << 12;
		this->field_74.vy += (Rnd(21) - 10) << 12;
	}

	if (Rnd(3) != 0)
		this->field_88 = Rnd(3) + 6;
	else
		this->field_88 = Rnd(3) + 1;
}

// @Ok
// Original 0x4F9060.
CKnottedWeb::~CKnottedWeb(void)
{
	delete this->mpInnerLine;
	for (i32 i = 0; i < this->mNumSegs; i++)
		delete this->mpExtraSegs[i].mpBit;
	Mem_Delete(this->mpExtraSegs);
}

// @Ok
// Original 0x4F71E0, vtable slot 4.
void CSwinger::DeleteStuff(void)
{
	if (this->mpLine)
		delete this->mpLine;
	this->mpLine = 0;
}

// @Ok
// Original 0x4F7200.
CSwinger::~CSwinger(void)
{
	this->DeleteStuff();
}

// @Ok
// Original 0x4F6F00. The third argument is a full 32-byte MATRIX.
CSwinger::CSwinger(CVector* anchor, i32 length, const MATRIX* basis, CVector* direction)
{
	this->field_7C = G_TIMER_RELATED;
	this->mLength = length;
	this->mAnchor = *anchor;
	this->mBaseMatrix = *basis;
	this->mForward.vx = this->mBaseMatrix.m[0][2];
	this->mForward.vy = this->mBaseMatrix.m[1][2];
	this->mForward.vz = this->mBaseMatrix.m[2][2];
	this->mSide.vx = this->mBaseMatrix.m[0][0];
	this->mSide.vy = this->mBaseMatrix.m[1][0];
	this->mSide.vz = this->mBaseMatrix.m[2][0];
	this->mDown.vx = -this->mBaseMatrix.m[0][1];
	this->mDown.vy = -this->mBaseMatrix.m[1][1];
	this->mDown.vz = -this->mBaseMatrix.m[2][1];
	CVector end = this->mAnchor - (this->mLength * this->mDown);
	CKnottedWeb* line = new CKnottedWeb(this->mAnchor, end);
	this->mpLine = line;
	line->SetStartAndEnd(&this->mAnchor, &end);
	Utils_CalcUnitFacingCamera(&this->mAnchor, &end, reinterpret_cast<CVector*>(&line->field_58));
	for (i32 i = 0; i < line->mNumSegs; i++)
		line->mpExtraSegs[i].mPos = line->mSegs[i].End;
	line->field_6D = 1;
	line->field_74 = this->field_F8;
	i32 dot = ((this->mDown.vx * direction->vx) >> 12)
		+ ((this->mDown.vy * direction->vy) >> 12)
		+ ((this->mDown.vz * direction->vz) >> 12);
	this->mAngles.vx = Utils_ArcCos(dot < 0 ? -dot : dot);
	this->field_180 = 2048;
	this->mPhaseSpeed = (120 - M3dMaths_SquareRoot0(this->mLength)) / 2;
	this->field_18C = this->mLength / 128;
	if (this->field_18C < 3)
		this->field_18C = 3;
	this->field_18C = 9;
	this->field_188 = this->mLength / 8;
}

// @Ok
// Original 0x4F7310, vtable slot 2.
void CSwinger::AI(void)
{
	CKnottedWeb* line = static_cast<CKnottedWeb*>(this->mpLine);
	if (this->field_F8 && line)
		line->field_74 = 1;
	this->field_180 += (G_TIMER_RELATED - this->field_7C) * this->mPhaseSpeed;
	this->field_7C = G_TIMER_RELATED;
	SVECTOR angles;
	angles.vx = (this->mAngles.vx * (G_RCOSSIN_TBL[this->field_180 & 0xFFF].cos + 4096)) >> 12;
	angles.vy = 0;
	angles.vz = this->mAngles.vz;
	MATRIX rotation;
	M3dMaths_RotMatrixYXZ(&angles, &rotation);
	this->mSwingMatrix = this->mBaseMatrix;
	MulMatrix(&this->mSwingMatrix, &rotation);
	this->mForward.vx = this->mSwingMatrix.m[0][2];
	this->mForward.vy = this->mSwingMatrix.m[1][2];
	this->mForward.vz = this->mSwingMatrix.m[2][2];
	this->mSide.vx = this->mSwingMatrix.m[0][0];
	this->mSide.vy = this->mSwingMatrix.m[1][0];
	this->mSide.vz = this->mSwingMatrix.m[2][0];
	this->mDown.vx = -this->mSwingMatrix.m[0][1];
	this->mDown.vy = -this->mSwingMatrix.m[1][1];
	this->mDown.vz = -this->mSwingMatrix.m[2][1];
	CVector end = this->mAnchor - (this->mLength * this->mDown);
	line->SetStartAndEnd(&this->mAnchor, &end);
	Utils_CalcUnitFacingCamera(&this->mAnchor, &end, reinterpret_cast<CVector*>(&line->field_58));
	for (i32 i = 0; i < line->mNumSegs; i++)
		line->mpExtraSegs[i].mPos = line->mSegs[i].End;
}

// @Ok
// @Matching
// Original 0x4F74B0.
void CSwinger::SetRenderEnd(CVector& end)
{
	this->field_FC = end - this->mpLine->mSegs[this->mpLine->mNumSegs - 1].End;
	CVector* anchor = &this->mAnchor;
	CKnottedWeb* line = static_cast<CKnottedWeb*>(this->mpLine);
	line->SetStartAndEnd(anchor, &end);
	Utils_CalcUnitFacingCamera(anchor, &end, reinterpret_cast<CVector*>(&line->field_58));
	i32* extra = reinterpret_cast<i32*>(line->mpExtraSegs);
	i32* seg = reinterpret_cast<i32*>(line->mSegs);
	for (i32 i = 0; i < line->mNumSegs; extra += 7, seg += 4, i++)
	{
		extra[0] = seg[0];
		extra[1] = seg[1];
		extra[2] = seg[2];
	}
}

// @Ok
int CSwinger::IsOneTimeToDie(void)
{
	return this->field_180 >= 4096;
}

// @Ok
// @Matching
void CSwinger::SetSpideyAnimFrame(i32 a2)
{
	G_MECHLIST_PLAYER->mFrame = (a2 * (this->field_180 - 2048)) >> 11;
}


// @Ok
INLINE void CDomeShockWave::ResetHitFlags(CBody* body)
{
	for(CBody *cur = body; cur; cur = reinterpret_cast<CBody*>(cur->mNextItem))
		cur->mCBodyFlags &= 0xFEFF;
}

void validate_CKnottedWebSplat(void){
	VALIDATE_SIZE(CKnottedWebSplat, 0xB0);

	VALIDATE(CKnottedWebSplat, field_84, 0x84);
	VALIDATE(CKnottedWebSplat, field_88, 0x88);
	VALIDATE(CKnottedWebSplat, field_8C, 0x8C);
	VALIDATE(CKnottedWebSplat, field_98, 0x98);
	VALIDATE(CKnottedWebSplat, field_A4, 0xA4);
}

void validate_CImpactWeb(void){
	VALIDATE_SIZE(CImpactWeb, 0x8C);
}

void validate_CDomePiece(void){
	VALIDATE_SIZE(CDomePiece, 0x100);

	VALIDATE(CDomePiece, field_F4, 0xF4);
	VALIDATE(CDomePiece, field_F8, 0xF8);
	VALIDATE(CDomePiece, field_FC, 0xFC);
}

void validate_CDome(void){
	VALIDATE_SIZE(CDome, 0x11C);

	VALIDATE(CDome, hPlayer, 0xF8);
	VALIDATE(CDome, field_100, 0x100);

	VALIDATE(CDome, field_104, 0x104);
	VALIDATE(CDome, field_108, 0x108);
	VALIDATE(CDome, field_10C, 0x10C);
	VALIDATE(CDome, field_110, 0x110);
	VALIDATE(CDome, field_114, 0x114);
	VALIDATE(CDome, field_118, 0x118);
}

void validate_CDomeRing(void){
	VALIDATE_SIZE(CDomeRing, 0x110);

	VALIDATE(CDomeRing, field_F8, 0xF8);
	VALIDATE(CDomeRing, field_FC, 0xFC);
	VALIDATE(CDomeRing, field_100, 0x100);
	VALIDATE(CDomeRing, field_104, 0x104);
	VALIDATE(CDomeRing, field_108, 0x108);
}

void validate_CWeb(void){
	VALIDATE_SIZE(CKnottedWebFrag, 0x98);
	VALIDATE(CKnottedWebFrag, mEndVelocity, 0x78);
	VALIDATE(CKnottedWebFrag, mEndPos, 0x84);
	VALIDATE(CKnottedWebFrag, mGroundY, 0x90);
	VALIDATE(CKnottedWebFrag, mRestDistance, 0x94);

	VALIDATE_SIZE(CWeb, 0x13C);


	VALIDATE(CWeb, field_F8, 0xF8);

	VALIDATE(CWeb, field_100, 0x100);
	VALIDATE(CWeb, field_102, 0x102);
	VALIDATE(CWeb, field_104, 0x104);

	VALIDATE(CWeb, field_108, 0x108);
	VALIDATE(CWeb, field_114, 0x114);

	VALIDATE(CWeb, field_120, 0x120);
	VALIDATE(CWeb, field_124, 0x124);
	VALIDATE(CWeb, field_128, 0x128);

	VALIDATE(CWeb, field_12C, 0x12C);

	VALIDATE(CWeb, field_130, 0x130);


	VALIDATE(CWeb, field_134, 0x134);
	VALIDATE(CWeb, field_138, 0x138);
}

void validate_CSwinger(void){
	VALIDATE_SIZE(CSwinger, 0x190);

	VALIDATE(CSwinger, field_F8, 0xF8);
	VALIDATE(CSwinger, field_FC, 0xFC);
	VALIDATE(CSwinger, mpLine, 0x17C);
	VALIDATE(CSwinger, field_180, 0x180);
}

void validate_CTrapWebEffect(void)
{
	VALIDATE_SIZE(CTrapWebEffect, 0x430);

	VALIDATE(CTrapWebEffect, field_3C, 0x3C);
	VALIDATE(CTrapWebEffect, field_44, 0x44);
	VALIDATE(CTrapWebEffect, field_48, 0x48);
	VALIDATE(CTrapWebEffect, field_2D0, 0x2D0);
	VALIDATE(CTrapWebEffect, field_374, 0x374);
	VALIDATE(CTrapWebEffect, field_378, 0x378);
	VALIDATE(CTrapWebEffect, field_418, 0x418);
	VALIDATE(CTrapWebEffect, field_41A, 0x41A);
	VALIDATE(CTrapWebEffect, field_41C, 0x41C);
	VALIDATE(CTrapWebEffect, field_41E, 0x41E);
	VALIDATE(CTrapWebEffect, field_41F, 0x41F);
	VALIDATE(CTrapWebEffect, field_420, 0x420);
	VALIDATE(CTrapWebEffect, field_424, 0x424);
	VALIDATE(CTrapWebEffect, field_428, 0x428);
	VALIDATE(CTrapWebEffect, field_42C, 0x42C);
}

void validate_CDomeShockWave(void)
{
	VALIDATE_SIZE(CDomeShockWave, 0x98);

	VALIDATE(CDomeShockWave, field_44, 0x44);
	VALIDATE(CDomeShockWave, field_50, 0x50);
	VALIDATE(CDomeShockWave, field_90, 0x90);
}

void validate_CWebFrag(void)
{
	VALIDATE_SIZE(CWebFrag, 0x8C);

	VALIDATE(CWebFrag, field_5C, 0x5C);
	VALIDATE(CWebFrag, field_68, 0x68);
	VALIDATE(CWebFrag, field_74, 0x74);
	VALIDATE(CWebFrag, mGroundY, 0x80);
	VALIDATE(CWebFrag, field_84, 0x84);
	VALIDATE(CWebFrag, field_88, 0x88);
}

// Global flag CSwinger_SwingBack only READS, never writes. Evidence: the exact same
// "test eax,eax; jnz <skip>" guard idiom, reading this SAME address (0x60D224), also appears
// verbatim (checked via IDA xrefs) in several other, apparently unrelated SEH-wrapped
// functions elsewhere in the binary (0x42FFC0, 0x43C150, 0x43C330, 0x43C3E0, 0x43C430,
// 0x457E00, 0x457EE0, 0x458050, 0x458210, 0x4582D0, 0x48C0D0, 0x48C730, 0x4F9BD0, 0x4FAF40),
// none of which write to it in the code inspected either. Whatever writes it to nonzero is
// outside every function traced this session. Named for its observed role in THIS function
// only (a "run the block below once" gate); its true system-wide purpose is not confirmed.
static volatile i32 * const gWebSwingBackOnceFlag = (i32*)0x0060D224;

// Vtable of an unidentified CKnottedWeb-derived class. CSwinger_SwingBack (0x4F7550) builds a
// plain CKnottedWeb in freshly allocated memory, then overwrites its vtable pointer with this
// address right after construction returns -- the same "construct as base, then poke the
// vtable to the real derived type" idiom already used for box->vtable in
// PShell_EndTrainingInit (pshell.cpp). No name found in idb_globals.txt, spideypc_names.txt,
// or spiderman_names.txt (Mac build). The two extra bytes of state this subclass reads/writes
// beyond a plain CKnottedWeb (field_6D, field_74) are declared directly on CKnottedWeb itself
// in bit2.h, since they sit inside CKnottedWeb's own already-validated 0x78-byte footprint,
// in bytes CKnottedWeb's own constructor never initializes.
static void * const gSwingBackWebVtable = (void*)0x0053C750;

// Plain non-throwing placement new, same convention as front.cpp's copy (the original builds
// this object via a raw CBit::operator new call plus a manual constructor call, not a plain
// `new CKnottedWeb(...)` expression, which would allocate sizeof(CKnottedWeb)=0x78 bytes
// instead of the 0x7C the disasm actually pushes -- see CSwinger_SwingBack below).
// @Bogus
inline void* operator new(size_t, void* location)
{
	return location;
}

// @Ok
// 2026-08-31: functional decompile (session bar). Address 0x4F7550; Mac mangled name
// (idbs/spiderman_names.txt) confirms "CSwinger::SwingBack". Despite the name this is not a
// simple detach/reset: gated by gWebSwingBackOnceFlag (see its own comment), it builds one
// CKnottedWeb-shaped "snapping web" visual effect object spanning from the swing line's start
// point to a point derived from the line's last segment end minus a1->field_FC, then
// reclassifies it (manual vtable poke to gSwingBackWebVtable, see its own comment) and copies
// a1->field_F8 into the resulting object's field_74 (CKnottedWeb, bit2.h). Every field access
// and call argument order below was cross-checked instruction-by-instruction against the
// 0x4F7550 disasm (operator- and Utils_CalcUnitFacingCamera's argument orders in particular,
// since both return/write through a hidden pointer).
// Called by CPlayer::CheckJumpingSmashKick to release the held web-swinging object.
void CSwinger_SwingBack(CSwinger *a1)
{
	// Original: nullsub_1(a1->mpLine != 0, "No line?"). nullsub_1 (0x4015B0) is the
	// confirmed-empty print_if_false stub this whole repo already omits at call sites (e.g.
	// ps2m3d.cpp), so only the semantic assertion is preserved here as a comment.

	if (*gWebSwingBackOnceFlag == 0)
	{
		CKnottedWeb *pWeb = (CKnottedWeb*)CBit::operator new(0x7C);

		if (pWeb)
		{
			CGPolyLine *pLine = a1->mpLine;
			SLineSeg *pLastSeg = &pLine->mSegs[pLine->mNumSegs - 1];

			CVector relEnd = pLastSeg->End - a1->field_FC;

#ifdef SPIDEY_STANDALONE
			::new (pWeb) CSwingBack(pLine->mStart, relEnd);
#else
			::new (pWeb) CKnottedWeb(pLine->mStart, relEnd);
			*reinterpret_cast<void**>(pWeb) = gSwingBackWebVtable;
#endif

			pWeb->SetStartAndEnd(&pLine->mStart, &relEnd);

			Utils_CalcUnitFacingCamera(
					&pLine->mStart,
					&relEnd,
					reinterpret_cast<CVector*>(&pWeb->field_58));

			for (i32 i = 0; i < pWeb->mNumSegs; i++)
			{
				pWeb->mpExtraSegs[i].mPos = pWeb->mSegs[i].End;
			}

			pWeb->field_6D = 1;

			pWeb->SetSemiTransparent();
			pWeb->mpInnerLine->SetSemiTransparent();
		}

		// Original writes this unconditionally, even down the "operator new returned null"
		// path (where it would null-deref) -- a genuine defect in the original if
		// CBit::operator new ever really returns 0 here; reproduced as-is per this repo's
		// "reproduce the source-level bug, don't fix it" convention (CLAUDE.md).
		pWeb->field_74 = a1->field_F8;
	}
}
// @NotOk
// Original 0x4F6290. Give each endpoint its speed and find the ground below.
// Six instruction diffs remain in the ray setup and hit selection.
CKnottedWebFrag::CKnottedWebFrag(const CVector &start, const CVector &end,
	const CVector &startVelocity, const CVector &endVelocity)
	: CKnottedWeb(start, end)
{
	this->mPos = start;
	this->mEndPos = end;
	this->mVel = startVelocity;
	this->mEndVelocity = endVelocity;

	const CVector &middle = (start + end) >> 1;
	i32 x = middle.vx;
	i32 y = middle.vy;
	i32 z = middle.vz;
	G_LINE_INFO.StartCoords.vy = y;
	y += 0x1388000;
	G_LINE_INFO.StartCoords.vx = x;
	G_LINE_INFO.StartCoords.vz = z;
	G_LINE_INFO.EndCoords.vx = x;
	G_LINE_INFO.EndCoords.vy = y;
	G_LINE_INFO.EndCoords.vz = z;
	M3dColij_InitLineInfo(&G_LINE_INFO);
	M3dZone_LineToItem(&G_LINE_INFO, 1);
	i32 ground = G_LINE_INFO.Position.vy;
	if (!G_LINE_INFO.pItem)
		ground = G_LINE_INFO.EndCoords.vy;
	this->mGroundY = ground;
	this->field_6E = 1;
	this->field_70 = 2;
	this->mRestDistance = Utils_Dist(this->mPos, this->mEndPos) >> 3;
}

// @Ok
// @Matching
// Original 0x4F64B0. Move both ends and ease the broken strand together.
void CKnottedWebFrag::Move(void)
{
	this->mPos += this->mVel;
	this->mVel.vy += 29584;
	if (this->mPos.vy > this->mGroundY)
	{
		this->mPos.vy = this->mGroundY;
		this->mVel.vy = 0;
		this->mVel.vx >>= 1;
		this->mVel.vz >>= 1;
	}
	this->mEndPos += this->mEndVelocity;
	this->mEndVelocity.vy += 29584;
	if (this->mEndPos.vy > this->mGroundY)
	{
		this->mEndPos.vy = this->mGroundY;
		this->mEndVelocity.vy = 0;
		this->mEndVelocity.vx >>= 1;
		this->mEndVelocity.vz >>= 1;
	}
	i32 distance = Utils_Dist(this->mPos, this->mEndPos);
	CVector middle = (this->mPos + this->mEndPos) >> 1;
	// This scalar overload reads only the first word of its left vector.
	CVector correction = ((middle +
		(*reinterpret_cast<const CVector*>(&this->mRestDistance) * (this->mPos - middle)) / distance)
		- this->mPos) >> 4;
	this->mPos += correction;
	this->mEndPos -= correction;
	this->SetStartAndEnd(&this->mPos, &this->mEndPos);
	Utils_CalcUnitFacingCamera(&this->mPos, &this->mEndPos,
		reinterpret_cast<CVector*>(&this->field_58));
	i32 *extra = reinterpret_cast<i32*>(this->mpExtraSegs);
	i32 *seg = reinterpret_cast<i32*>(this->mSegs);
	for (i32 i = 0; i < this->mNumSegs; extra += 7, seg += 4, i++)
	{
		extra[0] = seg[0];
		extra[1] = seg[1];
		extra[2] = seg[2];
	}
	this->CKnottedWeb::Move();
	if (++this->mAge > 30)
		this->Die();
}

// Turns a live web into a blob: mode field_104 goes to 3 and, unless the
// attached item is type 401, bit 3 of the attached item's flag word at 0x2A8
// is cleared (the "web attached" flag on the target, going by the fact that
// this is the only thing switching to blob mode does to it). Called by
// CPlayer::Hit (0x4BD890) and CPlayer::SwitchToDeathMode when the player
// loses a web mid-flight. Original address 0x4F6260.
// @Ok
void CWeb::SwitchToBlob(void)
{
	this->field_104 = 3;

	CItem *pTarget = reinterpret_cast<CItem*>(
			Mem_RecoverPointer(reinterpret_cast<SHandle*>(&this->field_134)));

	if (pTarget == 0)
		return;

	if (pTarget->mType == 401)
		return;

	// offset 0x2A8 of the attached item. Which concrete class owns it is not
	// known yet (CWeb only ever sees it through this handle), so it stays a
	// raw offset rather than a guessed field name.
	*reinterpret_cast<i32*>(reinterpret_cast<u8*>(pTarget) + 0x2A8) &= ~8;
}

// @Ok
// @Matching
// Original 0x4F5E50. Unlink before freeing the web's effects.
CWeb::~CWeb(void)
{
	this->DeleteFrom(&G_WEB_LIST);
	delete this->field_12C;
	delete reinterpret_cast<CChain*>(this->field_130);
}

// @Ok
// Original 0x4F6C10. Update a loose web, then retire its line and chain.
void CWeb::AI(void)
{
	this->EveryFrame();
	if (this->field_104 == 2)
	{
		this->field_100 += static_cast<u16>(this->field_80);
		if (this->field_100 > 64)
		{
			this->field_104 = 4;
			delete this->field_12C;
			this->field_12C = 0;
			delete reinterpret_cast<CChain*>(this->field_130);
			this->field_130 = 0;
		}
		else
		{
			CBody* target = static_cast<CBody*>(Mem_RecoverPointer(
				reinterpret_cast<SHandle*>(&this->field_134)));
			if (target)
			{
				CVector pos;
				if (target->mType == 401)
					static_cast<CManipOb*>(target)->GetAttachPoint(&pos);
				else
					pos = target->mPos;
				CChain* chain = reinterpret_cast<CChain*>(this->field_130);
				print_if_false(chain != 0, "No chain?");
				chain->Move(&pos);
				print_if_false(this->field_12C != 0, "No line?");
				this->field_12C->mStart = chain->field_4[0].field_0;
				SChainData* point = &chain->field_4[1];
				for (i32 i = 0; i < this->field_12C->mNumSegs; i++, point++)
					this->field_12C->mSegs[i].End = point->field_0;
			}
		}
	}
	else if (this->field_104 == 3)
	{
		this->field_100 += static_cast<u16>(this->field_80);
		if (this->field_100 > 16)
		{
			this->field_104 = 4;
			delete this->field_12C;
			this->field_12C = 0;
		}
		else
		{
			CVector delta = this->field_114 - this->field_108;
			delta = (delta >> 12) * G_RCOSSIN_TBL[(this->field_100 << 6) & 0xFFF].cos;
			print_if_false(this->field_12C != 0, "No line?");
			CVector start = this->field_114 - delta;
			CKnottedWeb* line = this->field_12C;
			line->SetStartAndEnd(&start, &this->field_114);
			Utils_CalcUnitFacingCamera(&start, &this->field_114, reinterpret_cast<CVector*>(&line->field_58));
			for (i32 i = 0; i < line->mNumSegs; i++)
				line->mpExtraSegs[i].mPos = line->mSegs[i].End;
		}
	}
	else if (this->field_104 == 4)
	{
		this->mCBodyFlags |= 0x40;
	}
}

// @Ok
// @Matching
// Original 0x4F6170. Follow a trapped target's drawn hook when available.
void CWeb::SetFirePos(CVector& pos)
{
	this->field_108 = pos;
	CSuper* target = static_cast<CSuper*>(Mem_RecoverPointer(
		reinterpret_cast<SHandle*>(&this->field_134)));
	if (target)
	{
		CTrapWebEffect* trap = static_cast<CTrapWebEffect*>(Mem_RecoverPointer(&target->field_104));
		if (trap && trap->field_42C)
			this->field_114 = trap->field_44->mSegs[Rnd(trap->field_42C)].End;
	}
	print_if_false(this->field_12C != 0, "No line?");
	CVector* end = &this->field_114;
	CVector* start = &this->field_108;
	CKnottedWeb* line = this->field_12C;
	line->SetStartAndEnd(end, start);
	Utils_CalcUnitFacingCamera(end, start, reinterpret_cast<CVector*>(&line->field_58));
	i32* extra = reinterpret_cast<i32*>(line->mpExtraSegs);
	i32* seg = reinterpret_cast<i32*>(line->mSegs);
	for (i32 i = 0; i < line->mNumSegs; extra += 7, seg += 4, i++)
	{
		extra[0] = seg[0];
		extra[1] = seg[1];
		extra[2] = seg[2];
	}
}

// @Ok
// verified against the IDA disasm of 0x4F5DA0 (136 bytes). Chains
// CBody::CBody, zeroes the two anchor vectors and the three scalars behind
// them, links the new web into G_WEB_LIST and stamps mType 1. Everything else
// (the mode in field_104, the webbing cost in field_102, the hook and
// target handles) is filled in by the caller, CPlayer::CheckJumpingR1ZipWeb
// or CPlayer::CheckJumpingR2ZipWeb.
CWeb::CWeb(void)
{
	this->field_108.vx = 0;
	this->field_108.vy = 0;
	this->field_108.vz = 0;

	this->field_114.vx = 0;
	this->field_114.vy = 0;
	this->field_114.vz = 0;

	this->field_120 = 0;
	this->field_124 = 0;
	this->field_128 = 0;

	this->AttachTo(&G_WEB_LIST);

	this->mType = 1;
}

// @Ok
// 0x4A5E40, 400 bytes. Grows the splat and rebuilds its quad from the centre
// and the two corner offsets the constructor worked out, then fades it away
// once it is older than 30 frames and kills it when nothing is left of the
// tint.
//
// The original scales the corner offsets through the (CVector, CVector)
// operator* overload with a one-int CVector standing in for the left hand
// side (that overload only reads lhs.vx), the same idiom vector.h's
// explicit CVector(i32) constructor already exists for.
void CKnottedWebSplat::Move(void)
{
	this->field_88 += (this->field_84 - this->field_88) >> 1;

	CVector Scale(this->field_88);

	CVector CornerB = Scale * this->field_98;
	CVector CornerC = Scale * this->field_A4;

	this->mPos = this->field_8C - CornerB - CornerC;
	this->mPosB = this->field_8C + CornerB - CornerC;
	this->mPosC = this->field_8C - CornerB + CornerC;
	this->mPosD = this->field_8C + CornerB + CornerC;

	this->mAge++;

	if (this->mAge > 30)
	{
		this->SetSemiTransparent();
		Bit_ReduceRGB(&this->mTint, 5);
	}

	if ((this->mTint & 0xFFFFFF) == 0)
		this->Die();
}

// @Ok
// 0x4F5BC0, 420 bytes. The blob of webbing left where a web shot lands: a
// quad, facing along the surface normal and spun at a random angle, that
// grows to full size over the first few frames (see Move above).
//
// The nine zero stores the original makes over 0x8C..0xAC before it does
// anything else are the implicit construction of the three CVector members,
// so they are not written out here (CVector's default constructor zeroes its
// three components). field_88 is left at zero by CBit::operator new.
//
// shell.cpp has a near-identical sibling splat class at 0x4907F0 (same
// SetTexture / SetSemiTransparent / OrientUsing(..., 1, 1, Rnd(4096)) /
// corner-offset / Move() shape, one field further along), which is a good
// second reading of every step here.
//
// The original works out the offset from the surface with the
// (CVector, CVector) operator* overload and a one-int CVector left hand side
// (that overload only reads lhs.vx), the idiom vector.h's explicit
// CVector(i32) constructor exists for: the splat centre is the hit point
// pushed 10 units back out along the normal.
CKnottedWebSplat::CKnottedWebSplat(const CVector *pPos, const CVector *pNormal)
{
	this->SetTexture(0x3AF6DFF3);
	this->SetSemiTransparent();

	this->field_84 = 32;

	this->field_8C = *pPos;
	this->field_8C += CVector(10) * *pNormal;

	// the caller sign-extended a CSVector into the three i32s of pNormal
	// (see CWeb::Fire below); this reads the low words straight back out
	SVECTOR Normal;
	Normal.vx = static_cast<i16>(pNormal->vx);
	Normal.vy = static_cast<i16>(pNormal->vy);
	Normal.vz = static_cast<i16>(pNormal->vz);

	this->OrientUsing(&this->field_8C, &Normal, 1, 1, Rnd(4096));

	this->field_98 = (this->mPosB - this->mPos) >> 1;
	this->field_A4 = (this->mPosC - this->mPos) >> 1;

	this->Move();

	this->mType = 40;
}

// @Ok
// 0x4F5ED0, 662 bytes. Called by CPlayer::FireWeb. Anchors the web between
// Hook and Target, optionally drops a splat where it landed, and (only the
// first time, while field_12C is still empty) builds the drawn strand: a
// CKnottedWeb whose extra dangling segments start at the base line's own
// segment ends.
//
// The tail branches on which web shot the player is running (CPlayer
// field_8F8: 1 forward, 2 yank), except that a web fired at the player
// himself always counts as the yank shot. The yank path bursts any
// CTrapWebEffect already on the target's field_10C slot and puts a fresh
// Type 1 one on with 16 strands.
//
// Original defects kept: both `new`s are used without a null check (the
// original calls SetTint / SetStartAndEnd straight through the returned
// pointer), and the "mhYankWebContact not gone?" check calls
// Mem_RecoverPointer purely to feed print_if_false, which is a no-op here.
void CWeb::Fire(
		CVector &Hook,
		CVector &Target,
		CBody *pTarget,
		bool bSplat,
		CSVector &Normal)
{
	this->field_108 = Hook;
	this->field_114 = Target;

	if (bSplat)
	{
		// the original sign-extends the CSVector into three i32s here and
		// hands the splat a CVector; the splat's own constructor reads the
		// low word of each i32 back out when it needs the angles again
		CVector SplatNormal(Normal.vx, Normal.vy, Normal.vz);

		CKnottedWebSplat *pSplat = new CKnottedWebSplat(&Target, &SplatNormal);

		if (this->field_F8 != 0)
			pSplat->SetTint(0xFF, 0x7B, 0);
		else
			pSplat->SetTint(0x80, 0x80, 0x80);
	}

	if (this->field_12C == 0)
	{
		*reinterpret_cast<SHandle*>(&this->field_134) = Mem_MakeHandle(pTarget);

		CKnottedWeb *pWeb = new CKnottedWeb(this->field_114, this->field_108);

		this->field_12C = pWeb;

		pWeb->SetStartAndEnd(&this->field_114, &this->field_108);

		Utils_CalcUnitFacingCamera(&this->field_114, &this->field_108,
				reinterpret_cast<CVector*>(&pWeb->field_58));

		SLineSeg *pSeg = pWeb->mSegs;
		SKnottedWebSeg *pExtra = pWeb->mpExtraSegs;

		for (i32 i = 0; i < pWeb->mNumSegs; i++)
		{
			pExtra->mPos.vx = pSeg->End.vx;
			pExtra->mPos.vy = pSeg->End.vy;
			pExtra->mPos.vz = pSeg->End.vz;

			pSeg++;
			pExtra++;
		}

		this->field_12C->field_74 = this->field_F8;

		print_if_false(G_MECHLIST != 0, "No spidey?");

		i32 WebShot;

		if (pTarget == G_MECHLIST)
			WebShot = 2;
		else
			WebShot = reinterpret_cast<CPlayer*>(G_MECHLIST)->field_8F8;

		if (WebShot == 1)
		{
			this->field_12C->field_6C = 1;
		}
		else if (WebShot == 2)
		{
			if (pTarget != 0)
			{
				CSuper *pSuper = reinterpret_cast<CSuper*>(pTarget);

				print_if_false((pSuper->mFlags >> 1) & 1, "Tried to yank a non superitem");

				CTrapWebEffect *pOld = reinterpret_cast<CTrapWebEffect*>(
						Mem_RecoverPointer(&pSuper->field_10C));

				if (pOld != 0)
					pOld->Burst();

				print_if_false(Mem_RecoverPointer(&pSuper->field_10C) == 0,
						"mhYankWebContact not gone?");

				CTrapWebEffect *pEffect = new CTrapWebEffect(pSuper, 1);

				for (i32 j = 16; j != 0; j--)
					pEffect->AddAnotherStrand();
			}

			this->field_12C->field_6E = 1;
		}
	}

	this->field_104 = 1;
}

// @Ok
// 0x4F8D10, 229 bytes. Called by CPlayer::FireWeb. Wraps a baddy in webbing:
// reuses the CTrapWebEffect already hanging off the baddy's field_104 if
// there is one, otherwise makes a fresh one (Type 0) and tells the player it
// just landed a hit. Either way eight more strands go on.
//
// The original writes field_418 straight through the pointer `new` returned
// without checking it, so an out-of-memory CBit::operator new dereferences
// null here. That is the original's own defect and is left as written.
//
// The original emits eight straight-line AddAnotherStrand calls rather than a
// loop; the loop below is the same eight calls.
void Web_Trap(CSuper *pSuper, i32 a2)
{
	print_if_false(pSuper != 0, "NULL pBaddy sent to Web_Trap");
	print_if_false((pSuper->mFlags >> 1) & 1, "Non superitem sent to Web_Trap");

	CTrapWebEffect *pEffect = reinterpret_cast<CTrapWebEffect*>(
			Mem_RecoverPointer(&pSuper->field_104));

	if (pEffect == 0)
	{
		pEffect = new CTrapWebEffect(pSuper, 0);
		pEffect->field_418 = (u8)a2;

		if (G_MECHLIST != 0)
			reinterpret_cast<CPlayer*>(G_MECHLIST)->SetFirstContactDetails();
	}

	for (i32 i = 0; i < 8; i++)
		pEffect->AddAnotherStrand();
}

// @Ok
// 0x4F9940, 604 bytes. Called by CPlayer::FireWeb. The web projectile itself:
// a CFlatBit that flies from Pos along Normal at Speed. The constructor casts
// the whole flight path as one line up front (gLineInfo + M3dZone_LineToItem)
// and, if it will hit something, shortens mLifetime to the number of frames
// that takes and remembers the impact point, so the sprite stops exactly on
// the surface instead of being tested every frame.
//
// LineOfSightCheck (m3dcolij.h) is raised around the M3dZone_LineToItem call,
// the same "this is a sight/trace query, not a real collision" flag the rest
// of the collision code uses.
//
// mHitPos / mHitNormal are zeroed before the body runs by CVector's and
// CSVector's own default constructors (vector.h), which is exactly the six
// zero stores the original emits before its vtable store; writing them out
// again here would double them.
//
// Original defects kept: mpHitItem / mpHitFace are only written on a hit,
// while the six fields after them are pre-zeroed, so a miss leaves those two
// holding whatever the freshly allocated block had; and the "Not in list"
// walk over EnviroList computes a result that only ever feeds print_if_false,
// which is a no-op in this build.
CImpactWeb::CImpactWeb(
		const CVector &Pos,
		const CSVector &Normal,
		i32 Speed,
		i32 Damage,
		i32 Lifetime)
{
	CPlayer *pPlayer = reinterpret_cast<CPlayer*>(G_MECHLIST);

	if (pPlayer != 0)
		this->mDamage = pPlayer->GetDamageInflictedFromDifficulty(Damage);
	else
		this->mDamage = Damage;

	this->mPos = Pos;
	this->mStartTime = G_TIMER_RELATED;

	print_if_false(Speed != 0, "Zero speed sent to CImpactWeb");

	Utils_GetVecFromMagDir(&this->mVel, Speed, const_cast<CSVector*>(&Normal));

	this->mLifetime = (u16)Lifetime;

	G_LINE_INFO.StartCoords = this->mPos;

	G_LINE_INFO.EndCoords.vx = this->mPos.vx + this->mLifetime * this->mVel.vx;
	G_LINE_INFO.EndCoords.vy = this->mPos.vy + this->mLifetime * this->mVel.vy;
	G_LINE_INFO.EndCoords.vz = this->mPos.vz + this->mLifetime * this->mVel.vz;

	M3dColij_InitLineInfo(&G_LINE_INFO);

	G_LINE_OF_SIGHT_CHECK = 1;
	M3dZone_LineToItem(&G_LINE_INFO, 0);
	G_LINE_OF_SIGHT_CHECK = 0;

	if (G_LINE_INFO.pItem != 0)
	{
		CItem *pItem = EnviroList;

		while (pItem != 0 && pItem != G_LINE_INFO.pItem)
			pItem = pItem->mNextItem;

		print_if_false(pItem != 0, "Not in list");

		this->mpHitItem = G_LINE_INFO.pItem;
		this->mpHitFace = G_LINE_INFO.pFace;

		this->mHitPos.vx = G_LINE_INFO.Position.vx;
		this->mHitPos.vy = G_LINE_INFO.Position.vy;
		this->mHitPos.vz = G_LINE_INFO.Position.vz;

		this->mHitNormal.vx = G_LINE_INFO.Normal.vx;
		this->mHitNormal.vy = G_LINE_INFO.Normal.vy;
		this->mHitNormal.vz = G_LINE_INFO.Normal.vz;

		this->mLifetime = (u16)(G_LINE_INFO.Distance / Speed);

		print_if_false((this->mpHitItem->mFlags & 0x10) == 0, "Hit env obj!");
	}

	if ((i32)this->mLifetime > Lifetime)
		this->mLifetime = (u16)Lifetime;

	this->SetAnim(9);
	this->SetFrame(0);
	this->SetScale(0);

	this->mPostScale = 0x0A001000;

	this->field_5A = Rnd(2) != 0 ? 768 : -768;
}

// @Ok
// 0x4FAD50, 205 bytes. Called by CPlayer::PriorToVenomDistanceAttack. Pops
// the dome: a web dome (field_104 == 0) shatters the item it trapped, then
// the dome switches to mode 3, spawns the shockwave and the ring, and dies.
//
// field_110 is typed CClass* like the dome's other four sub-objects, but the
// original treats this one as a CItem here (it clears mFlags bit 0x400 at
// +4 and makes a thiscall to CItem::Burst), so it is cast, exactly as
// written. Both allocations are plain `new`: the original's
// CBit::operator new(0x98) / CClass::operator new(0x110) plus null check is
// what MSVC6 emits for `new CDomeShockWave` / `new CDomeRing` (CItem's
// operator new is link-folded onto CClass's).
void CDome::Burst(void)
{
	if (this->field_104 == 0)
	{
		CItem *pTrapped = reinterpret_cast<CItem*>(this->field_110);

		if (pTrapped != 0)
		{
			pTrapped->mFlags &= ~0x400;
			pTrapped->Burst(30, 30);
		}
	}

	this->field_100 = 3;

	new CDomeShockWave(this->field_104);
	new CDomeRing(&this->mPos, this->field_104);

	this->Die();
}

#include "my_patch.h"

// @Bogus
void patch_web(void)
{
	// Not hooked, and why:
	//
	// 1. Missing virtuals. Hooking a constructor stamps our own vtable on the
	//    object, so every slot our class does not declare silently falls back
	//    to the base. These classes are all short of the exe's vtable:
	//    CWeb (0x53C768 slot 2 = CWeb_AI 0x4F6C10),
	//    CDome (0x53C7D4 slot 2 = CDome_AI 0x4FA830),
	//    CDomePiece (0x53C7C0 slot 2 = sub_4FA5E0),
	//    CDomeRing (0x53C73C slot 2 = sub_4F5800),
	//    CTrapWebEffect (0x53C798 slot 1 = CTrapWebEffect_Move 0x4F8860),
	//    CWebFrag (0x53C7B8 slot 1 = sub_4FA340),
	//    CImpactWeb (0x53C7B0 slot 1 = sub_4F9BD0),
	//    CDomeShockWave (0x53C7E8 slot 1 = sub_4FB0E0).
	//    That also rules out the functions that build one of them:
	//    CWeb::Fire (new CTrapWebEffect and new CKnottedWeb, and CKnottedWeb
	//    in bit2.h is missing its own Move too), Web_Trap (new
	//    CTrapWebEffect), CDome::Burst (new CDomeShockWave, new CDomeRing)
	//    and CTrapWebEffect::Burst (new CWebFrag).
	//
	// 2. MiscList. CDome, CDomePiece and CDomeRing attach to and detach from
	//    MiscList, which is a plain repo global owned by shell.cpp with no
	//    G_* macro yet. The exe's main loop walks its own MiscList, so a
	//    hooked constructor or destructor here would link the dome into a
	//    list nothing renders. That blocks the four destructors as well
	//    (0x4FA580, 0x4FA770, 0x4F5720).
	//
	// 3. CKnottedWebSplat::Move lives at 0x4A5E40, which the linker folded
	//    together with CSimbyShotSplat::Move. That address belongs to
	//    simby.cpp, so it is left for whoever hooks that file. Hooking the
	//    constructor below is still correct: it stamps our vtable, whose
	//    slot 1 is our own CKnottedWebSplat::Move.
	//
	// 4. CDomeShockWave::ResetHitFlags is INLINE, the original has no
	//    separate function for it.
	PATCH_PUSH_RET(0x004F54A0, Web_GetGroundY);

	PATCH_PUSH_RET_POLY(0x004F5BC0, CKnottedWebSplat::CKnottedWebSplat, "??0CKnottedWebSplat@@QAE@PBVCVector@@0@Z");

	PATCH_PUSH_RET(0x004F6260, CWeb::SwitchToBlob);

	PATCH_PUSH_RET(0x004F72C0, CSwinger::SetSpideyAnimFrame);
	PATCH_PUSH_RET(0x004F72F0, CSwinger::IsOneTimeToDie);
	PATCH_PUSH_RET(0x004F7550, CSwinger_SwingBack);

	PATCH_PUSH_RET(0x004F7680, BoundingBoxCollisionCheck);
	PATCH_PUSH_RET(0x004F7AE0, Web_CollideWithSuper);

	PATCH_PUSH_RET(0x004F8190, CTrapWebEffect::CalcHook);
	PATCH_PUSH_RET(0x004F82A0, CTrapWebEffect::MoveProjector);
	PATCH_PUSH_RET(0x004F83C0, CTrapWebEffect::AddAnotherStrand);

	PATCH_PUSH_RET_POLY(0x004FAF30, CDomeShockWave::~CDomeShockWave, "??1CDomeShockWave@@UAE@XZ");
}
