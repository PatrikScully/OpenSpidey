#include "core/fixed_vector.h"
#include <cassert>
#include <limits>

static void expect(const CVector& value, i32 x, i32 y, i32 z) {
    assert(value.vx == x && value.vy == y && value.vz == z);
}
int main() {
    static_assert(sizeof(CVector) == 12 && sizeof(CSVector) == 6,
        "Vector binary layouts must remain unchanged.");
    const i32 min = std::numeric_limits<i32>::min();
    const i32 max = std::numeric_limits<i32>::max();
    expect(CVector(7), 7, 0, 0);
    expect(CVector(1, 2, 3) + CVector(4, 8, 16), 5, 10, 19);
    expect(CVector(max, min, -1) + CVector(1, -1, 1), min, max, 0);
    expect(CVector(min, max, 0) - CVector(1, -1, min), max, min, min);
    expect(CVector(max, min, -3) * 2, -2, 0, -6);
    expect(2 * CVector(max, min, -3), -2, 0, -6);
    expect(CVector(-7, 7, -1) / 2, -3, 3, 0);
    expect(CVector(-7, 7, min) >> 33, -4, 3, min / 2);
    expect(CVector(-1, max, min) << 33, -2, -2, 0);
    CVector vector(max, -5, min);
    assert((vector += vector) == &vector);
    expect(vector, -2, -10, 0);
    vector -= vector;
    expect(vector, 0, 0, 0);
    vector.Set(-5, 9, min);
    vector %= CFriction(1, 32, 0);
    expect(vector, -2, 0, 0);
    vector.Set(-2048, 2048, 2049);
    vector.KillSmall();
    expect(vector, 0, 0, 2049);
    assert(CVector(max, min, -1).SquaredLength() == 2);
    CSVector angles(32767, -32768, -1);
    angles += CSVector(1, -1, 1);
    assert(angles.vx == -32768 && angles.vy == 32767 && angles.vz == 0);
    angles = CSVector(32767, -32768, -3) * max;
    assert(angles.vx == -32767 && angles.vy == -32768 && angles.vz == 3);
    angles = CSVector(-5, 9, -32768);
    angles %= CFriction(1, 33, 0);
    assert(angles.vx == -2 && angles.vy == 5 && angles.vz == 0);
    angles = CSVector(-1, 4096, 4097);
    angles.Mask();
    assert(angles.vx == 4095 && angles.vy == 0 && angles.vz == 1);
    angles = CSVector(-1, 1, 2);
    angles.KillSmall();
    assert(angles.vx == 0 && angles.vy == 0 && angles.vz == 2);
}
