#include "game/ui/screen.h"
#include "engine/rendering/scene_renderer.h"
#include "engine/rendering/render_buffers.h"
#include "game/ui/panel.h"
#include "game/cinematics/camera.h"
#include "engine/rendering/ps2funcs.h"
#include "compatibility/psx_types.h"
#include "core/game_heap.h"
#include "core/utils.h"
#include "game/ui/PCShell.h"
#include "game/application/SpideyDX.h"
#include "engine/rendering/m3dinit.h"
#include "engine/rendering/PCGfx.h"

// stru_56F224: a fixed MATRIX the original loads into the GTE rotation
// register before projecting the target position (0x56F224).
static MATRIX * const gTargetRotMatrix = (MATRIX*)0x56F224;


// All the globals below live in one block at 0x006A7460..0x006A7573 in the
// exe. Every address here comes from the store or load instruction named in
// its comment. They stay file local: nothing outside screen.cpp reads them.

// Screen_TargetOn (0x48AA40) writes it ("mov [6A7572h],al"), the reticle
// draw (0x48AA90) reads it.
#ifndef SPIDEY_STANDALONE
EXPORT bool gScreenTarget;
#else
extern bool gScreenTarget;
#endif
//#define G_SCREEN_TARGET (gScreenTarget)
#define G_SCREEN_TARGET (*reinterpret_cast<bool*>(0x006A7572))

// The arrow has its OWN on/off byte, one past the reticle's.
// Screen_DrawArrow (0x48AE30) opens with "mov al,[6A7573h]", not 0x6A7572.
// No named function in tools/functions writes it, so the writer is still
// unidentified.
#ifndef SPIDEY_STANDALONE
EXPORT bool gScreenArrow;
#else
extern bool gScreenArrow;
#endif
//#define G_SCREEN_ARROW (gScreenArrow)
#define G_SCREEN_ARROW (*reinterpret_cast<bool*>(0x006A7573))

// Screen_SetTarget (0x48AA50) writes the three components at 0x6A7488 /
// 0x6A748C / 0x6A7490 and the two words at 0x6A7494 (its u16 argument) and
// 0x6A7484 (its i16 argument). sub_48AA90 (the reticle draw) reads all five.
#ifndef SPIDEY_STANDALONE
EXPORT CVector gTargetRelated;
#else
extern CVector gTargetRelated;
#endif
//#define G_TARGET_RELATED (gTargetRelated)
#define G_TARGET_RELATED (*reinterpret_cast<CVector*>(0x006A7488))

#ifndef SPIDEY_STANDALONE
EXPORT u16 gTargetOne;
#else
extern u16 gTargetOne;
#endif
//#define G_TARGET_ONE (gTargetOne)
#define G_TARGET_ONE (*reinterpret_cast<u16*>(0x006A7494))

#ifndef SPIDEY_STANDALONE
EXPORT u16 gTargetTwo;
#else
extern u16 gTargetTwo;
#endif
//#define G_TARGET_TWO (gTargetTwo)
#define G_TARGET_TWO (*reinterpret_cast<u16*>(0x006A7484))

// The arrow points at a different vector than the reticle does:
// Screen_DrawArrow reads 0x6A74D8 / 0x6A74DC / 0x6A74E0, written by the
// small unnamed setter sub_48A800.
#ifndef SPIDEY_STANDALONE
EXPORT CVector gArrowTargetPos;
#else
extern CVector gArrowTargetPos;
#endif
//#define G_ARROW_TARGET_POS (gArrowTargetPos)
#define G_ARROW_TARGET_POS (*reinterpret_cast<CVector*>(0x006A74D8))

// Screen_StartCircularFadeIn (0x48AFB0) writes all five
// ("mov dword ptr [6A7460h],20h", "mov [6A751Ch],eax", "mov [6A756Ch],ecx",
// "mov byte ptr [6A7570h],1", "mov [6A7571h],al"); Screen_UpdateFades
// (0x48AFE0) reads and writes the last four. 0x6A7460 has no reader.
#ifndef SPIDEY_STANDALONE
EXPORT i32 gCircularFadeRelated;
#else
extern i32 gCircularFadeRelated;
#endif
//#define G_CIRCULAR_FADE_RELATED (gCircularFadeRelated)
#define G_CIRCULAR_FADE_RELATED (*reinterpret_cast<i32*>(0x006A7460))

#ifndef SPIDEY_STANDALONE
EXPORT i32 gCircularFadeRelatedOne;
#else
extern i32 gCircularFadeRelatedOne;
#endif
//#define G_CIRCULAR_FADE_RELATED_ONE (gCircularFadeRelatedOne)
#define G_CIRCULAR_FADE_RELATED_ONE (*reinterpret_cast<i32*>(0x006A751C))

#ifndef SPIDEY_STANDALONE
EXPORT i32 gCircularFadeRelatedTwo;
#else
extern i32 gCircularFadeRelatedTwo;
#endif
//#define G_CIRCULAR_FADE_RELATED_TWO (gCircularFadeRelatedTwo)
#define G_CIRCULAR_FADE_RELATED_TWO (*reinterpret_cast<i32*>(0x006A756C))

#ifndef SPIDEY_STANDALONE
EXPORT u8 gCircularFadeRelatedThree;
#else
extern u8 gCircularFadeRelatedThree;
#endif
//#define G_CIRCULAR_FADE_RELATED_THREE (gCircularFadeRelatedThree)
#define G_CIRCULAR_FADE_RELATED_THREE (*reinterpret_cast<u8*>(0x006A7570))

#ifndef SPIDEY_STANDALONE
EXPORT u8 gCircularFadeRelatedFour;
#else
extern u8 gCircularFadeRelatedFour;
#endif
//#define G_CIRCULAR_FADE_RELATED_FOUR (gCircularFadeRelatedFour)
#define G_CIRCULAR_FADE_RELATED_FOUR (*reinterpret_cast<u8*>(0x006A7571))


// @Ok
// The arrow reads its own flag and its own target vector, not the reticle's:
// 0x48AE30 starts with "mov al,[6A7573h]" and then loads 0x6A74D8/DC/E0,
// while the reticle path (0x48AA40 / 0x48AA50 / 0x48AA90) uses 0x6A7572 and
// 0x6A7488/8C/90. This file used to point both at the reticle pair.
void Screen_DrawArrow(void)
{
	if (!G_SCREEN_ARROW)
		return;

	u32 *next = G_PPOLY + 15;
	if ((u8*)next >= G_POLY_BUFFER_END)
		return;
	POLY_F3 *tri = (POLY_F3*)G_PPOLY;
	G_PPOLY = next;

	VECTOR relPos;
	relPos.vx = (G_ARROW_TARGET_POS.vx >> 12) - G_MIKE_CAMERA[0].Position.vx;
	relPos.vy = (G_ARROW_TARGET_POS.vy >> 12) - G_MIKE_CAMERA[0].Position.vy;
	relPos.vz = (G_ARROW_TARGET_POS.vz >> 12) - G_MIKE_CAMERA[0].Position.vz;

	gte_ldlv0(&relPos);
	gte_rtps();
	i32 stlv[3];
	gte_stlvnl2(stlv);

	u8 *clip = G_VIEW_CLIP_INFO;
	u16 clipMin = *(u16*)(clip + 8);
	u16 clipMax = *(u16*)(clip + 0xA);
	if ((u32)relPos.vy < clipMin || (u32)relPos.vy > clipMax)
		return;

	i32 sxy;
	gte_stsxy(&sxy);
	i32 screenX = (i16)sxy;
	i32 screenY = (i16)(sxy >> 16);

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: setPolyF3");

	tri->code = 0xA0;
	tri->x0 = (i16)screenX;
	tri->y0 = (i16)screenY;
	tri->x1 = (i16)(screenX - 12);
	tri->y1 = (i16)(screenY - 12);
	tri->x2 = (i16)(screenX + 12);
	tri->y2 = (i16)(screenY - 12);

	gsub_46CB90(G_RENDER_BUF);

	POLY_F4 *quad = (POLY_F4*)((u8*)tri + 0x14);
	quad->code = 0xA0;

	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: setPolyF4");

	quad->x0 = (i16)(screenX - 6);
	quad->x1 = (i16)(screenX - 6);
	quad->y0 = (i16)(screenY - 12);
	quad->y2 = (i16)(screenY - 12);
	quad->x2 = (i16)(screenX + 6);
	quad->x3 = (i16)(screenX + 6);
	quad->y1 = (i16)(screenY - 24);
	quad->y3 = (i16)(screenY - 24);

	gsub_46CB90(G_RENDER_BUF);
}

// @Ok
// (0x0048AA90, 925 bytes). Draws the 4-triangle target reticle. The IDB
// decompiler mangled the FPU scaling; the logic is: project the target with
// the GTE, then for each of 4 triangles build 3 corners from rcossin_tbl
// offsets around the screen x/y and scale by gGameResolution/Yres,Xres.
void Screen_DrawTarget(void)
{
	if (!G_SCREEN_TARGET)
		return;

	gte_SetRotMatrix(gTargetRotMatrix);
	m3d_ZeroTransVector();

	VECTOR relPos;
	relPos.vx = (G_TARGET_RELATED.vx >> 12) - G_MIKE_CAMERA[0].Position.vx;
	relPos.vy = (G_TARGET_RELATED.vy >> 12) - G_MIKE_CAMERA[0].Position.vy;
	relPos.vz = (G_TARGET_RELATED.vz >> 12) - G_MIKE_CAMERA[0].Position.vz;
	gte_ldlv0(&relPos);
	gte_rtps();

	PCGfx_UseTexture(1, DCGfx_BlendingMode_0);

	u8 *pPolyByte = (u8*)G_PPOLY;
	if (pPolyByte + 80 >= G_POLY_BUFFER_END)
		return;

	u8 *pQuad = pPolyByte + 4;  // quad data starts 4 bytes in (header)
	G_PPOLY = (u32*)(pPolyByte + 80);

	i32 screenXY;
	gte_stsxy(&screenXY);
	i16 screenX = (i16)screenXY;
	i16 screenY = (i16)(screenXY >> 16);

	f32 yScale = (f32)G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 xScale = (f32)G_GAME_RESOLUTION_X / (f32)G_XRES;

	i32 angleA = G_TARGET_TWO;
	i32 angleB = G_TARGET_TWO + 128;
	for (i32 i = 0; i < 4; i++)
	{
		u8 *v = pQuad + i * 20;
		*(u32*)v = 41120;  // 0xA0A0 gray

		i16 c0x = screenX + (((G_TARGET_ONE - 12) * G_RCOSSIN_TBL[angleA & 0xFFF].sin) >> 12);
		i16 c0y = screenY + 320 * (((G_TARGET_ONE - 12) * G_RCOSSIN_TBL[angleA & 0xFFF].cos) >> 12) / 512;
		i16 c1x = screenX + ((G_TARGET_ONE * G_RCOSSIN_TBL[(angleB - 256) & 0xFFF].sin) >> 12);
		i16 c1y = screenY + 320 * ((G_TARGET_ONE * G_RCOSSIN_TBL[(angleB - 256) & 0xFFF].cos) >> 12) / 512;
		i16 c2x = screenX + ((G_TARGET_ONE * G_RCOSSIN_TBL[angleB & 0xFFF].sin) >> 12);
		i16 c2y = screenY + 320 * ((G_TARGET_ONE * G_RCOSSIN_TBL[angleB & 0xFFF].cos) >> 12) / 512;

		*(i16*)(v + 4) = c0x;
		*(i16*)(v + 6) = c0y;
		*(i16*)(v + 8) = c1x;
		*(i16*)(v + 10) = c1y;
		*(i16*)(v + 12) = c2x;
		*(i16*)(v + 14) = c2y;

		f32 p0x = (f32)c0x * xScale;
		f32 p0y = (f32)c0y * yScale;
		f32 p1x = (f32)c1x * xScale;
		f32 p1y = (f32)c1y * yScale;
		f32 p2x = (f32)c2x * xScale;
		f32 p2y = (f32)c2y * yScale;

		PCGfx_DrawQPoly2D(p0x, p0y, 1.0f, 0.0f, 41120, p1x, p1y, 0.0f, 0.0f, 41120,
				p2x, p2y, 0.0f, 1.0f, 41120, p2x, p2y, 1.0f, 1.0f, 41120, 5.0f);

		angleA += 1024;
		angleB += 1024;
	}
}

// Same two addresses as gFrontCameraModeFlagOne/Two in front.cpp (0x56FB78 /
// 0x56FBF4, "meaning unclear without a consumer" per that file's comment).
// Not G_* macros (no shared/hooked state), so a second tentative name in
// this file follows the older "duplicate static address, own name" rule.
// Here they are cleared right before, and set right after, three
// Db_FlipClear()/optimized_unused_garbage() calls bracketing the whole
// palette rebuild, so a "double buffer busy" style flag pair is the best
// guess from this call site.
#define gDbBusyFlagOne (*reinterpret_cast<u8*>(0x0056FB78))
#define gDbBusyFlagTwo (*reinterpret_cast<u8*>(0x0056FBF4))

// @Ok
// Functional-only pass (session override on the acceptance bar). Verified
// against the IDA Hex-Rays decompile of 0x48A820 line by line: the three
// gPrintStubbed-guarded debug calls, Pause(1), the gDbBusyFlagOne/Two clear
// -> Db_FlipClear -> gsub_430680 sequence, the DCMem_New(0x1000) 4-chunk
// split (each chunk +0x400 apart), the 240-iteration outer loop over 256
// dwords per chunk, and the pixel math (5-bit channel sum, *1365>>12 and
// *682>>12 truncated to u8 for the two grey levels, low pixel keeps d&0x8000,
// high pixel keeps d&0x80000000, both greys packed as grey|(grey<<5)|(other<<10))
// all match exactly. The two real bugs found in an earlier pass (missing CSE
// on the channel-sum expression, sar/shr sign bug from i32 vs u32 sum locals)
// are still fixed. residue: 84 mnemonic diffs (cmpsum) is scheduling/register
// pressure only: a dead counter (bx, incremented every loop iteration and never
// stored to memory, matches the decompile's v1/LOWORD(v1)++ pattern) and
// register-spill differences inside the per-pixel loop, not a logic gap.
void Screen_SepiaFade(void)
{
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: MoveImage");
	if (!gPrintStubbed)
		gsub_46CB90((void*)"stubbed out: DrawSync");

	Pause(1);
	gDbBusyFlagTwo = 0;
	gDbBusyFlagOne = 0;
	Db_FlipClear();
	gsub_430680();

	void *p = DCMem_New(0x1000, 0, 1, 0, true);
	i32 x = (i32)G_PDOUBLE_BUFFER - (i32)&G_DOUBLE_BUFFER[0];
	void *chunk[4];
	chunk[0] = p;
	p = (u8*)p + 0x400;
	chunk[1] = p;
	p = (u8*)p + 0x400;
	chunk[2] = p;
	p = (u8*)p + 0x400;
	chunk[3] = p;

	i32 i = 0;
	volatile u16 bx = (x != 0) ? 0x100 : 0;

	for (; i < 0xF0; i++, bx++)
	{
		u32 *p = (u32*)chunk[i & 3];

		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: StoreImage");

		for (i32 j = 0; j < 0x100; j++)
		{
			u32 d = *p;

			u32 s = ((d >> 10) & 0x1F) + ((d >> 5) & 0x1F) + (d & 0x1F);
			u8 grey1 = (s * 1365) >> 12;
			u8 grey2 = (s * 682) >> 12;
			u32 lo = (d & 0x8000) | (grey1 << 10) | (grey2 << 5) | grey2;

			u32 s2 = ((d >> 26) & 0x1F) + ((d >> 21) & 0x1F) + ((d >> 16) & 0x1F);
			u8 grey1b = (s2 * 1365) >> 12;
			u8 grey2b = (s2 * 682) >> 12;
			u32 hi = (grey1b << 10) | (grey2b << 5) | grey2b;

			*p = (d & 0x80000000) | lo | (hi << 16);
			p++;
		}
	}

	Mem_Delete(chunk[0]);
	Db_FlipClear();
	gsub_430680();
	Db_FlipClear();
	gDbBusyFlagTwo = 1;
	gDbBusyFlagOne = 1;

	G_GAME_FADE = 0x1E005A;
}

// @Ok
void Screen_SetTarget(
		CVector *a1,
		u16 a2,
		i16 a3)
{
	G_TARGET_RELATED = *a1;
	G_TARGET_ONE = a2;
	G_TARGET_TWO = a3;
}

// @Ok
void Screen_StartCircularFadeIn(i32,i32 a2)
{
	G_CIRCULAR_FADE_RELATED = 32;
	G_CIRCULAR_FADE_RELATED_ONE = 0;
	G_CIRCULAR_FADE_RELATED_TWO = a2 << 12;

	G_CIRCULAR_FADE_RELATED_THREE = 1;
	G_CIRCULAR_FADE_RELATED_FOUR = 0;
}

// gCircularFadeShapeProgram/gCircularFadeShapePoints (0x54ED9C/0x54ECBC):
// fixed game data read by Screen_UpdateFades (0x48AFE0) to build the curved
// boundary of the circular wipe. The program is a byte stream of 21
// primitives: opcode byte (3=triangle/3 points, 4=quad/4 points), one pad
// byte, then N point-index bytes. Each index looks up an (x,y) pair in the
// point table (i16, values roughly in 0..256 x 0..240, centred at 256,120).
// Confirmed by reading the raw bytes at these addresses with IDA and
// replaying the loop in sub_48AFE0 by hand (21 entries: 20 opcode-4 quads
// and one opcode-3 triangle at position 3, matching the v142=21 loop count).
static u8 * const gCircularFadeShapeProgram = (u8*)0x54ED9C;
static i16 * const gCircularFadeShapePoints = (i16*)0x54ECBC;

// Shared tail of every fade primitive: scale the poly's screen-space corners
// (in the fade's 512x240 reference space) into game resolution and issue it
// twice through PCGfx_DrawQPoly2D, with the vertex order/UV swapped between
// the two calls (V1/V2 swapped, UVs adjusted to match). Verified against the
// disassembly bit for bit; the reason for issuing it twice isn't understood
// (a two-pass blend for a soft edge is the working guess) but the sequence
// is reproduced exactly rather than guessed away.
// This helper is not a separate function in the original (its 3 call sites
// are inlined at 0x48b198, 0x48b567, 0x48b8ff); factored out here since the
// acceptance bar for this pass is functional correctness, not a byte match
// (PLAN.md), so it doesn't hurt to avoid the triplication.
// @Ok
static void CircularFade_DrawQuad(POLY_F4 *p, f32 xScale, f32 yScale)
{
	gsub_46CB90(G_RENDER_BUF);

	f32 x0 = (f32)p->x0 * xScale, y0 = (f32)p->y0 * yScale;
	f32 x1 = (f32)p->x1 * xScale, y1 = (f32)p->y1 * yScale;
	f32 x2 = (f32)p->x2 * xScale, y2 = (f32)p->y2 * yScale;
	f32 x3 = (f32)p->x3 * xScale, y3 = (f32)p->y3 * yScale;

	PCGfx_DrawQPoly2D(x0, y0, 0.0f, 0.0f, 0,
			x1, y1, 1.0f, 0.0f, 0,
			x2, y2, 0.0f, 1.0f, 0,
			x3, y3, 1.0f, 1.0f, 0, 0.0f);

	PCGfx_DrawQPoly2D(x0, y0, 0.0f, 0.0f, 0,
			x2, y2, 0.0f, 1.0f, 0,
			x1, y1, 1.0f, 0.0f, 0,
			x3, y3, 1.0f, 1.0f, 0, 0.0f);
}

// Same as CircularFade_DrawQuad but for the ring's one triangle primitive:
// the original reuses point 2 as a degenerate 4th vertex instead of a
// separate 3-vertex draw call.
// @Ok
static void CircularFade_DrawTri(POLY_F3 *p, f32 xScale, f32 yScale)
{
	gsub_46CB90(G_RENDER_BUF);

	f32 x0 = (f32)p->x0 * xScale, y0 = (f32)p->y0 * yScale;
	f32 x1 = (f32)p->x1 * xScale, y1 = (f32)p->y1 * yScale;
	f32 x2 = (f32)p->x2 * xScale, y2 = (f32)p->y2 * yScale;

	PCGfx_DrawQPoly2D(x0, y0, 0.0f, 0.0f, 0,
			x1, y1, 1.0f, 0.0f, 0,
			x2, y2, 0.0f, 1.0f, 0,
			x2, y2, 1.0f, 1.0f, 0, 0.0f);

	PCGfx_DrawQPoly2D(x0, y0, 0.0f, 0.0f, 0,
			x2, y2, 0.0f, 1.0f, 0,
			x1, y1, 1.0f, 0.0f, 0,
			x1, y1, 1.0f, 1.0f, 0, 0.0f);
}

// @Ok
// Full re-decompile of 0x48AFE0 (3078 bytes), replacing the old
// screen_DrawCircularFade split. See CLAUDE.md 2026-08-27 note: names.json
// mislabelled 0x48AFE0 as screen_DrawCircularFade; it is actually the real
// Screen_UpdateFades with screen_DrawCircularFade fully inlined (the Mac
// build keeps them separate per prototypes.json: 164 + 3228 bytes). The
// dispatch part below (G_CIRCULAR_FADE_RELATED_THREE/Four/One/Two) was already
// correct; what follows the old screen_DrawCircularFade() call is new.
//
// Once the state update above says "still fading", this draws the circular
// wipe: while G_CIRCULAR_FADE_RELATED_ONE (scaled x16 into a 512x240 reference
// space as `radius`) is under 256, four rectangles cover
// everything outside a centred cross the circle is growing into (left,
// right, top, bottom bands -- cases 0-3 in the switch, all sharing the same
// left/right x bounds since the "half width" only depends on radius, not on
// which band is being built). Then, always, a 21-primitive fan (see
// gCircularFadeShapeProgram above) draws the actual curved boundary, run
// twice (i loop) with the x sign flipped to cover both left and right
// halves of the screen.
void Screen_UpdateFades(void)
{
	if (G_CIRCULAR_FADE_RELATED_THREE)
	{
		G_CIRCULAR_FADE_RELATED_ONE += G_CIRCULAR_FADE_RELATED_TWO >> 12;
		G_CIRCULAR_FADE_RELATED_TWO += 3072;
		if ( G_CIRCULAR_FADE_RELATED_ONE >= 640 )
		{
			G_CIRCULAR_FADE_RELATED_THREE = 0;
			return;
		}
	}
	else
	{
		if (!G_CIRCULAR_FADE_RELATED_FOUR)
			return;
		G_CIRCULAR_FADE_RELATED_ONE -= G_CIRCULAR_FADE_RELATED_TWO >> 12;
		if (G_CIRCULAR_FADE_RELATED_ONE <= 0)
		{
			G_CIRCULAR_FADE_RELATED_FOUR = 0;
			return;
		}
	}

	// Bail if there isn't room left in the poly scratch buffer for the
	// worst case (21 quads + the 4 rectangles, all POLY_F4-sized).
	if ((u8*)G_PPOLY + 1200 >= G_POLY_BUFFER_END)
		return;

	PCGfx_UseTexture(1, DCGfx_BlendingMode_0);

	i32 radius = 16 * G_CIRCULAR_FADE_RELATED_ONE;

	f32 yScale = (f32)G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 xScale = (f32)G_GAME_RESOLUTION_X / (f32)G_XRES;

	if (radius < 0x1000)
	{
		i16 half = (i16)((radius << 8) >> 12);
		i16 left = (i16)(256 - half);
		i16 right = (i16)(256 + half);
		i16 hTop = (i16)(120 - ((120 * radius) >> 12));
		i16 hBot = (i16)((120 * radius) >> 12);

		for (i32 band = 0; band < 4; band++)
		{
			POLY_F4 *p = (POLY_F4*)G_PPOLY;
			G_PPOLY = (u32*)((u8*)G_PPOLY + sizeof(POLY_F4));

			p->tag = 0x5000000;
			p->r0 = 0;
			p->g0 = 0;
			p->b0 = 0;
			p->code = 40;

			switch (band)
			{
			case 0:
				p->x0 = 0;    p->y0 = 0;
				p->x1 = left; p->y1 = 0;
				p->x2 = 0;    p->y2 = 240;
				p->x3 = left; p->y3 = 240;
				break;
			case 1:
				p->x0 = 512;   p->y0 = 0;
				p->x1 = right; p->y1 = 0;
				p->x2 = 512;   p->y2 = 240;
				p->x3 = right; p->y3 = 240;
				break;
			case 2:
				p->x0 = left;  p->y0 = 0;
				p->x1 = right; p->y1 = 0;
				p->x2 = left;  p->y2 = hTop;
				p->x3 = right; p->y3 = hTop;
				break;
			case 3:
				p->x0 = left;  p->y0 = (i16)(hBot + 120);
				p->x1 = right; p->y1 = (i16)(hBot + 120);
				p->x2 = left;  p->y2 = 240;
				p->x3 = right; p->y3 = 240;
				break;
			}

			CircularFade_DrawQuad(p, xScale, yScale);
		}
	}

	for (i32 i = 0; i < 2; i++)
	{
		u8 *prog = gCircularFadeShapeProgram;
		i32 sign = (i != 0) ? 1 : -1;

		for (i32 n = 21; n != 0; n--)
		{
			u8 opcode = *prog++;

			if (opcode == 3)
			{
				++prog; // pad byte

				POLY_F3 *p = (POLY_F3*)G_PPOLY;
				G_PPOLY = (u32*)((u8*)G_PPOLY + sizeof(POLY_F3));

				p->tag = 0x4000000;
				p->r0 = 0;
				p->g0 = 0;
				p->b0 = 0;
				p->code = 32;

				for (i32 j = 0; j < 3; j++)
				{
					i32 idx = *prog++;
					i16 x = (i16)(sign * ((radius * gCircularFadeShapePoints[2 * idx]) >> 12) + 256);
					i16 y = (i16)(((radius * (gCircularFadeShapePoints[2 * idx + 1] - 120)) >> 12) + 120);
					switch (j)
					{
					case 0: p->x0 = x; p->y0 = y; break;
					case 1: p->x1 = x; p->y1 = y; break;
					case 2: p->x2 = x; p->y2 = y; break;
					}
				}

				CircularFade_DrawTri(p, xScale, yScale);
			}
			else // opcode == 4
			{
				++prog; // pad byte

				POLY_F4 *p = (POLY_F4*)G_PPOLY;
				G_PPOLY = (u32*)((u8*)G_PPOLY + sizeof(POLY_F4));

				p->tag = 0x5000000;
				p->r0 = 0;
				p->g0 = 0;
				p->b0 = 0;
				p->code = 40;

				for (i32 j = 0; j < 4; j++)
				{
					i32 idx = *prog++;
					i16 x = (i16)(sign * ((radius * gCircularFadeShapePoints[2 * idx]) >> 12) + 256);
					i16 y = (i16)(((radius * (gCircularFadeShapePoints[2 * idx + 1] - 120)) >> 12) + 120);
					switch (j)
					{
					case 0: p->x0 = x; p->y0 = y; break;
					case 1: p->x1 = x; p->y1 = y; break;
					case 2: p->x2 = x; p->y2 = y; break;
					case 3: p->x3 = x; p->y3 = y; break;
					}
				}

				CircularFade_DrawQuad(p, xScale, yScale);
			}
		}
	}
}

// @Ok
// @Matching
void Screen_TargetOn(bool value)
{
	G_SCREEN_TARGET = value;
}

#include "compatibility/my_patch.h"

// @Bogus
void patch_screen(void)
{
	// Every global this file touches now points into the exe (see the macro
	// block at the top), so all seven functions are safe to run against the
	// game's own state.
	//
	// Two addresses do not match tools/names.json, they follow CLAUDE.md's
	// 2026-08-27 note: 0x48AA50 is Screen_SetTarget (names.json says
	// Screen_DrawTarget) and the real Screen_DrawTarget is the unnamed
	// sub_48AA90. 0x48AFE0 is Screen_UpdateFades with screen_DrawCircularFade
	// inlined into it.
	PATCH_PUSH_RET(0x0048A820, Screen_SepiaFade);
	PATCH_PUSH_RET(0x0048AA40, Screen_TargetOn);
	PATCH_PUSH_RET(0x0048AA50, Screen_SetTarget);
	PATCH_PUSH_RET(0x0048AA90, Screen_DrawTarget);
	PATCH_PUSH_RET(0x0048AE30, Screen_DrawArrow);
	PATCH_PUSH_RET(0x0048AFB0, Screen_StartCircularFadeIn);
	PATCH_PUSH_RET(0x0048AFE0, Screen_UpdateFades);
}
