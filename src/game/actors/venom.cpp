#include "game/actors/venom.h"
#include "compatibility/validate.h"
#include "core/game_heap.h"
#include "core/utils.h"
#include "engine/audio/ps2lowsfx.h"
#include "engine/world/trig.h"
#include "game/player/web.h"
#include "engine/assets/spool.h"
#include "game/ui/panel.h"
#include "engine/world/trig.h"
#include "game/cinematics/camera.h"
#include "engine/rendering/ps2funcs.h"
#include "compatibility/my_assert.h"
#include "engine/rendering/PCGfx.h"
#include "game/application/SpideyDX.h"
#include "engine/rendering/m3dinit.h"
#include "game/player/spidey.h"



// The ten chase-bar textures. Venom_RelocatableModuleClear (0x4E7D50) zeroes
// exactly ten dwords from 0x6B4EE4 ("mov ecx,0Ah; xor eax,eax; mov
// edi,6B4EE4h; rep stosd") and Venom_DisplayProgressBar (0x4E7E10) copies the
// same ten dwords out of that address into its stack scratch. File local, no
// other .cpp touches it.
#define LEN_VENOM_TEXS 10
#ifndef SPIDEY_STANDALONE
EXPORT Texture* gVenomTexs[LEN_VENOM_TEXS];
#else
extern Texture* gVenomTexs[LEN_VENOM_TEXS];
#endif
//#define G_VENOM_TEXS (gVenomTexs)
#define G_VENOM_TEXS (reinterpret_cast<Texture**>(0x006B4EE4))

// @Ok
// @Matching
CVenom::~CVenom(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&G_BADDY_LIST));
	Panel_DestroyHealthBar();
	this->KillAllCommandBlocks();

	if (Trig_GetLevelID() == 1281)
	{
		Panel_DestroyCompass();
	}

	delete this->field_434;
	delete this->field_428;
	delete this->field_398;
	delete this->field_3E4;
	delete this->field_334;

	if (this->field_390)
	{
		Mem_Delete(this->field_390);
	}
}

// @Ok
// @Matching
void CVenom::ExitWaitState(u32 a2, u32 a3)
{
	this->field_3B4 = 0;
	if (this->field_31C.bothFlags == 1)
	{
		this->field_3B0 = a2;
		if (a2 == 4)
		{
			this->field_3B4 = a3;
		}
	}
}

// @Ok
// @Matching
void CVenom::EnterWaitState(void)
{
	this->field_3B4 = 0;
	if (this->field_31C.bothFlags != 1)
	{
		this->field_31C.bothFlags = 1;
		this->dumbAssPad = 0;
	}
}

// @Ok
// Verified against IDA decompile of 0x4E8990 (??0CVenomHitSpark@@QAE@PBVCVector@@@Z). Same body
// shape as CCarnageHitSpark::CCarnageHitSpark (carnage.cpp, also @Ok): camera-facing normal via
// gte_ldopv1/gte_ldopv2/gte_op0, tangent via gte_ldlvl/gte_sqr0, M3dMaths_SquareRoot0 normalize,
// then three rcossin_tbl-scaled offset vectors (mVel, mPos, mPosD +-mPosC, mPosB); texture/tint/
// type constants at the end (SetTexture(0x877E63C8), SetTint(0xFF,0x80,0), mType=30) all match
// field-for-field, offsets line up with the class layout (VALIDATE_SIZE 0x84, same as
// CCarnageHitSpark). Residue is register-scheduling noise through the long vector-math chain,
// no structural mismatch found. Functional-only bar per session override, not independently
// re-diffed byte-for-byte here.
CVenomHitSpark::CVenomHitSpark(const CVector *pVec)
{
	this->mPosC = *pVec;

	CVector v41;
	v41.vx = 0;
	v41.vy = -4096;
	v41.vz = 0;

	CVector v40;

	v40.vx = G_MIKE_CAMERA[0].Position.vx - (this->mPosC.vx >> 12);
	v40.vy = G_MIKE_CAMERA[0].Position.vy - (this->mPosC.vy >> 12);
	v40.vz = G_MIKE_CAMERA[0].Position.vz - (this->mPosC.vz >> 12);

	gte_ldopv1(reinterpret_cast<VECTOR*>(&v41));
	gte_ldopv2(reinterpret_cast<VECTOR*>(&v40));
	gte_op0();

	gte_stlvnl(reinterpret_cast<VECTOR*>(&v40));

	CVector v39;
	v39.vx = v40.vx >> 8;
	v39.vy = v40.vy >> 8;
	v39.vz = v40.vz >> 8;

	gte_ldlvl(reinterpret_cast<VECTOR*>(&v39));
	gte_sqr0();
	gte_stlvnl(reinterpret_cast<VECTOR*>(&v39));

	i32 v8 = M3dMaths_SquareRoot0(v39.vx + v39.vy + v39.vz);
	v40.vx = 16 * (v40.vx / v8);
	v40.vy = 16 * (v40.vy / v8);
	v40.vz = 16 * (v40.vz / v8);


	i32 v9 = Rnd(4096);
	i32 v10 = Rnd(30);
	i32 v11 = (4 * v9) & 0x3FFC;

	i32 v12 = ((v10 + 5) * G_RCOSSIN_TBL[v11].cos) >> 12;
	i32 v22 = ((v10 + 5) * G_RCOSSIN_TBL[v11].sin) >> 12;

	this->mVel = (v12 * v41) + (v22 * v40);

	i32 v13 = Rnd(50) + 50;
	i32 v14 = (v13 * G_RCOSSIN_TBL[v11].cos) >> 12;
	i32 v20 = (v13 * G_RCOSSIN_TBL[v11].sin) >> 12;

	CVector v33 = (v14 * v41) + (v20 * v40);

	i32 v15 = (4 * (v9 + 1024)) & 0x3FFC;

	i32 v18 = (10 * G_RCOSSIN_TBL[v15].sin) >> 12;
	i32 v19 = (10 * G_RCOSSIN_TBL[v15].cos) >> 12;

	CVector v30 = (v19 * v41) + (v18 * v40);

	this->mPos = this->mPosC + v33;

	if (Rnd(2))
	{
		this->mPosD = this->mPosC + v30;
	}
	else
	{
		this->mPosD = this->mPosC - v30;
	}

	this->mPosB = this->mPosD + v33;


	this->SetTexture(0x877E63C8);
	this->SetSemiTransparent();
	this->SetTint(0xFF, 0x80u, 0);
	this->mType = 30;
}

// @Ok
// @Matching
// @Note: COMDAT should merge this with CarnageHitSpark::Move
void CVenomHitSpark::Move(void)
{
	this->mPos.vx += this->mVel.vx;
	this->mPos.vy += this->mVel.vy;
	this->mPos.vz += this->mVel.vz;

	this->mPosB.vx += this->mVel.vx;
	this->mPosB.vy += this->mVel.vy;
	this->mPosB.vz += this->mVel.vz;

	this->mPosC.vx += this->mVel.vx;
	this->mPosC.vy += this->mVel.vy;
	this->mPosC.vz += this->mVel.vz;

	this->mPosD.vx += this->mVel.vx;
	this->mPosD.vy += this->mVel.vy;
	this->mPosD.vz += this->mVel.vz;

	if (++this->mAge > 0)
		Bit_ReduceRGB(&this->mTint, 30);

	if ((this->mTint & 0xFFFFFF) == 0)
		this->Die();
}

// @Ok
// @Matching
CVenomHitSpark::~CVenomHitSpark(void)
{
}


// @Ok
// @Matching
void CVenom::CreateCombatImpactEffect(CVector *a2,i32)
{
	for (i32 i = 0; i < 16; i++)
	{
		new CVenomHitSpark(a2);
	}
}

// Screen Y-offset added to several HUD bar draws. Same address panel.cpp
// already names gPanelScreenY (0x60F76C); duplicated here file-locally per
// repo convention (CLAUDE.md: static address globals may be duplicated
// across files).
static i32 * const gPanelScreenY = (i32*)0x0060F76C;

// @Bogus
// Not a separate function in the original (sub_4E7E10 repeats this whole
// sequence inline 5 times); factored out here purely for readability, one
// draw of a screen-space textured quad for the Venom chase bar. Common
// tail shared by all 5 draw call sites in Venom_DisplayProgressBar below:
// alloc a POLY_FT4 from renderTex, size/UV it against sizeTex (usually the
// same texture as renderTex; the animated-frame loop is the one exception,
// see the comment there), bind renderTex's clut, then draw the resulting
// quad scaled from native to screen resolution. `tag` is the original's
// trailing PCGfx_DrawQPoly2D float argument (1.0/2.0/3.0/4.0 per call
// site in the disassembly; meaning unknown, reproduced as-is).
static void Venom_DrawBarPiece(Texture *renderTex, Texture *sizeTex, i32 x, i32 y, i32 w, i32 h, f32 tag)
{
	POLY_FT4 *poly = reinterpret_cast<POLY_FT4*>(Panel_DrawTexturedPoly(renderTex, 0));
	Panel_SetStretchedScreenCoords(x, y, poly, sizeTex, w, h);
	PCGfx_UseTexture(renderTex->clut, DCGfx_BlendingMode_1);

	f32 scaleX = G_GAME_RESOLUTION_X / static_cast<f32>(G_XRES);
	f32 scaleY = G_GAME_RESOLUTION_Y / static_cast<f32>(G_YRES);
	u32 color = 0xFF000000u | (static_cast<u32>(poly->r0) << 16) | (static_cast<u32>(poly->g0) << 8) | poly->b0;

	PCGfx_DrawQPoly2D(
		poly->x0 * scaleX, poly->y0 * scaleY, 0.0f, 0.0f, color,
		poly->x1 * scaleX, poly->y1 * scaleY, 1.0f, 0.0f, color,
		poly->x2 * scaleX, poly->y2 * scaleY, 0.0f, 1.0f, color,
		poly->x3 * scaleX, poly->y3 * scaleY, 1.0f, 1.0f, color,
		tag);
}

// @Ok
// Translated from IDA decompile+disasm of sub_4E7E10 (0x4E7E10, 2935 bytes), the Venom "chase"
// HUD bar. Confirmed against the disassembly, not just Hex-Rays output.
//
// Real signature: the original takes ZERO parameters (does not match this stub's declared
// `(const u32*, u32*)`, which comes from the Mac build's prototype/how it's stored in
// reloc_mod::field_C - a generic callback slot cast at each use site). Left the C++ signature
// as-is (matches venom.h/reloc_mod usage); the body ignores both parameters, matching the real
// function.
//
// Globals confirmed by nearest-neighbor audit against idb_globals.txt: dword_54D474 =
// DifficultyLevel, dword_5FAE98 = G_POST_WATER_EFFECT, dword_568158/dword_568154 =
// gGameResolutionY/gGameResolutionX, dword_628614/dword_61B5FC = Yres/Xres (matches the existing
// gGameResolutionX/Y over Xres/Yres screen-scale idiom already used in chopper.cpp), dword_60F76C
// = the same screen Y-offset panel.cpp already names gPanelScreenY, dword_6A9038 = MechList
// (G_MECHLIST in ob.h; used here as the player position, per idb_globals.txt and the existing
// comment in panel.cpp/chopper.cpp/hostage.cpp about this address).
//
// unk_6B4EE4 (10 dwords, zeroed by Venom_RelocatableModuleClear, same size/pattern as G_VENOM_TEXS)
// is very likely the real fixed address of G_VENOM_TEXS, but this function is not being hooked (no
// patch_*() added), so there is no runtime hazard in just using the existing repo array directly
// instead of binding a new fixed-address alias - same conceptual data either way.
//
// dword_6B4DA8 (a scratch texture-pointer slot) is written and read ONLY inside this one function
// (verified via xrefs_to in IDA: all 9 references are inside 0x4E7E10), so it is implemented as a
// local instead of a new persistent global.
//
// The dword_559E48..dword_559E94 block (20 dwords, 5 groups of 4) is a small per-draw-call
// screen-position/size nudge table. Read its actual bytes straight out of the binary via IDA
// (get_bytes at 0x559E48, 80 bytes) instead of guessing: {-1,-5,0,0, 1,-5,0,0, 0,-7,0,0, 0,-5,0,0,
// 0,-5,0,0} (int32 each). Folded directly into the arithmetic below as the confirmed literal
// results (e.g. "446 - barLen" for the Spidey icon's sliding X) rather than kept as an unnamed
// global array, since we now have their real values, not a guess.
//
// The texture-name table at 0x559D08 (10 x 32-byte slots, walked by the original's texture-load
// loop) was also read directly from the binary (get_bytes): "VenomChase_Bar_01".."_06",
// "VenomChase_Bar_LeftEnd", "VenomChase_Bar_RightEnd", "Spidey_Chase", "Venom_Chase" - the same
// order as G_VENOM_TEXS's 10 slots (confirmed by Venom_RelocatableModuleClear zeroing all 10).
//
// Logic: find the venom baddy (FindBaddyOfType(313)) and the player (MechList); if either is
// missing, do nothing (matches the original's early-return). Otherwise: XZ distance from player to
// venom, clamped to a per-difficulty max (hard=7500, easy/normal=9000, other=6000, exact original
// if/else-if chain reproduced even though the difficulty==0 and ==1 cases both land on 9000),
// scaled to a 0..307 bar length. If G_POST_WATER_EFFECT is off, advance the venom's chase-bar
// animation accumulator (field_32C += field_80, CBody's existing per-frame delta field) - nothing
// in this function reads field_32C back, so whatever advances field_328 from it is elsewhere, not
// yet decompiled. Then draw, in original order: the Spidey icon (always, slides along the bar via
// barLen), the Venom icon (only if its texture loaded, fixed position), 19 repeated chase-bar frame
// segments 18px apart (confirmed from the disassembly: every iteration allocates its POLY_FT4 from
// texs[0] and binds texs[0]'s clut, but sizes/UVs it against texs[field_328] - texs[0] is reused as
// the shared texture page/clut for what is presumably one shared texture atlas, while field_328
// only selects which sub-rect of it to show; reproduced literally, not "fixed" to use texs[field_328]
// throughout), then the left and right end caps.
void Venom_DisplayProgressBar(const u32*, u32*)
{
	static const char* const kBarTexNames[LEN_VENOM_TEXS] =
	{
		"VenomChase_Bar_01", "VenomChase_Bar_02", "VenomChase_Bar_03",
		"VenomChase_Bar_04", "VenomChase_Bar_05", "VenomChase_Bar_06",
		"VenomChase_Bar_LeftEnd", "VenomChase_Bar_RightEnd",
		"Spidey_Chase", "Venom_Chase",
	};

	// Local copy of G_VENOM_TEXS; any missing entry is loaded here but (matching the original) not
	// written back to G_VENOM_TEXS itself.
	Texture *texs[LEN_VENOM_TEXS];
	i32 i;
	for (i = 0; i < LEN_VENOM_TEXS; i++)
	{
		texs[i] = G_VENOM_TEXS[i];
		if (!texs[i])
			texs[i] = Spool_FindTextureEntry(const_cast<char*>(kBarTexNames[i]));
		print_if_false(texs[i] != 0, "No texture");
	}

	CVenom *pVenom = reinterpret_cast<CVenom*>(FindBaddyOfType(313));
	if (!G_MECHLIST_PLAYER || !pVenom)
		return;

	i32 dist = Utils_XZDist(&G_MECHLIST_PLAYER->mPos, &pVenom->mPos);

	i32 maxDist;
	if (DifficultyLevel == 2)
		maxDist = 7500;
	else if (DifficultyLevel == 1)
		maxDist = 9000;
	else if (DifficultyLevel == 0)
		maxDist = 9000;
	else
		maxDist = 6000;

	if (dist > maxDist)
		dist = maxDist;

	i32 barLen = 307 * dist / maxDist;

	if (!G_POST_WATER_EFFECT)
		pVenom->field_32C += pVenom->mElapsedTicks;

	// Spidey icon: slides toward Venom's fixed icon as the distance closes.
	Venom_DrawBarPiece(texs[8], texs[8], 446 - barLen, *gPanelScreenY + 11, 20, 26, 1.0f);

	// Venom icon: fixed position, only drawn if its texture actually loaded.
	if (texs[9])
		Venom_DrawBarPiece(texs[9], texs[9], 446, *gPanelScreenY + 11, 20, 26, 2.0f);

	// 19 chase-bar frame segments, 18px apart, spanning the bar's 342px width.
	for (i = 0; i < 342; i += 18)
		Venom_DrawBarPiece(texs[0], texs[pVenom->field_328], i + 138, *gPanelScreenY + 21, 12, 10, 4.0f);

	// Left/right end caps.
	Venom_DrawBarPiece(texs[6], texs[6], 132, *gPanelScreenY + 23, 4, 8, 3.0f);
	Venom_DrawBarPiece(texs[7], texs[7], 481, *gPanelScreenY + 23, 4, 8, 3.0f);
}

// @Ok
// @Matching
void Venom_RelocatableModuleClear(void)
{
	CItem *pSearch = G_BADDY_LIST;

	while (pSearch)
	{
		CItem *pNext = pSearch->mNextItem;

		if (pSearch->mType == 313)
			delete pSearch;

		pSearch = pNext;
	}

	for (i32 i = 0; i < LEN_VENOM_TEXS; i++)
	{
		G_VENOM_TEXS[i] = 0;
	}
}

// @Ok
// @Matching
void Venom_RelocatableModuleInit(reloc_mod *pMod)
{
	pMod->mClearFunc = Venom_RelocatableModuleClear;
	pMod->field_C[0] = Venom_CreateVenom;
	pMod->field_C[1] = Venom_DisplayProgressBar;
}

// @Ok
 i32* CVenom::GetNewCommandBlock(u32 a1)
{
	i32* res = static_cast<i32*>(DCMem_New(4 * a1, 0, 1, 0, 1));
	res[a1 - 1] = 0;

	if (!this->field_35C)
	{
		this->field_35C = res;
	}
	else
	{
		i32* it = this->field_35C;
		while (1)
		{
			if (!it[it[1] - 1])
				break;

			it = reinterpret_cast<i32*>(it[it[1] - 1]);
		}

		it[it[1] - 1] = reinterpret_cast<i32>(res);
	}

	return res;
}

// @Ok
// @Matching
// Node type 1002 (ID_SWITCH_TO) with subtype 4 marks a "go to switch" node. For each
// one found, walk its link chain to the final target node, find the position, then
// find the nearest CSwitch (mType 407) in G_CONTROL_BADDY_LIST to that position.
void CVenom::ResolveSwitchNodes(void)
{
	this->field_3CC = 0;

	for (i32 i = 1; i < NumNodes; i++)
	{
		i16 *node = G_OFFSETLIST[i];

		if (node[0] == 1002 && node[1] == 4)
		{
			print_if_false(this->field_3CC < 4, "More than 4 ID_SWITCH_TO nodes found");

			i32 linkIndex = i;
			this->field_3BC[this->field_3CC] = linkIndex;

			u16 *links = Trig_GetLinksPointer(linkIndex);

			if (links[0] != 0)
			{
				do
				{
					print_if_false(links[0] == 1, "More than 1 linked node for GointToSwitches path");
					linkIndex = links[1];
					links = Trig_GetLinksPointer(linkIndex);
				} while (links[0] != 0);
			}

			CVector v3;
			v3.vx = 0;
			v3.vy = 0;
			v3.vz = 0;
			Trig_GetPosition(&v3, linkIndex);

			u32 bestDist = 0xFFFFFFFF;

			for (CItem *cur = G_CONTROL_BADDY_LIST; cur; cur = reinterpret_cast<CItem*>(cur->mNextItem))
			{
				if (cur->mType == 407)
				{
					u32 dist = Utils_Dist(v3, cur->mPos);

					if (dist < bestDist)
					{
						bestDist = dist;
						this->field_3D0[this->field_3CC] = cur;
					}
				}
			}

			print_if_false(bestDist < 0xFFFFFFFF, "No switch found");

			this->field_3CC++;
		}
	}

	this->field_3B8 = (1 << this->field_3CC) - 1;
}

EXPORT SLight M3d_VenomLight =
{
	{ { -2430, -2228, -2430 }, { 2509, -2896, 1447 }, { -648, -3711, -1607 } },
	0,
	{ { 3200, 1040, 2048 }, { 2720, 1600, 1920 }, { 2400, 2560, 2048 } },
	0,
	{ 1200, 1200, 960 }
};

// duplicate of the byte right before gWhatIf (0x60CFC4, ob.cpp). Name from
// baddy.cpp's gSubmarinerDieRelated. Tentative, static per file per repo convention.
static u8 * const gSubmarinerDieRelated = (u8*)0x60CFC4;

// @Ok
// @AlmostMatching: 2 mnemonic diffs (one store/push swap around the SquirtPos call setup,
// field_3EC store vs the SquirtPos arg push exchange position). Everything else, including
// every level-ID branch and the difficulty-level ternaries, matches exactly. 15 hypotheses
// tried, see ~/Documents/spidey-work/wt/CVenom_CVenom.attempts.md.
CVenom::CVenom(i32 *a2, i32)
{
	this->field_37C = 0;
	this->field_380 = 0;
	this->field_384 = 0;
	this->field_3A0 = 0;
	this->field_3A4 = 0;
	this->field_3A8 = 0;
	this->field_3E8 = 0;
	this->field_3EC = 0;
	this->field_3F0 = 0;
	this->field_400 = 0;
	this->field_404 = 0;
	this->field_408 = 0;
	this->field_40C = 0;
	this->field_410 = 0;
	this->field_414 = 0;
	this->field_418 = 0;
	this->field_41C = 0;
	this->field_420 = 0;

	i16 *pos = this->SquirtPos(reinterpret_cast<i16*>(a2));
	i16 *angles = this->SquirtAngles(pos);

	this->field_454 = 0xFF;
	this->mRGB = 0xFFFFFF;
	this->field_33D = 1;

	u32 levelId = Trig_GetLevelID();

	switch (levelId)
	{
		case 0x9904:
		case 0x601:
		case 0x602:
		case 0x501:
		case 0x503:
		case 0x504:
		case 0x505:
		case 0x506:
			this->mCBodyFlags &= ~0x10;
			this->mRMinor = 0;
			this->InitItem("venom2");
			break;

		default:
			this->mRMinor = 0xC0;
			this->InitItem("venom");
			break;
	}

	this->mType = 313;
	this->AttachTo(reinterpret_cast<CBody**>(&G_BADDY_LIST));
	this->mFlags |= 0x480;

	this->mpLight = &M3d_VenomLight;
	this->field_21E = 100;
	this->field_31C.bothFlags = 1;
	this->dumbAssPad = 0;

	i32 groundY = Utils_GetGroundHeight(&this->mPos, 0, 0x1000, 0);
	if (groundY != -1)
	{
		this->mPos.vy = groundY - (this->field_21E << 12);
	}

	this->OutlineOn();
	this->OutlineOff();

	switch (levelId)
	{
		case 0x603:
		{
			this->mHealth = 0x258;
			this->field_460 = 0xF0;
			this->field_464 = 0x2D0;

			i32 v603 = (G_DIFFICULTY_LEVEL == 1 || G_DIFFICULTY_LEVEL == 0) ? 0x3C : 0x14;
			this->mFlags |= 0x41;
			this->field_39C = v603;
			this->field_45C = 0;
			this->dumbAssPad = 1;
			break;
		}

		case 0x604:
		case 0x601:
		case 0x602:
		case 0x501:
		case 0x502:
		case 0x503:
		case 0x504:
		case 0x505:
		case 0x506:
			this->field_31C.bothFlags = 0x4000;
			this->dumbAssPad = 0;
			this->field_358 = reinterpret_cast<i32>(angles);

			if (levelId == 0x501)
			{
				this->field_218 |= 0x8000;
			}
			else if (levelId == 0x502)
			{
				this->mHealth = 0x258;
				this->field_460 = 0x78;
				this->field_464 = 0x168;

				this->field_39C = (G_DIFFICULTY_LEVEL == 1 || G_DIFFICULTY_LEVEL == 0) ? 0x3C : 0x14;
				Panel_CreateHealthBar(this, 313);
			}
			else if (levelId == 0x604)
			{
				this->mHealth = 0x258;
				this->field_460 = 0xF0;
				this->field_464 = 0x2D0;

				this->field_39C = (G_DIFFICULTY_LEVEL == 1 || G_DIFFICULTY_LEVEL == 0) ? 0x46 : 0x32;
				this->field_33C = 1;
				Panel_CreateHealthBar(this, 313);
				this->ResolveSwitchNodes();
			}
			break;
	}

	if (*gSubmarinerDieRelated)
	{
		print_if_false(0, "Error");
	}
}

// @Ok
void Venom_CreateVenom(const unsigned int *stack, unsigned int *result) {
	int* v2 = reinterpret_cast<int*>(*stack);
	int v3 = static_cast<int>(stack[1]);

	*result = reinterpret_cast<unsigned int>(new CVenom(v2, v3));
}

// @Ok
void CVenom::Shouldnt_DoPhysics_Be_Virtual(void)
{
	this->DoPhysics();
}

// @Ok
// @AlmostMatching: 18 mnemonic diffs in two small reordering clusters (G_MECHLIST address
// computation order in the field_218&4 case; mAngAcc.vy read timing in the angular velocity
// decay step). Everything else, including all three aim branches and the physics integration
// loop, matches exactly. 15 hypotheses tried, see
// ~/Documents/spidey-work/wt/CVenom_DoPhysics.attempts.md.
void CVenom::DoPhysics(void)
{
	if (!this->field_34D)
	{
		i32 flags = this->field_218;

		if (flags & 1)
		{
			if ((this->mAnim != 5 && this->mAnim != 4) || this->mAnimFinished)
			{
				this->RunAnim(5, 0, -1);
			}

			CSVector aimAngles;
			aimAngles.vx = 0;
			aimAngles.vy = 0;
			aimAngles.vz = 0;
			Utils_CalcAim(&aimAngles, &this->mPos, &this->field_240);

			i32 savedVx = aimAngles.vx;
			aimAngles.vx = 0;
			Utils_TurnTowards(this->mAngles, &this->mAngVel, &this->mAngAcc, aimAngles, 10);
			aimAngles.vx = savedVx;

			i32 velY = this->mAngVel.vy;
			i32 signMask = velY >> 31;
			i32 absVelY = (signMask ^ velY) - signMask;

			i32 mag;
			if (absVelY >= 0x40)
			{
				mag = 0;
			}
			else
			{
				mag = (0x40 - absVelY) << 6;
			}
			mag = (static_cast<u32>(mag) * 14) >> 12;

			Utils_GetVecFromMagDir(&this->mVel, mag, &aimAngles);
		}
		else if (flags & 2)
		{
			CSVector aimAngles;
			aimAngles.vx = 0;
			aimAngles.vy = 0;
			aimAngles.vz = 0;
			Utils_CalcAim(&aimAngles, &this->mPos, reinterpret_cast<CVector*>(&this->field_3A0));

			aimAngles.vx = 0;
			Utils_TurnTowards(this->mAngles, &this->mAngVel, &this->mAngAcc, aimAngles, 8);
		}
		else if (flags & 4)
		{
			CSVector aimAngles;
			aimAngles.vx = 0;
			aimAngles.vy = 0;
			aimAngles.vz = 0;
			Utils_CalcAim(&aimAngles, &this->mPos, &G_MECHLIST->mPos);

			aimAngles.vx = 0;
			Utils_TurnTowards(this->mAngles, &this->mAngVel, &this->mAngAcc, aimAngles, 8);
		}
		else
		{
			this->mAngVel.vx = 0;
			this->mAngVel.vy = 0;
			this->mAngVel.vz = 0;
			this->mAngAcc.vx = 0;
			this->mAngAcc.vy = 0;
			this->mAngAcc.vz = 0;
		}
	}

	this->mAngVel.vx += this->mAngAcc.vx;
	this->mAngVel.vx -= this->mAngVel.vx >> 2;
	this->mAngVel.vy += this->mAngAcc.vy;
	this->mAngVel.vy -= this->mAngVel.vy >> 2;

	this->mAngVel.KillSmall();

	for (i32 i = 0; i < this->mElapsedTicks; i++)
	{
		this->mPos += this->mVel;
		this->mAngles += this->mAngVel;
	}

	this->mAngles.Mask();
}

// @Ok
void CVenomWrap::Die(void)
{
	CBit::Die();
}

// @Ok
// @Matching
 i32* CVenom::KillCommandBlock(i32* a1)
{
	i32* res = reinterpret_cast<i32*>(a1[a1[1]-1]);

	if (this->field_35C == a1)
	{
		this->field_35C = res;
	}
	else
	{
		i32* it = this->field_35C;

		while (it)
		{
			if (a1 == reinterpret_cast<i32*>(it[it[1]-1]))
			{
				it[it[1]-1] = reinterpret_cast<i32>(res);
				break;
			}

			it = reinterpret_cast<i32*>(it[it[1]-1]);
		}
	}

	Mem_Delete(reinterpret_cast<void*>(a1));
	return res;
}

// @Ok
// @Matching
 void CVenom::KillAllCommandBlocks(void)
{
	for (i32* cur = this->field_35C; cur; cur = this->KillCommandBlock(cur))
		;
	this->field_35C = 0;
}

// @Ok
unsigned char CVenom::TugImpulse(CVector *a2, CVector *a3, CVector *a4)
{
	if (a4)
	{
		Mem_Delete(a4);
	}
	this->field_218 |= 0x200;

	return 0;
}

// @Ok
CVenomElectrified::CVenomElectrified(CSuper* pSuper)
{
	print_if_false(pSuper != 0, "NULL pSuper sent to CVenomWrap");
	print_if_false((pSuper->mType == 313), "Non venom sent to CVenomElectrified");

	this->field_3C = Mem_MakeHandle(pSuper);
}

// Real address 0x6B4E58: confirmed via IDA disasm of the inlined body in CVenom_AI (0x4EC040,
// around 0x4EC135-0x4EC178, a "play footstep on this anim frame, once per frame" check). Not in
// the maintainer's IDB globals list yet. Tentative name, address is confirmed by evidence above.
static i32 * const gVenomFootstepRelated = (i32*)0x6B4E58;

// @Ok
// Verified against IDA decompile+disasm of the inlined body in CVenom_AI (0x4EC040, around
// 0x4EC135-0x4EC178): Rnd(4)+245 re-rolled in a loop until it differs from gVenomFootstepRelated,
// stored back, then SFX_PlayPos(i|0x8000, &this->mPos, 0). CVenom_AI itself is not decompiled in
// this file yet (separate, much larger function), this only validates PlayNextFootstepSFX's own
// body, which the original also compiles as a real out-of-line function on the Mac build
// (PlayNextFootstepSFX__6CVenomFv, 100 bytes, per prototypes.json).
void CVenom::PlayNextFootstepSFX(void)
{
	i32 i;
	for (i = Rnd(4) + 245; i == *gVenomFootstepRelated; i = Rnd(4) + 245)
		;

	*gVenomFootstepRelated = i;
	SFX_PlayPos(i | 0x8000, &this->mPos, 0);
}

// @Ok
// Verified against IDA decompile+disasm of the inlined body in CVenom::ScanNodesForJumpTarget
// (0x4ECC60, around 0x4ECCDB-0x4ECD07): Trig_GetPosition into a zeroed CVector, then
// Utils_GetGroundHeight(pVector, 0, 0x2000, 0); on success (result != -1) it writes
// result - (this->field_21E << 12) into pVector->vy and returns 1, otherwise leaves the vector
// alone and returns 0. field_21E is CBaddy's i16 at 0x21E (baddy.h), matches the disasm's
// *(__int16*)(this+542)<<12. ScanNodesForJumpTarget itself is not decompiled in this file yet
// (separate function), this only validates GetTargetPosFromNode's own body, which the original
// also compiles as a real out-of-line function on the Mac build (124 bytes per prototypes.json).
i32 CVenom::GetTargetPosFromNode(CVector *pVector, i32 a3)
{
	Trig_GetPosition(pVector, a3);

	i32 v5 = Utils_GetGroundHeight(pVector, 0, 0x2000, 0);
	if (v5 == -1)
	{
		return 0;
	}

	pVector->vy = v5 - (this->field_21E << 12);
	return 1;
}

// @Ok
void CVenom::Lookaround(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_218 &= 0xFFFFFFF8;
			this->mVel.vx = 0;
			this->mVel.vy = 0;
			this->mVel.vz = 0;
			this->RunAnim(8, 0, -1);
			this->dumbAssPad++;
			break;
		case 1:
			if (this->mAnimFinished)
			{
				this->field_31C.bothFlags = 32;
				this->dumbAssPad = 0;
			}
			break;
		default:
			print_if_false(0, "Unknown substate");
			break;
	}
}

// @Ok
void CVenom::TugWeb(void)
{
	switch (this->dumbAssPad)
	{
		case 0:
			this->field_218 &= 0xFFFFFFF8;
			this->mVel.vz = 0;
			this->mVel.vy = 0;
			this->mVel.vx = 0;
			this->dumbAssPad++;
		case 1:
			if (this->field_218 & 0x200)
			{
				this->field_218 &= 0xFFFFFDFF;
				if (this->field_10C.pWhatever)
				{
					CTrapWebEffect* pWeb = reinterpret_cast<CTrapWebEffect*>(
							Mem_RecoverPointer(&this->field_10C));

					if (pWeb)
						pWeb->Burst();

					this->field_10C.pWhatever = 0;
				}

				this->field_31C.bothFlags = 32;
				this->dumbAssPad = 0;
			}
			break;
		default:
			break;
	}
}

// @Ok
void CVenom::AdjustWaterModel(void)
{
	//SHandle *pHandle = &this->field_340;
	CBody* pBody = reinterpret_cast<CBody*>(
			Mem_RecoverPointer(&this->field_340));

	if (!pBody)
	{
		u32 Model = Spool_GetModel(0x26D2DBB7, G_OBJ_FILE_REGION);

		for (pBody = G_ENVIRONMENTAL_OBJECT_LIST;
				pBody;
				pBody = reinterpret_cast<CBody*>(pBody->mNextItem))
		{
			if (pBody->mRegion == G_OBJ_FILE_REGION && pBody->mModel == Model)
			{
				this->field_340 = Mem_MakeHandle(pBody);
				this->field_348 = pBody->mPos.vy;
				break;
			}
		}
	}

	if (pBody)
	{
		pBody->mPos.vy = this->field_348 - 18 * this->field_338;
	}

}

// @Ok
// @Matching
void CVenom::PulseL6A4Node(bool a2)
{
	CVector v3;

	for (i32 i = 1; i < NumNodes; i++)
	{
		if (*G_OFFSETLIST[i] == 1)
		{
			Trig_GetPosition(&v3, i);

			if (a2)
			{
				if (!(v3.vz | v3.vy | v3.vx))
				{
					Trig_SendPulseToNode(i);
					return;
				}
			}
			else
			{
				if (v3.vx == 0x3E8000 && !(v3.vz | v3.vy))
				{
					Trig_SendPulseToNode(i);
					return;
				}
			}
		}
	}

	DoAssert(0, "Node not found");
}


// @Ok
void CVenom::VenomDie(void)
{
	switch (this->dumbAssPad)
	{

		case 0:
			this->field_330 = 16;
			this->field_218 &= 0xFFFFFFF8;
			this->mVel.vx = 0;
			this->mVel.vy = 0;
			this->mVel.vz = 0;

			this->mCBodyFlags &= 0xFFEF;
			this->mFlags &= 0xFFBE;
			this->field_218 &= 0xFFFFFE7F;
			this->dumbAssPad++;
			break;
		case 1:
			this->field_330 = 16;
			if (this->mAnimFinished)
			{
				this->RunAnim(0x29, 0, -1);
				this->dumbAssPad++;
			}

			break;
		case 2:
			if (this->mAnimFinished)
			{
				if (Trig_GetLevelID() == 1540)
				{
					this->PulseL6A4Node(false);
					this->dumbAssPad++;
				}
				else
				{
					this->Die(0);
				}
			}
			break;
	}
}

void validate_CVenom(void){
	VALIDATE_SIZE(CVenom, 0x468);

	VALIDATE(CVenom, field_328, 0x328);
	VALIDATE(CVenom, field_32C, 0x32C);

	VALIDATE(CVenom, field_330, 0x330);
	VALIDATE(CVenom, field_334, 0x334);
	VALIDATE(CVenom, field_338, 0x338);

	VALIDATE(CVenom, field_33C, 0x33C);
	VALIDATE(CVenom, field_33D, 0x33D);

	VALIDATE(CVenom, field_340, 0x340);
	VALIDATE(CVenom, field_348, 0x348);
	VALIDATE(CVenom, field_34D, 0x34D);

	VALIDATE(CVenom, field_358, 0x358);
	VALIDATE(CVenom, field_35C, 0x35C);

	VALIDATE(CVenom, field_37C, 0x37C);
	VALIDATE(CVenom, field_380, 0x380);
	VALIDATE(CVenom, field_384, 0x384);
	VALIDATE(CVenom, field_388, 0x388);

	VALIDATE(CVenom, field_390, 0x390);

	VALIDATE(CVenom, field_398, 0x398);
	VALIDATE(CVenom, field_39C, 0x39C);

	VALIDATE(CVenom, field_3A0, 0x3A0);
	VALIDATE(CVenom, field_3A4, 0x3A4);
	VALIDATE(CVenom, field_3A8, 0x3A8);

	VALIDATE(CVenom, field_3B0, 0x3B0);
	VALIDATE(CVenom, field_3B4, 0x3B4);

	VALIDATE(CVenom, field_3B8, 0x3B8);
	VALIDATE(CVenom, field_3BC, 0x3BC);
	VALIDATE(CVenom, field_3CC, 0x3CC);
	VALIDATE(CVenom, field_3D0, 0x3D0);

	VALIDATE(CVenom, field_3E4, 0x3E4);

	VALIDATE(CVenom, field_3E8, 0x3E8);
	VALIDATE(CVenom, field_3EC, 0x3EC);
	VALIDATE(CVenom, field_3F0, 0x3F0);
	VALIDATE(CVenom, field_400, 0x400);
	VALIDATE(CVenom, field_404, 0x404);
	VALIDATE(CVenom, field_408, 0x408);
	VALIDATE(CVenom, field_40C, 0x40C);
	VALIDATE(CVenom, field_410, 0x410);
	VALIDATE(CVenom, field_414, 0x414);
	VALIDATE(CVenom, field_418, 0x418);
	VALIDATE(CVenom, field_41C, 0x41C);
	VALIDATE(CVenom, field_420, 0x420);

	VALIDATE(CVenom, field_428, 0x428);

	VALIDATE(CVenom, field_430, 0x430);
	VALIDATE(CVenom, field_434, 0x434);


	VALIDATE(CVenom, field_454, 0x454);
	VALIDATE(CVenom, field_458, 0x458);
	VALIDATE(CVenom, field_45C, 0x45C);
	VALIDATE(CVenom, field_460, 0x460);
	VALIDATE(CVenom, field_464, 0x464);

	VALIDATE_VTABLE(CVenom, EnterWaitState, 17);
	VALIDATE_VTABLE(CVenom, ExitWaitState, 18);
}

void validate_CVenomWrap(void)
{
	VALIDATE_SIZE(CVenomWrap, 0x5C);
}

void validate_CVenomElectrified(void)
{
	VALIDATE_SIZE(CVenomElectrified, 0x48);

	VALIDATE(CVenomElectrified, field_3C, 0x3C);
}

void validate_CVenomHitSpark(void)
{
	VALIDATE_SIZE(CVenomHitSpark, 0x84);
}

#include "compatibility/my_patch.h"

// @Bogus
void patch_venom(void)
{
	// Not hooked, and why:
	//
	// CVenom::CVenom (0x4E9600) and Venom_CreateVenom (0x4E7D90, which is
	// just "new CVenom"). The exe's CVenom vtable (0x53C6B8, 19 slots) has
	// CVenom_AI (0x4EC040) in slot 2 and CVenom_Hit (0x4EB7F0) in slot 3.
	// Our CVenom declares neither, so hooking the constructor would stamp a
	// vtable where both fall back to CBaddy and the boss would stop
	// thinking and stop reacting to hits. Venom_RelocatableModuleInit
	// (0x4E7D30) is skipped for the same reason: all it does is install
	// Venom_CreateVenom's address into the reloc table.
	//
	// CVenom::Shouldnt_DoPhysics_Be_Virtual is the 5 byte jump thunk at
	// 0x4EBDC0, too short for a 6 byte hook (and patching it would run into
	// CVenom_DoPhysics at 0x4EBDD0, which is hooked below anyway).
	//
	// CVenomHitSpark::Move shares its body with CCarnageHitSpark::Move
	// (0x41A240, link folded); carnage.cpp already hooks that address.
	//
	// CVenomWrap::Die, CVenomElectrified::CVenomElectrified,
	// CVenom::PlayNextFootstepSFX and CVenom::GetTargetPosFromNode have no
	// address of their own in the PC build, the compiler inlined them into
	// their callers (CVenom_AI and CVenom::ScanNodesForJumpTarget).
	PATCH_PUSH_RET(0x004E7D50, Venom_RelocatableModuleClear);
	PATCH_PUSH_RET(0x004E7E10, Venom_DisplayProgressBar);

	PATCH_PUSH_RET_POLY(0x004E8990, CVenomHitSpark::CVenomHitSpark, "??0CVenomHitSpark@@QAE@PBVCVector@@@Z");
	PATCH_PUSH_RET_POLY(0x004E8D60, CVenomHitSpark::~CVenomHitSpark, "??1CVenomHitSpark@@UAE@XZ");

	PATCH_PUSH_RET_POLY(0x004E9960, CVenom::~CVenom, "??1CVenom@@UAE@XZ");

	PATCH_PUSH_RET(0x004EB690, CVenom::ResolveSwitchNodes);
	PATCH_PUSH_RET(0x004EBDD0, CVenom::DoPhysics);

	PATCH_PUSH_RET_POLY(0x004ECED0, CVenom::EnterWaitState, "?EnterWaitState@CVenom@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x004ECF00, CVenom::ExitWaitState, "?ExitWaitState@CVenom@@UAEXII@Z");

	PATCH_PUSH_RET(0x004ED330, CVenom::VenomDie);

	PATCH_PUSH_RET_POLY(0x004EF6A0, CVenom::TugImpulse, "?TugImpulse@CVenom@@UAEEPAVCVector@@00@Z");
	PATCH_PUSH_RET_POLY(0x004F0E90, CVenom::CreateCombatImpactEffect, "?CreateCombatImpactEffect@CVenom@@UAEXPAVCVector@@H@Z");

	PATCH_PUSH_RET(0x004F0F00, CVenom::PulseL6A4Node);
	PATCH_PUSH_RET(0x004F0FB0, CVenom::AdjustWaterModel);
}
