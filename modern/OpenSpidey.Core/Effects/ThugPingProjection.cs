using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;

namespace OpenSpidey.Core.Effects;

public readonly record struct ThugPingPixel(int X, int Y);

/// <summary>Original low-precision DisplayFlatBitList rotated frame corners, before viewport scaling.</summary>
public static class ThugPingProjection
{
    public static ThugPingPixel[]? Project(SpriteAnimationFrame frame, ushort scale, short angle, int depth, int centerX, int centerY, uint postScale = 0x10001000)
    {
        if (depth < 200) return null;
        int perspective = (int)(((uint)scale << 7) / (uint)depth);
        if ((uint)perspective > 512) return null;
        int halfWidth = unchecked(perspective * (int)(postScale & 0xFFFF)) >> 12;
        int halfHeight = unchecked(perspective * (int)(postScale >> 16)) >> 12;
        if (angle == 0)
        {
            int width = unchecked(frame.Width * halfWidth) >> 7, height = unchecked(frame.Height * halfHeight) >> 7;
            if ((uint)width < 5) width = 2;
            if ((uint)height < 5) height = 1;
            var rectangle = new FlatSpriteRectangle(unchecked(frame.OffsetX * halfWidth) >> 7, unchecked(frame.OffsetY * halfHeight) >> 7, width, height);
            int left = unchecked(centerX + rectangle.X), top = unchecked(centerY + rectangle.Y);
            return [Pixel(left, top), Pixel(left + rectangle.Width, top), Pixel(left, top + rectangle.Height), Pixel(left + rectangle.Width, top + rectangle.Height)];
        }
        (short sine, short cosine) = GameAngle.Heading(angle);
        int x0 = unchecked(cosine * frame.OffsetX), y0 = unchecked(sine * frame.OffsetX);
        int x1 = unchecked(sine * frame.OffsetY), y1 = unchecked(cosine * frame.OffsetY);
        int rightX = unchecked(x0 + frame.Width * cosine), rightY = unchecked(y0 + frame.Width * sine);
        int bottomX = unchecked(x1 + frame.Height * sine), bottomY = unchecked(y1 + frame.Height * cosine);
        return [Rotated(x0 - x1, y0 + y1), Rotated(rightX - x1, y1 + rightY), Rotated(x0 - bottomX, bottomY + y0), Rotated(rightX - bottomX, rightY + bottomY)];

        ThugPingPixel Rotated(int x, int y) => Pixel(unchecked(centerX + ((ushort)(unchecked(halfWidth * x) >> 16) >> 3)),
            unchecked(centerY + ((ushort)(unchecked(halfHeight * y) >> 16) >> 3)));
        static ThugPingPixel Pixel(int x, int y) => new(x & 0xFFF, y & 0xFFF);
    }
}
