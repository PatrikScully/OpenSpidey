#ifndef SPIDEY_DECODER_PROCESS_H
#define SPIDEY_DECODER_PROCESS_H

#include "../my_types.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cerrno>

#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#include <fcntl.h>
#include <signal.h>
#include <spawn.h>
#include <sys/wait.h>
#include <unistd.h>
extern char** environ;
#endif

enum { PLAT_DECODER_PATH_SIZE = 1024 };

struct PlatDecoder
{
#ifdef _WIN32
	HANDLE pid, fd;
#else
	pid_t pid;
	i32 fd;
#endif
};

// @Bogus
static FILE* Plat_DecoderTemp(char* path, const char* prefix)
{
#ifdef _WIN32
	char directory[MAX_PATH];
	DWORD length = GetTempPathA(sizeof(directory), directory);
	if (!length || length >= sizeof(directory) || !GetTempFileNameA(directory, prefix, 0, path))
		return 0;
	FILE* output = fopen(path, "wb");
	if (!output)
	{
		DeleteFileA(path);
		path[0] = 0;
	}
	return output;
#else
	snprintf(path, PLAT_DECODER_PATH_SIZE, "/tmp/%s-XXXXXX", prefix);
	i32 fd = mkstemp(path);
	if (fd < 0)
		return 0;
	FILE* output = fdopen(fd, "wb");
	if (!output)
		close(fd);
	return output;
#endif
}

// @Bogus
static void Plat_DecoderRemove(const char* path)
{
#ifdef _WIN32
	DeleteFileA(path);
#else
	unlink(path);
#endif
}

// @Bogus
static void Plat_DecoderStop(PlatDecoder* decoder)
{
#ifdef _WIN32
	if (decoder->fd && decoder->fd != INVALID_HANDLE_VALUE)
		CloseHandle(decoder->fd);
	decoder->fd = 0;
	if (decoder->pid)
	{
		if (WaitForSingleObject(decoder->pid, 0) == WAIT_TIMEOUT)
			TerminateProcess(decoder->pid, 1);
		WaitForSingleObject(decoder->pid, INFINITE);
		CloseHandle(decoder->pid);
	}
#else
	if (decoder->fd >= 0)
		close(decoder->fd);
	decoder->fd = -1;
	if (decoder->pid > 0)
	{
		kill(decoder->pid, SIGKILL);
		while (waitpid(decoder->pid, 0, 0) < 0 && errno == EINTR)
			;
	}
#endif
	decoder->pid = 0;
}

#endif
