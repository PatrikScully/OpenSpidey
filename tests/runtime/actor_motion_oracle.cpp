// Optional Linux/i386 regression against the user's original executable.
#include "engine/world/actor_motion_integrator.h"
#include "core/wrapping_math.h"
#include <cassert>
#include <cstdio>
#include <cstring>
#include <fstream>
#include <iterator>
#include <vector>
#include "original_executable.h"

namespace {
using original_executable::loadOriginalImage;
void runOriginal(u32 entry, void* body) {
    static_assert(sizeof(void*) == 4, "Compile this fixture with -m32.");
    // These two original methods use thiscall and invoke only integer/vector
    // arithmetic. They do not require Windows imports, SEH, TLS or game globals.
    asm volatile("call *%1" : "+c"(body) : "r"(entry) : "eax", "edx", "memory", "cc");
}
template<class Value> void put(u8* body, std::size_t offset, const Value& value) {
    std::memcpy(body + offset, &value, sizeof(value));
}
u32 randomBits() {
    static u32 state = 0xC19A8B27;
    state = state * 1664525u + 1013904223u;
    return state;
}
}

int main(int argc, char** argv) {
    assert(argc == 2);
    loadOriginalImage(argv[1]);
    const u8 catPrologue[] = {0x83, 0xEC, 0x4C};
    const u8 clonePrologue[] = {0x83, 0xEC, 0x5C};
    assert(!std::memcmp(reinterpret_cast<void*>(0x414B10), catPrologue, 3));
    assert(!std::memcmp(reinterpret_cast<void*>(0x4B0D80), clonePrologue, 3));
    const i32 elapsedCases[] = {-1, 0, 1, 2, 3, 4, 6, 7};
    const u32 scaleCases[] = {0, 1, 4096, 8190, 8192, 24576, 65535, 0xFFFFF000, 0xFFFFFFFF};
    for (int actor = 0; actor < 2; ++actor) {
        for (int sample = 0; sample < 2000; ++sample) {
            CVector position(spidey::core::signedWord(randomBits()),
                spidey::core::signedWord(randomBits()), spidey::core::signedWord(randomBits()));
            CVector velocity(spidey::core::signedWord(randomBits()),
                spidey::core::signedWord(randomBits()), spidey::core::signedWord(randomBits()));
            CVector acceleration(99, -101, 103);
            CSVector angles(spidey::core::signedHalfWord(randomBits()),
                spidey::core::signedHalfWord(randomBits()), spidey::core::signedHalfWord(randomBits()));
            CSVector angularVelocity(spidey::core::signedHalfWord(randomBits()),
                spidey::core::signedHalfWord(randomBits()), spidey::core::signedHalfWord(randomBits()));
            CSVector angularAcceleration(spidey::core::signedHalfWord(randomBits()),
                spidey::core::signedHalfWord(randomBits()), spidey::core::signedHalfWord(randomBits()));
            CFriction friction(static_cast<u8>(randomBits()), static_cast<u8>(randomBits()),
                static_cast<u8>(randomBits()));
            i32 gravity = spidey::core::signedWord(randomBits());
            i32 elapsed = elapsedCases[sample % 8];
            u32 scale = scaleCases[sample % 9];
            u8 original[0x354] = {};
            put(original, 0x08, position);
            put(original, 0x14, angles);
            put(original, 0x60, velocity);
            put(original, 0x6C, acceleration);
            put(original, 0x80, elapsed);
            put(original, 0x88, angularVelocity);
            put(original, 0x8E, angularAcceleration);
            put(original, 0x94, friction);
            put(original, actor ? 0x328 : 0x32C, gravity);
            if (actor) put(original, 0x334, scale);
            u8 rewritten[sizeof(original)];
            std::memcpy(rewritten, original, sizeof(original));
            spidey::world::ActorMotionState state = {position, velocity, acceleration,
                angles, angularVelocity, angularAcceleration, friction};
            spidey::world::ActorMotionIntegrator motion(state);
            if (actor) motion.advanceFixedStep(scale, gravity);
            else motion.advanceTwoTickSteps(elapsed, gravity);
            put(rewritten, 0x08, position);
            put(rewritten, 0x14, angles);
            put(rewritten, 0x60, velocity);
            put(rewritten, 0x6C, acceleration);
            put(rewritten, 0x88, angularVelocity);
            runOriginal(actor ? 0x4B0D80 : 0x414B10, original);
            if (std::memcmp(original, rewritten, sizeof(original))) {
                std::fprintf(stderr, "Mismatch actor=%d sample=%d elapsed=%d scale=%u\n",
                    actor, sample, elapsed, scale);
                for (unsigned byte = 0; byte < sizeof(original); ++byte)
                    if (original[byte] != rewritten[byte])
                        std::fprintf(stderr, "offset=%#x original=%#x rewritten=%#x\n",
                            byte, original[byte], rewritten[byte]);
                return 1;
            }
        }
    }
    std::puts("PASS 4000 actor motion cases against original executable instructions");
}
