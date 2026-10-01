// Phase 2 entry point. Replaces RealWinMain (SpideyDX.cpp) for the
// standalone build: no window class, no CD check, no SEH filter. The order
// of the game side calls is RealWinMain's.
//
//   spider [game-dir]      (or SPIDEY_GAME_DIR=...)
//   SPIDEY_EXE=path        SpideyPC.exe used to seed the exe data block,
//                          default <game-dir>/SpideyPC.exe

#include "exemem.h"
#include "plat.h"
#include "launcher.h"

#include "../SpideyDX.h"
#include "../DXinit.h"
#include "../PCTimer.h"
#include "../main.h"

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <signal.h>
#ifdef _WIN32
#include <direct.h>
#define chdir _chdir
#else
#include <unistd.h>
#include <execinfo.h>
#include <sys/prctl.h>
#endif

void compile_time_assertions(void);   // main.cpp

// Debug aid: "kill -QUIT <pid>" (or timeout -s QUIT) prints where the main
// thread is, for hangs the null backend cannot show otherwise.
// @Bogus
static void onQuitSignal(int)
{
#ifndef _WIN32
	void* frames[64];
	int n = backtrace(frames, 64);
	fputs("---- SIGQUIT backtrace ----\n", stderr);
	backtrace_symbols_fd(frames, n, 2);
	_exit(3);
#endif
}

// @Bogus
int main(int argc, char** argv)
{
	setvbuf(stdout, 0, _IONBF, 0);   // logs survive a crash
#ifndef _WIN32
	signal(SIGQUIT, onQuitSignal);
	prctl(PR_SET_PTRACER, PR_SET_PTRACER_ANY, 0, 0, 0);   // let gdb -p attach
#endif
	Launcher_Bootstrap(argc, argv);

	const char* gameDir = argc > 1 ? argv[1] : getenv("SPIDEY_GAME_DIR");
	if (gameDir && chdir(gameDir) != 0)
	{
		perror(gameDir);
		return 1;
	}

	const char* exe = getenv("SPIDEY_EXE");
	if (!exe)
		exe = "SpideyPC.exe";

	// normally already done by exemem.cpp's early constructor
	if (!ExeMem_Init(exe))
		return 1;
	if (!ExeMem_IsSeeded())
		puts("WARNING: exe data block not seeded, menus and tables will be empty");

	compile_time_assertions();

	// RealWinMain from here on
	SPIDEYDX_LoadSettings();

	gRenderTest = 0;
	G_GAME_RESOLUTION_X = 640;
	gDxResolutionX = 640;
	G_GAME_RESOLUTION_Y = 480;
	gDxResolutionY = 480;
	gMMXSupport = 1;

	PCTIMER_Init();
	// bit 0 of the flags is the game's "windowed" option (gDxOptionRelated:
	// Blt to the window instead of a fullscreen Flip). Windowed by default,
	// SPIDEY_FULLSCREEN=1 for fullscreen. Bit 1 = depth buffer.
	i32 dxFlags = getenv("SPIDEY_FULLSCREEN") ? 2 : 3;
	DXINIT_DirectX8(0, 0, dxFlags);   // creates the window in the standalone build

	SpideyMain();

	SPIDEYDX_Shutdown();
	return 0;
}
