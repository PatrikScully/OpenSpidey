#include "game/player/crawl_motion.h"
#include <cassert>
#include <limits>

using spidey::player::CrawlMotion;
static void expect(const CVector& value, i32 x, i32 y, i32 z) {
    assert(value.vx == x && value.vy == y && value.vz == z);
}
int main() {
    CVector position(100, 200, 300), velocity(4097, -4097, 3);
    assert(CrawlMotion::advance(position, velocity, CVector(), CFriction(1, 1, 31), 4));
    expect(velocity, 2049, 0, 0);
    expect(position, 4197, 200, 300);
    position.Zero(); velocity.Set(-4099, 4099, 8192);
    assert(CrawlMotion::advance(position, velocity, CVector(), CFriction(31, 31, 31), 3));
    expect(velocity, -4098, 4099, 8192);
    expect(position, -6147, 6148, 12288);
    position.Set(10, 20, 30); velocity.Set(-2048, 2048, 0);
    assert(!CrawlMotion::advance(position, velocity, CVector(), CFriction(31, 31, 31), 6));
    expect(position, 10, 20, 30); expect(velocity, 0, 0, 0);
    position.Zero(); velocity.Set(4096, -4096, 8192);
    assert(CrawlMotion::advance(position, velocity, CVector(), CFriction(32, 32, 33), 0));
    expect(position, 0, 0, 4096);
    const i32 max = std::numeric_limits<i32>::max();
    position.Set(max, 0, 0); velocity.Set(4096, 0, 0);
    assert(CrawlMotion::advance(position, velocity, CVector(1, 0, 0), CFriction(31,31,31), -1));
    expect(position, spidey::core::wrappingAdd(max, 4097), 0, 0);
    assert(CrawlMotion::normalAlignment(CSVector(0, 4096, 0), CSVector(0, 4096, 0)) == 4096);
    assert(CrawlMotion::normalAlignment(CSVector(0, 4096, 0), CSVector(4096, 0, 0)) == 0);
    assert(CrawlMotion::normalAlignment(CSVector(0, 4096, 0), CSVector(0, -4096, 0)) == -4096);
}
