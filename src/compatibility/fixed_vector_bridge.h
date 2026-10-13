#pragma once
#include "core/fixed_vector.h"

// Original executable ABI: left.vx is a scalar, not a vector product.
// Game code uses scalar multiplication explicitly.
EXPORT CVector operator*(const CVector& left, const CVector& right);
