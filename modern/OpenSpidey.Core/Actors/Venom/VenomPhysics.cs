using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Venom;

/// <summary>CVenom::DoPhysics (0x4EBDD0): aim branches on field_218, per-tick integration.</summary>
public static class VenomPhysics
{
    public static void DoPhysics(VenomState state, IVenomHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (state.Field34D != 0) return;

        ActorBodyMotion body = host.Body;
        int flags = unchecked((int)state.Field218);
        if ((flags & 1) != 0)
        {
            if ((host.AnimationId != 5 && host.AnimationId != 4) || host.AnimationFinished) host.RunAnimation(5);
            AimAngles aim = FixedAim.Calculate(body.Position, host.PrimaryAimTarget);
            int savedPitch = aim.Pitch;
            CinematicActorMotion.TurnTowards(body, new(0, aim.Yaw), 10);
            int velY = body.AngularVelocity.Y;
            int signMask = velY >> 31;
            int absVelY = (signMask ^ velY) - signMask;
            int magnitude = absVelY >= 0x40 ? 0 : (((0x40 - absVelY) << 6) * 14) >> 12;
            body.Velocity = FixedAim.Velocity(magnitude, new AimAngles((short)savedPitch, aim.Yaw));
        }
        else if ((flags & 2) != 0)
        {
            AimAngles aim = FixedAim.Calculate(body.Position, host.SecondaryAimTarget);
            CinematicActorMotion.TurnTowards(body, new(0, aim.Yaw), 8);
        }
        else if ((flags & 4) != 0)
        {
            if (host.MechListPosition is not { } mech)
                throw new NotSupportedException("CVenom::DoPhysics field_218&4 requires G_MECHLIST (0x6A9038) position.");
            AimAngles aim = FixedAim.Calculate(body.Position, mech);
            CinematicActorMotion.TurnTowards(body, new(0, aim.Yaw), 8);
        }
        else
        {
            body.AngularVelocity = default;
            body.AngularAcceleration = default;
        }

        body.AngularVelocity = body.AngularVelocity with
        {
            X = Damp(body.AngularVelocity.X + body.AngularAcceleration.X),
            Y = Damp(body.AngularVelocity.Y + body.AngularAcceleration.Y)
        };
        body.AngularVelocity = body.AngularVelocity.KillSmall();
        for (int tick = 0; tick < host.ElapsedTicks; tick++)
        {
            body.Position += body.Velocity;
            body.Angles = (body.Angles + body.AngularVelocity).Mask();
        }
    }

    private static short Damp(int value) => unchecked((short)(value - (value >> 2)));
}
