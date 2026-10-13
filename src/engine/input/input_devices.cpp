#include "engine/input/button_state.h"
#include "engine/input/input_devices.h"
#include "game/application/DXinit.h"
#include "game/application/SpideyDX.h"
#ifdef SPIDEY_STANDALONE
#include "platform/plat.h"
#endif
#include <cstring>
#include <cstdlib>

#ifdef SPIDEY_STANDALONE
static f32 gStandaloneRumbleStrength = 0.5f;
#endif

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
		gControllerButtonState[i] = spidey::input::ButtonState(gControllerButtonState[i]).advance((buttons[i] & 0x80) != 0).encoded();
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
		u8 next = spidey::input::ButtonState(gKeyState[i]).advance((now[i] & 0x80) != 0).encoded();
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
		gMouseButtonState[i] = spidey::input::ButtonState(gMouseButtonState[i]).advance((buttons[i] & 0x80) != 0).encoded();
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
