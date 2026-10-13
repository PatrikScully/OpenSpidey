#pragma once

#ifndef INIT_H
#define INIT_H

#include "engine/world/ob.h"
#include "compatibility/export.h"
EXPORT void DeleteList(CBody *);
EXPORT void Init_AtEnd(void);
EXPORT u8 Init_AtStart(i32);
EXPORT void Init_Cleanup(i32);
EXPORT void Init_KillAll(void);

// set to 5 or 20 by SpideyMain on the way out of a level. idb_globals.txt name.
// Defined in init.cpp, it just had no header entry.
EXPORT extern i32 gSpideyMainRelated;

#endif
