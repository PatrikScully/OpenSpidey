#include "compatibility/directx/bmr.h"
#include "game/ui/dcshellutils.h"
#include "core/utils.h"
#include "game/ui/front.h"
#include "engine/rendering/ps2funcs.h"
#include "engine/rendering/PCGfx.h"

#ifndef SPIDEY_STANDALONE
EXPORT Sprite2* gLoadedBmp = 0;
#else
extern Sprite2* gLoadedBmp;
#endif

// @Ok
// @Matching
void BMP_Draw(const char * pName)
{
	if (!G_SCENE_RELATED)
	{
		PCGfx_BeginScene(1, -1);
	}

	DeleteBMP();
	LoadBMP(pName);
	DrawBMP();

	if (G_SCENE_RELATED)
	{
		PCGfx_EndScene(1);
	}

	DeleteBMP();
}

// @Ok
 void DeleteBMP(void)
{
	if (gLoadedBmp)
	{
		delete gLoadedBmp;
		gLoadedBmp = 0;
	}
}

// @Ok
 void DrawBMP(void)
{
	if (gLoadedBmp)
	{
		Front_ClearScreen();
		gLoadedBmp->screenHeight();
		gLoadedBmp->draw(0, 0, 0, 0);
		DrawSync();
	}
}

// @Ok
void LoadBMP(const char * pName)
{
	print_if_false(gLoadedBmp == 0, "BMP already loaded");

	gLoadedBmp = new Sprite2(pName, 1, 0, 0, 3);
	Pause(2);
}
