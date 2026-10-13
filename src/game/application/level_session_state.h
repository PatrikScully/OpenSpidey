#pragma once

#include "core/types.h"

// Shared with the original shell module. Its executable-data address remains
// fixed until the shell and level session migrate this state together.
static u8* const gFrontUseAltTriggerMask = reinterpret_cast<u8*>(0x005FAE9D);
