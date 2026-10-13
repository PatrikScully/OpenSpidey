#pragma once

#ifndef MUSIC_H
#define MUSIC_H

#include "compatibility/export.h"

// 0x0060D874. Set by Music_MusicUpdate when the track that is playing is
// within 3 vblanks of the duration listed for it, cleared on every other call.
// CScorpion::AI (reads it at 0x004887F3 and 0x00488819) and the other bosses
// use it as the safe point at which they may change gMusicPhase.
EXPORT extern u8 gMusicTrackEnding;

// 0x0060D880. Picks which track list a level uses when it has more than one
// (level 0x202 and level 0x705). Written by CScorpion::AI (0x00488802,
// 0x00488828, 0x0048883E, values 0/1/2) and by CMysterio::AI (0x0045F198,
// value 1).
EXPORT extern i32 gMusicPhase;

EXPORT void Music_MusicUpdate(void);

#endif
