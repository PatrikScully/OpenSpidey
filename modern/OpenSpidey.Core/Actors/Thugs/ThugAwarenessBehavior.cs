using OpenSpidey.Core.Actors.Procedures;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugAwarenessHost : IThugFightHost, IThugGuardHost, IActorSoundHost
{
    short PlayerSurfaceClearance { get; }
    void ApplyHit(uint flags, int damage);
    void StrikeUpConversation();
}

/// <summary>Recovered caution, confusion and per-update AI timer/contact decisions.</summary>
public static class ThugAwarenessBehavior
{
    public static void Caution(ThugState state, ActorProcedureState actor, IThugAwarenessHost host, Func<int, int> random)
    {
        if (state.Substate != 0) throw new InvalidOperationException($"Unknown thug caution substate {state.Substate}.");
        state.TurnRate = 0;
        host.Neutralize();
        host.PlaySound(unchecked((ushort)((random(3) + 14) | 0x8000)));
        if (ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - state.GroundReferenceY - 0x4000)) < 409600 &&
            ThugPerception.PlayerIsVisible(state, actor, host))
        {
            if (!ShouldShootPlayer(state, actor, host))
            {
                state.State = host.Player.UnavailableFlag != 0 ? 1 : 4;
                state.Substate = 0;
            }
        }
        else if (!ThugFightBehavior.Determine(state, actor, host))
        {
            state.State = 2;
            state.Substate = 0;
        }
    }

    public static bool ShouldShootPlayer(ThugState state, ActorProcedureState actor, IThugAwarenessHost host)
    {
        if (host.Player.UnavailableFlag != 0 || host.HasFightOwner || Distance(2) >= 2000 || host.Player.HoldsObject ||
            (Distance(2) <= 650 && ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - state.GroundReferenceY - 0x4000)) <= 409600))
            return false;
        host.Neutralize();
        host.SetFightOwner();
        state.State = 9;
        state.Substate = 0;
        return true;

        int Distance(int tolerance) => ThugPerception.DistanceToPlayer(state, actor.Body, host.Player, host.AttackClock, tolerance);
    }

    public static void LookConfused(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        IThugGuardHost host, Func<int, int> random)
    {
        switch (state.Substate)
        {
            case 0:
                state.ActionCounter = unchecked(random(3) + 2);
                state.Substate++;
                goto case 1;
            case 1:
                host.Neutralize();
                if (state.ActionCounter == 0)
                {
                    state.AlertTicks = 0;
                    state.State = 28;
                    state.Substate = 0;
                }
                else
                {
                    state.Substate = 5;
                    state.ActionCounter = unchecked(state.ActionCounter - 1);
                }
                break;
            case 5:
                int angle = unchecked(random(1000) + 1000);
                if (random(2) != 0) angle = unchecked(-angle);
                angle = unchecked(angle + actor.Body.Angles.Y);
                procedures.Attach(ActorLookAtProcedure.ForYaw(actor, angle, 2, 80, 200));
                state.Substate++;
                break;
            case 6:
                ThugAnimationBehavior.RunAppropriate(state, actor.Body.MovementFlags, host);
                if ((actor.CompletionFlags & 2) != 0)
                {
                    state.Substate = 1;
                    actor.CompletionFlags &= ~2u;
                }
                break;
            default: throw new InvalidOperationException($"Unknown thug confusion substate {state.Substate}.");
        }
    }

    public static void DoAISwitchLogic(ThugState state, ActorProcedureState actor, ThugStateFlags flags,
        IThugAwarenessHost host, Func<int, int> random, int elapsedTicks)
    {
        if ((state.ActionFlags & 4) != 0 && (state.AttackOwnershipFlags & 1) != 0 &&
            Distance(0) < (state.Type != 304 ? 190 : 140))
        {
            host.Neutralize();
            state.State = 5;
            state.Substate = 0;
        }
        if ((state.CombatFlags & 0x80000) != 0 && state.State != 14)
        {
            host.ApplyHit(4, 50);
            state.CombatFlags &= ~0x80000u;
        }
        // These are the six contiguous words beginning at field_32C in the recovered actor.
        state.RangedCooldownTicks = Timer(state.RangedCooldownTicks);
        state.AlertTicks = Timer(state.AlertTicks);
        state.Timer334Ticks = Timer(state.Timer334Ticks);
        state.GunSoundCooldownTicks = Timer(state.GunSoundCooldownTicks);
        state.ConversationTicks = Timer(state.ConversationTicks);
        state.CollisionDelayTicks = Timer(state.CollisionDelayTicks);
        if (state.AlertTicks == 0 && BumpedIntoPlayer(state, actor, host, 100))
        {
            state.State = 3;
            state.Substate = 0;
        }
        if (state.ClosePlayerTicks != 0)
        {
            state.ClosePlayerTicks = Timer(state.ClosePlayerTicks);
            if (state.ClosePlayerTicks == 0 && Distance(2) < 300 &&
                ThugPerception.SourceAbs(unchecked(host.Player.Position.Y - actor.Body.Position.Y)) < 819200 &&
                (flags.Check(state.State) & 0x40) != 0 && state.AlertTicks == 0)
            {
                state.State = 3;
                state.Substate = 0;
            }
        }
        if (state.GunSoundDelayTicks != 0)
        {
            state.GunSoundDelayTicks = Timer(state.GunSoundDelayTicks);
            if (state.GunSoundDelayTicks == 0)
            {
                state.GunSoundCooldownTicks = 166;
                host.PlaySound(unchecked((ushort)((random(2) + 30) | 0x8000)));
            }
        }
        if ((state.CombatFlags & 0x8000) != 0 && state.ConversationTicks == 0 &&
            (flags.Check(state.State) & 0x20) != 0 && (host.AttackClock & 0xF) == 0)
            host.StrikeUpConversation();
        state.FightSuppressionTicks = Timer(state.FightSuppressionTicks);

        int Timer(int value) => Math.Max(0, unchecked(value - elapsedTicks));
        int Distance(int tolerance) => ThugPerception.DistanceToPlayer(state, actor.Body, host.Player, host.AttackClock, tolerance);
    }

    public static bool BumpedIntoPlayer(ThugState state, ActorProcedureState actor, IThugAwarenessHost host, int maximumDistance)
    {
        int distance = ThugPerception.DistanceToPlayer(state, actor.Body, host.Player, host.AttackClock, 4);
        if (distance >= maximumDistance) return false;
        int clearance = unchecked(state.SurfaceClearance - host.PlayerSurfaceClearance - (host.Player.Position.Y >> 12) + (actor.Body.Position.Y >> 12));
        return ThugPerception.SourceAbs(clearance) < 200;
    }
}
