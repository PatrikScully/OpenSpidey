using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Rhino;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Cameras;
using OpenSpidey.Core.Audio;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.Player;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Original source307 Rhino CBaddy: construction, CRhino::AI (0x4819C0) and module lifetime.</summary>
public partial class GodotRhinoActor : GodotScriptActor, IRhinoHost, IGodotSourceBaddyBody, IActorScriptHost, IActorHeightHost,
    IActorPathHost, IActorAttackMonitorHost
{
    private GodotActorRegistry registry = null!;
    private GodotRhinoConfiguration configuration = null!;
    private RhinoBrain brain = null!;
    private readonly ActorBodyMotion playerBody = new();
    private FixedJointTransform[] hookPose = [];
    private bool begun;
    private bool deathPulseSent;
    private bool destroyed;
    public RhinoState State { get; private set; } = null!;
    public RhinoStateFlags StateFlags { get; } = new();
    public ActorProcedureState Actor { get; private set; } = null!;
    public ActorProcedureList Procedures { get; } = new();
    public ActorBodyMotion Body => Actor.Body;
    public ActorHeightController Height { get; private set; } = null!;
    public ActorWaypointRoute Route { get; private set; } = null!;
    public ActorPathController Path { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public GodotSourceVertexLighting Lighting { get; private set; } = null!;
    public GodotActorWorldQuery World { get; private set; } = null!;
    public ThugMessageQueue Messages { get; } = new();
    public ActorSpeechAttachment SpeechAttachment { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public uint PlayerDistance { get; private set; }
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public int AnimationId => Model.ClipId;
    public int AnimationFrame => Model.Playback.Frame;
    public bool AnimationFinished => Model.Playback.Finished != 0;
    public int ElapsedTicks => FrameTiming.ElapsedTicks;
    public int AttackClock => configuration.AttackClock();
    public FixedVector3 PlayerPosition => registry.PlayerPosition ?? default;
    public int PlayerInputSuppression => configuration.PlayerInputSuppression();
    public bool HasMessages => Messages.Head is not null;
    public bool PlayerIsCrawling => registry.Player is { } player && player.Controller.IsCrawling;
    public int PlayerActionState => unchecked((int)RequirePlayer().Controller.Action);
    public bool PlayerOnWallOrCeiling => RequirePlayer().Controller is { } controller && (controller.OnWall || controller.OnCeiling);
    public object? PlayerHeldObject => configuration.PlayerBinding().Combat.Session.HeldActor;
    public int Difficulty => registry.Difficulty;
    public int WebDomeCount => configuration.WebDomeCount();
    public int DomeCount => WebDomeCount;
    public uint RouteFlags => Route.Flags;
    public ushort AttackDamage => Variables.Damage;
    public short PlayerHealth => registry.Player?.Combat.State.Health ?? 0;
    public ushort MinorRadius => State.MinorRadius;
    public ushort BodyFlags => unchecked((ushort)State.BodyFlags);
    public uint BehaviorFlags => State.BehaviorFlags;
    public short Health => State.Health;

    public static GodotRhinoActor Create(TriggerNode node, GodotActorRegistry registry, GodotRhinoConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != RhinoState.Type) throw new NotSupportedException("Rhino factory requires source307.");
        int ordinal = registry.BaddyConstruction.RequireCurrentOrdinal();
        var actor = new GodotRhinoActor { Name = $"Rhino{node.Id}", registry = registry, configuration = configuration, Definition = definition };
        try
        {
            actor.State = new(node.Id, configuration.AttackClock(), ordinal);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= ~2u;
            actor.Actor = new(new ActorBodyMotion
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z),
                LinearDamping = new(1, 1, 1), AngularDamping = new(1, 1, 1)
            });
            actor.ScriptState = new() { ActorType = RhinoState.Type, NodeId = node.Id };
            actor.Variables = new(actor.ScriptState, registry)
            {
                Position = actor.Body.Position, GroundOffset = actor.State.SurfaceClearance,
                CollisionRadius = actor.State.MinorRadius, ItemFlags = 2, AnimationSpeed = 0x10000
            };
            actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            actor.Model = configuration.ModelFactory();
            actor.Model.AdvanceAutomatically = false; actor.AddChild(actor.Model);
            actor.Lighting = new(actor.Model, configuration.Light(), configuration.LightingState);
            actor.World = new(actor, registry.Triggers);
            actor.Height = new(actor.Actor, actor) { Clearance = actor.State.SurfaceClearance, QueryInterval = unchecked((byte)actor.State.Field216) };
            actor.Route = new(registry.Triggers, node.Id, actor.ParseWaypoint);
            actor.Path = new(actor.Actor, actor);
            actor.Runtime = new(new ActorScriptCursor(definition.BehaviorData,
                checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor);
            actor.brain = new(actor.State, actor.StateFlags, actor);
            actor.AddChild(new GodotNativeDestructionObserver { Name = "RhinoDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            registry.ActorDistanceMeasured += actor.OnDistanceMeasured;
            RhinoBrain.Construct(actor.State, actor, () => { }, configuration.Random);
            configuration.CreateHealthBar(actor.State);
            actor.ItemFlags |= 0x480;
            actor.BuildSourceTransform();
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    internal override void Begin()
    {
        Runtime.Start(registry.Environment);
        begun = true;
        BuildSourceTransform();
    }

    internal override void Advance()
    {
        SourceUpdates++;
        State.BodyFlags = FrameTiming.Prepare(configuration.Clock(), unchecked((ushort)State.BodyFlags), ItemFlags,
            new(Model.Playback.Frame, Model.Playback.ClipId, Model.Playback.Direction));
        if ((ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        brain.Advance();
        Variables.Position = Body.Position;
        Variables.CollisionRadius = State.MinorRadius;
        Variables.GroundOffset = State.SurfaceClearance;
        Variables.Health = State.Health;
        ScriptState.BehaviorFlags = Actor.BehaviorFlags;
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }

    public override void _Process(double delta)
    {
        if (!begun || destroyed || registry.Player is null) return;
        ItemFlags = configuration.IsSourceCulled(this) ? (ushort)(ItemFlags | 0x8000) : (ushort)(ItemFlags & 0x7FFF);
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }

    public void SynchronizeSourceLighting() => Lighting.Synchronize(SourceTransformBasis, ItemFlags);
    public void BuildTransform() => BuildSourceTransform();
    private void BuildSourceTransform()
    {
        ActorAngles angles = Body.Angles;
        SourceTransformBasis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        Transform3D transform = PsxCoordinates.CharacterTransform(SourceTransformBasis);
        transform.Origin = PsxCoordinates.FixedWorldPosition((Body.Position >> 12) << 12);
        Transform = transform;
    }

    public bool TryAdvanceWaypoint() => Route.Advance();
    public int GroundHeight(FixedVector3 position, int above, int below, int flags)
    {
        if (flags != 0) throw new NotSupportedException("Rhino source ground queries use flags0.");
        return World.GroundHeight(position, above, below);
    }
    public int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support) =>
        World.GetGroundHeight(position, above, below, out support);
    public void SetHeight(bool initial, int above, int below) => Height.SetHeight(initial, above, below, ElapsedTicks);
    public void RunAnimation(int animation) => Model.RunClip(animation);
    public void RunAnimation(int animation, int frame) => Model.RunClip(animation, frame);
    public void CycleAnimation(int animation) => Model.CycleClip(animation);
    public void SetAnimationSpeed(int speed)
    {
        Variables.AnimationSpeed = speed;
        Model.PlaybackSpeed = speed;
    }
    public void Neutralize() => Actor.Neutralize(Procedures);
    /// <summary>CBaddy::DoPhysics; mode 1 spends at most the field_1F8 flight budget this update.</summary>
    public void DoPhysics(int mode)
    {
        int elapsed = ElapsedTicks;
        if (mode != 0)
        {
            elapsed = State.Field1F8 > ElapsedTicks ? ElapsedTicks : State.Field1F8;
            if (elapsed == 0) return;
            State.Field1F8 -= elapsed;
        }
        Actor.Body.UseLocalVelocity = (Actor.BehaviorFlags & 0x80000000) != 0 || (Actor.BehaviorFlags & 1) != 0;
        Body.Integrate(elapsed);
    }
    public void SetGlobalBoss(bool enabled) =>
        configuration.SetGlobalBoss(enabled ? new CameraFocusTarget(this, () => SourcePosition) : null);
    public void SetBossCameraMode() => configuration.SetBossCameraMode();
    public int Random(int bound) => configuration.Random(bound);
    public ActorBodyMotion? RecoverPlayerBody()
    {
        if (registry.Player is null) return null;
        playerBody.Position = PlayerPosition;
        return playerBody;
    }
    public void AttachPlayerLookAt(uint completionMask, int rate, int tolerance) =>
        Procedures.Attach(ActorLookAtProcedure.ForBody(Actor, RecoverPlayerBody, completionMask, rate, tolerance));
    // SFX_PlayPos stays unwired for the Rhino, as for CSimby: no voice handle is ever allocated, so field_338 stays 0.
    public void PlayPositionalSound(int id) { }
    public int PlayTrackedSound(int id) => 0;
    public void StopSound(int handle) { }
    public RhinoWallChunkLists WallChunks => configuration.WallChunks;
    public bool TryGetChargeWallContact(FixedVector3 start, FixedVector3 direction, out RhinoChargeWallContact contact)
    {
        FixedVector3 end = Body.Position + direction * 128;
        GodotActorWorldHit? hit = World.Trace(start, end);
        if (hit is null)
        {
            contact = default;
            return false;
        }
        contact = Contact(hit);
        return true;
    }
    public bool TryLineToItem(FixedVector3 start, FixedVector3 end, out uint checksum, out FixedVector3 hitPosition)
    {
        GodotActorWorldHit? hit = World.Trace(start, end);
        checksum = hit is null ? 0 : Contact(hit).ModelChecksum;
        hitPosition = hit?.Position ?? default;
        return hit is not null;
    }
    private static RhinoChargeWallContact Contact(GodotActorWorldHit hit)
    {
        uint checksum = hit.ModelChecksum ?? 0;
        if (hit.SupportId is { } id && GodotObject.InstanceFromId(id) is GodotManipulationActor item)
            return new(checksum, ManipulationState.Type, item.State.ItemFlags, hit.Position, hit.Triangle.FixedNormal, item);
        uint itemFlags = hit.Collider is ImportedCollisionBody imported ? imported.SourceFlags : 0u;
        return new(checksum, 0, itemFlags, hit.Position, hit.Triangle.FixedNormal, hit.Collider);
    }
    public void ChunkItemByChecksum(uint checksum) =>
        (registry.EnvironmentChunks ?? throw new NotSupportedException(
            "Chunk_ChunkItemByChecksum requires the scene environment-chunk host.")).ChunkEnvironment(checksum);
    public bool ChargeLineHitsPlayer(FixedVector3 start, FixedVector3 end, int radius)
    {
        if (registry.Player is null) return false;
        var body = new ActorLaserBody(registry.Player, PlayerPosition, registry.Player.ActorState.MinorRadius,
            registry.Player.ActorState.BodyFlags);
        return ActorLaserCollision.CheckBodies(start, end, [body], radiusScale: radius) is not null;
    }
    public void HitPlayerFromCharge(FixedVector3 delta) => HitPlayer(new ActorHit(0xE, 0, 0xC, 0x1E, delta));
    public bool HitPlayer(ActorHit hit) => RequirePlayer().Combat.Hit(hit);
    void IActorAttackMonitorHost.HitPlayer(ActorHit hit) => HitPlayer(hit);
    public void KnockPlayerFromCrawl() => RequirePlayer().Combat.KnockFromCrawlPosition();
    public void SetPlayerIgnoreInputTimer(int ticks) => RequirePlayer().Controller.SetIgnoreInputTimer(ticks);
    /// <summary>CBaddy::Baddy_SendSignal (0x403230): Trig_SendSignal (0x4DFFE0) flags linked bodies, not a mission pulse.</summary>
    public void SendSignal() => configuration.SendSignal(this);
    public IThugTrapWeb? RecoverTrapWeb() => configuration.PlayerBinding().Web.Contacts.RecoverContact(this, false);
    public bool TrapWebHandleSet => RecoverTrapWeb() is not null;
    // TrapWebEffect.Burst clears its contact binding, which is all that field_104 = 0 releases here.
    public void ReleaseTrapWebHandle() { }
    public ReadOnlySpan<RhinoSpeechEntry> SpeechEntries => configuration.SpeechTable.Span;
    public bool TryPlayPositionSpeech(FixedVector3 position, int bank, int slot, int priority) =>
        configuration.TryPlayPositionSpeech(bank, slot, position, priority);
    public bool PlayerCanTalkNow() =>
        registry.Player is { } player && (player.Combat.State.ActionState & 0x800080) == 0;
    public void AttachRhinoSpeech(int bank, int slot) =>
        SpeechAttachment.Attach(configuration.SourceClock(), bank, slot);
    public void AttachPlayerSpeech(int bank, int slot) =>
        registry.Player!.Combat.SpeechAttachment.Attach(configuration.SourceClock(), bank, slot);
    public void StopMyXA() => configuration.StopSpeech(SpeechAttachment);
    public void AttachAttackMonitor(int startFrame, uint hookMask, int frames, uint completionMask) =>
        Procedures.Attach(new ActorAttackMonitorProcedure(Actor, this, startFrame, hookMask, frames, completionMask));
    public FixedVector3 GetActorHookPosition(ActorHook hook)
    {
        if (hookPose.Length != Model.SourcePartCount) hookPose = new FixedJointTransform[Model.SourcePartCount];
        for (int part = 0; part < hookPose.Length; part++) hookPose[part] = Model.GetSourcePartPose(part);
        return ActorHookPosition.Evaluate(Body.Position, Body.Position >> 12, SourceTransformBasis, hookPose, hook);
    }
    public bool CollideWithPlayer(FixedVector3 from, FixedVector3 to, int collisionScale, out ActorHook hitHook) =>
        RequirePlayer().Combat.CollideWithPlayer(from, to, collisionScale, out hitHook);
    public void CreateCombatImpactEffect(FixedVector3 position, int flags) => configuration.CreateCombatImpact(position);
    public void PlayPositionSound(ushort sound, FixedVector3 position, int volume) { }
    // CRhino inherits the empty CBaddy::Victorious.
    public void Victorious() { }
    public void ShakeCamera() => configuration.PlayerBinding().Camera.Shake(Body.Position, 0);
    public bool VibrationEnabled => RequirePlayer().Combat.VibrationEnabled;
    public int ActuatorTime(int controller, int actuator) =>
        configuration.PlayerBinding().Combat.ControllerFeedback.RemainingTicks(controller, actuator);
    public void ActuatorOn(int controller, int duration, int actuator, int strength) =>
        configuration.PlayerBinding().Combat.ControllerFeedback.Activate(controller, unchecked((ushort)duration), actuator, strength);
    public IReadOnlyList<RhinoEnvironmentBody> EnvironmentalBodies => registry.SourceActors(ActorUpdateGroup.Environment)
        .Select(actor => new RhinoEnvironmentBody(actor, actor is GodotManipulationActor ? ManipulationState.Type : actor.ScriptState.ActorType,
            actor.SourcePosition)).ToArray();
    public void SmashEnvironmentalBody(object? identity)
    {
        if (identity is not GodotManipulationActor item)
            throw new NotSupportedException("CRhino only reaches the type401 CManipOb::Smash vtable slot.");
        item.Smash();
    }
    /// <summary>CBaddy::Die(0) (0x404320).</summary>
    public void Die()
    {
        if ((State.BodyFlags & 0x40) != 0) return;
        // 0x2A8 bit 0x4000 suppresses it; byte 0x211 sends it once; node 0xFFFF has no links.
        if ((Actor.BehaviorFlags & 0x4000) == 0 && !deathPulseSent && Definition.NodeId != ushort.MaxValue)
        {
            deathPulseSent = true;
            SendDeathPulse();
        }
        State.BodyFlags |= 0x40;
        ItemFlags |= 1;
        Lifecycle.Kill();
    }
    public bool CameraPresent => configuration.PlayerBinding().Camera is not null;
    public short CameraAngle => configuration.PlayerBinding().Camera.RhinoDeathViewHeading;
    public void SetCameraAngle(short heading, ushort duration) => configuration.PlayerBinding().Camera.SetCameraAngle(heading, duration);
    public void BeginDeathCamera() => configuration.PlayerBinding().Camera.BeginRhinoDeathView(this, () => SourcePosition);

    // Rhino particle bits (MGS shadow quad, dazed-star CFT4Bits, CRhinoNasalSteam, Effects_Electrify/RhinoStomp,
    // CRhinoWallImpact) are not rendered yet. The brain still runs their source state and Rnd draws.
    public void UpdateShadow() { }
    public void UpdateDazedStars(ReadOnlySpan<ActorHook> hooks, bool spawn) { }
    public void EmitNasalSteam(ActorHook position, ActorHook direction, int spin) { }
    public void Electrify() { }
    public void UnElectrify() { }
    public void EmitStompEffect() { }
    public void CreateWallImpact(RhinoChargeWallContact contact) { }

    public ActorPathCheckResult PathCheck(FixedVector3 from, FixedVector3 to, bool wantHitPoint, int clearance)
    {
        Path.Radius = State.MinorRadius;
        Path.Clearance = State.SurfaceClearance;
        Path.StoredBodyY = State.ShadowPosition.Y;
        return Path.PathCheck(from, to, wantHitPoint, clearance, default);
    }
    int IActorPathHost.CollisionCheckMode { get => World.CollisionCheckMode; set => World.CollisionCheckMode = value; }
    bool IActorPathHost.LineOfSight(FixedVector3 from, FixedVector3 to, int flags, ref FixedVector3 hit) =>
        World.LineOfSight(from, to, flags, ref hit);
    int IActorPathHost.GroundHeight(FixedVector3 position, int above, int below) => World.GroundHeight(position, above, below);
    FixedVector3 IActorPathHost.WaypointPosition(int node) => World.WaypointPosition(node);

    public bool Hit(ActorHit hit) => brain.Hit(hit) != 0;
    public void MarkSourceBodyDead() => State.BodyFlags |= 0x40;
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) =>
        Messages.Enqueue(new ThugMessage(code, sender) { Vector0 = vector });

    private void SyncScriptStorage()
    {
        Variables.Position = Body.Position; Variables.Health = State.Health;
        Variables.CollisionRadius = State.MinorRadius; Variables.GroundOffset = State.SurfaceClearance;
        ScriptState.BehaviorFlags = Actor.BehaviorFlags;
    }
    public short ReadVariable(ushort variable, ActorScriptCursor operands) { SyncScriptStorage(); return Variables.Read(variable, operands); }
    public void WriteVariable(ushort variable, ActorScriptCursor operands)
    {
        SyncScriptStorage(); Variables.Write(variable, operands);
        Body.Position = Variables.Position; State.Health = Variables.Health;
        State.MinorRadius = Variables.CollisionRadius; Model.PlaybackSpeed = Variables.AnimationSpeed;
    }
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
    {
        if (GodotLinkedBaddyCommands.TryExecute(registry, Definition.NodeId, opcode, operands)) return true;
        if (GodotActorLinkEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotActorDirectEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotEnvironmentChunkCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorScreenFlashCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorSparkCommands.TryExecute(registry, opcode, operands, SourcePosition, Body.Angles)) return true;
        if (GodotActorCameraShakeCommands.TryExecute(registry, opcode, operands, ReadVariable, SourcePosition)) return true;
        if (GodotActorVisualEffectCommands.TryExecute(registry, opcode, operands, SourcePosition)) return true;
        if (GodotActorSpeechCommands.TryExecute(registry, opcode, operands, ScriptState, environment, SourcePosition)) return true;
        if (opcode == 0x42B9 && GodotActorPlayerScriptCommands.TryExecute(registry, opcode, operands)) return true;
        if (Route.TryExecuteCommand(opcode, operands)) return true;
        throw new NotSupportedException($"Rhino{Definition.NodeId}: original script command0x{opcode:X4} is not connected.");
    }
    public ActorScriptCursor ScriptAtNode(ushort node) => registry.ScriptAtNode(node);
    public bool PlayerCeilingCheck(short x, short z) => registry.PlayerCeilingCheck(x, z);
    public void FlashCompass() => registry.FlashCompass();
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void Retire() { SendDeathPulse(); State.BodyFlags |= 0x40; ItemFlags |= 1; Lifecycle.Kill(); }
    private void ParseWaypoint(ActorScriptCursor script)
    {
        SyncScriptStorage();
        Runtime = new(script, this);
        Runtime.Start(registry.Environment);
    }

    private GodotPlayer RequirePlayer() => registry.Player ?? throw new InvalidOperationException("CRhino needs the live player (MechList).");
    private void OnDistanceMeasured(GodotScriptActor actor, uint distance) { if (ReferenceEquals(actor, this)) PlayerDistance = distance; }
    private void OnSourceRemoved(GodotScriptActor actor) { if (ReferenceEquals(actor, this)) DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if (destroyed) return;
        destroyed = true; registry.ActorRemoved -= OnSourceRemoved; registry.ActorDistanceMeasured -= OnDistanceMeasured;
        configuration.SetGlobalBoss(null);
        Messages.Cleanup(all: true);
    }
}
