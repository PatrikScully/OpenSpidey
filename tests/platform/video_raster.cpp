// Run on a private X11 display with Mesa and SDL_AUDIO_DRIVER=dummy.
#include "platform/sdl3/plat_sdl3.cpp"
#include <cassert>
#include <vector>

i32 gDbgFanBlend, gDbgFanTex, gDbgFanBucket;

// @Bogus
void Plat_MusicStop(void) {}

// @Bogus
void Plat_MovieStop(void) {}

// @Bogus
static void DrawRect(f32 left, f32 top, f32 right, f32 bottom)
{
	SDXPolyField vertices[4];
	memset(vertices, 0, sizeof(vertices));
	vertices[0].x = vertices[3].x = left;
	vertices[1].x = vertices[2].x = right;
	vertices[0].y = vertices[1].y = top;
	vertices[2].y = vertices[3].y = bottom;
	for (i32 i = 0; i < 4; i++)
	{
		vertices[i].reciprocalW = 1.0f;
		vertices[i].colorArgb = 0xFF000000;
	}
	Plat_GfxDrawFan(vertices, 4);
}

// @Bogus
int main(int argc, char** argv)
{
	setvbuf(stdout, 0, _IONBF, 0);
	assert(Plat_Init(640, 480, 0));
	Plat_GfxBeginScene(0xFF336699, 1);
	Plat_GfxSetTexture(0);
	Plat_GfxSetBlendMode(0);
	Plat_GfxSetDepthTest(0);
	// M3d_RenderCleanup (0x4737F0) passes x=0 and integer y values.
	// PCGfx_DrawQuad2D (0x507470) copies x/y and adds width/height.
	// At gWideScreen=30, 640x480 and Yres=240, each bar is 60 high.
	DrawRect(0, 0, 640, 60);
	DrawRect(0, 420, 640, 480);
	DrawRect(0, 0, 60, 480);
	DrawRect(580, 0, 640, 480);
	std::vector<u8> pixels(gViewportWidth * gViewportHeight * 3);
	glPixelStorei(GL_PACK_ALIGNMENT, 1);
	glReadPixels(gViewportX, gViewportY, gViewportWidth, gViewportHeight,
		GL_RGB, GL_UNSIGNED_BYTE, &pixels[0]);
	i32 failures = 0;
	for (i32 y = 0; y < gViewportHeight; y++)
		for (i32 x = 0; x < gViewportWidth; x++)
		{
			bool border = x < gViewportWidth * 60 / 640 || x >= gViewportWidth * 580 / 640
				|| y < gViewportHeight * 60 / 480 || y >= gViewportHeight * 420 / 480;
			const u8* pixel = &pixels[(y * gViewportWidth + x) * 3];
			if ((border && (pixel[0] || pixel[1] || pixel[2]))
				|| (!border && (pixel[0] != 51 || pixel[1] != 102 || pixel[2] != 153)))
				failures++;
		}
	GLint samples = 0;
	glGetIntegerv(GL_SAMPLES, &samples);
	printf("CHECK raster viewport=%dx%d MSAA=%d wrong_pixels=%d\n",
		gViewportWidth, gViewportHeight, samples, failures);
	assert(!failures);
	// Gameplay fills wide outputs and retains the original vertical scale.
	Plat_GfxBeginScene(0xFF336699, 1);
	Plat_GfxSetGameView(1);
	i32 outputWidth = 0, outputHeight = 0;
	assert(SDL_GetWindowSizeInPixels(gWindow, &outputWidth, &outputHeight));
	if ((f32)outputWidth / outputHeight >= 640.0f / 480.0f)
	{
		assert(gViewportWidth == outputWidth && gViewportHeight == outputHeight);
		DrawRect(0, 0, 640, 60);
		DrawRect(260, 180, 380, 300);
		pixels.resize(gViewportWidth * gViewportHeight * 3);
		glReadPixels(0, 0, gViewportWidth, gViewportHeight, GL_RGB, GL_UNSIGNED_BYTE, &pixels[0]);
		// A fullscreen bar reaches both edges; a 120x120 square stays square.
		for (i32 x = 0; x < outputWidth; x++)
			assert(pixels[((outputHeight - 1) * outputWidth + x) * 3] == 0);
		i32 squareWidth = 0;
		for (i32 x = 0; x < outputWidth; x++)
			if (pixels[((outputHeight / 2) * outputWidth + x) * 3] == 0)
				squareWidth++;
		assert(abs(squareWidth - outputHeight / 4) <= 1);
		printf("CHECK widescreen viewport=%dx%d square_width=%d expected=%d\n",
			outputWidth, outputHeight, squareWidth, outputHeight / 4);
		Plat_GfxSetHudView(1);
		DrawRect(20, 200, 60, 240);
		DrawRect(580, 200, 620, 240);
		glReadPixels(0, 0, outputWidth, outputHeight, GL_RGB, GL_UNSIGNED_BYTE, &pixels[0]);
		i32 margin = outputHeight * 40 / 480;
		i32 hudY = outputHeight * 260 / 480;
		assert(pixels[(hudY * outputWidth + margin) * 3] == 0);
		assert(pixels[(hudY * outputWidth + outputWidth - margin) * 3] == 0);
		Plat_GfxSetHudView(0);
	}
	// The following menu frame returns to its original aspect ratio.
	Plat_GfxBeginScene(0, 1);
	assert(gViewportWidth * 480 == gViewportHeight * 640);
	if (argc > 1 && !strcmp(argv[1], "borderless"))
	{
		assert(SDL_GetWindowFlags(gWindow) & SDL_WINDOW_FULLSCREEN);
		assert(!SDL_GetWindowFullscreenMode(gWindow));
		const SDL_DisplayMode* desktop = SDL_GetDesktopDisplayMode(SDL_GetDisplayForWindow(gWindow));
		assert(desktop);
		i32 width = 0, height = 0;
		assert(SDL_GetWindowSizeInPixels(gWindow, &width, &height));
		assert(width == (i32)(desktop->w * desktop->pixel_density));
		assert(height == (i32)(desktop->h * desktop->pixel_density));
		printf("CHECK borderless desktop=%dx%d density=%.2f output=%dx%d\n",
			desktop->w, desktop->h, desktop->pixel_density, width, height);
	}
	Plat_Shutdown();
	printf("PASS opaque rectangle raster and desktop resolution\n");
	return 0;
}
