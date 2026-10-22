using Godot;
using OpenSpidey.Core.Actors;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.Missions;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

/// <summary>Mutable flags on the actual current G-region items and their imported native geometry.</summary>
public sealed partial class GodotEnvironmentVisibility : IEnvironmentVisibilityHost
{
    private readonly Func<ImportedLevelWorld> currentWorld;
    private readonly PsxLibraryRepository repository;
    private readonly Func<ImportedLevelSource, int, GodotSwitchEnvironmentItem?> claimedItem;
    private ImportedLevelSource? source;
    private PsxAssetLibrary? bank;
    private GodotEnvironmentVisibilityItem[] items = [];

    public GodotEnvironmentVisibility(Func<ImportedLevelWorld> currentWorld, PsxLibraryRepository repository,
        Func<ImportedLevelSource, int, GodotSwitchEnvironmentItem?>? claimedItem = null)
    {
        this.currentWorld = currentWorld ?? throw new ArgumentNullException(nameof(currentWorld));
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
        this.claimedItem = claimedItem ?? ((_, _) => null);
    }

    public IReadOnlyList<GodotEnvironmentVisibilityItem> SourceItems
    {
        get { SynchronizeSource(); return items; }
    }

    public void SetVisibility(uint checksum, bool visible)
    {
        SynchronizeSource();
        // Spool_FindEnviroItem returns the first same-checksum ENV record in loader order.
        GodotEnvironmentVisibilityItem? item = items.FirstOrDefault(item => item.ModelChecksum == checksum);
        if (item is not null) SetItemVisibility(item.Source, item.InstanceIndex, visible);
    }

    public ushort SourceItemFlags(ImportedLevelSource source, int instanceIndex) =>
        RequireItem(source, instanceIndex).ItemFlags;

    public void SetItemVisibility(ImportedLevelSource source, int instanceIndex, bool visible)
    {
        GodotEnvironmentVisibilityItem item = RequireItem(source, instanceIndex);
        ushort flags = EnvironmentVisibilityPolicy.SetVisible(item.ItemFlags, visible);
        if (claimedItem(source, instanceIndex) is { } claimed)
        {
            claimed.State.ItemFlags = flags;
            claimed.Synchronize();
        }
        else SynchronizeNative(item, flags);
        item.StoredFlags = flags;
    }

    /// <summary>A whole CItem flag word write, rendered by the item's current native owner.</summary>
    public void WriteSourceItemFlags(GodotEnvironmentVisibilityItem item, ushort flags)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!ReferenceEquals(RequireItem(item.Source, item.InstanceIndex), item))
            throw new InvalidOperationException("ENV flag write references a previous source item generation.");
        if (claimedItem(item.Source, item.InstanceIndex) is { } claimed)
        {
            claimed.State.ItemFlags = flags;
            claimed.Synchronize();
        }
        else SynchronizeNative(item, flags);
        item.StoredFlags = flags;
    }

    internal ImportedSceneMesh? NativeMesh(GodotEnvironmentVisibilityItem item)
    {
        RequireItem(item.Source, item.InstanceIndex);
        string groupName = System.IO.Path.GetFileNameWithoutExtension(item.Source.AssetPath);
        Node3D geometry = currentWorld().Geometry.GetChildren().OfType<Node3D>().Single(group => group.Name.ToString().Equals(groupName, StringComparison.OrdinalIgnoreCase));
        return geometry.GetChildren().OfType<ImportedSceneMesh>().SingleOrDefault(mesh => mesh.SourceInstanceIndex == item.InstanceIndex);
    }

    public bool IsCurrentSourceItem(GodotEnvironmentVisibilityItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        SynchronizeSource();
        return ReferenceEquals(item.Source, source) && (uint)item.InstanceIndex < items.Length && ReferenceEquals(items[item.InstanceIndex], item);
    }

    /// <summary>Preserves source flags when a real switch transfers this static model into its own native owner.</summary>
    public void TransferFlagsToClaimedItem(GodotSwitchEnvironmentItem claimed)
    {
        SynchronizeSource();
        GodotEnvironmentVisibilityItem item = items.Single(item =>
            item.Source.AssetPath.Equals(claimed.SourceAsset, StringComparison.OrdinalIgnoreCase) &&
            item.InstanceIndex == claimed.SourceInstanceIndex);
        claimed.State.ItemFlags = item.StoredFlags;
        if (item.ChunkModel is { } model)
        {
            claimed.State.Position = item.StoredPosition;
            claimed.State.Angles = item.StoredAngles;
            claimed.InstallSharedChunkModel(model);
        }
        claimed.Synchronize();
    }

    internal ushort ReadFlags(GodotEnvironmentVisibilityItem item)
    {
        if (!ReferenceEquals(RequireItem(item.Source, item.InstanceIndex), item))
            throw new InvalidOperationException("ENV visibility item no longer belongs to the current generation.");
        return claimedItem(item.Source, item.InstanceIndex)?.State.ItemFlags ?? item.StoredFlags;
    }

    internal bool IsRegionUsable(GodotEnvironmentVisibilityItem item) =>
        repository.Loaded.TryGetValue(item.Source.AssetPath, out PsxAssetLibrary? live) &&
        ReferenceEquals(live.Scene, item.Source.Scene) && currentWorld().Sources.Any(source => ReferenceEquals(source, item.Source));

    internal FixedVector3 ReadPosition(GodotEnvironmentVisibilityItem item)
    {
        RequireItem(item.Source, item.InstanceIndex);
        if (claimedItem(item.Source, item.InstanceIndex) is { } claimed) return claimed.State.Position;
        return item.StoredPosition;
    }

    private GodotEnvironmentVisibilityItem RequireItem(ImportedLevelSource expected, int index)
    {
        SynchronizeSource();
        if (!ReferenceEquals(expected, source) || (uint)index >= items.Length)
            throw new InvalidOperationException("ENV visibility references an unloaded or different source item generation.");
        return items[index];
    }

    private void SynchronizeSource()
    {
        ImportedLevelSource? next = currentWorld().Sources.SingleOrDefault(source =>
            source.AssetPath.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase));
        if (next is null) { source = null; bank = null; items = []; return; }
        PsxAssetLibrary nextBank = repository.RequireLoaded(next.AssetPath);
        if (!ReferenceEquals(next.Scene, nextBank.Scene))
            throw new InvalidOperationException("ENV visibility requires the actual loaded imported bank generation.");
        if (ReferenceEquals(source, next)) return;
        PsxScene scene = next.Scene;
        if (scene.Instances.Any(instance => (uint)instance.ModelIndex >= scene.Models.Count))
            throw new InvalidDataException("ENV instance references a missing model.");
        if (scene.Models.Any(model => model.Checksum is null)) throw new InvalidDataException("ENV source checksum table is missing.");
        if (scene.Models.GroupBy(model => model.Checksum!.Value % 256).Any(bucket => bucket.Count() > 5))
            throw new InvalidDataException("ENV source checksum bucket exceeds its five-entry bound.");
        source = next;
        bank = nextBank;
        items = scene.Instances.Select((instance, index) => new GodotEnvironmentVisibilityItem(this, next, index,
            InitialFlags(instance.Flags, scene.Models[instance.ModelIndex]))).ToArray();
    }

    private static ushort InitialFlags(ushort flags, PsxModel model)
    {
        if ((model.Flags & 0x10) != 0) flags |= 0x20;
        if ((model.Flags & 0x20) != 0 || !PsxRenderPolicy.IsModelVisible(model)) flags |= 0x1000;
        return flags;
    }

    private void SynchronizeNative(GodotEnvironmentVisibilityItem item, ushort flags)
    {
        if (item.ChunkModel is not null) { SynchronizeChunkNative(item, flags); return; }
        ImportedLevelWorld world = currentWorld();
        string groupName = System.IO.Path.GetFileNameWithoutExtension(item.Source.AssetPath);
        Node3D geometry = world.Geometry.GetChildren().OfType<Node3D>().Single(group => group.Name.ToString().Equals(groupName, StringComparison.OrdinalIgnoreCase));
        Node3D collision = world.Collision.GetChildren().OfType<Node3D>().Single(group => group.Name.ToString().Equals(groupName, StringComparison.OrdinalIgnoreCase));
        ImportedSceneMesh? mesh = geometry.GetChildren().OfType<ImportedSceneMesh>().SingleOrDefault(mesh => mesh.SourceInstanceIndex == item.InstanceIndex);
        int modelIndex = item.Source.Scene.Instances[item.InstanceIndex].ModelIndex;
        bool renderVisible = (flags & 0x1001) == 0 && PsxRenderPolicy.IsModelVisible(item.Source.Scene.Models[modelIndex]);
        if (mesh is null && renderVisible)
        {
            PsxModel model = item.Source.Scene.Models[modelIndex];
            string texturesPath = item.Source.AssetPath[..^6] + "_L.psx";
            IReadOnlyList<DecodedTexture> textures = repository.Loaded.TryGetValue(texturesPath, out PsxAssetLibrary? textureBank)
                ? textureBank.Textures.Concat(bank!.Textures).ToArray() : bank!.Textures;
            ImportedGeometryRoot owner = geometry as ImportedGeometryRoot ??
                throw new InvalidOperationException("Deferred ENV visibility requires the actual imported bank's resource owner.");
            ArrayMesh? nativeMesh = owner.BuildOwnedModel(model, item.Source.Scene.ColorTable, textures);
            if (nativeMesh is not null)
            {
                mesh = new ImportedSceneMesh { Name = $"Instance{item.InstanceIndex}", SourceInstanceIndex = item.InstanceIndex,
                    SourceModelIndex = modelIndex, Mesh = nativeMesh,
                    Transform = PsxCoordinates.InstanceTransform(item.Source.Scene.Instances[item.InstanceIndex]) };
                geometry.AddChild(mesh);
            }
        }
        if (mesh is not null) { mesh.Visible = renderVisible; mesh.SetSourceTranslucency(SourceItemTranslucency.Alpha(flags)); }
        CollisionObject3D[] bodies = collision.GetChildren().OfType<CollisionObject3D>().Where(body => Matches(body, item)).ToArray();
        if (bodies.Length == 0 && OriginalCollisionFilter.IsInstanceEnabled(flags))
        {
            PsxInstance instance = item.Source.Scene.Instances[item.InstanceIndex] with { Flags = 0 };
            Node3D constructed = PsxWorldCollisionBuilder.Build(new(item.Source.Scene.Version, [instance],
                item.Source.Scene.Models, item.Source.Scene.ColorTable), item.Source.AssetPath);
            try
            {
                foreach (CollisionObject3D body in constructed.GetChildren().OfType<CollisionObject3D>().ToArray())
                {
                    if (body is ImportedCollisionBody terrain) terrain.InstanceIndex = item.InstanceIndex;
                    if (body is ImportedTriggerSurface trigger) trigger.InstanceIndex = item.InstanceIndex;
                    body.Name = $"Instance{item.InstanceIndex}_Layers{body.CollisionLayer}";
                    constructed.RemoveChild(body); collision.AddChild(body);
                }
            }
            finally { constructed.Free(); }
            bodies = collision.GetChildren().OfType<CollisionObject3D>().Where(body => Matches(body, item)).ToArray();
        }
        foreach (CollisionObject3D body in bodies)
        {
            if (!item.CollisionLayers.TryGetValue(body.GetInstanceId(), out uint layers))
                item.CollisionLayers.Add(body.GetInstanceId(), layers = body.CollisionLayer);
            body.CollisionLayer = OriginalCollisionFilter.IsInstanceEnabled(flags) ? layers : 0;
            if (body is ImportedCollisionBody terrain) terrain.SourceFlags = flags;
        }
    }

    private static bool Matches(CollisionObject3D body, GodotEnvironmentVisibilityItem item) => body switch
    {
        ImportedCollisionBody terrain => terrain.SourceAsset == item.Source.AssetPath && terrain.InstanceIndex == item.InstanceIndex,
        ImportedTriggerSurface trigger => trigger.SourceAsset == item.Source.AssetPath && trigger.InstanceIndex == item.InstanceIndex,
        _ => false
    };
}

/// <summary>A visibility view of one actual source ENV record, retaining its imported source and index.</summary>
public sealed class GodotEnvironmentVisibilityItem : IVisibilityBoxItem, IEnvironmentChunkSourceItem
{
    private readonly GodotEnvironmentVisibility owner;
    internal GodotEnvironmentVisibilityItem(GodotEnvironmentVisibility owner, ImportedLevelSource source, int index, ushort flags)
    {
        this.owner = owner; Source = source; InstanceIndex = index; StoredFlags = flags;
        PsxInstance instance = source.Scene.Instances[index];
        StoredPosition = new(GameWord.TruncateToInt32(instance.Position.X), GameWord.TruncateToInt32(instance.Position.Y), GameWord.TruncateToInt32(instance.Position.Z));
        StoredAngles = new((short)instance.Angles.X, (short)instance.Angles.Y, (short)instance.Angles.Z);
    }
    public ImportedLevelSource Source { get; }
    public int InstanceIndex { get; }
    public int ModelIndex => Source.Scene.Instances[InstanceIndex].ModelIndex;
    public uint ModelChecksum => Source.Scene.Models[ModelIndex].Checksum!.Value;
    public FixedVector3 SourcePosition => owner.ReadPosition(this);
    public bool RegionUsable => owner.IsRegionUsable(this);
    public ushort ItemFlags { get => owner.ReadFlags(this); set => owner.WriteChunkFlags(this, value); }
    public FixedVector3 Position { get => SourcePosition; set => owner.WriteChunkPosition(this, value); }
    public ActorAngles Angles { get => owner.ReadChunkAngles(this); set => owner.WriteChunkAngles(this, value); }
    public IEnvironmentChunkModel Model => ChunkModel ?? throw new InvalidOperationException("Actual source ENV model preparation has not been connected.");
    public void SetVisible(bool visible) => owner.SetItemVisibility(Source, InstanceIndex, visible);
    internal ushort StoredFlags { get; set; }
    internal FixedVector3 StoredPosition { get; set; }
    internal ActorAngles StoredAngles { get; set; }
    internal EnvironmentChunkModelState? ChunkModel { get; set; }
    internal int NativeModelRevision { get; set; } = -1;
    internal int NativeCollisionRevision { get; set; } = -1;
    internal Dictionary<ulong, uint> CollisionLayers { get; } = [];
}
