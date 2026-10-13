#pragma once

#ifndef PS2FUNCS_H
#define PS2FUNCS_H

#include "compatibility/export.h"
#include "core/quaternion.h"
#include "core/fixed_vector.h"

EXPORT extern i32 gClutRelated;
EXPORT extern i32 gPortRelatedOne;
EXPORT extern i32 gSomeSize;
EXPORT extern i32 DoVblankProcessing;

struct SSinCos
{
	i16 sin;
	i16 cos;
};

#define FLATBIT_VELOCITIES_SIZE (4096)
#define FLATBIT_VELOCITIES_MAX_INDEX (FLATBIT_VELOCITIES_SIZE-1)

// @FIXME
// that's not the name
// it's something like rcosin_table
// ---------------------------------------------------------------------------
// The PSX GTE register file, emulated in software.  None of the gte_* functions
// are hooked, so a whole GTE sequence normally stays in one world.  It does not
// stay there once callers get hooked: M3dAsm_SetTransVector in the exe would
// write the exe's translationVector while a hooked caller's gte_rtps read ours.
// Pointing the register file at the exe's memory removes the whole problem and
// is what the original does anyway.
//
// Two of these were already broken before any of this:
//  - rcossin_tbl is 4096 sin/cos pairs that Port_InitAtStart builds at startup
//    (via DCInitSinCosTable, inlined at 0x0046CCEA).  Port_InitAtStart is not
//    hooked, so our copy stayed all zeros while 100 functions in the original
//    read the table.  Nothing in the whole .text ever writes the address, and
//    it sits past the raw-backed end of .data, so it is generated, not baked.
//  - the three rtps projection constants hold 276 / 512 / 240 in the exe and 0
//    in our copy, so a hooked gte_rtps projected every vertex onto (0, 0).
//
// Addresses came from the disassembly: gte_ldv0 writes 0x00610BB0, gte_lddp
// writes 0x00610C00, gte_ldopv1 writes 0x00610B80, gte_ldopv2 writes
// 0x00610B90, gte_rtps reads 0x0054F03C/40/44 in that order.
// ---------------------------------------------------------------------------
EXPORT extern SSinCos rcossin_tbl[FLATBIT_VELOCITIES_SIZE];
//#define G_RCOSSIN_TBL (rcossin_tbl)
#define G_RCOSSIN_TBL (*reinterpret_cast<SSinCos(*)[FLATBIT_VELOCITIES_SIZE]>(0x00610C48))

EXPORT extern i32 Pal16X;
EXPORT extern i32 Pal16Y;

EXPORT extern u8 gPrintStubbed;
EXPORT extern u8 gClearImagePrint;

// values 0..21 (0x15) are the valid range MTC2 accepts; some of those are
// reserved and MTC2 asserts on them (GT_SIX/GT_TEN/GT_FOURTEEN, see MTC2 for
// the exact assert strings pulled from the original binary). Only GT_ZERO
// and GT_ONE names are backed by real call sites (utils.cpp); the rest are
// tentative sequential names, not hardware register names.
enum GTREGType
{
	GT_ZERO = 0,
	GT_ONE = 1,
	GT_TWO = 2,
	GT_THREE = 3,
	GT_FOUR = 4,
	GT_FIVE = 5,
	GT_SIX = 6,
	GT_SEVEN = 7,
	GT_EIGHT = 8,
	GT_NINE = 9,
	GT_TEN = 10,
	GT_ELEVEN = 11,
	GT_TWELVE = 12,
	GT_THIRTEEN = 13,
	GT_FOURTEEN = 14,
	GT_FIFTEEN = 15,
	GT_SIXTEEN = 16,
	GT_SEVENTEEN = 17,
	GT_EIGHTEEN = 18,
	GT_NINETEEN = 19,
	GT_TWENTY = 20,
	GT_TWENTYONE = 21,
};

struct SLineInfo;

// size: 0xC
struct SJoint {
	// offset: 0000 (6 bytes)
	struct SVector Angles;
	// offset: 0006 (6 bytes)
	struct SVector Displacement;
};

// size: 0xC
struct SLink {
	// offset: 0000
	unsigned short Part;
	// offset: 0002
	unsigned short ParentPart;
	// offset: 0004 (6 bytes)
	struct SVector Pivot;
	// offset: 000A
	unsigned short ParentLink;
};

struct MATRIX{
	i16 m[3][3];
	i32 t[3];
};

struct SMatrix {
	// offset: 0000 (18 bytes)
	i16 m[3][3];
	// offset: 0012 (6 bytes)
	i16 t[3];
};

typedef struct {		/* long word type 3D vector */
	long	vx, vy;
	long	vz, pad;
} VECTOR;
	
struct SVECTOR {		/* short word type 3D vector */	
	i16	vx, vy;
	i16	vz, pad;

	EXPORT inline SVECTOR(void)
	{
		this->vx = 0;
		this->vy = 0;
		this->vz = 0;
	}

};
	       
// The rest of the emulated GTE register file, see the note above rcossin_tbl.
// These were declared with a local `extern` in every .cpp that used them; one
// declaration here replaces all of those.
EXPORT extern i16 gRotMatrix[3][3];
//#define G_ROT_MATRIX (gRotMatrix)
#define G_ROT_MATRIX (*reinterpret_cast<i16(*)[3][3]>(0x00610B20))

EXPORT extern VECTOR translationVector;
//#define G_TRANSLATION_VECTOR (translationVector)
#define G_TRANSLATION_VECTOR (*reinterpret_cast<VECTOR*>(0x00610B34))

EXPORT extern SVECTOR gLineToSphereDirMatrix[3];
//#define G_LINE_TO_SPHERE_DIR_MATRIX (gLineToSphereDirMatrix)
#define G_LINE_TO_SPHERE_DIR_MATRIX (*reinterpret_cast<SVECTOR(*)[3]>(0x00610B40))

EXPORT extern VECTOR gGeneralLongVector;
//#define G_GENERAL_LONG_VECTOR (gGeneralLongVector)
#define G_GENERAL_LONG_VECTOR (*reinterpret_cast<VECTOR*>(0x00610BA0))

EXPORT extern int vertexRegister[4];
//#define G_VERTEX_REGISTER (vertexRegister)
#define G_VERTEX_REGISTER (*reinterpret_cast<int(*)[4]>(0x00610BB0))

typedef struct {		/* color type vector */	
  u8	r, g, b, cd;
} CVECTOR;
	       
typedef struct {		/* 2D short vector */
	short vx, vy;
} DVECTOR;


void validate_MATRIX(void);
void validate_SMatrix(void);
void validate_SJoint(void);
void validate_SLink(void);

EXPORT void Port_InitAtStart(void);
EXPORT void Port_Exit(void);

EXPORT void gte_op0(void);
EXPORT void gte_SetRotMatrix(MATRIX*);
EXPORT void gte_ldv0(const SVECTOR* a1);
EXPORT void gte_rtv0tr(void);
EXPORT void gte_stlvnl(VECTOR *a1);
EXPORT void gte_rtps(void);
EXPORT void gte_rtpt(void);
EXPORT void gte_op12(void);
EXPORT void gte_ldlvl(VECTOR *a1);
EXPORT void gte_sqr0(void);
EXPORT void gte_rtv0(void);
EXPORT void gte_stlvnl0(i32 *a1);
EXPORT void gte_stlvnl2(i32 *a1);
EXPORT void gte_gpf0(void);
// sf=1 variant of gte_gpf0 (real GTE opcode pair GPF0/GPF), address 0x0046E010.
// Only caller: M3dColij_LineToSphere (m3dcolij.cpp).
EXPORT void gte_gpf(void);
EXPORT void gte_ldlzc(i32 a1);
EXPORT void gte_stlzc(i32 *a1);
EXPORT void gte_stsv(SVECTOR *a1);
EXPORT void gte_mvmva(i32 _sf, i32 mx, i32 a3, i32 cv, i32 lm);
EXPORT void gte_stsxy(i32 *a1);
EXPORT void gte_lddp(i32 a1);
EXPORT void gte_ldsvrtrow0(const SVECTOR *a1);
EXPORT void gte_ldopv1(VECTOR *a1);
EXPORT void gte_ldopv2(VECTOR *a1);
EXPORT void gte_ldlv0(const VECTOR *a1);
EXPORT void gte_stsxy3(i32 *a1, i32 *a2, i32 *a3);
EXPORT void gte_rtir(void);

// unnamed GTE helpers, addresses 0x0046D930/0x0046DEB0. Both used ONLY by
// M3dColij_LineToSphere (m3dcolij.cpp, verified via IDA xrefs 2026-08-31).
// gsub_46D930 loads a short vector into row 0 of a small scratch "matrix"
// (address 0x00610B40, right after gTranslationVector and before gWtfOP12,
// no idb_globals.txt entry); gsub_46DEB0 dot-products each row of that
// matrix against vertexRegister (fixed-point, >>12) into
// gGeneralLongVector. LineToSphere only ever writes row 0 (the line
// direction) and only ever reads gGeneralLongVector.vx back out
// afterward (via gte_stlvnl0), so rows 1/2 are functionally dead for the
// one call site that exists in the binary; kept as a 3-row matrix for
// fidelity to the real memory layout, not because rows 1/2 do anything.
EXPORT void gsub_46D930(const SVECTOR *a1);
EXPORT void gsub_46DEB0(void);

// unnamed GTE store, address 0x0046D9B0. called by Utils_CalculateSpatialAttenuation
// right after gte_rtir(), stores 2 of the 3 result fields (offset 0 and offset 8 of
// the destination are read by the caller, offset 4 is not). guess: another
// store-long-vector variant next to gte_stlvnl/gte_stlvnl0/gte_stlvnl2.
EXPORT void gsub_46D9B0(VECTOR *a1);

// unnamed GTE accumulator helpers, addresses 0x0046E090/0x0046E430. See
// ps2funcs.cpp for details; used by M3dUtils_InterpolateVectors (still
// forwarded, m3dutils.cpp).
EXPORT void gsub_46E090(void);
EXPORT i16* gsub_46E430(i16 *a1);

// unnamed GTE store+double-transform, address 0x0046F820. See ps2funcs.cpp
// for details; used by M3dUtils_GetHookPosition/M3dUtils_GetDynamicHookPosition.
EXPORT void gsub_46F820(void *pOffset, SMatrix *pPoseFrame, MATRIX *pTransform);

EXPORT void M3dMaths_SetIdentityRotation(MATRIX *a1);
EXPORT void MulMatrix0(MATRIX *a1, MATRIX *a2, MATRIX *a3);
EXPORT void MulMatrix(MATRIX *a1, MATRIX *a2);
EXPORT void m3d_ZeroTransVector(void);
EXPORT void VectorNormal(VECTOR*, VECTOR*);
EXPORT void VectorNormalS(VECTOR*, SVECTOR*);

EXPORT i32 M3dMaths_SquareRoot0(i32 i);

EXPORT i32 M3dMaths_MulDiv64(i32, i32, i32);

EXPORT void M3dMaths_TransposeMatrix1(MATRIX *a1, MATRIX *a2);

class CItem;
EXPORT void M3dMaths_ScaleMatrix(CItem*, MATRIX *a2);

EXPORT void M3dMaths_CopyMat(MATRIX*, MATRIX*);

EXPORT void M3dAsm_ProcessPolys(u32*, SVECTOR*, i32);
EXPORT void M3dAsm_SetTransVector(VECTOR*);

EXPORT MATRIX* RotMatrixYXZ(SVECTOR *a1, MATRIX *a2);
EXPORT MATRIX* M3dMaths_RotMatrixYXZ(SVECTOR *a1, MATRIX *a2);

EXPORT i32 ratan2(i32, i32);

// original mangled name is ?GetClut@@YAHHH@Z (returns int, not u16)
EXPORT i32 GetClut(i32, i32);

EXPORT void M3dAsm_LineColijPreprocessItems(CItem*, i32, SLineInfo*, u16);
EXPORT void M3dAsm_LineColijPreprocessItemsZoned(CItem**, i32, SLineInfo*, u16);

EXPORT void TransMatrix(MATRIX*, VECTOR*);

EXPORT void setPolyGT4(void);
// takes the register VALUE directly, not a pointer (confirmed from the original
// disasm: case handlers never dereference the first arg slot, they split/store it
// directly). The old `i32*` prototype was a placeholder that didn't match.
EXPORT void MTC2(i32, GTREGType);


EXPORT void DCSetFatalError(i32);
EXPORT void DCInitSinCosTable(void);
EXPORT u8 IsForEurope(void);

// @Ok
inline static void DrawSync(void)
{
	if (!gPrintStubbed)
	{
		stubbed_printf("stubbed out: DrawSync");
	}
}

// @Ok
inline static void ClearImage(void)
{
	if (!gPrintStubbed)
	{
		stubbed_printf("stubbed out: ClearImage");
	}
}

// @Ok
inline static void _LoadImage(void)
{
	if (!gPrintStubbed)
	{
		stubbed_printf("stubbed out: LoadImage");
	}
}

// @Ok
inline static void StoreImage(void)
{
	if (!gPrintStubbed)
	{
		stubbed_printf("stubbed out: StoreImage");
	}
}

// @Ok
inline static void setDrawTPage(void)
{
	if (!gPrintStubbed)
	{
		stubbed_printf("stubbed out: setDrawTPage");
	}
}

// @Ok
inline static void PutDispEnv(void)
{
	if (!gPrintStubbed)
	{
		stubbed_printf("stubbed out: PutDispEnv");
	}
}

#define STUBBED_FUNC(x)\
	inline static void x(void)\
	{\
		if (!gPrintStubbed)\
		{\
			stubbed_printf("stubbed out: " #x);\
		}\
	}

STUBBED_FUNC(SetDispMask)
STUBBED_FUNC(ClearImage2)
STUBBED_FUNC(ClearOTagR)
STUBBED_FUNC(SetDefDrawEnv)
STUBBED_FUNC(SetDefDispEnv)
STUBBED_FUNC(setRGB0)
STUBBED_FUNC(PutDrawEnv)
STUBBED_FUNC(DrawOTag)
STUBBED_FUNC(SetDrawArea)
STUBBED_FUNC(setPolyF3)
STUBBED_FUNC(setSemiTrans)

void patch_ps2funcs(void);

#endif
