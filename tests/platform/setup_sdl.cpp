// Run on an X11 display with Mesa, SDL_AUDIO_DRIVER=dummy.
#include "../../platform/sdl3/plat_sdl3.cpp"
#include <cassert>
#include <vector>

static std::vector<f32> gains;

extern "C" bool __real_SDL_SetAudioStreamGain(SDL_AudioStream*, float);

// @Bogus
extern "C" bool __wrap_SDL_SetAudioStreamGain(SDL_AudioStream* stream, float gain)
{
	bool result = __real_SDL_SetAudioStreamGain(stream, gain);
	if (result)
		gains.push_back(SDL_GetAudioStreamGain(stream));
	return result;
}

// @Bogus
void Plat_MusicStop(void) {}

// @Bogus
void Plat_MovieStop(void) {}
