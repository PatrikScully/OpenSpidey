using Godot;
using System.Buffers.Binary;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors.Switches;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.World;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Game.Import;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Actors;

/// <summary>Shared source ENV ownership. Switches resolve existing geometry; they never get a proxy model.</summary>
public partial class GodotSwitchEnvironment : Node3D
{
    private ImportedLevelWorld world = null!;
    private PsxAssetLibrary library = null!;
    private IReadOnlyList<DecodedTexture> textures = [];
    private readonly Dictionary<int, GodotSwitchEnvironmentItem> claimed = [];
    private FixedVector3[] sourcePositions = [];
    private readonly Dictionary<uint, int[]> buckets = [];
    public IReadOnlyCollection<GodotSwitchEnvironmentItem> Items => claimed.Values;
    public event Action<GodotSwitchEnvironmentItem>? ItemClaimed;
    public static GodotSwitchEnvironment Create(ImportedLevelWorld world, PsxAssetLibrary environmentLibrary, IReadOnlyList<DecodedTexture> textures, ReadOnlyMemory<byte> environmentBytes)
    {
        if (!environmentLibrary.AssetPath.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Switch ENV lookup requires the level's original geometry environment region.");
        if (!world.Sources.Any(source => source.AssetPath.Equals(environmentLibrary.AssetPath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Switch environment library is not owned by this imported level.");
        PsxScene scene = environmentLibrary.Scene;
        if (scene.Models.Count != scene.Instances.Count || scene.Instances.Where((instance, index) => instance.ModelIndex != index).Any())
            throw new NotSupportedException("Original ENV checksum lookup requires one same-index source instance per model.");
        ReadOnlySpan<byte> raw = environmentBytes.Span;
        if (raw.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(raw[8..]) != scene.Instances.Count ||
            raw.Length < checked(12 + scene.Instances.Count * 36))
            throw new InvalidDataException("Switch ENV raw instance table does not match its decoded original library.");
        var sourcePositions = new FixedVector3[scene.Instances.Count];
        for (int index = 0; index < sourcePositions.Length; index++)
        {
            int offset = 12 + index * 36;
            if (BinaryPrimitives.ReadUInt16LittleEndian(raw[(offset + 22)..]) != scene.Instances[index].ModelIndex)
                throw new InvalidDataException("Switch ENV raw model owner differs from its decoded instance.");
            sourcePositions[index] = new(BinaryPrimitives.ReadInt32LittleEndian(raw[(offset + 4)..]),
                BinaryPrimitives.ReadInt32LittleEndian(raw[(offset + 8)..]), BinaryPrimitives.ReadInt32LittleEndian(raw[(offset + 12)..]));
        }
        var result = new GodotSwitchEnvironment { Name = "SourceSwitchEnvironment", world = world, library = environmentLibrary, textures = textures, sourcePositions = sourcePositions };
        foreach (var bucket in scene.Models.Select((model, index) => (model.Checksum, index)).GroupBy(value =>
            value.Checksum is { } checksum ? checksum % 256 : throw new InvalidDataException("ENV model checksum table is missing.")))
        {
            int[] indices = bucket.Select(value => value.index).ToArray();
            if (indices.Length > 5) throw new InvalidDataException("Original ENV hash bucket exceeds its five-slot source bound.");
            result.buckets.Add(bucket.Key, indices);
        }
        return result;
    }
    public SwitchEnvironmentItem? Resolve(uint checksum)
    {
        if (!buckets.TryGetValue(checksum % 256, out int[]? indices)) return null;
        foreach (int index in indices)
        {
            if (library.Scene.Models[index].Checksum != checksum) continue;
            if (claimed.TryGetValue(index, out GodotSwitchEnvironmentItem? existing)) return existing.State;
            GodotSwitchEnvironmentItem item = GodotSwitchEnvironmentItem.Create(library, textures, index, sourcePositions[index]);
            try
            {
                world.ClaimObjectModel(library.AssetPath, index);
                AddChild(item); claimed.Add(index, item);
                ItemClaimed?.Invoke(item);
                return item.State;
            }
            catch { claimed.Remove(index); item.Free(); throw; }
        }
        return null;
    }
    public void MarkSwitchFaces(SwitchEnvironmentItem item)
    {
        if (item.Identity is not GodotSwitchEnvironmentItem native || !claimed.Values.Contains(native))
            throw new InvalidDataException("Switch face mutation references another ENV region.");
        native.MarkSwitchFaces();
    }
    public void HideModel(SwitchEnvironmentItem item)
    {
        if (item.Identity is not GodotSwitchEnvironmentItem native || !claimed.Values.Contains(native))
            throw new InvalidDataException("ENV model mutation references another source region.");
        native.HideModel();
    }
    public GodotSwitchEnvironmentItem? ResolveCollider(CollisionObject3D collider) =>
        claimed.Values.FirstOrDefault(item => item.Colliders.Any(body => ReferenceEquals(body, collider)));
    public void Synchronize() { foreach (GodotSwitchEnvironmentItem item in claimed.Values) item.Synchronize(); }
}

/// <summary>Actual source ENV instance geometry, triangles, transform, flags and PC RGB tint.</summary>
public partial class GodotSwitchEnvironmentItem : Node3D
{
    private PsxAssetLibrary library = null!;
    private PsxModel model = null!;
    private EnvironmentChunkModelState? sharedModel;
    private PsxMaterialLibrary? materials;
    private OwnedMaterialTranslucency? translucency;
    private readonly List<Shape3D> collisionShapes = [];
    private int appliedModelRevision = -1;
    private bool resourcesDisposed;
    private MeshInstance3D mesh = null!;
    private Node3D collision = null!;
    private readonly List<(ImportedCollisionBody Body, uint Layers)> bodies = [];
    private readonly List<(Godot.Collections.Array Arrays, Color[] Colors, Material Material)> surfaces = [];
    private ArrayMesh sourceMesh = null!;
    private ArrayMesh ownedMesh = null!;
    private (bool Enabled, uint Rgb)? lastTint;
    public SwitchEnvironmentItem State { get; private set; } = null!;
    public int SourceInstanceIndex { get; private set; }
    public int ModelIndex => SourceInstanceIndex;
    public ushort SourceModelFlags => sharedModel?.Flags ?? model.Flags;
    public ActorAngles SourceAngles => State.Angles;
    public EnvironmentChunkModelState? SharedSourceModel => sharedModel;
    public bool ResourcesDisposed => resourcesDisposed;
    public string SourceAsset => library.AssetPath;
    public IReadOnlyList<ImportedCollisionBody> Colliders => bodies.Select(value => value.Body).ToArray();
    public MeshInstance3D Mesh => mesh;
    internal static GodotSwitchEnvironmentItem Create(PsxAssetLibrary library, IReadOnlyList<DecodedTexture> textures, int index, FixedVector3 sourcePosition)
    {
        PsxInstance instance = library.Scene.Instances[index];
        var item = new GodotSwitchEnvironmentItem { Name = $"SwitchENV{index}", library = library, model = library.Scene.Models[index], SourceInstanceIndex = index };
        try
        {
            item.State = new(item, item.model.Checksum!.Value, sourcePosition)
                { ItemFlags = instance.Flags, Color = instance.Color,
                    Angles = new((short)instance.Angles.X, (short)instance.Angles.Y, (short)instance.Angles.Z) };
            foreach (uint checksum in item.model.Faces.Select(face => face.TextureChecksum).OfType<uint>().Distinct())
                if (!textures.Any(texture => texture.Checksum == checksum))
                    throw new InvalidDataException($"Original switch ENV texture {checksum:X8} is missing.");
            item.materials = new(textures);
            item.translucency = new(item.materials);
            // Source ruin faces can initially have every draw bit disabled. Their native owner remains valid until actual chunk reveal.
            item.sourceMesh = PsxGeometryBuilder.BuildModel(item.model, library.Scene.ColorTable, item.materials) ?? new ArrayMesh();
            item.ownedMesh = new ArrayMesh();
            for (int surface = 0; surface < item.sourceMesh.GetSurfaceCount(); surface++)
            {
                var arrays = item.sourceMesh.SurfaceGetArrays(surface);
                var material = item.sourceMesh.SurfaceGetMaterial(surface);
                item.surfaces.Add((arrays, arrays[(int)Godot.Mesh.ArrayType.Color].AsColorArray(), material));
                item.ownedMesh.AddSurfaceFromArrays(item.sourceMesh.SurfaceGetPrimitiveType(surface), arrays);
                item.ownedMesh.SurfaceSetMaterial(surface, material);
            }
            item.mesh = new MeshInstance3D { Name = "OriginalENVModel", Mesh = item.ownedMesh, Transform = PsxCoordinates.InstanceTransform(instance) };
            item.AddChild(item.mesh); item.BuildCollision(); item.Synchronize();
            return item;
        }
        catch { item.Free(); throw; }
    }
    public void MarkSwitchFaces()
    {
        if (sharedModel is not null)
        {
            foreach (IEnvironmentChunkFace face in sharedModel.Faces) face.CollisionWord |= 0x02000000u;
            Synchronize(); return;
        }
        PsxFace[] faces = model.Faces.Select(face => face.CollisionWord is { } word ? face with { CollisionWord = word | 0x02000000u }
            : throw new NotSupportedException("Switch face flag mutation requires the original fourth packet word.")).ToArray();
        model = model with { Faces = Array.AsReadOnly(faces) };
        BuildCollision(); Synchronize();
    }
    /// <summary>RenderSuperItem 475D61: a chunk-prepared model (DCModelData flag4000) draws through DC_PSXModel_RenderModel.</summary>
    internal float SourceTranslucencyAlpha => sharedModel is { } shared && (shared.RendererFlags & 0x4000) != 0
        ? SourceItemTranslucency.PsxModelAlpha : SourceItemTranslucency.ModelAlpha;
    internal void VerifySourceTranslucency(float? alpha) =>
        (translucency ?? throw new InvalidOperationException("Switch ENV translucency requires its imported materials.")).Verify(alpha, [mesh]);
    public void HideModel()
    {
        if (sharedModel is not null) sharedModel.Flags |= 0x20;
        else model = model with { Flags = (ushort)(model.Flags | 0x20) };
        Synchronize();
    }
    private void BuildCollision()
    {
        PsxModel[] models = library.Scene.Models.ToArray(); models[ModelIndex] = sharedModel?.CollisionSnapshot() ?? model;
        // Enable construction even for a source off-state item; original flags are restored before querying.
        PsxInstance instance = library.Scene.Instances[SourceInstanceIndex] with { Flags = 0, Position = default, Angles = default };
        Node3D replacement = PsxWorldCollisionBuilder.Build(new(library.Scene.Version, [instance], models, library.Scene.ColorTable), library.AssetPath);
        Shape3D[] shapes = replacement.GetChildren().OfType<CollisionObject3D>().SelectMany(body => body.GetChildren().OfType<CollisionShape3D>())
            .Select(child => child.Shape).OfType<Shape3D>().ToArray();
        try
        {
            if (replacement.GetChildren().OfType<ImportedTriggerSurface>().Any())
                throw new NotSupportedException("Switch ENV models with trigger-zone faces require their own source inquiry owner.");
            RetireCollision();
            bodies.Clear(); collision = replacement; collisionShapes.AddRange(shapes);
            AddChild(collision);
            foreach (ImportedCollisionBody body in collision.GetChildren().OfType<ImportedCollisionBody>())
            { body.InstanceIndex = SourceInstanceIndex; bodies.Add((body, body.CollisionLayer)); }
        }
        catch
        {
            if (!ReferenceEquals(collision, replacement))
            { foreach (Shape3D shape in shapes) shape.Dispose(); replacement.Free(); }
            throw;
        }
    }
    public void Synchronize()
    {
        ObjectDisposedException.ThrowIf(resourcesDisposed, this);
        if (sharedModel is not null && appliedModelRevision != sharedModel.Revision)
        { RebuildSharedDrawModel(); BuildCollision(); appliedModelRevision = sharedModel.Revision; }
        Transform3D transform = PsxCoordinates.CharacterTransform(OpenSpidey.Assets.Animation.FixedJointTransform.FromJoint(
            new(State.Angles.X, State.Angles.Y, State.Angles.Z, 0, 0, 0)));
        transform.Origin = PsxCoordinates.FixedWorldPosition(State.Position);
        mesh.Transform = transform;
        if (collision is not null) collision.Transform = transform;
        mesh.Visible = (State.ItemFlags & 0x1001) == 0 && (SourceModelFlags & 0x20) == 0;
        translucency?.Set(SourceItemTranslucency.Alpha(State.ItemFlags, SourceTranslucencyAlpha));
        foreach ((ImportedCollisionBody body, uint layers) in bodies)
        {
            body.SourceFlags = State.ItemFlags;
            body.SourceRotation = PsxCoordinates.OriginalRotation(transform.Basis);
            body.CollisionLayer = OriginalCollisionFilter.IsInstanceEnabled(State.ItemFlags) ? layers : 0;
        }
        bool tint = (State.ItemFlags & 0x480) == 0x400;
        if (lastTint == (tint, State.Color)) return;
        ownedMesh.ClearSurfaces();
        for (int index = 0; index < surfaces.Count; index++)
        {
            var surface = surfaces[index];
            Color[] colors = tint ? surface.Colors.Select(color => new Color(Channel(color.R, (byte)State.Color),
                Channel(color.G, (byte)(State.Color >> 8)), Channel(color.B, (byte)(State.Color >> 16)), color.A)).ToArray() : surface.Colors;
            surface.Arrays[(int)Godot.Mesh.ArrayType.Color] = colors;
            ownedMesh.AddSurfaceFromArrays(sourceMesh.SurfaceGetPrimitiveType(index), surface.Arrays);
            ownedMesh.SurfaceSetMaterial(index, surface.Material);
        }
        lastTint = (tint, State.Color);
    }
    private static float Channel(float original, byte channel)
    {
        // Same source ENV-to-CSuper PC vertex tint conversion as the actor renderer's 0.7 table.
        int originalByte = Math.Clamp((int)Math.Round(original * 255f), 0, 255);
        float powered = (float)Math.Pow(channel / 255f, .7f);
        int tint = (int)((double)Math.Min(1f, powered) * 255.0);
        return ((originalByte * tint) >> 8) / 255f;
    }
    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || resourcesDisposed) return;
        resourcesDisposed = true;
        if (mesh is not null) mesh.Mesh = null;
        foreach (var surface in surfaces) surface.Arrays.Dispose(); surfaces.Clear();
        sourceMesh?.Dispose(); ownedMesh?.Dispose();
        foreach (Shape3D shape in collisionShapes) shape.Dispose(); collisionShapes.Clear();
        translucency = null;
        materials?.Dispose(); materials = null;
    }

    /// <summary>Claims and chunk destruction share the original SModel/DC face state; copied switch aim points remain independent constructor snapshots.</summary>
    public void InstallSharedChunkModel(EnvironmentChunkModelState shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        ObjectDisposedException.ThrowIf(resourcesDisposed, this);
        if (sharedModel is not null && !ReferenceEquals(sharedModel, shared))
            throw new InvalidOperationException("A claimed ENV owner cannot alias a different source-bank model generation.");
        if (sharedModel is null)
        {
            PsxModel original = library.Scene.Models[ModelIndex];
            // These original switch leaves add hide20 and switch-face02000000 before a later chunk preparation may be requested.
            shared.Flags |= unchecked((ushort)(model.Flags & ~original.Flags));
            for (int index = 0; index < model.Faces.Count; index++)
            {
                uint added = (model.Faces[index].CollisionWord ?? 0) & ~(original.Faces[index].CollisionWord ?? 0);
                shared.Faces[index].CollisionWord |= added;
            }
            sharedModel = shared; appliedModelRevision = -1;
        }
        Synchronize();
    }
    private void RebuildSharedDrawModel()
    {
        ArrayMesh replacement = PsxGeometryBuilder.BuildModel(sharedModel!.RenderSnapshot(), library.Scene.ColorTable, materials!,
            sourceFacesPrepared: true) ?? new ArrayMesh();
        var replacementSurfaces = new List<(Godot.Collections.Array Arrays, Color[] Colors, Material Material)>();
        try
        {
            for (int index = 0; index < replacement.GetSurfaceCount(); index++)
            {
                Godot.Collections.Array arrays = replacement.SurfaceGetArrays(index);
                replacementSurfaces.Add((arrays, arrays[(int)Godot.Mesh.ArrayType.Color].AsColorArray(), replacement.SurfaceGetMaterial(index)));
            }
        }
        catch
        { foreach (var surface in replacementSurfaces) surface.Arrays.Dispose(); replacement.Dispose(); throw; }
        foreach (var surface in surfaces) surface.Arrays.Dispose(); surfaces.Clear();
        ownedMesh.ClearSurfaces(); sourceMesh.Dispose();
        sourceMesh = replacement; surfaces.AddRange(replacementSurfaces);
        lastTint = null;
    }
    internal int CachedSurfaceArrayCount => surfaces.Count;
    internal (PsxMaterialLibrary Materials, Resource[] Resources) CaptureOwnedResources()
    {
        ObjectDisposedException.ThrowIf(resourcesDisposed, this);
        var resources = new List<Resource> { sourceMesh, ownedMesh };
        resources.AddRange(collisionShapes);
        foreach (var surface in surfaces)
        {
            resources.Add(surface.Material);
            if (surface.Material is StandardMaterial3D { AlbedoTexture: { } texture }) resources.Add(texture);
        }
        return (materials!, resources.DistinctBy(resource => resource.GetInstanceId()).ToArray());
    }
    private void RetireCollision()
    {
        foreach (Shape3D shape in collisionShapes) shape.Dispose(); collisionShapes.Clear();
        if (collision is null) return;
        RemoveChild(collision); collision.Free(); collision = null!;
    }
}
