using Godot;
using OpenSpidey.Assets.Animation;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Scripting;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Actors.Manipulation;
using OpenSpidey.Core.Actors.Scripting;
using OpenSpidey.Core.Actors.Switches;
using OpenSpidey.Core.Combat;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Player;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

public sealed class GodotManipulationConfiguration
{
    public required GodotSwitchEnvironment Environment { get; init; }
    public required Action<GodotManipulationActor> AttachToEnvironmentalList { get; init; }
    public required Action<GodotManipulationActor> DetachFromEnvironmentalList { get; init; }
    public required Func<IReadOnlyList<ManipulationTargetBody>> SourceBaddyOrder { get; init; }
    public required Func<bool> IgnoreSphereRadius { get; init; }
    public required Func<FixedVector3, FixedVector3, int, ManipulationWorldHit?> LineToItem { get; init; }
    public required Action<object, ActorHit> HitBaddy { get; init; }
    public required Action<FixedVector3, int> ShakeCamera { get; init; }
    public required IManipulationImpactHost ImpactEffects { get; init; }
    public GodotLevelOneBombConfiguration? LevelOneBomb { get; init; }
}

/// <summary>Original type401 CManipOb with its OBJ model, owned collision and source environment-list lifetime.</summary>
public partial class GodotManipulationActor : GodotScriptActor, IGodotEnvironmentalActor, IManipulationHost, IActorScriptVariableWorld, IGodotSourceActorVisibility
{
    private GodotActorRegistry registry = null!;
    private GodotManipulationConfiguration configuration = null!;
    private bool attached;
    private PsxAssetLibrary? modelLibrary;
    private MeshInstance3D mesh = null!;
    private Node3D collision = null!;
    private readonly List<ImportedCollisionBody> colliders = [];
    private readonly List<Shape3D> collisionShapes = [];
    private PsxMaterialLibrary? modelMaterials;
    private OwnedMaterialTranslucency? translucency;
    private ArrayMesh? modelMesh;
    private bool resourcesDisposed;
    public ManipulationState State { get; private set; } = null!;
    public GodotLevelOneBomb? LevelOneBomb { get; private set; }
    public ActorBodyMotion Body => State.Body;
    public ActorBodyMotion SupportMotion => Body;
    public override FixedVector3 SourcePosition => Body.Position;
    public int ModelIndex { get; private set; } = -1;
    public PsxAssetLibrary? SourceVisibilityBank => modelLibrary;
    public ushort SourceVisibilityFlags => State.ItemFlags;
    public void SetSourceVisibilityFlags(ushort flags) { State.ItemFlags = flags; Synchronize(); }
    public IReadOnlyList<ImportedCollisionBody> Colliders => colliders;
    public MeshInstance3D SourceMesh => mesh;
    public bool ResourcesDisposed => resourcesDisposed;
    public override ActorUpdateGroup UpdateGroup => ActorUpdateGroup.Environment;
    public PlayerKickObject KickObject => new(this, 401, State.Body.Position, State.Flags);

    public static GodotManipulationActor Create(TriggerNode node, GodotActorRegistry registry, GodotManipulationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(registry); ArgumentNullException.ThrowIfNull(configuration);
        TriggerActorDefinition definition = TriggerActorDefinition.Read(node);
        if (definition.ActorType is not 401 and not 600) throw new InvalidDataException("Manipulation factory requires original401 or its600 bomb specialization.");
        TriggerActorDefinition baseDefinition = definition.ActorType == 600
            ? (configuration.LevelOneBomb ?? throw new InvalidOperationException("Original bomb factory needs its genuine scene timer/ground/audio/link services."))
                .BaseDefinition(node, definition) : definition;
        var actor = new GodotManipulationActor { Name = $"{(definition.ActorType == 600 ? "LevelOneBomb" : "Manipulation")}{node.Id}", Definition = definition,
            registry = registry, configuration = configuration, State = new(node.Id, new()),
            ScriptState = new() { ActorType = 401, NodeId = node.Id }, Lifecycle = new(!definition.ClearsBodyFlag2, false) };
        try
        {
            actor.Variables = new(actor.ScriptState, actor) { CollisionRadius = 0 };
            ManipulationBehavior.Construct(actor.State, baseDefinition, node.SourceOffset, actor);
            if (definition.ActorType == 600) actor.LevelOneBomb = new(actor, registry, configuration.LevelOneBomb!);
            if (definition.ClearsBodyFlag2) actor.State.BodyFlags &= unchecked((ushort)~2);
            actor.Synchronize(); return actor;
        }
        catch
        {
            actor.LevelOneBomb?.Dispose();
            if (actor.attached) { configuration.DetachFromEnvironmentalList(actor); actor.attached = false; }
            actor.Free(); throw;
        }
    }
    internal override void Begin() => Synchronize();
    internal override void Advance() => AdvanceSource(LevelOneBomb?.PrepareElapsedTicks() ?? 2);
    public void AdvanceSource(int elapsedTicks)
    {
        SourceUpdates++;
        if (LevelOneBomb is { } bomb) bomb.Advance(elapsedTicks);
        else ManipulationBehavior.Advance(State, elapsedTicks, this, registry.RandomSource);
        Synchronize();
    }
    public void Pickup() { ManipulationBehavior.Pickup(State, this); Synchronize(); }
    public void Drop(FixedVector3 velocity) { ManipulationBehavior.Drop(State, velocity, registry.RandomSource); Synchronize(); }
    public void ThrowVelocity(FixedVector3 velocity) { ManipulationBehavior.Throw(State, velocity, registry.RandomSource); Synchronize(); }
    public void ThrowAt(FixedVector3 target, int count) { ManipulationBehavior.ThrowAt(State, target, count, registry.RandomSource); Synchronize(); }
    public bool TugImpulse(FixedVector3 point, FixedVector3 velocity)
    { bool result = ManipulationBehavior.TugImpulse(State, point, velocity, registry.RandomSource); Synchronize(); return result; }
    public FixedVector3 GetAttachPoint() => ManipulationBehavior.GetAttachPoint(State);
    public void SetHeldTransform(FixedVector3 position, ActorAngles angles) { Body.Position = position; Body.Angles = angles; Synchronize(); }
    public void Smash()
    { if (LevelOneBomb is { } bomb) bomb.Smash(); else ManipulationBehavior.Smash(State, this); Synchronize(); }
    public void Die()
    { if (LevelOneBomb is { } bomb) bomb.Die(); else State.BodyFlags |= 0x40; Synchronize(); }
    public bool Hit(ActorHit hit) => ManipulationBehavior.Hit();

    private void Synchronize()
    {
        Variables.Position = Body.Position; Variables.ItemFlags = State.ItemFlags;
        FixedJointTransform rotation = FixedJointTransform.FromJoint(new(Body.Angles.X, Body.Angles.Y, Body.Angles.Z, 0, 0, 0));
        Transform3D transform = PsxCoordinates.CharacterTransform(rotation);
        transform.Origin = PsxCoordinates.FixedWorldPosition(Body.Position); Transform = transform;
        Visible = (State.ItemFlags & 0x1001) == 0;
        translucency?.Set(SourceItemTranslucency.Alpha(State.ItemFlags));
        foreach (ImportedCollisionBody collider in colliders)
        {
            collider.SourceRotation = rotation; collider.SourceFlags = State.ItemFlags; collider.SupportId = GetInstanceId();
            foreach (Node child in collider.GetChildren())
                if (child is CollisionShape3D shape) shape.Disabled = (State.ItemFlags & 0x21) != 0;
        }
        if ((State.BodyFlags & 0x40) != 0) Lifecycle.Kill();
    }
    internal void VerifySourceTranslucency(float? alpha) =>
        (translucency ?? throw new InvalidOperationException("Manipulation translucency requires its selected OBJ model.")).Verify(alpha, [mesh]);
    public void InitializeObjectLibrary() => _ = registry.ObjectLibrary();
    public void AttachToEnvironmentalList(ManipulationState state)
    {
        if (!ReferenceEquals(state, State)) throw new InvalidDataException("Manipulation list attachment references another source body.");
        configuration.AttachToEnvironmentalList(this); attached = true;
    }
    public void SelectObjectModel(uint checksum)
    {
        if (modelLibrary is not null) throw new InvalidOperationException("Original CManipOb selects its OBJ model only during construction.");
        PsxAssetLibrary library = registry.ObjectLibrary(); ModelIndex = library.FindModel(checksum);
        modelLibrary = library;
        PsxModel model = library.Scene.Models[ModelIndex];
        IReadOnlyList<OpenSpidey.Assets.Textures.DecodedTexture> textures = registry.ObjectTextures();
        foreach (uint texture in model.Faces.Select(face => face.TextureChecksum).OfType<uint>().Distinct())
            if (!textures.Any(candidate => candidate.Checksum == texture))
                throw new InvalidDataException($"Manipulation OBJ model texture{texture:X8} is missing.");
        modelMaterials = new PsxMaterialLibrary(textures);
        translucency = new(modelMaterials);
        modelMesh = PsxGeometryBuilder.BuildModel(model, library.Scene.ColorTable, modelMaterials)
            ?? throw new InvalidDataException("Manipulation source OBJ model has no drawable faces.");
        mesh = new() { Name = "OriginalOBJModel", Mesh = modelMesh };
        AddChild(mesh);
        var scene = new PsxScene(library.Scene.Version,
            [new(0, System.Numerics.Vector3.Zero, System.Numerics.Vector3.Zero, (ushort)ModelIndex, 0)], library.Scene.Models, library.Scene.ColorTable);
        collision = PsxWorldCollisionBuilder.Build(scene, library.AssetPath); AddChild(collision);
        collisionShapes.AddRange(collision.GetChildren().OfType<CollisionObject3D>().SelectMany(body => body.GetChildren().OfType<CollisionShape3D>())
            .Select(child => child.Shape).OfType<Shape3D>());
        foreach (Node child in collision.GetChildren())
        {
            if (child is ImportedTriggerSurface) throw new NotSupportedException("Manipulation OBJ trigger-zone faces require an original inquiry owner.");
            if (child is ImportedCollisionBody body) colliders.Add(body);
        }
        registry.ClaimObjectModel(library.AssetPath, ModelIndex);
    }
    public SwitchEnvironmentItem? FindEnvironmentItem(uint checksum) => configuration.Environment.Resolve(checksum);
    public void HideEnvironmentModel(SwitchEnvironmentItem item) => configuration.Environment.HideModel(item);
    public void SendAttachedPulse(ushort node)
    {
        if (node != Definition.NodeId) throw new InvalidDataException("Manipulation pulse lost its source node owner.");
        foreach (ushort linked in Definition.Links) registry.ReceivePulse(linked);
    }
    public IReadOnlyList<ManipulationTargetBody> SourceBaddyOrder => configuration.SourceBaddyOrder();
    public bool IgnoreSphereRadius => configuration.IgnoreSphereRadius();
    public ManipulationWorldHit? LineToItem(FixedVector3 start, FixedVector3 end, int mode) => configuration.LineToItem(start, end, mode);
    public void HitBaddy(object identity, ActorHit hit) => configuration.HitBaddy(identity, hit);
    public void ShakeCamera(FixedVector3 position, int magnitude) => configuration.ShakeCamera(position, magnitude);
    public void Chunk(ManipulationState state, FixedVector3 normal) => ManipulationChunk.Create(state, normal, configuration.ImpactEffects, registry.RandomSource);
    public int Difficulty => registry.Difficulty;
    public int MovieInputPolls => registry.MovieInputPolls;
    public FixedVector3? PlayerPosition => registry.PlayerPosition;
    public bool HasLineOfSight(FixedVector3 from, FixedVector3 to) => registry.HasLineOfSight(from, to);
    public int Random(int range) => registry.RandomSource(range);
    public ushort FirstLink(ushort node) => registry.FirstLink(node);
    public void SelectModel(uint checksum) => throw new NotSupportedException("Original CManipOb does not execute model-selection scripts.");
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || resourcesDisposed) return;
        resourcesDisposed = true;
        try
        {
            LevelOneBomb?.Dispose();
            if (attached) { configuration.DetachFromEnvironmentalList(this); attached = false; }
            if (State is not null) State.ThrowPositions = null;
        }
        finally
        {
            if (mesh is not null) mesh.Mesh = null;
            modelMesh?.Dispose(); modelMesh = null;
            foreach (Shape3D shape in collisionShapes) shape.Dispose(); collisionShapes.Clear();
            translucency = null;
            modelMaterials?.Dispose(); modelMaterials = null;
            colliders.Clear();
        }
    }
    internal (PsxMaterialLibrary Materials, Resource[] Resources) CaptureOwnedResources()
    {
        ObjectDisposedException.ThrowIf(resourcesDisposed, this);
        var resources = new List<Resource> { modelMesh! };
        resources.AddRange(collisionShapes);
        for (int index = 0; index < modelMesh!.GetSurfaceCount(); index++)
        {
            Material material = modelMesh.SurfaceGetMaterial(index); resources.Add(material);
            if (material is StandardMaterial3D { AlbedoTexture: { } texture }) resources.Add(texture);
        }
        return (modelMaterials!, resources.DistinctBy(resource => resource.GetInstanceId()).ToArray());
    }
}
