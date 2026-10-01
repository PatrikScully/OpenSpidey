// Run on an X11 display with Mesa, SDL_AUDIO_DRIVER=dummy.
#include "../../platform/sdl3/plat_sdl3.cpp"
#include <cassert>
#include <vector>

static std::vector<f32> gains;

extern "C" bool __real_SDL_SetAudioStreamGain(SDL_AudioStream*, float);

// @Bogus
extern "C" bool __wrap_SDL_SetAudioStreamGain(SDL_AudioStream* stream, float gain)
{
	bool result = __real_SDL_SetAudioStreamGain(stream, gain);
	if (result)
		gains.push_back(SDL_GetAudioStreamGain(stream));
	return result;
}

// @Bogus
void Plat_MusicStop(void) {}

// @Bogus
void Plat_MovieStop(void) {}

// @Bogus
extern "C" void __wrap__Z12Plat_GfxFlipv(void) {}

// @Bogus
int main(int argc, char** argv)
{
	setvbuf(stdout, 0, _IONBF, 0);
	setenv("SPIDEY_MASTER_VOLUME", "40", 1);
	setenv("SPIDEY_SFX_VOLUME", "25", 1);
	setenv("SPIDEY_MUSIC_VOLUME", "50", 1);
	assert(fabs(Plat_AudioGain("SPIDEY_SFX_VOLUME") - 0.1f) < 0.00001f);
	setenv("SPIDEY_SFX_VOLUME", "200", 1);
	assert(fabs(Plat_AudioGain("SPIDEY_SFX_VOLUME") - 0.4f) < 0.00001f);
	setenv("SPIDEY_SFX_VOLUME", "25", 1);
	assert(Plat_Init(640, 480, 0));
	i32 drawableWidth = 0, drawableHeight = 0;
	assert(SDL_GetWindowSizeInPixels(gWindow, &drawableWidth, &drawableHeight));
	Plat_GfxBeginScene(0xFF112233, 1);
	GLint viewport[4];
	glGetIntegerv(GL_VIEWPORT, viewport);
	assert(viewport[0] == (drawableWidth - viewport[2]) / 2);
	assert(viewport[1] == (drawableHeight - viewport[3]) / 2);
	assert(abs(viewport[2] * 3 - viewport[3] * 4) <= 4);
	u8 pixel[3];
	glReadPixels(viewport[0] + viewport[2] / 2, viewport[1] + viewport[3] / 2, 1, 1, GL_RGB, GL_UNSIGNED_BYTE, pixel);
	assert(pixel[0] == 17 && pixel[1] == 34 && pixel[2] == 51);
	if (viewport[0] || viewport[1])
	{
		glReadPixels(0, 0, 1, 1, GL_RGB, GL_UNSIGNED_BYTE, pixel);
		assert(pixel[0] == 0 && pixel[1] == 0 && pixel[2] == 0);
	}
	std::vector<u8> capture(640 * 480 * 3);
	assert(Plat_GfxReadPixels(&capture[0], 640, 480));
	assert(capture[0] == 51 && capture[1] == 34 && capture[2] == 17);
	assert(capture[capture.size() - 3] == 51);
	SDL_WindowFlags flags = SDL_GetWindowFlags(gWindow);
	bool exclusive = SDL_GetWindowFullscreenMode(gWindow) != 0;
	printf("CHECK mode=%s output=%dx%d viewport=%d,%d,%d,%d\n",
		(flags & SDL_WINDOW_FULLSCREEN) ? (exclusive ? "fullscreen" : "borderless") : "windowed",
		drawableWidth, drawableHeight, viewport[0], viewport[1], viewport[2], viewport[3]);
	if (argc > 1 && !strcmp(argv[1], "windowed"))
	{
		assert(!(flags & SDL_WINDOW_FULLSCREEN));
		assert(SDL_SetWindowSize(gWindow, 640, 360));
		SDL_SyncWindow(gWindow);
		Plat_Yield();
		Plat_GfxBeginScene(0xFF112233, 1);
		glGetIntegerv(GL_VIEWPORT, viewport);
		assert(viewport[0] == 80 && viewport[1] == 0 && viewport[2] == 480 && viewport[3] == 360);
		printf("CHECK resize viewport=%d,%d,%d,%d\n", viewport[0], viewport[1], viewport[2], viewport[3]);
	}
	if (argc > 1 && !strcmp(argv[1], "borderless"))
		assert((flags & SDL_WINDOW_FULLSCREEN) && !exclusive);
	if (argc > 1 && !strcmp(argv[1], "fullscreen"))
		assert((flags & SDL_WINDOW_FULLSCREEN) && exclusive);
	u8 movie[] = { 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255, 0, 255 };
	Plat_MovieDrawFrame(movie, 2, 2);
	glReadPixels(gViewportX + gViewportWidth / 2, gViewportY + gViewportHeight / 2,
		1, 1, GL_RGB, GL_UNSIGNED_BYTE, pixel);
	assert(pixel[0] == 0 && pixel[1] == 255 && pixel[2] == 0);
	Plat_MovieDrawFrame(0, 0, 0);
	assert(Plat_SndInit());
	assert(fabs(SDL_GetAudioStreamGain(gStream) - 0.1f) < 0.00001f);
	assert(Plat_MusicAudio(PLAT_MUSIC_AUDIO_OPEN, 0, 0));
	assert(fabs(gains.back() - 0.2f) < 0.00001f);
	assert(Plat_MusicAudio(PLAT_MUSIC_AUDIO_VOLUME, 0, 16384));
	assert(fabs(gains.back() - 0.1f) < 0.00001f);
	assert(Plat_MovieAudio(PLAT_MOVIE_AUDIO_OPEN, 0, 0));
	assert(fabs(gains.back() - 0.4f) < 0.00001f);
	assert(Plat_MovieAudio(PLAT_MOVIE_AUDIO_VOLUME, 0, 128));
	assert(fabs(gains.back() - (128 / 255.0f * 0.4f)) < 0.00001f);
	setenv("SPIDEY_MASTER_VOLUME", "0", 1);
	assert(Plat_MusicAudio(PLAT_MUSIC_AUDIO_VOLUME, 0, 32768));
	assert(gains.back() == 0.0f);
	assert(Plat_MovieAudio(PLAT_MOVIE_AUDIO_VOLUME, 0, 255));
	assert(gains.back() == 0.0f);
	Plat_MusicAudio(PLAT_MUSIC_AUDIO_CLOSE, 0, 0);
	Plat_MovieAudio(PLAT_MOVIE_AUDIO_CLOSE, 0, 0);
	Plat_SndShutdown();
	Plat_Shutdown();
	printf("PASS SDL settings, letterbox, capture and audio gains\n");
	return 0;
}
