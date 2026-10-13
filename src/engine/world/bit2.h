#pragma once

#ifndef BIT2_H
#define BIT2_H


#include "engine/world/bit.h"

struct SLineSeg
{
	CVector End;

	u8 r;
	u8 g;
	u8 b;
	u8 code;
};

class CGPolyLine : public CBit {
public:

	EXPORT CGPolyLine(i32);
	EXPORT ~CGPolyLine();
	EXPORT void SetSemiTransparent(void);
	EXPORT void SetStartAndEnd(CVector*, CVector*);

	i32 mNumSegs;

	SLineSeg* mSegs;
	CVector mStart;
	
	u8 mStartR;
	u8 mStartG;
	u8 mStartB;
	u8 mCode;

	PADDING(0x3);

	u8 field_57;
};

// Per-segment metadata for CKnottedWeb's own extra (dangling-bit) array, one entry per base
// segment (mNumSegs entries, 28 bytes each, DCMem_New'd separately from the inherited mSegs
// array). Field names are placeholders: the values are confirmed byte-for-byte from the
// disasm (0x4F8E20), but which of field_D/field_10/field_11 is really "r"/"g"/"b" (they are
// filled with three different Rnd() ranges) is not confirmed against source.
// mPos (offset 0, was blanket PADDING(0xC)): CKnottedWeb's own constructor (0x4F8E20) never
// writes here, so it looked like pure padding when this struct was first reverse engineered.
// CSwinger_SwingBack (0x4F7550, web.cpp) proves it is a real CVector: it copies each base
// segment's mSegs[i].End straight into mpExtraSegs[i]'s first 12 bytes (three i32 stores at
// offsets 0/4/8, stride 0x1C between elements, matching this struct's own 28-byte size).
struct SKnottedWebSeg
{
	CVector mPos;

	u8 field_C;
	u8 field_D;
	i16 field_E;
	u8 field_10;
	u8 field_11;

	PADDING(0x2);

	CFlatBit *mpBit;

	PADDING(0x4);
};

// Reverse engineered 2026-08-31 (CheckForPadUnplugged chain, functional decompile session).
// Address 0x4F8E20 (Mac mangled name confirms it: idbs/spiderman_names.txt
// ".__ct__11CKnottedWebFRC7CVectorRC7CVector", right next to CDropDownController and
// CheckForPadUnplugged in the Mac symbol table). Base is CGPolyLine (0x412350,
// "??0CGPolyLine@@QAE@H@Z" in names.json), confirmed by the ctor's first real call being
// CGPolyLine::CGPolyLine(numSegs), and by *(this+0x3C) (CGPolyLine::mNumSegs) being read
// before this class writes it. Draws a "knotted" length of spider web between two points:
// the base CGPolyLine's own segments (recoloured 0xA2/0xA2/0xA2), a second embedded
// CGPolyLine with twice as many segments (mpInnerLine), and mNumSegs dangling CFlatBit
// sprites (mpExtraSegs), one per base segment.
class CKnottedWeb : public CGPolyLine
{
public:

	EXPORT CKnottedWeb(const CVector&, const CVector&);
	EXPORT virtual ~CKnottedWeb(void);
	EXPORT virtual void Move(void);

	i32 field_58;
	i32 field_5C;
	i32 field_60;

	SKnottedWebSeg *mpExtraSegs;
	CGPolyLine *mpInnerLine;

	// CWeb::Fire (0x4F5ED0, web.cpp) sets this to 1 when the player's current web shot is
	// the forward one (CPlayer::field_8F8 == 1), the same way it sets field_6E for the yank
	// shot. Was the head of a blanket PADDING(0x2).
	u8 field_6C;

	// Was the tail of a blanket PADDING(0x2). CSwinger_SwingBack (0x4F7550, web.cpp) writes
	// byte 1 here (`mov byte ptr [esi+6Dh], 1`) on a CKnottedWeb-shaped object it built and
	// then reclassified via a manual vtable poke (see that function's own comment); this
	// constructor (0x4F8E20) never touches 0x6D itself. Declared on CKnottedWeb directly
	// since the byte sits inside CKnottedWeb's own already-validated layout either way.
	u8 field_6D;

	// touched by CDropDownController::AI (0x48E930) once the drop finishes; exact meaning
	// (a "settled"/"snapped" flag on the inner line, guessed) not confirmed.
	u8 field_6E;
	u8 field_6F;

	// set to 1 right after construction; same CBit::mProtected-style "don't reap me yet"
	// convention seen elsewhere in this constructor, but at an offset past CGPolyLine's own
	// fields, so it must be CKnottedWeb's own flag, not the inherited mProtected.
	u8 field_70;

	PADDING(0x3);

	// Was the tail of a blanket PADDING(7). CSwinger_SwingBack (0x4F7550, web.cpp) is the
	// only place found writing here (`mov [esi+74h], ecx`, a plain i32 copied verbatim from
	// the calling CSwinger's own field_F8, see web.h); this constructor never initializes
	// it. Still inside CKnottedWeb's own already-validated 0x78-byte size, so declared here
	// rather than inventing a separate subclass just for this one field.
	i32 field_74;
};

// The 0x7C-byte loose strand created by CSwinger_SwingBack.
class CSwingBack : public CKnottedWeb
{
public:
	EXPORT CSwingBack(const CVector&, const CVector&);
	EXPORT virtual void Move(void);
	i32 mRetracting;
};

class CGLine : public CBit
{
	public:
		EXPORT void SetRGB1(u8, u8, u8);
		EXPORT void SetRGB0(u8, u8, u8);

		EXPORT CGLine(void);
		EXPORT virtual ~CGLine(void);

		u32 mCodeBGR0;
		u32 mPadBGR1;

		CVector mStart;

		CVector mEnd;
};

class CPolyLine : public CBit
{
	public:
		EXPORT CPolyLine(i32);
		EXPORT virtual ~CPolyLine(void) OVERRIDE;

		EXPORT void SetSemiTransparent(void);

		PADDING(4);

		i32 mNumSegs;
		SLineSeg* mSegs;
		CVector mStart;
};

class CSmokeGenerator : public CNonRenderedBit
{
	public:
		EXPORT CSmokeGenerator(const CVector *,i32,i32,u8,u8,u8,i32,i32,i32,i32);
		EXPORT virtual void Move(void) OVERRIDE;
		EXPORT virtual ~CSmokeGenerator(void) OVERRIDE;

		i32 mPuffs;

		u8 mR;
		u8 mG;
		u8 mB;

		PADDING(1);

		i32 mVBase;
		i32 mVRandom;

		i32 mScaleBase;
		i32 mScaleRandom;
};

class CGLineParticle : public CGLine
{
	public:
		EXPORT CGLineParticle(CVector &,CVector &,u16,i32);
		EXPORT virtual ~CGLineParticle(void) OVERRIDE;

		EXPORT virtual void Move(void) OVERRIDE;

		i32 field_5C;
};

void validate_CGPolyLine(void);
void validate_CPolyLine(void);
void validate_CGLine(void);
void validate_SLineSeg(void);
void validate_CSmokeGenerator(void);
void validate_CGLineParticle(void);
void validate_CKnottedWeb(void);
void validate_SKnottedWebSeg(void);

void patch_bit2(void);

#endif
