#include "../plat.h"
#include <SDL3/SDL.h>
#include <GL/gl.h>

// @Bogus
i32 Plat_MovieAudio(PlatMovieAudioOp op, const void* pcm, i32 value)
{
	static SDL_AudioStream* stream;
	if (op == PLAT_MOVIE_AUDIO_CLOSE)
	{
		if (stream)
			SDL_DestroyAudioStream(stream);
		stream = 0;
		return 1;
	}
	if (op == PLAT_MOVIE_AUDIO_OPEN)
	{
		if (stream)
			SDL_DestroyAudioStream(stream);
		SDL_AudioSpec spec = { SDL_AUDIO_S16, 2, 44100 };
		stream = SDL_OpenAudioDeviceStream(SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, 0, 0);
		return stream != 0;
	}
	if (!stream)
		return 0;
	switch (op)
	{
		case PLAT_MOVIE_AUDIO_QUEUE:
			return SDL_PutAudioStreamData(stream, pcm, value);
		case PLAT_MOVIE_AUDIO_QUEUED:
			return SDL_GetAudioStreamQueued(stream);
		case PLAT_MOVIE_AUDIO_START:
			return SDL_ResumeAudioStreamDevice(stream);
		case PLAT_MOVIE_AUDIO_VOLUME:
			return SDL_SetAudioStreamGain(stream, value / 255.0f);
		default:
			return 0;
	}
}

// @Bogus
void Plat_MovieDrawFrame(const u8* bgra, i32 width, i32 height)
{
	static GLuint texture;
	if (!bgra)
	{
		if (texture)
			glDeleteTextures(1, &texture);
		texture = 0;
		return;
	}
	glPushAttrib(GL_ALL_ATTRIB_BITS);
	glPushClientAttrib(GL_CLIENT_PIXEL_STORE_BIT);
	GLint viewport[4], matrixMode;
	glGetIntegerv(GL_VIEWPORT, viewport);
	glGetIntegerv(GL_MATRIX_MODE, &matrixMode);
	if (!texture)
		glGenTextures(1, &texture);
	glBindTexture(GL_TEXTURE_2D, texture);
	glPixelStorei(GL_UNPACK_ALIGNMENT, 1);
	glPixelStorei(GL_UNPACK_ROW_LENGTH, 0);
	glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, width, height, 0, GL_BGRA, GL_UNSIGNED_BYTE, bgra);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP_TO_EDGE);
	glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP_TO_EDGE);
	glEnable(GL_TEXTURE_2D);
	glDisable(GL_DEPTH_TEST);
	glDisable(GL_CULL_FACE);
	glDisable(GL_ALPHA_TEST);
	glDisable(GL_BLEND);
	glDisable(GL_FOG);
	glTexEnvi(GL_TEXTURE_ENV, GL_TEXTURE_ENV_MODE, GL_REPLACE);
	glColorMask(GL_TRUE, GL_TRUE, GL_TRUE, GL_TRUE);
	glClearColor(0, 0, 0, 1);
	glClear(GL_COLOR_BUFFER_BIT);
	glMatrixMode(GL_PROJECTION);
	glPushMatrix();
	glLoadIdentity();
	glOrtho(0, viewport[2], viewport[3], 0, -1, 1);
	glMatrixMode(GL_MODELVIEW);
	glPushMatrix();
	glLoadIdentity();
	f32 scale = (f32)viewport[2] / width;
	if (height * scale > viewport[3])
		scale = (f32)viewport[3] / height;
	f32 x = (viewport[2] - width * scale) * 0.5f;
	f32 y = (viewport[3] - height * scale) * 0.5f;
	glBegin(GL_QUADS);
	glTexCoord2f(0, 0); glVertex2f(x, y);
	glTexCoord2f(1, 0); glVertex2f(x + width * scale, y);
	glTexCoord2f(1, 1); glVertex2f(x + width * scale, y + height * scale);
	glTexCoord2f(0, 1); glVertex2f(x, y + height * scale);
	glEnd();
	glPopMatrix();
	glMatrixMode(GL_PROJECTION);
	glPopMatrix();
	glMatrixMode(matrixMode);
	glPopClientAttrib();
	glPopAttrib();
	Plat_GfxFlip();
}
