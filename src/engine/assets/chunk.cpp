#include "engine/assets/chunk.h"
#include "game/actors/baddy.h"
#include "core/game_heap.h"
#include "engine/world/bit.h"
#include "core/utils.h"
#include "engine/assets/spool.h"
#include "engine/rendering/dcmodel.h"
#include "engine/world/trig.h"

#include "compatibility/validate.h"


extern CBody* ControlBaddyList;

// Same table m3dinit.cpp calls gDCRegionItems: one DCModelData block per region,
// 36 bytes per model piece. Declared i32* (not DCModelData**) for the same reason
// m3dinit.cpp gives, MSVC6 will not fold a pointer-to-pointer literal address into
// an immediate. Kept file-local, like the copy in m3dinit.cpp.
static i32 * const gDCRegionItems = (i32 *)0x5F6764;

// Turns one named environment item into rubble.
//
// SPSXRegion::pChunkData (0x6B2474, filled by the "Chnk" branch of ProcessNewPSX
// at 0x4C9A60) is the region's chunk table. Layout, worked out from this function:
// a dword count, then that many {item checksum, byte offset} pairs, then the data
// area the offsets point into. Each data record starts with a packed dword, low
// half is how many "RuinModel" checksums follow and high half how many "RuinChunk"
// checksums follow, then the two checksum lists back to back.
//
// A RuinModel is a static piece that just gets swapped in (made visible, marked
// for the PSX renderer). A RuinChunk is the same but also handed to a fresh
// CChunkControl so it flies off and disappears. Both cases do the same three edits
// per piece: clear some CItem flags, clear the matching SModel flags, then walk the
// model's faces turning on the "draw" bit in both the source SModel face records
// and the runtime DCFace records the PC renderer actually uses.
//
// The original does not null check the CChunkControl before calling AddChunk on it,
// so a failed allocation with a non-zero chunk count would call a member on NULL.
// Kept as is, that is the original behaviour.
//
// Codegen residue (functional bar, not byte matched): 279 instructions/881 bytes
// against the original's 290/911. Every constant, flag mask, stride and branch
// matches; the difference is register allocation in three spots. Our checksum
// search loop compares in place and steps the pointer by 8 once (cmp [ebx],ebp /
// add ebx,8) where the original loads through a post-incremented pointer twice,
// our CChunkControl construction copies mPos through a shifted pointer (add esi,8
// then [esi],[esi+4],[esi+8]) where the original reads [esi+8],[esi+0Ch],[esi+10h]
// and spills them, and MSVC merges the "is the chunk count non zero" test with the
// same test inside the inlined CChunkControl constructor.
// @Ok
void Chunk_ChunkItemByChecksum(u32 Checksum)
{
	Trig_TriggerCommandPoint(Checksum, false);

	CItem *pItem = Spool_FindEnviroItem(Checksum);
	print_if_false(pItem != NULL, "Bad checksum");

	Chunk_MakeItemDisappear(pItem);

	u32 *pChunkData = G_PSXREGION[pItem->mRegion].pChunkData;

	u32 Count = pChunkData[0];
	u32 *pEntry = pChunkData + 1;
	u32 *pRecords = pChunkData + 2 * Count + 1;

	while (Count != 0)
	{
		if (pEntry[0] == Checksum)
			break;

		pEntry += 2;
		Count--;
	}

	print_if_false(Count != 0, "Checksum not found");

	if (Count == 0)
		return;

	u32 *pRecord = &pRecords[pEntry[1] >> 2];

	u32 NumModels = pRecord[0] & 0xFFFF;
	u32 NumChunks = pRecord[0] >> 16;

	u32 *pList = pRecord + 1;

	CChunkControl *pControl = NULL;

	if (NumChunks != 0)
		pControl = new CChunkControl(&pItem->mPos, static_cast<u16>(NumChunks));

	while (NumModels != 0)
	{
		CItem *pRuin = Spool_FindEnviroItem(*pList);
		pList++;

		print_if_false(pRuin != NULL, "Bad checksum");

		pRuin->mFlags &= 0xEFDE;

		SModel *pModel = G_PSXREGION[pRuin->mRegion].ppModels[pRuin->mModel];
		pModel->Flags &= 0xFFCF;

		DCModelData *pData = reinterpret_cast<DCModelData*>(gDCRegionItems[pRuin->mRegion] + 36 * pRuin->mModel);

		print_if_false(pData != NULL, "Null DCModel for RuinModel.");
		if (pData)
			pData->mFlags = (pData->mFlags & ~0x100) | 0x4000;

		DCFace *pDCFace = pData->pFaces;
		print_if_false(pModel->NumFaces == pData->mNumFaces, "NumFaces mismatch for RuinModel.");

		u32 *pFace = reinterpret_cast<u32*>(reinterpret_cast<u8*>(pModel) + 0x1C + 8 * (pModel->NumNormals + pModel->NumVertices));

		for (u32 i = 0; i < pModel->NumFaces; i++)
		{
			if ((pDCFace->mFlags & 0xC0) == 0)
				pDCFace->mFlags |= 0x80;

			u32 Word = pFace[0];
			pDCFace++;

			pFace[0] = Word | 0x80;
			pFace[3] &= ~0x10000;

			pFace += Word >> 18;
		}

		NumModels--;
	}

	while (NumChunks != 0)
	{
		CItem *pRuin = Spool_FindEnviroItem(*pList);
		pList++;

		pControl->AddChunk(pRuin);

		pRuin->mFlags &= 0xEFFE;

		SModel *pModel = G_PSXREGION[pRuin->mRegion].ppModels[pRuin->mModel];
		pModel->Flags &= 0xFFDF;

		DCModelData *pData = reinterpret_cast<DCModelData*>(gDCRegionItems[pRuin->mRegion] + 36 * pRuin->mModel);

		print_if_false(pData != NULL, "Null DCModel for RuinChunk.");
		if (pData)
			pData->mFlags = (pData->mFlags & ~0x100) | 0x4000;

		DCFace *pDCFace = pData->pFaces;
		print_if_false(pModel->NumFaces == pData->mNumFaces, "NumFaces mismatch for RuinChunk.");

		u32 *pFace = reinterpret_cast<u32*>(reinterpret_cast<u8*>(pModel) + 0x1C + 8 * (pModel->NumNormals + pModel->NumVertices));

		for (u32 i = 0; i < pModel->NumFaces; i++)
		{
			if ((pDCFace->mFlags & 0xC0) == 0)
				pDCFace->mFlags |= 0x80;

			u32 Word = pFace[0];
			pDCFace++;

			pFace[0] = Word | 0x80;

			pFace += Word >> 18;
		}

		NumChunks--;
	}
}

// @Ok
// @Test
void CChunkControl::AddChunk(CItem* pItem)
{
	if ( this->field_FA < this->field_F8 )
	{
		SChunkEntry *entry = &this->field_FC[this->field_FA];
		this->field_FA++;
		entry->pItem = pItem;

		i32 v4 = Rnd(4096) & 0xFFF;
		i32 v5 = Rnd(32) + 32;

		entry->field_4.vx = v5 * G_RCOSSIN_TBL[v4].sin;
		entry->field_4.vy = (-48 - Rnd(32)) << 12;
		entry->field_4.vz = v5 * G_RCOSSIN_TBL[v4].cos;
		entry->field_14.vx = Rnd(512) - 256;
		entry->field_14.vy = Rnd(512) - 256;
		entry->field_14.vz = Rnd(512) - 256;
		entry->field_1C = Rnd(120) + 120;
	}
}

// @Ok
void CChunkControl::AI(void)
{
	i8 v1 = 1;
	for (u32 i = 0; i < this->field_FA; i++)
	{
		SChunkEntry *entry = &this->field_FC[i];

		if (entry->pItem)
		{
			if (entry->field_1C <= this->mElapsedTicks)
			{
				Chunk_MakeItemDisappear(entry->pItem);
				entry->pItem = 0;
			}
			else
			{
				v1 = 0;
				entry->field_1C = entry->field_1C - this->mElapsedTicks;
				entry->pItem->mPos = entry->field_4;

				entry->field_4.vy += 0x8000;

				entry->pItem->mAngles = entry->field_14;
			}
		}
	}

	if (v1)
	{
		this->Die();
	}
}

// @Ok
CChunkControl::~CChunkControl(void)
{
	this->DeleteFrom(reinterpret_cast<CBody**>(&ControlBaddyList));

	if (this->field_FC)
		Mem_Delete(static_cast<void*>(this->field_FC));
}

// @Ok
 CChunkControl::CChunkControl(CVector* a2, u16 a3)
{
	this->mPos = *a2;
	this->AttachTo(reinterpret_cast<CBody**>(&ControlBaddyList));

	this->field_F8 = a3;
	if (this->field_F8)
		this->field_FC = static_cast<SChunkEntry*>(DCMem_New(32 * this->field_F8, 0, 1, 0, 1));
}

// @Ok
 void Chunk_MakeItemDisappear(CItem* item)
{
	item->mFlags |= 0x21;
}

void validate_CChunkControl(void)
{
	VALIDATE_SIZE(CChunkControl, 0x100);

	VALIDATE(CChunkControl, field_F8, 0xF8);
	VALIDATE(CChunkControl, field_FA, 0xFA);
	VALIDATE(CChunkControl, field_FC, 0xFC);
}

void validate_SChunkEntry(void)
{
	VALIDATE_SIZE(SChunkEntry, 0x20);

	VALIDATE(SChunkEntry, pItem, 0x0);
	VALIDATE(SChunkEntry, field_4, 0x4);
	VALIDATE(SChunkEntry, field_14, 0x14);
	VALIDATE(SChunkEntry, field_1C, 0x1C);
}
