using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugInjuryBody { int Type { get; } }

public interface IThugInjuryHost : IThugGuardHost, IActorHeightAdjustmentHost
{
    FixedVector3 PlayerPosition { get; }
    ushort ItemFlags { get; set; }
    bool HasJoints { get; }
    short JointOnePitch { get; set; }
    int GroundHeight(FixedVector3 position, int above, int below);
    void KillShadow();
    void ApplyPoseForType(int type);
    void ClearAttackFlags();
    void DoPhysics(int mode);
    IThugInjuryBody? StruckGameObject(int first, int second);
    void ElasticCollision(IThugInjuryBody target, FixedVector3 velocity, int duration);
    void CheckFallBack();
    void NotifyKill(int type);
    bool Die(int stage);
    void MarkDead();
    void SendDeathPulse();
    void PlayPositionalSound(ushort sound, FixedVector3 position, int attenuation);
    void PlayGlobalSound(ushort sound, int volume, int pan);
}

/// <summary>Recovered fall, flight-hit recovery and death state machines with explicit terrain/lifecycle effects.</summary>
public static class ThugInjuryBehavior
{
    private sealed class LandingHost(ThugState state) : IActorFallHost { public int LandingY => state.LandingY; }

    public static void Fall(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        IThugInjuryHost host, Func<int, int> random, int elapsedTicks)
    {
        state.ActionFlags |= 2;
        switch (state.Substate)
        {
            case 0:
                host.ClearAttackFlags();
                state.TurnRate = 0;
                if ((state.CombatFlags & 0x40002) == 0 && random(3) == 0) { state.Substate = 3; return; }
                goto case 1;
            case 1:
                actor.Body.Acceleration = actor.Body.Acceleration with { Y = 147456 };
                procedures.Attach(new ActorFallProcedure(actor, new LandingHost(state), 4));
                if ((state.CombatFlags & 2) == 0) host.CycleAnimation(state.Type != 304 ? (ushort)15 : (ushort)33);
                actor.BehaviorFlags &= ~1u;
                state.Substate++;
                state.ActionCounter = 65;
                if ((actor.BehaviorFlags & 0x400) == 0 || state.LandingY > unchecked(actor.Body.Position.Y + 1024000))
                {
                    FixedVector3 position = actor.Body.Position;
                    FixedVector3 player = host.PlayerPosition;
                    host.PlayPositionalSound(0x8018, new(Weighted(position.X, player.X), Weighted(position.Y, player.Y),
                        Weighted(position.Z, player.Z)), 0);
                }
                return;
            case 2:
                actor.Body.Angles = actor.Body.Angles with { X = unchecked((short)(actor.Body.Angles.X + 40)) };
                if (actor.Body.Velocity.Y >= 1228800) actor.Body.Velocity = actor.Body.Velocity with { Y = 1228800 };
                state.ActionCounter = Timer(state.ActionCounter, elapsedTicks);
                if ((actor.CompletionFlags & 4) != 0)
                {
                    actor.CompletionFlags &= ~4u;
                    host.Neutralize();
                    host.SendDeathPulse();
                    state.Health = 0;
                    actor.BehaviorFlags |= 0x8000;
                    host.PlayGlobalSound(0x802E, 0x2000, 0);
                    state.ActionCounter = 50;
                    state.Substate = 5;
                }
                else if (state.ActionCounter == 0)
                {
                    host.Neutralize();
                    host.SendDeathPulse();
                    state.Health = 0;
                    host.PlayGlobalSound(0x802E, 0x2000, 0);
                    state.ActionCounter = 50;
                    state.Substate = 6;
                }
                return;
            case 3:
                state.ActionCounter = 0;
                host.PlayPositionalSound(0x8010, actor.Body.Position, 0);
                host.CycleAnimation(state.IdleAnimation);
                state.Substate++;
                return;
            case 4:
                if (host.HasJoints)
                {
                    if (state.ActionCounter == 0)
                    {
                        host.JointOnePitch = unchecked((short)(host.JointOnePitch + 90));
                        if (host.JointOnePitch >= 900) { state.ActionCounter = 1; return; }
                    }
                    else
                    {
                        host.JointOnePitch = unchecked((short)(host.JointOnePitch - 90));
                        if (host.JointOnePitch <= 0) { host.ItemFlags &= unchecked((ushort)~4); state.Substate = 1; return; }
                    }
                }
                host.ItemFlags |= 4;
                host.ApplyPoseForType(state.Type);
                return;
            case 5:
            case 6:
                host.ItemFlags |= 1;
                state.ActionCounter = Timer(state.ActionCounter, elapsedTicks);
                if (state.ActionCounter == 0)
                {
                    host.PlayPositionalSound(0x802C, actor.Body.Position, 0);
                    if (state.Substate == 6) actor.BehaviorFlags |= 0x8000;
                    state.State = 27;
                    state.Substate = 0;
                }
                return;
            default: throw new InvalidOperationException($"Unknown thug fall substate {state.Substate}.");
        }
    }

    public static void FlyAcrossRoom(ThugState state, ActorProcedureState actor, IThugInjuryHost host, int elapsedTicks)
    {
        switch (state.Substate)
        {
            case 0:
                state.ActionFlags |= 2;
                host.ClearAttackFlags();
                state.TurnRate = 0;
                if (state.Health <= 0) state.BodyFlags &= ~0x10u;
                if (state.ReactionContext is 1 or 2 or 6 or 23) state.MinorRadius = 0;
                int speed = actor.Body.Velocity.X;
                if (ThugPerception.SourceAbs(speed) <= ThugPerception.SourceAbs(actor.Body.Velocity.Z)) speed = actor.Body.Velocity.Z;
                if (ThugPerception.SourceAbs(unchecked(speed * state.ActionCounter)) < 819200) RunAppropriateHitAnim(state, host);
                else host.CycleAnimation(state.Type == 304 ? (ushort)33 : (ushort)15);
                state.Substate = state.Health <= 0 || ((state.CombatFlags & 0x80000) != 0 && state.Health <= 50) ? 10 : 1;
                break;
            case 1:
                state.ActionFlags |= 2;
                state.PreviousPosition = actor.Body.Position;
                host.DoPhysics(1);
                IThugInjuryBody? struck = host.StruckGameObject(0, 1);
                if (struck is not null)
                {
                    actor.BehaviorFlags &= ~0x10u;
                    if (state.ActionCounter > 1 && struck.Type is 312 or 304)
                        host.ElasticCollision(struck, actor.Body.Velocity, state.ActionCounter >> 1);
                }
                if (state.ActionCounter == 0 || struck is not null)
                {
                    actor.Body.Velocity = default;
                    if (ShouldFall(state, actor, host, 200, 389120) != 0)
                    {
                        state.State = 22;
                        state.Substate = 0;
                        state.CombatFlags &= ~2u;
                    }
                    else
                    {
                        host.SetHeight(true, 100, 600);
                        PlayHitWallSound(state, actor, host);
                        if (state.ReactionContext is 1 or 2 or 6 or 23 or 25)
                        {
                            host.CheckFallBack();
                            host.RunAnimation(RecoveryClip(state, actor));
                            state.Substate++;
                        }
                        else
                        {
                            if (host.AnimationId == (state.Type == 304 ? 33 : 15)) { state.State = 28; state.Substate = 0; }
                            state.Substate = 5;
                        }
                    }
                }
                break;
            case 2:
                host.SetHeight(false, 100, 600);
                if (host.AnimationFinished != 0)
                {
                    state.MinorRadius = state.Type == 304 ? 96 : 150;
                    host.RunAnimation(state.Type == 304 ? (ushort)15 : (ushort)20);
                    state.Substate++;
                }
                break;
            case 3:
                host.SetHeight(false, 100, 600);
                if (host.AnimationFrame < 22) state.ActionFlags |= 1;
                if (host.AnimationFinished != 0)
                {
                    if (state.Health > 0) { state.BodyFlags |= 0x10; state.State = 2; state.Substate = 0; }
                    else DieAfterFlyingAcrossRoom(state, actor, host);
                }
                break;
            case 5:
                if (host.SetHeight(false, 100, 600) == 2 && host.AnimationFinished != 0) { state.State = 28; state.Substate = 0; }
                break;
            case 10:
                state.PreviousPosition = actor.Body.Position;
                host.DoPhysics(1);
                state.ActionFlags |= 2;
                if ((state.CombatFlags & 0x40000) != 0)
                {
                    ActorAngles angles = actor.Body.Angles;
                    actor.Body.Angles = new(unchecked((short)(angles.X + (short)elapsedTicks * (short)state.SlidePitchRate)),
                        unchecked((short)(angles.Y + (short)elapsedTicks * (short)state.SlideYawRate)), angles.Z);
                }
                // The source initializes body=this; a nonzero collision delay retains that truthy sentinel.
                bool hasBody = state.CollisionDelayTicks != 0;
                if (state.CollisionDelayTicks == 0)
                {
                    IThugInjuryBody? body = host.StruckGameObject(0, 1);
                    hasBody = body is not null;
                    if (body is not null)
                    {
                        actor.BehaviorFlags &= ~0x10u;
                        if (state.ActionCounter > 1 && body.Type is 312 or 304)
                        {
                            host.ElasticCollision(body, actor.Body.Velocity, state.ActionCounter >> 1);
                            if ((state.CombatFlags & 0x40000) != 0) { state.CollisionDelayTicks = 10; hasBody = false; }
                        }
                    }
                }
                if (state.ActionCounter == 0 || hasBody)
                {
                    actor.Body.Velocity = default;
                    actor.Body.Angles = actor.Body.Angles with { X = 0 };
                    DieAfterFlyingAcrossRoom(state, actor, host);
                }
                break;
            default: throw new InvalidOperationException($"Unknown thug flight substate {state.Substate}.");
        }
    }

    public static void DieThug(ThugState state, ActorProcedureState actor, IThugInjuryHost host, Func<int, int> random,
        int elapsedTicks, bool instant)
    {
        switch (state.Substate)
        {
            case 0:
                host.NotifyKill(state.Type);
                host.SetHeight(true, 100, 600);
                host.ClearAttackFlags();
                state.TurnRate = 0;
                host.Neutralize();
                state.BodyFlags &= ~0x10u;
                actor.BehaviorFlags |= 0x41000;
                if (instant) { host.Die(0); state.Substate = 3; }
                else if ((state.CombatFlags & 0x80) != 0) { host.RunAnimation(state.Type != 304 ? (ushort)29 : (ushort)39); state.Substate = 5; }
                else
                {
                    int clip = unchecked((ushort)host.AnimationId);
                    bool retained = state.Type == 304 ? clip is 26 or 14 or 37 : clip is 26 or 19 or 32;
                    if (!retained)
                    {
                        host.CheckFallBack();
                        host.RunAnimation(RecoveryClip(state, actor));
                    }
                    state.ActionCounter = 0;
                    state.Substate++;
                }
                break;
            case 1:
                host.SetHeight(false, 100, 600);
                if (host.AnimationFinished != 0) { state.MinorRadius = 0; host.Die(1); state.ActionCounter = 0; state.Substate++; }
                break;
            case 2:
                host.SetHeight(false, 100, 600);
                if (host.Die(2)) { host.Die(3); state.Substate++; }
                break;
            case 3: break;
            case 5:
                if (host.AnimationFinished != 0) { host.RunAnimation(state.Type != 304 ? (ushort)35 : (ushort)40); state.Substate++; }
                break;
            case 6:
                if (host.AnimationFinished != 0) { state.ActionCounter = 0; state.Substate = 7; }
                break;
            case 7:
                state.ActionCounter = unchecked(state.ActionCounter + elapsedTicks);
                if (state.ActionCounter >= 10)
                {
                    if ((actor.BehaviorFlags & 0x4000) == 0) host.SendDeathPulse();
                    host.CycleAnimation(state.Type != 304 ? (ushort)36 : (ushort)41);
                    state.ActionCounter = 120;
                    state.DeathSoundTicks = unchecked(random(300) + 150);
                    state.Substate++;
                }
                break;
            case 8:
                state.ActionCounter = Timer(state.ActionCounter, elapsedTicks);
                state.DeathSoundTicks = Timer(state.DeathSoundTicks, elapsedTicks);
                if (state.ActionCounter == 0) { host.RunAnimation(unchecked((ushort)host.AnimationId)); state.Substate = 1; }
                else if (state.DeathSoundTicks == 0)
                {
                    state.DeathSoundTicks = unchecked(random(300) + 150);
                    host.PlayPositionalSound(unchecked((ushort)((random(2) + 43) | 0x8000)), actor.Body.Position, 0);
                }
                break;
            default: throw new InvalidOperationException($"Unknown thug death substate {state.Substate}.");
        }
    }

    public static void DieAfterFlyingAcrossRoom(ThugState state, ActorProcedureState actor, IThugInjuryHost host)
    {
        if (ShouldFall(state, actor, host, 200, 0x5F000) != 0) { state.CombatFlags &= ~2u; state.State = 22; }
        else { host.SetHeight(true, 100, 600); PlayHitWallSound(state, actor, host); state.State = 26; }
        state.Substate = 0;
    }

    public static int ShouldFall(ThugState state, ActorProcedureState actor, IThugInjuryHost host, int above, int maximumDrop)
    {
        int ground = host.GroundHeight(actor.Body.Position, above, 4096);
        if (ground == -1)
        {
            state.LandingY = unchecked(actor.Body.Position.Y - 100);
            host.KillShadow();
            actor.BehaviorFlags &= ~0x400u;
            return -1;
        }
        int target = unchecked(ground - (state.SurfaceClearance << 12));
        int drop = unchecked(target - actor.Body.Position.Y);
        if (drop <= maximumDrop) return 0;
        host.KillShadow();
        state.LandingY = target;
        actor.BehaviorFlags |= 0x400;
        return drop;
    }

    public static void RunAppropriateHitAnim(ThugState state, IThugGuardHost host)
    {
        ushort clip;
        if ((state.CombatFlags & 0x10000) != 0) clip = state.Type != 304 ? (ushort)38 : (ushort)43;
        else if ((state.CombatFlags & 0x20000) != 0) clip = state.Type != 304 ? (ushort)37 : (ushort)42;
        else clip = state.Type != 304 ? (ushort)18 : (ushort)25;
        host.RunAnimation(clip);
    }

    private static void PlayHitWallSound(ThugState state, ActorProcedureState actor, IThugInjuryHost host)
    {
        if (state.ActionCounter == 0) host.PlayPositionalSound((state.CombatFlags & 0x400) != 0 ? (ushort)0x802F : (ushort)0x802E,
            actor.Body.Position, 0);
    }
    private static ushort RecoveryClip(ThugState state, ActorProcedureState actor)
    {
        if ((actor.BehaviorFlags & 0x10) != 0) return 26;
        return state.Type == 304 ? (ushort)14 : (ushort)19;
    }
    private static int Timer(int value, int elapsedTicks) => Math.Max(0, unchecked(value - elapsedTicks));
    private static int Weighted(int first, int second) => unchecked((unchecked(1600 * first) >> 12) + (unchecked(2400 * second) >> 12));
}
