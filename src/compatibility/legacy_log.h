#pragma once
#include "compatibility/export.h"

// Original release-build logger. Kept as an empty entry point so recovered
// error paths retain their calling contract without a renderer dependency.
EXPORT void stateLog(char const*, ...);
