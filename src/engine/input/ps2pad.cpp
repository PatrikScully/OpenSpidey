#include "engine/input/ps2pad.h"
#include "engine/input/PCInput.h"
#include "game/ui/PCShell.h"
#include "core/utils.h"
#include "engine/input/pcdcPad.h"
#include "engine/persistence/dcmemcard.h"
#include "compatibility/my_assert.h"
#include "game/application/tweak.h"

#include "compatibility/validate.h"

// ---------------------------------------------------------------------------
// The pad state is written from all over the exe, so it gets G_ macros.
// Addresses read out of the original disassembly (tools/functions/*.bin):
//
//   gPadVibrate   0x006610C0  DCPad_Vibrate            mov byte [6610C0h],1
//   gSControl     0x00661100  Pad_GetActuatorTime      mov ax,[edx*2+661278h]
//                             (0x661278 is Motor0Timer, +0x178, stride 0x18C)
//   Pad_IdleTime  0x0066129C  Pad_Button               mov [66129Ch],edx
//   gPadInited    0x006612AC  Pad_InitAtStart          mov dword [6612ACh],1
//   gPadActuator  0x006612B0  Pad_ActuatorOff          mov dword [eax*4+6612B0h],1
//
// gPadOne (0x005502AC) and gPadTwo (0x006612B8) stay repo-local.  A scan of the
// whole .text finds a single read of each, in Pad_ActuatorOn, and no writes
// anywhere, and the exe's .data byte for gPadOne is 0x3B, the value we
// initialise it to.  gAlarmFirst (dcmemcard.cpp, 0x0055028C) is the same case:
// read only, and the exe's four dwords are {1, 2, 7, 8}, exactly our table.
// ---------------------------------------------------------------------------

// @Ok
#ifndef SPIDEY_STANDALONE
SControl gSControl[NUM_CONTROLLERS];
#else
extern SControl gSControl[NUM_CONTROLLERS];
#endif

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 Pad_IdleTime;
#else
extern i32 Pad_IdleTime;
#endif

// @OK
#ifndef SPIDEY_STANDALONE
EXPORT u8 gPadVibrate[5];
#else
extern u8 gPadVibrate[5];
#endif
//#define G_PAD_VIBRATE (gPadVibrate)
#define G_PAD_VIBRATE (reinterpret_cast<u8*>(0x006610C0))

// @Ok
// @FIXME - is it really 2?
#ifndef SPIDEY_STANDALONE
EXPORT i32 gPadActuator[2];
#else
extern i32 gPadActuator[2];
#endif
//#define G_PAD_ACTUATOR (gPadActuator)
#define G_PAD_ACTUATOR (reinterpret_cast<i32*>(0x006612B0))

// @Ok
// Read only, see the note at the top of the file.
#ifndef SPIDEY_STANDALONE
EXPORT u8 gPadOne = 0x3B;
#else
extern u8 gPadOne;
#endif
// @Ok
// Read only, see the note at the top of the file.
#ifndef SPIDEY_STANDALONE
EXPORT u8 gPadTwo;
#else
extern u8 gPadTwo;
#endif

// @Ok
// @Matching
void Pad_ActuatorOff(u8 a1, u8)
{
	G_PAD_ACTUATOR[a1] = 1;
}

// @Ok
// @Matching
 void Pad_SetDigitalMapping(SControl *pControl, i32 a2, i32 a3, i32 a4, i32 a5)
{
	/*
	pControl->field_140 = a2;
	pControl->field_144 = a3;
	pControl->field_148 = a4;
	pControl->field_14C = a5;
	*/
}

// @Ok
// @Matching
u16 Pad_GetActuatorTime(u8 a1, u8 a2)
{
	// @FIXME - not portable
	u16 *p = &G_SCONTROL[a1].Motor0Timer;
	return p[a2];
}

// @Ok
// @Matching
void Pad_ActuatorOn(
		u8 a1,
		u16 a2,
		u8 a3,
		u8 a4)
{
	G_PAD_ACTUATOR[a1] = G_VBLANKS + a2 + 10;
	DCPad_Vibrate(a1, 5 * a3 + 2, gPadOne, gPadTwo);
}

// @Ok
// @Matching
void DCPad_ExpireVibrations(void)
{
	for (i32 i = 0; i < 2; i++)
	{
		u32 v4 = G_PAD_ACTUATOR[i];
		if (v4)
		{
			if (G_VBLANKS > v4)
			{
				pdVibMxStop(gAlarmFirst[2 * i]);
				pdVibMxStop(gAlarmFirst[2 * i + 1]);
				G_PAD_ACTUATOR[i] = 0;
			}
		}
	}
}

// @Bogus
static void nullsub_3(void)
{
}

// @Ok
// @Matching
void DCPad_ShutDownVibrations(void)
{
	for (i32 i = 0; i < 4; i++)
	{
		pdVibMxStop(gAlarmFirst[i]);
	}

	nullsub_3();
	G_PAD_ACTUATOR[0] = 1;
	G_PAD_ACTUATOR[1] = 1;
}

// @Ok
// @NotMatching: uses one more register
i32 DCPad_Vibrate(
		i32 a1,
		i8 a2,
		u8 a3,
		u8 a4)
{
	i32 limit = (a1*2)+2;
	for (i32 i = a1 *2; i < limit; i++)
	{
		if (pdGetPeripheral(6 * (i/2))->pBig->field_0)
		{
			i32 val = gAlarmFirst[i];
			if (pdVibMxIsReady(val) == 1)
			{
				G_PAD_VIBRATE[0] = 1;
				G_PAD_VIBRATE[1] = 1;
				G_PAD_VIBRATE[2] = a2;
				G_PAD_VIBRATE[3] = a3;
				G_PAD_VIBRATE[4] = a4;
				do
				{
				}
				while (pdVibMxStart(val, G_PAD_VIBRATE));
				break;
			}
		}
	}


	return 0;
}


// @Ok
// @Matching
void Pad_Button(SButton* pBut, i32 state)
{
	pBut->TriggeredTime++;
	if (!pBut->Pressed)
	{
		if (state)
		{
			pBut->Triggered = 1;
			pBut->field_2 = pBut->TriggeredTime < 10;
			pBut->TriggeredTime = 0;
		}
	}
	else
	{
		pBut->field_2 = 0;
	}

	if ( state )
	{
		G_PAD_IDLE_TIME = 0;
		pBut->Pressed = 1;
	}
	else
	{
		pBut->Pressed = 0;
	}
#ifdef SPIDEY_STANDALONE
	if (state && getenv("SPIDEY_TRACE_PAD"))
		printf("PADBTN %p state=%d pressed=%d (Up at %p)\n", (void*)pBut, state, (i32)pBut->Pressed, (void*)&G_SCONTROL[0].Up);
#endif

	if ( pBut->Pressed )
	{
		pBut->PressedTime++;
		pBut->ReleasedTime = 0;
	}
	else
	{
		pBut->PressedTime = 0;
		pBut->ReleasedTime++;
	}
}

// @Ok
// @Matching
 void Pad_Clear(SControl *pControl)
{
	if (!pControl)
	{
		for (i32 i = 0; i < 1; i++)
		{
			Pad_Clear(&G_SCONTROL[i]);
		}

		return;
	}


	SButton *pButton = &pControl->Triangle;

	for (i32 i = 0; i < 20; i++)
	{
		pButton[i].Pressed = 0;
		pButton[i].Triggered = 0;
		pButton[i].PressedTime = 0;
		pButton[i].ReleasedTime = 0;
		pButton[i].TriggeredTime = 0;
		pButton[i].field_2 = 0;
	}

	/*
	G_SCONTROL[0].field_168 = 0;
	G_SCONTROL[0].field_169 = 0;
	G_SCONTROL[0].field_16A = 0;
	G_SCONTROL[0].field_16B = 0;
	*/
}

// @Ok
// @Matching
 void Pad_ClearAll(void)
{
	for (i32 i = 0; i < 1; i++)
	{
		Pad_ClearAllOne(i);
	}
}

// @Ok
// @Matching
 void Pad_ClearAllOne(i32 a1)
{
	Pad_Clear(&G_SCONTROL[a1]);

	/*
	G_SCONTROL[a1].field_170 = 0;
	G_SCONTROL[a1].field_16B = 0;
	G_SCONTROL[a1].field_16A = 0;
	G_SCONTROL[a1].field_169 = 0;
	G_SCONTROL[a1].field_168 = 0;
	*/
}

// @Ok
// @Matching
void Pad_ClearTriggers(SControl *pControl)
{
	if (!pControl)
	{
		for (i32 i = 0; i < 1; i++)
		{
			Pad_ClearTriggers(&G_SCONTROL[i]);
		}

		return;
	}

	SButton *pButton = &pControl->Triangle;
	for (i32 i = 0; i < 20; i++)
	{
		pButton[i].Triggered = 0;
		pButton[i].field_2 = 0;
	}
}

#ifndef SPIDEY_STANDALONE
EXPORT i32 gPadInited;
#else
extern i32 gPadInited;
#endif
//#define G_PAD_INITED (gPadInited)
#define G_PAD_INITED (*reinterpret_cast<i32*>(0x006612AC))

// @Bogus
static void nullsub_38()
{
}

// @Ok
// @Matching
void Pad_InitAtStart(void)
{
	DoAssert(1u, "NUMPADS defined as 0");
	DoAssert(G_PAD_INITED == 0, "Control system already initialised");

	for (i32 i = 0; i < 1; i++)
	{
		G_SCONTROL[i].pTriangle = &G_SCONTROL[i].LeftOne;
		G_SCONTROL[i].pSquare = &G_SCONTROL[i].LeftTwo;
		G_SCONTROL[i].pCircle = &G_SCONTROL[i].RightOne;
		G_SCONTROL[i].pX = &G_SCONTROL[i].RightTwo;

		Pad_SetDigitalMapping(
				&G_SCONTROL[i], 
				G_GAMESTATE[0],
				G_GAMESTATE[1],
				G_GAMESTATE[2],
				G_GAMESTATE[3]);

		Pad_SetAnalogueMapping(
				&G_SCONTROL[i],
				3, 2, 1, 0,
				G_GAMESTATE[4],
				G_GAMESTATE[5],
				G_GAMESTATE[6],
				G_GAMESTATE[7]);
	}

	Pad_ClearAll();
	G_PAD_IDLE_TIME = 0;
	G_PAD_INITED = 1;
	nullsub_38();

	Pad_Update();

}

// @Ok
// @Matching
 void Pad_SetAnalogueMapping(
		SControl *pControl,
		u8 a2,
		u8 a3,
		u8 a4,
		u8 a5,
		i32 a6,
		i32 a7,
		i32 a8,
		i32 a9)
{
	/*
	pControl->field_160 = a2;
	pControl->field_161 = a3;
	pControl->field_162 = a4;
	pControl->field_163 = a5;

	pControl->field_150 = a6;
	pControl->field_154 = a7;
	pControl->field_158 = a8;
	pControl->field_15C = a9;
	*/
}

void validate_SControl(void)
{
	VALIDATE_SIZE(SControl, 0x18C);

	VALIDATE(SControl, Triangle, 0x0);
	VALIDATE(SControl, Square, 0x10);
	VALIDATE(SControl, Circle, 0x20);
	VALIDATE(SControl, X, 0x30);

	VALIDATE(SControl, LeftOne, 0x40);
	VALIDATE(SControl, LeftTwo, 0x50);

	VALIDATE(SControl, RightOne, 0x60);
	VALIDATE(SControl, RightTwo, 0x70);

	VALIDATE(SControl, Left, 0x80);
	VALIDATE(SControl, Right, 0x90);
	VALIDATE(SControl, Up, 0xA0);
	VALIDATE(SControl, Down, 0xB0);

	VALIDATE(SControl, AnalogueLeft, 0xC0);
	VALIDATE(SControl, AnalogueRight, 0xD0);

	VALIDATE(SControl, Start, 0xE0);
	VALIDATE(SControl, Select, 0xF0);

	VALIDATE(SControl, Crouch, 0x100);
	VALIDATE(SControl, Jump, 0x110);
	VALIDATE(SControl, SmartBomb, 0x120);
	VALIDATE(SControl, SelectWeapon, 0x130);

	VALIDATE(SControl, DigitalMapping, 0x140);
	VALIDATE(SControl, AnalogueMapping, 0x150);

	VALIDATE(SControl, AnaloguePotMapping, 0x160);

	VALIDATE(SControl, RawAnalogueMoveForwardsBackwards, 0x164);
	VALIDATE(SControl, RawAnalogueMoveLeftRight, 0x165);
	VALIDATE(SControl, RawAnalogueAimForwardsBackwards, 0x166);
	VALIDATE(SControl, RawAnalogueAimLeftRight, 0x167);

	VALIDATE(SControl, AnalogueMoveForwardsBackwards, 0x168);
	VALIDATE(SControl, AnalogueMoveLeftRight, 0x169);
	VALIDATE(SControl, AnalogueAimForwardsBackwards, 0x16A);
	VALIDATE(SControl, AnalogueAimLeftRight, 0x16B);

	VALIDATE(SControl, Type, 0x16C);
	VALIDATE(SControl, ResetCounter, 0x170);

	VALIDATE(SControl, Motor0, 0x174);
	VALIDATE(SControl, Motor1, 0x175);
	VALIDATE(SControl, Lock, 0x176);

	VALIDATE(SControl, AlignCalled, 0x177);

	VALIDATE(SControl, Motor0Timer, 0x178);
	VALIDATE(SControl, Motor1Timer, 0x17A);

	VALIDATE(SControl, pTriangle, 0x17C);
	VALIDATE(SControl, pSquare, 0x180);
	VALIDATE(SControl, pCircle, 0x184);
	VALIDATE(SControl, pX, 0x188);
}

void validate_SButton(void)
{
	VALIDATE_SIZE(SButton, 0x10);

	VALIDATE(SButton, Pressed, 0x0);
	VALIDATE(SButton, Triggered, 0x1);
	VALIDATE(SButton, field_2, 0x2);


	VALIDATE(SButton, PressedTime, 0x4);
	VALIDATE(SButton, ReleasedTime, 0x8);
	VALIDATE(SButton, TriggeredTime, 0xC);
}
