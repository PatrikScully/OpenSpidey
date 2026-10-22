using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.LizMan;

/// <summary>CLizMan::FollowWaypoints (0x44C640): waypoint walk, optional climb leg and guard handoff.</summary>
public static class LizManFollowWaypoints
{
    public static void Advance(LizManState state, LizManAttackOwnership ownership, object identity, ILizManFollowWaypointsHost host)
    {
        switch (state.Substate)
        {
            case 0: BeginMove(state, ownership, identity, host); break;
            case 1: ArriveWaypoint(state, host); break;
            case 2:
                state.Substate = 3;
                break;
            case 3:
            {
                int timer = state.Field230;
                host.RunTimer(ref timer);
                state.Field230 = timer;
                if (state.Field230 != 0) break;
                state.Substate = 0;
                break;
            }
            case 16: ClimbLeg(state, host); break;
            default:
                throw new InvalidDataException($"Unknown CLizMan FollowWaypoints substate {state.Substate}.");
        }
    }

    private static void BeginMove(LizManState state, LizManAttackOwnership ownership, object identity, ILizManFollowWaypointsHost host)
    {
        host.Neutralize();
        state.CombatFlags &= ~0x800u;
        state.Field310 = 0;
        FixedVector3 destination;
        if (state.Field1F0 != 0)
        {
            state.CombatFlags &= ~0x10000000u;
            destination = host.PathPointPosition(state.Field1F0);
            host.RouteFlags = (host.RouteFlags & ~2u) | 1u;
        }
        else if (state.Field1F4 > 0)
        {
            state.CombatFlags |= 0x10000000;
            destination = host.WaypointPosition(state.Field1F4);
            LizManAttackFlags.Clear(state, identity, ownership);
        }
        else
        {
            TransitionToGuard(state);
            return;
        }

        uint route = host.RouteFlags;
        if ((route & 1) != 0)
        {
            if (state.Field390 != 0)
            {
                host.ClearClimbFacing();
                state.Field390 = 0;
            }
            int anim = host.AnimationId;
            host.RouteFlags = route & ~1u;
            if (anim == 5)
            {
                state.Field340 = 0;
                host.RunAnimation(7);
            }
            else if (anim != 8)
            {
                state.Field340 = 0;
                host.RunAnimation(8);
            }
            AttachMove(host, destination, 96, 70, 193);
            state.Substate = 1;
            return;
        }

        if ((route & 0x20) != 0)
        {
            host.EnsureMatrixFacing();
            host.RouteFlags = route & ~0x20u;
            state.Field390 = 1;
            state.Field340 = 0;
            host.RunAnimation(35);
            host.KillShadow();
            state.Substate = 16;
            return;
        }

        state.Field340 = 0;
        host.RunAnimation(2);
        AttachMove(host, destination, 56, 70, 500);
        state.Substate = 1;
    }

    private static void ArriveWaypoint(LizManState state, ILizManFollowWaypointsHost host)
    {
        if ((host.Actor.CompletionFlags & 1) == 0) return;
        host.Actor.CompletionFlags &= ~1u;
        int ground = host.GroundHeight(host.Body.Position, 0, state.SurfaceClearance + 64, 0);
        if (ground == -1)
        {
            // The original falls through with the -1 height stored.
            state.State = 18;
            state.Substate = 0;
        }
        host.ShadowGroundY = ground;
        host.SetHeight(true, 100, 600);
        if (state.Field1F0 != 0) state.Field1F0--;
        else if (!host.TryAdvanceWaypoint())
        {
            TransitionToGuard(state);
            return;
        }

        if (unchecked((uint)(host.TotalTime - state.Field374)) < 0x12C && host.DetermineFightState()) return;

        if ((host.RouteFlags & 2) == 0)
        {
            state.Substate = 0;
            return;
        }
        state.Field340 = 0;
        host.RunAnimation(5);
        state.Field230 = 50;
        state.Substate = 2;
    }

    private static void ClimbLeg(LizManState state, ILizManFollowWaypointsHost host)
    {
        if (host.AnimationId != 35)
        {
            state.Field340 = 0;
            host.RunAnimation(35);
        }
        state.Field394 |= 1;
        state.MinorRadius = 0;
        FixedVector3 target = host.WaypointPosition(state.Field1F4);
        FixedVector3 delta = target - host.Body.Position;
        int elapsed = host.ElapsedTicks;
        long distSq = (long)delta.X * delta.X + (long)delta.Y * delta.Y + (long)delta.Z * delta.Z;
        long threshold = (long)elapsed * elapsed << 8;
        if (distSq >= threshold)
        {
            FixedVector3 direction = FixedVectorMath.Normalize(delta >> 12);
            int speed = (96 * state.Field338) >> 12;
            host.Body.Velocity = (direction * speed) >> 4;
            host.Body.Acceleration = default;
            return;
        }
        if (host.TryAdvanceWaypoint())
        {
            if ((host.RouteFlags & 0x40) != 0)
            {
                state.Field318 = 1;
                return;
            }
            if (!host.WaypointHasLinks(state.Field1F4))
            {
                host.Body.Velocity = default;
                state.Field340 = 0;
                host.RunAnimation(36);
                state.State = 22;
                state.Substate = 0;
                return;
            }
            return;
        }
        if (state.Field318 != 0)
        {
            host.RouteFlags &= ~0x40u;
            state.Field340 = 0;
            host.RunAnimation(37);
            state.State = 19;
            state.Substate = 0;
            return;
        }
        host.ClearClimbFacing();
        int ground = host.GroundHeight(target with { Y = target.Y - (state.SurfaceClearance << 12) }, 128, 512, 0);
        if (ground == -1) throw new InvalidDataException("CLizMan FollowWaypoints climb leg found no ground.");
        host.ShadowGroundY = ground;
        host.Body.Position = new(target.X, ground - (state.SurfaceClearance << 12), target.Z);
        host.Body.Velocity = default;
        state.Field390 = 0;
        TransitionToGuard(state);
    }

    private static void AttachMove(ILizManFollowWaypointsHost host, FixedVector3 destination, int speed, int turn, int tolerance) =>
        host.Procedures.AttachMove(new ActorMoveToProcedure(host.Actor, destination, speed, turn, tolerance, 1));

    private static void TransitionToGuard(LizManState state)
    {
        state.State = 1;
        state.Substate = 0;
    }
}

public interface ILizManFollowWaypointsHost : ILizManMessageHost
{
    int ShadowGroundY { get; set; }
    FixedVector3 WaypointPosition(int node);
    FixedVector3 PathPointPosition(int index);
    void EnsureMatrixFacing();
    void ClearClimbFacing();
    /// <summary>CLizMan fight probe (0x44FD40).</summary>
    bool DetermineFightState();
    bool WaypointHasLinks(int node);
    void RunTimer(ref int ticks);
}
