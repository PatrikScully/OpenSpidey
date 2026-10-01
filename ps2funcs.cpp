#include "ps2funcs.h"
#include "validate.h"
#include <cmath>
#include <cstring>
#include "ob.h"
#include "m3dcolij.h"
#include "Sbinit.h"
#include "stubs.h"
#include "PCGfx.h"
#include "pcdcMem.h"
#include "ps2gamefmv.h"
#include "ps2redbook.h"
#include "ps2lowsfx.h"
#include "ps2pad.h"
#include "PCTex.h"

#include <cstdlib>

#include "my_assert.h"

#ifndef SPIDEY_STANDALONE
i32 gClutRelated;
#else
extern i32 gClutRelated;
#endif

i32 DoVblankProcessing = 1;
i32 gPortRelatedOne;
i32 gSomeSize = 0x6E0000;

#ifndef SPIDEY_STANDALONE
SSinCos rcossin_tbl[FLATBIT_VELOCITIES_SIZE];
#else
extern SSinCos rcossin_tbl[FLATBIT_VELOCITIES_SIZE];
#endif

i32 Pal16X;
i32 Pal16Y;

#ifndef SPIDEY_STANDALONE
EXPORT i16 gRotMatrix[3][3];
#else
extern i16 gRotMatrix[3][3];
#endif

#ifndef SPIDEY_STANDALONE
EXPORT int vertexRegister[4];
#else
extern int vertexRegister[4];
#endif

// guesses: not in idb_globals.txt. Positioned right after vertexRegister ("V0"),
// same shape, used by MTC2 the same way for register indices 2/3 ("V1") and 4/5 ("V2").
#ifndef SPIDEY_STANDALONE
EXPORT int gVertexRegister1[4];
#else
extern int gVertexRegister1[4];
#endif
//#define G_VERTEX_REGISTER1 (gVertexRegister1)
#define G_VERTEX_REGISTER1 (*reinterpret_cast<int(*)[4]>(0x00610BC0))
#ifndef SPIDEY_STANDALONE
EXPORT int gVertexRegister2[4];
#else
extern int gVertexRegister2[4];
#endif
//#define G_VERTEX_REGISTER2 (gVertexRegister2)
#define G_VERTEX_REGISTER2 (*reinterpret_cast<int(*)[4]>(0x00610BD0))

#ifndef SPIDEY_STANDALONE
EXPORT VECTOR translationVector;
#else
extern VECTOR translationVector;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT VECTOR gGeneralLongVector;
#else
extern VECTOR gGeneralLongVector;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT int gRtpsRelatedNoClue;
#else
extern int gRtpsRelatedNoClue;
#endif
//#define G_RTPS_PROJ_DISTANCE (gRtpsRelatedNoClue)
#define G_RTPS_PROJ_DISTANCE (*reinterpret_cast<int*>(0x0054F03C))
#ifndef SPIDEY_STANDALONE
EXPORT int gRtpsRelatedNoClue2;
#else
extern int gRtpsRelatedNoClue2;
#endif
//#define G_RTPS_SCREEN_OFFSET_X (gRtpsRelatedNoClue2)
#define G_RTPS_SCREEN_OFFSET_X (*reinterpret_cast<int*>(0x0054F040))
#ifndef SPIDEY_STANDALONE
EXPORT int gRtpsRelatedNoClue3;
#else
extern int gRtpsRelatedNoClue3;
#endif
//#define G_RTPS_SCREEN_OFFSET_Y (gRtpsRelatedNoClue3)
#define G_RTPS_SCREEN_OFFSET_Y (*reinterpret_cast<int*>(0x0054F044))

#ifndef SPIDEY_STANDALONE
EXPORT VECTOR gFtwOp12;
#else
extern VECTOR gFtwOp12;
#endif
//#define G_FTW_OP12 (gFtwOp12)
#define G_FTW_OP12 (*reinterpret_cast<VECTOR*>(0x00610B90))
#ifndef SPIDEY_STANDALONE
EXPORT VECTOR gWtfOP12;
#else
extern VECTOR gWtfOP12;
#endif
//#define G_WTF_OP12 (gWtfOP12)
#define G_WTF_OP12 (*reinterpret_cast<VECTOR*>(0x00610B80))
#ifndef SPIDEY_STANDALONE
EXPORT VECTOR gOp12Result;
#else
extern VECTOR gOp12Result;
#endif
//#define G_OP12_RESULT (gOp12Result)
#define G_OP12_RESULT (*reinterpret_cast<VECTOR*>(0x00610BE0))

static unsigned char stubGte = 1;

#ifndef SPIDEY_STANDALONE
u8 gPrintStubbed = 1;
#else
extern u8 gPrintStubbed;
#endif
u8 gClearImagePrint = 1;

// @Ok
// @Matching
void MTC2(i32 a1, GTREGType a2)
{
	print_if_false(a2 >= 0 && a2 < 0x16, "Invalid GTE register specified to MTC2.");

	switch (a2)
	{
	case GT_ZERO:
		G_VERTEX_REGISTER[0] = (i16)a1;
		G_VERTEX_REGISTER[1] = a1 >> 16;
		break;
	case GT_ONE:
		G_VERTEX_REGISTER[2] = (i16)a1;
		break;
	case GT_TWO:
		G_VERTEX_REGISTER1[0] = (i16)a1;
		G_VERTEX_REGISTER1[1] = a1 >> 16;
		break;
	case GT_THREE:
		G_VERTEX_REGISTER1[2] = (i16)a1;
		break;
	case GT_FOUR:
		G_VERTEX_REGISTER2[0] = a1 & 0xFFFF;
		G_VERTEX_REGISTER2[1] = a1 >> 16;
		break;
	case GT_FIVE:
		G_VERTEX_REGISTER2[2] = (i16)a1;
		break;
	case GT_SIX:
		print_if_false(0, "MTC2 tried to write to IR0.");
		break;
	case GT_SEVEN:
		G_OP12_RESULT.vx = a1;
		break;
	case GT_EIGHT:
		G_OP12_RESULT.vy = a1;
		break;
	case GT_NINE:
		G_OP12_RESULT.vz = a1;
		break;
	case GT_TEN:
		print_if_false(0, "MTC2 tried to write to MAC0.");
		break;
	case GT_ELEVEN:
		G_GENERAL_LONG_VECTOR.vx = a1;
		break;
	case GT_TWELVE:
		G_GENERAL_LONG_VECTOR.vy = a1;
		break;
	case GT_THIRTEEN:
		G_GENERAL_LONG_VECTOR.vz = a1;
		break;
	case GT_FOURTEEN:
		print_if_false(0, "MTC2 tried to write to RotMat.");
		break;
	case GT_FIFTEEN:
		G_ROT_MATRIX[2][2] = (i16)a1;
		break;
	case GT_SIXTEEN:
		G_TRANSLATION_VECTOR.vx = a1;
		break;
	case GT_SEVENTEEN:
		G_TRANSLATION_VECTOR.vy = a1;
		break;
	case GT_EIGHTEEN:
		G_TRANSLATION_VECTOR.vz = a1;
		break;
	case GT_NINETEEN:
	case GT_TWENTY:
	case GT_TWENTYONE:
	default:
		print_if_false(0, "Unhandled case.");
		break;
	}
}

// @Bogus
void setPolyGT4(void)
{
	if (!gPrintStubbed)
		stubbed_printf("stubbed out: setPolyGT4");
}

// @Ok
// @Matching
void TransMatrix(MATRIX* a1, VECTOR* a2)
{
	a1->t[0] = a2->vx;
	a1->t[1] = a2->vy;
	a1->t[2] = a2->vz;
}

// @Ok
// @Matching
void gte_op0(void)
{
	G_GENERAL_LONG_VECTOR.vx = G_WTF_OP12.vy * G_FTW_OP12.vz - G_WTF_OP12.vz * G_FTW_OP12.vy;
	G_GENERAL_LONG_VECTOR.vy = G_WTF_OP12.vz * G_FTW_OP12.vx - G_FTW_OP12.vz * G_WTF_OP12.vx;
	G_GENERAL_LONG_VECTOR.vz = G_FTW_OP12.vy * G_WTF_OP12.vx - G_WTF_OP12.vy * G_FTW_OP12.vx;
}

// @Ok
// @Matching
void gte_SetRotMatrix(MATRIX* a1)
{
	for (int i = 0; i < 3; i++){
		for (int j = 0; j < 3; j++){
			G_ROT_MATRIX[i][j] = a1->m[i][j];
		}
	}
}

// auto_inline off: these are real out-of-line functions in the original binary (each has
// exactly one caller in this TU). With /Ob2 the compiler would otherwise inline them into
// that caller, turning the caller's real "call ADDR" into inlined code and desyncing the
// caller's disassembly. This does not change behavior, only codegen shape.
#pragma auto_inline(off)

// unnamed helper, address 0x0046E990. Called once by M3dAsm_LineColijPreprocessItemsZoned
// with the fixed-point start/end coordinate arrays and the address of a local the caller
// DOES read back afterward, as a per-axis swap bitmask (confirmed against Hex-Rays: the
// caller casts it to a byte and passes it straight into gsub_46EA20 as the flags
// parameter). Sorts a1[i]/a2[i] per axis (so a1 <= a2), toggling bit i of *a3 whenever a
// swap happens, then stores the per-axis a2-a1 deltas into gRotMatrix (idb_globals.txt:
// 0x00610B20 = gRotMatrix; here it is reused as scratch storage for the sorted box's
// extents, read back by gsub_46EB30 below).
// @Ok
// @Matching
EXPORT void gsub_46E990(i32 *a1, i32 *a2, i32 *a3)
{
	if (a1[0] > a2[0])
	{
		*a3 ^= 1;
		i32 tmp = a1[0];
		a1[0] = a2[0];
		a2[0] = tmp;
	}

	if (a1[1] > a2[1])
	{
		*a3 ^= 2;
		i32 tmp = a1[1];
		a1[1] = a2[1];
		a2[1] = tmp;
	}

	if (a1[2] > a2[2])
	{
		*a3 ^= 4;
		i32 tmp = a1[2];
		a1[2] = a2[2];
		a2[2] = tmp;
	}

	*reinterpret_cast<i32*>(&gRotMatrix[0][0]) = a2[0] - a1[0];
	*reinterpret_cast<i32*>(&gRotMatrix[1][1]) = a2[1] - a1[1];
	*reinterpret_cast<i32*>(&gRotMatrix[2][2]) = a2[2] - a1[2];
}

// unnamed helper, address 0x0046EA20. Called once per item by
// M3dAsm_LineColijPreprocessItemsZoned (and by M3dAsm_LineColijPreprocessItems) with the
// per-model pointer looked up from CItemRelatedList[region*17][model], the line's sorted
// start/end coords (flat, not a Vector by value), the axis-swap bitmask gsub_46E990 wrote
// through its 3rd argument, and the item's fixed-point position. pModel[3]/[4]/[5] each
// pack two i16 model-space extents per axis (low 16 bits, high 16 bits). Builds the
// item's world-space bounding box from those extents and writes it into the two output
// Vector-shaped int[3] pointers, mirroring the box about (start+end) per axis whenever
// gsub_46E990 had to swap that axis, so both corners line up with the line's (now always
// start <= end) coordinates. Return value discarded by every caller, so it is void here.
// @Ok
EXPORT void gsub_46EA20(const i32 *pModel, i32 startX, i32 startY, i32 startZ,
                         i32 endX, i32 endY, i32 endZ, u8 flags,
                         i32 posX, i32 posY, i32 posZ, i32 *pOut1, i32 *pOut2)
{
	i32 loX = (i16)pModel[3] + posX + 2;
	i32 hiX = (pModel[3] >> 16) + posX - 2;

	if (flags & 1)
	{
		pOut1[0] = startX + endX - loX;
		pOut2[0] = startX + endX - hiX;
	}
	else
	{
		pOut1[0] = hiX;
		pOut2[0] = loX;
	}

	i32 loY = (i16)pModel[4] + posY + 2;
	i32 hiY = (pModel[4] >> 16) + posY - 2;

	if (flags & 2)
	{
		pOut1[1] = startY + endY - loY;
		pOut2[1] = startY + endY - hiY;
	}
	else
	{
		pOut1[1] = hiY;
		pOut2[1] = loY;
	}

	i32 loZ = (i16)pModel[5] + posZ + 2;
	i32 hiZ = (pModel[5] >> 16) + posZ - 2;

	if (flags & 4)
	{
		pOut1[2] = startZ + endZ - loZ;
		pOut2[2] = startZ + endZ - hiZ;
	}
	else
	{
		pOut1[2] = hiZ;
		pOut2[2] = loZ;
	}
}

// unnamed helper, address 0x0046EB30. Called once per item by
// M3dAsm_LineColijPreprocessItemsZoned (and by M3dAsm_LineColijPreprocessItems) with the
// line's sorted start (a1..a3) and end (a4..a6) coords and the item's box corners as
// written by gsub_46EA20 (a7..a9 = pOut1, a10..a12 = pOut2). Returns nonzero when the
// line segment intersects the box (a slab/cross-product test). Reads the per-axis
// end-start deltas back out of gRotMatrix's diagonal, where gsub_46E990 stashed them as
// scratch (see its comment above).
// @Ok
EXPORT i32 gsub_46EB30(i32 a1, i32 a2, i32 a3, i32 a4, i32 a5, i32 a6,
                        i32 a7, i32 a8, i32 a9, i32 a10, i32 a11, i32 a12)
{
	if (a7 > a4 || a8 > a5 || a9 > a6 || a10 < a1 || a11 < a2 || a12 < a3)
		return 0;

	i32 dx = *reinterpret_cast<i32*>(&G_ROT_MATRIX[0][0]);
	i32 dy = *reinterpret_cast<i32*>(&G_ROT_MATRIX[1][1]);
	i32 dz = *reinterpret_cast<i32*>(&G_ROT_MATRIX[2][2]);

	i32 ey = a11 - a2;
	i32 ez = a12 - a3;
	i32 ex = a10 - a1;

	i32 dxez = dx * ez;
	i32 dyez = dy * ez;

	if (dyez - dz * ey >= 0)
	{
		if (dx * ey - dy * ex < 0)
			return (dy * (a9 - a3) - dz * ey <= 0) && (dx * ey - dy * (a7 - a1) >= 0);

		return (dz * ex - dx * (a9 - a3) >= 0) && (dx * (a8 - a2) - dy * ex <= 0);
	}

	if (dz * ex - dxez < 0)
		return (dz * ex - dx * (a9 - a3) >= 0) && (dx * (a8 - a2) - dy * ex <= 0);

	if (dyez - dz * (a8 - a2) >= 0 && dz * (a7 - a1) - dxez <= 0)
		return 1;

	return 0;
}

#pragma auto_inline(on)

// @Ok
// Fixed against Hex-Rays decompile of 0x0046E7B0 (2026-08-31): the old version had two
// real bugs, not just register-allocation residue. (1) The "unused" bitmask local IS read
// back: gsub_46E990 toggles bit i of it whenever it swaps axis i, and the caller then
// casts it to a byte and feeds it into gsub_46EA20 as a flags argument. (2) gsub_46EA20
// and gsub_46EB30 were being called with the wrong shape entirely (gsub_46EA20 took only
// the model pointer; gsub_46EB30 took Vector-by-value start/itemPos/end). Their real
// signatures (confirmed against the disassembly of both) are flat-int and very different:
// gsub_46EA20 builds the item's world-space bounding box from the model's packed extents
// and the swap flags, gsub_46EB30 does a line-vs-box intersection test against that box.
// See both functions' updated comments above for the full call shape.
void M3dAsm_LineColijPreprocessItemsZoned(CItem **ppItem, i32 ModelTable, SLineInfo *pInfo, u16 Inquiry)
{
	Vector start = {0, 0, 0};
	Vector end = {0, 0, 0};

	CItem *pItem = *ppItem;

	if (pItem)
	{
		start.vx = pInfo->StartCoords.vx >> 12;
		start.vy = pInfo->StartCoords.vy >> 12;
		start.vz = pInfo->StartCoords.vz >> 12;

		end.vx = pInfo->EndCoords.vx >> 12;
		end.vy = pInfo->EndCoords.vy >> 12;
		end.vz = pInfo->EndCoords.vz >> 12;

		i32 swapFlags = 0;
		gsub_46E990(reinterpret_cast<i32*>(&start), reinterpret_cast<i32*>(&end), &swapFlags);

		do
		{
			i32 **pRegionEntry = CItemRelatedList[pItem->mRegion * 17];

			if (pItem->mFlags & 0x21)
			{
				pItem->mInquiry = Inquiry;
			}
			else if (pItem->mInquiry != Inquiry)
			{
				const i32 *pModel = reinterpret_cast<const i32*>(pRegionEntry[pItem->mModel]);

				i32 posX = pItem->mPos.vx >> 12;
				i32 posY = pItem->mPos.vy >> 12;
				i32 posZ = pItem->mPos.vz >> 12;

				i32 boxA[3];
				i32 boxB[3];

				gsub_46EA20(pModel, start.vx, start.vy, start.vz, end.vx, end.vy, end.vz,
				            static_cast<u8>(swapFlags), posX, posY, posZ, boxA, boxB);

				if (!gsub_46EB30(start.vx, start.vy, start.vz, end.vx, end.vy, end.vz,
				                 boxA[0], boxA[1], boxA[2], boxB[0], boxB[1], boxB[2]))
				{
					pItem->mInquiry = Inquiry;
				}
			}

			ppItem++;
			pItem = *ppItem;
		} while (pItem);
	}
}

// @Ok
void gte_ldv0(const SVECTOR* a1){
	G_VERTEX_REGISTER[0] = a1->vx;
	G_VERTEX_REGISTER[1] = a1->vy;
	G_VERTEX_REGISTER[2] = a1->vz;
}

// @Ok
EXPORT void INLINE FixedXForm(i16 matrix[3][3], const VECTOR* a, VECTOR *r){

	int x = a->vx;
	int y = a->vy;
	int z = a->vz;

	r->vx = (x * matrix[0][0] + y * matrix[0][1] + z * matrix[0][2]) >> 12;
	r->vy = (x * matrix[1][0] + y * matrix[1][1] + z * matrix[1][2]) >> 12;
	r->vz = (x * matrix[2][0] + y * matrix[2][1] + z * matrix[2][2]) >> 12;
	r->pad = (long)r;
}

// @Ok
void gte_rtv0tr(void)
{
	FixedXForm(G_ROT_MATRIX, (VECTOR*)&G_VERTEX_REGISTER[0], &G_GENERAL_LONG_VECTOR);

	G_GENERAL_LONG_VECTOR.vx += G_TRANSLATION_VECTOR.vx >> 12;
	G_GENERAL_LONG_VECTOR.vy += G_TRANSLATION_VECTOR.vy >> 12;
	G_GENERAL_LONG_VECTOR.vz += G_TRANSLATION_VECTOR.vz >> 12;
}

// @Ok
void gte_stlvnl(VECTOR *a1)
{
  a1->vx = G_GENERAL_LONG_VECTOR.vx;
  a1->vy = G_GENERAL_LONG_VECTOR.vy;
  a1->vz = G_GENERAL_LONG_VECTOR.vz;
}

// @NotOk
// Original 0x46DBC0. Perspective uses the transformed third row as depth.
void gte_rtps(void)
{
    VECTOR transformed;
    i32 x = G_VERTEX_REGISTER[0];
    i32 y = G_VERTEX_REGISTER[1];
    i32 z = G_VERTEX_REGISTER[2];
    transformed.vx = (x * G_ROT_MATRIX[0][0] + y * G_ROT_MATRIX[0][1] + z * G_ROT_MATRIX[0][2]) >> 12;
    transformed.vy = (x * G_ROT_MATRIX[1][0] + y * G_ROT_MATRIX[1][1] + z * G_ROT_MATRIX[1][2]) >> 12;
    transformed.vz = (x * G_ROT_MATRIX[2][0] + y * G_ROT_MATRIX[2][1] + z * G_ROT_MATRIX[2][2]) >> 12;
    G_GENERAL_LONG_VECTOR = transformed;
    G_GENERAL_LONG_VECTOR.vz = G_TRANSLATION_VECTOR.vz + transformed.vz;
    if (!G_GENERAL_LONG_VECTOR.vz)
    {
        G_GENERAL_LONG_VECTOR.vx = 0x8000;
        G_GENERAL_LONG_VECTOR.vy = 0x8000;
    }
    else
    {
        G_GENERAL_LONG_VECTOR.vx = G_RTPS_SCREEN_OFFSET_X / 2
            + (transformed.vx + G_TRANSLATION_VECTOR.vx) * G_RTPS_PROJ_DISTANCE / G_GENERAL_LONG_VECTOR.vz;
        G_GENERAL_LONG_VECTOR.vy = G_RTPS_SCREEN_OFFSET_Y / 2
            + (transformed.vy + G_TRANSLATION_VECTOR.vy) * G_RTPS_PROJ_DISTANCE / G_GENERAL_LONG_VECTOR.vz;
    }
}

// @ok
void gte_rtpt(void){
	if ( !stubGte )
		stubbed_printf("stubbed out: gte_rtpt()");
}

// @Ok
void gte_op12(void)
{
  G_GENERAL_LONG_VECTOR.vz = (G_FTW_OP12.vy * G_WTF_OP12.vx - G_WTF_OP12.vy * G_FTW_OP12.vx) >> 12;
  G_GENERAL_LONG_VECTOR.vx = (G_WTF_OP12.vy * G_FTW_OP12.vz - G_WTF_OP12.vz * G_FTW_OP12.vy) >> 12;
  G_GENERAL_LONG_VECTOR.vy = (G_WTF_OP12.vz * G_FTW_OP12.vx - G_FTW_OP12.vz * G_WTF_OP12.vx) >> 12;
  G_OP12_RESULT = G_GENERAL_LONG_VECTOR;
}


// @Ok
void gte_ldlvl(VECTOR *a1)
{
  G_OP12_RESULT = *a1;
}

// @Ok
void gte_sqr0(void)
{
  G_GENERAL_LONG_VECTOR.vx = G_OP12_RESULT.vx * G_OP12_RESULT.vx;
  G_GENERAL_LONG_VECTOR.vy = G_OP12_RESULT.vy * G_OP12_RESULT.vy;
  G_GENERAL_LONG_VECTOR.vz = G_OP12_RESULT.vz * G_OP12_RESULT.vz;
}



// @Ok
void gte_rtv0(void)
{
	FixedXForm(G_ROT_MATRIX, (VECTOR*)&G_VERTEX_REGISTER[0], &G_GENERAL_LONG_VECTOR);

	G_OP12_RESULT = G_GENERAL_LONG_VECTOR;
}

// @Ok
void gte_stlvnl0(int *a1)
{
  *a1 = gGeneralLongVector.vx;
}

// @Ok
void gte_stlvnl2(int *a1)
{
  *a1 = gGeneralLongVector.vz;
}

#ifndef SPIDEY_STANDALONE
EXPORT int gScalar;
#else
extern int gScalar;
#endif
//#define G_SCALAR (gScalar)
#define G_SCALAR (*reinterpret_cast<int*>(0x00610C00))
// @Ok
void gte_gpf0()
{
  G_GENERAL_LONG_VECTOR.vx = G_OP12_RESULT.vx * G_SCALAR;
  G_GENERAL_LONG_VECTOR.vy = G_SCALAR * G_OP12_RESULT.vy;
  G_GENERAL_LONG_VECTOR.vz = G_SCALAR * G_OP12_RESULT.vz;
}

// @Ok
// sf=1 variant of gte_gpf0 above: same multiply, but with a fixed-point
// >>12 shift on every component (matches the real GTE opcode pair
// GPF0/GPF, sf=0 vs sf=1). Unnamed in the IDB, address 0x0046E010. Only
// caller: M3dColij_LineToSphere (m3dcolij.cpp).
void gte_gpf(void)
{
  G_GENERAL_LONG_VECTOR.vx = (G_OP12_RESULT.vx * G_SCALAR) >> 12;
  G_GENERAL_LONG_VECTOR.vy = (G_OP12_RESULT.vy * G_SCALAR) >> 12;
  G_GENERAL_LONG_VECTOR.vz = (G_OP12_RESULT.vz * G_SCALAR) >> 12;
}

#ifndef SPIDEY_STANDALONE
EXPORT int lzc;
#else
extern int lzc;
#endif
//#define G_LZC (lzc)
#define G_LZC (*reinterpret_cast<int*>(0x00610C04))

// @Ok
// @Matching
// loads the leading-zero-count source register (GTE LZCS). Real hardware would count
// the leading zeroes lazily on read (gte_stlzc); this emulation just stashes the value.
void gte_ldlzc(i32 a1)
{
  G_LZC = a1;
}

// @Ok
// @Matching
void gte_stlzc(int *a1)
{
  int v1; // esi
  int v2; // eax
  int v3; // eax

  v1 = G_LZC;
  print_if_false(G_LZC != 0, "G_LZC not zero");
  if ( v1 < 0 )
  {
    v2 = 0;
    do
    {
      v1 <<= 1;
      ++v2;
    }
    while ( v1 < 0 );
    *a1 = v2;
  }
  else
  {
	v3 = 0;
    do
    {
      v1 <<= 1;
      ++v3;
    }
    while ( v1 >= 0 );
    *a1 = v3;
  }
}


// @Ok
void gte_stsv(SVECTOR *a1)
{
  a1->vx = (short)G_OP12_RESULT.vx;
  a1->vy = (short)G_OP12_RESULT.vy;
  a1->vz = (short)G_OP12_RESULT.vz;
}


// @Ok
// Verified 2026-08-31 against Hex-Rays decompile of 0x0046E0F0: the row*vector dot product
// here (gRotMatrix[i][0]*v7->vx + gRotMatrix[i][1]*v7->vy + gRotMatrix[i][2]*v7->vz per row)
// matches exactly once the original's 32-bit-aliased reads of gRotMatrix (dword_610B20,
// dword_610B24, dword_610B28, dword_610B2C, dword_610B30, each packing two adjacent i16
// matrix cells) are unpacked back into individual gRotMatrix[i][j] cells; same for the
// v7 selection (dword_610BB0 vs dword_610BE0 based on a3, matching vertexRegister vs
// gOp12Result here) and the final gOp12Result/gGeneralLongVector writes including the pad
// field. Only register-home/scheduling residue remains, which is fine under the
// functional-only bar for this session.
void gte_mvmva(int _sf, int mx, int a3, int cv, int lm)
{
  VECTOR *v7; // eax

  print_if_false(!(_sf!=0 && _sf!=1), "sf!=0 && sf!=1");
  print_if_false(mx == 0, "MX!=0");
  print_if_false(!a3 || a3 == 3, "bad v");
  print_if_false(cv == 3, "cv!=3");
  print_if_false(lm == 0, "lm!=0");
  v7 = (VECTOR *)G_VERTEX_REGISTER;

  if ( a3 )
    v7 = &G_OP12_RESULT;

  G_GENERAL_LONG_VECTOR.vx = G_ROT_MATRIX[0][0] * v7->vx + G_ROT_MATRIX[0][1] * v7->vy + G_ROT_MATRIX[0][2] * v7->vz;
  G_GENERAL_LONG_VECTOR.vy = G_ROT_MATRIX[1][0] * v7->vx + G_ROT_MATRIX[1][1] * v7->vy + G_ROT_MATRIX[1][2] * v7->vz;
  G_GENERAL_LONG_VECTOR.vz = G_ROT_MATRIX[2][0] * v7->vx + G_ROT_MATRIX[2][1] * v7->vy + G_ROT_MATRIX[2][2] * v7->vz;

  if ( _sf == 1 )
  {
    G_GENERAL_LONG_VECTOR.vx = G_GENERAL_LONG_VECTOR.vx >> 12;
    G_GENERAL_LONG_VECTOR.vy = G_GENERAL_LONG_VECTOR.vy >> 12;
    G_GENERAL_LONG_VECTOR.vz = G_GENERAL_LONG_VECTOR.vz >> 12;
  }

  G_OP12_RESULT.vz = G_GENERAL_LONG_VECTOR.vz;
  G_OP12_RESULT.vx = G_GENERAL_LONG_VECTOR.vx;
  G_OP12_RESULT.vy = G_GENERAL_LONG_VECTOR.vy;
  G_OP12_RESULT.pad = G_GENERAL_LONG_VECTOR.pad;
}


// @Ok
void gte_stsxy(int *a1)
{
  *a1 = (gGeneralLongVector.vx & 0xFFFF) | (gGeneralLongVector.vy << 16);
}

// @Ok
void gte_lddp(int a1)
{
  G_SCALAR = a1;
}


// @Ok
void gte_ldsvrtrow0(const SVECTOR *a1)
{
  G_ROT_MATRIX[0][0] = a1->vx;
  G_ROT_MATRIX[0][1] = a1->vy;
  G_ROT_MATRIX[0][2] = a1->vz;
}

// @Ok
void gte_ldopv1(VECTOR *a1)
{
  G_WTF_OP12 = *a1;
}

// @Ok
void gte_ldopv2(VECTOR *a1)
{
  G_FTW_OP12 = *a1;
}


// @Ok
// @Matching
void gte_ldlv0(const VECTOR *a1)
{
  *(VECTOR *)vertexRegister = *a1;
}


// @Ok
// @Matching
void gte_stsxy3(int *a1, int *a2, int *a3)
{
  *a1 = (gOp12Result.vx & 0xFFFF) | (gOp12Result.vy << 16);
  *a2 = (gOp12Result.vx & 0xFFFF) | (gOp12Result.vy << 16);
  *a3 = (gOp12Result.vx & 0xFFFF) | (gOp12Result.vy << 16);
  if ( !stubGte )
    stubbed_printf("stubbed out:  gte_stsxy3");
}


// @Ok
void gte_rtir(void){
	FixedXForm(G_ROT_MATRIX, &G_OP12_RESULT, &G_GENERAL_LONG_VECTOR);
}

// Scratch 3-row short-vector "matrix" at address 0x00610B40 in the original:
// right after gTranslationVector (0x00610B34, VECTOR, ends 0x00610B40) and
// before gWtfOP12 (0x00610B80). No idb_globals.txt entry for this gap.
// gsub_46D930 (writer) and gsub_46DEB0 (reader) are its only users, and
// (per IDA xrefs, 2026-08-31) both are called ONLY from
// M3dColij_LineToSphere, which only ever writes row 0 and only ever reads
// gGeneralLongVector.vx back out of gsub_46DEB0's result. Rows 1/2 are
// therefore never populated by anything in this binary; kept as a 3-row
// array for fidelity to the real layout, not because rows 1/2 do anything.
#ifndef SPIDEY_STANDALONE
EXPORT SVECTOR gLineToSphereDirMatrix[3];
#else
extern SVECTOR gLineToSphereDirMatrix[3];
#endif

// @Ok
// unnamed in the IDB, address 0x0046D930. Loads a short vector into row 0
// of gLineToSphereDirMatrix. Only caller: M3dColij_LineToSphere, which
// uses it to stash the line's (already normalized) direction.
void gsub_46D930(const SVECTOR *a1)
{
	G_LINE_TO_SPHERE_DIR_MATRIX[0].vx = a1->vx;
	G_LINE_TO_SPHERE_DIR_MATRIX[0].vy = a1->vy;
	G_LINE_TO_SPHERE_DIR_MATRIX[0].vz = a1->vz;
}

// @Ok
// unnamed in the IDB, address 0x0046DEB0. Dot-products each row of
// gLineToSphereDirMatrix against vertexRegister (the GTE V0 "IR" vector),
// fixed-point (>>12), into gGeneralLongVector. Only caller:
// M3dColij_LineToSphere (see gLineToSphereDirMatrix comment above for why
// rows 1/2 do not matter there). The original also stores a 4th value
// (read off the caller's stack via an argument that is never actually
// passed at the one call site in the binary, i.e. caller-frame garbage)
// into the word right after this matrix; that value is never read back
// anywhere, so it is not reproduced here.
void gsub_46DEB0(void)
{
	VECTOR *v = (VECTOR *)G_VERTEX_REGISTER;

	G_GENERAL_LONG_VECTOR.vx = (G_LINE_TO_SPHERE_DIR_MATRIX[0].vx * v->vx
	                        + G_LINE_TO_SPHERE_DIR_MATRIX[0].vy * v->vy
	                        + G_LINE_TO_SPHERE_DIR_MATRIX[0].vz * v->vz) >> 12;
	G_GENERAL_LONG_VECTOR.vy = (G_LINE_TO_SPHERE_DIR_MATRIX[1].vx * v->vx
	                        + G_LINE_TO_SPHERE_DIR_MATRIX[1].vy * v->vy
	                        + G_LINE_TO_SPHERE_DIR_MATRIX[1].vz * v->vz) >> 12;
	G_GENERAL_LONG_VECTOR.vz = (G_LINE_TO_SPHERE_DIR_MATRIX[2].vx * v->vx
	                        + G_LINE_TO_SPHERE_DIR_MATRIX[2].vy * v->vy
	                        + G_LINE_TO_SPHERE_DIR_MATRIX[2].vz * v->vz) >> 12;
}

// @Ok
// @Matching
void gsub_46D9B0(VECTOR *a1)
{
	a1->vx = G_OP12_RESULT.vx;
	a1->vy = G_OP12_RESULT.vy;
	a1->vz = G_OP12_RESULT.vz;
}

// @Ok
// @Matching
// unnamed in the IDB, address 0x0046E090. Multiplies gOp12Result by
// gScalar (fixed point, >>12) and accumulates into gGeneralLongVector.
// One of the two small GTE accumulator helpers M3dUtils_InterpolateVectors
// (m3dutils.cpp, still @BIGTODO/forwarded) calls; decompiled while tracing
// that function's callees.
void gsub_46E090(void)
{
	G_GENERAL_LONG_VECTOR.vx += (G_OP12_RESULT.vx * G_SCALAR) >> 12;
	G_GENERAL_LONG_VECTOR.vy += (G_OP12_RESULT.vy * G_SCALAR) >> 12;
	G_GENERAL_LONG_VECTOR.vz += (G_OP12_RESULT.vz * G_SCALAR) >> 12;
}

// @Ok
// @Matching
// unnamed in the IDB, address 0x0046E430. Stores gGeneralLongVector into
// a1, truncated to 16 bits per component (the original does 16-bit
// "mov [x], cx" stores, not 32-bit). The other of the two small GTE
// accumulator helpers M3dUtils_InterpolateVectors calls.
i16* gsub_46E430(i16 *a1)
{
	a1[0] = static_cast<i16>(G_GENERAL_LONG_VECTOR.vx);
	a1[1] = static_cast<i16>(G_GENERAL_LONG_VECTOR.vy);
	a1[2] = static_cast<i16>(G_GENERAL_LONG_VECTOR.vz);
	return a1;
}

// @Ok
// Decompiled 2026-08-31 from Hex-Rays at 0x0046F820. Unnamed in the IDB.
// Applies a hook's part-local offset (pOffset, a CSVector-shaped 6-byte
// vector: SHook::Part in m3dutils.h) through pPoseFrame (the part's pose
// matrix, SMatrix, i16 rotation + i16 translation) to get a local result,
// then applies pTransform (the item's world transform, MATRIX, i16
// rotation + i32 translation) to that result, leaving it in the software
// GTE "long vector" accumulator (gGeneralLongVector, same global
// gte_stlvnl reads) for a following gte_stlvnl call to pick up. Both
// stages are the same fixed-point rotate-then-translate shape as
// FixedXForm plus a translation add; the "64 bit multiply" the decompiler
// showed is IDA's mixed i16/i32 type inference, not a real 64 bit op (the
// original hardware does this as ordinary 32 bit multiplies). Used by
// M3dUtils_GetHookPosition and M3dUtils_GetDynamicHookPosition
// (m3dutils.cpp).
void gsub_46F820(void *pOffset, SMatrix *pPoseFrame, MATRIX *pTransform)
{
	i16 *pOff = reinterpret_cast<i16*>(pOffset);
	i16 offX = pOff[0];
	i16 offY = pOff[1];
	i16 offZ = pOff[2];

	i16 localX = static_cast<i16>(pPoseFrame->t[0]
		+ ((pPoseFrame->m[0][0] * offX + pPoseFrame->m[0][1] * offY + pPoseFrame->m[0][2] * offZ) >> 12));
	i16 localY = static_cast<i16>(pPoseFrame->t[1]
		+ ((pPoseFrame->m[1][0] * offX + pPoseFrame->m[1][1] * offY + pPoseFrame->m[1][2] * offZ) >> 12));
	i16 localZ = static_cast<i16>(pPoseFrame->t[2]
		+ ((pPoseFrame->m[2][0] * offX + pPoseFrame->m[2][1] * offY + pPoseFrame->m[2][2] * offZ) >> 12));

	G_GENERAL_LONG_VECTOR.vx = pTransform->t[0]
		+ ((pTransform->m[0][0] * localX + pTransform->m[0][1] * localY + pTransform->m[0][2] * localZ) >> 12);
	G_GENERAL_LONG_VECTOR.vy = pTransform->t[1]
		+ ((pTransform->m[1][0] * localX + pTransform->m[1][1] * localY + pTransform->m[1][2] * localZ) >> 12);
	G_GENERAL_LONG_VECTOR.vz = pTransform->t[2]
		+ ((pTransform->m[2][0] * localX + pTransform->m[2][1] * localY + pTransform->m[2][2] * localZ) >> 12);
}

// @Ok
void M3dMaths_SetIdentityRotation(MATRIX *a1)
{
  a1->m[2][2] = 4096;
  a1->m[1][1] = 4096;
  a1->m[0][0] = 4096;

  a1->m[2][1] = 0;
  a1->m[2][0] = 0;
  a1->m[1][2] = 0;

  a1->m[1][0] = 0;
  a1->m[0][2] = 0;
  a1->m[0][1] = 0;
}

// @Ok
// Verified 2026-08-31 against Hex-Rays decompile of 0x0046CD90: the row*col dot product of
// a1*a2 into a3 (each >>12) matches term for term, including the pointer-walk read/store
// order. Only register allocation of the 9 dot-product statements and instruction
// scheduling residue remains, which is fine under the functional-only bar for this session.
void MulMatrix0(MATRIX *a1, MATRIX *a2, MATRIX *a3)
{
  int v3; // [sp+0h] [-78h]
  int v4; // [sp+4h] [-74h]
  int v5; // [sp+8h] [-70h]
  int v6; // [sp+Ch] [-6Ch]
  int v7; // [sp+10h] [-68h]
  int v8; // [sp+14h] [-64h]
  int v9; // [sp+18h] [-60h]
  int v10; // [sp+1Ch] [-5Ch]
  int v11; // [sp+20h] [-58h]
  int v12; // [sp+24h] [-54h]
  int v13; // [sp+28h] [-50h]
  int v14; // [sp+2Ch] [-4Ch]
  int v15; // [sp+30h] [-48h]
  int v16; // [sp+34h] [-44h]
  int v17; // [sp+38h] [-40h]
  int v18; // [sp+3Ch] [-3Ch]
  int v19; // [sp+40h] [-38h]
  int v20; // [sp+44h] [-34h]

  i16* p1 = &a1->m[0][0];
  v12 = *p1; p1++;
  v13 = *p1; p1++;
  v14 = *p1; p1++;
  v15 = *p1; p1++;
  v16 = *p1; p1++;
  v17 = *p1; p1++;
  v18 = *p1; p1++;
  v19 = *p1; p1++;
  v20 = *p1;

  i16* p2 = &a2->m[0][0];
  v3 = *p2; p2++;
  v4 = *p2; p2++;
  v5 = *p2; p2++;
  v6 = *p2; p2++;
  v7 = *p2; p2++;
  v8 = *p2; p2++;
  v9 = *p2; p2++;
  v10 = *p2; p2++;
  v11 = *p2;

  i16* p3 = &a3->m[0][0];
  *p3 = (v12 * v3 + v13 * v6 + v14 * v9) >> 12; p3++;
  *p3 = (v12 * v4 + v13 * v7 + v14 * v10) >> 12; p3++;
  *p3 = (v12 * v5 + v13 * v8 + v14 * v11) >> 12; p3++;
  *p3 = (v15 * v3 + v16 * v6 + v17 * v9) >> 12; p3++;
  *p3 = (v15 * v4 + v16 * v7 + v17 * v10) >> 12; p3++;
  *p3 = (v15 * v5 + v16 * v8 + v17 * v11) >> 12; p3++;
  *p3 = (v18 * v3 + v19 * v6 + v20 * v9) >> 12; p3++;
  *p3 = (v18 * v4 + v19 * v7 + v20 * v10) >> 12; p3++;
  *p3 = (v18 * v5 + v19 * v8 + v20 * v11) >> 12;
}


// @Ok
// 0x46D0E0: a1 = a1 * a2 (libgte MulMatrix). The original copies the nine
// rotation words of a1 into a stack temp and calls MulMatrix0(temp, a2, a1).
// The previous version copied a2 instead, so every caller got a2 * a2: a
// yaw rotation composed through it came out with twice the angle, which
// (via M3dColij_InitLineInfo's line-space frame) made every diagonal
// collision ray test the wrong faces. Found by the standalone build:
// Spidey could not stick to the wall at the level 1 spawn.
void MulMatrix(MATRIX *a1, MATRIX *a2)
{
	MATRIX v2;
	for (int i = 0; i<3; i++){
		for (int j = 0; j<3; j++){
			v2.m[i][j] = a1->m[i][j];
		}
	}
  MulMatrix0(&v2, a2, a1);
}

// @Ok
void m3d_ZeroTransVector(void)
{
  G_TRANSLATION_VECTOR.vx = 0;
  G_TRANSLATION_VECTOR.vy = 0;
  G_TRANSLATION_VECTOR.vz = 0;
}

// @Ok
// @Matching
void VectorNormal(VECTOR* a1, VECTOR* a2)
{
	float fx = (float)a1->vx;
	float fy = (float)a1->vy;
	float fz = (float)a1->vz;

	float lenSq = fx * fx + fy * fy + fz * fz;

	if (lenSq == 0.0f)
	{
		a2->vx = 0;
		a2->vy = 0x1000;
		a2->vz = 0;
		return;
	}

	float len = (float)sqrt((double)lenSq);

	a2->vx = (i32)((float)(a1->vx << 12) / len);
	a2->vy = (i32)((float)(a1->vy << 12) / len);
	a2->vz = (i32)((float)(a1->vz << 12) / len);
}

// @Ok
// 0x00470520. VectorNormal's SVECTOR twin, the Mac build has
// VectorNormalS__FP6VECTORP7SVECTOR (0x000B2AA0) right after VectorNormal
// (0x000B28D0). Same maths, the unit vector is just written out as i16.
void VectorNormalS(VECTOR* a1, SVECTOR* a2)
{
	float fx = (float)a1->vx;
	float fy = (float)a1->vy;
	float fz = (float)a1->vz;

	float lenSq = fx * fx + fy * fy + fz * fz;

	if (lenSq == 0.0f)
	{
		a2->vx = 0;
		a2->vy = 0x1000;
		a2->vz = 0;
		return;
	}

	float len = (float)sqrt((double)lenSq);

	a2->vx = (i16)(i32)((float)(a1->vx << 12) / len);
	a2->vy = (i16)(i32)((float)(a1->vy << 12) / len);
	a2->vz = (i16)(i32)((float)(a1->vz << 12) / len);
}

// @Ok
// @Matching
// @Note: kudos to valps, adding extra parentheses fix it
i32 M3dMaths_SquareRoot0(i32 i){

    if (i <= -32768) {
        return 32768;
    }

    if (i < 0) {
        return 0;
    }
    
	return sqrt(((double)i));
}



// @Ok
// @Matching
i32 M3dMaths_MulDiv64(i32 a1, i32 a2, i32 a3)
{
	if (!a3)
	{
		return -1;
	}

	f64 hope = (f64)a1 * (f64)a2;
	hope /= (f64)a3;

	ASSERT(hope <= 2147483647.0, "hope<=INT_MAX");
	ASSERT(hope >= -2147483648.0, "hope>=INT_MIN");

	return hope;
}

// @Ok
void M3dMaths_TransposeMatrix1(MATRIX *a1, MATRIX *a2)
{
	a2->m[0][0] = a1->m[0][0];
	a2->m[0][1] = a1->m[1][0];
	a2->m[0][2] = a1->m[2][0];
	a2->m[1][0] = a1->m[0][1];
	a2->m[1][1] = a1->m[1][1];
	a2->m[1][2] = a1->m[2][1];
	a2->m[2][0] = a1->m[0][2];
	a2->m[2][1] = a1->m[1][2];
	a2->m[2][2] = a1->m[2][2];
}



// @Ok
// @Matching
void M3dMaths_ScaleMatrix(CItem *a1, MATRIX *a2)
{
	MATRIX v7;
	MATRIX v8;
	memset((void*)&v8, 0, sizeof(v8));

	v8.m[0][0] = a1->mScale.vx;
	v8.m[1][1] = a1->mScale.vy;
	v8.m[2][2] = a1->mScale.vz;

	MulMatrix0(a2, &v8, &v7);

	for (i32 i = 0; i < 3; i++)
	{
		for(i32 j = 0; j < 3; j++)
		{
			a2->m[i][j] = v7.m[i][j];
		}

	}
}

// @Ok
// @Matching
void M3dMaths_CopyMat(MATRIX* a1, MATRIX* a2)
{
	memcpy(reinterpret_cast<void*>(a2), reinterpret_cast<void*>(a1), 3*3*2);
}


unsigned char byte_54D347 = 1;
// @Ok
void M3dAsm_ProcessPolys(unsigned int*, SVECTOR*, int)
{
	if ( !byte_54D347 )
		stubbed_printf("stubbed out: void M3dAsm_ProcessPolys(Uint32 *pFace, SVECTOR *Normals, int NumFaces)");
}


// @Ok
void M3dAsm_SetTransVector(VECTOR* a1)
{
	G_TRANSLATION_VECTOR.vx = a1->vx;
	G_TRANSLATION_VECTOR.vy = a1->vy;
	G_TRANSLATION_VECTOR.vz = a1->vz;
}


// @Ok
// @Matching
MATRIX* RotMatrixYXZ(SVECTOR *a1, MATRIX *a2)
{
	float rx = (float)a1->vx * 0.0015360969118773937f;
	float sx = (float)sin(rx);
	float cx = (float)cos(rx);

	float ry = (float)a1->vy * 0.0015360969118773937f;
	float sy = (float)sin(ry);
	float cy = (float)cos(ry);

	float rz = (float)a1->vz * 0.0015360969118773937f;
	float sz = (float)sin(rz);
	float cz = (float)cos(rz);

	float t1 = sz * sy;
	float t2 = cz * cy;
	a2->m[0][0] = (t1 * sx + t2) * 4096.0f;

	float t3 = cz * sy;
	float t4 = sz * cy;
	a2->m[0][1] = (t3 * sx - t4) * 4096.0f;

	a2->m[0][2] = (sy * cx) * 4096.0f;
	a2->m[1][0] = (sz * cx) * 4096.0f;
	a2->m[1][1] = (cz * cx) * 4096.0f;
	a2->m[1][2] = (-sx) * 4096.0f;
	a2->m[2][0] = (t4 * sx - t3) * 4096.0f;
	a2->m[2][1] = (t2 * sx + t1) * 4096.0f;
	a2->m[2][2] = (cy * cx) * 4096.0f;

	return a2;
}

// @Ok
MATRIX* M3dMaths_RotMatrixYXZ(SVECTOR *a1, MATRIX *a2)
{
	return RotMatrixYXZ(a1, a2);
}


// @Ok
int ratan2(int x, int y)
{
	if (!y)
	{
		if (x < 0)
		{
			return -1024;
		}
		else if (x > 0)
		{
			return 1024;
		}
		else
		{
			print_if_false(0, "x and y are both zero (ratan2)");
			return 0;
		}
	}

	return atan2((f64)x, (f64)y) * 651.0006103515625;

}


// @Ok
// @Matching
i32 GetClut(int, int a2)
{
	return a2 - gClutRelated;
}

#pragma auto_inline(off)

// unnamed helper, address 0x0046D1E0. Only caller is
// M3dAsm_LineColijPreprocessItems below (once decompiled from Hex-Rays,
// this is the same rotation-matrix formula as RotMatrixYXZ term for term,
// just operating on a raw i16 SVECTOR-shaped input and a raw i16
// MATRIX::m-shaped output instead of typed pointers). Builds a 3x3 rotation
// matrix from an item's mAngles.
// @Ok
EXPORT i16* gsub_46D1E0(i16 *pAngles, i16 *pOut)
{
	float rx = (float)pAngles[0] * 0.0015360969118773937f;
	float sx = (float)sin(rx);
	float cx = (float)cos(rx);

	float ry = (float)pAngles[1] * 0.0015360969118773937f;
	float sy = (float)sin(ry);
	float cy = (float)cos(ry);

	float rz = (float)pAngles[2] * 0.0015360969118773937f;
	float sz = (float)sin(rz);
	float cz = (float)cos(rz);

	float t1 = sz * sy;
	float t2 = cz * cy;
	pOut[0] = (t1 * sx + t2) * 4096.0f;

	float t3 = cz * sy;
	float t4 = sz * cy;
	pOut[1] = (t3 * sx - t4) * 4096.0f;

	pOut[2] = (sy * cx) * 4096.0f;
	pOut[3] = (sz * cx) * 4096.0f;
	pOut[4] = (cz * cx) * 4096.0f;
	pOut[5] = (-sx) * 4096.0f;
	pOut[6] = (t4 * sx - t3) * 4096.0f;
	pOut[7] = (t2 * sx + t1) * 4096.0f;
	pOut[8] = (cy * cx) * 4096.0f;

	return pOut;
}

// unnamed helper, address 0x0046CFC0. Only caller is
// M3dAsm_LineColijPreprocessItems below. 3x3 (i16) matrix times a flat i32
// vector, fixed point (>>12), writing into a caller-supplied output triple
// (matches FixedXForm's row*vector shape, just on i32 in/out instead of a
// VECTOR). The original opens with a call to nullsub_1 (0x4015B0, same
// address as gsub_4015B0 in panel.cpp, proven empty: tools/functions/
// 4199856.bin is a single `ret` byte), so that call is omitted here since
// it cannot do anything.
// @Ok
EXPORT i32* gsub_46CFC0(i16 *pMatrix, i32 *pIn, i32 *pOut)
{
	pOut[0] = (pMatrix[0] * pIn[0] + pMatrix[1] * pIn[1] + pMatrix[2] * pIn[2]) >> 12;
	pOut[1] = (pMatrix[3] * pIn[0] + pMatrix[4] * pIn[1] + pMatrix[5] * pIn[2]) >> 12;
	pOut[2] = (pMatrix[6] * pIn[0] + pMatrix[7] * pIn[1] + pMatrix[8] * pIn[2]) >> 12;
	return pOut;
}

#pragma auto_inline(on)

// Decompiled 2026-08-31 from Hex-Rays at 0x0046ECB0 (previously left as a
// forward-to-original stub once the call-shape reconnaissance below was
// done; leaf helpers gsub_46D1E0/gsub_46CFC0 decompiled first per the
// leaf-first rule). Non-zoned sibling of M3dAsm_LineColijPreprocessItemsZoned:
// same mFlags&0x21/mInquiry shortcut, but walks a plain CItem linked list
// (mNextItem) instead of an array of pointers, and each item's box is built
// in the item's LOCAL space (line coords minus mPos) instead of letting
// gsub_46EA20 do the pos offset.
//
// If the item has a nonzero mAngles (CSVector at CItem+0x14), the local box
// corners get rotated by the TRANSPOSE of the item's rotation matrix
// (gsub_46D1E0 builds it, M3dMaths_TransposeMatrix1's exact math transposes
// it) and re-sorted with gsub_46E990, after a quick per-axis
// bounding-radius reject test against pModel[2]. gsub_46EA20 is then called
// with pos 0,0,0 (the box is already local), and if mFlags&0x200 is set the
// resulting box corners get scaled per axis by three i16 reads at CItem
// offsets 0x18/0x1A/0x1C. Those three offsets do not line up with any named
// CItem field (0x18 lands on the second half of mAngles, i.e. mAngles.vz;
// 0x1A is mModel, reinterpreted signed; 0x1C is mDummyFrame+mTintIndex
// reinterpreted as one i16) - this reads like stale/reused bytes rather
// than a deliberate named "scale" field, so it is reproduced verbatim
// (same offsets, same signedness) rather than guessed at with a new field
// name, per "reproduce the source-level bug, don't fix it".
//
// Finally gsub_46EB30 tests the (possibly rotated) local box corners
// against the object-space box gsub_46EA20 built; a collision keeps the
// item pending (advance to the next item unchanged), no collision or a
// failed bounds/radius check marks the item done (mInquiry = Inquiry) so
// later systems skip it.
// @Ok
void M3dAsm_LineColijPreprocessItems(CItem* pItem, i32 ModelTable, SLineInfo* pInfo, u16 Inquiry)
{
	if (!pItem)
		return;

	Vector start = {0, 0, 0};
	Vector end = {0, 0, 0};

	start.vx = pInfo->StartCoords.vx >> 12;
	start.vy = pInfo->StartCoords.vy >> 12;
	start.vz = pInfo->StartCoords.vz >> 12;

	end.vx = pInfo->EndCoords.vx >> 12;
	end.vy = pInfo->EndCoords.vy >> 12;
	end.vz = pInfo->EndCoords.vz >> 12;

	i32 swapFlags = 0;
	gsub_46E990(reinterpret_cast<i32*>(&start), reinterpret_cast<i32*>(&end), &swapFlags);

	do
	{
		if (pItem->mFlags & 0x21)
		{
			pItem->mInquiry = Inquiry;
		}
		else if (pItem->mInquiry != Inquiry)
		{
			i32 **pRegionEntry = CItemRelatedList[pItem->mRegion * 17];
			const i32 *pModel = reinterpret_cast<const i32*>(pRegionEntry[pItem->mModel]);

			i32 posX = pItem->mPos.vx >> 12;
			i32 posY = pItem->mPos.vy >> 12;
			i32 posZ = pItem->mPos.vz >> 12;

			i32 boxMin[3] = { start.vx - posX, start.vy - posY, start.vz - posZ };
			i32 boxMax[3] = { end.vx - posX, end.vy - posY, end.vz - posZ };

			i32 itemFlags = pItem->mFlags;
			bool boundsOk = true;

			if (pItem->mAngles.vx != 0 || pItem->mAngles.vy != 0 || pItem->mAngles.vz != 0)
			{
				i32 radius = pModel[2] >> 12;

				if (boxMin[0] > radius || boxMin[1] > radius || boxMin[2] > radius ||
				    boxMax[0] < -radius || boxMax[1] < -radius || boxMax[2] < -radius)
				{
					boundsOk = false;
				}
				else
				{
					i16 rotMat[9];
					i16 rotMatT[9];
					gsub_46D1E0(reinterpret_cast<i16*>(&pItem->mAngles), rotMat);
					M3dMaths_TransposeMatrix1(reinterpret_cast<MATRIX*>(rotMat), reinterpret_cast<MATRIX*>(rotMatT));

					if (swapFlags & 1)
					{
						i32 tmp = boxMin[0]; boxMin[0] = boxMax[0]; boxMax[0] = tmp;
					}
					if (swapFlags & 2)
					{
						i32 tmp = boxMin[1]; boxMin[1] = boxMax[1]; boxMax[1] = tmp;
					}
					if (swapFlags & 4)
					{
						i32 tmp = boxMin[2]; boxMin[2] = boxMax[2]; boxMax[2] = tmp;
					}

					i32 tmpIn[3] = { boxMin[0], boxMin[1], boxMin[2] };
					gsub_46CFC0(rotMatT, tmpIn, boxMin);

					tmpIn[0] = boxMax[0]; tmpIn[1] = boxMax[1]; tmpIn[2] = boxMax[2];
					gsub_46CFC0(rotMatT, tmpIn, boxMax);

					swapFlags = 0;
					gsub_46E990(boxMin, boxMax, &swapFlags);
				}
			}

			if (boundsOk)
			{
				*reinterpret_cast<i32*>(&gRotMatrix[0][0]) = boxMax[0] - boxMin[0];
				*reinterpret_cast<i32*>(&gRotMatrix[1][1]) = boxMax[1] - boxMin[1];
				*reinterpret_cast<i32*>(&gRotMatrix[2][2]) = boxMax[2] - boxMin[2];

				i32 outMin[3] = {0, 0, 0};
				i32 outMax[3] = {0, 0, 0};

				gsub_46EA20(pModel, boxMin[0], boxMin[1], boxMin[2], boxMax[0], boxMax[1], boxMax[2],
				            static_cast<u8>(swapFlags), 0, 0, 0, outMin, outMax);

				if (itemFlags & 0x200)
				{
					const u8 *pRawItem = reinterpret_cast<const u8*>(pItem);
					i16 scaleX = *reinterpret_cast<const i16*>(pRawItem + 0x18);
					i16 scaleY = *reinterpret_cast<const i16*>(pRawItem + 0x1A);
					i16 scaleZ = *reinterpret_cast<const i16*>(pRawItem + 0x1C);

					outMin[0] = (outMin[0] * scaleX) >> 12;
					outMin[1] = (outMin[1] * scaleY) >> 12;
					outMin[2] = (outMin[2] * scaleZ) >> 12;
					outMax[0] = (outMax[0] * scaleX) >> 12;
					outMax[1] = (outMax[1] * scaleY) >> 12;
					outMax[2] = (outMax[2] * scaleZ) >> 12;
				}

				if (!gsub_46EB30(boxMin[0], boxMin[1], boxMin[2], boxMax[0], boxMax[1], boxMax[2],
				                 outMin[0], outMin[1], outMin[2], outMax[0], outMax[1], outMax[2]))
				{
					pItem->mInquiry = Inquiry;
				}
			}
			else
			{
				pItem->mInquiry = Inquiry;
			}
		}

		pItem = pItem->mNextItem;
	} while (pItem);
}

// idb_globals.txt: DCFatalError @ 0x6150E4
#ifndef SPIDEY_STANDALONE
i32 DCFatalError;
#else
extern i32 DCFatalError;
#endif

// @Ok
// @Matching
void DCSetFatalError(i32 a1)
{
	DCFatalError = a1;

	if (PCGfx_IsInScene())
	{
		PCGfx_EndScene(1);

		// same address as gsub_430880 (nullsub_3), declared and defined in
		// PCShell.cpp; extern here, cast to accept the (unused) dummy arg this
		// call site passes, so it is a real cross-TU direct call.
		extern void gsub_430880(void);
		((void(*)(i32))gsub_430880)(4);
	}

	GameFMV_StopFMV();
	Redbook_XAExit();
	SFX_ShutDown();
	DCPad_ShutDownVibrations();
	PCTex_ReleaseAllTextures();

	if (DCFatalError == 2)
	{
		// same address as gsub_430880 (nullsub_3), declared and defined in
		// PCShell.cpp; extern here so this call is a real cross-TU direct
		// call instead of an indirect register call.
		extern void gsub_430880(void);
		gsub_430880();
	}
	else
	{
		sbExitSystem();

		// same address as buIsReady (pcdcBkup.h) but called here with no argument
		// pushed, so it is probably a different function whose body got folded
		// into buIsReady's at link time. Cast to a real cross-TU direct call.
		extern i32 buIsReady(i32);
		((void(*)(void))buIsReady)();
	}

	exit(0);
}

// @Ok
INLINE void DCInitSinCosTable(void)
{
	for (i32 i = 0; i < FLATBIT_VELOCITIES_SIZE; i++)
	{
		f64 v9 = (f64)i * 0.001536096911877394;
		G_RCOSSIN_TBL[i].sin = sin(v9) * 4096.0;
		G_RCOSSIN_TBL[i].cos = cos(v9) * 4096.0;
	}

}

EXPORT i32 gBroadcastMode = 0x38;
EXPORT i32 gDisplayModeRelated;
EXPORT i32 gDisplayModeRelatedTwo = 2;
EXPORT u8 gEuropeVersion;

// @Ok
INLINE u8 IsForEurope(void)
{
	return gEuropeVersion;
}

// @Ok
void Port_InitAtStart(void)
{
	gBroadcastMode = 0x38;
	gDisplayModeRelated = 0;
	gDisplayModeRelatedTwo = 1;

	i32 v1 = syCblCheck();
	i32 v2 = 0;

	switch (syCblCheck())
	{
		case 0:
		case 2:
			v2 = syCblCheckBroadcast();
			switch (v2)
			{
				case 0:
				case 2:
					print_if_false(IsForEurope() == 0, "NTSC TV on European version.");
					gBroadcastMode = 0x38;
					break;
				case 1:
				case 3:
					print_if_false(IsForEurope(), "Pal TV on non-European version.");
					gBroadcastMode = 0x7A;
					break;
				default:
					print_if_false(0, "invalid broadcast enumeration value.");
					break;
			}
			break;
		case 1:
			gBroadcastMode = 0x31;
			break;
		default:
			print_if_false(0, "invalid cable enumeration value.");
			break;
	}

	sbInitSystem(gBroadcastMode, gDisplayModeRelated, gDisplayModeRelatedTwo);
	if (!v1 && (v2 == 1 || v2 == 3))
	{
		kmSetPALEXTCallback(reinterpret_cast<void*>(0x46CD70), 0);
		kmSetDisplayMode(122, 0, 1, 0);
	}
	gPortRelatedOne = reinterpret_cast<i32>(syMalloc(gSomeSize));
	print_if_false(gPortRelatedOne != 0, "Out of system memory.");
	DCInitSinCosTable();
}

// @Ok
void Port_Exit(void)
{
	sbExitSystem();
}


void validate_MATRIX(void){
	VALIDATE_SIZE(MATRIX, 0x20);
	VALIDATE(MATRIX, m, 0x0);
	VALIDATE(MATRIX, t, 0x14);
}

void validate_SMatrix(void)
{
	VALIDATE_SIZE(SMatrix, 0x18);
	VALIDATE(SMatrix, m, 0x0);
	VALIDATE(SMatrix, t, 0x12);
}

void validate_SJoint(void)
{
	VALIDATE_SIZE(SJoint, 0xC);

	VALIDATE(SJoint, Angles, 0x0);
	VALIDATE(SJoint, Displacement, 0x6);
}

void validate_SLink(void)
{
	VALIDATE_SIZE(SLink, 0xC);

	VALIDATE(SLink, Part, 0x0);
	VALIDATE(SLink, ParentPart, 0x2);
	VALIDATE(SLink, Pivot, 0x4);
	VALIDATE(SLink, ParentLink, 0xA);
}

#include "my_patch.h"

// @Bogus
void patch_ps2funcs(void)
{
	PATCH_PUSH_RET(0x0046D430, M3dMaths_SquareRoot0);
	PATCH_PUSH_RET(0x0046D500, M3dMaths_MulDiv64);
	PATCH_PUSH_RET(0x0046E730, M3dMaths_RotMatrixYXZ);

	// The emulated GTE. These were not hookable before, because the register
	// file they work on was a private copy in our DLL: a sequence started by
	// exe code and finished by ours (or the other way round) would have used
	// two different sets of registers. Now that the file is bound to the exe's
	// memory any mix of hooked and unhooked gte_* calls sees the same state.
	//
	// Every one of these was checked against the disassembly for references
	// outside the register file, and none has any. gte_rtpt, gte_stsxy3 and
	// M3dAsm_ProcessPolys read the stubGte flag, which is 1 in both copies and
	// never written.
	PATCH_PUSH_RET(0x0046CFA0, TransMatrix);
	PATCH_PUSH_RET(0x0046D0E0, MulMatrix);
	PATCH_PUSH_RET(0x0046D130, ratan2);
	PATCH_PUSH_RET(0x0046D1E0, RotMatrixYXZ);
	PATCH_PUSH_RET(0x0046D3E0, M3dMaths_TransposeMatrix1);
	PATCH_PUSH_RET(0x0046D480, M3dMaths_ScaleMatrix);
	PATCH_PUSH_RET(0x0046D5A0, M3dMaths_CopyMat);
	PATCH_PUSH_RET(0x0046D640, gte_ldopv1);
	PATCH_PUSH_RET(0x0046D670, gte_ldopv2);
	PATCH_PUSH_RET(0x0046D6A0, gte_op0);
	PATCH_PUSH_RET(0x0046D700, gte_op12);
	PATCH_PUSH_RET(0x0046D790, gte_stlvnl);
	PATCH_PUSH_RET(0x0046D7B0, gte_SetRotMatrix);
	PATCH_PUSH_RET(0x0046D840, gte_ldlv0);
	PATCH_PUSH_RET(0x0046D870, gte_ldlvl);
	PATCH_PUSH_RET(0x0046D8A0, gte_ldv0);
	PATCH_PUSH_RET(0x0046D960, gte_ldsvrtrow0);
	PATCH_PUSH_RET(0x0046D990, gte_lddp);
	PATCH_PUSH_RET(0x0046D9A0, gte_ldlzc);
	PATCH_PUSH_RET(0x0046D9D0, gte_stlzc);
	PATCH_PUSH_RET(0x0046DA10, gte_stsv);
	PATCH_PUSH_RET(0x0046DA40, gte_rtir);
	PATCH_PUSH_RET(0x0046DAF0, gte_rtv0tr);
	PATCH_PUSH_RET(0x0046DBC0, gte_rtps);
	PATCH_PUSH_RET(0x0046DCE0, gte_rtpt);
	PATCH_PUSH_RET(0x0046DD00, gte_sqr0);
	PATCH_PUSH_RET(0x0046DDF0, gte_rtv0);
	PATCH_PUSH_RET(0x0046DF60, gte_stlvnl0);
	PATCH_PUSH_RET(0x0046DF70, gte_stlvnl2);
	PATCH_PUSH_RET(0x0046DF80, gte_stsxy);
	PATCH_PUSH_RET(0x0046DFA0, gte_stsxy3);
	PATCH_PUSH_RET(0x0046E050, gte_gpf0);
	PATCH_PUSH_RET(0x0046E0F0, gte_mvmva);
	PATCH_PUSH_RET(0x0046E270, MTC2);
	PATCH_PUSH_RET(0x0046E460, m3d_ZeroTransVector);
	PATCH_PUSH_RET(0x0046E480, M3dMaths_SetIdentityRotation);
	PATCH_PUSH_RET(0x0046E750, M3dAsm_ProcessPolys);
	PATCH_PUSH_RET(0x0046E770, M3dAsm_SetTransVector);

	// Not hooked: GetClut (0x0046E7A0) reads 0x0060DBE4, still a private copy,
	// and M3dAsm_LineColijPreprocessItems / ...Zoned pull in the whole
	// collision closure, which is being converted separately.
}
