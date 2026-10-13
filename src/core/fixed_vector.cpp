#include "core/fixed_vector.h"
#include "core/wrapping_math.h"

using namespace spidey::core;

CVector operator+(const CVector& lhs, const CVector& other) {
    return CVector(wrappingAdd(lhs.vx, other.vx), wrappingAdd(lhs.vy, other.vy), wrappingAdd(lhs.vz, other.vz));
}

CVector* CVector::operator+=(const CVector& other) {
    vx = wrappingAdd(vx, other.vx);
    vy = wrappingAdd(vy, other.vy);
    vz = wrappingAdd(vz, other.vz);
    return this;
}

CVector operator-(const CVector& lhs, const CVector& other) {
    return CVector(wrappingSubtract(lhs.vx, other.vx), wrappingSubtract(lhs.vy, other.vy), wrappingSubtract(lhs.vz, other.vz));
}

CVector* CVector::operator-=(const CVector& other) {
    vx = wrappingSubtract(vx, other.vx);
    vy = wrappingSubtract(vy, other.vy);
    vz = wrappingSubtract(vz, other.vz);
    return this;
}

CVector operator*(const CVector& lhs, const int& other) {
    return CVector(wrappingMultiply(lhs.vx, other), wrappingMultiply(lhs.vy, other), wrappingMultiply(lhs.vz, other));
}

CVector* CVector::operator*=(const int& other) {
    vx = wrappingMultiply(vx, other);
    vy = wrappingMultiply(vy, other);
    vz = wrappingMultiply(vz, other);
    return this;
}

CVector operator/(const CVector& lhs, const int& other) {
    return CVector(lhs.vx / other, lhs.vy / other, lhs.vz / other);
}

CVector* CVector::operator/=(const int& other) {
    vx = vx / other;
    vy = vy / other;
    vz = vz / other;
    return this;
}

CVector operator>>(const CVector& lhs, const int& other) {
    return CVector(arithmeticShiftRight(lhs.vx, other), arithmeticShiftRight(lhs.vy, other), arithmeticShiftRight(lhs.vz, other));
}

CVector* CVector::operator>>=(const int& other) {
    vx = arithmeticShiftRight(vx, other);
    vy = arithmeticShiftRight(vy, other);
    vz = arithmeticShiftRight(vz, other);
    return this;
}

CVector operator<<(const CVector& lhs, const int& other) {
    return CVector(wrappingShiftLeft(lhs.vx, other), wrappingShiftLeft(lhs.vy, other), wrappingShiftLeft(lhs.vz, other));
}

CVector* CVector::operator<<=(const int& other) {
    vx = wrappingShiftLeft(vx, other);
    vy = wrappingShiftLeft(vy, other);
    vz = wrappingShiftLeft(vz, other);
    return this;
}

CVector* CVector::operator%=(const CFriction& friction) {
    vx = wrappingSubtract(vx, arithmeticShiftRight(vx, friction.vx));
    vy = wrappingSubtract(vy, arithmeticShiftRight(vy, friction.vy));
    vz = wrappingSubtract(vz, arithmeticShiftRight(vz, friction.vz));
    return this;
}

void CVector::KillSmall() {
    if (vx >= -2048 && vx <= 2048) vx = 0;
    if (vy >= -2048 && vy <= 2048) vy = 0;
    if (vz >= -2048 && vz <= 2048) vz = 0;
}

CSVector* CSVector::operator+=(const CSVector& other) {
    vx = signedHalfWord(wrappingAdd(vx, other.vx));
    vy = signedHalfWord(wrappingAdd(vy, other.vy));
    vz = signedHalfWord(wrappingAdd(vz, other.vz));
    return this;
}

CSVector operator-(const CSVector& lhs, const CSVector& other) {
    return CSVector(signedHalfWord(wrappingSubtract(lhs.vx, other.vx)), signedHalfWord(wrappingSubtract(lhs.vy, other.vy)), signedHalfWord(wrappingSubtract(lhs.vz, other.vz)));
}

CSVector operator*(const CSVector& lhs, const int& other) {
    return CSVector(signedHalfWord(wrappingMultiply(lhs.vx, other)), signedHalfWord(wrappingMultiply(lhs.vy, other)), signedHalfWord(wrappingMultiply(lhs.vz, other)));
}

CSVector operator/(const CSVector& lhs, const int& other) {
    return CSVector(signedHalfWord(lhs.vx / other), signedHalfWord(lhs.vy / other), signedHalfWord(lhs.vz / other));
}

CSVector* CSVector::operator/=(const int& other) {
    vx = signedHalfWord(vx / other);
    vy = signedHalfWord(vy / other);
    vz = signedHalfWord(vz / other);
    return this;
}

CSVector* CSVector::operator%=(const CFriction& friction) {
    vx = signedHalfWord(wrappingSubtract(vx, arithmeticShiftRight(vx, friction.vx)));
    vy = signedHalfWord(wrappingSubtract(vy, arithmeticShiftRight(vy, friction.vy)));
    vz = signedHalfWord(wrappingSubtract(vz, arithmeticShiftRight(vz, friction.vz)));
    return this;
}

void CSVector::KillSmall() {
    if (vx >= -1 && vx <= 1) vx = 0;
    if (vy >= -1 && vy <= 1) vy = 0;
    if (vz >= -1 && vz <= 1) vz = 0;
}

CVector operator*(const int& scalar, const CVector& vector) { return vector * scalar; }

i32 CVector::operator!=(const CVector& other) {
    return vx != other.vx || vy != other.vy || vz != other.vz;
}

void CVector::Zero() { Set(0, 0, 0); }

void CSVector::Mask() {
    vx &= 0xFFF;
    vy &= 0xFFF;
    vz &= 0xFFF;
}
