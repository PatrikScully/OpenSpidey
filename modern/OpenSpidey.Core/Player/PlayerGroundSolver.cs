using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerGroundPolicy(bool WideGroundProbeEnabled, float GroundProbeSpread,
    bool LimitVerticalFallAfterGroundHit);

public readonly record struct PlayerFallResult(PlayerMovementStep Movement, PlayerGroundResponse? Ground, PlayerWorldHit? Hit);

/// <summary>Recovered falling probe sequence and ground response after horizontal movement.</summary>
public static class PlayerGroundSolver
{
    public static PlayerFallResult ResolveFall(PlayerGroundFrame frame, int savedFallVelocity, int elapsedTicks,
        IPlayerWorldQuery world, PlayerGroundPolicy policy)
    {
        int fall = PlayerMotion.FallDisplacement(savedFallVelocity, elapsedTicks);
        FixedVector3 start = frame.Position with { Y = unchecked(frame.Position.Y - 0x28000) };
        FixedVector3 end = start with { Y = unchecked(fall + start.Y + 0x8C000) };
        PlayerWorldHit? hit = world.Trace(start, end);
        if (hit is null && policy.WideGroundProbeEnabled)
            hit = ProbeWideGround(start, end, world, policy.GroundProbeSpread);
        if (hit is null)
        {
            FixedVector3 position = frame.Position with { Y = unchecked(frame.Position.Y + fall) };
            FixedVector3 velocity = frame.Velocity with { Y = savedFallVelocity };
            return new(new(position, velocity), null, null);
        }
        PlayerGroundResponse response = PlayerGroundResponder.Resolve(frame, hit.Value.Contact,
            fall, savedFallVelocity, policy.LimitVerticalFallAfterGroundHit);
        return new(new(response.Position, response.Velocity), response, hit);
    }

    private static PlayerWorldHit? ProbeWideGround(FixedVector3 start, FixedVector3 end,
        IPlayerWorldQuery world, float spread)
    {
        float x = start.X / 4096.0f;
        float z = start.Z / 4096.0f;
        int startY = GameWord.TruncateToInt32((start.Y / 4096.0f) * 4096.0f);
        int endY = GameWord.TruncateToInt32((end.Y / 4096.0f) * 4096.0f);
        for (uint index = 0; index <= 8; index++)
        {
            GroundProbePoint point = GroundProbeFan.Point(x, z, spread, index);
            int fixedX = GameWord.TruncateToInt32(point.X * 4096.0f);
            int fixedZ = GameWord.TruncateToInt32(point.Z * 4096.0f);
            PlayerWorldHit? hit = world.Trace(new(fixedX, startY, fixedZ), new(fixedX, endY, fixedZ));
            if (hit is not null) return hit;
        }
        return null;
    }
}
