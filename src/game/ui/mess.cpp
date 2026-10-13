#include "game/ui/mess.h"
#include "game/ui/FontTools.h"
#include "core/game_heap.h"
#include "engine/world/bit.h"

#include <cstring>

#include "compatibility/validate.h"
#include "compatibility/my_assert.h"

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT u8 TextJustification;
#else
extern u8 TextJustification;
#endif
//#define G_TEXT_JUSTIFICATION (TextJustification)
#define G_TEXT_JUSTIFICATION (*reinterpret_cast<u8*>(0x0060D5A8))

EXPORT SMessage* pMessages;

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT SSimpleMessage* pSimpleMessages;
#else
extern SSimpleMessage* pSimpleMessages;
#endif
//#define G_SIMPLE_MESSAGES (pSimpleMessages)
#define G_SIMPLE_MESSAGES (*reinterpret_cast<SSimpleMessage**>(0x0060D5A0))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT Font gMessFont;
#else
extern Font gMessFont;
#endif
//#define G_MESS_FONT (gMessFont)
#define G_MESS_FONT (*reinterpret_cast<Font*>(0x0060D238))


// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT u16 Scale;
#else
extern u16 Scale;
#endif
//#define G_SCALE (Scale)
#define G_SCALE (*reinterpret_cast<u16*>(0x0060D5A4))

// @Ok - must be extern to have the code match, reads are not aligned to 32 bits
extern u16 Sort;
// G_SORT is now defined in mess.h (used from pshell.cpp too)

EXPORT i32 gMessRelated;

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT i32 gRGBBottom = 0x808080;
#else
extern i32 gRGBBottom;
#endif
//#define G_RGB_BOTTOM (gRGBBottom)
#define G_RGB_BOTTOM (*reinterpret_cast<i32*>(0x0054D7B4))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT u8 gShadowRGB = 0x80;
#else
extern u8 gShadowRGB;
#endif
//#define G_SHADOW_RGB (gShadowRGB)
#define G_SHADOW_RGB (*reinterpret_cast<u8*>(0x0054D7A8))

EXPORT char gMessFontName[32];
EXPORT u8 gMessFontLoaded;

#ifndef SPIDEY_STANDALONE
EXPORT u32 gSimpleMessageRelated;
#else
extern u32 gSimpleMessageRelated;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT i32 gSimpleMessageTextWidth;
#else
extern i32 gSimpleMessageTextWidth;
#endif


// @Ok
// @Matching
 SMessage* CreateMessage(void)
{
	SMessage* newMessage = static_cast<SMessage*>(
			DCMem_New(sizeof(SMessage), 0, 1, 0, 1));

	newMessage->pPrevious = 0;
	newMessage->pNext = pMessages;

	pMessages = newMessage;

	if (newMessage->pNext)
		newMessage->pNext->pPrevious = newMessage;

	return newMessage;
}

// @Ok
 SSimpleMessage* CreateSimpleMessage(void)
{
	SSimpleMessage* newMessage = static_cast<SSimpleMessage*>(
			DCMem_New(sizeof(SSimpleMessage), 0, 1, 0, 1));

	newMessage->pPrevious = 0;
	newMessage->pNext = pSimpleMessages;

	pSimpleMessages = newMessage;

	if (newMessage->pNext)
		newMessage->pNext->pPrevious = newMessage;

	return newMessage;
}

// @Ok
// For the life of me, can't get it to match the assembly is too obtuse
// I think the developers wrote some macros that when expanded looked weird
void Mess_DeleteAll(void)
{
	SMessage *curMessage = pMessages;
	while (curMessage)
	{
		SMessage* nextMessage = curMessage->pNext;
		Mem_Delete(curMessage);

		curMessage = nextMessage;
	}

	pMessages = 0;

	SSimpleMessage *curSimpleMessage = pSimpleMessages;
	while (curSimpleMessage)
	{
		SSimpleMessage* nextMessage = curSimpleMessage->pNext;
		Mem_Delete(curSimpleMessage);
		curSimpleMessage = nextMessage;
	}

	pSimpleMessages = 0;
}

// @Ok
// @Matching
void Mess_Display(void)
{
	SSimpleMessage* pMess = pSimpleMessages;
	Mess_SetTextJustify(1);
	Mess_SetRGB(0x60, 0x60, 0x80, 0);

	while (pMess)
	{
		Mess_SetScale(pMess->field_14);
		Mess_DrawText(
				pMess->field_C,
				pMess->field_10,
				pMess->field_0,
				0,
				pMess->field_18);

		pMess = pMess->pNext;
	}
}

// @Ok
i32 Mess_DrawText(
		i32 a1,
		i32 a2,
		const char * a3,
		i32 a4,
		u32 a5)
{
	switch (G_TEXT_JUSTIFICATION)
	{
		case 0:
			G_MESS_FONT.field_4 = 1;
			break;
		case 1:
			G_MESS_FONT.field_4 = 0;
			break;
		case 2:
			G_MESS_FONT.field_4 = 2;
			break;
	}

	G_MESS_FONT.field_34 = 8 * G_SCALE;

	switch (G_SORT)
	{
		case 4093:
			G_MESS_FONT.draw(a1, a2, a3, 8, 6.0999999f);
			break;
		case 4094:
			G_MESS_FONT.draw(a1, a2, a3, 8, -4.0f);
			break;
		case 4095:
			G_MESS_FONT.draw(a1, a2, a3, 8, -3.0f);
			break;
		default:
			G_MESS_FONT.draw(a1, a2, a3, 8, 4.0f);
			break;
	}

	return 0;
}

// @Ok
i32 Mess_GetScale(void)
{
	return G_SCALE;
}

// @Ok
i32 Mess_GetSort(void)
{
	return G_SORT;
}

// @Ok
i32 Mess_LoadFont(
		const char* pName,
		i32 a2,
		i32 a3,
		i32 a4)
{
	Font* pFont = FontManager::LoadFont(pName);
	gMessFont = *pFont;

	strcpy(gMessFontName, pName);

	gMessFont.field_C = a2;
	gMessFont.field_10 = a2;

	gMessFont.field_24 = a3;
	gMessFont.field_28 = a3;
	gMessFont.field_8 = 1;

	gMessFontLoaded = 1;

	return 0;
}

// @Ok
// @Matching
SMessage* Mess_Message(const char * pText, SMessageProg* pProg)
{
	SMessage* newMessage = CreateMessage();

	newMessage->pText = pText;
	newMessage->pProg = pProg;

	newMessage->T = 0;
	newMessage->T_total = 0;
	return newMessage;
}

// @Ok
// @Matching
void Mess_SetShadowRGB(u8 rgb)
{
	if (rgb < 0x80)
		rgb = 0x80;

	G_SHADOW_RGB = rgb;
}

// @Ok
// @Matching
void Mess_SetSort(i32 sort)
{
	G_SORT = sort;
}

// @Ok
// @Matching
void Mess_ShadowsOff(void)
{
	FontManager::AllShadowOff();
}

// @Ok
// @Matching
void Mess_ShadowsOn(void)
{
	FontManager::AllShadowOn();
}

// @Ok
SSimpleMessage* Mess_SimpleMessage(
		const char * a1,
		u32 a2,
		u32 a3,
		u32 a4)
{
	SSimpleMessage* pMessage = CreateSimpleMessage();

	pMessage->field_0 = a1;
	pMessage->field_4 = 0;
	pMessage->field_8 = a4;
	pMessage->field_C = a2;
	pMessage->field_10 = a3;
	pMessage->field_14 = 200;

	gSimpleMessageRelated = a2;

	Mess_SetScale(200);

	gSimpleMessageTextWidth = Mess_TextWidth(a1);

	return pMessage;
}

// @Ok
// @Matching
 i32 Mess_TextWidth(const char* pMessage)
{
	G_MESS_FONT.field_34 = 8 * G_SCALE;
	return G_MESS_FONT.width(pMessage);
}

// @Ok
// @Matching
void Mess_UnloadAllFonts(void)
{
	FontManager::UnloadAllFonts();
}

// @Ok
void Mess_Update(void)
{
	if (!gMessRelated)
		gMessRelated = G_TIMER_RELATED - 2;

	i32 v1 = G_TIMER_RELATED - gMessRelated;
	SSimpleMessage* pMessage = pSimpleMessages;
	gMessRelated = G_TIMER_RELATED;

	while (pMessage)
	{
		pMessage->field_4 += v1;

		if (pMessage->field_4 > pMessage->field_8 + 90)
		{
			SSimpleMessage *next = pMessage->pNext;
			DeleteSimpleMessage(pMessage);
			pMessage = next;
		}
		else
		{
			if (pMessage->field_4 > pMessage->field_8 + 30)
			{
				pMessage->field_18 = 4096 - ((pMessage->field_4 - (pMessage->field_8 + 30)) << 12) / 60;
			}
			else if (pMessage->field_4 > 60)
			{
				pMessage->field_18 = 4096;
			}
			else
			{
				pMessage->field_18 = (pMessage->field_4 << 12) / 60;
			}

			pMessage = pMessage->pNext;
		}
	}
}

// @Ok
// @Matching
 void Mess_SetTextJustify(unsigned char value)
{
	G_TEXT_JUSTIFICATION = value;
}

// @Ok
// @Matching
 void Mess_SetScale(i32 value)
{
	G_SCALE = value;
}

// @Ok
// @Matching
void Mess_SetRGBBottom(unsigned char a2, unsigned char a3, unsigned char a4)
{
	G_RGB_BOTTOM = a2 | (a3 << 8) | (a4 << 16);
}

// @Ok
// @Matching
void Mess_UnloadFont(void)
{
	FontManager::UnloadFont(&G_MESS_FONT);
}

// @Ok
// @Matching
char* Mess_GetCurrentFont(void)
{
	return FontManager::GetFontName(&G_MESS_FONT);
}

// @Ok
// @Matching
i32 Mess_TextHeight(char *pStr)
{
	return G_MESS_FONT.height(pStr);
}

// @Ok
// @Matching
void Mess_ClearSimpleMessages(void)
{
	while (G_SIMPLE_MESSAGES)
	{
		DeleteSimpleMessage(G_SIMPLE_MESSAGES);
	}
}

// @Ok
// @Matching
 void DeleteSimpleMessage(SSimpleMessage* pMessage)
{
	ASSERT(pMessage != 0, "Tried to delete a NULL pMessage");

	if (pMessage->pNext)
		pMessage->pNext->pPrevious = pMessage->pPrevious;

	if (pMessage->pPrevious)
		pMessage->pPrevious->pNext = pMessage->pNext;

	if (pMessage == G_SIMPLE_MESSAGES)
		G_SIMPLE_MESSAGES = pMessage->pNext;

	Mem_Delete(pMessage);
}


// @Ok
// @Matching
 void Mess_SetRGB(u8 r, u8 g, u8 b, i32)
{
	G_MESS_FONT.mRed = r;
	G_MESS_FONT.mGreen = g;
	G_MESS_FONT.mBlue = b;
}

// @Ok
// @Matching
void Mess_SetCurrentFont(char* a1)
{
	Font* pFont = FontManager::GetFont(a1);
	G_MESS_FONT = *pFont;
}

void validate_SimpleMessage(void)
{
	VALIDATE_SIZE(SSimpleMessage, 0x24);

	VALIDATE(SSimpleMessage, field_0, 0x0);
	VALIDATE(SSimpleMessage, field_4, 0x4);
	VALIDATE(SSimpleMessage, field_8, 0x8);
	VALIDATE(SSimpleMessage, field_C, 0xC);

	VALIDATE(SSimpleMessage, field_10, 0x10);
	VALIDATE(SSimpleMessage, field_14, 0x14);
	VALIDATE(SSimpleMessage, field_18, 0x18);

	VALIDATE(SSimpleMessage, pNext, 0x1C);
	VALIDATE(SSimpleMessage, pPrevious, 0x20);
}

void validate_SMessageProg(void)
{
}

void validate_SMessage(void)
{
	VALIDATE_SIZE(SMessage, 0x1C);

	VALIDATE(SMessage, pText, 0x0);
	VALIDATE(SMessage, T, 0x4);
	VALIDATE(SMessage, T_total, 0x6);

	VALIDATE(SMessage, pProg, 0x10);
	VALIDATE(SMessage, pNext, 0x14);
	VALIDATE(SMessage, pPrevious, 0x18);
}

#include "compatibility/my_patch.h"

// @Bogus
void patch_mess(void)
{
	PATCH_PUSH_RET(0x00458D50, Mess_GetCurrentFont);
	PATCH_PUSH_RET(0x00458C20, Mess_UnloadAllFonts);
	PATCH_PUSH_RET(0x00458C10, Mess_UnloadFont);
	PATCH_PUSH_RET(0x004586F0, Mess_TextHeight);
	PATCH_PUSH_RET(0x004586C0, Mess_TextWidth);

	PATCH_PUSH_RET(0x004586B0, Mess_SetShadowRGB);

	PATCH_PUSH_RET(0x00458690, Mess_ShadowsOff);
	PATCH_PUSH_RET(0x004586A0, Mess_ShadowsOn);

	PATCH_PUSH_RET(0x00458670, Mess_SetRGBBottom);
	PATCH_PUSH_RET(0x00458640, Mess_SetRGB);

	PATCH_PUSH_RET(0x00458630, Mess_SetSort);
	PATCH_PUSH_RET(0x00458620, Mess_SetScale);
	PATCH_PUSH_RET(0x00458610, Mess_SetTextJustify);

	PATCH_PUSH_RET(0x00458700, Mess_DrawText);

	PATCH_PUSH_RET(0x004588E0, Mess_ClearSimpleMessages);
	PATCH_PUSH_RET(0x00458C30, Mess_SetCurrentFont);
}
