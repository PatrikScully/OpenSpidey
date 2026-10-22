using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Mathematics;

namespace OpenSpidey.Core.Actors.Venom;

public interface IVenomHost : ICinematicActorScriptHost
{
    int TriggerNodeCount { get; }
    TriggerNode TriggerNode(int node);
    ushort TriggerNodeTypeCode(int node);
    short TriggerNodeSubtype(int node);
    IEnumerable<VenomControlSwitch> EnumerateControlSwitches();
    int LevelId { get; }
    int ElapsedTicks { get; }
    /// <summary>CItem 0x128 animation frame.</summary>
    int AnimationFrame { get; }
    /// <summary>gTimerRelated (0x6B4CA8).</summary>
    int TimerRelated { get; }
    FixedVector3 PlayerPosition { get; }
    FixedVector3 PrimaryAimTarget { get; }
    FixedVector3 SecondaryAimTarget { get; }
    FixedVector3? MechListPosition { get; }
    bool FastForwardRequested { get; }
    int DifficultyLevel { get; }
    /// <summary>Venom-only footstep repeat guard at 0x6B4E58.</summary>
    int LastFootstepSound { get; set; }
    ThugMessageQueue Messages { get; }
    int Random(int bound);
    void PlayPositionalSound(int sound);
    /// <summary>Redbook_XAPlayPos (0x479D30) with priority 0.</summary>
    void PlayPositionSpeech(int bank, int track, FixedVector3 position);
    /// <summary>Utils_LineOfSight (0x4E67A0) with no hit output and flags 0.</summary>
    bool LineOfSight(FixedVector3 from, FixedVector3 to);
    /// <summary>CSwitch field_100 of a ResolveSwitchNodes target (1 = can be flicked, 2 = flicked).</summary>
    int SwitchState(object? control);
    bool SwitchIsOn(object? control) => SwitchState(control) == 1;
    /// <summary>CSwitch::Flick (0x4D1870).</summary>
    void FlickSwitch(object? control);
    /// <summary>MechList->SetFocusLockTarget(this) (0x4B9E30).</summary>
    void SetPlayerFocusLockTarget();
    /// <summary>Panel_CreateCompass (0x463800).</summary>
    void CreateCompass(FixedVector3 position);
    /// <summary>Trig_SendPulseToNode.</summary>
    void SendPulseToNode(int node);
    /// <summary>CVenom::AdjustWaterModel (0x4F0FB0) for the l6a4 water level at field_338.</summary>
    void AdjustWaterModel(int waterLevel);
    /// <summary>CBaddy::Die(0) (0x404320).</summary>
    void Die();
    /// <summary>M3d_BuildTransform (0x475F50).</summary>
    void BuildTransform();
    void PulseL6A4Node(bool atOrigin) => VenomPulse.PulseL6A4Node(this, atOrigin);
    /// <summary>Combat boundaries; null on hosts that only run the node-travel states.</summary>
    IVenomCombatHost? Combat => null;
}

/// <summary>CVenom constructor (0x4E9600) and AI dispatch (0x4EC040) for walkabout/wait leaves.</summary>
public sealed class VenomBrain(VenomState state, IVenomHost host, CinematicActorScriptRuntime synthesizeScript)
{
    public static void Construct(VenomState state, IVenomHost host, Action attachToBaddyList, Action<VenomState>? createHealthBar = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(attachToBaddyList);
        if (state.LevelId is 0x9904 or 0x601 or 0x602 or 0x501 or 0x503 or 0x504 or 0x505 or 0x506)
        {
            state.BodyFlags &= ~0x10u;
            state.MinorRadius = 0;
        }
        attachToBaddyList();
        state.Field358 = state.SourceNode;
        state.AiState = 1;
        state.Substate = 0;
        switch (state.LevelId)
        {
            case 0x603:
                state.Health = 0x258;
                state.Field460 = 0xF0;
                state.Field464 = 0x2D0;
                state.Field39C = state.DifficultyLevel is 0 or 1 ? 0x3C : 0x14;
                host.ItemFlags |= 0x41;
                state.Field45C = 0;
                state.Substate = 1;
                break;
            case 0x604 or 0x601 or 0x602 or 0x501 or 0x502 or 0x503 or 0x504 or 0x505 or 0x506:
                state.AiState = 0x4000;
                state.Substate = 0;
                if (state.LevelId == 0x501) state.Field218 |= 0x8000;
                else if (state.LevelId == 0x502)
                {
                    state.Health = 0x258;
                    state.Field460 = 0x78;
                    state.Field464 = 0x168;
                    state.Field39C = state.DifficultyLevel is 0 or 1 ? 0x3C : 0x14;
                    createHealthBar?.Invoke(state);
                }
                else if (state.LevelId == 0x604)
                {
                    state.Health = 0x258;
                    state.Field460 = 0xF0;
                    state.Field464 = 0x2D0;
                    state.Field39C = state.DifficultyLevel is 0 or 1 ? 0x46 : 0x32;
                    state.Field33C = 1;
                    createHealthBar?.Invoke(state);
                    state.ResolveSwitchNodesPending = true;
                }
                break;
        }
    }

    private bool grounded;

    public void SnapToGround(Func<FixedVector3, int> groundHeight)
    {
        if (grounded) return;
        grounded = true;
        int ground = groundHeight(host.Body.Position);
        if (ground != -1)
            host.Body.Position = host.Body.Position with { Y = unchecked(ground - (state.SurfaceClearance << 12)) };
        if (state.ResolveSwitchNodesPending)
            VenomResolveSwitchNodes.Resolve(state, host);
    }

    public void Advance()
    {
        if (state.Field388 != 0)
        {
            state.Field388 = unchecked(state.Field388 - host.ElapsedTicks);
            if (state.Field388 < 0) state.Field388 = 0;
        }
        if ((state.Field218 & 0x8000) != 0)
        {
            state.Field330 = 15;
            host.CreateCompass(host.Body.Position);
        }
        if (host.AnimationId == 5) Footsteps();
        VenomPhysics.DoPhysics(state, host);
        ProcessMessages();
        host.Combat?.Procedures.Execute(host.ElapsedTicks);
        switch (state.AiState)
        {
            case 0x4000:
                VenomWalkabout.Tick(state, host, () => VenomSynthesizeAnalogueInput.Tick(state, host, synthesizeScript));
                break;
            case 1:
                VenomWait.Tick(state, host);
                break;
            case 2:
                VenomNodeStates.LookingToAttack(state, host);
                break;
            case 4:
                VenomNodeStates.Walkabout(state, host);
                break;
            case 8:
                VenomNodeStates.GoToSwitch(state, host);
                break;
            case 0x8000:
                if (state.Substate == 0)
                {
                    state.Field218 &= ~7u;
                    host.Body.Velocity = default;
                    host.RunAnimation(8);
                    state.Substate++;
                }
                else if (state.Substate == 1 && host.AnimationFinished)
                {
                    state.AiState = 32;
                    state.Substate = 0;
                }
                break;
            case 16:
                VenomJump.Jump(state, host, Combat);
                break;
            case 32:
                VenomFight.EnterFightMode(state, host, Combat);
                break;
            case 64:
                throw Unported("ImmediateAttack", 0x4EE7B0);
            case 128:
                VenomFight.GetWithinAttackRange(state, host);
                break;
            case 256:
                throw Unported("StretchGrabChokeDropAttack", 0x4EFEC0);
            case 512:
                throw Unported("WebYankAndPunchAttack", 0x4EF6D0);
            case 1024:
                VenomFight.GetTrapped(state, host, Combat);
                break;
            case 2048:
                VenomFight.TakeHit(state, host, Combat);
                break;
            case 4096:
                VenomFight.VenomDie(state, host);
                break;
            case 0x2000:
                VenomFight.TugWeb(state, host, Combat);
                break;
            default:
                // Original asserts and continues.
                break;
        }

        if (state.Field330 <= host.ElapsedTicks)
        {
            state.Field330 = 0;
            if (state.ElectrifiedPresent)
            {
                Combat.SetElectrified(false);
                state.ElectrifiedPresent = false;
            }
        }
        else
        {
            state.Field330 = unchecked(state.Field330 - host.ElapsedTicks);
            if (!state.ElectrifiedPresent && host.Combat is { } combat)
            {
                combat.SetElectrified(true);
                state.ElectrifiedPresent = true;
            }
        }
        VenomFade.Update(state, host);
        if ((host.ItemFlags & 4) == 0) host.BuildTransform();
        if (state.Field33C != 0) UpdateWater();
    }

    private void Footsteps()
    {
        int frame = host.AnimationFrame;
        if (frame < 7)
        {
            state.Field324 = 0;
            return;
        }
        int bit = frame >= 17 ? 2 : 1;
        if ((state.Field324 & bit) != 0) return;
        state.Field324 |= bit;
        int sound;
        do sound = host.Random(4) + 245;
        while (sound == host.LastFootstepSound);
        host.LastFootstepSound = sound;
        host.PlayPositionalSound(sound | 0x8000);
    }

    private IVenomCombatHost Combat => host.Combat ??
        throw new NotSupportedException("CVenom combat states need a host that owns the player, web and collision boundaries.");

    private void ProcessMessages()
    {
        ThugMessage? message = host.Messages.Head;
        if (message is null) return;
        if (host.Combat is { } combat)
        {
            VenomFight.ProcessMessages(state, host, combat);
            return;
        }
        for (; message is not null; message = message.Next)
        {
            if (state.AiState != 4096 && message.Code is 5 or 6 or 14 or 15)
                throw new NotSupportedException(
                    $"CVenom::AI message {message.Code} needs the unported trap/tug web handling (0x4EC040).");
            message.Flags |= 1;
        }
        host.Messages.Cleanup();
    }

    /// <summary>l6a4 tail: re-flag flickable switches and raise the water by the flicked count.</summary>
    private void UpdateWater()
    {
        int flicked = 0;
        state.Field3B8 = 0;
        for (int slot = 0; slot < state.SwitchNodeCount; slot++)
        {
            int switchState = host.SwitchState(state.SwitchTargets[slot]);
            if (switchState == 1) state.Field3B8 |= 1 << slot;
            else if (switchState == 2) flicked++;
        }
        int ticks = host.ElapsedTicks;
        int rise = host.DifficultyLevel == 2 ? unchecked((ticks * flicked) << 6)
            : unchecked((uint)host.DifficultyLevel) < 2 ? unchecked(32 * ticks * flicked) : unchecked(72 * ticks * flicked);
        state.Field338 = unchecked(state.Field338 + rise);
        if (state.Field338 > 233472)
        {
            state.Field338 = 233472;
            host.PulseL6A4Node(true);
        }
        host.AdjustWaterModel(state.Field338);
    }

    private static NotSupportedException Unported(string name, uint address) =>
        new($"CVenom::{name} (0x{address:X6}) is not ported yet: it installs a per-frame player AI override (player+0x554, e.g. 0x4F0A70/0x4F0CA0/0x4F0D70) that the modern player does not have.");
}
