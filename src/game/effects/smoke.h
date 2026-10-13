#pragma once

#ifndef SMOKE_H
#define SMOKE_H

#include "compatibility/export.h"
#include "game/player/weapons.h"

class CSmokeJet : public CTexturedRibbon
{
	public:
		PADDING(0x125-40);
};

void validate_CSmokeJet(void);

#endif
