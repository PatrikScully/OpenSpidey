using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Npcs;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Original source303 Mary Jane, 326 lizard and 719 submariner CBaddy bodies with genuine models and lights.</summary>
public partial class GodotIdleNpcActor : GodotScriptActor, IIdleNpcHost, IGodotSourceBaddyBody
{
    private GodotActorRegistry registry = null!;
    private GodotIdleNpcConfiguration configuration = null!;
    private IdleNpcBrain brain = null!;
    private bool begun;
    private bool destroyed;
    public IdleNpcState State { get; private set; } = null!;
    public ActorBodyMotion Body { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public GodotSourceVertexLighting Lighting { get; private set; } = null!;
    public ThugMessageQueue Messages { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public int TransformBuilds { get; private set; }
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public ushort InputFlags { get => ScriptState.PendingSignals; set => ScriptState.PendingSignals = value; }
    public int AnimationId => Model.ClipId;
    public bool HasMessages => Messages.Head is not null;
    public bool FastForwardRequested => configuration.FastForwardRequested();
    public ushort MinorRadius => State.MinorRadius;
    public ushort BodyFlags => unchecked((ushort)State.BodyFlags);
    public uint BehaviorFlags => 0;
    public short Health => 0;

    public static GodotIdleNpcActor Create(TriggerNode node, GodotActorRegistry registry, GodotIdleNpcConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        IdleNpcKind kind = IdleNpcState.KindOf(definition.ActorType);
        int ordinal = registry.BaddyConstruction.RequireCurrentOrdinal();
        var actor = new GodotIdleNpcActor { Name = $"{kind}{node.Id}", registry = registry, configuration = configuration, Definition = definition };
        try
        {
            actor.State = new(kind, node.Id, ordinal);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= ~2u;
            actor.Body = new() { Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z), AngularDamping = new(1, 1, 1) };
            actor.ScriptState = new() { ActorType = actor.State.BodyType, NodeId = node.Id };
            actor.Variables = new(actor.ScriptState, registry) { Position = actor.Body.Position, CollisionRadius = actor.State.MinorRadius,
                ItemFlags = 2, AnimationSpeed = 0x10000 };
            actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            actor.Model = configuration.ModelFactory(actor.State);
            actor.Model.AdvanceAutomatically = false; actor.AddChild(actor.Model);
            actor.Lighting = new(actor.Model, configuration.Light(actor.State), configuration.LightingState);
            actor.brain = new(actor.State, actor);
            actor.AddChild(new GodotNativeDestructionObserver { Name = "IdleNpcDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            actor.ConstructSourceBody();
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    private void ConstructSourceBody()
    {
        // CycleAnim(0,1) precedes the constructor's 0x480 item flags; the abort test follows the type/node stores.
        Model.CycleClip(0, 1);
        ItemFlags |= 0x480;
        if (State.DiesOnFastForwardAtConstruction && FastForwardRequested) State.Die(this);
        BuildSourceTransform();
    }

    internal override void Begin() { begun = true; BuildSourceTransform(); }
    internal override void Advance()
    {
        SourceUpdates++;
        State.BodyFlags = FrameTiming.Prepare(configuration.Clock(), unchecked((ushort)State.BodyFlags), ItemFlags,
            new(Model.Playback.Frame, Model.Playback.ClipId, Model.Playback.Direction));
        if ((ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        brain.Advance();
        Variables.Position = Body.Position;
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    public override void _Process(double delta)
    {
        if (!begun || destroyed || registry.Player is null) return;
        ItemFlags = configuration.IsSourceCulled(this) ? (ushort)(ItemFlags | 0x8000) : (ushort)(ItemFlags & 0x7FFF);
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }

    /// <summary>Called by the source render phase in original actor-list order.</summary>
    public void SynchronizeSourceLighting() => Lighting.Synchronize(SourceTransformBasis, ItemFlags);
    public void CleanUpMessages(bool all, bool keep)
    {
        if (keep) throw new NotSupportedException("Idle NPC AI only deletes its whole CMessage list.");
        Messages.Cleanup(all);
    }
    public void CycleAnimation(int animation, int direction) => Model.CycleClip(animation, checked((sbyte)direction));
    /// <summary>M3d_BuildTransform (0x475F50) from the body angles and whole-unit position.</summary>
    public void BuildTransform() => BuildSourceTransform();
    private void BuildSourceTransform()
    {
        ActorAngles angles = Body.Angles;
        SourceTransformBasis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        Transform3D transform = PsxCoordinates.CharacterTransform(SourceTransformBasis);
        transform.Origin = PsxCoordinates.FixedWorldPosition((Body.Position >> 12) << 12);
        Transform = transform;
        TransformBuilds++;
    }
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void MarkDead() => Lifecycle.Kill();
    /// <summary>All three vtables share slot3 0x4B0D60, the unconditional return0 leaf.</summary>
    public bool Hit(ActorHit hit) => false;
    public void MarkSourceBodyDead() => State.BodyFlags |= 0x40;
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) =>
        Messages.Enqueue(new ThugMessage(code, sender) { Vector0 = vector });

    private void OnSourceRemoved(GodotScriptActor actor) { if (ReferenceEquals(actor, this)) DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if (destroyed) return;
        destroyed = true; registry.ActorRemoved -= OnSourceRemoved;
        Messages.Cleanup(all: true);
    }
}
