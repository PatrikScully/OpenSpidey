using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Thugs;

public interface IThugTrapWeb
{
    int StrandCount { get; }
    void Burst();
}

public interface IThugReactionHost : IThugInjuryHost
{
    int AttackClock { get; }
    int PlayerAnimation { get; }
    int PlayerActionState { get; }
    short PlayerYaw { get; }
    void SetAttacker();
    bool GrabUpdate(ref FixedVector3 position, ref short yaw);
    int PathCheck(FixedVector3 from, FixedVector3 to, int clearance);
    bool AddPointToPath(FixedVector3 point, int argument);
    FixedVector3 GetPathPoint(int sourceIndex);
    FixedVector3 GetThrowPoint(int sourceIndex);
    ThugState? FindClosest(int type);
    void SendMessage(ThugState target, int code);
    void SendSelfMessage(int code);
    void ApplyHitToBody(IThugInjuryBody body, ActorHit hit);
    IThugTrapWeb? RecoverTrapWeb();
    int PlayerWebComboValue { get; }
    void SetPlayerWebCombo(int duration, int amount);
    ActorBodyMotion? RecoverConversationPartnerBody();
    void PlayDialogue(int bank, int clip, FixedVector3 position, int attenuation);
}

/// <summary>Recovered grab/yank/web reaction and acknowledgement states.</summary>
public static class ThugReactionBehavior
{
    public static void BackpedalPlease(ThugState state, ActorProcedureState actor, IThugReactionHost host)
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.ActionCounter = 0;
                FixedVector3 position = actor.Body.Position;
                FixedVector3 player = host.PlayerPosition;
                FixedVector3 point = new(unchecked(position.X + 6 * (unchecked(position.X - player.X) >> 2)), position.Y,
                    unchecked(position.Z + 6 * (unchecked(position.Z - player.Z) >> 2)));
                if (host.AddPointToPath(position, 0) && host.AddPointToPath(point, 0))
                    actor.Body.Velocity = ((actor.Body.Position - host.PlayerPosition) >> 2) with { Y = 0 };
                ThugInjuryBehavior.RunAppropriateHitAnim(state, host);
                state.Substate++;
                break;
            case 1:
                host.DoPhysics(1);
                if (state.ActionCounter <= 0) host.Neutralize();
                if (host.AnimationFinished != 0) { host.Neutralize(); state.State = 28; state.Substate = 0; }
                break;
            default: throw new InvalidOperationException($"Unknown thug backpedal substate {state.Substate}.");
        }
    }

    public static void GettingGrabbed(ThugState state, ActorProcedureState actor, IThugReactionHost host, Func<int, int> random)
    {
        host.SetAttacker();
        if (host.PlayerAnimation == 124)
        {
            ushort clip = state.Type == 304 ? (ushort)31 : (ushort)28;
            if (host.AnimationId != clip) host.RunAnimation(clip);
        }
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                host.RunAnimation(state.Type == 304 ? (ushort)34 : (ushort)29);
                state.Substate++;
                break;
            case 1:
                if (host.AnimationFinished != 0) RunGrabAnimation(state, host, false, actor.Body.Position, random);
                FixedVector3 target = host.GetPathPoint(state.PathPointIndex);
                if (ThugPerception.SourceAbs(unchecked(actor.Body.Position.X - target.X)) < 20 &&
                    ThugPerception.SourceAbs(unchecked(actor.Body.Position.Z - target.Z)) < 20)
                {
                    actor.Body.AngularVelocity = actor.Body.AngularVelocity with { Y = 0 };
                    actor.Body.AngularAcceleration = actor.Body.AngularAcceleration with { Y = 0 };
                    actor.Body.Angles = actor.Body.Angles with { Y = host.PlayerYaw };
                    state.PathPointIndex--;
                    state.Substate++;
                }
                else
                {
                    ActorYawSteering.YawTowards(actor.Body, host.PlayerYaw, 100);
                    FixedVector3 position = actor.Body.Position;
                    actor.Body.Position = new(unchecked(position.X + (unchecked(target.X - position.X) >> 1)), position.Y,
                        unchecked(position.Z + (unchecked(target.Z - position.Z) >> 1)));
                }
                break;
            case 2:
                FixedVector3 grabbedPosition = actor.Body.Position;
                short yaw = actor.Body.Angles.Y;
                bool updated = host.GrabUpdate(ref grabbedPosition, ref yaw);
                actor.Body.Position = grabbedPosition;
                actor.Body.Angles = actor.Body.Angles with { Y = yaw };
                if (!updated) actor.BehaviorFlags &= ~0x40u;
                if (host.AnimationFinished != 0) RunGrabAnimation(state, host, true, actor.Body.Position, random);
                break;
            default: throw new InvalidOperationException($"Unknown thug grab substate {state.Substate}.");
        }
    }

    public static void GetYankedBySpidey(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        IThugReactionHost host, int elapsedTicks)
    {
        if ((host.AttackClock & 3) == 0) WarnBuddy(host);
        if (state.State == 14) ApplyYankOffset(actor, state.YankOscillationTicks, true);
        switch (state.Substate)
        {
            case 0:
                state.TurnRate = 0;
                host.ClearAttackFlags();
                state.BodyFlags &= ~0x10u;
                state.CombatFlags &= ~0x20u;
                state.YankOscillationTicks = 0;
                procedures.Attach(new ThugStateSwitchMessageProcedure(state, 19, host.SendSelfMessage));
                host.RunAnimation(state.Type == 304 ? (ushort)18 : (ushort)16);
                if ((state.CombatFlags & 0x100) != 0) state.Substate = 9;
                else state.Substate = state.Health <= 0 ? 10 : 1;
                state.ActionFlags |= 2;
                break;
            case 1:
                state.ActionFlags |= 2;
                if (host.AnimationFinished != 0) host.CycleAnimation(state.Type == 304 ? (ushort)36 : (ushort)17);
                state.PreviousPosition = actor.Body.Position;
                host.DoPhysics(1);
                IThugInjuryBody? struck = null;
                if (state.ActionCounter > 0)
                {
                    struck = host.StruckGameObject(1, 1);
                    if (struck is null) break;
                    ProcessCollision(struck, false);
                }
                if (state.ActionCounter <= 0 || struck is not null)
                {
                    state.State = 15;
                    actor.Body.Velocity = default;
                    if (FallAfterCollision()) break;
                    if (struck is not null) { state.ActionCounter = 0; state.CombatFlags |= 0x400; }
                    PlayWallSound(state, actor, host);
                    host.RunAnimation(state.Type == 304 ? (ushort)14 : (ushort)19);
                    state.Substate++;
                }
                break;
            case 2:
                if (host.AnimationFinished != 0) { host.RunAnimation(state.Type == 304 ? (ushort)15 : (ushort)20); state.Substate++; }
                break;
            case 3:
                if (host.AnimationFinished != 0) { state.BodyFlags |= 0x10; state.State = 2; state.Substate = 0; }
                break;
            case 9:
                state.ActionFlags |= 2;
                if (state.Health <= 0) actor.BehaviorFlags |= 0x10;
                state.Substate = 10;
                goto case 10;
            case 10:
                if (host.AnimationFinished != 0) host.CycleAnimation(state.Type == 304 ? (ushort)36 : (ushort)17);
                state.PreviousPosition = actor.Body.Position;
                host.DoPhysics(1);
                IThugInjuryBody? body = host.StruckGameObject(1, 1);
                if (body is not null) ProcessCollision(body, true);
                if (state.ActionCounter <= 0 || body is not null)
                {
                    state.State = 15;
                    actor.Body.Velocity = default;
                    if (FallAfterCollision()) break;
                    if (body is not null || (state.CombatFlags & 0x100) == 0)
                    {
                        state.ActionCounter = 0;
                        state.CombatFlags |= 0x400;
                        PlayWallSound(state, actor, host);
                        if (state.Health <= 0) { state.State = 26; state.Substate = 0; }
                        else
                        {
                            host.PlayPositionalSound(0x801B, actor.Body.Position, 0);
                            host.RunAnimation(state.Type == 304 ? (ushort)14 : (ushort)19);
                            state.Substate = 2;
                        }
                    }
                    else { host.RunAnimation(state.Type == 304 ? (ushort)37 : (ushort)32); state.Substate = 11; }
                }
                break;
            case 11:
                if (host.AnimationFinished != 0)
                {
                    if (state.Health <= 0) { state.State = 26; state.Substate = 0; }
                    else { host.RunAnimation(state.Type == 304 ? (ushort)15 : (ushort)20); state.Substate = 3; }
                }
                break;
            default: throw new InvalidOperationException($"Unknown thug yank substate {state.Substate}.");
        }
        if (state.State == 14 && state.Substate > 0)
        {
            if ((state.CombatFlags & 0x20) != 0)
            {
                if (state.YankOscillationTicks != 0)
                    state.YankOscillationTicks = Math.Max(0, unchecked(state.YankOscillationTicks - elapsedTicks));
            }
            else
            {
                state.YankOscillationTicks = unchecked(state.YankOscillationTicks + elapsedTicks);
                if (state.YankOscillationTicks >= 40) state.CombatFlags |= 0x20;
            }
            ApplyYankOffset(actor, state.YankOscillationTicks, false);
        }

        void ProcessCollision(IThugInjuryBody target, bool clearDirection)
        {
            if (clearDirection) actor.BehaviorFlags &= ~0x10u;
            if (target.Type == 50)
            {
                actor.Body.Position = state.PreviousPosition;
                host.ApplyHitToBody(target, new ActorHit(2, 0, 10, 0, default));
            }
            if (state.ActionCounter > 1 && target.Type is 312 or 304)
                host.ElasticCollision(target, actor.Body.Velocity, state.ActionCounter >> 1);
        }
        bool FallAfterCollision()
        {
            if (ThugInjuryBehavior.ShouldFall(state, actor, host, 200, 389120) == 0) return false;
            state.State = 22;
            state.Substate = 0;
            state.CombatFlags &= ~2u;
            return true;
        }
    }

    public static void WebThrow(ThugState state, ActorProcedureState actor, IThugReactionHost host)
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.Substate++;
                host.RunAnimation(state.Type != 304 ? (ushort)32 : (ushort)37);
                state.ActionCounter = 0;
                goto case 1;
            case 1:
                state.ActionFlags |= 2;
                FixedVector3 point = host.GetThrowPoint(state.ActionCounter);
                int path = host.PathCheck(actor.Body.Position, point, 55);
                bool stop = path != 0 || !host.AddPointToPath(point, 0);
                if (!stop) { state.ActionCounter = unchecked(state.ActionCounter + 1); stop = state.ActionCounter >= 8; }
                if (stop)
                {
                    int fall = ThugInjuryBehavior.ShouldFall(state, actor, host, 200, 389120);
                    if (fall != 0)
                    {
                        if (fall == -1 || fall > 1024000) { state.State = 22; state.Substate = 0; state.CombatFlags |= 2; }
                        else state.Substate = 4;
                    }
                    else
                    {
                        if (path != 0) host.RunAnimation(state.Type != 304 ? (ushort)19 : (ushort)14);
                        state.Substate++;
                    }
                }
                else actor.Body.Position = host.GetThrowPoint(state.ActionCounter - 1);
                break;
            case 2:
                if (host.AnimationFinished != 0)
                {
                    state.Health = unchecked((short)(state.Health - 50));
                    if (state.Health > 0) { host.RunAnimation(state.Type != 304 ? (ushort)20 : (ushort)15); state.Substate++; }
                    else { state.State = 26; state.Substate = 0; }
                }
                break;
            case 3:
                if (host.AnimationFinished != 0) { state.BodyFlags |= 0x10; state.State = 0; state.Substate = 0; }
                break;
            case 4:
                actor.Body.Position = actor.Body.Position with { Y = unchecked(actor.Body.Position.Y + 409600) };
                if (actor.Body.Position.Y > state.LandingY)
                {
                    host.PlayPositionalSound(0x802E, actor.Body.Position, 0);
                    actor.Body.Position = actor.Body.Position with { Y = state.LandingY };
                    state.Substate = 2;
                }
                break;
            default: throw new InvalidOperationException($"Unknown thug web throw substate {state.Substate}.");
        }
    }

    public static void GetTrapped(ThugState state, ActorProcedureState actor, ActorProcedureList procedures,
        IThugReactionHost host, Func<int, int> random)
    {
        switch (state.Substate)
        {
            case 0:
                procedures.Attach(new ThugStateSwitchMessageProcedure(state, 14, host.SendSelfMessage));
                WarnBuddy(host);
                host.PlayPositionalSound(0x800E, actor.Body.Position, 0);
                state.TurnRate = 0;
                host.ClearAttackFlags();
                host.RunAnimation(state.Type != 304 ? (ushort)14 : (ushort)16);
                state.Substate++;
                break;
            case 1:
                if (host.AnimationFinished != 0)
                {
                    state.WorkCounter = 0;
                    state.WorkDelayCounter = 0;
                    state.ActionCounter = 5;
                    state.Substate = 2;
                    host.CycleAnimation(state.Type != 304 ? (ushort)15 : (ushort)17);
                    host.PlayPositionalSound(unchecked((ushort)((random(2) + 43) | 0x8000)), actor.Body.Position, 0);
                }
                break;
            case 2:
                if (state.WorkDelayCounter > 0) state.WorkDelayCounter--;
                state.ActionCounter = unchecked(state.ActionCounter - 1);
                if (state.ActionCounter <= 0)
                {
                    IThugTrapWeb? web = host.RecoverTrapWeb();
                    if (web is null || web.StrandCount == state.WorkCounter) state.Substate++;
                    else
                    {
                        state.WorkDelayCounter = unchecked(state.WorkDelayCounter + (unchecked(8000 * unchecked(web.StrandCount - state.WorkCounter)) >> 12));
                        state.ActionCounter = 5;
                        state.WorkCounter = web.StrandCount;
                        if (web.StrandCount >= 80)
                        {
                            state.State = 26;
                            state.Substate = 0;
                            host.SendDeathPulse();
                            state.Health = 0;
                            host.SetPlayerWebCombo(360, unchecked((host.PlayerWebComboValue + 11) << 10));
                            state.CombatFlags |= 0xC0;
                        }
                    }
                }
                break;
            case 3:
                state.WorkDelayCounter = unchecked(state.WorkDelayCounter - 1);
                if (state.WorkDelayCounter <= 0)
                {
                    host.RecoverTrapWeb()?.Burst();
                    state.State = 28;
                    state.Substate = 0;
                }
                break;
            default: throw new InvalidOperationException($"Unknown thug trapped substate {state.Substate}.");
        }
    }

    public static void Acknowledge(ThugState state, ActorProcedureState actor, ActorProcedureList procedures, IThugReactionHost host)
    {
        switch (state.Substate)
        {
            case 0:
                state.BodyFlags |= 0x10;
                host.Neutralize();
                if (state.DialogueClip < 0)
                {
                    host.RunAnimation(state.Type != 304 ? (ushort)39 : (ushort)32);
                    host.PlayDialogue(state.DialogueBank, unchecked(-state.DialogueClip), actor.Body.Position, 20);
                }
                if (host.RecoverConversationPartnerBody() is not null)
                    procedures.Attach(ActorLookAtProcedure.ForBody(actor, host.RecoverConversationPartnerBody, 2, 70, 200));
                state.Substate++;
                break;
            case 1:
                if (host.AnimationFinished != 0) host.CycleAnimation(state.IdleAnimation);
                if (state.ConversationTicks == 0)
                {
                    if (state.DialogueClip > 0)
                    {
                        host.PlayDialogue(state.DialogueBank, state.DialogueClip, actor.Body.Position, 20);
                        host.RunAnimation(state.Type != 304 ? (ushort)40 : (ushort)32);
                        state.Substate++;
                        return;
                    }
                    state.ConversationTicks = 2000;
                    state.State = 2;
                    state.Substate = 0;
                }
                break;
            case 2:
                if (host.AnimationFinished != 0) { state.ConversationTicks = 2000; state.State = 2; state.Substate = 0; }
                break;
            default: throw new InvalidOperationException($"Unknown thug acknowledgement substate {state.Substate}.");
        }
    }

    private static void WarnBuddy(IThugReactionHost host)
    {
        ThugState? buddy = host.FindClosest(304) ?? host.FindClosest(312);
        if (buddy is not null && buddy.AlertTicks == 0) host.SendMessage(buddy, 7);
    }
    private static void RunGrabAnimation(ThugState state, IThugReactionHost host, bool sound, FixedVector3 position, Func<int, int> random)
    {
        if (host.PlayerActionState == 0x8000000)
        {
            ushort held = state.Type == 304 ? (ushort)38 : (ushort)33;
            ushort alternate = state.Type == 304 ? (ushort)30 : (ushort)27;
            host.RunAnimation(host.AnimationId == held || host.AnimationId == alternate ? held : alternate);
            state.CombatFlags |= 4;
        }
        else
        {
            host.RunAnimation(state.Type == 304 ? (ushort)35 : (ushort)30);
            state.CombatFlags &= ~4u;
            if (sound) host.PlayPositionalSound(unchecked((ushort)((random(2) + 43) | 0x8000)), position, 0);
        }
    }
    private static void ApplyYankOffset(ActorProcedureState actor, int ticks, bool restore)
    {
        int height = unchecked(6144 * ticks);
        int pitch = unchecked(8 * ticks);
        FixedVector3 position = actor.Body.Position;
        ActorAngles angles = actor.Body.Angles;
        if (restore)
        {
            actor.Body.Position = position with { Y = unchecked(position.Y + height) };
            actor.Body.Angles = angles with { X = unchecked((short)(angles.X - pitch)) };
        }
        else
        {
            actor.Body.Position = position with { Y = unchecked(position.Y - height) };
            actor.Body.Angles = angles with { X = unchecked((short)(angles.X + pitch)) };
        }
    }
    private static void PlayWallSound(ThugState state, ActorProcedureState actor, IThugReactionHost host)
    {
        if (state.ActionCounter == 0) host.PlayPositionalSound((state.CombatFlags & 0x400) != 0 ? (ushort)0x802F : (ushort)0x802E,
            actor.Body.Position, 0);
    }
}
