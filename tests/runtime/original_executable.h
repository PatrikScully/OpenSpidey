#pragma once
// Optional Linux/i386 fixture: maps code only; never starts the Windows game.
#include "core/types.h"
#include <cassert>
#include <cstring>
#include <fstream>
#include <iterator>
#include <vector>
#include <sys/mman.h>

namespace original_executable {
inline u32 readWord(const std::vector<u8>& file, std::size_t offset) {
    assert(offset + 4 <= file.size());
    u32 word;
    std::memcpy(&word, &file[offset], 4);
    return word;
}
inline u16 readHalf(const std::vector<u8>& file, std::size_t offset) {
    assert(offset + 2 <= file.size());
    u16 word;
    std::memcpy(&word, &file[offset], 2);
    return word;
}
inline void* loadOriginalImage(const char* path) {
    std::ifstream stream(path, std::ios::binary);
    assert(stream);
    std::vector<u8> file((std::istreambuf_iterator<char>(stream)), std::istreambuf_iterator<char>());
    assert(readHalf(file, 0) == 0x5A4D);
    u32 pe = readWord(file, 0x3C);
    assert(readWord(file, pe) == 0x4550);
    assert(readHalf(file, pe + 4) == 0x14C);
    u32 optional = pe + 24;
    assert(readHalf(file, optional) == 0x10B);
    u32 base = readWord(file, optional + 28);
    u32 size = readWord(file, optional + 56);
    assert(base == 0x400000 && size < 0x4000000);
    void* mapped = mmap(reinterpret_cast<void*>(base), size,
        PROT_READ | PROT_WRITE | PROT_EXEC,
        MAP_PRIVATE | MAP_ANONYMOUS | MAP_FIXED_NOREPLACE, -1, 0);
    assert(mapped != MAP_FAILED && mapped == reinterpret_cast<void*>(base));
    u32 sections = optional + readHalf(file, pe + 20);
    for (u32 index = 0; index < readHalf(file, pe + 6); ++index) {
        u32 section = sections + index * 40;
        u32 address = readWord(file, section + 12);
        u32 bytes = readWord(file, section + 16);
        u32 raw = readWord(file, section + 20);
        assert(address + bytes <= size && raw + bytes <= file.size());
        if (bytes) std::memcpy(static_cast<u8*>(mapped) + address, &file[raw], bytes);
    }
    return mapped;
}
}
