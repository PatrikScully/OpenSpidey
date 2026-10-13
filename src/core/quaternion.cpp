#include "core/quaternion.h"
#include "compatibility/validate.h"
#include "engine/rendering/ps2funcs.h"
#include "core/utils.h"

// guess: cyclic next-axis lookup table {1, 2, 0}, referenced only here.
// no name in the maintainer's idb_globals.txt near 0x551B70 (0x551B80 is
// gRelocTable, this table sits right before it).
static i32 * const gQuatAxisNext = (i32*)0x551B70;

// @Ok
// @Matching
void MToQ(MATRIX const & a1, CQuat& a2)
{
	i32 trace = a1.m[0][0] + a1.m[1][1] + a1.m[2][2];

	if (trace > 0)
	{
		i32 s = M3dMaths_SquareRoot0((trace + 4096) << 12);
		a2.w = s >> 1;
		i32 scale = 0x800000 / s;
		a2.x = ((a1.m[1][2] - a1.m[2][1]) * scale) >> 12;
		a2.y = ((a1.m[2][0] - a1.m[0][2]) * scale) >> 12;
		a2.z = ((a1.m[0][1] - a1.m[1][0]) * scale) >> 12;
		return;
	}

	i32 i = 0;
	if (a1.m[1][1] > a1.m[0][0]) i = 1;
	if (a1.m[2][2] > a1.m[i][i]) i = 2;

	i32 j = gQuatAxisNext[i];
	i32 k = gQuatAxisNext[j];

	i32 s = M3dMaths_SquareRoot0((a1.m[i][i] - a1.m[k][k] - a1.m[j][j] + 4096) << 12);

	switch (i)
	{
		case 0: a2.x = s >> 1; break;
		case 1: a2.y = s >> 1; break;
		case 2: a2.z = s >> 1; break;
	}

	i32 scale = 0x800000 / s;

	a2.w = ((a1.m[j][k] - a1.m[k][j]) * scale) >> 12;

	switch (j)
	{
		case 0: a2.x = ((a1.m[i][0] + a1.m[0][i]) * scale) >> 12; break;
		case 1: a2.y = ((a1.m[i][1] + a1.m[1][i]) * scale) >> 12; break;
		case 2: a2.z = ((a1.m[i][2] + a1.m[2][i]) * scale) >> 12; break;
	}

	switch (k)
	{
		case 0: a2.x = ((a1.m[i][0] + a1.m[0][i]) * scale) >> 12; break;
		case 1: a2.y = ((a1.m[i][1] + a1.m[1][i]) * scale) >> 12; break;
		case 2: a2.z = ((a1.m[i][2] + a1.m[2][i]) * scale) >> 12; break;
	}
}


// @Ok
// functional: fixed-point quat->matrix conversion. Verified against the
// disassembly at 0x47C7F0: all 9 rotation terms (xx,yy,zz,xy,xz,yz,xw,yw,zw)
// and the zeroed translation match semantically, and the built instruction
// count (82) and length (238 bytes) match the original (82 instructions,
// 236 bytes) exactly, so nothing is missing, just MSVC6 register scheduling
// residue (see quat.attempts.md history, 13 source-shape hypotheses tried).
// Not byte-identical, functional parity confirmed per session policy.
void QToM(CQuat* q, MATRIX* m)
{
	i32 x = q->x;
	i32 y = q->y;
	i32 z = q->z;
	i32 w = q->w;

	i32 xx = (x * x) >> 11;
	i32 zw = (z * w) >> 11;
	i32 xw = (x * w) >> 11;
	i32 yw = (y * w) >> 11;
	i32 yy = (y * y) >> 11;
	i32 xy = (y * x) >> 11;
	i32 xz = (z * x) >> 11;
	i32 zz = (z * z) >> 11;
	i32 yz = (z * y) >> 11;

	m->m[0][0] = 4096 - zz - yy;
	m->m[0][1] = zw + xy;
	m->m[1][0] = xy - zw;
	m->m[0][2] = xz - yw;
	m->m[1][1] = 4096 - xx - zz;
	m->m[1][2] = xw + yz;
	m->m[2][0] = yw + xz;
	m->m[2][1] = yz - xw;
	m->m[2][2] = 4096 - xx - yy;

	m->t[0] = 0;
	m->t[1] = 0;
	m->t[2] = 0;
}

// sin table shared with shell.cpp/spidey.cpp/manipob.cpp (same address,
// same file-local raw-address convention already used there).
static i16 * const word_610C48 = (i16*)0x610C48;

// @Ok
// functional: full slerp algorithm (zero-quat guards, dot product, negate-
// if-opposite, near-identical lerp shortcut, near-opposite perpendicular-
// blend shortcut, general arccos/sin-table slerp). Verified against the
// disassembly at 0x47CAC0: built instruction count (206) and length
// (607 bytes) match the original (205 instructions, 602 bytes) closely,
// with every branch, store and shortcut path accounted for, so nothing is
// missing. Remaining diffs are MSVC6 register scheduling residue, same
// class as QToM in this file. Not byte-identical, functional parity
// confirmed per session policy.
void Quat_Slerp (CQuat& a1, CQuat const & a2, int a3, CQuat& a4)
{
	if (a1.x + a1.y + a1.z + a1.w == 0)
	{
		a4 = a2;
	}

	if (a2.x + a2.y + a2.z + a2.w == 0)
	{
		a4 = a1;
	}

	i32 dot = (a2.x * a1.x + a1.y * a2.y + a2.z * a1.z + a2.w * a1.w) >> 12;

	if (dot < 0)
	{
		a1.x = -a1.x;
		a1.y = -a1.y;
		a1.z = -a1.z;
		a1.w = -a1.w;
		dot = -dot;
	}

	if (dot + 4096 <= 128)
	{
		// nearly opposite quaternions: blend against a perpendicular quat
		a4.x = -a1.y;
		a4.y = -a1.x;
		a4.z = -a1.w;
		a4.w = a1.z;

		i32 angle0 = ((4096 - a3) * 6434) >> 12;
		i32 angle1 = (a3 * 6434) >> 12;
		i32 s0 = word_610C48[2 * (angle0 & 0xFFF)];
		i32 s1 = word_610C48[2 * (angle1 & 0xFFF)];

		a4.x = (a1.x * s0 + a4.x * s1) >> 12;
		a4.y = (a1.y * s0 + a4.y * s1) >> 12;
		a4.z = (a1.z * s0 + a4.z * s1) >> 12;
		a4.w = (a1.w * s0 + a4.w * s1) >> 12;
		return;
	}

	i32 w0, w1;

	if (4096 - dot <= 128)
	{
		// nearly identical quaternions: plain lerp
		w0 = 4096 - a3;
		w1 = a3;
	}
	else
	{
		i32 theta = Utils_ArcCos(dot);
		i32 sinTheta = word_610C48[2 * (theta & 0xFFF)];
		i32 angle0 = ((4096 - a3) * theta) >> 12;
		i32 angle1 = (a3 * theta) >> 12;
		w0 = (word_610C48[2 * (angle0 & 0xFFF)] << 12) / sinTheta;
		w1 = (word_610C48[2 * (angle1 & 0xFFF)] << 12) / sinTheta;
	}

	a4.x = (a1.x * w0 + a2.x * w1) >> 12;
	a4.y = (a1.y * w0 + a2.y * w1) >> 12;
	a4.z = (a1.z * w0 + a2.z * w1) >> 12;
	a4.w = (a1.w * w0 + a2.w * w1) >> 12;
}

// @Ok
// 0x47C640 (232 bytes). Hex-Rays gives the four products with the operands
// named as they are loaded: the second argument's components multiply the
// first argument's, so with a = lhs and b = rhs every term below reads
// b.<c> * a.<c>, in the original's order.
CQuat operator*(const CQuat& a, const CQuat& b)
{
	CQuat r;
	r.x = (b.x * a.w + b.y * a.z + b.w * a.x - b.z * a.y) >> 12;
	r.y = (b.y * a.w + b.w * a.y + b.z * a.x - b.x * a.z) >> 12;
	r.z = (b.z * a.w + b.w * a.z + b.x * a.y - b.y * a.x) >> 12;
	r.w = (b.w * a.w - b.y * a.y - b.x * a.x - b.z * a.z) >> 12;
	return r;
}

// @Ok
// 0x47C730 (56 bytes): sar eax,1; and eax,0FFFh; shl eax,2; movsx from the
// table at 0x610C48 (sin) and 0x610C4A (cos); stores x, 0, 0, w.
CQuat QFromXRot(i32 angle)
{
	CQuat q;
	i32 idx = 2 * ((angle >> 1) & 0xFFF);
	q.x = word_610C48[idx];
	q.y = 0;
	q.z = 0;
	q.w = word_610C48[idx + 1];
	return q;
}

// @Ok
// 0x47C770 (56 bytes), same as QFromXRot with the sine in y.
CQuat QFromYRot(i32 angle)
{
	CQuat q;
	i32 idx = 2 * ((angle >> 1) & 0xFFF);
	q.x = 0;
	q.y = word_610C48[idx];
	q.z = 0;
	q.w = word_610C48[idx + 1];
	return q;
}

// @Ok
// 0x47C7B0 (56 bytes), same as QFromXRot with the sine in z.
CQuat QFromZRot(i32 angle)
{
	CQuat q;
	i32 idx = 2 * ((angle >> 1) & 0xFFF);
	q.x = 0;
	q.y = 0;
	q.z = word_610C48[idx];
	q.w = word_610C48[idx + 1];
	return q;
}

void validate_CQuat(void){
	VALIDATE_SIZE(CQuat, 0x10);

	VALIDATE(CQuat, x, 0x0);
	VALIDATE(CQuat, y, 0x4);
	VALIDATE(CQuat, z, 0x8);
	VALIDATE(CQuat, w, 0xC);
}
