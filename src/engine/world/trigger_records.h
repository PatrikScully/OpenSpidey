#pragma once
#include "core/types.h"

// Original record names remain global to preserve exported C++ signatures.
struct SCommandPoint {
    u16* pCommands;
    u8 Collision;
    u8 Executed;
    u8 NumPulsesSet;
    u8 PulsesReceived;
    u16 NumPulses;
    u16 NodeIndex;
    u32 Checksum;
    SCommandPoint* pNextSimilar;
    SCommandPoint* pNext;
};

struct PendingListEntry {
    u16 NodeIndex;
    u16 field_2; // Reserved word; queue updates leave it unchanged.
    u16* pCommands;
};
