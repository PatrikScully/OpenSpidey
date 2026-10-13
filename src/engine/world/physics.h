#pragma once

#ifndef PHYSICS_H
#define PHYSICS_H

#include "core/fixed_vector.h"
#include "compatibility/export.h"

EXPORT void Physics_SetGravity(CVector *);

// Recovered free-function adapter for the typed CSwinger::currentPosition.
EXPORT void CSwinger_GetCurrentParams(i32 *pSwinger, CVector *pOut);

#endif
