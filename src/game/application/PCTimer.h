#pragma once

#ifndef PCTIMER_H
#define PCTIMER_H

#include "compatibility/non_win32.h"
#include "compatibility/export.h"

struct STimerInfo
{
	UINT uTimerID;
	UINT field_4;
	UINT uPeriod;
	UINT field_C;
};

EXPORT void PCTIMER_Init(void);
EXPORT i32 PCTIMER_Kill(void);
EXPORT void PCTIMER_Pause(void);
EXPORT void PCTIMER_Resume(void);
EXPORT void CALLBACK TimerCallback(UINT,UINT, unsigned long, unsigned long, unsigned long);

void validate_STimerInfo(void);
#endif
