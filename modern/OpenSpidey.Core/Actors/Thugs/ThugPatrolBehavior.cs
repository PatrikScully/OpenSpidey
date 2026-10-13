using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugPatrolHost : IThugGuardHost
{
    FixedVector3 GetPathPoint(int sourceIndex);
    void ClearAttackOwnership();
    bool DetermineFightState();
}

/// <summary>CThug::FollowWaypoints with real shared movement procedures and first-link route traversal.</summary>
public static class ThugPatrolBehavior
{
    public static void Step(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        ActorWaypointRoute route, IThugPatrolHost host, Func<int, int> random)
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.BodyFlags |= 0x10;
                actor.BehaviorFlags &= ~0x800u;
                state.TurnRate = 0;
                FixedVector3 destination;
                if (state.PathPointIndex != 0)
                {
                    actor.BehaviorFlags &= ~0x10000000u;
                    destination = host.GetPathPoint(state.PathPointIndex);
                    route.Flags = (route.Flags & ~2u) | 1;
                }
                else
                {
                    actor.BehaviorFlags |= 0x10000000;
                    destination = route.Position();
                    host.ClearAttackOwnership();
                }
                bool running = (route.Flags & 1) != 0;
                ThugAnimationBehavior.SetMode(state, running ? 1 : 0, !running, host);
                int speed = state.Type == 304 ? (running ? 192 : 120) : (running ? 144 : 48);
                if (running) route.Flags &= ~1u;
                procedures.AttachMove(new ActorMoveToProcedure(actor, destination, speed, 70, running ? 193 : 500, 1));
                state.Substate++;
                break;
            case 1:
                ThugAnimationBehavior.RunAppropriate(state, actor.Body.MovementFlags, host);
                if ((actor.CompletionFlags & 1) == 0) return;
                actor.CompletionFlags &= ~1u;
                if (state.State == 24)
                {
                    if (state.AlertTicks != 0)
                    {
                        state.State = 23;
                        state.Substate = 0;
                        return;
                    }
                    state.State = 2;
                }
                if (state.PathPointIndex != 0) state.PathPointIndex--;
                else
                {
                    state.CombatFlags &= ~1u;
                    if (!route.Advance())
                    {
                        state.State = 1;
                        state.Substate = 0;
                        return;
                    }
                }
                if (state.AlertTicks == 0 || !host.DetermineFightState())
                {
                    if ((route.Flags & 2) == 0)
                    {
                        state.Substate = 0;
                        return;
                    }
                    if (state.Type == 304) host.RunAnimation(10);
                    else if (random(2) != 0)
                    {
                        host.RunAnimation(21);
                        state.Substate = 5;
                        return;
                    }
                    else host.RunAnimation(22);
                    state.Substate++;
                }
                break;
            case 2:
                if (host.AnimationFinished != 0)
                {
                    host.CycleAnimation(state.Type == 304 ? (ushort)11 : (ushort)0);
                    state.PauseUpdates = 50;
                    state.Substate++;
                }
                break;
            case 3:
                int wait = state.PauseUpdates;
                state.PauseUpdates = unchecked(wait - 1);
                if (wait <= 0)
                {
                    if (state.Type == 304)
                    {
                        host.RunAnimation(12);
                        state.Substate++;
                    }
                    else state.Substate = 0;
                }
                break;
            case 4:
                if (host.AnimationFinished != 0) state.Substate = 0;
                break;
            case 5:
                if (host.MonitorSpitPlease()) state.Substate = 2;
                break;
            default:
                throw new InvalidOperationException($"Unknown thug patrol substate {state.Substate}.");
        }
    }
}
