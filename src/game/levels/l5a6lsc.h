#pragma once

#ifndef L5A6LSC_H
#define L5A6LSC_H

#include "compatibility/export.h"
#include "engine/assets/reloc.h"

EXPORT void L5A6LSC_MonitorSpideyinWater(const u32 *,u32 *);
EXPORT void L5A6LSC_RelocatableModuleInit(reloc_mod *);
EXPORT i32 obtainWaterLevelInPool(i32);


#endif
