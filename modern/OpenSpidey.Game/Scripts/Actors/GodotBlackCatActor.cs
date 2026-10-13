using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Cinematics;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Thugs;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Effects;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Original source319 Black Cat actor, using cinematic bytecode, genuine models and mutable head links.</summary>
public partial class GodotBlackCatActor : GodotScriptActor, ICinematicBlackCatHost
{
    private GodotActorRegistry registry = null!;
    private GodotBlackCatConfiguration configuration = null!;
    private BlackCatBrain brain = null!;
    private FixedJointTransform[] hookPose = [];
    private bool begun;
    private bool destroyed;
    public BlackCatState State { get; private set; } = null!;
    public ActorBodyMotion Body { get; private set; } = null!;
    public CinematicActorScriptRuntime CinematicScript { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public OriginalBlackCatData SourceData { get; private set; } = null!;
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
    public int SurfaceClearance => State.SurfaceClearance;
    public int AnimationId => Model.ClipId;
    public int AnimationFrame => Model.Playback.Frame;
    public bool AnimationFinished => Model.Playback.Finished != 0;
    public bool FastForwardRequested => configuration.FastForwardRequested();
    public FixedVector3 PlayerPosition => registry.PlayerPosition ?? throw new InvalidOperationException("Black Cat AI requires the actual scene player.");

    public static GodotBlackCatActor Create(TriggerNode node, GodotActorRegistry registry, GodotBlackCatConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != BlackCatState.Type) throw new NotSupportedException("Black Cat factory requires source319.");
        int ordinal = registry.BaddyConstruction.RequireCurrentOrdinal();
        var actor = new GodotBlackCatActor { Name = $"BlackCat{node.Id}", registry = registry, configuration = configuration, Definition = definition };
        try
        {
            actor.State = new(node.Id, registry.Environment.LevelId, ordinal);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= ~2u;
            actor.Body = new() { Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z), AngularDamping = new(1, 1, 1) };
            actor.ScriptState = new() { ActorType = BlackCatState.Type, NodeId = node.Id };
            actor.Variables = new(actor.ScriptState, registry) { Position = actor.Body.Position, GroundOffset = actor.State.SurfaceClearance,
                CollisionRadius = 0, ItemFlags = 0x482, AnimationSpeed = 0x10000 };
            actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            actor.Model = configuration.ModelFactory(actor.State.ModelName);
            actor.Model.AdvanceAutomatically = false; actor.AddChild(actor.Model); actor.Model.RunClip(12);
            actor.hookPose = new FixedJointTransform[actor.Model.SourcePartCount];
            actor.SourceData = configuration.PoseData(actor.Model.SourcePartCount);
            actor.Lighting = new(actor.Model, actor.SourceData.Light, configuration.LightingState);
            actor.World = new(actor, registry.Triggers);
            actor.CinematicScript = new(new ActorScriptCursor(definition.BehaviorData,
                checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor, CinematicActorScriptProfile.BlackCat);
            actor.brain = new(actor.State, actor, actor.CinematicScript);
            actor.BuildTransform();
            actor.AddChild(new GodotNativeDestructionObserver { Name = "BlackCatDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            if (actor.FastForwardRequested && actor.State.LevelId != 0x803) actor.State.Die(actor);
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    internal override void Begin() { begun = true; BuildTransform(); }
    internal override void Advance()
    {
        SourceUpdates++;
        State.BodyFlags = FrameTiming.Prepare(configuration.Clock(), unchecked((ushort)State.BodyFlags), ItemFlags,
            new(Model.Playback.Frame, Model.Playback.ClipId, Model.Playback.Direction));
        if ((ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        brain.Advance(FrameTiming.ElapsedTicks);
        Variables.Position = Body.Position; ScriptState.BehaviorFlags = State.BehaviorFlags;
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    public override void _Process(double delta)
    {
        if (!begun || destroyed || registry.Player is null) return;
        ItemFlags = configuration.IsSourceCulled(this) ? (ushort)(ItemFlags | 0x8000) : (ushort)(ItemFlags & 0x7FFF);
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    public void BuildTransform()
    {
        ActorAngles angles = Body.Angles;
        SourceTransformBasis = FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0));
        SourceTransformTranslation = Body.Position >> 12;
        Transform3D transform = PsxCoordinates.CharacterTransform(SourceTransformBasis);
        transform.Origin = PsxCoordinates.FixedWorldPosition(SourceTransformTranslation << 12); Transform = transform;
    }
    /// <summary>Called by the source render phase in original actor-list order; AI never advances shared colour scratch.</summary>
    public void SynchronizeSourceLighting() => Lighting.Synchronize(SourceTransformBasis, ItemFlags);
    public FixedVector3 TriggerPosition(int node) => World.WaypointPosition(node);
    public IReadOnlyList<ushort> TriggerLinks(int node) => (uint)node < registry.Triggers.Nodes.Count
        ? MissionRuntime.ReadLinks(registry.Triggers.Nodes[node])
        : throw new InvalidDataException("Black Cat link lookup references a missing source node.");
    public int GroundHeight(FixedVector3 position, int above, int below, int flags)
    {
        if (flags != 0) throw new NotSupportedException("Black Cat source ground queries use flags0.");
        return World.GroundHeight(position, above, below);
    }
    public void RunAnimation(int animation) => Model.RunClip(animation);
    public void CycleAnimation(int animation) => Model.CycleClip(animation, 1);
    public void PlayVoice(short bank, short track, short priority) => registry.VoicePlayback(bank, track, priority);
    public void PlayPositionSound(uint sound, FixedVector3 position, int pitch)
    {
        if (pitch != 0) throw new NotSupportedException("Black Cat source SFX uses pitchoffset0.");
        (registry.Sounds ?? throw new InvalidOperationException("Black Cat source SFX requires the scene sound channels."))
            .PlayAt(sound, position, registry.SoundListener());
    }
    public bool ConsumeAnimationSignal(CinematicActorSignalSource source)
    {
        if (source != CinematicActorSignalSource.InputFlags) throw new NotSupportedException("Black Cat waits on source inputflags1 at CBody44.");
        if ((ScriptState.PendingSignals & 1) == 0) return false;
        ScriptState.PendingSignals &= 0xFFFE; return true;
    }
    public bool Hit(ActorHit hit) => false; // Original Black Cat vtable slot3 is the unconditional return0 leaf.
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) =>
        Messages.Enqueue(new ThugMessage(code, sender) { Vector0 = vector });
    public void DeleteAllMessages() => Messages.Cleanup(all: true);
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void MarkDead() => Lifecycle.Kill();

    public void UpdatePoseAndShadow(int lookTargetNode, bool scriptRunning)
    {
        AimAngles look = default;
        if (lookTargetNode != 0)
        {
            FixedVector3 hook = Hook(16);
            FixedVector3 local = FixedRotation.Transform(CinematicActorPose.Transpose(SourceTransformBasis),
                (TriggerPosition(lookTargetNode) - hook) >> 12) << 12;
            look = FixedAim.Calculate(default, local);
        }
        if (Model.HasSourceJoints)
        {
            RawJointPose head = Model.GetSourceJoint(3);
            AimAngles eased = CinematicActorPose.AdvanceHead(new(head.RotationX, head.RotationY), look);
            Model.SetSourceJoint(3, head with { RotationX = eased.Pitch, RotationY = eased.Yaw });
            if ((ItemFlags & 4) != 0 && scriptRunning && (eased.Pitch | eased.Yaw) == 0) ItemFlags &= 0xFFFB;
        }
        if ((ItemFlags & 4) != 0)
        {
            Model.InitializeSourceJoints(SourceData.PoseLinks);
            BuildTransform(); Model.ApplySourceJointPose(true);
        }
        else { BuildTransform(); if (Model.SourcePoseEnabled) Model.ApplySourceJointPose(false); }
        FixedVector3[] corners = CinematicActorPose.ShadowCorners(Body.Position, SourceTransformBasis,
            State.GroundY, new[] { Hook(3), Hook(6), Hook(13), Hook(9) });
        if (Shadow is null)
        {
            Shadow = GodotCinematicFootprintShadow.Create(configuration.ShadowTexture); AddChild(Shadow);
            GodotCinematicFootprintShadow captured = Shadow;
            configuration.SourceShadowCreated(new(captured, _ => { }, () => captured.IsAlive, captured.DestroySourceBit));
        }
        Shadow.SetCorners(corners);
    }
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
        destroyed = true; registry.ActorRemoved -= OnSourceRemoved;
        if (Shadow is { } shadow) { shadow.DestroySourceBit(); configuration.SourceShadowRemoved(shadow); }
        CinematicScript.ClearCommands();
        DeleteAllMessages();
    }
}
