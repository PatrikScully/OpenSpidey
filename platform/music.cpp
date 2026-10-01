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
		unlink(gMusic.path);
	memset(&gMusic, 0, sizeof(gMusic));
	gMusic.decoder.fd = -1;
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
				i32 count = read(gMusic.decoder.fd, pcm, room < sizeof(pcm) ? room : sizeof(pcm));
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
					unlink(gMusic.path);
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

