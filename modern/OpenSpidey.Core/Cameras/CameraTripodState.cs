using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Cameras;

/// <summary>CCamera's posed tripod, source4/8/4 easing and timed tripod-position interpolation.</summary>
public sealed class CameraTripodState
{
    public FixedVector3 Position { get; set; }
    public FixedVector3 Target { get; set; }
    public FixedVector3 Rate { get; set; }
    public int RemainingTicks { get; set; }
    public int HookIndex { get; set; } = -1;
    public bool TrackingEnabled { get; set; } = true;
    public int EaseX { get; set; } = 4;
    public int EaseY { get; set; } = 8;
    public int EaseZ { get; set; } = 4;

    /// <summary>CCamera::SetTripodMotion resets an existing interpolation to the live body position, then starts the new signed vector rate.</summary>
    public void SetMotion(FixedVector3 target, uint duration, FixedVector3? bodyPosition, bool bodyDead)
    {
        ArgumentOutOfRangeException.ThrowIfZero(duration);
        if (RemainingTicks != 0 && bodyPosition is { } body && !bodyDead) Position = body;
        RemainingTicks = unchecked((int)duration);
        Target = target;
        Rate = (target - Position) / unchecked((int)duration);
    }

    public void Advance(FixedVector3? tripodPosition, bool tripodDead, Func<int, FixedVector3> posedHook, int elapsedTicks)
    {
        FixedVector3 previous = Position;
        if (RemainingTicks == 0 && tripodPosition is { } body && TrackingEnabled && !tripodDead)
            Position = HookIndex == -1 ? body : posedHook(HookIndex);
        Position = new(Blend(Position.X, previous.X, EaseX), Blend(Position.Y, previous.Y, EaseY), Blend(Position.Z, previous.Z, EaseZ));
        if (unchecked((uint)RemainingTicks) <= unchecked((uint)elapsedTicks))
        {
            if (RemainingTicks != 0) { RemainingTicks = 0; Position = Target; }
        }
        else
        {
            RemainingTicks = unchecked(RemainingTicks - elapsedTicks);
            Position += Rate * elapsedTicks;
        }
    }

    private static int Blend(int desired, int previous, int ease) =>
        unchecked(ease * (desired >> 4) + (previous >> 4) * (16 - ease));
}
