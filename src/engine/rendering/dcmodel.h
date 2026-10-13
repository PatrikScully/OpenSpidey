#pragma once

#ifndef DCMODEL_H
#define DCMODEL_H

#include "compatibility/export.h"

EXPORT void DCClearSkater(void);


class DCStrip
{
	public:
		PADDING(8);

		void* field_8;

		EXPORT ~DCStrip(void);
};

class DCKeyFrame
{
	public:
		PADDING(44);

		DCKeyFrame *pNext;
		EXPORT ~DCKeyFrame(void);
};

class DCMaterial
{
	public:

		PADDING(0x10);

		void *field_10;

		PADDING(0x34-0x10-4);

		void *field_34;
		i32 field_38;

		PADDING(0x3F-0x38-4);

		u8 field_3F;

		EXPORT DCMaterial::~DCMaterial(void);
};

class DCObject;

class DCObjectList
{
	public:
		DCObject *pObject;

		EXPORT ~DCObjectList(void);
};

class DCObject
{
	public:

		PADDING(4);

		void *field_4;

		PADDING(0xD0-0x4-4);

		DCKeyFrame *field_D0;

		PADDING(0xE0-0xD0-4);

		void *field_E0;

		DCObjectList field_E4;

		DCObject *field_E8;

		PADDING(0x128-0xE8-4);

		void *field_128;
		void *field_12C;

		PADDING(4);

		DCStrip *field_134;

		EXPORT ~DCObject(void);
};

class DCSkaterModel
{
	public:

		i32 field_0;

		u8 field_4;
		
		PADDING(3);

		u8 field_8;

		PADDING(0x18-0x8-1);

		i32 field_18;
		i32 field_1C;
		i32 field_20;
		DCMaterial *field_24;
		DCObjectList field_28;

		EXPORT DCSkaterModel(void);


		EXPORT void ClearSkaterModel(void);
		EXPORT ~DCSkaterModel(void);
};


struct SModel;

// DCVert, DCFace, DCNormal, DCModelData: reverse engineered 2026-08-31 from IDA
// decompiles of DCModel_CreateFromSModel (0x431430, the constructor, writes
// every field below), DCModel_RenderModel (0x476D00), DC_PSXModel_RenderModel
// (0x478180, header check only) and RenderSuperItem (0x474C10, confirms the
// 36-byte stride and the "no dc model data" string). See the long comment on
// DCModel_CreateFromSModel in dcmodel.cpp for the full evidence per field.
// These are the RUNTIME model data the game builds once from a PSX-format
// SModel (spool.h) so the PC renderer does not have to re-decode PSX packets
// every frame.

// One welded vertex position + its per-vertex flags. 16 bytes, matches the
// stride implied by the vertex-conversion loop in DCModel_CreateFromSModel
// (writes 4 floats/16 bytes per source vertex).
struct DCVert
{
	// x/y/z: usually a plain int-to-float conversion of the source SModel
	// vertex (SVECTOR, 1/8 unit fixed point div by 8 first if the source
	// per-vertex flag bit 0x10 is set). CONFIRMED BY DISASSEMBLY (0x431430,
	// around 0x431cbd-0x431d08): when that source flag is clear, x/y/z go
	// through fild/fstp (real float conversion). When it is set, z still
	// gets a proper fild/fstp, but x and y are stored as the RAW INTEGER
	// bit pattern of ((u32)src >> 3), with NO float conversion at all. This
	// looks like a genuine original defect/quirk (probably meant to also go
	// through fild and never did); CLAUDE.md says reproduce the source bug,
	// not "fix" it, so DCModel_CreateFromSModel writes it exactly this way.
	f32 x;
	f32 y;
	f32 z;
	// Raw copy of the source per-vertex flags word (low 16 bits), OR'd with
	// a resolved "stitched normal" index in bits 16-23 when the source flag
	// bit 0x2 is set (index comes from the source vertex X field >> 3,
	// offset by the running gModelStitchNormalIndexBase counter). Stored as
	// a full i32 slot to keep the 16-byte stride. Confidence: high on the
	// low 16 bits (verbatim copy), medium on the exact meaning of the
	// stitch-index packing (transliterated from disassembly, not otherwise
	// cross-checked).
	i32 mFlags;
};

// One welded vertex normal. 12 bytes (x,y,z floats), confirmed by the
// normal-conversion loop in DCModel_CreateFromSModel (writes 3 floats per
// source normal, `12 * NumNormals` sized allocation).
struct DCNormal
{
	f32 x;
	f32 y;
	f32 z;
};

// One face record, 56 bytes. Confirmed stride: DCModel_CreateFromSModel's
// per-face loop pointer advances by exactly 56 bytes/iteration, and the
// allocation size term is `56 * NumFaces` (7 * NumFaces * 8, un-optimized by
// the compiler into that shape). Faces are always stored as quads (a
// triangle gets its 4th vertex/UV duplicated from the 3rd); the source
// SModel flag bit 0x10 marks "this is really a triangle".
struct DCFace
{
	// Low 16 bits of the source face flags dword, copied verbatim.
	u16 mFlags;
	// CLUT / texture id. Set to a constant 1 when the face is untextured
	// (source flag byte low 2 bits != 3, or the caller forced untextured
	// via DCModel_CreateFromSModel's bForceUntextured param); otherwise
	// copied from the source's texture-info struct (word at pTexInfo+2).
	u16 mTexIndex;
	// Up to 4 vertex indices into the model's DCVert array (index into the
	// WELDED/expanded array, i.e. DCModelData::mVertexCount range, not the
	// original SModel vertex count). For a triangle, index 3 duplicates
	// index 2.
	u8 mVertIndex[4];
	// R,G,B, gamma-corrected through gConvertedColors (see
	// PreComputeConvertedColors) UNLESS the source face flags have bit
	// 0x800 set, in which case the color is copied through unconverted
	// (tentative: "already-correct/raw color", e.g. a pulsing/animated
	// color that should not be re-gamma'd).
	u8 mColor[3];
	// 4th color byte, always copied through raw (never gamma corrected).
	// Guess: alpha. Low-medium confidence, not cross-checked against a
	// renderer read.
	u8 mColorExtra;
	// Per-corner resolved vertex "slot" (u16, one per corner, matching
	// mVertIndex's 4 slots), high bit (0x8000) used as a "this corner was
	// matched/merged with a neighbouring face during welding" marker. Built
	// by the adjacency/stitching search in DCModel_CreateFromSModel and
	// read back by DCModel_RenderModel's per-face vertex loop (confirmed:
	// RenderModel indexes off `pFaces + 12 + 2*corner` at face granularity,
	// which is exactly this array). Medium confidence on the exact
	// semantics of the high bit; the underlying index itself is
	// higher-confidence since both the writer and a reader were seen.
	u16 mVertSlot[4];
	// Per-corner U texture coordinates (4 corners).
	f32 mU[4];
	// Per-corner V texture coordinates (4 corners).
	f32 mV[4];
	// Trailing 4 bytes. Zeroed at the start of face processing in
	// DCModel_CreateFromSModel (two word stores); not otherwise written by
	// the constructor. Likely runtime/per-frame scratch filled in by the
	// renderer (DCModel_RenderModel/DC_PSXModel_RenderModel are not fully
	// decompiled yet, so this is unconfirmed). Low confidence.
	u8 field_34[4];
};

// The DCModelData struct itself. Size CONFIRMED as exactly 0x24 (36) bytes
// two independent ways: (1) DCModel_CreateFromSModel's only caller,
// M3dInit_ParsePSX (0x4534A0), allocates `36 * partCount` bytes
// (sub_505470(Size: 36 * v13)) and calls sub_431430(a1: partIndex*36 + base,
// ...) per part; (2) RenderSuperItem (0x474C10) independently computes
// `dword_5F6764[region] + 36 * partIndex` right before the "no dc model
// data" nullsub_1 check and the DCModel_RenderModel/DC_PSXModel_RenderModel
// dispatch. ps2m3d.cpp's existing gM3dBackgroundModelData usage
// (`gM3dBackgroundModelData[region] + 36 * modelIndex`) is a third,
// independent confirmation already in the tree before this session.
struct DCModelData
{
	// Welded vertex array, mVertexCount entries (>= mNumVertices when the
	// welding pass split any UV-seam/flat-shaded vertices). High confidence:
	// allocated as `round4(16 * NumVertices)` bytes, walked with a 16-byte
	// stride matching DCVert, and read back by DCModel_RenderModel's vertex
	// transform loop (`v194 = *a2; ... (float*)(v194+12)`, 16-byte stride).
	DCVert *pVertices;
	// Face array, mNumFaces entries, 56 bytes/entry (DCFace). High
	// confidence: allocated as `round4(56 * NumFaces)` bytes and confirmed
	// read by DCModel_RenderModel (`v181 = a2[1]`, walked with a 56-byte
	// per-face stride).
	DCFace *pFaces;
	// Normal array, allocated as `round4(12 * NumNormals)` bytes. Never
	// directly read back by CreateFromSModel itself after being written, but
	// the allocation math and 12-byte-per-entry write loop are unambiguous.
	// Not yet cross-checked against a RenderModel read (RenderModel was not
	// fully decompiled this session), so "used only via DCVert/DCFace at
	// render time" is a reasonable guess, not a confirmed fact.
	DCNormal *pNormals;
	// Bitfield of model-wide flags. HIGH CONFIDENCE this is the field:
	// DCModel_RenderModel reads it at this exact offset
	// (`v4 = a2[3]` where a2 is DCModelData* cast to i32*, i.e. a2[3] =
	// offset 0xC) and tests bit 0x10/0x100 to bail out of rendering
	// entirely, and RenderSuperItem/M3d_RenderBackground (both already in
	// this repo) read the SAME offset (`*(i32*)(pModelData+0xC)`) to test
	// bit 0x100 (skip) and bit 0x4000 (pick DC_PSXModel_RenderModel over
	// DCModel_RenderModel). Bits confirmed SET by DCModel_CreateFromSModel:
	// CORRECTED 2026-08-31 (re-verified against a fresh IDA decompile AND raw
	// disassembly of 0x431430 for this session's task): the previous version
	// of this comment had 0x001/0x004's conditions backwards and mislabeled
	// 0x800 as 0x008. Fixed below; see dcmodel.cpp for the exact asm evidence
	// (raw disasm at 0x4323be/0x4323e2 for 0x001/0x004, `or ch, 8` at
	// 0x4324d8 for 0x800, and the distinct-mTexIndex scan at 0x43240d for
	// 0x002).
	//   0x001 - set if any of the model's ORIGINAL vertices (0..NumVertices)
	//           has its per-vertex flags byte bits 0-1 NOT both clear (i.e.
	//           (flags & 3) != 0 for at least one vertex). Low confidence on
	//           meaning.
	//   0x002 - set if faces flagged with mFlags bit 0x1 use more than one
	//           DISTINCT DCFace::mTexIndex value (a "this model uses more
	//           than one texture" flag). Confirmed via raw disasm at
	//           0x43240d-0x432433: walks DCFace::mTexIndex (offset 2) gated
	//           by DCFace::mFlags bit 0. NOT related to stitched normals
	//           (previous guess was wrong).
	//   0x004 - set if any of the model's ORIGINAL vertices has its flags
	//           byte bit 0x10 SET (not clear). Low confidence on meaning,
	//           but this and bit 0x001 are computed by a near-identical pair
	//           of loops, so they are likely a related pair (e.g. "has flat"
	//           / "has smooth" vertices).
	//   0x800 - set if any face's texture (PCTex_GetTextureFlags(mTexIndex),
	//           now decompiled and @Ok in PCTex.cpp) has its flag bit 0x10
	//           set. Medium confidence: "has a transparent/blended face"
	//           (matches the file's existing @Ok DCMaterial destructor,
	//           which special-cases texture release, and the general
	//           PVR/PCTex "transparency" bit convention already documented
	//           in CLAUDE.md). CORRECTED: raw disasm shows `or ch, 8` at
	//           0x4324d8, i.e. bit 3 of the flags dword's byte 1 = bit 0x800
	//           of the whole dword, not 0x008 as previously written.
	//   0x080 - set if a face is flagged "pulsing" (source face flags byte1
	//           bit 0x8) AND its color bytes match one of up to 3 entries in
	//           the a4 (pPulseColorList) array passed into
	//           DCModel_CreateFromSModel. Medium-high confidence: a4 is
	//           built by the caller (M3dInit_ParsePSX) from a PSX
	//           "colour pulsing packet", so 0x080 = "has an animated/pulsing
	//           color".
	//   0x400 - set unconditionally when DCModel_CreateFromSModel's a3
	//           (texture-format flags) parameter has bit 0 CLEAR. Low
	//           confidence on meaning ("new-style UV encoding" model?).
	// Bits confirmed TESTED but never set by this constructor (so they must
	// be set by some other, not-yet-found code path, or default to 0):
	//   0x100  - sk render entirely (DCModel_RenderModel, RenderSuperItem).
	//   0x4000 - use DC_PSXModel_RenderModel instead of DCModel_RenderModel
	//            (RenderSuperItem, M3d_RenderBackground).
	i32 mFlags;
	// Final vertex count in pVertices AFTER welding (can exceed
	// mNumVertices when the construction pass splits a vertex at a UV seam
	// or a flat/smooth shading boundary). High confidence: written as the
	// final value of the loop counter that also sizes the "vertex in faces"
	// scratch table, and read back by DCModel_RenderModel as its vertex loop
	// bound (`v199 = a2[4]`, looped with `v199 > 0`/`n = v199`).
	i32 mVertexCount;
	// Original SModel::NumFaces, zero-extended to i32. High confidence
	// (verbatim copy of the source header field, also read back by
	// DCModel_RenderModel as a stats-counter accumulator).
	i32 mNumFaces;
	// Original SModel::NumVertices, zero-extended to i32. High confidence
	// (verbatim copy of the source header field, also read back by
	// DCModel_RenderModel, again only as a stats-counter accumulator).
	i32 mNumVertices;
	// Per-part sort/depth bias, used by DCModel_RenderModel ONLY when
	// gLowGraphics (PCTex.h's G_LOWGRAPHICS game address, 0x6B78F8) is 0.
	// Both this and mSortBiasLowGraphics are loaded from a small,
	// level-specific "offsets\<name>.off" text table (dword_5F6A60 in IDA,
	// parsed by sub_501120, 3 integers per line: part index, then these two
	// values) keyed by this model's part index (the constructor's a6
	// param). Medium confidence: the offset/bias role is inferred from the
	// literal file name ("offsets") and from DCModel_RenderModel adding this
	// value into a per-face sort/OT key (`dword_AC08E0 = v208 + v108`)
	// before a Z/order-table style insertion; not independently confirmed
	// against a second source.
	i32 mSortBiasNormal;
	// Same as mSortBiasNormal, but used when gLowGraphics is nonzero.
	i32 mSortBiasLowGraphics;
};

EXPORT void DCModel_CreateFromSModel(DCModelData *,SModel *,i32,i32 *,bool,i32);
EXPORT void PreComputeConvertedColors(f32);

EXPORT extern u8 gConvertedColors[256];

void validate_DCSkaterModel(void);
void validate_DCMaterial(void);
void validate_DCObject(void);
void validate_DCStrip(void);
void validate_DCObjectList(void);
void validate_DCKeyFrame(void);
void validate_DCModelData(void);

#endif
