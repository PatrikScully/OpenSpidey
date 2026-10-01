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

