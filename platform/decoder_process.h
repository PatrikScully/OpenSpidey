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

// @Bogus
static i32 Plat_DecoderStart(PlatDecoder* decoder, const char* const* args)
{
#ifdef _WIN32
	char command[8192];
	u32 used = 0;
	for (i32 i = 0; args[i]; i++)
	{
		if (used + strlen(args[i]) * 2 + 4 >= sizeof(command))
			return 0;
		if (i)
			command[used++] = ' ';
		command[used++] = '"';
		u32 slashes = 0;
		for (const char* c = args[i];; c++)
		{
			if (*c == '\\')
			{
				slashes++;
				continue;
			}
			u32 copies = (*c == '"' || !*c) ? slashes * 2 : slashes;
			while (copies--)
				command[used++] = '\\';
			slashes = 0;
			if (!*c)
				break;
			if (*c == '"')
				command[used++] = '\\';
			command[used++] = *c;
		}
		command[used++] = '"';
	}
	command[used] = 0;
	SECURITY_ATTRIBUTES security = { sizeof(security), 0, TRUE };
	HANDLE input = 0, output = 0;
	if (!CreatePipe(&input, &output, &security, 65536))
		return 0;
	SetHandleInformation(input, HANDLE_FLAG_INHERIT, 0);
	HANDLE nullInput = CreateFileA("NUL", GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
		&security, OPEN_EXISTING, 0, 0);
	STARTUPINFOA startup;
	PROCESS_INFORMATION process;
	memset(&startup, 0, sizeof(startup));
	memset(&process, 0, sizeof(process));
	startup.cb = sizeof(startup);
	startup.dwFlags = STARTF_USESTDHANDLES;
	startup.hStdInput = nullInput;
	startup.hStdOutput = output;
	startup.hStdError = GetStdHandle(STD_ERROR_HANDLE);
	BOOL started = CreateProcessA(0, command, 0, 0, TRUE, CREATE_NO_WINDOW, 0, 0, &startup, &process);
	DWORD error = GetLastError();
	CloseHandle(output);
	if (nullInput != INVALID_HANDLE_VALUE)
		CloseHandle(nullInput);
	if (!started)
	{
		CloseHandle(input);
		fprintf(stderr, "Cannot start ffmpeg (Windows error %lu).\n", error);
		return 0;
	}
	CloseHandle(process.hThread);
	decoder->pid = process.hProcess;
	decoder->fd = input;
#else
	i32 pipes[2];
	if (pipe(pipes) != 0)
		return 0;
	fcntl(pipes[0], F_SETFD, FD_CLOEXEC);
	fcntl(pipes[1], F_SETFD, FD_CLOEXEC);
	posix_spawn_file_actions_t actions;
	posix_spawn_file_actions_init(&actions);
	posix_spawn_file_actions_adddup2(&actions, pipes[1], STDOUT_FILENO);
	posix_spawn_file_actions_addclose(&actions, pipes[0]);
	posix_spawn_file_actions_addclose(&actions, pipes[1]);
	i32 error = posix_spawnp(&decoder->pid, args[0], &actions, 0, (char* const*)args, environ);
	posix_spawn_file_actions_destroy(&actions);
	close(pipes[1]);
	if (error)
	{
		close(pipes[0]);
		decoder->pid = 0;
		fprintf(stderr, "Cannot start ffmpeg: %s.\n", strerror(error));
		return 0;
	}
	decoder->fd = pipes[0];
	fcntl(decoder->fd, F_SETFL, O_NONBLOCK);
#endif
	return 1;
}

#endif
