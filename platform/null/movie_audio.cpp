#include "../plat.h"

// @Bogus
i32 Plat_MovieAudio(PlatMovieAudioOp op, const void*, i32 value)
{
	static i32 queued, started;
	static u32 tick;
	u32 now = Plat_Ticks();
	if (started)
	{
		queued -= (now - tick) * 44100 * 4 / 1000;
		if (queued < 0)
			queued = 0;
	}
	tick = now;
	if (op == PLAT_MOVIE_AUDIO_OPEN || op == PLAT_MOVIE_AUDIO_CLOSE)
	{
		queued = started = 0;
		return 1;
	}
	if (op == PLAT_MOVIE_AUDIO_QUEUE)
		queued += value;
	if (op == PLAT_MOVIE_AUDIO_START)
		started = 1;
	return op == PLAT_MOVIE_AUDIO_QUEUED ? queued : 1;
}

