using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cops;

/// <summary>Recovered CCop::AttackPlayer (0x42AEA0) and the close-range retreat at 0x42AC60.</summary>
public static class CopAttackBehavior
{
    public static void AttackPlayer(CopState state, ActorProcedureState actor, ActorProcedureList procedures,
        ICopHost host, Func<int, int> distanceToPlayer, int elapsedTicks)
    {
        int meleeRange = state.Type != 306 ? 300 : 200;
        switch (state.Substate)
        {
            case 0:
                state.TurnRate = 20;
                host.Neutralize();
                state.SetAnimMode(3, true, clip => host.RunAnimation(clip));
                state.BodyFlags |= 0x10;
                state.Substate++;
                goto case 1;
            case 1:
                if (host.Player.Health <= 0)
                {
                    state.State = 28;
                    state.Substate = 0;
                    break;
                }
                procedures.Attach(ActorLookAtProcedure.ForBody(actor, () => host.PlayerBody, 2, 100, 0));
                state.ActionCounter = 120;
                state.Substate++;
                break;
            case 2:
                state.ActionCounter = Math.Max(0, unchecked(state.ActionCounter - elapsedTicks));
                if (state.ActionCounter == 0)
                {
                    host.Neutralize();
                    state.State = 4;
                    state.Substate = 0;
                    break;
                }
                if (host.AnimationFinished != 0)
                    host.CycleAnimation(unchecked((ushort)((byte)(state.AnimationWord294 >> 8))));
                if ((actor.CompletionFlags & 2) == 0) break;
                actor.CompletionFlags &= ~2u;
                procedures.Mark(category: 0x100);
                actor.Body.AngularVelocity = actor.Body.AngularVelocity with { Y = 0 };
                actor.Body.AngularAcceleration = actor.Body.AngularAcceleration with { Y = 0 };
                host.RunAnimation(30);
                host.StartAttackMonitor(6, 142, 6, 0);
                host.PlayPositionSound(0x8014, actor.Body.Position, 0);
                state.Substate++;
                break;
            case 3:
                if (host.AnimationFinished == 0) break;
                host.CycleAnimation(state.IdleAnimation);
                state.Substate++;
                if (TryRetreat(state, actor, host, distanceToPlayer)) break;
                goto case 4;
            case 4:
                // The source's in-range branch stores substate 1 and still falls into the switch to state 28.
                if (distanceToPlayer(2) < meleeRange &&
                    ThugPerception.SourceAbs(unchecked(actor.Body.Position.Y - host.Player.Position.Y)) < 204800)
                    state.Substate = 1;
                state.State = 28;
                state.Substate = 0;
                break;
            case 5:
                if (host.AnimationFinished != 0) host.CycleAnimation(state.IdleAnimation);
                int pause = state.PauseUpdates;
                state.PauseUpdates = unchecked(pause - 1);
                if (pause <= 0) state.Substate = 1;
                break;
            case 6:
                if (host.AnimationFinished != 0)
                    host.CycleAnimation(unchecked((ushort)((byte)(state.AnimationWord294 >> 8))));
                state.ActionCounter = unchecked(state.ActionCounter - 1);
                if (state.ActionCounter != 0)
                {
                    FixedVector3 position = actor.Body.Position;
                    FixedVector3 target = host.GetPathPoint(state.PathPointIndex);
                    actor.Body.Position = new(unchecked(position.X + (unchecked(target.X - position.X) >> 1)),
                        position.Y, unchecked(position.Z + (unchecked(target.Z - position.Z) >> 1)));
                }
                else state.Substate = 4;
                break;
            default:
                throw new InvalidOperationException("Unknown substate!");
        }
    }

    public static bool TryRetreat(CopState state, ActorProcedureState actor, ICopHost host,
        Func<int, int> distanceToPlayer)
    {
        bool police = state.Type == 306;
        int minimum = police ? 105 : 162;
        if (distanceToPlayer(2) >= minimum) return false;
        int clearance = police ? 120 : 185;
        int factor = police ? 17 : 11;
        FixedVector3 position = actor.Body.Position;
        FixedVector3 player = host.Player.Position;
        int distance = distanceToPlayer(2);
        int x = unchecked(position.X + ((factor * (position.X - player.X) * (clearance - distance)) >> 12));
        int z = unchecked(position.Z + ((factor * (position.Z - player.Z) * (clearance - distance)) >> 12));
        var target = new FixedVector3(x, position.Y, z);
        if (host.PathCheck(position, target, 55, (actor.BehaviorFlags & 0x2000) != 0) != 0 ||
            !host.AddPointToPath(target, 0))
            return false;
        state.Substate = 6;
        state.ActionCounter = 3;
        return true;
    }
}
