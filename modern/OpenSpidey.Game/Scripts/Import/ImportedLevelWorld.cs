using Godot;
using OpenSpidey.Assets.Archives;
using OpenSpidey.Assets.Models;
using OpenSpidey.Assets.Textures;
using OpenSpidey.Game.World;

namespace OpenSpidey.Game.Import;

public sealed record ImportedLevelSource(string AssetPath, PsxScene Scene);

/// <summary>A level's world and object libraries share textures but retain distinct source identities.</summary>
public partial class ImportedLevelWorld : Node3D
{
    private readonly Dictionary<string, (Node3D Geometry, Node3D Collision)> sourceNodes = new(StringComparer.OrdinalIgnoreCase);
    public Node3D Geometry { get; private set; } = null!;
    public Node3D Collision { get; private set; } = null!;
    public IReadOnlyList<ImportedLevelSource> Sources { get; private set; } = [];
    public int InstanceCount => Sources.Sum(source => source.Scene.Instances.Count);

    public static ImportedLevelWorld Load(PkrArchive archive, string geometryAsset)
        => Load(new PsxLibraryRepository(archive), geometryAsset);

    public static ImportedLevelWorld Load(PsxLibraryRepository libraries, string geometryAsset)
    {
        if (!geometryAsset.EndsWith("_G.psx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Level worlds require a _G.psx geometry file.", nameof(geometryAsset));
        var world = new ImportedLevelWorld { Name = "LevelWorld" };
        try
        {
            world.Geometry = new Node3D { Name = "Geometry" };
            world.Collision = new Node3D { Name = "Collision" };
            world.AddChild(world.Geometry);
            world.AddChild(world.Collision);
            var textures = new List<DecodedTexture>();
            string library = geometryAsset[..^6] + "_L.psx";
            if (libraries.Contains(library)) textures.AddRange(libraries.Load(library).Textures);
            var sources = new List<ImportedLevelSource>();
            LoadSource(geometryAsset);
            string objects = geometryAsset[..^6] + "_O.psx";
            if (libraries.Contains(objects)) LoadSource(objects);
            world.Sources = sources.AsReadOnly();
            return world;

            void LoadSource(string asset)
            {
                PsxAssetLibrary sourceLibrary = libraries.Load(asset);
                PsxScene scene = sourceLibrary.Scene;
                var sourceTextures = textures.Concat(sourceLibrary.Textures).ToArray();
                Node3D geometry = PsxGeometryBuilder.Build(scene, sourceTextures);
                geometry.Name = System.IO.Path.GetFileNameWithoutExtension(asset);
                world.Geometry.AddChild(geometry);
                Node3D collision = PsxWorldCollisionBuilder.Build(scene, asset);
                collision.Name = geometry.Name;
                world.Collision.AddChild(collision);
                world.sourceNodes.Add(asset, (geometry, collision));
                sources.Add(new(asset, scene));
            }
        }
        catch
        {
            world.Free();
            throw;
        }
    }

    public void UnloadSource(string assetPath)
    {
        if (!sourceNodes.Remove(assetPath, out var nodes)) return;
        Geometry.RemoveChild(nodes.Geometry);
        Collision.RemoveChild(nodes.Collision);
        nodes.Geometry.Free();
        nodes.Collision.Free();
        Sources = Array.AsReadOnly(Sources.Where(source => !source.AssetPath.Equals(assetPath, StringComparison.OrdinalIgnoreCase)).ToArray());
    }

    public void ClaimObjectModel(string assetPath, int modelIndex)
    {
        if (!sourceNodes.TryGetValue(assetPath, out var nodes)) return;
        foreach (ImportedSceneMesh mesh in nodes.Geometry.GetChildren().OfType<ImportedSceneMesh>()
            .Where(mesh => mesh.SourceModelIndex == modelIndex))
        {
            nodes.Geometry.RemoveChild(mesh);
            mesh.QueueFree();
        }
        foreach (Node body in nodes.Collision.GetChildren())
        {
            int? sourceModel = body switch { ImportedCollisionBody terrain => terrain.ModelIndex,
                ImportedTriggerSurface trigger => trigger.ModelIndex, _ => null };
            if (sourceModel != modelIndex) continue;
            if (body is CollisionObject3D collider) collider.CollisionLayer = 0;
            nodes.Collision.RemoveChild(body);
            body.QueueFree();
        }
    }
}
