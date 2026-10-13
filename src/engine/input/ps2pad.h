#pragma once

#ifndef PS2PAD_H
#define PS2PAD_H

#include "compatibility/export.h"

struct SButton
{
	u8 Pressed;
	u8 Triggered;
	u8 field_2;

	i32 PressedTime;
	i32 ReleasedTime;
	i32 TriggeredTime;
};

struct SControl
{
	SButton Triangle;
	SButton Square;
	SButton Circle;
	SButton X;
	SButton LeftOne;
	SButton LeftTwo;
	SButton RightOne;
	SButton RightTwo;
	SButton Left;
	SButton Right;
	SButton Up;
	SButton Down;

	SButton AnalogueLeft;
	SButton AnalogueRight;

	SButton Start;
	SButton Select;

	SButton Crouch;
	SButton Jump;
	SButton SmartBomb;
	SButton SelectWeapon;

	// tentative names, set by Pad_SetDigitalMapping / Pad_SetAnalogueMapping
	// (Pad_InitAtStart stores gGameState[0..7] here, Font::getCharIndex reads DigitalMapping)
	i32 DigitalMapping[4];
	i32 AnalogueMapping[4];

	u8 AnaloguePotMapping[4];
	u8 RawAnalogueMoveForwardsBackwards;
	u8 RawAnalogueMoveLeftRight;
	u8 RawAnalogueAimForwardsBackwards;
	u8 RawAnalogueAimLeftRight;

	i8 AnalogueMoveForwardsBackwards;
	i8 AnalogueMoveLeftRight;
	i8 AnalogueAimForwardsBackwards;
	i8 AnalogueAimLeftRight;

	i32 Type;
	u32 ResetCounter;


	u8 Motor0;
	u8 Motor1;
	u8 Lock;

	u8 AlignCalled;

	u16 Motor0Timer;
	u16 Motor1Timer;

	SButton *pTriangle;
	SButton *pSquare;
	SButton *pCircle;
	SButton *pX;
};

EXPORT void Pad_SetDigitalMapping(SControl*, i32, i32, i32, i32);
EXPORT void Pad_ActuatorOff(u8, u8);
EXPORT u16 Pad_GetActuatorTime(u8, u8);
EXPORT void Pad_ActuatorOn(u8, u16, u8, u8);
EXPORT void DCPad_ExpireVibrations(void);
EXPORT void DCPad_ShutDownVibrations(void);
EXPORT i32 DCPad_Vibrate(i32,signed char,u8,u8);
EXPORT void Pad_Button(SButton *,i32);
EXPORT void Pad_Clear(SControl *);
EXPORT void Pad_ClearAll(void);
EXPORT void Pad_ClearAllOne(i32);
EXPORT void Pad_ClearTriggers(SControl *);
EXPORT void Pad_InitAtStart(void);
EXPORT void Pad_SetAnalogueMapping(SControl *,u8,u8,u8,u8,i32,i32,i32,i32);
EXPORT i32 Pad_Update(void);

void validate_SControl(void);
void validate_SButton(void);

static const i32 NUM_CONTROLLERS = 1;
EXPORT extern SControl gSControl[NUM_CONTROLLERS];
//#define G_SCONTROL (gSControl)
#define G_SCONTROL (reinterpret_cast<SControl*>(0x00661100))

// 0x0066129C. Frames since the last button press. Pad_Update (pcdcPad.cpp)
// bumps it, Pad_Button and a dozen menu and cutscene routines in the exe zero
// it, so it has to be the exe's dword.
EXPORT extern i32 Pad_IdleTime;
//#define G_PAD_IDLE_TIME (Pad_IdleTime)
#define G_PAD_IDLE_TIME (*reinterpret_cast<i32*>(0x0066129C))

#endif
