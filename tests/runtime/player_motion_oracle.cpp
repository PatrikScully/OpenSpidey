// Optional Linux/i386 regression for the original collision-free animation
// window. It exercises real original velocity integration and position updates.
#include "game/player/player_motion.h"
#include "original_executable.h"
#include <cstdio>

namespace {
u32 randomBits() {
    static u32 state=0x491BB827;
    state=state*1664525u+1013904223u;
    return state;
}
CVector randomVector() {
    using spidey::core::signedWord;
    return CVector(signedWord(randomBits()),signedWord(randomBits()),signedWord(randomBits()));
}
template<class Value> void put(void* object,unsigned offset,const Value& value) {
    std::memcpy(static_cast<u8*>(object)+offset,&value,sizeof(value));
}
}
int main(int argc,char** argv) {
    static_assert(sizeof(void*)==4,"Build with -m32 -fno-pie -no-pie.");
    assert(argc==2);
    original_executable::loadOriginalImage(argv[1]);
    const u8 prologue[]={0x81,0xEC,0x1C,0x01,0x00,0x00};
    assert(!std::memcmp(reinterpret_cast<void*>(0x466CE0),prologue,sizeof(prologue)));
    typedef void (__attribute__((thiscall)) *Original)(void*);
    const i32 elapsed[]={-1,0,1,2,3,4,6,7,0x7FFFFFFF};
    for (int sample=0;sample<10000;++sample) {
        alignas(4) u8 player[0x1000]={};
        CVector position=randomVector(),velocity=randomVector(),acceleration=randomVector();
        CFriction friction(randomBits(),randomBits(),randomBits());
        put(player,8,position);
        put(player,0x60,velocity);
        put(player,0x6C,acceleration);
        put(player,0x78,friction);
        put(player,0x80,elapsed[sample%9]);
        put(player,0xE1C,i32(0x40000));
        put(player,0x12A,u16(sample%2 ? 270 : 271));
        put(player,0x128,u16(13));
        reinterpret_cast<Original>(0x466CE0)(player);
        spidey::player::PlayerMotion::updateVelocity(velocity,acceleration,friction);
        position+=spidey::player::PlayerMotion::collisionFreeDisplacement(velocity,elapsed[sample%9]);
        assert(!std::memcmp(&position,player+8,sizeof(position)));
        assert(!std::memcmp(&velocity,player+0x60,sizeof(velocity)));
    }
    std::puts("PASS: 10000 collision-free player movement steps match original instructions");
}
