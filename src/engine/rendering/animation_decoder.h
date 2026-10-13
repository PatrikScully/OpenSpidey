#pragma once

#ifndef DECOMP_H
#define DECOMP_H

#include "compatibility/export.h"

class CSuper;
struct SMatrix;

// resolves the current per-part pose matrices for pSuper (LOD selection,
// hierarchy calculation order, matrix decompression). Returns a pointer to
// an array of SMatrix, one per part.
EXPORT SMatrix* Decomp_GetAnimTransform(CSuper*);

#endif
