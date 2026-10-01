#include "audio_settings.h"
#include <cstdlib>

// @Bogus
f32 Plat_AudioGain(const char* channel)
{
	const char* names[2] = { "SPIDEY_MASTER_VOLUME", channel };
	f32 gain = 1.0f;
	for (i32 i = 0; i < 2; i++)
	{
		const char* setting = names[i] ? getenv(names[i]) : 0;
		if (!setting || !*setting)
			continue;
		char* end = 0;
		long volume = strtol(setting, &end, 10);
		if (*end || volume < 0 || volume > 100)
			continue;
		gain *= volume / 100.0f;
	}
	return gain;
}
