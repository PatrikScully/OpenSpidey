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

// @Bogus
int main(void)
{
	if (buInit(122, 255, 0, backupReady) != 0 || callbacks != 1)
		return 1;
	if (strcmp(gMemorycardPath, "save/") != 0)
		return 2;
	u8 written[512];
	u8 read[512];
	for (i32 i = 0; i < 512; ++i)
		written[i] = (u8)(i * 37);
	memset(read, 0, sizeof(read));
	if (buSaveFile(0, "OPENSPIDEY_TEST.DAT", written, 1, 0) != 0)
		return 3;
	if (buGetFileSize(0, "OPENSPIDEY_TEST.DAT") != 1)
		return 4;
	if (buLoadFile(0, "OPENSPIDEY_TEST.DAT", read, 1) != 0 || memcmp(written, read, 512) != 0)
		return 5;
	if (buInit(122, 255, 0, backupReady) != 0 || callbacks != 2)
		return 6;
	FILE* file = fopen("save/OPENSPIDEY_TEST.DAT", "rb");
	if (!file)
		return 7;
	if (fseek(file, 0, SEEK_END) != 0 || ftell(file) != 512)
		return 8;
	fclose(file);
	if (remove("save/OPENSPIDEY_TEST.DAT") != 0)
		return 9;
	if (buGetFileSize(0, "OPENSPIDEY_TEST.DAT") != -1 || buLoadFile(0, "OPENSPIDEY_TEST.DAT", read, 1) != -249)
		return 10;
	if (buSaveFile(0, "missing/OPENSPIDEY_TEST.DAT", written, 1, 0) != -248)
		return 11;
	puts("Native save path, callbacks and file round trip passed.");
	return 0;
}
