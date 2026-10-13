#include "compatibility/fixed_vector_bridge.h"
#include "compatibility/validate.h"
#include "compatibility/my_patch.h"
#include "engine/rendering/ps2funcs.h"

i32 CVector::LengthSquared(void)
{
	CVector tmp;

	tmp.vx = this->vx >> 12;
	tmp.vy = this->vy >> 12;
	tmp.vz = this->vz >> 12;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&tmp));
	gte_sqr0();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&tmp));

	return tmp.vx + tmp.vy + tmp.vz;
}

int CVector::Length(void)
{
	CVector v4;

	v4.vx = this->vx >> 12;
	v4.vy = this->vy >> 12;
	v4.vz = this->vz >> 12;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&v4));
	gte_sqr0();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&v4));

	return M3dMaths_SquareRoot0(v4.vx + v4.vy + v4.vz);
}

CVector operator*(const CVector& left, const CVector& right) {
    return left.vx * right;
}

void validate_CVector(void)
{
	VALIDATE(CVector, vx, 0);
	VALIDATE(CVector, vy, 4);
	VALIDATE(CVector, vz, 8);

	VALIDATE(Vector, vx, 0);
	VALIDATE(Vector, vy, 4);
	VALIDATE(Vector, vz, 8);
}

void validate_CSVector(void)
{
	VALIDATE(CSVector, vx, 0);
	VALIDATE(CSVector, vy, 2);
	VALIDATE(CSVector, vz, 4);
}

void validate_SVector(void){
	VALIDATE(SVector, vx, 0);
	VALIDATE(SVector, vy, 2);
	VALIDATE(SVector, vz, 4);
}

void patch_vector(void)
{
	// Preserve the original entry points and MSVC export names. Length methods
	// use the shared GTE emulation state; arithmetic methods are implemented in core.
	//
	// Not hooked: CVector::CVector and the Set/SetX/SetY/SetZ family are INLINE
	// in vector.h with no standalone body; CVector::Zero, operator*(int, CVector)
	// and CSVector's operator/= and operator%= have no named address in the
	// original (operator*(int, CVector) has the same body as
	// operator*(CVector, int) and was most likely folded into 0x004E77A0 at link
	// time, so hooking it would mean guessing).
	PATCH_PUSH_RET_POLY(0x004E74A0, CVector::Length,        "?Length@CVector@@QAEHXZ");
	PATCH_PUSH_RET_POLY(0x004E7500, CVector::LengthSquared, "?LengthSquared@CVector@@QAEHXZ");
	PATCH_PUSH_RET_POLY(0x004E7550, CVector::KillSmall,     "?KillSmall@CVector@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004E7590, CVector::operator_add_assign, "??YCVector@@QAEPAV0@ABV0@@Z");
	PATCH_PUSH_RET_POLY(0x004E75C0, CVector::operator_sub_assign, "??ZCVector@@QAEPAV0@ABV0@@Z");
	PATCH_PUSH_RET_POLY(0x004E75F0, CVector::operator_mul_assign, "??XCVector@@QAEPAV0@ABH@Z");
	PATCH_PUSH_RET_POLY(0x004E7620, CVector::operator_div_assign, "??_0CVector@@QAEPAV0@ABH@Z");
	PATCH_PUSH_RET_POLY(0x004E7650, CVector::operator_shr_assign, "??_2CVector@@QAEPAV0@ABH@Z");
	PATCH_PUSH_RET_POLY(0x004E7680, CVector::operator_shl_assign, "??_3CVector@@QAEPAV0@ABH@Z");
	PATCH_PUSH_RET_POLY(0x004E76B0, CVector::operator_mod_assign, "??_1CVector@@QAEPAV0@ABVCFriction@@@Z");
	PATCH_PUSH_RET_POLY(0x004E76F0, CVector::operator_not_equal,  "??9CVector@@QAEHABV0@@Z");
	PATCH_PUSH_RET_POLY(0x004E7720, operator_add_vec_vec, "??H@YA?AVCVector@@ABV0@0@Z");
	PATCH_PUSH_RET_POLY(0x004E7760, operator_sub_vec_vec, "??G@YA?AVCVector@@ABV0@0@Z");
	PATCH_PUSH_RET_POLY(0x004E77A0, operator_mul_vec_int, "??D@YA?AVCVector@@ABV0@ABH@Z");
	PATCH_PUSH_RET_POLY(0x004E77D0, operator_mul_vec_vec, "??D@YA?AVCVector@@ABV0@0@Z");
	PATCH_PUSH_RET_POLY(0x004E7800, operator_div_vec_int, "??K@YA?AVCVector@@ABV0@ABH@Z");
	PATCH_PUSH_RET_POLY(0x004E7840, operator_shr_vec_int, "??5@YA?AVCVector@@ABV0@ABH@Z");
	PATCH_PUSH_RET_POLY(0x004E7870, operator_shl_vec_int, "??6@YA?AVCVector@@ABV0@ABH@Z");
	PATCH_PUSH_RET_POLY(0x004E78A0, CSVector::Mask,       "?Mask@CSVector@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004E78C0, CSVector::KillSmall,  "?KillSmall@CSVector@@QAEXXZ");
	PATCH_PUSH_RET_POLY(0x004E7900, CSVector::operator_add_assign, "??YCSVector@@QAEPAV0@ABV0@@Z");
}
