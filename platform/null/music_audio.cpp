#include "../plat.h"

// @Bogus
i32 Plat_MusicAudio(PlatMusicAudioOp op, const void*, i32 value)
{
	static i32 queued, active, started, paused;
	static u32 last;
	u32 now = Plat_Ticks();
	if (active && started && !paused)
	{
		u32 used = (u32)(now - last) * 176400 / 1000;
		queued = used < (u32)queued ? queued - used : 0;
	}
	last = now;
	switch (op)
	{
		case PLAT_MUSIC_AUDIO_OPEN:
			active = 1;
			queued = started = paused = 0;
			return 1;
		case PLAT_MUSIC_AUDIO_CLOSE:
			active = queued = started = paused = 0;
			return 1;
		case PLAT_MUSIC_AUDIO_QUEUE:
			queued += value;
			return active;
		case PLAT_MUSIC_AUDIO_QUEUED:
			return queued;
		case PLAT_MUSIC_AUDIO_START:
			started = 1;
			return active;
		case PLAT_MUSIC_AUDIO_PAUSE:
			paused = value != 0;
			return active;
		default:
			return active;
	}
}
