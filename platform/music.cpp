// Stream the original Bink voice files independently of FMV audio.
#include "plat.h"
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cerrno>
#include <fcntl.h>
#include <pthread.h>
#include <signal.h>
#include <spawn.h>
#include <sys/wait.h>
#include <unistd.h>

extern char** environ;

struct MusicDecoder
{
	pid_t pid;
	i32 fd;
};

struct MusicState
{
	MusicDecoder decoder;
	pthread_t thread;
	char path[64];
	i32 threadCreated, stop, active, paused, started, ended, trace;
	u32 lastDataAt, bytes, traceAt;
};

static MusicState gMusic;
static pthread_mutex_t gMusicLock = PTHREAD_MUTEX_INITIALIZER;

// @Bogus
static void stopMusicDecoder(MusicDecoder* decoder)
{
	if (decoder->pid > 0)
	{
		kill(decoder->pid, SIGKILL);
		while (waitpid(decoder->pid, 0, 0) < 0 && errno == EINTR)
			;
	}
	if (decoder->fd >= 0)
		close(decoder->fd);
	decoder->fd = -1;
	decoder->pid = 0;
}

// @Bogus
static i32 startMusicDecoder(MusicDecoder* decoder, const char* path)
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
	const char* args[] = { "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error",
		"-threads", "1", "-i", path, "-map", "0:a:0", "-vn", "-ac", "2", "-ar", "44100",
		"-f", "s16le", "pipe:1", 0 };
	i32 err = posix_spawnp(&decoder->pid, "ffmpeg", &actions, 0, (char* const*)args, environ);
	posix_spawn_file_actions_destroy(&actions);
	close(pipes[1]);
	if (err)
	{
		close(pipes[0]);
		decoder->pid = 0;
		fprintf(stderr, "Music: cannot start ffmpeg: %s. Install ffmpeg to play voice tracks.\n", strerror(err));
		return 0;
	}
	decoder->fd = pipes[0];
	fcntl(decoder->fd, F_SETFL, O_NONBLOCK);
	return 1;
}

