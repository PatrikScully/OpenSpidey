using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Hostages;

public interface IHostageHost : IActorAnimationCycleHost, IActorHeightAdjustmentHost
{
    object Identity { get; }
    FixedVector3 PlayerPosition { get; }
    int AttackClock { get; }
    int Difficulty { get; }
    int AnimationId { get; }
    byte AnimationFinished { get; }
    uint WaypointFlags { get; }
    uint SpeechClock { get; }
    SpeechRequest? CurrentSpeech { get; }
    IReadOnlyList<ActorCollisionBody> SourceBaddyOrder { get; }
    void DoPhysics(int mode);
    void BuildTransform();
    int GroundHeight(FixedVector3 position, int above, int below);
    FixedVector3 WaypointPosition(int node);
    bool GetNextWaypoint();
    void RunAnimation(ushort clip, int from = 0, int to = -1);
    bool PlayPositionalSpeech(int bank, int slot, FixedVector3 position, int priority);
    void StopSpeech();
    void SendSignal();
    void SendMessage(object target, int code);
    ActorProcedureState? RecoverShooter(object? handle);
    ActorBodyMotion? RecoverBody(ActorProcedureState actor);
    void KillShadow();
    bool Die(int stage);
    void ReportUnknownSubstate(HostageBehaviorState state, int substate);
}

/// <summary>Original CHostage AI and all seven leaf states. Actor-list animation precedes this call.</summary>
public sealed class HostageBrain(HostageState state, ActorProcedureState actor, ActorProcedureList procedures,
    HostageMessageQueue messages, ActorSpeechAttachment speech, IHostageHost host, Func<int, int> random)
{
    private static readonly int[] RescueSpeechSlots = [0, 1, 2, 4, 12];

    public void Advance(int elapsedTicks)
    {
        host.DoPhysics(0); host.BuildTransform();
        for (HostageMessage? message = messages.Head; message is not null; message = message.Next)
        {
            if (!((actor.BehaviorFlags & 0x1000) != 0 || state.State == HostageBehaviorState.Dying) || message.Code >= 11)
            {
                switch (message.Code)
                {
                    case 1:
                        if (state.State == HostageBehaviorState.Begging) { message.Age = -60; continue; }
                        int age = message.Age; message.Age = unchecked(age + 1);
                        if (age < 15) continue;
                        TellSomebodyToShootMe();
                        break;
                    case 2: state.State = HostageBehaviorState.Begging; state.Substate = 0; state.ShooterHandle = message.SenderHandle; break;
                    case 3: state.State = HostageBehaviorState.Dying; state.Substate = 0; break;
                }
            }
            message.Flags |= 1;
        }
        messages.Cleanup();
        procedures.Execute(elapsedTicks);
        switch (state.State)
        {
            case HostageBehaviorState.Initializing:
                int ground = host.GroundHeight(actor.Body.Position, 300, 300);
                if (ground != -1)
                {
                    state.GroundHeight = ground;
                    actor.Body.Position = actor.Body.Position with { Y = unchecked(ground - (state.SurfaceClearance << 12)) };
                    state.GroundReferenceY = actor.Body.Position.Y;
                }
                host.CycleAnimation(0); state.State = HostageBehaviorState.WaitingForPlayer; state.Substate = 0;
                state.WaitUpdates = unchecked(random(120) + 120);
                break;
            case HostageBehaviorState.WaitingForPlayer: WaitForPlayer(); break;
            case HostageBehaviorState.FollowingWaypoints: FollowWaypoints(); break;
            case HostageBehaviorState.Begging: Beg(); break;
            case HostageBehaviorState.GettingUp: GetUp(); break;
            case HostageBehaviorState.Disappearing: Disappear(); break;
            case HostageBehaviorState.Dying: DieHostage(); break;
            default: host.ReportUnknownSubstate(state.State, state.Substate); break;
        }
        state.ShadowPosition = actor.Body.Position with { Y = unchecked(actor.Body.Position.Y + (state.SurfaceClearance << 12)) };
    }

    public void TellSomebodyToShootMe()
    {
        if (host.Difficulty is 0 or 1) return;
        ActorCollisionBody? nearest = null; int distance = 10656;
        foreach (ActorCollisionBody candidate in host.SourceBaddyOrder)
        {
            if (candidate.Type != 304 || ReferenceEquals(candidate.Identity, host.Identity)) continue;
            int range = unchecked((int)Distance(actor.Body.Position with { Y = candidate.Position.Y }, candidate.Position));
            if (distance <= range) continue;
            distance = range; nearest = candidate;
        }
        host.SendMessage(nearest?.Identity ?? host.Identity, nearest is null ? 1 : 13);
    }

    private void CheckIfFreed()
    {
        if (Distance(host.PlayerPosition, actor.Body.Position) >= 200 && (state.InputFlags & 1) == 0) return;
        if (host.Difficulty is 0 or 1) state.RescueFlags |= 1;
        host.SendSignal(); state.State = HostageBehaviorState.GettingUp; state.Substate = 0; TellSomebodyToShootMe();
    }
    private void WaitForPlayer()
    {
        switch (state.Substate)
        {
            case 0:
                state.WaitUpdates = unchecked(state.WaitUpdates - 1);
                if (state.WaitUpdates <= 0) { host.RunAnimation(1); state.Substate++; state.WaitUpdates = unchecked(random(120) + 120); }
                CheckIfFreed(); break;
            case 1:
                if (host.AnimationFinished != 0) { host.CycleAnimation(0); state.Substate = 0; }
                CheckIfFreed(); break;
            default: host.ReportUnknownSubstate(state.State, state.Substate); break;
        }
    }
    private void FollowWaypoints()
    {
        if ((state.RescueFlags & 1) != 0 && (state.RescueFlags & 2) == 0 && DistanceToPlayer(10) < 512)
        {
            state.RescueFlags |= 2;
            // This direct XA request deliberately does not attach to the hostage.
            host.PlayPositionalSpeech(7, RescueSpeechSlots[random(5)], actor.Body.Position, 100);
        }
        switch (state.Substate)
        {
            case 0:
                procedures.AttachMove(new(actor, host.WaypointPosition(state.CurrentWaypoint), 240, 80, 455, 1));
                host.SetHeight(true, 100, 600); state.Substate++;
                if ((host.WaypointFlags & 8) != 0) { FinishRoute(); break; }
                goto case 1;
            case 1:
                host.SetHeight(false, 100, 600);
                if ((actor.CompletionFlags & 1) != 0)
                {
                    actor.CompletionFlags &= ~1u;
                    if (host.GetNextWaypoint()) { state.Substate = 0; return; }
                    host.CycleAnimation(state.IdleAnimation); FinishRoute();
                }
                else RunAppropriateAnimation();
                break;
            default: host.ReportUnknownSubstate(state.State, state.Substate); break;
        }
    }
    private void FinishRoute() { state.Substate = 0; state.State = HostageBehaviorState.Disappearing; }
    private void Beg()
    {
        switch (state.Substate)
        {
            case 0:
                actor.Neutralize(procedures);
                ActorProcedureState? shooter = host.RecoverShooter(state.ShooterHandle);
                if (shooter is not null) procedures.Attach(ActorLookAtProcedure.ForBody(actor, () => host.RecoverBody(shooter), 0, 80, 200));
                if (host.Difficulty == 2) state.WaitUpdates = 100;
                else if (host.Difficulty == 3) state.WaitUpdates = 65;
                host.CycleAnimation(state.Type == 315 ? (ushort)6 : (ushort)5); state.Substate++;
                HostageSpeech(7, random(3) + 9, 50); break;
            case 1:
                state.WaitUpdates = unchecked(state.WaitUpdates - 1);
                if (state.WaitUpdates <= 0) { state.State = HostageBehaviorState.FollowingWaypoints; state.Substate = 0; }
                break;
            default: host.ReportUnknownSubstate(state.State, state.Substate); break;
        }
    }
    private void GetUp()
    {
        switch (state.Substate)
        {
            case 0: host.RunAnimation(2); state.Substate++; break;
            case 1:
                if (host.AnimationFinished != 0)
                {
                    if (host.GetNextWaypoint()) { state.State = HostageBehaviorState.FollowingWaypoints; state.Substate = 0; }
                    else { state.State = HostageBehaviorState.Disappearing; state.Substate = 1; }
                }
                break;
            default: host.ReportUnknownSubstate(state.State, state.Substate); break;
        }
    }
    private void Disappear()
    {
        switch (state.Substate)
        {
            case 0: actor.Neutralize(procedures); state.Substate++; goto case 1;
            case 1:
                state.BodyFlags &= 0xFFEF; actor.BehaviorFlags |= 0x5000; state.MinorRadius = 0;
                host.Die(1); state.Substate++; break;
            case 2:
                if (host.Die(2)) { host.Die(3); state.Substate++; }
                else host.SetHeight(false, 100, 600);
                break;
            case 3: break;
            default: host.ReportUnknownSubstate(state.State, state.Substate); break;
        }
    }
    private void DieHostage()
    {
        switch (state.Substate)
        {
            case 0:
                state.BodyFlags &= ~0x10u; procedures.Mark(ordinary: true); actor.Body.Acceleration = default; actor.Neutralize(procedures);
                state.ItemFlags |= 0x800; state.Transparency = 128; state.BodyFlags &= ~8u; host.KillShadow(); state.ItemFlags |= 0x400; state.DeathUpdates = 0;
                host.RunAnimation(state.Type == 315 ? (ushort)10 : (ushort)3);
                if (speech.CanStop(host.SpeechClock, host.CurrentSpeech)) host.StopSpeech();
                state.Substate++; goto case 1;
            case 1:
                state.DeathUpdates = unchecked(state.DeathUpdates + 1);
                state.TintColor = unchecked(((state.DeathUpdates & 1) != 0 ? 0xC03030u : 0) + 0x3F0F0F);
                if (state.DeathUpdates > 7) { state.Substate++; state.DeathUpdates = 0; }
                break;
            case 2:
                int old = state.DeathUpdates; state.DeathUpdates = unchecked(old + 1);
                if (old >= 20) { state.Substate = 3; host.Die(0); }
                int difference = unchecked(20 - state.DeathUpdates); difference = unchecked(difference + (difference << 4));
                difference = unchecked(difference * 3); difference = (difference << 10) >> 12;
                state.TintColor = unchecked((uint)((((difference << 10) | (difference & -4)) << 6) | (difference >> 2)));
                break;
            case 3: break;
            default: host.ReportUnknownSubstate(state.State, state.Substate); break;
        }
    }
    private void HostageSpeech(int bank, int slot, int priority)
    {
        if (host.PlayPositionalSpeech(bank, slot, actor.Body.Position, priority)) speech.Attach(host.SpeechClock, bank, slot);
    }
    private void RunAppropriateAnimation()
    {
        ushort first = (byte)state.MovementAnimations, second = (byte)(state.MovementAnimations >> 8);
        uint movement = actor.Body.MovementFlags;
        if ((movement & 0x40000) != 0)
        {
            if (first != second && host.AnimationId != first && host.AnimationId != second) { host.RunAnimation(first); return; }
            if (first == second || host.AnimationId != first || host.AnimationFinished != 0) host.CycleAnimation(second);
        }
        else if ((movement & 0x10000) != 0)
        {
            ushort clip = (byte)(state.MovementAnimations >> 16);
            if (host.AnimationId != clip || host.AnimationFinished != 0) host.RunAnimation(clip);
        }
        else if ((movement & 0x20000) != 0)
        {
            ushort clip = (byte)(state.MovementAnimations >> 24);
            if (host.AnimationId != clip || host.AnimationFinished != 0) host.RunAnimation(clip);
        }
        else host.CycleAnimation(state.IdleAnimation);
    }
    private int DistanceToPlayer(int maxAge)
    {
        if (state.DistanceSampleClock != 0 && unchecked(host.AttackClock - state.DistanceSampleClock) <= maxAge) return state.CachedPlayerDistance;
        state.DistanceSampleClock = host.AttackClock;
        state.CachedPlayerDistance = unchecked((int)Distance(actor.Body.Position with { Y = host.PlayerPosition.Y }, host.PlayerPosition));
        return state.CachedPlayerDistance;
    }
    private static uint Distance(FixedVector3 first, FixedVector3 second)
    {
        int largest = GameWord.Absolute(unchecked(first.X - second.X)), middle = GameWord.Absolute(unchecked(first.Y - second.Y)), smallest = GameWord.Absolute(unchecked(first.Z - second.Z));
        if (largest < middle) (largest, middle) = (middle, largest);
        if (middle < smallest) (middle, smallest) = (smallest, middle);
        if (largest < middle) (largest, middle) = (middle, largest);
        return unchecked((uint)(unchecked(largest + (middle >> 1) + (smallest >> 2)) >> 12));
    }
}
