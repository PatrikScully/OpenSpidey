#pragma once

#ifndef ALGEBRA_H
#define ALGEBRA_H

#include "compatibility/export.h"
#include "core/types.h"

// 3-float vector (float, not fixed-point). Used by the Display*List
// projection code in bit.cpp.
struct SFloat3
{
	f32 x;
	f32 y;
	f32 z;
	EXPORT SFloat3* Copy(const SFloat3* src);
};

// 4x4 matrix transform. out = M * in, M is the column-major matrix at
// 0x56E668 (16 floats). in/out are 4-float vectors (x,y,z,w).
EXPORT f32* Algebra_Transform4(f32* out, f32* in);

#endif
