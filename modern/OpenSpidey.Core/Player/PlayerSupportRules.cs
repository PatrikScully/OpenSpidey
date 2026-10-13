using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerSupportBody(ulong Id, FixedVector3 Velocity, bool Dead = false);

public interface IPlayerSupportActor
{
    PlayerSupportBody SupportState { get; }
    void NotifyTrodden();
}

public interface IPlayerSupportWorld
{
    PlayerSupportBody? FindSupport(ulong id);
    void NotifyTrodden(ulong id);
}

/// <summary>Recovered finalizer platform travel and jump impulse, separate from actor identity and engine queries.</summary>
public static class PlayerSupportRules
{
    public static FixedVector3 Ride(FixedVector3 position, FixedVector3 supportVelocity, bool airborne, IPlayerWorldQuery world)
    {
        FixedVector3 movement = airborne ? supportVelocity with { Y = 0 } : supportVelocity;
        if (movement == default) return position;
        FixedVector3 target = position + movement;
        return world.Trace(position, target) is null ? target : position;
    }

    public static int JumpVelocity(int? supportVerticalVelocity)
    {
        if (supportVerticalVelocity is not { } velocity) return -245760;
        return velocity >= -262144 ? unchecked(velocity - 245760) : unchecked(2 * velocity - 245760);
    }
}
