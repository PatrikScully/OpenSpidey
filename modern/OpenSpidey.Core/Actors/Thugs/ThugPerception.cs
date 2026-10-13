using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public readonly record struct ThugPlayerState(FixedVector3 Position, short Health, uint BodyFlags,
    int UnavailableFlag, bool HoldsObject)
{
    // CBody::IsDead tests the zombie flag, independently of its health.
    public bool IsDead => (BodyFlags & 0x40) != 0;
}

public interface IThugFightHost : IActorMotionHost
{
    ThugPlayerState Player { get; }
    int AttackClock { get; }
    bool HasFightOwner { get; }
    bool LineOfSight(FixedVector3 from, FixedVector3 to, uint flags);
    int PathCheck(FixedVector3 from, FixedVector3 to, int clearance);
    bool AddPointToPath(FixedVector3 point, int argument);
    void SetFightOwner();
    void ClearAttackOwnership();
    void SendSignal();
}

/// <summary>Source visibility, cached distance and sight-cone eligibility without world-query substitutes.</summary>
public static class ThugPerception
{
    public static int DistanceToPlayer(ThugState state, ActorBodyMotion body, ThugPlayerState player,
        int attackClock, int cacheTolerance)
    {
        if (state.DistanceSampleClock != 0 && unchecked(attackClock - state.DistanceSampleClock) <= cacheTolerance)
            return state.CachedPlayerDistance;
        state.DistanceSampleClock = attackClock;
        state.CachedPlayerDistance = ApproximateHorizontalDistance(body.Position, player.Position);
        return state.CachedPlayerDistance;
    }

    public static bool PlayerIsVisible(ThugState state, ActorProcedureState actor, IThugFightHost host)
    {
        if (host.Player.IsDead || !host.LineOfSight(actor.Body.Position, host.Player.Position, 0)) return false;
        if (host.PathCheck(actor.Body.Position, host.Player.Position, 55) == 0)
        {
            state.LastSeenPlayerPosition = host.Player.Position;
            actor.BehaviorFlags |= 0x800;
        }
        return true;
    }

    public static bool CheckSightCone(ThugState state, ActorBodyMotion body, ThugPlayerState player, int attackClock)
    {
        if (state.Field374 == 0) return false;
        int vertical = unchecked(player.Position.Y - body.Position.Y);
        if (SourceAbs(vertical) > unchecked(state.Field374 << 12)) return false;
        int distance = DistanceToPlayer(state, body, player, attackClock, 2);
        if (distance < state.Field37C) return true;
        if (distance > state.Field370) return false;
        int delta = FixedAim.Calculate(body.Position, player.Position).Yaw - body.Angles.Y;
        if (delta < -2048) delta += 4096;
        else if (delta > 2048) delta -= 4096;
        return SourceAbs(delta) <= (state.Field378 >> 1);
    }

    public static int ApproximateHorizontalDistance(FixedVector3 first, FixedVector3 second)
    {
        int largest = SourceAbs(unchecked(first.X - second.X));
        int middle = 0;
        int smallest = SourceAbs(unchecked(first.Z - second.Z));
        // Keep the original three comparisons, including signed INT_MIN absolute-value overflow.
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12;
    }

    internal static int SourceAbs(int value) => value < 0 ? unchecked(-value) : value;
}
