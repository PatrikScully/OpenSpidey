namespace OpenSpidey.Core.Effects;

public readonly record struct ThugRibbonScreenPoint(int PackedPosition, int Depth, ushort Width, bool Clipped);
public readonly record struct ThugRibbonScreenNormal(int X, int Y);

/// <summary>Machine-confirmed CalcScreenNormal and CGouraudRibbon edge offsets.</summary>
public static class ThugRibbonProjection
{
    public static ThugRibbonScreenNormal Normal(ThugRibbonScreenPoint first, ThugRibbonScreenPoint next)
    {
        if (first.Clipped || next.Clipped) return default;
        int dx = unchecked((short)next.PackedPosition - (short)first.PackedPosition);
        // PC0x4F2AF8 subtracts current Y; the recovered C++ accidentally subtracts current X.
        int dy = unchecked((next.PackedPosition >> 16) - (first.PackedPosition >> 16));
        int aspectDx = unchecked(320 * dx) / 512;
        int magnitudeX = GameAbs(aspectDx), magnitudeY = GameAbs(dy);
        int length = magnitudeX <= magnitudeY ? unchecked(magnitudeY + magnitudeX / 2) : unchecked(magnitudeX + magnitudeY / 2);
        if (length < 2) return default;
        return new(unchecked((unchecked(dy << 6) / length) << 9) / 320, unchecked(-aspectDx << 6) / length);
    }

    public static int Edge(ThugRibbonScreenPoint point, ThugRibbonScreenNormal normal)
    {
        int width = unchecked(400 * (int)point.Width) / point.Depth;
        int x = unchecked((short)point.PackedPosition + (unchecked(normal.X * width) >> 6));
        int y = unchecked((point.PackedPosition >> 16) + (unchecked(normal.Y * width) >> 6));
        return (x & 0xFFFF) | unchecked(y << 16);
    }

    public static int Pack(int x, int y) => (x & 0xFFFF) | unchecked(y << 16);
    private static int GameAbs(int value) => value < 0 ? unchecked(-value) : value;
}
