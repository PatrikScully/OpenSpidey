#include "engine/rendering/m3dutils.h"
#include "compatibility/validate.h"

#include "engine/assets/spool.h"
#include "engine/rendering/animation_decoder.h"

#include "compatibility/my_assert.h"

// @Ok
// @Matching
void M3dUtils_ReadLinksPacket(CSuper* pSuper, void* pPacket)
{
	i32 NumJoints = reinterpret_cast<u16*>(pPacket)[1];
	pSuper->mpLinks = reinterpret_cast<SLink*>(reinterpret_cast<i32>(pPacket) + 4);

	pSuper->mpPoseBuffer = static_cast<SMatrix*>(Mem_New(sizeof(SMatrix) * G_PSXREGION[pSuper->mRegion].NumParts));

	pSuper->mpJoints = static_cast<SJoint*>(Mem_New(sizeof(SJoint) * NumJoints));

	for (i32 i = 0; i < NumJoints; i++)
	{
		pSuper->mpJoints[i].Displacement.vz = 0;
		pSuper->mpJoints[i].Displacement.vy = 0;
		pSuper->mpJoints[i].Displacement.vx = 0;

		pSuper->mpJoints[i].Angles.vz = 0;
		pSuper->mpJoints[i].Angles.vy = 0;
		pSuper->mpJoints[i].Angles.vx = 0;
	}

	for (i32 j = 0; j < NumJoints; j++)
	{
		i32 k;
		for (k = 0; k < NumJoints; k++)
		{
			if (pSuper->mpLinks[j].ParentPart == pSuper->mpLinks[k].Part)
			{
				pSuper->mpLinks[j].ParentLink = k;
				break;
			}
		}

		if (k == NumJoints)
		{
			pSuper->mpLinks[j].ParentLink = 0xFFFF;
		}
	}
}

// @Ok
// @Matching
void M3dUtils_InBetween(CSuper *pSuper)
{
	u32 *pAnimFile = G_PSXREGION[pSuper->mRegion].pAnimFile;
    i32 Interval =  (pAnimFile[2 * pSuper->mAnim + 2] >> 16) + 1;
    
	if (Interval != 1)
	{
		i32 NumParts = G_PSXREGION[pSuper->mRegion].NumParts;
		ASSERT(NumParts <= 30, "Too many parts for TweenBuffer");

		M3dUtils_InterpolateVectors(
				4 * NumParts,
				Interval,
				pAnimFile,
				pSuper,
				0,
				NumParts);
	}
}

// Scratch buffer the animation system tweens part-local pose matrices into
// when the current anim frame does not land exactly on a keyframe (see the
// "Too many parts for TweenBuffer" ASSERT in M3dUtils_InBetween and the
// M3dUtils_InterpolateVectors write target). Fixed game address, confirmed
// from the original disassembly of M3dUtils_InterpolateVectors,
// M3dUtils_GetHookPosition and M3dUtils_GetDynamicHookPosition, all of
// which reference 0x5FC250 directly. Sized for up to 30 parts (SMatrix, 24
// bytes each).
static SMatrix* const gTweenBuffer = reinterpret_cast<SMatrix*>(0x5FC250);

// @Ok
// Composes two rigid part transforms (SMatrix: i16 3x3 rotation + i16
// translation), pDst = pParent * pChild: rotation is a plain 3x3 product
// (result[row][col] = sum_k parent.m[row][k] * child.m[k][col]), each
// k-term individually shifted >>12 (fixed point) before being summed;
// translation is parent.Rot*child.t + parent.t, with the three products
// summed first and shifted >>12 once. pDst may alias pParent or pChild
// (matches the original, which calls this in place). Unnamed in the IDB,
// address 0x0046E4F0; decompiled 2026-08-31 while tracing
// M3dUtils_BuildPose's callees. Kept file-local (only BuildPose's
// hierarchy composition uses it).
static void M3dUtils_ComposeTransform(SMatrix* pDst, const SMatrix* pParent, const SMatrix* pChild)
{
	SMatrix Tmp;

	for (i32 row = 0; row < 3; row++)
	{
		for (i32 col = 0; col < 3; col++)
		{
			i32 Sum = 0;
			for (i32 k = 0; k < 3; k++)
				Sum += (pParent->m[row][k] * pChild->m[k][col]) >> 12;

			Tmp.m[row][col] = static_cast<i16>(Sum);
		}
	}

	Tmp.t[0] = static_cast<i16>(pParent->t[0] + ((pChild->t[0] * pParent->m[0][0] + pChild->t[2] * pParent->m[0][2] + pChild->t[1] * pParent->m[0][1]) >> 12));
	Tmp.t[1] = static_cast<i16>(pParent->t[1] + ((pChild->t[2] * pParent->m[1][2] + pChild->t[1] * pParent->m[1][1] + pChild->t[0] * pParent->m[1][0]) >> 12));
	Tmp.t[2] = static_cast<i16>(pParent->t[2] + ((pChild->t[0] * pParent->m[2][0] + pChild->t[1] * pParent->m[2][1] + pChild->t[2] * pParent->m[2][2]) >> 12));

	*pDst = Tmp;
}

// @Ok
// pDst = pParent * inverse(pChild), where inverse() of a rigid transform
// is Rot^T and -(Rot^T * t) (negation happens BEFORE the >>12, matching
// the original's operator precedence exactly, which matters for
// negative-value rounding). Unnamed in the IDB, address 0x0046E660;
// calls the same composition as M3dUtils_ComposeTransform above (0x46E4F0)
// with the inverted child. Only used by M3dUtils_BuildPose, to fold a
// just-processed joint's absolute transform back into a "delta relative
// to its own raw keyframe matrix" form for descendant joints to look up.
static void M3dUtils_ComposeWithInverse(SMatrix* pDst, const SMatrix* pParent, const SMatrix* pChild)
{
	SMatrix Inv;

	for (i32 row = 0; row < 3; row++)
		for (i32 col = 0; col < 3; col++)
			Inv.m[row][col] = pChild->m[col][row];

	i32 tx = pChild->t[0];
	i32 ty = pChild->t[1];
	i32 tz = pChild->t[2];

	Inv.t[0] = static_cast<i16>(-(tz * Inv.m[0][2] + ty * Inv.m[0][1] + tx * Inv.m[0][0]) >> 12);
	Inv.t[1] = static_cast<i16>(-(tz * Inv.m[1][2] + ty * Inv.m[1][1] + tx * Inv.m[1][0]) >> 12);
	Inv.t[2] = static_cast<i16>(-(tz * Inv.m[2][2] + ty * Inv.m[2][1] + tx * Inv.m[2][0]) >> 12);

	M3dUtils_ComposeTransform(pDst, pParent, &Inv);
}

// @Ok
// Decompiled 2026-08-31 from Hex-Rays/disasm at 0x00454450. Builds the
// per-part absolute pose (pSuper->mpPoseBuffer) for the current frame by
// walking the joint/link hierarchy (mpJoints/mpLinks, built by
// M3dUtils_ReadLinksPacket): every joint with a nonzero local rotation
// (Angles) or offset (Displacement) gets that composed onto its part's
// keyframe pose (from Decomp_GetAnimTransform, which itself may return
// the interpolated gTweenBuffer or the raw decompressed frame), then
// composed onto its nearest already-processed ancestor's transform
// (found via a small fixed 6-entry scratch stack, searched by
// SLink::ParentLink against the producing joint's loop index -- exact
// mechanism reproduced from the disassembly, see
// M3dUtils_ComposeWithInverse above for why entries store a transform
// "relative to their own raw keyframe matrix"). Joints with no local
// adjustment either compose their ancestor directly with the raw
// keyframe part matrix, or (no ancestor, i.e. root) copy the raw
// keyframe matrix straight through. Any part never touched by a joint
// (still holding the 0x8000 sentinel written up front) is filled from
// the raw keyframe array afterward, but only on an exact (non-tweened)
// frame, matching the original's guard.
void M3dUtils_BuildPose(CSuper* pSuper)
{
	if ((pSuper->mFlags & 4) == 0 || pSuper->mpPoseBuffer == NULL || pSuper->mpJoints == NULL)
		return;

	i32 NumParts = G_PSXREGION[pSuper->mRegion].NumParts;
	u32* pAnimFile = G_PSXREGION[pSuper->mRegion].pAnimFile;

	for (i32 i = 0; i < NumParts; i++)
		pSuper->mpPoseBuffer[i].m[0][0] = static_cast<i16>(0x8000);

	i32 NumJoints = *(reinterpret_cast<u16*>(pSuper->mpLinks) - 1);
	SMatrix* pKeyframeArray = Decomp_GetAnimTransform(pSuper);

	struct SPoseStackEntry
	{
		SMatrix Transform;
		i32 JointIndex;
	};

	SPoseStackEntry Stack[6];
	i32 StackCount = 0;

	for (i32 j = 0; j < NumJoints; j++)
	{
		SLink* pLink = &pSuper->mpLinks[j];
		SJoint* pJoint = &pSuper->mpJoints[j];
		i32 Part = pLink->Part;

		i32 FoundIndex = -1;
		for (i32 s = StackCount - 1; s >= 0; s--)
		{
			if (pLink->ParentLink >= Stack[s].JointIndex)
			{
				FoundIndex = s;
				break;
			}
		}

		// Drop branches that are not ancestors of this joint.
		StackCount = FoundIndex + 1;

		bool HasLocal =
			pJoint->Angles.vx != 0 || pJoint->Angles.vy != 0 || pJoint->Angles.vz != 0 ||
			pJoint->Displacement.vx != 0 || pJoint->Displacement.vy != 0 || pJoint->Displacement.vz != 0;

		if (HasLocal)
		{
			SMatrix KeyPart = pKeyframeArray[Part];
			KeyPart.t[0] = static_cast<i16>(KeyPart.t[0] + pJoint->Displacement.vx);
			KeyPart.t[1] = static_cast<i16>(KeyPart.t[1] + pJoint->Displacement.vy);
			KeyPart.t[2] = static_cast<i16>(KeyPart.t[2] + pJoint->Displacement.vz);

			SMatrix Local;
			// only writes Local.m (the 9 i16 rotation entries); MATRIX and
			// SMatrix share that layout at offset 0, and RotMatrixYXZ never
			// touches translation, so this reinterpret is safe (same idiom
			// used at every other M3dMaths_RotMatrixYXZ call site in the repo).
			M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR*>(&pJoint->Angles), reinterpret_cast<MATRIX*>(&Local));
			Local.t[0] = 0;
			Local.t[1] = 0;
			Local.t[2] = 0;

			M3dUtils_ComposeTransform(&Local, &KeyPart, &Local);

			if (FoundIndex >= 0)
				M3dUtils_ComposeTransform(&Local, &Stack[FoundIndex].Transform, &Local);

			print_if_false(StackCount < 6, "Matrix stack overflow");

			M3dUtils_ComposeWithInverse(&Stack[StackCount].Transform, &Local, &pKeyframeArray[Part]);
			Stack[StackCount].JointIndex = j;
			StackCount++;

			pSuper->mpPoseBuffer[Part] = Local;
		}
		else if (FoundIndex >= 0)
		{
			M3dUtils_ComposeTransform(&pSuper->mpPoseBuffer[Part], &Stack[FoundIndex].Transform, &pKeyframeArray[Part]);
		}
		else
		{
			pSuper->mpPoseBuffer[Part] = pKeyframeArray[Part];
		}
	}

	if ((pAnimFile[2 * pSuper->mAnim + 2] & 0xFFFF0000) == 0 && NumParts > 0)
	{
		for (i32 i = 0; i < NumParts; i++)
		{
			if (pSuper->mpPoseBuffer[i].m[0][0] == static_cast<i16>(0x8000))
				pSuper->mpPoseBuffer[i] = pKeyframeArray[i];
		}
	}
}

// @Ok
// Decompiled 2026-08-31 from Hex-Rays/disasm at 0x00454270. Linearly
// interpolates NumVectors 3xi16 "vectors" (rows of the part pose
// matrices: an SMatrix is 4 such vectors, m[0..2] then t) between the
// two keyframes straddling the current frame, or copies the exact
// keyframe data through when the frame lands exactly on one (FracScaled
// == 0). Writes into gTweenBuffer starting at part index Part.
// pSuper->mAnimMode selects clamp-at-the-last-interval (0, set by
// CSuper::RunAnim) vs loop-back-to-frame-0 (1, set by CSuper::CycleAnim)
// behaviour once the frame passes the last full interval. Fixed-point
// fraction is scaled by 4096 (<<12), same convention as the rest of the
// GTE-shaped animation code (gte_lddp/MTC2/gsub_46E090/gsub_46E430,
// already implemented in ps2funcs.cpp).
void M3dUtils_InterpolateVectors(i32 NumVectors, i32 Interval, u32* pAnimFile, CSuper* pSuper, i32 Part, i32 NumParts)
{
	i32 Anim = pSuper->mAnim;
	i32 Frame = pSuper->mFrame;

	u8* pFrameBase = reinterpret_cast<u8*>(pAnimFile) + pAnimFile[2 * Anim + 1];
	i32 NumFrames = static_cast<u16>(pAnimFile[2 * Anim + 2]);

	i32 LowerFrame = Frame - Frame % Interval;
	i32 UpperFrameRaw = LowerFrame + Interval;
	i32 DivUpper = NumFrames;
	i32 FracScaled;

	if (UpperFrameRaw < NumFrames)
	{
		DivUpper = UpperFrameRaw;
		FracScaled = ((Frame - LowerFrame) << 12) / (DivUpper - LowerFrame);
	}
	else if (pSuper->mAnimMode != 1)
	{
		LowerFrame -= Interval;
		if (LowerFrame < 0)
			LowerFrame = 0;

		UpperFrameRaw -= Interval;

		if (UpperFrameRaw == 0)
		{
			FracScaled = 0;
		}
		else
		{
			DivUpper = UpperFrameRaw;
			FracScaled = ((Frame - LowerFrame) << 12) / (DivUpper - LowerFrame);
		}
	}
	else
	{
		UpperFrameRaw = 0;
		FracScaled = ((Frame % Interval) << 12) / (DivUpper - LowerFrame);
	}

	i32 LowerIndex = NumParts * LowerFrame / Interval;
	i16* pSrc0 = reinterpret_cast<i16*>(pFrameBase + 24 * (LowerIndex + Part));
	i16* pDest = reinterpret_cast<i16*>(reinterpret_cast<u8*>(gTweenBuffer) + 24 * Part);

	if (FracScaled != 0)
	{
		i32 UpperIndex = NumParts * UpperFrameRaw / Interval;
		i32 KeyframeStride = 24 * (UpperIndex - LowerIndex);

		gte_lddp(FracScaled);

		for (i32 v = 0; v < NumVectors; v++)
		{
			i16* pV0 = pSrc0 + 3 * v;
			i16* pV1 = reinterpret_cast<i16*>(reinterpret_cast<u8*>(pSrc0) + KeyframeStride) + 3 * v;

			i32 x0 = pV0[0], y0 = pV0[1], z0 = pV0[2];
			i32 dx = pV1[0] - x0;
			i32 dy = pV1[1] - y0;
			i32 dz = pV1[2] - z0;

			MTC2(x0, GT_ELEVEN);
			MTC2(y0, GT_TWELVE);
			MTC2(z0, GT_THIRTEEN);

			MTC2(dx, GT_SEVEN);
			MTC2(dy, GT_EIGHT);
			MTC2(dz, GT_NINE);

			gsub_46E090();
			gsub_46E430(pDest + 3 * v);
		}
	}
	else
	{
		for (i32 v = 0; v < NumVectors; v++)
		{
			i16* pV0 = pSrc0 + 3 * v;
			i16* pD = pDest + 3 * v;

			pD[0] = pV0[0];
			pD[1] = pV0[1];
			pD[2] = pV0[2];
		}
	}
}

// @Ok
// @AlmostMatching: 18 source variants tested. At 0x45411F/0x454125,
// MSVC folds the fixed tween-buffer address into the index before shifting.
// The original uses two LEAs; all other instructions match after relocation.
// A real array reproduces those LEAs but does not read the game buffer.
// 0x454040. Resolve a part's world rotation, with an optional local offset.
i32 M3dUtils_GetPartAngles(CSuper* pSuper, i32 part, CSVector* pAngles, CSVector* pOffset)
{
	print_if_false(pSuper != 0, "NULL pSuper sent to M3dUtils_GetPartAngles");
	print_if_false(pSuper->mRegion < 40, "Bad mRegion in pSuper");
	print_if_false(G_PSXREGION[pSuper->mRegion].Usable != 0, "mRegion not usable");
	SPSXRegion* pRegion = &G_PSXREGION[pSuper->mRegion];
	i32 anim = pSuper->mAnim;
	u32* pAnimFile = pRegion->pAnimFile;
	i32 valid;
	if (part < 0 || part >= pRegion->NumParts)
		valid = 0;
	else
		valid = 1;
	print_if_false(valid, "Bad part number sent to M3dUtils_GetPartAngles");
	SMatrix* pPose;
	if (pSuper->mFlags & 4)
		pPose = pSuper->mpPoseBuffer + part;
	else
	{
		u32 interval = pAnimFile[2 * anim + 2];
		if (interval & 0xFFFF0000)
		{
			M3dUtils_InterpolateVectors(4, (interval >> 16) + 1, pAnimFile,
					pSuper, part, pRegion->NumParts);
			pPose = gTweenBuffer + part;
		}
		else
			pPose = Decomp_GetAnimTransform(pSuper) + part;
	}
	MATRIX offset;
	MATRIX world;
	if (pOffset)
		M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR*>(pOffset), &offset);
	else
		M3dMaths_SetIdentityRotation(&offset);
	M3dMaths_CopyMat(&pSuper->mTransform, &world);
	MulMatrix(&world, reinterpret_cast<MATRIX*>(pPose));
	MulMatrix(&world, &offset);
	i32 x = world.m[0][2];
	i32 yx = world.m[1][0];
	i32 yz = world.m[1][2];
	i32 z = world.m[2][2];
	i32 yy = world.m[1][1];
	i32 length = M3dMaths_SquareRoot0(x * x + z * z);
	pAngles->vx = ratan2(-yz, length);
	pAngles->vy = ratan2(x, z);
	i32 result = ratan2(yx, yy);
	pAngles->vz = result;
	return result;
}

// @Ok
void M3dUtils_GetHookPosition(VECTOR* pOut, CSuper* pSuper, i32 hookIndex)
{
	SHook* pHook = &G_PSXREGION[pSuper->mRegion].pHooks[hookIndex];
	u16 PartIndex = pHook->Offset;
	u16 Anim = pSuper->mAnim;

	// mirror the part-0 column of the world transform while this hook is
	// resolved, then flip it back at the end (see the matching block below)
	if (pSuper->mExtraFlags & 2)
	{
		pSuper->mTransform.m[0][0] = -pSuper->mTransform.m[0][0];
		pSuper->mTransform.m[1][0] = -pSuper->mTransform.m[1][0];
		pSuper->mTransform.m[2][0] = -pSuper->mTransform.m[2][0];
	}

	SMatrix* pPoseFrame;

	if (pSuper->mFlags & 4)
	{
		if (pSuper->mpPoseBuffer == NULL)
		{
			pOut->vx = 0;
			pOut->vy = 0;
			pOut->vz = 0;
			return;
		}

		pPoseFrame = pSuper->mpPoseBuffer + PartIndex;
	}
	else
	{
		u32* pAnimFile = G_PSXREGION[pSuper->mRegion].pAnimFile;
		u32 IntervalWord = pAnimFile[2 * Anim + 2];

		if (IntervalWord & 0xFFFF0000)
		{
			M3dUtils_InterpolateVectors(
					4,
					(IntervalWord >> 16) + 1,
					pAnimFile,
					pSuper,
					PartIndex,
					G_PSXREGION[pSuper->mRegion].NumParts);

			pPoseFrame = gTweenBuffer + PartIndex;
		}
		else
		{
			pPoseFrame = Decomp_GetAnimTransform(pSuper) + PartIndex;
		}
	}

	gsub_46F820(pHook, pPoseFrame, &pSuper->mTransform);
	gte_stlvnl(pOut);

	pOut->vx <<= 12;
	pOut->vy <<= 12;
	pOut->vz <<= 12;

	pOut->vx = pSuper->mPos.vx + ((pOut->vx - pSuper->mPos.vx) >> 4);
	pOut->vy = pSuper->mPos.vy + ((pOut->vy - pSuper->mPos.vy) >> 4);
	pOut->vz = pSuper->mPos.vz + ((pOut->vz - pSuper->mPos.vz) >> 4);

	if (pSuper->mExtraFlags & 2)
	{
		pSuper->mTransform.m[0][0] = -pSuper->mTransform.m[0][0];
		pSuper->mTransform.m[1][0] = -pSuper->mTransform.m[1][0];
		pSuper->mTransform.m[2][0] = -pSuper->mTransform.m[2][0];
	}
}

// @Ok
void M3dUtils_GetDynamicHookPosition(VECTOR* pOut, CSuper* pSuper, SHook* pHook)
{
	u16 PartIndex = pHook->Offset;
	u16 Anim = pSuper->mAnim;

	print_if_false(G_PSXREGION[pSuper->mRegion].ppModels != NULL,
			"Tried to get hook position with no model table");

	print_if_false(PartIndex < G_PSXREGION[pSuper->mRegion].NumParts,
			"Bad part number sent to M3dUtils_GetDynamicHookPosition");

	SMatrix* pPoseFrame;

	if (pSuper->mFlags & 4)
	{
		if (pSuper->mpPoseBuffer == NULL)
		{
			pOut->vx = 0;
			pOut->vy = 0;
			pOut->vz = 0;
			return;
		}

		pPoseFrame = pSuper->mpPoseBuffer + PartIndex;
	}
	else
	{
		u32* pAnimFile = G_PSXREGION[pSuper->mRegion].pAnimFile;
		u32 IntervalWord = pAnimFile[2 * Anim + 2];

		if (IntervalWord & 0xFFFF0000)
		{
			M3dUtils_InterpolateVectors(
					4,
					(IntervalWord >> 16) + 1,
					pAnimFile,
					pSuper,
					PartIndex,
					G_PSXREGION[pSuper->mRegion].NumParts);

			pPoseFrame = gTweenBuffer + PartIndex;
		}
		else
		{
			pPoseFrame = Decomp_GetAnimTransform(pSuper) + PartIndex;
		}
	}

	gsub_46F820(pHook, pPoseFrame, &pSuper->mTransform);
	gte_stlvnl(pOut);

	pOut->vx <<= 12;
	pOut->vy <<= 12;
	pOut->vz <<= 12;

	pOut->vx = pSuper->mPos.vx + ((pOut->vx - pSuper->mPos.vx) >> 4);
	pOut->vy = pSuper->mPos.vy + ((pOut->vy - pSuper->mPos.vy) >> 4);
	pOut->vz = pSuper->mPos.vz + ((pOut->vz - pSuper->mPos.vz) >> 4);
}

// @Ok
// @Matching
void M3dUtils_ReadHooksPacket(CSuper* pSuper, void* pPacket)
{
	G_PSXREGION[pSuper->mRegion].pHooks = reinterpret_cast<SHook*>(reinterpret_cast<i32>(pPacket) + 4);
}

void validate_SHook(void)
{
	VALIDATE_SIZE(SHook, 0x8);

	VALIDATE(SHook, Part, 0x0);
	VALIDATE(SHook, Offset, 0x6);
}


#include "compatibility/my_patch.h"

// @Bogus
void patch_m3dutils(void)
{
	PATCH_PUSH_RET(0x00453C30, M3dUtils_ReadHooksPacket);
	PATCH_PUSH_RET(0x00454200, M3dUtils_InBetween);

	PATCH_PUSH_RET(0x00453C50, M3dUtils_ReadLinksPacket);
}
