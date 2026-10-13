#pragma once

#ifndef L6A1LSC_H
#define L6A1LSC_H

#include "compatibility/export.h"
#include "engine/assets/reloc.h"

EXPORT void L6A1LSC_MonitorSpideyinWater(u32 const *,u32 *);
EXPORT void L6A1LSC_RelocatableModuleInit(reloc_mod *);
EXPORT void L6A1LSC_RelocatableModuleClear(void);
EXPORT i32 obtainWaterLevelInPoolL6A1(i32);

#endif
