#include "core/algebra.h"

// 0x56E668: active 4x4 transform matrix, 16 floats, column-major.
// The Display*List projection code in bit.cpp copies the current
// camera matrix into this before projecting.
static f32 * const gGfxMatrix = (f32 *)0x56E668;

// @Ok
// @Matching
SFloat3* SFloat3::Copy(const SFloat3* src)
{
	this->x = src->x;
	this->y = src->y;
	this->z = src->z;
	return this;
}

// @Ok
// @AlmostMatching: 22 operand diffs, all from x87 term order inside the four
// dot products. The mnemonic sequence matches exactly (cmpsum 0). The original
// emits each row's four terms in the order in[3],in[0],in[1],in[2] (row 0 is
// in[3],in[1],in[0],in[2]); our pointer build emits a different term order
// (in[3],in[1],in[2],in[0] for rows 3,2,1). 19 source hypotheses tried:
// 1D pointer with 4 term orders (fixed reordering, no match); 2D pointer with
// 2 term orders (always in[3],in[0],in[2],in[1]); real 1D array f32 M[16] and
// real 2D array f32 M[4][4] with 2 term orders each (these DO reproduce the
// original row 3,2,1 order in[3],in[0],in[1],in[2]); 8 row-0-only term orders.
// The real arrays match the term order but live in the DLL .data, so they read
// the wrong matrix (the game's matrix is the fixed global at 0x56E668) and are
// functionally wrong; the pointer reads 0x56E668 correctly but cannot reproduce
// the original's term scheduling. Structure (4 f32 locals, store order
// out[0],out[1],out[2],out[3]) and operand order (matrix first, in[] second)
// are confirmed correct.
f32* Algebra_Transform4(f32* out, f32* in)
{
	f32 o1, o2, o3, o0;
	o3 = gGfxMatrix[15] * in[3] + gGfxMatrix[3] * in[0] + gGfxMatrix[7] * in[1] + gGfxMatrix[11] * in[2];
	o2 = gGfxMatrix[14] * in[3] + gGfxMatrix[2] * in[0] + gGfxMatrix[6] * in[1] + gGfxMatrix[10] * in[2];
	o1 = gGfxMatrix[13] * in[3] + gGfxMatrix[1] * in[0] + gGfxMatrix[5] * in[1] + gGfxMatrix[9] * in[2];
	o0 = gGfxMatrix[12] * in[3] + gGfxMatrix[4] * in[1] + gGfxMatrix[0] * in[0] + gGfxMatrix[8] * in[2];
	out[0] = o0;
	out[1] = o1;
	out[2] = o2;
	out[3] = o3;
	return out;
}
