#ifndef LIZARD_H
#define LIZARD_H

#pragma once

#include "compatibility/export.h"
#include "game/actors/baddy.h"
#include "engine/assets/reloc.h"

class CLizard : public CBaddy {
public:
	EXPORT CLizard(i16*, i32);
	EXPORT ~CLizard(void);
	EXPORT void AI(void);
};


void validate_CLizard(void);
EXPORT void Lizard_CreateLizard(const u32 *, u32 *);
EXPORT void Lizard_RelocatableModuleClear(void);
EXPORT void Lizard_RelocatableModuleInit(reloc_mod *);
#endif
