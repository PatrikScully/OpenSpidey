using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.SpClone;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Source327 CSpClone: landing, analogue-input bytecode, head look and MGS shadow over the genuine spidey model.</summary>
public partial class GodotSpCloneActor : GodotScriptActor, ISpCloneHost, IGodotSourceBaddyBody
{
    private GodotActorRegistry registry = null!;
    private GodotSpCloneConfiguration configuration = null!;
    private SpCloneBrain brain = null!;
    private ActorPoseLinkPacket poseLinks = null!;
    private FixedJointTransform[] hookPose = [];
    private bool begun, destroyed;
    public SpCloneState State { get; private set; } = null!;
    public ActorBodyMotion Body { get; private set; } = null!;
    public CinematicActorScriptRuntime CinematicScript { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public GodotSourceVertexLighting Lighting { get; private set; } = null!;
    public GodotActorWorldQuery World { get; private set; } = null!;
    public ThugMessageQueue Messages { get; } = new();
    public ActorFrameTiming FrameTiming { get; } = new();
    public GodotCinematicFootprintShadow? Shadow { get; private set; }
    public FixedJointTransform SourceTransformBasis { get; private set; }
    public FixedVector3 SourceTransformTranslation { get; private set; }
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public ushort MinorRadius => State.MinorRadius;
    public ushort BodyFlags => unchecked((ushort)State.BodyFlags);
    public uint BehaviorFlags => 0;
    public short Health => State.Health;
    public int SurfaceClearance => State.Field21E;
    public int AnimationId => Model.ClipId;
    public bool AnimationFinished => Model.Playback.Finished != 0;
    public bool FastForwardRequested => configuration.FastForwardRequested();
    public bool HasMessages => Messages.Head is not null;
    public bool HasSourceJoints => Model.HasSourceJoints;
    public FixedVector3 PlayerPosition => registry.PlayerPosition ?? throw new InvalidOperationException("SpClone AI requires the actual scene player.");
    public AimAngles HeadAngles
    {
        get { RawJointPose head = Model.GetSourceJoint(3); return new(head.RotationX, head.RotationY); }
        set => Model.SetSourceJoint(3, Model.GetSourceJoint(3) with { RotationX = value.Pitch, RotationY = value.Yaw });
    }

    public static GodotSpCloneActor Create(TriggerNode node, GodotActorRegistry registry, GodotSpCloneConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != SpCloneState.Type)
            throw new NotSupportedException("SpClone factory requires source327.");
        int ordinal = registry.BaddyConstruction.RequireCurrentOrdinal();
        var actor = new GodotSpCloneActor { Name = $"SpClone{node.Id}", registry = registry, configuration = configuration, Definition = definition };
        try
        {
            actor.State = new(node.Id, registry.Environment.LevelId, ordinal);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= ~2u;
            actor.Body = new()
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z),
                AngularDamping = new(1, 1, 1)
            };
            actor.ScriptState = new() { ActorType = SpCloneState.Type, NodeId = node.Id };
            actor.Variables = new(actor.ScriptState, registry)
            {
                Position = actor.Body.Position, GroundOffset = actor.State.Field21E, CollisionRadius = actor.State.MinorRadius,
                ItemFlags = 2, AnimationSpeed = 0x10000, Health = actor.State.Health
            };
            actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            actor.Model = configuration.ModelFactory();
            actor.Model.AdvanceAutomatically = false;
            actor.AddChild(actor.Model);
            actor.hookPose = new FixedJointTransform[actor.Model.SourcePartCount];
            actor.poseLinks = configuration.PoseLinks(actor.Model.SourcePartCount);
            actor.Lighting = new(actor.Model, configuration.Light(), configuration.LightingState);
            actor.World = new(actor, registry.Triggers);
            actor.CinematicScript = new(new ActorScriptCursor(definition.BehaviorData,
                checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor, CinematicActorScriptProfile.SpiderClone);
            actor.brain = new(actor.State, actor, actor.CinematicScript);
            actor.AddChild(new GodotNativeDestructionObserver { Name = "SpCloneDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            SpCloneBrain.Construct(actor.State, actor, () => { });
            actor.BuildSourceTransform();
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    internal override void Begin() { begun = true; BuildSourceTransform(); }
    internal override void Advance()
    {
        SourceUpdates++;
        State.BodyFlags = FrameTiming.Prepare(configuration.Clock(), unchecked((ushort)State.BodyFlags), ItemFlags,
            new(Model.Playback.Frame, Model.Playback.ClipId, Model.Playback.Direction));
        if ((ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        brain.Advance(FrameTiming.ElapsedTicks);
        Variables.Position = Body.Position;
        Variables.Health = State.Health;
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    public override void _Process(double delta)
    {
        if (!begun || destroyed || registry.Player is null) return;
        ItemFlags = configuration.IsSourceCulled(this) ? (ushort)(ItemFlags | 0x8000) : (ushort)(ItemFlags & 0x7FFF);
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    /// <summary>M3d_BuildTransform (0x475F50); a disabled corrected pose returns to the sampled clip.</summary>
    public void BuildTransform()
    {
        BuildSourceTransform();
        if (Model.SourcePoseEnabled) Model.ApplySourceJointPose(false);
    }
    private void BuildSourceTransform()
    {
        ActorAngles angles = Body.Angles;
        SourceTransformBasis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        SourceTransformTranslation = Body.Position >> 12;
        Transform3D transform = PsxCoordinates.CharacterTransform(SourceTransformBasis);
        transform.Origin = PsxCoordinates.FixedWorldPosition(SourceTransformTranslation << 12);
        Transform = transform;
    }
    /// <summary>CSuper::ApplyPose(0x555888): first call allocates the twelve links; each call rebuilds the corrected pose.</summary>
    public void ApplyPosePacket()
    {
        Model.InitializeSourceJoints(poseLinks);
        BuildSourceTransform(); Model.ApplySourceJointPose(true);
    }
    /// <summary>Called by the source render phase in original actor-list order; AI never advances shared colour scratch.</summary>
    public void SynchronizeSourceLighting() => Lighting.Synchronize(SourceTransformBasis, ItemFlags);
    public FixedVector3 TriggerPosition(int node) => World.WaypointPosition(node);
    public IReadOnlyList<ushort> TriggerLinks(int node) => (uint)node < registry.Triggers.Nodes.Count
        ? MissionRuntime.ReadLinks(registry.Triggers.Nodes[node])
        : throw new InvalidDataException("SpClone link lookup references a missing source node.");
    public int GroundHeight(FixedVector3 position, int above, int below, int flags)
    {
        if (flags != 0) throw new NotSupportedException("SpClone source ground queries use flags0.");
        return World.GroundHeight(position, above, below);
    }
    public void RunAnimation(int animation) => Model.RunClip(animation);
    public void CycleAnimation(int animation) => Model.CycleClip(animation, 1);
    public void PlayVoice(short bank, short track, short priority) => registry.VoicePlayback(bank, track, priority);
    public bool ConsumeAnimationSignal(CinematicActorSignalSource source)
    {
        if (source != CinematicActorSignalSource.InputFlags) throw new NotSupportedException("SpClone waits on source inputflags1 at CBody44.");
        if ((ScriptState.PendingSignals & 1) == 0) return false;
        ScriptState.PendingSignals &= 0xFFFE; return true;
    }
    public AimAngles LookTarget(int node)
    {
        FixedVector3 hook = Hook(7);
        FixedVector3 local = FixedRotation.Transform(CinematicActorPose.Transpose(SourceTransformBasis),
            (TriggerPosition(node) - hook) >> 12) << 12;
        return FixedAim.Calculate(default, local);
    }
    /// <summary>CSpClone::DoMGSShadow 0x4B0EF0: hooks14/17/11/6 over the landing plane.</summary>
    public void UpdateShadow()
    {
        FixedVector3[] corners = CinematicActorPose.ShadowCorners(Body.Position, SourceTransformBasis,
            State.GroundY, new[] { Hook(14), Hook(17), Hook(11), Hook(6) });
        if (Shadow is null)
        {
            Shadow = GodotCinematicFootprintShadow.Create(configuration.ShadowTexture); AddChild(Shadow);
            GodotCinematicFootprintShadow captured = Shadow;
            configuration.SourceShadowCreated(new(captured, _ => { }, () => captured.IsAlive, captured.DestroySourceBit));
        }
        Shadow.SetCorners(corners);
    }
    /// <summary>CSpClone vtable slot3 is 0x4B0D60, the unconditional return0 leaf.</summary>
    public bool Hit(ActorHit hit) => false;
    public void MarkSourceBodyDead() => State.BodyFlags |= 0x40;
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) =>
        Messages.Enqueue(new ThugMessage(code, sender) { Vector0 = vector });
    public void DeleteAllMessages() => Messages.Cleanup(all: true);
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void MarkDead() => Lifecycle.Kill();
    private FixedVector3 Hook(ushort part)
    {
        for (int index = 0; index < hookPose.Length; index++) hookPose[index] = Model.GetSourcePartPose(index);
        return ActorHookPosition.Evaluate(Body.Position, SourceTransformTranslation, SourceTransformBasis,
            hookPose, new ActorHook(0, 0, 0, part));
    }
    private void OnSourceRemoved(GodotScriptActor actor) { if (ReferenceEquals(actor, this)) DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if (destroyed) return;
        destroyed = true;
        registry.ActorRemoved -= OnSourceRemoved;
        if (Shadow is { } shadow) { shadow.DestroySourceBit(); configuration.SourceShadowRemoved(shadow); }
        CinematicScript.ClearCommands();
        DeleteAllMessages();
    }
}
