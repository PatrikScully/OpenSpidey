// Stream the original Bink files through ffmpeg. Pipes and the audio queue
// bound decoded memory to one video frame and 250 ms of sound.
#include "plat.h"
#include "decoder_process.h"

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cerrno>

typedef PlatDecoder MovieDecoder;

struct MovieState
{
	MovieDecoder video, audio;
	char path[PLAT_DECODER_PATH_SIZE];
	u8* pixels;
	u32 width, height, fps, fpsDen, frames;
	u32 frame, filled, frameBytes, startedAt, openedAt, lastDataAt, traceAt;
	i32 started, audioEnded, audioEnabled, videoEnded, trace;
};

static MovieState gMovie;

// @Bogus
static void stopDecoder(MovieDecoder* decoder)
{
	Plat_DecoderStop(decoder);
}

// @Bogus
static i32 startDecoder(MovieDecoder* decoder, const char* path, i32 audio)
{
	const char* videoArgs[] = { "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error",
		"-threads", "1", "-i", path, "-map", "0:v:0", "-an", "-threads", "1",
		"-fps_mode", "passthrough", "-pix_fmt", "bgra", "-f", "rawvideo", "pipe:1", 0 };
	const char* audioArgs[] = { "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error",
		"-threads", "1", "-i", path, "-map", "0:a:0", "-vn", "-ac", "2", "-ar", "44100",
		"-f", "s16le", "pipe:1", 0 };
	return Plat_DecoderStart(decoder, audio ? audioArgs : videoArgs);
}

// @Bogus
void Plat_MovieStop(void)
{
	// No decoder owns fd 0 before the first movie is opened.
	if (gMovie.video.pid)
		stopDecoder(&gMovie.video);
	if (gMovie.audio.pid)
		stopDecoder(&gMovie.audio);
	Plat_MovieAudio(PLAT_MOVIE_AUDIO_CLOSE, 0, 0);
	Plat_MovieDrawFrame(0, 0, 0);
	if (gMovie.path[0])
		Plat_DecoderRemove(gMovie.path);
	free(gMovie.pixels);
	memset(&gMovie, 0, sizeof(gMovie));
#ifndef _WIN32
	gMovie.video.fd = gMovie.audio.fd = -1;
#endif
}

// @Bogus
i32 Plat_MovieOpen(const char* path, u32 offset, u32 bytes)
{
	Plat_MovieStop();
	if (getenv("SPIDEY_SKIP_MOVIES") && atoi(getenv("SPIDEY_SKIP_MOVIES")))
		return 0;
	FILE* source = fopen(path, "rb");
	if (!source)
	{
		fprintf(stderr, "Movie: cannot open %s: %s\n", path, strerror(errno));
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
		bytes = (u32)size;
	}
	u32 header[11];
	if (fseek(source, offset, SEEK_SET) != 0 || bytes < sizeof(header) ||
		fread(header, sizeof(header), 1, source) != 1 ||
		memcmp(header, "BIK", 3) != 0 || !header[2] || header[2] > 1000000 ||
		!header[5] || header[5] > 4096 || !header[6] || header[6] > 4096 ||
		!header[7] || !header[8] || header[10] > 256 || header[1] > bytes - 8)
	{
		fprintf(stderr, "Movie: invalid Bink header in %s\n", path);
		fclose(source);
		return 0;
	}
	FILE* output = Plat_DecoderTemp(gMovie.path, "spidey-movie");
	if (!output)
	{
		fclose(source);
		Plat_MovieStop();
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
	if (left || closed != 0)
	{
		Plat_MovieStop();
		return 0;
	}
	gMovie.width = header[5];
	gMovie.height = header[6];
	gMovie.fps = header[7];
	gMovie.fpsDen = header[8];
	gMovie.frames = header[2];
	gMovie.frameBytes = gMovie.width * gMovie.height * 4;
	gMovie.pixels = (u8*)malloc(gMovie.frameBytes);
	gMovie.audioEnded = header[10] == 0;
	gMovie.trace = getenv("SPIDEY_TRACE_MOVIES") != 0;
	if (!gMovie.pixels || !startDecoder(&gMovie.video, gMovie.path, 0))
	{
		Plat_MovieStop();
		return 0;
	}
	if (header[10])
	{
		gMovie.audioEnabled = Plat_MovieAudio(PLAT_MOVIE_AUDIO_OPEN, 0, 0);
		if (!startDecoder(&gMovie.audio, gMovie.path, 1))
		{
			Plat_MovieStop();
			return 0;
		}
	}
	gMovie.openedAt = gMovie.lastDataAt = Plat_Ticks();
	fprintf(stderr, "Movie: %s, %ux%u, %u/%u fps, %u frames\n",
		path, gMovie.width, gMovie.height, gMovie.fps, gMovie.fpsDen, gMovie.frames);
	return 1;
}

// @Bogus
static void feedMovieAudio(void)
{
	if (gMovie.audioEnded)
		return;
	i32 queued = Plat_MovieAudio(PLAT_MOVIE_AUDIO_QUEUED, 0, 0);
	if (queued < 0)
		queued = 0;
	u8 pcm[8192];
	while (queued < 44100)
	{
		i32 room = (44100 - queued) & ~3;
		if (room <= 0)
			break;
		i32 count = read(gMovie.audio.fd, pcm, room < sizeof(pcm) ? room : sizeof(pcm));
		if (count == 0 || (count < 0 && errno != EAGAIN && errno != EINTR))
		{
			gMovie.audioEnded = 1;
			break;
		}
		if (count < 0)
			break;
		if (gMovie.audioEnabled)
			Plat_MovieAudio(PLAT_MOVIE_AUDIO_QUEUE, pcm, count);
		queued += count;
	}
}

// @Bogus
i32 Plat_MovieNextFrame(void)
{
	if (!gMovie.pixels || !Plat_Yield())
	{
		Plat_MovieStop();
		return 0;
	}
	feedMovieAudio();
	u32 now = Plat_Ticks();
	while (gMovie.filled < gMovie.frameBytes && !gMovie.videoEnded)
	{
		i32 count = read(gMovie.video.fd, gMovie.pixels + gMovie.filled,
			gMovie.frameBytes - gMovie.filled);
		if (count == 0 || (count < 0 && errno != EAGAIN && errno != EINTR))
		{
			gMovie.videoEnded = 1;
			break;
		}
		if (count < 0)
			break;
		gMovie.filled += count;
		gMovie.lastDataAt = now;
	}
	if (!gMovie.started && gMovie.filled == gMovie.frameBytes &&
		(gMovie.audioEnded || !gMovie.audioEnabled ||
		 Plat_MovieAudio(PLAT_MOVIE_AUDIO_QUEUED, 0, 0) >= 17640))
	{
		gMovie.started = 1;
		gMovie.startedAt = now;
		Plat_MovieAudio(PLAT_MOVIE_AUDIO_START, 0, 0);
	}
	f64 elapsed = (u32)(now - gMovie.startedAt);
	f64 frameTime = (f64)gMovie.frame * 1000.0 * gMovie.fpsDen / gMovie.fps;
	if (gMovie.started && elapsed >= frameTime && gMovie.filled == gMovie.frameBytes)
	{
		Plat_MovieDrawFrame(gMovie.pixels, gMovie.width, gMovie.height);
		gMovie.filled = 0;
		gMovie.frame++;
		if (gMovie.trace && (gMovie.frame == 1 || (u32)(now - gMovie.traceAt) >= 1000))
		{
			fprintf(stderr, "MOVIE frame=%u/%u time=%.0f audio=%d\n", gMovie.frame,
				gMovie.frames, elapsed, Plat_MovieAudio(PLAT_MOVIE_AUDIO_QUEUED, 0, 0));
			gMovie.traceAt = now;
		}
	}
	if ((gMovie.videoEnded && (!gMovie.started || elapsed >= frameTime)) ||
		(!gMovie.started && (u32)(now - gMovie.openedAt) > 10000) ||
		(gMovie.started && gMovie.frame < gMovie.frames &&
		 gMovie.filled < gMovie.frameBytes && (u32)(now - gMovie.lastDataAt) > 10000))
	{
		if (gMovie.frame != gMovie.frames)
			fprintf(stderr, "Movie: decoder stopped at frame %u of %u\n", gMovie.frame, gMovie.frames);
		Plat_MovieStop();
		return 0;
	}
	Plat_Sleep(2);
	return 1;
}

// @Bogus
void Plat_MovieSetVolume(i32 volume)
{
	if (volume < 0) volume = 0;
	if (volume > 255) volume = 255;
	Plat_MovieAudio(PLAT_MOVIE_AUDIO_VOLUME, 0, volume);
}
