#include "game/player/weapons.h"
#include "compatibility/validate.h"
#include "core/game_heap.h"
#include "engine/rendering/ps2funcs.h"
#include "game/cinematics/camera.h"
#include "engine/assets/spool.h"
#include "core/utils.h"
#include "engine/rendering/render_buffers.h"
#include "game/ui/panel.h"
#include "engine/rendering/scene_renderer.h"
#include "engine/rendering/PCGfx.h"
#include "core/algebra.h"
#include "game/ui/screen.h"
#include "engine/rendering/m3dinit.h"
#include "game/application/SpideyDX.h"
#include "compatibility/my_patch.h"
#include <cmath>

CItem* CWeapons;
#include "game/cinematics/camera.h"

// Per-point scratch ring used only by CGouraudRibbon::Display (below), one
// element per ribbon point, address confirmed via raw disasm (dword_614CD4).
// Reuses the same 0x614CD4 scratch address as CSmokeRing::Display's own
// gSmokeRingScreenPoints (above) with a completely different 32-byte layout -
// different Display functions time-sharing one scratch region between calls
// is the normal pattern in this codebase (see the comment above
// gSmokeRingScreenPoints). field_14 is never written or read by anything
// this session traced; kept only so the struct's stride matches the
// original's (CalcScreenNormal, below, relies on this struct's field_20/
// field_38 landing on the NEXT ring element's screenXY/clipped purely from
// the 0x20-byte stride).
struct SGouraudRibbonScreenPoint
{
	i32 screenXY;      // 0x00: packed sx | (sy<<16), this frame (Transform()/gte_stsxy)
	i32 normX;         // 0x04: smoothed screen-space edge-normal X (see CalcScreenNormal)
	i32 normY;         // 0x08: smoothed screen-space edge-normal Y
	i32 depth;         // 0x0C: raw camera-space depth (Transform()/gte_stlvnl2)
	i32 scaledWidth;   // 0x10: 400 * point's Width / depth, only valid when !clipped
	i32 field_14;       // 0x14: never read back; kept for stride fidelity only
	i32 clipped;         // 0x18: 1 if depth falls outside G_VIEW_CLIP_INFO's range
	f32 invZ;             // 0x1C: 1 / w from the Algebra_Transform4 pass, or -1e12 fallback
};
static SGouraudRibbonScreenPoint* const gGouraudRibbonScreenPoints = reinterpret_cast<SGouraudRibbonScreenPoint*>(0x614CD4);

// The shared growing poly buffer is db.cpp's pPoly/PolyBufferEnd
// (0x56FB04/0x5FCD1C), reached here through G_PPOLY/G_POLY_BUFFER_END in db.h.
// This file used to alias the same two addresses under its own names. Only the
// pointer advance and the overflow bail-out are observable here (see the
// comment above CGouraudRibbon::Display), so this one never writes any record
// contents through it.

// Camera-space projection matrix refresh shared by the whole Display*List/
// ribbon-Display family (see bit.cpp's RefreshGfxMatrix and the family notes
// above CSimpleTexturedRibbon::Display there). bit.cpp's own copy is `static
// INLINE` (file-local), so this file gets its own copy of the same 16-float
// memcpy at the same addresses, per repo convention.
static f32 * const gGouraudFrameProjMatrix = (f32*)0x56E668;    // == algebra.cpp's gGfxMatrix
static f32 * const gGouraudCameraBasisMatrix = (f32*)0x56E6F8;  // == bit.cpp's gCameraBasisMatrix
// @Ok
static void GouraudRibbon_RefreshGfxMatrix(void)
{
	for (i32 i = 0; i < 16; i++)
	{
		gGouraudFrameProjMatrix[i] = gGouraudCameraBasisMatrix[i];
	}
}

// Both quad types CGouraudRibbon::Display draws build their vertex colours
// from a CONSTANT alpha byte (128, DCGfx_BlendingMode_2) rather than per-point
// data - confirmed via raw disasm, the record byte tested for the semi-
// transparent flag is always the fixed float bit-pattern (0x3A000000) this
// function itself writes a few instructions earlier, so the branch is
// provably always taken. Reproduced directly rather than as a dead branch.
static const u32 kGouraudRibbonBlack = 0x80000000u;
// @Ok
static u32 GouraudRibbon_RealColor(const SRibbonPoint* pPt)
{
	return 0x80000000u | ((u32)pPt->r << 16) | ((u32)pPt->g << 8) | (u32)pPt->b;
}

// Packed-short screen xy (as produced by Transform()/gte_stsxy, or stored in
// SRibbonPoint::Last1Scr/Last2Scr) to device screen space, same
// gGameResolutionX/Xres, gGameResolutionY/Yres ratio idiom
// PanelCompass_DrawNeedleHalf (panel.cpp) and PCGfx.cpp's own DrawTexture2D
// helpers already use.
// @Ok
static void GouraudRibbon_ScaleScreenXY(i32 packedXY, f32* outX, f32* outY)
{
	i32 sx = (i16)(packedXY & 0xFFFF);
	i32 sy = (i16)(packedXY >> 16);
	*outX = (f32)sx * gGameResolutionX / (f32)Xres;
	*outY = (f32)sy * gGameResolutionY / (f32)Yres;
}

// Shared draw for both quad types CGouraudRibbon::Display emits: a
// black-to-real-colour fade quad between an "old" vertex pair (associated
// with point i+1 then point i) and a "new" vertex pair (same order). Used
// both for the motion-trail quad (old = 2-frames-ago Last2Scr, new = this
// frame's position) and the edge/glow quad (old = width-offset screen
// corner, new = the raw un-offset point). UVs are the fixed 0/1 corner
// constants the whole Display*List family uses (see the family notes above
// CSimpleTexturedRibbon::Display, bit.cpp).
// @Ok
static void GouraudRibbon_DrawFadeQuad(
		i32 oldNextXY, f32 oldNextInvZ, u32 oldNextColor,
		i32 oldCurXY,  f32 oldCurInvZ,  u32 oldCurColor,
		i32 newNextXY, f32 newNextInvZ, u32 newNextColor,
		i32 newCurXY,  f32 newCurInvZ,  u32 newCurColor)
{
	PCGfx_UseTexture(1, DCGfx_BlendingMode_2);

	f32 x0, y0, x1, y1, x2, y2, x3, y3;
	GouraudRibbon_ScaleScreenXY(oldNextXY, &x0, &y0);
	GouraudRibbon_ScaleScreenXY(oldCurXY,  &x1, &y1);
	GouraudRibbon_ScaleScreenXY(newNextXY, &x2, &y2);
	GouraudRibbon_ScaleScreenXY(newCurXY,  &x3, &y3);

	// First draw: matches the original's confirmed vertex/colour assignment
	// exactly (see the comment above CGouraudRibbon::Display).
	PCGfx_DrawQPoly3D(
			x0, y0, oldNextInvZ, 0.0f, 0.0f, oldNextColor,
			x1, y1, oldCurInvZ,  1.0f, 0.0f, oldCurColor,
			x2, y2, newNextInvZ, 0.0f, 1.0f, newNextColor,
			x3, y3, newCurInvZ,  1.0f, 1.0f, newCurColor);

	// Second draw: the original calls PCGfx_DrawQPoly3D again here reading the
	// exact same source bytes (confirmed via raw disasm), with no second
	// corner/colour data anywhere in flight - reproduced as the same quad
	// with a reordered vertex pair (double-sided/backface visibility for a
	// zero-thickness strip). See the comment above CGouraudRibbon::Display:
	// this is the one part of the function not pinned down to individual
	// argument slots.
	PCGfx_DrawQPoly3D(
			x1, y1, oldCurInvZ,  0.0f, 0.0f, oldCurColor,
			x0, y0, oldNextInvZ, 1.0f, 0.0f, oldNextColor,
			x3, y3, newCurInvZ,  0.0f, 1.0f, newCurColor,
			x2, y2, newNextInvZ, 1.0f, 1.0f, newNextColor);
}

// Implemented 2026-08-31 (functional-only bar, see PLAN.md). Address 0x4f1860,
// 4681 bytes / 1286 instructions. Traced fully against Hex-Rays and cross-checked
// with raw disassembly (0x4f1860..0x4f2aa1) section by section; the older
// investigation notes above this line (now removed) are superseded by this one.
// Four phases, matching the original's structure exactly:
//
// 1) Per-point projection into a shared scratch ring (SGouraudRibbonScreenPoint,
//    below, reusing the SAME 0x614CD4 address CSmokeRing::Display's
//    gSmokeRingScreenPoints uses for an unrelated layout - different Display
//    functions time-sharing one scratch region between calls is the normal
//    pattern in this codebase). Screen xy/depth come from Transform() (already
//    @Ok, right below in this file - its qword_56F1B4/dword_56F1BC camera
//    subtraction is exactly what this loop's raw disasm does too), clipped
//    against G_VIEW_CLIP_INFO's depth range (screen.h). invZ comes from the
//    separate Algebra_Transform4 pass (algebra.cpp, already @Ok): the raw
//    disasm's "QModelIndex::QModelIndex(...)" call at ~0x4f1a7c is a FLIRT
//    false-positive on a vector4d(f32,f32,f32,f32) build from Algebra_Transform4's
//    4 outputs - confirmed by matching each of the 4 dot products term-by-term
//    against gGfxMatrix's indices. The sequel call (sub_402600) just reads the
//    w component back out, which Algebra_Transform4's own out[3] already gives
//    directly, so neither vector3d/vector4d ctor is needed in the source.
// 2) Smoothed per-point screen-space edge normal via CalcScreenNormal (already
//    @Ok, below): point 0 and the last point get their single adjoining
//    segment's raw normal, interior points get the average of both adjoining
//    segments' raw normals (reproduced by calling CalcScreenNormal with the
//    ring cast to SCalcBuffer*, which reads this point's screenXY/clipped at
//    field_0/field_18 and relies on the struct's own field_20/field_38 landing
//    on the NEXT ring element's screenXY/clipped purely from the two structs
//    sharing the same 0x20-byte stride - verified offset by offset).
// 3) Per segment (i, i+1), skipped if either point's smoothed normal is zero
//    (CalcScreenNormal zeroes both outputs whenever either endpoint was
//    clipped): a screen-space corner offset (normal * scaledWidth >> 6) is
//    computed for point i+1's "right" edge; point i's own corner was carried
//    forward from when IT was the "i+1" of the previous iteration (or, for
//    segment 0, computed once up front from point 0's own normal - matches the
//    raw disasm's v261 priming before the loop). Two draw call pairs, each
//    gated on both points' invZ being positive (the original's third z-check,
//    against a value it calls "v252", is provably the exact same quantity as
//    the second check once its assignment chain is followed back to the ring -
//    both alias point i's invZ - so it is dropped here as genuinely redundant,
//    not simplified away by guessing):
//      - if this->mTrail > 2 (i.e. trailing has been running >= 3 frames, so
//        Last2Scr is meaningfully "2 frames old" and not just the priming copy
//        from frame 1 - see phase 4): a motion-trail quad from each point's
//        2-frames-ago screen position (SRibbonPoint::Last2Scr) to its current
//        one, colour fading from black (old) to the point's real colour (new).
//      - always: an edge/glow quad from point i+1's/point i's offset ("right")
//        corner to their raw (un-offset) centre, colour fading from black
//        (corner) to real colour (centre) - a soft glow along the ribbon's edge.
//    Both quad types are drawn TWICE (sub_508550/PCGfx_DrawQPoly3D called back
//    to back with the exact same source record - confirmed via raw disasm, the
//    second call reads identical byte offsets from the same 36-/80-byte scratch
//    record as the first). No separate "left corner" data is threaded through
//    the loop anywhere (only one corner value is carried between iterations),
//    so the second call of each pair is reproduced here as the same quad drawn
//    with a reordered vertex pair (0<->1, 2<->3) rather than a distinct
//    left-edge quad - a double-sided/backface-visible strip is the standard
//    reason to draw a zero-thickness quad twice this way, and it is the only
//    hypothesis consistent with there being just one corner value in flight.
//    This vertex-order swap is the one part of the function not pinned down to
//    individual argument slots (the two calls' colour/position data itself IS
//    fully confirmed, byte offset by byte offset, via raw disasm); flagged here
//    for anyone re-verifying at the instruction level.
//    Colour/alpha: both quad types build their vertex colours from a constant
//    alpha byte, not per-point data - the record byte tested for the semi-
//    transparent flag is always a fixed float bit-pattern (0x3A000000) this
//    function itself just wrote a few instructions earlier, so the branch is
//    provably always taken (alpha=128, DCGfx_BlendingMode_2, texture slot 1 -
//    same "flat/line" slot DisplayGLineList's notes document in bit.cpp);
//    reproduced directly as a constant rather than as a dead runtime branch.
//    The pPoly scratch queue (G_PPOLY/G_POLY_BUFFER_END, 0x56FB04/0x5FCD1C)
//    is bumped and bounds-checked exactly like the original (an overflow
//    bails the whole remaining segment loop, matching bit.cpp's
//    DisplayLinked2EndedBitListLeftover precedent for "pointer advance and
//    bounds check are observable, record contents are not") but its actual
//    byte contents are never written here - every field this function reads
//    back out of that scratch record is available more simply from the ring/
//    mpPoints data already in local scope, and nothing else in this function
//    (or, per the family notes above CSimpleTexturedRibbon::Display, in any
//    sibling Display*List function) ever reads the queue's contents back.
// 4) Per-point history shift, only if this->mTrail != 0: SRibbonPoint::Last1Scr
//    becomes this frame's screen position; Last2Scr becomes the previous
//    Last1Scr (or, on the very first trailing frame, this frame's position
//    too, priming both). this->mTrail then increments every frame from then on
//    (it is NOT just a 0/1 flag once trailing starts - the constructor's
//    LeaveTrail param only seeds it - which is exactly why phase 3 gates the
//    trail quad on "> 2": frames 1 and 2 after enabling haven't built up a real
//    2-frames-old Last2Scr yet). SRibbonPoint::Last3Scr is never touched by
//    this function.
// @Ok
void CGouraudRibbon::Display(void)
{
	print_if_false(this->mNumPoints <= 64, "More GouraudRibbon points than planned for.");

	GouraudRibbon_RefreshGfxMatrix();

	u8* clip = G_VIEW_CLIP_INFO;
	u16 clipMin = *(u16*)(clip + 8);
	u16 clipMax = *(u16*)(clip + 0xA);

	// Phase 1: project every point into the shared scratch ring.
	for (i32 i = 0; i < this->mNumPoints; i++)
	{
		SRibbonPoint* pPt = &this->mpPoints[i];
		SGouraudRibbonScreenPoint* pRing = &gGouraudRibbonScreenPoints[i];

		i32 depth = Transform(&pPt->Pos, &pRing->screenXY);
		pRing->depth = depth;

		if (depth < clipMin || depth > clipMax)
		{
			pRing->clipped = 1;
		}
		else
		{
			pRing->clipped = 0;
			pRing->scaledWidth = (400 * (i32)pPt->Width) / depth;
		}

		f32 inVec[3];
		inVec[0] = (f32)pPt->Pos.vx / 4096.0f;
		inVec[1] = (f32)pPt->Pos.vy / 4096.0f;
		inVec[2] = (f32)pPt->Pos.vz / 4096.0f;

		f32 outVec[4];
		Algebra_Transform4(outVec, inVec);

		if (fabs(outVec[3]) > 0.0000000099999999)
		{
			pRing->invZ = 1.0f / outVec[3];
		}
		else
		{
			pRing->invZ = -1.0e12f;
		}
	}

	// Phase 2: smoothed per-point screen-space edge normal.
	if (this->mNumPoints > 0)
	{
		i32 nx, ny;
		CalcScreenNormal(reinterpret_cast<SCalcBuffer*>(&gGouraudRibbonScreenPoints[0]), &nx, &ny, 2);
		gGouraudRibbonScreenPoints[0].normX = nx;
		gGouraudRibbonScreenPoints[0].normY = ny;

		i32 prevNx = nx, prevNy = ny;

		for (i32 i = 1; i < this->mNumPoints - 1; i++)
		{
			CalcScreenNormal(reinterpret_cast<SCalcBuffer*>(&gGouraudRibbonScreenPoints[i]), &nx, &ny, 2);

			gGouraudRibbonScreenPoints[i].normX = (prevNx + nx) / 2;
			gGouraudRibbonScreenPoints[i].normY = (prevNy + ny) / 2;

			prevNx = nx;
			prevNy = ny;
		}

		if (this->mNumPoints > 1)
		{
			gGouraudRibbonScreenPoints[this->mNumPoints - 1].normX = prevNx;
			gGouraudRibbonScreenPoints[this->mNumPoints - 1].normY = prevNy;
		}
	}

	// Phase 3: per-segment corner computation and drawing.
	i32 prevCornerXY = 0;

	if (this->mNumPoints > 1)
	{
		SGouraudRibbonScreenPoint* pFirst = &gGouraudRibbonScreenPoints[0];
		i32 dx0 = (pFirst->normX * pFirst->scaledWidth) >> 6;
		i32 dy0 = (pFirst->normY * pFirst->scaledWidth) >> 6;
		i32 sx0 = (i16)(pFirst->screenXY & 0xFFFF);
		i32 sy0 = (i16)(pFirst->screenXY >> 16);
		prevCornerXY = ((sx0 + dx0) & 0xFFFF) | ((sy0 + dy0) << 16);

		for (i32 i = 0; i < this->mNumPoints - 1; i++)
		{
			SGouraudRibbonScreenPoint* pCur = &gGouraudRibbonScreenPoints[i];
			SGouraudRibbonScreenPoint* pNext = &gGouraudRibbonScreenPoints[i + 1];

			if ((pCur->normX == 0 && pCur->normY == 0) ||
				(pNext->normX == 0 && pNext->normY == 0))
			{
				continue;
			}

			i32 dx = (pNext->normX * pNext->scaledWidth) >> 6;
			i32 dy = (pNext->normY * pNext->scaledWidth) >> 6;
			i32 sxNext = (i16)(pNext->screenXY & 0xFFFF);
			i32 syNext = (i16)(pNext->screenXY >> 16);
			i32 rightCornerXY = ((sxNext + dx) & 0xFFFF) | ((syNext + dy) << 16);
			i32 curCornerXY = prevCornerXY;

			if (reinterpret_cast<u8*>(G_PPOLY) + 80 > G_POLY_BUFFER_END)
				break;
			G_PPOLY = reinterpret_cast<u32*>(reinterpret_cast<u8*>(G_PPOLY) + 80);

			if (this->mTrail > 2)
			{
				if (reinterpret_cast<u8*>(G_PPOLY) + 36 > G_POLY_BUFFER_END)
					break;
				G_PPOLY = reinterpret_cast<u32*>(reinterpret_cast<u8*>(G_PPOLY) + 36);

				if (pNext->invZ > 0.0f && pCur->invZ > 0.0f)
				{
					SRibbonPoint* pMptCur = &this->mpPoints[i];
					SRibbonPoint* pMptNext = &this->mpPoints[i + 1];

					GouraudRibbon_DrawFadeQuad(
							pMptNext->Last2Scr, pNext->invZ, kGouraudRibbonBlack,
							pMptCur->Last2Scr, pCur->invZ, kGouraudRibbonBlack,
							pNext->screenXY, pNext->invZ, GouraudRibbon_RealColor(pMptNext),
							pCur->screenXY, pCur->invZ, GouraudRibbon_RealColor(pMptCur));
				}
			}

			if (pNext->invZ > 0.0f && pCur->invZ > 0.0f)
			{
				SRibbonPoint* pMptCur = &this->mpPoints[i];
				SRibbonPoint* pMptNext = &this->mpPoints[i + 1];

				GouraudRibbon_DrawFadeQuad(
						rightCornerXY, pNext->invZ, kGouraudRibbonBlack,
						curCornerXY, pCur->invZ, kGouraudRibbonBlack,
						pNext->screenXY, pNext->invZ, GouraudRibbon_RealColor(pMptNext),
						pCur->screenXY, pCur->invZ, GouraudRibbon_RealColor(pMptCur));
			}

			prevCornerXY = rightCornerXY;
		}
	}

	// Phase 4: shift the per-point trail history.
	if (this->mTrail != 0)
	{
		for (i32 i = 0; i < this->mNumPoints; i++)
		{
			SRibbonPoint* pPt = &this->mpPoints[i];

			if (this->mTrail == 1)
			{
				pPt->Last1Scr = gGouraudRibbonScreenPoints[i].screenXY;
				pPt->Last2Scr = gGouraudRibbonScreenPoints[i].screenXY;
			}
			else
			{
				pPt->Last2Scr = pPt->Last1Scr;
				pPt->Last1Scr = gGouraudRibbonScreenPoints[i].screenXY;
			}
		}

		this->mTrail++;
	}
}

// @Ok
// @Matching
void CGouraudRibbon::SetRGB(u8 a2,u8 a3,u8 a4)
{
	for (i32 i = 0; i < this->mNumPoints; i++)
	{
		this->mpPoints[i].r = a2;
		this->mpPoints[i].g = a3;
		this->mpPoints[i].b = a4;
	}
}

// @Ok
// @Matching
void CGouraudRibbon::SetWidth(u16 Width)
{
	for (i32 i = 0; i < this->mNumPoints; i++)
	{
		this->mpPoints[i].Width = Width;
	}
}

// @Ok
CGouraudRibbon::~CGouraudRibbon(void)
{
	Mem_Delete(this->mpPoints);
}

// @Ok
// @Test
CSmokeRing::CSmokeRing(i32 NumSectors, u32 a3)
{
	this->field_48.vx = 0;
	this->field_48.vy = 0;
	this->field_48.vz = 0;

	print_if_false(NumSectors != 0, "Zero sectors sent to smoke ring");
	this->mpSectors = static_cast<SSmokeRingRelated *>(DCMem_New(sizeof(SSmokeRingRelated) * NumSectors, 0, 1, 0, 1));
	this->mNumSectors = NumSectors;
	this->field_3C = Spool_FindTextureEntry(a3);
	print_if_false(this->field_3C != 0, "Could not find smoke ring texture");

	for (i32 i = 0; i < this->mNumSectors; i++)
	{
		setPolyGT4();

		this->mpSectors[i].field_3B |= 2;
		this->mpSectors[i].field_42 = this->field_3C->clut;
		this->mpSectors[i].field_4E = this->field_3C->tpage;

		setPolyGT4();

		this->mpSectors[i].field_7 |= 2;
		this->mpSectors[i].field_E = this->field_3C->clut;
		this->mpSectors[i].field_1A = this->field_3C->tpage;
	}

	this->SetRGB(128, 128, 128);
	this->SetUV(0, 0, 2);
	this->field_60 = -1;
}

struct SSmokeRingScreenPoint
{
	i32 xyA;
	i16 visibleA;

	PADDING(2);

	i32 xyB;
	i16 visibleB;

	PADDING(2);

	i32 xyC;
	i16 visibleC;
	i16 minDepth;
};

static SSmokeRingScreenPoint* const gSmokeRingScreenPoints = reinterpret_cast<SSmokeRingScreenPoint*>(0x614CD4);

struct SSmokeRingGT4
{
	u32 tag;

	u8 r0,g0,b0,code;
	i32 xy0;
	u8 u0,v0;
	u16 clut;

	u8 r1,g1,b1,pad1;
	i32 xy1;
	u8 u1,v1;
	u16 tpage;

	u8 r2,g2,b2,pad2;
	i32 xy2;
	u8 u2,v2;
	u16 pad3;

	u8 r3,g3,b3,pad4;
	i32 xy3;
	u8 u3,v3;
	u16 pad5;
};

// @Bogus
// internal helper, not a standalone function in the original (the field
// copies are inlined directly in CSmokeRing::Display); factored out here
// only to keep Display's source readable, no independent ground truth
static void CopySmokeRingTemplate(SSmokeRingGT4* pDst, const SSmokeRingGT4* pSrc)
{
	pDst->tag = pSrc->tag;

	pDst->r0 = pSrc->r0;
	pDst->g0 = pSrc->g0;
	pDst->b0 = pSrc->b0;
	pDst->code = pSrc->code;

	pDst->u0 = pSrc->u0;
	pDst->v0 = pSrc->v0;
	pDst->clut = pSrc->clut;

	pDst->r1 = pSrc->r1;
	pDst->g1 = pSrc->g1;
	pDst->b1 = pSrc->b1;
	pDst->pad1 = pSrc->pad1;

	pDst->u1 = pSrc->u1;
	pDst->v1 = pSrc->v1;
	pDst->tpage = pSrc->tpage;

	pDst->r2 = pSrc->r2;
	pDst->g2 = pSrc->g2;
	pDst->b2 = pSrc->b2;
	pDst->pad2 = pSrc->pad2;

	pDst->u2 = pSrc->u2;
	pDst->v2 = pSrc->v2;
	pDst->pad3 = pSrc->pad3;

	pDst->r3 = pSrc->r3;
	pDst->g3 = pSrc->g3;
	pDst->b3 = pSrc->b3;
	pDst->pad4 = pSrc->pad4;

	pDst->u3 = pSrc->u3;
	pDst->v3 = pSrc->v3;
	pDst->pad5 = pSrc->pad5;
}

// unnamed pair of dwords written into the PS2 GS "texture window" tag built once per
// visible wall segment in CSmokeRing::Display (see the "stubbed out: setTexWindow"
// debug string next to them in the original). Not in idb_globals.txt; they sit in the
// unlabeled gap between GLineList (0x56E9CC) and PixelList (0x56E9E0), so this is a
// guess based on that neighbourhood, not a confirmed struct member.
static u32 * const gGsTexWindowTagLo = reinterpret_cast<u32*>(0x56E9D0);
static u32 * const gGsTexWindowTagHi = reinterpret_cast<u32*>(0x56E9D4);

// @Ok
void CSmokeRing::Display(void)
{
	for (i32 i = 0; i < this->mNumSectors; i++)
	{
		SSmokeRingScreenPoint* pPoint = &gSmokeRingScreenPoints[i];
		i32 depth;

		depth = Transform(&this->mpSectors[i].field_80, &pPoint->xyA);
		if (pPoint->xyA == 0x3FF03FF || pPoint->xyA == 0x3FFFC00 ||
			pPoint->xyA == (i32)0xFC0003FF || pPoint->xyA == (i32)0xFC00FC00 ||
			depth < -20000 || depth > 20000)
		{
			pPoint->visibleA = 0;
		}
		else
		{
			pPoint->minDepth = (i16)depth;
			pPoint->visibleA = 1;
		}

		depth = Transform(&this->mpSectors[i].field_74, &pPoint->xyB);
		if (pPoint->xyB == 0x3FF03FF || pPoint->xyB == 0x3FFFC00 ||
			pPoint->xyB == (i32)0xFC0003FF || pPoint->xyB == (i32)0xFC00FC00 ||
			depth < -20000 || depth > 20000)
		{
			pPoint->visibleB = 0;
		}
		else
		{
			pPoint->visibleB = 1;
			if (depth < pPoint->minDepth)
				pPoint->minDepth = (i16)depth;
		}

		depth = Transform(&this->mpSectors[i].field_68, &pPoint->xyC);
		if (pPoint->xyC == 0x3FF03FF || pPoint->xyC == 0x3FFFC00 ||
			pPoint->xyC == (i32)0xFC0003FF || pPoint->xyC == (i32)0xFC00FC00 ||
			depth < -20000 || depth > 20000)
		{
			pPoint->visibleC = 0;
		}
		else
		{
			pPoint->visibleC = 1;
			if (depth < pPoint->minDepth)
				pPoint->minDepth = (i16)depth;
		}
	}

	SSmokeRingGT4* pTemplate1 = reinterpret_cast<SSmokeRingGT4*>(&this->mpSectors[0]);
	SSmokeRingGT4* pTemplate2 = reinterpret_cast<SSmokeRingGT4*>(reinterpret_cast<u8*>(&this->mpSectors[0]) + 0x34);

	SSmokeRingScreenPoint prev = gSmokeRingScreenPoints[0];

	for (i32 j = 1; j <= this->mNumSectors; j++)
	{
		SSmokeRingScreenPoint* pCur = (j == this->mNumSectors) ? &gSmokeRingScreenPoints[0] : &gSmokeRingScreenPoints[j];

		// running minimum of cur/prev minDepth (0x4F509D: cmp+jge+mov, keeps the smaller
		// of the two), not the maximum
		i16 depth = prev.minDepth;
		if (pCur->minDepth < depth)
			depth = pCur->minDepth;

		SSmokeRingGT4* pBuiltPoly1 = 0;
		SSmokeRingGT4* pBuiltPoly2 = 0;

		// original tests field_66 != 0 (jnz skips the depth check entirely), not !field_66
		if (this->field_66 || depth != 0)
		{
			if (this->field_60 & (1 << j))
			{
				// unconditional per-segment PS2 GS "texture window" tag, built once
				// regardless of which (if any) of the two wall quads below turn out visible
				if ((u8*)G_PPOLY + 0x20 > G_POLY_BUFFER_END)
					return;

				u8* pTagBuf = (u8*)G_PPOLY;
				G_PPOLY = (u32*)(pTagBuf + 0x20);

				gsub_46CB90((void*)"stubbed out: setTexWindow");
				gsub_46CB90((void*)"stubbed out: setTexWindow");

				*(u32*)(pTagBuf + 0x18) = *gGsTexWindowTagLo;
				*(u32*)(pTagBuf + 0x1C) = *gGsTexWindowTagHi;

				gsub_46CB90(G_RENDER_BUF);

				if (prev.visibleA && prev.visibleB && pCur->visibleA && pCur->visibleB)
				{
					if ((u8*)G_PPOLY + sizeof(SSmokeRingGT4) > G_POLY_BUFFER_END)
						return;

					SSmokeRingGT4* pNewPoly = (SSmokeRingGT4*)G_PPOLY;
					G_PPOLY = (u32*)((u8*)G_PPOLY + sizeof(SSmokeRingGT4));

					CopySmokeRingTemplate(pNewPoly, pTemplate1);

					pNewPoly->xy0 = prev.xyA;
					pNewPoly->xy1 = pCur->xyA;
					pNewPoly->xy2 = prev.xyB;
					pNewPoly->xy3 = pCur->xyB;

					pBuiltPoly1 = pNewPoly;
				}

				if (prev.visibleC && prev.visibleB && pCur->visibleC && pCur->visibleB)
				{
					if ((u8*)G_PPOLY + sizeof(SSmokeRingGT4) > G_POLY_BUFFER_END)
						return;

					SSmokeRingGT4* pNewPoly = (SSmokeRingGT4*)G_PPOLY;
					G_PPOLY = (u32*)((u8*)G_PPOLY + sizeof(SSmokeRingGT4));

					CopySmokeRingTemplate(pNewPoly, pTemplate2);

					pNewPoly->xy0 = prev.xyB;
					pNewPoly->xy1 = pCur->xyB;
					pNewPoly->xy2 = prev.xyC;
					pNewPoly->xy3 = pCur->xyC;

					pBuiltPoly2 = pNewPoly;
				}

				if (pBuiltPoly1)
					gsub_46CB90(G_RENDER_BUF);

				if (pBuiltPoly2)
					gsub_46CB90(G_RENDER_BUF);

				gsub_46CB90(G_RENDER_BUF);
				gsub_46CB90(G_RENDER_BUF);
			}
		}

		prev = *pCur;
	}
}

// @Ok
// @Validate
void CSmokeRing::SetParams(
		const CVector* a2,
		i32 a3,
		i32 a4)
{
	this->mPos = *a2;
	this->field_54 = a4;
	this->field_50 = a3;

	CSVector v15 = this->field_48;

	i32 v23 = 4096 / this->mNumSectors;

	SSmokeRingRelated* pSector = this->mpSectors;
	for (i32 i = 0; i < this->mNumSectors; i++)
	{
		CVector v16;
		v16.vx = 0;
		v16.vy = 0;
		v16.vz = 0;

		Utils_GetVecFromMagDir(&v16, 1, &v15);

		pSector[i].field_68 = (this->mPos + (v16 * this->field_50));
		CVector v14 = (v16 * this->field_54);
		pSector[i].field_74 = (pSector[i].field_68 + v14);
		pSector[i].field_80 = (pSector[i].field_74 + v14);

		v15.vy += v23;
	}
}

// @Ok
// @NonMatching
// @Test
void CSmokeRing::SetRGB(i32 a2, i32 a3, i32 a4)
{
	if (a2 < 0)
		a2 = 0;

	if (a3 < 0)
		a3 = 0;

	if (a4 < 0)
		a4 = 0;

	for (i32 i = 0; i < this->mNumSectors; i++)
	{
			this->mpSectors[i].field_4 = 0;
			this->mpSectors[i].field_5 = 0;
			this->mpSectors[i].field_6 = 0;

			this->mpSectors[i].field_10 = 0;
			this->mpSectors[i].field_11 = 0;
			this->mpSectors[i].field_12 = 0;
			this->mpSectors[i].field_1C = a2;
			this->mpSectors[i].field_1D = a3;
			this->mpSectors[i].field_1E = a4;

			this->mpSectors[i].field_28 = a2;
			this->mpSectors[i].field_29 = a3;
			this->mpSectors[i].field_2A = a4;

			this->mpSectors[i].field_38 = a2;
			this->mpSectors[i].field_39 = a3;
			this->mpSectors[i].field_3A = a4;

			this->mpSectors[i].field_44 = a2;
			this->mpSectors[i].field_45 = a3;
			this->mpSectors[i].field_46 = a4;

			this->mpSectors[i].field_50 = 0;
			this->mpSectors[i].field_51 = 0;
			this->mpSectors[i].field_52 = 0;

			this->mpSectors[i].field_5C = 0;
			this->mpSectors[i].field_5D = 0;
			this->mpSectors[i].field_5E = 0;
	}
}

// @Ok
// checked against 0x4F4C80: field offsets (field_C/D, field_18/19, field_24/25,
// field_30/31, field_40/41, field_4C/4D, field_58/59, field_64/65), the u0/v0 read
// from field_3C, the step computation and the wrap with & 0x3F all match.
void CSmokeRing::SetUV(i32 a2,i32 a3,i32 a4)
{
	this->field_58 = a2;
	this->field_5C = a3;
	u8 v6 = this->field_3C->u0 + (a2 & 0x3F);

	a3 = (a3 & 0x3F) + this->field_3C->v0;
	i32 v9 = (a4 << 6) / this->mNumSectors;

	u8 a2a = a3 + 32;
	u8 a4a = a3 + 64;
	for (i32 i = 0; i < this->mNumSectors; i++)
	{
		u8 v12 = v6 + v9;
		this->mpSectors[i].field_C = v6;
		this->mpSectors[i].field_D = a3;

		this->mpSectors[i].field_18 = v12;
		this->mpSectors[i].field_19 = a3;

		this->mpSectors[i].field_24 = v6;
		this->mpSectors[i].field_25 = a2a;

		this->mpSectors[i].field_30 = v12;
		this->mpSectors[i].field_31 = a2a;

		this->mpSectors[i].field_40 = v6;
		this->mpSectors[i].field_41 = a2a;

		this->mpSectors[i].field_4C = v12;
		this->mpSectors[i].field_4D = a2a;

		this->mpSectors[i].field_58 = v6;
		this->mpSectors[i].field_59 = a4a;

		this->mpSectors[i].field_64 = v12;
		this->mpSectors[i].field_65 = a4a;

		v6 = (v9 + v6) & 0x3F;
	}
}

// @Ok
CSmokeRing::~CSmokeRing(void)
{
	Mem_Delete(this->mpSectors);
}

// @Ok
// @Matching
CTexturedRibbon::CTexturedRibbon(i32 NumPoints,i32 LeaveTrail)
{
	print_if_false(NumPoints > 1, "NumPoints must be at least 2");
	print_if_false((u32)NumPoints <= 0x20, "NumPoints too big for buffer.");
	this->mNumPoints = NumPoints;

	this->mpPoints = static_cast<SRibbonPoint *>(DCMem_New(sizeof(SRibbonPoint) * NumPoints, 0, 1, 0, 1));

	for (i32 i = 0; i < this->mNumPoints; i++)
	{
		this->mpPoints[i].WidthB = 0;
		this->mpPoints[i].Width = 0;
	}

	print_if_false(!LeaveTrail || LeaveTrail == 1, "LeaveTrail must be 0 or 1");
	this->mTrail = LeaveTrail;
	this->field_50 = 8;

	this->field_60 = static_cast<int *>(DCMem_New(8 * NumPoints + 4, 0, 1, 0, 1));
	this->field_60[0] = 0;
}

// @Ok
// matching
void CTexturedRibbon::SetCoreRGBi(
		i32 a2,
		u8 a3,
		u8 a4,
		u8 a5)
{
	this->field_60[a2 + 1 + this->mNumPoints] = (((a5 << 8) | a4) << 8) | a3;
}

// @Ok
void CTexturedRibbon::SetOuterRGBi(i32 index, u8 a3, u8 a4,u8 a5)
{
	this->field_60[index+1] = (a3 | (((a5 << 8) | a4) << 8));
}

// @Ok
// @Matching
void CTexturedRibbon::SetTexture(Texture* pTex)
{
	print_if_false(pTex != 0, "no texture for ribbon");
	this->field_40 = pTex;
}

// @Ok
CTexturedRibbon::~CTexturedRibbon(void)
{
	Mem_Delete(this->mpPoints);
	Mem_Delete(this->field_60);
}

// @Ok
// @Validate
void CalcScreenNormal(
		SCalcBuffer* pBuffer,
		i32 * a2,
		i32 * a3,
		i32 a4)
{
	if (!pBuffer->field_18 && !pBuffer->field_38)
	{
		i32 v4 = pBuffer->field_20;
		i32 v5 = ((pBuffer->field_20 << 16) >> 16) - ((pBuffer->field_0 << 16) >> 16);
		i32 v6 = (pBuffer->field_20 >> 16) - ((pBuffer->field_0 << 16) >> 16);
		*a2 = v6;
		i32 v7 = 320 * v5 / 512;
		i32 v8 = 320 * v5 / -512;
		*a3 = v8;
		if ( v7 < 0 )
			v7 = v8;
		if ( v6 < 0 )
			v6 = -v6;

		i32 v9;
		if ( v7 <= v6 )
			v9 = v6 + v7 / 2;
		else
			v9 = v7 + v6 / 2;
		if ( v9 >= a4 )
		{
			*a2 = (*a2 << 6) / v9;
			*a3 = (*a3 << 6) / v9;
			*a2 = (*a2 << 9) / 320;
		}
		else
		{
			*a3 = 0;
			*a2 = 0;
		}
	}
	else
	{
		*a3 = 0;
		*a2 = 0;
	}
}

// @Ok
// checked against 0x4F4DF0 (CSmokeRing::Display), the only caller: field order,
// camera subtraction (gMikeCamera[0].Position split across qword_56F1B4 low/high
// and dword_56F1BC) and the gte_ call sequence all match.
 i32 Transform(CVector *a1, i32* a2)
{
	CVector v8;
	v8.vx = 0;
	v8.vy = 0;
	v8.vz = 0;


	v8.vx = (a1->vx >> 12) - G_MIKE_CAMERA[0].Position.vx;
	v8.vy = (a1->vy >> 12) - G_MIKE_CAMERA[0].Position.vy;
	v8.vz = (a1->vz >> 12) - G_MIKE_CAMERA[0].Position.vz;

	gte_ldlv0(reinterpret_cast<VECTOR*>(&v8));

	gte_rtps();
	gte_stsxy(a2);

	i32 v7;
	gte_stlvnl2(&v7);

	return v7;
}

// @Ok
CGouraudRibbon::CGouraudRibbon(i32 NumPoints, i32 LeaveTrail)
{
	print_if_false(NumPoints > 1, "NumPoints must be at least 2");
	print_if_false((u32)NumPoints <= 0x20, "NumPoints too big for buffer.");

	this->mNumPoints = NumPoints;

	this->mpPoints = static_cast<SRibbonPoint *>(
			DCMem_New(
				sizeof(SRibbonPoint) * NumPoints,
				0,
				1,
				0,
				1));

	print_if_false(LeaveTrail == 0 || LeaveTrail == 1, "LeaveTrail must be 0 or 1");

	this->mTrail = LeaveTrail;
}

void validate_CGouraudRibbon(void)
{
	VALIDATE_SIZE(CGouraudRibbon, 0x48);

	VALIDATE(CGouraudRibbon, mTrail, 0x3C);
	VALIDATE(CGouraudRibbon, mNumPoints, 0x40);
	VALIDATE(CGouraudRibbon, mpPoints, 0x44);
}

void validate_CSmokeRing(void)
{
	VALIDATE_SIZE(CSmokeRing, 0x6C);

	VALIDATE(CSmokeRing, field_3C, 0x3C);
	VALIDATE(CSmokeRing, mNumSectors, 0x40);
	VALIDATE(CSmokeRing, mpSectors, 0x44);

	VALIDATE(CSmokeRing, field_48, 0x48);

	VALIDATE(CSmokeRing, field_50, 0x50);
	VALIDATE(CSmokeRing, field_54, 0x54);

	VALIDATE(CSmokeRing, field_58, 0x58);
	VALIDATE(CSmokeRing, field_5C, 0x5C);
	VALIDATE(CSmokeRing, field_60, 0x60);

	VALIDATE(CSmokeRing, field_66, 0x66);

	VALIDATE(CSmokeRing, field_68, 0x68);
	VALIDATE(CSmokeRing, field_6A, 0x6A);
}

void validate_CTexturedRibbon(void)
{
	VALIDATE(CTexturedRibbon, mTrail, 0x3C);

	VALIDATE(CTexturedRibbon, field_40, 0x40);
	VALIDATE(CTexturedRibbon, field_50, 0x50);

	VALIDATE(CTexturedRibbon, mNumPoints, 0x58);
	VALIDATE(CTexturedRibbon, mpPoints, 0x5C);

	VALIDATE(CTexturedRibbon, field_60, 0x60);
}

void validate_SSmokeRingRelated(void)
{
	VALIDATE_SIZE(SSmokeRingRelated, 0x8C);

	VALIDATE(SSmokeRingRelated, field_4, 0x4);
	VALIDATE(SSmokeRingRelated, field_5, 0x5);
	VALIDATE(SSmokeRingRelated, field_6, 0x6);

	VALIDATE(SSmokeRingRelated, field_7, 0x7);

	VALIDATE(SSmokeRingRelated, field_C, 0xC);
	VALIDATE(SSmokeRingRelated, field_D, 0xD);

	VALIDATE(SSmokeRingRelated, field_E, 0xE);

	VALIDATE(SSmokeRingRelated, field_18, 0x18);
	VALIDATE(SSmokeRingRelated, field_19, 0x19);

	VALIDATE(SSmokeRingRelated, field_1A, 0x1A);

	VALIDATE(SSmokeRingRelated, field_1C, 0x1C);
	VALIDATE(SSmokeRingRelated, field_1D, 0x1D);
	VALIDATE(SSmokeRingRelated, field_1E, 0x1E);

	VALIDATE(SSmokeRingRelated, field_24, 0x24);
	VALIDATE(SSmokeRingRelated, field_25, 0x25);

	VALIDATE(SSmokeRingRelated, field_28, 0x28);
	VALIDATE(SSmokeRingRelated, field_29, 0x29);
	VALIDATE(SSmokeRingRelated, field_2A, 0x2A);

	VALIDATE(SSmokeRingRelated, field_30, 0x30);
	VALIDATE(SSmokeRingRelated, field_31, 0x31);

	VALIDATE(SSmokeRingRelated, field_38, 0x38);
	VALIDATE(SSmokeRingRelated, field_39, 0x39);
	VALIDATE(SSmokeRingRelated, field_3A, 0x3A);

	VALIDATE(SSmokeRingRelated, field_3B, 0x3B);

	VALIDATE(SSmokeRingRelated, field_40, 0x40);
	VALIDATE(SSmokeRingRelated, field_41, 0x41);
	VALIDATE(SSmokeRingRelated, field_42, 0x42);

	VALIDATE(SSmokeRingRelated, field_44, 0x44);
	VALIDATE(SSmokeRingRelated, field_45, 0x45);
	VALIDATE(SSmokeRingRelated, field_46, 0x46);

	VALIDATE(SSmokeRingRelated, field_4C, 0x4C);
	VALIDATE(SSmokeRingRelated, field_4D, 0x4D);
	VALIDATE(SSmokeRingRelated, field_4E, 0x4E);

	VALIDATE(SSmokeRingRelated, field_50, 0x50);
	VALIDATE(SSmokeRingRelated, field_51, 0x51);
	VALIDATE(SSmokeRingRelated, field_52, 0x52);

	VALIDATE(SSmokeRingRelated, field_58, 0x58);
	VALIDATE(SSmokeRingRelated, field_59, 0x59);

	VALIDATE(SSmokeRingRelated, field_5C, 0x5C);
	VALIDATE(SSmokeRingRelated, field_5D, 0x5D);
	VALIDATE(SSmokeRingRelated, field_5E, 0x5E);

	VALIDATE(SSmokeRingRelated, field_64, 0x64);
	VALIDATE(SSmokeRingRelated, field_65, 0x65);

	VALIDATE(SSmokeRingRelated, field_68, 0x68);
	VALIDATE(SSmokeRingRelated, field_74, 0x74);
	VALIDATE(SSmokeRingRelated, field_80, 0x80);
}

void validate_SCalcBuffer(void)
{
	VALIDATE_SIZE(SCalcBuffer, 0x3C);

	VALIDATE(SCalcBuffer, field_0, 0x0);
	VALIDATE(SCalcBuffer, field_4, 0x4);

	VALIDATE(SCalcBuffer, field_18, 0x18);
	VALIDATE(SCalcBuffer, field_20, 0x20);

	VALIDATE(SCalcBuffer, field_38, 0x38);
}

// @Bogus
// CTexturedRibbon is missing on purpose. Its Display lives at 0x4F2DB0 in the
// exe (7018 bytes) and our class never got it, so it still falls back to the
// empty CSpecialDisplay::Display. Hooking the constructor would stamp our
// vtable and the ribbon would stop drawing, so the exe keeps building those
// objects. The other members are safe, they never touch the vtable.
void patch_weapons(void)
{
	PATCH_PUSH_RET_POLY(0x004F16C0, CGouraudRibbon::CGouraudRibbon, "??0CGouraudRibbon@@QAE@HH@Z");
	PATCH_PUSH_RET_POLY(0x004F1790, CGouraudRibbon::~CGouraudRibbon, "??1CGouraudRibbon@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004F17F0, CGouraudRibbon::SetRGB, "?SetRGB@CGouraudRibbon@@QAEXEEE@Z");
	PATCH_PUSH_RET_POLY(0x004F1830, CGouraudRibbon::SetWidth, "?SetWidth@CGouraudRibbon@@QAEXG@Z");
	PATCH_PUSH_RET_POLY(0x004F1860, CGouraudRibbon::Display, "?Display@CGouraudRibbon@@UAEXXZ");

	PATCH_PUSH_RET(0x004F2AB0, CalcScreenNormal);

	PATCH_PUSH_RET_POLY(0x004F2CC0, CTexturedRibbon::~CTexturedRibbon, "??1CTexturedRibbon@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004F2D20, CTexturedRibbon::SetTexture, "?SetTexture@CTexturedRibbon@@QAEXPAUTexture@@@Z");
	PATCH_PUSH_RET_POLY(0x004F2D50, CTexturedRibbon::SetCoreRGBi, "?SetCoreRGBi@CTexturedRibbon@@QAEXHEEE@Z");
	PATCH_PUSH_RET_POLY(0x004F2D80, CTexturedRibbon::SetOuterRGBi, "?SetOuterRGBi@CTexturedRibbon@@QAEXHEEE@Z");

	PATCH_PUSH_RET_POLY(0x004F4930, CSmokeRing::CSmokeRing, "??0CSmokeRing@@QAE@HI@Z");
	PATCH_PUSH_RET_POLY(0x004F4AC0, CSmokeRing::~CSmokeRing, "??1CSmokeRing@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004F4B20, CSmokeRing::SetParams, "?SetParams@CSmokeRing@@QAEXPBVCVector@@HH@Z");
	PATCH_PUSH_RET_POLY(0x004F4C80, CSmokeRing::SetUV, "?SetUV@CSmokeRing@@QAEXHHH@Z");
	PATCH_PUSH_RET_POLY(0x004F4D40, CSmokeRing::SetRGB, "?SetRGB@CSmokeRing@@QAEXHHH@Z");
	PATCH_PUSH_RET_POLY(0x004F4DF0, CSmokeRing::Display, "?Display@CSmokeRing@@UAEXXZ");
}
