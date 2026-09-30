#include "../plat.h"
#include <SDL3/SDL.h>
#include <GL/gl.h>

// @Bogus
i32 Plat_MovieAudio(PlatMovieAudioOp op, const void* pcm, i32 value)
{
	static SDL_AudioStream* stream;
	if (op == PLAT_MOVIE_AUDIO_CLOSE)
	{
		if (stream)
			SDL_DestroyAudioStream(stream);
		stream = 0;
		return 1;
	}
	if (op == PLAT_MOVIE_AUDIO_OPEN)
	{
		if (stream)
			SDL_DestroyAudioStream(stream);
		SDL_AudioSpec spec = { SDL_AUDIO_S16, 2, 44100 };
		stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, 0, 0);
		return stream != 0;
	}
	if (!stream)
		return 0;
	switch (op)
	{
		case PLAT_MOVIE_AUDIO_QUEUE:
			return SDL_PutAudioStreamData(stream, pcm, value);
		case PLAT_MOVIE_AUDIO_QUEUED:
			return SDL_GetAudioStreamQueued(stream);
		case PLAT_MOVIE_AUDIO_START:
			return SDL_ResumeAudioStreamDevice(stream);
		case PLAT_MOVIE_AUDIO_VOLUME:
			return SDL_SetAudioStreamGain(stream, value / 255.0f);
		default:
			return 0;
	}
}

