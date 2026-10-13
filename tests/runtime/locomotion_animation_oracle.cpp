// Linux/i386 comparison of the original falling-animation decision block.
// Entry: 0x4B2A15; exit: 0x4B2BBB. Only animation playback is replaced by a
// capture callback; the original comparisons and reference/flag writes execute.
#include "game/player/locomotion_animation_policy.h"
#include "original_executable.h"
#include <cstdio>
#include <cstdint>
#include <limits>

extern "C" void runOriginalFallDecision(void*, i32);
asm(R"(
.text
.globl runOriginalFallDecision
.type runOriginalFallDecision,@function
runOriginalFallDecision:
    push %ebp
    mov %esp,%ebp
    push %ebx
    push %esi
    push %edi
    sub $0x28,%esp
    mov 8(%ebp),%esi
    mov 12(%ebp),%eax
    mov %eax,0x20(%esp)
    mov $0x4B2A15,%eax
    call *%eax
    add $0x28,%esp
    pop %edi
    pop %esi
    pop %ebx
    pop %ebp
    ret
.size runOriginalFallDecision,.-runOriginalFallDecision
)");

namespace {
using namespace spidey::player;
int animationCalls;
void* expectedPlayer;
template<class Value> void put(void* object, unsigned offset, Value value) {
    std::memcpy(static_cast<u8*>(object)+offset,&value,sizeof(value));
}
void __attribute__((thiscall,force_align_arg_pointer)) captureAnimation(
    void* player, i32 animation, i32 flags, i32 repeat)
{
    assert(player==expectedPlayer && flags==0 && repeat==-1);
    assert(++animationCalls==1);
    put(player,0x12A,static_cast<u16>(animation));
}
void redirect(u32 address, void* callback) {
    *reinterpret_cast<u8*>(address)=0xE9;
    put(reinterpret_cast<void*>(address),1,
        u32(reinterpret_cast<uintptr_t>(callback))-(address+5));
}
}

int main(int argc,char** argv) {
    static_assert(sizeof(void*)==4,"Build with -m32 -fno-pie -no-pie.");
    assert(argc==2);
    original_executable::loadOriginalImage(argv[1]);
    const u8 entry[]={0x8B,0x4E,0x64,0x8B,0x7C,0x24,0x24};
    const u8 exit[]={0x8A,0x86,0xE5,0x0A,0x00,0x00};
    assert(!std::memcmp(reinterpret_cast<void*>(0x4B2A15),entry,sizeof(entry)));
    assert(!std::memcmp(reinterpret_cast<void*>(0x4B2BBB),exit,sizeof(exit)));
    *reinterpret_cast<u8*>(0x4B2BBB)=0xC3;
    redirect(0x4C9130,reinterpret_cast<void*>(&captureAnimation));
    const i32 pairs[][2]={{-1,-1},{-1,0},{0,-1},{0,0},{0,1},{1,0},
        {std::numeric_limits<i32>::min(),std::numeric_limits<i32>::max()},
        {std::numeric_limits<i32>::max(),std::numeric_limits<i32>::min()}};
    const u8 releases[]={0,1,2,255};
    const u32 tick=0xAABBCCDD;
    std::memcpy(reinterpret_cast<void*>(0x6B4CA8),&tick,sizeof(tick));
    unsigned samples=0;
    for (unsigned animation=0;animation<=0xFFFF;++animation) {
        for (const auto& velocities:pairs) {
            for (u8 release:releases) {
                alignas(4) u8 original[0x1000]={},expected[0x1000];
                put(original,0x0C,i32(123456));
                put(original,0x64,velocities[1]);
                put(original,0x12A,static_cast<u16>(animation));
                put(original,0x54D,release);
                put(original,0xE38,u32(0x11223344));
                put(original,0xE3C,u32(0x55667788));
                std::memcpy(expected,original,sizeof(original));
                const auto plan=LocomotionAnimationPolicy::falling(
                    animation,velocities[0],velocities[1],release!=0);
                if (plan.resetReference) {
                    put(expected,0xE38,i32(123456));
                    put(expected,0xE3C,tick);
                }
                if (plan.nextAnimation!=LocomotionAnimationPolicy::KeepAnimation)
                    put(expected,0x12A,plan.nextAnimation);
                if (plan.clearWebRelease) put(expected,0x54D,u8(0));
                expectedPlayer=original;
                animationCalls=0;
                runOriginalFallDecision(original,velocities[0]);
                assert(animationCalls==int(plan.nextAnimation!=LocomotionAnimationPolicy::KeepAnimation));
                assert(!std::memcmp(original,expected,sizeof(original)));
                ++samples;
            }
        }
    }
    std::printf("PASS: %u falling-animation decisions match original instructions\n",samples);
}
