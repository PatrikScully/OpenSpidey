using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerTerrainFrame(FixedVector3 Position, FixedVector3 Velocity,
    FixedVector3 Acceleration, AxisDamping Damping, int ElapsedTicks, ushort SurfaceClearance,
    int ProbeReach, bool HoldingObject, int? PreviousSupportVerticalVelocity = null);

public readonly record struct PlayerTerrainPolicy(bool LimitVerticalMoveAfterCollision, PlayerGroundPolicy Ground);

public readonly record struct PlayerTerrainStep(PlayerMovementStep Movement, ushort CollisionFlags,
    PlayerSweepResult Sweep, PlayerWorldHit? CeilingHit, PlayerFallResult? Fall);

/// <summary>Ordinary terrain motion; actor actions, arena constraints and crawl/swing modes live above this adapter.</summary>
public static class PlayerTerrainMotion
{
    public static PlayerTerrainStep Step(PlayerTerrainFrame frame, IPlayerWorldQuery world, PlayerTerrainPolicy policy)
    {
        FixedVector3 velocity = PlayerMotion.UpdateVelocity(frame.Velocity, frame.Acceleration, frame.Damping);
        int savedFallVelocity = -1;
        if (velocity.Y >= 0)
        {
            savedFallVelocity = velocity.Y;
            velocity = velocity with { Y = 0 };
        }
        PlayerSweepResult sweep = PlayerMovementSweep.Resolve(frame.Position,
            PlayerMotion.Displacement(velocity, frame.ElapsedTicks), frame.ProbeReach, frame.ElapsedTicks, frame.HoldingObject, world);
        velocity = PlayerMotion.VelocityFromDisplacement(sweep.Displacement, frame.ElapsedTicks);
        FixedVector3 position = frame.Position;
        if (sweep.LastHit is null) position += sweep.Displacement;
        else if (!policy.LimitVerticalMoveAfterCollision || sweep.Displacement.Y < 0)
            position = position with { Y = unchecked(position.Y + sweep.Displacement.Y) };
        ushort collisions = sweep.CollisionFlags;
        PlayerWorldHit? ceiling = null;
        if (velocity.Y < 0 || frame.PreviousSupportVerticalVelocity < 0)
        {
            FixedVector3 start = position with { Y = unchecked(position.Y - sweep.Displacement.Y + 0x40000) };
            FixedVector3 end = position with { Y = unchecked(position.Y - 0xB8000) };
            ceiling = world.Trace(start, end);
            if (ceiling is not null && unchecked(position.Y - 0x78000) < ceiling.Value.Contact.Position.Y)
            {
                position = position with { Y = frame.Position.Y };
                velocity = velocity with { Y = 0 };
                collisions = (ushort)((collisions & 0xFFBF) | 0x100);
            }
        }
        PlayerFallResult? fall = null;
        if (savedFallVelocity >= 0)
        {
            var groundFrame = new PlayerGroundFrame(position, frame.Position, velocity, collisions,
                frame.SurfaceClearance, frame.HoldingObject, frame.Damping);
            fall = PlayerGroundSolver.ResolveFall(groundFrame, savedFallVelocity, frame.ElapsedTicks, world, policy.Ground);
            position = fall.Value.Movement.Position;
            velocity = fall.Value.Movement.Velocity;
            if (fall.Value.Ground is { } response) collisions = response.CollisionFlags;
        }
        return new(new(position, velocity), collisions, sweep, ceiling, fall);
    }
}
