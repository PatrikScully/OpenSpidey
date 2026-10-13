#pragma once
#include "core/types.h"
#include "core/friction.h"
#include "core/wrapping_math.h"

// Original packed layouts remain part of the executable ABI. Coordinates
// conventionally use 12-bit fixed point; angles use a 4096-unit turn.
struct Vector { i32 vx, vy, vz; };
struct SVector { i16 vx, vy, vz; };

class CVector {
public:
    i32 vx, vy, vz;

    EXPORT CVector() : vx(0), vy(0), vz(0) {}
    EXPORT CVector(i32 x, i32 y, i32 z) : vx(x), vy(y), vz(z) {}
    EXPORT explicit CVector(i32 x) : vx(x), vy(0), vz(0) {}
    EXPORT void Set(i32 x, i32 y, i32 z) { vx = x; vy = y; vz = z; }
    EXPORT void Set(CVector* vector) { Set(vector->vx, vector->vy, vector->vz); }
    EXPORT void SetX(i32 value) { vx = value; }
    EXPORT void SetY(i32 value) { vy = value; }
    EXPORT void SetZ(i32 value) { vz = value; }
    EXPORT void Zero();
    EXPORT void KillSmall(); // Inclusive dead zone [-2048, 2048].

    // Pointer return values are retained for the original exported ABI.
    EXPORT CVector* operator+=(const CVector& other);
    EXPORT CVector* operator-=(const CVector& other);
    EXPORT CVector* operator*=(const int& scalar);
    EXPORT CVector* operator/=(const int& divisor);
    EXPORT CVector* operator>>=(const int& count);
    EXPORT CVector* operator<<=(const int& count);
    EXPORT CVector* operator%=(const CFriction& friction);
    EXPORT i32 operator!=(const CVector& other);

    // Compatibility bridge: convert to world units, then use shared GTE math.
    EXPORT i32 LengthSquared();
    EXPORT int Length();
    // Raw fixed-point components, with 32-bit wrapping rather than conversion.
    i32 SquaredLength() const {
        using namespace spidey::core;
        return wrappingAdd(wrappingAdd(wrappingMultiply(vx, vx),
            wrappingMultiply(vy, vy)), wrappingMultiply(vz, vz));
    }
};

EXPORT CVector operator+(const CVector& left, const CVector& right);
EXPORT CVector operator-(const CVector& left, const CVector& right);
EXPORT CVector operator*(const CVector& vector, const int& scalar);
EXPORT CVector operator*(const int& scalar, const CVector& vector);
EXPORT CVector operator/(const CVector& vector, const int& divisor);
EXPORT CVector operator>>(const CVector& vector, const int& count);
EXPORT CVector operator<<(const CVector& vector, const int& count);

class CSVector {
public:
    i16 vx, vy, vz;

    EXPORT CSVector() : vx(0), vy(0), vz(0) {}
    EXPORT CSVector(i16 x, i16 y, i16 z) : vx(x), vy(y), vz(z) {}
    EXPORT void Set(i16 x, i16 y, i16 z) { vx = x; vy = y; vz = z; }
    EXPORT CSVector* operator+=(const CSVector& other);
    EXPORT CSVector* operator/=(const int& divisor);
    EXPORT CSVector* operator%=(const CFriction& friction);
    EXPORT void Mask(); // Normalize each component to one 4096-unit turn.
    EXPORT void KillSmall(); // Inclusive dead zone [-1, 1].
};

EXPORT CSVector operator*(const CSVector& vector, const int& scalar);
EXPORT CSVector operator-(const CSVector& left, const CSVector& right);
EXPORT CSVector operator/(const CSVector& vector, const int& divisor);

void validate_CVector();
void validate_CSVector();
void validate_SVector();
void patch_vector();
