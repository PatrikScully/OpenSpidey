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

