#include "engine/world/ob.h"
#include "core/game_heap.h"
#include "engine/assets/spool.h"
#include <cstring>
#include "compatibility/validate.h"
#include "engine/audio/ps2redbook.h"
#include "engine/rendering/scene_renderer.h"
#include "engine/rendering/m3dutils.h"
#include "engine/assets/spool.h"
#include "core/utils.h"
#include "compatibility/my_assert.h"
#include "game/player/web.h"
#include "game/effects/exp.h"

// @Ok
#ifndef SPIDEY_STANDALONE
CVector ZeroVector;
#else
extern CVector ZeroVector;
#endif

// @Ok
#ifndef SPIDEY_STANDALONE
u8 gWhatIf;
#else
extern u8 gWhatIf;
#endif

#ifndef SPIDEY_STANDALONE
u32 SuspendedDistance;
#else
extern u32 SuspendedDistance;
#endif
#ifndef SPIDEY_STANDALONE
u8 gPsxItemsIndex;
#else
extern u8 gPsxItemsIndex;
#endif
#ifndef SPIDEY_STANDALONE
const char *gObjFile;
#else
extern const char * gObjFile;
#endif
#ifndef SPIDEY_STANDALONE
CBody* EnvironmentalObjectList;
#else
extern CBody* EnvironmentalObjectList;
#endif
#ifndef SPIDEY_STANDALONE
CBody* SuspendedList;
#else
extern CBody* SuspendedList;
#endif
#ifndef SPIDEY_STANDALONE
CItem* EnviroList;
#else
extern CItem* EnviroList;
#endif


#ifndef SPIDEY_STANDALONE
CBody* RealMechList;
#else
extern CBody* RealMechList;
#endif

#ifndef SPIDEY_STANDALONE
i32 gSuperItemRelated = 1;
#else
extern i32 gSuperItemRelated;
#endif
#ifndef SPIDEY_STANDALONE
f32 gFloatSuperRelated = 1.0f;
#else
extern f32 gFloatSuperRelated;
#endif

// @Ok
EXPORT SLight M3d_DefaultLight =
{
  { { -2896, -2896, 0 }, { 3277, -2458, 0 }, { 1737, 3277, 1737 } },
  0,
  { { 2800, 1900, 1000 }, { 1900, 1900, 1500 }, { 1500, 2800, 1000 } },
  0,
  { 512, 512, 512 }
};


// @Ok
// @Matching
void CBody::DeleteStuff(void)
{
}

// @Ok
// @Matching
CBody* CBody::FindBodyByNode(
		i32 type,
		CBody* pBody)
{
	for (CBody *cur = pBody; cur; cur = reinterpret_cast<CBody*>(cur->mNextItem))
	{
		if (cur->mNode == type)
			return cur;
	}

	return 0;
}

// @Ok
// @Matching
void* CItem::operator new(size_t size)
{
	void *pnew = Mem_New(size);

	// Ensure size is a multiple of 4.
	size = ( size + 3 ) & ~0x03;

	// Zero all the newly allocated memory
	u32 *p=(u32 *)pnew;
	for (i32 i=0; i<size/4; ++i) *p++=0;

	return pnew;
}

// @Ok
void CItem::operator delete(void *ptr)
{
	Mem_Delete(ptr);
}


// @Ok
// @Matching
CItem::CItem()
{
	this->mScale.Set(0x1000, 0x1000, 0x1000);

	this->mpLight = &M3d_DefaultLight;
}

// @Ok
// @Matching
CItem::~CItem()
{
}

// @Ok
void CBody::AI(void)
{
}

// @Ok
int CBody::Hit(SHitInfo*)
{
	return 1;
}

// @Ok
// @Matching
void CItem::InitItem(const char * pName)
{
	this->mRegion = Spool_FindRegion(pName);
	this->mModel = 0;


	if (G_PSXREGION[this->mRegion].IsSuper)
	{
		SModel *pModel = G_PSXREGION[this->mRegion].ppModels[0];
		pModel->Radius = 0x64000;
		pModel->Box.vx = 0xFF9C0064;
		pModel->Box.vy = 0xFF9C0064;
		pModel->Box.vz = 0xFF9C0064;
	}
}

// @Ok
// 0x45FDC0, 607 bytes. Blows the item apart into CItemFrag pieces. Transforms
// every model vertex of the item's current model into world space with the
// GTE, then walks the face list and spawns one fragment per face whose flag
// word does not have bit 0x10 set, flying out from the item centre at
// speed + Rnd(randomSpeed). Called by CDome::Burst (web.cpp) with (30, 30).
// The first print_if_false really is a constant 1 in the original (a disabled
// PSX-only assert), reproduced as written rather than dropped.
// The 0x6B2454 table the original indexes with region*17 is
// G_PSXREGION[region].ppModels (SPSXRegion is 68 bytes = 17 dwords and
// ppModels sits at +0x14, so 0x6B2440 + 0x14 = 0x6B2454); indexed through the
// containing global here per the address-audit rule, exactly as InitItem
// above already does.
void CItem::Burst(i32 speed, i32 randomSpeed)
{
	print_if_false(1, "CItem::Burst only works for PSX version 0x20004");
	print_if_false(((this->mFlags >> 1) & 1) == 0, "Cannot burst a superitem");

	i32 groundY = Web_GetGroundY(&this->mPos);

	SModel *pModel = G_PSXREGION[this->mRegion].ppModels[this->mModel];

	i32 numVerts = pModel->NumVertices;
	SVECTOR *pSrc = reinterpret_cast<SVECTOR*>(&pModel->Vertices);

	// one spare CVector past the end, as the original allocates it
	CVector *pVerts = reinterpret_cast<CVector*>(Mem_New(4 * (3 * numVerts + 3)));

	MATRIX rotMat;
	M3dMaths_RotMatrixYXZ(reinterpret_cast<SVECTOR*>(&this->mAngles), &rotMat);
	gte_SetRotMatrix(&rotMat);
	M3dAsm_SetTransVector(reinterpret_cast<VECTOR*>(&this->mPos));

	if (numVerts != 0)
	{
		CVector *pDst = pVerts;
		i32 i = numVerts;

		do
		{
			gte_ldv0(pSrc);
			gte_rtv0tr();
			gte_stlvnl(reinterpret_cast<VECTOR*>(pDst));

			pDst->vx <<= 12;
			pDst->vy <<= 12;
			pDst->vz <<= 12;

			pDst++;
			pSrc++;
			i--;
		} while (i != 0);
	}

	i32 numFaces = pModel->NumFaces;

	// faces follow the vertex and normal SVECTOR arrays, both 8 bytes each
	u32 *pFace = reinterpret_cast<u32*>(reinterpret_cast<u8*>(pModel) + 0x1C
			+ 8 * (pModel->NumNormals + numVerts));

	if (numFaces != 0)
	{
		i32 i = numFaces;

		do
		{
			if ((*reinterpret_cast<u8*>(pFace) & 0x10) == 0)
			{
				CVector dir = pVerts[pFace[1] & 0xFF] - this->mPos;

				i32 length = dir.Length();

				if (length != 0)
				{
					i32 speedNow = speed + Rnd(randomSpeed);
					CVector vel = (dir * speedNow) / length;

					new CItemFrag(pFace, pVerts, &vel, groundY);
				}
			}

			// face records are variable length, size in dwords in bits 18+
			pFace += *pFace >> 18;
			i--;
		} while (i != 0);
	}

	Mem_Delete(pVerts);
}



// @Ok
// @Matching
 i32 CBody::IsDead(void) const
{
	if (this->mCBodyFlags & CBODY_ZOMBIE)
		return 1;
	return 0;
}
	


// @Ok
// @Matching
void CBody::Die(void)
{
	if(!this->IsDead())
	{
		this->mCBodyFlags |= CBODY_ZOMBIE;
	}
}

// @Ok
// @Matching
void CBody::ShadowOn(void){
	this->mCBodyFlags |= CBODY_HASSHADOW;
}


// @Ok
// @Matching
void CBody::KillShadow(void)
{
	this->mCBodyFlags &= ~(CBODY_HASSHADOW);
	if (this->mpShadow)
	{
		delete this->mpShadow;
		this->mpShadow = 0;
	}
}

// @Ok
// @Matching
void CBody::UpdateShadow(void)
{
	if(this->mCBodyFlags & 8){

		if(!this->mpShadow){

			G_TOTALBITUSAGE = 0;
			this->mpShadow = new CQuadBit();;
			G_TOTALBITUSAGE = 1;

			this->mpShadow->SetTexture(0, 0);
			this->mpShadow->SetSubtractiveTransparency();
			this->mpShadow->mFrigDeltaZ = 32;
			this->mpShadow->mProtected = 1;
		}

		CSVector vec;
		vec.Set(0, -4096, 0);

		this->mpShadow->OrientUsing(
				&this->mShadowPos,
				reinterpret_cast<SVECTOR*>(&vec),
				this->mShadowScale,
				this->mShadowScale);


		i32 trans = ((this->mShadowThreshold - this->mShadowDist) << 7) / this->mShadowThreshold;

		if(trans < 0)
		{
			trans = 0;
		}

		this->mpShadow->SetTransparency(trans);

	}
	else{
		   this->KillShadow();
	}

}


// @Ok
// @Matching
 void CBody::AttachTo(CBody** ppList)
{
	this->mNextItem = *ppList;
	this->mPreviousItem = 0;

	*ppList = this;

	if (this->mNextItem)
		this->mNextItem->mPreviousItem = this;
}

// @Ok
// @Matching
 void CBody::DeleteFrom(CBody **ppList)
{

	if(this->mCBodyFlags & CBODY_SUSPENDED && ppList != &G_SUSPENDED_LIST)
	{
		this->UnSuspend();
	}

	if (this->mNextItem)
		this->mNextItem->mPreviousItem = this->mPreviousItem;

	if (this->mPreviousItem)
		this->mPreviousItem->mNextItem = this->mNextItem;

	if (*ppList == this)
		*ppList = reinterpret_cast<CBody*>(this->mNextItem);
}

// @Ok
// @Matching
 void CBody::UnSuspend(void)
{

	if (this->mCBodyFlags & CBODY_SUSPENDED)
	{
		this->DeleteFrom(&G_SUSPENDED_LIST);
		this->AttachTo(this->mppOriginalList);
		this->mCBodyFlags &= ~CBODY_SUSPENDED;
	}
}

// @Ok
// @Matching
void CBody::Suspend(CBody **a2)
{
	ASSERT((this->mCBodyFlags & CBODY_SUSPENDED) == 0, "Suspended flag illegally set");
	ASSERT(a2 != 0, "woops");

	this->DeleteStuff();

	this->mppOriginalList = a2;
	this->DeleteFrom(a2);

	this->AttachTo(&G_SUSPENDED_LIST);
	this->mCBodyFlags |= CBODY_SUSPENDED;
}




// @Ok
// @Matching
void CBody::InterleaveAI(void)
{
	if (this->mFlags & CBODY_RADIALSUSPENSION)
	{
		this->EveryFrame();
		CSuper *super = reinterpret_cast<CSuper*>(this);
		super->UpdateFrame();

		this->AI();
	}
	else
	{
		this->EveryFrame();
		this->AI();
	}
}

// @Ok
// @Matching
i16* CBody::SquirtPos(i16* p_info)
{
	i32 *walker = reinterpret_cast<i32*>(p_info);
	ASSERT(((i32)walker & 3) == 0, "Bad alignment");

	this->mPos.vx = *walker++ << 12;
	this->mPos.vy = *walker++ << 12;
	this->mPos.vz = *walker++ << 12;

	return reinterpret_cast<i16*>(walker);
}

// @Ok
// @Matching
i16* CBody::SquirtAngles(i16* p_info)
{
	this->mAngles.vx = *p_info++;
	this->mAngles.vy = *p_info++;
	this->mAngles.vz = *p_info++;

	return p_info;
}

// @Ok
// @Matching
void CBody::AttachXA(i32 a2, i32 a3)
{
	this->field_98 = G_VBLANKS;
	this->field_9C = a2;
	this->field_A0 = a3;
}

// @Ok
// @Matching
void CBody::StopMyXA(void)
{
	if ((G_VBLANKS - this->field_98) < 0x12C
			&& G_REDBOOK_XA_RELATED_ONE == this->field_9C
			&& G_REDBOOK_XA_RELATED_TWO == this->field_A0)
	{
		Redbook_XAStop();
	}
}

// @Ok
// @AlmostMatching: vtable is moved later than expected for some reason
CBody::CBody(void)
{
	this->mFric.vx = 1;
	this->mFric.vy = 1;
	this->mFric.vz = 1;

	this->mAngFric.vx = 1;
	this->mAngFric.vy = 1;
	this->mAngFric.vz = 1;
	this->mCBodyFlags |= 0x16;

	this->mPushVal = 10;
	this->field_A4 = 0;
	this->mNode = 0xFFFF;
	this->mShadowScale = 32;
	this->mShadowThreshold = 200;
}

// @Ok
// @Matching
CSuper::CSuper()
{
	this->mFlags |= 2u;
	this->mNumFrames = 1;
	this->mAnimFinished = 1;

	this->mAnimSpeed = 0x10000;
	this->field_13E = 100;
	this->field_13F = 94;
}

// @Ok
// @Matching
void CSuper::OutlineOff(void)
{
	this->mExtraFlags &= ~CSUPER_OUTLINE;
}

// @Ok
// 0x460880, 824 bytes, real PC function (names.json ?OutlineOn@CSuper@@QAEXXZ, Mac size 852).
// Full translation, verified against the raw disasm of 0x460880 (idalib session 0dc9741d), not
// just Hex-Rays. Builds the per-face neighbour (adjacency) table the outline renderer needs, once,
// on the first call, and caches it in field_11C.
//
// What the disasm does, step by step:
//  - mExtraFlags |= CSUPER_OUTLINE always; the table is built only when field_11C is still null.
//  - pRegionEntry = CItemRelatedList[mRegion * 17] (ob.h, 0x6B2454, mRegion at CItem+0x1F), the
//    same per-region model table switch.cpp/mysterio.cpp/web.cpp already walk. The model count is
//    the DWORD stored 4 bytes BEFORE that entry (`mov edi,[eax-4]`, CLAUDE.md's shifted-pointer
//    idiom), and pRegionEntry[0] is the first model record.
//  - allocates count*1024 bytes with Mem_New (DCMem_New(size,0,1,0,1), the exact 5 args pushed)
//    and fills it with 0xFF via the nested 256 x 4-byte store loop the original emits. 1024 bytes
//    per model = 256 faces * 4 neighbour bytes, which is why "NumFaces too big" asserts at 0x100.
//  - per model: numFaces = u16 at rec+6, face records start at rec + (u16@+2 + u16@+4)*8 + 0x1C
//    (byte-identical to Switch_SetSwitchFaceFlags), each record advances by (rec[0] >> 18) DWORDs.
//  - per outer face j (cursor B) it scans every other face i (cursor A, reset per j) and compares
//    the four bytes of the DWORD at record+4 (the face's vertex indices) all-pairs. Writing i into
//    slot k of face j's 4-byte record means face i shares edge k with face j. bit 0x10 of the
//    outer record's byte 0 selects a 3-corner face: the quad case fills 4 edges (b0b1, b0b2, b1b3,
//    b2b3), the triangle case only 3 (b0b1, b0b2, b1b2), i.e. a quad wound 0,1,3,2. That edge
//    reading is what makes the byte roles unambiguous; an earlier pass left this stub because the
//    record format is undocumented, but the operations themselves are fully determined by the
//    disasm and are reproduced here 1:1 (self-compare skip, cursor advances, and the exact write
//    conditions/order included).
//  - the next model record is where the last face record ended (cursor B after the walk).
// Both debug asserts are constant-true in this build (call nullsub_1): print_if_false(1, "Bad
// SNbrFaces size.") is a folded sizeof check, the second is the real numFaces <= 0x100 test.
// Not runtime-testable in a headless smoke run (visual-only boss outline, CVenom/CDummy callers),
// so this is verified against the disassembly only.
void CSuper::OutlineOn(void){
	this->mExtraFlags |= CSUPER_OUTLINE;

	if (!this->field_11C){

		i32 **pRegionEntry = CItemRelatedList[this->mRegion * 17];
		u8 *pModelRec = reinterpret_cast<u8*>(pRegionEntry[0]);
		i32 numModels = reinterpret_cast<i32*>(pRegionEntry)[-1];

		print_if_false(1, "Bad SNbrFaces size.");

		u8 *pNbrTable = static_cast<u8*>(Mem_New(numModels << 10));
		this->field_11C = pNbrTable;

		if (numModels > 0)
		{
			u8 *pFill = pNbrTable;
			for (i32 model = numModels; model != 0; model--)
			{
				for (i32 slot = 256; slot != 0; slot--)
				{
					pFill[0] = 0xFF;
					pFill[1] = 0xFF;
					pFill[2] = 0xFF;
					pFill[3] = 0xFF;
					pFill += 4;
				}
			}
		}

		if (numModels > 0)
		{
			u8 *pRow = static_cast<u8*>(this->field_11C) + 2;

			for (i32 model = numModels; model != 0; model--)
			{
				i32 numFaces = *reinterpret_cast<u16*>(pModelRec + 6);
				print_if_false(numFaces <= 0x100, "NumFaces too big");

				i32 faceOffset =
					*reinterpret_cast<u16*>(pModelRec + 4) + *reinterpret_cast<u16*>(pModelRec + 2);

				u8 *pFacesBase = pModelRec + faceOffset * 8 + 0x1C;
				u8 *pFaceB = pFacesBase;

				if (numFaces > 0)
				{
					u8 *pNbr = pRow;

					for (i32 j = 0; j < numFaces; j++)
					{
						u8 *pFaceA = pFacesBase;

						for (i32 i = 0; i < numFaces; i++)
						{
							if (i != j)
							{
								u32 a = *reinterpret_cast<u32*>(pFaceA + 4);
								u32 b = *reinterpret_cast<u32*>(pFaceB + 4);

								u32 a0 = a & 0xFF;
								u32 a1 = (a >> 8) & 0xFF;
								u32 a2 = (a >> 16) & 0xFF;
								u32 a3 = a >> 24;

								u32 b0 = b & 0xFF;
								u32 b1 = (b >> 8) & 0xFF;
								u32 b2 = (b >> 16) & 0xFF;
								u32 b3 = b >> 24;

								i32 m0 = (b0 == a3) || (b0 == a2) || (b0 == a1) || (b0 == a0);
								i32 m1 = (b1 == a3) || (b1 == a2) || (b1 == a1) || (b1 == a0);
								i32 m2 = (b2 == a3) || (b2 == a2) || (b2 == a1) || (b2 == a0);

								if (pFaceB[0] & 0x10)
								{
									if (m0)
									{
										if (m1)
											pNbr[-2] = static_cast<u8>(i);

										if (m2)
											pNbr[-1] = static_cast<u8>(i);
									}

									if (m1 && m2)
										pNbr[0] = static_cast<u8>(i);
								}
								else
								{
									i32 m3 = (b3 == a3) || (b3 == a2) || (b3 == a1) || (b3 == a0);

									if (m0)
									{
										if (m1)
											pNbr[-2] = static_cast<u8>(i);

										if (m2)
											pNbr[-1] = static_cast<u8>(i);
									}

									if (m1 && m3)
										pNbr[0] = static_cast<u8>(i);

									if (m2 && m3)
										pNbr[1] = static_cast<u8>(i);
								}
							}

							pFaceA += (*reinterpret_cast<u32*>(pFaceA) >> 18) * 4;
						}

						pNbr += 4;
						pFaceB += (*reinterpret_cast<u32*>(pFaceB) >> 18) * 4;
					}
				}

				pModelRec = pFaceB;
				pRow += 1024;
			}
		}
	}

	this->outlineR = -1;
	this->outlineG = -1;
	this->outlineB = -1;
	this->alsoOutlineRelated = 0x50000000;
}


// @Ok
// @Matching
void CSuper::SetOutlineSemiTransparent(){
	this->alsoOutlineRelated |= 0x02000000;
}


// @Ok
// @Matching
void CSuper::SetOutlineRGB(
		u8 a2,
		u8 a3,
		u8 a4)
{
	this->outlineR = a2;
	this->outlineG = a3;
	this->outlineB = a4;
}

// @Ok
// Functional only, checked against the Hex-Rays decompile of 0x460DA0.
// Not byte matched: register allocation differs (edx/eax swapped) and was
// not chased further per session policy (functional decomp is the bar).
void CSuper::UpdateFrame(void){

	char v1; // bl
	i32 v2; // esi
	i32 v3; // edx
	i32 v4; // eax
	i32 v5; // edx
	i32 v6; // eax
	u16 v7; // dx


	if ( !this->mElapsedTicks )
	  this->mElapsedTicks = 2;
	v1 = this->mAnimDir;
	v2 = this->mElapsedTicks * this->mAnimSpeed / 2;
	v3 = (u16)this->mFrameFrac | (this->mFrame << 16);
	if ( this->mAnimDir == 1 )
	  v3 += v2;
	if ( v1 == -1 )
	  v3 -= v2;
	v4 = v3;
	this->mFrameFrac = v3;
	v5 = (u8)this->mAnimMode;
	v6 = v4 >> 16;
	this->mFrame = v6;

	if (v5) {
		if ( --v5 == 0)
		{
		  v7 = this->mNumFrames;
		  if ( (i16)v6 >= (int)v7 )
		  {
			  this->mFrame = v6 - v7;
        
		  }
		  else
		  {

			if ( (i16)(v6) < 0 )
			  this->mFrame = v6 + v7;
		  }
		}
	}
	else if( (this->mAnimDir == 1 && (i16)v6 >= this->mTargetFrame)
		||
		(v1 == -1 && (i16)v6 <= this->mTargetFrame)
		)
	{
		this->mFrame = this->mTargetFrame;
		this->mAnimFinished = 1;
	}
}


// @Ok
// @Matching
void CSuper::CycleAnim(i32 anim, i8 animdir)
{
	if (this->mAnim != anim)
	{
		this->mFrame = 0;
		this->mFrameFrac = 0;
		this->mAnim = anim;

		DoAssert(
			static_cast<u32>(anim & 0xFFFF) < G_PSXREGION[this->mRegion].pAnimFile[0],
			"Bad anim sent to CycleAnim");

		this->mNumFrames =
			reinterpret_cast<u16*>(G_PSXREGION[this->mRegion].pAnimFile)[4 + (4 * this->mAnim)];


		this->mAnimDir = animdir;
	}

	this->mAnimMode = 1;
	this->mAnimFinished = 0;
}


// @Ok
// @Matching
void CSuper::ApplyPose(i16 *a2){

	if (!this->mpJoints)
	{
		M3dUtils_ReadLinksPacket(this, reinterpret_cast<void*>(a2));
		this->actualcsuperend = a2;
	}

	M3dUtils_InBetween(this);

	if ((this->mFlags & 4) != 0)
	{
		M3d_BuildTransform(this);
		M3dUtils_BuildPose(this);
	}
}


// @Ok
// @Matching
void CSuper::RunAnim(
		i32 anim,
		i32 from,
		i32 to)
{

	this->mAnim = anim;
	DoAssert(
			static_cast<u32>(anim & 0xFFFF) < G_PSXREGION[this->mRegion].pAnimFile[0],
			"Bad anim sent to RunAnim");
	u16 v6 = reinterpret_cast<u16*>(G_PSXREGION[this->mRegion].pAnimFile)[4 + (4 * this->mAnim)];

	this->mNumFrames = v6;
	if (from == -1)
	{
		from = v6 - 1;
	}

	if (to == -1)
	{
		to = v6 - 1;
	}

	if (from < 0 || from >= v6)
		from = 0;
	if (to < 0 || to >= v6)
		to = 0;

	this->mAnimMode = 0;

	i32 res;
	if (to > from)
	{
		res = 1;
	}
	else
	{
		res = (to >= from) ? 0 : -1;
	}

	this->mTargetFrame = to;
	this->mAnimDir = res;
	this->mFrame = from;
	this->mFrameFrac = 0;
	this->mAnimFinished = static_cast<u16>(from) == static_cast<u16>(to);
}

// @Ok
// Functional only, checked against the Hex-Rays decompile of 0x460ED0.
// Not byte matched: add esp, 8 happens 2 instructions later after DoAssert,
// not chased further per session policy (functional decomp is the bar).
void CBody::EveryFrame(void)
{
	if (this->mCBodyFlags & 4)
	{
		this->mElapsedTicks = 2;
		this->mCBodyFlags &= 0xFFFB;
		this->mLastUpdateTick = G_TIMER_RELATED;
		this->mAccumulatedTicks = 0;
	}
	else
	{
		this->mElapsedTicks = G_TIMER_RELATED - this->mLastUpdateTick;
		DoAssert(
				this->mElapsedTicks >= 0,
				"Timing error");

		this->mLastUpdateTick = G_TIMER_RELATED;
		if (this->mElapsedTicks > 6)
		{
			this->mElapsedTicks = 6;
		}
	}

	this->mAccumulatedTicks += this->mElapsedTicks;

	if (this->mFlags & 2)
	{
		CSuper *pSuper = reinterpret_cast<CSuper*>(this);
		pSuper->field_152 = pSuper->mFrame;
		pSuper->field_150 = pSuper->mFrame;
		pSuper->field_154 = pSuper->mAnim;
		pSuper->field_143 = pSuper->mAnimDir;
	}

}

// @Ok
// @Matching
 CBody::~CBody(void)
{
	delete this->mpShadow;
}

// @Ok
CSuper::~CSuper(void)
{
	if (this->mpPoseBuffer)
		Mem_Delete(this->mpPoseBuffer);

	if (this->mpJoints)
		Mem_Delete(this->mpJoints);

	if (this->mpDecompressedFrame)
		Mem_Delete(this->mpDecompressedFrame);

	if (this->mpCalculationOrder)
		Mem_Delete(this->mpCalculationOrder);

	CItem *first = reinterpret_cast<CItem*>(
			Mem_RecoverPointer(&this->field_104));

	if (first)
		delete first;

	CItem *second = reinterpret_cast<CItem*>(
			Mem_RecoverPointer(&this->field_10C));

	if (second)
		delete second;

	if (this->field_11C)
		Mem_Delete(this->field_11C);

	this->field_11C = 0;
}

void validate_CItem(void)
{
	VALIDATE_SIZE(CItem, 0x40);

	VALIDATE(CItem, mFlags, 0x4);
	VALIDATE(CItem, mInquiry, 0x6);
	VALIDATE(CItem, mPos, 0x8);
	VALIDATE(CItem, mAngles, 0x14);
	VALIDATE(CItem, mModel, 0x1A);

	VALIDATE(CItem, mDummyFrame, 0x1C);
	VALIDATE(CItem, mTintIndex, 0x1D);
	VALIDATE(CItem, mDummyAnim, 0x1E);

	VALIDATE(CItem, mRegion, 0x1F);

	VALIDATE(CItem, mNextItem, 0x20);


	VALIDATE(CItem, mRGB, 0x24);
	VALIDATE(CItem, mScale, 0x28);

	VALIDATE(CItem, mTRN, 0x30);
	VALIDATE(CItem, mPreviousItem, 0x34);
	VALIDATE(CItem, mType, 0x38);
	VALIDATE(CItem, mpLight, 0x3C);

}


void validate_CBody(void){

	VALIDATE_SIZE(CBody, 0xF4);
	
	VALIDATE(CBody, mppOriginalList, 0x40);

	VALIDATE(CBody, mInputFlags, 0x44);
	VALIDATE(CBody, mCBodyFlags, 0x46);

	VALIDATE(CBody, field_48, 0x48);

	VALIDATE(CBody, field_54, 0x54);

	VALIDATE(CBody, mVel, 0x60);
	VALIDATE(CBody, mAcc, 0x6C);

	VALIDATE(CBody, mFric, 0x78);


	VALIDATE(CBody, mLastUpdateTick, 0x7C);

	VALIDATE(CBody, mElapsedTicks, 0x80);
	VALIDATE(CBody, mAccumulatedTicks, 0x84);

	VALIDATE(CBody, mAngVel, 0x88);
	VALIDATE(CBody, mAngAcc, 0x8E);

	VALIDATE(CBody, mAngFric, 0x94);

	VALIDATE(CBody, field_98, 0x98);
	VALIDATE(CBody, field_9C, 0x9C);
	VALIDATE(CBody, field_A0, 0xA0);

	VALIDATE(CBody, field_A4, 0xA4);
	VALIDATE(CBody, field_A8, 0xA8);

	VALIDATE(CBody, mShadowPos, 0xB8);
	VALIDATE(CBody, mShadowNormal, 0xC4);
	VALIDATE(CBody, mpShadow, 0xCC);

	VALIDATE(CBody, mShadowScale, 0xD0);
	VALIDATE(CBody, mShadowDist, 0xD2);
	VALIDATE(CBody, mShadowThreshold, 0xD4);

	VALIDATE(CBody, mPushVal, 0xD8);

	VALIDATE(CBody, mRMinor, 0xDC);

	VALIDATE(CBody, mNode, 0xDE);

	VALIDATE(CBody, mCollision, 0xE0);
	VALIDATE(CBody, mHealth, 0xE2);

	VALIDATE(CBody, mPlayerDist, 0xE4);

	VALIDATE(CBody, field_E8, 0xE8);

	VALIDATE_VTABLE(CBody, Die, 1);
	VALIDATE_VTABLE(CBody, AI, 2);
	VALIDATE_VTABLE(CBody, Hit, 3);
	VALIDATE_VTABLE(CBody, DeleteStuff, 4);
}

void validate_CSuper(void)
{

	VALIDATE_SIZE(CSuper, 0x1A4);
	
	VALIDATE(CSuper, field_F4, 0xF4);
	VALIDATE(CSuper, field_F8, 0xF8);
	VALIDATE(CSuper, field_FC, 0xFC);
	VALIDATE(CSuper, field_100, 0x100);
	VALIDATE(CSuper, field_104, 0x104);

	VALIDATE(CSuper, field_10C, 0x10C);
	VALIDATE(CSuper, field_114, 0x114);
	VALIDATE(CSuper, field_11C, 0x11C);
	VALIDATE(CSuper, alsoOutlineRelated, 0x120);
	VALIDATE(CSuper, outlineR, 0x124);
	VALIDATE(CSuper, outlineG, 0x125);
	VALIDATE(CSuper, outlineB, 0x126);

	VALIDATE(CSuper, mFrame, 0x128);
	VALIDATE(CSuper, mAnim, 0x12A);

	VALIDATE(CSuper, mExtraFlags, 0x12C);

	VALIDATE(CSuper, mpCalculationOrder, 0x130);
	VALIDATE(CSuper, mpDecompressedFrame, 0x134);

	VALIDATE(CSuper, mRoot, 0x138);

	VALIDATE(CSuper, mDecompressedAnim, 0x13A);
	VALIDATE(CSuper, mDecompressedFrame, 0x13C);

	VALIDATE(CSuper, field_13E, 0x13E);
	VALIDATE(CSuper, field_13F, 0x13F);

	VALIDATE(CSuper, mAnimMode, 0x140);
	VALIDATE(CSuper, mAnimDir, 0x141);
	VALIDATE(CSuper, mAnimFinished, 0x142);
	VALIDATE(CSuper, field_143, 0x143);


	VALIDATE(CSuper, mTargetFrame, 0x144);	
	VALIDATE(CSuper, mFrameFrac, 0x146);	

	VALIDATE(CSuper, mNumFrames, 0x148);	
	VALIDATE(CSuper, mAnimSpeed, 0x14C);

	VALIDATE(CSuper, field_150, 0x150);
	VALIDATE(CSuper, field_152, 0x152);
	VALIDATE(CSuper, field_154, 0x154);
	VALIDATE(CSuper, field_156, 0x156);

	VALIDATE(CSuper, field_158, 0x158);

	VALIDATE(CSuper, mTransform, 0x164);

	VALIDATE(CSuper, mpPoseBuffer, 0x184);
	VALIDATE(CSuper, mpJoints, 0x188);
	VALIDATE(CSuper, mpLinks, 0x18C);
	VALIDATE(CSuper, actualcsuperend, 0x190);
}

void validate_SHitInfo(void)
{
	VALIDATE_SIZE(SHitInfo, 0x1C);

	VALIDATE(SHitInfo, field_0, 0x0);
	VALIDATE(SHitInfo, field_1, 0x1);

	VALIDATE(SHitInfo, field_4, 0x4);
	VALIDATE(SHitInfo, field_8, 0x8);
	VALIDATE(SHitInfo, field_C, 0xC);

	VALIDATE(SHitInfo, field_18, 0x18);
	VALIDATE(SHitInfo, field_1A, 0x1A);
}

void validate_SLight(void)
{
	VALIDATE_SIZE(SLight, 0x34);

	VALIDATE(SLight, LightMatrix, 0x0);

	VALIDATE(SLight, ColorMatrix, 0x14);

	VALIDATE(SLight, BackColor, 0x28);
}

#include "compatibility/my_patch.h"

// @Bogus
void patch_CItem(void)
{
	PATCH_PUSH_RET(0x00460020, CItem::InitItem);
}

// @Bogus
void patch_CBody(void)
{
	PATCH_PUSH_RET(0x00460570, CBody::KillShadow);
	PATCH_PUSH_RET(0x00460F90, CBody::InterleaveAI);
	PATCH_PUSH_RET(0x004603A0, CBody::SquirtAngles);

	PATCH_PUSH_RET(0x00460260, CBody::AttachTo);
	PATCH_PUSH_RET(0x00460500, CBody::UnSuspend);

	PATCH_PUSH_RET(0x00460280, CBody::DeleteFrom);

	PATCH_PUSH_RET(0x004602F0, CBody::FindBodyByNode);

	PATCH_PUSH_RET(0x00460330, CBody::SquirtPos);
	PATCH_PUSH_RET(0x004603D0, CBody::AttachXA);
	PATCH_PUSH_RET(0x00460440, CBody::Suspend);

	PATCH_PUSH_RET(0x00460560, CBody::ShadowOn);
	PATCH_PUSH_RET_POLY(0x004606F0, CBody::Die, "?Die@CBody@@UAEXXZ");
	PATCH_PUSH_RET(0x00460700, CBody::IsDead);
}

// @Bogus
void patch_CSuper(void)
{
	PATCH_PUSH_RET(0x00460BC0, CSuper::OutlineOff);
	PATCH_PUSH_RET(0x00460BD0, CSuper::SetOutlineSemiTransparent);
	PATCH_PUSH_RET(0x00460BE0, CSuper::SetOutlineRGB);
	PATCH_PUSH_RET(0x00460D00, CSuper::CycleAnim);
	PATCH_PUSH_RET(0x00460E80, CSuper::ApplyPose);
}
