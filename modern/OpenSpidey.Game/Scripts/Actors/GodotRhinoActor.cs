using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
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
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Original source307 Rhino CBaddy: construction, ChargePlayer leaves and module lifetime.</summary>
public partial class GodotRhinoActor : GodotScriptActor, IRhinoHost, IGodotSourceBaddyBody, IActorScriptHost, IActorHeightHost
{
    private GodotActorRegistry registry = null!;
    private GodotRhinoConfiguration configuration = null!;
    private RhinoBrain brain = null!;
    private readonly ActorBodyMotion playerBody = new();
    private bool begun;
    private bool destroyed;
    public RhinoState State { get; private set; } = null!;
    public RhinoStateFlags StateFlags { get; } = new();
    public ActorProcedureState Actor { get; private set; } = null!;
    public ActorProcedureList Procedures { get; } = new();
    public ActorBodyMotion Body => Actor.Body;
    public ActorHeightController Height { get; private set; } = null!;
    public ActorWaypointRoute Route { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public GodotSourceVertexLighting Lighting { get; private set; } = null!;
    public GodotActorWorldQuery World { get; private set; } = null!;
    public ThugMessageQueue Messages { get; } = new();
    public ActorSpeechAttachment SpeechAttachment { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public int AnimationId => Model.ClipId;
    public bool AnimationFinished => Model.Playback.Finished != 0;
    public int ElapsedTicks => FrameTiming.ElapsedTicks;
    public int AttackClock => configuration.AttackClock();
    public FixedVector3 PlayerPosition => registry.PlayerPosition ?? default;
    public int PlayerInputSuppression => configuration.PlayerInputSuppression();
    public bool HasMessages => Messages.Head is not null;
    public bool PlayerIsCrawling => registry.Player is { } player && player.Combat.State.IsCrawling;
    public int Difficulty => registry.Difficulty;
    public int WebDomeCount => configuration.WebDomeCount();
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
            actor.Runtime = new(new ActorScriptCursor(definition.BehaviorData,
                checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor);
            actor.brain = new(actor.State, actor.StateFlags, actor);
            actor.AddChild(new GodotNativeDestructionObserver { Name = "RhinoDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
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
    public void Neutralize() => Actor.Neutralize(Procedures);
    public void ProcessMessages() =>
        throw new NotSupportedException("CRhino::AI message dispatch (0x4819C0) is not ported yet.");
    public void PlaySounds() { }
    public void DoPhysics(int mode)
    {
        if (mode != 0) throw new NotSupportedException($"CRhino DoPhysics mode{mode} is not ported yet.");
        Actor.Body.UseLocalVelocity = (Actor.BehaviorFlags & 0x80000000) != 0 || (Actor.BehaviorFlags & 1) != 0;
        Body.Integrate(ElapsedTicks);
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
    public void PlayPositionalSound(int id) { }
    public bool LineOfSightToPlayer(int argument) =>
        throw new NotSupportedException($"CRhino::LineOfSightCheck (0x4823C0) argument{argument} is not ported yet.");
    public RhinoWallChunkLists WallChunks => configuration.WallChunks;
    public bool TryGetChargeWallContact(FixedVector3 start, FixedVector3 direction, out RhinoChargeWallContact contact)
    {
        FixedVector3 end = Body.Position + direction * 128;
        GodotActorWorldHit? hit = World.Trace(start, end);
        if (hit?.ModelChecksum is not { } checksum)
        {
            contact = default;
            return false;
        }
        uint itemFlags = hit.Collider is ImportedCollisionBody imported ? imported.SourceFlags : 0u;
        int actorType = 0;
        contact = new(checksum, actorType, itemFlags, hit.Position, hit.Triangle.FixedNormal, hit.Collider);
        return true;
    }
    public void ChunkItemByChecksum(uint checksum) =>
        (registry.EnvironmentChunks ?? throw new NotSupportedException(
            "Chunk_ChunkItemByChecksum requires the scene environment-chunk host.")).ChunkEnvironment(checksum);
    public void RewindChargePosition(FixedVector3 position) => Body.Position = position;
    public void NotifyChargeBarrierVisible(object? collider) { }
    public void ApplyChargeBarrierCollision(int mode)
    {
        State.Health = unchecked((short)(State.Health - 100));
        if (State.Health <= 0)
        {
            State.State = 0x15;
            State.Substate = 0;
            return;
        }
        Neutralize();
        State.BodyFlags |= 0x10;
        State.State = 9;
        State.Substate = 0;
    }
    public void ApplyChargeScrapeDamage()
    {
        State.Health = unchecked((short)(State.Health - 10));
        if (State.Health <= 0)
        {
            State.State = 0x15;
            State.Substate = 0;
        }
    }
    public void BeginStuckHorn(RhinoChargeWallContact contact, int shake)
    {
        // CRhino::SetUpStuckHorn (0x47E1F0) LookAt/MoveTo/RunAnim/camera shake: stub until stuck-horn AI is ported.
        _ = contact;
        _ = shake;
    }
    public bool IsPlayerBody(object? collider) => registry.Player is not null && ReferenceEquals(collider, registry.Player);
    public bool TryDetermineFightState(RhinoState state, int argument) => false;
    public bool ChargeLineHitsPlayer(FixedVector3 start, FixedVector3 end, int radius)
    {
        if (registry.Player is null) return false;
        var body = new ActorLaserBody(registry.Player, PlayerPosition, registry.Player.ActorState.MinorRadius,
            registry.Player.ActorState.BodyFlags);
        return ActorLaserCollision.CheckBodies(start, end, [body], radiusScale: radius) is not null;
    }
    public void HitPlayerFromCharge(FixedVector3 delta)
    {
        if (registry.Player is null)
            throw new InvalidOperationException("CRhino::ChargePlayer player Hit needs a live player.");
        registry.Player.Combat.Hit(new ActorHit(0xE, 0, 0xC, 0x1E, delta));
    }
    public ReadOnlySpan<RhinoSpeechEntry> SpeechEntries => configuration.SpeechTable.Span;
    public bool TryPlayPositionSpeech(FixedVector3 position, int bank, int slot, int priority) =>
        configuration.TryPlayPositionSpeech(bank, slot, position, priority);
    public bool PlayerCanTalkNow() =>
        registry.Player is { } player && (player.Combat.State.ActionState & 0x800080) == 0;
    public void AttachRhinoSpeech(int bank, int slot) =>
        SpeechAttachment.Attach(configuration.SourceClock(), bank, slot);
    public void AttachPlayerSpeech(int bank, int slot) =>
        registry.Player!.Combat.SpeechAttachment.Attach(configuration.SourceClock(), bank, slot);
    public bool Hit(ActorHit hit) =>
        throw new NotSupportedException("CRhino::Hit (0x47EE70) is not ported yet.");
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

    private void OnSourceRemoved(GodotScriptActor actor) { if (ReferenceEquals(actor, this)) DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if (destroyed) return;
        destroyed = true; registry.ActorRemoved -= OnSourceRemoved;
        configuration.SetGlobalBoss(null);
        Messages.Cleanup(all: true);
    }
}
