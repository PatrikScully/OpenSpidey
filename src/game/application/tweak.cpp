#include "game/application/tweak.h"

#ifndef SPIDEY_STANDALONE
i16 gGameState[30];
#else
extern i16 gGameState[30];
#endif

// @Ok
// @Matching
void Tweak_Init(void)
{
	gGameState[14] = 1;
	gGameState[8] = 0;
	gGameState[9] = 0;
	gGameState[10] = 0;
}
