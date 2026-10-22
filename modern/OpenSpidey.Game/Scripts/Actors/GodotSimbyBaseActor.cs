using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Simby;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Assets.Models;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>CSimbyBase (vtable 0x53C100) on the source baddy list: static "sym_base" mesh, AI 0x4ACF00, destructor 0x4ACEA0.</summary>
public partial class GodotSimbyBaseActor : GodotScriptActor, IGodotSourceBaddyBody, IGodotSourceActorVisibility
{
    private GodotActorRegistry registry = null!;
    private GodotScriptActor owner = null!;
    private MeshInstance3D mesh = null!;
    private PsxAssetLibrary modelLibrary = null!;
    private bool begun;
    public SimbyBaseState State { get; private set; } = null!;
    public ActorBodyMotion Body { get; } = new();
    ImportedAnimatedModel IGodotSourceBaddyBody.Model => throw new NotSupportedException("CSimbyBase uses the static sym_base mesh, not an animated actor model.");
    public ushort ItemFlags { get => State.ItemFlags; set => State.ItemFlags = value; }
    public ushort MinorRadius => 0;
    public ushort BodyFlags => State.BodyFlags;
    public uint BehaviorFlags => 0;
    public short Health => 0;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public override FixedVector3 SourcePosition => State.Position;
    public PsxAssetLibrary? SourceVisibilityBank => modelLibrary;
    public ushort SourceVisibilityFlags => State.ItemFlags;
    public void SetSourceVisibilityFlags(ushort flags) { State.ItemFlags = flags; Variables.ItemFlags = flags; mesh.Visible = (flags & 0x8001) == 0; }

    /// <summary>Inlined new CSimbyBase: DCMem_New zeroes the body, so its node and type fields stay 0; field_32C = Mem_MakeHandle(owner).</summary>
    internal static GodotSimbyBaseActor Spawn(GodotScriptActor owner, FixedVector3 position, ActorAngles angles,
        GodotActorRegistry registry, Func<GodotSimbyBaseModel> createMesh)
    {
        ArgumentNullException.ThrowIfNull(owner); ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(createMesh);
        var actor = new GodotSimbyBaseActor { Name = $"SimbyBase{owner.SourceNodeId}", registry = registry, owner = owner };
        try
        {
            actor.State = new SimbyBaseState(position, angles);
            actor.ScriptState = new() { ActorType = 0, NodeId = 0 };
            actor.Variables = new(actor.ScriptState, registry) { Position = position, ItemFlags = actor.State.ItemFlags, CollisionRadius = 0 };
            actor.Lifecycle = new(distanceSuspension: false, startsSuspended: false);
            actor.Definition = owner.Definition with { ActorType = 0 };
            GodotSimbyBaseModel model = createMesh();
            actor.mesh = model.Mesh;
            actor.modelLibrary = model.Library;
            actor.AddChild(actor.mesh);
            actor.SynchronizeTransform();
            registry.Adopt(actor);
            return actor;
        }
        catch { if (GodotObject.IsInstanceValid(actor)) actor.Free(); throw; }
    }

    internal override void Begin() { begun = true; SynchronizeTransform(); }
    internal override void Advance()
    {
        SourceUpdates++;
        SimbyBaseBrain.Advance(State, () => registry.Owns(owner));
        Variables.Position = State.Position;
        Variables.ItemFlags = State.ItemFlags;
        SynchronizeTransform();
        if (State.Dead) Lifecycle.Kill();
    }

    /// <summary>delete pBase (0x4ACE80): unlink from the baddy list and free it now.</summary>
    public void Delete()
    {
        if (!registry.Owns(this)) throw new InvalidOperationException("CSimby deleted a sym_base that the baddy list no longer owns.");
        registry.DestroyImmediately(this);
    }

    public void ReceiveMessage(int code, object? sender, FixedVector3 vector = default) { }
    /// <summary>Inherited CBody::Hit (0x460710): accepts without changing this body.</summary>
    public bool Hit(ActorHit hit) => true;
    public void MarkSourceBodyDead() => State.BodyFlags |= 0x40;

    /// <summary>CItem transform rebuild (0x475F50): angle matrix, scaled by 0x28..0x2C while ItemFlags bit 0x200 is set.</summary>
    private void SynchronizeTransform()
    {
        ActorAngles angles = State.Angles;
        Transform3D transform = PsxCoordinates.CharacterTransform(FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0)));
        if ((State.ItemFlags & 0x200) != 0)
            transform.Basis = transform.Basis * Basis.FromScale(new Vector3(State.ScaleX, State.ScaleY, State.ScaleZ) / 4096f);
        transform.Origin = PsxCoordinates.FixedWorldPosition((State.Position >> 12) << 12);
        Transform = transform;
    }

    public override void _Process(double delta)
    {
        if (!begun) return;
        mesh.Visible = (State.ItemFlags & 0x8001) == 0;
    }
}
