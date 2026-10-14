using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Combat;

namespace OpenSpidey.Core.Player;

/// <summary>
/// Terrain and surface locomotion in source update order. Web, combat, carried-object,
/// scripted input and arena actions are connected by their own controllers as they are ported.
/// </summary>
public sealed class PlayerTerrainController
{
    private readonly IPlayerWorldQuery world;
    private readonly IPlayerAnimationDriver animation;
    private readonly PlayerTerrainPolicy policy;
    private readonly Func<int, int> random;
    private PlayerMovementInputState input;
    private PlayerHeadingTurn turn;
    private AxisDamping damping = new(1, 4, 1);
    private FixedVector3 acceleration;
    private FixedVector3 normal = new(0, 4096, 0);
    private bool groundInitialized;
    private int headingDelta;
    private byte groundGrace;
    private FixedVector3 orientationReference;
    private int forwardClearance;
    private int cornerDistance;
    private PlayerWorldHit? zipTarget;
    private FixedVector3 zipOrigin;
    private bool zipLocked;
    private byte pendingWebReleaseAnimation;
    private PlayerButtonInput buttons;
    private PlayerJumpImpulse jump;
    private int sourceTicks;
    private int fallReferenceTickFallback;
    private int fallReferenceTick
    {
        get => CombatState?.FallReferenceTick ?? fallReferenceTickFallback;
        set { fallReferenceTickFallback = value; if (CombatState is { } combat) combat.FallReferenceTick = value; }
    }
    private bool suppressAirControlFallback;
    private bool suppressAirControl
    {
        get => CombatState is { } combat ? combat.FieldAE5 != 0 : suppressAirControlFallback;
        set { suppressAirControlFallback = value; if (CombatState is { } combat) combat.FieldAE5 = value ? (byte)1 : (byte)0; }
    }
    private byte zipDiveHeadingRetargetFlag;
    public byte ZipDiveHeadingRetargetFlag
    {
        get => CombatState?.Field551 ?? zipDiveHeadingRetargetFlag;
        private set
        {
            zipDiveHeadingRetargetFlag = value;
            if (CombatState is { } combat) combat.Field551 = value;
        }
    }
    private bool runningJumpFallback;
    private bool SuppressCameraRelativeAirHeading
    {
        get => CombatState is { } combat ? combat.FieldE8C != 0 : runningJumpFallback;
        set
        {
            runningJumpFallback = value;
            if (CombatState is { } combat) combat.FieldE8C = value ? (byte)1 : (byte)0;
        }
    }
    private IPlayerHitHost? combatHost;
    private IPlayerDamageTimerHost? combatTimerHost;
    private IPlayerDeathProgressHost? deathProgressHost;
    public IPlayerMeleeFrameHost? Melee {get;set;}
    public IPlayerWebFrameHost? Web { get; set; }
    public IPlayerLookaroundFrameHost? Lookaround { get; set; }
    public IPlayerCameraFacingFrameHost? CameraFacing { get; set; }
    public IPlayerSwingFrameHost? Swing { get; set; }
    public IPlayerSmashFrameHost? Smash { get; set; }
    public IPlayerLandingRecoveryFrameHost? LandingRecovery { get; set; }
    public IPlayerWalkingFrameHost? Walking { get; set; }
    public IPlayerAirborneMovementFrameHost? AirborneMovement { get; set; }
    public IPlayerTriggerZoneFrameHost? TriggerZones { get; set; }
    public IPlayerPowerUpCollectionFrameHost? PowerUps { get; set; }
    public IPlayerSurfaceBrightnessHost? SurfaceBrightness { get; set; }
    public PlayerMovementInputState MovementInput => input;
    public PlayerButtonInput CurrentButtons => buttons;
    public PlayerJumpImpulse SourceJumpImpulse => jump;
    /// <summary>The actual sourceEA4 contact grace byte, shared with crawl/ground physics and CheckGroundGone.</summary>
    public byte SourceGroundContactGrace { get => groundGrace; set => groundGrace = value; }
    public void SetSourceSpeedRamp(int value) => SpeedRamp = value;
    public byte PendingWebReleaseAnimation
    {
        get => CombatState?.PendingWebReleaseAnimation ?? pendingWebReleaseAnimation;
        set
        {
            pendingWebReleaseAnimation = value;
            if (CombatState is { } combat) combat.PendingWebReleaseAnimation = value;
        }
    }
    public FixedVector3 OrientationReference { get => orientationReference; set => orientationReference = value; }
    public void SetSwingJumpImpulse(PlayerJumpImpulse impulse) => jump = impulse;
    public void ClearJumpDurations() => jump = jump with { Phase = 0, Hold = 0 };
    public void SetForcedAirControl(sbyte upDown, sbyte leftRight, int ramp)
    {
        input = input with { Axes = new(upDown, leftRight) };
        SpeedRamp = ramp;
    }
    /// <summary>Source lookaround camera and indicators run after movement/pose and before button-edge reset.</summary>
    public event Action<int>? AfterPlayerMovement;

    public FixedVector3 Position { get; private set; }
    public FixedVector3 Velocity { get; private set; }
    public PlayerAction Action { get; private set; } = PlayerAction.Idle;
    public ushort CollisionFlags { get; private set; }
    public int SpeedRamp { get; private set; }
    public PlayerOrientation Orientation { get; }
    public PlayerHeadingTurn HeadingTurn => turn;
    /// <summary>CPlayer::SetStartOrientation multiplies the authored yaw into the current matrix before its genuine non-target surface alignment.</summary>
    public void SetStartOrientation(short yaw)
    {
        normal = CombatState?.CrawlSurfaceNormal ?? normal;
        PlayerStartOrientationResult result = Orientation.SetStartOrientation(yaw, normal, headingDelta);
        if (result.StandRequired) Stand();
        if (CombatState is { } state)
        {
            state.ForwardDirection = Orientation.Forward;
            state.UpDirection = Orientation.Up;
            if (result.StandRequired)
            {
                state.ActionState = (uint)Action;
                state.AnimationId = animation.Playback.ClipId;
                state.AnimationFrame = animation.Playback.Frame;
            }
        }
    }
    public PlayerInputSuppression InputSuppression { get; } = new();
    public PlayerTerrainStep LastTerrainStep { get; private set; }
    public PlayerCrawlStep? LastCrawlStep { get; private set; }
    public PlayerSupportBody? SupportBody { get; private set; }
    public void ReleaseSupport(ulong sourceId)
    {
        if (SupportBody?.Id == sourceId) SupportBody = null;
    }
    public ushort AirborneTicks { get; private set; }
    public Func<FixedVector3, FixedVector3>? BodyHook { private get; set; }
    public bool IsCrawling { get; private set; }
    public bool OnWall => IsCrawling && normal.Y is >= -2600 and <= 3400;
    public bool OnCeiling => IsCrawling && normal.Y > 3400;
    public bool HardSurface { get; private set; }
    public PlayerWorldHit? ZipTarget => zipTarget;
    public bool ZipWebVisible { get; private set; }
    public bool IsGrounded => (CollisionFlags & 2) != 0;
    public PlayerCombatState? CombatState { get; private set; }
    public uint SourceTimeTicks => unchecked((uint)sourceTicks);
    public event Action<uint>? BeginPlayerAi;
    public event Action? AfterPlayerAi;
    public event Action<uint>? BeforePlayerInput;
    public event Action? BeforeDamageTimers;
    public event Action? AfterDamageTimers;
    public const ushort SurfaceClearance = 96;
    public const int ProbeReach = 70;

    public PlayerTerrainController(FixedVector3 position, short yaw, IPlayerWorldQuery world,
        IPlayerAnimationDriver animation, PlayerTerrainPolicy policy, Func<int, int>? random = null)
    {
        Position = position;
        Orientation = new(yaw);
        this.world = world;
        this.animation = animation;
        this.policy = policy;
        this.random = random ?? Random.Shared.Next;
        animation.Run(0);
    }

    public void Step(PlayerInputSample sample, short cameraHeading, int ticks = 2, PlayerButtonInput buttons = default,
        Func<PlayerControlFrame>? scriptedControls = null)
    {
        if (ticks <= 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        this.buttons = buttons;
        FixedVector3 previousFramePosition = Position;
        sourceTicks = unchecked(sourceTicks + ticks);
        animation.Advance(ticks);
        // Ob_AI updates the pose before CPlayer::AI walks its own web list and invokes SpideyAI0.
        if (CombatState is { } preparation) preparation.FieldAE4 = 1;
        BeginPlayerAi?.Invoke(SourceTimeTicks);
        BeforePlayerInput?.Invoke(SourceTimeTicks);
        if (CombatState is { } music) PlayerFightMusicFade.Advance(music, ticks);
        if (CombatState is { } combat)
        {
            CaptureCombatState(ticks);
            combat.SynthesizedInputActive = scriptedControls is not null;
            BeforeDamageTimers?.Invoke();
            PlayerCombatTimers.Advance(combat, combatTimerHost!);
            AfterDamageTimers?.Invoke();
        }
        AirborneTicks = ((uint)Action & 0x800004) != 0 && !IsGrounded
            ? unchecked((ushort)(AirborneTicks + ticks)) : (ushort)0;
        if (Action != PlayerAction.Falling) fallReferenceTick = sourceTicks;
        PlayerInputSuppressionStep suppression = InputSuppression.Advance(ticks, scriptedControls is not null);
        if (suppression.RestoredAnimationSpeed is { } speed) animation.Speed = speed;
        if (suppression.ClearButtons)
        {
            this.buttons = default;
            buttons = default;
            sample = sample with { Up = false, Down = false, Right = false, Left = false,
                LeftOnePressed = false, LeftTwoTriggered = false };
        }
        InitializeGroundContact();
        animation.Speed = 0x10000;
        int previousVerticalVelocity = Velocity.Y;
        PlayerSupportBody? previousSupport = RefreshSupport();
        SupportBody = null;
        if (Swing is not null)
        {
            CaptureCombatState(ticks);
            Swing.PreparePlayerFrame();
        }
        UpdatePhysics(ticks, previousSupport?.Velocity.Y);
        if (Orientation.Orient(normal, headingDelta, OnCeiling ? orientationReference : null)) Stand();
        damping = SelectDamping();
        if(Melee is not null){CaptureCombatState(ticks);Melee.PrepareTargets(ticks);}
        if (scriptedControls is not null)
        {
            PlayerControlFrame controls = scriptedControls();
            sample = controls.Movement;
            this.buttons = controls.Buttons;
            buttons = controls.Buttons;
        }
        input = PlayerMovementInput.Read(input, sample, new(suppression.BlockMovement, 0, 0, false, false)).State;
        Melee?.ReadControls(input,buttons);
        if (Lookaround is not null)
        {
            buttons = Lookaround.ReadControls(input, buttons, groundGrace);
            this.buttons = buttons;
        }
        if (!buttons.IsHeld(PlayerButton.Swing)) zipLocked = false;
        SpeedRamp = PlayerFlatMovement.UpdateSpeedRamp(SpeedRamp, input.Axes, ticks);
        ushort? completed = null;
        if (PlayerAnimationFollowOns.Resolve(animation.Playback, random) is { } next)
        {
            completed = animation.Playback.ClipId;
            animation.Run(next);
        }
        turn = turn with { Heading = Orientation.GetEffectiveHeading(OnWall) };
        short wanted = (short)((input.Heading + (OnWall ? 0 : cameraHeading)) & 4095);
        switch (Action)
        {
            case PlayerAction.SwingWindup:
            case PlayerAction.SwingRelease:
            case PlayerAction.Swinging:
            case PlayerAction.FenceTransition:
                CaptureCombatState(ticks);
                IPlayerSwingFrameHost swing = Swing ?? throw new InvalidOperationException("Swing actions require the direct native swing owner.");
                if (Action == PlayerAction.SwingWindup) swing.UpdateWindup();
                else if (Action == PlayerAction.SwingRelease) swing.UpdateRelease();
                else if (Action == PlayerAction.Swinging) swing.UpdateSwinging();
                else swing.UpdateSurfaceTransition(completed ?? ushort.MaxValue);
                ApplyCombatState();
                break;
            case PlayerAction.JumpingSmash:
                CaptureCombatState(ticks);
                (Smash ?? throw new InvalidOperationException("Jumping smash requires its genuine native action and trail owner.")).Update();
                ApplyCombatState();
                break;
            case PlayerAction.WebWindup:
                CaptureCombatState(ticks);
                (Web ?? throw new InvalidOperationException("Web windup requires the native web action owner.")).UpdateWindup();
                ApplyCombatState();
                break;
            case PlayerAction.WebFiring:
                CaptureCombatState(ticks);
                (Web ?? throw new InvalidOperationException("Web firing requires the native web action owner.")).UpdateFiring();
                ApplyCombatState();
                break;
            case PlayerAction.WebAttach:
                CaptureCombatState(ticks);
                (Web ?? throw new InvalidOperationException("Web release requires the native web action owner.")).UpdateAttach();
                ApplyCombatState();
                break;
            case PlayerAction.WebThrow:
                CaptureCombatState(ticks);
                (Web ?? throw new InvalidOperationException("Web throw requires the native web action owner.")).UpdateThrow();
                ApplyCombatState();
                break;
            case PlayerAction.WebDome:
                CaptureCombatState(ticks);
                (Web ?? throw new InvalidOperationException("Web dome requires the native dome action owner.")).UpdateDome(completed ?? ushort.MaxValue);
                ApplyCombatState();
                break;
            case PlayerAction.Pickup:
            case PlayerAction.Throw:
            case PlayerAction.FlickSwitch:
                CaptureCombatState(ticks);
                (Melee ?? throw new InvalidOperationException("Player interactions require a native melee owner.")).UpdateInteraction(completed ?? ushort.MaxValue);
                ApplyCombatState();
                break;
            case PlayerAction.ZipDive:
            case PlayerAction.ZipDivePunch:
            case PlayerAction.ZipDiveKick:
                CaptureCombatState(ticks);
                (Melee ?? throw new InvalidOperationException("Player ZipDive requires its native target/action owner.")).UpdateZipDive(completed ?? ushort.MaxValue);
                ApplyCombatState();
                break;
            case PlayerAction.PunchCombo:
                CaptureCombatState(ticks);
                (Melee ?? throw new InvalidOperationException("PunchCombo requires a native melee owner.")).UpdatePunchCombo();
                ApplyCombatState();
                break;
            case PlayerAction.KnockdownRecovery:
                CaptureCombatState(ticks);
                PlayerHitRecoveryRules.Step(CombatState ?? throw new InvalidOperationException("Hit recovery requires a combat state."),
                    combatHost!, completed ?? ushort.MaxValue);
                ApplyCombatState();
                break;
            case PlayerAction.Dead:
                CaptureCombatState(ticks);
                PlayerDeathProgressRules.Step(CombatState ?? throw new InvalidOperationException("Death dispatch requires a combat state."),
                    deathProgressHost!, animation.Playback.IsFinished);
                ApplyCombatState();
                break;
            case PlayerAction.Idle:
                if (!CheckGroundGone() && !TryWebShot() && !TryStraightZip() && !TryMeleeKick() && !TryJump()) StartWalking(wanted);
                break;
            case PlayerAction.GroundMovement:
                if (Walking is not null)
                {
                    CaptureCombatState(ticks);
                    Walking.UpdateWalking(completed ?? ushort.MaxValue);
                    ApplyCombatState();
                }
                else if (!CheckGroundGone() && !TryWebShot() && !TryJump() && !TryMeleeKick() && !TryWebShot() && !TryStraightZip()) UpdateWalking(wanted, completed);
                break;
            case PlayerAction.SurfaceLanding:
            case PlayerAction.ScriptedStandCompletion:
                CaptureCombatState(ticks);
                IPlayerWalkingFrameHost walking = Walking ?? throw new InvalidOperationException("Surface recovery requires the native walking owner.");
                if (Action == PlayerAction.SurfaceLanding) walking.UpdateInterior(completed ?? ushort.MaxValue);
                else walking.UpdateWallCompletion();
                ApplyCombatState();
                break;
            case PlayerAction.Rolling:
                if (!CheckGroundGone()) UpdateRolling(completed);
                break;
            case PlayerAction.Falling:
                if (AirborneMovement is not null)
                {
                    CaptureCombatState(ticks);
                    AirborneMovement.UpdateFalling(previousVerticalVelocity);
                    ApplyCombatState();
                }
                else UpdateFalling(previousVerticalVelocity);
                break;
            case PlayerAction.WallAttached:
                if (LandingRecovery is not null)
                {
                    CaptureCombatState(ticks);
                    LandingRecovery.UpdateRecovery(completed ?? ushort.MaxValue);
                    ApplyCombatState();
                }
                else if (!CheckGroundGone() && !TryJump() && !TryMeleeKick() && !TryStraightZip() && completed is not null) Stand();
                break;
            case PlayerAction.JumpWindup:
                if (LandingRecovery is not null)
                {
                    CaptureCombatState(ticks);
                    LandingRecovery.UpdateWindup();
                    ApplyCombatState();
                }
                else UpdateJumpWindup();
                break;
            case PlayerAction.Jumping:
                if (AirborneMovement is not null)
                {
                    CaptureCombatState(ticks);
                    AirborneMovement.UpdateJumping();
                    ApplyCombatState();
                }
                else if (!TryCeilingAttachment() && !TryStraightZip())
                {
                    if (!buttons.IsHeld(PlayerButton.Jump)) jump = jump with { Hold = 0 };
                    if (jump.Phase <= 0 && jump.Hold <= 0) Action = PlayerAction.Falling;
                }
                break;
            case PlayerAction.ZipWebPull:
                UpdateZipWeb();
                break;
            case PlayerAction.ScriptedDelay:
                break;
            default:
                throw new InvalidOperationException($"Terrain controller cannot dispatch action {Action}.");
        }
        bool sourceTorsoApplied = false;
        if (CameraFacing is not null)
        {
            CaptureCombatState(ticks);
            cameraHeading = CameraFacing.UpdateCameraFacing();
            if (CameraFacing is IPlayerTorsoAimFinalizerFrameHost torso)
            {
                torso.UpdateTorsoAim(input.Heading, cameraHeading);
                sourceTorsoApplied = true;
            }
            ApplyCombatState();
            // Original updateTorsoAim reads field23A after camera-facing's immediate
            // writes, then combines that heading with E32 for the final turn target.
            wanted = unchecked((short)((input.Heading + (OnWall ? 0 : cameraHeading)) & 4095));
        }
        FinalizeMovement(wanted, cameraHeading, ticks, sourceTorsoApplied);
        if (SupportBody is { } support)
            Position = PlayerSupportRules.Ride(Position, support.Velocity, AirborneTicks != 0, world);
        PlayerJumpImpulseStep impulse = PlayerJumpImpulseRules.Advance(jump, Velocity.Y, CollisionFlags, (uint)Action, ticks);
        jump = impulse.Impulse;
        Velocity = Velocity with { Y = impulse.VerticalVelocity };
        if (Smash is not null)
        {
            CaptureCombatState(ticks);
            Smash.ResolveContacts(previousFramePosition);
            ApplyCombatState();
        }
        if (CombatState is not null) CaptureCombatState(ticks);
        PowerUps?.CollectPowerUps();
        TriggerZones?.FinishTriggerZoneSweep(previousFramePosition);
        Melee?.FinishFrame();
        Lookaround?.FinishFrame(ticks);
        if (Lookaround is not null && CombatState is not null) ApplyCombatState();
        if (AfterPlayerMovement is not null)
        {
            AfterPlayerMovement(ticks);
            if (CombatState is not null) ApplyCombatState();
        }
        Melee?.FinishInput();
    }

    /// <summary>Actual CPlayer::AI's post-field554 user-function boundary, after the native source final pose/contact presentation.</summary>
    public void CompletePlayerAi() => AfterPlayerAi?.Invoke();

    private bool TryMeleeKick()
    {
        if(Melee is null)return false;
        CaptureCombatState(CombatState?.ElapsedTicks ?? 2);
        bool selected=Melee.CheckKick();
        if(selected)ApplyCombatState();
        return selected;
    }
    private bool TryWebShot()
    {
        if (Web is null) return false;
        CaptureCombatState(CombatState?.ElapsedTicks ?? 2);
        bool selected = Web.CheckWebShot();
        if (selected) ApplyCombatState();
        return selected;
    }
    public bool MeleeCheckGroundGone()
    {bool result=CheckGroundGone();CaptureCombatState(CombatState?.ElapsedTicks ?? 2);return result;}
    public bool MeleeCheckJump()
    {bool result=TryJump();CaptureCombatState(CombatState?.ElapsedTicks ?? 2);return result;}
    public void SetZipDiveAirControlSuppression(byte enabled) => suppressAirControl = enabled != 0;
    public void ZipDiveCheckForwards(short cameraHeading, byte surfaceTransition)
    {
        if (CombatState is not { } state) throw new InvalidOperationException("ZipDive heading requires shared source combat state.");
        PlayerZipDiveForwardResult result = PlayerZipDiveForwardTurn.Evaluate(new(surfaceTransition, input.Axes.Forward, input.Axes.Sideways,
            animation.Playback.ClipId, unchecked((ushort)Orientation.GetEffectiveHeading(OnWall)), input.Heading, cameraHeading, state.Field8E8 != 0,
            turn.Target, turn.RemainingTicks, ZipDiveHeadingRetargetFlag));
        if (result.Retarget) Target(result.Wanted, fast: true);
        ZipDiveHeadingRetargetFlag = result.RetargetFlag;
        state.HeadingTurnTicksRemaining = turn.RemainingTicks;
    }
    public void OrientFromMelee(int delta,bool useTarget,FixedVector3 target)
    {headingDelta=delta;OrientFromCombat(useTarget,target);headingDelta=0;}
    public void LockMeleeTorsoAngle()
    {turn=PlayerTorsoTargeting.Lock(turn);if(CombatState is { } state)state.HeadingTurnTicksRemaining=turn.RemainingTicks;}
    /// <summary>Actual distance-attack cleanup of source548 heading delta and DF8 remaining turn ticks, retaining target and step.</summary>
    public void ClearSourceHeadingTurn()
    {
        headingDelta = 0;
        turn = turn with { RemainingTicks = 0 };
        if (CombatState is { } state) state.HeadingTurnTicksRemaining = 0;
    }
    public void TargetMeleeTorsoAngle(FixedVector3 target,OpenSpidey.Assets.Animation.FixedJointTransform targetingRotation,int airborneTurnFactor)
    {
        turn=turn with{Heading=Orientation.GetEffectiveHeading(OnWall)};
        turn=PlayerTorsoTargeting.TargetPoint(turn,CombatState?.Position ?? Position,target,targetingRotation,OnCeiling,
            CombatState?.ActionState ?? (uint)Action,airborneTurnFactor,IsCrawling);
        if(CombatState is { } state)state.HeadingTurnTicksRemaining=turn.RemainingTicks;
    }
    public void TargetMeleeTorsoHeading(short wanted, int airborneTurnFactor, bool fast)
    {
        turn = turn with { Heading = Orientation.GetEffectiveHeading(OnWall) };
        turn = PlayerHeadingTurning.Target(turn, wanted, CombatState?.ActionState ?? (uint)Action,
            airborneTurnFactor, IsCrawling, fast);
        if (CombatState is { } state) state.HeadingTurnTicksRemaining = turn.RemainingTicks;
    }

    public void ConfigureCombat(PlayerCombatState state, IPlayerHitHost host, IPlayerDamageTimerHost timerHost,
        IPlayerDeathProgressHost progressHost)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(timerHost);
        ArgumentNullException.ThrowIfNull(progressHost);
        CombatState = state;
        InputSuppression.BindSourceState(state, host.ExitLookaroundMode);
        combatHost = host;
        combatTimerHost = timerHost;
        deathProgressHost = progressHost;
        CaptureCombatState(2);
    }

    public bool Hit(ActorHit hit)
    {
        if (CombatState is null || combatHost is null) throw new InvalidOperationException("Player damage requires the source combat host.");
        CaptureCombatState(CombatState.ElapsedTicks);
        bool accepted = PlayerHitRules.Apply(CombatState, combatHost, hit, random);
        ApplyCombatState();
        return accepted;
    }

    /// <summary>Copies live locomotion fields while retaining combat timers, health and effect ownership.</summary>
    public void CaptureCombatState(int elapsedTicks)
    {
        if (CombatState is not { } state) return;
        state.Position = Position;
        state.Velocity = Velocity;
        state.ActionState = (uint)Action;
        state.AnimationId = animation.Playback.ClipId;
        state.AnimationFrame = animation.Playback.Frame;
        state.ElapsedTicks = elapsedTicks;
        state.CollisionFlags = CollisionFlags;
        state.AirborneTicks = AirborneTicks;
        state.IsCrawling = IsCrawling;
        state.CrawlSurfaceNormal = normal;
        state.ForwardDirection = Orientation.Forward;
        state.UpDirection = Orientation.Up;
        state.Field8E9 = (byte)(normal.Y > 3400 ? 1 : 0);
        state.Field8E8 = (byte)(normal.Y is >= -2600 and <= 3400 ? 1 : 0);
        state.HeadingTurnTicksRemaining = turn.RemainingTicks;
    }

    public void ApplyCombatState()
    {
        if (CombatState is not { } state) throw new InvalidOperationException("Player has no combat state to apply.");
        Position = state.Position;
        Velocity = state.Velocity;
        Action = (PlayerAction)state.ActionState;
        IsCrawling = state.IsCrawling;
        normal = state.CrawlSurfaceNormal;
        turn = turn with { RemainingTicks = state.HeadingTurnTicksRemaining, Heading = Orientation.GetEffectiveHeading(OnWall) };
        if (Action != PlayerAction.ZipWebPull && ZipWebVisible)
        {
            ZipWebVisible = false;
            zipTarget = null;
        }
    }

    public void OrientFromCombat(bool useTarget, FixedVector3 target)
    {
        if (CombatState is not { } state) throw new InvalidOperationException("Player orientation requires a combat state.");
        normal = state.CrawlSurfaceNormal;
        IsCrawling = state.IsCrawling;
        if (Orientation.Orient(normal, headingDelta, useTarget ? target : null))
        {
            Stand();
            state.ActionState = (uint)Action;
            state.AnimationId = animation.Playback.ClipId;
            state.AnimationFrame = animation.Playback.Frame;
        }
        state.ForwardDirection = Orientation.Forward;
        state.UpDirection = Orientation.Up;
    }

    public bool CombatLineOfSight(FixedVector3 from, FixedVector3 to) => world.Trace(from, to) is null;

    private AxisDamping SelectDamping()
    {
        if (IsCrawling || Action == PlayerAction.ZipWebPull) return new(1, 1, 1);
        if (Action is PlayerAction.Falling or PlayerAction.Jumping) return new(4, 4, 4);
        return new(1, 4, 1);
    }

    private bool TryJump()
    {
        if (AirborneMovement is IPlayerAirborneEntryFrameHost entry)
        {
            CaptureCombatState(CombatState?.ElapsedTicks ?? 2);
            bool selected = entry.CheckJump();
            ApplyCombatState();
            return selected;
        }
        if (!buttons.WasTriggered(PlayerButton.Jump)) return false;
        if (!IsCrawling)
        {
            suppressAirControl = false;
            jump = jump with { Velocity = PlayerSupportRules.JumpVelocity(SupportBody?.Velocity.Y) };
            bool moving = Action == PlayerAction.GroundMovement;
            SuppressCameraRelativeAirHeading = moving;
            animation.Run(moving ? PlayerAnimationIds.RunningJumpWindup : PlayerAnimationIds.StandingJumpWindup, moving ? 5 : 4);
            Action = PlayerAction.JumpWindup;
            if ((CollisionFlags & 1) != 0) SpeedRamp = 0;
            return true;
        }
        FixedVector3 previousUp = Orientation.Up;
        FixedVector3 previousForward = Orientation.Forward;
        bool wall = OnWall;
        if (!wall) SuppressCameraRelativeAirHeading = false;
        suppressAirControl = wall;
        CollisionFlags &= unchecked((ushort)~2);
        IsCrawling = false;
        normal = new(0, -4096, 0);
        Action = PlayerAction.Falling;
        animation.Run(wall ? PlayerAnimationIds.WallRelease : PlayerAnimationIds.CeilingRelease);
        FixedVector3 target = previousForward;
        if (wall) target = input.Axes != default && input.Heading is >= 1536 and <= 2560 ? previousUp * -1 : previousUp;
        Orientation.Orient(normal, 0, target);
        return true;
    }

    private void UpdateJumpWindup()
    {
        ushort clip = animation.Playback.ClipId;
        if (!animation.Playback.IsFinished || clip is not (PlayerAnimationIds.StandingJumpWindup or PlayerAnimationIds.RunningJumpWindup)) return;
        int airborne;
        if (clip == PlayerAnimationIds.StandingJumpWindup) airborne = PlayerAnimationIds.StandingJump;
        else airborne = random(2) != 0 ? PlayerAnimationIds.RunningFlip : PlayerAnimationIds.RunningJump;
        animation.Run(airborne);
        jump = new(-245760, 0x70000, 0xA8000);
        Action = PlayerAction.Jumping;
    }

    private bool TryCeilingAttachment()
    {
        if (Velocity.Y > 0 || (CollisionFlags & 0x100) == 0 || !buttons.IsHeld(PlayerButton.Jump) ||
            LastTerrainStep.CeilingHit is not { } hit || hit.Contact.Normal.Y <= 3400 ||
            (hit.Contact.SurfaceFlags & 0x40000) != 0 || BodyHook is null) return false;
        normal = hit.Contact.Normal;
        orientationReference = Orientation.Forward;
        Orientation.Orient(normal, 0, orientationReference);
        Position = hit.Contact.Position + normal * SurfaceClearance;
        IsCrawling = true;
        jump = default;
        SuppressCameraRelativeAirHeading = false;
        animation.Run(animation.Playback.ClipId == PlayerAnimationIds.RunningFlipRecovery
            ? PlayerAnimationIds.FlipCeilingAttachment : PlayerAnimationIds.CeilingAttachment);
        Action = PlayerAction.Idle;
        return true;
    }

    private bool TryStraightZip()
    {
        if (!buttons.IsHeld(PlayerButton.Zip)) return false;
        PlayerWorldHit? target = PlayerZipWebRules.FindStraightTarget(new(Position, Orientation.Up, normal,
            (uint)Action, SurfaceClearance, Locked: zipLocked), world);
        if (target is null) return false;
        zipTarget = target;
        zipOrigin = Position;
        turn = turn with { RemainingTicks = 0 };
        if (((uint)Action & 1) != 0) animation.Run(IsCrawling ? PlayerAnimationIds.CrawlingZipShot : PlayerAnimationIds.StandingZipShot);
        else { animation.Run(PlayerAnimationIds.ZipPull, PlayerZipWebRules.PullStartFrame); ZipWebVisible = true; }
        Action = PlayerAction.ZipWebPull;
        return true;
    }

    /// <summary>Retains the actual lookaround-selected surface for the existing source zip pull and landing driver.</summary>
    public void BeginLookaroundZip(PlayerWorldHit target)
    {
        if (CombatState?.ActionState != (uint)PlayerAction.ZipWebPull)
            throw new InvalidOperationException("Lookaround zip requires the source target selector's zip action.");
        zipTarget = target;
        zipOrigin = CombatState.Position;
        ZipWebVisible = false;
    }

    private void UpdateZipWeb()
    {
        if (zipTarget is not { } target) throw new InvalidOperationException("Zip action has no target surface.");
        ushort clip = animation.Playback.ClipId;
        if (clip is PlayerAnimationIds.StandingZipShot or PlayerAnimationIds.CrawlingZipShot && animation.Playback.IsFinished)
        {
            animation.Run(PlayerAnimationIds.ZipPull);
            ZipWebVisible = true;
            return;
        }
        if (PlayerZipWebRules.IsPullWindow(animation.Playback))
        {
            if (clip == PlayerAnimationIds.ZipPull) { IsCrawling = false; ZipWebVisible = false; }
            if (!PlayerZipWebRules.HasArrived(Position, target))
            {
                if (buttons.WasTriggered(PlayerButton.Jump))
                {
                    Velocity = default;
                    normal = new(0, -4096, 0);
                    orientationReference = Orientation.Forward;
                    Orientation.Orient(normal, 0, orientationReference);
                    Action = PlayerAction.Falling;
                    zipLocked = true;
                    PendingWebReleaseAnimation = 1;
                }
                else Velocity = FixedAim.Velocity(PlayerZipWebRules.PullSpeed, FixedAim.Calculate(Position, target.Contact.Position));
                return;
            }
            PlayerZipLanding landing = PlayerZipWebRules.Land(zipOrigin, target, normal, Orientation.Forward, SurfaceClearance);
            Position = landing.Position;
            Velocity = default;
            normal = landing.Normal;
            if (landing.OrientationTarget is { } reference) orientationReference = reference;
            Orientation.Orient(normal, headingDelta, landing.OrientationTarget);
            IsCrawling = true;
            suppressAirControl = false;
            animation.Run(PlayerAnimationIds.ZipLanding);
            Position = PlayerZipWebRules.TidyLanding(Position, Orientation.Right, Orientation.Forward, 32, world);
            return;
        }
        if (clip != PlayerAnimationIds.ZipLanding) return;
        if (!OnWall && !OnCeiling) IsCrawling = false;
        if (TryJump()) return;
        if (!animation.Playback.IsFinished) return;
        if (!OnWall && !OnCeiling) animation.Run(PlayerAnimationIds.FloorRecovery);
        zipLocked = true;
        Stand();
    }

    private void UpdatePhysics(int ticks, int? previousSupportVelocity)
    {
        if (Swing?.StepPhysics() is { } swing)
        {
            Position = swing.Position;
            Velocity = swing.Velocity;
            CollisionFlags = swing.CollisionFlags;
            LastCrawlStep = null;
            LastTerrainStep = default;
            return;
        }
        if (IsCrawling)
        {
            var frame = new PlayerCrawlFrame(Position, Velocity, acceleration, damping, ticks,
                SurfaceClearance, normal, Orientation.Up, Orientation.Forward, Orientation.Right,
                OnWall, groundGrace, forwardClearance, cornerDistance);
            PlayerCrawlStep step = PlayerCrawlMotion.Step(frame, world,
                BodyHook ?? throw new InvalidOperationException("Surface movement requires the cached player body hook."));
            LastCrawlStep = step;
            Walking?.FinishCrawlPhysics(step);
            LastTerrainStep = default;
            Position = step.Movement.Position;
            Velocity = step.Movement.Velocity;
            CollisionFlags = step.CollisionFlags;
            groundGrace = step.GroundContactGraceUpdates;
            if (step.CeilingControlsChanged && step.SurfaceNormal.Y > 3400) orientationReference = Orientation.Forward;
            normal = step.SurfaceNormal;
            forwardClearance = step.ForwardClearance;
            cornerDistance = step.CornerDistance;
            HardSurface = step.HardSurface;
            if (step.NotifySupport && step.GroundHit is { } crawlSupport) AcceptSupport(crawlSupport);
            if (step.UseAlternateBrightness is bool darkSurface) SurfaceBrightness?.AdjustBrightness(darkSurface, ticks);
            return;
        }
        LastCrawlStep = null;
        if (Action == PlayerAction.ZipWebPull && PlayerZipWebRules.IsPullWindow(animation.Playback))
        {
            PlayerMovementStep movement = PlayerMotion.IntegrateCollisionFree(Position, Velocity, acceleration, damping, ticks);
            Position = movement.Position;
            Velocity = movement.Velocity;
            CollisionFlags = 0;
            LastTerrainStep = default;
            return;
        }
        LastTerrainStep = PlayerTerrainMotion.Step(new(Position, Velocity, acceleration, damping,
            ticks, SurfaceClearance, ProbeReach, false, previousSupportVelocity), world, policy);
        Walking?.FinishTerrainPhysics(LastTerrainStep);
        Position = LastTerrainStep.Movement.Position;
        Velocity = LastTerrainStep.Movement.Velocity;
        CollisionFlags = LastTerrainStep.CollisionFlags;
        if (LastTerrainStep.Fall?.Ground is { } ground)
        {
            normal = ground.SurfaceNormal;
            groundGrace = ground.GroundContactGraceUpdates;
            if (ground.NotifySupport && LastTerrainStep.Fall?.Hit is { } floorSupport) AcceptSupport(floorSupport);
            SurfaceBrightness?.AdjustBrightness(ground.UseAlternateBrightness, ticks);
        }
    }

    private PlayerSupportBody? RefreshSupport()
    {
        if (SupportBody is not { } previous || world is not IPlayerSupportWorld supports) return null;
        return supports.FindSupport(previous.Id) is { Dead: false } live ? live : null;
    }

    private void AcceptSupport(PlayerWorldHit hit)
    {
        if (hit.SupportId is not { } id || world is not IPlayerSupportWorld supports ||
            supports.FindSupport(id) is not { Dead: false } body) return;
        supports.NotifyTrodden(id);
        SupportBody = body;
    }

    public void AttachToSurface(PlayerWallAttachment attachment)
    {
        if (BodyHook is null) throw new InvalidOperationException("Attachment requires the cached player body hook.");
        groundInitialized = true;
        IsCrawling = true;
        normal = attachment.Normal;
        orientationReference = Orientation.Forward;
        Orientation.Orient(normal, 0, OnCeiling ? orientationReference : new(0, 4096, 0));
        Position = attachment.Position;
        Velocity = default;
        turn = turn with { RemainingTicks = 0, Heading = Orientation.GetEffectiveHeading(OnWall) };
        animation.Run(attachment.AnimationId);
        Action = PlayerAction.Idle;
    }

    public void SetScriptPosition(FixedVector3 position) => Position = position;
    public void SetIgnoreInputTimer(int duration) => InputSuppression.Set(duration, animation.Speed, CombatState?.LookaroundActive == true);
    public void SetScriptVelocity(FixedVector3 velocity)
    {
        Velocity = velocity;
        if (CombatState is { } state) state.Velocity = velocity;
    }
    public void EnterScriptedDelay() => Action = PlayerAction.ScriptedDelay;

    public void TeleportFromScript(FixedVector3 position)
    {
        SupportBody = null;
        Position = position;
        if (normal.Y > 3400)
        {
            FixedVector3 reference = Orientation.Forward;
            normal = new(0, -4096, 0);
            Orientation.Orient(normal, 0, reference);
        }
        else if (normal.Y >= -2600)
        {
            FixedVector3 reference = Orientation.Up;
            normal = new(0, -4096, 0);
            Orientation.Orient(normal, 0, reference);
        }
        else Orientation.Orient(normal, 0);
        IsCrawling = false;
        Velocity = default;
        ZipWebVisible = false;
        zipTarget = null;
        groundInitialized = true;
        if (world.Trace(position, position + new FixedVector3(0, 256 << 12, 0)) is { } ground)
        {
            Position = Position with { Y = unchecked(ground.Contact.Position.Y - (SurfaceClearance << 12)) };
            Action = PlayerAction.Idle;
        }
        else Action = PlayerAction.Falling;
    }

    private void InitializeGroundContact()
    {
        if (groundInitialized) return;
        groundInitialized = true;
        if (world.Trace(Position, Position + new FixedVector3(0, 0x800 << 12, 0)) is not { } ground) return;
        Position = Position with { Y = unchecked(ground.Contact.Position.Y - (SurfaceClearance << 12)) };
        normal = new(0, -4096, 0);
        Action = PlayerAction.Idle;
        if (Orientation.Orient(normal, 0)) Stand();
    }

    private bool CheckGroundGone()
    {
        if (AirborneMovement is IPlayerAirborneEntryFrameHost entry)
        {
            CaptureCombatState(CombatState?.ElapsedTicks ?? 2);
            bool selected = entry.CheckGroundGone();
            ApplyCombatState();
            return selected;
        }
        if (IsGrounded) return false;
        if (groundGrace != 0) groundGrace--;
        if (groundGrace != 0) return false;
        animation.Run(212);
        if (IsCrawling)
        {
            IsCrawling = false;
            normal = new(0, -4096, 0);
            Orientation.Orient(normal, 0, new(0, 0, 4096));
        }
        Action = PlayerAction.Falling;
        return true;
    }

    private void StartWalking(short wanted)
    {
        if (LandingRecovery is not null)
        {
            CaptureCombatState(CombatState?.ElapsedTicks ?? 2);
            LandingRecovery.CheckForwards(true);
            ApplyCombatState();
            return;
        }
        if (CombatState?.LookaroundActive == true || (buttons.IsHeld(PlayerButton.Target) && ((uint)Action & 1) != 0)) return;
        if (input.Axes == default || animation.Playback.ClipId is 234 or 227 or 281) return;
        int delta = (wanted - turn.Heading) & 4095;
        if (delta is > 1536 and < 2560 && animation.Playback.ClipId is 0 or 11 or 12 or 13)
        {
            animation.Run(31);
            turn = turn with { RemainingTicks = 0 };
            Action = PlayerAction.Rolling;
            return;
        }
        if (delta is >= 128 and <= 3968)
        {
            int difference = (turn.Target - wanted) & 4095;
            if (turn.RemainingTicks == 0 || difference is >= 64 and <= 4032)
                Target(wanted, fast: true);
            return;
        }
        Action = PlayerAction.GroundMovement;
        animation.Run(PlayerLocomotionAnimations.Start(IsCrawling, HardSurface));
    }

    private void UpdateWalking(short wanted, ushort? completed)
    {
        if (CombatState?.LookaroundActive == true) { Stand(); return; }
        if (input.Axes == default) { Stand(); return; }
        if (completed == 52 && !HardSurface) animation.Run(50);
        else if (completed is 51 or 85 && HardSurface) animation.Run(52);
        int delta = (wanted - turn.Heading) & 4095;
        if (delta is <= 1536 or >= 2560 || PlayerLocomotionAnimations.Reverse(animation.Playback.ClipId) is not { } reverse) return;
        animation.Run(reverse);
        Velocity = default;
        turn = turn with { RemainingTicks = 0 };
        Action = PlayerAction.Rolling;
    }

    private void UpdateRolling(ushort? completed)
    {
        animation.Speed = 0x20000;
        if (completed is not null)
        {
            int frame = animation.Playback.Frame;
            if (!TryJump()) return;
            int rotation = (int)((uint)(frame << 11) / (uint)(animation.Playback.FrameCount - 1));
            Orientation.Orient(normal, rotation);
            headingDelta = 0;
            turn = turn with { Heading = Orientation.GetEffectiveHeading(OnWall) };
            return;
        }
        if (Orientation.Orient(normal, 2048)) Stand();
        headingDelta = 0;
        turn = turn with { Heading = Orientation.GetEffectiveHeading(OnWall) };
        if (TryJump()) return;
        if (input.Axes == default) Stand();
        else Action = PlayerAction.GroundMovement;
    }

    private void UpdateFalling(int previousVerticalVelocity)
    {
        if (CombatState is { Health: <= 0 } dead)
        {
            CaptureCombatState(dead.ElapsedTicks);
            dead.FieldEA6 = 0;
            if (IsGrounded)
            {
                // Source updateFalling only checks landing while dead. The default CheckLanded
                // sound and B0/AF landing clip run before SwitchToDeathMode(false) selects B6.
                combatHost!.PlayPositionSound(9, Position, 0);
                if (combatTimerHost!.VibrationEnabled) combatTimerHost.ActuatorOn(0, 4, 0, 1);
                ushort landing = animation.Playback.ClipId is 0xAF or 0xB0 ? (ushort)0xB2 : (ushort)0xD5;
                combatHost.ResetDeathAnimationSounds(landing);
                combatHost.RunAnimation(landing, 0, -1);
                dead.AnimationId = landing;
                dead.AnimationFrame = 0;
                dead.ActionState = 8;
                dead.FieldAE5 = 0;
                PendingWebReleaseAnimation = 0;
                PlayerDeathRules.Enter(dead, combatHost);
                ApplyCombatState();
            }
            return;
        }
        if (IsGrounded)
        {
            animation.Run(213);
            Action = PlayerAction.WallAttached;
            return;
        }
        if (BodyHook is not null && PlayerWallAttachmentRules.Resolve(new(Position, (uint)Action,
            CollisionFlags, LastTerrainStep.Sweep.BodyHit, SurfaceClearance, animation.Playback.ClipId), world) is { } attachment)
        {
            AttachToSurface(attachment);
            return;
        }
        if (TryCeilingAttachment() || TryStraightZip()) return;
        PlayerFallAnimationPlan fall = PlayerFallAnimations.Resolve(animation.Playback.ClipId,
            previousVerticalVelocity, Velocity.Y, PendingWebReleaseAnimation != 0);
        if (fall.Next is { } next) animation.Run(next);
        if (fall.ClearWebRelease) PendingWebReleaseAnimation = 0;
        if (fall.ResetReference)
        {
            fallReferenceTick = sourceTicks;
            if (CombatState is { } state) state.FallReferenceHeight = Position.Y;
        }
        if (suppressAirControl && unchecked(sourceTicks - fallReferenceTick) > 30) suppressAirControl = false;
    }

    public void Stand()
    {
        PlayerAnimationPlan plan = PlayerLocomotionAnimations.Stand(animation.Playback, IsCrawling, Melee?.HeldObjectFlags);
        if (plan.Restart) animation.Run(plan.Clip, plan.From, plan.To);
        Action = PlayerAction.Idle;
    }

    private void Target(short wanted, bool fast = false) =>
        turn = PlayerHeadingTurning.Target(turn, wanted, (uint)Action, 1, IsCrawling, fast);

    private void FinalizeMovement(short wanted, short cameraHeading, int ticks, bool sourceTorsoApplied)
    {
        if (!sourceTorsoApplied)
        {
            if (Action == PlayerAction.GroundMovement) Target(wanted);
            else if (Action == PlayerAction.Idle && input.Axes == default) turn = turn with { RemainingTicks = 0 };
            else if (Action is PlayerAction.Falling or PlayerAction.Jumping or PlayerAction.WallAttached)
            {
                if (input.Axes == default) turn = turn with { RemainingTicks = 0 };
                else Target(wanted);
            }
        }
        short previousHeading = turn.Heading;
        turn = PlayerHeadingTurning.Advance(turn, ticks);
        headingDelta = turn.Heading - previousHeading;
        if (OnCeiling && previousHeading != turn.Heading)
        {
            (short sine, short cosine) = GameAngle.Heading((previousHeading - turn.Heading) & 4095);
            orientationReference = FixedRotation.Transform(Orientation.Basis, new(sine, 0, cosine));
        }
        acceleration = IsCrawling ? default : new(0, 0xA000, 0);
        var frame = new PlayerFlatMovementFrame(Velocity, input.Axes,
            turn.Heading, input.Heading, cameraHeading, (uint)Action, SpeedRamp,
            animation.Playback.ClipId, animation.Speed, Crawling: IsCrawling, SuppressMovement: suppressAirControl,
            SuppressAirHeading: SuppressCameraRelativeAirHeading, Lookaround: CombatState?.LookaroundActive == true,
            MovementEnabled: CombatState is null || CombatState.FieldAE4 != 0);
        PlayerFlatMovementResult movement = OnWall ? PlayerWallMovement.Resolve(frame, Orientation.Basis) : PlayerFlatMovement.Resolve(frame);
        Velocity = movement.Velocity;
        animation.Speed = movement.AnimationSpeed;
    }
}
