#include "game/application/PCTimer.h"
#include "compatibility/non_win32.h"
#include "core/utils.h"

#include "compatibility/validate.h"

EXPORT i32 gPcTimerPaused;

EXPORT u32 gTimerInitOne;
EXPORT u32 gTimerInitTwo;

#ifndef _WIN32
#define LPTIMECALLBACK void*
#endif
EXPORT LPTIMECALLBACK fptc;

EXPORT f64 gTimerMsInterval;

EXPORT STimerInfo gTimerInfo;
EXPORT f64 gTimerVblankRelated;

#ifndef _WIN32

struct TIMECAPS
{
	u32 wPeriodMin;
	u32 wPeriodMax;
};

void onexit(...)
{
}

void timeBeginPeriod(...)
{
}

#ifdef SPIDEY_STANDALONE
// The Win32 multimedia timer, on the platform layer's timer thread.
#include "platform/plat.h"

static void standaloneTimerThunk(void* user)
{
	TimerCallback(0, 0, reinterpret_cast<unsigned long>(user), 0, 0);
}

u32 timeSetEvent(u32 delay, u32, void*, DWORD user, u32)
{
	return Plat_TimerStart(delay, standaloneTimerThunk, reinterpret_cast<void*>(user));
}

void timeKillEvent(UINT id)
{
	Plat_TimerStop(id);
}

bool timeGetDevCaps(TIMECAPS* pCaps, u32)
{
	pCaps->wPeriodMin = 1;
	pCaps->wPeriodMax = 1000;
	return false;
}
#else
// @FIXME sanity build only, no timer at all
void timeKillEvent(UINT)
{
}

u32 timeSetEvent(...)
{
	return 69;
}

bool timeGetDevCaps(...)
{
	return false;
}
#endif
#endif

TIMECAPS ptc;

// @Ok
void PCTIMER_Init(void)
{
	gTimerInfo.uTimerID = 0;
	gTimerInfo.field_4 = 0;
	ptc.wPeriodMin = 0;
	gTimerInfo.uPeriod = 0;
	gTimerInfo.field_C = 0;
	ptc.wPeriodMax = 0;
	fptc = 0;

	if (timeGetDevCaps(&ptc, sizeof(ptc)))
	{
		gTimerInfo.uTimerID = 0;
		print_if_false(0, "\t\tD3DTimer init error!");
		return;
	}

	UINT wPeriodMin = ptc.wPeriodMin;
	if (wPeriodMin <= 16)
		wPeriodMin = 16;

	UINT wPeriodMax;
	if (wPeriodMin < ptc.wPeriodMax)
	{
		if (ptc.wPeriodMin > 16)
		{
			wPeriodMax = ptc.wPeriodMin;
		}
		else
		{
			wPeriodMax = 16;
		}
	}
	else
	{
		wPeriodMax = ptc.wPeriodMax;
	}

	gTimerInfo.uPeriod = wPeriodMax;

	printf(
		"t\tPCTimer - Min: %i, Max: %i, Ideal: %i, Used:%i\r\n",
		ptc.wPeriodMin,
		ptc.wPeriodMax,
		16,
		wPeriodMax);

	u32 uResolution = gTimerInfo.uPeriod;
    u32 uDelay = 16;

	gTimerInfo.field_4 = 16;

	// make clang happy :)
#ifdef _WIN32
	fptc = TimerCallback;
#else
	fptc = reinterpret_cast<void*>(TimerCallback);
#endif

	if (gTimerInfo.uPeriod > 0x10)
	{
		print_if_false(0, "Timer low resolution error!");
		uResolution = gTimerInfo.uPeriod;
		uDelay = gTimerInfo.uPeriod;
		gTimerInfo.field_4 = gTimerInfo.uPeriod;
	}

	gTimerInfo.uTimerID = timeSetEvent(uDelay, uResolution, fptc, (DWORD)&gTimerInfo, 1);

	gTimerMsInterval = (f64)gTimerInfo.field_4 * 60.0 / 1000.0;
	timeBeginPeriod(gTimerInfo.uPeriod);

	onexit(PCTIMER_Kill);
}

// @Ok
// @Matching
i32 PCTIMER_Kill(void)
{
	if (gTimerInfo.uTimerID)
	{
		timeKillEvent(gTimerInfo.uTimerID);
		gTimerInfo.uTimerID = 0;
	}

	return 0;
}

// @Ok
// @Matching
void PCTIMER_Pause(void)
{
	gPcTimerPaused = 1;
}

// @Ok
// @Matching
void PCTIMER_Resume(void)
{
	gPcTimerPaused = 0;
}

// @Ok
// @Matching
void CALLBACK TimerCallback(
	UINT uTimerID,
	UINT uMsg,
	unsigned long dwUser,
	unsigned long dw1,
	unsigned long dw2)
{
	STimerInfo* pTimeInfo = reinterpret_cast<STimerInfo*>(dwUser);

	if (!gPcTimerPaused)
	{
		gTimerMsInterval = (f64)pTimeInfo->field_4 * 60.0 / 1000.0;
		pTimeInfo->field_C += pTimeInfo->field_4;
		gTimerVblankRelated += gTimerMsInterval;

		u32 newTime = gTimerVblankRelated;
		while ( newTime > G_VBLANKS )
			MyVSync();
	}
	
}

void validate_STimerInfo(void)
{
	VALIDATE_SIZE(STimerInfo, 0x10);

	VALIDATE(STimerInfo, uTimerID, 0x0);
	VALIDATE(STimerInfo, field_4, 0x4);
	VALIDATE(STimerInfo, uPeriod, 0x8);
	VALIDATE(STimerInfo, field_C, 0xC);
}
