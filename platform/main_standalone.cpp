// Phase 2 entry point. Replaces RealWinMain (SpideyDX.cpp) for the
// standalone build: no window class, no CD check, no SEH filter. The order
// of the game side calls is RealWinMain's.
//
//   spider [game-dir]      (or SPIDEY_GAME_DIR=...)
//   SPIDEY_EXE=path        SpideyPC.exe used to seed the exe data block,
//                          default <game-dir>/SpideyPC.exe

#include "platform/launcher.h"

#include "game/application/game_application.h"

#include <cstdio>
#include <cstdlib>
#include <signal.h>
#ifndef _WIN32
#include <unistd.h>
#include <execinfo.h>
#include <sys/prctl.h>
#endif

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

	const char* gameDirectory = argc > 1 ? argv[1] : getenv("SPIDEY_GAME_DIR");
	spidey::application::GameApplication application;
	return application.run(gameDirectory);
}
