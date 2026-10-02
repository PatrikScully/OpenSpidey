// Drive through input_sdl.py on a private X11 display; no game assets needed.
#include "../../platform/sdl3/plat_sdl3.cpp"
#include <cassert>

i32 gWideScreen;
CMenu* gPausedMenu;

// @Bogus
void Plat_MusicStop(void) {}

// @Bogus
void Plat_MovieStop(void) {}

// @Bogus
void Plat_InputMapCutsceneSkip(u8*) {}

// @Bogus
void PCINPUT_GetKeyboardMappingForAction(u32, u32*)
{
	assert(false); // Modern action remapping is disabled in this fixture.
}

// @Bogus
int main(void)
{
	setvbuf(stdout, 0, _IONBF, 0);
	setenv("SPIDEY_MODERN_CONTROLS", "0", 1);
	unsetenv("SPIDEY_KEYS");
	assert(Plat_Init(640, 480, 0));
	assert(SDL_SetWindowRelativeMouseMode(gWindow, false));
	SDL_PropertiesID properties = SDL_GetWindowProperties(gWindow);
	Sint64 window = SDL_GetNumberProperty(properties, SDL_PROP_WINDOW_X11_WINDOW_NUMBER, 0);
	assert(window);
	printf("READY %lld\n", (long long)window);

	char command[32];
	unsigned key, down, quit;
	while (scanf("%31s %u %u %u", command, &key, &down, &quit) == 4)
	{
		assert(key < 256);
		u8 state[256];
		u32 deadline = Plat_Ticks() + 1500;
		do
		{
			// No render, WinYield, SDL event calls or separate platform yield.
			Plat_InputPollKeyboard(state);
			if ((state[key] != 0) == (down != 0) && (gQuit != 0) == (quit != 0))
				break;
			Plat_Sleep(2);
		} while (Plat_Ticks() < deadline);
		printf("CHECK %s key=%u down=%u quit=%u\n", command, key, state[key] != 0, gQuit != 0);
		assert((state[key] != 0) == (down != 0));
		assert((gQuit != 0) == (quit != 0));
		printf("PASS %s\n", command);
	}
	Plat_Shutdown();
	return 0;
}
