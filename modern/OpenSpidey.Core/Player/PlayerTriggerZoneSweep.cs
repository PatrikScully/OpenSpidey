using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Player;

public readonly record struct PlayerTriggerZoneRay(FixedVector3 Start, FixedVector3 End);

public interface IPlayerTriggerZoneSweepHost
{
    /// <summary>Source mode1 environmental/grid walk with RecordTriggerZoneHits=1 and TriggerCollisionCheck=0.</summary>
    void RecordTriggerZoneHits(PlayerTriggerZoneRay ray);
}

public interface IPlayerTriggerZoneFrameHost
{
    void FinishTriggerZoneSweep(FixedVector3 previousPosition);
}

/// <summary>Source4B7BF0's retained movement direction and sixteen-unit backtrack before the final player pose.</summary>
public sealed class PlayerTriggerZoneSweep
{
    // The original owns one process-static direction. A game session supplies one shared sweep owner.
    public FixedVector3 LastMoveDirection { get; private set; }

    public void RestoreDirection(FixedVector3 direction) => LastMoveDirection = direction;

    public PlayerTriggerZoneRay? Sweep(FixedVector3 position, FixedVector3 previousPosition, uint sourceAction,
        IPlayerTriggerZoneSweepHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (sourceAction == 0x80) return null;
        FixedVector3 movement = (position - previousPosition) >> 12;
        if (movement != default) LastMoveDirection = movement;
        FixedVector3 direction = FixedVectorMath.Normalize(LastMoveDirection);
        var ray = new PlayerTriggerZoneRay(previousPosition - direction * 16, position);
        host.RecordTriggerZoneHits(ray);
        return ray;
    }
}
