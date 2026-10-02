// Check the native graphics stack before selecting it for the game.
#include <SDL3/SDL.h>
#include <SDL3/SDL_opengl.h>
#include <cstdio>
#include <cstring>
#include <unistd.h>

// @Bogus
int main()
{
	// A broken driver must not leave startup waiting forever.
	alarm(5);
	if (!SDL_Init(SDL_INIT_VIDEO))
	{
		std::fprintf(stderr, "Runtime video check: %s\n", SDL_GetError());
		return 1;
	}
	SDL_GL_SetAttribute(SDL_GL_CONTEXT_MAJOR_VERSION, 2);
	SDL_GL_SetAttribute(SDL_GL_CONTEXT_MINOR_VERSION, 1);
	SDL_Window* window = SDL_CreateWindow("OpenSpidey graphics check", 32, 32,
		SDL_WINDOW_OPENGL | SDL_WINDOW_HIDDEN);
	SDL_GLContext context = window ? SDL_GL_CreateContext(window) : 0;
	if (!context)
	{
		std::fprintf(stderr, "Runtime graphics check: %s\n", SDL_GetError());
		if (window) SDL_DestroyWindow(window);
		SDL_Quit();
		return 1;
	}
	const char* renderer = reinterpret_cast<const char*>(glGetString(GL_RENDERER));
	const char* version = reinterpret_cast<const char*>(glGetString(GL_VERSION));
	bool hardware = renderer && !std::strstr(renderer, "llvmpipe")
		&& !std::strstr(renderer, "softpipe") && !std::strstr(renderer, "Software")
		&& !std::strstr(renderer, "SwiftShader");
	std::printf("Runtime graphics check: %s / %s\n", renderer ? renderer : "unavailable",
		version ? version : "unavailable");
	SDL_GL_DestroyContext(context);
	SDL_DestroyWindow(window);
	SDL_Quit();
	return hardware ? 0 : 2;
}
