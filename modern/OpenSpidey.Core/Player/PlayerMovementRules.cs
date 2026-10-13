namespace OpenSpidey.Core.Player;

/// <summary>Shared gates, input magnitude and scale for the recovered velocity finalizers.</summary>
internal static class PlayerMovementRules
{
    public static bool Enabled(PlayerFlatMovementFrame frame) =>
        !frame.Lookaround && frame.MovementEnabled && (frame.Action & 0xBFF7F8C1) == 0;

    public static int Magnitude(PlayerAxes axes) =>
        Math.Min(Math.Abs(axes.Sideways * 64) + Math.Abs(axes.Forward * 64), 4096);

    public static int AnimationSpeed(PlayerFlatMovementFrame frame, int magnitude) =>
        frame.AnimationId is 50 or 51 or 52 ? Math.Max(((magnitude >> 6) << 16) >> 6, 0x8000) : frame.AnimationSpeed;

    public static (int Magnitude, int Scale) Scale(PlayerFlatMovementFrame frame, int magnitude)
    {
        if (frame.Crawling) return (magnitude, 26);
        if (frame.HoldingObject) return (magnitude, frame.SmallHeldObject ? 10 : 24);
        return (4096, 40);
    }
}
