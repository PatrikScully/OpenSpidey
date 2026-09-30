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

