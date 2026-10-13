using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Effects;

public readonly record struct LinkedRibbonPoint(int X, int Y, int Depth);
public readonly record struct LinkedRibbonView(object Identity, byte Flags, LinkedRibbonPoint Start, LinkedRibbonPoint End, ushort Scale, byte FrameWidth);
public readonly record struct LinkedRibbonCorner(int X, int Y);
public readonly record struct LinkedRibbonQuad(object Identity, LinkedRibbonCorner First, LinkedRibbonCorner Second,
    LinkedRibbonCorner Third, LinkedRibbonCorner Fourth);

/// <summary>Actual DisplayLinked2EndedBitListLeftover's perspective width and connected screen corners, preserving source list order.</summary>
public static class LinkedRibbonProjection
{
    public static IReadOnlyList<LinkedRibbonQuad> Project(IEnumerable<LinkedRibbonView> segments, ushort near, ushort far)
    {
        var draws = new List<LinkedRibbonQuad>();
        bool previousFailed = false;
        LinkedRibbonPoint previousEnd = default;
        LinkedRibbonCorner previousLeft = default, previousRight = default, historyLeft = default, historyRight = default;
        foreach (LinkedRibbonView segment in segments)
        {
            bool fresh = (segment.Flags & 16) != 0 || previousFailed;
            LinkedRibbonPoint start = fresh ? segment.Start : previousEnd, end = segment.End;
            if ((fresh && Clipped(start)) || Clipped(end)) { previousFailed = true; continue; }
            int dx = unchecked(end.X - start.X), dy = unchecked(end.Y - start.Y);
            int length = FixedVectorMath.OriginalSquareRoot(unchecked(dx * dx + dy * dy));
            if (length == 0) { previousFailed = true; continue; }
            LinkedRibbonCorner startLeft = previousLeft, startRight = previousRight;
            if (fresh) (startLeft, startRight) = Corners(start);
            if ((segment.Flags & 16) != 0) { historyLeft = startLeft; historyRight = startRight; }
            (LinkedRibbonCorner endLeft, LinkedRibbonCorner endRight) = (segment.Flags & 32) != 0 ? (historyLeft, historyRight) : Corners(end);
            previousEnd = end; previousLeft = endLeft; previousRight = endRight; previousFailed = false;
            LinkedRibbonQuad quad = (segment.Flags & 3) switch
            {
                2 => new(segment.Identity, endRight, endLeft, startRight, startLeft),
                3 => new(segment.Identity, endLeft, endRight, startLeft, startRight),
                1 => new(segment.Identity, startRight, endRight, startLeft, endLeft),
                _ => new(segment.Identity, startLeft, endLeft, startRight, endRight)
            };
            draws.Add(quad with { First = Narrow(quad.First), Second = Narrow(quad.Second), Third = Narrow(quad.Third), Fourth = Narrow(quad.Fourth) });
            (LinkedRibbonCorner Left, LinkedRibbonCorner Right) Corners(LinkedRibbonPoint point)
            {
                uint numerator = unchecked((uint)(segment.FrameWidth * (short)segment.Scale));
                int width = Math.Max(unchecked((int)(numerator / (uint)point.Depth >> 1)), 2);
                int offsetY = unchecked(width * dy) / length, offsetX = unchecked(width * dx) / length;
                return (new(unchecked(point.X + offsetY), unchecked(point.Y - offsetX)), new(unchecked(point.X - offsetY), unchecked(point.Y + offsetX)));
            }
        }
        return draws;
        bool Clipped(LinkedRibbonPoint point) => (uint)point.Depth < near || (uint)point.Depth > far;
        LinkedRibbonCorner Narrow(LinkedRibbonCorner corner) => new(unchecked((short)corner.X), unchecked((short)corner.Y));
    }
}
