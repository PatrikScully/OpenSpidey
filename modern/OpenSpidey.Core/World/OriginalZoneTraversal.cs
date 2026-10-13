using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.World;

/// <summary>Source4549A0's clipped map-grid walk and source inquiry deduplication, preserving cell/item order.</summary>
public static class OriginalZoneTraversal
{
    public static IReadOnlyList<(int X, int Z)> Cells(PsxZoneGrid grid, FixedVector3 from, FixedVector3 to)
    {
        var cells = new List<(int X, int Z)>();
        int startX = from.X, endX = to.X, startZ = from.Z, endZ = to.Z;
        int xMin = grid.MinimumX, xMax = grid.MaximumX, zMin = grid.MinimumZ, zMax = grid.MaximumZ;
        int zoneWidth = grid.CellWidth;
        if (startX < xMin && endX < xMin || startX > xMax && endX > xMax ||
            startZ < zMin && endZ < zMin || startZ > zMax && endZ > zMax) return cells;
        if (startX == endX && startZ == endZ)
        {
            int pointX = unchecked(startX - xMin) / zoneWidth, pointZ = unchecked(startZ - zMin) / zoneWidth;
            if (pointX == grid.Width) pointX--;
            if (pointZ == grid.Height) pointZ--;
            Add(pointX, pointZ);
            return cells;
        }
        unchecked
        {
            if (startX < xMin)
            {
                startZ = startZ >= endZ
                    ? endZ + MulDiv(endX - xMin, startZ - endZ, endX - startX)
                    : startZ + MulDiv(xMin - startX, endZ - startZ, endX - startX);
                startX = xMin;
            }
            if (endX < xMin)
            {
                endZ = endZ >= startZ
                    ? startZ + MulDiv(startX - xMin, endZ - startZ, startX - endX)
                    : endZ + MulDiv(xMin - endX, startZ - endZ, startX - endX);
                endX = xMin;
            }
            if (startX > xMax)
            {
                startZ = endZ >= startZ
                    ? startZ + MulDiv(startX - xMax, endZ - startZ, startX - endX)
                    : endZ + MulDiv(xMax - endX, startZ - endZ, startX - endX);
                startX = xMax;
            }
            if (endX > xMax)
            {
                endZ = startZ >= endZ
                    ? endZ + MulDiv(endX - xMax, startZ - endZ, endX - startX)
                    : startZ + MulDiv(xMax - startX, endZ - startZ, endX - startX);
                endX = xMax;
            }
            if (startZ < zMin)
            {
                startX = startX >= endX
                    ? endX + MulDiv(endZ - zMin, startX - endX, endZ - startZ)
                    : startX + MulDiv(zMin - startZ, endX - startX, endZ - startZ);
                startZ = zMin;
            }
            if (endZ < zMin)
            {
                endX = endX >= startX
                    ? startX + MulDiv(startZ - zMin, endX - startX, startZ - endZ)
                    : endX + MulDiv(zMin - endZ, startX - endX, startZ - endZ);
                endZ = zMin;
            }
            if (startZ > zMax)
            {
                startX = endX >= startX
                    ? startX + MulDiv(startZ - zMax, endX - startX, startZ - endZ)
                    : endX + MulDiv(zMax - endZ, startX - endX, startZ - endZ);
                startZ = zMax;
            }
            if (endZ > zMax)
            {
                endX = startX >= endX
                    ? endX + MulDiv(endZ - zMax, startX - endX, endZ - startZ)
                    : startX + MulDiv(zMax - startZ, endX - startX, endZ - startZ);
                endZ = zMax;
            }
            int dx = endX - startX, stepX = 1;
            if (dx < 0) { stepX = -1; dx = startX - endX; }
            int dz = endZ - startZ, stepZ = 1;
            if (dz < 0) { stepZ = -1; dz = startZ - endZ; }
            int xOffset = startX - xMin;
            int cellX = xOffset / zoneWidth, endCellX = (endX - xMin) / zoneWidth;
            int cellZ = (startZ - zMin) / zoneWidth, endCellZ = (endZ - zMin) / zoneWidth;
            int xRemainder = xOffset % zoneWidth, zRemainder = (startZ - zMin) % zoneWidth;
            if (cellX == grid.Width) { cellX--; xRemainder += zoneWidth; }
            if (cellZ == grid.Height) { cellZ--; zRemainder += zoneWidth; }
            if (endCellX == grid.Width) endCellX--;
            if (endCellZ == grid.Height) endCellZ--;
            int zAdjust = MulDiv(dx, zRemainder, zoneWidth), xAdjust = MulDiv(dz, xRemainder, zoneWidth);
            int errorX = startX >= endX ? -xAdjust : xAdjust - dz;
            int errorZ = startZ >= endZ ? zAdjust : dx - zAdjust;
            int error = errorZ + errorX;
            while (cellX != endCellX || cellZ != endCellZ)
            {
                if (!IsInside(cellX, cellZ)) return cells;
                cells.Add((cellX, cellZ));
                if (error < 0) { error += dx; cellZ += stepZ; }
                else { error -= dz; cellX += stepX; }
            }
            if (IsInside(cellX, cellZ)) cells.Add((cellX, cellZ));
        }
        return cells;

        bool IsInside(int x, int z) => x >= 0 && x < grid.Width && z >= 0 && z < grid.Height;
        void Add(int x, int z)
        {
            if (!IsInside(x, z)) throw new InvalidDataException("Original point-query zone index is out of range.");
            cells.Add((x, z));
        }
    }

    public static IReadOnlyList<int> Instances(PsxZoneGrid grid, FixedVector3 from, FixedVector3 to)
    {
        var items = new List<int>();
        var inquiry = new HashSet<int>();
        foreach ((int x, int z) in Cells(grid, from, to))
        foreach (int instance in grid.Cell(x, z))
            if (inquiry.Add(instance)) items.Add(instance);
        return items;
    }

    private static int MulDiv(int first, int second, int divisor) => unchecked((int)((long)first * second / divisor));
}
