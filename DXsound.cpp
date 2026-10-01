#include "DXsound.h"
#if defined(SPIDEY_STANDALONE) && !defined(_WIN32)
#include <execinfo.h>
#endif
#ifdef SPIDEY_STANDALONE
i32 gDbgFanBlend, gDbgFanTex, gDbgFanBucket;   // debugging aids for the GLFAN log
#endif
#ifdef SPIDEY_STANDALONE
#include "platform/plat.h"

// DirectInput buffered data gives the game edge states: 0xFF = went down
// this poll, 0x7F = still down, 0x80 = went up this poll, 0 = up. The
// platform layer only reports "down or not", this rebuilds the edges.
static u8 edgeState(u8 prev, i32 down)
{
	i32 wasDown = (prev & 0x7F) != 0;
	if (down)
		return wasDown ? 0x7F : 0xFF;
	return wasDown ? 0x80 : 0;
}

static f32 gStandaloneRumbleStrength = 0.5f;
#endif
#include "DXinit.h"
#include "SpideyDX.h"
#include "validate.h"
#include "pcdcFile.h"

#include <cstring>
#include <cstdlib>

// The currently-bound D3D texture surface, tracked by DXPOLY_SetTexture to
// skip redundant SetTexture calls. Same class of bug as G_D3DDEVICE7 in
// DXinit.h: DXPOLY_SetTexture is called directly from hooked game logic, so
// this has to be the exe's copy too. Address from the same disassembly,
// "mov [6B7A74h],edi" at the end of DXPOLY_SetTexture.
#ifndef SPIDEY_STANDALONE
EXPORT LPDIRECTDRAWSURFACE7 gDDSurface7;
#else
extern LPDIRECTDRAWSURFACE7 gDDSurface7;
#endif
//#define G_DD_SURFACE7 (gDDSurface7)
#define G_DD_SURFACE7 (*reinterpret_cast<LPDIRECTDRAWSURFACE7*>(0x006B7A74))
EXPORT bool gTexAlpha = false;
EXPORT u32 dword_6B7A8C;
EXPORT f32 flt_56817C = 10.0f;
EXPORT i32 dword_568184;
EXPORT DXPOLY* gSceneBuffer[0x1001];
EXPORT u8 gInBeginScene;
EXPORT i32 gScreenshotNumber;
EXPORT u32 gCurrentBlendMode;
EXPORT char* gD3DDepthCompareNames[9] =
{
	"",
    "D3DCMP_NEVER",
    "D3DCMP_LESS",
    "D3DCMP_EQUAL",
    "D3DCMP_LESSEQUAL",
    "D3DCMP_GREATER",
    "D3DCMP_NOTEQUAL",
    "D3DCMP_GREATEREQUAL",
    "D3DCMP_ALWAYS",
};

EXPORT D3DVALUE gFlDepthCompare = 1.0f;
EXPORT u32 gDepthCompareIndex;
EXPORT u8 gDepthBuffering;
EXPORT DWORD gMagFilters[2] = { 1, 2 };
EXPORT DWORD gMinFilters[2] = { 1, 2 };

EXPORT u32 gCurrentFilterIndex;

EXPORT bool gDepthWriting;
#ifndef SPIDEY_STANDALONE
EXPORT bool gDxPolyRelated;
#else
extern bool gDxPolyRelated;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT i32 gHudOffset;
#else
extern i32 gHudOffset;
#endif
EXPORT f32 gFlHudOffset = 1.0f;

EXPORT D3DCOLOR gDxPolyBackgroundColor = 0x0FF000000;
EXPORT u32 gDxOutlineColor = 0x0FF00FF00;

// @Ok
EXPORT LPDIRECTSOUNDBUFFER g_pDSBuffer;

// @Ok
EXPORT IDirectSoundBuffer* gDxSoundBuffers[0x80];

// @Ok
EXPORT SDDXSoundHolder gDxSoundHolder[0x20];

// @Ok
EXPORT LPDIRECTINPUTDEVICE8A g_pKeyboard;

// @Ok
EXPORT LPDIRECTINPUTDEVICE8A g_pMouse;

//@Ok
EXPORT LPDIRECTINPUTDEVICE8A gControllerRelated;

// @Ok
EXPORT LPDIRECTINPUTEFFECT gForceFeedbackRelated;

// @Ok
EXPORT i32 gNumControllerButtons;

// @Ok
EXPORT u8 gKeyState[0x100];

// @Ok
EXPORT u8 gControllerButtonState[0x20];

// @Ok
EXPORT u8 gMouseButtonState[3];

EXPORT char* gDxKeyNames[0x100] = 
{
	"NULL",
	"ESC",
	"1",
	"2",
	"3",
	"4",
	"5",
	"6",
	"7",
	"8",
	"9",
	"0",
	"DASH",
	"EQUAL",
	"BACK",
	"TAB",
	"Q",
	"W",
	"E",
	"R",
	"T",
	"Y",
	"U",
	"I",
	"O",
	"P",
	"LBRACE",
	"RBRACE",
	"RETURN",
	"LCTRL",
	"A",
	"S",
	"D",
	"F",
	"G",
	"H",
	"J",
	"K",
	"L",
	"COLON",
	"QUOTE",
	"TILDE",
	"LSHIFT",
	"BKSLASH",
	"Z",
	"X",
	"C",
	"V",
	"B",
	"N",
	"M",
	"COMMA",
	"PERIOD",
	"SLASH",
	"RSHIFT",
	"STAR",
	"LALT",
	"SPACE",
	"CAPSLOCK",
	"F1",
	"F2",
	"F3",
	"F4",
	"F5",
	"F6",
	"F7",
	"F8",
	"F9",
	"F10",
	"NUMLOCK",
	"SCROLL",
	"PAD7",
	"PAD8",
	"PAD9",
	"PADMINUS",
	"PAD4",
	"PAD5",
	"PAD6",
	"PADPLUS",
	"PAD1",
	"PAD2",
	"PAD3",
	"PAD0",
	"PadDel",
	".54.",
	".55.",
	"OEM_102",
	"F11",
	"F12",
	".59.",
	".5A.",
	".5B.",
	".5C.",
	".5D.",
	".5E.",
	".5F.",
	".60",
	".61.",
	".62.",
	".63.",
	"F13",
	"F14",
	"F15",
	".64.",
	".68",
	".69.",
	".6A.",
	".6B.",
	".6C.",
	".6D.",
	".6E.",
	".6F.",
	"KANA",
	".71.",
	".72.",
	"ABNT_C1",
	".74.",
	".75.",
	".76.",
	".77.",
	".78.",
	".79.",
	"CONVERT",
	".7B.",
	".7C.",
	".7D.",
	"NOCONVERT",
	".7F.",
	".80.",
	".81.",
	".82.",
	"YEN",
	"ABNT_C2",
	".85.",
	".86.",
	".87.",
	".88.",
	".89.",
	".8A.",
	".8B.",
	".8C.",
	"PADEQ",
	".8E.",
	".8F.",
	"PREVTRK",
	"AT",
	"COLON",
	"UNDERLINE",
	"KANJI",
	"STOP",
	"AX",
	".97.",
	".98.",
	"NEXTTRK",
	".9A.",
	".9B.",
	"PADENTR",
	"RCTRL",
	".9E.",
	"MUTE",
	"CALC",
	"PLAYPAUSE",
	".A2.",
	"MEDIASTOP",
	".A4.",
	".A5.",
	".A6.",
	".A7.",
	".A8.",
	".A9.",
	"VOLDOWN",
	".AB.",
	".AC.",
	".AD.",
	".AE.",
	"VOLUMEUP",
	".B0.",
	".B1.",
	"WEBHOME",
	"PADCOMMA",
	".B4.",
	"PADSLASH",
	".B6.",
	"SYSRQ",
	"RALT",
	".B0.",
	".BA.",
	".BB.",
	".BC.",
	".BD.",
	".BE.",
	".BF.",
	".C0.",
	".C1.",
	".C2.",
	".C3.",
	".C4.",
	"PAUSE",
	".C6.",
	"HOME",
	"UP",
	"PGUP",
	".CA.",
	"LEFT",
	".CC.",
	"RIGHT",
	".CE.",
	"END",
	"DOWN",
	"PGDOWN",
	"INS",
	"DEL",
	".D4.",
	".D5.",
	".D6.",
	".D7.",
	".D8.",
	".D9.",
	".DA.",
	"LWIN",
	"RWIN",
	"APPS",
	".DE.",
	"POWER",
	"SLEEP",
	".E1.",
	".E2.",
	".E3.",
	"WAKE",
	"WEBSEARCH",
	"WEBFAV",
	"WEBREF",
	".E8.",
	"WEBSTOP",
	"WEBFWD",
	"WEBBACK",
	"MYCOMPUTER",
	"MAIL",
	"MEDIASLCT",
	".EF.",
	".F0.",
	".F1.",
	".F2.",
	".F3.",
	".F4.",
	".F5.",
	".F6.",
	".F7.",
	".F8.",
	".F9.",
	".FA.",
	".FB.",
	".FC.",
	".FD.",
	".FE.",
	".FF.",
};


// @Ok
EXPORT LPDIRECTINPUT8 g_pDI;

// @Ok
EXPORT HWND gDxInputHwnd;

EXPORT u8 gDxInputRelated;

// @Ok
// @Matching
void DXINPUT_GetKeyName(u8 key, char* dstName)
{
	strcpy(dstName, gDxKeyNames[key]);
}

// @Ok
// @Matching
u8 DXINPUT_GetControllerButtonState(u8 button)
{
	return gControllerButtonState[button];
}

// @Ok
// @Matching
u8 DXINPUT_GetKeyState(u8 key)
{
	return gKeyState[key];
}

// @Ok
// @Matching
u8 DXINPUT_GetMouseButtonState(u8 button)
{
	return gMouseButtonState[button];
}

// @Ok
// @Matching
i32 DXINPUT_GetNumControllerButtons(void)
{
	return gNumControllerButtons;
}

// @Ok
// @Matching
void DXINPUT_Initialize(LPDIRECTINPUT8 a1, HWND a2)
{
	g_pDI = a1;
	gDxInputHwnd = a2;

	g_pKeyboard = 0;
	g_pMouse = 0;
	gControllerRelated = 0;
	gForceFeedbackRelated = 0;
	
	memset(gKeyState, 0, sizeof(gKeyState));
	memset(gMouseButtonState, 0, sizeof(gMouseButtonState));
	memset(gControllerButtonState, 0, sizeof(gControllerButtonState));

	gDxInputRelated = 0;
	gNumControllerButtons = 0;
}

// @Ok
// @Matching
i32 DXINPUT_PollController(i32 *pX, i32 *pY, i32 *pZ)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	DWORD dwElements = 16;
	DIDEVICEOBJECTDATA didod[16];
	memset(didod, 0, sizeof(didod));

	if (gControllerRelated)
	{
		HRESULT hr = gControllerRelated->Poll();
		if (hr == DIERR_INPUTLOST || hr == DIERR_NOTACQUIRED)
		{
			hr = gControllerRelated->Acquire();
			if (hr == DIERR_OTHERAPPHASPRIO)
			{
				DXERR_printf("Other application has priority when attempting to acquire controller\n");
				return 0;
			}

			DI_ERROR_LOG_AND_QUIT(hr);
			gControllerRelated->Poll();
		}
		else if (FAILED(hr))
		{
			return 0;
		}

		gControllerRelated->GetDeviceData(sizeof(DIDEVICEOBJECTDATA), didod, &dwElements, 0);

		for (i32 i = 0; i < 32; i++)
			gControllerButtonState[i] &= ~0x80u;

		for (DWORD k = 0; k < dwElements; k++)
		{
			if (didod[k].dwOfs >= DIJOFS_BUTTON0 && didod[k].dwOfs < (DWORD)(gNumControllerButtons + DIJOFS_BUTTON0))
			{
				if (didod[k].dwData & 0x80)
				{
					gControllerButtonState[didod[k].dwOfs - DIJOFS_BUTTON0] = -1;
				}
				else
				{
					gControllerButtonState[didod[k].dwOfs - DIJOFS_BUTTON0] = 0x80;
				}
			}
			else if (didod[k].dwOfs == DIJOFS_X)
			{
				*pX = didod[k].dwData;
			}
			else if (didod[k].dwOfs == DIJOFS_Y)
			{
				*pY = didod[k].dwData;
			}
			else if (didod[k].dwOfs == DIJOFS_POV(0))
			{
				*pZ = didod[k].dwData;
			}
		}

		return 1;
	}
#elif defined(SPIDEY_STANDALONE)
	u8 buttons[32];
	i32 numButtons;
	u32 pov;
	if (!Plat_InputPollController(pX, pY, &pov, buttons, &numButtons))
		return 0;
	*pZ = pov;
	gNumControllerButtons = numButtons;
	for (i32 i = 0; i < 32; i++)
		gControllerButtonState[i] = edgeState(gControllerButtonState[i], buttons[i] & 0x80);
	return 1;
#endif

	return 0;
}

// @Ok
i32 DXINPUT_PollKeyboard(void)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	DWORD dwElements = 16;
	DIDEVICEOBJECTDATA didod[16]; 
	memset(didod, 0, sizeof(didod));

	if (!g_pKeyboard)
	{
		return -1;
	}

	if (g_pKeyboard->GetDeviceData(sizeof(DIDEVICEOBJECTDATA), didod, &dwElements, 0) == DIERR_INPUTLOST)
	{
		HRESULT hr = g_pKeyboard->Acquire();
		if (hr == DIERR_OTHERAPPHASPRIO)
		{
			DXERR_printf("Other application has priority when attempting to acquire keyboard\n");
			return -1;
		}

		DI_ERROR_LOG_AND_QUIT(hr);
		if (g_pKeyboard->GetDeviceData(sizeof(DIDEVICEOBJECTDATA), didod, &dwElements, 0) == DIERR_NOTACQUIRED)
		{
			return -1;
		}
	}

	for (i32 i = 0; i < 256; i++)
		gKeyState[i] &= ~0x80u;

	for (DWORD k = 0; k < dwElements; k++)
	{
		if (didod[k].dwData & 0x80)
		{
			gKeyState[didod[k].dwOfs] = -1;
		}
		else
		{
			gKeyState[didod[k].dwOfs] = 0x80;
		}
	}

	return dwElements;
#elif defined(SPIDEY_STANDALONE)
	u8 now[256];
	Plat_InputPollKeyboard(now);
	i32 changed = 0;
	for (i32 i = 0; i < 256; i++)
	{
		u8 next = edgeState(gKeyState[i], now[i] & 0x80);
		if ((next ^ gKeyState[i]) & 0x80)
			changed++;
		gKeyState[i] = next;
	}
	return changed;
#endif
	return 0;
}

// @Ok
// @Matching
i32 DXINPUT_PollMouse(i32 *pX, i32 *pY)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	DWORD dwElements = 16;
	DIDEVICEOBJECTDATA didod[16];
	memset(didod, 0, sizeof(didod));

	if (!g_pMouse)
	{
		return 0;
	}

	if (g_pMouse->GetDeviceData(sizeof(DIDEVICEOBJECTDATA), didod, &dwElements, 0) == DIERR_INPUTLOST)
	{
		HRESULT hr = g_pMouse->Acquire();
		if (hr == DIERR_OTHERAPPHASPRIO)
		{
			DXERR_printf("Other application has priority when attempting to acquire mouse\n");
			return 0;
		}

		DI_ERROR_LOG_AND_QUIT(hr);
		if (g_pMouse->GetDeviceData(sizeof(DIDEVICEOBJECTDATA), didod, &dwElements, 0) == DIERR_NOTACQUIRED)
		{
			return 0;
		}
	}

	for (i32 i = 0; i < 3; i++)
		gMouseButtonState[i] &= ~0x80u;

	*pY = 0;
	*pX = 0;

	if (dwElements == 0)
	{
		return 0;
	}

	for (DWORD k = 0; k < dwElements; k++)
	{
		if (didod[k].dwOfs >= DIMOFS_BUTTON0 && didod[k].dwOfs < DIMOFS_BUTTON3)
		{
			if (didod[k].dwData & 0x80)
			{
				gMouseButtonState[didod[k].dwOfs - DIMOFS_BUTTON0] = -1;
			}
			else
			{
				gMouseButtonState[didod[k].dwOfs - DIMOFS_BUTTON0] = 0x80;
			}
		}
		else switch (didod[k].dwOfs)
		{
			case DIMOFS_X:
				*pX += didod[k].dwData;
				break;
			case DIMOFS_Y:
				*pY += didod[k].dwData;
				break;
		}
	}

	return 1;
#elif defined(SPIDEY_STANDALONE)
	u8 buttons[3];
	Plat_InputPollMouse(pX, pY, buttons);
	for (i32 i = 0; i < 3; i++)
		gMouseButtonState[i] = edgeState(gMouseButtonState[i], buttons[i] & 0x80);
	return 1;
#else
	return 0;
#endif
}

// @Ok
// @Matching
void DXINPUT_Release(void)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (g_pKeyboard)
	{
		g_pKeyboard->Unacquire();
		g_pKeyboard->Release();
		g_pKeyboard = 0;
	}

	if (g_pMouse)
	{
		g_pMouse->Unacquire();
		g_pMouse->Release();
		g_pMouse = 0;
	}

	if (gControllerRelated)
	{
		gControllerRelated->Unacquire();
		gControllerRelated->Release();
		gControllerRelated = 0;
	}

	if (gForceFeedbackRelated)
	{
		DXINPUT_StopForceFeedbackEffect();
		gForceFeedbackRelated->Release();
		gForceFeedbackRelated = 0;
	}

	g_pDI = 0;
	memset(gKeyState, 0, sizeof(gKeyState));
	memset(gMouseButtonState, 0, sizeof(gMouseButtonState));
	memset(gControllerButtonState, 0, sizeof(gControllerButtonState));

	gDxInputRelated = 0;
	gNumControllerButtons = 0;
#elif defined(SPIDEY_STANDALONE)
	Plat_InputRumble(0, 0.0f);
	memset(gKeyState, 0, sizeof(gKeyState));
	memset(gMouseButtonState, 0, sizeof(gMouseButtonState));
	memset(gControllerButtonState, 0, sizeof(gControllerButtonState));
	gDxInputRelated = 0;
	gNumControllerButtons = 0;
#endif
}

// @Ok
// @Matching
void DXINPUT_SetKeyState(u8 key, u8 state)
{
	gKeyState[key] = state;
}

// @Ok
// @Matching
void DXINPUT_SetMouseButtonState(u8 button, u8 state)
{
	gMouseButtonState[button] = state;
}

// @Ok
// @Matching
i32 DXINPUT_SetupController(void)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	g_pDI->EnumDevices(DI8DEVCLASS_GAMECTRL, EnumControllersCallback, 0, DIEDFL_ATTACHEDONLY);

	if (!gControllerRelated)
	{
		return 0;
	}

	HRESULT hr = gControllerRelated->SetCooperativeLevel(gDxInputHwnd, DISCL_FOREGROUND | DISCL_EXCLUSIVE);
	DI_ERROR_LOG_AND_QUIT(hr);

	hr = gControllerRelated->SetDataFormat(&c_dfDIJoystick);
	DI_ERROR_LOG_AND_QUIT(hr);

	DIPROPDWORD dipdw;
	dipdw.diph.dwSize = sizeof(DIPROPDWORD);
	dipdw.diph.dwHeaderSize = sizeof(DIPROPHEADER);
	dipdw.diph.dwObj = 0;
	dipdw.diph.dwHow = DIPH_DEVICE;
	dipdw.dwData = 16;

	hr = gControllerRelated->SetProperty(DIPROP_BUFFERSIZE, &dipdw.diph);
	DI_ERROR_LOG_AND_QUIT(hr);

	dipdw.dwData = DIPROPAXISMODE_ABS;
	hr = gControllerRelated->SetProperty(DIPROP_AXISMODE, &dipdw.diph);
	DI_ERROR_LOG_AND_QUIT(hr);

	DIPROPRANGE diprg;
	diprg.diph.dwSize = sizeof(DIPROPRANGE);
	diprg.diph.dwHeaderSize = sizeof(DIPROPHEADER);
	diprg.diph.dwHow = DIPH_BYOFFSET;
	diprg.diph.dwObj = DIJOFS_X;
	diprg.lMin = -1000;
	diprg.lMax = 1000;

	hr = gControllerRelated->SetProperty(DIPROP_RANGE, &diprg.diph);
	DI_ERROR_LOG_AND_QUIT(hr);

	diprg.diph.dwSize = sizeof(DIPROPRANGE);
	diprg.diph.dwHeaderSize = sizeof(DIPROPHEADER);
	diprg.diph.dwHow = DIPH_BYOFFSET;
	diprg.diph.dwObj = DIJOFS_Y;
	diprg.lMin = -1000;
	diprg.lMax = 1000;

	hr = gControllerRelated->SetProperty(DIPROP_RANGE, &diprg.diph);
	DI_ERROR_LOG_AND_QUIT(hr);

	hr = gControllerRelated->Acquire();
	if (hr == DIERR_OTHERAPPHASPRIO)
	{
		DXERR_printf("Other application has priority when attempting to acquire controller\n");
	}
	else
	{
		DI_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	i32 x, y;
	u32 pov;
	u8 buttons[32];
	if (!Plat_InputPollController(&x, &y, &pov, buttons, &gNumControllerButtons))
		return 0;
	gDxInputRelated = 1;   // rumble goes through the platform layer
#endif

	return 1;
}

// @Ok
// @Matching
i32 DXINPUT_SetupForceFeedbackSineEffect(i32 magnitude, f32 period)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (!gDxInputRelated || !gControllerRelated)
	{
		return 0;
	}

	DWORD rgdwAxes[2];
	LONG rglDirection[2];
	DIPERIODIC periodic;
	DIEFFECT eff;

	memset(&eff, 0, sizeof(eff));
	memset(&periodic, 0, sizeof(periodic));

	rgdwAxes[0] = DIJOFS_X;
	rgdwAxes[1] = DIJOFS_Y;
	rglDirection[0] = 0;
	rglDirection[1] = 0;

	periodic.dwMagnitude = magnitude;
	periodic.dwPeriod = (DWORD)(period * 1000000.0f);

	eff.dwSize = sizeof(DIEFFECT);
	eff.dwFlags = DIEFF_OBJECTOFFSETS | DIEFF_POLAR;
	eff.dwDuration = INFINITE;
	eff.dwGain = DI_FFNOMINALMAX;
	eff.dwTriggerButton = DIEB_NOTRIGGER;
	eff.cAxes = 2;
	eff.rgdwAxes = rgdwAxes;
	eff.rglDirection = rglDirection;
	eff.cbTypeSpecificParams = sizeof(DIPERIODIC);
	eff.lpvTypeSpecificParams = &periodic;

	if (gForceFeedbackRelated)
	{
		GUID guid;
		HRESULT hr = gForceFeedbackRelated->GetEffectGuid(&guid);
		DI_ERROR_LOG_AND_QUIT(hr);

		if (guid == GUID_Sine)
		{
			hr = gForceFeedbackRelated->SetParameters(&eff, DIEP_TYPESPECIFICPARAMS);
			DI_ERROR_LOG_AND_QUIT(hr);
		}
		else
		{
			gForceFeedbackRelated->Release();
			gForceFeedbackRelated = 0;
		}
	}

	if (!gForceFeedbackRelated)
	{
		HRESULT hr = gControllerRelated->CreateEffect(GUID_Sine, &eff, &gForceFeedbackRelated, 0);
		DI_ERROR_LOG_AND_QUIT(hr);

		if (!gForceFeedbackRelated)
		{
			return 0;
		}
	}

	if (FAILED(gForceFeedbackRelated->Download()))
	{
		DXERR_printf("Could not download force-feedback effect into controller!\n");
	}

	return 1;
#elif defined(SPIDEY_STANDALONE)
	(void)period;
	gStandaloneRumbleStrength = (f32)magnitude / 10000.0f;
	return gDxInputRelated;
#else
	return 0;
#endif
}

// @Ok
i32 DXINPUT_SetupKeyboard(i32 exclusive, i32 buffered)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	HRESULT hr = g_pDI->CreateDevice(GUID_SysKeyboard, &g_pKeyboard, NULL);
	DI_ERROR_LOG_AND_QUIT(hr);

	hr = g_pKeyboard->SetCooperativeLevel(gDxInputHwnd,
			exclusive ? 
				DISCL_NOWINKEY | DISCL_FOREGROUND | DISCL_EXCLUSIVE :
				DISCL_FOREGROUND | DISCL_NONEXCLUSIVE);
	DI_ERROR_LOG_AND_QUIT(hr);

	hr = g_pKeyboard->SetDataFormat(&c_dfDIKeyboard);
	DI_ERROR_LOG_AND_QUIT(hr);

	if (buffered)
	{
		DIPROPDWORD v12;
		v12.diph.dwHeaderSize = 16;
		v12.dwData = 16;
		v12.diph.dwSize = 20;
		v12.diph.dwObj = 0;
		v12.diph.dwHow = 0;

		hr = g_pKeyboard->SetProperty(DIPROP_BUFFERSIZE, &v12.diph);
		DI_ERROR_LOG_AND_QUIT(hr);
	}

	hr = g_pKeyboard->Acquire();
	if (hr == DIERR_OTHERAPPHASPRIO)
	{
		DXERR_printf("Other application has priority when attempting to acquire keyboard\n");
	}
	else
	{
		DI_ERROR_LOG_AND_QUIT(hr);
	}
#endif
	
	return 1;
}

// @Ok
i32 DXINPUT_SetupMouse(i32 exclusive)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	HRESULT hr = g_pDI->CreateDevice(GUID_SysMouse, &g_pMouse, NULL);
	DI_ERROR_LOG_AND_QUIT(hr);

	DIPROPDWORD v12;
	v12.diph.dwHeaderSize = 16;
	v12.dwData = 16;
	v12.diph.dwSize = 20;
	v12.diph.dwObj = 0;
	v12.diph.dwHow = 0;

	hr = g_pMouse->SetProperty(DIPROP_BUFFERSIZE, &v12.diph);
	DI_ERROR_LOG_AND_QUIT(hr);

	hr = g_pMouse->SetDataFormat(&c_dfDIMouse);
	DI_ERROR_LOG_AND_QUIT(hr);

	hr = g_pMouse->SetCooperativeLevel(gDxInputHwnd,
			exclusive ? 
				DISCL_FOREGROUND | DISCL_EXCLUSIVE :
				DISCL_FOREGROUND | DISCL_NONEXCLUSIVE);
	DI_ERROR_LOG_AND_QUIT(hr);

	hr = g_pMouse->Acquire();
	if (hr == DIERR_OTHERAPPHASPRIO)
	{
		DXERR_printf("Other application has priority when attempting to acquire mouse\n");
	}
	else
	{
		DI_ERROR_LOG_AND_QUIT(hr);
	}
#endif
	
	return 1;
}

// @Ok
// @Matching
i32 DXINPUT_StartForceFeedbackEffect(void)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gDxInputRelated && gControllerRelated && gForceFeedbackRelated)
	{
		gForceFeedbackRelated->Start(1, DIES_NODOWNLOAD);
		return 1;
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxInputRelated)
	{
		Plat_InputRumble(1, gStandaloneRumbleStrength);
		return 1;
	}
#endif

	return 0;
}

// @Ok
// @Matching
i32 DXINPUT_StopForceFeedbackEffect(void)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gDxInputRelated && gControllerRelated && gForceFeedbackRelated)
	{
		gForceFeedbackRelated->Stop();
		return 1;
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxInputRelated)
	{
		Plat_InputRumble(0, 0.0f);
		return 1;
	}
#endif

	return 0;
}


// Tentative globals for the low graphics scanline setup below. Names and
// guessed roles are ours, not confirmed against his IDB (idb_globals.txt has
// nothing at these addresses except gLowGraphicsRelated, already known).
static void* gLowGraphicsSurface;             // 0x2E096D4, cached lpSurface
static i32 gLowGraphicsPixelCount;            // 0x2E096DC, width * height
static u8* const gLowGraphicsPaletteDirty = (u8*)0x2E096E1; // set 1 here, cleared by gsub_514ED0
static i32* const gLowGraphicsColor16 = (i32*)0x2E096D0;    // packed 16 bit color, read first thing in gsub_514ED0
static i32 gLowGraphicsWidth;                 // 0x568F90, cached every call
static i32 gLowGraphicsHeight;                // 0x568F98, cached, used to detect a size change
static i32 gLowGraphicsPitch;                 // 0x568F94, cached every call, read back as a row stride in gsub_514ED0
static f32 gLowGraphicsFadeColor;             // 0x2E04568
static i32 gLowGraphicsColorRelated;          // 0x282854C
static f32 gLowGraphicsHalfWidth;             // 0xADC4EC
static f32 gLowGraphicsHalfHeight;            // 0xADC4F0
// Two small descriptor structs the fog/backdrop code reads back elsewhere
// (not in this file). Field layout is a guess from the store pattern only.
static void* gLowGraphicsFadeDescPtrA;        // 0xADC4E0, set to a fixed address
static void* gLowGraphicsFadeDescPtrB;        // 0xADC4E4, set to a fixed address
static i32 gLowGraphicsFadeDescUnused14;      // 0xADC4F4
static i32 gLowGraphicsViewWidth;             // 0x2828548
static i32 gLowGraphicsViewUnused8;           // 0x2828550
static i32 gLowGraphicsViewHeight;            // 0x2828554

// @Ok
// Functional: low-graphics scanline table setup, logic verified against
// Hex-Rays at 0x514DB0. Reallocates gLowGraphicsRelated (16 bytes per
// scanline) only when the height changes; caches pitch for gsub_514ED0.
// Fixed a condition bug found while verifying: the guard was
// (width>=0 && height<0), original is (width<0 || height<0) (a dead-code
// check, width/height are always >= 0 in practice).
EXPORT void gsub_514DB0(
		LPVOID lpSurface,
		i32 width,
		i32 height,
		LONG pitch,
		u32 color16,
		f32 fadeColor,
		i32 colorRelated,
		f32 halfWidth,
		f32 halfHeight)
{
	gLowGraphicsFadeColor = fadeColor;
	gLowGraphicsColorRelated = colorRelated;
	gLowGraphicsHalfHeight = halfHeight;
	gLowGraphicsWidth = width;
	gLowGraphicsSurface = lpSurface;
	gLowGraphicsHalfWidth = halfWidth;
	*gLowGraphicsPaletteDirty = 1;
	gLowGraphicsPitch = pitch;

	if (height != gLowGraphicsHeight)
	{
		void* oldBuf = gLowGraphicsRelated;
		gLowGraphicsHeight = height;

		if (oldBuf)
			free(oldBuf);

		gLowGraphicsRelated = malloc(gLowGraphicsHeight * 0x10);
		memset(gLowGraphicsRelated, 0, gLowGraphicsHeight * 0x10);
	}

	gLowGraphicsFadeDescPtrA = (void*)0x2828558;
	gLowGraphicsFadeDescPtrB = (void*)0xADC4F8;
	gLowGraphicsFadeDescUnused14 = 0;
	gLowGraphicsViewWidth = gLowGraphicsWidth;
	gLowGraphicsViewUnused8 = 0;
	gLowGraphicsViewHeight = gLowGraphicsHeight;

	if (gLowGraphicsWidth < 0 || gLowGraphicsHeight < 0)
	{
		gLowGraphicsFadeDescUnused14 = 0;
		gLowGraphicsViewWidth = 0;
		gLowGraphicsViewUnused8 = 0;
		gLowGraphicsViewHeight = 0;
	}

	gLowGraphicsPixelCount = gLowGraphicsWidth * gLowGraphicsHeight;
	*gLowGraphicsColor16 = color16;
}

// @Ok
void DXPOLY_BeginScene(void)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	print_if_false(gInBeginScene == 0, "nested BeginScene() calls!");
	memset(gSceneBuffer, 0, sizeof(gSceneBuffer));
	gInBeginScene = 1;

	if (gLowGraphics)
	{
		DDSURFACEDESC2 v13;
		memset(&v13, 0, sizeof(v13));
		v13.dwSize = sizeof(v13);

		HRESULT hr = g_pDDS_Scene->Lock(0, &v13, 2048, 0);
		D3D_ERROR_LOG_AND_QUIT(hr);

		i32 width, height;
		DXINIT_GetCurrentResolution(&width, &height);
		f32 v9 = (f32)height;
		f32 v8 = v9 * 0.5f;
		f32 v10 = (f32)width;
		f32 v7 = v10 * 0.5f;
		gsub_514DB0(
			v13.lpSurface,
			width,
			height,
			v13.lPitch,
			(gDxPolyBackgroundColor & 0xF8 | ((gDxPolyBackgroundColor & 0xFC00 | (gDxPolyBackgroundColor >> 3) & 0x1F0000) >> 2)) >> 3,
			flt_56817C,
			dword_568184,
			v7,
			v8);
	}
	else
	{
		HRESULT hr = G_D3DDEVICE7->BeginScene();
		D3D_ERROR_LOG_AND_QUIT(hr);

		if (gDxPolyRelated)
			hr = G_D3DDEVICE7->Clear(0, 0, 3, gDxPolyBackgroundColor, gFlDepthCompare, 0);
		else
			hr = G_D3DDEVICE7->Clear(0, 0, 1, gDxPolyBackgroundColor, gFlDepthCompare, 0);
		D3D_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	print_if_false(gInBeginScene == 0, "nested BeginScene() calls!");
	memset(gSceneBuffer, 0, sizeof(gSceneBuffer));
	gInBeginScene = 1;
	Plat_GfxBeginScene(gDxPolyBackgroundColor, gDxPolyRelated);
#endif
}

// base value for the depth bucket math below, tentative name/purpose guess.
static i32* const gDxPolyDepthBucketBase = (i32*)0x6BBAA8;

#ifdef SPIDEY_STANDALONE
static i32 gDumpFrames = -1;
static u32 gDumpAt;
static void dumpPoly(const DXPOLY* pPoly, i32 bucket)
{
	printf("POLY bucket=%d n=%lu tex=%p blend=%u flags=%x", bucket, (unsigned long)pPoly->field_C,
			pPoly->field_4, pPoly->mBlendMode, pPoly->field_A);
	for (u32 k = 0; k < pPoly->field_C && k < 4; k++)
		printf(" [%.1f,%.1f,%.4f rhw=%.4f c=%08x uv=%.3f,%.3f]",
				pPoly->field_10[k].field_0, pPoly->field_10[k].field_4, pPoly->field_10[k].field_8,
				pPoly->field_10[k].field_C, pPoly->field_10[k].field_10,
				pPoly->field_10[k].field_14, pPoly->field_10[k].field_18);
	printf("\n");
}
#endif

// @Ok
// Behaviour: with low graphics on and not on render pass 1, a near plane
// visibility test on the poly's first/second/last (and, for 4+ verts,
// third/fourth) vertex runs first and can discard the poly outright. Then:
// a forced slot (a2 >= 0) always goes straight into that gSceneBuffer slot;
// otherwise, if gDxPolyRelated is set and the poly has no blend mode, it
// draws right now instead of queueing; otherwise it goes into a depth-sorted
// bucket derived from depth and depthBias.
// Verified logic against Hex-Rays at 0x503100 this session, fixed two real
// bugs: cross2 (the third/fourth vertex cull term) had dx3/dy3 swapped
// against dx2/dy2, computing the negated cross product and inverting cull2
// for any nonzero case; and the immediate-draw path called
// DXPOLY_SetFilterMode, which the original does not do at all here (only
// SetTexture, SetBlendMode, address U/V and tex alpha are set before
// DrawPrimitive). 199 mnemonic diffs left before this pass, register
// scheduling only, see dxsound.attempts.md.
void DXPOLY_DrawPoly(
		DXPOLY* pPoly,
		i32 a2,
		i32 depthBias,
		f32 depth)
{
	if (!gInBeginScene)
	{
		DXERR_printf("drawing outside scene\r\n");
#ifdef SPIDEY_STANDALONE
#ifndef _WIN32
		// print the first caller once so the stray draw can be found
		static i32 shown = 0;
		if (!shown)
		{
			shown = 1;
			void* frames[16];
			i32 n = backtrace(frames, 16);
			printf("OUTSIDE backtrace:");
			for (i32 k = 0; k < n; k++)
				printf(" %p", frames[k]);
			printf("\n");
		}
#endif
#endif
	}

	if (gLowGraphics && dword_6B7A8C != 1)
	{
		f32 dx1 = pPoly->field_10[1].field_0 - pPoly->field_10[0].field_0;
		f32 dy1 = pPoly->field_10[1].field_4 - pPoly->field_10[0].field_4;
		i32 lastIdx = pPoly->field_C - 1;
		f32 dxN = pPoly->field_10[lastIdx].field_0 - pPoly->field_10[0].field_0;
		f32 dyN = pPoly->field_10[lastIdx].field_4 - pPoly->field_10[0].field_4;

		f32 dx2 = 0.0f, dy2 = 0.0f, dx3 = 0.0f, dy3 = 0.0f;
		if (lastIdx > 2)
		{
			dx2 = pPoly->field_10[1].field_0 - pPoly->field_10[2].field_0;
			dy2 = pPoly->field_10[1].field_4 - pPoly->field_10[2].field_4;
			dx3 = pPoly->field_10[3].field_0 - pPoly->field_10[2].field_0;
			dy3 = pPoly->field_10[3].field_4 - pPoly->field_10[2].field_4;
		}

		f32 cross1 = dyN * dx1 - dxN * dy1;
		u8 cull1 = (dword_6B7A8C == 3) ? (cross1 >= 0.0f) : (cross1 <= 0.0f);

		if (pPoly->field_C > 3)
		{
			f32 cross2 = dx3 * dy2 - dy3 * dx2;
			u8 cull2 = (dword_6B7A8C == 3) ? (cross2 >= 0.0f) : (cross2 <= 0.0f);

			if (cull1 != cull2)
				return;
		}
		else if (!cull1)
		{
			return;
		}
	}

	if (a2 >= 0)
	{
		print_if_false(a2 <= 4096, "Invalid forced slot number!");
		pPoly->pNext = gSceneBuffer[a2];
		gSceneBuffer[a2] = pPoly;
	}
	else if (gDxPolyRelated && pPoly->mBlendMode == 0)
	{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
		DXPOLY_SetTexture(pPoly->field_4);
		DXPOLY_SetBlendMode(pPoly->mBlendMode);

		DXPOLY_SetAddressUAndV(
				(pPoly->field_A & 2) ? 1 : 3,
				(pPoly->field_A & 4) ? 1 : 3);

		DXPOLY_EnableTexAlpha((pPoly->field_A & 8) != 0);

		G_D3DDEVICE7->DrawPrimitive(
				D3DPT_TRIANGLEFAN,
				324,
				&pPoly->field_10[0],
				pPoly->field_C,
				0);
#elif defined(SPIDEY_STANDALONE)
		if (gDumpFrames > 0 && Plat_Ticks() >= gDumpAt)
			dumpPoly(pPoly, -1);
		DXPOLY_SetTexture(pPoly->field_4);
		DXPOLY_SetBlendMode(pPoly->mBlendMode);
		DXPOLY_SetAddressUAndV(
				(pPoly->field_A & 2) ? 1 : 3,
				(pPoly->field_A & 4) ? 1 : 3);
		DXPOLY_EnableTexAlpha((pPoly->field_A & 8) != 0);
		gDbgFanBlend = pPoly->mBlendMode; gDbgFanTex = (i32)(size_t)pPoly->field_4; gDbgFanBucket = -1;
		Plat_GfxDrawFan(pPoly->field_10, pPoly->field_C);
#endif
	}
	else
	{
		i32 bucket = *gDxPolyDepthBucketBase - (i32)(depth * -4096.0f) + depthBias;
		if (bucket < 0)
			bucket = 0;
		else if (bucket > 0x1000)
			bucket = 0x1000;

		pPoly->pNext = gSceneBuffer[bucket];
		gSceneBuffer[bucket] = pPoly;
	}
}

// @Ok
void DXPOLY_EnableTexAlpha(bool a1)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (a1 != gTexAlpha)
	{
		gTexAlpha = a1;
		G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_ALPHAOP, a1 ? 4 : 3);
	}
#elif defined(SPIDEY_STANDALONE)
	if (a1 != gTexAlpha)
	{
		gTexAlpha = a1;
		Plat_GfxSetTexAlpha(a1);
	}
#endif
}

// Two callees this function needs that are outside our assigned range
// (0x513FF0, 0x511860). Forwarded to the original rather than guessed at,
// same pattern as ClearRegion in spool.cpp.
typedef i32 (*func_513FF0_t)(i32, i32, void*, i32);
static const func_513FF0_t gsub_513FF0 = (func_513FF0_t)0x00513FF0;
typedef void (*func_511860_t)(i32, i32);
static const func_511860_t gsub_511860 = (func_511860_t)0x00511860;

// One entry of the per scanline table gsub_514DB0 allocates into
// gLowGraphicsRelated (16 bytes/row). mReady gates whether the row
// participates in the copy below; mTexA/mTexB are two chains of unknown
// "texture" objects (offset 0x40 = next, 0x44 = a row-write callback taking
// (node, dest), 0x48 = a u16 word count used to size the write). Struct
// layout guessed from the disasm only, not confirmed against his IDB.
struct SLowGraphicsScanline
{
	u8 mReady;
	u8 pad[7];
	void* mTexA;
	void* mTexB;
};
struct SLowGraphicsTexNode
{
	u8 pad0[0x40];
	SLowGraphicsTexNode* pNext;
	void (*mWriteRow)(SLowGraphicsTexNode*, void*);
	u16 mRowWords;
};

// @Ok
// Low graphics frame flush: builds an 8 bit RGB fade color from the packed
// 16 bit gLowGraphicsColor16 (skips everything if it is negative), hands a
// small local table of it to gsub_513FF0/gsub_511860 (both out of scope, not
// attempted, real purpose unclear beyond "fog/fade related"), then walks
// gLowGraphicsRelated's per scanline texture node chains and MMX-copies each
// node's 0x40 byte row into the destination surface at gLowGraphicsSurface.
// Verified the scanline copy loop (the part that actually writes visible
// pixels) against Hex-Rays at 0x514ED0 this session and fixed two bugs: the
// copy size per row was rounded UP to the next 64 byte block, the original
// rounds DOWN (plain integer division, `2*width/64*64`, no +0x3F); and the
// mReady gate only tests bit 0 of the scanline flags byte (`& 1`), not the
// whole byte's truth value. The fade table section that feeds
// gsub_513FF0/gsub_511860 is still a best effort translation (its exact
// field layout is only confirmed for the color/fade values that matter to
// the copy loop, not the two forwarded callees themselves, which are out of
// this session's assigned range and were not decompiled). See
// dxsound.attempts.md.
void gsub_514ED0(void)
{
	i32 color = *gLowGraphicsColor16;

	if (color >= 0)
	{
		i32 color16 = color & 0xFFFF;

		i32 r8 = ((color16 >> 11) & 0x1F) * 255 / 31;
		i32 g8 = ((color16 >> 5) & 0x3F) * 255 / 63;
		i32 b8 = (color16 & 0x1F) * 255 / 31;

		f32 fadeShade = gLowGraphicsFadeColor / 128000.0f;

		// Best effort only, exact field usage of this local table is not
		// confirmed (see dxsound.attempts.md).
		i32 fadeTable[5][7] = {0};
		for (i32 i = 0; i < 5; i++)
		{
			fadeTable[i][0] = (i32)fadeShade;
			fadeTable[i][1] = r8;
			fadeTable[i][2] = g8;
		}

		if (gLowGraphicsPixelCount > 0)
		{
			for (i32 i = 3; i >= 0; i--)
				fadeTable[i][3] = b8;

			i32 result = gsub_513FF0(0, 0, fadeTable, 4);
			if (result >= 3)
			{
				stateLog("%s", (char*)0x563D88);
			}

			gsub_511860(0, 0);
		}
	}

	// This alignment nudge (round up to the next multiple of 8, unless
	// already aligned) matches the disasm but its purpose here is unclear.
	u8* alignedScratch = (u8*)0x2E086D0;
	if (((u32)alignedScratch & 7) != 0)
		alignedScratch = (u8*)(((u32)alignedScratch & ~7) + 8);

	SLowGraphicsScanline* scanlines = (SLowGraphicsScanline*)gLowGraphicsRelated;
	u8* dest = (u8*)gLowGraphicsSurface;

	for (i32 y = 0; y < gLowGraphicsHeight; y++)
	{
		u8* rowDest = dest;

		if (*gLowGraphicsColor16 >= 0 && (scanlines[y].mReady & 1))
			rowDest = alignedScratch;

		for (i32 which = 0; which < 2; which++)
		{
			SLowGraphicsTexNode* node = which == 0
					? (SLowGraphicsTexNode*)scanlines[y].mTexA
					: (SLowGraphicsTexNode*)scanlines[y].mTexB;

			while (node)
			{
				node->mWriteRow(node, rowDest + node->mRowWords * 2);
				node = node->pNext;
			}
		}

		if (*gLowGraphicsColor16 >= 0 && (scanlines[y].mReady & 1))
		{
			// MMX 64 byte block copy, alignedScratch -> dest, matches the
			// original's 8x movq loop. The original rounds the block count
			// DOWN (plain integer division), not up.
			memcpy(dest, alignedScratch, (gLowGraphicsWidth * 2 / 0x40) * 0x40);
		}

		dest += gLowGraphicsPitch;
	}

	*gLowGraphicsPaletteDirty = 0;
}

// @Ok
void DXPOLY_EndScene(bool a1)
{
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gInBeginScene)
	{
		gInBeginScene = 0;
		renderScene();
		if (gLowGraphics)
		{
			gsub_514ED0();
			g_pDDS_Scene->Unlock(0);
		}
		else
		{
			HRESULT hr = G_D3DDEVICE7->EndScene();
			D3D_ERROR_LOG_AND_QUIT(hr);
		}

		if (a1)
		{
			DXPOLY_Flip();
		}
	}
#elif defined(SPIDEY_STANDALONE)
	if (gInBeginScene)
	{
		gInBeginScene = 0;
		renderScene();
		Plat_GfxEndScene();
		if (a1)
			DXPOLY_Flip();
	}
#endif
}

// @Ok
// @Matching
void DXPOLY_Flip(void)
{
#ifdef SPIDEY_STANDALONE
	// SPIDEY_SHOTS="ms,ms,...": save scrnNNNN.bmp at those times (test aid)
	{
		static i32 parsed;
		static u32 shotAt[16];
		static i32 shotCount, shotNext;
		if (!parsed)
		{
			parsed = 1;
			const char* env = getenv("SPIDEY_SHOTS");
			while (env && *env && shotCount < 16)
			{
				shotAt[shotCount++] = (u32)strtoul(env, 0, 10);
				const char* c = strchr(env, ',');
				env = c ? c + 1 : 0;
			}
		}
		if (shotNext < shotCount && Plat_Ticks() >= shotAt[shotNext])
		{
			shotNext++;
			DXPOLY_SaveScreen();
		}
	}
	Plat_GfxFlip();
	// the Windows build pumps its message queue from several places the
	// standalone build does not have; once per presented frame is enough
	WinYield();
#endif
#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	if (gDxOptionRelated)
	{
		DDBLTFX v4;
		memset(&v4, 0, sizeof(v4));
		v4.dwSize = sizeof(v4);

		HRESULT hr = g_pDDS_SaveScreen->Blt(&gRect, g_pDDS_Scene, 0, 0x1000000, &v4);
		D3D_ERROR_LOG_AND_QUIT(hr);
	}
	else
	{
		HRESULT hr = g_pDDS_SaveScreen->Flip(0, 1);
		D3D_ERROR_LOG_AND_QUIT(hr);
	}
#endif
}

EXPORT u8 byte_6B7A80 = 0;

EXPORT f32 gFogStart;
EXPORT f32 gFogEnd;
EXPORT u32 gFogColor;
EXPORT DWORD gAddressU;
EXPORT DWORD gAddressV;

u32 dword_568F98;
u32 dword_568F94;
u32 dword_568F90;
u32 dword_2E096D4;

// @Ok
EXPORT void gsub_515270(void)
{
	dword_568F98 = 0;
	dword_568F94 = 0;
	dword_568F90 = 0;
	dword_2E096D4 = 0;
	gLowGraphicsRelated = 0;
}

// @Ok
// Verified field by field against Hex-Rays at 0x502220 this session: every
// constant, every SetRenderState/SetTextureStageState argument and value,
// and the gMagFilters/gMinFilters[gCurrentFilterIndex] pair at the end all
// match. gDxPolyRelated is (a1>>1)&1 stored once and reused (not (a1&2)!=0
// recomputed per use), and it is the same global 0x6BBAA5 DXPOLY_DrawPoly's
// immediate-draw check reads (fixed there too, was a separate invented
// gDxPolyImmediateDraw pointer). Most of the SetRenderState/
// SetTextureStageState arguments below are raw D3DRENDERSTATETYPE/
// D3DTEXTURESTAGESTATETYPE numbers, not enum names, since Hex-Rays only
// resolves them as integers too. 396 mnemonic diffs, whole-function
// register allocation/scheduling only (the very first instructions already
// diverge), no logic difference found.
void DXPOLY_Init(u32 a1)
{
	if ( gLowGraphics )
		gsub_515270();

	gDxPolyRelated = (a1 >> 1) & 1;
	gDepthCompareIndex = 4;
	byte_6B7A80 = 0;
	gDepthBuffering = gDxPolyRelated;
	gDepthWriting = gDxPolyRelated;
	gTexAlpha = false;
	gCurrentFilterIndex = 1;
	gFogStart = 0.1f;
	gFogEnd = 0.99000001f;
	gFogColor = 0xFFFFFF;
	dword_6B7A8C = 3;
	gAddressU = 3;
	gAddressV = 3;
	gCurrentBlendMode = 0;
	G_DD_SURFACE7 = 0;

#if defined(_WIN32) && !defined(SPIDEY_STANDALONE)
	G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ANTIALIAS, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)4, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)7, gDxPolyRelated != 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)8, 3);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)9, 2);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)10, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)14, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)15, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)16, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)19, 2);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)20, 1);
	if ( gLowGraphics )
		G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)22, 1);
	else
		G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)22, 3);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)23, 4);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)24, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)25, 8);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)26, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)27, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)28, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)29, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)33, 0);
	G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_FOGCOLOR, gFogColor);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)35, 0);
	G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_FOGSTART, gFogStart);
	G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_FOGEND, gFogEnd);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)38, 1065353216);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)40, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)41, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)47, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)48, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)52, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)53, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)54, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)55, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)56, 8);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)57, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)58, -1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)59, -1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)60, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)128, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)129, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)130, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)131, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)132, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)133, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)134, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)135, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)136, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)137, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)138, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)139, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)140, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)141, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)142, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)143, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)144, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)145, 1);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)146, 2);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)147, 2);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)148, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)151, 0);
	G_D3DDEVICE7->SetRenderState((D3DRENDERSTATETYPE)152, 0);

	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)1, 4);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)2, 2);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)3, 0);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)4, 4);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)5, 2);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)6, 0);
	G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_ADDRESSU, gAddressU);
	G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_ADDRESSV, gAddressV);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)16, gMagFilters[gCurrentFilterIndex]);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)17, gMinFilters[gCurrentFilterIndex]);
	G_D3DDEVICE7->SetTextureStageState(0, (D3DTEXTURESTAGESTATETYPE)18, 1);
#elif defined(SPIDEY_STANDALONE)
	// the same defaults as the SetRenderState block above, in platform terms
	Plat_GfxSetDepthTest(gDxPolyRelated);     // 7  ZENABLE
	Plat_GfxSetDepthWrite(1);                 // 14 ZWRITEENABLE
	Plat_GfxSetDepthFunc(4);                  // 23 ZFUNC = LESSEQUAL
	Plat_GfxSetBlendMode(0);                  // 19/20/27 ONE, ZERO, no blend
	Plat_GfxSetFog(0, gFogColor, gFogStart, gFogEnd);
	Plat_GfxSetAddress(gAddressU, gAddressV);
	Plat_GfxSetFilter(gCurrentFilterIndex);
	Plat_GfxSetTexAlpha(1);                   // ALPHAOP = MODULATE
	Plat_GfxSetTexture(0);
#endif
}

// @Ok
// @Matching
void DXPOLY_SaveScreen(void)
{
#ifdef SPIDEY_STANDALONE
	// 24 bit BMP straight from the platform layer's frame buffer
	{
		char name[32];
		sprintf(name, "scrn%4.4i.bmp", ++gScreenshotNumber);
		i32 width, height;
		DXINIT_GetCurrentResolution(&width, &height);
		i32 rowBytes = (width * 3 + 3) & ~3;
		u8* pixels = static_cast<u8*>(malloc(rowBytes * height));
		memset(pixels, 0, rowBytes * height);
		if (Plat_GfxReadPixels(pixels, width, height))
		{
			FILE* f = fopen(name, "wb");
			if (f)
			{
				u32 fileSize = 54 + rowBytes * height;
				u8 hdr[54];
				memset(hdr, 0, sizeof(hdr));
				hdr[0] = 'B'; hdr[1] = 'M';
				memcpy(hdr + 2, &fileSize, 4);
				u32 off = 54; memcpy(hdr + 10, &off, 4);
				u32 dib = 40; memcpy(hdr + 14, &dib, 4);
				memcpy(hdr + 18, &width, 4);
				i32 negHeight = -height;   // top down rows
				memcpy(hdr + 22, &negHeight, 4);
				u16 planes = 1, bpp = 24;
				memcpy(hdr + 26, &planes, 2);
				memcpy(hdr + 28, &bpp, 2);
				u32 imgSize = rowBytes * height; memcpy(hdr + 34, &imgSize, 4);
				fwrite(hdr, 1, 54, f);
				// Plat_GfxReadPixels packs rows at width*3, the BMP wants
				// rows padded to 4 bytes
				for (i32 y = 0; y < height; y++)
				{
					fwrite(pixels + y * width * 3, 1, width * 3, f);
					if (rowBytes != width * 3)
						fwrite(hdr + 50, 1, rowBytes - width * 3, f);   // zero pad
				}
				fclose(f);
				printf("Saved %s\n", name);
			}
		}
		free(pixels);
	}
	return;
#endif
#ifdef _WIN32
	char v7[32];
	sprintf(v7, "scrn%4.4i.bmp", ++gScreenshotNumber);

	DDSURFACEDESC2 v6;
	memset(&v6, 0, sizeof(v6));
	v6.dwSize = sizeof(v6);

	if (gDxOptionRelated)
	{
		u32 width, height;
		DXINIT_GetCurrentResolution(&width, &height);

		HRESULT hr = g_pDDS_SaveScreen->Lock(&gRect, &v6, 16, 0);
		D3D_ERROR_LOG_AND_QUIT(hr);

		DXPOLY_SaveSurfaceAsBMP(
				v7,
				v6.lpSurface,
				width,
				height,
				v6.lPitch,
				&v6.ddpfPixelFormat,
				false);
		g_pDDS_SaveScreen->Unlock(&gRect);
	}
	else
	{
		HRESULT hr = g_pDDS_SaveScreen->Lock(0, &v6, 16, 0);
		D3D_ERROR_LOG_AND_QUIT(hr);

		DXPOLY_SaveSurfaceAsBMP(
				v7,
				v6.lpSurface,
				v6.dwWidth,
				v6.dwHeight,
				v6.lPitch,
				&v6.ddpfPixelFormat,
				false);
		g_pDDS_SaveScreen->Unlock(0);
	}

	DXERR_printf("Saved Screenshot %i.\r\n", gScreenshotNumber);
#endif
}

// @Ok
// Writes a bottom-up 24 bit BGR BMP of a surface to disk. Understands 3
// 16 bit source pixel formats (555 with a top 1 bit alpha, 565, 4444) plus a
// 32 bit 0x00RRGGBB path. `flag` picks an alternate per pixel source read:
// for the 32 bit path it takes the top byte only, replicated to R/G/B; for
// the two 16 bit formats that carry an alpha mask (555+alpha, 4444) it does
// the same thing with the alpha bits instead of R/G/B. 565 has no alpha
// mask, so the original bails out early (returns without writing anything)
// if flag is set for a 565 source, instead of reading garbage alpha fields.
// Verified logic against Hex-Rays at 0x503560 this session: the disasm's
// early "if (flag) return" branch belongs to 565, not to a distinct "555
// without alpha" format like a previous draft had it (there is no such
// format branch in the original at all: a 555 source with alpha mask 0 does
// not match 555+alpha, 565 or 4444, and the real disasm just falls through
// reading a stale, never-written stack slot for the blue bit count, i.e.
// genuine undefined behaviour on an input DirectDraw does not actually
// hand out in practice; we print the same "unknown format" error instead
// of reproducing that garbage read). Any other format prints an error and
// returns without writing, same as the true 32 bit mismatch path.
void DXPOLY_SaveSurfaceAsBMP(
		char* filename,
		void* pData,
		i32 width,
		i32 height,
		i32 pitch,
		_DDPIXELFORMAT* pf,
		bool flag)
{
#ifdef _WIN32
	u8 use32BitSrc = 0;
	i32 rShift = 0, gShift = 0, bShift = 0, aShift = 0;
	i32 rBits = 0, gBits = 0, bBits = 0, aBits = 0;
	u32 rMask = 0, gMask = 0, bMask = 0, aMask = 0;

	if (pf->dwRGBBitCount == 16)
	{
		u32 r = pf->dwRBitMask;
		u32 g = pf->dwGBitMask;
		u32 b = pf->dwBBitMask;
		u32 a = pf->dwRGBAlphaBitMask;

		if (r == 0x7C00 && g == 0x3E0 && b == 0x1F && a == 0x8000)
		{
			rMask = r; gMask = g; bMask = b; aMask = a;
			rShift = 10; gShift = 5; bShift = 0; aShift = 15;
			rBits = 5; gBits = 5; bBits = 5; aBits = 1;
		}
		else if (r == 0xF800 && g == 0x7E0 && b == 0x1F && a == 0)
		{
			if (flag)
				return;

			rMask = r; gMask = g; bMask = b;
			rShift = 11; gShift = 5; bShift = 0;
			rBits = 5; gBits = 6; bBits = 5;
		}
		else if (r == 0xF00 && g == 0xF0 && b == 0xF && a == 0xF000)
		{
			rMask = r; gMask = g; bMask = b; aMask = a;
			rShift = 8; gShift = 4; bShift = 0; aShift = 12;
			rBits = 4; gBits = 4; bBits = 4; aBits = 4;
		}
		else
		{
			print_if_false(0, "SaveTex(): Unknown format = [%8.8X, %8.8X, %8.8X, %8.8X]\r\n", r, g, b, a);
			return;
		}
	}
	else if (pf->dwRGBBitCount == 0x20 &&
			pf->dwRBitMask == 0xFF0000 && pf->dwGBitMask == 0xFF00 && pf->dwBBitMask == 0xFF)
	{
		use32BitSrc = 1;
	}
	else
	{
		print_if_false(0, "SaveTex(): Unknown format = [%8.8X, %8.8X, %8.8X, %8.8X]\r\n",
				pf->dwRBitMask, pf->dwGBitMask, pf->dwBBitMask, pf->dwRGBAlphaBitMask);
		return;
	}

	i32 rowBytes = ((width * 3 + 3) / 4) * 4;
	i32 imageSize = rowBytes * height;

	FILE* f = fopen(filename, "wb");
	if (!f)
	{
		print_if_false(0, "SaveSurfaceAsBMP(): Problems creating: %s...\r\n", filename);
		return;
	}

	BITMAPFILEHEADER bmfh;
	memset(&bmfh, 0, sizeof(bmfh));
	bmfh.bfType = 0x4D42;
	bmfh.bfSize = imageSize + 0x36;
	bmfh.bfOffBits = 0x36;
	fwrite(&bmfh, 0xE, 1, f);

	BITMAPINFOHEADER bmih;
	memset(&bmih, 0, sizeof(bmih));
	bmih.biSize = 0x28;
	bmih.biWidth = width;
	bmih.biHeight = height;
	bmih.biPlanes = 1;
	bmih.biBitCount = 0x18;
	fwrite(&bmih, 0x28, 1, f);

	u8* row = (u8*)malloc(rowBytes);
	u8* srcRow = (u8*)pData + (height - 1) * pitch;

	for (i32 y = height; y > 0; y--)
	{
		u8* dst = row;
		u16* src16 = (u16*)srcRow;
		u32* src32 = (u32*)srcRow;

		for (i32 x = width; x > 0; x--)
		{
			u32 r, g, b;

			if (use32BitSrc)
			{
				u32 px = *src32++;
				if (flag)
				{
					b = (px >> 24) & 0xFF;
					g = b;
					r = b;
				}
				else
				{
					r = (px >> 16) & 0xFF;
					g = (px >> 8) & 0xFF;
					b = px & 0xFF;
				}
			}
			else
			{
				u32 px = *src16++;
				if (flag)
				{
					r = g = b = ((px & aMask) >> aShift) << (8 - aBits);
				}
				else
				{
					r = ((px & rMask) >> rShift) << (8 - rBits);
					g = ((px & gMask) >> gShift) << (8 - gBits);
					b = ((px & bMask) >> bShift) << (8 - bBits);
				}
			}

			*dst++ = (u8)b;
			*dst++ = (u8)g;
			*dst++ = (u8)r;
		}

		fwrite(row, rowBytes, 1, f);
		srcRow -= pitch;
	}

	fclose(f);
	free(row);
#endif
}

// @Ok
// @Matching
void DXPOLY_SetBackgroundColor(u32 color)
{
	gDxPolyBackgroundColor = color;
}

// @Ok
void DXPOLY_SetBlendMode(u32 a1)
{

#ifdef _WIN32
	u32 newBlendMode = a1;
	if (gCurrentBlendMode != a1)
	{
		switch(a1)
		{
			default:
				DXERR_printf("ERROR: Invalid blend mode passed to DXPOLY_SetBlendMode(): %lu\r\n", a1);
				newBlendMode = 0;
			case 0:
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_SRCBLEND, 2);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_DESTBLEND, 1);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ALPHABLENDENABLE, 0);
				DXPOLY_SetDepthWriting(1);
				break;
			case 3:
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_SRCBLEND, 1);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_DESTBLEND, 4);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ALPHABLENDENABLE, 1);
				DXPOLY_SetDepthWriting(0);
				break;
			case 1:
			case 5:
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_SRCBLEND, 5);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_DESTBLEND, 6);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ALPHABLENDENABLE, 1);
				DXPOLY_SetDepthWriting(0);
				break;
			case 2:
			case 4:
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_SRCBLEND, 5);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_DESTBLEND, 2);
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ALPHABLENDENABLE, 1);
				DXPOLY_SetDepthWriting(0);
				break;

		}

		gCurrentBlendMode = newBlendMode;
	}
#elif defined(SPIDEY_STANDALONE)
	u32 newBlendMode = a1;
	if (gCurrentBlendMode != a1)
	{
		if (a1 > 5)
		{
			DXERR_printf("ERROR: Invalid blend mode passed to DXPOLY_SetBlendMode(): %lu\r\n", a1);
			newBlendMode = 0;
		}
		Plat_GfxSetBlendMode(newBlendMode);
		DXPOLY_SetDepthWriting(newBlendMode == 0);
		gCurrentBlendMode = newBlendMode;
	}
#endif
}

// @Ok
// @Matching
// The ZENABLE flag here is gDepthBuffering (0x6B7A98, the IDB's gDepthBUffering),
// not gDepthWriting (0x6B7A89, ZWRITEENABLE, owned by DXPOLY_SetDepthWriting).
// With the two merged, enabling the depth test marked writes as enabled too and
// the next DXPOLY_SetDepthWriting(1) was skipped: opaque world polys were drawn
// with the depth mask off and the far backdrop painted over them (found with
// the SDL3 build, 2026-09-03).
void DXPOLY_SetDepthCompare(u32 a1)
{
#ifdef _WIN32
	if (gDxPolyRelated)
	{
		if (!a1)
		{
			if (gDepthBuffering)
			{
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZENABLE, 0);
				gDepthBuffering = 0;
				DXERR_printf("Depth Buffering Disabled.\r\n");
			}

			return;
		}

		if (!gDepthBuffering)
		{
			G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZENABLE, 1);
			gDepthBuffering = 1;
			DXERR_printf("Depth Buffering Enabled.\r\n");
		}

		if (a1 != gDepthCompareIndex)
		{
			G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZFUNC, a1);
			char* status = gD3DDepthCompareNames[a1];
			gDepthCompareIndex = a1;

			DXERR_printf("Depth Compare = %s\r\n", status);
			if ( a1 == 5 || (gFlDepthCompare = 1.0f, a1 == 7) )
				gFlDepthCompare = 0.0f;
		}
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxPolyRelated)
	{
		if (!a1)
		{
			if (gDepthBuffering)
			{
				Plat_GfxSetDepthTest(0);
				gDepthBuffering = 0;
			}
			return;
		}

		if (!gDepthBuffering)
		{
			Plat_GfxSetDepthTest(1);
			gDepthBuffering = 1;
		}

		if (a1 != gDepthCompareIndex)
		{
			Plat_GfxSetDepthFunc(a1);
			gDepthCompareIndex = a1;
			if ( a1 == 5 || (gFlDepthCompare = 1.0f, a1 == 7) )
				gFlDepthCompare = 0.0f;
		}
	}
#endif
}

// @Ok
// @Matching
void DXPOLY_SetDepthWriting(bool a1)
{
#ifdef _WIN32
	if (gDxPolyRelated && a1 != gDepthWriting)
	{
		G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZWRITEENABLE, a1);
		gDepthWriting = a1;

		char *status = "Enabled";
		if (!a1)
			status = "Disabled";

		DXERR_printf("Depth Buffer Writes %s.\r\n", status);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxPolyRelated && a1 != gDepthWriting)
	{
		Plat_GfxSetDepthWrite(a1);
		gDepthWriting = a1;
	}
#endif
}

// @Ok
// @Matching
void DXPOLY_SetFilterMode(u32 filterIndex)
{
#ifdef _WIN32
	if (filterIndex != gCurrentFilterIndex)
	{
		G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_MAGFILTER, gMagFilters[filterIndex]);
		G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_MINFILTER, gMinFilters[filterIndex]);

		gCurrentFilterIndex = filterIndex;

		char *status = "PointSample";
		if (filterIndex)
			status = "Bilinear";
		DXERR_printf("Filter %s.\r\n", status);
	}
#elif defined(SPIDEY_STANDALONE)
	if (filterIndex != gCurrentFilterIndex)
	{
		Plat_GfxSetFilter(filterIndex);
		gCurrentFilterIndex = filterIndex;
	}
#endif
}

// @Ok
// @Matching
void DXPOLY_SetHUDOffset(i32 a1)
{
	gHudOffset = a1;
	f32 v1 = (f32)(4096 - a1);
	gFlHudOffset = v1 / 4096.0f;
}

// @Ok
// @Matching
void DXPOLY_SetOutlineColor(u32 a1)
{
	gDxOutlineColor = a1;
}

// @Ok
// @Matching
void DXPOLY_SetTexture(LPDIRECTDRAWSURFACE7 a1)
{
#ifdef _WIN32
	if (a1 != G_DD_SURFACE7)
	{
		HRESULT hr = G_D3DDEVICE7->SetTexture(0, a1);
		D3D_ERROR_LOG_AND_QUIT(hr);
		G_DD_SURFACE7 = a1;
	}
#elif defined(SPIDEY_STANDALONE)
	if (a1 != G_DD_SURFACE7)
	{
		Plat_GfxSetTexture(reinterpret_cast<PlatTexture*>(a1));
		G_DD_SURFACE7 = a1;
	}
#endif
}

// @Ok
// @Matching
void DXSOUND_Close(i32 a1)
{
#ifdef _WIN32
	if (gDxSoundHolder[a1].pDSB)
	{
		HRESULT hr = gDxSoundHolder[a1].pDSB->Release();

		DS_ERROR_LOG_AND_QUIT(hr);

		gDxSoundHolder[a1].pDSB = 0;
		gDxSoundHolder[a1].mFrequency = 0;
		gDxSoundHolder[a1].field_8 = 0;
		gDxSoundHolder[a1].field_9 = 0;
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[a1].pDSB)
	{
		Plat_SndDestroyVoice(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[a1].pDSB));
		gDxSoundHolder[a1].pDSB = 0;
		gDxSoundHolder[a1].mFrequency = 0;
		gDxSoundHolder[a1].field_8 = 0;
		gDxSoundHolder[a1].field_9 = 0;
	}
#endif
}

// @Ok
// No standalone PC address (fully inlined into DXSOUND_Load in the
// original, which has its own inline copy of the same logic, verified
// against Hex-Rays at 0x503B40 this session, see the DXSOUND_Load comment
// and dxsound.attempts.md). Not separately runnable or verifiable, kept as
// a real, checked translation of the same steps.
void DXSOUND_CreateDSBuffer(char *fileName, i32 index)
{
#ifdef _WIN32
	LPVOID ptr1 = 0;
	DWORD len1 = 0;
	LPVOID ptr2 = 0;
	DWORD len2 = 0;
	WAVEFORMATEX wfx;
	long size;
	DSBUFFERDESC dsbd;

	u8* pData = loadWAV(fileName, &wfx, &size);
	if (!pData)
	{
		stateLog("\t\tERROR Loading WAV file %s!!!\r\n", fileName);
		return;
	}

	memset(&dsbd, 0, sizeof(dsbd));
	dsbd.dwSize = sizeof(dsbd);
	dsbd.dwFlags = DSBCAPS_STATIC | DSBCAPS_CTRLFREQUENCY | DSBCAPS_CTRLPAN | DSBCAPS_CTRLVOLUME;
	dsbd.dwBufferBytes = size;
	dsbd.lpwfxFormat = &wfx;

	HRESULT hr = g_pDS->CreateSoundBuffer(&dsbd, &gDxSoundBuffers[index], 0);
	DS_ERROR_LOG_AND_QUIT(hr);

	hr = gDxSoundBuffers[index]->Lock(0, size, &ptr1, &len1, &ptr2, &len2, DSBLOCK_ENTIREBUFFER);
	if (hr == DSERR_BUFFERLOST)
	{
		hr = gDxSoundBuffers[index]->Restore();
		DS_ERROR_LOG_AND_QUIT(hr);
		hr = gDxSoundBuffers[index]->Lock(0, size, &ptr1, &len1, &ptr2, &len2, DSBLOCK_ENTIREBUFFER);
	}
	DS_ERROR_LOG_AND_QUIT(hr);

	memcpy(ptr1, pData, len1);
	if (ptr2)
		memcpy(ptr2, pData + len1, len2);

	hr = gDxSoundBuffers[index]->Unlock(ptr1, len1, ptr2, len2);
	DS_ERROR_LOG_AND_QUIT(hr);

	free(pData);
#endif
}

// @Ok
// @Matching
void DXSOUND_Init(void)
{
#ifdef _WIN32
	DSBUFFERDESC v6;

	memset(gDxSoundBuffers, 0, sizeof(gDxSoundBuffers));
	memset(gDxSoundHolder, 0, sizeof(gDxSoundHolder));
	memset(&v6, 0, sizeof(v6));

	v6.dwBufferBytes = 0;
	v6.dwSize = sizeof(v6);
	v6.dwFlags = DSBCAPS_CTRLVOLUME | DSBCAPS_PRIMARYBUFFER;

	HRESULT hr = g_pDS->CreateSoundBuffer(&v6, &g_pDSBuffer, 0);
	DS_ERROR_LOG_AND_QUIT(hr);

	hr = g_pDSBuffer->SetVolume(0);
	DS_ERROR_LOG_AND_QUIT(hr);

	hr = g_pDSBuffer->Play(0, 0, 1);
	DS_ERROR_LOG_AND_QUIT(hr);
#elif defined(SPIDEY_STANDALONE)
	memset(gDxSoundBuffers, 0, sizeof(gDxSoundBuffers));
	memset(gDxSoundHolder, 0, sizeof(gDxSoundHolder));
#endif
}

// @Ok
// @Matching
i32 DXSOUND_IsPlaying(i32 a1)
{
	DWORD v5 = 0;

#ifdef _WIN32

	LPDIRECTSOUNDBUFFER pDSB = gDxSoundHolder[a1].pDSB;
	if (!pDSB)
		return 0;

	if (gDxSoundHolder[a1].field_8)
		return 1;

	HRESULT hr = pDSB->GetStatus(&v5);

	DS_ERROR_LOG_AND_QUIT(hr);
#elif defined(SPIDEY_STANDALONE)
	if (!gDxSoundHolder[a1].pDSB)
		return 0;
	if (gDxSoundHolder[a1].field_8)
		return 1;
	if (Plat_SndIsPlaying(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[a1].pDSB)))
		v5 = 4;   // DSBSTATUS_PLAYING
#endif

	return v5 & 4;
}

// Table of 64 sound names per audio group, only DXSOUND_Load reads it.
// Group 0 starts with "missing", "s_burn". Owner TU is the audio group code.
// Macro, not a static pointer variable: a stored pointer forces a memory load
// at every use, the original computes the address as a constant offset.
#define gAudioGroupSoundNames ((char**)0x0055AD64)

// @Ok
// loadWAV and DXSOUND_CreateDSBuffer are both fully inlined here on PC (neither
// has its own PC address), whole body written inline instead of calling out
// to them. Verified step by step against Hex-Rays at 0x503B40 this session:
// the group index/dsIndex math, the gdFsOpen/GetFileSize/Read/Close calls
// and their control flow (Close runs on both the read-failed and
// read-succeeded paths), the mmio chunk walk and its "hmmio == 0" early out
// (only frees the file buffer, no mmioClose, matches our unconditional
// free(fileBuf) after the if block), the WAVEFORMATEX 18 byte read and the
// WAVE_FORMAT_PCM check, and the DSBUFFERDESC flags (DSBCAPS_STATIC |
// CTRLFREQUENCY | CTRLPAN | CTRLVOLUME = 0xE2, matches the disasm's literal
// 226) all match. The error prints (nullsub_1 at "could not load"/"error
// loading") and our stateLog calls are both empty 1 byte functions in this
// release build, so which one we call has no behavioural effect. 238
// mnemonic diffs left against 0x503B40, all register scheduling
// (specifically which register the compiler dedicates as the whole
// function's persistent zero, see dxsound.attempts.md), 15+ hypotheses
// tried across two sessions without moving it.
void DXSOUND_Load(char *groupName)
{
#ifdef _WIN32
	if (!g_pDS)
		return;

	i32 group = AUDIOGROUPS_GetGroup(groupName);
	if (group == -1)
	{
		stateLog("\tCould not loads sound for %s\r\n", groupName);
		return;
	}

	i32 index = group != 1 ? 0x40 : 0;
	char** pName = &gAudioGroupSoundNames[group * 64];

	for (i32 i = 0; i < 64; i++)
	{
		char fileName[0x100];

		if (!*pName)
			return;

		strcpy(fileName, *pName);
		if (strlen(fileName))
		{
			strcat(fileName, ".wav");

			// DXSOUND_CreateDSBuffer(fileName, index++) inlined
			i32 dsIndex = index++;

			// loadWAV(fileName, &wfx, &size) inlined
			char path[0x100];
			u8* pData = 0;
			MMIOINFO mmioinfo;
			MMCKINFO ckRiff;
			MMCKINFO ckIn;
			MMCKINFO ckData;
			i32 fileSize = 0;
			WAVEFORMATEX wfx;
			long size = 0;
			LPVOID ptr1 = 0;
			DWORD len2 = 0;

			strcpy(path, "AUDIO\\");
			strcat(path, fileName);

			HANDLE h = gdFsOpen(path, 0);
			if (h)
			{
				gdFsGetFileSize((i32)h, &fileSize);
				u8* fileBuf = (u8*)malloc(fileSize);
				if (gdFsRead((i32)h, fileSize / 0x800, fileBuf))
				{
					free(fileBuf);
					gdFsClose(h);
				}
				else
				{
					gdFsClose(h);

					memset(&mmioinfo, 0, sizeof(mmioinfo));
					mmioinfo.fccIOProc = FOURCC_MEM;
					mmioinfo.pchBuffer = (HPSTR)fileBuf;
					mmioinfo.cchBuffer = fileSize;
					mmioinfo.adwInfo[0] = 0;

					HMMIO hmmio = mmioOpen(fileName, &mmioinfo, MMIO_READ);
					ckRiff.fccType = mmioStringToFOURCC("WAVE", 0);
					if (hmmio)
					{
						if (mmioDescend(hmmio, &ckRiff, 0, MMIO_FINDRIFF) == 0)
						{
							ckIn.ckid = mmioStringToFOURCC("fmt ", 0);
							mmioDescend(hmmio, &ckIn, &ckRiff, MMIO_FINDCHUNK);
							mmioRead(hmmio, (HPSTR)&wfx, sizeof(WAVEFORMATEX));
							mmioAscend(hmmio, &ckIn, 0);

							if (wfx.wFormatTag == WAVE_FORMAT_PCM)
							{
								ckData.ckid = mmioStringToFOURCC("data", 0);
								mmioDescend(hmmio, &ckData, &ckRiff, MMIO_FINDCHUNK);

								if (ckData.cksize)
								{
									size = (ckData.cksize + 3) & ~3;
									pData = (u8*)malloc(size);
									memset(pData, 0, size);
									if (pData)
										mmioRead(hmmio, (HPSTR)pData, ckData.cksize);
								}
							}
						}

						mmioClose(hmmio, 0);
					}

					free(fileBuf);
				}
			}

			if (!pData)
			{
				stateLog("\t\tERROR Loading WAV file %s!!!\r\n", fileName);
			}
			else
			{
				DSBUFFERDESC dsbd;
				memset(&dsbd, 0, sizeof(dsbd));
				dsbd.dwSize = sizeof(dsbd);
				dsbd.dwFlags = DSBCAPS_STATIC | DSBCAPS_CTRLFREQUENCY | DSBCAPS_CTRLPAN | DSBCAPS_CTRLVOLUME;
				dsbd.dwBufferBytes = size;
				dsbd.lpwfxFormat = &wfx;

				DWORD len1 = 0;
				LPVOID ptr2 = 0;

				HRESULT hr = g_pDS->CreateSoundBuffer(&dsbd, &gDxSoundBuffers[dsIndex], 0);
				DS_ERROR_LOG_AND_QUIT(hr);

				hr = gDxSoundBuffers[dsIndex]->Lock(0, size, &ptr1, &len1, &ptr2, &len2, DSBLOCK_ENTIREBUFFER);
				if (hr == DSERR_BUFFERLOST)
				{
					hr = gDxSoundBuffers[dsIndex]->Restore();
					DS_ERROR_LOG_AND_QUIT(hr);
					hr = gDxSoundBuffers[dsIndex]->Lock(0, size, &ptr1, &len1, &ptr2, &len2, DSBLOCK_ENTIREBUFFER);
				}
				DS_ERROR_LOG_AND_QUIT(hr);

				memcpy(ptr1, pData, len1);
				if (ptr2)
					memcpy(ptr2, pData + len1, len2);

				hr = gDxSoundBuffers[dsIndex]->Unlock(ptr1, len1, ptr2, len2);
				DS_ERROR_LOG_AND_QUIT(hr);

				free(pData);
			}
		}

		pName++;
	}
#elif defined(SPIDEY_STANDALONE)
	// Same walk as above with a plain RIFF parser instead of mmio.
	if (!g_pDS)
		return;

	i32 group = AUDIOGROUPS_GetGroup(groupName);
	if (group == -1)
		return;

	i32 index = group != 1 ? 0x40 : 0;
	char** pName = &gAudioGroupSoundNames[group * 64];

	for (i32 i = 0; i < 64; i++, pName++)
	{
		if (!*pName)
			return;
		if (!strlen(*pName))
			continue;

		char path[0x100];
		strcpy(path, "AUDIO\\");
		strcat(path, *pName);
		strcat(path, ".wav");

		i32 dsIndex = index++;

		HANDLE h = gdFsOpen(path, 0);
		if (!h)
		{
			DXERR_printf("\t\tERROR Loading WAV file %s!!!\r\n", path);
			continue;
		}

		i32 fileSize = 0;
		gdFsGetFileSize((i32)h, &fileSize);
		u8* fileBuf = (u8*)malloc(fileSize + 0x800);
		gdFsRead((i32)h, (fileSize + 0x7FF) / 0x800, fileBuf);
		gdFsClose(h);

		// RIFF/WAVE: "fmt " then "data"
		i32 rate = 0, channels = 0, bits = 0, fmtTag = 0;
		const u8* pcm = 0;
		i32 pcmBytes = 0;
		if (fileSize >= 12 && !memcmp(fileBuf, "RIFF", 4) && !memcmp(fileBuf + 8, "WAVE", 4))
		{
			i32 pos = 12;
			while (pos + 8 <= fileSize)
			{
				u32 ckSize;
				memcpy(&ckSize, fileBuf + pos + 4, 4);
				const u8* ck = fileBuf + pos + 8;
				if (!memcmp(fileBuf + pos, "fmt ", 4) && ckSize >= 16)
				{
					u16 v16;
					memcpy(&v16, ck, 2); fmtTag = v16;
					memcpy(&v16, ck + 2, 2); channels = v16;
					memcpy(&rate, ck + 4, 4);
					memcpy(&v16, ck + 14, 2); bits = v16;
				}
				else if (!memcmp(fileBuf + pos, "data", 4))
				{
					pcm = ck;
					pcmBytes = (i32)ckSize;
					if (pos + 8 + pcmBytes > fileSize)
						pcmBytes = fileSize - pos - 8;
					break;
				}
				pos += 8 + (i32)((ckSize + 1) & ~1u);
			}
		}

		if (pcm && pcmBytes > 0 && fmtTag == 1)
			gDxSoundBuffers[dsIndex] = reinterpret_cast<IDirectSoundBuffer*>(
					Plat_SndCreateBuffer(pcm, pcmBytes, rate, channels, bits));
		else
			DXERR_printf("\t\tERROR Loading WAV file %s!!!\r\n", path);

		free(fileBuf);
	}
#endif
}

// @Ok
// @Matching
void DXSOUND_Open(
		i32 a1,
		i32 a2,
		i32 a3)
{
#ifdef _WIN32
	if (g_pDS)
	{
		if (a3)
			a2 += 0x40;

		IDirectSoundBuffer *v3 = gDxSoundBuffers[a2];

		if (v3)
		{
			HRESULT hr = g_pDS->DuplicateSoundBuffer(v3, &gDxSoundHolder[a1].pDSB);
			DS_ERROR_LOG_AND_QUIT(hr);

			DWORD freq;
			hr = gDxSoundHolder[a1].pDSB->GetFrequency(&freq);
			DS_ERROR_LOG_AND_QUIT(hr);

			gDxSoundHolder[a1].mFrequency = freq;
		}
	}
#elif defined(SPIDEY_STANDALONE)
	if (g_pDS)
	{
		if (a3)
			a2 += 0x40;

		PlatSoundBuffer* pBuf = reinterpret_cast<PlatSoundBuffer*>(gDxSoundBuffers[a2]);
		if (pBuf)
		{
			PlatSoundVoice* pVoice = Plat_SndCreateVoice(pBuf);
			gDxSoundHolder[a1].pDSB = pVoice;
			gDxSoundHolder[a1].mFrequency = Plat_SndGetFrequency(pVoice);
		}
	}
#endif
}

// @Ok
// @Matching
void DXSOUND_Play(i32 a1, i32 a2)
{
#ifdef _WIN32
	if (gDxSoundHolder[a1].pDSB)
	{
		gDxSoundHolder[a1].field_8 = 0;
		gDxSoundHolder[a1].field_9 = a2 != 0;

		HRESULT hr = gDxSoundHolder[a1].pDSB->Play(0, 0, a2 != 0);
		DS_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[a1].pDSB)
	{
		gDxSoundHolder[a1].field_8 = 0;
		gDxSoundHolder[a1].field_9 = a2 != 0;
		Plat_SndPlay(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[a1].pDSB), a2 != 0);
	}
#endif
}

// @Ok
// @Matching
void DXSOUND_SetPan(i32 a1,i32 a2)
{
#ifdef _WIN32
	LPDIRECTSOUNDBUFFER pDSB = gDxSoundHolder[a1].pDSB;
	if (pDSB)
	{
		HRESULT hr = pDSB->SetPan(645 * a2 - 10000);
		DS_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[a1].pDSB)
		Plat_SndSetPan(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[a1].pDSB), 645 * a2 - 10000);
#endif
}

// @Ok
// @Matching
void DXSOUND_SetPitch(i32 a1, i32 a2)
{
#ifdef _WIN32
	LPDIRECTSOUNDBUFFER pDSB = gDxSoundHolder[a1].pDSB;
	if (pDSB)
	{
		i32 res = (i32)(static_cast<f32>(a2) / 1200.f * static_cast<f32>(gDxSoundHolder[a1].mFrequency));
		HRESULT hr = pDSB->SetFrequency(res);
		DS_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[a1].pDSB)
	{
		i32 res = (i32)(static_cast<f32>(a2) / 1200.f * static_cast<f32>(gDxSoundHolder[a1].mFrequency));
		Plat_SndSetFrequency(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[a1].pDSB), res);
	}
#endif
}

// @Ok
// @Matching
void DXSOUND_SetVolume(i32 a1,i32 a2)
{
#ifdef _WIN32
	LPDIRECTSOUNDBUFFER pDSB = gDxSoundHolder[a1].pDSB;
	if (pDSB)
	{
		HRESULT hr = pDSB->SetVolume(39 * (a2 - 255));
		DS_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[a1].pDSB)
		Plat_SndSetVolume(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[a1].pDSB), 39 * (a2 - 255));
#endif
}

// @Ok
// @Matching
void DXSOUND_ShutDown(void)
{
#ifdef _WIN32
	HRESULT hr = g_pDSBuffer->Stop();
	DS_ERROR_LOG_AND_QUIT(hr);

	DXSOUND_Unload(0, 1);
#elif defined(SPIDEY_STANDALONE)
	for (i32 i = 0; i < 0x20; i++)
		DXSOUND_Close(i);
	DXSOUND_Unload(0, 1);
#endif
}

// @Ok
// @Matching
void DXSOUND_Stop(i32 a1)
{
#ifdef _WIN32
	LPDIRECTSOUNDBUFFER pDSB = gDxSoundHolder[a1].pDSB;
	if (pDSB)
	{
		HRESULT hr = pDSB->Stop();
		DS_ERROR_LOG_AND_QUIT(hr);
	}
#elif defined(SPIDEY_STANDALONE)
	if (gDxSoundHolder[a1].pDSB)
		Plat_SndStop(reinterpret_cast<PlatSoundVoice*>(gDxSoundHolder[a1].pDSB));
#endif
}


// @Ok
// @Matching
void DXSOUND_Unload(char *a1, i32 a2)
{
#ifdef _WIN32
	if (a2)
	{
		for (i32 i = 0; i < 0x80; i++)
		{
			IDirectSoundBuffer *pBuf = gDxSoundBuffers[i];
			if (pBuf)
			{
				HRESULT hr = pBuf->Release();
				DS_ERROR_LOG_AND_QUIT(hr);
				gDxSoundBuffers[i] = 0;
			}
		}
	}
	else
	{
		i32 startIndex = AUDIOGROUPS_GetGroup(a1) != 1 ? 0x40 : 0;

		for (i32 j = 0; j < 64; j++)
		{
			IDirectSoundBuffer *pBuf = gDxSoundBuffers[startIndex];
			if (pBuf)
			{
				HRESULT hr = pBuf->Release();
				DS_ERROR_LOG_AND_QUIT(hr);
				gDxSoundBuffers[startIndex] = 0;
			}

			startIndex++;
		}
	}
#elif defined(SPIDEY_STANDALONE)
	i32 startIndex = 0, count = 0x80;
	if (!a2)
	{
		startIndex = AUDIOGROUPS_GetGroup(a1) != 1 ? 0x40 : 0;
		count = 64;
	}
	for (i32 i = 0; i < count; i++, startIndex++)
	{
		if (gDxSoundBuffers[startIndex])
		{
			Plat_SndDestroyBuffer(reinterpret_cast<PlatSoundBuffer*>(gDxSoundBuffers[startIndex]));
			gDxSoundBuffers[startIndex] = 0;
		}
	}
#endif
}

// @Ok
// @Matching
BOOL CALLBACK EnumControllersCallback(
		const DIDEVICEINSTANCEA *pDev,
		void *)
{
#ifdef _WIN32
	if (FAILED(g_pDI->CreateDevice(pDev->guidInstance, &gControllerRelated, 0)))
	{
		return DIENUM_CONTINUE;
	}

	DIDEVCAPS v5;
	memset(&v5, 0, sizeof(v5));
	v5.dwSize = sizeof(v5);

	HRESULT hr = gControllerRelated->GetCapabilities(&v5);
	DI_ERROR_LOG_AND_QUIT(hr);

	gDxInputRelated = !!(v5.dwFlags & DIDC_FORCEFEEDBACK);
	gNumControllerButtons = v5.dwButtons;

	return DIENUM_STOP;
#else
	return 0;
#endif
}

// @Ok
// No PC address at all, proven dead code: scanned every CALL in the whole
// .text section against the mmioOpenA/mmioDescend/mmioRead/mmioClose/
// mmioAscend import thunks, the only call sites anywhere in the binary are
// inside DXSOUND_Load's own inlined copy of this same parsing logic (see
// loadWAV above and dxsound.attempts.md). The Mac source has a standalone
// ParseWavHeader (spiderman_names.txt 0x166040), the PC port folded it
// (and DXSOUND_CreateDSBuffer) straight into DXSOUND_Load instead. Same
// chunk-walk verified against Hex-Rays for DXSOUND_Load/loadWAV this
// session, adapted here to its pointer-to-pointer output style. Since
// nothing calls it, it cannot be exercised or byte-verified on its own.
void ParseWavHeader(char *fileName, tWAVEFORMATEX **ppwfx, long *pSize, u8 **ppData)
{
#ifdef _WIN32
	char path[0x100];
	MMIOINFO mmioinfo;
	MMCKINFO ckRiff;
	MMCKINFO ckIn;
	MMCKINFO ckData;
	i32 fileSize;

	*ppwfx = 0;
	*ppData = 0;

	strcpy(path, "AUDIO\\");
	strcat(path, fileName);

	HANDLE h = gdFsOpen(path, 0);
	if (!h)
		return;

	gdFsGetFileSize((i32)h, &fileSize);
	u8* fileBuf = (u8*)malloc(fileSize);
	if (gdFsRead((i32)h, fileSize / 0x800, fileBuf))
	{
		free(fileBuf);
		gdFsClose(h);
		return;
	}
	gdFsClose(h);

	memset(&mmioinfo, 0, sizeof(mmioinfo));
	mmioinfo.fccIOProc = FOURCC_MEM;
	mmioinfo.pchBuffer = (HPSTR)fileBuf;
	mmioinfo.cchBuffer = fileSize;
	mmioinfo.adwInfo[0] = 0;

	HMMIO hmmio = mmioOpen(fileName, &mmioinfo, MMIO_READ);
	ckRiff.fccType = mmioStringToFOURCC("WAVE", 0);
	if (!hmmio)
	{
		free(fileBuf);
		return;
	}

	if (mmioDescend(hmmio, &ckRiff, 0, MMIO_FINDRIFF) == 0)
	{
		tWAVEFORMATEX* pwfx = (tWAVEFORMATEX*)malloc(sizeof(WAVEFORMATEX));

		ckIn.ckid = mmioStringToFOURCC("fmt ", 0);
		mmioDescend(hmmio, &ckIn, &ckRiff, MMIO_FINDCHUNK);
		mmioRead(hmmio, (HPSTR)pwfx, sizeof(WAVEFORMATEX));
		mmioAscend(hmmio, &ckIn, 0);

		if (pwfx->wFormatTag == WAVE_FORMAT_PCM)
		{
			ckData.ckid = mmioStringToFOURCC("data", 0);
			mmioDescend(hmmio, &ckData, &ckRiff, MMIO_FINDCHUNK);

			if (ckData.cksize)
			{
				*pSize = (ckData.cksize + 3) & ~3;
				u8* pData = (u8*)malloc(*pSize);
				memset(pData, 0, *pSize);
				if (pData)
					mmioRead(hmmio, (HPSTR)pData, ckData.cksize);
				*ppData = pData;
			}
		}

		*ppwfx = pwfx;
	}

	mmioClose(hmmio, 0);
	free(fileBuf);
#endif
}

// @Bogus
// SOLVED in the 2026-09-01 audit: this is not a separate function in the PC
// build, its body is inlined into DXPOLY_Init and is already translated
// there (DXPOLY_Init above, @Ok). Evidence: on Mac the two sit next to each
// other (idbs/spiderman_names.txt 00164630 DXINPUT_GetNumControllerButtons,
// 00164670 initialSettings, 00164990 renderScene, 00164b10 DXPOLY_Init at
// only 80 bytes, i.e. a wrapper that calls it); on PC there is no gap at all
// between DXINPUT_GetNumControllerButtons (0x502210) and DXPOLY_Init
// (0x502220), and PC's DXPOLY_Init is 1424 bytes holding the entire render
// state block (the ~60 SetRenderState / SetTextureStageState calls plus the
// 16 global stores) with no call out to a helper. idalib lookup_funcs
// "initialSettings" returns Not found; the name has zero hits in
// idbs/spideypc_names.txt.
// Not reconstructed as its own function (which is what was done for
// renderScene, the sibling in the same Mac file that is also PC-inlined,
// see @Ok renderScene below) because the PC binary gives no way to tell
// which of DXPOLY_Init's stores belonged to initialSettings and which to
// DXPOLY_Init itself, and Mac sizes cannot settle it either (the Mac
// renderer is not D3D7, so its 748 bytes are a different implementation).
// Splitting @Ok code on that guess would be worse than leaving this stub.
// Retagged @NotOk -> @Bogus: there is no PC code left to decompile here.
//
// Earlier notes from the sessions that scoped it, kept for the record:
// no PC address, no caller anywhere in this file, and unlike
// loadWAV/ParseWavHeader there is no sibling function to tie its logic to
// either (checked every CALL in the 0x500000-0x520000 range against
// tools/names.json, nothing unnamed calls in from outside that range points
// here). Re-check this session: decompiled DXSOUND_Init (0x5039f0, the
// natural place an "initial settings" helper would be called from) directly
// with Hex-Rays - it zeroes two DirectSound buffer-position arrays,
// allocates the primary sound buffer and sets its format, with no call to
// anything matching this function anywhere in its body. Also ran func_query
// over the entire DXsound.cpp address range (0x4fbdc0 through past
// 0x50f6d0): every byte is already claimed by a function IDA has a size for
// (named or sub_XXXXXX), so there is no unnamed gap hiding this code under a
// different name. The Mac source has it at spiderman_names.txt 0x164670, so
// it is real on that platform. Those earlier checks were right that there is
// no separate PC function; what they missed is where the body went, which the
// DXPOLY_Init size comparison above now answers.
void initialSettings(void)
{
    printf("initialSettings(void)");
}

// @Ok
// No standalone PC address (fully inlined into DXSOUND_Load in the original,
// alongside DXSOUND_CreateDSBuffer). DXSOUND_Load has its own inline copy,
// verified against Hex-Rays at 0x503B40 this session (see its comment).
// Reads AUDIO\<fileName> through the PKR file system and parses it with
// mmio from memory. Returns the sample data, *pSize is the size rounded up
// to 4. Not separately runnable or byte-verifiable on its own.
u8* loadWAV(char *fileName, tWAVEFORMATEX *pwfx, long *pSize)
{
#ifdef _WIN32
	char path[0x100];
	u8* pData = 0;
	MMIOINFO mmioinfo;
	MMCKINFO ckRiff;
	MMCKINFO ckIn;
	MMCKINFO ckData;
	i32 fileSize;

	strcpy(path, "AUDIO\\");
	strcat(path, fileName);

	HANDLE h = gdFsOpen(path, 0);
	if (!h)
		return 0;

	gdFsGetFileSize((i32)h, &fileSize);
	u8* fileBuf = (u8*)malloc(fileSize);
	if (gdFsRead((i32)h, fileSize / 0x800, fileBuf))
	{
		free(fileBuf);
		gdFsClose(h);
		return 0;
	}
	gdFsClose(h);

	memset(&mmioinfo, 0, sizeof(mmioinfo));
	mmioinfo.fccIOProc = FOURCC_MEM;
	mmioinfo.pchBuffer = (HPSTR)fileBuf;
	mmioinfo.cchBuffer = fileSize;
	mmioinfo.adwInfo[0] = 0;

	HMMIO hmmio = mmioOpen(fileName, &mmioinfo, MMIO_READ);
	ckRiff.fccType = mmioStringToFOURCC("WAVE", 0);
	if (!hmmio)
	{
		free(fileBuf);
		return 0;
	}

	if (mmioDescend(hmmio, &ckRiff, 0, MMIO_FINDRIFF) == 0)
	{
		ckIn.ckid = mmioStringToFOURCC("fmt ", 0);
		mmioDescend(hmmio, &ckIn, &ckRiff, MMIO_FINDCHUNK);
		mmioRead(hmmio, (HPSTR)pwfx, sizeof(WAVEFORMATEX));
		mmioAscend(hmmio, &ckIn, 0);

		if (pwfx->wFormatTag == WAVE_FORMAT_PCM)
		{
			ckData.ckid = mmioStringToFOURCC("data", 0);
			mmioDescend(hmmio, &ckData, &ckRiff, MMIO_FINDCHUNK);

			if (ckData.cksize)
			{
				*pSize = (ckData.cksize + 3) & ~3;
				pData = (u8*)malloc(*pSize);
				memset(pData, 0, *pSize);
				if (pData)
					mmioRead(hmmio, (HPSTR)pData, ckData.cksize);
			}
		}
	}

	mmioClose(hmmio, 0);
	free(fileBuf);
	return pData;
#else
	return 0;
#endif
}

// @Ok
void DXPOLY_SetAddressUAndV(DWORD addressU, DWORD addressV)
{
#ifdef _WIN32
	if (addressU != gAddressU)
	{
		G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_ADDRESSU, addressU);
		gAddressU = addressU;
	}

	if (addressV != gAddressV)
	{
		G_D3DDEVICE7->SetTextureStageState(0, D3DTSS_ADDRESSV, addressV);
		gAddressV = addressV;
	}
#elif defined(SPIDEY_STANDALONE)
	if (addressU != gAddressU || addressV != gAddressV)
	{
		Plat_GfxSetAddress(addressU, addressV);
		gAddressU = addressU;
		gAddressV = addressV;
	}
#endif
}

// @Ok
// No standalone PC address (fully inlined into DXPOLY_EndScene, same as
// loadWAV into DXSOUND_Load), not verifiable on its own. Decompiled
// DXPOLY_EndScene itself (0x502A40) this session to check this: the real
// non low graphics render pass walks gSceneBuffer BACKWARDS, from index
// 4096 down to 0 inclusive (a previous session had flipped this to count
// up, that was wrong: the forward 0-to-4096 walk belongs to the LOW
// GRAPHICS branch instead, a different algorithm entirely that calls two
// unnamed helpers (0x514B10, 0x514C60, chosen by a bit in pPoly->field_8)
// and a third (0x514DA0) per polygon, none of which are in this session's
// assigned range, so that branch is still a stub). The per polygon draw
// sequence in the real (non low graphics) branch does call SetFilterMode
// (unlike DXPOLY_DrawPoly's immediate-draw path, which does not), so this
// part of the translation was already right.
void renderScene(void)
{
#ifdef _WIN32
	if (gLowGraphics)
	{
		DXERR_printf("DO ME PLEASE renderScene");
	}
	else
	{
		for (
				i32 i = 4096;
				i >= 0;
				i--)
		{

			DXPOLY* pPoly = gSceneBuffer[i];
			if (gDxPolyRelated && gHudOffset > 0 && i == gHudOffset)
				G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZENABLE, 0);

			while (pPoly)
			{
				DXPOLY_SetTexture(pPoly->field_4);
				DXPOLY_SetBlendMode(pPoly->mBlendMode);

				DXPOLY_SetAddressUAndV(
						(pPoly->field_A & 2) ? 1 : 3,
						(pPoly->field_A & 4) ? 1 : 3);

				DXPOLY_EnableTexAlpha((pPoly->field_A & 8) != 0);
				DXPOLY_SetFilterMode((pPoly->field_A & 0x10) == 0);

				G_D3DDEVICE7->DrawPrimitive(
						D3DPT_TRIANGLEFAN,
						324,
						&pPoly->field_10[0],
						pPoly->field_C,
						0);

				pPoly = pPoly->pNext;
			}
		}

		if (gDxPolyRelated && gHudOffset > 0)
			G_D3DDEVICE7->SetRenderState(D3DRENDERSTATE_ZENABLE, 1);
	}
#elif defined(SPIDEY_STANDALONE)
	// SPIDEY_DUMPPOLYS=N (+ SPIDEY_DUMPPOLYS_AT=ms): print every fan of N
	// frames once that time is reached
	if (gDumpFrames < 0)
	{
		const char* d = getenv("SPIDEY_DUMPPOLYS");
		const char* at = getenv("SPIDEY_DUMPPOLYS_AT");
		gDumpFrames = d ? atoi(d) : 0;
		gDumpAt = at ? (u32)atoi(at) : 0;
	}
	i32 dumping = gDumpFrames > 0 && Plat_Ticks() >= gDumpAt;
	if (dumping)
		gDumpFrames--;

	for (i32 i = 4096; i >= 0; i--)
	{
		DXPOLY* pPoly = gSceneBuffer[i];
		if (gDxPolyRelated && gHudOffset > 0 && i == gHudOffset)
			Plat_GfxSetDepthTest(0);

		while (pPoly)
		{
			if (dumping)
				dumpPoly(pPoly, i);
			DXPOLY_SetTexture(pPoly->field_4);
			DXPOLY_SetBlendMode(pPoly->mBlendMode);
			DXPOLY_SetAddressUAndV(
					(pPoly->field_A & 2) ? 1 : 3,
					(pPoly->field_A & 4) ? 1 : 3);
			DXPOLY_EnableTexAlpha((pPoly->field_A & 8) != 0);
			DXPOLY_SetFilterMode((pPoly->field_A & 0x10) == 0);
			gDbgFanBlend = pPoly->mBlendMode; gDbgFanTex = (i32)(size_t)pPoly->field_4; gDbgFanBucket = i;
			Plat_GfxDrawFan(pPoly->field_10, pPoly->field_C);
			pPoly = pPoly->pNext;
		}
	}

	if (gDxPolyRelated && gHudOffset > 0)
		Plat_GfxSetDepthTest(1);
#endif
}

// @Ok
// @Matching
// Debug logger of this file. The release build has an empty body,
// so the PC binary keeps it as the one byte ret at 0x502D50 (nullsub_5).
void stateLog(char const *,...)
{
}

void validate_DXsound(void)
{
	VALIDATE_SIZE(_GUID, 0x10);
}

void validate_DXPOLY(void)
{
	VALIDATE_SIZE(DXPOLY, 0xF0);

	VALIDATE(DXPOLY, pNext, 0x0);
	VALIDATE(DXPOLY, field_4, 0x4);
	VALIDATE(DXPOLY, mBlendMode, 0x8);
	VALIDATE(DXPOLY, field_A, 0xA);

	VALIDATE(DXPOLY, field_C, 0xC);
	VALIDATE(DXPOLY, field_10, 0x10);
}

void validate_SDXPolyField(void)
{
	VALIDATE_SIZE(SDXPolyField, 0x1C);

	VALIDATE(SDXPolyField, field_0, 0x0);
	VALIDATE(SDXPolyField, field_4, 0x4);
	VALIDATE(SDXPolyField, field_8, 0x8);
	VALIDATE(SDXPolyField, field_C, 0xC);
	VALIDATE(SDXPolyField, field_10, 0x10);
	VALIDATE(SDXPolyField, field_14, 0x14);
	VALIDATE(SDXPolyField, field_18, 0x18);
}

void validate_SDXSoundHolder(void)
{
	VALIDATE_SIZE(SDDXSoundHolder, 0xC);

	VALIDATE(SDDXSoundHolder, pDSB, 0x0);
	VALIDATE(SDDXSoundHolder, mFrequency, 0x4);

	VALIDATE(SDDXSoundHolder, field_8, 0x8);
	VALIDATE(SDDXSoundHolder, field_9, 0x9);
}

void validate_SDxSomething(void)
{
	VALIDATE_SIZE(gDxSoundBuffers, 0x200);
}

void validate_DSBUFFERDESC(void)
{
#ifdef _WIN32
	VALIDATE_SIZE(DSBUFFERDESC, 36);

	VALIDATE(DSBUFFERDESC, dwSize, 0x0);
	VALIDATE(DSBUFFERDESC, dwFlags, 0x4);
	VALIDATE(DSBUFFERDESC, dwBufferBytes, 0x8);
#endif
}
