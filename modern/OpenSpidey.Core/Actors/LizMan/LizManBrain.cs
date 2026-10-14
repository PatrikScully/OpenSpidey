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
    void LookAtPlayer(int holdTicks, int rate);
    int ScanNearbyNodesForJumpTarget();
    FixedVector3 TriggerPosition(int node);
    void PlayPositionalSound(int id);
    int Random(int bound);
}

/// <summary>CLizMan constructor tail after SquirtPos/Angles and AI (0x451350) for proven states.</summary>
public sealed class LizManBrain(LizManState state, LizManStateFlags flags, LizManAttackOwnership ownership, ILizManHost host)
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
            state.Field1F8 = unchecked(state.Field1F8 + host.ElapsedTicks);

        int followOn = LizManAnimationFollowOn.Resolve(host.AnimationFinished, host.AnimationId);
        if (followOn == 31) state.BehaviorFlags |= 0x4000;
        if (followOn >= 0)
        {
            state.Field340 = 0;
            host.RunAnimation(followOn);
        }

        host.UpdateFootstepBits();
        if ((flags.Check(state.State) & 4) == 0)
            LizManPhysics.Advance(host.Body, host.AnimationId, host.AnimationFrame, host.ElapsedTicks, out var scratch);
        else
            TrajectoryScratch = default;
        if ((host.ItemFlags & 4) == 0) host.BuildTransform();
        host.PlaySounds();
        UpdateShadow();
        if (host.HasMessages) host.ProcessMessages();
        if (state.State != 0) DoAiSwitchLogic();

        byte climb = state.Field390;
        state.Field394 = 0;
        state.CombatFlags &= ~0x20u;
        if (climb == 0 && ((flags.Check(state.State) & 0x80) != 0 ||
            ((state.BehaviorFlags & 0x8000) != 0 && (flags.Check(state.State) & 0x100) != 0)))
            host.SetHeight(false, 100, 600);

        switch (state.State)
        {
            case 0: Initialize(); break;
            case 1: Guard(); break;
            case 15: StandStill(); break;
            default:
                throw new NotSupportedException(
                    $"CLizMan::AI state {state.State} (substate {state.Substate}) is not ported yet; original dispatch 0x451350.");
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
                state.ShadowPosition = new(host.Body.Position.X, height, host.Body.Position.Z);
                // Original stores both ground and body Y into the shadow placement pair at 0x29C/0x2A0.
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

    private void Guard()
    {
        switch (state.Substate)
        {
            case 0:
                state.Field1F8 = 0;
                host.Neutralize();
                if (unchecked(host.TotalTime - state.Field374) < 0x12C)
                    host.LookAtPlayer(128, 200);
                state.Substate++;
                break;
            case 1:
                if (LizManAnimationFollowOn.PendingAnimation != 0xFFFF && host.AnimationId != 5)
                {
                    state.Field340 = 0;
                    host.RunAnimation(5);
                }
                state.Field1F8 = unchecked(state.Field1F8 + host.ElapsedTicks);
                if (state.Field1F8 <= 120) break;
                state.Field1F8 = 0;
                int target = host.ScanNearbyNodesForJumpTarget();
                if (target == 0) break;
                // Jump path: CalculateJumpPositionArray then state23 CLizMan::Jump remain unported.
                throw new NotSupportedException(
                    $"CLizMan::Jump (0x44F420) after ScanNearbyNodesForJumpTarget node {target} is not ported yet.");
            default:
                throw new InvalidDataException($"Unknown LizMan guard substate {state.Substate}.");
        }
    }

    private void StandStill()
    {
        switch (state.Substate)
        {
            case 0:
                host.Neutralize();
                state.Field340 = 0;
                host.RunAnimation(19);
                state.Substate++;
                break;
            case 1:
                throw new InvalidDataException("Unknown substate.");
            default:
                throw new InvalidDataException($"Unknown LizMan stand-still substate {state.Substate}.");
        }
    }

    private void DoAiSwitchLogic()
    {
        // CLizMan_DoAISwitchLogic 0x451280. The warn probe uses TotalTime against field_374 and AttackClock&phase.
        if (state.State == 25)
            throw new NotSupportedException("CLizMan::DoAISwitchLogic state25 probe (0x44FD40) is not ported yet.");
        if (unchecked((uint)(host.TotalTime - state.Field374)) < 0x12C &&
            (((host.AttackClock + state.ActorPhase) & 0x1F) == 0))
            throw new NotSupportedException("CLizMan::WarnOtherLizMans / AI switch probe (0x44D520) is not ported yet.");
    }

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
