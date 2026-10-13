namespace OpenSpidey.Core.Cinematics;

public readonly record struct MediaRectangle(float X, float Y, float Width, float Height);

/// <summary>Fits prerecorded pixels into any viewport without distorting their original aspect.</summary>
public static class MediaFrameLayout
{
    public static MediaRectangle Fit(float viewportWidth, float viewportHeight, float sourceWidth, float sourceHeight)
    {
        if (!float.IsFinite(viewportWidth) || !float.IsFinite(viewportHeight) || viewportWidth < 0 || viewportHeight < 0)
            throw new ArgumentOutOfRangeException(nameof(viewportWidth));
        if (!float.IsFinite(sourceWidth) || !float.IsFinite(sourceHeight) || sourceWidth <= 0 || sourceHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWidth));
        float scale = Math.Min(viewportWidth / sourceWidth, viewportHeight / sourceHeight);
        float width = sourceWidth * scale;
        float height = sourceHeight * scale;
        return new((viewportWidth - width) / 2, (viewportHeight - height) / 2, width, height);
    }
}
