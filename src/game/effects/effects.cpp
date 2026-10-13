#include "game/effects/effects.h"
#include "engine/assets/spool.h"
#include "core/utils.h"
#include "compatibility/my_assert.h"
#include "core/game_heap.h"
#include "engine/rendering/ps2funcs.h"
#include "engine/world/trig.h"
#include "engine/rendering/m3dutils.h"
#include "game/cinematics/camera.h"
#include "game/effects/exp.h"
#include "game/player/web.h"

#include "compatibility/validate.h"

// dword_56EA9C: a pointer (set up by the game, not by this repo) to a
// table of effect Texture* entries, indexed by byte offset (stride 4).
// Same table simby.cpp's CEmber::CEmber reads via a raw address cast at
// offset 44 (0x56EA9C+44, "same pattern as CSimbyDroplet's texture lookup
// at 0x56EAC4"). Confirmed via IDA decompile of CBouncingRock::
// CBouncingRock (0x43B550) and several other effect constructors
// (CSymbioteBladeWallSplat, CSymbioteBladeSplat, CSymbioteBladeSpark,
// CLizSpitSplat, CEmber, CSmokeJet) that all read *(Texture**)(dword_56EA9C
// + offset). Needs one extra dereference the old plain-i32 declaration was
// missing (gTextureRelated held its own uninitialized value, not the
// game's pointer at 0x56EA9C).
#define G_TEXTURE_RELATED (*reinterpret_cast<i32*>(0x56EA9C))

// per-vertex wobble state for CVertexWobble, 22 bytes. tentative layout from
// CVertexWobble::CVertexWobble and CVertexWobble::Move.
struct SVertexWobbleEntry
{
	i16 vx, vy, vz;   // snapshot of the vertex position at construction time
	i16 dx, dy, dz;   // delta from the average centre at construction time
	u8 vertexIndex;   // index into the model's own vertex table
	u8 field_0D;
	i16 distance;     // sqrt(dx*dx+dy*dy+dz*dz) at construction time
	u16 amplitude;
	i16 phaseSpeed;
	i16 phase;
};

// @Ok
// Functional decompile (session-wide bar 2026-08-30: correctness, not byte
// match). Field mapping and semantics verified against the disasm: a1 is
// stored at offset 0x6A (right after CElectro's own validated size), a2/a3
// are angle indices for Trig_GetPosition giving the line's start/end
// points, a4-a6 are RGB, a7/a8 map to Setup's width/extra (u16), a9 is the
// field_68 slot, a10/a11/a12 map to Setup's NumFaces/NumTextures/
// pChecksums. The point arrays (field_54, a CVector per face+1, and
// field_44, a SSimpleRibbonParams per face+1 whose first 12 bytes overlap a
// CVector) get linearly interpolated from start to end, step = (end-start)
// / NumFaces. Remaining byte residue (49/154 mnemonic diffs) is entirely
// downstream of the known repo-wide issue in CLAUDE.md: vector.h's
// operator-(CVector,CVector) is INLINE but the original calls it out of
// line at 0x4E7760; not a functional bug.
CElectroLine::CElectroLine(u16 a1, u16 a2, u16 a3, u8 a4, u8 a5, u8 a6, i32 a7, i32 a8, i32 a9, i32 a10, i32 a11, u32* a12)
{
	this->field_6A = a1;
	this->mType = 9;

	this->Setup(a10, a11, a12, a4, a5, a6, static_cast<u16>(a7), static_cast<u16>(a8));

	this->field_68 = static_cast<u16>(a9);

	CVector start;
	CVector end;
	Trig_GetPosition(&start, a2);
	Trig_GetPosition(&end, a3);

	CVector *points = reinterpret_cast<CVector*>(this->field_54);
	SSimpleRibbonParams *params = this->field_44;

	points[0] = start;
	*reinterpret_cast<CVector*>(&params[0]) = start;

	CVector step = (end - start) / a10;
	CVector pos = start;

	for (i32 i = 0; i < a10 - 1; i++)
	{
		pos += step;
		points[i + 1] = pos;
		*reinterpret_cast<CVector*>(&params[i + 1]) = pos;
	}

	points[a10] = end;
	*reinterpret_cast<CVector*>(&params[a10]) = end;
}

// @Ok
// Functional decompile (session-wide bar 2026-08-30: correctness, not byte
// match). Globals (G_PSXREGION) and field semantics worked out from the
// disasm, see effects.attempts.md. Cross-checked against CVertexWobble::
// Move, which consumes every field this constructor fills. Remaining byte
// residue (85/193 mnemonic diffs) is register allocation and loop-pointer
// scheduling only (ebx/ebp swap, pointer-advance-early shape); not a
// functional bug.
CVertexWobble::CVertexWobble(u32 a1, u32 a2, u32 a3, u8* a4, i32 a5, i32 a6, i32 a7, i32 a8)
{
	print_if_false(a1 < static_cast<u32>(MAXPSX), "Region out of range");
	print_if_false(G_PSXREGION[a1].Usable != 0, "PSX not usable");

	SHandle handle = Mem_MakeHandle(G_PSXREGION[a1].pPSX);
	this->field_3C = handle.pWhatever;
	this->field_40 = handle.Id;

	print_if_false(a2 < reinterpret_cast<u32*>(G_PSXREGION[a1].ppModels)[-1], "Model index out of range");

	this->field_4C = G_PSXREGION[a1].ppModels[a2];

	print_if_false(a3 != 0, "Zero NumVerts");
	print_if_false(a4 != 0, "NULL vertex list");

	u32 i;
	for (i = 0; i < a3; i++)
		print_if_false(a4[i] < *reinterpret_cast<u16*>(reinterpret_cast<u8*>(this->field_4C) + 2), "Vertex index out of range");

	this->field_50 = a3;
	this->field_54 = DCMem_New(a3 * sizeof(SVertexWobbleEntry), 0, 1, 0, 1);

	this->field_58.vx = 0;
	this->field_58.vy = 0;
	this->field_58.vz = 0;

	SVertexWobbleEntry *entries = reinterpret_cast<SVertexWobbleEntry*>(this->field_54);

	for (i = 0; i < a3; i++)
	{
		SVertexWobbleEntry *entry = &entries[i];
		entry->vertexIndex = a4[i];

		i16 *vertex = reinterpret_cast<i16*>(reinterpret_cast<u8*>(this->field_4C) + 0x1C + entry->vertexIndex * 8);
		entry->vx = vertex[0];
		entry->vy = vertex[1];
		entry->vz = vertex[2];

		this->field_58.vx += entry->vx;
		this->field_58.vy += entry->vy;
		this->field_58.vz += entry->vz;

		entry->amplitude = static_cast<u16>(Rnd(a7) + a7);
		entry->phaseSpeed = static_cast<i16>(Rnd(a8) + a8);
		entry->phase = static_cast<i16>(Rnd(a5 + a6));
	}

	this->field_58 /= static_cast<i32>(a3);

	for (i = 0; i < a3; i++)
	{
		SVertexWobbleEntry *entry = &entries[i];

		entry->dx = static_cast<i16>(entry->vx - this->field_58.vx);
		entry->dy = static_cast<i16>(entry->vy - this->field_58.vy);
		entry->dz = static_cast<i16>(entry->vz - this->field_58.vz);

		i32 sq = entry->dx * entry->dx + entry->dy * entry->dy + entry->dz * entry->dz;
		entry->distance = static_cast<i16>(M3dMaths_SquareRoot0(sq));
	}
}

// @Ok
// Functional decompile (session-wide bar 2026-08-30: correctness, not byte
// match). Semantics match (verified by reading the disasm field by field):
// for each entry, phase += phaseSpeed, then newRadius = distance +
// amplitude + sin(phase)*amplitude/4096, then each axis of the target
// vertex is centre + delta*newRadius/distance. Remaining byte residue
// (47/83 mnemonic diffs) is a pointer-advance-early scheduling shape in
// the original's compiled loop (walks field_54 one iteration early, reads
// the current entry's remaining fields through negative offsets off the
// already-bumped pointer); not a functional bug. See effects.attempts.md.
void CVertexWobble::Move(void)
{
	print_if_false(Mem_RecoverPointer(reinterpret_cast<SHandle*>(&this->field_3C)) != 0, "NULL CVertexWobble handle");

	SVertexWobbleEntry *entry = reinterpret_cast<SVertexWobbleEntry*>(this->field_54);

	for (i32 i = 0; i < this->field_50; i++, entry++)
	{
		entry->phase += entry->phaseSpeed;

		i32 sinVal = G_RCOSSIN_TBL[entry->phase & 0xFFF].sin;
		i32 newRadius = (sinVal * entry->amplitude) / 4096 + entry->distance + entry->amplitude;

		i16 *vertex = reinterpret_cast<i16*>(reinterpret_cast<u8*>(this->field_4C) + 0x1C + entry->vertexIndex * 8);

		vertex[0] = static_cast<i16>(entry->dx * newRadius / entry->distance + this->field_58.vx);
		vertex[1] = static_cast<i16>(entry->dy * newRadius / entry->distance + this->field_58.vy);
		vertex[2] = static_cast<i16>(entry->dz * newRadius / entry->distance + this->field_58.vz);
	}
}

// @Ok
CElectroLine::~CElectroLine(void)
{
}

// @Ok
// @Matching
 CElectro::CElectro(void)
{
}

// @Ok
// Functional decompile (session-wide bar 2026-08-30: correctness, not byte
// match). Verified against a fresh IDA decompile of 0x439AE0: field_54 is
// freed first, then field_50, both via a plain non-zero check, matching
// the field offsets already validated in validate_CElectro (0x54, 0x50).
// The base class destructor call (sub_40A510, CSimpleTexturedRibbon's
// dtor) that follows in the disasm is emitted automatically by the C++
// compiler via CElectro's inheritance, no source change needed for it.
CElectro::~CElectro(void)
{
	if (this->field_54)
		Mem_Delete(this->field_54);

	if (this->field_50)
		Mem_Delete(this->field_50);
}

// @Ok
// @Matching
// Setup is used only by CElectroLine::CElectroLine, but writes to a field
// (a8, u16) at offset 0x64, which is right past CElectro's own validated
// size (CSimpleTexturedRibbon 0x4C + field_4C 4 + field_50/field_54 8 +
// field_58 CVector 12 = 0x64). Every real caller passes a CElectroLine
// (0x6C, PADDING(8) at 0x64), so this stays in bounds in practice. Kept as
// a raw offset write instead of restructuring CElectro's validated layout,
// since Setup is documented (via the Mac build) as CElectro's own member.
void CElectro::Setup(i32 a1, i32 a2, u32 *a3, u8 a4, u8 a5, u8 a6, u16 a7, u16 a8)
{
	this->SetNumFaces(a1);
	this->field_54 = DCMem_New((a1 * 3 + 3) * 4, 0, 1, 0, 1);

	print_if_false(a2 != 0, "Zero NumTextures");
	print_if_false(a2 < 20, "Suspicious NumTextures");

	this->field_50 = DCMem_New(a2 * 4, 0, 1, 0, 1);
	this->field_4C = a2;
	print_if_false(a3 != 0, "NULL pChecksums");

	for (a2 = 0; a2 < this->field_4C; a2++)
	{
		u32 checksum = *a3;
		a3++;
		reinterpret_cast<Texture**>(this->field_50)[a2] = Spool_FindTextureEntry(checksum);
		print_if_false(reinterpret_cast<Texture**>(this->field_50)[a2] != 0, "Could not find CElectro texture");
	}

	this->SetTexture(reinterpret_cast<Texture**>(this->field_50)[0]);
	this->SetRGB(a4, a5, a6);
	this->SetSemiTransparent();
	this->SetWidth(a7);

	*reinterpret_cast<u16*>(reinterpret_cast<u8*>(this) + 0x64) = a8;
}

// @Ok
void CBouncingRock::Move(void)
{
	this->mPos.vx += this->mVel.vx;
	this->mPos.vy += this->mVel.vy;
	this->mPos.vz += this->mVel.vz;

	if (this->mPos.vy > this->field_68)
	{
		this->mPos.vy = this->field_68;

		this->mVel.vx <<= 7;
		this->mVel.vx >>= 8;

		this->mVel.vy <<= 7;
		this->mVel.vy >>= 8;

		this->mVel.vz <<= 7;
		this->mVel.vz >>= 8;

		this->mVel.vy = -this->mVel.vy;

		this->field_6C++;
	}

	this->mVel.vy += 29584;
	this->mAngle += this->field_5A;

	if (this->field_6C >= 5)
		this->Die();

	if (++this->mLifetime > 0x3C)
		this->Die();

}

// @Ok
CBouncingRock::~CBouncingRock(void)
{
}

// @Ok
// Functional decompile (session-wide bar 2026-08-30: correctness, not byte
// match). Verified against a fresh IDA decompile of 0x43B550. Fixed a real
// bug: the texture lookups used gTextureRelated (a plain repo i32, holding
// nothing useful at runtime) as if it were the table base itself; the
// original reads it as a pointer stored by the game at a fixed address
// (0x56EA9C), see the G_TEXTURE_RELATED comment above. a4 selects the
// texture by checksum-like constant (0x28001F00 / 0x3288E271); the two
// branches are logically if/else-if on a4, written here as the original's
// inverted first condition to keep the same block layout.
CBouncingRock::CBouncingRock(
		CVector* a2,
		i32 a3,
		u32 a4)
{
	this->mPos = *a2;
	this->field_68 = a3;
	if ( a4 != 0x28001F00 )
	{
		if ( a4 == 0x3288E271 )
		{
			this->SetTexture(*reinterpret_cast<Texture **>(G_TEXTURE_RELATED + 20));
			this->mSemiTransparencyRate = 0;
		}
	}
	else
	{
		this->SetTexture(*reinterpret_cast<Texture **>(G_TEXTURE_RELATED + 44));
	}

	this->mScale = Rnd(200) + 350;
	i32 v6 = Rnd(4096);
	i32 v7 = Rnd(10) + 10;
	i32 v8 = v6 & 0xFFF;
	this->mVel.vx = v7 * G_RCOSSIN_TBL[v8].sin;
	this->mVel.vz = v7 * G_RCOSSIN_TBL[v8].cos;

	this->mVel.vy = -81920 - (Rnd(20) << 12);
	this->field_5A = 500;
	if ( Rnd(2) )
		this->field_5A *= -1;
	this->mPostScale = 0xC001000;
}

// @Ok
void CChunkSmoke::Move(void)
{
	this->mAngle += this->field_5A;
	this->field_5A -= this->field_5A >> 3;
	this->mScale += (this->field_7C - this->mScale) >> 1;

	if (++this->mAge > this->field_74)
	{
		this->mPos.vy -= this->mVel.vy;

		Bit_ReduceRGB(&this->mCodeBGR, this->field_78);
		if (!(0xFFFFFF & this->mCodeBGR))
			this->Die();
	}
	else
	{
		this->mPos += (this->field_68 - this->mPos) >> 2;
	}
}

// @Ok
// @Matching
// 0x43B410, 317 bytes (sub_43B410 in names.json; the Mac build names it
// Effects_LaserFlash(CVector const&, int, int, uchar, uchar, uchar, uchar),
// the symbol sits between CChunkSmoke::Move and CBouncingRock::CBouncingRock
// on both platforms). Translated from the disassembly: one CGlowFlash whose
// size params scale with a2, then either a random start angle (a3 == 0) or
// a fixed-ish angle plus a section mask that hides one half minus one
// section (a3 != 0). The original writes the angle/mask through the new
// pointer without a null check, kept as is.
void Effects_LaserFlash(CVector* pPos, i32 a2, i32 a3, u8 a4, u8 a5, u8 a6, u8 a7)
{
	CGlowFlash* pFlash = new CGlowFlash(
			pPos, 8, 255, 255, 255, 0, a4, a5, a6, 0,
			(30 * a2) >> 8, 0, 1,
			(40 * a2) >> 8, (130 * a2) >> 8, (20 * a2) >> 8, (65 * a2) >> 8,
			(4 * a2) >> 8, a7);

	if (a3 != 0)
	{
		pFlash->mAngle = Rnd(401) - 1224;
		i32 half = pFlash->mNumSections >> 1;
		pFlash->mMask = ((1 << half) - 1) & ~(1 << (half - 1)) & ~1;
	}
	else
	{
		pFlash->mAngle = Rnd(4096);
	}
}

// @Ok
// 0x439CC0, 350 bytes (Mac: Effects_CreateElectroLines(int)). Builds one
// CElectroLine per link, following the single link of each node round the
// chain until it comes back to the start node. Node 409 data (after the
// u16 type/subtype header): i32 at +8 passed straight through, three
// texture name pointers at +12 (terminated by a zero dword) that get
// replaced in place by their Spool checksums, then the parameter block:
// three bytes at +0/+2/+4 and u16s at +6/+8/+12 of it.
void Effects_CreateElectroLines(i32 NodeIndex)
{
	u16 *pNode = reinterpret_cast<u16*>(G_OFFSETLIST[NodeIndex]);
	print_if_false(pNode[0] == 1 && pNode[1] == 409, "Node is not an electroline");

	CVector pos(0, 0, 0);
	u8 *pData = reinterpret_cast<u8*>(Trig_GetPosition(&pos, NodeIndex));

	i32 a10 = *reinterpret_cast<i32*>(pData + 8);
	u32 *pTextures = reinterpret_cast<u32*>(pData + 12);
	u32 *pCur = pTextures + 1;
	i32 numTextures = 0;
	if (pTextures[0] != 0)
	{
		u32 v;
		do
		{
			v = *pCur;
			numTextures++;
			pCur++;
		}
		while (v != 0);
	}
	print_if_false(numTextures == 3, "Electrolines isn't composed of 3 Textures.");

	pTextures[0] = Spool_FindTextureChecksum("Bolt");
	pTextures[1] = Spool_FindTextureChecksum("Bolt2");
	pTextures[2] = Spool_FindTextureChecksum("Bolt3");

	u8 *pParams = reinterpret_cast<u8*>(pCur);
	u16 node = static_cast<u16>(NodeIndex);
	u16 next;
	do
	{
		u16 *pLinks = Trig_GetLinksPointer(node);
		if (pLinks[0] == 0)
			break;
		next = pLinks[1];

		new CElectroLine(
				static_cast<u16>(NodeIndex), node, next,
				pParams[0], pParams[2], pParams[4],
				*reinterpret_cast<u16*>(pParams + 6),
				*reinterpret_cast<u16*>(pParams + 8),
				*reinterpret_cast<u16*>(pParams + 12),
				a10, numTextures, pTextures);

		node = next;
	}
	while (next != NodeIndex);
}

// @Ok
// @Matching
// 0x43B740, 115 bytes (sub_43B740 in names.json; the Mac build names it
// Effects_MakeRocks(CVector const&, ulong), between CBouncingRock::Move and
// CVertexWobble::CVertexWobble on both platforms). Ten CBouncingRock bits at
// the ground height under pPos; a2 is the texture selector the rock
// constructor switches on. CPlayer::CreateCombatImpactEffect has the same
// loop inlined.
void Effects_MakeRocks(CVector* pPos, u32 a2)
{
	i32 groundY = Web_GetGroundY(pPos);

	for (i32 i = 10; i != 0; i--)
		new CBouncingRock(pPos, groundY, a2);
}

// @Ok
CChunkSmoke::~CChunkSmoke(void)
{
}

// @Ok
// @Test
CChunkSmoke::CChunkSmoke(
		CVector* a2,
		CVector* a3,
		i32 a4)
{
	this->field_68.vx = 0;
	this->field_68.vy = 0;
	this->field_68.vz = 0;

	this->SetSemiTransparent();
	this->mCodeBGR = 0x2E202020;
	this->mScale = 0;

	this->field_7C = Rnd(1000) + 1000;
	this->mAngle = Rnd(4096);
	this->field_5A = 150;

	if ( a4 < 0 )
		this->field_5A = -150;

	this->mPos = *a2;
	this->field_68 = *a3;
	this->field_78 = Rnd(0) + 4;
	this->field_74 = Rnd(40);

	this->SetAnim(0xEu);

	this->mFrame = 0;
	DoAssert(this->mNumFrames != 0, "Woops");

	this->mpPSXFrame = &this->mpPSXAnim[this->mFrame];
	this->mVel.vy = Rnd(5) << 12;
}

// @Ok
// Functional decompile of 0x439E20 (Mac: CPingLine::CPingLine(CVector const&,
// CSVector const&, int, int, int, int, int, int)). pDir is a rotation, not a
// vector: it goes straight into Utils_GetVecFromMagDir, once with velMag to
// build mVel and once with lengthMag to build the line's end offset. mStart is
// the raw spawn point, mEnd is that point plus the offset. The start colour is
// forced to black and only the end colour carries the tint, then bit 25 of
// mCodeBGR0 is set (the same "gouraud line" code bit CGLine users set
// elsewhere). The original asserts on a zero fade rate and still stores it.
CPingLine::CPingLine(CVector* pPos, CSVector* pDir, i32 r, i32 g, i32 b, i32 fadeRate, i32 lengthMag, i32 velMag)
{
	Utils_GetVecFromMagDir(&this->mVel, velMag, pDir);

	this->mStart = *pPos;

	Utils_GetVecFromMagDir(&this->mEnd, lengthMag, pDir);
	this->mEnd += *pPos;

	this->SetRGB0(0, 0, 0);
	this->SetRGB1(static_cast<u8>(r), static_cast<u8>(g), static_cast<u8>(b));

	this->mCodeBGR0 |= 0x2000000;

	print_if_false(fadeRate != 0, "Zero FadeRate");
	this->mFadeRate = static_cast<u8>(fadeRate);
}

// @Ok
// Functional decompile of 0x439F20. Same body as the CSVector overload above,
// except pDir is a real direction vector here, so the two scaled copies come
// from CVector operator*(const CVector&, const int&) (0x4E77A0) instead of
// Utils_GetVecFromMagDir. No PC call site for this overload exists yet, it is
// implemented because the class needs it to be complete.
CPingLine::CPingLine(CVector* pPos, CVector* pDir, i32 r, i32 g, i32 b, i32 fadeRate, i32 lengthMag, i32 velMag)
{
	this->mVel = *pDir * velMag;

	this->mStart = *pPos;

	this->mEnd = *pDir * lengthMag;
	this->mEnd += *pPos;

	this->SetRGB0(0, 0, 0);
	this->SetRGB1(static_cast<u8>(r), static_cast<u8>(g), static_cast<u8>(b));

	this->mCodeBGR0 |= 0x2000000;

	print_if_false(fadeRate != 0, "Zero FadeRate");
	this->mFadeRate = static_cast<u8>(fadeRate);
}

// @Ok
CPingLine::~CPingLine(void)
{
}

// @Ok
// Functional decompile of 0x43A040. Both ends move by mVel, then every channel
// of the end colour drops by mFadeRate with an unsigned clamp at zero (the
// original uses jbe, so a fade rate above the channel value gives 0, it does
// not wrap). The bit dies once the whole colour word is zero.
void CPingLine::Move(void)
{
	this->mStart.vx += this->mVel.vx;
	this->mStart.vy += this->mVel.vy;
	this->mStart.vz += this->mVel.vz;

	this->mEnd.vx += this->mVel.vx;
	this->mEnd.vy += this->mVel.vy;
	this->mEnd.vz += this->mVel.vz;

	u8 fade = this->mFadeRate;

	u8 low = static_cast<u8>(this->mPadBGR1);
	u8 mid = static_cast<u8>(this->mPadBGR1 >> 8);
	u8 high = static_cast<u8>(this->mPadBGR1 >> 16);

	if (fade > low)
		low = 0;
	else
		low -= fade;

	if (fade > mid)
		mid = 0;
	else
		mid -= fade;

	if (fade > high)
		high = 0;
	else
		high -= fade;

	this->mPadBGR1 = (((high << 8) | mid) << 8) | low;

	if (!this->mPadBGR1)
		this->Die();
}

// @Ok
void CFootprint::Move(void)
{
	if (this->field_84)
	{
		this->field_84--;
		return;
	}

	u8 low = this->mTint;
	u8 mid = this->mTint >> 8;
	u8 high = this->mTint >> 16;

	if (low < 1)
		low = 0;
	else
		low--;

	if (mid < 1)
		mid = 0;
	else
		mid--;

	if (high < 1)
		high = 0;
	else
		high--;


	this->mTint = (((high << 8) | mid) << 8) | low;
	if (!this->mTint)
		this->Die();
}

// @Ok
CFootprint::~CFootprint(void)
{
}

// @Ok
// Functional decompile (session-wide bar 2026-08-30: correctness, not byte
// match). Verified field by field against a fresh IDA decompile of
// 0x43A0F0: every store to mPos/mPosB/mPosC/mPosD, the trig table lookup,
// the RhinoStomp texture pick, SetSubtractiveTransparency, SetTint and
// field_84/mType all match. The original reuses one scaled register value
// (v12*70) for both mPos.vz and mPosB.vx since they hold the same value
// before scaling; that register reuse shows up as scheduling noise, not a
// semantic difference.
CFootprint::CFootprint(CVector* pVector, i32 a3)
{
	this->SetTexture(Spool_FindTextureChecksum("RhinoStomp"));
	this->SetSubtractiveTransparency();
	this->SetTint(0x12u, 0x12u, 0x12u);
	this->field_84 = 2000;

	this->mPos.vy = pVector->vy;
	this->mPosB.vy = pVector->vy;
	this->mPosC.vy = pVector->vy;
	this->mPosD.vy = pVector->vy;

	i32 vxVel = G_RCOSSIN_TBL[a3 & 0xFFF].sin;
	i32 vzVel = G_RCOSSIN_TBL[a3 & 0xFFF].cos;

	this->mPos.vx = vxVel - vzVel;
	i32 v12 = vxVel + vzVel;
	i32 v13 = vzVel - vxVel;

	this->mPos.vz = v12;
	this->mPosB.vx = v12;
	this->mPosB.vz = v13;
	this->mPosC.vx = -v12;
	this->mPosC.vz = this->mPos.vx;
	this->mPosD.vx = v13;
	this->mPosD.vz = -v12;
	this->mPos.vx *= 70;
	this->mPos.vz *= 70;
	this->mPosB.vx *= 70;
	this->mPosB.vz *= 70;
	this->mPosC.vx *= 70;
	this->mPosC.vz *= 70;
	this->mPosD.vx *= 70;
	this->mPosD.vz *= 70;
	this->mPos.vx += pVector->vx;
	this->mPos.vz += pVector->vz;
	this->mPosB.vx += pVector->vx;
	this->mPosB.vz += pVector->vz;
	this->mPosC.vx += pVector->vx;
	this->mPosC.vz += pVector->vz;
	this->mPosD.vx += pVector->vx;
	this->mPosD.vz += pVector->vz;

	this->mType = 25;
}

// @Ok
// Functional decompile of 0x43A300 (Mac: Effects_FootStomp(CVector const&,
// ulong)). Throws 20 dust lines out of one spot: each one picks a random
// heading, spawns 80 units away from pPos along that heading (the trig table
// entries are read signed, movsx in the original) and flies off at a random
// speed with a fixed upward pitch of -312. Checksum is the second parameter
// the PC build never reads, see the note in effects.h.
void Effects_FootStomp(CVector* pPos, u32 Checksum)
{
	CSVector Dir;
	CVector Spawn;

	Dir.vy = 0;
	Dir.vx = -312;
	Dir.vz = 0;

	Spawn.vx = 0;
	Spawn.vz = 0;
	Spawn.vy = pPos->vy;

	for (i32 i = 20; i != 0; i--)
	{
		Dir.vy = static_cast<i16>(Rnd(4096));

		Spawn.vx = pPos->vx - G_RCOSSIN_TBL[Dir.vy & 0xFFF].sin * 80;
		Spawn.vz = pPos->vz - G_RCOSSIN_TBL[Dir.vy & 0xFFF].cos * 80;

		new CPingLine(&Spawn, &Dir, 0x80, 0x80, 0x80, 0xF, 0xC8, Rnd(50) + 30);
	}
}

// @Ok
void CRhinoWallImpact::Move(void)
{
	if (++this->mAge >= 200)
	{
		Bit_ReduceRGB(&this->mTint, 1);
		if (!(0xFFFFFF & this->mTint))
			this->Die();
	}
}

// @Ok
CRhinoWallImpact::~CRhinoWallImpact(void)
{
}

// @Ok
CRhinoWallImpact::CRhinoWallImpact(SLineInfo* pLineInfo)
{
	print_if_false(pLineInfo != 0, "NULL pLineInfo");

	this->SetTexture(Spool_FindTextureChecksum("RhinoWallImpact"));
	this->SetTint(0x12u, 0x12u, 0x12u);
	this->SetSubtractiveTransparency();

	this->mCodeBGR &= ~0x200;

	CVector v2;
	v2 = pLineInfo->Position;
	v2.vy -= 204800;

	this->OrientUsing(&v2, reinterpret_cast<SVECTOR*>(&pLineInfo->Normal), 100, 100);
	this->mType = 26;
}

// @Ok
// Functional decompile (session-wide bar 2026-08-30: correctness, not byte
// match). Re-verified field by field against a fresh IDA decompile of
// 0x43A690: every field write, texture pick, model/vertex lookup and RNG
// call matches exactly, including the two real bugs found on the way (the
// SetTint call reads a single BYTE at pSuper+0x24, the low byte of
// CItem::mRGB, directly, not the full u32 field masked down; and the
// VertA/VertB range checks compare as plain signed ints, not unsigned,
// since the source u8 values and the model's u16 vertex count both promote
// to plain int). Remaining byte residue (67/230 mnemonic diffs, 802 bytes
// original) is scheduling only: independent field zero-stores interleaved
// with the three NULL checks and the "Bad Model"/"Bad VertA"/"Bad VertB"
// checks, and one 3-byte-earlier SetTint read the original does not cache
// into a local (re-reads pSuper+0x24 three times); not a functional bug.
// Picks a
// random SSkinGooSource entry (pSources[Rnd(numSources)]), textures itself
// from one of that entry's two texture checksums (chosen 50/50), then reads
// two vertex positions (A/B, source's byte 1 and byte 2) off the model
// chosen by G_PSXREGION[region].ppModels[source's byte 0]. The "flip" byte
// (source's byte 3) swaps which of the two becomes A/B and gets toggled
// every call, so repeated calls with the same source entry alternate ends.
// field_BC/field_C0/field_C4 and field_CC/field_D0/field_D4 are random
// spawn offset / launch velocity, driven by pParams.
CSkinGoo::CSkinGoo(CSuper* pSuper, SSkinGooSource* pSources, i32 numSources, SSkinGooParams* pParams)
{
	this->field_8C = 0;
	this->field_90 = 0;
	this->field_94 = 0;
	this->field_98 = 0;
	this->field_9C = 0;
	this->field_A0 = 0;

	this->field_A4 = 0;
	this->field_A6 = 0;
	this->field_A8 = 0;

	this->field_AC = 0;
	this->field_AE = 0;
	this->field_B0 = 0;

	this->field_CC = 0;
	this->field_D0 = 0;
	this->field_D4 = 0;

	print_if_false(pSources != 0, "NULL pGooSources");
	print_if_false(pParams != 0, "NULL pGooParams");
	print_if_false(pSuper != 0, "NULL pSuper sent to CVenomWrap");

	SHandle superHandle = Mem_MakeHandle(pSuper);
	this->field_84 = superHandle.pWhatever;
	this->field_88 = superHandle.Id;

	i32 sourceIndex = Rnd(numSources);
	i32 textureChoice = Rnd(2);

	if (textureChoice != 0)
	{
		if (textureChoice == 1)
			this->SetTexture(pSources[sourceIndex].field_8);
	}
	else
	{
		this->SetTexture(pSources[sourceIndex].field_4);
	}

	this->SetSemiTransparent();
	this->mCodeBGR &= ~0x40;

	if (pSuper->mFlags & 0x800)
	{
		this->SetSemiTransparent();
		u8 lowByteOfRGB = *(reinterpret_cast<u8*>(pSuper) + 0x24);
		this->SetTint(lowByteOfRGB, lowByteOfRGB, lowByteOfRGB);
	}

	u8 *source = reinterpret_cast<u8*>(&pSources[sourceIndex]);
	u32 modelIndex = source[0];

	print_if_false(modelIndex < reinterpret_cast<u32*>(G_PSXREGION[pSuper->mRegion].ppModels)[-1], "Bad Model");

	void *model = G_PSXREGION[pSuper->mRegion].ppModels[modelIndex];

	i32 vertA = source[1];
	print_if_false(vertA < *reinterpret_cast<u16*>(reinterpret_cast<u8*>(model) + 2), "Bad VertA");

	i32 vertB = source[2];
	print_if_false(vertB < *reinterpret_cast<u16*>(reinterpret_cast<u8*>(model) + 2), "Bad VertB");

	u8 flip = source[3];
	i32 idxA = vertA;
	i32 idxB = vertB;

	if (flip != 0)
	{
		idxA = vertB;
		idxB = vertA;
	}

	source[3] = flip ^ 1;

	this->field_AA = static_cast<u16>(modelIndex);
	this->field_B2 = static_cast<u16>(modelIndex);

	i16 *vertexA = reinterpret_cast<i16*>(reinterpret_cast<u8*>(model) + 0x1C + idxA * 8);
	this->field_A4 = vertexA[0];
	this->field_A6 = vertexA[1];
	this->field_A8 = vertexA[2];

	i16 *vertexB = reinterpret_cast<i16*>(reinterpret_cast<u8*>(model) + 0x1C + idxB * 8);
	this->field_AC = vertexB[0];
	this->field_AE = vertexB[1];
	this->field_B0 = vertexB[2];

	this->field_BC = pParams->mOffsetXBase + Rnd(pParams->mOffsetXRange);
	this->field_C0 = pParams->mOffsetXBase + Rnd(pParams->mOffsetXRange);
	this->field_C4 = pParams->mOffsetZBase + Rnd(pParams->mOffsetZRange);

	this->field_CC = (Rnd(2 * pParams->mVelRange + 1) - pParams->mVelRange) << 12;

	i32 rollD0 = Rnd(2 * pParams->mVelRange + 1);
	i32 velRangeD0 = pParams->mVelRange;
	this->mType = 27;
	this->field_D0 = (rollD0 - velRangeD0) << 12;

	this->field_D4 = (Rnd(2 * pParams->mVelRange + 1) - pParams->mVelRange) << 12;
}

// @Ok
// Functional decompile (session-wide bar 2026-08-30: correctness, not byte
// match). Verified against a fresh IDA decompile of 0x43A9F0. Same field
// layout, model/vertex lookup and spawn/velocity maths as the
// SSkinGooSource overload above (see its comment); the only real
// difference is the texture pick, which reads one of two 32-byte name
// strings from the source entry (field_4 / field_24) through the new
// CQuadBit::SetTexture(char*) overload instead of a checksum through
// SetTexture(u32). Fixed a real gap on review: the SetSemiTransparent /
// mCodeBGR &= ~0x40 / conditional SetTint block (same as the sibling
// overload, gated on pSuper->mFlags & 0x800) was missing entirely from the
// first commit of this function; re-added after re-checking against the
// IDA decompile, which has it between the texture pick and the model/vert
// lookup.
CSkinGoo::CSkinGoo(CSuper* pSuper, SSkinGooSource2* pSources, i32 numSources, SSkinGooParams* pParams)
{
	this->field_8C = 0;
	this->field_90 = 0;
	this->field_94 = 0;
	this->field_98 = 0;
	this->field_9C = 0;
	this->field_A0 = 0;

	this->field_A4 = 0;
	this->field_A6 = 0;
	this->field_A8 = 0;

	this->field_AC = 0;
	this->field_AE = 0;
	this->field_B0 = 0;

	this->field_CC = 0;
	this->field_D0 = 0;
	this->field_D4 = 0;

	print_if_false(pSources != 0, "NULL pGooSources");
	print_if_false(pParams != 0, "NULL pGooParams");
	print_if_false(pSuper != 0, "NULL pSuper sent to CVenomWrap");

	SHandle superHandle = Mem_MakeHandle(pSuper);
	this->field_84 = superHandle.pWhatever;
	this->field_88 = superHandle.Id;

	i32 sourceIndex = Rnd(numSources);
	i32 textureChoice = Rnd(2);

	if (textureChoice != 0)
	{
		if (textureChoice == 1)
			this->SetTexture(pSources[sourceIndex].field_24);
	}
	else
	{
		this->SetTexture(pSources[sourceIndex].field_4);
	}

	this->SetSemiTransparent();
	this->mCodeBGR &= ~0x40;

	if (pSuper->mFlags & 0x800)
	{
		this->SetSemiTransparent();
		u8 lowByteOfRGB = *(reinterpret_cast<u8*>(pSuper) + 0x24);
		this->SetTint(lowByteOfRGB, lowByteOfRGB, lowByteOfRGB);
	}

	u8 *source = reinterpret_cast<u8*>(&pSources[sourceIndex]);
	u32 modelIndex = source[0];

	print_if_false(modelIndex < reinterpret_cast<u32*>(G_PSXREGION[pSuper->mRegion].ppModels)[-1], "Bad Model");

	void *model = G_PSXREGION[pSuper->mRegion].ppModels[modelIndex];

	i32 vertA = source[1];
	print_if_false(vertA < *reinterpret_cast<u16*>(reinterpret_cast<u8*>(model) + 2), "Bad VertA");

	i32 vertB = source[2];
	print_if_false(vertB < *reinterpret_cast<u16*>(reinterpret_cast<u8*>(model) + 2), "Bad VertB");

	u8 flip = source[3];
	i32 idxA = vertA;
	i32 idxB = vertB;

	if (flip != 0)
	{
		idxA = vertB;
		idxB = vertA;
	}

	source[3] = flip ^ 1;

	this->field_AA = static_cast<u16>(modelIndex);
	this->field_B2 = static_cast<u16>(modelIndex);

	i16 *vertexA = reinterpret_cast<i16*>(reinterpret_cast<u8*>(model) + 0x1C + idxA * 8);
	this->field_A4 = vertexA[0];
	this->field_A6 = vertexA[1];
	this->field_A8 = vertexA[2];

	i16 *vertexB = reinterpret_cast<i16*>(reinterpret_cast<u8*>(model) + 0x1C + idxB * 8);
	this->field_AC = vertexB[0];
	this->field_AE = vertexB[1];
	this->field_B0 = vertexB[2];

	this->field_BC = pParams->mOffsetXBase + Rnd(pParams->mOffsetXRange);
	this->field_C0 = pParams->mOffsetXBase + Rnd(pParams->mOffsetXRange);
	this->field_C4 = pParams->mOffsetZBase + Rnd(pParams->mOffsetZRange);

	this->field_CC = (Rnd(2 * pParams->mVelRange + 1) - pParams->mVelRange) << 12;
	this->field_D0 = (Rnd(2 * pParams->mVelRange + 1) - pParams->mVelRange) << 12;

	this->mType = 27;
	this->field_D4 = (Rnd(2 * pParams->mVelRange + 1) - pParams->mVelRange) << 12;
}

// @Ok
// @Matching
// cmpsum against the rebuilt DLL shows 0 mnemonic diffs (all 133 instructions
// match in count and mnemonic). A few instructions use a different physical
// register for the same operation with the same shape (register-allocator
// colour choice, e.g. ecx vs eax around 0x43900e-0x439030) - this only shows
// up as an operand difference, not a mnemonic diff, so it does not count
// against the match per the project's cmpsum bar.
CElectrify::CElectrify(CSuper* pSuper, i32 a2)
	: CSimpleTexturedRibbon(a2)
{
	print_if_false(pSuper != 0, "NULL pSuper");
	print_if_false((pSuper->mFlags >> 1) & 1, "pSuper not ready for CElectrify");

	SHandle superHandle = Mem_MakeHandle(pSuper);
	this->field_5C = superHandle.pWhatever;
	this->field_60 = superHandle.Id;

	SHandle *pField114 = &pSuper->field_114;
	print_if_false(Mem_RecoverPointer(pField114) == 0, "CElectrify already attached");

	SHandle selfHandle = Mem_MakeHandle(this);
	pField114->pWhatever = selfHandle.pWhatever;
	i32 region = pSuper->mRegion;
	pField114->Id = selfHandle.Id;

	print_if_false(G_PSXREGION[region].ppModels != 0, "No models for CElectrify");

	this->field_50 = reinterpret_cast<i32*>(G_PSXREGION[pSuper->mRegion].ppModels)[-1];
	this->field_4C = DCMem_New(this->field_50 * 8, 0, 1, 0, 1);
	this->field_54 = reinterpret_cast<CVector*>(DCMem_New(this->field_50 * 12, 0, 1, 0, 1));

	for (i32 j = 0; j < this->field_50; j++)
		reinterpret_cast<u16*>(this->field_4C)[j * 4 + 3] = static_cast<u16>(j);

	this->SetTexture(Spool_FindTextureChecksum("Electro"));
	this->SetWidth(0x19);
	this->SetRGB(0, 0x20, 0x80);
	this->SetSemiTransparent();

	i32 subType = *reinterpret_cast<u16*>(reinterpret_cast<u8*>(pSuper) + 0x38);

	if (subType != 50)
	{
		if (subType != 0x133)
			this->field_58 = 0x64;
		else
			this->field_58 = 0xB4;
	}
	else
	{
		this->field_58 = 0x40;
	}

	this->ChooseRandomPositions(0, 1);
}

// keep the MSVC inliner away, same trick as shell.cpp/PCShell.cpp: this stub
// lives in the same TU as its caller (CElectrify::CElectrify), and the
// original calls it as a real out-of-line function.
#ifdef _MSC_VER
#pragma auto_inline(off)
#endif
// @Ok
// Functional decompile (session-wide bar 2026-08-30: correctness, not byte
// match). cmpsum shows only 1 mnemonic diff (the final "ret 8" falls
// outside the compare window); the residue is a stack frame 12 bytes bigger
// than the original (sub esp,60h here vs sub esp,54h), which pushes one
// "lea" past the disp8/disp32 encoding boundary. Every instruction content
// and value already matches; not a functional bug. field_4C is
// an array of SHook (see M3dUtils_GetDynamicHookPosition), field_54 is the
// matching array of computed CVector positions (both field_50 entries
// long). Each call picks a random hook, finds a random point between it and
// its parent hook (via G_PSXREGION[region].pHierarchy), then pushes that
// point out towards the camera by a fixed distance and stores it into
// field_44[i] (the ribbon's own point array, CSimpleTexturedRibbon).
void CElectrify::ChooseRandomPositions(i32 a1, i32 a2)
{
	CSuper *pSuper = reinterpret_cast<CSuper*>(Mem_RecoverPointer(reinterpret_cast<SHandle*>(&this->field_5C)));
	print_if_false(pSuper != 0, "NULL pSuper?");

	for (i32 i = 0; i < this->field_50; i++)
	{
		reinterpret_cast<SHook*>(this->field_4C)[i].Part.vx = static_cast<i16>(Rnd(201) - 100);
		reinterpret_cast<SHook*>(this->field_4C)[i].Part.vy = static_cast<i16>(Rnd(201) - 100);
		reinterpret_cast<SHook*>(this->field_4C)[i].Part.vz = static_cast<i16>(Rnd(201) - 100);

		M3dUtils_GetDynamicHookPosition(reinterpret_cast<VECTOR*>(&this->field_54[i]), pSuper, reinterpret_cast<SHook*>(this->field_4C) + i);
	}

	u16 *pHierarchy = G_PSXREGION[pSuper->mRegion].pHierarchy;
	print_if_false(pHierarchy != 0, "NULL pHierarchy?");

	for (i32 idx = a1; idx < this->field_3C + 1; idx += a2)
	{
		u16 pos = static_cast<u16>(Rnd(this->field_50));
		u16 parent = pHierarchy[pos];
		print_if_false(parent < this->field_50, "Bad Parent index");

		i16 randOffset = static_cast<i16>(Rnd(this->field_58 + 256));

		CVector interp;

		CVector toCamera;
		toCamera.vx = G_MIKE_CAMERA[0].Position.vx << 12;

		i16 weight = static_cast<i16>(randOffset - this->field_58);
		toCamera.vy = G_MIKE_CAMERA[0].Position.vy << 12;
		toCamera.vz = G_MIKE_CAMERA[0].Position.vz << 12;

		interp.vx = this->field_54[pos].vx + ((weight * (this->field_54[parent].vx - this->field_54[pos].vx)) >> 8);
		interp.vy = this->field_54[pos].vy + ((weight * (this->field_54[parent].vy - this->field_54[pos].vy)) >> 8);
		interp.vz = this->field_54[pos].vz + ((weight * (this->field_54[parent].vz - this->field_54[pos].vz)) >> 8);

		toCamera = (toCamera - interp) >> 12;
		VectorNormal(reinterpret_cast<VECTOR*>(&toCamera), reinterpret_cast<VECTOR*>(&toCamera));

		*reinterpret_cast<CVector*>(&this->field_44[idx]) = interp + 30 * toCamera;
	}
}
#ifdef _MSC_VER
#pragma auto_inline(on)
#endif

// @Ok
void Effects_UnElectrify(CSuper* pSuper)
{
	print_if_false(pSuper != 0, "NULL pSuper?");

	CItem *v2 = reinterpret_cast<CItem*>(Mem_RecoverPointer(&pSuper->field_114));
	if (v2)
		delete v2;
}

// @Ok
// Functional: electrify, logic verified against Hex-Rays at 0x438e20.
void Effects_Electrify(CSuper* pSuper)
{
	print_if_false(pSuper != 0, "NULL pSuper?");
	Effects_UnElectrify(pSuper);

	if (pSuper->mType == 50)
	{
		if (G_CURRENTSUIT != 4)
		{
			new CElectrify(pSuper, 10);
		}
	}
	else
	{
		new CElectrify(pSuper, 20);
	}
}

void validate_CElectrify(void)
{
	VALIDATE_SIZE(CElectrify, 0x64);

	VALIDATE(CElectrify, field_4C, 0x4C);
	VALIDATE(CElectrify, field_50, 0x50);
	VALIDATE(CElectrify, field_54, 0x54);
	VALIDATE(CElectrify, field_58, 0x58);
	VALIDATE(CElectrify, field_5C, 0x5C);
	VALIDATE(CElectrify, field_60, 0x60);
}

void validate_CSkinGoo(void)
{
	VALIDATE_SIZE(CSkinGoo, 0xD8);

	VALIDATE(CSkinGoo, field_84, 0x84);
	VALIDATE(CSkinGoo, field_88, 0x88);

	VALIDATE(CSkinGoo, field_8C, 0x8C);
	VALIDATE(CSkinGoo, field_90, 0x90);
	VALIDATE(CSkinGoo, field_94, 0x94);
	VALIDATE(CSkinGoo, field_98, 0x98);
	VALIDATE(CSkinGoo, field_9C, 0x9C);
	VALIDATE(CSkinGoo, field_A0, 0xA0);

	VALIDATE(CSkinGoo, field_A4, 0xA4);
	VALIDATE(CSkinGoo, field_A6, 0xA6);
	VALIDATE(CSkinGoo, field_A8, 0xA8);
	VALIDATE(CSkinGoo, field_AA, 0xAA);
	VALIDATE(CSkinGoo, field_AC, 0xAC);
	VALIDATE(CSkinGoo, field_AE, 0xAE);
	VALIDATE(CSkinGoo, field_B0, 0xB0);
	VALIDATE(CSkinGoo, field_B2, 0xB2);

	VALIDATE(CSkinGoo, field_BC, 0xBC);
	VALIDATE(CSkinGoo, field_C0, 0xC0);
	VALIDATE(CSkinGoo, field_C4, 0xC4);

	VALIDATE(CSkinGoo, field_CC, 0xCC);
	VALIDATE(CSkinGoo, field_D0, 0xD0);
	VALIDATE(CSkinGoo, field_D4, 0xD4);
}

void validate_SSkinGooSource(void)
{
	VALIDATE_SIZE(SSkinGooSource, 0xC);

	VALIDATE(SSkinGooSource, field_0, 0x0);
	VALIDATE(SSkinGooSource, field_4, 0x4);
	VALIDATE(SSkinGooSource, field_8, 0x8);
}

void validate_SSkinGooSource2(void)
{
	VALIDATE_SIZE(SSkinGooSource2, 0x44);

	VALIDATE(SSkinGooSource2, field_0, 0x0);
	VALIDATE(SSkinGooSource2, field_4, 0x4);
	VALIDATE(SSkinGooSource2, field_24, 0x24);
}

void validate_SSkinGooParams(void)
{
}

void validate_CRhinoWallImpact(void)
{
	VALIDATE_SIZE(CRhinoWallImpact, 0x88);
}

void validate_CFootprint(void)
{
	VALIDATE_SIZE(CFootprint, 0x88);

	VALIDATE(CFootprint, field_84, 0x84);
}

void validate_CPingLine(void)
{
	VALIDATE_SIZE(CPingLine, 0x60);

	VALIDATE(CPingLine, mFadeRate, 0x5C);
}

void validate_CChunkSmoke(void)
{
	VALIDATE_SIZE(CChunkSmoke, 0x80);

	VALIDATE(CChunkSmoke, field_68, 0x68);
	VALIDATE(CChunkSmoke, field_74, 0x74);
	VALIDATE(CChunkSmoke, field_78, 0x78);
	VALIDATE(CChunkSmoke, field_7C, 0x7C);
}

void validate_CBouncingRock(void)
{
	VALIDATE_SIZE(CBouncingRock, 0x70);

	VALIDATE(CBouncingRock, field_68, 0x68);
	VALIDATE(CBouncingRock, field_6C, 0x6C);
}

void validate_CElectro(void)
{
	VALIDATE_SIZE(CElectro, 0x64);

	VALIDATE(CElectro, field_4C, 0x4C);
	VALIDATE(CElectro, field_50, 0x50);
	VALIDATE(CElectro, field_54, 0x54);

	VALIDATE(CElectro, field_58, 0x58);
}

void validate_CElectroLine(void)
{
	VALIDATE_SIZE(CElectroLine, 0x6C);

	VALIDATE(CElectroLine, field_68, 0x68);
	VALIDATE(CElectroLine, field_6A, 0x6A);
}

void validate_CVertexWobble(void)
{
	VALIDATE_SIZE(CVertexWobble, 0x60);

	VALIDATE(CVertexWobble, field_3C, 0x3C);
	VALIDATE(CVertexWobble, field_40, 0x40);
	VALIDATE(CVertexWobble, field_4C, 0x4C);
	VALIDATE(CVertexWobble, field_50, 0x50);
	VALIDATE(CVertexWobble, field_54, 0x54);
	VALIDATE(CVertexWobble, field_58, 0x58);
}

#include "compatibility/my_patch.h"

// @Bogus
// Only the functions whose whole call closure runs on game memory are hooked.
//
// Two things block the rest, both outside this file:
//
// 1. The CFlatBit, CQuadBit and CGLine constructors and destructors attach and
//    detach through FlatBitList (0x0056EA50), QuadBitList (0x0056EB2C) and
//    GLineList (0x0056E9CC). Those three are still plain repo globals in
//    bit.cpp, unlike their three neighbours (BitCount, SpecialDisplayList,
//    NonRenderedBitList) which already have G_ macros. So a hooked constructor
//    would put the new bit on our own list, which nothing walks, and a hooked
//    destructor would unlink it from the wrong list head. That rules out the
//    CBouncingRock, CChunkSmoke, CFootprint, CRhinoWallImpact, CSkinGoo and
//    CPingLine constructors and destructors, and Effects_FootStomp.
//
// 2. A hooked constructor stamps our vtable into the object. Some virtuals of
//    these classes are missing from this file, so our vtable would inherit the
//    base version: CElectrify has no Move (0x00439410) and no destructor
//    (0x004390E0), CElectro has no Move (0x00439670), CSkinGoo has no Move
//    (0x0043AD30) and no destructor (0x0043AD20), CVertexWobble has no
//    destructor (0x0043BA70). That rules out those constructors, and
//    Effects_Electrify with them, since it does new CElectrify.
//
// 3. Rnd (utils.cpp) runs on three file-local statics that nothing seeds on
//    our side, because Utils_InitialRand is not hooked. So Rnd returns 0 in
//    every hooked function. That keeps CElectrify::ChooseRandomPositions
//    (0x00439170) out, it is all Rnd calls and would put every arc in the
//    same place.
//
// The Move overrides below are safe either way: they only touch the object and
// game memory, so it does not matter who built the object.
void patch_effects(void)
{
	PATCH_PUSH_RET(0x00438EE0, Effects_UnElectrify);
	PATCH_PUSH_RET(0x00439570, CElectro::Setup);

	PATCH_PUSH_RET_POLY(0x00439AE0, CElectro::~CElectro, "??1CElectro@@UAE@XZ");

	PATCH_PUSH_RET_POLY(0x0043A040, CPingLine::Move, "?Move@CPingLine@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x0043A290, CFootprint::Move, "?Move@CFootprint@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x0043A650, CRhinoWallImpact::Move, "?Move@CRhinoWallImpact@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x0043B1B0, CChunkSmoke::Move, "?Move@CChunkSmoke@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x0043B6B0, CBouncingRock::Move, "?Move@CBouncingRock@@UAEXXZ");
	PATCH_PUSH_RET_POLY(0x0043BB30, CVertexWobble::Move, "?Move@CVertexWobble@@UAEXXZ");
}
