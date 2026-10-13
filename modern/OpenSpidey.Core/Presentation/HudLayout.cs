using OpenSpidey.Assets.Animation;

namespace OpenSpidey.Core.Presentation;

public readonly record struct HudSourceQuad(short Left, short Top, short Right, short Bottom);
public readonly record struct ScreenRectangle(float X, float Y, float Width, float Height);
public enum HudHorizontalAnchor { Left, Center, Right }
public enum HudVerticalAnchor { Top, Center, Bottom }

/// <summary>Original Panel_SetStretchedScreenCoords rounding, signed frame offsets and short vertex stores.</summary>
public static class HudSpriteLayout
{
    public static HudSourceQuad Stretch(SpriteAnimationFrame frame, int x, int y, int width = 0, int height = 0)
    {
        int scaledWidth = unchecked((width == 0 ? frame.Width : width) * 6554);
        if ((scaledWidth & 0xFFF) >= 0x800) scaledWidth = unchecked(scaledWidth + 4096);
        scaledWidth >>= 12;
        short left = unchecked((short)(x + ((6554 * frame.OffsetX) >> 12)));
        short top = unchecked((short)(y + frame.OffsetY));
        return new(left, top, unchecked((short)(left + scaledWidth)),
            unchecked((short)(top + (height == 0 ? frame.Height : height))));
    }
}

/// <summary>One transform per HUD group: original512×240 pixel aspect, intact spacing and viewport-edge anchors.</summary>
public readonly record struct HudViewportTransform
{
    public float ScaleX { get; }
    public float ScaleY { get; }
    public float ExtraWidth { get; }
    public float ExtraHeight { get; }
    public ScreenRectangle MenuFrame => new(ExtraWidth / 2, ExtraHeight / 2, 512 * ScaleX, 240 * ScaleY);

    public HudViewportTransform(float viewportWidth, float viewportHeight)
    {
        if (!float.IsFinite(viewportWidth) || viewportWidth <= 0) throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        if (!float.IsFinite(viewportHeight) || viewportHeight <= 0) throw new ArgumentOutOfRangeException(nameof(viewportHeight));
        ScaleY = Math.Min(viewportHeight / 240, viewportWidth / 320);
        ScaleX = ScaleY * 0.625f;
        ExtraWidth = viewportWidth - 512 * ScaleX;
        ExtraHeight = viewportHeight - 240 * ScaleY;
    }

    public ScreenRectangle Project(HudSourceQuad quad, HudHorizontalAnchor horizontal = HudHorizontalAnchor.Left,
        HudVerticalAnchor vertical = HudVerticalAnchor.Top)
    {
        float x = Offset(horizontal, ExtraWidth), y = Offset(vertical, ExtraHeight);
        return new(x + quad.Left * ScaleX, y + quad.Top * ScaleY,
            (quad.Right - quad.Left) * ScaleX, (quad.Bottom - quad.Top) * ScaleY);
    }

    public ScreenRectangle Project(ScreenRectangle rectangle, HudHorizontalAnchor horizontal = HudHorizontalAnchor.Left,
        HudVerticalAnchor vertical = HudVerticalAnchor.Top) => new(
            Offset(horizontal, ExtraWidth) + rectangle.X * ScaleX,
            Offset(vertical, ExtraHeight) + rectangle.Y * ScaleY, rectangle.Width * ScaleX, rectangle.Height * ScaleY);

    private static float Offset(HudHorizontalAnchor anchor, float extra) => anchor switch
    {
        HudHorizontalAnchor.Left => 0, HudHorizontalAnchor.Center => extra / 2, HudHorizontalAnchor.Right => extra,
        _ => throw new ArgumentOutOfRangeException(nameof(anchor))
    };
    private static float Offset(HudVerticalAnchor anchor, float extra) => anchor switch
    {
        HudVerticalAnchor.Top => 0, HudVerticalAnchor.Center => extra / 2, HudVerticalAnchor.Bottom => extra,
        _ => throw new ArgumentOutOfRangeException(nameof(anchor))
    };
}
