#include "export.h"
#include "bit.h"
#include "mem.h"
#include <cstring>
#include <cstdlib>
#include <cmath>
#include "validate.h"
#include "spool.h"
#include "utils.h"
#include "ps2lowsfx.h"
#include "my_assert.h"
#include "db.h"
#include "panel.h"
#include "ps2funcs.h"
#include "PCGfx.h"
#include "PCTex.h"
#include "m3dinit.h"
#include "SpideyDX.h"
#include "algebra.h"
#include "shatter.h"
#include "screen.h"
#include "ps2m3d.h"

// The six spark globals. Only CSpark::CSpark reads them and only the five
// Bit_SetSpark* setters write them, all in this file, but the exe keeps its
// own copies at these fixed addresses and CSpark is created from eight other
// files, so the macros point at game memory. Addresses proved from the
// disasm: Bit_SetSparkSize (0x40F410) stores 0x547EB8, Bit_SetSparkRGB
// (0x40F440) stores 0x547EC0/EC1/EC2, Bit_SetSparkFadeRGB (0x40F460) stores
// 0x547EC4/EC5/EC6, Bit_SetSparkTrajectory (0x40F3D0) stores 0x56E9B8,
// Bit_SetSparkTrajectoryCone (0x40F3F0) stores 0x56EB38, and CSpark::CSpark
// (0x40F480) reads all of them plus 0x547EBC. idb_globals.txt confirms
// SparkSize, SparkSemiTrans, gSparkRGB, gSparkTrajectory and
// gSparkTrajectoryCone by name.

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT bool SparkSemiTrans = true;
#else
extern bool SparkSemiTrans;
#endif
//#define G_SPARK_SEMI_TRANS (SparkSemiTrans)
#define G_SPARK_SEMI_TRANS (*reinterpret_cast<bool*>(0x00547EBC))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT CSVector SparkTrajectoryCone;
#else
extern CSVector SparkTrajectoryCone;
#endif
//#define G_SPARK_TRAJECTORY_CONE (SparkTrajectoryCone)
#define G_SPARK_TRAJECTORY_CONE (*reinterpret_cast<CSVector*>(0x0056EB38))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT CSVector SparkTrajectory;
#else
extern CSVector SparkTrajectory;
#endif
//#define G_SPARK_TRAJECTORY (SparkTrajectory)
#define G_SPARK_TRAJECTORY (*reinterpret_cast<CSVector*>(0x0056E9B8))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT u8 gSparkRGB[3] = { 0x80, 0x80, 0x80 };
#else
extern u8 gSparkRGB[3];
#endif
//#define G_SPARK_RGB (gSparkRGB)
#define G_SPARK_RGB (reinterpret_cast<u8*>(0x00547EC0))

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT u8 gSparkFadeRGB[3] = { 4, 4, 4 };
#else
extern u8 gSparkFadeRGB[3];
#endif
//#define G_SPARK_FADE_RGB (gSparkFadeRGB)
#define G_SPARK_FADE_RGB (reinterpret_cast<u8*>(0x00547EC4))

// @Ok
EXPORT char *gAnimNames[29] =
{
	"SHADOW  ",
	"SMOKE   ",
	"ribbon  ",
	"Buttons ",
	"WebKnot ",
	"Reticle ",
	"WebSplat",
	"Sp      ",
	"HITSPRIT",
	"ImpactWb",
	"HitPing ",
	"Compass ",
	"WebCart ",
	"LoadIcon",
	"Misc",

	"menubox ",
	"fireimp",
	"costarm",
	"cost99",
	"costbag",
	"costblk",
	"costcapt",

	"costscar",
	"xtri",

	"symdrop",
	"trail",
	"slime",
	"splats",
	"fire_rock03"
};



// @Ok
// Static storage duration means C++ already zero-initializes this before
// any dynamic init runs, and Bit_Init also memsets it explicitly, so the
// old "must be zero initialized" worry does not apply. Writing the
// initializer here makes that guarantee visible in the source too.
#ifndef SPIDEY_STANDALONE
SAnimFrame* gAnimTable[0x1D] = {0};
#else
extern SAnimFrame* gAnimTable[0x1D];
#endif

#ifndef SPIDEY_STANDALONE
EXPORT CChunkBit* ChunkBitList;
#else
extern CChunkBit* ChunkBitList;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT CGlow* GlowList;
#else
extern CGlow* GlowList;
#endif
#ifndef SPIDEY_STANDALONE
CTextBox* TextBoxList = 0;
#else
extern CTextBox* TextBoxList;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT volatile i32 BitCount = 0;
#else
extern volatile i32 BitCount;
#endif

//#define G_BITCOUNT (BitCount)
#define G_BITCOUNT (*reinterpret_cast<volatile i32*>(0x0056EB48))

#ifndef SPIDEY_STANDALONE
i32 TotalBitUsage = 0;
#else
extern i32 TotalBitUsage;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT CFlatBit *FlatBitList;
#else
extern CFlatBit * FlatBitList;
#endif

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT CSpecialDisplay *SpecialDisplayList = 0;
#else
extern CSpecialDisplay * SpecialDisplayList;
#endif
//#define G_SPECIALDISPLAY_LIST (SpecialDisplayList)
#define G_SPECIALDISPLAY_LIST (*reinterpret_cast<CSpecialDisplay**>(0x0056EB34))


#ifndef SPIDEY_STANDALONE
EXPORT CPixel* PixelList;
#else
extern CPixel* PixelList;
#endif

#ifndef SPIDEY_STANDALONE
u32 SparkSize = 1;
#else
extern u32 SparkSize;
#endif
//#define G_SPARK_SIZE (SparkSize)
#define G_SPARK_SIZE (*reinterpret_cast<u32*>(0x00547EB8))

// @FIXME - is it really?
// volatile i32 gTimerRelated;   // see G_TIMER_RELATED in bit.h

// @Ok
#ifndef SPIDEY_STANDALONE
EXPORT CNonRenderedBit* NonRenderedBitList = 0;
#else
extern CNonRenderedBit* NonRenderedBitList;
#endif
//#define G_NONRENDEREDBIT_LIST (NonRenderedBitList)
#define G_NONRENDEREDBIT_LIST (*reinterpret_cast<CNonRenderedBit**>(0x0056EAD8))


#ifndef SPIDEY_STANDALONE
EXPORT CBit* Linked2EndedBitListLeftover;
#else
extern CBit* Linked2EndedBitListLeftover;
#endif
#ifndef SPIDEY_STANDALONE
CBit* PolyLineList;
#else
extern CBit* PolyLineList;
#endif
#ifndef SPIDEY_STANDALONE
CBit* GPolyLineList;
#else
extern CBit* GPolyLineList;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT CBit* QuadBitList;
#else
extern CBit* QuadBitList;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT CBit* GenPolyList;
#else
extern CBit* GenPolyList;
#endif
#ifndef SPIDEY_STANDALONE
EXPORT CBit* GlassList;
#else
extern CBit* GlassList;
#endif
#ifndef SPIDEY_STANDALONE
CBit* GLineList;
#else
extern CBit* GLineList;
#endif

#ifndef SPIDEY_STANDALONE
EXPORT CBitServer* gBitServer = 0;
#else
extern CBitServer* gBitServer;
#endif

// @Ok
// @Validate: when inlined
INLINE void Bit_CalculateSparkVelocity(CVector &a1, i32 a2)
{
	CSVector v11 = G_SPARK_TRAJECTORY;

	if (G_SPARK_TRAJECTORY_CONE.vx)
	{
		v11.vx += Rnd(G_SPARK_TRAJECTORY_CONE.vx) - (G_SPARK_TRAJECTORY.vx >> 1);
	}

	if (G_SPARK_TRAJECTORY_CONE.vy)
	{
		v11.vy += Rnd(G_SPARK_TRAJECTORY_CONE.vy) - (G_SPARK_TRAJECTORY.vy >> 1);
	}

	if (G_SPARK_TRAJECTORY_CONE.vz)
	{
		v11.vz += Rnd(G_SPARK_TRAJECTORY_CONE.vz) - (G_SPARK_TRAJECTORY.vz >> 1);
	}

	Utils_GetVecFromMagDir(&a1, a2, &v11);
}

// @Ok
// @NotMatching: different register alloc totally diff registers
// the call to calculate spark velocity is lsight differnt but all good
CSpark::CSpark(
		CVector& a2,
		i32 a3,
		i32 a4,
		i32 a5)
{
	if ((G_SPARK_SIZE & 0xF) != 1)
	{
		this->code = 96;
		this->tag = 0x3000000;
		this->mWidthHeight = G_SPARK_SIZE;
	}
	else
	{
		this->code = 104;
		this->tag = 0x2000000;
		this->mWidthHeight = 1;
	}

	this->r0 = G_SPARK_RGB[0];
	this->g0 = G_SPARK_RGB[1];
	this->b0 = G_SPARK_RGB[2];

	this->mFadeR = G_SPARK_FADE_RGB[0];
	this->mFadeG = G_SPARK_FADE_RGB[1];
	this->mFadeB = G_SPARK_FADE_RGB[2];

	this->mPos = a2;

	Bit_CalculateSparkVelocity(this->mVel, a3);

	this->mAcc.vy = a4;

	this->mFric.Set(3, 3, 3);

	this->mLifetime = Rnd(a5);

	if (G_SPARK_SEMI_TRANS)
	{
		this->code |= 2u;
	}
}

// @Ok
// @Matching
void CSpark::Move(void)
{
	this->mPos.vx += this->mVel.vx;
	this->mPos.vy += this->mVel.vy;
	this->mPos.vz += this->mVel.vz;

	i32 newVelX = this->mVel.vx + this->mAcc.vx;
	i32 newVelY = this->mVel.vy + this->mAcc.vy;
	i32 newVelZ = this->mVel.vz + this->mAcc.vz;

	this->mVel.vx = newVelX - (newVelX >> 3);
	this->mVel.vy = newVelY - (newVelY >> 3);
	this->mVel.vz = newVelZ - (newVelZ >> 3);

	if (++this->mAge >= this->mLifetime)
	{
		this->Die();
	}

	this->r0 -= this->mFadeR;
	this->g0 -= this->mFadeG;
	this->b0 -= this->mFadeB;
}

// @Ok
// @Matching
CSpark::~CSpark(void)
{
}

// @Ok
INLINE CSpecialDisplay::~CSpecialDisplay(void)
{
	this->DeleteFrom(&G_SPECIALDISPLAY_LIST);
}

// @Ok
// base vtable slot for Display, needed by CSpecialDisplay subclasses that do not override it (e.g. CTexturedRibbon)
void CSpecialDisplay::Display(void)
{
}

// @Ok
// @AlmostMatching: slightly out of order due ot the AttachTo
CSimpleTexturedRibbon::CSimpleTexturedRibbon(i32 numfaces)
{
	this->SetNumFaces(numfaces);
}


// Shared infrastructure notes for the whole Display*List / ribbon-Display
// family below (CSimpleTexturedRibbon::Display, DisplayGLineList,
// DisplayGlassList, DisplayGlowList, DisplayChunkBitList, DisplayQuadBitList,
// DisplayFlatBitList, DisplayLinked2EndedBitListLeftover, DisplayPixelList,
// DisplayPolyLineList, DisplayGPolyLineList), found this session (2026-08-31)
// while trying to implement them. DisplayQuadBitList and DisplayPixelList
// are now done (@Ok, see below); the rest are not done yet, but a lot of
// the pipeline they all share is already built and @Ok elsewhere in the
// repo, which should make a real attempt much faster than starting cold:
//
// - Per-point camera-space projection idiom (verified against the already
//   @Ok Screen_DrawArrow in screen.cpp, which uses this exact shape):
//     VECTOR relPos;
//     relPos.vx = (rawPos.vx >> 12) - gMikeCamera[0].Position.vx; // same for vy, vz
//     gte_ldlv0(&relPos);
//     gte_rtps();
//     i32 sxy; gte_stsxy(&sxy);
//     i32 screenX = (i16)sxy, screenY = (i16)(sxy >> 16);
//   gte_ldlv0/gte_rtps/gte_stsxy/gte_stlvnl/gte_stlvnl2/gte_ldv0 are all
//   already implemented and @Ok in ps2funcs.cpp. IMPORTANT: IDA's decompiler
//   mislabels the call to gte_ldlv0 (0x46D840) as
//   "qt_register_signal_spy_callbacks" (a coincidental FLIRT signature hit
//   on an unrelated Qt library function). Always cross-check call targets
//   against tools/names.json, not the symbol name IDA prints.
// - Clip test after the transform: G_VIEW_CLIP_INFO (screen.cpp, u8* at
//   0x64E514, guessed layout: u16 min/max at +8/+0xA) already exists;
//   Screen_DrawArrow clips relPos.vy against it, DisplayGLineList/
//   DisplayFlatBitList/DisplayLinked2EndedBitListLeftover clip the
//   gte_stlvnl2() depth value instead (same struct, different field of the
//   transform result). ps2m3d.cpp also has a second, inconsistent alias for
//   the same address (gM3dViewportPtr, typed i32*) - not reconciled here.
// - A second, separate pipeline is used for a per-vertex inverse-depth
//   ("invZ") value fed into the draw calls: it builds a raw fixed-to-float
//   vector (rawPos.axis / 4096.0f, no >>12 first) and runs it through
//   DCX_XformVector (already @Ok @AlmostMatching as Algebra_Transform4 in
//   algebra.cpp/algebra.h, using the camera matrix gGfxMatrix at 0x56E668),
//   then takes 1/result.w (with a fabs(w) > 1e-8 guard, else -1e12) and
//   wraps it through a vector3d/vector4d ctor (sub_402540/402560/402620,
//   still unnamed) before use. Every function in this family that uses this
//   path opens with the SAME 16-float copy loop, copying dword_56E6F8 (x
//   row?), dword_56E6FC, dword_56E700 and part of dword_56E668 itself into
//   the contiguous gGfxMatrix layout; this copy loop is not implemented
//   anywhere in the repo yet and needs its own name once someone writes it
//   (it looks like "refresh the float camera matrix from its 4 source
//   arrays before using Algebra_Transform4", once per Display call, not
//   once per point).
// - Output paths differ per function, identified via callee lists:
//   - Immediate PCGfx_DrawQPoly3D (0x508550, already @Ok in PCGfx.cpp,
//     signature is really (x,y,z, u,v, color) per vertex despite the
//     w0/uv0 parameter names in PCGfx.h - confirmed against
//     DisplayQuadBitList, which always passes the constants 0.01/0.99 in
//     those two slots per corner, the classic texture-bleed-inset quad
//     UVs): used directly by DisplayGlassList, DisplayGlowList,
//     DisplayQuadBitList, DisplayFlatBitList,
//     DisplayLinked2EndedBitListLeftover, CSimpleTexturedRibbon::Display
//     (confirmed via each function's callee list).
//   - A second quad/line emitter, sub_509000, still unnamed, is used by
//     DisplayGLineList, DisplayPolyLineList and DisplayGPolyLineList
//     instead of sub_508550 - all three are the "line" family, so this is
//     likely a distinct 2-or-4-vertex line-shaped primitive rather than
//     PCGfx_DrawQPoly3D's textured quad. Its signature is not decoded yet.
//   - DisplayChunkBitList instead calls sub_5081F0 (still unnamed) to draw
//     each of its 4 faces, built from WPlane objects (sub_40C190 =
//     WPlane::WPlane(WVector&, f32), already named in names.json).
//   - DisplayPixelList calls sub_507470 = PCGfx_DrawQuad2D (already @Ok,
//     PCGfx.h) instead, i.e. it is a 2D sprite/dot draw, not a 3D quad.
//     DONE this session (2026-08-31): its screen x/y args do NOT come from
//     the GTE pipeline at all (this function never calls gte_ldlv0/rtps),
//     they are the x*invZ/y*invZ stack slots written by the same
//     sub_402540 vector3d-ctor call that produces the depth value - traced
//     in the raw disassembly (Hex-Rays lost the alias and printed them as
//     uninitialized locals). See DisplayPixelList's implementation below.
//   - DisplayGLineList and DisplayGlassList (0x411560, also traced this
//     session) BOTH append a 28-byte record (tag 0x4000000) to the pPoly
//     queue (0x56FB04, bounds-checked against PolyBufferEnd at 0x5FCD1C)
//     before their real draw calls: tag(u32), r/g/b/0x22 (4 bytes, from
//     the bit's current color), 3x packed-sxy (12 bytes), then the two
//     globals dword_56E9D0/56E9D4 (also referenced the same way by
//     DisplayGlowList, DisplayPolyLineList, DisplayGPolyLineList and
//     DisplayGLineList - xref-confirmed, meaning still unknown). The
//     3x-sxy fill is sub_46DFA0, which IS already decompiled and @Ok in
//     this repo: it is gte_stsxy3 (ps2funcs.cpp), and the original PC
//     port's own gte_stsxy3 body calls stubbed_printf("stubbed out:
//     gte_stsxy3") - i.e. the original binary itself treats this whole
//     28-byte queue as an unfinished/inert debug path, not the real
//     render output (DisplayGlassList's actual PCGfx_DrawQPoly3D calls
//     read positions from a completely separate Algebra_Transform4/invZ
//     pipeline, not from this record). Safe to skip reproducing this
//     queue write for a functional decomp; note it if picking this back
//     up so the skip is a documented choice, not an oversight.
//   - DisplayGlassList's actual draw geometry is NOT just its 3 stored
//     corners (mPosA/B/C): the disassembly derives a 4th and further
//     offset corners via scaled vector differences (a "grow" scale field
//     read from the bit, exact field not pinned down yet) before running
//     each through its own Algebra_Transform4/invZ pass - closer to a
//     tessellated glass-shard fan than a single static quad. Needs more
//     work to identify the scale field before this can be implemented
//     correctly; do not guess the geometry.
// - DisplayPolyLineList/DisplayGPolyLineList's clip helper, sub_505B90, is
//   labelled "syRtcInit" in tools/names.json; that is almost certainly a
//   link-time duplicate-body merge (CLAUDE.md: identical bodies across TUs
//   share one address) with the real clip-test helper used here, not an
//   actual real-time-clock init call - the context (called right after
//   gte_stsxy, result used as a clip bound) does not fit "RTC init" at all.
// - CSimpleTexturedRibbon::Display additionally calls gte_SetRotMatrix
//   (0x46D7B0) and m3d_ZeroTransVector (0x46E460) before projecting, i.e.
//   it sets up its own rotation matrix and zeroes translation first
//   (billboard/camera-facing orientation, not the shared per-frame camera
//   matrix the other functions read as-is); it also calls the already-@Ok
//   Utils_CalcUnitFacingCamera (utils.cpp) which strongly suggests this is
//   a camera-facing ("billboarded") ribbon, consistent with its name.
//
// DisplayQuadBitList and DisplayPixelList got finished this session (see
// RefreshGfxMatrix and their implementations below) using exactly this
// map. The rest were not enough to responsibly finish within this
// session's budget (dense float/fixed-point pipelines, several still
// -unnamed helpers - sub_509000, sub_5081F0 - or, for DisplayGlassList,
// derived geometry beyond the bit's stored corners that is not pinned
// down yet, and no runtime test available to catch a wrong sign or
// offset), so they stay @BIGTODO. The next attempt should be able to move
// much faster starting from this map instead of raw disassembly.

// Shared camera-matrix refresh for the Display*List family (see the family
// notes above). Every function that reads the invZ pass through
// Algebra_Transform4 opens with the same instruction sequence: a 16-float
// copy that IDA renders as 4 interleaved "arrays"
// (dword_56E6F8/56E6FC/56E700/flt_56E674) because of array-boundary folding
// (CLAUDE.md: "MSVC folds array indexing into neighboring globals' base
// addresses"). Traced instruction-by-instruction against the raw disasm of
// 0x4097e0 (0x4097f6..0x409895): dword_56E6FC is just dword_56E6F8+4 bytes
// and dword_56E700 is dword_56E6F8+8 bytes (one contiguous 16-float array,
// not three), and flt_56E674 is literally &gGfxMatrix[3] (an alias into the
// SAME destination array algebra.cpp already declares as gGfxMatrix at
// 0x56E668). Once the folding is undone the whole loop is exactly
// `memcpy(gGfxMatrix, gCameraBasisMatrix, 16 * sizeof(f32))`. gGfxMatrix is
// `static` in algebra.cpp (file-local pointer, shared address per repo
// convention), so this file gets its own pointer to the same 0x56E668
// address rather than reaching into algebra.cpp.
static f32 * const gFrameProjMatrix = (f32*)0x56E668;   // == algebra.cpp's gGfxMatrix
// Tentative name; source basis matrix the family copies into gFrameProjMatrix
// once per Display call, address confirmed via the byte-offset trace above.
// Not in idb_globals.txt. Likely the camera's un-composed view/rotation
// matrix (refreshed once per frame elsewhere), but that is not confirmed.
static f32 * const gCameraBasisMatrix = (f32*)0x56E6F8;

// @Ok
// Functional. Factored out of DisplayQuadBitList's opening instructions
// (see the comment above gFrameProjMatrix); every Display*List function
// that projects through Algebra_Transform4 repeats this exact copy inline
// in the original, so this helper is reused across the family as more of
// it gets implemented.
static INLINE void RefreshGfxMatrix(void)
{
	for (i32 i = 0; i < 16; i++)
	{
		gFrameProjMatrix[i] = gCameraBasisMatrix[i];
	}
}

// Camera position used by the per-corner relPos = (rawPos>>12) - camPos
// idiom (see the family notes and Screen_DrawArrow). Same address as
// chopper.cpp's gCameraViewPos / mysterio.cpp's and spidey.cpp's
// stru_56F1B4; duplicated here per the repo's file-local-static convention.
static CVector * const gCameraViewPos = (CVector*)0x56F1B4;

// @Ok
// @Matching
void CSimpleTexturedRibbon::SetNumFaces(i32 a1)
{
	DoAssert(a1 != 0, "Zero NumFaces");
	this->field_3C = a1;
	this->field_3E = a1;

	i32 texSize = this->field_3C * sizeof(SRibbonTexture);
	this->pTextures = static_cast<SRibbonTexture *>(DCMem_New(
		texSize,
		0,
		1,
		0,
		1));

	u32 v4 = 0;
	u32 v5 = 0;

	SRibbonTexture *pTexture = this->pTextures;

	for (i32 i = 0; i < this->field_3C; i++)
	{
		pTexture->field_0 = 0xA01;
		pTexture->field_2 = 32;


		pTexture->field_4 = v4;
		pTexture->field_5 = v4 + 2;
		pTexture->field_6 = v4 + 1;

		pTexture->field_7 = v4 + 3;
		v4 += 2;

		pTexture->field_8 = v5;
		pTexture->field_9 = v5 + 1;
		pTexture->field_A = v5;
		pTexture->field_B = v5 + 1;

		pTexture->field_C = 0;
		pTexture->field_E = 0;

		v5++;

		pTexture++;
	}

	i32 ffSize = sizeof(SSimpleRibbonParams) *  (a1+1);
	this->field_44 = static_cast<SSimpleRibbonParams*>(
			DCMem_New(ffSize, 0, 1, 0, 1));

	i32 feSize = sizeof(u32) * (a1+1);
	this->pColours = static_cast<u32*>(
			DCMem_New(feSize, 0, 1, 0, 1));
}

// @Ok
// @Matching
void CSimpleTexturedRibbon::SetOpaque(void)
{
	SRibbonTexture *pTexture = this->pTextures;
	for (i32 i = 0; i < this->field_3C; i++)
	{
		pTexture->field_0 |= 0x80;
		pTexture++;
	}
}


// @Ok
// @Matching
void CSimpleTexturedRibbon::SetTexture(Texture *a2)
{
	DoAssert(a2 != 0, "NULL pTex sent to SetTexture");

	SRibbonTexture *pTexture = this->pTextures;

	for (i32 i = 0; i < this->field_3C; i++)
	{
		pTexture->mPage = a2->tpage;
		pTexture->mClut = a2->clut;
		pTexture->u0 = a2->u0;
		pTexture->u1 = a2->u1;
		pTexture->v0 = a2->v0;
		pTexture->v1 = a2->v1;
		pTexture->u2 = a2->u2;
		pTexture->u3 = a2->u3;
		pTexture->v2 = a2->v2;
		pTexture->v3 = a2->v3;
		pTexture->mTexWin = a2->TexWin;
		++pTexture;
	}
}

// @Ok
// @Matching
void CSimpleTexturedRibbon::SetTexture(u32 checksum)
{
	Texture *TextureEntry = Spool_FindTextureEntry(checksum);
	DoAssert(TextureEntry != 0, "Could not find texture for ribbon");
	if (!TextureEntry)
	{
		TextureEntry = G_ANIM_TABLE[13]->pTexture;
	}

	this->SetTexture(TextureEntry);
}

// @Ok
// @Matching
void CSimpleTexturedRibbon::SetTexturei(i32 a2, Texture *a3)
{
	DoAssert(a2 < this->field_3C, "Bad i in call to CSimpleTexturedRibbon::SetTexturei");
	DoAssert(a3 != 0, "NULL pTex sent to SetTexturei");

	this->pTextures[a2].mPage = a3->tpage;
	this->pTextures[a2].mClut = a3->clut;

	this->pTextures[a2].u0 = a3->u0;

	this->pTextures[a2].u1 = a3->u1;
	this->pTextures[a2].v0 = a3->v0;

	this->pTextures[a2].v1 = a3->v1;

	this->pTextures[a2].u2 = a3->u2;
	this->pTextures[a2].u3 = a3->u3;

	this->pTextures[a2].v2 = a3->v2;

	this->pTextures[a2].v3 = a3->v3;

	this->pTextures[a2].mTexWin = a3->TexWin;
}

// @Ok
// @Matching
void CSimpleTexturedRibbon::SetTexturei(i32 a1, u32 a2)
{
	DoAssert(a1 < this->field_3C, "Bad i in call to CSimpleTexturedRibbon::SetTexturei");
	Texture *TextureEntry = Spool_FindTextureEntry(a2);
	DoAssert(TextureEntry != 0, "Could not find texture for ribbon");

	this->SetTexturei(a1, TextureEntry);
}

// @Ok
// @Matching
void CSimpleTexturedRibbon::SetWidth(u16 a2)
{
	SSimpleRibbonParams *pParam = this->field_44;
	for (i32 i = 0; i < this->field_3C + 1; i++)
	{
		pParam->mWidth = a2;
		++pParam;
	}
}

// @Ok
// @Matching
void CSimpleTexturedRibbon::SetWidthi(i32 a2, u16 a3)
{
	DoAssert(a2 < this->field_3C + 1, "Bad i in call to CSimpleTexturedRibbon::SetWidthi");
	this->field_44[a2].mWidth = a3;
}

// @Ok
CSimpleTexturedRibbon::~CSimpleTexturedRibbon(void)
{
	Mem_Delete(this->pTextures);
	Mem_Delete(this->field_44);
	Mem_Delete(this->pColours);
}

// @Ok
// @Matching
CSimpleTexturedRibbon::CSimpleTexturedRibbon(void)
{
}

// @Ok
// @Matching
void CSimpleTexturedRibbon::SetSemiTransparent(void)
{
	SRibbonTexture *pTexture = this->pTextures;
	for (i32 i = 0; i < this->field_3C; i++)
	{
		pTexture->field_0 |= 0xC0;
		pTexture++;
	}
}

// @Ok
// @Matching
void Bit_RemoveDeadBits(void)
{
	RemoveDeadBits(G_NONRENDEREDBIT_LIST);
	RemoveDeadBits(G_TEXTBOX_LIST);
	RemoveDeadBits(G_FLATBIT_LIST);
	RemoveDeadBits(G_LINKED2ENDEDBIT_LIST_LEFTOVER);

	RemoveDeadBits(G_PIXEL_LIST);
	RemoveDeadBits(G_POLYLINE_LIST);

	RemoveDeadBits(G_GPOLYLINE_LIST);

	RemoveDeadBits(G_QUADBIT_LIST);
	RemoveDeadBits(G_GENPOLY_LIST);
	RemoveDeadBits(G_CHUNKBIT_LIST);

	RemoveDeadBits(G_GLOW_LIST);
	RemoveDeadBits(G_GLASS_LIST);
	RemoveDeadBits(G_GLINE_LIST);
	RemoveDeadBits(G_SPECIALDISPLAY_LIST);
}

// @Ok
// @Matching
INLINE void RemoveDeadBits(CBit *pBit)
{
	if (pBit)
 	{
 		CBit *pNext = pBit->mNext;
 		while (1)
 		{
 			if (pBit->mDead)
 				delete pBit;
 
 			pBit = pNext;
 
 			if (!pBit)
 				break;
 
 			pNext = pNext->mNext;
 		}
 	}
}

// address 0x411B30. Functional decompile session (2026-08-31, CheckForPadUnplugged chain).
// Same 14 lists, same order, as Bit_RemoveDeadBits (already @Ok @Matching), which strongly
// suggests this is the real original order too, since these sibling functions walk the same
// bit registry. Unlike RemoveDeadBits (which must cache pNext before a delete), the original
// re-reads pBit->mNext after the Move() call, so a plain for loop matches its shape.
// @Ok
INLINE void MoveBits(CBit *pBit)
{
	for ( ; pBit != 0; pBit = pBit->mNext)
	{
		if (!pBit->mDead)
			pBit->Move();
	}
}

// address 0x411B30, name from names.json. Runs every live (non-dead) bit's Move() once per
// frame, across every registered bit list.
// @Ok
void Bit_Move(void)
{
	MoveBits(G_NONRENDEREDBIT_LIST);
	MoveBits(G_TEXTBOX_LIST);
	MoveBits(G_FLATBIT_LIST);
	MoveBits(G_LINKED2ENDEDBIT_LIST_LEFTOVER);

	MoveBits(G_PIXEL_LIST);
	MoveBits(G_POLYLINE_LIST);

	MoveBits(G_GPOLYLINE_LIST);

	MoveBits(G_QUADBIT_LIST);
	MoveBits(G_GENPOLY_LIST);
	MoveBits(G_CHUNKBIT_LIST);

	MoveBits(G_GLOW_LIST);
	MoveBits(G_GLASS_LIST);
	MoveBits(G_GLINE_LIST);
	MoveBits(G_SPECIALDISPLAY_LIST);
}

// 0x0056F224. Passed by address to gte_SetRotMatrix right before Bit_Display's
// render pass, same idiom as screen.cpp's gte_SetRotMatrix(gTargetRotMatrix)
// + m3d_ZeroTransVector() pair, so this is a MATRIX the display pass resets the
// GTE to before projecting anything (shared by every DisplayXList callback).
// It is the same object six other files already reach at a fixed address
// (utils.cpp gCameraViewMatrix, chopper.cpp gCameraViewMatrix, spidey.cpp and
// panel.cpp stru_56F224, screen.cpp gTargetRotMatrix): the camera view matrix.
// Nothing in this repo writes it, the camera code in the exe does, so it has to
// be read out of game memory or a hooked Bit_Display would load an all zero
// rotation into the GTE. Was a plain repo MATRIX until this pass.
static MATRIX * const gBitDisplayMatrix = (MATRIX*)0x0056F224;

// address 0x411CF0, name from names.json. Runs every registered CBitServer display slot once
// per frame (CBitServer::DisplayRegisteredSlots, already @Ok, is the exact same 32-slot loop
// this inlines in the original), after resetting the GTE rotation/translation and asserting
// the poly buffer write cursor has not run past its end.
// @Ok
void Bit_Display(void)
{
	print_if_false(reinterpret_cast<u8*>(G_PPOLY) <= G_POLY_BUFFER_END, "Poly buffer overflowed before Bit_Display");

	gte_SetRotMatrix(gBitDisplayMatrix);
	m3d_ZeroTransVector();

	if (G_BITSERVER)
		G_BITSERVER->DisplayRegisteredSlots();
}

// @Ok
// @AlmostMatching: CFriction::Set was not inlined and attachto seems different too
CQuadBit::CQuadBit(void)
{
	this->AttachTo(&G_QUADBIT_LIST);
	this->mCodeBGR = 0x1C0001;
	this->field_68 = 0x2030001;
	this->mTint = 0x808080;
	this->field_70 = 0;
}

// @Ok
// Functional: field_0[0..2] = a1,a2,a3 is unambiguously correct for a
// 3-float constructor regardless of calling convention. Note for whoever
// revisits byte-matching: names.json labels 0x402540 as this constructor
// (??0vector3d@@QAE@MMM@Z, 3 floats), and every call site in this file
// passes it a single pointer to 3 packed floats, and cmpsum shows the
// original does "ret 4" (pops one stack arg) where our 3-stack-float
// __thiscall does "ret 0Ch" (pops three) -- so 0x402540 most likely is
// NOT this f32,f32,f32 overload but a different vector3d(f32*)-shaped
// helper that the names.json entry mislabels. Not chased further since
// this session's bar is functional correctness, not byte match.
vector3d::vector3d(f32 a1, f32 a2, f32 a3)
{
	this->field_0[0] = a1;
	this->field_0[1] = a2;
	this->field_0[2] = a3;
}

// @Ok
INLINE vector4d::vector4d(const vector3d& a1, f32 a2)
{
	this->field_0[0] = a1.field_0[0];
	this->field_0[1] = a1.field_0[1];
	this->field_0[2] = a1.field_0[2];

	this->field_0[3] = a2;
}

// @Ok
// @Matching
INLINE void DeleteBitList(CBit *pBitList)
{
	if (!pBitList)
	{
		return;
	}

	CBit* pCurr = pBitList;
	CBit* pNext = pCurr->mNext;
	while (pCurr)
	{
		if (!pCurr->mProtected)
		{
			delete pCurr;
		}

		pCurr = pNext;

		if (!pNext)
		{
			break;
		}

		pNext = pNext->mNext;
	}
}

// @Ok
// @Matching
// Disassembled with IDA (0x40D630, 32 bytes). Not a call to DeleteBitList:
// it walks TextBoxList and deletes every entry unconditionally, ignoring
// mProtected, with the next pointer read before the delete. The asm has a
// genuinely redundant "if (p)" check inside the loop body (test ecx,ecx
// right after the mNext load), which this source shape reproduces exactly.
void Bit_ClearTextBoxes(void)
{
	CBit *p = G_TEXTBOX_LIST;
	while (p)
	{
		CBit *pNext = p->mNext;
		if (p)
		{
			delete p;
		}
		p = pNext;
	}
}

// @Ok
// @AlmostMatching: my inlining stops at ChunkBitList while OG stops at GlowList
void Bit_DeleteAll(void)
{
	DeleteBitList(G_NONRENDEREDBIT_LIST);
	DeleteBitList(G_TEXTBOX_LIST);
	DeleteBitList(G_FLATBIT_LIST);
	DeleteBitList(G_LINKED2ENDEDBIT_LIST_LEFTOVER);
	DeleteBitList(G_PIXEL_LIST);
	DeleteBitList(G_POLYLINE_LIST);
	DeleteBitList(G_GPOLYLINE_LIST);
	DeleteBitList(G_QUADBIT_LIST);
	DeleteBitList(G_GENPOLY_LIST);
	DeleteBitList(G_CHUNKBIT_LIST);
	DeleteBitList(G_GLOW_LIST);
	DeleteBitList(G_GLASS_LIST);
	DeleteBitList(G_GLINE_LIST);
	DeleteBitList(G_SPECIALDISPLAY_LIST);

	DoAssert(G_NONRENDEREDBIT_LIST == 0, "NonRenderedBitList  Leftover protected bits!");
	DoAssert(G_TEXTBOX_LIST == 0, "TextBoxList  Leftover protected bits!");
	DoAssert(G_FLATBIT_LIST == 0, "FlatBitList  Leftover protected bits!");
	DoAssert(G_LINKED2ENDEDBIT_LIST_LEFTOVER == 0, "Linked2EndedBitListLeftover protected bits!");
	DoAssert(G_PIXEL_LIST == 0, "PixelList  Leftover protected bits!");
	DoAssert(G_POLYLINE_LIST == 0, "PolyLineList  Leftover protected bits!");
	DoAssert(G_GPOLYLINE_LIST == 0, "GPolyLineList  Leftover protected bits!");
	DoAssert(G_QUADBIT_LIST == 0, "QuadBitList  Leftover protected bits!");
	DoAssert(G_GENPOLY_LIST == 0, "GenPolyList  Leftover protected bits!");
	DoAssert(G_CHUNKBIT_LIST == 0, "ChunkBitList  Leftover protected bits!");
	DoAssert(G_GLOW_LIST == 0, "GlowList  Leftover protected bits!");
	DoAssert(G_GLASS_LIST == 0, "GlassList  Leftover protected bits!");
	DoAssert(G_GLINE_LIST == 0, "GLineList  Leftover protected bits!");
	DoAssert(G_SPECIALDISPLAY_LIST == 0, "SpecialDisplayList  Leftover protected bits!");

	DoAssert(G_BITCOUNT == 0, "Still some bits left");
}

// Tentative struct for GLineList's bit type (not in bit.h; no confirmed
// name in names.json/idb_globals.txt). Traced from the raw disassembly of
// DisplayGLineList (0x412f10) this session: two ARGB-ish colours (u32
// each) followed by two endpoint CVectors, laid out directly after CBit
// (whose own size is the confirmed 0x3C, VALIDATE_SIZE below).
struct SGLineBit : public CBit
{
	u32 mColor1;
	u32 mColor2;
	CVector mPos1;
	CVector mPos2;
};

// @Ok
// Functional (session-wide functional-only bar, 2026-08-31). Address
// 0x412f10 (names.json: DisplayGLineList_0). Fully traced against the raw
// disassembly, which corrects the shared family notes above for this one
// function specifically: the 28-byte pPoly-queue record it builds (tag
// 0x4000000) is NOT dead/inert here (unlike DisplayGlassList's matching
// -shaped record, still unconfirmed) - it IS read back to build the real
// PCGfx_DrawLine call, confirmed byte-offset by byte-offset. Only the two
// dword_56E9D0/56E9D4 globals copied into the record's last 8 bytes go
// unread, matching the family's "always written, never read" note for
// those two globals specifically. sub_509000 (family notes: "still
// unnamed") is also resolved: it is the already-@Ok PCGfx_DrawLine
// (PCGfx.cpp, PCGfx.h - 9-param signature matches exactly).
//
// Per bit: both endpoints (mPos1, mPos2) go through the standard
// gte_ldlv0/gte_rtps/gte_stlvnl2 camera transform and are clip tested
// against G_VIEW_CLIP_INFO's depth range (screen.cpp/screen.h); either
// endpoint failing skips the whole bit with no draw. CBit::mFrigDeltaZ
// (0x38, VALIDATEd below) is then subtracted from the nearer of the two
// depths as a second, combined near-clip fudge test.
//
// Screen position comes from gte_stsxy per endpoint. invZ per endpoint
// comes from the separate Algebra_Transform4/gGfxMatrix pass (see
// RefreshGfxMatrix above); raw disasm confirms the "vector3d ctor" call
// the family notes flagged as producing dead output for DisplayQuadBitList
// corner 0 is NOT dead here - its 3rd float (stack offset traced directly)
// IS invZ1, Hex-Rays' pseudocode just failed to alias the stack slot back
// to a named local. Both invZ values get PCGfx_DrawLine's own documented
// -7.0710726 z bias before being passed in; both must be positive
// (endpoint in front of camera) or the whole bit is skipped.
//
// Colour is each endpoint's own mColor1/mColor2 dword's low 3 bytes
// (byte0->R, byte1->G, byte2->B, same ARGB pack the rest of this family
// uses), with a SHARED alpha/blend mode for both endpoints taken only from
// mColor1's byte 3 bit 0x02 (semi-transparent) - mColor2's own top byte is
// never read for this. Texture slot 1 (the flat/line texture constant
// DisplayTextBoxList and DisplayPixelList also use), line width flat 2.0f.
void DisplayGLineList(void** a1)
{
	RefreshGfxMatrix();

	u8* clip = G_VIEW_CLIP_INFO;
	u16 clipMin = *(u16*)(clip + 8);
	u16 clipMax = *(u16*)(clip + 0xA);

	SGLineBit* pBit = reinterpret_cast<SGLineBit*>(*a1);
	while (pBit)
	{
		VECTOR relPos1;
		relPos1.vx = (pBit->mPos1.vx >> 12) - gCameraViewPos->vx;
		relPos1.vy = (pBit->mPos1.vy >> 12) - gCameraViewPos->vy;
		relPos1.vz = (pBit->mPos1.vz >> 12) - gCameraViewPos->vz;

		gte_ldlv0(&relPos1);
		gte_rtps();

		i32 depth1;
		gte_stlvnl2(&depth1);

		if (depth1 >= clipMin && depth1 <= clipMax)
		{
			i32 sxy1;
			gte_stsxy(&sxy1);

			VECTOR relPos2;
			relPos2.vx = (pBit->mPos2.vx >> 12) - gCameraViewPos->vx;
			relPos2.vy = (pBit->mPos2.vy >> 12) - gCameraViewPos->vy;
			relPos2.vz = (pBit->mPos2.vz >> 12) - gCameraViewPos->vz;

			gte_ldlv0(&relPos2);
			gte_rtps();

			i32 depth2;
			gte_stlvnl2(&depth2);

			if (depth2 >= clipMin && depth2 <= clipMax)
			{
				i32 sxy2;
				gte_stsxy(&sxy2);

				i32 nearDepth = (depth1 < depth2 ? depth1 : depth2) - pBit->mFrigDeltaZ;
				if (nearDepth >= clipMin)
				{
					i16 x1 = (i16)sxy1;
					i16 y1 = (i16)(sxy1 >> 16);
					i16 x2 = (i16)sxy2;
					i16 y2 = (i16)(sxy2 >> 16);

					f32 rawPos1[3];
					rawPos1[0] = (f32)pBit->mPos1.vx / 4096.0f;
					rawPos1[1] = (f32)pBit->mPos1.vy / 4096.0f;
					rawPos1[2] = (f32)pBit->mPos1.vz / 4096.0f;

					f32 xf1[4];
					Algebra_Transform4(xf1, rawPos1);

					f32 invZ1;
					if (fabsf(xf1[3]) > 0.00000001f)
						invZ1 = 1.0f / xf1[3];
					else
						invZ1 = -1.0e12f;

					f32 rawPos2[3];
					rawPos2[0] = (f32)pBit->mPos2.vx / 4096.0f;
					rawPos2[1] = (f32)pBit->mPos2.vy / 4096.0f;
					rawPos2[2] = (f32)pBit->mPos2.vz / 4096.0f;

					f32 xf2[4];
					Algebra_Transform4(xf2, rawPos2);

					f32 invZ2;
					if (fabsf(xf2[3]) > 0.00000001f)
						invZ2 = 1.0f / xf2[3];
					else
						invZ2 = -1.0e12f;

					if (invZ1 > 0.0f && invZ2 > 0.0f)
					{
						i32 blendMode = 0;
						u32 alpha = 0xFF;
						if ((pBit->mColor1 >> 24) & 2)
						{
							blendMode = 2;
							alpha = 0x80;
						}

						PCGfx_UseTexture(1, (DCGfx_BlendingMode)blendMode);

						u32 color1 = (alpha << 24) | ((pBit->mColor1 & 0xFF) << 16) |
								(((pBit->mColor1 >> 8) & 0xFF) << 8) | ((pBit->mColor1 >> 16) & 0xFF);
						u32 color2 = (alpha << 24) | ((pBit->mColor2 & 0xFF) << 16) |
								(((pBit->mColor2 >> 8) & 0xFF) << 8) | ((pBit->mColor2 >> 16) & 0xFF);

						f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
						f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;

						PCGfx_DrawLine(
								x1 * scaleX, y1 * scaleY, invZ1 - 7.0710726f, color1,
								x2 * scaleX, y2 * scaleY, invZ2 - 7.0710726f, color2,
								2.0f);
					}
				}
			}
		}

		pBit = reinterpret_cast<SGLineBit*>(pBit->mNext);
	}
}

// @Ok
// @Matching
void DisplaySpecialDisplayList(void** a1)
{
	CSpecialDisplay* p = reinterpret_cast<CSpecialDisplay*>(*a1);
	while (p)
	{
		p->Display();
		p = reinterpret_cast<CSpecialDisplay*>(p->mNext);
	}
}

// @Ok
// Functional (session-wide functional-only bar, 2026-08-31). Address 0x411560, real name
// DisplayGlass in tools/names.json. CORRECTION this session against a fresh IDA decompile: the
// old note's "grow scale field" / "4th and further offset corners" / "tessellated fan" theory
// was WRONG - there is no 4th corner and no scale field anywhere in the disasm. CGlassBit only
// ever has 3 corners (mPosA/B/C, already VALIDATEd) and this function draws exactly one
// triangle (A, B, C) TWICE (front then back, matching the family's usual double-sided idiom),
// each time via PCGfx_DrawQPoly3D (sub_508550, already @Ok) with its 4th vertex position
// DUPLICATED onto the 3rd corner (same xyz, different UV) - a plain trick to get a full 0..1 UV
// square onto a 3-corner shape using the quad-shaped draw call, not a real 4th point.
//
// The gte_ldv0/ldv1/ldv2+rtpt+stsxy3 chain (sub_46D8A0/46D8D0/46D900/46DCE0/46DFA0) the old note
// flagged only feeds a 28-byte scratch/tag record bump-allocated from the SAME shared buffer
// flash.cpp's Flash_Display already documents (0x56FB04/0x5FCD1C, db.cpp's pPoly/PolyBufferEnd,
// reached through G_PPOLY/G_POLY_BUFFER_END). Confirmed dead for the real draw exactly like the family notes already said:
// the actual screen positions come from a completely separate per-corner Algebra_Transform4/invZ
// pipeline (same rawPos[3]/4096.0f + fabsf-eps idiom already used throughout this file, e.g.
// DisplayGLineList). The record's own r/g/b bytes (CGlassBit::mR/mG/mB, offsets 0x67-0x69,
// already VALIDATEd) ARE reused for the real draw color though - read directly from the bit
// instead of round-tripping through the scratch buffer. Skipping the scratch-buffer bump alloc
// (and its capacity bounds check, which would otherwise stop the whole bit loop early once that
// unrelated shared buffer fills) is a deliberate, documented choice, not an oversight.
//
// Blend mode 2 (semi-transparent) and texture slot 1 are set ONCE for the whole list (not
// per-bit), and colour is a fixed-alpha (0xA0) RGB pack straight from mR/mG/mB with no byte
// swap (unlike DisplayChunkBitList/DisplayGLineList's R<->B swap - traced independently, this
// one really does keep R,G,B in that order).
void DisplayGlassList(void** a1)
{
	PCGfx_UseTexture(1, DCGfx_BlendingMode_2);

	RefreshGfxMatrix();

	CGlassBit* pBit = reinterpret_cast<CGlassBit*>(*a1);
	while (pBit)
	{
		f32 rawA[3] = { (f32)pBit->mPosA.vx / 4096.0f, (f32)pBit->mPosA.vy / 4096.0f, (f32)pBit->mPosA.vz / 4096.0f };
		f32 rawB[3] = { (f32)pBit->mPosB.vx / 4096.0f, (f32)pBit->mPosB.vy / 4096.0f, (f32)pBit->mPosB.vz / 4096.0f };
		f32 rawC[3] = { (f32)pBit->mPosC.vx / 4096.0f, (f32)pBit->mPosC.vy / 4096.0f, (f32)pBit->mPosC.vz / 4096.0f };

		f32 xfA[4], xfB[4], xfC[4];
		Algebra_Transform4(xfA, rawA);
		Algebra_Transform4(xfB, rawB);
		Algebra_Transform4(xfC, rawC);

		f32 izA = (fabsf(xfA[3]) > 0.00000001f) ? 1.0f / xfA[3] : -1.0e12f;
		f32 izB = (fabsf(xfB[3]) > 0.00000001f) ? 1.0f / xfB[3] : -1.0e12f;
		f32 izC = (fabsf(xfC[3]) > 0.00000001f) ? 1.0f / xfC[3] : -1.0e12f;

		f32 sxA = xfA[0] * izA, syA = xfA[1] * izA;
		f32 sxB = xfB[0] * izB, syB = xfB[1] * izB;
		f32 sxC = xfC[0] * izC, syC = xfC[1] * izC;

		u32 color = 0xA0000000 | (pBit->mR << 16) | (pBit->mG << 8) | pBit->mB;

		// Face 1: B, A, C (4th vertex duplicates C's position with a different UV corner).
		PCGfx_DrawQPoly3D(
			sxB, syB, izB, 0.0f, 1.0f, color,
			sxA, syA, izA, 0.0f, 0.0f, color,
			sxC, syC, izC, 1.0f, 0.0f, color,
			sxC, syC, izC, 1.0f, 1.0f, color);

		// Face 2: A, B, C (winding flipped vs face 1 - back face).
		PCGfx_DrawQPoly3D(
			sxA, syA, izA, 0.0f, 0.0f, color,
			sxB, syB, izB, 0.0f, 1.0f, color,
			sxC, syC, izC, 1.0f, 0.0f, color,
			sxC, syC, izC, 1.0f, 1.0f, color);

		pBit = reinterpret_cast<CGlassBit*>(pBit->mNext);
	}
}

// Ring-point double buffer (dword_614CD4/614CD8 in the old notes): CLAUDE.md's array-boundary-
// folding warning applies exactly here - raw disasm (`mov [esp+..], 614CDAh`) proves this is ONE
// flat i16[2]-per-point array starting at 0x614CD4 (point i's x/y at +4*i/+4*i+2), not two
// separate globals. Two rings share this one region back to back: ringA (the "previous" ring,
// starts at 0x614CD4) and ringB (the "current" ring being built, starts right after ringA's
// (mNumFringes+1) points) - confirmed because the original computes ringB's base as
// `4*mNumFringes + 0x614CD8` which is algebraically identical to
// `0x614CD4 + 4*(mNumFringes+1)`, i.e. exactly where ringA's own points end.
static u8 * const gGlowRingBase = (u8*)0x614CD4;

// sin/cos table, same address/idiom as camera.cpp's CCamera::CM_Normal (word_610C48[idx]=sin,
// word_610C48[idx+1]=cos, idx = 2*(angle&0xFFF)); file-local copy per repo convention.
static i16 * const word_610C48 = (i16*)0x610C48;
#define GLOW_RING_X(buf, i) (*(i16*)((buf) + 4 * (i)))
#define GLOW_RING_Y(buf, i) (*(i16*)((buf) + 4 * (i) + 2))

// Tentative name/guess, not in idb_globals.txt. A 3-float point (0x56E768/6C/70, confirmed
// float via the raw disasm's `*(float*)&dword_56E770` bit-reinterpretation, NOT a fixed-point
// CVector) the glow's proximity-based "grow near camera" factor measures distance to (see
// below); could be a secondary/near-plane reference position. Naming it a "focus point"
// describes its use here, not a confirmed engine concept.
static f32 * const gGlowFocusPoint = (f32*)0x56E768;

// Tentative name/guess, not in idb_globals.txt. Single float at 0x547E3C used as a "near"
// distance threshold: when the glow's centre is within 10 units beyond this threshold of
// gGlowFocusPoint, invZ gets scaled up by (threshold/distance + 1) - a proximity-based grow-when-
// close bloom effect.
static f32 * const gGlowNearThreshold = (f32*)0x547E3C;

// @Ok
// Factored out of DisplayGlowList's ring-point clamp (-100..612 x range), reused per point.
static INLINE i16 ClampGlowX(i32 v) { if (v > 612) return 612; if (v < -100) return -100; return (i16)v; }

// @Ok
// Factored out of DisplayGlowList's ring-point clamp (-100..340 y range), reused per point.
static INLINE i16 ClampGlowY(i32 v) { if (v > 340) return 340; if (v < -100) return -100; return (i16)v; }

// @Ok
// Factored out of DisplayGlowList's depth-based fade-in colour scale (`(fade*c)>>8`, applied
// only when fade != 0, i.e. depth < 700).
static INLINE u8 GlowFade(u8 c, i32 fade) { return fade ? (u8)((fade * (i32)c) >> 8) : c; }

// @Ok
// Factored out of DisplayGlowList's fixed-alpha-0xA0 RGB colour pack, same idiom as
// DisplayGlassList's inline `0xA0000000 | r<<16 | g<<8 | b`.
static INLINE u32 GlowColor(u8 r, u8 g, u8 b) { return 0xA0000000u | ((u32)r << 16) | ((u32)g << 8) | b; }

// @Ok
// Functional (session-wide functional-only bar, 2026-08-31, third pass). Address 0x40c6f0.
// Fully retraced against a fresh IDA decompile AND raw disassembly (every offset, gate constant
// and record layout below cross-checked against the actual bytes, not just Hex-Rays' rendering,
// since Hex-Rays' typing badly mangles this function's ring-buffer pointer arithmetic). This is
// a radial "starburst" glow/halo: mNumSections angular wedges sweep around by mStepAngle each
// (closing back to the original mAngle on the last wedge), and within each wedge mNumFringes
// concentric quads connect the previous wedge's ring points to the current wedge's ring points,
// plus one triangle-as-quad "cap" per wedge connecting the glow's centre to the innermost ring
// point of each wedge pair (skippable via CGlow::mSkipTriangles).
//
// - The two previously-unnamed helpers sub_4E7840/sub_4E7760 ARE CVector::operator>>/operator-,
//   confirmed (same repo-wide wrongly-INLINE issue CLAUDE.md documents), but since this session's
//   bar is functional correctness (not byte-matching), the relPos math below is written as plain
//   field arithmetic like every other function in this family instead of chasing that operator
//   fix.
// - dword_64E514 (G_VIEW_CLIP_INFO, screen.h) offsets +0xA/+0xE are the same far-limit/half-limit
//   fields the family already uses elsewhere (DisplayGLineList, DisplayFlatBitList); two more
//   fields at +0x10/+0x12 are read here as signed screen-space X/Y origin constants for a
//   close-up linear extrapolation fallback (see below) - still not named/pinned down beyond
//   "clip/viewport descriptor", consistent with the family notes' existing caution.
// - The ring buffer really is one flat region at 0x614CD4 (see gGlowRingBase above), not two
//   separate globals - the old note's "dword_614CD4/614CD8" framing was an IDA fold artifact.
// - The scratch buffer (0x56FB04/0x5FCD1C) really is used for real screen positions and colours
//   here (unlike DisplayGlassList/DisplayFlatBitList's dead copies of the same buffer), so this
//   version computes the same positions/colours directly into locals instead of round-tripping
//   through a byte-packed record - the bump-allocation and its "abort entire function on
//   overflow" bounds check are still reproduced faithfully (3 alloc sites: 8 bytes once per bit,
//   28 bytes per wedge for the cap triangle, 36 bytes per fringe strip quad), since that gates
//   whether later effects in the same frame have buffer room, but the two dead tag dwords each
//   record opens with are not reproduced (never read back anywhere).
// - Colour: CGlow::mCentreCodeBGR (0x4C) for the cap's centre vertex, CGlow::mMask (0x58) bit
//   `sectionIndex` gates whether a wedge draws anything at all, SSection::PadBGR / SFringeQuad::
//   CodeBGR for the ring/fringe colours, all packed as fixed-alpha-0xA0 RGB (GlowColor above,
//   same idiom as DisplayGlassList) and optionally scaled by a depth-based fade-in ramp
//   (255*depth/700+1 while depth<700, matches the family notes).
// - mpFringes is indexed as a flat SSection-major grid: `fringes[row*mNumSections+col]`, where
//   `col` is the wedge/section index and `row` is the fringe-ring index within that wedge -
//   confirmed via the raw pointer strides (`+= 2*mNumSections` DWORDs = one full row per step).
void DisplayGlowList(void** a1)
{
	u8* clip = G_VIEW_CLIP_INFO;
	i32 halfLimit = *(u16*)(clip + 0xE) >> 1;

	PCGfx_UseTexture(1, DCGfx_BlendingMode_2);

	CGlow* pBit = reinterpret_cast<CGlow*>(*a1);
	while (pBit)
	{
		VECTOR relPos;
		relPos.vx = (pBit->mPos.vx >> 12) - gCameraViewPos->vx;
		relPos.vy = (pBit->mPos.vy >> 12) - gCameraViewPos->vy;
		relPos.vz = (pBit->mPos.vz >> 12) - gCameraViewPos->vz;

		gte_ldlv0(&relPos);
		gte_rtps();

		u8* rec8 = reinterpret_cast<u8*>(G_PPOLY);
		if (rec8 + 8 > G_POLY_BUFFER_END)
			return;
		G_PPOLY = reinterpret_cast<u32*>(rec8 + 8);

		VECTOR stlv;
		gte_stlvnl(&stlv);
		i32 depth = stlv.vz;

		f32 rawPos[3];
		rawPos[0] = (f32)pBit->mPos.vx / 4096.0f;
		rawPos[1] = (f32)pBit->mPos.vy / 4096.0f;
		rawPos[2] = (f32)pBit->mPos.vz / 4096.0f;

		f32 xf[4];
		Algebra_Transform4(xf, rawPos);

		f32 invZBase = (fabsf(xf[3]) > 0.00000001f) ? 1.0f / xf[3] : -1.0e12f;

		f32 halfPos[3] = { rawPos[0] * 0.5f, rawPos[1] * 0.5f, rawPos[2] * 0.5f };
		f32 dx = halfPos[0] - gGlowFocusPoint[0];
		f32 dy = halfPos[1] - gGlowFocusPoint[1];
		f32 dz = halfPos[2] - gGlowFocusPoint[2];
		f32 dist = sqrtf(dx * dx + dy * dy + dz * dz);
		f32 growFactor = ((dist - *gGlowNearThreshold) >= 10.0f) ? (*gGlowNearThreshold / dist + 1.0f) : 1.0f;
		f32 invZ = invZBase * growFactor;

		if (depth >= 5 && depth <= 20000)
		{
			i32 fadeScale = (depth < 700) ? (255 * depth / 700 + 1) : 0;

			i32 numSections = (i32)pBit->mNumSections;
			i32 numFringes = (i32)pBit->mNumFringes;
			SSection* sections = pBit->mpSections;
			SFringeQuad* fringes = pBit->mpFringes;

			i32 centerX, centerY;
			if (depth >= halfLimit)
			{
				i32 sxy;
				gte_stsxy(&sxy);
				centerX = (i16)sxy;
				centerY = (i16)(sxy >> 16);
			}
			else
			{
				i32 halfLimitRaw = *(u16*)(clip + 0xE);
				centerX = *(i16*)(clip + 0x10) + stlv.vx * halfLimitRaw / depth;
				centerY = *(i16*)(clip + 0x12) + stlv.vy * halfLimitRaw / depth;
				centerX = (centerX > 1023) ? 1023 : ((centerX < -1024) ? -1024 : centerX);
				centerY = (centerY > 1023) ? 1023 : ((centerY < -1024) ? -1024 : centerY);
			}

			if (centerX != -1024 && centerX != 1023 && centerY != -1024 && centerY != 1023)
			{
				u8* ringA = gGlowRingBase;
				u8* ringB = gGlowRingBase + 4 * (numFringes + 1);

				i32 angle = pBit->mAngle;
				i32 idx0 = 2 * (angle & 0xFFF);
				i32 sin0 = 400 * word_610C48[idx0] / 256;
				i32 cos0 = word_610C48[idx0 + 1];

				i32 outerRadius = (i32)sections[numSections - 1].Radius;
				i32 scaledOuter = 400 * outerRadius / depth;

				i32 x0 = centerX + (scaledOuter * sin0) / 4096;
				i32 y0 = centerY - (scaledOuter * cos0) / 4096;
				GLOW_RING_X(ringA, 0) = ClampGlowX(x0);
				GLOW_RING_Y(ringA, 0) = ClampGlowY(y0);

				i32 seedAccum = outerRadius;
				for (i32 row = 0; row < numFringes; row++)
				{
					seedAccum += (i32)fringes[row * numSections + (numSections - 1)].Width;
					i32 xs = centerX + sin0 * (400 * seedAccum / depth) / 4096;
					i32 ys = centerY - cos0 * (400 * seedAccum / depth) / 4096;
					GLOW_RING_X(ringA, row + 1) = ClampGlowX(xs);
					GLOW_RING_Y(ringA, row + 1) = ClampGlowY(ys);
				}

				i32 wedgeAngle = angle + pBit->mStepAngle;

				for (i32 sectionIndex = 0; sectionIndex < numSections; sectionIndex++)
				{
					bool sectionVisible = ((pBit->mMask >> sectionIndex) & 1) != 0;

					if (sectionIndex == numSections - 1)
						wedgeAngle = pBit->mAngle;

					i32 sectionRadius = (i32)sections[sectionIndex].Radius;
					i32 scaledRadius = 400 * sectionRadius / depth;

					i32 idx = 2 * (wedgeAngle & 0xFFF);
					i32 cosW = word_610C48[idx + 1];
					i32 sinW = 400 * word_610C48[idx] / 256;

					i32 rx = centerX + (sinW * scaledRadius) / 4096;
					i32 ry = centerY - (cosW * scaledRadius) / 4096;
					GLOW_RING_X(ringB, 0) = ClampGlowX(rx);
					GLOW_RING_Y(ringB, 0) = ClampGlowY(ry);

					if (pBit->mSkipTriangles == 0 && sectionVisible)
					{
						u8* rec28 = reinterpret_cast<u8*>(G_PPOLY);
						if (rec28 + 28 > G_POLY_BUFFER_END)
							return;
						G_PPOLY = reinterpret_cast<u32*>(rec28 + 28);

						u8 cr = (u8)(pBit->mCentreCodeBGR & 0xFF);
						u8 cg = (u8)((pBit->mCentreCodeBGR >> 8) & 0xFF);
						u8 cb = (u8)((pBit->mCentreCodeBGR >> 16) & 0xFF);
						u32 centerColor = GlowColor(GlowFade(cr, fadeScale), GlowFade(cg, fadeScale), GlowFade(cb, fadeScale));

						u32 pad = sections[sectionIndex].PadBGR;
						u8 pr = (u8)(pad & 0xFF);
						u8 pg = (u8)((pad >> 8) & 0xFF);
						u8 pb = (u8)((pad >> 16) & 0xFF);
						u32 ringColor = GlowColor(GlowFade(pr, fadeScale), GlowFade(pg, fadeScale), GlowFade(pb, fadeScale));

						f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
						f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;
						f32 z = invZ * 1.03f;

						f32 cxs = centerX * scaleX, cys = centerY * scaleY;
						f32 pxs = GLOW_RING_X(ringA, 0) * scaleX, pys = GLOW_RING_Y(ringA, 0) * scaleY;
						f32 txs = GLOW_RING_X(ringB, 0) * scaleX, tys = GLOW_RING_Y(ringB, 0) * scaleY;

						PCGfx_DrawQPoly3D(
							cxs, cys, z, 0.0f, 0.0f, centerColor,
							pxs, pys, z, 1.0f, 0.0f, ringColor,
							txs, tys, z, 0.0f, 1.0f, ringColor,
							txs, tys, z, 1.0f, 1.0f, ringColor);
					}

					for (i32 row = 0; row < numFringes; row++)
					{
						sectionRadius += (i32)fringes[row * numSections + sectionIndex].Width;
						i32 fx = centerX + sinW * (400 * sectionRadius / depth) / 4096;
						i32 fy = centerY - cosW * (400 * sectionRadius / depth) / 4096;
						GLOW_RING_X(ringB, row + 1) = ClampGlowX(fx);
						GLOW_RING_Y(ringB, row + 1) = ClampGlowY(fy);

						if (sectionVisible)
						{
							u8* rec36 = reinterpret_cast<u8*>(G_PPOLY);
							if (rec36 + 36 > G_POLY_BUFFER_END)
								return;
							G_PPOLY = reinterpret_cast<u32*>(rec36 + 36);

							u32 newCol = fringes[row * numSections + sectionIndex].CodeBGR;
							u32 oldCol = (row != 0) ? fringes[(row - 1) * numSections + sectionIndex].CodeBGR : sections[sectionIndex].PadBGR;

							u8 nr = (u8)(newCol & 0xFF), ng = (u8)((newCol >> 8) & 0xFF), nb = (u8)((newCol >> 16) & 0xFF);
							u8 orr = (u8)(oldCol & 0xFF), og = (u8)((oldCol >> 8) & 0xFF), ob = (u8)((oldCol >> 16) & 0xFF);

							u32 colorNew = GlowColor(GlowFade(nr, fadeScale), GlowFade(ng, fadeScale), GlowFade(nb, fadeScale));
							u32 colorOld = GlowColor(GlowFade(orr, fadeScale), GlowFade(og, fadeScale), GlowFade(ob, fadeScale));

							f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
							f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;
							f32 z = invZ * 1.03f;

							f32 pInnerX = GLOW_RING_X(ringA, row) * scaleX, pInnerY = GLOW_RING_Y(ringA, row) * scaleY;
							f32 tInnerX = GLOW_RING_X(ringB, row) * scaleX, tInnerY = GLOW_RING_Y(ringB, row) * scaleY;
							f32 pOuterX = GLOW_RING_X(ringA, row + 1) * scaleX, pOuterY = GLOW_RING_Y(ringA, row + 1) * scaleY;
							f32 tOuterX = GLOW_RING_X(ringB, row + 1) * scaleX, tOuterY = GLOW_RING_Y(ringB, row + 1) * scaleY;

							PCGfx_DrawQPoly3D(
								pOuterX, pOuterY, z, 0.0f, 0.0f, colorNew,
								tOuterX, tOuterY, z, 1.0f, 0.0f, colorNew,
								pInnerX, pInnerY, z, 0.0f, 1.0f, colorOld,
								tInnerX, tInnerY, z, 1.0f, 1.0f, colorOld);
						}
					}

					u8* tmp = ringA; ringA = ringB; ringB = tmp;
					wedgeAngle += pBit->mStepAngle;
				}
			}
		}

		pBit = reinterpret_cast<CGlow*>(pBit->mNext);
	}
}

// @Ok
// Functional (session-wide functional-only bar, 2026-08-31). Address 0x40bac0. Confirmed
// against a fresh IDA decompile, instruction by instruction: builds one {x,y,z} float triple
// per corner straight from CChunkBit::mWorldPosA/B/C/D (already VALIDATEd) via a plain
// int-to-float CONVERSION with NO >>12 shift (unlike mPosA-D, mWorldPosA-D are already
// world-space floats, presumably filled in by CalculateWorldCoords, still a stub above), then
// runs each through Algebra_Transform4 (already @Ok) to get a homogeneous {x,y,z,w}. Per
// corner: invZ = 1/w (or -1e12 if |w| is ~0, same eps as the rest of the family), then
// screenX = x*invZ, screenY = y*invZ - the SAME triple DisplayQuadBitList's corner-0 builds
// and then throws away (that family note called it "dead"); here it is the ONLY projection
// pipeline used (no GTE calls anywhere in this function's disasm) and its output is what
// actually gets drawn.
//
// No RefreshGfxMatrix call at the top of this function (confirmed against the raw disasm, the
// loop starts immediately) - it relies on gFrameProjMatrix already being set by an earlier
// Display*List call this frame (DisplayQuadBitList, registered before ChunkBitList in
// Bit_Init's slot order, already refreshes it).
//
// If all 4 corners have invZ > 0, the bit draws as a tetrahedron: 4 PCGfx_DrawTPoly3D
// (sub_5081F0, already @Ok) calls, one per face, each omitting exactly one corner (traced
// every argument slot: ABC omits D, ADB omits C, BDC omits A, CDA omits B - every corner
// appears in exactly 3 of the 4 faces, the classic tetrahedron face set). Every face's 3
// triangle corners pull UV from the SAME 3 pairs (mUV0/mUV1/mUV2, offsets 0x94-0xA8) in slot
// order, regardless of which world corner sits in that slot - i.e. UV is per-slot-position, not
// per-corner-identity. Colour is the opposite: it IS tied to corner identity (mColorA follows
// corner A into every face it appears in), repacked with the same byte-swap-to-BGR +
// forced-alpha-0xFF idiom DisplayGLineList already uses. One PCGfx_UseTexture call (clut =
// mClut, opaque blend) covers the whole bit before its faces draw, matching sub_506440's single
// call outside the per-face draws in the disasm.
void DisplayChunkBitList(void** a1)
{
	CChunkBit* pBit = reinterpret_cast<CChunkBit*>(*a1);
	while (pBit)
	{
		const CVector* corners[4] = { &pBit->mWorldPosA, &pBit->mWorldPosB, &pBit->mWorldPosC, &pBit->mWorldPosD };

		f32 sx[4], sy[4], iz[4];
		for (i32 i = 0; i < 4; i++)
		{
			f32 worldPos[3];
			worldPos[0] = (f32)corners[i]->vx;
			worldPos[1] = (f32)corners[i]->vy;
			worldPos[2] = (f32)corners[i]->vz;

			f32 xf[4];
			Algebra_Transform4(xf, worldPos);

			f32 w;
			if (fabsf(xf[3]) > 0.00000001f)
				w = 1.0f / xf[3];
			else
				w = -1.0e12f;

			sx[i] = xf[0] * w;
			sy[i] = xf[1] * w;
			iz[i] = w;
		}

		if (iz[0] > 0.0f && iz[1] > 0.0f && iz[2] > 0.0f && iz[3] > 0.0f)
		{
			PCGfx_UseTexture(pBit->mClut, DCGfx_BlendingMode_0);

			u32 colorA = 0xFF000000 | ((pBit->mColorA & 0xFF) << 16) | (((pBit->mColorA >> 8) & 0xFF) << 8) | ((pBit->mColorA >> 16) & 0xFF);
			u32 colorB = 0xFF000000 | ((pBit->mColorB & 0xFF) << 16) | (((pBit->mColorB >> 8) & 0xFF) << 8) | ((pBit->mColorB >> 16) & 0xFF);
			u32 colorC = 0xFF000000 | ((pBit->mColorC & 0xFF) << 16) | (((pBit->mColorC >> 8) & 0xFF) << 8) | ((pBit->mColorC >> 16) & 0xFF);
			u32 colorD = 0xFF000000 | ((pBit->mColorD & 0xFF) << 16) | (((pBit->mColorD >> 8) & 0xFF) << 8) | ((pBit->mColorD >> 16) & 0xFF);

			// Face ABC (omits D).
			PCGfx_DrawTPoly3D(
				sx[0], sy[0], iz[0], pBit->mUV0[0], pBit->mUV0[1], colorA,
				sx[1], sy[1], iz[1], pBit->mUV1[0], pBit->mUV1[1], colorB,
				sx[2], sy[2], iz[2], pBit->mUV2[0], pBit->mUV2[1], colorC);

			// Face ADB (omits C).
			PCGfx_DrawTPoly3D(
				sx[0], sy[0], iz[0], pBit->mUV0[0], pBit->mUV0[1], colorA,
				sx[3], sy[3], iz[3], pBit->mUV1[0], pBit->mUV1[1], colorD,
				sx[1], sy[1], iz[1], pBit->mUV2[0], pBit->mUV2[1], colorB);

			// Face BDC (omits A).
			PCGfx_DrawTPoly3D(
				sx[1], sy[1], iz[1], pBit->mUV0[0], pBit->mUV0[1], colorB,
				sx[3], sy[3], iz[3], pBit->mUV1[0], pBit->mUV1[1], colorD,
				sx[2], sy[2], iz[2], pBit->mUV2[0], pBit->mUV2[1], colorC);

			// Face CDA (omits B).
			PCGfx_DrawTPoly3D(
				sx[2], sy[2], iz[2], pBit->mUV0[0], pBit->mUV0[1], colorC,
				sx[3], sy[3], iz[3], pBit->mUV1[0], pBit->mUV1[1], colorD,
				sx[0], sy[0], iz[0], pBit->mUV2[0], pBit->mUV2[1], colorA);
		}

		pBit = reinterpret_cast<CChunkBit*>(pBit->mNext);
	}
}

// Two small per-corner scratch buffers the family stages screen coords
// through before drawing (see the family notes and DisplayQuadBitList
// below). Names from idb_globals.txt (gRevisitInitOne/Two); no known field
// layout beyond "8 bytes per corner, up to 4 corners".
static u8 * const gRevisitInitOne = (u8*)0x628618;
static u8 * const gRevisitInitTwo = (u8*)0x654F54;

// Tentative name, address 0x6150C8, not in idb_globals.txt. DisplayQuadBitList
// saves this dword, forces it to 0xFFFF0000 for the whole list walk, then
// restores it before returning; no other decompiled function in the repo
// reads or writes this address yet, so the real purpose is unconfirmed
// (possibly a render-state sentinel some other still-undecompiled function
// reads while quads are being queued).
#define G_QUADBIT_RENDER_STATE (*reinterpret_cast<i32*>(0x006150C8))

// @Ok
// Functional (session-wide functional-only bar, 2026-08-31). Address
// 0x4097e0, found by tracing Bit_Init's RegisterSlot calls (see
// DisplayTextBoxList). Traced fully against Hex-Rays pseudocode plus the
// raw disasm of the opening matrix-refresh loop; see the family notes above
// CSimpleTexturedRibbon::Display and RefreshGfxMatrix above.
//
// Per corner (mPos, mPosB, mPosC, mPosD, in that order): the usual
// gte_ldlv0/gte_rtps camera-space projection (relPos = (raw>>12) -
// gCameraViewPos), then BOTH gte_stlvnl and gte_stsxy are called on the
// projected point. gte_stsxy's packed x/y is written into a scratch record
// in gRevisitInitOne (screenX, screenY, gte_stlvnl's raw vz as a "depth",
// then a constant -256) that nothing else in this function reads back - kept
// here only because it is a real global other code may consume, not because
// the source needs it. gte_stlvnl's full-width vx/vy (screen space, same
// value as gte_stsxy's halves before 16-bit packing) is the pair actually
// used later, staged through gRevisitInitTwo.
//
// Separately, Algebra_Transform4 over the corner divided by 4096.0f (not
// shifted first) gives invZ = 1/w (or -1e12 if |w| is ~0), overridden to
// -1.0 if gte_stlvnl's raw vz for that corner is below 100 (near-clip), then
// always scaled by 1.03. (The disassembly shows corner 0 routing this same
// 1/w value through an extra dead x*invZ/y*invZ temporary and a 3-float
// copy-ctor call before reading it back; corners 1-3 read w directly. Both
// paths produce the same invZ, so this function does not reproduce the
// redundant corner-0 detour - it has no observable effect, only matters for
// byte-for-byte matching which is out of scope this session.)
//
// The screen x/y actually drawn come from gRevisitInitTwo, scaled from GTE
// space into pixels the same way as DisplayTextBoxList
// (gGameResolutionX/Y over Xres/Yres). Vertex color is CQuadBit::mTint's
// low 3 bytes (R,G,B) reassembled into 0xAARRGGBB, with alpha and blend mode
// chosen from CQuadBit::mCodeBGR's 0x40/0x80 bits via PCGfx_UseTexture.
// Two PCGfx_DrawQPoly3D calls draw the quad twice: the first in corner order
// 0,1,2,3 with the standard 0.01/0.99 texture-bleed-inset UVs
// (0,0)/(1,0)/(0,1)/(1,1), the second in order 0,2,1,3 (middle two corners
// swapped) with the SAME position/UV pairing per corner, which only flips
// the triangle winding - i.e. front face then back face of the same quad.
void DisplayQuadBitList(void** a1)
{
	u8*& screenScratch = *reinterpret_cast<u8**>(0x00628618);
	u8*& viewScratch = *reinterpret_cast<u8**>(0x00654F54);
	*reinterpret_cast<i32*>(viewScratch + 4) = 0;
	*reinterpret_cast<i32*>(viewScratch + 12) = 0;
	*reinterpret_cast<i32*>(viewScratch + 20) = 0;
	*reinterpret_cast<i32*>(viewScratch + 28) = 0;


	i32 savedRenderState = G_QUADBIT_RENDER_STATE;
	G_QUADBIT_RENDER_STATE = (i32)0xFFFF0000;
	i32 cameraZ = gCameraViewPos->vz;
	i32 cameraX = gCameraViewPos->vx;
	i32 cameraY = gCameraViewPos->vy;
	for (i32 row = 1; row < 17; row += 4)
	{
		gFrameProjMatrix[row - 1] = gCameraBasisMatrix[row - 1];
		gFrameProjMatrix[row] = gCameraBasisMatrix[row];
		gFrameProjMatrix[row + 1] = gCameraBasisMatrix[row + 1];
		gFrameProjMatrix[row + 2] = gCameraBasisMatrix[row + 2];
	}


	CQuadBit* pBit = reinterpret_cast<CQuadBit*>(*a1);
	while (pBit)
	{
		CVector corners[4];
		corners[0] = pBit->mPos;
		corners[1] = pBit->mPosB;
		corners[2] = pBit->mPosC;
		corners[3] = pBit->mPosD;

		f32 screenX[4];
		f32 screenY[4];
		f32 invZ[4];

		i32 i;

		// The original projects the four corners separately.
		{
			VECTOR relPos;
			relPos.vx = (corners[0].vx >> 12) - cameraX;
			relPos.vy = (corners[0].vy >> 12) - cameraY;
			relPos.vz = (corners[0].vz >> 12) - cameraZ;

			gte_ldlv0(&relPos);
			gte_rtps();

			VECTOR stlv;
			gte_stlvnl(&stlv);

			i32 sxy;
			gte_stsxy(&sxy);
			i16 sx = (i16)sxy;
			i16 sy = (i16)(sxy >> 16);

			u8* rec1 = screenScratch + 0;
			*(i16*)(rec1 + 0) = sx;
			*(i16*)(rec1 + 2) = sy;
			*(i16*)(rec1 + 4) = (i16)stlv.vz;
			*(i16*)(rec1 + 6) = -256;

			u8* rec2 = viewScratch + 0;
			*(i16*)(rec2 + 0) = (i16)stlv.vx;
			*(i16*)(rec2 + 2) = (i16)stlv.vy;

			screenX[0] = (f32)(i16)stlv.vx;
			screenY[0] = (f32)(i16)stlv.vy;

			f32 rawPos[4];
			rawPos[0] = (f32)corners[0].vx / 4096.0f;
			rawPos[1] = (f32)corners[0].vy / 4096.0f;
			rawPos[2] = (f32)corners[0].vz / 4096.0f;
			rawPos[3] = 1.0f;

			f32 xf[4];
			Algebra_Transform4(xf, rawPos);

			f32 iz;
			if (fabsf(xf[3]) > 0.00000001f)
				iz = 1.0f / xf[3];
			else
				iz = -1.0e12f;

			if (stlv.vz < 100)
				iz = -1.0f;

			invZ[0] = iz * 1.03f;
		}
		{
			VECTOR relPos;
			relPos.vx = (corners[1].vx >> 12) - cameraX;
			relPos.vy = (corners[1].vy >> 12) - cameraY;
			relPos.vz = (corners[1].vz >> 12) - cameraZ;

			gte_ldlv0(&relPos);
			gte_rtps();

			VECTOR stlv;
			gte_stlvnl(&stlv);

			i32 sxy;
			gte_stsxy(&sxy);
			i16 sx = (i16)sxy;
			i16 sy = (i16)(sxy >> 16);

			u8* rec1 = screenScratch + 8;
			*(i16*)(rec1 + 0) = sx;
			*(i16*)(rec1 + 2) = sy;
			*(i16*)(rec1 + 4) = (i16)stlv.vz;
			*(i16*)(rec1 + 6) = -256;

			u8* rec2 = viewScratch + 8;
			*(i16*)(rec2 + 0) = (i16)stlv.vx;
			*(i16*)(rec2 + 2) = (i16)stlv.vy;

			screenX[1] = (f32)(i16)stlv.vx;
			screenY[1] = (f32)(i16)stlv.vy;

			f32 rawPos[4];
			rawPos[0] = (f32)corners[1].vx / 4096.0f;
			rawPos[1] = (f32)corners[1].vy / 4096.0f;
			rawPos[2] = (f32)corners[1].vz / 4096.0f;
			rawPos[3] = 1.0f;

			f32 xf[4];
			Algebra_Transform4(xf, rawPos);

			f32 iz;
			if (fabsf(xf[3]) > 0.00000001f)
				iz = 1.0f / xf[3];
			else
				iz = -1.0e12f;

			if (stlv.vz < 100)
				iz = -1.0f;

			invZ[1] = iz * 1.03f;
		}
		{
			VECTOR relPos;
			relPos.vx = (corners[2].vx >> 12) - cameraX;
			relPos.vy = (corners[2].vy >> 12) - cameraY;
			relPos.vz = (corners[2].vz >> 12) - cameraZ;

			gte_ldlv0(&relPos);
			gte_rtps();

			VECTOR stlv;
			gte_stlvnl(&stlv);

			i32 sxy;
			gte_stsxy(&sxy);
			i16 sx = (i16)sxy;
			i16 sy = (i16)(sxy >> 16);

			u8* rec1 = screenScratch + 16;
			*(i16*)(rec1 + 0) = sx;
			*(i16*)(rec1 + 2) = sy;
			*(i16*)(rec1 + 4) = (i16)stlv.vz;
			*(i16*)(rec1 + 6) = -256;

			u8* rec2 = viewScratch + 16;
			*(i16*)(rec2 + 0) = (i16)stlv.vx;
			*(i16*)(rec2 + 2) = (i16)stlv.vy;

			screenX[2] = (f32)(i16)stlv.vx;
			screenY[2] = (f32)(i16)stlv.vy;

			f32 rawPos[4];
			rawPos[0] = (f32)corners[2].vx / 4096.0f;
			rawPos[1] = (f32)corners[2].vy / 4096.0f;
			rawPos[2] = (f32)corners[2].vz / 4096.0f;
			rawPos[3] = 1.0f;

			f32 xf[4];
			Algebra_Transform4(xf, rawPos);

			f32 iz;
			if (fabsf(xf[3]) > 0.00000001f)
				iz = 1.0f / xf[3];
			else
				iz = -1.0e12f;

			if (stlv.vz < 100)
				iz = -1.0f;

			invZ[2] = iz * 1.03f;
		}
		{
			VECTOR relPos;
			relPos.vx = (corners[3].vx >> 12) - cameraX;
			relPos.vy = (corners[3].vy >> 12) - cameraY;
			relPos.vz = (corners[3].vz >> 12) - cameraZ;

			gte_ldlv0(&relPos);
			gte_rtps();

			VECTOR stlv;
			gte_stlvnl(&stlv);

			i32 sxy;
			gte_stsxy(&sxy);
			i16 sx = (i16)sxy;
			i16 sy = (i16)(sxy >> 16);

			u8* rec1 = screenScratch + 24;
			*(i16*)(rec1 + 0) = sx;
			*(i16*)(rec1 + 2) = sy;
			*(i16*)(rec1 + 4) = (i16)stlv.vz;
			*(i16*)(rec1 + 6) = -256;

			u8* rec2 = viewScratch + 24;
			*(i16*)(rec2 + 0) = (i16)stlv.vx;
			*(i16*)(rec2 + 2) = (i16)stlv.vy;

			screenX[3] = (f32)(i16)stlv.vx;
			screenY[3] = (f32)(i16)stlv.vy;

			f32 rawPos[4];
			rawPos[0] = (f32)corners[3].vx / 4096.0f;
			rawPos[1] = (f32)corners[3].vy / 4096.0f;
			rawPos[2] = (f32)corners[3].vz / 4096.0f;
			rawPos[3] = 1.0f;

			f32 xf[4];
			Algebra_Transform4(xf, rawPos);

			f32 iz;
			if (fabsf(xf[3]) > 0.00000001f)
				iz = 1.0f / xf[3];
			else
				iz = -1.0e12f;

			if (stlv.vz < 100)
				iz = -1.0f;

			invZ[3] = iz * 1.03f;
		}

		f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
		f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;

		i32 clut = pBit->mpTexture ? pBit->mpTexture->clut : 1;

		i32 blendMode = 0;
		u32 alpha = 0xFF000000;
		if (pBit->mCodeBGR & 0x40)
		{
			blendMode = (pBit->mCodeBGR & 0x80) ? 2 : 1;
			alpha = (pBit->mCodeBGR & 0x80) ? 0x80000000 : 0xFF000000;
		}

		PCGfx_UseTexture(clut, (DCGfx_BlendingMode)blendMode);

		u32 r = pBit->mTint & 0xFF;
		u32 g = (pBit->mTint >> 8) & 0xFF;
		u32 b = (pBit->mTint >> 16) & 0xFF;
		u32 color = alpha | (r << 16) | (g << 8) | b;

		f32 x[4], y[4];
		for (i = 0; i < 4; i++)
		{
			x[i] = screenX[i] * scaleX;
			y[i] = screenY[i] * scaleY;
		}

		// Front face: corners in order 0,1,2,3.
		PCGfx_DrawQPoly3D(
			x[0], y[0], invZ[0], 0.01f, 0.01f, color,
			x[1], y[1], invZ[1], 0.99f, 0.01f, color,
			x[2], y[2], invZ[2], 0.01f, 0.99f, color,
			x[3], y[3], invZ[3], 0.99f, 0.99f, color);

		// Back face: corners in order 0,2,1,3 (winding flipped).
		PCGfx_DrawQPoly3D(
			x[0], y[0], invZ[0], 0.01f, 0.01f, color,
			x[2], y[2], invZ[2], 0.01f, 0.99f, color,
			x[1], y[1], invZ[1], 0.99f, 0.01f, color,
			x[3], y[3], invZ[3], 0.99f, 0.99f, color);

		pBit = reinterpret_cast<CQuadBit*>(pBit->mNext);
	}

	G_QUADBIT_RENDER_STATE = savedRenderState;
}

// field_48 CONTRADICTION RESOLVED (2026-08-31, 4th pass): it is a per-point packed RGB colour
// array, not "widths". Proof: CSimpleTexturedRibbon::SetRGB (below) already packs
// `value = r | (g<<8) | (b<<16)` into every entry of this->field_48 (field_3C+1 of them). The raw
// disasm of Display's final draw loop (0x40b2f1-0x40b3a0) reads `field_48[faceIndex]` and unpacks
// it BYTE-FOR-BYTE the same way (byte0->R, byte1->G, byte2->B), then combines it with an alpha
// byte derived from pTextures[0]'s blend flags into a 0xAARRGGBB colour passed straight to
// PCGfx_DrawQPoly3D. Renamed field_48 -> pColours across the class (bit.h) to match; also renamed
// SSimpleRibbonParams::field_18 -> mWidth and added its mPos field (was inside PADDING(0x18)),
// both confirmed by this same trace.
//
// sub_4E7090, the helper this function calls to get the per-segment perpendicular "width axis"
// vector, turns out to be the ALREADY-@Ok Utils_CalcUnitFacingCamera (utils.cpp): same address
// (0x4e7090, confirmed in names.json) and its body is an exact algorithm match against the raw
// disasm here (delta = (a2-a1)>>12, further >>4 past a 500 overflow guard; toCamera =
// gMikeCamera[0].Position - a1; cross = delta x toCamera via gte_ldopv1/ldopv2/gte_op0 - gte_op0
// is 0x46D6A0, a genuinely separate address from gte_op12's 0x46D700 but the same shape; magnitude
// via gte_sqr0+M3dMaths_SquareRoot0; zeroed if magnitude<5, else scaled to magnitude 16). So this
// function just calls Utils_CalcUnitFacingCamera directly instead of reimplementing it.

// @Ok
// Functional. Shared per-edge projection step factored out of CSimpleTexturedRibbon::Display
// below (both the main loop and the tail cross-section repeat this exact sequence): project one
// ribbon-strip corner through the GTE (relPos = (edge>>12) - gCameraViewPos, gte_ldlv0/gte_rtps),
// compute its invZ via Algebra_Transform4 (with the near-clip override to -1.0f when the GTE
// depth is below 100), append invZ to the flat scratch array and a screen x/y + depth record to
// gRevisitInitOne. outStlv optionally returns the raw gte_stlvnl result for the tail section's
// extra gRevisitInitTwo write (the original reads back the SAME gte_stlvnl call for both writes,
// not two separate calls).
static void ProjectRibbonEdge(const CVector& edge, f32* invZArray, i32& invZIndex, u8*& recCursor, VECTOR* outStlv)
{
	VECTOR relPos;
	relPos.vx = (edge.vx >> 12) - gCameraViewPos->vx;
	relPos.vy = (edge.vy >> 12) - gCameraViewPos->vy;
	relPos.vz = (edge.vz >> 12) - gCameraViewPos->vz;

	gte_ldlv0(&relPos);
	gte_rtps();

	f32 rawPos[4];
	rawPos[0] = (f32)edge.vx / 4096.0f;
	rawPos[1] = (f32)edge.vy / 4096.0f;
	rawPos[2] = (f32)edge.vz / 4096.0f;
	rawPos[3] = 1.0f;

	f32 xf[4];
	Algebra_Transform4(xf, rawPos);

	f32 iz;
	if (fabsf(xf[3]) > 0.00000001f)
		iz = 1.0f / xf[3];
	else
		iz = -1.0e12f;

	VECTOR stlv;
	gte_stlvnl(&stlv);

	i32 sxy;
	gte_stsxy(&sxy);

	*(i16*)(recCursor + 0) = (i16)sxy;
	*(i16*)(recCursor + 2) = (i16)(sxy >> 16);
	*(i16*)(recCursor + 4) = (i16)stlv.vz;
	*(i16*)(recCursor + 6) = -256;
	recCursor += 8;

	if (stlv.vz < 100)
		iz = -1.0f;

	invZArray[invZIndex++] = iz;

	if (outStlv)
		*outStlv = stlv;
}

// @Ok
// Functional (session-wide functional-only bar, 2026-08-31). Address 0x40aa00. This is a
// billboarded ("camera-facing") textured ribbon quad-strip over mNumFacesToDisplay+1 spine points
// (this->field_44), verified against the full decompile plus a raw-disasm cross-check of the tail
// section (0x40b060-0x40b3a0, where Hex-Rays reuses/relabels stack slots across unrelated values).
//   1. RefreshGfxMatrix() (see above), unconditionally, once - matches DisplayQuadBitList's shape,
//      not DisplayTextBoxList's "already refreshed by an earlier call this frame" shortcut.
//   2. For each spine index i in [0, field_3E): crossVec = Utils_CalcUnitFacingCamera(
//      &spine[i+1].mPos, &spine[i].mPos, &crossVec) (args are NEXT point, CURRENT point - reversed
//      from a naive prev/next reading, confirmed at the call site), delta = spine[i].mWidth *
//      crossVec (per axis), edgeA = spine[i].mPos - delta, edgeB = spine[i].mPos + delta. Both
//      edges are projected through the shared GTE pipeline: gte_SetRotMatrix(&gBitDisplayMatrix)
//      (same 0x56F224 address as spidey.cpp/panel.cpp's stru_56F224 - reusing bit.cpp's own
//      Bit_Display-owned gBitDisplayMatrix instead of adding a second alias) + m3d_ZeroTransVector()
//      ONCE per segment (confirmed: no second SetRotMatrix call between edgeA and edgeB, only two
//      ldlv0/rtps pairs), then gte_ldlv0/gte_rtps per edge. Each edge appends its invZ (via
//      Algebra_Transform4, same idiom as the rest of this file - the original inlines this call at
//      one call site via a manual vector4d_ctor dot-product, confirmed functionally identical, not
//      chased separately) to a flat float scratch array, and appends its screen x/y (via
//      gte_stsxy) plus GTE depth to an 8-byte record in gRevisitInitOne (near-clip: invZ forced to
//      -1.0f when depth<100).
//   3. ONE more "tail" cross-section is built the exact same way from spine[field_3E] (the point
//      one past the loop - needs field_3E+1 spine points for a field_3E-face ribbon, matching a
//      quad strip needing N+1 cross-sections for N quads), reusing the LAST iteration's crossVec/
//      mWidth (no new Utils_CalcUnitFacingCamera call) AND the GTE rotation matrix still set by
//      that iteration's edgeA (confirmed via raw disasm: no gte_SetRotMatrix call anywhere after
//      the main loop). This tail section is also the only place gRevisitInitTwo gets written, one
//      8-byte {stlv.vx, stlv.vy, 0, 0} record per edge - nothing in this function reads it back,
//      reproduced anyway since it is a real global other code may consume.
//   4. PCGfx_UseTexture is called once for the whole ribbon: clut = pTextures[0].mClut, blend
//      mode/alpha from pTextures[0].field_0's 0x40/0x80 bits (opaque unless the semi-transparent
//      bits are set - same shape as SetOpaque/SetSemiTransparent above).
//   5. Draw loop, field_3E iterations: quad k connects cross-section k (edgeA_k, edgeB_k) to
//      cross-section k+1 (edgeA_{k+1}, edgeB_{k+1}) - a classic overlapping quad strip, screen x/y
//      read back from gRevisitInitOne (scaled by gGameResolutionX/Y over Xres/Yres, same idiom as
//      DisplayQuadBitList) and invZ from the flat scratch array. Colour is flat per quad,
//      pColours[k] unpacked to RGB with the alpha from step 4. UV is (0.01,0.01)/(0.01,0.99)/
//      (0.99,0.01)/(0.99,0.99) per corner (u = which cross-section, v = which edge), the same
//      texture-bleed-inset pattern PCGfx_DrawQPoly3D's other callers use.
//   6. G_COLOUR_TABLE (ps2m3d.h, 0x64F5D0 - a scratch pointer several unrelated subsystems reuse,
//      e.g. ps2m3d.cpp's colour-pulse code reads it back) is set to this->pColours at the end,
//      unconditionally once mNumFacesToDisplay != 0 - a real side effect on shared game state,
//      reproduced for correctness even though nothing in this function reads it back itself.
void CSimpleTexturedRibbon::Display(void)
{
	if (this->field_3E == 0)
		return;

	i32 savedRenderState = G_QUADBIT_RENDER_STATE;
	G_QUADBIT_RENDER_STATE = (i32)0xFFFF0000;

	print_if_false(this->field_3E <= this->field_3C, "Bad mNumFacesToDisplay");
	print_if_false(2 * this->field_3E + 2 <= 1000, "CSimpleTexturedRibbon::mNumFacesToDisplay too big to display");

	RefreshGfxMatrix();

	SSimpleRibbonParams* spine = this->field_44;

	// Confirmed name (maintainer's IDB): a large, general-purpose scratch buffer shared by
	// several unrelated subsystems (see dcmodel.cpp/spool.cpp for other users). Display borrows
	// it here as the flat invZ scratch array, 2 floats per cross-section.
	// 0x5498FC holds the POINTER to the scratch buffer, not the buffer
	f32* invZArray = static_cast<f32*>(gSpoolSystemMemory);
	i32 invZIndex = 0;

	u8* recCursor = *reinterpret_cast<u8**>(gRevisitInitOne);

	CVector crossVec(0, 0, 0);
	i32 width = 0;

	for (i32 i = 0; i < this->field_3E; i++)
	{
		Utils_CalcUnitFacingCamera(&spine[i + 1].mPos, &spine[i].mPos, &crossVec);
		width = (i32)spine[i].mWidth;

		i32 dx = width * crossVec.vx;
		i32 dy = width * crossVec.vy;
		i32 dz = width * crossVec.vz;

		CVector edgeA(spine[i].mPos.vx - dx, spine[i].mPos.vy - dy, spine[i].mPos.vz - dz);
		CVector edgeB(spine[i].mPos.vx + dx, spine[i].mPos.vy + dy, spine[i].mPos.vz + dz);

		gte_SetRotMatrix(gBitDisplayMatrix);
		m3d_ZeroTransVector();
		ProjectRibbonEdge(edgeA, invZArray, invZIndex, recCursor, 0);

		ProjectRibbonEdge(edgeB, invZArray, invZIndex, recCursor, 0);
	}

	// Tail cross-section: one past the last segment, reusing the last iteration's crossVec/width
	// and GTE rotation matrix (see the function comment above).
	{
		i32 dx = width * crossVec.vx;
		i32 dy = width * crossVec.vy;
		i32 dz = width * crossVec.vz;

		SSimpleRibbonParams* tail = &spine[this->field_3E];
		CVector edgeA(tail->mPos.vx - dx, tail->mPos.vy - dy, tail->mPos.vz - dz);
		CVector edgeB(tail->mPos.vx + dx, tail->mPos.vy + dy, tail->mPos.vz + dz);

		VECTOR stlvA, stlvB;
		ProjectRibbonEdge(edgeA, invZArray, invZIndex, recCursor, &stlvA);
		ProjectRibbonEdge(edgeB, invZArray, invZIndex, recCursor, &stlvB);

		u8* rec2 = *reinterpret_cast<u8**>(gRevisitInitTwo);
		*(i16*)(rec2 + 0) = (i16)stlvA.vx;
		*(i16*)(rec2 + 2) = (i16)stlvA.vy;
		*(i16*)(rec2 + 4) = 0;
		*(i16*)(rec2 + 6) = 0;

		*(i16*)(rec2 + 8 + 0) = (i16)stlvB.vx;
		*(i16*)(rec2 + 8 + 2) = (i16)stlvB.vy;
		*(i16*)(rec2 + 8 + 4) = 0;
		*(i16*)(rec2 + 8 + 6) = 0;
	}

	i32 blendMode = 0;
	u32 alpha = 0xFF000000;
	if (this->pTextures[0].field_0 & 0x40)
	{
		blendMode = (this->pTextures[0].field_0 & 0x80) ? 2 : 1;
		alpha = (this->pTextures[0].field_0 & 0x80) ? 0x80000000 : 0xFF000000;
	}

	PCGfx_UseTexture(this->pTextures[0].mClut, (DCGfx_BlendingMode)blendMode);

	f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
	f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;

	i16* sxyBase = *reinterpret_cast<i16**>(gRevisitInitOne);

	for (i32 k = 0; k < this->field_3E; k++)
	{
		i16* rec0 = sxyBase + 4 * (2 * k + 0);
		i16* rec1 = sxyBase + 4 * (2 * k + 1);
		i16* rec2 = sxyBase + 4 * (2 * k + 2);
		i16* rec3 = sxyBase + 4 * (2 * k + 3);

		f32 x0 = (f32)rec0[0] * scaleX, y0 = (f32)rec0[1] * scaleY;
		f32 x1 = (f32)rec1[0] * scaleX, y1 = (f32)rec1[1] * scaleY;
		f32 x2 = (f32)rec2[0] * scaleX, y2 = (f32)rec2[1] * scaleY;
		f32 x3 = (f32)rec3[0] * scaleX, y3 = (f32)rec3[1] * scaleY;

		u32 packedColour = this->pColours[k];
		u32 r = packedColour & 0xFF;
		u32 g = (packedColour >> 8) & 0xFF;
		u32 b = (packedColour >> 16) & 0xFF;
		u32 color = alpha | (r << 16) | (g << 8) | b;

		f32 iz0 = invZArray[2 * k + 0];
		f32 iz1 = invZArray[2 * k + 1];
		f32 iz2 = invZArray[2 * k + 2];
		f32 iz3 = invZArray[2 * k + 3];

		PCGfx_DrawQPoly3D(
			x0, y0, iz0, 0.01f, 0.01f, color,
			x1, y1, iz1, 0.01f, 0.99f, color,
			x2, y2, iz2, 0.99f, 0.01f, color,
			x3, y3, iz3, 0.99f, 0.99f, color);
	}

	G_COLOUR_TABLE = reinterpret_cast<u32*>(this->pColours);

	G_QUADBIT_RENDER_STATE = savedRenderState;
}


// @Ok
// Functional (session-wide functional-only bar, 2026-08-30). Address 0x40d7c0,
// found by tracing CBitServer::RegisterSlot calls inside Bit_Init (0x407fc0):
// the first 6 slots (TextBoxList..GPolyLineList) register through an inlined
// hashtable insert instead of a visible RegisterSlot call, so the callback
// addresses do not show up in names.json. Field offsets verified against the
// CBit VALIDATE block below: mFric is CFriction (3 u8, offset 0x34) and holds
// the box color, mAge/mLifetime (0xC/0xE) drive a 16-frame pop-in/pop-out grow
// animation, mPos.vx/vy (0x10/0x14) and mVel.vx/vy (0x1C/0x20) hold position
// and size (CVector members are i32 in this codebase, not floats, so no float
// conversion happens, matching the raw dword loads in the disassembly). This
// was attempted once before in this branch's history and reverted as
// "unverified"; the revert was correct procedure at the time (no address was
// known), but every field and call in that draft turns out to match this
// decompile exactly, so it is restored here with the address now confirmed.
// Coordinates are stored in the logical Xres x Yres space and scaled to the
// real screen size before drawing, same idiom as the already-@Ok
// Panel_DrawFlatShadedPoly (panel.cpp) and PCGfx_DrawTexture2D-family code
// (dcshellutils.cpp).
void DisplayTextBoxList(void** a1)
{
	CTextBox* pBox = reinterpret_cast<CTextBox*>(*a1);

	while (pBox)
	{
		if ((u8*)G_PPOLY + sizeof(POLY_F4) > G_POLY_BUFFER_END)
			return;

		POLY_F4* p = (POLY_F4*)G_PPOLY;
		G_PPOLY = (u32*)((u8*)G_PPOLY + sizeof(POLY_F4));

		if (!gPrintStubbed)
			gsub_46CB90((void*)"stubbed out: setPolyF4");

		p->r0 = pBox->mFric.vx;
		p->g0 = pBox->mFric.vy;
		p->b0 = pBox->mFric.vz;

		i16 age = pBox->mAge;
		i32 x = pBox->mPos.vx;
		i32 y = pBox->mPos.vy;
		i32 w = pBox->mVel.vx;
		i32 h = pBox->mVel.vy;

		if (age < 16)
		{
			y += (u32)((16 - age) * h) >> 5;
			h = (u32)(age * h) >> 4;
		}
		else
		{
			i32 lifetime = pBox->mLifetime;
			if (age > lifetime - 16)
			{
				i32 rem = lifetime - age;
				y += (u32)((16 - rem) * h) >> 5;
				h = (u32)(rem * h) >> 4;
			}
		}

		p->x0 = (i16)x;
		p->y0 = (i16)y;
		p->x1 = (i16)(x + w);
		p->y1 = (i16)y;
		p->x2 = (i16)x;
		p->y2 = (i16)(y + h);
		p->x3 = (i16)(x + w);
		p->y3 = (i16)(y + h);

		gsub_46CB90(G_RENDER_BUF);

		PCGfx_UseTexture(1, DCGfx_BlendingMode_0);

		u32 color = 0xA0000000 | (p->r0 << 16) | (p->g0 << 8) | p->b0;
#ifdef SPIDEY_STANDALONE
		if (getenv("SPIDEY_TRACE_TEXTBOX"))
			printf("TEXTBOX age=%d life=%d x=%d y=%d w=%d h=%d color=%08x scene=%d\n",
					age, pBox->mLifetime, x, y, w, h, color, (i32)G_SCENE_RELATED);
#endif
		f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
		f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;

		PCGfx_DrawQPoly2D(
				p->x0 * scaleX, p->y0 * scaleY, 0.0f, 0.0f, color,
				p->x1 * scaleX, p->y1 * scaleY, 1.0f, 0.0f, color,
				p->x2 * scaleX, p->y2 * scaleY, 0.0f, 1.0f, color,
				p->x3 * scaleX, p->y3 * scaleY, 1.0f, 1.0f, color,
				5.0f);

		pBox = reinterpret_cast<CTextBox*>(pBox->mNext);
	}
}

// @NotOk
// Original 0x40DBD0. Projection setup and inverse depth are checked against original bytes.
// The remaining renderer retains 555 MSVC mnemonic differences; matching is unfinished.
// The field layouts and masks below were recovered from the original:
//
// - The u16 at offset 0x66 IS a real per-instance CFlatBit field (added to bit.h as
//   mClutOverride): CMotionBlur/CFrag (plain CFlatBit, no extra fields of their own) both
//   VALIDATE_SIZE at 0x68, which only works if CFlatBit itself is 0x68 bytes - i.e. this u16
//   is real storage, not compiler tail padding. Confirmed by the raw disasm (`mov cx,[ebx+66h]`)
//   independently of which CFlatBit subclass is actually walking the list; every subclass shares
//   the same base-class field, so no need to know which concrete class populated FlatBitList.
// - The 32-vs-16-bit corner-packing split really is CFT4Bit::mBitFlags bit 0x1 alone (previous
//   pass's correction still holds).
// - `dword_64E514+0xA`/`+0xE` (G_VIEW_CLIP_INFO, screen.h) are read here as a "far" limit and a
//   half-limit respectively, gating a perspective-scale visibility test together with a hardcoded
//   raw-depth floor of 200 - semantics beyond "visibility gate" not pinned down further, not
//   needed to translate the code faithfully.
// - The scratch buffer (0x56FB04/0x5FCD1C) bump-allocation and its overflow bail (abandons the
//   REST of the list, not just the current bit - confirmed via the raw disasm jump target, which
//   is the function's own return) is real and reproduced. Its content writes (texpage/clut/UV
//   fields at rec+12/20/28/36, and four debug-assert byte decrements guarded by
//   `nullsub_1(cond, "Sub on zero")` = print_if_false, at rec+20/29/36/37) are all confirmed dead
//   for the actual draw call below (never read back anywhere in this function or elsewhere in the
//   repo) and are skipped, same documented choice as DisplayGlassList's scratch record.
// - When the visibility gate fails, the original rewinds the bump allocation
//   (`G_PPOLY = rec`) instead of leaving it consumed - reproduced below.
// - Colour is CFT4Bit::mCodeBGR's low 3 bytes as (R,G,B) with alpha forced to 0x80 (semi
//   -transparent, blend mode 2) when CFlatBit::mSemiTransparencyRate == 32, else 0xFF (opaque,
//   blend mode 0) - read straight from the bit instead of round-tripping through the dead scratch
//   record. Clut for PCGfx_UseTexture is mpPSXFrame->pTexture->clut (Texture::clut, texture.h).
// - Only the sprite centre (CBit::mPos) goes through the camera-space GTE transform
//   (gte_ldlv0/gte_rtps) and the separate inverse-depth pass. The original refreshes the camera
//   matrix at entry and adds the homogeneous translation term once; all 4
//   corners share that single invZ. The 4 corners' screen positions are built directly from
//   SAnimFrame::OffX/OffY/Width/Height (mpPSXFrame->pTexture's owning frame), CFT4Bit::mScale
//   (perspective falloff with raw depth), CFlatBit::mPostScale (packed lo/hi u16 X/Y scale) and,
//   when CFlatBit::mAngle != 0, a rcossin_tbl (word_610C48/610C4A, same idiom as CCamera::CM_Normal
//   in camera.cpp) rotation of the frame rectangle. This matches a billboarded, optionally
//   rotated, 2D sprite quad - consistent with the class family name (CSimpleAnim/CMotionBlur/
//   CCombatImpactRing/CFrag are all "flat" 2D effect sprites).
// - Single PCGfx_DrawQPoly3D call per bit (no back-face pass, unlike DisplayQuadBitList/
//   DisplayGlassList), standard 0.01/0.99 texture-bleed-inset UVs, gated on
//   `mpPSXFrame->pTexture->clut != 0 && corners-all-in-range`.
EXPORT void DisplayFlatBitList(void** a1)
{
	RefreshGfxMatrix();
	u8* clip = G_VIEW_CLIP_INFO;
	i32 farLimit = *(u16*)(clip + 0xA);
	i32 halfLimit = *(u16*)(clip + 0xE) >> 1;

	CFlatBit* pBit = reinterpret_cast<CFlatBit*>(*a1);
	while (pBit)
	{
		VECTOR relPos;
		relPos.vx = (pBit->mPos.vx >> 12) - gCameraViewPos->vx;
		relPos.vy = (pBit->mPos.vy >> 12) - gCameraViewPos->vy;
		relPos.vz = (pBit->mPos.vz >> 12) - gCameraViewPos->vz;

		gte_ldlv0(&relPos);
		gte_rtps();

		f32 rawPos[3];
		rawPos[0] = (f32)pBit->mPos.vx / 4096.0f;
		rawPos[1] = (f32)pBit->mPos.vy / 4096.0f;
		rawPos[2] = (f32)pBit->mPos.vz / 4096.0f;

		// The original adds the translation term directly, so homogeneous w is 1.
		f32 inverseW = gFrameProjMatrix[3] * rawPos[0]
				+ gFrameProjMatrix[11] * rawPos[2]
				+ gFrameProjMatrix[7] * rawPos[1] + gFrameProjMatrix[15];
		f32 invZ = (fabsf(inverseW) > 0.00000001f) ? 1.0f / inverseW : -1.0e12f;

		u8* rec = reinterpret_cast<u8*>(G_PPOLY);
		if (rec + 40 > G_POLY_BUFFER_END)
			break;
		G_PPOLY = reinterpret_cast<u32*>(rec + 40);

		i32 depth;
		gte_stlvnl2(&depth);

		bool visible = false;
		i32 scale = 0;
		if (depth >= halfLimit && depth <= farLimit && depth >= 200)
		{
			scale = (i32)(((u32)(pBit->mScale << 7)) / (u32)depth);
			visible = ((u32)scale <= 0x200) || (pBit->mBitFlags & 8);
		}

		if (!visible)
		{
			G_PPOLY = reinterpret_cast<u32*>(rec);
			pBit = reinterpret_cast<CFlatBit*>(pBit->mNext);
			continue;
		}

		i32 packedCenter;
		gte_stsxy(&packedCenter);

		SAnimFrame* frame = pBit->mpPSXFrame;
		i32 offX = frame->OffX;
		i32 offY = frame->OffY;
		i32 width = frame->Width;
		i32 height = frame->Height;
		Texture* pTexture = frame->pTexture;

		i32 halfW = (scale * (i32)(pBit->mPostScale & 0xFFFF)) >> 12;
		i32 halfH = (scale * (i32)(pBit->mPostScale >> 16)) >> 12;

		bool highPrecision = (pBit->mBitFlags & 1) != 0;
		i32 outOfRange = 0;

		i16 cx[4], cy[4];

		if (pBit->mAngle != 0)
		{
			i32 idx = pBit->mAngle & 0xFFF;
			i32 sinA = G_RCOSSIN_TBL[idx].sin;
			i32 cosA = G_RCOSSIN_TBL[idx].cos;

			i32 t54 = sinA * offX;
			i32 t31 = cosA * offX;
			i32 t53 = offY * sinA;
			i32 t55 = offY * cosA;
			i32 t32 = t54 + width * sinA;
			i32 t63 = t53 + height * sinA;
			i32 t34 = t31 + width * cosA;
			i32 t60 = t55 + height * cosA;

			if (highPrecision)
			{
				i32 dw16 = (((halfH * (t54 + t55)) >> 3) & 0x1FFF0000) + packedCenter + ((halfW * (t31 - t53)) >> 19);
				i32 dw8  = (((halfH * (t55 + t32)) >> 3) & 0x1FFF0000) + packedCenter + ((halfW * (t34 - t53)) >> 19);
				i32 dw32 = (((halfH * (t54 + t60)) >> 3) & 0x1FFF0000) + packedCenter + ((halfW * (t31 - t63)) >> 19);
				i32 dw24 = (((halfH * (t32 + t60)) >> 3) & 0x1FFF0000) + packedCenter + ((halfW * (t34 - t63)) >> 19);

				cx[0] = (i16)dw8;  cy[0] = (i16)(dw8 >> 16);
				cx[1] = (i16)dw16; cy[1] = (i16)(dw16 >> 16);
				cx[2] = (i16)dw24; cy[2] = (i16)(dw24 >> 16);
				cx[3] = (i16)dw32; cy[3] = (i16)(dw32 >> 16);
			}
			else
			{
				i16 x0 = (i16)(packedCenter + (i32)((u16)((halfW * (t31 - t53)) >> 16) >> 3));
				i16 y0 = (i16)((packedCenter >> 16) + (i32)((u16)((halfH * (t54 + t55)) >> 16) >> 3));
				i16 x1 = (i16)(packedCenter + (i32)((u16)((halfW * (t34 - t53)) >> 16) >> 3));
				i16 y1 = (i16)((packedCenter >> 16) + (i32)((u16)((halfH * (t55 + t32)) >> 16) >> 3));
				i16 x2 = (i16)(packedCenter + (i32)((u16)((halfW * (t31 - t63)) >> 16) >> 3));
				i16 y2 = (i16)((packedCenter >> 16) + (i32)((u16)((halfH * (t60 + t54)) >> 16) >> 3));
				i16 x3 = (i16)(packedCenter + (i32)((u16)((halfW * (t34 - t63)) >> 16) >> 3));
				i16 y3 = (i16)((packedCenter >> 16) + (i32)((u16)((halfH * (t32 + t60)) >> 16) >> 3));

				i16 ccx = (i16)packedCenter;
				i16 ccy = (i16)(packedCenter >> 16);
				if (ccx < -256 || ccy < -256 || ccx > 768 || ccy > 360)
					outOfRange = 1;

				cx[0] = x0 & 0xFFF; cy[0] = y0 & 0xFFF;
				cx[1] = x1 & 0xFFF; cy[1] = y1 & 0xFFF;
				cx[2] = x2 & 0xFFF; cy[2] = y2 & 0xFFF;
				cx[3] = x3 & 0xFFF; cy[3] = y3 & 0xFFF;
			}
		}
		else
		{
			i16 v21 = (i16)((offX * halfW) >> 7);
			i32 v23 = (height * halfH) & 0xFFFFFF80;
			i32 v24 = (width * halfW) >> 7;
			i32 v25 = packedCenter + ((0x7FFF80 & (offY * halfH)) << 9) + v21;
			i32 v26 = v23 << 9;

			if ((u32)v24 < 5)
				v24 = 2;
			if ((u32)v26 < 0x50000)
				v26 = 0x10000;

			if (highPrecision)
			{
				i32 dw16 = v25;
				i32 dw8  = v24 + v25;
				i32 dw32 = v26 + v25;
				i32 dw24 = v25 + v24 + v26;

				cx[0] = (i16)dw8;  cy[0] = (i16)(dw8 >> 16);
				cx[1] = (i16)dw16; cy[1] = (i16)(dw16 >> 16);
				cx[2] = (i16)dw24; cy[2] = (i16)(dw24 >> 16);
				cx[3] = (i16)dw32; cy[3] = (i16)(dw32 >> 16);
			}
			else
			{
				i16 x0 = (i16)v25;
				i16 y0 = (i16)(v25 >> 16);
				i16 x1 = (i16)(v24 + v25);
				i16 y1 = (i16)(v25 >> 16);
				i16 x2 = (i16)v25;
				i16 y2 = (i16)((v26 >> 16) + (v25 >> 16));
				i16 x3 = (i16)(v24 + v25);
				i16 y3 = (i16)((v26 >> 16) + (v25 >> 16));

				i16 ccx = (i16)v25;
				i16 ccy = (i16)(v25 >> 16);
				if (ccx < -256 || ccy < -256 || ccx > 768 || ccy > 360)
					outOfRange = 1;

				cx[0] = x0 & 0xFFF; cy[0] = y0 & 0xFFF;
				cx[1] = x1 & 0xFFF; cy[1] = y1 & 0xFFF;
				cx[2] = x2 & 0xFFF; cy[2] = y2 & 0xFFF;
				cx[3] = x3 & 0xFFF; cy[3] = y3 & 0xFFF;
			}
		}

		u16 clut = pTexture->clut;
		if (clut != 0 && outOfRange == 0)
		{
			i32 blendMode = (pBit->mSemiTransparencyRate == 32) ? 2 : 0;
			u32 alpha = (pBit->mSemiTransparencyRate == 32) ? 0x80 : 0xFF;

			PCGfx_UseTexture(clut, (DCGfx_BlendingMode)blendMode);

			u32 color = (alpha << 24) | ((pBit->mCodeBGR & 0xFF) << 16) |
					(((pBit->mCodeBGR >> 8) & 0xFF) << 8) | ((pBit->mCodeBGR >> 16) & 0xFF);

			f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
			f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;

			PCGfx_DrawQPoly3D(
				cx[0] * scaleX, cy[0] * scaleY, invZ, 0.01f, 0.01f, color,
				cx[1] * scaleX, cy[1] * scaleY, invZ, 0.99f, 0.01f, color,
				cx[2] * scaleX, cy[2] * scaleY, invZ, 0.01f, 0.99f, color,
				cx[3] * scaleX, cy[3] * scaleY, invZ, 0.99f, 0.99f, color);
		}

		pBit = reinterpret_cast<CFlatBit*>(pBit->mNext);
	}
}

// @Ok
// Functional (session-wide functional-only bar, 2026-08-31). Address
// 0x40e840, found by tracing Bit_Init's RegisterSlot calls (see
// DisplayTextBoxList). Fully traced against the raw disassembly this
// session (previous note's "CBit pairs" framing was a guess; the real
// operand is CLinked2EndedBit, bit.h).
//
// CLinked2EndedBit::field_58/field_64 (both CVector, VALIDATEd above) are
// one segment's two world-space endpoints. Consecutive nodes in the
// registered list form a connected quad-strip ribbon: CRibbon::CRibbon
// (above) sets mBits[0]->mBitFlags |= 0x10 to mark a chain's first bit.
//
// Per node: unless mBitFlags & 0x10 (chain start) or the previous node's
// segment failed (clipped or zero-length), the start point and its
// perpendicular half-width corners are simply reused from the previous
// node's end point/corners (screen-space strip continuity, no GTE work).
// Otherwise the start point is freshly projected from this node's own
// field_58 via the gte_ldlv0/gte_rtps/gte_stlvnl2/gte_stsxy idiom (same as
// Screen_DrawArrow, screen.cpp), clip-tested against G_VIEW_CLIP_INFO's
// depth range, with its own perspective-scaled half width from
// CFT4Bit::mScale and the current frame's Height (CFT4Bit::mpPSXFrame->
// pTexture, SAnimFrame::Height, bit.h).
//
// The end point is always freshly projected from field_64 the same way.
// The segment direction (end - start) feeds M3dMaths_SquareRoot0 for the
// length, then a perpendicular offset (scaled by each end's own half
// width) gives a left/right corner pair at both ends. mBitFlags & 3 selects
// which of the 4 resulting points lands in which quad slot (screen
// winding/orientation variant - all 4 combinations traced and reproduced
// below via the switch). mBitFlags & 0x20 (no in-repo setter found yet)
// skips recomputing the end corners and reuses whatever was last cached as
// the "chain start" corners (set only when 0x10 fires) instead.
//
// A single invZ (from the separate Algebra_Transform4/gGfxMatrix pass, see
// RefreshGfxMatrix above) over the CURRENT/end point only is shared by all
// 4 corners - unlike DisplayQuadBitList there is no per-corner invZ and no
// 1.03 fudge factor here. Colour is CFT4Bit::mCodeBGR's low 3 bytes
// (byte0->R, byte1->G, byte2->B, same ARGB pack DisplayQuadBitList uses for
// CQuadBit::mTint), alpha/blend mode from mCodeBGR's byte 3 bit 0x02
// (semi-transparent). UVs are the fixed 0/1 corner constants, like
// DisplayQuadBitList. A 40-byte scratch record is carved out of the pPoly
// queue per node (tag 0x9000000) and its texture-corner/UV bytes are built
// but never read back for the draw call - same confirmed dead/inert queue
// pattern the family notes describe for DisplayGLineList/DisplayGlassList's
// 28-byte record - but the pointer advance and PolyBufferEnd bounds check
// ARE kept here (unlike those two) because on overflow the original does a
// bare `return`, abandoning the rest of the list, which is an observable
// effect even though the record's contents are not.
void DisplayLinked2EndedBitListLeftover(void** a1)
{
	RefreshGfxMatrix();

	CLinked2EndedBit* pBit = reinterpret_cast<CLinked2EndedBit*>(*a1);

	bool prevFailed = false;
	i32 prevEndX = 0, prevEndY = 0;
	i32 prevEndLeftX = 0, prevEndLeftY = 0, prevEndRightX = 0, prevEndRightY = 0;
	i32 historyLeftX = 0, historyLeftY = 0, historyRightX = 0, historyRightY = 0;

	while (pBit)
	{
		u8* clip = G_VIEW_CLIP_INFO;
		u16 clipMin = *(u16*)(clip + 8);
		u16 clipMax = *(u16*)(clip + 0xA);

		bool freshStart = (pBit->mBitFlags & 0x10) || prevFailed;

		i32 startX, startY;
		i32 halfWidthStart = 0;

		if (freshStart)
		{
			VECTOR relPos;
			relPos.vx = (pBit->field_58.vx >> 12) - gCameraViewPos->vx;
			relPos.vy = (pBit->field_58.vy >> 12) - gCameraViewPos->vy;
			relPos.vz = (pBit->field_58.vz >> 12) - gCameraViewPos->vz;

			gte_ldlv0(&relPos);
			gte_rtps();

			i32 depth;
			gte_stlvnl2(&depth);
			if ((u32)depth < clipMin || (u32)depth > clipMax)
			{
				prevFailed = true;
				pBit = reinterpret_cast<CLinked2EndedBit*>(pBit->mNext);
				continue;
			}

			i32 sxy;
			gte_stsxy(&sxy);
			startX = (i16)sxy;
			startY = (i16)(sxy >> 16);

			u8 height = pBit->mpPSXFrame->Height;
			halfWidthStart = (i32)(((u32)height * (u16)pBit->mScale) / (u32)depth) >> 1;
			if (halfWidthStart < 2)
				halfWidthStart = 2;
		}
		else
		{
			startX = prevEndX;
			startY = prevEndY;
		}

		if ((u8*)G_PPOLY + 40 > G_POLY_BUFFER_END)
			return;
		G_PPOLY = (u32*)((u8*)G_PPOLY + 40);

		VECTOR relEnd;
		relEnd.vx = (pBit->field_64.vx >> 12) - gCameraViewPos->vx;
		relEnd.vy = (pBit->field_64.vy >> 12) - gCameraViewPos->vy;
		relEnd.vz = (pBit->field_64.vz >> 12) - gCameraViewPos->vz;

		gte_ldlv0(&relEnd);
		gte_rtps();

		i32 endDepth;
		gte_stlvnl2(&endDepth);
		if ((u32)endDepth < clipMin || (u32)endDepth > clipMax)
		{
			prevFailed = true;
			pBit = reinterpret_cast<CLinked2EndedBit*>(pBit->mNext);
			continue;
		}

		i32 endSxy;
		gte_stsxy(&endSxy);
		i32 endX = (i16)endSxy;
		i32 endY = (i16)(endSxy >> 16);

		u8 height = pBit->mpPSXFrame->Height;
		i32 halfWidthEnd = (i32)(((u32)height * (u16)pBit->mScale) / (u32)endDepth) >> 1;
		if (halfWidthEnd < 2)
			halfWidthEnd = 2;

		i32 dx = endX - startX;
		i32 dy = endY - startY;
		i32 len = M3dMaths_SquareRoot0(dx * dx + dy * dy);

		if (len == 0)
		{
			prevFailed = true;
			pBit = reinterpret_cast<CLinked2EndedBit*>(pBit->mNext);
			continue;
		}

		i32 startLeftX, startLeftY, startRightX, startRightY;
		if (freshStart)
		{
			i32 offY = (halfWidthStart * dy) / len;
			i32 offX = (halfWidthStart * dx) / len;
			startLeftX = startX + offY;
			startLeftY = startY - offX;
			startRightX = startX - offY;
			startRightY = startY + offX;
		}
		else
		{
			startLeftX = prevEndLeftX;
			startLeftY = prevEndLeftY;
			startRightX = prevEndRightX;
			startRightY = prevEndRightY;
		}

		if (pBit->mBitFlags & 0x10)
		{
			historyLeftX = startLeftX;
			historyLeftY = startLeftY;
			historyRightX = startRightX;
			historyRightY = startRightY;
		}

		i32 endLeftX, endLeftY, endRightX, endRightY;
		if (pBit->mBitFlags & 0x20)
		{
			endLeftX = historyLeftX;
			endLeftY = historyLeftY;
			endRightX = historyRightX;
			endRightY = historyRightY;
		}
		else
		{
			i32 offY = (halfWidthEnd * dy) / len;
			i32 offX = (halfWidthEnd * dx) / len;
			endLeftX = endX + offY;
			endLeftY = endY - offX;
			endRightX = endX - offY;
			endRightY = endY + offX;
		}

		prevEndX = endX;
		prevEndY = endY;
		prevEndLeftX = endLeftX;
		prevEndLeftY = endLeftY;
		prevEndRightX = endRightX;
		prevEndRightY = endRightY;
		prevFailed = false;

		i32 ax, ay, bx, by, cx, cy, dcx, dcy;
		switch (pBit->mBitFlags & 3)
		{
			case 2:
				ax = endRightX; ay = endRightY;
				bx = endLeftX;  by = endLeftY;
				cx = startRightX; cy = startRightY;
				dcx = startLeftX; dcy = startLeftY;
				break;
			case 3:
				ax = endLeftX;  ay = endLeftY;
				bx = endRightX; by = endRightY;
				cx = startLeftX; cy = startLeftY;
				dcx = startRightX; dcy = startRightY;
				break;
			case 1:
				ax = startRightX; ay = startRightY;
				bx = endRightX;   by = endRightY;
				cx = startLeftX;  cy = startLeftY;
				dcx = endLeftX;   dcy = endLeftY;
				break;
			default:
				ax = startLeftX; ay = startLeftY;
				bx = endLeftX;   by = endLeftY;
				cx = startRightX; cy = startRightY;
				dcx = endRightX;  dcy = endRightY;
				break;
		}

		f32 rawPos[3];
		rawPos[0] = (f32)pBit->field_64.vx / 4096.0f;
		rawPos[1] = (f32)pBit->field_64.vy / 4096.0f;
		rawPos[2] = (f32)pBit->field_64.vz / 4096.0f;

		f32 xf[4];
		Algebra_Transform4(xf, rawPos);

		f32 invZ;
		if (fabsf(xf[3]) > 0.00000001f)
			invZ = 1.0f / xf[3];
		else
			invZ = -1.0e12f;

		i32 blendMode = 0;
		u32 alpha = 0xFF;
		if ((pBit->mCodeBGR >> 24) & 2)
		{
			blendMode = 2;
			alpha = 0x80;
		}

		Texture* pTexture = pBit->mpPSXFrame->pTexture;
		PCGfx_UseTexture(pTexture->clut, (DCGfx_BlendingMode)blendMode);

		u32 colorR = pBit->mCodeBGR & 0xFF;
		u32 colorG = (pBit->mCodeBGR >> 8) & 0xFF;
		u32 colorB = (pBit->mCodeBGR >> 16) & 0xFF;
		u32 color = (alpha << 24) | (colorR << 16) | (colorG << 8) | colorB;

		f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
		f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;

		PCGfx_DrawQPoly3D(
				ax * scaleX, ay * scaleY, invZ, 0.0f, 0.0f, color,
				bx * scaleX, by * scaleY, invZ, 1.0f, 0.0f, color,
				cx * scaleX, cy * scaleY, invZ, 0.0f, 1.0f, color,
				dcx * scaleX, dcy * scaleY, invZ, 1.0f, 1.0f, color);

		pBit = reinterpret_cast<CLinked2EndedBit*>(pBit->mNext);
	}
}

// @Ok
// Functional (session-wide functional-only bar, 2026-08-31). Address
// 0x40f110, found by tracing Bit_Init's RegisterSlot calls (see
// DisplayTextBoxList). See the shared family notes above
// CSimpleTexturedRibbon::Display. No gte_ldlv0/gte_rtps here, unlike the
// rest of the family: builds a raw fixed-to-float vector (pos / 4096.0f,
// no >>12), runs it through Algebra_Transform4, and gets invZ = 1/w
// (fabs(w) > 1e-8 guard, else -1e12).
//
// The old note here said the screen x/y arguments passed to
// PCGfx_DrawQuad2D never visibly appeared in the Hex-Rays pseudocode.
// Traced in the raw disassembly this session: they are the x*invZ and
// y*invZ stack slots written by the vector3d-ctor call right before the
// depth check (the same call that produces the depth value itself) -
// Hex-Rays just failed to alias them back to named locals. Confirmed
// against PCGfx_DrawQuad2D's real 11-argument signature (PCGfx.h): x, y,
// width, height, 0, 0, 1, 1, color, z, bool. Width/height are
// (mWidthHeight & 0xF) scaled into pixels via gGameResolutionX/Y over
// Xres/Yres, same scaling idiom as DisplayQuadBitList and
// DisplayTextBoxList, but note it scales the SPRITE SIZE here, not the
// already-projected screen position. The z argument is invZ offset by a
// constant (~7.071, likely a fixed near-plane bias for 2D sprite sorting,
// not otherwise identified). Color is r0/g0/b0 direct with alpha 0xFF, or
// 0x80 plus blend mode 2 if CPixel::code has bit 2 set (semi-transparent).
void DisplayPixelList(void** a1)
{
	RefreshGfxMatrix();

	CPixel* pPixel = reinterpret_cast<CPixel*>(*a1);
	while (pPixel)
	{
		f32 rawPos[3];
		rawPos[0] = (f32)pPixel->mPos.vx / 4096.0f;
		rawPos[1] = (f32)pPixel->mPos.vy / 4096.0f;
		rawPos[2] = (f32)pPixel->mPos.vz / 4096.0f;

		f32 xf[4];
		Algebra_Transform4(xf, rawPos);

		f32 invZ;
		if (fabsf(xf[3]) > 0.00000001f)
			invZ = 1.0f / xf[3];
		else
			invZ = -1.0e12f;

		f32 screenX = xf[0] * invZ;
		f32 screenY = xf[1] * invZ;

		if (invZ > 0.0f)
		{
			i32 blendMode = 0;
			u32 alpha = 0xFF;
			if (pPixel->code & 2)
			{
				blendMode = 2;
				alpha = 0x80;
			}

			u32 color = (alpha << 24) | (pPixel->r0 << 16) | (pPixel->g0 << 8) | pPixel->b0;

			PCGfx_UseTexture(1, (DCGfx_BlendingMode)blendMode);

			f32 width = (f32)(pPixel->mWidthHeight & 0xF);
			f32 scaleX = G_GAME_RESOLUTION_X / (f32)G_XRES;
			f32 scaleY = G_GAME_RESOLUTION_Y / (f32)G_YRES;

			PCGfx_DrawQuad2D(
					screenX, screenY,
					scaleX * width, scaleY * width,
					0.0f, 0.0f, 1.0f, 1.0f,
					color, invZ - 7.0710726f, false);
		}

		pPixel = reinterpret_cast<CPixel*>(pPixel->mNext);
	}
}

// Tentative structs for PolyLineList's bit type and its inner vertex array
// (not in bit.h; no confirmed names). Traced from the raw disassembly of
// DisplayPolyLineList (0x411ef0) this session. field_3C is an unidentified
// 4-byte gap between CBit (confirmed size 0x3C, VALIDATE_SIZE below) and
// the fields this function actually reads (0x40/0x44/0x48) - not guessed.
struct SPolyLineVert
{
	CVector mPos;
	u32 mColorFlags;
};

struct SPolyLineBit : public CBit
{
	u32 field_3C;
	i32 mNumVerts;          // 0x40
	SPolyLineVert* mVerts;  // 0x44
	CVector mStartPos;      // 0x48
};

// @Ok
// Functional (session-wide functional-only bar, 2026-08-31). Address
// 0x411ef0. Fully traced against the raw disassembly, which corrects two
// things the shared family notes and this function's own old note guessed
// wrong:
//
// 1) sub_505B90 ("syRtcInit" in names.json) is a REAL, literal `xor eax,eax
//    ; retn` no-op (confirmed via raw disasm at the actual address, no
//    hidden logic) - it really is a stubbed init call, exactly what its
//    name says, not a mislabeled clip helper. Its return value (always 0)
//    makes the `if (result >= 0)` that gates each segment's draw always
//    true, so this function performs NO clip test of any kind - every
//    segment always draws. This is a real, reproducible property of the
//    shipped game for this bit type, not something to "fix".
// 2) The pPoly-queue record it builds (tag 0x3000000, 24 bytes) genuinely
//    IS fully dead/inert here: every field written into it (the
//    dword_56E9D0/56E9D4 pair, and two globals doing double duty as a
//    would-be "point 1 screen position" - dword_614CEC/614CF0, which
//    other, unrelated, not-yet-decompiled functions also write - see
//    xrefs) gets read back for color only (record+12..14, i.e. this
//    segment's own SPolyLineVert::mColorFlags low 3 bytes); the position
//    data is never read from the record at all. This function's actual
//    on-screen positions come entirely from a second, separate
//    Algebra_Transform4/gGfxMatrix pass per point (same idiom
//    DisplayPixelList uses: screenX/Y = xf[0..1]*invZ, no gte_ldlv0/rtps
//    GTE path at all, and no gGameResolutionX/Y scaling - the values are
//    already in pixel space).
//
// Per bit: mStartPos is the strip's first point; mVerts[0..mNumVerts-1]
// are the following points, each also carrying its own colour/blend-flag
// dword (SPolyLineVert::mColorFlags, bit 0x2000000 selects a semi
// -transparent blend the same way the rest of this family's mCodeBGR-style
// flags do). One PCGfx_DrawLine segment (already-@Ok, PCGfx.cpp) is drawn
// per consecutive point pair, both endpoints sharing THIS segment's own
// colour (the vertex being walked TO, not a per-endpoint blend), flat
// width 1.0f; the walk then advances (this point becomes the next
// segment's start), matching the "shifted pointer over CBit-like sub
// -entries" description in the old note.
void DisplayPolyLineList(void** a1)
{
	RefreshGfxMatrix();

	SPolyLineBit* pBit = reinterpret_cast<SPolyLineBit*>(*a1);
	while (pBit)
	{
		CVector pt1 = pBit->mStartPos;

		for (i32 i = 0; i < pBit->mNumVerts; i++)
		{
			SPolyLineVert& vert = pBit->mVerts[i];
			CVector pt2 = vert.mPos;

			if ((u8*)G_PPOLY + 24 > G_POLY_BUFFER_END)
				return;
			G_PPOLY = (u32*)((u8*)G_PPOLY + 24);

			i32 blendMode = 0;
			u32 alpha = 0xFF;
			if (vert.mColorFlags & 0x2000000)
			{
				blendMode = 2;
				alpha = 0x80;
			}

			PCGfx_UseTexture(1, (DCGfx_BlendingMode)blendMode);

			u32 color = (alpha << 24) | ((vert.mColorFlags & 0xFF) << 16) |
					(((vert.mColorFlags >> 8) & 0xFF) << 8) | ((vert.mColorFlags >> 16) & 0xFF);

			f32 rawPos1[3];
			rawPos1[0] = (f32)pt1.vx / 4096.0f;
			rawPos1[1] = (f32)pt1.vy / 4096.0f;
			rawPos1[2] = (f32)pt1.vz / 4096.0f;

			f32 xf1[4];
			Algebra_Transform4(xf1, rawPos1);

			f32 invZ1;
			if (fabsf(xf1[3]) > 0.00000001f)
				invZ1 = 1.0f / xf1[3];
			else
				invZ1 = -1.0e12f;

			f32 screen1X = xf1[0] * invZ1;
			f32 screen1Y = xf1[1] * invZ1;

			f32 rawPos2[3];
			rawPos2[0] = (f32)pt2.vx / 4096.0f;
			rawPos2[1] = (f32)pt2.vy / 4096.0f;
			rawPos2[2] = (f32)pt2.vz / 4096.0f;

			f32 xf2[4];
			Algebra_Transform4(xf2, rawPos2);

			f32 invZ2;
			if (fabsf(xf2[3]) > 0.00000001f)
				invZ2 = 1.0f / xf2[3];
			else
				invZ2 = -1.0e12f;

			f32 screen2X = xf2[0] * invZ2;
			f32 screen2Y = xf2[1] * invZ2;

			if (invZ1 > 0.0f && invZ2 > 0.0f)
			{
				PCGfx_DrawLine(
						screen1X, screen1Y, invZ1, color,
						screen2X, screen2Y, invZ2, color,
						1.0f);
			}

			pt1 = pt2;
		}

		pBit = reinterpret_cast<SPolyLineBit*>(pBit->mNext);
	}
}

// Tentative struct for GPolyLineList's bit type (not in bit.h; no
// confirmed name). Reuses SPolyLineVert (above) for the inner vertex
// array - same 16-byte CVector+u32 entry shape - but this bit's own
// layout differs from SPolyLineBit's: no 0x3C gap (mNumVerts sits right
// after CBit), and it carries its own starting colour/flags dword
// (mStartColorFlags) alongside the starting position.
struct SGPolyLineBit : public CBit
{
	i32 mNumVerts;              // 0x3C
	SPolyLineVert* mVerts;      // 0x40
	CVector mStartPos;          // 0x44
	u32 mStartColorFlags;       // 0x50
};

// @Ok
// Functional (session-wide functional-only bar, 2026-08-31). Address
// 0x4125c0. Old note's guess was right: this is the gouraud (per-vertex
// colour) sibling of DisplayPolyLineList (0x411ef0, see its comment for
// the sub_505B90-is-a-real-no-op and dead-record findings, both confirmed
// to hold here too - same 0x4000000-tag record shape, just 4 bytes longer
// to carry a second colour dword). The two functions are otherwise
// structurally identical: no clip test (sub_505B90 always returns 0), the
// same Algebra_Transform4/gGfxMatrix pass per point (screenX/Y =
// xf[0..1]*invZ, no GTE, no gGameResolutionX/Y scaling), one PCGfx_DrawLine
// (already-@Ok, PCGfx.cpp) per consecutive point pair, and a walk that
// carries this iteration's point/colour forward as the next iteration's
// start.
//
// The difference: each SPolyLineVert entry supplies its OWN colour, so
// PCGfx_DrawLine's two colour args differ per call - point1 uses the
// colour carried in from the previous point (or SGPolyLineBit::
// mStartColorFlags for the first segment), point2 uses this entry's own
// mColorFlags. The blend mode/alpha (mColorFlags bit 0x2000000) is decided
// ONLY by point1's flags for both colour args, mirroring the exact
// asymmetry traced in the disassembly (point2's own flag bit is never
// tested, only its RGB bytes are used).
void DisplayGPolyLineList(void** a1)
{
	RefreshGfxMatrix();

	SGPolyLineBit* pBit = reinterpret_cast<SGPolyLineBit*>(*a1);
	while (pBit)
	{
		CVector pt1 = pBit->mStartPos;
		u32 flags1 = pBit->mStartColorFlags;

		for (i32 i = 0; i < pBit->mNumVerts; i++)
		{
			SPolyLineVert& vert = pBit->mVerts[i];
			CVector pt2 = vert.mPos;
			u32 flags2 = vert.mColorFlags;

			if ((u8*)G_PPOLY + 28 > G_POLY_BUFFER_END)
				return;
			G_PPOLY = (u32*)((u8*)G_PPOLY + 28);

			i32 blendMode = 0;
			u32 alpha = 0xFF;
			if (flags1 & 0x2000000)
			{
				blendMode = 2;
				alpha = 0x80;
			}

			PCGfx_UseTexture(1, (DCGfx_BlendingMode)blendMode);

			u32 color1 = (alpha << 24) | ((flags1 & 0xFF) << 16) |
					(((flags1 >> 8) & 0xFF) << 8) | ((flags1 >> 16) & 0xFF);
			u32 color2 = (alpha << 24) | ((flags2 & 0xFF) << 16) |
					(((flags2 >> 8) & 0xFF) << 8) | ((flags2 >> 16) & 0xFF);

			f32 rawPos1[4];
			rawPos1[0] = (f32)pt1.vx / 4096.0f;
			rawPos1[1] = (f32)pt1.vy / 4096.0f;
			rawPos1[2] = (f32)pt1.vz / 4096.0f;
			rawPos1[3] = 1.0f;

			f32 xf1[4];
			Algebra_Transform4(xf1, rawPos1);

			f32 invZ1;
			if (fabsf(xf1[3]) > 0.00000001f)
				invZ1 = 1.0f / xf1[3];
			else
				invZ1 = -1.0e12f;

			f32 screen1X = xf1[0] * invZ1;
			f32 screen1Y = xf1[1] * invZ1;

			f32 rawPos2[4];
			rawPos2[0] = (f32)pt2.vx / 4096.0f;
			rawPos2[1] = (f32)pt2.vy / 4096.0f;
			rawPos2[2] = (f32)pt2.vz / 4096.0f;
			rawPos2[3] = 1.0f;

			f32 xf2[4];
			Algebra_Transform4(xf2, rawPos2);

			f32 invZ2;
			if (fabsf(xf2[3]) > 0.00000001f)
				invZ2 = 1.0f / xf2[3];
			else
				invZ2 = -1.0e12f;

			f32 screen2X = xf2[0] * invZ2;
			f32 screen2Y = xf2[1] * invZ2;

			if (invZ1 > 0.0f && invZ2 > 0.0f)
			{
				PCGfx_DrawLine(
						screen1X, screen1Y, invZ1, color1,
						screen2X, screen2Y, invZ2, color2,
						1.0f);
			}

			pt1 = pt2;
			flags1 = flags2;
		}

		pBit = reinterpret_cast<SGPolyLineBit*>(pBit->mNext);
	}
}

// @Ok
void Bit_Init(void)
{
	G_BITCOUNT = 0;
	G_NONRENDEREDBIT_LIST = 0;
	G_TEXTBOX_LIST = 0;
	G_FLATBIT_LIST = 0;
	G_LINKED2ENDEDBIT_LIST_LEFTOVER = 0;
	G_PIXEL_LIST = 0;
	G_POLYLINE_LIST = 0;
	G_GPOLYLINE_LIST = 0;
	G_QUADBIT_LIST = 0;
	G_GENPOLY_LIST = 0;
	G_CHUNKBIT_LIST = 0;
	G_GLOW_LIST = 0;
	G_GLASS_LIST = 0;
	G_GLINE_LIST = 0;
	G_SPECIALDISPLAY_LIST = 0;

	if (!G_BITSERVER)
	{
		G_BITSERVER = new CBitServer();

		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_TEXTBOX_LIST), DisplayTextBoxList);
		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_FLATBIT_LIST), DisplayFlatBitList);
		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_LINKED2ENDEDBIT_LIST_LEFTOVER), DisplayLinked2EndedBitListLeftover);
		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_PIXEL_LIST), DisplayPixelList);
		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_POLYLINE_LIST), DisplayPolyLineList);
		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_GPOLYLINE_LIST), DisplayGPolyLineList);

		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_QUADBIT_LIST), DisplayQuadBitList);
		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_CHUNKBIT_LIST), DisplayChunkBitList);
		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_GLOW_LIST), DisplayGlowList);

		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_GLASS_LIST), DisplayGlassList);
		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_GLINE_LIST), DisplayGLineList);
		G_BITSERVER->RegisterSlot(reinterpret_cast<void**>(&G_SPECIALDISPLAY_LIST), DisplaySpecialDisplayList);
	}

	setDrawTPage();
	memset(G_ANIM_TABLE, 0, NUM_ANIM_ENTRIES * sizeof(SAnimFrame*));
}

// @Ok
void Bit_SetSparkSize(u32 size)
{
	DoAssert(size < 0x10, "Daft spark size");

	G_SPARK_SIZE = size | (size << 16);
}

// @Ok
// @Matching
CWibbly::CWibbly(
		u8 a2,
		u8 a3,
		u8 a4,
		i32 a5,
		i32 a6,
		i32 a7,
		i32 a8,
		i32 a9,
		i32 a10,
		i32 a11,
		i32 a12,
		i32 a13,
		i32 a14)
	: CGouraudRibbon(a6 + 1, 0)
{
	this->field_7C = a9;
	this->field_88 = a10;
	this->field_80 = a11;

	this->field_8C = a13;
	this->field_84 = a12;
	this->field_90 = a14;
	this->field_94 = Rnd(4096);

	this->SetRGB(a2, a3, a4);

	this->mpPoints[this->mNumPoints - 1].r = a2 >> 1;
	this->mpPoints->r = this->mpPoints[this->mNumPoints - 1].r;

	this->mpPoints[this->mNumPoints - 1].g = a3 >> 1;
	this->mpPoints->g = this->mpPoints[this->mNumPoints - 1].g;

	this->mpPoints[this->mNumPoints - 1].b = a4 >> 1;
	this->mpPoints->b = this->mpPoints[this->mNumPoints - 1].b;

	this->SetWidth(a8 / 20);

	this->mpPoints[this->mNumPoints - 1].Width = a8 / 40;
	this->mpPoints->Width = this->mpPoints[this->mNumPoints - 1].Width;

	i32 v15 = 2 * a2;
	if (v15 > 255)
	{
		v15 = 0xFF;
	}

	i32 v21 = 2 * a3;
	if (v21 > 255)
	{
		v21 = 0xFF;
	}

	i32 v20 = 2 * a4;
	if (v20 > 255)
	{
		v20 = 0xFF;
	}

	this->SetCore(v15, v21, v20, a8 / 80);
}

// TTime, per the maintainer's IDB (idb_globals.txt: 0x0060CFA8 TTime). A
// global game clock/counter, read directly from game memory. Nothing else
// in this repo touches it yet (only read, never written, here), so it
// stays file-local for now.
#ifndef SPIDEY_STANDALONE
volatile i32 TTime = 0;
#else
extern volatile i32 TTime;
#endif
//#define G_TTIME (TTime)
#define G_TTIME (*reinterpret_cast<volatile i32*>(0x0060CFA8))

// @Ok
// Functional (session-wide functional-only bar, 2026-08-30). Address
// 0x4105f0 (found via CWibbly's own vtable, off_53B3E4+4; the 0x29a=666
// byte body matches prototypes.json's Mac size for CWibbly::Move, 660
// bytes). Rebuilds the wobble points between the two fixed ends of the
// ribbon: point 0 is pinned to field_4C (the start), the last point is
// pinned to field_4C + (mNumPoints-1)*field_58 (the end, using the
// CVector(int)*CVector "only reads lhs.vx" idiom already documented in
// vector.h, since field_58 is the (end-start)/(numPoints-1) step set by
// SetEndPoints). Every interior point i gets two sine-table "wave" offsets
// summed: field_70 modulated by (field_8C, field_90) and field_64
// modulated by (field_80, field_84), both driven by the global clock
// TTime and the shared random phase field_94 (rerolled about 1 time in 15
// per point). The original first stores prevPoint + ((field_58>>6) *
// Sine(angle))/64 into mpPoints[i], then immediately overwrites the same
// slot with the wave sum before that first value is ever read anywhere;
// that first store is dead (CLAUDE.md: reproduce source-level oddities,
// but a provably unread store has no functional effect) and is not
// reproduced here. Finally, if field_48 (the inner "core" ribbon set up by
// SetCore) exists, every point's position (not its color/width) is copied
// into field_48's own point array, so the core ribbon follows the same
// wobble path as the outer one.
void CWibbly::Move(void)
{
	i32 numPoints = this->mNumPoints;

	this->mpPoints[0].Pos = this->field_4C;
	this->mpPoints[numPoints - 1].Pos = this->field_4C + CVector(numPoints - 1) * this->field_58;

	for (i32 i = 1; i < numPoints - 1; i++)
	{
		if (Rnd(15) == 0)
			this->field_94 = Rnd(4096);

		i32 angleA = i * this->field_8C + this->field_94 + 2 * this->field_90 * G_TTIME;
		i32 angleB = i * this->field_80 + this->field_94 + 2 * this->field_84 * G_TTIME;

		CVector waveA = (this->field_70 * Sine(angleA)) / 4096;
		CVector waveB = (this->field_64 * Sine(angleB)) / 4096;

		this->mpPoints[i].Pos = waveA + waveB;
	}

	if (this->field_48)
	{
		for (i32 i = 0; i < numPoints; i++)
		{
			this->field_48->mpPoints[i].Pos = this->mpPoints[i].Pos;
		}
	}
}

// @Ok
INLINE void CWibbly::SetCore(
		u8 a2,
		u8 a3,
		u8 a4,
		i32 a5)
{
	delete this->field_48;

	this->field_48 = new CGouraudRibbon(this->mNumPoints, 0);

	this->field_48->mProtected = 1;
	this->field_48->SetRGB(a2, a3, a4);
	this->field_48->SetWidth(a5);
}

// @Ok
void CWibbly::SetEndPoints(
		CVector* a2,
		CVector* a3)
{
	this->field_4C = *a2;

	this->field_58 = (*a3 - *a2) / (this->mNumPoints - 1);

	CSVector v12;
	v12.vx = 0;
	v12.vy = 0;
	v12.vz = 0;

	Utils_CalcAim(&v12, a2, a3);
	v12.vy = (v12.vy + 1024) & 0xFFF;

	Utils_GetVecFromMagDir(&this->field_64, this->field_7C, &v12);
	v12.vy = (v12.vy - 1024) & 0xFFF;
	v12.vx = (v12.vx + 1024) & 0xFFF;

	Utils_GetVecFromMagDir(&this->field_70, this->field_88, &v12);
}

// @Ok
CWibbly::~CWibbly(void)
{
	delete this->field_48;
}

// @Ok
CFireyExplosion::CFireyExplosion(CVector* pPos)
{
	this->mPos = *pPos;
	this->mLifetime = 50;
	SFX_PlayPos(1, &this->mPos, 0);
}

// @Ok
CFireyExplosion::~CFireyExplosion(void)
{
}

// @Ok
// @Matching
void CFireyExplosion::Move(void)
{
	if (this->mAge == 5)
	{
		for (i32 i = 0; i < 1; ++i )
		{
			Rnd(100);
		}

	}
	if (++this->mAge > this->mLifetime)
	{
		this->Die();
	}
}

// @Ok
CCombatImpactRing::~CCombatImpactRing(void)
{
}

// @Ok
CTextBox::CTextBox(
		i32 a2,
		i32 a3,
		i32 a4,
		i32 a5,
		u32 a6,
		CFriction* pFric)
{
	this->AttachTo(reinterpret_cast<CBit**>(&G_TEXTBOX_LIST));

	this->mPos.vx = a2;
	this->mPos.vy = a3;

	this->mVel.vx = a4;
	this->mVel.vy = a5;

	this->mFric.vx = pFric->vx;
	this->mFric.vy = pFric->vy;
	this->mFric.vz = pFric->vz;

	this->mLifetime = a6;

	this->field_3C = G_TIMER_RELATED;

}

// @Ok
CTextBox::~CTextBox(void)
{
	this->DeleteFrom(reinterpret_cast<CBit**>(&G_TEXTBOX_LIST));
}

// @Ok
// @Note: Constructor got different inline and some oeprations are at different order, but don't care
// @Test
CChunkBit::CChunkBit(
		CSVector *a,
		CSVector *b,
		CSVector *c)
{
	this->mType = 6;

	this->mPosA.vx = a->vx;
	this->mPosA.vy = a->vy;
	this->mPosA.vz = a->vz;

	this->mPosB.vx = b->vx;
	this->mPosB.vy = b->vy;
	this->mPosB.vz = b->vz;

	this->mPosC.vx = c->vx;
	this->mPosC.vy = c->vy;
	this->mPosC.vz = c->vz;


	i32 v8 = b->vz - a->vz;
	i32 v10 = c->vz - a->vz;

	i32 v11 = b->vy - a->vy;
	i32 v13 = c->vy - a->vy;

	i32 v14 = b->vx - a->vx;
	i32 v15 = c->vx - a->vx;

	CVector v18;
	v18.vx = v13 * v8 - v10 * v11;
	v18.vy = v10 * v14 - v15 * v8;
	v18.vz = v15 * v11 - v13 * v14;

	v18 >>= 12;
	VectorNormal(
			reinterpret_cast<VECTOR*>(&v18),
			reinterpret_cast<VECTOR*>(&v18));
	
	i32 v16 = Rnd(96);
	this->mPosD.vx = (v18.vx * (v16 + 128)) >> 12;

	this->mPosD.vy = (v18.vy * (v16 + 128)) >> 12;
	this->mPosD.vz = (v18.vz * (v16 + 128)) >> 12;

	this->AttachTo(&G_CHUNKBIT_LIST);
}

// @Ok
CChunkBit::~CChunkBit(void)
{
	this->DeleteFrom(reinterpret_cast<CBit**>(&G_CHUNKBIT_LIST));
}

// Stores the plain r|g<<8|b<<16 pack in mColorA, then three independently
// Rnd(4096)-dithered variants of the same base color in mColorB/C/D (the
// per-corner colors DisplayChunkBitList applies to mWorldPosA/B/C/D).
// @Ok
void CChunkBit::SetRGB(u8 r, u8 g, u8 b)
{
	this->mColorA = r | (g << 8) | (b << 16);

	i32 rnd1 = Rnd(4096);
	this->mColorB = ((rnd1 * r) >> 12) | (16 * ((rnd1 * b) & 0xFFFFF000)) | (((rnd1 * g) >> 4) & 0xFFFFFF00);

	i32 rnd2 = Rnd(4096);
	this->mColorC = ((rnd2 * r) >> 12) | (16 * ((rnd2 * b) & 0xFFFFF000)) | (((rnd2 * g) >> 4) & 0xFFFFFF00);

	i32 rnd3 = Rnd(4096);
	this->mColorD = ((rnd3 * r) >> 12) | (16 * ((rnd3 * b) & 0xFFFFF000)) | (((rnd3 * g) >> 4) & 0xFFFFFF00);
}

// Stores the texture/clut id (zero-extended u16) in mClut, then scales the
// six uchar UV params by the texture's inverse size into the three mUV0/1/2
// pairs. The second u16 param is a confirmed-dead parameter in the original
// disasm (see shatter.h's G_SHATTER_UV_UNUSED comment).
// @Ok
void CChunkBit::SetUVs(u16 texId, u16 unused, u8 u0, u8 v0, u8 u1, u8 v1, u8 u2, u8 v2)
{
	this->mClut = texId;

	f32 invW;
	f32 invH;
	PCTex_GetInvTextureSize(texId, &invW, &invH);

	this->mUV0[0] = (f32)u0 * invW;
	this->mUV0[1] = (f32)v0 * invH;
	this->mUV1[0] = (f32)u1 * invW;
	this->mUV1[1] = (f32)v1 * invH;
	this->mUV2[0] = (f32)u2 * invW;
	this->mUV2[1] = (f32)v2 * invH;
}

// Rotates the four corner positions (mPosA-D) by mAngles and translates them
// by the CBit base mPos, storing the world-space results in mWorldPosA-D.
// @Ok
static void CChunkBit_CalculateWorldCoords(CChunkBit *self)
{
	MATRIX matrix;
	M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR *>(&self->mAngles), &matrix);
	gte_SetRotMatrix(&matrix);
	M3dAsm_SetTransVector(reinterpret_cast<VECTOR *>(&self->mPos));

	gte_ldv0(&self->mPosA);
	gte_rtv0tr();
	gte_stlvnl(reinterpret_cast<VECTOR *>(&self->mWorldPosA));

	gte_ldv0(&self->mPosB);
	gte_rtv0tr();
	gte_stlvnl(reinterpret_cast<VECTOR *>(&self->mWorldPosB));

	gte_ldv0(&self->mPosC);
	gte_rtv0tr();
	gte_stlvnl(reinterpret_cast<VECTOR *>(&self->mWorldPosC));

	gte_ldv0(&self->mPosD);
	gte_rtv0tr();
	gte_stlvnl(reinterpret_cast<VECTOR *>(&self->mWorldPosD));
}

// @Ok
// 2026-08-31: session bar is functional decomp, not byte match (see task instructions). See the
// CShatterBit class comment in bit.h for the full evidence trail from 0x48BDC0's disasm.
// Forwards its first 3 args straight to CChunkBit's own (already @Ok) 3-CSVector-pointer
// constructor via a base-init list; const_cast is safe here because CChunkBit::CChunkBit only
// ever reads through those pointers. The vtable-set-then-overwrite codegen the original shows
// (base ctor sets CChunkBit's vtable, then this ctor's own body sets CShatterBit's) falls out
// automatically from normal C++ derived-class construction, no manual fixup needed.
// The 5th parameter is genuinely unused in the original: checked every instruction in 0x48BDC0,
// none of them read it, despite it being part of the Mac-confirmed 5-arg signature. Every known
// call site (Split, in shatter.cpp) passes literal 0. Kept for signature fidelity, intentionally
// unused, matching the original's own dead parameter.
CShatterBit::CShatterBit(
		CSVector const& deltaA,
		CSVector const& deltaB,
		CSVector const& deltaC,
		CVector const& center,
		i32 unused)
	: CChunkBit(const_cast<CSVector*>(&deltaA), const_cast<CSVector*>(&deltaB), const_cast<CSVector*>(&deltaC))
{
	(void)unused;

	// Coin flip: one of vx/vy gets a random 80..239 spin rate, the other (and vz, always) stay 0.
	this->mSpinRate.vx = 0;
	this->mSpinRate.vy = 0;
	this->mSpinRate.vz = 0;

	if (Rnd(2) != 0)
	{
		this->mSpinRate.vx = (i16)(Rnd(160) + 80);
	}
	else
	{
		this->mSpinRate.vy = (i16)(Rnd(160) + 80);
	}

	this->mLifetime = (u16)(Rnd(30) + 45);

	this->SetPos(center);
	CChunkBit_CalculateWorldCoords(this);
}

// @Ok
// 2026-08-31: functional decomp (session bar). 0x48BEC0 disasm: sets its own vtable (automatic
// here from normal C++ destructor semantics), then if mTrailRibbon is non-null, calls its
// vtable slot 0 with arg 1 (MSVC's "scalar deleting destructor" convention, i.e. plain
// `delete`), then chains to CChunkBit's own destructor (sub_40B7E0, automatic here too). No
// call site in this session's tracing ever actually sets mTrailRibbon away from its
// zero-initialized default (see the field's comment in bit.h), so this path is unverified at
// runtime, only by hand-tracing the disasm.
CShatterBit::~CShatterBit(void)
{
	if (this->mTrailRibbon != 0)
		delete this->mTrailRibbon;
}

// @Ok
// 2026-08-31: functional decomp (session bar). Derived from 0x48BF20 disasm + Hex-Rays
// decompile. sub_408950 resolves in tools/names.json to CBit::SetPos(const CVector&) (base
// class call, already @Ok); sub_408930 to CBit::Die(); the two CVector operator calls
// (0x4E77D0, 0x4E7800) resolve to the already-declared operator*(CVector const&,CVector const&)
// and operator/(CVector const&,int const&) overloads in vector.h. The (CVector,CVector)
// overload's documented "only reads lhs.vx" quirk (see vector.h's comment on the explicit
// CVector(i32) ctor) is exactly how Rnd(48) becomes a per-axis uniform scale here: a
// CVector(randMag) built via that ctor has vx=randMag with vy/vz don't-care, multiplied against
// delta, reading only vx from the left side.
void CShatterBit::SetPos(const CVector& pos)
{
	this->CBit::SetPos(pos);

	CVector delta = pos - G_SHATTER_FACE_CENTER;
	i32 dist = delta.Length();

	if (dist != 0)
	{
		i32 randMag = Rnd(48);
		this->mVel = (CVector(randMag) * delta) / dist;
	}
	else
	{
		this->Die();
	}

	i32 randScale = Rnd(45);
	this->mVel.vx += randScale * G_SHATTER_VELOCITY_SCALE[0];
	this->mVel.vy += randScale * G_SHATTER_VELOCITY_SCALE[1];
	this->mVel.vz += randScale * G_SHATTER_VELOCITY_SCALE[2];

	this->mVel.vy -= Rnd(175) << 12;

	if (Rnd(20) == 0)
	{
		this->mVel >>= 1;
	}

	if (this->mVel.vy > 0)
		this->mVel.vy = 0;

	if (this->mTrailRibbon != 0)
		this->mTrailRibbon->SetPos(this->mPos);
}

// @Ok
// 2026-08-31: functional decomp (session bar). Derived from 0x48C060 disasm + Hex-Rays
// decompile. sub_4E7590 resolves to CVector::operator+= (mPos += mVel); sub_4E7900, called with
// an implicit `this` of &mAngles and its one visible stack arg &mSpinRate, is
// CSVector::operator+= (mAngles += mSpinRate); sub_410EB0 resolves in tools/names.json to
// CRibbon::SetPos(CVector&), independently confirming mTrailRibbon's type (see bit.h); sub_40BA20
// is CChunkBit::CalculateWorldCoords, forwarded via CChunkBit_CalculateWorldCoords above;
// sub_408930 resolves to CBit::Die().
void CShatterBit::Move(void)
{
	this->mPos += this->mVel;
	this->mVel.vy += 20500;

	this->mAngles += this->mSpinRate;

	if (this->mTrailRibbon != 0)
		this->mTrailRibbon->SetPos(this->mPos);

	CChunkBit_CalculateWorldCoords(this);

	if (this->mLifetime == 0)
	{
		this->Die();
		return;
	}

	this->mLifetime--;
}


// @Ok
CBitServer::CBitServer(void)
{
	this->mNumEntries = 0;
}

// @Ok
CBitServer::~CBitServer(void)
{
}

// @Ok
// so close but that while loop is hard to match, prob because they didn't create a type
INLINE u32 CBitServer::RegisterSlot(void** bitList, void (*drawFunc)(void**))
{
	u32 usedSlot = this->mNumEntries;
	if (usedSlot < 0x20)
	{
		this->mEntry[usedSlot].field_0 = bitList;
		this->mEntry[usedSlot].field_4 = drawFunc;

		usedSlot = this->mNumEntries;
		this->mNumEntries = usedSlot + 1;


		u32 curSlot = this->mNumEntries;
		while (curSlot < 0x20)
		{
			if (this->mEntry[curSlot++].field_0 == 0)
				return usedSlot;

			this->mNumEntries = curSlot;
		}

		this->mNumEntries = 666;
	}

	return this->mNumEntries;
}

// @Ok
void CBitServer::DisplayRegisteredSlots(void)
{
	for (i32 i = 0; i < 0x20; i++)
	{
		if (this->mEntry[i].field_0)
			this->mEntry[i].field_4(this->mEntry[i].field_0);
	}
}

// @Ok
CPixel::~CPixel(void)
{
	this->DeleteFrom(reinterpret_cast<CBit**>(&G_PIXEL_LIST));
}

// @Ok
INLINE CPixel::CPixel(void)
{
	this->AttachTo(reinterpret_cast<CBit**>(&G_PIXEL_LIST));
}

// @Ok
// @Matching
CFrag::~CFrag(void)
{
}

// @Ok
// @Matching
void CFrag::Move(void)
{

	this->mPos.vx += this->mVel.vx;
	this->mPos.vy += this->mVel.vy;
	this->mPos.vz += this->mVel.vz;

	this->mVel.vx += this->mAcc.vx;
	this->mVel.vy += this->mAcc.vy;
	this->mVel.vz += this->mAcc.vz;

	this->mVel.vx -= this->mVel.vx >> this->mFric.vx;
	this->mVel.vy -= this->mVel.vy >> this->mFric.vy;
	this->mVel.vz -= this->mVel.vz >> this->mFric.vz;


	if (++this->mFrame >= this->mNumFrames)
	{
		this->mFrame = this->mNumFrames - 1;
	}

	if (++this->mAge >= this->mLifetime)
	{
		this->Die();
	}
}

// @Ok
// @AlmostMatching: inlines different and also the mVel assignement
CFrag::CFrag(
		CVector *a2,
		u8 a3,
		u8 a4,
		u8 a5,
		i32 a6,
		u16 a7,
		i32 a8,
		i32 a9,
		i32 a10,
		i32 a11)
{
	this->mPos = *a2;

	this->SetAnim(a6);

	if (a8)
	{
		this->SetSemiTransparent();
	}

	this->SetTint(a3, a4, a5);
	this->mScale = a7;

	this->mVel.vx = (Rnd(2 * a9 + 1) - a9) << 12;
	this->mVel.vy = -4096 * Rnd(a9);
	this->mVel.vz = (Rnd(2 * a9 + 1) - a9) << 12;
	this->mAcc.vy = a10;
	this->mFric.Set(3,3,3);

	this->mLifetime = Rnd(a11);
}

// @Ok
// @Matching
void CGlow::SetFringeWidth(u32 Fringe, u32 Width)
{
	DoAssert(Fringe < this->mNumFringes, "Bad Fringe sent to SetFringeWidth");

	SFringeQuad* pFringe = &this->mpFringes[Fringe * this->mNumSections];
	for (u32 i = 0; i < this->mNumSections; i++)
	{
		pFringe[i].Width = Width;
	}
}

// @Ok
// @CloseMatching - bruh lea vs add
void CGlow::SetFringeRGB(
		u32 Fringe,
		u8 r,
		u8 g,
		u8 b)
{
	print_if_false(Fringe < this->mNumFringes, "Bad Fringe sent to SetFringeRGB");

	u32 val = (((((0x3A << 8) | b) << 8) | g ) << 8) | r;

	SFringeQuad* pFringe = &this->mpFringes[Fringe * this->mNumFringes];
	for (u32 i = 0; i < this->mNumSections; i++)
	{
		pFringe[i].CodeBGR = val;
	}
}

// @Ok
// @Matching
void CCombatImpactRing::Move(void)
{
	if (this->mScale == this->field_68)
	{
		this->Die();
	}
	else
	{
		i32 v1 = G_TIMER_RELATED - this->field_70;
		this->field_70 = G_TIMER_RELATED;
		this->mScale += this->field_6C * v1;

		if (this->mScale > this->field_68)
			this->mScale = this->field_68;
	}
}

// @Ok
// @AlmostMatching: diff vector assingment and mFrame is assinged later for some reason
// also mScale is out of order
CCombatImpactRing::CCombatImpactRing(
		CVector *a2,
		u8 a3,
		u8 a4,
		u8 a5,
		i32 a6,
		i32 a7,
		i32 a8)
{
	this->mPos = *a2;

	this->mAngle = Rnd(4096);
	this->mScale = a6;

	this->field_68 = a7;
	this->field_6C = a8;
	this->mPostScale = 0xA001000;
	this->field_70 = G_TIMER_RELATED;

	this->SetTint(a3, a4, a5);

	this->mFrigDeltaZ = 64;

	this->SetAnim(8);
	this->SetSemiTransparent();
}

// @Ok
// @Matching
void CSimpleAnim::Move(void)
{
	this->IncFrame();

	if (this->mDie)
	{
		if (this->mFrame >= this->mDieFrame)
		{
			this->Die();
		}
	}
	else
	{
		if (this->mFrame >= this->mNumFrames)
		{
			if (this->mDieFrame == -2)
			{
				this->SetFrame(this->mNumFrames - 1);
			}
			else
			{
				this->SetFrame(0);
			}
		}
	}
}

// @Ok
// @Note: mine setups SEH frame (maybe because it's exported)
// also last if assingment is diff
CSimpleAnim::CSimpleAnim(
		CVector *a2,
		i32 a3,
		u16 a4,
		i32 a5,
		i32 a6,
		i32 a7)
{
	this->mPos = *a2;
	this->SetAnim(a3);
	this->mScale = a4;

	if (a5)
		this->SetSemiTransparent();


	this->mDie = a6;
	if (a7 == -1)
	{
		this->mDieFrame = this->mNumFrames - 1;
	}
	else
	{
		this->mDieFrame = a7;
	}
}

// @Ok
// @Matching
INLINE CSimpleAnim::~CSimpleAnim(void)
{
}

// @Ok
// @Matching
void CRibbon::SetPos(CVector &pos)
{
	if (++this->field_48 == this->mNumPoints)
	{
		this->field_48 = 0;
	}

	this->mPoints[this->field_48] = pos;

	i32 v4 = this->field_48;
	for (i32 i = this->mNumBits - 1; i >= 0; i--)
	{
		this->mBits[i]->field_64 = this->mPoints[v4];
		v4 -= this->mPointsPerBit;
		if (v4 < 0)
		{
			v4 += this->mNumPoints;
		}

		this->mBits[i]->field_58 = this->mPoints[v4];
	}
}

// @Ok
// @Matching
void CRibbon::SetScale(i32 Scale)
{
	for (i32 i = 0; i < this->mNumBits; i++)
	{
		this->mBits[i]->SetScale(Scale);
	}
}

// @Ok
// @Matching
CRibbon::~CRibbon(void)
{
	for (i32 i = 0; i < this->mNumBits; i++)
	{
		delete this->mBits[i];
	}

	Mem_Delete(this->mBits);
	Mem_Delete(this->mPoints);
}

// @Ok
// @AlmostMatching: some weird inlines, but overall good
// @Test
CRibbon::CRibbon(
		CVector *pos,
		i32 numbits,
		i32 pointsperbit,
		i32 middleanimindex,
		i32 endanimindex,
		i32 scale,
		i32 semitrans)
{
	this->mNumBits = numbits;
	this->mPointsPerBit = pointsperbit - 1;
	this->mNumPoints = numbits * (pointsperbit - 1) + 1;

	this->mPoints = static_cast<CVector*>(
			DCMem_New(sizeof(CVector) * this->mNumPoints, 0, 1, 0, 1));

	for (i32 j = 0; j < this->mNumPoints; j++)
	{
		this->mPoints[j] = *pos;
	}

	this->mBits = static_cast<CRibbonBit**>(
			DCMem_New(sizeof(CRibbonBit*) * numbits, 0, 1, 0, 1));

	for (i32 i = this->mNumBits - 1; i >= 0; i--)
	{
		this->mBits[i] = new CRibbonBit();

		this->mBits[i]->mProtected = 1;
		this->mBits[i]->SetAnim(middleanimindex);
		this->mBits[i]->SetScale(scale);

		if (semitrans)
			this->mBits[i]->SetSemiTransparent();
	}

	this->mBits[0]->mBitFlags |= 0x10u;
	this->mBits[0]->SetAnim(endanimindex);
}

// @Ok
// @Matching
void CSmokeTrail::Move(void)
{
	if (this->mFadeAway)
	{
		i32 v2 = 1;

		for (i32 i = 0; i < this->mNumBits; i++)
		{
			if (!this->mBits[i]->Fade(0))
			{
				v2 = 0;
			}
		}

		if (v2)
		{
			this->Die();
		}
	}
}


// @Ok
// @Matching
CSmokeTrail::~CSmokeTrail(void)
{
}

// @Ok
// @Matching
CSmokeTrail::CSmokeTrail(
		CVector* pos,
		i32 numbits,
		i32 r,
		i32 g,
		i32 b)
	: CRibbon(pos, numbits, 2, 2, 2, 400, 1)
{

	i32 v12 = r / this->mNumBits;
	i32 v13 = g / this->mNumBits;
	i32 v14 = b / this->mNumBits;

	for (i32 i = 0; i < this->mNumBits; i++)
	{
		i32 tmp = (this->mNumBits - 1 - i);
		this->mBits[i]->SetTint(
				r - tmp * v12, 
				g - tmp * v13, 
				b - tmp * v14);

		this->mBits[i]->SetTransDecay(8);
	}

}

// @Ok
// @Note: code works because sizeof(SFringeQuad) == sizeof(SSection), weird shit
CGlow::CGlow(u32 NumPoints, u32 NumFringes)
{
	print_if_false(NumPoints != 0, "Bad NumPoints sent to CGlow");
	this->mNumSections = 2 * NumPoints;
	this->mStepAngle = 0x1000u / (2 * NumPoints);
	this->mNumFringes = NumFringes;

	// This shit weird
	this->mpSections = static_cast<SSection*>(DCMem_New(sizeof(SSection) * (this->mNumSections + this->mNumSections * this->mNumFringes), 0, 1, 0, 1));

	this->mpFringes = reinterpret_cast<SFringeQuad*>(&this->mpSections[this->mNumSections]);
	for (u32 i = 0; i < this->mNumSections * this->mNumFringes; i++)
	{
		this->mpFringes[i].CodeBGR = 0x3A000000;
	}

	this->mCentreCodeBGR = 0x32000000;
	this->mMask = -1;

	this->AttachTo(reinterpret_cast<CBit**>(&G_GLOW_LIST));
}

// @Ok
CGlow::~CGlow(void)
{
	Mem_Delete(static_cast<void*>(this->mpSections));
	this->DeleteFrom(reinterpret_cast<CBit**>(&G_GLOW_LIST));
}

// @Ok
// Functional: quad-bit roll orientation, logic verified against Hex-Rays at
// 0x409560. Builds dir/perp1/perp2 via Utils_CalcPerps, indexes
// rcossin_tbl[a6 & 0xFFF] for the roll angle, rollA = (perp1*sin +
// perp2*cos) >> 12, rollB = (perp1*cos + perp2*-sin) >> 12, rollA *= a4,
// rollB *= a5, and the four corners are *a2 -+ rollA -+ rollB. (The 13
// mnemonic diffs from the byte-match phase are the MSVC6 rcossin_tbl
// double-field-read quirk, same as Utils_RotateY; the logic is equivalent.)
void CQuadBit::OrientUsing(CVector *a2, SVECTOR *a3, i32 a4, i32 a5, i32 a6)
{
	CVector dir(a3->vx, a3->vy, a3->vz);
	CVector perp1;
	CVector perp2;

	Utils_CalcPerps(&dir, &perp2, &perp1);

	SSinCos const *sc = &G_RCOSSIN_TBL[a6 & 0xFFF];
	i32 s = sc->sin;
	i32 c = sc->cos;

	CVector rollA = ((perp1 * s) + (perp2 * c)) >> 12;
	CVector rollB = ((perp1 * c) + (perp2 * -s)) >> 12;

	rollA *= a4;
	rollB *= a5;

	this->mPos = *a2 - rollA - rollB;
	this->mPosB = *a2 + rollA - rollB;
	this->mPosC = *a2 - rollA + rollB;
	this->mPosD = *a2 + rollA + rollB;
}

// @Ok
// @Matching: wtf is that cast
void CQuadBit::SetTexture(u32 checksum)
{
	Texture *pTexture = Spool_FindTextureEntry(checksum);
	this->mpTexture = pTexture;

	if (this->mpTexture)
	{
		if (this->mpTexture->field_12 & 0xF0)
			this->mCodeBGR |= 0x20;

		// @FIXME
		this->field_74 = *reinterpret_cast<u32*>(&this->mpTexture->u0);
		// @FIXME
		this->field_78 = *reinterpret_cast<u32*>(&this->mpTexture->u1);
		// @FIXME
		this->field_7C = *reinterpret_cast<u32*>(&this->mpTexture->u2);
		this->field_80 = this->mpTexture->TexWin;
	}
}

// @Ok
// Functional (session-wide functional-only bar, 2026-08-30). Logic fully
// verified against Hex-Rays at 0x40c350: all 4 fill loops, AttachTo, mPos
// copy and mCentreCodeBGR line up instruction-for-instruction with the
// disassembly; the only remaining gap (kept for the record, no longer
// chased) was a 21-mnemonic register-allocation residue from the original
// sharing one preloaded register between the inlined CFriction::Set(1,1,1)
// stores and two of DCMem_New's args, which our source-shape attempts could
// not reproduce. See prior history in this file for the attempt log.
CGlow::CGlow(
		CVector* pVector,
		i32 a3,
		i32 a4,
		u8 a5,
		u8 a6,
		u8 a7,
		u8 a8,
		u8 a9,
		u8 a10)
{
	SSection* pSections = static_cast<SSection*>(DCMem_New(0x80, 0, 1, 0, 1));

	this->mNumSections = 8;
	this->mStepAngle = 0x200;
	this->mNumFringes = 1;

	this->mpSections = pSections;
	this->mpFringes = reinterpret_cast<SFringeQuad*>(this->mpSections + this->mNumSections);

	this->AttachTo(reinterpret_cast<CBit**>(&G_GLOW_LIST));

	this->mPos = *pVector;

	u32 i;

	for (i = 0; i < this->mNumSections; i++)
		this->mpSections[i].Radius = a3;

	this->mCentreCodeBGR = 0x32000000 | (((a7 << 8) | a6) << 8) | a5;

	for (i = 0; i < this->mNumSections; i++)
		this->mpSections[i].PadBGR = (a10 << 16) | (a9 << 8) | a8;

	for (i = 0; i < this->mNumSections; i++)
	{
		this->mpFringes[i].Width = a4;
		this->mpFringes[i].CodeBGR = 0x3A000000;
	}

	this->mMask = -1;
}

// @Ok
INLINE CFlatBit::~CFlatBit(void)
{
	this->DeleteFrom(reinterpret_cast<CBit**>(&G_FLATBIT_LIST));
}

// @Ok
// @Note: this is missing from the original game because CFlatBit is not inlined
CMotionBlur::~CMotionBlur(void)
{
}

// @Ok
// @Test
void CMotionBlur::Move(void)
{
	this->mPos.vx += this->mVel.vx;
	this->mPos.vy += this->mVel.vy;
	this->mPos.vz += this->mVel.vz;

	this->mScale -= this->field_3E;
	if (this->mScale < 0)
	{
		this->mScale = 0;
		this->Die();
		return;
	}

    u8 mCodeBGR = this->mCodeBGR;
    i16 mTransDecay = this->mTransDecay;

    u32 v10 = this->mCodeBGR >> 8;
    u32 mCodeBGR_high = (this->mCodeBGR >> 16);

	u8 v18;
    if ( mTransDecay > mCodeBGR )
      v18 = 0;
    else
      v18 = mCodeBGR - (this->mTransDecay & 0xFF);

	u8 v12;
    if ( mTransDecay > (u8)v10 )
      v12 = 0;
    else
      v12 = v10 - (this->mTransDecay & 0xFF);

	u8 v13;
    if ( mTransDecay > (u8)mCodeBGR_high )
      v13 = 0;
    else
      v13 = mCodeBGR_high - (this->mTransDecay & 0xFF);

	u16 v14 = (v13 << 8) | v12;
    this->mCodeBGR = v18 | this->mCodeBGR & 0xFF000000 | (v14 << 8);

	if (!(this->mCodeBGR & 0xFFFFFF))
	{
		this->Die();
		return;
	}

	if ( ++this->mAge & 1)
	{
		if ( ++this->mFrame >= this->mNumFrames )
			this->mFrame = 0;
	}

	this->mpPSXFrame = &this->mpPSXAnim[this->mFrame];
}

// @Ok
CMotionBlur::CMotionBlur(
		CVector* a2,
		CVector* a3,
		i32 a4,
		i32 a5,
		i32 a6,
		i32 a7)
{
	this->mPos = *a2;
	this->mVel = *a3;
	this->SetAnim(a4);
	this->SetSemiTransparent();
	this->mScale = a5;
	this->field_3E = a6;
	this->mTransDecay = a7;
}

// @Ok
// @Matching
INLINE CBit::CBit()
{
	this->mFric.vx = 1;
	this->mFric.vy = 1;
	this->mFric.vz = 1;
	//this->mFric.Set(1,1,1);

	G_BITCOUNT++;
}

// @Ok
// @Matching
INLINE void* CBit::operator new(size_t size) {

	void *pnew;
	if (G_TOTALBITUSAGE == 0)
		pnew = Mem_New(size);
	else
		pnew = Mem_New(size);

	// Ensure size is a multiple of 4.
	size = ( size + 3 ) & ~0x03;

	// Zero all the newly allocated memory
	u32 *p=(u32 *)pnew;
	for (i32 i=0; i<size/4; ++i) *p++=0;

	return pnew;
}


// @Ok
// 0x409090, 66 bytes: unlink from QuadBitList, then CBit::~CBit. Without it a
// direct "delete pQuadBit" (CPlayer::DoMGSShadow, CBody::KillShadow) left the
// freed bit in the list and Bit_Move walked the heap scribble (0x55555555).
CQuadBit::~CQuadBit(void)
{
	this->DeleteFrom(&G_QUADBIT_LIST);
}

// @Ok
void CBit::operator delete(void* ptr)
{
	Mem_Delete(ptr);
}

// @Ok
// @Matching
INLINE CBit::~CBit()
{
	--G_BITCOUNT;
}

// @Ok
// @Matching
INLINE void CBit::Die(void)
{
	ASSERT(this->mProtected == 0, "A protected bit die");
	this->mDead = 1;
}

// @Ok
// @Matching
INLINE void CBit::AttachTo(void* p)
{
	this->mNext = *reinterpret_cast<CBit**>(p);
	this->mPrevious = 0;
	*reinterpret_cast<CBit**>(p) = this;

	if (this->mNext)
		this->mNext->mPrevious = this;
}

// @Ok
// @Matching
void CBit::SetPos(const CVector &pos)
{
	this->mPos = pos;
}


// @Ok
// @Matching
INLINE void CBit::DeleteFrom(void *p)
{
	if (this->mNext)
		this->mNext->mPrevious = this->mPrevious;

	if (this->mPrevious)
		this->mPrevious->mNext = this->mNext;

	CBit **ppList = reinterpret_cast<CBit**>(p);

	if (*ppList == this)
		*ppList = this->mNext;
}

// @Ok
void CQuadBit::SetTint(unsigned char a2, unsigned char a3, unsigned char a4)
{
  this->mTint = a2 | ((a4 << 16) & 0xFF0000 | (a3 << 8) & 0xFF00) & 0xFFFFFF00;
}


// @Ok
void CQuadBit::SetSemiTransparent()
{
	this->mCodeBGR = (this->mCodeBGR & 0xFFFFFFFE) | 0x2C0;
}

// @Ok
void CQuadBit::SetOpaque(){
	this->mCodeBGR = (this->mCodeBGR & 0xFFFFFDBF) | 0x80;
}


// @Ok
void CQuadBit::SetSubtractiveTransparency(){
	this->mCodeBGR = (this->mCodeBGR & 0xFFFFFF7F) | 0x340;
}

// @Ok
void CQuadBit::SetCorners(const CVector &a2, const CVector &a3, const CVector &a4, const CVector &a5)
{
	this->mPos = a2;
	this->mPosB = a3;
	this->mPosC = a4;
	this->mPosD = a5;
}

// @Ok
void CQuadBit::SetTransparency(unsigned char a2){
	this->mTint = a2 | ((a2 | (a2 << 8)) << 8);
}

// @Ok
// @Matching
// Was blocked on the repo-wide vector.h operator-(CVector,CVector) INLINE
// bug (it compiled inline instead of the real out-of-line call the
// original makes at 0x4E7760). Now that operator- moved out-of-line into
// vector.cpp (2026-08-27), rebuilt clean with 0 mnemonic diffs against
// 0x409400, no source change needed here. a5 is read from the stack frame
// but never referenced in the original disasm; unused here too.
void CQuadBit::OrientUsing(CVector *a2, SVECTOR *a3, int a4, int a5)
{
	CVector dir(a3->vx, a3->vy, a3->vz);
	CVector perp1;
	CVector perp2;

	Utils_CalcPerps(&dir, &perp2, &perp1);

	perp2 *= a4;
	perp1 *= a4;

	this->mPos = *a2 - perp1 - perp2;
	this->mPosB = *a2 + perp1 - perp2;
	this->mPosC = *a2 - perp1 + perp2;
	this->mPosD = *a2 + perp1 + perp2;
}

// @Ok
// @Matching
void CQuadBit::SetTexture(int a, int b){
	DoAssert(a >= 0 && static_cast<u32>(a) < NUM_ANIM_ENTRIES, "Bad lookup value sent to CQuadBit::SetTexture");

	SAnimFrame* pAnim = G_ANIM_TABLE[a];

	DoAssert(b >= 0 && b < *(reinterpret_cast<i32*>(pAnim) - 1), "Bad frame sent to CQuadBit::SetTexture");

	this->mpTexture = pAnim[b].pTexture;

	if (a == 0 && b == 0)
		this->mpTexture = Spool_FindTextureEntry("Shadow2");

	if (this->mpTexture->field_12 & 0xF0)
		this->mCodeBGR |= 0x20u;

	// @FIXME
	this->field_74 = *reinterpret_cast<u32*>(&this->mpTexture->u0);
	// @FIXME
	this->field_78 = *reinterpret_cast<u32*>(&this->mpTexture->u1);
	// @FIXME
	this->field_7C = *reinterpret_cast<u32*>(&this->mpTexture->u2);

	this->field_80 = this->mpTexture->TexWin;
}

// by-anim-name overload, address 0x409190 (unnamed in names.json, sits between
// the (i32,i32) and (Texture*) overloads; assert string names the class).
// @Ok
// @Matching
void CQuadBit::SetTexture(char* pName, i32 frame)
{
	SAnimFrame* pAnim = Spool_FindAnim(pName, 1);

	DoAssert(frame >= 0 && frame < *(reinterpret_cast<i32*>(pAnim) - 1), "Bad frame sent to CQuadBit::SetTexture");

	this->mpTexture = pAnim[frame].pTexture;

	if (this->mpTexture->field_12 & 0xF0)
		this->mCodeBGR |= 0x20u;

	// @FIXME
	this->field_74 = *reinterpret_cast<u32*>(&this->mpTexture->u0);
	// @FIXME
	this->field_78 = *reinterpret_cast<u32*>(&this->mpTexture->u1);
	// @FIXME
	this->field_7C = *reinterpret_cast<u32*>(&this->mpTexture->u2);

	this->field_80 = this->mpTexture->TexWin;
}

// @Ok
// @Matching: the assingments are weird bro
void CQuadBit::SetTexture(Texture *pTex)
{
	this->mpTexture = pTex;
	if (this->mpTexture->field_12 & 0xF0)
		this->mCodeBGR |= 0x20u;

	// @FIXME
	this->field_74 = *reinterpret_cast<u32*>(&this->mpTexture->u0);
	// @FIXME
	this->field_78 = *reinterpret_cast<u32*>(&this->mpTexture->u1);
	// @FIXME
	this->field_7C = *reinterpret_cast<u32*>(&this->mpTexture->u2);

	this->field_80 = this->mpTexture->TexWin;
}

// @Ok
// By-name overload, address 0x409280 (unnamed in names.json, sits right
// after SetTexture(u32)). Looks the texture up by name and stores it with
// no null check, same field writes as SetTexture(Texture*). Needed by
// CSkinGoo::CSkinGoo(CSuper*, SSkinGooSource2*, i32, SSkinGooParams*),
// which stores two texture name strings per source entry instead of
// checksums. Spool_FindTextureEntry(char*) never returns null (falls back
// to gAnimTable[13]->pTexture), which is why this overload skips the null
// check that SetTexture(u32) has.
void CQuadBit::SetTexture(char* pName)
{
	this->mpTexture = Spool_FindTextureEntry(pName);
	if (this->mpTexture->field_12 & 0xF0)
		this->mCodeBGR |= 0x20u;

	this->field_74 = *reinterpret_cast<u32*>(&this->mpTexture->u0);
	this->field_78 = *reinterpret_cast<u32*>(&this->mpTexture->u1);
	this->field_7C = *reinterpret_cast<u32*>(&this->mpTexture->u2);

	this->field_80 = this->mpTexture->TexWin;
}

// @Ok
// @Matching
INLINE CNonRenderedBit::CNonRenderedBit(void)
{
	this->AttachTo(&G_NONRENDEREDBIT_LIST);
}

// @Ok
// @Matching
INLINE CNonRenderedBit::~CNonRenderedBit(void)
{
	this->DeleteFrom(&G_NONRENDEREDBIT_LIST);
}

// @Ok
// @Note: not matererialized. it's very weird on PSX and on PowerPC
// according to the symbols there's no local variable and i can't seem to
// be able to write it without it.
INLINE void CFT4Bit::IncFrame(void)
{
	i16 val = ((this->mFrame << 8) | this->mFrameFrac) + this->mAnimSpeed;

	this->mFrame = val >> 8;
	this->mFrameFrac = val;

	this->mpPSXFrame = &this->mpPSXAnim[this->mFrame];
}

// @Ok
INLINE CFT4Bit::~CFT4Bit()
{
	if (this->mDeleteAnimOnDestruction)
		Mem_Delete(reinterpret_cast<void*>(this->mpPSXAnim));
}


// @Ok
// @Matching
void CFT4Bit::SetAnimSpeed(i16 s)
{
	this->mAnimSpeed = s;
}

// @Ok
// @Matching
INLINE void CFT4Bit::SetScale(u16 s)
{
	this->mScale = s;
}


// @Ok
// @Matching
INLINE void CFT4Bit::SetSemiTransparent()
{
	this->mCodeBGR |= 0x2000000;
}

// @Ok
// @Matching
void CFT4Bit::SetTransparency(u8 t)
{
	this->mCodeBGR = t | this->mCodeBGR & 0xFF000000 | ((t | (t << 8)) << 8);
}


// @Ok
// @Matching
INLINE void CFT4Bit::SetAnim(i32 a2)
{
	ASSERT(a2 >= 0 && !(static_cast<u32>(a2) >= NUM_ANIM_ENTRIES), "Bad lookup value sent to SetAnim");
	ASSERT(this->mDeleteAnimOnDestruction == 0, "mDeleteAnimOnDestruction set?");

	this->mpPSXAnim = G_ANIM_TABLE[a2];
	this->mNumFrames = *reinterpret_cast<u8*>(&this->mpPSXAnim[-1].pTexture);
	this->mFrameFrac = 0;
	this->mFrame = 0;
	this->mpPSXFrame = this->mpPSXAnim;
}

// @Ok
// @Matching
INLINE void CFT4Bit::SetTint(u8 r, u8 g, u8 b)
{
	this->mCodeBGR = (this->mCodeBGR & 0xFF000000) | (b << 0x10) | (g << 8) | r;
}

// @Ok
// @Matching
void CFT4Bit::SetTexture(Texture* pTexture)
{
	ASSERT(this->mpPSXAnim == 0, "mpPSXAnim already set?");
	ASSERT(pTexture != 0, "No Texture for SetTexture");

	this->mpPSXAnim = static_cast<SAnimFrame*>(Mem_New(sizeof(SAnimFrame)));
	this->mDeleteAnimOnDestruction = 1;

	i32 w = pTexture->u1 - pTexture->u0;
	i32 h = pTexture->v2 - pTexture->v0;

	this->mpPSXAnim->Width = w;
	this->mpPSXAnim->Height = h;

	this->mpPSXAnim->OffX = w / -2;
	this->mpPSXAnim->OffY = h / -2;

	this->mpPSXAnim->pTexture = pTexture;
	this->mpPSXFrame = this->mpPSXAnim;

	this->mNumFrames = 1;
}

// @Ok
// @Matching
void CFT4Bit::SetTexture(u32 Checksum)
{
	ASSERT(this->mpPSXAnim == 0, "mpPSXAnim already set?");

	Texture *pTexture = Spool_FindTextureEntry(Checksum);
	ASSERT(pTexture != 0, "No Texture for SetTexture");

	this->mpPSXAnim = static_cast<SAnimFrame*>(Mem_New(sizeof(SAnimFrame)));
	this->mDeleteAnimOnDestruction = 1;

	i32 w = pTexture->u1 - pTexture->u0;
	i32 h = pTexture->v2 - pTexture->v0;

	this->mpPSXAnim->Width = w;
	this->mpPSXAnim->Height = h;

	this->mpPSXAnim->OffX = w / -2;
	this->mpPSXAnim->OffY = h / -2;

	this->mpPSXAnim->pTexture = pTexture;
	this->mpPSXFrame = this->mpPSXAnim;

	this->mNumFrames = 1;
}

// @Ok
// @Matching
i32 CFT4Bit::Fade(i32 die)
{
	if (!(this->mCodeBGR & 0xFFFFFF))
	{
		if (die)
		{
			this->Die();
		}

		return 1;
	}

	u8 r = (this->mCodeBGR) & 0xFF;
	u8 g = (this->mCodeBGR >> 8) & 0xFF;
	u8 b = (this->mCodeBGR >> 16) & 0xFF;

#define DECAY_COLOR(x) if (this->mTransDecay > (x)) (x) = 0; else (x) -= this->mTransDecay;

	DECAY_COLOR(r);
	DECAY_COLOR(g);
	DECAY_COLOR(b);

#undef DECAY_COLOR 


	this->mCodeBGR = (this->mCodeBGR & 0xFF000000) | r | (g << 8) | (b << 16);

	return 0;
}

// @Ok
// Functional (session-wide functional-only bar, 2026-08-30). Verified against
// Hex-Rays at 0x410890: mPos = *pCenter, mVel = {cos*velScale, 0, sin*velScale},
// SetAnim/SetSemiTransparent/SetScale/SetTransDecay all match field-for-field.
// The trailing `p->mFrigDeltaZ = frigDeltaZ;` outside the `if (p)` block is not
// a bug in this source: the original binary does the exact same unconditional
// write after the allocation-failure branch (a genuine original defect,
// reproduced here per the "don't fix original bugs" rule). Byte residue (72
// mnemonic diffs, register/inlining-level choice only) was chased through 14
// source-shape hypotheses previously; not revisited since this bar only needs
// functional correctness.
i32 Bit_MakeSpriteRing(CVector *pCenter, i32 count, i32 velScale, i32 animIndex, i32 scale, i32 field3E, i32 transDecay, i32 frigDeltaZ)
{
	for (i32 i = 0; i < count; i++)
	{
		i32 angle = ((i * 0x1000) / count) & 0xFFF;
		CVector vel(G_RCOSSIN_TBL[angle].cos * velScale, 0, G_RCOSSIN_TBL[angle].sin * velScale);

		CFlatBit *p = new CFlatBit();

		if (p)
		{
			p->mVel = vel;
			p->mPos = *pCenter;

			p->SetAnim(animIndex);
			p->SetSemiTransparent();

			p->SetScale(scale);
			p->field_3E = field3E;
			p->SetTransDecay(transDecay);
		}

		p->mFrigDeltaZ = frigDeltaZ;
	}

	return 0;
}

// @Ok
void CBit::Move(void)
{
}

// @Ok
void MoveList(CBit *pBit)
{
	for (CBit *p = pBit; p; p = p->mNext)
	{
		if (!p->mDead)
			p->Move();
	}
}

// @Ok
// @Matching
void Bit_SetSparkRGB(u8 r, u8 g, u8 b)
{
	G_SPARK_RGB[0] = r;
	G_SPARK_RGB[1] = g;
	G_SPARK_RGB[2] = b;
}

// @Ok
// @Matching
void Bit_SetSparkFadeRGB(u8 r, u8 g, u8 b)
{
	G_SPARK_FADE_RGB[0] = r;
	G_SPARK_FADE_RGB[1] = g;
	G_SPARK_FADE_RGB[2] = b;
}

// @Ok
INLINE CSpecialDisplay::CSpecialDisplay(void)
{
	this->AttachTo(&G_SPECIALDISPLAY_LIST);
}

// @Ok
void CGlow::SetCentreRGB(unsigned char a2, unsigned char a3, unsigned char a4)
{
	this->mCentreCodeBGR = 0x32000000 | (((a4 << 8) | a3) << 8) | a2;
}

// @Ok
// Functional: verified against Hex-Rays at 0x40f3d0, which does a plain
// 6-byte copy of *pVec into the global (a DWORD then a WORD, matching
// CSVector's size). Same shape as the already-@Ok
// Bit_SetSparkTrajectoryCone right below.
void Bit_SetSparkTrajectory(const CSVector *pVec)
{
	G_SPARK_TRAJECTORY = *pVec;
}

// @Ok
// Functional: set spark trajectory cone, logic verified against Hex-Rays at
// 0x40f3f0. Just stores the cone vector into gSparkTrajectoryCone.
void Bit_SetSparkTrajectoryCone(const CSVector *pVec)
{
	G_SPARK_TRAJECTORY_CONE = *pVec;
}

// @Ok
// @Matching
INLINE CFT4Bit::CFT4Bit(void)
{
	this->mAnimSpeed = 0x80;
	this->mScale = 400;
	this->mCodeBGR = 0x2C808080;
}

// @Ok
// @Validate: when inlined
INLINE CLinked2EndedBit::CLinked2EndedBit(void)
{
	this->AttachTo(&G_LINKED2ENDEDBIT_LIST_LEFTOVER);
}

// @Ok
// @AlmostMatching: slightly different inline
INLINE CLinked2EndedBit::~CLinked2EndedBit(void)
{
	this->DeleteFrom(reinterpret_cast<CBit**>(&G_LINKED2ENDEDBIT_LIST_LEFTOVER));
}

// @Ok
// @Matching
INLINE CRibbonBit::CRibbonBit(void)
{
}

// @Ok
// @Matching
CRibbonBit::~CRibbonBit(void)
{
}

// @Ok
void CRibbonBit::Move(void)
{
	this->IncFrameWithWrap();
}

// @Ok
// @Note: not materialized, got it from CRibbonBit::Move
void CFT4Bit::IncFrameWithWrap(void)
{
	i16 val = ((this->mFrame << 8) | this->mFrameFrac) + this->mAnimSpeed;

	this->mFrame = val >> 8;
	this->mFrameFrac = val;

	if (this->mFrame >= this->mNumFrames)
		this->mFrame = 0;

	this->mpPSXFrame = &this->mpPSXAnim[this->mFrame];
}

/*
// @Ok
void CTexturedRibbon::SetOuterRGBi(int index, unsigned char a3, unsigned char a4, unsigned char a5)
{
	this->field_60[index+1] = (a3 | (((a5 << 8) | a4) << 8));
}
*/

// @Ok
// @Matching
void CGlow::SetRadius(int radius)
{
	for (u32 i = 0; i < this->mNumSections; i++)
	{
		this->mpSections[i].Radius = radius;
	}
}

// @Ok
// Functional: set ribbon color, logic verified against Hex-Rays at 0x40a920.
// The original has an explicit field_3C != -1 check, but the loop runs 0
// times when field_3C is -1, so the behavior is the same.
void CSimpleTexturedRibbon::SetRGB(unsigned char r, unsigned char g, unsigned char b)
{
	int value = (r | (((b << 8) | g) << 8));
	u32 *ptr = this->pColours;

	int i = 0;
	for (i = 0; i < this->field_3C + 1; i++)
		ptr[i] = value;
}

// @Ok
void CGlow::SetRGB(u8 r, u8 g, u8 b)
{
	u32 value = (r | (((b << 8) | g) << 8));

	for (u32 i = 0; i < this->mNumSections; i++)
	{
		this->mpSections[i].PadBGR = value;
	}
}

// @Ok
// @Matching
void Bit_ReduceRGB(u32* p, i32 amount)
{
	u8 b = *p;
	u8 g = *p >> 8;
	u8 r = *p >> 16;

	b = (b >= amount) ? b - amount : 0;
	g = (g >= amount) ? g - amount : 0;
	r = (r >= amount) ? r - amount : 0;

	*p = (*p & 0xFF000000) | (r << 16) | (g << 8) | b;
}

// @Ok
// @Matching
INLINE void CFT4Bit::SetFrame(i32 frame)
{
	ASSERT(frame >= 0 && frame < this->mNumFrames, "Bad frame sent to SetFrame");
	ASSERT(this->mpPSXAnim != 0, "SetFrame called before SetAnim");

	this->mFrame = frame;
	this->mFrameFrac = 0;

	this->mpPSXFrame = &this->mpPSXAnim[this->mFrame];
}

// @Ok
// @Note: no materialization exists
INLINE void CFT4Bit::SetTransDecay(i32 decay)
{
	this->mTransDecay = decay;
}

// @Ok
// @Matching
CFlatBit::CFlatBit(void)
{
	this->AttachTo(reinterpret_cast<CBit**>(&G_FLATBIT_LIST));

	this->mSemiTransparencyRate = 0x20;
	this->mAngFric = 1;
	this->mPostScale = 0x10001000;
}

// @Ok
// @Matching
void Bit_UpdateQuickAnimLookups(void)
{
	for (i32 i = 0; i < NUM_ANIM_ENTRIES; i++)
	{
		if (G_ANIM_TABLE[i])
		{
			DoAssert(G_ANIM_TABLE[i]->pTexture != 0, "Anim has no texture, don't know the region!");
			Spool_RemoveAccess(
					reinterpret_cast<void**>(&G_ANIM_TABLE[i]),
					G_ANIM_TABLE[i]->pTexture->mRegion);
		}

		Spool_AnimAccess(gAnimNames[i], &G_ANIM_TABLE[i]);
#ifdef SPIDEY_STANDALONE
		// SPIDEY_TRACE_ANIMS=1: show which quick anim lookups resolve
		if (getenv("SPIDEY_TRACE_ANIMS"))
			printf("ANIMS %2d %-12s -> %p\n", i, gAnimNames[i], (void*)G_ANIM_TABLE[i]);
#endif
	}
}

// @Ok
// @AlmostMatching: 1 byte diff in CFriction::Set
CGlassBit::CGlassBit(
		const CVector *Pos,
		const CVector *Vel,
		i32 GroundY,
		u8 r,
		u8 g,
		u8 b,
		i32 dx,
		i32 dy,
		i32 dz)
{
	this->AttachTo(&G_GLASS_LIST);

	this->mPos = *Pos;

	this->mPosA.vx = Pos->vx + ((Rnd(2 * dx + 1) - dx) << 12);
	this->mPosA.vy = Pos->vy + ((Rnd(2 * dy + 1) - dy) << 12);
	this->mPosA.vz = Pos->vz + ((Rnd(2 * dz + 1) - dz) << 12);
	this->mPosB.vx = Pos->vx + ((Rnd(2 * dx + 1) - dx) << 12);
	this->mPosB.vy = Pos->vy + ((Rnd(2 * dy + 1) - dy) << 12);
	this->mPosB.vz = Pos->vz + ((Rnd(2 * dz + 1) - dz) << 12);
	this->mPosC.vx = Pos->vx + ((Rnd(2 * dx + 1) - dx) << 12);
	this->mPosC.vy = Pos->vy + ((Rnd(2 * dy + 1) - dy) << 12);
	this->mPosC.vz = Pos->vz + ((Rnd(2 * dz + 1) - dz) << 12);

	this->mVel = *Vel;

	this->mGroundY = GroundY;
	
	this->mDefaultR = r >> 2;
	this->mDefaultG = g >> 2;
	this->mDefaultB = b >> 2;

	this->mR = r >> 2;
	this->mG = g >> 2;
	this->mB = b >> 2;

	this->mFadeRate = 4;
	this->mLifetime = Rnd(30) + 30;
}

// @Ok
// @Matching
void CGlassBit::Move(void)
{
	this->mPosA += this->mVel;
	this->mPosB += this->mVel;
	this->mPosC += this->mVel;

	this->mVel.vy += 10000;

	if (this->mPosA.vy > this->mGroundY)
	{
		if (this->mAge < 2)
		{
			this->mPosA.vy = this->mGroundY - (Rnd(50) << 12);
			this->mPosB.vy = this->mGroundY - (Rnd(50) << 12);
			this->mPosC.vy = this->mGroundY - (Rnd(50) << 12);

			this->mVel.vy = -this->mVel.vy;
			this->mVel.vy >>= 2;
			this->mAge++;
		}
		else
		{
			this->mVel.vy = 0;
		}

		this->mVel.vx >>= 1;
		this->mVel.vz >>= 1;
	}

	if (this->mAge >= 2 || Rnd(5))
	{
		this->mR = this->mDefaultR;
		this->mG = this->mDefaultG;
		this->mB = this->mDefaultB;
	}

	else
	{
		this->mB = 64;
		this->mR = 48;
		this->mG = 48;
	}

	if (this->mLifetime)
	{
		this->mLifetime--;
	}

	if (!this->mLifetime)
	{
		i32 newR;
		if (this->mDefaultR > this->mFadeRate)
		{
			newR = this->mDefaultR - this->mFadeRate;
		}
		else
		{
			newR = 0;
		}
		this->mDefaultR = newR;

		i32 newG;
		if (this->mDefaultG > this->mFadeRate)
		{
			newG = this->mDefaultG - this->mFadeRate;
		}
		else
		{
			newG = 0;
		}
		this->mDefaultG = newG;

		i32 newB;
		if (this->mDefaultB > this->mFadeRate)
		{
			newB = this->mDefaultB - this->mFadeRate;
		}
		else
		{
			newB = 0;
		}
		this->mDefaultB = newB;

	}

	if (!(this->mDefaultB | this->mDefaultR | this->mDefaultG))
	{
		this->Die();
	}
}

// @Ok
// @Matching
CGlassBit::~CGlassBit(void)
{
	this->DeleteFrom(&G_GLASS_LIST);
}

void validate_CFlatBit(void){
	VALIDATE_SIZE(CFlatBit, 0x68);

	VALIDATE(CFlatBit, mAngle, 0x58);
	VALIDATE(CFlatBit, field_5A, 0x5A);
	VALIDATE(CFlatBit, mAngFric, 0x5E);
	VALIDATE(CFlatBit, mPostScale, 0x60);
	VALIDATE(CFlatBit, mSemiTransparencyRate, 0x65);
	VALIDATE(CFlatBit, mClutOverride, 0x66);
}

void validate_CFT4Bit(void){
	VALIDATE(CFT4Bit, mTransDecay, 0x3C);
	VALIDATE(CFT4Bit, field_3E, 0x3E);

	VALIDATE(CFT4Bit, mCodeBGR, 0x40);

	VALIDATE(CFT4Bit, mDeleteAnimOnDestruction, 0x44);
	VALIDATE(CFT4Bit, mpPSXAnim, 0x48);
	VALIDATE(CFT4Bit, mpPSXFrame, 0x4C);

	VALIDATE(CFT4Bit, mBitFlags, 0x50);

	VALIDATE(CFT4Bit, mNumFrames, 0x51);
	VALIDATE(CFT4Bit, mFrame, 0x52);
	VALIDATE(CFT4Bit, mFrameFrac, 0x53);

	VALIDATE(CFT4Bit, mAnimSpeed, 0x54);
	VALIDATE(CFT4Bit, mScale, 0x56);
}

void validate_CQuadBit(void)
{
	VALIDATE_SIZE(CQuadBit, 0x84);

	VALIDATE(CQuadBit, mPosB, 0x3C);
	VALIDATE(CQuadBit, mPosC, 0x48);
	VALIDATE(CQuadBit, mPosD, 0x54);
	VALIDATE(CQuadBit, mpTexture, 0x60);
	VALIDATE(CQuadBit, mCodeBGR, 0x64);

	VALIDATE(CQuadBit, field_68, 0x68);

	VALIDATE(CQuadBit, mTint, 0x6C);

	VALIDATE(CQuadBit, field_70, 0x70);

	VALIDATE(CQuadBit, field_74, 0x74);
	VALIDATE(CQuadBit, field_78, 0x78);
	VALIDATE(CQuadBit, field_7C, 0x7C);
	VALIDATE(CQuadBit, field_80, 0x80);
}



void validate_CBit(void)
{
	VALIDATE_SIZE(CBit, 0x3C);
	VALIDATE(CBit, mPrevious, 0x4);
	VALIDATE(CBit, mNext, 0x8);

	VALIDATE(CBit, mAge, 0xC);
	VALIDATE(CBit, mLifetime, 0xE);

	VALIDATE(CBit, mPos, 0x10);
	VALIDATE(CBit, mVel, 0x1C);
	VALIDATE(CBit, mAcc, 0x28);
	VALIDATE(CBit, mDead, 0x37);
	VALIDATE(CBit, mFrigDeltaZ, 0x38);
	VALIDATE(CBit, mProtected, 0x3A);
	VALIDATE(CBit, mType, 0x3B);
}

void validate_CSmokeTrail(void)
{
	VALIDATE_SIZE(CSmokeTrail, 0x58);

	VALIDATE(CSmokeTrail, mFadeAway, 0x54);
}

void validate_CGlow(void)
{
	VALIDATE_SIZE(CGlow, 0x5C);

	VALIDATE(CGlow, mpSections, 0x3C);
	VALIDATE(CGlow, mpFringes, 0x40);

	VALIDATE(CGlow, mNumSections, 0x44);
	VALIDATE(CGlow, mNumFringes, 0x48);
	VALIDATE(CGlow, mCentreCodeBGR, 0x4C);
	VALIDATE(CGlow, mStepAngle, 0x50);
	VALIDATE(CGlow, mSkipTriangles, 0x52);

	VALIDATE(CGlow, mAngle, 0x54);
	VALIDATE(CGlow, mMask, 0x58);
}

void validate_CLinked2EndedBit(void)
{
	VALIDATE_SIZE(CLinked2EndedBit, 0x70);

	VALIDATE(CLinked2EndedBit, field_58, 0x58);
	VALIDATE(CLinked2EndedBit, field_64, 0x64);
}

void validate_CRibbon(void)
{
	VALIDATE_SIZE(CRibbon, 0x54);

	VALIDATE(CRibbon, mNumBits, 0x3C);
	VALIDATE(CRibbon, mPointsPerBit, 0x40);
	VALIDATE(CRibbon, mNumPoints, 0x44);

	VALIDATE(CRibbon, field_48, 0x48);

	VALIDATE(CRibbon, mPoints, 0x4C);
	VALIDATE(CRibbon, mBits, 0x50);
}

void validate_CRibbonBit(void)
{
	VALIDATE_SIZE(CRibbonBit, 0x70);
}

/*
void validate_CTexturedRibbon(void)
{
	VALIDATE(CTexturedRibbon, field_60, 0x60);
}
*/

void validate_CSimpleTexturedRibbon(void)
{
	VALIDATE_SIZE(CSimpleTexturedRibbon, 0x4C);

	VALIDATE(CSimpleTexturedRibbon, field_3C, 0x3C);

	VALIDATE(CSimpleTexturedRibbon, field_3E, 0x3E);

	VALIDATE(CSimpleTexturedRibbon, pTextures, 0x40);
	VALIDATE(CSimpleTexturedRibbon, field_44, 0x44);
	VALIDATE(CSimpleTexturedRibbon, pColours, 0x48);
}

void validate_CSimpleAnim(void)
{
	VALIDATE_SIZE(CSimpleAnim, 0x70);

	VALIDATE(CSimpleAnim, mDie, 0x68);
	VALIDATE(CSimpleAnim, mDieFrame, 0x6C);
}

void validate_CNonRenderedBit(void)
{
	VALIDATE_SIZE(CNonRenderedBit, 0x3C);
}

void validate_CMotionBlur(void)
{
	VALIDATE_SIZE(CMotionBlur, 0x68);
}

void validate_CSpecialDisplay(void)
{
	VALIDATE_SIZE(CSpecialDisplay, 0x3C);
}

void validate_SFlatBitVelocity(void)
{
	VALIDATE_SIZE(SSinCos, 0x4);

	VALIDATE(SSinCos, sin, 0x0);
	VALIDATE(SSinCos, cos, 0x2);
}

void validate_CCombatImpactRing(void)
{
	VALIDATE_SIZE(CCombatImpactRing, 0x74);

	VALIDATE(CCombatImpactRing, field_68, 0x68);
	VALIDATE(CCombatImpactRing, field_6C, 0x6C);
	VALIDATE(CCombatImpactRing, field_70, 0x70);
}

void validate_SRibbonPoint(void)
{
	VALIDATE_SIZE(SRibbonPoint, 0x24);

	VALIDATE(SRibbonPoint, Pos, 0x0);
	VALIDATE(SRibbonPoint, r, 0xC);
	VALIDATE(SRibbonPoint, g, 0xD);
	VALIDATE(SRibbonPoint, b, 0xE);
	VALIDATE(SRibbonPoint, Width, 0x10);
	VALIDATE(SRibbonPoint, WidthB, 0x12);
	VALIDATE(SRibbonPoint, rB, 0x14);
	VALIDATE(SRibbonPoint, gB, 0x15);
	VALIDATE(SRibbonPoint, bB, 0x16);
	VALIDATE(SRibbonPoint, Last1Scr, 0x18);
	VALIDATE(SRibbonPoint, Last2Scr, 0x1C);
	VALIDATE(SRibbonPoint, Last3Scr, 0x20);
}

void validate_CFrag(void)
{
	VALIDATE_SIZE(CFrag, 0x68);
}

void validate_CPixel(void)
{
	VALIDATE_SIZE(CPixel, 0x48);

	VALIDATE(CPixel, tag, 0x3C);

	VALIDATE(CPixel, r0, 0x40);
	VALIDATE(CPixel, g0, 0x41);
	VALIDATE(CPixel, b0, 0x42);

	VALIDATE(CPixel, code, 0x43);
	VALIDATE(CPixel, mWidthHeight, 0x44);
}

void validate_CBitServer(void)
{
	VALIDATE_SIZE(CBitServer, 0x108);
}

void validate_CChunkBit(void)
{
	// Size and field offsets 0x94-0xC4 confirmed while decompiling DisplayChunkBitList
	// (0x40bac0, 2026-08-31); see bit.h's CChunkBit and DisplayChunkBitList below.
	VALIDATE_SIZE(CChunkBit, 0xC8);

	VALIDATE(CChunkBit, mPosA, 0x3C);
	VALIDATE(CChunkBit, mPosB, 0x44);
	VALIDATE(CChunkBit, mPosC, 0x4C);
	VALIDATE(CChunkBit, mPosD, 0x54);

	VALIDATE(CChunkBit, mWorldPosA, 0x5C);
	VALIDATE(CChunkBit, mWorldPosB, 0x68);
	VALIDATE(CChunkBit, mWorldPosC, 0x74);
	VALIDATE(CChunkBit, mWorldPosD, 0x80);

	VALIDATE(CChunkBit, mAngles, 0x8C);

	VALIDATE(CChunkBit, mUV0, 0x94);
	VALIDATE(CChunkBit, mUV1, 0x9C);
	VALIDATE(CChunkBit, mUV2, 0xA4);

	VALIDATE(CChunkBit, mClut, 0xB4);

	VALIDATE(CChunkBit, mColorA, 0xB8);
	VALIDATE(CChunkBit, mColorB, 0xBC);
	VALIDATE(CChunkBit, mColorC, 0xC0);
	VALIDATE(CChunkBit, mColorD, 0xC4);
}

void validate_CShatterBit(void)
{
	VALIDATE_SIZE(CShatterBit, 0xD8);

	VALIDATE(CShatterBit, mShardColor, 0xC8);
	VALIDATE(CShatterBit, mSpinRate, 0xCC);
	VALIDATE(CShatterBit, mTrailRibbon, 0xD4);
}

void validate_CTextBox(void)
{
	VALIDATE_SIZE(CTextBox, 0x44);

	VALIDATE(CTextBox, field_3C, 0x3C);
}

void validate_CFireyExplosion(void)
{
	VALIDATE_SIZE(CFireyExplosion, 0x3C);
}

void validate_CWibbly(void)
{
	VALIDATE_SIZE(CWibbly, 0x98);

	VALIDATE(CWibbly, field_48, 0x48);
	VALIDATE(CWibbly, field_4C, 0x4C);
	VALIDATE(CWibbly, field_58, 0x58);
	VALIDATE(CWibbly, field_64, 0x64);
	VALIDATE(CWibbly, field_70, 0x70);
	VALIDATE(CWibbly, field_7C, 0x7C);

	VALIDATE(CWibbly, field_80, 0x80);
	VALIDATE(CWibbly, field_84, 0x84);

	VALIDATE(CWibbly, field_88, 0x88);
	VALIDATE(CWibbly, field_8C, 0x8C);

	VALIDATE(CWibbly, field_90, 0x90);
	VALIDATE(CWibbly, field_94, 0x94);
}

void validate_SBitServerEntry(void)
{
	VALIDATE_SIZE(SBitServerEntry, 0x8);

	VALIDATE(SBitServerEntry, field_0, 0x0);
	VALIDATE(SBitServerEntry, field_4, 0x4);
}

void validate_SSection(void)
{
	VALIDATE_SIZE(SSection, 8);

	VALIDATE(SSection, Radius, 0x0);
	VALIDATE(SSection, PadBGR, 0x4);
}

void validate_SFringeQuad(void)
{
	VALIDATE_SIZE(SFringeQuad, sizeof(SSection));
	VALIDATE_SIZE(SFringeQuad, 8);

	VALIDATE(SFringeQuad, Width, 0x0);
	VALIDATE(SFringeQuad, CodeBGR, 0x4);
}

void validate_vector4d(void)
{
	VALIDATE_SIZE(vector4d, 0x10);
	VALIDATE(vector4d, field_0, 0x0);
}

void validate_vector3d(void)
{
	VALIDATE_SIZE(vector3d, 0xC);

	VALIDATE(vector3d, field_0, 0x0);
}

void validate_CGlassBit(void)
{
	VALIDATE_SIZE(CGlassBit, 0x6C);

	VALIDATE(CGlassBit, mPosA, 0x3C);
	VALIDATE(CGlassBit, mPosB, 0x48);
	VALIDATE(CGlassBit, mPosC, 0x54);

	VALIDATE(CGlassBit, mGroundY, 0x60);

	VALIDATE(CGlassBit, mDefaultR, 0x64);
	VALIDATE(CGlassBit, mDefaultG, 0x65);
	VALIDATE(CGlassBit, mDefaultB, 0x66);

	VALIDATE(CGlassBit, mR, 0x67);
	VALIDATE(CGlassBit, mG, 0x68);
	VALIDATE(CGlassBit, mB, 0x69);

	VALIDATE(CGlassBit, mFadeRate, 0x6A);
}

void validate_SRibbonTexture(void)
{
	VALIDATE_SIZE(SRibbonTexture, 0x20);

	VALIDATE(SRibbonTexture, field_0, 0x0);
	VALIDATE(SRibbonTexture, field_2, 0x2);

	VALIDATE(SRibbonTexture, field_4, 0x4);
	VALIDATE(SRibbonTexture, field_5, 0x5);
	VALIDATE(SRibbonTexture, field_6, 0x6);
	VALIDATE(SRibbonTexture, field_7, 0x7);
	VALIDATE(SRibbonTexture, field_8, 0x8);
	VALIDATE(SRibbonTexture, field_9, 0x9);
	VALIDATE(SRibbonTexture, field_A, 0xA);
	VALIDATE(SRibbonTexture, field_B, 0xB);

	VALIDATE(SRibbonTexture, field_C, 0xC);
	VALIDATE(SRibbonTexture, field_E, 0xE);

	VALIDATE(SRibbonTexture, u0, 0x10);
	VALIDATE(SRibbonTexture, v0, 0x11);

	VALIDATE(SRibbonTexture, mClut, 0x12);

	VALIDATE(SRibbonTexture, u1, 0x14);
	VALIDATE(SRibbonTexture, v1, 0x15);

	VALIDATE(SRibbonTexture, mPage, 0x16);


	VALIDATE(SRibbonTexture, u2, 0x18);
	VALIDATE(SRibbonTexture, v2, 0x19);

	VALIDATE(SRibbonTexture, u3, 0x1A);
	VALIDATE(SRibbonTexture, v3, 0x1B);

	VALIDATE(SRibbonTexture, mTexWin, 0x1C);
}

void validate_SSimpleRibbonParams(void)
{
	VALIDATE_SIZE(SSimpleRibbonParams, 0x1C);

	VALIDATE(SSimpleRibbonParams, mPos, 0x0);
	VALIDATE(SSimpleRibbonParams, mWidth, 0x18);
}

void validate_CSpark(void)
{
	VALIDATE_SIZE(CSpark, 0x4C);

	VALIDATE(CSpark, mFadeR, 0x48);
	VALIDATE(CSpark, mFadeG, 0x49);
	VALIDATE(CSpark, mFadeB, 0x4A);
}

#include "my_patch.h"

// @Bogus
void patch_CBit(void)
{
	PATCH_PUSH_RET(0x004088E0, CBit::AttachTo);
	PATCH_PUSH_RET(0x00408900, CBit::DeleteFrom);
	PATCH_PUSH_RET(0x00408930, CBit::Die);
	PATCH_PUSH_RET(0x00408950, CBit::SetPos);
}

// @Bogus
void patch_CFT4Bit(void)
{
	PATCH_PUSH_RET(0x00408C70, CFT4Bit::SetScale);
	PATCH_PUSH_RET(0x00408C80, CFT4Bit::SetSemiTransparent);
	PATCH_PUSH_RET(0x00408CC0, CFT4Bit::SetTint);

	PATCH_PUSH_RET(0x00408CF0, CFT4Bit::SetAnim);
	PATCH_PUSH_RET(0x00408E90, CFT4Bit::SetFrame);
	PATCH_PUSH_RET_POLY(0x00408DF0, CFT4Bit::SetTexture, "?SetTexture@CFT4Bit@@QAEXPAUTexture@@@Z");
	PATCH_PUSH_RET(0x00408EF0, CFT4Bit::Fade);
	PATCH_PUSH_RET(0x00408C60, CFT4Bit::SetAnimSpeed);
	PATCH_PUSH_RET(0x00408C90, CFT4Bit::SetTransparency);

	PATCH_PUSH_RET_POLY(0x004C9460, CFT4Bit::SetTexture, "?Spool_FindTextureEntry@@YAPAUTexture@@I@Z");

	PATCH_PUSH_RET_POLY(0x0040F980, CSimpleAnim::Move, "?Move@CSimpleAnim@@UAEXXZ");
}

// Everything below is hooked into the running game. Two rules decided what is
// in and what is out.
//
// 1. Virtual functions and constructors/destructors go through
//    PATCH_PUSH_RET_POLY. Taking the address of a virtual member in MSVC6 gives
//    a vcall thunk, not the function, and patching the exe with that thunk would
//    loop forever.
// 2. Nothing that draws is hooked. Every Display*List function and
//    CSimpleTexturedRibbon::Display end up in PCGfx_Draw*, and PCGfx.cpp still
//    keeps gDxPolys/gEndSceneRelatedTwo as plain repo globals, so our polys
//    would land in the dll's own array and never reach the screen. Those come
//    back once PCGfx gets the G_* treatment.
//
// Constructors are only hooked where our class declares every virtual the
// original vtable has, because a hooked constructor stamps our vtable on the
// object. Skipped for that reason: CQuadBit (the original has
// CQuadBit::~CQuadBit at 0x409090, we have no destructor), CTextBox (the
// original has CTextBox::Move at 0x40D770, we have none), CSimpleTexturedRibbon
// and CWibbly (their vtables carry a Display we cannot hook, see rule 2).

// @Bogus
void patch_bit(void)
{
	// list keeping and per frame drivers
	// NOT hooked: our copy leaves the exe's gAnimTable empty, and the exe's
	// Utils_InitLoadIcons then faults dereferencing gAnimTable[13] (0x0056EA98)
	// at 0x004E5A97 right after the title screen. Spool_AnimAccess is not hooked,
	// so our version of this loop cannot resolve the anims. Needs the spool anim
	// path converted first.
	// PATCH_PUSH_RET(0x00408280, Bit_UpdateQuickAnimLookups);
	PATCH_PUSH_RET(0x004082E0, DeleteBitList);
	PATCH_PUSH_RET(0x00408310, Bit_DeleteAll);
	PATCH_PUSH_RET(0x00408610, Bit_RemoveDeadBits);
	PATCH_PUSH_RET(0x004087E0, RemoveDeadBits);
	PATCH_PUSH_RET(0x00408810, Bit_ReduceRGB);
	PATCH_PUSH_RET(0x0040D630, Bit_ClearTextBoxes);
	PATCH_PUSH_RET(0x00410890, Bit_MakeSpriteRing);
	PATCH_PUSH_RET(0x00411B30, Bit_Move);
	PATCH_PUSH_RET(0x00411CF0, Bit_Display);

	// spark parameters
	PATCH_PUSH_RET(0x0040F3D0, Bit_SetSparkTrajectory);
	PATCH_PUSH_RET(0x0040F3F0, Bit_SetSparkTrajectoryCone);
	PATCH_PUSH_RET(0x0040F410, Bit_SetSparkSize);
	PATCH_PUSH_RET(0x0040F440, Bit_SetSparkRGB);
	PATCH_PUSH_RET(0x0040F460, Bit_SetSparkFadeRGB);

	// CBitServer. Bit_Init itself stays unhooked: it would register our own
	// Display*List functions into gBitServer, and those cannot draw yet.
	PATCH_PUSH_RET(0x00407F70, CBitServer::RegisterSlot);

	// CBit
	PATCH_PUSH_RET_POLY(0x004088A0, CBit::operator new, "??2CBit@@SAPAXI@Z");
	PATCH_PUSH_RET_POLY(0x00408970, CBit::CBit, "??0CBit@@QAE@XZ");
	PATCH_PUSH_RET_POLY(0x004089F0, CBit::~CBit, "??1CBit@@UAE@XZ");

	// CSpecialDisplay. Display is not hooked: the original shares one empty
	// function at 0x4015B0 for it and for CBit::Move, so patching that address
	// would redirect every class that inherits either slot.
	PATCH_PUSH_RET_POLY(0x00408A10, CSpecialDisplay::CSpecialDisplay, "??0CSpecialDisplay@@QAE@XZ");
	PATCH_PUSH_RET_POLY(0x00408AA0, CSpecialDisplay::~CSpecialDisplay, "??1CSpecialDisplay@@UAE@XZ");

	// CNonRenderedBit. 0x410180 is also CFireyExplosion's destructor, the
	// original folded the two.
	PATCH_PUSH_RET_POLY(0x00408B10, CNonRenderedBit::CNonRenderedBit, "??0CNonRenderedBit@@QAE@XZ");
	PATCH_PUSH_RET_POLY(0x00410180, CNonRenderedBit::~CNonRenderedBit, "??1CNonRenderedBit@@UAE@XZ");

	// CFT4Bit
	PATCH_PUSH_RET_POLY(0x00408B80, CFT4Bit::CFT4Bit, "??0CFT4Bit@@QAE@XZ");
	PATCH_PUSH_RET_POLY(0x00408C00, CFT4Bit::~CFT4Bit, "??1CFT4Bit@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x00408D50, CFT4Bit::SetTexture, "?SetTexture@CFT4Bit@@QAEXI@Z");
	// 0x410AE0 is CFT4Bit::IncFrameWithWrap and CRibbonBit::Move at once, the
	// original folded them because CRibbonBit::Move is just the inlined call.
	PATCH_PUSH_RET(0x00410AE0, CFT4Bit::IncFrameWithWrap);

	// CQuadBit setters. The constructor stays unhooked, see the note above.
	PATCH_PUSH_RET_POLY(0x004090E0, CQuadBit::SetTexture, "?SetTexture@CQuadBit@@QAEXHH@Z");
	PATCH_PUSH_RET_POLY(0x00409210, CQuadBit::SetTexture, "?SetTexture@CQuadBit@@QAEXPAUTexture@@@Z");
	PATCH_PUSH_RET_POLY(0x00409240, CQuadBit::SetTexture, "?SetTexture@CQuadBit@@QAEXI@Z");
	PATCH_PUSH_RET(0x004092C0, CQuadBit::SetCorners);
	PATCH_PUSH_RET(0x00409390, CQuadBit::SetSemiTransparent);
	PATCH_PUSH_RET(0x004093A0, CQuadBit::SetSubtractiveTransparency);
	PATCH_PUSH_RET(0x004093B0, CQuadBit::SetOpaque);
	PATCH_PUSH_RET(0x004093C0, CQuadBit::SetTint);
	PATCH_PUSH_RET(0x004093E0, CQuadBit::SetTransparency);
	PATCH_PUSH_RET_POLY(0x00409400, CQuadBit::OrientUsing, "?OrientUsing@CQuadBit@@QAEXPAVCVector@@PAUSVECTOR@@HH@Z");
	PATCH_PUSH_RET_POLY(0x00409560, CQuadBit::OrientUsing, "?OrientUsing@CQuadBit@@QAEXPAVCVector@@PAUSVECTOR@@HHH@Z");

	// CSimpleTexturedRibbon setters. Both constructors and Display stay
	// unhooked, see the note above.
	PATCH_PUSH_RET_POLY(0x0040A510, CSimpleTexturedRibbon::~CSimpleTexturedRibbon, "??1CSimpleTexturedRibbon@@UAE@XZ");
	PATCH_PUSH_RET(0x0040A5B0, CSimpleTexturedRibbon::SetNumFaces);
	PATCH_PUSH_RET(0x0040A690, CSimpleTexturedRibbon::SetSemiTransparent);
	PATCH_PUSH_RET(0x0040A6B0, CSimpleTexturedRibbon::SetOpaque);
	PATCH_PUSH_RET_POLY(0x0040A6D0, CSimpleTexturedRibbon::SetTexture, "?SetTexture@CSimpleTexturedRibbon@@QAEXPAUTexture@@@Z");
	PATCH_PUSH_RET_POLY(0x0040A7B0, CSimpleTexturedRibbon::SetTexture, "?SetTexture@CSimpleTexturedRibbon@@QAEXI@Z");
	PATCH_PUSH_RET_POLY(0x0040A810, CSimpleTexturedRibbon::SetTexturei, "?SetTexturei@CSimpleTexturedRibbon@@QAEXHPAUTexture@@@Z");
	PATCH_PUSH_RET_POLY(0x0040A8D0, CSimpleTexturedRibbon::SetTexturei, "?SetTexturei@CSimpleTexturedRibbon@@QAEXHI@Z");
	PATCH_PUSH_RET(0x0040A920, CSimpleTexturedRibbon::SetRGB);
	PATCH_PUSH_RET(0x0040A970, CSimpleTexturedRibbon::SetWidth);
	PATCH_PUSH_RET(0x0040A9B0, CSimpleTexturedRibbon::SetWidthi);

	// CChunkBit
	PATCH_PUSH_RET_POLY(0x0040B570, CChunkBit::CChunkBit, "??0CChunkBit@@QAE@PAVCSVector@@00@Z");
	PATCH_PUSH_RET_POLY(0x0040B7E0, CChunkBit::~CChunkBit, "??1CChunkBit@@UAE@XZ");
	PATCH_PUSH_RET(0x0040B830, CChunkBit::SetRGB);
	PATCH_PUSH_RET(0x0040B910, CChunkBit::SetUVs);

	// CGlow
	PATCH_PUSH_RET_POLY(0x0040C210, CGlow::CGlow, "??0CGlow@@QAE@II@Z");
	PATCH_PUSH_RET_POLY(0x0040C350, CGlow::CGlow, "??0CGlow@@QAE@PAVCVector@@HHEEEEEE@Z");
	PATCH_PUSH_RET_POLY(0x0040C4F0, CGlow::~CGlow, "??1CGlow@@UAE@XZ");
	PATCH_PUSH_RET(0x0040C580, CGlow::SetRadius);
	PATCH_PUSH_RET(0x0040C5B0, CGlow::SetFringeWidth);
	PATCH_PUSH_RET(0x0040C600, CGlow::SetCentreRGB);
	PATCH_PUSH_RET(0x0040C630, CGlow::SetRGB);
	PATCH_PUSH_RET(0x0040C670, CGlow::SetFringeRGB);

	// CTextBox. Only the destructor, see the note above.
	PATCH_PUSH_RET_POLY(0x0040D720, CTextBox::~CTextBox, "??1CTextBox@@UAE@XZ");

	// CFlatBit
	PATCH_PUSH_RET_POLY(0x0040DA90, CFlatBit::CFlatBit, "??0CFlatBit@@QAE@XZ");
	PATCH_PUSH_RET_POLY(0x0040DB40, CFlatBit::~CFlatBit, "??1CFlatBit@@UAE@XZ");

	// CCombatImpactRing
	PATCH_PUSH_RET_POLY(0x0040E530, CCombatImpactRing::CCombatImpactRing, "??0CCombatImpactRing@@QAE@PAVCVector@@EEEHHH@Z");
	PATCH_PUSH_RET_POLY(0x0040E6A0, CCombatImpactRing::~CCombatImpactRing, "??1CCombatImpactRing@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x0040E730, CCombatImpactRing::Move, "?Move@CCombatImpactRing@@UAEXXZ");

	// CLinked2EndedBit and CRibbonBit. Their constructors have no address of
	// their own in the exe, the original inlined them.
	PATCH_PUSH_RET_POLY(0x0040E7B0, CLinked2EndedBit::~CLinked2EndedBit, "??1CLinked2EndedBit@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x00410A50, CRibbonBit::~CRibbonBit, "??1CRibbonBit@@UAE@XZ");

	// CPixel and CSpark. 0x40F680 is both destructors, the original folded them.
	PATCH_PUSH_RET_POLY(0x0040F080, CPixel::CPixel, "??0CPixel@@QAE@XZ");
	PATCH_PUSH_RET_POLY(0x0040F680, CPixel::~CPixel, "??1CPixel@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x0040F480, CSpark::CSpark, "??0CSpark@@QAE@AAVCVector@@HHH@Z");
	PATCH_PUSH_RET_POLY(0x0040F6D0, CSpark::Move, "?Move@CSpark@@UAEXXZ");

	// CSimpleAnim. Move is already hooked in patch_CFT4Bit.
	PATCH_PUSH_RET_POLY(0x0040F7A0, CSimpleAnim::CSimpleAnim, "??0CSimpleAnim@@QAE@PAVCVector@@HGHHH@Z");
	PATCH_PUSH_RET_POLY(0x0040F8F0, CSimpleAnim::~CSimpleAnim, "??1CSimpleAnim@@UAE@XZ");

	// CMotionBlur
	PATCH_PUSH_RET_POLY(0x0040FA80, CMotionBlur::CMotionBlur, "??0CMotionBlur@@QAE@PAVCVector@@0HHHH@Z");
	PATCH_PUSH_RET_POLY(0x0040FBE0, CMotionBlur::~CMotionBlur, "??1CMotionBlur@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x0040FC70, CMotionBlur::Move, "?Move@CMotionBlur@@UAEXXZ");

	// CFrag
	PATCH_PUSH_RET_POLY(0x0040FDA0, CFrag::CFrag, "??0CFrag@@QAE@PAVCVector@@EEEHGHHHH@Z");
	PATCH_PUSH_RET_POLY(0x0040FF50, CFrag::~CFrag, "??1CFrag@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x0040FFE0, CFrag::Move, "?Move@CFrag@@UAEXXZ");

	// CFireyExplosion. Its destructor shares 0x410180 with CNonRenderedBit.
	PATCH_PUSH_RET_POLY(0x00410090, CFireyExplosion::CFireyExplosion, "??0CFireyExplosion@@QAE@PAVCVector@@@Z");
	PATCH_PUSH_RET_POLY(0x004101D0, CFireyExplosion::Move, "?Move@CFireyExplosion@@UAEXXZ");

	// CWibbly. The constructor stays unhooked, see the note above.
	PATCH_PUSH_RET_POLY(0x004104A0, CWibbly::~CWibbly, "??1CWibbly@@UAE@XZ");
	PATCH_PUSH_RET(0x00410500, CWibbly::SetEndPoints);
	PATCH_PUSH_RET_POLY(0x004105F0, CWibbly::Move, "?Move@CWibbly@@UAEXXZ");

	// CRibbon
	PATCH_PUSH_RET_POLY(0x00410B20, CRibbon::CRibbon, "??0CRibbon@@QAE@PAVCVector@@HHHHHH@Z");
	PATCH_PUSH_RET_POLY(0x00410DC0, CRibbon::~CRibbon, "??1CRibbon@@UAE@XZ");
	PATCH_PUSH_RET(0x00410E80, CRibbon::SetScale);
	PATCH_PUSH_RET(0x00410EB0, CRibbon::SetPos);

	// CSmokeTrail
	PATCH_PUSH_RET_POLY(0x00410F50, CSmokeTrail::CSmokeTrail, "??0CSmokeTrail@@QAE@PAVCVector@@HHHH@Z");
	PATCH_PUSH_RET_POLY(0x00411040, CSmokeTrail::~CSmokeTrail, "??1CSmokeTrail@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x00411100, CSmokeTrail::Move, "?Move@CSmokeTrail@@UAEXXZ");

	// CGlassBit
	PATCH_PUSH_RET_POLY(0x00411160, CGlassBit::CGlassBit, "??0CGlassBit@@QAE@PBVCVector@@0HEEEHHH@Z");
	PATCH_PUSH_RET_POLY(0x004113A0, CGlassBit::~CGlassBit, "??1CGlassBit@@UAE@XZ");
	PATCH_PUSH_RET_POLY(0x004113F0, CGlassBit::Move, "?Move@CGlassBit@@UAEXXZ");

	// CShatterBit lives in this file even though its four functions sit in
	// shatter.cpp's address range (0x48Bxxx). Nothing else hooks them.
	PATCH_PUSH_RET_POLY(0x0048BDC0, CShatterBit::CShatterBit, "??0CShatterBit@@QAE@ABVCSVector@@00ABVCVector@@H@Z");
	PATCH_PUSH_RET_POLY(0x0048BEC0, CShatterBit::~CShatterBit, "??1CShatterBit@@UAE@XZ");
	PATCH_PUSH_RET(0x0048BF20, CShatterBit::SetPos);
	PATCH_PUSH_RET_POLY(0x0048C060, CShatterBit::Move, "?Move@CShatterBit@@UAEXXZ");
}
