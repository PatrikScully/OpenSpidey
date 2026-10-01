// Build with the real backup routines and SPIDEY_STANDALONE.
#include "../../pcdcBkup.h"
#include <cstdio>
#include <cstring>

extern char gMemorycardPath[688];
static i32 callbacks;

// @Bogus
static void backupReady(void)
{
	++callbacks;
}

