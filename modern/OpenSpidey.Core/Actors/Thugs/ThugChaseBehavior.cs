using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugChaseHost : IThugAttackHost
{
    int PlayerUnavailableFlag { get; }
    bool PlayerHoldsObject { get; }
    bool HasFightOwner { get; }
    void SetFightOwner();
    int SetAttackFlags();
    void ClearAttackFlags();
    int PathCheckWithHit(FixedVector3 from, FixedVector3 to, int clearance, out FixedVector3 hit);
    FixedVector3 GetWaypointNearTarget(FixedVector3 target, int radius, byte phase);
    void ReportChaseDepth(int depth);
}

/// <summary>Recovered chase, attack-slot targeting and lost-player search with real movement scheduling.</summary>
public static class ThugChaseBehavior
{
    public static void Step(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        IThugChaseHost host, Func<int, int> random, int elapsedTicks, int depth = 0)
    {
        // The source diagnostic does not terminate dispatch or recursion.
        if (depth >= 5) host.ReportChaseDepth(depth);
        int meleeRange = state.Type != 304 ? 190 : 140;
        bool timed;
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.BodyFlags |= 0x10;
                state.WorkCounter = 0;
                if (state.Type != 304 && state.GrenadeElapsedTicks > 600)
                {
                    state.State = 11;
                    state.Substate = 0;
                    break;
                }
                goto case 1;
            case 1:
                state.TurnRate = 160;
                if (ThugAnimationBehavior.SetMode(state, 1, true, host))
                {
                    state.Substate = 5;
                    break;
                }
                host.SetAttackFlags();
                FixedVector3 target = GetAttackPosition(state, actor, host);
                if (host.DistanceToPlayer(2) < meleeRange && HeightWithin(204800))
                {
                    if ((state.AttackOwnershipFlags & 1) != 0) goto AttackOrGuard;
                    state.Substate = 20;
                    break;
                }
                int result = host.PathCheckWithHit(actor.Body.Position, target, 55, out FixedVector3 hit);
                if (result == 0 || (result == 2 && ApproximateDistance(hit, host.PlayerPosition) < (uint)meleeRange))
                {
                    if (result == 2) target = hit;
                    if (host.AddPointToPath(target, 0) ||
                        (host.AddPointToPath(actor.Body.Position, 0) && host.AddPointToPath(target, 0)))
                    {
                        actor.Body.Acceleration = actor.Body.Acceleration with { Z = 0 };
                        actor.Body.LocalVelocity = actor.Body.LocalVelocity with { Z = 0 };
                        goto Advance;
                    }
                }
                state.AttackSlotCursor = unchecked((byte)(state.AttackSlotCursor + 1));
                host.Neutralize();
                if (host.PlayerUnavailableFlag != 0 || (state.AttackOwnershipFlags & 1) == 0 || host.HasFightOwner ||
                    host.PlayerHoldsObject || host.DistanceToPlayer(2) <= 650 || host.DistanceToPlayer(2) >= 1500)
                    state.State = 1;
                else
                {
                    host.SetFightOwner();
                    state.State = 9;
                }
                state.Substate = 0;
                host.ClearAttackFlags();
                break;
            case 2:
                procedures.AttachMove(new ActorMoveToProcedure(actor, host.GetPathPoint(state.PathPointIndex),
                    state.Type != 304 ? 144 : 192, 70, 193, 1));
                goto Advance;
            case 3:
                if ((state.AttackOwnershipFlags & 1) != 0 && host.DistanceToPlayer(0) < meleeRange)
                {
                    StopProcedures();
                    state.State = 5;
                    state.Substate = 0;
                    break;
                }
                ThugAnimationBehavior.RunAppropriate(state, actor.Body.MovementFlags, host);
                if ((actor.CompletionFlags & 1) != 0)
                {
                    actor.CompletionFlags &= ~1u;
                    goto Advance;
                }
                int counter = state.ActionCounter;
                state.ActionCounter = unchecked(counter + 1);
                if (counter <= 6)
                {
                    timed = false;
                    if (host.DistanceToPlayer(2) >= (state.Type != 304 ? 285 : 210)) break;
                }
                else timed = true;
                host.SetAttackFlags();
                if (host.DistanceToPlayer(2) < 800)
                {
                    if (timed) state.WorkCounter = unchecked(state.WorkCounter + 1);
                    if (state.WorkCounter >= 6)
                    {
                        state.WorkCounter--;
                        if (host.PlayerUnavailableFlag == 0 && !host.HasFightOwner && !host.PlayerHoldsObject) goto Shoot;
                    }
                }
                if ((state.AttackOwnershipFlags & 1) == 0) break;
                if (host.DistanceToPlayer(2) < meleeRange && HeightWithin(204800)) goto AttackOrGuard;
                uint pathDistance = unchecked((uint)ThugPerception.ApproximateHorizontalDistance(actor.Body.Position,
                    host.GetPathPoint(state.PathPointIndex)));
                if (unchecked((uint)(host.DistanceToPlayer(2) + 50)) < pathDistance)
                {
                    if (host.PlayerUnavailableFlag != 0 || host.PlayerHoldsObject || host.HasFightOwner ||
                        host.DistanceToPlayer(2) <= 650 || random(5) != 0)
                    {
                        StopProcedures();
                        state.Substate = 1;
                    }
                    else goto Shoot;
                }
                state.ActionCounter = 0;
                break;
            case 4:
                if ((state.AttackOwnershipFlags & 1) != 0 && host.DistanceToPlayer(2) < meleeRange && HeightWithin(204800))
                {
                    host.Neutralize();
                    host.RunAnimation(state.IdleAnimation);
                    goto AttackOrGuard;
                }
                host.CycleAnimation(state.IdleAnimation);
                state.Substate = 20;
                break;
            case 5:
                if (host.AnimationFinished != 0) state.Substate = 1;
                break;
            case 20:
                state.TurnRate = 160;
                host.Neutralize();
                host.CycleAnimation(state.IdleAnimation);
                procedures.Attach(ActorLookAtProcedure.ForBody(actor, host.RecoverPlayerBody, 2, 100, 200));
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
                if (host.PlayerUnavailableFlag != 0 || host.HasFightOwner || host.DistanceToPlayer(2) >= 2000 || host.PlayerHoldsObject)
                {
                    state.Substate = 1;
                    break;
                }
                if (host.DistanceToPlayer(2) <= 650 &&
                    ThugPerception.SourceAbs(unchecked(host.PlayerPosition.Y - state.GroundReferenceY - 0x4000)) <= 409600)
                {
                    state.Substate = 1;
                    break;
                }
                goto Shoot;
            default:
                throw new InvalidOperationException($"Unknown thug chase substate {state.Substate}.");
        }
        return;

    Advance:
        state.Substate++;
        if (state.State == 4) Step(state, actor, procedures, host, random, elapsedTicks, unchecked(depth + 1));
        return;
    AttackOrGuard:
        state.Substate = 0;
        state.State = host.PlayerUnavailableFlag != 0 ? 1 : 5;
        return;
    Shoot:
        host.Neutralize();
        host.SetFightOwner();
        state.State = 9;
        state.Substate = 0;
        return;

        bool HeightWithin(int maximum) => ThugPerception.SourceAbs(unchecked(actor.Body.Position.Y - host.PlayerPosition.Y)) < maximum;
        void StopProcedures()
        {
            procedures.Mark(ordinary: true);
            actor.Body.Acceleration = default;
        }
    }

    public static FixedVector3 GetAttackPosition(ThugState state, ActorProcedureState actor, IThugChaseHost host)
    {
        FixedVector3 target = host.PlayerPosition;
        if ((state.AttackOwnershipFlags & 1) != 0)
        {
            FixedVector3 direction = (target - actor.Body.Position) >> (host.DistanceToPlayer(2) > 100 ? 12 : 8);
            direction = direction with { Y = 0 };
            direction = FixedVectorMath.Normalize(direction);
            direction *= (3200 * (state.Type != 304 ? 190 : 140)) >> 12;
            target -= direction;
        }
        else if ((state.AttackOwnershipFlags & 2) != 0)
        {
            int distance = (state.AttackSlotMask & 0xAA) != 0 ? 1228800 : 1638400;
            if ((state.AttackSlotMask & 0xEE) != 0)
                target = target with { X = unchecked(target.X + (state.AttackSlotMask > 8 ? -distance : distance)) };
            if ((state.AttackSlotMask & 0xBB) != 0)
                target = target with { Z = unchecked(target.Z + ((state.AttackSlotMask & 0x83) != 0 ? distance : -distance)) };
        }
        return target;
    }

    public static void LookForPlayer(ThugState state, ActorProcedureState actor, IThugChaseHost host, Func<int, int> random)
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                host.CycleAnimation(state.IdleAnimation);
                state.Substate++;
                state.ActorPhase = unchecked((byte)random(64));
                state.ActionCounter = 0;
                break;
            case 1:
                FixedVector3 origin = state.ActionCounter < 64 ? host.PlayerPosition : actor.Body.Position;
                FixedVector3 point = host.GetWaypointNearTarget(origin, 409600, state.ActorPhase);
                state.ActorPhase = unchecked((byte)(state.ActorPhase + 1));
                if (host.PathCheck(actor.Body.Position, point, 55) == 0 &&
                    (host.AddPointToPath(point, 0) || (host.AddPointToPath(actor.Body.Position, 0) && host.AddPointToPath(point, 0))))
                {
                    state.State = 24;
                    state.Substate = 0;
                }
                else
                {
                    int counter = state.ActionCounter;
                    state.ActionCounter = unchecked(counter + 1);
                    if (counter > 128)
                    {
                        state.AlertTicks = 0;
                        state.State = 2;
                        state.Substate = 0;
                    }
                }
                break;
            default: throw new InvalidOperationException($"Unknown thug search substate {state.Substate}.");
        }
    }

    private static uint ApproximateDistance(FixedVector3 first, FixedVector3 second)
    {
        int largest = ThugPerception.SourceAbs(unchecked(first.X - second.X));
        int middle = ThugPerception.SourceAbs(unchecked(first.Y - second.Y));
        int smallest = ThugPerception.SourceAbs(unchecked(first.Z - second.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked((uint)(unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12));
    }
}
