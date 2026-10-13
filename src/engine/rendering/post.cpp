#include "engine/rendering/post.h"
#include "engine/rendering/scene_renderer.h"
#include "engine/world/bit.h"
#include "engine/rendering/PCGfx.h"
#include "engine/rendering/m3dinit.h"
#include "game/application/SpideyDX.h"
#include "engine/rendering/render_buffers.h"
#include "game/ui/panel.h"
#include "compatibility/psx_types.h"

#ifndef SPIDEY_STANDALONE
EXPORT i32 gWaterEffect;
#else
extern i32 gWaterEffect;
#endif
EXPORT i32 gPostTimerRelated;
#ifndef SPIDEY_STANDALONE
EXPORT i32 gPostWaterEffect;
#else
extern i32 gPostWaterEffect;
#endif
EXPORT u16 gPostWordRelated;

EXPORT u8 gPaletteProcessingPaused;

EXPORT i32 gPostSpideyLogoRelated;
EXPORT u32 gPostPauseRelated;


// @Ok
// @Matching
void Post_DoPauseDisplayListProcessing(void)
{
	if (gPaletteProcessingPaused)
		Post_SpideyLogo();
}

// @Ok
// @Matching
void Post_DoPausePaletteProcessing(void)
{
	gPaletteProcessingPaused = 1;
	gPostSpideyLogoRelated = 0;
	gPostPauseRelated = G_DB_SKY_COLOR;
}

// @Ok
// @Matching
void Post_PostProcessEffects(void)
{
	if (gWaterEffect)
	{
		gPostTimerRelated = 48 * G_TIMER_RELATED;
		Post_WaterEffect();
	}
	if (gPostWordRelated)
	{
		--gPostWordRelated;
	}
}

// Vertex table for the logo outline at 0x54ECBC, 4 bytes per entry, a signed
// x/y pair. Indexed by the vertex index bytes in gSpideyLogoPolys below. Raw
// game data, unnamed in the maintainer's IDB, so the name is a guess from the
// only use site (this function).
static const i16 * const gSpideyLogoVerts = reinterpret_cast<const i16*>(0x0054ECBC);

// The logo outline itself, at 0x54ED9C. A stream of 40 records, each one a
// corner count byte (4 draws a quad, 3 a triangle, any other value skips the
// record and consumes just that one byte), then a colour selector byte (0
// picks the first colour, anything else the second), then that many vertex
// index bytes. Every byte is read signed by the original. Also unnamed in the
// IDB, name guessed the same way.
static const i8 * const gSpideyLogoPolys = reinterpret_cast<const i8*>(0x0054ED9C);

// @Ok
// Real address 0x46A3E0, 2267 bytes. Not in names.json and no
// tools/functions/*.bin blob for it, found from the exe through IDA xrefs on
// gPostSpideyLogoRelated. The INLINE marker this stub used to carry was wrong:
// the caller (sub_46ACC0, Post_DoPauseDisplayListProcessing above) tail jumps
// to it (jmp sub_46A3E0), so it is a real out of line function. Dropped INLINE.
//
// It draws the flashing SPIDER-MAN logo of the pause screen as flat shaded PSX
// primitives. The outline is stored once, for the left half, and drawn twice,
// mirrored in x about the centre line at 256. Each record is built into the
// display list as a real POLY_F4/POLY_F3 (so the PSX path would pick it up)
// and then also drawn straight away through PCGfx_DrawQPoly2D, twice per
// record with the two windings, which is how the PC port renders both faces.
// A triangle is drawn as a quad with its last corner duplicated, the same
// trick DCModel_CreateFromSModel uses.
//
// The fade colour ramps with gPostSpideyLogoRelated (96 per call, held at 768):
// two channels get the full 255 ramp and one gets a 64 ramp, and which channel
// is the dim one is what the per record selector byte chooses.
//
// Verified against the original by structure rather than by cmpsum, since this
// address is not in names.json and has no tools/functions blob. Built 572
// instructions/2054 bytes against 619/2272. The call sequence is identical and
// in the same order (PCGfx_UseTexture, then printf + 1 draw in one branch and
// printf + 2 draws in the other, because MSVC tail merges one of the four draw
// call sites, in the original too), and the float work matches exactly: 44
// fild, 36 fld, 28 fmul, 8 fdiv, 7 imul in both. The whole difference is the
// fade colour maths, where the original folds the two channel ramps and the
// byte packing into one masked expression (and 0FFFFF00Fh / 0FFFF0000h) and
// this writes the two ramps out plainly, which costs 3 "and" and 6 "shl" fewer
// on our side plus the register allocation that follows from it.
// Also fixes Post_DoPauseDisplayListProcessing above: with INLINE dropped it
// now emits the original's exact "mov al,flag / test / je / jmp logo" tail
// call instead of inlining a stub.
void Post_SpideyLogo(void)
{
	gPostSpideyLogoRelated += 96;
	if (gPostSpideyLogoRelated > 768)
		gPostSpideyLogoRelated = 768;

	if (reinterpret_cast<u8*>(G_PPOLY) + 1920 > G_POLY_BUFFER_END)
		return;

	u32 Fade = static_cast<u32>((90 * gPostSpideyLogoRelated) >> 6);
	if (Fade > 4096)
		Fade = 4096;

	u32 Bright = (255 * Fade) >> 12;
	u32 Dim = (64 * Fade) >> 12;

	u32 ColourA = (Dim << 16) | (Bright << 8) | Bright;
	u32 ColourB = (Bright << 16) | (Bright << 8) | Dim;

	PCGfx_UseTexture(1, DCGfx_BlendingMode_3);

	for (i32 Pass = 0; Pass < 2; Pass++)
	{
		const i8 *pRecord = gSpideyLogoPolys;
		i16 Mirror = static_cast<i16>(Pass != 0 ? 1 : -1);

		for (i32 Left = 40; Left != 0; Left--)
		{
			i32 Corners = *pRecord;
			pRecord++;

			if (Corners == 4)
			{
				i32 Selector = *pRecord;
				pRecord++;

				POLY_F4 *p = reinterpret_cast<POLY_F4*>(G_PPOLY);
				G_PPOLY = reinterpret_cast<u32*>(reinterpret_cast<u8*>(G_PPOLY) + sizeof(POLY_F4));

				u32 Colour = Selector != 0 ? ColourB : ColourA;

				p->tag = 0x5000000;
				p->r0 = static_cast<u8>(Colour);
				p->g0 = static_cast<u8>(Colour >> 8);
				p->b0 = static_cast<u8>(Colour >> 16);
				p->code = 0x2A;

				i32 v = *pRecord;
				pRecord++;
				p->x0 = static_cast<i16>(gSpideyLogoVerts[2 * v] * Mirror + 256);
				p->y0 = gSpideyLogoVerts[2 * v + 1];

				v = *pRecord;
				pRecord++;
				p->x1 = static_cast<i16>(gSpideyLogoVerts[2 * v] * Mirror + 256);
				p->y1 = gSpideyLogoVerts[2 * v + 1];

				v = *pRecord;
				pRecord++;
				p->x2 = static_cast<i16>(gSpideyLogoVerts[2 * v] * Mirror + 256);
				p->y2 = gSpideyLogoVerts[2 * v + 1];

				v = *pRecord;
				pRecord++;
				p->x3 = static_cast<i16>(gSpideyLogoVerts[2 * v] * Mirror + 256);
				p->y3 = gSpideyLogoVerts[2 * v + 1];

				gsub_46CB90(G_RENDER_BUF);

				u32 col = p->b0 | ((p->g0 | ((p->r0 | 0xFFFF8000) << 8)) << 8);

				f32 yScale = G_GAME_RESOLUTION_Y / (f32)G_YRES;
				f32 xScale = G_GAME_RESOLUTION_X / (f32)G_XRES;

				PCGfx_DrawQPoly2D(
						p->x0 * xScale, p->y0 * yScale, 0.0f, 0.0f, col,
						p->x1 * xScale, p->y1 * yScale, 1.0f, 0.0f, col,
						p->x2 * xScale, p->y2 * yScale, 0.0f, 1.0f, col,
						p->x3 * xScale, p->y3 * yScale, 1.0f, 1.0f, col,
						6.0f);

				yScale = G_GAME_RESOLUTION_Y / (f32)G_YRES;
				xScale = G_GAME_RESOLUTION_X / (f32)G_XRES;

				PCGfx_DrawQPoly2D(
						p->x0 * xScale, p->y0 * yScale, 0.0f, 0.0f, col,
						p->x2 * xScale, p->y2 * yScale, 0.0f, 1.0f, col,
						p->x1 * xScale, p->y1 * yScale, 1.0f, 0.0f, col,
						p->x3 * xScale, p->y3 * yScale, 1.0f, 1.0f, col,
						6.0f);
			}
			else if (Corners == 3)
			{
				i32 Selector = *pRecord;
				pRecord++;

				POLY_F3 *p = reinterpret_cast<POLY_F3*>(G_PPOLY);
				G_PPOLY = reinterpret_cast<u32*>(reinterpret_cast<u8*>(G_PPOLY) + sizeof(POLY_F3));

				u32 Colour = Selector != 0 ? ColourB : ColourA;

				p->tag = 0x4000000;
				p->r0 = static_cast<u8>(Colour);
				p->g0 = static_cast<u8>(Colour >> 8);
				p->b0 = static_cast<u8>(Colour >> 16);
				p->code = 0x22;

				i32 v = *pRecord;
				pRecord++;
				p->x0 = static_cast<i16>(gSpideyLogoVerts[2 * v] * Mirror + 256);
				p->y0 = gSpideyLogoVerts[2 * v + 1];

				v = *pRecord;
				pRecord++;
				p->x1 = static_cast<i16>(gSpideyLogoVerts[2 * v] * Mirror + 256);
				p->y1 = gSpideyLogoVerts[2 * v + 1];

				v = *pRecord;
				pRecord++;
				p->x2 = static_cast<i16>(gSpideyLogoVerts[2 * v] * Mirror + 256);
				p->y2 = gSpideyLogoVerts[2 * v + 1];

				gsub_46CB90(G_RENDER_BUF);

				u32 col = p->b0 | ((p->g0 | ((p->r0 | 0xFFFF8000) << 8)) << 8);

				f32 yScale = G_GAME_RESOLUTION_Y / (f32)G_YRES;
				f32 xScale = G_GAME_RESOLUTION_X / (f32)G_XRES;

				PCGfx_DrawQPoly2D(
						p->x0 * xScale, p->y0 * yScale, 0.0f, 0.0f, col,
						p->x1 * xScale, p->y1 * yScale, 1.0f, 0.0f, col,
						p->x2 * xScale, p->y2 * yScale, 0.0f, 1.0f, col,
						p->x2 * xScale, p->y2 * yScale, 1.0f, 1.0f, col,
						6.0f);

				yScale = G_GAME_RESOLUTION_Y / (f32)G_YRES;
				xScale = G_GAME_RESOLUTION_X / (f32)G_XRES;

				PCGfx_DrawQPoly2D(
						p->x0 * xScale, p->y0 * yScale, 0.0f, 0.0f, col,
						p->x2 * xScale, p->y2 * yScale, 0.0f, 1.0f, col,
						p->x1 * xScale, p->y1 * yScale, 1.0f, 0.0f, col,
						p->x1 * xScale, p->y1 * yScale, 1.0f, 1.0f, col,
						6.0f);
			}
		}
	}
}

// @Ok
// @Matching
void Post_UndoPausePaletteProcessing(void)
{
	if (gPaletteProcessingPaused)
		gPaletteProcessingPaused = 0;
}

// @Ok
// @Matching
// @Note: getting the v1 and v2 assignment was insane I had wrong code thank god I checked the size
 void Post_WaterEffect(void)
{
	if (!gPostWaterEffect)
	{
		i32 v1 = G_RCOSSIN_TBL[(gPostTimerRelated / 2) & 0xFFF].sin >> 6;
		i32 v2 = G_RCOSSIN_TBL[(gPostTimerRelated / 2) & 0xFFF].cos >> 6;

		PCGfx_UseTexture(1, DCGfx_BlendingMode_1);
		f32 v3 = G_GAME_RESOLUTION_Y;
		f32 a3 = G_YRES;
		f32 v8 = v3 / a3 * 240.0f;
		f32 a3a = G_GAME_RESOLUTION_X;

		f32 v4 = G_XRES;
		f32 a3b = a3a / v4 * 512.0f;

		PCGfx_DrawQuad2D(
			0.0,
			0.0,
			a3b,
			v8,
			0.0,
			0.0,
			1.0,
			1.0,
			(my_abs(v1) + 64) << 8 | (my_abs(v2) + 64) << 16 | 0x30000032,
			6.0,
			0);
	}
}
