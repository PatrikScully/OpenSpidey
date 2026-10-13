#pragma once

#ifndef TRAINING_H
#define TRAINING_H

#include "compatibility/export.h"
#include "engine/assets/reloc.h"

EXPORT void Training_RelocatableModuleInit(reloc_mod *);
EXPORT void Training_RelocatableModuleClear(void);
EXPORT void Training_SpideyDied(const u32*, u32*);
EXPORT void Training_MonitorLevel(const u32*, u32*);
#endif
