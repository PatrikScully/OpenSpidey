using Godot;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>Original source412 CPunchOb: constructor 0x4A73B0 and AI 0x4A75E0.</summary>
public partial class GodotPunchObActor : GodotScriptActor, IPunchObHost, IGodotSourceBaddyBody, IActorScriptHost
{
    private GodotActorRegistry registry = null!;
    private GodotSimbyConfiguration configuration = null!;
    private PunchObBrain brain = null!;
    private TriggerNode sourceNode = null!;
    private bool begun;
    private bool destroyed;
    public PunchObState State { get; private set; } = null!;
    public ActorBodyMotion Body { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public ThugMessageQueue Messages { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public ushort InputFlags { get => ScriptState.PendingSignals; set => ScriptState.PendingSignals = value; }
    public int AnimationId => Model.ClipId;
    public bool AnimationFinished => Model.Playback.Finished != 0;
    public bool HasMessages => Messages.Head is not null;
    public int ElapsedTicks => FrameTiming.ElapsedTicks;
    public int SourceScale { get; set; } = 4096;
    public bool HasTrapWeb => false;
    public ushort MinorRadius => State.MinorRadius;
    public ushort BodyFlags => unchecked((ushort)State.BodyFlags);
    public uint BehaviorFlags => State.BehaviorFlags;
    public short Health => State.Health;

    public static GodotPunchObActor Create(TriggerNode node, GodotActorRegistry registry, GodotSimbyConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != PunchObState.Type) throw new NotSupportedException("PunchOb factory requires source412.");
        int ordinal = registry.BaddyConstruction.RequireCurrentOrdinal();
        var actor = new GodotPunchObActor
        {
            Name = $"PunchOb{node.Id}", registry = registry, configuration = configuration, Definition = definition, sourceNode = node
        };
        try
        {
            actor.State = new(node.Id, ordinal);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= ~2u;
            actor.Body = new()
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z),
                AngularDamping = new(1, 1, 1)
            };
            actor.ScriptState = new() { ActorType = PunchObState.Type, NodeId = node.Id };
            actor.Variables = new(actor.ScriptState, registry)
            {
                Position = actor.Body.Position, CollisionRadius = actor.State.MinorRadius, ItemFlags = 2, AnimationSpeed = 0x10000
            };
            actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            actor.brain = new(actor.State, actor);
            actor.AddChild(new GodotNativeDestructionObserver { Name = "PunchObDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            PunchObConstruction.Construct(actor.State, actor);
            actor.BuildSourceTransform();
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    public void InitializeModel(string name)
    {
        Model = configuration.PunchObModelFactory(State);
        Model.AdvanceAutomatically = false;
        AddChild(Model);
    }
    public void AttachToSourceBaddyList() { }
    public void RunAnimation(int animation) => Model.RunClip(animation);
    public void ParseAuthoredScript()
    {
        Runtime = new(new ActorScriptCursor(Definition.BehaviorData,
            checked(sourceNode.SourceOffset + Definition.ObjectDataOffset + 18)), this);
        Runtime.Start(registry.Environment);
    }
    public void DeleteAllMessages() => Messages.Cleanup(all: true);
    public void BuildTransform() => BuildSourceTransform();
    public void BurstTrapWeb() => throw new NotSupportedException("CPunchOb trap-web burst needs the CTrapWebEffect adapter.");
    public void HitFromInput(short health) => throw new NotSupportedException("CPunchOb::Hit 0x4A74F0 is not ported.");

    internal override void Begin() { begun = true; BuildSourceTransform(); }
    internal override void Advance()
    {
        SourceUpdates++;
        State.BodyFlags = FrameTiming.Prepare(configuration.Clock(), unchecked((ushort)State.BodyFlags), ItemFlags,
            new(Model.Playback.Frame, Model.Playback.ClipId, Model.Playback.Direction));
        if ((ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        brain.Advance(configuration.AttackClock());
        Variables.Position = Body.Position;
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    public override void _Process(double delta)
    {
        if (!begun || destroyed || registry.Player is null) return;
        ItemFlags = configuration.IsPunchObCulled(this) ? (ushort)(ItemFlags | 0x8000) : (ushort)(ItemFlags & 0x7FFF);
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    private void BuildSourceTransform()
    {
        ActorAngles angles = Body.Angles;
        SourceTransformBasis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        Transform3D transform = PsxCoordinates.CharacterTransform(SourceTransformBasis);
        transform.Origin = PsxCoordinates.FixedWorldPosition((Body.Position >> 12) << 12);
        Transform = transform;
    }
    public bool Hit(ActorHit hit) => throw new NotSupportedException("CPunchOb::Hit 0x4A74F0 is not ported.");
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
        if (opcode == 0x4227) { operands.ReadWord(); return true; }
        throw new NotSupportedException($"CPunchOb actor script requires an unported opcode{opcode:X4}.");
    }
    public ActorScriptCursor ScriptAtNode(ushort nodeId) => registry.ScriptAtNode(nodeId);
    public bool PlayerCeilingCheck(short x, short z) => throw new NotSupportedException("CPunchOb script has no proven player-ceiling dependency.");
    public void FlashCompass() => throw new NotSupportedException("CPunchOb script has no proven compass dependency.");
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void Retire() => throw new NotSupportedException("CPunchOb must retain its native body resources.");
    private void OnSourceRemoved(GodotScriptActor actor) { if (ReferenceEquals(actor, this)) DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if (destroyed) return;
        destroyed = true; registry.ActorRemoved -= OnSourceRemoved;
        Messages.Cleanup(all: true);
    }
}
