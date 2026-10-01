#include "../plat.h"
#include "audio_settings.h"
#include <SDL3/SDL.h>

// @Bogus
i32 Plat_MusicAudio(PlatMusicAudioOp op, const void* pcm, i32 value)
{
	static SDL_AudioStream* stream;
	if (op == PLAT_MUSIC_AUDIO_CLOSE)
	{
		if (stream)
			SDL_DestroyAudioStream(stream);
		stream = 0;
		return 1;
	}
	if (op == PLAT_MUSIC_AUDIO_OPEN)
	{
		if (stream)
			SDL_DestroyAudioStream(stream);
		SDL_AudioSpec spec = { SDL_AUDIO_S16, 2, 44100 };
		stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, 0, 0);
		if (stream)
			SDL_SetAudioStreamGain(stream, Plat_AudioGain("SPIDEY_MUSIC_VOLUME"));
		return stream != 0;
	}
	if (!stream)
		return 0;
	switch (op)
	{
		case PLAT_MUSIC_AUDIO_QUEUE:
			return SDL_PutAudioStreamData(stream, pcm, value);
		case PLAT_MUSIC_AUDIO_QUEUED:
			return SDL_GetAudioStreamQueued(stream);
		case PLAT_MUSIC_AUDIO_START:
			return SDL_ResumeAudioStreamDevice(stream);
		case PLAT_MUSIC_AUDIO_PAUSE:
			return value ? SDL_PauseAudioStreamDevice(stream) : SDL_ResumeAudioStreamDevice(stream);
		case PLAT_MUSIC_AUDIO_VOLUME:
			return SDL_SetAudioStreamGain(stream, value / 32768.0f * Plat_AudioGain("SPIDEY_MUSIC_VOLUME"));
		case PLAT_MUSIC_AUDIO_FLUSH:
			return SDL_FlushAudioStream(stream);
		default:
			return 0;
	}
}
