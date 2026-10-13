#include "core/floating_word.h"
#include "original_executable.h"
#include <cstdio>

int main(int argc, char** argv) {
    assert(argc==2);
    original_executable::loadOriginalImage(argv[1]);
    static_assert(sizeof(void*)==4,"Compile with -m32 -fno-pie -no-pie.");
    const u8 prologue[]={0x55,0x8B,0xEC};
    assert(!std::memcmp(reinterpret_cast<void*>(0x529C7C),prologue,3));
    u32 state=0xCF93771;
    for (int sample=0;sample<10000;++sample) {
        state=state*1664525u+1013904223u;
        float value;
        std::memcpy(&value,&state,4);
        i32 original;
        u32 entry=0x529C7C;
        asm volatile("flds %1; call *%2" : "=&a"(original) : "m"(value), "r"(entry)
            : "edx","ecx","st","memory","cc");
        assert(original==spidey::core::truncateToGameWord(value));
    }
    std::puts("PASS: 10000 floating-word conversions match original x87 instructions");
}
