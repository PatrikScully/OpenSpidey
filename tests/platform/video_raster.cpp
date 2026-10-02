// Run on a private X11 display with Mesa and SDL_AUDIO_DRIVER=dummy.
#include "../../platform/sdl3/plat_sdl3.cpp"
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
	vertices[0].field_0 = vertices[3].field_0 = left;
	vertices[1].field_0 = vertices[2].field_0 = right;
	vertices[0].field_4 = vertices[1].field_4 = top;
	vertices[2].field_4 = vertices[3].field_4 = bottom;
	for (i32 i = 0; i < 4; i++)
	{
		vertices[i].field_C = 1.0f;
		vertices[i].field_10 = 0xFF000000;
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
