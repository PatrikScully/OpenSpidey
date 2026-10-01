#include "launcher.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>

#ifdef _WIN32
#include <windows.h>
#else
#include <unistd.h>
#include <limits.h>
#endif

// @Bogus
void Launcher_Bootstrap(int argc, char** argv)
{
#ifndef SPIDEY_SETUP_LAUNCHER
	return;
#else
	bool settings = argc > 1 && strcmp(argv[1], "--settings") == 0;
	if (argc > 1 && strcmp(argv[1], "--help") == 0)
	{
		puts("OpenSpidey: start without arguments for saved settings or first-run setup.\n"
			"  --settings     Open the configuration window\n"
			"  [game-dir]     Run directly with existing game files");
		exit(0);
	}
	if (!settings && ((argc > 1 && argv[1][0] != '-') || getenv("SPIDEY_GAME_DIR")))
		return;
	if (argc > 1 && !settings)
	{
		fprintf(stderr, "Unknown option: %s. Use --help for startup options.\n", argv[1]);
		exit(2);
	}
#ifdef _WIN32
	wchar_t binary[32768], directory[32768];
	DWORD length = GetModuleFileNameW(0, binary, 32768);
	if (!length || length >= 32700) exit(1);
	wcscpy(directory, binary);
	wchar_t* slash = wcsrchr(directory, L'\\');
	if (!slash) exit(1);
	*slash = 0;
	wchar_t application[32768], script[32768], command[32768];
	swprintf(application, 32768, L"%ls\\OpenSpidey.exe", directory);
	swprintf(script, 32768, L"%ls\\launcher\\launcher.py", directory);
	bool packaged = GetFileAttributesW(application) != INVALID_FILE_ATTRIBUTES;
	int commandLength;
	if (packaged)
		commandLength = swprintf(command, 32768, L"\"%ls\" --game-binary \"%ls\"%ls", application, binary, settings ? L" --settings" : L"");
	else
		commandLength = swprintf(command, 32768, L"python.exe \"%ls\" --game-binary \"%ls\"%ls", script, binary, settings ? L" --settings" : L"");
	if (commandLength < 0 || commandLength >= 32768) exit(1);
	STARTUPINFOW startup;
	PROCESS_INFORMATION process;
	memset(&startup, 0, sizeof(startup));
	memset(&process, 0, sizeof(process));
	startup.cb = sizeof(startup);
	if (!CreateProcessW(packaged ? application : 0, command, 0, 0, FALSE, 0, 0, directory, &startup, &process))
	{
		MessageBoxW(0, L"OpenSpidey setup could not start. Extract the complete release, including OpenSpidey.exe, into one folder. Source builds need Python with Tkinter.", L"OpenSpidey", MB_OK | MB_ICONERROR);
		exit(1);
	}
	WaitForSingleObject(process.hProcess, INFINITE);
	DWORD result = 1;
	GetExitCodeProcess(process.hProcess, &result);
	CloseHandle(process.hThread);
	CloseHandle(process.hProcess);
	exit((int)result);
#else
	char binary[4096];
	ssize_t length = readlink("/proc/self/exe", binary, sizeof(binary) - 1);
	if (length <= 0)
	{
		perror("OpenSpidey: cannot locate the game executable");
		exit(1);
	}
	binary[length] = 0;
	char directory[4096];
	strcpy(directory, binary);
	char* slash = strrchr(directory, '/');
	if (!slash) exit(1);
	*slash = 0;
	char application[8192], script[8192];
	snprintf(application, sizeof(application), "%s/OpenSpidey", directory);
	if (access(application, X_OK) == 0)
	{
		if (settings) execl(application, application, "--game-binary", binary, "--settings", (char*)0);
		else execl(application, application, "--game-binary", binary, (char*)0);
	}
	snprintf(script, sizeof(script), "%s/launcher/launcher.py", directory);
	if (settings) execlp("python3", "python3", script, "--game-binary", binary, "--settings", (char*)0);
	else execlp("python3", "python3", script, "--game-binary", binary, (char*)0);
	perror("OpenSpidey: cannot start setup (source builds need Python 3 and Tkinter)");
	exit(1);
#endif
#endif
}
