#include <cstdlib>
#include "engine/rendering/scene_renderer.h"

#include "compatibility/psx_types.h"
#include "game/ui/panel.h"
#include "game/ui/pshell.h"
#include "engine/assets/spool.h"
#include "game/objects/level_one_bomb.h"
#include "game/player/spidey.h"
#include "engine/rendering/render_buffers.h"
#include "engine/rendering/ps2funcs.h"
#include "engine/rendering/PCGfx.h"
#include "engine/rendering/m3dinit.h"
#include "game/application/SpideyDX.h"
#include "engine/world/trig.h"
#include "engine/assets/reloc.h"
#include "engine/world/bit.h"
#include "core/utils.h"
#include "engine/audio/ps2lowsfx.h"
#include "game/ui/mess.h"
#include "game/actors/rhino.h"
#include "game/actors/venom.h"
#include "game/actors/carnage.h"
#include "game/actors/docock.h"
#include "game/actors/scorpion.h"
#include "game/actors/mysterio.h"

#include "compatibility/validate.h"

// The HUD state block at 0x0060F654..0x0060F78F. Every address below is the
// operand of the store or load named in its comment. All of these stay file
// local, nothing outside panel.cpp reads them today.

// Panel_CreateHealthBar (0x463610) writes the boss item type at 0x60F654
// ("mov [60F654h],eax") and the five boss textures at 0x60F658..0x60F668
// (one store per Spool_FindTextureEntry result), then the two health
// snapshots at 0x60F744 (first bar) and 0x60F748 (second bar).
#ifndef SPIDEY_STANDALONE
EXPORT i32 gHealthBarItemType;
#else
extern i32 gHealthBarItemType;
#endif
//#define G_HEALTH_BAR_ITEM_TYPE (gHealthBarItemType)
#define G_HEALTH_BAR_ITEM_TYPE (*reinterpret_cast<i32*>(0x0060F654))

#ifndef SPIDEY_STANDALONE
EXPORT i32 gHealthBarRelated;
#else
extern i32 gHealthBarRelated;
#endif
//#define G_HEALTH_BAR_RELATED (gHealthBarRelated)
#define G_HEALTH_BAR_RELATED (*reinterpret_cast<i32*>(0x0060F744))

#ifndef SPIDEY_STANDALONE
EXPORT i32 gHealthBarRelatedTwo;
#else
extern i32 gHealthBarRelatedTwo;
#endif
//#define G_HEALTH_BAR_RELATED_TWO (gHealthBarRelatedTwo)
#define G_HEALTH_BAR_RELATED_TWO (*reinterpret_cast<i32*>(0x0060F748))

#ifndef SPIDEY_STANDALONE
EXPORT Texture* gHealthBarTextures[5];
#else
extern Texture* gHealthBarTextures[5];
#endif
//#define G_HEALTH_BAR_TEXTURES (gHealthBarTextures)
#define G_HEALTH_BAR_TEXTURES (reinterpret_cast<Texture**>(0x0060F658))

// Panel_CreateCompass (0x463800) writes the three components at
// 0x60F708/0x60F70C/0x60F710 and sets the status byte at 0x60F77C;
// Panel_DestroyCompass (0x463850) clears the same byte.
// @FIXME
#ifndef SPIDEY_STANDALONE
EXPORT CVector gCompassPosition;
#else
extern CVector gCompassPosition;
#endif
//#define G_COMPASS_POSITION (gCompassPosition)
#define G_COMPASS_POSITION (*reinterpret_cast<CVector*>(0x0060F708))

#ifndef SPIDEY_STANDALONE
EXPORT u8 gCompassStatus;
#else
extern u8 gCompassStatus;
#endif
//#define G_COMPASS_STATUS (gCompassStatus)
#define G_COMPASS_STATUS (*reinterpret_cast<u8*>(0x0060F77C))

// Panel_Init (0x461CA0) hands these three addresses to Spool_AnimAccess in
// this order: 0x60F758 "Sp", 0x60F75C "Compass", 0x60F760 "Webcart".
#ifndef SPIDEY_STANDALONE
EXPORT SAnimFrame* gAnimSp;
#else
extern SAnimFrame* gAnimSp;
#endif
//#define G_ANIM_SP (gAnimSp)
#define G_ANIM_SP (*reinterpret_cast<SAnimFrame**>(0x0060F758))

#ifndef SPIDEY_STANDALONE
EXPORT SAnimFrame* gAnimCompass;
#else
extern SAnimFrame* gAnimCompass;
#endif
//#define G_ANIM_COMPASS (gAnimCompass)
#define G_ANIM_COMPASS (*reinterpret_cast<SAnimFrame**>(0x0060F75C))

#ifndef SPIDEY_STANDALONE
EXPORT SAnimFrame* gAnimWebcart;
#else
extern SAnimFrame* gAnimWebcart;
#endif
//#define G_ANIM_WEBCART (gAnimWebcart)
#define G_ANIM_WEBCART (*reinterpret_cast<SAnimFrame**>(0x0060F760))

// Panel_DestroyHealthBar (0x4637F0) zeroes exactly these two
// ("mov [60F788h],eax; mov [60F78Ch],eax").
#ifndef SPIDEY_STANDALONE
EXPORT CBody* gHealthBarOne;
#else
extern CBody* gHealthBarOne;
#endif
//#define G_HEALTH_BAR_ONE (gHealthBarOne)
#define G_HEALTH_BAR_ONE (*reinterpret_cast<CBody**>(0x0060F788))

#ifndef SPIDEY_STANDALONE
EXPORT CBody* gHealthBarTwo;
#else
extern CBody* gHealthBarTwo;
#endif
//#define G_HEALTH_BAR_TWO (gHealthBarTwo)
#define G_HEALTH_BAR_TWO (*reinterpret_cast<CBody**>(0x0060F78C))


// real translation (0x4631c0, 1091 bytes). Two near-duplicate blocks
// selected by (a4 == 1): the vertex1/vertex2 argument slots swap which
// POLY_GT4 corner feeds them (colors/coords keep their own alpha,
// 0xDF for corners 0/1, 0x60 for corners 2/3, tied to the source corner
// not the argument slot, confirmed from the color-pack read order in each
// block). cmpsum: 296 mnemonic diffs, diverges right at the prologue, at
// the print_if_false call. Likely the same known repo-wide defect noted in
// CLAUDE.md ("print_if_false inlining makes old @Ok tags go stale"):
// export.h's print_if_false is static so our build always inlines it,
// while the original calls it out-of-line at 0x4015B0. This looks like the
// shared root cause behind this function and both DCPanel_DrawTexturedPoly
// overloads above (all three call print_if_false and are all stuck at a
// small stable diff count); none of the three can byte-match until that
// header issue is fixed. 1 attempt this session, not iterated further
// @Ok
// Functional: draw gouraud quad, logic verified against Hex-Rays at 0x4631c0.
void DCDrawGouraudPoly(f32 zOffset, POLY_GT4 *poly, Texture *tex, i32 a4)
{
	print_if_false(tex != 0, "no texture for draw gouraud poly.");

	PCGfx_UseTexture(tex->clut, DCGfx_BlendingMode_2);

	f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 y3 = poly->y3 * scaleY;
	f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
	f32 x3 = poly->x3 * scaleX;
	f32 y2 = poly->y2 * scaleY;
	f32 x2 = poly->x2 * scaleX;
	f32 y1 = poly->y1 * scaleY;
	f32 x1 = poly->x1 * scaleX;
	f32 y0 = poly->y0 * scaleY;
	f32 x0 = poly->x0 * scaleX;

	u32 color0 = 0xDF000000 | (poly->r0 << 16) | (poly->g0 << 8) | poly->b0;
	u32 color1 = 0xDF000000 | (poly->r1 << 16) | (poly->g1 << 8) | poly->b1;
	u32 color2 = 0x60000000 | (poly->r2 << 16) | (poly->g2 << 8) | poly->b2;
	u32 color3 = 0x60000000 | (poly->r3 << 16) | (poly->g3 << 8) | poly->b3;

	if (a4 == 1)
	{
		PCGfx_DrawQPoly2D(
				x0, y0, 0.0f, 0.0f, color0,
				x2, y2, 1.0f, 0.0f, color2,
				x1, y1, 0.0f, 1.0f, color1,
				x3, y3, 1.0f, 1.0f, color3,
				zOffset);
	}
	else
	{
		PCGfx_DrawQPoly2D(
				x0, y0, 0.0f, 0.0f, color0,
				x1, y1, 1.0f, 0.0f, color1,
				x2, y2, 0.0f, 1.0f, color2,
				x3, y3, 1.0f, 1.0f, color3,
				zOffset);
	}
}

// packed PS2-style 0x00BBGGRR colors need swapping to 0xFFRRGGBB for the PC renderer.
// @Bogus
static u32 DCGouraud_SwapColor(u32 c)
{
	return 0xFF000000 | ((c & 0xFF) << 16) | (c & 0xFF00) | ((c >> 16) & 0xFF);
}

// @Ok
// real translation, 0x00462FB0, 9 args (zOffset,x,y,w,h,c0,c1,c2,c3). The
// old version of this function had a bogus 10th i32 parameter and used it
// (plus two of the color args) in the wrong vertex slots: it read c4/c2/c3/c1
// where the original reads c0/c1/c2/c3 in plain corner order. Confirmed from
// Hex-Rays at 0x462fb0 (only caller of this address is the not-yet-written
// Panel_DisplayHealthBar/Panel_Display, so no existing caller depended on
// the old, wrong param list). Functional only, not chasing byte match this
// session.
void DCDrawGouraudPoly(f32 zOffset, i32 x, i32 y, i32 w, i32 h, u32 c0, u32 c1, u32 c2, u32 c3)
{
	PCGfx_UseTexture(1, DCGfx_BlendingMode_1);

	f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 yEnd = (y + h) * scaleY;
	f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
	f32 xEnd = (x + w) * scaleX;
	f32 xScaled = x * scaleX;
	f32 yScaled = y * scaleY;

	PCGfx_DrawQPoly2D(
			xScaled, yScaled, 0.0f, 0.0f, DCGouraud_SwapColor(c0),
			xEnd, yScaled, 1.0f, 0.0f, DCGouraud_SwapColor(c1),
			xScaled, yEnd, 0.0f, 1.0f, DCGouraud_SwapColor(c2),
			xEnd, yEnd, 1.0f, 1.0f, DCGouraud_SwapColor(c3),
			zOffset);
}

// @Ok
// @Matching
void DCPanel_DrawFlatShadedPoly(f32 zOffset, i32 x, i32 y, i32 w, i32 h, u8 r, u8 g, u8 b, i32, i32 blendMode)
{
	u8 alpha = 0xFF;

	if (blendMode == 1)
	{
		PCGfx_UseTexture(blendMode, (DCGfx_BlendingMode)blendMode);
		alpha = 0x7F;
	}
	else if (blendMode == 2)
	{
		PCGfx_UseTexture(1, DCGfx_BlendingMode_1);
		alpha = 0x7F;
	}
	else if (blendMode == 3)
	{
		PCGfx_UseTexture(1, DCGfx_BlendingMode_1);
		alpha = 0xDC;
	}
	else
	{
		PCGfx_UseTexture(1, DCGfx_BlendingMode_0);
	}

	f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 hScaled = h * scaleY;
	f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
	f32 wScaled = w * scaleX;
	f32 yScaled = y * scaleY;
	f32 xScaled = x * scaleX;

	u32 color = (alpha << 24) | (r << 16) | (g << 8) | b;

	PCGfx_DrawQuad2D(
			xScaled,
			yScaled,
			wScaled,
			hScaled,
			0.0f,
			0.0f,
			1.0f,
			1.0f,
			color,
			zOffset,
			false);
}

// @Ok
// real translation (0x4626a0, 640 bytes). Found a genuine functional bug
// while cross-checking against Hex-Rays and the raw disasm of the only
// three call sites (shell.cpp gsub_498240, tagged @Ok @Matching, e.g.
// `DCPanel_DrawTexturedPoly(1.0f, pPoly, gAnimTable[23], x - 85, y - 11,
// 20, 12, G_SORT, 0)`): the trailing i32 params are, in real stack order,
// x, y, w, h, an unused slot, then flags. The old source here had them as
// x, y, a6(unused), h, w, flags, so the geometry branch read the unused
// slot (G_SORT in the shell.cpp calls) as the width and never looked at
// the real width argument. Confirmed from the raw asm at 0x4626a0: in the
// true branch, edx = arg_14 (added to x for x1/x3) and ebx = arg_18 (added
// to y for y2), and arg_14/arg_18 are exactly the "w"/"h" slots that match
// frame->Width/frame->Height in the fallback (else) branch below. Since
// callers pass arguments positionally, only the declaration order needed
// to change here; shell.cpp's already-matching call site is unaffected.
// Functional only, not chasing byte match this session.
void DCPanel_DrawTexturedPoly(f32 zOffset, POLY_FT4 *poly, SAnimFrame const *frame, i32 x, i32 y, i32 w, i32 h, i32 a8, u32 flags)
{
	print_if_false(frame != 0, "NULL pFrame for draw texture poly.");

	if (w && h)
	{
		poly->x0 = (i16)x;
		poly->x2 = (i16)x;
		poly->y0 = (i16)y;
		poly->y1 = (i16)y;
		poly->x1 = (i16)(w + x);
		poly->x3 = (i16)(w + x);
		poly->y2 = (i16)(h + y);
		poly->y3 = (i16)(h + y);
	}
	else
	{
		poly->x0 = (i16)x;
		poly->y0 = (i16)y;
		poly->x1 = (i16)(x + frame->Width);
		poly->y1 = (i16)y;
		poly->x2 = (i16)x;
		poly->y2 = (i16)(y + frame->Height);
		poly->x3 = (i16)(x + frame->Width);
		poly->y3 = (i16)(y + frame->Height);
	}

	u32 color = flags;
	if (!flags)
	{
		color = 0xFF000000 | (poly->r0 << 16) | (poly->g0 << 8) | poly->b0;
	}

	Texture *tex = frame->pTexture;
	PCGfx_UseTexture(tex->clut, DCGfx_BlendingMode_0);

	f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 y3 = poly->y3 * scaleY;
	f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
	f32 x3 = poly->x3 * scaleX;
	f32 y2 = poly->y2 * scaleY;
	f32 x2 = poly->x2 * scaleX;
	f32 y1 = poly->y1 * scaleY;
	f32 x1 = poly->x1 * scaleX;
	f32 y0 = poly->y0 * scaleY;
	f32 x0 = poly->x0 * scaleX;

	PCGfx_DrawQPoly2D(
			x0, y0, 0.01f, 0.01f, color,
			x1, y1, 1.0f, 0.01f, color,
			x2, y2, 0.01f, 1.0f, color,
			x3, y3, 1.0f, 1.0f, color,
			zOffset);
}

// @Ok
// real translation (0x4624a0, 506 bytes). Logic checked against Hex-Rays:
// clut read from frame->pTexture->clut, color falls back to poly->r0/g0/b0
// when flags is 0, all 8 corners scaled the same way as the other
// DCPanel_DrawTexturedPoly overloads. The remaining diffs seen in earlier
// sessions were register/scheduling residue only (float scale step
// ordering), not a functional problem. Functional only, not chasing byte
// match this session.
void DCPanel_DrawTexturedPoly(f32 zOffset, POLY_FT4 *poly, SAnimFrame const *frame, u32 flags)
{
	print_if_false(frame != 0, "NULL pFrame for draw texture poly.");

	Texture *tex = frame->pTexture;
	PCGfx_UseTexture(tex->clut, DCGfx_BlendingMode_0);

	u32 color = flags;
	if (!flags)
	{
		color = 0xFF000000 | (poly->r0 << 16) | (poly->g0 << 8) | poly->b0;
	}

	f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 y3 = poly->y3 * scaleY;
	f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
	f32 x3 = poly->x3 * scaleX;
	f32 y2 = poly->y2 * scaleY;
	f32 x2 = poly->x2 * scaleX;
	f32 y1 = poly->y1 * scaleY;
	f32 x1 = poly->x1 * scaleX;
	f32 y0 = poly->y0 * scaleY;
	f32 x0 = poly->x0 * scaleX;

	PCGfx_DrawQPoly2D(
			x0, y0, 0.01f, 0.01f, color,
			x1, y1, 1.0f, 0.01f, color,
			x2, y2, 0.01f, 1.0f, color,
			x3, y3, 1.0f, 1.0f, color,
			zOffset);
}

// @Ok
// Functional: draw textured quad, logic verified against Hex-Rays at 0x462930.
// The 29 mnemonic diffs are register allocation / scheduling residue.
void DCPanel_DrawTexturedPoly(f32 zOffset, POLY_FT4 *poly, Texture const *tex, u32 flags)
{
	print_if_false(tex != 0, "no texture for draw textured poly.");

	PCGfx_UseTexture(tex->clut, DCGfx_BlendingMode_0);

	u32 color = flags;
	if (!flags)
	{
		color = 0xFF000000 | (poly->r0 << 16) | (poly->g0 << 8) | poly->b0;
	}

	f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 y3 = poly->y3 * scaleY;
	f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
	f32 x3 = poly->x3 * scaleX;
	f32 y2 = poly->y2 * scaleY;
	f32 x2 = poly->x2 * scaleX;
	f32 y1 = poly->y1 * scaleY;
	f32 x1 = poly->x1 * scaleX;
	f32 y0 = poly->y0 * scaleY;
	f32 x0 = poly->x0 * scaleX;

	PCGfx_DrawQPoly2D(
			x0, y0, 0.01f, 0.01f, color,
			x1, y1, 1.0f, 0.01f, color,
			x2, y2, 0.01f, 1.0f, color,
			x3, y3, 1.0f, 1.0f, color,
			zOffset);
}

// unnamed globals used only by gsub_46CB90. Not in idb_globals.txt near neighbours,
// tentative names based on how they gate the debug print below.
static u8 * const gDebugPrintDisabled = (u8*)0x006B4CB8;
static u8 * const gDebugPrintEnabled = (u8*)0x0054F038;

static char * const gDebugPrintBuf = (char*)0x006109E0;

// gsub_4015B0 is declared in panel.h and defined further down, so it is not
// visible for same-TU inlining at this call site (matches the original,
// which has a real "call" instruction here, not an inlined body).

// unnamed helper at 0x46CB90, argument is gRenderBuf (idb_globals.txt: 0x0056EB54, exact type unknown).
// Not runtime-hooked this session, so a printf placeholder instead of a forward-to-original
// for gsub_4015B0. cmpsum: 0 mnemonic diffs.
// @Ok
// @Matching
void gsub_46CB90(void* fmt, ...)
{
	if (*gDebugPrintDisabled)
		return;

	if (!*gDebugPrintEnabled)
		return;

	va_list args;
	va_start(args, fmt);
	vsprintf(gDebugPrintBuf, (char*)fmt, args);

	gsub_4015B0(gDebugPrintBuf);
}

// unnamed helper at 0x4015B0 (names.json calls it print_if_false, but the
// export.h print_if_false has a different arg count and is static/inlined
// away in our build). Original bytes are a single `ret` (1 byte function,
// tools/functions/4199856.bin), so the body is empty in this build
// configuration. cmpsum: 0 mnemonic diffs.
// @Ok
// @Matching
EXPORT void gsub_4015B0(void*)
{
}

// @Ok
void Panel_CreateCompass(CVector * pVec)
{
	G_COMPASS_POSITION = *pVec >> 12;
	G_COMPASS_STATUS = 1;
}

// screen Y offset for the HUD (runtime value, 0 at boot). Moved up from its
// original spot right before Panel_DisplayTimer so Panel_DisplayHealthBar
// (defined earlier in the file) can use it too.
static i32 * const gPanelScreenY = (i32*)0x0060F76C;

// Master HUD switch. pshell.cpp already reaches this same address through a
// file-local macro under this same name; PShell_EndTrainingInit saves its value
// and clears it, PShell_EndTrainingUpdate puts it back when the player leaves
// the end-of-training results screen. Panel_Display below shows what it really
// controls: 0 means draw no HUD at all (only the compass, and only when
// gSynthInputScriptFlag asks for it). One address defined twice in two files is
// not great, it should become one definition in a shared header the next time
// panel.cpp and pshell.cpp are touched together.
// gScreenModeFlag now lives in pshell.h as G_SCREEN_MODE_FLAG (shared with pshell.cpp).

// Same global spidey.cpp already declares under this name (0x0060F770). It is
// written by CPlayer::CPlayer and by SynthesizeAnalogueInput's script opcode
// 18. Panel_Display below is the only reader, and it uses it as "keep the
// compass on screen even while the rest of the HUD is hidden", which is the
// first real evidence of what the opcode is for.
static u8 * const gSynthInputScriptFlag = (u8*)0x0060F770;

// Two ASCII digits, plus the NUL that follows them at 0x0054EA92, holding the
// web cartridge count drawn next to the webcart icon. The shipped .data
// contents are "00". Only Panel_Display touches it (xrefs_to 0x0054EA90).
static char * const gWebCartDigits = (char*)0x0054EA90;

// Defined further down next to Panel_DisplayHealthBar, which is where it was
// first factored out; Panel_Display needs it too and comes earlier in the file.
static void PanelHB_DrawIconOverlay(POLY_FT4 *p, Texture *tex, DCGfx_BlendingMode blend, f32 zOffset);

// Five of the sprites Panel_Display draws use the exact same three step
// sequence in the original, with only the frame, position and size changing:
// take a POLY_FT4 out of the poly buffer for the frame's texture, stretch it
// over the given screen rect, then blit it. Blend mode 0 and zOffset 1.0 at all
// five sites. Factored into one helper here; the original repeats it inline.
// @Ok
static void PanelDisp_DrawIcon(SAnimFrame *pFrame, i32 x, i32 y, i32 w, i32 h)
{
	POLY_FT4 *p = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(pFrame->pTexture, 0));
	if (p == 0)
		return;

	Panel_SetStretchedScreenCoords(x, y, p, pFrame, w, h);
	print_if_false(pFrame->pTexture != 0, "No spidey health bar texture.");
	PanelHB_DrawIconOverlay(p, pFrame->pTexture, DCGfx_BlendingMode_0, 1.0f);
}


// Real translation, 0x004658C0 (4555 bytes). tools/names.json has NO entry for
// this address; it was identified via xrefs_to on Panel_DisplayCompass
// (0x463860) and Panel_DisplayHealthBar (0x464270) - this function is their
// only caller, and the "Poly buffer overflowed before Panel_Display" string it
// pushes at 0x4658D2 names it outright. Decompiled with Hex-Rays and checked
// line by line against the raw disassembly.
//
// What it draws, top to bottom: it first decays the bomb countdown
// (gBombAIRelated) by gBombRelated per elapsed tick, then runs
// Panel_DisplayTimer. If the HUD is switched off (G_SCREEN_MODE_FLAG == 0) or the
// player is driving something (field_E18/field_1AC, the same "in a mech"
// suppression pair Panel_DisplayTimer already uses), it stops there and only
// draws the compass, and only if gSynthInputScriptFlag asked for it. Otherwise
// it draws the compass plus the whole player HUD: the web cartridge icon with
// its 1-2 digit count, a health icon (costume specific), two SP pips, an SP end
// cap, the two web meter caps, the health bar (solid damage overlay plus a
// gouraud highlight that pulses when health is low), and the vertical web meter.
//
// Callees, all already decompiled: sub_461D00/463860/464270 =
// Panel_DisplayTimer/Panel_DisplayCompass/Panel_DisplayHealthBar (this file),
// sub_462BB0 = Panel_DrawTexturedPoly(Texture*,int), sub_462C30 =
// Panel_SetStretchedScreenCoords(...,SAnimFrame*,...), sub_462D60 =
// DCPanel_DrawFlatShadedPoly, sub_462FB0 = the 9-arg DCDrawGouraudPoly,
// sub_506440/507910 = PCGfx_UseTexture/PCGfx_DrawQPoly2D (PCGfx.cpp),
// sub_458610/458620/458630/458640/458670/458700 = Mess_SetTextJustify/
// Mess_SetScale/Mess_SetSort/Mess_SetRGB/Mess_SetRGBBottom/Mess_DrawText
// (mess.h), nullsub_1 (0x4015B0) = print_if_false.
//
// Globals resolved: dword_6A9038 = MechList (the player, spidey.h),
// dword_6B4CA8 = gTimerRelated (bit.h), byte_60F772 = gBombDieRelatedTwo,
// dword_60F774 = gBombAIRelated, dword_60F778 = gBombDieTimerRelated,
// dword_54E8D4 = gBombRelated (all l1a3bomb.h), dword_56FB04/dword_5FCD1C =
// pPoly/PolyBufferEnd (db.h), dword_60F76C = gPanelScreenY (above),
// dword_60F758/60F760 = G_ANIM_SP/G_ANIM_WEBCART (top of this file),
// dword_60F750/60F754 = gSpideyAnim/gSpideyAnimTwo (spidey.cpp, see the extern
// block below), word_610C48 = rcossin_tbl (ps2funcs.h, read with the same
// "2*i" u16 stride Panel_DisplayCompass already uses), dword_568158/568154/
// 628614/61B5FC = gGameResolutionY/gGameResolutionX/Yres/Xres.
//
// dword_54E9B0..54E9FC are twenty read-only .data ints, each referenced exactly
// once and only from here (xrefs_to), holding a small x/y/w/h nudge per icon.
// They are not runtime state, so they are folded into the literals below; the
// shipped values, read off the binary with IDA, are 2/0/0/0, 1/0/-1/-1,
// 1/0/-1/0, 5/0/-2/0, 5/0/-2/0. Same treatment Panel_DisplayHealthBar already
// gives its own 0x54E910..54E99C block.
//
// Two original defects reproduced, not fixed: the bomb decay block dereferences
// MechList without the null check the line above it just made, and the poly
// buffer check at the top only prints, it does not stop the function from
// writing past the end.
//
// One fidelity note for later byte-matching work: every call site of
// DCDrawGouraudPoly (0x462FB0) in the original, here and in
// Panel_DisplayHealthBar, pushes TEN arguments (add esp, 28h) while the
// function body only ever reads nine (arg_0..arg_20). So the real prototype has
// a tenth, unused parameter, always passed as 0. panel.h declares nine; left
// alone here since it changes nothing functionally, but it will matter when
// somebody chases the bytes.
// @Ok
void Panel_Display(void)
{
	print_if_false(reinterpret_cast<u8*>(G_PPOLY) <= G_POLY_BUFFER_END, "Poly buffer overflowed before Panel_Display");

	i32 inMech = 0;
	if (G_MECHLIST_PLAYER != 0 && (G_MECHLIST_PLAYER->field_E18 != 0 || G_MECHLIST_PLAYER->mSynthesizedInputActive != 0))
		inMech = 1;

	if (gBombDieRelatedTwo != 0)
	{
		u32 decay;
		if (G_MECHLIST_PLAYER->field_E18 != 0 || G_MECHLIST_PLAYER->mSynthesizedInputActive != 0)
			decay = 0;
		else
			decay = (static_cast<u32>(G_TIMER_RELATED - gBombDieTimerRelated) * gBombRelated) >> 12;

		if (gBombAIRelated <= decay)
			gBombAIRelated = 0;
		else
			gBombAIRelated -= decay;
	}
	gBombDieTimerRelated = G_TIMER_RELATED;

	Panel_DisplayTimer();

	if (G_SCREEN_MODE_FLAG == 0 || inMech != 0)
	{
		if (*gSynthInputScriptFlag != 0)
			Panel_DisplayCompass();
		return;
	}

	Panel_DisplayCompass();

	POLY_FT4 *pWebcart = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(G_ANIM_WEBCART->pTexture, 0));
	if (pWebcart != 0)
	{
		Panel_SetStretchedScreenCoords(80, *gPanelScreenY + 58, pWebcart, G_ANIM_WEBCART, 20, 16);

		u8 pulse;
		if (G_MECHLIST_PLAYER->field_5E8 != 0)
			pulse = static_cast<u8>((abs(G_RCOSSIN_TBL[(G_TIMER_RELATED << 5) & 0xFFF].sin) << 7) >> 12);
		else
			pulse = 0x80;
		pWebcart->g0 = pulse;
		pWebcart->b0 = pulse;

		i32 cartridges = G_MECHLIST_PLAYER->field_5D8;
		if (cartridges >= 10)
		{
			gWebCartDigits[0] = '1';
			gWebCartDigits[1] = static_cast<char>(cartridges + 38);
		}
		else
		{
			gWebCartDigits[0] = '0';
			gWebCartDigits[1] = static_cast<char>(cartridges + 48);
		}

		Mess_SetScale(256);
		Mess_SetTextJustify(1);
		Mess_SetRGB(128, 128, 128, 0);
		Mess_SetRGBBottom(69, 60, 107);
		Mess_SetSort(4093);
		Mess_DrawText(95, *gPanelScreenY + 56, gWebCartDigits, 0, 0x1000);
		Mess_SetSort(0);

		i32 age = G_TIMER_RELATED - G_MECHLIST_PLAYER->field_5DC;
		if (age < 32)
		{
			i16 x0 = pWebcart->x0;
			i16 y0 = pWebcart->y0;
			i16 x1 = pWebcart->x1;
			i16 y2 = pWebcart->y2;
			u8 flare = static_cast<u8>(255 - 4 * age);
			pWebcart->r0 = flare;
			pWebcart->g0 = flare;
			pWebcart->b0 = flare;

			i32 grow = (32 - age) << 7;
			i32 dx = (grow * (x1 - x0)) >> 12;
			i32 dy = (grow * (y2 - y0)) >> 12;

			pWebcart->x2 -= static_cast<i16>(dx);
			pWebcart->x3 += static_cast<i16>(dx);
			pWebcart->x1 = static_cast<i16>(x1 + dx);
			pWebcart->x0 = static_cast<i16>(x0 - dx);

			pWebcart->y1 -= static_cast<i16>(dy);
			pWebcart->y3 += static_cast<i16>(dy);
			pWebcart->y2 = static_cast<i16>(y2 + dy);
			pWebcart->y0 = static_cast<i16>(y0 - dy);

			pWebcart->v2 = static_cast<u8>(pWebcart->v2 - 1);
			pWebcart->v3 = static_cast<u8>(pWebcart->v3 - 1);
		}

		print_if_false(G_ANIM_WEBCART->pTexture != 0, "No WebCartAnim texture.");
		PanelHB_DrawIconOverlay(pWebcart, G_ANIM_WEBCART->pTexture, DCGfx_BlendingMode_0, 6.0f);
	}

	SAnimFrame *pHealthIcon = gSpideyAnimTwo;
	if (pHealthIcon == 0)
	{
		pHealthIcon = gSpideyAnim;
		if (pHealthIcon == 0)
			pHealthIcon = G_ANIM_SP;
	}
	PanelDisp_DrawIcon(pHealthIcon, 67, *gPanelScreenY + 45, 28, 30);

	for (i32 pip = 0; pip < 50; pip += 25)
		PanelDisp_DrawIcon(&G_ANIM_SP[3], pip + 85, *gPanelScreenY + 36, 15, 15);

	PanelDisp_DrawIcon(&G_ANIM_SP[4], 132, *gPanelScreenY + 36, 11, 16);
	PanelDisp_DrawIcon(&G_ANIM_SP[1], 53, *gPanelScreenY + 59, 14, 16);
	PanelDisp_DrawIcon(&G_ANIM_SP[2], 53, *gPanelScreenY + 73, 14, 12);

	if (G_MECHLIST_PLAYER->field_5E9 != 0)
	{
		i32 pulseFrac = ((G_MECHLIST_PLAYER->mMaxHealth - G_MECHLIST_PLAYER->field_5EC) << 7) / G_MECHLIST_PLAYER->mMaxHealth;
		i32 shade = 255 - 255 * pulseFrac / 128;
		DCDrawGouraudPoly(2.0f, 58, *gPanelScreenY + 25, 61 - 61 * pulseFrac / 128, 6,
				0xFF0000, 0xFF0000 | (shade << 8) | shade,
				0xFF0000, 0xFF0000 | (shade << 8) | shade);
	}

	i32 damageFrac = ((G_MECHLIST_PLAYER->mMaxHealth - G_MECHLIST_PLAYER->mHealth) << 7) / G_MECHLIST_PLAYER->mMaxHealth;
	i32 damageWidth = 61 * damageFrac / 128;
	if (damageWidth != 0)
		DCPanel_DrawFlatShadedPoly(3.0f, 119 - damageWidth, *gPanelScreenY + 25, damageWidth, 6, 0, 0, 0, 0, 0);

	i32 hitAge = G_TIMER_RELATED - G_MECHLIST_PLAYER->field_5E0;
	i32 flash = (hitAge >= 32) ? 0 : 255 - 8 * hitAge;

	if (damageWidth <= 30)
		DCDrawGouraudPoly(4.0f, 88, *gPanelScreenY + 25, 30, 6,
				(flash << 16) | 0xFFFF, (flash << 16) | 0xFF00 | flash,
				(flash << 16) | 0xFFFF, (flash << 16) | 0xFF00 | flash);

	if (damageFrac > 76)
	{
		i32 wave = G_RCOSSIN_TBL[(G_TIMER_RELATED * (175 * (damageFrac - 76) / 52 + 25)) & 0xFFF].sin;
		u32 beat = static_cast<u32>(((wave * wave) | 0xFF00) >> 8);
		DCDrawGouraudPoly(4.0f, 58, *gPanelScreenY + 25, 31, 6, beat, beat, beat, beat);
	}
	else
	{
		DCDrawGouraudPoly(4.0f, 58, *gPanelScreenY + 25, 31, 6,
				(flash << 16) | (flash << 8) | 0xFF, (flash << 16) | 0xFFFF,
				(flash << 16) | (flash << 8) | 0xFF, (flash << 16) | 0xFFFF);
	}

	i32 webEmpty = 26 * (((4096 - G_MECHLIST_PLAYER->mWebbing) << 7) / 4096) / 128;
	if (webEmpty != 0)
		DCPanel_DrawFlatShadedPoly(3.0f, 31, *gPanelScreenY - webEmpty + 67, 11, webEmpty, 0, 0, 0, 0, 0);

	if (G_MECHLIST_PLAYER->field_5E8 != 0)
		DCPanel_DrawFlatShadedPoly(4.0f, 31, *gPanelScreenY + 41, 11, 26, 255, 0, 0, 0, 0);
	else
		DCPanel_DrawFlatShadedPoly(4.0f, 31, *gPanelScreenY + 41, 11, 26, 64, 64, 160, 0, 0);

	Panel_DisplayHealthBar();
}

// player-relative reference point (CVector) and rotation matrix (MATRIX)
// used to turn world positions into a local-space direction - same
// addresses and same precedent comment as spidey.cpp's stru_56F1B4/
// stru_56F224 (CPlayer::UpdateSpideySenseList and others); repo convention
// allows duplicating static address globals across files.
static CVector * const stru_56F1B4 = (CVector*)0x56F1B4;
static MATRIX * const stru_56F224 = (MATRIX*)0x56F224;

// compass "flash" countdown: nonzero right after Panel_CreateCompass makes
// the needle pulse brighter for a few frames, decaying by 2 per call here,
// clamped to 0. Also touched by two other not-yet-decompiled functions
// (0x00407840, 0x004E9B00) elsewhere in the binary; tentative name from
// this function's own usage only.
static i32 * const gCompassFlashTimer = (i32*)0x0060F768;

// Shared draw sequence for the two needle-half POLY_FT4 quads in
// Panel_DisplayCompass below (originally repeated inline at 0x463f00ish and
// 0x464200ish: PCGfx_UseTexture then a manual scale+color-pack
// PCGfx_DrawQPoly2D, same idiom as PanelHB_DrawIconOverlay above, but with
// the needle's inset 0.05..0.95 UV range instead of a full 0..1 quad and a
// fixed zOffset of 0.0).
// @Ok
static void PanelCompass_DrawNeedleHalf(POLY_FT4 *p, Texture *tex)
{
	PCGfx_UseTexture(tex->clut, DCGfx_BlendingMode_0);

	f32 yScale = G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 xScale = G_GAME_RESOLUTION_X / (f32)G_XRES;
	u32 col = p->b0 | ((p->g0 | ((p->r0 | 0xFFFFFF00) << 8)) << 8);

	// the second half is the mirror image: Panel_DisplayCompass swaps its
	// u0/u1 bytes and the original's second call (0x464257) uses 0.95 on the
	// left and 0.05 on the right
	f32 uLeft = (p->u0 > p->u1) ? 0.95f : 0.05f;
	f32 uRight = (p->u0 > p->u1) ? 0.05f : 0.95f;

	PCGfx_DrawQPoly2D(
			p->x0 * xScale, p->y0 * yScale, uLeft, 0.0f, col,
			p->x1 * xScale, p->y1 * yScale, uRight, 0.0f, col,
			p->x2 * xScale, p->y2 * yScale, uLeft, 1.0f, col,
			p->x3 * xScale, p->y3 * yScale, uRight, 1.0f, col,
			0.0f);
}

// Real translation, 0x00463860. Decompiled via Hex-Rays and cross-checked
// against the raw disasm. As with Panel_DisplayHealthBar, the previous
// session's blocking-callee list was wrong: every one of sub_46D7B0/
// 46DA40/46D430/46D790/470430/46D130/(the call Hex-Rays mislabels
// "qt_register_signal_spy_callbacks" at 0x46D870, a decompiler symbol mixup
// - names.json and its declared VECTOR* signature both confirm it is really
// gte_ldlvl) is already decompiled and named: gte_SetRotMatrix, gte_rtir,
// M3dMaths_SquareRoot0, gte_stlvnl, VectorNormal, ratan2, gte_ldlvl (all in
// ps2funcs.cpp, already @Ok). sub_506440/507910 = PCGfx_UseTexture/
// PCGfx_DrawQPoly2D (see Panel_DisplayHealthBar's comment above for how
// that was confirmed). sub_462BB0/462C30 = Panel_DrawTexturedPoly(Texture*,
// int) / Panel_SetStretchedScreenCoords(...,SAnimFrame*,...), both already
// in this file.
//
// Globals: byte_60F77C = G_COMPASS_STATUS (already declared, set by
// Panel_CreateCompass/cleared by Panel_DestroyCompass). dword_60F708/70C/
//710 = G_COMPASS_POSITION's vx/vy/vz (confirmed via Panel_CreateCompass's own
// disasm at 0x463800, which writes exactly these three dwords - matches
// this file's existing `G_COMPASS_POSITION = *pVec >> 12;`). qword_56F1B4/
// dword_56F1BC = gMikeCamera[0].Position (idb_globals.txt: 0x56F1B0
// gMikeCamera; weapons.cpp's Transform() already documents "gMikeCamera[0]
// .Position split across qword_56F1B4 low/high"); unk_56F224 = the camera
// view matrix at gMikeCamera[1]'s tail (utils.cpp's gCameraViewMatrix,
// spidey.cpp's stru_56F224 - same address, reused name here). dword_60F76C
// = gPanelScreenY (already declared above). dword_6B4CA8 = gTimerRelated
// (bit.h). dword_56FB04 = pPoly (idb_globals.txt), the same growing poly
// buffer Panel_DrawFlatShadedPoly/Panel_DrawTexturedPoly already use, here
// advanced by sizeof(POLY_F3) per triangle with NO PolyBufferEnd check -
// matches the original disasm exactly (a genuine missing bounds check,
// reproduced not fixed). dword_568158/568154/628614/61B5FC = gGameResolutionY
// /gGameResolutionX/Yres/Xres (PCGfx.cpp's own naming comment). word_610C48/
// 610C4A = rcossin_tbl's .sin/.cos fields read as a raw u16 array (confirmed
// by address: 610C4A is 610C48+2, i.e. rcossin_tbl[i].cos read via the same
// "2*i" stride already used for .sin - the same struct, no new global).
// dword_60F75C = G_ANIM_COMPASS (already declared). byte_54D341 = gPrintStubbed
// (ps2funcs.h); its debug-print calls (gsub_46CB90/nullsub_1) are no-ops in
// this build (see Panel_DisplayHealthBar's comment above) and are omitted.
//
// Two faithfully-reproduced original dead stores: in each needle-half block,
// `poly->code |= 2` is computed and stored, then immediately overwritten by
// the very next `*(u32*)&poly->r0 = tintColor` full-dword store (confirmed
// from the disasm store order) - the |2 never actually takes effect. Kept
// exactly as ordered in the original rather than dropped as "dead code".
//
// The math: builds a camera-relative direction vector from the player/
// camera position to G_COMPASS_POSITION, GTE-rotates and normalises it,
// turns it into a heading angle via ratan2, then builds a 2-triangle flat
// dial background (pPoly, POLY_F3) plus a 2-quad needle sprite (G_ANIM_COMPASS,
// split left/right down the middle via a U-coordinate swap on the second
// half) pointing along that heading. The needle brightens (gCompassFlashTimer)
// right after Panel_CreateCompass and always has a small time-based pulse
// (rcossin_tbl indexed by gTimerRelated) baked into the dial's alpha.
// @Ok
void Panel_DisplayCompass(void)
{
	if (!G_COMPASS_STATUS)
		return;

	gte_SetRotMatrix(stru_56F224);

	CVector dir;
	dir.vx = (G_COMPASS_POSITION.vx - stru_56F1B4->vx) >> 6;
	dir.vy = (G_COMPASS_POSITION.vy - stru_56F1B4->vy) >> 6;
	dir.vz = (G_COMPASS_POSITION.vz - stru_56F1B4->vz) >> 6;

	gte_ldlvl(reinterpret_cast<VECTOR *>(&dir));
	gte_rtir();

	i32 dist = M3dMaths_SquareRoot0(dir.vx * dir.vx + dir.vy * dir.vy + dir.vz * dir.vz) << 6;

	gte_stlvnl(reinterpret_cast<VECTOR *>(&dir));

	dir.vy = 0;

	VectorNormal(reinterpret_cast<VECTOR *>(&dir), reinterpret_cast<VECTOR *>(&dir));

	i16 angle = (i16)ratan2(dir.vz, dir.vx);

	i32 xOff1 = (25 * dir.vx) >> 12;
	i32 idxA = (angle - 1024) & 0xFFF;
	i32 sideA = (9 * G_RCOSSIN_TBL[idxA].cos) >> 12;
	i32 idxB = (angle + 1024) & 0xFFF;
	i32 sideB = (9 * G_RCOSSIN_TBL[idxB].cos) >> 12;
	i32 xOff2 = (6 * dir.vx) >> 12;

	i32 tipY = *gPanelScreenY + 3604 * (320 * ((25 * dir.vz) >> 12) / 512) / 4096;
	i32 tailA = *gPanelScreenY + 320 * ((9 * G_RCOSSIN_TBL[idxA].sin) >> 12) / 512;
	i32 tailB = *gPanelScreenY + 320 * ((9 * G_RCOSSIN_TBL[idxB].sin) >> 12) / 512;
	i32 tipY2 = *gPanelScreenY + 320 * ((6 * dir.vz) >> 12) / 512;

	i32 pulseHalfWidth = (dist <= 0x4000) ? (((0x4000 - dist) >> 8) + 8) : 8;

	i32 pulseSin = G_RCOSSIN_TBL[(pulseHalfWidth * (i16)G_TIMER_RELATED) & 0xFFF].sin;
	i32 pulseBrightness = (255 * abs(pulseSin)) >> 12;
	u32 dialColor = (pulseBrightness << 8) | 0xFF;

	for (i32 tri = 0; tri < 2; tri++)
	{
		i32 sideOffset = (tri == 0) ? sideA : sideB;
		i32 tailOffset = (tri == 0) ? tailA : tailB;

		POLY_F3 *p = (POLY_F3 *)G_PPOLY;
		G_PPOLY = (u32 *)((u8 *)G_PPOLY + sizeof(POLY_F3));

		*(u32 *)&p->r0 = dialColor;
		p->y0 = (i16)(199 - tipY);
		p->x0 = (i16)(xOff1 + 432);
		p->x1 = (i16)(sideOffset + 432);
		p->y1 = (i16)(199 - tailOffset);
		p->y2 = (i16)(199 - tipY2);
		p->x2 = (i16)(xOff2 + 432);
	}

	// 0x463B6C: the PC port draws the two triangles above as one untextured
	// quad (tip, side A, tip 2, side B) in the pulsing yellow, z offset 0.2
	{
		f32 xScale = G_GAME_RESOLUTION_X / (f32)G_XRES;
		f32 yScale = G_GAME_RESOLUTION_Y / (f32)G_YRES;
		u32 arrowColor = (static_cast<u32>(pulseBrightness) | 0xFFFFFF00u) << 8;

		PCGfx_UseTexture(1, DCGfx_BlendingMode_0);
		PCGfx_DrawQPoly2D(
				(sideA + 432) * xScale, (199 - tailA) * yScale, 0.0f, 0.0f, arrowColor,
				(xOff2 + 432) * xScale, (199 - tipY2) * yScale, 0.0f, 0.0f, arrowColor,
				(xOff1 + 432) * xScale, (199 - tipY) * yScale, 0.0f, 0.0f, arrowColor,
				(sideB + 432) * xScale, (199 - tailB) * yScale, 0.0f, 0.0f, arrowColor,
				0.2f);
	}

	POLY_FT4 *pNeedle = (POLY_FT4 *)Panel_DrawTexturedPoly(G_ANIM_COMPASS->pTexture, 0);
	if (pNeedle)
	{
		u32 tintColor = (*(u32 *)&pNeedle->r0 & 0xFF000000) | 0x323280;

		if (*gCompassFlashTimer != 0)
		{
			i32 sinT = G_RCOSSIN_TBL[(G_TIMER_RELATED << 6) & 0xFFF].sin;
			i32 signMask = (205 * sinT) >> 31;
			tintColor = (abs((127 * sinT) >> 12) + 128)
					| (((signMask ^ ((205 * sinT) >> 12)) + 0xFFFFFF * signMask + 50) << 8)
					| (*(u32 *)&pNeedle->r0 & 0xFF000000);
			*gCompassFlashTimer -= 2;
			if (*gCompassFlashTimer < 0)
				*gCompassFlashTimer = 0;
		}

		Panel_SetStretchedScreenCoords(458, 215 - *gPanelScreenY, pNeedle, G_ANIM_COMPASS, 32, 40);

		i16 x0 = pNeedle->x0;
		u8 codeOr2 = pNeedle->code | 2;
		pNeedle->tpage &= 0xFF9F;
		pNeedle->code = codeOr2;
		i16 x1 = pNeedle->x1;
		*(u32 *)&pNeedle->r0 = tintColor;
		i16 halfWidth = (i16)((x1 - x0) / 2);
		pNeedle->x2 += halfWidth;
		pNeedle->x3 += halfWidth;
		i16 newX1 = x1 + halfWidth;
		pNeedle->x0 = x0 + halfWidth;
		pNeedle->x1 = newX1;

		PanelCompass_DrawNeedleHalf(pNeedle, G_ANIM_COMPASS->pTexture);

		POLY_FT4 *pNeedle2 = (POLY_FT4 *)Panel_DrawTexturedPoly(G_ANIM_COMPASS->pTexture, 0);
		if (pNeedle2)
		{
			Panel_SetStretchedScreenCoords(458, 215 - *gPanelScreenY, pNeedle2, G_ANIM_COMPASS, 32, 40);

			pNeedle2->tpage &= 0xFF9F;
			pNeedle2->x0 -= halfWidth;
			pNeedle2->x1 -= halfWidth;
			pNeedle2->x2 -= halfWidth;
			pNeedle2->code |= 2;
			u8 u1minus1 = pNeedle2->u1 - 1;
			u8 origU0 = pNeedle2->u0;
			pNeedle2->x3 -= halfWidth;
			pNeedle2->u2 = u1minus1;
			pNeedle2->u0 = u1minus1;
			pNeedle2->u3 = origU0;
			pNeedle2->u1 = origU0;
			*(u32 *)&pNeedle2->r0 = tintColor;

			PanelCompass_DrawNeedleHalf(pNeedle2, G_ANIM_COMPASS->pTexture);
		}
	}
}

// Shared inline draw sequence used at every icon-overlay site in
// Panel_DisplayHealthBar below (originally repeated inline 7 times in the
// disassembly at 0x464270: PCGfx_UseTexture(tex->clut, blend) then a
// manual scale+color-pack PCGfx_DrawQPoly2D call reading straight from the
// poly's own r0/g0/b0 and x0..y3 fields). Same shape as the icon-overlay
// block already established in Panel_DisplayTimer above (lines ~516-543).
// Factored into one static helper here since the original inlines it
// verbatim with only the poly/texture/blend/zOffset varying; not chasing a
// byte match this session (see PLAN.md acceptance bar), so factoring does
// not hurt correctness and avoids seven near-identical copies.
// @Ok
static void PanelHB_DrawIconOverlay(POLY_FT4 *p, Texture *tex, DCGfx_BlendingMode blend, f32 zOffset)
{
	PCGfx_UseTexture(tex->clut, blend);

	f32 yScale = G_GAME_RESOLUTION_Y / (f32)G_YRES;
	f32 xScale = G_GAME_RESOLUTION_X / (f32)G_XRES;
	u32 col = p->b0 | ((p->g0 | ((p->r0 | 0xFFFFFF00) << 8)) << 8);

	PCGfx_DrawQPoly2D(
			p->x0 * xScale, p->y0 * yScale, 0.0f, 0.0f, col,
			p->x1 * xScale, p->y1 * yScale, 1.0f, 0.0f, col,
			p->x2 * xScale, p->y2 * yScale, 0.0f, 1.0f, col,
			p->x3 * xScale, p->y3 * yScale, 1.0f, 1.0f, col,
			zOffset);
}

// Real translation, 0x00464270. Decompiled via Hex-Rays and cross-checked
// against the raw disasm. The previous session's investigation undercounted
// this: every "undecompiled helper" it listed (sub_462BB0/462C30/462CD0/
// 462D60/462FB0/506440/507910) turned out to already be decompiled and
// named in this repo (tools/names.json), just not recognised as such:
// sub_462BB0 = Panel_DrawTexturedPoly(Texture*,int) (this file, line
// ~852), sub_462C30/462CD0 = the two Panel_SetStretchedScreenCoords
// overloads (line ~653/~699), sub_462D60 = DCPanel_DrawFlatShadedPoly
// (line ~132), sub_462FB0 = the 9-arg DCDrawGouraudPoly (line ~111),
// sub_506440 = PCGfx_UseTexture (PCGfx.cpp, already @Ok), sub_507910 =
// PCGfx_DrawQPoly2D (PCGfx.h/.cpp, already @Ok, confirmed via its own
// naming comment in PCGfx.cpp mapping 0x568158/0x628614/0x568154/0x61B5FC
// to gGameResolutionY/Yres/gGameResolutionX/Xres, the exact same globals
// this function scales its icon coordinates with).
//
// dword_60F788/60F78C = G_HEALTH_BAR_ONE/G_HEALTH_BAR_TWO, dword_60F744/60F748 =
// G_HEALTH_BAR_RELATED/G_HEALTH_BAR_RELATED_TWO, dword_60F654 = G_HEALTH_BAR_ITEM_TYPE,
// dword_60F658.."660.."668 = G_HEALTH_BAR_TEXTURES[0..4] (all already declared
// at the top of this file, confirmed 1:1 against Panel_CreateHealthBar's
// own disasm at 0x463610 which writes exactly these five slots per boss
// case, matching G_HEALTH_BAR_TEXTURES' assignment order in the existing
// Panel_CreateHealthBar source above). dword_60F758 = G_ANIM_SP (idb_globals
// confirms 0x60F758 = G_ANIM_SP; SAnimFrame is 8 bytes/VALIDATE_SIZE 0x8, so
// "dword_60F758 + 8/16/24" are &G_ANIM_SP[1]/[2]/[3]).
//
// The boss-specific "extra icon" gate fields at absolute offsets 828/829
// (0x33C/0x33D, Venom-only) were already named fields (CVenom::field_33C/
// field_33D, both u8, matching the byte-sized compares in the disasm).
// Fields at 976/832 (CRhino::field_3D0, CCarnage::field_340) were also
// already named. Fields at 904/1212/1004/808 fell inside as-yet-unexplored
// PADDING() gaps in CVenom/CDocOc/CScorpion/CMysterio; confirmed via disasm
// they are read as plain dword != 0 checks, so this session split those
// PADDING regions and added CVenom::field_388, CDocOc::field_4BC,
// CScorpion::field_3EC, CMysterio::field_328 (all i32, VALIDATE added,
// total struct size unchanged) - see those headers/.cpp files for the
// exact split. Their real meaning (what makes a boss "wounded") is not
// known, only that a nonzero value there swaps in the boss's *_wounded (or
// equivalent) texture instead of the default one; same honest-placeholder
// convention already used throughout the repo for field_XXX members.
//
// CBody::mCBodyFlags bit 0x40 (already-named field, VALIDATE'd at 0x46) is
// tested as a "boss destroyed" flag that tears the health bar down.
// CBody::mHealth (i16 @ 0xE2/226, already named) supplies current health;
// G_HEALTH_BAR_RELATED/RelatedTwo hold the health captured when the bar was
// created (used as the 100% baseline for the percentage-width bar math).
//
// The many small per-call x/y/w/h adjustments (0x54E910..0x54E99C) are
// read-only .data ints referenced exactly once each, only from this
// function (confirmed via xrefs_to) - not runtime state, so they are
// folded into literal constants below (values read directly off the
// binary via IDA, not guessed) rather than modelled as unnamed globals.
// byte_54E8D0 (also read-only, xref'd only from here) is a baked constant
// equal to 1 in the shipped binary, i.e. its "if" is always taken; kept as
// a literal `true` rather than invented as a live global.
//
// nullsub_1 (0x4015B0, this file's gsub_4015B0) is called at a few spots
// in the original with a (bool, const char*) debug-print argument pair
// that does not match gsub_4015B0's declared (void*) signature here; since
// gsub_4015B0's real body is a single `ret` (confirmed elsewhere in this
// file, tools/functions/4199856.bin), the call is a provable no-op and is
// omitted below rather than fought into a mismatched declaration.
//
// The final block (only reached when G_HEALTH_BAR_ITEM_TYPE == 310 and
// G_HEALTH_BAR_TEXTURES[2] is set) draws a second, smaller health bar for
// G_HEALTH_BAR_TWO/G_HEALTH_BAR_RELATED_TWO - almost certainly the Jonah Jameson
// hostage bar shown during the Scorpion fight (G_HEALTH_BAR_TEXTURES[2] for
// case 310 is the "jonah" texture, set in Panel_CreateHealthBar above).
// @Ok
void Panel_DisplayHealthBar(void)
{
	if (G_HEALTH_BAR_ONE == 0)
		return;

	if (G_HEALTH_BAR_ONE->mCBodyFlags & 0x40)
	{
		G_HEALTH_BAR_ONE = 0;
		G_HEALTH_BAR_TWO = 0;
		return;
	}

	switch (G_HEALTH_BAR_ITEM_TYPE)
	{
	case 307:
	case 308:
	case 310:
	case 311:
	case 313:
	case 314:
		break;
	default:
		return;
	}

	if (G_HEALTH_BAR_ITEM_TYPE == 313)
	{
		if (((CVenom *)G_HEALTH_BAR_ONE)->field_33C != 0)
		{
			POLY_FT4 *pMJ = (POLY_FT4 *)Panel_DrawTexturedPoly(G_HEALTH_BAR_TEXTURES[2], 0);
			if (pMJ)
				Panel_SetStretchedScreenCoords(445, *gPanelScreenY + 71, pMJ, G_HEALTH_BAR_TEXTURES[2], 31, 30);
			PanelHB_DrawIconOverlay(pMJ, G_HEALTH_BAR_TEXTURES[2], DCGfx_BlendingMode_1, 2.9999001f);

			for (i32 y = 0; y < 48; y += 16)
			{
				SAnimFrame *pFrame = &G_ANIM_SP[1];
				POLY_FT4 *p = (POLY_FT4 *)Panel_DrawTexturedPoly(pFrame->pTexture, 0);
				if (p)
					Panel_SetStretchedScreenCoords(486, y + *gPanelScreenY + 115, p, pFrame, 15, 16);
				PanelHB_DrawIconOverlay(p, pFrame->pTexture, DCGfx_BlendingMode_0, 3.0f);
			}

			{
				SAnimFrame *pFrame = &G_ANIM_SP[2];
				POLY_FT4 *p = (POLY_FT4 *)Panel_DrawTexturedPoly(pFrame->pTexture, 0);
				if (p)
					Panel_SetStretchedScreenCoords(486, *gPanelScreenY + 161, p, pFrame, 15, 14);
				PanelHB_DrawIconOverlay(p, pFrame->pTexture, DCGfx_BlendingMode_0, 3.0f);
			}

			DCPanel_DrawFlatShadedPoly(3.0f, 467, *gPanelScreenY - ((CVenom *)G_HEALTH_BAR_ONE)->field_338 / 4096 + 155, 12, ((CVenom *)G_HEALTH_BAR_ONE)->field_338 / 4096, 64, 64, 160, 0, 0);
			DCPanel_DrawFlatShadedPoly(4.0f, 467, *gPanelScreenY + 99, 12, 56, 0, 0, 0, 0, 0);
		}

		if (((CVenom *)G_HEALTH_BAR_ONE)->field_33D == 0)
			return;
	}

	i32 healthWidth = 163 * (((G_HEALTH_BAR_RELATED - G_HEALTH_BAR_ONE->mHealth) << 7) / G_HEALTH_BAR_RELATED) / 128;

	Texture *pBossTex;
	switch (G_HEALTH_BAR_ITEM_TYPE)
	{
	case 310:
		pBossTex = (((CScorpion *)G_HEALTH_BAR_ONE)->field_3EC != 0) ? G_HEALTH_BAR_TEXTURES[3] : G_HEALTH_BAR_TEXTURES[0];
		break;
	case 307:
		pBossTex = (((CRhino *)G_HEALTH_BAR_ONE)->field_3D0 != 0) ? G_HEALTH_BAR_TEXTURES[2] : G_HEALTH_BAR_TEXTURES[0];
		break;
	case 313:
		pBossTex = (((CVenom *)G_HEALTH_BAR_ONE)->field_388 != 0) ? G_HEALTH_BAR_TEXTURES[4] : G_HEALTH_BAR_TEXTURES[0];
		break;
	case 314:
		pBossTex = (((CCarnage *)G_HEALTH_BAR_ONE)->field_340 != 0) ? G_HEALTH_BAR_TEXTURES[2] : G_HEALTH_BAR_TEXTURES[0];
		break;
	case 308:
		pBossTex = (((CDocOc *)G_HEALTH_BAR_ONE)->field_4BC != 0) ? G_HEALTH_BAR_TEXTURES[2] : G_HEALTH_BAR_TEXTURES[0];
		break;
	case 311:
		pBossTex = (((CMysterio *)G_HEALTH_BAR_ONE)->field_328 != 0) ? G_HEALTH_BAR_TEXTURES[2] : G_HEALTH_BAR_TEXTURES[0];
		break;
	default:
		pBossTex = G_HEALTH_BAR_TEXTURES[0];
		break;
	}

	POLY_FT4 *pBoss = (POLY_FT4 *)Panel_DrawTexturedPoly(pBossTex, 0);
	if (pBoss)
		Panel_SetStretchedScreenCoords(448, *gPanelScreenY + 16, pBoss, pBossTex, 28, 31);
	PanelHB_DrawIconOverlay(pBoss, pBossTex, DCGfx_BlendingMode_1, 1.0f);

	POLY_FT4 *pLabel = (POLY_FT4 *)Panel_DrawTexturedPoly(G_HEALTH_BAR_TEXTURES[1], 0);
	if (pLabel)
		Panel_SetStretchedScreenCoords(283, *gPanelScreenY + 24, pLabel, G_HEALTH_BAR_TEXTURES[1], 12, 16);
	PanelHB_DrawIconOverlay(pLabel, G_HEALTH_BAR_TEXTURES[1], DCGfx_BlendingMode_1, 1.0f);

	for (i32 x = 0; x < 150; x += 25)
	{
		SAnimFrame *pFrame = &G_ANIM_SP[3];
		POLY_FT4 *p = (POLY_FT4 *)Panel_DrawTexturedPoly(pFrame->pTexture, 0);
		if (p)
			Panel_SetStretchedScreenCoords(x + 325, *gPanelScreenY + 40, p, pFrame, 16, 16);
		PanelHB_DrawIconOverlay(p, pFrame->pTexture, DCGfx_BlendingMode_1, 1.0f);
	}

	if (healthWidth != 0)
		DCPanel_DrawFlatShadedPoly(3.0f, 288, *gPanelScreenY + 29, healthWidth, 8, 0, 0, 0, 0, 0);
	if (healthWidth <= G_HEALTH_BAR_RELATED / 2)
		DCDrawGouraudPoly(4.0f, 288, *gPanelScreenY + 29, 81, 8, 0x0000FF00, 0x0000FFFF, 0x0000FF00, 0x0000FFFF);
	DCDrawGouraudPoly(4.0f, 369, *gPanelScreenY + 29, 82, 8, 0x0000FFFF, 0x000000FF, 0x0000FFFF, 0x000000FF);

	if (G_HEALTH_BAR_ITEM_TYPE == 310 && G_HEALTH_BAR_TEXTURES[2] != 0)
	{
		POLY_FT4 *pJonah = (POLY_FT4 *)Panel_DrawTexturedPoly(G_HEALTH_BAR_TEXTURES[2], 0);
		if (pJonah)
			Panel_SetStretchedScreenCoords(448, *gPanelScreenY + 45, pJonah, G_HEALTH_BAR_TEXTURES[0], 28, 30);
		PanelHB_DrawIconOverlay(pJonah, G_HEALTH_BAR_TEXTURES[2], DCGfx_BlendingMode_1, 1.0f);

		POLY_FT4 *pJonahLabel = (POLY_FT4 *)Panel_DrawTexturedPoly(G_HEALTH_BAR_TEXTURES[1], 0);
		if (pJonahLabel)
			Panel_SetStretchedScreenCoords(407, *gPanelScreenY + 53, pJonahLabel, G_HEALTH_BAR_TEXTURES[1], 12, 15);
		PanelHB_DrawIconOverlay(pJonahLabel, G_HEALTH_BAR_TEXTURES[1], DCGfx_BlendingMode_1, 1.0f);

		{
			SAnimFrame *pFrame = &G_ANIM_SP[3];
			POLY_FT4 *p = (POLY_FT4 *)Panel_DrawTexturedPoly(pFrame->pTexture, 0);
			if (p)
				Panel_SetStretchedScreenCoords(450, *gPanelScreenY + 69, p, pFrame, 16, 15);
			PanelHB_DrawIconOverlay(p, pFrame->pTexture, DCGfx_BlendingMode_1, 1.0f);
		}

		i32 jonahWidth = 38 * (((G_HEALTH_BAR_RELATED_TWO - G_HEALTH_BAR_TWO->mHealth) << 7) / G_HEALTH_BAR_RELATED_TWO) / 128;

		if (jonahWidth != 0)
			DCPanel_DrawFlatShadedPoly(3.0f, 413, *gPanelScreenY + 57, jonahWidth, 8, 0, 0, 0, 0, 0);
		if (jonahWidth <= G_HEALTH_BAR_RELATED_TWO / 2)
			DCDrawGouraudPoly(4.0f, 413, *gPanelScreenY + 57, 19, 8, 0x0000FF00, 0x0000FFFF, 0x0000FF00, 0x0000FFFF);
		DCDrawGouraudPoly(4.0f, 432, *gPanelScreenY + 57, 19, 8, 0x0000FFFF, 0x000000FF, 0x0000FFFF, 0x000000FF);
	}
}

// bomb-timer animation state (l1a3bomb level)
static u8 * const gBombTimerAnimOne = (u8*)0x0060F784;
static u8 * const gBombTimerAnimTwo = (u8*)0x0060F785;
static i32 * const gBombTimerAnimOnePos = (i32*)0x0060F66C;
static i32 * const gBombTimerAnimTwoPos = (i32*)0x0060F74C;
static i32 * const gBombTimerLastMinute = (i32*)0x0060F780;

// @Ok
void Panel_DisplayTimer(void)
{
	i32 LevelId = Trig_GetLevelId();
	switch (LevelId)
	{
	case 513:
		if (G_MECHLIST_PLAYER == 0 || (G_MECHLIST_PLAYER->field_E18 == 0 && G_MECHLIST_PLAYER->mSynthesizedInputActive == 0))
			Reloc_CallUserFunction("l2a1lsc", 1, 0, 0);
		break;
	case 1281:
		if (G_MECHLIST_PLAYER == 0 || (G_MECHLIST_PLAYER->field_E18 == 0 && G_MECHLIST_PLAYER->mSynthesizedInputActive == 0))
			Reloc_CallUserFunction("venom", 1, 0, 0);
		break;
	case 1285:
		Reloc_CallUserFunction("l5a5lsc", 1, 0, 0);
		break;
	case 2054:
		Reloc_CallUserFunction("superock", 1, 0, 0);
		break;
	default:
		if (gBombDieRelatedOne != 0)
		{
			if (*gBombTimerAnimOne == 0 && Rnd(20) == 0)
			{
				*gBombTimerAnimOne = 1;
				*gBombTimerAnimOnePos = 69632;
			}
			if (*gBombTimerAnimTwo == 0 && Rnd(30) == 0)
			{
				*gBombTimerAnimTwo = 1;
				*gBombTimerAnimTwoPos = 94208;
			}
			char v57[8];
			strcpy(v57, "00:00");
			i32 mins = gBombAIRelated / 0xE10;
			i32 secs = gBombAIRelated / 0x3C - 60 * (gBombAIRelated / 0xE10);
			if (mins < 0xA)
			{
				v57[0] = 48;
			}
			else
			{
				v57[0] = mins / 10 + 48;
				mins = mins + 2 * (4 * (mins / -10) - mins / 10);
			}
			v57[1] = mins + 48;
			if (secs != *gBombTimerLastMinute)
			{
				SFX_Play(0x20, 0x2000, 0);
				*gBombTimerLastMinute = secs;
			}
			if (secs < 10)
			{
				v57[3] = 48;
			}
			else
			{
				v57[3] = secs / 10 + 48;
				secs = secs + 2 * (4 * (secs / -10) - secs / 10);
			}
			v57[4] = secs + 48;

			Texture* pTexture = G_ANIM_TABLE[14][19].pTexture;
			if (pTexture != 0)
			{
				POLY_FT4* v5 = (POLY_FT4*)Panel_DrawTexturedPoly(pTexture, 0);
				Panel_SetStretchedScreenCoords(0 + 219, 0 + *gPanelScreenY + 19, v5, pTexture, 27, 24);
				PCGfx_UseTexture(pTexture->clut, DCGfx_BlendingMode_1);
				float yScale = (float)G_GAME_RESOLUTION_Y / (float)G_YRES;
				float xScale = (float)G_GAME_RESOLUTION_X / (float)G_XRES;
				u32 col = v5->b0 | ((v5->g0 | ((v5->r0 | 0xFFFFFF00) << 8)) << 8);
				PCGfx_DrawQPoly2D(
					(float)v5->x0 * xScale, (float)v5->y0 * yScale, 0, 0, col,
					(float)v5->x1 * xScale, (float)v5->y1 * yScale, 1065353216, 0, col,
					(float)v5->x2 * xScale, (float)v5->y2 * yScale, 0, 1065353216, col,
					(float)v5->x3 * xScale, (float)v5->y3 * yScale, 1065353216, 1065353216, col, 0.6f);

				POLY_FT4* v7 = (POLY_FT4*)Panel_DrawTexturedPoly(pTexture, 0);
				Panel_SetStretchedScreenCoords(20 + 241, 0 + *gPanelScreenY + 19, v7, pTexture, 27, 24);
				v7->u2 = v5->u1;
				v7->u0 = v5->u1;
				v7->u3 = v5->u0;
				v7->u1 = v5->u0;
				PCGfx_UseTexture(pTexture->clut, DCGfx_BlendingMode_1);
				u32 col2 = v7->b0 | ((v7->g0 | ((v7->r0 | 0xFFFFFF00) << 8)) << 8);
				PCGfx_DrawQPoly2D(
					(float)v7->x0 * xScale, (float)v7->y0 * yScale, 1065353216, 0, col2,
					(float)v7->x1 * xScale, (float)v7->y1 * yScale, 0, 0, col2,
					(float)v7->x2 * xScale, (float)v7->y2 * yScale, 1065353216, 1065353216, col2,
					(float)v7->x3 * xScale, (float)v7->y3 * yScale, 0, 1065353216, col2, 0.6f);
			}

			Mess_SetScale(256);
			Mess_SetTextJustify(1);
			Mess_SetRGB(0x60, 0x60, 0x80, 0);
			char* CurrentFont = Mess_GetCurrentFont();
			char v58[32];
			sprintf(v58, CurrentFont);
			Mess_SetCurrentFont("sp_fnt01.fnt");
			Mess_DrawText(234, *gPanelScreenY + 38, v57, 0, 0x1000);
			Mess_SetCurrentFont(v58);
			DCPanel_DrawFlatShadedPoly(0.5f, 222, *gPanelScreenY + 23, 78, 20, 0, 0, 0, 0, 1);

			if (*gBombTimerAnimOne != 0)
			{
				i32 pos = *gBombTimerAnimOnePos >> 12;
				if (pos >= 23)
				{
					i32 h = pos <= 36 ? 7 : 43 - pos;
					DCPanel_DrawFlatShadedPoly(0.5f, 222, pos + *gPanelScreenY, 78, h, 24, 0x18, 0x18, 0, 2);
				}
				else
				{
					DCPanel_DrawFlatShadedPoly(0.5f, 222, *gPanelScreenY + 23, 78, pos - 16, 24, 0x18, 0x18, 0, 2);
				}
				*gBombTimerAnimOnePos += 3798;
				if (*gBombTimerAnimOnePos > 176128)
					*gBombTimerAnimOne = 0;
			}
			if (*gBombTimerAnimTwo != 0)
			{
				DCPanel_DrawFlatShadedPoly(0.5f, 222, *gPanelScreenY + (*gBombTimerAnimTwoPos >> 12), 78, 1, 32, 0x20, 0x20, 0, 2);
				*gBombTimerAnimTwoPos += 2334;
				if (*gBombTimerAnimTwoPos > 176128)
					*gBombTimerAnimTwo = 0;
			}
		}
		break;
	}
}

// @Ok
// @Matching
int Panel_DrawFlatShadedPoly(i32 x, i32 y, i32 w, i32 h, u8 r, u8 g, u8 b, i32, i32 a9)
{
	if ((u8*)G_PPOLY + sizeof(POLY_F4) > G_POLY_BUFFER_END)
	{
		return 0;
	}

	POLY_F4* p = (POLY_F4*)G_PPOLY;
	G_PPOLY = (u32*)((u8*)G_PPOLY + sizeof(POLY_F4));

	if (!gPrintStubbed)
	{
		gsub_46CB90((void*)"Panel_DrawFlatShadedPoly");
	}

	p->r0 = r;
	p->b0 = b;
	p->g0 = g;

	p->x0 = (i16)x;
	p->y0 = (i16)y;
	p->x1 = (i16)(x + w);
	p->y1 = (i16)y;
	p->x2 = (i16)x;
	p->y2 = (i16)(y + h);
	p->x3 = (i16)(x + w);
	p->y3 = (i16)(y + h);

	gsub_46CB90(G_RENDER_BUF);

	if (a9)
	{
		if (!gPrintStubbed)
		{
			gsub_46CB90((void*)"Panel_DrawFlatShadedPoly: extra");
		}

		if ((u8*)G_PPOLY + 8 > G_POLY_BUFFER_END)
		{
			return 0;
		}

		G_PPOLY = (u32*)((u8*)G_PPOLY + 8);

		if (!gPrintStubbed)
		{
			gsub_46CB90((void*)"Panel_DrawFlatShadedPoly: extra2");
		}

		gsub_46CB90(G_RENDER_BUF);
	}

	return (int)p;
}

// @Ok
// @Matching
void Panel_Init(void)
{
	Spool_AnimAccess("Sp", &G_ANIM_SP);
	Spool_AnimAccess("Compass", &G_ANIM_COMPASS);
	Spool_AnimAccess("Webcart", &G_ANIM_WEBCART);
}

// @Ok
// @AlmostMatching: same logic but slightly out of order
void Panel_SetStretchedScreenCoords(
		i32 a1,
		i32 a2,
		POLY_FT4 *a3,
		SAnimFrame *a4,
		i32 a5,
		i32 a6)
{
	i32 v6 = a5;
	if (!v6)
	{
		v6 = a4->Width;
	}

	v6 *= 6554;

	if ( (v6 & 0xFFF) >= 0x800)
	{
		v6 += 4096;
	}

	v6 >>= 12;

	if (!a6)
	{
		a6 = a4->Height;
	}

	i32 OffY = a4->OffY;
	i32 OffX = a4->OffX;


	a3->x0 = a1 + ((6554 * OffX) >> 12);
	a3->y0 = a2 + OffY;

	a3->x1 = a3->x0 + v6;
	a3->y1 = a3->y0;
	a3->x2 = a3->x0;

	a3->y2 = a3->y0 + a6;
	a3->x3 = a3->x1;
	a3->y3 = a3->y2;
}

// @Ok
// @Matching
void Panel_SetStretchedScreenCoords(
		i32 a1,
		i32 a2,
		POLY_FT4 *a3,
		const Texture *a4,
		i32 a5,
		i32 a6)
{
	i32 v6 = a5;
	if (!a5)
	{
		v6 = a4->u1 - a4->u0;
	}

	v6 *= 6554;

	if ((v6 & 0xFFF) >= 0x800)
	{
		v6 += 4096;
	}

	v6 >>= 12;

	if (!a6)
	{
		a6 = a4->v2 - a4->v0;
	}

	a3->x0 = a1;
	a3->y0 = a2;
	a3->x1 = a1 + v6;
	a3->y1 = a2;
	a3->x2 = a1;
	a3->y2 = a2 + a6;
	a3->x3 = a3->x1;
	a3->y3 = a3->y2;
}

// @Ok
 void Panel_UpdateTimer(void)
{
	if (gBombDieRelatedTwo)
	{
		u32 v1 = 0;
		if (G_MECHLIST_PLAYER->field_E18 || G_MECHLIST_PLAYER->mSynthesizedInputActive)
		{
			v1 = 0;
		}
		else
		{
			v1 = (gBombRelated * (G_TIMER_RELATED - gBombDieTimerRelated)) >> 12;
		}

		if (gBombAIRelated > v1)
		{
			gBombAIRelated -= v1;
		}
		else
		{
			gBombAIRelated = 0;
		}

	}

	gBombDieTimerRelated = G_TIMER_RELATED;
}

// @Ok
void Panel_CreateHealthBar(CBody* pBody, i32 a2)
{
	if ( a2 != 316 )
	{
		G_HEALTH_BAR_ONE = pBody;
		G_HEALTH_BAR_ITEM_TYPE = a2;
		G_HEALTH_BAR_RELATED = pBody->mHealth;
	}
	else
	{
		G_HEALTH_BAR_TWO = pBody;
		G_HEALTH_BAR_RELATED_TWO = pBody->mHealth;
	}

	switch ( a2 )
	{
		case 310:
			G_HEALTH_BAR_TEXTURES[0] = Spool_FindTextureEntry("scorpion");
			G_HEALTH_BAR_TEXTURES[1] = Spool_FindTextureEntry("boss");
			G_HEALTH_BAR_TEXTURES[2] = Spool_FindTextureEntry("jonah");
			G_HEALTH_BAR_TEXTURES[3] = Spool_FindTextureEntry("scorpion_wounded");
			break;
		case 307:
			G_HEALTH_BAR_TEXTURES[0] = Spool_FindTextureEntry("rhino");
			G_HEALTH_BAR_TEXTURES[1] = Spool_FindTextureEntry("boss");
			G_HEALTH_BAR_TEXTURES[2] = Spool_FindTextureEntry("rhino_wounded");
			break;
		case 311:
			G_HEALTH_BAR_TEXTURES[0] = Spool_FindTextureEntry("mysterio");
			G_HEALTH_BAR_TEXTURES[1] = Spool_FindTextureEntry("boss");
			G_HEALTH_BAR_TEXTURES[2] = Spool_FindTextureEntry("mysterio_wounded");
			break;
		case 313:
			G_HEALTH_BAR_TEXTURES[0] = Spool_FindTextureEntry("venom");
			G_HEALTH_BAR_TEXTURES[1] = Spool_FindTextureEntry("boss");
			G_HEALTH_BAR_TEXTURES[2] = Spool_FindTextureEntry("maryjane_01");
			G_HEALTH_BAR_TEXTURES[3] = Spool_FindTextureEntry("maryJane_bar");
			G_HEALTH_BAR_TEXTURES[4] = Spool_FindTextureEntry("venom_wounded");
			break;
		case 308:
			G_HEALTH_BAR_TEXTURES[0] = Spool_FindTextureEntry("dococ");
			G_HEALTH_BAR_TEXTURES[1] = Spool_FindTextureEntry("boss");
			G_HEALTH_BAR_TEXTURES[2] = Spool_FindTextureEntry("docOc_wounded");
			break;
		case 314:
			G_HEALTH_BAR_TEXTURES[0] = Spool_FindTextureEntry("carnage");
			G_HEALTH_BAR_TEXTURES[1] = Spool_FindTextureEntry("boss");
			G_HEALTH_BAR_TEXTURES[2] = Spool_FindTextureEntry("carnage_wounded");
			break;
		default:
			G_HEALTH_BAR_ONE = 0;
			G_HEALTH_BAR_ITEM_TYPE = 0;
			break;
	}
}

// @Ok
void Panel_DestroyHealthBar(void)
{
	G_HEALTH_BAR_ONE = 0;
	G_HEALTH_BAR_TWO = 0;
}

// @Ok
void Panel_DestroyCompass(void)
{
	G_COMPASS_STATUS = 0;
}

// auto_inline off: both overloads below are real out-of-line functions in
// the original binary (0x462B90 and 0x462BB0). The new 4-arg overload
// further down calls the SAnimFrame one, which calls the Texture one; under
// /Ob2 the compiler otherwise inlines both into that call site, turning the
// original's two real "call" instructions into inlined bodies and desyncing
// the whole function. Same fix as ps2funcs.cpp's gsub_46E990 callers.
#pragma auto_inline(off)

// @Ok
int Panel_DrawTexturedPoly(SAnimFrame* pFrame, int a2)
{
	return Panel_DrawTexturedPoly(pFrame->pTexture, a2);
}

// @Ok
// @Matching
int Panel_DrawTexturedPoly(Texture* pTexture, int a2)
{
	if (!pTexture)
	{
		return 0;
	}

	print_if_false(a2 < 0x1000, "Panel_DrawTexturedPoly");

	if ((u8*)G_PPOLY + sizeof(POLY_FT4) > G_POLY_BUFFER_END)
	{
		return 0;
	}

	POLY_FT4* p = (POLY_FT4*)G_PPOLY;
	G_PPOLY = (u32*)((u8*)G_PPOLY + sizeof(POLY_FT4));

	p->tag = 0x09000000;
	*(u32*)&p->r0 = 0x2C808080;

	u32 u0v0clut = *(u32*)&pTexture->u0;
	u32 u1v1tpage = *(u32*)&pTexture->u1;
	u32 u2v2u3v3 = *(u32*)&pTexture->u2;
	u16 u3v3 = *(u16*)&pTexture->u3;

	*(u32*)&p->u0 = u0v0clut;
	*(u32*)&p->u1 = u1v1tpage;
	*(u32*)&p->u2 = u2v2u3v3;
	*(u16*)&p->u3 = u3v3;

	gsub_46CB90(G_RENDER_BUF);

	return (int)p;
}

#pragma auto_inline(on)

// real translation, 0x00462B30, 94 bytes, called from PShell_DrawMenuBox
// (pshell.cpp) with (frame, x - 14, someY, sort). Builds on the 2-arg
// overload right above: gets the poly from Panel_DrawTexturedPoly(pFrame,
// sort), then writes an (x,y) positioned quad sized by pFrame->Width/Height,
// same shape as Panel_SetStretchedScreenCoords. No null check on the
// returned poly pointer before the field stores, matching the original.
// Needed #pragma auto_inline(off) around the two overloads above (they
// were getting inlined into the call here under /Ob2, turning the
// original's real "call" into inlined code). Also needed the x1/y2 sums as
// named i16 locals, declared between the y0 and y1 stores: this makes the
// compiler evaluate the Width+x sum before storing y1 (and the Height+y sum
// before the "add esp,8" call cleanup), matching the original's
// instruction scheduling. i32 locals in the same spot gave a completely
// different (byte-loaded, 32-bit add) shape; only i16 reproduced the
// movzx+16-bit-add pattern the original uses. cmpsum: 0 mnemonic diffs,
// only remaining byte diff is the relocated call target.
// @Ok
// @Matching
int Panel_DrawTexturedPoly(SAnimFrame* pFrame, i32 x, i32 y, i32 sort)
{
	POLY_FT4* p = (POLY_FT4*)Panel_DrawTexturedPoly(pFrame->pTexture, sort);

	p->x0 = (i16)x;
	p->y0 = (i16)y;
	i16 x1 = (i16)(pFrame->Width + x);
	p->y1 = (i16)y;
	p->x1 = x1;
	p->x2 = (i16)x;
	i16 y2 = (i16)(pFrame->Height + y);
	p->y2 = y2;
	p->x3 = (i16)(pFrame->Width + x);
	p->y3 = (i16)(pFrame->Height + y);

	return (int)p;
}

void validate_SAnimFrame(void)
{
	VALIDATE_SIZE(SAnimFrame, 0x8);

	VALIDATE(SAnimFrame, OffX, 0x0);
	VALIDATE(SAnimFrame, OffY, 0x1);
	VALIDATE(SAnimFrame, Width, 0x2);
	VALIDATE(SAnimFrame, Height, 0x3);
	VALIDATE(SAnimFrame, pTexture, 0x4);
}

void validate_Texture(void)
{
	VALIDATE_SIZE(Texture, 0x2C);

	VALIDATE(Texture, u0, 0x0);
	VALIDATE(Texture, v0, 0x1);

	VALIDATE(Texture, clut, 0x2);

	VALIDATE(Texture, u1, 0x4);
	VALIDATE(Texture, v1, 0x5);

	VALIDATE(Texture, tpage, 0x6);

	VALIDATE(Texture, u2, 0x8);
	VALIDATE(Texture, v2, 0x9);

	VALIDATE(Texture, u3, 0xA);
	VALIDATE(Texture, v3, 0xB);

	VALIDATE(Texture, TexWin, 0xC);
	VALIDATE(Texture, Usage, 0x10);
	VALIDATE(Texture, field_12, 0x12);
	VALIDATE(Texture, Checksum, 0x14);

	VALIDATE(Texture, pPalette, 0x18);
	VALIDATE(Texture, x, 0x1C);
	VALIDATE(Texture, y, 0x1E);

	VALIDATE(Texture, pNext, 0x20);
	VALIDATE(Texture, pPrevious, 0x24);
	VALIDATE(Texture, mRegion, 0x28);
}

void validate_POLY_FT4(void)
{
	VALIDATE_SIZE(POLY_FT4, 0x28);

	VALIDATE(POLY_FT4, tag, 0x0);

	VALIDATE(POLY_FT4, r0, 0x4);
	VALIDATE(POLY_FT4, g0, 0x5);
	VALIDATE(POLY_FT4, b0, 0x6);

	VALIDATE(POLY_FT4, code, 0x7);

	VALIDATE(POLY_FT4, x0, 0x8);
	VALIDATE(POLY_FT4, y0, 0xA);

	VALIDATE(POLY_FT4, u0, 0xC);
	VALIDATE(POLY_FT4, v0, 0xD);

	VALIDATE(POLY_FT4, clut, 0xE);

	VALIDATE(POLY_FT4, x1, 0x10);
	VALIDATE(POLY_FT4, y1, 0x12);

	VALIDATE(POLY_FT4, u1, 0x14);
	VALIDATE(POLY_FT4, v1, 0x15);

	VALIDATE(POLY_FT4, tpage, 0x16);

	VALIDATE(POLY_FT4, x2, 0x18);
	VALIDATE(POLY_FT4, y2, 0x1A);

	VALIDATE(POLY_FT4, x3, 0x20);
	VALIDATE(POLY_FT4, y3, 0x22);

	VALIDATE(POLY_FT4, u3, 0x24);
	VALIDATE(POLY_FT4, v3, 0x25);
}

void validate_POLY_GT4(void)
{
	VALIDATE_SIZE(POLY_GT4, 0x34);

	VALIDATE(POLY_GT4, tag, 0x0);

	VALIDATE(POLY_GT4, r0, 0x4);
	VALIDATE(POLY_GT4, g0, 0x5);
	VALIDATE(POLY_GT4, b0, 0x6);

	VALIDATE(POLY_GT4, code, 0x7);

	VALIDATE(POLY_GT4, x0, 0x8);
	VALIDATE(POLY_GT4, y0, 0xA);

	VALIDATE(POLY_GT4, u0, 0xC);
	VALIDATE(POLY_GT4, v0, 0xD);

	VALIDATE(POLY_GT4, clut, 0xE);

	VALIDATE(POLY_GT4, r1, 0x10);
	VALIDATE(POLY_GT4, g1, 0x11);
	VALIDATE(POLY_GT4, b1, 0x12);

	VALIDATE(POLY_GT4, x1, 0x14);
	VALIDATE(POLY_GT4, y1, 0x16);

	VALIDATE(POLY_GT4, u1, 0x18);
	VALIDATE(POLY_GT4, v1, 0x19);

	VALIDATE(POLY_GT4, tpage, 0x1A);

	VALIDATE(POLY_GT4, r2, 0x1C);
	VALIDATE(POLY_GT4, g2, 0x1D);
	VALIDATE(POLY_GT4, b2, 0x1E);

	VALIDATE(POLY_GT4, x2, 0x20);
	VALIDATE(POLY_GT4, y2, 0x22);

	VALIDATE(POLY_GT4, u2, 0x24);
	VALIDATE(POLY_GT4, v2, 0x25);

	VALIDATE(POLY_GT4, r3, 0x28);
	VALIDATE(POLY_GT4, g3, 0x29);
	VALIDATE(POLY_GT4, b3, 0x2A);

	VALIDATE(POLY_GT4, x3, 0x2C);
	VALIDATE(POLY_GT4, y3, 0x2E);

	VALIDATE(POLY_GT4, u3, 0x30);
	VALIDATE(POLY_GT4, v3, 0x31);
}

void validate_POLY_F4(void)
{
	VALIDATE_SIZE(POLY_F4, 0x18);

	VALIDATE(POLY_F4, tag, 0x0);

	VALIDATE(POLY_F4, r0, 0x4);
	VALIDATE(POLY_F4, g0, 0x5);
	VALIDATE(POLY_F4, b0, 0x6);

	VALIDATE(POLY_F4, code, 0x7);

	VALIDATE(POLY_F4, x0, 0x8);
	VALIDATE(POLY_F4, y0, 0xA);

	VALIDATE(POLY_F4, x1, 0xC);
	VALIDATE(POLY_F4, y1, 0xE);

	VALIDATE(POLY_F4, x2, 0x10);
	VALIDATE(POLY_F4, y2, 0x12);

	VALIDATE(POLY_F4, x3, 0x14);
	VALIDATE(POLY_F4, y3, 0x16);
}

#include "compatibility/my_patch.h"

// @Bogus
void patch_panel(void)
{
	// Not hooked, and why:
	//
	// Panel_Display (0x4658C0) and Panel_DisplayTimer (0x461D00) read and
	// write the l1a3bomb.cpp countdown globals (gBombDieRelatedOne 0x60F771,
	// gBombDieRelatedTwo 0x60F772, gBombAIRelated 0x60F774,
	// gBombDieTimerRelated 0x60F778, gBombRelated 0x54E8D4) and Panel_Display
	// also reads spidey.cpp's gSpideyAnim / gSpideyAnimTwo (0x60F750 /
	// 0x60F754). Those are still plain repo globals owned by other files, and
	// their writers (CL1A3Bomb::AI, Training_MonitorLevel,
	// Spidey_LoadAlternativeHealthIcon, CPlayer::CPlayer) are not hooked, so
	// our copies would stay zero. They need G_* macros in l1a3bomb.h and
	// spidey.h first.
	//
	// gsub_4015B0 (0x4015B0) is a single "ret", too short for a 6 byte hook.
	//
	// PanelDisp_DrawIcon, PanelCompass_DrawNeedleHalf and
	// PanelHB_DrawIconOverlay are our own helpers, the original repeats them
	// inline, so there is no address to hook.
	PATCH_PUSH_RET(0x00461CA0, Panel_Init);

	PATCH_PUSH_RET_POLY(0x004624A0, DCPanel_DrawTexturedPoly, "?DCPanel_DrawTexturedPoly@@YAXMPAUPOLY_FT4@@PBUSAnimFrame@@I@Z");
	PATCH_PUSH_RET_POLY(0x004626A0, DCPanel_DrawTexturedPoly, "?DCPanel_DrawTexturedPoly@@YAXMPAUPOLY_FT4@@PBUSAnimFrame@@HHHHHI@Z");
	PATCH_PUSH_RET_POLY(0x00462930, DCPanel_DrawTexturedPoly, "?DCPanel_DrawTexturedPoly@@YAXMPAUPOLY_FT4@@PBUTexture@@I@Z");

	PATCH_PUSH_RET_POLY(0x00462B30, Panel_DrawTexturedPoly, "?Panel_DrawTexturedPoly@@YAHPAUSAnimFrame@@HHH@Z");
	PATCH_PUSH_RET_POLY(0x00462B90, Panel_DrawTexturedPoly, "?Panel_DrawTexturedPoly@@YAHPAUSAnimFrame@@H@Z");
	PATCH_PUSH_RET_POLY(0x00462BB0, Panel_DrawTexturedPoly, "?Panel_DrawTexturedPoly@@YAHPAUTexture@@H@Z");

	PATCH_PUSH_RET_POLY(0x00462C30, Panel_SetStretchedScreenCoords, "?Panel_SetStretchedScreenCoords@@YAXHHPAUPOLY_FT4@@PAUSAnimFrame@@HH@Z");
	PATCH_PUSH_RET_POLY(0x00462CD0, Panel_SetStretchedScreenCoords, "?Panel_SetStretchedScreenCoords@@YAXHHPAUPOLY_FT4@@PBUTexture@@HH@Z");

	PATCH_PUSH_RET(0x00462D60, DCPanel_DrawFlatShadedPoly);
	PATCH_PUSH_RET(0x00462EC0, Panel_DrawFlatShadedPoly);

	PATCH_PUSH_RET_POLY(0x00462FB0, DCDrawGouraudPoly, "?DCDrawGouraudPoly@@YAXMHHHHIIII@Z");
	PATCH_PUSH_RET_POLY(0x004631C0, DCDrawGouraudPoly, "?DCDrawGouraudPoly@@YAXMPAUPOLY_GT4@@PAUTexture@@H@Z");

	PATCH_PUSH_RET(0x00463610, Panel_CreateHealthBar);
	PATCH_PUSH_RET(0x004637F0, Panel_DestroyHealthBar);
	PATCH_PUSH_RET(0x00463800, Panel_CreateCompass);
	PATCH_PUSH_RET(0x00463850, Panel_DestroyCompass);
	PATCH_PUSH_RET(0x00463860, Panel_DisplayCompass);
	PATCH_PUSH_RET(0x00464270, Panel_DisplayHealthBar);

	// The out of line debug print helper at 0x46CB90 that this file owns.
	PATCH_PUSH_RET(0x0046CB90, gsub_46CB90);
}
