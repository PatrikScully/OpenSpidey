using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerFlatMovementFrame(FixedVector3 Velocity, PlayerAxes Axes,
    short Heading, short InputHeading, short CameraHeading, uint Action, int SpeedRamp,
    ushort AnimationId, int AnimationSpeed, bool MovementEnabled = true, bool Lookaround = false,
    bool Crawling = false, bool HoldingObject = false, bool SmallHeldObject = false,
    bool SuppressMovement = false, bool SuppressAirHeading = false);

public readonly record struct PlayerFlatMovementResult(FixedVector3 Velocity, int AnimationSpeed);

/// <summary>Recovered movement finalization on the free horizontal plane; surface-local and boss-arena movement are separate policies.</summary>
public static class PlayerFlatMovement
{
    public static int UpdateSpeedRamp(int ramp, PlayerAxes axes, int ticks)
    {
        if (axes != default)
        {
            if (ramp < 16) ramp = Math.Min(unchecked(ramp + ticks), 16);
        }
        else
        {
            if (ramp != 0) ramp = unchecked(ramp - ticks);
            if (ramp < 0) ramp = 0;
        }
        return ramp;
    }

    public static PlayerFlatMovementResult Resolve(PlayerFlatMovementFrame frame)
    {
        FixedVector3 velocity = frame.Velocity;
        int speed = frame.AnimationSpeed;
        if (!PlayerMovementRules.Enabled(frame))
            return new(velocity, speed);
        if (frame.Axes == default)
        {
            if ((frame.Action & 0x40040004) == 0)
                velocity = Quantize(new(0, Quantize(velocity).Y, 0));
            return new(velocity, speed);
        }
        int magnitude = PlayerMovementRules.Magnitude(frame.Axes);
        speed = PlayerMovementRules.AnimationSpeed(frame, magnitude);
        (magnitude, int scale) = PlayerMovementRules.Scale(frame, magnitude);
        if (frame.SuppressMovement) return new(velocity, speed);
        int yaw = frame.Heading;
        if ((frame.Action & 6) != 0 && !frame.SuppressAirHeading)
            yaw = (frame.InputHeading + frame.CameraHeading) & 4095;
        (short sine, short cosine) = GameAngle.Heading(yaw);
        int forward = unchecked((((cosine * magnitude) >> 12) * frame.SpeedRamp) * scale);
        int sideways = unchecked((((sine * magnitude) >> 12) * frame.SpeedRamp) * scale);
        velocity = Quantize(new(unchecked(-(sideways / 16)), Quantize(velocity).Y, unchecked(-(forward / 16))));
        return new(velocity, speed);
    }

    // The identity GTE transform still wraps each component's multiply by 4096 before shifting it back.
    private static FixedVector3 Quantize(FixedVector3 vector) =>
        new((((vector.X >> 6) << 12) >> 12) << 6, (((vector.Y >> 6) << 12) >> 12) << 6,
            (((vector.Z >> 6) << 12) >> 12) << 6);
}
