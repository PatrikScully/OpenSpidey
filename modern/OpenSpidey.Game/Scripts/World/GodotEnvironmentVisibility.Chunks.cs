using System.Buffers.Binary;
using Godot;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Core.Actors;
using OpenSpidey.Core.Mathematics;
using OpenSpidey.Core.World;
using OpenSpidey.Game.Actors;
using OpenSpidey.Game.Import;

namespace OpenSpidey.Game.World;

public sealed partial class GodotEnvironmentVisibility
{
    /// <summary>Bind original integer CItem records and prepared models to the same live ENV generation used by visibility and blasts.</summary>
    public ImportedLevelSource PrepareChunkSource(ReadOnlySpan<byte> bytes)
    {
        SynchronizeSource();
        ImportedLevelSource live = source ?? throw new InvalidOperationException("Chunk destruction requires the actual current ENV bank.");
        if (items.All(item => item.ChunkModel is not null)) return live;
        if (bytes.Length < 12 + items.Length * 36 || BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]) != items.Length)
            throw new InvalidDataException("Chunk source requires its genuine bounded CItem records.");
        IReadOnlyList<PreparedEnvironmentModel> models = PsxEnvironmentPreparation.Prepare(live.Scene, bytes, SourceTextures(live));
        for (int index = 0; index < items.Length; index++)
        {
            int offset = 12 + index * 36;
            int modelIndex = live.Scene.Instances[index].ModelIndex;
            if (BinaryPrimitives.ReadUInt16LittleEndian(bytes[(offset + 22)..]) != modelIndex)
                throw new InvalidDataException("Chunk source CItem model does not match its actual ENV owner.");
            GodotEnvironmentVisibilityItem item = items[index];
            item.StoredPosition = new(BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 4)..]),
                BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 8)..]), BinaryPrimitives.ReadInt32LittleEndian(bytes[(offset + 12)..]));
            item.StoredAngles = new(BinaryPrimitives.ReadInt16LittleEndian(bytes[(offset + 16)..]),
                BinaryPrimitives.ReadInt16LittleEndian(bytes[(offset + 18)..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[(offset + 20)..]));
            item.ChunkModel = new(models[modelIndex]);
            GodotSwitchEnvironmentItem? claimed = claimedItem(live, index);
            if (claimed is not null) item.StoredFlags = claimed.State.ItemFlags;
            if ((models[modelIndex].Model.Flags & 0x10) != 0) item.StoredFlags |= 0x20;
            if ((models[modelIndex].Model.Flags & 0x20) != 0) item.StoredFlags |= 0x1000;
            if (claimed is not null)
            { claimed.State.ItemFlags = item.StoredFlags; claimed.InstallSharedChunkModel(item.ChunkModel); }
        }
        return live;
    }
    public FixedVector3 SourceItemPosition(ImportedLevelSource source, int index) => RequireItem(source, index).Position;
    public bool SourceItemPrepared(ImportedLevelSource source, int index) => RequireItem(source, index).ChunkModel is not null;
    public ActorAngles SourceItemAngles(ImportedLevelSource source, int index) => RequireItem(source, index).Angles;
    public ushort SourceModelFlags(ImportedLevelSource source, int index)
    {
        GodotEnvironmentVisibilityItem item = RequireItem(source, index);
        return claimedItem(source, index)?.SourceModelFlags ?? item.ChunkModel?.Flags ?? source.Scene.Models[item.ModelIndex].Flags;
    }
    public void SynchronizeSourceItem(GodotEnvironmentVisibilityItem item)
    {
        RequireItem(item.Source, item.InstanceIndex);
        if (claimedItem(item.Source, item.InstanceIndex) is { } claimed)
        {
            claimed.InstallSharedChunkModel(item.ChunkModel ?? throw new InvalidOperationException("Actual claimed chunk model preparation is missing."));
            claimed.Synchronize(); return;
        }
        SynchronizeNative(item, item.StoredFlags);
    }
    internal void WriteChunkFlags(GodotEnvironmentVisibilityItem item, ushort flags)
    {
        RequireItem(item.Source, item.InstanceIndex); item.StoredFlags = flags;
        if (claimedItem(item.Source, item.InstanceIndex) is { } claimed) claimed.State.ItemFlags = flags;
    }
    internal void WriteChunkPosition(GodotEnvironmentVisibilityItem item, FixedVector3 position)
    {
        RequireItem(item.Source, item.InstanceIndex);
        if (claimedItem(item.Source, item.InstanceIndex) is { } claimed) claimed.State.Position = position;
        item.StoredPosition = position;
    }
    internal ActorAngles ReadChunkAngles(GodotEnvironmentVisibilityItem item)
    {
        RequireItem(item.Source, item.InstanceIndex);
        return claimedItem(item.Source, item.InstanceIndex)?.SourceAngles ?? item.StoredAngles;
    }
    internal void WriteChunkAngles(GodotEnvironmentVisibilityItem item, ActorAngles angles)
    {
        RequireItem(item.Source, item.InstanceIndex);
        if (claimedItem(item.Source, item.InstanceIndex) is { } claimed) claimed.State.Angles = angles;
        item.StoredAngles = angles;
    }
    private IReadOnlyList<DecodedTexture> SourceTextures(ImportedLevelSource source)
    {
        string texturesPath = source.AssetPath[..^6] + "_L.psx";
        return repository.Loaded.TryGetValue(texturesPath, out PsxAssetLibrary? textures)
            ? textures.Textures.Concat(bank!.Textures).ToArray() : bank!.Textures;
    }
    private void SynchronizeChunkNative(GodotEnvironmentVisibilityItem item, ushort flags)
    {
        EnvironmentChunkModelState model = item.ChunkModel!;
        ImportedLevelWorld world = currentWorld();
        string groupName = System.IO.Path.GetFileNameWithoutExtension(item.Source.AssetPath);
        ImportedGeometryRoot geometry = world.Geometry.GetChildren().OfType<ImportedGeometryRoot>().Single(group =>
            group.Name.ToString().Equals(groupName, StringComparison.OrdinalIgnoreCase));
        Node3D collision = world.Collision.GetChildren().OfType<Node3D>().Single(group => group.Name.ToString().Equals(groupName, StringComparison.OrdinalIgnoreCase));
        ImportedSceneMesh? mesh = geometry.GetChildren().OfType<ImportedSceneMesh>().SingleOrDefault(mesh => mesh.SourceInstanceIndex == item.InstanceIndex);
        PsxModel render = model.RenderSnapshot();
        bool visible = (flags & 0x1001) == 0 && (model.Flags & 0x20) == 0 && render.Faces.Any(face => (face.Flags & 0xC0) != 0);
        if (visible && (mesh is null || item.NativeModelRevision != model.Revision))
        {
            ArrayMesh? owned = geometry.BuildOwnedModel(render, item.Source.Scene.ColorTable, SourceTextures(item.Source), sourceFacesPrepared: true);
            if (owned is not null)
            {
                if (mesh is null)
                {
                    mesh = new() { Name = $"Instance{item.InstanceIndex}", SourceInstanceIndex = item.InstanceIndex, SourceModelIndex = item.InstanceIndex };
                    geometry.AddChild(mesh);
                }
                mesh.Mesh = owned;
            }
            item.NativeModelRevision = model.Revision;
        }
        Transform3D transform = SourceItemTransform(item);
        if (mesh is not null)
        {
            mesh.Transform = transform; mesh.Visible = visible;
            // RenderSuperItem 475D61: chunk preparation's DCModelData flag4000 selects DC_PSXModel_RenderModel.
            mesh.SetSourceTranslucency(SourceItemTranslucency.Alpha(flags,
                (model.RendererFlags & 0x4000) != 0 ? SourceItemTranslucency.PsxModelAlpha : SourceItemTranslucency.ModelAlpha));
        }
        CollisionObject3D[] bodies = collision.GetChildren().OfType<CollisionObject3D>().Where(body => Matches(body, item)).ToArray();
        if (item.NativeCollisionRevision != model.Revision)
        {
            foreach (CollisionObject3D body in bodies)
            {
                item.CollisionLayers.Remove(body.GetInstanceId());
                if (body.GetNodeOrNull<GodotNativeDestructionObserver>("OriginalChunkCollisionResources") is null)
                    foreach (CollisionShape3D child in body.GetChildren().OfType<CollisionShape3D>())
                    { Shape3D? shape = child.Shape; child.Shape = null; shape?.Dispose(); }
                collision.RemoveChild(body); body.Free();
            }
            var instance = item.Source.Scene.Instances[item.InstanceIndex] with { Flags = 0, Position = default, Angles = default };
            var models = item.Source.Scene.Models.ToArray(); models[item.InstanceIndex] = model.CollisionSnapshot();
            Node3D constructed = PsxWorldCollisionBuilder.Build(new(item.Source.Scene.Version, [instance], models, item.Source.Scene.ColorTable), item.Source.AssetPath);
            try
            {
                foreach (CollisionObject3D body in constructed.GetChildren().OfType<CollisionObject3D>().ToArray())
                {
                    if (body is ImportedCollisionBody terrain) terrain.InstanceIndex = item.InstanceIndex;
                    if (body is ImportedTriggerSurface trigger) trigger.InstanceIndex = item.InstanceIndex;
                    body.Name = $"Instance{item.InstanceIndex}_Layers{body.CollisionLayer}";
                    constructed.RemoveChild(body); collision.AddChild(body);
                    item.CollisionLayers[body.GetInstanceId()] = body.CollisionLayer;
                    Shape3D[] ownedShapes = body.GetChildren().OfType<CollisionShape3D>().Select(child => child.Shape).OfType<Shape3D>().ToArray();
                    body.AddChild(new GodotNativeDestructionObserver { Name = "OriginalChunkCollisionResources",
                        Destroyed = () => { foreach (Shape3D shape in ownedShapes) shape.Dispose(); } });
                }
            }
            finally { constructed.Free(); }
            item.NativeCollisionRevision = model.Revision;
            bodies = collision.GetChildren().OfType<CollisionObject3D>().Where(body => Matches(body, item)).ToArray();
        }
        foreach (CollisionObject3D body in bodies)
        {
            if (!item.CollisionLayers.TryGetValue(body.GetInstanceId(), out uint layers))
                item.CollisionLayers.Add(body.GetInstanceId(), layers = body.CollisionLayer);
            body.Transform = transform;
            body.CollisionLayer = OriginalCollisionFilter.IsInstanceEnabled(flags) ? layers : 0;
            if (body is ImportedCollisionBody terrain)
            { terrain.SourceFlags = flags; terrain.SourceRotation = PsxCoordinates.OriginalRotation(transform.Basis); }
        }
    }
    private static Transform3D SourceItemTransform(GodotEnvironmentVisibilityItem item)
    {
        ActorAngles angles = item.Angles;
        Transform3D result = PsxCoordinates.CharacterTransform(OpenSpidey.Assets.Animation.FixedJointTransform.FromJoint(new(angles.X, angles.Y, angles.Z, 0, 0, 0)));
        result.Origin = PsxCoordinates.FixedWorldPosition(item.Position);
        return result;
    }
}
