using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

/// <summary>Recovered ground response, independent of the engine's world queries and actor objects.</summary>
public static class PlayerGroundResponder
{
    private const uint RestrictedWhileCarrying = 0x4000000;
    private const uint AlternateBrightness = 0x800000;
    private const ushort GroundCollision = 2;

    public static PlayerGroundResponse Resolve(PlayerGroundFrame frame, PlayerGroundContact contact,
        int fallDisplacement, int savedFallVelocity, bool limitVerticalFallAfterGroundHit)
    {
        FixedVector3 position = frame.Position;
        FixedVector3 velocity = frame.Velocity;
        FixedVector3 shadow = contact.Position;
        ushort collisions = frame.CollisionFlags;
        int clearance = frame.SurfaceClearance << 12;

        if (frame.HoldingObject && (contact.SurfaceFlags & RestrictedWhileCarrying) != 0)
        {
            position = frame.InitialPosition;
            collisions |= GroundCollision;
        }
        else if (contact.Normal.Y < -2600)
        {
            // Preserve signed floor quantization, including positions below zero.
            shadow = shadow with { Y = shadow.Y & unchecked((int)0xFFFFF000) };
            position = position with { Y = unchecked(shadow.Y - clearance) };
            velocity = velocity with { Y = 0 };
            collisions |= GroundCollision;
        }
        else if (contact.Distance > 0)
        {
            int slideX = unchecked((PlayerMotion.DampedValue(fallDisplacement, frame.Damping.X) >> 12) * contact.Normal.X);
            int slideZ = unchecked((PlayerMotion.DampedValue(fallDisplacement, frame.Damping.Z) >> 12) * contact.Normal.Z);
            int remainingFall = PlayerMotion.DampedValue(fallDisplacement, frame.Damping.Y);
            position = new(unchecked(position.X + slideX), unchecked(shadow.Y - clearance + remainingFall),
                unchecked(position.Z + slideZ));
            velocity = velocity with { Y = savedFallVelocity };
        }
        else
        {
            if (!limitVerticalFallAfterGroundHit || fallDisplacement < 0)
                position = position with { Y = unchecked(position.Y + fallDisplacement) };
            velocity = velocity with { Y = savedFallVelocity };
        }

        return new(position, velocity, collisions, 4, new(0, -4096, 0), shadow,
            contact.SupportsPlayer, (contact.SurfaceFlags & AlternateBrightness) != 0);
    }
}
