using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Choppers;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>CChopper type318 with story-command fields; flight AI remains unported.</summary>
public partial class GodotChopperActor : GodotScriptActor, IMissionChopper, IGodotSourceBaddyBody, IActorScriptHost
{
    private GodotActorRegistry registry = null!;
    private GodotChopperConfiguration configuration = null!;
    private bool begun;
    private bool destroyed;
    public ActorFrameTiming FrameTiming { get; } = new();
    public ActorBodyMotion Body { get; private set; } = null!;
    public ImportedAnimatedModel Model { get; private set; } = null!;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public ushort ItemFlags { get => Variables.ItemFlags; set => Variables.ItemFlags = value; }
    public ushort MinorRadius { get; private set; } = 200;
    public ushort BodyFlags { get; private set; }
    public uint BehaviorFlags => ScriptState.BehaviorFlags;
    public short Health { get; private set; } = 100;
    public int Flags218 { get; set; }
    public int Waypoint1F4 { get; set; }
    public int Velocity348 { get; set; }
    public int MinimumHeight350 { get; set; }
    public int TargetAngle358 { get; set; }
    /// <summary>CChopper::field_380; written by SetFlag when selector is zero.</summary>
    public bool Flag380 { get; private set; } = true;
    /// <summary>CChopper::field_3B4; cycles 2..9 for M3dUtils_GetHookPosition missile launches.</summary>
    public int MissileLaunchIndex { get; private set; } = 2;
    private FixedVector3 sourceTransformTranslation;
    private FixedJointTransform sourceTransformBasis;

    public static GodotChopperActor Create(TriggerNode node, GodotActorRegistry registry, GodotChopperConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType != ChopperState.ChopperType)
            throw new NotSupportedException("Chopper factory user0 requires type318.");
        var actor = new GodotChopperActor { Name = $"Chopper{node.Id}", registry = registry, configuration = configuration, Definition = definition };
        try
        {
            actor.Body = new()
            {
                Position = new FixedVector3(definition.Position.X, definition.Position.Y, definition.Position.Z) << 12,
                Angles = new(definition.Orientation.X, definition.Orientation.Y, definition.Orientation.Z), UseLocalVelocity = true
            };
            actor.ScriptState = new() { ActorType = definition.ActorType, NodeId = node.Id, BehaviorFlags = 1 };
            actor.Variables = new(actor.ScriptState, registry) { Position = actor.Body.Position, Health = actor.Health,
                CollisionRadius = actor.MinorRadius, ItemFlags = 2, AnimationSpeed = 0x10000 };
            actor.Lifecycle = new(!definition.ClearsBodyFlag2, false);
            actor.Runtime = new(new ActorScriptCursor(definition.BehaviorData, checked(node.SourceOffset + definition.ObjectDataOffset + 18)), actor);
            actor.Model = configuration.ModelFactory();
            actor.Model.AdvanceAutomatically = false; actor.AddChild(actor.Model);
            actor.AddChild(new GodotNativeDestructionObserver { Name = "SourceChopperDestructor", Destroyed = actor.DestroySourceOwnedState });
            registry.ActorRemoved += actor.OnSourceRemoved;
            actor.BuildTransform();
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    internal override void Begin() { Runtime.Start(registry.Environment); begun = true; BuildTransform(); }
    internal override void Advance()
    {
        SourceUpdates++;
        if ((ItemFlags & 2) != 0) FrameTiming.RecordAnimationElapsed(Model.AdvanceSourceTicks(FrameTiming.ElapsedTicks));
        SyncScriptStorage();
    }
    public override void _Process(double delta)
    {
        if (!begun || destroyed || configuration.PlayerProvider() is null) return;
        ItemFlags = configuration.IsSourceCulled(this) ? (ushort)(ItemFlags | 0x8000) : (ushort)(ItemFlags & 0x7FFF);
        Model.Visible = (ItemFlags & 0x8001) == 0;
    }
    public void BuildTransform()
    {
        Transform3D transform = PsxCoordinates.CharacterTransform(FixedJointTransform.FromJoint(new(Body.Angles.X, Body.Angles.Y, Body.Angles.Z, 0, 0, 0)));
        transform.Origin = PsxCoordinates.FixedWorldPosition(Body.Position);
        Transform = transform;
        sourceTransformTranslation = Body.Position >> 12;
        sourceTransformBasis = FixedJointTransform.FromJoint(new(Body.Angles.X, Body.Angles.Y, Body.Angles.Z, 0, 0, 0));
    }
    public void FireMissileAtWaypoint(uint waypoint)
    {
        FixedVector3 spawn = GetHookPosition(MissileLaunchIndex);
        MissileLaunchIndex = ChopperMissileConstruction.AdvanceMissileLaunchIndex(MissileLaunchIndex);
        GodotChopperMissileActor.Spawn(this, waypoint, spawn, registry, configuration.Missiles);
    }
    public FixedVector3 GetHookPosition(int index)
    {
        ActorHook hook = OriginalChopperModelPackets.Hooks.Hooks[index];
        int parts = Model.SourcePartCount;
        if (hook.PartIndex >= parts) throw new InvalidDataException($"Chopper hook {index} references missing part {hook.PartIndex}.");
        var pose = new FixedJointTransform[parts];
        for (int part = 0; part < parts; part++) pose[part] = Model.GetSourcePartPose(part);
        return ActorHookPosition.Evaluate(Body.Position, sourceTransformTranslation, sourceTransformBasis, pose, hook);
    }
    public void FireMachineGunAtWaypoint(uint waypoint, uint count) =>
        throw new NotSupportedException("CChopper::FireMachineGunAtWaypoint is not ported to Godot combat yet.");
    public void SetFlag(ushort flag, short value)
    {
        if (flag != 0) throw new InvalidOperationException("CChopper::SetFlag (0x425990) only accepts flag selector 0.");
        Flag380 = value != 0;
    }
    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) { }
    public bool Hit(ActorHit hit) { Health = (short)Math.Max(0, Health - hit.Damage); SyncScriptStorage(); return true; }
    public void MarkSourceBodyDead() => BodyFlags |= 0x40;
    public void MarkDead() => Lifecycle.Kill();
    public bool ExecuteCommand(ushort opcode, ActorScriptCursor operands, ActorScriptEnvironment environment)
    {
        if (GodotLinkedBaddyCommands.TryExecute(registry, SourceNodeId, opcode, operands)) return true;
        throw new NotSupportedException($"Chopper {Definition.NodeId}: source script command 0x{opcode:X4} is not connected.");
    }
    public short ReadVariable(ushort variable, ActorScriptCursor operands) { SyncScriptStorage(); return Variables.Read(variable, operands); }
    public void WriteVariable(ushort variable, ActorScriptCursor operands)
    {
        SyncScriptStorage(); Variables.Write(variable, operands);
        Body.Position = Variables.Position; Health = Variables.Health; MinorRadius = Variables.CollisionRadius;
    }
    public ActorScriptCursor ScriptAtNode(ushort nodeId) => registry.ScriptAtNode(nodeId);
    public bool PlayerCeilingCheck(short x, short z) => registry.PlayerCeilingCheck(x, z);
    public void FlashCompass() => registry.FlashCompass();
    public void SendDeathPulse() { foreach (ushort link in Definition.Links) registry.ReceivePulse(link); }
    public void Retire() { SendDeathPulse(); MarkSourceBodyDead(); ItemFlags |= 1; MarkDead(); }
    private void SyncScriptStorage()
    {
        Variables.Position = Body.Position; Variables.Health = Health; Variables.CollisionRadius = MinorRadius;
    }
    private void OnSourceRemoved(GodotScriptActor actor) { if (ReferenceEquals(actor, this)) DestroySourceOwnedState(); }
    private void DestroySourceOwnedState()
    {
        if (destroyed) return;
        destroyed = true;
        registry.ActorRemoved -= OnSourceRemoved;
    }
}
