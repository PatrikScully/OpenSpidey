using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Choppers;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Effects;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.Actors;

/// <summary>CChopperMissile type321 on the source baddy list with model, smoke ribbon and homing AI.</summary>
public partial class GodotChopperMissileActor : GodotScriptActor, IChopperMissileHost, IGodotSourceBaddyBody
{
    private GodotActorRegistry registry = null!;
    private GodotChopperMissileConfiguration configuration = null!;
    private ActorFrameTiming frameTiming = new();
    private SmokeTrail? smoke;
    private MeshInstance3D? mesh;
    private bool begun;
    public ChopperMissileState State { get; private set; } = null!;
    public ActorBodyMotion Body { get; private set; } = null!;
    ImportedAnimatedModel IGodotSourceBaddyBody.Model => throw new NotSupportedException("CChopperMissile uses a static objects.psx mesh, not an animated actor model.");
    public ushort ItemFlags { get => State.ItemFlags; set => State.ItemFlags = value; }
    public ushort MinorRadius => 0;
    public ushort BodyFlags => unchecked((ushort)State.BodyFlags);
    public uint BehaviorFlags => 0;
    public short Health => 0;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => Body.Position;
    public TriggerFile Triggers => registry.Triggers;
    public int ElapsedTicks => frameTiming.ElapsedTicks;
    public int Difficulty => registry.Difficulty;

    internal static GodotChopperMissileActor Spawn(GodotChopperActor owner, uint waypoint, FixedVector3 spawnPosition,
        GodotActorRegistry registry, GodotChopperMissileConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(owner); ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        var actor = new GodotChopperMissileActor { Name = $"ChopperMissile{waypoint}", registry = registry, configuration = configuration };
        try
        {
            actor.State = ChopperMissileConstruction.Create(spawnPosition, (int)waypoint, phase: 0, registry.Difficulty, registry.Triggers);
            actor.Body = new() { Position = spawnPosition, Angles = owner.Body.Angles, UseLocalVelocity = true };
            actor.ScriptState = new() { ActorType = ChopperState.MissileType, NodeId = owner.SourceNodeId };
            actor.Variables = new(actor.ScriptState, registry) { Position = spawnPosition, ItemFlags = 0, CollisionRadius = 0 };
            actor.Lifecycle = new(distanceSuspension: false, startsSuspended: false);
            actor.Definition = owner.Definition with { ActorType = ChopperState.MissileType };
            actor.State.BodyFlags = 0x16;
            actor.State.BodyFlags |= 4;
            actor.CommonInitialise();
            registry.Adopt(actor);
            return actor;
        }
        catch { actor.Free(); throw; }
    }

    private void CommonInitialise()
    {
        State.ItemFlags &= 0xFFFD;
        State.BodyFlags &= 0xFFEF;
        mesh = configuration.CreateMesh();
        AddChild(mesh);
        smoke = configuration.EmitSmoke(State.Position);
        State.SoundHandle = configuration.PlayLaunchSound(ChopperState.MissileLaunchSound, State.Position);
    }

    internal override void Begin() { begun = true; SynchronizeTransform(); }
    internal override void Advance()
    {
        SourceUpdates++;
        State.BodyFlags = frameTiming.PrepareClock(configuration.Timer(), State.BodyFlags);
        ChopperMissileBrain.Advance(this);
        Body.Position = State.Position;
        Body.Angles = State.Angles;
        Variables.Position = State.Position;
        Variables.ItemFlags = State.ItemFlags;
        SynchronizeTransform();
        if (!State.Alive) Lifecycle.Kill();
    }

    public void SetSmokePosition(FixedVector3 position)
    {
        if (smoke is null) return;
        smoke.SetPosition(position);
        configuration.RebuildSmoke();
    }

    public void ModifySoundPosition(FixedVector3 position) => configuration.ModifySound(State.SoundHandle, position);

    public void SendPulse(ushort node) => registry.ReceivePulse(node);

    /// <summary>CChopperMissile::Explode (0x4252C0): player proximity hit, grenade wave, smoke fade and CBody::Die.</summary>
    public void Explode()
    {
        configuration.ApplyExplosionEffects(State.Position);
        if (smoke is { } trail)
        {
            trail.FadeAway = 1;
            configuration.RebuildSmoke();
        }
        smoke = null;
        State.Alive = false;
        MarkSourceBodyDead();
    }

    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) { }
    public bool Hit(ActorHit hit) => false;
    public void MarkSourceBodyDead() => State.BodyFlags |= 0x40;

    private void SynchronizeTransform()
    {
        if (mesh is null) return;
        Transform3D transform = PsxCoordinates.CharacterTransform(FixedJointTransform.FromJoint(new(Body.Angles.X, Body.Angles.Y, Body.Angles.Z, 0, 0, 0)));
        transform.Origin = PsxCoordinates.FixedWorldPosition(Body.Position);
        Transform = transform;
    }

    public override void _Process(double delta)
    {
        if (!begun || mesh is null) return;
        mesh.Visible = (State.ItemFlags & 0x8001) == 0;
    }
}

public sealed class GodotChopperMissileConfiguration
{
    public required Func<uint> Timer { get; init; }
    public required Func<MeshInstance3D> CreateMesh { get; init; }
    public required Func<FixedVector3, SmokeTrail> EmitSmoke { get; init; }
    public required Action RebuildSmoke { get; init; }
    public required Func<ushort, FixedVector3, uint> PlayLaunchSound { get; init; }
    public required Action<uint, FixedVector3> ModifySound { get; init; }
    public required Action<FixedVector3> ApplyExplosionEffects { get; init; }
}
