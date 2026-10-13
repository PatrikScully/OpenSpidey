#include "compatibility/non_win32.h"


#ifndef _WIN32

#include <ctime>
#include <cctype>
#include <unistd.h>

void CloseHandle(HANDLE)
{
}

// milliseconds since an arbitrary start, like the Win32 one
u32 GetTickCount()
{
	struct timespec ts;
	clock_gettime(CLOCK_MONOTONIC, &ts);
	return (u32)(ts.tv_sec * 1000 + ts.tv_nsec / 1000000);
}

void Sleep(u32 ms)
{
	usleep(ms * 1000);
}

i32 GetDriveTypeA(char*)
{
	return 69;   // never DRIVE_CDROM: the CD paths are not used on Linux
}

void GetCurrentDirectoryA(u32 size, char* buf)
{
	if (!getcwd(buf, size))
		buf[0] = 0;
}

void CreateDirectoryA(char*, i32)
{
}

void MessageBeep(u32)
{
}

void strlwr(char* inp)
{
	for (; *inp; inp++)
		*inp = (char)tolower((unsigned char)*inp);
}

#endif
