// Stream the original Bink voice files independently of FMV audio.
#include "plat.h"
#include "decoder_process.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cerrno>
#include <pthread.h>

typedef PlatDecoder MusicDecoder;

struct MusicState
{
	MusicDecoder decoder;
	pthread_t thread;
	char path[PLAT_DECODER_PATH_SIZE];
	i32 threadCreated, stop, active, paused, started, ended, trace;
	u32 lastDataAt, bytes, traceAt;
};

static MusicState gMusic;
static pthread_mutex_t gMusicLock = PTHREAD_MUTEX_INITIALIZER;

// @Bogus
static void stopMusicDecoder(MusicDecoder* decoder)
{
	Plat_DecoderStop(decoder);
}

// @Bogus
static i32 startMusicDecoder(MusicDecoder* decoder, const char* path)
{
	const char* args[] = { "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error",
		"-threads", "1", "-i", path, "-map", "0:a:0", "-vn", "-ac", "2", "-ar", "44100",
		"-f", "s16le", "pipe:1", 0 };
	return Plat_DecoderStart(decoder, args);
}

// @Bogus
void Plat_MusicStop(void)
{
	pthread_mutex_lock(&gMusicLock);
	gMusic.stop = 1;
	pthread_mutex_unlock(&gMusicLock);
	if (gMusic.threadCreated)
		pthread_join(gMusic.thread, 0);
	if (gMusic.decoder.pid)
		stopMusicDecoder(&gMusic.decoder);
	Plat_MusicAudio(PLAT_MUSIC_AUDIO_CLOSE, 0, 0);
	if (gMusic.path[0])
		Plat_DecoderRemove(gMusic.path);
	memset(&gMusic, 0, sizeof(gMusic));
#ifndef _WIN32
	gMusic.decoder.fd = -1;
#endif
}

// @Bogus
static void* playMusic(void*)
{
	for (;;)
	{
		pthread_mutex_lock(&gMusicLock);
		if (gMusic.stop)
		{
			pthread_mutex_unlock(&gMusicLock);
			return 0;
		}
		u32 now = Plat_Ticks();
		if (!gMusic.paused)
		{
			i32 queued = Plat_MusicAudio(PLAT_MUSIC_AUDIO_QUEUED, 0, 0);
			u8 pcm[8192];
			while (!gMusic.ended && queued >= 0 && queued < 44100)
			{
				i32 room = (44100 - queued) & ~3;
				if (!room)
					break;
				i32 count = Plat_DecoderRead(&gMusic.decoder, pcm, room < sizeof(pcm) ? room : sizeof(pcm));
				if (!count)
				{
					gMusic.ended = 1;
					Plat_MusicAudio(PLAT_MUSIC_AUDIO_FLUSH, 0, 0);
					break;
				}
				if (count < 0)
				{
					if (errno != EAGAIN && errno != EINTR)
						queued = -1;
					break;
				}
				if (!Plat_MusicAudio(PLAT_MUSIC_AUDIO_QUEUE, pcm, count))
				{
					queued = -1;
					break;
				}
				gMusic.bytes += count;
				gMusic.lastDataAt = now;
				queued += count;
			}
			if (!gMusic.started && queued >= 0 && (queued >= 17640 || gMusic.ended))
			{
				gMusic.started = Plat_MusicAudio(PLAT_MUSIC_AUDIO_START, 0, 0);
				if (!gMusic.started)
					queued = -1;
			}
			if (queued < 0 || (gMusic.ended && queued == 0) ||
				(!gMusic.ended && (u32)(now - gMusic.lastDataAt) > 10000))
			{
				if (gMusic.trace)
					fprintf(stderr, "MUSIC finished bytes=%u queued=%d\n", gMusic.bytes, queued);
				stopMusicDecoder(&gMusic.decoder);
				Plat_MusicAudio(PLAT_MUSIC_AUDIO_CLOSE, 0, 0);
				if (gMusic.path[0])
					Plat_DecoderRemove(gMusic.path);
				gMusic.path[0] = 0;
				gMusic.active = 0;
				pthread_mutex_unlock(&gMusicLock);
				return 0;
			}
			if (gMusic.trace && (u32)(now - gMusic.traceAt) >= 1000)
			{
				fprintf(stderr, "MUSIC bytes=%u queued=%d started=%d\n", gMusic.bytes, queued, gMusic.started);
				gMusic.traceAt = now;
			}
		}
		pthread_mutex_unlock(&gMusicLock);
		Plat_Sleep(10);
	}
}

// @Bogus
i32 Plat_MusicOpen(const char* path, u32 offset, u32 bytes)
{
	Plat_MusicStop();
	FILE* source = fopen(path, "rb");
	if (!source)
	{
		fprintf(stderr, "Music: cannot open %s: %s\n", path, strerror(errno));
		return 0;
	}
	if (!bytes)
	{
		fseek(source, 0, SEEK_END);
		long size = ftell(source);
		if (size < 44 || size > 0x7FFFFFFF)
		{
			fclose(source);
			return 0;
		}
		bytes = size;
	}
	u32 header[11];
	if (fseek(source, offset, SEEK_SET) != 0 || bytes < sizeof(header) ||
		fread(header, sizeof(header), 1, source) != 1 || memcmp(header, "BIK", 3) ||
		!header[2] || !header[7] || !header[8] || !header[10] || header[10] > 256 || header[1] > bytes - 8)
	{
		fprintf(stderr, "Music: invalid Bink audio header in %s\n", path);
		fclose(source);
		return 0;
	}
	FILE* output = Plat_DecoderTemp(gMusic.path, "spidey-music");
	if (!output)
	{
		fclose(source);
		Plat_MusicStop();
		return 0;
	}
	fseek(source, offset, SEEK_SET);
	u8 chunk[65536];
	u32 left = bytes;
	while (left)
	{
		u32 count = left < sizeof(chunk) ? left : sizeof(chunk);
		if (fread(chunk, 1, count, source) != count || fwrite(chunk, 1, count, output) != count)
			break;
		left -= count;
	}
	fclose(source);
	i32 closed = fclose(output);
	if (left || closed || !Plat_MusicAudio(PLAT_MUSIC_AUDIO_OPEN, 0, 0) ||
		!startMusicDecoder(&gMusic.decoder, gMusic.path))
	{
		Plat_MusicStop();
		return 0;
	}
	gMusic.trace = getenv("SPIDEY_TRACE_MUSIC") != 0;
	gMusic.active = 1;
	gMusic.lastDataAt = Plat_Ticks();
	if (pthread_create(&gMusic.thread, 0, playMusic, 0) != 0)
	{
		Plat_MusicStop();
		return 0;
	}
	gMusic.threadCreated = 1;
	return 1;
}

// @Bogus
i32 Plat_MusicIsPlaying(void)
{
	pthread_mutex_lock(&gMusicLock);
	i32 active = gMusic.active;
	pthread_mutex_unlock(&gMusicLock);
	return active;
}

// @Bogus
void Plat_MusicPause(i32 paused)
{
	pthread_mutex_lock(&gMusicLock);
	gMusic.paused = paused != 0;
	gMusic.lastDataAt = Plat_Ticks();
	if (gMusic.active && gMusic.started)
		Plat_MusicAudio(PLAT_MUSIC_AUDIO_PAUSE, 0, gMusic.paused);
	pthread_mutex_unlock(&gMusicLock);
}

// @Bogus
void Plat_MusicSetVolume(i32 volume)
{
	if (volume < 0)
		volume = 0;
	pthread_mutex_lock(&gMusicLock);
	if (gMusic.active)
		Plat_MusicAudio(PLAT_MUSIC_AUDIO_VOLUME, 0, volume);
	pthread_mutex_unlock(&gMusicLock);
}
