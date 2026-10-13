#include "game/player/swing_arc.h"
#include "game/player/swing_collision_resolver.h"
#include <cassert>
#include <limits>

using namespace spidey::player;
static void expect(const CVector& value, i32 x, i32 y, i32 z) {
    assert(value.vx == x && value.vy == y && value.vz == z);
}
class Probe : public SwingCollisionProbe {
public:
    int calls, hitOn;
    CVector starts[3], ends[3];
    SwingSurfaceHit surface;
    explicit Probe(int hit) : calls(0), hitOn(hit) {
        surface.position.Set(100, 200, 300);
        surface.normal.Set(3, -2600, -7);
    }
    bool trace(const CVector& start, const CVector& end, SwingSurfaceHit& hit) override {
        assert(calls < 3);
        starts[calls] = start;
        ends[calls] = end;
        ++calls;
        if (calls != hitOn) return false;
        hit = surface;
        return true;
    }
};
int main() {
    assert(SwingArc::advancePhase(2048, 3, 19) == 2105);
    assert(SwingArc::advancePhase(std::numeric_limits<i32>::max(), 1, 1)
        == std::numeric_limits<i32>::min());
    assert(SwingArc::pitchAngle(1024, -4096) == 0);
    assert(SwingArc::pitchAngle(1024, 0) == 1024);
    assert(SwingArc::pitchAngle(1024, 4096) == 2048);
    assert(SwingArc::pitchAngle(-1025, -1) == -1025);
    assert(SwingArc::animationFrame(24, 2048) == 0);
    assert(SwingArc::animationFrame(24, 3072) == 12);
    assert(SwingArc::animationFrame(24, 4096) == 24);
    assert(!SwingArc::completed(4095) && SwingArc::completed(4096));
    expect(SwingArc::endPosition(CVector(100, 200, 300), 96, CVector(0, -4096, 0)),
        100, 200 + 96 * 4096, 300);

    for (int hitOn = 0; hitOn <= 3; ++hitOn) {
        CVector position(50, 60, 70), velocity(1, 2, 3);
        u16 flags = 0x100;
        Probe probe(hitOn);
        SwingCollisionResolver resolver(position, velocity, flags, probe);
        resolver.resolve(CVector(5, 6, 7), CVector(10, 20, 30), CVector(40, 50, 60), 96);
        assert(probe.calls == (hitOn ? hitOn : 3));
        expect(probe.starts[0], 10, 20, 30);
        expect(probe.ends[0], 70, 50, 90);
        if (probe.calls >= 2) {
            expect(probe.starts[1], 70, 50, 90);
            expect(probe.ends[1], 70, 50 + 0x80000, 90);
        }
        if (probe.calls >= 3) {
            expect(probe.starts[2], 70, 50, 90);
            expect(probe.ends[2], 70, 50 - 0x20000, 90);
        }
        if (hitOn) {
            expect(position, 388, 200 - 2600 * 96, -372);
            expect(velocity, 0, 0, 0);
            assert(flags == (hitOn == 2 ? 0x102 : 0x101));
        } else {
            expect(position, 50, 60, 70);
            expect(velocity, 45, 54, 63);
            assert(flags == 0x100);
        }
    }
    CVector position, velocity;
    u16 flags = 0;
    Probe probe(1);
    probe.surface.normal.vy = -2601;
    SwingCollisionResolver(position, velocity, flags, probe).resolve(position, position, position, 0);
    assert(flags == 2);
    expect(position, 100, 200, 300);
}
