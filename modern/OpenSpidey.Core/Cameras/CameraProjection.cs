namespace OpenSpidey.Core.Cameras;

/// <summary>Original PC projection slope adapted to a fixed vertical view on modern displays.</summary>
public static class CameraProjection
{
    public const int InitialZoom = 2365;
    public const double OriginalAspect = 4.0 / 3.0;

    public static double VerticalFieldOfView(int zoom)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(zoom);
        // M3d_RenderSetup uses (width / 2) * 4096 / zoom for both pixel axes.
        double verticalSlope = zoom / (4096.0 * OriginalAspect);
        return 2 * Math.Atan(verticalSlope) * (180 / Math.PI);
    }
}
