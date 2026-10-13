using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Recovered surface-local velocity finalization for wall crawling.</summary>
public static class PlayerWallMovement
{
    /// <param name="frame">Heading is the surface's effective heading, independent of camera yaw.</param>
    public static PlayerFlatMovementResult Resolve(PlayerFlatMovementFrame frame, FixedJointTransform basis)
    {
        int speed = frame.AnimationSpeed;
        if (!PlayerMovementRules.Enabled(frame))
            return new(frame.Velocity, speed);
        FixedVector3 local = FixedRotation.TransformTranspose(basis, frame.Velocity >> 6) << 6;
        if (frame.Axes == default)
        {
            if ((frame.Action & 0x40040004) != 0) return new(frame.Velocity, speed);
            local = local with { X = 0, Z = 0 };
        }
        else
        {
            int magnitude = PlayerMovementRules.Magnitude(frame.Axes);
            speed = PlayerMovementRules.AnimationSpeed(frame, magnitude);
            (magnitude, int scale) = PlayerMovementRules.Scale(frame, magnitude);
            int cosine = Math.Abs((int)GameAngle.Heading((frame.Heading - frame.InputHeading) & 4095).Cosine);
            int forward = unchecked((((cosine * magnitude) >> 12) * scale) * frame.SpeedRamp);
            local = local with { Z = unchecked(-(forward / 16)) };
        }
        return new(FixedRotation.Transform(basis, local >> 6) << 6, speed);
    }
}
