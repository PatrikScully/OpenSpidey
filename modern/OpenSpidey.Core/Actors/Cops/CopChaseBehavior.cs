using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Cops;

/// <summary>Recovered CCop::ChasePlayer (0x42A060).</summary>
public static class CopChaseBehavior
{
    public static void ChasePlayer(CopState state, ActorProcedureState actor, ActorProcedureList procedures,
        CopCombatCoordinator combat, ICopHost host, Func<int, int> random, int elapsedTicks,
        Func<int, int> distanceToPlayer, Func<bool> determineFightState, Action runAppropriateAnim,
        Func<ushort, bool> setAnimMode, int depth = 0)
    {
        int meleeRange = state.Type != 306 ? 300 : 200;
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.BodyFlags |= 0x10;
                state.WorkCounter = 0;
                goto case 1;
            case 1:
                state.TurnRate = 20;
                if (setAnimMode(1))
                {
                    state.Substate = 5;
                    break;
                }
                SetFlags();
                FixedVector3 target = AttackPosition(state, host.Player.Position);
                if (distanceToPlayer(2) < meleeRange && HeightDelta(actor, host) < 204800)
                {
                    if ((state.AttackOwnershipFlags & 1) != 0)
                    {
                        state.Substate = 0;
                        state.State = host.Player.UnavailableFlag != 0 ? 1 : 5;
                    }
                    else state.Substate = 20;
                    break;
                }
                if (PathOk(actor, host, target) &&
                    (host.AddPointToPath(target, 0) ||
                     (host.AddPointToPath(actor.Body.Position, 0) && host.AddPointToPath(target, 0))))
                {
                    actor.Body.Acceleration = actor.Body.Acceleration with { Z = 0 };
                    actor.Body.LocalVelocity = actor.Body.LocalVelocity with { Z = 0 };
                    goto Advance;
                }
                state.AttackSlotCursor = unchecked((byte)(state.AttackSlotCursor + 1));
                host.Neutralize();
                if (host.Player.UnavailableFlag != 0 || (state.AttackOwnershipFlags & 1) == 0 || combat.HasFightOwner ||
                    host.Player.HoldsObject || distanceToPlayer(2) <= 650 || distanceToPlayer(2) >= 1500)
                    state.State = 1;
                else
                {
                    combat.FightOwner = state;
                    state.State = 9;
                }
                state.Substate = 0;
                combat.ClearAttackFlags(state);
                break;
            case 2:
                // The source's "What, no waypoint in cache?" print_if_false is inert; slot 0 is the last-seen position.
                procedures.AttachMove(new ActorMoveToProcedure(actor, host.GetPathPoint(state.PathPointIndex),
                    state.Type != 306 ? 144 : 200, 70, 193, 1));
                state.PreviousPosition = actor.Body.Position;
                goto Advance;
            case 3:
                if ((state.AttackOwnershipFlags & 1) != 0 && distanceToPlayer(0) < meleeRange)
                {
                    Stop(procedures, actor);
                    state.State = 5;
                    state.Substate = 0;
                    break;
                }
                runAppropriateAnim();
                if ((actor.CompletionFlags & 1) != 0)
                {
                    actor.CompletionFlags &= ~1u;
                    goto Advance;
                }
                int counter = state.ActionCounter;
                state.ActionCounter = unchecked(counter + 1);
                bool timed;
                if (counter <= 6)
                {
                    timed = false;
                    if (distanceToPlayer(2) >= (state.Type != 306 ? 450 : 300)) break;
                }
                else timed = true;
                SetFlags();
                if (distanceToPlayer(2) < 800)
                {
                    if (timed) state.WorkCounter = unchecked(state.WorkCounter + 1);
                    if (state.WorkCounter >= 6)
                    {
                        state.WorkCounter--;
                        if (host.Player.UnavailableFlag == 0 && !combat.HasFightOwner && !host.Player.HoldsObject)
                            goto Shoot;
                    }
                }
                if ((state.AttackOwnershipFlags & 1) == 0) { state.ActionCounter = 0; break; }
                if (distanceToPlayer(2) < meleeRange && HeightDelta(actor, host) < 204800)
                {
                    state.Substate = 0;
                    state.State = host.Player.UnavailableFlag != 0 ? 1 : 5;
                    break;
                }
                uint pathDistance = unchecked((uint)ThugPerception.ApproximateHorizontalDistance(actor.Body.Position,
                    host.GetPathPoint(state.PathPointIndex)));
                if (unchecked((uint)(distanceToPlayer(2) + 50)) < pathDistance)
                {
                    if (host.Player.UnavailableFlag != 0 || host.Player.HoldsObject || combat.HasFightOwner ||
                        distanceToPlayer(2) <= 650 || random(5) != 0)
                    {
                        Stop(procedures, actor);
                        state.Substate = 1;
                    }
                    else goto Shoot;
                }
                state.ActionCounter = 0;
                break;
            case 4:
                if ((state.AttackOwnershipFlags & 1) != 0 && distanceToPlayer(2) < meleeRange &&
                    HeightDelta(actor, host) < 204800)
                {
                    host.Neutralize();
                    host.RunAnimation(state.IdleAnimation);
                    state.State = 5;
                    state.Substate = 0;
                }
                else
                {
                    host.CycleAnimation(state.IdleAnimation);
                    state.Substate = 20;
                }
                break;
            case 5:
                if (host.AnimationFinished != 0) state.Substate = 1;
                break;
            case 20:
                state.TurnRate = 20;
                host.Neutralize();
                host.CycleAnimation(state.IdleAnimation);
                procedures.Attach(ActorLookAtProcedure.ForBody(actor, () => host.PlayerBody, 2, 100, 200));
                state.Substate++;
                break;
            case 21:
                state.ActionFlags |= 4;
                if ((actor.CompletionFlags & 2) != 0)
                {
                    state.ActionCounter = 30;
                    actor.CompletionFlags &= ~2u;
                    state.Substate = 22;
                }
                break;
            case 22:
                state.ActionCounter = Math.Max(0, unchecked(state.ActionCounter - elapsedTicks));
                if (state.ActionCounter != 0)
                {
                    state.ActionFlags |= 4;
                    break;
                }
                if (host.Player.UnavailableFlag != 0 || combat.HasFightOwner || distanceToPlayer(2) >= 2000 ||
                    host.Player.HoldsObject ||
                    (distanceToPlayer(2) <= 650 &&
                     ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - state.GroundReferenceY - 0x4000)) <= 409600))
                {
                    state.Substate = 1;
                    break;
                }
                goto Shoot;
            default:
                throw new InvalidOperationException("Unknown substate!");
        }
        return;

    Advance:
        state.Substate++;
        if (state.State == 4)
            ChasePlayer(state, actor, procedures, combat, host, random, elapsedTicks, distanceToPlayer,
                determineFightState, runAppropriateAnim, setAnimMode, unchecked(depth + 1));
        return;
    Shoot:
        host.Neutralize();
        combat.FightOwner = state;
        state.State = 9;
        state.Substate = 0;
        return;

        void SetFlags() => combat.SetAttackFlags(state, actor.Body.Position, host.Player.Position, distanceToPlayer,
            (mode, play) => state.SetAnimMode(mode, play, clip => host.RunAnimation(clip)));
    }

    private static FixedVector3 AttackPosition(CopState state, FixedVector3 player)
    {
        if ((state.AttackOwnershipFlags & 1) == 0 && (state.AttackOwnershipFlags & 2) != 0)
        {
            int distance = (state.AttackSlotMask & 0xAA) != 0 ? 1228800 : 1638400;
            if ((state.AttackSlotMask & 0xEE) != 0)
                player = player with { X = unchecked(player.X + (state.AttackSlotMask > 8 ? -distance : distance)) };
            if ((state.AttackSlotMask & 0xBB) != 0)
                player = player with { Z = unchecked(player.Z + ((state.AttackSlotMask & 0x83) != 0 ? distance : -distance)) };
        }
        return player;
    }

    private static bool PathOk(ActorProcedureState actor, ICopHost host, FixedVector3 target) =>
        host.PathCheck(actor.Body.Position, target, 55, (actor.BehaviorFlags & 0x2000) != 0) == 0;

    private static int HeightDelta(ActorProcedureState actor, ICopHost host) =>
        ThugPerception.SourceAbs(unchecked(actor.Body.Position.Y - host.Player.Position.Y));

    private static void Stop(ActorProcedureList procedures, ActorProcedureState actor)
    {
        procedures.Mark(ordinary: true);
        actor.Body.Acceleration = default;
    }
}
