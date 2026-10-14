using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Procedures;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Original source324 CSimby: constructor 0x4A7870, AI 0x4AE3D0 Initialize/Emerge/MoveFreely leaves.</summary>
public partial class GodotSimbyActor : GodotScriptActor, ISimbyConstructionHost, ISimbyHost, IGodotSourceBaddyBody, IActorScriptHost, IActorHeightHost
{
    private GodotActorRegistry registry = null!;
    private GodotSimbyConfiguration configuration = null!;
    private SimbyBrain brain = null!;
    private TriggerNode sourceNode = null!;
    private bool begun;
    private bool destroyed;
    public SimbyState State { get; private set; } = null!;
    public SimbyStateFlags StateFlags { get; } = new();
    public ActorProcedureState Actor { get; private set; } = null!;
    public ActorProcedureList Procedures { get; } = new();
    public ActorBodyMotion Body => Actor.Body;
    public ActorHeightController Height { get; private set; } = null!;
    public ActorWaypointRoute Route { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public ActorPoseLinkPacket PoseLinks { get; private set; } = null!;
    public GodotSourceVertexLighting Lighting { get; private set; } = null!;
    public GodotActorWorldQuery World { get; private set; } = null!;
    public ThugMessageQueue Messages { get; } = new();
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
    public short PlayerHealth => registry.Player?.Combat.State.Health ?? 0;
    public byte PlayerSpecialState => 0;
    public byte PlayerInputLock => 0;
    public bool HasMessages => Messages.Head is not null;
    public bool HasProcedures => Procedures.Count != 0;
    public bool HasSupportBody => false;
    public int IdleSetupLow => 513;
    public int IdleSetupHigh => 0;
    public int CurrentWaypoint => Route.CurrentNode;
    public ushort WaypointFlags { get => unchecked((ushort)Route.Flags); set => Route.Flags = value; }
    public ushort MinorRadius => State.MinorRadius;
    public ushort BodyFlags => unchecked((ushort)State.BodyFlags);
    public uint BehaviorFlags => State.BehaviorFlags;
    public short Health => State.Health;

    public static GodotSimbyActor Create(TriggerNode node, GodotActorRegistry registry, GodotSimbyConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != SimbyState.Type) throw new NotSupportedException("Simby factory requires source324.");
        int ordinal = registry.BaddyConstruction.RequireCurrentOrdinal();
        var actor = new GodotSimbyActor
        {
            Name = $"Simby{node.Id}", registry = registry, configuration = configuration, Definition = definition, sourceNode = node
        };
        try
        {
            actor.State = new(node.Id, registry.Environment.LevelId, ordinal);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= ~2u;
            actor.Actor = new(new ActorBodyMotion
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z),
                AngularDamping = new(1, 1, 1)
            });
            actor.ScriptState = new() { ActorType = SimbyState.Type, NodeId = node.Id };
            actor.Variables = new(actor.ScriptState, registry)
            {
                Position = actor.Body.Position, GroundOffset = actor.State.SurfaceClearance,
                CollisionRadius = actor.State.MinorRadius, ItemFlags = 2, AnimationSpeed = 0x10000
            };
            actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            actor.World = new(actor, registry.Triggers);
            actor.Height = new(actor.Actor, actor) { Clearance = actor.State.SurfaceClearance };
            actor.Route = new(registry.Triggers, node.Id, actor.ParseWaypoint);
            actor.brain = new(actor.State, configuration.World, actor.StateFlags, actor);
            actor.AddChild(new GodotNativeDestructionObserver { Name = "SimbyDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            SimbyConstruction.Construct(actor.State, configuration.World, actor, configuration.AttackClock());
            actor.BuildSourceTransform();
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    public void InitializeModel(string name)
    {
        Model = configuration.ModelFactory(State);
        Model.AdvanceAutomatically = false;
        AddChild(Model);
        Lighting = new(Model, new SourceModelLight(
            new(4096, 0, 0, 0, 4096, 0, 0, 0, 4096, 0, 0, 0),
            new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), 0, 0, 0), configuration.LightingState);
    }
    public void AttachToSourceBaddyList() { }
    public void ReadPosePacket()
    {
        PoseLinks = configuration.PosePacket(Model.SourcePartCount);
        Model.InitializeSourceJoints(PoseLinks);
    }
    public void ParseAuthoredScript()
    {
        Runtime = new(new ActorScriptCursor(Definition.BehaviorData,
            checked(sourceNode.SourceOffset + Definition.ObjectDataOffset + 18)), this);
        Runtime.Start(registry.Environment);
    }
    public int NextRandom(int bound) => configuration.Random(bound);
    public void EnsureVertexWibbler() => configuration.EnsureVertexWibbler();

    internal override void Begin()
    {
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

    public int PerimeterDistance(int mode) =>
        ThugPerception.ApproximateHorizontalDistance(Body.Position, PlayerPosition);
    public int GroundHeight(FixedVector3 position, int above, int below, int flags)
    {
        if (flags != 0) throw new NotSupportedException("CSimby ground queries use flags0.");
        return World.GroundHeight(position, above, below);
    }
    public int GroundHeightWithSupport(FixedVector3 position, int above, int below, out bool hasSupport)
    {
        int ground = World.GetGroundHeight(position, above, below, out ActorBodyMotion? support);
        hasSupport = support is not null;
        return ground;
    }
    public int GetGroundHeight(FixedVector3 position, int above, int below, out ActorBodyMotion? support) =>
        World.GetGroundHeight(position, above, below, out support);
    public int SetHeight(bool initial, int above, int below) =>
        Height.SetHeight(initial, above, below, ElapsedTicks);
    public void RunAnimation(int animation) => Model.RunClip(animation);
    public void CycleAnimation(int animation) => Model.RunClip(animation);
    public void Neutralize() => Actor.Neutralize(Procedures);
    public void ProcessMessages() => Messages.Cleanup(all: true);
    public void ProcessProcedures() => Procedures.Execute(ElapsedTicks);
    public void DoPhysics(int mode)
    {
        if (mode != 0) throw new NotSupportedException($"CSimby DoPhysics mode{mode} is not ported yet.");
        Body.Integrate(ElapsedTicks);
    }
    public void RunTimer(ref int timer)
    {
        timer = unchecked(timer - ElapsedTicks);
        if (timer < 0) timer = 0;
    }
    public bool TryAdvanceWaypoint()
    {
        bool advanced = Route.Advance();
        State.RouteFlags = unchecked((ushort)Route.Flags);
        return advanced;
    }
    public FixedVector3 WaypointPosition(int node) => World.WaypointPosition(node);
    public int GetToTarget(int mode) => SimbyGetToTarget.TryEarlyExit(State, this, mode);
    public void SpawnEmergeEffect() { }
    public void PlayPositionalSound(int id) { }
    public int Random(int bound) => configuration.Random(bound);
    public void EnsureSlimeQuad() => State.SlimeHandle = 1;
    public void DestroySlimeQuad() => State.SlimeHandle = 0;
    public void UpdateSlimeQuad(bool invertedClearance) { }
    public bool LineOfSightToPlayer()
    {
        FixedVector3 hit = default;
        return World.LineOfSight(Body.Position, PlayerPosition, 0, ref hit);
    }
    public bool RegisterTargetFromPlayer() =>
        throw new NotSupportedException("CSimby::RegisterTargetPos (0x4A2680) is not ported yet.");
    public bool BumpedIntoSpidey(int radius) => false;
    public bool Hit(ActorHit hit) => throw new NotSupportedException("CSimby::Hit 0x4A8AB0 is not ported.");
    public void MarkSourceBodyDead() => State.BodyFlags |= 0x40;
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) =>
        Messages.Enqueue(new ThugMessage(code, sender) { Vector0 = vector });
    public short ReadVariable(ushort variable, ActorScriptCursor operands) => Variables.Read(variable, operands);
    public void WriteVariable(ushort variable, ActorScriptCursor operands) => Variables.Write(variable, operands);
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
    {
        if (GodotLinkedBaddyCommands.TryExecute(registry, SourceNodeId, opcode, operands)) return true;
        if (GodotActorLinkEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotActorDirectEventCommands.TryExecute(registry, opcode, operands, ReadVariable)) return true;
        if (GodotEnvironmentChunkCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorScreenFlashCommands.TryExecute(registry, opcode, operands)) return true;
        if (GodotActorSparkCommands.TryExecute(registry, opcode, operands, SourcePosition, Body.Angles)) return true;
        if (GodotActorCameraShakeCommands.TryExecute(registry, opcode, operands, ReadVariable, SourcePosition)) return true;
        if (GodotActorVisualEffectCommands.TryExecute(registry, opcode, operands, SourcePosition)) return true;
        if (GodotActorSpeechCommands.TryExecute(registry, opcode, operands, ScriptState, environment, SourcePosition)) return true;
        if (opcode == 0x42B9 && GodotActorPlayerScriptCommands.TryExecute(registry, opcode, operands)) return true;
        if (opcode == 0x470F) { State.BehaviorFlags |= 0x02000000; return true; }
        if (Route.TryExecuteCommand(opcode, operands)) return true;
        if (opcode == 0x4227)
        {
            ushort interval = operands.ReadWord();
            State.RouteFlags |= 4;
            State.HeightQueryInterval = unchecked((byte)interval);
            State.HeightQueryElapsed = 0;
            Height.QueryInterval = unchecked((byte)interval);
            Height.QueryElapsed = 0;
            return true;
        }
        throw new NotSupportedException($"CSimby actor script requires an unported opcode{opcode:X4}.");
    }
    public ActorScriptCursor ScriptAtNode(ushort nodeId) => registry.ScriptAtNode(nodeId);
    public bool PlayerCeilingCheck(short x, short z) => throw new NotSupportedException("CSimby script has no proven player-ceiling dependency.");
    public void FlashCompass() => throw new NotSupportedException("CSimby script has no proven compass dependency.");
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void Retire() => throw new NotSupportedException("CSimby must retain its native body resources.");
    private void ParseWaypoint(ActorScriptCursor cursor) => Runtime = new(cursor, this);
    private void OnSourceRemoved(GodotScriptActor actor) { if (ReferenceEquals(actor, this)) DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if (destroyed) return;
        destroyed = true; registry.ActorRemoved -= OnSourceRemoved;
        configuration.BossGlobal.ClearIf(this);
        SimbyReactions.ClearAttackData(State, configuration.World);
        configuration.World.Count = Math.Max(0, configuration.World.Count - 1);
        Messages.Cleanup(all: true);
    }
}
