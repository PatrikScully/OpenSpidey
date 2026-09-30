// Stream the original Bink files through ffmpeg. Pipes and the audio queue
// bound decoded memory to one video frame and 250 ms of sound.
#include "plat.h"

#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cerrno>
#include <fcntl.h>
#include <signal.h>
#include <spawn.h>
#include <sys/wait.h>
#include <unistd.h>

extern char** environ;

struct MovieDecoder
{
	pid_t pid;
	i32 fd;
};

struct MovieState
{
	MovieDecoder video, audio;
	char path[64];
	u8* pixels;
	u32 width, height, fps, fpsDen, frames;
	u32 frame, filled, frameBytes, startedAt, openedAt, lastDataAt, traceAt;
	i32 started, audioEnded, audioEnabled, videoEnded, trace;
};

static MovieState gMovie;

// @Bogus
static void stopDecoder(MovieDecoder* decoder)
{
	if (decoder->fd >= 0)
		close(decoder->fd);
	decoder->fd = -1;
	if (decoder->pid > 0)
	{
		kill(decoder->pid, SIGKILL);
		while (waitpid(decoder->pid, 0, 0) < 0 && errno == EINTR)
			;
	}
	decoder->pid = 0;
}

// @Bogus
static i32 startDecoder(MovieDecoder* decoder, const char* path, i32 audio)
{
	i32 pipes[2];
	if (pipe(pipes) != 0)
		return 0;
	fcntl(pipes[0], F_SETFD, FD_CLOEXEC);
	fcntl(pipes[1], F_SETFD, FD_CLOEXEC);
	posix_spawn_file_actions_t actions;
	posix_spawn_file_actions_init(&actions);
	posix_spawn_file_actions_adddup2(&actions, pipes[1], STDOUT_FILENO);
	posix_spawn_file_actions_addclose(&actions, pipes[0]);
	posix_spawn_file_actions_addclose(&actions, pipes[1]);
	const char* videoArgs[] = { "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error",
		"-threads", "1", "-i", path, "-map", "0:v:0", "-an", "-threads", "1",
		"-fps_mode", "passthrough", "-pix_fmt", "bgra", "-f", "rawvideo", "pipe:1", 0 };
	const char* audioArgs[] = { "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error",
		"-threads", "1", "-i", path, "-map", "0:a:0", "-vn", "-ac", "2", "-ar", "44100",
		"-f", "s16le", "pipe:1", 0 };
	i32 err = posix_spawnp(&decoder->pid, "ffmpeg", &actions, 0,
		(char* const*)(audio ? audioArgs : videoArgs), environ);
	posix_spawn_file_actions_destroy(&actions);
	close(pipes[1]);
	if (err)
	{
		close(pipes[0]);
		decoder->pid = 0;
		fprintf(stderr, "Movie: cannot start ffmpeg: %s. Install ffmpeg to play cinematics.\n", strerror(err));
		return 0;
	}
	decoder->fd = pipes[0];
	fcntl(decoder->fd, F_SETFL, O_NONBLOCK);
	return 1;
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
		unlink(gMovie.path);
	free(gMovie.pixels);
	memset(&gMovie, 0, sizeof(gMovie));
	gMovie.video.fd = gMovie.audio.fd = -1;
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
	strcpy(gMovie.path, "/tmp/spidey-movie-XXXXXX");
	i32 temp = mkstemp(gMovie.path);
	if (temp < 0)
	{
		fclose(source);
		Plat_MovieStop();
		return 0;
	}
	FILE* output = fdopen(temp, "wb");
	if (!output)
	{
		close(temp);
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

