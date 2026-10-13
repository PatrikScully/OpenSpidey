// Optional Linux/i386 comparison with original DoCrawlingPhysics. Hook sampling
// captures movement before collision; world callbacks return no surface hits.
#include "game/player/crawl_motion.h"
#include "original_executable.h"
#include <cstdio>
#include <cstdint>

namespace {
CVector sampledPosition, sampledVelocity;
int hookCalls;
u32 randomBits() {
    static u32 state = 0xA197253C;
    state = state * 1664525u + 1013904223u;
    return state;
}
CVector randomVector() {
    using spidey::core::signedWord;
    return CVector(signedWord(randomBits()), signedWord(randomBits()), signedWord(randomBits()));
}
template<class Value> void put(void* object, unsigned offset, const Value& value) {
    std::memcpy(static_cast<u8*>(object) + offset, &value, sizeof(value));
}
void __attribute__((force_align_arg_pointer)) sampleHook(CVector* output, void* player, int hook) {
    assert(hook == 2);
    ++hookCalls;
    std::memcpy(&sampledPosition, static_cast<u8*>(player) + 8, sizeof(sampledPosition));
    std::memcpy(&sampledVelocity, static_cast<u8*>(player) + 0x60, sizeof(sampledVelocity));
    *output = sampledPosition;
}
void __attribute__((force_align_arg_pointer)) initLine(void* line) { put(line, 0x68, u32(0)); }
void __attribute__((force_align_arg_pointer)) traceLine(void*, int mode) { assert(mode == 1); }
void redirect(u32 address, void* callback) {
    *reinterpret_cast<u8*>(address) = 0xE9;
    put(reinterpret_cast<void*>(address), 1,
        u32(reinterpret_cast<uintptr_t>(callback)) - (address + 5));
}
}
int main(int argc, char** argv) {
    static_assert(sizeof(void*) == 4, "Build with -m32 -fno-pie -no-pie.");
    assert(argc == 2);
    original_executable::loadOriginalImage(argv[1]);
    const u8 prologue[] = {0x81, 0xEC, 0xAC, 0x01, 0x00, 0x00};
    assert(!std::memcmp(reinterpret_cast<void*>(0x467FD0), prologue, sizeof(prologue)));
    redirect(0x453D60, reinterpret_cast<void*>(&sampleHook));
    redirect(0x4524C0, reinterpret_cast<void*>(&initLine));
    redirect(0x4549A0, reinterpret_cast<void*>(&traceLine));
    typedef void (__attribute__((thiscall)) *Original)(void*);
    const i32 elapsed[] = {-1, 0, 1, 2, 3, 4, 6, 7, 0x7FFFFFFF};
    for (int sample = 0; sample < 10000; ++sample) {
        alignas(4) u8 player[0x1000] = {};
        CVector position = randomVector(), velocity = randomVector(), acceleration = randomVector();
        CFriction friction(randomBits(), randomBits(), randomBits());
        if (sample % 10 == 0) { velocity.Zero(); acceleration.Zero(); }
        put(player, 8, position);
        put(player, 0x60, velocity);
        put(player, 0x6C, acceleration);
        put(player, 0x78, friction);
        put(player, 0x80, elapsed[sample % 9]);
        put(player, 0xAD4, u8(1));
        hookCalls = 0;
        reinterpret_cast<Original>(0x467FD0)(player);
        assert(hookCalls == 1);
        spidey::player::CrawlMotion::advance(position, velocity, acceleration, friction, elapsed[sample % 9]);
        assert(!std::memcmp(&position, &sampledPosition, sizeof(position)));
        assert(!std::memcmp(&velocity, &sampledVelocity, sizeof(velocity)));
    }
    std::puts("PASS: 10000 crawl movement steps match original executable instructions");
}
