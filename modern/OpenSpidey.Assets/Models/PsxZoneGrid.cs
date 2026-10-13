namespace OpenSpidey.Assets.Models;

/// <summary>Immutable type10 map cells retain the on-disk instance order used by the source collision inquiry.</summary>
public sealed record PsxZoneGrid(int MinimumX, int MinimumZ, int MaximumX, int MaximumZ,
    short Width, short Height, IReadOnlyList<IReadOnlyList<int>> Cells)
{
    public int CellWidth => unchecked(MaximumX - MinimumX) / Width;
    public IReadOnlyList<int> Cell(int x, int z) => Cells[z * Width + x];
}
