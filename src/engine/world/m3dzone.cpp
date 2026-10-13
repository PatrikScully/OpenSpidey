#include "engine/world/m3dzone.h"
#include "engine/world/collision.h"
#include "compatibility/my_assert.h"
#include "engine/assets/spool.h"

#include "compatibility/validate.h"

#define NUM_ZONES 1
#ifndef SPIDEY_STANDALONE
EXPORT SZone Zones[NUM_ZONES];
#else
extern SZone Zones[NUM_ZONES];
#endif
// M3dZone_Init (0x454980) is the proof: "mov dword [5FC640h],0" is Zones[0].Flags = 0,
// then "mov edi,5FC660h; mov ecx,190h; rep stosd" zeroes the 20x20 Ptr grid at
// Zones[0] + 0x20. M3dZone_LineToItem indexes it as [eax+5FC640h] with eax = i*0x660,
// which is sizeof(SZone). idb_globals.txt agrees (0x005FC640 Zones).
// The exe's M3dZone_SetZone fills this while nothing in m3dzone.cpp is hooked, and
// the already-hooked M3dColij_LineToItemZoned is fed the cell pointers out of it, so
// the macro has to be on game memory.
//#define G_ZONES (Zones)
#define G_ZONES (reinterpret_cast<SZone*>(0x005FC640))

// @Ok
// functional: line-vs-zone-grid collision walk. Ghidra's generic v-names
// were replaced with descriptive ones and locals moved to first use;
// no arithmetic, operand order or control flow was changed from the
// decompiled shape, only identifiers (v2 was always equal to the loop's
// i, so it was dropped; v18/v26 and v19/v3 were the same logical cell
// counter split across a loop-carried register pair by the decompiler,
// collapsed into one cellX/cellZ each). Verified against the disassembly
// at 0x4549A0. Not byte-identical (MSVC6 register scheduling), functional
// parity confirmed per session policy.
void M3dZone_LineToItem(
		SLineInfo *pInfo,
		i32 CheckEnviroObs)
{
	print_if_false(G_ZONES[0].Flags != 0, "No zone information");

	G_M3DCOLIJ_ONE_MASK = 0;
	G_M3DCOLIJ_ZERO_MASK = -1;
	if (G_LINE_OF_SIGHT_CHECK)
		G_M3DCOLIJ_ONE_MASK = 0x400000;
	if (!G_BADDY_COLLISION_CHECK)
		G_M3DCOLIJ_ONE_MASK ^= 0x200000u;
	if (G_CAMERA_COLLISION_CHECK)
		G_M3DCOLIJ_ZERO_MASK = 0xFFEFFFFF;
	if (G_TRIGGER_COLLISION_CHECK)
		G_M3DCOLIJ_ZERO_MASK ^= 0x20000u;
	if (CheckEnviroObs)
		M3dColij_LineToItem(&G_ENVIRONMENTAL_OBJECT_LIST[0], pInfo);

	for (i32 i = 0; i <= (NUM_ZONES - 1); i++)
	{
		if (G_ZONES[i].Flags)
		{
			i32 startX = pInfo->StartCoords.vx;
			i32 endX = pInfo->EndCoords.vx;
			i32 startZ = pInfo->StartCoords.vz;
			i32 endZ = pInfo->EndCoords.vz;
			i32 xMin = G_ZONES[i].xMin;
			i32 xMax = G_ZONES[i].xMax;
			i32 zMin = G_ZONES[i].zMin;
			i32 zMax = G_ZONES[i].zMax;
			i32 ZoneWidth = G_ZONES[i].ZoneWidth;

			if (startX >= xMin || endX >= xMin)
			{
				if (startX > xMax)
				{
					if (endX > xMax)
						continue;
				}

				if (startZ < zMin)
				{
					if (endZ < zMin)
						continue;
				}

				if (startZ > zMax)
				{
					if (endZ > zMax)
						continue;
				}

				if (startX == endX && startZ == endZ)
				{
					i32 pointCellX = (startX - xMin) / ZoneWidth;
					i32 pointCellZ = (startZ - zMin) / ZoneWidth;
					i32 gridWidthDeg = G_ZONES[i].Width;

					if (pointCellX == gridWidthDeg)
						--pointCellX;
					i32 gridHeightDeg = G_ZONES[i].Height;
					if (pointCellZ == gridHeightDeg)
						--pointCellZ;
					if (pointCellX < 0 || pointCellX >= gridWidthDeg || pointCellZ < 0 || pointCellZ >= gridHeightDeg)
						DoAssert(0, "Zone index out of range");
					else
						DoAssert(1u, "Zone index out of range");
					M3dColij_LineToItemZoned(
							reinterpret_cast<CItem**>(G_ZONES[i].Ptr[pointCellX][pointCellZ]),
							pInfo);
				}
				else
				{
					// clip the line segment against the zone's rectangle
					// (xMin/xMax/zMin/zMax), one edge at a time
					if (startX < xMin)
					{
						if (startZ >= endZ)
							startZ = endZ + M3dMaths_MulDiv64(endX - xMin, startZ - endZ, endX - startX);
						else
							startZ += M3dMaths_MulDiv64(xMin - startX, endZ - startZ, endX - startX);
						startX = xMin;
					}

					if (endX < xMin)
					{
						if (endZ >= startZ)
							endZ = startZ + M3dMaths_MulDiv64(startX - xMin, endZ - startZ, startX - endX);
						else
							endZ += M3dMaths_MulDiv64(xMin - endX, startZ - endZ, startX - endX);
						endX = xMin;
					}

					if (startX > xMax)
					{
						if (endZ >= startZ)
							startZ += M3dMaths_MulDiv64(startX - xMax, endZ - startZ, startX - endX);
						else
							startZ = endZ + M3dMaths_MulDiv64(xMax - endX, startZ - endZ, startX - endX);
						startX = xMax;
					}

					if (endX > xMax)
					{
						if (startZ >= endZ)
							endZ += M3dMaths_MulDiv64(endX - xMax, startZ - endZ, endX - startX);
						else
							endZ = startZ + M3dMaths_MulDiv64(xMax - startX, startZ - endZ, endX - startX);
						endX = xMax;
					}

					if (startZ < zMin)
					{
						if (startX >= endX)
							startX = endX + M3dMaths_MulDiv64(endZ - zMin, startX - endX, endZ - startZ);
						else
							startX += M3dMaths_MulDiv64(zMin - startZ, endX - startX, endZ - startZ);
						startZ = zMin;
					}

					if (endZ < zMin)
					{
						if (endX >= startX)
							endX = startX + M3dMaths_MulDiv64(startZ - zMin, endX - startX, startZ - endZ);
						else
							endX += M3dMaths_MulDiv64(zMin - endZ, startX - endX, startZ - endZ);
						endZ = zMin;
					}

					if (startZ > zMax)
					{
						if (endX >= startX)
							startX += M3dMaths_MulDiv64(startZ - zMax, endX - startX, startZ - endZ);
						else
							startX = endX + M3dMaths_MulDiv64(zMax - endZ, startX - endX, startZ - endZ);
						startZ = zMax;
					}

					if (endZ > zMax)
					{
						if (startX >= endX)
							endX += M3dMaths_MulDiv64(endZ - zMax, startX - endX, endZ - startZ);
						else
							endX = startX + M3dMaths_MulDiv64(zMax - startZ, endX - startX, endZ - startZ);
						endZ = zMax;
					}

					// grid-walk (Bresenham-style) from the start cell to the
					// end cell, calling M3dColij_LineToItemZoned per cell
					i32 dx = endX - startX;
					i32 stepX;
					if (endX - startX >= 0)
					{
						stepX = 1;
					}
					else
					{
						stepX = -1;
						dx = startX - endX;
					}
					i32 dz = endZ - startZ;
					i32 stepZ;
					if (endZ - startZ >= 0)
					{
						stepZ = 1;
					}
					else
					{
						stepZ = -1;
						dz = startZ - endZ;
					}

					i32 xOffsetInZone = startX - xMin;
					i32 startCellX = (startX - xMin) / ZoneWidth;
					i32 endCellX = (endX - xMin) / ZoneWidth;
					i32 startCellZ = (startZ - zMin) / ZoneWidth;
					i32 endCellZ = (endZ - zMin) / ZoneWidth;
					i32 xRemainder = xOffsetInZone % ZoneWidth;
					i32 zRemainder = (startZ - zMin) % ZoneWidth;
					i32 gridWidth = G_ZONES[i].Width;
					if (startCellX == gridWidth)
					{
						--startCellX;
						xRemainder += ZoneWidth;
					}
					i32 gridHeight = G_ZONES[i].Height;

					if (startCellZ == gridHeight)
					{
						--startCellZ;
						zRemainder += ZoneWidth;
						gridHeight = G_ZONES[i].Height;
					}

					if (endCellX == gridWidth)
						--endCellX;

					if (endCellZ == gridHeight)
						--endCellZ;

					i32 zAdjust = M3dMaths_MulDiv64(dx, zRemainder, ZoneWidth);
					i32 xAdjust = M3dMaths_MulDiv64(dz, xRemainder, ZoneWidth);
					i32 cellX = startCellX;
					i32 cellZ = startCellZ;

					i32 errorPartX;
					if (startX >= endX)
						errorPartX = -xAdjust;
					else
						errorPartX = xAdjust - dz;

					i32 errorPartZ;
					if (startZ >= endZ)
						errorPartZ = zAdjust;
					else
						errorPartZ = dx - zAdjust;

					i32 errorAccum = errorPartZ + errorPartX;
					i32 rowOffset = 20 * startCellX;
					while (cellX != endCellX || cellZ != endCellZ)
					{
						if (cellX < 0)
							goto next_zone;
						if (cellX >= G_ZONES[i].Width)
							break;
						if (cellZ < 0 || cellZ >= G_ZONES[i].Height)
							break;

						M3dColij_LineToItemZoned(
								reinterpret_cast<CItem**>(G_ZONES[i].Ptr[0][cellZ + rowOffset]),
								pInfo);
						if (errorAccum < 0)
						{
							errorAccum += dx;
							cellZ += stepZ;
						}
						else
						{
							errorAccum -= dz;
							cellX += stepX;
							rowOffset += 20 * stepX;
						}
					}
					if (cellX >= 0 && cellX < G_ZONES[i].Width && cellZ >= 0 && cellZ < G_ZONES[i].Height)
						M3dColij_LineToItemZoned(
								reinterpret_cast<CItem**>(G_ZONES[i].Ptr[cellX][cellZ]),
								pInfo);
				}
			}
		}

next_zone:
		;
	}

	M3dColij_GetLineInfo(pInfo);
}

// @Ok
// @Matching
 void M3dZone_FreePSX(i32 EnvIndex)
{
	if (EnvIndex <= (NUM_ZONES-1))
	{
		G_ZONES[EnvIndex].Flags = 0;

		for (i32 i = 0; i < 20; i++)
		{
			for (i32 j = 0; j < 20; j++)
			{
				G_ZONES[EnvIndex].Ptr[i][j] = 0;
			}
		}
	}
}

// @Ok
// @Matching
void M3dZone_Init(void)
{
	M3dZone_FreePSX(0);
}

// @Ok
// functional: verified field-by-field against the disassembly at 0x455060
// (Zones[EnvIndex] offset math is index*0x660, matching sizeof(SZone);
// every field store, both DoAssert calls, and the nested Width/Height loop
// match). The "wtf is that 64" note below is resolved: the disasm shifts
// the collision-record index left by 6 (shl eax,6) which is exactly
// 0x10 * sizeof(u32), confirming PSXRegion[...].pSuper is indexed as an
// array of 16-u32 records, so `0x10 * *v18` is correct as written.
// Not byte-identical (MSVC6 computes the Zones[EnvIndex] byte offset with a
// different multiply sequence, same result), functional parity confirmed
// per session policy.
void M3dZone_SetZone(
		i32 EnvIndex,
		u32 *pPack)
{
	DoAssert(EnvIndex == 0, "EnvIndex not zero");

	G_ZONES[EnvIndex].xMin = *pPack;
	G_ZONES[EnvIndex].zMin = pPack[1];
	G_ZONES[EnvIndex].xMax = pPack[2];
	G_ZONES[EnvIndex].zMax = pPack[3];

	G_ZONES[EnvIndex].Width = reinterpret_cast<i16*>(pPack)[8];
	G_ZONES[EnvIndex].Height = reinterpret_cast<i16*>(pPack)[9];

	G_ZONES[EnvIndex].Flags = 1;
	G_ZONES[EnvIndex].ZoneWidth = (G_ZONES[EnvIndex].xMax - G_ZONES[EnvIndex].xMin) / G_ZONES[EnvIndex].Width;

	DoAssert(G_ZONES[EnvIndex].Width <= 20, "ZONE WIDTH TOO LARGE");
	DoAssert(G_ZONES[EnvIndex].Height <= 20, "ZONE HEIGHT\tTOO LARGE");

	u32* v14 = &pPack[5];

	for (i32 i = 0; i < G_ZONES[EnvIndex].Height; i++ )
	{
		for (i32 j = 0; j < G_ZONES[EnvIndex].Width; j++ )
		{
			i32 v17 = v14[2];
			u32 *v18 = &v14[3];

			G_ZONES[EnvIndex].Ptr[j][i] = reinterpret_cast<u32>(v18);

			while (v17-- > 0)
			{
				// 0x10 (16 u32s = 64 bytes per record) matches the disasm's
				// shl eax,6 on the index, confirmed by rebuild verification.
				// the cell list holds item INDICES on disk and CItem POINTERS
				// in memory (M3dAsm_LineColijPreprocessItemsZoned walks them as
				// CItem*): store the item's address, not its first dword (the
				// vtable pointer, which is what "*v18 = *tmp" put there).
				u32 *tmp = &reinterpret_cast<u32 *>(G_PSXREGION[EnvRegions[EnvIndex]].pSuper)[0x10 * *v18];
				*v18 = reinterpret_cast<u32>(tmp);
				++v18;

			}
			v14 = v18 + 1;
		}
	}
}

void validate_SZone(void)
{
	VALIDATE_SIZE(SZone, 0x660);

	VALIDATE(SZone, Flags, 0x0);

	VALIDATE(SZone, xMin, 0x4);
	VALIDATE(SZone, zMin, 0x8);
	VALIDATE(SZone, xMax, 0xC);
	VALIDATE(SZone, zMax, 0x10);

	VALIDATE(SZone, ZoneWidth, 0x14);
	VALIDATE(SZone, ZoneHeight, 0x18);

	VALIDATE(SZone, Width, 0x1C);
	VALIDATE(SZone, Height, 0x1E);

	VALIDATE(SZone, Ptr, 0x20);
}
