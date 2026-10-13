#include "engine/input/pcdcPad.h"
#include "engine/input/PCInput.h"
#include "engine/input/ps2pad.h"
#include "game/ui/PCShell.h"


#include <cstring>
#include <cstdio>
#include <cstdlib>
#include "compatibility/validate.h"



#define NUM_PADS 1
EXPORT SPdPadSmall gPdPeriRelated[NUM_PADS];
EXPORT SPdPadBig gBigPad[NUM_PADS];

// @Ok
// @Matching
i32 Pad_Update(void)
{
	G_PAD_IDLE_TIME++;
	u32 v3;
	u32 v4;
	PCINPUT_GetMappedStates(&v3, &v4);
#ifdef SPIDEY_STANDALONE
	{
		// SPIDEY_TRACE_PAD=1: print the mapped button word whenever it changes
		static i32 tracePad = -1;
		static u32 lastV3 = 0;
		if (tracePad < 0)
			tracePad = getenv("SPIDEY_TRACE_PAD") ? 1 : 0;
		if (tracePad && v3 != lastV3)
		{
			printf("PAD v3=0x%x v4=0x%x\n", v3, v4);
			lastV3 = v3;
		}
	}
#endif

	if (PCSHELL_UpdateMouse())
		G_PAD_IDLE_TIME = 0;

	for (i32 i = 0; i < NUM_CONTROLLERS; i++)
	{
		// 0x505775: "mov dword ptr [eax+66126Ch],41h", the controller type the
		// shell tests for "pad unplugged" (CheckForPadUnplugged loops forever
		// without it)
		G_SCONTROL[i].Type = 0x41;
		Pad_Button(&G_SCONTROL[i].Triangle, v3 & 0x80);
		Pad_Button(&G_SCONTROL[i].Square, v3 & 0x40);
		Pad_Button(&G_SCONTROL[i].Circle, v3 & 0x20);
		Pad_Button(&G_SCONTROL[i].X, v3 & 0x10);
		Pad_Button(&G_SCONTROL[i].LeftOne, v3 & 0x100);
		Pad_Button(&G_SCONTROL[i].LeftTwo, 0);
		Pad_Button(&G_SCONTROL[i].RightOne, v3 & 0x200);
		Pad_Button(&G_SCONTROL[i].RightTwo, v3 & 0x400);
		Pad_Button(&G_SCONTROL[i].Left, v3 & 4);
		Pad_Button(&G_SCONTROL[i].Right, v3 & 8);
		Pad_Button(&G_SCONTROL[i].Up, v3 & 1);
		Pad_Button(&G_SCONTROL[i].Down, v3 & 2);
		Pad_Button(&G_SCONTROL[i].AnalogueLeft, 0);
		Pad_Button(&G_SCONTROL[i].AnalogueRight, 0);
		Pad_Button(&G_SCONTROL[i].Start, v3 & 0x1000);
		Pad_Button(&G_SCONTROL[i].Select, 0);
		if ( !i )
		{
			Pad_Button(&G_SCONTROL[0].Crouch, v3 & 0x10);
			Pad_Button(&G_SCONTROL[0].Jump, v3 & 0x80);
			Pad_Button(&G_SCONTROL[0].SmartBomb, v3 & 0x40);
			Pad_Button(&G_SCONTROL[0].SelectWeapon, v3 & 0x20);
		}
	}

	DCPad_ExpireVibrations();
	return 0;
}

// @Ok
// @Matching
void pdInitPeripheral(void)
{
	memset(gPdPeriRelated, 0, sizeof(SPdPadSmall) * NUM_PADS);

	for (i32 i = 0; i < NUM_PADS; i++)
	{
		SPdPadBig *pBig = &gBigPad[i];

		gPdPeriRelated[i].pBig = pBig;
		gPdPeriRelated[i].pBig->field_0 = 1;
	}

	PCINPUT_Initialize();
}

// @Ok
// @Matching
void pdExitPeripheral(void)
{
	PCINPUT_Shutdown();
}

// @Ok
// @Matching
SPdPadSmall* pdGetPeripheral(u32 a1)
{
	return &gPdPeriRelated[a1 / 6];
}

// @Ok
// @Matching
i32 pdVibMxStart(i32, u8*)
{
	if (PCINPUT_SetupForceFeedbackSineEffect(2000, 5.0f))
		PCINPUT_StartForceFeedbackEffect();
	return 0;
}

// @Ok
// @Matching
i32 pdVibMxStop(i32)
{
	PCINPUT_StopForceFeedbackEffect();
	return 0;
}

// @Ok
// @Matching
i32 pdVibMxIsReady(i32)
{
	return 1;
}

// @Ok
// @Matching
i32 pdTmrAlarm(i32, u8*)
{
	return 0;
}

void validate_SPdPadBig(void)
{
	VALIDATE_SIZE(SPdPadBig, 0x78);

	VALIDATE(SPdPadBig, field_0, 0x0);
}

void validate_SPdPadSmall(void)
{
	VALIDATE_SIZE(SPdPadSmall, 0x34);

	VALIDATE(SPdPadSmall, pBig, 0x30);
}
