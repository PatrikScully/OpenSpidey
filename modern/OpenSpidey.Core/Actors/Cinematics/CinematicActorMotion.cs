using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cinematics;

/// <summary>Original Black Cat/clone two-tick physics, including vertical drag and post-velocity displacement.</summary>
public static class CinematicActorMotion
{
    public static void Advance(ActorBodyMotion body, int elapsedTicks, int gravityAcceleration)
    {
        ArgumentNullException.ThrowIfNull(body);
        for (uint tick = 0; elapsedTicks > 0 && tick < (uint)elapsedTicks; tick += 2)
        {
            body.Acceleration = new(0, unchecked(gravityAcceleration - body.Velocity.Y / 16), 0);
            body.Velocity += body.Acceleration * 2;
            // The source multiplies by four before its signed division by two.
            // Retaining that intermediate overflow matters for extreme source velocities.
            body.Position += body.Velocity * 2 + new FixedVector3(
                unchecked(body.Acceleration.X * 4) / 2, unchecked(body.Acceleration.Y * 4) / 2,
                unchecked(body.Acceleration.Z * 4) / 2);
            body.Angles = (body.Angles with { Y = unchecked((short)(body.Angles.Y + body.AngularVelocity.Y * 2)) }).Mask();
            ActorAngles angular = body.AngularVelocity with { Y = unchecked((short)(body.AngularVelocity.Y + body.AngularAcceleration.Y * 2)) };
            body.AngularVelocity = new(Damp(angular.X, body.AngularDamping.X),
                Damp(angular.Y, body.AngularDamping.Y), Damp(angular.Z, body.AngularDamping.Z));
        }
    }

    private static short Damp(short value, byte shift)
    {
        short damped = unchecked((short)(value - (value >> shift)));
        return damped is >= -1 and <= 1 ? (short)0 : damped;
    }

    /// <summary>Utils_TurnTowards keeps previous angular acceleration when the current/ideal angles agree.</summary>
    public static void TurnTowards(ActorBodyMotion body, AimAngles ideal, int factor)
    {
        short pitch = WrappedDifference(ideal.Pitch, body.Angles.X), yaw = WrappedDifference(ideal.Yaw, body.Angles.Y);
        if ((pitch | yaw) == 0)
            body.AngularVelocity = body.AngularVelocity with { X = 0, Y = 0 };
        else body.AngularAcceleration = body.AngularAcceleration with
        { X = unchecked((short)(factor * pitch >> 8)), Y = unchecked((short)(factor * yaw >> 8)) };
    }

    private static short WrappedDifference(short ideal, short current)
    {
        short difference = unchecked((short)(ideal - current));
        if (difference < -2048) difference = unchecked((short)(difference + 4096));
        if (difference > 2048) difference = unchecked((short)(difference - 4096));
        return difference;
    }
}
