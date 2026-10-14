using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Actors.Venom;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Source313 CVenom: module factory, wait-state mission hooks and walkabout/wait AI leaves.</summary>
public partial class GodotVenomActor : GodotScriptActor, IVenomHost, IGodotSourceBaddyBody, IActorScriptHost, IActorHeightHost, IMissionVenom
{
    private GodotActorRegistry registry = null!;
    private GodotVenomConfiguration configuration = null!;
    private VenomBrain brain = null!;
    private bool begun;
    private bool destroyed;
    public VenomState State { get; private set; } = null!;
    public ActorProcedureState Actor { get; private set; } = null!;
    public ActorProcedureList Procedures { get; } = new();
    public ActorBodyMotion Body => Actor.Body;
    public ActorHeightController Height { get; private set; } = null!;
    public ActorWaypointRoute Route { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public GodotSourceVertexLighting Lighting { get; private set; } = null!;
    public GodotActorWorldQuery World { get; private set; } = null!;
    public ThugMessageQueue Messages { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public FixedVector3 PrimaryAimTarget { get; private set; }
    public FixedVector3 SecondaryAimTarget { get; private set; }
    public FixedVector3? MechListPosition => registry.PlayerPosition;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public int AnimationId => Model.ClipId;
    public int AnimationFrame => Model.Playback.Frame;
    public bool AnimationFinished => Model.Playback.Finished != 0;
    public int ElapsedTicks => FrameTiming.ElapsedTicks;
    public int LevelId => registry.Environment.LevelId;
    public int DifficultyLevel => configuration.DifficultyLevel();
    public FixedVector3 BodyPosition => Body.Position;
    public FixedVector3 PlayerPosition => registry.PlayerPosition ?? default;
    public bool HasMessages => Messages.Head is not null;
    public ushort MinorRadius => State.MinorRadius;
    public ushort BodyFlags => unchecked((ushort)State.BodyFlags);
    public uint BehaviorFlags => State.Field218;
    public short Health => State.Health;

    public static GodotVenomActor Create(TriggerNode node, GodotActorRegistry registry, GodotVenomConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != VenomState.Type) throw new NotSupportedException("Venom factory requires source313.");
        int ordinal = registry.BaddyConstruction.RequireCurrentOrdinal();
        var actor = new GodotVenomActor { Name = $"Venom{node.Id}", registry = registry, configuration = configuration, Definition = definition };
        try
        {
            actor.State = new(node.Id, registry.Environment.LevelId, configuration.DifficultyLevel(), ordinal);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= ~2u;
            actor.Actor = new(new ActorBodyMotion
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z),
                LinearDamping = new(1, 1, 1), AngularDamping = new(1, 1, 1)
            });
            actor.ScriptState = new() { ActorType = VenomState.Type, NodeId = node.Id };
            actor.Variables = new(actor.ScriptState, registry)
            {
                Position = actor.Body.Position, GroundOffset = actor.State.SurfaceClearance,
                CollisionRadius = actor.State.MinorRadius, ItemFlags = 2, AnimationSpeed = 0x10000
            };
            actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            actor.Model = configuration.ModelFactory(actor.State);
            actor.Model.AdvanceAutomatically = false; actor.AddChild(actor.Model);
            actor.Lighting = new(actor.Model, configuration.Light(), configuration.LightingState);
            actor.World = new(actor, registry.Triggers);
            actor.Height = new(actor.Actor, actor) { Clearance = actor.State.SurfaceClearance, QueryInterval = 0x1E };
            actor.Route = new(registry.Triggers, node.Id, actor.ParseWaypoint);
            actor.Runtime = new(new ActorScriptCursor(definition.BehaviorData,
                checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor);
            actor.brain = new(actor.State, actor);
            actor.AddChild(new GodotNativeDestructionObserver { Name = "VenomDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            VenomBrain.Construct(actor.State, actor, () => { }, configuration.CreateHealthBar);
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
        State.BodyFlags = FrameTiming.Prepare(0, unchecked((ushort)State.BodyFlags), ItemFlags,
            new(Model.Playback.Frame, Model.Playback.ClipId, Model.Playback.Direction));
        if ((ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        if (registry.PlayerPosition is { } player) PrimaryAimTarget = SecondaryAimTarget = player;
        brain.SnapToGround(position => World.GroundHeight(position, 0, 0x1000));
        brain.Advance();
        Variables.Position = Body.Position;
        Variables.CollisionRadius = State.MinorRadius;
        Variables.GroundOffset = State.SurfaceClearance;
        Variables.Health = State.Health;
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }

    public override void _Process(double delta)
    {
        if (!begun || destroyed || registry.Player is null) return;
        ItemFlags = configuration.IsSourceCulled(this) ? (ushort)(ItemFlags | 0x8000) : (ushort)(ItemFlags & 0x7FFF);
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }

    public void EnterWaitState() => VenomWaitState.Enter(State);
    public void ExitWaitState(uint command, uint operand) =>
        VenomWaitState.Exit(State, unchecked((ushort)command), unchecked((ushort)operand));
    public void MarkSourceBodyDead() => State.BodyFlags |= 0x40;
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) =>
        Messages.Enqueue(new ThugMessage(code, sender) { Vector0 = vector });

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

    public int GroundHeight(FixedVector3 position, int above, int below) => World.GroundHeight(position, above, below);
    public int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support) =>
        World.GetGroundHeight(position, above, below, out support);
    public void SetHeight(bool initial, int above, int below) => Height.SetHeight(initial, above, below, ElapsedTicks);
    public void RunAnimation(int animation) => Model.RunClip(animation);
    public void Neutralize() => Actor.Neutralize(Procedures);
    public void ProcessMessages() => Messages.Cleanup(all: true);
    public void PlaySounds() { }
    public void PulseWaterModel(bool enabled) =>
        throw new NotSupportedException("CVenom::PulseL6A4Node / AdjustWaterModel (0x4F0F00) is not ported yet.");

    public bool Hit(ActorHit hit) =>
        throw new NotSupportedException("CVenom::Hit (0x4EB7F0) is not ported yet.");

    private void SyncScriptStorage()
    {
        Variables.Position = Body.Position; Variables.Health = State.Health;
        Variables.CollisionRadius = State.MinorRadius; Variables.GroundOffset = State.SurfaceClearance;
    }
    public short ReadVariable(ushort variable, ActorScriptCursor operands) { SyncScriptStorage(); return Variables.Read(variable, operands); }
    public void WriteVariable(ushort variable, ActorScriptCursor operands)
    {
        SyncScriptStorage(); Variables.Write(variable, operands);
        Body.Position = Variables.Position; State.Health = Variables.Health;
        State.MinorRadius = Variables.CollisionRadius; Model.PlaybackSpeed = Variables.AnimationSpeed;
    }
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment) =>
        throw new NotSupportedException($"Venom{Definition.NodeId}: original script command0x{opcode:X4} is not connected.");
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
        Messages.Cleanup(all: true);
    }
}
