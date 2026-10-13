#pragma once


#ifndef QUAT_H
#define QUAT_H

#include "compatibility/export.h"

struct MATRIX;

class CQuat {
	public:

		// @Ok
		EXPORT inline CQuat(void)
		{
			this->x = 0;
			this->y = 0;
			this->z = 0;
			this->w = 4096;
		}

		i32 x,y,z,w;

};

void validate_CQuat(void);

EXPORT void Quat_Slerp (CQuat& a1, CQuat const & a2, int a3, CQuat& a4);
EXPORT void MToQ(MATRIX const &, CQuat&);
EXPORT void QToM(CQuat*, MATRIX*);

// 0x47C640 (Mac: __ml(CQuat const &,CQuat const &)), quaternion product
// in 12-bit fixed point, returned by value.
EXPORT CQuat operator*(const CQuat&, const CQuat&);

// 0x47C730 / 0x47C770 / 0x47C7B0. Rotation quaternion about one axis from an
// angle in 4096ths of a turn (sin/cos of the half angle from the shared
// table). The Mac build declares the angle as short; the PC code reads the
// full 32-bit argument, so i32 here.
EXPORT CQuat QFromXRot(i32);
EXPORT CQuat QFromYRot(i32);
EXPORT CQuat QFromZRot(i32);

#endif
