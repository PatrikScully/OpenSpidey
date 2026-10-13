#include "core/fixed_vector.h"
#include "original_executable.h"
#include <cstdio>
#include <limits>

namespace {
u32 randomBits() {
    static u32 state = 0x718ADB25;
    state = state * 1664525u + 1013904223u;
    return state;
}
CVector randomVector() {
    using spidey::core::signedWord;
    return CVector(signedWord(randomBits()), signedWord(randomBits()), signedWord(randomBits()));
}
template<class VectorType, class Argument>
void compare(u32 entry, const VectorType& vector, const Argument& argument, const VectorType& expected) {
    VectorType actual;
    typedef void (*Original)(VectorType*, const VectorType*, const Argument*);
    reinterpret_cast<Original>(entry)(&actual, &vector, &argument);
    assert(!std::memcmp(&actual, &expected, sizeof(actual)));
}
}
int main(int argc, char** argv) {
    static_assert(sizeof(void*) == 4, "Build this optional fixture with -m32 -fno-pie -no-pie.");
    assert(argc == 2);
    original_executable::loadOriginalImage(argv[1]);
    const u8 prologue[] = {0x8B, 0x44, 0x24, 0x08};
    assert(!std::memcmp(reinterpret_cast<void*>(0x4E7720), prologue, 4));
    for (int sample = 0; sample < 10000; ++sample) {
        CVector left = randomVector(), right = randomVector();
        i32 scalar = spidey::core::signedWord(randomBits());
        compare(0x4E7720, left, right, left + right);
        compare(0x4E7760, left, right, left - right);
        compare(0x4E77A0, left, scalar, left * scalar);
        compare(0x4E77D0, left, right, left.vx * right);
        compare(0x4E7840, left, scalar, left >> scalar);
        compare(0x4E7870, left, scalar, left << scalar);
        // Preserve the original idiv domain; zero and INT_MIN/-1 trap there.
        i32 divisor = (scalar == 0 || scalar == -1) ? 2 : scalar;
        compare(0x4E7800, left, divisor, left / divisor);
        CSVector shortLeft(spidey::core::signedHalfWord(left.vx),
            spidey::core::signedHalfWord(left.vy), spidey::core::signedHalfWord(left.vz));
        CSVector shortRight(spidey::core::signedHalfWord(right.vx),
            spidey::core::signedHalfWord(right.vy), spidey::core::signedHalfWord(right.vz));
        compare(0x4E7A40, shortLeft, scalar, shortLeft * scalar);
        compare(0x4E79F0, shortLeft, shortRight, shortLeft - shortRight);
        compare(0x4E7AE0, shortLeft, divisor, shortLeft / divisor);
    }
    std::puts("PASS: 100000 vector results match original executable arithmetic");
}
