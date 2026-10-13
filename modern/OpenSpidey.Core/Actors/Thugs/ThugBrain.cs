using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;

namespace OpenSpidey.Core.Actors.Thugs;

/// <summary>Native actor effects and reaction handlers around the recovered AI dispatch.</summary>
public interface IThugBrainHost : IThugAwarenessHost, IThugChaseHost, IThugDamageHost, IThugHostageHost,
    IThugPatrolHost, IThugInitializationHost, IThugReactionHost
{
    new int AttackClock { get; }
    bool HasMessages { get; }
    void BuildTransform();
    void PlayAnimationSounds();
    void ProcessMessages();
    void WarnOtherThugs();
}

/// <summary>Source CThug AI ordering and state routing. Animation advances in the actor-list stage before this call.</summary>
public sealed class ThugBrain(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
    ActorWaypointRoute route, ThugStateFlags flags, IThugBrainHost host, Func<int, int> random)
{
    public void Advance(int elapsedTicks)
    {
        if (state.Type != 304 && state.LevelId == 0x1101 && state.State != 5)
            state.GrenadeElapsedTicks = unchecked(state.GrenadeElapsedTicks + elapsedTicks);
        if (state.State != 0) ThugAwarenessBehavior.DoAISwitchLogic(state, actor, flags, host, random, elapsedTicks);
        if ((flags.Check(state.State) & 4) == 0)
        {
            actor.Body.UseLocalVelocity = (actor.BehaviorFlags & 1) != 0;
            actor.Body.Integrate(elapsedTicks);
        }
        if ((((IThugRangedHost)host).ItemFlags & 4) == 0) host.BuildTransform();
        host.PlayAnimationSounds();
        if ((state.ActionFlags & 2) != 0) state.ShadowScale = Math.Max(0, state.ShadowScale - 5);
        else
        {
            state.ShadowScale = unchecked((ushort)(state.ShadowScale + 10));
            if (state.ShadowScale > 50) state.ShadowScale = 50;
            state.ShadowPosition = actor.Body.Position with { Y = unchecked(actor.Body.Position.Y + (state.SurfaceClearance << 12)) };
        }
        if (host.HasMessages) host.ProcessMessages();
        state.ActionFlags = 0;
        procedures.Execute(elapsedTicks);
        if (state.State == 28)
        {
            state.BodyFlags |= 0x10;
            state.TurnRate = 0;
            if (!ThugFightBehavior.Determine(state, actor, host))
            {
                state.State = host.Player.UnavailableFlag != 0 && state.AlertTicks != 0 ? 25 : 2;
                state.Substate = 0;
            }
        }
        else if ((flags.Check(state.State) & 0x40) != 0 &&
            (unchecked(host.AttackClock + state.ActorPhase) & state.FightPollInterval) == 0)
            ThugFightBehavior.Determine(state, actor, host);
        if (state.AlertTicks != 0 && (unchecked(host.AttackClock + state.ActorPhase) & 31) == 0 &&
            ThugPerception.PlayerIsVisible(state, actor, host)) host.WarnOtherThugs();
        actor.BehaviorFlags &= ~0x20u;
        Dispatch(elapsedTicks);
    }

    private void Dispatch(int elapsedTicks)
    {
        switch ((ThugBehaviorState)state.State)
        {
            case ThugBehaviorState.Initializing:
                ThugInitializationBehavior.Step(state, route, host, host.AttackClock);
                break;
            case ThugBehaviorState.Guarding:
                ThugGuardBehavior.Step(state, host, random);
                break;
            case ThugBehaviorState.Patrolling:
            case ThugBehaviorState.FollowingLastKnownPosition:
                ThugPatrolBehavior.Step(state, actor, procedures, route, host, random);
                host.SetHeight(false, 100, 600);
                break;
            case ThugBehaviorState.Cautious:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 600;
                ThugAwarenessBehavior.Caution(state, actor, host, random);
                host.SetHeight(false, 100, 600);
                break;
            case ThugBehaviorState.Chasing:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 600;
                ThugChaseBehavior.Step(state, actor, procedures, host, random, elapsedTicks);
                host.SetHeight(false, 100, 600);
                break;
            case ThugBehaviorState.MeleeAttacking:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 600;
                ThugAttackBehavior.Step(state, actor, procedures, host, random, elapsedTicks);
                host.SetHeight(false, 100, 600);
                break;
            case ThugBehaviorState.ShootingHostage: ThugHostageBehavior.Step(state, actor, procedures, host, random); break;
            case ThugBehaviorState.ShootingPlayer:
                state.AlertTicks = 600;
                actor.BehaviorFlags |= 0x20;
                ThugRangedBehavior.ShootPlayer(state, actor, procedures, host, random, elapsedTicks);
                break;
            case ThugBehaviorState.StoppingFire:
                actor.BehaviorFlags |= 0x20;
                if (state.Substate == 0) { host.RunAnimation(9); state.Substate++; }
                else if (state.Substate == 1)
                {
                    if (host.AnimationFinished != 0) { state.State = 28; state.Substate = 0; }
                }
                else throw new InvalidOperationException($"Unknown thug stop-shooting substate {state.Substate}.");
                break;
            case ThugBehaviorState.ThrowingGrenade:
                actor.BehaviorFlags |= 0x20;
                state.AlertTicks = 600;
                ThugRangedBehavior.TossGrenade(state, actor, procedures, host, random);
                break;
            case ThugBehaviorState.RecoveringFromHit:
                actor.BehaviorFlags |= 0x20;
                ThugDamageBehavior.TakeHit(state, actor, procedures, host);
                state.AlertTicks = 600;
                break;
            case ThugBehaviorState.Backpedaling: ThugReactionBehavior.BackpedalPlease(state, actor, host); break;
            case ThugBehaviorState.Yanked:
            case ThugBehaviorState.YankedAlternate:
                actor.BehaviorFlags |= 0x20;
                ThugReactionBehavior.GetYankedBySpidey(state, actor, procedures, host, elapsedTicks);
                state.AlertTicks = 600;
                break;
            case ThugBehaviorState.WebThrown: ThugReactionBehavior.WebThrow(state, actor, host); break;
            case ThugBehaviorState.FlyingFromHit:
                actor.BehaviorFlags |= 0x20;
                ThugInjuryBehavior.FlyAcrossRoom(state, actor, host, elapsedTicks);
                break;
            case ThugBehaviorState.TrappedInWeb:
                state.AlertTicks = 600;
                actor.BehaviorFlags |= 0x20;
                ThugReactionBehavior.GetTrapped(state, actor, procedures, host, random);
                break;
            case ThugBehaviorState.WebAttached:
                state.AlertTicks = 600;
                actor.BehaviorFlags |= 0x20;
                if ((actor.BehaviorFlags & 8) != 0)
                {
                    if (state.Substate == 0)
                    {
                        host.Neutralize();
                        host.CycleAnimation(state.Type == 304 ? (ushort)17 : (ushort)15);
                        state.Substate++;
                    }
                    else if (state.Substate != 1) throw new InvalidOperationException($"Unknown thug attached-web substate {state.Substate}.");
                }
                else { state.State = 28; state.Substate = 0; }
                break;
            case ThugBehaviorState.Grabbed:
                state.AlertTicks = 600;
                actor.BehaviorFlags |= 0x20;
                if ((actor.BehaviorFlags & 0x40) != 0) ThugReactionBehavior.GettingGrabbed(state, actor, host, random);
                else { host.PlaySound(0x802D); state.State = 13; state.Substate = 0; }
                break;
            case ThugBehaviorState.Acknowledging: ThugReactionBehavior.Acknowledge(state, actor, procedures, host); break;
            case ThugBehaviorState.Falling: ThugInjuryBehavior.Fall(state, actor, procedures, host, random, elapsedTicks); break;
            case ThugBehaviorState.Searching: ThugChaseBehavior.LookForPlayer(state, actor, host, random); break;
            case ThugBehaviorState.Confused: ThugAwarenessBehavior.LookConfused(state, actor, procedures, host, random); break;
            case ThugBehaviorState.Dying: ThugInjuryBehavior.DieThug(state, actor, host, random, elapsedTicks, false); break;
            case ThugBehaviorState.ImmediateDeath: ThugInjuryBehavior.DieThug(state, actor, host, random, elapsedTicks, true); break;
            default: throw new NotSupportedException($"Thug state {state.State} has no recovered AI dispatch.");
        }
    }
}
