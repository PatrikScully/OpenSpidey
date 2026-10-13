using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Core.Player;

public readonly record struct FlatSpriteRectangle(int X, int Y, int Width, int Height);

/// <summary>Recovered unrotated flat-bit sizing in original logical screen pixels.</summary>
public static class FlatSpriteProjection
{
    public static FlatSpriteRectangle? Project(SpriteAnimationFrame frame, ushort scale, int depth)
    {
        if (depth < 200) return null;
        int perspective = (int)(((uint)scale << 7) / (uint)depth);
        if ((uint)perspective > 512) return null;
        int width = (frame.Width * perspective) >> 7;
        int height = (frame.Height * perspective) >> 7;
        if ((uint)width < 5) width = 2;
        if ((uint)height < 5) height = 1;
        return new((frame.OffsetX * perspective) >> 7, (frame.OffsetY * perspective) >> 7, width, height);
    }
}
