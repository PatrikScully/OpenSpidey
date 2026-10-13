// Linux/i386 oracle for swing geometry and ordered collision response. Only
// hook sampling and world tracing are replaced with deterministic callbacks.
#include "game/player/swing_arc.h"
#include "game/player/swing_collision_resolver.h"
#include "original_executable.h"
#include <cstdio>
#include <cstdint>

namespace {
using namespace spidey::player;
CVector hooks[2], starts[3], ends[3];
SwingSurfaceHit surface;
int hookCalls, traceCalls, hitOn;
u32 randomBits() {
    static u32 state = 0x8910ABCD;
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
template<class Value> Value get(const void* object, unsigned offset) {
    Value value;
    std::memcpy(&value, static_cast<const u8*>(object) + offset, sizeof(value));
    return value;
}
void __attribute__((force_align_arg_pointer)) sampleHook(CVector* output, void*, int hook) {
    assert(hook == 2 && hookCalls < 2);
    *output = hooks[hookCalls++];
}
void __attribute__((force_align_arg_pointer)) initLine(void* line) {
    put(line, 0x68, u32(0));
}
void __attribute__((force_align_arg_pointer)) traceLine(void* line, int mode) {
    assert(mode == 1 && traceCalls < 3);
    starts[traceCalls] = get<CVector>(line, 0);
    ends[traceCalls] = get<CVector>(line, 12);
    ++traceCalls;
    if (traceCalls != hitOn) return;
    put(line, 0x68, u32(1));
    put(line, 0x6C, surface.position);
    put(line, 0x78, surface.normal);
}
void redirect(u32 address, void* callback) {
    *reinterpret_cast<u8*>(address) = 0xE9;
    put(reinterpret_cast<void*>(address), 1,
        u32(reinterpret_cast<uintptr_t>(callback)) - (address + 5));
}
class Probe : public SwingCollisionProbe {
public:
    int calls;
    Probe() : calls(0) {}
    bool trace(const CVector& start, const CVector& end, SwingSurfaceHit& hit) override {
        assert(calls < traceCalls);
        assert(!std::memcmp(&start, &starts[calls], sizeof(start)));
        assert(!std::memcmp(&end, &ends[calls], sizeof(end)));
        ++calls;
        if (calls != hitOn) return false;
        hit = surface;
        return true;
    }
};
}
int main(int argc, char** argv) {
    static_assert(sizeof(void*) == 4, "Build with -m32 -fno-pie -no-pie.");
    assert(argc == 2);
    original_executable::loadOriginalImage(argv[1]);
    const u8 prologue[] = {0x83, 0xEC, 0x30};
    assert(!std::memcmp(reinterpret_cast<void*>(0x467D20), prologue, 3));
    redirect(0x453D60, reinterpret_cast<void*>(&sampleHook));
    redirect(0x4524C0, reinterpret_cast<void*>(&initLine));
    redirect(0x4549A0, reinterpret_cast<void*>(&traceLine));
    typedef void (__attribute__((thiscall)) *Original)(void*);
    const Original advance = reinterpret_cast<Original>(0x467D20);
    for (int sample = 0; sample < 10000; ++sample) {
        alignas(4) u8 player[0x1000] = {}, swinger[0x190] = {};
        CVector previous = randomVector(), anchor = randomVector(), down = randomVector();
        CVector position, velocity = randomVector();
        i32 length = spidey::core::signedWord(randomBits());
        u16 clearance = randomBits(), flags = randomBits();
        hooks[0] = randomVector(); hooks[1] = randomVector();
        surface.position = randomVector();
        surface.normal.Set(spidey::core::signedHalfWord(randomBits()),
            sample % 2 ? -2600 : -2601, spidey::core::signedHalfWord(randomBits()));
        hitOn = sample % 4;
        hookCalls = traceCalls = 0;
        put(swinger, 0x108, length);
        put(swinger, 0x10C, anchor);
        put(swinger, 0x170, down);
        put(player, 8, previous);
        put(player, 0x60, velocity);
        put(player, 0xE0, flags);
        put(player, 0xE64, swinger + 0);
        put(player, 0xEA8, clearance);
        advance(player);
        assert(hookCalls == 2);
        position = SwingArc::endPosition(anchor, length, down);
        Probe probe;
        SwingCollisionResolver(position, velocity, flags, probe).resolve(previous, hooks[0], hooks[1], clearance);
        assert(probe.calls == traceCalls);
        assert(!std::memcmp(&position, player + 8, sizeof(position)));
        assert(!std::memcmp(&velocity, player + 0x60, sizeof(velocity)));
        assert(flags == get<u16>(player, 0xE0));
        const i32 phase = spidey::core::signedWord(randomBits());
        const i32 frameScale = spidey::core::signedWord(randomBits());
        put(swinger, 0x180, phase);
        put(reinterpret_cast<void*>(0x6A9038), 0, player + 0);
        typedef void (__attribute__((thiscall)) *Animate)(void*, i32);
        reinterpret_cast<Animate>(0x4F72C0)(swinger, frameScale);
        assert(get<i16>(player, 0x128) == spidey::core::signedHalfWord(
            SwingArc::animationFrame(frameScale, phase)));
        typedef i32 (__attribute__((thiscall)) *Completed)(void*);
        assert(bool(reinterpret_cast<Completed>(0x4F72F0)(swinger)) == SwingArc::completed(phase));
    }
    std::puts("PASS: 10000 swing paths, probe sequences, collisions and animation results match original instructions");
}
