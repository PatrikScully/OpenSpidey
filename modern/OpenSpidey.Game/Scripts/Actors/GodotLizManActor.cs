using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.LizMan;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Original source317 LizMan CBaddy: construction, init/guard AI leaves and module lifetime.</summary>
public partial class GodotLizManActor : GodotScriptActor, ILizManHost, IGodotSourceBaddyBody, IActorScriptHost, IActorHeightHost
{
    private GodotActorRegistry registry = null!;
    private GodotLizManConfiguration configuration = null!;
    private LizManBrain brain = null!;
    private bool begun;
    private bool destroyed;
    public LizManState State { get; private set; } = null!;
    public LizManStateFlags StateFlags { get; } = new();
    public LizManAttackOwnership AttackOwnership { get; } = new();
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
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public int AnimationId => Model.ClipId;
    public int AnimationFrame => Model.Playback.Frame;
    public bool AnimationFinished => Model.Playback.Finished != 0;
    public int ElapsedTicks => FrameTiming.ElapsedTicks;
    public int AttackClock => configuration.AttackClock();
    public int TotalTime => configuration.TotalTime();
    public FixedVector3 PlayerPosition => registry.PlayerPosition ?? default;
    public bool HasMessages => Messages.Head is not null;
    public bool HasLinkedClimbNode => (State.Field2F0 & 0x20) != 0;
    public ushort MinorRadius => State.MinorRadius;
    public ushort BodyFlags => unchecked((ushort)State.BodyFlags);
    public uint BehaviorFlags => State.BehaviorFlags;
    public short Health => State.Health;

    public static GodotLizManActor Create(TriggerNode node, GodotActorRegistry registry, GodotLizManConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != LizManState.Type) throw new NotSupportedException("LizMan factory requires source317.");
        int ordinal = registry.BaddyConstruction.RequireCurrentOrdinal();
        var actor = new GodotLizManActor { Name = $"LizMan{node.Id}", registry = registry, configuration = configuration, Definition = definition };
        try
        {
            actor.State = new(node.Id, registry.Environment.LevelId, configuration.AttackClock(), ordinal);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= ~2u;
            actor.Actor = new(new ActorBodyMotion
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z),
                LinearDamping = new(1, 1, 1), AngularDamping = new(1, 1, 1)
            });
            actor.ScriptState = new() { ActorType = LizManState.Type, NodeId = node.Id };
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
            actor.Height = new(actor.Actor, actor) { Clearance = actor.State.SurfaceClearance, QueryInterval = actor.State.Field212 };
            actor.Route = new(registry.Triggers, node.Id, actor.ParseWaypoint);
            actor.Runtime = new(new ActorScriptCursor(definition.BehaviorData,
                checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor);
            actor.brain = new(actor.State, actor.StateFlags, actor.AttackOwnership, actor);
            actor.AddChild(new GodotNativeDestructionObserver { Name = "LizManDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            LizManBrain.Construct(actor.State, actor, configuration.TotalTime(), () => { });
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
        ScriptState.BehaviorFlags = State.BehaviorFlags;
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
        if (flags != 0) throw new NotSupportedException("LizMan source ground queries use flags0.");
        return World.GroundHeight(position, above, below);
    }
    public int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support) =>
        World.GetGroundHeight(position, above, below, out support);
    public void SetHeight(bool initial, int above, int below) => Height.SetHeight(initial, above, below, ElapsedTicks);
    public void RunAnimation(int animation) => Model.RunClip(animation);
    public void Neutralize() => Actor.Neutralize(Procedures);
    public void ProcessMessages() => Messages.Cleanup(all: true);
    public void UpdateFootstepBits() { }
    public void PlaySounds() { }
    public void LookAtPlayer(int holdTicks, int rate) =>
        throw new NotSupportedException("CLizMan::Guard CAIProc_LookAt (0x4500E0 case0) is not ported yet.");
    public int ScanNearbyNodesForJumpTarget()
    {
        var candidates = new List<LizManJumpCandidate>();
        foreach (TriggerNode node in registry.Triggers.Nodes)
        {
            if (node.Id == 0 || node.TypeCode != 1000) continue;
            TriggerPlacement placement = TriggerPlacement.Read(node);
            int scriptOffset = placement.AfterPositionOffset + 6;
            if (scriptOffset + 2 > node.Data.Length) continue;
            if (BinaryPrimitives.ReadUInt16LittleEndian(node.Data.Span[scriptOffset..]) != 0x470A) continue;
            candidates.Add(new(node.Id, new FixedVector3(placement.Position.X, placement.Position.Y, placement.Position.Z) << 12));
        }
        return LizManJumpScan.Select(Body.Position, PlayerPosition, candidates, IsJumpSlotOccupied);
    }
    private bool IsJumpSlotOccupied(FixedVector3 position)
    {
        foreach (object identity in registry.SourceList(ActorUpdateGroup.Enemy).SourceOrder)
        {
            if (identity is not GodotLizManActor other || !registry.Owns(other) || ReferenceEquals(other, this)) continue;
            if (ThugPerception.ApproximateHorizontalDistance(position, other.Body.Position) < 0x100) return true;
            if (other.State.JumpPositions is { Length: > 0 } arr)
            {
                FixedVector3 mid = arr[Math.Min(arr.Length - 1, arr.Length / 2)];
                if (ThugPerception.ApproximateHorizontalDistance(position, mid) < 0x100) return true;
            }
        }
        return false;
    }
    public FixedVector3 TriggerPosition(int node) => World.WaypointPosition(node);
    public void PlayPositionalSound(int id) { }
    public int Random(int bound) => configuration.Random(bound);
    public bool Hit(ActorHit hit) =>
        throw new NotSupportedException("CLizMan::Hit (0x44BA00) is not ported yet.");
    public void MarkSourceBodyDead() => State.BodyFlags |= 0x40;
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) =>
        Messages.Enqueue(new ThugMessage(code, sender) { Vector0 = vector });

    private void SyncScriptStorage()
    {
        Variables.Position = Body.Position; Variables.Health = State.Health;
        Variables.CollisionRadius = State.MinorRadius; Variables.GroundOffset = State.SurfaceClearance;
        ScriptState.BehaviorFlags = State.BehaviorFlags;
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
        if (opcode == 0x470F) { State.CombatFlags |= 0x02000000; return true; }
        if (opcode == 0x4227)
        {
            ushort interval = operands.ReadWord();
            Route.Flags |= 4;
            Height.QueryInterval = unchecked((byte)interval);
            Height.QueryElapsed = 0;
            return true;
        }
        throw new NotSupportedException($"LizMan{Definition.NodeId}: original script command0x{opcode:X4} is not connected.");
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
        LizManAttackFlags.Clear(State, this, AttackOwnership);
        Messages.Cleanup(all: true);
    }
}
