using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.LizMan;

public interface ILizManHost
{
    ActorBodyMotion Body { get; }
    ActorProcedureState Actor { get; }
    ActorProcedureList Procedures { get; }
    int AnimationId { get; }
    int AnimationFrame { get; }
    bool AnimationFinished { get; }
    ushort ItemFlags { get; set; }
    int ElapsedTicks { get; }
    int AttackClock { get; }
    int TotalTime { get; }
    FixedVector3 PlayerPosition { get; }
    bool HasMessages { get; }
    bool HasLinkedClimbNode { get; }
    bool TryAdvanceWaypoint();
    int GroundHeight(FixedVector3 position, int above, int below, int flags);
    void SetHeight(bool initial, int above, int below);
    void RunAnimation(int animation);
    void Neutralize();
    void BuildTransform();
    void ProcessMessages();
    void UpdateFootstepBits();
    void PlaySounds();
    /// <summary>MechList body for CAIProc_LookAt targets; null when the player is gone.</summary>
    ActorBodyMotion? RecoverPlayerBody();
    /// <summary>CItem 0x128 frame clamp used by CLizMan::Jump.</summary>
    void SetAnimationFrame(int frame);
    /// <summary>CBody::KillShadow (0x460570).</summary>
    void KillShadow();
    int ScanNearbyNodesForJumpTarget();
    FixedVector3 TriggerPosition(int node);
    void PlayPositionalSound(int id);
    int Random(int bound);
}

/// <summary>CLizMan constructor tail after SquirtPos/Angles and AI (0x451350) for proven states.</summary>
public sealed class LizManBrain(LizManState state, LizManStateFlags flags, LizManAttackOwnership ownership, ILizManFollowWaypointsHost host)
{
    public LizManAttackOwnership AttackOwnership { get; } = ownership;
    public FixedVector3 TrajectoryScratch { get; private set; }

    public static void Construct(LizManState state, ILizManHost host, int totalTime, Action attachToBaddyList)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(attachToBaddyList);
        state.Field338 = 0x1000;
        state.AttackRelatedSnapshot = host.AttackClock;
        state.ShadowScale = 0x30;
        state.Field3AC = 0x21;
        state.Field374 = unchecked(totalTime - 0x131);
        attachToBaddyList();
        state.Field1F4 = state.SourceNode;
        state.MinorRadius = 0x80;
        state.Field1F8 = 0;
        state.State = 0;
        state.Substate = 0;
        state.ApplyLevelDefaults();
    }

    public void Advance()
    {
        if (state.LevelId == 0x1103 && state.State != 4)
            state.Field324 = unchecked(state.Field324 + host.ElapsedTicks);

        int followOn = LizManAnimationFollowOn.Resolve(host.AnimationFinished, host.AnimationId);
        if (followOn == 31) state.BehaviorFlags |= 0x4000;
        if (followOn >= 0)
        {
            state.Field340 = 0;
            host.RunAnimation(followOn);
        }
        if (ReferenceEquals(AttackOwnership.Shooter, host.AttackIdentity) && state.State != 7)
            AttackOwnership.Shooter = null;

        host.UpdateFootstepBits();
        if ((flags.Check(state.State) & 4) == 0)
            LizManPhysics.Advance(host.Body, host.AnimationId, host.AnimationFrame, host.ElapsedTicks, out var scratch);
        else
            TrajectoryScratch = default;
        if ((host.ItemFlags & 4) == 0) host.BuildTransform();
        host.PlaySounds();
        UpdateShadow();
        if (host.HasMessages) host.ProcessMessages();
        if (state.State != 0) LizManPerception.DoAiSwitchLogic(state, AttackOwnership, host);

        byte climb = state.Field390;
        state.Field394 = 0;
        state.CombatFlags &= ~0x20u;
        if (climb == 0 && ((flags.Check(state.State) & 0x80) != 0 ||
            ((state.BehaviorFlags & 0x8000) != 0 && (flags.Check(state.State) & 0x100) != 0)))
            host.SetHeight(false, 100, 600);

        switch (state.State)
        {
            case 0: Initialize(); break;
            case 1: LizManGuard.Guard(state, host); break;
            case 2:
                LizManFollowWaypoints.Advance(state, AttackOwnership, host.AttackIdentity, host);
                break;
            case 23: LizManGuard.Jump(state, host); break;
            case 25:
                // CLizMan::AI has no case 25; DoAISwitchLogic resolves it and the switch only asserts.
                break;
            case 15: StandStill(); break;
            default:
                throw new NotSupportedException(
                    $"CLizMan::AI state {state.State} (substate {state.Substate}) is not ported yet: {UnportedState(state.State)}.");
        }
        AfterState();
    }

    private void Initialize()
    {
        switch (state.Substate)
        {
            case 0:
                if ((state.Field2F0 & 0x20) != 0)
                {
                    if (!host.HasLinkedClimbNode)
                        throw new InvalidDataException("No linked climb node");
                    state.State = 2;
                    state.Substate = 0;
                    return;
                }
                if (unchecked(host.AttackClock - state.AttackRelatedSnapshot) > 10 && (host.AttackClock & 15) != 0)
                    return;
                int ground = host.GroundHeight(host.Body.Position, 300, 300, 0);
                if (ground == -1) return;
                int height = unchecked(ground - (state.SurfaceClearance << 12));
                host.Body.Position = host.Body.Position with { Y = height };
                state.Field29C = height;
                state.ShadowGroundY = ground;
                host.SetHeight(true, 100, 600);
                state.Substate++;
                state.Field340 = 0;
                host.RunAnimation(0);
                break;
            case 1:
                if (state.Field1F0 == 0 && !host.TryAdvanceWaypoint())
                {
                    state.State = 1;
                    state.Substate = 0;
                }
                else
                {
                    state.State = 2;
                    state.Substate = 0;
                }
                break;
            default:
                throw new InvalidDataException($"Unknown LizMan initialization substate {state.Substate}.");
        }
    }

    /// <summary>CLizMan::AI case 15 (inline StandStill): needs 0x2A8 bit 8, else hands back to the fight probe.</summary>
    private void StandStill()
    {
        state.Field374 = host.TotalTime;
        state.CombatFlags |= 0x20;
        if ((state.CombatFlags & 8) == 0)
        {
            state.State = 25;
            state.Substate = 0;
            return;
        }
        if (state.Substate != 0) return;
        host.Neutralize();
        state.Field340 = 0;
        host.RunAnimation(19);
        state.Substate++;
    }

    private static string UnportedState(int value) => value switch
    {
        3 => "ChasePlayer 0x44BC70",
        4 => "AttackPlayer 0x44CD70",
        7 => "ShootPlayer 0x44D610",
        8 => "TakeHit 0x44DAA0",
        9 => "BackpedalPlease 0x450480",
        10 or 11 => "GetYankedBySpidey 0x44E550",
        12 => "GetWhippedLikeTheWhoreYouAre 0x44E2D0",
        13 => "FlyAcrossRoom 0x44DEC0",
        14 => "GetTrapped 0x44DC00",
        16 => "state16 0x44FAF0",
        17 => "Acknowledge 0x44FFE0",
        18 => "Fall 0x44EFA0",
        19 => "ControlledDrop 0x44F850",
        20 or 21 => "DieLizMan 0x44EBB0",
        22 => "ClimbUp 0x44F640",
        24 => "SubwayFlyback 0x44EE60",
        _ => "original dispatch 0x451350"
    };

    private void AfterState()
    {
        ushort flag = flags.Check(state.State);
        if ((flag & 0x40) != 0 || state.Field390 != 0)
        {
            state.Field394 |= 1;
            state.CombatFlags |= 0x10200;
        }
        else
            state.CombatFlags &= ~0x10200u;

        if ((state.Field394 & 1) != 0)
            state.BodyFlags &= ~0x10u;
        else
        {
            state.BodyFlags |= 0x10;
            state.MinorRadius = 0x80;
        }
    }

    private void UpdateShadow()
    {
        if ((state.Field394 & 2) != 0)
        {
            if (state.ShadowScale < 5) state.ShadowScale = 0;
            else state.ShadowScale -= 5;
        }
        else
        {
            state.ShadowScale += 10;
            if (state.ShadowScale > 0x30) state.ShadowScale = 0x30;
            state.ShadowPosition = new(host.Body.Position.X,
                unchecked(host.Body.Position.Y + (state.SurfaceClearance << 12)), host.Body.Position.Z);
        }
    }
}
