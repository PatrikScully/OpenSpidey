#include "game/player/player_motion.h"
#include "game/player/sweep_geometry.h"
#include "game/player/ground_probe_fan.h"
#include "core/floating_word.h"
#include <cassert>
#include <limits>

using namespace spidey::player;
static void expect(const CVector& vector, i32 x, i32 y, i32 z) {
    assert(vector.vx == x && vector.vy == y && vector.vz == z);
}
int main() {
    const CVector velocity(4097,-4097,8192);
    expect(PlayerMotion::displacement(velocity,2),4097,-4097,8192);
    expect(PlayerMotion::displacement(velocity,3),6145,-6146,12288);
    expect(PlayerMotion::collisionFreeDisplacement(velocity,3),8194,-8194,16384);
    expect(PlayerMotion::collisionFreeDisplacement(velocity,0),4097,-4097,8192);
    expect(PlayerMotion::velocityFromDisplacement(CVector(7,-7,8),3),4,-4,5);
    assert(PlayerMotion::fallDisplacement(4097,3)==6145);
    assert(PlayerMotion::dampedValue(-5, 1)==-2);
    assert(PlayerMotion::dampedValue(5, 32)==0);
    assert(spidey::core::truncateToGameWord(-7.75f)==-7);
    assert(spidey::core::truncateToGameWord(2147483648.0f)==std::numeric_limits<i32>::min());
    assert(spidey::core::truncateToGameWord(4294967296.0f)==0);
    assert(spidey::core::truncateToGameWord(std::numeric_limits<float>::infinity())==0);
    CVector move(-4096,8000,8192);
    PlayerMotion::slideHorizontally(move,CVector(4096,0,0));
    expect(move,0,8000,2048);
    move.Set(4096,8000,8192);
    PlayerMotion::slideHorizontally(move,CVector(4096,0,0));
    expect(move,4096,8000,8192);
    assert(SweepGeometry::scaledSquaredLength(CVector(4096,4096,4096))==192);
    assert(SweepGeometry::scaledSquaredLength(CVector(4096,4096,4096),true)==128);
    assert(SweepGeometry::reachForTicks(70,4)==70);
    assert(SweepGeometry::reachForTicks(70,5)==105);
    expect(SweepGeometry::mainRay(CVector(4096,0,0),70,2,8),322560,0,0);
    expect(SweepGeometry::shoulderSide(CVector(4096,0,0),70,8),0,0,-143360);
    expect(SweepGeometry::shoulderForward(CVector(4096,4096,0),70,2,12,8),286720,191146,0);
    const float expected[9][2]={{110,200},{90,200},{100,210},{100,190},
        {105,205},{105,195},{95,205},{95,195},{100,200}};
    for (unsigned i=0;i<9;++i) {
        const GroundProbePoint point=GroundProbeFan::point(100,200,10,i);
        assert(point.x==expected[i][0] && point.z==expected[i][1]);
    }
    expect(PlayerMotion::collisionFreeDisplacement(CVector(std::numeric_limits<i32>::max(),0,0),3),-2,0,0);
}
