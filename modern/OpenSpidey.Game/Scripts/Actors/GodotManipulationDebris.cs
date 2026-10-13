using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

public sealed class GodotManipulationDebrisConfiguration
{
    public required Action<GodotManipulationDebris> AttachToBaddyList { get; init; }
    public required Action<GodotManipulationDebris> DetachFromBaddyList { get; init; }
    public required IManipulationDebrisHost WorldQueries { get; init; }
}

/// <summary>Original transient CManipObChunk BaddyList body, distinct from its parent environmental object.</summary>
public partial class GodotManipulationDebris : GodotScriptActor, IActorScriptVariableWorld, IGodotSourceActorVisibility
{
    private GodotActorRegistry registry = null!;
    private GodotManipulationDebrisConfiguration configuration = null!;
    private bool attached;
    private PsxAssetLibrary? modelLibrary;
    public ManipulationDebrisState State { get; private set; } = new();
    public ActorBodyMotion Body => State.Body;
    public override FixedVector3 SourcePosition => Body.Position;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Enemy;
    public ushort ItemFlags => State.ItemFlags;
    public PsxAssetLibrary? SourceVisibilityBank => modelLibrary;
    public ushort SourceVisibilityFlags => ItemFlags;
    public void SetSourceVisibilityFlags(ushort flags) { State.ItemFlags = flags; Synchronize(); }
    public ushort BodyFlags => State.BodyFlags;
    public ushort MinorRadius => 0;
    public uint SourceBehaviorFlags => 0;
    public MeshInstance3D SourceMesh { get; private set; } = null!;
    public int ModelIndex { get; private set; }

    public static GodotManipulationDebris Create(Func<ManipulationDebrisRequest> sourceFactory, GodotActorRegistry registry,
        GodotManipulationDebrisConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(sourceFactory); ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        var actor = new GodotManipulationDebris { Name = "OriginalManipulationChunk", registry = registry, configuration = configuration,
            ScriptState = new() { ActorType = 401, NodeId = ushort.MaxValue }, Lifecycle = new(true, false) };
        try
        {
            actor.Variables = new(actor.ScriptState, actor) { CollisionRadius = 0 };
            PsxAssetLibrary library = registry.ObjectLibrary();
            actor.modelLibrary = library;
            ManipulationDebrisRequest request = sourceFactory();
            configuration.AttachToBaddyList(actor); actor.attached = true;
            actor.State = ManipulationDebrisState.Create(request.ModelChecksum, request.Position, request.Velocity, registry.RandomSource);
            actor.ModelIndex = library.FindModel(actor.State.ModelChecksum);
            PsxModel model = library.Scene.Models[actor.ModelIndex];
            var textures = registry.ObjectTextures();
            foreach (uint checksum in model.Faces.Select(face => face.TextureChecksum).OfType<uint>().Distinct())
                if (!textures.Any(texture => texture.Checksum == checksum))
                    throw new InvalidDataException($"Original chunk OBJ texture{checksum:X8} is missing.");
            registry.ClaimObjectModel(library.AssetPath, actor.ModelIndex);
            actor.SourceMesh = new() { Name = "OriginalChunkOBJModel", Mesh = PsxGeometryBuilder.BuildModel(model,
                library.Scene.ColorTable, new PsxMaterialLibrary(textures)) ?? throw new InvalidDataException("Original chunk model has no source faces.") };
            actor.AddChild(actor.SourceMesh); actor.Synchronize(); return actor;
        }
        catch
        {
            if (actor.attached) { configuration.DetachFromBaddyList(actor); actor.attached = false; }
            actor.Free(); throw;
        }
    }
    internal override void Begin() => Synchronize();
    internal override void Advance() => AdvanceSource(2);
    public void AdvanceSource(int elapsedTicks)
    { SourceUpdates++; State.Advance(elapsedTicks, configuration.WorldQueries, registry.RandomSource); Synchronize(); }
    public bool Hit(ActorHit hit) => true; // Actual inherited CBody::Hit accepts without changing this body.
    private void Synchronize()
    {
        Variables.Position = Body.Position; Variables.ItemFlags = ItemFlags;
        Transform3D transform = PsxCoordinates.CharacterTransform(FixedJointTransform.FromJoint(new(Body.Angles.X, Body.Angles.Y, Body.Angles.Z, 0, 0, 0)));
        transform.Origin = PsxCoordinates.FixedWorldPosition(Body.Position); Transform = transform;
        Visible = (ItemFlags & 0x1001) == 0;
        if ((State.BodyFlags & 0x40) != 0) Lifecycle.Kill();
    }
    public int Difficulty => registry.Difficulty;
    public int MovieInputPolls => registry.MovieInputPolls;
    public FixedVector3? PlayerPosition => registry.PlayerPosition;
    public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => registry.HasLineOfSight(from, to);
    public int Random(int range) => registry.RandomSource(range);
    public ushort FirstLink(ushort node) => registry.FirstLink(node);
    public void SelectModel(uint checksum) => throw new NotSupportedException("CManipObChunk does not execute model-selection scripts.");
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete) return;
        if (!attached) return;
        configuration.DetachFromBaddyList(this); attached = false;
    }
}
